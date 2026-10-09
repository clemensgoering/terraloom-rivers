using System;
using TerraLoom.Core;

namespace TerraLoom.Rivers
{
    /// <summary>Authoritative four-column cross-section shared by mesh and excavation.
    /// Columns: right bank, right bed edge, left bed edge, left bank. No terrain resampling.</summary>
    public static class RiverChannelSurface
    {
        public static WorldPoint Point(RiverRoute route, int row, int column, double width, double bankWidth)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (row < 0 || row >= route.WaterPolyline.Count || column < 0 || column > 3
                || !RiverHash.Positive(width) || !RiverHash.Nonnegative(bankWidth))
                throw new ArgumentOutOfRangeException(nameof(row));
            var bank = column < 2 ? route.RightBankPolyline[row] : route.LeftBankPolyline[row];
            if (column == 0 || column == 3) return bank;
            var water = route.WaterPolyline[row]; double ratio = width / 2 / (width / 2 + bankWidth);
            return new WorldPoint(water.X + (bank.X-water.X)*ratio, route.BedPolyline[row].Y,
                water.Z + (bank.Z-water.Z)*ratio);
        }

        /// <summary>Lower height of covering triangles in one planned span. Outside returns false.
        /// Uses the exact mesh diagonal and has no extra rounded end caps.</summary>
        public static bool TryHeight(RiverRoute route, int span, double width, double bankWidth,
            double x, double z, out double height)
        {
            height = double.PositiveInfinity;
            if (!RiverHash.Finite(x) || !RiverHash.Finite(z)) throw new ArgumentException("Finite sample coordinates required.");
            var l0=Point(route,span,3,width,bankWidth); var l1=Point(route,span+1,3,width,bankWidth);
            var r0=Point(route,span,0,width,bankWidth); var r1=Point(route,span+1,0,width,bankWidth);
            if (x<Math.Min(Math.Min(l0.X,l1.X),Math.Min(r0.X,r1.X))
                || x>Math.Max(Math.Max(l0.X,l1.X),Math.Max(r0.X,r1.X))
                || z<Math.Min(Math.Min(l0.Z,l1.Z),Math.Min(r0.Z,r1.Z))
                || z>Math.Max(Math.Max(l0.Z,l1.Z),Math.Max(r0.Z,r1.Z))) return false;
            for (int column = 0; column < 3; column++)
            {
                var a=Point(route,span,column,width,bankWidth); var b=Point(route,span,column+1,width,bankWidth);
                var d=Point(route,span+1,column,width,bankWidth); var e=Point(route,span+1,column+1,width,bankWidth);
                if (Triangle(a,b,d,x,z,out double first)) height=Math.Min(height,first);
                if (Triangle(b,e,d,x,z,out double second)) height=Math.Min(height,second);
            }
            return RiverHash.Finite(height);
        }
        private static bool Triangle(WorldPoint a, WorldPoint b, WorldPoint c, double x, double z, out double height)
        {
            height=0; double bx=b.X-a.X,bz=b.Z-a.Z,cx=c.X-a.X,cz=c.Z-a.Z,px=x-a.X,pz=z-a.Z;
            double determinant=bx*cz-bz*cx;
            // Zero-bank-width profiles intentionally have degenerate outer strips.
            if (!RiverHash.Finite(determinant) || Math.Abs(determinant)<=1e-12) return false;
            double u=(px*cz-pz*cx)/determinant,v=(bx*pz-bz*px)/determinant;
            if (u < -1e-10 || v < -1e-10 || u+v > 1+1e-10) return false;
            height=a.Y+u*(b.Y-a.Y)+v*(c.Y-a.Y); return RiverHash.Finite(height);
        }
    }
}
