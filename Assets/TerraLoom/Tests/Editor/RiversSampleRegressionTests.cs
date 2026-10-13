using System.Linq;
using NUnit.Framework;
using TerraLoom.Rivers;
using TerraLoom.Rivers.Unity;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraLoom.Tests
{
    public sealed class RiversSampleRegressionTests
    {
        [Test] public void SavedSampleCanReplanFromItsOriginalTerrain()
        {
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/TerraLoom/RiversSamples/Generated/RiversSample.unity");
                var rivers=Object.FindFirstObjectByType<TerraLoomRivers>();
                Assert.That(rivers,Is.Not.Null);
                var world=rivers.World;
                Assert.That(world,Is.Not.Null);
                var source=rivers.OriginalTerrainData;
                Assert.That(source,Is.Not.Null,"The saved sample must retain its uncarved source TerrainData.");
                var input=rivers.CaptureInput(out var bounds,out var requests);
                var profile=rivers.CaptureProfile();
                var plan=RiverPlanner.Plan(input,profile,bounds,requests);
                TestContext.WriteLine($"scene=RiversSample world={world.WorldId} seed={world.Seed} revision={world.Revision} " +
                    $"source={source.name} size={source.size} anchors={string.Join(",",world.ManualAnchors.Select(a=>a.Id+":"+a.Position))} " +
                    $"request={string.Join(",",requests.Select(r=>r.Id+":"+r.SourceId+">"+r.MouthId))} " +
                    $"width={profile.Width} bank={profile.BankWidth} depth={profile.Depth} cell={profile.CellSize} " +
                    $"slope={profile.MaximumSlope} footprintSample={profile.SampleSpacing} input={input.Identity} " +
                    $"report={string.Join(" | ",plan.Reports.Select(r=>r.Outcome+":"+r.Detail))}");
                Assert.That(plan.Complete,Is.True,string.Join(" | ",plan.Reports.Select(r=>r.Detail)));
                Assert.That(plan.Routes.Count,Is.EqualTo(1));
                Assert.That(rivers.Generate(),Is.True,rivers.Diagnostics);
                Assert.That(rivers.LastPlan.Routes.Count,Is.EqualTo(1));
            }
            finally
            {
                if(previous.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
    }
}
