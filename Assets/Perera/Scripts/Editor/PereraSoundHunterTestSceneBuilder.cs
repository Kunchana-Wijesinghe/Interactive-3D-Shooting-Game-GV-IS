using System.Collections.Generic;
using Perera.Combat;
using Perera.Core;
using Perera.SoundHunter;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Perera.EditorTools
{
    /// <summary>
    /// Builds Assets/Perera/Scenes/Perera_SoundHunterTest.unity (isolated; Prototype.unity untouched).
    /// Layout (top view, metres, rooms are 14 m cells):
    ///        C(0,14) --8-- D(14,14)[platform P(18,18), unreachable on foot]
    ///          |8            |8
    ///        A(0,0)  --40-- B(14,0)  [hunter patrol + cover wall]
    ///                        |30
    ///                      F(14,-14)           E(-28,0) sealed vault: acoustically disconnected
    /// Numbers are hearing link costs. A->B direct costs 40, but A-C-D-B costs 24, so Dijkstra uses the detour.
    /// Everything is labelled PLACEHOLDER primitives.
    /// </summary>
    public static class PereraSoundHunterTestSceneBuilder
    {
        public const string ScenePath = "Assets/Perera/Scenes/Perera_SoundHunterTest.unity";
        private const string NavPath = "Assets/Perera/Scenes/Perera_SoundHunterTest_NavMesh.asset";
        private const string MatDir = "Assets/Perera/Materials";

        private static Transform geometry;
        private static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        [MenuItem("Perera/Build Sound Hunter Test Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            mats.Clear();
            System.IO.Directory.CreateDirectory(MatDir);

            geometry = new GameObject("Geometry").transform;
            BuildGeometry();

            // ---------------- hearing graph ----------------
            var graphGo = new GameObject("HearingGraph");
            var graph = graphGo.AddComponent<SceneHearingGraph>();
            SetField(graph, "maxMappingDistance", 12f);

            var nA = Node(graphGo, "Room_A", new Vector3(0, 1, 0), new Vector3(14.4f, 6, 14.4f), new Vector3(0, 1, 0));
            var nB = Node(graphGo, "Room_B", new Vector3(14, 1, 0), new Vector3(14.4f, 6, 14.4f), new Vector3(0, 1, 0));
            var nC = Node(graphGo, "Room_C", new Vector3(0, 1, 14), new Vector3(14.4f, 6, 14.4f), new Vector3(0, 1, 0));
            var nD = Node(graphGo, "Room_D", new Vector3(14, 1, 14), new Vector3(14.4f, 6, 14.4f), new Vector3(0, 1, 0));
            var nF = Node(graphGo, "Room_F_far", new Vector3(14, 1, -14), new Vector3(14.4f, 6, 14.4f), new Vector3(0, 1, 0));
            var nE = Node(graphGo, "Vault_E_disconnected", new Vector3(-28, 1, 0), new Vector3(11.6f, 6, 11.6f), new Vector3(0, 1, 0));
            var nP = Node(graphGo, "Platform_P", new Vector3(18, 3.2f, 18), new Vector3(4, 3, 4), new Vector3(0, 0.3f, 0));

            Link(graphGo, "Link_A_C_doorway", nA, nC, 8f);
            Link(graphGo, "Link_C_D_doorway", nC, nD, 8f);
            Link(graphGo, "Link_D_B_doorway", nD, nB, 8f);
            Link(graphGo, "Link_A_B_closed_door", nA, nB, 40f);
            Link(graphGo, "Link_B_F_doorway", nB, nF, 30f);
            Link(graphGo, "Link_D_P_platform", nD, nP, 2f);
            // The vault (E) deliberately has no link: acoustically disconnected.
            graph.Rebuild();
            if (graph.Problems.Count > 0) Debug.LogError("Hearing graph problems: " + string.Join("; ", graph.Problems));

            // ---------------- anchors ----------------
            var anchors = new GameObject("TestAnchors").transform;
            var tp = new[]
            {
                Anchor(anchors, "TP1_RoomA_start", new Vector3(0, 1.1f, -3)),
                Anchor(anchors, "TP2_RoomC", new Vector3(0, 1.1f, 14)),
                Anchor(anchors, "TP3_RoomD", new Vector3(14, 1.1f, 14)),
                Anchor(anchors, "TP4_RoomB_open", new Vector3(15, 1.1f, 2)),
                Anchor(anchors, "TP5_RoomB_behind_cover", new Vector3(18.5f, 1.1f, 0)),
                Anchor(anchors, "TP6_RoomF_far", new Vector3(14, 1.1f, -14)),
                Anchor(anchors, "TP7_Vault_E", new Vector3(-28, 1.1f, 0)),
            };
            var sndA = Anchor(anchors, "Sound_RoomA", new Vector3(-3, 1.5f, -3));
            var sndC = Anchor(anchors, "Sound_RoomC", new Vector3(-3, 1.5f, 16));
            var sndD = Anchor(anchors, "Sound_RoomD_reachable", new Vector3(12, 1.5f, 12));
            var sndP = Anchor(anchors, "Sound_Platform_unreachable", new Vector3(18, 3.5f, 18));
            var sndE = Anchor(anchors, "Sound_Vault_disconnected", new Vector3(-28, 1.5f, 0));
            var pat1 = Anchor(anchors, "Patrol_1", new Vector3(9.5f, 0, 0));
            var pat2 = Anchor(anchors, "Patrol_2", new Vector3(13, 0, 0));

            // ---------------- navigation (test-only NavMeshAgent mover) ----------------
            var surface = geometry.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            if (surface.navMeshData != null)
            {
                AssetDatabase.CreateAsset(surface.navMeshData, NavPath);
                surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath);
                EditorUtility.SetDirty(surface);
            }

            // ---------------- player rig (placeholder for Bandara's controller) ----------------
            var player = new GameObject("Player_TestRig") { tag = "Player" };
            player.transform.position = tp[0].position;
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f;
            var playerHealth = player.AddComponent<Health>();

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(player.transform, false);
            pivot.transform.localPosition = new Vector3(0, 0.7f, 0);
            var camGo = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivot.transform, false);
            camGo.transform.localPosition = Vector3.zero; camGo.transform.localRotation = Quaternion.identity;
            if (camGo.GetComponent<Camera>() == null) camGo.AddComponent<Camera>();
            camGo.GetComponent<Camera>().nearClipPlane = 0.05f;

            var weaponGo = new GameObject("Weapon_Placeholder");
            weaponGo.transform.SetParent(camGo.transform, false);
            weaponGo.transform.localPosition = new Vector3(0.25f, -0.2f, 0.5f);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "WeaponBody_PLACEHOLDER";
            body.transform.SetParent(weaponGo.transform, false);
            body.transform.localScale = new Vector3(0.08f, 0.1f, 0.5f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(weaponGo.transform, false);
            muzzle.transform.localPosition = new Vector3(0, 0.02f, 0.3f);
            var hw = weaponGo.AddComponent<HitscanWeapon>();
            SetField(hw, "aimCamera", camGo.GetComponent<Camera>());
            SetField(hw, "muzzle", muzzle.transform);
            SetField(hw, "owner", player.transform);
            SetField(hw, "ownerHealth", playerHealth);

            var ctrl = player.AddComponent<TestFpsController>();
            SetField(ctrl, "cameraPivot", pivot.transform);
            player.AddComponent<CombatDebugHud>();

            // ---------------- hunter ----------------
            var hunterGo = GameObject.CreatePrimitive(PrimitiveType.Capsule); // memory lifetime set below
            hunterGo.name = "SoundHunter_PLACEHOLDER";
            hunterGo.transform.position = pat1.position + Vector3.up;
            hunterGo.transform.rotation = Quaternion.Euler(0, 90, 0);
            hunterGo.GetComponent<Renderer>().sharedMaterial = Mat("Hunter", new Color(0.85f, 0.15f, 0.15f));
            var agent = hunterGo.AddComponent<NavMeshAgent>();
            agent.baseOffset = 1f; agent.radius = 0.4f; agent.height = 1.9f; agent.speed = 3.5f;
            agent.acceleration = 14f; agent.angularSpeed = 360f;
            hunterGo.AddComponent<TestNavMeshMover>();
            var eye = new GameObject("Eye").transform;
            eye.SetParent(hunterGo.transform, false); eye.localPosition = new Vector3(0, 0.6f, 0.2f);
            var brain = hunterGo.AddComponent<SoundHunterBrain>(); // adds Health, Listener, Vision
            SetField(hunterGo.GetComponent<SoundHunterListener>(), "hearingGraph", graph);
            SetField(hunterGo.GetComponent<SoundHunterListener>(), "memorySeconds", 25f); // longer than the 24 s crossing from room A
            var vision = hunterGo.GetComponent<HunterVision>();
            SetField(vision, "target", playerHealth);
            SetField(vision, "eye", eye);
            var brainSo = new SerializedObject(brain);
            var pts = brainSo.FindProperty("patrolPoints");
            pts.arraySize = 2;
            pts.GetArrayElementAtIndex(0).objectReferenceValue = pat2;
            pts.GetArrayElementAtIndex(1).objectReferenceValue = pat1;
            brainSo.ApplyModifiedPropertiesWithoutUndo();
            Label(hunterGo.transform, "HUNTER (placeholder)", new Vector3(0, 1.9f, 0));

            // ---------------- debug + controls ----------------
            var tools = new GameObject("TestTools");
            var hud = tools.AddComponent<HunterDebugHud>();
            hud.Bind(brain);
            var controls = tools.AddComponent<HunterTestControls>();
            controls.Bind(cc, tp, sndD, sndC, sndP, sndE, sndA);
            var runner = tools.AddComponent<SoundHunterScenarioRunner>(); // autoRun off by default
            runner.Bind(brain, playerHealth, hw, controls, pat1);

            // Labels for sound anchors / zones (placeholder visuals)
            Label(sndP, "unreachable platform sound", Vector3.up * 0.5f);
            Label(sndE, "disconnected vault sound", Vector3.up * 0.5f);
            Label(sndD, "reachable sound (N/M keys)", Vector3.up * 0.5f);

            System.IO.Directory.CreateDirectory("Assets/Perera/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Perera sound hunter test scene saved: " + ScenePath);
        }

        // ------------------------------------------------------------ geometry
        private static void BuildGeometry()
        {
            var floorMat = Mat("Floor", new Color(0.55f, 0.55f, 0.58f));
            var wallMat = Mat("Wall", new Color(0.35f, 0.38f, 0.45f));
            var coverMat = Mat("Cover", new Color(0.2f, 0.6f, 0.25f));
            var platMat = Mat("Platform", new Color(0.85f, 0.65f, 0.15f));
            var vaultMat = Mat("Vault", new Color(0.4f, 0.2f, 0.5f));

            Box("Floor_Main_PLACEHOLDER", new Vector3(7, -0.25f, 0), new Vector3(32, 0.5f, 46), floorMat);
            Box("Floor_Vault_PLACEHOLDER", new Vector3(-28, -0.25f, 0), new Vector3(14, 0.5f, 14), vaultMat);

            // Z-axis walls (constant x), X-axis walls (constant z). Gaps are doorways.
            WallX(-7, -7, 21, wallMat);                          // west outer
            WallX(21, -21, 21, wallMat);                         // east outer
            WallX(7, -7, 7, wallMat);                            // A|B solid (closed door, muffled link)
            WallX(7, 7, 21, wallMat, 14f);                       // C|D doorway at z=14
            WallX(7, -21, -7, wallMat);                          // F west
            WallZ(21, -7, 21, wallMat);                          // north outer
            WallZ(7, -7, 7, wallMat, 0f);                        // A|C doorway at x=0
            WallZ(7, 7, 21, wallMat, 14f);                       // B|D doorway at x=14
            WallZ(-7, -7, 7, wallMat);                           // A south
            WallZ(-7, 7, 21, wallMat, 14f);                      // B|F doorway at x=14
            WallZ(-21, 7, 21, wallMat);                          // F south

            // Vault: fully sealed box.
            WallX(-34, -6, 6, vaultMat, null, "Vault"); WallX(-22, -6, 6, vaultMat, null, "Vault");
            WallZ(-6, -34, -22, vaultMat, null, "Vault"); WallZ(6, -34, -22, vaultMat, null, "Vault");

            // Cover in room B (blocks the hunter's patrol-line view of the area behind it).
            var cover = Box("Cover_Wall_PLACEHOLDER", new Vector3(16, 1, 0), new Vector3(0.5f, 2, 3), coverMat);
            Label(geometry, "COVER", new Vector3(16, 2.6f, 0));

            // Platform: hearable but not walkable (4x4, top at y=3).
            var plat = Box("Platform_Unreachable_PLACEHOLDER", new Vector3(18, 1.5f, 18), new Vector3(4, 3, 4), platMat);
            Label(geometry, "UNREACHABLE PLATFORM", new Vector3(18, 4.2f, 18));

            // Closed-door placeholder (documented stand-in until Wijesinghe's door contract exists).
            var door = Box("Door_Closed_PLACEHOLDER", new Vector3(6.9f, 1, 0), new Vector3(0.15f, 2, 1.5f), Mat("Door", new Color(0.6f, 0.3f, 0.1f)));
            Object.DestroyImmediate(door.GetComponent<Collider>());
            Label(geometry, "CLOSED DOOR (muffled link 40)", new Vector3(6.5f, 2.6f, 0));

        }

        private static GameObject Box(string name, Vector3 pos, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(geometry, false);
            go.transform.position = pos; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        private static void WallX(float x, float z0, float z1, Material m, float? gap = null, string tag = "Wall")
        {
            const float h = 3f, t = 0.4f, door = 2.5f;
            Segment($"{tag}_X{x}", z0, z1, gap, door, (a, b) => Box($"{tag}_X{x}_{a:0}_{b:0}_PLACEHOLDER",
                new Vector3(x, h / 2, (a + b) / 2), new Vector3(t, h, b - a), m));
        }

        private static void WallZ(float z, float x0, float x1, Material m, float? gap = null, string tag = "Wall")
        {
            const float h = 3f, t = 0.4f, door = 2.5f;
            Segment($"{tag}_Z{z}", x0, x1, gap, door, (a, b) => Box($"{tag}_Z{z}_{a:0}_{b:0}_PLACEHOLDER",
                new Vector3((a + b) / 2, h / 2, z), new Vector3(b - a, h, t), m));
        }

        private static void Segment(string _, float from, float to, float? gap, float doorWidth, System.Action<float, float> make)
        {
            if (gap == null) { make(from, to); return; }
            float g0 = gap.Value - doorWidth / 2, g1 = gap.Value + doorWidth / 2;
            if (g0 > from) make(from, g0);
            if (g1 < to) make(g1, to);
        }

        // ------------------------------------------------------------ helpers
        private static HearingNode Node(GameObject parent, string name, Vector3 pos, Vector3 zoneSize, Vector3 zoneOffset)
        {
            var go = new GameObject("Node_" + name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = pos;
            var n = go.AddComponent<HearingNode>();
            SetField(n, "zoneName", name);
            SetField(n, "zoneSize", zoneSize);
            SetField(n, "zoneOffset", zoneOffset);
            return n;
        }

        private static void Link(GameObject parent, string name, HearingNode a, HearingNode b, float cost)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var l = go.AddComponent<HearingLink>();
            l.NodeA = a; l.NodeB = b; l.Cost = cost; l.Open = true;
        }

        private static Transform Anchor(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            return go.transform;
        }

        private static Material Mat(string name, Color color)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;
            string path = $"{MatDir}/Perera_SH_{name}.mat";
            var baseMat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var src = baseMat.GetComponent<Renderer>().sharedMaterial;
            Object.DestroyImmediate(baseMat);
            var m = new Material(src) { name = "Perera_SH_" + name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            AssetDatabase.CreateAsset(m, path);
            mats[name] = m;
            return m;
        }

        private static void Label(Transform parent, string text, Vector3 localPos)
        {
            if (string.IsNullOrEmpty(text)) return;
            var go = new GameObject("Label_" + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text; tm.characterSize = 0.12f; tm.fontSize = 48; tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.white;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            go.AddComponent<BillboardLabel>();
            // Labels must not be baked into the NavMesh or block sight.
        }

        private static void SetField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            switch (value)
            {
                case float f: p.floatValue = f; break;
                case string s: p.stringValue = s; break;
                case Vector3 v: p.vector3Value = v; break;
                case Object o: p.objectReferenceValue = o; break;
                default: Debug.LogError("Unsupported SetField type for " + field); break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
