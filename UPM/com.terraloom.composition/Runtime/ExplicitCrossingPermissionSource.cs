using System;
using System.Linq;
using TerraLoom.Core;
using TerraLoom.Paths;
using UnityEngine;

namespace TerraLoom.Integration
{
    /// <summary>A manually reviewed, scene-owned permission list. Exact candidate IDs and XZ
    /// rectangles are authored here; the component binds them to each freshly captured plan.
    /// Changed seeds/corridors invalidate IDs instead of silently granting a new location.</summary>
    public sealed class ExplicitCrossingPermissionSource : MonoBehaviour, ICurrentCrossingPermissionSource
    {
        [Tooltip("Only explicitly listed crossings are authorized. Leave empty for no grants.")]
        public CrossingApproval[] Approvals = Array.Empty<CrossingApproval>();

        public PathCrossingPermissions GetCurrentPermissions(PlanSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (Approvals == null || Approvals.Length > 64 || Approvals.Any(a => a == null))
                throw new InvalidOperationException("Crossing approvals must be a non-null list of at most 64 entries.");
            var grants = Approvals.Select(approval =>
            {
                if (string.IsNullOrWhiteSpace(approval.CrossingId) || approval.Sections == null
                    || approval.Sections.Length == 0 || approval.Sections.Length > 64 || approval.Sections.Any(s => s == null))
                    throw new InvalidOperationException("Every approved crossing needs a name and 1–64 reviewed water sections.");
                var sections = approval.Sections.Select(section =>
                {
                    var candidate = snapshot.Crossings.FirstOrDefault(c => c.Id == section.CandidateId);
                    if (candidate == null || candidate.WaterId != section.WaterId)
                        throw new InvalidOperationException("Reviewed water/candidate ID is missing or changed: " + section.CandidateId);
                    var area = new WorldBounds(section.MinXZ.x, section.MinXZ.y, section.MaxXZ.x, section.MaxXZ.y);
                    if (!candidate.Bounds.Contains(area))
                        throw new InvalidOperationException("Reviewed crossing area exceeds its named candidate: " + section.CandidateId);
                    return new CrossingSectionPermission(section.WaterId, section.CandidateId, area);
                });
                return new CollectiveCrossingAuthorization(approval.CrossingId, snapshot.Identity, sections);
            });
            return new PathCrossingPermissions(grants);
        }
    }

    [Serializable]
    public sealed class CrossingApproval
    {
        [Tooltip("Exact planner crossing ID, for example bridge:<candidate ID>:0.")]
        public string CrossingId;
        [Tooltip("Every affected water reach needs its own candidate and approved rectangle.")]
        public CrossingSectionApproval[] Sections = Array.Empty<CrossingSectionApproval>();
    }

    [Serializable]
    public sealed class CrossingSectionApproval
    {
        public string WaterId;
        public string CandidateId;
        [Tooltip("Lower X/Z corner of the explicitly approved area, in world metres.")]
        public Vector2 MinXZ;
        [Tooltip("Upper X/Z corner of the explicitly approved area, in world metres.")]
        public Vector2 MaxXZ;
    }
}
