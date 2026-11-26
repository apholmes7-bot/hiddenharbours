using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The calm mirror ships on at the owner's 0.6, and 0 is still exactly the old water.</b>
    ///
    /// <para>The owner's dial <see cref="GameConfig.WaterMirrorStrength"/> defaults to 0.6 (approved
    /// 2026-11-26). At 0 nothing the mirror owns is published (both globals read their unset 0), no pass is
    /// recorded and no camera is touched. Above 0 the mirror takes over the water's object reflection share
    /// on calm water, so a hull never reflects twice, and gives it back as the sea gets up.</para>
    /// </summary>
    public class WaterCalmMirrorTests
    {
        const string ConfigAsset = "Assets/_Project/Data/Config/GameConfig.asset";
        const string WaterShader = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        static WaterMirrorSettings S => WaterMirrorSettings.Default;

        [Test]
        public void Dial_DefaultsToTheOwners06_InCodeAndInTheShippedAsset()
        {
            Assert.AreEqual(0.6f, GameConfig.DefaultWaterMirrorStrength);
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try { Assert.AreEqual(0.6f, config.WaterMirrorStrength, "a new config ships the mirror on at 0.6"); }
            finally { Object.DestroyImmediate(config); }
            GameConfig previous = GameServices.Config;
            try
            {
                GameServices.Config = null;
                Assert.AreEqual(0.6f, GameServices.WaterMirrorStrength, 1e-6f, "no config reads the ship default");
            }
            finally { GameServices.Config = previous; }
            string yaml = File.ReadAllText(ConfigAsset);
            Match m = Regex.Match(yaml, @"^\s*WaterMirrorStrength:\s*([-0-9.eE]+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, "the shipped GameConfig carries the dial");
            Assert.AreEqual(0.6f, float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                "the shipped GameConfig has the mirror on at the owner's 0.6 (approved 2026-11-26)");
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

        // The dial turned to 0 by hand: the default is now 0.6, so the "0 is the old water" tests install a
        // config that says 0 rather than leaning on the default.
        static GameConfig InstallDialZero(out GameConfig previous)
        {
            previous = GameServices.Config;
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.WaterMirrorStrength = 0f;
            GameServices.Config = config;
            return config;
        }

        static void Uninstall(GameConfig config, GameConfig previous)
        {
            GameServices.Config = previous;
            Object.DestroyImmediate(config);
        }

        [Test]
        public void StrengthZero_PublishesNothing_AndRecordsNothing()
        {
            GameConfig config = InstallDialZero(out GameConfig previous);
            try
            {
                Assert.AreEqual(0f, GameServices.WaterMirrorStrength);
                Shader.SetGlobalFloat(CalmMirror.HandoverId, 0f);
                Shader.SetGlobalFloat(CalmMirror.MeshTagId, 0f);
                CalmMirror.AddTo(null, null);   // a null renderer would throw if a pass were enqueued
                Assert.IsFalse(CalmMirror.Active);
                Assert.AreEqual(0f, Shader.GetGlobalFloat(CalmMirror.HandoverId), "the water keeps its whole share");
                Assert.AreEqual(0f, Shader.GetGlobalFloat(CalmMirror.MeshTagId), "the overlay writes alpha 1, as before");
            }
            finally { Uninstall(config, previous); }
        }

        [Test]
        public void StrengthZero_LowersAStaleValue()
        {
            GameConfig config = InstallDialZero(out GameConfig previous);
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
                Uninstall(config, previous);
            }
        }

        [Test]
        public void DialOn_ButNoGameCamera_StillPublishesNothing()
        {
            // At the shipped 0.6 the mirror only runs on an orthographic game camera with a real device;
            // a null camera (and an editor or preview camera) is left exactly as the old water.
            GameConfig previous = GameServices.Config;
            GameServices.Config = null;
            Shader.SetGlobalFloat(CalmMirror.HandoverId, 0.7f);
            Shader.SetGlobalFloat(CalmMirror.MeshTagId, 1f);
            try
            {
                Assert.AreEqual(0.6f, GameServices.WaterMirrorStrength, 1e-6f);
                CalmMirror.AddTo(null, null);
                Assert.IsFalse(CalmMirror.Active);
                Assert.AreEqual(0f, Shader.GetGlobalFloat(CalmMirror.HandoverId));
                Assert.AreEqual(0f, Shader.GetGlobalFloat(CalmMirror.MeshTagId));
            }
            finally
            {
                Shader.SetGlobalFloat(CalmMirror.HandoverId, 0f);
                Shader.SetGlobalFloat(CalmMirror.MeshTagId, 0f);
                GameServices.Config = previous;
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
        public void MirrorPass_AsksForAnIntermediateTexture_AndNeverDescribesTheBackBuffer()
        {
            // Without an intermediate texture the active colour is the back buffer or the camera's own target,
            // which has no descriptor: reading one throws inside the render graph and the camera loses its frame.
            string src = File.ReadAllText("Assets/_Project/Code/Art/CalmMirror.cs");
            StringAssert.Contains("requiresIntermediateTexture = true", src);
            StringAssert.Contains("resources.isActiveTargetBackBuffer) return;", src);
            Assert.Less(src.IndexOf("isActiveTargetBackBuffer", System.StringComparison.Ordinal),
                        src.IndexOf("graph.GetTextureDesc(target)", System.StringComparison.Ordinal),
                        "the back buffer is turned away before its descriptor is read");
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

        // ---- Decks: a top faces the sky and is never mirrored (slice 3) ----

        sealed class FakeDeck : IStandableSurface, IStandableOutline
        {
            public Vector2[] Corners;
            public float[] Elevations;
            public string Id => "test.deck";
            public bool TryGetDeckElevation(Vector2 worldPos, out float deckElevation) { deckElevation = 0f; return false; }
            public int GetDrawnOutline(Vector2[] drawn, float[] deckElevation)
            {
                int n = Mathf.Min(Corners.Length, drawn.Length);
                for (int i = 0; i < n; i++) { drawn[i] = Corners[i]; deckElevation[i] = Elevations[i]; }
                return n;
            }
        }

        sealed class FootingOnly : IStandableSurface
        {
            public string Id => "test.footing";
            public bool TryGetDeckElevation(Vector2 worldPos, out float deckElevation) { deckElevation = 1f; return true; }
        }

        static FakeDeck RectDeck(float x, float y, float w, float h, float elevation)
        {
            var d = new FakeDeck { Corners = new Vector2[4], Elevations = new float[4] };
            StandableOutline.Rect(new Rect(x, y, w, h), 0f, elevation, d.Corners, d.Elevations);
            return d;
        }

        static readonly Rect View = new Rect(-50f, -50f, 100f, 100f);

        static int Pack(IStandableSurface[] surfaces, float water, out Vector4[] a, out Vector4[] b, out Vector4[] h, int max = CalmMirror.MaxTops)
        {
            a = new Vector4[max]; b = new Vector4[max]; h = new Vector4[max];
            return CalmMirrorMath.PackTops(surfaces, View, water, new Vector2[16], new float[16], a, b, h);
        }

        [Test]
        public void Decks_Settings_ShipOn_WithAUsableFace()
        {
            Assert.IsTrue(S.DeckAware, "tops are never mirrored, by default");
            Assert.Greater(S.FaceSnapMetres, 0f);
            Assert.That(S.FaceFeatherMetres, Is.InRange(0f, 1f));
            Assert.That(S.FaceBandShare, Is.InRange(0f, 1f));
            Assert.Less(S.FaceBandColour.grayscale, 0.5f, "a face in its own shade reads darker than the deck");
            string yaml = File.ReadAllText(ConfigAsset);
            Assert.IsTrue(Regex.IsMatch(yaml, @"^\s*DeckAware:\s*1\s*$", RegexOptions.Multiline), "the shipped GameConfig turns decks on");
            foreach (string key in new[] { "FaceSnapMetres", "FaceFeatherMetres", "FaceBandColour", "FaceBandShare" })
                Assert.IsTrue(Regex.IsMatch(yaml, $@"^\s*{key}:", RegexOptions.Multiline), $"the shipped GameConfig carries {key}");
        }

        [Test]
        public void Outline_Rect_LiftsEveryCorner_AndCarriesTheDeck()
        {
            var drawn = new Vector2[4]; var e = new float[4];
            Assert.AreEqual(4, StandableOutline.Rect(new Rect(10f, 20f, 4f, 2f), 0.5f, 1.2f, drawn, e));
            Assert.AreEqual(new Vector2(10f, 20.5f), drawn[0]);
            Assert.AreEqual(new Vector2(14f, 20.5f), drawn[1]);
            Assert.AreEqual(new Vector2(14f, 22.5f), drawn[2]);
            Assert.AreEqual(new Vector2(10f, 22.5f), drawn[3]);
            foreach (float v in e) Assert.AreEqual(1.2f, v);
            Assert.AreEqual(0, StandableOutline.Rect(new Rect(0, 0, 1, 1), 0f, 0f, new Vector2[3], new float[3]), "short buffers write nothing");
            Assert.AreEqual(2f * IsoGround.HeightScale, StandableOutline.Lift(2f), 1e-6f, "the kit's convention");
        }

        [Test]
        public void Outline_Segment_LiftsEachEndByItsOwnDeck()
        {
            var drawn = new Vector2[4]; var e = new float[4];
            Assert.AreEqual(4, StandableOutline.Segment(new Vector2(0f, 10f), 3f, new Vector2(0f, 0f), 1f, 1f, drawn, e));
            float hs = IsoGround.HeightScale;
            Assert.AreEqual(10f + 3f * hs, drawn[0].y, 1e-5f);
            Assert.AreEqual(0f + 1f * hs, drawn[1].y, 1e-5f);
            Assert.AreEqual(2f, Mathf.Abs(drawn[0].x - drawn[3].x), 1e-5f, "full width across");
            Assert.AreEqual(3f, e[0]); Assert.AreEqual(1f, e[1]); Assert.AreEqual(1f, e[2]); Assert.AreEqual(3f, e[3]);
            Assert.AreEqual(0, StandableOutline.Segment(Vector2.one, 0f, Vector2.one, 0f, 1f, drawn, e), "a degenerate brow has no outline");
        }

        [Test]
        public void PackTops_ARect_IsOneQuad_WithItsFreeboardOnScreen()
        {
            int n = Pack(new IStandableSurface[] { RectDeck(0f, 0f, 10f, 2f, 1.5f) }, 0.5f, out var a, out var b, out var h);
            Assert.AreEqual(1, n);
            Assert.AreEqual(new Vector4(0f, 0f, 10f, 0f), a[0]);
            Assert.AreEqual(new Vector4(10f, 2f, 0f, 2f), b[0]);
            Assert.AreEqual(1f * IsoGround.HeightScale, h[0].x, 1e-5f, "1 m of freeboard above the water, drawn at HeightScale");
        }

        [Test]
        public void PackTops_FaceIsNeverNegative_WhenTheWaterIsOverTheDeck()
        {
            Pack(new IStandableSurface[] { RectDeck(0f, 0f, 4f, 2f, 1f) }, 3f, out _, out _, out var h);
            Assert.AreEqual(0f, h[0].x);
        }

        [Test]
        public void PackTops_FaceIsTakenAtTheFrontEdge_TheLowestDrawnCorner()
        {
            var ramp = new FakeDeck
            {
                Corners = new[] { new Vector2(0f, 0f), new Vector2(2f, 0f), new Vector2(2f, 8f), new Vector2(0f, 8f) },
                Elevations = new[] { 0.8f, 0.8f, 3.4f, 3.4f },
            };
            Pack(new IStandableSurface[] { ramp }, 0f, out _, out _, out var h);
            Assert.AreEqual(0.8f * IsoGround.HeightScale, h[0].x, 1e-5f);
        }

        [Test]
        public void PackTops_CullsDecksOutOfView_AndSkipsSurfacesWithNoOutline()
        {
            var surfaces = new IStandableSurface[] { new FootingOnly(), RectDeck(500f, 500f, 4f, 2f, 1f), RectDeck(0f, 0f, 4f, 2f, 1f) };
            int n = Pack(surfaces, 0f, out var a, out _, out _);
            Assert.AreEqual(1, n);
            Assert.AreEqual(0f, a[0].x, "the one in view");
        }

        [Test]
        public void PackTops_FansAPolygon_AndATriangleRepeatsItsLastCorner()
        {
            var hex = new FakeDeck { Corners = new Vector2[6], Elevations = new float[6] };
            for (int i = 0; i < 6; i++) hex.Corners[i] = new Vector2(Mathf.Cos(i * Mathf.PI / 3f), Mathf.Sin(i * Mathf.PI / 3f)) * 3f;
            Assert.AreEqual(2, Pack(new IStandableSurface[] { hex }, 0f, out var a, out var b, out _), "six corners: quads (0,1,2,3) and (0,3,4,5)");
            Assert.AreEqual(new Vector4(hex.Corners[0].x, hex.Corners[0].y, hex.Corners[3].x, hex.Corners[3].y), a[1]);

            var tri = new FakeDeck { Corners = new[] { Vector2.zero, new Vector2(4f, 0f), new Vector2(0f, 4f) }, Elevations = new float[3] };
            Assert.AreEqual(1, Pack(new IStandableSurface[] { tri }, 0f, out _, out b, out _));
            Assert.AreEqual(new Vector4(0f, 4f, 0f, 4f), b[0], "a triangle is a quad with its last corner twice");
        }

        [Test]
        public void PackTops_NeverWritesPastItsBuffers()
        {
            var many = new IStandableSurface[40];
            for (int i = 0; i < many.Length; i++) many[i] = RectDeck(i - 20f, 0f, 0.5f, 0.5f, 1f);
            Assert.AreEqual(CalmMirror.MaxTops, Pack(many, 0f, out _, out _, out _));
            Assert.AreEqual(3, Pack(many, 0f, out _, out _, out _, 3));
        }

        [Test]
        public void CalmMirrorShader_CompilesWithoutError()
        {
            // No material asset uses the hidden shader, so the project's material-driven compile guards never
            // see it: compile its pass here and read the compiler's verdict.
            const string path = "Assets/_Project/Art/Shaders/HiddenHarboursCalmMirror.shader";
            UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceUpdate | UnityEditor.ImportAssetOptions.ForceSynchronousImport);
            var shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.IsNotNull(shader);
            var mat = new Material(shader);
            try
            {
                for (int pass = 0; pass < Mathf.Max(1, mat.passCount); pass++) UnityEditor.ShaderUtil.CompilePass(mat, pass, true);
                var errors = new System.Text.StringBuilder();
                foreach (var msg in UnityEditor.ShaderUtil.GetShaderMessages(shader))
                    if (msg.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                        errors.AppendLine($"{msg.message} (line {msg.line}, {msg.platform})");
                Assert.IsEmpty(errors.ToString(), "the calm mirror shader must compile (a broken one draws magenta water)");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void Shader_DeckArrays_MatchTheCap()
        {
            string src = File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursCalmMirror.shader");
            Match m = Regex.Match(src, @"#define\s+HH_MAX_TOPS\s+(\d+)");
            Assert.IsTrue(m.Success);
            Assert.AreEqual(CalmMirror.MaxTops, int.Parse(m.Groups[1].Value), "CalmMirror packs exactly as many quads as the shader holds");
            foreach (string array in new[] { "_CalmMirrorTopA", "_CalmMirrorTopB", "_CalmMirrorTopH" })
                StringAssert.Contains($"float4 {array}[HH_MAX_TOPS];", src);
        }
    }
}
