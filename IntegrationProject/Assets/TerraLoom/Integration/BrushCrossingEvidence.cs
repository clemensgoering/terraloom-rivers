using System;
using System.Collections;
using System.Linq;
using TerraLoom.Core.Unity;
using TerraLoom.Paths;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>Explicit Windows smoke/render probe for the experimental brush crossing fixture.</summary>
    public sealed class BrushCrossingEvidence : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-terraloomScreenshot");
            if(!args.Contains("-terraloomSmoke")||at<0||at+1>=args.Length)yield break;
            yield return null;
            var composition=FindFirstObjectByType<TerraLoomIntegration>();
            try
            {
                if(!composition||!composition.Generate())throw new InvalidOperationException(composition?composition.Diagnostics:"Missing composition");
                if(!composition.ValidateCurrent(out var reason))throw new InvalidOperationException(reason);
                if(!composition.Rivers.TerrainBrush||!composition.Paths.LastPlan.Routes.Any(r=>r.Surfaces.Contains(PathSurface.Bridge)))
                    throw new InvalidOperationException("Evidence needs terrain brush and a negotiated bridge.");
                foreach(var decoration in composition.World.GetComponentsInChildren<RegionDecoration>())decoration.Generate();
                if(composition.Paths.TimberBeamBridge)
                {
                    var route=composition.Paths.LastPlan.Routes.First(r=>r.Surfaces.Contains(PathSurface.Bridge));
                    int index=route.Surfaces.ToList().IndexOf(PathSurface.Bridge);var p=route.Waypoints[index];var q=route.Waypoints[index+1];
                    var forward=new Vector3((float)(q.X-p.X),0,(float)(q.Z-p.Z)).normalized;
                    var side=new Vector3(-forward.z,0,forward.x);var position=new Vector3((float)p.X,0,(float)p.Z)-forward+side*2.1f;
                    position.y=composition.World.Terrain.SampleHeight(position)+composition.World.Terrain.transform.position.y+.9f;
                    var reference=GameObject.CreatePrimitive(PrimitiveType.Capsule);reference.name="Metric reference: 1.80m high, 0.5m wide";
                    reference.transform.position=position;reference.transform.localScale=new Vector3(.5f,.9f,.5f);
                    reference.GetComponent<Collider>().enabled=false;
                    var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",new Color(.82f,.72f,.5f));
                    reference.GetComponent<Renderer>().material=material;
                    Debug.Log("TERRALOOM_METRIC_REFERENCE height="+reference.GetComponent<Renderer>().bounds.size.y+"m; temporary capture reference, no gameplay collider.");
                }
            }
            catch(Exception ex){Debug.LogError("TERRALOOM_BRUSH_CROSSING_FAILED "+ex);Application.Quit(1);yield break;}
            yield return new WaitForFixedUpdate();
            for(int i=0;i<4;i++)yield return null;
            try
            {
                RuntimeVisualCapture.SaveViews(Camera.main,composition,args[at+1]);
                Debug.Log("TERRALOOM_BRUSH_CROSSING_SUCCESS "+composition.Diagnostics);Application.Quit(0);
            }
            catch(Exception ex){Debug.LogError("TERRALOOM_BRUSH_CROSSING_FAILED "+ex);Application.Quit(1);}
        }
    }
}
