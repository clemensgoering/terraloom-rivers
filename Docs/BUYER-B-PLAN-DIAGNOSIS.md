# Buyer B: why the path crosses near the world edge

Frozen negative case: Unity 6000.3.9f1 URP, four Git packages, `Assets/BuyerB/WorldRecipe.asset`
in `.bootstrap-tools/composition-buyer-b-final-20261013`. SHA-256 of the recipe:
`AD73AC57D16F9103394B3B7CF77A13C78DC5823F40DC77D3A586DB2417FF472F`.
The copied snapshot is `.artifacts/BuyerB-WorldRecipe-negative.asset`; repeated
diagnostic runs left the original hash unchanged. The diagnostic scene changes
below were in memory only and were not saved to the recipe or scene.

The recipe uses a 96×16×96 m DrainageValley, seed 2042, source/mouth at
(48,4)/(48,92), path targets at (8,48)/(88,48), one river 4 m wide with
2 m banks and 0.2 m water inset, and a 2 m path with 2 m grid, maximum grade
0.5, slope cost 4, bridge clearance 1 m and maximum ford depth 0.3 m.
No Path region cost or manual reservation is configured. Path routing is
unrestricted. Both `TimberBeamBridge` and `TimberTrussBridge` are false.

The river publishes 135 water segments, 135 bridge candidates and 270
reservations. Its bank reservations are soft and each has `TransitionCost=2`;
they overlap heavily where a 9.5 m deck covers several adjacent river
segments. The shared Core `CrossingEvaluator` adds every intersecting soft
reservation, and the Paths edge/landing evaluator also adds such costs. This
makes the crossing price depend on river tessellation and location along one
river, rather than only on the occupied continuous bank. These sums are
diagnostic facts, not a claim that all overlapping reservations belong to
separate independent obstacles.

| Route on the same source | Deck centre Z | Route length | Route cost | Soft bank overlaps / sum on deck footprint | Core-accepted candidate offers |
| --- | ---: | ---: | ---: | ---: | ---: |
| Original unrestricted recipe | 90.75 | 126.56 m | 374.59 | 9 / 18 | 7 |
| Diagnostic routing window Z 36..60 | 41.75 | 81.41 m | 435.67 | 16 / 32 | 13 |

The mid-world route exists and is 45.15 m shorter; it is simply more
expensive under the present scoring. The bridge-link cost includes
`span * (1 + CrossingDecision.TransitionCost)` plus both landing costs. On
the 9.5 m deck, the soft-cost portion alone is approximately 171 m-cost
units near Z90.75 versus 304 near Z41.75: a 133-unit disadvantage before
comparing the shorter approaches. Around Z48 the same 9.5×2 m footprint
intersects 17–18 soft bank reservations, costing 34–36. Three in-memory
windows (Z36..60, Z24..72, Z8..88) all selected the same viable Z41.75
crossing, so the central option is not rejected by terrain coverage, slope,
Core crossing rights or the work budget. The unrestricted planner minimizes
its current cost and selects the cheaper near-edge crossing. Its Path report
is `Connected`, expanded nodes 1408.

This route has **no external crossing grants**:
`CrossingPermissions == null`, empty authorization fingerprint, and zero
`PathBridgeCrossing` bindings. Core still evaluates river bridge offers.
Paths requires an explicit current grant only when a physical bridge kit adds
body depth or lateral padding. With both kit flags false, `MaximumBridgeSpan`
and `BridgeBodyDepth` are zero, a valid `PathSurface.Bridge` produces the plain
flat `BridgeDeck` mesh, and the Composition diagnostic currently calls it a
“negotiated bridge deck.” Candidate IDs for the chosen legacy link are not
retained in `PathRoute` without a grant; the seven listed candidates only
contain its footprint geometrically. This limits exact post-build provenance.

Small near-term UX proposal: Composition should distinguish a *surface-only
crossing* from a bridge with a physical kit and current grant. After a build,
show a non-blocking review warning when a path detours far beyond target
distance or its bridge sits within a configurable/derived safety margin of
the finite world edge, with the measured route cost, deck position and
permission/kit status. Do not silently shift targets or relax crossing gates.

Product follow-up: define provenance-aware union semantics for soft bank cost
from segments of one river, then use the same normalized cost in Core
crossing evaluation and Paths ground/landing checks. Test that independent
soft obstacles still compose as intended, and that seed/revision, grants,
terrain, slope and full-footprint rules remain unchanged. A visible timber
truss should be tested later only with an explicit current grant and a span
within its 6–12 m contract. Do not treat this surface-deck Player capture as
visual acceptance of that truss.

Local evidence: `.artifacts/plan-diagnosis.txt`, `crossing-candidates.csv`,
`crossing-cost-map.csv`, `path-waypoints.csv`, `river-water.csv`,
`route-window-diagnosis.txt`, and the four `.artifacts/PlayerViews/*.png` in
the frozen consumer. The Player build and execution passed structurally;
the visual gate remains open.
