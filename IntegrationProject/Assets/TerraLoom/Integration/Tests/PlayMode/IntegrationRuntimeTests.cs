using System.Collections;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using TerraLoom.Core.Unity;
using TerraLoom.Paths;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace TerraLoom.Tests
{
    public sealed class IntegrationRuntimeTests
    {
        [UnityTest] public IEnumerator RiverBridgeGroundAndRampsWorkTogetherWithoutChangingTheSourceTerrain()
        {
            var original=new TerrainData { heightmapResolution=129,size=new Vector3(96,16,96) };
            var heights=new float[129,129];for(int z=0;z<129;z++)for(int x=0;x<129;x++)heights[z,x]=(6-.03f*(96f*z/128))/16;
            original.SetHeights(0,0,heights);var terrain=Terrain.CreateTerrainGameObject(original);
            var host=new GameObject("Three-module runtime test");var ground=new Material(Shader.Find("Universal Render Pipeline/Lit"));var water=new Material(Shader.Find("TerraLoom/RiverWater"));
            try
            {
                var world=host.AddComponent<TerraLoomWorld>(); world.Terrain=terrain.GetComponent<Terrain>();
                world.ManualAnchors=new[] {
                    new ManualWorldAnchor { Id="source",Position=new Vector3(48,0,4) },new ManualWorldAnchor { Id="mouth",Position=new Vector3(48,0,92) },
                    new ManualWorldAnchor { Id="west",Position=new Vector3(12,0,48) },new ManualWorldAnchor { Id="east",Position=new Vector3(84,0,48) } };
                var paths=host.AddComponent<TerraLoomPaths>(); paths.World=world;paths.GroundMaterial=ground;paths.BridgeMaterial=ground; paths.AutomaticNetwork=false;
                paths.Connections.Add(new PathConnectionSettings { Id="road",StartId="west",EndId="east" });paths.MaximumSlope=.35f;paths.BridgeClearance=1.2f;
                var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;rivers.WaterMaterial=water;rivers.BedMaterial=ground;rivers.BankMaterial=ground;rivers.AutomaticSourceAndMouth=false;
                rivers.Connections.Add(new RiverConnectionSettings { Id="river",SourceId="source",MouthId="mouth" });
                var integration=host.AddComponent<TerraLoom.Integration.TerraLoomIntegration>();integration.World=world;integration.Paths=paths;integration.Rivers=rivers;
                Assert.That(integration.Generate(),Is.True,integration.Diagnostics);
                Assert.That(paths.LastPlan.Routes.Single().Surfaces.Contains(PathSurface.Bridge),Is.True,"A detour around the river does not prove negotiated bridge integration.");
                var sourceAfter=original.GetHeights(0,0,129,129);Assert.That(sourceAfter,Is.EqualTo(heights).Within(.00002f));
                Assert.That(world.Terrain.terrainData,Is.Not.SameAs(original));
                yield return new WaitForFixedUpdate();
                var route=paths.LastPlan.Routes.Single();
                for(int s=0;s<route.Surfaces.Count;s++)
                {
                    var a=route.Waypoints[s];var b=route.Waypoints[s+1];float length=Vector2.Distance(new Vector2((float)a.X,(float)a.Z),new Vector2((float)b.X,(float)b.Z));
                    int steps=Mathf.Max(2,Mathf.CeilToInt(length/.2f));
                    for(int i=0;i<=steps;i++)
                    {
                        float t=(float)i/steps,x=Mathf.Lerp((float)a.X,(float)b.X,t),z=Mathf.Lerp((float)a.Z,(float)b.Z,t);
                        Assert.That(Physics.Raycast(new Vector3(x,25,z),Vector3.down,out var hit,40),Is.True,"Missing collider at segment "+s+" t="+t);
                        Assert.That(hit.collider.GetComponent<PathGeneratedGeometry>(),Is.Not.Null,"Terrain or river collider replaces path at segment "+s+" t="+t);
                        if(route.Surfaces[s]==PathSurface.Bridge) Assert.That(hit.point.y,Is.GreaterThanOrEqualTo(Mathf.Min((float)a.Y,(float)b.Y)),"Deck collision below plan.");
                    }
                }
                var parts=paths.GeneratedRoot.GetComponentsInChildren<PathGeneratedGeometry>();
                Assert.That(parts.Any(p=>p.Role==PathGeometryRole.BridgeLanding),Is.True);
                foreach(var part in parts) Assert.That(part.GetComponent<MeshRenderer>().shadowCastingMode,
                    Is.EqualTo(part.Role==PathGeometryRole.BridgeDeck?ShadowCastingMode.On:ShadowCastingMode.Off));
                // Exercise Unity's actual walking solver as well as vertical collider coverage.
                var walker=new GameObject("Collider traversal probe");walker.transform.SetParent(host.transform);
                var controller=walker.AddComponent<CharacterController>();controller.height=1.8f;controller.radius=.3f;controller.stepOffset=.25f;controller.slopeLimit=40;
                var start=route.Waypoints[0];controller.enabled=false;walker.transform.position=new Vector3((float)start.X,(float)start.Y+1,(float)start.Z);controller.enabled=true;
                Physics.SyncTransforms();yield return new WaitForFixedUpdate();
                controller.Move(Vector3.down*.2f);
                Assert.That(controller.isGrounded,Is.True,"Walking probe did not settle on the first path surface.");
                for(int s=0;s<route.Surfaces.Count;s++)
                {
                    var a=route.Waypoints[s];var b=route.Waypoints[s+1];
                    int steps=Mathf.CeilToInt(Vector2.Distance(new Vector2((float)a.X,(float)a.Z),new Vector2((float)b.X,(float)b.Z))/.1f);
                    for(int i=1;i<=steps;i++)
                    {
                        float t=(float)i/steps;
                        var target=new Vector3(Mathf.Lerp((float)a.X,(float)b.X,t),walker.transform.position.y,Mathf.Lerp((float)a.Z,(float)b.Z,t));
                        controller.Move(target-walker.transform.position+Vector3.down*.06f);
                        Assert.That(Vector2.Distance(new Vector2(walker.transform.position.x,walker.transform.position.z),new Vector2(target.x,target.z)),Is.LessThan(.2f),"Walking solver is blocked at segment "+s);
                    }
                }
                Assert.That(walker.transform.position.y,Is.GreaterThan((float)route.Waypoints.Last().Y+.75f));
                var oldRiver=rivers.GeneratedRoot;var oldPath=paths.GeneratedRoot;
                Assert.That(integration.Generate(new CancellationToken(true)),Is.False);
                Assert.That(rivers.GeneratedRoot,Is.SameAs(oldRiver));Assert.That(paths.GeneratedRoot,Is.SameAs(oldPath));
                Assert.That(integration.Generate(),Is.True,integration.Diagnostics);
                integration.Clear();yield return null;
                Assert.That(world.Terrain.terrainData,Is.SameAs(original));Assert.That(paths.GeneratedRoot,Is.Null);Assert.That(rivers.GeneratedRoot,Is.Null);
            }
            finally { Object.Destroy(host);Object.Destroy(terrain);Object.Destroy(original);Object.Destroy(ground);Object.Destroy(water); }
            yield return null;
        }
    }
}
