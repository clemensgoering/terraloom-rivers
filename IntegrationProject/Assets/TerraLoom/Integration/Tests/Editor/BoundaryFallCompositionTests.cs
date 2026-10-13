using System;
using TerraLoom.Core;
using NUnit.Framework;
using TerraLoom.Core.Unity;
using TerraLoom.Integration;
using TerraLoom.Paths;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Tests
{
    public sealed class BoundaryFallCompositionTests
    {
        [Test] public void WorldGenerationUsesPreparedBoundaryFallBeforePathsAndRollsBackOnDeniedRoute()
        {
            var host=new GameObject("Boundary composition test");
            Terrain terrain=null;TerrainData source=null;Material ground=null,water=null,rock=null;
            try
            {
                var world=host.AddComponent<TerraLoomWorld>();world.WorldId="boundary-composition";
                world.Resolution=129;world.TerrainSize=new Vector3(32,16,32);world.GenerateTerrain();
                terrain=world.Terrain;source=terrain.terrainData;
                var heights=new float[129,129];
                for(int z=0;z<129;z++)for(int x=0;x<129;x++)heights[z,x]=(x<24?8f:2f)/16f;
                source.SetHeights(0,0,heights);
                world.UseSeedAnchors=false;
                world.ManualAnchors=new[]{
                    new ManualWorldAnchor{Id="source",Position=new Vector3(1,8,16)},
                    new ManualWorldAnchor{Id="fall",Position=new Vector3(6,8,16)},
                    new ManualWorldAnchor{Id="mouth",Position=new Vector3(32,2,16)},
                    new ManualWorldAnchor{Id="path-a",Position=new Vector3(2,8,4)},
                    new ManualWorldAnchor{Id="path-b",Position=new Vector3(4,8,8)}};
                var shader=Shader.Find("Universal Render Pipeline/Lit");
                ground=new Material(shader);water=new Material(shader);rock=new Material(shader);
                var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;
                rivers.WaterMaterial=water;rivers.FallRockMaterial=rock;rivers.BedMaterial=rock;rivers.BankMaterial=rock;
                rivers.AutomaticSourceAndMouth=false;
                rivers.Connections.Add(new RiverConnectionSettings{Id="main",SourceId="source",MouthId="mouth",
                    FallId="main-fall",FallAnchorId="fall",RequirePlannedLowerReach=true,WorldBoundaryMouth=true});
                var paths=host.AddComponent<TerraLoomPaths>();paths.World=world;
                paths.GroundMaterial=ground;paths.BridgeMaterial=ground;paths.AutomaticNetwork=false;
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
                if(ground)Object.DestroyImmediate(ground);
                if(water)Object.DestroyImmediate(water);
                if(rock)Object.DestroyImmediate(rock);
            }
        }
    }
}
