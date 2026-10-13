using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using TerraLoom.Core.Unity;
using TerraLoom.Core;
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
    public static void Run()=>Run(false,false,false);
    public static void BuildPlayer()=>Run(true,false,false);
    public static void RunStructural()=>Run(false,true,false);
    public static void BuildStructuralPlayer()=>Run(true,true,false);
    public static void RunAuthorizedTruss()=>Run(false,true,true);
    public static void BuildAuthorizedTrussPlayer()=>Run(true,true,true);
    private static void Run(bool buildPlayer,bool structural,bool positive)
    {
        var elapsed=Stopwatch.StartNew();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        string root=positive?"Assets/BuyerA-AuthorizedTruss":"Assets/BuyerA";
        if(AssetDatabase.IsValidFolder(root))AssetDatabase.DeleteAsset(root);
        AssetDatabase.CreateFolder("Assets",positive?"BuyerA-AuthorizedTruss":"BuyerA");
        var ground=Material(root,"Ground",new Color(.31f,.36f,.25f));
        var bridge=Material(root,"Bridge",new Color(.32f,.19f,.08f));
        var water=Material(root,"Water",new Color(.08f,.35f,.58f));
        var layers=new[] {Layer(root,"Meadow",new Color(.35f,.40f,.28f)),Layer(root,"Silt",new Color(.48f,.39f,.30f))};
        var data=new TerrainData{heightmapResolution=129,size=new Vector3(96,16,96)};
        var heights=new float[129,129];
        for(int z=0;z<129;z++)for(int x=0;x<129;x++)heights[z,x]=(6-.03f*(96f*z/128))/16;
        data.SetHeights(0,0,heights);data.terrainLayers=layers;
        var tree=GameObject.CreatePrimitive(PrimitiveType.Capsule);tree.name="BuyerA Tree";
        var treeAsset=PrefabUtility.SaveAsPrefabAsset(tree,root+"/Tree.prefab");
        UnityEngine.Object.DestroyImmediate(tree);
        data.treePrototypes=new[]{new TreePrototype{prefab=treeAsset}};
        data.treeInstances=new[]{
            Tree(.5f,.5f), // channel
            Tree(.25f,.5625f), // road approach, on the final curved route
            Tree(.1f,.1f)  // adjacent vegetation must survive
        };
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
        var world=host.AddComponent<TerraLoomWorld>();world.WorldId=positive?"buyer-a-authorized-truss":"buyer-a";world.Seed=42;world.Terrain=terrain;
        world.ManualAnchors=new[]{
            new ManualWorldAnchor{Id="source",Position=new Vector3(48,0,4)},
            new ManualWorldAnchor{Id="mouth",Position=new Vector3(48,0,92)},
            new ManualWorldAnchor{Id="west",Position=positive?new Vector3(24,0,57):new Vector3(12,0,48)},
            new ManualWorldAnchor{Id="east",Position=positive?new Vector3(72,0,57):new Vector3(84,0,48)}
        };
        var paths=host.AddComponent<TerraLoomPaths>();paths.World=world;paths.GroundMaterial=ground;paths.BridgeMaterial=bridge;
        paths.AutomaticNetwork=false;paths.MaximumSlope=.35f;paths.BridgeClearance=1.2f;
        if(structural)paths.TimberTrussBridge=true;
        paths.Connections.Add(new PathConnectionSettings{Id="buyer-road",StartId="west",EndId="east"});
        var rivers=host.AddComponent<TerraLoomRivers>();rivers.World=world;rivers.WaterMaterial=water;
        rivers.AutomaticSourceAndMouth=false;rivers.TerrainBrush=true;rivers.WaterInset=.6f;rivers.SedimentTerrainLayer=1;
        rivers.Connections.Add(new RiverConnectionSettings{Id="buyer-river",SourceId="source",MouthId="mouth"});
        var composition=host.AddComponent<TerraLoomIntegration>();composition.World=world;composition.Paths=paths;composition.Rivers=rivers;
        if(structural)composition.CrossingPermissions=positive?AuthorizePositiveCrossing:AuthorizeBuyerCrossing;
        if(positive){composition.RestrictPathRouting=true;composition.PathRoutingZone=new Rect(16,54,64,10);}
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),root+"/Authored.unity");
        Debug.Log("BUYER_A_SETUP_SECONDS "+elapsed.Elapsed.TotalSeconds.ToString("F2")+" script-authored; human clicks not measured");
        var originalHeights=data.GetHeights(0,0,129,129);
        var originalTrees=data.treeInstances.Select(t=>t.position).ToArray();
        var originalDetails=data.GetDetailLayer(0,0,128,128,0);
        try{IntegrationEditor.GenerateOnSuppliedTerrain(composition);}
        catch(Exception ex)
        {
            if(structural)
            {
                var shared=composition.LastSharedInput;
                var attempt=paths.LastAttemptPlan;
                Debug.LogError((positive?"BUYER_A_POSITIVE_REJECTED":"BUYER_A_STRUCTURAL_REJECTED")+" step="+composition.ActiveStep+" "+ex.Message+
                    " candidates="+(shared?.Crossings.Count??0)+" grants="+(shared==null?0:composition.CrossingPermissions(shared).Grants.Count)+
                    " bridgeSurfaces="+(attempt?.Routes.Sum(r=>r.Surfaces.Count(s=>s==PathSurface.Bridge))??0)+
                    " boundCrossings="+(attempt?.Routes.Sum(r=>r.BridgeCrossings.Count)??0)+
                    " bridgeSegments="+(attempt==null?"none":string.Join(";",attempt.Routes.SelectMany(r=>
                        Enumerable.Range(0,r.Surfaces.Count).Where(i=>r.Surfaces[i]==PathSurface.Bridge)
                            .Select(i=>r.Waypoints[i].X.ToString("F1")+","+r.Waypoints[i].Z.ToString("F1")+
                                "->"+r.Waypoints[i+1].X.ToString("F1")+","+r.Waypoints[i+1].Z.ToString("F1"))))));
            }
            throw;
        }
        Debug.Log("BUYER_A_GENERATE_SECONDS "+elapsed.Elapsed.TotalSeconds.ToString("F2")+" "+composition.Diagnostics);
        if(structural)
        {
            int bound=paths.LastPlan.Routes.Sum(r=>r.BridgeCrossings.Count);
            var geometry=paths.GeneratedRoot.GetComponentsInChildren<PathGeneratedGeometry>(true);
            int body=geometry.Count(g=>g.Role==PathGeometryRole.BridgeStructure);
            int ramps=geometry.Count(g=>g.Role==PathGeometryRole.BridgeRamp);
            Debug.Log((positive?"BUYER_A_POSITIVE_STRUCTURE":"BUYER_A_STRUCTURAL")+" geometryBody="+body+" ramps="+ramps+" boundCrossings="+bound);
            if(bound==0||body==0||ramps<2||geometry.Where(g=>g.Role==PathGeometryRole.BridgeStructure||g.Role==PathGeometryRole.BridgeRamp)
                .Any(g=>!g.GetComponent<MeshCollider>()||!g.GetComponent<MeshCollider>().sharedMesh))
                throw new InvalidOperationException("BUYER_A_STRUCTURAL_BLOCKER complete collider-backed timber bridge not published.");
            if(positive)
            {
                var binding=paths.LastPlan.Routes.Single().BridgeCrossings.Single();
                var footprint=binding.Footprint(paths.Width+2*paths.CaptureProfile().BridgeLateralPadding);
                var zone=new WorldBounds(24,54,64,64);
                bool current=composition.ValidateCurrent(out var validity);
                if(!zone.Contains(footprint)||!current)
                    throw new InvalidOperationException("BUYER_A_POSITIVE_BLOCKER published truss exceeds granted zone or is stale: "+validity);
                Debug.Log("BUYER_A_POSITIVE_AUTH grants="+AuthorizePositiveCrossing(composition.LastSharedInput).Grants.Count+
                    " span="+Math.Abs(binding.End.X-binding.Start.X)+" footprint=["+footprint.MinX+","+footprint.MinZ+","+
                    footprint.MaxX+","+footprint.MaxZ+"]");
            }
        }
        if(!paths.LastPlan.Routes.Any(r=>r.Surfaces.Contains(PathSurface.Bridge)))
            throw new InvalidOperationException("BUYER_A_BLOCKER no actual bridge in generated route.");
        if(terrain.terrainData==data||collider.terrainData!=terrain.terrainData||!data.GetHeights(0,0,129,129).Cast<float>().SequenceEqual(originalHeights.Cast<float>()))
            throw new InvalidOperationException("BUYER_A_BLOCKER source terrain changed or carved terrain/collider not bound together.");
        var published=terrain.terrainData;
        var treeAtRiver=published.treeInstances.Any(t=>NearRiver(new Vector2(t.position.x*96,t.position.z*96),rivers,3.5f));
        var roadTree=published.treeInstances.Any(t=>Mathf.Abs(t.position.x-.25f)<.001f&&Mathf.Abs(t.position.z-.5625f)<.001f);
        var adjacentTree=published.treeInstances.Any(t=>Mathf.Abs(t.position.x-.1f)<.001f&&Mathf.Abs(t.position.z-.1f)<.001f);
        var centerDetails=published.GetDetailLayer(62,62,4,4,0).Cast<int>().Sum();
        var roadDetails=published.GetDetailLayer(32,72,1,1,0)[0,0];
        var adjacentDetails=published.GetDetailLayer(2,2,4,4,0).Cast<int>().Sum();
        Debug.Log("BUYER_A_VEGETATION originalTrees="+data.treeInstanceCount+" publishedTrees="+published.treeInstanceCount+
            " treeInRiverCorridor="+treeAtRiver+" roadTree="+roadTree+" adjacentTree="+adjacentTree+
            " detailsCenter4x4="+centerDetails+" roadCenterCell="+roadDetails+" adjacentDetails4x4="+adjacentDetails+
            " bridgeCount="+paths.LastPlan.Routes.Sum(r=>r.Surfaces.Count(s=>s==PathSurface.Bridge)));
        if(treeAtRiver||centerDetails>0||!adjacentTree||adjacentDetails!=32||
            (!structural&&(roadTree||roadDetails>0))||
            !data.treeInstances.Select(t=>t.position).SequenceEqual(originalTrees)||
            !data.GetDetailLayer(0,0,128,128,0).Cast<int>().SequenceEqual(originalDetails.Cast<int>()))
            throw new InvalidOperationException("BUYER_A_BLOCKER existing Terrain TreeInstances/details remain in the generated river/road corridor.");
        if(buildPlayer)
        {
            var baked=UnityEngine.Object.Instantiate(published);baked.hideFlags=HideFlags.None;baked.name="BuyerA Carved Terrain";
            AssetDatabase.CreateAsset(baked,root+"/CarvedTerrain.asset");terrain.terrainData=baked;collider.terrainData=baked;
            AssetDatabase.CreateFolder(root,"Meshes");int meshIndex=0;
            foreach(var filter in host.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh)continue;
                var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.hideFlags=HideFlags.None;
                AssetDatabase.CreateAsset(mesh,root+"/Meshes/Generated"+(meshIndex++)+".asset");filter.sharedMesh=mesh;
                var meshCollider=filter.GetComponent<MeshCollider>();if(meshCollider)meshCollider.sharedMesh=mesh;
            }
            var lightObject=new GameObject("BuyerA Sun");var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;
            light.intensity=1.3f;lightObject.transform.rotation=Quaternion.Euler(45,25,0);
            var cameraObject=new GameObject("BuyerA Player Camera");var camera=cameraObject.AddComponent<Camera>();
            cameraObject.tag="MainCamera";camera.fieldOfView=65;camera.farClipPlane=300;
            var capture=cameraObject.AddComponent<BuyerAPlayerCapture>();
            var route=paths.LastPlan.Routes.Single();
            int bridgeSegment=route.Surfaces.ToList().IndexOf(PathSurface.Bridge);
            Vector3 Point(int i){var p=route.Waypoints[i];return new Vector3((float)p.X,(float)p.Y+paths.SurfaceOffset,(float)p.Z);}
            capture.Route=Enumerable.Range(0,route.Waypoints.Count).Select(Point).ToArray();
            capture.BridgeStart=Point(bridgeSegment);capture.BridgeEnd=Point(bridgeSegment+1);
            capture.ValidateWalkability=positive;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),root+"/Player.unity");AssetDatabase.SaveAssets();
            var output=Path.GetFullPath(positive?".artifacts/BuyerA-20261013/AuthorizedTrussPlayer/BuyerA.exe":".artifacts/BuyerA-20261013/Player/BuyerA.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{root+"/Player.unity"},
                locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("BUYER_A_BLOCKER Player build: "+report.summary.result);
            Debug.Log("BUYER_A_PLAYER_BUILT "+output);
        }
        else
        {
            composition.Clear();
            if(terrain.terrainData!=data||collider.terrainData!=data)
                throw new InvalidOperationException("BUYER_A_BLOCKER clearing did not restore the original terrain binding.");
        }
        Debug.Log((positive?"BUYER_A_POSITIVE_READY_FOR_PLAYER":"BUYER_A_READY_FOR_PLAYER")+" seconds="+elapsed.Elapsed.TotalSeconds.ToString("F2"));
    }

    private static TreeInstance Tree(float x,float z)=>new TreeInstance{
        position=new Vector3(x,0,z),prototypeIndex=0,widthScale=1,heightScale=1,
        color=Color.white,lightmapColor=Color.white};

    private static PathCrossingPermissions AuthorizeBuyerCrossing(PlanSnapshot snapshot)
    {
        var zone=new WorldBounds(24,54,64,64);
        var grants=snapshot.Crossings.Where(c=>(c.AllowedKinds&CrossingKind.Bridge)!=0&&c.Bounds.Overlaps(zone))
            .Select(c=>
            {
                var b=c.Bounds;
                var area=new WorldBounds(Math.Max(b.MinX,zone.MinX),Math.Max(b.MinZ,zone.MinZ),
                    Math.Min(b.MaxX,zone.MaxX),Math.Min(b.MaxZ,zone.MaxZ));
                return new CollectiveCrossingAuthorization("bridge:"+c.Id+":0",snapshot.Identity,
                    new[]{new CrossingSectionPermission(c.WaterId,c.Id,area)});
            });
        return new PathCrossingPermissions(grants);
    }
    private static PathCrossingPermissions AuthorizePositiveCrossing(PlanSnapshot snapshot)
    {
        var zone=new WorldBounds(24,54,64,64);
        double half=1+.3; // unchanged 2m walking width plus TimberTruss lateral space
        var sections=snapshot.Crossings.Where(c=>(c.AllowedKinds&CrossingKind.Bridge)!=0&&c.Bounds.Overlaps(zone))
            .Select(c=>new CrossingSectionPermission(c.WaterId,c.Id,new WorldBounds(
                Math.Max(c.Bounds.MinX,zone.MinX),Math.Max(c.Bounds.MinZ,zone.MinZ),
                Math.Min(c.Bounds.MaxX,zone.MaxX),Math.Min(c.Bounds.MaxZ,zone.MaxZ)))).ToArray();
        var grants=snapshot.Crossings.Where(c=>sections.Any(s=>s.CandidateId==c.Id)).Select(c=>
        {
            double z=(c.Bounds.MinZ+c.Bounds.MaxZ)*.5;
            if(z-half<zone.MinZ||z+half>zone.MaxZ)return null;
            var strip=new WorldBounds(zone.MinX,z-half,zone.MaxX,z+half);
            var selected=sections.Where(s=>s.Area.Overlaps(strip)).ToArray();
            return new CollectiveCrossingAuthorization("bridge:"+c.Id+":0",snapshot.Identity,selected);
        }).Where(g=>g!=null);
        return new PathCrossingPermissions(grants);
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
    private static Material Material(string root,string name,Color color)
    {
        var shader=Shader.Find("Universal Render Pipeline/Lit");
        if(!shader)throw new InvalidOperationException("URP/Lit shader missing.");
        var value=new Material(shader){name=name};value.SetColor("_BaseColor",color);
        AssetDatabase.CreateAsset(value,root+"/"+name+".mat");return value;
    }
    private static TerrainLayer Layer(string root,string name,Color color)
    {
        var texture=new Texture2D(2,2);texture.SetPixels(new[]{color,color,color,color});texture.Apply();
        AssetDatabase.CreateAsset(texture,root+"/"+name+"Texture.asset");
        var layer=new TerrainLayer{diffuseTexture=texture,tileSize=new Vector2(8,8)};
        AssetDatabase.CreateAsset(layer,root+"/"+name+".terrainlayer");return layer;
    }
}
