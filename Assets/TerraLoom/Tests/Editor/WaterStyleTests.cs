using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TerraLoom.Tests
{
    public sealed class WaterStyleTests
    {
        [Test] public void OriginalWaterPalettesPreserveConfigurableFlowIceAndRegionProperties()
        {
            const string root="Assets/TerraLoom/RiversSamples/WaterStyles/";
            foreach(string name in new[]{"ClearRiver","GlacialRiver","MarshWater","FrozenWater"})
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(root+name+".mat");Assert.That(material,Is.Not.Null,name);
                Assert.That(material.shader.name,Is.EqualTo("TerraLoom/RiverWater"));Assert.That(ShaderUtil.ShaderHasError(material.shader),Is.False);
                foreach(string property in new[]{"_FlowSpeed","_IceAmount","_UseWorldRegions","_WinterBoundaryZ","_RegionBlend","_FoamStrength"})Assert.That(material.HasProperty(property),Is.True,property);
                Assert.That(material.GetFloat("_UseWorldRegions"),Is.Zero,"Palettes must work independently of the example world's coordinates.");
                Assert.That(material.GetFloat("_IceAmount"),Is.InRange(0,1));
            }
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(root+"FrozenWater.mat").GetFloat("_IceAmount"),Is.EqualTo(1));
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(root+"ClearRiver.mat").GetFloat("_IceAmount"),Is.Zero);
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(root+"ClearRiver.mat").GetFloat("_FlowSpeed"),Is.GreaterThan(0));
        }
    }
}
