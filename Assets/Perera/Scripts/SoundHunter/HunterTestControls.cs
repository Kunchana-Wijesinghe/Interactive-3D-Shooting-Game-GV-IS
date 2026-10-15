using Perera.Core;
using UnityEngine;

namespace Perera.SoundHunter
{
    /// <summary>
    /// TEST-ONLY keyboard controls for the isolated Perera_SoundHunterTest scene (legacy Input).
    /// Teleport the player and inject synthetic sounds so every behaviour is reproducible without
    /// doors or other teammates' systems.
    ///   1..7  teleport player: A start, C, D, B open, B behind cover, F far room, E sealed vault
    ///   N / M  synthetic Thrown sound, SAME source id 9001, in room D / room C (refresh test)
    ///   U      synthetic sound on the unreachable platform (hearable, not walkable)
    ///   V      synthetic loud sound inside the acoustically disconnected vault
    ///   G      weak synthetic Footstep (strength 10) in room A (connected but too quiet)
    ///   Left click fires the player weapon (real gunshot, loudness 45).
    /// Public methods are also used by the automated scenario runner.
    /// </summary>
    public class HunterTestControls : MonoBehaviour
    {
        [SerializeField] private CharacterController player;
        [SerializeField] private Transform[] teleportPoints; // index 0..6
        [SerializeField] private Transform soundRoomD, soundRoomC, soundPlatform, soundVault, soundRoomA;
        [SerializeField] private float testLoudness = 45f;

        public const int SourceRefresh = 9001, SourcePlatform = 9002, SourceVault = 9003, SourceQuiet = 9004;

        /// <summary>Named world anchors used by the scenario runner: a, c, d, platform, vault.</summary>
        public Transform Anchor(string key)
        {
            switch (key)
            {
                case "a": return soundRoomA;
                case "c": return soundRoomC;
                case "d": return soundRoomD;
                case "platform": return soundPlatform;
                case "vault": return soundVault;
                default: return null;
            }
        }

        public void Bind(CharacterController p, Transform[] points, Transform d, Transform c, Transform platform, Transform vault, Transform a)
        { player = p; teleportPoints = points; soundRoomD = d; soundRoomC = c; soundPlatform = platform; soundVault = vault; soundRoomA = a; }

        private void Update()
        {
            for (int i = 0; i < 7; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Teleport(i);
            if (Input.GetKeyDown(KeyCode.N)) EmitAt(soundRoomD, testLoudness, SoundCategory.Thrown, SourceRefresh);
            if (Input.GetKeyDown(KeyCode.M)) EmitAt(soundRoomC, testLoudness, SoundCategory.Thrown, SourceRefresh);
            if (Input.GetKeyDown(KeyCode.U)) EmitAt(soundPlatform, testLoudness, SoundCategory.Thrown, SourcePlatform);
            if (Input.GetKeyDown(KeyCode.V)) EmitAt(soundVault, 200f, SoundCategory.Gunshot, SourceVault);
            if (Input.GetKeyDown(KeyCode.G)) EmitAt(soundRoomA, 10f, SoundCategory.Footstep, SourceQuiet);
        }

        public void Teleport(int index)
        {
            if (player == null || teleportPoints == null || index < 0 || index >= teleportPoints.Length || teleportPoints[index] == null) return;
            player.enabled = false;                       // CharacterController would fight a direct position set
            player.transform.position = teleportPoints[index].position;
            player.enabled = true;
        }

        public void EmitAt(Transform where, float strength, SoundCategory cat, int sourceId)
        {
            if (where == null) return;
            SoundEventBus.Emit(where.position, strength, cat, sourceId);
        }
    }
}
