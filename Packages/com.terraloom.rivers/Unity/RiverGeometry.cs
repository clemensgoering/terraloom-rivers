using System;
using System.Collections.Generic;
using System.Threading;
using TerraLoom.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TerraLoom.Rivers.Unity
{
    public enum RiverGeometryRole { Water, Bed, Banks }

    /// <summary>Continuous mitered cross-sections, shared by runtime and editor. UVs are metres.</summary>
    public static class RiverGeometry
    {
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
            var mesh = new Mesh { name = "River " + role, hideFlags = HideFlags.DontSave, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv);
            var colors = new Color[vertices.Count]; for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            mesh.colors = colors; mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        private static Vector3 V(WorldPoint p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
