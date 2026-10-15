using UnityEngine;
using UnityEngine.AI;

namespace Perera.SoundHunter
{
    /// <summary>
    /// TEST-ONLY IHunterMovement built on Unity's NavMeshAgent so the Sound Hunter is playable in
    /// the isolated Perera test scene. It is NOT Bandara's movement controller and is not the
    /// final implementation; replace it with an adapter over the shared controller on integration.
    /// One path computation per RequestMove (the brain rate-limits requests). Incomplete paths are
    /// treated as failure; a stuck / overlong move is abandoned (bounded).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class TestNavMeshMover : MonoBehaviour, IHunterMovement
    {
        [SerializeField] private float speed = 3.5f;
        [Tooltip("Snap radius when sampling the requested goal onto the NavMesh.")]
        [SerializeField] private float sampleDistance = 3f;
        [SerializeField] private float stuckSeconds = 2.5f;
        [SerializeField] private float maxMoveSeconds = 45f;

        private NavMeshAgent agent;
        private float moveStart, stuckAccum, windowStart;
        private Vector3 windowPos;
        private const float StuckWindow = 0.5f, StuckMinProgress = 0.1f;

        public MoveStatus Status { get; private set; }
        public string FailureReason { get; private set; } = "";

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.speed = speed;
        }

        private void OnEnable() { Status = MoveStatus.Idle; FailureReason = ""; }

        public bool RequestMove(Vector3 goal, float stoppingDistance)
        {
            if (!agent.isOnNavMesh) return Fail("agent not on NavMesh");
            if (!NavMesh.SamplePosition(goal, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
                return Fail("goal not near NavMesh");

            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                return Fail("no complete path");

            agent.stoppingDistance = Mathf.Max(0.05f, stoppingDistance);
            agent.isStopped = false;
            agent.SetPath(path);
            Status = MoveStatus.Moving;
            FailureReason = "";
            moveStart = windowStart = Time.time; stuckAccum = 0f; windowPos = transform.position;
            return true;
        }

        public void Stop()
        {
            if (agent != null && agent.isOnNavMesh) { agent.ResetPath(); agent.velocity = Vector3.zero; } // hard stop (test mover)
            Status = MoveStatus.Idle;
        }

        private bool Fail(string reason)
        {
            Status = MoveStatus.Failed; FailureReason = reason;
            if (agent != null && agent.isOnNavMesh) agent.ResetPath();
            return false;
        }

        private void Update()
        {
            if (Status != MoveStatus.Moving) return;

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.15f)
            {
                agent.ResetPath();
                Status = MoveStatus.Arrived;
                return;
            }

            if (Time.time - moveStart > maxMoveSeconds) { Fail("move timeout"); return; }

            // Progress is measured over a time window, so the result does not depend on frame rate.
            if (Time.time - windowStart >= StuckWindow)
            {
                if ((transform.position - windowPos).magnitude < StuckMinProgress) stuckAccum += Time.time - windowStart;
                else stuckAccum = 0f;
                windowStart = Time.time; windowPos = transform.position;
                if (stuckAccum >= stuckSeconds) Fail("stuck");
            }
        }
    }
}
