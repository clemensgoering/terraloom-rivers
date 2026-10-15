using System;
using System.Collections.Generic;
using System.Threading;
using TerraLoom.Core;

namespace TerraLoom.Rivers
{
    /// <summary>Immutable physical world-space triangles for one publication identity.
    /// Input must be the final wetted footprint BEFORE rendering offsets. No Unity objects
    /// are retained. Construction fails rather than accepting unbounded query work.</summary>
    public sealed class RiverWaterSurfaceSnapshot : IWaterSurfaceQuery
    {
        public const int MaximumTriangles = 65536;
        public const int MaximumTrianglesPerCell = 4096;
        public const int MaximumIndexReferences = 1048576;
        private readonly Triangle[] triangles;
        private readonly Dictionary<long, int[]> cells;
        private readonly double minX, minZ, maxX, maxZ, cellSize;
        public PlanIdentity Identity { get; }
        public int SurfaceTriangleCount => triangles.Length;

        /// <summary>Copies physical world vertices and indexed triangles. Vertical and
        /// degenerate triangles are individually excluded; a pool beneath them survives.</summary>
        public RiverWaterSurfaceSnapshot(PlanIdentity identity, IReadOnlyList<WorldPoint> physicalVertices,
            IReadOnlyList<int> indices, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!identity.IsValid) throw new ArgumentException("A publication identity is required.", nameof(identity));
            if (physicalVertices == null) throw new ArgumentNullException(nameof(physicalVertices));
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            if (indices.Count % 3 != 0 || indices.Count / 3 > MaximumTriangles)
                throw new ArgumentException("Triangle count is invalid or exceeds the query budget.", nameof(indices));
            Identity = identity;
            var accepted = new List<Triangle>();
            double loX = double.PositiveInfinity, loZ = double.PositiveInfinity;
            double hiX = double.NegativeInfinity, hiZ = double.NegativeInfinity;
            for (int i = 0; i < indices.Count; i += 3)
            {
                cancellation.ThrowIfCancellationRequested();
                var a = Vertex(physicalVertices, indices[i]);
                var b = Vertex(physicalVertices, indices[i + 1]);
                var c = Vertex(physicalVertices, indices[i + 2]);
                var t = new Triangle(a, b, c);
                if (!t.IsSurface) continue;
                accepted.Add(t);
                loX = Math.Min(loX, t.MinX); loZ = Math.Min(loZ, t.MinZ);
                hiX = Math.Max(hiX, t.MaxX); hiZ = Math.Max(hiZ, t.MaxZ);
            }
            triangles = accepted.ToArray();
            cells = new Dictionary<long, int[]>();
            if (triangles.Length == 0) { cellSize = 1; return; }
            minX = loX; minZ = loZ; maxX = hiX; maxZ = hiZ;
            double extent = Math.Max(maxX - minX, maxZ - minZ);
            if (!Finite(extent)) throw new ArgumentException("Surface bounds exceed finite world coordinates.");
            cellSize = Math.Max(1, extent / 128);
            var building = new Dictionary<long, List<int>>();
            int references = 0;
            for (int i = 0; i < triangles.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var t = triangles[i];
                for (int x = CellX(t.MinX); x <= CellX(t.MaxX); x++)
                    for (int z = CellZ(t.MinZ); z <= CellZ(t.MaxZ); z++)
                    {
                        if (++references > MaximumIndexReferences)
                            throw new ArgumentException("Water query spatial-index budget exceeded.");
                        long key = Key(x, z);
                        if (!building.TryGetValue(key, out var list)) building.Add(key, list = new List<int>());
                        if (list.Count >= MaximumTrianglesPerCell)
                            throw new ArgumentException("Water query cell density exceeds the bounded lookup budget.");
                        list.Add(i);
                    }
            }
            foreach (var pair in building) cells.Add(pair.Key, pair.Value.ToArray());
            cancellation.ThrowIfCancellationRequested();
        }

        public bool TryGetSurfaceHeight(double worldX, double worldZ, out double surfaceY)
        {
            surfaceY = 0;
            if (!Finite(worldX) || !Finite(worldZ) || triangles.Length == 0
                || worldX < minX || worldX > maxX || worldZ < minZ || worldZ > maxZ
                || !cells.TryGetValue(Key(CellX(worldX), CellZ(worldZ)), out var candidates)) return false;
            bool found = false;
            foreach (int index in candidates)
                if (triangles[index].TryHeight(worldX, worldZ, out double height)
                    && (!found || height > surfaceY)) { surfaceY = height; found = true; }
            return found;
        }

        private int CellX(double x) => (int)Math.Floor((x - minX) / cellSize);
        private int CellZ(double z) => (int)Math.Floor((z - minZ) / cellSize);
        private static long Key(int x, int z) => ((long)x << 32) | (uint)z;
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        private static WorldPoint Vertex(IReadOnlyList<WorldPoint> vertices, int i)
        {
            if (i < 0 || i >= vertices.Count) throw new ArgumentException("Water triangle index is outside the vertex array.");
            var p = vertices[i];
            if (!Finite(p.X) || !Finite(p.Y) || !Finite(p.Z)) throw new ArgumentException("Water vertex is not finite.");
            return p;
        }

        private readonly struct Triangle
        {
            private readonly WorldPoint a, b, c;
            private readonly double denominator;
            public readonly bool IsSurface;
            public readonly double MinX, MaxX, MinZ, MaxZ;
            public Triangle(WorldPoint a, WorldPoint b, WorldPoint c)
            {
                this.a = a; this.b = b; this.c = c;
                double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z;
                double vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
                double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                denominator = ux * vz - uz * vx;
                double scale = Math.Max(Math.Abs(nx), Math.Max(Math.Abs(ny), Math.Abs(nz)));
                if (!Finite(scale) || !Finite(denominator)) throw new ArgumentException("Water triangle arithmetic overflow.");
                IsSurface = scale > 0 && Math.Abs(denominator) > scale * 1e-8;
                MinX = Math.Min(a.X, Math.Min(b.X, c.X)); MaxX = Math.Max(a.X, Math.Max(b.X, c.X));
                MinZ = Math.Min(a.Z, Math.Min(b.Z, c.Z)); MaxZ = Math.Max(a.Z, Math.Max(b.Z, c.Z));
            }
            public bool TryHeight(double x, double z, out double height)
            {
                height = 0;
                if (x < MinX || x > MaxX || z < MinZ || z > MaxZ) return false;
                double px = x - a.X, pz = z - a.Z;
                double u = (px * (c.Z - a.Z) - pz * (c.X - a.X)) / denominator;
                double v = ((b.X - a.X) * pz - (b.Z - a.Z) * px) / denominator;
                const double edgeTolerance = 1e-10;
                if (!Finite(u) || !Finite(v) || u < -edgeTolerance || v < -edgeTolerance || u + v > 1 + edgeTolerance) return false;
                height = (1 - u - v) * a.Y + u * b.Y + v * c.Y;
                return Finite(height);
            }
        }
    }
}
