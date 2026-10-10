using System.Linq;
using NUnit.Framework;
using TerraLoom.Core.Unity;
using TerraLoom.Integration;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Tests
{
    /// <summary>Preserves a rejected general terrain/endpoints recipe separately from the analytic positive examples.</summary>
    public sealed class HydraulicSeedBoundaryTests
    {
        [Test] public void IndependentSeedEndpointsDoNotImplyAHydraulicallyValidProtectedBrushRoute()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();Terrain generated=null;TerrainData owned=null;
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomFrozenTruss.unity");
                var c=Object.FindFirstObjectByType<FrozenCurvedCrossing>().Composition;var world=c.World;
                var original=world.Terrain;world.Seed=2043;world.Recipe=TerrainRecipe.DrainageValley;
                world.Resolution=129;world.TerrainSize=new Vector3(96,16,96);world.Frequency=4;
                world.GeneratedLayers=original.terrainData.terrainLayers.Take(2).ToArray();world.UseSeedAnchors=false;world.GenerateTerrain();
                generated=world.Terrain;owned=generated.terrainData;
                float sourceX=40+2*(Hash(world.Seed,0)%9),mouthX=40+2*(Hash(world.Seed,1)%9);
                float roadZ=56+2*(Hash(world.Seed,2)%5);
                world.ManualAnchors=new[]{Anchor("source",sourceX,8),Anchor("mouth",mouthX,88),Anchor("west",24,roadZ-2),Anchor("east",72,roadZ+2)};
                var collider=generated.GetComponent<TerrainCollider>();var fingerprint=world.TerrainContentFingerprint;
                Assert.That(c.Generate(),Is.False,"Downhill along +Z does not grant every transverse route or protected detour.");
                Assert.That(c.ActiveStep,Is.EqualTo("02-plan-water"));
                Assert.That(c.Diagnostics,Does.Contain("NoRoute"));Assert.That(c.Diagnostics,Does.Contain("Hydraulic dead end"));
                Assert.That(c.LastPathAttempt,Is.Null);Assert.That(c.LastSharedInput,Is.Null);
                Assert.That(c.Paths.GeneratedRoot==null&&c.Rivers.GeneratedRoot==null,Is.True);
                Assert.That(c.Rivers.CarvedTerrainData,Is.Null);
                Assert.That(generated.terrainData,Is.SameAs(owned));Assert.That(collider.terrainData,Is.SameAs(owned));
                Assert.That(world.TerrainContentFingerprint,Is.EqualTo(fingerprint));
                string diagnostic=c.Diagnostics;Assert.That(c.Generate(),Is.False);Assert.That(c.Diagnostics,Is.EqualTo(diagnostic));
                Assert.That(world.TerrainContentFingerprint,Is.EqualTo(fingerprint));
                TestContext.WriteLine("TERRALOOM_GENERAL_SEED_HYDRAULIC_NEGATIVE seed=2043 sourceX="+sourceX+" mouthX="+mouthX+" "+diagnostic);
                c.Clear();world.Terrain=original;
            }
            finally
            {
                if(generated)Object.DestroyImmediate(generated.gameObject);if(owned)Object.DestroyImmediate(owned);
                if(setup.Any(s=>s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
        private static ManualWorldAnchor Anchor(string id,float x,float z)=>new ManualWorldAnchor{Id=id,Position=new Vector3(x,0,z)};
        private static uint Hash(int seed,uint stream)
        {unchecked{uint value=(uint)seed^(stream+1)*0x9e3779b9U;value^=value>>16;value*=0x7feb352dU;value^=value>>15;value*=0x846ca68bU;return value^(value>>16);}}
    }
}
