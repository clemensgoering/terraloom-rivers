using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraLoom.Core.Unity;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>One owned, unbound candidate and frozen demo inputs. Dispose if publication fails.</summary>
    public sealed class PreparedSeededWorldRequest : IDisposable
    {
        private TerrainData terrain;
        public readonly int Seed;
        public readonly TerrainRecipe Recipe;
        public readonly int Resolution;
        public readonly Vector3 Size;
        public readonly float Frequency;
        public readonly ManualWorldAnchor[] Anchors;
        public readonly LandscapeArea[] Areas;
        public readonly RiverConnectionSettings[] RiverConnections;
        public readonly PathConnectionSettings[] PathConnections;
        public readonly RegionalStyleProfile[] Profiles;
        public readonly int DecorationCount;
        public readonly RegionalProfileSelection Selection;
        public readonly float Boundary;
        public readonly RegionDecoration Decoration;
        public PreparedSeededWorldRequest(TerrainData terrain,int seed,TerrainRecipe recipe,int resolution,Vector3 size,float frequency,
            ManualWorldAnchor[] anchors,LandscapeArea[] areas,RiverConnectionSettings[] rivers,PathConnectionSettings[] paths,
            RegionalStyleProfile[] profiles,int decorationCount,RegionalProfileSelection selection,float boundary,RegionDecoration decoration)
        {
            this.terrain=terrain;Seed=seed;Recipe=recipe;Resolution=resolution;Size=size;Frequency=frequency;
            Anchors=anchors;Areas=areas;RiverConnections=rivers;PathConnections=paths;
            Profiles=(RegionalStyleProfile[])profiles.Clone();DecorationCount=decorationCount;Selection=selection;
            Boundary=boundary;Decoration=decoration;
        }
        public TerrainData TakeTerrain(){var result=terrain;terrain=null;return result;}
        public void Dispose(){if(!terrain)return;if(Application.isPlaying)UnityEngine.Object.Destroy(terrain);else UnityEngine.Object.DestroyImmediate(terrain);terrain=null;}
    }

}