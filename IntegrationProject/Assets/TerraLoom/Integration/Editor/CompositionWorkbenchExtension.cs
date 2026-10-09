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
        public override string Title=>"Shared generation / runtime seed recipe";
        public override string Icon=>"Core";
        public override int Order=>10;
        public override Color Color=>new Color(.7f,.6f,1);
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
