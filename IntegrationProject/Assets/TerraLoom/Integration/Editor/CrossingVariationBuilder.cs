using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TerraLoom.Integration.Editor
{
    public static class CrossingVariationBuilder
    {
        [MenuItem("Tools/TerraLoom/Integration/Build Manual Curved Crossing")]
        public static void BuildManualBatch()=>Build(false);
        [MenuItem("Tools/TerraLoom/Integration/Build Seeded Curved Crossing")]
        public static void BuildSeededBatch()=>Build(true);
        private static void Build(bool seeded)
        {
            string root=IntegrationSampleBuilder.Root;
            var scene=EditorSceneManager.OpenScene(root+"/TerraLoomFrozenTruss.unity");
            var scenario=UnityEngine.Object.FindFirstObjectByType<FrozenCurvedCrossing>();var c=scenario.Composition;
            scenario.GenerateOnStart=false;scenario.ConstructionProbe=false;
            c.RestrictPathRouting=true;c.PathRoutingZone=new Rect(8,64,80,20);
            scenario.ApprovedCrossingZone=new Rect(24,64,48,20);
            foreach(var a in c.World.ManualAnchors)
            {
                if(a.Id=="source")a.Position=new Vector3(54,0,8);
                if(a.Id=="mouth")a.Position=new Vector3(44,0,88);
                if(a.Id=="west")a.Position=new Vector3(24,0,72);
                if(a.Id=="east")a.Position=new Vector3(72,0,76);
            }
            var variant=scenario.gameObject.AddComponent<CrossingRecipeVariant>();variant.Scenario=scenario;variant.SeedDriven=seeded;
            if(!variant.Rebuild())throw new InvalidOperationException(c.Diagnostics);
            if(c.Paths.LastPlan.Routes.Single().BridgeCrossings.Count!=1||!c.ValidateCurrent(out _))throw new InvalidOperationException("A complete current route with one negotiated crossing is required.");
            var binding=c.Paths.LastPlan.Routes.Single().BridgeCrossings.Single();
            Debug.Log("VARIANT_EDITOR_SUCCESS seeded="+seeded+" source="+c.World.TerrainContentFingerprint+" span="+Math.Abs(binding.End.X-binding.Start.X)+" z="+binding.Start.Z);
            c.GetComponent<FrozenCrossingEvidence>().RequireFullRoute=true;
            variant.Clear();AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene,root+(seeded?"/TerraLoomSeededCrossing.unity":"/TerraLoomManualCrossing.unity"));
        }
    }
}
