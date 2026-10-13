# ADR: one soft bank cost per river provenance

Status: **implemented and locally validated, 13 October 2026**. The failing
Buyer B fixture and old cost measurements are in
[BUYER-B-PLAN-DIAGNOSIS.md](BUYER-B-PLAN-DIAGNOSIS.md).

## Problem

Rivers exports one soft `RiverBank` reservation per sampled corridor segment.
Each currently has a transition cost of 2 and a segment-specific owner ID.
Core `CrossingEvaluator` and `CollectiveCrossingEvaluator`, and Paths ground
and bridge-landing cost checks, add every intersecting soft reservation. A
single 9.5×2 m bridge footprint in Buyer B intersects 16 such reservations
near Z41.75 (cost 32) and 9 near Z90.75 (cost 18). The planner selects the
126.56 m near-edge route over an available 81.41 m central route because the
fragmentation penalty dominates. Changing sample spacing can change route
ranking without changing the intended river or bank strength.

## Contract

Add a stable optional **cost provenance group** to Core `AreaReservation`.
Rivers assigns every soft bank piece from one logical river request and module
instance the same group ID. For a given occupied footprint, overlapping soft
reservations in one group contribute the **maximum** configured cost once;
independent groups add. A soft reservation without a group keeps today's
additive behavior. Hard reservations, river beds, protected areas, candidate
containment, terrain clearance, full-footprint grade and external crossing
grants are unaffected.

Core crossing evaluators and Paths ground/ramp evaluators use the same shared
aggregation function. The Core input fingerprint is now
`terraloom-input-v2-soft-cost-provenance`; Paths planner is
`paths-clipped-grid-v9-soft-cost-provenance`; Rivers planner is
`rivers-inset-shoreline-v6-soft-cost-provenance`. Recipe schema stays 2 because
its fields did not change; regenerate plans after package upgrades. Assembled
snapshots retain the Core base identity, so module algorithm versions are part
of the compatibility boundary for contributed costs. Long-edge occupancy
integration remains open: the current formula charges once per overlapping
group for the edge footprint, even for a short contact on a long edge.

## Required red/green evidence

1. Split one bank into many overlapping segments while keeping geometric
   union, strength and provenance constant: crossing and path cost, chosen
   route and seed determinism must remain invariant.
2. Place two independent soft obstacles on the same footprint: their costs
   still add. Different Rivers instances and requests must have different
   stable groups even when their geometry overlaps.
3. Existing hard/protected/bed exclusions, Core candidate acceptance,
   collective authorization, physical 6–12 m truss constraints and source
   rollback must retain their outcomes. Foreign Terrain stays read-only.
4. Run the frozen Buyer B recipe before and after, with candidate/cost traces
   and Player views. The route may change for a justified scoring reason; a
   visually good route cannot be asserted from cost alone.

Portable Core/Paths/Rivers compilation and 40 shared contract checks pass.
The frozen Buyer B recipe SHA-256 remains
`AD73AC57D16F9103394B3B7CF77A13C78DC5823F40DC77D3A586DB2417FF472F`.
Unity 6000.3.9f1 changed the path from 126.56 m at Z90.75 (cost 374.59,
9 bank overlaps, transition 18) to 81.41 m at Z41.75 (cost 150.67,
16 bank overlaps, transition 2). Central bake and Windows Player build/run
succeeded. Four Player captures are in the ignored local consumer at
`.bootstrap-tools/composition-buyer-b-final-20261013/.artifacts/PlayerViewsSoftGroup`.
The route crosses centrally with connected approaches, but this recipe still
has a flat surface-only bridge deck and the sample river appears too straight.
This closes the bank-cost cause of the detour, not physical bridge or final
visual acceptance. This frozen recipe contains no external crossing grant.
