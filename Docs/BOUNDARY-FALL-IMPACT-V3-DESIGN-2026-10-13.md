# Bounded impact-zone design before implementation

Scope: one explicit fall on a Core-owned, axis-aligned 32 m tile. Legacy `RiverFallSite`/`WaterfallRecipe` V2 stays the default and keeps its exact zero-run, planar curtain and level receiving pool. A new explicit V3 site mode changes the authored physical route and receives a distinct domain fingerprint; it must never silently reinterpret an old scene or JSON plan. Foreign terrain, diagonal earthworks, overlapping holes, stale inputs and incomplete receiving routes remain rejected.

For V3, the lip is still at the fall anchor and the lower ground is sampled from the same source Terrain snapshot. The **impact** moves downstream by `R = min(0.8 m, 0.35 × channel width)`, bounded below by one height-grid/sample support step and below the first pool station. R is a controlled authoring heuristic, not a measured hydraulic prediction. The receiving station origin moves with that actual impact: pool/outlet world anchors remain where the planner previously placed them, while `PoolStation`, `OutletStation` and lower-reach distance decrease by R. This is a measurable trajectory, not a render-only offset. Flight time is `T = sqrt(2 × drop / 9.81) s`, initial horizontal speed `R / T`; normalized flight time `u` gives downstream position `lip + R·u` and height `lipY − drop·u²`. The upstream reach ends horizontal at the lip, matching the initial horizontal tangent. The sheet then freely falls into the carved impact bed. It may stand clear of the wall; it must never cut through the original terrain or leave the receiving bed unsupported across its full width.

The first implementation keeps the receiving pool water level and bed profile unchanged, while carving it from the actual impact station. The fall and pool meshes share the exact impact row; any shading-normal blend is separate from physical contact validation. There is deliberately **no C1 physical streamline across a plunge**: falling water dissipates into a turbulent pool. A future local impact-surface or foam/boil treatment requires its own surface contract and must not masquerade as an ordinary monotonically descending RiverRoute, query or crossing offer. A zero/near-zero horizontal-run V2 recipe stays legacy; V3 requires R above the bounded support step and fails closed otherwise.

Proof order: first portable V2 unchanged/V3 trajectory and station tests, then V3 read-only candidate checks, followed by owned earthworks with full-width free-sheet clearance, impact-bed contact, manifold collider, stale/cancel rollback and saved-plan continuity. Finally compare Gate6i vs V3 with identical seed42, materials, light, camera positions and bridge walk in separately named Player evidence. If the fixed `foot` and `fall-wide` images do not improve meaningfully, retain the design/negative evidence but revert the unconsumed V3 product API and switch.

## 13 October experimental result: rejected at visual gate

The bounded V3 prototype passed its nine focused recipe checks, canonical
Integration Bake 1/1, separate-process Reopen 1/1, and a Windows D3D11 Player
run (61 bridge steps, three landings, ten fixed views, exit 0). The physical
curve required exact station deduplication at the float reach end and a
free-sheet clearance check in place of the legacy wall-contact check. Those
checks were implemented for the experiment; neither was used to relax a
production gate. The same seed 42, materials and fixed `foot` view still show
a hard vertical water plate with an abrupt diagonal pool join. The visible
gain is insufficient for V0 acceptance. The unconsumed V3 runtime, editor and
test changes were therefore reverted; the shipped fall remains V2.

Local, immutable comparison evidence is in
`Integration/.artifacts/V3ImpactPlayer-20261013/` with `manifest.json`,
Player log and all ten images, compared against the `Gate6i` folder named in
`BOUNDARY-FALL-SHOULDER-CONTACT-2026-10-13.md`. The next treatment must change
the visible impact surface and foot presentation as one physical contract;
curving only the falling sheet does not remove the plate silhouette. Keep the
same fixed camera/material gate and no false C1 streamline through impact.
