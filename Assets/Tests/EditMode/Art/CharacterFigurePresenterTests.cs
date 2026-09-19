using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE CAST'S FIGURE PRESENTER CHOOSES MESH OR SPRITE, AND OWNS NOTHING IT DOES NOT SET</b>
    /// (ADR 0044, amendment 2026-09-17).
    ///
    /// <para>The presenter reads its stand every frame and answers one question: does this character draw
    /// as the skinned figure on the hull under them, or as the sprite it already has? These tests build
    /// both answers from a synthetic def, with the def present and absent. They read component state only:
    /// no GPU is used and no frame is drawn. <see cref="CharacterFigurePresenter.PoseFigure"/> is called
    /// directly, because EditMode runs no <c>LateUpdate</c>.</para>
    ///
    /// <para><b>What the presenter must never own</b> is asserted as hard as what it draws.
    /// <c>SpriteRenderer.enabled</c> belongs to <c>ApplyShelter</c> and is only read. A
    /// <c>forceRenderingOff</c> that someone else set is never cleared. The sprite's sorting is only
    /// compared. Each negative below opens from a POSITIVE on the same fixture, so a presenter that
    /// could not draw at all cannot pass it.</para>
    /// </summary>
    public sealed class CharacterFigurePresenterTests
    {
        private const int Bones = 3;
        private const int RestSortingOrder = 7;
        private const float StandBearing = 90f;

        private static readonly Vector3 StandPoint = new Vector3(0.3f, -0.2f, 0.5f);

        private readonly List<Object> _made = new List<Object>();
        private GameConfig _previousConfig;
        private ICharacterFigurePresentationService _previousService;

        private GameConfig _config;
        private CharacterSkinDef _skin;
        private CharacterVisualDef _visual;
        private GameObject _characterGo;
        private IsoCharacterSprite _character;
        private SpriteRenderer _sprite;
        private GameObject _hullGo;
        private IsoFacetHullRenderer _hull;
        private FakeStand _stand;

        private sealed class FakeStand : ICharacterFigureStand
        {
            public IsoCharacterSprite Character;
            public Transform Hull;
            public Vector3 Point;
            public float Bearing;

            public IsoCharacterSprite FigureCharacter => Character;
            public Transform FigureHull => Hull;
            public Vector3 FigureStandRigMetres => Point;
            public float FigureDeckBearingDegrees => Bearing;
        }

        private sealed class FakeService : ICharacterFigurePresentationService
        {
            public ICharacterFigure Attach(GameObject host, ICharacterFigureStand stand) => null;
        }

        [SetUp]
        public void SetUp()
        {
            _previousConfig = GameServices.Config;
            _previousService = CharacterFigurePresentation.Service;

            _config = Track(ScriptableObject.CreateInstance<GameConfig>());
            _config.MeshCast = true;
            GameServices.Config = _config;

            _skin = MakeSkin();
            _visual = Track(ScriptableObject.CreateInstance<CharacterVisualDef>());
            _visual.Skin = _skin;

            _hullGo = Track(new GameObject("TestFacetHull"));
            _hull = _hullGo.AddComponent<IsoFacetHullRenderer>();
            _hull.Configure(SetupFrom(_skin));

            _characterGo = Track(new GameObject("TestSkipper"));
            _character = _characterGo.AddComponent<IsoCharacterSprite>();
            // [RequireComponent] added it; resolve it, never add a second.
            _sprite = _characterGo.GetComponent<SpriteRenderer>();
            _character.Configure(_visual);
            _sprite.sortingOrder = RestSortingOrder;

            _stand = new FakeStand
            {
                Character = _character, Hull = _hullGo.transform, Point = StandPoint, Bearing = StandBearing,
            };

            Assert.IsTrue(_skin.IsUsable(), "harness: the synthetic skin must be usable, or every test " +
                                            "below measures the fixture");
            Assert.IsNotNull(_hull.PosedMesh, "harness: the hull must have a posed mesh to stand a figure in");
            Assert.IsNotNull(_sprite, "harness: IsoCharacterSprite requires a SpriteRenderer");
            Assert.AreEqual(1, _characterGo.GetComponents<SpriteRenderer>().Length,
                            "harness: one SpriteRenderer, or a negative reads the wrong one");
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Config = _previousConfig;
            CharacterFigurePresentation.Service = _previousService;
            for (int i = _made.Count - 1; i >= 0; i--)
                if (_made[i] != null) Object.DestroyImmediate(_made[i]);
            _made.Clear();
        }

        // =================================================================== present: the mesh draws

        [Test]
        public void AUsableSkinOnAFacetHullDrawsTheFigureAndHidesTheSpriteOnlyByForceRenderingOff()
        {
            CharacterFigurePresenter presenter = Attach();
            Assert.IsFalse(_sprite.forceRenderingOff, "attaching alone must not hide the sprite");

            presenter.PoseFigure(_stand, aboard: true);

            AssertDraws(presenter);
            Assert.IsTrue(_sprite.enabled, "the presenter must never write SpriteRenderer.enabled");

            IsoCharacterFigureRenderer figure = presenter.Figure;
            Assert.AreSame(_hull.PosedMesh, figure.transform.parent,
                           "the figure must stand in the hull's posed mesh, the rig frame the facet pass draws");
            Assert.AreEqual(CharacterFigurePresenter.FigureObjectName, figure.gameObject.name);
            Assert.AreEqual(_hull.PosedMesh.gameObject.layer, figure.gameObject.layer,
                            "the figure must be on the hull's layer, or the facet pass never sees it");
            Assert.AreEqual("idle", presenter.DrawnStateKey,
                            "a standing character with no stance requested draws the idle clip");

            Assert.AreEqual(StandPoint, presenter.FigureLocalMetres);
            Assert.That(Vector3.Distance(StandPoint, figure.transform.localPosition), Is.LessThan(1e-5f),
                        "the figure's feet must be the stand's rig point");
            // The fixture's skin is CCW (set below, not read back), so a 90° bearing yaws −90° about +z.
            Assert.AreEqual(-StandBearing, presenter.FigureYawDegrees, 1e-4f);
            Assert.That(Quaternion.Angle(Quaternion.AngleAxis(-StandBearing, Vector3.forward),
                                         figure.transform.localRotation), Is.LessThan(0.01f));
        }

        [Test]
        public void ReposingEveryFrameReusesTheOneFigure()
        {
            CharacterFigurePresenter presenter = Attach();
            presenter.PoseFigure(_stand, aboard: true);
            IsoCharacterFigureRenderer first = presenter.Figure;

            for (int i = 0; i < 5; i++) presenter.PoseFigure(_stand, aboard: true);

            AssertDraws(presenter);
            Assert.AreSame(first, presenter.Figure, "a steady frame must not rebuild the figure (rule 7)");
            Assert.AreEqual(1, _hull.PosedMesh.childCount, "exactly one figure under the hull");
        }

        // =================================================================== absent: the sprite draws

        [Test]
        public void NoSkinMeansNoPresenterAndNoNewComponentAtAll()
        {
            _visual.Skin = null;
            int components = _characterGo.GetComponents<Component>().Length;

            ICharacterFigure attached = new CharacterFigurePresentationService().Attach(_characterGo, _stand);

            Assert.IsNull(attached, "a character with no skin must get no presenter");
            Assert.AreEqual(components, _characterGo.GetComponents<Component>().Length,
                            "the sprite, exactly as today, means exactly today's components");
            Assert.IsFalse(_sprite.forceRenderingOff);
        }

        [Test]
        public void TheSwitchOffKeepsTheSpriteAndGivesTheHideBack()
        {
            CharacterFigurePresenter presenter = Attach();
            presenter.PoseFigure(_stand, aboard: true);
            AssertDraws(presenter);
            IsoCharacterFigureRenderer figure = presenter.Figure;

            _config.MeshCast = false;
            presenter.PoseFigure(_stand, aboard: true);

            AssertSprite(presenter, CharacterFigurePresenter.Refusal.SwitchOff);
            Assert.IsFalse(figure.Visible, "the figure must hide while the switch is off");

            _config.MeshCast = true;
            presenter.PoseFigure(_stand, aboard: true);

            AssertDraws(presenter);
            Assert.AreSame(figure, presenter.Figure, "a switch flip must hide the figure, not rebuild it");
        }

        [Test]
        public void AStateTheSkinDoesNotSwitchOnKeepsTheSprite()
        {
            CharacterFigurePresenter presenter = Attach();
            presenter.PoseFigure(_stand, aboard: true);
            AssertDraws(presenter);

            _skin.MeshStates = new string[0];
            presenter.PoseFigure(_stand, aboard: true);

            AssertSprite(presenter, CharacterFigurePresenter.Refusal.StateNotMeshed);
            StringAssert.Contains("idle", presenter.NotDrawingReason, "the refusal must name the state");
        }

        [Test]
        public void AnUnusableSkinKeepsTheSprite()
        {
            _skin.Clips = new CharacterSkinDef.SkinClip[0];
            Assert.IsFalse(_skin.IsUsable(), "harness: a skin with no clips must be unusable");
            CharacterFigurePresenter presenter = Attach();

            presenter.PoseFigure(_stand, aboard: true);

            AssertSprite(presenter, CharacterFigurePresenter.Refusal.SkinUnusable);
            Assert.IsNull(presenter.Figure, "nothing may be built for a skin that cannot draw");
        }

        [Test]
        public void AshoreNeverDrawsAndBuildsNothing()
        {
            CharacterFigurePresenter presenter = Attach();

            presenter.PoseFigure(_stand, aboard: false);

            AssertSprite(presenter, CharacterFigurePresenter.Refusal.Ashore);
            Assert.IsNull(presenter.Figure);
            Assert.AreEqual(0, _hull.PosedMesh.childCount);
        }

        [Test]
        public void ASpriteHullIsNotAFacetHull()
        {
            CharacterFigurePresenter presenter = Attach();
            GameObject spriteHull = Track(new GameObject("TestSpriteHull"));
            _stand.Hull = spriteHull.transform;

            presenter.PoseFigure(_stand, aboard: true);

            AssertSprite(presenter, CharacterFigurePresenter.Refusal.NotAFacetHull);
            Assert.IsNull(presenter.Figure);
        }

        [Test]
        public void ASuspendedCharacterKeepsTheSprite()
        {
            CharacterFigurePresenter presenter = Attach();
            presenter.PoseFigure(_stand, aboard: true);
            AssertDraws(presenter);

            _character.Suspend();
            presenter.PoseFigure(_stand, aboard: true);
            AssertSprite(presenter, CharacterFigurePresenter.Refusal.CharacterSuspended);

            _character.Release();
            presenter.PoseFigure(_stand, aboard: true);
            AssertDraws(presenter);
        }

        // =================================================================== what it never owns

        [Test]
        public void TheSpritesEnabledFlagIsReadAndNeverWritten()
        {
            CharacterFigurePresenter presenter = Attach();
            presenter.PoseFigure(_stand, aboard: true);
            AssertDraws(presenter);

            _sprite.enabled = false;   // what ApplyShelter does when a cabin hides the skipper
            presenter.PoseFigure(_stand, aboard: true);

            AssertSprite(presenter, CharacterFigurePresenter.Refusal.SpriteDisabled);
            Assert.IsFalse(_sprite.enabled, "the presenter must leave a hidden sprite hidden");
            Assert.IsFalse(presenter.Figure.Visible, "a sheltered character must not draw as a figure either");

            _sprite.enabled = true;
            presenter.PoseFigure(_stand, aboard: true);
            AssertDraws(presenter);
        }

        [Test]
        public void AHideSomeoneElseSetIsNeverCleared()
        {
            CharacterFigurePresenter presenter = Attach();
            _sprite.forceRenderingOff = true;

            presenter.PoseFigure(_stand, aboard: true);

            Assert.AreEqual(CharacterFigurePresenter.Refusal.SpriteHiddenElsewhere, presenter.WhyNot);
            Assert.IsFalse(presenter.DrawsInsteadOfSprite);
            Assert.IsTrue(_sprite.forceRenderingOff, "a foreign hide must survive the presenter");
            Assert.IsFalse(presenter.HidesSprite);
        }

        [Test]
        public void ARestagedSpriteTakesTheDrawBackUntilItsStagingReturns()
        {
            CharacterFigurePresenter presenter = Attach();
            presenter.PoseFigure(_stand, aboard: true);
            AssertDraws(presenter);

            // What the arrival does below decks: lift the skipper over the room. A figure composited at the
            // hull's own sorting order cannot follow it there.
            _sprite.sortingOrder = RestSortingOrder + 40;
            presenter.PoseFigure(_stand, aboard: true);

            AssertSprite(presenter, CharacterFigurePresenter.Refusal.SpriteRestaged);
            Assert.AreEqual(RestSortingOrder + 40, _sprite.sortingOrder, "sorting is compared, never written");

            _sprite.sortingOrder = RestSortingOrder;
            presenter.PoseFigure(_stand, aboard: true);
            AssertDraws(presenter);
        }

        [Test]
        public void ADestroyedHullReleasesTheFigureAndTheSprite()
        {
            CharacterFigurePresenter presenter = Attach();
            presenter.PoseFigure(_stand, aboard: true);
            AssertDraws(presenter);

            Object.DestroyImmediate(_hullGo);
            presenter.PoseFigure(_stand, aboard: true);

            AssertSprite(presenter, CharacterFigurePresenter.Refusal.NotAFacetHull);
            Assert.IsTrue(presenter.Figure == null, "the figure went with its hull and must not be kept");
        }

        // =================================================================== the seam's service

        [Test]
        public void AttachingTwiceKeepsOnePresenter()
        {
            var service = new CharacterFigurePresentationService();
            ICharacterFigure first = service.Attach(_characterGo, _stand);
            ICharacterFigure second = service.Attach(_characterGo, _stand);

            Assert.IsNotNull(first);
            Assert.AreSame(first, second);
            Assert.AreEqual(1, _characterGo.GetComponents<CharacterFigurePresenter>().Length);
        }

        [Test]
        public void RegistrationFillsAnEmptySeatAndNeverReplacesADouble()
        {
            CharacterFigurePresentation.Service = null;
            CharacterFigurePresentationService.EnsureRegistered();
            Assert.IsInstanceOf<CharacterFigurePresentationService>(CharacterFigurePresentation.Service);

            var fake = new FakeService();
            CharacterFigurePresentation.Service = fake;
            CharacterFigurePresentationService.EnsureRegistered();
            Assert.AreSame(fake, CharacterFigurePresentation.Service);
        }

        // =================================================================== helpers

        private CharacterFigurePresenter Attach()
        {
            ICharacterFigure attached = new CharacterFigurePresentationService().Attach(_characterGo, _stand);
            Assert.IsInstanceOf<CharacterFigurePresenter>(attached, "harness: a skinned character gets the presenter");
            return (CharacterFigurePresenter)attached;
        }

        private void AssertDraws(CharacterFigurePresenter presenter)
        {
            Assert.AreEqual(CharacterFigurePresenter.Refusal.None, presenter.WhyNot, presenter.NotDrawingReason);
            Assert.IsNull(presenter.NotDrawingReason);
            Assert.IsTrue(presenter.DrawsInsteadOfSprite);
            Assert.IsTrue(presenter.HidesSprite);
            Assert.IsTrue(_sprite.forceRenderingOff, "the drawn figure must hide the sprite");
            Assert.IsNotNull(presenter.Figure);
            Assert.IsTrue(presenter.Figure.Visible);
        }

        private void AssertSprite(CharacterFigurePresenter presenter, CharacterFigurePresenter.Refusal why)
        {
            Assert.AreEqual(why, presenter.WhyNot, presenter.NotDrawingReason);
            Assert.IsNotNull(presenter.NotDrawingReason, "a refusal must be explained");
            Assert.IsFalse(presenter.DrawsInsteadOfSprite);
            Assert.IsFalse(presenter.HidesSprite);
            Assert.IsFalse(_sprite.forceRenderingOff, "the sprite must draw whenever the figure does not");
        }

        private T Track<T>(T made) where T : Object
        {
            _made.Add(made);
            return made;
        }

        // =================================================================== the synthetic skin

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

        private CharacterSkinDef MakeSkin()
        {
            var bind = Track(new Mesh { name = "HHTestCastBind", hideFlags = HideFlags.HideAndDontSave });
            bind.SetVertices(new[]
            {
                new Vector3(-0.2f, 0f, 0f), new Vector3(0.2f, 0f, 0f),
                new Vector3(0.2f, 0f, 1.6f), new Vector3(-0.2f, 0f, 1.6f),
            });
            bind.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            bind.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            bind.boneWeights = new[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                new BoneWeight { boneIndex0 = 0, weight0 = 1f },
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

            var keys = new CharacterSkinDef.BoneKey[4 * Bones];
            for (int f = 0; f < 4; f++)
                for (int b = 0; b < Bones; b++)
                    keys[f * Bones + b] = new CharacterSkinDef.BoneKey
                    {
                        Position = new Vector3(0f, 0f, 0.8f * b + 0.01f * f),
                        Rotation = Quaternion.identity,
                    };

            var bayer = new float[16];
            for (int i = 0; i < 16; i++) bayer[i] = (i + 0.5f) / 16f;

            var skin = Track(ScriptableObject.CreateInstance<CharacterSkinDef>());
            skin.Id = "charskin.test_cast";
            skin.BindMesh = bind;
            skin.Bones = new[]
            {
                new CharacterSkinDef.Bone { Id = "root", Parent = CharacterSkinDef.NoBone, OwnsVertex = true },
                new CharacterSkinDef.Bone { Id = "spine", Parent = 0, OwnsVertex = true },
                new CharacterSkinDef.Bone { Id = "head", Parent = 1, OwnsVertex = true },
            };
            skin.MaxInfluences = 2;
            skin.Materials = new[]
            {
                new CharacterSkinDef.Material { Name = "skin", Colors = RampA, Offset = 0 },
                new CharacterSkinDef.Material { Name = "oilskin", Colors = RampB, Offset = 3 },
            };
            skin.Bayer16 = bayer;
            skin.Keyline = new Color32(12, 14, 18, 255);
            skin.LightN = new Vector3(0.3f, -0.5f, 0.81f);
            skin.Gain = 1.15f;
            skin.Bias = -0.04f;
            skin.CellW = 64;
            skin.CellH = 92;
            skin.PivotPx = new Vector2(32f, 80f);
            skin.PxPerMetre = 32;
            skin.ElevationDeg = 40f;
            skin.AzimuthCounterClockwise = true;
            skin.Clips = new[]
            {
                new CharacterSkinDef.SkinClip
                {
                    Anim = "idle", State = "idle", FramesPerSecond = 12f, Loop = true, FrameCount = 4, Keys = keys,
                },
            };
            skin.MeshStates = new[] { "idle" };
            return skin;
        }

        private static IsoFacetHullSetup SetupFrom(CharacterSkinDef skin) => new IsoFacetHullSetup
        {
            Mesh = skin.BindMesh,
            Ramps = new[] { RampA, RampB },
            RampOffsets = new[] { 0, 3 },
            LightN = skin.LightN,
            Gain = skin.Gain,
            Bias = skin.Bias,
            Bayer16 = skin.Bayer16,
            Keyline = skin.Keyline,
            PivotPx = skin.PivotPx,
            PxPerMetre = skin.PxPerMetre,
            CellW = skin.CellW,
            CellH = skin.CellH,
            ElevationDeg = skin.ElevationDeg,
        };
    }
}
