using UnityEngine;

public class InteractiveDoor : MonoBehaviour
{
    [Header("Door Settings")]
    public float openAngle = 90f;
    public float openSpeed = 4f;

    private Quaternion closedRotation;
    private Quaternion openRotation;

    private bool isOpen = false;
    private bool playerNearby = false;

    public bool IsOpen => isOpen;

    // Shared adapter boundary: opening is safe; closing waits until characters clear.
    public bool RequestOpen(bool open)
    {
        if (!open)
        {
            foreach (Collider panel in GetComponentsInChildren<Collider>())
            {
                if (panel.isTrigger) continue;
                Bounds bounds = panel.bounds;
                foreach (Collider occupant in Physics.OverlapBox(bounds.center,
                    bounds.extents + Vector3.one * 0.15f, Quaternion.identity, ~0,
                    QueryTriggerInteraction.Ignore))
                {
                    if (occupant.transform.IsChildOf(transform)) continue;
                    if (occupant.GetComponentInParent<CharacterController>() ||
                        (occupant.attachedRigidbody && !occupant.attachedRigidbody.isKinematic))
                        return false;
                }
            }
        }
        isOpen = open;
        return true;
    }

    void Start()
    {
        closedRotation = transform.localRotation;

        openRotation =
            closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    void Update()
    {
        if (Time.timeScale > 0 && playerNearby && Input.GetKeyDown(KeyCode.E))
        {
            RequestOpen(!isOpen);
        }

        Quaternion target =
            isOpen ? openRotation : closedRotation;

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            target,
            openSpeed * Time.deltaTime
        );
    }

    public void SetPlayerNearby(bool nearby)
    {
        playerNearby = nearby;
    }
}
