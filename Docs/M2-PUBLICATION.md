# Synchronous prepared composition publication

The source extension is now installed and natively validated against Core
351351d2c5b4340ce9012a7fee98770c9e873c22:137/137 Editor tests and5/5 Play tests
pass, including12 source lifecycle and5 independent Core ownership cases.
Fresh canonical Player build and both2043/2044 runs exit0; full route/real
CharacterController,9.51181m span,538/488 collision probes. All eight canonical
views opened: dry approaches and clear passage, but repeated gravel/timber and
bright white water bands remain visual gaps. Evidence archive:
Integration/.artifacts/M2-SourceLifecycle-20261010 (Canonical-* logs/XML and
canonical-seed* captures). Runtime ownership only, not atomic saved assets/scenes.

This is a bounded whole-world consumer transaction for Core terrain bindings,
Rivers and Paths. Planning, preparation and publication use the existing portable
planners and Unity geometry builders. Product modules do not import each other.
There is no streaming, asynchronous work, scene Undo or general transaction framework.

`PrepareFromSnapshot` returns an `IDisposable` handle. It creates inactive owned
geometry and, for Rivers, a private terrain copy without replacing published
roots, plan JSON or live Terrain/TerrainCollider bindings. Rivers provides a
copied final `GridHeightSource` directly from that candidate TerrainData. Paths
checks bridge supports, actual structure clearance and dry landings against it.
The live world is never temporarily assigned the candidate to capture heights.

The consumer rechecks source/profile/material/transform/settings and external
crossing authority before entering a synchronous binding swap. Product adapter
commit methods do not invoke caller callbacks. The optional consumer diagnostic
boundary callback exists for fault injection and must remain null in production.
Generation and handle lifetimes must be serialized on Unity's main thread.

`CommitBindings` keeps previous resources alive and reversible. Until `SealBindings`,
disposing a committed handle restores the previous root/plan objects/JSON and
exact Terrain and TerrainCollider references, including a separately captured
collider reference. Disposing pending handles releases their candidate meshes and
terrain copy. Both handles are sealed without cleanup before any retirement.
`Complete` retires previous owned resources after both modules bind
and output metadata is captured. Clear then restores the original foreign source.
The consumer invalidates old region decoration only after collective success.
Retirement exceptions are reported as post-commit cleanup diagnostics while the
new composition remains Ready. A throwing custom Paths destroy callback triggers
default cleanup; remaining River retirement is still attempted. Collider identity
captures presence and instance ID so a removed component cannot masquerade as
an originally absent collider. Missing-component Unity wrappers can differ by
CLR reference; those are treated as absent. Commit writes the validated component.

Cancellation is checked during planning/preparation and immediately before the
final path binding. Cancellation before that boundary rolls back the attempt.
After both bindings succeed, a subsequently cancelled token belongs to the next
run; it cannot mark the published world cancelled through the schedule's usual
post-action check. A thrown diagnostic fault before completion still rolls back.

Failed attempts retain previous resources but report Failed/Cancelled and are
not considered Ready; retaining geometry is not permission to consume stale
inputs. A subsequent normal generation revalidates all inputs and can become
Ready. Changes to the source made by an external caller are not undone.

The bounded seeded crossing recipe now uses Core `WorldSourcePublication` and
the consumer `GenerateWithSource`: explicit candidate captures, reversible
source seed/anchors/ownership, and all three handles sealed before retirement.
Its old source is retained on planning/materialization failure or cancellation.
Other generators do not automatically acquire this lifecycle. See Core
Docs/M2-SOURCE-LIFECYCLE.md for ownership, explicit composed binding and limits.
Decoration is retained or invalidated, not regenerated inside the transaction.
Standalone legacy Paths callbacks remain caller-controlled and outside the
collective callback-free consumer swap.

Earlier output-only canonical validation:19/19 targeted transaction tests,120/120 broad Editor tests
and4/4 shared Play tests pass against the actual sibling packages. Portable
Paths/Rivers runtime builds have zero warnings/errors. Canonical evidence is
archived separately at Integration/.artifacts/M2-Canonical-20261010. The fresh
canonical Player build and run both exit 0: fullRoute=True, span=9.51181 m,
485 collision probes and a real CharacterController traversal. All four fresh
overview/west/east/underside captures were opened and inspected: dry approach
contacts and bearings, clear water passage, no submerged bridge deck. Repeated
gravel and timber patterns and bright water highlights remain prototype art;
this is bounded crossing acceptance, not general visual release approval.

Earlier isolated validation evidence is recorded in Core/Docs/NEXT-CHAT.md and the local
Integration/.artifacts/M2-Prepared-20261010 archive. Initial isolated-package
native fault suite passed 9/9; broad post-audit isolated suite passed 118/118,
including 18 transaction cases. Final metadata-order targeted18/18 and post-audit
Play4/4 pass; the extra missing->present collider case makes final targeted19/19.
An isolated-package test is not a canonical
installation. Player pictures from before the ownership audit remain labelled
as such; the audit changes bindings/retirement, not the geometry builders.
