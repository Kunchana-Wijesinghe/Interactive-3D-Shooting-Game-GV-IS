using FacilityBreach.Audio;
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
    [DisallowMultipleComponent]
    public sealed class EnemyCombatAdapter : MonoBehaviour, IDamageable
    {
        public string Identity = "SH-01";
        public int MaximumHealth = 80;
        public float Range = 12, WarningSeconds = .4f, ReloadSeconds = 1.5f, ShotInterval = .2f;
        [Range(0, 1)] public float MissChance = .4f;
        public int StartingReserve = 1000;
        public bool IsDead => record == null || record.Life != EnemyLife.Active;
        public bool Warning => pending && !IsDead && runtime && runtime.AcceptsGameplay;
        public int ResolvedShots { get; private set; }
        public int SuccessfulHits { get; private set; }
        public float ReloadRemaining => reload;
        public void CancelPendingAttack() { pending = null; warning = 0; }
        public int Magazine { get; private set; } = 50;
        public int CurrentHealth => record?.Health ?? 0;
        public Vector3 RevivalPost => IsDead ? deathPoint : post;
        public EnemyLife Life => record?.Life ?? EnemyLife.AwaitingSafeSpawn;
        private int reserve;
        private EnemyRecord record;
        private FacilityBreachRuntime runtime;
        private Transform pending;
        private float warning, reload, cadence, nextReviveAttempt;
        private Vector3 post, deathPoint;
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
            if (!runtime || !runtime.AcceptsGameplay || IsDead || info.Amount <= 0 || !float.IsFinite(info.Amount)) return false;
            bool applied = runtime.Enemies.Damage(Identity, Mathf.CeilToInt(info.Amount), runtime.GameplayTime);
            GameplayDiagnostics.Trace($"damage {Identity} amount={info.Amount} HP={CurrentHealth} life={Life} applied={applied}", this);
            Synchronize(); return applied;
        }
        public bool TryAttack(Transform perceivedTarget)
        {
            if (perceivedTarget && !IsDead && runtime && runtime.AcceptsGameplay)
                GetComponent<FacilityBreach.AI.SoundHunter.SoundHunterNavMeshMover>()?.FacePoint(perceivedTarget.position);
            if (IsDead || !runtime.AcceptsGameplay || pending || reload > 0 || cadence > 0 || Magazine <= 0 || !ClearSight(perceivedTarget)) return false;
            pending = perceivedTarget; warning = Mathf.Max(.35f, WarningSeconds); return true;
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
            if (runtime.AcceptsGameplay && record.Life == EnemyLife.AwaitingSafeSpawn && runtime.GameplayTime >= nextReviveAttempt) { nextReviveAttempt = runtime.GameplayTime + .5f; TryRevive(); }
            if (IsDead || !runtime.AcceptsGameplay) { CancelPendingAttack(); return; }
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
            Magazine--; cadence = Mathf.Max(.05f, ShotInterval); ResolvedShots++;
            bool landed = false;
            if (Random.value >= MissChance)
                landed = target.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo(10, target.position, (target.position - transform.position).normalized, gameObject)) ?? false;
            if (landed) SuccessfulHits++;
            GameplayDiagnostics.Trace($"enemy shot id={Identity} resolved={ResolvedShots} hit={landed} magazine={Magazine} playerHP={runtime.Resources.Health}", this);
            SoundEventBus.Emit(transform.position, 40, SoundCategory.Gunshot, GetEntityId().GetHashCode());
            if (Identity == "SH-01")
            {
                var gear = GetComponent<SoundHunterTacticalGear>();
                FacilityAudioPlayer.Instance.PlayEnemyGunshot(gear && gear.Muzzle ? gear.Muzzle.position : transform.position + Vector3.up * 1.2f);
                if (gear) gear.NotifyShot();
            }
        }
        private void Synchronize()
        {
            if (record.Life == lastLife) return;
            lastLife = record.Life; bool active = !IsDead;
            if (!active && record.Life == EnemyLife.TemporarilyInactive) deathPoint = transform.position; // revive where it fell
            pending = null; reload = 0; cadence = 0;
            foreach (var collider in colliders) if (collider) collider.enabled = active;
            var nav = GetComponent<NavMeshAgent>(); if (nav && nav.isOnNavMesh) { nav.ResetPath(); nav.isStopped = !active; }
            foreach (var component in GetComponents<MonoBehaviour>())
                if (component is IEnemyActivitySink sink) { sink.SetEnemyActive(active); if (active) sink.ResetEnemyActions(); }
        }
        private void OnDisable() { CancelPendingAttack(); }
        private void TryRevive()
        {
            for (int i = 0; i < 9; i++)
            {
                Vector3 candidate = deathPoint + (i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(i * Mathf.PI / 4), 0, Mathf.Sin(i * Mathf.PI / 4)) * 1.2f);
                if (!NavMesh.SamplePosition(candidate, out var hit, .6f, NavMesh.AllAreas)) continue;
                var body = GetComponent<CapsuleCollider>();
                float radius = body ? body.radius : .35f, halfSegment = body ? body.height * .5f - radius : .55f;
                Vector3 center = hit.position + (body ? body.center : Vector3.up * .9f);
                if (Physics.CheckCapsule(center - Vector3.up * halfSegment, center + Vector3.up * halfSegment, radius, ~0, QueryTriggerInteraction.Ignore)) continue;
                var nav = GetComponent<NavMeshAgent>(); if (nav && !nav.Warp(hit.position)) continue;
                transform.position = hit.position; runtime.Enemies.Revive(record, true); Magazine = 50; reserve = StartingReserve;
                Synchronize(); return;
            }
        }
    }
}
