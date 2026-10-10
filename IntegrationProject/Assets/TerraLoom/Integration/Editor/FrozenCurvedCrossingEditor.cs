using UnityEditor;
using UnityEngine;

namespace TerraLoom.Integration.Editor
{
    /// <summary>Makes full-road rejection and the separate local evidence visible in the inspector.</summary>
    [CustomEditor(typeof(FrozenCurvedCrossing))]
    public sealed class FrozenCurvedCrossingEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var instance=(FrozenCurvedCrossing)target;
            EditorGUILayout.HelpBox("Frozen seed42. Construction Probe only tests a local bridge and both approaches. It never replaces or accepts the original target connection.",MessageType.Info);
            if(GUILayout.Button("Generate / inspect frozen crossing")){instance.Generate();SceneView.RepaintAll();}
            if(GUILayout.Button("Clear probe and river earthworks")){instance.ClearProbe();if(instance.Composition)instance.Composition.Clear();SceneView.RepaintAll();}
            if(!string.IsNullOrEmpty(instance.ProbeDiagnostic))EditorGUILayout.HelpBox(instance.ProbeDiagnostic,MessageType.Warning);
            if(!string.IsNullOrEmpty(instance.FullRouteFailure))EditorGUILayout.HelpBox(instance.FullRouteFailure,MessageType.Error);
        }
    }
}
