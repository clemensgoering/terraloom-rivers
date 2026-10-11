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
