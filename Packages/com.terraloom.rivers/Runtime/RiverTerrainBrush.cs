using System;

namespace TerraLoom.Rivers
{
    /// <summary>Continuous cut-only cross-section for terrain, future material masks and editor
    /// previews. Existing planned channel faces bound every sample; no extra circular end caps.
    /// The original source height, never a previously stamped height, is used for blending.</summary>
    public static class RiverTerrainBrush
    {
        public static bool TryHeight(RiverRoute route,int span,RiverProfile profile,double x,double z,
            double sourceHeight,out double height,out double influence)
        {
            if(profile==null||!profile.TerrainBrush||!RiverHash.Finite(sourceHeight))
                throw new ArgumentException("Terrain brush profile and finite original height required.");
            height=sourceHeight;influence=0;
            if(!RiverChannelSurface.TryHeight(route,span,profile.Width,profile.BankWidth,x,z,out _))return false;
            var a=route.WaterPolyline[span];var b=route.WaterPolyline[span+1];
            double dx=b.X-a.X,dz=b.Z-a.Z,run2=dx*dx+dz*dz;
            double t=Math.Max(0,Math.Min(1,((x-a.X)*dx+(z-a.Z)*dz)/run2));
            double ox=x-a.X-t*dx,oz=z-a.Z-t*dz;
            double u=Math.Max(0,Math.Min(1,(Math.Sqrt(ox*ox+oz*oz)-profile.Width/2)/profile.BankWidth));
            // Zero derivative at the flat bed and at the original terrain seam.
            influence=1-u*u*(3-2*u);
            double bed=route.BedPolyline[span].Y+(route.BedPolyline[span+1].Y-route.BedPolyline[span].Y)*t-.02;
            height=sourceHeight+(Math.Min(sourceHeight,bed)-sourceHeight)*influence;
            return true;
        }
    }
}
