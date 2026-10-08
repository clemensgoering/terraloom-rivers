# Paths and Rivers cooperation

Paths and Rivers exchange offers through shared `TerraLoom.Core` contracts. Rivers publishes a `RiverOffer` containing a corridor, its crossing candidates, and its reservations; callers combine those contributions in one `PlanSnapshot` with a shared `PlanIdentity`. Paths submits crossing requests to Core's evaluator through `PathCrossingNegotiator`, preserving a decision for every request, including conflicts. Neither module depends on the other.

The package manifest, lockfile and bootstrap config pin the published Core contract commit `987abb096961c2d2b39fdae951e02898c439a697`. The normal compile check fetches this exact revision. Local contract proof builds may use development Core through an explicit `--development-core` override; it leaves the pin unchanged. No machine-specific path is saved in package dependencies.
