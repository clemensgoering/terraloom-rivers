using System;
using TerraLoom.Core;

namespace TerraLoom.Rivers
{
    public enum WaterfallProfileVersion { LandscapeBaselineV0=0, AnchoredPoolV1=1 }
    public enum WaterfallSection { Upstream, Fall, Pool, Outlet }
    public readonly struct WaterfallSample
    {
        public WorldPoint Surface { get; }
        public double HalfWidth { get; }
        public double CentreDepth { get; }
        public WaterfallSection Section { get; }
        internal WaterfallSample(WorldPoint surface,double halfWidth,double depth,WaterfallSection section)
        {Surface=surface;HalfWidth=halfWidth;CentreDepth=depth;Section=section;}
    }

    /// <summary>Immutable explicit longitudinal water/bed intent in world metres. No Unity,
    /// placement search, terrain writes, reservations or rendering. V1 supports a straight
    /// horizontal axis with ordered lip/impact/pool-centre/outlet anchors, a level pool and
    /// submerged outlet sill. Stations increase downstream from the lip; positive depth
    /// is measured below the water surface. Consumers must validate final geometry/rights.</summary>
    public sealed class WaterfallProfile
    {
        public WaterfallProfileVersion Version { get; }
        public WorldPoint Lip { get; }
        public WorldPoint Impact { get; }
        public WorldPoint Pool { get; }
        public WorldPoint Outlet { get; }
        public double UpperWaterLevel { get; }
        public double LowerWaterLevel { get; }
        public double ChannelWidth { get; }
        public double PoolWidth { get; }
        public double ChannelDepth { get; }
        public double PoolDepth { get; }
        public double OutletSillDepth { get; }
        public double UpstreamSlope { get; }
        public double DownstreamSlope { get; }
        public double OutletTransitionLength { get; }
        public double ImpactStation { get; }
        public double PoolStation { get; }
        public double OutletStation { get; }
        private readonly double dx,dz;
        private readonly WaterfallPoolShape receivingShape;

        public WaterfallProfile(WorldPoint lip,WorldPoint impact,WorldPoint pool,WorldPoint outlet,
            double upperWaterLevel,double lowerWaterLevel,double channelWidth,double poolWidth,
            double channelDepth,double poolDepth,double outletSillDepth,double upstreamSlope=.035,
            double downstreamSlope=.035,double outletTransitionLength=3)
        {
            foreach(double value in new[]{lip.X,lip.Y,lip.Z,impact.X,impact.Y,impact.Z,pool.X,pool.Y,pool.Z,outlet.X,outlet.Y,outlet.Z,
                upperWaterLevel,lowerWaterLevel,channelWidth,poolWidth,channelDepth,poolDepth,outletSillDepth,upstreamSlope,downstreamSlope,outletTransitionLength})
                if(!Finite(value)||Math.Abs(value)>1000000000)throw new ArgumentException("Finite world-metre waterfall values within +/-1e9 required.");
            double length=Distance(lip,impact);
            if(length<=.01||upperWaterLevel<=lowerWaterLevel||channelWidth<=0||poolWidth<channelWidth||poolWidth>channelWidth*1.5
                ||channelDepth<=0||poolDepth<channelDepth||outletSillDepth<=0||outletSillDepth>channelDepth
                ||upstreamSlope<0||downstreamSlope<0||outletTransitionLength<=.01)
                throw new ArgumentException("Ordered drop, positive widths/depths, pool width1–1.5x and submerged sill required.");
            dx=(impact.X-lip.X)/length;dz=(impact.Z-lip.Z)/length;
            double Station(WorldPoint p)=>(p.X-lip.X)*dx+(p.Z-lip.Z)*dz;
            bool OnAxis(WorldPoint p)=>Math.Abs((p.X-lip.X)*dz-(p.Z-lip.Z)*dx)<=.000001;
            if(!OnAxis(pool)||!OnAxis(outlet)||Station(pool)<=length+.01||Station(outlet)<=Station(pool)+.01
                ||Station(outlet)>1000000
                ||lip.Y!=upperWaterLevel||impact.Y!=lowerWaterLevel||pool.Y!=lowerWaterLevel||outlet.Y!=lowerWaterLevel)
                throw new ArgumentException("V1 anchors must share a straight axis, ordered lip/impact/pool/outlet and explicit matching water levels.");
            if(upstreamSlope>3*(upperWaterLevel-lowerWaterLevel)/length)
                throw new ArgumentException("Upstream tangent would reverse the monotone fall.");
            Version=WaterfallProfileVersion.AnchoredPoolV1;Lip=lip;Impact=impact;Pool=pool;Outlet=outlet;
            UpperWaterLevel=upperWaterLevel;LowerWaterLevel=lowerWaterLevel;ChannelWidth=channelWidth;PoolWidth=poolWidth;
            ChannelDepth=channelDepth;PoolDepth=poolDepth;OutletSillDepth=outletSillDepth;
            UpstreamSlope=upstreamSlope;DownstreamSlope=downstreamSlope;OutletTransitionLength=outletTransitionLength;
            ImpactStation=length;PoolStation=Station(pool);OutletStation=Station(outlet);
            receivingShape=new WaterfallPoolShape(channelWidth,poolWidth,channelDepth,poolDepth,outletSillDepth,downstreamSlope,outletTransitionLength);
        }
        private WaterfallProfile()
        {
            Version=WaterfallProfileVersion.LandscapeBaselineV0;UpperWaterLevel=15.6;LowerWaterLevel=8.4;
            ChannelWidth=5;PoolWidth=6.75;ChannelDepth=1.25;PoolDepth=1.85;OutletSillDepth=1.25;
            UpstreamSlope=DownstreamSlope=.035;OutletTransitionLength=3;
            ImpactStation=4;PoolStation=12;OutletStation=20;dx=0;dz=-1;
            Lip=Legacy(0).Surface;Impact=Legacy(4).Surface;Pool=Legacy(12).Surface;Outlet=Legacy(20).Surface;
        }
        /// <summary>Frozen historical picture recipe. Intentionally retains its sloping pool,
        /// Gaussian width/depth and float arithmetic; it is not the corrected V1 contract.</summary>
        public static WaterfallProfile LandscapeBaseline()=>new WaterfallProfile();
        public double StationAt(double x,double z)
        {
            if(!Finite(x)||!Finite(z))throw new ArgumentOutOfRangeException();
            return Version==WaterfallProfileVersion.LandscapeBaselineV0?90-z:(x-Lip.X)*dx+(z-Lip.Z)*dz;
        }
        public WaterfallSample Sample(double station)
        {
            if(!Finite(station)||Math.Abs(station)>1000000)throw new ArgumentOutOfRangeException(nameof(station));
            if(Version==WaterfallProfileVersion.LandscapeBaselineV0)return Legacy((float)station);
            double y,depth,half=ChannelWidth*.5;WaterfallSection section;
            if(station<0){y=UpperWaterLevel-UpstreamSlope*station;depth=ChannelDepth;section=WaterfallSection.Upstream;}
            else if(station<ImpactStation)
            {
                double t=station/ImpactStation,t2=t*t,t3=t2*t;
                y=(2*t3-3*t2+1)*UpperWaterLevel+(t3-2*t2+t)*(-UpstreamSlope*ImpactStation)+(-2*t3+3*t2)*LowerWaterLevel;
                depth=Lerp(ChannelDepth,PoolDepth,Smooth(t));section=WaterfallSection.Fall;
            }
            else
            {
                receivingShape.Evaluate(station,ImpactStation,PoolStation,OutletStation,LowerWaterLevel,out y,out half,out depth,out section);
            }
            return new WaterfallSample(new WorldPoint(Lip.X+station*dx,y,Lip.Z+station*dz),half,depth,section);
        }
        /// <summary>Projects a world X/Z query onto the explicit axis. Baseline evaluates its
        /// historical world-Z input directly to avoid round-trip float changes in old pictures.</summary>
        public WaterfallSample SampleAtWorldPosition(double x,double z)
        {
            double station=StationAt(x,z);
            if(Math.Abs(station)>1000000)throw new ArgumentOutOfRangeException();
            return Version==WaterfallProfileVersion.LandscapeBaselineV0?LegacyAtZ((float)z):Sample(station);
        }
        public double NominalHalfWidth(double station)
        {
            if(!Finite(station)||Math.Abs(station)>1000000)throw new ArgumentOutOfRangeException(nameof(station));
            return Version==WaterfallProfileVersion.LandscapeBaselineV0?LegacyChannel(90-(float)station):ChannelWidth*.5;
        }
        public double NominalHalfWidthAtWorldPosition(double x,double z)
        {
            double station=StationAt(x,z);
            if(Math.Abs(station)>1000000)throw new ArgumentOutOfRangeException();
            return Version==WaterfallProfileVersion.LandscapeBaselineV0?LegacyChannel((float)z):ChannelWidth*.5;
        }
        private static float Sin(float x)=>(float)Math.Sin(x);
        private static float Cos(float x)=>(float)Math.Cos(x);
        private static float LegacyChannel(float z)=>2.5f*(1+.1f*Sin(z*.14f)+.07f*Cos(z*.071f));
        private static WaterfallSample Legacy(float s)
            =>LegacyAtZ(90-s);
        private static WaterfallSample LegacyAtZ(float z)
        {
            float x=80+7*Sin(z*.055f)+2*Sin(z*.14f)+.018f*(z-90),y;
            if(z>=90)y=15.6f+(z-90)*.035f;
            else if(z<=86)y=8.4f+(z-86)*.035f;
            else{float t=(90-z)/4;y=15.6f+(8.4f-15.6f)*(t*t*(3-2*t));}
            float q=(z-78)/6.5f,pool=(float)Math.Exp(-(float)Math.Pow(q,2));
            float width=LegacyChannel(z)*(1+(1.35f-1)*pool),depth=1.25f+pool*.6f;
            return new WaterfallSample(new WorldPoint(x,y,z),width,depth,z>=90?WaterfallSection.Upstream:z>=86?WaterfallSection.Fall:WaterfallSection.Pool);
        }
        private static double Distance(WorldPoint a,WorldPoint b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z));
        private static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        private static double Smooth(double t)=>t*t*(3-2*t);
        private static double Lerp(double a,double b,double t)=>a+(b-a)*t;
    }
}
