using System.Collections;
using NUnit.Framework;
using TerraLoom.Core.Unity;
using TerraLoom.Rivers.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace TerraLoom.Tests
{
    public sealed class RiverRuntimeTests
    {
        [UnityTest] public IEnumerator StartupGenerationAndDestructionReleaseTheTerrainCopyAndKeepOriginal()
        {
            var data=new TerrainData { heightmapResolution=33,size=new Vector3(32,8,32) };
            var heights=new float[33,33];for(int z=0;z<33;z++)for(int x=0;x<33;x++)heights[z,x]=.5f;
            data.SetHeights(0,0,heights);var terrain=Terrain.CreateTerrainGameObject(data);
            var host=new GameObject("Runtime rivers");var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                var world=host.AddComponent<TerraLoomWorld>();world.Terrain=terrain.GetComponent<Terrain>();
                world.ManualAnchors=new[] {new ManualWorldAnchor {Id="a",Position=new Vector3(16,0,8)},new ManualWorldAnchor {Id="b",Position=new Vector3(16,0,24)}};
                var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;rivers.WaterMaterial=mat;rivers.BedMaterial=mat;rivers.BankMaterial=mat;rivers.GenerateOnStart=true;
                yield return null;
                Assert.That(rivers.GeneratedRoot,Is.Not.Null,rivers.Diagnostics);var copy=rivers.CarvedTerrainData;var mesh=rivers.GeneratedRoot.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(world.Terrain.terrainData,Is.SameAs(copy));
                Object.Destroy(rivers);yield return null;yield return null;
                Assert.That(world.Terrain.terrainData,Is.SameAs(data));Assert.That(copy==null,Is.True);Assert.That(mesh==null,Is.True);
                Assert.That(data.GetHeight(16,16),Is.EqualTo(4).Within(.001));
            }
            finally { Object.Destroy(host);Object.Destroy(terrain);Object.Destroy(data);Object.Destroy(mat); }
            yield return null;
        }
    }
}
