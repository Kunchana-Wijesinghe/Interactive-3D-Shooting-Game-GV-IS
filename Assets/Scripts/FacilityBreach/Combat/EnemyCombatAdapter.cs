using FacilityBreach.Core;
using Perera.Core;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityBreach.Combat
{
    public interface IEnemyActivitySink
    {
        void SetEnemyActive(bool active);
        void ResetEnemyActions();
    }
    // Shared combat only. Team decision components supply successfully perceived targets.
    public sealed class EnemyCombatAdapter : MonoBehaviour, IDamageable
    {
        public string Identity = "SH-01";
        public int MaximumHealth = 80;
        public float Range = 16, WarningSeconds = .4f, ReloadSeconds = 1.5f, ShotInterval = .2f;
        [Range(0, 1)] public float MissChance = .4f;
        public int StartingReserve = 1000;
        public bool IsDead => record == null || record.Life != EnemyLife.Active;
        public bool Warning => pending && !IsDead;
        public int Magazine { get; private set; } = 50;
        private int reserve;
        private EnemyRecord record;
        private FacilityBreachRuntime runtime;
        private Transform pending;
        private float warning, reload, cadence;
        private Vector3 post;
        private Collider[] colliders;
        private EnemyLife lastLife;
        private void Start()
        {
            runtime = FindAnyObjectByType<FacilityBreachRuntime>();
            if (!runtime) { enabled = false; return; }
            record = runtime.Enemies.Register(Identity, MaximumHealth); reserve = StartingReserve;
            post = transform.position; colliders = GetComponentsInChildren<Collider>();
        }
        public bool TakeDamage(in DamageInfo info)
        {
            if (!runtime || !runtime.Run.Running || runtime.Run.Paused || IsDead || info.Amount <= 0 || float.IsNaN(info.Amount)) return false;
            bool applied = runtime.Enemies.Damage(Identity, Mathf.CeilToInt(info.Amount), runtime.GameplayTime);
            Synchronize(); return applied;
        }
        public bool TryAttack(Transform perceivedTarget)
        {
            if (IsDead || !runtime.Run.Running || runtime.Run.Paused || pending || reload > 0 || cadence > 0 || Magazine <= 0 || !ClearSight(perceivedTarget)) return false;
            pending = perceivedTarget; warning = WarningSeconds; return true;
        }
        private bool ClearSight(Transform target)
        {
            if (!target) return false;
            Vector3 origin = transform.position + Vector3.up * 1.4f;
            Vector3 aim = target.position + Vector3.up;
            Vector3 delta = aim - origin;
            if (delta.magnitude > Range) return false;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(target)) return false;
            return true;
        }
        private void Update()
        {
            if (!runtime || record == null) return;
            runtime.Enemies.Tick(runtime.GameplayTime); Synchronize();
            if (record.Life == EnemyLife.AwaitingSafeSpawn) TryRevive();
            if (IsDead || !runtime.Run.Running || runtime.Run.Paused) return;
            float dt = Time.deltaTime; cadence = Mathf.Max(0, cadence - dt);
            if (reload > 0)
            {
                reload = Mathf.Max(0, reload - dt);
                if (reload == 0) { int n = Mathf.Min(50 - Magazine, reserve); reserve -= n; Magazine += n; }
            }
            if (Magazine == 0 && reserve > 0 && reload == 0) reload = ReloadSeconds;
            if (!pending) return;
            warning -= dt;
            if (warning > 0) return;
            var target = pending; pending = null;
            if (!ClearSight(target) || Magazine <= 0) return;
            Magazine--; cadence = ShotInterval;
            if (Random.value >= MissChance)
                target.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo(10, target.position, (target.position - transform.position).normalized, gameObject));
            SoundEventBus.Emit(transform.position, 40, SoundCategory.Gunshot, GetEntityId().GetHashCode());
        }
        private void Synchronize()
        {
            if (record.Life == lastLife) return;
            lastLife = record.Life; bool active = !IsDead;
            pending = null; reload = 0; cadence = 0;
            foreach (var collider in colliders) if (collider) collider.enabled = active;
            var nav = GetComponent<NavMeshAgent>(); if (nav && nav.isOnNavMesh) { nav.ResetPath(); nav.isStopped = !active; }
            foreach (var component in GetComponents<MonoBehaviour>())
                if (component is IEnemyActivitySink sink) { sink.SetEnemyActive(active); if (active) sink.ResetEnemyActions(); }
        }
        private void TryRevive()
        {
            for (int i = 0; i < 9; i++)
            {
                Vector3 candidate = post + (i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(i * Mathf.PI / 4), 0, Mathf.Sin(i * Mathf.PI / 4)) * 1.2f);
                if (!NavMesh.SamplePosition(candidate, out var hit, .6f, NavMesh.AllAreas)) continue;
                if (Physics.CheckCapsule(hit.position + Vector3.up * .4f, hit.position + Vector3.up * 1.4f, .3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                var nav = GetComponent<NavMeshAgent>(); if (nav && !nav.Warp(hit.position)) continue;
                transform.position = hit.position; runtime.Enemies.Revive(record, true); Magazine = 50; reserve = StartingReserve;
                Synchronize(); return;
            }
        }
    }
}
