using FacilityBreach.Core;
using Perera.Core;
using UnityEngine;

namespace FacilityBreach.Interactions
{
    public enum StationKind { Card, Weapon, Health, AmmoCrate, Device }
    public sealed class ResourceStation : MonoBehaviour, IDamageable
    {
        public StationKind Kind;
        public int Quantity = 25;
        public int Weapon = 1;
        public bool Available = true;
        public bool Broken { get; private set; }
        public int Rounds { get; private set; }
        public float RestoreRemaining { get; private set; }
        public bool IsDead => !Available || Broken;
        public bool CanOfferInteraction => Available && (!Broken || Rounds > 0);
        public Vector3 InteractionPoint => ammoPickup && ammoPickup.activeSelf ? ammoPickup.transform.position : transform.position;
        public string RejectionReason { get; private set; }
        public GameObject[] PickupModels;
        public GameObject DisplayVisual { get; private set; }
        private int shownWeapon = -1;
        private Renderer visual;
        private GameObject ammoPickup;
        private void Awake() { visual = GetComponent<Renderer>(); Rounds = Quantity; }
        public string Prompt => Kind == StationKind.AmmoCrate ? (Broken ? $"E: collect {Rounds} rounds" : $"Shoot marked crate: {Quantity} rounds") : $"E: {name}";
        public bool TakeDamage(in DamageInfo hit)
        {
            if (Kind != StationKind.AmmoCrate || Broken || !Available || hit.Amount <= 0) return false;
            Broken = true; Rounds = Quantity; RestoreRemaining = 15;
            ammoPickup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ammoPickup.name = "Associated ammo pickup";
            ammoPickup.transform.SetParent(transform, false);
            ammoPickup.transform.localScale = Vector3.one * .4f;
            ammoPickup.GetComponent<Renderer>().sharedMaterial = visual.sharedMaterial;
            Refresh(); return true;
        }
        public bool Use(MissionRun run, PlayerResources player)
        {
            RejectionReason = "Requirements not met";
            if (!Available || !run.Running || run.Paused || !player.Alive) { RejectionReason = "Unavailable or mission paused"; return false; }
            bool used = false;
            switch (Kind)
            {
                case StationKind.Card: used = run.CollectCard(); if (used) Available = false; break;
                case StationKind.Health: used = player.Heal(); if (used) { Available = false; RestoreRemaining = 5; } else RejectionReason = "Health must be 50 or lower"; break;
                case StationKind.Weapon:
                    Weapon = player.Swap(Weapon); name = $"Ammo{Weapon + 1}gun swap";
                    var label = GetComponentInChildren<TextMesh>(); if (label) label.text = name;
                    used = true; break;
                case StationKind.Device: used = run.Connect(true); break;
                case StationKind.AmmoCrate:
                    if (Broken) { int n = player.CollectAmmo(Rounds); Rounds -= n; used = n > 0; RejectionReason = "Reserve full (200)"; }
                    else RejectionReason = "Shoot this marked crate to release ammunition";
                    break;
            }
            Refresh(); return used;
        }
        private void LateUpdate()
        {
            if (Kind != StationKind.Weapon || shownWeapon == Weapon || PickupModels == null || PickupModels.Length <= Weapon || !PickupModels[Weapon]) return;
            if (DisplayVisual) { DisplayVisual.SetActive(false); Destroy(DisplayVisual); }
            DisplayVisual = Instantiate(PickupModels[Weapon], transform);
            DisplayVisual.transform.localScale = Vector3.one * 1.5f;
            foreach (var collider in DisplayVisual.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
            shownWeapon = Weapon; visual.enabled = false;
        }
        public void Tick(float dt)
        {
            if (RestoreRemaining <= 0) return;
            RestoreRemaining = Mathf.Max(0, RestoreRemaining - dt);
            if (RestoreRemaining == 0)
            { if (ammoPickup) Destroy(ammoPickup); ammoPickup = null; Available = true; Broken = false; Rounds = Quantity; Refresh(); }
        }
        private void Refresh()
        {
            if (visual) visual.enabled = Available && !Broken && !DisplayVisual;
            if (visual) visual.sharedMaterial.color = Broken ? Color.yellow : Kind == StationKind.Health ? Color.green : Kind == StationKind.Card ? Color.cyan : Color.gray;
            if (ammoPickup) ammoPickup.SetActive(Rounds > 0);
            var collider = GetComponent<Collider>(); if (collider) collider.enabled = Available && !Broken;
        }
    }
}
