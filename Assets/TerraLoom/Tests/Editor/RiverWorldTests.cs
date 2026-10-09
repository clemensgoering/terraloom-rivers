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
