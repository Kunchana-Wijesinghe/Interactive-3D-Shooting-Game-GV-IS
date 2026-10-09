using System;
using Perera.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Perera.Combat
{
    /// <summary>
    /// First-person hitscan weapon (instant ray, NOT simulated ballistics).
    /// Aim ray: camera -> intended point. Obstruction check: muzzle -> intended
    /// point, so nearby cover/walls in front of the barrel absorb the shot.
    /// Input uses the project's legacy Input Manager (LMB fire, R reload).
    /// All presentation hooks are optional UnityEvents.
    /// </summary>
    public class HitscanWeapon : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Transform muzzle;
        [Tooltip("Shooter root; colliders under it are ignored. Defaults to transform.root.")]
        [SerializeField] private Transform owner;
        [Tooltip("Optional. If set (or found in parents), a dead owner cannot shoot.")]
        [SerializeField] private Health ownerHealth;

        [Header("Shot")]
        [SerializeField] private float damage = 25f;
        [SerializeField] private float range = 60f;
        [SerializeField] private float roundsPerMinute = 450f;
        [SerializeField] private bool automatic = true;
        [SerializeField] private LayerMask hitMask = ~(1 << 2); // everything except Ignore Raycast

        [Header("Ammo")]
        [SerializeField] private int magazineSize = 12;
        [SerializeField] private int startingReserve = 48;
        [SerializeField] private float reloadDuration = 1.6f;
        [SerializeField] private bool autoReloadWhenEmpty = true;

        [Header("Input (legacy Input Manager)")]
        [SerializeField] private KeyCode reloadKey = KeyCode.R;

        [Header("Sound event")]
        [Tooltip("Hearing budget a gunshot carries (graph cost units).")]
        [SerializeField] private float shotLoudness = 45f;

        [Header("Optional presentation hooks")]
        public UnityEvent OnFired;                       // muzzle flash, shot audio, recoil
        public UnityEvent<Vector3, Vector3> OnImpact;    // point, normal
        public UnityEvent OnDryFire;
        public UnityEvent OnReloadStarted;               // reload animation
        public UnityEvent OnReloadCompleted;
        public UnityEvent<int, int> OnAmmoChanged;       // magazine, reserve (HUD)

        public AmmoMagazine Ammo { get; private set; }
        public bool IsReloading { get; private set; }

        private float nextFireTime;
        private float reloadEndTime;
        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

        private void Awake()
        {
            if (owner == null) owner = transform.root;
            if (ownerHealth == null) ownerHealth = GetComponentInParent<Health>();
            if (aimCamera == null) aimCamera = GetComponentInParent<Camera>();
            Ammo = new AmmoMagazine(magazineSize, startingReserve);
        }

        private void OnEnable()
        {
            // Restart-safe: fresh timers each time the weapon is enabled.
            IsReloading = false;
            nextFireTime = 0f;
            NotifyAmmo();
        }

        private void OnDisable() => IsReloading = false; // cancels any in-progress reload

        private bool CanAct => (ownerHealth == null || !ownerHealth.IsDead) && Time.timeScale > 0f;

        private void Update()
        {
            if (!CanAct) return;

            if (IsReloading)
            {
                if (Time.time >= reloadEndTime) FinishReload();
                return; // no firing during reload
            }

            if (Input.GetKeyDown(reloadKey)) TryStartReload();

            bool trigger = automatic ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
            if (trigger) TryFire();
        }

        public bool TryStartReload()
        {
            if (!CanAct || IsReloading || !Ammo.CanReload) return false;
            IsReloading = true;
            reloadEndTime = Time.time + reloadDuration;
            OnReloadStarted?.Invoke();
            return true;
        }

        private void FinishReload()
        {
            IsReloading = false;
            Ammo.Reload();
            NotifyAmmo();
            OnReloadCompleted?.Invoke();
        }

        public bool TryFire()
        {
            if (!CanAct || IsReloading || Time.time < nextFireTime) return false;

            if (!Ammo.CanFire)
            {
                nextFireTime = Time.time + 0.25f; // dry-fire click cooldown
                OnDryFire?.Invoke();
                if (autoReloadWhenEmpty) TryStartReload();
                return false;
            }

            float interval = 60f / Mathf.Max(1f, roundsPerMinute);
            // Keep cadence stable across frame rates; never bank more than one shot.
            nextFireTime = Time.time - nextFireTime > interval ? Time.time + interval : nextFireTime + interval;

            Ammo.TryConsume();
            NotifyAmmo();
            Shoot();
            return true;
        }

        private void Shoot()
        {
            Transform cam = aimCamera != null ? aimCamera.transform : transform;
            Vector3 muzzlePos = muzzle != null ? muzzle.position : cam.position;

            // 1) Intended point from the camera ray.
            Vector3 target = FirstValidHit(cam.position, cam.forward, range, out RaycastHit camHit)
                ? camHit.point
                : cam.position + cam.forward * range;

            // 2) Obstruction check from the muzzle toward that point.
            Vector3 toTarget = target - muzzlePos;
            float dist = toTarget.magnitude;
            bool hit = false;
            RaycastHit finalHit = default;
            if (dist > 0.001f)
                hit = FirstValidHit(muzzlePos, toTarget / dist, dist + 0.05f, out finalHit);

            // The shot is audible whether or not it hits anything.
            SoundEventBus.Emit(muzzlePos, shotLoudness, SoundCategory.Gunshot, owner.gameObject.GetHashCode());
            OnFired?.Invoke();

            if (!hit) return;

            // Damage once per shot, resolved via parent so child colliders work.
            IDamageable target0 = finalHit.collider.GetComponentInParent<IDamageable>();
            target0?.TakeDamage(new DamageInfo(damage, finalHit.point, (finalHit.point - muzzlePos).normalized, owner.gameObject));
            OnImpact?.Invoke(finalHit.point, finalHit.normal);
        }

        /// <summary>Nearest hit that is not the shooter's own collider or a trigger.</summary>
        private bool FirstValidHit(Vector3 origin, Vector3 dir, float maxDist, out RaycastHit best)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, hitBuffer, maxDist, hitMask, QueryTriggerInteraction.Ignore);
            best = default;
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = hitBuffer[i];
                if (owner != null && h.collider.transform.IsChildOf(owner)) continue;
                if (h.distance < bestDist) { bestDist = h.distance; best = h; found = true; }
            }
            return found;
        }

        private void NotifyAmmo() => OnAmmoChanged?.Invoke(Ammo.InMagazine, Ammo.Reserve);
    }
}
