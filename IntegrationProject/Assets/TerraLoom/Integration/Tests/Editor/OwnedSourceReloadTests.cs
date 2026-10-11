using System;
using System.Linq;
using NUnit.Framework;
using TerraLoom.Integration;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TerraLoom.Tests
{
    public sealed class OwnedSourceReloadTests
    {
        [Test] public void ChangedSeedFailureRetainsCompleteWorldThenRetryPublishesDecoration()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomDynamic.unity");
                var recipe=UnityEngine.Object.FindFirstObjectByType<SeededWorldDemo>();
                recipe.Seed=2042;Assert.That(recipe.Rebuild(),Is.True,recipe.Diagnostics);
                var c=recipe.Composition;var world=c.World;var decoration=recipe.Decoration;
                var terrain=world.Terrain.terrainData;var collider=world.Terrain.GetComponent<TerrainCollider>().terrainData;
                var river=c.Rivers.GeneratedRoot;var path=c.Paths.GeneratedRoot;var nature=decoration.GeneratedRoot;
                var anchors=world.ManualAnchors;var areas=world.Areas;
                var riverConnections=c.Rivers.Connections;var pathConnections=c.Paths.Connections;
                int seed=world.Seed;long revision=world.Revision;var coreRecipe=world.Recipe;
                recipe.Seed=2043;
                c.PublicationBoundary=stage=>{if(stage=="before-commit")throw new InvalidOperationException("Injected demo failure");};
                Assert.That(recipe.Rebuild(),Is.False);
                c.PublicationBoundary=null;
                Assert.That(world.Seed,Is.EqualTo(seed));Assert.That(world.Revision,Is.EqualTo(revision));
                Assert.That(world.Recipe,Is.EqualTo(coreRecipe));
                Assert.That(world.ManualAnchors,Is.SameAs(anchors));Assert.That(world.Areas,Is.SameAs(areas));
                Assert.That(c.Rivers.Connections,Is.SameAs(riverConnections));Assert.That(c.Paths.Connections,Is.SameAs(pathConnections));
                Assert.That(world.Terrain.terrainData,Is.SameAs(terrain));
                Assert.That(world.Terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(collider));
                Assert.That(c.Rivers.GeneratedRoot,Is.SameAs(river));Assert.That(c.Paths.GeneratedRoot,Is.SameAs(path));
                Assert.That(decoration.GeneratedRoot,Is.SameAs(nature));Assert.That(nature.activeSelf,Is.True);
                Assert.That(c.ValidateCurrent(out var reason),Is.True,reason);
                Assert.That(recipe.Rebuild(),Is.True,recipe.Diagnostics);
                Assert.That(world.Seed,Is.EqualTo(2043));
                Assert.That(world.Terrain.terrainData,Is.Not.SameAs(terrain));
                Assert.That(c.Rivers.GeneratedRoot,Is.Not.SameAs(river));
                Assert.That(c.Paths.GeneratedRoot,Is.Not.SameAs(path));
                Assert.That(decoration.GeneratedRoot,Is.Not.SameAs(nature));
                Assert.That(decoration.PlacedCount,Is.GreaterThan(0));
                Assert.That(c.ValidateCurrent(out reason),Is.True,reason);
            }
            finally
            {
                if(setup.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
        [Test] public void SuppliedTerrainCannotBeReplacedByCoreOwnedRebuild()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomIntegration.unity");
                var composition=UnityEngine.Object.FindFirstObjectByType<TerraLoomIntegration>();
                Assert.That(composition,Is.Not.Null);
                Assert.That(composition.World.OwnsTerrain,Is.False);
                var original=composition.Rivers.OriginalTerrainData;
                var savedCarve=composition.Rivers.CarvedTerrainData;
                var live=composition.World.Terrain.terrainData;
                Assert.Throws<InvalidOperationException>(()=>composition.GenerateOwnedSource());
                using(var request=new PreparedSeededWorldRequest(UnityEngine.Object.Instantiate(original),composition.World.Seed,
                    composition.World.Recipe,original.heightmapResolution,original.size,composition.World.Frequency,
                    composition.World.ManualAnchors,composition.World.Areas,Array.Empty<TerraLoom.Rivers.Unity.RiverConnectionSettings>(),
                    Array.Empty<TerraLoom.Paths.Unity.PathConnectionSettings>(),Array.Empty<TerraLoom.Core.Unity.RegionalStyleProfile>(),
                    1,TerraLoom.Core.Unity.RegionalProfileSelection.PriorityWinner,0,null))
                    Assert.Throws<InvalidOperationException>(()=>composition.GenerateSeedRequest(request));
                Assert.That(composition.Rivers.OriginalTerrainData,Is.SameAs(original));
                Assert.That(composition.Rivers.CarvedTerrainData,Is.SameAs(savedCarve));
                Assert.That(composition.World.Terrain.terrainData,Is.SameAs(live));
            }
            finally
            {
                if(setup.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
        [Test] public void OwnedSourceRebuildRetainsLastWorldOnFailureAndDoesNotAccumulateCarve()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomDynamic.unity");
                var recipe=UnityEngine.Object.FindFirstObjectByType<SeededWorldDemo>();
                Assert.That(recipe,Is.Not.Null);
                recipe.Seed=2042;
                Assert.That(recipe.Rebuild(),Is.True,recipe.Diagnostics);
                var composition=recipe.Composition;
                Assert.That(composition.GenerateOwnedSource(),Is.True,composition.Diagnostics);
                string sourceDigest=composition.World.CaptureTerrainFingerprint(composition.Rivers.OriginalTerrainData);
                string carveDigest=composition.World.CaptureTerrainFingerprint(composition.Rivers.CarvedTerrainData);
                var source=composition.Rivers.OriginalTerrainData;
                var carve=composition.Rivers.CarvedTerrainData;
                var pathRoot=composition.Paths.GeneratedRoot;
                var riverRoot=composition.Rivers.GeneratedRoot;
                var published=composition.PublishedSharedInput;
                var publishedSeed=composition.World.Seed;
                composition.PublicationBoundary=stage=>
                {if(stage=="source-prepared")throw new InvalidOperationException("Injected source failure");};
                Assert.That(composition.GenerateOwnedSource(2043),Is.False);
                composition.PublicationBoundary=null;
                Assert.That(composition.World.Seed,Is.EqualTo(publishedSeed));
                Assert.That(composition.PublishedSharedInput.Identity.Seed,Is.EqualTo(publishedSeed));
                Assert.That(composition.Rivers.OriginalTerrainData,Is.SameAs(source));
                Assert.That(composition.Rivers.CarvedTerrainData,Is.SameAs(carve));
                Assert.That(composition.World.Terrain.terrainData,Is.SameAs(carve));
                Assert.That(composition.World.Terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(carve));
                Assert.That(composition.Paths.GeneratedRoot,Is.SameAs(pathRoot));
                Assert.That(composition.Rivers.GeneratedRoot,Is.SameAs(riverRoot));
                Assert.That(composition.PublishedSharedInput,Is.SameAs(published));
                Assert.That(composition.ValidateCurrent(out var retainedReason),Is.True,retainedReason);
                composition.PublicationBoundary=stage=>
                {if(stage=="before-commit")throw new InvalidOperationException("Injected late failure");};
                Assert.That(composition.GenerateOwnedSource(),Is.False);
                composition.PublicationBoundary=null;
                Assert.That(composition.LastSharedInput,Is.Not.SameAs(published));
                Assert.That(composition.PublishedSharedInput,Is.SameAs(published));
                Assert.That(composition.ValidateCurrent(out retainedReason),Is.True,retainedReason);
                Assert.That(composition.GenerateOwnedSource(),Is.True,composition.Diagnostics);
                Assert.That(composition.World.CaptureTerrainFingerprint(composition.Rivers.OriginalTerrainData),Is.EqualTo(sourceDigest));
                Assert.That(composition.World.CaptureTerrainFingerprint(composition.Rivers.CarvedTerrainData),Is.EqualTo(carveDigest));
                Assert.That(composition.ValidateCurrent(out var reason),Is.True,reason);
                composition.Clear();
            }
            finally
            {
                if(setup.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
    }
}
