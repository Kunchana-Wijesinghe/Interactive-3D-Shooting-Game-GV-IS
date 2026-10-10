using System.IO;
using UnityEditor;
using UnityEngine;

namespace FacilityBreach.Editor
{
    public static class EnemyModelPrefabBuilder
    {
        [MenuItem("Facility Breach/Build Enemy Prefabs Phase 2")]
        public static void BuildAllEnemyPrefabs()
        {
            EnsureDirectories();
            CreateEnemyMaterials();
            CreateVisualPrefabs();
            ValidateEnemyModels();
        }

        private static void EnsureDirectories()
        {
            Directory.CreateDirectory("Assets/Materials/FacilityBreach/Enemies");
            Directory.CreateDirectory("Assets/Prefabs/FacilityBreach/Enemies");
            Directory.CreateDirectory("docs/facility-breach");
        }

        private static void CreateEnemyMaterials()
        {
            CreateMat("Assets/Materials/FacilityBreach/Enemies/PG_Teal.mat", new Color(0, 0.8f, 0.8f));
            CreateMat("Assets/Materials/FacilityBreach/Enemies/SH_Orange.mat", new Color(1, 0.5f, 0));
            CreateMat("Assets/Materials/FacilityBreach/Enemies/CD_Violet.mat", new Color(0.6f, 0.2f, 0.8f));
            CreateMat("Assets/Materials/FacilityBreach/Enemies/FL_Lime.mat", new Color(0.7f, 1, 0.2f));
            CreateMat("Assets/Materials/FacilityBreach/Enemies/ChestStateLight.mat", new Color(0, 0.8f, 0.8f));
            AssetDatabase.Refresh();
        }

        private static void CreateMat(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Standard"));
                mat.color = color;
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.color = color;
                EditorUtility.SetDirty(mat);
            }
        }

        private static void CreateVisualPrefabs()
        {
            BuildPrefab("PG-01", "Assets/Models/FacilityBreach/Enemies/PatrolGuard.fbx", "Assets/Materials/FacilityBreach/Enemies/PG_Teal.mat", "Assets/Prefabs/FacilityBreach/Enemies/PatrolGuard_PG01.prefab");
            BuildPrefab("SH-01", "Assets/Models/FacilityBreach/Enemies/SoundHunter.fbx", "Assets/Materials/FacilityBreach/Enemies/SH_Orange.mat", "Assets/Prefabs/FacilityBreach/Enemies/SoundHunter_SH01.prefab");
            BuildPrefab("CD-03", "Assets/Models/FacilityBreach/Enemies/CoverDefender.fbx", "Assets/Materials/FacilityBreach/Enemies/CD_Violet.mat", "Assets/Prefabs/FacilityBreach/Enemies/CoverDefender_CD03.prefab");
            BuildPrefab("FL-04", "Assets/Models/FacilityBreach/Enemies/Flanker.fbx", "Assets/Materials/FacilityBreach/Enemies/FL_Lime.mat", "Assets/Prefabs/FacilityBreach/Enemies/FL_Lime_FL04.prefab");
        }

        private static void BuildPrefab(string identity, string fbxPath, string matPath, string prefabPath)
        {
            var fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (!fbxModel) return;

            var root = new GameObject(identity + "_VisualPrefab");
            var visual = Object.Instantiate(fbxModel, root.transform);
            visual.name = "ModelVisual";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat)
            {
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = mat;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        public static void ValidateEnemyModels()
        {
            int count = 0;
            string[] names = { "PatrolGuard", "SoundHunter", "CoverDefender", "Flanker" };
            foreach (var name in names)
            {
                string fbx = $"Assets/Models/FacilityBreach/Enemies/{name}.fbx";
                string blend = $"ArtSource/FacilityBreach/Enemies/{name}/{name}.blend";
                if (File.Exists(fbx) && File.Exists(blend)) count++;
            }

            Debug.Log($"[Facility Breach Phase 2] Enemy Models Validated: {count}/4 models & blends present.");
        }
    }
}
