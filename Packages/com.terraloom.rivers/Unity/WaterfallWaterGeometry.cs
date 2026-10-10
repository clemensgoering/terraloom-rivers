using System;
using System.Collections.Generic;
using System.Linq;
using TerraLoom.Core;
using UnityEngine;

namespace TerraLoom.Rivers.Unity
{
    /// <summary>Shared editor/runtime tessellator for the physical WaterfallRecipe.
    /// Returns Upper/Fall/Lower in that order, with world-space vertices and metre UVs.
    /// Success transfers ownership of all three meshes to the caller; cancellation or
    /// failure destroys partial meshes. Creates no objects, materials or colliders and
    /// writes no terrain. Hosts must validate their actual terrain/solid contacts.</summary>
    public static class WaterfallWaterGeometry
    {
        public static Mesh[] Build(WaterfallRecipe recipe,float upstreamLength=12,float lowerLength=17,
            float renderOffset=.01f,int upperSegments=64,int fallSegments=128,int lowerSegments=128,
            int acrossSegments=16,Func<bool> cancelled=null)
        {
            if(recipe==null)throw new ArgumentNullException(nameof(recipe));
            if(!Finite(upstreamLength)||!Finite(lowerLength)||!Finite(renderOffset)||upstreamLength<=0||upstreamLength>1e6
                ||lowerLength<recipe.OutletStation+recipe.OutletTransitionLength||lowerLength>1e6||Math.Abs(renderOffset)>1)
                throw new ArgumentException("Finite reach lengths must include the complete outlet transition; render offset within +/-1m.");
            if(upperSegments<1||upperSegments>512||fallSegments<1||fallSegments>512||lowerSegments<1||lowerSegments>512||acrossSegments<2||acrossSegments>64)
                throw new ArgumentOutOfRangeException("Bounded segment counts required: longitudinal1..512, across2..64.");
            var meshes=new List<Mesh>();
            void CheckCancelled(){if(cancelled!=null&&cancelled())throw new OperationCanceledException("Waterfall water candidate cancelled.");}
            Mesh Strip(string name,int count,Func<int,float,Vector3> point,Func<int,float> distance)
            {
                var mesh=new Mesh{name=name};meshes.Add(mesh);
                var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();int stride=acrossSegments+1;
                for(int i=0;i<=count;i++)
                {
                    CheckCancelled();
                    for(int j=0;j<=acrossSegments;j++)
                    {
                        float a=(j-acrossSegments*.5f)/(acrossSegments*.5f);
                        vertices.Add(point(i,a)+Vector3.up*renderOffset);uv.Add(new Vector2(a,distance(i)));
                    }
                }
                for(int i=0;i<count;i++)for(int j=0;j<acrossSegments;j++)
                {int n=i*stride+j;triangles.AddRange(new[]{n,n+stride,n+1,n+1,n+stride,n+stride+1});}
                for(int i=0;i<triangles.Count;i+=3)
                {
                    var area=Vector3.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]).sqrMagnitude;
                    if(!Finite(area)||area<=0)throw new InvalidOperationException("Waterfall mesh collapses or overflows Unity float precision; use a suitable local origin/scale.");
                }
                mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return mesh;
            }
            try
            {
                CheckCancelled();
                float upperArc=upstreamLength*Mathf.Sqrt(1+(float)(recipe.UpstreamSlope*recipe.UpstreamSlope));
                Strip("Upper reach to exact lip",upperSegments,(i,a)=>V(recipe.SampleUpperSurface(upstreamLength*(1-i/(float)upperSegments),a*recipe.Fall.Width*.5)),i=>i/(float)upperSegments*upperArc);
                Strip("Parametric fall, zero-run safe",fallSegments,(i,a)=>V(recipe.Fall.Sample(i/(double)fallSegments,a*recipe.Fall.Width*.5)),i=>i/(float)fallSegments*(float)recipe.Fall.ArcLength);
                var stations=Enumerable.Range(0,lowerSegments+1).Select(i=>i/(double)lowerSegments*lowerLength)
                    .Concat(new[]{recipe.PoolStation,recipe.OutletStation,recipe.OutletStation+recipe.OutletTransitionLength}).Distinct().OrderBy(s=>s).ToArray();
                var distances=new float[stations.Length];
                for(int i=1;i<stations.Length;i++)distances[i]=distances[i-1]+Vector3.Distance(V(recipe.SampleLowerReach(stations[i-1]).Surface),V(recipe.SampleLowerReach(stations[i]).Surface));
                Strip("Impact pool and receiving reach",stations.Length-1,(i,a)=>V(recipe.SampleLowerSurface(stations[i],a*recipe.SampleLowerReach(stations[i]).HalfWidth)),i=>distances[i]);
                CheckCancelled();return meshes.ToArray();
            }
            catch
            {
                foreach(var mesh in meshes)if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);
                throw;
            }
        }
        private static Vector3 V(WorldPoint p)=>new Vector3((float)p.X,(float)p.Y,(float)p.Z);
        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
    }
}
