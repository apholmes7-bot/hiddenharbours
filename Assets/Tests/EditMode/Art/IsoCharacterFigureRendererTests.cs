using System;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE FIGURE IS A SUBJECT OF THE FACET PASS, NOT A HULL IN IT.</b>
    ///
    /// <para>Two things have to be true at once for the skinned player to be drawn correctly, and
    /// they pull against each other. She must carry the SAME facet material a hull carries, or she
    /// inks in a different palette from the boat she is standing on. And she must NOT register in
    /// <c>IsoFacetHullRegistry</c>, because <c>Count &gt; 0</c> is the gate that decides whether the
    /// facet block is recorded at all — a figure that registered would turn the pass on ASHORE, where
    /// this PR's charter says she cannot draw, and would spend a hull id plus a twelve-wide fore block
    /// out of the 255 the whole fleet shares.</para>
    ///
    /// <para>So: one test that the registry does not move, and one that the material matches a hull
    /// built from the same numbers. The second is the test
    /// <c>IsoCharacterFigureRenderer.BuildMaterial</c>'s own comment points at as the reason the
    /// builder is duplicated rather than extracted while a byte-identical hull plate is part of the
    /// acceptance.</para>
    ///
    /// <para><b>No GPU is used or needed here.</b> Nothing is drawn: the material and the ramp tables
    /// are built on the CPU, the skinning is arithmetic, and the assertions read component state. The
    /// only external dependency is that the two facet shaders can be found in the asset database,
    /// which is true on CI (<c>NothingASkinnedRendererCarriesExcludesItFromTheFacetList</c> already
    /// relies on it, ungated).</para>
    /// </summary>
    public sealed class IsoCharacterFigureRendererTests
    {
        private const int Bones = 3;

        private CharacterSkinDef _def;
        private GameObject _figureGo;
        private GameObject _hullGo;
        private Mesh _bind;

        [TearDown]
        public void TearDown()
        {
            if (_figureGo != null) UnityEngine.Object.DestroyImmediate(_figureGo);
            if (_hullGo != null) UnityEngine.Object.DestroyImmediate(_hullGo);
            if (_def != null) UnityEngine.Object.DestroyImmediate(_def);
            if (_bind != null) UnityEngine.Object.DestroyImmediate(_bind);
            _figureGo = null;
            _hullGo = null;
            _def = null;
            _bind = null;
        }

        // =================================================================== the synthetic def

        private static readonly Color32[] RampA =
        {
            new Color32(10, 20, 30, 255), new Color32(40, 50, 60, 255), new Color32(70, 80, 90, 255),
        };

        private static readonly Color32[] RampB =
        {
            new Color32(200, 10, 10, 255), new Color32(180, 20, 20, 255),
            new Color32(160, 30, 30, 255), new Color32(140, 40, 40, 255),
            new Color32(120, 50, 50, 255),
        };

        private static float[] Bayer()
        {
            var b = new float[16];
            for (int i = 0; i < 16; i++) b[i] = (i + 0.5f) / 16f;
            return b;
        }

        /// <summary>A quad skinned to two of three bones — enough geometry for the blend to be real
        /// and small enough to assert on by hand.</summary>
        private Mesh BuildBindMesh()
        {
            _bind = new Mesh { name = "HHTestBind", hideFlags = HideFlags.HideAndDontSave };
            _bind.SetVertices(new[]
            {
                new Vector3(-0.2f, 0f, 0f), new Vector3(0.2f, 0f, 0f),
                new Vector3(0.2f, 0f, 1.6f), new Vector3(-0.2f, 0f, 1.6f),
            });
            _bind.SetNormals(new[]
            {
                Vector3.back, Vector3.back, Vector3.back, Vector3.back,
            });
            _bind.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            _bind.boneWeights = new[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f, boneIndex1 = 0, weight1 = 0f },
                new BoneWeight { boneIndex0 = 0, weight0 = 1f, boneIndex1 = 0, weight1 = 0f },
                new BoneWeight { boneIndex0 = 1, weight0 = 0.5f, boneIndex1 = 2, weight1 = 0.5f },
                new BoneWeight { boneIndex0 = 1, weight0 = 0.5f, boneIndex1 = 2, weight1 = 0.5f },
            };
            _bind.bindposes = new[]
            {
                Matrix4x4.identity,
                Matrix4x4.TRS(new Vector3(0f, 0f, -0.8f), Quaternion.identity, Vector3.one),
                Matrix4x4.TRS(new Vector3(0f, 0f, -1.6f), Quaternion.identity, Vector3.one),
            };
            _bind.RecalculateBounds();
            return _bind;
        }

        private static CharacterSkinDef.SkinClip Clip(string key, int frames, bool loop,
                                                      Func<int, int, Vector3> position)
        {
            var keys = new CharacterSkinDef.BoneKey[frames * Bones];
            for (int f = 0; f < frames; f++)
                for (int b = 0; b < Bones; b++)
                    keys[f * Bones + b] = new CharacterSkinDef.BoneKey
                    {
                        Position = position(f, b),
                        Rotation = Quaternion.AngleAxis(f * 11f, Vector3.right),
                    };

            return new CharacterSkinDef.SkinClip
            {
                Anim = key, State = key, FramesPerSecond = 12f, Loop = loop,
                FrameCount = frames, Keys = keys,
            };
        }

        private CharacterSkinDef MakeDef()
        {
            _def = ScriptableObject.CreateInstance<CharacterSkinDef>();
            _def.Id = "character.test_figure";
            _def.BindMesh = BuildBindMesh();
            _def.Bones = new[]
            {
                new CharacterSkinDef.Bone { Id = "root", Parent = CharacterSkinDef.NoBone, OwnsVertex = true },
                new CharacterSkinDef.Bone { Id = "spine", Parent = 0, OwnsVertex = true },
                new CharacterSkinDef.Bone { Id = "head", Parent = 1, OwnsVertex = true },
            };
            _def.MaxInfluences = 2;
            _def.Materials = new[]
            {
                new CharacterSkinDef.Material { Name = "skin", Colors = RampA, Offset = 0 },
                new CharacterSkinDef.Material { Name = "oilskin", Colors = RampB, Offset = 3 },
            };
            _def.Bayer16 = Bayer();
            _def.Keyline = new Color32(12, 14, 18, 255);
            _def.LightN = new Vector3(0.3f, -0.5f, 0.81f);
            _def.Gain = 1.15f;
            _def.Bias = -0.04f;
            _def.CellW = 64;
            _def.CellH = 92;
            _def.PivotPx = new Vector2(32f, 80f);
            _def.PxPerMetre = 32;
            _def.ElevationDeg = 40f;

            // `idle` is clean. `mount_dory` carries the boot spike the four mount clips really have:
            // frames 2 and 3 throw one bone 140 m from the rig origin.
            _def.Clips = new[]
            {
                Clip("idle", 4, true, (f, b) => new Vector3(0f, 0f, 0.8f * b + 0.01f * f)),
                Clip("mount_dory", 6, false,
                     (f, b) => (f == 2 || f == 3) && b == 2
                         ? new Vector3(140f, 0f, 0f)
                         : new Vector3(0f, 0f, 0.8f * b + 0.02f * f)),
            };
            _def.MeshStates = new[] { "idle", "mount_dory" };

            Assert.IsTrue(_def.IsUsable(),
                "the synthetic def this fixture is built on does not satisfy IsUsable(), so every " +
                "assertion below would be measuring the fixture rather than the renderer");
            return _def;
        }

        private IsoCharacterFigureRenderer MakeFigure(CharacterSkinDef def)
        {
            _figureGo = new GameObject("TestFigure");
            var figure = _figureGo.AddComponent<IsoCharacterFigureRenderer>();
            figure.Configure(def);
            return figure;
        }

        // =================================================================== the registry fence

        [Test]
        public void TheFigureDoesNotRegisterAsAHull()
        {
            int before = IsoFacetHullRegistry.Count;
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());

            Assert.AreEqual(before, IsoFacetHullRegistry.Count,
                "the figure took a place in IsoFacetHullRegistry. Count > 0 is gate 1 — the test " +
                "that decides whether the facet block is recorded AT ALL — so a registered figure " +
                "would draw herself ashore, where this PR says she cannot draw, and would spend a " +
                "hull id plus a twelve-wide fore block out of the 255 the fleet shares.");

            Assert.IsNull(figure.GetComponent<IsoFacetHullRenderer>(),
                "the figure carries a hull renderer of its own");
            Assert.IsNull(figure.GetComponentInChildren<IsoFacetHullRenderer>(),
                "the figure's drawn child carries a hull renderer");
        }

        [Test]
        public void TheFigureFindsNoHullWhenItIsNotParentedToOne()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());

            Assert.IsNull(figure.Hull,
                "a figure standing on nothing must resolve no hull — ashore there is no facet pass " +
                "to draw her through, and the presenter's gate is exactly this answer");
        }

        // =================================================================== the same ink

        [Test]
        public void TheFigureMaterialMatchesAHullConfiguredFromTheSameSetup()
        {
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer figure = MakeFigure(def);

            _hullGo = new GameObject("TestHull");
            var hull = _hullGo.AddComponent<IsoFacetHullRenderer>();
            hull.Configure(new IsoFacetHullSetup
            {
                Mesh = def.BindMesh,
                Ramps = new[] { RampA, RampB },
                RampOffsets = new[] { 0, 3 },
                LightN = def.LightN,
                Gain = def.Gain,
                Bias = def.Bias,
                Bayer16 = def.Bayer16,
                Keyline = def.Keyline,
                PivotPx = def.PivotPx,
                PxPerMetre = def.PxPerMetre,
                CellW = def.CellW,
                CellH = def.CellH,
                ElevationDeg = def.ElevationDeg,
            });

            Material figureMat = DrawnMaterial(figure.transform, "figure");
            Material hullMat = DrawnMaterial(hull.PosedMesh, "hull");

            Assert.AreSame(hullMat.shader, figureMat.shader,
                "the figure and the hull are inked by different shaders");

            AssertFloat(hullMat, figureMat, IsoFacetShaderIds.Gain, "_Gain");
            AssertFloat(hullMat, figureMat, IsoFacetShaderIds.Bias, "_Bias");
            AssertFloat(hullMat, figureMat, IsoFacetShaderIds.PixelsPerMetre, "_PixelsPerMetre");
            AssertVector(hullMat, figureMat, IsoFacetShaderIds.LightN, "_LN");
            AssertVector(hullMat, figureMat, IsoFacetShaderIds.PivotPx, "_PivotPx");
            Assert.AreEqual(hullMat.GetColor(IsoFacetShaderIds.KeyColor),
                            figureMat.GetColor(IsoFacetShaderIds.KeyColor), "_KeyColor");

            Vector4[] hullMeta = hullMat.GetVectorArray(IsoFacetShaderIds.RampMeta);
            Vector4[] figMeta = figureMat.GetVectorArray(IsoFacetShaderIds.RampMeta);
            Assert.IsNotNull(hullMeta, "the hull wrote no _RampMeta");
            Assert.IsNotNull(figMeta, "the figure wrote no _RampMeta");
            for (int m = 0; m < 2; m++)
                Assert.AreEqual(hullMeta[m], figMeta[m],
                    "_RampMeta[" + m + "] — the ramp length and palette offset the shader indexes " +
                    "with. A mismatch here is a figure inked out of the wrong band of the palette.");

            Vector4[] hullBayer = hullMat.GetVectorArray(IsoFacetShaderIds.Bayer);
            Vector4[] figBayer = figureMat.GetVectorArray(IsoFacetShaderIds.Bayer);
            for (int x = 0; x < 4; x++)
                Assert.AreEqual(hullBayer[x], figBayer[x], "_Bayer row " + x);

            // The ramp textures are uploaded with makeNoLongerReadable, on both sides, so their
            // PIXELS cannot be compared from script. Their shape can, and the palette they were
            // filled from is the same array object in this fixture — which is the point: the two
            // builders must lay the same table out the same way.
            AssertSameShape(hullMat, figureMat, IsoFacetShaderIds.RampTex, "_RampTex");
            AssertSameShape(hullMat, figureMat, IsoFacetShaderIds.DarkRampTex, "_DarkRampTex");
        }

        // =================================================================== stepping and the fence

        [Test]
        public void SetPoseStepsToExactlyTheSampleAskedForAndNeverBetweenTwo()
        {
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer figure = MakeFigure(def);

            Assert.IsTrue(figure.SetPose("idle", 1));
            Vector3[] atOne = PosedVertices(figure);
            Assert.IsTrue(figure.SetPose("idle", 2));
            Vector3[] atTwo = PosedVertices(figure);

            Assert.AreEqual(2, figure.DrawnFrame);
            Assert.IsFalse(figure.LastFrameWasFenced, "a clean clip must not be fenced");

            // The pose for frame 2 must be frame 2's own composition — not a midpoint of 1 and 2,
            // which is what any interpolation would produce and what would swing a 178 degree bone
            // through the body.
            Vector3[] expected = HandSkinned(def, "idle", 2);
            for (int v = 0; v < expected.Length; v++)
                Assert.That((expected[v] - atTwo[v]).magnitude, Is.LessThan(1e-4f),
                    "vertex " + v + " is not frame 2's own pose");

            bool moved = false;
            for (int v = 0; v < atOne.Length; v++)
                if ((atOne[v] - atTwo[v]).sqrMagnitude > 1e-8f) moved = true;
            Assert.IsTrue(moved, "the figure did not move between two different frames at all");
        }

        [Test]
        public void ReposingToTheSameFrameIsAcceptedWithoutReskinning()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());

            Assert.IsTrue(figure.SetPose("idle", 1));
            Vector3[] first = PosedVertices(figure);
            Assert.IsTrue(figure.SetPose("idle", 1));
            Vector3[] again = PosedVertices(figure);

            Assert.AreEqual(first.Length, again.Length);
            for (int v = 0; v < first.Length; v++)
                Assert.AreEqual(first[v], again[v], "vertex " + v + " moved on a repeat pose");
            Assert.AreEqual(1, figure.RequestedFrame);
            Assert.AreEqual(1, figure.DrawnFrame);
        }

        [Test]
        public void APoisonedFrameIsFencedAndTheFenceIsReportedNotHidden()
        {
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer figure = MakeFigure(def);

            Assert.IsTrue(figure.SetPose("mount_dory", 3));

            Assert.AreEqual(3, figure.RequestedFrame);
            Assert.AreEqual(1, figure.DrawnFrame,
                "frames 2 and 3 throw a bone 140 m out; the figure must hold the last honest pose (1)");
            Assert.IsTrue(figure.LastFrameWasFenced,
                "the fence fired but the renderer does not admit it, so a plate of this frame would " +
                "be quietly a different frame from the one it claims");

            CollectionAssert.IsNotEmpty(figure.FenceReport);
            bool named = false;
            foreach (string line in figure.FenceReport)
                if (line.Contains("mount_dory") && line.Contains("2-3")) named = true;
            Assert.IsTrue(named,
                "the fence report must name the clip and the exact fenced span so the PR body can " +
                "quote it as a fact rather than a behaviour. Got: " +
                string.Join(" | ", figure.FenceReport));
        }

        [Test]
        public void AnHonestClipIsNotInTheFenceReportAtAll()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());

            foreach (string line in figure.FenceReport)
                Assert.IsFalse(line.StartsWith("idle:", StringComparison.Ordinal),
                    "a clean clip was reported as fenced: " + line);
        }

        [Test]
        public void PosingAStateTheDefDoesNotCarryIsRefusedRatherThanGuessed()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());

            Assert.IsFalse(figure.SetPose("helm", 0),
                "a clip the bake never made must be refused — drawing some other clip in its place " +
                "is the art gap that never gets found");
            Assert.IsFalse(figure.SetPose(null, 0));
            Assert.IsFalse(figure.SetPose("", 0));
        }

        [Test]
        public void AFrameBeyondTheClipIsClampedIntoIt()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());

            Assert.IsTrue(figure.SetPose("idle", 99));
            Assert.AreEqual(3, figure.DrawnFrame, "idle has four frames, so 99 must clamp to 3");
            Assert.IsTrue(figure.SetPose("idle", -5));
            Assert.AreEqual(0, figure.DrawnFrame);
        }

        // =================================================================== configuration gates

        [Test]
        public void ConfigureRefusesADefThatIsNotUsable()
        {
            CharacterSkinDef def = MakeDef();
            def.Bayer16 = new float[4];   // the dither table the shader indexes 4x4

            _figureGo = new GameObject("TestFigure");
            var figure = _figureGo.AddComponent<IsoCharacterFigureRenderer>();

            Assert.Throws<InvalidOperationException>(() => figure.Configure(def),
                "an unusable def must be refused loudly — a figure configured from a half-baked " +
                "asset draws a collapsed or mis-inked body and looks like a shader bug");
            Assert.IsFalse(figure.IsConfigured);
        }

        [Test]
        public void ConfigureRefusesNull()
        {
            _figureGo = new GameObject("TestFigure");
            var figure = _figureGo.AddComponent<IsoCharacterFigureRenderer>();

            Assert.Throws<ArgumentNullException>(() => figure.Configure(null));
            Assert.IsFalse(figure.IsConfigured);
        }

        [Test]
        public void TheDrawnChildIsHiddenAndShownThroughVisibleWithoutBeingRebuilt()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());
            var mr = figure.GetComponentInChildren<MeshRenderer>(true);
            Assert.IsNotNull(mr, "the figure built no drawn child");

            figure.Visible = false;
            Assert.IsFalse(figure.Visible);
            figure.Visible = true;
            Assert.IsTrue(figure.Visible);

            Assert.AreSame(mr, figure.GetComponentInChildren<MeshRenderer>(true),
                "toggling visibility rebuilt the drawn child — a per-frame allocation on the one " +
                "object that is on screen every frame (rule 7)");
        }

        // =================================================================== helpers

        private static Material DrawnMaterial(Transform root, string what)
        {
            var mr = root.GetComponentInChildren<MeshRenderer>(true);
            Assert.IsNotNull(mr, "the " + what + " has no drawn MeshRenderer");
            Assert.IsNotNull(mr.sharedMaterial, "the " + what + "'s drawn renderer has no material");
            return mr.sharedMaterial;
        }

        private static Vector3[] PosedVertices(IsoCharacterFigureRenderer figure)
        {
            var mf = figure.GetComponentInChildren<MeshFilter>(true);
            Assert.IsNotNull(mf, "the figure built no drawn child");
            Assert.IsNotNull(mf.sharedMesh, "the figure's drawn child carries no mesh");
            return mf.sharedMesh.vertices;
        }

        private Vector3[] HandSkinned(CharacterSkinDef def, string stateKey, int frame)
        {
            Assert.IsTrue(def.TryGetClip(stateKey, out CharacterSkinDef.SkinClip clip));
            var world = new Matrix4x4[Bones];
            var skin = new Matrix4x4[Bones];
            CharacterSkinPose.ComposeSkinMatrices(clip, frame, def.Bones, def.BindMesh.bindposes,
                                                  world, skin);
            Vector3[] src = def.BindMesh.vertices;
            var outV = new Vector3[src.Length];
            var outN = new Vector3[src.Length];
            CharacterSkinPose.Skin(skin, def.BindMesh.boneWeights, src, def.BindMesh.normals,
                                   outV, outN);
            return outV;
        }

        private static void AssertFloat(Material a, Material b, int id, string name) =>
            Assert.AreEqual(a.GetFloat(id), b.GetFloat(id), 1e-6f, name);

        private static void AssertVector(Material a, Material b, int id, string name)
        {
            Vector4 va = a.GetVector(id), vb = b.GetVector(id);
            Assert.That((va - vb).magnitude, Is.LessThan(1e-5f),
                name + ": hull " + va + " vs figure " + vb);
        }

        private static void AssertSameShape(Material a, Material b, int id, string name)
        {
            var ta = a.GetTexture(id) as Texture2D;
            var tb = b.GetTexture(id) as Texture2D;
            Assert.IsNotNull(ta, "the hull wrote no " + name);
            Assert.IsNotNull(tb, "the figure wrote no " + name);
            Assert.AreEqual(ta.width, tb.width, name + " width");
            Assert.AreEqual(ta.height, tb.height, name + " height");
            Assert.AreEqual(ta.filterMode, tb.filterMode, name + " filterMode");
            Assert.AreEqual(ta.wrapMode, tb.wrapMode, name + " wrapMode");
        }
    }
}
