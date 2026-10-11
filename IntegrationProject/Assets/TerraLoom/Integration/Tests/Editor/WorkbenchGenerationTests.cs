using System.Reflection;
using System.Linq;
using NUnit.Framework;
using TerraLoom.Core.Editor;
using TerraLoom.Core.Unity;
using TerraLoom.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TerraLoom.Tests
{
    public sealed class WorkbenchGenerationTests
    {
        [Test] public void WorldActionRebuildsOwnedSourceAfterRuntimeRecipeIsRemoved()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            TerraLoomWorldWindow window=null;
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomDynamic.unity");
                var recipe=Object.FindFirstObjectByType<SeededWorldDemo>();
                Assert.That(recipe.Rebuild(),Is.True,recipe.Diagnostics);
                var composition=recipe.Composition;
                var world=composition.World;
                Assert.That(world.OwnsTerrain,Is.True);
                Object.DestroyImmediate(recipe);
                window=ScriptableObject.CreateInstance<TerraLoomWorldWindow>();
                typeof(TerraLoomWorldWindow).GetField("world",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,world);
                typeof(TerraLoomWorldWindow).GetMethod("GenerateWorld",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
                Assert.That(composition.ValidateCurrent(out var reason),Is.True,reason);
                Assert.That(composition.Rivers.OriginalTerrainData,Is.Not.SameAs(composition.Rivers.CarvedTerrainData));
            }
            finally
            {
                if(window)Object.DestroyImmediate(window);
                if(setup.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
        [Test] public void WorldActionUsesSharedCompositionAndRefreshesDecoration()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            TerraLoomWorldWindow window=null;
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomIntegration.unity");
                var composition=Object.FindFirstObjectByType<TerraLoomIntegration>();
                Assert.That(composition,Is.Not.Null);
                var world=composition.World;
                var decoration=world.GetComponent<RegionDecoration>();
                Assert.That(decoration,Is.Not.Null);
                window=ScriptableObject.CreateInstance<TerraLoomWorldWindow>();
                typeof(TerraLoomWorldWindow).GetField("world",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,world);
                typeof(TerraLoomWorldWindow).GetMethod("GenerateWorld",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
                Assert.That(composition.ValidateCurrent(out var reason),Is.True,reason);
                Assert.That(composition.Paths.GeneratedRoot,Is.Not.Null);
                Assert.That(composition.Rivers.GeneratedRoot,Is.Not.Null);
                Assert.That(decoration.PlacedCount,Is.GreaterThan(0));
            }
            finally
            {
                if(window)Object.DestroyImmediate(window);
                if(setup.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
    }
}
