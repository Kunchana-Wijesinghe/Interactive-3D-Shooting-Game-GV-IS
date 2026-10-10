using System.Collections.Generic;
using FacilityBreach.Combat;
using Perera.Core;
using Perera.SoundHunter;
using UnityEngine;

namespace FacilityBreach.AI.SoundHunter
{
    public enum SoundHunterState { Patrol, Investigate, Search, Combat, Inactive }

    [RequireComponent(typeof(EnemyCombatAdapter))]
    public sealed class FacilityBreachSoundHunterBrain : MonoBehaviour, IEnemyActivitySink
    {
        public SoundHunterState CurrentState { get; private set; } = SoundHunterState.Patrol;
        public float SightRange = 16.0f;
        public float SoundBudget = 50.0f;
        public float MemoryExpirySeconds = 10.0f;
        public float SearchDuration = 4.0f;
        public Color TealColor = new Color(0, 0.8f, 0.8f);
        public Color OrangeColor = new Color(1, 0.5f, 0);
        public Color VioletColor = new Color(0.6f, 0.2f, 0.8f);
        public Color LimeColor = new Color(0.7f, 1, 0.2f);

        private EnemyCombatAdapter combatAdapter;
        private IHunterMovement movement;
        private FacilityBreachHearingGraphSetup graphSetup;
        private FacilityBreachRuntime runtime;
        private SoundMemory memory;
        private SoundRanker ranker;
        private HearingEvaluator evaluator;

        private Renderer chestRenderer;
        private Material chestMaterial;

        private Transform playerTransform;
        private Vector3 lastSeenPosition;
        private bool hasLastSeen;
        private float searchTimer;
        private SoundMemory.Entry currentInvestigatingEntry;
        private bool isInvestigatingSound;
        private bool isActive = true;

        private readonly List<Vector3> patrolWaypoints = new List<Vector3>();
        private int currentPatrolIndex;

        private void Awake()
        {
            combatAdapter = GetComponent<EnemyCombatAdapter>();
            movement = GetComponent<IHunterMovement>();
            if (movement == null) movement = gameObject.AddComponent<SoundHunterNavMeshMover>();
            graphSetup = GetComponent<FacilityBreachHearingGraphSetup>();
            if (!graphSetup) graphSetup = gameObject.AddComponent<FacilityBreachHearingGraphSetup>();

            ranker = new SoundRanker();
            memory = new SoundMemory(ranker, MemoryExpirySeconds, 4);

            SetupChestVisual();
            SetupPatrolWaypoints();
        }

        private void Start()
        {
            runtime = FindAnyObjectByType<FacilityBreachRuntime>();
            graphSetup.BuildGraph();
            if (graphSetup.Graph != null)
            {
                evaluator = new HearingEvaluator(graphSetup.Graph);
            }
        }

        private void OnEnable()
        {
            SoundEventBus.Emitted += OnSoundEmitted;
        }

        private void OnDisable()
        {
            SoundEventBus.Emitted -= OnSoundEmitted;
        }

        private void OnDestroy()
        {
            SoundEventBus.Emitted -= OnSoundEmitted;
            if (chestMaterial) Destroy(chestMaterial);
        }

        private void SetupChestVisual()
        {
            var lightObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lightObj.name = "FB_SH_StateLight";
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0, 1.2f, 0.35f);
            lightObj.transform.localScale = Vector3.one * 0.25f;
            var col = lightObj.GetComponent<Collider>();
            if (col) Destroy(col);
            chestRenderer = lightObj.GetComponent<Renderer>();
            chestMaterial = new Material(Shader.Find("Standard"));
            chestMaterial.color = OrangeColor;
            chestRenderer.sharedMaterial = chestMaterial;
        }

        private void SetupPatrolWaypoints()
        {
            // Waypoints near Exit and connected corridors
            patrolWaypoints.Add(new Vector3(18, 0, -18));
            patrolWaypoints.Add(new Vector3(15, 0, -10));
            patrolWaypoints.Add(new Vector3(0, 0, 0));
            patrolWaypoints.Add(new Vector3(0, 0, -10));
        }

        public void OnSoundEmitted(SoundStimulus stimulus)
        {
            if (!isActive || !runtime || combatAdapter.IsDead) return;
            if (float.IsNaN(stimulus.Position.x) || float.IsInfinity(stimulus.Position.x)) return;

            if (evaluator == null && graphSetup.Graph != null)
            {
                evaluator = new HearingEvaluator(graphSetup.Graph);
            }

            if (evaluator == null) return;

            int sourceNode = graphSetup.GetNearestNode(stimulus.Position);
            int listenerNode = graphSetup.GetNearestNode(transform.position);

            if (evaluator.TryHear(stimulus, sourceNode, listenerNode, out var heard))
            {
                memory.Remember(heard, runtime.GameplayTime);
            }
        }

        private void Update()
        {
            if (!runtime || combatAdapter.IsDead) return;
            if (!isActive)
            {
                SetState(SoundHunterState.Inactive);
                return;
            }

            float now = runtime.GameplayTime;
            memory.Prune(now);

            playerTransform = runtime.PlayerTransform;
            bool canSeePlayer = EvaluateVision(out Vector3 visiblePlayerPos);

            if (canSeePlayer)
            {
                lastSeenPosition = visiblePlayerPos;
                hasLastSeen = true;
                SetState(SoundHunterState.Combat);
            }
            else if (CurrentState == SoundHunterState.Combat)
            {
                // Lost sight of player -> transition to investigating last seen position
                SetState(SoundHunterState.Investigate);
            }

            switch (CurrentState)
            {
                case SoundHunterState.Combat:
                    ExecuteCombat(canSeePlayer);
                    break;

                case SoundHunterState.Investigate:
                    ExecuteInvestigate(now);
                    break;

                case SoundHunterState.Search:
                    ExecuteSearch();
                    break;

                case SoundHunterState.Patrol:
                    ExecutePatrol(now);
                    break;

                case SoundHunterState.Inactive:
                    UpdateChestLight();
                    break;
            }

            UpdateChestLight();
        }

        private bool EvaluateVision(out Vector3 visiblePos)
        {
            visiblePos = Vector3.zero;
            if (!playerTransform) return false;

            Vector3 eyeOrigin = transform.position + Vector3.up * 1.4f;
            Vector3 targetPos = playerTransform.position + Vector3.up * 1.0f;
            Vector3 delta = targetPos - eyeOrigin;

            if (delta.magnitude > SightRange) return false;

            if (Physics.Raycast(eyeOrigin, delta.normalized, out var hit, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(playerTransform))
                {
                    visiblePos = playerTransform.position;
                    return true;
                }
            }
            return false;
        }

        private void ExecuteCombat(bool canSeePlayer)
        {
            if (canSeePlayer && playerTransform)
            {
                movement.RequestMove(playerTransform.position, 1.0f);
                combatAdapter.TryAttack(playerTransform);
            }
            else if (hasLastSeen)
            {
                SetState(SoundHunterState.Investigate);
            }
        }

        private void ExecuteInvestigate(float now)
        {
            if (hasLastSeen)
            {
                movement.RequestMove(lastSeenPosition, 0.8f);
                if (movement.Status == MoveStatus.Arrived)
                {
                    hasLastSeen = false;
                    searchTimer = SearchDuration;
                    SetState(SoundHunterState.Search);
                }
                return;
            }

            if (!isInvestigatingSound || !memory.Contains(currentInvestigatingEntry.Revision))
            {
                if (memory.TryGetBest(now, out var best))
                {
                    currentInvestigatingEntry = best;
                    isInvestigatingSound = true;
                }
                else
                {
                    isInvestigatingSound = false;
                    SetState(SoundHunterState.Patrol);
                    return;
                }
            }

            movement.RequestMove(currentInvestigatingEntry.Position, 0.8f);

            if (movement.Status == MoveStatus.Arrived)
            {
                memory.Forget(currentInvestigatingEntry);
                isInvestigatingSound = false;
                searchTimer = SearchDuration;
                SetState(SoundHunterState.Search);
            }
            else if (movement.Status == MoveStatus.Failed)
            {
                memory.Forget(currentInvestigatingEntry);
                isInvestigatingSound = false;
                SetState(SoundHunterState.Patrol);
            }
        }

        private void ExecuteSearch()
        {
            movement.Stop();
            searchTimer -= Time.deltaTime;
            transform.Rotate(0, 90.0f * Time.deltaTime, 0);

            if (searchTimer <= 0)
            {
                SetState(SoundHunterState.Patrol);
            }
        }

        private void ExecutePatrol(float now)
        {
            if (memory.TryGetBest(now, out var best))
            {
                currentInvestigatingEntry = best;
                isInvestigatingSound = true;
                SetState(SoundHunterState.Investigate);
                return;
            }

            if (patrolWaypoints.Count == 0) return;
            Vector3 targetWp = patrolWaypoints[currentPatrolIndex];
            movement.RequestMove(targetWp, 0.8f);

            if (movement.Status == MoveStatus.Arrived)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolWaypoints.Count;
            }
        }

        private void SetState(SoundHunterState newState)
        {
            CurrentState = newState;
        }

        private void UpdateChestLight()
        {
            if (!chestMaterial) return;
            if (!isActive || combatAdapter.IsDead)
            {
                chestMaterial.color = Color.gray;
                return;
            }

            if (combatAdapter.Warning)
            {
                chestMaterial.color = Color.white;
            }
            else
            {
                switch (CurrentState)
                {
                    case SoundHunterState.Combat: chestMaterial.color = Color.red; break;
                    case SoundHunterState.Investigate:
                    case SoundHunterState.Search: chestMaterial.color = Color.yellow; break;
                    case SoundHunterState.Patrol: chestMaterial.color = OrangeColor; break;
                    default: chestMaterial.color = Color.gray; break;
                }
            }
        }

        public void SetEnemyActive(bool active)
        {
            isActive = active;
            if (!active)
            {
                movement.Stop();
                memory.Clear();
                isInvestigatingSound = false;
                hasLastSeen = false;
                SetState(SoundHunterState.Inactive);
            }
            else
            {
                SetState(SoundHunterState.Patrol);
            }
        }

        public void ResetEnemyActions()
        {
            memory.Clear();
            isInvestigatingSound = false;
            hasLastSeen = false;
            SetState(SoundHunterState.Patrol);
        }
    }
}
