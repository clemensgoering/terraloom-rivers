using System;
using TerraLoom.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TerraLoom.Integration.Editor
{
    [CustomEditor(typeof(TerraLoomIntegration))]
    public sealed class IntegrationEditor : UnityEditor.Editor
    {
        private string actionError;
        /// <summary>Confirmation precedes all mutations. Clear is intentionally not registered with Undo.</summary>
        public static bool ClearConfirmed(TerraLoomIntegration instance,Func<bool> confirm)
        {
            if(!confirm())return false;
            instance.Clear();
            EditorUtility.SetDirty(instance);
            EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
            return true;
        }
        /// <summary>Rebuild from the explicitly assigned schema-2 recipe, rather than stale scene defaults.</summary>
        public static void RestoreSavedRecipe(TerraLoomIntegration instance)
        {
            if(!instance.RecipeAsset)throw new InvalidOperationException("Assign a saved Composition Recipe before restoring.");
            if(!instance.GenerateRecipe(instance.RecipeAsset,instance.RecipeDecoration))
                throw new InvalidOperationException(instance.Diagnostics);
            EditorUtility.SetDirty(instance);
            EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
        }
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); var instance=(TerraLoomIntegration)target;
            if (GUILayout.Button("Rebuild Core / Rivers / Paths")) { instance.Generate(); EditorUtility.SetDirty(instance); EditorSceneManager.MarkSceneDirty(instance.gameObject.scene); }
            if (GUILayout.Button("Clear generated composition (not undoable)"))
            {
                try
                {
                    ClearConfirmed(instance,()=>EditorUtility.DisplayDialog("Clear generated world?",
                        "This releases generated terrain, river, path and decoration resources. Ctrl+Z cannot restore them. "
                        +(instance.RecipeAsset?"Rebuild from saved recipe regenerates the assigned recipe; unsaved changes are not a snapshot.":"No saved Composition Recipe is assigned. Assign and save one before clearing if you need reproducible restoration."),
                        "Clear generated world","Cancel"));
                    actionError=null;
                }
                catch(Exception ex){actionError=ex.Message;}
            }
            using(new EditorGUI.DisabledScope(!instance.RecipeAsset))
                if(GUILayout.Button("Rebuild from saved recipe"))
                {
                    try{RestoreSavedRecipe(instance);actionError=null;}
                    catch(Exception ex){actionError=ex.Message;}
                }
            EditorGUILayout.HelpBox("Clear cannot be undone. Assign and save a Composition Recipe to rebuild its recorded settings. Rebuilding is deterministic regeneration, not an Undo snapshot of unsaved changes or external assets.",MessageType.None);
            if(!string.IsNullOrEmpty(actionError))EditorGUILayout.HelpBox(actionError,MessageType.Error);
            EditorGUILayout.LabelField("Geometry generation",instance.GenerationState.ToString());
            EditorGUILayout.HelpBox("Source snapshot → water offers → route negotiation → river earthworks → walkable geometry. Final terrain/material rules precede vegetation; navigation/details follow final collision. Rebuild the composition when upstream data changes.",MessageType.Info);
            if(!string.IsNullOrEmpty(instance.GenerationTrace))EditorGUILayout.HelpBox(instance.GenerationTrace,MessageType.None);
            if(GUILayout.Button("Check input/output freshness"))
            {bool current=instance.ValidateCurrent(out var reason);EditorUtility.DisplayDialog(current?"Composition current":"Composition stale",reason,"OK");}
            EditorGUILayout.HelpBox(instance.Diagnostics,MessageType.Info);
        }
    }
}
