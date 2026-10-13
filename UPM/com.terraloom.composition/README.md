# TerraLoom Composition 0.5.0

Composition is an optional UPM host for projects that use TerraLoom Core, Paths
and Rivers together. The packages retain their independent dependencies;
Composition depends on all three. The package includes a runtime host and an
Editor inspector; it does not include sample scenes or art assets.

## Install the tested package set

Add these four entries to `Packages/manifest.json`. These are the exact Git
revisions resolved by the Unity 6000.3 consumer for the focused Clear check.
The earlier schema-2 Save/Reopen check used 1ba05d2. Keep the pins together; do not replace them with floating
branches or unrelated package revisions.

```json
{
  "dependencies": {
    "com.terraloom.core": "https://github.com/clemensgoering/terraloom-core.git?path=/Packages/com.terraloom.core#014e4ce4fbdfe7bab988ca8b927ef8a17b80fc69",
    "com.terraloom.paths": "https://github.com/clemensgoering/terraloom-paths.git?path=/Packages/com.terraloom.paths#d1f4ef04fba9415571a8efd1f54cf585aeb94216",
    "com.terraloom.rivers": "https://github.com/clemensgoering/terraloom-rivers.git?path=/Packages/com.terraloom.rivers#7889dc7c73f3ada87074bfc5546fec75a731f262",
    "com.terraloom.composition": "https://github.com/clemensgoering/terraloom-rivers.git?path=/UPM/com.terraloom.composition#7889dc7c73f3ada87074bfc5546fec75a731f262"
  }
}
```

Composition 0.5.0 targets Unity 6000.3 and depends on Core, Paths and Rivers
0.3.0. The Git pins above are the tested installation set, not a claim that
other Unity versions or render pipelines have been qualified.

## First recipe

1. Add `TerraLoomWorld`, `TerraLoomPaths`, `TerraLoomRivers`, and
   `RegionDecoration` to your scene. Assign the same Core world to all four
   components. Configure the required path, water, bed and bank materials and
   decoration profile assets.
2. Add `TerraLoomIntegration` and assign Core, Paths and Rivers. Assign its
   **Recipe Decoration** reference, or keep exactly one Region Decoration bound
   to that Core world so the inspector can find it without ambiguity.
3. In the Composition inspector choose **Capture scene settings as new Recipe
   asset**, select a new path under `Assets`, and save. This calls the same
   `CompositionRecipe.CaptureFrom` method available to code users. It validates
   component links, materials and decoration profiles before creating the asset.
   An existing asset is never overwritten. Keep the new asset and its referenced
   profiles/materials in source control.
4. Choose **Generate from saved Recipe (Core terrain)**. The saved recipe
   supplies its own seed, configuration and material references even if scene
   fields have since changed. The optional **Generate On Start** host field
   runs the same recipe at runtime when both recipe and decoration are assigned.
   For existing/foreign Terrain, use **Generate Paths + Rivers on supplied
   terrain** in this inspector; that operation leaves regional decoration to
   its separate component. Recipe generation refuses to replace external
   `TerrainData`.

The public namespace is `TerraLoom.Integration`; the assembly is
`TerraLoom.Composition.Unity`; the Inspector assembly is
`TerraLoom.Composition.Editor`. Add the runtime assembly reference to your own
asmdef only when your scripts use the host, recipe or prepared-request types.

Schema 2 persists the seed, terrain recipe/resolution/size/frequency, anchors,
regions, river/path connections, decoration profiles/count/selection, and module
planning, construction, carving, material, layer, routing, and sampling settings.
Unknown schema versions fail before generation. Generated geometry is not stored
in the recipe; bake it separately if you need scene-persistent outputs.

## Move a Schema 1 recipe to Schema 2 safely

Schema 1 is not silently upgraded from current scene defaults. Its module settings
may not be present, and overwriting the original asset could lose authoring data.

1. Duplicate or commit the original scene and Schema 1 recipe. Keep both intact.
2. Open that source scene and restore the intended Core, Paths, Rivers and
   decoration component values. Compare the old recipe's seed, anchors, regions,
   connections and decoration references with the scene; manually restore any
   values that exist only in the old recipe before capture.
3. Create a **new** Composition Recipe asset and call `CaptureFrom` on those
   restored components. Do not change the old asset's `SchemaVersion` by hand.
4. Compare the new asset's seed, connections, profiles, module limits, bridge
   settings and material/layer references with the source scene. Save a copy of
   the scene using the new asset, close and reopen it, then run
   `GenerateRecipe` and validate the resulting world.
5. Keep the original scene and recipe until Save/Reopen and regeneration succeed.
   If anything differs, return to that source and recapture; do not delete the
   originals as part of migration.

## Consumer-owned data and limits

Recipes reference profile, material and terrain-layer assets; they do not copy,
freeze or grant redistribution rights to those assets. The consumer must provide
them and verify each asset's license before distributing a project or store
package. TerraLoom's own package and third-party asset licenses must also be
checked independently; no redistribution rights are implied by this README.

Scene ownership and component transforms are host responsibilities and are not
stored in the recipe. External `CrossingPermissions` delegates are runtime host
policy and are not serialized or granted by a saved plan. The recipe uses its
explicit anchors and connections instead of automatic target/network discovery.
The host must rebind its transforms and crossing policy after loading a scene.

Initial blank-scene creation bootstraps owned Core terrain before downstream
publication; later recipe generations prepare the source, Rivers, Paths and
decoration together and retain the previous published world if preparation or
publication fails. This package does not provide player-relative streaming,
performance budgets, a complete buyer UI, visual acceptance, or a support matrix.
Those remain project-specific validation and product-release gates.

## Clear and Undo boundary

`Clear()` releases matching world decoration, Paths, Rivers and the composed
owned source in reverse order, restoring the borrowed Core terrain/collider.
It does not delete recipe/profile/material assets. Repeated Clear is supported.
The operation is currently irreversible in the Editor: Ctrl+Z cannot restore
the destroyed transient meshes/TerrainData or the nonserialized source publisher.
Regenerate the saved recipe to reconstruct the world. The sample Clear button
states this limitation. A full resource-aware Undo/Redo adapter is still pending;
recording component fields alone would restore invalid resource references.
