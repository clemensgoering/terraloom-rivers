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
                        var a = route.WaterPolyline[s]; var b = route.WaterPolyline[s+1];
                        double dx=b.X-a.X,dz=b.Z-a.Z, t=Math.Max(0,Math.Min(1,((px-a.X)*dx+(pz-a.Z)*dz)/(dx*dx+dz*dz)));
                        double ex=px-a.X-t*dx,ez=pz-a.Z-t*dz, distance=Math.Sqrt(ex*ex+ez*ez);
                        double half=profile.Width/2, outer=half+profile.BankWidth;
                        if (distance > outer) continue;
                        double bed=a.Y+(b.Y-a.Y)*t-profile.Depth-.02;
                        double blend=distance<=half ? 0 : (distance-half)/profile.BankWidth;
                        target=Math.Min(target,bed+(current-bed)*blend);
                    }
                    double normalized=(target-origin.y)/size.y;
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
