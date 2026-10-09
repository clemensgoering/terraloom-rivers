using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Rivers;

namespace TerraLoom.Tests
{
    public sealed class RiverPlannerCurveTests
    {
        private static readonly WorldBounds Bounds = new WorldBounds(-12, -12, 12, 12);
        private static readonly RiverRequest[] Requests = { new RiverRequest("curve", "source", "mouth") };
        private static readonly AreaReservation Obstacle = new AreaReservation("protected", "world",
            new WorldBounds(-1, -1, 1, 1), ReservationStrength.Hard, ReservationPurpose.ProtectedArea);

        [Test] public void ObstacleRouteRoundsTurnsAndPublishesOnlyAuthoritativeDownhillGeometry()
        {
            var input = Input(new Height(Terrain), true);
            var profile = new RiverProfile(allowExcavation: true);
            var plan = RiverPlanner.Plan(input.BaseSnapshot, profile, Bounds, Requests);
            Assert.That(plan.Complete, Is.True, Detail(plan));
            var replay = RiverPlanner.Plan(input.BaseSnapshot, profile, Bounds, Requests);
            var route = plan.Routes.Single(); var points = route.WaterPolyline;
            Assert.That(points, Is.EqualTo(replay.Routes.Single().WaterPolyline));
            Assert.That(plan.Identity, Is.EqualTo(replay.Identity));
            Assert.That(plan.Identity.AlgorithmVersion, Is.EqualTo(RiverPlanner.AlgorithmVersion));
            Assert.That(points.First().X, Is.EqualTo(-8.3)); Assert.That(points.Last().X, Is.EqualTo(8.1));
            Assert.That(points.First().Z, Is.Zero); Assert.That(points.Last().Z, Is.Zero);
            Assert.That(points.Any(p => Math.Abs(p.Z) > 3), Is.True, "Obstacle requires a real detour.");
            Assert.That(route.BedPolyline.Count, Is.EqualTo(points.Count));
            Assert.That(route.LeftBankPolyline.Count, Is.EqualTo(points.Count));
            Assert.That(route.RightBankPolyline.Count, Is.EqualTo(points.Count));
            double largestTurn = 0; int curvedTurns = 0;
            for (int i = 0; i < points.Count; i++)
            {
                Assert.That(points[i].Y, Is.EqualTo(Terrain(points[i].X, points[i].Z) + profile.SurfaceOffset).Within(1e-12), "Interpolated Core Y leaked into water.");
                Assert.That(points[i].Y - route.BedPolyline[i].Y, Is.EqualTo(profile.Depth).Within(1e-12));
                Assert.That(route.LeftBankPolyline[i].Y, Is.GreaterThanOrEqualTo(points[i].Y));
                Assert.That(route.RightBankPolyline[i].Y, Is.GreaterThanOrEqualTo(points[i].Y));
                if (i > 0)
                {
                    double run = Distance(points[i - 1], points[i]);
                    Assert.That(run, Is.GreaterThan(0).And.LessThanOrEqualTo(.500001));
                    Assert.That(points[i].Y, Is.LessThanOrEqualTo(points[i - 1].Y));
                    Assert.That(route.BedPolyline[i].Y, Is.LessThanOrEqualTo(route.BedPolyline[i - 1].Y));
                    Assert.That(route.LeftBankPolyline[i].Y, Is.LessThanOrEqualTo(route.LeftBankPolyline[i - 1].Y));
                    Assert.That(route.RightBankPolyline[i].Y, Is.LessThanOrEqualTo(route.RightBankPolyline[i - 1].Y));
                    Assert.That((points[i - 1].Y - points[i].Y) / run, Is.LessThanOrEqualTo(profile.MaximumSlope));
                    Assert.That(route.Offers.Any(o => o.Corridor.Bed.Contains(points[i - 1]) && o.Corridor.Bed.Contains(points[i])), Is.True);
                }
                if (i > 1)
                {
                    var a = points[i - 2]; var b = points[i - 1]; var c = points[i];
                    double cosine = ((b.X - a.X) * (c.X - b.X) + (b.Z - a.Z) * (c.Z - b.Z)) / (Distance(a, b) * Distance(b, c));
                    double angle = Math.Acos(Math.Max(-1, Math.Min(1, cosine)));
                    largestTurn = Math.Max(largestTurn, angle); if (angle > .001) curvedTurns++;
                }
            }
            Assert.That(curvedTurns, Is.GreaterThan(4), "Lattice corners were retained.");
            Assert.That(largestTurn, Is.LessThanOrEqualTo(Math.PI / 12), "Final sampled heading changes by more than 15 degrees.");
            Assert.That(route.Offers.All(o => !o.Corridor.Banks.Overlaps(Obstacle.Bounds)), Is.True);
            Assert.That(route.Offers.SelectMany(o => o.Reservations).All(r => !r.Bounds.Overlaps(Obstacle.Bounds)), Is.True);
            Assert.That(plan.Reports.Single().TerrainSamples, Is.LessThanOrEqualTo(profile.TotalSampleBudget));
        }

        [Test] public void StraightValleyHasNoArtificialMeanders()
        {
            var plan = RiverPlanner.Plan(Input(new Height((x, z) => -x * .01), false).BaseSnapshot,
                new RiverProfile(), Bounds, Requests);
            Assert.That(plan.Complete, Is.True, Detail(plan));
            Assert.That(plan.Routes.Single().WaterPolyline.All(p => p.Z == 0), Is.True);
            Assert.That(plan.Routes.Single().Offers.Count, Is.EqualTo(1));
        }

        [Test] public void SmoothCentrelineDoesNotAuthorizeFoldedWideBankStrips()
        {
            var controls=new[]{new WorldPoint(-8,0,0),new WorldPoint(0,0,0),new WorldPoint(0,0,8)};
            Assert.That(PlanarCurve.TryRound(controls,2,.2,out var curve),Is.True);
            Assert.That(PlanarCurve.IsSimple(curve),Is.True);
            bool Check(double radius)
            {
                var left=new WorldPoint[curve.Count];var right=new WorldPoint[curve.Count];
                for(int i=0;i<curve.Count;i++)
                {
                    var a=curve[Math.Max(0,i-1)];var b=curve[Math.Min(curve.Count-1,i+1)];double run=Distance(a,b);
                    double dx=(b.X-a.X)/run,dz=(b.Z-a.Z)/run;
                    left[i]=new WorldPoint(curve[i].X-dz*radius,0,curve[i].Z+dx*radius);
                    right[i]=new WorldPoint(curve[i].X+dz*radius,0,curve[i].Z-dx*radius);
                }
                return RiverFootprint.IsValid(curve,left,right);
            }
            Assert.That(Check(.5),Is.True);Assert.That(Check(2),Is.False,"Inner bank folds despite smooth centreline.");
        }

        [Test] public void FinalCurveSamplingHonorsBudgetAndLateCancellationWithoutPartialRoute()
        {
            var baseline = RiverPlanner.Plan(Input(new Height(Terrain), true).BaseSnapshot,
                new RiverProfile(allowExcavation: true), Bounds, Requests);
            Assert.That(baseline.Complete, Is.True, Detail(baseline));
            int samples = baseline.Reports.Single().TerrainSamples;
            var limited = RiverPlanner.Plan(Input(new Height(Terrain), true).BaseSnapshot,
                new RiverProfile(allowExcavation: true, totalSampleBudget: samples - 1), Bounds, Requests);
            Assert.That(limited.Reports.Single().Outcome, Is.EqualTo(RiverOutcome.BudgetExceeded));
            Assert.That(limited.Routes, Is.Empty);
            Assert.That(limited.Reports.Single().TerrainSamples, Is.LessThanOrEqualTo(samples - 1));
            using (var cancellation = new CancellationTokenSource())
            {
                int calls = 0;
                var input = Input(new Height((x, z) => { if (++calls == samples - 10) cancellation.Cancel(); return Terrain(x, z); }), true);
                calls = 0;
                var cancelled = RiverPlanner.Plan(input.BaseSnapshot, new RiverProfile(allowExcavation: true), Bounds, Requests, cancellation.Token);
                Assert.That(cancelled.Reports.Single().Outcome, Is.EqualTo(RiverOutcome.Cancelled));
                Assert.That(cancelled.Routes, Is.Empty);
            }
        }

        [Test] public void HydraulicallyInvalidRoundingRejectsInsteadOfPublishingSharpSearchChain()
        {
            // Only the two cardinal axes have low terrain. A sharp L is feasible with bounded
            // excavation, but every rounded corner enters higher centre terrain and rises uphill.
            var input = new PlanningInput(42, 1, "core", "curve-rejection",
                new TerrainSnapshot("world", 1, "metres", new Height((x, z) => x == 0 || z == 0 ? 0 : .1)), "axes-v1",
                new ManualAnchorSource("anchors", 1, new[] { new WorldAnchor("source", new WorldPoint(-8, 0, 0)), new WorldAnchor("mouth", new WorldPoint(0, 0, 8)) }),
                Array.Empty<LandscapeSample>(), Array.Empty<AreaReservation>());
            var plan = RiverPlanner.Plan(input.BaseSnapshot, new RiverProfile(allowExcavation: true), Bounds, Requests);
            Assert.That(plan.Routes, Is.Empty);
            Assert.That(plan.Reports.Single().Outcome, Is.EqualTo(RiverOutcome.NoRoute));
            Assert.That(Detail(plan), Does.Contain("five radius attempts").And.Contain("no sharp-corner fallback").And.Contain("uphill"));
        }

        private static double Terrain(double x, double z) => 2 - .02 * x - .0005 * x * x;
        private static double Distance(WorldPoint a, WorldPoint b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
        private static string Detail(RiverPlan plan) => string.Join(" | ", plan.Reports.Select(r => r.Outcome + ": " + r.Detail));
        private static PlanningInput Input(IHeightSource heights, bool obstacle) => new PlanningInput(42, 1, "core", "curve-test",
            new TerrainSnapshot("world", 1, "metres", heights), "terrain-v1",
            new ManualAnchorSource("anchors", 1, new[] { new WorldAnchor("source", new WorldPoint(-8.3, 99, 0)), new WorldAnchor("mouth", new WorldPoint(8.1, -99, 0)) }),
            Array.Empty<LandscapeSample>(), obstacle ? new[] { Obstacle } : Array.Empty<AreaReservation>());
        private sealed class Height : IHeightSource
        {
            private readonly Func<double, double, double> sample;
            internal Height(Func<double, double, double> sample) { this.sample = sample; }
            public bool TryGetHeight(double x, double z, out double y) { y = sample(x, z); return true; }
        }
    }
}
