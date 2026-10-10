using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TerraLoom.Core;
using TerraLoom.Core.Unity;
using UnityEngine;

namespace TerraLoom.Rivers.Unity
{
    [Serializable] public sealed class RiverConnectionSettings
    {
        public string Id = "river", SourceId = "source", MouthId = "mouth";
        public RiverRequest Capture() => new RiverRequest(Id, SourceId, MouthId);
    }
    [Serializable] public sealed class RiverRegionSettings
    { public string RegionId = "dry"; [Min(0)] public float CostPerMetre = 4; }
    [Serializable] public sealed class RiverProtectionSettings
    {
        public string Id = "protected"; public Vector2 Center, Size = new Vector2(10,10);
        public AreaReservation Capture() => new AreaReservation(Id, "user", new WorldBounds(Center.x - Size.x/2, Center.y - Size.y/2,
            Center.x + Size.x/2, Center.y + Size.y/2), ReservationStrength.Hard, ReservationPurpose.ProtectedArea);
    }

    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class TerraLoomRivers : MonoBehaviour
    {
        public TerraLoomWorld World;
        public bool GenerateOnStart;
        public bool AutomaticSourceAndMouth = true;
        public List<RiverConnectionSettings> Connections = new List<RiverConnectionSettings>();
        [Min(.1f)] public float Width = 4, Depth = .6f, BankWidth = 2, CellSize = 2;
        [Tooltip("Minimum sampled XZ centreline radius in metres. Zero selects max(0.25 m, half width + bank width). External endpoint directions and continuous curvature are not certified.")]
        [Min(0)] public float MinimumBendRadius;
        [Tooltip("Experimental: water metres below captured centre terrain, before the 2 cm offset. Requires carving; at most Depth. Actual staged terrain probes can reject curved shorelines. Zero preserves overlay geometry.")]
        [Min(0)] public float WaterInset;
        [Min(0)] public float MaximumSlope = .3f, BridgeClearance = 1;
        [Min(.05f)] public float SampleSpacing = .5f;
        public int MaximumNodes = 65536, TotalSearchBudget = 250000, TotalSampleBudget = 4000000;
        public List<RiverRegionSettings> RegionCosts = new List<RiverRegionSettings>();
        public List<RiverProtectionSettings> ProtectedAreas = new List<RiverProtectionSettings>();
        public Material WaterMaterial, BedMaterial, BankMaterial;
        public bool CarveTerrainCopy = true;
        [Tooltip("Experimental smooth terrain channel with only a water mesh. Requires carving, positive Water Inset and Bank Width. Bed/Bank materials are unused. Clear restores source terrain.")]
        public bool TerrainBrush;
        [Tooltip("Optional existing TerrainLayer index for sediment, -1 preserves source textures. Terrain Brush only. Painting uses the realized cut and smooth channel influence.")]
        public int SedimentTerrainLayer = -1;
        [Tooltip("Metres of actual terrain cut needed for full sediment exposure, still softened by the channel brush. Material only; does not change river heights or reservations.")]
        [Min(.001f)] public float SedimentExposureDepth = .15f;
        public int MaximumGeometryVertices = 1000000;
        [SerializeField, TextArea] private string diagnostics, planJson;
        [SerializeField] private GameObject generatedRoot;
        [SerializeField, HideInInspector] private Terrain carvedTerrain;
        [SerializeField, HideInInspector] private TerrainData originalData, carvedData;
        [SerializeField, HideInInspector] private bool ownsTransientData;
        public string Diagnostics => diagnostics;
        public string PlanJson => planJson;
        public GameObject GeneratedRoot => generatedRoot;
        public TerrainData OriginalTerrainData => originalData;
        public TerrainData CarvedTerrainData => carvedData;
        public RiverPlan LastPlan { get; private set; }
        private void Start() { if (GenerateOnStart && !generatedRoot && !Generate()) Debug.LogError(diagnostics, this); }

        public RiverProfile CaptureProfile() => new RiverProfile(Width, Depth, BankWidth, CellSize, MaximumSlope,
            SampleSpacing, MaximumNodes, TotalSearchBudget, TotalSampleBudget, bridgeClearance: BridgeClearance,
            regionCosts: RegionCosts.Select(r => new RiverRegionCost(r.RegionId, r.CostPerMetre)), allowExcavation: CarveTerrainCopy,
            minimumBendRadius: MinimumBendRadius, waterInset: WaterInset, terrainBrush: TerrainBrush,
            terrainCellGuard: CaptureTerrainSupportGuard());

        private double CaptureTerrainSupportGuard()
        {
            if(!TerrainBrush||!World||!World.Terrain||!World.Terrain.terrainData)return 0;
            var data=World.Terrain.terrainData;
            double guard=Math.Max((double)data.size.x/(data.heightmapResolution-1),(double)data.size.z/(data.heightmapResolution-1));
            if(SedimentTerrainLayer>=0)guard=Math.Max(guard,Math.Max((double)data.size.x/(data.alphamapWidth-1),(double)data.size.z/(data.alphamapHeight-1)));
            return guard;
        }

        public PlanSnapshot CaptureInput(out WorldBounds bounds, out IReadOnlyList<RiverRequest> requests)
        {
            if (!World || !World.Terrain || !World.Terrain.terrainData) throw new InvalidOperationException("Assign a Core world with terrain.");
            var selected = World.Terrain; var current = selected.terrainData;
            if (carvedData && (selected != carvedTerrain || current != carvedData))
                throw new InvalidOperationException("Terrain changed since carving. Clear Rivers before selecting a different terrain.");
            try
            {
                if (carvedData) selected.terrainData = originalData;
                var terrain = World.CaptureTerrain(); var anchors = World.CaptureAnchors();
                Vector3 origin = selected.transform.position, size = selected.terrainData.size;
                bounds = new WorldBounds(origin.x, origin.z, origin.x + size.x, origin.z + size.z);
                requests = AutomaticSourceAndMouth ? RiverPlanner.CreateRequests(anchors.Anchors, terrain.Heights) : Connections.Select(c => c.Capture()).ToArray();
                if (requests.Count == 0) throw new InvalidOperationException("At least two distinct targets are required.");
                int nx = (int)Math.Ceiling(size.x / CellSize), nz = (int)Math.Ceiling(size.z / CellSize);
                if ((long)(nx+1)*(nz+1) > Math.Min(MaximumNodes,65536)) throw new InvalidOperationException("Increase cell size: landscape capture exceeds its node budget.");
                var points = new List<WorldPoint>();
                for (int z = 0; z <= nz; z++) for (int x = 0; x <= nx; x++) points.Add(new WorldPoint(origin.x + size.x*x/nx, 0, origin.z + size.z*z/nz));
                var profile = CaptureProfile();
                return new PlanningInput(World.Seed, World.Revision, RiverPlanner.AlgorithmVersion,
                    RiverPlanner.ProfileVersion(profile, bounds, requests), terrain, World.TerrainContentFingerprint, anchors,
                    World.CaptureLandscape(points), ProtectedAreas.Select(p =>
                    {
                        var area=p.Capture();
                        if(!TerrainBrush)return area;
                        double gx=CaptureTerrainSupportGuard(),gz=gx;
                        var b=area.Bounds;
                        return new AreaReservation(area.Id,area.OwnerId,new WorldBounds(b.MinX-gx,b.MinZ-gz,b.MaxX+gx,b.MaxZ+gz),area.Strength,area.Purpose,area.TransitionCost);
                    })).BaseSnapshot;
            }
            finally { selected.terrainData = current; }
        }

        public bool Generate(CancellationToken cancellation = default)
        {
            try
            {
                cancellation.ThrowIfCancellationRequested();
                var input = CaptureInput(out var bounds, out var requests);
                return GenerateFromSnapshot(input, bounds, requests, cancellation);
            }
            catch (Exception ex) { diagnostics = ex.Message; return false; }
        }
        public bool GenerateFromSnapshot(PlanSnapshot input, WorldBounds bounds, IEnumerable<RiverRequest> requests,
            CancellationToken cancellation = default)
        {
            try
            {
                using(var prepared=PrepareFromSnapshot(input,bounds,requests,cancellation))
                { cancellation.ThrowIfCancellationRequested();prepared.CommitBindings();var cleanup=prepared.Complete();if(cleanup.Length!=0)diagnostics+="\n"+cleanup;return true; }
            }
            catch(Exception ex){diagnostics=ex is OperationCanceledException?"Cancelled; previous river retained.":ex.Message;return false;}
        }

        /// <summary>Builds inactive owned outputs without replacing terrain, collider or published geometry.
        /// The caller must dispose the handle; SealBindings accepts the result before Complete retires old resources.</summary>
        public PreparedPublication PrepareFromSnapshot(PlanSnapshot input, WorldBounds bounds, IEnumerable<RiverRequest> requests,
            CancellationToken cancellation=default)
        {
            GameObject staged = null; TerrainData stagedData = null;
            try
            {
                cancellation.ThrowIfCancellationRequested();
                if (!WaterMaterial || !TerrainBrush && (!BedMaterial || !BankMaterial)) throw new InvalidOperationException("Assign water material; mesh channels also need bed and bank materials.");
                if (!World || !World.Terrain) throw new InvalidOperationException("Assign a Core world.");
                if (Mathf.Abs(transform.localToWorldMatrix.determinant) < .000001f) throw new InvalidOperationException("River transform must have nonzero scale.");
                if(TerrainBrush && SedimentTerrainLayer < -1)throw new InvalidOperationException("Sediment layer must be -1 or an existing terrain layer index.");
                var profile = CaptureProfile(); var plan = RiverPlanner.Plan(input, profile, bounds, requests, cancellation);
                diagnostics = string.Join("\n", plan.Reports.Select(r => r.Request.Id + ": " + r.Outcome + " — " + r.Detail));
                if (!plan.Complete || plan.Routes.Count == 0) throw new InvalidOperationException(diagnostics);
                staged = new GameObject("Rivers (generated)"); staged.SetActive(false); staged.transform.SetParent(transform, false);
                int vertices = 0;
                foreach (var route in plan.Routes)
                foreach (RiverGeometryRole role in Enum.GetValues(typeof(RiverGeometryRole)))
                {
                    if(profile.TerrainBrush && role!=RiverGeometryRole.Water)continue;
                    var mesh = RiverGeometry.Build(route, Width, BankWidth, role,
                        MaximumGeometryVertices - vertices, cancellation, profile.WaterInset > 0, profile.TerrainBrush); vertices += mesh.vertexCount;
                    var child = new GameObject(route.Request.Id + " " + role); child.transform.SetParent(staged.transform, false);
                    var marker = child.AddComponent<RiverGeneratedGeometry>();
                    child.AddComponent<MeshFilter>().sharedMesh = mesh; marker.Initialize(role);
                    // Geometry is world-space, including under transformed module hosts.
                    var positions = mesh.vertices; for (int i = 0; i < positions.Length; i++) positions[i] = child.transform.InverseTransformPoint(positions[i]);
                    mesh.vertices = positions; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    child.AddComponent<MeshRenderer>().sharedMaterial = role == RiverGeometryRole.Water ? WaterMaterial : role == RiverGeometryRole.Bed ? BedMaterial : BankMaterial;
                    if (role != RiverGeometryRole.Water) child.AddComponent<MeshCollider>().sharedMesh = mesh;
                    marker.Restore();
                }
                Terrain target = World.Terrain; TerrainData basis = carvedData ? originalData : target.terrainData;
                if (carvedData && (target != carvedTerrain || target.terrainData != carvedData)) throw new InvalidOperationException("Selected terrain changed; clear before regeneration.");
                if (CarveTerrainCopy) stagedData = RiverTerrainCarver.Build(basis, target.transform.position, plan, profile, cancellation,
                    profile.TerrainBrush ? input.Reservations : null);
                if(profile.WaterInset>0)
                {
                    if(profile.TerrainBrush && SedimentTerrainLayer>=0)
                        diagnostics += "\nSediment layer "+SedimentTerrainLayer+": "+RiverTerrainPainter.Apply(
                            stagedData,basis,target.transform.position,plan,profile,SedimentTerrainLayer,input.Reservations,cancellation,SedimentExposureDepth)+" painted texels on owned terrain.";
                    foreach(var marker in staged.GetComponentsInChildren<RiverGeneratedGeometry>(true))
                    {
                        if(marker.Role!=RiverGeometryRole.Water)continue;
                        var filter=marker.GetComponent<MeshFilter>();int oldCount=filter.sharedMesh.vertexCount;
                        var clipped=RiverWaterTerrainClipper.Build(filter.sharedMesh,filter.transform.localToWorldMatrix,
                            stagedData,target.transform.position,MaximumGeometryVertices-vertices+oldCount,cancellation);
                        marker.DisposeOwnedMesh();filter.sharedMesh=clipped;marker.Initialize(RiverGeometryRole.Water);
                        vertices+=clipped.vertexCount-oldCount;
                    }
                    var inspection=RiverWaterTerrainInspection.Inspect(staged,stagedData,target.transform.position,cancellation);
                    if(!inspection.Acceptable)throw new InvalidOperationException("Inset shoreline intersects the actual carved terrain. Previous river retained. " + inspection);
                    diagnostics += "\n" + inspection;
                }
                cancellation.ThrowIfCancellationRequested();
                string serialized = RiverPlanStore.Save(plan);
                var result=new PreparedPublication(this,staged,stagedData,basis,target,plan,serialized);
                staged=null;stagedData=null;return result;
            }
            finally { try{DestroyRoot(staged);}finally{ReleaseData(stagedData);} }
        }
        /// <summary>Synchronous reversible binding swap. No external callbacks run during commit.
        /// Until SealBindings, Dispose restores the exact previous Terrain and Collider bindings.</summary>
        public sealed class PreparedPublication : IDisposable
        {
            private readonly TerraLoomRivers owner;
            private readonly GameObject root,oldRoot;
            private readonly TerrainData data,basis,oldData,oldOriginal,oldBinding,oldColliderBinding;
            private readonly Terrain target,oldTerrain;
            private readonly TerrainCollider collider;
            private readonly int colliderId;
            private readonly bool oldOwned,oldActive;
            private readonly RiverPlan plan,oldPlan;
            private readonly string json,oldJson;
            private bool committed,completed,disposed;
            internal PreparedPublication(TerraLoomRivers owner,GameObject root,TerrainData data,TerrainData basis,Terrain target,RiverPlan plan,string json)
            {
                this.owner=owner;this.root=root;this.data=data;this.basis=basis;this.target=target;this.plan=plan;this.json=json;
                oldRoot=owner.generatedRoot;oldData=owner.carvedData;oldOriginal=owner.originalData;oldTerrain=owner.carvedTerrain;
                oldOwned=owner.ownsTransientData;oldPlan=owner.LastPlan;oldJson=owner.planJson;oldActive=oldRoot&&oldRoot.activeSelf;
                oldBinding=target.terrainData;collider=target.GetComponent<TerrainCollider>();colliderId=collider?collider.GetInstanceID():0;oldColliderBinding=collider?collider.terrainData:null;
            }
            /// <summary>Captures the staged final grid directly, without temporarily binding it to the live world.</summary>
            public IHeightSource CaptureFinalHeights()
            {
                if(disposed)throw new ObjectDisposedException(nameof(PreparedPublication));
                var selected=data?data:basis;int n=selected.heightmapResolution;var raw=selected.GetHeights(0,0,n,n);
                var position=target.transform.position;var size=selected.size;var heights=new double[n*n];
                for(int z=0;z<n;z++)for(int x=0;x<n;x++)heights[z*n+x]=(double)position.y+(double)raw[z,x]*size.y;
                return new GridHeightSource(position.x,(double)position.x+size.x,position.z,(double)position.z+size.z,n,n,heights);
            }
            public void ValidateBeforeCommit()
            {
                if(disposed||committed)throw new InvalidOperationException("River preparation is no longer pending.");
                var currentCollider=target?target.GetComponent<TerrainCollider>():null;
                bool sameCollider=colliderId==0?!currentCollider:currentCollider&&currentCollider.GetInstanceID()==colliderId;
                if(!owner||!target||owner.World.Terrain!=target||target.terrainData!=oldBinding
                    ||!sameCollider
                    ||(collider&&collider.terrainData!=oldColliderBinding)||owner.generatedRoot!=oldRoot||owner.carvedData!=oldData)
                    throw new InvalidOperationException("River bindings changed after preparation.");
            }
            public void CommitBindings()
            {
                ValidateBeforeCommit();committed=true;
                owner.originalData=data?basis:null;owner.carvedTerrain=data?target:null;owner.carvedData=data;owner.ownsTransientData=data!=null;
                target.terrainData=data?data:basis;if(collider)collider.terrainData=data?data:basis;
                owner.generatedRoot=root;owner.LastPlan=plan;owner.planJson=json;
                if(oldRoot)oldRoot.SetActive(false);root.SetActive(true);
            }
            public void SealBindings()
            {
                if(disposed||!committed)throw new InvalidOperationException("Commit river bindings before completion.");
                completed=true;
            }
            private bool retired;
            public string Complete()
            {
                SealBindings();if(retired)return "";retired=true;var errors=new List<string>();
                try{DestroyRoot(oldRoot);}catch(Exception ex){errors.Add("River root retirement: "+ex.Message);}
                try{if(oldOwned)ReleaseData(oldData);}catch(Exception ex){errors.Add("River terrain retirement: "+ex.Message);}
                return string.Join("\n",errors);
            }
            public void Dispose()
            {
                if(disposed)return;if(completed){Complete();disposed=true;return;}disposed=true;
                if(committed)
                {
                    target.terrainData=oldBinding;if(collider)collider.terrainData=oldColliderBinding;
                    owner.generatedRoot=oldRoot;owner.originalData=oldOriginal;owner.carvedData=oldData;owner.carvedTerrain=oldTerrain;
                    owner.ownsTransientData=oldOwned;owner.LastPlan=oldPlan;owner.planJson=oldJson;
                    if(oldRoot)oldRoot.SetActive(oldActive);
                }
                try{DestroyRoot(root);}finally{ReleaseData(data);}
            }
        }

        public bool LoadPlanJson(string json, CancellationToken cancellation = default)
        {
            try
            {
                var input = CaptureInput(out var bounds, out var requests); var profile = CaptureProfile();
                var expected = RiverPlanner.Plan(input, profile, bounds, requests, cancellation);
                RiverPlanStore.Validate(json, expected);
                return GenerateFromSnapshot(input, bounds, requests, cancellation);
            }
            catch (Exception ex) { diagnostics = ex.Message; return false; }
        }
        public void MarkTerrainBaked(TerrainData data)
        {
            if (!carvedTerrain || !carvedData) throw new InvalidOperationException("No carved terrain to bake.");
            var previous = carvedData; carvedData = data; Assign(carvedTerrain,data); ownsTransientData = false; ReleaseData(previous);
        }
        public void Clear()
        {
            DestroyRoot(generatedRoot); generatedRoot = null; LastPlan = null; planJson = "";
            if (carvedTerrain && carvedTerrain.terrainData == carvedData) Assign(carvedTerrain, originalData);
            if (ownsTransientData) ReleaseData(carvedData);
            originalData = null; carvedData = null; carvedTerrain = null; ownsTransientData = false;
        }
        private static void Assign(Terrain terrain, TerrainData data)
        { terrain.terrainData = data; var collider = terrain.GetComponent<TerrainCollider>(); if (collider) collider.terrainData = data; }
        private static void ReleaseData(TerrainData data)
        {
            if (!data) return;
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(data)) return;
#endif
            if (Application.isPlaying) Destroy(data); else DestroyImmediate(data);
        }
        private static void DestroyRoot(GameObject root)
        {
            if (!root) return;
            foreach (var marker in root.GetComponentsInChildren<RiverGeneratedGeometry>(true)) marker.DisposeOwnedMesh();
            root.SetActive(false); if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
        }
        private void OnDestroy() { Clear(); }
    }
}
