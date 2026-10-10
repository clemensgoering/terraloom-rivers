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

## Final-geometry import packets (2026-10-10)

`VerticalWaterfallPacket.Save` now exports the actual published terrain and mesh
buffers after native validation. `Capture` adds the actual three cameras and the
three completed CharacterController probes from that Player run. It does not
regenerate heights, cliff or water from another mathematical implementation.

Packet directories under `Integration/.artifacts/Evidence`:

- `vertical-packet-v2-packet`
- `nearvertical-packet-v2-packet`
- `rotatedvertical-packet-v2-packet`

Each contains `manifest.json` plus19 binary payloads. Manifest is the completion
marker, written last and removed before refreshing an existing export. A snapshot
index including manifest hashes lives in `Docs/VERTICAL-WATERFALL-PACKETS.json`.
Binary evidence stays ignored; rerun the three documented build methods and
Player arguments with the corresponding screenshot prefix to reproduce exports.
Hashes identify these measured snapshots, not promises of deterministic physics
or byte-identical rendered frames across Unity/platform versions.

Import rules (all payload hashes/lengths must pass before constructing objects):

- 513² uint16 LE normalized native heights; worldY=code/65535*32. Terrain origin
  (0,0,0), size64×32×64. X fastest, increasing worldZ. Coordinates are Unity metres.
- 512² uint8 holemask, **0=hole,1=solid**. Exactly2400 holecells per case.
- 512² uint8 native collider diagonals:0=SW-NE,1=NW-SE,255=hole. Skip holecells.
  For SW=(x,z),SE=(x+1,z),NW=(x,z+1),NE=(x+1,z+1), upward triangle indices are
  SW,NW,NE / SW,NE,SE for0; SW,NW,SE / SE,NW,NE for1. Equal-height diagonal
  candidates choose0; either then represents the same planar cell. Maximum
  measured native cell-centre reconstruction error1.4305115e-6m in all3cases.
- Four final meshes: ClosedCliff, Upper, Fall, Pool. Positions/normals float32 LE
  xyz, triangle indices int32 LE, UV float32 LE xy. Vertices are **already world
  space**, object transform identity. Mesh bounds included. Cliff UV payload is
  empty, **no hidden UV convention**. Water start/endContact arrays select the
  actual17vertex boundary rows; upper end=fall start, fall end=pool start.
- Quarter-turn already permutes heights/holes and rotates final mesh vertices
  and frame: canonical(x,y,z)→(z,y,64-x). **Do not rotate imported payload again.**
- Physical Lip/Impact differ from rendered mesh heights by explicit +.01m Y.
  Offset is **already baked into exported water vertices**; do not add it twice.
- Camera eye,target,rotation,ground,FOV55 and1600×1000 are measured at capture.
  Controller start/end/forward/parameters/140fixedsteps/actualfixedDeltaTime and
  grounded state are measured in Player, not inferred from editor ray probes.

Unity-free consumer check:

```text
python Rivers/Tools/verify_vertical_packet.py <packet-directory> [more directories]
```

Validates lengths, SHA256, bounds on payload paths, mesh index ranges/finite
buffers, actual full-width joins, masks, grounded camera heights and actual
controller drops. All3Playerpackets pass. Deliberately corrupted/truncated height
payload and directory escape each fail closed. Native fixture now roundtrips
the exported buffers against actual terrain/mesh data in each of its3cases;
4/4native tests pass (VerticalPacket-4.xml/.log). All3newbuilds/Players exit0;
logs `{Vertical,NearVertical,RotatedVertical}Packet{Build,Player}.log`.

Diagnostic captures use **unchanged cameras** and geometry: original image,
`-side/-lip/-foot-flat.png`, `-side/-lip/-foot-normals.png`. Flat pass is opaque,
unlit, no procedural foam/specular/fog; grey cliff, separate blue/cyan waterparts.
Normals pass maps normalized worldnormal to RGB=.5+.5normal. Original materials
are restored in finally and temporary instances disposed. Shader diagnostic
default0 preserves ordinary rendering, including earlier V0/V1 recipes.

Here all3lip flat/normal pairs plus normal-case original lip actually opened:
large bright upper-water region disappears in flat pass; hard surface outlines,
grey cliff top areas and small lip corner remain. This isolates a material
contribution to brightness, **does not prove no interior overlap or visual seam**.
27newPNGs generated; only those7opened images are claimed inspected this run.
V2 remains a contact experiment, upper slope0/bed1.1m/pool max4.2m, **no r2 outlet
sill**. World session received packet paths for separate Epos V2 import, keeping
r2 and legacy pilots. No Epos product files or purchased assets modified here.

The remaining small lip corner is now assigned to actual faces using
`Tools/probe_vertical_packet.py <packet-directory> --pixel 1033 731` (pixels from
top-left of the1600×1000image, samplecentre+.5). Measured camera projection and
double-sided ray/triangle intersection over authenticated final mesh buffers:
normal-case lip corner pixels1033,731 /1036,730 /1038,725 /1035,736 /1030,733
hit **Fall** triangle IDs127/95/63/127/158 (zero-based). Example127indices67,83,84,
worldX33.3125..33.5,Y11.11..11.135,Z32. Normal+Z is consistent, camera upstream
sees its **backface** because shader CullOff. At upper planeY11.21 those rays
crossX33.511..33.541, outside upper-water edgeX33.5: the rays peek around that
side, then see the curtain. This is not an unmatched top boundary vertex or
inverted individual triangle. All fall normals are checked against the expected
frame normal in each native case. The prober excludes native terrain occlusion
and is a geometry diagnostic, not a complete GPU pixel-ID pass. This explains
these sampled corner pixels; no general contact/art acceptance is implied and
no culling/camera/geometry change is made to hide the result. V0/V1 native
WaterfallPrototypeTests regression7/7 rerun after diagnostic shader change.

Next shared data contract is now implemented as `WaterfallRecipe`: see
[combined recipe](WATERFALL-RECIPE.md). It combines actual V2fall and V1pool/sill
without changing these frozen V2 scenes/packets into different comparison cases.
