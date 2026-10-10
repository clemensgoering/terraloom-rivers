using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace TerraLoom.Integration.Tests
{
    public sealed class WaterfallPrototypeTests
    {
        [Test] public void SharedProfileDescendsAndPoolHasBoundedOutlet()
        {
            var p=new WaterfallLandscapePlan(4102026);float previous=p.Water(160);
            for(float z=159.9f;z>=0;z-=.1f)
            {
                Assert.That(p.Water(z),Is.LessThanOrEqualTo(previous));previous=p.Water(z);
                Assert.That(p.Ground(p.Centre(z),z),Is.LessThan(p.Water(z)-1));
                Assert.That(p.HalfWidth(z)/p.ChannelHalfWidth(z),Is.InRange(1,WaterfallLandscapePlan.PoolWidthRatio+.00001f));
            }
            Assert.That(p.HalfWidth(78)/p.ChannelHalfWidth(78),Is.EqualTo(1.35f).Within(.001));
            Assert.That(p.Water(78),Is.GreaterThan(p.Water(65)),"Pool has lower downstream outlet.");
        }
        [Test] public void SeedRepeatsReliefAndObservationIntentRemainsDry()
        {
            var a=new WaterfallLandscapePlan(4102026);var b=new WaterfallLandscapePlan(4102026);var other=new WaterfallLandscapePlan(73);
            Assert.That(a.Ground(35,92),Is.EqualTo(b.Ground(35,92)));Assert.That(a.Ground(35,92),Is.Not.EqualTo(other.Ground(35,92)));
            for(int i=0;i<3;i++){var o=a.Observation(i);Assert.That(a.Ground(o.x,o.y),Is.GreaterThan(a.Water(o.y)+.6f));}
        }
        [Test] public void OriginalRockHasOutwardNormalsAndTreeContainsIndividualLeaves()
        {
            var rock=PrototypeMeshRecipe.Rock(38);var tree=PrototypeMeshRecipe.Tree(4102026);
            try
            {
                var vertices=rock.vertices;var normals=rock.normals;int outward=0;
                for(int i=0;i<vertices.Length;i++)if(Vector3.Dot(vertices[i],normals[i])>0)outward++;
                Assert.That(outward,Is.GreaterThan(vertices.Length*.95f));
                Assert.That(tree.leaves.vertexCount,Is.GreaterThan(6000));Assert.That(tree.trunk.vertexCount,Is.GreaterThan(500));
            }
            finally{Object.DestroyImmediate(rock);Object.DestroyImmediate(tree.trunk);Object.DestroyImmediate(tree.leaves);}
        }
        [TestCase(false)][TestCase(true)] public void ConfigurationRebuildOwnsTransientTerrainAndKeepsSourceMaterials(bool comparison)
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/"+(comparison?"TerraLoomWaterfallComparison":"TerraLoomWaterfallPrototype")+".unity");
                var recipe=Object.FindFirstObjectByType<WaterfallLandscapePrototype>();Assert.That(recipe.Ground,Is.Null);
                var material=recipe.RockMaterial;var layer=recipe.GroundLayers[0];
                recipe.Generate();var first=recipe.Ground.terrainData;float sample=first.GetHeight(260,400);
                Assert.That(AssetDatabase.Contains(first),Is.False);Assert.That(recipe.transform.childCount,Is.EqualTo(1));
                recipe.Generate();Assert.That(first==null,Is.True,"Old transient terrain disposed during editor rebuild.");
                Assert.That(recipe.Ground.terrainData.GetHeight(260,400),Is.EqualTo(sample));
                Assert.That(recipe.transform.childCount,Is.EqualTo(1));Assert.That(recipe.RockMaterial,Is.SameAs(material));
                Assert.That(AssetDatabase.Contains(layer),Is.True);
                if(comparison)
                {
                    var profile=recipe.Plan.Profile;
                    var patch=WaterfallTerrainPatch.Measure(recipe.Ground);
                    Assert.That(patch.maximumColliderCentreError,Is.LessThan(.003f));
                    Assert.That(System.Convert.FromBase64String(patch.heightsBase64).Length,Is.EqualTo(225*225*2));
                    foreach(double station in new[]{0,profile.ImpactStation,profile.PoolStation,profile.OutletStation,18})
                    {
                        var samplePoint=profile.Sample(station);
                        float actual=recipe.Ground.SampleHeight(new Vector3((float)samplePoint.Surface.X,0,(float)samplePoint.Surface.Z));
                        Assert.That(actual,Is.EqualTo(samplePoint.Surface.Y-samplePoint.CentreDepth).Within(.003),"Final raster bed must retain pool scour and submerged outlet sill.");
                    }
                }
            }
            finally
            {
                if(System.Array.Exists(setup,s=>s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
        [Test] public void ConsumerUsesAnchoredContractForFlatPoolAndRealBedSill()
        {
            var plan=new WaterfallLandscapePlan(4102026,true);
            Assert.That(plan.Profile.Version,Is.EqualTo(TerraLoom.Rivers.WaterfallProfileVersion.AnchoredPoolV1));
            Assert.That(plan.Water(85),Is.EqualTo(plan.Water(81)));
            Assert.That(plan.Ground(80,85),Is.LessThan(plan.Ground(80,81)-.5f));
            Assert.That(plan.Water(75),Is.EqualTo(plan.Water(85)),"Receiving reach is level in the common recipe.");
            Assert.That(plan.HalfWidth(85)*2,Is.EqualTo(4.2f).Within(.00001));
        }
        [Test] public void BaselineObservedCameraTerrainHeightsRemainFrozen()
        {
            var plan=new WaterfallLandscapePlan(4102026);
            var a=plan.Observation(0);var b=plan.Observation(1);var c=plan.Observation(2);
            // Previous player used TerrainData interpolation, so small raster difference is expected.
            Assert.That(plan.Ground(a.x,a.y),Is.EqualTo(16.6997318f).Within(.002));
            Assert.That(plan.Ground(b.x,b.y),Is.EqualTo(10.5557747f).Within(.002));
            Assert.That(plan.Ground(c.x,c.y),Is.EqualTo(9.59434f).Within(.002));
        }
    }
}
