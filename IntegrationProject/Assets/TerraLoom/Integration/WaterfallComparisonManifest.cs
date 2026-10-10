using System;
using System.Collections.Generic;
using TerraLoom.Core;
using TerraLoom.Rivers;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>Measured comparison export. Same local foot intent alone is not same camera
    /// if final terrain differs: export actual world eyes, rotations and height separately.</summary>
    [Serializable] public sealed class WaterfallComparisonManifest
    {
        public int schemaVersion=1,profileVersion=1,seed,imageWidth=1600,imageHeight=1000;
        public string recipeRevision="anchored-waterfall-v1-r2";
        public string terrainPatch;
        public string units="metres",status="Proposed common V1 profile; not renderer selection, rights, navigation or art acceptance";
        public string stationConvention="Station0 at lip localZ=-1; station=localZ+1 increases downstream";
        public string fallBasis="Monotone cubic Hermite Y over horizontal station; derivative at lip=-upstreamSlope, impact=0";
        public double[] originWorld={80,12.4,89};public double yawDegrees=180;
        public double[] lipLocal,impactLocal,poolLocal,outletSillLocal,receivingJoinLocal={0,0,17};
        public double upperLevel,lowerLevel,upperLevelLocal,lowerLevelLocal,channelWidth,poolWidth,channelDepth,poolDepth,sillDepth,upstreamSlope,downstreamSlope,outletTransitionLength;
        public double impactStation,poolStation,sillStation,receivingJoinStation=18;
        public List<StationEvidence> stations=new List<StationEvidence>();
        public List<CameraEvidence> cameras=new List<CameraEvidence>();
        public List<BedEvidence> finalTerrain=new List<BedEvidence>();
        [Serializable] public sealed class BedEvidence{public string anchor;public double station,waterLocal,designBedLocal;public float actualBedLocal;}
        [Serializable] public sealed class StationEvidence{public double station;public double[] surfaceLocal;public double width,depth;public string section;}
        [Serializable] public sealed class CameraEvidence{public string name;public float[] eyeWorld,eyeLocal,targetLocal,rotationWorld,footLocal;public float eyeAboveTerrain,fov;public bool groundedIntent;}
        public WaterfallComparisonManifest(WaterfallProfile p,int seed)
        {
            this.seed=seed;profileVersion=(int)p.Version;
            double[] Local(WorldPoint a)=>new[]{80-a.X,a.Y-12.4,89-a.Z};
            lipLocal=Local(p.Lip);impactLocal=Local(p.Impact);poolLocal=Local(p.Pool);outletSillLocal=Local(p.Outlet);
            upperLevel=p.UpperWaterLevel;lowerLevel=p.LowerWaterLevel;channelWidth=p.ChannelWidth;poolWidth=p.PoolWidth;
            upperLevelLocal=upperLevel-12.4;lowerLevelLocal=lowerLevel-12.4;
            channelDepth=p.ChannelDepth;poolDepth=p.PoolDepth;sillDepth=p.OutletSillDepth;
            upstreamSlope=p.UpstreamSlope;downstreamSlope=p.DownstreamSlope;outletTransitionLength=p.OutletTransitionLength;
            impactStation=p.ImpactStation;poolStation=p.PoolStation;sillStation=p.OutletStation;
            var stops=new SortedSet<double>{0,p.ImpactStation,p.PoolStation,p.OutletStation,18};
            for(int i=-180;i<=180;i++)stops.Add(i/10d);
            foreach(double s in stops){var a=p.Sample(s);stations.Add(new StationEvidence{station=s,surfaceLocal=Local(a.Surface),width=a.HalfWidth*2,depth=a.CentreDepth,section=a.Section.ToString()});}
        }
        public void RecordCamera(string name,Camera camera,Vector3? foot,float above,Vector3 target)
        {
            var eye=camera.transform.position;var angles=camera.transform.eulerAngles;
            cameras.Add(new CameraEvidence{name=name,eyeWorld=new[]{eye.x,eye.y,eye.z},eyeLocal=new[]{80-eye.x,eye.y-12.4f,89-eye.z},targetLocal=new[]{80-target.x,target.y-12.4f,89-target.z},rotationWorld=new[]{angles.x,angles.y,angles.z},
                footLocal=foot.HasValue?new[]{80-foot.Value.x,foot.Value.y-12.4f,89-foot.Value.z}:Array.Empty<float>(),
                eyeAboveTerrain=above,fov=camera.fieldOfView,groundedIntent=foot.HasValue});
        }
        public void RecordBed(WaterfallProfile profile,Terrain terrain)
        {
            var names=new[]{"lip","impact","pool","outlet-sill","receiving-join"};
            var stops=new[]{0,profile.ImpactStation,profile.PoolStation,profile.OutletStation,18};
            for(int i=0;i<stops.Length;i++)
            {
                var sample=profile.Sample(stops[i]);var p=sample.Surface;
                float actual=terrain.SampleHeight(new Vector3((float)p.X,0,(float)p.Z))+terrain.transform.position.y-12.4f;
                finalTerrain.Add(new BedEvidence{anchor=names[i],station=stops[i],waterLocal=p.Y-12.4,designBedLocal=p.Y-12.4-sample.CentreDepth,actualBedLocal=actual});
            }
        }
    }
}
