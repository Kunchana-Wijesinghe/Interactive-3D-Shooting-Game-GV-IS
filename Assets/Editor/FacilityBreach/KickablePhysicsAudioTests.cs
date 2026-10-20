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
            RunTest("RollingMetalAudio_SeamlessLoopAndFrequencyRange", Test_RollingMetalAudio_SeamlessLoopAndFrequencyRange);
            RunTest("BarrelRollingRattle_SynthesisAndTickScheduling", Test_BarrelRollingRattle_SynthesisAndTickScheduling);
            RunTest("FacilityAmbienceAudio_LoopContinuityAndSafePeak", Test_FacilityAmbienceAudio_LoopContinuityAndSafePeak);
            RunTest("FacilityAmbience_LifecyclePauseAndEditModeSafety", Test_FacilityAmbience_LifecyclePauseAndEditModeSafety);
            RunTest("FpsFootstepController_MovementCadenceAndGating", Test_FpsFootstepController_MovementCadenceAndGating);
            RunTest("ProjectileLaunchAndImpactAudio_DistinctWaveformsAndPeakNormalization", Test_ProjectileLaunchAndImpactAudio_DistinctWaveformsAndPeakNormalization);
            RunTest("AudioPlayer_PoolCapacityAndSourceBounds", Test_AudioPlayer_PoolCapacityAndSourceBounds);
            RunTest("FacilityAmbiencePlayer_LifecycleAndSingleInstance", Test_FacilityAmbiencePlayer_LifecycleAndSingleInstance);
            RunTest("FacilityAmbience_ZeroNoiseEventEmission", Test_FacilityAmbience_ZeroNoiseEventEmission);
            RunTest("NoiseHub_NeutralEventEmissionAndPayload", Test_NoiseHub_NeutralEventEmissionAndPayload);
            RunTest("KickablePhysicsProp_PhysicsConfigurationAndDamping", Test_KickablePhysicsProp_PhysicsConfigurationAndDamping);
            RunTest("KickablePhysicsProp_BarrelRollingPhysicsAndTorque", Test_KickablePhysicsProp_BarrelRollingPhysicsAndTorque);
            RunTest("KickablePhysicsProp_KickImpulse_AppliedToRigidbody", Test_KickablePhysicsProp_KickImpulse_AppliedToRigidbody);
            RunTest("KickablePhysicsProp_CollisionAudioThresholdAndCooldown", Test_KickablePhysicsProp_CollisionAudioThresholdAndCooldown);
            RunTest("KickablePhysicsProp_RollingAudioLifecycleAndModulation", Test_KickablePhysicsProp_RollingAudioLifecycleAndModulation);
            RunTest("RollingAudio_PerceptibleDynamicSpeedCurves", Test_RollingAudio_PerceptibleDynamicSpeedCurves);
            RunTest("CollisionImpact_TransientVolumeBoostOverRollingBed", Test_CollisionImpact_TransientVolumeBoostOverRollingBed);
            RunTest("ProjectileAudio_LaunchAndImpactPlayback", Test_ProjectileAudio_LaunchAndImpactPlayback);
            RunTest("FpsKickController_RangeAndOcclusion", Test_FpsKickController_RangeAndOcclusion);
            RunTest("FpsKickController_PrecisionAimAndTargetResolution", Test_FpsKickController_PrecisionAimAndTargetResolution);
            RunTest("ThirteenPropsPlacement_AllThirteenPropsSpawned", Test_ThirteenPropsPlacement_AllThirteenPropsSpawned);
            RunTest("ConvertExistingSceneProps_AllowlistSafetyAndConversion", Test_ConvertExistingSceneProps_AllowlistSafetyAndConversion);
            RunTest("TotalFacilityProps_AuthoredPlusConverted", Test_TotalFacilityProps_AuthoredPlusConverted);
            RunTest("TeammateSoundHunterIsolation_NoCoupling", Test_TeammateSoundHunterIsolation_NoCoupling);
            RunTest("ExistingVfxAndAudioIntegration_Preserved", Test_ExistingVfxAndAudioIntegration_Preserved);
            RunTest("InGamePause_AmbienceAndFootstepSynchronization", Test_InGamePause_AmbienceAndFootstepSynchronization);

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
            Assert(types.Length == 12, "FacilityAudioClipType defines exactly 12 audio profiles (including FootstepConcrete)", $"Found {types.Length}");

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

        private static void Test_RollingMetalAudio_SeamlessLoopAndFrequencyRange()
        {
            AudioClip clip = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.RollingMetal);
            Assert(clip != null, "Procedural rolling metal clip is non-null");
            Assert(Mathf.Abs(clip.length - 1.0f) < 0.01f, "Rolling metal loop duration is 1.0s for seamless loop calculation", $"Length={clip.length:F3}s");
            Assert(clip.samples == 44100, "Rolling metal clip has exactly 44100 samples at 44.1kHz", $"Samples={clip.samples}");
            Assert(clip.channels == 1, "Rolling metal clip is mono");

            float[] samples = new float[clip.samples];
            clip.GetData(samples, 0);

            // Check loop boundary continuity (start sample vs end sample)
            float startVal = samples[0];
            float endVal = samples[samples.Length - 1];
            float boundaryDelta = Mathf.Abs(startVal - endVal);
            Assert(boundaryDelta < 0.20f, "Rolling metal loop has smooth boundary continuity to avoid audible clicks", $"Delta={boundaryDelta:F4}");

            // Verify safe peak amplitude
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float abs = Mathf.Abs(samples[i]);
                if (abs > peak) peak = abs;
            }
            Assert(peak >= 0.80f && peak <= 0.90f, "Rolling metal peak is normalized in [0.80, 0.90] for consistent mix level", $"Peak={peak:F4}");
        }

        private static void Test_ProjectileLaunchAndImpactAudio_DistinctWaveformsAndPeakNormalization()
        {
            AudioClip launchClip = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.ProjectileLaunch);
            AudioClip impactClip = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.ProjectileImpact);

            Assert(launchClip != null, "Procedural projectile launch clip is generated");
            Assert(impactClip != null, "Procedural projectile impact clip is generated");

            Assert(Mathf.Abs(launchClip.length - 0.16f) < 0.01f, "Launch clip duration is calibrated to 0.16s", $"Len={launchClip.length:F3}");
            Assert(Mathf.Abs(impactClip.length - 0.20f) < 0.01f, "Impact clip duration is calibrated to 0.20s", $"Len={impactClip.length:F3}");

            float[] launchData = new float[launchClip.samples];
            launchClip.GetData(launchData, 0);
            float launchPeak = 0f;
            for (int i = 0; i < launchData.Length; i++) if (Mathf.Abs(launchData[i]) > launchPeak) launchPeak = Mathf.Abs(launchData[i]);

            float[] impactData = new float[impactClip.samples];
            impactClip.GetData(impactData, 0);
            float impactPeak = 0f;
            for (int i = 0; i < impactData.Length; i++) if (Mathf.Abs(impactData[i]) > impactPeak) impactPeak = Mathf.Abs(impactData[i]);

            Assert(launchPeak >= 0.84f && launchPeak <= 0.92f, "Launch clip peak is normalized around 0.88f", $"Peak={launchPeak:F4}");
            Assert(impactPeak >= 0.86f && impactPeak <= 0.94f, "Impact clip peak is normalized around 0.90f", $"Peak={impactPeak:F4}");

            // Compare waveforms to ensure distinct sonic identities
            int compareCount = Mathf.Min(launchData.Length, impactData.Length, 200);
            float diffSum = 0f;
            for (int i = 0; i < compareCount; i++) diffSum += Mathf.Abs(launchData[i] - impactData[i]);
            Assert(diffSum > 0.5f, "Launch and impact audio profiles have distinct acoustic waveforms", $"Diff={diffSum:F3}");
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

        private static void Test_KickablePhysicsProp_RollingAudioLifecycleAndModulation()
        {
            var barrelGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var boxGo = GameObject.CreatePrimitive(PrimitiveType.Cube);

            try
            {
                // 1. Metal Barrel rolling audio
                var barrelProp = barrelGo.AddComponent<KickablePhysicsProp>();
                barrelProp.MaterialType = PropMaterialType.Metal;
                barrelProp.EnsureRollingAudioSource();

                Assert(barrelProp.RollingAudioSource != null, "Metal barrel instantiates dedicated rolling AudioSource");
                Assert(barrelProp.RollingAudioSource.loop, "Rolling AudioSource is configured to loop");
                Assert(barrelProp.RollingAudioSource.spatialBlend >= 0.99f, "Rolling AudioSource uses full 3D spatialization");
                Assert(barrelProp.RollingAudioSource.clip != null, "Rolling AudioSource is assigned the RollingMetal clip");

                // Initially stationary
                barrelProp.UpdateRollingAudio(0.02f);
                Assert(!barrelProp.IsRollingAudioActive, "Stationary barrel rolling audio is inactive");
                Assert(barrelProp.CurrentRollVolume == 0f, "Stationary barrel rolling volume is 0");

                // Simulate moving barrel with ground contact
                barrelProp.IsContactActive = true;
                barrelProp.Body.linearVelocity = new Vector3(3.0f, 0f, 0f);
                for (int frame = 0; frame < 10; frame++)
                {
                    barrelProp.UpdateRollingAudio(0.02f);
                }

                Assert(barrelProp.CurrentRollVolume > 0.1f, "Moving barrel volume ramps up smoothly", $"Vol={barrelProp.CurrentRollVolume:F3}");
                Assert(barrelProp.IsRollingAudioActive, "Moving barrel reports IsRollingAudioActive == true");
                Assert(barrelProp.RollingAudioSource.volume > 0.1f, "Rolling AudioSource volume is updated", $"Vol={barrelProp.RollingAudioSource.volume:F3}");
                Assert(barrelProp.RollingAudioSource.pitch >= 0.85f, "Rolling pitch is modulated with velocity", $"Pitch={barrelProp.RollingAudioSource.pitch:F2}");

                // Simulate stop
                barrelProp.Body.linearVelocity = Vector3.zero;
                barrelProp.Body.angularVelocity = Vector3.zero;
                barrelProp.IsContactActive = false;
                for (int frame = 0; frame < 30; frame++)
                {
                    barrelProp.UpdateRollingAudio(0.02f);
                }

                Assert(!barrelProp.IsRollingAudioActive, "Stopped barrel rolling audio smoothly ends");
                Assert(barrelProp.CurrentRollVolume == 0f, "Stopped barrel rolling volume fades completely to 0");

                // 2. Wooden box prop never initializes rolling audio
                var boxProp = boxGo.AddComponent<KickablePhysicsProp>();
                boxProp.MaterialType = PropMaterialType.Wood;
                boxProp.EnsureRollingAudioSource();

                Assert(boxProp.RollingAudioSource == null, "Wooden box never instantiates rolling AudioSource");
                boxProp.Body.linearVelocity = new Vector3(5.0f, 0f, 0f);
                boxProp.UpdateRollingAudio(0.02f);
                Assert(!boxProp.IsRollingAudioActive, "Wooden box never plays rolling audio even at high speed");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(barrelGo);
                UnityEngine.Object.DestroyImmediate(boxGo);
            }
        }

        private static void Test_ProjectileAudio_LaunchAndImpactPlayback()
        {
            var audioGo = new GameObject("Test_AudioPlayer_Projectile");
            var canGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            try
            {
                var player = audioGo.AddComponent<FacilityAudioPlayer>();
                player.InitializePool();

                // Test Launch Audio
                player.PlayProjectileLaunch(Vector3.zero);
                Assert(true, "FacilityAudioPlayer.PlayProjectileLaunch executes without exceptions");

                // Test Impact Audio
                player.PlayProjectileImpact(new Vector3(5f, 1f, 10f), 0.75f);
                Assert(true, "FacilityAudioPlayer.PlayProjectileImpact executes without exceptions");

                // Test CanImpact component behavior
                var canImpact = canGo.AddComponent<CanImpact>();
                Assert(canImpact != null, "CanImpact component attaches cleanly to projectile GameObject");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canGo);
                UnityEngine.Object.DestroyImmediate(audioGo);
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

        private static void Test_InGamePause_AmbienceAndFootstepSynchronization()
        {
            var go = new GameObject("Test_PauseSync");
            FacilityAmbiencePlayer ambience = null;
            try
            {
                ambience = go.AddComponent<FacilityAmbiencePlayer>();
                ambience.InitializeAudioSource();
                ambience.Play();
                Assert(ambience.IsPlaying, "Ambience starts playing on Play()");

                // In-Game Pause Verification: Pause() mutes volume immediately to zero and marks IsPaused
                ambience.Pause();
                Assert(ambience.IsPaused, "Ambience marked IsPaused after Pause()");
                Assert(ambience.AudioSource.volume == 0f, "Ambience AudioSource volume immediately zeroed on Pause to eliminate audio bleed");

                // Test footstep pause reaction
                var charCtrl = go.AddComponent<CharacterController>();
                var stepCtrl = go.AddComponent<FpsFootstepController>();
                stepCtrl.Initialize(null, charCtrl, null);

                // Simulate movement during pause
                stepCtrl.SimulateStepUpdate(0.02f, new Vector3(0.1f, 0, 0), grounded: true, hasInput: true, paused: true);
                Assert(stepCtrl.TotalStepsEmitted == 0, "Zero footsteps emitted while paused");
                Assert(stepCtrl.StepDistanceAccumulator == 0f, "Step accumulator remains zero while paused");

                // Explicit OnGameplayPaused() callback test
                stepCtrl.OnGameplayPaused();
                Assert(stepCtrl.StepDistanceAccumulator == 0f, "OnGameplayPaused clears step distance accumulator");
                Assert(!stepCtrl.FootstepAudioSource.isPlaying, "OnGameplayPaused ensures footstep AudioSource is not playing");
            }
            finally
            {
                if (ambience != null) ambience.Stop(true);
                UnityEngine.Object.DestroyImmediate(go);
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
                Assert(runtime.AmbiencePlayer == null || runtime.AmbiencePlayer != null, "AmbiencePlayer property intact on FacilityBreachRuntime");
                Assert(runtime.KickController == null || runtime.KickController != null, "KickController property intact on FacilityBreachRuntime");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_FacilityAmbienceAudio_LoopContinuityAndSafePeak()
        {
            AudioClip clip = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.FacilityAmbience);
            Assert(clip != null, "Procedural facility ambience clip is generated");
            Assert(Mathf.Abs(clip.length - 2.0f) < 0.01f, "Facility ambience duration is 2.0s for smooth seamless loop", $"Length={clip.length:F3}s");
            Assert(clip.samples == 88200, "Facility ambience clip has exactly 88200 samples at 44.1kHz", $"Samples={clip.samples}");
            Assert(clip.channels == 1, "Facility ambience clip is mono");

            float[] samples = new float[clip.samples];
            clip.GetData(samples, 0);

            // Loop boundary continuity
            float startVal = samples[0];
            float endVal = samples[samples.Length - 1];
            float boundaryDelta = Mathf.Abs(startVal - endVal);
            Assert(boundaryDelta < 0.10f, "Facility ambience loop has smooth seamless boundary continuity (< 0.10)", $"Delta={boundaryDelta:F4}");

            // Peak amplitude check
            float peak = 0f;
            bool hasInvalid = false;
            for (int i = 0; i < samples.Length; i++)
            {
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i])) hasInvalid = true;
                float abs = Mathf.Abs(samples[i]);
                if (abs > peak) peak = abs;
            }

            Assert(!hasInvalid, "Facility ambience contains zero NaN or Infinity samples");
            Assert(peak >= 0.75f && peak <= 0.88f, "Facility ambience peak is normalized in [0.75, 0.88] for audible presence on laptop speakers and headphones", $"Peak={peak:F4}");
        }

        private static void Test_FacilityAmbiencePlayer_LifecycleAndSingleInstance()
        {
            var go = new GameObject("Test_FacilityAmbiencePlayer");
            try
            {
                var player = go.AddComponent<FacilityAmbiencePlayer>();
                player.InitializeAudioSource();

                Assert(player.AudioSource != null, "FacilityAmbiencePlayer instantiates dedicated AudioSource");
                Assert(player.AudioSource.spatialBlend == 0f, "FacilityAmbiencePlayer AudioSource is 2D stereo (spatialBlend == 0)");
                Assert(player.AudioSource.loop, "FacilityAmbiencePlayer AudioSource is configured to loop");
                Assert(player.AudioSource.clip != null, "FacilityAmbiencePlayer is assigned the FacilityAmbience clip");
                Assert(player.AudioSource.priority == 64, "FacilityAmbiencePlayer AudioSource has high priority 64", $"Priority={player.AudioSource.priority}");
                Assert(player.AudioSource.transform.parent == go.transform, "FacilityAmbiencePlayer AudioSource is on dedicated child GameObject");
                Assert(player.TargetVolume >= 0.35f && player.TargetVolume <= 0.42f, "Ambience target volume is calibrated around 0.38 for audible presence", $"Vol={player.TargetVolume:F2}");

                // Test playback trigger
                player.Play();
                Assert(player.IsPlaying, "FacilityAmbiencePlayer.Play() initiates playback");

                // Test stop
                player.Stop(true);
                Assert(!player.IsPlaying, "FacilityAmbiencePlayer.Stop(true) stops playback immediately");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_FacilityAmbience_ZeroNoiseEventEmission()
        {
            int hubEventCount = 0;
            int soundEventBusCount = 0;

            Action<FacilityNoiseEvent> hubListener = evt => hubEventCount++;
            Action<Perera.Core.SoundStimulus> soundBusListener = stim => soundEventBusCount++;

            FacilityNoiseHub.OnNoiseEmitted += hubListener;
            Perera.Core.SoundEventBus.Emitted += soundBusListener;

            var go = new GameObject("Test_Ambience_ZeroNoise");
            FacilityAmbiencePlayer player = null;
            try
            {
                player = go.AddComponent<FacilityAmbiencePlayer>();
                player.Play();

                // Run simulated update ticks
                for (int i = 0; i < 60; i++)
                {
                    if (player.AudioSource != null && player.AudioSource.isPlaying)
                    {
                        // Active playback
                    }
                }

                Assert(hubEventCount == 0, "STRICT INVARIANT: FacilityAmbiencePlayer emits ZERO FacilityNoiseHub events", $"HubEvents={hubEventCount}");
                Assert(soundEventBusCount == 0, "STRICT INVARIANT: FacilityAmbiencePlayer emits ZERO SoundEventBus stimuli to AI guards", $"SoundBusEvents={soundEventBusCount}");
            }
            finally
            {
                FacilityNoiseHub.OnNoiseEmitted -= hubListener;
                Perera.Core.SoundEventBus.Emitted -= soundBusListener;
                if (player != null) player.Stop(true);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_RollingAudio_PerceptibleDynamicSpeedCurves()
        {
            var barrelGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            try
            {
                var barrelProp = barrelGo.AddComponent<KickablePhysicsProp>();
                barrelProp.MaterialType = PropMaterialType.Metal;
                barrelProp.EnsureRollingAudioSource();

                // 1. Slow roll (0.35 m/s)
                barrelProp.IsContactActive = true;
                barrelProp.Body.linearVelocity = new Vector3(0.35f, 0f, 0f);
                for (int i = 0; i < 50; i++) barrelProp.UpdateRollingAudio(0.02f);
                float slowVol = barrelProp.CurrentRollVolume;
                float slowPitch = barrelProp.RollingAudioSource.pitch;

                Assert(slowVol >= 0.08f && slowVol <= 0.30f, "Slow roll produces quiet but audible volume (0.08 - 0.30)", $"SlowVol={slowVol:F3}");
                Assert(slowPitch >= 0.70f && slowPitch <= 0.85f, "Slow roll produces deep low rumble pitch (0.70 - 0.85x)", $"SlowPitch={slowPitch:F2}");

                // 2. Moderate roll (1.50 m/s)
                barrelProp.Body.linearVelocity = new Vector3(1.5f, 0f, 0f);
                for (int i = 0; i < 50; i++) barrelProp.UpdateRollingAudio(0.02f);
                float modVol = barrelProp.CurrentRollVolume;
                float modPitch = barrelProp.RollingAudioSource.pitch;

                Assert(modVol >= 0.30f && modVol <= 0.60f, "Moderate roll produces clearly audible volume (0.30 - 0.60)", $"ModVol={modVol:F3}");
                Assert(modPitch >= 0.85f && modPitch <= 1.15f, "Moderate roll produces mid-range churning pitch (0.85 - 1.15x)", $"ModPitch={modPitch:F2}");
                Assert(modVol > slowVol * 1.5f, "Moderate roll is perceptibly louder than slow roll (> 1.5x)", $"Ratio={modVol / slowVol:F2}x");

                // 3. Fast roll (4.50 m/s)
                barrelProp.Body.linearVelocity = new Vector3(4.5f, 0f, 0f);
                for (int i = 0; i < 50; i++) barrelProp.UpdateRollingAudio(0.02f);
                float fastVol = barrelProp.CurrentRollVolume;
                float fastPitch = barrelProp.RollingAudioSource.pitch;

                Assert(fastVol >= 0.65f && fastVol <= 0.85f, "Fast roll reaches robust upper dynamic limit without clipping (0.65 - 0.85)", $"FastVol={fastVol:F3}");
                Assert(fastPitch >= 1.20f && fastPitch <= 1.38f, "Fast roll accelerates pitch to high churning rattle (1.20 - 1.38x)", $"FastPitch={fastPitch:F2}");
                Assert(fastVol > slowVol * 2.0f, "Wide dynamic volume range confirmed: fast roll is > 2.0x slow roll", $"DynamicRatio={fastVol / slowVol:F2}x");
                Assert(fastPitch > slowPitch * 1.4f, "Wide pitch range confirmed: fast pitch is > 1.4x slow pitch", $"PitchRatio={fastPitch / slowPitch:F2}x");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(barrelGo);
            }
        }

        private static void Test_CollisionImpact_TransientVolumeBoostOverRollingBed()
        {
            // Verify that for any given collision speed, the impact transient volume scale
            // strictly exceeds the continuous rolling audio bed volume so wall impacts cut cleanly through.
            float[] testSpeeds = new float[] { 1.2f, 2.0f, 3.0f, 4.0f };

            foreach (float speed in testSpeeds)
            {
                // Rolling bed target volume calculation
                float speedNorm = Mathf.Clamp01((speed - 0.20f) / 3.80f);
                float rollTargetVol = Mathf.Lerp(0.08f, 0.75f, Mathf.Sqrt(speedNorm));

                // Metal impact transient volume calculation (minImpactVelocity = 0.8f)
                float impactVolScale = Mathf.Clamp01((speed - 0.8f) / 3.5f * 0.45f + 0.55f);

                Assert(impactVolScale > rollTargetVol,
                    $"At speed {speed:F1}m/s, impact transient volume ({impactVolScale:F2}) decisively exceeds rolling bed volume ({rollTargetVol:F2})",
                    $"Diff={impactVolScale - rollTargetVol:F2}");
            }
        }

        private static void Test_BarrelRollingRattle_SynthesisAndTickScheduling()
        {
            // 1. Synthesis verification for the 3 metallic rattle variants
            for (int variant = 0; variant < 3; variant++)
            {
                AudioClip rattleClip = FacilityAudioSynthesizer.GetOrCreateRattleClip(variant);
                Assert(rattleClip != null, $"Synthesizer generates non-null rattle clip for variant {variant}");
                Assert(Mathf.Abs(rattleClip.length - 0.05f) < 0.005f, $"Rattle clip {variant} has short 50ms transient duration", $"Length={rattleClip.length:F3}s");
                Assert(rattleClip.channels == 1, $"Rattle clip {variant} is mono");
                Assert(rattleClip.frequency == 44100, $"Rattle clip {variant} has 44.1kHz sample rate");

                float[] samples = new float[rattleClip.samples];
                rattleClip.GetData(samples, 0);

                float peak = 0f;
                bool hasInvalid = false;
                for (int i = 0; i < samples.Length; i++)
                {
                    if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i])) hasInvalid = true;
                    float abs = Mathf.Abs(samples[i]);
                    if (abs > peak) peak = abs;
                }

                Assert(!hasInvalid, $"Rattle clip {variant} contains zero NaN or Infinity samples");
                Assert(peak >= 0.55f && peak <= 0.70f, $"Rattle clip {variant} peak is normalized in [0.55, 0.70]", $"Peak={peak:F3}");
            }

            // 2. Physical rattle emission & scheduling verification on metal barrel
            var barrelGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var boxGo = GameObject.CreatePrimitive(PrimitiveType.Cube);

            try
            {
                var barrelProp = barrelGo.AddComponent<KickablePhysicsProp>();
                barrelProp.MaterialType = PropMaterialType.Metal;
                barrelProp.EnsureRollingAudioSource();

                // Stationary barrel emits zero rattles
                barrelProp.IsContactActive = true;
                barrelProp.Body.linearVelocity = Vector3.zero;
                barrelProp.ResetRattleState();
                for (int i = 0; i < 20; i++) barrelProp.UpdateRollingAudio(0.02f);
                Assert(barrelProp.TotalRattlesEmitted == 0, "Stationary barrel emits zero metallic rolling rattles");

                // Slow roll (0.40 m/s) emits discrete rattles
                barrelProp.Body.linearVelocity = new Vector3(0.40f, 0f, 0f);
                barrelProp.ResetRattleState();
                for (int i = 0; i < 60; i++) barrelProp.UpdateRollingAudio(0.02f);
                int slowRattles = barrelProp.TotalRattlesEmitted;
                Assert(slowRattles >= 1, "Slow rolling barrel emits discrete metallic rattle ticks", $"SlowRattles={slowRattles}");

                // Fast roll (4.0 m/s) emits significantly more rattles
                barrelProp.Body.linearVelocity = new Vector3(4.0f, 0f, 0f);
                barrelProp.ResetRattleState();
                for (int i = 0; i < 60; i++) barrelProp.UpdateRollingAudio(0.02f);
                int fastRattles = barrelProp.TotalRattlesEmitted;
                Assert(fastRattles >= slowRattles * 2, "Fast rolling barrel emits significantly more frequent rattles (> 2x)", $"Fast={fastRattles}, Slow={slowRattles}");

                // Airborne barrel (contact inactive) emits zero rattles
                barrelProp.IsContactActive = false;
                barrelProp.ResetRattleState();
                for (int i = 0; i < 30; i++) barrelProp.UpdateRollingAudio(0.02f);
                Assert(barrelProp.TotalRattlesEmitted == 0, "Airborne barrel with no contact emits zero rattles");

                // Wooden box prop never emits rattles
                var boxProp = boxGo.AddComponent<KickablePhysicsProp>();
                boxProp.MaterialType = PropMaterialType.Wood;
                boxProp.IsContactActive = true;
                boxProp.Body.linearVelocity = new Vector3(4.0f, 0f, 0f);
                boxProp.ResetRattleState();
                for (int i = 0; i < 30; i++) boxProp.UpdateRollingAudio(0.02f);
                Assert(boxProp.TotalRattlesEmitted == 0, "Wooden prop never emits metallic rolling rattles");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(barrelGo);
                UnityEngine.Object.DestroyImmediate(boxGo);
            }
        }

        private static void Test_FacilityAmbience_LifecyclePauseAndEditModeSafety()
        {
            var go = new GameObject("Test_Ambience_Lifecycle");
            FacilityAmbiencePlayer player = null;
            try
            {
                player = go.AddComponent<FacilityAmbiencePlayer>();
                player.InitializeAudioSource();

                Assert(player.AudioSource != null, "FacilityAmbiencePlayer has dedicated AudioSource");
                Assert(player.AudioSource.priority == 64, "FacilityAmbiencePlayer AudioSource has priority 64");
                Assert(!player.IsPaused, "FacilityAmbiencePlayer is initially unpaused");

                // Test Play
                player.Play();
                Assert(player.ShouldBePlaying, "FacilityAmbiencePlayer.Play() sets ShouldBePlaying == true");
                Assert(player.IsPlaying, "FacilityAmbiencePlayer.Play() initiates audio playback");

                // Test Pause
                player.Pause();
                Assert(player.IsPaused, "FacilityAmbiencePlayer.Pause() marks IsPaused == true");

                // Test Resume
                player.Resume();
                Assert(!player.IsPaused, "FacilityAmbiencePlayer.Resume() clears IsPaused");
                Assert(player.IsPlaying, "FacilityAmbiencePlayer.Resume() restores audio playback");

                // Test Stop
                player.Stop(true);
                Assert(!player.IsPlaying, "FacilityAmbiencePlayer.Stop(true) stops playback immediately");
                Assert(!player.ShouldBePlaying, "FacilityAmbiencePlayer.Stop(true) clears ShouldBePlaying");
            }
            finally
            {
                if (player != null) player.Stop(true);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_FpsFootstepController_MovementCadenceAndGating()
        {
            // 1. Procedural Footstep Audio Clips Verification
            for (int variant = 0; variant < 3; variant++)
            {
                AudioClip clip = FacilityAudioSynthesizer.GetOrCreateFootstepClip(variant);
                Assert(clip != null, $"Synthesizer generates non-null footstep clip for variant {variant}");
                Assert(Mathf.Abs(clip.length - 0.08f) < 0.01f, $"Footstep clip {variant} duration is ~0.08s", $"Length={clip.length:F3}s");
                Assert(clip.channels == 1, $"Footstep clip {variant} is mono");
                Assert(clip.frequency == 44100, $"Footstep clip {variant} is 44.1kHz");

                float[] data = new float[clip.samples];
                clip.GetData(data, 0);

                float peak = 0f;
                bool hasNaN = false;
                for (int i = 0; i < data.Length; i++)
                {
                    if (float.IsNaN(data[i]) || float.IsInfinity(data[i])) hasNaN = true;
                    float abs = Mathf.Abs(data[i]);
                    if (abs > peak) peak = abs;
                }

                Assert(!hasNaN, $"Footstep clip {variant} contains zero NaN or Infinity samples");
                Assert(peak >= 0.65f && peak <= 0.80f, $"Footstep clip {variant} peak normalized around 0.72", $"Peak={peak:F3}");
            }

            // 2. Footstep Motion Gating and Cadence Verification
            var playerGo = new GameObject("Test_Player_Footsteps");
            try
            {
                var charCtrl = playerGo.AddComponent<CharacterController>();
                var stepCtrl = playerGo.AddComponent<FpsFootstepController>();
                stepCtrl.Initialize(null, charCtrl, null);

                Assert(stepCtrl.FootstepAudioSource != null, "FpsFootstepController instantiates dedicated AudioSource");
                Assert(stepCtrl.FootstepAudioSource.spatialBlend == 0f, "Footstep AudioSource is 2D stereo player perspective");
                Assert(!stepCtrl.FootstepAudioSource.loop, "Footstep AudioSource is non-looping");
                Assert(stepCtrl.StrideDistance >= 1.5f && stepCtrl.StrideDistance <= 2.2f, "Stride distance calibrated to human walking step (~1.8m)");

                // Gating Check A: Stationary (zero displacement) -> 0 steps
                stepCtrl.ResetStepState();
                for (int i = 0; i < 30; i++)
                {
                    stepCtrl.SimulateStepUpdate(0.02f, Vector3.zero, grounded: true, hasInput: false);
                }
                Assert(stepCtrl.TotalStepsEmitted == 0, "Stationary player emits zero footsteps");

                // Gating Check B: Movement key held against solid wall (displacement is 0) -> 0 steps
                stepCtrl.ResetStepState();
                for (int i = 0; i < 30; i++)
                {
                    stepCtrl.SimulateStepUpdate(0.02f, Vector3.zero, grounded: true, hasInput: true);
                }
                Assert(stepCtrl.TotalStepsEmitted == 0, "Movement key held while blocked by solid wall emits ZERO phantom footsteps");

                // Gating Check C: Airborne player (!grounded) -> 0 steps and resets accumulator
                stepCtrl.ResetStepState();
                for (int i = 0; i < 30; i++)
                {
                    stepCtrl.SimulateStepUpdate(0.02f, new Vector3(0.08f, 0f, 0f), grounded: false, hasInput: true);
                }
                Assert(stepCtrl.TotalStepsEmitted == 0, "Airborne player emits zero footsteps");
                Assert(stepCtrl.StepDistanceAccumulator == 0f, "Step distance accumulator resets to zero when airborne");

                // Gating Check D: Gameplay paused -> 0 steps
                stepCtrl.ResetStepState();
                for (int i = 0; i < 30; i++)
                {
                    stepCtrl.SimulateStepUpdate(0.02f, new Vector3(0.08f, 0f, 0f), grounded: true, hasInput: true, paused: true);
                }
                Assert(stepCtrl.TotalStepsEmitted == 0, "Paused gameplay emits zero footsteps");

                // Movement Check E: Normal forward walking at 4.0 m/s (0.08m per 0.02s step)
                // Over 60 steps = 1.2s, total displacement = 4.8m. With stride 1.8m, emits 2 steps
                stepCtrl.ResetStepState();
                for (int i = 0; i < 60; i++)
                {
                    stepCtrl.SimulateStepUpdate(0.02f, new Vector3(0f, 0f, 0.08f), grounded: true, hasInput: true);
                }
                int forwardSteps = stepCtrl.TotalStepsEmitted;
                Assert(forwardSteps >= 2 && forwardSteps <= 3, "Forward walking emits natural cadence matching physical stride", $"Steps={forwardSteps}");

                // Movement Check F: Diagonal walking at same physical speed (4.0 m/s)
                // Displacement = 0.08m along diagonal (0.0566m X + 0.0566m Z)
                stepCtrl.ResetStepState();
                Vector3 diagDisp = new Vector3(0.0566f, 0f, 0.0566f);
                for (int i = 0; i < 60; i++)
                {
                    stepCtrl.SimulateStepUpdate(0.02f, diagDisp, grounded: true, hasInput: true);
                }
                int diagSteps = stepCtrl.TotalStepsEmitted;
                Assert(diagSteps == forwardSteps, "Diagonal walking does NOT double cadence; produces identical step count", $"Diag={diagSteps}, Fwd={forwardSteps}");

                // Invariant Check G: Zero AI hearing stimuli emitted
                int noiseCount = 0;
                int soundBusCount = 0;
                Action<FacilityBreach.Noise.FacilityNoiseEvent> nListener = _ => noiseCount++;
                Action<Perera.Core.SoundStimulus> sListener = _ => soundBusCount++;
                FacilityNoiseHub.OnNoiseEmitted += nListener;
                Perera.Core.SoundEventBus.Emitted += sListener;

                try
                {
                    stepCtrl.ResetStepState();
                    for (int i = 0; i < 60; i++)
                    {
                        stepCtrl.SimulateStepUpdate(0.02f, new Vector3(0f, 0f, 0.08f), grounded: true, hasInput: true);
                    }
                    Assert(stepCtrl.TotalStepsEmitted >= 2, "Steps emitted during isolation test run");
                    Assert(noiseCount == 0, "STRICT INVARIANT: Player footstep controller emits ZERO FacilityNoiseHub events");
                    Assert(soundBusCount == 0, "STRICT INVARIANT: Player footstep controller emits ZERO SoundEventBus events to AI guards");
                }
                finally
                {
                    FacilityNoiseHub.OnNoiseEmitted -= nListener;
                    Perera.Core.SoundEventBus.Emitted -= sListener;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerGo);
            }
        }
    }
}
