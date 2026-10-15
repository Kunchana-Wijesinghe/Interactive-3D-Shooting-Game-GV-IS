using UnityEngine;

namespace FacilityBreach.Player
{
    // Temporary boundary until Bandara's controller is available. No teammate movement is replaced.
    [RequireComponent(typeof(CharacterController))]
    public sealed class TemporaryPlayerAdapter : MonoBehaviour
    {
        public Camera View;
        public bool InputEnabled;
        public float WalkSpeed = 4, SprintSpeed = 6, Sensitivity = 2;
        private CharacterController body;
        private float pitch, vertical;
        private bool releaseInputs = true;
        public void ClearInputs() { releaseInputs = true; vertical = 0; }
        private void Awake() { body = GetComponent<CharacterController>(); }
        private void Update()
        {
            if (!InputEnabled || Cursor.lockState != CursorLockMode.Locked) { vertical = 0; return; }
            if (releaseInputs)
            {
                if (!Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.A) && !Input.GetKey(KeyCode.S) && !Input.GetKey(KeyCode.D) && !Input.GetKey(KeyCode.LeftShift)) releaseInputs = false;
                return;
            }
            transform.Rotate(0, Input.GetAxis("Mouse X") * Sensitivity, 0);
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * Sensitivity, -80, 80);
            View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            Vector3 move = Vector3.ClampMagnitude(transform.right * Input.GetAxisRaw("Horizontal") + transform.forward * Input.GetAxisRaw("Vertical"), 1);
            vertical = body.isGrounded ? -2 : vertical + Physics.gravity.y * Time.deltaTime;
            body.Move((move * (Input.GetKey(KeyCode.LeftShift) ? SprintSpeed : WalkSpeed) + Vector3.up * vertical) * Time.deltaTime);
        }
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var rigid = hit.collider.attachedRigidbody;
            if (rigid && !rigid.isKinematic && hit.moveDirection.y > -0.3f)
                rigid.AddForce(new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z) * 6, ForceMode.Force);
        }
    }
}
