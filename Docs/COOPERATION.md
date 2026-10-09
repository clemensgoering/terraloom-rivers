# Paths and Rivers cooperation

Paths and Rivers exchange offers through shared `TerraLoom.Core` contracts. Rivers publishes a `RiverOffer` containing a corridor, its crossing candidates, and its reservations; callers combine those contributions in one `PlanSnapshot` with a shared `PlanIdentity`. Paths submits crossing requests to Core's evaluator through `PathCrossingNegotiator`, preserving a decision for every request, including conflicts. Neither module depends on the other.

The package manifest, lockfile and bootstrap config pin the published Core contract commit `094e511a9dcf10abdbeb37b87aa85caa89317407`. The normal compile check fetches this exact revision. Local contract proof builds may use development Core through an explicit `--development-core` override; it leaves the pin unchanged. No machine-specific path is saved in package dependencies.

## Lokale Plan-Zusammenfuehrung

`RiverOffer.ToContribution(moduleInstanceId)` exportiert das kopierte Angebot
fuer `PlanAssembler.Assemble(input, contributions)` im Core. Die Identity muss
exakt aus der gemeinsamen `PlanningInput` stammen. Mehrere Flussinstanzen haben
verschiedene Modulinstanz-IDs; pro Instanz wird ein aggregierter Beitrag geliefert.
Der portable Core-Nachweis prueft Rivers-Export, Core-Zusammenfuehrung und Paths-
Querungsbewertung gemeinsam. Noch keine Flussgeometrie/Hydrologie.
