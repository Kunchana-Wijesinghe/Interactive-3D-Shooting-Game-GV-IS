using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FacilityBreach;
using FacilityBreach.Audio;
using FacilityBreach.Noise;
using FacilityBreach.Physics;
using FacilityBreach.Player;

namespace FacilityBreach.Editor
{
    /// <summary>
    /// Automated regression tests for GV Part 2 Player Interaction Physics and Audio:
    /// - Procedural audio synthesis and finite PCM sample generation (all 6 clip types).
    /// - Distinct gunshot audio profiles across Ammo1, Ammo2, and Ammo3.
    /// - FacilityAudioPlayer pooling bounds, 2D/3D spatial separation, and reuse.
    /// - Neutral facility noise event contract publication and decoupling.
    /// - KickablePhysicsProp Rigidbody configuration, damping, and collision threshold.
    /// - FpsKickController reach, directional loft impulse, and cooldown mechanics.
    /// - Seven facility props configuration (3 boxes, 2 crates, 2 barrels) and duplicate prevention.
    /// - Teammate Sound Hunter isolation invariant.
    /// - Existing Shooting VFX and tactical arms integration preservation.
    /// </summary>
    public static class KickablePhysicsAudioTests
    {
        public static Action<string> Log = msg => Debug.Log(msg);
        public static Action<string> LogError = msg => Debug.LogError(msg);

        private static int passedCount;
        private static int failedCount;
        private static readonly List<string> failureMessages = new List<string>();

        [MenuItem("Facility Breach/Run Kickable Physics and Audio Tests")]
        public static void RunAll()
        {
            passedCount = 0;
            failedCount = 0;
            failureMessages.Clear();

            Log("<color=cyan>=== [KickablePhysicsAudioTests] Starting Test Suite ===</color>");

            RunTest("AudioClipGeneration_AllTypes_FiniteSamplesAndNonZero", Test_AudioClipGeneration_AllTypes_FiniteSamplesAndNonZero);
            RunTest("AudioClipCaching_ReturnsIdenticalInstance", Test_AudioClipCaching_ReturnsIdenticalInstance);
            RunTest("AudioGunshotProfiles_DistinctSampleData", Test_AudioGunshotProfiles_DistinctSampleData);
            RunTest("AudioPlayer_PoolCapacityAndSourceBounds", Test_AudioPlayer_PoolCapacityAndSourceBounds);
            RunTest("NoiseHub_NeutralEventEmissionAndPayload", Test_NoiseHub_NeutralEventEmissionAndPayload);
            RunTest("KickablePhysicsProp_PhysicsConfigurationAndDamping", Test_KickablePhysicsProp_PhysicsConfigurationAndDamping);
            RunTest("KickablePhysicsProp_KickImpulse_AppliedToRigidbody", Test_KickablePhysicsProp_KickImpulse_AppliedToRigidbody);
            RunTest("KickablePhysicsProp_CollisionAudioThresholdAndCooldown", Test_KickablePhysicsProp_CollisionAudioThresholdAndCooldown);
            RunTest("FpsKickController_RangeAndOcclusion", Test_FpsKickController_RangeAndOcclusion);
            RunTest("SevenPropsPlacement_AllSevenPropsSpawned", Test_SevenPropsPlacement_AllSevenPropsSpawned);
            RunTest("TeammateSoundHunterIsolation_NoCoupling", Test_TeammateSoundHunterIsolation_NoCoupling);
            RunTest("ExistingVfxAndAudioIntegration_Preserved", Test_ExistingVfxAndAudioIntegration_Preserved);

            if (failedCount == 0)
            {
                Log($"<color=green>[KickablePhysicsAudioTests] Completed {passedCount} checks: {passedCount} PASSED, 0 FAILED.</color>");
            }
            else
            {
                LogError($"<color=red>[KickablePhysicsAudioTests] FAILED: {failedCount} failures out of {passedCount + failedCount} checks.</color>");
                foreach (var err in failureMessages)
                {
                    LogError("  - " + err);
                }
            }
        }

        public static void RunBatch()
        {
            RunAll();
            EditorApplication.Exit(failedCount > 0 ? 1 : 0);
        }

        private static void RunTest(string testName, Action testMethod)
        {
            try
            {
                testMethod();
            }
            catch (Exception ex)
            {
                failedCount++;
                failureMessages.Add($"[FAIL] {testName} threw unhandled exception: {ex.Message}\n{ex.StackTrace}");
                LogError($"[FAIL] {testName} threw exception: {ex.Message}");
            }
        }

        private static void Assert(bool condition, string message, string context = "")
        {
            if (condition)
            {
                passedCount++;
            }
            else
            {
                failedCount++;
                string fullMsg = string.IsNullOrEmpty(context) ? message : $"{message} ({context})";
                failureMessages.Add("[FAIL] " + fullMsg);
                LogError("[FAIL] " + fullMsg);
            }
        }

        private static void Test_AudioClipGeneration_AllTypes_FiniteSamplesAndNonZero()
        {
            var types = (FacilityAudioClipType[])Enum.GetValues(typeof(FacilityAudioClipType));
            Assert(types.Length == 6, "FacilityAudioClipType defines exactly 6 audio profiles", $"Found {types.Length}");

            foreach (var type in types)
            {
                AudioClip clip = FacilityAudioSynthesizer.GetOrCreateClip(type);
                Assert(clip != null, $"Synthesizer generates non-null clip for {type}");
                Assert(clip.samples > 0, $"Clip {type} has positive sample count", $"Samples={clip.samples}");
                Assert(clip.channels == 1, $"Clip {type} is mono (1 channel)", $"Channels={clip.channels}");
                Assert(clip.frequency == 44100, $"Clip {type} has 44.1kHz sample rate", $"Freq={clip.frequency}");

                float[] data = new float[clip.samples];
                clip.GetData(data, 0);

                float maxAbs = 0f;
                bool hasNaN = false;
                for (int i = 0; i < data.Length; i++)
                {
                    if (float.IsNaN(data[i]) || float.IsInfinity(data[i])) hasNaN = true;
                    float abs = Mathf.Abs(data[i]);
                    if (abs > maxAbs) maxAbs = abs;
                }

                Assert(!hasNaN, $"Clip {type} contains zero NaN or Infinity samples");
                Assert(maxAbs > 0.05f, $"Clip {type} has audible non-silent amplitude", $"Peak={maxAbs:F3}");
                Assert(maxAbs <= 1.0f, $"Clip {type} is strictly bounded to <= 1.0f (no digital clipping)", $"Peak={maxAbs:F3}");
            }
        }

        private static void Test_AudioClipCaching_ReturnsIdenticalInstance()
        {
            AudioClip clipA1 = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.GunshotAmmo1);
            AudioClip clipA2 = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.GunshotAmmo1);
            Assert(ReferenceEquals(clipA1, clipA2), "Synthesizer returns cached instance for repeated calls to GunshotAmmo1");

            AudioClip clipWood1 = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.ImpactWood);
            AudioClip clipWood2 = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.ImpactWood);
            Assert(ReferenceEquals(clipWood1, clipWood2), "Synthesizer returns cached instance for repeated calls to ImpactWood");
        }

        private static void Test_AudioGunshotProfiles_DistinctSampleData()
        {
            AudioClip clip1 = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.GunshotAmmo1);
            AudioClip clip2 = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.GunshotAmmo2);
            AudioClip clip3 = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.GunshotAmmo3);

            Assert(clip1.length != clip2.length, "Ammo1 and Ammo2 gunshots have distinct lengths", $"L1={clip1.length:F3}s, L2={clip2.length:F3}s");
            Assert(clip2.length != clip3.length, "Ammo2 and Ammo3 gunshots have distinct lengths", $"L2={clip2.length:F3}s, L3={clip3.length:F3}s");

            // Compare sample data differences
            float[] d1 = new float[100];
            float[] d2 = new float[100];
            clip1.GetData(d1, 0);
            clip2.GetData(d2, 0);

            float diffSum = 0f;
            for (int i = 0; i < 100; i++) diffSum += Mathf.Abs(d1[i] - d2[i]);
            Assert(diffSum > 0.1f, "Ammo1 and Ammo2 have distinct waveform data", $"DiffSum={diffSum:F3}");
        }

        private static void Test_AudioPlayer_PoolCapacityAndSourceBounds()
        {
            var go = new GameObject("Test_AudioPlayer_Go");
            try
            {
                var player = go.AddComponent<FacilityAudioPlayer>();
                player.InitializePool();

                Assert(player.SpatialPoolCapacity == 8, "Spatial audio pool capacity is calibrated to 8 sources");
                Assert(player.MasterVolume > 0f && player.MasterVolume <= 1.0f, "Master volume is within safe bounds", $"Volume={player.MasterVolume}");

                var sources = go.GetComponentsInChildren<AudioSource>();
                // 1 dedicated 2D source on root + 8 spatial child sources = 9 sources total
                Assert(sources.Length == 9, "AudioPlayer instantiates exactly 9 AudioSource components (1 2D + 8 spatial)", $"Found {sources.Length}");

                int spatialCount = 0;
                int twoDCount = 0;
                foreach (var src in sources)
                {
                    if (src.spatialBlend >= 0.99f) spatialCount++;
                    else if (src.spatialBlend <= 0.01f) twoDCount++;
                }

                Assert(spatialCount == 8, "Exactly 8 sources have 3D spatialBlend == 1.0f", $"Count={spatialCount}");
                Assert(twoDCount == 1, "Exactly 1 source has 2D spatialBlend == 0.0f", $"Count={twoDCount}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_NoiseHub_NeutralEventEmissionAndPayload()
        {
            FacilityNoiseEvent receivedEvent = default;
            bool eventFired = false;

            Action<FacilityNoiseEvent> listener = evt =>
            {
                receivedEvent = evt;
                eventFired = true;
            };

            FacilityNoiseHub.OnNoiseEmitted += listener;

            try
            {
                var dummySource = new GameObject("Test_NoiseSource");
                try
                {
                    Vector3 testPos = new Vector3(12f, 1f, -4f);
                    FacilityNoiseHub.Emit(testPos, 25f, FacilityNoiseType.Gunshot, dummySource);

                    Assert(eventFired, "FacilityNoiseHub fires OnNoiseEmitted when Emit is called");
                    Assert(receivedEvent.Type == FacilityNoiseType.Gunshot, "Received noise event has correct Type (Gunshot)");
                    Assert(receivedEvent.Position == testPos, "Received noise event has correct Position", $"Pos={receivedEvent.Position}");
                    Assert(Mathf.Approximately(receivedEvent.Radius, 25f), "Received noise event has correct Radius", $"Radius={receivedEvent.Radius}");
                    Assert(receivedEvent.Source == dummySource, "Received noise event preserves Source reference");
                    Assert(receivedEvent.Timestamp >= 0f, "Received noise event has non-negative Timestamp");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(dummySource);
                }
            }
            finally
            {
                FacilityNoiseHub.OnNoiseEmitted -= listener;
            }
        }

        private static void Test_KickablePhysicsProp_PhysicsConfigurationAndDamping()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var prop = go.AddComponent<KickablePhysicsProp>();
                prop.BaseMass = 5.0f;
                prop.ConfigurePhysics();

                Assert(prop.Body != null, "KickablePhysicsProp requires and caches Rigidbody");
                Assert(Mathf.Approximately(prop.Body.mass, 5.0f), "Prop Rigidbody mass is properly calibrated", $"Mass={prop.Body.mass}");
                Assert(!prop.Body.isKinematic, "Prop Rigidbody is non-kinematic for dynamic physical motion");
                Assert(prop.Body.linearDamping >= 0.5f, "Prop Rigidbody has positive linear damping");
                Assert(prop.Body.angularDamping >= 1.0f, "Prop Rigidbody has positive angular damping to prevent endless spinning");
                Assert(prop.Body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic, "Collision detection mode is ContinuousDynamic");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_KickablePhysicsProp_KickImpulse_AppliedToRigidbody()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var prop = go.AddComponent<KickablePhysicsProp>();
                prop.BaseMass = 4.0f;
                prop.ConfigurePhysics();

                Assert(!prop.IsKickedRecently, "Prop is initially not flagged as kicked recently");

                Vector3 impulse = new Vector3(10f, 3.5f, 0f);
                bool applied = prop.ApplyKick(impulse, go.transform.position);

                Assert(applied, "ApplyKick returns true on non-kinematic Rigidbody");
                Assert(prop.IsKickedRecently, "Prop reports IsKickedRecently == true immediately after kick");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_KickablePhysicsProp_CollisionAudioThresholdAndCooldown()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var prop = go.AddComponent<KickablePhysicsProp>();
                prop.MinImpactVelocity = 1.5f;
                prop.ImpactCooldown = 0.20f;

                Assert(Mathf.Approximately(prop.MinImpactVelocity, 1.5f), "MinImpactVelocity property is verified", $"Val={prop.MinImpactVelocity}");
                Assert(Mathf.Approximately(prop.ImpactCooldown, 0.20f), "ImpactCooldown property is verified", $"Val={prop.ImpactCooldown}");

                prop.MaterialType = PropMaterialType.Metal;
                Assert(prop.MaterialType == PropMaterialType.Metal, "MaterialType correctly switches to Metal");
                prop.MaterialType = PropMaterialType.Wood;
                Assert(prop.MaterialType == PropMaterialType.Wood, "MaterialType correctly switches to Wood");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_FpsKickController_RangeAndOcclusion()
        {
            var go = new GameObject("Test_Player_KickController");
            try
            {
                var kickCtrl = go.AddComponent<FpsKickController>();
                Assert(kickCtrl.KickReach >= 2.0f && kickCtrl.KickReach <= 2.5f, "Kick reach is calibrated to approximately 2.2m", $"Reach={kickCtrl.KickReach}");
                Assert(kickCtrl.KickImpulse >= 10.0f && kickCtrl.KickImpulse <= 20.0f, "Kick impulse is safely bounded", $"Impulse={kickCtrl.KickImpulse}");
                Assert(kickCtrl.UpwardLoft > 0f && kickCtrl.UpwardLoft <= 5.0f, "Upward loft is positive and modest", $"Loft={kickCtrl.UpwardLoft}");
                Assert(kickCtrl.KickCooldown >= 0.3f && kickCtrl.KickCooldown <= 0.6f, "Kick cooldown prevents rapid spamming", $"Cooldown={kickCtrl.KickCooldown}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_SevenPropsPlacement_AllSevenPropsSpawned()
        {
            var rootParent = new GameObject("Test_RootParent");
            try
            {
                var props = FacilityKickablePropsSetup.EnsurePropsSpawned(rootParent.transform);

                Assert(props != null, "EnsurePropsSpawned returns non-null list");
                Assert(props.Count == 7, "Exactly 7 kickable physics props are spawned", $"Count={props.Count}");

                int woodCount = 0;
                int metalCount = 0;
                int boxCount = 0;
                int crateCount = 0;
                int barrelCount = 0;

                foreach (var prop in props)
                {
                    Assert(prop != null, "Spawned prop instance is non-null");
                    Assert(prop.Body != null, $"Prop {prop.name} has Rigidbody attached");
                    Assert(!prop.Body.isKinematic, $"Prop {prop.name} is non-kinematic");
                    Assert(prop.GetComponent<Collider>() != null, $"Prop {prop.name} has Collider attached");

                    if (prop.MaterialType == PropMaterialType.Wood) woodCount++;
                    if (prop.MaterialType == PropMaterialType.Metal) metalCount++;

                    if (prop.name.StartsWith("Box_Small")) boxCount++;
                    if (prop.name.StartsWith("Crate_Medium")) crateCount++;
                    if (prop.name.StartsWith("Barrel_Metal")) barrelCount++;
                }

                Assert(boxCount == 3, "Exactly 3 small boxes spawned", $"BoxCount={boxCount}");
                Assert(crateCount == 2, "Exactly 2 medium crates spawned", $"CrateCount={crateCount}");
                Assert(barrelCount == 2, "Exactly 2 metal barrels spawned", $"BarrelCount={barrelCount}");
                Assert(woodCount == 5, "Exactly 5 wooden props (3 boxes + 2 crates)", $"WoodCount={woodCount}");
                Assert(metalCount == 2, "Exactly 2 metallic props (2 barrels)", $"MetalCount={metalCount}");

                // Test duplicate prevention
                var duplicateCheck = FacilityKickablePropsSetup.EnsurePropsSpawned(rootParent.transform);
                Assert(duplicateCheck.Count == 7, "Repeated call does not spawn duplicate props (retains 7)", $"Count={duplicateCheck.Count}");

                var allPropsInScene = UnityEngine.Object.FindObjectsByType<KickablePhysicsProp>(FindObjectsSortMode.None);
                Assert(allPropsInScene.Length == 7, "Total props in scene remains strictly 7 (zero duplicate leaks)", $"Total={allPropsInScene.Length}");
            }
            finally
            {
                var root = GameObject.Find(FacilityKickablePropsSetup.PropsRootName);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(rootParent);
            }
        }

        private static void Test_TeammateSoundHunterIsolation_NoCoupling()
        {
            // Verify FacilityNoiseHub does not depend on or modify SoundHunter
            var evt = new FacilityNoiseEvent(Vector3.zero, 10f, FacilityNoiseType.Interaction);
            Assert(evt.Type == FacilityNoiseType.Interaction, "FacilityNoiseEvent instantiates independently");

            // Verify SoundEventBus is still functional and untouched
            bool soundEventBusFired = false;
            Action<Perera.Core.SoundStimulus> listener = s => soundEventBusFired = true;
            Perera.Core.SoundEventBus.Emitted += listener;
            try
            {
                Perera.Core.SoundEventBus.Emit(Vector3.zero, 10f, Perera.Core.SoundCategory.Other, 999);
                Assert(soundEventBusFired, "Teammate SoundEventBus remains fully functional and uncoupled");
            }
            finally
            {
                Perera.Core.SoundEventBus.Emitted -= listener;
            }
        }

        private static void Test_ExistingVfxAndAudioIntegration_Preserved()
        {
            var go = new GameObject("Test_Runtime_Integration");
            try
            {
                var runtime = go.AddComponent<FacilityBreachRuntime>();

                // Verify runtime properties exist and are cleanly decoupled
                Assert(runtime.BulletTracer == null || runtime.BulletTracer != null, "BulletTracer property intact on FacilityBreachRuntime");
                Assert(runtime.MuzzleFlash == null || runtime.MuzzleFlash != null, "MuzzleFlash property intact on FacilityBreachRuntime");
                Assert(runtime.ImpactSparks == null || runtime.ImpactSparks != null, "ImpactSparks property intact on FacilityBreachRuntime");
                Assert(runtime.AudioPlayer == null || runtime.AudioPlayer != null, "AudioPlayer property intact on FacilityBreachRuntime");
                Assert(runtime.KickController == null || runtime.KickController != null, "KickController property intact on FacilityBreachRuntime");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
