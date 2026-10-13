using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TerraLoom.Core;

namespace TerraLoom.Rivers
{
    /// <summary>Read-only proposed straight receiving route from the end of the pool
    /// transition to the named mouth. No terrain, geometry or contribution is published.</summary>
    public sealed class RiverFallLowerReach
    {
        public PlanIdentity CoreInputIdentity { get; }
        public PlanIdentity PlanIdentity { get; }
        public string RequestId { get; }
        public double JoinStation { get; }
        public double ChannelHalfWidth { get; }
        public IReadOnlyList<WorldPoint> Water { get; }
        public IReadOnlyList<WorldPoint> Bed { get; }
        public IReadOnlyList<WorldPoint> LeftBank { get; }
        public IReadOnlyList<WorldPoint> RightBank { get; }
        public IReadOnlyList<WaterCorridor> Corridors { get; }
        public IReadOnlyList<AreaReservation> Reservations { get; }
        public IReadOnlyList<CrossingCandidate> CrossingOffers { get; }

        private RiverFallLowerReach(PlanIdentity core,PlanIdentity plan,string request,double join,double halfWidth,WorldPoint[] water,
            WorldPoint[] bed,WorldPoint[] left,WorldPoint[] right,WaterCorridor[] corridors,
            AreaReservation[] reservations,CrossingCandidate[] crossings)
        {
            CoreInputIdentity=core;PlanIdentity=plan;RequestId=request;JoinStation=join;ChannelHalfWidth=halfWidth;
            Water=Array.AsReadOnly(water);Bed=Array.AsReadOnly(bed);
            LeftBank=Array.AsReadOnly(left);RightBank=Array.AsReadOnly(right);
            Corridors=Array.AsReadOnly(corridors);Reservations=Array.AsReadOnly(reservations);
            CrossingOffers=Array.AsReadOnly(crossings);
        }

        internal static bool TryBuild(PlanSnapshot source,WorldBounds bounds,RiverRequest request,
            RiverProfile profile,WaterfallRecipe recipe,double length,out RiverFallLowerReach reach,out string diagnostic,
            CancellationToken cancellation=default)
        {
            reach=null;diagnostic=null;
            double join=recipe.OutletStation+recipe.OutletTransitionLength;
            double distance=length-join;
            if(!RiverHash.Positive(distance)||distance<profile.SampleSpacing)
            {diagnostic="Receiving reach needs sampled space beyond the pool/outlet transition.";return false;}
            double countRaw=Math.Ceiling(distance/Math.Min(profile.SampleSpacing,.5));
            if(!RiverHash.Finite(countRaw)||countRaw<1||countRaw*3>profile.TotalSampleBudget||countRaw>16384)
            {diagnostic="Receiving reach exceeds its bounded station budget.";return false;}
            int count=(int)countRaw;
            var end=recipe.SampleLowerReach(length).Surface;
            if(!source.Terrain.Heights.TryGetHeight(end.X,end.Z,out double mouthGround)||!RiverHash.Finite(mouthGround))
            {diagnostic="Receiving mouth terrain is unavailable.";return false;}
            double startSurface=recipe.SampleLowerReach(join).Surface.Y;
            double terminalSurface=mouthGround+profile.SurfaceOffset-profile.WaterInset;
            double slope=(startSurface-terminalSurface)/distance;
            if(slope<-.000001||slope>profile.MaximumSlope+1e-8)
            {diagnostic="Receiving grade cannot connect the outlet transition to mouth within the maximum slope.";return false;}
            slope=Math.Max(0,slope);
            var water=new WorldPoint[count+1];var bed=new WorldPoint[count+1];
            var left=new WorldPoint[count+1];var right=new WorldPoint[count+1];
            var across=recipe.Fall.Across;double radius=profile.Width*.5+profile.BankWidth;
            var tile=source.Terrain.Heights as GridHeightSource;
            if(request.FallSite.WorldBoundaryMouth&&tile==null)
            {diagnostic="Boundary receiving route requires a finite height grid.";return false;}
            WorldBounds Clip(WorldBounds area)=>tile==null||!request.FallSite.WorldBoundaryMouth?area:
                new WorldBounds(Math.Max(area.MinX,tile.MinX),Math.Max(area.MinZ,tile.MinZ),
                    Math.Min(area.MaxX,tile.MaxX),Math.Min(area.MaxZ,tile.MaxZ));
            WorldBounds? BridgeWindow(WorldBounds area)
            {
                // Every wet corridor under a later full deck needs a named
                // candidate. Clip its permission window behind the pool rather
                // than omitting the corridor entirely.
                var forward=recipe.Fall.Forward;
                double station=join+profile.Width;
                double x=recipe.Fall.Impact.X+forward.X*station;
                double z=recipe.Fall.Impact.Z+forward.Z*station;
                double minX=area.MinX,maxX=area.MaxX,minZ=area.MinZ,maxZ=area.MaxZ;
                if(forward.X>.999)minX=Math.Max(minX,x);
                else if(forward.X<-.999)maxX=Math.Min(maxX,x);
                else if(forward.Z>.999)minZ=Math.Max(minZ,z);
                else if(forward.Z<-.999)maxZ=Math.Min(maxZ,z);
                else return null; // No axis-aligned bridge offer for a diagonal fall.
                return minX<maxX&&minZ<maxZ?new WorldBounds(minX,minZ,maxX,maxZ):(WorldBounds?)null;
            }
            for(int i=0;i<=count;i++)
            {
                cancellation.ThrowIfCancellationRequested();
                double station=join+distance*i/count;
                var section=recipe.SampleLowerReach(station);
                var sampled=section.Surface;
                var centre=new WorldPoint(sampled.X,startSurface-slope*(station-join),sampled.Z);
                if(i>0&&centre.Y>water[i-1].Y+1e-8)
                {diagnostic="Receiving water climbs downstream.";return false;}
                if(i>0&&(water[i-1].Y-centre.Y)/(distance/count)>profile.MaximumSlope+1e-8)
                {diagnostic="Receiving water exceeds the maximum downhill grade.";return false;}
                double ground;
                if(!source.Terrain.Heights.TryGetHeight(centre.X,centre.Z,out ground)||!RiverHash.Finite(ground))
                {diagnostic="Receiving centre terrain is unavailable at station "+station;return false;}
                double floor=centre.Y-section.CentreDepth;
                if(ground<floor-.05||ground-floor>Math.Max(profile.Depth*2+profile.WaterInset+.02,.05))
                {diagnostic="Receiving bed lacks support or exceeds bounded excavation at station "+station;return false;}
                water[i]=centre;bed[i]=new WorldPoint(centre.X,floor,centre.Z);
                foreach(int side in new[]{-1,1})
                {
                    double x=centre.X+side*across.X*radius,z=centre.Z+side*across.Z*radius;
                    if(!source.Terrain.Heights.TryGetHeight(x,z,out double bank)||!RiverHash.Finite(bank)||
                       bank<centre.Y-.05||!bounds.Contains(new WorldPoint(x,bank,z)))
                    {diagnostic="Receiving bank lacks full-width support at station "+station;return false;}
                    var point=new WorldPoint(x,bank,z);
                    if(side<0)left[i]=point;else right[i]=point;
                }
                if(i>0&&bed[i].Y>bed[i-1].Y+1e-8)
                {diagnostic="Receiving bed climbs downstream outside the pool/sill.";return false;}
            }
            var corridors=new List<WaterCorridor>();var reservations=new List<AreaReservation>();
            var crossings=new List<CrossingCandidate>();
            for(int i=0;i<count;i++)
            {
                cancellation.ThrowIfCancellationRequested();
                string id="fall:"+request.FallSite.Id+":lower:"+i;
                var wet=Clip(Envelope(water[i],water[i+1],profile.Width*.5));
                var banks=Clip(Envelope(water[i],water[i+1],radius));
                if(!bounds.Contains(banks)||source.Reservations.Any(r=>r.Strength==ReservationStrength.Hard&&r.Bounds.Overlaps(banks)))
                {diagnostic="Receiving corridor intersects bounds or a hard reservation at segment "+i;return false;}
                double waterTop=Math.Max(water[i].Y,water[i+1].Y);
                var corridor=new WaterCorridor(id,wet,banks,Math.Min(bed[i].Y,bed[i+1].Y),
                    waterTop,Math.Max(waterTop,Math.Max(Math.Max(left[i].Y,right[i].Y),
                        Math.Max(left[i+1].Y,right[i+1].Y))));
                corridors.Add(corridor);
                reservations.Add(new AreaReservation(id+":bed",id,wet,ReservationStrength.Hard,ReservationPurpose.RiverBed));
                reservations.Add(new AreaReservation(id+":bank",id,banks,ReservationStrength.Soft,
                    ReservationPurpose.RiverBank,profile.BankTransitionCost,"fall-bank:"+request.Id));
                var bridgeWindow=BridgeWindow(banks);
                if(bridgeWindow.HasValue&&bridgeWindow.Value.Overlaps(wet))
                    crossings.Add(new CrossingCandidate(id+":bridge",id,bridgeWindow.Value,
                        CrossingKind.Bridge,profile.BridgeClearance));
            }
            var domain=RiverPlanner.ProfileVersion(profile,bounds,new[]{request});
            var planIdentity=RiverPlanner.IdentityFor(source.Identity,domain);
            reach=new RiverFallLowerReach(source.Identity,planIdentity,request.Id,join,profile.Width*.5,water,bed,left,right,
                corridors.ToArray(),reservations.ToArray(),crossings.ToArray());
            return true;
        }

        private static WorldBounds Envelope(WorldPoint a,WorldPoint b,double radius)=>
            new WorldBounds(Math.Min(a.X,b.X)-radius,Math.Min(a.Z,b.Z)-radius,
                Math.Max(a.X,b.X)+radius,Math.Max(a.Z,b.Z)+radius);
    }
}
