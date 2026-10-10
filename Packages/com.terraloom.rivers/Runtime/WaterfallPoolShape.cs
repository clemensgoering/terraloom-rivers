using System;

namespace TerraLoom.Rivers
{
    /// <summary>Shared V1/V2 receiving-pool and submerged-sill arithmetic. Callers own
    /// anchor validation and coordinate frames. Retains V1 operation order.</summary>
    internal sealed class WaterfallPoolShape
    {
        private readonly double channelWidth,poolWidth,channelDepth,poolDepth,sillDepth,slope,transition;
        internal WaterfallPoolShape(double channelWidth,double poolWidth,double channelDepth,double poolDepth,double sillDepth,double slope,double transition)
        {this.channelWidth=channelWidth;this.poolWidth=poolWidth;this.channelDepth=channelDepth;this.poolDepth=poolDepth;this.sillDepth=sillDepth;this.slope=slope;this.transition=transition;}
        internal void Evaluate(double station,double impact,double pool,double outlet,double lower,
            out double y,out double half,out double depth,out WaterfallSection section)
        {
            half=channelWidth*.5;
            if(station<=outlet)
            {
                y=lower;section=WaterfallSection.Pool;
                double t=station<=pool?(station-impact)/(pool-impact):(outlet-station)/(outlet-pool);
                half=Lerp(channelWidth,poolWidth,Smooth(t))*.5;
                depth=station<=pool?poolDepth:Lerp(sillDepth,poolDepth,Smooth(t));
            }
            else
            {
                section=WaterfallSection.Outlet;double q=station-outlet,t=Math.Min(1,q/transition);
                double drop=q<transition?slope*transition*(t*t*t-.5*t*t*t*t):slope*(q-.5*transition);
                y=lower-drop;depth=Lerp(sillDepth,channelDepth,Smooth(t));
            }
        }
        private static double Smooth(double t)=>t*t*(3-2*t);
        private static double Lerp(double a,double b,double t)=>a+(b-a)*t;
    }
}
