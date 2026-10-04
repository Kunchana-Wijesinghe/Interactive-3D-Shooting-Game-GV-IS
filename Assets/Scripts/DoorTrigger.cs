using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    public InteractiveDoor door;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (door != null)
                door.SetPlayerNearby(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (door != null)
                door.SetPlayerNearby(false);
        }
    }
}