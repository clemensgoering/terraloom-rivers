using System;
using System.Collections.Generic;
using System.Threading;
using TerraLoom.Core;
using UnityEngine;

namespace TerraLoom.Rivers.Unity
{
    /// <summary>Copies final clipped water into a portable physical snapshot. Render
    /// offsets are removed in local space before transforming to world metres.</summary>
    public static class RiverWaterSurfaceCapture
    {
        public static RiverWaterSurfaceSnapshot Capture(GameObject root, PlanIdentity identity,
            CancellationToken cancellation = default)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            var vertices = new List<WorldPoint>(); var indices = new List<int>();
            foreach (var marker in root.GetComponentsInChildren<RiverGeneratedGeometry>(true))
            {
                cancellation.ThrowIfCancellationRequested();
                if (marker.Role != RiverGeometryRole.Water) continue;
                if (!marker.HasPhysicalSurfaceMetadata)
                    throw new InvalidOperationException("Water has no validated physical offset metadata; regenerate this publication.");
                var filter = marker.GetComponent<MeshFilter>(); var mesh = filter ? filter.sharedMesh : null;
                if (!mesh || !mesh.isReadable)
                    throw new InvalidOperationException("Physical water query requires a readable water mesh.");
                if ((long)vertices.Count + mesh.vertexCount > RiverWaterSurfaceSnapshot.MaximumTriangles * 3L)
                    throw new InvalidOperationException("Water vertex capture budget exceeded.");
                var sourceIndices = mesh.triangles;
                if ((long)indices.Count + sourceIndices.Length > RiverWaterSurfaceSnapshot.MaximumTriangles * 3L)
                    throw new InvalidOperationException("Water query triangle capture budget exceeded.");
                var sourceVertices = mesh.vertices;
                int start = vertices.Count;
                var offset = marker.PhysicalSurfaceRenderOffsetLocal;
                var matrix = marker.transform.localToWorldMatrix;
                if (float.IsNaN(matrix.determinant) || float.IsInfinity(matrix.determinant) || matrix.determinant == 0)
                    throw new InvalidOperationException("Water transform must be finite and invertible.");
                foreach (var v in sourceVertices)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var physical = matrix.MultiplyPoint3x4(v - offset);
                    vertices.Add(new WorldPoint(physical.x, physical.y, physical.z));
                }
                foreach (int index in sourceIndices)
                {
                    if (index < 0 || index >= sourceVertices.Length)
                        throw new InvalidOperationException("Water mesh index is out of range.");
                    indices.Add(checked(start + index));
                }
            }
            return new RiverWaterSurfaceSnapshot(identity, vertices, indices, cancellation);
        }
    }
}
