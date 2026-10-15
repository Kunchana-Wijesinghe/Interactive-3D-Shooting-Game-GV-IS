using System.IO;
using UnityEditor;
using UnityEngine;

namespace FacilityBreach.Editor
{
    public static class WeaponModelPrefabBuilder
    {
        [MenuItem("Facility Breach/Build Weapon Prefabs Phase 3")]
        public static void BuildAllWeaponPrefabs()
        {
            EnsureDirectories();
            CreateWeaponMaterials();
            CreateWeaponPrefabs();
            ValidateWeaponModels();
        }

        private static void EnsureDirectories()
        {
            Directory.CreateDirectory("Assets/Materials/FacilityBreach/Weapons");
            Directory.CreateDirectory("Assets/Prefabs/FacilityBreach/Weapons");
        }

        private static void CreateWeaponMaterials()
        {
            CreateMat("Assets/Materials/FacilityBreach/Weapons/Ammo1gun_Mat.mat", new Color(0.2f, 0.6f, 0.9f));
            CreateMat("Assets/Materials/FacilityBreach/Weapons/Ammo2gun_Mat.mat", new Color(0.9f, 0.7f, 0.1f));
            CreateMat("Assets/Materials/FacilityBreach/Weapons/Ammo3gun_Mat.mat", new Color(0.9f, 0.2f, 0.2f));
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

        private static void CreateWeaponPrefabs()
        {
            BuildWeapon("Ammo1gun", "Assets/Models/FacilityBreach/Weapons/Ammo1gun.fbx", "Assets/Materials/FacilityBreach/Weapons/Ammo1gun_Mat.mat");
            BuildWeapon("Ammo2gun", "Assets/Models/FacilityBreach/Weapons/Ammo2gun.fbx", "Assets/Materials/FacilityBreach/Weapons/Ammo2gun_Mat.mat");
            BuildWeapon("Ammo3gun", "Assets/Models/FacilityBreach/Weapons/Ammo3gun.fbx", "Assets/Materials/FacilityBreach/Weapons/Ammo3gun_Mat.mat");
        }

        private static void BuildWeapon(string weaponName, string fbxPath, string matPath)
        {
            var fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (!fbxModel) return;

            // View Model Prefab
            var fpRoot = new GameObject(weaponName + "_ViewModel");
            var visual = Object.Instantiate(fbxModel, fpRoot.transform);
            visual.name = "VisualModel";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat)
            {
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial = mat;
            }

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(fpRoot.transform);
            muzzle.transform.localPosition = new Vector3(0, 0.1f, 0.6f);

            var grip = new GameObject("GripAttachment");
            grip.transform.SetParent(fpRoot.transform);
            grip.transform.localPosition = new Vector3(0, -0.1f, -0.1f);

            string fpPath = $"Assets/Prefabs/FacilityBreach/Weapons/{weaponName}_ViewModel.prefab";
            PrefabUtility.SaveAsPrefabAsset(fpRoot, fpPath);
            Object.DestroyImmediate(fpRoot);

            // Pickup World Prefab
            var pickupRoot = new GameObject(weaponName + "_Pickup");
            var pickupVisual = Object.Instantiate(fbxModel, pickupRoot.transform);
            pickupVisual.name = "VisualModel";
            pickupVisual.transform.localScale = Vector3.one * 0.8f;
            if (mat)
            {
                foreach (var renderer in pickupVisual.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial = mat;
            }

            var boxCol = pickupRoot.AddComponent<BoxCollider>();
            boxCol.size = new Vector3(0.3f, 0.3f, 0.8f);

            string pickupPath = $"Assets/Prefabs/FacilityBreach/Weapons/{weaponName}_Pickup.prefab";
            PrefabUtility.SaveAsPrefabAsset(pickupRoot, pickupPath);
            Object.DestroyImmediate(pickupRoot);
        }

        public static void ValidateWeaponModels()
        {
            int count = 0;
            string[] names = { "Ammo1gun", "Ammo2gun", "Ammo3gun" };
            foreach (var name in names)
            {
                string fbx = $"Assets/Models/FacilityBreach/Weapons/{name}.fbx";
                string blend = $"ArtSource/FacilityBreach/Weapons/{name}/{name}.blend";
                if (File.Exists(fbx) && File.Exists(blend)) count++;
            }

            Debug.Log($"[Facility Breach Phase 3] First-Person Weapon Models Validated: {count}/3 models & blends present.");
        }
    }
}
