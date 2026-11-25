using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ADR 0061: where the sea cuts the mesh figure. The rule is the rig's own (rig 10 poses: swim
    /// <c>waterZ = Pz + 0.09</c>, tread <c>mean(shoulder z) − 0.05</c>), read from the skin's posed
    /// bones; the margins are GameConfig's.
    /// </summary>
    public sealed class CharacterFigureWaterlineTests
    {
        private const float SwimMargin = 0.09f;
        private const float TreadMargin = 0.05f;

        private static readonly string[] Presets =
            { "fisher", "ginny", "skipper", "nan", "hand", "cutter", "packer", "deckboss", "boy", "girl" };

        [Test]
        public void TheMode_FollowsTheBand_AndTheDrawnClip()
        {
            Assert.AreEqual(FigureWater.Dry, CharacterFigureWaterline.ModeFor(OnFootWaterState.Dry, "swim"));
            Assert.AreEqual(FigureWater.Wade, CharacterFigureWaterline.ModeFor(OnFootWaterState.Wade, "walk"));
            Assert.AreEqual(FigureWater.Swim, CharacterFigureWaterline.ModeFor(OnFootWaterState.Swim, "swim"));
            Assert.AreEqual(FigureWater.Tread, CharacterFigureWaterline.ModeFor(OnFootWaterState.Swim, "tread"));
            Assert.AreEqual(FigureWater.Swim, CharacterFigureWaterline.ModeFor(OnFootWaterState.Swim, "idle"),
                            "in the swim band anything but the tread is taken as prone");
        }

        [Test]
        public void TheLine_FromMeasuredHeights()
        {
            Assert.IsTrue(CharacterFigureWaterline.TryLine(FigureWater.Swim, 0.40f, 1.30f, 0f, SwimMargin, TreadMargin, out float swim));
            Assert.AreEqual(0.49f, swim, 1e-5f);
            Assert.IsTrue(CharacterFigureWaterline.TryLine(FigureWater.Tread, 0.40f, 1.30f, 0f, SwimMargin, TreadMargin, out float tread));
            Assert.AreEqual(1.25f, tread, 1e-5f);
            Assert.IsTrue(CharacterFigureWaterline.TryLine(FigureWater.Wade, 0.95f, 1.30f, 0.6f, SwimMargin, TreadMargin, out float wade));
            Assert.AreEqual(0.6f, wade, 1e-5f);
            Assert.IsTrue(CharacterFigureWaterline.TryLine(FigureWater.Wade, 0.95f, 1.30f, 3.0f, SwimMargin, TreadMargin, out float deep));
            Assert.AreEqual(1.25f, deep, 1e-5f, "a deep wade never rises past the tread line");

            Assert.IsFalse(CharacterFigureWaterline.TryLine(FigureWater.Dry, 0.4f, 1.3f, 1f, SwimMargin, TreadMargin, out _));
            Assert.IsFalse(CharacterFigureWaterline.TryLine(FigureWater.Wade, 0.9f, 1.3f, 0f, SwimMargin, TreadMargin, out _), "dry feet");
            Assert.IsFalse(CharacterFigureWaterline.TryLine(FigureWater.Wade, 0.9f, 1.3f, float.NegativeInfinity, SwimMargin, TreadMargin, out _),
                           "no terrain answers -infinity: no line");
            Assert.IsFalse(CharacterFigureWaterline.TryLine(FigureWater.Swim, float.NaN, 1.3f, 0f, SwimMargin, TreadMargin, out _));
        }

        [TestCaseSource(nameof(Presets))]
        public void OnTheBakedBodies_TheSeaSitsWhereTheRigPutsIt(string preset)
        {
            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(
                $"Assets/_Project/Data/Characters/Skin/{preset}.asset");
            Assert.IsNotNull(skin, preset);
            var world = new Matrix4x4[skin.BoneCount];
            int head = skin.IndexOfBone("head");
            int pelvis = skin.IndexOfBone(CharacterFigureWaterline.PelvisBone);
            Assert.GreaterOrEqual(head, 0);
            Assert.GreaterOrEqual(pelvis, 0);

            Assert.IsTrue(skin.TryGetClip("swim", out CharacterSkinDef.SkinClip swim), preset + ": swim");
            for (int f = 0; f < swim.FrameCount; f++)
            {
                Assert.IsTrue(CharacterFigureWaterline.TryLine(skin, swim, f, world, FigureWater.Swim, 0f,
                                                               SwimMargin, TreadMargin, out float line), $"{preset} swim {f}");
                Assert.AreEqual(world[pelvis].m23 + SwimMargin, line, 1e-5f, $"{preset} swim {f}: pelvis + margin");
                // Rig 10 lays the swimmer with the pelvis at 0.40 m (0.45 m in a skirt), bobbing 0.012 m.
                Assert.That(line, Is.InRange(0.45f, 0.58f), $"{preset} swim {f}");
            }

            Assert.IsTrue(skin.TryGetClip("tread", out CharacterSkinDef.SkinClip tread), preset + ": tread");
            for (int f = 0; f < tread.FrameCount; f++)
            {
                Assert.IsTrue(CharacterFigureWaterline.TryLine(skin, tread, f, world, FigureWater.Tread, 0f,
                                                               SwimMargin, TreadMargin, out float line), $"{preset} tread {f}");
                Assert.Less(line, world[head].m23, $"{preset} tread {f}: the head stays above the sea");
                Assert.Greater(line, world[pelvis].m23, $"{preset} tread {f}: the body is under it");
            }

            Assert.IsFalse(CharacterFigureWaterline.TryLine(skin, swim, 0, world, FigureWater.Dry, 0f,
                                                            SwimMargin, TreadMargin, out _), "dry: no line");
            Assert.IsFalse(CharacterFigureWaterline.TryLine(skin, swim, 0, new Matrix4x4[1], FigureWater.Swim, 0f,
                                                            SwimMargin, TreadMargin, out _), "a scratch too short: no line");
        }

        [Test]
        public void TheWaterLook_IsTheHardCutWithNoConfig_AndTheConfigsOtherwise()
        {
            FigureWaterLook none = FigureWaterLook.FromConfig(null);
            Assert.IsFalse(none.ShowsUnderwater, "no config: nothing drawn under the line");
            Assert.AreEqual(0f, none.FoamMetres, "no config: no foam");

            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                config.FigureShowsUnderwater = true;
                config.FigureUnderwaterTint = new Color(0.1f, 0.2f, 0.3f, 1f);
                config.FigureUnderwaterTintAmount = 0.4f;
                config.FigureUnderwaterDim = 0.25f;
                config.FigureWaterlineFoamMetres = 0.02f;
                config.FigureWaterlineFoam = 0.7f;
                FigureWaterLook look = FigureWaterLook.FromConfig(config);
                Assert.IsTrue(look.ShowsUnderwater);
                Assert.AreEqual(config.FigureUnderwaterTint, look.Tint);
                Assert.AreEqual(0.4f, look.TintAmount);
                Assert.AreEqual(0.25f, look.Dim);
                Assert.AreEqual(0.02f, look.FoamMetres);
                Assert.AreEqual(0.7f, look.Foam);
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void Wading_TakesItsOwnFoamBand_EveryOtherModeTheSwimBand()
        {
            Assert.AreEqual(0f, FigureWaterLook.FromConfig(null, FigureWater.Wade).FoamMetres, "no config: the hard cut");

            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                config.FigureUnderwaterDim = 0.25f;
                config.FigureWaterlineFoamMetres = 0.02f;
                config.FigureWaterlineFoam = 0.4f;
                config.FigureWadeWaterlineFoamMetres = 0.11f;
                config.FigureWadeWaterlineFoam = 0.9f;
                FigureWaterLook wade = FigureWaterLook.FromConfig(config, FigureWater.Wade);
                Assert.AreEqual(0.11f, wade.FoamMetres);
                Assert.AreEqual(0.9f, wade.Foam);
                Assert.AreEqual(0.25f, wade.Dim, "the rest of the look is the shared one");
                foreach (FigureWater mode in new[] { FigureWater.Swim, FigureWater.Tread, FigureWater.Dry })
                {
                    FigureWaterLook look = FigureWaterLook.FromConfig(config, mode);
                    Assert.AreEqual(0.02f, look.FoamMetres, mode.ToString());
                    Assert.AreEqual(0.4f, look.Foam, mode.ToString());
                }
            }
            finally { Object.DestroyImmediate(config); }
        }

        [TestCaseSource(nameof(Presets))]
        public void TheShippedWadeLine_IsAtLeastTwoPixelsTall_OnEveryBody(string preset)
        {
            // Wading he stands upright, so the foam band over the line is seen edge-on: its screen
            // height is metres x the skin's pixels per metre x cos(elevation). The swim band's 0.03 m
            // came to 0.74 px on the shipped bodies and the line at the surface did not draw.
            var shipped = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>($"Assets/_Project/Data/Characters/Skin/{preset}.asset");
            Assert.IsNotNull(shipped, "the shipped GameConfig");
            Assert.IsNotNull(skin, preset);
            FigureWaterLook wade = FigureWaterLook.FromConfig(shipped, FigureWater.Wade);
            float px = wade.FoamMetres * skin.PxPerMetre * Mathf.Cos(skin.ElevationDeg * Mathf.Deg2Rad);
            Assert.GreaterOrEqual(px, 2f, $"{preset}: the wade foam band is {px:0.00} px tall");
            Assert.GreaterOrEqual(wade.Foam, 0.5f, $"{preset}: the line at the surface is pale enough to read");
            Assert.IsTrue(wade.ShowsUnderwater, $"{preset}: his legs under the line still draw, tinted");
        }

        [Test]
        public void TheShippedConfig_DrawsHimInTheWater_AndQuietensTheSwimShadow()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(shipped, "the shipped GameConfig");
            Assert.IsTrue(shipped.FigureShowsUnderwater, "the submerged body draws, tinted");
            Assert.Greater(shipped.FigureUnderwaterTintAmount, 0f);
            Assert.Greater(shipped.FigureWaterlineFoamMetres, 0f, "a soft waterline, not a hard cut");
            Assert.IsTrue(shipped.FigureSwimHidesSunShadow, "no upright ghost shadow on the water");
        }
    }
}
