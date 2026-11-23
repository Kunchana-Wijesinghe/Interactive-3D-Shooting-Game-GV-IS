using FacilityBreach.AI.Roles;
using FacilityBreach.AI.SoundHunter;
using Perera.SoundHunter;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityBreach.Combat
{
    /// <summary>
    /// Procedural leg locomotion system for robot enemies (Flanker, Sound Hunter, Patrol Guard, Cover Defender).
    /// Drives leg bones (Lleg, Rleg) with velocity-synchronized alternating pitch cycles to eliminate foot sliding,
    /// smooth idle recovery, in-place turn shuffling, and clean defeat reset.
    /// Operates exclusively on leg bone transforms (Lleg, Rleg) so the torso, head, and arms remain 100% stable and visible.
    /// Configures all SkinnedMeshRenderers with updateWhenOffscreen=true to prevent unintended frustum-culling during movement.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RobotLegLocomotion : MonoBehaviour
    {
        [Header("Locomotion Tuning")]
        [Tooltip("Effective stride length in meters. Stride frequency scales with velocity to prevent foot sliding.")]
        [SerializeField] private float strideLength = 1.25f;

        [Tooltip("Maximum leg swing angle (degrees) during normal walking.")]
        [SerializeField] private float maxWalkSwingAngle = 24f;

        [Tooltip("Maximum leg swing angle (degrees) during sprinting/chasing.")]
        [SerializeField] private float maxRunSwingAngle = 32f;

        [Tooltip("Reference run speed in m/s at which leg swing reaches maxRunSwingAngle.")]
        [SerializeField] private float runSpeedThreshold = 3.2f;

        [Tooltip("How quickly legs blend back to neutral standing pose when stopping.")]
        [SerializeField] private float stopDampingSpeed = 8f;

        [Tooltip("Leg swing angle (degrees) during in-place turning.")]
        [SerializeField] private float turnShuffleSwingAngle = 10f;

        // Bone references: ONLY legitimate leg pivots are driven
        private Transform leftLegBone;
        private Transform rightLegBone;
        private Transform modelVisual;

        // Resting poses
        private Quaternion leftLegRestRot = Quaternion.Euler(180f, 0f, 0f);
        private Quaternion rightLegRestRot = Quaternion.Euler(180f, 0f, 0f);

        // Subsystems
        private EnemyCombatAdapter combat;
        private SoundHunterNavMeshMover mover;
        private NavMeshAgent agent;
        private FacilityBreachRuntime runtime;

        // Motion tracking state
        private Vector3 lastWorldPosition;
        private float lastYaw;
        private float stridePhase;
        private float turnPhase;
        private float movementWeight;
        private float turnWeight;
        private bool initialized;

        // Public telemetry & inspection
        public bool IsBound => leftLegBone != null && rightLegBone != null;
        public float CurrentSpeed { get; private set; }
        public float AngularVelocity { get; private set; }
        public float StridePhase => stridePhase;
        public float LeftLegPitch { get; private set; }
        public float RightLegPitch { get; private set; }
        public float MovementWeight => movementWeight;
        public bool IsMoving => movementWeight > 0.05f;
        public bool ForceDefeated { get; set; }
        public bool IsDead => ForceDefeated || (combat != null && combat.enabled && combat.IsDead);

        private void Awake()
        {
            combat = GetComponent<EnemyCombatAdapter>();
            mover = GetComponent<SoundHunterNavMeshMover>();
            agent = GetComponent<NavMeshAgent>();
            runtime = FindAnyObjectByType<FacilityBreachRuntime>();
        }

        private void Start()
        {
            InitializeBones();
            lastWorldPosition = transform.position;
            lastYaw = transform.eulerAngles.y;
            ApplyRobotSpecificProfile();
        }

        /// <summary>
        /// Discovers and caches bone transforms from the ModelVisual hierarchy.
        /// Also configures all SkinnedMeshRenderers with updateWhenOffscreen=true to prevent camera culling.
        /// Can be safely invoked externally or multiple times.
        /// </summary>
        public bool InitializeBones()
        {
            if (initialized && leftLegBone && rightLegBone) return true;

            modelVisual = transform.Find("ModelVisual");
            Transform searchRoot = modelVisual != null ? modelVisual : transform;

            // Find non-renderer transforms named Lleg and Rleg (case-insensitive)
            foreach (var t in searchRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<Renderer>() != null) continue; // Skip mesh renderers

                string nameLower = t.name.ToLowerInvariant();
                if (nameLower == "lleg")
                {
                    leftLegBone = t;
                    leftLegRestRot = t.localRotation;
                }
                else if (nameLower == "rleg")
                {
                    rightLegBone = t;
                    rightLegRestRot = t.localRotation;
                }
            }

            // Ensure all SkinnedMeshRenderers remain visible during animation and locomotion without frustum culling
            foreach (var smr in searchRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
            }

            initialized = leftLegBone != null && rightLegBone != null;
            return initialized;
        }

        /// <summary>
        /// Tunes stride parameters according to the robot's specific tactical role and visual weight.
        /// </summary>
        public void ApplyRobotSpecificProfile()
        {
            string identity = combat != null ? combat.Identity : "";
            switch (identity)
            {
                case "FL-04": // Flanker: swift, agile flank runner
                    strideLength = 1.15f;
                    maxWalkSwingAngle = 26f;
                    maxRunSwingAngle = 34f;
                    runSpeedThreshold = 3.0f;
                    break;

                case "SH-01": // Sound Hunter: predatory stalker, long calculated strides
                    strideLength = 1.25f;
                    maxWalkSwingAngle = 24f;
                    maxRunSwingAngle = 32f;
                    runSpeedThreshold = 3.2f;
                    break;

                case "PG-01": // Patrol Guard: disciplined steady military march
                    strideLength = 1.30f;
                    maxWalkSwingAngle = 24f;
                    maxRunSwingAngle = 30f;
                    runSpeedThreshold = 3.2f;
                    break;

                case "CD-03": // Cover Defender: heavy armor, sturdy grounded steps
                    strideLength = 1.20f;
                    maxWalkSwingAngle = 22f;
                    maxRunSwingAngle = 28f;
                    runSpeedThreshold = 2.8f;
                    break;
            }
        }

        private void LateUpdate()
        {
            if (!initialized && !InitializeBones()) return;

            // Handle death state: instantly restore neutral leg pose so defeat presentation tilts cleanly
            if (IsDead)
            {
                ResetToRestPose();
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Mover/NavMesh speed fallback
            float moverSpeed = 0f;
            if (mover != null && mover.Status == MoveStatus.Moving)
            {
                Vector3 vel = mover.Velocity;
                vel.y = 0f;
                moverSpeed = vel.magnitude;
            }
            else if (agent != null && agent.isOnNavMesh && !agent.isStopped)
            {
                Vector3 vel = agent.velocity;
                vel.y = 0f;
                moverSpeed = vel.magnitude;
            }

            SimulateStep(dt, transform.position, transform.eulerAngles.y, moverSpeed);
        }

        /// <summary>
        /// Explicit simulation step for deterministic locomotion calculation and testing.
        /// Animates ONLY Lleg and Rleg bones. Upper body, torso, and head transforms are never modified.
        /// </summary>
        public void SimulateStep(float dt, Vector3 worldPos, float currentYaw, float moverSpeed = 0f)
        {
            if (!initialized && !InitializeBones()) return;

            if (IsDead)
            {
                ResetToRestPose();
                return;
            }

            if (dt <= 0f) return;

            // Calculate current ground velocity and speed
            Vector3 frameDisplacement = worldPos - lastWorldPosition;
            frameDisplacement.y = 0f;
            float measuredSpeed = frameDisplacement.magnitude / dt;
            lastWorldPosition = worldPos;

            // Ground speed is the maximum of measured displacement and mover velocity
            CurrentSpeed = Mathf.Max(measuredSpeed, moverSpeed);

            // Track angular velocity (degrees/sec)
            float deltaYaw = Mathf.DeltaAngle(lastYaw, currentYaw);
            AngularVelocity = Mathf.Abs(deltaYaw) / dt;
            lastYaw = currentYaw;

            // Determine locomotion mode
            bool isTranslating = CurrentSpeed > 0.08f;
            bool isTurningInPlace = !isTranslating && AngularVelocity > 15f;

            // Update movement weight
            float targetMoveWeight = isTranslating ? 1f : 0f;
            movementWeight = Mathf.MoveTowards(movementWeight, targetMoveWeight, dt * (isTranslating ? 12f : stopDampingSpeed));

            // Update turn weight
            float targetTurnWeight = isTurningInPlace ? Mathf.Clamp01((AngularVelocity - 15f) / 45f) : 0f;
            turnWeight = Mathf.MoveTowards(turnWeight, targetTurnWeight, dt * 10f);

            // Advance linear stride phase proportionally to speed: dPhase = 2 * PI * (speed * dt / strideLength)
            if (movementWeight > 0.001f)
            {
                float phaseDelta = (2f * Mathf.PI * CurrentSpeed * dt) / Mathf.Max(0.1f, strideLength);
                stridePhase = (stridePhase + phaseDelta) % (2f * Mathf.PI);
            }

            // Advance turning phase
            if (turnWeight > 0.001f)
            {
                float turnPhaseDelta = (AngularVelocity * Mathf.Deg2Rad * dt * 4f);
                turnPhase = (turnPhase + turnPhaseDelta) % (2f * Mathf.PI);
            }

            // Compute leg pitch angles:
            // Model orientation audit established:
            // Negative pitch around local X swings leg FORWARD (+Z)
            // Positive pitch around local X swings leg BACKWARD (-Z)
            float speedRatio = Mathf.Clamp01(CurrentSpeed / Mathf.Max(0.5f, runSpeedThreshold));
            float currentMaxSwing = Mathf.Lerp(maxWalkSwingAngle, maxRunSwingAngle, speedRatio);

            // Left leg leads forward when sin(phase) > 0: -sin(phase) * swing
            // Right leg opposes: +sin(phase) * swing
            float linearSwing = Mathf.Sin(stridePhase) * currentMaxSwing * movementWeight;
            float turnShuffleSwing = Mathf.Sin(turnPhase) * turnShuffleSwingAngle * turnWeight;

            // Combine linear swing with turn shuffle
            float totalSwing = linearSwing + turnShuffleSwing;
            LeftLegPitch = -totalSwing;
            RightLegPitch = +totalSwing;

            // Apply bone rotations relative to rest pose strictly to leg bones
            if (leftLegBone != null) leftLegBone.localRotation = leftLegRestRot * Quaternion.Euler(LeftLegPitch, 0f, 0f);
            if (rightLegBone != null) rightLegBone.localRotation = rightLegRestRot * Quaternion.Euler(RightLegPitch, 0f, 0f);
        }

        /// <summary>
        /// Resets all driven leg bones to their neutral resting poses.
        /// </summary>
        public void ResetToRestPose()
        {
            if (leftLegBone != null) leftLegBone.localRotation = leftLegRestRot;
            if (rightLegBone != null) rightLegBone.localRotation = rightLegRestRot;

            LeftLegPitch = 0f;
            RightLegPitch = 0f;
            CurrentSpeed = 0f;
            AngularVelocity = 0f;
            movementWeight = 0f;
            turnWeight = 0f;
        }

        private void OnDisable()
        {
            ResetToRestPose();
        }
    }
}
