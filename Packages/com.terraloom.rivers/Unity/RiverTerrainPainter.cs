using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TerraLoom.Core;
using UnityEngine;

namespace TerraLoom.Rivers.Unity
{
    /// <summary>Optional sediment paint on the owned carved copy. Existing TerrainLayers are
    /// reused; other layer proportions and all source assets stay unchanged.</summary>
    public static class RiverTerrainPainter
    {
        public static int Apply(TerrainData carved,TerrainData source,Vector3 origin,RiverPlan plan,
            RiverProfile profile,int sedimentLayer,IEnumerable<AreaReservation> reservations,
            CancellationToken cancellation=default,float fullExposureDepth=.15f)
        {
            if(!carved||!source||carved==source||plan==null||profile==null||!profile.TerrainBrush
                ||!plan.Complete||plan.ProfileFingerprint!=profile.Fingerprint)
                throw new ArgumentException("Matching terrain brush plan and a separate carved terrain required.");
            int w=carved.alphamapWidth,h=carved.alphamapHeight,layers=carved.alphamapLayers;
            if(float.IsNaN(fullExposureDepth)||float.IsInfinity(fullExposureDepth)||fullExposureDepth<=0)
                throw new ArgumentOutOfRangeException(nameof(fullExposureDepth),"Full sediment exposure requires a finite positive cut depth in metres.");
            if(sedimentLayer<0||sedimentLayer>=layers)throw new ArgumentOutOfRangeException(nameof(sedimentLayer),"Select an existing terrain layer, or disable sediment painting with -1.");
            var hard=(reservations??Array.Empty<AreaReservation>()).Where(a=>a.Strength==ReservationStrength.Hard).ToArray();
            long spans=plan.Routes.Sum(r=>(long)r.WaterPolyline.Count-1);
            if(w>512||h>512||(spans+hard.Length)*w*h>50000000)
                throw new InvalidOperationException("Sediment painting exceeds the bounded alphamap work limit.");
            var weights=carved.GetAlphamaps(0,0,w,h);var size=carved.size;int painted=0;
            for(int z=0;z<h;z++)for(int x=0;x<w;x++)
            {
                cancellation.ThrowIfCancellationRequested();
                float u=(float)x/(w-1),v=(float)z/(h-1);
                double px=origin.x+size.x*u,pz=origin.z+size.z*v;
                double before=origin.y+source.GetInterpolatedHeight(u,v);
                double after=origin.y+carved.GetInterpolatedHeight(u,v);
                if(before-after<=.00001)continue;
                double mask=0;
                foreach(var route in plan.Routes)for(int span=0;span<route.WaterPolyline.Count-1;span++)
                    if(RiverTerrainBrush.TryHeight(route,span,profile,px,pz,before,out _,out double influence))mask=Math.Max(mask,influence);
                if(mask<=0)continue;
                // Paint follows realized earthwork as well as the planned cross-section. No maximal
                // reservation rectangle is painted, and untouched low terrain is not stained.
                // Visual exposure is independent of channel depth: a deep river must not
                // make an equally excavated bank retain more meadow than a shallow river.
                mask*=Math.Min(1,(before-after)/fullExposureDepth);
                float amount=(float)Math.Max(0,Math.Min(1,mask));
                double sx=size.x/(w-1),sz=size.z/(h-1);
                var support=new WorldBounds(px-sx,pz-sz,px+sx,pz+sz);
                foreach(var area in hard)if(support.Overlaps(area.Bounds))
                    throw new InvalidOperationException("Sediment texel support overlaps hard reservation "+area.Id+". Previous river retained.");
                for(int layer=0;layer<layers;layer++)
                    weights[z,x,layer]=weights[z,x,layer]*(1-amount)+(layer==sedimentLayer?amount:0);
                painted++;
            }
            cancellation.ThrowIfCancellationRequested();carved.SetAlphamaps(0,0,weights);return painted;
        }
    }
}
