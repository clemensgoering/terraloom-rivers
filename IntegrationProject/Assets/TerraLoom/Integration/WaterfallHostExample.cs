using System;
using TerraLoom.Core;
using TerraLoom.Rivers;
using TerraLoom.Rivers.Unity;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>Small independent consumer: supplied recipe + colliders + materials.
    /// No dependency on the comparison prototype or its terrain/cameras/scene flags.
    /// The same entry point accepts hand-authored and seeded planner recipes.
    /// Its contact validator checks actual collider floor depths at three lower
    /// centre stations; broader site/fall-wall support remains a host responsibility.</summary>
    public static class WaterfallHostExample
    {
        public static void Generate(WaterfallWaterHost host, WaterfallRecipe recipe,
            Material reach, Material fall, Collider[] contactGeometry, Func<bool> cancelled = null)
        {
            host.Generate(recipe, reach, fall, (r, meshes) =>
            {
                if (contactGeometry == null || contactGeometry.Length == 0)
                    throw new ArgumentException("Supply actual contact colliders.");
                Physics.SyncTransforms();
                foreach (double station in new[] { 0d, r.PoolStation, r.OutletStation })
                {
                    var sample = r.SampleLowerReach(station);
                    WorldPoint p = sample.Surface;
                    var start = new Vector3((float)p.X, (float)p.Y + 10, (float)p.Z);
                    float bed = float.NegativeInfinity;
                    foreach (var collider in contactGeometry)
                    {
                        if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy)
                            throw new InvalidOperationException("Contact colliders must exist and be active.");
                        if (collider.Raycast(new Ray(start, Vector3.down), out var hit, 100)) bed = Mathf.Max(bed, hit.point.y);
                    }
                    if (float.IsNegativeInfinity(bed) || Math.Abs(bed - (p.Y - sample.CentreDepth)) >= .004)
                        throw new InvalidOperationException("Supplied collider bed differs from recipe by >=4mm or is missing.");
                }
            }, cancelled: cancelled);
        }
    }
}
