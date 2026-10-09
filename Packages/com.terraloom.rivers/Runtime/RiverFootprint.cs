using System;
using System.Collections.Generic;
using System.Threading;
using TerraLoom.Core;

namespace TerraLoom.Rivers
{
    /// <summary>Certifies sampled offset strips, not just the water centreline.
    /// Rejects folded quads and global boundary intersections. No terrain sampling.</summary>
    public static class RiverFootprint
    {
        public static bool IsValid(IReadOnlyList<WorldPoint> water, IReadOnlyList<WorldPoint> left,
            IReadOnlyList<WorldPoint> right, CancellationToken cancellation = default)
        {
            if (water == null || left == null || right == null) throw new ArgumentNullException(nameof(water));
            cancellation.ThrowIfCancellationRequested();
            if (water.Count < 2 || water.Count > 16384 || left.Count != water.Count || right.Count != water.Count) return false;
            var first = new List<WorldPoint>(water.Count * 2);
            var second = new List<WorldPoint>(water.Count * 2);
            for (int i = 0; i < water.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (left[i].Y < water[i].Y || right[i].Y < water[i].Y) return false;
                if (i > 0)
                {
                    // Both triangles must retain positive XZ area when traversing
                    // left -> right -> downstream. A folded inner bank reverses one.
                    double a = Cross(left[i-1], right[i-1], left[i]);
                    double b = Cross(right[i-1], right[i], left[i]);
                    if (!FinitePositive(a) || !FinitePositive(b)) return false;
                }
                first.Add(left[i]); second.Add(right[water.Count - 1 - i]);
            }
            for (int i = water.Count - 1; i >= 0; i--) first.Add(right[i]);
            for (int i = 0; i < water.Count; i++) second.Add(left[i]);
            // Each open boundary omits one different end cap. Together these two
            // bounded checks cover the whole closed strip, including both caps.
            return PlanarCurve.IsSimple(first, 1000000, cancellation) && PlanarCurve.IsSimple(second, 1000000, cancellation);
        }
        private static double Cross(WorldPoint a, WorldPoint b, WorldPoint c) =>
            (b.X-a.X)*(c.Z-a.Z)-(b.Z-a.Z)*(c.X-a.X);
        private static bool FinitePositive(double value) => value > 0 && !double.IsInfinity(value);
    }
}
