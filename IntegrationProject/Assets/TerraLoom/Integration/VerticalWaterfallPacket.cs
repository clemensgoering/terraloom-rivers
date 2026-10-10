using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>Exports measured final geometry, never a second terrain recipe. Binary
    /// payloads use little endian values and are individually SHA256 authenticated.</summary>
    [Serializable] public sealed class VerticalWaterfallPacket
    {
        public int schemaVersion=1;
        public string revision="vertical-contact-v2",caseName;
        public string coordinates="Unity world metres, Y up; mesh positions/normals already world space; identity object transforms";
        public string limitations="Fresh owned terrain only; upper slope zero, bed depth 1.1m, pool width max 4.2m; no r2 outlet sill; cliff UV absent; no material or lighting equivalence claimed";
        public string quarterTurnRule="canonical (x,y,z) -> (z,y,64-x); grid/mask already permuted, do not rotate again";
        public Vector3 lip,impact,forward,across,renderOffset=new Vector3(0,.01f,0);
        public double width,horizontalRun,arcLength;
        public Vector3 terrainOrigin,terrainSize;
        public int heightResolution,holeResolution;
        public string terrainRule="heights: uint16 normalized/65535*terrainSize.y+terrainOrigin.y; row-major X fastest, increasing world Z. holes: byte per cell, 0=hole, 1=solid. diagonals: byte per cell, 0=SW-NE, 1=NW-SE, 255=hole; ties choose 0";
        public Payload heights,holes,diagonals;
        public float maximumColliderCentreError;
        public MeshPayload[] meshes;
        public Observer[] cameras;
        public ControllerProbe[] controllerProbes;
        public ReceivingRecipe receivingRecipe;
        public BedProbe[] bedProbes;
        [Serializable] public sealed class ReceivingRecipe
        {
            public Vector3 pool,outletSill;
            public double poolStation,outletStation,poolWidth,channelDepth,poolDepth,sillDepth,upstreamSlope,downstreamSlope,outletTransitionLength;
            public string cliffUvRule="Per-face dominant world normal axis projection: Y dominant uses XZ, Z uses XY, X uses ZY; UV=world metres/2; tangents recalculated";
        }
        [Serializable] public sealed class BedProbe
        {
            public string name;public double station,lateral;public Vector3 physicalWater;
            public float renderedWaterY,intendedBedY,actualBedY,physicalWetDepth,renderedWetDepth;
        }
        [Serializable] public sealed class Payload { public string file,sha256,encoding; public int bytes; }
        [Serializable] public sealed class MeshPayload
        {
            public string name; public int vertexCount,indexCount,uvCount; public Bounds bounds;
            public Payload vertices,normals,indices,uv;
            public int[] startContact,endContact;
        }
        [Serializable] public sealed class Observer
        {
            public string name; public Vector3 eye,target,ground; public Quaternion rotation;
            public float fov; public int width=1600,height=1000;
        }
        [Serializable] public sealed class ControllerProbe
        {
            public float lateral; public Vector3 start,end,forward;
            public bool grounded; public float height=1.8f,radius=.16f,stepOffset=.1f,slopeLimit=60,speed=.8f,gravity=9.81f,fixedDeltaTime;
            public Vector3 centre=new Vector3(0,.9f,0);
            public int steps=140;
            public string rule="Disable, warp, enable, SyncTransforms; velocity starts 0; each step if grounded and velocity<0 set -2; subtract gravity*dt then Move((up*velocity+forward*speed)*dt). No water collider/swimming.";
        }
        private static Vector3 V(TerraLoom.Core.WorldPoint p)=>new Vector3((float)p.X,(float)p.Y,(float)p.Z);
        public static VerticalWaterfallPacket Save(VerticalWaterfallPrototype recipe,string directory,Observer[] observers,ControllerProbe[] probes)
        {
            Directory.CreateDirectory(directory);
            // Remove only the completion marker when refreshing an existing packet.
            // Failed exports must not leave an older manifest pointing at new bytes.
            File.Delete(Path.Combine(directory,"manifest.json"));
            Payload Write(string name,string encoding,Action<BinaryWriter> emit)
            {
                string file=name+".bin"; byte[] bytes;
                using(var stream=new MemoryStream()) { using(var writer=new BinaryWriter(stream))emit(writer); bytes=stream.ToArray(); }
                File.WriteAllBytes(Path.Combine(directory,file),bytes);
                using(var hash=SHA256.Create())return new Payload {file=file,bytes=bytes.Length,encoding=encoding,sha256=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()};
            }
            var data=recipe.Ground.terrainData;int n=data.heightmapResolution,h=data.holesResolution;
            if(recipe.Ground.transform.rotation!=Quaternion.identity||recipe.Ground.transform.lossyScale!=Vector3.one)
                throw new InvalidOperationException("Terrain export requires an unrotated unit-scale native grid.");
            var packet=new VerticalWaterfallPacket {caseName=recipe.RotateQuarterTurn?"quarter-turn":recipe.NearVertical?"near-vertical":"vertical",lip=V(recipe.Fall.Lip),impact=V(recipe.Fall.Impact),forward=V(recipe.Fall.Forward),across=V(recipe.Fall.Across),width=recipe.Fall.Width,horizontalRun=recipe.Fall.HorizontalRun,arcLength=recipe.Fall.ArcLength,terrainOrigin=recipe.Ground.transform.position,terrainSize=data.size,heightResolution=n,holeResolution=h,cameras=observers,controllerProbes=probes};
            if(recipe.CombinedRecipe!=null)
            {
                var r=recipe.CombinedRecipe;packet.revision="combined-waterfall-v2-r1";packet.caseName="combined-zero-run";
                packet.limitations="Fresh owned terrain only; shared WaterfallRecipe upper/fall/pool/sill/outflow. Original landscape layers and rock textures; UV projection documented; no general terrain writer/site solver or final art acceptance.";
                packet.receivingRecipe=new ReceivingRecipe {pool=V(r.Pool),outletSill=V(r.OutletSill),poolStation=r.PoolStation,outletStation=r.OutletStation,poolWidth=r.PoolWidth,channelDepth=r.ChannelDepth,poolDepth=r.PoolDepth,sillDepth=r.OutletSillDepth,upstreamSlope=r.UpstreamSlope,downstreamSlope=r.DownstreamSlope,outletTransitionLength=r.OutletTransitionLength};
                packet.bedProbes=recipe.MeasureBed();
            }
            var heights=data.GetHeights(0,0,n,n);var holes=data.GetHoles(0,0,h,h);
            packet.heights=Write("heights","uint16 LE",w=>{for(int z=0;z<n;z++)for(int x=0;x<n;x++)w.Write((ushort)Mathf.RoundToInt(heights[z,x]*65535));});
            packet.holes=Write("holes","uint8",w=>{for(int z=0;z<h;z++)for(int x=0;x<h;x++)w.Write((byte)(holes[z,x]?1:0));});
            Physics.SyncTransforms();var collider=recipe.Ground.GetComponent<TerrainCollider>();
            packet.diagonals=Write("diagonals","uint8",w=>
            {
                for(int z=0;z<h;z++)for(int x=0;x<h;x++)
                {
                    if(!holes[z,x]){w.Write((byte)255);continue;}
                    var origin=packet.terrainOrigin;var size=packet.terrainSize;
                    var ray=new Ray(origin+new Vector3((x+.5f)*size.x/h,size.y+1,(z+.5f)*size.z/h),Vector3.down);
                    if(!collider.Raycast(ray,out var hit,size.y+2))throw new InvalidOperationException("Solid terrain cell missed during export.");
                    float a=(heights[z,x]+heights[z+1,x+1])*size.y*.5f+origin.y;
                    float b=(heights[z+1,x]+heights[z,x+1])*size.y*.5f+origin.y;
                    float ea=Mathf.Abs(hit.point.y-a),eb=Mathf.Abs(hit.point.y-b);
                    packet.maximumColliderCentreError=Mathf.Max(packet.maximumColliderCentreError,Mathf.Min(ea,eb));w.Write((byte)(eb<ea?1:0));
                }
            });
            if(packet.maximumColliderCentreError>.003f)throw new InvalidOperationException("Export cannot reproduce TerrainCollider triangles.");
            var filters=recipe.Ground.transform.parent.GetComponentsInChildren<MeshFilter>();
            packet.meshes=filters.Select((filter,index)=>
            {
                var mesh=filter.sharedMesh;var vs=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;var indices=mesh.triangles;
                var result=new MeshPayload {name=mesh.name,vertexCount=vs.Length,indexCount=indices.Length,uvCount=uv.Length,bounds=mesh.bounds};
                result.vertices=Write("mesh"+index+"-vertices","float32 LE xyz",w=>{foreach(var p in vs){var q=filter.transform.TransformPoint(p);w.Write(q.x);w.Write(q.y);w.Write(q.z);}});
                result.normals=Write("mesh"+index+"-normals","float32 LE xyz",w=>{foreach(var p in normals){var q=filter.transform.TransformDirection(p).normalized;w.Write(q.x);w.Write(q.y);w.Write(q.z);}});
                result.indices=Write("mesh"+index+"-indices","int32 LE; Unity triangle winding",w=>{foreach(int i in indices)w.Write(i);});
                result.uv=Write("mesh"+index+"-uv","float32 LE xy; empty means absent",w=>{foreach(var p in uv){w.Write(p.x);w.Write(p.y);}});
                if(mesh.name!="Closed cliff replacement solid") {result.startContact=Enumerable.Range(0,17).ToArray();result.endContact=Enumerable.Range(vs.Length-17,17).ToArray();}
                return result;
            }).ToArray();
            // Manifest last: consumers never treat partial payloads as a completed packet.
            File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonUtility.ToJson(packet,true));return packet;
        }
    }
}
