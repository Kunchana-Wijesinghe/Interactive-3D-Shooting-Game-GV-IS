using System.IO;
using FacilityBreach.AI.SoundHunter;
using Perera.Core;
using Perera.SoundHunter;
using UnityEditor;
using UnityEngine;

namespace FacilityBreach.Editor
{
    public static class SoundHunterValidation
    {
        [MenuItem("Facility Breach/Validate Sound Hunter IS Phase 1")]
        public static void ValidateAll()
        {
            int passed = 0, total = 0;
            void Assert(bool condition, string message)
            {
                total++;
                if (!condition) Debug.LogError("[FAIL] " + message);
                else passed++;
            }

            // 1. Protected folder check
            Assert(Directory.Exists("Assets/Perera"), "Protected Assets/Perera folder exists");

            // 2. Hearing Graph Dijkstra test
            var graph = new HearingGraph();
            int n0 = graph.AddNode(new Vector3(0, 0, 0));
            int n1 = graph.AddNode(new Vector3(10, 0, 0));
            int n2 = graph.AddNode(new Vector3(20, 0, 0));
            graph.AddEdge(n0, n1, 5.0f);
            graph.AddEdge(n1, n2, 10.0f);

            float cost0To2 = graph.MinCost(n0, n2);
            Assert(Mathf.Approximately(cost0To2, 15.0f), $"Graph MinCost n0->n2 should be 15, got {cost0To2}");

            // 3. Sound Ranker & Sound Memory test
            var ranker = new SoundRanker();
            var memory = new SoundMemory(ranker, 10.0f, 4);

            var gunshotStim = new SoundStimulus(new Vector3(5, 0, 5), 40, SoundCategory.Gunshot, 101);
            var canStim = new SoundStimulus(new Vector3(2, 0, 2), 20, SoundCategory.DistractionCan, 102);

            var heardGunshot = new HeardSound(gunshotStim, 10, 30);
            var heardCan = new HeardSound(canStim, 5, 15);

            memory.Remember(heardCan, 1.0f);
            memory.Remember(heardGunshot, 1.0f);

            Assert(memory.TryGetBest(1.0f, out var best), "Memory returns best entry");
            Assert(best.Sound.Stimulus.Category == SoundCategory.Gunshot, "Gunshot ranked higher than distraction can");

            // Test 10s memory expiry
            Assert(memory.TryGetBest(11.1f, out _), "Memory expires after 10 seconds");

            // 4. Test Sound Hunter agent instancing in scene
            var agentObj = new GameObject("FB_SH01_TestAgent");
            var brain = agentObj.AddComponent<FacilityBreachSoundHunterBrain>();
            Assert(brain.CurrentState == SoundHunterState.Patrol, "Initial Sound Hunter state is Patrol");

            Object.DestroyImmediate(agentObj);

            Debug.Log($"[Facility Breach IS Phase 1] Validation Complete. {passed}/{total} checks passed.");
        }
    }
}
