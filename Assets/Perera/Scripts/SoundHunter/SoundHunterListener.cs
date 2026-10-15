using System;
using Perera.Core;
using UnityEngine;

namespace Perera.SoundHunter
{
    /// <summary>
    /// Hearing sense of the Sound Hunter. Self-contained: subscribes to the global SoundEventBus
    /// while enabled, maps sound + own position onto the scene hearing graph, evaluates audibility
    /// with HearingEvaluator, and stores eligible sounds in SoundMemory.
    ///
    /// CLOCK: one gameplay clock, Clock() (default Time.time), is used for every memory call
    /// (Remember, TryGetBest, Prune). Stimulus CreatedTime is only validated and used for ranking;
    /// expiry counts from the moment the sound was HEARD.
    /// Memory is pruned every Update so entries expire even when no new sound arrives.
    /// </summary>
    public class SoundHunterListener : MonoBehaviour
    {
        /// <summary>Live enabled listeners (leak/duplicate check for restarts).</summary>
        public static int ActiveInstances { get; private set; }

        [SerializeField] private SceneHearingGraph hearingGraph;
        [SerializeField] private float memorySeconds = 12f;
        [SerializeField] private int memoryCapacity = 4;
        [SerializeField] private float hearingMultiplier = 1f;
        [Tooltip("How often the hunter's own graph node is re-resolved (s). Bounded, not per frame.")]
        [SerializeField] private float listenerNodeInterval = 0.2f;

        public float MemorySeconds => memorySeconds;
        public SoundRanker Ranker { get; private set; }
        public SoundMemory Memory { get; private set; }
        public Func<float> Clock { get; set; } = () => Time.time;
        public float Now => Clock();

        public int ListenerNode { get; private set; } = -1;
        public bool Listening { get; private set; }
        public SceneHearingGraph Graph { get => hearingGraph; set { hearingGraph = value; evaluator = null; } }

        // Debug counters
        public int HeardCount { get; private set; }
        public int InaudibleCount { get; private set; }
        public int RejectedCount { get; private set; }
        public HeardSound LastHeard { get; private set; }
        public bool HasLastHeard { get; private set; }

        public event Action<SoundMemory.Entry> SoundRemembered;

        private HearingEvaluator evaluator;
        private float nextNodeTime;
        private bool subscribed;

        private void Awake() => EnsureMemory();

        private void EnsureMemory()
        {
            if (Memory != null) return;
            Ranker = new SoundRanker();
            Memory = new SoundMemory(Ranker, memorySeconds, memoryCapacity);
        }

        private void OnEnable()
        {
            EnsureMemory();
            Memory.Clear();              // restart-safe: no stale memory survives re-enable
            ResetCounters();
            nextNodeTime = 0f;
            ListenerNode = -1;
            SetListening(true);
        }

        private void OnDisable() { SetListening(false); Memory?.Clear(); }

        // Covers destroy as well (OnDisable is called before OnDestroy).

        /// <summary>Start/stop reacting to sounds. Stopping also clears memory.</summary>
        public void SetListening(bool on)
        {
            if (on && !subscribed)
            {
                SoundEventBus.Emitted -= OnSound; // never double-subscribe
                SoundEventBus.Emitted += OnSound;
                subscribed = true; Listening = true; ActiveInstances++;
            }
            else if (!on && subscribed)
            {
                SoundEventBus.Emitted -= OnSound;
                subscribed = false; Listening = false; ActiveInstances = Math.Max(0, ActiveInstances - 1);
                Memory?.Clear();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ActiveInstances = 0;

        private void ResetCounters() { HeardCount = InaudibleCount = RejectedCount = 0; HasLastHeard = false; }

        private void Update()
        {
            if (!Listening) return;
            float now = Now;
            Memory.ExpirySeconds = memorySeconds;
            Memory.Prune(now);            // expiry independent of incoming sounds
            RefreshListenerNode(now);
        }

        private void RefreshListenerNode(float now)
        {
            if (now < nextNodeTime || hearingGraph == null) return;
            nextNodeTime = now + Mathf.Max(0.05f, listenerNodeInterval);
            ListenerNode = hearingGraph.TryMapPoint(transform.position, out int n) ? n : -1;
        }

        private void OnSound(SoundStimulus s)
        {
            if (!Listening || hearingGraph == null || hearingGraph.Graph == null) return;

            // Validate before any maths.
            if (!float.IsFinite(s.Strength) || s.Strength < 0f || !float.IsFinite(s.CreatedTime)
                || !float.IsFinite(s.Position.x) || !float.IsFinite(s.Position.y) || !float.IsFinite(s.Position.z))
            { RejectedCount++; return; }

            float now = Now;
            if (!float.IsFinite(now)) { RejectedCount++; return; }

            RefreshListenerNode(now);
            if (ListenerNode < 0) { RejectedCount++; return; }                       // hunter not inside any zone
            if (!hearingGraph.TryMapPoint(s.Position, out int source)) { RejectedCount++; return; } // source in no room

            evaluator ??= new HearingEvaluator(hearingGraph.Graph);
            evaluator.HearingMultiplier = hearingMultiplier;
            if (!evaluator.TryHear(s, source, ListenerNode, out HeardSound heard)) { InaudibleCount++; return; }

            if (!Memory.Remember(heard, now, out SoundMemory.Entry stored)) { RejectedCount++; return; }
            HeardCount++; LastHeard = heard; HasLastHeard = true;
            SoundRemembered?.Invoke(stored);
        }

        /// <summary>Call after SceneHearingGraph.Rebuild() so the evaluator uses the new graph.</summary>
        public void InvalidateGraph() => evaluator = null;
    }
}
