using UnityEditor;
namespace TerraLoom.Rivers.Editor
{
    public static class RiversProjectMenu
    {
        [MenuItem("Tools/TerraLoom/Rivers/Status")]
        public static void ShowStatus()
        {
            EditorUtility.DisplayDialog("TerraLoom Rivers",
                "The project skeleton is ready. Generation and profiles are the next development step.", "OK");
        }
    }
}
