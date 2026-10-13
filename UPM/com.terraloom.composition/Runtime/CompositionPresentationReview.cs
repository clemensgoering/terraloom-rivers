using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TerraLoom.Core.Unity;
using TerraLoom.Paths;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;

namespace TerraLoom.Integration
{
    /// <summary>Read-only visual review of a committed plan. It never changes search costs or grants.</summary>
    internal static class CompositionPresentationReview
    {
        internal static string BridgeStatus(PathPlan plan, TerraLoomPaths paths)
        {
            int decks = plan.Routes.Sum(route => route.Surfaces.Count(surface => surface == PathSurface.Bridge));
            if (decks == 0) return "no bridge deck";
            if (!paths.HasTimberStructure) return decks + " surface-only bridge deck(s) (no physical bridge kit)";
            int bindings = plan.Routes.Sum(route => route.BridgeCrossings.Count);
            string kit = paths.TimberTrussBridge ? "TimberTrussBridge" : "TimberBeamBridge";
            return decks + " physical " + kit + " deck(s) with " + bindings + " crossing binding(s) verified at generation";
        }

        internal static string Warnings(PathPlan plan, TerraLoomWorld world, TerraLoomPaths paths, TerraLoomRivers rivers)
        {
            var warnings = new List<string>();
            int decks = plan.Routes.Sum(route => route.Surfaces.Count(surface => surface == PathSurface.Bridge));
            if (decks > 0 && !paths.HasTimberStructure)
                warnings.Add("Surface-only bridge deck: no physical timber kit is selected. This is a flat path strip; review its appearance before using it as an example.");
            if (decks > 0 && paths.HasTimberStructure && plan.Routes.Sum(route => route.BridgeCrossings.Count) < decks)
                warnings.Add("Physical bridge deck lacks a matching current crossing authorization; inspect published bridge bindings.");

            var terrain = world.Terrain;
            var origin = terrain.transform.position;
            var size = terrain.terrainData.size;
            // The margin scales with the corridor rather than a fixed percentage of world size.
            double edgeThreshold = Math.Max(6, paths.Width * 2 + rivers.BankWidth + rivers.Width * .5);
            foreach (var route in plan.Routes)
            {
                var first = route.Waypoints[0];
                var last = route.Waypoints[route.Waypoints.Count - 1];
                double direct = Math.Sqrt((last.X - first.X) * (last.X - first.X) + (last.Z - first.Z) * (last.Z - first.Z));
                double length = 0;
                for (int i = 1; i < route.Waypoints.Count; i++)
                {
                    var a = route.Waypoints[i - 1]; var b = route.Waypoints[i];
                    length += Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
                }
                // Ratio 1.5 plus 10 m absolute excess avoids noise on short local connections.
                if (direct > 1e-6 && length / direct > 1.5 && length - direct > 10)
                    warnings.Add(string.Format(CultureInfo.InvariantCulture,
                        "Path {0} from {1} ({2:F2},{3:F2}) to {4} ({5:F2},{6:F2}) travels {7:F2} m versus {8:F2} m direct ({9:F2}x); review detour and crossing costs.",
                        route.Connection.Id, route.Connection.StartId, first.X, first.Z,
                        route.Connection.EndId, last.X, last.Z, length, direct, length / direct));
                for (int i = 0; i < route.Surfaces.Count; i++)
                {
                    if (route.Surfaces[i] != PathSurface.Bridge) continue;
                    var a = route.Waypoints[i]; var b = route.Waypoints[i + 1];
                    double dx = b.X - a.X, dz = b.Z - a.Z, span = Math.Sqrt(dx * dx + dz * dz);
                    double padX = Math.Abs(dz) / span * paths.Width * .5;
                    double padZ = Math.Abs(dx) / span * paths.Width * .5;
                    double minX = Math.Min(a.X, b.X) - padX, maxX = Math.Max(a.X, b.X) + padX;
                    double minZ = Math.Min(a.Z, b.Z) - padZ, maxZ = Math.Max(a.Z, b.Z) + padZ;
                    double edge = Math.Min(Math.Min(minX - origin.x, origin.x + size.x - maxX),
                        Math.Min(minZ - origin.z, origin.z + size.z - maxZ));
                    if (edge < edgeThreshold)
                        warnings.Add(string.Format(CultureInfo.InvariantCulture,
                            "Path {0} bridge deck at X={1:F2}, Z={2:F2} is {3:F2} m from terrain edge (review margin {4:F2} m); inspect landings and camera views.",
                            route.Connection.Id, (a.X + b.X) * .5, (a.Z + b.Z) * .5, edge, edgeThreshold));
                }
            }
            return string.Join("\n", warnings);
        }
    }
}
