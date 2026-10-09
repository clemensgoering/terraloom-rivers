using System;
using System.Linq;
using TerraLoom.Core;
using UnityEngine;

namespace TerraLoom.Rivers.Unity
{
    public static class RiverPlanStore
    {
        [Serializable] private sealed class Stored
        { public int format = 1, seed; public long revision; public string algorithm, domain, input; public Route[] routes; }
        [Serializable] private sealed class Route { public string id; public Point[] water; }
        [Serializable] private sealed class Point { public double x,y,z; }
        public static string Save(RiverPlan plan)
        {
            if (plan == null || !plan.Complete) throw new ArgumentException("A complete river plan is required.");
            return JsonUtility.ToJson(new Stored { seed = plan.Identity.Seed, revision = plan.Identity.Revision,
                algorithm = plan.Identity.AlgorithmVersion, domain = plan.DomainFingerprint, input = plan.Identity.InputFingerprint,
                routes = plan.Routes.Select(r => new Route { id = r.Request.Id, water = r.WaterPolyline.Select(p => new Point { x=p.X,y=p.Y,z=p.Z }).ToArray() }).ToArray() }, true);
        }
        public static void Validate(string json, RiverPlan fresh)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 32000000) throw new ArgumentException("Invalid plan JSON size.");
            var stored = JsonUtility.FromJson<Stored>(json);
            // Compare the original canonical doubles. JsonUtility's read/write roundtrip can change their printed precision.
            if (stored == null || stored.format != 1 || json != Save(fresh))
                throw new ArgumentException("Saved river plan is stale, incomplete or edited. Rebuild against the current inputs.");
        }
    }
}
