using System.Collections;
using System.Linq;
using NUnit.Framework;
using TerraLoom.Core.Unity;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace TerraLoom.Tests
{
    public sealed class CompositionHostLifecyclePlayTests
    {
        [UnityTest] public IEnumerator DestroyingActiveHostReleasesPublishedWorldAfterFrameEnd()
        {
            var host=new GameObject("Composition host lifecycle");host.SetActive(false);
            var world=host.AddComponent<TerraLoomWorld>();world.WorldId="host-lifecycle";
            var paths=host.AddComponent<TerraLoomPaths>();paths.World=world;paths.BridgeClearance=1.2f;paths.MaximumSlope=.35f;
            const string root="Assets/TerraLoom/Integration/Generated/";
            paths.GroundMaterial=AssetDatabase.LoadAssetAtPath<Material>(root+"Gravel.mat");
            paths.BridgeMaterial=AssetDatabase.LoadAssetAtPath<Material>(root+"Bridge.mat");
            var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;
            rivers.WaterMaterial=AssetDatabase.LoadAssetAtPath<Material>(root+"RegionalWater.mat");
            rivers.BankMaterial=AssetDatabase.LoadAssetAtPath<Material>(root+"RegionalBank.mat");
            rivers.BedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/TerraLoom/RiversSamples/Generated/Bed.mat");
            var composition=host.AddComponent<TerraLoom.Integration.TerraLoomIntegration>();
            composition.World=world;composition.Paths=paths;composition.Rivers=rivers;
            var decoration=host.AddComponent<RegionDecoration>();decoration.World=world;
            var demo=host.AddComponent<TerraLoom.Integration.SeededWorldDemo>();
            demo.Composition=composition;demo.Decoration=decoration;demo.Seed=2042;demo.ShowControls=false;
            demo.Profiles=new[]{"summer-assets","winter-assets","summer-shader","winter-shader"}
                .Select(id=>AssetDatabase.LoadAssetAtPath<RegionalStyleProfile>(
                    "Assets/TerraLoom/SeasonalSamples/Profiles/"+id+".asset")).ToArray();
            host.SetActive(true);yield return null;
            try
            {
                Assert.That(paths.GeneratedRoot&&rivers.GeneratedRoot&&decoration.GeneratedRoot,Is.True,demo.Diagnostics);
                var source=rivers.OriginalTerrainData;var carved=world.Terrain.terrainData;
                var pathRoot=paths.GeneratedRoot;var riverRoot=rivers.GeneratedRoot;var natureRoot=decoration.GeneratedRoot;
                Object.Destroy(composition);
                yield return null;yield return null;
                Assert.That(paths.GeneratedRoot==null&&rivers.GeneratedRoot==null&&decoration.GeneratedRoot==null,Is.True);
                Assert.That(pathRoot==null&&riverRoot==null&&natureRoot==null,Is.True);
                Assert.That(carved==null,Is.True,"Published carve must be released.");
                Assert.That(source==null||world.Terrain.terrainData==source,Is.True,
                    "The source may survive only if it is the Core-owned original restored after Clear.");
            }
            finally{Object.Destroy(host);}
            yield return null;
        }
    }
}
