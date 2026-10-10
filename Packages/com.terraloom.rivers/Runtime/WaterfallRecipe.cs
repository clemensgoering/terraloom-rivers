using System;
using TerraLoom.Core;

namespace TerraLoom.Rivers
{
    /// <summary>Immutable combined waterfall intent: explicit upper reach, parametric
    /// V2 fall, level receiving pool and V1 submerged outlet sill. Does not find a site,
    /// alter terrain, generate meshes, choose materials or authorize terrain writes.
    /// Separate upper distance / fall progress / lower station avoid ambiguous X/Z->Y
    /// queries at a vertical fall. Inputs and samples are physical metres, no render offset.</summary>
    public sealed class WaterfallRecipe
    {
        public int Version=>2;
        public WaterfallFallSurface Fall {get;}
        public WorldPoint Pool {get;}
        public WorldPoint OutletSill {get;}
        /// <summary>Lower-reach distances start at the actual impact, not the lip.</summary>
        public double PoolStation {get;}
        public double OutletStation {get;}
        public double PoolWidth {get;}
        public double ChannelDepth {get;}
        public double PoolDepth {get;}
        public double OutletSillDepth {get;}
        public double UpstreamSlope {get;}
        public double DownstreamSlope {get;}
        public double OutletTransitionLength {get;}
        private readonly WaterfallPoolShape shape;
        public WaterfallRecipe(WorldPoint lip,WorldPoint impact,WorldPoint pool,WorldPoint outletSill,
            double forwardX,double forwardZ,double channelWidth,double poolWidth,double channelDepth,
            double poolDepth,double outletSillDepth,double upstreamSlope=.012,double downstreamSlope=0,double outletTransitionLength=3)
        {
            Fall=new WaterfallFallSurface(lip,impact,forwardX,forwardZ,channelWidth);
            foreach(double v in new[]{pool.X,pool.Y,pool.Z,outletSill.X,outletSill.Y,outletSill.Z,poolWidth,channelDepth,poolDepth,outletSillDepth,upstreamSlope,downstreamSlope,outletTransitionLength})
                if(!Finite(v)||Math.Abs(v)>1e9)throw new ArgumentException("Finite bounded waterfall recipe values required.");
            if(poolWidth<channelWidth||poolWidth>channelWidth*1.5||channelDepth<=0||poolDepth<channelDepth
                ||outletSillDepth<=0||outletSillDepth>channelDepth||upstreamSlope<0||downstreamSlope<0||outletTransitionLength<=.01)
                throw new ArgumentException("Positive depths, pool width1–1.5x, submerged sill and nonnegative slopes required.");
            double Station(WorldPoint p)=>(p.X-impact.X)*Fall.Forward.X+(p.Z-impact.Z)*Fall.Forward.Z;
            bool OnAxis(WorldPoint p)=>Math.Abs((p.X-impact.X)*Fall.Across.X+(p.Z-impact.Z)*Fall.Across.Z)<=1e-6;
            double ps=Station(pool),os=Station(outletSill);
            if(!OnAxis(pool)||!OnAxis(outletSill)||pool.Y!=impact.Y||outletSill.Y!=impact.Y||ps<=.01||os<=ps+.01||os>1e6)
                throw new ArgumentException("Ordered on-axis level pool/sill anchors within lower sampling range required.");
            Pool=pool;OutletSill=outletSill;PoolStation=ps;OutletStation=os;PoolWidth=poolWidth;
            ChannelDepth=channelDepth;PoolDepth=poolDepth;OutletSillDepth=outletSillDepth;
            UpstreamSlope=upstreamSlope;DownstreamSlope=downstreamSlope;OutletTransitionLength=outletTransitionLength;
            shape=new WaterfallPoolShape(channelWidth,poolWidth,channelDepth,poolDepth,outletSillDepth,downstreamSlope,outletTransitionLength);
        }
        /// <summary>Nonnegative distance upstream from lip. Never samples the fall.</summary>
        public WaterfallSample SampleUpperReach(double distance)
        {
            CheckStation(distance);
            return new WaterfallSample(new WorldPoint(Fall.Lip.X-Fall.Forward.X*distance,
                Fall.Lip.Y+UpstreamSlope*distance,Fall.Lip.Z-Fall.Forward.Z*distance),Fall.Width*.5,ChannelDepth,WaterfallSection.Upstream);
        }
        /// <summary>Nonnegative downstream station from impact. Shares V1 pool/sill
        /// shape, with level pool and continuous transition to downstream grade.</summary>
        public WaterfallSample SampleLowerReach(double station)
        {
            CheckStation(station);
            shape.Evaluate(station,0,PoolStation,OutletStation,Fall.Impact.Y,out double y,out double half,out double depth,out var section);
            return new WaterfallSample(new WorldPoint(Fall.Impact.X+Fall.Forward.X*station,y,Fall.Impact.Z+Fall.Forward.Z*station),half,depth,section);
        }
        public WorldPoint SampleUpperSurface(double distance,double lateral)=>Across(SampleUpperReach(distance),lateral);
        public WorldPoint SampleLowerSurface(double station,double lateral)=>Across(SampleLowerReach(station),lateral);
        private WorldPoint Across(WaterfallSample sample,double lateral)
        {
            if(!Finite(lateral)||Math.Abs(lateral)>sample.HalfWidth)throw new ArgumentOutOfRangeException(nameof(lateral));
            return new WorldPoint(sample.Surface.X+Fall.Across.X*lateral,sample.Surface.Y,sample.Surface.Z+Fall.Across.Z*lateral);
        }
        private static void CheckStation(double station)
        {if(!Finite(station)||station<0||station>1e6)throw new ArgumentOutOfRangeException(nameof(station));}
        private static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
    }
}
