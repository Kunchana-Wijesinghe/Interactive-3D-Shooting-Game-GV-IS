using UnityEngine;

namespace Perera.SoundHunter
{
    /// <summary>Test-only: keeps a world-space TextMesh label facing the main camera.</summary>
    public class BillboardLabel : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
