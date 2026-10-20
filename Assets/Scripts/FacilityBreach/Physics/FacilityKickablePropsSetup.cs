using System.Collections.Generic;
using UnityEngine;

namespace FacilityBreach
{
    /// <summary>
    /// GV Part 2: Facility Kickable Props Spawner & Configuration.
    /// Deterministically places and configures kickable physics props:
    /// small wooden boxes, medium crates, and rolling metal barrels.
    /// Barrels feature tuned capsule collision geometry and low angular damping to permit natural rolling.
    /// All objects are placed in safe alcoves clear of door envelopes and guard patrol routes.
    /// </summary>
    public static class FacilityKickablePropsSetup
    {
        public const string PropsRootName = "[FacilityKickableProps]";

        public struct PropDefinition
        {
            public string Name;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public PrimitiveType Primitive;
            public PropMaterialType MaterialType;
            public float Mass;
            public float LinearDamping;
            public float AngularDamping;
            public Color Tint;

            public PropDefinition(
                string name,
                Vector3 position,
                Quaternion rotation,
                Vector3 scale,
                PrimitiveType primitive,
                PropMaterialType materialType,
                float mass,
                float linearDamping,
                float angularDamping,
                Color tint)
            {
                Name = name;
                Position = position;
                Rotation = rotation;
                Scale = scale;
                Primitive = primitive;
                MaterialType = materialType;
                Mass = mass;
                LinearDamping = linearDamping;
                AngularDamping = angularDamping;
                Tint = tint;
            }

            public PropDefinition(
                string name,
                Vector3 position,
                Vector3 scale,
                PrimitiveType primitive,
                PropMaterialType materialType,
                float mass,
                float linearDamping,
                float angularDamping,
                Color tint) : this(name, position, Quaternion.identity, scale, primitive, materialType, mass, linearDamping, angularDamping, tint)
            {
            }
        }

        public static readonly PropDefinition[] AuthoredPropDefinitions = new PropDefinition[]
        {
            // 1. Entrance Lobby Alcove (Small Wooden Box)
            new PropDefinition(
                "Box_Small_EntranceLobby_01",
                new Vector3(-10.5f, 0.25f, -27.5f),
                Quaternion.identity,
                Vector3.one * 0.45f,
                PrimitiveType.Cube,
                PropMaterialType.Wood,
                3.5f,
                1.2f,
                1.8f,
                new Color(0.78f, 0.58f, 0.35f) // Warm wood
            ),

            // 2. Entrance-Junction Corridor Nook (Medium Wooden Crate)
            new PropDefinition(
                "Crate_Medium_EntranceCorridor_01",
                new Vector3(-5.5f, 0.35f, -18.0f),
                Quaternion.identity,
                Vector3.one * 0.65f,
                PrimitiveType.Cube,
                PropMaterialType.Wood,
                6.0f,
                1.1f,
                1.6f,
                new Color(0.68f, 0.48f, 0.28f) // Darker crate wood
            ),

            // 3. West Corridor Service Alcove (Metal Barrel - Upright, Calibrated Rolling Physics)
            new PropDefinition(
                "Barrel_Metal_WestCorridor_01",
                new Vector3(-11.0f, 0.47f, -5.0f),
                Quaternion.identity,
                new Vector3(0.60f, 0.45f, 0.60f),
                PrimitiveType.Cylinder,
                PropMaterialType.Metal,
                7.5f,
                0.50f,
                0.25f, // Low angular damping allows smooth rolling
                new Color(0.32f, 0.38f, 0.44f) // Industrial slate steel
            ),

            // 4. Central Junction Perimeter Safety Alcove (Small Wooden Box)
            new PropDefinition(
                "Box_Small_CentralJunction_01",
                new Vector3(3.2f, 0.25f, 10.5f),
                Quaternion.identity,
                Vector3.one * 0.45f,
                PrimitiveType.Cube,
                PropMaterialType.Wood,
                3.5f,
                1.2f,
                1.8f,
                new Color(0.78f, 0.58f, 0.35f)
            ),

            // 5. Toxic Waste Area Outer Curb (Metal Barrel - Resting Horizontally on Side, Immediate Roll)
            new PropDefinition(
                "Barrel_Metal_ToxicPerimeter_01",
                new Vector3(1.8f, 0.32f, 18.2f),
                Quaternion.Euler(0f, 35f, 90f),
                new Vector3(0.60f, 0.45f, 0.60f),
                PrimitiveType.Cylinder,
                PropMaterialType.Metal,
                8.0f,
                0.45f,
                0.20f, // Low rolling resistance along curved surface
                new Color(0.40f, 0.44f, 0.36f) // Olive drab containment barrel
            ),

            // 6. Room 2 Storage Alcove (Small Wooden Box)
            new PropDefinition(
                "Box_Small_Room2Storage_01",
                new Vector3(-2.5f, 0.25f, 6.5f),
                Quaternion.identity,
                Vector3.one * 0.45f,
                PrimitiveType.Cube,
                PropMaterialType.Wood,
                3.5f,
                1.2f,
                1.8f,
                new Color(0.78f, 0.58f, 0.35f)
            ),

            // 7. Room 3 Machinery Workshop Corner (Medium Wooden Crate)
            new PropDefinition(
                "Crate_Medium_Room3Workshop_01",
                new Vector3(16.5f, 0.35f, 21.0f),
                Quaternion.identity,
                Vector3.one * 0.65f,
                PrimitiveType.Cube,
                PropMaterialType.Wood,
                6.5f,
                1.1f,
                1.6f,
                new Color(0.68f, 0.48f, 0.28f)
            )
        };

        /// <summary>
        /// Ensures all authored props are instantiated once under a dedicated root.
        /// Prevents duplicate spawning across reloads or repeated calls.
        /// </summary>
        public static List<KickablePhysicsProp> EnsurePropsSpawned(Transform parentTransform = null)
        {
            var spawnedProps = new List<KickablePhysicsProp>();

            GameObject root = GameObject.Find(PropsRootName);
            if (root != null)
            {
                var existing = root.GetComponentsInChildren<KickablePhysicsProp>();
                if (existing != null && existing.Length == AuthoredPropDefinitions.Length)
                {
                    spawnedProps.AddRange(existing);
                    return spawnedProps;
                }
            }
            else
            {
                root = new GameObject(PropsRootName);
                if (parentTransform != null)
                {
                    root.transform.SetParent(parentTransform, true);
                }
            }

            // Create shared materials
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var woodMat = new Material(shader) { name = "M_Kickable_Wood_Procedural" };
            woodMat.color = new Color(0.72f, 0.52f, 0.32f);

            var metalMat = new Material(shader) { name = "M_Kickable_Metal_Procedural" };
            metalMat.color = new Color(0.36f, 0.40f, 0.45f);

            for (int i = 0; i < AuthoredPropDefinitions.Length; i++)
            {
                var def = AuthoredPropDefinitions[i];

                // Check if prop already exists under root
                Transform existingChild = root.transform.Find(def.Name);
                if (existingChild != null)
                {
                    var propComp = existingChild.GetComponent<KickablePhysicsProp>();
                    if (propComp != null) spawnedProps.Add(propComp);
                    continue;
                }

                GameObject propGo = GameObject.CreatePrimitive(def.Primitive);
                propGo.name = def.Name;
                propGo.transform.SetParent(root.transform, false);
                propGo.transform.position = def.Position;
                propGo.transform.rotation = def.Rotation;
                propGo.transform.localScale = def.Scale;

                // Renderer & Material
                var renderer = propGo.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material mat = def.MaterialType == PropMaterialType.Wood ? woodMat : metalMat;
                    renderer.material = mat;
                }

                // If cylinder, replace default mesh collider with capsule collider for smoother rolling
                if (def.Primitive == PrimitiveType.Cylinder)
                {
                    var meshCol = propGo.GetComponent<Collider>();
                    if (meshCol != null && !(meshCol is CapsuleCollider))
                    {
                        Object.DestroyImmediate(meshCol);
                        var capsule = propGo.AddComponent<CapsuleCollider>();
                        capsule.radius = 0.5f;
                        capsule.height = 2.0f;
                        capsule.direction = 1; // Y axis in local space
                    }
                }

                // Rigidbody physics
                var rb = propGo.GetComponent<Rigidbody>();
                if (rb == null) rb = propGo.AddComponent<Rigidbody>();
                rb.mass = def.Mass;
                rb.linearDamping = def.LinearDamping;
                rb.angularDamping = def.AngularDamping;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.maxAngularVelocity = 30.0f;

                // Kickable component
                var kickable = propGo.AddComponent<KickablePhysicsProp>();
                kickable.MaterialType = def.MaterialType;
                kickable.BaseMass = def.Mass;
                kickable.LinearDamping = def.LinearDamping;
                kickable.AngularDamping = def.AngularDamping;
                kickable.ConfigurePhysics();

                spawnedProps.Add(kickable);
            }

            return spawnedProps;
        }
    }
}
