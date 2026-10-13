using UnityEditor;
using UnityEngine;

namespace TerraLoom.Integration.Editor
{
    [CustomEditor(typeof(ExplicitCrossingPermissionSource))]
    public sealed class ExplicitCrossingPermissionSourceEditor : UnityEditor.Editor
    {
        private bool showApprovals;
        public override void OnInspectorGUI()
        {
            var source=(ExplicitCrossingPermissionSource)target;
            int count=source.Approvals?.Length??0;
            EditorGUILayout.LabelField("Current crossing authority",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Only reviewed crossing IDs and XZ rectangles entered here can be granted. A saved recipe does not authorize a bridge. Changed candidates fail closed when the world is regenerated.",MessageType.Info);
            EditorGUILayout.LabelField("Reviewed crossings",count.ToString());
            showApprovals=EditorGUILayout.Foldout(showApprovals,"Edit exact approvals",true);
            if(!showApprovals)return;
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Approvals"),true);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Each crossing needs its exact planner crossing ID and a separate section for every touched water reach. The approved rectangle must fit the named candidate. Inspect the generated world before entering these rights.",MessageType.None);
        }
    }
}
