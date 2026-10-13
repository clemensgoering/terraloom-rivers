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
