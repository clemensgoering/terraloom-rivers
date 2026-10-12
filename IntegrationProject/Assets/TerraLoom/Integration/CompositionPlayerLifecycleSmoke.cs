using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Integration
{
    /// <summary>Opt-in Windows Player lifecycle evidence. Never runs in normal scenes or Editor tests.</summary>
    public sealed class CompositionPlayerLifecycleSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(!Application.isEditor&&Environment.GetCommandLineArgs().Contains("-terraloomLifecycle"))
                new GameObject("Composition Player lifecycle evidence").AddComponent<CompositionPlayerLifecycleSmoke>();
        }

        private IEnumerator Start()
        {
            var run=Probe();
            while(true)
            {
                bool more;object next=null;
                try{more=run.MoveNext();if(more)next=run.Current;}
                catch(Exception ex)
                {
                    Debug.LogError("TERRALOOM_PLAYER_LIFECYCLE_FAILED "+ex);
                    (run as IDisposable)?.Dispose();Application.Quit(1);yield break;
                }
                if(!more){(run as IDisposable)?.Dispose();Application.Quit(0);yield break;}
                yield return next;
            }
        }

        private IEnumerator Probe()
        {
            // Let the existing scene initialize and all startup Destroy calls drain.
            yield return null;yield return null;
            var demo=FindFirstObjectByType<SeededWorldDemo>();
            Require(demo&&demo.Composition&&demo.Decoration,"Missing existing seed demo.");
            var c=demo.Composition;var decoration=demo.Decoration;
            Require(c.World.OwnsTerrain,"The seed demo must initialize an owned Core source.");
            c.Clear();
            yield return null;yield return null;
            var source=c.World.Terrain.terrainData;
            var collider=c.World.Terrain.GetComponent<TerrainCollider>();var colliderSource=collider.terrainData;
            var baseline=Snapshot.Capture();
            var recipe=ScriptableObject.CreateInstance<CompositionRecipe>();recipe.CaptureFrom(c,decoration);recipe.Settings.Seed=2042;
            try
            {
                Require(c.GenerateRecipe(recipe,decoration),c.Diagnostics);
                yield return null;yield return null;
                var first=Snapshot.Capture();var carved=c.World.Terrain.terrainData;var generatedSource=c.Rivers.OriginalTerrainData;
                var path=c.Paths.GeneratedRoot;var river=c.Rivers.GeneratedRoot;var nature=decoration.GeneratedRoot;
                Require(path&&river&&nature,"Generation must publish all three output roots.");
                string pathJson=c.Paths.PlanJson,riverJson=c.Rivers.PlanJson;
                string digest=c.World.CaptureTerrainFingerprint(generatedSource);
                c.Clear();
                yield return null;yield return null;
                Require(c.World.Terrain.terrainData==source&&collider.terrainData==colliderSource,"Clear did not restore source bindings.");
                Require(!carved&&!generatedSource&&!path&&!river&&!nature,"Deferred Destroy retained a generated resource.");
                var cleared=Snapshot.Capture();baseline.RequireSameIds(cleared);
                Debug.Log("TERRALOOM_PLAYER_LIFECYCLE_CLEAR_OK seed=2042 afterTwoFrames=true "+cleared);
                Require(c.GenerateRecipe(recipe,decoration),c.Diagnostics);
                yield return null;yield return null;
                var second=Snapshot.Capture();
                Require(first.Meshes.Length==second.Meshes.Length&&first.Terrains.Length==second.Terrains.Length&&first.Objects.Length==second.Objects.Length,
                    "Regeneration resource totals grew: first "+first+", second "+second);
                Require(c.World.Seed==2042&&c.Paths.GeneratedRoot&&c.Rivers.GeneratedRoot&&decoration.GeneratedRoot,"Regeneration did not publish.");
                Require(c.ValidateCurrent(out var reason),reason);
                Require(c.Paths.PlanJson==pathJson&&c.Rivers.PlanJson==riverJson,"Same-seed plans changed after Clear.");
                Require(c.World.CaptureTerrainFingerprint(c.Rivers.OriginalTerrainData)==digest,"Same-seed source changed after Clear.");
                Debug.Log("TERRALOOM_PLAYER_LIFECYCLE_SUCCESS unity="+Application.unityVersion+" seed=2042 baseline="+baseline+" first="+first+" second="+second);
            }
            finally{Destroy(recipe);}
        }

        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private sealed class Snapshot
        {
            public int[] Meshes,Terrains,Objects;
            private static int[] Ids<T>() where T:Object => Resources.FindObjectsOfTypeAll<T>().Where(o=>o).Select(o=>o.GetInstanceID()).OrderBy(id=>id).ToArray();
            public static Snapshot Capture()=>new Snapshot{Meshes=Ids<Mesh>(),Terrains=Ids<TerrainData>(),Objects=Ids<GameObject>()};
            public void RequireSameIds(Snapshot other)
            {
                Require(Meshes.SequenceEqual(other.Meshes),"Mesh IDs differ after deferred Clear.");
                Require(Terrains.SequenceEqual(other.Terrains),"TerrainData IDs differ after deferred Clear.");
                Require(Objects.SequenceEqual(other.Objects),"GameObject IDs differ after deferred Clear.");
            }
            public override string ToString()=>"meshes="+Meshes.Length+",terrainData="+Terrains.Length+",objects="+Objects.Length;
        }
    }
}
