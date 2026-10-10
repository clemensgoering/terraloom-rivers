# Explicit waterfall profile V1

`Packages/com.terraloom.rivers/Runtime/WaterfallProfile.cs` is an immutable,
Unity-free contract. Integration consumes its water levels, widths, centre
depths and sections, without a second longitudinal profile formula. Explicit
anchors can come from manual authors or seed planners; no biomes or Unity Terrain
are required. Placement search, rights and terrain writes remain separate.

World metres, Y up; station0 at lip, positive downstream along the horizontal
lip-to-impact axis at any yaw. Impact/pool-centre/outlet-sill must be collinear
and strictly ordered. Anchor Y must match explicit upper/lower levels. Inputs
finite/bounded +/-1e9, stations +/-1e6. Positive widths/depths, pool width1..1.5x
channel, pool depth>=channel depth,0<sill depth<=channel depth, nonnegative slopes.
Upper tangent>3*drop/run is rejected as nonmonotone. Straight axis and bounded
widening are V1 limits, not universal physical laws. Version is explicit.
Every accepted anchor station is <=1e6 and sampleable; native/portable regression
covers rejection beyond that limit and all anchors at the accepted boundary.

Fall Y uses monotone cubic Hermite: lip derivative=-upstreamSlope, impact=0.
Impact through sill is level water. Width smoothly grows to pool centre then
returns to channel width. The real centre floor stays at pool depth then rises
to a submerged sill. Beyond it, depth returns to channel depth and integrated
smoothstep changes water slope from0 to downstreamSlope over transitionLength.
Widening alone does not stand in for scour/sill. This is geometric intent,
not fluid simulation or an assurance that any terrain fits it.

## Common comparison

[WATERFALL-COMPARISON-V1.json](WATERFALL-COMPARISON-V1.json) is the actual Player
export: recipe `anchored-waterfall-v1-r2`, schema1/profile1. Local origin lower
water=world(80,12.4,89), yaw180; station=localZ+1.

| Anchor | Local XYZ | Station |
|---|---|---|
| Lip | 0,3.2,-1 | 0 |
| Impact | 0,0,2.3 | 3.3 |
| Pool centre | 0,0,4 | 5 |
| Outlet sill | 0,0,8 | 9 |
| Receiving join | 0,0,17 | 18 |

Channel3m, maximum pool4.2m; channel depth.75m, pool1.1m, sill.5m; upper slope.012,
downstream slope0, transition3m. V1 supports a sloped outlet; this comparison has
a level receiving reach. Seed4102026 controls relief/assets, not anchor intent.
The receiving join is downstream of the sill, not a15m-long pool anchor.

Measured final raster centre bed relative to lower water: impact-1.098907m,
pool-1.100787m, sill-.500469m, receiving-.750423m. Native rebuild checks all five
anchor depths within3mm including lip. These measurements, not opaque-water
images, establish floor depth. Discrete probes do not prove continuous contact.

Common cameras1600x1000/FOV55: overview local eye(11,9,18), target(0,1,1);
side footXZ(7,5), target(0,2.8,0); player footXZ(7,9), target(0,2.6,.5).
Feet use actual final terrain; eyes+1.7m. JSON records actual eyes World/Local,
rotations, targets and foot heights. Equal footXZ alone is not equal camera
geometry if consumers have different final terrain. Old1.5/1.4m targets hit the
new fall terrain and were explicitly revised with Epos. A procedural6m foliage
exclusion corridor frees the fixed overview: non-colliding leaves previously hid
it despite collision raycasts. All four final comparison images were opened.

The manifest also references `waterfall-comparison-v1-4102026-terrain.json`:
actual35x35m patch,225x225 original height vertices,.15625m spacing,
worldX62.5..97.5/Z67.5..102.5. Heights are little-endian uint16,worldY=code/65535*70;
localY=worldY-12.4. Each224x224 cell has a measured diagonal bit (0SW-NE,1NW-SE),
row-major X fastest/worldZ increasing,LSBfirst. All collider cell centres tested,
maximum error9.536743e-6m. Hash SHA256(raw height bytes followed by diagonal bytes)
850477a5b52bf72f0855a15cd6594c228b736c2e9d42852e0ae1802e0601bcbe,
independently verified in Python. This bounded export enables identical lateral
ground geometry; it is not a general terrain publishing API or a whole-world map.

## Reproduce

`python Tools/create_integration.py` creates sibling Integration. Unity menu:
Tools > TerraLoom > Integration > Build Anchored Waterfall Comparison. Open
generated `TerraLoomWaterfallComparison.unity`, Play. Batch build method:
`TerraLoom.Integration.Editor.RuntimePlayerValidation.BuildWaterfallComparisonBatch`.
Player: Tab walk,1/2/3 observations,0 overview,R regenerate,Escape exit walk.
Run built Player with `-terraloomSmoke -terraloomScreenshot <absolute.png>`:
overview/upper-lip/foot-pool/outflow PNGs plus `<stem>-profile.json`. Requires
clear dry capsule/head/target sightline and actual grounded2m controller motion.

Executed10.10: pure Rivers compile zero warnings, portable profile10/10,
native profile10/10, Integration7/7, comparison and baseline Windows build/Player0.
Ignored logs: Integration/.artifacts/WaterfallV1Build.log,WaterfallV1Player.log,
WaterfallV1-7.xml/.log,WaterfallBaselineExtractedBuild.log,
WaterfallBaselineExtractedPlayer.log. Native profile results also under
.bootstrap-tools/RiverRadiusValidation/.artifacts/WaterfallProfile-10.xml/.log.
Portable: compile with `Tools/verify.py --compile --development-core ../Core`,
build Tools/WaterfallProfileChecks.csproj supplying CoreRuntimeAssembly DLL,
run its net8 executable. Native filters: TerraLoom.Rivers.Tests.WaterfallProfileTests
(10 cases), TerraLoom.Integration.Tests.WaterfallPrototypeTests (7 cases).

## Baseline and limits

`LandscapeBaseline()` preserves historical float arithmetic, meander, sloping
Gaussian pool and mesh section boundaries. It is explicitly V0. Default scene,
seed4102026 and FOV65 remain separate. New baseline Player reports exactly the
three previous camera terrain heights16.6997318/10.5557747/9.59434m. Its four new
PNGs were opened. They are not bitwise identical to earlier frames; shader time
is not frozen. No full terrain checksum or image identity claim is made.

Images: Integration/.artifacts/Evidence/waterfall-comparison-v1-4102026
{,-upper-lip,-foot-pool,-outflow}.png; baseline prefix waterfall-baseline-extracted-4102026.
Coordinator subsequently relayed explicit user acceptance of the visual direction.
Known imperfections remain smooth weir wall, bright repeated stripes, hard banks,
repeated trees; optional polishing is deferred. Original assets only. Renderer selection awaits Epos images of
this SAME V1 recipe; its earlier Bezier/Gaussian pilot is a different case.
No general placement/tile/rights/cancel certificate, full navigation or FPS budget
is inferred. M1 curved rights, collective Paths integration and publication
lifecycle remain separate open tasks; this is not release readiness.
New required next case: near-vertical AND exactly vertical falls with plausible
source/impact/bank contact. V1 rejects horizontal run<=.01m; do not weaken that
guard. A separate fall parameter and explicit flow direction are needed, plus
bounded owned cliff meshes/colliders where a heightfield cannot represent a wall.
