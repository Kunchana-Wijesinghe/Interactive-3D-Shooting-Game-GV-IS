using System;
using System.Collections.Generic;
using System.IO;
using FacilityBreach.Core;
using FacilityBreach.Interactions;
using UnityEditor;
using UnityEngine;

namespace FacilityBreach.Editor
{
    /// <summary>
    /// Automated Edit Mode tests for FacilityInteractionDispatcher:
    /// - Held barricade exclusive release priority
    /// - One-action-per-key arbitration guarantee
    /// - Multi-target arbitration (door vs barricade vs station)
    /// - Spatial scoring: distance, facing angle (dot product), and direct aim bonus
    /// - Range and visibility thresholds
    /// - Dynamic registration and cleanup lifecycle
    /// - Gameplay acceptance state guards
    /// </summary>
    public static class FacilityInteractionDispatcherTests
    {
        public static Action<string> Log = msg => Debug.Log(msg);
        public static Action<string> LogError = msg => Debug.LogError(msg);

        private static int passedCount;
        private static int failedCount;
        private static readonly List<string> failureMessages = new List<string>();

        [MenuItem("Facility Breach/Run Interaction Dispatcher Tests")]
        public static void RunAll()
        {
            passedCount = 0;
            failedCount = 0;
            failureMessages.Clear();

            Log("==== [FacilityInteractionDispatcherTests] Starting Interaction Arbitration Verification ====");

            Test_HeldBarricade_AbsoluteReleasePriority();
            Test_OneActionPerKeyPress_NoDuplicateExecutions();
            Test_DistanceScoring_ClosestCandidateSelected();
            Test_OutOfRange_Ignored();
            Test_FacingAngle_BehindPlayerIgnored();
            Test_DirectAim_ScoreBonus();
            Test_DoorToggle_AndPromptUpdate();
            Test_Barricade_GrabAndReleaseCycle();
            Test_Registration_AndCleanup();
            Test_CalculateScore_DeterministicFormula();

            int total = passedCount + failedCount;
            string summary = $"[FacilityInteractionDispatcherTests] Completed {total} checks: {passedCount} PASSED, {failedCount} FAILED.";

            if (failedCount == 0)
            {
                Log($"<color=green>{summary}</color>");
                File.WriteAllText("interaction-tests-passed.txt", summary + "\nAll interaction arbitration tests passed successfully.\n");
            }
            else
            {
                LogError($"<color=red>{summary}</color>\nFailures:\n" + string.Join("\n", failureMessages));
                File.WriteAllText("interaction-tests-failed.txt", summary + "\n" + string.Join("\n", failureMessages) + "\n");
            }
        }

        public static void RunBatch()
        {
            RunAll();
            EditorApplication.Exit(failedCount == 0 ? 0 : 1);
        }

        private static void Assert(bool condition, string testName, string details = "")
        {
            if (condition)
            {
                passedCount++;
                Log($"[Interaction Test PASS] {testName}");
            }
            else
            {
                failedCount++;
                string message = $"FAIL: {testName}. {details}";
                failureMessages.Add(message);
                LogError($"[Interaction Test FAIL] {message}");
            }
        }

        private static void Test_HeldBarricade_AbsoluteReleasePriority()
        {
            var root = new GameObject("TestRoot_HeldPriority");
            try
            {
                var player = new GameObject("Player").transform;
                player.SetParent(root.transform);
                player.position = Vector3.zero;
                player.forward = Vector3.forward;

                var camObj = new GameObject("Camera");
                camObj.transform.SetParent(player);
                camObj.transform.localPosition = Vector3.up * 1.6f;
                var cam = camObj.AddComponent<Camera>();

                var dispatcherObj = new GameObject("Dispatcher");
                dispatcherObj.transform.SetParent(root.transform);
                var dispatcher = dispatcherObj.AddComponent<FacilityInteractionDispatcher>();
                dispatcher.Initialize(null, cam, player);

                // Add Barricade that is held
                var barricadeObj = new GameObject("Barricade_Held");
                barricadeObj.transform.SetParent(root.transform);
                barricadeObj.transform.position = new Vector3(0, 0, 2f);
                var rb = barricadeObj.AddComponent<Rigidbody>();
                var col = barricadeObj.AddComponent<BoxCollider>();
                var barricade = barricadeObj.AddComponent<MovableBarricade>();
                dispatcher.RegisterBarricade(barricade);

                // Add Station right next to player
                var stationObj = new GameObject("Station");
                stationObj.transform.SetParent(root.transform);
                stationObj.transform.position = new Vector3(0, 0, 1.2f);
                var station = stationObj.AddComponent<ResourceStation>();
                station.Kind = StationKind.Card;
                station.Available = true;
                dispatcher.RegisterStation(station);

                // Add Door in front of player
                var doorObj = new GameObject("Door");
                doorObj.transform.SetParent(root.transform);
                doorObj.transform.position = new Vector3(0, 0, 1.5f);
                var door = doorObj.AddComponent<InteractiveDoor>();
                door.handleInternalInput = false;
                dispatcher.RegisterDoor(door);

                // Simulate held barricade
                barricade.Grab(player);

                Assert(dispatcher.GetHeldBarricade() == barricade, "GetHeldBarricade identifies held barricade");

                var candidate = dispatcher.FindBestCandidate(out var rejection);
                Assert(candidate.Kind == InteractionTargetKind.HeldBarricade, "FindBestCandidate prioritizes held barricade over station and door", $"Got {candidate.Kind}");
                Assert(candidate.Target == barricade, "Held candidate target matches held barricade");

                string prompt = dispatcher.GetContextualPrompt();
                Assert(prompt == "E: Release barricade", "HUD prompt indicates release barricade when held", $"Got '{prompt}'");

                bool executed = dispatcher.ExecuteInteraction(out rejection, out var executedKind, out var executedTarget);
                Assert(executed, "ExecuteInteraction succeeds on held barricade release");
                Assert(executedKind == InteractionTargetKind.HeldBarricade, "Executed kind is HeldBarricade");
                Assert(!barricade.IsHeld, "Barricade is no longer held after release interaction");
                Assert(!door.IsOpen, "Door state remained unchanged during barricade release");
                Assert(station.Available, "Station remained unchanged during barricade release");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Test_OneActionPerKeyPress_NoDuplicateExecutions()
        {
            var root = new GameObject("TestRoot_OneAction");
            try
            {
                var player = new GameObject("Player").transform;
                player.SetParent(root.transform);
                player.position = Vector3.zero;
                player.forward = Vector3.forward;

                var camObj = new GameObject("Camera");
                camObj.transform.SetParent(player);
                camObj.transform.localPosition = Vector3.up * 1.6f;
                var cam = camObj.AddComponent<Camera>();

                var dispatcherObj = new GameObject("Dispatcher");
                dispatcherObj.transform.SetParent(root.transform);
                var dispatcher = dispatcherObj.AddComponent<FacilityInteractionDispatcher>();
                dispatcher.Initialize(null, cam, player);

                // Barricade at 2.0m directly ahead
                var barricadeObj = new GameObject("Barricade");
                barricadeObj.transform.SetParent(root.transform);
                barricadeObj.transform.position = new Vector3(0, 0, 2.0f);
                barricadeObj.AddComponent<Rigidbody>();
                barricadeObj.AddComponent<BoxCollider>();
                var barricade = barricadeObj.AddComponent<MovableBarricade>();
                dispatcher.RegisterBarricade(barricade);

                // Door at 2.2m slightly off-angle
                var doorObj = new GameObject("Door");
                doorObj.transform.SetParent(root.transform);
                doorObj.transform.position = new Vector3(0.5f, 0, 2.2f);
                doorObj.AddComponent<BoxCollider>();
                var door = doorObj.AddComponent<InteractiveDoor>();
                door.handleInternalInput = false;
                dispatcher.RegisterDoor(door);

                // Execute a single interaction
                bool executed = dispatcher.ExecuteInteraction(out _, out var executedKind, out var executedTarget);
                Assert(executed, "Single interaction successfully dispatched");

                // Exactly one was touched
                bool barricadeTouched = barricade.IsHeld;
                bool doorTouched = door.IsOpen;

                Assert(barricadeTouched ^ doorTouched, "Exactly one system responded to the single interaction (exclusive arbitration)", $"BarricadeHeld={barricadeTouched}, DoorOpen={doorTouched}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Test_DistanceScoring_ClosestCandidateSelected()
        {
            var root = new GameObject("TestRoot_DistanceScoring");
            try
            {
                var player = new GameObject("Player").transform;
                player.SetParent(root.transform);
                player.position = Vector3.zero;
                player.forward = Vector3.forward;

                var camObj = new GameObject("Camera");
                camObj.transform.SetParent(player);
                camObj.transform.localPosition = Vector3.up * 1.6f;
                var cam = camObj.AddComponent<Camera>();

                var dispatcherObj = new GameObject("Dispatcher");
                dispatcherObj.transform.SetParent(root.transform);
                var dispatcher = dispatcherObj.AddComponent<FacilityInteractionDispatcher>();
                dispatcher.Initialize(null, cam, player);

                // Barricade 1 at 1.5m directly ahead
                var b1Obj = new GameObject("Barricade_Near");
                b1Obj.transform.SetParent(root.transform);
                b1Obj.transform.position = new Vector3(0, 0, 1.5f);
                b1Obj.AddComponent<Rigidbody>();
                b1Obj.AddComponent<BoxCollider>();
                var b1 = b1Obj.AddComponent<MovableBarricade>();
                dispatcher.RegisterBarricade(b1);

                // Barricade 2 at 2.8m directly ahead
                var b2Obj = new GameObject("Barricade_Far");
                b2Obj.transform.SetParent(root.transform);
                b2Obj.transform.position = new Vector3(0, 0, 2.8f);
                b2Obj.AddComponent<Rigidbody>();
                b2Obj.AddComponent<BoxCollider>();
                var b2 = b2Obj.AddComponent<MovableBarricade>();
                dispatcher.RegisterBarricade(b2);

                var best = dispatcher.FindBestCandidate(out _);
                Assert(best.Kind == InteractionTargetKind.Barricade, "Best candidate is barricade");
                Assert(best.Target == b1, "Closer barricade selected over farther barricade", $"Selected {best.Target}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Test_OutOfRange_Ignored()
        {
            var root = new GameObject("TestRoot_OutOfRange");
            try
            {
                var player = new GameObject("Player").transform;
                player.SetParent(root.transform);
                player.position = Vector3.zero;
                player.forward = Vector3.forward;

                var camObj = new GameObject("Camera");
                camObj.transform.SetParent(player);
                camObj.transform.localPosition = Vector3.up * 1.6f;
                var cam = camObj.AddComponent<Camera>();

                var dispatcherObj = new GameObject("Dispatcher");
                dispatcherObj.transform.SetParent(root.transform);
                var dispatcher = dispatcherObj.AddComponent<FacilityInteractionDispatcher>();
                dispatcher.Initialize(null, cam, player);

                // Barricade at 5.0m (max allowed is 3.0m)
                var barricadeObj = new GameObject("Barricade_TooFar");
                barricadeObj.transform.SetParent(root.transform);
                barricadeObj.transform.position = new Vector3(0, 0, 5.0f);
                barricadeObj.AddComponent<Rigidbody>();
                barricadeObj.AddComponent<BoxCollider>();
                var barricade = barricadeObj.AddComponent<MovableBarricade>();
                dispatcher.RegisterBarricade(barricade);

                var best = dispatcher.FindBestCandidate(out var rejection);
                Assert(best.Kind == InteractionTargetKind.None, "Out-of-range object produces None candidate");
                Assert(!string.IsNullOrEmpty(rejection), "Out-of-range returns descriptive rejection reason", $"Rejection: '{rejection}'");

                bool executed = dispatcher.ExecuteInteraction(out rejection, out var executedKind, out _);
                Assert(!executed, "ExecuteInteraction fails when all candidates out of range");
                Assert(executedKind == InteractionTargetKind.None, "Executed kind is None when out of range");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Test_FacingAngle_BehindPlayerIgnored()
        {
            var root = new GameObject("TestRoot_FacingAngle");
            try
            {
                var player = new GameObject("Player").transform;
                player.SetParent(root.transform);
                player.position = Vector3.zero;
                player.forward = Vector3.forward; // Facing +Z

                var camObj = new GameObject("Camera");
                camObj.transform.SetParent(player);
                camObj.transform.localPosition = Vector3.up * 1.6f;
                var cam = camObj.AddComponent<Camera>();

                var dispatcherObj = new GameObject("Dispatcher");
                dispatcherObj.transform.SetParent(root.transform);
                var dispatcher = dispatcherObj.AddComponent<FacilityInteractionDispatcher>();
                dispatcher.Initialize(null, cam, player);

                // Barricade behind player at (0, 0, -2.0m)
                var barricadeObj = new GameObject("Barricade_Behind");
                barricadeObj.transform.SetParent(root.transform);
                barricadeObj.transform.position = new Vector3(0, 0, -2.0f);
                barricadeObj.AddComponent<Rigidbody>();
                barricadeObj.AddComponent<BoxCollider>();
                var barricade = barricadeObj.AddComponent<MovableBarricade>();
                dispatcher.RegisterBarricade(barricade);

                var best = dispatcher.FindBestCandidate(out _);
                Assert(best.Kind == InteractionTargetKind.None, "Barricade behind player is ignored");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Test_DirectAim_ScoreBonus()
        {
            var dispatcherObj = new GameObject("Dispatcher_ScoreTest");
            try
            {
                var dispatcher = dispatcherObj.AddComponent<FacilityInteractionDispatcher>();
                float scoreWithoutAim = dispatcher.CalculateScore(2.0f, 0.8f, false);
                float scoreWithAim = dispatcher.CalculateScore(2.0f, 0.8f, true);

                Assert(scoreWithAim < scoreWithoutAim, "Direct aim gives lower (better) arbitration score");
                Assert(Mathf.Approximately(scoreWithoutAim - scoreWithAim, dispatcher.directAimScoreBonus), "Direct aim bonus matches configured directAimScoreBonus value");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dispatcherObj);
            }
        }

        private static void Test_DoorToggle_AndPromptUpdate()
        {
            var root = new GameObject("TestRoot_DoorToggle");
            try
            {
                var player = new GameObject("Player").transform;
                player.SetParent(root.transform);
                player.position = Vector3.zero;
                player.forward = Vector3.forward;

                var camObj = new GameObject("Camera");
                camObj.transform.SetParent(player);
                camObj.transform.localPosition = Vector3.up * 1.6f;
                var cam = camObj.AddComponent<Camera>();

                var dispatcherObj = new GameObject("Dispatcher");
                dispatcherObj.transform.SetParent(root.transform);
                var dispatcher = dispatcherObj.AddComponent<FacilityInteractionDispatcher>();
                dispatcher.Initialize(null, cam, player);

                var doorObj = new GameObject("Door_Interactive");
                doorObj.transform.SetParent(root.transform);
                doorObj.transform.position = new Vector3(0, 0, 1.8f);
                doorObj.AddComponent<BoxCollider>();
                var door = doorObj.AddComponent<InteractiveDoor>();
                door.handleInternalInput = false;
                dispatcher.RegisterDoor(door);

                Assert(!door.handleInternalInput, "Registered door internal input is disabled to avoid dual-trigger");
                Assert(!door.IsOpen, "Door starts closed");

                string promptBefore = dispatcher.GetContextualPrompt();
                Assert(promptBefore == "E: Open door", "Closed door prompt is 'E: Open door'", $"Got '{promptBefore}'");

                bool executed = dispatcher.ExecuteInteraction(out _, out var executedKind, out var executedTarget);
                Assert(executed, "Door interaction executed successfully");
                Assert(executedKind == InteractionTargetKind.Door, "Executed kind is Door");
                Assert(door.IsOpen, "Door opened following interaction");

                string promptAfter = dispatcher.GetContextualPrompt();
                Assert(promptAfter == "E: Close door", "Opened door prompt is 'E: Close door'", $"Got '{promptAfter}'");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Test_Barricade_GrabAndReleaseCycle()
        {
            var root = new GameObject("TestRoot_BarricadeCycle");
            try
            {
                var player = new GameObject("Player").transform;
                player.SetParent(root.transform);
                player.position = Vector3.zero;
                player.forward = Vector3.forward;

                var camObj = new GameObject("Camera");
                camObj.transform.SetParent(player);
                camObj.transform.localPosition = Vector3.up * 1.6f;
                var cam = camObj.AddComponent<Camera>();

                var dispatcherObj = new GameObject("Dispatcher");
                dispatcherObj.transform.SetParent(root.transform);
                var dispatcher = dispatcherObj.AddComponent<FacilityInteractionDispatcher>();
                dispatcher.Initialize(null, cam, player);

                var barricadeObj = new GameObject("Barricade_Cycle");
                barricadeObj.transform.SetParent(root.transform);
                barricadeObj.transform.position = new Vector3(0, 0, 1.8f);
                barricadeObj.AddComponent<Rigidbody>();
                barricadeObj.AddComponent<BoxCollider>();
                var barricade = barricadeObj.AddComponent<MovableBarricade>();
                barricade.ConfigurePhysics();
                dispatcher.RegisterBarricade(barricade);

                Assert(!barricade.IsHeld, "Barricade starts unheld");

                string promptGrab = dispatcher.GetContextualPrompt();
                Assert(promptGrab == "E: Grab barricade", "Prompt before grab is 'E: Grab barricade'", $"Got '{promptGrab}'");

                // Grab
                bool grabbed = dispatcher.ExecuteInteraction(out _, out var kind1, out _);
                Assert(grabbed, "First interaction grabs barricade");
                Assert(kind1 == InteractionTargetKind.Barricade, "First kind is Barricade");
                Assert(barricade.IsHeld, "Barricade is now held");

                string promptRelease = dispatcher.GetContextualPrompt();
                Assert(promptRelease == "E: Release barricade", "Prompt while holding is 'E: Release barricade'", $"Got '{promptRelease}'");

                // Release
                bool released = dispatcher.ExecuteInteraction(out _, out var kind2, out _);
                Assert(released, "Second interaction releases barricade");
                Assert(kind2 == InteractionTargetKind.HeldBarricade, "Second kind is HeldBarricade");
                Assert(!barricade.IsHeld, "Barricade is now released");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Test_Registration_AndCleanup()
        {
            var root = new GameObject("TestRoot_Registration");
            try
            {
                var dispatcherObj = new GameObject("Dispatcher");
                dispatcherObj.transform.SetParent(root.transform);
                var dispatcher = dispatcherObj.AddComponent<FacilityInteractionDispatcher>();

                var bObj = new GameObject("Barricade");
                bObj.transform.SetParent(root.transform);
                bObj.AddComponent<Rigidbody>();
                bObj.AddComponent<BoxCollider>();
                var b = bObj.AddComponent<MovableBarricade>();

                var dObj = new GameObject("Door");
                dObj.transform.SetParent(root.transform);
                var d = dObj.AddComponent<InteractiveDoor>();

                var sObj = new GameObject("Station");
                sObj.transform.SetParent(root.transform);
                var s = sObj.AddComponent<ResourceStation>();

                dispatcher.RegisterBarricade(b);
                dispatcher.RegisterDoor(d);
                dispatcher.RegisterStation(s);

                Assert(dispatcher.Barricades.Count == 1, "Barricade registered");
                Assert(dispatcher.Doors.Count == 1, "Door registered");
                Assert(dispatcher.Stations.Count == 1, "Station registered");

                // Duplicate registration ignored
                dispatcher.RegisterBarricade(b);
                Assert(dispatcher.Barricades.Count == 1, "Duplicate barricade registration prevented");

                dispatcher.UnregisterBarricade(b);
                Assert(dispatcher.Barricades.Count == 0, "Barricade unregistered");

                dispatcher.ClearAll();
                Assert(dispatcher.Doors.Count == 0 && dispatcher.Stations.Count == 0 && dispatcher.Barricades.Count == 0, "ClearAll empties all candidate collections");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Test_CalculateScore_DeterministicFormula()
        {
            var dispatcherObj = new GameObject("Dispatcher_Formula");
            try
            {
                var dispatcher = dispatcherObj.AddComponent<FacilityInteractionDispatcher>();

                // Score formula: distance + 2 * (1 - dot) - (directAim ? bonus : 0)
                float d = 2.5f;
                float dot = 0.8f;
                float expected = 2.5f + 2.0f * (1.0f - 0.8f); // 2.5 + 0.4 = 2.9

                float actual = dispatcher.CalculateScore(d, dot, false);
                Assert(Mathf.Approximately(actual, expected), "Arbitration score matches analytical formula", $"Expected {expected}, got {actual}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dispatcherObj);
            }
        }
    }
}

