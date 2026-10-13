# Buyer A: supplied terrain, first stop (13 October 2026)

Fresh consumer: `.bootstrap-tools/composition-buyer-a-20261013`, created with
only Unity ProjectSettings and Git packages, with no copied TerraLoom sample
scene or art. Unity 6000.3.9f1, URP 17.3.0. Manifest pins:
Core `014e4ce4fbdfe7bab988ca8b927ef8a17b80fc69`,
Paths `d1f4ef04fba9415571a8efd1f54cf585aeb94216`,
Rivers `7889dc7c73f3ada87074bfc5546fec75a731f262`,
Composition `f86c96a9a0a18ab00367ddaef05c967d03bed8bc`.

Reproduce by copying `Tools/BuyerAProbe.cs` to `Assets/Editor` in that
fresh Git consumer and running `-executeMethod BuyerAProbe.Run` with the Unity
Editor. The checked-in source creates its own 96 m TerrainData asset, 1 prefab
TreeInstance at the center, a uniform DetailPrototype grass layer, two terrain
layers and three simple URP materials. It configures four manual anchors and
one river/road connection, then calls the **packaged Inspector's**
`IntegrationEditor.GenerateOnSuppliedTerrain` action. No sample builder or
private algorithm is called. This is an automated buyer-path probe, not a
human clickthrough. No manual click count or under-15-minute claim is made.

Requested fields: Core World + supplied Terrain; Paths Ground and Bridge
materials, 0.35 maximum slope and 1.2 m bridge clearance; Rivers Water
material, Terrain Brush, 0.6 m inset and terrain layer 1 for sediment. Bed and
Bank materials are unnecessary in Terrain Brush mode. No Region Decoration
component or profile is needed for the supplied-terrain Paths + Rivers button.
The original source TerrainData is an asset and is never written by the probe.

Native outcome: setup inside the Editor script 0.47 s, generation at 1.63 s
after script start (cold Unity import and authoring time excluded). Composition
reported one river, one path and one negotiated bridge deck. Source heights
remained byte-for-byte equal; selected Terrain and TerrainCollider both bound
the same carved copy. No missing-material or route error occurred.

**First blocker:** the carved copy still had the TreeInstance in the river
corridor. It retained 1 of 1 original trees and 32 grass detail entries in the
center 4×4 detail cells. The source remains untouched, but vegetation is not
cleared for the generated river/road. The probe deliberately throws
`BUYER_A_BLOCKER existing Terrain TreeInstances/details remain in the generated
river/road corridor.` Its nonzero exit is the expected red result. Evidence:
`.bootstrap-tools/composition-buyer-a-20261013/.artifacts/BuyerA-20261013/build.log`
and the authored `Assets/BuyerA/Authored.unity` in that local consumer. The
script-backed setup is reproducible from this repo's Tools source. Player
screenshots and visual bridge/landing acceptance were **not run** because the
agreed gate stops at the first concrete blocker. The bridge is proven in the
plan, not yet from player height.

**13 October follow-up, local package validation:** Composition now clears
vegetation on its unpublished carved TerrainData after the final path is
replanned against carved heights. TreeInstance intersection uses a horizontal
footprint measured from the prefab meshes and its width scale. Grass uses exact
detail-cell rectangles against the final river and curved road segments, not
their broad bounding rectangles. The mask is built once per segment, reused
for all detail layers. Original TerrainData is never written. If the prefab
has no measurable mesh footprint, generation fails before publication.

The focused native red/green probe uses three trees: one in the channel, one
on the curved road approach, one well outside both. The latter and adjacent
grass must remain. It verifies source heights and vegetation, carved
Terrain/TerrainCollider bindings, one negotiated bridge, both clear corridors,
and original bindings after Clear. Local file-package run
`green-local7.log`: setup 0.60 s, generation 1.80 s, published 1 of 3 trees,
channel 4×4 detail sum 0, road center detail 0, outside 4×4 sum 32,
`BUYER_A_READY_FOR_PLAYER`, exit 0. These are in-script durations after Unity
startup, not human click times. The red result above remains evidence of the
unfixed Git pin. The same probe then passed in the fresh Git consumer pinned
to Composition `b901c1b0ac204e8b37c2266ab733b953c0222783`:
`green-git.log` reports one river, one path, one negotiated bridge, original
tree/detail arrays untouched, channel and road clear, outside vegetation
retained, and `BUYER_A_READY_FOR_PLAYER` (exit 0). A Player visual review is
still required before broader acceptance.

Scope: the shared Composition Editor and runtime path only. Standalone Rivers
and Paths do not yet perform this vegetation clearance and must not be claimed
as fixed by this change.

**Player view, same Git consumer:** `Tools/BuyerAProbe.cs` can be placed in
`Assets/Editor`, `Tools/BuyerAPlayerCapture.cs` in `Assets`, then run with
`-executeMethod BuyerAProbe.BuildPlayer`. The minimal consumer must also copy
the TerraLoom URP assets/renderer from `Rivers/Assets/Settings` into its own
`Assets/Settings`: the copied ProjectSettings reference their GUIDs. Without
them, the Player rendered path and bridge magenta. The first framebuffer
capture was black because it read outside a draw frame; the checked-in capture
now renders into its own RenderTexture. Those two faulty image runs are not
visual evidence.

The final Windows Development Player build (`player-build3.log`, exit 0) and
run (`player-run3.log`) produced genuine 1280×720 west/east player-height
views, retained locally under `.artifacts/BuyerA-20261013/PlayerEvidence/`.
They show a colored river and a road strip crossing it without magenta. They
do **not** show a credible load-bearing bridge body or fall protection, and
from these low views the precise landing/terrain contact cannot be approved.
This probe leaves `TimberBeamBridge` and `TimberTrussBridge` disabled; its
Bridge-surface count must not be confused with a validated structural kit.
The visual/structural bridge gate remains open. The new vegetation clearance
passes independently of that gate.

**Bounded structured-bridge attempt:** `BuyerAProbe.RunStructural` enables the
existing 6–12 m TimberTruss kit and supplies fresh, explicit bridge candidate
grants only inside X 24–64 / Z 54–64 of this unchanged 96 m world. It does not
increase span or clearance limits or grant any rights elsewhere. Native
`structural3.log` stops in Stage 05 with `Long side truss requires explicit
current crossing authorization`; the previously published source remains.
The shared input had 77 crossing candidates and 14 current grants in the
declared zone, but the final path selected one Bridge surface near X 43.3–52.8
/ Z 8.5 with zero bound crossings. That segment lies far outside the
authorized area. The planner's visible Bridge surface therefore cannot be
treated as a constructible bridge. This is a Buyer-A planning/UX blocker, not
a reason to broaden authority or claim the earlier flat deck as success.
There is no structural Player view of this rejected publication.

The existing `IntegrationProject` FrozenCurvedCrossing is the separate
legitimate 9.511811 m, explicitly authorized local TimberTruss construction
probe. It validates a local body and approaches but records rejection of its
unchanged full target route. It cannot be substituted for successful Buyer-A
composition or whole-route Player acceptance.

**Separate authorized-truss consumer on the same foreign-terrain recipe:**
`BuyerAProbe.RunAuthorizedTruss` keeps the 96 m height field, terrain layers,
TreeInstances/details, source/mouth at X 48 / Z 4–92, river brush, seed 42,
path slope/clearance and TimberTruss limits. It has its own authored folder
`Assets/BuyerA-AuthorizedTruss`; the original `RunStructural` case, its
west/east targets at Z 48 and its single-section grants remain unchanged.
The positive case places west/east at X 24/72, Z 57 and restricts routing to
X 16–80 / Z 54–64. Its consumer policy grants only selected partial crossing
sections that actually overlap each deck strip inside the **same** X 24–64 /
Z 54–64 authorization zone. This yields six current collective grants. It
does not authorize a crossing at the previously selected Z 8.5 headwater
detour or change a construction limit.

With Git-pinned Core, Paths `312ac793d49736f7a06698f6e9543610978960be`,
Rivers and Composition `b901c1b0ac204e8b37c2266ab733b953c0222783`,
`authorized-truss-final-git.log` is green: one bound 9.5 m bridge, one
BridgeStructure, two BridgeRamps, collider-backed geometry, current
composition, final structure footprint X 43.25–52.75 / Z 57.95–60.55 wholly
inside the declared grant zone. The source trees/details and heights are
unchanged and Clear restores the source binding. `BuildAuthorizedTrussPlayer`
and the Windows Player run report `BUYER_A_PLAYER_SUCCESS structural=True
span=9.5 colliderProbes=499`. CharacterController traversed the entire route.
Five 1280×720 views are preserved locally under
`.artifacts/BuyerA-20261013/AuthorizedTrussEvidence/`: overview, west, east,
side and underside. They show timber side trusses, railings, dry approaches
and open water below. The simple terrain/water/material styling is only
technical evidence and is not final V0 visual acceptance.

Re-running the **original** `RunStructural` after this separate positive
recipe still gives Stage-03 `NoRoute` (`original-negative-after-positive.log`):
the old west/east Z 48 targets and old grants are not silently replaced by
the new recipe. The baseline surface-only vegetation test also stays green
(`baseline-after-positive.log`). Its surface-only deck still lacks a physical
kit and must not be presented as a finished bridge.
