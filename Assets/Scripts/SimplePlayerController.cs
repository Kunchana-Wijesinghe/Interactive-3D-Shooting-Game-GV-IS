using UnityEngine;

/// <summary>
/// GV Part 2: Player Interaction Physics - Temporary Test Player Controller.
/// Provides first-person movement, camera look, cursor locking, and handles
/// interaction physics with doors and movable barricades.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class SimplePlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5.0f;
    public float gravity = 20.0f;

    [Header("Look")]
    public Camera playerCamera;
    public float mouseSensitivity = 2.0f;
    public float maxLookAngle = 80.0f;

    [Header("Physics Interaction")]
    [Tooltip("Push force applied when physically colliding with non-kinematic Rigidbodies.")]
    public float physicalPushPower = 40.0f;

    [Header("Interaction Settings")]
    public float interactionRangeHint = 3.0f;

    private CharacterController characterController;
    private float verticalVelocity = 0.0f;
    private float cameraPitch = 0.0f;
    private bool cursorLocked = true;

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        SetCursorLock(true);
    }

    void Update()
    {
        HandleCursorLock();

        if (cursorLocked)
        {
            HandleRotation();
        }

        HandleMovement();
        HandleInteraction();
    }

    private void HandleCursorLock()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorLock(!cursorLocked);
        }
        else if (Input.GetMouseButtonDown(0) && !cursorLocked)
        {
            SetCursorLock(true);
        }
    }

    private void SetCursorLock(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void HandleRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Yaw: Rotate player body
        transform.Rotate(Vector3.up * mouseX);

        // Pitch: Tilt camera
        if (playerCamera != null)
        {
            cameraPitch -= mouseY;
            cameraPitch = Mathf.Clamp(cameraPitch, -maxLookAngle, maxLookAngle);
            playerCamera.transform.localEulerAngles = new Vector3(cameraPitch, 0f, 0f);
        }
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 moveDirection = transform.forward * vertical + transform.right * horizontal;
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1.0f);

        if (characterController.isGrounded)
        {
            verticalVelocity = -0.5f; // Grounding bias
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = verticalVelocity;

        characterController.Move(velocity * Time.deltaTime);
    }

    private void HandleInteraction()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            // Barricade interaction: toggle grab/push
            MovableBarricade barricade = FindAnyObjectByType<MovableBarricade>();
            if (barricade != null)
            {
                if (barricade.IsHeld)
                {
                    barricade.Release();
                }
                else if (barricade.IsInRange(transform.position))
                {
                    barricade.Interact(transform);
                }
            }
        }
    }

    /// <summary>
    /// Unity physics callback: allows the CharacterController to physically push non-kinematic Rigidbodies
    /// when walking against them, while being stopped by their mass and colliders.
    /// </summary>
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;

        // Don't push objects below player feet
        if (hit.moveDirection.y < -0.3f) return;

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
        if (pushDir.sqrMagnitude < 0.001f) return;

        body.AddForce(pushDir.normalized * physicalPushPower, ForceMode.Force);
    }

    void OnGUI()
    {
        GUIStyle headerStyle = new GUIStyle(GUI.skin.box);
        headerStyle.alignment = TextAnchor.UpperLeft;
        headerStyle.fontSize = 13;
        headerStyle.normal.textColor = Color.white;

        GUILayout.BeginArea(new Rect(10, 10, 420, 185), headerStyle);
        GUILayout.Label("<b>GV Part 2: Interaction Physics Test</b>");
        GUILayout.Label("• Movement: <b>W, A, S, D</b>");
        GUILayout.Label("• Look: <b>Mouse</b> (Press <b>ESC</b> to toggle cursor)");
        GUILayout.Label("• Interact: <b>E</b> (Approach object)");

        // Door Status Feedback
        InteractiveDoor door = FindAnyObjectByType<InteractiveDoor>();
        if (door != null)
        {
            float dist = Vector3.Distance(transform.position, door.transform.position);
            bool inRange = dist <= interactionRangeHint;
            string status = inRange
                ? "<color=#00FF88><b>IN RANGE</b> — Press [E] to Toggle Door</color>"
                : "<color=#FFAA00><b>OUT OF RANGE</b> (" + dist.ToString("F1") + "m away)</color>";
            GUILayout.Space(4);
            GUILayout.Label("Door: " + status);
        }

        // Barricade Status Feedback
        MovableBarricade barricade = FindAnyObjectByType<MovableBarricade>();
        if (barricade != null)
        {
            float dist = barricade.GetDistanceToPlayer(transform.position);
            string status;
            if (barricade.IsHeld)
            {
                status = "<color=#00FFFF><b>GRABBED / MANEUVERING</b> — Move to Push/Pull, Press [E] to Release</color>";
            }
            else if (dist <= barricade.interactionDistance)
            {
                status = "<color=#00FF88><b>IN RANGE</b> (" + dist.ToString("F1") + "m) — Press [E] to Grab & Push</color>";
            }
            else
            {
                status = "<color=#FFAA00><b>OUT OF RANGE</b> (" + dist.ToString("F1") + "m away)</color>";
            }
            GUILayout.Space(4);
            GUILayout.Label("Barricade: " + status);
        }

        GUILayout.EndArea();

        // Crosshair
        float size = 8f;
        float centerX = Screen.width / 2f;
        float centerY = Screen.height / 2f;
        GUI.Box(new Rect(centerX - size / 2, centerY - size / 2, size, size), GUIContent.none);
    }
}
