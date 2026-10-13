# Route-bound fall candidate gate

An explicit `RiverFallSite` on a `RiverRequest` identifies one fall anchor between source and mouth. The planner validates the source identity, full bank/pool/outlet footprint, hard reservations, ordinary upstream/downstream reach slopes, terrain support and bounded sample work. A valid six-metre example produces one `RiverFallCandidate` with shared `WaterfallRecipe` geometry and a `FallCandidateOnly` report.

This candidate deliberately has **no `RiverRoute` and no publish permission**. `RiverPlan.Complete` remains false; `ToContribution` and `TerraLoomRivers.Generate` reject it before modifying terrain, water, collider or saved plan. Existing requests without a fall site retain their domain fingerprint and unchanged river route logic. `RiverConnectionSettings` requires the fall ID and anchor ID together.

The next gate is physical prepared earthworks: one opt-in owned terrain tile, a closed supported wall/Collider, continuous lip-to-pool-to-outlet water using this same recipe, contact sampling and transactional publication with old outputs retained on rejection or cancellation. A candidate alone is not a waterfall product feature or a visual acceptance.

Verification: portable RiverPlanner suite 21/21; Unity 6000.3.9f1 EditMode selection 10/10 (`Temp/FallCandidateNative-20261013.xml`).
