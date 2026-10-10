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
            EditorGUILayout.HelpBox(instance.ConstructionProbe?"Construction Probe only tests a local bridge and both approaches. It never replaces or accepts a rejected target connection.":"Full published route required. Construction-only fallback is disabled. The consumer routing zone grants no crossing rights.",MessageType.Info);
            if(GUILayout.Button("Generate / inspect crossing"))
            {var recipe=instance.GetComponent<CrossingRecipeVariant>();if(recipe)recipe.Rebuild();else instance.Generate();SceneView.RepaintAll();}
            if(GUILayout.Button("Clear probe and river earthworks"))
            {instance.ClearProbe();var recipe=instance.GetComponent<CrossingRecipeVariant>();if(recipe)recipe.Clear();else if(instance.Composition)instance.Composition.Clear();SceneView.RepaintAll();}
            if(!string.IsNullOrEmpty(instance.ProbeDiagnostic))EditorGUILayout.HelpBox(instance.ProbeDiagnostic,MessageType.Warning);
            if(!string.IsNullOrEmpty(instance.FullRouteFailure))EditorGUILayout.HelpBox(instance.FullRouteFailure,MessageType.Error);
        }
    }
}
