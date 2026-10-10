# Manual and seeded curved crossing recipes

Validation:100/100 native Editor tests and4/4 shared Play tests pass; Rivers portable runtime compiles
without warnings/errors. Final evidence is archived under Integration
`.artifacts/CrossingVariations-20261010`.

These are bounded consumer examples, using the existing timber truss and normal
Planner -> current external grants -> PlanStore -> Publish pipeline. No new
construction type, product module or permission default is introduced. The original
frozen negative case and the first positive targeted corridor remain separate.

| Recipe | Source / mouth XZ | Road targets XZ | Road routing zone | Crossing |
| --- | --- | --- | --- | --- |
| Manual | (54,8) / (44,88) | (24,72) / (72,76) | X8..88, Z64..84 | 11.962713m at Z65.382055 |
| Seeded 2043 / 2044 | Seed parity selects X50 / X46, Z8 -> Z88 | X24 / X72, seed-derived Z with -2/+2 offsets | X16..80, Z52..64 | 9.511811m, different positions/heights and approaches |

Manual retains the frozen source terrain and original central protection. The
seeded recipe creates an owned copy of source TerrainData, retaining the original
layers/materials. Its explicitly defined analytic flat-floor valley varies floor
level, downhill grade and shoulder relief phase with the seed. Seed-derived typed
anchors feed the same capture/planning APIs as manually authored anchors; the
river planner creates the protected-area detour. This is a sample-specific seed
policy, not uniform random Core anchors or a universal hydraulic source selector.
The optional general DrainageValley recipe and independent random river endpoints
did not admit these brush/protection settings in initial checks (hydraulic dead
end / excavation and bend failure). Those failures were not waived. No claim of
arbitrary seeded terrain compatibility follows from the analytic examples.

The first general-recipe failure is retained as `HydraulicSeedBoundaryTests`:
Core DrainageValley, seed2043,129 samples,96x16x96m,frequency4; the independent
hash streams select source(50,8),mouth(54,88), with the original central protection
and brush settings. Native1/1 passes: two identical attempts reject at stage02
with `NoRoute Hydraulic dead end`, hard protection15 and uphill water/bed184.
TerrainData, TerrainCollider data and height digest remain unchanged; neither
module publishes geometry and Paths is never planned. The fixture supplies two
existing layers to Core's ground/rock generation style; painting is outside this
hydraulic claim. Original failed build logs and the new XML/log are archived at
Integration `.artifacts/HydraulicSeedBoundary-20261010`. This isolated-package
consumer regression preserves a real rejection; it is not a positive general
terrain-generation or M2 canonical-installation result.

`CrossingRecipeVariant.Rebuild` is shared by the editor button and runtime Start;
it clears downstream outputs before regenerating its own source. It restores the
original source and frees only its owned copy on Clear. This full rebuild is not
an atomic M2 transaction. Terrain origin/size/resolution are fixed by this sample
at (0,0,0), 96x16x96 and129 samples. Change the seed then rebuild explicitly.

`FrozenCurvedCrossing.LimitSectionsToCrossingStrip` is an opt-in external sample
policy. Each crossing grant selects only already declared partial windows that
touch that crossing's full body-width strip. Areas remain clipped to the explicit
authority zone. The 64 sections per grant and1024 grants per batch limits remain;
there is no bounding-box permission union or removal of foreign/hard protections.
The full shared snapshot is retained. Saved JSON still requires fresh grants.

Build scenes/Windows players with `RuntimePlayerValidation.BuildManualCrossingBatch`
and `BuildSeededCrossingBatch`. Saved scenes are configuration-only. For a seeded
Player run, add `-terraloomRecipeSeed 2043` (or2044) to the normal graphics-enabled
`-batchmode -terraloomSmoke -terraloomScreenshot <absolute>.png` arguments.

Player evidence: manual678 collider probes; seed2043=538, seed2044=488. All three
executables exit0 after real CharacterController traversal of the complete road.
All12 URP overview/toe/toe/underside captures were opened: dry toe contacts, bank
bearings and free passage under the deck are visible. Seed terrain digests differ:
2043 `9fcc6a36c0069b1fea65308011eadabc38ded8be42f0564a1fa9ca14b62cc2f8`,
2044 `4b1fa1800c083b900aa0b3af3d15547a83862cba7afda89ea1edea6796dc9a1f`.
Native tests compare real terrain, anchors and route waypoints;2043 ->2044 changes
them, and returning to2043 reproduces them. JSON reload and current-rights checks
run for manual and both seeds. Seed-scoped grant bounds and rights withdrawal
also have negative tests. Earlier target-access, wet-gap and body-outside-zone
negative regressions remain active.

Water highlight/end contact, patterned gravel and simple timber remain prototype
materials. This increases limited M1 integration evidence; it is not a universal
M1/art acceptance or structural load certificate. M2 composition publication,
rollback/ownership/cancellation review remains next after the breadth review.
