using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>Player-height evidence camera for the Git-consumer Buyer A probe.</summary>
public sealed class BuyerAPlayerCapture : MonoBehaviour
{
    private IEnumerator Start()
    {
        var camera=GetComponent<Camera>();
        var output=Path.Combine(Application.persistentDataPath,"BuyerA-Evidence");
        Directory.CreateDirectory(output);
        var positions=new[]{new Vector3(33,6.3f,59.3f),new Vector3(64,6.3f,59.3f)};
        var targets=new[]{new Vector3(48,5.4f,59.3f),new Vector3(48,5.4f,59.3f)};
        for(int i=0;i<positions.Length;i++)
        {
            camera.transform.position=positions[i];camera.transform.LookAt(targets[i]);
            yield return null;
            var target=new RenderTexture(1280,720,24);
            var previous=RenderTexture.active;
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
            var path=Path.Combine(output,"bridge-"+(i==0?"west":"east")+".png");
            File.WriteAllBytes(path,texture.EncodeToPNG());Destroy(texture);
            camera.targetTexture=null;RenderTexture.active=previous;Destroy(target);
            Debug.Log("BUYER_A_PLAYER_VIEW "+path);
            yield return null;
        }
        Application.Quit(0);
    }
}
