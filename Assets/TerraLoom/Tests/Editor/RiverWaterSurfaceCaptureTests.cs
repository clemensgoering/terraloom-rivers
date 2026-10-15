using System;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Rivers.Unity;
using UnityEngine;

namespace TerraLoom.Tests
{
    public sealed class RiverWaterSurfaceCaptureTests
    {
        [Test] public void OffsetIsRemovedBeforeRotatedScaledHostTransform()
        {
            var root=new GameObject("Query transform test");var mesh=new Mesh();
            try
            {
                root.transform.position=new Vector3(7,3,9);
                root.transform.rotation=Quaternion.Euler(12,32,7);
                root.transform.localScale=new Vector3(2,3,.5f);
                var physical=new[]{new Vector3(0,2,0),new Vector3(2,2,0),new Vector3(0,2,2)};
                var offset=new Vector3(.03f,.07f,-.02f);
                mesh.vertices=new[]{physical[0]+offset,physical[1]+offset,physical[2]+offset};
                mesh.triangles=new[]{0,1,2};
                root.AddComponent<MeshFilter>().sharedMesh=mesh;
                var marker=root.AddComponent<RiverGeneratedGeometry>();marker.Initialize(RiverGeometryRole.Water,offset);
                var identity=new PlanIdentity(42,1,"capture-check","physical");
                var query=RiverWaterSurfaceCapture.Capture(root,identity);
                var point=root.transform.TransformPoint(new Vector3(.5f,2,.5f));
                Assert.That(query.TryGetSurfaceHeight(point.x,point.z,out var y),Is.True);
                Assert.That(y,Is.EqualTo(point.y).Within(.00001));
                root.transform.position+=Vector3.up*10;
                Assert.That(query.TryGetSurfaceHeight(point.x,point.z,out var retained),Is.True);
                Assert.That(retained,Is.EqualTo(y));
            }
            finally { UnityEngine.Object.DestroyImmediate(root);if(mesh)UnityEngine.Object.DestroyImmediate(mesh); }
        }
        [Test] public void OldGeometryWithoutPhysicalMetadataFailsClosed()
        {
            var root=new GameObject("Legacy water marker");
            try
            {
                root.AddComponent<RiverGeneratedGeometry>();
                Assert.Throws<InvalidOperationException>(()=>RiverWaterSurfaceCapture.Capture(root,
                    new PlanIdentity(42,1,"capture-check","legacy")));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
