using TerraLoom.Core;
using TerraLoom.Paths;

namespace TerraLoom.Integration
{
    /// <summary>Consumer-owned authority for the current captured world. Implement on a
    /// MonoBehaviour and assign it to the Composition host. Return only explicit named
    /// grants for this snapshot; a saved recipe or previous path plan is never a right.</summary>
    public interface ICurrentCrossingPermissionSource
    {
        PathCrossingPermissions GetCurrentPermissions(PlanSnapshot snapshot);
    }
}
