#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TerraLoom.Core.Unity;
using TerraLoom.Paths;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TerraLoom.Tests
{
    public sealed class SeededWorldRuntimeTests
    {
        [UnityTest] public IEnumerator EntireWorldBuildsAtStartRepeatsSeedAndRegeneratesWithoutEditorAlgorithms()
        {
            var host=new GameObject("Runtime seed recipe test");host.SetActive(false);
            var world=host.AddComponent<TerraLoomWorld>();world.WorldId="dynamic-test";world.AutoGenerateOnStart=true;
            var paths=host.AddComponent<TerraLoomPaths>();paths.World=world;paths.BridgeClearance=1.2f;paths.MaximumSlope=.35f;
            const string root="Assets/TerraLoom/Integration/Generated/";
            paths.GroundMaterial=AssetDatabase.LoadAssetAtPath<Material>(root+"Gravel.mat");paths.BridgeMaterial=AssetDatabase.LoadAssetAtPath<Material>(root+"Bridge.mat");
            var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;
            rivers.WaterMaterial=AssetDatabase.LoadAssetAtPath<Material>(root+"RegionalWater.mat");rivers.BankMaterial=AssetDatabase.LoadAssetAtPath<Material>(root+"RegionalBank.mat");
            rivers.BedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/TerraLoom/RiversSamples/Generated/Bed.mat");
            var composition=host.AddComponent<TerraLoom.Integration.TerraLoomIntegration>();composition.World=world;composition.Paths=paths;composition.Rivers=rivers;
            var decoration=host.AddComponent<RegionDecoration>();decoration.World=world;
            var profiles=new[] {"summer-assets","winter-assets","summer-shader","winter-shader"}.Select(id=>AssetDatabase.LoadAssetAtPath<RegionalStyleProfile>("Assets/TerraLoom/SeasonalSamples/Profiles/"+id+".asset")).ToArray();
            var recipe=host.AddComponent<TerraLoom.Integration.SeededWorldDemo>();recipe.Composition=composition;recipe.Decoration=decoration;recipe.Profiles=profiles;recipe.Seed=2042;recipe.ShowControls=false;
            try
            {
                Assert.That(world.Terrain,Is.Null);Assert.That(paths.GeneratedRoot,Is.Null);Assert.That(rivers.GeneratedRoot,Is.Null);
                int terrainBaseline=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).Length;
                host.SetActive(true);yield return null;var ownedTerrain=world.Terrain;
                Assert.That(world.AutoGenerateOnStart,Is.False,"Composition must own startup before independent Start hooks run.");
                Assert.That(paths.GeneratedRoot,Is.Not.Null,recipe.Diagnostics);Assert.That(rivers.GeneratedRoot,Is.Not.Null,recipe.Diagnostics);
                string digest=world.TerrainContentFingerprint;var points=paths.LastPlan.Routes.Single().Waypoints.ToArray();
                var riverPoints=rivers.LastPlan.Routes.Single().WaterPolyline.ToArray();
                var firstPositions=decoration.GeneratedRoot.GetComponentsInChildren<RegionalDecorationInstance>().Select(p=>p.transform.position).ToArray();
                Assert.That(recipe.Rebuild(),Is.True,recipe.Diagnostics);yield return null;
                Assert.That(world.TerrainContentFingerprint,Is.EqualTo(digest));Assert.That(paths.LastPlan.Routes.Single().Waypoints,Is.EqualTo(points));
                Assert.That(rivers.LastPlan.Routes.Single().WaterPolyline,Is.EqualTo(riverPoints));
                Assert.That(decoration.GeneratedRoot.GetComponentsInChildren<RegionalDecorationInstance>().Select(p=>p.transform.position),Is.EqualTo(firstPositions));
                foreach(int seed in new[] {2043,71,-10})
                {
                    recipe.Seed=seed;Assert.That(recipe.Rebuild(),Is.True,recipe.Diagnostics);yield return new WaitForFixedUpdate();
                    Assert.That(world.TerrainContentFingerprint,Is.Not.EqualTo(digest));Assert.That(paths.LastPlan.Routes.Single().Surfaces.Contains(PathSurface.Bridge),Is.True);
                    Assert.That(decoration.PlacedCount,Is.GreaterThan(250));
                    var markers=decoration.GeneratedRoot.GetComponentsInChildren<RegionalDecorationInstance>();
                    Assert.That(markers.Select(m=>m.RegionId).Distinct().Count(),Is.EqualTo(4));
                    foreach(var marker in markers)
                    {
                        var p=new Vector2(marker.transform.position.x,marker.transform.position.z);
                        Assert.That(decoration.Exclusions.Any(r=>r.Contains(p,true)),Is.False,"Nature collider inside path/water reservation.");
                    }
                    var route=paths.LastPlan.Routes.Single();
                    foreach(var point in route.Waypoints)
                    {
                        Assert.That(Physics.Raycast(new Vector3((float)point.X,(float)point.Y+2,(float)point.Z),Vector3.down,out var hit,5),Is.True);
                        Assert.That(hit.collider.GetComponent<PathGeneratedGeometry>(),Is.Not.Null,"A tree or rock replaced path coverage.");
                    }
                    Assert.That(world.Terrain,Is.SameAs(ownedTerrain));
                    Assert.That(Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).Length,Is.EqualTo(terrainBaseline+1));
                    Assert.That(host.GetComponentsInChildren<RegionDecoration>().Length,Is.EqualTo(1));
                }
                // Manual mode uses the very same components, target IDs and planners.
                recipe.UseSeedConfiguration=false;var manual=world.ManualAnchors.Select(a=>a.Position).ToArray();
                Assert.That(recipe.Rebuild(),Is.True,recipe.Diagnostics);Assert.That(world.ManualAnchors.Select(a=>a.Position),Is.EqualTo(manual));
            }
            finally{Object.Destroy(host);}
            yield return null;yield return null;
        }
    }
}
#endif
