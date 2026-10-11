using System;
using System.Linq;
using TerraLoom.Core.Editor;
using TerraLoom.Core.Unity;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Integration.Editor
{
    /// <summary>Consumer-only orchestration UI. Neither product package imports its sibling.</summary>
    public sealed class CompositionWorkbenchExtension : WorldModuleEditorExtension
    {
        public override string Id=>"consumer.composition";
        public override string Title=>"Shared generation";
        public override string Icon=>"Core";
        public override int Order=>10;
        public override Color Color=>new Color(.7f,.6f,1);
        public override bool CoordinatesGeometry=>true;
        public override bool IncludesDecoration(Component component)=>component&&component.GetComponent<SeededWorldDemo>();
        public override bool PreparesTerrain(Component component)=>component&&component.GetComponent<SeededWorldDemo>();
        public override void Generate(Component component)
        {
            var composition=(TerraLoomIntegration)component;
            var recipe=composition.GetComponent<SeededWorldDemo>();
            if(recipe)
            {
                if(!recipe.Rebuild())throw new InvalidOperationException(recipe.Diagnostics);
                EditorUtility.SetDirty(recipe);
            }
            else if(composition.World && composition.World.OwnsTerrain)
            {
                if(!composition.GenerateOwnedSource())throw new InvalidOperationException(composition.Diagnostics);
            }
            else if(!composition.Generate())throw new InvalidOperationException(composition.Diagnostics);
            EditorUtility.SetDirty(composition);
        }
        public override bool CanDetach(Component component,out string reason)
        {
            var composition=(TerraLoomIntegration)component;
            reason=composition.Paths&&composition.Paths.GeneratedRoot||composition.Rivers&&composition.Rivers.GeneratedRoot
                ?"Clear generated composition first.":null;
            return reason==null;
        }
        public override Component[] Find(TerraLoomWorld world)=>Object.FindObjectsByType<TerraLoomIntegration>(FindObjectsSortMode.None).Where(c=>c.World==world).Cast<Component>().ToArray();
        public override Component Add(TerraLoomWorld world,bool separateObject)
        {
            var paths=Object.FindObjectsByType<TerraLoomPaths>(FindObjectsSortMode.None).FirstOrDefault(p=>p.World==world);
            var rivers=Object.FindObjectsByType<TerraLoomRivers>(FindObjectsSortMode.None).FirstOrDefault(r=>r.World==world);
            if(!paths||!rivers)throw new InvalidOperationException("Attach and configure Paths and Rivers for this world before adding the consumer composition.");
            var host=world.gameObject;
            if(separateObject){host=new GameObject("TerraLoom Composition");Undo.RegisterCreatedObjectUndo(host,"Create composition");Undo.SetTransformParent(host.transform,world.transform,"Parent composition");}
            var composition=Undo.AddComponent<TerraLoomIntegration>(host);composition.World=world;composition.Paths=paths;composition.Rivers=rivers;EditorUtility.SetDirty(composition);return composition;
        }
        public override void DrawInspector(Component component)
        {
            var composition=(TerraLoomIntegration)component;
            var dynamic=composition.GetComponent<SeededWorldDemo>();
            if(dynamic)WorldWorkbenchGUI.DrawComponentInspector(dynamic);
            else WorldWorkbenchGUI.DrawComponentInspector(composition);
        }
    }
}
