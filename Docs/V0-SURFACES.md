# V0 surface baseline, 2026-10-10

First bounded art iteration, not V0 completion. Core LandscapeSurfaceRecipe supplies
original periodic meadow/damp sediment/weathered rock with matched normals and
smoothness alpha. Same96x16x96m terrain, height129/alpha128, three layer indices,
seed42 straight crossing, light and old five cameras. Three extra views derive
from the actual contiguous negotiated bridge: both ends and underside. Saved
normal/albedo/2m tiling/alpha bindings checked on original AND carved TerrainData.
Only the ignored sibling Integration scene rebuilt, not user Core/Rivers assets.

Evidence: `Integration/.artifacts/Evidence/v0-surfaces-42.png` plus `-close`,
`-player`, `-river-section`, `-river-player`, `-landing-0`, `-landing-1`, `-underside`.
All eight opened by implementation owner; coordinator opened four, world owner
reports all eight reviewed. Old five cameras retained for before/after comparison
with `brush-crossing-verified*`. No manual repair or lighting-only change.

Unity6000.3.9f1 BuildBrushCrossingBatch and Windows smoke exit zero. Logs under
Integration/.artifacts: V0SurfaceBuild.log, V0SurfacePlayer.log. Water probes unchanged:
6318vertices min5.003929mm,2106centres min80.294609mm; discrete, not continuous proof.
Targeted saved-brush EditMode1/1 passes with persisted256px normals/albedo/2m tiles/
DiffuseAlphaChannel checked on both terrain versions. Core native75/75 passes
including repeatability, packed-normal range and unchanged Unity random state.

Fresh combined Integration PlayMode5/5 passed, zero skipped/failed, Unity exit zero:
`Invoke-TerraLoomUnityTests -ProjectRoot D:/Unity/Projects/TerraLoom/Integration
-Platform PlayMode -Filter TerraLoom.Tests -ExpectedTests 5` after sourcing
Core/Tools/UnityTestRunner.ps1. Includes seed-driven examples and the preserved
curved-failure baseline. An earlier narrower IntegrationRuntimeTests filter ran
3/3 green but incorrectly expected5; that count-gate failed. The corrected whole
namespace command above was rerun successfully and is the reproducible gate.

Reproduction from Integration: Unity `-batchmode -quit -projectPath <Integration>
-executeMethod TerraLoom.Integration.Editor.RuntimePlayerValidation.BuildBrushCrossingBatch
-logFile <buildlog>`, then `.artifacts/PlayerBrushCrossing/TerraLoom.exe -batchmode
-terraloomSmoke -terraloomScreenshot <absolute-evidence.png> -logFile <playerlog>`.

## Findings and next step

- Finer/darker less-orange terrain is limited progress. Damp bank still reads too
  grassy; variable-width lower wet/earth strip and water contact need work.
- Uniform canal, straight termination and background line remain visible.
- Path resembles coarse ornamental paving with hard cut edges. DryGravel recipe
  exists but is not bound; use proper scale, quieter centre and soft shoulders.
- Underside exposes missing solid bridge body (one-sided sheet disappears).
  Next bounded kit: closed deck boards, longitudinal beams/cross ties, both dry
  full-width end supports from the crossing plan. Explicit maximum free span,
  no arbitrarily stretched timbers and no unnegotiated water piers. Clearance
  includes actual deepest beams, not top alone. Keep walkable top fixed unless
  replanned. Check supports against final terrain, never cosmetic floating ends.
  Railings preserve clear walking width; open sides explicitly configured.
- Add calibrated1.8m human reference and record camera height in the next player
  case. These images alone prove neither dimensions nor full contact/agent use.

Finish small bridge kit and review same seed42 end/underside/player views first,
then shoulder/material and wet-bank shaping, then three rock/two vegetation variants.
Second seed and curved M1 remain open. New Core permissions are not wired into Paths
and do not certify final sweeps/landings. Curved protected failure remains expected.

Five Epos V0 images20261010-073159Z opened here: followcamera,landing0/1,underside,far.
Legible structure/player scale useful; long ramps, bright deck gaps, serial railing,
hard winter boundary remain defects. No Epos product files changed or bought art
copied. Feedback exchanged with world/coordinator. Core visual gate now applies
early, parallel to implementation; technical success cannot substitute image review.
