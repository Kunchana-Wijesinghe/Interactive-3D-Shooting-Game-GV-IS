using System;
using UnityEngine;
using FacilityBreach.Interactions;

namespace FacilityBreach
{
    /// <summary>
    /// Procedural first-person view-model obstruction and wall-clipping prevention controller.
    /// When the player approaches a wall or solid environment geometry in close-quarters (CQB),
    /// this component detects proximity via forward-facing sphere and ray queries and smoothly
    /// retracts and lowers the equipped weapon model into a compressed low-ready tactical stance.
    ///
    /// Aiming, crosshair alignment, and hitscan bullet collision origin remain unaltered.
    /// </summary>
    [DisallowMultipleComponent]
    public class FpsViewModelObstruction : MonoBehaviour
    {
        [Header("Obstruction Detection")]
        [Tooltip("Radius of the volumetric forward probe matching typical weapon receiver/barrel radius.")]
        [SerializeField] private float sphereRadius = 0.08f;

        [Tooltip("Forward cushion beyond the muzzle to initiate retraction before physical visual contact.")]
        [SerializeField] private float forwardCushion = 0.08f;

        [Tooltip("Minimum distance from camera where maximum retraction is reached.")]
        [SerializeField] private float minClearance = 0.18f;

        [Tooltip("Collision mask for solid environment obstacles.")]
        [SerializeField] private LayerMask collisionMask = ~0;

        [Header("Tactical Low-Ready Posing")]
        [Tooltip("Maximum distance the view-model can slide backward along camera local Z.")]
        [SerializeField] private float maxRetractZ = 0.28f;

        [Tooltip("Maximum distance the view-model lowers along camera local Y.")]
        [SerializeField] private float maxLowerY = 0.16f;

        [Tooltip("Maximum downward pitch tilt angle (degrees) in compressed low-ready.")]
        [SerializeField] private float maxPitchAngle = 32f;

        [Tooltip("Maximum inward yaw tilt angle (degrees) toward player center.")]
        [SerializeField] private float maxYawAngle = 8f;

        [Tooltip("Interpolation smoothing speed for retraction and recovery.")]
        [SerializeField] private float smoothSpeed = 14f;

        // References
        private Camera aimCamera;
        private Transform playerRoot;
        private Transform weaponTransform;
        private Transform muzzleTransform;

        // Baseline View-Model Pose
        private Vector3 defaultLocalPosition = new Vector3(0.18f, -0.22f, 0.5f);
        private Quaternion defaultLocalRotation = Quaternion.identity;

        // Dynamic State
        private float weaponReach = 0.95f;
        private bool isObstructed;
        private float currentObstructionDistance = float.PositiveInfinity;
        private Vector3 targetLocalPosition;
        private Quaternion targetLocalRotation;

        public bool IsObstructed => isObstructed;
        public float CurrentObstructionDistance => currentObstructionDistance;
        public float WeaponReach => weaponReach;
        public Vector3 DefaultLocalPosition => defaultLocalPosition;
        public Quaternion DefaultLocalRotation => defaultLocalRotation;
        public Vector3 TargetLocalPosition => targetLocalPosition;
        public Quaternion TargetLocalRotation => targetLocalRotation;
        public float SmoothSpeed { get => smoothSpeed; set => smoothSpeed = value; }
        public float MaxRetractZ { get => maxRetractZ; set => maxRetractZ = value; }

        /// <summary>
        /// Initializes the obstruction controller with current camera, player, and weapon view-model references.
        /// Re-called whenever a weapon swap occurs.
        /// </summary>
        public void Initialize(Camera cam, Transform player, Transform weapon, Transform muzzle)
        {
            aimCamera = cam;
            playerRoot = player;
            weaponTransform = weapon;
            muzzleTransform = muzzle;

            if (weaponTransform != null)
            {
                defaultLocalPosition = weaponTransform.localPosition;
                defaultLocalRotation = weaponTransform.localRotation;
                targetLocalPosition = defaultLocalPosition;
                targetLocalRotation = defaultLocalRotation;

                // Calibrate forward reach based on authored muzzle position in camera space
                if (muzzleTransform != null && aimCamera != null)
                {
                    Vector3 localMuzzle = aimCamera.transform.InverseTransformPoint(muzzleTransform.position);
                    weaponReach = Mathf.Max(0.70f, localMuzzle.z);
                }
                else
                {
                    weaponReach = 0.95f;
                }
            }

            isObstructed = false;
            currentObstructionDistance = float.PositiveInfinity;
        }

        private void LateUpdate()
        {
            if (!weaponTransform || !aimCamera) return;

            if (CheckObstruction(out var hit, out float distance))
            {
                isObstructed = true;
                currentObstructionDistance = distance;
                CalculateTargetPose(distance, out targetLocalPosition, out targetLocalRotation);
            }
            else
            {
                isObstructed = false;
                currentObstructionDistance = float.PositiveInfinity;
                targetLocalPosition = defaultLocalPosition;
                targetLocalRotation = defaultLocalRotation;
            }

            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                weaponTransform.localPosition = Vector3.Lerp(weaponTransform.localPosition, targetLocalPosition, dt * smoothSpeed);
                weaponTransform.localRotation = Quaternion.Slerp(weaponTransform.localRotation, targetLocalRotation, dt * smoothSpeed);
            }
            else
            {
                weaponTransform.localPosition = targetLocalPosition;
                weaponTransform.localRotation = targetLocalRotation;
            }
        }

        /// <summary>
        /// Probes forward from the camera for solid environment obstacles within weapon reach.
        /// Combines SphereCast (for volumetric barrel cylinder) with Raycast (for close-range points).
        /// </summary>
        public bool CheckObstruction(out RaycastHit bestHit, out float bestDistance)
        {
            bestHit = default;
            bestDistance = float.PositiveInfinity;
            if (!aimCamera || !weaponTransform) return false;

            Vector3 origin = aimCamera.transform.position;
            Vector3 direction = aimCamera.transform.forward;
            float maxQueryDistance = weaponReach + forwardCushion;

            // 1. Volumetric SphereCast check
            var sphereHits = Physics.SphereCastAll(origin, sphereRadius, direction, maxQueryDistance, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < sphereHits.Length; i++)
            {
                var hit = sphereHits[i];
                if (IsValidObstacle(hit.collider) && hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestHit = hit;
                }
            }

            // 2. Direct raycast fallback for acute edges
            var rayHits = Physics.RaycastAll(origin, direction, maxQueryDistance, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < rayHits.Length; i++)
            {
                var hit = rayHits[i];
                if (IsValidObstacle(hit.collider) && hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestHit = hit;
                }
            }

            return bestDistance < maxQueryDistance;
        }

        /// <summary>
        /// Validates that a detected collider is an actual environment obstacle and not the player,
        /// the weapon visual itself, a trigger volume, or an interactable station.
        /// </summary>
        public bool IsValidObstacle(Collider col)
        {
            if (col == null) return false;
            if (col.isTrigger) return false;
            if (playerRoot != null && col.transform.IsChildOf(playerRoot)) return false;
            if (weaponTransform != null && col.transform.IsChildOf(weaponTransform)) return false;
            if (col.GetComponentInParent<ResourceStation>() != null) return false;

            // If a barricade is actively held by the player, it follows ahead and shouldn't trigger visual retraction
            var barricade = col.GetComponentInParent<MovableBarricade>();
            if (barricade != null && barricade.IsHeld) return false;

            return true;
        }

        /// <summary>
        /// Pure analytical calculation of the target view-model pose given an obstacle distance.
        /// Exposed for deterministic unit testing and runtime pose calculation.
        /// </summary>
        public void CalculateTargetPose(float hitDistance, out Vector3 targetPos, out Quaternion targetRot)
        {
            float effectiveReach = weaponReach + forwardCushion;
            if (hitDistance >= effectiveReach || float.IsPositiveInfinity(hitDistance) || hitDistance < 0f)
            {
                targetPos = defaultLocalPosition;
                targetRot = defaultLocalRotation;
                return;
            }

            float penetration = effectiveReach - hitDistance;
            float maxPenetration = effectiveReach - minClearance;
            float t = Mathf.Clamp01(penetration / Mathf.Max(0.01f, maxPenetration));

            // Procedural low-ready offsets
            float zOffset = -Mathf.Min(penetration * 0.75f, maxRetractZ);
            float yOffset = -t * maxLowerY;
            float pitchAngle = t * maxPitchAngle;
            float yawAngle = -t * maxYawAngle;

            targetPos = defaultLocalPosition + new Vector3(0f, yOffset, zOffset);
            targetRot = defaultLocalRotation * Quaternion.Euler(pitchAngle, yawAngle, -yawAngle * 0.4f);
        }
    }
}
