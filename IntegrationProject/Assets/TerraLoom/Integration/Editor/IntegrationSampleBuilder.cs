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
