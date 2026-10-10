using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TerraLoom.Core;
using TerraLoom.Rivers;
using UnityEngine;
using UnityEngine.Rendering;

namespace TerraLoom.Integration
{
    /// <summary>Isolated vertical-contact experiment. Owns a fresh terrain, its local hole,
    /// closed replacement solid and collider. Never edits foreign terrain. Falling water
    /// is a separate parametric surface; it is deliberately not an X/Z height query.</summary>
    public sealed class VerticalWaterfallPrototype : MonoBehaviour
    {
        public bool NearVertical;
        public bool RotateQuarterTurn;
        public Material TerrainMaterial,CliffMaterial,WaterMaterial,FallMaterial;
        public TerrainLayer[] Layers;
        private GameObject generated;
        private List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        private readonly List<VerticalWaterfallPacket.ControllerProbe> measuredProbes=new List<VerticalWaterfallPacket.ControllerProbe>();
        public Terrain Ground {get;private set;}
        public MeshCollider Cliff {get;private set;}
        public WaterfallFallSurface Fall {get;private set;}
        public float MaximumSeamError {get;private set;}
        private const float OriginX=32,OriginY=8,OriginZ=32,CliffZ=-.125f;
        private Vector3 World(Vector3 p)=>RotateQuarterTurn?new Vector3(32+p.z-32,p.y,32-(p.x-32)):p;
        private static float Smooth(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
        private static float Taper(float x)=>1-Smooth((Mathf.Abs(x)-3)/4.5f);
        private float Width(float z)
        {
            float run=NearVertical?.025f:0;
            float t=z<4?(z-run)/(4-run):(8-z)/4;
            return 1.5f+.6f*Smooth(t);
        }
        private float Floor(float x,float z)
        {
            float w=Width(z),d=Mathf.Abs(x);
            return OriginY+(d<w?-1.1f*(1-d*d/(w*w)):Mathf.Min((d-w)*.48f,1.5f));
        }
        private float SurfaceGround(float x,float z,bool upper)=>Floor(x,z)+(upper?3.2f*Taper(x):0);
        private static Vector3 V(WorldPoint p)=>new Vector3((float)p.X,(float)p.Y,(float)p.Z);
        private static void Release(GameObject root,List<UnityEngine.Object> assets)
        {
            if(root){root.SetActive(false);if(Application.isPlaying)Destroy(root);else DestroyImmediate(root);}
            foreach(var a in assets)if(a){if(Application.isPlaying)Destroy(a);else DestroyImmediate(a);}
        }
        public void Generate(Func<bool> cancelled=null)
        {
            if(!TerrainMaterial||!CliffMaterial||!WaterMaterial||!FallMaterial||Layers==null||Layers.Length!=4)
                throw new InvalidOperationException("Vertical recipe materials missing.");
            double run=NearVertical?.025:0;
            var fall=new WaterfallFallSurface(new WorldPoint(32,11.2,32),new WorldPoint(32+(RotateQuarterTurn?run:0),8,32+(RotateQuarterTurn?0:run)),RotateQuarterTurn?1:0,RotateQuarterTurn?0:1,3);
            var candidate=new GameObject("Owned vertical fall, terrain hole and closed cliff");candidate.transform.SetParent(transform,false);
            var assets=new List<UnityEngine.Object>();
            try
            {
                var data=new TerrainData{heightmapResolution=513,size=new Vector3(64,32,64),alphamapResolution=64};assets.Add(data);
                var heights=new float[513,513];
                for(int z=0;z<513;z++)for(int x=0;x<513;x++)
                {
                    float cx=RotateQuarterTurn?32-z*.125f:x*.125f-32,cz=RotateQuarterTurn?x*.125f-32:z*.125f-32;
                    heights[z,x]=SurfaceGround(cx,cz,cz<CliffZ)/32;
                }
                data.SetHeights(0,0,heights);data.terrainLayers=Layers;
                var paint=new float[64,64,4];for(int z=0;z<64;z++)for(int x=0;x<64;x++)paint[z,x,0]=1;data.SetAlphamaps(0,0,paint);
                if(data.holesResolution!=512)throw new InvalidOperationException("Required native holes unsupported.");
                // Exact original grid boundaries: worldX24.5..39.5,Z30.75..33.25.
                // False only in our fresh owned terrain: no source holes can be overwritten.
                if(RotateQuarterTurn)data.SetHoles(246,196,new bool[120,20]);else data.SetHoles(196,246,new bool[20,120]);
                var terrainGO=Terrain.CreateTerrainGameObject(data);terrainGO.transform.SetParent(candidate.transform,false);
                var ground=terrainGO.GetComponent<Terrain>();ground.materialTemplate=TerrainMaterial;ground.heightmapPixelError=1;
                var vertices=new List<Vector3>();var triangles=new List<int>();
                void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward)
                {
                    int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});
                    if(Vector3.Dot(Vector3.Cross(b-a,c-a),outward)>=0)triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
                    else triangles.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});
                }
                Vector3 P(float x,float z,bool upper)=>new Vector3(32+x,SurfaceGround(x,z,upper),32+z);
                for(int ix=0;ix<120;ix++)
                {
                    float x=-7.5f+ix*.125f,nx=x+.125f;
                    for(int iz=0;iz<20;iz++)
                    {
                        float z=-1.25f+iz*.125f,nz=z+.125f;bool upper=z<CliffZ;
                        Quad(P(x,z,upper),P(nx,z,upper),P(nx,nz,upper),P(x,nz,upper),Vector3.up);
                    }
                    Quad(P(x,CliffZ,true),P(nx,CliffZ,true),P(nx,CliffZ,false),P(x,CliffZ,false),Vector3.forward);
                    foreach(float z in new[]{-1.25f,1.25f})
                    {
                        var a=P(x,z,z<0);var b=P(nx,z,z<0);
                        Quad(a,b,new Vector3(b.x,3,b.z),new Vector3(a.x,3,a.z),z<0?Vector3.back:Vector3.forward);
                    }
                }
                for(int iz=0;iz<20;iz++)foreach(float x in new[]{-7.5f,7.5f})
                {
                    float z=-1.25f+iz*.125f,nz=z+.125f;var a=P(x,z,z<CliffZ);var b=P(x,nz,z<CliffZ);
                    Quad(a,b,new Vector3(b.x,3,b.z),new Vector3(a.x,3,a.z),x<0?Vector3.left:Vector3.right);
                }
                // Bottom uses the same boundary segmentation, avoiding T junctions.
                for(int ix=0;ix<120;ix++)for(int iz=0;iz<20;iz++)
                {
                    float x=24.5f+ix*.125f,z=30.75f+iz*.125f;
                    Quad(new Vector3(x,3,z),new Vector3(x+.125f,3,z),new Vector3(x+.125f,3,z+.125f),new Vector3(x,3,z+.125f),Vector3.down);
                }
                var solid=new Mesh{name="Closed cliff replacement solid",indexFormat=IndexFormat.UInt32};assets.Add(solid);solid.SetVertices(vertices.Select(World).ToList());solid.SetTriangles(triangles,0);solid.RecalculateNormals();solid.RecalculateBounds();
                var cliffGO=new GameObject(solid.name);cliffGO.transform.SetParent(candidate.transform,false);
                cliffGO.AddComponent<MeshFilter>().sharedMesh=solid;cliffGO.AddComponent<MeshRenderer>().sharedMaterial=CliffMaterial;
                var cliff=cliffGO.AddComponent<MeshCollider>();cliff.sharedMesh=solid;
                var waterMaterial=new Material(WaterMaterial);var fallMaterial=new Material(FallMaterial);assets.Add(waterMaterial);assets.Add(fallMaterial);
                foreach(var material in new[]{waterMaterial,fallMaterial})
                {
                    material.SetFloat("_ImpactX",(float)fall.Impact.X);material.SetFloat("_ImpactZ",(float)fall.Impact.Z);
                    material.SetVector("_ImpactAxis",new Vector4((float)fall.Forward.X,(float)fall.Forward.Z,0,0));
                }
                void Water(string name,int count,float length,Func<int,float,Vector3> point,Material material)
                {
                    var vs=new List<Vector3>();var uv=new List<Vector2>();var ts=new List<int>();
                    for(int i=0;i<=count;i++)for(int j=0;j<=16;j++){vs.Add(point(i,(j-8)/8f));uv.Add(new Vector2((j-8)/8f,i/(float)count*length));}
                    for(int i=0;i<count;i++)for(int j=0;j<16;j++){int n=i*17+j;ts.AddRange(new[]{n,n+17,n+1,n+1,n+17,n+18});}
                    var mesh=new Mesh{name=name};assets.Add(mesh);mesh.SetVertices(vs);mesh.SetUVs(0,uv);mesh.SetTriangles(ts,0);mesh.RecalculateNormals();
                    var go=new GameObject(name);go.transform.SetParent(candidate.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
                }
                Water("Upper reach to exact lip",64,12,(i,a)=>V(fall.Sample(0,a*1.5))-V(fall.Forward)*(12*(1-i/64f))+Vector3.up*.01f,waterMaterial);
                Water("Parametric fall, zero-run safe",128,(float)fall.ArcLength,(i,a)=>V(fall.Sample(i/128d,a*1.5))+Vector3.up*.01f,fallMaterial);
                Water("Impact pool and receiving reach",128,17,(i,a)=>V(fall.Impact)+V(fall.Forward)*(i/128f*17)+V(fall.Across)*a*Width((float)run+i/128f*17)+Vector3.up*.01f,waterMaterial);
                Physics.SyncTransforms();
                if(cancelled!=null&&cancelled())throw new OperationCanceledException("Candidate cancelled before publication.");
                float seam=ValidateGeometry(ground,cliff,fall);
                // Publication follows validation; failure/cancel keeps previous owned state.
                Release(generated,owned);generated=candidate;owned=assets;Ground=ground;Cliff=cliff;Fall=fall;MaximumSeamError=seam;
            }
            catch{Release(candidate,assets);throw;}
        }
        private float ValidateGeometry(Terrain ground,MeshCollider cliff,WaterfallFallSurface fall)
        {
            var tc=ground.GetComponent<TerrainCollider>();float error=0;
            // Whole hole perimeter on both sides, not centre-only support tests.
            for(int i=0;i<=120;i++)foreach(float z in new[]{-1.25f,1.25f})foreach(float offset in new[]{-.002f,.002f})
            {
                float x=-7.5f+i*.125f;
                var ray=new Ray(World(new Vector3(32+x,31,32+z+offset)),Vector3.down);
                bool a=tc.Raycast(ray,out var th,40),b=cliff.Raycast(ray,out var ch,40);
                if(!a&&!b)throw new InvalidOperationException("Uncovered hole boundary.");
                float actual=a&&b?Mathf.Max(th.point.y,ch.point.y):a?th.point.y:ch.point.y;
                error=Mathf.Max(error,Mathf.Abs(actual-SurfaceGround(x,z+offset,z+offset<CliffZ)));
            }
            for(int i=0;i<=20;i++)foreach(float x in new[]{-7.5f,7.5f})foreach(float offset in new[]{-.002f,.002f})
            {
                float z=-1.25f+i*.125f;var ray=new Ray(World(new Vector3(32+x+offset,31,32+z)),Vector3.down);
                bool a=tc.Raycast(ray,out var th,40),b=cliff.Raycast(ray,out var ch,40);
                if(!a&&!b)throw new InvalidOperationException("Uncovered side hole boundary.");
                float actual=a&&b?Mathf.Max(th.point.y,ch.point.y):a?th.point.y:ch.point.y;
                error=Mathf.Max(error,Mathf.Abs(actual-SurfaceGround(x+offset,z,z<CliffZ)));
            }
            if(error>.004f)throw new InvalidOperationException("Cliff/terrain perimeter seam error "+error);
            for(int across=0;across<=32;across++)for(int progress=0;progress<=128;progress++)
            {
                var point=V(fall.Sample(progress/128d,(across-16)*1.5/16));
                var ray=new Ray(point+Vector3.up*.01f,Vector3.down);
                if(cliff.Raycast(ray,out var hit,40)&&hit.point.y>point.y+.002f)throw new InvalidOperationException("Fall penetrates solid.");
                if(progress==0||progress==128)
                {
                    var join=V(progress==0?fall.Lip:fall.Impact)+V(fall.Across)*(across-16)*1.5f/16;
                    ValidateJoin(point,join);
                }
            }
            return error;
        }
        public static void ValidateJoin(Vector3 fallEdge,Vector3 reachEdge)
        {
            float distance=Vector3.Distance(fallEdge,reachEdge);
            if(float.IsNaN(distance)||float.IsInfinity(distance)||distance>.0001f)throw new InvalidOperationException("Unmatched full-width water contact.");
        }
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();bool smoke=args.Contains("-terraloomSmoke");
            try{Generate();}catch(Exception e){Debug.LogError("TERRALOOM_VERTICAL_FAILED "+e);if(smoke)Application.Quit(1);yield break;}
            for(int i=0;i<5;i++)yield return null;
            if(!smoke)yield break;
            measuredProbes.Clear();
            // Explicit isolated gameplay rule: no water collider/barrier. A controller
            // crossing the lip must actually fall and land on the lower solid floor.
            foreach(float lateral in new[]{-1.25f,0,1.25f})
            {
                var probe=new GameObject("Actual left/centre/right drop probe");probe.transform.SetParent(generated.transform,false);
                var cc=probe.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.16f;cc.center=new Vector3(0,.9f,0);cc.stepOffset=.1f;cc.slopeLimit=60;
                cc.enabled=false;probe.transform.position=World(new Vector3(32+lateral,SurfaceGround(lateral,-.65f,true)+.05f,31.35f));cc.enabled=true;Physics.SyncTransforms();
                float startY=probe.transform.position.y,velocity=0;
                var measurement=new VerticalWaterfallPacket.ControllerProbe {lateral=lateral,start=probe.transform.position,forward=V(Fall.Forward),fixedDeltaTime=Time.fixedDeltaTime};
                for(int step=0;step<140;step++)
                {
                    if(cc.isGrounded&&velocity<0)velocity=-2;
                    velocity-=9.81f*Time.fixedDeltaTime;
                    cc.Move((Vector3.up*velocity+V(Fall.Forward)*.8f)*Time.fixedDeltaTime);yield return new WaitForFixedUpdate();
                }
                if(probe.transform.position.y>startY-2.5f||!cc.isGrounded)
                {Debug.LogError("TERRALOOM_VERTICAL_FAILED actual drop probe "+lateral+" final="+probe.transform.position+" startY="+startY+" grounded="+cc.isGrounded);Application.Quit(1);yield break;}
                Debug.Log("TERRALOOM_VERTICAL_DROP actualController lateral="+lateral+" drop="+(startY-probe.transform.position.y)+" grounded=true; isolated fall rule, not swim/navigation acceptance");
                measurement.end=probe.transform.position;measurement.grounded=cc.isGrounded;measuredProbes.Add(measurement);
                probe.SetActive(false);Destroy(probe);
            }
            try
            {
                int at=Array.IndexOf(args,"-terraloomScreenshot");if(at<0)throw new ArgumentException("Screenshot path required.");
                Capture(args[at+1]);Debug.Log("TERRALOOM_VERTICAL_SUCCESS run="+Fall.HorizontalRun+" fullwidthjoins=true holePerimeterError="+MaximumSeamError+"; isolated owned-terrain contact experiment");Application.Quit(0);
            }
            catch(Exception e){Debug.LogError("TERRALOOM_VERTICAL_FAILED "+e);Application.Quit(1);}
        }
        public void Capture(string path)
        {
            var camera=Camera.main;camera.fieldOfView=55;string stem=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path));
            var feet=new[]{new Vector2(36,34),new Vector2(35,30),new Vector2(36,38)};
            var names=new[]{"side","lip","foot"};
            var observers=new List<VerticalWaterfallPacket.Observer>();
            for(int i=0;i<3;i++)
            {
                var f=feet[i];var ray=new Ray(World(new Vector3(f.x,31,f.y)),Vector3.down);
                if(!Physics.Raycast(ray,out var hit,40))throw new InvalidOperationException("Observer has no ground.");
                camera.transform.position=hit.point+Vector3.up*1.7f;var target=World(new Vector3(32,i==1?11.3f:9.5f,32));
                camera.transform.LookAt(target);
                if(Physics.CheckSphere(camera.transform.position,.1f)||Physics.Linecast(camera.transform.position,target))throw new InvalidOperationException("Observer sight blocked "+names[i]);
                RuntimeVisualCapture.Save(camera,i==0?path:stem+"-"+names[i]+".png",1600,1000);
                observers.Add(new VerticalWaterfallPacket.Observer {name=names[i],eye=camera.transform.position,target=target,ground=hit.point,rotation=camera.transform.rotation,fov=camera.fieldOfView});
                CaptureDiagnostics(camera,stem+"-"+names[i]);
                Debug.Log("TERRALOOM_VERTICAL_CAMERA "+names[i]+" eye="+camera.transform.position.ToString("F6")+" ground="+hit.point.y+" height=1.7m fov=55");
            }
            var packet=VerticalWaterfallPacket.Save(this,stem+"-packet",observers.ToArray(),measuredProbes.ToArray());
            Debug.Log("TERRALOOM_VERTICAL_PACKET "+packet.caseName+" measuredColliderError="+packet.maximumColliderCentreError+" cameras="+packet.cameras.Length+" actualControllers="+packet.controllerProbes.Length);
        }
        private void CaptureDiagnostics(Camera camera,string stem)
        {
            var renderers=generated.GetComponentsInChildren<MeshRenderer>();
            var originals=renderers.Select(r=>r.sharedMaterial).ToArray();
            var diagnostics=new List<Material>();
            var colors=new[]{new Color(.4f,.4f,.4f),new Color(.1f,.4f,.7f),new Color(.2f,.7f,.7f),new Color(.1f,.3f,.6f)};
            try
            {
                for(int i=0;i<renderers.Length;i++)
                {
                    var m=new Material(WaterMaterial);diagnostics.Add(m);m.SetFloat("_Diagnostic",1);m.SetColor("_BaseColor",colors[i%colors.Length]);renderers[i].sharedMaterial=m;
                }
                RuntimeVisualCapture.Save(camera,stem+"-flat.png",1600,1000);
                foreach(var m in diagnostics)m.SetFloat("_Diagnostic",2);
                RuntimeVisualCapture.Save(camera,stem+"-normals.png",1600,1000);
            }
            finally
            {
                for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterial=originals[i];
                foreach(var m in diagnostics)if(Application.isPlaying)Destroy(m);else DestroyImmediate(m);
            }
        }
        private void OnDestroy(){Release(generated,owned);generated=null;Ground=null;Cliff=null;Fall=null;owned.Clear();}
    }
}
