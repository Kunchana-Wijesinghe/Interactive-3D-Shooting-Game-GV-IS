using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityBreach.Editor
{
    public static class PlayModeIntegrationSetup
    {
        // Run in the isolated copy. Generates only new gameplay assets; never saves the environment.
        public static void Prepare()
        {
            string[] enemies = { "PatrolGuard", "SoundHunter", "CoverDefender", "Flanker" };
            for (int i = 0; i < enemies.Length; i++) ModelPrefab("Enemies", enemies[i], enemies[i] + "_Visual", 1.8f, false);
            for (int i = 0; i < 3; i++)
            {
                string name = "Ammo" + (i + 1) + "gun";
                ModelPrefab("Weapons", name, name + "_ViewModel", .65f + i * .1f, true);
                ModelPrefab("Weapons", name, name + "_Pickup", .65f + i * .1f, true);
            }
            EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
            var floor = GameObject.Find("Facility_Floor").GetComponent<Collider>().bounds;
            var bounds = new Bounds(new Vector3(floor.center.x, 2, floor.center.z), new Vector3(floor.size.x, 6, floor.size.z));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(source =>
            {
                var col = source.component as Collider;
                return col && (col.GetComponentInParent<InteractiveDoor>() ||
                    col.transform.root.name.Contains("Roof") || col.bounds.min.y > 3 ||
                    (col.attachedRigidbody && !col.attachedRigidbody.isKinematic));
            });
            var settings = NavMesh.GetSettingsByIndex(0); settings.agentRadius = .35f; settings.agentHeight = 1.8f;
            settings.overrideVoxelSize = true; settings.voxelSize = .1f;
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (!data) throw new Exception("Scoped navigation bake failed");
            const string folder = "Assets/Navigation/FacilityBreach";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            string path = folder + "/PrototypeGameplayNavMesh.asset";
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(path)) throw new Exception("Refusing to overwrite existing navigation asset");
            AssetDatabase.CreateAsset(data, path); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            ExportReferences();
            Debug.Log("FB integration assets prepared; environment scene not saved");
        }
        [Serializable] private class AssetLink { public string path, guid; public long fileId; }
        [Serializable] private class Links { public List<AssetLink> assets = new List<AssetLink>(); }
        private static void Link(Links links, string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long fileId);
            links.assets.Add(new AssetLink { path = path, guid = guid, fileId = fileId });
        }
        public static void ExportReferences()
        {
            var links = new Links();
            foreach (string name in new[] { "PatrolGuard", "SoundHunter", "CoverDefender", "Flanker" }) Link(links, $"Assets/Prefabs/FacilityBreach/Enemies/{name}_Visual.prefab");
            for (int i = 1; i <= 3; i++) Link(links, $"Assets/Prefabs/FacilityBreach/Weapons/Ammo{i}gun_ViewModel.prefab");
            for (int i = 1; i <= 3; i++) Link(links, $"Assets/Prefabs/FacilityBreach/Weapons/Ammo{i}gun_Pickup.prefab");
            Link(links, "Assets/Navigation/FacilityBreach/PrototypeGameplayNavMesh.asset");
            File.WriteAllText("integration-references.json", JsonUtility.ToJson(links, true));
        }
        private static Transform Named(GameObject root, string name)
        { foreach (var transform in root.GetComponentsInChildren<Transform>()) if (transform.name == name) return transform; return null; }
        private static Bounds ModelBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(); if (renderers.Length == 0) throw new Exception("Missing model renderers: " + root.name);
            Bounds bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds); return bounds;
        }
        private static void ModelPrefab(string category, string name, string prefabName, float size, bool weapon)
        {
            string modelPath = $"Assets/Models/FacilityBreach/{category}/{name}.fbx";
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath); if (!imported) throw new Exception("Missing FBX: " + modelPath);
            var root = new GameObject(prefabName); var visual = UnityEngine.Object.Instantiate(imported, root.transform); visual.name = "ModelVisual";
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            Bounds bounds = ModelBounds(visual);
            float dimension = weapon ? Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) : bounds.size.y;
            visual.transform.localScale *= size / dimension;
            if (weapon)
            {
                var muzzle = Named(visual, "Muzzle"); var grip = Named(visual, "GripAttachment");
                if (!muzzle || !grip) throw new Exception("Missing exported weapon attachment points: " + name);
                Vector3 forward = muzzle.position - grip.position; forward.y = 0;
                visual.transform.localRotation = Quaternion.FromToRotation(forward.normalized, Vector3.forward) * visual.transform.localRotation;
                visual.transform.localPosition -= grip.position;
            }
            else
            {
                bounds = ModelBounds(visual);
                visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }
            string folder = $"Assets/Prefabs/FacilityBreach/{category}"; Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            string path = folder + "/" + prefabName + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) throw new Exception("Refusing to overwrite existing visual prefab: " + path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"FB model {name} bounds={ModelBounds(root).size} prefab={path}");
            UnityEngine.Object.DestroyImmediate(root);
        }
        [MenuItem("Facility Breach/Hide light gizmo icons")]
        public static void HideLightIcons()
        { GizmoUtility.SetIconEnabled(typeof(Light), false); GizmoUtility.SetGizmoEnabled(typeof(Light), false); }
    }
}
