using System.Linq;
using NUnit.Framework;
using TerraLoom.Core.Unity;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEngine;

namespace TerraLoom.Tests
{
    public sealed class PreparedSeedAnchorAdapterTests
    {
        [Test] public void BothOptionalModulesCaptureTheRequestedSeedWithoutPublishingIt()
        {
            var host=new GameObject("prepared seed adapters");
            var source=new TerrainData{heightmapResolution=33,size=new Vector3(32,8,32)};
            var terrainObject=Terrain.CreateTerrainGameObject(source);
            try
            {
                var world=host.AddComponent<TerraLoomWorld>();
                world.Terrain=terrainObject.GetComponent<Terrain>();
                world.Resolution=33;world.TerrainSize=new Vector3(32,8,32);
                world.Recipe=TerrainRecipe.DrainageValley;
                world.Seed=42;world.UseSeedAnchors=true;world.SeedAnchorCount=4;world.AnchorMargin=4;
                world.ManualAnchors=new[]{new ManualWorldAnchor{Id="authored",Position=new Vector3(1,0,1)}};
                var publishedArea=new LandscapeArea{Id="summer",Center=new Vector2(16,16),Size=new Vector2(32,32)};
                world.Areas=new[]{publishedArea};
                var stagedAreas=new[]{new LandscapeArea{Id="winter",Center=new Vector2(16,16),Size=new Vector2(32,32)}};
                var paths=host.AddComponent<TerraLoomPaths>();paths.World=world;
                var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;
                using(var publisher=new WorldSourcePublication(world))
                using(var prepared=publisher.PrepareFreshSource(43,true,world.ManualAnchors,stagedAreas,
                    ()=>world.CreateGeneratedTerrainData(43)))
                {
                    var expected=world.CaptureAnchors(prepared.Data,43,true,world.ManualAnchors).Anchors;
                    var pathInput=paths.CaptureInput(paths.CaptureProfile(),out _,out _,prepared);
                    var riverInput=rivers.CaptureInput(out _,out _,prepared);
                    Assert.That(pathInput.Anchors.Count,Is.EqualTo(expected.Count));
                    Assert.That(riverInput.Anchors.Count,Is.EqualTo(expected.Count));
                    for(int i=0;i<expected.Count;i++)
                    {
                        Assert.That(pathInput.Anchors[i].Id,Is.EqualTo(expected[i].Id));
                        Assert.That(pathInput.Anchors[i].Position,Is.EqualTo(expected[i].Position));
                        Assert.That(riverInput.Anchors[i].Id,Is.EqualTo(expected[i].Id));
                        Assert.That(riverInput.Anchors[i].Position,Is.EqualTo(expected[i].Position));
                    }
                    Assert.That(world.Seed,Is.EqualTo(42));
                    Assert.That(pathInput.Landscape.Any(p=>p.Regions.GetWeight("winter")>0),Is.True);
                    Assert.That(riverInput.Landscape.Any(p=>p.Regions.GetWeight("winter")>0),Is.True);
                    Assert.That(world.Areas[0],Is.SameAs(publishedArea));
                    Assert.That(world.UseSeedAnchors,Is.True);
                    Assert.That(world.Terrain.terrainData,Is.SameAs(source));
                    Assert.That(world.Terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(source));
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(terrainObject);
                Object.DestroyImmediate(source);
            }
        }
    }
}
