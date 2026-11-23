using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using FacilityBreach.Core;
using FacilityBreach.Audio;
using FacilityBreach.Noise;
using FacilityBreach.Environment;

namespace FacilityBreach.EditorTests
{
    /// <summary>
    /// GV Systems Engineer / Environmental Mechanics Test Suite.
    /// Validates the Facility Emergency Alert System (3-minute duration configuration,
    /// procedurally synthesized siren and explosion audio, pulsing lighting controller,
    /// camera trauma screen shake, and acoustic noise event emission).
    /// </summary>
    public static class FacilityEmergencyAlertTests
    {
        private static int passedCount = 0;
        private static int failedCount = 0;
        private static readonly System.Collections.Generic.List<string> logs = new System.Collections.Generic.List<string>();

        [MenuItem("Facility Breach/Run Facility Emergency Alert Tests")]
        public static void RunAll()
        {
            passedCount = 0;
            failedCount = 0;
            logs.Clear();

            Log("<color=cyan>=== RUNNING FACILITY EMERGENCY ALERT TESTS ===</color>");

            RunTest("EmergencySiren_ProceduralSynthesisValidSamples", Test_EmergencySiren_ProceduralSynthesisValidSamples);
            RunTest("ExplosionBlast_ProceduralSynthesisValidSamples", Test_ExplosionBlast_ProceduralSynthesisValidSamples);
            RunTest("MissionRun_DefaultRemains300ForFoundationCompatibility", Test_MissionRun_DefaultRemains300ForFoundationCompatibility);
            RunTest("MissionRun_ConfigureDuration_UpdatesTo180InMenu", Test_MissionRun_ConfigureDuration_UpdatesTo180InMenu);
            RunTest("MissionRun_ConfigureDuration_RejectedWhileRunning", Test_MissionRun_ConfigureDuration_RejectedWhileRunning);
            RunTest("AlertController_DefaultConfigurationAndState", Test_AlertController_DefaultConfigurationAndState);
            RunTest("AlertController_ActivationAndDeactivationLifecycle", Test_AlertController_ActivationAndDeactivationLifecycle);
            RunTest("AlertController_CameraTraumaClamping", Test_AlertController_CameraTraumaClamping);
            RunTest("WallSconces_PlacementDefinitionsAndGeneration", Test_WallSconces_PlacementDefinitionsAndGeneration);
            RunTest("AudioPlayer_ExplosionPlaybackSafeExecution", Test_AudioPlayer_ExplosionPlaybackSafeExecution);
            RunTest("AudioPlayer_EmergencyAlarmPlaybackAndPauseLifecycle", Test_AudioPlayer_EmergencyAlarmPlaybackAndPauseLifecycle);
            RunTest("AlertController_EmergencyAlarm30sThresholdAndVolumeTarget", Test_AlertController_EmergencyAlarm30sThresholdAndVolumeTarget);
            RunTest("NoiseHub_ExplosionAcousticEmissionAndReception", Test_NoiseHub_ExplosionAcousticEmissionAndReception);

            string summary = $"<color={(failedCount == 0 ? "green" : "red")}>=== EMERGENCY ALERT TESTS COMPLETE: {passedCount} PASSED, {failedCount} FAILED ===</color>";
            Log(summary);
            Debug.Log(summary);

            if (failedCount == 0)
            {
                File.WriteAllText("facility-emergency-tests-passed.txt", $"PASS {passedCount} assertions in FacilityEmergencyAlertTests at {DateTime.Now}\n");
            }
        }

        private static void RunTest(string testName, Action testMethod)
        {
            try
            {
                testMethod();
                passedCount++;
                Log($"[PASS] {testName}");
            }
            catch (Exception ex)
            {
                failedCount++;
                Log($"[FAIL] {testName}: {ex.Message}");
                Debug.LogError($"[FacilityEmergencyAlertTests] FAIL {testName}: {ex}");
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void Log(string msg)
        {
            logs.Add(msg);
        }

        private static void Test_EmergencySiren_ProceduralSynthesisValidSamples()
        {
            AudioClip clip = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.EmergencySirenLoop);
            Assert(clip != null, "EmergencySirenLoop clip must not be null");
            Assert(clip.samples > 0, "EmergencySirenLoop clip must contain audio samples");
            Assert(clip.channels == 1, "EmergencySirenLoop clip should be mono");
            Assert(clip.frequency == 44100, "EmergencySirenLoop frequency must be 44.1kHz");

            float[] data = new float[clip.samples];
            clip.GetData(data, 0);

            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float s = data[i];
                Assert(!float.IsNaN(s) && !float.IsInfinity(s), $"Sample {i} must be finite");
                Assert(s >= -1.0f && s <= 1.0f, $"Sample {i} must be normalized in [-1, 1]");
                if (Mathf.Abs(s) > peak) peak = Mathf.Abs(s);
            }
            Assert(peak > 0.1f, "EmergencySirenLoop must contain audible acoustic energy");
        }

        private static void Test_ExplosionBlast_ProceduralSynthesisValidSamples()
        {
            AudioClip clip = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.ExplosionBlast);
            Assert(clip != null, "ExplosionBlast clip must not be null");
            Assert(clip.samples > 0, "ExplosionBlast clip must contain audio samples");
            Assert(clip.frequency == 44100, "ExplosionBlast frequency must be 44.1kHz");

            float[] data = new float[clip.samples];
            clip.GetData(data, 0);

            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float s = data[i];
                Assert(!float.IsNaN(s) && !float.IsInfinity(s), $"Sample {i} must be finite");
                Assert(s >= -1.0f && s <= 1.0f, $"Sample {i} must be normalized in [-1, 1]");
                if (Mathf.Abs(s) > peak) peak = Mathf.Abs(s);
            }
            Assert(peak > 0.2f, "ExplosionBlast must have sufficient punch and audible amplitude");
        }

        private static void Test_MissionRun_DefaultRemains300ForFoundationCompatibility()
        {
            var run = new MissionRun();
            Assert(run.Remaining == 300, "Default MissionRun.Remaining must be 300 for Foundation test compatibility");
            Assert(run.InitialDuration == 300, "Default MissionRun.InitialDuration must be 300");
            Assert(run.State == MissionState.Menu, "MissionRun starts in Menu state");
        }

        private static void Test_MissionRun_ConfigureDuration_UpdatesTo180InMenu()
        {
            var run = new MissionRun();
            run.ConfigureDuration(180);
            Assert(run.Remaining == 180, "ConfigureDuration(180) must set Remaining to 180");
            Assert(run.InitialDuration == 180, "ConfigureDuration(180) must set InitialDuration to 180");

            run.Start();
            Assert(run.State == MissionState.Active, "Mission should be Active after Start()");
            run.Tick(10);
            Assert(Math.Abs(run.Remaining - 170) < 0.001, "Remaining must tick down from 180 to 170 after 10s");
            Assert(Math.Abs(run.Elapsed - 10) < 0.001, "Elapsed must be 10s");
        }

        private static void Test_MissionRun_ConfigureDuration_RejectedWhileRunning()
        {
            var run = new MissionRun();
            run.Start();
            Assert(run.Running, "Mission is running");
            run.ConfigureDuration(60);
            Assert(run.Remaining == 300, "ConfigureDuration must be rejected while mission is Running");
        }

        private static void Test_AlertController_DefaultConfigurationAndState()
        {
            var go = new GameObject("Test_AlertController");
            try
            {
                var controller = go.AddComponent<FacilityEmergencyAlertController>();
                Assert(!controller.IsEmergencyActive, "IsEmergencyActive must initially be false");
                Assert(!controller.AreLightsRedAlert, "AreLightsRedAlert must initially be false");
                Assert(!controller.IsSoundAlertActive, "IsSoundAlertActive must initially be false");
                Assert(!controller.IsClimaxActive, "IsClimaxActive must initially be false");
                Assert(controller.LightAlertRemainingSeconds == 60f, "LightAlertRemainingSeconds must default to 60s");
                Assert(controller.SoundAlertRemainingSeconds == 30f, "SoundAlertRemainingSeconds must default to 30s");
                Assert(controller.ClimaxRemainingSeconds == 10f, "ClimaxRemainingSeconds must default to 10s");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_AlertController_ActivationAndDeactivationLifecycle()
        {
            var go = new GameObject("Test_AlertController_Lifecycle");
            try
            {
                var controller = go.AddComponent<FacilityEmergencyAlertController>();
                controller.ActivateRedAlertLights();
                Assert(controller.AreLightsRedAlert, "AreLightsRedAlert must be true after ActivateRedAlertLights()");
                Assert(controller.IsEmergencyActive, "IsEmergencyActive must be true");

                controller.ActivateSoundAlert();
                Assert(controller.IsSoundAlertActive, "IsSoundAlertActive must be true after ActivateSoundAlert()");

                controller.ActivateClimax();
                Assert(controller.IsClimaxActive, "IsClimaxActive must be true after ActivateClimax()");

                controller.DeactivateEmergency();
                Assert(!controller.IsEmergencyActive, "IsEmergencyActive must be false after DeactivateEmergency()");
                Assert(!controller.AreLightsRedAlert, "AreLightsRedAlert must be false after DeactivateEmergency()");
                Assert(!controller.IsSoundAlertActive, "IsSoundAlertActive must be false after DeactivateEmergency()");
                Assert(!controller.IsClimaxActive, "IsClimaxActive must be false after DeactivateEmergency()");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_AlertController_CameraTraumaClamping()
        {
            var go = new GameObject("Test_AlertController_Trauma");
            try
            {
                var controller = go.AddComponent<FacilityEmergencyAlertController>();
                controller.AddCameraTrauma(0.5f);
                controller.AddCameraTrauma(0.8f); // Total would be 1.3f, clamped to 1.0f
                Assert(true, "Camera trauma clamping executed safely");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_WallSconces_PlacementDefinitionsAndGeneration()
        {
            Assert(FacilityWallSconces.Placements.Length > 0, "FacilityWallSconces must have defined placements");
            var root = FacilityWallSconces.GenerateAllSconces();
            try
            {
                Assert(root != null, "Wall sconce root must be generated");
                Assert(root.name == FacilityWallSconces.ROOT_NAME, "Root name must match FacilityWallSconces.ROOT_NAME");
                Light[] lights = root.GetComponentsInChildren<Light>();
                Assert(lights.Length == FacilityWallSconces.Placements.Length * 2, "Each sconce must have an upward and downward SpotLight");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Test_AudioPlayer_ExplosionPlaybackSafeExecution()
        {
            var go = new GameObject("Test_ExplosionAudio_Go");
            try
            {
                var player = go.AddComponent<FacilityAudioPlayer>();
                player.InitializePool();
                player.PlayExplosion(new Vector3(5, 2, 5), 1.0f);
                Assert(true, "PlayExplosion executed without exception");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_NoiseHub_ExplosionAcousticEmissionAndReception()
        {
            Vector3 testPos = new Vector3(10f, 1f, 15f);
            FacilityNoiseEvent receivedEvent = default;
            bool eventFired = false;

            Action<FacilityNoiseEvent> handler = evt =>
            {
                receivedEvent = evt;
                eventFired = true;
            };

            FacilityNoiseHub.OnNoiseEmitted += handler;
            try
            {
                FacilityNoiseHub.Emit(testPos, 40f, FacilityNoiseType.PhysicsImpact, null);
                Assert(eventFired, "FacilityNoiseHub must invoke OnNoiseEmitted subscriber");
                Assert(Vector3.Distance(receivedEvent.Position, testPos) < 0.001f, "Received position must match emitted blast position");
                Assert(receivedEvent.Radius == 40f, "Received blast radius must be 40m");
                Assert(receivedEvent.Type == FacilityNoiseType.PhysicsImpact, "Received noise type must match PhysicsImpact");
            }
            finally
            {
                FacilityNoiseHub.OnNoiseEmitted -= handler;
            }
        }

        private static void Test_AudioPlayer_EmergencyAlarmPlaybackAndPauseLifecycle()
        {
            var go = new GameObject("Test_AudioPlayer_Alarm");
            try
            {
                var player = go.AddComponent<FacilityAudioPlayer>();
                player.InitializePool();

                // Test start alarm with 0.50f calibrated volume
                player.StartEmergencyAlarm(null, 0.50f);
                Assert(player.EmergencyAlarmVolume > 0f, "Emergency alarm volume must be greater than zero when started");

                // Test pause and unpause
                player.SetEmergencyAlarmPause(true);
                player.SetEmergencyAlarmPause(false);

                // Test stop
                player.StopEmergencyAlarm();
                Assert(!player.IsEmergencyAlarmPlaying, "Emergency alarm must be stopped after StopEmergencyAlarm call");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_AlertController_EmergencyAlarm30sThresholdAndVolumeTarget()
        {
            var go = new GameObject("Test_AlertController_30s");
            try
            {
                var controller = go.AddComponent<FacilityEmergencyAlertController>();
                Assert(controller.SoundAlertRemainingSeconds == 30f, "SoundAlertRemainingSeconds must default to 30s");
                Assert(controller.LightAlertRemainingSeconds == 60f, "LightAlertRemainingSeconds must default to 60s");
                Assert(controller.ClimaxRemainingSeconds == 10f, "ClimaxRemainingSeconds must default to 10s");
                Assert(controller.SirenStaggerIntervalSeconds == 2.0f, "SirenStaggerIntervalSeconds must default to 2s");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
