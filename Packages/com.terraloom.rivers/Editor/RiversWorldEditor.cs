using System;
using System.IO;
using System.Text;
using TerraLoom.Core.Editor;
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
            var rivers = (TerraLoomRivers)target;
            serializedObject.Update();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent("TerraLoom Rivers", TerraLoomEditorIcons.Get("Rivers")), EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(!rivers.World))
                    if (GUILayout.Button("Open World Workbench")) TerraLoomWorldWindow.Open(rivers.World);
            }
            WorldInspectorSections.Draw(serializedObject, "rivers.core", "Core link / startup", true, "World", "GenerateOnStart");
            WorldInspectorSections.Draw(serializedObject, "rivers.targets", "Targets / network", true, "AutomaticSourceAndMouth", "Connections");
            WorldInspectorSections.Draw(serializedObject, "rivers.shape", "Shape / grade", true, "Width", "Depth", "BankWidth", "WaterInset", "MinimumBendRadius", "MaximumSlope", "BridgeClearance", "CarveTerrainCopy");
            WorldInspectorSections.Draw(serializedObject, "rivers.regions", "Regions / protected", true, "RegionCosts", "ProtectedAreas");
            WorldInspectorSections.Draw(serializedObject, "rivers.materials", "Materials", true, "WaterMaterial", "BedMaterial", "BankMaterial");
            WorldInspectorSections.Draw(serializedObject, "rivers.budgets", "Budgets", false, "CellSize", "SampleSpacing", "MaximumNodes", "TotalSearchBudget", "TotalSampleBudget", "MaximumGeometryVertices");
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Bed and Banks are visible channel surfaces with colliders. Depth lowers the bed below water. Water Inset lowers water below captured centre terrain (plus 2 cm); positive inset is experimental, requires terrain carving, extends water to sampled bank intersections and rejects actual terrain penetration before publication. Bank heights still follow local terrain. Clear restores the source terrain.", MessageType.Info);
            EditorGUILayout.LabelField("Terrain mode", rivers.CarveTerrainCopy ? "Excavation on an owned terrain copy; Clear restores the source" : "Mesh overlay; source terrain heights remain unchanged");
            EditorGUILayout.HelpBox("Assign a Core world and Terrain (foreign Terrain supported). Generate carves a copy when enabled; Clear restores the original. Bake before saving generated content.", MessageType.Info);
            EditorGUILayout.HelpBox("Visible bank seams follow local captured terrain; crossing offers keep a separate conservative clearance envelope. Excavation follows the final channel triangles, never fills terrain, and is capped at twice Depth plus Water Inset plus 2 cm clearance. Brown live scene lines show the actual planned bank seams.", MessageType.Info);
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
            if (rivers.LastPlan != null)
            {
                EditorGUILayout.LabelField("Final planned water centrelines", EditorStyles.boldLabel);
                foreach (var route in rivers.LastPlan.Routes) RouteInspectionGUI.Draw(route.Request.Id, route.WaterPolyline);
                EditorGUILayout.HelpBox("Measurements read the final water plan, not a separate smoothed preview. Full bed/bank validation remains the planner's responsibility.", MessageType.None);
            }
        }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception ex) { message = ex.Message; Debug.LogException(ex, target); }
            finally { Repaint(); }
        }
    }
}
