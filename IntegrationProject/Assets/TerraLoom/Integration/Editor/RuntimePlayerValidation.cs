using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TerraLoom.Integration.Editor
{
    /// <summary>Builds the configuration-only scene as an actual Windows player. The smoke run
    /// then exercises the same seed recipe without any editor assemblies being present.</summary>
    public static class RuntimePlayerValidation
    {
        public static void BuildFrozenTrussCrossingBatch()
        {FrozenCrossingBuilder.BuildBatch();Build("TerraLoomFrozenTruss","PlayerFrozenTruss");}
        public static void BuildBatch()
            => Build("TerraLoomDynamic", "Player");
        public static void BuildLandscapeBatch()
            => Build("TerraLoomLandscape", "PlayerLandscape");
        public static void BuildWaterfallLandscapeBatch()
        {
            WaterfallLandscapeBuilder.BuildBatch();
            Build("TerraLoomWaterfallPrototype","PlayerWaterfallPrototype");
        }
        public static void BuildWaterfallComparisonBatch()
        {
            WaterfallLandscapeBuilder.BuildComparisonBatch();
            Build("TerraLoomWaterfallComparison","PlayerWaterfallComparison");
        }
        public static void BuildVerticalWaterfallBatch()
        {
            VerticalWaterfallBuilder.BuildBatch();Build("TerraLoomVertical","PlayerVertical");
        }
        public static void BuildCombinedWaterfallBatch()
        {VerticalWaterfallBuilder.BuildCombinedBatch();Build("TerraLoomCombinedWaterfall","PlayerCombinedWaterfall");}
        public static void BuildNaturalHostWaterfallBatch()
        {VerticalWaterfallBuilder.BuildNaturalHostBatch();Build("TerraLoomNaturalHostWaterfall","PlayerNaturalHostWaterfall");}
        public static void BuildNearVerticalWaterfallBatch()
        {
            VerticalWaterfallBuilder.BuildNearBatch();Build("TerraLoomNearVertical","PlayerNearVertical");
        }
        public static void BuildRotatedVerticalWaterfallBatch()
        {VerticalWaterfallBuilder.BuildRotatedBatch();Build("TerraLoomRotatedVertical","PlayerRotatedVertical");}
        public static void BuildBrushCrossingBatch()
        {
            IntegrationSampleBuilder.BuildBrushBatch();
            new GameObject("Brush crossing evidence").AddComponent<BrushCrossingEvidence>();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(),IntegrationSampleBuilder.Root+"/TerraLoomBrushCrossing.unity");
            Build("TerraLoomBrushCrossing","PlayerBrushCrossing");
        }
        public static void BuildTerrainBrushEvidenceBatch()
        {
            TerraLoom.Rivers.Editor.RiversSampleBuilder.BuildInsetBatch();
            BuildRiverEvidenceBatch();
        }
        public static void BuildTimberCrossingBatch()
        {
            IntegrationSampleBuilder.BuildTimberBatch();
            new GameObject("Timber crossing evidence").AddComponent<BrushCrossingEvidence>();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(),IntegrationSampleBuilder.Root+"/TerraLoomTimberCrossing.unity");
            Build("TerraLoomTimberCrossing","PlayerTimberCrossing");
        }
        public static void BuildRiverEvidenceBatch()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(TerraLoom.Rivers.Editor.RiversSampleBuilder.SampleScene);
            new GameObject("Standalone river evidence").AddComponent<RiverSampleEvidence>();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,IntegrationSampleBuilder.Root+"/RiverEvidence.unity");
            Build("RiverEvidence","PlayerRiverEvidence");
        }
        private static void Build(string scene,string output)
        {
            Directory.CreateDirectory(".artifacts/"+output);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{IntegrationSampleBuilder.Root+"/"+scene+".unity"},
                locationPathName=".artifacts/"+output+"/TerraLoom.exe",target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.Development
            });
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Runtime player build: "+report.summary.result+", errors "+report.summary.totalErrors);
            Debug.Log("TerraLoom runtime player built, bytes "+report.summary.totalSize+", errors "+report.summary.totalErrors);
        }
    }
}
