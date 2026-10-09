using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TerraLoom.Core;
using UnityEngine;

namespace TerraLoom.Rivers.Unity
{
    /// <summary>Returns an owned copy. Neither the supplied TerrainData nor its textures are modified.</summary>
    public static class RiverTerrainCarver
    {
        public static TerrainData Build(TerrainData basis, Vector3 origin, RiverPlan plan, RiverProfile profile, CancellationToken cancellation,
            IEnumerable<AreaReservation> reservations = null)
        {
            if (!basis || basis.heightmapResolution > 1025) throw new ArgumentException("Terrain carving supports resolutions up to 1025.");
            if (plan == null || profile == null || !plan.Complete || plan.ProfileFingerprint != profile.Fingerprint
                || !profile.AllowExcavation) throw new ArgumentException("Complete matching plan and explicit excavation permission required.");
            int n = basis.heightmapResolution; var values = basis.GetHeights(0,0,n,n); var size = basis.size;
            long segments = 0; foreach (var route in plan.Routes) segments += route.WaterPolyline.Count - 1;
            var hard=(reservations??Array.Empty<AreaReservation>()).Where(a=>a.Strength==ReservationStrength.Hard).ToArray();
            if ((segments+hard.Length) * n * n > 50000000) throw new InvalidOperationException("Carving exceeds 50 million segment/sample checks; reduce terrain resolution or river extent.");
            for (int z = 0; z < n; z++)
            {
                cancellation.ThrowIfCancellationRequested();
                for (int x = 0; x < n; x++)
                {
                    double px = origin.x + size.x * x / (n-1), pz = origin.z + size.z * z / (n-1);
                    double current = origin.y + size.y * values[z,x], target = current;
                    foreach (var route in plan.Routes) for (int s = 0; s < route.WaterPolyline.Count - 1; s++)
                    {
                        if(profile.TerrainBrush)
                        {
                            if(RiverTerrainBrush.TryHeight(route,s,profile,px,pz,current,out double cut,out _))
                                target=Math.Min(target,cut);
                        }
                        else if(RiverChannelSurface.TryHeight(route,s,profile.Width,profile.BankWidth,px,pz,out double surface))
                            target=Math.Min(target,surface-.02);
                    }
                    double normalized=(target-origin.y)/size.y;
                    if(profile.TerrainBrush && target<current-1e-9)
                    {
                        // A changed grid node influences all adjacent cells, not only its own XZ point.
                        double sx=(double)size.x/(n-1),sz=(double)size.z/(n-1);
                        var support=new WorldBounds(px-sx,pz-sz,px+sx,pz+sz);
                        foreach(var area in hard)if(support.Overlaps(area.Bounds))
                            throw new InvalidOperationException("Terrain brush cell support overlaps hard reservation " + area.Id + ". Previous terrain retained.");
                    }
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
