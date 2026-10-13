# Optional Composition package: tested Git installation

Tested 13 October 2026 with Unity 6000.3.9f1. Add these entries to the
consumer's `Packages/manifest.json` dependencies (retain its other packages):

```json
"com.terraloom.core": "https://github.com/clemensgoering/terraloom-core.git?path=/Packages/com.terraloom.core#a66deea564d19ca2137516f5d99f7f689f991254",
"com.terraloom.paths": "https://github.com/clemensgoering/terraloom-paths.git?path=/Packages/com.terraloom.paths#0cbebb81f17656955df1902e724950b7e91baaa4",
"com.terraloom.rivers": "https://github.com/clemensgoering/terraloom-rivers.git?path=/Packages/com.terraloom.rivers#ec972d831541d87f92f48d1334315ca50df6b81d",
"com.terraloom.composition": "https://github.com/clemensgoering/terraloom-rivers.git?path=/UPM/com.terraloom.composition#ec972d831541d87f92f48d1334315ca50df6b81d"
```

Git authentication is required for private repositories. Use complete hashes.
The four pins above were resolved together in the Buyer B Unity 6000.3.9f1
consumer. Its authored physical truss scene regenerated after reopen with
one explicit current grant. The preceding tested pin set also built and ran
the Windows Player with 479 walkability probes. See
[Buyer B physical kit](BUYER-B-PHYSICAL-KIT.md); the sample source for a fresh
consumer is under `Tools/BuyerBForeign*.cs` and
`Tools/BuyerBReviewedCrossing.cs`. Copy the capture script into `Assets`, the
probe/audits into `Assets/Editor`, and run only in an empty test project because
the fixed reviewed IDs belong to that fixture's captured river. The probe
refuses to overwrite its existing output folder.

Composition is opt-in: installing it requires the three modules. Core, Paths
and Rivers have no dependency on Composition; their standalone installations
remain possible. No package automatically discovers third-party modules.

Runtime API and authoring steps: [package README](../UPM/com.terraloom.composition/README.md).
`TerraLoomIntegration` and `PreparedSeededWorldRequest` moved into the package;
their namespace is retained and the host script GUID is unchanged. Consumers
using asmdefs must add `TerraLoom.Composition.Unity`. Remove the old copied host
source/meta before installing to avoid duplicate classes/GUIDs. Demo code still
derives example seed placements, supplies materials and uses the shared engine.

Fresh local fixture: `.bootstrap-tools/composition-consumer-20261012` under
the TerraLoom workspace. No Library or lock file was copied into it. Its lock
records `source=git` and exact hashes for all four packages; no TerraLoom local
file dependencies. Native compile and the focused
`OwnedSourceReloadTests.BakedOwnedSeedWorldReopensWithSourceAndCarveBindings`
passed 1/1 at implementation revision `1ba05d2` (reported in Core NEXT-CHAT;
the rotating `.artifacts/editmode-v1.xml` is overwritten by later runs).
Composition 0.4.0 uses recipe schema 2.
The extended existing test saves a
schema-2 recipe and a baked world with deliberately conflicting scene defaults, reopens the scene, reconstructs through
`GenerateRecipe`, checks identical source heights, rejects schema 999 without
changing the live TerrainData, and generates seed 71 through the same API.
All captured module parameters round-trip against the recipe; the source
height fingerprint and decoration count remain identical. An injected failed
publication restores pre-call scene parameters and the previous live terrain.
Recipe/profile assets belong to the consumer and are not copied into product
packages. The fixture's sample/test files come from this repository's
`IntegrationProject`; the implementation comes from the Git-installed package.

The regular local Integration project also uses the extracted package through
its development file reference; native batch compile exited 0 after migration
(`Integration/.artifacts/CompositionCompile-20261012.log`). Prior copied host/manifest files are backed up
under `.bootstrap-tools/composition-migration-backup`.

At earlier pin `7889dc7`, native compile and the focused
`RecipeClearReleasesOwnedOutputsAndKeepsEmptyUndoRedoConsistent` pass 1/1.
Before/after evidence is preserved in the fixture at
`.artifacts/M2Clear-20261012/{before,after}/editmode.xml`.
The failing before run demonstrated retained decoration. Clear now releases
all world decoration before geometry/source disposal. The after run restores
original render/collider data and exact baseline transient Mesh/TerrainData/
GameObject sets, including repeated Clear and empty-history Undo/Redo.
This is not a reversible Clear implementation: its editor has no Undo command,
and disposed native resources plus the nonserialized source publisher cannot
be restored by field-only Undo. That resource-aware editor lifecycle remains
open, as does Player deferred destruction. The Cancel proof is reused from
`6637856`; it was not rerun in this block.

Schema 2 adds explicit planner/search limits, geometry and carving settings,
region costs/protections, routing bounds, material/layer asset references and
decoration sampling filters. Version 1 is rejected rather than filled from
possibly different defaults; recapture from the intended source scene.

Limits: transforms/scene ownership and runtime crossing-policy delegates remain
host responsibilities. Asset contents are referenced, not copied/frozen. Manual
decoration exclusions are derived by composition from the plans; arbitrary
additional exclusions are not persisted. Initial
terrain bootstrap is not an atomic replacement of an existing world, and seed
recipes require Core-owned terrain. Use `Generate()` for supplied terrain.
Schema migration, streaming, a complete buyer workbench, Player visual parity
and sales acceptance are not claimed. The portable structural audit passes;
portable `--compile` is unavailable because this machine lacks a .NET SDK.

## Editor Clear and recipe recovery (12 October)

The sample Composition inspector asks for confirmation before destructive Clear.
Cancel performs no mutation. Clear does not register an Undo operation: native
meshes/TerrainData are destroyed and the nonserialized source publisher is
disposed, so field-only Undo cannot restore a complete publication.
Assign a saved schema-2 `CompositionRecipe` to `RecipeAsset` and its decoration
component to `RecipeDecoration`. After Clear, use **Rebuild from saved recipe**.
Without a recipe the button is disabled and the confirmation warns that none
is assigned. Rebuilding restores recorded recipe settings through GenerateRecipe;
it is not an exact snapshot of unsaved edits, transforms or external asset contents.
Unsupported recipes/failing generation produce an inline error.

One focused consumer test passed 1/1, Unity6000.3.9f1, seed2042:
`EditorClearCancellationPreservesWorldAndConfirmedClearRestoresSavedRecipe`.
It checks cancellation preserves publication and exact transient resource IDs,
confirmed Clear leaves no generated roots, empty-history Undo/Redo does not
revive roots, and recipe regeneration over conflicting scene widths produces
identical river/path JSON, carved terrain fingerprint and vegetation count.
Evidence: `.bootstrap-tools/composition-consumer-20261012/.artifacts/`
`M2EditorRestore-20261012/editmode.xml` and `attempt2.log`. Attempt1 failed
compilation because the test assembly lacked its sample Editor reference; fixed
before the second run. No third run, production runtime change or Epos edit.
The earlier Player-open note above is superseded by the focused Player lifecycle
proof documented in Core `Docs/NEXT-CHAT.md` (Rivers probe6e404b6).
Full resource-aware Undo/Redo and visual/buyer-workbench acceptance remain open.

## Packaged recipe Inspector (13 October)

Composition 0.5.0 introduced `TerraLoom.Composition.Editor`. Its single custom
Inspector moved from the Integration sample into the UPM package, with the
original script GUID retained. The sample duplicate was removed. The Inspector
resolves one matching Region Decoration or requires an explicit reference when
several share a world; it validates links and required materials before
capturing a new schema-2 recipe asset. Existing assets are not overwritten.
The saved recipe drives GenerateRecipe on Core-owned terrain; supplied terrain
has a separate Paths + Rivers action and still needs its own decoration step.

At `0c20acd`, the Composition World Workbench extension also lives in the
optional UPM package. Its Integration sample duplicate was removed. A fresh
Unity 6000.3.9f1 URP consumer with Core `014e4ce`, Paths `d1f4ef0`, Rivers
`7889dc7` and Composition `0c20acd` resolved four Git packages and passed an
empty-scene entry audit: one coordinator, Workbench menu, same-world module
attachment, Composition tab rediscovery and package Inspector selection.
Evidence: `TerraLoom/.bootstrap-tools/composition-buyer-b-git-20261013/`
`.artifacts/entry-audit.txt` and `entry.log`. Recipe capture, actual generation,
baked Save/Reopen and Player-height visual inspection remain separate Buyer B
gates; this audit does not claim them.

Buyer B generation on that fresh consumer then passed with original Core
seasonal assets, Rivers water styles, project-owned URP path/bridge/terrain
materials and two regional profiles. The shipped Inspector saved schema-2
`WorldRecipe.asset`; the shared build reported one river, one path and one
negotiated bridge deck. A plain scene save lost transient TerrainData on a
separate Unity process reopen. Existing Rivers→Paths bake actions followed by
scene save retained owned original/carved terrain, recipe, both generated roots
and 65 placed regional instances after reopening. The optional Composition
Inspector now exposes one guarded **Bake generated world for scene save**
action that delegates to those two existing bakes, requires current outputs and
a fresh empty Assets folder, and instructs a scene save. The centralized
local-package bake and separate-process reopen passed; evidence is in the same
consumer `.artifacts/{recipe-audit,reopen-audit,central-bake-audit,reopen-central-audit}.txt`.
The complete final Git consumer at Composition `27155dd` repeated the entry,
recipe, centralized bake and separate-process reopen gates successfully. A
Windows Player built and exited 0, generating four 1280×720 views at
`.bootstrap-tools/composition-buyer-b-final-20261013/.artifacts/PlayerViews/`.
Structural Player output reported a 9.5 m bridge deck at X48, Z90.75. The
visual gate remains red: this 96 m world routes the crossing near its edge and
the default deck is a flat strip, while the side view exposes the finite world
boundary. Improve layout/presentation before using this as a buyer-facing
example. Scripted execution is not a timed human clickthrough.

Composition `0566440` adds a read-only presentation review to that same
frozen Buyer B recipe. The Inspector labels the output as a surface-only deck
without a physical timber kit and warns that the path travels 126.56 m versus
80.00 m direct (1.58×), while the deck at X48/Z90.75 is 4.25 m from the
terrain edge against an 8 m corridor-derived review margin. A bounded
in-memory central route stays valid and gets no false detour or edge warning.
The warning survived baked Save/Reopen and cleared with generated output.
The physical-kit status branch was checked with a bound `PathBridgeCrossing`;
the package at `0566440` passed the frozen warning check in Unity.
None of these checks changes route costs or grants. The cost contract proposal
is [ADR-SOFT-BANK-COST.md](ADR-SOFT-BANK-COST.md); the visual release gate is
still open.

Fresh Git consumer: `.bootstrap-tools/composition-buyer-ui-20261013`, Unity
6000.3.9f1, Core014e4ce, Pathsd1f4ef0, Rivers7889dc7, Composition8838eb7.
Focused EditMode `PackageInspectorCapturesNewAssetAndGeneratesFromSavedSettings`
passed 1/1; evidence `.artifacts/RecipeEditor-20261013/editmode.xml`. It checks
package Inspector selection, preflight with no asset on missing Bridge Material,
asset save/reference assignment, and same path/river output after the recipe
restores conflicting scene materials. Earlier local-package test also passed
1/1. The test exercises the methods behind the buttons, not a human visual
clickthrough; the native 15-minute buyer A/B check is the next gate. This slice
adds no default assets, full wizard or combined Bake action.

## Host lifetime check (13 October)

The Composition host releases its published decoration, Paths, Rivers and
owned source when an active host is destroyed in PlayMode. Disabling the host
does not clear the world. Focused native PlayMode test
`CompositionHostLifecyclePlayTests.DestroyingActiveHostReleasesPublishedWorldAfterFrameEnd`
passes 1/1 in Unity 6000.3.9f1 with the package at `7c258c4`. After two
frames, generated roots and the carved TerrainData are gone; a still-bound
Core-owned original source is retained. Evidence is under the isolated Git
consumer `.artifacts/T9HostDestroy-20261013/playmode-after.xml`.

At pin `55517fe`, an EditMode red/green test confirms direct removal of a
previously disabled Composition component releases its own generated Meshes,
TerrainData and roots, then restores the original Core terrain/collider
binding. `ExecuteAlways` makes Unity invoke `OnDestroy` in the Editor;
`Awake`/`Start` generate only in PlayMode. Evidence:
`.bootstrap-tools/composition-consumer-20261012/.artifacts/`
`T9EditorRemoval-20261013/{before,after}.xml`, failed 1/1 then passed 1/1.
Focused Cancel and baked Save/Reopen tests also passed 1/1 each (`cancel.xml`,
`reopen.xml`) against the same pin. Temporary disable retains published output;
actual component removal owns cleanup. Unity Undo of component removal can
restore the component but not disposed meshes/source; regenerate from the
saved recipe. This is not a full Undo contract. Bootstrap rollback,
domain-reload original-source recovery and commit-window callback isolation
remain open T9 work.
