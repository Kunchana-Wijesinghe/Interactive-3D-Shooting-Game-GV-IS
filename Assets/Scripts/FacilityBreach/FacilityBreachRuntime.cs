using System.Collections.Generic;
using FacilityBreach.Core;
using FacilityBreach.Interactions;
using FacilityBreach.Player;
using FacilityBreach.Combat;
using Perera.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FacilityBreach
{
    // Serialized scene root. Runtime additions are children; authored environment is never rebuilt.
    [DefaultExecutionOrder(-200)]
    public sealed class FacilityBreachRuntime : MonoBehaviour
    {
        public MissionRun Run { get; private set; } = new MissionRun();
        public PlayerResources Resources { get; private set; } = new PlayerResources();
        public EnemyRoster Enemies { get; private set; } = new EnemyRoster();
        public float InteractionRange = 2.5f, ShotRange = 50, ShotInterval = .2f, ReloadSeconds = 1.5f;
        private readonly List<ResourceStation> stations = new List<ResourceStation>();
        private readonly List<GameObject> gates = new List<GameObject>();
        private readonly List<Collider> mapWalls = new List<Collider>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private TemporaryPlayerAdapter player;
        private Camera view;
        private Vector3 exit;
        private Transform muzzle;
        private ResourceStation device;
        private ResourceStation target;
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
        private void Start()
        {
            view = Camera.main;
            if (!view) throw new System.InvalidOperationException("Prototype requires its existing Main Camera");
            var spawn = Anchor("Door_Main_Entrance").position + Vector3.forward * 1.75f;
            exit = Anchor("Door_Main_Exit").position - Vector3.forward * 1.75f;
            var root = new GameObject("FB_TemporaryPlayer_BandaraAdapterPending");
            root.transform.SetParent(transform); root.transform.position = new Vector3(spawn.x, .1f, spawn.z); root.tag = "Player";
            var body = root.AddComponent<CharacterController>(); body.height = 1.8f; body.radius = .3f; body.center = Vector3.up * .9f;
            player = root.AddComponent<TemporaryPlayerAdapter>(); player.View = view;
            root.AddComponent<PlayerDamageAdapter>().Runtime = this;
            view.transform.SetParent(root.transform); view.transform.localPosition = Vector3.up * 1.6f; view.transform.localRotation = Quaternion.identity;
            var muzzleObject = new GameObject("FB_Muzzle_VisualPending"); muzzleObject.transform.SetParent(view.transform);
            muzzleObject.transform.localPosition = new Vector3(.2f, -.15f, .45f); muzzle = muzzleObject.transform;
            // The committed scene lacks the waste root despite shipping its generator.
            // Add only an obstacle here; never call the destructive world generators.
            Vector3 waste = new Vector3(-2, 0, 20);
            var authoredWaste = GameObject.Find("Toxic_Waste_Obstacle");
            if (authoredWaste) waste = authoredWaste.transform.position;
            else
            {
                var obstacle = Box("FB_ToxicWaste_IntegrationPlaceholder", waste + Vector3.up * .5f, new Vector3(5.2f, 1, 4.8f), new Color(.2f, .5f, .1f));
                Label(obstacle, "TOXIC WASTE — NO ENTRY");
                var nav = obstacle.AddComponent<UnityEngine.AI.NavMeshObstacle>(); nav.carving = true; nav.shape = UnityEngine.AI.NavMeshObstacleShape.Box; nav.size = Vector3.one;
            }
            // Platform extents from the supplied obstacle script. Eastern edge outside containment.
            Station("Access card", StationKind.Card, waste + new Vector3(3.4f, .75f, 0), Color.cyan);
            Station("Ammo3gun swap", StationKind.Weapon, waste + new Vector3(3.4f, .75f, -1.5f), Color.yellow).Weapon = 2;
            Station("Entrance health +50 (HP <=50)", StationKind.Health, spawn + new Vector3(2, .75f, 0), Color.green);
            Vector3 storage = Anchor("Room1_Right_Bottom").position;
            Station("Ammo2gun swap", StationKind.Weapon, new Vector3(storage.x - 1.5f, .75f, -14), Color.yellow).Weapon = 1;
            Station("Marked ammo crate 25", StationKind.AmmoCrate, spawn + new Vector3(-2, .65f, 2), Color.gray).Quantity = 25;
            Station("Marked ammo crate 50", StationKind.AmmoCrate, waste + new Vector3(3.5f, .65f, 2), Color.gray).Quantity = 50;
            Vector3 machinery = Anchor("Room3_Machine_A").position;
            device = Station("Connect card: fictional device", StationKind.Device, new Vector3(machinery.x - 2, 1, machinery.z), Color.red);
            foreach (string door in new[] { "Door_Room3_Left", "Door_Room3_Bottom", "Door_Room3_Top" })
            {
                var anchor = Anchor(door);
                var gate = Box("Card required — " + door, anchor.position + Vector3.up * 1.5f,
                    door.EndsWith("Left") ? new Vector3(.12f, 3, 2) : new Vector3(2, 3, .12f), new Color(1, .35f, 0));
                var obstacle = gate.AddComponent<UnityEngine.AI.NavMeshObstacle>(); obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                obstacle.size = Vector3.one; obstacle.carving = true;
                Label(gate, "CARD REQUIRED"); gates.Add(gate);
            }
            // Add FB_Agents root and all 4 security robots (PG-01, SH-01, CD-03, FL-04)
            var agentsRoot = new GameObject("FB_Agents"); agentsRoot.transform.SetParent(transform);
            
            // PG-01 Patrol Guard (60 HP, Teal)
            CreateAgent(agentsRoot, "FB_Agent_PG01", "PG-01", 60, new Vector3(0, 1, 0), new Color(0, 0.8f, 0.8f));
            // SH-01 Sound Hunter (80 HP, Orange) with IS decision brain
            var shObj = CreateAgent(agentsRoot, "FB_Agent_SH01", "SH-01", 80, new Vector3(18, 1, -18), new Color(1, 0.5f, 0));
            shObj.AddComponent<FacilityBreach.AI.SoundHunter.FacilityBreachSoundHunterBrain>();
            // CD-03 Cover Defender (90 HP, Violet)
            CreateAgent(agentsRoot, "FB_Agent_CD03", "CD-03", 90, new Vector3(-8, 1, 12), new Color(0.6f, 0.2f, 0.8f));
            // FL-04 Flanker (70 HP, Lime)
            CreateAgent(agentsRoot, "FB_Agent_FL04", "FL-04", 70, new Vector3(-2, 1, 15), new Color(0.7f, 1, 0.2f));

            foreach (var collider in FindObjectsByType<BoxCollider>())
                if (collider.transform.parent && collider.transform.parent.name == "Generated_Facility" && collider.bounds.size.y >= 3) mapWalls.Add(collider);
            SetPause(false); Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        private GameObject CreateAgent(GameObject parent, string name, string identity, int hp, Vector3 position, Color color)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Capsule); obj.name = name; obj.transform.SetParent(parent.transform);
            obj.transform.position = position;
            var mat = new Material(Shader.Find("Standard")); mat.color = color; ownedMaterials.Add(mat);
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            var adapter = obj.AddComponent<EnemyCombatAdapter>(); adapter.Identity = identity; adapter.MaximumHealth = hp;
            return obj;
        }
        private GameObject Box(string label, Vector3 position, Vector3 scale, Color color)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = label; obj.transform.SetParent(transform);
            obj.transform.position = position; obj.transform.localScale = scale;
            var material = new Material(Shader.Find("Standard")); material.color = color; ownedMaterials.Add(material);
            obj.GetComponent<Renderer>().sharedMaterial = material; return obj;
        }
        private ResourceStation Station(string name, StationKind kind, Vector3 position, Color color)
        {
            position = ClearPlacement(position, kind == StationKind.Card ? new Vector3(.2f, .05f, .125f) : Vector3.one * .25f);
            var obj = Box(name, position, kind == StationKind.Card ? new Vector3(.4f, .1f, .25f) : Vector3.one * .5f, color);
            var station = obj.AddComponent<ResourceStation>(); station.Kind = kind; stations.Add(station); Label(obj, name); return station;
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
            var mesh = label.AddComponent<TextMesh>(); mesh.text = text; mesh.characterSize = .12f; mesh.fontSize = 40; mesh.anchor = TextAnchor.MiddleCenter;
        }
        private void Update()
        {
            if (!player) return;
            if (Run.Running && Input.GetKeyDown(KeyCode.Escape)) SetPause(!Run.Paused);
            if (Run.Running && Cursor.lockState != CursorLockMode.Locked && !Run.Paused) SetPause(true);
            player.InputEnabled = Run.Running && !Run.Paused;
            if (!player.InputEnabled)
            {
                waitForRelease = true; target = null;
                if (!Run.Running) { reload = 0; Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                return;
            }
            float dt = Time.deltaTime; Run.Tick(dt); GameplayTime += dt; Enemies.Tick(GameplayTime);
            if (!Run.Running) return;
            foreach (var station in stations) station.Tick(dt);
            foreach (var gate in gates) gate.SetActive(!Run.HasCard);
            feedbackTime -= dt; cadence = Mathf.Max(0, cadence - dt);
            if (reload > 0) { reload = Mathf.Max(0, reload - dt); if (reload == 0) Resources.Reload(); }
            if (waitForRelease)
            {
                if (!Input.GetMouseButton(0) && !Input.GetKey(KeyCode.E) && !Input.GetKey(KeyCode.R) && !Input.GetKey(KeyCode.Q)) waitForRelease = false;
                return;
            }
            target = null;
            if (Physics.Raycast(view.transform.position, view.transform.forward, out var interact, InteractionRange, ~0, QueryTriggerInteraction.Ignore))
                target = interact.collider.GetComponentInParent<ResourceStation>();
            if (target && Input.GetKeyDown(KeyCode.E))
            {
                bool used = target.Use(Run, Resources);
                if (used && target.Kind == StationKind.Weapon) reload = 0;
                Say(used ? "Interaction complete" : "Requirements not met");
            }
            if (Input.GetKeyDown(KeyCode.R) || Resources.Magazine == 0) BeginReload();
            if (Input.GetMouseButton(0)) Fire();
            if (Input.GetKeyDown(KeyCode.Q)) ThrowCan();
            if (Run.ExitUnlocked && Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z), new Vector2(exit.x, exit.z)) < 1.2f) Run.Exit(Resources.Alive);
        }
        private void BeginReload()
        { if (reload > 0) return; if (Resources.CanReload) reload = ReloadSeconds; else if (Resources.Reserve == 0) Say("NO RESERVE AMMO"); }
        private void Fire()
        {
            if (reload > 0 || cadence > 0 || !Resources.Shoot()) return;
            cadence = ShotInterval;
            Vector3 aim = view.transform.position + view.transform.forward * ShotRange;
            if (NearestHit(view.transform.position, view.transform.forward, ShotRange, out var cameraHit)) aim = cameraHit.point;
            Vector3 direction = aim - muzzle.position;
            if (NearestHit(muzzle.position, direction.normalized, Mathf.Min(ShotRange, direction.magnitude + .01f), out var hit))
            {
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                bool damaged = damageable != null && damageable.TakeDamage(new DamageInfo(Resources.Damage[Resources.Equipped], hit.point, direction.normalized, player.gameObject));
                Say(damaged ? "HIT" : "Impact");
            }
            SoundEventBus.Emit(player.transform.position, 40, SoundCategory.Gunshot, player.GetEntityId().GetHashCode());
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
        }
        private void Say(string message) { feedback = message; feedbackTime = 2; }
        private void SetPause(bool paused)
        {
            Run.Paused = paused; waitForRelease = true; Input.ResetInputAxes();
            if (player) player.ClearInputs();
            Time.timeScale = Run.Running && !paused ? 1 : 0;
            Cursor.lockState = Run.Running && !paused ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
        }
        private void OnApplicationFocus(bool focus) { if (!focus && Run.Running) SetPause(true); }
        private void Restart() { Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
        private void OnDestroy() { Time.timeScale = 1; foreach (var material in ownedMaterials) if (material) Destroy(material); }
        private void OnGUI()
        {
            if (!player) return;
            GUI.Box(new Rect(15, 15, 355, 130), $"FACILITY BREACH\nHP {Resources.Health}/100\nAmmo{Resources.Equipped + 1}gun   {Resources.Magazine}/{Resources.Reserve}\n{(reload > 0 ? "RELOADING" : "")}\nCARD: {(Run.HasCard ? "ACQUIRED" : "MISSING")}");
            string objective = Run.State == MissionState.Escape ? "DEVICE DEACTIVATED — reach the exit" : Run.State == MissionState.Deactivating ? $"Deactivating: {5 - Run.ShutdownRemaining:F1}/5 seconds" : !Run.HasCard ? "Find the access card near toxic waste" : player.transform.position.x > 10 && player.transform.position.z > 12 && player.transform.position.z < 24 ? "Connect the card to deactivate the device" : "Use the card to enter Machinery";
            GUI.Box(new Rect(15, Screen.height - 65, 570, 50), objective + "\n" + (target ? target.Prompt : feedbackTime > 0 ? feedback : ""));
            if (Run.State == MissionState.Deactivating)
            {
                GUI.color = Color.cyan;
                GUI.DrawTexture(new Rect(15, Screen.height - 75, 570 * (1 - (float)Run.ShutdownRemaining / 5), 6), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 10, 20, 25), "+");
            int seconds = Mathf.CeilToInt((float)Run.Remaining);
            GUI.Box(new Rect(Screen.width - 245, 15, 230, 35), $"{seconds / 60:00}:{seconds % 60:00}");
            DrawMap(new Rect(Screen.width - 245, 55, 230, 330));
            if (Run.State == MissionState.Menu || Run.Paused || Run.State == MissionState.Won || Run.State == MissionState.Lost)
            {
                GUILayout.BeginArea(new Rect(Screen.width / 2f - 210, Screen.height / 2f - 160, 420, 320), GUI.skin.box);
                GUILayout.Label(Run.State == MissionState.Won ? "MISSION COMPLETE" : Run.State == MissionState.Lost ? Run.LossReason : Run.Paused ? "PAUSED" : "FACILITY BREACH");
                if (Run.State == MissionState.Menu)
                {
                    GUILayout.Label("Retrieve the card near toxic waste. Connect it to the fictional device in Machinery within five minutes. Escape alive. Combat is optional.");
                    if (GUILayout.Button("Start Mission")) { Run.Start(); SetPause(false); }
                    if (GUILayout.Button("Controls")) controls = !controls;
                    if (controls) GUILayout.Label("WASD move • Mouse look • Shift sprint • E interact\nLeft click shoot • R reload • Q can • Escape pause");
                }
                else if (Run.Paused && Run.Running)
                {
                    if (GUILayout.Button("Resume")) SetPause(false);
                    if (GUILayout.Button("Restart")) Restart();
                    if (GUILayout.Button("Main Menu")) Restart();
                }
                else
                {
                    if (Run.State == MissionState.Won) GUILayout.Label($"Completion time: {Run.Elapsed:F1}s\nFour-agent challenge: unavailable (team AI pending)");
                    if (GUILayout.Button(Run.State == MissionState.Won ? "Replay" : "Retry")) Restart();
                }
                GUILayout.EndArea();
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
                    Dot(rect, station.transform.position, station.Kind == StationKind.Card ? Color.cyan : station.Kind == StationKind.Health ? Color.green : Color.red, station.Kind.ToString());
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
