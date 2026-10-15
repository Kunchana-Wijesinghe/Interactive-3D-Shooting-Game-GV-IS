using Perera.Combat;
using Perera.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Perera.EditorTools
{
    /// <summary>Builds Assets/Perera/Scenes/Perera_CombatTest.unity from primitives (placeholders).</summary>
    public static class PereraCombatTestSceneBuilder
    {
        private const string ScenePath = "Assets/Perera/Scenes/Perera_CombatTest.unity";

        [MenuItem("Perera/Build Combat Test Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Prim("Floor", PrimitiveType.Cube, new Vector3(0, -0.25f, 8), new Vector3(30, 0.5f, 40));
            Prim("Wall_Cover", PrimitiveType.Cube, new Vector3(0, 1, 6), new Vector3(4, 2, 0.4f));
            Prim("Wall_Back", PrimitiveType.Cube, new Vector3(0, 2, 28), new Vector3(30, 4, 0.4f));

            // Targets: one in the open, one hidden behind the cover wall.
            Target("Dummy_Open", new Vector3(-5, 1, 14));
            Target("Dummy_BehindCover", new Vector3(0, 1, 10));

            // Player rig (placeholder for Bandara's controller).
            var player = new GameObject("Player_TestRig") { tag = "Player" };
            player.transform.position = new Vector3(0, 1.1f, -6);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f;
            player.AddComponent<Health>();

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(player.transform, false);
            pivot.transform.localPosition = new Vector3(0, 0.7f, 0);

            var camGo = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivot.transform, false);
            camGo.transform.localPosition = Vector3.zero;
            camGo.transform.localRotation = Quaternion.identity;
            if (camGo.GetComponent<Camera>() == null) camGo.AddComponent<Camera>();
            camGo.GetComponent<Camera>().nearClipPlane = 0.05f;

            var weapon = new GameObject("Weapon_Placeholder");
            weapon.transform.SetParent(camGo.transform, false);
            weapon.transform.localPosition = new Vector3(0.25f, -0.2f, 0.5f);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "WeaponBody_PLACEHOLDER";
            body.transform.SetParent(weapon.transform, false);
            body.transform.localScale = new Vector3(0.08f, 0.1f, 0.5f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(weapon.transform, false);
            muzzle.transform.localPosition = new Vector3(0, 0.02f, 0.3f);

            var hw = weapon.AddComponent<HitscanWeapon>();
            var so = new SerializedObject(hw);
            so.FindProperty("aimCamera").objectReferenceValue = camGo.GetComponent<Camera>();
            so.FindProperty("muzzle").objectReferenceValue = muzzle.transform;
            so.FindProperty("owner").objectReferenceValue = player.transform;
            so.FindProperty("ownerHealth").objectReferenceValue = player.GetComponent<Health>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var ctrl = player.AddComponent<TestFpsController>();
            var cso = new SerializedObject(ctrl);
            cso.FindProperty("cameraPivot").objectReferenceValue = pivot.transform;
            cso.ApplyModifiedPropertiesWithoutUndo();

            player.AddComponent<CombatDebugHud>();

            System.IO.Directory.CreateDirectory("Assets/Perera/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Perera combat test scene saved: " + ScenePath);
        }

        private static GameObject Prim(string name, PrimitiveType t, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name; go.transform.position = pos; go.transform.localScale = scale;
            return go;
        }

        private static void Target(string name, Vector3 pos)
        {
            var go = Prim(name + "_PLACEHOLDER", PrimitiveType.Capsule, pos, Vector3.one);
            var h = go.AddComponent<Health>();
            var so = new SerializedObject(h); so.FindProperty("debugLog").boolValue = true; so.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<DeathHide>();
        }
    }
}
