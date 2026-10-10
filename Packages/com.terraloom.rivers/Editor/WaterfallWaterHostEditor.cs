using TerraLoom.Rivers.Unity;
using UnityEditor;
using UnityEngine;

namespace TerraLoom.Rivers.Editor
{
    [CustomEditor(typeof(WaterfallWaterHost))]
    public sealed class WaterfallWaterHostEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var host = (WaterfallWaterHost)target;
            EditorGUILayout.HelpBox("Water output supplied by a manual or seeded recipe. The generating caller supplies materials and validates its terrain/solid contacts. This host does not edit terrain. Cyan gizmos show physical anchors, before render offset.", MessageType.Info);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Owned output", host.Output, typeof(GameObject), true);
                if (host.Output) EditorGUILayout.IntField("Water meshes", host.Output.GetComponentsInChildren<MeshFilter>(true).Length);
                serializedObject.Update();
                var anchors = serializedObject.FindProperty("physicalAnchors");
                if (host.Output && anchors.arraySize == 4)
                {
                    var names = new[] { "Physical lip", "Physical impact", "Pool", "Outlet sill" };
                    for (int i = 0; i < names.Length; i++) EditorGUILayout.Vector3Field(names[i], anchors.GetArrayElementAtIndex(i).vector3Value);
                }
            }
            if (!string.IsNullOrEmpty(host.LastIssue)) EditorGUILayout.HelpBox(host.LastIssue, MessageType.Warning);
            if (host.Output && GUILayout.Button("Dispose generated water")) host.DisposeOutput();
        }
    }
}
