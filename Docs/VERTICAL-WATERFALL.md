# Bounded vertical contact experiment V2

`Runtime/WaterfallFallSurface.cs` is the small Unity-free falling surface contract.
Version2, explicit Lip/Impact, horizontal Forward and perpendicular Across, Width,
HorizontalRun and 3D ArcLength. `Sample(progress,lateralMetres)` accepts progress
0..1 and lateral +/-halfwidth. Progress0 is lip,1 impact. A zero-run drop has many
water heights at the SAME X/Z. It must never be treated as a heightfield. Explicit
flow direction keeps the cross-frame stable at vertical tangent. This bounded
planar surface is not a new hydrology/routing system; V1-r2 is left separate.

Implemented sample: Integration `VerticalWaterfallPrototype.cs`, builders
`Editor/VerticalWaterfallBuilder.cs`, tests `Tests/Editor/VerticalWaterfallTests.cs`.
The old sample's water-based terrain height and Z-strip fall are NOT reused here.
Upper reach and lower pool are separate surfaces with identical boundary vertices
at falling surface endpoints over the full3m width. UV longitudinal scale uses
3D falling length. The shared sample shader receives explicit impact flow axis;
its default XZ axis(0,1) retains previous V0/V1 behavior.

## Explicit isolated recipes

| Case | Lip world | Impact world | Forward XZ |
|---|---|---|---|
| Vertical | 32,11.2,32 | 32,8,32 | 0,1 |
| Near vertical | 32,11.2,32 | 32,8,32.025 | 0,1 |
| Quarter turn | 32,11.2,32 | 32,8,32 | 1,0 |

Drop3.2m/channel3m. Lower pool grows to4.2m width at downstream4m and returns to3m
at8m; floor depth1.1m, lower reach17m. Unlike r2, this isolated contact recipe does
not claim a separate scour/sill result. All three are explicit generated recipes;
no manually repaired scene geometry. No purchased art. Grey cliff material is
accepted for the contact experiment and does not replace the user's accepted
landscape direction. Material UV/art refinement remains separate.

Owned fresh64x64m TerrainData,513²height vertices,.125m spacing,height32m;
native512²hole cells. Hole worldX24.5..39.5,Z30.75..33.25; quarter-turn exchanges
those axes. The closed replacement solid covers exactly that15x2.5m hole.
Its floor/bottom atY3 is below visible terrain. Front cliff at local downstream
-.125m, behind the falling sheet by.125m at zero run; a free water jet is allowed.
Upstream top follows actual cross section. Height difference tapers smoothly to
zero beyond3m lateral distance at7.5m, joining terrain on both sides. Mesh includes
top, wall, sides and segmented bottom; native tests count geometric triangle
edges (after ignoring degenerate zero-area faces) and require everyedge twice.

Terrain itself stays axis-aligned in the quarter-turn case: heights/holes are
permuted and generated meshes/flow are transformed. We do NOT rely on unsupported
rotation of a Unity Terrain object. There is no invisible barrier or water
collider in this experiment: the explicit gameplay rule is to fall to the lower
solid floor. This is not a swimming, safety-barrier or general agent-navigation rule.

## Ownership and limits

Only a fresh owned terrain is supported. No foreign TerrainData or pre-existing
holes are ever written. The required native hole resolution is checked before
publication. Candidate terrain+hole+solid+water/material instances are built and
validated together; failure/cancel disposes candidate and leaves the previously
published generated subtree intact. Rebuild removes previous terrain/meshes and
their colliders. Source materials/layers are shared and unmodified. Dispose
deactivates the owned subtree before deferred Player destruction.

This is NOT a foreign-terrain hole adapter. Preserving/restoring existing external
holes, platform-specific hole capability detection and publication into a general
world host remain future work. No such support follows from owned-terrain tests.
An Epos V2 geometry import packet is not yet supplied; do not reuse the r2 packet
as vertical evidence. Epos r2 import/results remain a separate comparison.

## Executed checks and reproduce

10.10.2026: pure compile zero warnings; portable16/16 (V1ten+V2six), native V2six;
native contact4/4 (three recipes plus invalid-contact case). Actual boundary
vertices of all three water meshes agree within1e-6m across17 width positions.
Contact geometry probes cover33 width positions x129 fall progress values and
the full hole perimeter, both sides at±2mm. Maximum terrain/solid perimeter error:
vertical/quarter-turn.0004768372m; near.0004777908m. Native tests additionally
cover closed solid, unchanged outside hole, cancel/rebuild/disposal and missing
material failure preserving old output. Invalid/NaN joins fail closed. Discrete
probes are not a mathematical continuous collision certificate.

Three Windows builds/Players exit0. Each final Player exercised actual controllers
at lateral-1.25/0/+1.25m across the lip under gravity; all landed grounded below:
vertical3.16999..3.278767m,near3.16999..3.275836m,quarter-turn3.16999..3.278766m
measured downward travel. First probe exposed a real test setup issue: controller
native position had not been synchronized after creation. Fixed by disabled warp,
enable and Physics.SyncTransforms before movement; successful final runs follow
that correction. No physics result inferred from camera placement.

All nine final side/lip/foot Player pictures actually opened, under ignored
Integration/.artifacts/Evidence prefixes waterfall-vertical-v2,
waterfall-nearvertical-v2,waterfall-rotated-vertical-v2 (side uses plain.png,
others-lip.png/-foot.png). Eyes1.7m above actual collider ground,FOV55,1600x1000;
target sightline/head checks pass. Lip view still shows a bright triangular area
and hard plate borders; mesh edge checks show no lip/impact boundary displacement,
but the precise optical cause is not conclusively classified. No visual contact
acceptance is inferred solely from vertex tests.

Build methods `RuntimePlayerValidation.BuildVerticalWaterfallBatch`,
`BuildNearVerticalWaterfallBatch`, `BuildRotatedVerticalWaterfallBatch`.
Generated scenes TerraLoomVertical/TerraLoomNearVertical/TerraLoomRotatedVertical.
Player args `-terraloomSmoke -terraloomScreenshot <absolute.png>`.
Logs Integration/.artifacts/{Vertical,NearVertical,RotatedVertical}{Build,Player}.log;
native VerticalContact-4.xml/.log, and regression WaterfallV1-7.xml/.log.
Pure native `.bootstrap-tools/RiverRadiusValidation/.artifacts/VerticalFallSurface-6.xml/.log`.
Pure fixture Tools/WaterfallProfileChecks.csproj now runs16cases; native filter
TerraLoom.Rivers.Tests.WaterfallFallSurfaceTests(6), Integration filter
TerraLoom.Integration.Tests.VerticalWaterfallTests(4). Seven previous V1 prototype
tests rerun green after shared shader axis extension. Final invalid-NaN-join guard
was added after successful Player captures and is checked natively; finite valid
recipe output is unchanged.
