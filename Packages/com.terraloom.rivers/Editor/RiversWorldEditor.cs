using System;
using System.IO;
using System.Linq;
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
            if(!WorldWorkbenchGUI.IsDrawingEmbedded)
            {
                WorldInspectorSections.DrawWorkbenchEntry("Rivers",rivers.World,
                    string.IsNullOrEmpty(rivers.Diagnostics)?"No generated result yet":rivers.Diagnostics);
                return;
            }
            serializedObject.Update();
            WorldInspectorSections.DrawGeneral(serializedObject,
                "Link Core for terrain, targets and regions. Rivers can be generated here or at runtime from the same settings.",
                "World", "GenerateOnStart");
            WorldInspectorSections.DrawDetails(serializedObject, "rivers.targets", "02  Source and mouth", true, () =>
            {
                var automatic = serializedObject.FindProperty("AutomaticSourceAndMouth");
                EditorGUILayout.PropertyField(automatic);
                if (!automatic.boolValue) EditorGUILayout.PropertyField(serializedObject.FindProperty("Connections"), true);
                EditorGUILayout.HelpBox(automatic.boolValue
                    ? "Source and mouth follow Core targets, including seed-generated worlds."
                    : "Edit source-to-mouth connections below. Inputs may also be supplied dynamically at runtime.", MessageType.None);
            });
            WorldInspectorSections.DrawExplained(serializedObject, "rivers.shape", "03  Channel and terrain rules", true,
                "Width and bank width define the carved channel. Inset controls water height; bend and grade constrain its route.",
                "Width", "Depth", "BankWidth", "WaterInset", "MinimumBendRadius", "MaximumSlope", "BridgeClearance", "CarveTerrainCopy", "TerrainBrush");
            WorldInspectorSections.DrawExplained(serializedObject, "rivers.regions", "04  Landscape response and exclusions", false,
                "Region costs steer the river; protected areas keep it out of reserved world space.", "RegionCosts", "ProtectedAreas");
            WorldInspectorSections.DrawDetails(serializedObject, "rivers.materials", "05  Water and bank appearance", false, () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("WaterMaterial"));
                if (!serializedObject.FindProperty("TerrainBrush").boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("BedMaterial"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("BankMaterial"));
                    EditorGUILayout.HelpBox("Mesh mode draws visible bed and bank surfaces. Terrain Brush uses the terrain itself for those surfaces.", MessageType.None);
                    return;
                }
                var linked=serializedObject.FindProperty("World").objectReferenceValue as TerraLoom.Core.Unity.TerraLoomWorld;
                var layers=linked&&linked.Terrain&&linked.Terrain.terrainData?linked.Terrain.terrainData.terrainLayers:Array.Empty<TerrainLayer>();
                var labels=new string[layers.Length+1];labels[0]="Keep source textures";
                for(int i=0;i<layers.Length;i++)labels[i+1]=i+": "+(layers[i]?layers[i].name:"Missing terrain layer");
                var sediment=serializedObject.FindProperty("SedimentTerrainLayer");
                if(sediment.intValue>=layers.Length)EditorGUILayout.HelpBox("Selected sediment layer is missing. Generation will retain the previous river; select an existing layer.",MessageType.Warning);
                EditorGUI.BeginChangeCheck();
                int choice=EditorGUILayout.Popup("Sediment Terrain Layer",Mathf.Clamp(sediment.intValue+1,0,labels.Length-1),labels);
                if(EditorGUI.EndChangeCheck())sediment.intValue=choice-1;
                if(sediment.intValue>=0)EditorGUILayout.PropertyField(serializedObject.FindProperty("SedimentExposureDepth"));
                EditorGUILayout.HelpBox("Sediment uses the realized cut and smooth brush mask on the owned terrain copy. Other layer proportions are retained; Clear restores source textures. No new layer is added at runtime.",MessageType.Info);
            });
            WorldInspectorSections.Draw(serializedObject, "rivers.budgets", "06  Advanced / search and mesh budgets", false, "CellSize", "SampleSpacing", "MaximumNodes", "TotalSearchBudget", "TotalSampleBudget", "MaximumGeometryVertices");
            serializedObject.ApplyModifiedProperties();
            WorldInspectorSections.DrawDetails(serializedObject, "rivers.technical", "Technical details / terrain and clearance", false, () =>
            {
            if(!rivers.TerrainBrush)EditorGUILayout.HelpBox("Bed and Banks are visible channel surfaces with colliders. Depth lowers the bed below water. Water Inset lowers water below captured centre terrain (plus 2 cm); positive inset is experimental, requires terrain carving, extends water to sampled bank intersections and rejects actual terrain penetration before publication. Bank heights still follow local terrain. Clear restores the source terrain.", MessageType.Info);
            if(rivers.TerrainBrush)EditorGUILayout.HelpBox("Terrain Brush: the owned TerrainData forms bed and banks with a smooth outer falloff. Only water is rendered as a mesh; the TerrainCollider carries the channel. Width is flat bed width, Bank Width is the smooth transition on each side. Positive Water Inset and carving required. Clear restores the source. Existing terrain materials are preserved.",MessageType.Info);
            EditorGUILayout.LabelField("Terrain mode", rivers.CarveTerrainCopy ? "Excavation on an owned terrain copy; Clear restores the source" : "Mesh overlay; source terrain heights remain unchanged");
            if(rivers.WaterInset>0)
                EditorGUILayout.HelpBox("Inset water is clipped against the actual carved terrain at the planned water level. Dry pieces are removed; Bed/Banks and conservative wet reservations remain unchanged. Probe results are shown in Diagnostics. Clipping is sampled, not a continuous proof or a solution for natural bank materials. Combined bridge windows remain experimental.",MessageType.Warning);
            EditorGUILayout.HelpBox("Assign a Core world and Terrain (foreign Terrain supported). Generate carves a copy when enabled; Clear restores the original. Bake before saving generated content.", MessageType.Info);
            EditorGUILayout.HelpBox("Visible bank seams follow local captured terrain; crossing offers keep a separate conservative clearance envelope. Excavation follows the final channel triangles, never fills terrain, and is capped at twice Depth plus Water Inset plus 2 cm clearance. Brown live scene lines show the actual planned bank seams.", MessageType.Info);
            });
            EditorGUILayout.LabelField("07  Build and inspect", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Reload river")) Run(() =>
                {
                    var coordinator=WorldEditorExtensions.All.FirstOrDefault(e=>e.CoordinatesGeometry&&rivers.World&&e.Find(rivers.World).Length>0);
                    if(coordinator!=null)
                    {
                        foreach(var component in coordinator.Find(rivers.World))coordinator.Generate(component);
                        message="Shared Rivers/Paths geometry rebuilt in dependency order.";
                    }
                    else
                    {
                        if (!rivers.Generate()) throw new InvalidOperationException(rivers.Diagnostics);
                        message = "River regenerated. Bake to persist terrain and geometry.";
                    }
                    RiversSampleBuilder.MarkDirty(rivers);
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
            WorldInspectorSections.DrawDetails(serializedObject, "rivers.results", "08  Result details / route measurements", false, () =>
            {
            if (rivers.LastPlan != null)
            {
                EditorGUILayout.LabelField("Final planned water centrelines", EditorStyles.boldLabel);
                foreach (var route in rivers.LastPlan.Routes) RouteInspectionGUI.Draw(route.Request.Id, route.WaterPolyline);
                EditorGUILayout.HelpBox("Measurements read the final water plan, not a separate smoothed preview. Full bed/bank validation remains the planner's responsibility.", MessageType.None);
            }
            });
        }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception ex) { message = ex.Message; Debug.LogException(ex, target); }
            finally { Repaint(); }
        }
    }
}
