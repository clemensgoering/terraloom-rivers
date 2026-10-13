using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Core.Unity;
using TerraLoom.Rivers.Unity;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Tests
{
    public sealed class RiverFallPublicationTests
    {
        [Test] public void BoundaryFallPublishesReceivingRouteAndPreservesPriorWorldOnFailure()
        {
            var host=new GameObject("Boundary published fall");Terrain terrain=null;TerrainData original=null;
            Material water=null,rock=null;
            try
            {
                var world=host.AddComponent<TerraLoomWorld>();world.WorldId="fall-boundary-publish";
                world.Resolution=129;world.TerrainSize=new Vector3(32,16,32);world.GenerateTerrain();
                terrain=world.Terrain;original=terrain.terrainData;
                var heights=new float[129,129];
                for(int z=0;z<129;z++)for(int x=0;x<129;x++)
                    heights[z,x]=(x<24?8f:2f)/16f;
                original.SetHeights(0,0,heights);
                world.UseSeedAnchors=false;
                world.ManualAnchors=new[]{
                    new ManualWorldAnchor{Id="source",Position=new Vector3(1,8,16)},
                    new ManualWorldAnchor{Id="fall",Position=new Vector3(6,8,16)},
                    new ManualWorldAnchor{Id="mouth",Position=new Vector3(32,2,16)}};
                water=new Material(Shader.Find("Universal Render Pipeline/Lit"));rock=new Material(water);
                var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;rivers.AutomaticSourceAndMouth=false;
                rivers.WaterMaterial=water;rivers.FallRockMaterial=rock;
                rivers.Connections.Add(new RiverConnectionSettings{Id="main",SourceId="source",MouthId="mouth",
                    FallId="main-fall",FallAnchorId="fall",RequirePlannedLowerReach=true,WorldBoundaryMouth=true});
                Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
                var plan=rivers.LastPlan;Assert.That(plan.Complete&&plan.HasContinuousSourceToMouth,Is.True);
                Assert.That(plan.Routes.Count,Is.EqualTo(1));
                Assert.That(plan.Routes[0].WaterPolyline.Last().X,Is.EqualTo(32).Within(.001));
                Assert.That(rivers.PlanJson,Does.Contain("\"format\": 4"));
                var contribution=plan.ToContribution(plan.CoreInputIdentity);
                Assert.That(contribution.Waters.Count,Is.GreaterThan(0));
                Assert.That(contribution.Crossings.All(c=>c.AllowedKinds==CrossingKind.Bridge),Is.True);
                Assert.That(contribution.Crossings.Count,Is.GreaterThan(0));
                var root=rivers.GeneratedRoot;var data=rivers.CarvedTerrainData;var json=rivers.PlanJson;
                Assert.That(root.GetComponentsInChildren<RiverGeneratedGeometry>(true).Length,Is.EqualTo(5));
                Assert.That(root.GetComponentsInChildren<MeshCollider>(true).Length,Is.EqualTo(1));
                Assert.That(terrain.terrainData,Is.SameAs(data));
                Assert.That(terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(data));
                var input=rivers.CaptureInput(out var bounds,out var requests);
                using(var staged=rivers.PrepareFromSnapshot(input,bounds,requests))
                {
                    terrain.terrainData=original;
                    Assert.Throws<InvalidOperationException>(()=>staged.CommitBindings());
                    terrain.terrainData=data;
                }
                Assert.That(rivers.GeneratedRoot,Is.SameAs(root));
                Assert.That(terrain.terrainData,Is.SameAs(data));
                Assert.That(rivers.LoadPlanJson(json.Replace("\"format\": 4","\"format\": 3")),Is.False);
                Assert.That(rivers.GeneratedRoot,Is.SameAs(root));
                using(var cancelled=new CancellationTokenSource())
                {
                    cancelled.Cancel();Assert.That(rivers.Generate(cancelled.Token),Is.False);
                }
                Assert.That(rivers.GeneratedRoot,Is.SameAs(root));
                Assert.That(rivers.PlanJson,Is.EqualTo(json));
                Assert.That(rivers.LoadPlanJson(json),Is.True,rivers.Diagnostics);
                Assert.That(rivers.LastPlan.HasContinuousSourceToMouth,Is.True);
                rivers.Clear();
                Assert.That(terrain.terrainData,Is.SameAs(original));
                Assert.That(terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(original));
            }
            finally
            {
                if(terrain)Object.DestroyImmediate(terrain.gameObject);
                if(host)Object.DestroyImmediate(host);
                if(original)Object.DestroyImmediate(original);
                if(water)Object.DestroyImmediate(water);
                if(rock)Object.DestroyImmediate(rock);
            }
        }
        [Test] public void OwnedFallPublishesTogetherAndAbortedReloadRetainsPreviousBindings()
        {
            var host=new GameObject("Owned fall publication");Terrain terrain=null;TerrainData original=null;
            Material water=null,rock=null;
            try
            {
                var world=host.AddComponent<TerraLoomWorld>();world.WorldId="fall-publish";
                world.Resolution=129;world.TerrainSize=new Vector3(32,16,32);world.GenerateTerrain();
                terrain=world.Terrain;original=terrain.terrainData;
                var heights=new float[129,129];
                for(int z=0;z<129;z++)for(int x=0;x<129;x++)
                    heights[z,x]=(x<64?8f:2f-.6f*Mathf.Clamp01((x-64)/56f))/16f;
                original.SetHeights(0,0,heights);
                world.UseSeedAnchors=false;
                world.ManualAnchors=new[]{
                    new ManualWorldAnchor{Id="source",Position=new Vector3(4,8,16)},
                    new ManualWorldAnchor{Id="fall",Position=new Vector3(16,8,16)},
                    new ManualWorldAnchor{Id="mouth",Position=new Vector3(28,2,16)}};
                water=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                rock=new Material(water);
                var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;rivers.AutomaticSourceAndMouth=false;
                rivers.WaterMaterial=water;rivers.FallRockMaterial=rock;
                rivers.Connections.Add(new RiverConnectionSettings{Id="main",SourceId="source",MouthId="mouth",FallId="main-fall",FallAnchorId="fall"});
                Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
                Assert.That(rivers.LastPlan.Complete,Is.True);
                Assert.That(rivers.LastPlan.HasContinuousSourceToMouth,Is.False);
                Assert.That(()=>rivers.LastPlan.RequireContinuousSourceToMouth(),Throws.InvalidOperationException);
                Assert.That(rivers.LastPlan.Routes.Count,Is.Zero);
                Assert.That(rivers.LastPlan.PublishedFalls.Count,Is.EqualTo(1));
                Assert.That(rivers.PlanJson,Does.Contain("\"format\": 3"));
                Assert.That(rivers.LastPlan.ToContribution(rivers.LastPlan.CoreInputIdentity).Reservations
                    .Any(r=>r.Strength==ReservationStrength.Hard&&r.Purpose==ReservationPurpose.ProtectedArea),Is.True);
                var firstRoot=rivers.GeneratedRoot;var firstData=rivers.CarvedTerrainData;
                Assert.That(firstRoot.GetComponentsInChildren<RiverGeneratedGeometry>(true).Length,Is.EqualTo(4));
                Assert.That(firstRoot.GetComponentsInChildren<MeshCollider>(true).Length,Is.EqualTo(1));
                Assert.That(terrain.terrainData,Is.SameAs(firstData));
                Assert.That(terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(firstData));
                var json=rivers.PlanJson;
                var input=rivers.CaptureInput(out var bounds,out var requests);
                using(var prepared=rivers.PrepareFromSnapshot(input,bounds,requests))
                {
                    Assert.That(prepared.Plan.Complete,Is.True);
                    Assert.That(prepared.FinalTerrainData,Is.Not.SameAs(firstData));
                    Assert.That(rivers.GeneratedRoot,Is.SameAs(firstRoot));
                    Assert.That(terrain.terrainData,Is.SameAs(firstData));
                }
                Assert.That(rivers.GeneratedRoot,Is.SameAs(firstRoot));
                Assert.That(terrain.terrainData,Is.SameAs(firstData));
                Assert.That(terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(firstData));
                Assert.That(rivers.PlanJson,Is.EqualTo(json));
                using(var stale=rivers.PrepareFromSnapshot(input,bounds,requests))
                {
                    terrain.terrainData=original;
                    Assert.Throws<InvalidOperationException>(()=>stale.CommitBindings());
                    terrain.terrainData=firstData;
                }
                Assert.That(rivers.GeneratedRoot,Is.SameAs(firstRoot));
                Assert.That(terrain.terrainData,Is.SameAs(firstData));
                Assert.That(rivers.LoadPlanJson(json.Replace("\"format\": 3","\"format\": 2")),Is.False);
                Assert.That(rivers.GeneratedRoot,Is.SameAs(firstRoot));
                Assert.That(terrain.terrainData,Is.SameAs(firstData));
                Assert.That(rivers.LoadPlanJson(json),Is.True,rivers.Diagnostics);
                firstRoot=rivers.GeneratedRoot;firstData=rivers.CarvedTerrainData;
                using(var cancelled=new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    Assert.That(rivers.Generate(cancelled.Token),Is.False);
                }
                Assert.That(rivers.GeneratedRoot,Is.SameAs(firstRoot));
                Assert.That(terrain.terrainData,Is.SameAs(firstData));
                Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
                Assert.That(rivers.GeneratedRoot,Is.Not.SameAs(firstRoot));
                Assert.That(rivers.PlanJson,Is.EqualTo(json));
                rivers.Clear();
                Assert.That(terrain.terrainData,Is.SameAs(original));
                Assert.That(terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(original));
                Assert.That(rivers.GeneratedRoot,Is.Null);
            }
            finally
            {
                if(terrain)Object.DestroyImmediate(terrain.gameObject);
                if(host)Object.DestroyImmediate(host);
                if(original)Object.DestroyImmediate(original);
                if(water)Object.DestroyImmediate(water);
                if(rock)Object.DestroyImmediate(rock);
            }
        }
    }
}
