# Crossing water-band diagnosis, 10 October 2026

The bright transverse strip in the 9.51181 m crossing fixture is direct PBR sun
specular on a surface whose animated waves previously changed color only. It is
not established as a foam, terrain clipping, bridge geometry or environment
reflection defect. This diagnosis applies to TerraLoom's RiverWater shader;
Epos uses other active receiver paths and needs its own evidence.

No canonical package shader was changed. Root's active Epos source/native window
requires its shared package sources to remain stable. The isolated ripple-normal
candidate is **rejected for installation**: it breaks the rigid band but introduces
too many bright repeated glints. No art release is claimed.

## Reproduce the bounded Player comparison

Create the sibling Integration project using `Tools/create_integration.py` first.
Close its editor and test runners, then run from the workspace in PowerShell:

```powershell
./Rivers/Tools/Run-WaterBandEvidence.ps1 -Python '<python executable>' -UnityEditor '<Unity 6000.3.9f1 executable>'
```

The development tool copies Rivers into an ignored diagnostic package, changes
only the ignored Integration manifest/capture fixture, builds a real Windows
Player and runs seeds 2043 and 2044. It restores the original manifest, lock and
capture-script bytes in `finally`. Shared Core/Paths/Rivers package files are
read-only. The build regenerates ignored consumer sample configuration assets
and replaces its diagnostic Player; regenerate a canonical Player before normal
smoke validation. This is a development experiment, not an installed module mode.

For each seed, the same four existing cameras capture baseline, foam-off,
direct-specular-off, environment-off, both-specular-off, smoothness0.65, world
normals and ripple-normals0.025m. Water shader time is fixed at4 in all modes;
each camera's variants render synchronously on the same generated world.
Material clones preserve base properties; state text records camera, lights,
ambient/fog, pipeline, shader keywords/properties and mesh hashes. Provenance
also hashes the shader and project/URP settings. No geometry or water heights
change between variants. Diagnostics compile runtime keyword variants with
`multi_compile_local_fragment`; `shader_feature_local_fragment` can strip them
and invalidate a runtime ablation.

## Native and visual results

Final evidence: `Integration/.artifacts/WaterBand-20261010` (local, ignored).
Build and both Player runs exit0. Both retain fullRoute=True, span9.51181m,
538/488 collider probes and actual CharacterController traversal, with unchanged
seed terrain fingerprints. There are64 captures and64 state files. All eight
ripple candidate views were opened; baseline overview/underside and individual
ablation views were opened during diagnosis. Not every diagnostic image has a
separate visual acceptance review.

White water pixels in the overview, using the same visible-water mask from the
normal diagnostic (G>240,R/B<220), and R/G/B>=250 in the compared capture:

| Seed | Baseline | Foam off | Direct specular off | Environment off | Smoothness0.65 | Ripple0.025m |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 2043 | 725 | 725 | 0 | 731 | 2388 | 3328 |
| 2044 | 486 | 486 | 0 | 489 | 2213 | 2997 |

This mask/count is a bounded diagnostic, not a general water quality score.
Direct specular off removes the band. Roughness and ripple candidates increase
clipped bright pixels; neither is accepted. Disabling all specular is a cause
isolation, not the proposed production appearance. Dry approach contacts and
clear bridge passage remain visible, but gravel/timber repetition and plain
terrain remain separate acceptance gaps.

Initial metadata failure (`JsonUtility` cannot serialize engine objects) is
retained as Initial-Metadata-Failure.log. Initial stripped keyword variants are
invalid ablation evidence; final multi-compile captures replace those conclusions.
The failed attempts did not alter canonical package sources or weaken physics.

## Next bounded correction

Keep the existing cameras, two seeds, lighting and material basis. Evaluate a
controlled direct sun-glint response without changing hydraulic geometry or
simply disabling lighting/foam. Before accepting any shader correction, check a
few fixed times at the same camera for flicker, IceAmount=1 for static output,
and the regional summer/ice transition for seams. A temporal or regional pass
for the rejected ripple candidate is not claimed. Install only after visual
acceptance and a coordinated short shared-source window; then validate the
installed shader. Core elevation/slope and editor transparency remain next in
the agreed queue.
