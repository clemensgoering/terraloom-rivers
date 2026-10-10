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
        [Test] public void ConfigurationRebuildOwnsTransientTerrainAndKeepsSourceMaterials()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomWaterfallPrototype.unity");
                var recipe=Object.FindFirstObjectByType<WaterfallLandscapePrototype>();Assert.That(recipe.Ground,Is.Null);
                var material=recipe.RockMaterial;var layer=recipe.GroundLayers[0];
                recipe.Generate();var first=recipe.Ground.terrainData;float sample=first.GetHeight(260,400);
                Assert.That(AssetDatabase.Contains(first),Is.False);Assert.That(recipe.transform.childCount,Is.EqualTo(1));
                recipe.Generate();Assert.That(first==null,Is.True,"Old transient terrain disposed during editor rebuild.");
                Assert.That(recipe.Ground.terrainData.GetHeight(260,400),Is.EqualTo(sample));
                Assert.That(recipe.transform.childCount,Is.EqualTo(1));Assert.That(recipe.RockMaterial,Is.SameAs(material));
                Assert.That(AssetDatabase.Contains(layer),Is.True);
            }
            finally
            {
                if(System.Array.Exists(setup,s=>s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
    }
}
