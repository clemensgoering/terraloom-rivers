# Curved brush crossing: frozen rejection baseline

The native `CurvedBrushCrossingReproducesOutsideCandidateBeforePublication` test
recreates the protected sample's analytic terrain in memory: 96m square, 129 height
samples, 128 alpha samples, seed42, .6m water inset and sediment layer1. No sample
asset builder, editor terrain capture or biome source is needed. Parameters and
the observed rejected request are in [curved-crossing-baseline.json](curved-crossing-baseline.json).

The test first publishes a valid straight brush/bridge composition. It then moves
the river endpoints to (48,8)/(48,88), road endpoints to (12,24)/(84,24) and protects
the 8x12m rectangle at (48,48). The river can plan its detour; joint routing stops
at `03-plan-routes` with `OutsideCandidate`. Repeating the same failed generation
produces identical diagnostics. The previous terrain object and height samples,
river/path roots and JSON stay unchanged; original heights/alphas stay unchanged.
This is preservation before publication, not rollback after river materialization.

## Measured conflict

Bounds below are `[minX,minZ,maxX,maxZ]` in world metres, rounded here for reading.
The JSON retains the emitted round-trip numbers and IDs.

| Shape | X interval | Z interval |
| --- | --- | --- |
| Requested deck rectangle | 33.2441–42.7559 | 53.3317–55.3317 |
| Primary crossing window | 33.2441–42.7559 | 48.5908–60.0727 |
| Conflicting reach bed | 34–42 | 55.2569–63.7495 |
| Conflicting crossing window | 33.2441–42.7559 | 54.5010–64.5054 |

The request touches the neighbouring bed over a narrow strip near Z55.3. That
contact fits its local window, while the full requested deck starts before that
window at Z53.3317. Core requires every affected water candidate to contain the
entire request, so it rejects this case. Paths also repeats full-containment checks.
Removing only one check is not a complete or safe contract change.

Paths now appends `CrossingContext` to the last Core-rejected attempt: input
fingerprint, revision, algorithm/profile, axis, requested deck height, exact request
and primary-window rectangles, conflicting bed and up to four sorted conflicting
candidate windows (total count included). This bounded text does not change routing,
permissions, budgets or algorithm identity. It is not an exhaustive search trace.

## Next bounded change

Introduce explicit collective authorization with crossing ID, PlanIdentity, included
reach IDs and approved partial surfaces. Prove every actual deck/wet contact lies
inside those surfaces; a bounding box spanning them is insufficient. Keep foreign
protection/foundations binding. Centralize full-width dry landing and deck underside
clearance checks against final terrain/water, then have Paths consume that decision
without the old redundant containment loop. First tests: positive multi-window curve,
unapproved residual wedge, foreign reach, partly wet landing and stale revision.
These checks and the collective contract are **not implemented by this baseline**.

## Architecture findings requiring separate small changes

* Shared orchestration is currently the consumer class in `IntegrationProject`, not
  a supported composition API in a product package. Core `GenerationRun` orders
  synchronous actions and marks failures, but explicitly provides no rollback.
  A fault after river publication can leave a new river and cleared paths.
* Freshness is manually invoked on the sample composition. Decoration has no
  persisted input/profile/output fingerprint; its counts cannot certify current
  inputs. Exclusion refresh is owned by individual sample consumers.
* The common planning snapshot contains source terrain. Composition records the
  final Unity terrain fingerprint separately; it does not export a final immutable
  height/wet/reservation snapshot under one shared result identity for Epos.
* Plan stores validate versioned JSON against a freshly replanned expectation.
  This is an exact replay check, not a documented long-lived save migration or
  streaming contract. Baked Unity object ownership and JSON replay must be reviewed
  separately before external consumer integration.

These are observed responsibility gaps, not evidence that all existing runtime
logic must be rewritten. The coordination roadmap is a local workspace file,
`../../coordination-roadmap.json`, owned by the coordination chat.

## Validation

5/5 Integration PlayMode tests passed, including the frozen failure and existing
legacy/brush traversals and seeded scenarios. 29 portable Core/Paths contract cases
passed; all pure assemblies compiled. No new player build, screenshot, positive
curved crossing or multi-module rollback validation in this block.

Reproduce after `Rivers/Tools/create_integration.py` sync (fixture source is versioned):
`Integration/Tools/ValidateUnity.ps1 -Platform PlayMode -Filter TerraLoom.Tests.IntegrationRuntimeTests.CurvedBrushCrossingReproducesOutsideCandidateBeforePublication`.
The native XML output includes `TERRALOOM_CURVED_CROSSING_BASELINE` with the exact context.
