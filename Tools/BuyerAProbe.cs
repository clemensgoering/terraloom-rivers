using System;
using System.Diagnostics;
using System.Linq;
using TerraLoom.Core.Unity;
using TerraLoom.Integration;
using TerraLoom.Integration.Editor;
using TerraLoom.Paths;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug=UnityEngine.Debug;

public static class BuyerAProbe
{
    public static void Run()
    {
        var elapsed=Stopwatch.StartNew();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        const string root="Assets/BuyerA";
        var ground=Material("Ground",new Color(.31f,.36f,.25f));
        var bridge=Material("Bridge",new Color(.32f,.19f,.08f));
        var water=Material("Water",new Color(.08f,.35f,.58f));
        var layers=new[] {Layer("Meadow",new Color(.35f,.40f,.28f)),Layer("Silt",new Color(.48f,.39f,.30f))};
        var data=new TerrainData{heightmapResolution=129,size=new Vector3(96,16,96)};
        var heights=new float[129,129];
        for(int z=0;z<129;z++)for(int x=0;x<129;x++)heights[z,x]=(6-.03f*(96f*z/128))/16;
        data.SetHeights(0,0,heights);data.terrainLayers=layers;
        var tree=GameObject.CreatePrimitive(PrimitiveType.Capsule);tree.name="BuyerA Tree";
        var treeAsset=PrefabUtility.SaveAsPrefabAsset(tree,root+"/Tree.prefab");
        UnityEngine.Object.DestroyImmediate(tree);
        data.treePrototypes=new[]{new TreePrototype{prefab=treeAsset}};
        data.treeInstances=new[]{new TreeInstance {
            position=new Vector3(.5f,0,.5f),prototypeIndex=0,widthScale=1,heightScale=1,
            color=Color.white,lightmapColor=Color.white
        }};
        var grass=new Texture2D(2,2);grass.SetPixels(new[]{Color.green,Color.green,Color.green,Color.green});grass.Apply();
        AssetDatabase.CreateAsset(grass,root+"/Grass.asset");
        data.detailPrototypes=new[]{new DetailPrototype{
            prototypeTexture=grass,renderMode=DetailRenderMode.GrassBillboard,
            healthyColor=Color.green,dryColor=Color.yellow,minWidth=.4f,maxWidth=.6f,minHeight=.5f,maxHeight=.9f
        }};
        data.SetDetailResolution(128,8);
        var detail=new int[128,128];for(int z=0;z<128;z++)for(int x=0;x<128;x++)detail[z,x]=2;
        data.SetDetailLayer(0,0,0,detail);
        AssetDatabase.CreateAsset(data,root+"/ForeignTerrain.asset");
        var terrainObject=Terrain.CreateTerrainGameObject(data);terrainObject.name="BuyerA Supplied Terrain";
        var terrain=terrainObject.GetComponent<Terrain>();var collider=terrainObject.GetComponent<TerrainCollider>();
        var host=new GameObject("BuyerA TerraLoom Host");
        var world=host.AddComponent<TerraLoomWorld>();world.WorldId="buyer-a";world.Seed=42;world.Terrain=terrain;
        world.ManualAnchors=new[]{
            new ManualWorldAnchor{Id="source",Position=new Vector3(48,0,4)},
            new ManualWorldAnchor{Id="mouth",Position=new Vector3(48,0,92)},
            new ManualWorldAnchor{Id="west",Position=new Vector3(12,0,48)},
            new ManualWorldAnchor{Id="east",Position=new Vector3(84,0,48)}
        };
        var paths=host.AddComponent<TerraLoomPaths>();paths.World=world;paths.GroundMaterial=ground;paths.BridgeMaterial=bridge;
        paths.AutomaticNetwork=false;paths.MaximumSlope=.35f;paths.BridgeClearance=1.2f;
        paths.Connections.Add(new PathConnectionSettings{Id="buyer-road",StartId="west",EndId="east"});
        var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;rivers.WaterMaterial=water;
        rivers.AutomaticSourceAndMouth=false;rivers.TerrainBrush=true;rivers.WaterInset=.6f;rivers.SedimentTerrainLayer=1;
        rivers.Connections.Add(new RiverConnectionSettings{Id="buyer-river",SourceId="source",MouthId="mouth"});
        var composition=host.AddComponent<TerraLoomIntegration>();composition.World=world;composition.Paths=paths;composition.Rivers=rivers;
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),root+"/Authored.unity");
        Debug.Log("BUYER_A_SETUP_SECONDS "+elapsed.Elapsed.TotalSeconds.ToString("F2")+" script-authored; human clicks not measured");
        var originalHeights=data.GetHeights(0,0,129,129);
        IntegrationEditor.GenerateOnSuppliedTerrain(composition);
        Debug.Log("BUYER_A_GENERATE_SECONDS "+elapsed.Elapsed.TotalSeconds.ToString("F2")+" "+composition.Diagnostics);
        if(!paths.LastPlan.Routes.Any(r=>r.Surfaces.Contains(PathSurface.Bridge)))
            throw new InvalidOperationException("BUYER_A_BLOCKER no actual bridge in generated route.");
        if(terrain.terrainData==data||collider.terrainData!=terrain.terrainData||!data.GetHeights(0,0,129,129).Cast<float>().SequenceEqual(originalHeights.Cast<float>()))
            throw new InvalidOperationException("BUYER_A_BLOCKER source terrain changed or carved terrain/collider not bound together.");
        var published=terrain.terrainData;
        var treeAtRiver=published.treeInstances.Any(t=>NearRiver(new Vector2(t.position.x*96,t.position.z*96),rivers,3.5f));
        var centerDetails=published.GetDetailLayer(62,62,4,4,0).Cast<int>().Sum();
        Debug.Log("BUYER_A_VEGETATION originalTrees="+data.treeInstanceCount+" publishedTrees="+published.treeInstanceCount+
            " treeInRiverCorridor="+treeAtRiver+" detailsCenter4x4="+centerDetails+
            " bridgeCount="+paths.LastPlan.Routes.Sum(r=>r.Surfaces.Count(s=>s==PathSurface.Bridge)));
        if(treeAtRiver||centerDetails>0)
            throw new InvalidOperationException("BUYER_A_BLOCKER existing Terrain TreeInstances/details remain in the generated river/road corridor.");
        Debug.Log("BUYER_A_READY_FOR_PLAYER seconds="+elapsed.Elapsed.TotalSeconds.ToString("F2"));
    }

    private static bool NearRiver(Vector2 point,TerraLoomRivers rivers,float radius)
    {
        foreach(var route in rivers.LastPlan.Routes)
            for(int i=1;i<route.WaterPolyline.Count;i++)
            {
                var a=new Vector2((float)route.WaterPolyline[i-1].X,(float)route.WaterPolyline[i-1].Z);
                var b=new Vector2((float)route.WaterPolyline[i].X,(float)route.WaterPolyline[i].Z);
                var ab=b-a;var t=Mathf.Clamp01(Vector2.Dot(point-a,ab)/Mathf.Max(ab.sqrMagnitude,1e-6f));
                if(Vector2.Distance(point,a+t*ab)<radius)return true;
            }
        return false;
    }
    private static Material Material(string name,Color color)
    {
        var shader=Shader.Find("Universal Render Pipeline/Lit");
        if(!shader)throw new InvalidOperationException("URP/Lit shader missing.");
        var value=new Material(shader){name=name};value.SetColor("_BaseColor",color);
        AssetDatabase.CreateAsset(value,"Assets/BuyerA/"+name+".mat");return value;
    }
    private static TerrainLayer Layer(string name,Color color)
    {
        var texture=new Texture2D(2,2);texture.SetPixels(new[]{color,color,color,color});texture.Apply();
        AssetDatabase.CreateAsset(texture,"Assets/BuyerA/"+name+"Texture.asset");
        var layer=new TerrainLayer{diffuseTexture=texture,tileSize=new Vector2(8,8)};
        AssetDatabase.CreateAsset(layer,"Assets/BuyerA/"+name+".terrainlayer");return layer;
    }
}
