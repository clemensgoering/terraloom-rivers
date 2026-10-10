using System;
using System.Linq;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Rivers;
using TerraLoom.Rivers.Unity;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Integration.Tests
{
    public sealed class WaterfallWaterGeometryTests
    {
        private static WaterfallRecipe Recipe(double level=8)=>new WaterfallRecipe(new WorldPoint(10,level+3.2,20),new WorldPoint(10,level,20),new WorldPoint(11.7,level,20),new WorldPoint(15.7,level,20),1,0,3,4.2,.75,1.1,.5);
        private static int OwnedMeshCount()=>Resources.FindObjectsOfTypeAll<Mesh>().Count(m=>m.name=="Upper reach to exact lip"||m.name=="Parametric fall, zero-run safe"||m.name=="Impact pool and receiving reach");
        [Test] public void SharedGeometryKeepsFrameContactsAndAnchorRows()
        {
            var r=Recipe();var meshes=WaterfallWaterGeometry.Build(r);
            try
            {
                Assert.That(meshes.Length,Is.EqualTo(3));var upper=meshes[0].vertices;var fall=meshes[1].vertices;var lower=meshes[2].vertices;
                for(int i=0;i<17;i++)
                {
                    Assert.That(upper[upper.Length-17+i],Is.EqualTo(fall[i]));Assert.That(fall[fall.Length-17+i],Is.EqualTo(lower[i]));
                }
                Assert.That(fall[8].y,Is.EqualTo(11.21f).Within(1e-6));
                Assert.That(lower[8].y,Is.EqualTo(8.01f).Within(1e-6));
                foreach(var anchor in new[]{r.Pool,r.OutletSill})Assert.That(lower.Any(v=>Vector3.Distance(v,new Vector3((float)anchor.X,(float)anchor.Y+.01f,(float)anchor.Z))<1e-6),Is.True);
                foreach(var mesh in meshes)Assert.That(mesh.normals.All(n=>n.sqrMagnitude>.99f),Is.True);
            }
            finally {foreach(var mesh in meshes)Object.DestroyImmediate(mesh);}
        }
        [Test] public void CancellationDestroysAlreadyCreatedMeshes()
        {
            int before=OwnedMeshCount(),checks=0;
            Assert.Throws<OperationCanceledException>(()=>WaterfallWaterGeometry.Build(Recipe(),cancelled:()=>++checks==80));
            Assert.That(checks,Is.EqualTo(80));Assert.That(OwnedMeshCount(),Is.EqualTo(before));
        }
        [Test] public void InvalidOrCollapsedGeometryFailsWithoutLeakingMeshes()
        {
            int before=OwnedMeshCount();
            Assert.Throws<ArgumentNullException>(()=>WaterfallWaterGeometry.Build(null));
            Assert.Throws<ArgumentException>(()=>WaterfallWaterGeometry.Build(Recipe(),lowerLength:5));
            Assert.Throws<ArgumentException>(()=>WaterfallWaterGeometry.Build(Recipe(),renderOffset:float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(()=>WaterfallWaterGeometry.Build(Recipe(),acrossSegments:65));
            Assert.Throws<InvalidOperationException>(()=>WaterfallWaterGeometry.Build(Recipe(1e8)));
            Assert.That(OwnedMeshCount(),Is.EqualTo(before));
        }
    }
}
