# Combined waterfall data recipe

`WaterfallRecipe` is an immutable, Unity-free Rivers contract combining an explicit
upper reach, `WaterfallFallSurface` V2, a level receiving pool and submerged V1
outlet sill. Manual placement and seeded planners pass the same physical anchors
and dimensions to the same constructor. No editor-only alternative algorithm.

```csharp
var recipe = new WaterfallRecipe(
    lip: new WorldPoint(32, 11.2, 32),
    impact: new WorldPoint(32, 8, 32),
    pool: new WorldPoint(32, 8, 33.7),
    outletSill: new WorldPoint(32, 8, 37.7),
    forwardX: 0, forwardZ: 1,
    channelWidth: 3, poolWidth: 4.2,
    channelDepth: .75, poolDepth: 1.1, outletSillDepth: .5,
    upstreamSlope: .012, downstreamSlope: .035,
    outletTransitionLength: 3);

var upper = recipe.SampleUpperSurface(distance: 0, lateral: .75);
var fallLip = recipe.Fall.Sample(progress: 0, lateral: .75);
var fallImpact = recipe.Fall.Sample(progress: 1, lateral: .75);
var lower = recipe.SampleLowerSurface(station: 0, lateral: .75);
// upper == fallLip; fallImpact == lower. No render offset in physical data.
var poolCentre = recipe.SampleLowerReach(recipe.PoolStation);
var sill = recipe.SampleLowerReach(recipe.OutletStation);
```

Three explicit sampling domains avoid the vertical X/Z-height ambiguity:

- Upper distance:0atlip, increases **upstream**. Water height increases with
  upstreamSlope, fixed channel width/depth. Samples identify Upstream section.
- Fall progress:0atlip,1atimpact. Actual planar3Dfall supports exactly zero run
  and near-vertical run; explicit Forward/Across remain stable. Fall UV length
  can use `Fall.DistanceAt(progress)`, no artificial nonzero horizontal distance.
- Lower station:0atimpact, increases **downstream**. PoolStation/OutletStation
  are measured from actual impact, **not from lip**. Pool is level until sill;
  width rises smoothly to PoolWidth then returns to ChannelWidth. CentreDepth
  remains PoolDepth until pool centre, then rises to OutletSillDepth. After sill,
  water grade transitions smoothly to DownstreamSlope, bed depth to ChannelDepth.

All anchors share the explicit straight horizontal axis; pool/sill match impactY
and are ordered with gaps>.01m. PoolWidth1–1.5×channel; poolDepth>=channelDepth,
0<sillDepth<=channelDepth. Finite inputs within±1e9; upper/lower sample distances
0..1e6m; signed lateral limited by the sampled half width. Arbitrary rigid yaw
and translation work in this pure data contract (not a claim about terrain
adapter rotation). Invalid anchors/levels/depths/slopes/queries fail explicitly.

`WaterfallPoolShape` is internal shared V1/V2 arithmetic; there is no virtual lip
or fabricated fall run to make V1 accept a vertical fall. Existing V1 station
origin, operation order and public API remain intact; historical V0 remains intact.
60,002 V0/V1 samples compared bit-for-bit against source from published parent
5236b1672b7023c2b7ef187da399bba3829da83d: identical coordinates, width, depth,
section. Local frozen-reference harness is ignored development evidence.

Validation: pure runtime compile0warnings/errors, portable24/24 (10V1+6Fall+8Recipe),
native same24/24 in isolated RiverRadiusValidation, existing integration11/11
(7V0/V1+4V2 contact/lifecycle/export). Recipe cases include full-width joins for
run0/.025/3.3, pool/sill anchors/depths,10,001receiving samples vsV1, arbitrary
yaw/translation, outlet tangent continuity and invalid inputs. Evidence:
`.bootstrap-tools/RiverRadiusValidation/.artifacts/CombinedWaterfall-24.xml/.log`,
`Integration/.artifacts/WaterfallCombinedRegression-11.xml/.log`.

This step delivers **data**, not a new visually accepted scene. Existing r2 and
V2 geometry packets deliberately remain separate historical comparison cases.
Next consumer work must build/export a new combined case from this recipe,
including actual terrain/solid/water/contact buffers and same-camera validation.
Terrain bed cross-section, cliff design, UV/material adapters, impact effects,
terrain-hole ownership, site planning/rights and gameplay are not inferred from
this contract. No general foreign-terrain writer or automatic waterfall siting
is added. Shader/mesh adapters must consume physical intent and apply any render
offset explicitly once, preserving both lip and impact contacts.

Epos final V2 comparison (World-reported):3Edit+1Play pass, all9controllers grounded,
max endpoint discrepancy.173092mm, maxeye.073671mm; nativeground/contact0m.
Here actually opened only normal-case side/lip/foot3of9finalimages from
20261010-102357Z-PlayMode-0_Game and read vertical-result.json. Blue flat curtain,
technical cliff panels, upper U appearance, foam rails and transitions remain
visually unresolved. ShaderSource review from World reports **no TFD vertex
displacement**; scene-colour/refraction appearance was the issue. Epos adapter
tests belong to Epos; they are not TerraLoom tests or final art approval.
