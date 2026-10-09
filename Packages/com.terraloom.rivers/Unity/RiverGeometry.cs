using System;
using System.Collections.Generic;
using System.Threading;
using TerraLoom.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TerraLoom.Rivers.Unity
{
    public enum RiverGeometryRole { Water, Bed, Banks }

    /// <summary>Continuous cross-sections, shared by runtime and editor. UVs are metres.</summary>
    public static class RiverGeometry
    {
        /// <summary>Builds authoritative planned cross-sections without terrain resampling. Outer banks
        /// retain their exact planned XYZ; inner rows interpolate bank XZ offsets at the bed/water ratio.
        /// Width and bankWidth must match the profile used to validate this route.</summary>
        public static Mesh Build(RiverRoute route, float width, float bankWidth, RiverGeometryRole role,
            int vertexBudget = 1000000, CancellationToken cancellation = default, bool extendWaterToBanks = false, bool terrainBrush = false)
        {
            if (route == null || !Finite(width) || width <= 0 || !Finite(bankWidth) || bankWidth < 0
                || !Enum.IsDefined(typeof(RiverGeometryRole), role))
                throw new ArgumentException("A planned river and finite dimensions required.");
            var water = route.WaterPolyline; var bed = route.BedPolyline;
            var left = route.LeftBankPolyline; var right = route.RightBankPolyline;
            if (water.Count < 2 || bed.Count != water.Count || left.Count != water.Count || right.Count != water.Count)
                throw new ArgumentException("Planned cross-section indices must align.");
            int columns = role == RiverGeometryRole.Banks ? 4 : 2;
            if ((long)water.Count * columns > vertexBudget || vertexBudget > 8000000)
                throw new ArgumentException("River geometry exceeds its vertex budget.");
            double half = (double)width / 2, outer = half + bankWidth, along = 0;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
            for (int i = 0; i < water.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (left[i].Y < water[i].Y || right[i].Y < water[i].Y)
                    throw new ArgumentException("Planned banks must stay above water.");
                if (i > 0)
                {
                    double dx = water[i].X - water[i - 1].X, dz = water[i].Z - water[i - 1].Z;
                    double run = Math.Sqrt(dx * dx + dz * dz);
                    if (double.IsNaN(run) || double.IsInfinity(run) || run <= 0)
                        throw new ArgumentException("Invalid planned river segment.");
                    along += run;
                }
                for (int c = 0; c < columns; c++)
                {
                    bool outerBank = role == RiverGeometryRole.Banks && (c == 0 || c == 3);
                    int channelColumn = role == RiverGeometryRole.Banks ? c : c + 1;
                    var channelPoint = RiverChannelSurface.Point(route, i, channelColumn, width, bankWidth);
                    var point = role==RiverGeometryRole.Water && terrainBrush
                        ? new WorldPoint((c==0?right[i]:left[i]).X,water[i].Y,(c==0?right[i]:left[i]).Z)
                        : role == RiverGeometryRole.Water && extendWaterToBanks
                        ? RiverChannelSurface.WaterPoint(route,i,c == 1,width,bankWidth)
                        : role == RiverGeometryRole.Water
                        ? new WorldPoint(channelPoint.X, water[i].Y, channelPoint.Z) : channelPoint;
                    var vertex = V(point);
                    if (!Finite(vertex.x) || !Finite(vertex.y) || !Finite(vertex.z) || !Finite((float)along))
                        throw new ArgumentException("Planned river exceeds mesh numeric range.");
                    float offset = (float)((c < columns / 2 ? -1 : 1) * (outerBank ? outer : half));
                    if(role==RiverGeometryRole.Water && extendWaterToBanks)
                    {
                        double dx=point.X-water[i].X,dz=point.Z-water[i].Z;
                        offset=(float)((c==0?-1:1)*Math.Sqrt(dx*dx+dz*dz));
                    }
                    vertices.Add(vertex); uv.Add(new Vector2(offset, (float)along));
                }
                if (i == 0) continue;
                for (int c = 0; c < columns - 1; c++)
                {
                    if (role == RiverGeometryRole.Banks && c == 1) continue;
                    int a = (i - 1) * columns + c, b = a + 1, d = i * columns + c, e = d + 1;
                    indices.AddRange(new[] { a, b, d, b, e, d });
                }
            }
            return Finish(vertices, uv, indices, role);
        }

        /// <summary>Legacy unplanned geometry helper. Reconstructs offsets and samples bank terrain;
        /// provides no planner footprint or hydraulic guarantees. Production plans use the route overload.</summary>
        public static Mesh Build(IReadOnlyList<WorldPoint> points, IHeightSource terrain, float width,
            float depth, float bankWidth, RiverGeometryRole role, int vertexBudget = 1000000,
            CancellationToken cancellation = default)
        {
            if (points == null || points.Count < 2 || terrain == null || !Finite(width) || width <= 0
                || !Finite(depth) || depth <= 0 || !Finite(bankWidth) || bankWidth <= 0)
                throw new ArgumentException("A river needs finite positive dimensions and at least two points.");
            int columns = role == RiverGeometryRole.Banks ? 4 : 2;
            if ((long)points.Count * columns > vertexBudget || vertexBudget > 8000000)
                throw new ArgumentException("River geometry exceeds its vertex budget.");
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
            float along = 0;
            for (int i = 0; i < points.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var center = V(points[i]);
                Vector3 before = V(points[Math.Max(0, i - 1)]), after = V(points[Math.Min(points.Count - 1, i + 1)]);
                var incoming = center - before; incoming.y = 0;
                var outgoing = after - center; outgoing.y = 0;
                if (i == 0) incoming = outgoing; if (i == points.Count - 1) outgoing = incoming;
                if (incoming.sqrMagnitude < 1e-8f || outgoing.sqrMagnitude < 1e-8f) throw new ArgumentException("Duplicate river vertices.");
                var direction = (incoming.normalized + outgoing.normalized).normalized;
                if (direction.sqrMagnitude < .01f) throw new ArgumentException("River cannot reverse at one vertex.");
                var side = new Vector3(-direction.z, 0, direction.x);
                // A bounded bevel stays inside the planner's conservative bank envelope.
                float miter = 1;
                if (i > 0) along += incoming.magnitude;
                for (int c = 0; c < columns; c++)
                {
                    float offset = role == RiverGeometryRole.Banks
                        ? new[] { -width / 2 - bankWidth, -width / 2, width / 2, width / 2 + bankWidth }[c]
                        : (c == 0 ? -width / 2 : width / 2);
                    var p = center + side * (offset * miter);
                    if (role == RiverGeometryRole.Bed || (role == RiverGeometryRole.Banks && (c == 1 || c == 2))) p.y -= depth;
                    else if (role == RiverGeometryRole.Banks)
                    {
                        if (!terrain.TryGetHeight(p.x, p.z, out double h)) throw new InvalidOperationException("Bank leaves captured terrain.");
                        p.y = (float)h + .015f;
                    }
                    vertices.Add(p); uv.Add(new Vector2(offset, along));
                }
                if (i == 0) continue;
                for (int c = 0; c < columns - 1; c++)
                {
                    if (role == RiverGeometryRole.Banks && c == 1) continue;
                    int a = (i - 1) * columns + c, b = a + 1, d = i * columns + c, e = d + 1;
                    indices.AddRange(new[] { a, b, d, b, e, d });
                }
            }
            return Finish(vertices, uv, indices, role);
        }
        private static Mesh Finish(List<Vector3> vertices, List<Vector2> uv, List<int> indices, RiverGeometryRole role)
        {
            var mesh = new Mesh { name = "River " + role, hideFlags = HideFlags.DontSave, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv);
            // Independent edge width survives subdivision; abs(interpolated across) does not.
            var shoreline=new List<Vector2>(uv.Count);
            foreach(var p in uv)shoreline.Add(new Vector2(Mathf.Abs(p.x),0));
            mesh.SetUVs(2,shoreline);
            var colors = new Color[vertices.Count]; for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            mesh.colors = colors; mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        private static Vector3 V(WorldPoint p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
