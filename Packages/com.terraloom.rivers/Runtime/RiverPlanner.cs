using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using TerraLoom.Core;

namespace TerraLoom.Rivers
{
    /// <summary>Deterministic bounded A* with directed hydraulically validated edges. No terrain writes.
    /// Source adapters must remain immutable for the input revision. Guarantees apply at SampleSpacing;
    /// a read-only point sampler cannot prove the absence of arbitrarily narrow unsampled terrain features.</summary>
    public static class RiverPlanner
    {
        public const string AlgorithmVersion = "rivers-inset-shoreline-v5";
        private const int MaximumCurvePoints = 16384;

        /// <summary>Selects one main river from manual or seeded Core anchors. Highest bed to lowest bed;
        /// equal mouth heights prefer greatest horizontal distance, then ordinal ID. Other anchors are not tributary requests.</summary>
        public static IReadOnlyList<RiverRequest> CreateRequests(IReadOnlyList<WorldAnchor> anchors, IHeightSource heights)
        {
            if (anchors == null || heights == null) throw new ArgumentNullException(anchors == null ? nameof(anchors) : nameof(heights));
            if (anchors.Count < 2 || anchors.Count > 512) throw new ArgumentException("Main river selection requires 2 to 512 anchors.");
            if (anchors.Any(a => string.IsNullOrWhiteSpace(a.Id)) || anchors.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != anchors.Count)
                throw new ArgumentException("Unique stable anchor IDs required.");
            var sampled = new List<WorldAnchor>();
            foreach (var a in anchors.OrderBy(a => a.Id, StringComparer.Ordinal))
            {
                if (!heights.TryGetHeight(a.Position.X, a.Position.Z, out double y) || !RiverHash.Finite(y))
                    throw new InvalidOperationException("Terrain unavailable for anchor " + a.Id);
                sampled.Add(new WorldAnchor(a.Id, new WorldPoint(a.Position.X, y, a.Position.Z)));
            }
            var source = sampled.OrderByDescending(a => a.Position.Y).ThenBy(a => a.Id, StringComparer.Ordinal).First();
            var mouths = sampled.Where(a => a.Id != source.Id && Distance(source.Position, a.Position) > 0)
                .OrderBy(a => a.Position.Y).ThenByDescending(a => Distance(source.Position, a.Position)).ThenBy(a => a.Id, StringComparer.Ordinal).ToArray();
            if (mouths.Length == 0) throw new ArgumentException("All anchors share one horizontal position.");
            string id = "main-" + RiverHash.Digest(w => { w.Write(source.Id); w.Write(mouths[0].Id); });
            return Array.AsReadOnly(new[] { new RiverRequest(id, source.Id, mouths[0].Id) });
        }

        public static string ProfileVersion(RiverProfile profile, WorldBounds bounds, IEnumerable<RiverRequest> requests)
        {
            if (profile == null || !bounds.IsValid) throw new ArgumentException("Profile and finite bounds required.");
            var ordered = Requests(requests);
            return RiverHash.Digest(w =>
            {
                w.Write("rivers-domain-v1"); w.Write(profile.Fingerprint);
                foreach (double n in new[] { bounds.MinX, bounds.MinZ, bounds.MaxX, bounds.MaxZ }) RiverHash.Number(w, n);
                w.Write(ordered.Length);
                foreach (var r in ordered) { w.Write(r.Id); w.Write(r.SourceId); w.Write(r.MouthId); }
            });
        }

        private static RiverRequest[] Requests(IEnumerable<RiverRequest> requests)
        {
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            // Materialize at most the supported batch, even for an infinite enumerable.
            var copy = requests.Take(1025).ToArray();
            if (copy.Length > 1024 || copy.Any(r => r == null)
                || copy.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ArgumentException("At most 1024 nonnull requests with unique IDs required.");
            return copy.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray();
        }

        public static RiverPlan Plan(PlanSnapshot snapshot, RiverProfile profile, WorldBounds bounds,
            IEnumerable<RiverRequest> requests, CancellationToken cancellation = default)
        {
            if (snapshot == null || profile == null) throw new ArgumentNullException(snapshot == null ? nameof(snapshot) : nameof(profile));
            if (!bounds.IsValid) throw new ArgumentException("Finite routing bounds required.");
            var ordered = Requests(requests);
            string domain = ProfileVersion(profile, bounds, ordered);
            var identity = new PlanIdentity(snapshot.Identity.Seed, snapshot.Identity.Revision, AlgorithmVersion, domain,
                RiverHash.Digest(w => { RiverHash.Identity(w, snapshot.Identity); w.Write(domain); }));
            var routes = new List<RiverRoute>(); var reports = new List<RiverReport>();
            var work = new Work(profile, cancellation);
            double rawX = Math.Ceiling((bounds.MaxX - bounds.MinX) / profile.CellSize) + 1;
            double rawZ = Math.Ceiling((bounds.MaxZ - bounds.MinZ) / profile.CellSize) + 1;
            bool gridValid = RiverHash.Finite(rawX * rawZ) && rawX >= 2 && rawZ >= 2 && rawX * rawZ + 2 <= profile.MaximumNodes;
            var field = gridValid ? new Field(snapshot, profile, bounds, (int)rawX, (int)rawZ, work) : null;
            foreach (var request in ordered)
            {
                int beforeNodes = work.Nodes, beforeSamples = work.Samples;
                RiverOutcome outcome; string detail;
                try
                {
                    work.Check();
                    if (!gridValid) throw new Stop(RiverOutcome.BudgetExceeded, "Routing lattice exceeds MaximumNodes; reduce bounds or increase CellSize.");
                    field.ResetDiagnostics();
                    var source = snapshot.Anchors.FirstOrDefault(a => a.Id == request.SourceId);
                    var mouth = snapshot.Anchors.FirstOrDefault(a => a.Id == request.MouthId);
                    if (source.Id == null || mouth.Id == null) throw new Stop(RiverOutcome.InvalidTarget, "Missing source or mouth anchor: " + request.SourceId + " -> " + request.MouthId);
                    if (!bounds.Contains(source.Position) || !bounds.Contains(mouth.Position)) throw new Stop(RiverOutcome.OutsideBounds, "Source or mouth lies outside the planning domain.");
                    if (Distance(source.Position, mouth.Position) == 0) throw new Stop(RiverOutcome.InvalidTarget, "Source and mouth share a horizontal location.");
                    if (!field.TryPoint(source.Position.X, source.Position.Z, out var start)
                        || !field.TryPoint(mouth.Position.X, mouth.Position.Z, out var end))
                        throw new Stop(RiverOutcome.MissingTerrain, "Terrain height unavailable at source or mouth.");
                    var route = Search(field, request, start, end, snapshot.Identity, domain);
                    if (route == null) { outcome = RiverOutcome.NoRoute; detail = "Hydraulic dead end; no full-footprint downhill route. " + field.Diagnostics; }
                    else
                    {
                        routes.Add(route); field.Add(route);
                        outcome = RiverOutcome.Connected; detail = "Connected source to mouth with rounded authoritative geometry; final full bed/bank footprint, downhill water/bed and hard exclusions validated. Terrain unchanged.";
                    }
                }
                catch (Stop stop) { outcome = stop.Outcome; detail = stop.Message; }
                reports.Add(new RiverReport(request, outcome, detail, work.Nodes - beforeNodes, work.Samples - beforeSamples,
                    identity, snapshot.Identity, profile.Fingerprint));
            }
            return new RiverPlan(snapshot.Identity, domain, profile, routes, reports);
        }

        private static RiverRoute Search(Field f, RiverRequest request, WorldPoint start, WorldPoint end, PlanIdentity core, string domain)
        {
            int source = f.Count, mouth = source + 1;
            var best = Enumerable.Repeat(double.PositiveInfinity, f.Count + 2).ToArray();
            var parents = Enumerable.Repeat(-1, f.Count + 2).ToArray();
            var edges = new Edge[f.Count + 2]; var closed = new bool[f.Count + 2];
            var queue = new Heap(); best[source] = 0; queue.Push(source, Distance(start, end), 0);
            var startLinks = f.Links(start).ToArray(); var endLinks = new HashSet<int>(f.Links(end));
            WorldPoint Position(int n) => n == source ? start : n == mouth ? end : f.Position(n);
            while (queue.Count != 0)
            {
                f.Work.Check(); var current = queue.Pop();
                if (closed[current.Node] || current.Cost != best[current.Node]) continue;
                f.Work.Expand(); closed[current.Node] = true;
                if (current.Node == mouth)
                {
                    var chain = new List<Edge>();
                    for (int n = mouth; n != source; n = parents[n]) chain.Add(edges[n]);
                    chain.Reverse();
                    return RoundAndBuild(request, chain, core, domain, f);
                }
                IEnumerable<int> neighbors = current.Node == source ? startLinks : f.Neighbors(current.Node);
                if ((current.Node == source && Distance(start, end) <= f.Profile.CellSize * 2) || endLinks.Contains(current.Node))
                    neighbors = neighbors.Concat(new[] { mouth });
                foreach (int next in neighbors)
                {
                    f.Work.Check();
                    if (closed[next]) continue;
                    var a = Position(current.Node); var b = Position(next);
                    // Coincident lattice/anchor nodes are links only, never zero-length geometry.
                    var edge = Distance(a, b) == 0 ? new Edge(new[] { a }, 0, a.Y + f.Profile.SurfaceOffset) : f.TryEdge(a, b);
                    if (edge == null) continue;
                    double cost = current.Cost + edge.Cost;
                    if (!RiverHash.Finite(cost)) { f.Reject("cost overflow"); continue; }
                    if (cost >= best[next]) continue;
                    best[next] = cost; parents[next] = current.Node; edges[next] = edge;
                    queue.Push(next, cost + Distance(b, end), cost);
                }
            }
            return null;
        }

        private static RiverRoute RoundAndBuild(RiverRequest request, List<Edge> chain, PlanIdentity core, string domain, Field field)
        {
            var work = field.Work; var p = field.Profile;
            // Search links may coincide with anchors. Keep only geometric edges and their exact endpoints.
            chain = chain.Where(e => e.Points.Length > 1).ToList();
            var controls = new List<WorldPoint> { chain[0].Points[0] };
            // At most sixteen candidate shortcuts per retained control. Full footprint validation and
            // cost comparison preserve protection and the search's landscape/soft-reservation preference.
            for (int first = 0; first < chain.Count;)
            {
                work.Check(); int chosen = first; double originalCost = 0;
                int limit = Math.Min(chain.Count, first + 16);
                for (int j = first; j < limit; j++) originalCost += chain[j].Cost;
                for (int last = limit - 1; last > first; last--)
                {
                    var shortcut = field.TryEdge(controls[controls.Count - 1], chain[last].Points.Last());
                    if (shortcut != null && shortcut.Cost <= originalCost + 1e-10 * Math.Max(1, originalCost))
                    { chosen = last; break; }
                    originalCost -= chain[last].Cost;
                }
                controls.Add(chain[chosen].Points.Last()); first = chosen + 1;
                if (controls.Count > MaximumCurvePoints)
                    throw new Stop(RiverOutcome.BudgetExceeded, "Rounded river control point budget exhausted.");
            }
            double spacing = Math.Min(p.SampleSpacing, Math.Min(p.Width / 4, p.CellSize / 4));
            double radius = Math.Max(p.MinimumBendRadius * 2, Math.Max((p.Width / 2 + p.BankWidth) * 3, p.CellSize));
            if (!RiverHash.Positive(spacing))
                throw new Stop(RiverOutcome.BudgetExceeded, "Rounded river spacing is below finite sampling resolution.");
            for (int attempt = 0; attempt < 5; attempt++, radius *= .5)
            {
                work.Check();
                IReadOnlyList<WorldPoint> curve;
                bool rounded;
                try
                {
                    rounded = PlanarCurve.TryRound(controls, radius, spacing, out curve, MaximumCurvePoints, work.Cancellation);
                    if (rounded && !PlanarCurve.IsSimple(curve, 1000000, work.Cancellation))
                    { field.Reject("rounded curve self intersection or comparison budget"); continue; }
                }
                catch (OperationCanceledException) { work.Check(); throw; }
                work.Check();
                if (!rounded)
                { field.Reject("rounded curve sampling or geometry limits"); continue; }
                if (curve.Count < 2 || curve.Count > MaximumCurvePoints
                    || curve[0].X != controls[0].X || curve[0].Z != controls[0].Z
                    || curve[curve.Count - 1].X != controls[controls.Count - 1].X
                    || curve[curve.Count - 1].Z != controls[controls.Count - 1].Z)
                { field.Reject("rounded curve invalid endpoints or point count"); continue; }
                // Core's Y interpolation is not authoritative: TryEdge resamples actual terrain and
                // validates every final bed/bank rectangle, downhill water/bed and excavation limits.
                var final = new List<Edge>(); double cost = 0;
                for (int i = 1; i < curve.Count; i++)
                {
                    work.Check(); var edge = field.TryEdge(curve[i - 1], curve[i]);
                    if (edge == null) { final.Clear(); break; }
                    final.Add(edge); cost += edge.Cost;
                }
                if (final.Count == curve.Count - 1 && RiverHash.Finite(cost))
                {
                    var built = Build(request, final, cost, core, domain, field);
                    if (built != null) return built;
                }
            }
            throw new Stop(RiverOutcome.NoRoute, "Rounded river rejected after five radius attempts; no sharp-corner fallback. " + field.Diagnostics);
        }

        private static RiverRoute Build(RiverRequest request, List<Edge> edges, double cost, PlanIdentity core,
            string domain, Field field)
        {
            var p = field.Profile; var work = field.Work;
            var terrain = new List<WorldPoint>(); var bank = new List<double>();
            foreach (var edge in edges)
            {
                work.Check();
                foreach (var point in edge.Points)
                {
                    if (terrain.Count != 0 && Distance(terrain[terrain.Count - 1], point) == 0)
                    { bank[bank.Count - 1] = Math.Max(bank[bank.Count - 1], edge.BankHeight); continue; }
                    if (terrain.Count >= MaximumCurvePoints)
                        throw new Stop(RiverOutcome.BudgetExceeded, "Final river point budget exhausted.");
                    terrain.Add(point); bank.Add(edge.BankHeight);
                }
            }
            // Check the actual authoritative samples, including joins between simplified straight
            // spans and arcs. Numeric subdivision must never reintroduce a visible sharp heading.
            for (int i = 2; i < terrain.Count; i++)
            {
                work.Check(); var a = terrain[i - 2]; var b = terrain[i - 1]; var c = terrain[i];
                double cosine = ((b.X - a.X) * (c.X - b.X) + (b.Z - a.Z) * (c.Z - b.Z))
                    / (Distance(a, b) * Distance(b, c));
                if (!RiverHash.Finite(cosine) || cosine < Math.Cos(Math.PI / 12))
                    throw new Stop(RiverOutcome.NoRoute, "Rounded river final heading exceeds 15 degrees; no sharp-corner fallback.");
            }
            // Use final terrain-resampled XZ stations, including end zones; no unknown external directions.
            if (terrain.Count > 2)
            {
                double arm = Math.Max(1, p.Width / 2 + p.BankWidth);
                RouteBendMeasurement bend; bool measured;
                try { measured = RouteBendInspection.TryMeasureWithEndpoints(terrain, arm, out bend,
                    MaximumCurvePoints, work.Cancellation); }
                catch (OperationCanceledException) { work.Check(); throw; }
                if (!measured)
                { field.Reject("sampled bend radius measurement failed"); return null; }
                if (bend.MinimumRadius + 1e-8 * Math.Max(1, p.MinimumBendRadius) < p.MinimumBendRadius)
                {
                    field.Reject(FormattableString.Invariant($"sampled bend radius {bend.MinimumRadius:R} m at vertex {bend.MinimumRadiusVertex} below required {p.MinimumBendRadius:R} m"));
                    return null;
                }
            }
            var spans = new List<(int First, int Last)>();
            for (int first = 0; first < terrain.Count - 1;)
            {
                work.Check(); int last = first + 1;
                while (last + 1 < terrain.Count && Linear(terrain[first], terrain[last], terrain[last + 1])) { work.Check(); last++; }
                if (last > first + 1 && terrain[first].X != terrain[last].X && terrain[first].Z != terrain[last].Z)
                {
                    // A rotated strip's merged Core AABB contains extra corners. Validate that larger envelope
                    // before offering a long diagonal bridge span; otherwise retain short validated offers.
                    var merged = field.TryEdge(terrain[first], terrain[last]);
                    if (merged == null) last = first + 1;
                    else for (int i = first; i <= last; i++) { work.Check(); bank[i] = Math.Max(bank[i], merged.BankHeight); }
                }
                spans.Add((first, last)); first = last;
            }
            // Continuous downhill bank envelope above all sampled terrain, without modifying the source.
            for (int i = bank.Count - 2; i >= 0; --i) bank[i] = Math.Max(bank[i], bank[i + 1]);
            int count = terrain.Count; var bed = new WorldPoint[count]; var water = new WorldPoint[count];
            var left = new WorldPoint[count]; var right = new WorldPoint[count];
            double radius = p.Width / 2 + p.BankWidth;
            for (int i = 0; i < count; i++)
            {
                work.Check(); var point = terrain[i]; double level = point.Y + p.SurfaceOffset - p.WaterInset;
                bed[i] = new WorldPoint(point.X, level - p.Depth, point.Z); water[i] = new WorldPoint(point.X, level, point.Z);
                var a = terrain[Math.Max(0, i - 1)]; var b = terrain[Math.Min(count - 1, i + 1)];
                double distance = Distance(a, b), dx = (b.X - a.X) / distance, dz = (b.Z - a.Z) / distance;
                // Visible seams follow captured local terrain; bank[] remains the conservative
                // downhill clearance envelope for crossing negotiation, not visible geometry.
                if (!field.TryPoint(point.X - dz * radius, point.Z + dx * radius, out var localLeft)
                    || !field.TryPoint(point.X + dz * radius, point.Z - dx * radius, out var localRight))
                { field.Reject("missing final bank seam terrain"); return null; }
                left[i] = new WorldPoint(localLeft.X, Math.Max(localLeft.Y, level), localLeft.Z);
                right[i] = new WorldPoint(localRight.X, Math.Max(localRight.Y, level), localRight.Z);
                bank[i] = Math.Max(bank[i], Math.Max(left[i].Y, right[i].Y));
            }
            // Include the exact final side samples as well as the rectangular footprint samples.
            // Downstream maxima propagate upstream only in the independent clearance envelope.
            for (int i = bank.Count - 2; i >= 0; --i) bank[i] = Math.Max(bank[i], bank[i + 1]);
            try
            {
                if (!RiverFootprint.IsValid(water, left, right, work.Cancellation))
                { field.Reject("folded or self-overlapping river footprint"); return null; }
            }
            catch (OperationCanceledException) { work.Check(); throw; }
            var identity = new PlanIdentity(core.Seed, core.Revision, AlgorithmVersion, domain,
                RiverHash.Digest(w => { RiverHash.Identity(w, core); w.Write(domain); }));
            var offers = new List<RiverOffer>();
            // Merge straight, height-linear sampled spans for useful bank-to-bank bridge candidates.
            foreach (var span in spans)
            {
                work.Check(); int first = span.First, last = span.Last;
                string id = "river:" + RiverHash.Digest(w => { w.Write(domain); w.Write(request.Id); w.Write(first); });
                // Protect sampled wet shoulders, rather than classifying the whole dry upper bank as water.
                double wetRadius = p.Width / 2;
                if (p.WaterInset > 0)
                    for (int i = first; i <= last; i++)
                    {
                        work.Check();
                        foreach (var side in new[] { left[i], right[i] })
                        {
                            double t = (water[i].Y-bed[i].Y)/(side.Y-bed[i].Y);
                            double ratio = (p.Width/2)/radius;
                            wetRadius = Math.Max(wetRadius,Distance(water[i],side)*(ratio+(1-ratio)*t));
                        }
                    }
                var footprint = Envelope(water[first], water[last], wetRadius);
                var banks = Envelope(water[first], water[last], radius);
                double dropPerMetre = (water[first].Y - water[last].Y) / Distance(water[first], water[last]);
                double projection = (Math.Abs(water[last].X - water[first].X) + Math.Abs(water[last].Z - water[first].Z))
                    / Distance(water[first], water[last]);
                // Conservative heights also include the extrapolated longitudinal end caps of the rectangular bed.
                double waterHeight = water[first].Y + dropPerMetre * wetRadius * projection;
                double bedHeight = bed[last].Y - dropPerMetre * wetRadius * projection;
                if (!RiverHash.Finite(waterHeight) || !RiverHash.Finite(bedHeight))
                    throw new Stop(RiverOutcome.NoRoute, "Exported height envelope exceeds numeric range.");
                double bankHeight = Math.Max(waterHeight, bank[first]);
                var corridor = new WaterCorridor(id, footprint, banks, bedHeight, waterHeight, bankHeight);
                var crossing = new CrossingCandidate(id + ":crossing", id, banks, CrossingKind.Bridge, p.BridgeClearance);
                var reservations = new[]
                {
                    new AreaReservation(id + ":bed", id, footprint, ReservationStrength.Hard, ReservationPurpose.RiverBed),
                    new AreaReservation(id + ":bank", id, banks, ReservationStrength.Soft, ReservationPurpose.RiverBank, p.BankTransitionCost)
                };
                offers.Add(new RiverOffer(identity, corridor, new[] { crossing }, reservations));
            }
            return new RiverRoute(request, bed, water, left, right, offers, cost);
        }

        private static bool Linear(WorldPoint a, WorldPoint b, WorldPoint c)
        {
            double ab = Distance(a, b), bc = Distance(b, c), ac = Distance(a, c);
            // Only exactly collinear in X/Z: otherwise a merged rectangle might exceed validated footprints.
            return (b.X - a.X) * (c.Z - b.Z) == (b.Z - a.Z) * (c.X - b.X)
                && ac > ab && ac > bc && Math.Abs(b.Y - (a.Y + (c.Y - a.Y) * (ab / ac))) <= 1e-12;
        }
        private static double Distance(WorldPoint a, WorldPoint b)
        { double dx = a.X - b.X, dz = a.Z - b.Z; return Math.Sqrt(dx * dx + dz * dz); }
        private static WorldBounds Envelope(WorldPoint a, WorldPoint b, double radius) =>
            new WorldBounds(Math.Min(a.X, b.X) - radius, Math.Min(a.Z, b.Z) - radius, Math.Max(a.X, b.X) + radius, Math.Max(a.Z, b.Z) + radius);

        private sealed class Edge
        {
            internal readonly WorldPoint[] Points;
            internal readonly double Cost, BankHeight;
            internal Edge(WorldPoint[] points, double cost, double bankHeight) { Points = points; Cost = cost; BankHeight = bankHeight; }
        }
        private sealed class Stop : Exception
        {
            internal readonly RiverOutcome Outcome;
            internal Stop(RiverOutcome outcome, string detail) : base(detail) { Outcome = outcome; }
        }
        private sealed class Work
        {
            private readonly RiverProfile profile;
            private readonly CancellationToken cancellation;
            internal CancellationToken Cancellation => cancellation;
            internal int Nodes, Samples;
            internal Work(RiverProfile profile, CancellationToken cancellation) { this.profile = profile; this.cancellation = cancellation; }
            internal void Check() { if (cancellation.IsCancellationRequested) throw new Stop(RiverOutcome.Cancelled, "Planning cancelled; no partial route published."); }
            internal void Expand()
            {
                Check(); if (Nodes >= profile.TotalSearchBudget) throw new Stop(RiverOutcome.BudgetExceeded, "Shared batch expansion budget exhausted."); Nodes++;
            }
            internal void Sample()
            {
                Check(); if (Samples >= profile.TotalSampleBudget) throw new Stop(RiverOutcome.BudgetExceeded, "Shared batch terrain sample budget exhausted."); Samples++;
            }
        }

        private sealed class Field
        {
            internal readonly RiverProfile Profile;
            internal readonly Work Work;
            private readonly PlanSnapshot snapshot;
            private readonly WorldBounds bounds;
            private readonly int nx, nz;
            private readonly double stepX, stepZ;
            private readonly List<AreaReservation> areas;
            private readonly LandscapeSample[][] landscape;
            private readonly SortedDictionary<string, int> rejected = new SortedDictionary<string, int>(StringComparer.Ordinal);
            internal int Count => nx * nz;
            internal string Diagnostics => string.Join("; ", rejected.Select(p => p.Key + "=" + p.Value.ToString(CultureInfo.InvariantCulture)));
            internal void ResetDiagnostics() => rejected.Clear();
            internal void Reject(string why) { rejected.TryGetValue(why, out int n); rejected[why] = n + 1; }
            internal Field(PlanSnapshot snapshot, RiverProfile profile, WorldBounds bounds, int nx, int nz, Work work)
            {
                this.snapshot = snapshot; Profile = profile; this.bounds = bounds; this.nx = nx; this.nz = nz; Work = work;
                stepX = (bounds.MaxX - bounds.MinX) / (nx - 1); stepZ = (bounds.MaxZ - bounds.MinZ) / (nz - 1);
                areas = snapshot.Reservations.ToList();
                // Core snapshots are retained, never copied into a second region schema. Sources blend additively;
                // within each source use nearest captured sample; positional ties use canonical sample contents.
                landscape = snapshot.Landscape.GroupBy(s => s.Regions.SourceId, StringComparer.Ordinal)
                    .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.OrderBy(SampleKey, StringComparer.Ordinal).ToArray()).ToArray();
            }
            private static string SampleKey(LandscapeSample sample) => RiverHash.Digest(w =>
            {
                RiverHash.Number(w, sample.Position.X); RiverHash.Number(w, sample.Position.Z);
                w.Write(sample.Regions.SourceId); w.Write(sample.Regions.Revision); w.Write(sample.Regions.Samples.Count);
                foreach (var r in sample.Regions.Samples) { w.Write(r.Definition.Id); RiverHash.Number(w, r.Weight); w.Write(r.Definition.Priority); }
            });
            internal void Add(RiverRoute route) { areas.AddRange(route.Offers.SelectMany(o => o.Reservations)); }
            internal WorldPoint Position(int n) => new WorldPoint(bounds.MinX + (n % nx) * stepX, 0, bounds.MinZ + (n / nx) * stepZ);
            internal bool TryPoint(double x, double z, out WorldPoint point)
            {
                Work.Sample(); point = default;
                if (!snapshot.Terrain.Heights.TryGetHeight(x, z, out double y) || !RiverHash.Finite(y)
                    || !RiverHash.Finite(y + Profile.SurfaceOffset) || !RiverHash.Finite(y + Profile.SurfaceOffset - Profile.Depth)) return false;
                point = new WorldPoint(x, y, z); return true;
            }
            internal IEnumerable<int> Links(WorldPoint point)
            {
                int x = Math.Min(nx - 2, Math.Max(0, (int)((point.X - bounds.MinX) / stepX)));
                int z = Math.Min(nz - 2, Math.Max(0, (int)((point.Z - bounds.MinZ) / stepZ)));
                yield return z * nx + x; yield return z * nx + x + 1;
                yield return (z + 1) * nx + x; yield return (z + 1) * nx + x + 1;
            }
            internal IEnumerable<int> Neighbors(int n)
            {
                int x = n % nx, z = n / nx;
                for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                    if ((dx != 0 || dz != 0) && x + dx >= 0 && x + dx < nx && z + dz >= 0 && z + dz < nz)
                        yield return (z + dz) * nx + x + dx;
            }
            private int Steps(double length)
            {
                double steps = Math.Max(1, Math.Ceiling(length / Profile.SampleSpacing));
                if (!RiverHash.Finite(steps) || steps > Profile.TotalSampleBudget)
                    throw new Stop(RiverOutcome.BudgetExceeded, "Footprint resolution exceeds terrain sample budget.");
                return (int)steps;
            }
            internal Edge TryEdge(WorldPoint rawA, WorldPoint rawB)
            {
                double length = Distance(rawA, rawB);
                if (!RiverHash.Positive(length)) { Reject("invalid segment length"); return null; }
                double radius = Profile.Width / 2 + Profile.BankWidth;
                // Check numeric bounds before constructing Core's finite rectangle.
                double minX = Math.Min(rawA.X, rawB.X) - radius, maxX = Math.Max(rawA.X, rawB.X) + radius;
                double minZ = Math.Min(rawA.Z, rawB.Z) - radius, maxZ = Math.Max(rawA.Z, rawB.Z) + radius;
                if (!RiverHash.Finite(minX) || !RiverHash.Finite(maxX) || !RiverHash.Finite(minZ) || !RiverHash.Finite(maxZ)
                    || minX < bounds.MinX || maxX > bounds.MaxX || minZ < bounds.MinZ || maxZ > bounds.MaxZ
                    || minX >= maxX || minZ >= maxZ) { Reject("bank footprint outside bounds"); return null; }
                var banks = new WorldBounds(minX, minZ, maxX, maxZ);
                double soft = 0;
                foreach (var area in areas)
                {
                    Work.Check(); if (!area.Bounds.Overlaps(banks)) continue;
                    if (area.Strength == ReservationStrength.Hard) { Reject("hard reservation " + area.Id); return null; }
                    soft += area.TransitionCost;
                }
                int along = Steps(length);
                if ((long)along + 1 > Profile.TotalSampleBudget - Work.Samples)
                    throw new Stop(RiverOutcome.BudgetExceeded, "Centreline resolution exceeds remaining terrain sample budget.");
                var points = new WorldPoint[along + 1];
                double cost = 0, bankHeight = double.NegativeInfinity;
                for (int i = 0; i <= along; i++)
                {
                    double t = (double)i / along;
                    // Exact endpoint coordinates ensure adjoining segments share exactly the same height sample.
                    double x = i == along ? rawB.X : i == 0 ? rawA.X : rawA.X + (rawB.X - rawA.X) * t;
                    double z = i == along ? rawB.Z : i == 0 ? rawA.Z : rawA.Z + (rawB.Z - rawA.Z) * t;
                    if (!TryPoint(x, z, out points[i])) { Reject("missing centre terrain"); return null; }
                    bankHeight = Math.Max(bankHeight, points[i].Y + Profile.SurfaceOffset);
                    if (i != 0)
                    {
                        double run = Distance(points[i - 1], points[i]); double drop = points[i - 1].Y - points[i].Y;
                        if (drop < 0 || !RiverHash.Positive(run) || drop / run > Profile.MaximumSlope)
                        { Reject(drop < 0 ? "uphill water/bed" : "maximum slope"); return null; }
                        cost += run * (1 + RegionCost(points[i - 1]) / 2 + RegionCost(points[i]) / 2);
                    }
                }
                // Validate every sampled point of the complete rectangular bank envelope. Inside bed,
                // terrain must not pierce the piecewise water surface unless the caller explicitly allows
                // excavation, capped at Depth above water. Sampling and centreline downhill rules are unchanged.
                // Bank terrain raises the bank envelope only; this planner never writes terrain.
                double maximumCut = Profile.AllowExcavation ? Profile.Depth + Profile.WaterInset : 0;
                var bedBounds = Envelope(rawA, rawB, Profile.Width / 2);
                int sx = Steps(maxX - minX), sz = Steps(maxZ - minZ);
                // Preflight prevents allocating/entering a giant product with a small remaining batch budget.
                if ((long)(sx + 1) * (sz + 1) > Profile.TotalSampleBudget - Work.Samples)
                    throw new Stop(RiverOutcome.BudgetExceeded, "Full footprint exceeds remaining terrain sample budget.");
                for (int iz = 0; iz <= sz; iz++) for (int ix = 0; ix <= sx; ix++)
                {
                    double x = minX + (maxX - minX) * ((double)ix / sx), z = minZ + (maxZ - minZ) * ((double)iz / sz);
                    if (!TryPoint(x, z, out var ground)) { Reject("missing footprint terrain"); return null; }
                    bankHeight = Math.Max(bankHeight, ground.Y);
                    if (!bedBounds.Contains(ground)) continue;
                    double t = ((x - rawA.X) * (rawB.X - rawA.X) + (z - rawA.Z) * (rawB.Z - rawA.Z)) / (length * length);
                    double sample = t * along;
                    int low = Math.Max(0, Math.Min(along - 1, (int)sample)); double fraction = sample - low;
                    double level = points[low].Y + (points[low + 1].Y - points[low].Y) * fraction + Profile.SurfaceOffset - Profile.WaterInset;
                    if (ground.Y > level && ground.Y - level > maximumCut)
                    { Reject(Profile.AllowExcavation ? "maximum excavation cut exceeded" : "terrain pierces water footprint"); return null; }
                }
                // Also sample bed rectangle edges explicitly; a bank grid may otherwise miss a narrow bed edge.
                int bx = Steps(bedBounds.MaxX - bedBounds.MinX), bz = Steps(bedBounds.MaxZ - bedBounds.MinZ);
                for (int iz = 0; iz <= bz; iz++) for (int ix = 0; ix <= bx; ix++)
                {
                    double x = bedBounds.MinX + (bedBounds.MaxX - bedBounds.MinX) * ((double)ix / bx);
                    double z = bedBounds.MinZ + (bedBounds.MaxZ - bedBounds.MinZ) * ((double)iz / bz);
                    if (!TryPoint(x, z, out var ground)) { Reject("missing bed terrain"); return null; }
                    double t = ((x - rawA.X) * (rawB.X - rawA.X) + (z - rawA.Z) * (rawB.Z - rawA.Z)) / (length * length);
                    double sample = t * along;
                    int low = Math.Max(0, Math.Min(along - 1, (int)sample));
                    double level = points[low].Y + (points[low + 1].Y - points[low].Y) * (sample - low) + Profile.SurfaceOffset - Profile.WaterInset;
                    if (ground.Y > level && ground.Y - level > maximumCut)
                    { Reject(Profile.AllowExcavation ? "maximum excavation cut exceeded" : "terrain pierces water footprint"); return null; }
                }
                cost += soft;
                if (!RiverHash.Finite(cost)) { Reject("cost overflow"); return null; }
                return new Edge(points, cost, bankHeight);
            }
            private double RegionCost(WorldPoint point)
            {
                double cost = 0;
                foreach (var source in landscape)
                {
                    LandscapeSample nearest = null; double best = double.PositiveInfinity;
                    foreach (var candidate in source)
                    {
                        Work.Check(); double distance = Distance(candidate.Position, point);
                        if (nearest == null || distance < best) { nearest = candidate; best = distance; }
                    }
                    foreach (var rule in Profile.RegionCosts) cost += nearest.Regions.GetWeight(rule.RegionId) * rule.CostPerMetre;
                }
                return cost;
            }
        }

        private sealed class Heap
        {
            internal readonly struct Entry
            {
                internal readonly int Node; internal readonly double Priority, Cost;
                internal Entry(int node, double priority, double cost) { Node = node; Priority = priority; Cost = cost; }
            }
            private readonly List<Entry> entries = new List<Entry>();
            internal int Count => entries.Count;
            private static bool Before(Entry a, Entry b) => a.Priority < b.Priority || (a.Priority == b.Priority && a.Node < b.Node);
            internal void Push(int node, double priority, double cost)
            {
                var entry = new Entry(node, priority, cost); int i = entries.Count; entries.Add(entry);
                while (i > 0) { int parent = (i - 1) / 2; if (!Before(entry, entries[parent])) break; entries[i] = entries[parent]; i = parent; }
                entries[i] = entry;
            }
            internal Entry Pop()
            {
                var first = entries[0]; var last = entries[entries.Count - 1]; entries.RemoveAt(entries.Count - 1);
                if (entries.Count == 0) return first;
                int i = 0;
                while (i * 2 + 1 < entries.Count)
                {
                    int child = i * 2 + 1;
                    if (child + 1 < entries.Count && Before(entries[child + 1], entries[child])) child++;
                    if (!Before(entries[child], last)) break;
                    entries[i] = entries[child]; i = child;
                }
                entries[i] = last; return first;
            }
        }
    }
}
