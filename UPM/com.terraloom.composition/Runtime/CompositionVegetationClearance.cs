using System;
using System.Collections.Generic;
using System.Threading;
using TerraLoom.Paths;
using TerraLoom.Rivers;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>Clears existing vegetation from the unpublished carved terrain. The source
    /// TerrainData is borrowed and must never be edited, even when generation fails.</summary>
    internal static class CompositionVegetationClearance
    {
        private readonly struct Corridor
        {
            public readonly Vector2 A, B;
            public readonly float Radius;
            public Corridor(double ax,double az,double bx,double bz,double radius)
            { A=new Vector2((float)ax,(float)az);B=new Vector2((float)bx,(float)bz);Radius=(float)radius; }
        }

        internal static void Apply(TerrainData target,TerrainData source,Vector3 origin,
            RiverPlan rivers,RiverProfile riverProfile,PathPlan paths,PathProfile pathProfile,
            CancellationToken cancellation)
        {
            if(!target||!source||target==source)
                throw new InvalidOperationException("Vegetation clearance requires a separate, unpublished terrain copy.");
            if(rivers==null||paths==null||riverProfile==null||pathProfile==null)
                throw new ArgumentException("Final river and path plans and profiles are required for vegetation clearance.");
            var corridors=new List<Corridor>();
            foreach(var route in rivers.Routes)
                for(int i=1;i<route.WaterPolyline.Count;i++)
                {
                    var a=route.WaterPolyline[i-1];var b=route.WaterPolyline[i];
                    corridors.Add(new Corridor(a.X,a.Z,b.X,b.Z,riverProfile.Width*.5+riverProfile.BankWidth));
                }
            foreach(var route in paths.Routes)
                for(int i=0;i<route.Surfaces.Count;i++)
                {
                    var a=route.Waypoints[i];var b=route.Waypoints[i+1];
                    bool landing=route.Surfaces[i]==PathSurface.Ground &&
                        ((i>0&&route.Surfaces[i-1]==PathSurface.Bridge)||
                         (i+1<route.Surfaces.Count&&route.Surfaces[i+1]==PathSurface.Bridge));
                    double radius=pathProfile.Width*.5+(landing?pathProfile.BridgeLateralPadding:0);
                    corridors.Add(new Corridor(a.X,a.Z,b.X,b.Z,radius));
                }
            if(corridors.Count==0)return;

            var prototypes=target.treePrototypes;
            var footprints=new float[prototypes.Length];
            for(int i=0;i<prototypes.Length;i++)footprints[i]=Footprint(prototypes[i].prefab);
            var oldTrees=target.treeInstances;
            var kept=new List<TreeInstance>(oldTrees.Length);
            foreach(var tree in oldTrees)
            {
                cancellation.ThrowIfCancellationRequested();
                if(tree.prototypeIndex<0||tree.prototypeIndex>=footprints.Length)
                    throw new InvalidOperationException("Terrain tree prototype index is invalid.");
                var point=new Vector2(origin.x+tree.position.x*target.size.x,origin.z+tree.position.z*target.size.z);
                float extent=footprints[tree.prototypeIndex]*tree.widthScale;
                if(!Intersects(point,extent,corridors))kept.Add(tree);
            }
            if(kept.Count!=oldTrees.Length)target.SetTreeInstances(kept.ToArray(),false);

            int width=target.detailWidth,height=target.detailHeight;
            if(width==0||height==0)return;
            float cellX=target.size.x/width,cellZ=target.size.z/height;
            // Build one reusable mask from each narrow segment's cell-space bounds.
            // This avoids testing every terrain cell against every curve segment and layer.
            var clear=new bool[height,width];
            foreach(var c in corridors)
            {
                cancellation.ThrowIfCancellationRequested();
                int minX=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(c.A.x,c.B.x)-c.Radius-origin.x)/cellX),0,width-1);
                int maxX=Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(c.A.x,c.B.x)+c.Radius-origin.x)/cellX),0,width-1);
                int minZ=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(c.A.y,c.B.y)-c.Radius-origin.z)/cellZ),0,height-1);
                int maxZ=Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(c.A.y,c.B.y)+c.Radius-origin.z)/cellZ),0,height-1);
                for(int z=minZ;z<=maxZ;z++)for(int x=minX;x<=maxX;x++)
                {
                    if(clear[z,x])continue;
                    float left=origin.x+x*cellX,bottom=origin.z+z*cellZ;
                    clear[z,x]=IntersectsCell(left,bottom,left+cellX,bottom+cellZ,c);
                }
            }
            for(int layer=0;layer<target.detailPrototypes.Length;layer++)
            {
                cancellation.ThrowIfCancellationRequested();
                var cells=target.GetDetailLayer(0,0,width,height,layer);
                bool changed=false;
                for(int z=0;z<height;z++)for(int x=0;x<width;x++)
                {
                    if(cells[z,x]==0)continue;
                    if((x&31)==0)cancellation.ThrowIfCancellationRequested();
                    if(!clear[z,x])continue;
                    cells[z,x]=0;changed=true;
                }
                if(changed)target.SetDetailLayer(0,0,layer,cells);
            }
        }

        private static float Footprint(GameObject prefab)
        {
            if(!prefab)throw new InvalidOperationException("Terrain tree prototype has no prefab; its footprint cannot be measured.");
            float radius=0;bool found=false;
            foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh)continue;
                var bounds=filter.sharedMesh.bounds;
                var root=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    var p=root.MultiplyPoint3x4(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z)));
                    radius=Mathf.Max(radius,new Vector2(p.x,p.z).magnitude);found=true;
                }
            }
            if(!found)throw new InvalidOperationException("Terrain tree prefab has no mesh footprint: "+prefab.name);
            return radius;
        }

        private static bool Intersects(Vector2 point,float extent,List<Corridor> corridors)
        {
            foreach(var c in corridors)if(DistanceSquared(point,c.A,c.B)<=Square(c.Radius+extent))return true;
            return false;
        }
        private static bool IntersectsCell(float x0,float z0,float x1,float z1,Corridor c)
        {
            if(c.A.x+c.Radius<x0&&c.B.x+c.Radius<x0||c.A.x-c.Radius>x1&&c.B.x-c.Radius>x1||
               c.A.y+c.Radius<z0&&c.B.y+c.Radius<z0||c.A.y-c.Radius>z1&&c.B.y-c.Radius>z1)return false;
            if(Inside(c.A,x0,z0,x1,z1)||Inside(c.B,x0,z0,x1,z1))return true;
            var p0=new Vector2(x0,z0);var p1=new Vector2(x1,z0);
            var p2=new Vector2(x1,z1);var p3=new Vector2(x0,z1);
            float distance=Mathf.Min(Mathf.Min(SegmentDistanceSquared(c.A,c.B,p0,p1),SegmentDistanceSquared(c.A,c.B,p1,p2)),
                Mathf.Min(SegmentDistanceSquared(c.A,c.B,p2,p3),SegmentDistanceSquared(c.A,c.B,p3,p0)));
            return distance<=Square(c.Radius);
        }
        private static bool Inside(Vector2 p,float x0,float z0,float x1,float z1)=>p.x>=x0&&p.x<=x1&&p.y>=z0&&p.y<=z1;
        private static float Square(float value)=>value*value;
        private static float DistanceSquared(Vector2 p,Vector2 a,Vector2 b)
        {var ab=b-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/Mathf.Max(ab.sqrMagnitude,1e-12f));return (p-a-t*ab).sqrMagnitude;}
        private static float SegmentDistanceSquared(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {
            float Cross(Vector2 u,Vector2 v)=>u.x*v.y-u.y*v.x;
            var r=b-a;var s=d-c;float denominator=Cross(r,s);
            if(Mathf.Abs(denominator)>1e-9f)
            {
                float t=Cross(c-a,s)/denominator,u=Cross(c-a,r)/denominator;
                if(t>=0&&t<=1&&u>=0&&u<=1)return 0;
            }
            return Mathf.Min(Mathf.Min(DistanceSquared(a,c,d),DistanceSquared(b,c,d)),
                Mathf.Min(DistanceSquared(c,a,b),DistanceSquared(d,a,b)));
        }
    }
}
