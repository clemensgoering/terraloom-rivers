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
            EditorGUILayout.HelpBox(instance.Diagnostics,MessageType.Info);
        }
    }
}
