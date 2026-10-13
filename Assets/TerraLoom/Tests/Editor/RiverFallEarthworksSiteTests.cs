using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Core.Unity;
using TerraLoom.Rivers;
using TerraLoom.Rivers.Unity;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Tests
{
    public sealed class RiverFallEarthworksSiteTests
    {
        [Test] public void OwnedSingleTilePreflightIsReadOnlyAndRejectsStaleOrOccupiedSite()
        {
            var host=new GameObject("Owned fall test");TerrainData data=null;Terrain terrain=null;
            try
            {
                var world=host.AddComponent<TerraLoomWorld>();world.WorldId="fall-test";
                world.Resolution=129;world.TerrainSize=new Vector3(32,16,32);world.GenerateTerrain();
                terrain=world.Terrain;data=terrain.terrainData;
                var heights=new float[129,129];
                for(int z=0;z<129;z++)for(int x=0;x<129;x++)
                    heights[z,x]=(x<64?8f:2f-.6f*Mathf.Clamp01((x-64)/56f))/16f;
                data.SetHeights(0,0,heights);
                world.UseSeedAnchors=false;
                world.ManualAnchors=new[]{
                    new ManualWorldAnchor{Id="source",Position=new Vector3(4,8,16)},
                    new ManualWorldAnchor{Id="fall",Position=new Vector3(16,8,16)},
                    new ManualWorldAnchor{Id="mouth",Position=new Vector3(28,2,16)}};
                var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;rivers.AutomaticSourceAndMouth=false;
                rivers.Connections.Add(new RiverConnectionSettings{Id="main",SourceId="source",MouthId="mouth",FallId="main-fall",FallAnchorId="fall"});
                var snapshot=rivers.CaptureInput(out var bounds,out var requests);
                var profile=rivers.CaptureProfile();var plan=RiverPlanner.Plan(snapshot,profile,bounds,requests);
                Assert.That(plan.FallCandidates.Count,Is.EqualTo(1),string.Join(";",plan.Reports.Select(r=>r.Detail)));
                var candidate=plan.FallCandidates.Single();
                var site=RiverFallEarthworksSite.Inspect(candidate,snapshot.Identity,world,data,profile);
                Assert.That(site.HoleWidth,Is.GreaterThanOrEqualTo(4));
                Assert.That(site.HoleHeight,Is.GreaterThanOrEqualTo(4));
                Assert.That(data.GetHoles(site.HoleX,site.HoleZ,site.HoleWidth,site.HoleHeight).Cast<bool>().All(x=>x),Is.True);
                using(var prepared=PreparedFallEarthworks.Prepare(candidate,snapshot,world,data,profile))
                {
                    Assert.That(prepared.TerrainData,Is.Not.SameAs(data));
                    Assert.That(prepared.TerrainData.GetHoles(site.HoleX,site.HoleZ,site.HoleWidth,site.HoleHeight)
                        .Cast<bool>().All(x=>!x),Is.True);
                    Assert.That(prepared.Solid.vertexCount,Is.GreaterThan(0));
                    AssertClosed(prepared.Solid);
                    Assert.That(prepared.Water.Length,Is.EqualTo(3));
                    var pool=candidate.Recipe.Pool;
                    var y=terrain.transform.position.y+prepared.TerrainData.GetInterpolatedHeight(
                        (float)(pool.X/data.size.x),(float)(pool.Z/data.size.z));
                    Assert.That(y,Is.LessThan(pool.Y-.1));
                    Assert.That(data.GetHoles(site.HoleX,site.HoleZ,site.HoleWidth,site.HoleHeight).Cast<bool>().All(x=>x),Is.True);
                }
                var changed=data.GetHeights(0,0,129,129);changed[10,10]+=.01f;
                data.SetHeights(0,0,changed);
                Assert.Throws<InvalidOperationException>(()=>PreparedFallEarthworks.Prepare(candidate,snapshot,world,data,profile));
                data.SetHeights(0,0,heights);
                var stale=new PlanIdentity(snapshot.Identity.Seed,snapshot.Identity.Revision+1,
                    snapshot.Identity.AlgorithmVersion,snapshot.Identity.ProfileVersion,snapshot.Identity.InputFingerprint);
                Assert.Throws<InvalidOperationException>(()=>RiverFallEarthworksSite.Inspect(candidate,stale,world,data,profile));
                using(var cancelled=new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    Assert.Throws<OperationCanceledException>(()=>RiverFallEarthworksSite.Inspect(candidate,snapshot.Identity,world,data,profile,cancelled.Token));
                    Assert.Throws<OperationCanceledException>(()=>PreparedFallEarthworks.Prepare(candidate,snapshot,world,data,profile,cancelled.Token));
                }
                var foreign=Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();
                try
                {
                    world.Terrain=foreign;
                    Assert.Throws<InvalidOperationException>(()=>PreparedFallEarthworks.Prepare(candidate,snapshot,world,data,profile));
                }
                finally{world.Terrain=terrain;Object.DestroyImmediate(foreign.gameObject);}
                var occupied=data.GetHoles(site.HoleX,site.HoleZ,site.HoleWidth,site.HoleHeight);occupied[0,0]=false;
                data.SetHoles(site.HoleX,site.HoleZ,occupied);
                Assert.Throws<InvalidOperationException>(()=>RiverFallEarthworksSite.Inspect(candidate,snapshot.Identity,world,data,profile));
            }
            finally
            {
                if(terrain)Object.DestroyImmediate(terrain.gameObject);
                if(host)Object.DestroyImmediate(host);
                if(data)Object.DestroyImmediate(data);
            }
        }
        private static void AssertClosed(Mesh mesh)
        {
            string Vertex(Vector3 p)=>Mathf.RoundToInt(p.x*1000)+","+Mathf.RoundToInt(p.y*1000)+","+Mathf.RoundToInt(p.z*1000);
            string Edge(string a,string b)=>string.CompareOrdinal(a,b)<0?a+"|"+b:b+"|"+a;
            var counts=new Dictionary<string,int>();var vertices=mesh.vertices;var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=Vertex(vertices[triangles[i]]);var b=Vertex(vertices[triangles[i+1]]);var c=Vertex(vertices[triangles[i+2]]);
                foreach(var key in new[]{Edge(a,b),Edge(b,c),Edge(c,a)})
                {counts.TryGetValue(key,out int count);counts[key]=count+1;}
            }
            Assert.That(counts.Values.All(count=>count==2),Is.True,
                "Prepared solid has open/nonmanifold welded edges: "+string.Join(";",counts.Where(k=>k.Value!=2).Take(10).Select(k=>k.Key+"="+k.Value)));
        }
    }
}
