using System;
using System.Linq;
using System.Reflection;
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
    public sealed class CompositionTransactionTests
    {
        [TestCase("river-prepared")]
        [TestCase("before-commit")]
        [TestCase("river-bound")]
        [TestCase("all-bound")]
        public void InjectedFailureRestoresPublishedResourcesAndDisposesCandidates(string boundary)
        {
            WithScene(s=>
            {
                var c=s.Composition;Assert.That(s.Generate(),Is.True,c.Diagnostics);
                var old=new Published(c);var decoration=AddDecoration(c);var decorationRoot=decoration.GeneratedRoot;
                var meshIds=TransientMeshIds();GameObject candidateRiver=null;TerrainData candidateData=null;
                c.PublicationBoundary=point=>
                {
                    if(point=="river-bound"){candidateRiver=c.Rivers.GeneratedRoot;candidateData=c.World.Terrain.terrainData;}
                    if(point==boundary)throw new InvalidOperationException("injected "+point);
                };
                Assert.That(c.Generate(),Is.False);Assert.That(c.GenerationState,Is.EqualTo(GenerationRunState.Failed));
                old.AssertUnchanged(c);Assert.That(decoration.GeneratedRoot,Is.SameAs(decorationRoot));
                Assert.That(decorationRoot.activeSelf,Is.True);
                Assert.That(candidateRiver==null,Is.True);Assert.That(candidateData==null,Is.True);
                CollectionAssert.AreEquivalent(meshIds,TransientMeshIds(),"A failed attempt leaked transient meshes.");
                c.PublicationBoundary=null;Assert.That(c.Generate(),Is.True,c.Diagnostics);
                Assert.That(c.ValidateCurrent(out var reason),Is.True,reason);
                Assert.That(decoration.GeneratedRoot==null,Is.True,"Successful replacement invalidates decoration.");
                c.Clear();Assert.That(c.Paths.GeneratedRoot==null&&c.Rivers.GeneratedRoot==null,Is.True);
                Assert.That(c.World.Terrain.terrainData,Is.SameAs(old.Source));
            });
        }

        [TestCase("river-prepared",false)]
        [TestCase("before-commit",false)]
        [TestCase("river-bound",false)]
        [TestCase("all-bound",true)]
        public void CancellationHasAnExplicitCollectiveCommitBoundary(string boundary,bool committed)
        {
            WithScene(s=>
            {
                var c=s.Composition;Assert.That(s.Generate(),Is.True,c.Diagnostics);var old=new Published(c);
                using(var cancellation=new CancellationTokenSource())
                {
                    c.PublicationBoundary=point=>{if(point==boundary)cancellation.Cancel();};
                    Assert.That(c.Generate(cancellation.Token),Is.EqualTo(committed),c.Diagnostics);
                }
                if(committed)
                {
                    Assert.That(c.GenerationState,Is.EqualTo(GenerationRunState.Ready));
                    Assert.That(c.World.Terrain.terrainData,Is.Not.SameAs(old.Data));
                    Assert.That(c.ValidateCurrent(out var reason),Is.True,reason);
                }
                else{Assert.That(c.GenerationState,Is.EqualTo(GenerationRunState.Cancelled));old.AssertUnchanged(c);}
                c.PublicationBoundary=null;Assert.That(c.Generate(),Is.True,c.Diagnostics);c.Clear();
                Assert.That(c.World.Terrain.terrainData,Is.SameAs(old.Source));
            });
        }

        [Test] public void PermissionWithdrawalAtFinalBoundaryCannotPublishPreparedGeometry()
        {
            WithScene(s=>
            {
                var c=s.Composition;Assert.That(s.Generate(),Is.True,c.Diagnostics);var old=new Published(c);
                c.PublicationBoundary=point=>{if(point=="before-commit")s.WithdrawPermissions=true;};
                Assert.That(c.Generate(),Is.False);old.AssertUnchanged(c);
                Assert.That(c.Diagnostics,Does.Contain("permission").IgnoreCase);
                c.PublicationBoundary=null;s.WithdrawPermissions=false;Assert.That(c.Generate(),Is.True,c.Diagnostics);c.Clear();
            });
        }

        [Test] public void PathGeometryFailureAfterRiverPreparationRetainsPriorComposition()
        {
            WithScene(s=>
            {
                var c=s.Composition;Assert.That(s.Generate(),Is.True,c.Diagnostics);var old=new Published(c);
                int budget=c.Paths.MaximumGeometryVertices;
                c.PublicationBoundary=point=>{if(point=="river-prepared")c.Paths.MaximumGeometryVertices=128;};
                Assert.That(c.Generate(),Is.False);old.AssertUnchanged(c);
                c.PublicationBoundary=null;c.Paths.MaximumGeometryVertices=budget;
                Assert.That(c.Generate(),Is.True,c.Diagnostics);c.Clear();
            });
        }

        [Test] public void BindingFailureOnFirstGenerationRestoresForeignSourceWithoutPublishedRoots()
        {
            WithScene(s=>
            {
                var c=s.Composition;var source=c.World.Terrain.terrainData;
                var collider=c.World.Terrain.GetComponent<TerrainCollider>();var colliderSource=collider.terrainData;
                var meshes=TransientMeshIds();
                c.PublicationBoundary=point=>{if(point=="river-bound")throw new InvalidOperationException("first generation failure");};
                Assert.That(s.Generate(),Is.False);
                Assert.That(c.World.Terrain.terrainData,Is.SameAs(source));Assert.That(collider.terrainData,Is.SameAs(colliderSource));
                Assert.That(c.Paths.GeneratedRoot==null&&c.Rivers.GeneratedRoot==null,Is.True);
                Assert.That(c.Paths.LastPlan,Is.Null);Assert.That(c.Rivers.LastPlan,Is.Null);
                CollectionAssert.AreEquivalent(meshes,TransientMeshIds());
                c.PublicationBoundary=null;Assert.That(c.Generate(),Is.True,c.Diagnostics);c.Clear();
                Assert.That(c.World.Terrain.terrainData,Is.SameAs(source));
            });
        }

        [Test] public void MaterialChangeAfterPreparationRejectsStaleCandidate()
        {
            WithScene(s=>
            {
                var c=s.Composition;Assert.That(s.Generate(),Is.True,c.Diagnostics);var old=new Published(c);
                var material=c.Paths.GroundMaterial;
                c.PublicationBoundary=point=>{if(point=="before-commit")c.Paths.GroundMaterial=c.Paths.BridgeMaterial;};
                Assert.That(c.Generate(),Is.False);old.AssertUnchanged(c);
                c.Paths.GroundMaterial=material;c.PublicationBoundary=null;
                Assert.That(c.Generate(),Is.True,c.Diagnostics);c.Clear();
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReentrantGenerationOrClearCannotDestroyRollbackResources(bool clear)
        {
            WithScene(s=>
            {
                var c=s.Composition;Assert.That(s.Generate(),Is.True,c.Diagnostics);var old=new Published(c);
                c.PublicationBoundary=point=>{if(point=="river-bound"){if(clear)c.Clear();else c.Generate();}};
                Assert.That(c.Generate(),Is.False);old.AssertUnchanged(c);
                c.PublicationBoundary=null;Assert.That(c.Generate(),Is.True,c.Diagnostics);c.Clear();
            });
        }

        [TestCase(false,false)]
        [TestCase(true,false)]
        [TestCase(true,true)]
        public void ColliderReplacementOrRemovalAfterPreparationCannotReceiveDisposedCandidate(bool replace,bool initiallyAbsent)
        {
            WithScene(s=>
            {
                var c=s.Composition;Assert.That(s.Generate(),Is.True,c.Diagnostics);
                var terrain=c.World.Terrain;var data=terrain.terrainData;var river=c.Rivers.GeneratedRoot;var path=c.Paths.GeneratedRoot;
                if(initiallyAbsent)Object.DestroyImmediate(terrain.GetComponent<TerrainCollider>());
                TerrainCollider replacement=null;
                c.PublicationBoundary=point=>
                {
                    if(point!="river-prepared")return;
                    var previous=terrain.GetComponent<TerrainCollider>();if(previous)Object.DestroyImmediate(previous);
                    if(replace){replacement=terrain.gameObject.AddComponent<TerrainCollider>();replacement.terrainData=data;}
                };
                Assert.That(c.Generate(),Is.False);
                Assert.That(terrain.terrainData,Is.SameAs(data));Assert.That(c.Rivers.GeneratedRoot,Is.SameAs(river));Assert.That(c.Paths.GeneratedRoot,Is.SameAs(path));
                if(replace)Assert.That(replacement.terrainData,Is.SameAs(data));else Assert.That(terrain.GetComponent<TerrainCollider>(),Is.Null);
                c.PublicationBoundary=null;Assert.That(c.Generate(),Is.True,c.Diagnostics);c.Clear();
            });
        }

        [Test] public void ThrowingStandaloneRetirementKeepsNewPublicationAndReportsCleanup()
        {
            WithScene(s=>
            {
                var c=s.Composition;Assert.That(s.Generate(),Is.True,c.Diagnostics);var old=c.Paths.GeneratedRoot;
                var terrain=c.World.Terrain;var p=terrain.transform.position;var size=terrain.terrainData.size;
                var bounds=c.CapturePathBounds(new WorldBounds(p.x,p.z,p.x+size.x,p.z+size.z));
                Assert.That(c.Paths.GenerateFromSnapshot(c.LastSharedInput,bounds,c.Paths.Connections.Select(r=>r.Capture()),
                    destroy:_=>throw new InvalidOperationException("standalone retirement fault"),
                    currentPermissions:()=>c.CrossingPermissions(c.LastSharedInput)),Is.True,c.Paths.Diagnostics);
                Assert.That(old==null,Is.True,"Default retirement fallback must dispose the old root.");
                Assert.That(c.Paths.GeneratedRoot.activeSelf,Is.True);Assert.That(c.Paths.LastPlan,Is.Not.Null);
                Assert.That(c.Paths.Diagnostics,Does.Contain("standalone retirement fault"));
                Assert.That(c.ValidateCurrent(out var reason),Is.True,reason);c.Clear();
            });
        }

        [Test] public void ThrowingCollectiveRetirementStillRetiresBothOldModulesAndKeepsNewWorldReady()
        {
            WithScene(s=>
            {
                var c=s.Composition;Assert.That(s.Generate(),Is.True,c.Diagnostics);
                var oldPath=c.Paths.GeneratedRoot;var oldRiver=c.Rivers.GeneratedRoot;var oldData=c.World.Terrain.terrainData;
                c.PathRetirement=_=>throw new InvalidOperationException("collective retirement fault");
                Assert.That(c.Generate(),Is.True,c.Diagnostics);
                Assert.That(c.GenerationState,Is.EqualTo(GenerationRunState.Ready));
                Assert.That(oldPath==null&&oldRiver==null&&oldData==null,Is.True,"Both retirements must run despite one callback failure.");
                Assert.That(c.World.Terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(c.World.Terrain.terrainData));
                Assert.That(c.Paths.GeneratedRoot.activeSelf&&c.Rivers.GeneratedRoot.activeSelf,Is.True);
                Assert.That(c.ValidateCurrent(out var reason),Is.True,reason);
                Assert.That(c.Diagnostics,Does.Contain("collective retirement fault"));
                c.PathRetirement=null;c.Clear();
            });
        }

        private sealed class Published
        {
            public readonly TerrainData Data,Source,ColliderData;
            private readonly GameObject river,path;
            private readonly object riverPlan,pathPlan;
            private readonly string riverJson,pathJson;
            public Published(TerraLoomIntegration c)
            {
                Data=c.World.Terrain.terrainData;Source=c.Rivers.OriginalTerrainData;
                ColliderData=c.World.Terrain.GetComponent<TerrainCollider>().terrainData;
                river=c.Rivers.GeneratedRoot;path=c.Paths.GeneratedRoot;riverPlan=c.Rivers.LastPlan;pathPlan=c.Paths.LastPlan;
                riverJson=c.Rivers.PlanJson;pathJson=c.Paths.PlanJson;
            }
            public void AssertUnchanged(TerraLoomIntegration c)
            {
                Assert.That(c.World.Terrain.terrainData,Is.SameAs(Data));
                Assert.That(c.World.Terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(ColliderData));
                Assert.That(c.Rivers.OriginalTerrainData,Is.SameAs(Source));Assert.That(c.Rivers.CarvedTerrainData,Is.SameAs(Data));
                Assert.That(c.Rivers.GeneratedRoot,Is.SameAs(river));Assert.That(c.Paths.GeneratedRoot,Is.SameAs(path));
                Assert.That(river.activeSelf&&path.activeSelf,Is.True);
                Assert.That(c.Rivers.LastPlan,Is.SameAs(riverPlan));Assert.That(c.Paths.LastPlan,Is.SameAs(pathPlan));
                Assert.That(c.Rivers.PlanJson,Is.EqualTo(riverJson));Assert.That(c.Paths.PlanJson,Is.EqualTo(pathJson));
            }
        }
        private static RegionDecoration AddDecoration(TerraLoomIntegration c)
        {
            var host=new GameObject("transaction decoration fixture");var decoration=host.AddComponent<RegionDecoration>();decoration.World=c.World;
            var generated=new GameObject("existing decoration");generated.transform.SetParent(host.transform);
            typeof(RegionDecoration).GetField("generatedRoot",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(decoration,generated);
            return decoration;
        }
        private static int[] TransientMeshIds()=>Resources.FindObjectsOfTypeAll<Mesh>()
            .Where(m=>(m.hideFlags&HideFlags.DontSave)!=0).Select(m=>m.GetInstanceID()).OrderBy(id=>id).ToArray();
        private static void WithScene(Action<FrozenCurvedCrossing> action)
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomPositiveCurvedCrossing.unity");
                action(Object.FindFirstObjectByType<FrozenCurvedCrossing>());
            }
            finally
            {
                if(setup.Any(s=>s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
    }
}
