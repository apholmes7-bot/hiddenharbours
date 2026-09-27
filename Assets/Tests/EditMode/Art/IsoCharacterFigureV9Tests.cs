using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A V9 FIGURE GETS HER OWN VARIANT; A RIG 7 FIGURE AND EVERY HULL DO NOT.</b>
    ///
    /// <para>A <see cref="ToneRule.V9"/> def turns <c>HH_FIGURE</c> on for the figure's OWN material
    /// instance and writes the variant's two tables at their full thirty-two, whatever it carries: the
    /// effective gain (<c>def.Gain · m.Gain</c>), the folded bias, the window and the offset, with unused
    /// slots padded. Its <c>_LN</c> is the folded key, not <c>LightN</c>. A rig 7 def keeps today's
    /// program: no keyword, sixteen slots, the global pair. Nothing here touches a hull's material or a
    /// global keyword. <c>TheFigureMaterialMatchesAHullConfiguredFromTheSameSetup</c> still holds rig 7
    /// to the hull, unchanged.</para>
    ///
    /// <para><b>No GPU is used or needed</b>, as in <see cref="IsoCharacterFigureRendererTests"/>: the
    /// material and its tables are built on the CPU and read back. What the variant DRAWS is the GPU plate
    /// against the kit's goldens, which is owed to an editor slot.</para>
    /// </summary>
    public sealed class IsoCharacterFigureV9Tests
    {
        private const int Bones = 3;

        // The kit's globals, as TEST data (see IsoFacetFigureToneTests): the def carries them as data.
        private static readonly Vector3 Key = new Vector3(0f, 0.8106792283998809f, 0.5854905538443586f);
        private const float Form = 0.5f;
        private const float FormMid = 0.45f;
        // The def's global pair. Every material below carries Gain 2, so its effective gain is the kit's
        // 2.4; a material with no bias of its own (NaN) takes this Bias, the kit's T5 1.8.
        private const float DefGain = 1.2f;
        private const float DefBias = 1.8f;

        private readonly List<IsoCharacterFigureRenderer> _figures = new List<IsoCharacterFigureRenderer>();
        private readonly List<UnityEngine.Object> _made = new List<UnityEngine.Object>();
        private GameObject _hullGo;

        [TearDown]
        public void TearDown()
        {
            foreach (IsoCharacterFigureRenderer figure in _figures)
                if (figure != null) Scrap(figure);
            _figures.Clear();
            if (_hullGo != null) UnityEngine.Object.DestroyImmediate(_hullGo);
            _hullGo = null;
            foreach (UnityEngine.Object o in _made)
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _made.Clear();
        }

        // =================================================================== the synthetic defs

        private static Color32[] Ramp(int length, byte hue)
        {
            var ramp = new Color32[length];
            for (int i = 0; i < length; i++)
                ramp[i] = new Color32((byte)(10 + 40 * i), hue, (byte)(20 + 30 * i), 255);
            return ramp;
        }

        /// <summary>Three kinds of v9 material, in turn by index: a T6 ramp with its own bias, a T5 dark
        /// alias (offset −1) on the def's bias, and a fixed one-colour ink.</summary>
        private static CharacterSkinDef.Material MaterialAt(int m)
        {
            switch (m % 3)
            {
                case 0:
                    return new CharacterSkinDef.Material
                    {
                        Name = "t6_" + m, Colors = Ramp(6, 60), Offset = 0, Gain = 2f, Bias = 2.7f,
                        FixedIndex = -1, ToneLo = 2, ToneHi = 5,
                    };
                case 1:
                    return new CharacterSkinDef.Material
                    {
                        Name = "t5D_" + m, Colors = Ramp(5, 120), Offset = -1, Gain = 2f, Bias = float.NaN,
                        FixedIndex = -1, ToneLo = 1, ToneHi = 4,
                    };
                default:
                    return new CharacterSkinDef.Material
                    {
                        Name = "ink_" + m, Colors = Ramp(1, 180), Offset = 0, Gain = 2f, Bias = 0f,
                        FixedIndex = -1, ToneLo = 0, ToneHi = 0,
                    };
            }
        }

        /// <summary>
        /// What the renderer must write for <see cref="MaterialAt"/>(m), worked by hand. The effective
        /// gain is 1.2 · 2 = 2.4 for all three, so gain·form·formMid = 2.4 · 0.5 · 0.45 = 0.54 and:
        ///   T6:  meta (6, 0, 2, 5),  bias' = 2.7 − 0.54 = 2.16
        ///   T5D: meta (5, −1, 1, 4), bias' = 1.8 (the def's, through BiasOr(NaN)) − 0.54 = 1.26
        ///   ink: meta (1, 0, 0, 0),  bias' = 0 − 0.54 = −0.54 (a fixed material ignores it: its window is 0..0)
        /// </summary>
        private static void Expected(int m, out Vector4 meta, out float bias)
        {
            switch (m % 3)
            {
                case 0: meta = new Vector4(6f, 0f, 2f, 5f); bias = 2.16f; return;
                case 1: meta = new Vector4(5f, -1f, 1f, 4f); bias = 1.26f; return;
                default: meta = new Vector4(1f, 0f, 0f, 0f); bias = -0.54f; return;
            }
        }

        private static float[] Bayer()
        {
            var b = new float[16];
            for (int i = 0; i < 16; i++) b[i] = (i + 0.5f) / 16f;
            return b;
        }

        /// <summary>The same skinned quad <see cref="IsoCharacterFigureRendererTests"/> draws.</summary>
        private Mesh BuildBindMesh()
        {
            var bind = new Mesh { name = "HHTestV9Bind", hideFlags = HideFlags.HideAndDontSave };
            _made.Add(bind);
            bind.SetVertices(new[]
            {
                new Vector3(-0.2f, 0f, 0f), new Vector3(0.2f, 0f, 0f),
                new Vector3(0.2f, 0f, 1.6f), new Vector3(-0.2f, 0f, 1.6f),
            });
            bind.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            bind.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            bind.boneWeights = new[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f, boneIndex1 = 0, weight1 = 0f },
                new BoneWeight { boneIndex0 = 0, weight0 = 1f, boneIndex1 = 0, weight1 = 0f },
                new BoneWeight { boneIndex0 = 1, weight0 = 0.5f, boneIndex1 = 2, weight1 = 0.5f },
                new BoneWeight { boneIndex0 = 1, weight0 = 0.5f, boneIndex1 = 2, weight1 = 0.5f },
            };
            bind.bindposes = new[]
            {
                Matrix4x4.identity,
                Matrix4x4.TRS(new Vector3(0f, 0f, -0.8f), Quaternion.identity, Vector3.one),
                Matrix4x4.TRS(new Vector3(0f, 0f, -1.6f), Quaternion.identity, Vector3.one),
            };
            bind.RecalculateBounds();
            return bind;
        }

        private static CharacterSkinDef.SkinClip Idle()
        {
            const int frames = 2;
            var keys = new CharacterSkinDef.BoneKey[frames * Bones];
            for (int f = 0; f < frames; f++)
                for (int b = 0; b < Bones; b++)
                    keys[f * Bones + b] = new CharacterSkinDef.BoneKey
                    {
                        Position = new Vector3(0f, 0f, 0.8f * b + 0.01f * f),
                        Rotation = Quaternion.identity,
                    };
            return new CharacterSkinDef.SkinClip
            {
                Anim = "idle", State = "idle", FramesPerSecond = 12f, Loop = true, FrameCount = frames, Keys = keys,
            };
        }

        private CharacterSkinDef MakeDef(ToneRule rule, int materials)
        {
            var def = ScriptableObject.CreateInstance<CharacterSkinDef>();
            _made.Add(def);
            def.Id = "charskin.test_v9_figure";
            def.BindMesh = BuildBindMesh();
            def.Bones = new[]
            {
                new CharacterSkinDef.Bone { Id = "root", Parent = CharacterSkinDef.NoBone, OwnsVertex = true },
                new CharacterSkinDef.Bone { Id = "spine", Parent = 0, OwnsVertex = true },
                new CharacterSkinDef.Bone { Id = "head", Parent = 1, OwnsVertex = true },
            };
            def.MaxInfluences = 2;
            def.Materials = new CharacterSkinDef.Material[materials];
            for (int m = 0; m < materials; m++) def.Materials[m] = MaterialAt(m);
            def.Bayer16 = Bayer();
            def.Keyline = new Color32(16, 26, 25, 255);
            // Rig 7's light: the fleet key. A v9 def carries it too and must NOT draw with it.
            def.LightN = new Vector3(-0.42750444f, 0.73286476f, 0.52929122f);
            def.Gain = DefGain;
            def.Bias = DefBias;
            def.CellW = 64;
            def.CellH = 92;
            def.PivotPx = new Vector2(32f, 82f);
            def.PxPerMetre = 32;
            def.ElevationDeg = 40f;
            def.Clips = new[] { Idle() };
            def.MeshStates = new[] { "idle" };
            def.ToneRule = rule;
            def.KeyScreen = Key;
            def.Form = Form;
            def.FormMid = FormMid;

            Assert.IsTrue(def.IsUsable(),
                "the synthetic def this fixture is built on does not satisfy IsUsable(), so every assertion " +
                "below would be measuring the fixture rather than the renderer");
            return def;
        }

        private IsoCharacterFigureRenderer MakeFigure(CharacterSkinDef def)
        {
            var go = new GameObject("TestV9Figure");
            var figure = go.AddComponent<IsoCharacterFigureRenderer>();
            _figures.Add(figure);
            figure.Configure(def);
            return figure;
        }

        // =================================================================== v9

        [Test]
        public void AV9Figure_TurnsHHFigureOnItsOwnMaterial_AndWritesBothTablesThirtyTwoWide()
        {
            const int materials = 20;   // past rig 7's sixteen, short of the thirty-two
            Material mat = DrawnMaterial(MakeFigure(MakeDef(ToneRule.V9, materials)).transform, "v9 figure");

            Assert.IsTrue(mat.IsKeywordEnabled(IsoFacetFigureShaderIds.FigureKeyword),
                "a v9 figure's material does not have HH_FIGURE on: she would draw with rig 7's program");
            Assert.IsFalse(mat.IsKeywordEnabled(IsoFacetShaderIds.LevelGateKeyword),
                "a figure carries no room; the level gate belongs to hulls");

            Vector4[] meta = mat.GetVectorArray(IsoFacetFigureShaderIds.RampMetaFigure);
            Vector4[] tone = mat.GetVectorArray(IsoFacetFigureShaderIds.RampToneFigure);
            Assert.IsNotNull(meta, "the figure wrote no _RampMetaFigure");
            Assert.IsNotNull(tone, "the figure wrote no _RampToneFigure");
            Assert.AreEqual(CharacterSkinDef.V9RampSlots, meta.Length,
                "_RampMetaFigure must be written at its full length every time: Unity fixes an array's " +
                "size at its first set");
            Assert.AreEqual(CharacterSkinDef.V9RampSlots, tone.Length, "_RampToneFigure, likewise");

            for (int m = 0; m < materials; m++)
            {
                Expected(m, out Vector4 wantMeta, out float wantBias);
                Assert.AreEqual(wantMeta, meta[m], $"_RampMetaFigure[{m}] = (len, off, lo, hi)");
                Assert.AreEqual(2.4f, tone[m].x, 1e-5f, $"_RampToneFigure[{m}].x: the effective gain, 1.2 · 2");
                Assert.AreEqual(wantBias, tone[m].y, 1e-5f, $"_RampToneFigure[{m}].y: the folded bias");
                Assert.AreEqual(0f, tone[m].z, $"_RampToneFigure[{m}].z");
                Assert.AreEqual(0f, tone[m].w, $"_RampToneFigure[{m}].w");
            }
            for (int m = materials; m < CharacterSkinDef.V9RampSlots; m++)
            {
                Assert.AreEqual(new Vector4(1f, 0f, 0f, 0f), meta[m], $"padding _RampMetaFigure[{m}]: a one-colour ramp");
                Assert.AreEqual(Vector4.zero, tone[m], $"padding _RampToneFigure[{m}]: zero gain and bias");
            }

            Texture ramp = mat.GetTexture(IsoFacetShaderIds.RampTex);
            Assert.IsNotNull(ramp, "the figure wrote no _RampTex");
            Assert.AreEqual(materials, ramp.height, "one ramp row per material, past rig 7's sixteen");
            Assert.AreEqual(6, ramp.width, "as wide as the longest ramp");
        }

        [Test]
        public void AV9Figure_LightsWithTheFoldedKey_NotWithLightN()
        {
            Material mat = DrawnMaterial(MakeFigure(MakeDef(ToneRule.V9, 3)).transform, "v9 figure");

            // ShaderLightVector(FoldLight(key, 0.5)) = (0, 0.8106792, −(0.5854906 + 0.5), 0)
            //                                        = (0, 0.8106792, −1.0854906, 0):
            // the folded key with z negated for the reflected frame, not turned by the elevation, and not
            // the fleet key this def also carries in LightN for rig 7.
            Vector4 ln = mat.GetVector(IsoFacetShaderIds.LightN);
            Assert.AreEqual(0f, ln.x, 1e-6f, "_LN.x");
            Assert.AreEqual(0.8106792f, ln.y, 1e-6f, "_LN.y");
            Assert.AreEqual(-1.0854906f, ln.z, 1e-6f, "_LN.z");
            Assert.AreEqual(0f, ln.w, "_LN.w");
        }

        [Test]
        public void AV9FigureAtItsCap_DrawsAllThirtyTwoMaterials()
        {
            Material mat = DrawnMaterial(MakeFigure(MakeDef(ToneRule.V9, CharacterSkinDef.V9RampSlots)).transform,
                                         "v9 figure");

            Assert.AreEqual(CharacterSkinDef.V9RampSlots, mat.GetTexture(IsoFacetShaderIds.RampTex).height,
                "the thirty-second material has no ramp row");
            Vector4[] meta = mat.GetVectorArray(IsoFacetFigureShaderIds.RampMetaFigure);
            int last = CharacterSkinDef.V9RampSlots - 1;
            Expected(last, out Vector4 wantMeta, out float _);
            Assert.AreEqual(wantMeta, meta[last], "the last slot must be the last material's, not padding");
        }

        // =================================================================== rig 7 and the hulls

        [Test]
        public void ARig7Figure_KeepsTheDefaultProgram_SixteenSlotsAndTheGlobalPair()
        {
            CharacterSkinDef def = MakeDef(ToneRule.Rig7, 5);
            Material mat = DrawnMaterial(MakeFigure(def).transform, "rig 7 figure");

            Assert.IsFalse(mat.IsKeywordEnabled(IsoFacetFigureShaderIds.FigureKeyword),
                "a rig 7 figure has HH_FIGURE on");
            Assert.AreEqual(CharacterSkinDef.RampSlots, mat.GetVectorArray(IsoFacetShaderIds.RampMeta).Length,
                "rig 7's _RampMeta is the fleet's float4[16]");
            Assert.AreEqual(def.Gain, mat.GetFloat(IsoFacetShaderIds.Gain), "rig 7 draws with the global _Gain");
            Assert.AreEqual(def.Bias, mat.GetFloat(IsoFacetShaderIds.Bias), "rig 7 draws with the global _Bias");
            Assert.AreEqual(IsoFacetMath.ShaderLightVector(def.LightN), mat.GetVector(IsoFacetShaderIds.LightN),
                "rig 7 lights with LightN, and ignores the v9 key this def also carries");
            Assert.AreEqual(5, mat.GetTexture(IsoFacetShaderIds.RampTex).height, "one ramp row per material");
        }

        [Test]
        public void HHFigureStaysOnTheV9FiguresOwnMaterial()
        {
            Material v9 = DrawnMaterial(MakeFigure(MakeDef(ToneRule.V9, 3)).transform, "v9 figure");
            CharacterSkinDef rig7Def = MakeDef(ToneRule.Rig7, 3);
            Material rig7 = DrawnMaterial(MakeFigure(rig7Def).transform, "rig 7 figure");

            _hullGo = new GameObject("TestV9Hull");
            var hull = _hullGo.AddComponent<IsoFacetHullRenderer>();
            hull.Configure(new IsoFacetHullSetup
            {
                Mesh = rig7Def.BindMesh,
                Ramps = new[] { rig7Def.Materials[0].Colors, rig7Def.Materials[1].Colors, rig7Def.Materials[2].Colors },
                RampOffsets = new[] { 0, -1, 0 },
                LightN = rig7Def.LightN,
                Gain = rig7Def.Gain,
                Bias = rig7Def.Bias,
                Bayer16 = rig7Def.Bayer16,
                Keyline = rig7Def.Keyline,
                PivotPx = rig7Def.PivotPx,
                PxPerMetre = rig7Def.PxPerMetre,
                CellW = rig7Def.CellW,
                CellH = rig7Def.CellH,
                ElevationDeg = rig7Def.ElevationDeg,
            });
            Material hullMat = DrawnMaterial(hull.PosedMesh, "hull");

            Assert.IsTrue(v9.IsKeywordEnabled(IsoFacetFigureShaderIds.FigureKeyword), "the v9 figure lost HH_FIGURE");
            Assert.AreNotSame(v9, rig7, "two figures share one material");
            Assert.AreNotSame(v9, hullMat, "a figure and a hull share one material");
            Assert.IsFalse(rig7.IsKeywordEnabled(IsoFacetFigureShaderIds.FigureKeyword),
                "a rig 7 figure built after a v9 one has HH_FIGURE on: the keyword leaked");
            Assert.IsFalse(hullMat.IsKeywordEnabled(IsoFacetFigureShaderIds.FigureKeyword),
                "a hull built after a v9 figure has HH_FIGURE on: the keyword leaked");
            Assert.IsFalse(Shader.IsKeywordEnabled(IsoFacetFigureShaderIds.FigureKeyword),
                "HH_FIGURE was set GLOBALLY. It is a _local keyword: Shader.EnableKeyword does not reach it, " +
                "and a global write is not how any facet material is meant to change");
            Assert.IsFalse(AssetDatabase.Contains(v9),
                "the v9 figure's material is an asset: the keyword would dirty it for everyone who shares it");
        }

        // =================================================================== helpers

        private static Material DrawnMaterial(Transform root, string what)
        {
            var mr = root.GetComponentInChildren<MeshRenderer>(true);
            Assert.IsNotNull(mr, "the " + what + " has no drawn MeshRenderer");
            Assert.IsNotNull(mr.sharedMaterial, "the " + what + "'s drawn renderer has no material");
            return mr.sharedMaterial;
        }

        /// <summary>EditMode runs no <c>OnDestroy</c> for the figure, so the material, ramp textures and
        /// posed mesh it built are given back here before its object goes.</summary>
        private static void Scrap(IsoCharacterFigureRenderer figure)
        {
            var mr = figure.GetComponentInChildren<MeshRenderer>(true);
            if (mr != null && mr.sharedMaterial != null)
            {
                Material mat = mr.sharedMaterial;
                Texture ramp = mat.GetTexture(IsoFacetShaderIds.RampTex);
                Texture dark = mat.GetTexture(IsoFacetShaderIds.DarkRampTex);
                if (ramp != null) UnityEngine.Object.DestroyImmediate(ramp);
                if (dark != null) UnityEngine.Object.DestroyImmediate(dark);
                UnityEngine.Object.DestroyImmediate(mat);
            }
            var mf = figure.GetComponentInChildren<MeshFilter>(true);
            if (mf != null && mf.sharedMesh != null) UnityEngine.Object.DestroyImmediate(mf.sharedMesh);
            UnityEngine.Object.DestroyImmediate(figure.gameObject);
        }
    }
}
