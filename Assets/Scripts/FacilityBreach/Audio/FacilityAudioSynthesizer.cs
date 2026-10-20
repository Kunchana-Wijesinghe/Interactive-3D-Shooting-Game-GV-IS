using System;
using System.Collections.Generic;
using UnityEngine;

namespace FacilityBreach.Audio
{
    public enum FacilityAudioClipType
    {
        GunshotAmmo1,
        GunshotAmmo2,
        GunshotAmmo3,
        ImpactWood,
        ImpactMetal,
        KickThud,
        RollingMetal,
        ProjectileLaunch,
        ProjectileImpact
    }

    /// <summary>
    /// GV Part 2: Procedural Audio Synthesizer.
    /// Synthesizes deterministic, non-clipping PCM audio clips at runtime using AudioClip.Create,
    /// eliminating external binary dependencies and guaranteeing out-of-the-box audible feedback.
    /// Each clip is synthesized exactly once and cached for high-performance reuse.
    /// </summary>
    public static class FacilityAudioSynthesizer
    {
        private const int SampleRate = 44100;
        private static readonly Dictionary<FacilityAudioClipType, AudioClip> ClipCache = new Dictionary<FacilityAudioClipType, AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ClipCache.Clear();
        }

        public static AudioClip GetOrCreateClip(FacilityAudioClipType clipType)
        {
            if (ClipCache.TryGetValue(clipType, out var cachedClip) && cachedClip != null)
            {
                return cachedClip;
            }

            AudioClip generatedClip = clipType switch
            {
                FacilityAudioClipType.GunshotAmmo1      => SynthesizeGunshotAmmo1(),
                FacilityAudioClipType.GunshotAmmo2      => SynthesizeGunshotAmmo2(),
                FacilityAudioClipType.GunshotAmmo3      => SynthesizeGunshotAmmo3(),
                FacilityAudioClipType.ImpactWood        => SynthesizeImpactWood(),
                FacilityAudioClipType.ImpactMetal       => SynthesizeImpactMetal(),
                FacilityAudioClipType.KickThud          => SynthesizeKickThud(),
                FacilityAudioClipType.RollingMetal      => SynthesizeRollingMetal(),
                FacilityAudioClipType.ProjectileLaunch  => SynthesizeProjectileLaunch(),
                FacilityAudioClipType.ProjectileImpact  => SynthesizeProjectileImpact(),
                _                                       => SynthesizeGunshotAmmo1()
            };

            if (generatedClip != null)
            {
                ClipCache[clipType] = generatedClip;
            }

            return generatedClip;
        }

        /// <summary>
        /// Ammo1gun (Pistol/Light): Snappy high-frequency crack with rapid exponential decay (0.13s).
        /// </summary>
        private static AudioClip SynthesizeGunshotAmmo1()
        {
            float duration = 0.13f;
            int totalSamples = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new System.Random(1337);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-28f * t);

                // Initial attack burst (first 2ms)
                float attack = Mathf.Clamp01(t / 0.002f);
                float noise = ((float)rng.NextDouble() * 2f - 1f);

                // Pitch drop: 220Hz down to 65Hz
                float pitch = Mathf.Lerp(220f, 65f, Mathf.Clamp01(t / duration));
                float tone = Mathf.Sin(2f * Mathf.PI * pitch * t);

                samples[i] = attack * envelope * (0.65f * noise + 0.35f * tone);
            }

            NormalizeSamples(samples, 0.88f);
            AudioClip clip = AudioClip.Create("Procedural_Gunshot_Ammo1", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Ammo2gun (Shotgun/Heavy): Low-frequency resonant punch with wide crunchy noise burst (0.22s).
        /// </summary>
        private static AudioClip SynthesizeGunshotAmmo2()
        {
            float duration = 0.22f;
            int totalSamples = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new System.Random(2442);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-16f * t);

                float attack = Mathf.Clamp01(t / 0.003f);
                float noise = ((float)rng.NextDouble() * 2f - 1f);

                // Heavy sub-bass drop: 120Hz down to 40Hz
                float pitch = Mathf.Lerp(120f, 40f, Mathf.Clamp01(t / duration));
                float subBass = Mathf.Sin(2f * Mathf.PI * pitch * t);
                float midPunch = Mathf.Sin(2f * Mathf.PI * (pitch * 1.8f) * t);

                float combined = 0.55f * noise + 0.30f * subBass + 0.15f * midPunch;
                // Soft saturation curve for punchiness
                combined = Mathf.Clamp(combined * 1.3f, -1f, 1f);

                samples[i] = attack * envelope * combined;
            }

            NormalizeSamples(samples, 0.92f);
            AudioClip clip = AudioClip.Create("Procedural_Gunshot_Ammo2", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Ammo3gun (Tactical Rifle): Sharp metallic snap with crisp mechanical body (0.16s).
        /// </summary>
        private static AudioClip SynthesizeGunshotAmmo3()
        {
            float duration = 0.16f;
            int totalSamples = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new System.Random(3553);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-22f * t);

                float attack = Mathf.Clamp01(t / 0.0015f);
                float noise = ((float)rng.NextDouble() * 2f - 1f);

                // High-velocity crack: 340Hz down to 90Hz + metallic overtone
                float pitch = Mathf.Lerp(340f, 90f, Mathf.Clamp01(t / duration));
                float body = Mathf.Sin(2f * Mathf.PI * pitch * t);
                float overtone = Mathf.Sin(2f * Mathf.PI * (pitch * 2.3f) * t) * 0.25f;

                samples[i] = attack * envelope * (0.60f * noise + 0.30f * body + overtone);
            }

            NormalizeSamples(samples, 0.90f);
            AudioClip clip = AudioClip.Create("Procedural_Gunshot_Ammo3", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Wooden Impact: Muffled resonant thud with rapid acoustic damping (0.12s).
        /// </summary>
        private static AudioClip SynthesizeImpactWood()
        {
            float duration = 0.12f;
            int totalSamples = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new System.Random(4664);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-32f * t);
                float attack = Mathf.Clamp01(t / 0.002f);

                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.35f;
                // Woody resonance: 135Hz fundamental with 270Hz second harmonic
                float woodTone1 = Mathf.Sin(2f * Mathf.PI * 135f * t) * 0.50f;
                float woodTone2 = Mathf.Sin(2f * Mathf.PI * 270f * t) * 0.15f;

                samples[i] = attack * envelope * (noise + woodTone1 + woodTone2);
            }

            NormalizeSamples(samples, 0.85f);
            AudioClip clip = AudioClip.Create("Procedural_Impact_Wood", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Metallic Impact: Distinct metallic transient ping with inharmonic resonance (0.18s).
        /// </summary>
        private static AudioClip SynthesizeImpactMetal()
        {
            float duration = 0.18f;
            int totalSamples = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new System.Random(5775);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-19f * t);
                float attack = Mathf.Clamp01(t / 0.001f);

                float click = ((float)rng.NextDouble() * 2f - 1f) * 0.25f;
                // Dual metallic frequencies: 540Hz and 820Hz for characteristic metal chime
                float ring1 = Mathf.Sin(2f * Mathf.PI * 540f * t) * 0.45f;
                float ring2 = Mathf.Sin(2f * Mathf.PI * 820f * t) * 0.30f;

                samples[i] = attack * envelope * (click + ring1 + ring2);
            }

            NormalizeSamples(samples, 0.86f);
            AudioClip clip = AudioClip.Create("Procedural_Impact_Metal", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Kick Thud: Heavy, punchy impact with enhanced acoustic body and transient slap (0.12s).
        /// Tuned for clear audibility on laptop speakers while preserving low-end resonance.
        /// </summary>
        private static AudioClip SynthesizeKickThud()
        {
            float duration = 0.12f;
            int totalSamples = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new System.Random(6886);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-28f * t);
                float attack = Mathf.Clamp01(t / 0.0018f);

                // Punch sweep: 140Hz dropping to 55Hz for strong low-mid presence audible on laptop speakers
                float punchPitch = Mathf.Lerp(140f, 55f, Mathf.Clamp01(t / duration));
                float punchBody = Mathf.Sin(2f * Mathf.PI * punchPitch * t) * 0.55f;

                // Sub bass weight: 70Hz dropping to 38Hz
                float subPitch = Mathf.Lerp(70f, 38f, Mathf.Clamp01(t / duration));
                float subWeight = Mathf.Sin(2f * Mathf.PI * subPitch * t) * 0.35f;

                // Initial boot contact click/slap (fast-decaying mid harmonic + air burst)
                float contactKnock = Mathf.Sin(2f * Mathf.PI * 220f * t) * Mathf.Exp(-65f * t) * 0.25f;
                float airBurst = ((float)rng.NextDouble() * 2f - 1f) * 0.25f;

                float combined = punchBody + subWeight + contactKnock + airBurst;
                // Soft saturation curve for punchiness
                combined = Mathf.Clamp(combined * 1.25f, -1.0f, 1.0f);

                samples[i] = attack * envelope * combined;
            }

            NormalizeSamples(samples, 0.96f);
            AudioClip clip = AudioClip.Create("Procedural_Kick_Thud", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Rolling Metal: Seamless loopable metallic rumble and textured scraping for rolling cylindrical barrels (1.0s loop).
        /// Combines low hollow barrel resonance (72Hz, 108Hz) with metallic chime overtones and smoothed boundary crossfade.
        /// </summary>
        private static AudioClip SynthesizeRollingMetal()
        {
            float duration = 1.0f;
            int totalSamples = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new System.Random(7997);

            // Base textured surface friction noise
            float[] noiseBuffer = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                noiseBuffer[i] = ((float)rng.NextDouble() * 2f - 1f);
            }

            // Seamless boundary crossfade for noise (0.05s equal-power smooth blend)
            int fadeSamples = Mathf.CeilToInt(SampleRate * 0.05f);
            for (int i = 0; i < fadeSamples; i++)
            {
                float t = (float)i / fadeSamples;
                float blend = t * t * (3f - 2f * t);
                int wrapIdx = totalSamples - fadeSamples + i;
                noiseBuffer[wrapIdx] = noiseBuffer[wrapIdx] * (1f - blend) + noiseBuffer[i] * blend;
            }

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;

                // Exact integer cycle frequencies for seamless clickless looping over 1.0s
                float lowRumble1 = Mathf.Sin(2f * Mathf.PI * 72f * t) * 0.35f;
                float lowRumble2 = Mathf.Sin(2f * Mathf.PI * 108f * t) * 0.25f;
                float metallicShell = Mathf.Sin(2f * Mathf.PI * 360f * t) * 0.15f;
                float metallicRing = Mathf.Sin(2f * Mathf.PI * 540f * t) * 0.10f;

                // Subtle rotational wobble (6Hz integer cycle)
                float wobble = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 6f * t);
                float texturedFriction = noiseBuffer[i] * 0.25f * wobble;

                samples[i] = lowRumble1 + lowRumble2 + metallicShell + metallicRing + texturedFriction;
            }

            // Smoothly bridge the last 5ms to match sample 0 for seamless click-free looping
            int seamSamples = Mathf.CeilToInt(SampleRate * 0.005f);
            float targetSample = samples[0];
            for (int k = 0; k < seamSamples; k++)
            {
                int idx = totalSamples - seamSamples + k;
                float blend = (float)(k + 1) / seamSamples;
                float smooth = blend * blend * (3f - 2f * blend);
                samples[idx] = Mathf.Lerp(samples[idx], targetSample, smooth);
            }

            NormalizeSamples(samples, 0.85f);
            AudioClip clip = AudioClip.Create("Procedural_Rolling_Metal", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Projectile Launch: Distinctive aerodynamic whoosh and air displacement transient for Q distraction can throws (0.16s).
        /// </summary>
        private static AudioClip SynthesizeProjectileLaunch()
        {
            float duration = 0.16f;
            int totalSamples = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new System.Random(8118);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float attack = Mathf.Clamp01(t / 0.015f);
                float envelope = Mathf.Exp(-18f * t);

                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.45f;

                // Aerodynamic downward whoosh pitch sweep: 280Hz down to 80Hz
                float pitch = Mathf.Lerp(280f, 80f, Mathf.Clamp01(t / duration));
                float airBody = Mathf.Sin(2f * Mathf.PI * pitch * t) * 0.45f;
                float windOverhead = Mathf.Sin(2f * Mathf.PI * (pitch * 1.6f) * t) * 0.20f;

                samples[i] = attack * envelope * (noise + airBody + windOverhead);
            }

            NormalizeSamples(samples, 0.88f);
            AudioClip clip = AudioClip.Create("Procedural_Projectile_Launch", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Projectile Impact: Distinctive hollow metallic can/ball clatter with secondary bounce micro-transient (0.20s).
        /// Localized in 3D space when distraction cans collide with facility walls, floors, or obstacles.
        /// </summary>
        private static AudioClip SynthesizeProjectileImpact()
        {
            float duration = 0.20f;
            int totalSamples = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[totalSamples];
            var rng = new System.Random(9229);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float attack = Mathf.Clamp01(t / 0.001f);
                float envelope = Mathf.Exp(-22f * t);

                // Sharp transient click and hollow metallic can body resonance
                float click = ((float)rng.NextDouble() * 2f - 1f) * 0.30f;
                float cavityTone = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.40f;
                float canPing1 = Mathf.Sin(2f * Mathf.PI * 620f * t) * 0.35f;
                float canPing2 = Mathf.Sin(2f * Mathf.PI * 950f * t) * 0.20f;

                // Secondary micro-bounce clatter at ~0.045s (characteristic physical can bounce)
                float secondaryTime = t - 0.045f;
                float secondaryBounce = 0f;
                if (secondaryTime > 0f)
                {
                    float secEnv = Mathf.Exp(-35f * secondaryTime);
                    float secPing = Mathf.Sin(2f * Mathf.PI * 740f * secondaryTime);
                    secondaryBounce = secEnv * secPing * 0.35f;
                }

                samples[i] = attack * envelope * (click + cavityTone + canPing1 + canPing2) + secondaryBounce;
            }

            NormalizeSamples(samples, 0.90f);
            AudioClip clip = AudioClip.Create("Procedural_Projectile_Impact", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static void NormalizeSamples(float[] samples, float targetPeak)
        {
            float maxAbs = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float abs = Mathf.Abs(samples[i]);
                if (abs > maxAbs) maxAbs = abs;
            }

            if (maxAbs > 0.0001f)
            {
                float scale = targetPeak / maxAbs;
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] *= scale;
                }
            }
        }
    }
}
