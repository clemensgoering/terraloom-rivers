using System;
using System.Linq;
using TerraLoom.Integration;
using TerraLoom.Paths.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class BuyerBForeignBakedReopenAudit
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/BuyerBForeign-AuthorizedTruss/Player.unity");
        var host=UnityEngine.Object.FindFirstObjectByType<TerraLoomIntegration>();
        if(!host || !(host.CrossingPermissionSource is ExplicitCrossingPermissionSource source) || source.Approvals.Length!=1)
            throw new Exception("Baked scene lost explicit crossing authority.");
        var geometry=host.Paths.GeneratedRoot.GetComponentsInChildren<PathGeneratedGeometry>(true);
        var structure=geometry.Single(x=>x.Role==PathGeometryRole.BridgeStructure);
        if(geometry.Count(x=>x.Role==PathGeometryRole.BridgeRamp)!=2 || !structure.GetComponent<MeshCollider>()
            || !EditorUtility.IsPersistent(structure.GetComponent<MeshFilter>().sharedMesh))
            throw new Exception("Baked scene lost truss body, ramps, collider or persistent mesh.");
        if(!host.World.Terrain || !EditorUtility.IsPersistent(host.World.Terrain.terrainData))
            throw new Exception("Baked scene lost persistent carved TerrainData.");
        Debug.Log("BUYER_B_FOREIGN_BAKED_REOPEN_PASS authority="+source.Approvals[0].CrossingId+
            " mesh="+structure.GetComponent<MeshFilter>().sharedMesh.vertexCount);
    }
}
