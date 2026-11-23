using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The calm mirror ships off, and off is today's water.</b>
    ///
    /// <para>The owner's dial <see cref="GameConfig.WaterMirrorStrength"/> defaults to 0. At 0 nothing the
    /// mirror owns is published (both globals read their unset 0), no pass is recorded and no camera is
    /// touched. Above 0 the mirror takes over the water's object reflection share on calm water, so a hull
    /// never reflects twice, and gives it back as the sea gets up.</para>
    /// </summary>
    public class WaterCalmMirrorTests
    {
        const string ConfigAsset = "Assets/_Project/Data/Config/GameConfig.asset";
        const string WaterShader = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        static WaterMirrorSettings S => WaterMirrorSettings.Default;

        [Test]
        public void Dial_DefaultsToZero_InCodeAndInTheShippedAsset()
        {
            Assert.AreEqual(0f, GameConfig.DefaultWaterMirrorStrength);
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try { Assert.AreEqual(0f, config.WaterMirrorStrength, "a new config ships the mirror off"); }
            finally { Object.DestroyImmediate(config); }
            string yaml = File.ReadAllText(ConfigAsset);
            Match m = Regex.Match(yaml, @"^\s*WaterMirrorStrength:\s*([-0-9.eE]+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, "the shipped GameConfig carries the dial");
            Assert.AreEqual(0f, float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                "the shipped GameConfig keeps the mirror off (the owner turns it on)");
        }

        [Test]
        public void Settings_Defaults_AreUsable()
        {
            Assert.Less(S.SeaFadeStart, S.SeaFadeEnd);
            Assert.Less(S.ReachStartMetres, S.ReachEndMetres);
            Assert.Greater(S.MaxMetres, 0f);
            Assert.GreaterOrEqual(S.StepPixels, 1f);
            Assert.GreaterOrEqual(S.RippleBandPixels, 1f);
            Assert.That(S.Darken, Is.InRange(0f, 1f));
        }

        [Test]
        public void Handover_IsZero_WhileTheDialIsZero_AtEverySea()
        {
            for (float sea = 0f; sea <= 1f; sea += 0.05f)
            {
                Assert.AreEqual(0f, CalmMirrorMath.Handover(0f, sea, S), $"sea {sea}");
                Assert.AreEqual(0f, CalmMirrorMath.EffectiveStrength(0f, sea, S), $"sea {sea}");
            }
        }

        [Test]
        public void Handover_TakesTheWholeShare_OnCalmWater_AndGivesItBack_WhenRough()
        {
            Assert.AreEqual(1f, CalmMirrorMath.Handover(0.6f, 0f, S), 1e-6f, "calm: the water keeps none, no doubling");
            Assert.AreEqual(1f, CalmMirrorMath.Handover(0.6f, S.SeaFadeStart, S), 1e-6f);
            Assert.AreEqual(0f, CalmMirrorMath.Handover(0.6f, S.SeaFadeEnd, S), 1e-6f, "rough: the water has it all back");
            Assert.AreEqual(0f, CalmMirrorMath.Handover(0.6f, 1f, S), 1e-6f);
            Assert.AreEqual(0.6f, CalmMirrorMath.EffectiveStrength(0.6f, 0f, S), 1e-6f, "the owner's 0.6 on calm water");
            Assert.AreEqual(0f, CalmMirrorMath.EffectiveStrength(0.6f, S.SeaFadeEnd, S), 1e-6f);
        }

        [Test]
        public void Handover_And_Strength_FadeTogether()
        {
            // Wherever the water hands its share over, the mirror is laid at the same share of the dial: one
            // fade drives both, so the two never overlap and never leave a gap.
            float last = 2f;
            for (float sea = 0f; sea <= 1f; sea += 0.025f)
            {
                float h = CalmMirrorMath.Handover(0.6f, sea, S);
                Assert.AreEqual(0.6f * h, CalmMirrorMath.EffectiveStrength(0.6f, sea, S), 1e-6f, $"sea {sea}");
                Assert.LessOrEqual(h, last + 1e-6f, "never rises as the sea gets up");
                last = h;
            }
        }

        [Test]
        public void Dial_IsClamped()
        {
            Assert.AreEqual(1f, CalmMirrorMath.EffectiveStrength(3f, 0f, S), 1e-6f);
            Assert.AreEqual(0f, CalmMirrorMath.EffectiveStrength(-1f, 0f, S), 1e-6f);
            Assert.AreEqual(0f, CalmMirrorMath.Handover(-1f, 0f, S), "a negative dial is off");
        }

        [Test]
        public void StrengthZero_PublishesNothing_AndRecordsNothing()
        {
            // No config is installed in EditMode: the dial reads its default, 0.
            Assert.AreEqual(0f, GameServices.WaterMirrorStrength);
            Shader.SetGlobalFloat(CalmMirror.HandoverId, 0f);
            Shader.SetGlobalFloat(CalmMirror.MeshTagId, 0f);
            CalmMirror.AddTo(null, null);   // a null renderer would throw if a pass were enqueued
            Assert.IsFalse(CalmMirror.Active);
            Assert.AreEqual(0f, Shader.GetGlobalFloat(CalmMirror.HandoverId), "the water keeps its whole share");
            Assert.AreEqual(0f, Shader.GetGlobalFloat(CalmMirror.MeshTagId), "the overlay writes alpha 1, as before");
        }

        [Test]
        public void StrengthZero_LowersAStaleValue()
        {
            Shader.SetGlobalFloat(CalmMirror.HandoverId, 0.7f);
            Shader.SetGlobalFloat(CalmMirror.MeshTagId, 1f);
            try
            {
                CalmMirror.AddTo(null, null);
                Assert.AreEqual(0f, Shader.GetGlobalFloat(CalmMirror.HandoverId));
                Assert.AreEqual(0f, Shader.GetGlobalFloat(CalmMirror.MeshTagId));
            }
            finally
            {
                Shader.SetGlobalFloat(CalmMirror.HandoverId, 0f);
                Shader.SetGlobalFloat(CalmMirror.MeshTagId, 0f);
            }
        }

        [Test]
        public void WaterShader_HandsOver_OnlyItsObjectReflectionShare_AndUnsetIsTimesOne()
        {
            string src = File.ReadAllText(WaterShader);
            StringAssert.Contains("float _HHCalmMirrorHandover;", src);
            StringAssert.Contains("strength *= 1.0 - saturate(_HHCalmMirrorHandover);", src,
                "unset reads 0, so today's strength is multiplied by exactly 1");
            Assert.AreEqual(1, Regex.Matches(src, @"_HHCalmMirrorHandover\)").Count, "one place only: no double handover");
        }

        [Test]
        public void GroundCover_LeavesTheAlphaClassAlone()
        {
            string[] ground =
            {
                "AdditiveLight", "Flower", "Grass", "HullSunShadow", "LampPool", "LampShadow", "PlantShadow",
                "SpriteShadow", "TerrainSplat",
            };
            foreach (string name in ground)
            {
                string src = File.ReadAllText($"Assets/_Project/Art/Shaders/HiddenHarbours{name}.shader");
                StringAssert.Contains("ColorMask RGB", src, $"{name} must not write the alpha the mirror reads");
            }
        }

        [Test]
        public void CalmMirrorShader_IsIncludedInBuilds()
        {
            string meta = File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursCalmMirror.shader.meta");
            string guid = Regex.Match(meta, @"guid:\s*([0-9a-f]{32})").Groups[1].Value;
            Assert.IsNotEmpty(guid);
            StringAssert.Contains(guid, File.ReadAllText("ProjectSettings/GraphicsSettings.asset"),
                "Shader.Find needs the hidden shader in Always Included Shaders");
            Assert.IsNotNull(Shader.Find(CalmMirror.ShaderName));
        }
    }
}
