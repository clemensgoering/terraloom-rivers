# Combined waterfall data recipe

`WaterfallRecipe` is an immutable, Unity-free Rivers contract combining an explicit
upper reach, `WaterfallFallSurface` V2, a level receiving pool and submerged V1
outlet sill. Manual placement and seeded planners pass the same physical anchors
and dimensions to the same constructor. No editor-only alternative algorithm.

```csharp
var recipe = new WaterfallRecipe(
    lip: new WorldPoint(32, 11.2, 32),
    impact: new WorldPoint(32, 8, 32),
    pool: new WorldPoint(32, 8, 33.7),
    outletSill: new WorldPoint(32, 8, 37.7),
    forwardX: 0, forwardZ: 1,
    channelWidth: 3, poolWidth: 4.2,
    channelDepth: .75, poolDepth: 1.1, outletSillDepth: .5,
    upstreamSlope: .012, downstreamSlope: .035,
    outletTransitionLength: 3);

var upper = recipe.SampleUpperSurface(distance: 0, lateral: .75);
var fallLip = recipe.Fall.Sample(progress: 0, lateral: .75);
var fallImpact = recipe.Fall.Sample(progress: 1, lateral: .75);
var lower = recipe.SampleLowerSurface(station: 0, lateral: .75);
// upper == fallLip; fallImpact == lower. No render offset in physical data.
var poolCentre = recipe.SampleLowerReach(recipe.PoolStation);
var sill = recipe.SampleLowerReach(recipe.OutletStation);
```

Three explicit sampling domains avoid the vertical X/Z-height ambiguity:

- Upper distance:0atlip, increases **upstream**. Water height increases with
  upstreamSlope, fixed channel width/depth. Samples identify Upstream section.
- Fall progress:0atlip,1atimpact. Actual planar3Dfall supports exactly zero run
  and near-vertical run; explicit Forward/Across remain stable. Fall UV length
  can use `Fall.DistanceAt(progress)`, no artificial nonzero horizontal distance.
- Lower station:0atimpact, increases **downstream**. PoolStation/OutletStation
  are measured from actual impact, **not from lip**. Pool is level until sill;
  width rises smoothly to PoolWidth then returns to ChannelWidth. CentreDepth
  remains PoolDepth until pool centre, then rises to OutletSillDepth. After sill,
  water grade transitions smoothly to DownstreamSlope, bed depth to ChannelDepth.

All anchors share the explicit straight horizontal axis; pool/sill match impactY
and are ordered with gaps>.01m. PoolWidth1–1.5×channel; poolDepth>=channelDepth,
0<sillDepth<=channelDepth. Finite inputs within±1e9; upper/lower sample distances
0..1e6m; signed lateral limited by the sampled half width. Arbitrary rigid yaw
and translation work in this pure data contract (not a claim about terrain
adapter rotation). Invalid anchors/levels/depths/slopes/queries fail explicitly.

`WaterfallPoolShape` is internal shared V1/V2 arithmetic; there is no virtual lip
or fabricated fall run to make V1 accept a vertical fall. Existing V1 station
origin, operation order and public API remain intact; historical V0 remains intact.
60,002 V0/V1 samples compared bit-for-bit against source from published parent
5236b1672b7023c2b7ef187da399bba3829da83d: identical coordinates, width, depth,
section. Local frozen-reference harness is ignored development evidence.

Validation: pure runtime compile0warnings/errors, portable24/24 (10V1+6Fall+8Recipe),
native same24/24 in isolated RiverRadiusValidation, existing integration11/11
(7V0/V1+4V2 contact/lifecycle/export). Recipe cases include full-width joins for
run0/.025/3.3, pool/sill anchors/depths,10,001receiving samples vsV1, arbitrary
yaw/translation, outlet tangent continuity and invalid inputs. Evidence:
`.bootstrap-tools/RiverRadiusValidation/.artifacts/CombinedWaterfall-24.xml/.log`,
`Integration/.artifacts/WaterfallCombinedRegression-11.xml/.log`.

This step delivers **data**, not a new visually accepted scene. Existing r2 and
V2 geometry packets deliberately remain separate historical comparison cases.
Next consumer work must build/export a new combined case from this recipe,
including actual terrain/solid/water/contact buffers and same-camera validation.
Terrain bed cross-section, cliff design, UV/material adapters, impact effects,
terrain-hole ownership, site planning/rights and gameplay are not inferred from
this contract. No general foreign-terrain writer or automatic waterfall siting
is added. Shader/mesh adapters must consume physical intent and apply any render
offset explicitly once, preserving both lip and impact contacts.

Epos final V2 comparison (World-reported):3Edit+1Play pass, all9controllers grounded,
max endpoint discrepancy.173092mm, maxeye.073671mm; nativeground/contact0m.
Here actually opened only normal-case side/lip/foot3of9finalimages from
20261010-102357Z-PlayMode-0_Game and read vertical-result.json. Blue flat curtain,
technical cliff panels, upper U appearance, foam rails and transitions remain
visually unresolved. ShaderSource review from World reports **no TFD vertex
displacement**; scene-colour/refraction appearance was the issue. Epos adapter
tests belong to Epos; they are not TerraLoom tests or final art approval.

## Combined Unity reference, revision combined-waterfall-v2-r1

The next consumer step is now implemented in the shared Integration generator:
`VerticalWaterfallPrototype.CombinedPoolSill` creates the combined case by consuming
WaterfallRecipe. Existing flags defaultfalse and previous r2/V2 comparison scenes
and exported snapshots remain separate. This reference owns fresh terrain/holes,
replacement cliff and water meshes; it is not a foreign-terrain writer.

Build `RuntimePlayerValidation.BuildCombinedWaterfallBatch` in the Integration
project (canonical sources in Rivers/IntegrationProject). Generated scene:
`Assets/TerraLoom/Integration/Generated/TerraLoomCombinedWaterfall.unity`.
Menu: Tools > TerraLoom > Integration > Build Combined Waterfall Pool And Sill.
Standalone `.artifacts/PlayerCombinedWaterfall/TerraLoom.exe` args:
`-terraloomSmoke -terraloomScreenshot <absolute>/combined-waterfall-v2-r1.png`.

Actual recipe: Lip(32,11.2,32),Impact(32,8,32),Forward+Z,Across+X,run0,width3m;
poolstation1.7,width4.2/depth1.1, sillstation5.7/depth.5, transitionend8.7,
outflow17/depth.75, upperSlope.012, downstreamSlope.035, transition3m.
Pool and sill stations are inserted as exact watermesh rows. Water UVx=-1..1,
UVy is cumulative3Dcentreline distance. Lower water level remains8m through the
sill and falls to7.657m at station17. Bed cross-section is quadratic below the
sampled water level, with sample.CentreDepth and HalfWidth; landscape shoulder
blends into the surrounding terrain. Pool/sill longitudinal formulas are consumed
from runtime and not copied into the sample. Upper bed uses recipe.ChannelDepth.

Terrain uses the existing original landscape meadow/damp-sediment/weathered-rock/
dry-gravel textures, blended by channel proximity and cliff vicinity. Cliff uses
the original rock colour/normal material. Its per-face UVs are dominant-axis
world projection divided by2m; normals/tangents rebuilt. Meshpacket includes UVs
and normals; importers should recalculate tangents for normal mapping. Water uses
the existing landscape shader/materials. No Epos art or new art series imported.

Three unchanged canonical foot positions/targets/FOV/resolution are retained;
actual eyes are recomputed from new final ground+1.7m and exported. Controller
parameters/start-XZ/routes remain fixed, startY uses the new upper floor.
Actual three drops3.527887..3.623734m all land grounded. Render offset remains
exactly+.01Y once. Candidate validates before publication; failure/cancel keeps
old generated subtree, successful rebuild disposes old owned meshes/terrain.

15 probes at impact/pool/sill/transition-end/outflow, lateral fractions-.75/0/.75,
measure **actual owned TerrainCollider/CliffCollider** and actual lower-water
triangles through XZ barycentrics, with physical and rendered depths separated.
No water collider is added. Measured centre physical depths: impact1.100000m,
pool1.100164m, sill.500519m, transition.749796m, outflow.750329m. Max bed intent
error2.022744mm across all15, actual rendered offset error.000229mm; native hole
perimeter seam max.745773mm, terrain cell-centre reconstruction1.907349µm.
All17actual boundary vertices at each lip/impact join validate; full fall-strip
support checks retained. Closed cliff/lifecycle/export/depth assertions plus all
prior fixture cases pass12/12; native CombinedReference-12.xml/.log. Windows
build/Playerexit0, CombinedWaterfallBuild.log /CombinedWaterfallPlayer.log.

New packet: `Integration/.artifacts/Evidence/combined-waterfall-v2-r1-packet`,
manifestSHA256 `2619f72538a92eaad75e7fcc6f5fbdcd6c4d212f1649c4cb0d28803516debefd`.
Same19hashedbinarypayloads/fourmeshes, now receivingRecipe and15bedProbes DTOs.
3actual cameras/3actualcontrollerprobes and physical anchors/separate renderoffset
included. `verify_vertical_packet.py` validates the new revision and measured
depths, in addition to hashes/grid/holes/indices/fullwidthjoins/observations.
World receives the actual buffers for separate combined Epos import.

NinePNGscaptured; HERE actually opened only main side/lip/foot and lipflat/normals
(5of9). Textured cliff replaces flat grey contact rendering and profile changes
are visible, but steep technical panels, straight upper pane, bright lip area,
small curtain corner and abrupt water-material transitions remain visually open.
This is a tested combined reference, **not final natural-landscape/art approval**.

## Shared Unity water consumer (2026-10-10)

`TerraLoom.Rivers.Unity.WaterfallWaterGeometry.Build(recipe)` now creates the
three water meshes directly from the runtime recipe. The combined reference uses
this package API; its previous duplicated tessellation has been removed. It runs
in Editor and Player and accepts recipes supplied by manual authoring or a seeded
runtime planner. It creates no scene objects, materials, colliders or terrain.

Returned order is Upper/Fall/Lower, with world-space vertices and metre UVs.
Attach using identity world transforms (or explicitly convert into the host's
local coordinates). Apply the render offset only in Build: it is already baked
into vertices. The caller owns successful meshes and must release them when
replaced/disposed. Build destroys partial meshes on failure/cancellation; Unity
Player destruction is deferred to end of frame. Shared materials remain caller
references and must never be destroyed by the consumer.

Lengths must cover the outlet transition; lengths are capped at1e6m and offset
at +/-1m. Longitudinal segments1..512, transverse2..64 keep meshes within16-bit
indices. Pool/sill/transition stations are inserted exactly. Float-collapsed or
overflowing triangles fail explicitly: large world coordinates require a suitable
local origin, and this API does not silently shift physical anchors.

Native `SharedWater-15.xml/.log`:15/15, including rotated-frame full-width joins,
exact pool/sill rows, cancellation after the first mesh, invalid parameters and
float-collapse cleanup. Windows build/Player exit0 (`SharedWaterBuild.log`,
`SharedWaterPlayer.log`). New evidence prefix `combined-shared-water-v2-r1`;
all19binary payloads byte-identical to frozen combined r1, manifest also retains
SHA2619f72538a92eaad75e7fcc6f5fbdcd6c4d212f1649c4cb0d28803516debefd.
Nine images captured, side/lip/foot originals actually opened here. Technical
wall/upper plane/material transitions remain visible; extraction adds reuse,
not visual acceptance. Previous packet and screenshots were not overwritten.

Epos feedback revealed why cell-centre equality is insufficient for import
acceptance. `verify_vertical_packet.py` now independently reconstructs all15bed
contacts using decoded uint16 heights, exported per-cell triangle diagonals and
closed cliff triangles inside holes. Maximum off-grid decoded/source difference
is0.172147mm, separately bounded below1mm. The source bed-design error remains
bounded below4mm. These are different budgets; never claim bit-identical arbitrary
TerrainCollider/mesh contacts or compensate by shifting exported geometry.
Corrupt/truncated/path-escape payload checks still reject invalid packets.

### Bounded scene-component follow-up

The design below defines the bounded scene host now implemented in the next
section. It is not a completed terrain/backend API. A host can be placed alongside
the Core world, but requires no Core world component. Optional inspector UI
delegates to the same runtime methods:

| Responsibility | Component contract |
| --- | --- |
| Input | Runtime WaterfallRecipe plus lengths/resolution; manual inspector fields and seeded planner both produce that same recipe. No dependency on sample camera/scene flags. |
| Materials | Explicit shared Upper/Lower and Fall material references, validated before generation. Optional renderer adapter maps UVs/palette without moving physical vertices. |
| Generate/Rebuild | Build an inactive owned candidate, validate actual terrain/solid contacts and joins, then swap. Cancellation/error keeps the previous published output. |
| Dispose | Destroy only owned output objects/meshes and explicitly owned terrain data; never shared source materials, user terrain or unrelated decorations. |
| Terrain/solid | Host supplies the accepted contact geometry. Initially own fresh terrain/solid only; foreign terrain edits/holes require a separate rollback and ownership contract. |
| Editor feedback | Report recipe anchors, physical/rendered surfaces, pool/sill depths, validation failures and generated ownership; gizmos use the runtime samples. One editing entry point, no duplicate Workbench controls. |
| Integration gate | Consume the shared builder in the natural reference landscape, preserve the17-wide joins and15bed probes, repeat fixed camera/Player checks before making it the default sample. |

Natural side-rock embedding is currently being compared by the World session in
Epos. Incorporate its contact/sightline findings before replacing the technical
reference solid. The shared builder alone neither finds waterfall sites nor
establishes arbitrary terrain support. General foreign terrain, M1 and collective
Paths integration remain open.

## Water-only scene host (2026-10-10, following shared builder)

`Rivers.Unity.WaterfallWaterHost` implements Generate/Rebuild/DisposeOutput around
the shared builder. No Core world GameObject is required. The host requires an
identity world transform; recipe vertices remain in physical world metres.
Materials are explicit shared references; the host creates no material instances.
A contact-validation callback is mandatory and must throw on unsupported terrain
or solid contacts. It must inspect candidates without changing/retaining/destroying
their meshes. No terrain/collider is created, edited or owned by this component.

```
host.Generate(recipe, reachMaterial, fallMaterial,
    (physicalRecipe, candidateMeshes) => ValidateMyActualContacts(physicalRecipe, candidateMeshes));
host.Rebuild(nextRecipe, reachMaterial, fallMaterial, ValidateMyActualContacts);
host.DisposeOutput();
```

Manual authoring and seeded planning provide the same immutable recipe argument.
Generation first builds an inactive candidate, validates contacts and checks
cancellation again, then activates the new output and disables/releases the old
one. Exceptions/cancellation retain the published output. Generate/Dispose calls
inside validation are rejected; validation is a synchronous, read-only step.
In Player, destruction completes at end of frame; old output is disabled
immediately. DisposeOutput is idempotent and never releases shared materials or
supplied colliders. Destroying the host releases only its own meshes/output.

Editor: Add Component > TerraLoom > Rivers > Waterfall Water Host. Inspector shows
owned output, mesh count, last failure and the four physical anchors; Dispose
uses the runtime method. Selected-host cyan gizmos show physical anchors and
centre connections, without render offset. Output/anchor references survive
assembly reload and transient output is not baked into the saved scene. The
immutable CurrentRecipe property is runtime state and must be supplied again by
the authoring tool/planner after reload. This host is a consumer adapter; its
inspector does not add another competing recipe authoring form or Workbench.

`Integration/WaterfallHostExample.cs` is a fresh small consumer, independent of
VerticalWaterfallPrototype. It accepts an externally authored/seeded recipe and
explicit collider/material inputs; checks actual collider centre depths at
impact/pool/sill against the4mm design budget. It is a code example exercised with
fresh contact pads in tests, not a new landscape/art scene or full site validator.
Full-width bank/fall-wall support, remaining stations and terrain seams need the
caller's appropriate validator; supplied colliders remain caller-owned.

Native WaterfallHost-20.xml/log:20/20 including previous15, two simultaneous
hosts, inactive validation, successful replacement, explicit disposal/deletion,
missing material/validator, transform/reentrancy rejection, failure/late-cancel
rollback and fresh example with seeded recipes0/42 and moved-collider rejection.
The first run exposed missing Editor mesh cleanup on host deletion; ExecuteAlways
now enables the same ownership cleanup in Editor, verified by the repeated run.
WaterfallHostRuntime-1.xml/log:1/1 PlayMode, checks deferred destruction, immediate
old-output disabling, late cancellation, host deletion and separate instance/
shared material survival. Pure runtime audit/compile:0warnings/0errors with local
development Core override; this does not replace the native host tests.

Epos combined comparison feedback: World reports Edit4/4 and Play1/1,15depths and
3landings, waterimport0m, bedimport.172138mm. HERE opened all3final combined PNGs
at 0_Game/_Docs/WorldPrototypeEvidence/WaterfallComparisonV2/
20261010-105551Z-PlayMode-0_Game (side/lip/foot). Felskit is visible; rectangular
curtain, bright solid/lip plates, hard raster shoreline and weak dark impact zone
remain. No visual acceptance. Next integrate accepted natural wet-rock/lip/impact
transitions using the shared host; do not transfer Epos purchased assets or hide
contact/import errors with geometric offsets. Frozen r1 packets remain unchanged.

## Host-backed visible contact reference, natural-host-waterfall-v2-r1

`VerticalWaterfallBuilder.BuildNaturalHostBatch` / menu Tools > TerraLoom >
Integration > Build Natural Host Waterfall Reference saves
`Generated/TerraLoomNaturalHostWaterfall.unity`. Build Windows Player through
`RuntimePlayerValidation.BuildNaturalHostWaterfallBatch`; run
`.artifacts/PlayerNaturalHostWaterfall/TerraLoom.exe -terraloomSmoke
-terraloomScreenshot <absolute>/natural-host-waterfall-v2-r1.png`.
Existing combined r1 and earlier scenes/packet snapshots are retained.

This reference actually calls WaterfallWaterHost.Generate. Its mandatory callback
checks all17vertices on both joins, all15actual collider/water-triangle bed probes,
the whole hole perimeter and full parametric fall support against the **same final
candidate terrain/solid**. It does not substitute the three-centre teaching pads.
Outer publication owns terrain, closed cliff, cloned materials and the host;
water meshes are owned only by the host, never registered twice. An outer failure
after the water host succeeds still destroys the entire new candidate and keeps
the previous published terrain/solid/water subtree. This is this owned-reference
lifecycle, not a general multi-module terrain rollback contract.

The supplied cliff now has bounded side relief outside the water envelope and
six closed original rock masses embedded in its actual mesh/collider. Hole
perimeter and the physical water envelope stay fixed. Existing original rock
colour/normal textures are reused with a darker, smoother owned material clone;
no purchased assets or new asset series. Opt-in prototype shader treatment
locates impact foam using physical impactY as well as XZ: the elevated lip no
longer receives lower-impact foam. Lower impact foam is stronger and upper specular
glare reduced. Old materials keep _NaturalContact=0 and old rendering behaviour.
No water vertex displacement, changed render offset or compensated import delta.

Native `NaturalHost-21.xml/.log`:21/21 including allprevious20. Natural case checks
real host/recipe binding, closed solid, full packet roundtrip, full joins/depths,
perimeter, replacement ownership and a late **outer** cancel after host publication
but before terrain/solid publication. Windows `NaturalHostBuild.log` and
`NaturalHostPlayer.log`:exit0, same3actual controller drops and clear observer
sightlines. Max bed-intent error2.022743mm, seam.745773mm, independent decoded/source
bed error.172147mm (<1mm import budget, separate from<4mm design budget).

Packet `Integration/.artifacts/Evidence/natural-host-waterfall-v2-r1-packet`,
revision natural-host-waterfall-v2-r1, manifestSHA256
a47a7509fc2d3c0942cf8afa6adbc32d90c529b38a51bd6bed7a7f50791fda4c.
15of19payloads are byte-identical to frozen combined r1: allterrain and allwater.
Only the four cliff payloads differ. All3camera DTOs,15bed-probe DTOs and3actual
controller DTOs are identical. Independent packet reader accepts both revisions.

Nine PNGs captured; HERE actually opened side/lip/foot originals plus lipflat and
lipnormals (5of9). Side rock contact silhouettes and reduced upper glare are
visible; rectangular curtain, remaining broad solid planes, faceted rocks and
hard water/material transitions still limit natural appearance. **No final visual
acceptance.** Keep this bounded visible integration evidence; return next to M1
collective curved crossing authorization and composition requirements rather than
expanding waterfall art experiments. No Epos product files were changed here.
