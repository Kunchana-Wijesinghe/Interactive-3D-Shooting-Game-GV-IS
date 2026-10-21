using FacilityBreach.AI.SoundHunter;
using FacilityBreach.Combat;
using Perera.SoundHunter;
using UnityEngine;

namespace FacilityBreach.AI.Roles
{
    /// <summary>
    /// Presentation-only idle facing: while a role enemy rests at its post and sees nobody, it faces the nearest wall.
    /// As soon as it sees the player the brain's own FacePoint calls take over, so it turns to the player.
    /// Reads the brain's public state only; it never changes decisions, movement goals or combat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RestingWallFacing : MonoBehaviour
    {
        private RoleDecisionBrain brain;
        private SoundHunterNavMeshMover mover;
        private EnemyCombatAdapter combat;
        private FacilityBreachRuntime runtime;
        private Vector3 post, wallPoint;
        private bool hasWall;

        private void Start()
        {
            brain = GetComponent<RoleDecisionBrain>(); mover = GetComponent<SoundHunterNavMeshMover>();
            combat = GetComponent<EnemyCombatAdapter>(); runtime = FindAnyObjectByType<FacilityBreachRuntime>();
            post = transform.position;
            hasWall = FindNearestWall(out wallPoint);
            if (hasWall)
            {
                Vector3 toWall = wallPoint - transform.position; toWall.y = 0;
                if (toWall.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(toWall); // start already facing the wall
            }
        }

        // Nearest solid wall in 16 directions; doors, card gates and door seals do not count as walls.
        private bool FindNearestWall(out Vector3 point)
        {
            point = default; float best = float.MaxValue; Vector3 origin = transform.position + Vector3.up * 1.2f;
            for (int i = 0; i < 16; i++)
            {
                Vector3 direction = Quaternion.Euler(0, i * 22.5f, 0) * Vector3.forward;
                foreach (var hit in Physics.RaycastAll(origin, direction, 25, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<InteractiveDoor>() ||
                        hit.collider.name.StartsWith("Card required") || hit.collider.GetComponentInParent<EnemyCombatAdapter>()) continue;
                    if (hit.distance < best) { best = hit.distance; point = hit.point; }
                }
            }
            return best < float.MaxValue;
        }

        private void LateUpdate()
        {
            if (!hasWall || !brain || !mover || !runtime || !runtime.AcceptsGameplay || combat.IsDead) return;
            bool resting = brain.State == RoleState.Patrol && !brain.SeesPlayer && mover.Status != MoveStatus.Moving &&
                (transform.position - post).sqrMagnitude < 2.25f;
            if (resting) mover.FacePoint(new Vector3(wallPoint.x, transform.position.y, wallPoint.z));
        }
    }
}
