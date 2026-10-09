using System;
using System.Threading;
using UnityEngine;

namespace TerraLoom.Rivers.Unity
{
    /// <summary>Returns an owned copy. Neither the supplied TerrainData nor its textures are modified.</summary>
    public static class RiverTerrainCarver
    {
        public static TerrainData Build(TerrainData basis, Vector3 origin, RiverPlan plan, RiverProfile profile, CancellationToken cancellation)
        {
            if (!basis || basis.heightmapResolution > 1025) throw new ArgumentException("Terrain carving supports resolutions up to 1025.");
            if (plan == null || profile == null || !plan.Complete || plan.ProfileFingerprint != profile.Fingerprint
                || !profile.AllowExcavation) throw new ArgumentException("Complete matching plan and explicit excavation permission required.");
            int n = basis.heightmapResolution; var values = basis.GetHeights(0,0,n,n); var size = basis.size;
            long segments = 0; foreach (var route in plan.Routes) segments += route.WaterPolyline.Count - 1;
            if (segments * n * n > 50000000) throw new InvalidOperationException("Carving exceeds 50 million segment/sample checks; reduce terrain resolution or river extent.");
            for (int z = 0; z < n; z++)
            {
                cancellation.ThrowIfCancellationRequested();
                for (int x = 0; x < n; x++)
                {
                    double px = origin.x + size.x * x / (n-1), pz = origin.z + size.z * z / (n-1);
                    double current = origin.y + size.y * values[z,x], target = current;
                    foreach (var route in plan.Routes) for (int s = 0; s < route.WaterPolyline.Count - 1; s++)
                    {
                        if (RiverChannelSurface.TryHeight(route,s,profile.Width,profile.BankWidth,px,pz,out double surface))
                            target=Math.Min(target,surface-.02);
                    }
                    double normalized=(target-origin.y)/size.y;
                    // Planner permits terrain at most Depth + WaterInset above water; bed is Depth below.
                    // Apply that same maximum to actual Unity grid samples, including banks after union.
                    if (current-target > profile.Depth*2+profile.WaterInset+.020001)
                        throw new InvalidOperationException("River excavation exceeds twice Depth plus Water Inset and 2 cm mesh clearance. Previous terrain retained.");
                    if (normalized < 0 || normalized > 1) throw new InvalidOperationException("River bed leaves Terrain height range. Raise the terrain or reduce river depth.");
                    values[z,x]=(float)normalized;
                }
            }
            var result = UnityEngine.Object.Instantiate(basis); result.name = "Rivers carved terrain (owned copy)"; result.hideFlags=HideFlags.DontSave;
            try { result.SetHeights(0,0,values); return result; }
            catch { if (Application.isPlaying) UnityEngine.Object.Destroy(result); else UnityEngine.Object.DestroyImmediate(result); throw; }
        }
    }
}
