using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TerraLoom.Core;
using TerraLoom.Core.Unity;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Rivers.Unity
{
    /// <summary>Unpublished owned-tile earthworks for one validated route-bound fall.
    /// Owns every cloned asset until the caller either validates and transfers it or disposes it.</summary>
    public sealed class PreparedFallEarthworks : IDisposable
    {
        public TerrainData TerrainData { get; private set; }
        public Mesh Solid { get; private set; }
        public Mesh[] Water { get; private set; }
        public RiverFallCandidate Candidate { get; }
        public RiverFallEarthworksSite Site { get; }
        /// <summary>Conservative no-crossing reservation for the first physical fall release.</summary>
        public AreaReservation ProtectedFootprint(RiverProfile profile)
        {
            if(profile==null)throw new ArgumentNullException(nameof(profile));
            var across=Candidate.Recipe.Fall.Across;
            double half=Candidate.Recipe.PoolWidth*.5+profile.BankWidth;
            double minX=double.PositiveInfinity,minZ=double.PositiveInfinity,maxX=double.NegativeInfinity,maxZ=double.NegativeInfinity;
            foreach(var point in new[]{Candidate.UpperStart,Candidate.Lip,Candidate.Impact,Candidate.LowerEnd})
            foreach(double sign in new[]{-1.0,1.0})
            {
                double x=point.X+across.X*half*sign,z=point.Z+across.Z*half*sign;
                minX=Math.Min(minX,x);minZ=Math.Min(minZ,z);maxX=Math.Max(maxX,x);maxZ=Math.Max(maxZ,z);
            }
            return new AreaReservation("fall:"+Candidate.Site.Id+":protected",Candidate.RequestId,
                new WorldBounds(minX,minZ,maxX,maxZ),ReservationStrength.Hard,ReservationPurpose.ProtectedArea);
        }
        private bool disposed;

        private PreparedFallEarthworks(RiverFallCandidate candidate,RiverFallEarthworksSite site)
        {Candidate=candidate;Site=site;}

        /// <summary>Transfers validated assets to a Rivers PreparedPublication only after
        /// all meshes have been installed on its staged root.</summary>
        internal TerrainData TakeTerrainAndMeshes()
        {
            if(disposed||!TerrainData||!Solid||Water==null)throw new InvalidOperationException("Fall earthworks already transferred or disposed.");
            var data=TerrainData;TerrainData=null;Solid=null;Water=null;disposed=true;return data;
        }

        public static PreparedFallEarthworks Prepare(RiverFallCandidate candidate,PlanSnapshot snapshot,
            TerraLoomWorld world,TerrainData source,RiverProfile profile,CancellationToken cancellation=default)
        {
            if(snapshot==null)throw new ArgumentNullException(nameof(snapshot));
            var site=RiverFallEarthworksSite.Inspect(candidate,snapshot.Identity,world,source,profile,cancellation);
            VerifySource(snapshot,world,source,cancellation);
            var result=new PreparedFallEarthworks(candidate,site);
            try
            {
                cancellation.ThrowIfCancellationRequested();
                result.TerrainData=Object.Instantiate(source);
                result.TerrainData.name="Prepared owned fall terrain";
                CarvePool(result.TerrainData,world.Terrain.transform.position,candidate,profile,cancellation);
                cancellation.ThrowIfCancellationRequested();
                var holes=new bool[site.HoleHeight,site.HoleWidth];
                result.TerrainData.SetHoles(site.HoleX,site.HoleZ,holes);
                result.Solid=BuildSolid(source,result.TerrainData,world.Terrain.transform.position,candidate,site,profile,cancellation);
                result.Water=WaterfallWaterGeometry.Build(candidate.Recipe,(float)site.UpperLength,
                    (float)site.LowerLength,cancelled:()=>cancellation.IsCancellationRequested);
                ValidateWaterJoins(result.Water);
                ValidateContacts(result,world.Terrain.transform.position,profile,cancellation);
                cancellation.ThrowIfCancellationRequested();
                return result;
            }
            catch{result.Dispose();throw;}
        }

        private static void VerifySource(PlanSnapshot snapshot,TerraLoomWorld world,TerrainData source,
            CancellationToken cancellation)
        {
            if(snapshot.Identity.Seed!=world.Seed||snapshot.Identity.Revision!=world.Revision||
                !(snapshot.Terrain.Heights is GridHeightSource grid))
                throw new InvalidOperationException("Fall candidate source is not the current owned grid revision.");
            int n=source.heightmapResolution;var size=source.size;var origin=world.Terrain.transform.position;
            if(grid.XCount!=n||grid.ZCount!=n||Math.Abs(grid.MinX-origin.x)>1e-5||
                Math.Abs(grid.MinZ-origin.z)>1e-5||Math.Abs(grid.MaxX-origin.x-size.x)>1e-5||
                Math.Abs(grid.MaxZ-origin.z-size.z)>1e-5)
                throw new InvalidOperationException("Fall source grid dimensions or bounds changed after planning.");
            var heights=source.GetHeights(0,0,n,n);
            for(int z=0;z<n;z++)
            {
                cancellation.ThrowIfCancellationRequested();
                double wz=origin.z+(double)size.z*z/(n-1);
                for(int x=0;x<n;x++)
                {
                    double wx=origin.x+(double)size.x*x/(n-1);
                    if(!grid.TryGetHeight(wx,wz,out double planned)||
                        Math.Abs(planned-(origin.y+(double)heights[z,x]*size.y))>1e-4)
                        throw new InvalidOperationException("Owned fall TerrainData changed after route planning.");
                }
            }
        }

        private static void CarvePool(TerrainData data,Vector3 origin,RiverFallCandidate candidate,
            RiverProfile profile,CancellationToken cancellation)
        {
            var fall=candidate.Recipe.Fall;int n=data.heightmapResolution;
            if((long)n*n>4200000)throw new InvalidOperationException("Fall terrain height grid exceeds preparation budget.");
            var values=data.GetHeights(0,0,n,n);var size=data.size;
            double finish=Math.Min(candidate.Recipe.OutletStation+candidate.Recipe.OutletTransitionLength,
                (candidate.LowerEnd.X-fall.Impact.X)*fall.Forward.X+(candidate.LowerEnd.Z-fall.Impact.Z)*fall.Forward.Z);
            for(int z=0;z<n;z++)
            {
                cancellation.ThrowIfCancellationRequested();
                double wz=origin.z+size.z*z/(n-1);
                for(int x=0;x<n;x++)
                {
                    double wx=origin.x+size.x*x/(n-1);
                    double dx=wx-fall.Impact.X,dz=wz-fall.Impact.Z;
                    double station=dx*fall.Forward.X+dz*fall.Forward.Z;
                    if(station<0||station>finish)continue;
                    double lateral=Math.Abs(dx*fall.Across.X+dz*fall.Across.Z);
                    var section=candidate.Recipe.SampleLowerReach(station);
                    if(lateral>section.HalfWidth+profile.BankWidth)continue;
                    double blend=1-Smooth((lateral-section.HalfWidth)/Math.Max(.01,profile.BankWidth));
                    // The outlet transition ends at the unmodified terrain instead of a hard cut.
                    double endFade=Math.Min(1,(finish-station)/Math.Max(.25,profile.Width*.5));
                    blend*=Smooth(endFade);
                    double old=origin.y+values[z,x]*size.y;
                    double bed=section.Surface.Y-section.CentreDepth;
                    double lowered=Math.Min(old,old+(bed-old)*blend);
                    if(lowered<origin.y||lowered>origin.y+size.y)
                        throw new InvalidOperationException("Fall pool bed leaves owned Terrain height range.");
                    values[z,x]=(float)((lowered-origin.y)/size.y);
                }
            }
            data.SetHeights(0,0,values);
        }

        private static Mesh BuildSolid(TerrainData source,TerrainData carved,Vector3 origin,
            RiverFallCandidate candidate,RiverFallEarthworksSite site,RiverProfile profile,CancellationToken cancellation)
        {
            var fall=candidate.Recipe.Fall;var size=source.size;
            double cellX=size.x/source.holesResolution,cellZ=size.z/source.holesResolution;
            var corners=new[]{
                new Vector2((float)(origin.x+site.HoleX*cellX),(float)(origin.z+site.HoleZ*cellZ)),
                new Vector2((float)(origin.x+(site.HoleX+site.HoleWidth)*cellX),(float)(origin.z+site.HoleZ*cellZ)),
                new Vector2((float)(origin.x+site.HoleX*cellX),(float)(origin.z+(site.HoleZ+site.HoleHeight)*cellZ)),
                new Vector2((float)(origin.x+(site.HoleX+site.HoleWidth)*cellX),(float)(origin.z+(site.HoleZ+site.HoleHeight)*cellZ))};
            double Station(Vector2 p)=>(p.x-fall.Lip.X)*fall.Forward.X+(p.y-fall.Lip.Z)*fall.Forward.Z;
            double Lateral(Vector2 p)=>(p.x-fall.Lip.X)*fall.Across.X+(p.y-fall.Lip.Z)*fall.Across.Z;
            double s0=corners.Min(Station),s3=corners.Max(Station),a0=corners.Min(Lateral),a1=corners.Max(Lateral);
            if(s0>=0||s3<=0||a1-a0<fall.Width)
                throw new InvalidOperationException("Fall solid does not contain the full lip and water width.");
            double setback=Math.Min(.08,Math.Min(cellX,cellZ)*.4);
            int across=Math.Max(4,Math.Min(256,(int)Math.Ceiling((a1-a0)/Math.Min(cellX,cellZ))));
            var verts=new List<Vector3>();var tris=new List<int>();
            Vector3 Point(double station,double lateral,double y)=>new Vector3(
                (float)(fall.Lip.X+fall.Forward.X*station+fall.Across.X*lateral),(float)y,
                (float)(fall.Lip.Z+fall.Forward.Z*station+fall.Across.Z*lateral));
            double SourceHeight(double s,double a)=>origin.y+source.GetInterpolatedHeight(
                (float)((fall.Lip.X+fall.Forward.X*s+fall.Across.X*a-origin.x)/size.x),
                (float)((fall.Lip.Z+fall.Forward.Z*s+fall.Across.Z*a-origin.z)/size.z));
            double CarvedHeight(double s,double a)=>origin.y+carved.GetInterpolatedHeight(
                (float)((fall.Lip.X+fall.Forward.X*s+fall.Across.X*a-origin.x)/size.x),
                (float)((fall.Lip.Z+fall.Forward.Z*s+fall.Across.Z*a-origin.z)/size.z));
            double top=fall.Lip.Y-profile.SurfaceOffset+profile.WaterInset;
            double low=fall.Impact.Y-profile.SurfaceOffset+profile.WaterInset;
            double floor=Math.Max(origin.y-1,Math.Min(top,low)-Math.Max(2,profile.Depth*3));
            void Quad(Vector3 p,Vector3 q,Vector3 r,Vector3 t,Vector3 outward)
            {
                int i=verts.Count;verts.Add(p);verts.Add(q);verts.Add(r);verts.Add(t);
                if(Vector3.Dot(Vector3.Cross(q-p,r-p),outward)>=0)
                    tris.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
                else tris.AddRange(new[]{i,i+2,i+1,i,i+3,i+2});
            }
            void Triangle(Vector3 p,Vector3 q,Vector3 r,Vector3 outward)
            {
                int i=verts.Count;verts.Add(p);verts.Add(q);verts.Add(r);
                if(Vector3.Dot(Vector3.Cross(q-p,r-p),outward)>=0)tris.AddRange(new[]{i,i+1,i+2});
                else tris.AddRange(new[]{i,i+2,i+1});
            }
            for(int j=0;j<across;j++)
            {
                cancellation.ThrowIfCancellationRequested();
                double a=a0+(a1-a0)*j/across,b=a0+(a1-a0)*(j+1)/across;
                double Upper(double lateral)=>top+SourceHeight(-Math.Max(setback,profile.SampleSpacing),lateral)-
                    SourceHeight(-Math.Max(setback,profile.SampleSpacing),0);
                double Lower(double lateral)
                {
                    double ground=CarvedHeight(Math.Max(setback,profile.SampleSpacing),lateral);
                    double centre=candidate.Recipe.SampleLowerReach(0).Surface.Y-candidate.Recipe.ChannelDepth;
                    double blend=1-Smooth((Math.Abs(lateral)-fall.Width*.5)/Math.Max(.01,profile.BankWidth));
                    return Math.Min(ground,ground+(centre-ground)*blend);
                }
                var u0=Point(s0,a,SourceHeight(s0,a));var u1=Point(s0,b,SourceHeight(s0,b));
                var h0=Point(-setback,a,Upper(a));var h1=Point(-setback,b,Upper(b));
                var l0=Point(-setback,a,Lower(a));var l1=Point(-setback,b,Lower(b));
                var d0=Point(s3,a,CarvedHeight(s3,a));var d1=Point(s3,b,CarvedHeight(s3,b));
                Quad(u0,u1,h1,h0,Vector3.up);
                Quad(h0,h1,l1,l0,new Vector3((float)fall.Forward.X,0,(float)fall.Forward.Z));
                Quad(l0,l1,d1,d0,Vector3.up);
                if(j==0)Side(u0,h0,l0,d0,a,-1);
                if(j==across-1)Side(u1,h1,l1,d1,b,1);
                Quad(Point(s0,a,floor),Point(-setback,a,floor),Point(-setback,b,floor),Point(s0,b,floor),Vector3.down);
                Quad(Point(-setback,a,floor),Point(s3,a,floor),Point(s3,b,floor),Point(-setback,b,floor),Vector3.down);
                Quad(u0,Point(s0,a,floor),Point(s0,b,floor),u1,new Vector3((float)-fall.Forward.X,0,(float)-fall.Forward.Z));
                Quad(d1,Point(s3,b,floor),Point(s3,a,floor),d0,new Vector3((float)fall.Forward.X,0,(float)fall.Forward.Z));
            }
            void Side(Vector3 u,Vector3 h,Vector3 l,Vector3 d,double lateral,int sign)
            {
                var outwards=new Vector3((float)(fall.Across.X*sign),0,(float)(fall.Across.Z*sign));
                var fu=Point(s0,lateral,floor);var fw=Point(-setback,lateral,floor);var fd=Point(s3,lateral,floor);
                Quad(u,h,fw,fu,outwards);
                Triangle(h,l,fw,outwards);
                Quad(l,d,fd,fw,outwards);
            }
            var mesh=new Mesh{name="Prepared fall closed rock solid"};
            if(verts.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }

        private static void ValidateWaterJoins(Mesh[] meshes)
        {
            if(meshes==null||meshes.Length!=3)throw new InvalidOperationException("Fall needs upper, vertical and lower water meshes.");
            var upper=meshes[0].vertices;var fall=meshes[1].vertices;var lower=meshes[2].vertices;
            for(int i=0;i<17;i++)
            {
                if(Vector3.Distance(upper[upper.Length-17+i],fall[i])>.0001f||
                   Vector3.Distance(fall[fall.Length-17+i],lower[i])>.0001f)
                    throw new InvalidOperationException("Fall water edges are discontinuous.");
            }
        }
        private static void ValidateContacts(PreparedFallEarthworks prepared,Vector3 origin,
            RiverProfile profile,CancellationToken cancellation)
        {
            var terrainHost=new GameObject("Fall terrain contact probe");
            var solidHost=new GameObject("Fall solid contact probe");
            try
            {
                terrainHost.hideFlags=HideFlags.HideAndDontSave;
                solidHost.hideFlags=HideFlags.HideAndDontSave;
                terrainHost.transform.position=origin;
                var terrain=terrainHost.AddComponent<TerrainCollider>();terrain.terrainData=prepared.TerrainData;
                var solid=solidHost.AddComponent<MeshCollider>();solid.sharedMesh=prepared.Solid;
                if(!solid.sharedMesh)throw new InvalidOperationException("Fall wall has no cooked collider mesh.");
                Physics.SyncTransforms();
                var recipe=prepared.Candidate.Recipe;var fall=recipe.Fall;
                var forward=new Vector3((float)fall.Forward.X,0,(float)fall.Forward.Z);
                var across=new Vector3((float)fall.Across.X,0,(float)fall.Across.Z);
                float Down(Vector3 point)
                {
                    var ray=new Ray(point+Vector3.up*2,Vector3.down);
                    bool a=terrain.Raycast(ray,out var th,32),b=solid.Raycast(ray,out var sh,32);
                    if(!a&&!b)throw new InvalidOperationException("Fall contact has no terrain or solid collider at "+point);
                    return a&&b?Mathf.Max(th.point.y,sh.point.y):a?th.point.y:sh.point.y;
                }
                for(int lateralIndex=-4;lateralIndex<=4;lateralIndex++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    float lateral=(float)(lateralIndex*fall.Width/8);
                    var lip=new Vector3((float)fall.Lip.X,(float)fall.Lip.Y,(float)fall.Lip.Z)+across*lateral;
                    var upper=Down(lip-forward*.12f);
                    if(Math.Abs(upper-fall.Lip.Y)>Math.Max(.2,profile.Depth*.3))
                        throw new InvalidOperationException("Fall lip lacks full-width solid support.");
                    var centre=fall.Sample(.5,lateral);
                    var atWater=new Vector3((float)centre.X,(float)centre.Y,(float)centre.Z);
                    if(!solid.Raycast(new Ray(atWater+forward*.06f,-forward),out var wall,.3f))
                        throw new InvalidOperationException("Fall water is not backed by the prepared wall across its width.");
                    if(Vector3.Distance(wall.point,atWater)>.2f)
                        throw new InvalidOperationException("Fall wall is too far from the water surface.");
                }
                double length=prepared.Site.LowerLength;
                int stations=Math.Max(4,Math.Min(128,(int)Math.Ceiling(length/.5)));
                for(int i=1;i<=stations;i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    double station=length*i/stations;
                    var sample=recipe.SampleLowerReach(station);
                    foreach(double lateral in new[]{0.0,-sample.HalfWidth*.8,sample.HalfWidth*.8})
                    {
                        var surface=recipe.SampleLowerSurface(station,lateral);
                        var point=new Vector3((float)surface.X,(float)surface.Y,(float)surface.Z);
                        double depth=surface.Y-Down(point);
                        if(depth<Math.Min(.15,sample.CentreDepth*.4)||depth>sample.CentreDepth+.3)
                            throw new InvalidOperationException("Fall pool/outlet bed loses full-width water contact at station "+station+", lateral "+lateral+": depth "+depth);
                    }
                }
            }
            finally
            {
                Destroy(terrainHost);Destroy(solidHost);
            }
        }
        private static double Smooth(double value)
        {value=Math.Max(0,Math.Min(1,value));return value*value*(3-2*value);}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(Water!=null)foreach(var mesh in Water)Destroy(mesh);
            Destroy(Solid);Destroy(TerrainData);Water=null;Solid=null;TerrainData=null;
        }
        private static void Destroy(Object value)
        {if(!value)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
    }
}
