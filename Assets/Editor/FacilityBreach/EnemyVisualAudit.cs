using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FacilityBreach.AI.SoundHunter;
using FacilityBreach.AI.Roles;
using FacilityBreach.Combat;
using FacilityBreach.Core;
using FacilityBreach.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FacilityBreach.Editor
{
    public static class EnemyVisualAudit
    {
        private static readonly Stack<IEnumerator> pending = new Stack<IEnumerator>();
        private static readonly List<string> errors = new List<string>();
        private static readonly List<string> evidence = new List<string>();
        private static double deadline;
        private static int checks;
        public static void Run()
        {
            File.Delete("enemy-visuals-passed.txt"); File.Delete("enemy-visuals-failed.txt");
            EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
            SessionState.SetBool("FBEnemyVisuals", true); Register(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod]
        private static void Restore() { if (SessionState.GetBool("FBEnemyVisuals", false)) Register(); }
        private static void Register()
        {
            deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
            EditorApplication.update -= Step; EditorApplication.update += Step;
            Application.logMessageReceived -= Console; Application.logMessageReceived += Console;
        }
        private static void Console(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        private static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) pending.Push(Checks());
            if (state == PlayModeStateChange.EnteredEditMode && File.Exists("enemy-visuals-passed.txt"))
            { SessionState.SetBool("FBEnemyVisuals", false); EditorApplication.Exit(0); }
        }
        private static void Step()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Fail(new Exception("Play Mode validation timeout")); return; }
            if (pending.Count == 0) return;
            try
            {
                var task = pending.Peek();
                if (!task.MoveNext()) { if (pending.Count > 0 && ReferenceEquals(pending.Peek(), task)) pending.Pop(); return; }
                if (task.Current is IEnumerator nested) pending.Push(nested);
            }
            catch (Exception exception) { Fail(exception); }
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("FAILED: " + message); checks++; evidence.Add("PASS: " + message); Debug.Log("FB integration PASS: " + message); }
        private static IEnumerator Wait(float duration)
        { float end = Time.time + duration; while (Time.time < end) yield return null; }
        private static void Warp(FacilityBreachRuntime run, Vector3 point, Vector3 aim)
        {
            var body = run.PlayerTransform.GetComponent<CharacterController>(); body.enabled = false;
            run.PlayerTransform.position = point; run.PlayerTransform.rotation = Quaternion.identity; body.enabled = true;
            run.GameplayCamera.transform.rotation = Quaternion.LookRotation(aim - run.GameplayCamera.transform.position);
            Physics.SyncTransforms();
        }
        private static void Approach(FacilityBreachRuntime run, ResourceStation station)
        { Warp(run, new Vector3(station.transform.position.x, .1f, station.transform.position.z - 1.2f), station.InteractionPoint); }
        private static void Capture(Camera camera,string file)
        {
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var target=new RenderTexture(1024,768,24);var texture=new Texture2D(1024,768,TextureFormat.RGB24,false);
            try { camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1024,768),0,0);texture.Apply();File.WriteAllBytes(file,texture.EncodeToPNG()); }
            finally { camera.targetTexture=oldTarget;RenderTexture.active=oldActive;UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(texture); }
        }
        private static IEnumerator Checks()
        {
            for(int i=0;i<20;i++)yield return null;
            Check(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null,"real graphics device available for camera render");
            var run=UnityEngine.Object.FindAnyObjectByType<FacilityBreachRuntime>();
            Capture(run.GameplayCamera,"enemy-rules-player-camera.png");
            foreach(var renderer in run.GameplayCamera.GetComponentsInChildren<Renderer>())
                foreach(var material in renderer.sharedMaterials)
                    if(material&&material.HasProperty("_Mode"))Check(material.GetFloat("_Mode")==0,"view-model material opaque: "+material.name);
            var photo=new GameObject("Temporary validation camera");var camera=photo.AddComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=80;camera.fieldOfView=50;
            var enemies=UnityEngine.Object.FindObjectsByType<EnemyCombatAdapter>();
            foreach(var enemy in enemies)
            {
                camera.transform.position=enemy.transform.position+new Vector3(0,1.2f,3.5f);camera.transform.LookAt(enemy.transform.position+Vector3.up*.9f);
                Capture(camera,"enemy-rules-"+enemy.Identity+".png");
                Bounds bounds=default;bool first=true;
                foreach(var renderer in enemy.GetComponentsInChildren<Renderer>())
                {
                    if(!renderer.enabled)continue;if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);
                    foreach(var material in renderer.sharedMaterials) if(material&&material.HasProperty("_Mode"))Check(material.GetFloat("_Mode")==0,enemy.Identity+" opaque material "+material.name);
                }
                Check(bounds.size.y<2&&bounds.size.x<1.5f&&bounds.size.z<1.5f,enemy.Identity+" live robot model bounds "+bounds.size);
            }
            UnityEngine.Object.Destroy(photo);yield return null;
            Check(errors.Count==0,"no unexpected visual audit Console errors: "+string.Join("; ",errors));
            File.WriteAllText("enemy-visuals-passed.txt",checks+" checks\n"+string.Join("\n",evidence));pending.Clear();EditorApplication.isPlaying=false;
        }
        private static void Fail(Exception exception)
        {
            SessionState.SetBool("FBEnemyVisuals", false); EditorApplication.update -= Step;
            File.WriteAllText("enemy-visuals-failed.txt", exception + "\n" + string.Join("\n", evidence) + "\nErrors: " + string.Join("; ", errors));
            EditorApplication.Exit(1);
        }
    }
}
