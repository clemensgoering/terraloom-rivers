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
                    Save(camera,stem+"-player.png");return;
                }
                throw new InvalidOperationException("Bridge viewpoints require a negotiated bridge.");
            }
            finally{camera.transform.SetPositionAndRotation(position,rotation);}
        }
        public static void Save(Camera camera,string path)
        {
            if(!camera)throw new InvalidOperationException("A runtime camera is required.");
            var target=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);
            var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
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
