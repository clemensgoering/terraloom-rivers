using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Integration.Tests
{
    public sealed class VerticalWaterfallTests
    {
        [TestCase(false,false)][TestCase(true,false)][TestCase(false,true)] public void OwnedCliffHoleContactAndCancelledRebuild(bool near,bool rotated)
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/Integration/Generated/"+(rotated?"TerraLoomRotatedVertical":near?"TerraLoomNearVertical":"TerraLoomVertical")+".unity");
                var recipe=Object.FindFirstObjectByType<VerticalWaterfallPrototype>();recipe.Generate();
                Assert.That(recipe.Fall.HorizontalRun,Is.EqualTo(near?.025:0).Within(1e-10));
                Assert.That(recipe.MaximumSeamError,Is.LessThan(.004));
                var oldTerrain=recipe.Ground;var oldData=oldTerrain.terrainData;var oldCliff=recipe.Cliff;
                AssertClosed(oldCliff.sharedMesh);
                var root=recipe.transform.GetChild(0);
                var upper=root.Find("Upper reach to exact lip").GetComponent<MeshFilter>().sharedMesh.vertices;
                var falling=root.Find("Parametric fall, zero-run safe").GetComponent<MeshFilter>().sharedMesh.vertices;
                var pool=root.Find("Impact pool and receiving reach").GetComponent<MeshFilter>().sharedMesh.vertices;
                for(int i=0;i<17;i++)
                {
                    Assert.That(Vector3.Distance(upper[upper.Length-17+i],falling[i]),Is.LessThan(1e-6));
                    Assert.That(Vector3.Distance(falling[falling.Length-17+i],pool[i]),Is.LessThan(1e-6));
                }
                Assert.That(oldData.GetHoles(rotated?246:196,rotated?196:246,1,1)[0,0],Is.False);
                Assert.That(oldData.GetHoles(rotated?245:195,rotated?195:245,1,1)[0,0],Is.True);
                Assert.Throws<OperationCanceledException>(()=>recipe.Generate(()=>true));
                Assert.That(recipe.Ground,Is.SameAs(oldTerrain));Assert.That(recipe.Cliff,Is.SameAs(oldCliff));
                Assert.That(recipe.transform.childCount,Is.EqualTo(1));
                var material=recipe.FallMaterial;recipe.FallMaterial=null;
                Assert.Throws<InvalidOperationException>(()=>recipe.Generate());recipe.FallMaterial=material;
                Assert.That(recipe.Ground,Is.SameAs(oldTerrain));
                recipe.Generate();Assert.That(oldTerrain==null,Is.True);Assert.That(oldData==null,Is.True);Assert.That(oldCliff==null,Is.True);
                Assert.That(recipe.transform.childCount,Is.EqualTo(1));
                foreach(float across in new[]{-1.5f,0,1.5f})
                {
                    var lip=recipe.Fall.Sample(0,across);var impact=recipe.Fall.Sample(1,across);
                    Assert.That(lip.Y-impact.Y,Is.EqualTo(3.2).Within(1e-10));
                }
            }
            finally
            {
                if(Array.Exists(setup,s=>s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
        [Test] public void UnmatchedContactFailsClosed()
        {
            Assert.Throws<InvalidOperationException>(()=>VerticalWaterfallPrototype.ValidateJoin(new Vector3(0,3.2f,0),new Vector3(.1f,3.2f,0)));
            Assert.Throws<InvalidOperationException>(()=>VerticalWaterfallPrototype.ValidateJoin(new Vector3(float.NaN,0,0),Vector3.zero));
        }
        private static void AssertClosed(Mesh mesh)
        {
            var vertices=mesh.vertices;var triangles=mesh.triangles;var edges=new Dictionary<string,int>();
            string Key(Vector3 p)=>Math.Round(p.x*100000)+","+Math.Round(p.y*100000)+","+Math.Round(p.z*100000);
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
                if(Vector3.Cross(b-a,c-a).sqrMagnitude<1e-14f)continue;
                var k=new[]{Key(a),Key(b),Key(c)};
                for(int j=0;j<3;j++){string x=k[j],y=k[(j+1)%3],edge=string.CompareOrdinal(x,y)<0?x+"|"+y:y+"|"+x;edges.TryGetValue(edge,out int n);edges[edge]=n+1;}
            }
            foreach(var edge in edges)Assert.That(edge.Value,Is.EqualTo(2),"Closed geometric solid edge "+edge.Key);
        }
    }
}
