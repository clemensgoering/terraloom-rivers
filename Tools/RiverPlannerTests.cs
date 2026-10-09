// Standalone SDK runner: dotnet run --project Tools/RiverPlannerTests.csproj
// Uses the sibling development Core by default; override with -p:CoreRoot=/path/to/core.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TerraLoom.Core;
using TerraLoom.Rivers;

internal static class RiverPlannerTests
{
    private static readonly WorldBounds Bounds = new WorldBounds(-12, -12, 12, 12);
    private static readonly RiverRequest[] Requests = { new RiverRequest("main", "source", "mouth") };
    private static int passed, failed;
    private static int Main()
    {
        Check("flat terrain: channel overlay, exact endpoints, aligned immutable polylines", () =>
        {
            var p = new RiverProfile(); var input = Input(); var plan = Plan(input, p);
            Require(plan.Complete && plan.Routes.Count == 1, Detail(plan)); var route = plan.Routes[0];
            Require(plan.Reports[0].Identity == plan.Identity && plan.Reports[0].CoreInputIdentity == input.Identity
                && plan.Reports[0].ProfileFingerprint == p.Fingerprint, "Report lost provenance.");
            Require(route.WaterPolyline.First().X == -8 && route.WaterPolyline.Last().X == 8, "Endpoints were snapped.");
            Require(route.WaterPolyline.Count == route.BedPolyline.Count && route.LeftBankPolyline.Count == route.BedPolyline.Count
                && route.RightBankPolyline.Count == route.BedPolyline.Count, "Polyline indices differ.");
            for (int i = 0; i < route.BedPolyline.Count; i++)
            {
                Require(route.WaterPolyline[i].Y == .02, "Flat water is not terrain + .02.");
                Near(route.WaterPolyline[i].Y - route.BedPolyline[i].Y, p.Depth);
                Require(route.BedPolyline[i].Y < 0 && route.LeftBankPolyline[i].Y >= .02 && route.RightBankPolyline[i].Y >= .02, "Overlay elevations invalid.");
            }
            Throws<NotSupportedException>(() => ((IList<WorldPoint>)route.WaterPolyline)[0] = default);
            var again = Plan(input, p);
            Require(plan.Identity == again.Identity && Points(route.WaterPolyline) == Points(again.Routes[0].WaterPolyline), "Plan changed on replay.");
        });
        Check("descending terrain: no rising water/bed/banks, seam depth and Core envelopes", () =>
        {
            var p = new RiverProfile(); var plan = Plan(Input(new Height((x, z) => -x * .1)), p);
            Require(plan.Complete, Detail(plan)); var route = plan.Routes[0];
            for (int i = 1; i < route.WaterPolyline.Count; i++)
            {
                Require(route.WaterPolyline[i].Y <= route.WaterPolyline[i - 1].Y, "Water goes uphill.");
                Require(route.BedPolyline[i].Y <= route.BedPolyline[i - 1].Y, "Bed goes uphill.");
                Require(route.LeftBankPolyline[i].Y <= route.LeftBankPolyline[i - 1].Y, "Bank envelope goes uphill.");
                Near(route.WaterPolyline[i].Y - route.BedPolyline[i].Y, p.Depth);
            }
            foreach (var offer in route.Offers)
            {
                Require(offer.Identity == plan.Identity, "Offer provenance differs.");
                foreach (var point in route.WaterPolyline.Where(point => offer.Corridor.Bed.Contains(point)))
                    Require(point.Y <= offer.Corridor.WaterLevel + 1e-12, "Core water envelope understates water.");
            }
        });
        Check("existing RiverOffer integration: hard beds, soft banks, long bank-to-bank bridge offer", () =>
        {
            var input = Input(); var plan = Plan(input); var contribution = plan.ToContribution("river-module", input.Identity);
            var assembled = PlanAssembler.Assemble(input, new[] { contribution });
            Require(contribution.Identity == input.Identity && plan.CoreInputIdentity == input.Identity && plan.Identity != input.Identity, "Own/shared identities conflated.");
            Require(contribution.Waters.Count == 1, "Straight river was fragmented into tiny bridge candidates.");
            var water = contribution.Waters.Single(); var crossing = contribution.Crossings.Single();
            Require(crossing.WaterId == water.Id && crossing.Bounds.Contains(water.Banks) && water.Banks.Contains(crossing.Bounds), "Crossing does not span banks.");
            Require(crossing.Bounds.MaxX - crossing.Bounds.MinX >= 16, "Straight run too short.");
            Require(contribution.Reservations.Any(a => a.Purpose == ReservationPurpose.RiverBed && a.Strength == ReservationStrength.Hard)
                && contribution.Reservations.Any(a => a.Purpose == ReservationPurpose.RiverBank && a.Strength == ReservationStrength.Soft), "Incorrect strengths.");
            Require(assembled.Waters.Count == 1, "Core rejected contribution.");
            var request = new CrossingRequest("bridge", crossing.Id, new WorldBounds(-1, -2, 1, 2), CrossingKind.Bridge, 3, 0);
            Require(CrossingEvaluator.Evaluate(assembled, input.Identity, request).Accepted, "Bank-to-bank bridge denied.");
            Throws<ArgumentException>(() => plan.ToContribution(new PlanIdentity(0, 0, "other", "other")));
        });
        Check("all hard footprints including banks and subcell obstacles are respected", () =>
        {
            var obstacle = new AreaReservation("small-hard", "world", new WorldBounds(.21, -.1, .23, .1), ReservationStrength.Hard, ReservationPurpose.ProtectedArea);
            var plan = Plan(Input(areas: new[] { obstacle })); Require(plan.Complete, Detail(plan));
            Require(plan.Routes[0].Offers.All(o => !o.Corridor.Banks.Overlaps(obstacle.Bounds)), "Hard footprint crossed.");
            Require(plan.Routes[0].WaterPolyline.Any(p => Math.Abs(p.Z) > 2), "Bank margin omitted.");
            var wall = new AreaReservation("wall", "world", new WorldBounds(-.01, -12, .01, 12), ReservationStrength.Hard, ReservationPurpose.ProtectedArea);
            var blocked = Plan(Input(areas: new[] { wall }));
            Require(blocked.Reports.Single().Outcome == RiverOutcome.NoRoute && blocked.Routes.Count == 0
                && Detail(blocked).Contains("wall"), "Dead end missing its hard conflict.");
            Throws<InvalidOperationException>(() => blocked.ToContribution(blocked.CoreInputIdentity));
        });
        Check("long diagonal bridge envelopes are revalidated before merging", () =>
        {
            var anchors = new ManualAnchorSource("diagonal", 1, new[]
            {
                new WorldAnchor("source", new WorldPoint(-6, 0, -6)), new WorldAnchor("mouth", new WorldPoint(6, 0, 6))
            });
            var clear = Plan(Input(anchors: anchors)); Require(clear.Complete, Detail(clear));
            Require(clear.Routes[0].Offers.Count == 1 && clear.Routes[0].Offers[0].Corridor.Banks.MinX <= -8
                && clear.Routes[0].Offers[0].Corridor.Banks.MaxZ >= 8, "Long diagonal crossing was fragmented.");
            var corner = new AreaReservation("extra-corner", "world", new WorldBounds(-5, 3, -3, 5), ReservationStrength.Hard, ReservationPurpose.ProtectedArea);
            var constrained = Plan(Input(anchors: anchors, areas: new[] { corner })); Require(constrained.Complete, Detail(constrained));
            Require(constrained.Routes[0].Offers.All(o => !o.Corridor.Banks.Overlaps(corner.Bounds)), "Merged diagonal crossed an unchecked AABB corner.");
        });
        Check("uphill, excessive slope and interior ridge never become successful rivers", () =>
        {
            var uphill = Plan(Input(new Height((x, z) => x * .1)));
            Require(uphill.Reports[0].Outcome == RiverOutcome.NoRoute && Detail(uphill).Contains("uphill"), "Uphill accepted.");
            var steep = Plan(Input(new Height((x, z) => -x)), new RiverProfile(maximumSlope: .1));
            Require(steep.Reports[0].Outcome == RiverOutcome.NoRoute && Detail(steep).Contains("slope"), "Steep river accepted.");
            var ridge = Plan(Input(new Height((x, z) => Math.Abs(x - .5) < .1 ? 2 : 0)));
            Require(ridge.Reports[0].Outcome == RiverOutcome.NoRoute, "Interior subcell ridge skipped.");
            var basin = Plan(Input(new Height((x, z) => x * x * .01)));
            Require(basin.Reports[0].Outcome == RiverOutcome.NoRoute && Detail(basin).Contains("uphill"), "Basin escaped uphill.");
        });
        Check("water is checked across bed width, not merely along centre", () =>
        {
            var input = Input(new Height((x, z) => Math.Abs(z) > .4 ? 3 : 0));
            var plan = Plan(input);
            Require(plan.Reports[0].Outcome == RiverOutcome.NoRoute && Detail(plan).Contains("water footprint"), "Dry side terrain intersects water.");
        });
        Check("explicit excavation permits bounded diagonal cross slopes without weakening hydraulics", () =>
        {
            var anchors = new ManualAnchorSource("cross-slope", 1, new[]
            {
                new WorldAnchor("source", new WorldPoint(-6, 0, -6)), new WorldAnchor("mouth", new WorldPoint(6, 0, 6))
            });
            var input = Input(new Height((x, z) => -z * .016), anchors: anchors);
            // Lattice endpoints lie on domain boundaries and cannot hold full banks: only the exact direct
            // diagonal is available, so strict routing cannot evade the cross slope via a cardinal detour.
            var strictProfile = new RiverProfile(cellSize: 100);
            var excavating = new RiverProfile(cellSize: 100, allowExcavation: true);
            var strict = Plan(input, strictProfile); var allowed = Plan(input, excavating);
            Require(!strictProfile.AllowExcavation && strict.Reports.Single().Outcome == RiverOutcome.NoRoute
                && Detail(strict).Contains("water footprint"), "Default read-only overlay was weakened.");
            Require(allowed.Complete, Detail(allowed)); var route = allowed.Routes.Single();
            Require(strictProfile.Fingerprint != excavating.Fingerprint && strict.Identity != allowed.Identity, "Excavation flag missing from provenance.");
            for (int i = 0; i < route.WaterPolyline.Count; i++)
            {
                Near(route.WaterPolyline[i].Y, -route.WaterPolyline[i].Z * .016 + excavating.SurfaceOffset);
                Near(route.WaterPolyline[i].Y - route.BedPolyline[i].Y, excavating.Depth);
                if (i > 0) Require(route.WaterPolyline[i].Y <= route.WaterPolyline[i - 1].Y, "Excavation allowed uphill water.");
            }
            Require(allowed.Reports.Sum(r => r.TerrainSamples) <= excavating.TotalSampleBudget, "Excavation escaped sampling budget.");
            var tooHigh = Plan(Input(new Height((x, z) => Math.Abs(z) > .4 ? .53 : 0)), new RiverProfile(allowExcavation: true));
            Require(tooHigh.Reports.Single().Outcome == RiverOutcome.NoRoute && Detail(tooHigh).Contains("maximum excavation cut"), "Cut above water + Depth accepted.");
            var uphill = Plan(Input(new Height((x, z) => x * .1)), new RiverProfile(allowExcavation: true));
            Require(uphill.Reports.Single().Outcome == RiverOutcome.NoRoute && Detail(uphill).Contains("uphill"), "Excavation relaxed centreline hydraulics.");
        });
        Check("missing target, missing terrain, outside endpoint and full bank bounds report separately", () =>
        {
            var missing = Plan(Input(), requests: new[] { new RiverRequest("missing", "source", "absent") });
            Require(missing.Reports.Single().Outcome == RiverOutcome.InvalidTarget, "Missing target ignored.");
            var absent = Plan(Input(new Height((x, z) => double.NaN)));
            Require(absent.Reports.Single().Outcome == RiverOutcome.MissingTerrain, "Missing terrain ignored.");
            var outside = Plan(Input(anchors: Anchors(-13, 8)));
            Require(outside.Reports.Single().Outcome == RiverOutcome.OutsideBounds, "Outside anchor accepted.");
            var edge = Plan(Input(anchors: Anchors(-11, 8)));
            Require(edge.Reports.Single().Outcome == RiverOutcome.NoRoute && Detail(edge).Contains("outside bounds"), "Bank footprint escaped domain.");
        });
        Check("grid, search, sample and tiny-resolution budgets are bounded", () =>
        {
            foreach (var p in new[] { new RiverProfile(maximumNodes: 4), new RiverProfile(totalSearchBudget: 1),
                new RiverProfile(totalSampleBudget: 3), new RiverProfile(sampleSpacing: 1e-200) })
            {
                var plan = Plan(Input(), p);
                Require(plan.Reports.Single().Outcome == RiverOutcome.BudgetExceeded, "Budget ignored: " + Detail(plan));
                Require(plan.Reports.Sum(r => r.ExpandedNodes) <= p.TotalSearchBudget && plan.Reports.Sum(r => r.TerrainSamples) <= p.TotalSampleBudget, "Budget overspent.");
            }
            var huge = RiverPlanner.Plan(Input().BaseSnapshot, new RiverProfile(cellSize: 1e-200), Bounds, Requests);
            Require(huge.Reports.Single().Outcome == RiverOutcome.BudgetExceeded, "Overflowing lattice accepted.");
        });
        Check("shared batch budgets and cancellation preserve a report for every request", () =>
        {
            var requests = new[] { new RiverRequest("z", "source", "mouth"), new RiverRequest("a", "source", "mouth") };
            var plan = Plan(Input(), new RiverProfile(totalSearchBudget: 1), requests);
            Require(plan.Reports.Count == 2 && plan.Reports.All(r => r.Outcome == RiverOutcome.BudgetExceeded)
                && plan.Reports.Sum(r => r.ExpandedNodes) == 1, "Batch work escaped budget.");
            var cancelled = RiverPlanner.Plan(Input().BaseSnapshot, new RiverProfile(), Bounds, requests, new CancellationToken(true));
            Require(cancelled.Reports.Count == 2 && cancelled.Reports.All(r => r.Outcome == RiverOutcome.Cancelled)
                && cancelled.Reports.Sum(r => r.TerrainSamples) == 0, "Precancelled request sampled terrain.");
            using (var token = new CancellationTokenSource())
            {
                int calls = 0;
                var input = Input(new Height((x, z) => { if (++calls == 20) token.Cancel(); return 0; }));
                var interrupted = RiverPlanner.Plan(input.BaseSnapshot, new RiverProfile(), Bounds, requests, token.Token);
                Require(interrupted.Reports.All(r => r.Outcome == RiverOutcome.Cancelled) && interrupted.Routes.Count == 0, "Mid-search cancel published geometry.");
            }
        });
        Check("profiles, domains and requests hash canonically; shared Core capture stays independent", () =>
        {
            var p = new RiverProfile(regionCosts: new[] { new RiverRegionCost("b", 3), new RiverRegionCost("a", 2) });
            var q = new RiverProfile(regionCosts: new[] { new RiverRegionCost("a", 2), new RiverRegionCost("b", 3) });
            Require(p.Fingerprint == q.Fingerprint, "Region enumeration order affects profile.");
            Require(p.Fingerprint != new RiverProfile(depth: .8).Fingerprint, "Depth missing from hash.");
            Require(new RiverProfile(maximumSlope: -0.0).Fingerprint == new RiverProfile(maximumSlope: 0.0).Fingerprint, "Signed zero not canonical.");
            var requests = new[] { new RiverRequest("b", "source", "mouth"), new RiverRequest("a", "mouth", "source") };
            Require(RiverPlanner.ProfileVersion(p, Bounds, requests) == RiverPlanner.ProfileVersion(q, Bounds, requests.Reverse()), "Request enumeration order affects hash.");
            Require(RiverPlanner.ProfileVersion(p, Bounds, Requests) != RiverPlanner.ProfileVersion(p, new WorldBounds(-13, -12, 12, 12), Requests), "Domain missing from hash.");
            var a = Plan(Input()); var b = Plan(Input(fingerprint: "terrain-content-changed"));
            Require(a.Identity != b.Identity, "Core input identity lost.");
            Throws<ArgumentException>(() => RiverPlanner.ProfileVersion(p, Bounds, new[] { Requests[0], Requests[0] }));
            Throws<ArgumentException>(() => new RiverProfile(regionCosts: new[] { new RiverRegionCost("x", 1), new RiverRegionCost("x", 2) }));
        });
        Check("manual and seeded Core anchors use one deterministic request/planning path", () =>
        {
            var terrain = new FlatHeightSource(0);
            var seeded = new SeededAnchorSource("seed", 1, 42, 4, -8, 8, -8, 8, terrain);
            var manual = new ManualAnchorSource("seed", 1, seeded.Anchors);
            var a = RiverPlanner.CreateRequests(seeded.Anchors, terrain); var b = RiverPlanner.CreateRequests(manual.Anchors.Reverse().ToArray(), terrain);
            Require(a.Single().SourceId == b.Single().SourceId && a.Single().MouthId == b.Single().MouthId, "Order changed generated request.");
            var first = Plan(Input(anchors: seeded), requests: a); var second = Plan(Input(anchors: manual), requests: b);
            Require(first.Complete && second.Complete && first.Identity == second.Identity
                && Points(first.Routes[0].WaterPolyline) == Points(second.Routes[0].WaterPolyline), "Manual/seeded parity broken.");
            Throws<InvalidOperationException>(() => RiverPlanner.CreateRequests(seeded.Anchors, new Height((x, z) => double.NaN)));
        });
        Check("landscape weights without biomes and soft reservation costs change route", () =>
        {
            var samples = new List<LandscapeSample>();
            var region = new RegionDefinition("costly", 1);
            for (int z = -10; z <= 10; z += 2) for (int x = -10; x <= 10; x += 2)
                samples.Add(new LandscapeSample(new WorldPoint(x, 0, z), new RegionSnapshot("regions", 1,
                    new[] { new RegionSample(region, Math.Abs(x) < 3 && Math.Abs(z) < 4 ? 1 : 0) })));
            var profile = new RiverProfile(regionCosts: new[] { new RiverRegionCost("costly", 100) });
            var plan = Plan(Input(landscape: samples), profile); Require(plan.Complete, Detail(plan));
            Require(plan.Routes[0].WaterPolyline.Any(p => Math.Abs(p.Z) >= 4), "Landscape cost ignored.");
            var reversed = Plan(Input(landscape: samples.AsEnumerable().Reverse()), profile);
            Require(plan.Identity == reversed.Identity && Points(plan.Routes[0].WaterPolyline) == Points(reversed.Routes[0].WaterPolyline), "Captured sample order changes routing.");
            var soft = new AreaReservation("soft", "world", new WorldBounds(-2, -3, 2, 3), ReservationStrength.Soft, ReservationPurpose.ProtectedArea, 1000);
            var detour = Plan(Input(areas: new[] { soft })); Require(detour.Complete, Detail(detour));
            Require(detour.Routes[0].Offers.All(o => !o.Corridor.Banks.Overlaps(soft.Bounds)), "Soft costs ignored.");
        });
        Check("height writers are never invoked, even when terrain exposes write capability", () =>
        {
            var spy = new Writer(); var input = Input(terrainOverride: new TerrainSnapshot("world", 1, "metres", new FlatHeightSource(0), spy));
            var plan = Plan(input); Require(plan.Complete && spy.Calls == 0, "Planner modified terrain.");
        });
        Check("sampled radius profile and final detour are enforced without a sharp fallback", () =>
        {
            var p=new RiverProfile(); Near(p.MinimumBendRadius,2);
            Require(p.Fingerprint==new RiverProfile(minimumBendRadius:2).Fingerprint,"Effective identity differs.");
            Require(p.Fingerprint!=new RiverProfile(minimumBendRadius:3).Fingerprint,"Radius omitted from identity.");
            foreach(double invalid in new[]{-1,double.NaN,double.PositiveInfinity})
                Throws<ArgumentException>(()=>new RiverProfile(minimumBendRadius:invalid));
            var area=new AreaReservation("radius-obstacle","world",new WorldBounds(-1,-1,1,1),ReservationStrength.Hard,ReservationPurpose.ProtectedArea);
            var input=Input(areas:new[]{area}); var plan=Plan(input,p); Require(plan.Complete,Detail(plan));
            var points=plan.Routes.Single().WaterPolyline;
            Require(RouteBendInspection.TryMeasureWithEndpoints(points,2,out var bend),"No final radius measurement.");
            Require(bend.MeasuredVertices==points.Count-2 && bend.MinimumRadius>=p.MinimumBendRadius-1e-8,"Final radius gate violated.");
            var impossible=Plan(input,new RiverProfile(minimumBendRadius:1000));
            Require(!impossible.Complete && impossible.Routes.Count==0 && Detail(impossible).Contains("sampled bend radius"),Detail(impossible));
        });
        Console.WriteLine("RiverPlanner: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void Check(string name, Action action)
    { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); } }
    private static void Require(bool condition, string detail) { if (!condition) throw new Exception(detail); }
    private static void Near(double a, double b) => Require(Math.Abs(a - b) <= 1e-10, "Expected " + a + " ~= " + b);
    private static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    private static string Detail(RiverPlan plan) => string.Join(" | ", plan.Reports.Select(r => r.Outcome + ": " + r.Detail));
    private static string Points(IEnumerable<WorldPoint> points) => string.Join(";", points.Select(p => p.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
        + "/" + p.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "/" + p.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
    private static IAnchorSource Anchors(double first = -8, double last = 8) => new ManualAnchorSource("anchors", 1,
        new[] { new WorldAnchor("source", new WorldPoint(first, 0, 0)), new WorldAnchor("mouth", new WorldPoint(last, 0, 0)) });
    private static PlanningInput Input(IHeightSource heights = null, IEnumerable<AreaReservation> areas = null,
        IAnchorSource anchors = null, IEnumerable<LandscapeSample> landscape = null, string fingerprint = "height-content-v1", TerrainSnapshot terrainOverride = null) =>
        new PlanningInput(42, 1, "shared-core-v1", "shared-profile-v1", terrainOverride ?? new TerrainSnapshot("world", 1, "metres", heights ?? new FlatHeightSource(0)),
            fingerprint, anchors ?? Anchors(), landscape ?? Array.Empty<LandscapeSample>(), areas ?? Array.Empty<AreaReservation>());
    private static RiverPlan Plan(PlanningInput input, RiverProfile profile = null, IEnumerable<RiverRequest> requests = null) =>
        RiverPlanner.Plan(input.BaseSnapshot, profile ?? new RiverProfile(), Bounds, requests ?? Requests);
    private sealed class Height : IHeightSource
    {
        private readonly Func<double, double, double> sample;
        internal Height(Func<double, double, double> sample) { this.sample = sample; }
        public bool TryGetHeight(double x, double z, out double y) { y = sample(x, z); return !double.IsNaN(y); }
    }
    private sealed class Writer : IHeightWriteSource
    {
        internal int Calls;
        public bool TrySetHeight(double x, double z, double y) { Calls++; throw new Exception("Unexpected terrain write."); }
    }
}
