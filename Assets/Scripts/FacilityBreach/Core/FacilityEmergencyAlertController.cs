using System.Collections.Generic;
using UnityEngine;
using FacilityBreach.Audio;
using FacilityBreach.Noise;
using FacilityBreach.Environment;

namespace FacilityBreach.Core
{
    /// <summary>
    /// GV / IS Facility Emergency Alert System.
    /// Manages the intense final-stage meltdown protocol:
    /// - Last 1 Minute (60s remaining): All facility lights (hanging + wall sconces) smoothly turn RED and pulse.
    /// - Last 30 Seconds (30s remaining): Siren audio triggers and plays alarm clip.
    /// - Last 10 Seconds (10s remaining): Climax urgency with increased explosion hazards and screen shake.
    /// - Custom Audio Slot: Public AudioClip property ready to accept user-provided sound file.
    /// </summary>
    public sealed class FacilityEmergencyAlertController : MonoBehaviour
    {
        public static FacilityEmergencyAlertController Instance { get; private set; }

        [Header("Phase Timing Thresholds")]
        [Tooltip("Remaining seconds when lights turn RED and start pulsing (Default: 60s / Last 1 Minute).")]
        [SerializeField] private float lightAlertRemainingSeconds = 60f;

        [Tooltip("Remaining seconds when siren audio triggers (Default: 30s / Last 30 Seconds).")]
        [SerializeField] private float soundAlertRemainingSeconds = 30f;

        [Tooltip("Remaining seconds when meltdown urgency reaches climax (Default: 10s).")]
        [SerializeField] private float climaxRemainingSeconds = 10f;

        [Header("Custom Audio Hook")]
        [Tooltip("Assign your chosen alarm sound file (.wav/.mp3) here. If unassigned, audio remains safely muted until your sound is ready.")]
        [SerializeField] private AudioClip customAlarmClip;
        public AudioClip CustomAlarmClip
        {
            get => customAlarmClip;
            set
            {
                customAlarmClip = value;
                if (sirenSource != null && value != null)
                {
                    sirenSource.clip = value;
                    if (IsSoundAlertActive && !sirenSource.isPlaying)
                    {
                        sirenSource.Play();
                    }
                }
            }
        }

        [Header("Lighting Configuration")]
        [SerializeField] private Color redAlertColor = new Color(1.0f, 0.06f, 0.06f, 1f);
        [SerializeField] private Color normalLightColor = new Color(1.0f, 0.70f, 0.32f, 1f);
        [SerializeField] private float colorTransitionSpeed = 1.6f;
        [SerializeField] private float pulseFrequency = 3.4f;
        [SerializeField] private float minPulseIntensity = 0.40f;
        [SerializeField] private float maxPulseIntensity = 3.4f;

        [Header("Hazard & Screen Shake")]
        [SerializeField] private float explosionIntervalMin = 6.0f;
        [SerializeField] private float explosionIntervalMax = 10.0f;
        [SerializeField] private float traumaDecayRate = 1.5f;
        [SerializeField] private float maxShakeAngle = 3.8f;
        [SerializeField] private float maxShakeOffset = 0.09f;

        // Active State Telemetry
        public bool AreLightsRedAlert { get; private set; }
        public bool IsSoundAlertActive { get; private set; }
        public bool IsClimaxActive { get; private set; }
        public bool IsEmergencyActive => AreLightsRedAlert || IsSoundAlertActive;

        public float LightAlertRemainingSeconds => lightAlertRemainingSeconds;
        public float SoundAlertRemainingSeconds => soundAlertRemainingSeconds;
        public float ClimaxRemainingSeconds => climaxRemainingSeconds;

        private FacilityBreachRuntime runtime;
        private AudioSource sirenSource;
        private readonly List<Light> cachedLights = new List<Light>();
        private readonly List<float> defaultIntensities = new List<float>();
        private readonly List<Color> defaultColors = new List<Color>();
        private readonly List<Material> glowMaterials = new List<Material>();

        private float colorTransitionProgress;
        private float nextExplosionTime;
        private float cameraTrauma;
        private float sirenVolumeTarget = 0.50f; // Reduced by half (50%) per user requirement
        private float nextSirenPlayTime;
        private Vector3 originalCameraLocalPos;
        private Quaternion originalCameraLocalRot;
        private bool cameraTransformCached;
        private Camera targetCamera;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public void Initialize(FacilityBreachRuntime hostRuntime)
        {
            runtime = hostRuntime;
            QualitySettings.pixelLightCount = 64;

            // Ensure modern wall sconces exist in the facility and have 4.2f intensity + ForcePixel
            GameObject sconceRoot = GameObject.Find(FacilityWallSconces.ROOT_NAME);
            if (sconceRoot == null)
            {
                FacilityWallSconces.GenerateAllSconces();
            }
            else
            {
                var spotLights = sconceRoot.GetComponentsInChildren<Light>(true);
                foreach (var spot in spotLights)
                {
                    if (spot != null && spot.type == LightType.Spot)
                    {
                        spot.intensity = 4.2f;
                        spot.renderMode = LightRenderMode.ForcePixel;
                    }
                }
            }

            CacheFacilityLightsAndMaterials();
            SetupSirenAudioSource();
            ScheduleNextExplosion();
        }

        private void SetupSirenAudioSource()
        {
            if (sirenSource == null)
            {
                sirenSource = gameObject.AddComponent<AudioSource>();
                sirenSource.loop = true;
                sirenSource.playOnAwake = false;
                sirenSource.spatialBlend = 0f; // 2D omnidirectional facility announcement
                sirenSource.volume = sirenVolumeTarget;

                if (customAlarmClip == null)
                {
                    customAlarmClip = UnityEngine.Resources.Load<AudioClip>("Audio/Emergency/facility_emergency_alarm");
                }

                if (customAlarmClip != null)
                {
                    sirenSource.clip = customAlarmClip;
                }
            }
        }

        public void CacheFacilityLightsAndMaterials()
        {
            cachedLights.Clear();
            defaultIntensities.Clear();
            defaultColors.Clear();
            glowMaterials.Clear();

            // Locate all point and spot lights across hanging fixtures and wall sconces
            Light[] allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in allLights)
            {
                if (l != null && (l.type == LightType.Point || l.type == LightType.Spot))
                {
                    cachedLights.Add(l);
                    defaultIntensities.Add(l.intensity);
                    defaultColors.Add(l.color);
                }
            }

            // Find all lamp glow materials to dynamically tint emission
            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                foreach (var mat in r.materials)
                {
                    if (mat != null && mat.name.Contains("YellowGlow") && !glowMaterials.Contains(mat))
                    {
                        glowMaterials.Add(mat);
                    }
                }
            }
        }

        private void Update()
        {
            if (runtime == null || runtime.Run == null) return;

            if (runtime.Run.Running && !runtime.Run.Paused)
            {
                double remaining = runtime.Run.Remaining;

                // Stop siren immediately if mission time has expired (3 minutes over / remaining <= 0)
                if (remaining <= 0.0)
                {
                    if (sirenSource != null && sirenSource.isPlaying)
                    {
                        sirenSource.Stop();
                    }
                    if (FacilityBreach.Audio.FacilityAudioPlayer.Instance != null && FacilityBreach.Audio.FacilityAudioPlayer.Instance.IsEmergencyAlarmPlaying)
                    {
                        FacilityBreach.Audio.FacilityAudioPlayer.Instance.StopEmergencyAlarm();
                    }
                    IsSoundAlertActive = false;
                }
                else
                {
                    // 1. Red Alert Lights: Triggers at last 1 minute (<= 60s)
                    if (remaining <= lightAlertRemainingSeconds)
                    {
                        if (!AreLightsRedAlert) ActivateRedAlertLights();
                    }

                    // 2. Sound Siren: Controlled timing during last 30 seconds (<= 30s)
                    if (remaining <= soundAlertRemainingSeconds)
                    {
                        UpdateSirenTiming(remaining);
                    }

                    // 3. Meltdown Climax: Triggers at last 10 seconds (<= 10s)
                    if (remaining <= climaxRemainingSeconds)
                    {
                        if (!IsClimaxActive) ActivateClimax();
                    }
                }
            }
            else
            {
                // When mission is not running (won, lost, menu) or remaining <= 0, ensure sound is stopped
                if (!runtime.Run.Running)
                {
                    if (sirenSource != null && sirenSource.isPlaying) sirenSource.Stop();
                    if (FacilityBreach.Audio.FacilityAudioPlayer.Instance != null && FacilityBreach.Audio.FacilityAudioPlayer.Instance.IsEmergencyAlarmPlaying)
                    {
                        FacilityBreach.Audio.FacilityAudioPlayer.Instance.StopEmergencyAlarm();
                    }
                    IsSoundAlertActive = false;
                }
            }

            // Update red alert pulsing lighting if active
            if (AreLightsRedAlert)
            {
                UpdateLighting();
            }

            // Maintain volume & pitch modulation during climax
            if (IsSoundAlertActive && sirenSource != null && sirenSource.isPlaying)
            {
                sirenSource.volume = sirenVolumeTarget;
                if (IsClimaxActive)
                {
                    sirenSource.pitch = Mathf.MoveTowards(sirenSource.pitch, 1.15f, Time.deltaTime * 0.1f);
                }
            }

            // Periodic hazard explosions during the alert phase
            if (AreLightsRedAlert && runtime.Run.Running && !runtime.Run.Paused && Time.time >= nextExplosionTime)
            {
                TriggerExplosionHazard();
                ScheduleNextExplosion();
            }

            // Camera trauma decay
            if (cameraTrauma > 0f)
            {
                cameraTrauma = Mathf.MoveTowards(cameraTrauma, 0f, Time.deltaTime * traumaDecayRate);
            }
        }

        public void ActivateRedAlertLights()
        {
            if (AreLightsRedAlert) return;
            AreLightsRedAlert = true;
            colorTransitionProgress = 0f;
            ScheduleNextExplosion(3.0f);
        }

        private void UpdateSirenTiming(double remaining)
        {
            if (sirenSource == null) SetupSirenAudioSource();
            if (sirenSource == null) return;

            if (sirenSource.clip == null)
            {
                if (customAlarmClip == null)
                    customAlarmClip = UnityEngine.Resources.Load<AudioClip>("Audio/Emergency/facility_emergency_alarm");
                sirenSource.clip = customAlarmClip;
            }

            if (sirenSource.clip == null) return;

            IsSoundAlertActive = true;
            sirenSource.volume = sirenVolumeTarget;

            // In the last 10 seconds: Play continuously without interval
            if (remaining <= climaxRemainingSeconds)
            {
                sirenSource.loop = true;
                if (!sirenSource.isPlaying)
                {
                    sirenSource.Play();
                }
            }
            // Between 30s and 10s: Play once, then 4s interval before next play
            else
            {
                sirenSource.loop = false;
                if (!sirenSource.isPlaying && Time.time >= nextSirenPlayTime)
                {
                    sirenSource.Play();
                    float clipDuration = sirenSource.clip != null ? sirenSource.clip.length : 1.5f;
                    nextSirenPlayTime = Time.time + clipDuration + 4.0f; // 4 second interval after play
                }
            }
        }

        public void ActivateSoundAlert()
        {
            if (IsSoundAlertActive) return;
            nextSirenPlayTime = 0f;
            if (runtime != null && runtime.Run != null)
            {
                UpdateSirenTiming(runtime.Run.Remaining);
            }
        }

        public void ActivateClimax()
        {
            if (IsClimaxActive) return;
            IsClimaxActive = true;
            pulseFrequency = 4.8f; // Faster, more intense flashing
            explosionIntervalMin = 3.5f;
            explosionIntervalMax = 6.0f;
            ScheduleNextExplosion(1.0f);
        }

        public void DeactivateEmergency()
        {
            AreLightsRedAlert = false;
            IsSoundAlertActive = false;
            IsClimaxActive = false;
            nextSirenPlayTime = 0f;

            if (sirenSource != null && sirenSource.isPlaying)
            {
                sirenSource.Stop();
            }

            if (FacilityBreach.Audio.FacilityAudioPlayer.Instance != null)
            {
                FacilityBreach.Audio.FacilityAudioPlayer.Instance.StopEmergencyAlarm();
            }

            RestoreNormalLighting();
            cameraTrauma = 0f;
        }

        public void PauseSiren(bool pause)
        {
            if (sirenSource != null)
            {
                if (pause && sirenSource.isPlaying) sirenSource.Pause();
                else if (!pause && IsSoundAlertActive && !sirenSource.isPlaying) sirenSource.UnPause();
            }

            if (FacilityBreach.Audio.FacilityAudioPlayer.Instance != null)
            {
                FacilityBreach.Audio.FacilityAudioPlayer.Instance.SetEmergencyAlarmPause(pause);
            }
        }

        private void UpdateLighting()
        {
            colorTransitionProgress = Mathf.MoveTowards(colorTransitionProgress, 1f, Time.deltaTime * colorTransitionSpeed);

            // Pulsing sine wave modulation
            float sinVal = Mathf.Sin(Time.time * pulseFrequency);
            float pulse01 = (sinVal + 1f) * 0.5f; // [0, 1]
            float currentIntensity = Mathf.Lerp(minPulseIntensity, maxPulseIntensity, pulse01);

            for (int i = 0; i < cachedLights.Count; i++)
            {
                var l = cachedLights[i];
                if (l == null) continue;

                Color baseColor = i < defaultColors.Count ? defaultColors[i] : normalLightColor;
                l.color = Color.Lerp(baseColor, redAlertColor, colorTransitionProgress);
                l.intensity = currentIntensity;
            }

            // Tint emission on lamp fixtures
            Color emissionColor = Color.Lerp(normalLightColor, redAlertColor, colorTransitionProgress) * pulse01 * 2.8f;
            for (int j = 0; j < glowMaterials.Count; j++)
            {
                var mat = glowMaterials[j];
                if (mat != null)
                {
                    mat.SetColor("_EmissionColor", emissionColor);
                }
            }
        }

        private void RestoreNormalLighting()
        {
            for (int i = 0; i < cachedLights.Count; i++)
            {
                var l = cachedLights[i];
                if (l == null) continue;

                if (i < defaultColors.Count) l.color = defaultColors[i];
                if (i < defaultIntensities.Count) l.intensity = defaultIntensities[i];
            }

            for (int j = 0; j < glowMaterials.Count; j++)
            {
                var mat = glowMaterials[j];
                if (mat != null)
                {
                    mat.SetColor("_EmissionColor", normalLightColor * 1.5f);
                }
            }
        }

        private void ScheduleNextExplosion(float initialDelay = -1f)
        {
            float delay = initialDelay > 0f ? initialDelay : Random.Range(explosionIntervalMin, explosionIntervalMax);
            nextExplosionTime = Time.time + delay;
        }

        private void TriggerExplosionHazard()
        {
            Vector3 playerPos = runtime != null && runtime.PlayerTransform != null 
                ? runtime.PlayerTransform.position 
                : Vector3.zero;

            Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(10f, 22f);
            Vector3 blastPosition = playerPos + new Vector3(randomCircle.x, 2.0f, randomCircle.y);

            // 1. Procedural explosion rumble
            if (FacilityAudioPlayer.Instance != null)
            {
                FacilityAudioPlayer.Instance.PlayExplosion(blastPosition, 1.0f);
            }

            // 2. Camera screen shake trauma (stronger during climax)
            AddCameraTrauma(IsClimaxActive ? 0.75f : 0.55f);

            // 3. Acoustic noise emission for AI perception (Patrol Guard PG-01 hearing)
            FacilityNoiseHub.Emit(blastPosition, 40f, FacilityNoiseType.PhysicsImpact, gameObject);

            // 4. Visual sparks effect
            if (runtime != null && runtime.ImpactSparks != null)
            {
                runtime.ImpactSparks.SpawnSparks(blastPosition, Vector3.down);
            }
        }

        public void AddCameraTrauma(float amount)
        {
            cameraTrauma = Mathf.Clamp01(cameraTrauma + amount);
        }

        private void LateUpdate()
        {
            if (runtime == null) return;
            Camera cam = runtime.GameplayCamera;
            if (cam == null) return;

            // Only apply explosive tremor when trauma is active.
            // Never overwrite or reset camera rotation when idle, so player mouse pitch functions with 100% freedom!
            if (cameraTrauma > 0.001f)
            {
                float shake = cameraTrauma * cameraTrauma;

                float pitch = (Mathf.PerlinNoise(Time.time * 28f, 0f) * 2f - 1f) * maxShakeAngle * shake;
                float yaw   = (Mathf.PerlinNoise(0f, Time.time * 28f) * 2f - 1f) * maxShakeAngle * shake;
                float roll  = (Mathf.PerlinNoise(Time.time * 28f, Time.time * 28f) * 2f - 1f) * (maxShakeAngle * 0.5f) * shake;

                cam.transform.localRotation *= Quaternion.Euler(pitch, yaw, roll);
            }
        }

        private void OnGUI()
        {
            if (!AreLightsRedAlert || runtime == null || !runtime.Run.Running) return;

            // Flashing warning banner at screen top
            float alpha = Mathf.PingPong(Time.time * (IsClimaxActive ? 4.5f : 2.5f), 1f);
            GUI.color = new Color(1f, 0.12f, 0.12f, Mathf.Lerp(0.45f, 1.0f, alpha));

            GUIStyle warningStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            warningStyle.normal.textColor = Color.white;

            float bannerWidth = 460f;
            float bannerHeight = 36f;
            Rect bannerRect = new Rect(Screen.width * 0.5f - bannerWidth * 0.5f, 15f, bannerWidth, bannerHeight);

            string alertText = IsClimaxActive
                ? "☠ CRITICAL MELTDOWN IN PROGRESS — IMMEDIATE EVACUATION ☠"
                : "⚠ WARNING: FACILITY CONTAINMENT BREACH (T-MINUS 01:00) ⚠";

            GUI.Box(bannerRect, alertText, warningStyle);
            GUI.color = Color.white;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (AreLightsRedAlert) RestoreNormalLighting();
        }
    }
}
