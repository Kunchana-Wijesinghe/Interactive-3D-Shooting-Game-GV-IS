using UnityEditor;
using UnityEngine;
using System.IO;

namespace FacilityBreach.Editor
{
    public static class RobotLegOrientationAudit
    {
        [MenuItem("Facility Breach/Audit Robot Leg Orientations")]
        public static void Audit()
        {
            string[] prefabs = {
                "Assets/Prefabs/FacilityBreach/Enemies/PatrolGuard_Visual.prefab",
                "Assets/Prefabs/FacilityBreach/Enemies/SoundHunter_Visual.prefab",
                "Assets/Prefabs/FacilityBreach/Enemies/CoverDefender_Visual.prefab",
                "Assets/Prefabs/FacilityBreach/Enemies/Flanker_Visual.prefab"
            };

            var report = new System.Text.StringBuilder();
            report.AppendLine("=== Robot Leg Orientation Audit ===");

            foreach (var path in prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) { report.AppendLine("MISSING: " + path); continue; }

                var instance = Object.Instantiate(prefab);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;

                Transform lleg = null, rleg = null, lfoot = null, rfoot = null;
                foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    if (!t.GetComponent<Renderer>())
                    {
                        if (n == "lleg") lleg = t;
                        if (n == "rleg") rleg = t;
                    }
                    if (t.name == "LFoot") lfoot = t;
                    if (t.name == "RFoot") rfoot = t;
                }

                report.AppendLine($"Prefab: {prefab.name}");
                report.AppendLine($"  lleg found: {lleg != null}, rleg found: {rleg != null}");
                if (lleg != null)
                {
                    // Create a probe at the bottom of the leg in bone local space
                    // Bone is oriented with localRotation Euler(180, 0, 0).
                    // In bone space, let's see which vector corresponds to down (-Y in model space).
                    Quaternion restRot = lleg.localRotation;
                    // In instance space:
                    Vector3 downInBoneSpace = lleg.InverseTransformDirection(Vector3.down);
                    report.AppendLine($"  Bone down vector in local space: {downInBoneSpace.ToString("F3")}");

                    GameObject probe = new GameObject("LegProbe");
                    probe.transform.SetParent(lleg, false);
                    probe.transform.localPosition = downInBoneSpace * 0.8f; // ~80cm down the leg

                    Vector3 probeRestInstance = instance.transform.InverseTransformPoint(probe.transform.position);
                    report.AppendLine($"  Leg probe rest (instance space): {probeRestInstance.ToString("F4")}");

                    // Test local rotation around X: +20
                    lleg.localRotation = restRot * Quaternion.Euler(20, 0, 0);
                    Vector3 probePitchPos = instance.transform.InverseTransformPoint(probe.transform.position);
                    float deltaZ_pos = probePitchPos.z - probeRestInstance.z;

                    // Test local rotation around X: -20
                    lleg.localRotation = restRot * Quaternion.Euler(-20, 0, 0);
                    Vector3 probePitchNeg = instance.transform.InverseTransformPoint(probe.transform.position);
                    float deltaZ_neg = probePitchNeg.z - probeRestInstance.z;

                    lleg.localRotation = restRot;
                    report.AppendLine($"  Pitch +20 on local X -> delta Z: {deltaZ_pos:F4} ({(deltaZ_pos > 0 ? "FORWARD (+Z)" : "BACKWARD (-Z)")})");
                    report.AppendLine($"  Pitch -20 on local X -> delta Z: {deltaZ_neg:F4} ({(deltaZ_neg > 0 ? "FORWARD (+Z)" : "BACKWARD (-Z)")})");
                    Object.DestroyImmediate(probe);
                }

                Object.DestroyImmediate(instance);
            }

            File.WriteAllText("robot_leg_audit.txt", report.ToString());
            Debug.Log(report.ToString());
        }
    }
}
