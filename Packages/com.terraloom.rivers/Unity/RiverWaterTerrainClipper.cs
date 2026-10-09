using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace TerraLoom.Rivers.Unity
{
    /// <summary>Clips inset water against the actual staged heightfield without moving its water
    /// level or excavating outside the authorized channel. Bounded subdivision and wet-side edge
    /// bisection are sampled approximations, not a continuous intersection proof.</summary>
    public static class RiverWaterTerrainClipper
    {
        private readonly struct Vertex
        {
            public readonly Vector3 Position;
            public readonly Vector2 Uv;
            public readonly float HalfWidth;
            public Vertex(Vector3 position,Vector2 uv,float halfWidth=0){Position=position;Uv=uv;HalfWidth=halfWidth;}
            public static Vertex Lerp(Vertex a,Vertex b,float t)
                => new Vertex(Vector3.Lerp(a.Position,b.Position,t),Vector2.Lerp(a.Uv,b.Uv,t),Mathf.Lerp(a.HalfWidth,b.HalfWidth,t));
        }

        /// <summary>Returns an owned replacement mesh. Input mesh and TerrainData are unchanged.
        /// All retained vertices target 5 mm physical water clearance; face probes require positive
        /// actual depth. Empty output, exhausted work/vertex budget or unresolved faces throw.</summary>
        public static Mesh Build(Mesh source,Matrix4x4 localToWorld,TerrainData terrain,Vector3 origin,
            int vertexBudget,CancellationToken cancellation=default)
        {
            if(!source||!terrain||vertexBudget<3||vertexBudget>8000000)
                throw new ArgumentException("Water mesh, terrain and bounded vertex budget required.");
            const float margin=.005f;
            const int maximumDepth=8,maximumVisits=2000000;
            int visits=0;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var shoreline=new List<Vector2>();var indices=new List<int>();
            var size=terrain.size;
            float Depth(Vertex v)
            {
                cancellation.ThrowIfCancellationRequested();
                var p=localToWorld.MultiplyPoint3x4(v.Position);
                float x=(p.x-origin.x)/size.x,z=(p.z-origin.z)/size.z;
                if(float.IsNaN(x)||float.IsNaN(z)||float.IsNaN(p.y)||float.IsInfinity(p.y)||x<0||x>1||z<0||z>1)
                    throw new InvalidOperationException("Inset water clipping leaves terrain bounds.");
                return p.y-origin.y-terrain.GetInterpolatedHeight(x,z);
            }
            bool Safe(Vertex a,Vertex b,Vertex c)
            {
                // Centre, all edge midpoints and three near-edge interior samples.
                return Depth(new Vertex((a.Position+b.Position+c.Position)/3,default))>0
                    && Depth(Vertex.Lerp(a,b,.5f))>0 && Depth(Vertex.Lerp(b,c,.5f))>0 && Depth(Vertex.Lerp(c,a,.5f))>0
                    && Depth(new Vertex(a.Position*.45f+b.Position*.45f+c.Position*.1f,default))>0
                    && Depth(new Vertex(b.Position*.45f+c.Position*.45f+a.Position*.1f,default))>0
                    && Depth(new Vertex(c.Position*.45f+a.Position*.45f+b.Position*.1f,default))>0;
            }
            void Emit(Vertex a,Vertex b,Vertex c)
            {
                if(Vector3.Cross(b.Position-a.Position,c.Position-a.Position).sqrMagnitude<1e-16f)return;
                if(vertices.Count>vertexBudget-3)throw new InvalidOperationException("Inset water clipping exceeds the shared geometry vertex budget.");
                foreach(var v in new[]{a,b,c}){indices.Add(vertices.Count);vertices.Add(v.Position);uv.Add(v.Uv);shoreline.Add(new Vector2(v.HalfWidth,0));}
            }
            Vertex Boundary(Vertex a,Vertex b,bool aWet)
            {
                Vertex wet=aWet?a:b,dry=aWet?b:a;
                // Retain the wet endpoint; never round back onto the dry side.
                for(int i=0;i<16;i++)
                {
                    var mid=Vertex.Lerp(wet,dry,.5f);
                    if(Depth(mid)>=margin)wet=mid;else dry=mid;
                }
                return wet;
            }
            void Split(Vertex a,Vertex b,Vertex c,int depth)
            {
                if(depth>=maximumDepth)throw new InvalidOperationException("Inset water shoreline remains unresolved at the subdivision limit. Previous river retained.");
                var ab=Vertex.Lerp(a,b,.5f);var bc=Vertex.Lerp(b,c,.5f);var ca=Vertex.Lerp(c,a,.5f);
                Process(a,ab,ca,depth+1);Process(ab,b,bc,depth+1);
                Process(ca,bc,c,depth+1);Process(ab,bc,ca,depth+1);
            }
            void Process(Vertex a,Vertex b,Vertex c,int depth)
            {
                if(++visits>maximumVisits)throw new InvalidOperationException("Inset water clipping exceeds its work budget.");
                bool wa=Depth(a)>=margin,wb=Depth(b)>=margin,wc=Depth(c)>=margin;
                if(depth==0){Split(a,b,c,depth);return;}
                if(wa&&wb&&wc)
                {
                    if(Safe(a,b,c))Emit(a,b,c);else Split(a,b,c,depth);
                    return;
                }
                if(!wa&&!wb&&!wc)
                {
                    if(Depth(Vertex.Lerp(a,b,.5f))>=margin||Depth(Vertex.Lerp(b,c,.5f))>=margin
                        ||Depth(Vertex.Lerp(c,a,.5f))>=margin
                        ||Depth(new Vertex((a.Position+b.Position+c.Position)/3,default))>=margin)
                        Split(a,b,c,depth);
                    return;
                }
                var input=new[]{a,b,c};var wet=new[]{wa,wb,wc};var polygon=new List<Vertex>(4);
                for(int i=0;i<3;i++)
                {
                    int next=(i+1)%3;
                    if(wet[i])polygon.Add(input[i]);
                    if(wet[i]!=wet[next])polygon.Add(Boundary(input[i],input[next],wet[i]));
                }
                for(int i=1;i+1<polygon.Count;i++)
                {
                    var p=polygon[0];var q=polygon[i];var r=polygon[i+1];
                    if(Safe(p,q,r))Emit(p,q,r);else Split(p,q,r,depth);
                }
            }
            var sourceVertices=source.vertices;var sourceUv=source.uv;var triangles=source.triangles;
            if(sourceUv.Length!=sourceVertices.Length)throw new ArgumentException("Water mesh requires UVs.");
            var sourceShoreline=new List<Vector2>();source.GetUVs(2,sourceShoreline);
            Vertex At(int i)=>new Vertex(sourceVertices[i],sourceUv[i],
                sourceShoreline.Count==sourceVertices.Length?sourceShoreline[i].x:Mathf.Abs(sourceUv[i].x));
            for(int i=0;i<triangles.Length;i+=3)Process(At(triangles[i]),At(triangles[i+1]),At(triangles[i+2]),0);
            if(indices.Count==0)throw new InvalidOperationException("Actual terrain leaves no inset water surface.");
            var result=new Mesh{name="River Water (terrain clipped)",hideFlags=HideFlags.DontSave,
                indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            try
            {
                result.SetVertices(vertices);result.SetUVs(0,uv);result.SetUVs(1,uv);result.SetUVs(2,shoreline);
                var colors=new Color[vertices.Count];for(int i=0;i<colors.Length;i++)colors[i]=Color.white;
                result.colors=colors;result.SetTriangles(indices,0);result.RecalculateNormals();result.RecalculateBounds();
                return result;
            }
            catch{if(Application.isPlaying)UnityEngine.Object.Destroy(result);else UnityEngine.Object.DestroyImmediate(result);throw;}
        }
    }
}
