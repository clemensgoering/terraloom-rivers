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
                bool bake=Environment.GetCommandLineArgs().Contains("-terraloomBakeBoundary");
                var world=host.AddComponent<TerraLoomWorld>();world.WorldId="boundary-composition";
                world.Resolution=129;world.TerrainSize=new Vector3(32,16,32);world.GenerateTerrain();
                terrain=world.Terrain;source=terrain.terrainData;
                var heights=new float[129,129];
                for(int z=0;z<129;z++)for(int x=0;x<129;x++)
                {
                    float lower=z<56||z>72?2.6f:2f;
                    if(!bake)heights[z,x]=(x<24?8f:lower)/16f;
                    else
                    {
                        float away=Mathf.Abs(z*.25f-16)/16;
                        float shoulder=away*away;
                        float start=6+2*shoulder,run=.25f+8*shoulder;
                        float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((x*.25f-start)/run));
                        heights[z,x]=Mathf.Lerp(8,lower,t)/16f;
                    }
                }
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

        [Test] public void DiagnoseUpperShoulderPixelContact()
        {
            if(!Environment.GetCommandLineArgs().Contains("-terraloomDiagnoseShoulder"))Assert.Ignore("Explicit saved-scene shoulder diagnosis only.");
            var setup=EditorSceneManager.GetSceneManagerSetup();GameObject cameraObject=null;RenderTexture target=null;
            try
            {
                EditorSceneManager.OpenScene(SavedScene);
                cameraObject=new GameObject("Temporary fall-wide contact camera");
                var camera=cameraObject.AddComponent<Camera>();
                camera.transform.position=new Vector3(17,7,3);
                camera.transform.LookAt(new Vector3(8,5,16));
                camera.fieldOfView=60;camera.aspect=960f/540f;
                target=new RenderTexture(960,540,24);camera.targetTexture=target;
                Physics.SyncTransforms();
                TestContext.WriteLine("colliders="+Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length);
                var solid=Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).First(c=>c.name=="Supported fall rock");
                var terrain=Object.FindFirstObjectByType<Terrain>();
                TestContext.WriteLine("solidBounds="+solid.bounds+" terrain="+terrain.terrainData.size);
                var holes=terrain.terrainData.GetHoles(0,0,terrain.terrainData.holesResolution,terrain.terrainData.holesResolution);
                int minX=int.MaxValue,minZ=int.MaxValue,maxX=-1,maxZ=-1;
                for(int z=0;z<holes.GetLength(0);z++)for(int x=0;x<holes.GetLength(1);x++)if(!holes[z,x])
                {minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minZ=Math.Min(minZ,z);maxZ=Math.Max(maxZ,z);}
                TestContext.WriteLine($"holeCells x={minX}..{maxX} z={minZ}..{maxZ}");
                foreach(int y in new[]{188,198,208,218,228})
                foreach(int x in new[]{500,510,520,530,540})
                {
                    var ray=camera.ScreenPointToRay(new Vector3(x,540-y,0));
                    var hits=Physics.RaycastAll(ray,100).OrderBy(h=>h.distance).ToArray();
                    if(x==510&&y==198)
                    {
                        var atSide=ray.GetPoint((21.25f-ray.origin.z)/ray.direction.z);
                        TestContext.WriteLine("pixel 510,198 at z21.25="+atSide+" sourceY="+
                            Object.FindFirstObjectByType<TerraLoomRivers>().OriginalTerrainData.GetInterpolatedHeight(
                                atSide.x/32,21.25f/32));
                        var down=new Ray(new Vector3(atSide.x,16,atSide.z),Vector3.down);
                        var contact=Physics.RaycastAll(down,32).OrderBy(h=>h.distance).ToArray();
                        var ground=contact.First(h=>h.collider is TerrainCollider).point.y;
                        var rock=contact.First(h=>h.collider.name=="Supported fall rock").point.y;
                        TestContext.WriteLine($"shoulder contact ground={ground:F3} rock={rock:F3}");
                        Assert.That(ground-rock,Is.LessThan(.12f));
                        Assert.That(hits.Any(h=>h.collider.name=="Supported fall rock"),Is.True,
                            "The fixed fall-wide shoulder pixel must hit closed rock, not the sky.");
                    }
                    TestContext.WriteLine($"pixel {x},{y}: "+(hits.Length==0?"SKY":
                        string.Join(" | ",hits.Take(3).Select(h=>$"{h.collider.name} {h.point} d={h.distance:F3}"))));
                }
            }
            finally
            {
                if(cameraObject)Object.DestroyImmediate(cameraObject);
                if(target)Object.DestroyImmediate(target);
                if(setup.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }

    }
}
