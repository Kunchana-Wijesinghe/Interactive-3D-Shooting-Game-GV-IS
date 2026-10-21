using System.Collections;
using System.IO;
using System.Linq;
using FacilityBreach;
using FacilityBreach.Audio;
using Perera.Core;
using FacilityBreach.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FacilityBreach.EditorTools
{
    /// <summary>Opens Prototype, enters Play mode, frames the first-aid kit and saves a Game view screenshot.</summary>
    public static class FirstAidKitScreenshot
    {
        private const string Flag = "FBFirstAidShot";
        private static IEnumerator routine;
        [MenuItem("Facility Breach/Capture first-aid kit and ammo crate screenshots")]
        public static void Run()
        {
            File.Delete("firstaid-done.txt");
            EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
            SessionState.SetBool(Flag, true); Register(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Restore() { if (SessionState.GetBool(Flag, false)) Register(); }
        private static void Register()
        {
            EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
            EditorApplication.update -= Step; EditorApplication.update += Step;
        }
        private static void Changed(PlayModeStateChange state)
        { if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Flag, false)) routine = Capture(); }
        private static void Step()
        {
            try { if (routine != null && !routine.MoveNext()) routine = null; }
            catch (System.Exception e) { File.WriteAllText("firstaid-failed.txt", e.ToString()); Finish(); }
        }
        private static void Finish()
        {
            SessionState.SetBool(Flag, false); routine = null; EditorApplication.update -= Step;
            EditorApplication.isPlaying = false;
        }
        private static void Look(FacilityBreachRuntime run, ResourceStation target)
        {
            var body = run.PlayerTransform.GetComponent<CharacterController>(); body.enabled = false;
            run.PlayerTransform.position = new Vector3(target.transform.position.x, .1f, target.transform.position.z - 1.6f);
            run.PlayerTransform.rotation = Quaternion.identity; body.enabled = true; Physics.SyncTransforms();
            run.GameplayCamera.transform.rotation = Quaternion.LookRotation(target.transform.position - run.GameplayCamera.transform.position);
        }
        private static IEnumerator Capture()
        {
            for (int i = 0; i < 30; i++) yield return null;
            var run = Object.FindAnyObjectByType<FacilityBreachRuntime>();
            run.StartMission();
            for (int i = 0; i < 30; i++) yield return null;
            var kit = Object.FindObjectsByType<ResourceStation>().First(s => s.Kind == StationKind.Health);
            Look(run, kit);
            for (int i = 0; i < 20; i++) yield return null;
            ScreenCapture.CaptureScreenshot("firstaid-kit.png");
            for (int i = 0; i < 20; i++) yield return null;
            var crate = Object.FindObjectsByType<ResourceStation>().First(c => c.Kind == StationKind.AmmoCrate);
            Look(run, crate);
            for (int i = 0; i < 20; i++) yield return null;
            ScreenCapture.CaptureScreenshot("ammo-crate-intact.png");
            for (int i = 0; i < 20; i++) yield return null;
            bool destroyed = crate.TakeDamage(new DamageInfo(10f, crate.transform.position, Vector3.forward, null));
            var clip = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.CrateDestroyed);
            for (int i = 0; i < 12; i++) yield return null;
            ScreenCapture.CaptureScreenshot("ammo-crate-destroyed.png");
            for (int i = 0; i < 20; i++) yield return null;
            File.WriteAllText("firstaid-done.txt", $"kit={kit.name} crossBars={kit.GetComponentsInChildren<Transform>().Count(t => t.name == "Cross bar")} crateDestroyed={destroyed} broken={crate.Broken} clip={(clip ? clip.length : 0)}s debris={GameObject.FindObjectsByType<Rigidbody>().Count(r => r.name == "Crate debris")}");
            Finish();
        }
    }
}
