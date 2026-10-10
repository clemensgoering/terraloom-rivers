using System.Collections;
using System.Threading;
using NUnit.Framework;
using TerraLoom.Core.Unity;
using TerraLoom.Integration;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace TerraLoom.Tests
{
    public sealed class SourceLifecyclePlayTests
    {
        [UnityTest] public IEnumerator SeedRollbackAndRetirementReleaseBothOwnedTerrainCopiesAtFrameEnd()
        {
            var borrowed=new TerrainData{heightmapResolution=129,size=new Vector3(96,16,96)};
            var raw=new float[129,129];for(int z=0;z<129;z++)for(int x=0;x<129;x++)raw[z,x]=(6-.03f*(96f*z/128))/16;
            borrowed.SetHeights(0,0,raw);
            var borrowedHeights=borrowed.GetHeights(0,0,129,129);
            var colliderSource=Object.Instantiate(borrowed);
            var terrain=Terrain.CreateTerrainGameObject(borrowed);terrain.GetComponent<TerrainCollider>().terrainData=colliderSource;
            var host=new GameObject("Source lifecycle runtime test");
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            WorldSourcePublication publisher=null;
            try
            {
                var world=host.AddComponent<TerraLoomWorld>();world.Terrain=terrain.GetComponent<Terrain>();
                var anchors=new[]{
                    new ManualWorldAnchor{Id="source",Position=new Vector3(48,0,4)},new ManualWorldAnchor{Id="mouth",Position=new Vector3(48,0,92)},
                    new ManualWorldAnchor{Id="west",Position=new Vector3(12,0,48)},new ManualWorldAnchor{Id="east",Position=new Vector3(84,0,48)}};
                world.ManualAnchors=anchors;int originalSeed=world.Seed;
                var paths=host.AddComponent<TerraLoomPaths>();paths.World=world;paths.GroundMaterial=material;paths.BridgeMaterial=material;
                paths.AutomaticNetwork=false;paths.MaximumSlope=.35f;paths.BridgeClearance=1.2f;
                paths.Connections.Add(new PathConnectionSettings{Id="road",StartId="west",EndId="east"});
                var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;rivers.WaterMaterial=material;rivers.BedMaterial=material;rivers.BankMaterial=material;
                rivers.AutomaticSourceAndMouth=false;rivers.Connections.Add(new RiverConnectionSettings{Id="river",SourceId="source",MouthId="mouth"});
                var c=host.AddComponent<TerraLoomIntegration>();c.World=world;c.Paths=paths;c.Rivers=rivers;
                publisher=new WorldSourcePublication(world);
                Assert.That(c.GenerateWithSource(()=>publisher.Prepare(2043,anchors,data=>data.SetHeights(0,0,raw))),Is.True,c.Diagnostics);
                var oldSource=publisher.CurrentSource;var oldCarve=world.Terrain.terrainData;
                var oldPaths=paths.GeneratedRoot;var oldRivers=rivers.GeneratedRoot;
                TerrainData abandonedSource=null,abandonedCarve=null;
                using(var cancellation=new CancellationTokenSource())
                {
                    c.PublicationBoundary=point=>
                    {
                        if(point!="source-bound")return;
                        abandonedSource=publisher.CurrentSource;abandonedCarve=world.Terrain.terrainData;cancellation.Cancel();
                    };
                    Assert.That(c.GenerateWithSource(()=>publisher.Prepare(2044,anchors,data=>data.SetHeights(0,0,raw)),cancellation.Token),Is.False);
                }
                Assert.That(world.Seed,Is.EqualTo(2043));Assert.That(publisher.CurrentSource,Is.SameAs(oldSource));
                Assert.That(world.Terrain.terrainData,Is.SameAs(oldCarve));
                Assert.That(paths.GeneratedRoot,Is.SameAs(oldPaths));Assert.That(rivers.GeneratedRoot,Is.SameAs(oldRivers));
                yield return null;
                Assert.That(abandonedSource==null&&abandonedCarve==null,Is.True,"Both abandoned copies finish destruction at frame end.");
                c.PublicationBoundary=null;
                Assert.That(c.GenerateWithSource(()=>publisher.Prepare(2044,anchors,data=>data.SetHeights(0,0,raw))),Is.True,c.Diagnostics);
                Assert.That(c.ValidateCurrent(out var reason),Is.True,reason);
                yield return null;
                Assert.That(oldSource==null&&oldCarve==null&&oldPaths==null&&oldRivers==null,Is.True);
                var last=publisher.CurrentSource;c.Clear();publisher.Clear();
                Assert.That(world.Terrain.terrainData,Is.SameAs(borrowed));
                Assert.That(terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(colliderSource));
                Assert.That(world.Seed,Is.EqualTo(originalSeed));Assert.That(world.ManualAnchors,Is.SameAs(anchors));
                yield return null;
                Assert.That(last==null,Is.True);Assert.That(borrowed!=null&&colliderSource!=null,Is.True);
                Assert.That(borrowed.GetHeights(0,0,129,129),Is.EqualTo(borrowedHeights));
            }
            finally
            {
                var c=host?host.GetComponent<TerraLoomIntegration>():null;if(c)c.Clear();publisher?.Dispose();
                Object.Destroy(host);Object.Destroy(terrain);Object.Destroy(material);Object.Destroy(borrowed);Object.Destroy(colliderSource);
            }
            yield return null;
        }
    }
}
