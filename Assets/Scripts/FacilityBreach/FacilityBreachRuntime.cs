using System.Collections.Generic;
using FacilityBreach.Core;
using FacilityBreach.Interactions;
using FacilityBreach.Player;
using FacilityBreach.Combat;
using Perera.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using FacilityBreach.AI.SoundHunter;
using FacilityBreach.AI.Roles;
using FacilityBreach.Audio;
using FacilityBreach.Noise;


namespace FacilityBreach
{
    // Serialized scene root. Runtime additions are children; authored environment is never rebuilt.
    [DefaultExecutionOrder(-200)]
    public sealed class FacilityBreachRuntime : MonoBehaviour
    {
        public static FacilityBreachRuntime Instance { get; private set; }
        public MissionRun Run { get; private set; } = new MissionRun();
        public PlayerResources Resources { get; private set; } = new PlayerResources();
        public EnemyRoster Enemies { get; private set; } = new EnemyRoster();
        public float InteractionRange = 2.5f, ShotRange = 50, ShotInterval = .2f, ReloadSeconds = 1.5f;
        public GameObject[] EnemyVisuals = new GameObject[4];
        public GameObject[] WeaponViewModels = new GameObject[3];
        public GameObject[] WeaponPickupModels = new GameObject[3];
        public NavMeshData Navigation;
        public bool DevelopmentDiagnostics;
        public bool AcceptsGameplay => Run.Running && !Run.Paused && Resources.Alive;
        public Camera GameplayCamera => view;
        public Collider LastShotCollider { get; private set; }
        public string InteractionRejection { get; private set; }
        public FacilityInteractionDispatcher Dispatcher => dispatcher;
        private FacilityInteractionDispatcher dispatcher;
        private NavMeshDataInstance navigationInstance;
        private GameObject equippedVisual;
        private readonly List<ResourceStation> stations = new List<ResourceStation>();
        private readonly List<GameObject> gates = new List<GameObject>();
        private readonly List<(GameObject seal, InteractiveDoor door)> doorSeals = new List<(GameObject, InteractiveDoor)>();
        private readonly List<Collider> mapWalls = new List<Collider>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private TemporaryPlayerAdapter player;
        private Camera view;
        private Vector3 exit;
        private Transform muzzle;
        private ResourceStation device;
        private ResourceStation target;
        private FpsBulletTracer bulletTracer;
        public FpsBulletTracer BulletTracer => bulletTracer;
        private FpsMuzzleFlashV2 muzzleFlash;
        public FpsMuzzleFlashV2 MuzzleFlash => muzzleFlash;
        private FpsImpactSparks impactSparks;
        public FpsImpactSparks ImpactSparks => impactSparks;
        private FacilityAudioPlayer audioPlayer;
        public FacilityAudioPlayer AudioPlayer => audioPlayer;
        private FacilityAmbiencePlayer ambiencePlayer;
        public FacilityAmbiencePlayer AmbiencePlayer => ambiencePlayer;
        private FpsKickController kickController;
        public FpsKickController KickController => kickController;
        private FpsFootstepController footstepController;
        public FpsFootstepController FootstepController => footstepController;
        private FacilityEmergencyAlertController alertController;
        public FacilityEmergencyAlertController AlertController => alertController;
        public float TargetMissionDuration = 180f;
        private float reload, cadence;
        private bool waitForRelease = true, controls;
        private string feedback = "";
        private float feedbackTime;
        public Transform PlayerTransform => player ? player.transform : null;
        public float GameplayTime { get; private set; }

        private Transform Anchor(string objectName)
        {
            GameObject found = GameObject.Find(objectName);
            if (!found) throw new System.InvalidOperationException("Facility Breach missing authored anchor: " + objectName);
            return found.transform;
        }
        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            view = Camera.main;
            if (!view) throw new System.InvalidOperationException("Prototype requires its existing Main Camera");
            GameplayDiagnostics.Enabled = DevelopmentDiagnostics;
            if (Navigation) navigationInstance = NavMesh.AddNavMeshData(Navigation);
            gameObject.AddComponent<DynamicNavigationObstacles>();
            view.nearClipPlane = .05f;
            if (!DevelopmentDiagnostics && TargetMissionDuration > 0)
            {
                Run.ConfigureDuration(TargetMissionDuration);
            }
            var spawn = Anchor("Door_Main_Entrance").position + Vector3.forward * 1.75f;
            exit = Anchor("Door_Main_Exit").position - Vector3.forward * 1.75f;
            var root = new GameObject("FB_TemporaryPlayer_BandaraAdapterPending");
            root.transform.SetParent(transform); root.transform.position = new Vector3(spawn.x, .1f, spawn.z); root.tag = "Player";
            var body = root.AddComponent<CharacterController>(); body.height = 1.8f; body.radius = .3f; body.center = Vector3.up * .9f;
            player = root.AddComponent<TemporaryPlayerAdapter>(); player.View = view;
            root.AddComponent<PlayerDamageAdapter>().Runtime = this;
            if (!root.GetComponent<FacilityBreach.Noise.FacilityNoiseBridge>()) root.AddComponent<FacilityBreach.Noise.FacilityNoiseBridge>();
            kickController = root.AddComponent<FpsKickController>(); kickController.Initialize(this, view);
            footstepController = root.AddComponent<FpsFootstepController>(); footstepController.Initialize(this, body, player);
            view.transform.SetParent(root.transform); view.transform.localPosition = Vector3.up * 1.6f; view.transform.localRotation = Quaternion.identity;
            var muzzleObject = new GameObject("FB_Muzzle_VisualPending"); muzzleObject.transform.SetParent(view.transform);
            muzzleObject.transform.localPosition = new Vector3(.2f, -.15f, .45f); muzzle = muzzleObject.transform;
            UpdateWeaponVisual();
            if (!audioPlayer) audioPlayer = GetComponent<FacilityAudioPlayer>();
            if (!audioPlayer) audioPlayer = gameObject.AddComponent<FacilityAudioPlayer>();
            if (!ambiencePlayer) ambiencePlayer = GetComponent<FacilityAmbiencePlayer>();
            if (!ambiencePlayer) ambiencePlayer = gameObject.AddComponent<FacilityAmbiencePlayer>();
            if (!alertController) alertController = GetComponent<FacilityEmergencyAlertController>();
            if (!alertController) alertController = gameObject.AddComponent<FacilityEmergencyAlertController>();
            alertController.Initialize(this);
            dispatcher = gameObject.AddComponent<FacilityInteractionDispatcher>();
            dispatcher.Initialize(this, view, player.transform);
            foreach (var door in FindObjectsByType<InteractiveDoor>(FindObjectsSortMode.None))
            {
                door.handleInternalInput = false;
                dispatcher.RegisterDoor(door);
            }
            foreach (var barricadeRb in FindObjectsByType<Rigidbody>())
            {
                if (!barricadeRb.name.StartsWith("Barricade")) continue;
                var mb = barricadeRb.GetComponent<MovableBarricade>();
                if (!mb) mb = barricadeRb.gameObject.AddComponent<MovableBarricade>();
                mb.mass = 8f;
                mb.linearDamping = 0.5f;
                mb.angularDamping = 8f;
                mb.interactionDistance = 3.0f;
                mb.holdDistance = 2.0f;
                mb.followSpeed = 8.0f;
                mb.maxHoldDistance = 3.8f;
                mb.ConfigurePhysics();
                dispatcher.RegisterBarricade(mb);
            }
            FacilityKickablePropsSetup.EnsurePropsSpawned(transform);
            FacilityKickablePropsSetup.ConvertExistingSceneProps();
            AttachBloodMarksToDoors();
            ReplaceScaleReferencesWithEntranceProps();
            // The committed scene lacks the waste root despite shipping its generator.
            // Add only an obstacle here; never call the destructive world generators.
            Vector3 waste = new Vector3(-2, 0, 20);
            var authoredWaste = GameObject.Find("Toxic_Waste_Obstacle");
            if (authoredWaste) waste = authoredWaste.transform.position;
            else
            {
                var obstacle = Box("FB_ToxicWaste_IntegrationPlaceholder", waste + Vector3.up * .5f, new Vector3(5.2f, 1, 4.8f), new Color(.2f, .5f, .1f));
                Label(obstacle, "TOXIC WASTE — NO ENTRY");
                obstacle.GetComponent<Renderer>().enabled = false;
                BuildToxicBarrels(waste);
                var nav = obstacle.AddComponent<UnityEngine.AI.NavMeshObstacle>(); nav.carving = true; nav.shape = UnityEngine.AI.NavMeshObstacleShape.Box; nav.size = Vector3.one;
            }
            // Platform extents from the supplied obstacle script. Eastern edge outside containment.
            Station("Access card", StationKind.Card, waste + new Vector3(3.4f, .75f, 0), ResourceStation.GoldCard);
            Station("Ammo3gun swap", StationKind.Weapon, waste + new Vector3(3.4f, .75f, -1.5f), Color.yellow).Weapon = 2;
            Station("Entrance health +50 (HP <=50)", StationKind.Health, spawn + new Vector3(2, .75f, 0), Color.white);
            Vector3 storage = Anchor("Room1_Right_Bottom").position;
            Station("Ammo2gun swap", StationKind.Weapon, new Vector3(storage.x - 1.5f, .75f, -14), Color.yellow).Weapon = 1;
            Station("Marked ammo crate 25", StationKind.AmmoCrate, spawn + new Vector3(-2, .65f, 2), CrateGreen).Quantity = 25;
            Station("Marked ammo crate 50", StationKind.AmmoCrate, waste + new Vector3(3.5f, .65f, 2), CrateGreen).Quantity = 50;
            Vector3 machinery = Anchor("Room3_Machine_A").position;
            device = Station("Connect card: fictional device", StationKind.Device, new Vector3(machinery.x - 2, 1, machinery.z), Color.red);
            foreach (string door in new[] { "Door_Room3_Left", "Door_Room3_Bottom", "Door_Room3_Top" })
            {
                var anchor = Anchor(door);
                var gate = Box("Card required — " + door, anchor.position + Vector3.up * 1.5f,
                    door.EndsWith("Left") ? new Vector3(.12f, 3, 2) : new Vector3(2, 3, .12f), new Color(1, .35f, 0));
                // Card barrier: physically blocks the player until the card is held. The door itself stays closed and
                // only the player can open it (with the card); a separate seal keeps enemies out while it is closed.
                var machineDoor = anchor.GetComponentInChildren<InteractiveDoor>();
                if (machineDoor) { machineDoor.RequiresCard = true; machineDoor.PlayerOnly = true; }
                var seal = new GameObject("Door seal — " + door); seal.transform.SetParent(transform); seal.transform.position = gate.transform.position;
                var block = seal.AddComponent<NavMeshObstacle>(); block.carving = true; block.shape = NavMeshObstacleShape.Box;
                block.size = door.EndsWith("Left") ? new Vector3(1.2f, 3, 2.4f) : new Vector3(2.4f, 3, 1.2f);
                doorSeals.Add((seal, machineDoor));
                Label(gate, "CARD REQUIRED"); gates.Add(gate);
            }
            // Add FB_Agents root and all 4 security robots (PG-01, SH-01, CD-03, FL-04)
            var agentsRoot = new GameObject("FB_Agents"); agentsRoot.transform.SetParent(transform);
            
            // PG-01 Patrol Guard (60 HP, Teal)
            var pg = CreateAgent(agentsRoot, "FB_Agent_PG01", "PG-01", 60, new Vector3(5, 0, 18), new Color(0, 0.8f, 0.8f), 0);
            pg.AddComponent<PatrolGuardBrain>().Route = new[] { new Vector3(5, 0, 18), new Vector3(5, 0, 22), new Vector3(7, 0, 22), new Vector3(7, 0, 18) };
            // SH-01 Sound Hunter (80 HP, Orange) with IS decision brain
            var shObj = CreateAgent(agentsRoot, "FB_Agent_SH01", "SH-01", 80, exit + Vector3.left * 3, new Color(1, 0.5f, 0), 1);
            shObj.AddComponent<FacilityBreach.AI.SoundHunter.FacilityBreachSoundHunterBrain>();
            // CD-03 Cover Defender (90 HP, Violet)
            CreateAgent(agentsRoot, "FB_Agent_CD03", "CD-03", 90, new Vector3(17.5f, 0, 19.5f), new Color(0.6f, 0.2f, 0.8f), 2).AddComponent<CoverDefenderBrain>();
            // FL-04 Flanker (70 HP, Lime)
            var flanker = CreateAgent(agentsRoot, "FB_Agent_FL04", "FL-04", 70, new Vector3(13, 0, 21), new Color(0.7f, 1, 0.2f), 3);
            flanker.AddComponent<FlankerBrain>();
            flanker.AddComponent<RestingWallFacing>(); // rests facing the nearest wall; turns to the player on sight

            foreach (var collider in FindObjectsByType<BoxCollider>())
                if (collider.transform.parent && collider.transform.parent.name == "Generated_Facility" && collider.bounds.size.y >= 3) mapWalls.Add(collider);
            SetPause(false); Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        private GameObject CreateAgent(GameObject parent, string name, string identity, int hp, Vector3 position, Color color, int visualIndex)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Capsule); obj.name = name; obj.transform.SetParent(parent.transform);
            if (Navigation && NavMesh.SamplePosition(position, out var safePost, 3, NavMesh.AllAreas)) position = safePost.position;
            obj.transform.position = position;
            var hitbox = obj.GetComponent<CapsuleCollider>(); hitbox.center = Vector3.up * .9f; hitbox.height = 1.8f; hitbox.radius = .35f;
            var mat = new Material(Shader.Find("Standard")); mat.color = color; ownedMaterials.Add(mat);
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            if (EnemyVisuals.Length > visualIndex && EnemyVisuals[visualIndex])
            {
                var visual = Instantiate(EnemyVisuals[visualIndex], obj.transform); visual.name = "ModelVisual";
                RemoveVisualColliders(visual);
                obj.GetComponent<Renderer>().enabled = false;
                if (identity == "SH-01") obj.AddComponent<SoundHunterTacticalGear>().Build(visual);
                else if (identity == "CD-03") obj.AddComponent<CoverDefenderArmor>().Build(visual);
                else if (identity == "PG-01") obj.AddComponent<EnemyWeaponEffects>().BuildRifle(visual, new Color(.14f, .16f, .18f), new Color(0, .8f, .8f), new Color(1f, .85f, .4f));
                else if (identity == "FL-04") obj.AddComponent<EnemyWeaponEffects>().BuildRifle(visual, new Color(.14f, .16f, .14f), new Color(.7f, 1f, .2f), new Color(1f, .85f, .4f));
            }
            else Debug.LogWarning("Missing robot visual: " + identity, obj);
            if (identity == "CD-03")
            {
                Vector3 facing = device.transform.position - position; facing.y = 0;
                if (facing.sqrMagnitude > .01f) obj.transform.rotation = Quaternion.LookRotation(facing);
            }
            foreach (var gate in gates) Physics.IgnoreCollision(hitbox, gate.GetComponent<Collider>());
            var adapter = obj.AddComponent<EnemyCombatAdapter>(); adapter.Identity = identity; adapter.MaximumHealth = hp;
            obj.AddComponent<SoundHunterNavMeshMover>();
            obj.AddComponent<EnemyModelAnimation>();
            obj.AddComponent<EnemyPresentation>();
            return obj;
        }
        private static void RemoveVisualColliders(GameObject visual)
        {
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
        }
        private void UpdateWeaponVisual()
        {
            if (equippedVisual) { equippedVisual.SetActive(false); Destroy(equippedVisual); }
            if (WeaponViewModels.Length <= Resources.Equipped || !WeaponViewModels[Resources.Equipped]) return;
            equippedVisual = Instantiate(WeaponViewModels[Resources.Equipped], view.transform);
            equippedVisual.transform.localPosition = new Vector3(.18f, -.22f, .5f);
            equippedVisual.transform.localScale = Vector3.one * .7f;
            equippedVisual.transform.localRotation = Quaternion.identity;
            RemoveVisualColliders(equippedVisual);
            foreach (var node in equippedVisual.GetComponentsInChildren<Transform>()) if (node.name == "Muzzle") { muzzle = node; break; }
            var obstruction = view.GetComponent<FpsViewModelObstruction>();
            if (!obstruction) obstruction = view.gameObject.AddComponent<FpsViewModelObstruction>();
            obstruction.Initialize(view, player ? player.transform : null, equippedVisual.transform, muzzle);
            if (!bulletTracer) bulletTracer = gameObject.GetComponent<FpsBulletTracer>();
            if (!bulletTracer) bulletTracer = gameObject.AddComponent<FpsBulletTracer>();
            bulletTracer.NotifyWeaponChanged(muzzle);
            if (!muzzleFlash) muzzleFlash = gameObject.GetComponent<FpsMuzzleFlashV2>();
            if (!muzzleFlash) muzzleFlash = gameObject.AddComponent<FpsMuzzleFlashV2>();
            muzzleFlash.NotifyWeaponChanged(muzzle);
            if (!impactSparks) impactSparks = gameObject.GetComponent<FpsImpactSparks>();
            if (!impactSparks) impactSparks = gameObject.AddComponent<FpsImpactSparks>();
        }
        private GameObject Box(string label, Vector3 position, Vector3 scale, Color color)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = label; obj.transform.SetParent(transform);
            obj.transform.position = position; obj.transform.localScale = scale;
            var material = new Material(Shader.Find("Standard")); material.color = color; ownedMaterials.Add(material);
            obj.GetComponent<Renderer>().sharedMaterial = material; return obj;
        }
        // Visual-only barrel cluster over the waste obstacle's collider: dark metal drums with black bands, some with a glowing green lid.
        private void BuildToxicBarrels(Vector3 center)
        {
            var holder = new GameObject("FB_ToxicWaste_Barrels"); holder.transform.SetParent(transform);
            Material Mat(Color color, bool glow)
            {
                var m = new Material(Shader.Find("Standard")) { color = color };
                if (glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(.2f, 1f, .25f) * 1.6f); }
                ownedMaterials.Add(m); return m;
            }
            var metal = Mat(new Color(.2f, .23f, .27f), false);
            var band = Mat(new Color(.03f, .03f, .03f), false);
            var glowLid = Mat(new Color(.25f, .8f, .3f), true);
            int index = 0;
            foreach (var x in new[] { -1.6f, 0f, 1.6f })
                foreach (var z in new[] { -1.5f, 0f, 1.5f })
                {
                    var at = center + new Vector3(x, 0, z);
                    Drum(holder.transform, "Barrel", at + Vector3.up * .5f, new Vector3(.9f, .5f, .9f), metal);
                    Drum(holder.transform, "Barrel band", at + Vector3.up * .12f, new Vector3(.95f, .04f, .95f), band);
                    Drum(holder.transform, "Barrel band", at + Vector3.up * .88f, new Vector3(.95f, .04f, .95f), band);
                    Drum(holder.transform, "Barrel lid", at + Vector3.up * 1.005f, new Vector3(.8f, .01f, .8f), index % 3 == 0 ? glowLid : metal);
                    index++;
                }
        }
        private static void Drum(Transform parent, string drumName, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = drumName; Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent); part.transform.position = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
        // The generator's white "scale reference" capsules at the entrance are design aids; hide them and put relevant props there.
        private void ReplaceScaleReferencesWithEntranceProps()
        {
            foreach (var reference in new[] { "Player_Scale_Reference", "NPC_Scale_Reference" })
            {
                var marker = GameObject.Find(reference);
                if (marker) marker.SetActive(false);
            }
            var holder = new GameObject("FB_EntranceProps"); holder.transform.SetParent(transform);
            GameObject Prop(string propName, Vector3 position, Vector3 scale, Color color, bool solid = true, float yaw = 0)
            {
                var part = Box(propName, position, scale, color);
                part.transform.SetParent(holder.transform); part.transform.rotation = Quaternion.Euler(0, yaw, 0);
                if (!solid) Destroy(part.GetComponent<Collider>());
                return part;
            }
            // Supply crates stacked beside the entrance
            var crate = new Color(.42f, .3f, .18f); var slat = new Color(.24f, .17f, .1f);
            Vector3 stack = new Vector3(-17, 0, -26.5f); // beside the spawn path (entrance door is at x -12.5)
            Prop("Entrance crate", stack + Vector3.up * .45f, new Vector3(.95f, .9f, .95f), crate);
            Prop("Entrance crate slat", stack + new Vector3(0, .45f, -.48f), new Vector3(1f, .12f, .03f), slat, false);
            Prop("Entrance crate slat", stack + new Vector3(0, .15f, -.48f), new Vector3(1f, .12f, .03f), slat, false);
            Prop("Entrance crate slat", stack + new Vector3(0, .75f, -.48f), new Vector3(1f, .12f, .03f), slat, false);
            Prop("Entrance crate top", stack + new Vector3(.05f, 1.25f, 0), new Vector3(.7f, .7f, .7f), new Color(.5f, .36f, .21f), true, 20);
            // Steel equipment locker with door seam, vents and handle, facing into the facility (+Z)
            var steel = new Color(.2f, .28f, .34f); var dark = new Color(.06f, .08f, .1f);
            Vector3 locker = new Vector3(-7.5f, 0, -26);
            Prop("Entrance locker", locker + Vector3.up * .95f, new Vector3(.8f, 1.9f, .5f), steel);
            Prop("Locker seam", locker + new Vector3(0, .95f, .255f), new Vector3(.02f, 1.7f, .01f), dark, false);
            for (int i = 0; i < 3; i++)
                Prop("Locker vent", locker + new Vector3(-.2f, 1.6f - i * .08f, .255f), new Vector3(.25f, .025f, .01f), dark, false);
            Prop("Locker handle", locker + new Vector3(.08f, 1.0f, .27f), new Vector3(.04f, .18f, .04f), new Color(.9f, .75f, .15f), false);
            Prop("Locker base", locker + new Vector3(0, .04f, 0), new Vector3(.86f, .08f, .56f), dark, false);
        }
        // Scene blood marks are loose objects; any mark lying on a door panel must swing with the door instead of hanging in the doorway.
        private void AttachBloodMarksToDoors()
        {
            var root = GameObject.Find("Facility_BloodMarks");
            if (!root) return;
            var doors = FindObjectsByType<InteractiveDoor>();
            var marks = new List<Transform>();
            foreach (Transform mark in root.transform) marks.Add(mark);
            foreach (var mark in marks)
            {
                var renderers = mark.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var area = renderers[0].bounds;
                foreach (var r in renderers) area.Encapsulate(r.bounds);
                area.Expand(.3f); // marks sit a few cm off the door surface
                foreach (var door in doors)
                {
                    bool onDoor = false;
                    foreach (var panel in door.GetComponentsInChildren<Collider>())
                        if (!panel.isTrigger && panel.bounds.Intersects(area)) { onDoor = true; break; }
                    if (onDoor) { mark.SetParent(door.transform, true); Debug.Log($"Blood mark {mark.name} attached to {door.name}"); break; }
                }
            }
        }
        private static readonly Color CrateGreen = new Color(.29f, .40f, .27f);
        private ResourceStation Station(string name, StationKind kind, Vector3 position, Color color)
        {
            position = ClearPlacement(position, kind == StationKind.Card ? new Vector3(.2f, .05f, .125f) : Vector3.one * .25f);
            var obj = Box(name, position, kind == StationKind.Card ? new Vector3(.4f, .1f, .25f) : Vector3.one * .5f, color);
            var station = obj.AddComponent<ResourceStation>(); station.Kind = kind; station.PickupModels = WeaponPickupModels; stations.Add(station); if (dispatcher != null) dispatcher.RegisterStation(station); Label(obj, name); return station;
        }
        private Vector3 ClearPlacement(Vector3 desired, Vector3 halfSize)
        {
            Physics.SyncTransforms();
            // Bounded geometry queries around an authored anchor; never move the environment.
            for (int ring = 0; ring <= 4; ring++)
                for (int direction = 0; direction < 8; direction++)
                {
                    float angle = direction * Mathf.PI / 4;
                    Vector3 candidate = desired + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * ring * .5f;
                    if (!Physics.CheckBox(candidate, halfSize * .95f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) return candidate;
                }
            throw new System.InvalidOperationException("No clear pickup placement near authored anchor " + desired);
        }
        private void Label(GameObject obj, string text)
        {
            var label = new GameObject("Label"); label.transform.SetParent(obj.transform, false); label.transform.localPosition = Vector3.up * 1.5f;
            // Pickup names are provided by the contextual HUD instead of giant world text.
            label.SetActive(false);
        }
        private void Update()
        {
            if (!player) return;
            GameplayDiagnostics.Enabled = DevelopmentDiagnostics;
            if (Run.Running && Input.GetKeyDown(KeyCode.Escape)) SetPause(!Run.Paused);
            if (!Application.isBatchMode && Run.Running && Cursor.lockState != CursorLockMode.Locked && !Run.Paused) SetPause(true);
            player.InputEnabled = Run.Running && !Run.Paused;
            if (!player.InputEnabled)
            {
                waitForRelease = true; target = null;
                if (!Run.Running) { reload = 0; Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; if (ambiencePlayer) ambiencePlayer.Pause(); }
                return;
            }
            float dt = Time.deltaTime; Run.Tick(dt); GameplayTime += dt; Enemies.Tick(GameplayTime);
            if (!Run.Running) { CancelEnemyAttacks(); return; }
            foreach (var station in stations) station.Tick(dt);
            if (device) device.SetDeviceActive(Run.State != MissionState.Escape && Run.State != MissionState.Won);
            foreach (var gate in gates) gate.SetActive(!Run.HasCard);
            // Enemies can only pass a Machinery door the player has opened with the card
            foreach (var entry in doorSeals) entry.seal.SetActive(!Run.HasCard || !entry.door || !entry.door.IsOpen);
            feedbackTime -= dt; cadence = Mathf.Max(0, cadence - dt);
            if (reload > 0) { reload = Mathf.Max(0, reload - dt); if (reload == 0) Resources.Reload(); }
            if (waitForRelease)
            {
                if (!Input.GetMouseButton(0) && !Input.GetKey(KeyCode.E) && !Input.GetKey(KeyCode.R) && !Input.GetKey(KeyCode.Q)) waitForRelease = false;
                return;
            }
            target = FindInteractionTarget(out var rejection); InteractionRejection = rejection;
            if (Input.GetKeyDown(KeyCode.E)) TryInteract();
            if (Input.GetKeyDown(KeyCode.R) || Resources.Magazine == 0) BeginReload();
            if (Input.GetMouseButton(0)) TryFire();
            if (Input.GetKeyDown(KeyCode.Q)) ThrowCan();
            if (Run.ExitUnlocked && Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z), new Vector2(exit.x, exit.z)) < 1.2f && Run.Exit(Resources.Alive)) CancelEnemyAttacks();
        }
        public void BeginReload()
        { if (reload > 0) return; if (Resources.CanReload) reload = ReloadSeconds; else if (Resources.Reserve == 0) Say("NO RESERVE AMMO"); }
        public ResourceStation FindInteractionTarget(out string rejection)
        {
            rejection = "Aim at a pickup within interaction range";
            ResourceStation best = null; float bestScore = float.PositiveInfinity;
            foreach (var station in stations)
            {
                if (!station.CanOfferInteraction) continue;
                Vector3 delta = station.InteractionPoint - view.transform.position;
                float distance = delta.magnitude;
                if (distance > InteractionRange || Vector3.Dot(view.transform.forward, delta.normalized) < .55f) continue;
                if (NearestHit(view.transform.position, delta.normalized, distance + .03f, out var hit) && !hit.transform.IsChildOf(station.transform))
                { rejection = "Blocked by " + hit.collider.name; continue; }
                float score = distance + 2 * (1 - Vector3.Dot(view.transform.forward, delta.normalized));
                if (score < bestScore) { best = station; bestScore = score; }
            }
            return best;
        }
        public bool TryInteract()
        {
            if (dispatcher != null)
            {
                bool success = dispatcher.ExecuteInteraction(out var rejection, out var kind, out var targetObj);
                if (kind == InteractionTargetKind.Station && targetObj is ResourceStation st)
                {
                    target = st;
                    if (success && st.Kind == StationKind.Weapon) { reload = 0; UpdateWeaponVisual(); }
                }
                else if (kind != InteractionTargetKind.None)
                {
                    target = null;
                }
                else
                {
                    target = FindInteractionTarget(out _);
                }

                InteractionRejection = success ? "" : rejection;
                string msg = success
                    ? (kind == InteractionTargetKind.HeldBarricade ? "Released barricade"
                        : kind == InteractionTargetKind.Barricade ? "Grabbed barricade"
                        : kind == InteractionTargetKind.Door ? "Door toggled"
                        : "Interaction complete")
                    : rejection;
                Say(msg);
                GameplayDiagnostics.Trace($"E state={Run.State} accepted={AcceptsGameplay} kind={kind} candidate={(targetObj != null ? targetObj.ToString() : (target ? target.name : "none"))} distance={(target ? Vector3.Distance(view.transform.position, target.InteractionPoint) : -1):F2} result={success} reason={rejection}", this);
                return success;
            }

            target = FindInteractionTarget(out var fallbackRejection);
            if (!AcceptsGameplay) fallbackRejection = "Mission inactive, paused or player eliminated";
            bool used = AcceptsGameplay && target && target.Use(Run, Resources);
            if (used && target.Kind == StationKind.Weapon) { reload = 0; UpdateWeaponVisual(); }
            if (!used && AcceptsGameplay && target) fallbackRejection = target.RejectionReason;
            InteractionRejection = used ? "" : fallbackRejection;
            Say(used ? "Interaction complete" : fallbackRejection);
            GameplayDiagnostics.Trace($"E state={Run.State} accepted={AcceptsGameplay} candidate={(target ? target.name : "none")} distance={(target ? Vector3.Distance(view.transform.position, target.InteractionPoint) : -1):F2} result={used} reason={fallbackRejection}", this);
            return used;
        }
        public bool TryFire()
        {
            if (!AcceptsGameplay || reload > 0 || cadence > 0 || !Resources.Shoot()) return false;
            LastShotCollider = null;
            cadence = ShotInterval;
            Vector3 aim = view.transform.position + view.transform.forward * ShotRange;
            if (NearestHit(view.transform.position, view.transform.forward, ShotRange, out var cameraHit)) aim = cameraHit.point;
            Vector3 direction = aim - muzzle.position;
            // A long view model can put its muzzle inside/beyond nearby cover.
            // First check the camera-to-muzzle segment so that cover cannot be skipped.
            Vector3 muzzleOffset = muzzle.position - view.transform.position;
            bool obstructed = NearestHit(view.transform.position, muzzleOffset.normalized, muzzleOffset.magnitude, out var hit);
            bool hitFound = obstructed || NearestHit(muzzle.position, direction.normalized, Mathf.Min(ShotRange, direction.magnitude + .01f), out hit);
            if (hitFound)
            {
                LastShotCollider = hit.collider;
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                var enemy = hit.collider.GetComponentInParent<EnemyCombatAdapter>(); int before = enemy ? enemy.CurrentHealth : -1;
                bool damaged = damageable != null && damageable.TakeDamage(new DamageInfo(Resources.Damage[Resources.Equipped], hit.point, direction.normalized, player.gameObject));
                GameplayDiagnostics.Trace($"shot camera={view.transform.position} muzzle={muzzle.position} dir={direction.normalized} collider={hit.collider.name} layer={hit.collider.gameObject.layer} receiver={damageable?.GetType().Name ?? "none"} applied={damaged} HP={before}->{(enemy ? enemy.CurrentHealth : -1)}", this);
                Say(damaged ? enemy ? $"{enemy.Identity}: {enemy.CurrentHealth}/{enemy.MaximumHealth} HP{(enemy.IsDead ? " — INACTIVE" : "")}" : "HIT" : "Blocked: " + hit.collider.name);
                if (bulletTracer) bulletTracer.SpawnTracer(muzzle, hit.point, true);
                bool isEnvironment = damageable == null && enemy == null;
                if (isEnvironment && impactSparks) impactSparks.SpawnSparks(hit.point, hit.normal);
            }
            else
            {
                GameplayDiagnostics.Trace($"shot miss camera={view.transform.position} muzzle={muzzle.position} dir={direction.normalized}", this);
                if (bulletTracer) bulletTracer.SpawnTracer(muzzle, aim, false);
            }
            if (muzzleFlash) muzzleFlash.TriggerFlash(muzzle);
            if (audioPlayer) audioPlayer.PlayGunshot(Resources.Equipped);
            else FacilityAudioPlayer.Instance.PlayGunshot(Resources.Equipped);
            FacilityNoiseHub.Emit(player.transform.position, 35f, FacilityNoiseType.Gunshot, player.gameObject);
            SoundEventBus.Emit(player.transform.position, 40, SoundCategory.Gunshot, player.GetEntityId().GetHashCode());
            return true;
        }
        private bool NearestHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default; bool found = false;
            foreach (var hit in Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(player.transform)) continue;
                if (!found || hit.distance < nearest.distance) { nearest = hit; found = true; }
            }
            return found;
        }
        private void ThrowCan()
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere); obj.name = "FB_DistractionCan";
            obj.transform.SetParent(transform); obj.transform.position = view.transform.position + view.transform.forward * .6f; obj.transform.localScale = Vector3.one * .15f;
            var body = obj.AddComponent<Rigidbody>(); body.linearVelocity = view.transform.forward * 8 + Vector3.up * 2;
            Physics.IgnoreCollision(obj.GetComponent<Collider>(), player.GetComponent<Collider>());
            obj.AddComponent<CanImpact>();
            FacilityAudioPlayer.Instance.PlayProjectileLaunch(view.transform.position);
        }
        public void Say(string message) { feedback = message; feedbackTime = 2; }
        public void CancelEnemyAttacks()
        { foreach (var enemy in GetComponentsInChildren<EnemyCombatAdapter>()) enemy.CancelPendingAttack(); }
        public void SetPause(bool paused)
        {
            if (paused) CancelEnemyAttacks();
            Run.Paused = paused; waitForRelease = true; Input.ResetInputAxes();
            if (player) player.ClearInputs();
            Time.timeScale = Run.Running && !paused ? 1 : 0;
            Cursor.lockState = Run.Running && !paused ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
            if (ambiencePlayer)
            {
                if (paused) ambiencePlayer.Pause();
                else if (Run.Running) ambiencePlayer.Resume();
            }
            if (footstepController && paused)
            {
                footstepController.OnGameplayPaused();
            }
            if (alertController)
            {
                alertController.PauseSiren(paused);
            }
        }
        private void OnApplicationFocus(bool focus) { if (!focus && Run.Running) SetPause(true); }
        public void StartMission()
        {
            if (!DevelopmentDiagnostics && TargetMissionDuration > 0)
            {
                Run.ConfigureDuration(TargetMissionDuration);
            }
            Run.Start();
            SetPause(false);
            if (ambiencePlayer) ambiencePlayer.Play();
        }
        public void Restart() { Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (navigationInstance.valid) navigationInstance.Remove();
            Time.timeScale = 1;
            foreach (var material in ownedMaterials) if (material) Destroy(material);
            if (ambiencePlayer) ambiencePlayer.Stop(true);
            if (alertController) alertController.DeactivateEmergency();
            if (_btnTransparentTex) Destroy(_btnTransparentTex);
            if (_whiteTex) Destroy(_whiteTex);
        }

        // =========================================================
        // TACTICAL UI ENGINE (Industrial Slate, Warm Amber Glow, Corner Brackets)
        // =========================================================
        private static Texture2D _whiteTex;
        private static Texture2D WhiteTex
        {
            get
            {
                if (_whiteTex == null)
                {
                    _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    _whiteTex.SetPixel(0, 0, Color.white);
                    _whiteTex.Apply();
                }
                return _whiteTex;
            }
        }

        private static Texture2D _btnTransparentTex;
        private static bool _uiStylesReady;

        private GUIStyle _hudTitleStyle;
        private GUIStyle _hudLabelStyle;
        private GUIStyle _hudValueStyle;
        private GUIStyle _hudBadgeStyle;
        private static GUIStyle _btnPrimaryStyle;
        private static GUIStyle _btnSecondaryStyle;
        private GUIStyle _briefingHeaderStyle;
        private GUIStyle _briefingBodyStyle;
        private GUIStyle _briefingAlertStyle;
        private GUIStyle _objectiveStyle;
        private GUIStyle _timerStyle;

        private static Texture2D MakeSolidTex(int w, int h, Color col)
        {
            Color[] pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private static bool DrawTacticalButton(Rect r, string text, bool isPrimary = false)
        {
            Vector2 mouse = Event.current.mousePosition;
            bool hover = r.Contains(mouse);

            Color bgColor = isPrimary
                ? (hover ? new Color(1.00f, 0.68f, 0.12f, 0.98f) : new Color(0.85f, 0.54f, 0.08f, 0.92f))
                : (hover ? new Color(0.24f, 0.22f, 0.18f, 0.96f) : new Color(0.14f, 0.13f, 0.11f, 0.92f));

            Color borderColor = isPrimary
                ? (hover ? new Color(1.00f, 0.88f, 0.40f, 1.0f) : new Color(1.00f, 0.76f, 0.18f, 0.85f))
                : (hover ? new Color(1.00f, 0.80f, 0.25f, 0.90f) : new Color(0.85f, 0.60f, 0.12f, 0.50f));

            Color textColor = isPrimary
                ? (hover ? new Color(0.06f, 0.04f, 0.02f, 1.0f) : Color.white)
                : (hover ? new Color(1.00f, 0.82f, 0.25f, 1.0f) : new Color(0.95f, 0.95f, 0.92f, 1.0f));

            // Solid clean background fill (no texture stretching or color bleed)
            UIDrawRect(r, bgColor);

            // Clean 1px outline border
            UIDrawRect(new Rect(r.x, r.y, r.width, 1), borderColor);
            UIDrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), borderColor);
            UIDrawRect(new Rect(r.x, r.y, 1, r.height), borderColor);
            UIDrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), borderColor);

            // Corner accent markers matching tactical UI aesthetic
            const float cl = 5f;
            const float ct = 1.5f;
            Color cornerCol = isPrimary ? borderColor : (hover ? new Color(1.00f, 0.80f, 0.25f, 1.0f) : new Color(1.00f, 0.76f, 0.18f, 0.85f));
            UIDrawRect(new Rect(r.x, r.y, cl, ct), cornerCol);
            UIDrawRect(new Rect(r.x, r.y, ct, cl), cornerCol);
            UIDrawRect(new Rect(r.xMax - cl, r.y, cl, ct), cornerCol);
            UIDrawRect(new Rect(r.xMax - ct, r.y, ct, cl), cornerCol);
            UIDrawRect(new Rect(r.x, r.yMax - ct, cl, ct), cornerCol);
            UIDrawRect(new Rect(r.x, r.yMax - cl, ct, cl), cornerCol);
            UIDrawRect(new Rect(r.xMax - cl, r.yMax - ct, cl, ct), cornerCol);
            UIDrawRect(new Rect(r.xMax - ct, r.yMax - cl, ct, cl), cornerCol);

            GUIStyle style = isPrimary ? _btnPrimaryStyle : _btnSecondaryStyle;
            if (style != null)
            {
                style.normal.textColor = textColor;
                style.hover.textColor = textColor;
                style.active.textColor = textColor;
                style.focused.textColor = textColor;
            }

            return GUI.Button(r, text, style ?? GUI.skin.button);
        }

        private void EnsureUIStyles()
        {
            if (_uiStylesReady && _hudTitleStyle != null) return;

            if (_btnTransparentTex == null)
                _btnTransparentTex = MakeSolidTex(2, 2, Color.clear);

            // Dynamic High-DPI System Font (zero blur, vector crispness at any scale)
            Font uiFont = GUI.skin.font;

            _hudTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _hudTitleStyle.normal.textColor = new Color(1.00f, 0.76f, 0.18f, 1f); // Warm Tactical Amber / Gold

            _hudLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            _hudLabelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.80f, 1f);

            _hudValueStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            _hudValueStyle.normal.textColor = Color.white;

            _hudBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _briefingHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _briefingHeaderStyle.normal.textColor = new Color(1.00f, 0.76f, 0.18f, 1f);

            _briefingBodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 12,
                wordWrap = false,
                richText = true,
                alignment = TextAnchor.UpperLeft
            };
            _briefingBodyStyle.normal.textColor = new Color(0.92f, 0.92f, 0.90f, 1f);

            _briefingAlertStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _briefingAlertStyle.normal.textColor = new Color(1.0f, 0.82f, 0.30f);

            _objectiveStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _objectiveStyle.normal.textColor = Color.white;

            _timerStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _timerStyle.normal.textColor = new Color(1.00f, 0.76f, 0.18f, 1f);

            // Primary Button style with transparent background (rendered with crisp geometry via DrawTacticalButton)
            _btnPrimaryStyle = new GUIStyle(GUI.skin.button)
            {
                font = uiFont,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _btnPrimaryStyle.normal.background = _btnTransparentTex;
            _btnPrimaryStyle.hover.background = _btnTransparentTex;
            _btnPrimaryStyle.active.background = _btnTransparentTex;
            _btnPrimaryStyle.focused.background = _btnTransparentTex;

            // Secondary Button style with transparent background
            _btnSecondaryStyle = new GUIStyle(GUI.skin.button)
            {
                font = uiFont,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _btnSecondaryStyle.normal.background = _btnTransparentTex;
            _btnSecondaryStyle.hover.background = _btnTransparentTex;
            _btnSecondaryStyle.active.background = _btnTransparentTex;
            _btnSecondaryStyle.focused.background = _btnTransparentTex;

            _uiStylesReady = true;
        }

        private static void UIDrawRect(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, WhiteTex);
            GUI.color = old;
        }

        private static void UIDrawPanel(Rect r, Color? border = null, Color? bg = null)
        {
            Color bgColor = bg ?? new Color(0.06f, 0.06f, 0.06f, 0.92f);
            Color borderColor = border ?? new Color(0.85f, 0.60f, 0.12f, 0.45f);
            Color cornerColor = border ?? new Color(1.00f, 0.76f, 0.18f, 0.95f);

            UIDrawRect(r, bgColor);

            UIDrawRect(new Rect(r.x, r.y, r.width, 1), borderColor);
            UIDrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), borderColor);
            UIDrawRect(new Rect(r.x, r.y, 1, r.height), borderColor);
            UIDrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), borderColor);

            const float cl = 7f;
            const float ct = 2f;
            UIDrawRect(new Rect(r.x, r.y, cl, ct), cornerColor);
            UIDrawRect(new Rect(r.x, r.y, ct, cl), cornerColor);
            UIDrawRect(new Rect(r.xMax - cl, r.y, cl, ct), cornerColor);
            UIDrawRect(new Rect(r.xMax - ct, r.y, ct, cl), cornerColor);
            UIDrawRect(new Rect(r.x, r.yMax - ct, cl, ct), cornerColor);
            UIDrawRect(new Rect(r.x, r.yMax - cl, ct, cl), cornerColor);
            UIDrawRect(new Rect(r.xMax - cl, r.yMax - ct, cl, ct), cornerColor);
            UIDrawRect(new Rect(r.xMax - ct, r.yMax - cl, ct, cl), cornerColor);
        }

        private void DrawTopLeftHUD()
        {
            Rect hudRect = new Rect(15, 15, 260, 106);
            UIDrawPanel(hudRect, new Color(0.85f, 0.60f, 0.12f, 0.50f));

            UIDrawRect(new Rect(hudRect.x + 8, hudRect.y + 7, 3, 14), new Color(1.00f, 0.76f, 0.18f, 1f));
            GUI.Label(new Rect(hudRect.x + 15, hudRect.y + 6, 150, 16), "FACILITY BREACH", _hudTitleStyle);
            GUI.Label(new Rect(hudRect.xMax - 70, hudRect.y + 6, 60, 16), "TACTICAL", _hudLabelStyle);

            UIDrawRect(new Rect(hudRect.x + 8, hudRect.y + 24, hudRect.width - 16, 1), new Color(0.85f, 0.60f, 0.12f, 0.25f));

            // Health Bar
            float hpY = hudRect.y + 29;
            GUI.Label(new Rect(hudRect.x + 10, hpY, 45, 16), "HP", _hudLabelStyle);
            int hp = Resources.Health;
            Color hpCol = hp > 50 ? new Color(0.00f, 0.90f, 0.50f) : hp > 25 ? new Color(1.0f, 0.75f, 0.10f) : new Color(1.0f, 0.20f, 0.20f);
            UIDrawRect(new Rect(hudRect.x + 45, hpY + 3, 125, 9), new Color(0.12f, 0.12f, 0.12f, 0.90f));
            float hpPct = Mathf.Clamp01(hp / 100f);
            UIDrawRect(new Rect(hudRect.x + 46, hpY + 4, 123 * hpPct, 7), hpCol);
            GUI.Label(new Rect(hudRect.x + 175, hpY, 75, 16), $"{hp}/100", _hudValueStyle);

            // Ammo
            float ammoY = hudRect.y + 51;
            GUI.Label(new Rect(hudRect.x + 10, ammoY, 50, 16), $"GUN-{Resources.Equipped + 1}", _hudLabelStyle);
            if (reload > 0)
            {
                bool flash = Mathf.PingPong(Time.time * 4f, 1f) > 0.4f;
                Color reloadCol = flash ? new Color(1.0f, 0.70f, 0.10f) : new Color(1.0f, 0.20f, 0.20f);
                UIDrawRect(new Rect(hudRect.x + 65, ammoY + 1, 95, 14), new Color(reloadCol.r, reloadCol.g, reloadCol.b, 0.25f));
                _hudBadgeStyle.normal.textColor = reloadCol;
                GUI.Label(new Rect(hudRect.x + 65, ammoY, 95, 16), "RELOADING", _hudBadgeStyle);
            }
            else
            {
                GUI.Label(new Rect(hudRect.x + 130, ammoY, 120, 16), $"{Resources.Magazine} / {Resources.Reserve}", _hudValueStyle);
            }

            // Keycard
            float cardY = hudRect.y + 73;
            GUI.Label(new Rect(hudRect.x + 10, cardY, 65, 16), "KEYCARD", _hudLabelStyle);
            Rect cardPillRect = new Rect(hudRect.x + 145, cardY + 1, 100, 16);
            if (Run.HasCard)
            {
                UIDrawRect(cardPillRect, new Color(0.00f, 0.85f, 0.50f, 0.25f));
                UIDrawRect(new Rect(cardPillRect.x, cardPillRect.y, cardPillRect.width, 1), new Color(0.00f, 0.95f, 0.60f, 0.8f));
                UIDrawRect(new Rect(cardPillRect.x, cardPillRect.yMax - 1, cardPillRect.width, 1), new Color(0.00f, 0.95f, 0.60f, 0.8f));
                _hudBadgeStyle.normal.textColor = new Color(0.10f, 1.0f, 0.60f);
                GUI.Label(cardPillRect, "ACQUIRED", _hudBadgeStyle);
            }
            else
            {
                UIDrawRect(cardPillRect, new Color(1.0f, 0.40f, 0.10f, 0.20f));
                UIDrawRect(new Rect(cardPillRect.x, cardPillRect.y, cardPillRect.width, 1), new Color(1.0f, 0.50f, 0.10f, 0.6f));
                UIDrawRect(new Rect(cardPillRect.x, cardPillRect.yMax - 1, cardPillRect.width, 1), new Color(1.0f, 0.50f, 0.10f, 0.6f));
                _hudBadgeStyle.normal.textColor = new Color(1.0f, 0.65f, 0.20f);
                GUI.Label(cardPillRect, "MISSING", _hudBadgeStyle);
            }
        }

        private void DrawCenterMenu()
        {
            float panelWidth = 460f;
            float panelHeight = controls && Run.State == MissionState.Menu ? 300f : 255f;
            Rect menuRect = new Rect(Screen.width / 2f - panelWidth / 2f, Screen.height / 2f - panelHeight / 2f, panelWidth, panelHeight);

            Color headerAccent = Run.State == MissionState.Won ? new Color(0.0f, 0.95f, 0.50f) :
                                 Run.State == MissionState.Lost ? new Color(1.0f, 0.25f, 0.25f) :
                                 Run.Paused ? new Color(1.0f, 0.75f, 0.20f) :
                                 new Color(1.00f, 0.76f, 0.18f);

            UIDrawPanel(menuRect, headerAccent);

            UIDrawRect(new Rect(menuRect.x + 1, menuRect.y + 1, menuRect.width - 2, 28), new Color(headerAccent.r, headerAccent.g, headerAccent.b, 0.18f));
            UIDrawRect(new Rect(menuRect.x + 1, menuRect.y + 29, menuRect.width - 2, 1), new Color(headerAccent.r, headerAccent.g, headerAccent.b, 0.45f));

            string title = Run.State == MissionState.Won ? "// MISSION COMPLETE" :
                           Run.State == MissionState.Lost ? $"// FAILED: {Run.LossReason}" :
                           Run.Paused ? "// SYSTEM PAUSED" :
                           "// MISSION BRIEFING : FACILITY BREACH";
            _briefingHeaderStyle.normal.textColor = headerAccent;
            GUI.Label(new Rect(menuRect.x, menuRect.y + 5, menuRect.width, 22), title, _briefingHeaderStyle);

            if (Run.State == MissionState.Menu)
            {
                if (!controls)
                {
                    float contentY = menuRect.y + 36;
                    string dur = TargetMissionDuration <= 180f ? "3 Minutes" : "5 Minutes";

                    GUI.Label(new Rect(menuRect.x + 18, contentY, menuRect.width - 36, 72),
                        $"<b>[OBJECTIVE]</b> Infiltrate facility and retrieve access card near toxic waste.\n" +
                        $"<b>[TARGET]</b> Connect card to device in Machinery.\n" +
                        $"<b>[TIME LIMIT]</b> {dur} before emergency meltdown lockdown.\n" +
                        $"<b>[ENGAGEMENT]</b> Combat is optional. Escape alive.", _briefingBodyStyle);

                    Rect warnRect = new Rect(menuRect.x + 18, contentY + 76, menuRect.width - 36, 24);
                    UIDrawRect(warnRect, new Color(1.0f, 0.70f, 0.10f, 0.12f));
                    UIDrawRect(new Rect(warnRect.x, warnRect.y, 3, warnRect.height), new Color(1.0f, 0.75f, 0.15f, 0.9f));
                    GUI.Label(new Rect(warnRect.x + 8, warnRect.y + 3, warnRect.width - 12, 18), "ALERT: Hostile patrols active. Avoid detection where possible.", _briefingAlertStyle);

                    float btnY = menuRect.y + 146;
                    if (DrawTacticalButton(new Rect(menuRect.x + 18, btnY, menuRect.width - 36, 38), "START MISSION", true))
                    {
                        StartMission();
                    }
                    if (DrawTacticalButton(new Rect(menuRect.x + 18, btnY + 44, menuRect.width - 36, 28), "VIEW CONTROLS", false))
                    {
                        controls = true;
                    }
                }
                else
                {
                    float cY = menuRect.y + 34;
                    _hudTitleStyle.normal.textColor = new Color(1.00f, 0.76f, 0.18f);
                    GUI.Label(new Rect(menuRect.x + 18, cY, 250, 18), "OPERATIONAL CONTROLS", _hudTitleStyle);

                    float gridY = cY + 22;
                    string col1 = "<b>[W][A][S][D]</b> Move\n" +
                                  "<b>[SHIFT]</b> Tactical Sprint\n" +
                                  "<b>[SPACE]</b> Jump / Vault\n" +
                                  "<b>[MOUSE]</b> Look / Aim";
                    string col2 = "<b>[L-CLICK]</b> Fire Weapon\n" +
                                  "<b>[R]</b> Reload Magazine\n" +
                                  "<b>[E]</b> Interact (Doors/Cards)\n" +
                                  "<b>[F]</b> Kick Barrels & Props\n" +
                                  "<b>[ESC]</b> Pause Mission";

                    GUI.Label(new Rect(menuRect.x + 18, gridY, 205, 105), col1, _briefingBodyStyle);
                    GUI.Label(new Rect(menuRect.x + 235, gridY, 205, 105), col2, _briefingBodyStyle);

                    float btnY = menuRect.y + 195;
                    if (DrawTacticalButton(new Rect(menuRect.x + 18, btnY, 200, 34), "< BRIEFING", false))
                    {
                        controls = false;
                    }
                    if (DrawTacticalButton(new Rect(menuRect.x + 225, btnY, 215, 34), "START MISSION", true))
                    {
                        StartMission();
                    }
                }
            }
            else if (Run.Paused && Run.Running)
            {
                float btnY = menuRect.y + 50;
                if (DrawTacticalButton(new Rect(menuRect.x + 30, btnY, menuRect.width - 60, 42), "RESUME MISSION", true))
                {
                    SetPause(false);
                }
                if (DrawTacticalButton(new Rect(menuRect.x + 30, btnY + 52, menuRect.width - 60, 34), "RESTART", false))
                {
                    Restart();
                }
                if (DrawTacticalButton(new Rect(menuRect.x + 30, btnY + 94, menuRect.width - 60, 34), "MAIN MENU", false))
                {
                    Restart();
                }
            }
            else
            {
                float infoY = menuRect.y + 55;
                if (Run.State == MissionState.Won)
                {
                    GUI.Label(new Rect(menuRect.x + 24, infoY, menuRect.width - 48, 50),
                        $"Completion Time: <b><color=#00E676>{Run.Elapsed:F1}s</color></b>\nStatus: Target Neutralized • Extraction Confirmed", _briefingBodyStyle);
                }
                else
                {
                    GUI.Label(new Rect(menuRect.x + 24, infoY, menuRect.width - 48, 50),
                        $"Status: <b><color=#FF5252>KIA / Mission Terminated</color></b>\nCause: {Run.LossReason}", _briefingBodyStyle);
                }

                float btnY = menuRect.y + 130;
                if (DrawTacticalButton(new Rect(menuRect.x + 30, btnY, menuRect.width - 60, 42), Run.State == MissionState.Won ? "REPLAY MISSION" : "RETRY MISSION", true))
                {
                    Restart();
                }
            }
        }

        private void DrawObjectiveBanner()
        {
            Rect objRect = new Rect(15, Screen.height - 62, 520, 48);
            UIDrawPanel(objRect, new Color(0.85f, 0.60f, 0.12f, 0.40f));

            string objective = Run.State == MissionState.Escape ? "DEVICE DEACTIVATED — reach the exit" :
                               Run.State == MissionState.Deactivating ? $"Deactivating: {5 - Run.ShutdownRemaining:F1}/5 seconds" :
                               !Run.HasCard ? "Find the access card near toxic waste" :
                               player.transform.position.x > 10 && player.transform.position.z > 12 && player.transform.position.z < 24 ? "Connect the card to deactivate the device" : "Use the card to enter Machinery";

            string prompt = feedbackTime > 0 ? feedback : (dispatcher != null ? dispatcher.GetContextualPrompt(target) : target ? target.Prompt : "");
            if (string.IsNullOrEmpty(prompt) && kickController != null && kickController.HasKickableTarget(out var propName)) prompt = "F: Kick " + propName;

            UIDrawRect(new Rect(objRect.x + 8, objRect.y + 8, 3, 16), new Color(1.00f, 0.76f, 0.18f, 1f));
            GUI.Label(new Rect(objRect.x + 18, objRect.y + 7, objRect.width - 28, 18), objective, _objectiveStyle);

            if (!string.IsNullOrEmpty(prompt))
            {
                _hudLabelStyle.normal.textColor = new Color(1.00f, 0.85f, 0.35f, 0.95f);
                GUI.Label(new Rect(objRect.x + 18, objRect.y + 26, objRect.width - 28, 16), $"[ {prompt} ]", _hudLabelStyle);
            }

            if (Run.State == MissionState.Deactivating)
            {
                float pct = 1 - (float)Run.ShutdownRemaining / 5f;
                UIDrawRect(new Rect(objRect.x, objRect.y - 6, objRect.width, 4), new Color(0.12f, 0.12f, 0.12f, 0.9f));
                UIDrawRect(new Rect(objRect.x, objRect.y - 6, objRect.width * pct, 4), new Color(1.00f, 0.76f, 0.18f, 1f));
            }
        }

        private void DrawTimerAndMinimap()
        {
            int seconds = Mathf.CeilToInt((float)Run.Remaining);
            Rect timerRect = new Rect(Screen.width - 245, 15, 230, 36);

            bool isAlert = seconds <= 60 && Run.Running;
            Color timerAccent = isAlert ? (Mathf.PingPong(Time.time * 3f, 1f) > 0.4f ? new Color(1.0f, 0.2f, 0.2f) : new Color(0.7f, 0.1f, 0.1f)) :
                                          new Color(0.85f, 0.60f, 0.12f, 0.50f);

            UIDrawPanel(timerRect, timerAccent);

            _timerStyle.normal.textColor = isAlert ? new Color(1.0f, 0.25f, 0.25f) : new Color(1.00f, 0.76f, 0.18f);
            string timerText = $"{seconds / 60:00}:{seconds % 60:00}";
            GUI.Label(timerRect, timerText, _timerStyle);

            if (isAlert)
            {
                _hudBadgeStyle.normal.textColor = new Color(1.0f, 0.3f, 0.3f);
                GUI.Label(new Rect(timerRect.x, timerRect.y + 20, timerRect.width, 14), "MELTDOWN WARNING", _hudBadgeStyle);
            }

            // Crosshair in center screen
            Color crosshairCol = new Color(1.00f, 0.76f, 0.18f, 0.75f);
            UIDrawRect(new Rect(Screen.width / 2f - 4, Screen.height / 2f, 8, 1), crosshairCol);
            UIDrawRect(new Rect(Screen.width / 2f, Screen.height / 2f - 4, 1, 8), crosshairCol);

            // Minimap
            Rect mapRect = new Rect(Screen.width - 245, 55, 230, 330);
            UIDrawPanel(mapRect, new Color(0.85f, 0.60f, 0.12f, 0.35f));
            DrawMap(mapRect);
        }

        private void OnGUI()
        {
            if (!player) return;
            EnsureUIStyles();

            DrawTopLeftHUD();
            DrawObjectiveBanner();
            DrawTimerAndMinimap();

            if (Run.State == MissionState.Menu || Run.Paused || Run.State == MissionState.Won || Run.State == MissionState.Lost)
            {
                DrawCenterMenu();
            }
        }
        private Vector2 MapPoint(Rect rect, Vector3 world) => new Vector2(rect.x + (world.x + 21) / 42 * rect.width, rect.yMax - (world.z + 30) / 60 * rect.height);
        private void Dot(Rect rect, Vector3 world, Color color, string label)
        { Vector2 p = MapPoint(rect, world); GUI.color = color; GUI.DrawTexture(new Rect(p.x - 3, p.y - 3, 6, 6), Texture2D.whiteTexture); GUI.color = Color.white; GUI.Label(new Rect(p.x + 4, p.y - 8, 80, 20), label); }
        private void DrawMap(Rect rect)
        {
            GUI.Box(rect, "N ↑");
            foreach (var wall in mapWalls)
            {
                if (!wall) continue; var b = wall.bounds; var min = MapPoint(rect, b.min); var max = MapPoint(rect, b.max);
                GUI.color = new Color(.45f, .45f, .45f); GUI.DrawTexture(new Rect(min.x, max.y, Mathf.Max(1, max.x - min.x), Mathf.Max(1, min.y - max.y)), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            foreach (var door in FindObjectsByType<InteractiveDoor>()) Dot(rect, door.transform.position, Color.gray, "");
            foreach (var station in stations)
                if (station.Available && (station.Kind == StationKind.Card || station.Kind == StationKind.Health || station.Kind == StationKind.Device))
                    Dot(rect, station.transform.position, station.Kind == StationKind.Card ? Color.cyan : station.Kind == StationKind.Health ? Color.white : Color.red, station.Kind.ToString());
            Dot(rect, exit, Run.ExitUnlocked ? Color.green : Color.red, "Exit");
            Dot(rect, player.transform.position, Color.white, "▲");
            Vector2 facing = MapPoint(rect, player.transform.position + player.transform.forward * 2); GUI.DrawTexture(new Rect(facing.x, facing.y, 3, 3), Texture2D.whiteTexture);
            foreach (var enemy in FindObjectsByType<EnemyCombatAdapter>())
                if (!enemy.IsDead)
                    Dot(rect, enemy.transform.position, enemy.Identity.StartsWith("SH") ? new Color(1, 0.5f, 0) : enemy.Identity.StartsWith("PG") ? new Color(0, 0.8f, 0.8f) : enemy.Identity.StartsWith("CD") ? new Color(0.6f, 0.2f, 0.8f) : new Color(0.7f, 1, 0.2f), enemy.Identity);
            GUI.Label(new Rect(rect.x, rect.yMax + 5, rect.width, 50), "PG teal • SH orange\nCD violet • FL lime");
        }
    }
}
