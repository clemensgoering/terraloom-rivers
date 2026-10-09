using System.Linq;
using NUnit.Framework;
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
    }
}
