using System.Collections.Generic;
using UnityEngine;

namespace FacilityBreach.Audio
{
    /// <summary>
    /// GV Part 2: Facility Audio Player.
    /// Manages bounded audio playback for player weapon fire, kick interactions,
    /// and spatial 3D collision impacts using procedurally synthesized clips.
    /// </summary>
    public sealed class FacilityAudioPlayer : MonoBehaviour
    {
        private static FacilityAudioPlayer instance;
        public static FacilityAudioPlayer Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<FacilityAudioPlayer>();
                    if (instance == null)
                    {
                        var go = new GameObject("[FacilityAudioPlayer]");
                        instance = go.AddComponent<FacilityAudioPlayer>();
                    }
                }
                return instance;
            }
        }

        [Header("Audio Pooling Configuration")]
        [SerializeField] private int spatialPoolCapacity = 8;
        [SerializeField] private float masterVolume = 0.95f;
        [SerializeField] private float minImpactDistance = 1.5f;
        [SerializeField] private float maxImpactDistance = 35.0f;

        private AudioSource player2dSource;
        private AudioSource[] spatialSourcePool;
        private int nextSpatialSourceIndex;
        private bool initialized;

        public int SpatialPoolCapacity => spatialPoolCapacity;
        public float MasterVolume => masterVolume;

        private void Awake()
        {
            if (instance == null) instance = this;
            InitializePool();
        }

        public void InitializePool()
        {
            if (initialized) return;

            // Dedicated 2D source for player-perspective firing and kick feedback
            player2dSource = gameObject.AddComponent<AudioSource>();
            player2dSource.playOnAwake = false;
            player2dSource.spatialBlend = 0f; // 2D stereo
            player2dSource.volume = masterVolume;

            // Bounded ring-buffer pool of 3D sources for spatial physics impacts
            spatialSourcePool = new AudioSource[spatialPoolCapacity];
            for (int i = 0; i < spatialPoolCapacity; i++)
            {
                var childGo = new GameObject($"SpatialAudioSource_{i:D2}");
                childGo.transform.SetParent(transform, false);
                var src = childGo.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f; // Full 3D
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = minImpactDistance;
                src.maxDistance = maxImpactDistance;
                src.volume = masterVolume;
                spatialSourcePool[i] = src;
            }

            initialized = true;
        }

        /// <summary>
        /// Plays the gunshot sound for the equipped weapon (Ammo1, Ammo2, Ammo3).
        /// </summary>
        public void PlayGunshot(int weaponIndex, float volumeScale = 1.0f)
        {
            InitializePool();
            FacilityAudioClipType clipType = weaponIndex switch
            {
                0 => FacilityAudioClipType.GunshotAmmo1,
                1 => FacilityAudioClipType.GunshotAmmo2,
                2 => FacilityAudioClipType.GunshotAmmo3,
                _ => FacilityAudioClipType.GunshotAmmo1
            };

            AudioClip clip = FacilityAudioSynthesizer.GetOrCreateClip(clipType);
            if (clip != null && player2dSource != null)
            {
                player2dSource.pitch = Random.Range(0.97f, 1.03f);
                player2dSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale * masterVolume));
            }
        }

        /// <summary>
        /// Plays spatial impact audio (Wood/Metal) at the collision contact point.
        /// </summary>
        public void PlayImpact(FacilityAudioClipType clipType, Vector3 worldPosition, float volumeScale = 1.0f)
        {
            InitializePool();
            if (spatialSourcePool == null || spatialSourcePool.Length == 0) return;

            AudioClip clip = FacilityAudioSynthesizer.GetOrCreateClip(clipType);
            if (clip == null) return;

            AudioSource source = spatialSourcePool[nextSpatialSourceIndex];
            nextSpatialSourceIndex = (nextSpatialSourceIndex + 1) % spatialSourcePool.Length;

            if (source != null)
            {
                source.transform.position = worldPosition;
                source.pitch = Random.Range(0.94f, 1.06f);
                source.PlayOneShot(clip, Mathf.Clamp01(volumeScale * masterVolume));
            }
        }

        /// <summary>
        /// Plays kick contact thud at the targeted physics prop.
        /// </summary>
        public void PlayKick(Vector3 worldPosition, float volumeScale = 1.0f)
        {
            InitializePool();
            AudioClip clip = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.KickThud);
            if (clip != null && player2dSource != null)
            {
                player2dSource.pitch = Random.Range(0.95f, 1.05f);
                player2dSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale * masterVolume));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }
    }
}
