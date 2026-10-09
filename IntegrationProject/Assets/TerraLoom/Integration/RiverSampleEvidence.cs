using System;
using System.Collections;
using System.Globalization;
using System.IO;
using TerraLoom.Rivers.Unity;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>Explicit standalone river evidence runner. Never reports multi-module readiness.</summary>
    public sealed class RiverSampleEvidence : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"-terraloomSmoke")<0)yield break;
            var rivers=FindFirstObjectByType<TerraLoomRivers>();
            if(!rivers){Debug.LogError("TERRALOOM_RIVER_EVIDENCE_FAILED Missing river");Application.Quit(1);yield break;}
            int at=Array.IndexOf(args,"-terraloomWaterInset");
            if(at>=0)
            {
                if(at+1>=args.Length || !float.TryParse(args[at+1],NumberStyles.Float,CultureInfo.InvariantCulture,out float inset))
                {Debug.LogError("TERRALOOM_RIVER_EVIDENCE_FAILED Invalid inset argument");Application.Quit(1);yield break;}
                rivers.WaterInset=inset;
            }
            at=Array.IndexOf(args,"-terraloomScreenshot");
            if(!rivers || at<0 || at+1>=args.Length || !rivers.Generate())
            {Debug.LogError("TERRALOOM_RIVER_EVIDENCE_FAILED "+(rivers?rivers.Diagnostics:"Missing river"));Application.Quit(1);yield break;}
            yield return new WaitForFixedUpdate();yield return new WaitForEndOfFrame();
            try
            {
                var camera=Camera.main;string path=args[at+1];
                RuntimeVisualCapture.Save(camera,path);
                RuntimeVisualCapture.InspectWaterTerrain(rivers);
                var route=rivers.LastPlan.Routes[0];int row=route.WaterPolyline.Count/2;
                var p=route.WaterPolyline[row];var a=route.WaterPolyline[Math.Max(0,row-1)];
                var b=route.WaterPolyline[Math.Min(route.WaterPolyline.Count-1,row+1)];
                var forward=new Vector3((float)(b.X-a.X),0,(float)(b.Z-a.Z)).normalized;
                var side=new Vector3(-forward.z,0,forward.x);
                var centre=new Vector3((float)p.X,(float)p.Y,(float)p.Z);
                string stem=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path));
                camera.transform.position=centre+side*9+Vector3.up*3;camera.transform.LookAt(centre);
                RuntimeVisualCapture.Save(camera,stem+"-section.png");
                camera.transform.position=centre+side*6-forward*4+Vector3.up*1.7f;camera.transform.LookAt(centre+forward*5);
                RuntimeVisualCapture.Save(camera,stem+"-player.png");
                Debug.Log("TERRALOOM_RIVER_EVIDENCE_SUCCESS inset="+rivers.WaterInset.ToString("R",CultureInfo.InvariantCulture));
                Application.Quit(0);
            }
            catch(Exception ex){Debug.LogError("TERRALOOM_RIVER_EVIDENCE_FAILED "+ex);Application.Quit(1);}
        }
    }
}
