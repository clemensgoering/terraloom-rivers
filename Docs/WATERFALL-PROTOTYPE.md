# Runtime landscape / waterfall prototype

Early direction evaluation requested by Clemens, 10 October2026. This isolated
explicit recipe is NOT the general Rivers waterfall API, a hydrology solution,
crossing-rights integration, tile streaming or art acceptance. It does not replace
the brush river or any existing scene. No purchased assets or Epos files are used.

## Open / reproduce

1. Run `python Rivers/Tools/create_integration.py` from the TerraLoom parent.
2. Open sibling Integration with Unity6000.3.9f1, select Tools > TerraLoom >
   Integration > Build Waterfall Landscape Prototype.
3. Open `Assets/TerraLoom/Integration/Generated/TerraLoomWaterfallPrototype.unity`,
   enter Play. The saved scene contains configuration/material references only;
   all terrain, water, rocks and vegetation build at runtime. No manual mesh fixup.
4. `0` overview; `1/2/3` the observations; `Tab` walking, WASD/mouse, Shift faster,
   Escape overview; `R` regenerate the same seed. Deep water/drop has invisible
   physical blockers. This is observation-space walking, not navigation approval.

Windows build: Unity `-batchmode -quit -projectPath <Integration> -executeMethod
TerraLoom.Integration.Editor.RuntimePlayerValidation.BuildWaterfallLandscapeBatch
-logFile <absolute-buildlog>`.
Player `.artifacts/PlayerWaterfallPrototype/TerraLoom.exe -batchmode
-terraloomSmoke -terraloomScreenshot <absolute-overview.png> -logFile <absolute-log>`.
It creates overview plus `-upper-lip`, `-foot-pool`, `-outflow` player images.

## Shared bounded recipe

`WaterfallLandscapePlan`: seed4102026,160×160m,1025² heightfield,512² four-layer
height/slope/shore paint. Upper level15.6m atZ90, lower8.4m atZ86:7.2m drop over4m,
monotone cubic profile. Upper/lower reaches slope0.035m/m; slowly meandering
centre and variable width. Nominal channel5m, local pool multiplies local channel
width by1.35 atZ78, with lower outlet rather than a closed bowl. These are explicit
evaluation parameters, not accepted universal thresholds. The Epos pilot uses a
different3.2m/3m/4.2m case; current pictures are NOT a same-profile A/B comparison.

Terrain carries the actual channel/drop/pool. Water uses the same centre, height
and width functions,32 columns/0.125m steps, with continuous endpoint geometry and
arc-length UVs. Original custom URP shader supplies moving water streaks and a
small impact fringe; no mist hides contacts. Supplemental original rock meshes,
four branched broadleaf prototypes with individual alpha-cut leaves, original
grass blades, and Core LandscapeSurfaceRecipe textures replace earlier cone/ball
placeholders. Material/asset fidelity is still explicitly provisional.

Rock lower-half vertices are embedded against the final sampled TerrainData,
rather than just sampling the centre. This is a discrete placement constraint,
not continuous entire-footprint contact proof. Physical rock/trunk/terrain
colliders share actual meshes. Trees use height/slope/dry-space exclusion;
observation volumes stay clear. Returned transient meshes and TerrainData belong
to the generated child and are disposed on rebuild; source assets remain owned
by the separate prototype folder.

## Evidence and interpretation

First four captures `waterfall-landscape-4102026` were actually reviewed and found
defective: obstructed upper view, stacked/hovering rock stamps, overgrey steep
canyon, square white water marks. V2 reduced/embedded rocks, greener/moderate
flanks, localized the drop into its valley, freed the upper view and elongated
shader streaks. All four V2 images were reviewed here and independently by the
coordinator: now a visible continuous fall/pool/outlet, still no style acceptance.

Final source adds dry-bank CharacterController motion and target sight-line
checks. Camera eyes are1.70m above final terrain,65° FOV. A clear capsule/sphere
tests occupation; a separate line excludes invisible water blockers and checks
target occlusion. It does not prove that the complete camera frustum is clear;
the saved pictures must also actually be inspected.

Final run10:48local: Windows build/player exit0; four targeted native EditMode
tests4/4 green on final source (profile descent/local1.35pool ratio, deterministic
seed/dry observations, outward rock normals/individual leaves, runtime generation
and editor rebuild/old transient terrain disposal/source material ownership).
Results `Integration/.artifacts/WaterfallPrototypeFinalEdit.xml` and `.log`;
build `WaterfallPrototypeBuild-final.log`, player `WaterfallPrototypePlayer-final.log`.
Player generation744ms on this machine is a single generation observation, not
FPS/p95/startup acceptance. Actual dry-bank CharacterController travels2m and is
grounded; this is not complete bank navigation or drop traversability.

All four final images actually opened here:
`Integration/.artifacts/Evidence/waterfall-landscape-final-4102026.png`,
`-upper-lip.png`, `-foot-pool.png`, `-outflow.png`. Eyes respectively
(72.0190,18.3997,98), (82.5949,12.2558,76), (73.0149,11.2943,58),1.70m above
final terrain,65° FOV. Head/capsule and target line checks pass. Foot-pool is a
lower detail view and crops the top of the fall; outflow shows the entire fall.
Final channel sweep158720 vertices minimum terrain clearance11.549mm; falling
strip4096 vertices min10.384mm. Discrete samples only. No full continuous contact,
arbitrary profile or world-tile/general waterfall proof is inferred.

Remaining: smooth weir-like cliff/fall, repetitive bright streaks, hard/uniform
shore, rock/terrain materials and repeated tree silhouettes still far from the
user's reference. Need more irregular rock bands, material scale, wet-contact
variation and natural outlet/shore detail. No waterfall release-readiness,
cross-module permission certificate, full agent traversal or FPS budget claimed.
