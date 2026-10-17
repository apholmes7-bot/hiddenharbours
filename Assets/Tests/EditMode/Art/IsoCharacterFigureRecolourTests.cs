using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A FIGURE PAINTED FROM OTHER RAMPS</b> (character PR 2b, ADR 0051): the renderer's half of the
    /// player's look. The builder hands it one ramp per material; it draws them and nothing else moves.
    ///
    /// <list type="bullet">
    /// <item>A recolour draws the given ramps, rebinds fresh textures and gives the old ones back.</item>
    /// <item>With the ink live, the recoloured one-step ramp is the one bound.</item>
    /// <item>No vertex moves.</item>
    /// <item>Resetting, or configuring again, draws the def's own colours.</item>
    /// <item>Ramps of another shape are refused before anything is touched, and the caller's arrays are
    /// copied.</item>
    /// </list>
    ///
    /// <para>A synthetic two-bone, two-material def. The textures are uploaded without a CPU copy, so
    /// what is drawn is read back from <c>ShownRamps</c> and the material's bindings. That the colours
    /// are the rig's is the builder's (<c>CharacterBuilderOracleTests</c>).</para>
    /// </summary>
    public sealed class IsoCharacterFigureRecolourTests
    {
        private static readonly Color32[] CoatOwn = { C(20), C(60), C(100), C(140) };
        private static readonly Color32[] BootOwn = { C(30) };
        private static readonly Color32[] CoatNew = { C(21), C(61), C(101), C(141) };
        private static readonly Color32[] BootNew = { C(31) };

        private static readonly MethodInfo s_onDestroy =
            typeof(IsoCharacterFigureRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<IsoCharacterFigureRenderer> _figures = new List<IsoCharacterFigureRenderer>();
        private readonly List<Object> _made = new List<Object>();
        private GameConfig _previousConfig;

        [SetUp]
        public void SetUp()
        {
            _previousConfig = GameServices.Config;
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _made.Add(config);
            GameServices.Config = config;
            IsoFacetFigureInk.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (IsoCharacterFigureRenderer figure in _figures)
                if (figure != null) Scrap(figure);
            _figures.Clear();
            GameServices.Config = _previousConfig;
            IsoFacetFigureInk.Reset();
            foreach (Object o in _made)
                if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // =================================================================== the recolour

        [Test]
        public void ARecolour_DrawsTheGivenRamps_OnFreshTextures_AndGivesTheOldOnesBack()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef(ink: false));
            Material mat = DrawnMaterial(figure);
            Texture ramp = mat.GetTexture(IsoFacetShaderIds.RampTex);
            Texture dark = mat.GetTexture(IsoFacetShaderIds.DarkRampTex);
            Assert.IsFalse(figure.Recoloured);

            figure.Recolour(new[] { CoatNew, BootNew });

            Assert.IsTrue(figure.Recoloured);
            AssertShown(figure, CoatNew, BootNew);
            Texture newRamp = mat.GetTexture(IsoFacetShaderIds.RampTex);
            Assert.IsNotNull(newRamp);
            Assert.AreNotSame(ramp, newRamp, "the ramp texture was not rebuilt and rebound");
            Assert.AreEqual("HHCharRampTex", newRamp.name);
            Assert.AreEqual(CoatOwn.Length, newRamp.width, "one texel per step of the longest ramp");
            Assert.AreEqual(2, newRamp.height, "one row per material");
            Assert.AreEqual("HHCharDarkRampTex", mat.GetTexture(IsoFacetShaderIds.DarkRampTex).name);
            Assert.IsTrue(ramp == null && dark == null, "the old ramp textures were leaked, not given back");
        }

        [Test]
        public void WithTheInkLive_TheRecolouredStepRampIsTheOneBound()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef(ink: true));
            Material mat = DrawnMaterial(figure);
            Assert.IsTrue(figure.InkLive, "harness: the ink is not live");
            Texture step = mat.GetTexture(IsoFacetShaderIds.DarkRampTex);

            figure.Recolour(new[] { CoatNew, BootNew });

            Texture bound = mat.GetTexture(IsoFacetShaderIds.DarkRampTex);
            Assert.AreEqual("HHCharStepRampTex", bound.name, "the recolour dropped the ink's one-step ramp");
            Assert.AreNotSame(step, bound, "the ink still draws the old colours' step ramp");
            Assert.IsTrue(figure.InkLive);
            Assert.AreEqual(1, IsoFacetFigureInk.Count, "a recolour moved the figure in the ink registry");
        }

        [Test]
        public void ARecolour_MovesNoVertex()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef(ink: true));
            Assert.IsTrue(figure.SetPose("idle", 0), "harness: the figure did not pose");
            Vector3[] before = Posed(figure);
            Assert.AreEqual(4, before.Length, "harness: the posed mesh is not the def's");

            figure.Recolour(new[] { CoatNew, BootNew });
            Assert.IsTrue(figure.SetPose("idle", 0));

            CollectionAssert.AreEqual(before, Posed(figure), "a colour moved the body");
        }

        [Test]
        public void ResettingTheColours_DrawsTheDefsOwnAgain()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef(ink: true));
            figure.Recolour(new[] { CoatNew, BootNew });

            figure.ResetColours();

            Assert.IsFalse(figure.Recoloured);
            AssertShown(figure, CoatOwn, BootOwn);
            Assert.AreEqual("HHCharStepRampTex", DrawnMaterial(figure).GetTexture(IsoFacetShaderIds.DarkRampTex).name);
        }

        [Test]
        public void ResettingAFigureNeverRecoloured_RebuildsNothing()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef(ink: false));
            Texture ramp = DrawnMaterial(figure).GetTexture(IsoFacetShaderIds.RampTex);

            figure.ResetColours();

            Assert.AreSame(ramp, DrawnMaterial(figure).GetTexture(IsoFacetShaderIds.RampTex));
            Assert.IsFalse(figure.Recoloured);
        }

        [Test]
        public void ConfiguringAgain_DrawsTheDefsOwnColours_AndForgetsTheRecolour()
        {
            CharacterSkinDef def = MakeDef(ink: false);
            IsoCharacterFigureRenderer figure = MakeFigure(def);
            figure.Recolour(new[] { CoatNew, BootNew });

            figure.Configure(def);

            Assert.IsFalse(figure.Recoloured);
            AssertShown(figure, CoatOwn, BootOwn);
        }

        [Test]
        public void TheRecolour_IsACopy_OfTheCallersRamps()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef(ink: false));
            var coat = (Color32[])CoatNew.Clone();

            figure.Recolour(new[] { coat, BootNew });
            coat[0] = C(200);

            AssertShown(figure, CoatNew, BootNew);
        }

        // =================================================================== refused

        [Test]
        public void RampsOfAnotherShape_AreRefused_AndNothingIsTouched()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef(ink: false));
            Texture ramp = DrawnMaterial(figure).GetTexture(IsoFacetShaderIds.RampTex);

            Assert.Throws<ArgumentNullException>(() => figure.Recolour(null));
            var tooFew = Assert.Throws<ArgumentException>(() => figure.Recolour(new[] { CoatNew }));
            StringAssert.Contains("paints 2 materials", tooFew.Message);
            var tooShort = Assert.Throws<ArgumentException>(() => figure.Recolour(new[] { CoatNew, new[] { C(1), C(2) } }));
            StringAssert.Contains("'boot'", tooShort.Message, "the refusal does not name the material");
            Assert.Throws<ArgumentException>(() => figure.Recolour(new[] { CoatNew, null }));

            Assert.IsFalse(figure.Recoloured);
            AssertShown(figure, CoatOwn, BootOwn);
            Assert.AreSame(ramp, DrawnMaterial(figure).GetTexture(IsoFacetShaderIds.RampTex));
        }

        [Test]
        public void ARecolourBeforeConfigure_IsRefused()
        {
            var figure = new GameObject("TestRecolourUnconfigured").AddComponent<IsoCharacterFigureRenderer>();
            _figures.Add(figure);

            Assert.Throws<InvalidOperationException>(() => figure.Recolour(new[] { CoatNew, BootNew }));
            Assert.DoesNotThrow(figure.ResetColours, "a reset with nothing to reset is a no-op");
        }

        // =================================================================== the synthetic def

        private static Color32 C(byte v) => new Color32(v, (byte)(v + 5), (byte)(v + 9), 255);

        private static readonly Vector3[] RestLocal = { Vector3.zero, new Vector3(0f, 0f, 0.9f) };

        private CharacterSkinDef MakeDef(bool ink)
        {
            var bind = new Mesh { name = "HHTestRecolourBind", hideFlags = HideFlags.HideAndDontSave };
            _made.Add(bind);
            bind.SetVertices(new[]
            {
                new Vector3(-0.15f, 0.1f, 0.6f), new Vector3(0.15f, 0.1f, 0.6f),
                new Vector3(0.15f, 0.1f, 1.3f), new Vector3(-0.15f, 0.1f, 1.3f),
            });
            bind.SetNormals(new[] { Vector3.down, Vector3.down, Vector3.down, Vector3.down });
            bind.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            var weights = new BoneWeight[4];
            for (int i = 0; i < 4; i++) weights[i] = new BoneWeight { boneIndex0 = 1, weight0 = 1f };
            bind.boneWeights = weights;
            bind.bindposes = new[] { Matrix4x4.identity, Matrix4x4.Translate(-RestLocal[1]) };
            bind.RecalculateBounds();

            var keys = new CharacterSkinDef.BoneKey[2];
            for (int b = 0; b < 2; b++)
                keys[b] = new CharacterSkinDef.BoneKey { Position = RestLocal[b], Rotation = Quaternion.identity };

            var def = ScriptableObject.CreateInstance<CharacterSkinDef>();
            _made.Add(def);
            def.Id = "charskin.test_recolour";
            def.BindMesh = bind;
            def.Bones = new[]
            {
                new CharacterSkinDef.Bone { Id = "root", Parent = CharacterSkinDef.NoBone, OwnsVertex = false },
                new CharacterSkinDef.Bone { Id = "chest", Parent = 0, OwnsVertex = true },
            };
            def.MaxInfluences = 1;
            def.Materials = new[]
            {
                new CharacterSkinDef.Material
                {
                    Name = "coat", Colors = (Color32[])CoatOwn.Clone(),
                    Offset = 0, Gain = 2.4f, Bias = 1.8f, FixedIndex = -1, ToneLo = 0, ToneHi = 3,
                },
                new CharacterSkinDef.Material
                {
                    Name = "boot", Colors = (Color32[])BootOwn.Clone(),
                    Offset = 0, Gain = 1f, Bias = 0f, FixedIndex = -1, ToneLo = 0, ToneHi = 0,
                },
            };
            var bayer = new float[16];
            for (int i = 0; i < 16; i++) bayer[i] = (i + 0.5f) / 16f;
            def.Bayer16 = bayer;
            def.Keyline = new Color32(16, 26, 25, 255);
            def.LightN = new Vector3(-0.42750444f, 0.73286476f, 0.52929122f);
            def.Gain = 1.2f;
            def.Bias = 1.8f;
            def.CellW = 64;
            def.CellH = 92;
            def.PivotPx = new Vector2(32f, 82f);
            def.PxPerMetre = 32;
            def.ElevationDeg = 40f;
            def.Clips = new[]
            {
                new CharacterSkinDef.SkinClip
                {
                    Anim = "idle", State = "idle", FramesPerSecond = 6f, Loop = true, FrameCount = 1, Keys = keys,
                },
            };
            def.MeshStates = new[] { "idle" };
            def.ToneRule = ToneRule.V9;
            def.KeyScreen = new Vector3(0f, 0.8106792283998809f, 0.5854905538443586f);
            def.Edge = ink ? 0.2f : 0f;
            def.KeylineMix = ink ? 0.3f : 0f;

            Assert.IsTrue(def.IsUsable(), "harness: the synthetic def is not usable");
            Assert.AreEqual(ink, def.HasInk, "harness: the def's ink is not what the test asked for");
            return def;
        }

        private IsoCharacterFigureRenderer MakeFigure(CharacterSkinDef def)
        {
            var figure = new GameObject("TestRecolourFigure").AddComponent<IsoCharacterFigureRenderer>();
            _figures.Add(figure);
            figure.Configure(def);
            return figure;
        }

        private static void AssertShown(IsoCharacterFigureRenderer figure, params Color32[][] want)
        {
            Color32[][] shown = figure.ShownRamps;
            Assert.IsNotNull(shown, "the figure says it draws no ramps");
            Assert.AreEqual(want.Length, shown.Length, "one ramp per material");
            for (int m = 0; m < want.Length; m++)
                CollectionAssert.AreEqual(want[m], shown[m], $"material {m} draws other colours");
        }

        private static Vector3[] Posed(IsoCharacterFigureRenderer figure) =>
            figure.GetComponentInChildren<MeshFilter>(true).sharedMesh.vertices;

        private static Material DrawnMaterial(IsoCharacterFigureRenderer figure) =>
            figure.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;

        /// <summary>EditMode runs no <c>OnDestroy</c>, so the renderer's own teardown is called here.</summary>
        private static void Scrap(IsoCharacterFigureRenderer figure)
        {
            Assert.IsNotNull(s_onDestroy, "harness: the renderer's OnDestroy is where it gives its objects back");
            s_onDestroy.Invoke(figure, null);
            Object.DestroyImmediate(figure.gameObject);
        }
    }
}
