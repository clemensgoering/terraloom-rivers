# Physical water surface query: first runtime slice

Implemented 14 October 2026: Core's portable `IWaterSurfaceQuery` and Rivers'
`RiverWaterSurfaceSnapshot`. The snapshot takes physical world-metre vertices and
triangle indices plus a valid `PlanIdentity`. It copies referenced triangles;
subsequent mutations to input arrays cannot change an existing query. Input must
already describe the final terrain-clipped wetted surface with rendering offsets
removed in the correct coordinate space. No Unity objects are retained.

`TryGetSurfaceHeight(x,z,out y)` returns the highest physical surface at that X/Z.
Dry points and non-finite query coordinates return false. Vertical/near-vertical
triangles are excluded individually using projected area relative to 3D area;
horizontal pool triangles beneath a waterfall remain queryable. Sloped water
uses barycentric height interpolation. This is a height query, so stacked water
volumes and immersion inside a falling curtain need a different 3D contract.

Construction uses a uniform spatial index with up to 128 subdivisions per longest
axis (one-metre minimum cell size). It accepts at most 65,536 input triangles,
1,048,576 index references and 4,096 triangles per cell. Excessive inputs fail
explicitly; a player lookup scans at most the cell's 4,096 candidates, allocates no
result collection and never traverses the entire world. These are bounded work
limits, not measured performance acceptance for a large game world.

A gameplay consumer can use the same interface independently at each probe:

```csharp
bool immersed = query.TryGetSurfaceHeight(probeX, probeZ, out double waterY)
    && probeY < waterY;
```

The portable checks exercise feet below water, head above it and a camera outside
the wet footprint. They also cover sloped heights, wet edges/dry triangle
complement, curtain/pool overlap, highest negative water height, retained snapshot
immutability, non-finite/empty inputs, spatial-cell/triangle seams, near-vertical
rejection, invalid indices/identity, density budget and cancelled construction.
All nine checks pass against the complete Core/Rivers Runtime source set using
Unity 6000.3.9f1's Roslyn compiler and Mono, with warnings treated as errors.
Core and Rivers structural audits also pass with the explicit local development
Core. The machine's standard `dotnet` command has no SDK, so SDK tests were not run.

Reproduce with an SDK using `dotnet run --project Tools/WaterSurfaceQueryChecks.csproj`,
or from the Rivers checkout using:

```powershell
./Tools/CheckWaterSurfaceQuery.ps1 -UnityData '<Unity installation>/Editor/Data'
```

## In-memory publication integration (15 October 2026)

`TerraLoomRivers.WaterSurfaceQuery` now exposes only the current committed snapshot.
Preparing a candidate builds its query after water clipping without replacing the
current reference. `PreparedPublication.CommitBindings` replaces terrain, root,
plan, JSON and query in the same synchronous binding swap. Disposing an unsealed
commit restores the old query reference along with the old world. Failed, cancelled
or stale preparation retains the old query. Successful completion replaces it;
Clear removes the current reference. Retained old snapshots remain immutable.

`RiverGeneratedGeometry` serializes explicit local-space render-offset metadata.
Ordinary route water uses zero. Fall and receiving producers share the explicit
`PreparedFallEarthworks.WaterRenderOffset`, also passed to the fall tessellator.
`RiverWaterSurfaceCapture` subtracts the metadata before the local-to-world
transform, copies final clipped water and drops all Unity references. Legacy
markers lacking metadata, unreadable meshes, invalid transforms or budget overruns
fail closed. Preparation cancellation covers the query copy/index construction.

Native Unity 6000.3.9f1 EditMode checks pass **4/4**, zero failed/skipped:
`RiverFallPublicationTests` and `RiverWaterSurfaceCaptureTests`. They verify
physical receiving height (excluding the 1 cm render offset), dry land, identity,
no query exposure during prepare, reference swap, rollback, stale/cancel/invalid
JSON preservation, retained snapshots after replacement, Clear, and physical
height under a rotated/nonuniformly scaled host. Report:
`.artifacts/WaterQueryPublication-20261015/Publication.xml`.

## Remaining integration boundary

The snapshot is deliberately nonserialized. After domain reload or opening a baked
scene, the query remains null until validated regeneration; a stale query is never
inferred from a saved root. Reconstructing it on baked reopen from jointly validated
plan/physical metadata remains a separate persistence slice. The active query
must be acquired with its matching publication identity. A retained old snapshot
continues describing its old world, even when a new one is published.

Core revision `9f41af8` adds the required interface. It must be included in the Core dependency revision before
shipping this Rivers change. Existing local development manifests were preserved;
package dependency pins/publication were not changed by this runtime slice.
There is no baked-reopen query, Player gameplay, Epos migration or visual acceptance
claim here. No Epos files were changed.
