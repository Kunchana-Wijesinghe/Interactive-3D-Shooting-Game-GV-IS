using UnityEngine;

namespace Perera.Combat
{
    /// <summary>Test-scene IMGUI readout of ammo and reload state. Not the shared HUD.</summary>
    public class CombatDebugHud : MonoBehaviour
    {
        private HitscanWeapon weapon;
        private void Awake() => weapon = GetComponentInChildren<HitscanWeapon>();

        private void OnGUI()
        {
            if (weapon == null || weapon.Ammo == null) return;
            string state = weapon.IsReloading ? "  RELOADING" : "";
            GUI.Label(new Rect(20, 20, 300, 24), $"Ammo {weapon.Ammo.InMagazine} / {weapon.Ammo.Reserve}{state}");
            GUI.Label(new Rect(Screen.width / 2 - 4, Screen.height / 2 - 10, 20, 20), "+");
        }
    }
}
