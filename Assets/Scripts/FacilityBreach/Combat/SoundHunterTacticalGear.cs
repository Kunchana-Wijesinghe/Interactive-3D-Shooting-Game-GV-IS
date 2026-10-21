using System.Collections.Generic;
using UnityEngine;

namespace FacilityBreach.Combat
{
    /// <summary>
    /// Visual-only tactical outfit for SH-01: helmet, goggles, gas mask, plate carrier, backpack, knee pads and a rifle,
    /// with the former blue camo accents in orange. Gear is parented to the rig's bones so the team's clips move it.
    /// Never touches health, movement or decisions.
    /// </summary>
    public sealed class SoundHunterTacticalGear : MonoBehaviour
    {
        private static readonly Color Black = new Color(.07f, .07f, .08f);
        private static readonly Color Gray = new Color(.19f, .2f, .22f);
        private static readonly Color Orange = new Color(1f, .5f, 0f);
        private readonly List<Material> materials = new List<Material>();
        private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        private Vector3 forward, right;
        private float scale = 1;
        private Light flashLight;
        private GameObject flashBody;
        private float flashRemaining;
        public Transform Muzzle { get; private set; }

        public void Build(GameObject visual)
        {
            // Only real bones: the skinned mesh objects share names like "Head"/"Torso" but never move with the animation.
            foreach (var t in visual.GetComponentsInChildren<Transform>(true))
                if (!t.GetComponent<Renderer>()) bones[t.name.ToLowerInvariant()] = t;
            var parts = new Dictionary<string, Renderer>();
            Bounds all = default; bool first = true;
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                parts[r.name] = r;
                if (first) { all = r.bounds; first = false; } else all.Encapsulate(r.bounds);
            }
            if (!parts.ContainsKey("Head") || !parts.ContainsKey("Torso")) { Debug.LogWarning("SH-01 gear skipped: rig parts not found", this); return; }
            scale = Mathf.Max(.1f, all.size.y / 1.7f);
            forward = transform.forward;
            if (parts.TryGetValue("Visor", out var visor))
            {
                var toVisor = visor.bounds.center - parts["Head"].bounds.center; toVisor.y = 0;
                if (toVisor.sqrMagnitude > 1e-6f) forward = toVisor.normalized;
            }
            right = Vector3.Cross(Vector3.up, forward);
            Recolor(parts);
            BuildHead(parts["Head"].bounds.center);
            BuildTorso(parts["Torso"].bounds.center);
            foreach (var name in new[] { "LKnee", "RKnee" })
                if (parts.TryGetValue(name, out var knee))
                    Part("Knee pad", PrimitiveType.Cube, knee.bounds.center + forward * .06f * scale, new Vector3(.18f, .2f, .07f), Gray, Bone(name[0] == 'L' ? "lleg" : "rleg"));
            Camo(parts);
            if (parts.TryGetValue("RHand", out var hand)) BuildRifle(hand.bounds.center);
        }

        private Transform Bone(string name) => bones.TryGetValue(name, out var t) ? t : transform;
        private Vector3 At(Vector3 origin, float r, float u, float f) => origin + (right * r + Vector3.up * u + forward * f) * scale;

        private Material Mat(Color color, bool glow = false)
        {
            var m = new Material(Shader.Find("Standard")) { color = color };
            if (glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 1.2f); }
            materials.Add(m); return m;
        }

        private GameObject Part(string partName, PrimitiveType type, Vector3 worldPosition, Vector3 size, Color color, Transform bone, float pitch = 0, bool roll = false, bool glow = false)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = partName; Destroy(part.GetComponent<Collider>());
            var turn = Quaternion.LookRotation(forward, Vector3.up) * (roll ? Quaternion.Euler(0, 0, 90) : Quaternion.Euler(pitch, 0, 0));
            part.transform.SetPositionAndRotation(worldPosition, turn);
            part.transform.localScale = size * scale;
            part.GetComponent<Renderer>().sharedMaterial = Mat(color, glow);
            part.transform.SetParent(bone, true);
            return part;
        }

        private void Recolor(Dictionary<string, Renderer> parts)
        {
            var dark = Mat(Black); var accent = Mat(Orange);
            foreach (var pair in parts)
            {
                string n = pair.Key;
                if (n == "StateLight") continue;
                if (n == "Visor") { pair.Value.enabled = false; continue; }
                bool orange = n.EndsWith("Shoulder") || n.EndsWith("Knee") || n == "ChestAccent" || n.StartsWith("HearingDisc") || n == "SignalAntenna";
                pair.Value.sharedMaterial = orange ? accent : dark;
            }
        }

        private void BuildHead(Vector3 head)
        {
            var bone = Bone("head");
            Part("Helmet", PrimitiveType.Sphere, At(head, 0, .07f, -.01f), new Vector3(.40f, .30f, .42f), Black, bone);
            Part("Helmet plate", PrimitiveType.Cube, At(head, 0, .15f, .19f), new Vector3(.12f, .06f, .03f), Gray, bone);
            Part("Helmet emblem", PrimitiveType.Cylinder, At(head, 0, .15f, .21f), new Vector3(.07f, .006f, .07f), new Color(.8f, .8f, .82f), bone, 90);
            Part("Goggle frame", PrimitiveType.Cube, At(head, 0, .03f, .155f), new Vector3(.33f, .11f, .03f), Orange, bone);
            Part("Goggle lens", PrimitiveType.Cube, At(head, 0, .03f, .17f), new Vector3(.29f, .08f, .03f), new Color(.92f, .94f, 1f), bone, 0, false, true);
            Part("Gas mask", PrimitiveType.Cube, At(head, 0, -.09f, .17f), new Vector3(.2f, .15f, .1f), Gray, bone);
            foreach (var side in new[] { -1f, 1f })
                Part("Mask filter", PrimitiveType.Cylinder, At(head, .14f * side, -.08f, .12f), new Vector3(.13f, .04f, .13f), Black, bone, 0, true);
        }

        private void BuildTorso(Vector3 torso)
        {
            var bone = Bone("torso");
            Part("Vest front", PrimitiveType.Cube, At(torso, 0, .05f, .195f), new Vector3(.5f, .46f, .04f), Gray, bone);
            Part("Vest back", PrimitiveType.Cube, At(torso, 0, .05f, -.195f), new Vector3(.5f, .46f, .04f), Gray, bone);
            Part("Vest emblem", PrimitiveType.Cylinder, At(torso, 0, .17f, .22f), new Vector3(.09f, .006f, .09f), new Color(.8f, .8f, .82f), bone, 90);
            foreach (var side in new[] { -.17f, .17f })
                Part("Vest pouch", PrimitiveType.Cube, At(torso, side, -.1f, .235f), new Vector3(.1f, .12f, .05f), Black, bone);
            Part("Backpack", PrimitiveType.Cube, At(torso, 0, 0, -.31f), new Vector3(.4f, .42f, .2f), Black, bone);
            Part("Backpack patch", PrimitiveType.Cube, At(torso, .06f, .06f, -.31f), new Vector3(.2f, .14f, .21f), Orange, bone);
        }

        private void Camo(Dictionary<string, Renderer> parts)
        {
            var random = new System.Random(77);
            foreach (var entry in new[] { ("LArm", "larm"), ("RArm", "rarm"), ("LLeg", "lleg"), ("RLeg", "rleg") })
            {
                if (!parts.TryGetValue(entry.Item1, out var limb)) continue;
                for (int i = 0; i < 2; i++)
                {
                    float up = (i == 0 ? .1f : -.12f) + (float)random.NextDouble() * .06f;
                    float side = ((float)random.NextDouble() - .5f) * .04f;
                    Part("Camo patch", PrimitiveType.Cube, At(limb.bounds.center, side, up, 0), new Vector3(.16f, .12f, .17f), Orange, Bone(entry.Item2));
                }
            }
        }

        private void BuildRifle(Vector3 hand)
        {
            var bone = Bone("rarm");
            Vector3 c = At(hand, -.1f, .05f, .26f);
            Color metal = new Color(.05f, .05f, .06f);
            Part("Rifle receiver", PrimitiveType.Cube, c, new Vector3(.06f, .09f, .38f), metal, bone);
            Part("Rifle handguard", PrimitiveType.Cube, At(c, 0, 0, .2f), new Vector3(.055f, .06f, .26f), metal, bone);
            Part("Rifle barrel", PrimitiveType.Cylinder, At(c, 0, .005f, .45f), new Vector3(.03f, .15f, .03f), metal, bone, 90);
            Part("Rifle magazine", PrimitiveType.Cube, At(c, 0, -.11f, .02f), new Vector3(.04f, .15f, .06f), metal, bone);
            Part("Rifle stock", PrimitiveType.Cube, At(c, 0, -.02f, -.26f), new Vector3(.05f, .1f, .16f), metal, bone);
            Part("Rifle carry handle", PrimitiveType.Cube, At(c, 0, .07f, -.02f), new Vector3(.03f, .05f, .2f), metal, bone);
            Part("Rifle front sight", PrimitiveType.Cube, At(c, 0, .06f, .35f), new Vector3(.02f, .07f, .02f), metal, bone);
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetPositionAndRotation(At(c, 0, .005f, .62f), Quaternion.LookRotation(forward, Vector3.up));
            muzzle.transform.SetParent(bone, true);
            Muzzle = muzzle.transform;
            flashBody = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flashBody.name = "Muzzle flash"; Destroy(flashBody.GetComponent<Collider>());
            flashBody.transform.SetParent(Muzzle, false); flashBody.transform.localScale = Vector3.one * .14f * scale;
            flashBody.GetComponent<Renderer>().sharedMaterial = Mat(new Color(1f, .75f, .2f), true);
            flashLight = flashBody.AddComponent<Light>(); flashLight.color = new Color(1f, .7f, .3f); flashLight.range = 5; flashLight.intensity = 2.5f;
            flashBody.SetActive(false);
        }

        /// <summary>Shows a short muzzle flash when SH-01's shot resolves.</summary>
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
