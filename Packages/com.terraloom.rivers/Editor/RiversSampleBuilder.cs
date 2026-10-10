using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TerraLoom.Core.Unity;
using TerraLoom.Core.Editor;
using TerraLoom.Rivers.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TerraLoom.Rivers.Editor
{
    public static class RiversSampleBuilder
    {
        public const string SampleDirectory = "Assets/TerraLoom/RiversSamples/Generated";
        public const string SampleScene = SampleDirectory + "/RiversSample.unity";
        private const string Owner = "TerraLoom.Rivers.SampleBuilder/v1; original procedural assets; no third-party art";

        [MenuItem("Tools/TerraLoom/Rivers/Build Sample Scene")]
        public static void BuildInteractive()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildBatch();
        }

        [MenuItem("Tools/TerraLoom/Rivers/Build Terrain Brush Sample Scene (Experimental)")]
        public static void BuildInsetInteractive()
        {
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())BuildInsetBatch();
        }

        /// <summary>Offline, GPU-free executeMethod entry. Replaces the open scene; use BuildInteractive in the editor.</summary>
        public static void BuildBatch() => Build(false);

        /// <summary>Same owned sample assets with experimental inset water. Not a combined bridge fixture.</summary>
        public static void BuildInsetBatch() => Build(true);

        private static void Build(bool inset)
        {
            WaterStyleLibrary.Build();
            EnsureFolder(SampleDirectory);
            // Refuse all unowned collisions before changing any saved sample content.
            foreach (string guid in AssetDatabase.FindAssets("", new[] { SampleDirectory }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsValidFolder(path)) RequireOwned(path);
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var data = new TerrainData { name = "Original Rivers slope", heightmapResolution = 129, size = new Vector3(96, 16, 96) };
            var heights = new float[129, 129];
            for (int z = 0; z < 129; z++)
                for (int x = 0; x < 129; x++)
                {
                    float px=96f*x/128f,pz=96f*z/128f;
                    // Keep the known river corridor unchanged. Side hills expose relief and
                    // slope-dependent source layers without inventing a biome requirement.
                    float shoulder=inset?Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Abs(px-48)-22)/20)):0;
                    float hill=shoulder*(5+3*Mathf.Pow(Mathf.Sin(pz*.055f+.4f),2));
                    heights[z,x]=(6f-.03f*pz+hill)/16f;
                }
            data.SetHeights(0, 0, heights);
            var layer = SurfaceLayer("Meadow", LandscapeSurfaceRecipe.Surface.Meadow);
            if(inset)
            {
                var sedimentLayer=SurfaceLayer("Sediment",LandscapeSurfaceRecipe.Surface.DampSediment);
                var rockLayer=SurfaceLayer("Rock",LandscapeSurfaceRecipe.Surface.WeatheredRock);
                data.terrainLayers=new[]{layer,sedimentLayer,rockLayer};data.alphamapResolution=128;
                var weights=new float[128,128,3];
                for(int z=0;z<128;z++)for(int x=0;x<128;x++)
                {
                    float u=x/127f,v=z/127f;
                    float slope=Mathf.InverseLerp(12,27,data.GetSteepness(u,v));
                    float high=Mathf.InverseLerp(8,12,data.GetInterpolatedHeight(u,v));
                    float exposed=Mathf.Clamp01(Mathf.Max(slope,high));
                    weights[z,x,0]=1-exposed;weights[z,x,2]=exposed;
                }
                data.SetAlphamaps(0,0,weights);
            }
            else data.terrainLayers = new[] { layer };
            data = Save(data, SampleDirectory + "/OriginalSlopeTerrain.asset", true);
            var terrain = Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();
            terrain.name = "Slope Terrain (copy carved by Rivers)";
            terrain.drawInstanced = true;
            terrain.materialTemplate = Save(MakeMaterial("Original terrain", "Universal Render Pipeline/Terrain/Lit", Color.white), SampleDirectory + "/Terrain.mat", true);
            var world = new GameObject("Rivers Sample World").AddComponent<TerraLoomWorld>();
            world.WorldId = "rivers-sample"; world.Seed = 42; world.Terrain = terrain;
            world.AutoGenerateOnStart = false; world.UseSeedAnchors = false;
            if(inset)world.Areas=new[]{new LandscapeArea{Id="river-landscape",Center=new Vector2(48,48),Size=new Vector2(96,96),Tags=new[]{"sample","landscape"}}};
            world.ManualAnchors = new[] { Anchor("source", 48, 8, terrain), Anchor("mouth", 48, 88, terrain) };
            var rivers = world.gameObject.AddComponent<TerraLoomRivers>();
            rivers.World = world; rivers.GenerateOnStart = false; rivers.AutomaticSourceAndMouth = false;
            rivers.Connections.Add(new RiverConnectionSettings { Id = "sample-river", SourceId = "source", MouthId = "mouth" });
            rivers.CarveTerrainCopy = true;
            rivers.WaterInset = inset ? .6f : 0;
            rivers.TerrainBrush = inset;
            rivers.SedimentTerrainLayer = inset ? 1 : -1;
            rivers.ProtectedAreas.Add(new RiverProtectionSettings { Id = "sample-detour", Center = new Vector2(48,48), Size = new Vector2(8,12) });
            rivers.BedMaterial = Save(MakeMaterial("Original river bed", "Universal Render Pipeline/Lit", new Color(.25f, .18f, .1f)), SampleDirectory + "/Bed.mat", true);
            rivers.BankMaterial = Save(MakeMaterial("Original river bank", "Universal Render Pipeline/Lit", new Color(.43f, .34f, .19f)), SampleDirectory + "/Bank.mat", true);
            string waterShader = Shader.Find("TerraLoom/RiverWater") ? "TerraLoom/RiverWater" : "Universal Render Pipeline/Lit";
            rivers.WaterMaterial = Save(MakeMaterial("Original river water", waterShader, new Color(.07f, .35f, .43f)), SampleDirectory + "/Water.mat", true);
            if (!rivers.Generate()) throw new InvalidOperationException("Rivers sample generation failed: " + rivers.Diagnostics);
            Bake(rivers, SampleDirectory + "/Baked", true);
            if(inset)AddHabitatDecoration(world,rivers);
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 2;
            sun.transform.rotation = Quaternion.Euler(48, -35, 0); sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun; RenderSettings.ambientLight = new Color(.6f, .66f, .72f);
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(100, 90, -38); camera.transform.LookAt(new Vector3(48, 4, 48));
            camera.fieldOfView = 48; camera.farClipPlane = 500; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.48f, .65f, .78f);
            SaveText(SampleDirectory + "/RiversPlan.json", rivers.PlanJson);
            SaveText(SampleDirectory + "/Provenance.json", "{\n  \"owner\": \"TerraLoom.Rivers.SampleBuilder/v1\",\n  \"recipe\": \"129x129; size 96x16x96; height metres = 6 - 0.03*z; brush sample adds side hills outside abs(x-48)>22m; source meadow/rock weights use slope12-27deg and height8-12m; source (48,8); mouth (48,88); protected detour (48,48) size 8x12; seed 42; WaterInset and SedimentTerrainLayer see saved component\",\n  \"assets\": \"Original generated terrain, periodic 256px meadow/damp-sediment/rock albedo-smoothness and matched normals, two-metre tiles (brush sample); Core LandscapeSurfaceRecipe, URP materials, baked terrain copy and river meshes. Brush habitat profiles and candidate budgets see saved child decorators; decoration follows final river carving and reservation exclusions. Original Core seasonal meshes/prefabs. No external art. Water uses TerraLoom/RiverWater when available, otherwise URP/Lit.\"\n}\n");
            MarkDirty(rivers); EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
            if (File.Exists(SampleScene)) RequireOwned(SampleScene);
            if (!EditorSceneManager.SaveScene(scene, SampleScene)) throw new IOException("Cannot save Rivers sample scene.");
            Stamp(SampleScene);
            Debug.Log("Rivers sample ready: " + SampleScene + "\n" + rivers.Diagnostics + "\nNo rendering performed; parent may render the saved scene.");
        }

        // The same original surface recipe is available to Core and optional modules.
        // Texture normal and albedo share a physical tile; alpha carries local smoothness.
        private static TerrainLayer SurfaceLayer(string name, LandscapeSurfaceRecipe.Surface surface)
        {
            var diffuse=Save(LandscapeSurfaceRecipe.Create(surface,false),SampleDirectory+"/"+name+"Texture.asset",true);
            var normal=Save(LandscapeSurfaceRecipe.Create(surface,true),SampleDirectory+"/"+name+"Normal.asset",true);
            return Save(new TerrainLayer { name="Original "+name, diffuseTexture=diffuse,normalMapTexture=normal,
                tileSize=new Vector2(LandscapeSurfaceRecipe.TileMetres,LandscapeSurfaceRecipe.TileMetres),normalScale=1,
                metallic=0,smoothnessSource=TerrainLayerSmoothnessSource.DiffuseAlphaChannel },SampleDirectory+"/"+name+"Layer.terrainlayer",true);
        }

        /// <summary>Sample composition: generate decoration AFTER the final river terrain.
        /// Separate adapters expose category-specific habitat rules using existing Core APIs.</summary>
        private static void AddHabitatDecoration(TerraLoomWorld world,TerraLoomRivers rivers)
        {
            SeasonalAssetBuilder.Build();
            var exclusions=rivers.LastPlan.Routes.SelectMany(r=>r.Offers).SelectMany(o=>o.Reservations)
                .Select(a=>new Rect((float)a.Bounds.MinX-2,(float)a.Bounds.MinZ-2,
                    (float)(a.Bounds.MaxX-a.Bounds.MinX)+4,(float)(a.Bounds.MaxZ-a.Bounds.MinZ)+4)).ToArray();
            string[] kinds={"Trees","Rocks","Grass"};
            string[] assets={"SummerPine","GraniteBoulder","MeadowGrass"};
            for(int category=0;category<3;category++)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SeasonalAssetBuilder.Root+"/"+assets[category]+".prefab");
                if(!prefab)throw new InvalidOperationException("Missing original habitat prefab "+assets[category]);
                var profile=ScriptableObject.CreateInstance<RegionalStyleProfile>();
                profile.name=kinds[category]+" habitat";profile.RegionId="river-landscape";profile.UseHabitat=true;
                profile.ElevationRange=category==1?new Vector2(7,16):new Vector2(0,category==0?7.5f:8.5f);
                profile.SlopeRange=new Vector2(0,category==1?65:category==0?26:32);
                profile.Density=category==1?1:.9f;profile.ClusterScale=12;profile.ClusterStrength=category==1?.2f:.45f;
                profile.MinScale=category==0?.65f:.8f;profile.MaxScale=category==0?.9f:1.3f;
                profile.AlignToTerrainNormal=category==1;profile.GroundOffset=category==1?-.3f:-.05f;
                if(category==0)profile.Trees=new[]{prefab};
                else if(category==1)profile.Rocks=new[]{prefab};else profile.Grass=new[]{prefab};
                profile.Validate();profile=Save(profile,SampleDirectory+"/"+kinds[category]+"Habitat.asset",true);
                var go=new GameObject(kinds[category]+" - habitat after Rivers");go.transform.SetParent(world.transform,false);
                var decorator=go.AddComponent<RegionDecoration>();decorator.World=world;decorator.Profiles=new[]{profile};
                decorator.SeedOffset=103+category*271;decorator.Count=category==2?900:600;
                decorator.MaximumSlope=65;decorator.Exclusions=exclusions;decorator.Generate();
                Debug.Log("RIVER_HABITAT "+kinds[category]+" placed="+decorator.PlacedCount+" rejected="+decorator.RejectedCount);
            }
        }

        public static void Bake(TerraLoomRivers rivers, string directory) => Bake(rivers, directory, false);

        private static void Bake(TerraLoomRivers rivers, string directory, bool sample)
        {
            if (!rivers || !rivers.GeneratedRoot || !rivers.World || !rivers.World.Terrain)
                throw new ArgumentException("Generate Rivers on an assigned terrain before baking.");
            if(!rivers.WaterMaterial||(!rivers.TerrainBrush&&(!rivers.BedMaterial||!rivers.BankMaterial)))
                throw new InvalidOperationException("Bind water material; mesh rivers also require bed and bank materials before baking.");
            EnsureFolder(directory);
            var persistence = new Persistence(directory, sample);
            // Save the original too: Clear after reload must never restore a missing transient reference.
            var original = rivers.OriginalTerrainData;
            if (original && !AssetDatabase.Contains(original))
            {
                var saved = persistence.Terrain(original, "OriginalTerrain");
                var serialized = new SerializedObject(rivers);
                var property = serialized.FindProperty("originalData");
                if (property == null) throw new InvalidOperationException("Rivers original terrain binding is unavailable.");
                property.objectReferenceValue = saved; serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            if (rivers.CarvedTerrainData && !AssetDatabase.Contains(rivers.CarvedTerrainData))
                rivers.MarkTerrainBaked(persistence.Terrain(rivers.CarvedTerrainData, "CarvedTerrain"));
            var terrain = rivers.World.Terrain;
            if (!AssetDatabase.Contains(terrain.terrainData))
            {
                terrain.terrainData = persistence.Terrain(terrain.terrainData, "Terrain");
                if (rivers.World.OwnsTerrain) { rivers.World.PreserveGeneratedTerrainData(); EditorUtility.SetDirty(rivers.World); }
            }
            var terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider) { terrainCollider.terrainData = terrain.terrainData; EditorUtility.SetDirty(terrainCollider); }
            if (terrain.materialTemplate) terrain.materialTemplate = persistence.Material(terrain.materialTemplate);
            int index = 0;
            foreach (var marker in rivers.GeneratedRoot.GetComponentsInChildren<RiverGeneratedGeometry>(true))
            {
                var filter = marker.GetComponent<MeshFilter>(); var collider = marker.GetComponent<MeshCollider>();
                if (!filter || !filter.sharedMesh) throw new InvalidOperationException("Generated river mesh is missing.");
                var previous = filter.sharedMesh;
                if (!AssetDatabase.Contains(previous)) filter.sharedMesh = Save(UnityEngine.Object.Instantiate(previous), directory + "/River" + index + "-" + marker.Role + ".asset", sample);
                if (collider) { collider.sharedMesh = null; collider.sharedMesh = filter.sharedMesh; EditorUtility.SetDirty(collider); }
                marker.MarkBaked();
                if (previous != filter.sharedMesh && !AssetDatabase.Contains(previous)) UnityEngine.Object.DestroyImmediate(previous);
                var renderer = marker.GetComponent<MeshRenderer>();
                if (!renderer) throw new InvalidOperationException("Generated river renderer is missing.");
                renderer.sharedMaterials = renderer.sharedMaterials.Select(persistence.Material).ToArray();
                EditorUtility.SetDirty(filter); EditorUtility.SetDirty(marker); EditorUtility.SetDirty(renderer); index++;
            }
            rivers.WaterMaterial = persistence.Material(rivers.WaterMaterial);
            if(rivers.BedMaterial)rivers.BedMaterial = persistence.Material(rivers.BedMaterial);
            if(rivers.BankMaterial)rivers.BankMaterial = persistence.Material(rivers.BankMaterial);
            EditorUtility.SetDirty(terrain); MarkDirty(rivers); AssetDatabase.SaveAssets();
        }

        private sealed class Persistence
        {
            private readonly string directory;
            private readonly bool sample;
            private readonly Dictionary<UnityEngine.Object, UnityEngine.Object> saved = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            public Persistence(string directory, bool sample) { this.directory = directory; this.sample = sample; }
            private T Copy<T>(T source, string name, string extension = ".asset") where T : UnityEngine.Object
            {
                if (!source) return null;
                if (saved.TryGetValue(source, out var result)) return (T)result;
                var copy = Save(UnityEngine.Object.Instantiate(source), directory + "/" + name + extension, sample);
                saved.Add(source, copy); return copy;
            }
            private Texture Texture(Texture source)
            {
                if (!source || AssetDatabase.Contains(source)) return source;
                if (!(source is Texture2D)) throw new InvalidOperationException("Persist render/cubemap textures before baking; transient Texture2D is supported.");
                return Copy(source, "Texture" + saved.Count);
            }
            public Material Material(Material source)
            {
                if (!source) throw new InvalidOperationException("Assign all generated river materials before baking.");
                if (saved.TryGetValue(source, out var known)) return (Material)known;
                if (AssetDatabase.Contains(source) && source.GetTexturePropertyNames().All(p => !source.GetTexture(p) || AssetDatabase.Contains(source.GetTexture(p)))) return source;
                var copy = Copy(source, "Material" + saved.Count, ".mat");
                foreach (string property in copy.GetTexturePropertyNames()) copy.SetTexture(property, Texture(copy.GetTexture(property)));
                EditorUtility.SetDirty(copy); return copy;
            }
            public TerrainData Terrain(TerrainData source, string name)
            {
                if(saved.TryGetValue(source,out var known))return (TerrainData)known;
                var copy = Save(TerrainAssetClone.Create(source),directory+"/"+name+".asset",sample);
                // Terrain native maps must be populated on the persistent destination:
                // creating/importing the asset may replace transient alpha textures.
                TerrainAssetClone.CopyInto(source,copy);
                saved.Add(source,copy);
                copy.terrainLayers = copy.terrainLayers.Select(layer =>
                {
                    if (!layer || AssetDatabase.Contains(layer)) return layer;
                    var persisted = Copy(layer, "Layer" + saved.Count, ".terrainlayer");
                    persisted.diffuseTexture = (Texture2D)Texture(layer.diffuseTexture);
                    persisted.normalMapTexture = (Texture2D)Texture(layer.normalMapTexture);
                    persisted.maskMapTexture = (Texture2D)Texture(layer.maskMapTexture);
                    EditorUtility.SetDirty(persisted); return persisted;
                }).ToArray();
                if (copy.detailPrototypes.Any(p => p.prototype && !AssetDatabase.Contains(p.prototype)) || copy.treePrototypes.Any(p => p.prefab && !AssetDatabase.Contains(p.prefab)))
                    throw new InvalidOperationException("Persist terrain detail/tree prefabs before baking.");
                var details = copy.detailPrototypes;
                foreach (var detail in details) detail.prototypeTexture = (Texture2D)Texture(detail.prototypeTexture);
                copy.detailPrototypes = details;
                EditorUtility.SetDirty(copy); return copy;
            }
        }

        internal static void MarkDirty(TerraLoomRivers rivers)
        {
            EditorUtility.SetDirty(rivers);
            if (rivers.World && rivers.World.Terrain)
            {
                var terrain = rivers.World.Terrain; EditorUtility.SetDirty(terrain);
                var collider = terrain.GetComponent<TerrainCollider>(); if (collider) EditorUtility.SetDirty(collider);
                if (terrain.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            }
            if (rivers.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(rivers.gameObject.scene);
        }

        private static Material MakeMaterial(string name, string shaderName, Color color)
        {
            var shader = Shader.Find(shaderName);
            if (!shader) throw new InvalidOperationException("Required shader missing: " + shaderName);
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", shaderName.Contains("Water") ? .8f : .12f);
            return material;
        }
        private static ManualWorldAnchor Anchor(string id, float x, float z, Terrain terrain) => new ManualWorldAnchor
        { Id = id, Position = new Vector3(x, terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y, z) };

        private static T Save<T>(T asset, string path, bool sample) where T : UnityEngine.Object
        {
            asset.hideFlags = HideFlags.None;
            if (!sample) path = AssetDatabase.GenerateUniqueAssetPath(path);
            if (sample && File.Exists(path))
            {
                RequireOwned(path);
                var existing = AssetDatabase.LoadAssetAtPath<T>(path);
                if (!existing) throw new IOException("Sample asset has incompatible type: " + path);
                if(asset is TerrainData terrainSource && existing is TerrainData terrainTarget)TerrainAssetClone.CopyInto(terrainSource,terrainTarget);
                else EditorUtility.CopySerialized(asset, existing);
                UnityEngine.Object.DestroyImmediate(asset);
                EditorUtility.SetDirty(existing); return existing;
            }
            AssetDatabase.CreateAsset(asset, path);
            // Native TerrainData maps must be flushed before importer reload; otherwise
            // a newly attached alpha texture can be replaced by the initial default map.
            AssetDatabase.SaveAssets();
            if (sample) Stamp(path);
            else { var importer = AssetImporter.GetAtPath(path); importer.userData = "TerraLoom.Rivers editor bake; generated copy; source assets preserved"; importer.SaveAndReimport(); }
            return asset;
        }
        private static void RequireOwned(string path)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null || importer.userData != Owner) throw new IOException("Refusing to overwrite user asset: " + path + ". Move it outside Generated or choose another bake folder.");
        }
        private static void Stamp(string path)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null) throw new IOException("Missing asset importer: " + path);
            importer.userData = Owner; importer.SaveAndReimport();
        }
        private static void SaveText(string path, string text)
        {
            if (File.Exists(path)) RequireOwned(path);
            File.WriteAllText(path, text, new UTF8Encoding(false)); AssetDatabase.ImportAsset(path); Stamp(path);
        }
        private static void EnsureFolder(string path)
        {
            if (path != "Assets" && !path.StartsWith("Assets/", StringComparison.Ordinal)) throw new ArgumentException("Asset output must be inside Assets.");
            var parts = path.Split('/'); string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(current, parts[i]))) throw new IOException("Cannot create " + next);
                current = next;
            }
        }
    }
}
