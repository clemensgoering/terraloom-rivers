using System;
using System.Collections.Generic;
using NUnit.Framework;
using TerraLoom.Core.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraLoom.Core.Tests
{
    /// <summary>External Editor tests: real 33x33 TerrainData, no sample scene or downstream adapter dependency.</summary>
    public sealed class PreparedSourceContractTests
    {
        private readonly List<TerrainData> callerData = new List<TerrainData>();
        private GameObject root;
        private TerraLoomWorld world;
        private Terrain terrain;
        private TerrainCollider collider;
        private TerrainData borrowed, borrowedCollider;
        private ManualWorldAnchor[] originalAnchors;
        private WorldSourcePublication owner;
        private WorldSourcePublication.PreparedSource prepared;

        [SetUp]
        public void SetUp()
        {
            borrowed = NewData("Borrowed terrain", 0.125f);
            borrowedCollider = NewData("Distinct borrowed collider", 0.25f);
            root = Terrain.CreateTerrainGameObject(borrowed);
            root.name = "PreparedSource contract fixture";
            terrain = root.GetComponent<Terrain>();
            collider = root.GetComponent<TerrainCollider>();
            collider.terrainData = borrowedCollider;
            world = root.AddComponent<TerraLoomWorld>();
            world.Terrain = terrain;
            world.Seed = 17;
            world.UseSeedAnchors = true;
            originalAnchors = new[] { new ManualWorldAnchor { Id = "original", Position = new Vector3(2, 3, 4) } };
            world.ManualAnchors = originalAnchors;
            owner = new WorldSourcePublication(world);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                prepared?.Dispose();
                // Any simulated downstream bindings belong to this fixture, which clears them first.
                if (terrain) terrain.terrainData = borrowed;
                if (collider) collider.terrainData = borrowedCollider;
                owner?.Dispose();
            }
            finally
            {
                if (root) Object.DestroyImmediate(root);
                foreach (var data in callerData) if (data) Object.DestroyImmediate(data);
                callerData.Clear();
                prepared = null;
                owner = null;
            }
        }

        [Test]
        public void PrepareBuildsPrivateCopyWithoutLiveWritesAndFreezesAnchors()
        {
            var target = new GameObject("Moving anchor");
            target.transform.SetParent(root.transform);
            var capturedPosition = new Vector3(5, 6, 7);
            target.transform.position = capturedPosition;
            var input = new[] { new ManualWorldAnchor { Id = "next", Target = target.transform } };
            bool built = false;
            prepared = owner.Prepare(29, input, data =>
            {
                built = true;
                Assert.That(data, Is.Not.SameAs(borrowed));
                Assert.That(data.hideFlags, Is.EqualTo(HideFlags.DontSave));
                AssertOriginalLiveState();
                SetHeight(data, 0.75f);
                AssertOriginalLiveState();
            });

            input[0].Id = "caller edit";
            target.transform.position = Vector3.one;
            var exposed = prepared.ManualAnchors;
            Assert.That(exposed[0].Target == null, Is.True);
            Assert.That(exposed[0].Position, Is.EqualTo(capturedPosition));
            exposed[0].Id = "consumer edit";
            exposed[0].Position = Vector3.zero;
            Assert.That(prepared.ManualAnchors[0].Id, Is.EqualTo("next"));
            Assert.That(prepared.ManualAnchors[0].Position, Is.EqualTo(capturedPosition));
            Assert.That(prepared.World, Is.SameAs(world));
            Assert.That(prepared.Terrain, Is.SameAs(terrain));
            Assert.That(prepared.Seed, Is.EqualTo(29));
            Assert.That(built, Is.True);
            Assert.That(prepared.IsCommitted, Is.False);
            Assert.That(prepared.Data.GetHeights(0, 0, 1, 1)[0, 0], Is.EqualTo(0.75f).Within(0.0001f));
            prepared.ValidateBeforeCommit();
            AssertOriginalLiveState();
        }

        [Test]
        public void DirectCommitDisposeRestoresPreviousOwnedSourceAndExactConfiguration()
        {
            prepared = owner.Prepare(23, Anchors("accepted"), data => SetHeight(data, 0.5f));
            prepared.CommitBindings();
            prepared.SealBindings();
            Assert.That(prepared.Complete(), Is.Empty);
            var previousSource = prepared.Data;
            prepared.Dispose();
            prepared = null;
            var previousAnchors = world.ManualAnchors;
            var previousEntry = previousAnchors[0];

            prepared = owner.Prepare(31, Anchors("reversible"), data => SetHeight(data, 0.75f));
            var candidate = prepared.Data;
            prepared.CommitBindings();
            Assert.That(prepared.IsCommitted, Is.True);
            Assert.That(terrain.terrainData, Is.SameAs(candidate));
            Assert.That(collider.terrainData, Is.SameAs(candidate));
            Assert.That(owner.CurrentSource, Is.SameAs(candidate));
            Assert.That(world.Seed, Is.EqualTo(31));
            Assert.That(world.UseSeedAnchors, Is.False);
            Assert.That(world.ManualAnchors[0].Id, Is.EqualTo("reversible"));

            prepared.Dispose();
            prepared.Dispose();
            Assert.That(candidate == null, Is.True, "Unsealed candidate must be destroyed in Editor mode.");
            Assert.That(previousSource != null, Is.True, "Rollback must retain the previous owned source.");
            Assert.That(owner.CurrentSource, Is.SameAs(previousSource));
            Assert.That(terrain.terrainData, Is.SameAs(previousSource));
            Assert.That(collider.terrainData, Is.SameAs(previousSource));
            Assert.That(world.Seed, Is.EqualTo(23));
            Assert.That(world.UseSeedAnchors, Is.False);
            Assert.That(world.ManualAnchors, Is.SameAs(previousAnchors));
            Assert.That(world.ManualAnchors[0], Is.SameAs(previousEntry));

            owner.Clear();
            Assert.That(previousSource == null, Is.True);
            AssertOriginalLiveState();
        }

        [Test]
        public void ComposedCommitRejectsWrongExactDataAndColliderMismatchWithoutOwningCallerOutput()
        {
            prepared = owner.Prepare(37, Anchors("composed"), data => SetHeight(data, 0.5f));
            var candidate = prepared.Data;
            var actual = NewData("Caller rendered output", 0.625f);
            var expected = NewData("Different caller rendered output", 0.75f);
            terrain.terrainData = actual;
            collider.terrainData = actual;
            Assert.Throws<InvalidOperationException>(() => prepared.CommitComposedBindings(expected));
            Assert.That(terrain.terrainData, Is.SameAs(actual));
            Assert.That(collider.terrainData, Is.SameAs(actual));
            terrain.terrainData = expected;
            Assert.Throws<InvalidOperationException>(() => prepared.CommitComposedBindings(expected));
            Assert.That(prepared.IsCommitted, Is.False);
            Assert.That(owner.CurrentSource == null, Is.True);
            Assert.That(world.Seed, Is.EqualTo(17));
            Assert.That(world.UseSeedAnchors, Is.True);
            Assert.That(world.ManualAnchors, Is.SameAs(originalAnchors));

            prepared.Dispose();
            Assert.That(candidate == null, Is.True);
            Assert.That(actual != null && expected != null, Is.True, "Rendered outputs are caller-owned.");
            Assert.That(terrain.terrainData, Is.SameAs(expected), "Pending source disposal must not roll back caller bindings.");
            Assert.That(collider.terrainData, Is.SameAs(actual));
            // The downstream caller performs its own rollback; the source never adopts either output.
            terrain.terrainData = borrowed;
            collider.terrainData = borrowedCollider;
            AssertOriginalLiveState();
        }

        [Test]
        public void BuilderReentryIsRejectedWithoutPoisoningOwnerOrOuterPreparation()
        {
            prepared = owner.Prepare(41, Anchors("outer"), data =>
            {
                Assert.Throws<InvalidOperationException>(() => owner.Prepare(43, Anchors("nested"), nested => { }));
                Assert.Throws<InvalidOperationException>(() => owner.Clear());
                Assert.Throws<InvalidOperationException>(() => owner.Dispose());
                SetHeight(data, 0.5f);
            });
            prepared.ValidateBeforeCommit();
            AssertOriginalLiveState();
            prepared.Dispose();
            prepared = owner.Prepare(47, Anchors("retry"), data => { });
            prepared.ValidateBeforeCommit();
            AssertOriginalLiveState();
        }

        [Test]
        public void CandidateHeightMutationRejectsValidationAndCommitWithoutChangingLiveState()
        {
            prepared = owner.Prepare(53, Anchors("mutation"), data => SetHeight(data, 0.5f));
            var candidate = prepared.Data;
            // Change a far-corner sample to catch fingerprints that inspect only the first rows.
            candidate.SetHeights(32, 32, new float[,] { { 0.875f } });
            Assert.Throws<InvalidOperationException>(() => prepared.ValidateBeforeCommit());
            Assert.Throws<InvalidOperationException>(() => prepared.CommitBindings());
            Assert.That(prepared.IsCommitted, Is.False);
            AssertOriginalLiveState();
            prepared.Dispose();
            Assert.That(candidate == null, Is.True);
            AssertOriginalLiveState();
        }

        private TerrainData NewData(string name, float height)
        {
            var data = new TerrainData { name = name, heightmapResolution = 33, size = new Vector3(32, 16, 32) };
            callerData.Add(data);
            SetHeight(data, height);
            return data;
        }

        private static void SetHeight(TerrainData data, float height)
        {
            var heights = new float[33, 33];
            for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++) heights[z, x] = height;
            data.SetHeights(0, 0, heights);
        }

        private static ManualWorldAnchor[] Anchors(string id) =>
            new[] { new ManualWorldAnchor { Id = id, Position = new Vector3(8, 2, 8) } };

        private void AssertOriginalLiveState()
        {
            Assert.That(world.Terrain, Is.SameAs(terrain));
            Assert.That(terrain.terrainData, Is.SameAs(borrowed));
            Assert.That(collider.terrainData, Is.SameAs(borrowedCollider));
            Assert.That(borrowed.GetHeights(0, 0, 1, 1)[0, 0], Is.EqualTo(0.125f).Within(0.0001f));
            Assert.That(borrowedCollider.GetHeights(0, 0, 1, 1)[0, 0], Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(world.Seed, Is.EqualTo(17));
            Assert.That(world.UseSeedAnchors, Is.True);
            Assert.That(world.ManualAnchors, Is.SameAs(originalAnchors));
            Assert.That(originalAnchors[0].Id, Is.EqualTo("original"));
            Assert.That(owner.CurrentSource == null, Is.True);
        }
    }
}
