using System.Linq;
using NUnit.Framework;
using TerraLoom.Core.Unity;
using TerraLoom.Rivers.Editor;
using TerraLoom.Rivers.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

namespace TerraLoom.Tests
{
    /// <summary>Run Build and Reopen as separate filtered Unity invocations.</summary>
    public sealed class RiverFallPersistenceTests
    {
        private const string Folder="Assets/TerraLoom/Tests/Generated/FallPersistence";
        private const string Scene=Folder+"/OwnedFall.unity";
        private const string BoundaryFolder="Assets/TerraLoom/Tests/Generated/BoundaryFallPersistence";
        private const string BoundaryScene=BoundaryFolder+"/BoundaryFall.unity";

        [Test] public void BuildBakedBoundaryFallFixture()
        {
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                if(!AssetDatabase.IsValidFolder("Assets/TerraLoom/Tests/Generated"))
                    AssetDatabase.CreateFolder("Assets/TerraLoom/Tests","Generated");
                if(AssetDatabase.IsValidFolder(BoundaryFolder))AssetDatabase.DeleteAsset(BoundaryFolder);
                AssetDatabase.CreateFolder("Assets/TerraLoom/Tests/Generated","BoundaryFallPersistence");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var host=new GameObject("Owned boundary fall persistence");
                var world=host.AddComponent<TerraLoomWorld>();world.WorldId="fall-boundary-reopen";
                world.Resolution=129;world.TerrainSize=new Vector3(32,16,32);world.GenerateTerrain();
                var original=world.Terrain.terrainData;
                var heights=new float[129,129];
                for(int z=0;z<129;z++)for(int x=0;x<129;x++)heights[z,x]=(x<24?8f:2f)/16f;
                original.SetHeights(0,0,heights);
                world.UseSeedAnchors=false;
                world.ManualAnchors=new[]{
                    new ManualWorldAnchor{Id="source",Position=new Vector3(1,8,16)},
                    new ManualWorldAnchor{Id="fall",Position=new Vector3(6,8,16)},
                    new ManualWorldAnchor{Id="mouth",Position=new Vector3(32,2,16)}};
                var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;rivers.AutomaticSourceAndMouth=false;
                rivers.WaterMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                rivers.FallRockMaterial=new Material(rivers.WaterMaterial);
                rivers.WaterMaterial.SetColor("_BaseColor",new Color(.12f,.55f,.72f,.7f));
                rivers.FallRockMaterial.SetColor("_BaseColor",new Color(.42f,.38f,.33f));
                rivers.Connections.Add(new RiverConnectionSettings{Id="main",SourceId="source",MouthId="mouth",
                    FallId="main-fall",FallAnchorId="fall",RequirePlannedLowerReach=true,WorldBoundaryMouth=true});
                var lightObject=new GameObject("Fall inspection sun");
                var sun=lightObject.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;
                lightObject.transform.rotation=Quaternion.Euler(35,-35,0);
                var probeScript=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/TerraLoom/Tests/Player/RiverFallPlayerProbe.cs");
                Assert.That(probeScript,Is.Not.Null);host.AddComponent(probeScript.GetClass());
                Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
                Assert.That(rivers.LastPlan.HasContinuousSourceToMouth,Is.True);
                RiversSampleBuilder.Bake(rivers,BoundaryFolder+"/Baked");
                Assert.That(EditorSceneManager.SaveScene(host.scene,BoundaryScene),Is.True);
            }
            finally
            {
                if(previous.Any(s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }

        [Test] public void ReopenBakedBoundaryFallAndValidateJson()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(BoundaryScene),Is.Not.Null,
                "Run BuildBakedBoundaryFallFixture in an earlier Unity process.");
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(BoundaryScene);
                var rivers=Object.FindFirstObjectByType<TerraLoomRivers>();Assert.That(rivers,Is.Not.Null);
                var terrain=rivers.World.Terrain;
                Assert.That(terrain.terrainData,Is.SameAs(rivers.CarvedTerrainData));
                Assert.That(terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(rivers.CarvedTerrainData));
                Assert.That(AssetDatabase.Contains(rivers.OriginalTerrainData),Is.True);
                Assert.That(rivers.GeneratedRoot.GetComponentsInChildren<RiverGeneratedGeometry>(true).Length,Is.EqualTo(5));
                Assert.That(rivers.GeneratedRoot.GetComponentsInChildren<MeshCollider>(true).Length,Is.EqualTo(1));
                var holes=rivers.CarvedTerrainData.GetHoles(0,0,rivers.CarvedTerrainData.holesResolution,
                    rivers.CarvedTerrainData.holesResolution);
                Assert.That(holes.Cast<bool>().Any(x=>!x),Is.True,"Fall opening must survive bake/reopen.");
                var json=rivers.PlanJson;Assert.That(json,Does.Contain("\"format\": 4"));
                Assert.That(rivers.LoadPlanJson(json),Is.True,rivers.Diagnostics);
                Assert.That(rivers.LastPlan.HasContinuousSourceToMouth,Is.True);
                Assert.That(rivers.LastPlan.ToContribution(rivers.LastPlan.CoreInputIdentity).Waters.Count,Is.GreaterThan(0));
                Assert.That(rivers.PlanJson,Is.EqualTo(json));
            }
            finally
            {
                if(previous.Any(s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }

        [Test] public void BuildBakedFallFixture()
        {
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                if(!AssetDatabase.IsValidFolder("Assets/TerraLoom/Tests/Generated"))
                    AssetDatabase.CreateFolder("Assets/TerraLoom/Tests","Generated");
                if(AssetDatabase.IsValidFolder(Folder))AssetDatabase.DeleteAsset(Folder);
                AssetDatabase.CreateFolder("Assets/TerraLoom/Tests/Generated","FallPersistence");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var host=new GameObject("Owned fall persistence");
                var world=host.AddComponent<TerraLoomWorld>();world.WorldId="fall-reopen";
                world.Resolution=129;world.TerrainSize=new Vector3(32,16,32);world.GenerateTerrain();
                var original=world.Terrain.terrainData;
                var heights=new float[129,129];
                for(int z=0;z<129;z++)for(int x=0;x<129;x++)
                    heights[z,x]=(x<64?8f:2f-.6f*Mathf.Clamp01((x-64)/56f))/16f;
                original.SetHeights(0,0,heights);
                world.UseSeedAnchors=false;
                world.ManualAnchors=new[]{
                    new ManualWorldAnchor{Id="source",Position=new Vector3(4,8,16)},
                    new ManualWorldAnchor{Id="fall",Position=new Vector3(16,8,16)},
                    new ManualWorldAnchor{Id="mouth",Position=new Vector3(28,2,16)}};
                var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;rivers.AutomaticSourceAndMouth=false;
                rivers.WaterMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                rivers.FallRockMaterial=new Material(rivers.WaterMaterial);
                rivers.WaterMaterial.SetColor("_BaseColor",new Color(.12f,.55f,.72f,.7f));
                rivers.FallRockMaterial.SetColor("_BaseColor",new Color(.42f,.38f,.33f));
                var lightObject=new GameObject("Fall inspection sun");
                var sun=lightObject.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;
                lightObject.transform.rotation=Quaternion.Euler(35,-35,0);
                var probeScript=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/TerraLoom/Tests/Player/RiverFallPlayerProbe.cs");
                Assert.That(probeScript,Is.Not.Null);
                host.AddComponent(probeScript.GetClass());
                rivers.Connections.Add(new RiverConnectionSettings{Id="main",SourceId="source",MouthId="mouth",FallId="main-fall",FallAnchorId="fall"});
                Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
                RiversSampleBuilder.Bake(rivers,Folder+"/Baked");
                Assert.That(EditorSceneManager.SaveScene(host.scene,Scene),Is.True);
                Assert.That(AssetDatabase.Contains(rivers.CarvedTerrainData),Is.True);
                Assert.That(AssetDatabase.Contains(rivers.OriginalTerrainData),Is.True);
            }
            finally
            {
                if(previous.Any(s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }

        [Test] public void ReopenBakedFallAndValidateJson()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene),Is.Not.Null,"Run BuildBakedFallFixture in an earlier Unity process.");
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(Scene);
                var rivers=Object.FindFirstObjectByType<TerraLoomRivers>();
                Assert.That(rivers,Is.Not.Null);
                var terrain=rivers.World.Terrain;
                Assert.That(terrain.terrainData,Is.SameAs(rivers.CarvedTerrainData));
                Assert.That(terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(rivers.CarvedTerrainData));
                Assert.That(AssetDatabase.Contains(rivers.OriginalTerrainData),Is.True);
                Assert.That(rivers.GeneratedRoot.GetComponentsInChildren<RiverGeneratedGeometry>(true).Length,Is.EqualTo(4));
                Assert.That(rivers.GeneratedRoot.GetComponentsInChildren<MeshCollider>(true).Length,Is.EqualTo(1));
                var holes=rivers.CarvedTerrainData.GetHoles(0,0,rivers.CarvedTerrainData.holesResolution,rivers.CarvedTerrainData.holesResolution);
                Assert.That(holes.Cast<bool>().Any(x=>!x),Is.True,"Owned fall opening must survive bake/reopen.");
                var json=rivers.PlanJson;
                Assert.That(json,Does.Contain("\"format\": 3"));
                Assert.That(rivers.LoadPlanJson(json),Is.True,rivers.Diagnostics);
                Assert.That(rivers.LastPlan.Complete,Is.True);
                Assert.That(rivers.PlanJson,Is.EqualTo(json));
                Assert.That(terrain.terrainData,Is.SameAs(rivers.CarvedTerrainData));
            }
            finally
            {
                if(previous.Any(s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }

        [Test] public void BuildWindowsFallPlayer()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene),Is.Not.Null);
            const string folder=".artifacts/FallGate4-20261013/Player";
            Directory.CreateDirectory(folder);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{Scene},locationPathName=folder+"/TerraLoom.exe",
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            Assert.That(report.summary.result,Is.EqualTo(BuildResult.Succeeded),"Windows fall Player build");
        }

        [Test] public void BuildWindowsBoundaryFallPlayer()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(BoundaryScene),Is.Not.Null);
            const string folder=".artifacts/FallGate6c-20261013/Player";
            Directory.CreateDirectory(folder);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{BoundaryScene},locationPathName=folder+"/TerraLoom.exe",
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            Assert.That(report.summary.result,Is.EqualTo(BuildResult.Succeeded),"Windows boundary fall Player build");
        }
    }
}
