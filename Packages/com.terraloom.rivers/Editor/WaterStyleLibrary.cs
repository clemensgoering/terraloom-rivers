using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TerraLoom.Rivers.Editor
{
    /// <summary>Original saved water palettes. Rebuilds update existing materials without replacing their GUIDs.
    /// All ice settings are visual; this library creates no physics, terrain or scene objects.</summary>
    public static class WaterStyleLibrary
    {
        public const string Root = "Assets/TerraLoom/RiversSamples/WaterStyles";
        private const string Owner = "TerraLoom.Rivers.WaterStyleLibrary/v1";
        private const string Provenance = Owner + "\n"
            + "Original TerraLoom water material palettes, authored with OpenAI assistance.\n"
            + "Shader: TerraLoom/RiverWater; original procedural waves, bank foam and static ice variation.\n"
            + "No purchased art, third-party textures or private project content is included.\n"
            + "The shader references Unity URP lighting/shadow helpers from the installed Unity packages.\n"
            + "ClearRiver: blue-green flowing water. GlacialRiver: pale blue, slower, partial visual ice.\n"
            + "MarshWater: subdued green with slow flow. FrozenWater: pale static ice, IceAmount=1.\n"
            + "UV contract: signed across metres in UV.x, along metres in UV.y, two bank-edge vertices per row.\n"
            + "Presets disable the example world-Z winter region; ice never changes collision or navigation.\n"
            + "Rebuild updates these four material assets in place and preserves existing GUIDs.\n";

        [MenuItem("Tools/TerraLoom/Rivers/Build Water Styles")]
        public static void Build()
        {
            var shader = Shader.Find("TerraLoom/RiverWater");
            if (!shader) throw new InvalidOperationException("TerraLoom/RiverWater must be imported before building water styles.");
            // Validate the property API and all occupied paths before modifying any material.
            var probe = new Material(shader);
            try
            {
                foreach (string property in new[] { "_BaseColor", "_WaveColor", "_FlowSpeed", "_IceAmount", "_IceColor",
                    "_UseWorldRegions", "_WinterBoundaryZ", "_RegionBlend", "_FoamColor", "_FoamWidth", "_FoamStrength" })
                    if (!probe.HasProperty(property)) throw new InvalidOperationException("RiverWater is missing property " + property);
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }

            foreach (string name in new[] { "ClearRiver", "GlacialRiver", "MarshWater", "FrozenWater" })
            {
                string path = Root + "/" + name + ".mat";
                var existing = AssetDatabase.LoadMainAssetAtPath(path);
                if (existing && !(existing is Material)) throw new InvalidOperationException("Material path is occupied by another asset: " + path);
                var importer = AssetImporter.GetAtPath(path);
                if (importer != null && !string.IsNullOrEmpty(importer.userData) && importer.userData != Owner)
                    throw new InvalidOperationException("Material has another owner's metadata: " + path);
                if (!existing && File.Exists(Absolute(path))) throw new InvalidOperationException("Unimported material occupies " + path);
            }
            string provenancePath = Root + "/PROVENANCE.txt";
            if (File.Exists(Absolute(provenancePath)))
            {
                string existingProvenance = File.ReadAllText(Absolute(provenancePath));
                if (!existingProvenance.StartsWith(Owner + "\n", StringComparison.Ordinal)
                    && !existingProvenance.StartsWith(Owner + "\r\n", StringComparison.Ordinal))
                    throw new InvalidOperationException("Provenance path contains unrelated content: " + provenancePath);
            }

            EnsureFolder(Root);
            Save(shader, "ClearRiver", new Color(.04f,.32f,.40f), new Color(.35f,.70f,.73f), .8f, 0,
                new Color(.65f,.82f,.90f), new Color(.75f,.88f,.86f), .18f, .35f);
            Save(shader, "GlacialRiver", new Color(.08f,.47f,.64f), new Color(.65f,.90f,.96f), .45f, .12f,
                new Color(.72f,.89f,.96f), new Color(.85f,.96f,1), .22f, .45f);
            Save(shader, "MarshWater", new Color(.12f,.22f,.10f), new Color(.38f,.43f,.24f), .18f, 0,
                new Color(.60f,.73f,.65f), new Color(.55f,.61f,.40f), .12f, .16f);
            Save(shader, "FrozenWater", new Color(.06f,.18f,.26f), new Color(.55f,.75f,.86f), 0, 1,
                new Color(.68f,.84f,.92f), new Color(.85f,.93f,.97f), .18f, 0);
            if (!File.Exists(Absolute(provenancePath)) || File.ReadAllText(Absolute(provenancePath)) != Provenance)
                File.WriteAllText(Absolute(provenancePath), Provenance, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(provenancePath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void Save(Shader shader, string name, Color water, Color reflections, float flow, float ice,
            Color iceColor, Color foam, float foamWidth, float foamStrength)
        {
            string path = Root + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            // Keep the existing Unity object, asset and meta rather than deleting/recreating them.
            material.shader = shader;
            material.name = name;
            material.SetColor("_BaseColor", water);
            material.SetColor("_WaveColor", reflections);
            material.SetFloat("_FlowSpeed", flow);
            material.SetFloat("_IceAmount", ice);
            material.SetColor("_IceColor", iceColor);
            material.SetFloat("_UseWorldRegions", 0);
            material.SetFloat("_WinterBoundaryZ", 48);
            material.SetFloat("_RegionBlend", 6);
            material.SetColor("_FoamColor", foam);
            material.SetFloat("_FoamWidth", foamWidth);
            material.SetFloat("_FoamStrength", foamStrength);
            material.enableInstancing = true;
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            var importer = AssetImporter.GetAtPath(path);
            if (importer != null && importer.userData != Owner)
            {
                importer.userData = Owner;
                AssetDatabase.WriteImportSettingsIfDirty(path);
            }
        }
        private static string Absolute(string assetPath) => Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            if (split <= 0) throw new InvalidOperationException("Invalid water style folder: " + path);
            string parent = path.Substring(0, split);
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, path.Substring(split + 1))))
                throw new InvalidOperationException("Cannot create water style folder: " + path);
        }
    }
}
