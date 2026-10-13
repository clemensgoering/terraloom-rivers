using System;
using System.Linq;
using System.Threading;
using TerraLoom.Core;

namespace TerraLoom.Rivers
{
    /// <summary>Opt-in site identity. The anchor supplies XZ; source terrain determines both elevations.</summary>
    public sealed class RiverFallSite
    {
        public string Id { get; }
        public string AnchorId { get; }
        /// <summary>Opt in to rejecting a fall whose receiving route cannot be planned.
        /// Legacy local previews retain their earlier site contract.</summary>
        public bool RequirePlannedLowerReach { get; }
        /// <summary>Explicit first publication policy: the receiving water exits a
        /// finite height grid at its downstream edge, not at an interior land point.</summary>
        public bool WorldBoundaryMouth { get; }
        public RiverFallSite(string id, string anchorId,bool requirePlannedLowerReach=false,bool worldBoundaryMouth=false)
        { RiverHash.Id(id); RiverHash.Id(anchorId); Id = id; AnchorId = anchorId;
          if(worldBoundaryMouth&&!requirePlannedLowerReach)
              throw new ArgumentException("Boundary mouth requires a planned lower reach.");
          RequirePlannedLowerReach=requirePlannedLowerReach;WorldBoundaryMouth=worldBoundaryMouth; }
    }

    /// <summary>Validated straight transition intent, deliberately not a RiverRoute or publication permit.</summary>
    public sealed class RiverFallCandidate
    {
        public PlanIdentity CoreInputIdentity { get; }
        public string RequestId { get; }
        public RiverFallSite Site { get; }
        public WorldPoint UpperStart { get; }
        public WorldPoint Lip => Recipe.Fall.Lip;
        public WorldPoint Impact => Recipe.Fall.Impact;
        public WorldPoint Pool => Recipe.Pool;
        public WorldPoint Outlet => Recipe.OutletSill;
        public WorldPoint LowerEnd { get; }
        public WaterfallRecipe Recipe { get; }
        public RiverFallLowerReach LowerReach { get; }
        public bool Publishable => false;
        internal RiverFallCandidate(PlanIdentity identity, RiverRequest request, WorldPoint upper, WorldPoint lower,
            WaterfallRecipe recipe,RiverFallLowerReach lowerReach)
        { CoreInputIdentity=identity;RequestId=request.Id;Site=request.FallSite;UpperStart=upper;LowerEnd=lower;
            Recipe=recipe;LowerReach=lowerReach; }
    }

    /// <summary>Finite, read-only candidate inspection. No terrain writes, route search or output publication.</summary>
    public static class RiverFallCandidatePlanner
    {
        public static bool TryCreate(PlanSnapshot source, PlanIdentity expected, WorldBounds bounds,
            RiverRequest request, RiverProfile profile, out RiverFallCandidate candidate, out string diagnostic,
            CancellationToken cancellation=default)
        {
            candidate=null;
            if(source==null||request==null||profile==null||request.FallSite==null)throw new ArgumentException("Explicit source, request, profile and fall site required.");
            if(source.Identity!=expected){diagnostic="Stale Core source identity/revision.";return false;}
            var start=source.Anchors.FirstOrDefault(a=>a.Id==request.SourceId);
            var end=source.Anchors.FirstOrDefault(a=>a.Id==request.MouthId);
            var site=source.Anchors.FirstOrDefault(a=>a.Id==request.FallSite.AnchorId);
            if(start.Id==null||end.Id==null||site.Id==null){diagnostic="Source, fall or mouth anchor missing.";return false;}
            double dx=end.Position.X-start.Position.X,dz=end.Position.Z-start.Position.Z;
            double length=Math.Sqrt(dx*dx+dz*dz);
            if(!RiverHash.Positive(length)){diagnostic="Source and mouth need distinct XZ.";return false;}
            double fx=dx/length,fz=dz/length,ax=fz,az=-fx;
            double sx=site.Position.X-start.Position.X,sz=site.Position.Z-start.Position.Z;
            double siteStation=sx*fx+sz*fz,offAxis=Math.Abs(sx*ax+sz*az);
            double support=Math.Max(.125,Math.Min(.25,profile.SampleSpacing));
            double poolStation=profile.Width,outletStation=2*profile.Width,lowerMin=profile.Width;
            double poolExtent=outletStation+lowerMin-support;
            // A valid ordinary river can be arbitrarily narrow. A fall additionally needs
            // resolvable lip support and distinct physical pool/sill stations.
            if(!RiverHash.Finite(poolExtent)||!RiverHash.Finite(profile.Width*1.35)
                ||profile.Width<2*support||poolStation<=.01||outletStation<=poolStation+.01
                ||poolExtent<=0)
            {diagnostic="Fall minimum footprint: lip support and ordered pool/outlet stations need more width.";return false;}
            double poolStepsRaw=Math.Ceiling(poolExtent/Math.Min(profile.SampleSpacing,.5));
            if(!RiverHash.Finite(poolStepsRaw)||poolStepsRaw<1||poolStepsRaw>profile.TotalSampleBudget)
            {diagnostic="Fall pool/outlet inspection exceeds the terrain sample budget.";return false;}
            if(offAxis>1e-6||siteStation<profile.Width+support||length-siteStation<outletStation+lowerMin)
            {diagnostic="Fall site is off the straight request axis or lacks upstream/pool/outlet/lower reach space.";return false;}
            if(!profile.AllowExcavation){diagnostic="Pool excavation is not authorized by the river profile.";return false;}
            var centre=site.Position;
            var upperPoint=new WorldPoint(centre.X-fx*support,0,centre.Z-fz*support);
            var lowerPoint=new WorldPoint(centre.X+fx*support,0,centre.Z+fz*support);
            double bankRadius=profile.Width*.675+profile.BankWidth;
            double minX=double.PositiveInfinity,minZ=double.PositiveInfinity,maxX=double.NegativeInfinity,maxZ=double.NegativeInfinity;
            foreach(double station in new[]{-support,0,outletStation+lowerMin})
            foreach(double lateral in new[]{-bankRadius,bankRadius})
            {double x=centre.X+fx*station+ax*lateral,z=centre.Z+fz*station+az*lateral;
                minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minZ=Math.Min(minZ,z);maxZ=Math.Max(maxZ,z);}
            var footprint=new WorldBounds(minX,minZ,maxX,maxZ);
            if(!bounds.Contains(footprint)){diagnostic="Full fall banks/pool/outlet footprint leaves river bounds.";return false;}
            if(source.Reservations.Any(r=>r.Strength==ReservationStrength.Hard&&r.Bounds.Overlaps(footprint)))
            {diagnostic="Fall/pool/outlet footprint overlaps a hard reservation.";return false;}
            var heights=source.Terrain.Heights;
            bool Height(WorldPoint p,double lateral,out double h)=>heights.TryGetHeight(p.X+ax*lateral,p.Z+az*lateral,out h)&&RiverHash.Finite(h);
            if(!Height(upperPoint,0,out double upper)||!Height(lowerPoint,0,out double lower))
            {diagnostic="Lip or impact terrain unavailable.";return false;}
            if(request.FallSite.WorldBoundaryMouth)
            {
                if(!(heights is GridHeightSource tile))
                {diagnostic="Boundary mouth requires a finite owned-style height grid.";return false;}
                const double tolerance=1e-5;
                bool exitX=fx>.999999&&Math.Abs(end.Position.X-tile.MaxX)<tolerance||
                    fx<-.999999&&Math.Abs(end.Position.X-tile.MinX)<tolerance;
                bool exitZ=fz>.999999&&Math.Abs(end.Position.Z-tile.MaxZ)<tolerance||
                    fz<-.999999&&Math.Abs(end.Position.Z-tile.MinZ)<tolerance;
                if(!exitX&&!exitZ)
                {diagnostic="Mouth must lie on the downstream edge of the source height grid.";return false;}
            }
            if(upper-lower<Math.Max(1,profile.Depth*2))
            {diagnostic="Fall site lacks a distinct vertical drop.";return false;}
            foreach(double lateral in new[]{-bankRadius,bankRadius})
            {
                if(!Height(upperPoint,lateral,out double bank)||bank<upper-profile.SurfaceOffset-.05)
                {diagnostic="Upper bank lacks full-width lip support.";return false;}
                if(!Height(lowerPoint,lateral,out bank)||bank<lower-profile.SurfaceOffset-.05)
                {diagnostic="Lower bank lacks full-width impact support.";return false;}
            }
            int poolSteps=(int)poolStepsRaw;
            for(int i=0;i<=poolSteps;i++)
            foreach(double lateral in new[]{0.0,-bankRadius,bankRadius})
            {
                cancellation.ThrowIfCancellationRequested();
                double station=support+(outletStation+lowerMin-support)*i/poolSteps;
                var p=new WorldPoint(centre.X+fx*station,0,centre.Z+fz*station);
                if(!Height(p,lateral,out double ground)||ground<lower-profile.Depth-.05)
                {diagnostic="Pool/outlet or lower bank lacks terrain support.";return false;}
            }
            // Ordinary reaches remain subject to the unmodified slope limit.
            if(!Reach(start.Position,upperPoint,upper,heights,profile,true,cancellation)
                ||!Reach(lowerPoint,end.Position,lower,heights,profile,false,cancellation))
            {diagnostic="Upper or lower reach is missing, uphill or exceeds ordinary maximum slope.";return false;}
            double high=upper+profile.SurfaceOffset-profile.WaterInset;
            double low=lower+profile.SurfaceOffset-profile.WaterInset;
            if(!heights.TryGetHeight(end.Position.X,end.Position.Z,out double mouthGround)||!RiverHash.Finite(mouthGround))
            {diagnostic="Mouth terrain unavailable for the receiving grade.";return false;}
            double lowerLength=length-siteStation;
            double transition=profile.Width;
            WorldPoint OnAxis(double station,double y)=>new WorldPoint(centre.X+fx*station,y,centre.Z+fz*station);
            WaterfallRecipe recipe;
            try
            {
                recipe=new WaterfallRecipe(OnAxis(0,high),OnAxis(0,low),OnAxis(poolStation,low),OnAxis(outletStation,low),
                    fx,fz,profile.Width,profile.Width*1.35,profile.Depth,profile.Depth*1.3,profile.Depth*.7,
                    upstreamSlope:0,downstreamSlope:0,outletTransitionLength:transition);
            }
            catch(ArgumentException error)
            {diagnostic="Fall recipe is outside bounded geometry: "+error.Message;return false;}
            bool hasReceiving=RiverFallLowerReach.TryBuild(source,bounds,request,profile,recipe,lowerLength,
                out var receiving,out diagnostic,cancellation);
            if(!hasReceiving&&request.FallSite.RequirePlannedLowerReach)return false;
            candidate=new RiverFallCandidate(source.Identity,request,
                new WorldPoint(start.Position.X,high,start.Position.Z),
                new WorldPoint(end.Position.X,mouthGround+profile.SurfaceOffset-profile.WaterInset,end.Position.Z),recipe,receiving);
            diagnostic=hasReceiving
                ?"Valid local fall candidate with sampled straight receiving route; physical full-reach publication remains unavailable."
                :"Valid local fall candidate only; receiving route unavailable: "+diagnostic;
            return true;
        }

        private static bool Reach(WorldPoint a,WorldPoint b,double edgeHeight,IHeightSource terrain,RiverProfile profile,bool upper,
            CancellationToken cancellation)
        {
            double distance=Math.Sqrt((b.X-a.X)*(b.X-a.X)+(b.Z-a.Z)*(b.Z-a.Z));
            double raw=Math.Ceiling(distance/Math.Min(profile.SampleSpacing,.5));
            if(!RiverHash.Finite(raw)||raw<1||raw>profile.TotalSampleBudget)return false;
            int steps=(int)raw;
            double previous=0;
            for(int i=0;i<=steps;i++)
            {cancellation.ThrowIfCancellationRequested();double t=(double)i/steps,x=a.X+(b.X-a.X)*t,z=a.Z+(b.Z-a.Z)*t;
                if(!terrain.TryGetHeight(x,z,out double y)||!RiverHash.Finite(y))return false;
                if((upper&&i==steps||!upper&&i==0)&&Math.Abs(y-edgeHeight)>.1)return false;
                if(i>0&&previous-y>profile.MaximumSlope*distance/steps+1e-8)return false;
                if(i>0&&y>previous+1e-8)return false;
                previous=y;
            }
            return true;
        }
    }
}
