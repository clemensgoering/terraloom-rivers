using System;
using TerraLoom.Core;

namespace TerraLoom.Rivers
{
    /// <summary>Version2 bounded planar falling-water surface, parameterized by progress
    /// rather than X/Z height. Explicit horizontal flow direction keeps the cross-frame
    /// stable even for zero horizontal run. Does not find terrain or authorize writes.</summary>
    public sealed class WaterfallFallSurface
    {
        public int Version=>2;
        public WorldPoint Lip {get;}
        public WorldPoint Impact {get;}
        public WorldPoint Forward {get;}
        public WorldPoint Across {get;}
        public double Width {get;}
        public double HorizontalRun {get;}
        public double ArcLength {get;}
        public WaterfallFallSurface(WorldPoint lip,WorldPoint impact,double forwardX,double forwardZ,double width)
        {
            foreach(double v in new[]{lip.X,lip.Y,lip.Z,impact.X,impact.Y,impact.Z,forwardX,forwardZ,width})
                if(double.IsNaN(v)||double.IsInfinity(v)||Math.Abs(v)>1e9)throw new ArgumentException("Finite bounded waterfall coordinates required.");
            double norm=Math.Sqrt(forwardX*forwardX+forwardZ*forwardZ);
            if(norm<=1e-9||width<=0||lip.Y<=impact.Y)throw new ArgumentException("Explicit horizontal direction, width and positive drop required.");
            var forward=new WorldPoint(forwardX/norm,0,forwardZ/norm);
            var across=new WorldPoint(forward.Z,0,-forward.X);
            double dx=impact.X-lip.X,dz=impact.Z-lip.Z,run=dx*forward.X+dz*forward.Z;
            if(run<0||Math.Abs(dx*across.X+dz*across.Z)>1e-6)throw new ArgumentException("Impact must be downstream on the explicit flow axis.");
            Lip=lip;Impact=impact;Forward=forward;Across=across;Width=width;HorizontalRun=run;
            ArcLength=Math.Sqrt(run*run+(lip.Y-impact.Y)*(lip.Y-impact.Y));
        }
        /// <summary>Progress0=lip,1=impact. Lateral metres in +/-half width. For a vertical
        /// fall, many Y values intentionally share one X/Z. Never use as a heightfield.</summary>
        public WorldPoint Sample(double progress,double lateral=0)
        {
            if(double.IsNaN(progress)||progress<0||progress>1||double.IsNaN(lateral)||Math.Abs(lateral)>Width*.5)
                throw new ArgumentOutOfRangeException();
            return new WorldPoint(Lip.X+(Impact.X-Lip.X)*progress+Across.X*lateral,
                Lip.Y+(Impact.Y-Lip.Y)*progress,Lip.Z+(Impact.Z-Lip.Z)*progress+Across.Z*lateral);
        }
        public double DistanceAt(double progress){Sample(progress);return ArcLength*progress;}
    }
}
