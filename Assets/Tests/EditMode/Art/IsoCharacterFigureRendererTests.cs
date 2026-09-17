using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
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
    /// <c>IsoFacetHullRegistry</c> as a hull: a hull spends an id plus a twelve-wide fore block out of
    /// the 255 the whole fleet shares, and <c>Count</c> is the fleet.</para>
    ///
    /// <para><b>Ashore the premise moved on purpose (ADR 0044, ashore PR 1).</b> She may now draw
    /// with no hull — but only through an explicit <c>EnterAshore</c>, which takes ONE figure id and
    /// raises <c>FigureCount</c>, never <c>Count</c>. Configuring her still registers nothing, and
    /// nothing in a shipped scene calls <c>EnterAshore</c>. The ashore guards below hold what she
    /// carries for herself there: her own id with no fore block on both renderers, the hull's frame,
    /// the cell overlay at the sprite's sort, the refusals, and a clean way back. Each takes its ids
    /// from a fresh pool and leaves ashore in <c>TearDown</c>, because neither <c>OnDisable</c> nor
    /// <c>OnDestroy</c> runs for this component in EditMode and a leaked figure id would hold the
    /// facet gate open for the rest of the domain.</para>
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
        private GameObject _spriteGo;
        private Mesh _bind;
        private IsoFacetIdPool _livePool;

        [TearDown]
        public void TearDown()
        {
            if (_figureGo != null)
            {
                // EditMode runs neither OnDisable nor OnDestroy for the figure: give the id back here.
                var figure = _figureGo.GetComponent<IsoCharacterFigureRenderer>();
                if (figure != null) figure.LeaveAshore();
                UnityEngine.Object.DestroyImmediate(_figureGo);
            }
            if (_spriteGo != null) UnityEngine.Object.DestroyImmediate(_spriteGo);
            if (_hullGo != null) UnityEngine.Object.DestroyImmediate(_hullGo);
            if (_def != null) UnityEngine.Object.DestroyImmediate(_def);
            if (_bind != null) UnityEngine.Object.DestroyImmediate(_bind);
            // Last, after every id taken from the fresh pool has been given back to it.
            if (_livePool != null) IsoFacetHullRegistry.SwapIdPoolForTests(_livePool);
            _figureGo = null;
            _spriteGo = null;
            _hullGo = null;
            _def = null;
            _bind = null;
            _livePool = null;
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
            int figuresBefore = IsoFacetHullRegistry.FigureCount;
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());

            Assert.AreEqual(before, IsoFacetHullRegistry.Count,
                "the figure took a place in IsoFacetHullRegistry as a HULL. Count is the fleet: a " +
                "figure counted there would spend a hull id plus a twelve-wide fore block out of the " +
                "255 the fleet shares.");
            Assert.AreEqual(figuresBefore, IsoFacetHullRegistry.FigureCount,
                "CONFIGURING the figure took a figure id. Only EnterAshore may — a figure that " +
                "registered on Configure would open the facet gate in every scene that merely builds her.");
            Assert.IsFalse(figure.IsAshore, "a configured figure is ashore without EnterAshore");

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
                "a figure standing on nothing must resolve no hull — ashore she draws through her OWN " +
                "figure id (EnterAshore), never through a hull she is not standing on");
        }

        // =================================================================== ashore

        private static readonly Regex FigureExhausted = new Regex(@"this figure gets NO facet id");

        /// <summary>Ids for this test come from a fresh pool; TearDown puts the live one back last.</summary>
        private void UseFreshIdPool(IsoFacetIdPool pool = null)
        {
            IsoFacetIdPool old = IsoFacetHullRegistry.SwapIdPoolForTests(pool ?? new IsoFacetIdPool());
            if (_livePool == null) _livePool = old;
        }

        private SpriteRenderer MakeSortSource(int sortingOrder)
        {
            _spriteGo = new GameObject("TestSortSource");
            var sprite = _spriteGo.AddComponent<SpriteRenderer>();
            sprite.sortingOrder = sortingOrder;
            return sprite;
        }

        private static MeshRenderer FacetRendererOf(IsoCharacterFigureRenderer figure)
        {
            foreach (MeshRenderer r in figure.GetComponentsInChildren<MeshRenderer>(true))
                if (r.gameObject.name == "FacetFigure") return r;
            Assert.Fail("the figure has no FacetFigure child");
            return null;
        }

        [Test]
        public void EnterAshoreTakesOneFigureId_AndLeaveAshoreGivesItBack()
        {
            UseFreshIdPool();
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());
            SpriteRenderer sprite = MakeSortSource(3);
            int hulls = IsoFacetHullRegistry.Count;
            int figures = IsoFacetHullRegistry.FigureCount;

            Assert.IsTrue(figure.EnterAshore(sprite), "a configured figure standing on nothing could not go ashore");
            Assert.IsTrue(figure.IsAshore);
            Assert.That(figure.FigureId, Is.InRange(1, 254), "an ashore figure holds the overflow id or none");
            Assert.AreEqual(figures + 1, IsoFacetHullRegistry.FigureCount);
            Assert.AreEqual(hulls, IsoFacetHullRegistry.Count, "going ashore counted her as a hull");
            Assert.IsTrue(IsoFacetHullFeature.FacetSubjectsLive, "an ashore figure did not open the facet gate");

            Assert.IsTrue(figure.EnterAshore(sprite), "entering again must re-point the sort, not refuse");
            Assert.AreEqual(figures + 1, IsoFacetHullRegistry.FigureCount, "entering again took a second id");

            figure.LeaveAshore();
            Assert.IsFalse(figure.IsAshore);
            Assert.AreEqual(0, figure.FigureId);
            Assert.AreEqual(figures, IsoFacetHullRegistry.FigureCount, "leaving ashore kept the id");
            Assert.AreEqual(IsoFacetHullRegistry.Count > 0, IsoFacetHullFeature.FacetSubjectsLive,
                "with her id given back the gate must be main's gate again");
            Assert.IsNull(figure.AshoreOverlay, "leaving ashore kept the overlay");

            figure.LeaveAshore();
            Assert.AreEqual(figures, IsoFacetHullRegistry.FigureCount, "leaving twice gave an id back twice");
        }

        [Test]
        public void EnterAshoreRefusesAFigureStandingOnAHull()
        {
            UseFreshIdPool();
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());
            SpriteRenderer sprite = MakeSortSource(0);
            _hullGo = new GameObject("TestHull");
            _hullGo.AddComponent<IsoFacetHullRenderer>();
            _figureGo.transform.SetParent(_hullGo.transform, false);
            int figures = IsoFacetHullRegistry.FigureCount;

            Assert.IsFalse(figure.EnterAshore(sprite),
                "a figure standing on a hull went ashore — aboard is the hull's frame and the hull's " +
                "id, and she would be kept by two overlays at once");
            Assert.IsFalse(figure.IsAshore);
            Assert.AreEqual(figures, IsoFacetHullRegistry.FigureCount);
            Assert.IsNull(figure.AshoreOverlay);
        }

        [Test]
        public void AnUnconfiguredFigureCannotGoAshore()
        {
            UseFreshIdPool();
            _figureGo = new GameObject("TestFigure");
            var figure = _figureGo.AddComponent<IsoCharacterFigureRenderer>();
            int figures = IsoFacetHullRegistry.FigureCount;

            Assert.IsFalse(figure.EnterAshore(MakeSortSource(0)), "an unconfigured figure went ashore");
            Assert.AreEqual(figures, IsoFacetHullRegistry.FigureCount, "an unconfigured figure took an id");
            Assert.Throws<ArgumentNullException>(() => figure.EnterAshore(null));
        }

        [Test]
        public void AtExhaustion_EnterAshoreRefusesAndBuildsNothing()
        {
            var drained = new IsoFacetIdPool();
            for (int i = 1; i < 255; i++) drained.TakeId();
            UseFreshIdPool(drained);
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());
            SpriteRenderer sprite = MakeSortSource(0);
            int figures = IsoFacetHullRegistry.FigureCount;

            LogAssert.Expect(LogType.Warning, FigureExhausted);
            Assert.IsFalse(figure.EnterAshore(sprite), "a refused figure went ashore anyway");
            Assert.IsFalse(figure.IsAshore);
            Assert.AreEqual(figures, IsoFacetHullRegistry.FigureCount);
            Assert.IsNull(figure.AshoreOverlay, "a refused figure built an overlay");
            Assert.AreEqual(figure.transform, FacetRendererOf(figure).transform.parent,
                "a refused figure moved her facet child into a frame");
            Assert.AreEqual(1, figure.transform.childCount, "a refused figure built a frame or an overlay");
        }

        [Test]
        public void AnAshoreFigureWritesItsOwnIdWithNoForeBlock_OnBothRenderers()
        {
            UseFreshIdPool();
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());
            _figureGo.transform.position = new Vector3(4.5f, -2.25f, 0f);
            Assert.IsTrue(figure.EnterAshore(MakeSortSource(0)));

            foreach (Renderer r in new Renderer[] { FacetRendererOf(figure), figure.AshoreOverlay })
            {
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                Assert.AreEqual(figure.FigureId / 255f, block.GetFloat(IsoFacetShaderIds.HullId), 1e-6f,
                    r.name + " does not carry her figure id — the overlay would keep none of her pixels");
                Assert.AreEqual(0f, block.GetFloat(IsoFacetShaderIds.HullIdFore),
                    r.name + " carries a fore block — she would keep another boat's crew pixels as her own");
                Assert.AreEqual(0f, block.GetFloat(IsoFacetShaderIds.HullIdForeSpan), r.name);
                Vector4 origin = block.GetVector(IsoFacetShaderIds.HullOrigin);
                Assert.AreEqual(4.5f, origin.x, 1e-5f, r.name + " dither origin x is not her own root");
                Assert.AreEqual(-2.25f, origin.y, 1e-5f, r.name + " dither origin y is not her own root");
            }
        }

        [Test]
        public void AnAshoreFigureStandsInTheHullsFrame_AndLeavingPutsHerBack()
        {
            UseFreshIdPool();
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer figure = MakeFigure(def);
            MeshRenderer facet = FacetRendererOf(figure);
            Assert.IsTrue(figure.EnterAshore(MakeSortSource(0)));

            Transform frame = facet.transform.parent;
            Assert.AreNotEqual(figure.transform, frame, "her facet child did not move into a frame");
            Assert.AreEqual(figure.transform, frame.parent);
            Quaternion expected = IsoFacetMath.HullRotation(0d, def.ElevationDeg);
            Assert.That(Quaternion.Angle(expected, frame.localRotation), Is.LessThan(1e-3f),
                "the ashore frame is not the rotation a hull's posed mesh child wears");
            Assert.AreEqual(IsoFacetMath.HullScale, frame.localScale, "the ashore frame lost the hull's mirror");

            figure.SetAshoreYaw(90f);
            Assert.That(Quaternion.Angle(Quaternion.AngleAxis(90f, Vector3.forward), facet.transform.localRotation),
                Is.LessThan(1e-3f), "her facing is not written inside the frame");

            figure.LeaveAshore();
            Assert.AreEqual(figure.transform, facet.transform.parent, "leaving ashore did not put her facet child back");
            Assert.AreEqual(Vector3.zero, facet.transform.localPosition);
            Assert.AreEqual(Quaternion.identity, facet.transform.localRotation);
            Assert.AreEqual(Vector3.one, facet.transform.localScale);
            Assert.IsFalse(facet.HasPropertyBlock(), "leaving ashore left her figure id on the facet renderer");
            Assert.AreEqual(1, figure.transform.childCount, "the ashore frame or overlay outlived LeaveAshore");
        }

        [Test]
        public void TheAshoreOverlayIsTheDefsCellPaddedOnePixel()
        {
            UseFreshIdPool();
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer figure = MakeFigure(def);
            Assert.IsTrue(figure.EnterAshore(MakeSortSource(0)));

            MeshRenderer overlay = figure.AshoreOverlay;
            Assert.IsNotNull(overlay);
            Assert.AreEqual("HiddenHarbours/IsoFacetOverlay", overlay.sharedMaterial.shader.name);
            Assert.AreEqual(figure.transform, overlay.transform.parent);
            Assert.AreEqual(Vector3.zero, overlay.transform.localPosition);
            Assert.IsNotNull(overlay.GetComponent<SortingGroup>(),
                "the overlay has no SortingGroup — a mesh renderer does not sort against sprites without one");
            Assert.AreEqual(ShadowCastingMode.Off, overlay.shadowCastingMode);

            float ppu = def.PxPerMetre, pad = 1f / ppu;
            Bounds b = overlay.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.AreEqual(-def.PivotPx.x / ppu - pad, b.min.x, 1e-5f, "left");
            Assert.AreEqual((def.CellW - def.PivotPx.x) / ppu + pad, b.max.x, 1e-5f, "right");
            Assert.AreEqual(def.PivotPx.y / ppu + pad, b.max.y, 1e-5f, "top");
            Assert.AreEqual(-(def.CellH - def.PivotPx.y) / ppu - pad, b.min.y, 1e-5f, "bottom");
        }

        [Test]
        public void TheAshoreOverlayCopiesTheSpritesSort_OnEveryWrite()
        {
            UseFreshIdPool();
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());
            SpriteRenderer sprite = MakeSortSource(17);
            Assert.IsTrue(figure.EnterAshore(sprite));

            MeshRenderer overlay = figure.AshoreOverlay;
            SortingGroup group = overlay.GetComponent<SortingGroup>();
            Assert.AreEqual(17, group.sortingOrder, "the SortingGroup did not take the sprite's order on entry");
            Assert.AreEqual(17, overlay.sortingOrder);
            Assert.AreEqual(sprite.sortingLayerID, group.sortingLayerID);
            Assert.AreEqual(sprite.sortingLayerID, overlay.sortingLayerID);

            sprite.sortingOrder = -4;
            figure.WriteAshoreProperties();
            Assert.AreEqual(-4, group.sortingOrder, "the SortingGroup did not follow the sprite's re-sort");
            Assert.AreEqual(-4, overlay.sortingOrder, "the overlay did not follow the sprite's re-sort");
        }

        [Test]
        public void VisibleHidesTheAshoreOverlayWithTheFigure()
        {
            UseFreshIdPool();
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());
            Assert.IsTrue(figure.EnterAshore(MakeSortSource(0)));

            figure.Visible = false;
            Assert.IsFalse(FacetRendererOf(figure).enabled);
            Assert.IsFalse(figure.AshoreOverlay.enabled, "a hidden figure left her overlay drawing");
            figure.Visible = true;
            Assert.IsTrue(figure.AshoreOverlay.enabled);
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
