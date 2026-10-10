# V0 short timber crossing — 10 October 2026

Reproduce: run `Tools/create_integration.py`, then choose Tools > TerraLoom >
Integration > Build Short Timber Crossing in the generated Integration project.
Separate TimberSourceTerrain, TimberRiver/TimberPaths assets and
`Assets/TerraLoom/Integration/Generated/TerraLoomTimberCrossing.unity` preserve the
wide Brush recipe. Batch: `RuntimePlayerValidation.BuildTimberCrossingBatch` as
Unity `-executeMethod`, then `.artifacts/PlayerTimberCrossing/TerraLoom.exe
-batchmode -terraloomSmoke -terraloomScreenshot <absolute-path.png>
-logFile <absolute-log>`.

The narrow fixture uses river width2m / bank width0.75m, seed42. Actual deck
span5.01181m, top6.102196m including4cm offset, beam underside5.602196m, main
body3360 vertices. Full dry footprint closure makes the negotiated span larger
than nominal water+bank widths. Kit limit stays6m. The existing wide fixture is
tested to reject; no stretched beams or relaxed limit to make the demo pass.

## Final validation

- Isolated native EditMode **185/185** passed, including bridge/body, whole-width
  supports/toe, candidate-clearance and cancellation/budget. XML at
  `.bootstrap-tools/RiverRadiusValidation/.artifacts/EditMode-v1.xml`,10:21 local.
- Integration EditMode **4/4**: saved body/ramp collider ownership, source terrain
  restoration and profile. `.artifacts/TimberFinalEdit.xml`.
- Integration PlayMode **6/6**: wide rejection preserving prior composition,
  narrow success, actual body collider raycasts and CharacterController walking
  the COMPLETE route including both approaches, rebuild/Clear/failure cleanup.
  `.artifacts/TimberFinalPlay.xml`. Both runs use the final toe contact gate.
- Fresh portable contracts **35/35**, all pure assemblies compile without warnings.
- Windows build/player exit0; `.artifacts/V0TimberBuild.log` and
  `V0TimberFinalPlayer.log`. Runtime rebuild uses the public composition.
- Discrete water probes6318 vertices min5.000114mm /2106 centres min76.614857mm;
  not continuous water/terrain or shore-contact proofs.

## Images actually reviewed

All ten final player files under `Integration/.artifacts/Evidence/` opened:
`v0-timber-final-42.png`, `-close`, `-player`, `-river-section`, `-river-player`,
`-landing-0/1`, `-landing-side-0/1`, `-underside` (suffixes end `.png`). Temporary
yellow1.8m reference has no gameplay collider. It partly occludes side0; side1
shows the toe/support unobstructed.

Earlier `v0-timber-ramp-baseline-42-landing-side-0/1.png`: SAME narrow plan/camera/
light before solid approaches. Walking-sheet vertex gaps above final terrain
0.03999996–1.36964798m,512 samples per approach. New solid approach has actual
wood slab/beam volume and buried toe. The same top-to-terrain diagnostic remains
nonzero by design: supported wood, no earthfill. These pairs support the approach
comparison. Previous wide `v0-surfaces-42` is DIFFERENT geometry and cannot prove
same-case improvement.

## Judgment and limits

Closed beams/sills and solid approaches are visible and physically used. Still
**no V0 art acceptance**: blocky massive sills, long blunt ramps, stretched/striped
side UVs, repetitive wood, coarse path stones/hard edges, uniform canal, grassy
wet shore and primitive repeated trees/rocks. Low-poly samples are not the style
goal: target relief has irregular rock bands, fine materials and dense credible
valley vegetation. Original assets must be developed toward that target.

Supports verify rectangular grid extrema; toe verifies a whole-width contact
LINE. Neither substitutes for arbitrary area rights/collective curved crossing
authorization. M1 remains open; no new atomic multi-module rollback claim.
Recipe has open sides and rejects unsupported geometry/grid inputs. See Paths
`Docs/TIMBER-BRIDGES.md` for API and limits.

Next bounded visual step: quieter path gravel/soft shoulders/wet strip, then
original rock/vegetation variation and reference-level relief. Compare Epos short
wood-collar/earth approaches after physical earth support is measured. Waterfall
design separately drafted in `WATERFALL-DESIGN.md`; not implemented.
