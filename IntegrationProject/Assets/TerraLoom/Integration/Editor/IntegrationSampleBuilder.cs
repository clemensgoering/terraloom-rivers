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
            world.Areas=new[] { new LandscapeArea { Id="wet-bank",Center=new Vector2(48,48),Size=new Vector2(16,96),Feather=2,Tags=new[] { "wet","riparian" } } };
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
            ScatterNature(world);
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.55f,.61f,.66f);
            var camera=UnityEngine.Object.FindFirstObjectByType<Camera>(); camera.transform.position=new Vector3(109,84,-28); camera.transform.LookAt(new Vector3(48,5,48));
            camera.backgroundColor=new Color(.58f,.72f,.8f);
            var driver=world.gameObject.AddComponent<IntegrationTestDriver>();driver.Composition=integration;driver.View=camera;
            File.WriteAllText(Root+"/PathsPlan.json",paths.PlanJson); File.WriteAllText(Root+"/RiversPlan.json",rivers.PlanJson);
            File.WriteAllText(Root+"/PROVENANCE.txt","TerraLoom original analytic terrain, textures, tree/grass/rock meshes, river water shader and gravel/wood recipes. Generated from the three local source packages. No third-party art.\n");
            AssetDatabase.Refresh(); AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(world.gameObject.scene,Root+"/TerraLoomIntegration.unity");
            Debug.Log(integration.Diagnostics);
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
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")) { name=name };
            mat.SetColor("_BaseColor",tint);mat.SetTexture("_BaseMap",texture);mat.SetFloat("_Smoothness",.07f);
            mat.SetTextureScale("_BaseMap",wood?new Vector2(.25f,.8f):new Vector2(.6f,.6f));
            return Save(mat,Root+"/"+name+".mat");
        }
        private static T Save<T>(T value,string path) where T:UnityEngine.Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if (old) { EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old; }
            AssetDatabase.CreateAsset(value,path);return value;
        }
        private static void ScatterNature(TerraLoomWorld world)
        {
            string[] names={"Tree","Rock","Grass"};
            for(int i=0;i<90;i++)
            {
                float z=10+(i*17%76),x=(i%2==0?34:61)+(i*7%9);
                if (Mathf.Abs(z-48)<7) continue;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TerraLoom/CoreSamples/Baked/"+names[i%9==0?0:i%5==0?1:2]+".prefab");
                if (!prefab) throw new InvalidOperationException("Missing own Core nature prefab.");
                var item=(GameObject)PrefabUtility.InstantiatePrefab(prefab,world.gameObject.scene); item.transform.SetParent(world.transform,true);
                item.transform.position=new Vector3(x,world.Terrain.SampleHeight(new Vector3(x,0,z))+world.Terrain.transform.position.y,z);
                item.transform.rotation=Quaternion.Euler(0,i*137.5f,0);item.transform.localScale=Vector3.one*.7f;
            }
        }
    }
}
