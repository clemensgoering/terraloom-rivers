using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TerraLoom.Core;

namespace TerraLoom.Rivers
{
    public enum RiverOutcome { Connected, NoRoute, MissingTerrain, OutsideBounds, BudgetExceeded, Cancelled, InvalidTarget }

    public sealed class RiverRegionCost
    {
        public string RegionId { get; }
        public double CostPerMetre { get; }
        public RiverRegionCost(string regionId, double costPerMetre)
        {
            RiverHash.Id(regionId);
            if (!RiverHash.Finite(costPerMetre) || costPerMetre < 0) throw new ArgumentOutOfRangeException(nameof(costPerMetre));
            RegionId = regionId; CostPerMetre = costPerMetre;
        }
    }

    /// <summary>Metres and rise/run. Sampling is explicit: arbitrary unsampled terrain cannot be certified.
    /// Water follows existing terrain plus SurfaceOffset, bed is Depth below water; banks are at least water height.
    /// This is channel overlay geometry; a caller must explicitly handle optional excavation on its own terrain copy.
    /// AllowExcavation permits sampled bed-footprint terrain up to Depth above water, without changing centreline hydraulics.</summary>
    public sealed class RiverProfile
    {
        public double Width { get; }
        /// <summary>Minimum sampled XZ centreline radius; zero constructor input selects half width plus bank width, at least 0.25 m.</summary>
        public double MinimumBendRadius { get; }
        public double Depth { get; }
        public double BankWidth { get; }
        public double CellSize { get; }
        public double MaximumSlope { get; }
        public double SampleSpacing { get; }
        public int MaximumNodes { get; }
        public int TotalSearchBudget { get; }
        public int TotalSampleBudget { get; }
        public double BankTransitionCost { get; }
        public double BridgeClearance { get; }
        public double SurfaceOffset { get; }
        /// <summary>Metres below captured centre terrain, in addition to SurfaceOffset. Requires excavation and cannot exceed Depth.</summary>
        public double WaterInset { get; }
        public bool AllowExcavation { get; }
        /// <summary>Smooth cut-only terrain channel instead of visible bed/bank meshes.</summary>
        public bool TerrainBrush { get; }
        public double TerrainCellGuard { get; }
        public IReadOnlyList<RiverRegionCost> RegionCosts { get; }
        public string Fingerprint { get; }

        public RiverProfile(double width = 2, double depth = .5, double bankWidth = 1, double cellSize = 2,
            double maximumSlope = .5, double sampleSpacing = .5, int maximumNodes = 100000,
            int totalSearchBudget = 250000, int totalSampleBudget = 2000000, double bankTransitionCost = 2,
            double bridgeClearance = 1, IEnumerable<RiverRegionCost> regionCosts = null, double surfaceOffset = .02,
            bool allowExcavation = false, double minimumBendRadius = 0, double waterInset = 0, bool terrainBrush = false, double terrainCellGuard = 0)
        {
            if (!RiverHash.Positive(width) || !RiverHash.Positive(depth) || !RiverHash.Nonnegative(bankWidth)
                || !RiverHash.Positive(cellSize) || !RiverHash.Nonnegative(maximumSlope) || !RiverHash.Positive(sampleSpacing)
                || !RiverHash.Nonnegative(bankTransitionCost) || !RiverHash.Nonnegative(bridgeClearance) || !RiverHash.Nonnegative(surfaceOffset)
                || !RiverHash.Finite(width / 2 + bankWidth) || !RiverHash.Nonnegative(minimumBendRadius)
                || !RiverHash.Nonnegative(waterInset) || waterInset > depth || waterInset > 0 && !allowExcavation
                || !RiverHash.Nonnegative(terrainCellGuard) || !RiverHash.Finite(width/2+bankWidth+terrainCellGuard)
                || terrainBrush && (!allowExcavation || waterInset <= 0 || bankWidth <= 0)
                || maximumNodes < 4 || maximumNodes > 2000000
                || totalSearchBudget < 1 || totalSearchBudget > 5000000 || totalSampleBudget < 1 || totalSampleBudget > 100000000)
                throw new ArgumentException("Invalid river profile or bounded work limits.");
            Width = width; Depth = depth; BankWidth = bankWidth; CellSize = cellSize; MaximumSlope = maximumSlope;
            SampleSpacing = sampleSpacing; MaximumNodes = maximumNodes; TotalSearchBudget = totalSearchBudget;
            TotalSampleBudget = totalSampleBudget; BankTransitionCost = bankTransitionCost; BridgeClearance = bridgeClearance;
            SurfaceOffset = surfaceOffset;
            WaterInset = waterInset; TerrainBrush = terrainBrush; TerrainCellGuard = terrainCellGuard;
            AllowExcavation = allowExcavation;
            MinimumBendRadius = minimumBendRadius == 0 ? Math.Max(.25, width / 2 + bankWidth) : minimumBendRadius;
            var costs = (regionCosts ?? Array.Empty<RiverRegionCost>()).ToArray();
            if (costs.Any(c => c == null) || costs.Select(c => c.RegionId).Distinct(StringComparer.Ordinal).Count() != costs.Length)
                throw new ArgumentException("Unique nonnull region costs required.");
            RegionCosts = Array.AsReadOnly(costs.OrderBy(c => c.RegionId, StringComparer.Ordinal).ToArray());
            Fingerprint = RiverHash.Digest(w =>
            {
                w.Write("rivers-profile-v4"); w.Write(TerrainBrush); RiverHash.Number(w,TerrainCellGuard); RiverHash.Number(w, MinimumBendRadius); RiverHash.Number(w, WaterInset);
                foreach (double n in new[] { Width, Depth, BankWidth, CellSize, MaximumSlope, SampleSpacing, BankTransitionCost, BridgeClearance, SurfaceOffset }) RiverHash.Number(w, n);
                w.Write(AllowExcavation);
                w.Write(MaximumNodes); w.Write(TotalSearchBudget); w.Write(TotalSampleBudget); w.Write(RegionCosts.Count);
                foreach (var c in RegionCosts) { w.Write(c.RegionId); RiverHash.Number(w, c.CostPerMetre); }
            });
        }
    }

    public sealed class RiverRequest
    {
        public string Id { get; }
        public string SourceId { get; }
        public string MouthId { get; }
        public RiverRequest(string id, string sourceId, string mouthId)
        {
            RiverHash.Id(id); RiverHash.Id(sourceId); RiverHash.Id(mouthId);
            if (sourceId == mouthId) throw new ArgumentException("Distinct source and mouth IDs required.");
            Id = id; SourceId = sourceId; MouthId = mouthId;
        }
    }

    public sealed class RiverReport
    {
        public PlanIdentity Identity { get; }
        public PlanIdentity CoreInputIdentity { get; }
        public string ProfileFingerprint { get; }
        public RiverRequest Request { get; }
        public RiverOutcome Outcome { get; }
        public string Detail { get; }
        public int ExpandedNodes { get; }
        public int TerrainSamples { get; }
        internal RiverReport(RiverRequest request, RiverOutcome outcome, string detail, int expanded, int samples,
            PlanIdentity identity, PlanIdentity coreInputIdentity, string profileFingerprint)
        {
            Request = request; Outcome = outcome; Detail = detail; ExpandedNodes = expanded; TerrainSamples = samples;
            Identity = identity; CoreInputIdentity = coreInputIdentity; ProfileFingerprint = profileFingerprint;
        }
    }

    /// <summary>Aligned geometry. Visible bank seams follow captured local terrain (at least water height)
    /// and may rise downstream. Water and bed must descend. Core offers retain independent conservative
    /// clearance envelopes; these are not visible seam vertices.</summary>
    public sealed class RiverRoute
    {
        public RiverRequest Request { get; }
        public IReadOnlyList<WorldPoint> BedPolyline { get; }
        public IReadOnlyList<WorldPoint> WaterPolyline { get; }
        public IReadOnlyList<WorldPoint> LeftBankPolyline { get; }
        public IReadOnlyList<WorldPoint> RightBankPolyline { get; }
        public IReadOnlyList<RiverOffer> Offers { get; }
        public double Cost { get; }
        internal RiverRoute(RiverRequest request, WorldPoint[] bed, WorldPoint[] water, WorldPoint[] left,
            WorldPoint[] right, IEnumerable<RiverOffer> offers, double cost)
        {
            Request = request; BedPolyline = Array.AsReadOnly((WorldPoint[])bed.Clone());
            WaterPolyline = Array.AsReadOnly((WorldPoint[])water.Clone()); LeftBankPolyline = Array.AsReadOnly((WorldPoint[])left.Clone());
            RightBankPolyline = Array.AsReadOnly((WorldPoint[])right.Clone()); Offers = Array.AsReadOnly(offers.ToArray()); Cost = cost;
        }
    }

    public sealed class RiverPlan
    {
        public PlanIdentity Identity { get; }
        public PlanIdentity CoreInputIdentity { get; }
        public string ProfileFingerprint { get; }
        public string DomainFingerprint { get; }
        public IReadOnlyList<RiverRoute> Routes { get; }
        public IReadOnlyList<RiverReport> Reports { get; }
        public bool Complete => Reports.All(r => r.Outcome == RiverOutcome.Connected);
        internal RiverPlan(PlanIdentity core, string domain, RiverProfile profile, IEnumerable<RiverRoute> routes, IEnumerable<RiverReport> reports)
        {
            CoreInputIdentity = core; ProfileFingerprint = profile.Fingerprint; DomainFingerprint = domain;
            Identity = new PlanIdentity(core.Seed, core.Revision, RiverPlanner.AlgorithmVersion, domain,
                RiverHash.Digest(w => { RiverHash.Identity(w, core); w.Write(domain); }));
            Routes = Array.AsReadOnly(routes.ToArray()); Reports = Array.AsReadOnly(reports.ToArray());
        }
        public PlanContribution ToContribution(PlanIdentity coreInputIdentity) => ToContribution("rivers", coreInputIdentity);
        public PlanContribution ToContribution(string moduleInstanceId, PlanIdentity coreInputIdentity)
        {
            RiverHash.Id(moduleInstanceId);
            if (coreInputIdentity != CoreInputIdentity) throw new ArgumentException("Export requires the original Core input identity.");
            if (!Complete) throw new InvalidOperationException("Incomplete river plans cannot be published as complete contributions.");
            var offers = Routes.SelectMany(r => r.Offers).ToArray();
            return new PlanContribution(moduleInstanceId, coreInputIdentity, offers.SelectMany(o => o.Reservations),
                offers.Select(o => o.Corridor), offers.SelectMany(o => o.Crossings));
        }
    }

    internal static class RiverHash
    {
        internal static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        internal static bool Positive(double n) => Finite(n) && n > 0;
        internal static bool Nonnegative(double n) => Finite(n) && n >= 0;
        internal static void Id(string id) { if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Stable nonempty ID required."); }
        internal static void Number(BinaryWriter w, double n) => w.Write(n == 0 ? 0.0 : n);
        internal static void Identity(BinaryWriter w, PlanIdentity id)
        { w.Write(id.Seed); w.Write(id.Revision); w.Write(id.AlgorithmVersion); w.Write(id.ProfileVersion); w.Write(id.InputFingerprint); }
        internal static string Digest(Action<BinaryWriter> write)
        {
            using (var s = new MemoryStream()) using (var w = new BinaryWriter(s, Encoding.UTF8, true))
            {
                write(w); w.Flush();
                using (var sha = SHA256.Create()) return "sha256:" + BitConverter.ToString(sha.ComputeHash(s.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
