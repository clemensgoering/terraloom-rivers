using UnityEngine;
using TerraLoom.Core;
using TerraLoom.Rivers;

namespace TerraLoom.Integration
{
    /// <summary>Explicit bounded visual recipe, not a general river/waterfall solver.
    /// All terrain, water, placement and observation queries share these metre-space functions.
    /// Seed affects relief detail; the authored fall/observation intent stays identifiable.</summary>
    public sealed class WaterfallLandscapePlan
    {
        public const float Size=160, Height=70, LipZ=90, FootZ=86;
        public readonly int Seed;
        public readonly WaterfallProfile Profile;
        public readonly bool Comparison;
        public WaterfallLandscapePlan(int seed,bool comparison=false)
        {
            Seed=seed;Comparison=comparison;
            // Common proposed local recipe: lipZ-1, impact2.3, pool4, sill8;
            // receiving join17 is the ordinary flat downstream reach, not a 15m pool.
            Profile=comparison?new WaterfallProfile(new WorldPoint(80,15.6,90),new WorldPoint(80,12.4,86.7),
                new WorldPoint(80,12.4,85),new WorldPoint(80,12.4,81),15.6,12.4,3,4.2,.75,1.1,.5,.012,0,3):WaterfallProfile.LandscapeBaseline();
        }
        public WaterfallSample At(float z)=>Profile.SampleAtWorldPosition(80,z);
        public float Centre(float z)=>(float)At(z).Surface.X;
        public float Water(float z)=>(float)At(z).Surface.Y;
        public const float PoolWidthRatio=1.35f; // Evaluation recipe, not a product standard.
        public float ChannelHalfWidth(float z)=>(float)Profile.NominalHalfWidthAtWorldPosition(80,z);
        public float HalfWidth(float z)=>(float)At(z).HalfWidth;
        public float Noise(float x,float z)=>Mathf.PerlinNoise(x*.06f+Seed%137,z*.06f+Seed%89);
        public float Ground(float x,float z)
        {
            var sample=At(z);float d=Mathf.Abs(x-(float)sample.Surface.X),w=(float)sample.HalfWidth,outside=Mathf.Max(0,d-w);
            // The actual terrain carries the channel, irregular shore and stepped valley.
            float cross=d<w?-(float)sample.CentreDepth*(1-Mathf.Pow(d/w,2)):
                Mathf.Min(outside*.48f,2.5f)+Mathf.Max(0,d-13)*.19f;
            float hills=Mathf.Pow(Mathf.Clamp01((d-12)/45),1.3f)*
                (18+10*Noise(x*.7f,z*.7f)+4*Mathf.Sin(z*.071f+x*.027f)*Mathf.Sin(z*.071f+x*.027f));
            float ridges=Mathf.Max(0,d-18)*.075f*Mathf.Pow(1-Mathf.Abs(2*Noise(x,z)-1),3);
            float detail=(Noise(x,z)-.5f)*1.3f*Mathf.SmoothStep(0,1,outside/5);
            float baseHeight=Mathf.Lerp((float)sample.Surface.Y,6+.055f*z,Mathf.SmoothStep(0,1,(d-10)/25));
            return Mathf.Clamp(baseHeight+cross+hills+ridges+detail,0,Height-1);
        }
        public Vector3 CentrePoint(float z)=>new Vector3(Centre(z),Water(z),z);
        public Vector2 Observation(int index)=>Comparison?(index==0?new Vector2(76,94):index==1?new Vector2(73,84):new Vector2(73,80)):
            index==0?new Vector2(Centre(98)-4.5f,98):
            index==1?new Vector2(Centre(77)+11,76):new Vector2(Centre(58)-8,58);
        public Vector3 Look(int index)=>Comparison?(index==0?new Vector3(80,14,88):index==1?new Vector3(80,15.2f,89):new Vector3(80,15f,88.5f)):
            index==0?CentrePoint(89):index==1?CentrePoint(80)+Vector3.up*.4f:CentrePoint(84);
    }
}
