using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>Bounded measured sample evidence, not a general terrain writer. Heights
    /// are original native grid vertices; each diagonal is checked against its collider.</summary>
    [Serializable] public sealed class WaterfallTerrainPatch
    {
        public int schemaVersion=1,columns=225,rows=225;
        public string recipeRevision="anchored-waterfall-v1-r2";
        public string encoding="base64 little-endian uint16 row-major, world X fastest, world Z increasing";
        public string heightRule="worldY=uint16/65535*heightScale; localY=worldY-originWorld.y";
        public string diagonalRule="base64 bitset row-major cells X fastest; bit0=worldSW-NE, bit1=worldNW-SE; bit index least significant first";
        public string hashRule="SHA256 raw height bytes followed by raw diagonal bitset bytes";
        public double[] originWorld={80,12.4,89},worldMinXZ={62.5,67.5},worldMaxXZ={97.5,102.5};
        public double[] localMinXZ={-17.5,-13.5},localMaxXZ={17.5,21.5};
        public double yawDegrees=180,spacing=.15625,heightScale=70;
        public string heightsBase64,diagonalsBase64,sha256;
        public float maximumColliderCentreError;
        public static WaterfallTerrainPatch Measure(Terrain terrain)
        {
            if(terrain.transform.position!=Vector3.zero||terrain.terrainData.heightmapResolution!=1025||terrain.terrainData.size!=new Vector3(160,70,160))
                throw new InvalidOperationException("Comparison patch requires the explicit native recipe grid.");
            var patch=new WaterfallTerrainPatch();var heights=terrain.terrainData.GetHeights(400,432,225,225);
            var bytes=new byte[225*225*2];var diagonals=new byte[(224*224+7)/8];
            for(int z=0;z<225;z++)for(int x=0;x<225;x++)
            {
                ushort code=(ushort)Mathf.RoundToInt(heights[z,x]*65535);int offset=(z*225+x)*2;
                bytes[offset]=(byte)code;bytes[offset+1]=(byte)(code>>8);
            }
            Physics.SyncTransforms();var collider=terrain.GetComponent<TerrainCollider>();
            if(!collider)throw new InvalidOperationException("Measured comparison requires real TerrainCollider.");
            for(int z=0;z<224;z++)for(int x=0;x<224;x++)
            {
                var ray=new Ray(new Vector3(62.5f+(x+.5f)*.15625f,80,67.5f+(z+.5f)*.15625f),Vector3.down);
                if(!collider.Raycast(ray,out var hit,100))throw new InvalidOperationException("Patch collider probe missed.");
                float swne=(heights[z,x]+heights[z+1,x+1])*35;
                float nwse=(heights[z+1,x]+heights[z,x+1])*35;
                float a=Mathf.Abs(hit.point.y-swne),b=Mathf.Abs(hit.point.y-nwse);
                if(b<a){int i=z*224+x;diagonals[i/8]|=(byte)(1<<(i%8));}
                patch.maximumColliderCentreError=Mathf.Max(patch.maximumColliderCentreError,Mathf.Min(a,b));
            }
            if(patch.maximumColliderCentreError>.003f)throw new InvalidOperationException("Patch triangles do not reproduce native collider within3mm.");
            patch.heightsBase64=Convert.ToBase64String(bytes);patch.diagonalsBase64=Convert.ToBase64String(diagonals);
            var combined=new byte[bytes.Length+diagonals.Length];Buffer.BlockCopy(bytes,0,combined,0,bytes.Length);Buffer.BlockCopy(diagonals,0,combined,bytes.Length,diagonals.Length);
            using(var hash=SHA256.Create())patch.sha256=BitConverter.ToString(hash.ComputeHash(combined)).Replace("-","").ToLowerInvariant();
            return patch;
        }
        public void Save(string path)=>File.WriteAllText(path,JsonUtility.ToJson(this,true));
    }
}
