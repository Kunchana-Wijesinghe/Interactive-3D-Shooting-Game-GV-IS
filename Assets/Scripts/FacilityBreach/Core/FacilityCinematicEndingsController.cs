using System;
using System.Collections.Generic;
using UnityEngine;
using FacilityBreach.Audio;
using FacilityBreach;

namespace FacilityBreach.Core
{
    public enum EndingSequenceType
    {
        None,
        MeltdownExplosion,
        PlayerDeathKIA,
        ExtractionLockdown
    }

    /// <summary>
    /// GV Part 2: Cinematic Mission Endings Controller.
    /// Manages dramatic real-time conclusion sequences:
    /// 1. Catastrophic Core Meltdown Explosion: violent camera tremor, blinding nuclear fireball flash,
    ///    radial blast physics launching barrels/crates across the facility, and seismic sub-bass rumble.
    /// 2. Operative Fatal KIA: first-person eye-level ground collapse (Z-roll fall to concrete),
    ///    slowing cardiac vitals audio into flatline, and blood vignette blackout.
    /// 3. Extraction & Tactical Lockdown: heavy blast door hydraulic slam and security latch bolt lock,
    ///    switching emergency alert wall sconces from red to stable green.
    /// </summary>
    public sealed class FacilityCinematicEndingsController : MonoBehaviour
    {
        public static FacilityCinematicEndingsController Instance { get; private set; }

        [Header("Sequence Durations")]
        [SerializeField] private float meltdownDuration = 3.2f;
        [SerializeField] private float deathDuration = 2.6f;
        [SerializeField] private float extractionDuration = 2.2f;

        [Header("Explosion Blast Physics")]
        [SerializeField] private float blastForce = 850f;
        [SerializeField] private float blastRadius = 35f;
        [SerializeField] private float blastUpwardModifier = 2.2f;

        public EndingSequenceType ActiveEnding { get; private set; } = EndingSequenceType.None;
        public float EndingTimer { get; private set; }
        public float EndingDuration { get; private set; } = 3.0f;
        public bool IsPlaying => ActiveEnding != EndingSequenceType.None && EndingTimer < EndingDuration;
        public bool IsComplete => ActiveEnding != EndingSequenceType.None && EndingTimer >= EndingDuration;
        public float NormalizedProgress => EndingDuration > 0f ? Mathf.Clamp01(EndingTimer / EndingDuration) : 1f;

        // Camera references & collapse tracking
        private Camera targetCamera;
        private Vector3 initialCamLocalPos;
        private Quaternion initialCamLocalRot;
        private bool hasRecordedCamDefaults;
        private float deathDropY = 0.22f;
        private float deathRollZ = 72f;

        // Overlay textures
        private Texture2D whiteTexture;
        private GUIStyle bannerStyle;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
        }

        public void BindCamera(Camera cam)
        {
            targetCamera = cam;
            if (targetCamera != null && !hasRecordedCamDefaults)
            {
                initialCamLocalPos = targetCamera.transform.localPosition;
                initialCamLocalRot = targetCamera.transform.localRotation;
                hasRecordedCamDefaults = true;
            }
        }

        /// <summary>
        /// Sequence 1: Triggered when countdown timer expires (Meltdown Detonation).
        /// </summary>
        public void TriggerMeltdownExplosion(Vector3 epicenter)
        {
            if (ActiveEnding != EndingSequenceType.None) return;

            ActiveEnding = EndingSequenceType.MeltdownExplosion;
            EndingDuration = meltdownDuration;
            EndingTimer = 0f;

            // 1. Play full-range explosion audio
            if (FacilityAudioPlayer.Instance != null)
            {
                FacilityAudioPlayer.Instance.PlayCatastrophicExplosion(epicenter, 1.0f);
            }

            // 2. Heavy explosive camera trauma
            if (FacilityEmergencyAlertController.Instance != null)
            {
                FacilityEmergencyAlertController.Instance.AddCameraTrauma(1.0f);
            }

            // 3. Physical shockwave: launch all nearby barrels, crates, and rigidbodies violently
            ApplyExplosiveShockwave(epicenter);
        }

        /// <summary>
        /// Sequence 2: Triggered when player health reaches zero (Fatal KIA).
        /// </summary>
        public void TriggerPlayerDeathKIA(Vector3 deathPos)
        {
            if (ActiveEnding != EndingSequenceType.None) return;

            ActiveEnding = EndingSequenceType.PlayerDeathKIA;
            EndingDuration = deathDuration;
            EndingTimer = 0f;

            if (targetCamera != null && !hasRecordedCamDefaults)
            {
                initialCamLocalPos = targetCamera.transform.localPosition;
                initialCamLocalRot = targetCamera.transform.localRotation;
                hasRecordedCamDefaults = true;
            }

            // 1. Play cardiac slowing and flatline audio
            if (FacilityAudioPlayer.Instance != null)
            {
                FacilityAudioPlayer.Instance.PlayPlayerDeathVitals(1.0f);
            }
        }

        /// <summary>
        /// Sequence 3: Triggered when player reaches exit with card deactivated (Mission Complete).
        /// </summary>
        public void TriggerExtractionLockdown(Vector3 exitDoorPos)
        {
            if (ActiveEnding != EndingSequenceType.None) return;

            ActiveEnding = EndingSequenceType.ExtractionLockdown;
            EndingDuration = extractionDuration;
            EndingTimer = 0f;

            // 1. Heavy blast door slam and latch lock sound
            if (FacilityAudioPlayer.Instance != null)
            {
                FacilityAudioPlayer.Instance.PlayBlastDoorLock(exitDoorPos, 1.0f);
            }

            // 2. Restore normal / secure green lighting throughout the facility
            if (FacilityEmergencyAlertController.Instance != null)
            {
                FacilityEmergencyAlertController.Instance.DeactivateEmergency();
            }
        }

        public void ResetSequence()
        {
            ActiveEnding = EndingSequenceType.None;
            EndingTimer = 0f;
            if (targetCamera != null && hasRecordedCamDefaults)
            {
                targetCamera.transform.localPosition = initialCamLocalPos;
                targetCamera.transform.localRotation = initialCamLocalRot;
            }
        }

        public void Tick(float dt)
        {
            if (ActiveEnding == EndingSequenceType.None) return;
            EndingTimer += dt;
            UpdateCameraTransform();
        }

        private void Update()
        {
            if (ActiveEnding == EndingSequenceType.None) return;
            EndingTimer += Time.deltaTime;
        }

        private void LateUpdate()
        {
            UpdateCameraTransform();
        }

        private void UpdateCameraTransform()
        {
            if (ActiveEnding == EndingSequenceType.None || targetCamera == null) return;

            if (ActiveEnding == EndingSequenceType.MeltdownExplosion)
            {
                // Violent seismic tremor during first 1.5s, decaying into subtle rumble
                float progress = NormalizedProgress;
                float trauma = Mathf.Clamp01(1.0f - progress);
                float shakeIntensity = trauma * trauma;

                float pitchTremor = (Mathf.PerlinNoise(Time.time * 38f, 0f) * 2f - 1f) * 9.5f * shakeIntensity;
                float yawTremor   = (Mathf.PerlinNoise(0f, Time.time * 38f) * 2f - 1f) * 9.5f * shakeIntensity;
                float rollTremor  = (Mathf.PerlinNoise(Time.time * 38f, Time.time * 38f) * 2f - 1f) * 6.0f * shakeIntensity;

                targetCamera.transform.localRotation = initialCamLocalRot * Quaternion.Euler(pitchTremor, yawTremor, rollTremor);
            }
            else if (ActiveEnding == EndingSequenceType.PlayerDeathKIA)
            {
                // Smooth camera ground collapse (eye level 1.6m down to concrete floor 0.22m) and sideways roll
                float dropProgress = Mathf.Clamp01(EndingTimer / 1.6f);
                // Ease-out ground impact curve with subtle bounce
                float easeDrop = Mathf.Sin(dropProgress * Mathf.PI * 0.5f);

                float targetY = Mathf.Lerp(initialCamLocalPos.y, deathDropY, easeDrop);
                targetCamera.transform.localPosition = new Vector3(initialCamLocalPos.x, targetY, initialCamLocalPos.z);

                float targetRoll = Mathf.Lerp(0f, deathRollZ, easeDrop);
                float headNodPitch = Mathf.Sin(dropProgress * Mathf.PI) * 12f;
                targetCamera.transform.localRotation = initialCamLocalRot * Quaternion.Euler(headNodPitch, 0f, targetRoll);
            }
        }

        private void ApplyExplosiveShockwave(Vector3 epicenter)
        {
            var colliders = UnityEngine.Physics.OverlapSphere(epicenter, blastRadius);
            var affectedRbs = new HashSet<Rigidbody>();

            foreach (var col in colliders)
            {
                var rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic && affectedRbs.Add(rb))
                {
                    // Random angular impulse to ensure barrels roll dramatically
                    rb.AddExplosionForce(blastForce, epicenter, blastRadius, blastUpwardModifier, ForceMode.Impulse);
                    rb.AddTorque(UnityEngine.Random.insideUnitSphere * blastForce * 0.25f, ForceMode.Impulse);
                }
            }
        }

        private void OnGUI()
        {
            if (ActiveEnding == EndingSequenceType.None) return;
            if (whiteTexture == null) return;

            float p = NormalizedProgress;

            if (ActiveEnding == EndingSequenceType.MeltdownExplosion)
            {
                // Fullscreen Nuclear Fireball Flash: White-Hot -> Blazing Orange -> Dark Smoky Ember Blackout
                Color flashColor;
                if (p < 0.15f)
                {
                    // Initial blinding white-hot flash
                    float a = p / 0.15f;
                    flashColor = Color.Lerp(new Color(1f, 1f, 1f, 0f), new Color(1.0f, 0.98f, 0.85f, 0.98f), a);
                }
                else if (p < 0.55f)
                {
                    // Transition to raging fiery orange-red
                    float a = (p - 0.15f) / 0.40f;
                    flashColor = Color.Lerp(new Color(1.0f, 0.98f, 0.85f, 0.98f), new Color(1.0f, 0.32f, 0.04f, 0.85f), a);
                }
                else
                {
                    // Fade into dark smoky charcoal blackout
                    float a = (p - 0.55f) / 0.45f;
                    flashColor = Color.Lerp(new Color(1.0f, 0.32f, 0.04f, 0.85f), new Color(0.04f, 0.03f, 0.02f, 0.95f), a);
                }

                GUI.color = flashColor;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);
                GUI.color = Color.white;

                // High-intensity tactical warning alert banner
                if (p > 0.35f)
                {
                    DrawTacticalBanner("// CATASTROPHIC FACILITY MELTDOWN DETONATED", new Color(1f, 0.25f, 0.15f, Mathf.Clamp01((p - 0.35f) / 0.3f)));
                }
            }
            else if (ActiveEnding == EndingSequenceType.PlayerDeathKIA)
            {
                // Expanding blood-red tunnel vignette fading into black
                float alpha = Mathf.Clamp01(p * 1.2f);
                Color deathColor = Color.Lerp(new Color(0.45f, 0.02f, 0.02f, 0.65f), new Color(0.02f, 0.01f, 0.01f, 0.95f), p);
                deathColor.a = alpha;

                GUI.color = deathColor;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);
                GUI.color = Color.white;

                if (p > 0.40f)
                {
                    DrawTacticalBanner("// CRITICAL: VITAL SIGNS TERMINATED — OPERATIVE KIA", new Color(1.0f, 0.20f, 0.20f, Mathf.Clamp01((p - 0.40f) / 0.3f)));
                }
            }
            else if (ActiveEnding == EndingSequenceType.ExtractionLockdown)
            {
                // Security Scanline Iris & Flash: Emergency Red cleared to Stable Security Green
                if (p < 0.35f)
                {
                    float flashA = Mathf.Sin(p / 0.35f * Mathf.PI) * 0.40f;
                    GUI.color = new Color(0.00f, 0.95f, 0.50f, flashA);
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);
                    GUI.color = Color.white;
                }

                if (p > 0.20f)
                {
                    DrawTacticalBanner("// EXTRACTION CONFIRMED: FACILITY SECURED & LOCKED DOWN", new Color(0.00f, 0.95f, 0.50f, Mathf.Clamp01((p - 0.20f) / 0.3f)));
                }
            }
        }

        private void DrawTacticalBanner(string message, Color col)
        {
            if (bannerStyle == null)
            {
                bannerStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            bannerStyle.normal.textColor = col;

            float w = 560f;
            float h = 42f;
            Rect r = new Rect(Screen.width / 2f - w / 2f, Screen.height * 0.32f, w, h);

            Color bg = new Color(0.04f, 0.04f, 0.04f, col.a * 0.92f);
            GUI.color = bg;
            GUI.DrawTexture(r, whiteTexture);

            // Border lines
            GUI.color = col;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2), whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 2, r.width, 2), whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 4, r.height), whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 4, r.y, 4, r.height), whiteTexture);

            GUI.Label(r, message, bannerStyle);
            GUI.color = Color.white;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (whiteTexture != null) Destroy(whiteTexture);
        }
    }
}
