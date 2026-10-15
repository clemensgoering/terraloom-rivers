using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TerraLoom.Core;
using UnityEngine;

namespace TerraLoom.Rivers.Unity
{
    /// <summary>Versioned saved publication evidence, not a substitute for a fresh plan.
    /// Rehydration accepts only the exact saved JSON, physical water and terrain content.</summary>
    [Serializable]
    public sealed class RiverWaterQueryPersistence
    {
        [SerializeField] private int format,seed;
        [SerializeField] private long revision;
        [SerializeField] private string algorithm,profile,fingerprint,contentHash;
        [SerializeField] private Terrain terrain;
        [SerializeField] private TerrainData terrainData;
        [SerializeField] private bool hadCollider;

        public static RiverWaterQueryPersistence Capture(GameObject root, Terrain target,
            TerrainData finalData, PlanIdentity identity, string planJson)
        {
            if(!identity.IsValid)throw new ArgumentException("Water query publication identity is invalid.");
            return new RiverWaterQueryPersistence {
                format=1,seed=identity.Seed,revision=identity.Revision,algorithm=identity.AlgorithmVersion,
                profile=identity.ProfileVersion,fingerprint=identity.InputFingerprint,
                terrain=target,terrainData=finalData,hadCollider=target&&target.GetComponent<TerrainCollider>(),
                contentHash=Hash(root,target,finalData,planJson,identity)
            };
        }

        public RiverWaterSurfaceSnapshot Restore(GameObject root,Terrain currentTerrain,string planJson)
        {
            if(format!=1||!terrain||!terrainData||terrain!=currentTerrain||terrain.terrainData!=terrainData)
                throw new InvalidOperationException("Saved water query terrain binding/version changed; regenerate the world.");
            var collider=terrain.GetComponent<TerrainCollider>();
            if((bool)collider!=hadCollider||(collider&&collider.terrainData!=terrainData))
                throw new InvalidOperationException("Saved water query collider binding changed; regenerate the world.");
            var identity=new PlanIdentity(seed,revision,algorithm,profile,fingerprint);
            if(Hash(root,terrain,terrainData,planJson,identity)!=contentHash)
                throw new InvalidOperationException("Saved water query plan, geometry or terrain content changed; regenerate the world.");
            return RiverWaterSurfaceCapture.Capture(root,identity);
        }

        public void RebindBakedTerrain(GameObject root,Terrain currentTerrain,string planJson)
        {
            var identity=new PlanIdentity(seed,revision,algorithm,profile,fingerprint);
            if(!currentTerrain||Hash(root,currentTerrain,currentTerrain.terrainData,planJson,identity)!=contentHash)
                throw new InvalidOperationException("Bake changed physical water/terrain content; saved query was not rebound.");
            terrain=currentTerrain;terrainData=currentTerrain.terrainData;
        }

        private static string Hash(GameObject root,Terrain target,TerrainData data,string json,PlanIdentity identity)
        {
            if(!root||!target||!data||string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("Saved water query publication is incomplete.");
            using(var sha=SHA256.Create())
            using(var stream=new CryptoStream(Stream.Null,sha,CryptoStreamMode.Write))
            using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                writer.Write(1);writer.Write(json);
                writer.Write(identity.Seed);writer.Write(identity.Revision);writer.Write(identity.AlgorithmVersion);
                writer.Write(identity.ProfileVersion);writer.Write(identity.InputFingerprint);
                var markers=root.GetComponentsInChildren<RiverGeneratedGeometry>(true);
                int count=0;foreach(var marker in markers)if(marker.Role==RiverGeometryRole.Water)count++;
                writer.Write(count);
                foreach(var marker in markers)
                {
                    if(marker.Role!=RiverGeometryRole.Water)continue;
                    writer.Write(marker.HasPhysicalSurfaceMetadata);
                    Write(writer,marker.PhysicalSurfaceRenderOffsetLocal);
                    var matrix=marker.transform.localToWorldMatrix;
                    for(int i=0;i<16;i++)writer.Write(matrix[i]);
                    var filter=marker.GetComponent<MeshFilter>();var mesh=filter?filter.sharedMesh:null;
                    if(!mesh||!mesh.isReadable)throw new InvalidOperationException("Saved water query mesh must be readable.");
                    var vertices=mesh.vertices;var indices=mesh.triangles;
                    writer.Write(vertices.Length);foreach(var point in vertices)Write(writer,point);
                    writer.Write(indices.Length);foreach(int index in indices)writer.Write(index);
                }
                var transform=target.transform.localToWorldMatrix;
                for(int i=0;i<16;i++)writer.Write(transform[i]);
                Write(writer,data.size);
                int resolution=data.heightmapResolution;writer.Write(resolution);
                var heights=data.GetHeights(0,0,resolution,resolution);
                foreach(float height in heights)writer.Write(height);
                int holesResolution=data.holesResolution;writer.Write(holesResolution);
                var holes=data.GetHoles(0,0,holesResolution,holesResolution);
                foreach(bool hole in holes)writer.Write(hole);
                writer.Flush();stream.FlushFinalBlock();
                return BitConverter.ToString(sha.Hash).Replace("-","");
            }
        }
        private static void Write(BinaryWriter writer,Vector3 p)
        {writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
    }
}
