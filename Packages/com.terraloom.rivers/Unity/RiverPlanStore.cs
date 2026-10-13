using System;
using System.Linq;
using TerraLoom.Core;
using UnityEngine;

namespace TerraLoom.Rivers.Unity
{
    public static class RiverPlanStore
    {
        [Serializable] private sealed class Stored
        { public int format = 2, seed; public long revision; public string algorithm, domain, input; public Route[] routes; }
        [Serializable] private sealed class StoredFall
        { public int format = 3, seed; public long revision; public string algorithm, domain, input; public Route[] routes; public Fall[] falls; }
        [Serializable] private sealed class StoredBoundaryFall
        { public int format = 4, seed; public long revision; public string algorithm, domain, input; public BoundaryRoute[] routes; public Fall[] falls; }
        [Serializable] private sealed class Route { public string id; public Point[] water, leftBank, rightBank; }
        [Serializable] private sealed class BoundaryRoute { public string id; public Point[] bed, water, leftBank, rightBank; }
        [Serializable] private sealed class Fall
        { public string request,site,anchor; public Point lip,impact,pool,outlet; public double minX,minZ,maxX,maxZ; }
        [Serializable] private sealed class Point { public double x,y,z; }
        public static string Save(RiverPlan plan)
        {
            if (plan == null || !plan.Complete) throw new ArgumentException("A complete river plan is required.");
            if(plan.PublishedFalls.Count>0)
            {
                var published=plan.PublishedFalls.Select(f=>new Fall{
                    request=f.Candidate.RequestId,site=f.Candidate.Site.Id,anchor=f.Candidate.Site.AnchorId,
                    lip=P(f.Candidate.Lip),impact=P(f.Candidate.Impact),pool=P(f.Candidate.Pool),outlet=P(f.Candidate.Outlet),
                    minX=f.ProtectedFootprint.Bounds.MinX,minZ=f.ProtectedFootprint.Bounds.MinZ,
                    maxX=f.ProtectedFootprint.Bounds.MaxX,maxZ=f.ProtectedFootprint.Bounds.MaxZ}).ToArray();
                if(plan.HasContinuousSourceToMouth)
                    return JsonUtility.ToJson(new StoredBoundaryFall{seed=plan.Identity.Seed,revision=plan.Identity.Revision,
                        algorithm=plan.Identity.AlgorithmVersion,domain=plan.DomainFingerprint,input=plan.Identity.InputFingerprint,
                        routes=plan.Routes.Select(r=>new BoundaryRoute{id=r.Request.Id,
                            bed=r.BedPolyline.Select(P).ToArray(),water=r.WaterPolyline.Select(P).ToArray(),
                            leftBank=r.LeftBankPolyline.Select(P).ToArray(),rightBank=r.RightBankPolyline.Select(P).ToArray()}).ToArray(),
                        falls=published},true);
                return JsonUtility.ToJson(new StoredFall{seed=plan.Identity.Seed,revision=plan.Identity.Revision,
                    algorithm=plan.Identity.AlgorithmVersion,domain=plan.DomainFingerprint,input=plan.Identity.InputFingerprint,
                    routes=Array.Empty<Route>(),falls=published},true);
            }
            return JsonUtility.ToJson(new Stored { seed = plan.Identity.Seed, revision = plan.Identity.Revision,
                algorithm = plan.Identity.AlgorithmVersion, domain = plan.DomainFingerprint, input = plan.Identity.InputFingerprint,
                routes = plan.Routes.Select(r => new Route { id = r.Request.Id,
                    water = r.WaterPolyline.Select(P).ToArray(), leftBank = r.LeftBankPolyline.Select(P).ToArray(),
                    rightBank = r.RightBankPolyline.Select(P).ToArray() }).ToArray() }, true);
        }
        private static Point P(WorldPoint p) => new Point { x=p.X,y=p.Y,z=p.Z };
        public static void Validate(string json, RiverPlan fresh)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 32000000) throw new ArgumentException("Invalid plan JSON size.");
            var stored = JsonUtility.FromJson<Stored>(json);
            // Compare the original canonical doubles. JsonUtility's read/write roundtrip can change their printed precision.
            int expected=fresh.PublishedFalls.Count==0?2:fresh.HasContinuousSourceToMouth?4:3;
            if (stored == null || stored.format != expected || json != Save(fresh))
                throw new ArgumentException("Saved river plan is stale, incomplete or edited. Rebuild against the current inputs.");
        }
    }
}
