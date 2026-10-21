using System.Collections.Generic;
using UnityEngine;
using FacilityBreach.Audio;

namespace FacilityBreach.Player
{
    /// <summary>
    /// GV Part 2: Motion-Based Player Footstep Audio Controller.
    /// Emits natural, audible combat boot footsteps on industrial concrete during grounded W/S/A/D movement.
    /// 
    /// SAFETY & INTEGRITY GATING:
    /// - Requires character to be grounded (controller isGrounded or downward raycast).
    /// - Requires active movement input (W/S/A/D).
    /// - Requires actual physical horizontal displacement (prevents phantom footsteps when walking into walls).
    /// - Automatically halts and resets during gameplay pause (Time.timeScale == 0, Run.Paused, or menu).
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
        [Tooltip("Horizontal travel distance (meters) required to trigger a footstep (~1.6m natural walking step).")]
        [SerializeField] private float strideDistance = 1.6f;

        [Tooltip("Minimum horizontal velocity (m/s) required to consider player actively moving.")]
        [SerializeField] private float minMoveSpeed = 0.45f;

        [Tooltip("Minimum cooldown interval (seconds) between consecutive footsteps to prevent double-stepping.")]
        [SerializeField] private float minStepInterval = 0.22f;

        [Tooltip("Master volume scale for footstep audio (comfortable presence on laptop speakers).")]
        [Range(0f, 1f)]
        [SerializeField] private float stepVolume = 0.16f;

        private CharacterController characterController;
        private FacilityBreachRuntime runtime;
        private TemporaryPlayerAdapter playerAdapter;
        private AudioSource footstepAudioSource;

        private Vector3 lastPosition;
        private float stepDistanceAccumulator = 0f;
        private float stepCooldownTimer = 0f;
        private float airborneTimer = 0f;
        private int stepVariantCounter = 0;
        private int totalStepsEmitted = 0;

        public float StrideDistance => strideDistance;
        public float MinMoveSpeed => minMoveSpeed;
        public float StepVolume => stepVolume;
        public float StepDistanceAccumulator => stepDistanceAccumulator;
        public int TotalStepsEmitted => totalStepsEmitted;
        public AudioSource FootstepAudioSource => footstepAudioSource;
        public bool IsGrounded => CheckGrounded();

        public void Initialize(FacilityBreachRuntime runtimeRef, CharacterController controllerRef, TemporaryPlayerAdapter adapterRef)
        {
            runtime = runtimeRef;
            characterController = controllerRef != null ? controllerRef : GetComponent<CharacterController>();
            playerAdapter = adapterRef != null ? adapterRef : GetComponent<TemporaryPlayerAdapter>();
            InitializeAudioSource();
            lastPosition = transform.position;
            stepDistanceAccumulator = 0f;
            airborneTimer = 0f;

            Debug.Log($"[FpsFootstepController] Initialized on '{gameObject.name}'. Grounded: {CheckGrounded()}, Stride: {strideDistance:F2}m, Vol: {stepVolume:F2}");
        }

        public void ResetStepState()
        {
            stepDistanceAccumulator = 0f;
            stepCooldownTimer = 0f;
            airborneTimer = 0f;
            totalStepsEmitted = 0;
            lastPosition = transform.position;
        }

        public void OnGameplayPaused()
        {
            stepDistanceAccumulator = 0f;
            if (footstepAudioSource != null && footstepAudioSource.isPlaying)
            {
                footstepAudioSource.Stop();
            }
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

        /// <summary>
        /// Robust ground detection: checks character controller isGrounded and downward ground raycast
        /// to prevent false airborne resets caused by script execution order or floor collider seams.
        /// </summary>
        public bool CheckGrounded()
        {
            if (characterController != null && characterController.isGrounded)
                return true;

            // Downward probe to handle frame execution order where CharacterController.Move has not yet run this frame
            Vector3 rayOrigin = transform.position + Vector3.up * 0.20f;
            return Physics.Raycast(rayOrigin, Vector3.down, 0.45f, ~0, QueryTriggerInteraction.Ignore);
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
            footstepAudioSource.volume = 1.0f;     // Master source at 1.0; PlayOneShot controls playback volume to avoid squaring
            footstepAudioSource.priority = 110;    // Standard SFX priority below gunshots (64/95)
            footstepAudioSource.dopplerLevel = 0f;
            footstepAudioSource.bypassEffects = true;
            footstepAudioSource.bypassListenerEffects = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 1. Gameplay gating: strictly require active unpaused gameplay session
            bool isGameplayPaused = Time.timeScale <= 0f
                || (runtime != null && (!runtime.Run.Running || runtime.Run.Paused || !runtime.Resources.Alive))
                || (playerAdapter != null && !playerAdapter.InputEnabled)
                || (FacilityBreachRuntime.Instance != null && (FacilityBreachRuntime.Instance.Run.Paused || !FacilityBreachRuntime.Instance.Run.Running));

            if (isGameplayPaused)
            {
                OnGameplayPaused();
                lastPosition = transform.position;
                return;
            }

            // 2. Input check (W/S/A/D)
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            bool hasMovementInput = Mathf.Abs(h) > 0.05f || Mathf.Abs(v) > 0.05f;

            // 3. Grounded check (robust)
            bool grounded = CheckGrounded();

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

            // Gating: airborne detection
            if (!grounded)
            {
                airborneTimer += dt;
                if (airborneTimer > 0.10f)
                {
                    stepDistanceAccumulator = 0f;
                }
                return;
            }
            airborneTimer = 0f;

            // Gating: wall blockage or stationary
            if (!hasMovementInput || horizontalSpeed < minMoveSpeed)
            {
                // Wall blockage or stationary: do not accumulate footsteps
                return;
            }

            // 6. Actively moving on ground: prime first step if starting from complete standstill
            if (stepDistanceAccumulator <= 0.001f)
            {
                stepDistanceAccumulator = strideDistance * 0.35f;
            }

            // Accumulate physical travel distance
            stepDistanceAccumulator += moveDistance;

            if (stepDistanceAccumulator >= strideDistance && stepCooldownTimer <= 0f)
            {
                EmitFootstep(horizontalSpeed);
                stepDistanceAccumulator -= strideDistance;
                stepCooldownTimer = minStepInterval;
            }
        }

        private static AudioClip[] cachedRecordedClips;

        public static AudioClip[] CachedRecordedClips => cachedRecordedClips;
        public static bool IsUsingRecordedClips => cachedRecordedClips != null && cachedRecordedClips.Length > 0;

        public static void ClearClipsCache()
        {
            cachedRecordedClips = null;
        }

        public static AudioClip[] LoadRecordedFootstepClips()
        {
            if (cachedRecordedClips != null && cachedRecordedClips.Length > 0)
                return cachedRecordedClips;

            var list = new System.Collections.Generic.List<AudioClip>();

            // 1. Try loading dedicated individual footstep slices from Resources
            for (int i = 1; i <= 8; i++)
            {
                var slice = UnityEngine.Resources.Load<AudioClip>($"Audio/Footsteps/footstep_0{i}");
                if (slice != null)
                {
                    list.Add(slice);
                }
            }

            // 2. If no individual slices found in Resources, try loading master recording
            if (list.Count == 0)
            {
                var masterClip = UnityEngine.Resources.Load<AudioClip>("Audio/Footsteps/foot_Steps_sound");
#if UNITY_EDITOR
                if (masterClip == null)
                {
                    masterClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Footsteps/foot_Steps_sound.mp3");
                }
                if (masterClip == null)
                {
                    masterClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/Footsteps/foot_Steps_sound.mp3");
                }
#endif
                if (masterClip != null)
                {
                    var extracted = ExtractSlicesFromClip(masterClip, 4);
                    if (extracted != null && extracted.Length > 0)
                    {
                        list.AddRange(extracted);
                    }
                }
            }

            if (list.Count > 0)
            {
                cachedRecordedClips = list.ToArray();
                Debug.Log($"[FpsFootstepController] Loaded {cachedRecordedClips.Length} recorded footstep clips (DEFAULT walking audio).");
                return cachedRecordedClips;
            }

            return null;
        }

        public static AudioClip[] ExtractSlicesFromClip(AudioClip masterClip, int maxSlices = 4)
        {
            if (masterClip == null) return null;

            int channels = masterClip.channels;
            int sampleRate = masterClip.frequency;
            int totalFrames = masterClip.samples;
            if (totalFrames <= 0) return null;

            float[] raw = new float[totalFrames * channels];
            if (!masterClip.GetData(raw, 0)) return null;

            float[] mono = new float[totalFrames];
            for (int i = 0; i < totalFrames; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += raw[i * channels + c];
                mono[i] = sum / channels;
            }

            int windowSize = Mathf.Max(1, sampleRate / 50);
            int numWindows = totalFrames / windowSize;
            float[] rms = new float[numWindows];
            float maxRms = 0f;
            for (int w = 0; w < numWindows; w++)
            {
                float sumSq = 0f;
                int start = w * windowSize;
                for (int j = 0; j < windowSize; j++)
                {
                    float s = mono[start + j];
                    sumSq += s * s;
                }
                rms[w] = Mathf.Sqrt(sumSq / windowSize);
                if (rms[w] > maxRms) maxRms = rms[w];
            }

            int minSpacing = (int)(0.25f * sampleRate / windowSize);
            float thresh = maxRms * 0.15f;
            var peakWindows = new System.Collections.Generic.List<int>();
            for (int w = 1; w < numWindows - 1; w++)
            {
                if (rms[w] > thresh && rms[w] >= rms[w - 1] && rms[w] >= rms[w + 1])
                {
                    if (peakWindows.Count == 0 || (w - peakWindows[peakWindows.Count - 1]) >= minSpacing)
                        peakWindows.Add(w);
                    else if (rms[w] > rms[peakWindows[peakWindows.Count - 1]])
                        peakWindows[peakWindows.Count - 1] = w;
                }
            }

            int countToExtract = Mathf.Clamp(peakWindows.Count, 1, maxSlices);
            var result = new System.Collections.Generic.List<AudioClip>();

            int stepDurationFrames = (int)(0.35f * sampleRate);
            int leadInFrames = (int)(0.03f * sampleRate);
            int fadeInFrames = (int)(0.005f * sampleRate);
            int fadeOutFrames = (int)(0.05f * sampleRate);

            for (int s = 0; s < countToExtract; s++)
            {
                int peakFrame = peakWindows[s] * windowSize;
                int startFrame = Mathf.Max(0, peakFrame - leadInFrames);
                int endFrame = Mathf.Min(totalFrames, startFrame + stepDurationFrames);
                int length = endFrame - startFrame;

                float[] slice = new float[length];
                float sliceMax = 0f;
                for (int i = 0; i < length; i++)
                {
                    float val = mono[startFrame + i];
                    if (i < fadeInFrames) val *= (float)i / fadeInFrames;
                    int fromEnd = length - 1 - i;
                    if (fromEnd < fadeOutFrames) val *= (float)fromEnd / fadeOutFrames;
                    slice[i] = val;
                    float abs = Mathf.Abs(val);
                    if (abs > sliceMax) sliceMax = abs;
                }

                if (sliceMax > 0.01f)
                {
                    float norm = 0.85f / sliceMax;
                    for (int i = 0; i < length; i++) slice[i] *= norm;
                }

                var clip = AudioClip.Create($"Recorded_Footstep_0{s + 1}", length, 1, sampleRate, false);
                clip.SetData(slice, 0);
                result.Add(clip);
            }

            return result.ToArray();
        }

        public static AudioClip GetFootstepClip(int variant)
        {
            var recorded = LoadRecordedFootstepClips();
            if (recorded != null && recorded.Length > 0)
            {
                int idx = Mathf.Abs(variant) % recorded.Length;
                if (recorded[idx] != null)
                {
                    return recorded[idx];
                }
            }

            // Procedural fallback strictly if recorded clips cannot be loaded
            return FacilityAudioSynthesizer.GetOrCreateFootstepClip(variant);
        }

        /// <summary>
        /// Emits a single natural footstep audio event with speed scaling and subtle variant alternating.
        /// Uses real recorded footstep audio as DEFAULT, falling back to procedural audio only if missing.
        /// </summary>
        private void EmitFootstep(float speed)
        {
            int variant = stepVariantCounter;
            stepVariantCounter++;
            totalStepsEmitted++;

            AudioClip clip = GetFootstepClip(variant);
            if (clip == null) return;

            if (footstepAudioSource == null)
            {
                InitializeAudioSource();
                if (footstepAudioSource == null) return;
            }

            // Dynamic volume scaling based on walking vs sprinting
            float speedScale = Mathf.Clamp(speed / 4.0f, 0.90f, 1.20f);
            float volume = Mathf.Clamp01(stepVolume * speedScale);

            // Subtle pitch micro-variation
            float pitch = Random.Range(0.97f, 1.03f);
            footstepAudioSource.pitch = pitch;
            footstepAudioSource.PlayOneShot(clip, volume);

            if (totalStepsEmitted == 1 || (totalStepsEmitted % 15 == 0))
            {
                Debug.Log($"[FpsFootstepController] Emitted recorded footstep #{totalStepsEmitted} on '{gameObject.name}' ({clip.name}, Speed: {speed:F2}m/s, Volume: {volume:F2})");
            }
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
            float speed = dt > 0.0001f ? dist / dt : 0f;

            if (!grounded)
            {
                stepDistanceAccumulator = 0f;
                return;
            }

            if (!hasInput || speed < minMoveSpeed)
            {
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