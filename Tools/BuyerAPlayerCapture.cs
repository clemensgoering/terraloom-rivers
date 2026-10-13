using System;
using System.Collections;
using System.IO;
using TerraLoom.Paths.Unity;
using UnityEngine;

/// <summary>Player-height and physical crossing evidence for the supplied-terrain consumer.</summary>
public sealed class BuyerAPlayerCapture : MonoBehaviour
{
    public Vector3[] Route;
    public Vector3 BridgeStart,BridgeEnd;
    public bool ValidateWalkability;

    private IEnumerator Start()
    {
        if(Route==null||Route.Length<2){Debug.LogError("BUYER_A_PLAYER_FAILED missing captured route");Application.Quit(1);yield break;}
        for(int i=0;i<4;i++)yield return null;
        try
        {
            int probes=0;
            if(ValidateWalkability)probes=WalkAndProbe();
            var camera=GetComponent<Camera>();
            var output=Path.Combine(Application.persistentDataPath,"BuyerA-Evidence");
            Directory.CreateDirectory(output);
            var from=BridgeStart;var to=BridgeEnd;var direction=(to-from).normalized;
            var side=new Vector3(-direction.z,0,direction.x);var middle=(from+to)*.5f;
            Save(camera,output,"bridge-overview",middle+side*15-direction*10+Vector3.up*12,middle-Vector3.up);
            Save(camera,output,"bridge-west",from-direction*4+side*3+Vector3.up*2.6f,from+direction*1.6f+Vector3.up*.4f);
            Save(camera,output,"bridge-east",to+direction*4+side*3+Vector3.up*2.6f,to-direction*1.6f+Vector3.up*.4f);
            Save(camera,output,"bridge-side",middle+side*10+Vector3.up*2,middle);
            Save(camera,output,"bridge-underside",middle+side*5-direction*2-Vector3.up*.75f,middle-Vector3.up*.4f);
            Debug.Log("BUYER_A_PLAYER_SUCCESS structural="+ValidateWalkability+" span="+Vector3.Distance(from,to)+" colliderProbes="+probes);
            Application.Quit(0);
        }
        catch(Exception ex){Debug.LogError("BUYER_A_PLAYER_FAILED "+ex);Application.Quit(1);}
    }

    private int WalkAndProbe()
    {
        Physics.SyncTransforms();int probes=0;
        for(int segment=0;segment<Route.Length-1;segment++)
        {
            var a=Route[segment];var b=Route[segment+1];int count=Mathf.CeilToInt(Vector3.Distance(a,b)/.15f);
            for(int i=0;i<=count;i++)
            {
                var point=Vector3.Lerp(a,b,(float)i/count);
                if(!Physics.Raycast(point+Vector3.up*5,Vector3.down,out var hit,8)||!hit.collider.GetComponent<PathGeneratedGeometry>())
                    throw new InvalidOperationException("Missing path collider at segment "+segment+" sample "+i);
                probes++;
            }
        }
        var walker=new GameObject("BuyerA actual CharacterController probe");
        try
        {
            var controller=walker.AddComponent<CharacterController>();controller.height=1.8f;controller.radius=.3f;
            controller.stepOffset=.25f;controller.slopeLimit=40;controller.enabled=false;
            walker.transform.position=Route[0]+Vector3.up;controller.enabled=true;Physics.SyncTransforms();
            controller.Move(Vector3.down*.2f);
            if(!controller.isGrounded)throw new InvalidOperationException("CharacterController cannot settle on first approach.");
            for(int segment=0;segment<Route.Length-1;segment++)
            {
                var a=Route[segment];var b=Route[segment+1];int count=Mathf.CeilToInt(Vector3.Distance(a,b)/.1f);
                for(int i=1;i<=count;i++)
                {
                    var target=Vector3.Lerp(a,b,(float)i/count);target.y=walker.transform.position.y;
                    controller.Move(target-walker.transform.position+Vector3.down*.06f);
                    var miss=walker.transform.position-target;miss.y=0;
                    if(miss.magnitude>.2f)throw new InvalidOperationException("CharacterController blocked at segment "+segment+" sample "+i);
                }
            }
        }
        finally{Destroy(walker);}
        return probes;
    }

    private static void Save(Camera camera,string directory,string name,Vector3 position,Vector3 target)
    {
        camera.transform.position=position;camera.transform.LookAt(target);
        var render=new RenderTexture(1280,720,24);var previous=RenderTexture.active;
        try
        {
            camera.targetTexture=render;camera.Render();RenderTexture.active=render;
            var texture=new Texture2D(render.width,render.height,TextureFormat.RGB24,false);
            try
            {
                texture.ReadPixels(new Rect(0,0,render.width,render.height),0,0);texture.Apply();
                var path=Path.Combine(directory,name+".png");File.WriteAllBytes(path,texture.EncodeToPNG());
                Debug.Log("BUYER_A_PLAYER_VIEW "+path);
            }
            finally{Destroy(texture);}
        }
        finally{camera.targetTexture=null;RenderTexture.active=previous;Destroy(render);}
    }
}
