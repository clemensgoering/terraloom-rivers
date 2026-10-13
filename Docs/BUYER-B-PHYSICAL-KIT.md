# Buyer B: explicit physical truss crossing

Validated 13 October 2026 on Unity 6000.3.9f1, URP 17.3, Windows Player.
This is a **separate supplied-terrain fixture**, not a rewrite of the frozen
schema-2 `WorldRecipe.asset` surface-deck case. The original recipe remains
the negative/compatibility reference for a valley whose western bank is too
low for the physical kit's 10 cm dry-landing guard.

The Composition Inspector now exposes one bridge-construction selector bound
to the Paths component and a scene `Crossing Permission Source` field. The
shipped `ExplicitCrossingPermissionSource` stores only reviewed crossing IDs,
water/candidate IDs and XZ rectangles. It binds those exact entries to the
captured snapshot on each request. The alternative
`ICurrentCrossingPermissionSource` interface supports a consumer's dynamic
seed-aware policy. Neither selecting a kit nor saving a Recipe creates a
grant. Stale IDs, withdrawn/disabled sources and missing sources fail closed.

Buyer B supplied terrain uses one reviewed crossing at Z55.75, ten listed
reach sections, a 9.5 m TimberTrussBridge, and an owned carved copy of the
foreign terrain. Existing Core collective coverage, water clearance, hard
reservations, final dry-footprint and Paths physical geometry checks remain
active. The native authoring run published one `BridgeStructure` and two
ramps with one bound crossing. Reopening the authored scene reproduced that
result; missing source or changed candidate ID rejected generation, and a
disabled source made the prior output stale. Reopening the baked Player scene
retained the source, persistent carved TerrainData, a 1,944-vertex truss mesh,
collider and ramps. Windows Player passed 479 walking/collision probes and
captured five views, including side and underside.

Local ignored evidence:

- `.bootstrap-tools/composition-buyer-b-final-20261013/.artifacts/buyer-b-foreign-builtinsource-build.log`
- `.bootstrap-tools/composition-buyer-b-final-20261013/.artifacts/buyer-b-foreign-builtinsource-reopen.log`
- `.bootstrap-tools/composition-buyer-b-final-20261013/.artifacts/buyer-b-foreign-baked-reopen.log`
- `.bootstrap-tools/composition-buyer-b-final-20261013/.artifacts/buyer-b-foreign-builtinsource-player.log`
- `.bootstrap-tools/composition-buyer-b-final-20261013/.artifacts/BuyerBForeignPlayerViews/`

Visual review: the bridge is recognizably physical, with lateral timber
trusses, underside members and dry end supports. The supplied terrain is a
uniform slope and the river is straight; these screenshots establish
function and construction only. The original valley recipe cannot yet host
this kit because its carved west-bank edge is below the final dry-landing
guard. Do not weaken that guard to make the demo green. Improve the water/
landscape reference and author a genuinely dry valley crossing next.
