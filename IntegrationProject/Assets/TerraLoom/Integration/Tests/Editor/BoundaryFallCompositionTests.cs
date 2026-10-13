using System;
using System.Linq;
using TerraLoom.Core;
using NUnit.Framework;
using TerraLoom.Core.Unity;
using TerraLoom.Integration;
using TerraLoom.Paths;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using TerraLoom.Rivers.Editor;
using TerraLoom.Paths.Editor;
using TerraLoom.Integration.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Tests
{
    public sealed class BoundaryFallCompositionTests
    {
        private const string SavedScene="Assets/TerraLoom/Integration/Generated/TerraLoomBoundaryFallBridge.unity";
        [Test] public void WorldGenerationUsesPreparedBoundaryFallBeforePathsAndRollsBackOnDeniedRoute()
        {
            var host=new GameObject("Boundary composition test");
            Terrain terrain=null;TerrainData source=null;Material ground=null,bridge=null,water=null,rock=null;
            try
            {
                var world=host.AddComponent<TerraLoomWorld>();world.WorldId="boundary-composition";
                world.Resolution=129;world.TerrainSize=new Vector3(32,16,32);world.GenerateTerrain();
                terrain=world.Terrain;source=terrain.terrainData;
                var heights=new float[129,129];
                for(int z=0;z<129;z++)for(int x=0;x<129;x++)
                    heights[z,x]=(x<24?8f:z<56||z>72?2.6f:2f)/16f;
                source.SetHeights(0,0,heights);
                world.UseSeedAnchors=false;
                world.ManualAnchors=new[]{
                    new ManualWorldAnchor{Id="source",Position=new Vector3(1,8,16)},
                    new ManualWorldAnchor{Id="fall",Position=new Vector3(6,8,16)},
                    new ManualWorldAnchor{Id="mouth",Position=new Vector3(32,2,16)},
                    new ManualWorldAnchor{Id="path-a",Position=new Vector3(2,8,4)},
                    new ManualWorldAnchor{Id="path-b",Position=new Vector3(4,8,8)},
                    new ManualWorldAnchor{Id="bridge-a",Position=new Vector3(30,2.6f,4)},
                    new ManualWorldAnchor{Id="bridge-b",Position=new Vector3(30,2.6f,28)}};
                var shader=Shader.Find("Universal Render Pipeline/Lit");
                ground=new Material(shader);water=new Material(shader);rock=new Material(shader);
                bridge=ground;
                bool bake=Environment.GetCommandLineArgs().Contains("-terraloomBakeBoundary");
                if(bake)
                {
                    const string parent="Assets/TerraLoom/Integration/Generated";
                    if(!AssetDatabase.IsValidFolder(parent+"/BoundaryFallBridge"))
                        AssetDatabase.CreateFolder(parent,"BoundaryFallBridge");
                    const string visual=parent+"/BoundaryFallBridge/VisualKit";
                    if(!AssetDatabase.IsValidFolder(visual))
                        AssetDatabase.CreateFolder(parent+"/BoundaryFallBridge","VisualKit");
                    Object.DestroyImmediate(ground);Object.DestroyImmediate(water);Object.DestroyImmediate(rock);
                    var kit=BoundaryFallVisualKit.Build(visual,source);
                    ground=kit.Ground;bridge=kit.Bridge;water=kit.Water;rock=kit.Rock;
                }
                var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;
                rivers.WaterMaterial=water;rivers.FallRockMaterial=rock;rivers.BedMaterial=rock;rivers.BankMaterial=rock;
                rivers.AutomaticSourceAndMouth=false;
                rivers.Connections.Add(new RiverConnectionSettings{Id="main",SourceId="source",MouthId="mouth",
                    FallId="main-fall",FallAnchorId="fall",RequirePlannedLowerReach=true,WorldBoundaryMouth=true});
                var paths=host.AddComponent<TerraLoomPaths>();paths.World=world;
                paths.GroundMaterial=ground;paths.BridgeMaterial=bridge;paths.AutomaticNetwork=false;
                paths.TimberTrussBridge=true;
                paths.Connections.Add(new PathConnectionSettings{Id="upland",StartId="path-a",EndId="path-b"});
                var composition=host.AddComponent<TerraLoomIntegration>();
                composition.World=world;composition.Paths=paths;composition.Rivers=rivers;
                bool sawPrepared=false;
                composition.PublicationBoundary=step=>
                {
                    if(step=="river-prepared")
                    {
                        if(!sawPrepared)
                        {
                            Assert.That(composition.PublishedSharedInput,Is.Null);
                            Assert.That(terrain.terrainData,Is.SameAs(source));
                        }
                        sawPrepared=true;
                    }
                };
                Assert.That(composition.Generate(),Is.True,composition.Diagnostics);
                Assert.That(sawPrepared,Is.True);
                Assert.That(rivers.LastPlan.HasContinuousSourceToMouth,Is.True);
                Assert.That(paths.LastPlan.Complete,Is.True);
                Assert.That(composition.PublishedSharedInput.Waters.Count,Is.GreaterThan(0));
                Assert.That(terrain.terrainData,Is.SameAs(rivers.CarvedTerrainData));
                paths.Connections[0].StartId="bridge-a";paths.Connections[0].EndId="bridge-b";
                composition.CrossingPermissions=input=>
                {
                    var candidates=input.Crossings.Where(c=>c.Bounds.MinX<32&&c.Bounds.MaxX>20).ToArray();
                    var sections=candidates.Select(c=>new CrossingSectionPermission(c.WaterId,c.Id,c.Bounds)).ToArray();
                    return new PathCrossingPermissions(candidates.SelectMany(c=>new[]{0,1}.Select(axis=>
                        new CollectiveCrossingAuthorization("bridge:"+c.Id+":"+axis,input.Identity,sections))));
                };
                Assert.That(composition.Generate(),Is.True,composition.Diagnostics);
                var parts=paths.GeneratedRoot.GetComponentsInChildren<PathGeneratedGeometry>(true);
                Assert.That(parts.Any(part=>part.Role==PathGeometryRole.BridgeDeck),Is.True);
                var body=parts.Single(part=>part.Role==PathGeometryRole.BridgeStructure);
                Assert.That(body.GetComponent<MeshCollider>().sharedMesh,
                    Is.SameAs(body.GetComponent<MeshFilter>().sharedMesh));
                Assert.That(parts.Count(part=>part.Role==PathGeometryRole.BridgeRamp),Is.EqualTo(2));
                Assert.That(composition.ValidateCurrent(out var freshness),Is.True,freshness);
                if(bake)
                {
                    const string root="Assets/TerraLoom/Integration/Generated/BoundaryFallBridge";
                    BoundaryFallVisualKit.PlaceProps(host.transform,terrain,root+"/VisualKit");
                    host.AddComponent<BoundaryFallPlayerProbe>();
                    var beforeBake=rivers.CaptureInput(out _,out _);
                    RiversSampleBuilder.Bake(rivers,root+"/Rivers");
                    PathsSampleBuilder.Bake(paths,root+"/Paths");
                    var afterBake=rivers.CaptureInput(out _,out _);
                    TestContext.WriteLine("BAKE_INPUT before="+beforeBake.Identity.InputFingerprint+
                        " after="+afterBake.Identity.InputFingerprint+
                        " terrain="+world.CaptureTerrainFingerprint(rivers.OriginalTerrainData)+
                        " seed="+world.Seed+" revision="+world.Revision+" anchors="+world.ManualAnchors.Length);
                    AssetDatabase.SaveAssets();
                    Assert.That(EditorSceneManager.SaveScene(host.scene,SavedScene),Is.True);
                    return;
                }
                var previousData=terrain.terrainData;var previousRiver=rivers.GeneratedRoot;
                var previousPath=paths.GeneratedRoot;var riverJson=rivers.PlanJson;var pathJson=paths.PlanJson;
                // A named grant for a candidate that does not exist cannot rescue
                // a route through the protected fall and must not publish a partial world.
                paths.Connections[0].StartId="fall";paths.Connections[0].EndId="mouth";
                composition.CrossingPermissions=input=>
                {
                    var candidate=input.Crossings[0];
                    return new PathCrossingPermissions(new[]{new CollectiveCrossingAuthorization("invalid-kit",input.Identity,
                        new[]{new CrossingSectionPermission(candidate.WaterId,"missing-candidate",candidate.Bounds)})});
                };
                Assert.That(composition.Generate(),Is.False);
                Assert.That(composition.ActiveStep,Is.EqualTo("03-plan-routes"),composition.Diagnostics);
                Assert.That(terrain.terrainData,Is.SameAs(previousData));
                Assert.That(rivers.GeneratedRoot,Is.SameAs(previousRiver));
                Assert.That(paths.GeneratedRoot,Is.SameAs(previousPath));
                Assert.That(rivers.PlanJson,Is.EqualTo(riverJson));
                Assert.That(paths.PlanJson,Is.EqualTo(pathJson));
                composition.Clear();
            }
            finally
            {
                if(terrain)Object.DestroyImmediate(terrain.gameObject);
                if(host)Object.DestroyImmediate(host);
                if(source)Object.DestroyImmediate(source);
                if(ground&&!AssetDatabase.Contains(ground))Object.DestroyImmediate(ground);
                if(bridge&&bridge!=ground&&!AssetDatabase.Contains(bridge))Object.DestroyImmediate(bridge);
                if(water&&!AssetDatabase.Contains(water))Object.DestroyImmediate(water);
                if(rock&&!AssetDatabase.Contains(rock))Object.DestroyImmediate(rock);
            }
        }

        [Test] public void ReopenedBoundaryFallBridgeRetainsOwnedAssetsAndConstruction()
        {
            if(!Environment.GetCommandLineArgs().Contains("-terraloomReopenBoundary"))Assert.Ignore("Separate-process bake/reopen probe only.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(SavedScene);
                var composition=Object.FindFirstObjectByType<TerraLoomIntegration>();
                Assert.That(composition,Is.Not.Null);
                var rivers=composition.Rivers;var paths=composition.Paths;
                Assert.That(AssetDatabase.Contains(rivers.OriginalTerrainData),Is.True);
                Assert.That(AssetDatabase.Contains(rivers.CarvedTerrainData),Is.True);
                Assert.That(composition.World.Terrain.terrainData,Is.SameAs(rivers.CarvedTerrainData));
                Assert.That(composition.World.Terrain.GetComponent<TerrainCollider>().terrainData,
                    Is.SameAs(rivers.CarvedTerrainData));
                Assert.That(rivers.PlanJson,Does.Contain("\"format\": 4"));
                Assert.That(paths.PlanJson,Is.Not.Empty);
                Assert.That(composition.ValidateCurrent(out var freshness),Is.True,freshness);
                Assert.That(rivers.GeneratedRoot.GetComponentsInChildren<RiverGeneratedGeometry>(true).Length,Is.EqualTo(5));
                var parts=paths.GeneratedRoot.GetComponentsInChildren<PathGeneratedGeometry>(true);
                var body=parts.Single(part=>part.Role==PathGeometryRole.BridgeStructure);
                Assert.That(AssetDatabase.Contains(body.GetComponent<MeshFilter>().sharedMesh),Is.True);
                Assert.That(body.GetComponent<MeshCollider>().sharedMesh,
                    Is.SameAs(body.GetComponent<MeshFilter>().sharedMesh));
                Assert.That(parts.Count(part=>part.Role==PathGeometryRole.BridgeRamp),Is.EqualTo(2));
                var art=GameObject.Find("Seasonal trees and stones (fixture)");
                Assert.That(art,Is.Not.Null);
                Assert.That(art.GetComponentsInChildren<MeshFilter>(true).Length,Is.EqualTo(16));
                Assert.That(art.GetComponentsInChildren<MeshFilter>(true)
                    .All(f=>AssetDatabase.Contains(f.sharedMesh)),Is.True);
            }
            finally
            {
                if(setup.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }

    }
}
