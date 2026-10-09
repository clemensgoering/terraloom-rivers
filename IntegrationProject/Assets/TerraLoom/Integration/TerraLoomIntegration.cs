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
        [SerializeField, TextArea] private string diagnostics;
        public string Diagnostics => diagnostics;
        private void Start() { if (GenerateOnStart && !Generate()) Debug.LogError(diagnostics,this); }
        public bool Generate(CancellationToken cancellation = default)
        {
            try
            {
                if (!World || !Paths || !Rivers || Paths.World != World || Rivers.World != World) throw new InvalidOperationException("All modules must share this Core world.");
                cancellation.ThrowIfCancellationRequested();
                if (!Paths.GroundMaterial || !Paths.BridgeMaterial || !Rivers.WaterMaterial || !Rivers.BedMaterial || !Rivers.BankMaterial)
                    throw new InvalidOperationException("Bind all path and river materials before rebuilding.");
                // Capture the unchanged source while keeping previous geometry available on planning failure.
                var selected=World.Terrain; var current=selected.terrainData;
                PlanSnapshot input; WorldBounds bounds; System.Collections.Generic.IReadOnlyList<PathConnection> graph;
                try
                {
                    if (Rivers.CarvedTerrainData)
                    {
                        if (current != Rivers.CarvedTerrainData) throw new InvalidOperationException("Selected terrain changed. Clear the composition first.");
                        selected.terrainData=Rivers.OriginalTerrainData;
                    }
                    input = Paths.CaptureInput(Paths.CaptureProfile(),out bounds,out graph);
                }
                finally { selected.terrainData=current; }
                var requests = Rivers.AutomaticSourceAndMouth ? RiverPlanner.CreateRequests(input.Anchors,input.Terrain.Heights)
                    : Rivers.Connections.Select(c=>c.Capture()).ToArray();
                var riverPlan = RiverPlanner.Plan(input,Rivers.CaptureProfile(),bounds,requests,cancellation);
                if (!riverPlan.Complete) throw new InvalidOperationException(string.Join("\n",riverPlan.Reports.Select(r=>r.Request.Id+": "+r.Outcome+" "+r.Detail)));
                var offer = riverPlan.ToContribution("rivers",input.Identity);
                var shared = new PlanSnapshot(input.Identity,input.Terrain,new AnchorSnapshot(input.AnchorSourceId,input.AnchorSourceRevision,input.Anchors),
                    input.Landscape,input.Reservations.Concat(offer.Reservations),offer.Waters,offer.Crossings);
                var pathPlan = PathPlanner.Plan(shared,Paths.CaptureProfile(),bounds,graph,cancellation);
                if (!pathPlan.Complete) throw new InvalidOperationException(string.Join("\n",pathPlan.Reports.Select(r=>r.Connection.Id+": "+r.Outcome+" "+r.Detail)));
                if (!Rivers.GenerateFromSnapshot(input,bounds,requests,cancellation)) throw new InvalidOperationException(Rivers.Diagnostics);
                if (!Paths.GenerateFromSnapshot(shared,bounds,graph,cancellation)) throw new InvalidOperationException(Paths.Diagnostics);
                diagnostics="Core + Rivers + Paths: "+riverPlan.Routes.Count+" river, "+pathPlan.Routes.Count+" path; "+pathPlan.Routes.Sum(r=>r.Surfaces.Count(s=>s==PathSurface.Bridge))+" negotiated bridge deck(s).";
                return true;
            }
            catch (Exception ex) { diagnostics=ex.Message; return false; }
        }
        public void Clear() { if (Paths) Paths.Clear(); if (Rivers) Rivers.Clear(); }
    }
}
