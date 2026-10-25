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
        public override bool IncludesDecoration(Component component)
        {
            var c=(TerraLoomIntegration)component;
            return c.ResolveConfiguredSource()==CompositionInputSource.SavedRecipe
                || c.ConfiguredSceneRecipe?.IncludesDecoration==true;
        }
        public override bool PreparesTerrain(Component component)
        {
            var c=(TerraLoomIntegration)component;
            return c.ResolveConfiguredSource()==CompositionInputSource.SavedRecipe
                || c.ConfiguredSceneRecipe?.PreparesTerrain==true;
        }

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
            if(!composition.RebuildConfiguredWorld())throw new InvalidOperationException(composition.Diagnostics);
            EditorUtility.SetDirty(composition);
        }

        public override void DrawBuildControls(Component component)
        {
            var c=(TerraLoomIntegration)component;
            EditorGUILayout.LabelField("Effective world inputs",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var chosen=(CompositionInputSource)EditorGUILayout.EnumPopup("Input source",c.InputSource);
            var recipe=(CompositionRecipe)EditorGUILayout.ObjectField("Saved recipe",c.RecipeAsset,typeof(CompositionRecipe),false);
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(c,"Choose world inputs");c.InputSource=chosen;c.RecipeAsset=recipe;EditorUtility.SetDirty(c);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            }
            try{EditorGUILayout.HelpBox("Build uses: "+c.ResolveConfiguredSource()+". Saved recipes apply their own settings; component edits are not recipe overrides.",MessageType.Info);}
            catch(Exception ex){EditorGUILayout.HelpBox(ex.Message,MessageType.Error);}
            if(c.RecipeAsset&&GUILayout.Button("Open saved recipe"))Selection.activeObject=c.RecipeAsset;
            EditorGUILayout.LabelField("Last build attempt",c.GenerationState+" (not an output freshness check)");
            if(!string.IsNullOrEmpty(c.Diagnostics))EditorGUILayout.HelpBox(c.Diagnostics,
                c.GenerationState==TerraLoom.Core.GenerationRunState.Failed?MessageType.Warning:MessageType.None);
        }

        public override void DrawInspector(Component component)
        {
            var c=(TerraLoomIntegration)component;
            DrawBuildControls(component);
            bool details=SessionState.GetBool("TerraLoom.Composition.Advanced."+c.GetInstanceID(),false);
            details=EditorGUILayout.Foldout(details,"Bindings and advanced settings",true);
            SessionState.SetBool("TerraLoom.Composition.Advanced."+c.GetInstanceID(),details);
            if(details)WorldWorkbenchGUI.DrawComponentInspector(component);
        }

        public override bool CanDetach(Component component, out string reason)
        {
            var composition = (TerraLoomIntegration)component;
            reason = composition.Paths && composition.Paths.GeneratedRoot || composition.Rivers && composition.Rivers.GeneratedRoot
                ? "Clear generated composition first." : null;
            return reason == null;
        }
    }
}
