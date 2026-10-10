using System;
using System.Linq;
using System.Threading;
using TerraLoom.Core;
using TerraLoom.Core.Unity;
using TerraLoom.Paths;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers;
using TerraLoom.Rivers.Unity;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>Consumer example: neither product module imports the other. Uses one captured Core input.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class TerraLoomIntegration : MonoBehaviour
    {
        public TerraLoomWorld World;
        public TerraLoomPaths Paths;
        public TerraLoomRivers Rivers;
        public bool GenerateOnStart;
        [Tooltip("Consumer routing constraint only; grants no crossing or terrain-edit rights. Rivers still use the full terrain domain.")]
        public bool RestrictPathRouting;
        public Rect PathRoutingZone;
        public WorldBounds CapturePathBounds(WorldBounds terrainBounds)
        {
            if(!RestrictPathRouting)return terrainBounds;
            var selected=new WorldBounds(PathRoutingZone.xMin,PathRoutingZone.yMin,PathRoutingZone.xMax,PathRoutingZone.yMax);
            if(!selected.IsValid||!terrainBounds.Contains(selected))throw new InvalidOperationException("Path routing zone must be a positive finite rectangle within the source terrain.");
            return selected;
        }
        [SerializeField, TextArea] private string diagnostics;
        [SerializeField] private GenerationRunState generationState;
        [SerializeField] private string activeStep;
        [SerializeField, TextArea] private string generationTrace;
        [SerializeField, HideInInspector] private string builtInput, builtRiverProfile, builtTerrain, builtPathsJson, builtRiversJson;
        [SerializeField, HideInInspector] private bool builtCarve;
        [SerializeField, HideInInspector] private float builtSpacing;
        [SerializeField, HideInInspector] private int builtSedimentLayer;
        [SerializeField, HideInInspector] private float builtSedimentExposure;
        [SerializeField, HideInInspector] private Material[] builtMaterials;
        [SerializeField, HideInInspector] private Matrix4x4 builtPathsTransform, builtRiversTransform;
        public string Diagnostics => diagnostics;
        public GenerationRunState GenerationState => generationState;
        public string ActiveStep => activeStep;
        public string GenerationTrace => generationTrace;
        /// <summary>External example policy; recalculated at planning and each publication boundary.
        /// A saved path plan never supplies this authority. Null preserves the legacy example.</summary>
        public Func<PlanSnapshot,PathCrossingPermissions> CrossingPermissions;
        public PlanSnapshot LastSharedInput { get; private set; }
        public PlanSnapshot LastSourceInput { get; private set; }
        public PathPlan LastPathAttempt { get; private set; }
        /// <summary>Consumer diagnostic seam for deterministic transaction fault tests. Leave null in production.
        /// Boundaries: river-prepared, before-commit, river-bound, all-bound. Never supplied to product adapters.</summary>
        public Action<string> PublicationBoundary;
        /// <summary>Optional retirement callback; failures are post-commit diagnostics, with default cleanup fallback.</summary>
        public Action<GameObject> PathRetirement;
        private bool generating;
        private static GenerationSchedule Schedule() => new GenerationSchedule(new[] {
            new GenerationStep("01-capture-source",Array.Empty<string>(),new[]{"source-snapshot"}),
            new GenerationStep("02-plan-water",new[]{"source-snapshot"},new[]{"river-plan","water-offers"}),
            new GenerationStep("03-plan-routes",new[]{"water-offers"},new[]{"path-plan"}),
            new GenerationStep("04-publish-earthworks",new[]{"river-plan","path-plan"},new[]{"carved-terrain","river-geometry"}),
            new GenerationStep("05-publish-walkable",new[]{"carved-terrain","path-plan"},new[]{"walkable-geometry"}) });
        private void Awake()
        {
            if(!GenerateOnStart)return;
            // Awake runs before every Start, independent of hierarchy and execution order.
            if(World)World.AutoGenerateOnStart=false;
            if(Paths)Paths.GenerateOnStart=false;
            if(Rivers)Rivers.GenerateOnStart=false;
        }
        private void Start() { if (GenerateOnStart && !Generate()) Debug.LogError(diagnostics,this); }
        public bool Generate(CancellationToken cancellation = default)
        {
            if(generating)throw new InvalidOperationException("Composition generation cannot reenter an active publication.");
            generating=true;
            var run=new GenerationRun(Schedule());generationTrace="";generationState=GenerationRunState.Pending;activeStep="preflight";
            TerraLoomRivers.PreparedPublication preparedRiver=null;
            TerraLoomPaths.PreparedPublication preparedPaths=null;
            bool publicationCommitted=false;string cleanupDiagnostics="";
            void Step(string id,Action action)
            {
                activeStep=id;generationState=GenerationRunState.Running;
                try{run.Execute(id,action,id=="05-publish-walkable"?CancellationToken.None:cancellation);generationTrace+=id+": complete\n";}
                finally{generationState=run.State;}
            }
            try
            {
                if (!World || !Paths || !Rivers || Paths.World != World || Rivers.World != World) throw new InvalidOperationException("All modules must share this Core world.");
                cancellation.ThrowIfCancellationRequested();
                if (!Paths.GroundMaterial || !Paths.BridgeMaterial || !Rivers.WaterMaterial
                    || (!Rivers.TerrainBrush && (!Rivers.BedMaterial || !Rivers.BankMaterial)))
                    throw new InvalidOperationException("Bind path and water materials; mesh rivers also require bed and bank materials.");
                if(float.IsNaN(Paths.MeshSpacing)||float.IsInfinity(Paths.MeshSpacing)||Paths.MeshSpacing<.05f||Paths.MeshSpacing>2
                    ||Paths.MaximumGeometryVertices<128||Paths.MaximumGeometryVertices>8000000
                    ||Rivers.MaximumGeometryVertices<4||Rivers.MaximumGeometryVertices>8000000)
                    throw new InvalidOperationException("Invalid module geometry spacing/budget; no outputs changed.");
                Rivers.CaptureProfile();
                // Capture the unchanged source while keeping previous geometry available on planning failure.
                PlanSnapshot input=null,shared=null;WorldBounds bounds=default;
                System.Collections.Generic.IReadOnlyList<PathConnection> graph=null;
                System.Collections.Generic.IReadOnlyList<RiverRequest> requests=null;
                RiverPlan riverPlan=null;PathPlan pathPlan=null;string plannedRiverProfile=null;
                var plannedMaterials=Materials();var plannedPathTransform=Paths.transform.localToWorldMatrix;var plannedRiverTransform=Rivers.transform.localToWorldMatrix;
                float plannedSpacing=Paths.MeshSpacing,plannedExposure=Rivers.SedimentExposureDepth;int plannedLayer=Rivers.SedimentTerrainLayer,plannedPathBudget=Paths.MaximumGeometryVertices,plannedRiverBudget=Rivers.MaximumGeometryVertices;
                bool plannedCarve=Rivers.CarveTerrainCopy;
                Step("01-capture-source",()=>{input=CaptureSource(out bounds,out graph);LastSourceInput=input;});
                Step("02-plan-water",()=>{
                    requests=Requests(input);plannedRiverProfile=RiverPlanner.ProfileVersion(Rivers.CaptureProfile(),bounds,requests);riverPlan=RiverPlanner.Plan(input,Rivers.CaptureProfile(),bounds,requests,cancellation);
                    if(!riverPlan.Complete)throw new InvalidOperationException(string.Join("\n",riverPlan.Reports.Select(r=>r.Request.Id+": "+r.Outcome+" "+r.Detail)));
                    var offer=riverPlan.ToContribution("rivers",input.Identity);
                    shared=new PlanSnapshot(input.Identity,input.Terrain,new AnchorSnapshot(input.AnchorSourceId,input.AnchorSourceRevision,input.Anchors),
                        input.Landscape,input.Reservations.Concat(offer.Reservations),offer.Waters,offer.Crossings);
                });
                Step("03-plan-routes",()=>{
                    LastSharedInput=shared;
                    pathPlan=PathPlanner.Plan(shared,Paths.CaptureProfile(),CapturePathBounds(bounds),graph,cancellation,CrossingPermissions?.Invoke(shared));
                    LastPathAttempt=pathPlan;
                    if(!pathPlan.Complete)throw new InvalidOperationException(string.Join("\n",pathPlan.Reports.Select(r=>r.Connection.Id+": "+r.Outcome+" "+r.Detail)));
                });
                Step("04-publish-earthworks",()=>{
                    preparedRiver=Rivers.PrepareFromSnapshot(input,bounds,requests,cancellation);
                    PublicationBoundary?.Invoke("river-prepared");
                });
                Step("05-publish-walkable",()=>{
                    preparedPaths=Paths.PrepareFromSnapshot(shared,CapturePathBounds(bounds),preparedRiver.CaptureFinalHeights(),graph,cancellation,
                        currentPermissions:CrossingPermissions==null?(Func<PathCrossingPermissions>)null:()=>CrossingPermissions?.Invoke(shared));
                    PublicationBoundary?.Invoke("before-commit");
                    cancellation.ThrowIfCancellationRequested();
                    var current=CaptureSource(out var liveBounds,out var liveGraph);
                    if(current.Identity.InputFingerprint!=input.Identity.InputFingerprint
                        ||RiverPlanner.ProfileVersion(Rivers.CaptureProfile(),liveBounds,Requests(current))!=plannedRiverProfile
                        ||!plannedMaterials.SequenceEqual(Materials())||plannedPathTransform!=Paths.transform.localToWorldMatrix||plannedRiverTransform!=Rivers.transform.localToWorldMatrix
                        ||plannedSpacing!=Paths.MeshSpacing||plannedExposure!=Rivers.SedimentExposureDepth||plannedLayer!=Rivers.SedimentTerrainLayer
                        ||plannedPathBudget!=Paths.MaximumGeometryVertices||plannedRiverBudget!=Rivers.MaximumGeometryVertices||plannedCarve!=Rivers.CarveTerrainCopy)
                        throw new InvalidOperationException("Source changed while preparing composition.");
                    preparedRiver.ValidateBeforeCommit();preparedPaths.ValidateBeforeCommit();
                    cancellation.ThrowIfCancellationRequested();
                    // Synchronous host swap. Old resources remain alive through both bindings and metadata capture.
                    preparedRiver.CommitBindings();PublicationBoundary?.Invoke("river-bound");
                    cancellation.ThrowIfCancellationRequested();
                    preparedPaths.CommitBindings();PublicationBoundary?.Invoke("all-bound");
                    builtTerrain=World.TerrainContentFingerprint;
                    builtPathsJson=Digest(Paths.PlanJson);builtRiversJson=Digest(Rivers.PlanJson);
                    builtInput=input.Identity.InputFingerprint;builtRiverProfile=plannedRiverProfile;
                    builtCarve=plannedCarve;builtSpacing=plannedSpacing;builtMaterials=plannedMaterials;
                    builtSedimentLayer=plannedLayer;builtSedimentExposure=plannedExposure;
                    builtPathsTransform=plannedPathTransform;builtRiversTransform=plannedRiverTransform;
                    preparedPaths.SealBindings();preparedRiver.SealBindings();publicationCommitted=true;
                    cleanupDiagnostics=preparedPaths.Complete(PathRetirement)+"\n"+preparedRiver.Complete();
                    // Once both outputs are committed cancellation belongs to the next run.
                    // Decorations are invalidated only after successful collective publication.
                    foreach(var decoration in UnityEngine.Object.FindObjectsByType<RegionDecoration>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                        if(decoration.World==World)
                            try{decoration.Clear();}catch(Exception ex){cleanupDiagnostics+="\nDecoration invalidation: "+ex.Message;}
                });
                activeStep="";
                diagnostics="Core + Rivers + Paths: "+riverPlan.Routes.Count+" river, "+pathPlan.Routes.Count+" path; "+pathPlan.Routes.Sum(r=>r.Surfaces.Count(s=>s==PathSurface.Bridge))+" negotiated bridge deck(s).";
                if(!string.IsNullOrWhiteSpace(cleanupDiagnostics))diagnostics+="\nPost-commit cleanup: "+cleanupDiagnostics.Trim();
                return true;
            }
            catch (Exception ex)
            {

                generationState=ex is OperationCanceledException?GenerationRunState.Cancelled:GenerationRunState.Failed;
                diagnostics="Stage "+activeStep+": "+ex.Message+(publicationCommitted?" New composition committed; post-commit failure.":" Previous composition resources retained; attempt is not ready.");return false;
            }
            finally { try{preparedPaths?.Dispose();}finally{try{preparedRiver?.Dispose();}finally{generating=false;}} }
        }
        private PlanSnapshot CaptureSource(out WorldBounds bounds,out System.Collections.Generic.IReadOnlyList<PathConnection> graph)
        {
            if(!World||!World.Terrain||!World.Terrain.terrainData)throw new InvalidOperationException("Generate/assign terrain before composition.");
            // Let the river adapter capture its source and raster-expanded authored protections.
            // Calling before the temporary Paths source swap also preserves its ownership checks.
            var riverInput=Rivers.CaptureInput(out _,out _);
            var selected=World.Terrain;var current=selected.terrainData;
            try
            {
                if(Rivers.CarvedTerrainData)
                {
                    if(current!=Rivers.CarvedTerrainData||!Rivers.OriginalTerrainData)throw new InvalidOperationException("Selected terrain changed. Clear the composition first.");
                    selected.terrainData=Rivers.OriginalTerrainData;
                }
                var pathInput=Paths.CaptureInput(Paths.CaptureProfile(),out bounds,out graph);
                double guard=Rivers.CaptureProfile().TerrainCellGuard;
                var protectedPaths=pathInput.Reservations.Select(area=>{
                    if(!Rivers.TerrainBrush||area.Strength!=ReservationStrength.Hard)return area;
                    var b=area.Bounds;
                    return new AreaReservation(area.Id,area.OwnerId,new WorldBounds(b.MinX-guard,b.MinZ-guard,b.MaxX+guard,b.MaxZ+guard),area.Strength,area.Purpose,area.TransitionCost);
                });
                // Re-fingerprint the combined protections: changing either inspector invalidates
                // downstream publication. Duplicate reservation IDs are rejected by Core.
                return new PlanningInput(pathInput.Identity.Seed,pathInput.Identity.Revision,
                    pathInput.Identity.AlgorithmVersion,PathPlanner.ProfileVersion(Paths.CaptureProfile(),CapturePathBounds(bounds),graph),pathInput.Terrain,
                    World.TerrainContentFingerprint,new AnchorSnapshot(pathInput.AnchorSourceId,pathInput.AnchorSourceRevision,pathInput.Anchors),
                    pathInput.Landscape,protectedPaths.Concat(riverInput.Reservations)).BaseSnapshot;
            }
            finally{selected.terrainData=current;}
        }
        private System.Collections.Generic.IReadOnlyList<RiverRequest> Requests(PlanSnapshot input)=>Rivers.AutomaticSourceAndMouth
            ?RiverPlanner.CreateRequests(input.Anchors,input.Terrain.Heights):Rivers.Connections.Select(c=>c.Capture()).ToArray();
        /// <summary>Explicit freshness check before consuming results/partial rebuild. Includes live
        /// source heights, targets, regions, profiles, output heights and both plan publications.
        /// Captures full terrain data: call at boundaries, not every rendered frame.</summary>
        public bool ValidateCurrent(out string reason)
        {
            try
            {
                if(generationState!=GenerationRunState.Ready||!Paths||!Rivers||!Paths.GeneratedRoot||!Rivers.GeneratedRoot)throw new InvalidOperationException("Composition is not ready.");
                if(Paths.World!=World||Rivers.World!=World||builtCarve!=Rivers.CarveTerrainCopy||builtSpacing!=Paths.MeshSpacing
                    ||(Rivers.TerrainBrush&&(builtSedimentLayer!=Rivers.SedimentTerrainLayer||builtSedimentExposure!=Rivers.SedimentExposureDepth))
                    ||builtMaterials==null||!builtMaterials.SequenceEqual(Materials())||builtPathsTransform!=Paths.transform.localToWorldMatrix||builtRiversTransform!=Rivers.transform.localToWorldMatrix)
                    throw new InvalidOperationException("Module bindings, geometry settings or transforms changed; rebuild composition.");
                var input=CaptureSource(out var bounds,out var graph);
                if(Paths.LastPlan!=null)PathCrossingPermissions.RequireCurrent(Paths.LastPlan,LastSharedInput==null?null:CrossingPermissions?.Invoke(LastSharedInput));
                if(input.Identity.InputFingerprint!=builtInput||RiverPlanner.ProfileVersion(Rivers.CaptureProfile(),bounds,Requests(input))!=builtRiverProfile
                    ||World.TerrainContentFingerprint!=builtTerrain||Digest(Paths.PlanJson)!=builtPathsJson||Digest(Rivers.PlanJson)!=builtRiversJson)
                    throw new InvalidOperationException("Upstream inputs or module outputs changed; rebuild the composition before downstream decoration/navigation.");
                reason="Current source, plans and terrain outputs agree.";return true;
            }
            catch(Exception ex){reason=ex.Message;return false;}
        }
        private static string Digest(string value)
        {using(var sha=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value??"")));}
        private Material[] Materials()=>new[]{Paths.GroundMaterial,Paths.BridgeMaterial,Rivers.WaterMaterial,Rivers.BedMaterial,Rivers.BankMaterial};
        public void Clear() { if(generating)throw new InvalidOperationException("Cannot clear during composition generation.");generationState=GenerationRunState.Pending;activeStep="";generationTrace="Cleared in reverse order: Paths, Rivers.\n";if (Paths) Paths.Clear(); if (Rivers) Rivers.Clear(); }
    }
}
