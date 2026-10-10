using System.Linq;
using NUnit.Framework;
using TerraLoom.Core.Editor;
using TerraLoom.Core.Unity;
using TerraLoom.Integration;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TerraLoom.Tests
{
    public sealed class SavedCompositionTests
    {
        [Test] public void SavedTimberFixtureRestoresSolidBodyColliderAndPlanningLimits()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomTimberCrossing.unity");
                var instance=Object.FindFirstObjectByType<TerraLoomIntegration>();
                Assert.That(instance.Paths.TimberBeamBridge,Is.True);
                Assert.That(instance.Paths.CaptureProfile().MaximumBridgeSpan,Is.EqualTo(6));
                Assert.That(instance.Paths.CaptureProfile().BridgeClearance,Is.EqualTo(instance.Paths.BridgeClearance).Within(.000001));
                Assert.That(instance.Paths.CaptureProfile().BridgeBodyDepth,Is.EqualTo(.5));
                Assert.That(instance.ValidateCurrent(out var reason),Is.True,reason);
                var parts=instance.Paths.GeneratedRoot.GetComponentsInChildren<PathGeneratedGeometry>();
                var body=parts.Single(p=>p.Role==PathGeometryRole.BridgeStructure);
                var mesh=body.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(AssetDatabase.Contains(mesh),Is.True);Assert.That(body.GetComponent<MeshCollider>().sharedMesh,Is.SameAs(mesh));
                Assert.That(body.GetComponent<MeshCollider>().enabled,Is.True);
                foreach(var deck in parts.Where(p=>p.Role==PathGeometryRole.BridgeDeck))
                {Assert.That(deck.GetComponent<MeshRenderer>().enabled,Is.False);Assert.That(deck.GetComponent<MeshCollider>().enabled,Is.False);}
                var ramps=parts.Where(p=>p.Role==PathGeometryRole.BridgeRamp).ToArray();Assert.That(ramps.Length,Is.EqualTo(2));
                foreach(var ramp in ramps)
                {Assert.That(AssetDatabase.Contains(ramp.GetComponent<MeshFilter>().sharedMesh),Is.True);Assert.That(ramp.GetComponent<MeshCollider>().enabled,Is.True);}
                var route=instance.Paths.LastPlan; // LastPlan is ephemeral after reload; saved geometry stays usable.
                Assert.That(route,Is.Null);
                var deckMesh=parts.First(p=>p.Role==PathGeometryRole.BridgeDeck).GetComponent<MeshFilter>().sharedMesh;
                float span=Mathf.Max(deckMesh.bounds.size.x,deckMesh.bounds.size.z);
                TestContext.WriteLine("TERRALOOM_TIMBER_SAVED span="+span+"m, deckTop="+deckMesh.bounds.max.y+"m, beamUnderside="+(deckMesh.bounds.max.y-.5f)+"m, bodyVertices="+mesh.vertexCount);
                var original=instance.Rivers.OriginalTerrainData;instance.Clear();
                Assert.That(instance.World.Terrain.terrainData,Is.SameAs(original));Assert.That(mesh,Is.Not.Null,"Clear cannot destroy persisted body asset.");
            }
            finally{if(setup.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
        }

        [Test] public void SavedBrushCrossingNeedsOnlyWaterAndRetainsTerrainOwnership()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomBrushCrossing.unity");
                var instance=Object.FindFirstObjectByType<TerraLoomIntegration>();Assert.That(instance,Is.Not.Null);
                Assert.That(instance.Rivers.TerrainBrush,Is.True);
                Assert.That(instance.Rivers.BedMaterial,Is.Null);Assert.That(instance.Rivers.BankMaterial,Is.Null);
                Assert.That(instance.ValidateCurrent(out var reason),Is.True,reason);
                Assert.That(AssetDatabase.Contains(instance.Rivers.OriginalTerrainData),Is.True);
                Assert.That(AssetDatabase.Contains(instance.Rivers.CarvedTerrainData),Is.True);
                foreach(var terrainData in new[]{instance.Rivers.OriginalTerrainData,instance.Rivers.CarvedTerrainData})
                {
                    Assert.That(terrainData.terrainLayers.Length,Is.EqualTo(3));
                    foreach(var layer in terrainData.terrainLayers)
                    {
                        Assert.That(AssetDatabase.Contains(layer.diffuseTexture),Is.True);
                        Assert.That(AssetDatabase.Contains(layer.normalMapTexture),Is.True);
                        Assert.That(layer.diffuseTexture.width,Is.EqualTo(256));
                        Assert.That(layer.normalMapTexture.width,Is.EqualTo(256));
                        Assert.That(layer.tileSize,Is.EqualTo(new Vector2(2,2)));
                        Assert.That(layer.smoothnessSource,Is.EqualTo(TerrainLayerSmoothnessSource.DiffuseAlphaChannel));
                    }
                }
                var riverMeshes=instance.Rivers.GeneratedRoot.GetComponentsInChildren<MeshFilter>();
                Assert.That(riverMeshes.Length,Is.EqualTo(1));Assert.That(AssetDatabase.Contains(riverMeshes[0].sharedMesh),Is.True);
                Assert.That(instance.Paths.GeneratedRoot.GetComponentsInChildren<PathGeneratedGeometry>().Any(p=>p.Role==PathGeometryRole.BridgeDeck),Is.True);
                var original=instance.Rivers.OriginalTerrainData;instance.Clear();
                Assert.That(instance.World.Terrain.terrainData,Is.SameAs(original));
                Assert.That(instance.World.Terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(original));
            }
            finally{if(setup.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
        }
        [Test] public void SavedCompositionRestoresMeshesMaterialsUvsAndOriginalTerrainOwnership()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomIntegration.unity");
                var instance=Object.FindFirstObjectByType<TerraLoomIntegration>();Assert.That(instance,Is.Not.Null);
                Assert.That(instance.Paths.GeneratedRoot,Is.Not.Null);Assert.That(instance.Rivers.GeneratedRoot,Is.Not.Null);
                Assert.That(AssetDatabase.Contains(instance.Rivers.OriginalTerrainData),Is.True);
                Assert.That(AssetDatabase.Contains(instance.Rivers.CarvedTerrainData),Is.True);
                Assert.That(instance.World.Terrain.terrainData==instance.Rivers.CarvedTerrainData,Is.True);
                Assert.That(instance.ValidateCurrent(out var freshness),Is.True,freshness);
                Assert.That(instance.Rivers.OriginalTerrainData.GetAlphamaps(64,100,1,1)[0,0,1],Is.GreaterThan(.95f),"Original regional snow paint persisted.");
                Assert.That(instance.Rivers.CarvedTerrainData.GetAlphamaps(64,100,1,1)[0,0,1],Is.GreaterThan(.95f),"Carved terrain snow paint persisted.");
                Assert.That(instance.Paths.GroundMaterial.GetColor("_BaseColor").r,Is.EqualTo(.55f).Within(.001));
                var gravel=(Texture2D)instance.Paths.GroundMaterial.GetTexture("_BaseMap");var pixel=gravel.GetPixel(12,12);
                Assert.That(pixel.r,Is.EqualTo(pixel.g).Within(.005),"Gravel texture remains grayscale.");
                Assert.That(pixel.r,Is.EqualTo(pixel.b).Within(.005),"Gravel texture remains grayscale.");
                var decoration=instance.World.GetComponent<RegionDecoration>();Assert.That(decoration,Is.Not.Null);
                Assert.That(decoration.PlacedCount,Is.GreaterThan(250));Assert.That(decoration.Profiles.Length,Is.EqualTo(4));
                Assert.That(WorldEditorExtensions.All.Any(e=>e.Id=="paths"),Is.True);Assert.That(WorldEditorExtensions.All.Any(e=>e.Id=="rivers"),Is.True);
                Assert.That(WorldEditorExtensions.All.Single(e=>e.Id=="paths").Find(instance.World),Does.Contain(instance.Paths));
                foreach(var filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    Assert.That(filter.sharedMesh,Is.Not.Null);Assert.That(AssetDatabase.Contains(filter.sharedMesh),Is.True,filter.name);
                    Assert.That(filter.GetComponent<MeshRenderer>().sharedMaterials.All(m=>m&&AssetDatabase.Contains(m)),Is.True,filter.name);
                    if(filter.GetComponent<PathGeneratedGeometry>()||filter.GetComponent<RiverGeneratedGeometry>())
                        Assert.That(filter.sharedMesh.uv2,Is.EqualTo(filter.sharedMesh.uv));
                }
                var bridge=instance.Paths.GeneratedRoot.GetComponentsInChildren<PathGeneratedGeometry>().First(p=>p.Role==PathGeometryRole.BridgeDeck);
                Assert.That(bridge.GetComponent<MeshCollider>().sharedMesh==bridge.GetComponent<MeshFilter>().sharedMesh,Is.True);
                Assert.That(bridge.GetComponent<MeshRenderer>().shadowCastingMode,Is.EqualTo(ShadowCastingMode.On));
                instance.Clear();Assert.That(instance.World.Terrain.terrainData,Is.Not.Null);
                Assert.That(AssetDatabase.Contains(instance.World.Terrain.terrainData),Is.True);
            }
            finally
            {
                if(setup.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
        [Test] public void DynamicSceneIsConfigurationOnlyAndModuleEditorsFindSeparateObjects()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomDynamic.unity");
                var recipe=Object.FindFirstObjectByType<SeededWorldDemo>();Assert.That(recipe,Is.Not.Null);
                Assert.That(recipe.GenerateOnStart,Is.True);Assert.That(recipe.Composition.World.Terrain,Is.Null);
                Assert.That(Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None),Is.Empty);
                Assert.That(Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None),Is.Empty);
                Assert.That(recipe.Composition.Paths.gameObject,Is.Not.SameAs(recipe.Composition.World.gameObject));
                Assert.That(recipe.Composition.Rivers.gameObject,Is.Not.SameAs(recipe.Composition.World.gameObject));
                foreach(string id in new[] {"paths","rivers","core.decoration","consumer.composition"})
                {var extension=WorldEditorExtensions.All.Single(e=>e.Id==id);Assert.That(extension.Find(recipe.Composition.World).Length,Is.EqualTo(1));Assert.That(TerraLoomEditorIcons.Get(extension.Icon),Is.Not.Null);}
            }
            finally{if(setup.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
        }
    }
}
