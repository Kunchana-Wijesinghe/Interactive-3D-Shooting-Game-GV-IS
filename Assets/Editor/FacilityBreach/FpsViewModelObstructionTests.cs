using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FacilityBreach;
using FacilityBreach.Interactions;

namespace FacilityBreach.Editor
{
    /// <summary>
    /// Automated Edit Mode and batch-mode regression unit tests for FpsViewModelObstruction.
    /// Evaluates distance-based procedural retraction, low-ready posing, multi-weapon reach adaptation,
    /// obstacle filtering (player, weapon, triggers, stations, held barricades), and swap reinitialization.
    /// </summary>
    public static class FpsViewModelObstructionTests
    {
        public static Action<string> Log = msg => Debug.Log(msg);
        public static Action<string> LogError = msg => Debug.LogError(msg);

        private static int passedCount;
        private static int failedCount;
        private static readonly List<string> failureMessages = new List<string>();

        [MenuItem("Facility Breach/Run FPS View Model Obstruction Tests")]
        public static void RunAll()
        {
            passedCount = 0;
            failedCount = 0;
            failureMessages.Clear();

            Log("<color=cyan>=== [FpsViewModelObstructionTests] Starting Test Suite ===</color>");

            Test_DefaultPose_WhenUnobstructed();
            Test_RetractionAndLowering_WhenObstacleDetected();
            Test_RetractionBounds_NeverExceedsMaximum();
            Test_AllThreeWeapons_AdaptReachFromMuzzle();
            Test_Filter_ExcludesPlayerAndWeaponColliders();
            Test_Filter_ExcludesResourceStations();
            Test_Filter_ExcludesHeldBarricades();
            Test_Filter_ExcludesTriggerVolumes();
            Test_WeaponSwap_ReinitializesPoseAndTracking();
            Test_PhysicsQuery_DetectsSolidObstacle();

            if (failedCount == 0)
            {
                Log($"<color=green>[FpsViewModelObstructionTests] Completed {passedCount} checks: {passedCount} PASSED, 0 FAILED.</color>");
            }
            else
            {
                LogError($"<color=red>[FpsViewModelObstructionTests] FAILED: {failedCount} failures out of {passedCount + failedCount} checks.</color>");
                foreach (var err in failureMessages) LogError("  - " + err);
            }
        }

        public static void RunBatch()
        {
            RunAll();
            EditorApplication.Exit(failedCount == 0 ? 0 : 1);
        }

        private static void Assert(bool condition, string testName, string message = "")
        {
            if (condition)
            {
                passedCount++;
                Log($"[Obstruction Test PASS] {testName}");
            }
            else
            {
                failedCount++;
                string err = $"FAIL: {testName} - {message}";
                failureMessages.Add(err);
                LogError($"[Obstruction Test FAIL] {err}");
            }
        }

        private static void Test_DefaultPose_WhenUnobstructed()
        {
            var camObj = new GameObject("TestCam");
            var cam = camObj.AddComponent<Camera>();
            var weapon = new GameObject("TestWeapon");
            weapon.transform.SetParent(camObj.transform);
            weapon.transform.localPosition = new Vector3(0.18f, -0.22f, 0.5f);
            weapon.transform.localRotation = Quaternion.identity;

            var obstruction = camObj.AddComponent<FpsViewModelObstruction>();
            obstruction.Initialize(cam, null, weapon.transform, null);

            obstruction.CalculateTargetPose(float.PositiveInfinity, out var targetPos, out var targetRot);
            Assert(Vector3.Distance(targetPos, weapon.transform.localPosition) < 0.001f, "Unobstructed distance infinity retains default position");
            Assert(Quaternion.Angle(targetRot, weapon.transform.localRotation) < 0.01f, "Unobstructed distance infinity retains default rotation");

            obstruction.CalculateTargetPose(1.5f, out targetPos, out targetRot);
            Assert(Vector3.Distance(targetPos, weapon.transform.localPosition) < 0.001f, "Clearance distance 1.5m retains default position");
            Assert(Quaternion.Angle(targetRot, weapon.transform.localRotation) < 0.01f, "Clearance distance 1.5m retains default rotation");

            UnityEngine.Object.DestroyImmediate(camObj);
        }

        private static void Test_RetractionAndLowering_WhenObstacleDetected()
        {
            var camObj = new GameObject("TestCam");
            var cam = camObj.AddComponent<Camera>();
            var weapon = new GameObject("TestWeapon");
            weapon.transform.SetParent(camObj.transform);
            weapon.transform.localPosition = new Vector3(0.18f, -0.22f, 0.5f);

            var obstruction = camObj.AddComponent<FpsViewModelObstruction>();
            obstruction.Initialize(cam, null, weapon.transform, null);

            // Obstacle detected at 0.35m (typical distance when player capsule touches wall)
            obstruction.CalculateTargetPose(0.35f, out var targetPos, out var targetRot);

            Assert(targetPos.z < weapon.transform.localPosition.z, "Retraction slides weapon backward along local Z", $"Z was {targetPos.z} vs default {weapon.transform.localPosition.z}");
            Assert(targetPos.y < weapon.transform.localPosition.y, "Low-ready lowers weapon downward along local Y", $"Y was {targetPos.y} vs default {weapon.transform.localPosition.y}");

            Vector3 euler = targetRot.eulerAngles;
            // Downward pitch will be > 0 in euler
            float pitch = euler.x > 180 ? euler.x - 360 : euler.x;
            Assert(pitch > 5f, "Weapon tilts downward in low-ready stance", $"Pitch was {pitch}");

            UnityEngine.Object.DestroyImmediate(camObj);
        }

        private static void Test_RetractionBounds_NeverExceedsMaximum()
        {
            var camObj = new GameObject("TestCam");
            var cam = camObj.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            var weapon = new GameObject("TestWeapon");
            weapon.transform.SetParent(camObj.transform);
            weapon.transform.localPosition = new Vector3(0.18f, -0.22f, 0.5f);

            var obstruction = camObj.AddComponent<FpsViewModelObstruction>();
            obstruction.Initialize(cam, null, weapon.transform, null);

            // Extreme close-up / zero distance
            obstruction.CalculateTargetPose(0.01f, out var targetPos, out var targetRot);

            float minAllowedZ = 0.5f - obstruction.MaxRetractZ;
            Assert(targetPos.z >= minAllowedZ - 0.001f, "Extreme close-up respects MaxRetractZ limit", $"targetPos.z: {targetPos.z}, minAllowedZ: {minAllowedZ}");
            Assert(targetPos.z > cam.nearClipPlane + 0.1f, "Retracted weapon remains safely ahead of camera nearClipPlane", $"targetPos.z: {targetPos.z}, nearClip: {cam.nearClipPlane}");

            UnityEngine.Object.DestroyImmediate(camObj);
        }

        private static void Test_AllThreeWeapons_AdaptReachFromMuzzle()
        {
            var camObj = new GameObject("TestCam");
            var cam = camObj.AddComponent<Camera>();
            var obstruction = camObj.AddComponent<FpsViewModelObstruction>();

            // Ammo1gun: compact rifle with muzzle at z ~ 0.53m in view model
            var w1 = new GameObject("Ammo1gun_View");
            w1.transform.SetParent(camObj.transform);
            w1.transform.localPosition = new Vector3(0.18f, -0.22f, 0.5f);
            var m1 = new GameObject("Muzzle");
            m1.transform.SetParent(w1.transform);
            m1.transform.localPosition = new Vector3(0f, 0.07f, 0.45f);

            obstruction.Initialize(cam, null, w1.transform, m1.transform);
            float reach1 = obstruction.WeaponReach;

            // Ammo2gun: medium rifle with muzzle further forward
            var w2 = new GameObject("Ammo2gun_View");
            w2.transform.SetParent(camObj.transform);
            w2.transform.localPosition = new Vector3(0.18f, -0.22f, 0.5f);
            var m2 = new GameObject("Muzzle");
            m2.transform.SetParent(w2.transform);
            m2.transform.localPosition = new Vector3(0f, 0.07f, 0.55f);

            obstruction.Initialize(cam, null, w2.transform, m2.transform);
            float reach2 = obstruction.WeaponReach;

            // Ammo3gun: heavy long rifle with longest barrel
            var w3 = new GameObject("Ammo3gun_View");
            w3.transform.SetParent(camObj.transform);
            w3.transform.localPosition = new Vector3(0.18f, -0.22f, 0.5f);
            var m3 = new GameObject("Muzzle");
            m3.transform.SetParent(w3.transform);
            m3.transform.localPosition = new Vector3(0f, 0.07f, 0.65f);

            obstruction.Initialize(cam, null, w3.transform, m3.transform);
            float reach3 = obstruction.WeaponReach;

            Assert(reach2 > reach1, "Ammo2gun reach is greater than Ammo1gun", $"reach1: {reach1}, reach2: {reach2}");
            Assert(reach3 > reach2, "Ammo3gun reach is greater than Ammo2gun", $"reach2: {reach2}, reach3: {reach3}");

            UnityEngine.Object.DestroyImmediate(camObj);
        }

        private static void Test_Filter_ExcludesPlayerAndWeaponColliders()
        {
            var playerRoot = new GameObject("PlayerRoot");
            var playerCollider = playerRoot.AddComponent<CapsuleCollider>();

            var camObj = new GameObject("Camera");
            camObj.transform.SetParent(playerRoot.transform);
            var cam = camObj.AddComponent<Camera>();

            var weapon = new GameObject("Weapon");
            weapon.transform.SetParent(camObj.transform);
            var weaponCollider = weapon.AddComponent<BoxCollider>();

            var obstruction = camObj.AddComponent<FpsViewModelObstruction>();
            obstruction.Initialize(cam, playerRoot.transform, weapon.transform, null);

            Assert(!obstruction.IsValidObstacle(playerCollider), "Player capsule collider is excluded from obstruction");
            Assert(!obstruction.IsValidObstacle(weaponCollider), "Weapon view model collider is excluded from obstruction");

            var wall = new GameObject("Wall");
            var wallCollider = wall.AddComponent<BoxCollider>();
            Assert(obstruction.IsValidObstacle(wallCollider), "Independent environment wall collider is recognized as an obstacle");

            UnityEngine.Object.DestroyImmediate(playerRoot);
            UnityEngine.Object.DestroyImmediate(wall);
        }

        private static void Test_Filter_ExcludesResourceStations()
        {
            var camObj = new GameObject("Camera");
            var cam = camObj.AddComponent<Camera>();
            var weapon = new GameObject("Weapon");
            weapon.transform.SetParent(camObj.transform);

            var obstruction = camObj.AddComponent<FpsViewModelObstruction>();
            obstruction.Initialize(cam, null, weapon.transform, null);

            var stationObj = new GameObject("WeaponStation");
            stationObj.AddComponent<BoxCollider>();
            stationObj.AddComponent<ResourceStation>();
            var col = stationObj.GetComponent<BoxCollider>();

            Assert(!obstruction.IsValidObstacle(col), "Resource station colliders are excluded from obstruction detection");

            UnityEngine.Object.DestroyImmediate(camObj);
            UnityEngine.Object.DestroyImmediate(stationObj);
        }

        private static void Test_Filter_ExcludesHeldBarricades()
        {
            var camObj = new GameObject("Camera");
            var cam = camObj.AddComponent<Camera>();
            var weapon = new GameObject("Weapon");
            weapon.transform.SetParent(camObj.transform);

            var obstruction = camObj.AddComponent<FpsViewModelObstruction>();
            obstruction.Initialize(cam, null, weapon.transform, null);

            var barricadeObj = new GameObject("Barricade");
            var rb = barricadeObj.AddComponent<Rigidbody>();
            var col = barricadeObj.AddComponent<BoxCollider>();
            var mb = barricadeObj.AddComponent<MovableBarricade>();

            // When unheld in world, it is an obstacle
            Assert(obstruction.IsValidObstacle(col), "Unheld world barricade is recognized as an obstacle");

            // When held, it moves with the player and should not trigger visual weapon retraction
            mb.GetType().GetField("isHeld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(mb, true);
            Assert(!obstruction.IsValidObstacle(col), "Actively held barricade is excluded from obstruction detection");

            UnityEngine.Object.DestroyImmediate(camObj);
            UnityEngine.Object.DestroyImmediate(barricadeObj);
        }

        private static void Test_Filter_ExcludesTriggerVolumes()
        {
            var camObj = new GameObject("Camera");
            var cam = camObj.AddComponent<Camera>();
            var weapon = new GameObject("Weapon");
            weapon.transform.SetParent(camObj.transform);

            var obstruction = camObj.AddComponent<FpsViewModelObstruction>();
            obstruction.Initialize(cam, null, weapon.transform, null);

            var triggerObj = new GameObject("TriggerZone");
            var triggerCol = triggerObj.AddComponent<BoxCollider>();
            triggerCol.isTrigger = true;

            Assert(!obstruction.IsValidObstacle(triggerCol), "Trigger volumes are excluded from obstruction detection");

            UnityEngine.Object.DestroyImmediate(camObj);
            UnityEngine.Object.DestroyImmediate(triggerObj);
        }

        private static void Test_WeaponSwap_ReinitializesPoseAndTracking()
        {
            var camObj = new GameObject("Camera");
            var cam = camObj.AddComponent<Camera>();
            var obstruction = camObj.AddComponent<FpsViewModelObstruction>();

            var w1 = new GameObject("Weapon1");
            w1.transform.SetParent(camObj.transform);
            w1.transform.localPosition = new Vector3(0.18f, -0.22f, 0.5f);

            obstruction.Initialize(cam, null, w1.transform, null);
            Assert(!obstruction.IsObstructed, "Initial weapon begins in unobstructed state");
            Assert(Vector3.Distance(obstruction.TargetLocalPosition, w1.transform.localPosition) < 0.001f, "Target matches default pose");

            // Simulate weapon swap to Weapon2
            UnityEngine.Object.DestroyImmediate(w1);
            var w2 = new GameObject("Weapon2");
            w2.transform.SetParent(camObj.transform);
            w2.transform.localPosition = new Vector3(0.15f, -0.20f, 0.48f); // Custom pose

            obstruction.Initialize(cam, null, w2.transform, null);
            Assert(Vector3.Distance(obstruction.DefaultLocalPosition, new Vector3(0.15f, -0.20f, 0.48f)) < 0.001f, "Swap successfully reinitializes baseline pose to new weapon");
            Assert(!obstruction.IsObstructed, "Swap clears any prior obstruction state cleanly");

            UnityEngine.Object.DestroyImmediate(camObj);
            UnityEngine.Object.DestroyImmediate(w2);
        }

        private static void Test_PhysicsQuery_DetectsSolidObstacle()
        {
            var camObj = new GameObject("Camera");
            camObj.transform.position = Vector3.zero;
            camObj.transform.rotation = Quaternion.identity;
            var cam = camObj.AddComponent<Camera>();

            var weapon = new GameObject("Weapon");
            weapon.transform.SetParent(camObj.transform);
            weapon.transform.localPosition = new Vector3(0.18f, -0.22f, 0.5f);

            var obstruction = camObj.AddComponent<FpsViewModelObstruction>();
            obstruction.Initialize(cam, null, weapon.transform, null);

            // Spawn a wall collider 0.4m directly in front of the camera
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 0f, 0.4f);
            wall.transform.localScale = new Vector3(2f, 2f, 0.1f);
            Physics.SyncTransforms();

            bool hitFound = obstruction.CheckObstruction(out var hit, out float distance);
            Assert(hitFound, "Physics query detects solid wall in front of camera");
            Assert(distance <= 0.45f && distance >= 0.25f, "Detected wall distance matches physical proximity", $"distance: {distance}");

            UnityEngine.Object.DestroyImmediate(camObj);
            UnityEngine.Object.DestroyImmediate(wall);
        }
    }
}
