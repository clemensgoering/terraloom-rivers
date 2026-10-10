using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TerraLoom.Integration
{
    /// <summary>Original runtime rock, branch, leaf-card and grass geometry for the bounded
    /// landscape evaluation. Shared prototypes replace the earlier cones and faceted balls.</summary>
    public static class PrototypeMeshRecipe
    {
        public sealed class Writer
        {
            public readonly List<Vector3> V=new List<Vector3>();
            public readonly List<Vector2> U=new List<Vector2>();
            public readonly List<int> T=new List<int>();
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int n=V.Count;V.AddRange(new[]{a,b,c,d});U.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
                T.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            public Mesh Finish(string name)
            {
                var m=new Mesh{name=name,indexFormat=V.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                m.SetVertices(V);m.SetUVs(0,U);m.SetTriangles(T,0);m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();return m;
            }
            public void Branch(Vector3 a,Vector3 b,float ra,float rb,int sides=9)
            {
                var axis=(b-a).normalized;var right=Vector3.Cross(axis,Vector3.forward).normalized;
                if(right.sqrMagnitude<.1f)right=Vector3.right;var up=Vector3.Cross(axis,right);
                for(int i=0;i<sides;i++)
                {
                    float t=i*Mathf.PI*2/sides,q=(i+1)*Mathf.PI*2/sides;
                    Vector3 p=right*Mathf.Cos(t)+up*Mathf.Sin(t),r=right*Mathf.Cos(q)+up*Mathf.Sin(q);
                    Quad(a+p*ra,a+r*ra,b+r*rb,b+p*rb);
                }
            }
        }
        private static float Next(System.Random r,float a,float b)=>a+(b-a)*(float)r.NextDouble();
        public static Mesh Rock(int seed)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            const int rings=18,sides=28;
            for(int j=0;j<=rings;j++)for(int i=0;i<=sides;i++)
            {
                float lat=j*Mathf.PI/rings,lon=i*2*Mathf.PI/sides;
                var n=new Vector3(Mathf.Sin(lat)*Mathf.Cos(lon),Mathf.Cos(lat),Mathf.Sin(lat)*Mathf.Sin(lon));
                float noise=Mathf.PerlinNoise(n.x*2.4f+seed,n.z*2.4f+n.y*.9f+seed*.71f);
                float shelf=1+.025f*Mathf.Sin(n.y*7+noise*3);
                v.Add(new Vector3(n.x,n.y*.8f,n.z)*(shelf*(.88f+.25f*noise)));
                uv.Add(new Vector2(lon/6.28f,lat/3.14f)*3);
            }
            for(int j=0;j<rings;j++)for(int i=0;i<sides;i++)
            {int a=j*(sides+1)+i,b=a+sides+1;t.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            var mesh=new Mesh{name="Original layered weathered rock "+seed};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        public static (Mesh trunk,Mesh leaves) Tree(int seed)
        {
            var random=new System.Random(seed);var trunk=new Writer();var leaves=new Writer();
            trunk.Branch(new Vector3(0,-.25f,0),new Vector3(.25f,6.6f,.12f),.22f,.045f,12);
            for(int branch=0;branch<13;branch++)
            {
                float angle=branch*2.39996f+Next(random,-.2f,.2f),level=2.7f+branch*.27f;
                float reach=Next(random,1.1f,2.5f)*(1-.035f*branch);
                var a=new Vector3(.1f,level,0);var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var b=a+direction*reach+Vector3.up*Next(random,1,1.7f);
                trunk.Branch(a,b,.075f,.015f,7);
                for(int twig=0;twig<3;twig++)
                {
                    var c=Vector3.Lerp(a,b,.55f+twig*.2f)+Quaternion.Euler(0,twig*47-47,0)*direction*.7f+Vector3.up*.4f;
                    trunk.Branch(Vector3.Lerp(a,b,.5f),c,.025f,.006f,5);
                    for(int leaf=0;leaf<42;leaf++)
                    {
                        float theta=Next(random,0,Mathf.PI*2),h=Next(random,-1,1),rad=Mathf.Sqrt(1-h*h);
                        Vector3 p=c+new Vector3(Mathf.Cos(theta)*rad*.75f,h*.7f,Mathf.Sin(theta)*rad*.75f);
                        Quaternion rot=Quaternion.Euler(Next(random,0,180),Next(random,0,360),Next(random,0,360));
                        Vector3 right=rot*Vector3.right*Next(random,.12f,.23f),up=rot*Vector3.up*Next(random,.19f,.29f);
                        leaves.Quad(p-right-up,p+right-up,p+right+up,p-right+up);
                    }
                }
            }
            return(trunk.Finish("Original branched broadleaf "+seed),leaves.Finish("Original individual leaf cards "+seed));
        }
        public static Mesh Grass()
        {
            var r=new System.Random(431);var w=new Writer();
            for(int i=0;i<18;i++)
            {
                float a=Next(r,0,6.28f),h=Next(r,.18f,.49f);var p=new Vector3(Next(r,-.22f,.22f),-.02f,Next(r,-.22f,.22f));
                var across=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.018f;
                var top=p+Vector3.up*h+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*.13f;
                w.Quad(p-across,p+across,top+across*.1f,top-across*.1f);
            }
            return w.Finish("Original fine meadow blades");
        }
    }
}
