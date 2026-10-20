using FacilityBreach.Audio;
using FacilityBreach.Noise;
using FacilityBreach.Physics;
using UnityEngine;

namespace FacilityBreach.Player
{
    /// <summary>
    /// GV Part 2: First-Person Kick Controller.
    /// Implements camera-directed short-range physics kick interaction (F key).
    /// Applies capped directional impulse with upward loft to KickablePhysicsProp objects.
    /// Weapon viewmodels and tactical arms remain fully visible and uninterrupted.
    /// </summary>
    public sealed class FpsKickController : MonoBehaviour
    {
        [Header("Kick Configuration")]
        [SerializeField] private float kickReach = 2.2f;
        [SerializeField] private float kickRadius = 0.35f;
        [SerializeField] private float kickImpulse = 14.0f;
        [SerializeField] private float upwardLoft = 3.5f;
        [SerializeField] private float kickCooldown = 0.45f;
        [SerializeField] private float kickNoiseRadius = 12.0f;

        private Camera playerCamera;
        private FacilityBreachRuntime runtime;
        private float nextKickTime;

        public float KickReach => kickReach;
        public float KickImpulse => kickImpulse;
        public float UpwardLoft => upwardLoft;
        public float KickCooldown => kickCooldown;
        public float NextKickTime => nextKickTime;

        public void Initialize(FacilityBreachRuntime runtimeRef, Camera cameraRef)
        {
            runtime = runtimeRef;
            playerCamera = cameraRef;
        }

        private void Update()
        {
            if (runtime != null && !runtime.AcceptsGameplay) return;

            if (Input.GetKeyDown(KeyCode.F))
            {
                TryKick();
            }
        }

        /// <summary>
        /// Attempts to execute a kick forward from the player's camera.
        /// </summary>
        public bool TryKick()
        {
            if (Time.time < nextKickTime) return false;

            Camera cam = playerCamera != null ? playerCamera : Camera.main;
            if (cam == null) return false;

            Vector3 origin = cam.transform.position;
            Vector3 forward = cam.transform.forward;

            // 1. Detect candidate prop using sphere-cast within reach
            RaycastHit candidateHit;
            bool hitSomething = UnityEngine.Physics.SphereCast(
                origin,
                kickRadius,
                forward,
                out candidateHit,
                kickReach,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            if (!hitSomething || candidateHit.collider == null) return false;

            // Ignore self / player colliders
            if (candidateHit.collider.transform.IsChildOf(transform)) return false;

            // 2. Validate target has KickablePhysicsProp component
            var prop = candidateHit.collider.GetComponentInParent<KickablePhysicsProp>();
            if (prop == null || prop.Body == null) return false;

            // 3. Occlusion line-of-sight test: ensure no solid wall or barricade blocks the kick
            Vector3 hitPoint = candidateHit.point;
            Vector3 toTarget = hitPoint - origin;
            float dist = toTarget.magnitude;

            if (dist > 0.05f && UnityEngine.Physics.Raycast(origin, toTarget.normalized, out RaycastHit occluderHit, dist - 0.05f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!occluderHit.collider.transform.IsChildOf(prop.transform) && !occluderHit.collider.transform.IsChildOf(transform))
                {
                    // Blocked by solid wall / cover
                    return false;
                }
            }

            // 4. Calculate directional impulse with upward loft
            Vector3 horizontalDir = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (horizontalDir.sqrMagnitude < 0.01f) horizontalDir = forward;

            Vector3 finalImpulse = (horizontalDir * kickImpulse) + (Vector3.up * upwardLoft);

            // 5. Apply physics kick to the Rigidbody
            prop.ApplyKick(finalImpulse, hitPoint);

            // 6. Play kick thud sound and emit neutral noise event
            FacilityAudioPlayer.Instance.PlayKick(hitPoint);
            FacilityNoiseHub.Emit(transform.position, kickNoiseRadius, FacilityNoiseType.Kick, gameObject);

            // 7. Update cooldown
            nextKickTime = Time.time + kickCooldown;

            if (runtime != null)
            {
                runtime.Say($"Kicked {prop.name}");
            }

            return true;
        }

        /// <summary>
        /// Returns true if a valid kickable prop is currently in range and aim.
        /// </summary>
        public bool HasKickableTarget(out string targetName)
        {
            targetName = null;
            Camera cam = playerCamera != null ? playerCamera : Camera.main;
            if (cam == null) return false;

            if (UnityEngine.Physics.SphereCast(cam.transform.position, kickRadius, cam.transform.forward, out RaycastHit hit, kickReach, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && !hit.collider.transform.IsChildOf(transform))
                {
                    var prop = hit.collider.GetComponentInParent<KickablePhysicsProp>();
                    if (prop != null)
                    {
                        targetName = prop.name;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
