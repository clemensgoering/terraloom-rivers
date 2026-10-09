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
