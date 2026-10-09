using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Core.Unity;
using TerraLoom.Rivers;
using TerraLoom.Rivers.Unity;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TerraLoom.Tests
{
    public sealed class RiverWorldTests
    {
        private GameObject host, terrainObject;
        private TerrainData original;
        private Material water, bed, bank;
        private TerraLoomRivers rivers;
        [SetUp] public void Setup()
        {
            original=new TerrainData { heightmapResolution=65,size=new Vector3(64,16,64) };
            var heights=new float[65,65]; for(int z=0;z<65;z++) for(int x=0;x<65;x++) heights[z,x]=.3f-z*.001f;
            original.SetHeights(0,0,heights); terrainObject=Terrain.CreateTerrainGameObject(original);
            host=new GameObject("Rivers test"); var world=host.AddComponent<TerraLoomWorld>(); world.Terrain=terrainObject.GetComponent<Terrain>();
            world.ManualAnchors=new[] { new ManualWorldAnchor { Id="source",Position=new Vector3(32,0,8) }, new ManualWorldAnchor { Id="mouth",Position=new Vector3(32,0,56) } };
            world.Areas=new[] { new LandscapeArea { Id="wet",Center=new Vector2(32,32),Size=new Vector2(32,64) } };
            water=new Material(Shader.Find("TerraLoom/RiverWater")); bed=new Material(Shader.Find("Universal Render Pipeline/Lit")); bank=new Material(bed);
            rivers=host.AddComponent<TerraLoomRivers>(); rivers.World=world; rivers.WaterMaterial=water; rivers.BedMaterial=bed; rivers.BankMaterial=bank; rivers.CellSize=4;
        }
        [TearDown] public void Teardown()
        { Object.DestroyImmediate(host); Object.DestroyImmediate(terrainObject); Object.DestroyImmediate(original); Object.DestroyImmediate(water); Object.DestroyImmediate(bed); Object.DestroyImmediate(bank); }

        [Test] public void GenerationCarvesOnlyAnOwnedCopyAndClearRestoresOriginal()
        {
            var before=original.GetHeights(0,0,65,65);
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
            Assert.That(rivers.CarvedTerrainData,Is.Not.Null);
            Assert.That(rivers.CarvedTerrainData,Is.Not.SameAs(original));
            Assert.That(original.GetHeights(0,0,65,65),Is.EqualTo(before));
            Assert.That(rivers.World.Terrain.terrainData.GetHeight(32,32),Is.LessThan(original.GetHeight(32,32)-.3f));
            var root=rivers.GeneratedRoot; var data=rivers.CarvedTerrainData;
            rivers.Clear(); Assert.That(root==null,Is.True); Assert.That(data==null,Is.True);
            Assert.That(rivers.World.Terrain.terrainData,Is.SameAs(original));
            Assert.That(terrainObject.GetComponent<TerrainCollider>().terrainData,Is.SameAs(original));
        }
        [Test] public void ExcavationFollowsFinalMeshFacesAndDoesNotCutBeyondRouteEndCaps()
        {
            var before=original.GetHeights(0,0,65,65);
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
            Physics.SyncTransforms();
            var colliders=rivers.GeneratedRoot.GetComponentsInChildren<RiverGeneratedGeometry>()
                .Where(m=>m.Role!=RiverGeometryRole.Water).Select(m=>m.GetComponent<MeshCollider>()).ToArray();
            int checkedSamples=0;
            for(int z=9;z<56;z++)for(int x=29;x<36;x++)
            {
                double surface=double.PositiveInfinity;
                foreach(var collider in colliders)
                    if(collider.Raycast(new Ray(new Vector3(x,20,z),Vector3.down),out var hit,40))surface=Math.Min(surface,hit.point.y);
                if(double.IsInfinity(surface))continue;
                double expected=Math.Min(before[z,x]*16,surface-.02);
                // Unity's native height storage quantizes normalized heights. Bound by one
                // terrain-height quantum, not by an arbitrary visual acceptance margin.
                Assert.That(rivers.CarvedTerrainData.GetHeight(x,z),Is.EqualTo(expected).Within(16.0/32767+.000001),$"grid {x},{z}");
                Assert.That(rivers.CarvedTerrainData.GetHeight(x,z),Is.LessThan(surface-.019));
                checkedSamples++;
            }
            Assert.That(checkedSamples,Is.GreaterThan(200));
            Assert.That(rivers.CarvedTerrainData.GetHeight(32,7),Is.EqualTo(original.GetHeight(32,7)),"No invented round source cap.");
            Assert.That(rivers.CarvedTerrainData.GetHeight(32,57),Is.EqualTo(original.GetHeight(32,57)),"No invented round mouth cap.");
            Assert.That(original.GetHeights(0,0,65,65),Is.EqualTo(before));
        }

        [Test] public void ActualTerrainEarthworkLimitRejectsTransactionWithoutModifyingItsSource()
        {
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
            var previousRoot=rivers.GeneratedRoot;var previousTerrain=rivers.CarvedTerrainData;
            var mismatched=Object.Instantiate(original);
            try
            {
                var heights=mismatched.GetHeights(0,0,65,65);heights[32,32]=.95f;mismatched.SetHeights(0,0,heights);
                var before=mismatched.GetHeights(0,0,65,65);
                Assert.That(Assert.Throws<InvalidOperationException>(()=>RiverTerrainCarver.Build(mismatched,Vector3.zero,
                    rivers.LastPlan,rivers.CaptureProfile(),CancellationToken.None)).Message,Does.Contain("excavation exceeds"));
                Assert.That(mismatched.GetHeights(0,0,65,65),Is.EqualTo(before));
                Assert.That(rivers.GeneratedRoot,Is.SameAs(previousRoot));Assert.That(rivers.CarvedTerrainData,Is.SameAs(previousTerrain));
            }
            finally {Object.DestroyImmediate(mismatched);}
        }

        [Test] public void SavedPreviewUsesCapturedBanksAndRejectsLegacyRecipeWithoutReplacingWorld()
        {
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
            string json=rivers.PlanJson;var root=rivers.GeneratedRoot;var plan=rivers.LastPlan;
            Assert.That(json,Does.Contain("\"format\": 2").And.Contain("\"leftBank\"").And.Contain("\"rightBank\""));
            var extension=new TerraLoom.Rivers.Editor.RiversWorkbenchExtension();
            var method=extension.GetType().GetMethod("GetPreview",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var preview=method.Invoke(extension,new object[]{rivers});
            var routes=(Array)preview.GetType().GetField("Routes").GetValue(preview);
            Assert.That(routes.Length,Is.EqualTo(1));var route=routes.GetValue(0);
            var left=(Vector3[])route.GetType().GetField("Left").GetValue(route);
            var right=(Vector3[])route.GetType().GetField("Right").GetValue(route);
            for(int i=0;i<left.Length;i++)
            {
                var l=plan.Routes.Single().LeftBankPolyline[i];var r=plan.Routes.Single().RightBankPolyline[i];
                Assert.That(left[i],Is.EqualTo(new Vector3((float)l.X,(float)l.Y,(float)l.Z)));
                Assert.That(right[i],Is.EqualTo(new Vector3((float)r.X,(float)r.Y,(float)r.Z)));
            }
            Assert.That(method.Invoke(extension,new object[]{rivers}),Is.SameAs(preview));
            Assert.That(rivers.LoadPlanJson(json.Replace("\"format\": 2","\"format\": 1")),Is.False);
            Assert.That(rivers.GeneratedRoot,Is.SameAs(root));Assert.That(rivers.LastPlan,Is.SameAs(plan));
        }

        [Test] public void CancelFailureAndBadJsonRetainPreviousResult()
        {
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics); var root=rivers.GeneratedRoot; var data=rivers.CarvedTerrainData;
            Assert.That(rivers.Generate(new CancellationToken(true)),Is.False); Assert.That(rivers.GeneratedRoot,Is.SameAs(root));
            string saved=rivers.PlanJson; Assert.That(rivers.LoadPlanJson(saved),Is.True,rivers.Diagnostics);
            root=rivers.GeneratedRoot; data=rivers.CarvedTerrainData;
            rivers.World.Seed++;
            Assert.That(rivers.LoadPlanJson(saved),Is.False); Assert.That(rivers.GeneratedRoot,Is.SameAs(root));
            rivers.ProtectedAreas.Add(new RiverProtectionSettings { Id="wall",Center=new Vector2(32,32),Size=new Vector2(64,8) });
            Assert.That(rivers.Generate(),Is.False); Assert.That(rivers.GeneratedRoot,Is.SameAs(root));
            Assert.That(rivers.CarvedTerrainData,Is.SameAs(data)); Assert.That(rivers.Diagnostics,Does.Contain("NoRoute"));
        }
        [Test] public void WaterWindingUvColliderAndShadowRolesAreExplicit()
        {
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
            var parts=rivers.GeneratedRoot.GetComponentsInChildren<RiverGeneratedGeometry>(); Assert.That(parts.Length,Is.EqualTo(3));
            foreach(var marker in parts)
            {
                var mesh=marker.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.normals.All(n=>n.y>0),Is.True,marker.name);
                Assert.That(mesh.uv.Length,Is.EqualTo(mesh.vertexCount)); Assert.That(mesh.uv2,Is.EqualTo(mesh.uv));
                Assert.That(mesh.colors.All(c=>c==Color.white),Is.True);
                Assert.That(marker.GetComponent<MeshRenderer>().shadowCastingMode,Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(marker.GetComponent<MeshRenderer>().sharedMaterial,Is.Not.Null);
                Assert.That(marker.GetComponent<MeshCollider>()!=null,Is.EqualTo(marker.Role!=RiverGeometryRole.Water));
            }
            var route=rivers.LastPlan.Routes.Single();
            for(int i=1;i<route.WaterPolyline.Count;i++) Assert.That(route.WaterPolyline[i].Y,Is.LessThanOrEqualTo(route.WaterPolyline[i-1].Y+1e-8));
            AssertMeshesMatchPlan();
        }
        [Test] public void InsetWaterHasDryBanksAndClearanceAgainstActualCarvedTerrain()
        {
            var before=original.GetHeights(0,0,65,65); rivers.WaterInset=.3f;
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
            var route=rivers.LastPlan.Routes.Single();
            Assert.That(route.LeftBankPolyline[0].Y-route.WaterPolyline[0].Y,Is.GreaterThan(.25));
            var waterFilter=rivers.GeneratedRoot.GetComponentsInChildren<RiverGeneratedGeometry>()
                .Single(g=>g.Role==RiverGeometryRole.Water).GetComponent<MeshFilter>();
            var mesh=waterFilter.sharedMesh; var vertices=mesh.vertices;
            foreach(var v in vertices)
            {
                var p=waterFilter.transform.TransformPoint(v);
                float terrainHeight=rivers.World.Terrain.SampleHeight(p)+rivers.World.Terrain.transform.position.y;
                Assert.That(terrainHeight,Is.LessThanOrEqualTo(p.y+.00001f),"Terrain pierces a water vertex at "+p);
            }
            var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3)
            {
                var p=waterFilter.transform.TransformPoint((vertices[indices[i]]+vertices[indices[i+1]]+vertices[indices[i+2]])/3);
                float terrainHeight=rivers.World.Terrain.SampleHeight(p)+rivers.World.Terrain.transform.position.y;
                Assert.That(terrainHeight,Is.LessThan(p.y),"No actual wet depth at triangle centre "+p);
            }
            Assert.That(original.GetHeights(0,0,65,65),Is.EqualTo(before));
            rivers.Clear(); Assert.That(rivers.World.Terrain.terrainData,Is.SameAs(original));
        }

        [Test] public void ActualTerrainInspectionReportsPenetrationAndCancellation()
        {
            rivers.WaterInset=.3f;
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
            var terrain=rivers.World.Terrain;
            var good=RiverWaterTerrainInspection.Inspect(rivers.GeneratedRoot,terrain.terrainData,terrain.transform.position);
            Assert.That(good.Acceptable,Is.True,good.ToString());
            Assert.That(good.Vertices,Is.GreaterThan(0));Assert.That(good.Centres,Is.GreaterThan(0));
            // Source terrain has not been excavated; testing the planned bed would miss this failure.
            var bad=RiverWaterTerrainInspection.Inspect(rivers.GeneratedRoot,original,terrain.transform.position);
            Assert.That(bad.Acceptable,Is.False);
            Assert.That(bad.MinimumVertexClearance,Is.LessThan(-.2));
            Assert.Throws<OperationCanceledException>(()=>RiverWaterTerrainInspection.Inspect(
                rivers.GeneratedRoot,terrain.terrainData,terrain.transform.position,new CancellationToken(true)));
        }

        [TestCase(false)] [TestCase(true)] public void CurvedSampleInsetClipsWaterAgainstActualTerrain(bool brush)
        {
            rivers.Clear();original.heightmapResolution=129;original.size=new Vector3(96,16,96);
            var heights=new float[129,129];
            for(int z=0;z<129;z++)for(int x=0;x<129;x++)heights[z,x]=(6f-.03f*(96f*z/128f))/16f;
            original.SetHeights(0,0,heights);
            rivers.World.ManualAnchors=new[]{
                new ManualWorldAnchor{Id="source",Position=new Vector3(48,0,8)},
                new ManualWorldAnchor{Id="mouth",Position=new Vector3(48,0,88)}};
            rivers.CellSize=2;
            rivers.ProtectedAreas.Add(new RiverProtectionSettings{Id="sample-detour",Center=new Vector2(48,48),Size=new Vector2(8,12)});
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
            rivers.WaterInset=.3f;rivers.TerrainBrush=brush;
            if(brush){rivers.BedMaterial=null;rivers.BankMaterial=null;}
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
            var result=RiverWaterTerrainInspection.Inspect(rivers.GeneratedRoot,rivers.CarvedTerrainData,Vector3.zero);
            Assert.That(result.Acceptable,Is.True,result.ToString());
            var filter=rivers.GeneratedRoot.GetComponentsInChildren<RiverGeneratedGeometry>()
                .Single(m=>m.Role==RiverGeometryRole.Water).GetComponent<MeshFilter>();
            Assert.That(filter.sharedMesh.vertexCount,Is.GreaterThan(rivers.LastPlan.Routes.Single().WaterPolyline.Count*2));
            var points=filter.sharedMesh.vertices;var uv=filter.sharedMesh.uv;string json=rivers.PlanJson;
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
            filter=rivers.GeneratedRoot.GetComponentsInChildren<RiverGeneratedGeometry>()
                .Single(m=>m.Role==RiverGeometryRole.Water).GetComponent<MeshFilter>();
            Assert.That(filter.sharedMesh.vertices,Is.EqualTo(points));Assert.That(filter.sharedMesh.uv,Is.EqualTo(uv));
            Assert.That(rivers.PlanJson,Is.EqualTo(json));
            Assert.That(original.GetHeights(0,0,129,129),Is.EqualTo(heights).Within(.00004f));
            var root=rivers.GeneratedRoot;var data=rivers.CarvedTerrainData;
            rivers.MaximumGeometryVertices=3;
            Assert.That(rivers.Generate(),Is.False);Assert.That(rivers.GeneratedRoot,Is.SameAs(root));
            Assert.That(rivers.World.Terrain.terrainData,Is.SameAs(data));Assert.That(rivers.PlanJson,Is.EqualTo(json));
            Assert.That(terrainObject.GetComponent<TerrainCollider>().terrainData,Is.SameAs(data));
        }

        [Test] public void WaterClipperKeepsLevelUvWidthAndInputsUnderWorldTranslation()
        {
            var data=new TerrainData{heightmapResolution=33,size=new Vector3(10,10,10)};
            var heights=new float[33,33];for(int z=0;z<33;z++)for(int x=0;x<33;x++)heights[z,x]=x/32f*.2f;
            data.SetHeights(0,0,heights);
            var mesh=new Mesh();Mesh clipped=null;
            try
            {
                mesh.vertices=new[]{new Vector3(2,1,2),new Vector3(2,1,8),new Vector3(8,1,2),new Vector3(8,1,8)};
                mesh.uv=new[]{new Vector2(-3,0),new Vector2(-3,6),new Vector2(3,0),new Vector2(3,6)};
                mesh.triangles=new[]{0,1,2,2,1,3};var before=mesh.vertices;
                var origin=new Vector3(100,3,200);var matrix=Matrix4x4.Translate(origin);
                clipped=RiverWaterTerrainClipper.Build(mesh,matrix,data,origin,100000);
                Assert.That(clipped.vertices.All(p=>p.y==1 && p.x>=2 && p.x<5),Is.True,"Keep level, clip wet side only.");
                Assert.That(clipped.normals.All(n=>n.y>.99f),Is.True);
                var widths=new System.Collections.Generic.List<Vector2>();clipped.GetUVs(2,widths);
                Assert.That(widths.Count,Is.EqualTo(clipped.vertexCount));
                Assert.That(widths.All(p=>Math.Abs(p.x-3)<1e-5),Is.True,"No false interior foam after subdivision.");
                Assert.That(clipped.uv2,Is.EqualTo(clipped.uv));Assert.That(mesh.vertices,Is.EqualTo(before));
                Assert.That(data.GetHeights(0,0,33,33),Is.EqualTo(heights).Within(.00004f));
                Assert.Throws<InvalidOperationException>(()=>RiverWaterTerrainClipper.Build(mesh,matrix,data,origin,3));
                Assert.Throws<OperationCanceledException>(()=>RiverWaterTerrainClipper.Build(mesh,matrix,data,origin,100000,new CancellationToken(true)));
            }
            finally{if(clipped)Object.DestroyImmediate(clipped);Object.DestroyImmediate(mesh);Object.DestroyImmediate(data);}
        }

        [TestCase(false)] [TestCase(true)] public void SeededInsetUsesSameTerrainClippingForThreeSeeds(bool brush)
        {
            rivers.World.UseSeedAnchors=true;rivers.World.SeedAnchorCount=5;rivers.World.AnchorMargin=10;
            rivers.WaterInset=.3f;rivers.TerrainBrush=brush;
            foreach(int seed in new[]{42,43,71})
            {
                rivers.World.Seed=seed;
                Assert.That(rivers.Generate(),Is.True,"Seed "+seed+": "+rivers.Diagnostics);
                var check=RiverWaterTerrainInspection.Inspect(rivers.GeneratedRoot,rivers.CarvedTerrainData,Vector3.zero);
                Assert.That(check.Acceptable,Is.True,"Seed "+seed+": "+check);
                string json=rivers.PlanJson;
                Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);Assert.That(rivers.PlanJson,Is.EqualTo(json));
            }
        }

        [Test] public void TerrainBrushFormsSmoothChannelWithoutBedOrBankMeshes()
        {
            rivers.TerrainBrush=true;rivers.WaterInset=.3f;rivers.BedMaterial=null;rivers.BankMaterial=null;
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
            var parts=rivers.GeneratedRoot.GetComponentsInChildren<RiverGeneratedGeometry>();
            Assert.That(parts.Length,Is.EqualTo(1));Assert.That(parts[0].Role,Is.EqualTo(RiverGeometryRole.Water));
            Assert.That(rivers.GeneratedRoot.GetComponentsInChildren<MeshCollider>(),Is.Empty);
            var data=rivers.CarvedTerrainData;var before=original.GetHeights(0,0,65,65);
            double full=original.GetHeight(32,32)-data.GetHeight(32,32);
            double shoulder=original.GetHeight(29,32)-data.GetHeight(29,32);
            Assert.That(full,Is.EqualTo(.9).Within(.001));Assert.That(shoulder,Is.EqualTo(.45).Within(.001));
            Assert.That(data.GetHeight(28,32),Is.EqualTo(original.GetHeight(28,32)));
            var route=rivers.LastPlan.Routes.Single();
            for(int z=0;z<65;z++)for(int x=0;x<65;x++)
            {
                bool inside=false;
                for(int span=0;span<route.WaterPolyline.Count-1;span++)
                    if(RiverChannelSurface.TryHeight(route,span,rivers.Width,rivers.BankWidth,x,z,out _)){inside=true;break;}
                if(!inside)Assert.That(data.GetHeight(x,z),Is.EqualTo(original.GetHeight(x,z)),"Brush left authorized face at "+x+","+z);
                Assert.That(data.GetHeight(x,z),Is.LessThanOrEqualTo(original.GetHeight(x,z)));
            }
            Assert.That(terrainObject.GetComponent<TerrainCollider>().terrainData,Is.SameAs(data));
            Assert.That(original.GetHeights(0,0,65,65),Is.EqualTo(before));
            // A source-cap neighbour is outside the face, but is influenced by an excavated node.
            Assert.Throws<InvalidOperationException>(()=>RiverTerrainCarver.Build(original,Vector3.zero,
                rivers.LastPlan,rivers.CaptureProfile(),default,new[]{new AreaReservation("source-support","test",
                new WorldBounds(31,7.25,33,7.75),ReservationStrength.Hard,ReservationPurpose.ProtectedArea)}));
            var root=rivers.GeneratedRoot;rivers.CarveTerrainCopy=false;
            Assert.That(rivers.Generate(),Is.False);Assert.That(rivers.GeneratedRoot,Is.SameAs(root));
            rivers.Clear();Assert.That(rivers.World.Terrain.terrainData,Is.SameAs(original));
        }

        [Test] public void SedimentPaintPreservesSourceLayerRatiosAndFailureOwnership()
        {
            var layers=new[]{new TerrainLayer{name="Grass"},new TerrainLayer{name="Rock"},new TerrainLayer{name="Sediment"}};
            try
            {
                original.terrainLayers=layers;original.alphamapResolution=32;
                var weights=new float[32,32,3];for(int z=0;z<32;z++)for(int x=0;x<32;x++){weights[z,x,0]=.6f;weights[z,x,1]=.4f;}
                original.SetAlphamaps(0,0,weights);var before=original.GetAlphamaps(0,0,32,32);
                rivers.TerrainBrush=true;rivers.WaterInset=.3f;rivers.SedimentTerrainLayer=2;
                Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
                var painted=rivers.CarvedTerrainData.GetAlphamaps(0,0,32,32);int mixed=0,soil=0;
                for(int z=0;z<32;z++)for(int x=0;x<32;x++)
                {
                    Assert.That(painted[z,x,0]+painted[z,x,1]+painted[z,x,2],Is.EqualTo(1).Within(.008));
                    if(painted[z,x,2]>.01f)soil++;
                    if(painted[z,x,2]>.1f&&painted[z,x,2]<.8f)
                    {Assert.That(painted[z,x,0]/painted[z,x,1],Is.EqualTo(1.5f).Within(.06));mixed++;}
                }
                Assert.That(soil,Is.GreaterThan(0));Assert.That(mixed,Is.GreaterThan(0));
                Assert.That(painted[0,0,0],Is.EqualTo(before[0,0,0]));
                Assert.That(original.GetAlphamaps(0,0,32,32),Is.EqualTo(before));
                var root=rivers.GeneratedRoot;var data=rivers.CarvedTerrainData;string json=rivers.PlanJson;
                rivers.SedimentTerrainLayer=3;Assert.That(rivers.Generate(),Is.False);
                Assert.That(rivers.GeneratedRoot,Is.SameAs(root));Assert.That(rivers.CarvedTerrainData,Is.SameAs(data));Assert.That(rivers.PlanJson,Is.EqualTo(json));
                rivers.SedimentTerrainLayer=2;
                Assert.That(rivers.CaptureProfile().TerrainCellGuard,Is.GreaterThanOrEqualTo(64.0/31));
                Assert.Throws<InvalidOperationException>(()=>RiverTerrainPainter.Apply(data,original,Vector3.zero,
                    rivers.LastPlan,rivers.CaptureProfile(),2,new[]{new AreaReservation("texture-only-guard","test",
                    new WorldBounds(27.25,30,27.75,34),ReservationStrength.Hard,ReservationPurpose.ProtectedArea)}));
                Assert.That(data.GetAlphamaps(0,0,32,32),Is.EqualTo(painted));
                Assert.Throws<OperationCanceledException>(()=>RiverTerrainPainter.Apply(data,original,Vector3.zero,
                    rivers.LastPlan,rivers.CaptureProfile(),2,Array.Empty<AreaReservation>(),new CancellationToken(true)));
                Assert.That(data.GetAlphamaps(0,0,32,32),Is.EqualTo(painted));
                rivers.World.UseSeedAnchors=true;rivers.World.SeedAnchorCount=5;rivers.World.AnchorMargin=10;rivers.World.Seed=43;
                Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);painted=rivers.CarvedTerrainData.GetAlphamaps(0,0,32,32);
                Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
                Assert.That(rivers.CarvedTerrainData.GetAlphamaps(0,0,32,32),Is.EqualTo(painted));
                rivers.Clear();Assert.That(rivers.World.Terrain.terrainData,Is.SameAs(original));
                Assert.That(original.GetAlphamaps(0,0,32,32),Is.EqualTo(before));
            }
            finally{rivers.Clear();original.terrainLayers=Array.Empty<TerrainLayer>();foreach(var layer in layers)Object.DestroyImmediate(layer);}
        }

        [Test] public void InvalidInsetRetainsPublishedTerrainAndMeshes()
        {
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
            var root=rivers.GeneratedRoot;var data=rivers.CarvedTerrainData;string json=rivers.PlanJson;
            rivers.WaterInset=rivers.Depth+1;
            Assert.That(rivers.Generate(),Is.False);
            Assert.That(rivers.GeneratedRoot,Is.SameAs(root)); Assert.That(rivers.CarvedTerrainData,Is.SameAs(data));
            Assert.That(rivers.PlanJson,Is.EqualTo(json));
        }

        [Test] public void CurvedMeshesUseExactPlannedBanksAndTheirInterpolatedInnerRows()
        {
            rivers.Width=1; rivers.BankWidth=.25f; rivers.CarveTerrainCopy=false;
            rivers.ProtectedAreas.Add(new RiverProtectionSettings { Id="curve-obstacle",Center=new Vector2(32,32),Size=new Vector2(8,8) });
            host.transform.SetPositionAndRotation(new Vector3(3,2,-5),Quaternion.Euler(0,17,0));
            host.transform.localScale=new Vector3(1.2f,1.1f,.9f);
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
            var route=rivers.LastPlan.Routes.Single();
            Assert.That(route.WaterPolyline.Any(p=>Math.Abs(p.X-32)>4),Is.True,"Expected a curved obstacle detour.");
            AssertMeshesMatchPlan();
            Assert.Throws<ArgumentException>(()=>RiverGeometry.Build(route,rivers.Width,rivers.BankWidth,RiverGeometryRole.Banks,3));
            Assert.Throws<OperationCanceledException>(()=>RiverGeometry.Build(route,rivers.Width,rivers.BankWidth,RiverGeometryRole.Water,cancellation:new CancellationToken(true)));
        }

        private void AssertMeshesMatchPlan()
        {
            var route=rivers.LastPlan.Routes.Single();
            double half=(double)rivers.Width/2, ratio=half/(half+rivers.BankWidth);
            foreach(var marker in rivers.GeneratedRoot.GetComponentsInChildren<RiverGeneratedGeometry>())
            {
                var vertices=marker.GetComponent<MeshFilter>().sharedMesh.vertices;
                int columns=marker.Role==RiverGeometryRole.Banks?4:2;
                Assert.That(vertices.Length,Is.EqualTo(route.WaterPolyline.Count*columns));
                for(int i=0;i<route.WaterPolyline.Count;i++)
                for(int c=0;c<columns;c++)
                {
                    var center=route.WaterPolyline[i];
                    var bankPoint=c<columns/2?route.RightBankPolyline[i]:route.LeftBankPolyline[i];
                    bool outer=marker.Role==RiverGeometryRole.Banks&&(c==0||c==3);
                    var expected=outer?bankPoint:new WorldPoint(center.X+(bankPoint.X-center.X)*ratio,
                        marker.Role==RiverGeometryRole.Water?center.Y:route.BedPolyline[i].Y,
                        center.Z+(bankPoint.Z-center.Z)*ratio);
                    var actual=marker.transform.TransformPoint(vertices[i*columns+c]);
                    Assert.That(actual.x,Is.EqualTo((float)expected.X).Within(2e-5f),marker.Role+" X");
                    Assert.That(actual.y,Is.EqualTo((float)expected.Y).Within(2e-5f),marker.Role+" Y");
                    Assert.That(actual.z,Is.EqualTo((float)expected.Z).Within(2e-5f),marker.Role+" Z");
                    if(!outer) continue;
                    Assert.That(actual.y,Is.GreaterThanOrEqualTo((float)center.Y-2e-5f));
                    if(i>0)
                    {
                        var previous=marker.transform.TransformPoint(vertices[(i-1)*columns+c]);
                        Assert.That(actual.y,Is.LessThanOrEqualTo(previous.y+2e-5f),"Rendered bank rises downstream.");
                    }
                }
            }
        }
        [Test] public void SeedTargetsUseSamePlannerAndRepeatExactly()
        {
            rivers.World.UseSeedAnchors=true; rivers.World.SeedAnchorCount=5; rivers.World.AnchorMargin=10;
            // This terrain descends along Z, so highest to lowest targets have a feasible downhill route.
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics); string first=rivers.PlanJson;
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics); Assert.That(rivers.PlanJson,Is.EqualTo(first));
            Assert.That(rivers.LastPlan.ToContribution(rivers.LastPlan.CoreInputIdentity).Waters.Count,Is.GreaterThan(0));
        }
        [Test] public void VertexBudgetFailureDoesNotLeakStagedMeshes()
        {
            Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics); var root=rivers.GeneratedRoot;
            int count=Resources.FindObjectsOfTypeAll<Mesh>().Count(m=>m.name.StartsWith("River ",StringComparison.Ordinal));
            rivers.MaximumGeometryVertices=3;
            Assert.That(rivers.Generate(),Is.False); Assert.That(rivers.GeneratedRoot,Is.SameAs(root));
            Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Count(m=>m.name.StartsWith("River ",StringComparison.Ordinal)),Is.EqualTo(count));
        }
        [Test] public void ReadOnlyOverlayDoesNotReplaceTerrain()
        {
            rivers.CarveTerrainCopy=false; Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
            Assert.That(rivers.World.Terrain.terrainData,Is.SameAs(original)); Assert.That(rivers.CarvedTerrainData,Is.Null);
        }
    }
}
