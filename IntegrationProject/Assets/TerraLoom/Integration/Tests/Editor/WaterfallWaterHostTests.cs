using System;
using System.Linq;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Rivers;
using TerraLoom.Rivers.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraLoom.Integration.Tests
{
    public sealed class WaterfallWaterHostTests
    {
        private GameObject a, b, contacts;
        private Material material;
        private WaterfallWaterHost first, second;
        private static WaterfallRecipe Recipe(int seed = 0)
        {
            var random = new System.Random(seed);
            double x = random.Next(-20, 21), y = random.Next(2, 8);
            return new WaterfallRecipe(new WorldPoint(x,y+3.2,0),new WorldPoint(x,y,0),
                new WorldPoint(x,y,1.7),new WorldPoint(x,y,5.7),0,1,3,4.2,.75,1.1,.5);
        }
        [SetUp] public void SetUp()
        {
            a = new GameObject("Host A"); b = new GameObject("Host B");
            first = a.AddComponent<WaterfallWaterHost>(); second = b.AddComponent<WaterfallWaterHost>();
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }
        [TearDown] public void TearDown()
        { Object.DestroyImmediate(a); Object.DestroyImmediate(b); if (contacts) Object.DestroyImmediate(contacts); Object.DestroyImmediate(material); }
        [Test] public void CandidateIsInactiveUntilValidatedAndInstancesOwnSeparateOutput()
        {
            first.Generate(Recipe(),material,material,(r,m)=>
            {
                Assert.That(first.Output,Is.Null);
                Assert.That(a.transform.childCount,Is.EqualTo(1));
                Assert.That(a.transform.GetChild(0).gameObject.activeSelf,Is.False);
                Assert.That(m.Length,Is.EqualTo(3));
            });
            second.Generate(Recipe(3),material,material,(r,m)=>{});
            var old = first.Output; var oldMeshes = old.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();
            var other = second.Output; var otherMesh = other.GetComponentInChildren<MeshFilter>().sharedMesh;
            first.Rebuild(Recipe(7),material,material,(r,m)=>{});
            Assert.That(old == null,Is.True); Assert.That(oldMeshes.All(m=>m==null),Is.True);
            Assert.That(other,Is.SameAs(second.Output)); Assert.That(otherMesh,Is.Not.Null);
            first.DisposeOutput(); first.DisposeOutput();
            Assert.That(second.Output.activeSelf,Is.True); Assert.That(material,Is.Not.Null);
            Object.DestroyImmediate(b); b = null;
            Assert.That(other == null,Is.True); Assert.That(otherMesh == null,Is.True); Assert.That(material,Is.Not.Null);
        }
        [Test] public void ValidationFailureAndLateCancelPreservePublishedOutput()
        {
            first.Generate(Recipe(),material,material,(r,m)=>{});
            var output = first.Output; var mesh = output.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.Throws<InvalidOperationException>(()=>first.Rebuild(Recipe(2),material,material,(r,m)=>throw new InvalidOperationException("Unsupported contacts")));
            bool cancel = false;
            Assert.Throws<OperationCanceledException>(()=>first.Rebuild(Recipe(2),material,material,(r,m)=>cancel=true,cancelled:()=>cancel));
            Assert.That(first.Output,Is.SameAs(output)); Assert.That(mesh,Is.Not.Null);
            Assert.That(output.activeSelf,Is.True); Assert.That(a.transform.childCount,Is.EqualTo(1));
            Assert.That(first.LastIssue,Does.Contain("cancelled"));
        }
        [Test] public void MissingValidationMaterialsTransformAndReentrancyFailExplicitly()
        {
            Assert.Throws<ArgumentNullException>(()=>first.Generate(Recipe(),material,material,null));
            Assert.Throws<ArgumentException>(()=>first.Generate(Recipe(),null,material,(r,m)=>{}));
            a.transform.position = Vector3.one;
            Assert.Throws<InvalidOperationException>(()=>first.Generate(Recipe(),material,material,(r,m)=>{}));
            a.transform.position = Vector3.zero;
            first.Generate(Recipe(),material,material,(r,m)=>
            {
                Assert.Throws<InvalidOperationException>(()=>first.Generate(r,material,material,(r2,m2)=>{}));
                Assert.Throws<InvalidOperationException>(()=>first.DisposeOutput());
            });
            Assert.That(first.Output.activeSelf,Is.True);
        }
        [TestCase(0)] [TestCase(42)] public void FreshConsumerChecksActualSuppliedCollidersForSeededRecipe(int seed)
        {
            var r = Recipe(seed); contacts = new GameObject("Supplied contacts");
            var colliders = new Collider[3]; int index = 0;
            foreach (double station in new[]{0d,r.PoolStation,r.OutletStation})
            {
                var sample = r.SampleLowerReach(station);
                var go = new GameObject("Contact bed"); go.transform.SetParent(contacts.transform);
                go.transform.position = new Vector3((float)sample.Surface.X,(float)(sample.Surface.Y-sample.CentreDepth)-.25f,(float)sample.Surface.Z);
                var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(6,.5f,.5f); colliders[index++] = box;
            }
            WaterfallHostExample.Generate(first,r,material,material,colliders);
            var old = first.Output;
            colliders[1].transform.position += Vector3.up * .02f;
            Assert.Throws<InvalidOperationException>(()=>WaterfallHostExample.Generate(first,r,material,material,colliders));
            Assert.That(first.Output,Is.SameAs(old));
            Assert.That(contacts.activeSelf,Is.True); // Neither host owns these colliders.
            first.DisposeOutput(); Assert.That(colliders.All(c=>c),Is.True);
        }
    }
}
