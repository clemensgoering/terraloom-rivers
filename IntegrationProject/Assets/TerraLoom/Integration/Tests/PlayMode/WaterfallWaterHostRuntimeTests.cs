using System;
using System.Collections;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Rivers;
using TerraLoom.Rivers.Unity;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TerraLoom.Integration.Tests
{
    public sealed class WaterfallWaterHostRuntimeTests
    {
        [UnityTest] public IEnumerator WaterfallHostRebuildAndCancelRetainOtherInstanceThroughDeferredDestruction()
        {
            var a = new GameObject("Runtime host A"); var b = new GameObject("Runtime host B");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                var first = a.AddComponent<WaterfallWaterHost>(); var second = b.AddComponent<WaterfallWaterHost>();
                var r = new WaterfallRecipe(new WorldPoint(0,4,0),new WorldPoint(0,.8,0),
                    new WorldPoint(0,.8,1.7),new WorldPoint(0,.8,5.7),0,1,3,4.2,.75,1.1,.5);
                first.Generate(r,material,material,(recipe,meshes)=>{});
                second.Generate(r,material,material,(recipe,meshes)=>{});
                var old = first.Output; var oldMesh = old.GetComponentInChildren<MeshFilter>().sharedMesh;
                var other = second.Output; var otherMesh = other.GetComponentInChildren<MeshFilter>().sharedMesh;
                first.Rebuild(r,material,material,(recipe,meshes)=>{});
                Assert.That(old.activeSelf,Is.False); // No duplicate visible water during deferred Destroy.
                var published = first.Output;
                bool cancel = false;
                Assert.Throws<OperationCanceledException>(()=>first.Rebuild(r,material,material,(recipe,meshes)=>cancel=true,cancelled:()=>cancel));
                Assert.That(first.Output,Is.SameAs(published)); Assert.That(published.activeSelf,Is.True);
                yield return null;
                Assert.That(old == null,Is.True); Assert.That(oldMesh == null,Is.True);
                Assert.That(a.transform.childCount,Is.EqualTo(1));
                Object.Destroy(a); yield return null;
                Assert.That(published == null,Is.True);
                Assert.That(second.Output,Is.SameAs(other)); Assert.That(otherMesh,Is.Not.Null);
                second.DisposeOutput(); second.DisposeOutput(); yield return null;
                Assert.That(other == null,Is.True); Assert.That(otherMesh == null,Is.True); Assert.That(material,Is.Not.Null);
            }
            finally { if(a) Object.Destroy(a); if(b) Object.Destroy(b); Object.Destroy(material); }
            yield return null;
        }
    }
}
