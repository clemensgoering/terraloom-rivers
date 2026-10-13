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
    /// <summary>Optional shared publication host. Core, Paths and Rivers remain independent packages.</summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(300)]
    public sealed class TerraLoomIntegration : MonoBehaviour
    {
        public TerraLoomWorld World;
        public TerraLoomPaths Paths;
        public TerraLoomRivers Rivers;
        public CompositionRecipe RecipeAsset;
        public RegionDecoration RecipeDecoration;
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
        [SerializeField, HideInInspector] private string presentationWarnings;
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
        /// <summary>Non-blocking review of the last committed route geometry, retained with a saved scene.</summary>
        public string PresentationWarnings => presentationWarnings;
        public GenerationRunState GenerationState => generationState;
        public string ActiveStep => activeStep;
        public string GenerationTrace => generationTrace;
        /// <summary>External example policy; recalculated at planning and each publication boundary.
        /// A saved path plan never supplies this authority. Null preserves the legacy example.</summary>
        public Func<PlanSnapshot,PathCrossingPermissions> CrossingPermissions;
        [SerializeField, HideInInspector] private MonoBehaviour crossingPermissionSource;
        /// <summary>Explicit scene authority, queried at planning and publication boundaries.
        /// A source must implement ICurrentCrossingPermissionSource. Null means no grants.</summary>
        public MonoBehaviour CrossingPermissionSource
        {
            get => crossingPermissionSource;
            set => crossingPermissionSource = value;
        }
        public bool HasCrossingPermissionAuthority => crossingPermissionSource || CrossingPermissions != null;
        public PathCrossingPermissions CurrentCrossingPermissions(PlanSnapshot snapshot)
        {
            if (CrossingPermissions != null && crossingPermissionSource)
                throw new InvalidOperationException("Choose either a code crossing delegate or a scene permission source, not both.");
            if (crossingPermissionSource)
            {
                if (!(crossingPermissionSource is ICurrentCrossingPermissionSource source) || !crossingPermissionSource.isActiveAndEnabled)
                    throw new InvalidOperationException("Crossing Permission Source must be an enabled component implementing ICurrentCrossingPermissionSource.");
                var permissions = source.GetCurrentPermissions(snapshot);
                permissions?.ValidateIdentity(snapshot.Identity);
                return permissions;
            }
            var delegated = CrossingPermissions?.Invoke(snapshot);
            delegated?.ValidateIdentity(snapshot.Identity);
            return delegated;
        }
        public PlanSnapshot LastSharedInput { get; private set; }
        public PlanSnapshot LastSourceInput { get; private set; }
        public PathPlan LastPathAttempt { get; private set; }
        /// <summary>The last committed crossing context, distinct from a failed planning attempt.</summary>
        public PlanSnapshot PublishedSharedInput { get; private set; }
        /// <summary>Consumer diagnostic seam for deterministic transaction fault tests. Leave null in production.
        /// Boundaries: river-prepared, before-commit, river-bound, all-bound. Never supplied to product adapters.</summary>
        public Action<string> PublicationBoundary;
        /// <summary>Optional retirement callback; failures are post-commit diagnostics, with default cleanup fallback.</summary>
        public Action<GameObject> PathRetirement;
        private bool generating;
        private WorldSourcePublication ownedSourcePublication;
        private static GenerationSchedule Schedule() => new GenerationSchedule(new[] {
            new GenerationStep("01-capture-source",Array.Empty<string>(),new[]{"source-snapshot"}),
            new GenerationStep("02-plan-water",new[]{"source-snapshot"},new[]{"river-plan","water-offers"}),
            new GenerationStep("03-plan-routes",new[]{"water-offers"},new[]{"path-plan"}),
            new GenerationStep("04-publish-earthworks",new[]{"river-plan","path-plan"},new[]{"carved-terrain","river-geometry"}),
            new GenerationStep("05-publish-walkable",new[]{"carved-terrain","path-plan"},new[]{"walkable-geometry"}) });
        private void Awake()
        {
            if(!Application.isPlaying||!GenerateOnStart)return;
            // Awake runs before every Start, independent of hierarchy and execution order.
            if(World)World.AutoGenerateOnStart=false;
            if(Paths)Paths.GenerateOnStart=false;
            if(Rivers)Rivers.GenerateOnStart=false;
        }
        private void Start() { if(Application.isPlaying && GenerateOnStart && !(RecipeAsset?GenerateRecipe(RecipeAsset,RecipeDecoration):Generate())) Debug.LogError(diagnostics,this); }
        private void OnDestroy()
        {
            // The host owns the publication's lifetime. Disabling it is temporary and must
            // keep the world; destroying it releases geometry and the borrowed source binding.
            if(generating)return;
            try{Clear();}
            catch(Exception ex){Debug.LogException(ex,this);}
        }
        public bool Generate(CancellationToken cancellation = default) => GenerateCore(null,cancellation);

        /// <summary>Reconstructs source inputs from a versioned asset using the shared prepared publication.
        /// Initial creation bootstraps an owned Core terrain; later reloads retain the previous world on failure.</summary>
        public bool GenerateRecipe(CompositionRecipe recipe,RegionDecoration decoration,CancellationToken cancellation=default)
        {
            CompositionModuleSettings previous=null;
            bool accepted=false;
            try
            {
                if(!recipe)throw new InvalidOperationException("Assign a composition recipe asset.");
                var settings=recipe.Snapshot();cancellation.ThrowIfCancellationRequested();
                if(!World||!Paths||!Rivers||!decoration||Paths.World!=World||Rivers.World!=World||decoration.World!=World)
                    throw new InvalidOperationException("Assign Core, Paths, Rivers and decoration bound to the same world.");
                if(World.Terrain&&!World.OwnsTerrain)
                    throw new InvalidOperationException("Seed recipes require Core-owned terrain. Use Generate for supplied terrain.");
                previous=CompositionModuleSettings.Capture(this,decoration);
                settings.Modules.Apply(this,decoration);
                if(!World.Terrain)
                {
                    var s=settings;
                    World.Seed=s.Seed;World.Recipe=s.TerrainRecipe;World.Resolution=s.Resolution;World.TerrainSize=s.Size;World.Frequency=s.Frequency;
                    World.GenerateTerrain();
                }
                using(var request=recipe.Prepare(World,decoration,settings))accepted=GenerateSeedRequest(request,cancellation);
                if(accepted&&World.Terrain)World.Terrain.materialTemplate=settings.Modules.Core.GeneratedTerrainMaterial;
                return accepted;
            }
            catch(Exception ex)
            {
                generationState=ex is OperationCanceledException?GenerationRunState.Cancelled:GenerationRunState.Failed;
                activeStep="recipe-preflight";diagnostics=ex.Message;return false;
            }
            finally
            {
                // Source/module publication owns geometry rollback. Restore authoring parameters
                // as well when preparation or publication fails; shared assets are never mutated.
                if(!accepted&&previous!=null)previous.Apply(this,decoration);
            }
        }

        /// <summary>Rebuilds a Core-owned source without using the currently rendered River carve
        /// as input. A failed plan keeps the previous source, river, path and collider bindings.</summary>
        public bool GenerateOwnedSource(CancellationToken cancellation = default)
            => GenerateOwnedSource(World ? World.Seed : 0,cancellation);

        /// <summary>Requests a seed without changing the published World seed until every
        /// terrain, river and path binding commits. Failed attempts retain the old identity.</summary>
        public bool GenerateOwnedSource(int requestedSeed,CancellationToken cancellation = default)
        {
            var publisher=EnsureOwnedSourcePublication();
            bool seedAnchors=World.UseSeedAnchors;
            var manualAnchors=World.ManualAnchors;
            return GenerateWithSource(()=>publisher.PrepareFreshSource(requestedSeed,seedAnchors,manualAnchors,()=>
            {
                var data=World.CreateGeneratedTerrainData(requestedSeed);
                try
                {
                    var profiles=UnityEngine.Object.FindObjectsByType<RegionDecoration>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                        .Where(d=>d.World==World&&d.Profiles!=null).SelectMany(d=>d.Profiles)
                        .Where(p=>p&&p.TerrainLayer).GroupBy(p=>p.RegionId,StringComparer.Ordinal)
                        .Select(group=>group.Distinct().Count()==1?group.First():throw new InvalidOperationException("Conflicting terrain profiles for region "+group.Key))
                        .ToArray();
                    if(profiles.Length>0)RegionSurfacePainter.PaintUnboundTerrain(World,data,profiles);
                    return data;
                }
                catch
                {
                    if(Application.isPlaying)Destroy(data);else DestroyImmediate(data);
                    throw;
                }
            },cancellation),cancellation);
        }

        private WorldSourcePublication EnsureOwnedSourcePublication()
        {
            if(!World||!World.OwnsTerrain||!World.Terrain||!World.Terrain.terrainData||!Rivers)
                throw new InvalidOperationException("An owned Core terrain and Rivers are required; supplied terrain remains unchanged.");
            var original=Rivers.CarvedTerrainData?Rivers.OriginalTerrainData:World.Terrain.terrainData;
            if(!original||(Rivers.CarvedTerrainData&&World.Terrain.terrainData!=Rivers.CarvedTerrainData))
                throw new InvalidOperationException("The River carve and Core source are not consistently bound.");
            if(ownedSourcePublication==null)ownedSourcePublication=new WorldSourcePublication(World,original,original);
            else if(ownedSourcePublication.CurrentSource&&original!=ownedSourcePublication.CurrentSource)
                throw new InvalidOperationException("The Core source changed outside this composition. Clear before rebuilding.");
            return ownedSourcePublication;
        }

        public bool GenerateSeedRequest(PreparedSeededWorldRequest request,CancellationToken cancellation=default)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            var publisher=EnsureOwnedSourcePublication();
            return GenerateCore(()=>publisher.PrepareFreshSource(request.Seed,false,request.Anchors,request.Areas,
                request.Recipe,request.Resolution,request.Size,request.Frequency,()=>request.TakeTerrain(),cancellation),
                cancellation,request);
        }

        /// <summary>Prepares an owned source inside the same lifecycle as both module outputs.</summary>
        public bool GenerateWithSource(Func<WorldSourcePublication.PreparedSource> prepareSource,CancellationToken cancellation=default)
            => GenerateCore(prepareSource??throw new ArgumentNullException(nameof(prepareSource)),cancellation);

        private bool GenerateCore(Func<WorldSourcePublication.PreparedSource> prepareSource,CancellationToken cancellation,
            PreparedSeededWorldRequest request=null)
        {
            if(generating)throw new InvalidOperationException("Composition generation cannot reenter an active publication.");
            generating=true;
            var run=new GenerationRun(Schedule());generationTrace="";generationState=GenerationRunState.Pending;activeStep="preflight";
            TerraLoomRivers.PreparedPublication preparedRiver=null;
            TerraLoomPaths.PreparedPublication preparedPaths=null;
            WorldSourcePublication.PreparedSource source=null;
            RegionDecoration.PreparedDecoration preparedDecoration=null;
            var oldPathConnections=Paths?Paths.Connections:null;
            var oldRiverConnections=Rivers?Rivers.Connections:null;
            bool oldAutomaticPaths=Paths&&Paths.AutomaticNetwork,oldAutomaticRivers=Rivers&&Rivers.AutomaticSourceAndMouth;
            bool configApplied=false;
            var requestedPaths=request?.PathConnections.Select(c=>c.Capture()).ToArray();
            var requestedRivers=request?.RiverConnections.Select(c=>c.Capture()).ToArray();
            var nextPathConnections=request==null?null:new System.Collections.Generic.List<PathConnectionSettings>(request.PathConnections);
            var nextRiverConnections=request==null?null:new System.Collections.Generic.List<RiverConnectionSettings>(request.RiverConnections);
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
                if(prepareSource!=null)
                {
                    source=prepareSource();
                    if(source==null||source.World!=World)throw new InvalidOperationException("Source factory must prepare this world.");
                    PublicationBoundary?.Invoke("source-prepared");
                    cancellation.ThrowIfCancellationRequested();
                }
                // Capture the unchanged source while keeping previous geometry available on planning failure.
                PlanSnapshot input=null,shared=null;WorldBounds bounds=default;
                System.Collections.Generic.IReadOnlyList<PathConnection> graph=null;
                System.Collections.Generic.IReadOnlyList<RiverRequest> requests=null;
                RiverPlan riverPlan=null;PathPlan pathPlan=null;string plannedRiverProfile=null;
                var plannedMaterials=Materials();var plannedPathTransform=Paths.transform.localToWorldMatrix;var plannedRiverTransform=Rivers.transform.localToWorldMatrix;
                float plannedSpacing=Paths.MeshSpacing,plannedExposure=Rivers.SedimentExposureDepth;int plannedLayer=Rivers.SedimentTerrainLayer,plannedPathBudget=Paths.MaximumGeometryVertices,plannedRiverBudget=Rivers.MaximumGeometryVertices;
                bool plannedCarve=Rivers.CarveTerrainCopy;
                Step("01-capture-source",()=>{input=CaptureSource(out bounds,out graph,source,requestedPaths,requestedRivers);LastSourceInput=input;});
                Step("02-plan-water",()=>{
                    requests=Requests(input,requestedRivers);plannedRiverProfile=RiverPlanner.ProfileVersion(Rivers.CaptureProfile(source?.Data),bounds,requests);riverPlan=RiverPlanner.Plan(input,Rivers.CaptureProfile(source?.Data),bounds,requests,cancellation);
                    if(!riverPlan.Complete)throw new InvalidOperationException(string.Join("\n",riverPlan.Reports.Select(r=>r.Request.Id+": "+r.Outcome+" "+r.Detail)));
                    var offer=riverPlan.ToContribution("rivers",input.Identity);
                    shared=new PlanSnapshot(input.Identity,input.Terrain,new AnchorSnapshot(input.AnchorSourceId,input.AnchorSourceRevision,input.Anchors),
                        input.Landscape,input.Reservations.Concat(offer.Reservations),offer.Waters,offer.Crossings);
                });
                Step("03-plan-routes",()=>{
                    LastSharedInput=shared;
                    pathPlan=PathPlanner.Plan(shared,Paths.CaptureProfile(),CapturePathBounds(bounds),graph,cancellation,CurrentCrossingPermissions(shared));
                    LastPathAttempt=pathPlan;
                    if(!pathPlan.Complete)throw new InvalidOperationException(string.Join("\n",pathPlan.Reports.Select(r=>r.Connection.Id+": "+r.Outcome+" "+r.Detail)));
                });
                Step("04-publish-earthworks",()=>{
                    preparedRiver=Rivers.PrepareFromSnapshot(input,bounds,requests,cancellation,source);
                    PublicationBoundary?.Invoke("river-prepared");
                });
                Step("05-publish-walkable",()=>{
                    preparedPaths=Paths.PrepareFromSnapshot(shared,CapturePathBounds(bounds),preparedRiver.CaptureFinalHeights(),graph,cancellation,
                        currentPermissions:CrossingPermissions==null&&!crossingPermissionSource?(Func<PathCrossingPermissions>)null:()=>CurrentCrossingPermissions(shared));
                    // The final path is replanned against the carved grid. Clear only the
                    // unpublished terrain copy, using that exact path and river geometry.
                    if(Rivers.CarveTerrainCopy)
                        CompositionVegetationClearance.Apply(preparedRiver.FinalTerrainData,
                            source?.Data??(Rivers.CarvedTerrainData?Rivers.OriginalTerrainData:World.Terrain.terrainData),
                            World.Terrain.transform.position,riverPlan,Rivers.CaptureProfile(source?.Data),
                            Paths.LastAttemptPlan,Paths.CaptureProfile(),cancellation);
                    if(request!=null)
                    {
                        preparedDecoration=request.Decoration.PrepareCandidate(preparedRiver.FinalTerrainData,request.Seed,
                            request.Areas,request.Profiles,request.DecorationCount,request.Selection,
                            BuildExclusions(riverPlan,pathPlan),cancellation);
                        PublicationBoundary?.Invoke("decoration-prepared");
                    }
                    PublicationBoundary?.Invoke("before-commit");
                    cancellation.ThrowIfCancellationRequested();
                    var current=CaptureSource(out var liveBounds,out var liveGraph,source,requestedPaths,requestedRivers);
                    if(current.Identity.InputFingerprint!=input.Identity.InputFingerprint
                        ||RiverPlanner.ProfileVersion(Rivers.CaptureProfile(source?.Data),liveBounds,Requests(current,requestedRivers))!=plannedRiverProfile
                        ||!plannedMaterials.SequenceEqual(Materials())||plannedPathTransform!=Paths.transform.localToWorldMatrix||plannedRiverTransform!=Rivers.transform.localToWorldMatrix
                        ||plannedSpacing!=Paths.MeshSpacing||plannedExposure!=Rivers.SedimentExposureDepth||plannedLayer!=Rivers.SedimentTerrainLayer
                        ||plannedPathBudget!=Paths.MaximumGeometryVertices||plannedRiverBudget!=Rivers.MaximumGeometryVertices||plannedCarve!=Rivers.CarveTerrainCopy)
                        throw new InvalidOperationException("Source changed while preparing composition.");
                    source?.ValidateBeforeCommit();preparedRiver.ValidateBeforeCommit();preparedPaths.ValidateBeforeCommit();
                    cancellation.ThrowIfCancellationRequested();
                    // Synchronous host swap. Old resources remain alive through both bindings and metadata capture.
                    preparedRiver.CommitBindings();PublicationBoundary?.Invoke("river-bound");
                    cancellation.ThrowIfCancellationRequested();
                    preparedPaths.CommitBindings();
                    if(source!=null)
                    {
                        PublicationBoundary?.Invoke("before-source-bind");cancellation.ThrowIfCancellationRequested();
                        source.CommitComposedBindings(preparedRiver.FinalTerrainData);
                        PublicationBoundary?.Invoke("source-bound");cancellation.ThrowIfCancellationRequested();
                    }
                    PublicationBoundary?.Invoke("all-bound");
                    if(preparedDecoration!=null)
                    {
                        preparedDecoration.Commit();
                        configApplied=true;
                        Paths.Connections=nextPathConnections;Rivers.Connections=nextRiverConnections;
                        Paths.AutomaticNetwork=false;Rivers.AutomaticSourceAndMouth=false;
                    }
                    builtTerrain=World.TerrainContentFingerprint;
                    builtPathsJson=Digest(Paths.PlanJson);builtRiversJson=Digest(Rivers.PlanJson);
                    builtInput=input.Identity.InputFingerprint;builtRiverProfile=plannedRiverProfile;
                    builtCarve=plannedCarve;builtSpacing=plannedSpacing;builtMaterials=plannedMaterials;
                    builtSedimentLayer=plannedLayer;builtSedimentExposure=plannedExposure;
                    builtPathsTransform=plannedPathTransform;builtRiversTransform=plannedRiverTransform;
                    preparedPaths.SealBindings();preparedRiver.SealBindings();source?.SealBindings();publicationCommitted=true;
                    PublishedSharedInput=shared;
                    cleanupDiagnostics=preparedPaths.Complete(PathRetirement)+"\n"+preparedRiver.Complete();
                    if(source!=null)cleanupDiagnostics+="\n"+source.Complete();
                    preparedDecoration?.Complete();
                    // Once both outputs are committed cancellation belongs to the next run.
                    // Decorations are invalidated only after successful collective publication.
                    foreach(var decoration in UnityEngine.Object.FindObjectsByType<RegionDecoration>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                        if(decoration.World==World&&decoration!=request?.Decoration)
                            try{decoration.Clear();}catch(Exception ex){cleanupDiagnostics+="\nDecoration invalidation: "+ex.Message;}
                });
                activeStep="";
                diagnostics="Core + Rivers + Paths: "+riverPlan.Routes.Count+" river, "+pathPlan.Routes.Count+" path; "+CompositionPresentationReview.BridgeStatus(pathPlan,Paths)+".";
                try{presentationWarnings=CompositionPresentationReview.Warnings(pathPlan,World,Paths,Rivers);}
                catch(Exception ex){presentationWarnings="Presentation review unavailable: "+ex.Message;}
                if(!string.IsNullOrWhiteSpace(cleanupDiagnostics))diagnostics+="\nPost-commit cleanup: "+cleanupDiagnostics.Trim();
                return true;
            }
            catch (Exception ex)
            {
                if(configApplied&&!publicationCommitted)
                {Paths.Connections=oldPathConnections;Rivers.Connections=oldRiverConnections;
                    Paths.AutomaticNetwork=oldAutomaticPaths;Rivers.AutomaticSourceAndMouth=oldAutomaticRivers;}

                generationState=ex is OperationCanceledException?GenerationRunState.Cancelled:GenerationRunState.Failed;
                diagnostics="Stage "+activeStep+": "+ex.Message+(publicationCommitted?" New composition committed; post-commit failure.":" Previous composition resources retained; attempt is not ready.");return false;
            }
            finally { try{preparedDecoration?.Dispose();}finally{try{preparedPaths?.Dispose();}finally{try{preparedRiver?.Dispose();}finally{try{source?.Dispose();}finally{generating=false;}}}} }
        }
        private PlanSnapshot CaptureSource(out WorldBounds bounds,out System.Collections.Generic.IReadOnlyList<PathConnection> graph,
            WorldSourcePublication.PreparedSource source=null,System.Collections.Generic.IReadOnlyList<PathConnection> requestedPaths=null,
            System.Collections.Generic.IReadOnlyList<RiverRequest> requestedRivers=null)
        {
            if(!World||!World.Terrain||!World.Terrain.terrainData)throw new InvalidOperationException("Generate/assign terrain before composition.");
            // Both adapters capture the explicit source without rebinding the live Terrain.
            var riverInput=Rivers.CaptureInput(out _,out _,source,requestedRivers);
            var selected=World.Terrain;var current=selected.terrainData;
                if(Rivers.CarvedTerrainData)
                {
                    if(current!=Rivers.CarvedTerrainData||!Rivers.OriginalTerrainData)throw new InvalidOperationException("Selected terrain changed. Clear the composition first.");
                }
                var data=source!=null?source.Data:Rivers.CarvedTerrainData?Rivers.OriginalTerrainData:current;
                var pathInput=Paths.CaptureInput(Paths.CaptureProfile(),out bounds,out graph,source,data,requestedPaths);
                double guard=Rivers.CaptureProfile(data).TerrainCellGuard;
                var protectedPaths=pathInput.Reservations.Select(area=>{
                    if(!Rivers.TerrainBrush||area.Strength!=ReservationStrength.Hard)return area;
                    var b=area.Bounds;
                    return new AreaReservation(area.Id,area.OwnerId,new WorldBounds(b.MinX-guard,b.MinZ-guard,b.MaxX+guard,b.MaxZ+guard),area.Strength,area.Purpose,area.TransitionCost);
                });
                // Re-fingerprint the combined protections: changing either inspector invalidates
                // downstream publication. Duplicate reservation IDs are rejected by Core.
                return new PlanningInput(pathInput.Identity.Seed,pathInput.Identity.Revision,
                    pathInput.Identity.AlgorithmVersion,PathPlanner.ProfileVersion(Paths.CaptureProfile(),CapturePathBounds(bounds),graph),pathInput.Terrain,
                    World.CaptureTerrainFingerprint(data),new AnchorSnapshot(pathInput.AnchorSourceId,pathInput.AnchorSourceRevision,pathInput.Anchors),
                    pathInput.Landscape,protectedPaths.Concat(riverInput.Reservations)).BaseSnapshot;
        }
        private System.Collections.Generic.IReadOnlyList<RiverRequest> Requests(PlanSnapshot input,
            System.Collections.Generic.IReadOnlyList<RiverRequest> requested=null)=>requested??(Rivers.AutomaticSourceAndMouth
            ?RiverPlanner.CreateRequests(input.Anchors,input.Terrain.Heights):Rivers.Connections.Select(c=>c.Capture()).ToArray());

        private Rect[] BuildExclusions(RiverPlan riverPlan,PathPlan pathPlan)
        {
            var result=new System.Collections.Generic.List<Rect>();
            void Add(Vector3[] points,float margin)
            {
                for(int i=1;i<points.Length;i++)result.Add(Rect.MinMaxRect(
                    Mathf.Min(points[i-1].x,points[i].x)-margin,Mathf.Min(points[i-1].z,points[i].z)-margin,
                    Mathf.Max(points[i-1].x,points[i].x)+margin,Mathf.Max(points[i-1].z,points[i].z)+margin));
            }
            foreach(var river in riverPlan.Routes)
                Add(river.WaterPolyline.Select(p=>new Vector3((float)p.X,(float)p.Y,(float)p.Z)).ToArray(),Rivers.Width*.5f+Rivers.BankWidth+4);
            foreach(var route in pathPlan.Routes)
                Add(route.Waypoints.Select(p=>new Vector3((float)p.X,(float)p.Y,(float)p.Z)).ToArray(),Paths.Width*.5f+4);
            return result.ToArray();
        }
        /// <summary>Explicit freshness check before consuming results/partial rebuild. Includes live
        /// source heights, targets, regions, profiles, output heights and both plan publications.
        /// Captures full terrain data: call at boundaries, not every rendered frame.</summary>
        public bool ValidateCurrent(out string reason)
        {
            try
            {
                // GenerationState describes the most recent attempt. A failed retry can leave a
                // previously published world intact; validate that publication independently.
                if((generationState!=GenerationRunState.Ready&&string.IsNullOrEmpty(builtInput))||!Paths||!Rivers||!Paths.GeneratedRoot||!Rivers.GeneratedRoot)throw new InvalidOperationException("Composition is not ready.");
                if(Paths.World!=World||Rivers.World!=World||builtCarve!=Rivers.CarveTerrainCopy||builtSpacing!=Paths.MeshSpacing
                    ||(Rivers.TerrainBrush&&(builtSedimentLayer!=Rivers.SedimentTerrainLayer||builtSedimentExposure!=Rivers.SedimentExposureDepth))
                    ||builtMaterials==null||!builtMaterials.SequenceEqual(Materials())||builtPathsTransform!=Paths.transform.localToWorldMatrix||builtRiversTransform!=Rivers.transform.localToWorldMatrix)
                    throw new InvalidOperationException("Module bindings, geometry settings or transforms changed; rebuild composition.");
                var input=CaptureSource(out var bounds,out var graph);
                if(Paths.LastPlan!=null)PathCrossingPermissions.RequireCurrent(Paths.LastPlan,PublishedSharedInput==null?null:CurrentCrossingPermissions(PublishedSharedInput));
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
        /// <summary>Releases generated decoration, geometry and owned source in reverse order.
        /// This runtime operation is irreversible; recording scene fields alone cannot restore disposed resources.</summary>
        public void Clear()
        {
            if(generating)throw new InvalidOperationException("Cannot clear during composition generation.");
            generationState=GenerationRunState.Pending;activeStep="";
            generationTrace="Cleared in reverse order: decoration, Paths, Rivers, owned Core source.\n";
            presentationWarnings="";
            PublishedSharedInput=null;
            foreach(var decoration in UnityEngine.Object.FindObjectsByType<RegionDecoration>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(decoration.World==World&&World)decoration.Clear();
            if(Paths)Paths.Clear();if(Rivers)Rivers.Clear();
            if(ownedSourcePublication!=null)
            {
                ownedSourcePublication.Clear();ownedSourcePublication.Dispose();ownedSourcePublication=null;
            }
        }
    }
}
