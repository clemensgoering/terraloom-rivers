# Positive curved crossing: full-route recipe

Validation: 96/96 native Editor tests, 4/4 shared Play tests, 38 shared portable
contracts and 6 Paths portable checks pass; Paths/Rivers runtime compile without
warnings/errors. Evidence is archived under Integration `.artifacts/PositiveCurved-20261010`.

This separate consumer recipe retains the frozen seed42 terrain height function,
river source/mouth, 4m water, 2m banks, protected detour, 2m road width, .35 along
and across grade limits, and explicit partial crossing permissions at X24..64,
Z54..64. Road targets change: west=(24,57), east=(72,57), replacing the
original west=(12,24), east=(84,24). The positive recipe also explicitly limits
road routing to X16..80, Z54..58.9; this targeted consumer requirement selects the
original 9.511811m option near Z57.533 and prevents bypassing
the river ends, grants no rights and leaves river planning over the full terrain.
No cut/fill, implicit permissions, or target
relocation is performed by the modules.

`PositiveCurvedCrossingBuilder.BuildBatch` loads the frozen configuration and
saves a separate `TerraLoomPositiveCurvedCrossing.unity`. The original scene and
source terrain asset remain unchanged. It first checks exact full-width local
target access, then requires normal composition planning and publication,
`Paths.LastPlan`, current composition validation and an actual 9.511811m bound
collective crossing. Construction-only fallback is disabled.

Build the actual Windows executable with
`RuntimePlayerValidation.BuildPositiveCurvedCrossingBatch`. Launch with
`-batchmode -terraloomSmoke -terraloomScreenshot <absolute-output>.png`, without
`-nographics`. Evidence requires a published full route and current rights,
raycasts every route segment and moves a real CharacterController along the
whole route. Four URP images show the bridge overview, both immediate landings
and underside. Successful physics checks alone do not establish visual quality.

The original frozen scene remains a negative target-access regression. A first
positive candidate with targets (24,24)/(72,24) was rejected as test evidence:
the valid road bypassed the river instead of negotiating a bridge. Free routing
at (24,60)/(72,60) also selects a valid bypass, even after the independently tested
fix to zero-area bank-contact ramp costs. Legitimate overlapping soft costs
remain additive; costs were not tuned to force a bridge.

Windows Player exits 0: published full route, 485 downward collider probes and
actual CharacterController traversal. All four 1440x900 URP captures were opened:
both dry approach toes meet the gravel path, full-width bearings touch the banks,
and no pier obstructs the water. Strongly repeating gravel, simple timber grain,
bright water and abrupt water-end highlights remain prototype art limitations.
This is geometry/integration evidence, not final art acceptance or an engineering
load certificate.

The small routing rectangle does not filter waters, candidates or reservations
from the shared snapshot. Full-world rights still evaluate the complete body,
rails and ramps. A negative case lowers its upper Z edge to 58.8: the clear deck
still fits but the side structure does not. That route must fail; saved JSON and
composition freshness must also reject a changed routing zone. Rights withdrawal
and a deliberately unapproved wet strip at X37.9..38.1 remain negative cases.
