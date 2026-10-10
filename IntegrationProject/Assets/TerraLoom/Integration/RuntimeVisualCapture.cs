using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TerraLoom.Integration
{
    /// <summary>Smoke-test image capture independent of the hidden player's window/backbuffer.
    /// Uses the public URP render-request API, just like the editor evidence adapter.</summary>
    public static class RuntimeVisualCapture
    {
        /// <summary>Same generated world at overview, bridge approach/player height and close range.
        /// The viewpoints use the actual negotiated bridge, including seed-dependent positions.</summary>
        public static void SaveViews(Camera camera,TerraLoomIntegration composition,string path)
        {
            var position=camera.transform.position;var rotation=camera.transform.rotation;
            try
            {
                Save(camera,path);
                InspectWaterTerrain(composition.Rivers);
                if(composition.Paths.TimberBeamBridge)
                {
                    foreach(var part in composition.Paths.GeneratedRoot.GetComponentsInChildren<TerraLoom.Paths.Unity.PathGeneratedGeometry>())
                    {
                        if(part.Role!=TerraLoom.Paths.Unity.PathGeometryRole.BridgeLanding)continue;
                        double minimum=double.PositiveInfinity,maximum=double.NegativeInfinity;int count=0;
                        foreach(var vertex in part.GetComponent<MeshFilter>().sharedMesh.vertices)
                        {
                            var point=part.transform.TransformPoint(vertex);
                            double ground=composition.World.Terrain.SampleHeight(point)+composition.World.Terrain.transform.position.y;
                            minimum=Math.Min(minimum,point.y-ground);maximum=Math.Max(maximum,point.y-ground);count++;
                        }
                        Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "TERRALOOM_LANDING_TERRAIN_GAP {0}: vertices={1}, min={2:R}m, max={3:R}m. Vertex-only distances; thin walking surface, no earth fill generated.",part.name,count,minimum,maximum));
                    }
                }
                var river=composition.Rivers.LastPlan.Routes[0];
                int middle=river.WaterPolyline.Count/2;
                var station=river.WaterPolyline[middle];
                var before=river.WaterPolyline[Math.Max(0,middle-1)];
                var after=river.WaterPolyline[Math.Min(river.WaterPolyline.Count-1,middle+1)];
                var forwardRiver=new Vector3((float)(after.X-before.X),0,(float)(after.Z-before.Z)).normalized;
                var sideRiver=new Vector3(-forwardRiver.z,0,forwardRiver.x);
                var centreRiver=new Vector3((float)station.X,(float)station.Y,(float)station.Z);
                string riverStem=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path));
                camera.transform.position=centreRiver+sideRiver*9+Vector3.up*3;
                camera.transform.LookAt(centreRiver);
                Save(camera,riverStem+"-river-section.png");
                camera.transform.position=centreRiver+sideRiver*6-forwardRiver*4+Vector3.up*1.7f;
                camera.transform.LookAt(centreRiver+forwardRiver*5);
                Save(camera,riverStem+"-river-player.png");
                foreach(var route in composition.Paths.LastPlan.Routes)for(int i=0;i<route.Surfaces.Count;i++)
                {
                    if(route.Surfaces[i]!=TerraLoom.Paths.PathSurface.Bridge)continue;
                    var p=route.Waypoints[i];var q=route.Waypoints[i+1];
                    var a=new Vector3((float)p.X,(float)p.Y,(float)p.Z);var b=new Vector3((float)q.X,(float)q.Y,(float)q.Z);
                    var center=(a+b)*.5f;var forward=b-a;forward.y=0;forward.Normalize();
                    string stem=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path));
                    camera.transform.position=center+new Vector3(9,7,-11);camera.transform.LookAt(center);
                    Save(camera,stem+"-close.png");
                    var player=a-forward*6;player.y=composition.World.Terrain.SampleHeight(player)+composition.World.Terrain.transform.position.y+1.7f;
                    camera.transform.position=player;camera.transform.LookAt(center+Vector3.up*1.2f);
                    Save(camera,stem+"-player.png");
                    // Additional evidence views expose both terrain joins and missing support
                    // structure. Existing five cameras stay identical for before/after comparison.
                    int end=i+1;
                    while(end<route.Surfaces.Count&&route.Surfaces[end]==TerraLoom.Paths.PathSurface.Bridge)end++;
                    var last=route.Waypoints[end];var far=new Vector3((float)last.X,(float)last.Y,(float)last.Z);
                    var axis=(far-a).normalized;var side=new Vector3(-axis.z,0,axis.x);
                    for(int landing=0;landing<2;landing++)
                    {
                        var foot=landing==0?a:far;var outward=landing==0?-axis:axis;
                        var eye=foot+outward*3+side*2;
                        eye.y=composition.World.Terrain.SampleHeight(eye)+composition.World.Terrain.transform.position.y+1.7f;
                        camera.transform.position=eye;camera.transform.LookAt(foot+Vector3.up*.4f);
                        Save(camera,stem+"-landing-"+landing+".png");
                        if(composition.Paths.TimberBeamBridge)
                        {
                            int outerIndex=landing==0?Math.Max(0,i-1):Math.Min(route.Waypoints.Count-1,end+1);
                            var outer=route.Waypoints[outerIndex];
                            var rampCenter=(foot+new Vector3((float)outer.X,(float)outer.Y,(float)outer.Z))*.5f;
                            camera.transform.position=rampCenter+side*6+Vector3.up*.6f;
                            camera.transform.LookAt(rampCenter);
                            Save(camera,stem+"-landing-side-"+landing+".png");
                        }
                    }
                    var span=(a+far)*.5f;
                    camera.transform.position=span+side*(composition.Paths.Width+2)-Vector3.up*.65f;
                    camera.transform.LookAt(span-Vector3.up*.3f);
                    Save(camera,stem+"-underside.png");return;
                }
                throw new InvalidOperationException("Bridge viewpoints require a negotiated bridge.");
            }
            finally{camera.transform.SetPositionAndRotation(position,rotation);}
        }
        public static void InspectWaterTerrain(TerraLoom.Rivers.Unity.TerraLoomRivers rivers)
        {
            var terrain=rivers.World.Terrain;
            var result=TerraLoom.Rivers.Unity.RiverWaterTerrainInspection.Inspect(
                rivers.GeneratedRoot,terrain.terrainData,terrain.transform.position);
            Debug.Log("TERRALOOM_WATER_TERRAIN_PROBE "+result);
            if(!result.Acceptable)Debug.LogWarning("TERRALOOM_WATER_TERRAIN_NOT_ACCEPTED "+result);
        }

        public static void Save(Camera camera,string path,int width=1440,int height=900)
        {
            if(!camera)throw new InvalidOperationException("A runtime camera is required.");
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
            var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                target.Create();var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
                if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("Runtime URP camera rendering is unavailable.");
                RenderPipeline.SubmitRenderRequest(camera,request);RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
                int colored=0;foreach(var color in pixels.GetPixels32())if(color.r>15||color.g>15||color.b>15)colored++;
                if(colored<10000)throw new InvalidOperationException("Runtime render is empty or black.");
                Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,pixels.EncodeToPNG());
                Debug.Log("TERRALOOM_RUNTIME_RENDER_OK "+colored+" nonblack pixels");
            }
            finally{RenderTexture.active=previous;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);}
        }
    }
}
