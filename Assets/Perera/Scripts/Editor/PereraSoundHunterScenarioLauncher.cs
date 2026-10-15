using UnityEditor;
using UnityEditor.SceneManagement;

namespace Perera.EditorTools
{
    /// <summary>
    /// Opens Perera_SoundHunterTest and runs the automated Play Mode scenarios.
    /// Menu: Perera/Run Sound Hunter Scenarios. Batch: -executeMethod Perera.EditorTools.PereraSoundHunterScenarioLauncher.RunBatch
    /// (no -quit; the runner exits the editor with code 0 only if every check passed).
    /// </summary>
    public static class PereraSoundHunterScenarioLauncher
    {
        [MenuItem("Perera/Run Sound Hunter Scenarios")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(PereraSoundHunterTestSceneBuilder.ScenePath);
            System.Environment.SetEnvironmentVariable("PERERA_AUTORUN", "1");
            EditorApplication.EnterPlaymode();
        }

        public static void RunBatch() => Run();
    }
}
