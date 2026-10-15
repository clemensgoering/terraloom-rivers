using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Core.Unity;
using TerraLoom.Integration;
using TerraLoom.Paths.Unity;
using TerraLoom.Paths.Editor;
using TerraLoom.Rivers.Unity;
using TerraLoom.Rivers.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Tests
{
    public sealed class ForeignCompositionWaterQueryTests
    {
        private const string Folder="Assets/TerraLoom/Integration/Generated/ForeignCompositionWaterQuery";
        private const string Scene=Folder+"/World.unity";
        [Serializable] private sealed class Expected
        { public double x,y,z;public string fingerprint,riverJson,pathJson;public int ground,deck,body,ramps,approvals; }

        [Test] public void BakeSuppliedTerrainCompositionWithAuthorizedTruss()
        {
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EnsureFolder(Folder);string run=Folder+"/Run-"+Guid.NewGuid().ToString("N");EnsureFolder(run);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var source=new TerrainData{heightmapResolution=129,size=new Vector3(96,16,96)};
                var heights=new float[129,129];
                for(int z=0;z<129;z++)for(int x=0;x<129;x++)heights[z,x]=(6-.03f*(96f*z/128))/16;
                source.SetHeights(0,0,heights);AssetDatabase.CreateAsset(source,run+"/SuppliedTerrain.asset");
                var original=source.GetHeights(0,0,129,129);
                var terrain=Terrain.CreateTerrainGameObject(source).GetComponent<Terrain>();
                var host=new GameObject("Supplied terrain composition roundtrip");var world=host.AddComponent<TerraLoomWorld>();
                world.WorldId="foreign-composition-query";world.Seed=42;world.Terrain=terrain;world.UseSeedAnchors=false;
                world.ManualAnchors=new[]{
                    new ManualWorldAnchor{Id="source",Position=new Vector3(48,0,4)},new ManualWorldAnchor{Id="mouth",Position=new Vector3(48,0,92)},
                    new ManualWorldAnchor{Id="west",Position=new Vector3(24,0,57)},new ManualWorldAnchor{Id="east",Position=new Vector3(72,0,57)}};
                var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material,run+"/Surface.mat");
                var paths=host.AddComponent<TerraLoomPaths>();paths.World=world;paths.GroundMaterial=material;paths.BridgeMaterial=material;
                paths.AutomaticNetwork=false;paths.MaximumSlope=.35f;paths.BridgeClearance=1.2f;paths.TimberTrussBridge=true;
                paths.MaximumFordDepth=0;paths.Connections.Add(new PathConnectionSettings{Id="buyer-road",StartId="west",EndId="east"});
                var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;rivers.WaterMaterial=material;
                rivers.AutomaticSourceAndMouth=false;rivers.TerrainBrush=true;rivers.WaterInset=.6f;
                rivers.Connections.Add(new RiverConnectionSettings{Id="buyer-river",SourceId="source",MouthId="mouth"});
                var composition=host.AddComponent<TerraLoomIntegration>();composition.World=world;composition.Rivers=rivers;composition.Paths=paths;
                composition.RestrictPathRouting=true;composition.PathRoutingZone=new Rect(16,54,64,10);
                Assert.That(world.OwnsTerrain,Is.False);
                Assert.That(composition.Generate(),Is.False,"Without an explicit bridge grant the fixed crossing must fail.");
                Assert.That(rivers.GeneratedRoot,Is.Null);Assert.That(paths.GeneratedRoot,Is.Null);
                var reviewed=new WorldBounds(43.25,54,52.75,64);
                var candidates=composition.LastSharedInput.Crossings.Where(c=>(c.AllowedKinds&CrossingKind.Bridge)!=0&&c.Bounds.Overlaps(reviewed)).ToArray();
                Assert.That(candidates.Length,Is.InRange(1,32));
                var sections=candidates.Select(c=>new CrossingSectionApproval{
                    WaterId=c.WaterId,CandidateId=c.Id,
                    MinXZ=new Vector2((float)Math.Max(c.Bounds.MinX,reviewed.MinX),(float)Math.Max(c.Bounds.MinZ,reviewed.MinZ)),
                    MaxXZ=new Vector2((float)Math.Min(c.Bounds.MaxX,reviewed.MaxX),(float)Math.Min(c.Bounds.MaxZ,reviewed.MaxZ))}).ToArray();
                var authority=host.AddComponent<ExplicitCrossingPermissionSource>();
                authority.Approvals=candidates.SelectMany(c=>new[]{0,1}.Select(axis=>new CrossingApproval{CrossingId="bridge:"+c.Id+":"+axis,Sections=sections})).ToArray();
                composition.CrossingPermissionSource=authority;
                Assert.That(composition.Generate(),Is.True,composition.Diagnostics);
                Assert.That(composition.ValidateCurrent(out var reason),Is.True,reason);
                Assert.That(source.GetHeights(0,0,129,129),Is.EqualTo(original));
                var parts=paths.GeneratedRoot.GetComponentsInChildren<PathGeneratedGeometry>(true);
                Assert.That(parts.Count(p=>p.Role==PathGeometryRole.Ground),Is.GreaterThan(0));
                Assert.That(parts.Count(p=>p.Role==PathGeometryRole.BridgeDeck),Is.EqualTo(1));
                Assert.That(parts.Count(p=>p.Role==PathGeometryRole.BridgeStructure),Is.EqualTo(1));
                Assert.That(parts.Count(p=>p.Role==PathGeometryRole.BridgeRamp),Is.EqualTo(2));
                var route=rivers.LastPlan.Routes.Single();var point=route.WaterPolyline[route.WaterPolyline.Count/2];
                var query=rivers.WaterSurfaceQuery;Assert.That(query.TryGetSurfaceHeight(point.X,point.Z,out var y),Is.True);
                Assert.That(y,Is.EqualTo(point.Y).Within(.0001));
                var expected=new Expected{x=point.X,y=point.Y,z=point.Z,fingerprint=query.Identity.InputFingerprint,
                    riverJson=rivers.PlanJson,pathJson=paths.PlanJson,approvals=authority.Approvals.Length,
                    ground=parts.Count(p=>p.Role==PathGeometryRole.Ground),deck=parts.Count(p=>p.Role==PathGeometryRole.BridgeDeck),body=1,ramps=2};
                RiversSampleBuilder.Bake(rivers,run+"/Rivers");PathsSampleBuilder.Bake(paths,run+"/Paths");
                Assert.That(composition.ValidateCurrent(out var baked),Is.True,baked);
                File.WriteAllText(Folder+"/Expected.json",JsonUtility.ToJson(expected));
                Assert.That(EditorSceneManager.SaveScene(host.scene,Scene),Is.True);
            }
            finally { Restore(previous); }
        }

        [Test] public void ReopenSuppliedTerrainPreservesQueryPathsAndBridge()
        {
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(Scene);
                var composition=Object.FindFirstObjectByType<TerraLoomIntegration>();Assert.That(composition,Is.Not.Null);
                var rivers=composition.Rivers;var paths=composition.Paths;var terrain=composition.World.Terrain;
                var expected=JsonUtility.FromJson<Expected>(File.ReadAllText(Folder+"/Expected.json"));
                Assert.That(composition.World.OwnsTerrain,Is.False);Assert.That(composition.ValidateCurrent(out var reason),Is.True,reason);
                Assert.That(rivers.LastPlan,Is.Null);Assert.That(paths.LastPlan,Is.Null);
                var riverRoot=rivers.GeneratedRoot;var pathRoot=paths.GeneratedRoot;var data=terrain.terrainData;
                var query=rivers.WaterSurfaceQuery;Assert.That(query,Is.Not.Null,rivers.Diagnostics);
                Assert.That(query.Identity.InputFingerprint,Is.EqualTo(expected.fingerprint));
                Assert.That(query.TryGetSurfaceHeight(expected.x,expected.z,out var y),Is.True);
                Assert.That(y,Is.EqualTo(expected.y).Within(.0001));Assert.That(query.TryGetSurfaceHeight(0,0,out _),Is.False);
                Assert.That(rivers.PlanJson,Is.EqualTo(expected.riverJson));Assert.That(paths.PlanJson,Is.EqualTo(expected.pathJson));
                Assert.That(terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(data));
                Assert.That(data,Is.SameAs(rivers.CarvedTerrainData));Assert.That(AssetDatabase.Contains(data),Is.True);
                Assert.That(AssetDatabase.Contains(rivers.OriginalTerrainData),Is.True);
                var parts=pathRoot.GetComponentsInChildren<PathGeneratedGeometry>(true);
                Assert.That(parts.Count(p=>p.Role==PathGeometryRole.Ground),Is.EqualTo(expected.ground));
                Assert.That(parts.Count(p=>p.Role==PathGeometryRole.BridgeDeck),Is.EqualTo(expected.deck));
                Assert.That(parts.Count(p=>p.Role==PathGeometryRole.BridgeStructure),Is.EqualTo(expected.body));
                Assert.That(parts.Count(p=>p.Role==PathGeometryRole.BridgeRamp),Is.EqualTo(expected.ramps));
                foreach(var part in parts)
                {
                    var mesh=part.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(AssetDatabase.Contains(mesh),Is.True);
                    Assert.That(part.GetComponent<MeshCollider>().sharedMesh,Is.SameAs(mesh));
                }
                var authority=composition.CrossingPermissionSource as ExplicitCrossingPermissionSource;
                Assert.That(authority,Is.Not.Null);Assert.That(authority.isActiveAndEnabled,Is.True);
                Assert.That(authority.Approvals.Length,Is.EqualTo(expected.approvals));
                Assert.That(composition.ValidateCurrent(out var after),Is.True,after);
                Assert.That(rivers.LastPlan,Is.Null);Assert.That(paths.LastPlan,Is.Null);
                Assert.That(rivers.GeneratedRoot,Is.SameAs(riverRoot));Assert.That(paths.GeneratedRoot,Is.SameAs(pathRoot));
                Assert.That(terrain.terrainData,Is.SameAs(data));
                var original=rivers.OriginalTerrainData;composition.Clear();
                Assert.That(rivers.WaterSurfaceQuery,Is.Null);Assert.That(paths.GeneratedRoot,Is.Null);
                Assert.That(terrain.terrainData,Is.SameAs(original));Assert.That(terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(original));
                Assert.That(query.TryGetSurfaceHeight(expected.x,expected.z,out var retained),Is.True);Assert.That(retained,Is.EqualTo(y));
            }
            finally { Restore(previous); }
        }
        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
        private static void Restore(SceneSetup[] previous)
        {
            if(previous.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
    }
}
