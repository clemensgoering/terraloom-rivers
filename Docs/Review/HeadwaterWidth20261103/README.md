# Headwater width control, 2026-11-03

Same saved mountain-source scene, same camera, same flat URP/Unlit water color. Left/baseline: 3 m river width. Right/diagnostic: each visible water-mesh vertex is displaced to twice its lateral distance from the saved centreline. The 6 m image is visual only: terrain excavation and physical water query were deliberately not changed, and the scene was not saved or published. It exposes more overhang and does not solve the hard band/shoreline gap.

A real `Width=6` rebuild on the unchanged source terrain was attempted separately and rejected by the Rivers planner: `NoRoute`, maximum excavation cut exceeded at 5 checks. No planner limit was relaxed. This review branch contains only diagnostic images, not a valid wide river product fixture. The low-poly trees/rocks are historical assets in this fixture, not current NatureLibrary approval.
