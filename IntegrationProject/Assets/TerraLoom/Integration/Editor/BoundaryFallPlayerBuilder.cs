using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace TerraLoom.Integration.Editor
{
    public static class BoundaryFallPlayerBuilder
    {
        public static void BuildBatch()
        {
            const string scene="Assets/TerraLoom/Integration/Generated/TerraLoomBoundaryFallBridge.unity";
            const string output=".artifacts/BoundaryFallBridgePlayer/TerraLoom.exe";
            if(!File.Exists(scene))throw new InvalidOperationException("Bake the boundary fall scene before building its Player.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=new[]{scene},locationPathName=output,target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.Development
            });
            if(report.summary.result!=BuildResult.Succeeded)
                throw new InvalidOperationException("Boundary fall Player build: "+report.summary.result+
                    ", errors "+report.summary.totalErrors);
            UnityEngine.Debug.Log("TERRALOOM_BOUNDARY_BUILD_PASS bytes="+report.summary.totalSize);
        }
    }
}
