using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Core.Unity;
using TerraLoom.Integration;
using TerraLoom.Integration.Editor;
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

        [Test] public void CentralBakeKeepsCoreOwnedWorldAndDecorationCurrent()
        {
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/TerraLoomDynamic.unity");
                var demo=Object.FindFirstObjectByType<SeededWorldDemo>();Assert.That(demo,Is.Not.Null);
                Assert.That(demo.Rebuild(),Is.True,demo.Diagnostics);
                var composition=demo.Composition;Assert.That(composition.World.OwnsTerrain,Is.True);
                Assert.That(demo.Decoration.GeneratedRoot,Is.Not.Null);
                EnsureFolder(Folder);string run=Folder+"/Owned-"+Guid.NewGuid().ToString("N");EnsureFolder(run);
                var decorationRoot=demo.Decoration.GeneratedRoot;
                AssertBakeFailureRetainsPublication(composition,run+"/Failed","bindings-swapped");
                Assert.That(demo.Decoration.GeneratedRoot,Is.SameAs(decorationRoot));
                IntegrationEditor.BakeGeneratedForScene(composition,run+"/Baked");
                Assert.That(composition.ValidateCurrent(out var reason),Is.True,reason);
                Assert.That(composition.World.OwnsTerrain,Is.True);
                Assert.That(demo.Decoration.GeneratedRoot,Is.SameAs(decorationRoot));
                foreach(var filter in decorationRoot.GetComponentsInChildren<MeshFilter>(true))
                    Assert.That(AssetDatabase.Contains(filter.sharedMesh),Is.True);
            }
            finally { Restore(previous); }
        }

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
                var sourceLayer=new TerrainLayer();AssetDatabase.CreateAsset(sourceLayer,run+"/SourceLayer.asset");
                source.terrainLayers=new[]{sourceLayer};
                source.alphamapResolution=32;
                var alpha=new float[32,32,1];for(int z=0;z<32;z++)for(int x=0;x<32;x++)alpha[z,x,0]=1;
                source.SetAlphamaps(0,0,alpha);
                var detailTexture=new Texture2D(2,2);AssetDatabase.CreateAsset(detailTexture,run+"/Detail.asset");
                source.detailPrototypes=new[]{new DetailPrototype{prototypeTexture=detailTexture,renderMode=DetailRenderMode.GrassBillboard}};
                source.SetDetailResolution(32,8);
                var detail=new int[32,32];for(int z=0;z<32;z++)for(int x=0;x<32;x++)detail[z,x]=3;
                source.SetDetailLayer(0,0,0,detail);
                var treeObject=GameObject.CreatePrimitive(PrimitiveType.Cube);
                treeObject.transform.localScale=new Vector3(.5f,4,.5f);
                var canopy=GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(canopy.GetComponent<Collider>());
                canopy.transform.SetParent(treeObject.transform,false);canopy.transform.localPosition=new Vector3(0,1,0);
                canopy.transform.localScale=new Vector3(40,1,40);
                var body=treeObject.AddComponent<LandscapeAssetShape>();body.UseExplicitBody=true;body.Role=LandscapeAssetRole.Root;
                body.PhysicalBody=new Bounds(Vector3.zero,Vector3.one);
                body.VisualEnvelope=new Bounds(new Vector3(0,.75f,0),new Vector3(40,2.5f,40));
                var treePrefab=PrefabUtility.SaveAsPrefabAsset(treeObject,run+"/Tree.prefab");Object.DestroyImmediate(treeObject);
                source.treePrototypes=new[]{new TreePrototype{prefab=treePrefab}};
                source.treeInstances=new[]{new TreeInstance{prototypeIndex=0,position=new Vector3(.6f,.2f,.5f),widthScale=1,heightScale=1,color=Color.white,lightmapColor=Color.white}};
                var trees=source.treeInstances;
                var original=source.GetHeights(0,0,129,129);
                var terrain=Terrain.CreateTerrainGameObject(source).GetComponent<Terrain>();
                var host=new GameObject("Supplied terrain composition roundtrip");var world=host.AddComponent<TerraLoomWorld>();
                world.WorldId="foreign-composition-query";world.Seed=42;world.Terrain=terrain;world.UseSeedAnchors=false;
                world.ManualAnchors=new[]{
                    new ManualWorldAnchor{Id="source",Position=new Vector3(48,0,4)},new ManualWorldAnchor{Id="mouth",Position=new Vector3(48,0,92)},
                    new ManualWorldAnchor{Id="west",Position=new Vector3(24,0,57)},new ManualWorldAnchor{Id="east",Position=new Vector3(72,0,57)}};
                var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                texture.SetPixels(Enumerable.Repeat(new Color(.2f,.4f,.6f,1),4).ToArray());texture.Apply();
                material.SetTexture("_BaseMap",texture);material.SetFloat("_Smoothness",.17f);
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
                var recipe=ScriptableObject.CreateInstance<CompositionRecipe>();
                try
                {
                    recipe.CaptureFrom(composition,null);
                    Assert.That(recipe.SchemaVersion,Is.EqualTo(3));
                    Assert.That(recipe.Settings.TerrainSource,Is.EqualTo(CompositionTerrainSource.Supplied));
                    Assert.That(recipe.Settings.DecorationCount,Is.Zero);
                    var reviewedBody=treePrefab.GetComponent<LandscapeAssetShape>();
                    reviewedBody.UseExplicitBody=false;
                    Assert.That(composition.GenerateRecipe(recipe,null),Is.True,composition.Diagnostics);
                    Assert.That(rivers.CarvedTerrainData.treeInstanceCount,Is.Zero,"The legacy crown envelope must reproduce the over-clearance.");
                    reviewedBody.UseExplicitBody=true;EditorUtility.SetDirty(reviewedBody);
                    Assert.That(composition.GenerateRecipe(recipe,null),Is.True,composition.Diagnostics);
                    Assert.That(rivers.CarvedTerrainData.treeInstanceCount,Is.EqualTo(1),"A clear physical trunk must survive its overhanging crown.");
                    foreach(var boundary in new[]{"river-prepared","before-commit","river-bound","all-bound"})
                    {
                        var oldData=terrain.terrainData;var oldQuery=rivers.WaterSurfaceQuery;
                        var oldRivers=rivers.GeneratedRoot;var oldPaths=paths.GeneratedRoot;
                        var oldAnchors=world.ManualAnchors;var oldAreas=world.Areas;
                        var oldPathConnections=paths.Connections;var oldRiverConnections=rivers.Connections;
                        composition.PublicationBoundary=stage=>{if(stage==boundary)throw new InvalidOperationException("Supplied recipe rollback "+boundary);};
                        Assert.That(composition.GenerateRecipe(recipe,null),Is.False);
                        composition.PublicationBoundary=null;
                        StringAssert.Contains("Supplied recipe rollback",composition.Diagnostics);
                        Assert.That(terrain.terrainData,Is.SameAs(oldData));
                        Assert.That(terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(oldData));
                        Assert.That(rivers.WaterSurfaceQuery,Is.SameAs(oldQuery));
                        Assert.That(rivers.GeneratedRoot,Is.SameAs(oldRivers));Assert.That(paths.GeneratedRoot,Is.SameAs(oldPaths));
                        Assert.That(world.ManualAnchors,Is.SameAs(oldAnchors));Assert.That(world.Areas,Is.SameAs(oldAreas));
                        Assert.That(paths.Connections,Is.SameAs(oldPathConnections));Assert.That(rivers.Connections,Is.SameAs(oldRiverConnections));
                        Assert.That(composition.ValidateCurrent(out var retained),Is.True,retained);
                    }
                    using(var cancelled=new System.Threading.CancellationTokenSource())
                    {
                        cancelled.Cancel();
                        Assert.That(composition.GenerateRecipe(recipe,null,cancelled.Token),Is.False);
                    }
                    // Recipe source mode is explicit; legacy schema 2 may not silently reinterpret supplied terrain.
                    recipe.SchemaVersion=2;
                    Assert.That(composition.GenerateRecipe(recipe,null),Is.False);
                    recipe.SchemaVersion=3;
                    Assert.That(composition.GenerateRecipe(recipe,null),Is.True,composition.Diagnostics);
                    Assert.That(world.OwnsTerrain,Is.False);
                }
                finally { composition.PublicationBoundary=null;Object.DestroyImmediate(recipe); }
                Assert.That(composition.ValidateCurrent(out var reason),Is.True,reason);
                Assert.That(source.GetHeights(0,0,129,129),Is.EqualTo(original));
                Assert.That(source.GetAlphamaps(0,0,32,32),Is.EqualTo(alpha));
                Assert.That(source.GetDetailLayer(0,0,32,32,0),Is.EqualTo(detail));
                Assert.That(source.treeInstances,Is.EqualTo(trees));
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
                foreach(string pointName in new[]{"assets-staged","bindings-swapped","before-complete"})
                    AssertBakeFailureRetainsPublication(composition,run+"/Fail-"+pointName,pointName);
                var unsupported=new RenderTexture(2,2,0);var beforeUnsupported=terrain.terrainData;
                try
                {
                    material.SetTexture("_BaseMap",unsupported);
                    Assert.Throws<InvalidOperationException>(()=>IntegrationEditor.BakeGeneratedForScene(composition,run+"/UnsupportedTexture"));
                    Assert.That(terrain.terrainData,Is.SameAs(beforeUnsupported));
                    Assert.That(rivers.WaterMaterial,Is.SameAs(material));
                    Assert.That(Directory.Exists(run+"/UnsupportedTexture"),Is.False);
                }
                finally { material.SetTexture("_BaseMap",texture);Object.DestroyImmediate(unsupported); }
                string occupied=run+"/Occupied";EnsureFolder(occupied);File.WriteAllText(occupied+"/Keep.txt","user file");
                Assert.Throws<InvalidOperationException>(()=>IntegrationEditor.BakeGeneratedForScene(composition,occupied));
                Assert.That(File.ReadAllText(occupied+"/Keep.txt"),Is.EqualTo("user file"));
                Assert.That(composition.ValidateCurrent(out var untouched),Is.True,untouched);
                IntegrationEditor.BakeGeneratedForScene(composition,run+"/Baked");
                Assert.That(AssetDatabase.Contains(rivers.WaterMaterial),Is.True);
                Assert.That(rivers.WaterMaterial,Is.SameAs(paths.GroundMaterial));
                Assert.That(rivers.WaterMaterial,Is.SameAs(paths.BridgeMaterial));
                Assert.That(AssetDatabase.Contains(rivers.WaterMaterial.GetTexture("_BaseMap")),Is.True);
                Assert.That(composition.ValidateCurrent(out var baked),Is.True,baked);
                File.WriteAllText(Folder+"/Expected.json",JsonUtility.ToJson(expected));
                var savedRecipe=IntegrationEditor.CaptureAndSaveRecipe(composition,null,run+"/SuppliedRecipe.asset");
                Assert.That(savedRecipe.Settings.TerrainSource,Is.EqualTo(CompositionTerrainSource.Supplied));
                IntegrationEditor.GenerateSavedRecipe(composition);
                Assert.That(composition.ValidateCurrent(out var recipeReason),Is.True,recipeReason);
                IntegrationEditor.BakeGeneratedForScene(composition,run+"/RecipeBaked");
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
                Assert.That(composition.RecipeAsset,Is.Not.Null);
                Assert.That(composition.RecipeAsset.SchemaVersion,Is.EqualTo(3));
                Assert.That(composition.RecipeAsset.Settings.TerrainSource,Is.EqualTo(CompositionTerrainSource.Supplied));
                Assert.That(composition.RecipeDecoration,Is.Null);
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
                Assert.That(AssetDatabase.Contains(rivers.WaterMaterial),Is.True);
                Assert.That(rivers.WaterMaterial,Is.SameAs(paths.GroundMaterial));
                Assert.That(rivers.WaterMaterial,Is.SameAs(paths.BridgeMaterial));
                Assert.That(rivers.WaterMaterial.GetFloat("_Smoothness"),Is.EqualTo(.17f).Within(.00001));
                var texture=rivers.WaterMaterial.GetTexture("_BaseMap") as Texture2D;
                Assert.That(texture,Is.Not.Null);Assert.That(AssetDatabase.Contains(texture),Is.True);
                Assert.That(texture.GetPixel(0,0).g,Is.EqualTo(.4f).Within(.001));
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
        private static void AssertBakeFailureRetainsPublication(TerraLoomIntegration composition,string directory,string point)
        {
            var rivers=composition.Rivers;var paths=composition.Paths;var terrain=composition.World.Terrain;
            var data=terrain.terrainData;var query=rivers.WaterSurfaceQuery;var material=rivers.WaterMaterial;
            var ground=paths.GroundMaterial;var bridge=paths.BridgeMaterial;
            var riverRoot=rivers.GeneratedRoot;var pathRoot=paths.GeneratedRoot;
            var filters=riverRoot.GetComponentsInChildren<MeshFilter>(true).Concat(pathRoot.GetComponentsInChildren<MeshFilter>(true)).ToArray();
            var meshes=filters.Select(f=>f.sharedMesh).ToArray();
            var fault=typeof(CompositionSceneBaker).GetField("InjectFailureAt",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            try
            {
                fault.SetValue(null,point);
                Assert.Throws<IOException>(()=>IntegrationEditor.BakeGeneratedForScene(composition,directory));
            }
            finally { fault.SetValue(null,null); }
            Assert.That(terrain.terrainData,Is.SameAs(data));Assert.That(terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(data));
            Assert.That(rivers.CarvedTerrainData,Is.SameAs(data));Assert.That(rivers.WaterSurfaceQuery,Is.SameAs(query));
            Assert.That(rivers.WaterMaterial,Is.SameAs(material));Assert.That(paths.GroundMaterial,Is.SameAs(ground));
            Assert.That(paths.BridgeMaterial,Is.SameAs(bridge));Assert.That(rivers.GeneratedRoot,Is.SameAs(riverRoot));
            Assert.That(paths.GeneratedRoot,Is.SameAs(pathRoot));
            for(int i=0;i<filters.Length;i++){Assert.That(meshes[i],Is.Not.Null);Assert.That(filters[i].sharedMesh,Is.SameAs(meshes[i]));}
            Assert.That(new SerializedObject(rivers).FindProperty("savedWaterQuery.terrainData").objectReferenceValue,Is.SameAs(data));
            Assert.That(Directory.Exists(directory),Is.False,"Failed bake left partial assets behind.");
            Assert.That(composition.ValidateCurrent(out var reason),Is.True,reason);
        }
        private static void Restore(SceneSetup[] previous)
        {
            if(previous.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
    }
}
