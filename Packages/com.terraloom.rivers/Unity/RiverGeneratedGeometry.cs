using UnityEngine;
using UnityEngine.Rendering;

namespace TerraLoom.Rivers.Unity
{
    public sealed class RiverGeneratedGeometry : MonoBehaviour
    {
        [SerializeField] private RiverGeometryRole role;
        [SerializeField] private bool ownsMesh;
        public RiverGeometryRole Role => role;
        public void Initialize(RiverGeometryRole value) { role = value; ownsMesh = true; Restore(); }
        public void MarkBaked() { ownsMesh = false; }
        private void OnEnable() { Restore(); }
        public void Restore()
        {
            var renderer = GetComponent<MeshRenderer>();
            if (renderer) { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = role != RiverGeometryRole.Water; }
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
