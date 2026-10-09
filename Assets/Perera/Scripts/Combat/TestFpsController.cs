using UnityEngine;

namespace Perera.Combat
{
    /// <summary>
    /// ISOLATED TEST SUBSTITUTE for Bandara's shared player/movement controller.
    /// Used only in the Perera test scene. Replace with the shared controller on integration.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class TestFpsController : MonoBehaviour
    {
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float lookSensitivity = 2f;

        private CharacterController cc;
        private float pitch;
        private float verticalVelocity;

        private void Awake()
        {
            cc = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;

            transform.Rotate(0f, Input.GetAxis("Mouse X") * lookSensitivity, 0f);
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * lookSensitivity, -85f, 85f);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            Vector3 move = (transform.right * Input.GetAxis("Horizontal") + transform.forward * Input.GetAxis("Vertical")) * moveSpeed;
            verticalVelocity = cc.isGrounded ? -1f : verticalVelocity - 20f * Time.deltaTime;
            move.y = verticalVelocity;
            cc.Move(move * Time.deltaTime);
        }
    }
}
