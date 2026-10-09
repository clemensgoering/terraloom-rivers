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
        [Min(0)] public float MaximumSlope = .3f, BridgeClearance = 1;
        [Min(.05f)] public float SampleSpacing = .5f;
        public int MaximumNodes = 65536, TotalSearchBudget = 250000, TotalSampleBudget = 4000000;
        public List<RiverRegionSettings> RegionCosts = new List<RiverRegionSettings>();
        public List<RiverProtectionSettings> ProtectedAreas = new List<RiverProtectionSettings>();
        public Material WaterMaterial, BedMaterial, BankMaterial;
        public bool CarveTerrainCopy = true;
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
            minimumBendRadius: MinimumBendRadius);

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
                    World.CaptureLandscape(points), ProtectedAreas.Select(p => p.Capture())).BaseSnapshot;
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
            GameObject staged = null; TerrainData stagedData = null;
            try
            {
                cancellation.ThrowIfCancellationRequested();
                if (!WaterMaterial || !BedMaterial || !BankMaterial) throw new InvalidOperationException("Assign water, bed and bank materials.");
                if (!World || !World.Terrain) throw new InvalidOperationException("Assign a Core world.");
                if (Mathf.Abs(transform.localToWorldMatrix.determinant) < .000001f) throw new InvalidOperationException("River transform must have nonzero scale.");
                var profile = CaptureProfile(); var plan = RiverPlanner.Plan(input, profile, bounds, requests, cancellation);
                diagnostics = string.Join("\n", plan.Reports.Select(r => r.Request.Id + ": " + r.Outcome + " — " + r.Detail));
                if (!plan.Complete || plan.Routes.Count == 0) return false;
                staged = new GameObject("Rivers (generated)"); staged.SetActive(false); staged.transform.SetParent(transform, false);
                int vertices = 0;
                foreach (var route in plan.Routes)
                foreach (RiverGeometryRole role in Enum.GetValues(typeof(RiverGeometryRole)))
                {
                    var mesh = RiverGeometry.Build(route, Width, BankWidth, role,
                        MaximumGeometryVertices - vertices, cancellation); vertices += mesh.vertexCount;
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
                if (CarveTerrainCopy) stagedData = RiverTerrainCarver.Build(basis, target.transform.position, plan, profile, cancellation);
                cancellation.ThrowIfCancellationRequested();
                string serialized = RiverPlanStore.Save(plan);
                var oldRoot = generatedRoot; var oldData = carvedData; bool oldOwned = ownsTransientData;
                if (oldData && carvedTerrain && carvedTerrain.terrainData == oldData) Assign(carvedTerrain, originalData);
                originalData = stagedData ? basis : null; carvedTerrain = stagedData ? target : null; carvedData = stagedData; ownsTransientData = stagedData != null;
                if (stagedData) Assign(target, stagedData);
                generatedRoot = staged; staged.SetActive(true); staged = null; stagedData = null;
                LastPlan = plan; planJson = serialized;
                DestroyRoot(oldRoot); if (oldOwned) ReleaseData(oldData);
                return true;
            }
            catch (Exception ex) { diagnostics = ex is OperationCanceledException ? "Cancelled; previous river retained." : ex.Message; return false; }
            finally { DestroyRoot(staged); ReleaseData(stagedData); }
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
