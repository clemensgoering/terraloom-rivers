using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Core.Unity;
using TerraLoom.Integration;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Tests
{
    public sealed class SourceLifecycleTests
    {
        [Test] public void ExplicitCandidateCaptureUsesItsOwnRasterSeedAndAnchorsWithoutLiveBinding()
        {
            WithScene(v=>
            {
                var c=v.Scenario.Composition;var world=c.World;var live=world.Terrain.terrainData;
                var collider=world.Terrain.GetComponent<TerrainCollider>().terrainData;
                int seed=world.Seed;string fingerprint=world.TerrainContentFingerprint;
                var anchors=new[]{new ManualWorldAnchor{Id="source",Position=new Vector3(48,0,8)},
                    new ManualWorldAnchor{Id="mouth",Position=new Vector3(48,0,88)},
                    new ManualWorldAnchor{Id="west",Position=new Vector3(24,0,56)},
                    new ManualWorldAnchor{Id="east",Position=new Vector3(72,0,60)}};
                using(var publisher=new WorldSourcePublication(world))
                using(var source=publisher.Prepare(991,anchors,data=>
                {
                    data.heightmapResolution=65;data.size=new Vector3(96,16,96);data.alphamapResolution=64;
                    var heights=new float[65,65];
                    for(int z=0;z<65;z++)for(int x=0;x<65;x++)heights[z,x]=(6-.03f*96*z/64)/16;
                    data.SetHeights(0,0,heights);
                }))
                {
                    var river=c.Rivers.CaptureInput(out _,out _,source);
                    var path=c.Paths.CaptureInput(c.Paths.CaptureProfile(),out _,out _,source);
                    Assert.That(river.Identity.Seed,Is.EqualTo(991));Assert.That(path.Identity.Seed,Is.EqualTo(991));
                    Assert.That(((GridHeightSource)river.Terrain.Heights).XCount,Is.EqualTo(65));
                    Assert.That(((GridHeightSource)path.Terrain.Heights).XCount,Is.EqualTo(65));
                    double expected=Math.Max(96d/64,96d/63);
                    Assert.That(c.Rivers.CaptureProfile(source.Data).TerrainCellGuard,Is.EqualTo(expected).Within(.000001));
                    CollectionAssert.AreEqual(anchors.OrderBy(a=>a.Id,StringComparer.Ordinal).Select(a=>new WorldPoint(a.Position.x,a.Position.y,a.Position.z)),path.Anchors.Select(a=>a.Position));
                    Assert.That(world.Terrain.terrainData,Is.SameAs(live));
                    Assert.That(world.Terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(collider));
                    Assert.That(world.Seed,Is.EqualTo(seed));Assert.That(world.TerrainContentFingerprint,Is.EqualTo(fingerprint));
                }
            });
        }
        [TestCase("source-prepared")] [TestCase("river-bound")]
        [TestCase("before-source-bind")] [TestCase("source-bound")] [TestCase("all-bound")]
        public void FailedReplacementRetainsPublishedSourceAndIdentity(string boundary)
            => WithScene(v=>CheckRollback(v,boundary,false));

        [TestCase("source-prepared")] [TestCase("river-bound")]
        [TestCase("before-source-bind")] [TestCase("source-bound")]
        public void CancelledReplacementRetainsPublishedSourceAndIdentity(string boundary)
            => WithScene(v=>CheckRollback(v,boundary,true));

        private static void CheckRollback(CrossingRecipeVariant v,string boundary,bool cancel)
        {
            var c=v.Scenario.Composition;v.Seed=2043;Assert.That(v.Rebuild(),Is.True,c.Diagnostics);
            var source=v.PublishedSource;var data=c.World.Terrain.terrainData;
            var collider=c.World.Terrain.GetComponent<TerrainCollider>().terrainData;
            var paths=c.Paths.GeneratedRoot;var rivers=c.Rivers.GeneratedRoot;
            var anchors=c.World.CaptureAnchors().Anchors.Select(a=>a.Position).ToArray();
            var pathsJson=c.Paths.PlanJson;var riversJson=c.Rivers.PlanJson;
            string heights=c.World.TerrainContentFingerprint;
            var counts=Resources.FindObjectsOfTypeAll<TerrainData>().Length;
            using(var cancellation=new CancellationTokenSource())
            {
                bool reached=false;
                c.PublicationBoundary=point=>
                {
                    if(point!=boundary)return;reached=true;
                    if(cancel)cancellation.Cancel();else throw new InvalidOperationException("Injected source lifecycle fault");
                };
                v.Seed=2044;Assert.That(v.Rebuild(cancellation.Token),Is.False,c.Diagnostics);Assert.That(reached,Is.True,c.Diagnostics);
            }
            Assert.That(v.Seed,Is.EqualTo(2044),"Requested seed remains user intent.");
            Assert.That(v.PublishedSeed,Is.EqualTo(2043));Assert.That(v.PublishedSource,Is.SameAs(source));
            Assert.That(c.GenerationState,Is.EqualTo(cancel?GenerationRunState.Cancelled:GenerationRunState.Failed));
            Assert.That(c.World.Terrain.terrainData,Is.SameAs(data));
            Assert.That(c.World.Terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(collider));
            CollectionAssert.AreEqual(anchors,c.World.CaptureAnchors().Anchors.Select(a=>a.Position));
            Assert.That(c.Paths.GeneratedRoot,Is.SameAs(paths));Assert.That(c.Rivers.GeneratedRoot,Is.SameAs(rivers));
            Assert.That(paths.activeSelf&&rivers.activeSelf,Is.True);
            Assert.That(c.Paths.PlanJson,Is.EqualTo(pathsJson));Assert.That(c.Rivers.PlanJson,Is.EqualTo(riversJson));
            Assert.That(c.World.TerrainContentFingerprint,Is.EqualTo(heights));
            Assert.That(Resources.FindObjectsOfTypeAll<TerrainData>().Length,Is.EqualTo(counts),"Candidate source and carve copies are both released.");
            c.PublicationBoundary=null;Assert.That(v.Rebuild(),Is.True,c.Diagnostics);
            Assert.That(v.PublishedSeed,Is.EqualTo(2044));Assert.That(source==null,Is.True,"Retired owned source is released after new outputs commit.");
            Assert.That(c.ValidateCurrent(out var reason),Is.True,reason);
            v.Seed=2043;Assert.That(v.Rebuild(),Is.True,c.Diagnostics);
            Assert.That(c.World.TerrainContentFingerprint,Is.EqualTo(heights));
            v.Clear();
        }

        [Test] public void ClearRestoresDistinctBorrowedRenderAndColliderDataWithoutMutatingAssets()
        {
            WithScene(v=>
            {
                var c=v.Scenario.Composition;var world=c.World;
                var borrowed=world.Terrain.terrainData;var initialSeed=world.Seed;
                var initialAnchors=world.ManualAnchors;bool initialUseSeed=world.UseSeedAnchors;
                var collider=world.Terrain.GetComponent<TerrainCollider>();var originalCollider=collider.terrainData;
                var distinct=Object.Instantiate(borrowed);var material=world.Terrain.materialTemplate;
                var layers=borrowed.terrainLayers;var raw=borrowed.GetHeights(0,0,129,129);
                try
                {
                    collider.terrainData=distinct;
                    v.Seed=2043;Assert.That(v.Rebuild(),Is.True,c.Diagnostics);
                    var first=v.PublishedSource;v.Seed=2044;Assert.That(v.Rebuild(),Is.True,c.Diagnostics);
                    Assert.That(first==null,Is.True);
                    var last=v.PublishedSource;v.Clear();
                    Assert.That(world.Terrain.terrainData,Is.SameAs(borrowed));Assert.That(collider.terrainData,Is.SameAs(distinct));
                    Assert.That(world.Seed,Is.EqualTo(initialSeed));Assert.That(world.ManualAnchors,Is.SameAs(initialAnchors));
                    Assert.That(world.UseSeedAnchors,Is.EqualTo(initialUseSeed));Assert.That(last==null,Is.True);
                    Assert.That(world.Terrain.materialTemplate,Is.SameAs(material));CollectionAssert.AreEqual(layers,borrowed.terrainLayers);
                    Assert.That(borrowed.GetHeights(0,0,129,129),Is.EqualTo(raw));Assert.That(distinct!=null,Is.True);
                    v.Clear();Assert.That(collider.terrainData,Is.SameAs(distinct));
                }
                finally{collider.terrainData=originalCollider;Object.DestroyImmediate(distinct);}
            });
        }

        [Test] public void FirstFailureRetainsBorrowedSourceAndCreatesNoPublishedGeneration()
        {
            WithScene(v=>
            {
                var c=v.Scenario.Composition;var data=c.World.Terrain.terrainData;int seed=c.World.Seed;
                bool reached=false;
                c.PublicationBoundary=point=>{if(point=="river-bound"){reached=true;throw new InvalidOperationException("First generation fault");}};
                Assert.That(v.Rebuild(),Is.False);Assert.That(c.World.Terrain.terrainData,Is.SameAs(data));
                Assert.That(reached,Is.True,c.Diagnostics);
                Assert.That(c.World.Seed,Is.EqualTo(seed));Assert.That(v.PublishedSource,Is.Null);
                Assert.That(c.Paths.GeneratedRoot,Is.Null);Assert.That(c.Rivers.GeneratedRoot,Is.Null);
                c.PublicationBoundary=null;v.Clear();
            });
        }

        private static void WithScene(Action<CrossingRecipeVariant> action)
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try{EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomSeededCrossing.unity");action(Object.FindFirstObjectByType<CrossingRecipeVariant>());}
            finally{if(setup.Any(s=>s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
        }
    }
}
