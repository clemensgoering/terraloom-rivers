# Frozen curved crossing: bounded construction evidence

Later target follow-up: Paths now checks the actual oriented full-width strip
clipped against bilinear cells (`paths-clipped-grid-v7`). All15 local options per
original target still fail; best along0.41066270750032324/across0.330397403450082,
witness west(12,23.25)/east(84,23.25). This is actual occupied terrain, stronger
than the historical conservative-cell comparison below; not a global trajectory
impossibility proof. Full road stays NoRoute before earthworks, expanded2.
See Paths `Docs/TARGET-ACCESS.md` for semantics, counterexample and native91/91.
Regenerate oldv6 saved plans. No terrain/target/grade/rights changes.
Rebuiltv7 Windows Player exit0,151 probes + CharacterController pass; all4
`frozen-truss-v7-*.png` views were opened. Geometry/contact matches the earlier
local evidence. `ExactSlope-Player.log` includes both exact target failure details.

The separate `TerraLoomFrozenTruss.unity` scene tests the new original Paths side
truss in the unchanged seed42 landscape. Recreate with Tools > TerraLoom >
Integration > Build Frozen Curved Truss Crossing, or batch
`TerraLoom.Integration.Editor.FrozenCrossingBuilder.BuildBatch` after running
`Tools/create_integration.py`. It is a configuration-only scene: Play creates
the channel, water, timber body and both solid guarded approaches at runtime.
Build an actual Windows Player with
`TerraLoom.Integration.Editor.RuntimePlayerValidation.BuildFrozenTrussCrossingBatch`.
Run with `-batchmode -terraloomSmoke -terraloomScreenshot <absolute-output.png>`.
Keep graphics enabled; a hidden normal player can pause when focus is lost.

## What this scene proves, and what it keeps rejected

Terrain remains 96x16x96m, heightmap129 and the frozen analytic height recipe;
world seed42, source(48,8), mouth(48,88), road targets(12,24)/(84,24), river4m,
bank2m, water inset0.6m, protection centered(48,48), size8x12, path2m,
maximum longitudinal AND transverse grade0.35, clearance1.2m. No narrower
water/protection, relocated targets, raised grade or modified source heights.
All materials/textures are original TerraLoom procedural recipes.

The external sample authority explicitly approves partial river windows inside
XZ[24,54]..[64,64]. It is a bounded authored construction-test zone around the
previously rejected 9.51m portion, NOT an automatic grant or the original road
target corridor atZ24. Unselected reaches, protected land and foundations stay
blocked. The initial Z20..28 trial did not yield the required construction.

The complete original road still returns **NoRoute at stage03**, before any
earthwork publication. Its search expands2 nodes; both targets have zero valid
nonzero-length local connectors. This is not BudgetExceeded. Native regression
records |gx|=0.516958872477214 and gz=-0.0735727945963542 in the necessary
conservative grid cell at each target. Even the best rotation has a combined
longitudinal/transverse maximum >=0.369228541649768, above0.35. The existing
planner tests every cell corner in its conservative full-width XZ envelope.
This diagnoses that gate; it does NOT prove impossibility for every exact
oriented footprint, alternate search topology or separately authorized earthwork.

`InspectAuthorizedBridgeAssemblies` exposes locally accepted dry-toe options
using the SAME runtime bridge negotiation and landing rules. The sample may
materialize an exact9.511811m option as **CONSTRUCTION ONLY**, then applies actual
body/side envelope rights, final dry bearing/landing/toe checks and current-grant
fingerprint before activation. `Paths.LastPlan` stays null; composition remains
Failed, and the original route rejection stays available. No fabricated target
connection or M1 acceptance. Clearing restores the original terrain. Withdrawing
sample rights removes the local construction instead of silently keeping it.

The construction has closed carrying parts, 0.65m lower chords, 1.25m side guards,
2m free walking width plus0.30m structure each side, dry full-width end sills and
matching closed guarded ramps. It has no water piers. Actual measured minimum
wet underside5.327024459838867m against selected water4.08702443580385m gives
1.240000024035017m free clearance. This is a bounded gameplay construction,
not an engineering certificate or arbitrary structural coverage proof.

## Validation

Unity6000.3.9f1: Paths plus frozen construction/negative regression87/87,
historical shared PlayMode4/4, no skipped cases. Portable shared38/38 and Paths
collective6/6 pass. Evidence lives in local Integration/.artifacts:
`Truss-AllPaths-Frozen-87.xml/.log`, `Truss-Historical-4.xml/.log`.
The first Player probe caught real ray holes through3mm board seams; a continuous
closed wooden backing now spans the seams. Final Windows Player exits0,151
vertical coverage probes and actual CharacterController traversal across both
ramps/deck pass (`FrozenTruss-PlayerFinal.log`). Four actual1440x900 Player renders
were opened and inspected: `frozen-truss-final-overview.png`, `-west.png`,
`-east.png`, `-underside.png`. Both toe contacts, dry bearing contacts, connected
side frames and free water passage are visible. Initial captures cropped the toe
contacts; final cameras specifically include them. Initial greyscale terrain
misused mesh material tint; final original terrain pixels are coloured and matte,
without altering heights/alphamap rules. The water remains bright/uniform, wood
and contact edges are still schematic; no finished-art or broad M1 acceptance.

Next bounded task: compare the exact full-width target connector footprint with
the conservative rejected cell; keep NoRoute if the declared grade remains
infeasible. Any cut/fill or special target landing needs explicit consumer rights
and construction/profile bounds. Do not move targets or loosen0.35 to make green.
