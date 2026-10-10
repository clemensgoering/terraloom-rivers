using System;
using System.IO;
using System.Linq;
using TerraLoom.Core.Editor;
using TerraLoom.Core.Unity;
using TerraLoom.Paths.Unity;
using TerraLoom.Paths.Editor;
using TerraLoom.Rivers.Unity;
using TerraLoom.Rivers.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TerraLoom.Integration.Editor
{
    public static class IntegrationSampleBuilder
    {
        public const string Root="Assets/TerraLoom/Integration/Generated";
        [MenuItem("Tools/TerraLoom/Integration/Build Terrain Brush Crossing Scene (Experimental)")]
        public static void BuildBrushInteractive() { if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())BuildBrushBatch(); }
        /// <summary>Straight brush river with one negotiated road crossing. This fixture is separate
        /// from the legacy gallery and does not promise arbitrary seeded crossing acceptance.</summary>
        public static void BuildBrushBatch()=>BuildBrushRecipe(false);
        [MenuItem("Tools/TerraLoom/Integration/Build Short Timber Crossing (Experimental)")]
        public static void BuildTimberInteractive(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())BuildTimberBatch();}
        public static void BuildTimberBatch()=>BuildBrushRecipe(true);
        private static void BuildBrushRecipe(bool timber)
        {
            RiversSampleBuilder.BuildInsetBatch();
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            var rivers=UnityEngine.Object.FindFirstObjectByType<TerraLoomRivers>();var world=rivers.World;
            rivers.Clear();
            // Own the source asset as well: rebuilding the standalone/legacy gallery must not
            // rewrite the source that Clear and freshness use in this independent fixture.
            var brushSource=SeasonalSampleBuilder.Save(TerrainAssetClone.Create(world.Terrain.terrainData),Root+(timber?"/TimberSourceTerrain.asset":"/BrushSourceTerrain.asset"));
            world.Terrain.terrainData=brushSource;world.Terrain.GetComponent<TerrainCollider>().terrainData=brushSource;
            // A dedicated straight acceptance case; the standalone curved/protected sample remains
            // separate. Curved adjacent crossing windows still need a joint corridor contract.
            rivers.ProtectedAreas.Clear();world.ManualAnchors[0].Position=new Vector3(48,0,4);
            world.ManualAnchors[1].Position=new Vector3(48,0,92);
            world.ManualAnchors=world.ManualAnchors.Concat(new[] {
                new ManualWorldAnchor { Id="west",Position=new Vector3(12,0,48) },
                new ManualWorldAnchor { Id="east",Position=new Vector3(84,0,48) } }).ToArray();
            // Terrain supplies the visible channel; these materials must be unnecessary.
            rivers.BedMaterial=null;rivers.BankMaterial=null;
            var pathHost=new GameObject("Paths (optional module)");pathHost.transform.SetParent(world.transform,false);
            TerraLoomEditorIcons.Apply(pathHost,"Paths");
            var paths=pathHost.AddComponent<TerraLoomPaths>();paths.World=world;paths.AutomaticNetwork=false;
            paths.Connections.Add(new PathConnectionSettings { Id="brush-crossing",StartId="west",EndId="east" });
            paths.GroundMaterial=MakeMaterial("BrushGravel",new Color(.55f,.42f,.26f),false);
            paths.BridgeMaterial=timber?MakeTimberWood():MakeMaterial("BrushBridge",new Color(.29f,.16f,.065f),true);
            paths.GroundMaterial.SetFloat("_UseWorldRegions",0);paths.BridgeMaterial.SetFloat("_UseWorldRegions",0);
            paths.BridgeClearance=1.2f;paths.MaximumSlope=.35f;
            // Separate short-span fixture: the original wide case exceeds the short beam kit's
            // fixed 6m limit. Never raise that construction limit merely to fit the sample.
            if(timber){rivers.Width=2;rivers.BankWidth=.75f;paths.TimberBeamBridge=true;}
            var composition=world.gameObject.AddComponent<TerraLoomIntegration>();composition.World=world;composition.Paths=paths;composition.Rivers=rivers;
            if(!composition.Generate())throw new InvalidOperationException(composition.Diagnostics);
            if(!paths.LastPlan.Routes.Any(r=>r.Surfaces.Contains(TerraLoom.Paths.PathSurface.Bridge)))
                throw new InvalidOperationException("Brush fixture must cross the river on a negotiated bridge.");
            // Composition clears stale vegetation. Refresh the fixture's exclusions from both
            // final offers and path envelopes before placing against the carved terrain.
            var exclusions=rivers.LastPlan.Routes.SelectMany(r=>r.Offers).SelectMany(o=>o.Reservations).Select(a=>new Rect((float)a.Bounds.MinX-2,(float)a.Bounds.MinZ-2,(float)(a.Bounds.MaxX-a.Bounds.MinX)+4,(float)(a.Bounds.MaxZ-a.Bounds.MinZ)+4)).ToList();
            float margin=paths.Width*.5f+2;
            foreach(var route in paths.LastPlan.Routes)for(int i=1;i<route.Waypoints.Count;i++)
            {
                var a=route.Waypoints[i-1];var b=route.Waypoints[i];
                exclusions.Add(Rect.MinMaxRect((float)Math.Min(a.X,b.X)-margin,(float)Math.Min(a.Z,b.Z)-margin,(float)Math.Max(a.X,b.X)+margin,(float)Math.Max(a.Z,b.Z)+margin));
            }
            foreach(var decoration in world.GetComponentsInChildren<RegionDecoration>())
            {decoration.Exclusions=exclusions.ToArray();decoration.Generate();}
            RiversSampleBuilder.Bake(rivers,Root+(timber?"/TimberRiver":"/BrushRiver"));PathsSampleBuilder.Bake(paths,Root+(timber?"/TimberPaths":"/BrushPaths"));
            var camera=UnityEngine.Object.FindFirstObjectByType<Camera>();camera.transform.position=new Vector3(104,66,-22);camera.transform.LookAt(new Vector3(48,4,42));
            var driver=world.gameObject.AddComponent<IntegrationTestDriver>();driver.Composition=composition;driver.View=camera;
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(world.gameObject.scene,Root+(timber?"/TerraLoomTimberCrossing.unity":"/TerraLoomBrushCrossing.unity"));
            Debug.Log("Experimental straight brush crossing saved. "+composition.Diagnostics);
        }
        [MenuItem("Tools/TerraLoom/Integration/Build Test Scene")]
        public static void BuildInteractive() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildBatch(); }
        public static void BuildBatch()
        {
            CoreSampleBuilder.BuildBatch();
            RiversSampleBuilder.BuildBatch();
            Directory.CreateDirectory(Root); AssetDatabase.Refresh();
            var rivers=UnityEngine.Object.FindFirstObjectByType<TerraLoomRivers>(); var world=rivers.World;
            // Banks touch both world edges: a successful road must negotiate water rather than skirt a river endpoint.
            world.ManualAnchors[0].Position=new Vector3(48,0,4);
            world.ManualAnchors[1].Position=new Vector3(48,0,92);
            world.ManualAnchors=world.ManualAnchors.Concat(new[] {
                new ManualWorldAnchor { Id="west",Position=new Vector3(12,0,48) },
                new ManualWorldAnchor { Id="east",Position=new Vector3(84,0,48) } }).ToArray();
            SeasonalSampleBuilder.ConfigureRegions(world);
            var regionalProfiles=SeasonalSampleBuilder.BuildProfiles();
            // Paint a fresh sample source, then let Rivers create its reversible height copy from it.
            rivers.Clear();
            var regionalSource=SeasonalSampleBuilder.PaintCopy(world,regionalProfiles,Root+"/RegionalSourceTerrain.asset");
            world.Terrain.terrainData=regionalSource;world.Terrain.GetComponent<TerrainCollider>().terrainData=regionalSource;
            WaterStyleLibrary.Build();
            rivers.WaterMaterial=MakeRegionalWater();
            rivers.BankMaterial=MakeMaterial("RegionalBank",new Color(.38f,.29f,.16f),false);
            var paths=world.gameObject.AddComponent<TerraLoomPaths>(); paths.World=world; paths.AutomaticNetwork=false;
            paths.Connections.Add(new PathConnectionSettings { Id="settlement-road",StartId="west",EndId="east" });
            paths.GroundMaterial=MakeMaterial("Gravel",new Color(.55f,.42f,.26f),false);
            paths.BridgeMaterial=MakeMaterial("Bridge",new Color(.29f,.16f,.065f),true);
            paths.BridgeClearance=1.2f; paths.MaximumSlope=.35f;
            var integration=world.gameObject.AddComponent<TerraLoomIntegration>(); integration.World=world; integration.Paths=paths; integration.Rivers=rivers;
            if (!integration.Generate()) throw new InvalidOperationException(integration.Diagnostics);
            if (!paths.LastPlan.Routes.Any(r=>r.Surfaces.Contains(TerraLoom.Paths.PathSurface.Bridge))) throw new InvalidOperationException("Integration sample must contain an actual negotiated bridge.");
            RiversSampleBuilder.Bake(rivers,Root+"/River");
            PathsSampleBuilder.Bake(paths,Root+"/Paths");
            SeasonalSampleBuilder.AddDecoration(world,regionalProfiles,new[] { new Rect(36,0,24,96),new Rect(0,32,96,32) });
            SeasonalSampleBuilder.AddLabels(world);
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.55f,.61f,.66f);
            var camera=UnityEngine.Object.FindFirstObjectByType<Camera>(); camera.transform.position=new Vector3(109,84,-28); camera.transform.LookAt(new Vector3(48,5,48));
            camera.backgroundColor=new Color(.58f,.72f,.8f);
            var driver=world.gameObject.AddComponent<IntegrationTestDriver>();driver.Composition=integration;driver.View=camera;
            File.WriteAllText(Root+"/PathsPlan.json",paths.PlanJson); File.WriteAllText(Root+"/RiversPlan.json",rivers.PlanJson);
            File.WriteAllText(Root+"/PROVENANCE.txt","TerraLoom original analytic terrain, textures, tree/grass/rock meshes, river water shader and gravel/wood recipes. Generated from the three local source packages. No third-party art.\n");
            AssetDatabase.Refresh(); AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(world.gameObject.scene,Root+"/TerraLoomIntegration.unity");
            Debug.Log(integration.Diagnostics);
            BuildRuntimeScene();
            BuildLandscapeScene();
            BuildBrushBatch();
            EditorSceneManager.OpenScene(Root+"/TerraLoomLandscape.unity");
        }
        /// <summary>Configuration-only scene: contains no terrain or generated mesh. Start builds everything.</summary>
        public static void BuildRuntimeScene()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var host=new GameObject("TerraLoom Dynamic World");TerraLoomEditorIcons.Apply(host,"Core");
            var world=host.AddComponent<TerraLoomWorld>();world.Recipe=TerrainRecipe.DrainageValley;world.Seed=2042;
            world.GeneratedTerrainMaterial=AssetDatabase.LoadAssetAtPath<Material>(RiversSampleBuilder.SampleDirectory+"/Terrain.mat");
            if(!world.GeneratedTerrainMaterial)throw new InvalidOperationException("Build the original river sample terrain material first.");
            var riverHost=new GameObject("Rivers (optional module)");riverHost.transform.SetParent(host.transform,false);TerraLoomEditorIcons.Apply(riverHost,"Rivers");
            var rivers=riverHost.AddComponent<TerraLoomRivers>();rivers.World=world;rivers.WaterInset=0;
            rivers.WaterMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/RegionalWater.mat");
            rivers.BankMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/RegionalBank.mat");
            rivers.BedMaterial=AssetDatabase.LoadAssetAtPath<Material>(RiversSampleBuilder.SampleDirectory+"/Bed.mat");
            var pathHost=new GameObject("Paths (optional module)");pathHost.transform.SetParent(host.transform,false);TerraLoomEditorIcons.Apply(pathHost,"Paths");
            var paths=pathHost.AddComponent<TerraLoomPaths>();paths.World=world;paths.BridgeClearance=1.2f;paths.MaximumSlope=.35f;
            paths.GroundMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Gravel.mat");paths.BridgeMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Bridge.mat");
            var composition=host.AddComponent<TerraLoomIntegration>();composition.World=world;composition.Paths=paths;composition.Rivers=rivers;
            var decoration=host.AddComponent<RegionDecoration>();decoration.World=world;decoration.Profiles=SeasonalSampleBuilder.BuildProfiles();
            var runtime=host.AddComponent<SeededWorldDemo>();runtime.Composition=composition;runtime.Decoration=decoration;runtime.Profiles=decoration.Profiles;
            host.AddComponent<RegionalShowcaseGuide>().World=world;
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=2;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(45,-35,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.61f,.66f);
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(109,84,-28);camera.transform.LookAt(new Vector3(48,5,48));
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.58f,.72f,.8f);camera.farClipPlane=500;
            var driver=host.AddComponent<IntegrationTestDriver>();driver.Composition=composition;driver.View=camera;
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,Root+"/TerraLoomDynamic.unity");
            Debug.Log("Configuration-only dynamic scene saved; all world geometry is generated at runtime from the seed.");
        }
        private static Material MakeMaterial(string name,Color tint,bool wood)
        {
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,true) { name=name+" original texture",wrapMode=TextureWrapMode.Repeat };
            var colors=new Color[4096];
            for (int y=0;y<64;y++) for(int x=0;x<64;x++)
            {
                float n=((x*73+y*97+x*y*11)%101)/100f;
                float value=wood?(y%16==0?.38f:.8f+.2f*n):.65f+.35f*n;
                colors[y*64+x]=new Color(value,value,value,1);
            }
            texture.SetPixels(colors);texture.Apply(); texture=Save(texture,Root+"/"+name+"Texture.asset");
            var mat=new Material(Shader.Find("TerraLoom/SeasonalSurface")) { name=name };
            mat.SetColor("_BaseColor",tint);mat.SetTexture("_BaseMap",texture);mat.SetFloat("_Smoothness",.07f);
            mat.SetTextureScale("_BaseMap",wood?new Vector2(.25f,.8f):new Vector2(.6f,.6f));
            mat.SetFloat("_UseWorldRegions",1);mat.SetFloat("_WinterBoundaryZ",48);mat.SetFloat("_RegionBlend",6);
            return Save(mat,Root+"/"+name+".mat");
        }
        private static Material MakeTimberWood()
        {
            const int size=256;var texture=new Texture2D(size,size,TextureFormat.RGBA32,true)
            {name="Original weathered timber grain",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=8};
            var colors=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(float)x/size,v=(float)y/size;
                float warped=v+.006f*Mathf.Sin(u*Mathf.PI*4)+.003f*Mathf.Sin(u*Mathf.PI*14+v*Mathf.PI*2);
                float grain=.5f+.5f*Mathf.Sin(warped*Mathf.PI*96);
                uint hash=unchecked((uint)(x*374761393+y*668265263));hash=(hash^(hash>>13))*1274126177u;
                float fine=(hash&65535)/65535f;
                float value=.55f+.13f*grain+.10f*fine+.12f*Mathf.Sin(v*Mathf.PI*6);
                // Three subdued periodic knots; no alternating two-colour board pattern.
                foreach(var knot in new[]{new Vector2(.22f,.19f),new Vector2(.73f,.61f),new Vector2(.48f,.87f)})
                {
                    float dx=Mathf.Min(Mathf.Abs(u-knot.x),1-Mathf.Abs(u-knot.x));
                    float dy=Mathf.Min(Mathf.Abs(v-knot.y),1-Mathf.Abs(v-knot.y));
                    float radius=Mathf.Sqrt(dx*dx*100+dy*dy*1600);
                    value-=.19f*Mathf.Exp(-radius*radius*2)*( .75f+.25f*Mathf.Cos(radius*20));
                }
                colors[y*size+x]=Color.Lerp(new Color(.19f,.095f,.04f),new Color(.52f,.34f,.17f),Mathf.Clamp01(value));
            }
            texture.SetPixels(colors);texture.Apply();texture=Save(texture,Root+"/TimberWoodTexture.asset");
            var material=new Material(Shader.Find("TerraLoom/SeasonalSurface")){name="Original short timber wood"};
            material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",texture);material.SetTextureScale("_BaseMap",new Vector2(.5f,2));
            material.SetFloat("_Smoothness",.12f);material.SetFloat("_UseWorldRegions",0);
            return Save(material,Root+"/TimberWood.mat");
        }
        /// <summary>Independent configuration-only landscape; keeps the regional comparison gallery intact.</summary>
        public static void BuildLandscapeScene()
        {
            EditorSceneManager.OpenScene(Root+"/TerraLoomDynamic.unity");
            var recipe=UnityEngine.Object.FindFirstObjectByType<SeededWorldDemo>();recipe.LandscapeMode=true;
            // Curved overlapping crossing windows do not yet cover widened wet bank shoulders.
            // Keep the existing landscape recipe explicit; failed inset requests never fall back at runtime.
            recipe.Composition.Rivers.WaterInset=0;
            Directory.CreateDirectory(Root+"/LandscapeProfiles");AssetDatabase.Refresh();
            recipe.Profiles=recipe.Profiles.Select(source=>{
                var profile=UnityEngine.Object.Instantiate(source);profile.name=source.name+" habitat";
                profile.UseHabitat=true;profile.Density=.9f;profile.SlopeRange=new Vector2(0,32);
                profile.ClusterScale=22;profile.ClusterStrength=.65f;
                return Save(profile,Root+"/LandscapeProfiles/"+source.RegionId+".asset");
            }).ToArray();
            recipe.Decoration.Profiles=recipe.Profiles;
            var sun=UnityEngine.Object.FindFirstObjectByType<Light>();sun.intensity=1.3f;
            recipe.GetComponent<RegionalShowcaseGuide>().enabled=false;
            var camera=Camera.main;camera.transform.position=new Vector3(111,60,-16);camera.transform.LookAt(new Vector3(48,5,48));
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(recipe.gameObject.scene,Root+"/TerraLoomLandscape.unity");
            Debug.Log("Seeded landscape configuration saved: connected road bends/branch and habitat profiles. Visual acceptance remains explicit.");
        }
        private static Material MakeRegionalWater()
        {
            var source=AssetDatabase.LoadAssetAtPath<Material>(WaterStyleLibrary.Root+"/GlacialRiver.mat");
            if(!source)throw new InvalidOperationException("Missing original water style.");
            var material=new Material(source) { name="Summer current to winter ice" };
            material.SetFloat("_UseWorldRegions",1);material.SetFloat("_WinterBoundaryZ",48);material.SetFloat("_RegionBlend",8);
            return Save(material,Root+"/RegionalWater.mat");
        }
        private static T Save<T>(T value,string path) where T:UnityEngine.Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if (old) { EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old; }
            AssetDatabase.CreateAsset(value,path);return value;
        }
    }
}
