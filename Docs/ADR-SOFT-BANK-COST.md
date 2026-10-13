# ADR (proposed): one soft bank cost per river provenance

Status: **proposed, read-only design**. No Core, Paths or Rivers planner
behavior is changed by this note. The failing Buyer B fixture and exact cost
measurements are in [BUYER-B-PLAN-DIAGNOSIS.md](BUYER-B-PLAN-DIAGNOSIS.md).

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

## Proposed contract

Add a stable optional **cost provenance group** to Core `AreaReservation`.
Rivers assigns every soft bank piece from one logical river request and module
instance the same group ID. For a given occupied footprint, overlapping soft
reservations in one group contribute the **maximum** configured cost once;
independent groups add. A soft reservation without a group keeps today's
additive behavior. Hard reservations, river beds, protected areas, candidate
containment, terrain clearance, full-footprint grade and external crossing
grants are unaffected.

Core crossing evaluators and Paths ground/ramp evaluators must use the same
shared aggregation function. An edge spanning long stretches should integrate
group occupancy along bounded samples rather than charging an entire long
edge for a tiny bank contact; preserve deterministic work budgets. The
precise cost formula and snapshot/profile fingerprint version should be
agreed before code changes so Editor and runtime plans remain identical.

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

This contract deliberately leaves the current source untouched until Core
and Paths file ownership is clear and a cross-package red/green test can be
run. A Composition warning is a near-term visibility aid, not a fix for the
cost semantics.
