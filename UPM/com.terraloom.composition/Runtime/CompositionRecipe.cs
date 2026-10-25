using System;
using System.Linq;
using TerraLoom.Core.Unity;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>Persistent source inputs. Outputs and scene ownership never belong to this asset.</summary>
    [CreateAssetMenu(menuName="TerraLoom/Composition Recipe",fileName="WorldRecipe")]
    public sealed class CompositionRecipe : ScriptableObject
    {
        public const int CurrentSchemaVersion=3;
        public int SchemaVersion=CurrentSchemaVersion;
        public CompositionRecipeSettings Settings=new CompositionRecipeSettings();

        public void Validate()
        {
            if(SchemaVersion!=CurrentSchemaVersion && SchemaVersion!=2)
                throw new InvalidOperationException("Unsupported TerraLoom composition recipe schema "+SchemaVersion+"; supported schema is "+CurrentSchemaVersion+". No generation was started.");
            if(SchemaVersion==2 && Settings!=null && Settings.TerrainSource!=CompositionTerrainSource.Generated)
                throw new InvalidOperationException("Schema 2 supports generated terrain only; capture a schema 3 recipe for supplied terrain.");
            if(Settings==null)throw new InvalidOperationException("Composition recipe settings are missing.");
            if(Settings.Modules==null)throw new InvalidOperationException("Schema 2 module parameters are missing; recapture the source recipe.");
            Settings.Modules.Validate();
            if(!Enum.IsDefined(typeof(CompositionTerrainSource),Settings.TerrainSource))
                throw new InvalidOperationException("Unknown terrain source mode.");
            if(!Enum.IsDefined(typeof(TerrainRecipe),Settings.TerrainRecipe))throw new InvalidOperationException("Unknown terrain recipe.");
            if(Settings.Anchors==null||Settings.Regions==null||Settings.Rivers==null||Settings.Paths==null||Settings.DecorationProfiles==null)
                throw new InvalidOperationException("Recipe arrays must be present; use empty arrays for empty inputs.");
            if(Settings.DecorationCount<0)throw new InvalidOperationException("Decoration count cannot be negative.");
            foreach(var profile in Settings.DecorationProfiles)
            {if(!profile)throw new InvalidOperationException("Missing decoration profile asset.");profile.Validate();}
        }

        /// <summary>Copies authoring inputs, retaining explicit profile asset references. Does not capture geometry.</summary>
        public void CaptureFrom(TerraLoomIntegration composition,RegionDecoration decoration)
        {
            if(!composition||!composition.World||!composition.Paths||!composition.Rivers)
                throw new ArgumentException("Assign composition, Core, Paths and Rivers.");
            var world=composition.World;
            Settings=Freeze(new CompositionRecipeSettings {
                TerrainSource=world.Terrain&&!world.OwnsTerrain?CompositionTerrainSource.Supplied:CompositionTerrainSource.Generated,
                Seed=world.Seed,TerrainRecipe=world.Recipe,Resolution=world.Resolution,Size=world.TerrainSize,Frequency=world.Frequency,
                Anchors=world.ManualAnchors,Regions=world.Areas,
                Rivers=composition.Rivers.Connections.ToArray(),Paths=composition.Paths.Connections.ToArray(),
                DecorationProfiles=decoration?decoration.Profiles:Array.Empty<RegionalStyleProfile>(),DecorationCount=decoration?decoration.Count:0,ProfileSelection=decoration?decoration.ProfileSelection:RegionalProfileSelection.PriorityWinner,
                Modules=CompositionModuleSettings.Capture(composition,decoration)
            });
            SchemaVersion=CurrentSchemaVersion;
        }

        internal CompositionRecipeSettings Snapshot()
        {
            Validate();
            return Freeze(Settings);
        }
        internal PreparedSeededWorldRequest Prepare(TerraLoomWorld world,RegionDecoration decoration,CompositionRecipeSettings s)
        {
            var terrain=world.CreateGeneratedTerrainData(s.Seed,s.TerrainRecipe,s.Resolution,s.Size,s.Frequency);
            try
            {
                RegionSurfacePainter.PaintUnboundTerrain(world,terrain,s.DecorationProfiles,s.Regions);
                return new PreparedSeededWorldRequest(terrain,s.Seed,s.TerrainRecipe,s.Resolution,s.Size,s.Frequency,
                    s.Anchors,s.Regions,s.Rivers,s.Paths,s.DecorationProfiles,s.DecorationCount,s.ProfileSelection,0,decoration);
            }
            catch
            {if(Application.isPlaying)Destroy(terrain);else DestroyImmediate(terrain);throw;}
        }
        private static CompositionRecipeSettings Freeze(CompositionRecipeSettings value)
            => JsonUtility.FromJson<CompositionRecipeSettings>(JsonUtility.ToJson(value));
    }

    public enum CompositionTerrainSource { Generated=0, Supplied=1 }

    /// <summary>Versioned source and module inputs. Region/profile IDs are explicit; biomes are not required.</summary>
    [Serializable]
    public sealed class CompositionRecipeSettings
    {
        public CompositionTerrainSource TerrainSource=CompositionTerrainSource.Generated;
        public int Seed=2042;
        public TerrainRecipe TerrainRecipe=TerrainRecipe.DrainageValley;
        public int Resolution=129;
        public Vector3 Size=new Vector3(96,16,96);
        public float Frequency=1;
        public ManualWorldAnchor[] Anchors=Array.Empty<ManualWorldAnchor>();
        public LandscapeArea[] Regions=Array.Empty<LandscapeArea>();
        public RiverConnectionSettings[] Rivers=Array.Empty<RiverConnectionSettings>();
        public PathConnectionSettings[] Paths=Array.Empty<PathConnectionSettings>();
        public RegionalStyleProfile[] DecorationProfiles=Array.Empty<RegionalStyleProfile>();
        public int DecorationCount=900;
        public RegionalProfileSelection ProfileSelection=RegionalProfileSelection.PriorityWinner;
        public CompositionModuleSettings Modules=new CompositionModuleSettings();
    }
}
