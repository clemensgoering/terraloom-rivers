using System;
using System.Collections.Generic;
using System.Threading;
using TerraLoom.Core;
using TerraLoom.Rivers;

internal static class WaterSurfaceQueryChecks
{
    private static readonly PlanIdentity Identity = new PlanIdentity(42, 1, "water-query-check", "physical-v1");
    private static int passed;
    private static int Main()
    {
        Check("physical sloped surface, exact edges and dry triangle complement", () =>
        {
            var q = Surface();
            Wet(q, .25, .25, 2.25); Wet(q, 0, 0, 2); Wet(q, 1, 0, 3);
            Dry(q, .8, .8); Dry(q, -.01, 0); Dry(q, 1.01, 0);
        });
        Check("feet head camera consumer independently samples actual XZ", () =>
        {
            IWaterSurfaceQuery q = Surface();
            Require(Immersed(q, new WorldPoint(.2, 2, .2)), "Feet should be immersed.");
            Require(!Immersed(q, new WorldPoint(.2, 3, .2)), "Head should be above water.");
            Require(!Immersed(q, new WorldPoint(.8, 1, .8)), "Camera outside footprint must remain dry.");
        });
        Check("vertical curtain excluded individually, overlapping pool retained", () =>
        {
            var points = new[] { new WorldPoint(0, 6, 0), new WorldPoint(0, 2, 0), new WorldPoint(0, 2, 1),
                new WorldPoint(-1, 2, 0), new WorldPoint(1, 2, 0), new WorldPoint(0, 2, 2) };
            var curtain = new RiverWaterSurfaceSnapshot(Identity, points, new[] { 0, 1, 2 });
            Dry(curtain, 0, .5);
            var pool = new RiverWaterSurfaceSnapshot(Identity, points, new[] { 0, 1, 2, 3, 4, 5 });
            Require(pool.SurfaceTriangleCount == 1, "Curtain became a height surface."); Wet(pool, 0, .5, 2);
        });
        Check("overlapping surfaces choose highest physical height", () =>
        {
            var p = new[] { new WorldPoint(0, -3, 0), new WorldPoint(1, -3, 0), new WorldPoint(0, -3, 1),
                new WorldPoint(0, -1, 0), new WorldPoint(1, -1, 0), new WorldPoint(0, -1, 1) };
            Wet(new RiverWaterSurfaceSnapshot(Identity, p, new[] { 0, 1, 2, 3, 4, 5 }), .2, .2, -1);
        });
        Check("input mutations cannot alter retained snapshot or identity", () =>
        {
            var p = new[] { new WorldPoint(0, 2, 0), new WorldPoint(1, 3, 0), new WorldPoint(0, 2, 1) };
            var indices = new[] { 0, 1, 2 }; var old = new RiverWaterSurfaceSnapshot(Identity, p, indices);
            p[0] = new WorldPoint(0, 12, 0); indices[0] = 2;
            Wet(old, 0, 0, 2); Require(old.Identity == Identity, "Identity changed.");
        });
        Check("nonfinite and empty surface queries are dry", () =>
        {
            var q = Surface(); Dry(q, double.NaN, 0); Dry(q, 0, double.PositiveInfinity);
            Dry(new RiverWaterSurfaceSnapshot(Identity, Array.Empty<WorldPoint>(), Array.Empty<int>()), 0, 0);
        });
        Check("shared spatial-cell and triangle boundaries are continuous", () =>
        {
            var p = new[] { new WorldPoint(0, 2, 0), new WorldPoint(2, 2, 0),
                new WorldPoint(0, 2, 2), new WorldPoint(2, 2, 2) };
            var q = new RiverWaterSurfaceSnapshot(Identity, p, new[] { 0, 1, 2, 2, 1, 3 });
            Wet(q, 1, 1, 2); Wet(q, 2, 2, 2); Wet(q, 1, .999999, 2);
        });
        Check("near vertical excluded, finite tilted water retained", () =>
        {
            var p = new[] { new WorldPoint(0, 0, 0), new WorldPoint(1e-10, 2, 0), new WorldPoint(0, 0, 1) };
            Require(new RiverWaterSurfaceSnapshot(Identity, p, new[] { 0, 1, 2 }).SurfaceTriangleCount == 0, "Near vertical accepted.");
            Wet(Surface(), .5, .25, 2.5);
        });
        Check("invalid input, density budget and cancellation fail closed", () =>
        {
            Throws<ArgumentException>(() => new RiverWaterSurfaceSnapshot(default, Array.Empty<WorldPoint>(), Array.Empty<int>()));
            Throws<ArgumentException>(() => new RiverWaterSurfaceSnapshot(Identity, Array.Empty<WorldPoint>(), new[] { 0, 1, 2 }));
            Throws<ArgumentException>(() => new RiverWaterSurfaceSnapshot(Identity, Array.Empty<WorldPoint>(), new[] { 0 }));
            var p = new[] { new WorldPoint(0, 2, 0), new WorldPoint(1, 2, 0), new WorldPoint(0, 2, 1) };
            var dense = new List<int>(); for (int i = 0; i <= RiverWaterSurfaceSnapshot.MaximumTrianglesPerCell; i++) dense.AddRange(new[] { 0, 1, 2 });
            Throws<ArgumentException>(() => new RiverWaterSurfaceSnapshot(Identity, p, dense));
            Throws<OperationCanceledException>(() => new RiverWaterSurfaceSnapshot(Identity, p, new[] { 0, 1, 2 }, new CancellationToken(true)));
        });
        Console.WriteLine($"Water surface query: {passed}/9 checks passed."); return 0;
    }
    private static RiverWaterSurfaceSnapshot Surface() => new RiverWaterSurfaceSnapshot(Identity,
        new[] { new WorldPoint(0, 2, 0), new WorldPoint(1, 3, 0), new WorldPoint(0, 2, 1) }, new[] { 0, 1, 2 });
    private static bool Immersed(IWaterSurfaceQuery query, WorldPoint point) =>
        query.TryGetSurfaceHeight(point.X, point.Z, out double y) && point.Y < y;
    private static void Wet(IWaterSurfaceQuery q, double x, double z, double expected)
    { Require(q.TryGetSurfaceHeight(x, z, out double y) && Math.Abs(y - expected) < 1e-9, "Physical water height differs."); }
    private static void Dry(IWaterSurfaceQuery q, double x, double z)
    { Require(!q.TryGetSurfaceHeight(x, z, out _), "Dry point reported as wet."); }
    private static void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    private static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    private static void Check(string name, Action action)
    { action(); passed++; Console.WriteLine("PASS " + name); }
}
