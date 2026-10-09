using System;
using System.IO;
using System.Text;
using TerraLoom.Rivers.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TerraLoom.Rivers.Editor
{
    [CustomEditor(typeof(TerraLoomRivers))]
    public sealed class RiversWorldEditor : UnityEditor.Editor
    {
        private string message;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var rivers = (TerraLoomRivers)target;
            EditorGUILayout.HelpBox("Assign a Core world and Terrain (foreign Terrain supported). Generate carves a copy when enabled; Clear restores the original. Bake before saving generated content.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Generate")) Run(() =>
                {
                    if (!rivers.Generate()) throw new InvalidOperationException(rivers.Diagnostics);
                    RiversSampleBuilder.MarkDirty(rivers);
                    message = "Generated. Bake to persist terrain and geometry.";
                });
                if (GUILayout.Button("Clear")) Run(() => { rivers.Clear(); RiversSampleBuilder.MarkDirty(rivers); message = "Cleared; original terrain restored."; });
                using (new EditorGUI.DisabledScope(!rivers.GeneratedRoot))
                    if (GUILayout.Button("Bake Assets")) Run(() =>
                    {
                        string folder = EditorUtility.SaveFolderPanel("Bake Rivers into Assets", Application.dataPath, "RiversBake");
                        if (string.IsNullOrEmpty(folder)) return;
                        string relative = FileUtil.GetProjectRelativePath(folder).Replace('\\', '/');
                        if (relative != "Assets" && !relative.StartsWith("Assets/", StringComparison.Ordinal))
                            throw new InvalidOperationException("Choose a folder inside this project's Assets directory.");
                        RiversSampleBuilder.Bake(rivers, relative);
                        message = "Baked assets and bindings. Save the scene to retain them.";
                    });
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(rivers.PlanJson)))
                    if (GUILayout.Button("Save Plan JSON")) Run(() =>
                    {
                        string path = EditorUtility.SaveFilePanel("Save Rivers plan", "", "RiversPlan", "json");
                        if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, rivers.PlanJson, new UTF8Encoding(false));
                    });
                if (GUILayout.Button("Validate JSON against Current Input")) Run(() =>
                {
                    string path = EditorUtility.OpenFilePanel("Validate Rivers plan", "", "json");
                    if (string.IsNullOrEmpty(path)) return;
                    var input = rivers.CaptureInput(out var bounds, out var requests);
                    var expected = RiverPlanner.Plan(input, rivers.CaptureProfile(), bounds, requests);
                    RiverPlanStore.Validate(File.ReadAllText(path, Encoding.UTF8), expected);
                    message = "JSON matches current terrain, anchors, profile and plan.";
                });
                if (GUILayout.Button("Load and Validate JSON")) Run(() =>
                {
                    string path = EditorUtility.OpenFilePanel("Load Rivers plan", "", "json");
                    if (string.IsNullOrEmpty(path)) return;
                    if (!rivers.LoadPlanJson(File.ReadAllText(path, Encoding.UTF8))) throw new InvalidOperationException(rivers.Diagnostics);
                    RiversSampleBuilder.MarkDirty(rivers);
                    message = "Validated and generated. Bake before saving.";
                });
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            if (!string.IsNullOrEmpty(rivers.Diagnostics)) EditorGUILayout.HelpBox(rivers.Diagnostics, MessageType.None);
        }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception ex) { message = ex.Message; Debug.LogException(ex, target); }
            finally { Repaint(); }
        }
    }
}
