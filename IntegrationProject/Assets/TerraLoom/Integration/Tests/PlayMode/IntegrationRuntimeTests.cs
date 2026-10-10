using System.Collections;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using TerraLoom.Core.Unity;
using TerraLoom.Paths;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace TerraLoom.Tests
{
    public sealed class IntegrationRuntimeTests
    {
        [UnityTest] public IEnumerator RiverBridgeGroundAndRampsWorkTogetherWithoutChangingTheSourceTerrain()
            => RunCrossing(false);

        [UnityTest] public IEnumerator TerrainBrushBridgeAndRampsWorkWithoutBedOrBankMeshes()
            => RunCrossing(true);

        [UnityTest] public IEnumerator CurvedBrushCrossingReproducesOutsideCandidateBeforePublication()
        {
            // Frozen geometry from the protected standalone brush sample; no asset builder or
            // editor capture is required. Keep this as the baseline until an explicit joint
            // crossing contract has positive and negative geometry coverage.
            var original=new TerrainData{heightmapResolution=129,alphamapResolution=128,size=new Vector3(96,16,96)};
            var heights=new float[129,129];
            for(int z=0;z<129;z++)for(int x=0;x<129;x++)
            {
                float px=96f*x/128,pz=96f*z/128;
                float shoulder=Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Abs(px-48)-22)/20));
                heights[z,x]=(6-.03f*pz+shoulder*(5+3*Mathf.Pow(Mathf.Sin(pz*.055f+.4f),2)))/16;
            }
            original.SetHeights(0,0,heights);
            var sourceHeights=original.GetHeights(0,0,129,129);
            var layers=new[]{new TerrainLayer(),new TerrainLayer(),new TerrainLayer()};original.terrainLayers=layers;
            var alpha=new float[128,128,3];for(int z=0;z<128;z++)for(int x=0;x<128;x++)alpha[z,x,0]=1;original.SetAlphamaps(0,0,alpha);
            var terrain=Terrain.CreateTerrainGameObject(original);var host=new GameObject("Frozen curved crossing failure");
            var ground=new Material(Shader.Find("Universal Render Pipeline/Lit"));var water=new Material(Shader.Find("TerraLoom/RiverWater"));
            try
            {
                var world=host.AddComponent<TerraLoomWorld>();world.WorldId="curved-brush-repro";world.Seed=42;world.Terrain=terrain.GetComponent<Terrain>();
                world.Areas=new[]{new LandscapeArea{Id="river-landscape",Center=new Vector2(48,48),Size=new Vector2(96,96),Tags=new[]{"sample","landscape"}}};
                world.ManualAnchors=new[]{
                    new ManualWorldAnchor{Id="source",Position=new Vector3(48,0,4)},new ManualWorldAnchor{Id="mouth",Position=new Vector3(48,0,92)},
                    new ManualWorldAnchor{Id="west",Position=new Vector3(12,0,48)},new ManualWorldAnchor{Id="east",Position=new Vector3(84,0,48)}};
                var paths=host.AddComponent<TerraLoomPaths>();paths.World=world;paths.GroundMaterial=ground;paths.BridgeMaterial=ground;
                paths.AutomaticNetwork=false;paths.MaximumSlope=.35f;paths.BridgeClearance=1.2f;
                paths.Connections.Add(new PathConnectionSettings{Id="brush-crossing",StartId="west",EndId="east"});
                var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;rivers.WaterMaterial=water;
                rivers.AutomaticSourceAndMouth=false;rivers.TerrainBrush=true;rivers.WaterInset=.6f;rivers.SedimentTerrainLayer=1;
                rivers.Connections.Add(new RiverConnectionSettings{Id="sample-river",SourceId="source",MouthId="mouth"});
                var composition=host.AddComponent<TerraLoom.Integration.TerraLoomIntegration>();composition.World=world;composition.Paths=paths;composition.Rivers=rivers;
                Assert.That(composition.Generate(),Is.True,composition.Diagnostics);
                var priorRiver=rivers.GeneratedRoot;var priorPath=paths.GeneratedRoot;var priorTerrain=world.Terrain.terrainData;
                string priorRiverJson=rivers.PlanJson,priorPathJson=paths.PlanJson;
                var priorHeights=priorTerrain.GetHeights(0,0,129,129);
                world.ManualAnchors[0].Position=new Vector3(48,0,8);world.ManualAnchors[1].Position=new Vector3(48,0,88);
                world.ManualAnchors[2].Position=new Vector3(12,0,24);world.ManualAnchors[3].Position=new Vector3(84,0,24);
                rivers.ProtectedAreas.Add(new RiverProtectionSettings{Id="sample-detour",Center=new Vector2(48,48),Size=new Vector2(8,12)});
                Assert.That(composition.Generate(),Is.False,"Baseline conflict must not silently become a detour or partial success.");
                Assert.That(composition.ActiveStep,Is.EqualTo("03-plan-routes"));
                string failure=composition.Diagnostics;
                foreach(var field in new[]{"OutsideCandidate","CrossingContext revision=","input=sha256:","footprintXZ=","primaryWindowXZ=","conflictBedXZ=","conflictWindows="})
                    Assert.That(failure,Does.Contain(field),failure);
                Assert.That(rivers.GeneratedRoot,Is.SameAs(priorRiver));Assert.That(paths.GeneratedRoot,Is.SameAs(priorPath));
                Assert.That(world.Terrain.terrainData,Is.SameAs(priorTerrain));Assert.That(priorTerrain.GetHeights(0,0,129,129),Is.EqualTo(priorHeights));
                Assert.That(rivers.PlanJson,Is.EqualTo(priorRiverJson));Assert.That(paths.PlanJson,Is.EqualTo(priorPathJson));
                Assert.That(original.GetAlphamaps(0,0,128,128),Is.EqualTo(alpha));
                Assert.That(original.GetHeights(0,0,129,129),Is.EqualTo(sourceHeights));
                Assert.That(composition.Generate(),Is.False);Assert.That(composition.Diagnostics,Is.EqualTo(failure),"Same captured inputs reproduce the exact rejection context.");
                TestContext.WriteLine("TERRALOOM_CURVED_CROSSING_BASELINE "+failure);
                // Planning failed before earthworks: this proves preservation at that boundary,
                // not multi-module rollback after a materialization failure.
                composition.Clear();Assert.That(world.Terrain.terrainData,Is.SameAs(original));
            }
            finally{Object.Destroy(host);Object.Destroy(terrain);Object.Destroy(original);Object.Destroy(ground);Object.Destroy(water);foreach(var layer in layers)Object.Destroy(layer);}
            yield return null;
        }

        [UnityTest] public IEnumerator ShortTimberRejectsWideCrossingAndWalksSolidKitWithRebuildAndClear()
            =>RunCrossing(true,true);

        private IEnumerator RunCrossing(bool terrainBrush,bool timber=false)
        {
            var original=new TerrainData { heightmapResolution=129,size=new Vector3(96,16,96) };
            var heights=new float[129,129];for(int z=0;z<129;z++)for(int x=0;x<129;x++)heights[z,x]=(6-.03f*(96f*z/128))/16;
            original.SetHeights(0,0,heights);var terrain=Terrain.CreateTerrainGameObject(original);
            var host=new GameObject("Three-module runtime test");var ground=new Material(Shader.Find("Universal Render Pipeline/Lit"));var water=new Material(Shader.Find("TerraLoom/RiverWater"));
            try
            {
                var world=host.AddComponent<TerraLoomWorld>(); world.Terrain=terrain.GetComponent<Terrain>();
                world.ManualAnchors=new[] {
                    new ManualWorldAnchor { Id="source",Position=new Vector3(48,0,4) },new ManualWorldAnchor { Id="mouth",Position=new Vector3(48,0,92) },
                    new ManualWorldAnchor { Id="west",Position=new Vector3(12,0,48) },new ManualWorldAnchor { Id="east",Position=new Vector3(84,0,48) } };
                var paths=host.AddComponent<TerraLoomPaths>(); paths.World=world;paths.GroundMaterial=ground;paths.BridgeMaterial=ground; paths.AutomaticNetwork=false;
                paths.Connections.Add(new PathConnectionSettings { Id="road",StartId="west",EndId="east" });paths.MaximumSlope=.35f;paths.BridgeClearance=1.2f;
                var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;rivers.WaterMaterial=water;rivers.BedMaterial=ground;rivers.BankMaterial=ground;rivers.AutomaticSourceAndMouth=false;
                rivers.Connections.Add(new RiverConnectionSettings { Id="river",SourceId="source",MouthId="mouth" });
                rivers.TerrainBrush=terrainBrush;rivers.WaterInset=terrainBrush?.6f:0;
                if(terrainBrush){rivers.BedMaterial=null;rivers.BankMaterial=null;}
                var integration=host.AddComponent<TerraLoom.Integration.TerraLoomIntegration>();integration.World=world;integration.Paths=paths;integration.Rivers=rivers;
                if(timber)
                {
                    Assert.That(integration.Generate(),Is.True,integration.Diagnostics);
                    var oldPaths=paths.GeneratedRoot;var oldRivers=rivers.GeneratedRoot;var oldTerrain=world.Terrain.terrainData;
                    paths.TimberBeamBridge=true;
                    Assert.That(integration.ValidateCurrent(out _),Is.False,"Construction depth/span belongs to captured planning profile.");
                    Assert.That(integration.Generate(),Is.False,"Short beam kit cannot bridge the original wide channel.");
                    Assert.That(integration.ActiveStep,Is.EqualTo("03-plan-routes"));
                    Assert.That(integration.Diagnostics,Does.Contain("construction limit"));
                    Assert.That(paths.GeneratedRoot,Is.SameAs(oldPaths));Assert.That(rivers.GeneratedRoot,Is.SameAs(oldRivers));
                    Assert.That(world.Terrain.terrainData,Is.SameAs(oldTerrain));
                    integration.Clear();rivers.Width=2;rivers.BankWidth=.75f;
                }
                Assert.That(integration.Generate(),Is.True,integration.Diagnostics);
                Assert.That(integration.GenerationState,Is.EqualTo(TerraLoom.Core.GenerationRunState.Ready));
                Assert.That(integration.ValidateCurrent(out var fresh),Is.True,fresh);
                var rollbackRiver=rivers.GeneratedRoot;var rollbackPath=paths.GeneratedRoot;var rollbackTerrain=world.Terrain.terrainData;
                var rollbackCollider=world.Terrain.GetComponent<TerrainCollider>().terrainData;
                string rollbackRiverJson=rivers.PlanJson,rollbackPathJson=paths.PlanJson;
                GameObject abandonedRoot=null;TerrainData abandonedData=null;
                integration.PublicationBoundary=point=>
                {
                    if(point!="river-bound")return;
                    abandonedRoot=rivers.GeneratedRoot;abandonedData=world.Terrain.terrainData;
                    throw new System.InvalidOperationException("Runtime fault after first binding");
                };
                Assert.That(integration.Generate(),Is.False);
                Assert.That(rivers.GeneratedRoot,Is.SameAs(rollbackRiver));Assert.That(paths.GeneratedRoot,Is.SameAs(rollbackPath));
                Assert.That(world.Terrain.terrainData,Is.SameAs(rollbackTerrain));
                Assert.That(world.Terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(rollbackCollider));
                Assert.That(rivers.PlanJson,Is.EqualTo(rollbackRiverJson));Assert.That(paths.PlanJson,Is.EqualTo(rollbackPathJson));
                Assert.That(rollbackRiver.activeSelf&&rollbackPath.activeSelf,Is.True);
                Assert.That(abandonedRoot.activeSelf,Is.False,"Pending destruction cannot leave candidate colliders active.");
                yield return null;
                Assert.That(abandonedRoot==null&&abandonedData==null,Is.True,"Runtime candidate disposal completes at frame end.");
                integration.PublicationBoundary=null;
                Assert.That(integration.Generate(),Is.True,integration.Diagnostics);
                Assert.That(integration.ValidateCurrent(out fresh),Is.True,fresh);
                if(terrainBrush)
                {
                    rivers.SedimentExposureDepth*=2;
                    Assert.That(integration.ValidateCurrent(out _),Is.False,"Changed paint exposure invalidates composition even though geometric JSON is unchanged.");
                    rivers.SedimentExposureDepth/=2;
                    Assert.That(integration.ValidateCurrent(out fresh),Is.True,fresh);
                    rivers.SedimentTerrainLayer=0;
                    Assert.That(integration.ValidateCurrent(out _),Is.False,"Changed sediment selection invalidates composition.");
                    rivers.SedimentTerrainLayer=-1;
                    Assert.That(integration.ValidateCurrent(out fresh),Is.True,fresh);
                    Assert.That(rivers.GeneratedRoot.GetComponentsInChildren<MeshRenderer>().Length,Is.EqualTo(1),"Brush publishes water only, with terrain providing the banks and bed.");
                }
                // A new protected source must invalidate freshness and stop both plans before any
                // publication. The last good river, path and owned terrain stay available.
                var protectedRiver=rivers.GeneratedRoot;var protectedPath=paths.GeneratedRoot;var protectedTerrain=world.Terrain.terrainData;
                rivers.ProtectedAreas.Add(new RiverProtectionSettings { Id="source-foundation",Center=new Vector2(48,4),Size=new Vector2(8,8) });
                Assert.That(integration.ValidateCurrent(out _),Is.False,"River inspector protection belongs to the shared input.");
                Assert.That(integration.Generate(),Is.False,"A protected source cannot be excavated by composition.");
                Assert.That(rivers.GeneratedRoot,Is.SameAs(protectedRiver));Assert.That(paths.GeneratedRoot,Is.SameAs(protectedPath));
                Assert.That(world.Terrain.terrainData,Is.SameAs(protectedTerrain));
                rivers.ProtectedAreas.Clear();
                Assert.That(integration.Generate(),Is.True,integration.Diagnostics);
                Assert.That(integration.ValidateCurrent(out fresh),Is.True,fresh);
                Assert.That(integration.GenerationTrace.IndexOf("02-plan-water"),Is.LessThan(integration.GenerationTrace.IndexOf("03-plan-routes")));
                world.ManualAnchors[2].Position+=Vector3.forward;
                Assert.That(integration.ValidateCurrent(out _),Is.False,"Changed targets invalidate dependent results.");
                world.ManualAnchors[2].Position-=Vector3.forward;
                Assert.That(integration.ValidateCurrent(out fresh),Is.True,fresh);
                rivers.Width+=1;Assert.That(integration.ValidateCurrent(out _),Is.False,"Changed river profile invalidates route negotiation.");rivers.Width-=1;
                Assert.That(integration.ValidateCurrent(out fresh),Is.True,fresh);
                Assert.That(paths.LastPlan.Routes.Single().Surfaces.Contains(PathSurface.Bridge),Is.True,"A detour around the river does not prove negotiated bridge integration.");
                var sourceAfter=original.GetHeights(0,0,129,129);Assert.That(sourceAfter,Is.EqualTo(heights).Within(.00002f));
                Assert.That(world.Terrain.terrainData,Is.Not.SameAs(original));
                yield return new WaitForFixedUpdate();
                var route=paths.LastPlan.Routes.Single();
                for(int s=0;s<route.Surfaces.Count;s++)
                {
                    var a=route.Waypoints[s];var b=route.Waypoints[s+1];float length=Vector2.Distance(new Vector2((float)a.X,(float)a.Z),new Vector2((float)b.X,(float)b.Z));
                    int steps=Mathf.Max(2,Mathf.CeilToInt(length/.2f));
                    for(int i=0;i<=steps;i++)
                    {
                        float t=(float)i/steps,x=Mathf.Lerp((float)a.X,(float)b.X,t),z=Mathf.Lerp((float)a.Z,(float)b.Z,t);
                        Assert.That(Physics.Raycast(new Vector3(x,25,z),Vector3.down,out var hit,40),Is.True,"Missing collider at segment "+s+" t="+t);
                        Assert.That(hit.collider.GetComponent<PathGeneratedGeometry>(),Is.Not.Null,"Terrain or river collider replaces path at segment "+s+" t="+t);
                        if(route.Surfaces[s]==PathSurface.Bridge) Assert.That(hit.point.y,Is.GreaterThanOrEqualTo(Mathf.Min((float)a.Y,(float)b.Y)),"Deck collision below plan.");
                    }
                }
                var parts=paths.GeneratedRoot.GetComponentsInChildren<PathGeneratedGeometry>();
                Assert.That(parts.Any(p=>p.Role==PathGeometryRole.BridgeLanding),Is.True);
                foreach(var part in parts) Assert.That(part.GetComponent<MeshRenderer>().shadowCastingMode,
                    Is.EqualTo(part.Role==PathGeometryRole.BridgeDeck||part.Role==PathGeometryRole.BridgeStructure||part.Role==PathGeometryRole.BridgeRamp?ShadowCastingMode.On:ShadowCastingMode.Off));
                if(timber)
                {
                    var body=parts.Single(p=>p.Role==PathGeometryRole.BridgeStructure);
                    Assert.That(body.GetComponent<MeshCollider>().enabled,Is.True);
                    Assert.That(body.GetComponent<MeshCollider>().sharedMesh,Is.SameAs(body.GetComponent<MeshFilter>().sharedMesh));
                    foreach(var deck in parts.Where(p=>p.Role==PathGeometryRole.BridgeDeck))
                    {Assert.That(deck.GetComponent<MeshRenderer>().enabled,Is.False);Assert.That(deck.GetComponent<MeshCollider>().enabled,Is.False);}
                    Assert.That(parts.Count(p=>p.Role==PathGeometryRole.BridgeRamp),Is.EqualTo(2));
                    foreach(var landing in parts.Where(p=>p.Role==PathGeometryRole.BridgeLanding))
                    {Assert.That(landing.GetComponent<MeshRenderer>().enabled,Is.False);Assert.That(landing.GetComponent<MeshCollider>().enabled,Is.False);}
                }
                // Exercise Unity's actual walking solver as well as vertical collider coverage.
                var walker=new GameObject("Collider traversal probe");walker.transform.SetParent(host.transform);
                var controller=walker.AddComponent<CharacterController>();controller.height=1.8f;controller.radius=.3f;controller.stepOffset=.25f;controller.slopeLimit=40;
                var start=route.Waypoints[0];controller.enabled=false;walker.transform.position=new Vector3((float)start.X,(float)start.Y+1,(float)start.Z);controller.enabled=true;
                Physics.SyncTransforms();yield return new WaitForFixedUpdate();
                controller.Move(Vector3.down*.2f);
                Assert.That(controller.isGrounded,Is.True,"Walking probe did not settle on the first path surface.");
                for(int s=0;s<route.Surfaces.Count;s++)
                {
                    var a=route.Waypoints[s];var b=route.Waypoints[s+1];
                    int steps=Mathf.CeilToInt(Vector2.Distance(new Vector2((float)a.X,(float)a.Z),new Vector2((float)b.X,(float)b.Z))/.1f);
                    for(int i=1;i<=steps;i++)
                    {
                        float t=(float)i/steps;
                        var target=new Vector3(Mathf.Lerp((float)a.X,(float)b.X,t),walker.transform.position.y,Mathf.Lerp((float)a.Z,(float)b.Z,t));
                        controller.Move(target-walker.transform.position+Vector3.down*.06f);
                        Assert.That(Vector2.Distance(new Vector2(walker.transform.position.x,walker.transform.position.z),new Vector2(target.x,target.z)),Is.LessThan(.2f),"Walking solver is blocked at segment "+s);
                    }
                }
                Assert.That(walker.transform.position.y,Is.GreaterThan((float)route.Waypoints.Last().Y+.75f));
                var oldRiver=rivers.GeneratedRoot;var oldPath=paths.GeneratedRoot;
                Assert.That(integration.Generate(new CancellationToken(true)),Is.False);
                Assert.That(integration.GenerationState,Is.EqualTo(TerraLoom.Core.GenerationRunState.Cancelled));
                Assert.That(integration.ValidateCurrent(out _),Is.False);
                Assert.That(rivers.GeneratedRoot,Is.SameAs(oldRiver));Assert.That(paths.GeneratedRoot,Is.SameAs(oldPath));
                Assert.That(integration.Generate(),Is.True,integration.Diagnostics);
                Assert.That(integration.ValidateCurrent(out fresh),Is.True,fresh);
                var oldHeight=world.Terrain.terrainData.GetHeights(0,0,1,1);world.Terrain.terrainData.SetHeights(0,0,new float[,]{{oldHeight[0,0]+.001f}});
                Assert.That(integration.ValidateCurrent(out _),Is.False,"Changed derived heights invalidate downstream work.");
                world.Terrain.terrainData.SetHeights(0,0,oldHeight);
                var budgetPath=paths.GeneratedRoot;var budgetRiver=rivers.GeneratedRoot;var budgetTerrain=world.Terrain.terrainData;
                int previousBudget=paths.MaximumGeometryVertices;paths.MaximumGeometryVertices=128;
                Assert.That(integration.Generate(),Is.False,"Exhausted geometry budget must stop publication.");
                Assert.That(integration.GenerationState,Is.EqualTo(TerraLoom.Core.GenerationRunState.Failed));
                Assert.That(paths.GeneratedRoot,Is.SameAs(budgetPath),"Failed preparation preserves the complete previous composition.");
                Assert.That(rivers.GeneratedRoot,Is.SameAs(budgetRiver));Assert.That(world.Terrain.terrainData,Is.SameAs(budgetTerrain));
                Assert.That(integration.ValidateCurrent(out _),Is.False);paths.MaximumGeometryVertices=previousBudget;
                Assert.That(integration.Generate(),Is.True,integration.Diagnostics);
                integration.Clear();yield return null;
                Assert.That(world.Terrain.terrainData,Is.SameAs(original));Assert.That(paths.GeneratedRoot,Is.Null);Assert.That(rivers.GeneratedRoot,Is.Null);
            }
            finally { Object.Destroy(host);Object.Destroy(terrain);Object.Destroy(original);Object.Destroy(ground);Object.Destroy(water); }
            yield return null;
        }
    }
}
