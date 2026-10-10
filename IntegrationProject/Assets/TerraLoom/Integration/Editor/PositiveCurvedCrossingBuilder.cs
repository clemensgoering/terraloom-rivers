using System;
using System.Linq;
using TerraLoom.Core;
using TerraLoom.Paths;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TerraLoom.Integration.Editor
{
    /// <summary>Positive full-route recipe. Only road targets differ from the frozen negative case.</summary>
    public static class PositiveCurvedCrossingBuilder
    {
        [MenuItem("Tools/TerraLoom/Integration/Build Positive Curved Crossing")]
        public static void BuildBatch()
        {
            string root=IntegrationSampleBuilder.Root;
            var scene=EditorSceneManager.OpenScene(root+"/TerraLoomFrozenTruss.unity");
            var scenario=UnityEngine.Object.FindFirstObjectByType<FrozenCurvedCrossing>();
            var c=scenario.Composition;
            c.RestrictPathRouting=true;c.PathRoutingZone=new Rect(16,54,64,4.9f);
            scenario.name="TerraLoom Positive Curved Crossing";
            scenario.GenerateOnStart=false;scenario.ConstructionProbe=false;
            foreach(var anchor in c.World.ManualAnchors)
            {
                if(anchor.Id=="west")anchor.Position=new Vector3(24,0,57);
                if(anchor.Id=="east")anchor.Position=new Vector3(72,0,57);
            }
            var grid=(GridHeightSource)c.World.CaptureTerrain().Heights;
            foreach(var anchor in c.World.ManualAnchors.Where(a=>a.Id=="west"||a.Id=="east"))
            {
                double x=anchor.Position.x,z=anchor.Position.z;
                grid.TryGetHeight(x,z,out double h);int accepted=0;
                for(int dz=-1;dz<=2;dz++)for(int dx=-1;dx<=2;dx++)
                {
                    if(dx==0&&dz==0)continue;
                    double tx=x+dx*c.Paths.CellSize,tz=z+dz*c.Paths.CellSize;
                    grid.TryGetHeight(tx,tz,out double th);
                    if(PathTerrainSlopeInspection.Inspect(grid,new WorldPoint(x,h,z),new WorldPoint(tx,th,tz),c.Paths.Width,c.Paths.MaximumSlope).Accepted)accepted++;
                }
                if(accepted==0)throw new InvalidOperationException("Positive target has no exact valid connector: "+anchor.Id);
                Debug.Log("POSITIVE_TARGET "+anchor.Id+" accepted="+accepted+"/15");
            }
            if(!scenario.Generate())throw new InvalidOperationException(c.Diagnostics);
            if(c.Paths.LastPlan==null||scenario.ProbeRoot||!c.ValidateCurrent(out var reason))
                throw new InvalidOperationException("Positive route did not publish a valid full composition.");
            foreach(var route in c.Paths.LastPlan.Routes)
            {
                Debug.Log("POSITIVE_ROUTE cost="+route.Cost+" points="+route.Waypoints.Count+" bindings="+route.BridgeCrossings.Count);
                foreach(var binding in route.BridgeCrossings)Debug.Log("POSITIVE_BRIDGE "+binding.Start.X+","+binding.Start.Z+" -> "+binding.End.X+","+binding.End.Z);
            }
            var terrain=c.World.Terrain;var size=terrain.terrainData.size;
            var bounds=new WorldBounds(0,0,size.x,size.z);
            foreach(var option in PathPlanner.InspectAuthorizedBridgeAssemblies(c.LastSharedInput,c.Paths.CaptureProfile(),bounds,c.CrossingPermissions(c.LastSharedInput)))
                Debug.Log("POSITIVE_OPTION cost="+option.Cost+" points="+string.Join(";",option.Waypoints.Select(p=>p.X.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+p.Z.ToString("R",System.Globalization.CultureInfo.InvariantCulture))));
            if(!c.Paths.LastPlan.Routes.SelectMany(r=>r.BridgeCrossings).Any(b=>Math.Abs(Math.Abs(b.End.X-b.Start.X)-9.511811023622044)<.00001))
                throw new InvalidOperationException("Positive route must actually use the collective 9.511811m crossing.");
            Debug.Log("POSITIVE_CURVED_EDITOR_SUCCESS "+c.Diagnostics);
            c.GetComponent<FrozenCrossingEvidence>().RequireFullRoute=true;
            c.Clear();scenario.GenerateOnStart=true;
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,root+"/TerraLoomPositiveCurvedCrossing.unity");
        }
    }
}
