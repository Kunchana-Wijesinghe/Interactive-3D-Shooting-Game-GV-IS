using FacilityBreach.Audio;
using FacilityBreach.Noise;
using UnityEngine;

namespace FacilityBreach.Player
{
    /// <summary>
    /// GV Part 2: First-Person Kick Controller.
    /// Implements camera-directed short-range physics kick interaction (F key).
    /// Applies capped directional impulse with upward loft to KickablePhysicsProp objects.
    /// Direct screen-center raycast takes priority, falling back to a focused candidate sweep.
    /// Weapon viewmodels and tactical arms remain fully visible and uninterrupted.
    /// </summary>
    public sealed class FpsKickController : MonoBehaviour
    {
        [Header("Kick Configuration")]
        [SerializeField] private float kickReach = 2.25f;
        [SerializeField] private float kickRadius = 0.20f;
        [SerializeField] private float kickImpulse = 18.0f;
        [SerializeField] private float upwardLoft = 2.8f;
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
        /// Unified target resolution used by both HUD prompt display and F-key execution.
        /// Evaluates direct screen-center raycast first, then falls back to a tight spherecast,
        /// strictly ignoring player colliders and enforcing solid wall occlusion.
        /// </summary>
        public bool FindKickTarget(Camera cam, out KickablePhysicsProp targetProp, out Vector3 targetHitPoint)
        {
            targetProp = null;
            targetHitPoint = Vector3.zero;

            if (cam == null) cam = playerCamera != null ? playerCamera : Camera.main;
            if (cam == null) return false;

            Vector3 origin = cam.transform.position;
            Vector3 forward = cam.transform.forward;

            // 1. Primary: Direct screen-center raycast (exact crosshair alignment)
            var directHits = UnityEngine.Physics.RaycastAll(origin, forward, kickReach, ~0, QueryTriggerInteraction.Ignore);
            if (directHits != null && directHits.Length > 0)
            {
                System.Array.Sort(directHits, (a, b) => a.distance.CompareTo(b.distance));

                for (int i = 0; i < directHits.Length; i++)
                {
                    var hit = directHits[i];
                    if (IsPlayerCollider(hit.collider)) continue;

                    var prop = hit.collider.GetComponentInParent<KickablePhysicsProp>();
                    if (prop != null && prop.Body != null)
                    {
                        targetProp = prop;
                        targetHitPoint = hit.point;
                        return true;
                    }

                    // A solid non-kickable collider (wall/door) was hit directly ahead; blocks ray
                    break;
                }
            }

            // 2. Secondary: Focused spherecast sweep for nearby candidates
            float sweepRadius = Mathf.Min(kickRadius, 0.22f);
            var sweepHits = UnityEngine.Physics.SphereCastAll(origin, sweepRadius, forward, kickReach, ~0, QueryTriggerInteraction.Ignore);
            if (sweepHits != null && sweepHits.Length > 0)
            {
                System.Array.Sort(sweepHits, (a, b) => a.distance.CompareTo(b.distance));

                for (int i = 0; i < sweepHits.Length; i++)
                {
                    var hit = sweepHits[i];
                    if (IsPlayerCollider(hit.collider)) continue;

                    var prop = hit.collider.GetComponentInParent<KickablePhysicsProp>();
                    if (prop == null || prop.Body == null) continue;

                    // Occlusion line-of-sight test: ensure no solid wall or closed door blocks line from camera to target
                    Vector3 testPoint = hit.point;
                    if (testPoint == Vector3.zero) testPoint = hit.collider.bounds.center;

                    Vector3 toTarget = testPoint - origin;
                    float dist = toTarget.magnitude;

                    if (dist > 0.1f)
                    {
                        var occluderHits = UnityEngine.Physics.RaycastAll(origin, toTarget.normalized, dist - 0.05f, ~0, QueryTriggerInteraction.Ignore);
                        bool isOccluded = false;
                        for (int k = 0; k < occluderHits.Length; k++)
                        {
                            var occ = occluderHits[k];
                            if (IsPlayerCollider(occ.collider)) continue;
                            if (occ.collider.transform.IsChildOf(prop.transform)) continue;

                            isOccluded = true;
                            break;
                        }
                        if (isOccluded) continue;
                    }

                    targetProp = prop;
                    targetHitPoint = testPoint;
                    return true;
                }
            }

            return false;
        }

        private bool IsPlayerCollider(Collider col)
        {
            if (col == null) return false;
            if (col.transform.IsChildOf(transform)) return true;
            if (col.CompareTag("Player")) return true;
            if (runtime != null && runtime.PlayerTransform != null && col.transform.IsChildOf(runtime.PlayerTransform)) return true;
            return false;
        }

        /// <summary>
        /// Attempts to execute a kick forward from the player's camera.
        /// </summary>
        public bool TryKick()
        {
            if (Time.time < nextKickTime) return false;

            Camera cam = playerCamera != null ? playerCamera : Camera.main;
            if (cam == null) return false;

            if (!FindKickTarget(cam, out KickablePhysicsProp prop, out Vector3 hitPoint))
            {
                return false;
            }

            // Calculate directional impulse with upward loft
            Vector3 forward = cam.transform.forward;
            Vector3 horizontalDir = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (horizontalDir.sqrMagnitude < 0.01f) horizontalDir = forward;

            Vector3 finalImpulse = (horizontalDir * kickImpulse) + (Vector3.up * upwardLoft);

            // Apply physics kick to the Rigidbody
            prop.ApplyKick(finalImpulse, hitPoint);

            // Play kick thud sound and emit neutral noise event
            FacilityAudioPlayer.Instance.PlayKick(hitPoint);
            FacilityNoiseHub.Emit(transform.position, kickNoiseRadius, FacilityNoiseType.Kick, gameObject);

            // Update cooldown
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

            if (FindKickTarget(cam, out var prop, out _))
            {
                targetName = prop.name;
                return true;
            }

            return false;
        }
    }
}
