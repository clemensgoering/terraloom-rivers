# Optional Composition package: tested Git installation

Tested 12 October 2026 with Unity 6000.3.9f1. Add these entries to the
consumer's `Packages/manifest.json` dependencies (retain its other packages):

```json
"com.terraloom.core": "https://github.com/clemensgoering/terraloom-core.git?path=/Packages/com.terraloom.core#014e4ce4fbdfe7bab988ca8b927ef8a17b80fc69",
"com.terraloom.paths": "https://github.com/clemensgoering/terraloom-paths.git?path=/Packages/com.terraloom.paths#d1f4ef04fba9415571a8efd1f54cf585aeb94216",
"com.terraloom.rivers": "https://github.com/clemensgoering/terraloom-rivers.git?path=/Packages/com.terraloom.rivers#7889dc7c73f3ada87074bfc5546fec75a731f262",
"com.terraloom.composition": "https://github.com/clemensgoering/terraloom-rivers.git?path=/UPM/com.terraloom.composition#7889dc7c73f3ada87074bfc5546fec75a731f262"
```

Git authentication is required for private repositories. Use complete hashes.
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

At current pin `7889dc7`, native compile and the focused
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

## Host lifetime check (13 October)

The Composition host releases its published decoration, Paths, Rivers and
owned source when an active host is destroyed in PlayMode. Disabling the host
does not clear the world. Focused native PlayMode test
`CompositionHostLifecyclePlayTests.DestroyingActiveHostReleasesPublishedWorldAfterFrameEnd`
passes 1/1 in Unity 6000.3.9f1 with the package at `7c258c4`. After two
frames, generated roots and the carved TerrainData are gone; a still-bound
Core-owned original source is retained. Evidence is under the isolated Git
consumer `.artifacts/T9HostDestroy-20261013/playmode-after.xml`.

An EditMode test that directly removed a Composition component (including
after re-enabling it) remained red: EditMode without ExecuteAlways did not
invoke this MonoBehaviour lifetime hook. Do not claim automatic cleanup for
Inspector removal, including a disabled component. Use confirmed Clear before
removing the host in the Editor. A separate Editor ownership/removal strategy
is required. Bootstrap rollback, domain-reload original-source recovery and
commit-window callback isolation are also open T9 work.
