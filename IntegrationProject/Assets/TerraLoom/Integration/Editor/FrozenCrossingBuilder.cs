using System;
using System.IO;
using TerraLoom.Core.Unity;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TerraLoom.Integration.Editor
{
    /// <summary>Exact frozen seed42 landscape/anchors/protection, with an independently bounded long construction.</summary>
    public static class FrozenCrossingBuilder
    {
        [MenuItem("Tools/TerraLoom/Integration/Build Frozen Curved Truss Crossing")]
        public static void BuildBatch()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string root=IntegrationSampleBuilder.Root;Directory.CreateDirectory(root);AssetDatabase.Refresh();
            var data=new TerrainData{heightmapResolution=129,alphamapResolution=128,size=new Vector3(96,16,96)};
            var heights=new float[129,129];
            for(int z=0;z<129;z++)for(int x=0;x<129;x++)
            {
                float px=96f*x/128,pz=96f*z/128;
                float shoulder=Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Abs(px-48)-22)/20));
                heights[z,x]=(6-.03f*pz+shoulder*(5+3*Mathf.Pow(Mathf.Sin(pz*.055f+.4f),2)))/16;
            }
            data.SetHeights(0,0,heights);
            var layers=new TerrainLayer[3];
            var colors=new[]{new Color(.31f,.39f,.2f),new Color(.37f,.29f,.19f),new Color(.44f,.43f,.4f)};
            for(int i=0;i<3;i++)
            {
                // Terrain/Lit does not inherit the tint of a separate mesh material. Bake
                // original coloured pixels, with low smoothness and broad organic variation.
                const int n=256;var texture=new Texture2D(n,n,TextureFormat.RGBA32,true){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=8};
                var pixels=new Color[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {
                    float u=(float)x/n,v=(float)y/n;
                    // Periodic trigonometric warping gives a seamless low-frequency base;
                    // small deterministic grain avoids the old 64px repeating cell motif.
                    float wave=.5f+.18f*Mathf.Sin(u*Mathf.PI*2+.8f*Mathf.Sin(v*Mathf.PI*4))+.12f*Mathf.Cos(v*Mathf.PI*6+.5f*Mathf.Sin(u*Mathf.PI*4));
                    uint hash=unchecked((uint)(x*374761393+y*668265263+i*7919));hash=(hash^(hash>>13))*1274126177u;
                    float detail=(hash&65535)/65535f;
                    var color=colors[i]*(.72f+.4f*wave+.12f*detail);color.a=.08f;pixels[y*n+x]=color;
                }
                texture.SetPixels(pixels);texture.Apply();texture=Save(texture,root+"/FrozenGroundTexture"+i+".asset");
                layers[i]=Save(new TerrainLayer{diffuseTexture=texture,tileSize=new Vector2(8,8),smoothness=.08f,metallic=0},root+"/FrozenLayer"+i+".terrainlayer");
            }
            data.terrainLayers=layers;var alpha=new float[128,128,3];for(int z=0;z<128;z++)for(int x=0;x<128;x++)alpha[z,x,0]=1;data.SetAlphamaps(0,0,alpha);
            data=Save(data,root+"/FrozenSourceTerrain.asset");
            var terrain=Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();
            terrain.materialTemplate=Save(new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")),root+"/FrozenTerrain.mat");
            var host=new GameObject("TerraLoom Frozen Curved Crossing");var world=host.AddComponent<TerraLoomWorld>();
            world.AutoGenerateOnStart=false;world.WorldId="curved-brush-repro";world.Seed=42;world.Terrain=terrain;
            world.Areas=new[]{new LandscapeArea{Id="river-landscape",Center=new Vector2(48,48),Size=new Vector2(96,96),Tags=new[]{"sample","landscape"}}};
            world.ManualAnchors=new[]{new ManualWorldAnchor{Id="source",Position=new Vector3(48,0,8)},new ManualWorldAnchor{Id="mouth",Position=new Vector3(48,0,88)},new ManualWorldAnchor{Id="west",Position=new Vector3(12,0,24)},new ManualWorldAnchor{Id="east",Position=new Vector3(84,0,24)}};
            var paths=new GameObject("Paths - independent 6 to 12m side truss").AddComponent<TerraLoomPaths>();paths.transform.SetParent(host.transform);
            paths.World=world;paths.GenerateOnStart=false;paths.AutomaticNetwork=false;paths.MaximumSlope=.35f;paths.BridgeClearance=1.2f;paths.TimberTrussBridge=true;
            paths.Connections.Add(new PathConnectionSettings{Id="brush-crossing",StartId="west",EndId="east"});
            paths.GroundMaterial=IntegrationSampleBuilder.MakeMaterial("FrozenGravel",new Color(.48f,.4f,.29f),false);paths.GroundMaterial.SetFloat("_UseWorldRegions",0);
            paths.BridgeMaterial=IntegrationSampleBuilder.MakeTimberWood("TrussWood");
            var rivers=new GameObject("Rivers - carved terrain and inset water").AddComponent<TerraLoomRivers>();rivers.transform.SetParent(host.transform);
            rivers.World=world;rivers.GenerateOnStart=false;rivers.AutomaticSourceAndMouth=false;rivers.TerrainBrush=true;rivers.WaterInset=.6f;rivers.SedimentTerrainLayer=1;
            rivers.BedMaterial=null;rivers.BankMaterial=null;
            rivers.Connections.Add(new RiverConnectionSettings{Id="sample-river",SourceId="source",MouthId="mouth"});
            rivers.ProtectedAreas.Add(new RiverProtectionSettings{Id="sample-detour",Center=new Vector2(48,48),Size=new Vector2(8,12)});
            rivers.WaterMaterial=Save(new Material(Shader.Find("TerraLoom/RiverWater")),root+"/FrozenWater.mat");
            var composition=host.AddComponent<TerraLoomIntegration>();composition.World=world;composition.Paths=paths;composition.Rivers=rivers;
            var authority=host.AddComponent<FrozenCurvedCrossing>();authority.Composition=composition;authority.GenerateOnStart=false;
            var evidence=host.AddComponent<FrozenCrossingEvidence>();evidence.Scenario=authority;
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.farClipPlane=500;camera.nearClipPlane=.08f;camera.backgroundColor=new Color(.58f,.7f,.77f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.transform.position=new Vector3(74,34,6);camera.transform.LookAt(new Vector3(48,5,24));
            var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(48,-28,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.6f,.65f);
            // Verify runtime recipe now; save configuration only to avoid serializing transient ownership.
            if(!authority.Generate())
            {
                if(composition.LastSharedInput!=null)foreach(var c in composition.LastSharedInput.Crossings)
                    Debug.Log("FROZEN_WINDOW "+c.Id+" kinds="+c.AllowedKinds+" X="+c.Bounds.MinX+".."+c.Bounds.MaxX+" Z="+c.Bounds.MinZ+".."+c.Bounds.MaxZ);
                throw new InvalidOperationException(composition.Diagnostics);
            }
            Debug.Log("FROZEN_TRUSS_EDITOR_SUCCESS "+authority.ProbeDiagnostic);authority.ClearProbe();composition.Clear();authority.GenerateOnStart=true;
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,root+"/TerraLoomFrozenTruss.unity");
        }
        private static T Save<T>(T value,string path) where T:UnityEngine.Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}
            AssetDatabase.CreateAsset(value,path);return value;
        }
    }
}
