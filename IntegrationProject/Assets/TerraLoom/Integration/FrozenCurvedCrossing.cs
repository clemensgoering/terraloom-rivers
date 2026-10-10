using System;
using System.Linq;
using TerraLoom.Core;
using TerraLoom.Paths;
using TerraLoom.Paths.Unity;
using TerraLoom.Core.Unity;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>Bounded external crossing authority for the frozen seed42 regression scene.
    /// Approves only partial candidate windows inside the authored road-crossing zone.
    /// It is not a module default or a grant to protected/foundation reservations.</summary>
    [DefaultExecutionOrder(290)]
    public sealed class FrozenCurvedCrossing : MonoBehaviour
    {
        public TerraLoomIntegration Composition;
        public bool GenerateOnStart=true;
        public bool WithdrawPermissions;
        public Rect ApprovedCrossingZone=new Rect(24,54,40,10);
        [Tooltip("Explicit construction-only evidence when the unchanged full road has no route. Never reports composition readiness.")]
        public bool ConstructionProbe=true;
        public PathRoute ProbeRoute { get; private set; }
        public GameObject ProbeRoot { get; private set; }
        public string ProbeDiagnostic { get; private set; }
        public string FullRouteFailure { get; private set; }
        private void OnEnable(){if(Composition)Composition.CrossingPermissions=Authorize;}
        private void Start(){if(GenerateOnStart&&!Generate())Debug.LogError(ProbeDiagnostic??Composition.Diagnostics,this);}
        public bool Generate()
        {
            if(!Composition)throw new InvalidOperationException("Frozen crossing composition missing.");
            Composition.CrossingPermissions=Authorize;
            if(Composition.Generate())return true;
            FullRouteFailure=Composition.Diagnostics;
            if(!ConstructionProbe||Composition.ActiveStep!="03-plan-routes")return false;
            try{return GenerateLocalProbe();}catch(Exception ex){ProbeDiagnostic="Construction probe failed: "+ex.Message+"\nFull route remains rejected: "+FullRouteFailure;return false;}
        }
        /// <summary>Visual evidence only. Keeps original anchors and full-route rejection; publishes no Paths plan.</summary>
        private bool GenerateLocalProbe()
        {
            ClearProbe();var c=Composition;var paths=c.Paths;var input=c.LastSharedInput;var source=c.LastSourceInput;
            var terrain=c.World.Terrain;var size=terrain.terrainData.size;var position=terrain.transform.position;
            var bounds=new WorldBounds(position.x,position.z,position.x+size.x,position.z+size.z);
            var permissions=Authorize(input);
            var options=PathPlanner.InspectAuthorizedBridgeAssemblies(input,paths.CaptureProfile(),bounds,permissions)
                .Where(r=>Math.Abs(Math.Abs(r.Waypoints[2].X-r.Waypoints[1].X)-9.511811023622044)<.00001).ToArray();
            if(options.Length==0)throw new InvalidOperationException("No authorized exact 9.511811m local assembly.");
            if(!c.Rivers.GenerateFromSnapshot(source,bounds,c.Rivers.Connections.Select(r=>r.Capture()).ToArray()))throw new InvalidOperationException(c.Rivers.Diagnostics);
            var final=c.World.CaptureTerrain().Heights as GridHeightSource;
            string last="";
            foreach(var route in options)
            {
                var staged=new GameObject("CONSTRUCTION PROBE ONLY - original target route rejected");staged.SetActive(false);staged.transform.SetParent(transform,false);
                try
                {
                    var binding=route.BridgeCrossings.Single();var grant=permissions.Find(binding.CrossingId);
                    var deck=PathGeometry.BuildSegment(route.Waypoints[1],route.Waypoints[2],PathGeometryRole.BridgeDeck,source.Terrain.Heights,paths.Width,paths.SurfaceOffset);
                    Mesh body=null;
                    try
                    {
                        body=TimberTrussGeometry.Build(route,1,2,input,final,paths.Width,paths.SurfaceOffset,paths.BridgeClearance,paths.MaximumGeometryVertices,authorization:grant);
                        var result=CollectiveBridgeCandidateInspection.Inspect(input,input.Identity,binding.CrossingId,binding.CandidateId,grant,deck,body,includeBodyFootprint:true);
                        if(!result.Decision.Accepted)throw new InvalidOperationException(result.Diagnostic);
                        Add(staged,body,PathGeometryRole.BridgeStructure);body=null;ProbeDiagnostic=result.Diagnostic;
                    }
                    finally{DestroyMesh(deck);if(body)DestroyMesh(body);}
                    foreach(int i in new[]{0,2})
                    {
                        var tread=PathGeometry.BuildSegment(route.Waypoints[i],route.Waypoints[i+1],PathGeometryRole.BridgeLanding,source.Terrain.Heights,paths.Width+2*TimberTrussGeometry.LateralPadding,paths.SurfaceOffset);
                        try
                        {
                            CollectiveBridgeLandingInspection.Validate(tread,input,final,grant,binding.Footprint(paths.Width+2*TimberTrussGeometry.LateralPadding));
                            Add(staged,TimberTrussGeometry.BuildApproach(tread,route.Waypoints[i],route.Waypoints[i+1],paths.Width,paths.SurfaceOffset,final,paths.MaximumGeometryVertices),PathGeometryRole.BridgeRamp);
                        }finally{DestroyMesh(tread);}
                    }
                    if(Authorize(input).Fingerprint!=permissions.Fingerprint)throw new InvalidOperationException("Current rights changed before probe activation.");
                    ProbeRoute=route;ProbeRoot=staged;staged.SetActive(true);
                    ProbeDiagnostic="CONSTRUCTION ONLY; full target connection is NOT ready. "+ProbeDiagnostic;
                    Debug.LogWarning(ProbeDiagnostic+"\n"+FullRouteFailure,this);return true;
                }
                catch(Exception ex){last=ex.Message;if(Application.isPlaying)Destroy(staged);else DestroyImmediate(staged);}
            }
            throw new InvalidOperationException("All locally planned exact-span candidates rejected final construction: "+last);
        }
        private void Add(GameObject root,Mesh mesh,PathGeometryRole role)
        {
            mesh.hideFlags=HideFlags.DontSave;var node=new GameObject(role.ToString());node.transform.SetParent(root.transform,false);
            node.AddComponent<MeshFilter>().sharedMesh=mesh;node.AddComponent<MeshRenderer>();node.AddComponent<MeshCollider>().sharedMesh=mesh;
            var marker=node.AddComponent<PathGeneratedGeometry>();marker.Initialize(role,Composition.Paths.BridgeMaterial);marker.TrackTransientMesh(mesh);
        }
        private static void DestroyMesh(Mesh mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
        public void ClearProbe(){if(ProbeRoot){if(Application.isPlaying)Destroy(ProbeRoot);else DestroyImmediate(ProbeRoot);}ProbeRoot=null;ProbeRoute=null;ProbeDiagnostic=null;}
        public PathCrossingPermissions Authorize(PlanSnapshot snapshot)
        {
            if(WithdrawPermissions)return null;
            var zone=new WorldBounds(ApprovedCrossingZone.xMin,ApprovedCrossingZone.yMin,ApprovedCrossingZone.xMax,ApprovedCrossingZone.yMax);
            var sections=snapshot.Crossings.Where(c=>(c.AllowedKinds&CrossingKind.Bridge)!=0&&c.Bounds.Overlaps(zone))
                .Select(c=>new {Candidate=c,Area=Intersection(c.Bounds,zone)})
                .Where(s=>s.Area.HasValue).Select(s=>new CrossingSectionPermission(s.Candidate.WaterId,s.Candidate.Id,s.Area.Value)).ToArray();
            if(sections.Length==0||sections.Length>64)throw new InvalidOperationException("Frozen road zone needs one to 64 explicit partial windows.");
            double half=Composition.Paths.Width*.5+Composition.Paths.CaptureProfile().BridgeLateralPadding;
            var grants=snapshot.Crossings.Where(c=>sections.Any(s=>s.CandidateId==c.Id))
                .Where(c=>{double z=(c.Bounds.MinZ+c.Bounds.MaxZ)/2;return z-half>=zone.MinZ&&z+half<=zone.MaxZ;})
                .Select(c=>new CollectiveCrossingAuthorization("bridge:"+c.Id+":0",snapshot.Identity,sections));
            return new PathCrossingPermissions(grants);
        }
        private static WorldBounds? Intersection(WorldBounds a,WorldBounds b)
        {
            double x0=Math.Max(a.MinX,b.MinX),x1=Math.Min(a.MaxX,b.MaxX),z0=Math.Max(a.MinZ,b.MinZ),z1=Math.Min(a.MaxZ,b.MaxZ);
            return x0<x1&&z0<z1?new WorldBounds(x0,z0,x1,z1):(WorldBounds?)null;
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color=WithdrawPermissions?Color.red:new Color(.9f,.6f,.15f);
            Gizmos.DrawWireCube(new Vector3(ApprovedCrossingZone.center.x,6,ApprovedCrossingZone.center.y),new Vector3(ApprovedCrossingZone.width,.2f,ApprovedCrossingZone.height));
        }
    }
}
