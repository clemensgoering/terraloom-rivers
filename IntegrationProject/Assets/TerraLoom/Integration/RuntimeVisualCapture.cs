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
        public static void Save(Camera camera,string path)
        {
            if(!camera)throw new InvalidOperationException("A runtime camera is required.");
            var target=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);
            var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                target.Create();var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
                if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("Runtime URP camera rendering is unavailable.");
                RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=target;
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
