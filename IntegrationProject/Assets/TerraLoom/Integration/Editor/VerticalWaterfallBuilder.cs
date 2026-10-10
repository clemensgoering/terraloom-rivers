using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TerraLoom.Integration.Editor
{
    public static class VerticalWaterfallBuilder
    {
        [MenuItem("Tools/TerraLoom/Integration/Build Vertical Waterfall Contact")]
        public static void BuildBatch()=>Build(false);
        [MenuItem("Tools/TerraLoom/Integration/Build Near Vertical Waterfall Contact")]
        public static void BuildNearBatch()=>Build(true);
        public static void BuildRotatedBatch()=>Build(false,true);
        [MenuItem("Tools/TerraLoom/Integration/Build Combined Waterfall Pool And Sill")]
        public static void BuildCombinedBatch()=>Build(false,false,true);
        private static void Build(bool near,bool rotated=false,bool combined=false)
        {
            WaterfallLandscapeBuilder.BuildBatch();
            var source=Object.FindFirstObjectByType<WaterfallLandscapePrototype>();
            var target=new GameObject("Vertical waterfall contact recipe").AddComponent<VerticalWaterfallPrototype>();
            target.NearVertical=near;target.RotateQuarterTurn=rotated;target.TerrainMaterial=source.TerrainMaterial;target.CliffMaterial=source.RockMaterial;
            target.CombinedPoolSill=combined;
            target.WaterMaterial=source.WaterMaterial;target.FallMaterial=source.FallMaterial;target.Layers=source.GroundLayers;
            Object.DestroyImmediate(source.gameObject);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),IntegrationSampleBuilder.Root+"/"+(combined?"TerraLoomCombinedWaterfall":rotated?"TerraLoomRotatedVertical":near?"TerraLoomNearVertical":"TerraLoomVertical")+".unity");
        }
    }
}
