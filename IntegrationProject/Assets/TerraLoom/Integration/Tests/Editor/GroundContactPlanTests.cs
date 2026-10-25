using System;
using System.Linq;
using System.Globalization;
using NUnit.Framework;
using TerraLoom.Core;
using TerraLoom.Core.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Tests
{
    public sealed class GroundContactPlanTests
    {
        private static AssetShapeSnapshot Shape(LandscapeAssetRole role)=>new AssetShapeSnapshot(role,
            new WorldBounds(-1,-1,1,1),new WorldBounds(-2,-2,2,2),new WorldBounds(-20,-20,20,20),2);
        [Test] public void ContactMasksUseStableMaxCompositionAcrossCellsAndInputOrder()
        {
            var a=new GroundContact("a","neutral-region",Shape(LandscapeAssetRole.Root));
            var b=new GroundContact("b","neutral-region",Shape(LandscapeAssetRole.Timber));
            var input=new[]{a,b};
            var plan=new GroundContactPlan("source",long.MaxValue,42,input,1);
            var reversed=new GroundContactPlan("source",long.MaxValue,42,input.Reverse(),1);
            Assert.That(reversed.Fingerprint,Is.EqualTo(plan.Fingerprint));
            Assert.That(plan.Sample(3,0).GroundWeight,Is.EqualTo(.5).Within(1e-12),"Overlap must not multiply or add repeated thinning.");
            Assert.That(plan.Sample(3,0).DominantContactId,Is.EqualTo("a"));
            Assert.That(plan.Sample(3,0,LandscapeAssetRole.Rock).GroundWeight,Is.Zero);
            Assert.That(plan.Sample(-1e-8,0).BodyCoverage,Is.EqualTo(plan.Sample(1e-8,0).BodyCoverage));
            Assert.That(plan.Sample(-3,0).GroundWeight,Is.EqualTo(plan.Sample(3,0).GroundWeight));
            input[0]=b;
            Assert.That(plan.Sample(3,0).DominantContactId,Is.EqualTo("a"),"The plan may not alias the caller's array.");
            Assert.That(new GroundContactPlan("source",long.MaxValue-1,42,new[]{a,b},1).Fingerprint,Is.Not.EqualTo(plan.Fingerprint));
            var culture=CultureInfo.CurrentCulture;
            try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");Assert.That(new GroundContactPlan("source",long.MaxValue,42,new[]{a,b},1).Fingerprint,Is.EqualTo(plan.Fingerprint));}
            finally{CultureInfo.CurrentCulture=culture;}
        }
        [Test] public void InvalidAndOversizedContactInputsFailBeforeIndexAllocation()
        {
            var a=new GroundContact("a","neutral-region",Shape(LandscapeAssetRole.Root));
            Assert.Throws<ArgumentException>(()=>new GroundContactPlan("source",0,42,new[]{a,a}));
            var large=new WorldBounds(-10000,-10000,10000,10000);
            var big=new GroundContact("big","neutral-region",new AssetShapeSnapshot(LandscapeAssetRole.Rock,large,large,large,0));
            Assert.Throws<ArgumentException>(()=>new GroundContactPlan("source",0,42,new[]{big},.1,32));
            var plan=new GroundContactPlan("source",0,42,new[]{a});
            Assert.Throws<ArgumentException>(()=>plan.Sample(double.NaN,0));
        }
        [Test] public void PreparedContactsKeepWorldPoseRollbackAndReopenWithoutReplanning()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            string folder="Assets/TerraLoomContacts-"+Guid.NewGuid().ToString("N");
            TerrainData candidate=null;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                AssetDatabase.CreateFolder("Assets",folder.Substring("Assets/".Length));
                var prototype=GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(prototype.GetComponent<Collider>());
                var body=prototype.AddComponent<LandscapeAssetShape>();body.UseExplicitBody=true;body.Role=LandscapeAssetRole.Rock;
                var prefab=PrefabUtility.SaveAsPrefabAsset(prototype,folder+"/Rock.prefab");Object.DestroyImmediate(prototype);
                var data=new TerrainData{heightmapResolution=33,size=new Vector3(32,8,32)};
                AssetDatabase.CreateAsset(data,folder+"/Terrain.asset");
                var host=new GameObject("Translated scaled contact owner");host.transform.position=new Vector3(-32,0,-24);
                host.transform.localScale=new Vector3(2,1,.5f);
                var world=host.AddComponent<TerraLoomWorld>();world.WorldId="contacts-test";world.Seed=42;
                world.Areas=new[]{new LandscapeArea{Id="neutral-region",Center=new Vector2(-16,-8),Size=new Vector2(32,32)}};
                world.Terrain=Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();world.Terrain.transform.position=host.transform.position;
                var style=ScriptableObject.CreateInstance<RegionalStyleProfile>();style.RegionId="neutral-region";style.GroundOffset=.5f;
                style.MinScale=style.MaxScale=1;style.Trees=new[]{prefab};style.Rocks=new[]{prefab};style.Grass=new[]{prefab};
                AssetDatabase.CreateAsset(style,folder+"/Style.asset");
                var d=host.AddComponent<RegionDecoration>();d.World=world;d.Count=8;d.Margin=2;d.Profiles=new[]{style};
                d.Generate();
                var before=d.CaptureGroundContactPlan(out var unknown);Assert.That(unknown,Is.Zero);Assert.That(before.Contacts.Count,Is.EqualTo(8));
                var root=d.GeneratedRoot;
                var positions=root.GetComponentsInChildren<RegionalDecorationInstance>().Select(x=>x.transform.position).ToArray();
                candidate=Object.Instantiate(data);
                using(var pending=d.PrepareCandidate(candidate,42,world.Areas,d.Profiles,8,d.ProfileSelection,Array.Empty<Rect>()))
                {
                    pending.Commit();
                    Assert.That(d.GeneratedRoot.GetComponentsInChildren<RegionalDecorationInstance>().Select(x=>x.transform.position).ToArray(),Is.EqualTo(positions));
                    var after=d.CaptureGroundContactPlan(out unknown);
                    if(after.Fingerprint!=before.Fingerprint)
                    {
                        TestContext.WriteLine("Context "+before.SourceId+" / "+after.SourceId+" rev "+before.Revision+"/"+after.Revision+" seed "+before.Seed+"/"+after.Seed);
                        for(int j=0;j<before.Contacts.Count;j++)
                        {
                            var a=before.Contacts[j];var b=after.Contacts[j];
                            TestContext.WriteLine(a.Id+" / "+b.Id+" body "+a.Shape.Body.MinX.ToString("R")+","+a.Shape.Body.MinZ.ToString("R")+" / "+b.Shape.Body.MinX.ToString("R")+","+b.Shape.Body.MinZ.ToString("R"));
                        }
                    }
                    Assert.That(after.Fingerprint,Is.EqualTo(before.Fingerprint));
                    // Dispose without Complete rolls both placement and contact publication back.
                }
                Assert.That(d.GeneratedRoot,Is.SameAs(root));
                Assert.That(d.CaptureGroundContactPlan(out unknown).Fingerprint,Is.EqualTo(before.Fingerprint));
                using(var pending=d.PrepareCandidate(candidate,42,world.Areas,d.Profiles,8,d.ProfileSelection,Array.Empty<Rect>()))
                {pending.Commit();pending.Complete();}
                Assert.That(root==null,Is.True);
                Assert.That(d.CaptureGroundContactPlan(out unknown).Fingerprint,Is.EqualTo(before.Fingerprint));
                Object.DestroyImmediate(candidate);candidate=null;
                AssetDatabase.SaveAssets();Assert.That(EditorSceneManager.SaveScene(host.scene,folder+"/Contacts.unity"),Is.True);
                EditorSceneManager.OpenScene(folder+"/Contacts.unity");
                d=Object.FindFirstObjectByType<RegionDecoration>();
                var reopened=d.CaptureGroundContactPlan(out unknown);
                Assert.That(reopened.Fingerprint,Is.EqualTo(before.Fingerprint));Assert.That(unknown,Is.Zero);
                var marker=d.GeneratedRoot.GetComponentsInChildren<RegionalDecorationInstance>().First();
                var position=marker.transform.position;
                Assert.That(reopened.Sample(position.x,position.z).BodyCoverage,Is.EqualTo(1));
                marker.transform.position+=Vector3.right;
                StringAssert.Contains("transform changed",Assert.Throws<InvalidOperationException>(()=>d.CaptureGroundContactPlan(out unknown)).Message);
                marker.transform.position=position;
                var source=marker.SourcePrefab.GetComponent<LandscapeAssetShape>();float feather=source.ContactFeather;
                source.ContactFeather+=.25f;
                Assert.Throws<InvalidOperationException>(()=>d.CaptureGroundContactPlan(out unknown));
                Assert.That(reopened.Sample(position.x,position.z).BodyCoverage,Is.EqualTo(1),"Retained snapshots must not query mutable prefabs.");
                source.ContactFeather=feather;
                d.Clear();
                Assert.Throws<InvalidOperationException>(()=>d.CaptureGroundContactPlan(out unknown));
                Assert.That(reopened.Sample(position.x,position.z).BodyCoverage,Is.EqualTo(1));
            }
            finally
            {
                if(candidate)Object.DestroyImmediate(candidate);
                if(setup.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
