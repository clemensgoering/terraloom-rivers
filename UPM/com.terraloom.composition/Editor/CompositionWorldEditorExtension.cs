using System;
using System.Linq;
using TerraLoom.Core.Editor;
using TerraLoom.Core.Unity;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraLoom.Integration.Editor
{
    /// <summary>Connects the optional Composition package to Core's single World Workbench.</summary>
    public sealed class CompositionWorldEditorExtension : WorldModuleEditorExtension
    {
        public override string Id => "composition";
        public override string Title => "Shared Rivers + Paths generation";
        public override string Icon => "Core";
        public override int Order => 10;
        public override Color Color => new Color(.7f, .6f, 1f);
        public override bool CoordinatesGeometry => true;
        private static ICompositionWorldRecipe SceneRecipe(TerraLoomIntegration composition) =>
            composition.GetComponents<MonoBehaviour>().OfType<ICompositionWorldRecipe>().FirstOrDefault();
        public override bool IncludesDecoration(Component component) => component is TerraLoomIntegration c &&
            (SceneRecipe(c)?.IncludesDecoration == true || c.RecipeAsset && c.RecipeDecoration);
        public override bool PreparesTerrain(Component component) => component is TerraLoomIntegration c &&
            (SceneRecipe(c)?.PreparesTerrain == true || c.RecipeAsset && c.RecipeDecoration);

        public override Component[] Find(TerraLoomWorld world) =>
            Object.FindObjectsByType<TerraLoomIntegration>(FindObjectsSortMode.None)
                .Where(c => c.World == world)
                .Cast<Component>().ToArray();

        public override Component Add(TerraLoomWorld world, bool separateObject)
        {
            var paths = Object.FindObjectsByType<TerraLoomPaths>(FindObjectsSortMode.None).FirstOrDefault(p => p.World == world);
            var rivers = Object.FindObjectsByType<TerraLoomRivers>(FindObjectsSortMode.None).FirstOrDefault(r => r.World == world);
            if (!paths || !rivers)
                throw new InvalidOperationException("Attach Paths and Rivers to this world before adding shared generation.");
            var host = world.gameObject;
            if (separateObject)
            {
                host = new GameObject("TerraLoom Composition");
                Undo.RegisterCreatedObjectUndo(host, "Create composition");
                Undo.SetTransformParent(host.transform, world.transform, "Parent composition");
            }
            var composition = Undo.AddComponent<TerraLoomIntegration>(host);
            composition.World = world;
            composition.Paths = paths;
            composition.Rivers = rivers;
            EditorUtility.SetDirty(composition);
            return composition;
        }

        public override void Generate(Component component)
        {
            var composition = (TerraLoomIntegration)component;
            if(!composition.World||!composition.Paths||!composition.Rivers||
               composition.Paths.World!=composition.World||composition.Rivers.World!=composition.World)
                throw new InvalidOperationException("Shared generation needs Core, Rivers and Paths bound to this world.");
            var sceneRecipe=SceneRecipe(composition);
            if(sceneRecipe!=null)
            {
                if(!sceneRecipe.RebuildWorld())throw new InvalidOperationException(sceneRecipe.WorldDiagnostics);
            }
            else if (composition.RecipeAsset)
                IntegrationEditor.GenerateSavedRecipe(composition);
            else if (composition.World && composition.World.OwnsTerrain)
            {
                if (!composition.GenerateOwnedSource()) throw new InvalidOperationException(composition.Diagnostics);
            }
            else
                IntegrationEditor.GenerateOnSuppliedTerrain(composition);
            EditorUtility.SetDirty(composition);
        }

        public override void DrawInspector(Component component) => WorldWorkbenchGUI.DrawComponentInspector(component);

        public override bool CanDetach(Component component, out string reason)
        {
            var composition = (TerraLoomIntegration)component;
            reason = composition.Paths && composition.Paths.GeneratedRoot || composition.Rivers && composition.Rivers.GeneratedRoot
                ? "Clear generated composition first." : null;
            return reason == null;
        }
    }
}
