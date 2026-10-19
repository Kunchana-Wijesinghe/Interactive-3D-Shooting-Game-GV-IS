using System;
using System.Linq;
using FacilityBreach.Combat;
using UnityEditor;
using UnityEngine;

namespace FacilityBreach.Editor
{
    public static class AgentAnimationLibraryBuilder
    {
        [MenuItem("Facility Breach/GV Agent Controller/Rebuild FBX Animation Library")]
        public static void Rebuild()
        {
            const string path = "Assets/Resources/EnemyAnimationLibrary.asset";
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var library = AssetDatabase.LoadAssetAtPath<EnemyAnimationLibrary>(path);
            if (!library) { library = ScriptableObject.CreateInstance<EnemyAnimationLibrary>(); AssetDatabase.CreateAsset(library, path); }
            string[] models = { "PatrolGuard", "SoundHunter", "CoverDefender", "Flanker" };
            string[] ids = { "PG-01", "SH-01", "CD-03", "FL-04" };
            library.Enemies = new EnemyAnimationLibrary.Set[4];
            for (int i = 0; i < models.Length; i++)
            {
                string model = "Assets/Models/FacilityBreach/Enemies/" + models[i] + ".fbx";
                var clips = AssetDatabase.LoadAllAssetsAtPath(model).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                AnimationClip Clip(string state) => clips.FirstOrDefault(c => c.name.EndsWith("_" + state, StringComparison.OrdinalIgnoreCase) && c.length > 0);
                var set = new EnemyAnimationLibrary.Set { Identity = ids[i], Idle = Clip("Idle"), Move = Clip("Move"), Search = Clip("Search"), Attack = Clip("Attack"), Defeat = Clip("Defeat") };
                library.Enemies[i] = set;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FacilityBreach/Enemies/" + models[i] + "_Visual.prefab");
                var visual = prefab.GetComponentsInChildren<Transform>(true).First(t => t.name == "ModelVisual");
                foreach (var clip in new[] { set.Idle, set.Move, set.Search, set.Attack, set.Defeat })
                {
                    if (!clip) throw new InvalidOperationException("Missing usable clip: " + model);
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    int found = bindings.Count(b => string.IsNullOrEmpty(b.path) || visual.Find(b.path));
                    Debug.Log($"[GV clip audit] {ids[i]} {clip.name}: {clip.length:F2}s, {found}/{bindings.Length} bindings resolve below ModelVisual");
                    if (found != bindings.Length || bindings.Length == 0) throw new InvalidOperationException("Unusable clip bindings: " + clip.name);
                }
            }
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
        }
    }
}
