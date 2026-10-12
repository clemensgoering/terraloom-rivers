# Optional Composition package: tested Git installation

Tested 12 October 2026 with Unity 6000.3.9f1. Add these entries to the
consumer's `Packages/manifest.json` dependencies (retain its other packages):

```json
"com.terraloom.core": "https://github.com/clemensgoering/terraloom-core.git?path=/Packages/com.terraloom.core#014e4ce4fbdfe7bab988ca8b927ef8a17b80fc69",
"com.terraloom.paths": "https://github.com/clemensgoering/terraloom-paths.git?path=/Packages/com.terraloom.paths#d1f4ef04fba9415571a8efd1f54cf585aeb94216",
"com.terraloom.rivers": "https://github.com/clemensgoering/terraloom-rivers.git?path=/Packages/com.terraloom.rivers#4c8fe61c917efc29390a359a6c39dde3dfd7231d",
"com.terraloom.composition": "https://github.com/clemensgoering/terraloom-rivers.git?path=/UPM/com.terraloom.composition#4c8fe61c917efc29390a359a6c39dde3dfd7231d"
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
passed 1/1 (`.artifacts/editmode-v1.xml`). The extended existing test saves a
schema-1 recipe and a baked world, reopens the scene, reconstructs through
`GenerateRecipe`, checks identical source heights, rejects schema 999 without
changing the live TerrainData, and generates seed 71 through the same API.
Recipe/profile assets belong to the consumer and are not copied into product
packages. The fixture's sample/test files come from this repository's
`IntegrationProject`; the implementation comes from the Git-installed package.

The regular local Integration project also uses the extracted package through
its development file reference; native batch compile exited 0 after migration
(`Integration/.artifacts/CompositionCompile-20261012.log`). Prior copied host/manifest files are backed up
under `.bootstrap-tools/composition-migration-backup`.

Limits: module planner/material settings remain on scene components, initial
terrain bootstrap is not an atomic replacement of an existing world, and seed
recipes require Core-owned terrain. Use `Generate()` for supplied terrain.
Schema migration, streaming, a complete buyer workbench, Player visual parity
and sales acceptance are not claimed. The portable structural audit passes;
portable `--compile` is unavailable because this machine lacks a .NET SDK.
