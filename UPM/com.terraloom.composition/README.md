# TerraLoom Composition

Optional UPM package for a world that uses Core, Paths and Rivers together.
Install all four Git-pinned packages in the consumer manifest. Composition
depends on the three modules when installed; none of the three depends on
Composition. Its location under `UPM` keeps it out of the standalone Rivers
project's embedded packages. No example art or editor assemblies are included.

Git URL: `https://github.com/clemensgoering/terraloom-rivers.git?path=/UPM/com.terraloom.composition#<full-commit>`.
Use matching 0.3.0 Core/Paths/Rivers Git pins at the manifest root because those
packages are not hosted on a public Unity registry.

Create **Assets > Create > TerraLoom > Composition Recipe**. Assign Core,
Paths, Rivers and RegionDecoration components to a `TerraLoomIntegration`
component. Every module and decoration must reference the same Core world.
Assign the recipe and decoration to the host for Generate On Start, or call:

```csharp
bool ready = composition.GenerateRecipe(recipe, decoration);
if (!ready) Debug.LogError(composition.Diagnostics);
```

`CompositionRecipe.CaptureFrom(composition, decoration)` copies current source
settings into an asset. Save that asset through the editor as usual. Schema 1
stores seed, terrain recipe/resolution/size/frequency, manual anchors, regions,
river/path connections and decoration profiles/count/selection. Unknown schema
versions fail before generation with an explicit supported-version diagnostic.
Schema migration is deliberately explicit; unknown versions are never guessed.

The public namespace remains `TerraLoom.Integration` to preserve existing source
consumers. The assembly is `TerraLoom.Composition.Unity`; asmdefs referencing
the host/request must now reference it. The host MonoScript GUID is preserved.
The sample supplies its assets and derives demo-specific seed placements, then
uses this same publication engine. There is no duplicate planning algorithm.

The recipe reconstructs owned procedural terrain. With supplied terrain use
`Generate()` instead; this API does not overwrite external TerrainData.
Initial creation bootstraps Core terrain before downstream publication; atomic
retention applies to subsequent generations of an existing owned world.
Module planner settings and materials remain on the scene components. Profile
asset references must be shipped by the consumer. Generated geometry is not
stored in the recipe; bake separately if the scene needs persisted outputs.
This package does not claim streaming, visual acceptance or a complete buyer UI.
