using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TerraLoom.Core.Unity;
using TerraLoom.Paths.Unity;
using TerraLoom.Rivers.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TerraLoom.Integration.Editor
{
    /// <summary>Stages new assets without modifying the live publication, then swaps
    /// scene references with a reversible journal. Never overwrites user assets.</summary>
    public static class CompositionSceneBaker
    {
        // Deterministic editor test fault injection; no callback runs inside the swap.
        internal static string InjectFailureAt;

        public static void Bake(TerraLoomIntegration instance,string directory)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Bake the published world in Edit Mode.");
            if(!instance||!instance.World||!instance.World.Terrain||!instance.World.Terrain.terrainData||
                !instance.Rivers||!instance.Rivers.GeneratedRoot||!instance.Paths||!instance.Paths.GeneratedRoot)
                throw new InvalidOperationException("Generate a shared Rivers/Paths world before baking.");
            if(!instance.ValidateCurrent(out var initial))throw new InvalidOperationException("Composition output is stale: "+initial);
            ValidateFolder(directory);
            var targets=Targets(instance);
            var copies=new Copies(directory);
            var journal=new List<Bindings>();
            var retiredMeshes=new HashSet<Mesh>();
            var oldCarve=instance.Rivers.CarvedTerrainData;
            bool ownedCarve=ReadBool(instance.Rivers,"ownsTransientData");
            bool success=false;
            Action retireSource=null,retireSession=null;
            try
            {
                // Discover and copy every supported resource before changing a binding.
                copies.PrepareDirectory();
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach(var target in targets)
                    {
                        var serialized=new SerializedObject(target);var iterator=serialized.GetIterator();
                        while(iterator.Next(true))
                            if(iterator.propertyType==SerializedPropertyType.ObjectReference)
                                copies.Persist(iterator.objectReferenceValue);
                    }
                }
                finally { AssetDatabase.StopAssetEditing(); }
                AssetDatabase.SaveAssets();Fault("assets-staged");
                if(!instance.ValidateCurrent(out var beforeSwap))
                    throw new InvalidOperationException("Composition changed while staging assets: "+beforeSwap);
                foreach(var marker in targets.OfType<Component>().Where(c=>c is RiverGeneratedGeometry||c is PathGeneratedGeometry))
                    if(ReadBool(marker,"ownsMesh"))
                    {
                        var filter=marker.GetComponent<MeshFilter>();
                        if(filter&&filter.sharedMesh&&!AssetDatabase.Contains(filter.sharedMesh))retiredMeshes.Add(filter.sharedMesh);
                    }
                foreach(var target in targets)
                {
                    var binding=new Bindings(target);journal.Add(binding);
                    binding.Remap(copies.Map);
                    if(target is RiverGeneratedGeometry||target is PathGeneratedGeometry)
                    {
                        binding.SetBool("ownsMesh",false);
                    }
                    if(target==instance.Rivers)binding.SetBool("ownsTransientData",false);
                    if(target==instance.World&&instance.World.OwnsTerrain)binding.SetBool("generatedDataIsTransient",false);
                }
                instance.Rivers.RebindBakedWaterQuery();
                Fault("bindings-swapped");
                if(!instance.ValidateCurrent(out var final))
                    throw new InvalidOperationException("Baked resources do not match the shared publication: "+final);
                retireSource=instance.PrepareBakedSourceRetirement();
                retireSession=instance.World.PrepareBakedSessionRetirement();
                AssetDatabase.SaveAssets();Fault("before-complete");
                foreach(var target in targets)EditorUtility.SetDirty(target);
                EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
                success=true;
            }
            finally
            {
                if(!success)
                {
                    // Old resources were never destroyed; restore references before deleting copies.
                    var rollbackErrors=new List<Exception>();
                    for(int i=journal.Count-1;i>=0;i--)
                        try{journal[i].Restore();}catch(Exception ex){rollbackErrors.Add(ex);}
                    if(rollbackErrors.Count!=0)
                        throw new AggregateException("Bake rollback could not restore every binding; staged assets retained for recovery.",rollbackErrors);
                    copies.Discard();
                }
            }
            // Retirement is after the reversible boundary. Caller-supplied materials/source
            // terrain stay alive; only module-owned transient output resources are retired.
            retireSource();
            retireSession();
            foreach(var mesh in retiredMeshes)if(mesh&&!AssetDatabase.Contains(mesh))Object.DestroyImmediate(mesh);
            if(ownedCarve&&oldCarve&&!AssetDatabase.Contains(oldCarve))Object.DestroyImmediate(oldCarve);
        }

        private static List<Object> Targets(TerraLoomIntegration instance)
        {
            var result=new HashSet<Object>{instance,instance.World,instance.Rivers,instance.Paths,instance.World.Terrain};
            var collider=instance.World.Terrain.GetComponent<TerrainCollider>();if(collider)result.Add(collider);
            var roots=new List<GameObject>{instance.Rivers.GeneratedRoot,instance.Paths.GeneratedRoot};
            foreach(var decoration in Object.FindObjectsByType<RegionDecoration>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(decoration.World==instance.World&&decoration.GeneratedRoot){result.Add(decoration);roots.Add(decoration.GeneratedRoot);}
            foreach(var root in roots)foreach(var component in root.GetComponentsInChildren<Component>(true))
                if(component)result.Add(component);
            return result.OrderBy(x=>x.GetInstanceID()).ToList();
        }
        private static bool ReadBool(Object target,string name)
        {var property=new SerializedObject(target).FindProperty(name);return property!=null&&property.boolValue;}
        private static void Fault(string point)
        {if(InjectFailureAt==point)throw new IOException("Injected Composition bake failure: "+point);}

        private static void ValidateFolder(string directory)
        {
            if(string.IsNullOrWhiteSpace(directory)||!directory.StartsWith("Assets/",StringComparison.Ordinal)||
                directory.Contains("..")||directory.Contains('\\')||directory.EndsWith("/",StringComparison.Ordinal))
                throw new InvalidOperationException("Choose a new folder inside Assets for the composition bake.");
            string assets=Path.GetFullPath(Application.dataPath);
            string absolute=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(assets),directory));
            if(!absolute.StartsWith(assets+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Bake folder must stay inside Assets.");
            for(var parent=new DirectoryInfo(absolute);parent!=null&&parent.FullName.Length>=assets.Length;parent=parent.Parent)
                if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)
                    throw new InvalidOperationException("Bake folders may not pass through filesystem links.");
            if(Directory.Exists(absolute)&&Directory.EnumerateFileSystemEntries(absolute).Any())
                throw new InvalidOperationException("Choose an empty bake folder; existing files are never overwritten.");
        }

        private sealed class Bindings
        {
            private readonly Object target;
            private readonly Dictionary<string,Object> references=new Dictionary<string,Object>();
            private readonly Dictionary<string,bool> booleans=new Dictionary<string,bool>();
            public Bindings(Object target){this.target=target;}
            public void Remap(Dictionary<Object,Object> map)
            {
                var serialized=new SerializedObject(target);var iterator=serialized.GetIterator();
                while(iterator.Next(true))
                    if(iterator.propertyType==SerializedPropertyType.ObjectReference&&iterator.objectReferenceValue&&
                        map.TryGetValue(iterator.objectReferenceValue,out var copy))
                    {
                        references.Add(iterator.propertyPath,iterator.objectReferenceValue);
                        iterator.objectReferenceValue=copy;
                    }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            public void SetBool(string name,bool value)
            {
                var serialized=new SerializedObject(target);var property=serialized.FindProperty(name);
                if(property==null)throw new InvalidOperationException("Bake ownership field is unavailable: "+name);
                booleans.Add(name,property.boolValue);property.boolValue=value;serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            public void Restore()
            {
                var serialized=new SerializedObject(target);
                foreach(var pair in references)serialized.FindProperty(pair.Key).objectReferenceValue=pair.Value;
                foreach(var pair in booleans)serialized.FindProperty(pair.Key).boolValue=pair.Value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private sealed class Copies
        {
            public readonly Dictionary<Object,Object> Map=new Dictionary<Object,Object>();
            private readonly string directory;
            private readonly List<string> assets=new List<string>(),folders=new List<string>();
            private readonly List<Object> allocated=new List<Object>();
            public Copies(string directory){this.directory=directory;}
            // Folders are fully imported before the asset-editing scope. Staging never
            // attempts to load any of its newly created assets while imports are paused.
            public void PrepareDirectory()=>EnsureFolder(directory);
            public Object Persist(Object source)
            {
                if(!source)return source;
                if(Map.TryGetValue(source,out var existing))return existing;
                if(!(source is Mesh||source is Material||source is Texture||source is TerrainData||source is TerrainLayer
                    ||source is CompositionRecipe||source is RegionalStyleProfile))return source;
                if(!NeedsCopy(source))return source;
                if(source is Texture&&!(source is Texture2D))
                    throw new InvalidOperationException("Persist transient textures as Texture2D assets before baking; unsupported texture: "+source.GetType().Name);
                var copy=Object.Instantiate(source);allocated.Add(copy);copy.name=source.name;copy.hideFlags=HideFlags.None;Map.Add(source,copy);
                if(copy is CompositionRecipe||copy is RegionalStyleProfile)
                {
                    // Only the two explicit TerraLoom authoring asset types participate.
                    // Never edit the caller's recipe/profile or clone arbitrary scene prefabs.
                    var serialized=new SerializedObject(copy);var iterator=serialized.GetIterator();
                    while(iterator.Next(true))
                        if(iterator.propertyType==SerializedPropertyType.ObjectReference&&iterator.propertyPath!="m_Script")
                        {
                            var reference=iterator.objectReferenceValue;
                            if(reference is GameObject&&!AssetDatabase.Contains(reference))
                                throw new InvalidOperationException("Save transient regional prefabs before baking.");
                            iterator.objectReferenceValue=Persist(reference);
                        }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                if(copy is Material material)
                    foreach(string property in material.GetTexturePropertyNames())material.SetTexture(property,(Texture)Persist(material.GetTexture(property)));
                if(copy is TerrainLayer layer)
                {
                    layer.diffuseTexture=(Texture2D)Persist(layer.diffuseTexture);layer.normalMapTexture=(Texture2D)Persist(layer.normalMapTexture);
                    layer.maskMapTexture=(Texture2D)Persist(layer.maskMapTexture);
                }
                if(copy is TerrainData terrain)
                {
                    terrain.terrainLayers=terrain.terrainLayers.Select(x=>(TerrainLayer)Persist(x)).ToArray();
                    foreach(var tree in terrain.treePrototypes)if(tree.prefab&&!AssetDatabase.Contains(tree.prefab))
                        throw new InvalidOperationException("Save transient terrain tree prefabs before baking.");
                    var details=terrain.detailPrototypes;
                    foreach(var detail in details)
                    {
                        if(detail.prototype&&!AssetDatabase.Contains(detail.prototype))throw new InvalidOperationException("Save transient detail prefabs before baking.");
                        detail.prototypeTexture=(Texture2D)Persist(detail.prototypeTexture);
                    }
                    terrain.detailPrototypes=details;
                }
                string path=directory+"/Resource"+assets.Count+(copy is Material?".mat":".asset");
                if(File.Exists(path)||AssetDatabase.LoadMainAssetAtPath(path))
                    throw new InvalidOperationException("A staged bake asset path is occupied: "+path);
                assets.Add(path);AssetDatabase.CreateAsset(copy,path);
                return copy;
            }
            private static bool NeedsCopy(Object source)
            {
                if(!source)return false;
                if(!AssetDatabase.Contains(source))return true;
                if(source is CompositionRecipe||source is RegionalStyleProfile)
                {
                    var serialized=new SerializedObject(source);var iterator=serialized.GetIterator();
                    while(iterator.Next(true))
                        if(iterator.propertyType==SerializedPropertyType.ObjectReference&&iterator.propertyPath!="m_Script"
                            &&NeedsCopy(iterator.objectReferenceValue))return true;
                    return false;
                }
                // Persistent caller assets may contain temporary dependencies. Copy the
                // parent too; never rewrite the caller's material/layer/terrain asset.
                if(source is Material material)return material.GetTexturePropertyNames().Any(p=>NeedsCopy(material.GetTexture(p)));
                if(source is TerrainLayer layer)return NeedsCopy(layer.diffuseTexture)||NeedsCopy(layer.normalMapTexture)||NeedsCopy(layer.maskMapTexture);
                if(source is TerrainData terrain)return terrain.terrainLayers.Any(NeedsCopy)||terrain.detailPrototypes.Any(d=>NeedsCopy(d.prototypeTexture));
                return false;
            }
            private void EnsureFolder(string path)
            {
                if(AssetDatabase.IsValidFolder(path))return;
                string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent,Path.GetFileName(path));folders.Add(path);
            }
            public void Discard()
            {
                var failures=new List<string>();
                AssetDatabase.StartAssetEditing();
                try
                {
                    for(int i=assets.Count-1;i>=0;i--)
                        if((File.Exists(assets[i])||AssetDatabase.LoadMainAssetAtPath(assets[i]))&&!AssetDatabase.DeleteAsset(assets[i]))failures.Add(assets[i]);
                }
                finally { AssetDatabase.StopAssetEditing(); }
                foreach(var item in allocated)if(item&&!AssetDatabase.Contains(item))Object.DestroyImmediate(item);
                for(int i=folders.Count-1;i>=0;i--)
                    if(AssetDatabase.IsValidFolder(folders[i])&&!Directory.EnumerateFileSystemEntries(folders[i]).Any()&&!AssetDatabase.DeleteAsset(folders[i]))failures.Add(folders[i]);
                if(failures.Count!=0)throw new IOException("World bindings restored, but staged asset cleanup failed: "+string.Join(", ",failures));
            }
        }
    }
}
