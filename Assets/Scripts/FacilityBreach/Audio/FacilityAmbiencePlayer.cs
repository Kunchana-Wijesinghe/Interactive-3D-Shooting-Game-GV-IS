using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FacilityBreach.Audio
{
    /// <summary>
    /// GV Part 2: Facility Industrial Environmental Ambience Player.
    /// Provides continuous, subtle 2D sci-fi background atmosphere (HVAC airflow, distant machinery,
    /// 60Hz hum) directly to the player's audio listener with smooth fade-in/fade-out lifecycle.
    /// 
    /// STRICT INVARIANT: Pure player-facing immersion audio.
    /// This component NEVER registers with or emits events to FacilityNoiseHub or SoundEventBus.
    /// AI patrol guards and noise-detection systems are completely unaffected (0 noise score / 0 hearing events).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FacilityAmbiencePlayer : MonoBehaviour
    {
        private static FacilityAmbiencePlayer instance;
        public static FacilityAmbiencePlayer Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<FacilityAmbiencePlayer>();
                    if (instance == null)
                    {
                        var go = new GameObject("[FacilityAmbiencePlayer]");
                        instance = go.AddComponent<FacilityAmbiencePlayer>();
                    }
                }
                return instance;
            }
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void RegisterEditorLifecycle()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.pauseStateChanged -= OnEditorPauseStateChanged;
            EditorApplication.pauseStateChanged += OnEditorPauseStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                StopAllAmbience();
            }
        }

        private static void OnEditorPauseStateChanged(PauseState state)
        {
            if (instance != null)
            {
                if (state == PauseState.Paused)
                {
                    instance.Pause();
                }
                else if (state == PauseState.Unpaused && Application.isPlaying && instance.shouldBePlaying)
                {
                    instance.Resume();
                }
            }
        }

        public static void StopAllAmbience()
        {
            if (instance != null)
            {
                instance.Stop(true);
            }

            var allSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (var src in allSources)
            {
                if (src != null && src.clip != null && src.clip.name == "Procedural_Facility_Ambience")
                {
                    src.volume = 0f;
                    src.Stop();
                }
            }
        }
#endif

        [Header("Ambience Configuration")]
        [Tooltip("Target playback volume for the 2D industrial ambience background (subtle presence).")]
        [Range(0f, 1f)]
        [SerializeField] private float targetVolume = 0.38f;

        [Tooltip("Fade speed (volume units per second). At 0.5f/s, fades from 0 to target in < 1s.")]
        [Range(0.01f, 2f)]
        [SerializeField] private float fadeSpeed = 0.50f;

        [Tooltip("Automatically synthesize and start playing on Start() during active gameplay.")]
        [SerializeField] private bool autoPlayOnStart = false;

        private AudioSource audioSource;
        private float desiredVolume;
        private bool isFadingOut;
        private bool isPaused;
        private bool shouldBePlaying;

        public AudioSource AudioSource => audioSource;
        public float TargetVolume
        {
            get => targetVolume;
            set
            {
                targetVolume = Mathf.Clamp01(value);
                if (!isFadingOut && !isPaused) desiredVolume = targetVolume;
            }
        }
        public float CurrentVolume => audioSource != null ? audioSource.volume : 0f;
        public bool IsPlaying => audioSource != null && audioSource.isPlaying;
        public bool IsPaused => isPaused;
        public bool ShouldBePlaying => shouldBePlaying;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                if (Application.isPlaying)
                    Destroy(this);
                else
                    DestroyImmediate(this);
                return;
            }

            InitializeAudioSource();
        }

        private void Start()
        {
            if (autoPlayOnStart && Application.isPlaying)
            {
                Play();
            }
        }

        private void Update()
        {
            if (audioSource == null) return;

            // Authoritative pause check: synchronize directly with FacilityBreachRuntime and Time.timeScale
            bool runtimePaused = (FacilityBreachRuntime.Instance != null && (FacilityBreachRuntime.Instance.Run.Paused || !FacilityBreachRuntime.Instance.Run.Running));
            if (Application.isPlaying && (runtimePaused || Time.timeScale <= 0f))
            {
                if (!isPaused && shouldBePlaying)
                {
                    Pause();
                }
                return;
            }

            if (isPaused) return;

            if (Mathf.Abs(audioSource.volume - desiredVolume) > 0.001f)
            {
                audioSource.volume = Mathf.MoveTowards(audioSource.volume, desiredVolume, fadeSpeed * Time.unscaledDeltaTime);
            }
            else
            {
                audioSource.volume = desiredVolume;
                if (isFadingOut && desiredVolume <= 0.0001f && audioSource.isPlaying)
                {
                    audioSource.Stop();
                    isFadingOut = false;
                }
            }
        }

        private void OnDisable()
        {
            shouldBePlaying = false;
            isPaused = false;
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.volume = 0f;
                audioSource.Stop();
            }
        }

        private void OnDestroy()
        {
            shouldBePlaying = false;
            isPaused = false;
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.volume = 0f;
                audioSource.Stop();
            }
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>
        /// Initializes the dedicated 2D AudioSource for ambient looping on a dedicated child GameObject.
        /// Guarantees complete isolation from FacilityAudioPlayer or any other AudioSource on the parent.
        /// </summary>
        public void InitializeAudioSource()
        {
            if (audioSource != null) return;

            Transform child = transform.Find("[FacilityAmbienceAudioSource]");
            GameObject childGo;
            if (child == null)
            {
                childGo = new GameObject("[FacilityAmbienceAudioSource]");
                childGo.transform.SetParent(transform, false);
            }
            else
            {
                childGo = child.gameObject;
            }

            audioSource = childGo.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = childGo.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f; // 2D stereo background (heard identically everywhere in facility)
            audioSource.volume = 0f;       // Start at zero for smooth fade-in
            audioSource.priority = 64;     // High priority so continuous ambience is never culled by SFX
            audioSource.dopplerLevel = 0f;
            audioSource.bypassEffects = true;
            audioSource.bypassListenerEffects = true;

            AudioClip clip = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.FacilityAmbience);
            if (clip != null)
            {
                audioSource.clip = clip;
            }
        }

        /// <summary>
        /// Begins playing the facility ambience with a smooth fade-in.
        /// </summary>
        public void Play()
        {
            InitializeAudioSource();

            if (audioSource.clip == null)
            {
                audioSource.clip = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.FacilityAmbience);
            }

            if (audioSource.clip != null)
            {
                if (!audioSource.isPlaying)
                {
                    audioSource.volume = 0f;
                    audioSource.Play();
                    Debug.Log($"[FacilityAmbiencePlayer] Playing 2D ambience clip '{audioSource.clip.name}' (Target Vol: {targetVolume:F2}, Priority: {audioSource.priority})");
                }
                else if (isPaused)
                {
                    audioSource.UnPause();
                }
            }

            isFadingOut = false;
            isPaused = false;
            shouldBePlaying = true;
            desiredVolume = targetVolume;
        }

        /// <summary>
        /// Pauses the facility ambience cleanly without resetting loop playback position.
        /// </summary>
        public void Pause()
        {
            isPaused = true;
            if (audioSource != null)
            {
                if (audioSource.isPlaying)
                {
                    audioSource.Pause();
                }
                audioSource.volume = 0f;
            }
        }

        /// <summary>
        /// Resumes the facility ambience from its current position.
        /// </summary>
        public void Resume()
        {
            if (!shouldBePlaying) return;
            if (FacilityBreachRuntime.Instance != null && (FacilityBreachRuntime.Instance.Run.Paused || !FacilityBreachRuntime.Instance.Run.Running)) return;

            InitializeAudioSource();
            if (audioSource != null)
            {
                if (!audioSource.isPlaying)
                {
                    audioSource.UnPause();
                    if (!audioSource.isPlaying && audioSource.clip != null)
                    {
                        audioSource.Play();
                    }
                }
                isPaused = false;
                desiredVolume = targetVolume;
            }
        }

        /// <summary>
        /// Stops the facility ambience, optionally fading out smoothly.
        /// </summary>
        public void Stop(bool immediate = false)
        {
            shouldBePlaying = false;
            isPaused = false;
            if (audioSource == null) return;

            if (immediate)
            {
                audioSource.volume = 0f;
                audioSource.Stop();
                desiredVolume = 0f;
                isFadingOut = false;
            }
            else
            {
                desiredVolume = 0f;
                isFadingOut = true;
            }
        }
    }
}
