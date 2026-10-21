using System.Collections.Generic;
using UnityEngine;

namespace FacilityBreach.Combat
{
    /// <summary>
    /// Visual-only weapon presentation for Patrol Guard, Cover Defender and Flanker: an optional rifle attached to the
    /// animated right-arm bone plus a short muzzle flash when a shot resolves. Never touches combat, movement or decisions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyWeaponEffects : MonoBehaviour
    {
        private readonly List<Material> materials = new List<Material>();
        private GameObject flashBody;
        private float flashRemaining;
        public Transform Muzzle { get; private set; }

        /// <summary>Registers the rifle tip and builds a hidden flash (glowing sphere and a point light) on it.</summary>
        public void SetMuzzle(Transform muzzle, Color flashColor, float size)
        {
            Muzzle = muzzle;
            flashBody = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flashBody.name = "Muzzle flash"; Destroy(flashBody.GetComponent<Collider>());
            flashBody.transform.SetParent(muzzle, false); flashBody.transform.localScale = Vector3.one * .14f * size;
            var material = new Material(Shader.Find("Standard")) { color = flashColor };
            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", flashColor * 2f);
            materials.Add(material); flashBody.GetComponent<Renderer>().sharedMaterial = material;
            var flashLight = flashBody.AddComponent<Light>(); flashLight.color = flashColor; flashLight.range = 5; flashLight.intensity = 2.5f;
            flashBody.SetActive(false);
        }

        /// <summary>Builds a rifle in the model's right hand and registers its muzzle. Returns false if the rig is unrecognised.</summary>
        public bool BuildRifle(GameObject visual, Color bodyColor, Color glowColor, Color flashColor)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (var t in visual.GetComponentsInChildren<Transform>(true))
                if (!t.GetComponent<Renderer>()) bones[t.name.ToLowerInvariant()] = t; // real bones only
            Renderer head = null, hand = null, visor = null; Bounds all = default; bool first = true;
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "Head") head = r; else if (r.name == "RHand") hand = r; else if (r.name == "Visor") visor = r;
                if (first) { all = r.bounds; first = false; } else all.Encapsulate(r.bounds);
            }
            if (!head || !hand || !bones.TryGetValue("rarm", out var bone)) { Debug.LogWarning("Enemy rifle skipped: rig parts not found", this); return false; }
            float scale = Mathf.Max(.1f, all.size.y / 1.7f);
            Vector3 forward = transform.forward;
            if (visor) { var toVisor = visor.bounds.center - head.bounds.center; toVisor.y = 0; if (toVisor.sqrMagnitude > 1e-6f) forward = toVisor.normalized; }
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 At(Vector3 o, float r, float u, float f) => o + (right * r + Vector3.up * u + forward * f) * scale;
            Material Mat(Color c, bool glow)
            {
                var m = new Material(Shader.Find("Standard")) { color = c };
                if (glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 2f); }
                materials.Add(m); return m;
            }
            void Part(PrimitiveType type, Vector3 position, Vector3 size, Color color, bool glow = false, float pitch = 0)
            {
                var part = GameObject.CreatePrimitive(type);
                part.name = "Rifle part"; Destroy(part.GetComponent<Collider>());
                part.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(pitch, 0, 0));
                part.transform.localScale = size * scale;
                part.GetComponent<Renderer>().sharedMaterial = Mat(color, glow);
                part.transform.SetParent(bone, true);
            }
            Vector3 c0 = At(hand.bounds.center, -.1f, .05f, .26f);
            Part(PrimitiveType.Cube, c0, new Vector3(.07f, .1f, .4f), bodyColor);
            Part(PrimitiveType.Cube, At(c0, 0, 0, .22f), new Vector3(.06f, .07f, .26f), bodyColor);
            Part(PrimitiveType.Cylinder, At(c0, 0, 0, .45f), new Vector3(.035f, .14f, .035f), bodyColor, false, 90);
            Part(PrimitiveType.Cube, At(c0, 0, -.11f, .02f), new Vector3(.045f, .15f, .06f), bodyColor);
            Part(PrimitiveType.Cube, At(c0, 0, -.02f, -.26f), new Vector3(.055f, .1f, .16f), bodyColor);
            Part(PrimitiveType.Cube, At(c0, .037f, .01f, .06f), new Vector3(.01f, .03f, .2f), glowColor, true);
            Part(PrimitiveType.Cube, At(c0, -.037f, .01f, .06f), new Vector3(.01f, .03f, .2f), glowColor, true);
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetPositionAndRotation(At(c0, 0, 0, .6f), Quaternion.LookRotation(forward, Vector3.up));
            muzzle.transform.SetParent(bone, true);
            SetMuzzle(muzzle.transform, flashColor, 1f);
            return true;
        }

        /// <summary>Shows a short muzzle flash when this enemy's shot resolves.</summary>
        public void NotifyShot() { flashRemaining = .06f; if (flashBody) flashBody.SetActive(true); }

        private void Update()
        {
            if (flashRemaining <= 0) return;
            flashRemaining -= Time.deltaTime;
            if (flashRemaining <= 0 && flashBody) flashBody.SetActive(false);
        }

        private void OnDestroy() { foreach (var m in materials) if (m) Destroy(m); }
    }
}
