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
        public static void BuildBatch()
            => Build("TerraLoomDynamic", "Player");
        public static void BuildLandscapeBatch()
            => Build("TerraLoomLandscape", "PlayerLandscape");
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
