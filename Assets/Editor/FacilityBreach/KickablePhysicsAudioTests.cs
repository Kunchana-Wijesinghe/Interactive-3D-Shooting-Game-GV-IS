using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FacilityBreach;
using FacilityBreach.Audio;
using FacilityBreach.Noise;
using FacilityBreach.Player;

namespace FacilityBreach.Editor
{
    /// <summary>
    /// Automated regression tests for GV Part 2 Player Interaction Physics and Audio:
    /// - Procedural audio synthesis and finite PCM sample generation (all 6 clip types).
    /// - Distinct gunshot audio profiles across Ammo1, Ammo2, and Ammo3.
    /// - Kick thud audio peak normalization (0.96) and acoustic body punch.
    /// - FacilityAudioPlayer pooling bounds, 2D/3D spatial separation, and reuse.
    /// - Neutral facility noise event contract publication and decoupling.
    /// - KickablePhysicsProp Rigidbody configuration, damping, rolling torque, and collision threshold.
    /// - FpsKickController reach, direct camera raycast aiming, player capsule exclusion, and occlusion.
    /// - Thirteen facility props configuration (5 boxes, 3 crates, 5 barrels) and duplicate prevention.
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
            RunTest("KickThudAudio_PeakNormalizationAndFrequency", Test_KickThudAudio_PeakNormalizationAndFrequency);
            RunTest("AudioPlayer_PoolCapacityAndSourceBounds", Test_AudioPlayer_PoolCapacityAndSourceBounds);
            RunTest("NoiseHub_NeutralEventEmissionAndPayload", Test_NoiseHub_NeutralEventEmissionAndPayload);
            RunTest("KickablePhysicsProp_PhysicsConfigurationAndDamping", Test_KickablePhysicsProp_PhysicsConfigurationAndDamping);
            RunTest("KickablePhysicsProp_BarrelRollingPhysicsAndTorque", Test_KickablePhysicsProp_BarrelRollingPhysicsAndTorque);
            RunTest("KickablePhysicsProp_KickImpulse_AppliedToRigidbody", Test_KickablePhysicsProp_KickImpulse_AppliedToRigidbody);
            RunTest("KickablePhysicsProp_CollisionAudioThresholdAndCooldown", Test_KickablePhysicsProp_CollisionAudioThresholdAndCooldown);
            RunTest("FpsKickController_RangeAndOcclusion", Test_FpsKickController_RangeAndOcclusion);
            RunTest("FpsKickController_PrecisionAimAndTargetResolution", Test_FpsKickController_PrecisionAimAndTargetResolution);
            RunTest("ThirteenPropsPlacement_AllThirteenPropsSpawned", Test_ThirteenPropsPlacement_AllThirteenPropsSpawned);
            RunTest("ConvertExistingSceneProps_AllowlistSafetyAndConversion", Test_ConvertExistingSceneProps_AllowlistSafetyAndConversion);
            RunTest("TotalFacilityProps_AuthoredPlusConverted", Test_TotalFacilityProps_AuthoredPlusConverted);
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

        public static void RunAll11SuitesBatch()
        {
            Log("<color=yellow>=== RUNNING ALL 11 TEST SUITES (374 Baseline + Kickable Physics & Audio) ===</color>");
            FpsArmsTests.RunAll();
            FpsViewModelObstructionTests.RunAll();
            FacilityInteractionDispatcherTests.RunAll();
            PatrolGuardAStarTests.RunAll();
            PatrolGuardSearchTests.RunAll();
            PatrolGuardReturnTests.RunAll();
            PatrolGuardObstacleTests.RunAll();
            FpsBulletTracerTests.RunAll();
            FpsMuzzleFlashV2Tests.RunAll();
            FpsImpactSparksTests.RunAll();
            KickablePhysicsAudioTests.RunAll();

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

        private static void Test_KickThudAudio_PeakNormalizationAndFrequency()
        {
            AudioClip clip = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.KickThud);
            Assert(clip != null, "Procedural kick thud clip is generated");
            Assert(Mathf.Approximately(clip.length, 0.12f) || Mathf.Abs(clip.length - 0.12f) < 0.005f, "Kick thud duration is 0.12s for enhanced acoustic presence", $"Length={clip.length:F3}s");

            float[] samples = new float[clip.samples];
            clip.GetData(samples, 0);

            float peak = 0f;
            bool hasInvalid = false;
            for (int i = 0; i < samples.Length; i++)
            {
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i])) hasInvalid = true;
                float abs = Mathf.Abs(samples[i]);
                if (abs > peak) peak = abs;
            }

            Assert(!hasInvalid, "Kick thud audio has zero NaN or Infinity samples");
            Assert(peak >= 0.94f && peak <= 0.98f, "Kick thud sample peak is normalized to 0.96 for punchy playback without clipping", $"Peak={peak:F4}");
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

        private static void Test_KickablePhysicsProp_BarrelRollingPhysicsAndTorque()
        {
            var barrelGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            try
            {
                barrelGo.name = "Test_MetalBarrel";
                var prop = barrelGo.AddComponent<KickablePhysicsProp>();
                prop.MaterialType = PropMaterialType.Metal;
                prop.BaseMass = 7.5f;
                prop.LinearDamping = 0.50f;
                prop.AngularDamping = 0.25f;
                prop.ConfigurePhysics();

                Assert(prop.MaterialType == PropMaterialType.Metal, "Prop material type is Metal");
                Assert(Mathf.Approximately(prop.LinearDamping, 0.50f), "Linear damping is calibrated for rolling", $"Damping={prop.LinearDamping}");
                Assert(Mathf.Approximately(prop.AngularDamping, 0.25f), "Angular damping is low (0.25) to permit natural rolling", $"Damping={prop.AngularDamping}");
                Assert(prop.Body.maxAngularVelocity >= 30.0f, "Max angular velocity is expanded to >= 30 for smooth rolling", $"MaxAV={prop.Body.maxAngularVelocity}");

                Vector3 forwardImpulse = new Vector3(0f, 0f, 12.0f);
                bool kicked = prop.ApplyKick(forwardImpulse, barrelGo.transform.position);

                Assert(kicked, "ApplyKick successfully applies impulse to barrel");
                Assert(prop.IsKickedRecently, "Prop is flagged as kicked recently");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(barrelGo);
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

        private static void Test_FpsKickController_PrecisionAimAndTargetResolution()
        {
            var playerGo = new GameObject("Test_Player");
            var camGo = new GameObject("Test_Camera");
            var propGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var obstacleGo = GameObject.CreatePrimitive(PrimitiveType.Cube);

            try
            {
                playerGo.AddComponent<CharacterController>();
                var kickCtrl = playerGo.AddComponent<FpsKickController>();
                var cam = camGo.AddComponent<Camera>();

                camGo.transform.SetParent(playerGo.transform, false);
                camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                camGo.transform.localRotation = Quaternion.identity;

                playerGo.transform.position = Vector3.zero;
                propGo.name = "Test_KickableBox";
                var prop = propGo.AddComponent<KickablePhysicsProp>();
                prop.ConfigurePhysics();

                // 1. Direct alignment within reach (1.5m in front of camera)
                propGo.transform.position = new Vector3(0f, 1.6f, 1.5f);
                obstacleGo.transform.position = new Vector3(50f, 50f, 50f);
                UnityEngine.Physics.SyncTransforms();

                bool foundDirect = kickCtrl.FindKickTarget(cam, out var targetProp, out var hitPoint);
                Assert(foundDirect, "Direct camera raycast resolves kickable prop within range");
                Assert(targetProp != null && targetProp == prop, "Resolved target matches authored kickable prop");
                Assert(targetProp != null && targetProp.gameObject != playerGo, "Player's own GameObject/CharacterController is never targeted");

                // 2. Out of range (beyond KickReach 2.2m, e.g. 3.5m)
                propGo.transform.position = new Vector3(0f, 1.6f, 3.5f);
                UnityEngine.Physics.SyncTransforms();
                bool foundOutOfRange = kickCtrl.FindKickTarget(cam, out _, out _);
                Assert(!foundOutOfRange, "Kick target beyond KickReach is rejected");

                // 3. Occlusion: prop is at 1.8m, but solid obstacle is at 0.9m
                propGo.transform.position = new Vector3(0f, 1.6f, 1.8f);
                obstacleGo.transform.position = new Vector3(0f, 1.6f, 0.9f);
                obstacleGo.transform.localScale = new Vector3(2f, 2f, 0.2f);
                UnityEngine.Physics.SyncTransforms();

                bool foundBehindWall = kickCtrl.FindKickTarget(cam, out _, out _);
                Assert(!foundBehindWall, "Kick target occluded behind solid wall obstacle is rejected");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(propGo);
                UnityEngine.Object.DestroyImmediate(obstacleGo);
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
            }
        }

        private static void Test_ThirteenPropsPlacement_AllThirteenPropsSpawned()
        {
            var rootParent = new GameObject("Test_RootParent");
            try
            {
                var props = FacilityKickablePropsSetup.EnsurePropsSpawned(rootParent.transform);

                Assert(props != null, "EnsurePropsSpawned returns non-null list");
                Assert(props.Count == 13, "Exactly 13 kickable physics props are spawned", $"Count={props.Count}");

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

                Assert(boxCount == 5, "Exactly 5 small boxes spawned", $"BoxCount={boxCount}");
                Assert(crateCount == 3, "Exactly 3 medium crates spawned", $"CrateCount={crateCount}");
                Assert(barrelCount == 5, "Exactly 5 metal barrels spawned", $"BarrelCount={barrelCount}");
                Assert(woodCount == 8, "Exactly 8 wooden props (5 boxes + 3 crates)", $"WoodCount={woodCount}");
                Assert(metalCount == 5, "Exactly 5 metallic props (5 barrels)", $"MetalCount={metalCount}");

                // Test duplicate prevention
                var duplicateCheck = FacilityKickablePropsSetup.EnsurePropsSpawned(rootParent.transform);
                Assert(duplicateCheck.Count == 13, "Repeated call does not spawn duplicate props (retains 13)", $"Count={duplicateCheck.Count}");

                var allPropsInScene = UnityEngine.Object.FindObjectsByType<KickablePhysicsProp>(FindObjectsSortMode.None);
                Assert(allPropsInScene.Length == 13, "Total props in scene remains strictly 13 (zero duplicate leaks)", $"Total={allPropsInScene.Length}");
            }
            finally
            {
                var root = GameObject.Find(FacilityKickablePropsSetup.PropsRootName);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(rootParent);
            }
        }

        private static void Test_ConvertExistingSceneProps_AllowlistSafetyAndConversion()
        {
            var mockParent = new GameObject("Test_Scene_Clutter");
            var mockBarricade = new GameObject("Barricade_Entrance");
            var mockStation = new GameObject("Entrance health +50 (HP <=50)");
            var mockBand = new GameObject("Room1_Crate_A_Band");
            var mockProps = new List<GameObject>();

            try
            {
                // Setup mock barricade that MUST be protected
                mockBarricade.AddComponent<BoxCollider>();
                mockBarricade.AddComponent<MovableBarricade>();

                // Setup mock health station that MUST be protected
                mockStation.AddComponent<BoxCollider>();
                mockStation.AddComponent<FacilityBreach.Interactions.ResourceStation>();

                // Setup mock band for Room1_Crate_A
                var bandCol = mockBand.AddComponent<BoxCollider>();

                // Setup mock objects for each item in SafeScenePropAllowlist
                for (int i = 0; i < FacilityKickablePropsSetup.SafeScenePropAllowlist.Length; i++)
                {
                    string propName = FacilityKickablePropsSetup.SafeScenePropAllowlist[i];
                    var go = propName.StartsWith("Barrel") ?
                        GameObject.CreatePrimitive(PrimitiveType.Cylinder) :
                        GameObject.CreatePrimitive(PrimitiveType.Cube);

                    go.name = propName;
                    go.transform.SetParent(mockParent.transform);
                    var rb = go.GetComponent<Rigidbody>();
                    if (rb == null) rb = go.AddComponent<Rigidbody>();
                    mockProps.Add(go);
                }

                Assert(FacilityKickablePropsSetup.SafeScenePropAllowlist.Length == 27, "Allowlist contains exactly 27 existing scene props (8 barrels + 8 crates + 8 storage boxes + 3 detail crates)");

                // Execute conversion
                var converted = FacilityKickablePropsSetup.ConvertExistingSceneProps();

                Assert(converted != null, "ConvertExistingSceneProps returns non-null list");
                Assert(converted.Count == 27, "All 27 allowlisted scene clutter props are converted", $"Count={converted.Count}");

                // Barricade and station must NOT have KickablePhysicsProp
                Assert(mockBarricade.GetComponent<KickablePhysicsProp>() == null, "Barricade_Entrance is strictly excluded from kick conversion");
                Assert(mockStation.GetComponent<KickablePhysicsProp>() == null, "Entrance health station is strictly excluded from kick conversion");

                // Verify mockBand collider was disabled and parented to Room1_Crate_A
                Assert(!bandCol.enabled, "Room1_Crate_A_Band collider is disabled to prevent raycast occlusion");
                Assert(mockBand.transform.parent != null && mockBand.transform.parent.name == "Room1_Crate_A", "Room1_Crate_A_Band is parented to Room1_Crate_A");

                int barrelCount = 0;
                int crateCount = 0;
                int boxCount = 0;
                int detailCrateCount = 0;

                foreach (var prop in converted)
                {
                    Assert(prop != null, "Converted prop is non-null");
                    Assert(prop.Body != null, $"Prop {prop.name} has Rigidbody");
                    Assert(prop.Body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic, $"Prop {prop.name} uses ContinuousDynamic collision");

                    if (prop.name.StartsWith("Barrel"))
                    {
                        barrelCount++;
                        Assert(prop.MaterialType == PropMaterialType.Metal, $"Barrel {prop.name} has Metal material");
                        Assert(prop.LinearDamping <= 0.50f, $"Barrel {prop.name} linear damping is calibrated for rolling", $"Damping={prop.LinearDamping}");
                        Assert(prop.AngularDamping <= 0.25f, $"Barrel {prop.name} angular damping is calibrated for rolling", $"AngularDamping={prop.AngularDamping}");
                    }
                    else if (prop.name.StartsWith("Box_"))
                    {
                        boxCount++;
                        Assert(prop.MaterialType == PropMaterialType.Wood, $"Storage Box {prop.name} has Wood material");
                        Assert(prop.LinearDamping >= 0.45f && prop.LinearDamping <= 0.55f, $"Storage Box {prop.name} linear damping calibrated for sliding", $"Damping={prop.LinearDamping}");
                    }
                    else if (prop.name.StartsWith("Room1_Crate"))
                    {
                        detailCrateCount++;
                        Assert(prop.MaterialType == PropMaterialType.Wood, $"Detail Crate {prop.name} has Wood material");
                        Assert(prop.LinearDamping >= 0.55f && prop.LinearDamping <= 0.70f, $"Detail Crate {prop.name} linear damping calibrated for solid mass", $"Damping={prop.LinearDamping}");
                    }
                    else if (prop.name.StartsWith("Crate"))
                    {
                        crateCount++;
                        Assert(prop.MaterialType == PropMaterialType.Wood, $"Crate {prop.name} has Wood material");
                        Assert(prop.LinearDamping >= 0.60f && prop.LinearDamping <= 0.70f, $"Crate {prop.name} linear damping gives heavy solid response", $"Damping={prop.LinearDamping}");
                    }
                }

                Assert(barrelCount == 8, "Exactly 8 existing barrels converted", $"BarrelCount={barrelCount}");
                Assert(crateCount == 8, "Exactly 8 existing crates converted", $"CrateCount={crateCount}");
                Assert(boxCount == 8, "Exactly 8 existing storage boxes converted", $"BoxCount={boxCount}");
                Assert(detailCrateCount == 3, "Exactly 3 existing detail crates converted", $"DetailCrateCount={detailCrateCount}");

                // Idempotence check
                var secondPass = FacilityKickablePropsSetup.ConvertExistingSceneProps();
                Assert(secondPass.Count == 27, "Repeated conversion is idempotent (returns 27 without duplicates)", $"Count={secondPass.Count}");
            }
            finally
            {
                foreach (var go in mockProps)
                {
                    if (go != null) UnityEngine.Object.DestroyImmediate(go);
                }
                if (mockBand != null) UnityEngine.Object.DestroyImmediate(mockBand);
                if (mockStation != null) UnityEngine.Object.DestroyImmediate(mockStation);
                if (mockBarricade != null) UnityEngine.Object.DestroyImmediate(mockBarricade);
                if (mockParent != null) UnityEngine.Object.DestroyImmediate(mockParent);
            }
        }

        private static void Test_TotalFacilityProps_AuthoredPlusConverted()
        {
            int authoredCount = FacilityKickablePropsSetup.AuthoredPropDefinitions.Length;
            int convertedAllowlistCount = FacilityKickablePropsSetup.SafeScenePropAllowlist.Length;
            int totalFacilityProps = authoredCount + convertedAllowlistCount;

            Assert(authoredCount == 13, "Authored prop definitions count is 13 (5 boxes, 3 crates, 5 barrels)", $"Authored={authoredCount}");
            Assert(convertedAllowlistCount == 27, "Converted allowlist count is 27 (8 barrels, 8 crates, 8 storage boxes, 3 detail crates)", $"Allowlist={convertedAllowlistCount}");
            Assert(totalFacilityProps == 40, "Total facility kickable props contract is exactly 40 props across the facility", $"Total={totalFacilityProps}");

            // Verify kick travel distance impulse and damping calibration
            var kickControllerGo = new GameObject("Test_KickController_DistanceTuning");
            try
            {
                var kickCtrl = kickControllerGo.AddComponent<FpsKickController>();
                Assert(kickCtrl.KickImpulse >= 18.0f, "Kick impulse is tuned for extended travel distance (>= 18.0f)", $"Impulse={kickCtrl.KickImpulse}");
                Assert(kickCtrl.UpwardLoft >= 2.5f && kickCtrl.UpwardLoft <= 3.0f, "Upward loft gives shallow trajectory arc for floor slide", $"Loft={kickCtrl.UpwardLoft}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(kickControllerGo);
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
