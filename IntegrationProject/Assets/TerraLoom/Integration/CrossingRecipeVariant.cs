using System;
using TerraLoom.Core;
using TerraLoom.Core.Unity;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>Consumer example: manual configuration or a bounded seeded valley/anchor recipe.
    /// Both use the existing composition and external authority; this recipe grants no rights.</summary>
    [DefaultExecutionOrder(280)]
    public sealed class CrossingRecipeVariant:MonoBehaviour
    {
        public FrozenCurvedCrossing Scenario;
        public bool SeedDriven;
        public int Seed=2043;
        public bool GenerateOnStart=true;
        private TerrainData source,generated;
        private void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-terraloomRecipeSeed");
            if(SeedDriven&&at>=0)
            {
                if(at+1>=args.Length||!int.TryParse(args[at+1],out int value))throw new ArgumentException("Recipe seed must be an integer.");
                Seed=value;
            }
            if(GenerateOnStart&&!Rebuild())Debug.LogError(Scenario.Composition.Diagnostics,this);
        }

        public bool Rebuild()
        {
            var c=Scenario.Composition;c.Clear();ReleaseSource();
            if(SeedDriven)
            {
                var world=c.World;world.Seed=Seed;
                var terrain=world.Terrain;source=terrain.terrainData;
                generated=Instantiate(source);generated.name="Seeded crossing source (owned consumer copy)";
                generated.hideFlags=HideFlags.DontSave;
                var heights=new float[129,129];
                for(int z=0;z<129;z++)for(int x=0;x<129;x++)
                {
                    float px=x*.75f,pz=z*.75f;
                    float shoulder=Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Abs(px-48)-22)/20));
                    float drop=.025f+.0025f*(Hash(Seed,0)%5),level=6+.15f*(Hash(Seed,1)%3);
                    float phase=(Hash(Seed,3)%628)/100f;
                    heights[z,x]=(level-drop*pz+shoulder*(5+3*Mathf.Pow(Mathf.Sin(pz*.055f+phase),2)))/16;
                }
                generated.SetHeights(0,0,heights);terrain.terrainData=generated;terrain.GetComponent<TerrainCollider>().terrainData=generated;
                // A declared analytic flat-floor valley admits a protected-area
                // detour without asking the planner to waive hydraulics. Seed parity
                // changes which side is approached; the planner owns the final bend.
                float sourceX=46+4*(Seed&1),mouthX=sourceX,roadZ=56+2*(Hash(Seed,2)%3);
                world.UseSeedAnchors=false;
                world.ManualAnchors=new[]{Anchor("source",sourceX,8),Anchor("mouth",mouthX,88),Anchor("west",24,roadZ-2),Anchor("east",72,roadZ+2)};
                c.RestrictPathRouting=true;c.PathRoutingZone=new Rect(16,52,64,12);
                Scenario.ApprovedCrossingZone=new Rect(24,52,48,12);
                Scenario.LimitSectionsToCrossingStrip=true;
            }
            Scenario.ConstructionProbe=false;Scenario.GenerateOnStart=false;
            return Scenario.Generate();
        }

        /// <summary>Clear downstream outputs before releasing only this recipe's owned source copy.</summary>
        public void Clear(){if(Scenario&&Scenario.Composition)Scenario.Composition.Clear();ReleaseSource();}
        private void ReleaseSource()
        {
            if(!generated)return;
            var terrain=Scenario.Composition.World.Terrain;
            if(terrain&&terrain.terrainData==generated)
            {terrain.terrainData=source;terrain.GetComponent<TerrainCollider>().terrainData=source;}
            if(Application.isPlaying)Destroy(generated);else DestroyImmediate(generated);
            generated=null;source=null;
        }
        private void OnDestroy(){Clear();}
        private static ManualWorldAnchor Anchor(string id,float x,float z)=>new ManualWorldAnchor{Id=id,Position=new Vector3(x,0,z)};
        private static uint Hash(int seed,uint stream)
        {unchecked{uint value=(uint)seed^(stream+1)*0x9e3779b9U;value^=value>>16;value*=0x7feb352dU;value^=value>>15;value*=0x846ca68bU;return value^(value>>16);}}
    }
}
