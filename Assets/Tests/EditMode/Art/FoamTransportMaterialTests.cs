using System.IO;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public sealed class FoamTransportMaterialTests
    {
        [Test] public void FoamTransport_AllNineMaterialsDefaultToZero()
        {
            string[] materials={"Water","WaterPresets/Water_DeepBlue","WaterPresets/Water_FoggySmother",
                "WaterPresets/Water_GlassyCalm","WaterPresets/Water_NorthAtlantic","WaterPresets/Water_StirredBrown",
                "WaterPresets/Water_StormGrey","WaterPresets/Water_Tropical","WaterPresets/Water_WarmShelter"};
            string[] keys={FoamTransport.StrainName,FoamTransport.CurlName,FoamTransport.CollectionName};
            foreach(string name in materials)
            {
                string path="Assets/_Project/Art/Materials/"+name+".mat";
                string yaml=File.ReadAllText(Path.Combine(Application.dataPath,"..",path));
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsNotNull(material,path);
                foreach(string key in keys)
                {
                    Assert.AreEqual(1,Regex.Matches(yaml,@"(?m)^\s*- "+key+@":\s*0(?:\.0+)?\s*$").Count,path+": explicit serialized "+key);
                    Assert.IsTrue(material.HasProperty(key),path+": property absent");
                    Assert.AreEqual(0,material.GetFloat(key),path+": default changed");
                }
            }
        }

        [Test] public void FoamTransport_StrengthsAreOwnerPolicyNotMood()
        {
            // Use the shipped material's shader name, rather than assuming its ShaderLab namespace.
            var shipped=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Water.mat");
            Assert.IsNotNull(shipped);
            var material=new Material(shipped.shader);
            Vector3 before=FoamInjectionRegistry.TransportStrengths;
            try
            {
                Assert.AreEqual(Vector3.zero,FoamTransport.ReadStrengths(material));
                material.SetFloat(FoamTransport.StrainName,.3f); material.SetFloat(FoamTransport.CurlName,.1f);
                FoamInjectionRegistry.PublishTransportStrengths(FoamTransport.ReadStrengths(material));
                Assert.AreEqual(new Vector3(.3f,.1f,0),FoamInjectionRegistry.TransportStrengths);
                string source=File.ReadAllText(Path.Combine(Application.dataPath,"_Project/Code/Art/WaterSurface.cs"));
                var mood=Regex.Match(source,@"MoodFloatNames\s*=\s*\{([\s\S]*?)\};");
                Assert.IsTrue(mood.Success);
                StringAssert.DoesNotContain("_FoamTransport",mood.Value);
                StringAssert.Contains("FoamTransport.ReadStrengths(live)",source);
            }
            finally { Object.DestroyImmediate(material); FoamInjectionRegistry.PublishTransportStrengths(before); }
        }
    }
}
