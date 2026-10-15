using UnityEngine;
using UnityEngine.Rendering;

namespace TerraLoom.Rivers.Unity
{
    public sealed class RiverGeneratedGeometry : MonoBehaviour
    {
        [SerializeField] private RiverGeometryRole role;
        [SerializeField] private bool ownsMesh;
        [SerializeField] private bool hasPhysicalSurfaceMetadata;
        [SerializeField] private Vector3 physicalSurfaceRenderOffsetLocal;
        public bool HasPhysicalSurfaceMetadata => hasPhysicalSurfaceMetadata;
        public Vector3 PhysicalSurfaceRenderOffsetLocal => physicalSurfaceRenderOffsetLocal;
        public RiverGeometryRole Role => role;
        public void Initialize(RiverGeometryRole value) { Initialize(value, Vector3.zero); }
        public void Initialize(RiverGeometryRole value, Vector3 renderOffsetLocal)
        {
            if (float.IsNaN(renderOffsetLocal.x) || float.IsInfinity(renderOffsetLocal.x)
                || float.IsNaN(renderOffsetLocal.y) || float.IsInfinity(renderOffsetLocal.y)
                || float.IsNaN(renderOffsetLocal.z) || float.IsInfinity(renderOffsetLocal.z))
                throw new System.ArgumentException("Physical surface render offset must be finite.");
            role = value; ownsMesh = true; physicalSurfaceRenderOffsetLocal = renderOffsetLocal;
            hasPhysicalSurfaceMetadata = true; Restore();
        }
        public void MarkBaked() { ownsMesh = false; }
        private void OnEnable() { Restore(); }
        public void Restore()
        {
            var renderer = GetComponent<MeshRenderer>();
            if (renderer) { renderer.shadowCastingMode = role == RiverGeometryRole.FallRock ? ShadowCastingMode.On : ShadowCastingMode.Off; renderer.receiveShadows = role != RiverGeometryRole.Water; }
        }
        public void DisposeOwnedMesh()
        {
            if (!ownsMesh) return;
            var filter = GetComponent<MeshFilter>(); var mesh = filter ? filter.sharedMesh : null;
            if (!mesh) return;
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(mesh)) return;
#endif
            ownsMesh = false; filter.sharedMesh = null;
            var collider = GetComponent<MeshCollider>(); if (collider) collider.sharedMesh = null;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }
        private void OnDestroy() { DisposeOwnedMesh(); }
    }
}
