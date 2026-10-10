using System;
using UnityEngine;

namespace TerraLoom.Rivers.Unity
{
    /// <summary>
    /// Owns water output only. The caller supplies a runtime recipe, shared materials
    /// and a mandatory validator for its terrain/solid contacts. No terrain is edited.
    /// Generate/Rebuild publish only after validation; failure leaves existing output.
    /// Use on an identity world transform because the builder returns world vertices.
    /// </summary>
    [ExecuteAlways] // Editor deletion must release native meshes as well as child objects.
    [DisallowMultipleComponent]
    [AddComponentMenu("TerraLoom/Rivers/Waterfall Water Host")]
    public sealed class WaterfallWaterHost : MonoBehaviour
    {
        [Serializable]
        public sealed class Settings
        {
            public float UpstreamLength = 12, LowerLength = 17, RenderOffset = .01f;
            public int UpperSegments = 64, FallSegments = 128, LowerSegments = 128, AcrossSegments = 16;
        }

        // Serialized references survive Editor assembly reload; output is transient.
        [SerializeField, HideInInspector] private GameObject output;
        [SerializeField, HideInInspector] private Mesh[] ownedMeshes;
        [SerializeField, HideInInspector] private string lastIssue;
        [SerializeField, HideInInspector] private Vector3[] physicalAnchors;
        private bool generating;
        private WaterfallRecipe currentRecipe;

        public GameObject Output => output;
        public string LastIssue => lastIssue;
        public WaterfallRecipe CurrentRecipe => currentRecipe;

        /// <param name="validateContacts">Read-only inspection of candidate recipe/meshes.
        /// Throw on unsupported terrain, contact or depth. Do not retain, mutate or
        /// destroy candidate meshes. Terrain/collider ownership stays with the caller.</param>
        public void Generate(WaterfallRecipe recipe, Material reachMaterial, Material fallMaterial,
            Action<WaterfallRecipe, Mesh[]> validateContacts, Settings settings = null, Func<bool> cancelled = null)
        {
            if (generating) throw new InvalidOperationException("Waterfall generation is already running.");
            generating = true;
            GameObject candidate = null;
            Mesh[] meshes = null;
            try
            {
                if (!reachMaterial || !fallMaterial) throw new ArgumentException("Reach and fall shared materials are required.");
                if (validateContacts == null) throw new ArgumentNullException(nameof(validateContacts), "Supply a validator for the actual terrain/solid contacts.");
                var matrix = transform.localToWorldMatrix;
                for (int i = 0; i < 16; i++)
                    if (Mathf.Abs(matrix[i] - Matrix4x4.identity[i]) > .00001f)
                        throw new InvalidOperationException("Waterfall water host requires an identity world transform.");
                var s = settings ?? new Settings();
                meshes = WaterfallWaterGeometry.Build(recipe, s.UpstreamLength, s.LowerLength,
                    s.RenderOffset, s.UpperSegments, s.FallSegments, s.LowerSegments, s.AcrossSegments, cancelled);
                candidate = new GameObject("Generated waterfall water") { hideFlags = HideFlags.DontSave };
                candidate.SetActive(false);
                candidate.transform.SetParent(transform, false);
                for (int i = 0; i < meshes.Length; i++)
                {
                    meshes[i].hideFlags = HideFlags.DontSave;
                    var child = new GameObject(meshes[i].name);
                    child.transform.SetParent(candidate.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                    child.AddComponent<MeshRenderer>().sharedMaterial = i == 1 ? fallMaterial : reachMaterial;
                }
                validateContacts(recipe, meshes);
                if (!this || !candidate) throw new InvalidOperationException("Waterfall host/candidate was destroyed during validation.");
                if (cancelled != null && cancelled()) throw new OperationCanceledException("Waterfall candidate cancelled before publication.");

                var oldOutput = output;
                var oldMeshes = ownedMeshes;
                output = candidate; ownedMeshes = meshes; currentRecipe = recipe;
                physicalAnchors = new[] { Point(recipe.Fall.Lip), Point(recipe.Fall.Impact), Point(recipe.Pool), Point(recipe.OutletSill) };
                candidate = null; meshes = null;
                output.SetActive(true);
                Release(oldOutput, oldMeshes);
                lastIssue = null;
            }
            catch (Exception error)
            {
                lastIssue = error.Message;
                Release(candidate, meshes);
                throw;
            }
            finally { generating = false; }
        }

        /// <summary>Uses the same candidate/validation path as first generation.</summary>
        public void Rebuild(WaterfallRecipe recipe, Material reachMaterial, Material fallMaterial,
            Action<WaterfallRecipe, Mesh[]> validateContacts, Settings settings = null, Func<bool> cancelled = null)
            => Generate(recipe, reachMaterial, fallMaterial, validateContacts, settings, cancelled);

        [ContextMenu("Dispose Generated Water")]
        public void DisposeOutput()
        {
            if (generating) throw new InvalidOperationException("Cannot dispose during generation/validation.");
            Release(output, ownedMeshes);
            output = null; ownedMeshes = null; currentRecipe = null; physicalAnchors = null; lastIssue = null;
        }

        private void OnDestroy() => Release(output, ownedMeshes);

        private static void Release(GameObject root, Mesh[] meshes)
        {
            if (root) { root.SetActive(false); DestroyOwned(root); }
            if (meshes != null) foreach (var mesh in meshes) if (mesh) DestroyOwned(mesh);
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        private void OnDrawGizmosSelected()
        {
            if (!output || physicalAnchors == null || physicalAnchors.Length != 4) return;
            Gizmos.color = Color.cyan;
            foreach (var p in physicalAnchors) Gizmos.DrawWireSphere(p, .12f);
            for (int i = 0; i < 3; i++) Gizmos.DrawLine(physicalAnchors[i], physicalAnchors[i + 1]);
        }
        private static Vector3 Point(TerraLoom.Core.WorldPoint p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);
    }
}
