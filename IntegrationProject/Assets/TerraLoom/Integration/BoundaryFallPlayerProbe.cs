using System;
using System.Collections;
using System.IO;
using System.Linq;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>Opt-in Windows Player evidence for the baked Core/Rivers/Paths fall fixture.</summary>
    public sealed class BoundaryFallPlayerProbe : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            if(!args.Contains("-terraloomBoundaryProbe"))yield break;
            yield return null;yield return null;
            int exit=0;
            try
            {
                var output=Arg(args,"-terraloomBoundaryOutput");
                if(string.IsNullOrEmpty(output))throw new InvalidOperationException("Missing boundary evidence output directory.");
                Directory.CreateDirectory(output);
                var composition=FindFirstObjectByType<TerraLoomIntegration>();
                if(!composition||!composition.World||!composition.Rivers||!composition.Paths)
                    throw new InvalidOperationException("Baked Composition host is incomplete.");
                var rivers=composition.Rivers;var paths=composition.Paths;
                if(!rivers.GeneratedRoot||!paths.GeneratedRoot||!rivers.PlanJson.Contains("\"format\": 4"))
                    throw new InvalidOperationException("Baked fall, Paths or format-4 plan is missing.");
                var terrain=composition.World.Terrain;
                if(!terrain||terrain.terrainData!=rivers.CarvedTerrainData||
                   terrain.GetComponent<TerrainCollider>().terrainData!=rivers.CarvedTerrainData)
                    throw new InvalidOperationException("Saved terrain/collider is not bound to the carved fall.");
                var riverParts=rivers.GeneratedRoot.GetComponentsInChildren<RiverGeneratedGeometry>(true);
                if(riverParts.Length!=5||riverParts.Count(p=>p.Role==RiverGeometryRole.FallRock)!=1)
                    throw new InvalidOperationException("Baked fall rock/receiving water parts missing.");
                var pathParts=paths.GeneratedRoot.GetComponentsInChildren<PathGeneratedGeometry>(true);
                var body=pathParts.Single(p=>p.Role==PathGeometryRole.BridgeStructure);
                var bodyMesh=body.GetComponent<MeshFilter>().sharedMesh;
                if(!bodyMesh||body.GetComponent<MeshCollider>().sharedMesh!=bodyMesh||
                   pathParts.Count(p=>p.Role==PathGeometryRole.BridgeRamp)!=2)
                    throw new InvalidOperationException("Baked timber truss or ramps lack physical colliders.");
                var deck=pathParts.Single(p=>p.Role==PathGeometryRole.BridgeDeck);
                var deckBounds=deck.GetComponent<MeshRenderer>().bounds;
                var bodyBounds=body.GetComponent<MeshRenderer>().bounds;
                Probe("lip",new Vector3(5.7f,12,16));
                Probe("pool",new Vector3(9.5f,7,16));
                Probe("outlet",new Vector3(29,7,16));
                var views=new[]{
                    new View("lip",new Vector3(1,9.2f,11),new Vector3(6,7,16)),
                    new View("side",new Vector3(6,6.3f,8),new Vector3(6,5,16)),
                    new View("foot",new Vector3(11,3.4f,10),new Vector3(6,4,16)),
                    new View("pool",new Vector3(13,4.2f,11),new Vector3(10,2.1f,16)),
                    new View("outlet",new Vector3(31,4,11),new Vector3(27,2.1f,16)),
                    new View("fall-wide",new Vector3(17,7,3),new Vector3(8,5,16)),
                    new View("receiving-bank",new Vector3(23,4.4f,26),new Vector3(26,2.2f,16)),
                    new View("bridge-end",new Vector3(bodyBounds.center.x+4,5,bodyBounds.min.z-5),bodyBounds.center),
                    new View("bridge-underside",new Vector3(bodyBounds.center.x+5,bodyBounds.min.y-.25f,bodyBounds.center.z),bodyBounds.center),
                    new View("overview",new Vector3(38,22,-8),new Vector3(16,4,16))};
                foreach(var view in views)Capture(output,view);
                WalkBridge(deckBounds,pathParts.Where(p=>p.Role==PathGeometryRole.BridgeRamp).ToArray());
                WalkLanding("pool",new Vector3(10,8,16));
                WalkLanding("receiving bank",new Vector3(22,8,19));
                WalkLanding("outlet",new Vector3(30,8,16));
                Debug.Log("TERRALOOM_BOUNDARY_PLAYER_PASS riverParts="+riverParts.Length+
                    " ramps=2 views="+views.Length+" bridgeSpan="+Mathf.Max(deckBounds.size.x,deckBounds.size.z).ToString("F2"));
            }
            catch(Exception ex){exit=1;Debug.LogError("TERRALOOM_BOUNDARY_PLAYER_FAIL "+ex);}
            Application.Quit(exit);
        }

        private static string Arg(string[] args,string key)
        {for(int i=0;i<args.Length-1;i++)if(args[i]==key)return args[i+1];return null;}
        private static void Probe(string label,Vector3 start)
        {
            if(!Physics.Raycast(start,Vector3.down,out var hit,20))
                throw new InvalidOperationException(label+" has no supporting collider.");
            Debug.Log("TERRALOOM_BOUNDARY_CONTACT "+label+" "+hit.collider.name+" y="+hit.point.y.ToString("F3"));
        }
        private static void WalkLanding(string label,Vector3 start)
        {
            var go=new GameObject("Controller "+label);go.transform.position=start;
            var controller=go.AddComponent<CharacterController>();controller.radius=.3f;controller.height=1.8f;
            try
            {
                bool landed=false;
                for(int i=0;i<48;i++)if((controller.Move(Vector3.down*.3f)&CollisionFlags.Below)!=0){landed=true;break;}
                if(!landed)throw new InvalidOperationException("Controller did not land at "+label);
                Debug.Log("TERRALOOM_BOUNDARY_LANDING "+label+" y="+go.transform.position.y.ToString("F3"));
            }
            finally{Destroy(go);}
        }
        private static void WalkBridge(Bounds deck,PathGeneratedGeometry[] ramps)
        {
            float z0=Mathf.Min(deck.min.z,ramps.Min(r=>r.GetComponent<MeshRenderer>().bounds.min.z));
            float z1=Mathf.Max(deck.max.z,ramps.Max(r=>r.GetComponent<MeshRenderer>().bounds.max.z));
            float x=deck.center.x;
            if(z1-z0<5)throw new InvalidOperationException("Bridge walk requires a longitudinal span.");
            var start=new Vector3(x,20,z0+.4f);
            if(!Physics.Raycast(start,Vector3.down,out var first,30))
                throw new InvalidOperationException("Bridge approach has no collider.");
            var go=new GameObject("Bridge walking controller");
            go.transform.position=new Vector3(x,first.point.y+.94f,z0+.4f);
            var controller=go.AddComponent<CharacterController>();controller.radius=.25f;controller.height=1.8f;
            controller.stepOffset=.45f;controller.slopeLimit=50;
            try
            {
                int steps=0;
                for(float z=z0+.65f;z<z1-.25f;z+=.25f)
                {
                    if(!Physics.Raycast(new Vector3(x,20,z),Vector3.down,out var hit,30))
                        throw new InvalidOperationException("Bridge walk support missing at z="+z.ToString("F2"));
                    var delta=new Vector3(0,hit.point.y-(go.transform.position.y-.9f)-.04f,z-go.transform.position.z);
                    float beforeZ=go.transform.position.z,beforeY=go.transform.position.y;
                    controller.Move(delta);
                    if(go.transform.position.z<z-.15f)
                        throw new InvalidOperationException("Bridge controller blocked at z="+z.ToString("F2")+
                            " from="+beforeZ.ToString("F2")+" to="+go.transform.position.z.ToString("F2")+
                            " y="+beforeY.ToString("F2")+" support="+hit.point.y.ToString("F2")+
                            " collider="+hit.collider.name+" role="+
                            hit.collider.GetComponent<PathGeneratedGeometry>()?.Role+" move="+delta);
                    steps++;
                }
                Debug.Log("TERRALOOM_BOUNDARY_BRIDGE_WALK_PASS steps="+steps+
                    " z="+go.transform.position.z.ToString("F2"));
            }
            finally{Destroy(go);}
        }
        private static void Capture(string folder,View view)
        {
            var go=new GameObject("Boundary camera "+view.Name);var camera=go.AddComponent<Camera>();
            camera.transform.position=view.Position;camera.transform.LookAt(view.Target);camera.fieldOfView=60;
            camera.nearClipPlane=.1f;camera.clearFlags=CameraClearFlags.Skybox;
            var target=new RenderTexture(960,540,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                var pixels=new Texture2D(960,540,TextureFormat.RGB24,false);
                try{pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();
                    File.WriteAllBytes(Path.Combine(folder,view.Name+".png"),pixels.EncodeToPNG());}
                finally{Destroy(pixels);}
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;Destroy(target);Destroy(go);}
        }
        private readonly struct View
        {
            public readonly string Name;public readonly Vector3 Position,Target;
            public View(string name,Vector3 position,Vector3 target){Name=name;Position=position;Target=target;}
        }
    }
}
