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

    void Start()
    {
        closedRotation = transform.localRotation;

        openRotation =
            closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    void Update()
    {
        if (playerNearby && Input.GetKeyDown(KeyCode.E))
        {
            isOpen = !isOpen;
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