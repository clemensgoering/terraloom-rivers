using TerraLoom.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TerraLoom.Integration.Editor
{
    [CustomEditor(typeof(TerraLoomIntegration))]
    public sealed class IntegrationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); var instance=(TerraLoomIntegration)target;
            if (GUILayout.Button("Rebuild Core / Rivers / Paths")) { instance.Generate(); EditorUtility.SetDirty(instance); EditorSceneManager.MarkSceneDirty(instance.gameObject.scene); }
            if (GUILayout.Button("Clear generated composition")) { instance.Clear(); EditorSceneManager.MarkSceneDirty(instance.gameObject.scene); }
            EditorGUILayout.LabelField("Geometry generation",instance.GenerationState.ToString());
            EditorGUILayout.HelpBox("Source snapshot → water offers → route negotiation → river earthworks → walkable geometry. Final terrain/material rules precede vegetation; navigation/details follow final collision. Rebuild the composition when upstream data changes.",MessageType.Info);
            if(!string.IsNullOrEmpty(instance.GenerationTrace))EditorGUILayout.HelpBox(instance.GenerationTrace,MessageType.None);
            if(GUILayout.Button("Check input/output freshness"))
            {bool current=instance.ValidateCurrent(out var reason);EditorUtility.DisplayDialog(current?"Composition current":"Composition stale",reason,"OK");}
            EditorGUILayout.HelpBox(instance.Diagnostics,MessageType.Info);
        }
    }
}
