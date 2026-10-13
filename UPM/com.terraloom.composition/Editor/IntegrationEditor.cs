using System;
using System.Linq;
using TerraLoom.Core.Unity;
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
        /// <summary>Resolve an explicit decoration first; an implicit match must be unambiguous.</summary>
        public static RegionDecoration ResolveDecoration(TerraLoomIntegration instance)
        {
            if(!instance)throw new InvalidOperationException("Select a Composition host first.");
            if(instance.RecipeDecoration)return instance.RecipeDecoration;
            if(!instance.World)throw new InvalidOperationException("Assign Core World on the Composition host.");
            var matches=UnityEngine.Object.FindObjectsByType<RegionDecoration>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(d=>d.World==instance.World).ToArray();
            if(matches.Length!=1)throw new InvalidOperationException(matches.Length==0
                ?"Add Region Decoration and assign the same Core World before capturing a recipe."
                :"Several Region Decorations use this Core World. Assign Recipe Decoration explicitly on the Composition host.");
            return matches[0];
        }
        /// <summary>Read-only authoring preflight; never changes a terrain or creates an asset.</summary>
        public static void ValidateRecipeInputs(TerraLoomIntegration instance,RegionDecoration decoration)
        {
            if(!instance||!instance.World||!instance.Paths||!instance.Rivers)
                throw new InvalidOperationException("Assign Core World, Paths and Rivers on the Composition host.");
            if(!decoration||instance.Paths.World!=instance.World||instance.Rivers.World!=instance.World||decoration.World!=instance.World)
                throw new InvalidOperationException("Paths, Rivers and Region Decoration must reference the same Core World.");
            if(!instance.Paths.GroundMaterial)throw new InvalidOperationException("Assign Paths Ground Material before capturing a recipe.");
            if(!instance.Paths.BridgeMaterial)throw new InvalidOperationException("Assign Paths Bridge Material before capturing a recipe.");
            if(!instance.Rivers.WaterMaterial)throw new InvalidOperationException("Assign Rivers Water Material before capturing a recipe.");
            if(!instance.Rivers.TerrainBrush&&(!instance.Rivers.BedMaterial||!instance.Rivers.BankMaterial))
                throw new InvalidOperationException("Assign Rivers Bed and Bank Materials for mesh banks, or enable Terrain Brush.");
            if(decoration.Count>0&&(decoration.Profiles==null||decoration.Profiles.Length==0))
                throw new InvalidOperationException("Assign at least one Region Decoration profile, or set its count to zero.");
        }
        /// <summary>Create and assign a new asset; never overwrite an existing recipe.</summary>
        public static CompositionRecipe CaptureAndSaveRecipe(TerraLoomIntegration instance,RegionDecoration decoration,string assetPath)
        {
            ValidateRecipeInputs(instance,decoration);
            if(string.IsNullOrWhiteSpace(assetPath)||!assetPath.StartsWith("Assets/",StringComparison.Ordinal)
                ||!assetPath.EndsWith(".asset",StringComparison.OrdinalIgnoreCase)
                ||AssetDatabase.LoadMainAssetAtPath(assetPath))
                throw new InvalidOperationException("Choose a new .asset path inside Assets; existing recipes are never overwritten.");
            var recipe=ScriptableObject.CreateInstance<CompositionRecipe>();
            try
            {
                recipe.CaptureFrom(instance,decoration);
                recipe.Validate();
                AssetDatabase.CreateAsset(recipe,assetPath);
                AssetDatabase.SaveAssets();
                instance.RecipeAsset=recipe;instance.RecipeDecoration=decoration;
                EditorUtility.SetDirty(instance);
                EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
                return recipe;
            }
            catch
            {
                if(!AssetDatabase.Contains(recipe))UnityEngine.Object.DestroyImmediate(recipe);
                throw;
            }
        }
        public static void GenerateSavedRecipe(TerraLoomIntegration instance)
        {
            if(!instance||!instance.RecipeAsset)throw new InvalidOperationException("Capture or assign a saved Composition Recipe first.");
            var decoration=ResolveDecoration(instance);
            if(!instance.World||!instance.Paths||!instance.Rivers||instance.Paths.World!=instance.World
                ||instance.Rivers.World!=instance.World||decoration.World!=instance.World)
                throw new InvalidOperationException("Core World, Paths, Rivers and Region Decoration must reference the same world.");
            instance.RecipeAsset.Validate();
            var modules=instance.RecipeAsset.Settings.Modules;
            if(!modules.Paths.GroundMaterial||!modules.Paths.BridgeMaterial)
                throw new InvalidOperationException("Saved Recipe needs Paths Ground and Bridge Materials. Assign them in the scene and capture a new recipe.");
            if(!modules.Rivers.WaterMaterial||(!modules.Rivers.TerrainBrush
                &&(!modules.Rivers.BedMaterial||!modules.Rivers.BankMaterial)))
                throw new InvalidOperationException("Saved Recipe needs Rivers Water Material and, for mesh banks, Bed and Bank Materials. Recapture after assigning them.");
            if(instance.World.Terrain&&!instance.World.OwnsTerrain)
                throw new InvalidOperationException("A saved seed recipe generates Core-owned terrain. For supplied terrain, use Generate on supplied terrain.");
            if(!instance.GenerateRecipe(instance.RecipeAsset,decoration))throw new InvalidOperationException(instance.Diagnostics);
            instance.RecipeDecoration=decoration;
            EditorUtility.SetDirty(instance);
            EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
        }
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
            GenerateSavedRecipe(instance);
        }
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); var instance=(TerraLoomIntegration)target;
            EditorGUILayout.LabelField("Recipe",EditorStyles.boldLabel);
            if(GUILayout.Button("Capture scene settings as new Recipe asset"))
            {
                try
                {
                    var decoration=ResolveDecoration(instance);
                    ValidateRecipeInputs(instance,decoration);
                    string path=EditorUtility.SaveFilePanelInProject("Save Composition Recipe","WorldRecipe","asset",
                        "Choose a new asset. Referenced materials and profiles stay in their original project assets.");
                    if(!string.IsNullOrEmpty(path))
                    {
                        var saved=CaptureAndSaveRecipe(instance,decoration,path);
                        Selection.activeObject=saved;
                    }
                    actionError=null;
                }
                catch(Exception ex){actionError=ex.Message;}
            }
            using(new EditorGUI.DisabledScope(!instance.RecipeAsset))
                if(GUILayout.Button("Generate from saved Recipe (Core terrain)"))
                {
                    try{GenerateSavedRecipe(instance);actionError=null;}
                    catch(Exception ex){actionError=ex.Message;}
                }
            if(GUILayout.Button("Generate Paths + Rivers on supplied terrain"))
            {
                try
                {
                    if(!instance.World||!instance.World.Terrain||instance.World.OwnsTerrain)
                        throw new InvalidOperationException("Assign a supplied Terrain to Core World, or use Generate from saved Recipe for Core terrain.");
                    if(!instance.Generate())throw new InvalidOperationException(instance.Diagnostics);
                    EditorUtility.SetDirty(instance);EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);actionError=null;
                }
                catch(Exception ex){actionError=ex.Message;}
            }
            EditorGUILayout.HelpBox("Recipe generation creates Core-owned terrain. Supplied Terrain uses the separate Paths + Rivers action; regional decoration remains configured on Region Decoration.",MessageType.None);
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
            EditorGUILayout.HelpBox("Clear or remove this component to release generated outputs. Unity Undo may restore the component, but not its released terrain and meshes. Assign and save a Composition Recipe to rebuild its recorded settings; unsaved changes and external asset contents are not snapshots.",MessageType.None);
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
