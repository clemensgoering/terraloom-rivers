using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TerraLoom.Core.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace TerraLoom.Integration
{
    /// <summary>Configuration-only, runtime-built visual experiment. Not the general Rivers
    /// waterfall API, rights solver or multi-module integration acceptance. Owns only its
    /// generated child, transient terrain and meshes. No edit to source terrain/assets.</summary>
    public sealed class WaterfallLandscapePrototype : MonoBehaviour
    {
        public int Seed=4102026;
        [Tooltip("Consumes the anchored V1 level-pool comparison recipe. Off preserves the historical V0 picture recipe.")]
        public bool ComparisonRecipe;
        public Material TerrainMaterial,RockMaterial,BarkMaterial,LeafMaterial,GrassMaterial,WaterMaterial,FallMaterial;
        public TerrainLayer[] GroundLayers=Array.Empty<TerrainLayer>();
        private GameObject generated;
        private readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        public Terrain Ground {get;private set;}
        public WaterfallLandscapePlan Plan {get;private set;}
        private CharacterController walker;private Camera view;private bool walking;private float yaw,pitch;
        private readonly List<Rect> waterExclusions=new List<Rect>();
        private T Own<T>(T asset)where T:UnityEngine.Object{owned.Add(asset);return asset;}
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();bool smoke=args.Contains("-terraloomSmoke");
            try{Generate();}catch(Exception ex){Debug.LogError("TERRALOOM_WATERFALL_FAILED "+ex);if(smoke)Application.Quit(1);yield break;}
            for(int i=0;i<5;i++)yield return null;
            if(!smoke)yield break;
            // Exercise the actual dry observation controller, not just camera placement.
            var walkStart=walker.transform.position;
            for(int i=0;i<25;i++){walker.Move(new Vector3(0,-.08f,.08f));yield return new WaitForFixedUpdate();}
            try
            {
                float travel=walker.transform.position.z-walkStart.z;
                if(travel<1.7f||!walker.isGrounded)throw new InvalidOperationException("Dry observer movement failed: "+travel+"m; grounded="+walker.isGrounded);
                Debug.Log("TERRALOOM_WATERFALL_WALK actual CharacterController dry-bank probe travel="+travel.ToString("R",CultureInfo.InvariantCulture)+"m grounded=true; not full navigation acceptance");
                int at=Array.IndexOf(args,"-terraloomScreenshot");if(at<0||at+1>=args.Length)throw new ArgumentException("Screenshot path required");
                Capture(args[at+1]);Debug.Log("TERRALOOM_WATERFALL_PROTOTYPE_SUCCESS seed="+Seed+"; bounded recipe, not general Rivers acceptance");Application.Quit(0);
            }
            catch(Exception ex){Debug.LogError("TERRALOOM_WATERFALL_FAILED "+ex);Application.Quit(1);}
        }
        public void Generate()
        {
            Clear();if(GroundLayers.Length!=4||!TerrainMaterial||!WaterMaterial||!FallMaterial||!LeafMaterial)throw new InvalidOperationException("Prototype material recipe missing.");
            var watch=System.Diagnostics.Stopwatch.StartNew();Plan=new WaterfallLandscapePlan(Seed,ComparisonRecipe);
            generated=new GameObject("Generated waterfall landscape (bounded prototype)");generated.transform.SetParent(transform,false);
            var data=Own(new TerrainData{name="Runtime rocky valley / lip / pool",heightmapResolution=1025,
                size=new Vector3(WaterfallLandscapePlan.Size,WaterfallLandscapePlan.Height,WaterfallLandscapePlan.Size),alphamapResolution=512,baseMapResolution=512});
            var heights=new float[1025,1025];for(int z=0;z<1025;z++)for(int x=0;x<1025;x++)heights[z,x]=Plan.Ground(x*160f/1024,z*160f/1024)/70;
            data.SetHeights(0,0,heights);data.terrainLayers=GroundLayers;
            var paint=new float[512,512,4];
            for(int z=0;z<512;z++)for(int x=0;x<512;x++)
            {
                float wx=x*160f/511,wz=z*160f/511,dist=Mathf.Abs(wx-Plan.Centre(wz))-Plan.HalfWidth(wz);
                float slope=data.GetSteepness(x/511f,z/511f),n=Plan.Noise(wx*2,wz*2);
                float rock=Mathf.SmoothStep(0,1,(slope-32)/24)*.9f;
                float band=(1-Mathf.SmoothStep(0,1,Mathf.Abs(Mathf.Sin(wx*.11f+wz*.075f))))*Mathf.SmoothStep(0,1,(slope-18)/20)*.4f;
                rock=Mathf.Max(rock,band);
                float wet=1-Mathf.SmoothStep(0,1,(dist+.5f)/2.7f);
                float gravel=Mathf.Max(0,(1-Mathf.Abs(dist-2)/2))*.45f;
                paint[z,x,2]=rock;paint[z,x,1]=(1-rock)*wet;
                paint[z,x,3]=(1-rock)*(1-wet)*gravel;
                paint[z,x,0]=1-paint[z,x,1]-paint[z,x,2]-paint[z,x,3];
            }
            data.SetAlphamaps(0,0,paint);
            var go=Terrain.CreateTerrainGameObject(data);go.name="Final carved rocky terrain";go.transform.SetParent(generated.transform,false);
            Ground=go.GetComponent<Terrain>();Ground.materialTemplate=TerrainMaterial;Ground.heightmapPixelError=3;Ground.basemapDistance=220;
            var world=GetComponent<TerraLoomWorld>();if(world){world.Terrain=Ground;world.Seed=Seed;}
            BuildWater();BuildRocksAndVegetation();Physics.SyncTransforms();
            view=Camera.main;if(!view)throw new InvalidOperationException("Prototype camera missing.");
            var person=new GameObject("Walking observer: 1.80m");person.transform.SetParent(generated.transform,false);
            walker=person.AddComponent<CharacterController>();walker.height=1.8f;walker.radius=.28f;walker.center=new Vector3(0,.9f,0);walker.stepOffset=.28f;walker.slopeLimit=46;
            var foot=Plan.Observation(1);person.transform.position=new Vector3(foot.x,Ground.SampleHeight(new Vector3(foot.x,0,foot.y))+.04f,foot.y);
            Overview();watch.Stop();Debug.Log("TERRALOOM_WATERFALL_GENERATED ms="+watch.ElapsedMilliseconds+" terrain=1025x1025, seed="+Seed+" runtime original assets; press Tab to walk,1/2/3 observations,0 overview,R regenerate");
        }
        private void MeshObject(string name,Mesh mesh,Material material,Vector3 position,Vector3 scale,bool collision)
        {
            var go=new GameObject(name);go.transform.SetParent(generated.transform,false);go.transform.position=position;go.transform.localScale=scale;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        private void BuildWater()
        {
            // Dense metre-space sweep shares the exact profile and cross-section with terrain.
            // Final heightfield is inspected below; this fixed case is not a hydrology solver.
            foreach(bool fall in new[]{false,true})
            {
                var w=new PrototypeMeshRecipe.Writer();float along=0;Vector3 previous=Plan.CentrePoint(.5f);
                const float dz=.125f;const int columns=32;
                var stops=new SortedSet<float>();for(float z=.5f;z<=159.5f;z+=dz)stops.Add(z);
                if(ComparisonRecipe)foreach(var anchor in new[]{Plan.Profile.Lip,Plan.Profile.Impact,Plan.Profile.Pool,Plan.Profile.Outlet})stops.Add((float)anchor.Z);
                var zs=stops.ToArray();
                for(int row=0;row<zs.Length-1;row++)
                {
                    float z=zs[row],next=zs[row+1];
                    bool isFall=Plan.At(ComparisonRecipe?(z+next)*.5f:z).Section==TerraLoom.Rivers.WaterfallSection.Fall;
                    float distance=Vector3.Distance(previous,Plan.CentrePoint(next));
                    float startAlong=ComparisonRecipe?along:along+distance;along+=distance;previous=Plan.CentrePoint(next);
                    if(isFall!=fall)continue;
                    for(int i=0;i<columns;i++)
                    {
                        float a=(i/(float)columns*2-1),b=((i+1)/(float)columns*2-1);
                        Vector3 P(float zz,float u)=>new Vector3(Plan.Centre(zz)+Plan.HalfWidth(zz)*u,Plan.Water(zz)+.012f,zz);
                        int start=w.U.Count;w.Quad(P(z,a),P(next,a),P(next,b),P(z,b));
                        w.U[start]=new Vector2(a,startAlong);w.U[start+1]=new Vector2(a,startAlong+(ComparisonRecipe?distance:dz));
                        w.U[start+2]=new Vector2(b,startAlong+(ComparisonRecipe?distance:dz));w.U[start+3]=new Vector2(b,startAlong);
                    }
                }
                var mesh=Own(w.Finish(fall?"Fall lip to pool, shared continuous profile":"Upper / lower water and small pool"));
                MeshObject(mesh.name,mesh,fall?FallMaterial:WaterMaterial,Vector3.zero,Vector3.one,false);
                float minimum=float.PositiveInfinity;foreach(var p in mesh.vertices)minimum=Mathf.Min(minimum,p.y-Ground.SampleHeight(p));
                Debug.Log("TERRALOOM_WATERFALL_CONTACT "+mesh.name+" vertices="+mesh.vertexCount+" minVertexTerrain="+minimum.ToString("R",CultureInfo.InvariantCulture)+"m; discrete vertices, no continuous proof");
                if(minimum<-.035f)throw new InvalidOperationException("Water intersects final heightfield: "+minimum);
            }
            // Invisible physical deep/steep region, same plan footprint. Shallow shore remains accessible.
            for(int z=2;z<159;z+=2)
            {
                var block=new GameObject("Deep water / fall exclusion "+z);block.transform.SetParent(generated.transform,false);
                block.layer=2; // IgnoreRaycast: physical walking blocker, not an opaque sight obstacle.
                block.transform.position=new Vector3(Plan.Centre(z),Plan.Water(z)+.7f,z);
                var box=block.AddComponent<BoxCollider>();box.size=new Vector3(Mathf.Max(2,Plan.HalfWidth(z)*2-1),4,2.1f);
                waterExclusions.Add(new Rect(Plan.Centre(z)-Plan.HalfWidth(z)-1,z-1.2f,Plan.HalfWidth(z)*2+2,2.4f));
            }
        }
        private void BuildRocksAndVegetation()
        {
            var random=new System.Random(Seed);float Next(float a,float b)=>a+(b-a)*(float)random.NextDouble();
            var rocks=Enumerable.Range(0,5).Select(i=>Own(PrototypeMeshRecipe.Rock(38+i*13))).ToArray();
            void Rock(float x,float z,Vector3 scale,int variant,bool collider)
            {
                for(int i=0;i<3;i++)if((Plan.Observation(i)-new Vector2(x,z)).magnitude<Mathf.Max(scale.x,scale.z)+1.5f)return;
                // Every lower-half vertex must be below final terrain, including the downhill
                // outer footprint. A centre-only height made hillside rocks visibly levitate.
                float y=float.PositiveInfinity;var rock=rocks[variant%5];
                foreach(var p in rock.vertices)if(p.y<=0)
                    y=Mathf.Min(y,Ground.SampleHeight(new Vector3(x+p.x*scale.x,0,z+p.z*scale.z))-p.y*scale.y-.09f);
                MeshObject("Original embedded rock "+variant,rock,RockMaterial,new Vector3(x,y,z),scale,collider);
            }
            // Structural lip shoulders/rock masses: never obstruct the flowing footprint.
            for(int side=-1;side<=1;side+=2)for(int i=0;i<2;i++)
            {float z=87+i*4;Rock(Plan.Centre(z)+side*(6.1f+i*.4f),z,new Vector3(3.2f,3.7f,3),i+(side>0?2:0),true);}
            for(int i=0;i<165;i++)
            {
                float x=Next(6,154),z=Next(5,155),d=Mathf.Abs(x-Plan.Centre(z));
                if(d<Plan.HalfWidth(z)+3)continue;
                float size=d>20?Next(1.1f,3.4f):Next(.3f,1.3f);
                Rock(x,z,new Vector3(size*Next(.8f,1.6f),size*Next(.5f,.9f),size),i,d<30);
            }
            var trees=Enumerable.Range(0,4).Select(i=>PrototypeMeshRecipe.Tree(Seed+i*41)).ToArray();
            foreach(var tree in trees){Own(tree.trunk);Own(tree.leaves);}
            bool ObservationClear(float x,float z)
            {
                // Keep the fixed comparison overview's whole foliage corridor open.
                // Trunk/capsule raycasts alone miss non-colliding leaf geometry.
                if(ComparisonRecipe)
                {
                    var a=new Vector2(69,71);var b=new Vector2(80,88);var point=new Vector2(x,z);
                    float t=Mathf.Clamp01(Vector2.Dot(point-a,b-a)/(b-a).sqrMagnitude);
                    if((point-Vector2.Lerp(a,b,t)).sqrMagnitude<36)return false;
                }
                for(int i=0;i<3;i++){var p=Plan.Observation(i);if((p-new Vector2(x,z)).sqrMagnitude<12)return false;}return true;
            }
            for(int i=0;i<330;i++)
            {
                float z=Next(6,154),x=Plan.Centre(z)+Next(-40,40),dist=Mathf.Abs(x-Plan.Centre(z));
                float slope=Ground.terrainData.GetSteepness(x/160,z/160);
                if(dist<Plan.HalfWidth(z)+6||slope>37||!ObservationClear(x,z))continue;
                float y=Ground.SampleHeight(new Vector3(x,0,z)),scale=Next(.72f,1.27f);int v=i%4;
                var position=new Vector3(x,y-.07f,z);var size=Vector3.one*scale;
                MeshObject("Branched valley tree "+i,trees[v].trunk,BarkMaterial,position,size,true);
                MeshObject("Individual broadleaf canopy "+i,trees[v].leaves,LeafMaterial,position,size,false);
            }
            var grass=Own(PrototypeMeshRecipe.Grass());var groups=new List<CombineInstance>();
            for(int i=0;i<4200;i++)
            {
                float x=Next(4,156),z=Next(4,156),dist=Mathf.Abs(x-Plan.Centre(z));
                if(dist<Plan.HalfWidth(z)+1.8f||Ground.terrainData.GetSteepness(x/160,z/160)>34)continue;
                var p=new Vector3(x,Ground.SampleHeight(new Vector3(x,0,z)),z);
                groups.Add(new CombineInstance{mesh=grass,transform=Matrix4x4.TRS(p,Quaternion.Euler(0,Next(0,360),0),Vector3.one*Next(.7f,1.4f))});
            }
            var combined=Own(new Mesh{name="Original clustered fine grass",indexFormat=IndexFormat.UInt32});combined.CombineMeshes(groups.ToArray());
            MeshObject(combined.name,combined,GrassMaterial,Vector3.zero,Vector3.one,false);
            generated.transform.Find(combined.name).GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        public void Capture(string path)
        {
            string stem=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path));
            var evidence=new WaterfallComparisonManifest(Plan.Profile,Seed);
            int width=ComparisonRecipe?1600:1440,height=ComparisonRecipe?1000:900;
            Overview();RuntimeVisualCapture.Save(view,path,width,height);
            evidence.RecordCamera("overview",view,null,0,ComparisonRecipe?new Vector3(80,13.4f,88):new Vector3(78,12,86));
            for(int i=0;i<3;i++)
            {
                Observe(i);Physics.SyncTransforms();var eye=view.transform.position;var foot=Plan.Observation(i);
                float ground=Ground.SampleHeight(new Vector3(foot.x,0,foot.y));
                // Exclude our own controller; physical dry-space blockers/rocks must not contain the observer.
                walker.enabled=false;bool blocked=Physics.CheckCapsule(new Vector3(foot.x,ground+.65f,foot.y),new Vector3(foot.x,ground+1.45f,foot.y),.28f)
                    ||Physics.CheckSphere(eye,.12f);
                if(blocked)throw new InvalidOperationException("Observation capsule blocked at "+i);
                if(Physics.Linecast(eye,Plan.Look(i),out var obstruction,~(1<<2)))
                    throw new InvalidOperationException("Observation sight corridor blocked at "+i+": "+obstruction.collider.name);
                string name=new[]{"upper-lip","foot-pool","outflow"}[i];
                RuntimeVisualCapture.Save(view,stem+"-"+name+".png",width,height);
                evidence.RecordCamera(name,view,new Vector3(foot.x,ground,foot.y),1.7f,Plan.Look(i));
                Debug.Log("TERRALOOM_WATERFALL_CAMERA "+i+" eye="+eye.ToString("F4")+" ground="+ground.ToString("R",CultureInfo.InvariantCulture)+" eyeAboveFinalTerrain=1.7000m fov="+view.fieldOfView+" dryCapsuleClear=true targetSightLineClear=true; line test is not full frustum visibility proof");
                walker.enabled=true;
            }
            if(ComparisonRecipe)
            {
                evidence.RecordBed(Plan.Profile,Ground);
                evidence.terrainPatch=Path.GetFileName(stem+"-terrain.json");
                WaterfallTerrainPatch.Measure(Ground).Save(stem+"-terrain.json");
                File.WriteAllText(stem+"-profile.json",JsonUtility.ToJson(evidence,true));
            }
            Overview();
        }
        private void Observe(int i)
        {
            walking=false;Cursor.lockState=CursorLockMode.None;var p=Plan.Observation(i);
            view.transform.SetParent(null);view.transform.position=new Vector3(p.x,Ground.SampleHeight(new Vector3(p.x,0,p.y))+1.7f,p.y);view.transform.LookAt(Plan.Look(i));
        }
        private void Overview()
        {walking=false;Cursor.lockState=CursorLockMode.None;view.transform.SetParent(null);
            view.transform.position=ComparisonRecipe?new Vector3(69,21.4f,71):new Vector3(140,73,32);
            view.transform.LookAt(ComparisonRecipe?new Vector3(80,13.4f,88):new Vector3(78,12,86));}
        private void Update()
        {
            if(!Ground||Keyboard.current==null)return;var k=Keyboard.current;
            if(k.rKey.wasPressedThisFrame){Generate();return;}
            if(k.digit0Key.wasPressedThisFrame)Overview();for(int i=0;i<3;i++)if(new[]{k.digit1Key,k.digit2Key,k.digit3Key}[i].wasPressedThisFrame)Observe(i);
            if(k.tabKey.wasPressedThisFrame)
            {
                walking=!walking;Cursor.lockState=walking?CursorLockMode.Locked:CursorLockMode.None;
                if(walking){view.transform.SetParent(walker.transform,false);view.transform.localPosition=new Vector3(0,1.7f,0);yaw=walker.transform.eulerAngles.y;pitch=0;}
            }
            if(k.escapeKey.wasPressedThisFrame)Overview();if(!walking)return;
            var delta=Mouse.current?.delta.ReadValue()??Vector2.zero;yaw+=delta.x*.13f;pitch=Mathf.Clamp(pitch-delta.y*.13f,-75,75);
            walker.transform.rotation=Quaternion.Euler(0,yaw,0);view.transform.localRotation=Quaternion.Euler(pitch,0,0);
            float x=(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),z=(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0);
            walker.Move((walker.transform.right*x+walker.transform.forward*z).normalized*(k.leftShiftKey.isPressed?5:2.6f)*Time.deltaTime+Vector3.down*4*Time.deltaTime);
        }
        private void Clear()
        {
            if(view)view.transform.SetParent(null);
            if(generated){generated.SetActive(false);if(Application.isPlaying)Destroy(generated);else DestroyImmediate(generated);}
            foreach(var item in owned)if(item){if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}owned.Clear();waterExclusions.Clear();
            Ground=null;walker=null;generated=null;
        }
        private void OnDestroy()=>Clear();
    }
}
