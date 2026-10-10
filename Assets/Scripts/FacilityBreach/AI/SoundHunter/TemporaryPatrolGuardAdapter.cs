using FacilityBreach.Combat;
using Perera.SoundHunter;
using UnityEngine;

namespace FacilityBreach.AI.SoundHunter
{
    // Explicit temporary visual-patrol adapter: teammate PG decision code is not published.
    // No hearing subscription, cover scoring or flank decisions.
    public sealed class TemporaryPatrolGuardAdapter : MonoBehaviour, IEnemyActivitySink
    {
        public Vector3[] Route;
        public float SightRange = 16;
        public bool SeesPlayer { get; private set; }
        private SoundHunterNavMeshMover mover;
        private EnemyCombatAdapter combat;
        private FacilityBreachRuntime runtime;
        private int waypoint;
        private bool active = true;
        private float nextDecision;
        private void Start()
        {
            combat = GetComponent<EnemyCombatAdapter>(); mover = GetComponent<SoundHunterNavMeshMover>();
            runtime = FindAnyObjectByType<FacilityBreachRuntime>();
        }
        private void Update()
        {
            if (!active || !runtime || !runtime.AcceptsGameplay || combat.IsDead || runtime.GameplayTime < nextDecision) return;
            nextDecision = runtime.GameplayTime + .2f;
            SeesPlayer = AgentPerception.Visible(transform, runtime.PlayerTransform, SightRange, out var seen);
            if (SeesPlayer) { mover.Stop(); combat.TryAttack(runtime.PlayerTransform); }
            else if (Route != null && Route.Length > 0)
            {
                if (mover.Status == MoveStatus.Arrived || mover.Status == MoveStatus.Failed) waypoint = (waypoint + 1) % Route.Length;
                mover.RequestMove(Route[waypoint], .5f);
            }
        }
        public void SetEnemyActive(bool value) { active = value; if (!value && mover) mover.Stop(); }
        public void ResetEnemyActions() { waypoint = 0; nextDecision = 0; SeesPlayer = false; }
    }
}
