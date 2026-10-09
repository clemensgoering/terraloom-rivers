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
        {
            Directory.CreateDirectory(".artifacts/Player");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{IntegrationSampleBuilder.Root+"/TerraLoomDynamic.unity"},
                locationPathName=".artifacts/Player/TerraLoom.exe",target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.Development
            });
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Runtime player build: "+report.summary.result+", errors "+report.summary.totalErrors);
            Debug.Log("TerraLoom runtime player built, bytes "+report.summary.totalSize+", errors "+report.summary.totalErrors);
        }
    }
}
