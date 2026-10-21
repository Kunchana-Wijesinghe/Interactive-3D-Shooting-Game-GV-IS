using FacilityBreach.Audio;
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
        private GameObject crossMarks;
        private Material lampMaterial;
        private bool? lampActive;
        /// <summary>Boom device status lamp: green while the device is active, red once it has been deactivated.</summary>
        public void SetDeviceActive(bool active)
        {
            if (Kind != StationKind.Device || lampActive == active) return;
            lampActive = active;
            if (!lampMaterial) return;
            var color = active ? new Color(.1f, .95f, .2f) : new Color(.95f, .1f, .08f);
            lampMaterial.color = color; lampMaterial.SetColor("_EmissionColor", color * 2f);
        }
        private void Awake() { visual = GetComponent<Renderer>(); Rounds = Quantity; }
        private void Start()
        {
            if (crossMarks) return;
            if (Kind == StationKind.Health) BuildFirstAidCrosses();
            else if (Kind == StationKind.AmmoCrate) BuildAmmoCrateDetails();
            else if (Kind == StationKind.Device) BuildBoomDevice();
            else return;
            Refresh();
        }
        // Industrial "boom device": steel drum generator with yellow end housing, control boxes, pipes and a railed platform.
        private void BuildBoomDevice()
        {
            const float k = .75f;
            crossMarks = new GameObject("Boom device");
            crossMarks.transform.SetParent(transform, false);
            float inverse = k / transform.lossyScale.x;
            crossMarks.transform.localScale = Vector3.one * inverse;
            crossMarks.transform.localPosition = Vector3.down * (transform.position.y / transform.lossyScale.x);
            var steel = DeviceMaterial(new Color(.2f, .22f, .25f), .6f, .45f);
            var darkSteel = DeviceMaterial(new Color(.09f, .1f, .11f), .5f, .4f);
            var yellow = DeviceMaterial(new Color(.8f, .6f, .12f), .35f, .5f);
            var pipe = DeviceMaterial(new Color(.55f, .57f, .6f), .8f, .6f);
            var tan = DeviceMaterial(new Color(.7f, .55f, .28f), .2f, .3f);
            var blue = DeviceMaterial(new Color(.1f, .24f, .52f), .2f, .4f);
            lampMaterial = DeviceMaterial(Color.green, 0f, .5f);
            lampMaterial.EnableKeyword("_EMISSION");
            lampActive = null; SetDeviceActive(true);
            // Platform and railing
            DevicePart(PrimitiveType.Cube, new Vector3(0, .06f, 0), new Vector3(2f, .12f, 1.3f), darkSteel);
            foreach (var x in new[] { -.95f, .95f })
                foreach (var z in new[] { -.6f, .6f })
                    DevicePart(PrimitiveType.Cube, new Vector3(x, .37f, z), new Vector3(.04f, .5f, .04f), yellow);
            foreach (var z in new[] { -.6f, .6f }) DevicePart(PrimitiveType.Cube, new Vector3(0, .6f, z), new Vector3(1.9f, .03f, .03f), yellow);
            foreach (var x in new[] { -.95f, .95f }) DevicePart(PrimitiveType.Cube, new Vector3(x, .6f, 0), new Vector3(.03f, .03f, 1.2f), yellow);
            // Main drum along X with yellow housing at the right and a smaller cap at the left
            var along = Quaternion.Euler(0, 0, 90);
            DevicePart(PrimitiveType.Cylinder, new Vector3(-.2f, .5f, 0), new Vector3(.7f, .55f, .7f), steel, along);
            DevicePart(PrimitiveType.Cylinder, new Vector3(.5f, .5f, 0), new Vector3(.76f, .3f, .76f), yellow, along);
            DevicePart(PrimitiveType.Cylinder, new Vector3(-.78f, .5f, 0), new Vector3(.76f, .08f, .76f), yellow, along);
            DevicePart(PrimitiveType.Cylinder, new Vector3(.9f, .5f, 0), new Vector3(.5f, .1f, .5f), darkSteel, along);
            foreach (var x in new[] { -.5f, .1f }) DevicePart(PrimitiveType.Cylinder, new Vector3(x, .5f, 0), new Vector3(.74f, .025f, .74f), darkSteel, along);
            // Control boxes, blue unit, pipes and warning lamp
            DevicePart(PrimitiveType.Cube, new Vector3(-.4f, .45f, -.37f), new Vector3(.2f, .24f, .06f), tan);
            DevicePart(PrimitiveType.Cube, new Vector3(-.1f, .4f, -.37f), new Vector3(.16f, .2f, .06f), tan);
            DevicePart(PrimitiveType.Cube, new Vector3(.8f, .27f, .35f), new Vector3(.3f, .26f, .28f), blue);
            DevicePart(PrimitiveType.Cylinder, new Vector3(0, .92f, .38f), new Vector3(.1f, .9f, .1f), pipe, along);
            DevicePart(PrimitiveType.Cylinder, new Vector3(-.9f, .55f, .38f), new Vector3(.1f, .45f, .1f), pipe);
            DevicePart(PrimitiveType.Sphere, new Vector3(-.2f, .9f, 0), new Vector3(.1f, .1f, .1f), lampMaterial);
            var body = GetComponent<BoxCollider>();
            if (body)
            {
                body.size = new Vector3(1.4f, .8f, .8f) * (k / transform.lossyScale.x);
                body.center = new Vector3(0, .5f * k - transform.position.y, 0) / transform.lossyScale.x;
            }
        }
        private Material DeviceMaterial(Color color, float metallic, float smoothness)
        {
            var material = new Material(Shader.Find("Standard")) { color = color };
            material.SetFloat("_Metallic", metallic); material.SetFloat("_Glossiness", smoothness);
            return material;
        }
        private void DevicePart(PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material, Quaternion? rotation = null)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = "Device part"; Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(crossMarks.transform, false);
            part.transform.localPosition = localPosition; part.transform.localScale = localScale;
            if (rotation.HasValue) part.transform.localRotation = rotation.Value;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
        // Olive-green military ammo can: carry handle, lid rim and front latch (no text).
        private void BuildAmmoCrateDetails()
        {
            crossMarks = new GameObject("Ammo crate details");
            crossMarks.transform.SetParent(transform, false);
            var steel = new Material(Shader.Find("Standard")) { name = "AmmoCrateSteel", color = new Color(.16f, .17f, .16f) };
            AddPart("Carry handle", new Vector3(0, .56f, 0), new Vector3(.6f, .08f, .12f), steel);
            AddPart("Handle post A", new Vector3(-.26f, .53f, 0), new Vector3(.08f, .1f, .12f), steel);
            AddPart("Handle post B", new Vector3(.26f, .53f, 0), new Vector3(.08f, .1f, .12f), steel);
            AddPart("Lid rim", new Vector3(0, .32f, 0), new Vector3(1.04f, .06f, 1.04f), steel);
            AddPart("Latch front", new Vector3(0, .32f, -.52f), new Vector3(.2f, .16f, .04f), steel);
        }
        private void AddPart(string partName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName; Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(crossMarks.transform, false);
            part.transform.localPosition = localPosition; part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
        // Metal fragments thrown out when the crate is shot open; they remove themselves after 3 seconds.
        private void SpawnDebris()
        {
            var color = visual ? visual.sharedMaterial.color : Color.gray;
            for (int i = 0; i < 10; i++)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "Crate debris";
                piece.transform.position = transform.position + Random.insideUnitSphere * .2f;
                piece.transform.rotation = Random.rotation;
                piece.transform.localScale = new Vector3(Random.Range(.08f, .2f), Random.Range(.02f, .05f), Random.Range(.08f, .2f));
                piece.GetComponent<Renderer>().material.color = i % 3 == 0 ? new Color(.16f, .17f, .16f) : color;
                var body = piece.AddComponent<Rigidbody>(); body.mass = .2f;
                body.AddExplosionForce(3f, transform.position + Vector3.down * .15f, 1.5f, .5f, ForceMode.Impulse);
                Destroy(piece, 3f);
            }
        }
        // White box body (tinted in Refresh) with a red cross on each of the six faces.
        private void BuildFirstAidCrosses()
        {
            crossMarks = new GameObject("First-aid crosses");
            crossMarks.transform.SetParent(transform, false);
            var red = new Material(Shader.Find("Standard")) { name = "FirstAidRed", color = new Color(.85f, .05f, .05f) };
            var normals = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var normal in normals)
            {
                var rotation = Quaternion.LookRotation(normal, Mathf.Abs(normal.y) > .9f ? Vector3.forward : Vector3.up);
                foreach (var size in new[] { new Vector3(.7f, .22f, .02f), new Vector3(.22f, .7f, .02f) })
                {
                    var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bar.name = "Cross bar"; Destroy(bar.GetComponent<Collider>());
                    bar.transform.SetParent(crossMarks.transform, false);
                    bar.transform.localRotation = rotation;
                    bar.transform.localPosition = normal * .501f;
                    bar.transform.localScale = size;
                    bar.GetComponent<Renderer>().sharedMaterial = red;
                }
            }
        }
        // Pickup left behind by a shot crate: open green tray holding six brass bullets with copper tips.
        private GameObject BuildBulletPack()
        {
            var pack = new GameObject("Associated ammo pickup");
            pack.transform.SetParent(transform, false);
            pack.transform.localScale = Vector3.one * .7f;
            var box = pack.AddComponent<BoxCollider>(); box.center = new Vector3(0, .1f, 0); box.size = new Vector3(1f, .6f, .65f);
            var green = PackMaterial(new Color(.12f, .36f, .15f), 0f, .35f);
            var brass = PackMaterial(new Color(.95f, .68f, .12f), .9f, .75f);
            var copper = PackMaterial(new Color(.85f, .42f, .08f), .9f, .8f);
            PackPart(pack, PrimitiveType.Cube, "Tray floor", new Vector3(0, -.2f, 0), new Vector3(.95f, .08f, .6f), green);
            PackPart(pack, PrimitiveType.Cube, "Tray front", new Vector3(0, -.05f, -.3f), new Vector3(.95f, .3f, .06f), green);
            PackPart(pack, PrimitiveType.Cube, "Tray back", new Vector3(0, -.05f, .3f), new Vector3(.95f, .3f, .06f), green);
            PackPart(pack, PrimitiveType.Cube, "Tray left", new Vector3(-.45f, -.05f, 0), new Vector3(.06f, .3f, .6f), green);
            PackPart(pack, PrimitiveType.Cube, "Tray right", new Vector3(.45f, -.05f, 0), new Vector3(.06f, .3f, .6f), green);
            foreach (var x in new[] { -.28f, 0f, .28f })
                foreach (var z in new[] { -.13f, .13f })
                {
                    PackPart(pack, PrimitiveType.Cylinder, "Bullet casing", new Vector3(x, .05f, z), new Vector3(.2f, .2f, .2f), brass);
                    PackPart(pack, PrimitiveType.Sphere, "Bullet tip", new Vector3(x, .25f, z), new Vector3(.2f, .28f, .2f), copper);
                }
            return pack;
        }
        private Material PackMaterial(Color color, float metallic, float smoothness)
        {
            var material = new Material(Shader.Find("Standard")) { color = color };
            material.SetFloat("_Metallic", metallic); material.SetFloat("_Glossiness", smoothness);
            return material;
        }
        private static void PackPart(GameObject pack, PrimitiveType type, string partName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = partName; Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(pack.transform, false);
            part.transform.localPosition = localPosition; part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
        public string Prompt => Kind == StationKind.AmmoCrate ? (Broken ? $"E: collect {Rounds} rounds" : $"Shoot marked crate: {Quantity} rounds") : $"E: {name}";
        public bool TakeDamage(in DamageInfo hit)
        {
            if (Kind != StationKind.AmmoCrate || Broken || !Available || hit.Amount <= 0) return false;
            Broken = true; Rounds = Quantity; RestoreRemaining = 15;
            ammoPickup = BuildBulletPack();
            SpawnDebris();
            FacilityAudioPlayer.Instance.PlayImpact(FacilityAudioClipType.CrateDestroyed, transform.position, 1f);
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
            if (visual) visual.enabled = Available && !Broken && !DisplayVisual && Kind != StationKind.Device;
            if (crossMarks) crossMarks.SetActive(Available && !Broken);
            if (visual) visual.sharedMaterial.color = Broken ? Color.yellow : Kind == StationKind.Health ? Color.white : Kind == StationKind.AmmoCrate ? new Color(.29f, .40f, .27f) : Kind == StationKind.Card ? Color.cyan : Color.gray;
            if (ammoPickup) ammoPickup.SetActive(Rounds > 0);
            var collider = GetComponent<Collider>(); if (collider) collider.enabled = Available && !Broken;
        }
    }
}
