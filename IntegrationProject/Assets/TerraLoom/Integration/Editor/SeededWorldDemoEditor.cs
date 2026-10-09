using TerraLoom.Core.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TerraLoom.Integration.Editor
{
    [CustomEditor(typeof(SeededWorldDemo))]
    public sealed class SeededWorldDemoEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var recipe=(SeededWorldDemo)target;
            EditorGUILayout.HelpBox("This configuration builds at runtime without baked geometry. Editor preview calls exactly the same Rebuild method. UseSeedConfiguration off preserves manual terrain/targets/regions. A full world rebuild clears previous geometry first.",MessageType.Info);
            if(GUILayout.Button("Rebuild from current seed (runtime pipeline)")){recipe.Rebuild();EditorUtility.SetDirty(recipe);EditorSceneManager.MarkSceneDirty(recipe.gameObject.scene);SceneView.RepaintAll();}
            if(GUILayout.Button("Open World Workbench")&&recipe.Composition)TerraLoomWorldWindow.Open(recipe.Composition.World);
            if(!string.IsNullOrEmpty(recipe.Diagnostics))EditorGUILayout.HelpBox(recipe.Diagnostics,MessageType.None);
        }
    }
}
