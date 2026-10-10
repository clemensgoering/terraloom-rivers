using System;
using System.Linq;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Integration;
using TerraLoom.Paths;
using TerraLoom.Paths.Unity;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Tests
{
    public sealed class FrozenTrussTests
    {
        [Test] public void UnchangedOriginalTargetsFailWithoutEarthworksOrBudgetExhaustionAndLocalConstructionRemainsDistinct()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomFrozenTruss.unity");
                var scenario=Object.FindFirstObjectByType<FrozenCurvedCrossing>();var c=scenario.Composition;var source=c.World.Terrain.terrainData;
                Assert.That(c.World.Seed,Is.EqualTo(42));Assert.That(c.Paths.Width,Is.EqualTo(2));Assert.That(c.Rivers.Width,Is.EqualTo(4));Assert.That(c.Rivers.BankWidth,Is.EqualTo(2));
                Assert.That(c.World.ManualAnchors.Single(a=>a.Id=="west").Position,Is.EqualTo(new Vector3(12,0,24)));
                Assert.That(c.World.ManualAnchors.Single(a=>a.Id=="east").Position,Is.EqualTo(new Vector3(84,0,24)));
                Assert.That(c.Generate(),Is.False);Assert.That(c.ActiveStep,Is.EqualTo("03-plan-routes"));
                var report=c.LastPathAttempt.Reports.Single();Assert.That(report.Outcome,Is.EqualTo(PathOutcome.NoRoute));
                Assert.That(report.ExpandedNodes,Is.LessThan(c.Paths.MaximumNodes));Assert.That(report.Detail,Does.Contain("start=0, end=0"));
                Assert.That(c.World.Terrain.terrainData,Is.SameAs(source));Assert.That(c.Rivers.GeneratedRoot,Is.Null);Assert.That(c.Paths.GeneratedRoot,Is.Null);
                // Four actual source heights expose the mandatory conservative grid corner at each original target.
                foreach(var anchor in c.LastSharedInput.Anchors.Where(a=>a.Id=="west"||a.Id=="east"))
                {
                    var grid=(GridHeightSource)c.LastSharedInput.Terrain.Heights;double x=anchor.Position.X,z=anchor.Position.Z,sx=(grid.MaxX-grid.MinX)/(grid.XCount-1),sz=(grid.MaxZ-grid.MinZ)/(grid.ZCount-1);
                    Assert.That(grid.TryGetHeight(x,z,out double h00),Is.True);grid.TryGetHeight(x+(x<48?sx:-sx),z,out double h10);grid.TryGetHeight(x,z+sz,out double h01);
                    double gx=(h10-h00)/sx,gz=(h01-h00)/sz,lowerBound=Math.Sqrt(gx*gx+gz*gz)/Math.Sqrt(2);
                    Assert.That(lowerBound,Is.GreaterThan(c.Paths.MaximumSlope));
                    double best=double.PositiveInfinity;int exactAccepted=0;string bestDetail="";
                    for(int dz=-1;dz<=2;dz++)for(int dx=-1;dx<=2;dx++)
                    {
                        if(dx==0&&dz==0)continue;double tx=x+dx*c.Paths.CellSize,tz=z+dz*c.Paths.CellSize;
                        grid.TryGetHeight(tx,tz,out double th);
                        var exact=PathTerrainSlopeInspection.Inspect(grid,anchor.Position,new WorldPoint(tx,th,tz),c.Paths.Width,c.Paths.MaximumSlope);
                        if(exact.Accepted)exactAccepted++;
                        double maximum=Math.Max(exact.MaximumAlongGrade,exact.MaximumAcrossGrade);
                        if(maximum<best){best=maximum;bestDetail="toXZ=("+tx+","+tz+") "+exact.Diagnostic;}
                    }
                    TestContext.WriteLine("FROZEN_EXACT_TARGET "+anchor.Id+" accepted="+exactAccepted+" of15 nonzero candidates; bestMax="+best+" "+bestDetail);
                    Assert.That(exactAccepted,Is.Zero);Assert.That(best,Is.EqualTo(.41066270750032324).Within(.00001));
                    TestContext.WriteLine("FROZEN_TARGET "+anchor.Id+" x="+x+" z="+z+" gx="+gx+" gz="+gz+" best-direction lower bound at mandatory conservative cell="+lowerBound+" profile="+c.Paths.MaximumSlope+" expanded="+report.ExpandedNodes+". This diagnoses the current full-width grid gate, not global geometric impossibility.");
                }
                Assert.That(report.Detail,Does.Contain("Target west: 0/15 actual full-width strips"));
                Assert.That(report.Detail,Does.Contain("Target east: 0/15 actual full-width strips"));
                Assert.That(scenario.Generate(),Is.True,scenario.ProbeDiagnostic);Assert.That(c.GenerationState,Is.EqualTo(GenerationRunState.Failed));Assert.That(c.Paths.LastPlan,Is.Null);
                Assert.That(scenario.ProbeDiagnostic,Does.Contain("CONSTRUCTION ONLY"));var route=scenario.ProbeRoute;
                Assert.That(Math.Abs(route.Waypoints[2].X-route.Waypoints[1].X),Is.EqualTo(9.511811023622044).Within(.000001));
                Assert.That(scenario.ProbeRoot.GetComponentsInChildren<PathGeneratedGeometry>().Length,Is.EqualTo(3));
                foreach(var part in scenario.ProbeRoot.GetComponentsInChildren<PathGeneratedGeometry>())Assert.That(part.GetComponent<MeshCollider>().sharedMesh,Is.SameAs(part.GetComponent<MeshFilter>().sharedMesh));
                scenario.WithdrawPermissions=true;Assert.That(scenario.Generate(),Is.False);Assert.That(scenario.ProbeRoot,Is.Null);
                c.Clear();Assert.That(c.World.Terrain.terrainData,Is.SameAs(source));
            }
            finally
            {
                if(setup.Any(s=>s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
    }
}
