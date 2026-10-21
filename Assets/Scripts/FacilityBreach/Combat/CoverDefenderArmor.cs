using System.Collections.Generic;
using UnityEngine;

namespace FacilityBreach.Combat
{
    /// <summary>
    /// Visual-only sci-fi armor for CD-03: black-visor helmet, pauldrons, chest plate with purple V, belt, thigh and shin
    /// armor, boots and a glowing plasma rifle. Gear is parented to the rig's animated bones; it never touches combat,
    /// movement or decisions.
    /// </summary>
    public sealed class CoverDefenderArmor : MonoBehaviour
    {
        private static readonly Color Gunmetal = new Color(.13f, .14f, .16f);
        private static readonly Color Plate = new Color(.36f, .38f, .43f);
        private static readonly Color Dark = new Color(.06f, .06f, .07f);
        private static readonly Color Purple = new Color(.62f, .24f, .86f);
        private readonly List<Material> materials = new List<Material>();
        private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        private Vector3 forward, right;
        private float scale = 1;

        public void Build(GameObject visual)
        {
            // Only real bones: skinned mesh objects share names like "Head"/"Torso" but never move with the animation.
            foreach (var t in visual.GetComponentsInChildren<Transform>(true))
                if (!t.GetComponent<Renderer>()) bones[t.name.ToLowerInvariant()] = t;
            var parts = new Dictionary<string, Renderer>();
            Bounds all = default; bool first = true;
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                parts[r.name] = r;
                if (first) { all = r.bounds; first = false; } else all.Encapsulate(r.bounds);
            }
            if (!parts.ContainsKey("Head") || !parts.ContainsKey("Torso")) { Debug.LogWarning("CD-03 armor skipped: rig parts not found", this); return; }
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
            if (parts.TryGetValue("Pelvis", out var pelvis)) BuildBelt(pelvis.bounds.center);
            foreach (var side in new[] { "L", "R" })
            {
                string leg = side.ToLowerInvariant() + "leg", arm = side.ToLowerInvariant() + "arm";
                if (parts.TryGetValue(side + "Shoulder", out var shoulder)) BuildShoulder(shoulder.bounds.center, Bone(arm));
                if (parts.TryGetValue(side + "Arm", out var armPart))
                {
                    Part("Forearm armor", PrimitiveType.Cube, At(armPart.bounds.center, 0, -.08f, .05f), new Vector3(.17f, .2f, .18f), Plate, Bone(arm));
                    Part("Forearm glow", PrimitiveType.Cube, At(armPart.bounds.center, 0, -.08f, .145f), new Vector3(.09f, .03f, .01f), Purple, Bone(arm), glow: true);
                }
                if (parts.TryGetValue(side + "Leg", out var legPart)) BuildLeg(legPart.bounds.center, Bone(leg));
                if (parts.TryGetValue(side + "Foot", out var foot))
                {
                    Part("Boot", PrimitiveType.Cube, At(foot.bounds.center, 0, .02f, .02f), new Vector3(.23f, .14f, .36f), Plate, Bone(leg));
                    Part("Boot band", PrimitiveType.Cube, At(foot.bounds.center, 0, .1f, .0f), new Vector3(.24f, .04f, .24f), Purple, Bone(leg), glow: true);
                }
            }
            if (parts.TryGetValue("RHand", out var hand)) BuildRifle(hand.bounds.center);
        }

        private Transform Bone(string name) => bones.TryGetValue(name, out var t) ? t : transform;
        private Vector3 At(Vector3 origin, float r, float u, float f) => origin + (right * r + Vector3.up * u + forward * f) * scale;

        private Material Mat(Color color, bool glow = false, float metallic = .3f)
        {
            var m = new Material(Shader.Find("Standard")) { color = color };
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Glossiness", .5f);
            if (glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 2f); }
            materials.Add(m); return m;
        }

        private GameObject Part(string partName, PrimitiveType type, Vector3 worldPosition, Vector3 size, Color color, Transform bone, float pitch = 0, float yaw = 0, bool glow = false)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = partName; Destroy(part.GetComponent<Collider>());
            part.transform.SetPositionAndRotation(worldPosition, Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(pitch, yaw, 0));
            part.transform.localScale = size * scale;
            part.GetComponent<Renderer>().sharedMaterial = Mat(color, glow);
            part.transform.SetParent(bone, true);
            return part;
        }

        private void Recolor(Dictionary<string, Renderer> parts)
        {
            var body = Mat(Gunmetal); var hands = Mat(Dark); var armor = Mat(Plate); var glow = Mat(Purple, true);
            foreach (var pair in parts)
            {
                string n = pair.Key;
                if (n == "StateLight") continue;
                if (n == "Visor") { pair.Value.enabled = false; continue; }
                if (n == "ChestAccent" || n.EndsWith("Knee")) pair.Value.sharedMaterial = glow;
                else if (n == "DefensiveArmor" || n.EndsWith("Shoulder")) pair.Value.sharedMaterial = armor;
                else if (n.EndsWith("Hand") || n.EndsWith("Foot") || n == "Pelvis") pair.Value.sharedMaterial = hands;
                else pair.Value.sharedMaterial = body;
            }
        }

        private void BuildHead(Vector3 head)
        {
            var bone = Bone("head");
            Part("Helmet", PrimitiveType.Sphere, At(head, 0, .05f, -.01f), new Vector3(.40f, .38f, .42f), new Color(.24f, .26f, .3f), bone);
            Part("Visor", PrimitiveType.Sphere, At(head, 0, .02f, .1f), new Vector3(.3f, .27f, .22f), new Color(.02f, .02f, .03f), bone);
            Part("Visor trim", PrimitiveType.Cube, At(head, 0, .165f, .13f), new Vector3(.25f, .015f, .06f), Purple, bone, 0, 0, true);
            foreach (var side in new[] { -1f, 1f })
                Part("Ear pod", PrimitiveType.Cylinder, At(head, .19f * side, -.01f, 0), new Vector3(.11f, .025f, .11f), Plate, bone).transform.Rotate(0, 0, 90, Space.Self);
        }

        private void BuildTorso(Vector3 torso)
        {
            var bone = Bone("torso");
            Part("Chest plate", PrimitiveType.Cube, At(torso, 0, .06f, .2f), new Vector3(.52f, .42f, .05f), Gunmetal, bone);
            Part("Chest V left", PrimitiveType.Cube, At(torso, -.1f, -.12f, .235f), new Vector3(.2f, .04f, .02f), Purple, bone, 0, 0, true).transform.Rotate(0, 0, -35, Space.Self);
            Part("Chest V right", PrimitiveType.Cube, At(torso, .1f, -.12f, .235f), new Vector3(.2f, .04f, .02f), Purple, bone, 0, 0, true).transform.Rotate(0, 0, 35, Space.Self);
            Part("Back pack", PrimitiveType.Cube, At(torso, 0, .02f, -.26f), new Vector3(.42f, .44f, .16f), Plate, bone);
        }

        private void BuildBelt(Vector3 pelvis)
        {
            var bone = Bone("root");
            Part("Belt", PrimitiveType.Cube, At(pelvis, 0, .1f, .0f), new Vector3(.5f, .1f, .3f), new Color(.5f, .51f, .54f), bone);
            Part("Belt glow", PrimitiveType.Cube, At(pelvis, 0, .1f, .155f), new Vector3(.06f, .04f, .01f), Purple, bone, 0, 0, true);
            foreach (var side in new[] { -.26f, .26f })
                Part("Belt pouch", PrimitiveType.Cube, At(pelvis, side, .08f, 0), new Vector3(.1f, .16f, .2f), Plate, bone);
        }

        private void BuildShoulder(Vector3 shoulder, Transform bone)
        {
            Part("Pauldron", PrimitiveType.Cube, At(shoulder, 0, .1f, 0), new Vector3(.27f, .13f, .31f), Plate, bone);
            Part("Pauldron glow", PrimitiveType.Cube, At(shoulder, 0, .175f, .06f), new Vector3(.1f, .015f, .08f), Purple, bone, 0, 0, true);
        }

        private void BuildLeg(Vector3 leg, Transform bone)
        {
            Part("Thigh armor", PrimitiveType.Cube, At(leg, 0, .12f, .09f), new Vector3(.18f, .32f, .06f), Plate, bone);
            Part("Thigh inset", PrimitiveType.Cube, At(leg, 0, .12f, .125f), new Vector3(.11f, .2f, .01f), Dark, bone);
            Part("Shin guard", PrimitiveType.Cube, At(leg, 0, -.2f, .09f), new Vector3(.18f, .26f, .07f), Plate, bone);
            Part("Shin band", PrimitiveType.Cube, At(leg, 0, -.3f, .0f), new Vector3(.19f, .05f, .19f), Purple, bone, 0, 0, true);
        }

        private void BuildRifle(Vector3 hand)
        {
            var bone = Bone("rarm");
            Vector3 c = At(hand, -.1f, .05f, .26f);
            Color body = new Color(.2f, .22f, .26f);
            Part("Rifle receiver", PrimitiveType.Cube, c, new Vector3(.08f, .11f, .42f), body, bone);
            Part("Rifle handguard", PrimitiveType.Cube, At(c, 0, 0, .24f), new Vector3(.07f, .08f, .26f), body, bone);
            Part("Rifle glow strip", PrimitiveType.Cube, At(c, .042f, .01f, .08f), new Vector3(.01f, .03f, .24f), Purple, bone, 0, 0, true);
            Part("Rifle glow strip", PrimitiveType.Cube, At(c, -.042f, .01f, .08f), new Vector3(.01f, .03f, .24f), Purple, bone, 0, 0, true);
            Part("Rifle barrel", PrimitiveType.Cylinder, At(c, 0, 0, .45f), new Vector3(.045f, .12f, .045f), body, bone, 90);
            Part("Rifle muzzle glow", PrimitiveType.Cylinder, At(c, 0, 0, .58f), new Vector3(.05f, .015f, .05f), Purple, bone, 90, 0, true);
            Part("Rifle magazine", PrimitiveType.Cube, At(c, 0, -.12f, .02f), new Vector3(.05f, .16f, .07f), body, bone);
            Part("Rifle stock", PrimitiveType.Cube, At(c, 0, -.02f, -.27f), new Vector3(.06f, .11f, .16f), body, bone);
        }

        private void OnDestroy() { foreach (var m in materials) if (m) Destroy(m); }
    }
}
