using System;
using System.Threading;
using TerraLoom.Core;
using TerraLoom.Core.Unity;
using UnityEngine;

namespace TerraLoom.Rivers.Unity
{
    /// <summary>Read-only grid/ownership preflight for a single explicit owned-terrain fall.
    /// A successful inspection is not a publication or physical-contact proof.</summary>
    public readonly struct RiverFallEarthworksSite
    {
        public int HoleX { get; }
        public int HoleZ { get; }
        public int HoleWidth { get; }
        public int HoleHeight { get; }
        public double UpperLength { get; }
        public double LowerLength { get; }

        private RiverFallEarthworksSite(int x,int z,int width,int height,double upper,double lower)
        {HoleX=x;HoleZ=z;HoleWidth=width;HoleHeight=height;UpperLength=upper;LowerLength=lower;}

        public static RiverFallEarthworksSite Inspect(RiverFallCandidate candidate,PlanIdentity expected,
            TerraLoomWorld world,TerrainData source,RiverProfile profile,CancellationToken cancellation=default)
        {
            if(candidate==null||world==null||source==null||profile==null)
                throw new ArgumentNullException(candidate==null?nameof(candidate):world==null?nameof(world):source==null?nameof(source):nameof(profile));
            cancellation.ThrowIfCancellationRequested();
            if(candidate.CoreInputIdentity!=expected)throw new InvalidOperationException("Fall site uses a stale Core source identity.");
            if(!world.OwnsTerrain||!world.Terrain)
                throw new InvalidOperationException("Fall earthworks require a Core-owned terrain tile.");
            var fall=candidate.Recipe.Fall;
            // The first earthworks tile has an axis-aligned wall. Rotated sites are rejected
            // here rather than cutting an AABB that would miss the actual water footprint.
            bool alongX=Math.Abs(Math.Abs(fall.Forward.X)-1)<1e-6&&Math.Abs(fall.Forward.Z)<1e-6;
            bool alongZ=Math.Abs(Math.Abs(fall.Forward.Z)-1)<1e-6&&Math.Abs(fall.Forward.X)<1e-6;
            if(!alongX&&!alongZ)throw new InvalidOperationException("Prepared fall earthworks currently require an axis-aligned owned tile.");
            var position=world.Terrain.transform.position;var size=source.size;
            int resolution=source.holesResolution;
            if(resolution<16||resolution>2048||source.heightmapResolution!=resolution+1)
                throw new InvalidOperationException("Owned fall tile needs matching hole/height grids of 16..2048 cells.");
            double cellX=size.x/resolution,cellZ=size.z/resolution;
            double stationCell=alongX?cellX:cellZ,acrossCell=alongX?cellZ:cellX;
            double halfLength=Math.Max(stationCell*2,profile.SampleSpacing);
            double halfWidth=candidate.Recipe.PoolWidth*.5+profile.BankWidth+acrossCell*2;
            if(!Finite(halfLength)||!Finite(halfWidth)||halfLength<=0||halfWidth<=0)
                throw new InvalidOperationException("Fall hole footprint is not finite and positive.");
            double upper=(fall.Lip.X-candidate.UpperStart.X)*fall.Forward.X+
                (fall.Lip.Z-candidate.UpperStart.Z)*fall.Forward.Z;
            double lower=(candidate.LowerEnd.X-fall.Impact.X)*fall.Forward.X+
                (candidate.LowerEnd.Z-fall.Impact.Z)*fall.Forward.Z;
            if(upper<halfLength||lower<candidate.Recipe.OutletStation+candidate.Recipe.OutletTransitionLength)
                throw new InvalidOperationException("Fall site lacks upper support or the complete pool/outlet transition.");
            double minX=fall.Lip.X-(alongX?halfLength:halfWidth),maxX=fall.Lip.X+(alongX?halfLength:halfWidth);
            double minZ=fall.Lip.Z-(alongZ?halfLength:halfWidth),maxZ=fall.Lip.Z+(alongZ?halfLength:halfWidth);
            int x0=(int)Math.Floor((minX-position.x)/cellX),x1=(int)Math.Ceiling((maxX-position.x)/cellX);
            int z0=(int)Math.Floor((minZ-position.z)/cellZ),z1=(int)Math.Ceiling((maxZ-position.z)/cellZ);
            if(x0<1||z0<1||x1>=resolution-1||z1>=resolution-1||x1<=x0||z1<=z0)
                throw new InvalidOperationException("Fall hole and two-cell bank support must remain inside one owned terrain tile.");
            var existing=source.GetHoles(x0,z0,x1-x0,z1-z0);
            for(int z=0;z<existing.GetLength(0);z++)for(int x=0;x<existing.GetLength(1);x++)
            {
                cancellation.ThrowIfCancellationRequested();
                if(!existing[z,x])throw new InvalidOperationException("Fall footprint overlaps an existing terrain hole.");
            }
            return new RiverFallEarthworksSite(x0,z0,x1-x0,z1-z0,upper,lower);
        }
        private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    }
}
