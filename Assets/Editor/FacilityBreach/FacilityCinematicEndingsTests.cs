using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using FacilityBreach.Core;
using FacilityBreach.Audio;

namespace FacilityBreach.EditorTests
{
    /// <summary>
    /// GV Systems Engineer & Technical Lead Test Suite.
    /// Validates the Cinematic Mission Endings System:
    /// 1. Meltdown Detonation (catastrophic explosion shockwave & camera tremor)
    /// 2. Operative Fatal KIA (first-person ground collapse & cardiac flatline vitals)
    /// 3. Extraction & Tactical Lockdown (blast door lock & secure facility sealing)
    /// </summary>
    public static class FacilityCinematicEndingsTests
    {
        private static int passedCount = 0;
        private static int failedCount = 0;
        private static readonly System.Collections.Generic.List<string> logs = new System.Collections.Generic.List<string>();

        [MenuItem("Facility Breach/Run Cinematic Mission Endings Tests")]
        public static void RunAll()
        {
            passedCount = 0;
            failedCount = 0;
            logs.Clear();

            Log("<color=cyan>=== RUNNING CINEMATIC MISSION ENDINGS TESTS ===</color>");

            RunTest("ProceduralAudio_MeltdownExplosion_ValidSynthesizedBuffer", Test_ProceduralAudio_MeltdownExplosion_ValidSynthesizedBuffer);
            RunTest("ProceduralAudio_PlayerDeathVitals_ValidSynthesizedBuffer", Test_ProceduralAudio_PlayerDeathVitals_ValidSynthesizedBuffer);
            RunTest("ProceduralAudio_BlastDoorLock_ValidSynthesizedBuffer", Test_ProceduralAudio_BlastDoorLock_ValidSynthesizedBuffer);
            RunTest("CinematicEndingsController_InitialStateAndDefaults", Test_CinematicEndingsController_InitialStateAndDefaults);
            RunTest("MeltdownExplosion_TriggerAndTimerProgression", Test_MeltdownExplosion_TriggerAndTimerProgression);
            RunTest("PlayerDeathKIA_TriggerAndCameraFallTransform", Test_PlayerDeathKIA_TriggerAndCameraFallTransform);
            RunTest("ExtractionLockdown_TriggerAndStatusTransition", Test_ExtractionLockdown_TriggerAndStatusTransition);
            RunTest("ResetSequence_RestoresCameraAndFlags", Test_ResetSequence_RestoresCameraAndFlags);
            RunTest("ToxicBarrelMeltdown_SmokeVentingThresholdAndDetonationLifecycle", Test_ToxicBarrelMeltdown_SmokeVentingThresholdAndDetonationLifecycle);
            RunTest("AudioPlayer_EndingHelperMethodsSafeExecution", Test_AudioPlayer_EndingHelperMethodsSafeExecution);

            string summary = $"<color={(failedCount == 0 ? "green" : "red")}>=== CINEMATIC ENDINGS TESTS COMPLETE: {passedCount} PASSED, {failedCount} FAILED ===</color>";
            Log(summary);
            Debug.Log(summary);

            if (failedCount == 0)
            {
                File.WriteAllText("facility-cinematic-endings-tests-passed.txt", $"PASS {passedCount} assertions in FacilityCinematicEndingsTests at {DateTime.Now}\n");
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
                Debug.LogError($"[FacilityCinematicEndingsTests] FAIL {testName}: {ex}");
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

        private static void Test_ProceduralAudio_MeltdownExplosion_ValidSynthesizedBuffer()
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
            Assert(peak > 0.1f, $"ExplosionBlast should have audible peak volume (>0.1), got {peak}");
        }

        private static void Test_ProceduralAudio_PlayerDeathVitals_ValidSynthesizedBuffer()
        {
            AudioClip clip = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.PlayerDeathVitals);
            Assert(clip != null, "PlayerDeathVitals clip must not be null");
            Assert(clip.samples > 0, "PlayerDeathVitals clip must contain audio samples");
            Assert(clip.channels == 1, "PlayerDeathVitals clip must be mono");
            Assert(clip.frequency == 44100, "PlayerDeathVitals frequency must be 44.1kHz");
            Assert(clip.length >= 4.9f, $"PlayerDeathVitals duration should be at least 4.9s for 5.0s sequence, got {clip.length}");

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
            Assert(peak > 0.05f, $"PlayerDeathVitals should have audible peak volume (>0.05), got {peak}");
        }

        private static void Test_ProceduralAudio_BlastDoorLock_ValidSynthesizedBuffer()
        {
            AudioClip clip = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.BlastDoorLock);
            Assert(clip != null, "BlastDoorLock clip must not be null");
            Assert(clip.samples > 0, "BlastDoorLock clip must contain audio samples");
            Assert(clip.channels == 1, "BlastDoorLock clip must be mono");
            Assert(clip.frequency == 44100, "BlastDoorLock frequency must be 44.1kHz");

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
            Assert(peak > 0.05f, $"BlastDoorLock should have audible peak volume (>0.05), got {peak}");
        }

        private static void Test_CinematicEndingsController_InitialStateAndDefaults()
        {
            var go = new GameObject("Test_EndingsController_Init");
            try
            {
                var controller = go.AddComponent<FacilityCinematicEndingsController>();
                Assert(controller.ActiveEnding == EndingSequenceType.None, "ActiveEnding must initially be None");
                Assert(!controller.IsPlaying, "IsPlaying must initially be false");
                Assert(!controller.IsComplete, "IsComplete must initially be false");
                Assert(controller.NormalizedProgress == 0f, "NormalizedProgress must initially be 0");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_MeltdownExplosion_TriggerAndTimerProgression()
        {
            var go = new GameObject("Test_EndingsController_Meltdown");
            try
            {
                var controller = go.AddComponent<FacilityCinematicEndingsController>();
                controller.TriggerMeltdownExplosion(Vector3.zero);

                Assert(controller.ActiveEnding == EndingSequenceType.MeltdownExplosion, "ActiveEnding should be MeltdownExplosion");
                Assert(controller.IsPlaying, "IsPlaying should be true after triggering");
                Assert(!controller.IsComplete, "IsComplete should be false immediately after trigger");

                // Tick partially
                controller.Tick(1.0f);
                Assert(controller.IsPlaying, "IsPlaying should still be true at t=1.0s");
                Assert(controller.NormalizedProgress > 0.3f && controller.NormalizedProgress < 0.6f,
                    $"NormalizedProgress should be around 0.4-0.5, got {controller.NormalizedProgress}");

                // Tick to finish (duration is 2.5s)
                controller.Tick(2.0f);
                Assert(!controller.IsPlaying, "IsPlaying should be false after sequence duration elapsed");
                Assert(controller.IsComplete, "IsComplete should be true after sequence duration elapsed");
                Assert(controller.NormalizedProgress >= 1.0f, "NormalizedProgress should reach 1.0");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_PlayerDeathKIA_TriggerAndCameraFallTransform()
        {
            var root = new GameObject("Test_Endings_PlayerRoot");
            var camGo = new GameObject("Test_Endings_Camera");
            camGo.transform.SetParent(root.transform);
            camGo.transform.localPosition = new Vector3(0, 1.6f, 0);
            camGo.transform.localRotation = Quaternion.identity;

            try
            {
                var controller = root.AddComponent<FacilityCinematicEndingsController>();
                controller.BindCamera(camGo.GetComponent<Camera>() ?? camGo.AddComponent<Camera>());
                controller.TriggerPlayerDeathKIA(root.transform.position);

                Assert(controller.ActiveEnding == EndingSequenceType.PlayerDeathKIA, "ActiveEnding should be PlayerDeathKIA");
                Assert(controller.IsPlaying, "IsPlaying should be true");

                // Advance by 2.0s
                controller.Tick(2.0f);
                Assert(camGo.transform.localPosition.y < 1.6f,
                    $"Camera height should descend from 1.6m during KIA fall, got {camGo.transform.localPosition.y}");
                Assert(controller.IsPlaying, "IsPlaying should still be true at t=2.0s during 5.0s death sequence");

                // Advance past 5.0s duration
                controller.Tick(3.2f);
                Assert(controller.IsComplete, "IsComplete should be true after 5.0s");
                Assert(camGo.transform.localPosition.y <= 0.35f,
                    $"Camera height should reach ground level (<=0.35m), got {camGo.transform.localPosition.y}");
                
                // Roll check: eulerAngles.z should be non-zero
                float zRoll = Mathf.Abs(camGo.transform.localEulerAngles.z);
                Assert(zRoll > 10f, $"Camera should have experienced sideways ground roll (>10 deg), got {zRoll}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Test_ExtractionLockdown_TriggerAndStatusTransition()
        {
            var go = new GameObject("Test_EndingsController_Extraction");
            try
            {
                var controller = go.AddComponent<FacilityCinematicEndingsController>();
                controller.TriggerExtractionLockdown(new Vector3(10, 0, 10));

                Assert(controller.ActiveEnding == EndingSequenceType.ExtractionLockdown, "ActiveEnding should be ExtractionLockdown");
                Assert(controller.IsPlaying, "IsPlaying should be true");

                // Tick to completion (duration 2.0s)
                controller.Tick(2.2f);
                Assert(controller.IsComplete, "IsComplete should be true after 2.0s");
                Assert(!controller.IsPlaying, "IsPlaying should be false");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_ResetSequence_RestoresCameraAndFlags()
        {
            var root = new GameObject("Test_Endings_ResetRoot");
            var camGo = new GameObject("Test_Endings_ResetCamera");
            camGo.transform.SetParent(root.transform);
            Vector3 originalPos = new Vector3(0, 1.6f, 0);
            Quaternion originalRot = Quaternion.identity;
            camGo.transform.localPosition = originalPos;
            camGo.transform.localRotation = originalRot;

            try
            {
                var controller = root.AddComponent<FacilityCinematicEndingsController>();
                controller.BindCamera(camGo.GetComponent<Camera>() ?? camGo.AddComponent<Camera>());
                controller.TriggerPlayerDeathKIA(root.transform.position);

                // Run halfway
                controller.Tick(1.2f);
                Assert(camGo.transform.localPosition != originalPos, "Camera position should have changed during fall");

                // Reset sequence
                controller.ResetSequence();
                Assert(controller.ActiveEnding == EndingSequenceType.None, "ActiveEnding should be None after ResetSequence");
                Assert(!controller.IsPlaying, "IsPlaying should be false after ResetSequence");
                Assert(!controller.IsComplete, "IsComplete should be false after ResetSequence");
                Assert(Vector3.Distance(camGo.transform.localPosition, originalPos) < 0.001f,
                    "Camera localPosition should be restored to initial state");
                Assert(Quaternion.Angle(camGo.transform.localRotation, originalRot) < 0.01f,
                    "Camera localRotation should be restored to initial state");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Test_ToxicBarrelMeltdown_SmokeVentingThresholdAndDetonationLifecycle()
        {
            var root = new GameObject("Test_ToxicBarrelsRoot");
            var drum1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            drum1.name = "Barrel"; drum1.transform.SetParent(root.transform);
            var lid1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lid1.name = "Barrel lid"; lid1.transform.SetParent(root.transform);

            try
            {
                var vfx = root.AddComponent<FacilityBreach.Environment.FacilityToxicBarrelMeltdownVfx>();
                vfx.Initialize(root.transform, new Vector3(-2, 0, 20));

                Assert(vfx.ClusterCenter == new Vector3(-2, 0, 20), "ClusterCenter matches initialized vector");
                Assert(vfx.TrackedBarrelCount == 2, $"TrackedBarrelCount should be 2, got {vfx.TrackedBarrelCount}");
                Assert(!vfx.IsVentingSmoke, "IsVentingSmoke initially false");
                Assert(!vfx.HasDetonated, "HasDetonated initially false");

                // Update with remaining = 20.0s (no smoke yet, >15s threshold)
                vfx.UpdateCountdown(20.0f);
                Assert(!vfx.IsVentingSmoke, "Should not vent smoke at remaining 20.0s (>15s)");

                // Update with remaining = 14.5s (triggers smoke venting in last 15s)
                vfx.UpdateCountdown(14.5f);
                Assert(vfx.IsVentingSmoke, "Should vent smoke at remaining 14.5s (<=15s)");
                Assert(!vfx.HasDetonated, "HasDetonated should remain false while venting");

                // Detonate at 0.0s
                vfx.UpdateCountdown(0.0f);
                Assert(vfx.HasDetonated, "HasDetonated should be true after reaching 0s");
                Assert(!vfx.IsVentingSmoke, "IsVentingSmoke should become false upon detonation");

                // Reset
                vfx.ResetVfx();
                Assert(!vfx.IsVentingSmoke, "IsVentingSmoke reset to false");
                Assert(!vfx.HasDetonated, "HasDetonated reset to false");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Test_AudioPlayer_EndingHelperMethodsSafeExecution()
        {
            var go = new GameObject("Test_Endings_AudioPlayer");
            try
            {
                var player = go.AddComponent<FacilityAudioPlayer>();
                // Safe execution without active scene audio listener / output device
                player.PlayCatastrophicExplosion(Vector3.zero);
                player.PlayPlayerDeathVitals();
                player.PlayBlastDoorLock(Vector3.zero);
                Assert(true, "Audio player ending helpers executed safely");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
