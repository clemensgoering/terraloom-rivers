using System;
using System.Threading;
using UnityEngine;

namespace TerraLoom.Rivers.Unity
{
    /// <summary>Discrete probes against the actual staged heightfield. These do not certify
    /// the entire continuous surface. Shared by publication and visual evidence adapters.</summary>
    public static class RiverWaterTerrainInspection
    {
        public readonly struct Result
        {
            public readonly int Vertices, Centres;
            public readonly double MinimumVertexClearance, MinimumCentreDepth;
            public readonly Vector3 WorstVertex, WorstCentre;
            public bool Acceptable => Vertices>0 && Centres>0 && MinimumVertexClearance>=0 && MinimumCentreDepth>0;
            internal Result(int nv,int nc,double mv,double mc,Vector3 vp,Vector3 cp)
            { Vertices=nv;Centres=nc;MinimumVertexClearance=mv;MinimumCentreDepth=mc;WorstVertex=vp;WorstCentre=cp; }
            public override string ToString() => FormattableString.Invariant(
                $"Water/terrain probes: vertices={Vertices}, minimum clearance={MinimumVertexClearance:R} m at {WorstVertex}; centres={Centres}, minimum depth={MinimumCentreDepth:R} m at {WorstCentre}. Discrete samples only.");
        }
        public static Result Inspect(GameObject root,TerrainData data,Vector3 origin,CancellationToken cancellation=default)
        {
            if(!root||!data)throw new ArgumentException("Water root and actual TerrainData required.");
            int nv=0,nc=0;double mv=double.PositiveInfinity,mc=double.PositiveInfinity;
            Vector3 vp=default,cp=default;var size=data.size;
            double Clearance(Vector3 p)
            {
                float x=(p.x-origin.x)/size.x,z=(p.z-origin.z)/size.z;
                if(float.IsNaN(x)||float.IsNaN(z)||float.IsNaN(p.y)||x<0||x>1||z<0||z>1)
                    throw new InvalidOperationException("Water probe leaves actual terrain bounds.");
                return p.y-origin.y-data.GetInterpolatedHeight(x,z);
            }
            foreach(var marker in root.GetComponentsInChildren<RiverGeneratedGeometry>(true))
            {
                if(marker.Role!=RiverGeometryRole.Water)continue;
                var filter=marker.GetComponent<MeshFilter>();var mesh=filter.sharedMesh;var vertices=mesh.vertices;
                foreach(var vertex in vertices)
                {
                    cancellation.ThrowIfCancellationRequested();var p=filter.transform.TransformPoint(vertex);
                    double d=Clearance(p);nv++;if(d<mv){mv=d;vp=p;}
                }
                var triangles=mesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var p=filter.transform.TransformPoint((vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3);
                    double d=Clearance(p);nc++;if(d<mc){mc=d;cp=p;}
                }
            }
            return new Result(nv,nc,mv,mc,vp,cp);
        }
    }
}
