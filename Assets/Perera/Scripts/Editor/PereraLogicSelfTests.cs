using System;
using System.Collections.Generic;
using Perera.Combat;
using Perera.Core;
using Perera.SoundHunter;
using UnityEditor;
using UnityEngine;

namespace Perera.EditorTools
{
    /// <summary>
    /// Dependency-free self-tests for pure logic (the Unity Test Framework package is not
    /// in the manifest). Run via menu or: Unity -batchmode -executeMethod Perera.EditorTools.PereraLogicSelfTests.RunBatch
    /// </summary>
    public static class PereraLogicSelfTests
    {
        private static int failures, checks;

        [MenuItem("Perera/Run Logic Self-Tests")]
        public static void Run() { RunAll(); }

        public static void RunBatch() { RunAll(); EditorApplication.Exit(failures == 0 ? 0 : 1); }

        private static void RunAll()
        {
            failures = checks = 0;
            AmmoTests();
            HealthTests();
            HearingGraphTests();
            HearingBoundaryTests();
            RankingTests();
            MemoryTests();
            MemoryRevisionTests();
            SceneGraphTests();
            Debug.Log($"[Perera.SelfTests] {checks - failures}/{checks} checks passed");
        }

        private static void Check(bool ok, string name)
        {
            checks++;
            if (!ok) { failures++; Debug.LogError("[Perera.SelfTests] FAIL: " + name); }
        }

        private static void AmmoTests()
        {
            var a = new AmmoMagazine(10, 25);
            Check(a.InMagazine == 10 && a.Reserve == 25, "ammo starts full");
            for (int i = 0; i < 10; i++) a.TryConsume();
            Check(!a.CanFire && !a.TryConsume(), "empty magazine cannot fire");
            Check(a.Reload() == 10 && a.Reserve == 15 && a.InMagazine == 10, "full reload moves 10");
            a.TryConsume(); a.TryConsume(); a.TryConsume();
            a.Reload();
            Check(a.InMagazine == 10 && a.Reserve == 12, "partial reload tops up, keeps remainder");
            var b = new AmmoMagazine(10, 4);
            for (int i = 0; i < 10; i++) b.TryConsume();
            Check(b.Reload() == 4 && b.InMagazine == 4 && b.Reserve == 0, "reload limited by reserve");
            Check(!b.CanReload || b.InMagazine == 4 && b.Reserve == 0 && !b.CanReload, "no reload without reserve");
            var c = new AmmoMagazine(5, 10);
            Check(!c.CanReload, "no reload on full magazine");
        }

        private static void HealthTests()
        {
            var go = new GameObject("HealthTest");
            var h = go.AddComponent<Health>();
            h.ResetHealth(); // Awake does not run outside Play Mode
            int deaths = 0, hits = 0;
            h.Died += _ => deaths++;
            h.Damaged += (_, __) => hits++;
            var info = new DamageInfo(60f, Vector3.zero, Vector3.forward, null);
            Check(h.TakeDamage(info) && !h.IsDead, "first hit applies, alive");
            Check(h.TakeDamage(info) && h.IsDead && deaths == 1, "lethal hit kills once");
            Check(!h.TakeDamage(info) && deaths == 1 && hits == 2, "damage after death ignored");
            h.ResetHealth();
            Check(!h.IsDead && h.Current == h.Max, "reset restores health");
            UnityEngine.Object.DestroyImmediate(go);
        }

        // ---- Milestone 2: Sound Hunter core ----
        // Graph: 0-1 (2), 1-2 (2), 0-2 (10) [expensive direct], node 3 isolated.
        private static HearingGraph MakeGraph()
        {
            var g = new HearingGraph();
            for (int i = 0; i < 4; i++) g.AddNode(new Vector3(i * 10f, 0f, 0f));
            g.AddEdge(0, 1, 2f); g.AddEdge(1, 2, 2f); g.AddEdge(0, 2, 10f);
            return g;
        }

        private static SoundStimulus Snd(Vector3 pos, float strength, SoundCategory cat = SoundCategory.Gunshot, int id = 1, float t = 0f)
            => new SoundStimulus(pos, strength, cat, id, t);

        private static HeardSound Heard(SoundCategory cat, float remaining, float t, int id, Vector3 p)
            => new HeardSound(Snd(p, remaining, cat, id, t), 0f, remaining);

        private static void HearingGraphTests()
        {
            var g = MakeGraph();
            var c = g.MinCostsFrom(0);
            Check(c[0] == 0f && c[1] == 2f, "dijkstra source and neighbour");
            Check(c[2] == 4f, "dijkstra prefers cheaper 2-hop route over 10 direct");
            Check(float.IsInfinity(c[3]), "disconnected node unreachable");
            Check(g.MinCost(2, 0) == 4f, "propagation cost symmetric");
            Check(g.NearestNode(new Vector3(21f, 0f, 0f)) == 2, "nearest node lookup");
            bool threw = false;
            try { g.AddEdge(0, 1, -1f); } catch (ArgumentOutOfRangeException) { threw = true; }
            Check(threw, "negative edge cost rejected");
        }

        private static void HearingBoundaryTests()
        {
            var g = MakeGraph();
            var ev = new HearingEvaluator(g);
            HeardSound h;
            Check(ev.TryHear(Snd(g.GetPosition(0), 4f), 2, out h) && h.Cost == 4f && h.Remaining == 0f, "cost == budget is heard (inclusive)");
            Check(!ev.TryHear(Snd(g.GetPosition(0), 3.99f), 2, out _), "cost just above budget not heard");
            Check(!ev.TryHear(Snd(g.GetPosition(0), 1000f), 3, out _), "disconnected listener never hears");
            Check(ev.TryHear(Snd(g.GetPosition(1), 0f), 1, out h) && h.Remaining == 0f, "same-node zero-strength sound heard");
            ev.HearingMultiplier = 0.5f;
            Check(!ev.TryHear(Snd(g.GetPosition(0), 6f), 2, out _), "multiplier shrinks budget below cost");
            ev.HearingMultiplier = 1f;
            Check(ev.TryHear(Snd(g.GetPosition(0), 6f), 2, out h) && h.Remaining == 2f, "remaining budget reported");
        }

        private static void RankingTests()
        {
            var r = new SoundRanker();
            var gun = Heard(SoundCategory.Gunshot, 1f, 0f, 5, Vector3.zero);
            var step = Heard(SoundCategory.Footstep, 9f, 0f, 5, Vector3.zero);
            Check(r.Compare(gun, step) < 0, "category priority beats remaining budget");
            var loud = Heard(SoundCategory.Footstep, 5f, 0f, 5, Vector3.zero);
            var quiet = Heard(SoundCategory.Footstep, 2f, 0f, 5, Vector3.zero);
            Check(r.Compare(loud, quiet) < 0, "higher remaining wins at equal priority");
            var newer = Heard(SoundCategory.Footstep, 5f, 3f, 5, Vector3.zero);
            Check(r.Compare(newer, loud) < 0, "newer wins when remaining ties");
            var lowId = Heard(SoundCategory.Footstep, 5f, 3f, 1, Vector3.zero);
            Check(r.Compare(lowId, newer) < 0, "lower SourceId wins next");
            var posA = Heard(SoundCategory.Footstep, 5f, 3f, 1, new Vector3(1f, 0f, 0f));
            var posB = Heard(SoundCategory.Footstep, 5f, 3f, 1, new Vector3(2f, 0f, 0f));
            Check(r.Compare(posA, posB) < 0 && r.Compare(posB, posA) > 0 && r.Compare(posA, posA) == 0, "position is final deterministic tie-break");
            var list = new List<HeardSound> { quiet, step, gun };
            var rev = new List<HeardSound> { gun, step, quiet };
            Check(list[r.IndexOfBest(list)].Stimulus.Category == SoundCategory.Gunshot
                && rev[r.IndexOfBest(rev)].Stimulus.Category == SoundCategory.Gunshot, "best pick independent of input order");
            r.SetPriority(SoundCategory.Footstep, 10);
            Check(r.Compare(step, gun) < 0, "priority table configurable");
        }

        private static void MemoryTests()
        {
            var mem = new SoundMemory(new SoundRanker(), expirySeconds: 5f, capacity: 2);
            SoundMemory.Entry e;
            Check(!mem.TryGetBest(0f, out _), "empty memory has no best");
            mem.Remember(Heard(SoundCategory.Footstep, 5f, 0f, 1, new Vector3(1f, 0f, 0f)), 0f);
            Check(mem.TryGetBest(1f, out e) && e.Position.x == 1f, "remembers heard position");
            mem.Remember(Heard(SoundCategory.Gunshot, 5f, 1f, 2, new Vector3(2f, 0f, 0f)), 1f);
            Check(mem.TryGetBest(1f, out e) && e.Position.x == 2f, "higher-priority sound replaces footstep as best");
            mem.Remember(Heard(SoundCategory.Footstep, 5f, 2f, 1, new Vector3(3f, 0f, 0f)), 2f);
            Check(mem.Count == 2 && mem.TryGetBest(2f, out e) && e.Position.x == 2f, "same source refreshes entry, no duplicate");
            mem.Remember(Heard(SoundCategory.Other, 1f, 2f, 9, new Vector3(9f, 0f, 0f)), 2f);
            Check(mem.Count == 2 && mem.TryGetBest(2f, out e) && e.Position.x == 2f, "capacity drops lowest-ranked entry");
            Check(mem.TryGetBest(5.99f, out e) && e.Position.x == 2f, "gunshot alive just before expiry");
            Check(mem.TryGetBest(6f, out e) && e.Position.x == 3f, "gunshot expired at boundary; refreshed footstep remains");
            Check(!mem.TryGetBest(7f, out _) && mem.Count == 0, "all expired after window");
            mem.Remember(Heard(SoundCategory.Gunshot, 5f, 10f, 1, Vector3.zero), 10f);
            Check(mem.TryGetBest(14.99f, out _), "alive just before expiry");
            Check(!mem.TryGetBest(15f, out _), "expires exactly at boundary");
            mem.ExpirySeconds = 100f;
            mem.Remember(Heard(SoundCategory.Gunshot, 5f, 20f, 1, Vector3.zero), 20f);
            Check(mem.TryGetBest(100f, out e), "expiry configurable");
            Check(mem.Forget(e) && mem.Count == 0, "investigated memory forgotten");
        }

        // ---- Milestone 3 ----
        private static void MemoryRevisionTests()
        {
            var mem = new SoundMemory(new SoundRanker(), 10f, 4);
            SoundMemory.Entry first, second;
            Check(mem.Remember(Heard(SoundCategory.Thrown, 5f, 0f, 7, new Vector3(1f, 0f, 0f)), 0f, out first), "remember returns entry");
            Check(mem.Remember(Heard(SoundCategory.Thrown, 5f, 1f, 7, new Vector3(2f, 0f, 0f)), 1f, out second) && second.Revision > first.Revision, "refresh assigns a newer revision");
            Check(!mem.Forget(first) && mem.Count == 1, "forgetting the OLD revision cannot delete the refreshed entry");
            SoundMemory.Entry latest;
            Check(mem.TryGetLatestFor(first, out latest) && latest.Revision == second.Revision && latest.Position.x == 2f, "latest-for-source finds the refresh");
            Check(mem.Forget(second) && mem.Count == 0, "forgetting the current revision removes it");
            Check(!mem.Remember(Heard(SoundCategory.Thrown, float.NaN, 0f, 8, Vector3.zero), 0f), "NaN remaining rejected");
            Check(!mem.Remember(Heard(SoundCategory.Thrown, 5f, 0f, 8, new Vector3(float.NaN, 0f, 0f)), 0f), "NaN position rejected");
            Check(!mem.Remember(Heard(SoundCategory.Thrown, 5f, 0f, 8, Vector3.zero), float.PositiveInfinity), "non-finite time rejected");
            mem.ExpirySeconds = -3f; mem.ExpirySeconds = float.NaN;
            Check(mem.ExpirySeconds == 10f, "invalid expiry ignored");
            var g = MakeGraph();
            var ev = new HearingEvaluator(g);
            Check(!ev.TryHear(Snd(g.GetPosition(0), float.PositiveInfinity), 0, 1, out _), "infinite strength not heard");
            Check(!ev.TryHear(Snd(g.GetPosition(0), float.NaN), 0, 1, out _), "NaN strength not heard");
            Check(!ev.TryHear(Snd(g.GetPosition(0), 5f), 0, 99, out _), "unknown listener node not heard");
        }

        private static HearingNode MakeNode(GameObject parent, string name, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name); go.transform.SetParent(parent.transform, false); go.transform.position = pos;
            var n = go.AddComponent<HearingNode>();
            var so = new SerializedObject(n); so.FindProperty("zoneSize").vector3Value = size; so.ApplyModifiedPropertiesWithoutUndo();
            return n;
        }

        private static HearingLink MakeLink(GameObject parent, string name, HearingNode a, HearingNode b, float cost)
        {
            var go = new GameObject(name); go.transform.SetParent(parent.transform, false);
            var l = go.AddComponent<HearingLink>(); l.NodeA = a; l.NodeB = b; l.Cost = cost; return l;
        }

        private static void SceneGraphTests()
        {
            var root = new GameObject("SceneGraphTest");
            var sg = root.AddComponent<SceneHearingGraph>();
            var so = new SerializedObject(sg); so.FindProperty("maxMappingDistance").floatValue = 8f; so.FindProperty("logProblems").boolValue = false; so.ApplyModifiedPropertiesWithoutUndo();
            // Two rooms separated by a wall at x=5: zones end at the wall.
            var left = MakeNode(root, "L", new Vector3(0, 0, 0), new Vector3(10, 4, 10));
            var right = MakeNode(root, "R", new Vector3(10, 0, 0), new Vector3(10, 4, 10));
            var tiny = MakeNode(root, "Platform", new Vector3(0, 3, 0), new Vector3(2, 2, 2));
            var lonely = MakeNode(root, "Lonely", new Vector3(50, 0, 0), new Vector3(4, 4, 4));
            var good = MakeLink(root, "good", left, right, 10f);
            MakeLink(root, "dup", right, left, 3f);                       // duplicate (reversed)
            MakeLink(root, "missing", left, null, 3f);                    // missing reference
            MakeLink(root, "self", left, left, 3f);                       // self link
            MakeLink(root, "nan", left, tiny, float.NaN);                 // invalid cost
            MakeLink(root, "neg", left, tiny, -1f);                       // invalid cost
            sg.Rebuild();
            Check(sg.Problems.Count == 5, "validation finds duplicate, missing, self, NaN and negative links (" + sg.Problems.Count + ")");
            Check(sg.Graph.NodeCount == 4, "all nodes added");
            Check(sg.Graph.MinCost(0, 1) == 10f, "duplicate link did not overwrite the first cost");
            Check(float.IsInfinity(sg.Graph.MinCost(0, 2)), "invalid-cost links are not in the graph");

            int n;
            Check(sg.TryMapPoint(new Vector3(4.9f, 0, 0), out n) && n == 0, "point inside left room maps to left node");
            Check(sg.TryMapPoint(new Vector3(5.1f, 0, 0), out n) && n == 1, "point just past the wall maps to the right room, not the nearer-in-line left node");
            Check(sg.TryMapPoint(new Vector3(0, 3, 0), out n) && n == 2, "smaller nested zone wins");
            Check(!sg.TryMapPoint(new Vector3(30, 0, 0), out n), "point in no zone rejected");
            Check(!sg.TryMapPoint(new Vector3(float.NaN, 0, 0), out n), "non-finite point rejected");
            sg.MaxMappingDistance = 4f;
            Check(!sg.TryMapPoint(new Vector3(-4.9f, 0, 4.9f), out n), "inside zone but beyond max mapping distance rejected");
            sg.MaxMappingDistance = 8f;

            good.Open = false; sg.Rebuild();
            Check(float.IsInfinity(sg.Graph.MinCost(0, 1)), "closed link removes the connection after rebuild");
            Check(sg.Problems.Count == 5, "closed link is not reported as a problem");
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
