using System;
using System.Linq;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Integration;
using TerraLoom.Paths;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Tests
{
    public sealed class PositiveCurvedCrossingTests
    {
        [Test] public void FullRoutePublishesReloadsAndRevocationCannotRemainReady()
        {
            WithScene(s=>
            {
                var c=s.Composition;var source=c.World.Terrain.terrainData;
                Assert.That(s.ConstructionProbe,Is.False);
                Assert.That(s.Generate(),Is.True,c.Diagnostics);
                Assert.That(c.GenerationState,Is.EqualTo(GenerationRunState.Ready));
                Assert.That(c.ValidateCurrent(out var reason),Is.True,reason);
                Assert.That(s.ProbeRoot,Is.Null);Assert.That(s.ProbeRoute,Is.Null);
                var route=c.Paths.LastPlan.Routes.Single();
                Assert.That(route.BridgeCrossings.Count,Is.GreaterThan(0));
                Assert.That(route.BridgeCrossings.Any(b=>Math.Abs(Math.Abs(b.End.X-b.Start.X)-9.511811023622044)<.00001),Is.True);
                var terrain=c.World.Terrain;var position=terrain.transform.position;var size=terrain.terrainData.size;
                var bounds=c.CapturePathBounds(new WorldBounds(position.x,position.z,position.x+size.x,position.z+size.z));
                string json=c.Paths.PlanJson;
                Assert.That(c.Paths.LoadPlanFromSnapshot(json,c.LastSharedInput,bounds,c.Paths.Connections.Select(r=>r.Capture()),currentPermissions:()=>c.CrossingPermissions(c.LastSharedInput)),Is.True,c.Paths.Diagnostics);
                Assert.That(c.ValidateCurrent(out reason),Is.True,reason);
                var root=c.Paths.GeneratedRoot;var carved=c.World.Terrain.terrainData;
                s.WithdrawPermissions=true;
                Assert.That(s.Generate(),Is.False);Assert.That(c.ActiveStep,Is.EqualTo("03-plan-routes"));
                Assert.That(c.Paths.GeneratedRoot,Is.SameAs(root));Assert.That(c.World.Terrain.terrainData,Is.SameAs(carved));
                Assert.That(c.ValidateCurrent(out reason),Is.False);
                c.Clear();Assert.That(c.World.Terrain.terrainData,Is.SameAs(source));
            });
        }

        [Test] public void UnapprovedWetStripCannotBeFilledByPermissionBoundingBox()
        {
            WithScene(s=>
            {
                var c=s.Composition;Assert.That(s.Generate(),Is.True,c.Diagnostics);
                var original=c.CrossingPermissions;var root=c.Paths.GeneratedRoot;
                c.CrossingPermissions=input=>new PathCrossingPermissions(original(input).Grants.Select(g=>
                    new CollectiveCrossingAuthorization(g.CrossingId,g.Identity,g.Sections.SelectMany(section=>
                    {
                        var a=section.Area;double left=Math.Min(a.MaxX,37.9),right=Math.Max(a.MinX,38.1);
                        return new[]{a.MinX<left?new CrossingSectionPermission(section.WaterId,section.CandidateId,new WorldBounds(a.MinX,a.MinZ,left,a.MaxZ)):null,
                            right<a.MaxX?new CrossingSectionPermission(section.WaterId,section.CandidateId,new WorldBounds(right,a.MinZ,a.MaxX,a.MaxZ)):null}.Where(p=>p!=null);
                    }))));
                Assert.That(c.ValidateCurrent(out _),Is.False);
                Assert.That(c.Generate(),Is.False);Assert.That(c.ActiveStep,Is.EqualTo("03-plan-routes"));
                Assert.That(c.Paths.GeneratedRoot,Is.SameAs(root));Assert.That(c.ValidateCurrent(out _),Is.False);
                c.Clear();
            });
        }

        [Test] public void RoutingZoneChangeInvalidatesReloadAndRejectsStructureOutsideEvenWhenClearDeckFits()
        {
            WithScene(s=>
            {
                var c=s.Composition;Assert.That(s.Generate(),Is.True,c.Diagnostics);
                var binding=c.Paths.LastPlan.Routes.Single().BridgeCrossings.Single();
                Assert.That(binding.Start.Z+c.Paths.Width/2,Is.LessThan(58.8));
                Assert.That(binding.Start.Z+c.Paths.Width/2+c.Paths.CaptureProfile().BridgeLateralPadding,Is.GreaterThan(58.8));
                Assert.That(c.LastSharedInput.Waters.Any(w=>w.Banks.MinZ<54||w.Banks.MaxZ>58.9),Is.True,"The search zone must not filter full-world water rights.");
                var root=c.Paths.GeneratedRoot;string json=c.Paths.PlanJson;
                c.PathRoutingZone=new Rect(16,54,64,4.8f);
                Assert.That(c.ValidateCurrent(out _),Is.False);
                var bounds=c.CapturePathBounds(new WorldBounds(0,0,96,96));
                Assert.That(c.Paths.LoadPlanFromSnapshot(json,c.LastSharedInput,bounds,c.Paths.Connections.Select(r=>r.Capture()),currentPermissions:()=>c.CrossingPermissions(c.LastSharedInput)),Is.False);
                Assert.That(c.Paths.GeneratedRoot,Is.SameAs(root));
                Assert.That(s.Generate(),Is.False);Assert.That(c.ActiveStep,Is.EqualTo("03-plan-routes"));
                Assert.That(c.Paths.GeneratedRoot,Is.SameAs(root));Assert.That(c.ValidateCurrent(out _),Is.False);
                c.Clear();
            });
        }

        [Test] public void FreeRoutingRetainsTheLegitimateCheaperRiverBypass()
        {
            WithScene(s=>
            {
                var c=s.Composition;c.RestrictPathRouting=false;
                foreach(var a in c.World.ManualAnchors)
                {
                    if(a.Id=="west")a.Position=new Vector3(24,0,60);
                    if(a.Id=="east")a.Position=new Vector3(72,0,60);
                }
                Assert.That(s.Generate(),Is.True,c.Diagnostics);
                Assert.That(c.Paths.LastPlan.Routes.Single().BridgeCrossings,Is.Empty);
                Assert.That(c.ValidateCurrent(out var reason),Is.True,reason);
                c.Clear();
            });
        }

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
