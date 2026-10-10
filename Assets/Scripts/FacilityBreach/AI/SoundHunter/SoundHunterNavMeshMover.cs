using Perera.SoundHunter;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityBreach.AI.SoundHunter
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SoundHunterNavMeshMover : MonoBehaviour, IHunterMovement
    {
        private NavMeshAgent agent;
        private float arrivalDistance = 1.2f;
        private float replanInterval = 0.25f;
        private float nextReplanTime;
        private Vector3 currentGoal;
        private bool hasGoal;

        public Vector3 CurrentPosition => transform.position;
        public bool IsArrived => hasGoal && !agent.pathPending && agent.remainingDistance <= arrivalDistance;
        public bool IsUnreachable => hasGoal && !agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathPartial;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (!agent) agent = gameObject.AddComponent<NavMeshAgent>();
            agent.stoppingDistance = 0.5f;
            agent.speed = 4.0f;
            agent.acceleration = 12.0f;
        }

        public void MoveTo(Vector3 target)
        {
            if (Time.time < nextReplanTime && hasGoal && Vector3.Distance(target, currentGoal) < 0.5f) return;
            currentGoal = target;
            hasGoal = true;
            nextReplanTime = Time.time + replanInterval;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(target);
            }
        }

        public void Stop()
        {
            hasGoal = false;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        private void Update()
        {
            if (!hasGoal || !agent.isOnNavMesh) return;
            // Check for nearby interactive doors along path to auto-open authorized doors
            Collider[] hits = Physics.OverlapSphere(transform.position, 2.0f);
            foreach (var hit in hits)
            {
                var door = hit.GetComponentInParent<InteractiveDoor>();
                if (door && !door.IsOpen)
                {
                    door.OpenDoor();
                }
            }
        }
    }
}
