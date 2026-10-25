using System;
using System.Linq;
using NUnit.Framework;
using TerraLoom.Integration;
using TerraLoom.Integration.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Tests
{
    public sealed class CompositionRecipeEditorTests
    {
        [Test] public void SuppliedRecipeStagesDecorationAndRejectsConcurrentRecipeEdits()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            CompositionRecipe recipe=null;
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomIntegration.unity");
                var c=Object.FindFirstObjectByType<TerraLoomIntegration>();
                var decoration=c.World.GetComponent<TerraLoom.Core.Unity.RegionDecoration>();
                Assert.That(c.Generate(),Is.True,c.Diagnostics);
                recipe=ScriptableObject.CreateInstance<CompositionRecipe>();recipe.CaptureFrom(c,decoration);
                Assert.That(recipe.Settings.TerrainSource,Is.EqualTo(CompositionTerrainSource.Supplied));
                Assert.That(c.GenerateRecipe(recipe,decoration),Is.True,c.Diagnostics);
                Assert.That(decoration.GeneratedRoot,Is.Not.Null);Assert.That(decoration.PlacedCount,Is.GreaterThan(0));
                var oldData=c.World.Terrain.terrainData;var oldRiver=c.Rivers.GeneratedRoot;var oldPath=c.Paths.GeneratedRoot;
                var oldQuery=c.Rivers.WaterSurfaceQuery;var oldNature=decoration.GeneratedRoot;int oldSeed=c.World.Seed;
                foreach(var stage in new[]{"decoration-prepared","all-bound"})
                {
                    int seed=recipe.Settings.Seed;
                    c.PublicationBoundary=boundary=>{if(boundary==stage)recipe.Settings.Seed++;};
                    Assert.That(c.GenerateRecipe(recipe,decoration),Is.False);
                    StringAssert.Contains("Recipe inputs changed",c.Diagnostics);
                    c.PublicationBoundary=null;
                    Assert.That(c.World.Seed,Is.EqualTo(oldSeed));
                    Assert.That(c.World.Terrain.terrainData,Is.SameAs(oldData));
                    Assert.That(c.World.Terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(oldData));
                    Assert.That(c.Rivers.GeneratedRoot,Is.SameAs(oldRiver));Assert.That(c.Paths.GeneratedRoot,Is.SameAs(oldPath));
                    Assert.That(c.Rivers.WaterSurfaceQuery,Is.SameAs(oldQuery));Assert.That(decoration.GeneratedRoot,Is.SameAs(oldNature));
                    Assert.That(c.ValidateCurrent(out var reason),Is.True,reason);
                    recipe.Settings.Seed=seed;
                }
                // A failed seed switch restores the published identity as well as decoration.
                recipe.Settings.Seed++;
                c.PublicationBoundary=stage=>{if(stage=="before-commit")throw new InvalidOperationException("Seed switch rollback");};
                Assert.That(c.GenerateRecipe(recipe,decoration),Is.False);
                StringAssert.Contains("Seed switch rollback",c.Diagnostics);
                c.PublicationBoundary=null;
                Assert.That(c.World.Seed,Is.EqualTo(oldSeed));Assert.That(decoration.GeneratedRoot,Is.SameAs(oldNature));
                Assert.That(c.GenerateRecipe(recipe,decoration),Is.True,c.Diagnostics);
                Assert.That(c.World.Seed,Is.EqualTo(recipe.Settings.Seed));
                Assert.That(c.ValidateCurrent(out var current),Is.True,current);
                var previousNature=decoration.GeneratedRoot;
                recipe.Settings.DecorationCount=0;
                recipe.Settings.DecorationProfiles=Array.Empty<TerraLoom.Core.Unity.RegionalStyleProfile>();
                c.PublicationBoundary=stage=>{if(stage=="before-commit")throw new InvalidOperationException("Empty decoration rollback");};
                Assert.That(c.GenerateRecipe(recipe,decoration),Is.False);
                StringAssert.Contains("Empty decoration rollback",c.Diagnostics);
                c.PublicationBoundary=null;
                Assert.That(decoration.GeneratedRoot,Is.SameAs(previousNature));
                Assert.That(decoration.PlacedCount,Is.GreaterThan(0));
                Assert.That(c.GenerateRecipe(recipe,decoration),Is.True,c.Diagnostics);
                Assert.That(decoration.PlacedCount,Is.Zero);Assert.That(decoration.Count,Is.Zero);
                Assert.That(previousNature==null,Is.True,"Old decoration retires only after the complete empty publication commits.");
                Assert.That(c.ValidateCurrent(out current),Is.True,current);
            }
            finally
            {
                if(recipe)Object.DestroyImmediate(recipe);
                if(setup.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }

        [Test] public void ConfiguredBuildRejectsCompetingRecipesBeforeMutation()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            CompositionRecipe saved=null;
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomDynamic.unity");
                var demo=Object.FindFirstObjectByType<SeededWorldDemo>();
                Assert.That(demo.Rebuild(),Is.True,demo.Diagnostics);
                var c=demo.Composition;
                saved=ScriptableObject.CreateInstance<CompositionRecipe>();saved.CaptureFrom(c,demo.Decoration);
                c.RecipeAsset=saved;c.RecipeDecoration=demo.Decoration;
                var data=c.World.Terrain.terrainData;var river=c.Rivers.GeneratedRoot;var path=c.Paths.GeneratedRoot;
                var query=c.Rivers.WaterSurfaceQuery;
                c.InputSource=CompositionInputSource.Automatic;
                Assert.That(c.RebuildConfiguredWorld(),Is.False);
                StringAssert.Contains("both present",c.Diagnostics);
                Assert.That(c.World.Terrain.terrainData,Is.SameAs(data));
                Assert.That(c.Rivers.GeneratedRoot,Is.SameAs(river));Assert.That(c.Paths.GeneratedRoot,Is.SameAs(path));
                Assert.That(c.Rivers.WaterSurfaceQuery,Is.SameAs(query));
                Assert.That(c.ValidateCurrent(out var reason),Is.True,reason);
                var workbench=new CompositionWorldEditorExtension();
                Assert.Throws<InvalidOperationException>(()=>workbench.PreparesTerrain(c));
                c.InputSource=CompositionInputSource.SavedRecipe;
                Assert.That(workbench.PreparesTerrain(c),Is.True);
                float width=saved.Settings.Modules.Paths.Width;c.Paths.Width=9;
                workbench.Generate(c);
                Assert.That(c.Paths.Width,Is.EqualTo(width));
                Assert.That(c.ValidateCurrent(out reason),Is.True,reason);
                saved.Settings.Modules.Paths.Width+=.25f;
                Assert.That(c.ValidateCurrent(out reason),Is.False,"An edited authoritative asset must not leave the old output certified current.");
                StringAssert.Contains("Authoring recipe",reason);
                saved.Settings.Modules.Paths.Width=width;
                Assert.That(c.ValidateCurrent(out reason),Is.True,reason);
                c.InputSource=CompositionInputSource.SceneRecipe;
                Assert.That(c.ValidateCurrent(out reason),Is.False,"Selecting another source must invalidate its previous receipt.");
                Assert.That(c.ConfiguredSceneRecipe,Is.SameAs(demo));
                c.RecipeAsset=null;c.InputSource=CompositionInputSource.SavedRecipe;
                data=c.World.Terrain.terrainData;
                Assert.That(c.RebuildConfiguredWorld(),Is.False);
                StringAssert.Contains("assign",c.Diagnostics);
                Assert.That(c.World.Terrain.terrainData,Is.SameAs(data));
            }
            finally
            {
                if(saved)Object.DestroyImmediate(saved);
                if(setup.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }

        [Test] public void PackageInspectorCapturesNewAssetAndGeneratesFromSavedSettings()
        {
            const string folder="Assets/TerraLoomRecipeEditorConsumerTest";
            const string path=folder+"/WorldRecipe.asset";
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomDynamic.unity");
                var demo=Object.FindFirstObjectByType<SeededWorldDemo>();
                Assert.That(demo.Rebuild(),Is.True,demo.Diagnostics);
                var c=demo.Composition;var decoration=demo.Decoration;
                var inspector=Editor.CreateEditor(c);
                try{
                    Assert.That(inspector.GetType(),Is.EqualTo(typeof(IntegrationEditor)),
                        "The installed package must own the Composition inspector.");
                }
                finally{Object.DestroyImmediate(inspector);}
                var previous=c.RecipeAsset;
                var oldBridge=c.Paths.BridgeMaterial;
                c.Paths.BridgeMaterial=null;
                var missing=Assert.Throws<InvalidOperationException>(()=>
                    IntegrationEditor.CaptureAndSaveRecipe(c,decoration,path));
                StringAssert.Contains("Bridge Material",missing.Message);
                Assert.That(AssetDatabase.LoadMainAssetAtPath(path),Is.Null,
                    "Preflight failure must not create an incomplete asset.");
                Assert.That(c.RecipeAsset,Is.SameAs(previous));
                c.Paths.BridgeMaterial=oldBridge;
                AssetDatabase.CreateFolder("Assets","TerraLoomRecipeEditorConsumerTest");
                var saved=IntegrationEditor.CaptureAndSaveRecipe(c,decoration,path);
                Assert.That(AssetDatabase.Contains(saved),Is.True);
                Assert.That(saved.SchemaVersion,Is.EqualTo(3));
                Assert.That(saved.Settings.TerrainSource,Is.EqualTo(CompositionTerrainSource.Generated));
                // Previously saved schema-2 generated recipes remain usable without rewriting the asset.
                saved.SchemaVersion=2;
                Assert.That(c.RecipeAsset,Is.SameAs(saved));
                Assert.That(c.RecipeDecoration,Is.SameAs(decoration));
                Assert.That(saved.Settings.Modules.Paths.BridgeMaterial,Is.SameAs(oldBridge));
                string pathJson=c.Paths.PlanJson,riverJson=c.Rivers.PlanJson;
                c.Paths.BridgeMaterial=null;c.Rivers.WaterMaterial=null;
                IntegrationEditor.GenerateSavedRecipe(c);
                Assert.That(c.Paths.BridgeMaterial,Is.SameAs(oldBridge));
                Assert.That(c.Rivers.WaterMaterial,Is.SameAs(saved.Settings.Modules.Rivers.WaterMaterial));
                Assert.That(c.Paths.PlanJson,Is.EqualTo(pathJson));
                Assert.That(c.Rivers.PlanJson,Is.EqualTo(riverJson));
                Assert.That(c.ValidateCurrent(out var reason),Is.True,reason);
            }
            finally
            {
                if(setup.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
