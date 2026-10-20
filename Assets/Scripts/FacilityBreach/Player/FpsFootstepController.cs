using UnityEngine;
using FacilityBreach.Audio;

namespace FacilityBreach.Player
{
    /// <summary>
    /// GV Part 2: Motion-Based Player Footstep Audio Controller.
    /// Emits natural, subtle combat boot footsteps on industrial concrete during grounded W/S/A/D movement.
    /// 
    /// SAFETY & INTEGRITY GATING:
    /// - Requires character to be grounded (body.isGrounded).
    /// - Requires active movement input (W/S/A/D).
    /// - Requires actual physical horizontal displacement (prevents phantom footsteps when walking into walls).
    /// - Resets step accumulator immediately when stopping or losing grounded status.
    /// - Automatically halts during gameplay pause (Time.timeScale == 0 or Run.Paused).
    /// - Diagonal movement accumulates distance organically without doubling cadence.
    /// 
    /// STRICT AI ISOLATION INVARIANT:
    /// Pure player-facing immersion audio. Never registers with or emits stimuli to
    /// FacilityNoiseHub or SoundEventBus. AI patrol guards have ZERO awareness of player footsteps.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FpsFootstepController : MonoBehaviour
    {
        [Header("Footstep Cadence Configuration")]
        [Tooltip("Horizontal travel distance (meters) required to trigger a footstep (~1.8m average human stride).")]
        [SerializeField] private float strideDistance = 1.8f;

        [Tooltip("Minimum horizontal velocity (m/s) required to consider player actively moving.")]
        [SerializeField] private float minMoveSpeed = 0.45f;

        [Tooltip("Minimum cooldown interval (seconds) between consecutive footsteps to prevent double-stepping.")]
        [SerializeField] private float minStepInterval = 0.22f;

        [Tooltip("Master volume scale for footstep audio (subtle background presence, below gunshots and kicks).")]
        [Range(0f, 1f)]
        [SerializeField] private float stepVolume = 0.26f;

        private CharacterController characterController;
        private FacilityBreachRuntime runtime;
        private TemporaryPlayerAdapter playerAdapter;
        private AudioSource footstepAudioSource;

        private Vector3 lastPosition;
        private float stepDistanceAccumulator = 0f;
        private float stepCooldownTimer = 0f;
        private int stepVariantCounter = 0;
        private int totalStepsEmitted = 0;

        public float StrideDistance => strideDistance;
        public float MinMoveSpeed => minMoveSpeed;
        public float StepVolume => stepVolume;
        public float StepDistanceAccumulator => stepDistanceAccumulator;
        public int TotalStepsEmitted => totalStepsEmitted;
        public AudioSource FootstepAudioSource => footstepAudioSource;
        public bool IsGrounded => characterController != null && characterController.isGrounded;

        public void Initialize(FacilityBreachRuntime runtimeRef, CharacterController controllerRef, TemporaryPlayerAdapter adapterRef)
        {
            runtime = runtimeRef;
            characterController = controllerRef != null ? controllerRef : GetComponent<CharacterController>();
            playerAdapter = adapterRef != null ? adapterRef : GetComponent<TemporaryPlayerAdapter>();
            InitializeAudioSource();
            lastPosition = transform.position;
        }

        public void ResetStepState()
        {
            stepDistanceAccumulator = 0f;
            stepCooldownTimer = 0f;
            totalStepsEmitted = 0;
            lastPosition = transform.position;
        }

        private void Awake()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }
            if (playerAdapter == null)
            {
                playerAdapter = GetComponent<TemporaryPlayerAdapter>();
            }
            InitializeAudioSource();
            lastPosition = transform.position;
        }

        private void Start()
        {
            lastPosition = transform.position;
        }

        public void InitializeAudioSource()
        {
            if (footstepAudioSource != null) return;

            Transform child = transform.Find("[FpsFootstepAudioSource]");
            GameObject childGo;
            if (child == null)
            {
                childGo = new GameObject("[FpsFootstepAudioSource]");
                childGo.transform.SetParent(transform, false);
            }
            else
            {
                childGo = child.gameObject;
            }

            footstepAudioSource = childGo.GetComponent<AudioSource>();
            if (footstepAudioSource == null)
            {
                footstepAudioSource = childGo.AddComponent<AudioSource>();
            }

            footstepAudioSource.playOnAwake = false;
            footstepAudioSource.loop = false;
            footstepAudioSource.spatialBlend = 0f; // 2D centered player perspective
            footstepAudioSource.volume = stepVolume;
            footstepAudioSource.priority = 110;    // Standard SFX priority below gunshots (64/95)
            footstepAudioSource.dopplerLevel = 0f;
            footstepAudioSource.bypassEffects = true;
            footstepAudioSource.bypassListenerEffects = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 1. Gameplay gating: strictly require active gameplay session
            if (Time.timeScale <= 0f)
            {
                stepDistanceAccumulator = 0f;
                lastPosition = transform.position;
                return;
            }

            if (runtime != null && (!runtime.Run.Running || runtime.Run.Paused || !runtime.Resources.Alive))
            {
                stepDistanceAccumulator = 0f;
                lastPosition = transform.position;
                return;
            }

            if (playerAdapter != null && !playerAdapter.InputEnabled)
            {
                stepDistanceAccumulator = 0f;
                lastPosition = transform.position;
                return;
            }

            // 2. Input check (W/S/A/D)
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            bool hasMovementInput = Mathf.Abs(h) > 0.05f || Mathf.Abs(v) > 0.05f;

            // 3. Grounded check
            bool grounded = characterController != null && characterController.isGrounded;

            // 4. Actual physical horizontal displacement check (prevents phantom steps against walls)
            Vector3 currentPos = transform.position;
            Vector3 displacement = currentPos - lastPosition;
            displacement.y = 0f; // horizontal only
            float moveDistance = displacement.magnitude;
            float horizontalSpeed = moveDistance / dt;
            lastPosition = currentPos;

            // 5. Update cooldown timer
            if (stepCooldownTimer > 0f)
            {
                stepCooldownTimer -= dt;
            }

            // Gating: must be grounded, have intentional input, and have actual physical movement
            if (!grounded || !hasMovementInput || horizontalSpeed < minMoveSpeed)
            {
                stepDistanceAccumulator = 0f;
                return;
            }

            // 6. Accumulate physical travel distance
            stepDistanceAccumulator += moveDistance;

            if (stepDistanceAccumulator >= strideDistance && stepCooldownTimer <= 0f)
            {
                EmitFootstep(horizontalSpeed);
                stepDistanceAccumulator = 0f;
                stepCooldownTimer = minStepInterval;
            }
        }

        /// <summary>
        /// Emits a single natural footstep audio event with speed scaling and subtle variant alternating.
        /// </summary>
        private void EmitFootstep(float speed)
        {
            int variant = stepVariantCounter % 3;
            stepVariantCounter++;
            totalStepsEmitted++;

            AudioClip clip = FacilityAudioSynthesizer.GetOrCreateFootstepClip(variant);
            if (clip == null) return;

            if (footstepAudioSource == null)
            {
                InitializeAudioSource();
                if (footstepAudioSource == null) return;
            }

            // Dynamic volume scaling based on walking vs sprinting
            float speedScale = Mathf.Clamp(speed / 4.0f, 0.85f, 1.25f);
            float volume = Mathf.Clamp01(stepVolume * speedScale);

            // Subtle pitch micro-variation
            float pitch = Random.Range(0.97f, 1.03f);
            footstepAudioSource.pitch = pitch;
            footstepAudioSource.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// Explicit test simulation method for headless EditMode unit verification.
        /// Allows deterministic verification of all motion gating without needing a physics loop.
        /// </summary>
        public void SimulateStepUpdate(float dt, Vector3 horizontalDisplacement, bool grounded, bool hasInput, bool paused = false)
        {
            if (paused)
            {
                stepDistanceAccumulator = 0f;
                return;
            }

            if (stepCooldownTimer > 0f)
            {
                stepCooldownTimer -= dt;
            }

            float dist = horizontalDisplacement.magnitude;
            float speed = dist / Mathf.Max(0.0001f, dt);

            if (!grounded || !hasInput || speed < minMoveSpeed)
            {
                stepDistanceAccumulator = 0f;
                return;
            }

            stepDistanceAccumulator += dist;

            if (stepDistanceAccumulator >= strideDistance && stepCooldownTimer <= 0f)
            {
                EmitFootstep(speed);
                stepDistanceAccumulator = 0f;
                stepCooldownTimer = minStepInterval;
            }
        }

        private void OnDisable()
        {
            stepDistanceAccumulator = 0f;
            stepCooldownTimer = 0f;
        }

        private void OnDestroy()
        {
            stepDistanceAccumulator = 0f;
        }
    }
}
