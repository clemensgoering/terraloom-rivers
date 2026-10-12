using System;
using System.Collections.Generic;
using TerraLoom.Core.Unity;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>Schema 2 planner, geometry and asset bindings. Contains inputs only, never published roots or diagnostics.</summary>
    [Serializable]
    public sealed class CompositionModuleSettings
    {
        public CoreRecipeParameters Core=new CoreRecipeParameters();
        public PathRecipeParameters Paths=new PathRecipeParameters();
        public RiverRecipeParameters Rivers=new RiverRecipeParameters();
        public DecorationRecipeParameters Decoration=new DecorationRecipeParameters();
        public bool RestrictPathRouting;
        public Rect PathRoutingZone;
        internal static CompositionModuleSettings Capture(TerraLoomIntegration c,RegionDecoration d) => new CompositionModuleSettings {
            Core=CoreRecipeParameters.Capture(c.World),Paths=PathRecipeParameters.Capture(c.Paths),
            Rivers=RiverRecipeParameters.Capture(c.Rivers),Decoration=DecorationRecipeParameters.Capture(d),
            RestrictPathRouting=c.RestrictPathRouting,PathRoutingZone=c.PathRoutingZone
        };
        internal void Apply(TerraLoomIntegration c,RegionDecoration d)
        {
            Core.Apply(c.World);Paths.Apply(c.Paths);Rivers.Apply(c.Rivers);Decoration.Apply(d);
            c.RestrictPathRouting=RestrictPathRouting;c.PathRoutingZone=PathRoutingZone;
        }
        internal void Validate()
        {
            if(Core==null||Paths==null||Rivers==null||Decoration==null||Core.GeneratedLayers==null||
                Paths.RegionCosts==null||Paths.Reservations==null||Rivers.RegionCosts==null||Rivers.ProtectedAreas==null)
                throw new InvalidOperationException("Schema 2 requires complete module parameters; recapture the recipe from its source world.");
        }
    }
    [Serializable]
    public sealed class CoreRecipeParameters
    {
        public TerrainLayer[] GeneratedLayers=Array.Empty<TerrainLayer>();
        public Material GeneratedTerrainMaterial=null;
        public int BlendResolution=128;
        public float RockSlopeStart=18;
        public float RockSlopeEnd=40;
        internal static CoreRecipeParameters Capture(TerraLoomWorld component) => new CoreRecipeParameters {
            GeneratedLayers=component.GeneratedLayers,
            GeneratedTerrainMaterial=component.GeneratedTerrainMaterial,
            BlendResolution=component.BlendResolution,
            RockSlopeStart=component.RockSlopeStart,
            RockSlopeEnd=component.RockSlopeEnd
        };
        internal void Apply(TerraLoomWorld component)
        {
            component.GeneratedLayers=GeneratedLayers;
            component.GeneratedTerrainMaterial=GeneratedTerrainMaterial;
            component.BlendResolution=BlendResolution;
            component.RockSlopeStart=RockSlopeStart;
            component.RockSlopeEnd=RockSlopeEnd;
        }
    }
    [Serializable]
    public sealed class PathRecipeParameters
    {
        public float Width=2;
        public float MinimumBendRadius=0;
        public float CellSize=2;
        public float MaximumSlope=.5f;
        public float SlopeCost=4;
        public float SurfaceOffset=.04f;
        public int MaximumNodes=100000;
        public int TotalSearchBudget=250000;
        public float BridgeClearance=1;
        public float MaximumFordDepth=.3f;
        public float MeshSpacing=.25f;
        public int MaximumGeometryVertices=2000000;
        public List<PathRegionSettings> RegionCosts=new List<PathRegionSettings>();
        public List<PathReservationSettings> Reservations=new List<PathReservationSettings>();
        public Material GroundMaterial=null;
        public Material BridgeMaterial=null;
        public bool TimberBeamBridge=false;
        public bool TimberTrussBridge=false;
        internal static PathRecipeParameters Capture(TerraLoomPaths component) => new PathRecipeParameters {
            Width=component.Width,
            MinimumBendRadius=component.MinimumBendRadius,
            CellSize=component.CellSize,
            MaximumSlope=component.MaximumSlope,
            SlopeCost=component.SlopeCost,
            SurfaceOffset=component.SurfaceOffset,
            MaximumNodes=component.MaximumNodes,
            TotalSearchBudget=component.TotalSearchBudget,
            BridgeClearance=component.BridgeClearance,
            MaximumFordDepth=component.MaximumFordDepth,
            MeshSpacing=component.MeshSpacing,
            MaximumGeometryVertices=component.MaximumGeometryVertices,
            RegionCosts=component.RegionCosts,
            Reservations=component.Reservations,
            GroundMaterial=component.GroundMaterial,
            BridgeMaterial=component.BridgeMaterial,
            TimberBeamBridge=component.TimberBeamBridge,
            TimberTrussBridge=component.TimberTrussBridge
        };
        internal void Apply(TerraLoomPaths component)
        {
            component.Width=Width;
            component.MinimumBendRadius=MinimumBendRadius;
            component.CellSize=CellSize;
            component.MaximumSlope=MaximumSlope;
            component.SlopeCost=SlopeCost;
            component.SurfaceOffset=SurfaceOffset;
            component.MaximumNodes=MaximumNodes;
            component.TotalSearchBudget=TotalSearchBudget;
            component.BridgeClearance=BridgeClearance;
            component.MaximumFordDepth=MaximumFordDepth;
            component.MeshSpacing=MeshSpacing;
            component.MaximumGeometryVertices=MaximumGeometryVertices;
            component.RegionCosts=RegionCosts;
            component.Reservations=Reservations;
            component.GroundMaterial=GroundMaterial;
            component.BridgeMaterial=BridgeMaterial;
            component.TimberBeamBridge=TimberBeamBridge;
            component.TimberTrussBridge=TimberTrussBridge;
        }
    }
    [Serializable]
    public sealed class RiverRecipeParameters
    {
        public float Width=4;
        public float Depth=.6f;
        public float BankWidth=2;
        public float CellSize=2;
        public float MinimumBendRadius=0;
        public float WaterInset=0;
        public float MaximumSlope=.3f;
        public float BridgeClearance=1;
        public float SampleSpacing=.5f;
        public int MaximumNodes=65536;
        public int TotalSearchBudget=250000;
        public int TotalSampleBudget=4000000;
        public List<RiverRegionSettings> RegionCosts=new List<RiverRegionSettings>();
        public List<RiverProtectionSettings> ProtectedAreas=new List<RiverProtectionSettings>();
        public Material WaterMaterial=null;
        public Material BedMaterial=null;
        public Material BankMaterial=null;
        public bool CarveTerrainCopy=true;
        public bool TerrainBrush=false;
        public int SedimentTerrainLayer=-1;
        public float SedimentExposureDepth=.15f;
        public int MaximumGeometryVertices=1000000;
        internal static RiverRecipeParameters Capture(TerraLoomRivers component) => new RiverRecipeParameters {
            Width=component.Width,
            Depth=component.Depth,
            BankWidth=component.BankWidth,
            CellSize=component.CellSize,
            MinimumBendRadius=component.MinimumBendRadius,
            WaterInset=component.WaterInset,
            MaximumSlope=component.MaximumSlope,
            BridgeClearance=component.BridgeClearance,
            SampleSpacing=component.SampleSpacing,
            MaximumNodes=component.MaximumNodes,
            TotalSearchBudget=component.TotalSearchBudget,
            TotalSampleBudget=component.TotalSampleBudget,
            RegionCosts=component.RegionCosts,
            ProtectedAreas=component.ProtectedAreas,
            WaterMaterial=component.WaterMaterial,
            BedMaterial=component.BedMaterial,
            BankMaterial=component.BankMaterial,
            CarveTerrainCopy=component.CarveTerrainCopy,
            TerrainBrush=component.TerrainBrush,
            SedimentTerrainLayer=component.SedimentTerrainLayer,
            SedimentExposureDepth=component.SedimentExposureDepth,
            MaximumGeometryVertices=component.MaximumGeometryVertices
        };
        internal void Apply(TerraLoomRivers component)
        {
            component.Width=Width;
            component.Depth=Depth;
            component.BankWidth=BankWidth;
            component.CellSize=CellSize;
            component.MinimumBendRadius=MinimumBendRadius;
            component.WaterInset=WaterInset;
            component.MaximumSlope=MaximumSlope;
            component.BridgeClearance=BridgeClearance;
            component.SampleSpacing=SampleSpacing;
            component.MaximumNodes=MaximumNodes;
            component.TotalSearchBudget=TotalSearchBudget;
            component.TotalSampleBudget=TotalSampleBudget;
            component.RegionCosts=RegionCosts;
            component.ProtectedAreas=ProtectedAreas;
            component.WaterMaterial=WaterMaterial;
            component.BedMaterial=BedMaterial;
            component.BankMaterial=BankMaterial;
            component.CarveTerrainCopy=CarveTerrainCopy;
            component.TerrainBrush=TerrainBrush;
            component.SedimentTerrainLayer=SedimentTerrainLayer;
            component.SedimentExposureDepth=SedimentExposureDepth;
            component.MaximumGeometryVertices=MaximumGeometryVertices;
        }
    }
    [Serializable]
    public sealed class DecorationRecipeParameters
    {
        public int SeedOffset=103;
        public float Margin=4;
        public float MaximumSlope=40;
        internal static DecorationRecipeParameters Capture(RegionDecoration component) => new DecorationRecipeParameters {
            SeedOffset=component.SeedOffset,
            Margin=component.Margin,
            MaximumSlope=component.MaximumSlope
        };
        internal void Apply(RegionDecoration component)
        {
            component.SeedOffset=SeedOffset;
            component.Margin=Margin;
            component.MaximumSlope=MaximumSlope;
        }
    }
}

