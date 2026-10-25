using System;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Core.Unity;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Tests
{
    public sealed class LandscapeAssetShapeTests
    {
        [Test] public void SnapshotSeparatesBodyContactAndCrownAcrossRotatedNonuniformPlacement()
        {
            var go=new GameObject("Reviewed shape");
            try
            {
                var shape=go.AddComponent<LandscapeAssetShape>();
                shape.UseExplicitBody=true;shape.Role=LandscapeAssetRole.Root;
                shape.PhysicalBody=new Bounds(Vector3.zero,new Vector3(2,6,4));
                shape.GroundContact=new Bounds(new Vector3(0,-3,0),new Vector3(1,.1f,1));
                shape.VisualEnvelope=new Bounds(Vector3.zero,new Vector3(20,20,20));
                var matrix=Matrix4x4.TRS(new Vector3(-12,5,-9),Quaternion.Euler(0,45,0),new Vector3(2,3,.5f));
                var result=shape.Capture(matrix);
                double extent=3/Math.Sqrt(2);
                Assert.That(result.Body.MinX,Is.EqualTo(-12-extent).Within(.0001));
                Assert.That(result.Body.MaxZ,Is.EqualTo(-9+extent).Within(.0001));
                Assert.That(result.Contact.MaxX-result.Contact.MinX,Is.LessThan(result.Body.MaxX-result.Body.MinX));
                Assert.That(result.Visual.Contains(result.Body),Is.True);
                shape.PhysicalBody=new Bounds(Vector3.zero,new Vector3(10,6,10));
                Assert.That(result.Body.MaxX,Is.EqualTo(-12+extent).Within(.0001),"The snapshot must not alias the authored bounds.");
                Assert.That(go.GetComponentsInChildren<Collider>().Length,Is.Zero,"Shape metadata must not introduce gameplay collision.");
            }
            finally{Object.DestroyImmediate(go);}
        }
        [Test] public void MeasuringVisualEnvelopeDoesNotTurnCrownIntoBody()
        {
            var root=new GameObject("Tree body and crown");
            try
            {
                root.transform.position=new Vector3(-100,6,-75);root.transform.rotation=Quaternion.Euler(0,40,0);
                root.transform.localScale=new Vector3(2,1,.7f);
                var trunk=GameObject.CreatePrimitive(PrimitiveType.Cube);trunk.transform.SetParent(root.transform,false);
                trunk.transform.localPosition=new Vector3(0,2,0);trunk.transform.localScale=new Vector3(.5f,4,.5f);
                var crown=GameObject.CreatePrimitive(PrimitiveType.Cube);crown.transform.SetParent(root.transform,false);
                crown.transform.localPosition=new Vector3(0,5,0);crown.transform.localScale=new Vector3(6,3,6);
                var shape=root.AddComponent<LandscapeAssetShape>();
                shape.PhysicalBody=new Bounds(new Vector3(0,2,0),new Vector3(.5f,4,.5f));
                var body=shape.PhysicalBody;
                var visual=TerraLoom.Core.Editor.LandscapeAssetShapeEditor.MeasureVisualEnvelope(shape);
                Assert.That(visual.size.x,Is.InRange(6,6.001f));Assert.That(visual.size.z,Is.InRange(6,6.001f));
                Assert.That(shape.PhysicalBody,Is.EqualTo(body));
                shape.VisualEnvelope=visual;shape.Role=LandscapeAssetRole.Root;shape.UseExplicitBody=true;
                Assert.DoesNotThrow(()=>shape.Validate());
            }
            finally{Object.DestroyImmediate(root);}
        }

        [Test] public void UnreviewedOrInvalidShapesCannotClaimPhysicalClearance()
        {
            var go=new GameObject("Unreviewed shape");
            try
            {
                var shape=go.AddComponent<LandscapeAssetShape>();
                Assert.Throws<InvalidOperationException>(()=>shape.Capture(Matrix4x4.identity));
                shape.UseExplicitBody=true;shape.Role=LandscapeAssetRole.Root;
                Assert.That(shape.Capture(Matrix4x4.identity).Role,Is.EqualTo(LandscapeAssetRole.Root));
                Assert.Throws<InvalidOperationException>(()=>shape.TerrainTreeClearanceRadius(-1,1));
                shape.ContactFeather=float.NaN;
                Assert.Throws<InvalidOperationException>(()=>shape.Capture(Matrix4x4.identity));
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}
