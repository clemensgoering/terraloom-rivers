using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraLoom.Core.Unity;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>Runtime-only consumer recipe. Builds all three modules and regional art from one seed.
    /// Editor tools call Rebuild too; there is no UnityEditor dependency or baked geometry requirement.
    /// The drainage valley is a deliberately solvable directed-river example, not a hydrology simulation.</summary>
    [DefaultExecutionOrder(400)]
    public sealed class SeededWorldDemo : MonoBehaviour
    {
        public TerraLoomIntegration Composition;
        public RegionDecoration Decoration;
        public RegionalStyleProfile[] Profiles = Array.Empty<RegionalStyleProfile>();
        public int Seed = 2042;
        public bool GenerateOnStart = true;
        [Tooltip("Off preserves manually authored terrain, regions and targets; the same planners/materializers still run.")]
        public bool UseSeedConfiguration = true;
        public bool ShowControls = true;
        [SerializeField, TextArea] private string diagnostics;
        public string Diagnostics => diagnostics;
        private void Awake()
        {
            // A composition owns startup. Disable independent module Start hooks before ANY
            // Start executes, otherwise they could build from targets/offers not initialized yet.
            if(!GenerateOnStart||!Composition)return;
            Composition.GenerateOnStart=false;
            if(Composition.World)Composition.World.AutoGenerateOnStart=false;
            if(Composition.Paths)Composition.Paths.GenerateOnStart=false;
            if(Composition.Rivers)Composition.Rivers.GenerateOnStart=false;
        }
        private void Start()
        {
            if(GenerateOnStart&&!Rebuild())Debug.LogError(diagnostics,this);
            if(Environment.GetCommandLineArgs().Contains("-terraloomSmoke"))StartCoroutine(PlayerSmoke());
        }

        private IEnumerator PlayerSmoke()
        {
            foreach(int value in new[]{2042,2043,71})
            {
                Seed=value;
                if(!Rebuild()){Debug.LogError("TERRALOOM_RUNTIME_SMOKE_FAILED "+diagnostics);Application.Quit(1);yield break;}
                yield return new WaitForFixedUpdate();
                if(!Composition.Paths.LastPlan.Routes.Any(r=>r.Surfaces.Contains(TerraLoom.Paths.PathSurface.Bridge))||Decoration.PlacedCount<250)
                {Debug.LogError("TERRALOOM_RUNTIME_SMOKE_FAILED missing bridge or regional instances");Application.Quit(1);yield break;}
                Debug.Log("TERRALOOM_RUNTIME_SEED_OK "+Seed+" "+diagnostics);
            }
            yield return new WaitForEndOfFrame();
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-terraloomScreenshot");
            if(index>=0&&index+1<args.Length)
            {
                try{RuntimeVisualCapture.Save(Camera.main,args[index+1]);}
                catch(Exception ex){Debug.LogError("TERRALOOM_RUNTIME_SMOKE_FAILED "+ex.Message);Application.Quit(1);yield break;}
            }
            Debug.Log("TERRALOOM_RUNTIME_SMOKE_SUCCESS");Application.Quit(0);
        }

        /// <summary>Explicit full rebuild. Clear restores the source before Core replaces its own terrain.
        /// A failed full world rebuild reports its stage; it is not an atomic multi-module transaction.</summary>
        public bool Rebuild()
        {
            string stage="validate";
            try
            {
                if(!Composition||!Composition.World||!Composition.Paths||!Composition.Rivers||!Decoration)
                    throw new InvalidOperationException("Assign Core, composition, Paths, Rivers and decoration.");
                if(Profiles==null||Profiles.Length!=4)throw new InvalidOperationException("The demonstration needs four seasonal style profiles.");
                foreach(var profile in Profiles){if(!profile)throw new InvalidOperationException("Missing regional profile.");profile.Validate();}
                var world=Composition.World;Composition.GenerateOnStart=false;
                Composition.Paths.GenerateOnStart=false;Composition.Rivers.GenerateOnStart=false;world.AutoGenerateOnStart=false;
                stage="clear previous generation";GetComponent<IntegrationTestDriver>()?.ReturnToOverview();Decoration.Clear();Composition.Clear();
                float boundary=world.Terrain?world.Terrain.transform.position.z+world.Terrain.terrainData.size.z*.5f:world.transform.position.z+48;
                if(UseSeedConfiguration)
                {
                    stage="seed terrain and targets";world.Seed=Seed;world.Revision++;
                    world.Recipe=TerrainRecipe.DrainageValley;world.Resolution=129;world.TerrainSize=new Vector3(96,16,96);world.GenerateTerrain();
                    var origin=world.Terrain.transform.position;
                    float riverX=origin.x+32+2*(Hash(Seed,0)%17),roadZ=origin.z+32+2*(Hash(Seed,1)%17);
                    boundary=roadZ;world.UseSeedAnchors=false;
                    world.ManualAnchors=new[] {
                        Anchor("source",riverX,origin.z+4),Anchor("mouth",riverX,origin.z+92),
                        Anchor("west",origin.x+8,roadZ),Anchor("east",origin.x+88,roadZ)
                    };
                    float split=origin.x+48;
                    world.Areas=new[] {
                        Area("summer-assets",origin.x,origin.z,split,boundary),Area("winter-assets",origin.x,boundary,split,origin.z+96),
                        Area("summer-shader",split,origin.z,origin.x+96,boundary),Area("winter-shader",split,boundary,origin.x+96,origin.z+96),
                        new LandscapeArea{Id="wet-bank",Center=new Vector2(riverX,origin.z+48),Size=new Vector2(16,96),Feather=2,Priority=2,Tags=new[]{"wet","riparian"}}
                    };
                    RegionSurfacePainter.PaintOwnedTerrain(world,Profiles);
                    Composition.Rivers.AutomaticSourceAndMouth=false;Composition.Rivers.Connections.Clear();
                    Composition.Rivers.Connections.Add(new RiverConnectionSettings{Id="seed-river",SourceId="source",MouthId="mouth"});
                    Composition.Paths.AutomaticNetwork=false;Composition.Paths.Connections.Clear();
                    Composition.Paths.Connections.Add(new PathConnectionSettings{Id="seed-road",StartId="west",EndId="east"});
                }
                stage="river offers and path crossings";
                if(!Composition.Generate())throw new InvalidOperationException(Composition.Diagnostics);
                stage="regional assets and shader parameters";
                Decoration.World=world;Decoration.Profiles=Profiles;Decoration.Count=900;
                var exclusions=new List<Rect>();
                foreach(var river in Composition.Rivers.LastPlan.Routes)
                    AddSegmentExclusions(exclusions,river.WaterPolyline.Select(p=>new Vector3((float)p.X,(float)p.Y,(float)p.Z)).ToArray(),Composition.Rivers.Width*.5f+Composition.Rivers.BankWidth+4);
                foreach(var route in Composition.Paths.LastPlan.Routes)
                    AddSegmentExclusions(exclusions,route.Waypoints.Select(p=>new Vector3((float)p.X,(float)p.Y,(float)p.Z)).ToArray(),Composition.Paths.Width*.5f+4);
                Decoration.Exclusions=exclusions.ToArray();Decoration.Generate();
                ApplyBoundary(Composition.Rivers.GeneratedRoot,boundary);ApplyBoundary(Composition.Paths.GeneratedRoot,boundary);
                diagnostics="Seed "+Seed+": "+Composition.Diagnostics+" Regional instances: "+Decoration.PlacedCount+". No baked geometry used.";
                return true;
            }
            catch(Exception ex){diagnostics="Stage "+stage+": "+ex.Message;return false;}
        }
        private static ManualWorldAnchor Anchor(string id,float x,float z)=>new ManualWorldAnchor{Id=id,Position=new Vector3(x,0,z)};
        private static LandscapeArea Area(string id,float x0,float z0,float x1,float z1)=>new LandscapeArea{Id=id,Center=new Vector2((x0+x1)*.5f,(z0+z1)*.5f),Size=new Vector2(x1-x0,z1-z0+8),Feather=8,Tags=new[]{id.StartsWith("winter")?"winter":"summer"}};
        private static uint Hash(int seed,int channel){uint n=unchecked((uint)seed+(uint)channel*747796405u);n=unchecked((n^(n>>16))*2246822519u);return n^(n>>13);}
        private static void AddSegmentExclusions(List<Rect> result,Vector3[] points,float margin)
        {
            for(int i=1;i<points.Length;i++)result.Add(Rect.MinMaxRect(Mathf.Min(points[i-1].x,points[i].x)-margin,Mathf.Min(points[i-1].z,points[i].z)-margin,Mathf.Max(points[i-1].x,points[i].x)+margin,Mathf.Max(points[i-1].z,points[i].z)+margin));
        }
        private static void ApplyBoundary(GameObject root,float boundary)
        {
            if(!root)return;var block=new MaterialPropertyBlock();
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if(!renderer.sharedMaterial||!renderer.sharedMaterial.HasProperty("_UseWorldRegions"))continue;
                renderer.GetPropertyBlock(block);block.SetFloat("_UseWorldRegions",1);block.SetFloat("_WinterBoundaryZ",boundary);block.SetFloat("_RegionBlend",8);renderer.SetPropertyBlock(block);block.Clear();
            }
        }
        private void OnGUI()
        {
            if(!ShowControls)return;
            GUILayout.BeginArea(new Rect(12,145,450,130),GUI.skin.box);
            GUILayout.Label("Fully dynamic runtime world · seed "+Seed);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Same seed"))Rebuild();if(GUILayout.Button("Next seed")){Seed++;Rebuild();}
            GUILayout.EndHorizontal();GUILayout.Label(diagnostics);GUILayout.EndArea();
        }
    }
}
