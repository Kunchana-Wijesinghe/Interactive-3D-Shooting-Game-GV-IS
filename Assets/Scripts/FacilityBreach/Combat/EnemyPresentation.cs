using UnityEngine;
using FacilityBreach.AI.SoundHunter;
using FacilityBreach.AI.Roles;

namespace FacilityBreach.Combat
{
    // Presentation attaches to the existing authoritative combat root, never supplies health.
    public sealed class EnemyPresentation : MonoBehaviour
    {
        private EnemyCombatAdapter combat;
        private Renderer stateLight;
        private Material lightMaterial;
        private Transform visual;
        private Quaternion restingRotation;
        private Vector3 restingPosition;
        private void Start()
        {
            combat = GetComponent<EnemyCombatAdapter>();
            visual = transform.Find("ModelVisual");
            if (visual) { restingPosition = visual.localPosition; restingRotation = visual.localRotation; }
            foreach (var renderer in GetComponentsInChildren<Renderer>())
                if (renderer.name.Contains("StateLight")) { stateLight = renderer; break; }
            if (stateLight)
            {
                lightMaterial = new Material(stateLight.sharedMaterial);
                stateLight.sharedMaterial = lightMaterial;
            }
        }
        private void LateUpdate()
        {
            if (!combat) return;
            if (visual)
            {
                visual.localRotation = combat.IsDead ? restingRotation * Quaternion.Euler(85, 0, 0) : restingRotation;
                visual.localPosition = restingPosition + (combat.IsDead ? Vector3.up * .15f : Vector3.zero);
            }
            if (!stateLight) return;
            stateLight.enabled = !combat.IsDead;
            if (combat.IsDead) return;
            var hunter = GetComponent<FacilityBreachSoundHunterBrain>();
            var role = GetComponent<RoleDecisionBrain>();
            Color color = combat.Warning ? Color.white : (hunter && hunter.CurrentState == SoundHunterState.Combat || role && (role.State == RoleState.Combat || role.State == RoleState.Cover)) ? Color.red :
                (hunter && (hunter.CurrentState == SoundHunterState.Investigate || hunter.CurrentState == SoundHunterState.Search) || role && (role.State == RoleState.Investigate || role.State == RoleState.Search || role.State == RoleState.Flank || role.State == RoleState.Detecting)) ? Color.yellow : Color.blue;
            lightMaterial.color = color;
            lightMaterial.SetColor("_EmissionColor", color * 2);
        }
        private void OnDestroy() { if (lightMaterial) Destroy(lightMaterial); }
    }
}
