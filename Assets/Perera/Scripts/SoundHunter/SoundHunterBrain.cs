using System;
using System.Collections.Generic;
using Perera.Core;
using UnityEngine;

namespace Perera.SoundHunter
{
    public enum HunterState { Patrol, Investigate, Search, Combat, Dead }

    /// <summary>
    /// Sound Hunter decision logic. Talks to the body only through IHunterMovement, senses only
    /// through SoundHunterListener (hearing) and HunterVision (sight).
    ///
    /// TRANSITIONS
    ///  Patrol      -> Investigate : memory holds a sound (and the investigate cooldown is over).
    ///  Patrol/Investigate/Search -> Combat : HunterVision sees the target (range+FOV+line of sight).
    ///  Investigate -> Search      : movement Arrived at the exact remembered position,
    ///                               or movement Failed/unreachable (short in-place search, bounded fallback).
    ///  Search      -> Investigate : search finished and another valid memory exists
    ///                               (or a better/refreshed sound arrived mid-search).
    ///  Search      -> Patrol      : search finished and memory is empty.
    ///  Combat      -> Investigate : sight lost for LoseSightDelay: go to the LAST SEEN position
    ///                               (a position, not a live player lookup), then Search, then Patrol/next sound.
    ///  any         -> Dead        : Health.Died. Sensing, movement and attacks stop permanently.
    ///
    /// GOAL REPLACEMENT (while Investigate/Search, evaluated every ReplanInterval)
    ///  1. A different memory entry that ranks STRICTLY better (SoundRanker order) replaces the goal.
    ///  2. A newer revision of the SAME source (refresh) replaces the goal's position.
    ///  3. Equal or worse sounds never replace the current goal; they stay in memory.
    ///  4. If the current goal expired and nothing else exists the hunter returns to Patrol.
    ///
    /// CONSUMPTION: the investigated entry is forgotten (by Revision) when its Search finishes.
    /// A refresh that arrived meanwhile has a new revision and survives. A search interrupted by a
    /// better sound does not consume the old entry; it remains until it expires.
    ///
    /// BOUNDS: vision runs every SensorInterval; path requests only every ReplanInterval and at
    /// most one per FailedRetryCooldown after a failure.
    ///
    /// COMBAT/LOSS OF SIGHT: in Combat the hunter closes to CombatStopDistance of the last SEEN
    /// position and fires only with valid perception (fresh TryPerceive in range) and cooldown.
    /// Losing sight keeps LastSeenPosition (the last perceived value) and, after LoseSightDelay,
    /// switches to Investigate at that position.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(SoundHunterListener))]
    [RequireComponent(typeof(HunterVision))]
    public class SoundHunterBrain : MonoBehaviour
    {
        [Header("Patrol")]
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private float patrolPause = 1.5f;

        [Header("Bounded timing (s)")]
        [SerializeField] private float sensorInterval = 0.1f;
        [SerializeField] private float replanInterval = 0.5f;
        [SerializeField] private float failedRetryCooldown = 4f;

        [Header("Investigate / Search")]
        [SerializeField] private float arrivalDistance = 1.2f;
        [SerializeField] private float searchDuration = 3.5f;
        [SerializeField] private float searchSweepAngle = 70f;
        [SerializeField] private float searchSweepSpeed = 120f;

        [Header("Combat")]
        [SerializeField] private float attackRange = 14f;
        [SerializeField] private float combatStopDistance = 7f;
        [SerializeField] private float attackCooldown = 0.8f;
        [SerializeField] private float attackDamage = 6f;
        [Range(0f, 1f)] [SerializeField] private float hitChance = 0.6f;
        [SerializeField] private float loseSightDelay = 2.5f;
        [SerializeField] private float turnSpeed = 360f;

        [SerializeField] private bool logTransitions = true;

        private Health health;
        private SoundHunterListener listener;
        private HunterVision vision;
        private IHunterMovement move;

        public HunterState State { get; private set; } = HunterState.Patrol;
        public bool HasGoalEntry { get; private set; }
        public SoundMemory.Entry GoalEntry { get; private set; }
        public Vector3 GoalPosition { get; private set; }
        public bool GoalIsLastSeen { get; private set; }
        public Vector3 LastSeenPosition { get; private set; }
        public bool HasLastSeen { get; private set; }
        public int FailedMoves { get; private set; }
        public int PathRequests { get; private set; }
        /// <summary>Path requests issued for Investigate goals only (unreachable-goal spam check).</summary>
        public int InvestigateRequests { get; private set; }
        public int ShotsFired { get; private set; }
        public string LastTransition { get; private set; } = "";
        public IHunterMovement Movement => move;
        public SoundHunterListener Listener => listener;
        public HunterVision Vision => vision;
        public readonly List<string> History = new List<string>();
        public event Action<HunterState, HunterState> StateChanged;

        private bool canSee;
        private float lastSeenTime, nextSensor, nextReplan, nextRequestAllowed, investigateBlockedUntil;
        private bool moveActive;
        private float searchEnd, searchBaseYaw, searchStart, nextAttack, patrolNext;
        private int patrolIndex = -1;
        private bool patrolLegActive;
        private Quaternion startRotation;

        private void Awake()
        {
            health = GetComponent<Health>();
            listener = GetComponent<SoundHunterListener>();
            vision = GetComponent<HunterVision>();
            move = GetComponent<IHunterMovement>();
            startRotation = transform.rotation;
        }

        private void OnEnable()
        {
            health.Died += OnDied;
            if (move == null) Debug.LogError("[SoundHunterBrain] needs an IHunterMovement component on the same object", this);
            ResetBehaviour();
        }

        private void OnDisable() { if (health != null) health.Died -= OnDied; }

        /// <summary>Fresh behaviour state (restart-safe). Health is not touched.</summary>
        public void ResetBehaviour()
        {
            State = HunterState.Patrol;
            HasGoalEntry = false; GoalIsLastSeen = false; HasLastSeen = false;
            canSee = false; moveActive = false; patrolLegActive = false;
            patrolIndex = -1; nextSensor = nextReplan = nextRequestAllowed = investigateBlockedUntil = 0f;
            patrolNext = 0f; FailedMoves = PathRequests = InvestigateRequests = ShotsFired = 0;
            History.Clear();
            if (health.IsDead) EnterDead(); // enabled while already dead stays dead
        }

        private float Now => listener.Now;

        private void Update()
        {
            if (State == HunterState.Dead)
            {
                if (listener.Listening) listener.SetListening(false); // component enable order safety
                return;
            }
            float now = Now;

            if (now >= nextSensor)
            {
                nextSensor = now + sensorInterval;
                if (vision.TryPerceive(out Vector3 seen))
                {
                    canSee = true; LastSeenPosition = seen; HasLastSeen = true; lastSeenTime = now;
                }
                else canSee = false;
            }

            if (canSee && State != HunterState.Combat) { EnterCombat(); }

            switch (State)
            {
                case HunterState.Patrol: TickPatrol(now); break;
                case HunterState.Investigate: TickInvestigate(now); break;
                case HunterState.Search: TickSearch(now); break;
                case HunterState.Combat: TickCombat(now); break;
            }
        }

        // ---------------------------------------------------------------- Patrol
        private void EnterPatrol()
        {
            SetState(HunterState.Patrol);
            HasGoalEntry = false; GoalIsLastSeen = false; patrolLegActive = false; moveActive = false;
            move.Stop();
            patrolNext = Now;
        }

        private void TickPatrol(float now)
        {
            if (now >= investigateBlockedUntil && listener.Memory.TryGetBest(now, out var best))
            {
                BeginInvestigate(best, now);
                return;
            }
            if (patrolPoints == null || patrolPoints.Length == 0) return;

            if (patrolLegActive)
            {
                if (move.Status == MoveStatus.Arrived) { patrolLegActive = false; patrolNext = now + patrolPause; }
                else if (move.Status != MoveStatus.Moving) { patrolLegActive = false; patrolNext = now + failedRetryCooldown; }
            }
            else if (now >= patrolNext)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                var p = patrolPoints[patrolIndex];
                PathRequests++;
                patrolLegActive = p != null && move.RequestMove(p.position, arrivalDistance);
                if (!patrolLegActive) patrolNext = now + failedRetryCooldown; // skip unreachable waypoint, bounded
            }
        }

        // ---------------------------------------------------------------- Investigate
        private void BeginInvestigate(SoundMemory.Entry entry, float now)
        {
            SetState(HunterState.Investigate);
           
            GoalIsLastSeen = false;
            Retarget(entry, now);
        }

        private void Retarget(SoundMemory.Entry entry, float now)
        {
            HasGoalEntry = true; GoalEntry = entry; GoalPosition = entry.Position; // exact remembered position
            moveActive = false;
            nextRequestAllowed = Mathf.Min(nextRequestAllowed, now);
            nextReplan = now + replanInterval;
        }

        private void TickInvestigate(float now)
        {
            if (!GoalIsLastSeen && now >= nextReplan)
            {
                nextReplan = now + replanInterval;
                if (!ReconsiderGoal(now)) return;
            }

            if (!moveActive)
            {
                if (now < nextRequestAllowed) return;
                PathRequests++; InvestigateRequests++;
                moveActive = move.RequestMove(GoalPosition, arrivalDistance);
                if (!moveActive) OnGoalUnreachable(now);
                return;
            }

            switch (move.Status)
            {
                case MoveStatus.Arrived: moveActive = false; EnterSearch(now, 1f); break;
                case MoveStatus.Failed: OnGoalUnreachable(now); break;
                case MoveStatus.Idle: moveActive = false; break; // interrupted externally: re-request later
            }
        }

        /// <summary>Applies the goal-replacement rules. Returns false if the state changed (caller must stop ticking).</summary>
        private bool ReconsiderGoal(float now)
        {
            bool haveBest = listener.Memory.TryGetBest(now, out var best);
            SoundMemory.Entry latest = default;
            bool currentAlive = HasGoalEntry && listener.Memory.TryGetLatestFor(GoalEntry, out latest);

            if (currentAlive)
            {
                if (haveBest && best.Revision != latest.Revision && listener.Ranker.Compare(best.Sound, latest.Sound) < 0)
                { Retarget(best, now); Log($"goal replaced by better {best.Sound.Stimulus.Category}#{best.Sound.Stimulus.SourceId}"); }
                else if (latest.Revision != GoalEntry.Revision)
                { Retarget(latest, now); Log("goal position refreshed by newer sound from same source"); }
                return true;
            }
            if (haveBest) { Retarget(best, now); Log("goal expired; switched to remembered " + best.Sound.Stimulus.Category); return true; }
            move.Stop(); moveActive = false;
            Log("goal expired and memory empty");
            EnterPatrol();
            return false;
        }

        /// <summary>Bounded fallback: no repeated path spam. Forget the unreachable memory, search where we stand, block new investigations briefly.</summary>
        private void OnGoalUnreachable(float now)
        {
            FailedMoves++;
            moveActive = false;
            if (HasGoalEntry) listener.Memory.Forget(GoalEntry);
            investigateBlockedUntil = now + failedRetryCooldown;
            nextRequestAllowed = now + failedRetryCooldown;
            Log($"goal unreachable ({move.FailureReason}); searching in place");
            move.Stop();
            HasGoalEntry = false;       // already forgotten; nothing more to consume
            EnterSearch(now, 0.5f);
        }

        // ---------------------------------------------------------------- Search
        private void EnterSearch(float now, float durationScale)
        {
            SetState(HunterState.Search);
            move.Stop();
            searchStart = now;
            searchEnd = now + searchDuration * durationScale;
            searchBaseYaw = transform.eulerAngles.y;
            if (!GoalIsLastSeen && HasGoalEntry)
            {
                // face the exact remembered position before sweeping
                Vector3 d = GoalPosition - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) searchBaseYaw = Quaternion.LookRotation(d).eulerAngles.y;
            }
            else if (GoalIsLastSeen)
            {
                Vector3 d = GoalPosition - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) searchBaseYaw = Quaternion.LookRotation(d).eulerAngles.y;
            }
        }

        private void TickSearch(float now)
        {
            // Sweep left-right around the base yaw (local search; vision keeps running).
            float t = now - searchStart;
            float yaw = searchBaseYaw + Mathf.Sin(t * searchSweepSpeed / Mathf.Max(1f, searchSweepAngle)) * searchSweepAngle;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, yaw, 0f), turnSpeed * Time.deltaTime);

            if (now >= nextReplan)
            {
                nextReplan = now + replanInterval;
                if (PreemptSearch(now)) return;
            }

            if (now < searchEnd) return;

            // Defined consumption point: the search is over.
            if (HasGoalEntry)
            {
                bool consumed = listener.Memory.Forget(GoalEntry);
                bool refreshed = !consumed && listener.Memory.TryGetLatestFor(GoalEntry, out _);
                Log(consumed ? "investigation complete; sound consumed"
                    : refreshed ? "investigation complete; newer sound from same source kept"
                    : "investigation complete; memory had already expired");
                HasGoalEntry = false;
            }
            GoalIsLastSeen = false;

            if (now >= investigateBlockedUntil && listener.Memory.TryGetBest(now, out var next))
            { BeginInvestigate(next, now); Log("continuing with next remembered sound"); }
            else EnterPatrol();
        }

        /// <summary>Mid-search: a strictly better sound, or a refresh of the searched source, restarts the investigation.</summary>
        private bool PreemptSearch(float now)
        {
            if (now < investigateBlockedUntil) return false;
            if (!listener.Memory.TryGetBest(now, out var best)) return false;
            if (!HasGoalEntry) { if (!GoalIsLastSeen) return false; BeginInvestigate(best, now); return true; }
            if (listener.Memory.TryGetLatestFor(GoalEntry, out var latest) && latest.Revision != GoalEntry.Revision)
            { BeginInvestigate(latest, now); Log("search restarted: same source heard again"); return true; }
            if (best.Revision != GoalEntry.Revision && listener.Ranker.Compare(best.Sound, GoalEntry.Sound) < 0)
            { BeginInvestigate(best, now); Log("search preempted by better sound"); return true; }
            return false;
        }

        // ---------------------------------------------------------------- Combat
        private void EnterCombat()
        {
            SetState(HunterState.Combat);
            HasGoalEntry = false; GoalIsLastSeen = false; moveActive = false; patrolLegActive = false;
            nextRequestAllowed = 0f; nextReplan = 0f;
            nextAttack = Now + 0.4f; // reaction delay before the first shot
            move.Stop();
        }

        private void TickCombat(float now)
        {
            if (canSee)
            {
                FaceTowards(LastSeenPosition);
                float dist = Vector3.Distance(transform.position, LastSeenPosition);
                if (dist <= attackRange) TryAttack(now);
                if (dist > combatStopDistance) ChaseLastSeen(now);
                else if (moveActive) { move.Stop(); moveActive = false; }
                return;
            }

            // Sight lost: keep heading to the last SEEN position, then investigate it.
            if (now - lastSeenTime < loseSightDelay) { ChaseLastSeen(now); return; }

            move.Stop(); moveActive = false;
            GoalIsLastSeen = true; HasGoalEntry = false; GoalPosition = LastSeenPosition;
            SetState(HunterState.Investigate);
            nextRequestAllowed = 0f; nextReplan = now + replanInterval;
            Log("sight lost; investigating last seen position");
        }

        private void ChaseLastSeen(float now)
        {
            if (now < nextReplan) return;
            nextReplan = now + replanInterval;   // bounded replans
            PathRequests++;
            moveActive = move.RequestMove(LastSeenPosition, combatStopDistance * 0.5f);
        }

        private void TryAttack(float now)
        {
            if (now < nextAttack) return;
            // Fresh, valid perception required at the moment of the shot.
            if (!vision.TryPerceive(out Vector3 aim)) return;
            if (Vector3.Distance(vision.transform.position, aim) > attackRange) return;
            nextAttack = now + attackCooldown;
            ShotsFired++;
            if (UnityEngine.Random.value <= hitChance && vision.Target != null)
                vision.Target.TakeDamage(new DamageInfo(attackDamage, aim, (aim - transform.position).normalized, gameObject));
        }

        private void FaceTowards(Vector3 p)
        {
            Vector3 d = p - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), turnSpeed * Time.deltaTime);
        }

        // ---------------------------------------------------------------- Dead
        private void OnDied(Health _) => EnterDead();

        private void EnterDead()
        {
            if (State == HunterState.Dead) return;
            SetState(HunterState.Dead);
            canSee = false; moveActive = false; patrolLegActive = false; HasGoalEntry = false;
            if (move != null) move.Stop();
            listener.SetListening(false);          // unsubscribes and clears memory
            vision.enabled = false;
            transform.rotation = transform.rotation * Quaternion.Euler(0f, 0f, 90f); // visible: tipped over
        }

        // ---------------------------------------------------------------- helpers
        private void SetState(HunterState s)
        {
            if (s == State) return;
            var old = State; State = s;
            Log($"{old} -> {s}");
            StateChanged?.Invoke(old, s);
        }

        private void Log(string msg)
        {
            LastTransition = msg;
            History.Add($"{Now:0.0}s {msg}");
            if (History.Count > 8) History.RemoveAt(0);
            if (logTransitions) Debug.Log("[SoundHunter] " + msg, this);
        }
    }
}
