#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Core.Unity;
using TerraLoom.Integration;
using TerraLoom.Paths;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TerraLoom.Tests
{
    public sealed class LandscapeRuntimeTests
    {
        [UnityTest] public IEnumerator LandscapeConfigurationBuildsSeededReliefBranchesAndHabitatThroughOrderedRuntimeStages()
        {
            const string path="Assets/TerraLoom/Integration/Generated/TerraLoomLandscape.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Additive));
            var scene=SceneManager.GetSceneByPath(path);
            try
            {
                var recipe=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SeededWorldDemo>()).Single();
                recipe.ShowControls=false;Assert.That(recipe.LandscapeMode,Is.True);
                foreach(int seed in new[]{2042,2043,71})
                {
                    recipe.Seed=seed;Assert.That(recipe.Rebuild(),Is.True,recipe.Diagnostics);yield return new WaitForFixedUpdate();
                    Assert.That(recipe.GenerationState,Is.EqualTo(GenerationRunState.Ready));
                    Assert.That(recipe.Composition.ValidateCurrent(out var current),Is.True,current);
                    var routes=recipe.Composition.Paths.LastPlan.Routes;
                    Assert.That(routes,Has.Count.EqualTo(4));Assert.That(routes.Any(r=>r.Connection.Id=="hill-trail"),Is.True);
                    Assert.That(routes.Any(r=>r.Surfaces.Contains(PathSurface.Bridge)),Is.True);
                    foreach(var route in routes)AssertNoRasterCorners(route.Waypoints,route.Connection.Id);
                    var water=recipe.Composition.Rivers.LastPlan.Routes.Single().WaterPolyline;
                    AssertNoRasterCorners(water,"river");
                    for(int i=1;i<water.Count;i++)Assert.That(water[i].Y,Is.LessThanOrEqualTo(water[i-1].Y),"Final river rises after rounding.");
                    Assert.That(recipe.Decoration.PlacedCount,Is.GreaterThan(250));Assert.That(recipe.Decoration.RejectedCount,Is.GreaterThan(0));
                    Assert.That(recipe.Decoration.Profiles.All(p=>p.UseHabitat),Is.True);
                    Assert.That(recipe.Decoration.GeneratedRoot.GetComponentsInChildren<RegionalDecorationInstance>().All(m=>m.HabitatApplied),Is.True);
                    var terrain=recipe.Composition.World.Terrain;float z=terrain.transform.position.z+48;
                    var floor=terrain.SampleHeight(new Vector3(48,0,z));
                    Assert.That(terrain.SampleHeight(new Vector3(0,0,z))-floor,Is.GreaterThan(5));
                    var points=routes.SelectMany(r=>r.Waypoints).ToArray();
                    var placed=recipe.Decoration.GeneratedRoot.GetComponentsInChildren<RegionalDecorationInstance>().Select(m=>m.transform.position).ToArray();
                    Assert.That(recipe.Rebuild(),Is.True,recipe.Diagnostics);
                    Assert.That(recipe.Composition.Paths.LastPlan.Routes.SelectMany(r=>r.Waypoints),Is.EqualTo(points));
                    Assert.That(recipe.Decoration.GeneratedRoot.GetComponentsInChildren<RegionalDecorationInstance>().Select(m=>m.transform.position),Is.EqualTo(placed));
                }
            }
            finally{if(scene.IsValid())SceneManager.UnloadSceneAsync(scene);}
            yield return null;yield return null;
        }
        private static void AssertNoRasterCorners(System.Collections.Generic.IReadOnlyList<WorldPoint> points,string name)
        {
            Assert.That(PlanarCurve.IsSimple(points),Is.True,name+" final centreline self-intersects.");
            for(int i=1;i+1<points.Count;i++)
            {
                var a=points[i-1];var b=points[i];var c=points[i+1];
                double ux=b.X-a.X,uz=b.Z-a.Z,vx=c.X-b.X,vz=c.Z-b.Z;
                double product=System.Math.Sqrt((ux*ux+uz*uz)*(vx*vx+vz*vz));
                Assert.That(product,Is.GreaterThan(1e-12),name+" duplicate samples.");
                Assert.That((ux*vx+uz*vz)/product,Is.GreaterThanOrEqualTo(System.Math.Cos(15*System.Math.PI/180)-1e-9),name+" retained a raster corner.");
            }
        }
    }
}
#endif
