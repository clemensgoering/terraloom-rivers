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

## Automatic baked-scene rehydration (15 October 2026)

On first access after reopening, `WaterSurfaceQuery` automatically reconstructs
the immutable snapshot without generating a plan or changing root/terrain bindings.
An explicit version-1 saved publication certificate includes the plan identity,
Terrain/TerrainData references and SHA-256 evidence of the exact plan JSON, water
mesh vertices/indices/transforms/physical offset metadata, terrain transform/size,
heights and holes. Collider presence and TerrainData binding must also match.
This is integrity checking of a saved publication, not authorization of arbitrary
edited plans or a substitute for fresh planning.

The baker validates unchanged physical content before rebinding copied terrain
assets. The certificate participates in the same commit/rollback as the live
query. Legacy scenes without a certificate require regeneration. A failed restore
leaves the query null and supplies a diagnostic; it is attempted once until a new
publication is generated. Changing a cached live scene is still handled through
the normal regeneration/publication lifecycle, not per-frame content hashing.

One-time rehydration reads and hashes the saved water and full terrain data; it has
not been profiled for large streaming worlds. Per-point query lookup is unchanged.
No surface is reconstructed solely from corridor bounds or an unchecked JSON plan.

Separate Unity 6000.3.9f1 processes prove boundary-fall Bake **1/1** then reopened
query/negative cases/publication regression **6/6**, zero failed/skipped.
`LastPlan` remains null while the reopened query answers the saved physical height
and dry-land probe, showing that no regeneration or retained managed snapshot
supplies it. The expected water height is recorded from the published route before
bake, not inferred from raw terrain height. Negative checks cover edited JSON,
geometry, terrain heights and collider binding; commit/rollback tests remain green.
Evidence: `.artifacts/WaterQueryReopen-20261015/{Bake,Reopen}.xml` and matching logs.

## Remaining integration boundary

The snapshot itself remains nonserialized; only validated saved publication data
is used to reconstruct it. The active query
must be acquired with its matching publication identity. A retained old snapshot
continues describing its old world, even when a new one is published.

Core revision `9f41af8` adds the required interface. It must be included in the Core dependency revision before
shipping this Rivers change. Existing local development manifests were preserved;
package dependency pins/publication were not changed by this runtime slice.
The native roundtrip also covers one ordinary river on buyer-provided terrain:
`RiverForeignWaterQueryPersistenceTests` bakes a 32 m sloped Terrain asset in one
Unity process (**1/1**) and reopens it in a separate process (**1/1**). Core never
owns or generates that terrain. The source heights remain unchanged relative to
Unity's imported/quantized baseline. The reopened query returns the recorded
physical route height and dry-land result without regenerating `LastPlan` or
changing roots/TerrainData. Clear removes the active query and restores the
original supplied TerrainData/collider; a retained snapshot still answers its old
immutable data. No production code change was needed for this additional case.
Evidence: `.artifacts/ForeignWaterQueryReopen-20261015/{Bake,Reopen}.xml` and logs.

The shared Core/Rivers/Paths Composition boundary-fall scene is also verified:
`BoundaryFallCompositionTests` records the physical receiving-route height and
shared query fingerprint before baking the fall, downstream river and explicitly
granted timber bridge. A fresh Unity process reopens the scene and restores the
query automatically with `LastPlan` still null. Height and identity match, dry land
is dry, both generated roots/TerrainData/JSON remain unchanged, and Composition's
`ValidateCurrent` passes before and after reading the query. Standalone plan reload
is deliberately not used for the joint snapshot. Bake **1/1** and Reopen **1/1**
pass with Unity exit 0, zero failures/skips. No production changes were required.
Evidence: canonical `Integration/.artifacts/CompositionWaterQueryReopen-20261015/`
contains `Bake.xml`, `Reopen.xml` and logs. The tracked fixture source is under
`Rivers/IntegrationProject` and was mirrored into the canonical Integration project.

## Shared Composition on supplied terrain

`ForeignCompositionWaterQueryTests` covers a 96 m supplied slope, one terrain-brush
river, dry road approaches and a physical timber-truss bridge. Core never owns the
source terrain. The test first refuses the crossing without a grant, leaving no
published module roots, then explicitly approves only named bridge candidates
inside a fixed reviewed window using the scene-owned permission source. The
provided source heights remain unchanged relative to Unity's imported baseline.

With persistent material assets, separate Unity 6000.3.9f1 processes pass Bake
**1/1** and Reopen **1/1**, zero failed/skipped. The restored query matches the
recorded physical height and shared identity and reports dry land correctly.
Rivers/Paths `LastPlan` remain null, proving no standalone replanning supplies the
result. Both JSON plans, roots and TerrainData remain unchanged and
`Composition.ValidateCurrent` passes before and after querying. Ground surfaces,
one deck, one truss and two ramps survive as persistent meshes with matching
MeshColliders; the enabled reviewed permission source and approval count survive.
Clear restores the original supplied terrain/collider and removes both module
outputs and the active query; a retained immutable query still answers its old data.

Evidence: canonical `Integration/.artifacts/ForeignCompositionWaterQuery-20261015/`
contains `Bake.xml`, `Reopen.xml`, logs and the initial negative material probe.
Tracked fixture source is under `Rivers/IntegrationProject`; its canonical
Integration mirror has the same SHA-256. No product code or Epos files changed.

The initial 15 October probe identified two buyer-facing gaps: the central bake
required a Core-owned recipe/decoration, and sequential module bakers invalidated
shared transient material references. Those findings were carried from review
`work-20261015-2058-5qkp` into implementation `work-20261016-0834-3xuk`.

## Central transactional Composition bake (16 October 2026)

`IntegrationEditor.BakeGeneratedForScene` now delegates to the package's
`CompositionSceneBaker`. It accepts current shared output on supplied or owned
terrain without requiring a recipe or decoration. Existing generated decoration
is included. All new resource assets are copied first, using one dependency map
for both modules. Material aliases, Texture2D dependencies, original/carved terrain,
terrain layers, generated meshes and collider bindings remain consistent. Existing
caller assets are never overwritten; persistent parents with transient texture
dependencies are copied instead of modifying the caller's asset.

The scene swap journals serialized resource references and ownership flags,
including the saved WaterQuery certificate and Composition's captured material
bindings. Freshness is checked before and after the swap, and the physical query
certificate validates copied geometry/terrain content. Old module-owned transient
meshes/carved terrain are retired only after success. Failure before completion
restores original references before deleting this attempt's copies. Nonempty or
linked folders are refused. If cleanup itself fails, the error names retained
paths; if reference recovery fails, staged assets are retained for recovery.
This is an in-process reversible bake, not crash recovery or resource-aware Undo.
Transient non-Texture2D textures and transient terrain/detail prefabs require
separate persistence. Saving the scene remains the caller's final step.

Focused Unity 6000.3.9f1 checks: Bake/rollback/owned-decoration compatibility **2/2**,
then supplied-terrain Reopen **1/1** in a separate process, zero failures/skips.
The supplied-terrain fixture now uses the central action with a transient shared
material and Texture2D. Three injected IO failures (assets staged, bindings swapped,
before completion) restore exact terrain/query/material/root/mesh references and
remove partial assets. A real unsupported RenderTexture and an occupied directory
also fail safely. The Core-owned world with decoration passes rollback and a
successful bake while retaining `ValidateCurrent` and its decoration root/assets.
Reopen verifies the persistent shared material alias, smoothness and texture pixel
alongside the existing water/road/truss/ramp/collider checks and Clear restoration.

Evidence: canonical `Integration/.artifacts/CentralCompositionBake-20261016/`
contains `BakeAndRollback.xml`, `Reopen.xml` and logs. No Epos files or runtime
generation algorithms changed. A Core-owned decorated Save/Reopen and broader
streaming/performance cases remain separate gates; the tested separate-process
roundtrip here is the supplied-terrain scene.

These probes cover owned boundary falls and ordinary/brush supplied-terrain rivers,
including an explicit bridge in shared Composition. Curved/multi-tile cases,
Player walking after this supplied-terrain reopen, large-world startup cost and
Epos migration remain separate gates. No visual acceptance follows from persistence.
There is no visual acceptance claim here. No Epos files were changed.
