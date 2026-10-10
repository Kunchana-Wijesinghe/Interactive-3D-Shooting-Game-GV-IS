using Perera.SoundHunter;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityBreach.AI.SoundHunter
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SoundHunterNavMeshMover : MonoBehaviour, IHunterMovement
    {
        private NavMeshAgent agent;
        private MoveStatus status = MoveStatus.Idle;
        private string failureReason = "";
        private Vector3 currentGoal;
        private float currentStoppingDistance = 0.5f;

        public MoveStatus Status => status;
        public string FailureReason => failureReason;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (!agent) agent = gameObject.AddComponent<NavMeshAgent>();
            agent.speed = 4.0f;
            agent.acceleration = 12.0f;
        }

        public bool RequestMove(Vector3 goal, float stoppingDistance)
        {
            currentGoal = goal;
            currentStoppingDistance = Mathf.Max(0.2f, stoppingDistance);
            agent.stoppingDistance = currentStoppingDistance;

            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                if (agent.SetDestination(goal))
                {
                    status = MoveStatus.Moving;
                    failureReason = "";
                    return true;
                }
            }

            status = MoveStatus.Failed;
            failureReason = "NavMesh destination set failed or agent off NavMesh";
            return false;
        }

        public void Stop()
        {
            status = MoveStatus.Idle;
            failureReason = "";
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        private void Update()
        {
            if (status != MoveStatus.Moving || !agent.isOnNavMesh) return;

            if (!agent.pathPending)
            {
                if (agent.remainingDistance <= currentStoppingDistance)
                {
                    status = MoveStatus.Arrived;
                }
                else if (agent.pathStatus == NavMeshPathStatus.PathPartial || agent.pathStatus == NavMeshPathStatus.PathInvalid)
                {
                    status = MoveStatus.Failed;
                    failureReason = "Path partial or invalid";
                }
            }

            // Interact with nearby doors along path
            Collider[] hits = Physics.OverlapSphere(transform.position, 2.0f);
            foreach (var hit in hits)
            {
                var door = hit.GetComponentInParent<InteractiveDoor>();
                if (door && !door.IsOpen)
                {
                    door.RequestOpen(true);
                }
            }
        }
    }
}
