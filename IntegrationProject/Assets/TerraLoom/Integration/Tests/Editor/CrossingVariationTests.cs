using System;
using System.Linq;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Integration;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Tests
{
    public sealed class CrossingVariationTests
    {
        [Test] public void ManualVariationPublishesAndReloadsWithCurrentRights()
        {
            WithScene("TerraLoomManualCrossing",v=>
            {
                Assert.That(v.SeedDriven,Is.False);Assert.That(v.Rebuild(),Is.True,v.Scenario.Composition.Diagnostics);
                Reload(v);v.Clear();
            });
        }

        [Test] public void SeedChangesRealTerrainTargetsAndRouteAndSameSeedRepeats()
        {
            WithScene("TerraLoomSeededCrossing",v=>
            {
                Assert.That(v.SeedDriven,Is.True);var c=v.Scenario.Composition;
                v.Seed=2043;Assert.That(v.Rebuild(),Is.True,c.Diagnostics);
                string first=c.World.TerrainContentFingerprint;var points=c.Paths.LastPlan.Routes.Single().Waypoints.ToArray();
                var anchors=c.LastSharedInput.Anchors.Select(a=>a.Position).ToArray();Reload(v);
                v.Seed=2044;Assert.That(v.Rebuild(),Is.True,c.Diagnostics);
                Assert.That(c.World.TerrainContentFingerprint,Is.Not.EqualTo(first));
                Assert.That(c.LastSharedInput.Anchors.Select(a=>a.Position).SequenceEqual(anchors),Is.False);
                Assert.That(c.Paths.LastPlan.Routes.Single().Waypoints.SequenceEqual(points),Is.False);Reload(v);
                v.Seed=2043;Assert.That(v.Rebuild(),Is.True,c.Diagnostics);
                Assert.That(c.World.TerrainContentFingerprint,Is.EqualTo(first));
                CollectionAssert.AreEqual(points,c.Paths.LastPlan.Routes.Single().Waypoints);v.Clear();
            });
        }

        [TestCase(2043)] [TestCase(2044)] public void ScopedSeedRightsRemainBoundedAndWithdrawalRejectsCurrentComposition(int seed)
        {
            WithScene("TerraLoomSeededCrossing",v=>
            {
                v.Seed=seed;var s=v.Scenario;var c=s.Composition;Assert.That(v.Rebuild(),Is.True,c.Diagnostics);
                var grants=c.CrossingPermissions(c.LastSharedInput).Grants;
                Assert.That(grants.Count,Is.GreaterThan(0));Assert.That(grants.Count,Is.LessThanOrEqualTo(1024));
                var zone=s.ApprovedCrossingZone;var area=new WorldBounds(zone.xMin,zone.yMin,zone.xMax,zone.yMax);
                Assert.That(grants.All(g=>g.Sections.Count>0&&g.Sections.Count<=64&&g.Sections.All(p=>area.Contains(p.Area))),Is.True);
                var root=c.Paths.GeneratedRoot;var terrain=c.World.Terrain.terrainData;
                s.WithdrawPermissions=true;Assert.That(s.Generate(),Is.False);
                Assert.That(c.ActiveStep,Is.EqualTo("03-plan-routes"));Assert.That(c.Paths.GeneratedRoot,Is.SameAs(root));
                Assert.That(c.World.Terrain.terrainData,Is.SameAs(terrain));Assert.That(c.ValidateCurrent(out _),Is.False);
                v.Clear();
            });
        }

        private static void Reload(CrossingRecipeVariant v)
        {
            var c=v.Scenario.Composition;Assert.That(c.GenerationState,Is.EqualTo(GenerationRunState.Ready));
            Assert.That(c.Paths.LastPlan.Routes.Single().BridgeCrossings.Count,Is.EqualTo(1));
            Assert.That(v.Scenario.ProbeRoot,Is.Null);Assert.That(c.ValidateCurrent(out var reason),Is.True,reason);
            var bounds=c.CapturePathBounds(new WorldBounds(0,0,96,96));
            Assert.That(c.Paths.LoadPlanFromSnapshot(c.Paths.PlanJson,c.LastSharedInput,bounds,c.Paths.Connections.Select(r=>r.Capture()),currentPermissions:()=>c.CrossingPermissions(c.LastSharedInput)),Is.True,c.Paths.Diagnostics);
            Assert.That(c.ValidateCurrent(out reason),Is.True,reason);
        }
        private static void WithScene(string name,Action<CrossingRecipeVariant> action)
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try{EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/"+name+".unity");action(Object.FindFirstObjectByType<CrossingRecipeVariant>());}
            finally{if(setup.Any(s=>s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
        }
    }
}
