using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TerraLoom.Core.Unity;
using TerraLoom.Rivers.Editor;
using TerraLoom.Rivers.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Tests
{
    /// <summary>Two filtered Unity processes; no Epos or Composition fixture changes.</summary>
    public sealed class RiverForeignWaterQueryPersistenceTests
    {
        private const string Folder="Assets/TerraLoom/Tests/Generated/ForeignWaterQueryPersistence";
        private const string Scene=Folder+"/ForeignRiver.unity";
        [Serializable] private sealed class Expected { public double x,z,y; }

        [Test] public void BakeProvidedTerrainRiverQuery()
        {
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EnsureFolder(Folder);
                var run=Folder+"/Run-"+Guid.NewGuid().ToString("N");EnsureFolder(run);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var source=new TerrainData{name="Buyer supplied terrain",heightmapResolution=129,size=new Vector3(32,16,32)};
                var heights=new float[129,129];
                for(int z=0;z<129;z++)for(int x=0;x<129;x++)heights[z,x]=(6f-.03f*(z*.25f))/16f;
                source.SetHeights(0,0,heights);AssetDatabase.CreateAsset(source,run+"/SuppliedTerrain.asset");
                var originalHeights=source.GetHeights(0,0,129,129);
                var terrain=Terrain.CreateTerrainGameObject(source).GetComponent<Terrain>();
                var host=new GameObject("Foreign terrain water query");var world=host.AddComponent<TerraLoomWorld>();
                world.WorldId="foreign-water-query";world.Terrain=terrain;world.UseSeedAnchors=false;
                world.ManualAnchors=new[]{new ManualWorldAnchor{Id="source",Position=new Vector3(16,5.88f,4)},
                    new ManualWorldAnchor{Id="mouth",Position=new Vector3(16,5.16f,28)}};
                Assert.That(world.OwnsTerrain,Is.False);
                var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;rivers.AutomaticSourceAndMouth=false;
                rivers.Connections.Add(new RiverConnectionSettings{Id="provided-river",SourceId="source",MouthId="mouth"});
                var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                rivers.WaterMaterial=material;rivers.BedMaterial=material;rivers.BankMaterial=material;
                Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
                var route=rivers.LastPlan.Routes.Single();var point=route.WaterPolyline[route.WaterPolyline.Count/2];
                Assert.That(rivers.WaterSurfaceQuery.TryGetSurfaceHeight(point.X,point.Z,out var y),Is.True);
                Assert.That(y,Is.EqualTo(point.Y).Within(.0001));
                Assert.That(source.GetHeights(0,0,129,129),Is.EqualTo(originalHeights),"Supplied source terrain was modified.");
                RiversSampleBuilder.Bake(rivers,run+"/Baked");
                File.WriteAllText(Folder+"/Expected.json",JsonUtility.ToJson(new Expected{x=point.X,z=point.Z,y=point.Y}));
                Assert.That(EditorSceneManager.SaveScene(host.scene,Scene),Is.True);
            }
            finally { Restore(previous); }
        }

        [Test] public void ReopenProvidedTerrainQueryWithoutRegeneration()
        {
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene),Is.Not.Null,"Run Bake in an earlier Unity process.");
                EditorSceneManager.OpenScene(Scene);
                var rivers=Object.FindFirstObjectByType<TerraLoomRivers>();Assert.That(rivers,Is.Not.Null);
                Assert.That(rivers.World.OwnsTerrain,Is.False);Assert.That(rivers.LastPlan,Is.Null);
                var root=rivers.GeneratedRoot;var data=rivers.World.Terrain.terrainData;
                Assert.That(data,Is.SameAs(rivers.CarvedTerrainData));
                Assert.That(rivers.World.Terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(data));
                Assert.That(AssetDatabase.Contains(rivers.OriginalTerrainData),Is.True);
                var expected=JsonUtility.FromJson<Expected>(File.ReadAllText(Folder+"/Expected.json"));
                var query=rivers.WaterSurfaceQuery;Assert.That(query,Is.Not.Null,rivers.Diagnostics);
                Assert.That(query.TryGetSurfaceHeight(expected.x,expected.z,out var y),Is.True);
                Assert.That(y,Is.EqualTo(expected.y).Within(.0001));
                Assert.That(query.TryGetSurfaceHeight(0,0,out _),Is.False);
                Assert.That(rivers.LastPlan,Is.Null);Assert.That(rivers.GeneratedRoot,Is.SameAs(root));
                Assert.That(rivers.World.Terrain.terrainData,Is.SameAs(data));
                var original=rivers.OriginalTerrainData;rivers.Clear();
                Assert.That(rivers.WaterSurfaceQuery,Is.Null);
                Assert.That(rivers.World.Terrain.terrainData,Is.SameAs(original));
                Assert.That(rivers.World.Terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(original));
                Assert.That(query.TryGetSurfaceHeight(expected.x,expected.z,out var retained),Is.True);
                Assert.That(retained,Is.EqualTo(y));
            }
            finally { Restore(previous); }
        }
        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            var parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
        private static void Restore(SceneSetup[] previous)
        {
            if(previous.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
    }
}
