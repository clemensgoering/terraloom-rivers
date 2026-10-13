using System;
using System.Linq;
using TerraLoom.Integration;
using TerraLoom.Integration.Editor;
using TerraLoom.Paths.Unity;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuyerBForeignReopenAudit
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/BuyerBForeign-AuthorizedTruss/Authored.unity");
        var composition=UnityEngine.Object.FindFirstObjectByType<TerraLoomIntegration>();
        var source=composition?.CrossingPermissionSource as ExplicitCrossingPermissionSource;
        if(!source || composition.CrossingPermissions!=null || !composition.Paths.TimberTrussBridge)
            throw new Exception("Saved scene lost its explicit external permission source or truss intent.");
        composition.CrossingPermissionSource=null; bool rejected=false; try { IntegrationEditor.GenerateOnSuppliedTerrain(composition); } catch(InvalidOperationException) { rejected=true; } if(!rejected || composition.Paths.GeneratedRoot) throw new Exception("Physical kit generated without current external grant."); composition.CrossingPermissionSource=source; var section=source.Approvals[0].Sections[0]; var original=section.CandidateId; section.CandidateId="stale-candidate"; rejected=false; try { IntegrationEditor.GenerateOnSuppliedTerrain(composition); } catch(InvalidOperationException) { rejected=true; } section.CandidateId=original; if(!rejected || composition.Paths.GeneratedRoot) throw new Exception("Changed candidate ID received a grant."); IntegrationEditor.GenerateOnSuppliedTerrain(composition);
        if(!composition.ValidateCurrent(out var reason))throw new Exception("Reopened world stale: "+reason);
        var route=composition.Paths.LastPlan.Routes.Single();
        if(route.BridgeCrossings.Count!=1 || !composition.Paths.GeneratedRoot.GetComponentsInChildren<PathGeneratedGeometry>(true)
            .Any(x=>x.Role==PathGeometryRole.BridgeStructure))throw new Exception("Reopened scene lost physical crossing.");
        source.enabled=false;
        if(composition.ValidateCurrent(out reason) || !reason.Contains("Crossing Permission Source"))
            throw new Exception("Withdrawing the scene source must invalidate current output: "+reason);
        source.enabled=true;
        if(!composition.ValidateCurrent(out reason))throw new Exception("Restored source should validate exact output: "+reason);
        Debug.Log("BUYER_B_FOREIGN_REOPEN_PASS binding="+route.BridgeCrossings.Single().CrossingId);
    }
}
