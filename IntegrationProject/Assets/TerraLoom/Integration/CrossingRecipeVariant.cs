using System;
using System.Threading;
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
        private WorldSourcePublication sourcePublication;
        /// <summary>Requested Seed is user intent; the World seed changes only after collective publication.</summary>
        public int PublishedSeed => Scenario.Composition.World.Seed;
        public TerrainData PublishedSource => sourcePublication?.CurrentSource;
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

        public bool Rebuild() => Rebuild(CancellationToken.None);
        public bool Rebuild(CancellationToken cancellation)
        {
            var c=Scenario.Composition;
            if(SeedDriven)
            {
                if(sourcePublication==null)sourcePublication=new WorldSourcePublication(c.World);
                int requestedSeed=Seed;
                float sourceX=46+4*(requestedSeed&1),mouthX=sourceX,roadZ=56+2*(Hash(requestedSeed,2)%3);
                var anchors=new[]{Anchor("source",sourceX,8),Anchor("mouth",mouthX,88),Anchor("west",24,roadZ-2),Anchor("east",72,roadZ+2)};
                c.RestrictPathRouting=true;c.PathRoutingZone=new Rect(16,52,64,12);
                Scenario.ApprovedCrossingZone=new Rect(24,52,48,12);
                Scenario.LimitSectionsToCrossingStrip=true;
                Scenario.ConstructionProbe=false;Scenario.GenerateOnStart=false;
                c.CrossingPermissions=Scenario.Authorize;
                return c.GenerateWithSource(()=>sourcePublication.Prepare(requestedSeed,anchors,data=>BuildSource(data,requestedSeed),cancellation),cancellation);
            }
            Scenario.ConstructionProbe=false;Scenario.GenerateOnStart=false;
            c.CrossingPermissions=Scenario.Authorize;
            return c.Generate(cancellation);
        }

        private static void BuildSource(TerrainData data,int seed)
        {
                if(data.heightmapResolution!=129||data.size!=new Vector3(96,16,96))
                    throw new InvalidOperationException("The bounded crossing recipe requires a 129-sample 96x16x96 source.");
                var heights=new float[129,129];
                for(int z=0;z<129;z++)for(int x=0;x<129;x++)
                {
                    float px=x*.75f,pz=z*.75f;
                    float shoulder=Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Abs(px-48)-22)/20));
                    float drop=.025f+.0025f*(Hash(seed,0)%5),level=6+.15f*(Hash(seed,1)%3);
                    float phase=(Hash(seed,3)%628)/100f;
                    heights[z,x]=(level-drop*pz+shoulder*(5+3*Mathf.Pow(Mathf.Sin(pz*.055f+phase),2)))/16;
                }
                data.SetHeights(0,0,heights);
                // A declared analytic flat-floor valley admits a protected-area
                // detour without asking the planner to waive hydraulics. Seed parity
                // changes which side is approached; the planner owns the final bend.
        }

        /// <summary>Clear downstream outputs before releasing only this recipe's owned source copy.</summary>
        public void Clear(){if(Scenario&&Scenario.Composition)Scenario.Composition.Clear();sourcePublication?.Clear();}
        private void OnDestroy(){Clear();}
        private static ManualWorldAnchor Anchor(string id,float x,float z)=>new ManualWorldAnchor{Id=id,Position=new Vector3(x,0,z)};
        private static uint Hash(int seed,uint stream)
        {unchecked{uint value=(uint)seed^(stream+1)*0x9e3779b9U;value^=value>>16;value*=0x7feb352dU;value^=value>>15;value*=0x846ca68bU;return value^(value>>16);}}
    }
}
