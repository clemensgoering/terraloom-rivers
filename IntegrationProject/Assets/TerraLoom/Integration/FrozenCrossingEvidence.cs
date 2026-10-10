using System;
using System.Collections;
using System.IO;
using System.Linq;
using TerraLoom.Paths;
using TerraLoom.Paths.Unity;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>Actual Player capture and physical coverage of the unchanged curved seed42 crossing.</summary>
    public sealed class FrozenCrossingEvidence:MonoBehaviour
    {
        public FrozenCurvedCrossing Scenario;
        public bool RequireFullRoute;
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-terraloomScreenshot");
            yield return null;
            if(!args.Contains("-terraloomSmoke")||at<0||at+1>=args.Length)yield break;
            for(int i=0;i<4;i++)yield return null;
            try
            {
                var c=Scenario.Composition;if(c.Paths.LastPlan==null&&Scenario.ProbeRoute==null)throw new InvalidOperationException(Scenario.ProbeDiagnostic??c.Diagnostics);
                if(RequireFullRoute&&(c.Paths.LastPlan==null||Scenario.ProbeRoot||Scenario.ProbeRoute!=null))throw new InvalidOperationException("Full published route required; construction probe cannot qualify.");
                if(c.Paths.LastPlan!=null&&!c.ValidateCurrent(out var reason))throw new InvalidOperationException(reason);
                var route=Scenario.ProbeRoute??c.Paths.LastPlan.Routes.Single();int first=route.Surfaces.ToList().IndexOf(PathSurface.Bridge),last=first;
                if(first<0)throw new InvalidOperationException("Missing actual crossing.");while(last<route.Surfaces.Count&&route.Surfaces[last]==PathSurface.Bridge)last++;
                Vector3 P(int i){var p=route.Waypoints[i];return new Vector3((float)p.X,(float)p.Y+c.Paths.SurfaceOffset,(float)p.Z);}
                var a=P(first);var b=P(last);var f=(b-a).normalized;var side=new Vector3(-f.z,0,f.x);var mid=(a+b)/2;
                Physics.SyncTransforms();int probes=0;
                for(int s=0;s<route.Surfaces.Count;s++)
                {
                    var p=P(s);var q=P(s+1);int steps=Mathf.CeilToInt(Vector3.Distance(p,q)/.15f);
                    for(int j=0;j<=steps;j++)
                    {
                        var point=Vector3.Lerp(p,q,(float)j/steps);
                        if(!Physics.Raycast(point+Vector3.up*5,Vector3.down,out var hit,8)||!hit.collider.GetComponent<PathGeneratedGeometry>())throw new InvalidOperationException("Missing path collider at "+s+":"+j);
                        probes++;
                    }
                }
                var walker=new GameObject("Actual CharacterController crossing probe");
                try
                {
                    var controller=walker.AddComponent<CharacterController>();controller.height=1.8f;controller.radius=.3f;controller.stepOffset=.25f;controller.slopeLimit=40;
                    controller.enabled=false;walker.transform.position=P(0)+Vector3.up;controller.enabled=true;Physics.SyncTransforms();controller.Move(Vector3.down*.2f);
                    if(!controller.isGrounded)throw new InvalidOperationException("CharacterController cannot settle on the approach toe.");
                    for(int s=0;s<route.Surfaces.Count;s++)
                    {
                        var p=P(s);var q=P(s+1);int steps=Mathf.CeilToInt(Vector3.Distance(p,q)/.1f);
                        for(int j=1;j<=steps;j++)
                        {
                            var target=Vector3.Lerp(p,q,(float)j/steps);target.y=walker.transform.position.y;
                            controller.Move(target-walker.transform.position+Vector3.down*.06f);
                            var horizontal=walker.transform.position-target;horizontal.y=0;
                            if(horizontal.magnitude>.2f)throw new InvalidOperationException("CharacterController blocked at "+s+":"+j);
                        }
                    }
                }
                finally{Destroy(walker);}
                string path=args[at+1];Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                void View(string suffix,Vector3 position,Vector3 target)
                {
                    Camera.main.transform.position=position;Camera.main.transform.LookAt(target);Camera.main.fieldOfView=55;
                    RuntimeVisualCapture.Save(Camera.main,Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+suffix+".png"));
                }
                View("-overview",mid+side*15-f*10+Vector3.up*12,mid-Vector3.up);
                View("-west",P(first-1)-f*4+side*3+Vector3.up*2.6f,P(first-1)+f*1.6f+Vector3.up*.4f);
                View("-east",P(last+1)+f*4+side*3+Vector3.up*2.6f,P(last+1)-f*1.6f+Vector3.up*.4f);
                View("-underside",mid+side*5-f*2-Vector3.up*.75f,mid-Vector3.up*.4f);
                File.WriteAllText(Path.ChangeExtension(path,".txt"),"Frozen seed42; span="+Vector3.Distance(a,b).ToString("R")+"; width="+c.Paths.Width+"; probes="+probes+"\n"+Scenario.ProbeDiagnostic+"\nFull route: "+c.Diagnostics);
                Debug.Log("TERRALOOM_FROZEN_TRUSS_SUCCESS fullRoute="+RequireFullRoute+" span="+Vector3.Distance(a,b).ToString("R")+" probes="+probes);Application.Quit(0);
            }
            catch(Exception ex){Debug.LogError("TERRALOOM_FROZEN_TRUSS_FAILED "+ex);Application.Quit(1);}
        }
    }
}
