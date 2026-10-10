using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>Explicit bounded visual recipe, not a general river/waterfall solver.
    /// All terrain, water, placement and observation queries share these metre-space functions.
    /// Seed affects relief detail; the authored fall/observation intent stays identifiable.</summary>
    public sealed class WaterfallLandscapePlan
    {
        public const float Size=160, Height=70, LipZ=90, FootZ=86;
        public readonly int Seed;
        public WaterfallLandscapePlan(int seed){Seed=seed;}
        public float Centre(float z)=>80+7*Mathf.Sin(z*.055f)+2*Mathf.Sin(z*.14f)+.018f*(z-90);
        public float Water(float z)
        {
            if(z>=LipZ)return 15.6f+(z-LipZ)*.035f;
            if(z<=FootZ)return 8.4f+(z-FootZ)*.035f;
            float t=(LipZ-z)/(LipZ-FootZ);
            return Mathf.Lerp(15.6f,8.4f,t*t*(3-2*t));
        }
        public const float PoolWidthRatio=1.35f; // Evaluation recipe, not a product standard.
        public float ChannelHalfWidth(float z)=>2.5f*(1+.1f*Mathf.Sin(z*.14f)+.07f*Mathf.Cos(z*.071f));
        public float HalfWidth(float z)=>ChannelHalfWidth(z)*(1+(PoolWidthRatio-1)*Mathf.Exp(-Mathf.Pow((z-78)/6.5f,2)));
        public float Noise(float x,float z)=>Mathf.PerlinNoise(x*.06f+Seed%137,z*.06f+Seed%89);
        public float Ground(float x,float z)
        {
            float d=Mathf.Abs(x-Centre(z)),w=HalfWidth(z),outside=Mathf.Max(0,d-w);
            float pool=Mathf.Exp(-Mathf.Pow((z-78)/6.5f,2));
            // The actual terrain carries the channel, irregular shore and stepped valley.
            float cross=d<w?-(1.25f+pool*.6f)*(1-Mathf.Pow(d/w,2)):
                Mathf.Min(outside*.48f,2.5f)+Mathf.Max(0,d-13)*.19f;
            float hills=Mathf.Pow(Mathf.Clamp01((d-12)/45),1.3f)*
                (18+10*Noise(x*.7f,z*.7f)+4*Mathf.Sin(z*.071f+x*.027f)*Mathf.Sin(z*.071f+x*.027f));
            float ridges=Mathf.Max(0,d-18)*.075f*Mathf.Pow(1-Mathf.Abs(2*Noise(x,z)-1),3);
            float detail=(Noise(x,z)-.5f)*1.3f*Mathf.SmoothStep(0,1,outside/5);
            float baseHeight=Mathf.Lerp(Water(z),6+.055f*z,Mathf.SmoothStep(0,1,(d-10)/25));
            return Mathf.Clamp(baseHeight+cross+hills+ridges+detail,0,Height-1);
        }
        public Vector3 CentrePoint(float z)=>new Vector3(Centre(z),Water(z),z);
        public Vector2 Observation(int index)=>index==0?new Vector2(Centre(98)-4.5f,98):
            index==1?new Vector2(Centre(77)+11,76):new Vector2(Centre(58)-8,58);
        public Vector3 Look(int index)=>index==0?CentrePoint(89):index==1?CentrePoint(80)+Vector3.up*.4f:CentrePoint(84);
    }
}
