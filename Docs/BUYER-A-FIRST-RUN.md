# Buyer A: supplied terrain, first stop (13 October 2026)

Fresh consumer: `.bootstrap-tools/composition-buyer-a-20261013`, created with
only Unity ProjectSettings and Git packages, with no copied TerraLoom sample
scene or art. Unity 6000.3.9f1, URP 17.3.0. Manifest pins:
Core `014e4ce4fbdfe7bab988ca8b927ef8a17b80fc69`,
Paths `d1f4ef04fba9415571a8efd1f54cf585aeb94216`,
Rivers `7889dc7c73f3ada87074bfc5546fec75a731f262`,
Composition `f86c96a9a0a18ab00367ddaef05c967d03bed8bc`.

Reproduce by copying `Tools/BuyerAProbe.cs` to `Assets/BuyerA/Editor` in that
fresh Git consumer and running `-executeMethod BuyerAProbe.Run` with the Unity
Editor. The checked-in source creates its own 96 m TerrainData asset, 1 prefab
TreeInstance at the center, a uniform DetailPrototype grass layer, two terrain
layers and three simple URP materials. It configures four manual anchors and
one river/road connection, then calls the **packaged Inspector's**
`IntegrationEditor.GenerateOnSuppliedTerrain` action. No sample builder or
private algorithm is called. This is an automated buyer-path probe, not a
human clickthrough. No manual click count or under-15-minute claim is made.

Requested fields: Core World + supplied Terrain; Paths Ground and Bridge
materials, 0.35 maximum slope and 1.2 m bridge clearance; Rivers Water
material, Terrain Brush, 0.6 m inset and terrain layer 1 for sediment. Bed and
Bank materials are unnecessary in Terrain Brush mode. No Region Decoration
component or profile is needed for the supplied-terrain Paths + Rivers button.
The original source TerrainData is an asset and is never written by the probe.

Native outcome: setup inside the Editor script 0.47 s, generation at 1.63 s
after script start (cold Unity import and authoring time excluded). Composition
reported one river, one path and one negotiated bridge deck. Source heights
remained byte-for-byte equal; selected Terrain and TerrainCollider both bound
the same carved copy. No missing-material or route error occurred.

**First blocker:** the carved copy still had the TreeInstance in the river
corridor. It retained 1 of 1 original trees and 32 grass detail entries in the
center 4×4 detail cells. The source remains untouched, but vegetation is not
cleared for the generated river/road. The probe deliberately throws
`BUYER_A_BLOCKER existing Terrain TreeInstances/details remain in the generated
river/road corridor.` Its nonzero exit is the expected red result. Evidence:
`.bootstrap-tools/composition-buyer-a-20261013/.artifacts/BuyerA-20261013/build.log`
and the authored `Assets/BuyerA/Authored.unity` in that local consumer. The
script-backed setup is reproducible from this repo's Tools source. Player
screenshots and visual bridge/landing acceptance were **not run** because the
agreed gate stops at the first concrete blocker. The bridge is proven in the
plan, not yet from player height.

Smallest candidate fix: on the **transient carved TerrainData**, after final
river and path plans are known and before publication, clear TreeInstances
whose prefab footprint meets the final river or road corridor and zero detail
cells covered by those corridors. Use the final curved polylines and their
width/bank/clearance rather than broad rectangles. Leave the supplied source
asset intact. One focused regression should verify source tree/detail arrays
unchanged, no vegetation in either published corridor, carved render/collider
bindings, and restoration of the original source on Clear. Keep the same
filter path in Editor and runtime composition; assess standalone Rivers/Paths
separately before claiming package-wide behavior.
