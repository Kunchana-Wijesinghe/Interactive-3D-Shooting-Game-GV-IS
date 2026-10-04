using UnityEngine;

/// <summary>
/// GV Part 2: Player Interaction Physics - Movable Barricade.
/// A heavy physical obstacle that the player can push, pull, and maneuver through E-key interaction.
/// Uses Rigidbody physics with Continuous collision detection so it collides with the player,
/// walls, and floor without tunneling or clipping through solid geometry.
/// Reusable for both isolated test scenes and full facility levels.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class MovableBarricade : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("Maximum distance from player to initiate interaction.")]
    public float interactionDistance = 3.0f;

    [Tooltip("Target distance to maintain in front of player when held/pushed.")]
    public float holdDistance = 2.0f;

    [Tooltip("Speed at which the barricade follows the target position.")]
    public float followSpeed = 8.0f;

    [Tooltip("Maximum distance before the hold breaks automatically (e.g. when blocked by walls).")]
    public float maxHoldDistance = 3.8f;

    [Header("Physics Tuning")]
    [Tooltip("Mass of the barricade in kg.")]
    public float mass = 60.0f;

    [Tooltip("Impulse force applied on discrete push.")]
    public float pushImpulse = 15.0f;

    [Tooltip("Linear damping when free-moving.")]
    public float linearDamping = 5.0f;

    [Tooltip("Angular damping to prevent unwanted spinning.")]
    public float angularDamping = 8.0f;

    [Header("Feedback")]
    public AudioSource audioSource;
    public AudioClip grabSound;
    public AudioClip releaseSound;

    private Rigidbody rb;
    private Transform holdingPlayer = null;
    private bool isHeld = false;
    private float lastInteractionTime = -10f;
    private const float INTERACTION_COOLDOWN = 0.25f;

    public bool IsHeld => isHeld;
    public Transform HoldingPlayer => holdingPlayer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ConfigurePhysics();
    }

    /// <summary>
    /// Configures the Rigidbody for stable, grounded, non-tunneling barricade physics.
    /// </summary>
    public void ConfigurePhysics()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.mass = mass;

#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
#else
        rb.drag = linearDamping;
        rb.angularDrag = angularDamping;
#endif

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Keep upright: freeze rotation on X and Z axes
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;
    }

    void FixedUpdate()
    {
        if (isHeld && holdingPlayer != null)
        {
            UpdateHoldPhysics();
        }
    }

    /// <summary>
    /// Smoothly guides the barricade toward the target position in front of the player
    /// using velocity rather than teleportation, allowing wall collisions to halt the movement naturally.
    /// </summary>
    private void UpdateHoldPhysics()
    {
        float currentDist = Vector3.Distance(transform.position, holdingPlayer.position);
        if (currentDist > maxHoldDistance)
        {
            // Player moved too far away (e.g. barricade got caught on a wall)
            Release();
            return;
        }

        // Target position: horizontal projection in front of player
        Vector3 playerForward = holdingPlayer.forward;
        playerForward.y = 0;
        playerForward.Normalize();

        Vector3 targetPos = holdingPlayer.position + playerForward * holdDistance;
        targetPos.y = transform.position.y; // Maintain ground height

        Vector3 toTarget = targetPos - rb.position;
        toTarget.y = 0; // Don't alter vertical gravity

        // Apply horizontal velocity toward target position
        Vector3 targetVelocity = toTarget * followSpeed;

#if UNITY_6000_0_OR_NEWER
        Vector3 currentVel = rb.linearVelocity;
        targetVelocity.y = currentVel.y; // Maintain gravity
        rb.linearVelocity = targetVelocity;
#else
        Vector3 currentVel = rb.velocity;
        targetVelocity.y = currentVel.y;
        rb.velocity = targetVelocity;
#endif

        // Align barricade facing with player's look direction
        Quaternion targetRotation = Quaternion.LookRotation(playerForward, Vector3.up);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * followSpeed));
    }

    /// <summary>
    /// Interaction entry point: toggles grab / release when player presses E.
    /// </summary>
    public bool Interact(Transform playerTransform)
    {
        if (Time.time - lastInteractionTime < INTERACTION_COOLDOWN) return false;
        lastInteractionTime = Time.time;

        if (isHeld)
        {
            Release();
            return true;
        }
        else
        {
            float dist = GetDistanceToPlayer(playerTransform.position);
            if (dist <= interactionDistance)
            {
                Grab(playerTransform);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Grabs the barricade, putting it in push/pull maneuver mode.
    /// </summary>
    public void Grab(Transform playerTransform)
    {
        holdingPlayer = playerTransform;
        isHeld = true;

        if (audioSource != null && grabSound != null)
        {
            audioSource.PlayOneShot(grabSound);
        }
    }

    /// <summary>
    /// Releases the barricade from player control.
    /// </summary>
    public void Release()
    {
        holdingPlayer = null;
        isHeld = false;

        // Dampen residual velocity on release
        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.2f, rb.linearVelocity.y, rb.linearVelocity.z * 0.2f);
#else
            rb.velocity = new Vector3(rb.velocity.x * 0.2f, rb.velocity.y, rb.velocity.z * 0.2f);
#endif
        }

        if (audioSource != null && releaseSound != null)
        {
            audioSource.PlayOneShot(releaseSound);
        }
    }

    /// <summary>
    /// Shoves the barricade with an impulse force.
    /// </summary>
    public void PushImpulse(Vector3 direction)
    {
        if (rb == null) return;
        direction.y = 0;
        direction.Normalize();
        rb.AddForce(direction * pushImpulse, ForceMode.Impulse);
    }

    /// <summary>
    /// Calculates the closest distance from the player position to the barricade collider.
    /// </summary>
    public float GetDistanceToPlayer(Vector3 playerPosition)
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Vector3 closest = col.ClosestPoint(playerPosition);
            return Vector3.Distance(closest, playerPosition);
        }
        return Vector3.Distance(transform.position, playerPosition);
    }

    public bool IsInRange(Vector3 position)
    {
        return GetDistanceToPlayer(position) <= interactionDistance;
    }
}
