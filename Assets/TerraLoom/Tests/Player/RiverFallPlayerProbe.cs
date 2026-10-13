using System;
using System.Collections;
using System.IO;
using System.Linq;
using TerraLoom.Rivers.Unity;
using UnityEngine;

/// <summary>Opt-in packaged-fall Player evidence. Lives in the sample test project, not the UPM.</summary>
public sealed class RiverFallPlayerProbe : MonoBehaviour
{
    private IEnumerator Start()
    {
        var args=Environment.GetCommandLineArgs();
        if(!args.Contains("-terraloomFallProbe"))yield break;
        yield return null;
        yield return null;
        int exit=0;
        try
        {
            var output=Arg(args,"-terraloomFallOutput");
            if(string.IsNullOrEmpty(output))throw new InvalidOperationException("Missing fall evidence output directory.");
            Directory.CreateDirectory(output);
            var rivers=FindFirstObjectByType<TerraLoomRivers>();
            if(!rivers||!rivers.GeneratedRoot)throw new InvalidOperationException("Baked fall root missing in Player.");
            bool boundary=rivers.PlanJson!=null&&rivers.PlanJson.Contains("\"format\": 4");
            var geometry=rivers.GeneratedRoot.GetComponentsInChildren<RiverGeneratedGeometry>(true);
            if(geometry.Length!=(boundary?5:4)||!geometry.Any(g=>g.Role==RiverGeometryRole.FallRock))
                throw new InvalidOperationException("Baked rock/water parts missing in Player.");
            if(rivers.GeneratedRoot.GetComponentsInChildren<MeshCollider>(true).Length!=1)
                throw new InvalidOperationException("Baked rock collider missing in Player.");
            float fallX=boundary?6:16;
            Probe("upper lip",new Vector3(fallX-.3f,12,16),12);
            Probe("lower pool",new Vector3(fallX+3.5f,7,16),7);
            Probe("outlet",new Vector3(boundary?29:25,7,16),7);
            var views=new[]{
                new View("lip",new Vector3(fallX-5,9.2f,11),new Vector3(fallX,7,16)),
                new View("side",new Vector3(fallX,6.3f,8),new Vector3(fallX,5,16)),
                new View("foot",new Vector3(fallX+5,3.4f,10),new Vector3(fallX,4,16)),
                new View("pool",new Vector3(fallX+7,4.2f,11),new Vector3(fallX+4,2.1f,16)),
                new View("outlet",boundary?new Vector3(31,4.0f,11):new Vector3(30,4.0f,14),
                    boundary?new Vector3(27,2.1f,16):new Vector3(24,2.1f,16))};
            foreach(var view in views)Capture(output,view);
            if(boundary)
            {
                Walk("pool",new Vector3(fallX+4,8,16));
                Walk("receiving bank",new Vector3(22,8,19));
                Walk("outlet",new Vector3(30,8,16));
            }
            Debug.Log("TERRALOOM_FALL_PLAYER_PASS parts="+geometry.Length+" collider=1 probes=3 images="+views.Length+
                " controller="+(boundary?3:0));
        }
        catch(Exception ex){exit=1;Debug.LogError("TERRALOOM_FALL_PLAYER_FAIL "+ex);}
        Application.Quit(exit);
    }

    private static string Arg(string[] args,string key)
    {for(int i=0;i<args.Length-1;i++)if(args[i]==key)return args[i+1];return null;}
    private static void Probe(string label,Vector3 origin,float distance)
    {
        if(!Physics.Raycast(origin,Vector3.down,out var hit,distance))
            throw new InvalidOperationException(label+" has no supporting collider.");
        Debug.Log("TERRALOOM_FALL_CONTACT "+label+" "+hit.collider.name+" y="+hit.point.y.ToString("F3"));
    }
    private static void Walk(string label,Vector3 start)
    {
        var go=new GameObject("Fall CharacterController "+label);
        go.transform.position=start;
        var controller=go.AddComponent<CharacterController>();controller.radius=.3f;controller.height=1.8f;
        try
        {
            bool grounded=false;
            for(int i=0;i<48;i++)
            {
                var flags=controller.Move(Vector3.down*.3f);
                if((flags&CollisionFlags.Below)!=0){grounded=true;break;}
            }
            if(!grounded)throw new InvalidOperationException("CharacterController did not land at "+label);
            Debug.Log("TERRALOOM_FALL_CONTROLLER "+label+" y="+go.transform.position.y.ToString("F3"));
        }
        finally{Destroy(go);}
    }
    private static void Capture(string folder,View view)
    {
        var go=new GameObject("Fall camera "+view.Name);
        var camera=go.AddComponent<Camera>();camera.transform.position=view.Position;
        camera.transform.LookAt(view.Target);camera.fieldOfView=60;camera.nearClipPlane=.1f;
        camera.clearFlags=CameraClearFlags.Skybox;
        var target=new RenderTexture(960,540,24,RenderTextureFormat.ARGB32);
        var previous=RenderTexture.active;
        try
        {
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            var pixels=new Texture2D(960,540,TextureFormat.RGB24,false);
            try
            {
                pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder,view.Name+".png"),pixels.EncodeToPNG());
            }
            finally{Destroy(pixels);}
        }
        finally
        {
            RenderTexture.active=previous;camera.targetTexture=null;Destroy(target);Destroy(go);
        }
    }
    private readonly struct View
    {
        public readonly string Name;public readonly Vector3 Position,Target;
        public View(string name,Vector3 position,Vector3 target){Name=name;Position=position;Target=target;}
    }
}
