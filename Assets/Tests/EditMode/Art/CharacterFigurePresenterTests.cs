using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Is = UnityEngine.TestTools.Constraints.Is;
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
    ///
    /// <para><b>Ashore, a villager draws too</b> (amendment 2026-09-27): a stand that says who she is
    /// (<see cref="ICharacterFigureAshoreStand"/>) gets her own figure with her own facet id, and a plain
    /// stand still refuses. Those guards take their ids from a pool of the test's own and give every one
    /// back in <c>TearDown</c>, because EditMode runs no <c>OnDisable</c> or <c>OnDestroy</c> for the
    /// figure. The live pool goes back BEFORE anything is destroyed: the fixture's hull runs in edit mode,
    /// took its id and fore block from the live pool in <c>SetUp</c>, and returns them on its
    /// <c>OnDisable</c>.</para>
    /// </summary>
    public sealed class CharacterFigurePresenterTests
    {
        private const int Bones = 3;
        private const int RestSortingOrder = 7;
        private const float StandBearing = 90f;

        // Ashore: two villagers' keys, and the synthetic idle clip's shape (MakeSkin: 4 frames at 12 fps).
        private const string KeyA = "npc.test_a";
        private const string KeyB = "npc.test_b";
        private const float VillagerHeading = 135f;
        private const int IdleFrameCount = 4;
        private const double IdleFrameSeconds = 1d / 12d;
        private const int MeasuredFrames = 48;

        private static readonly Vector3 StandPoint = new Vector3(0.3f, -0.2f, 0.5f);
        private static readonly Regex FigureExhausted = new Regex(@"this figure gets NO facet id");

        private readonly List<Object> _made = new List<Object>();
        private GameConfig _previousConfig;
        private ICharacterFigurePresentationService _previousService;
        private IGameClock _previousClock;
        private IEnvironmentService _previousEnvironment;
        private IsoFacetIdPool _livePool;
        private object _sink;

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

        private sealed class NamedStand : ICharacterFigureStand, ICharacterFigureIdentity
        {
            public IsoCharacterSprite Character;
            public Transform Hull;
            public Vector3 Point;
            public float Bearing;
            public string Key;

            public IsoCharacterSprite FigureCharacter => Character;
            public Transform FigureHull => Hull;
            public Vector3 FigureStandRigMetres => Point;
            public float FigureDeckBearingDegrees => Bearing;
            public string FigureKey => Key;
        }

        private sealed class FakeService : ICharacterFigurePresentationService
        {
            public ICharacterFigure Attach(GameObject host, ICharacterFigureStand stand) => null;
        }

        /// <summary>A villager on her own feet: a sprite and a key, and no hull.</summary>
        private sealed class FakeAshoreStand : ICharacterFigureAshoreStand
        {
            public IsoCharacterSprite Character;
            public string Key;

            public IsoCharacterSprite FigureCharacter => Character;
            public Transform FigureHull => null;
            public Vector3 FigureStandRigMetres => Vector3.zero;
            public float FigureDeckBearingDegrees => 0f;
            public string FigureKey => Key;
        }

        private sealed class ScriptedClock : IGameClock
        {
            public double TotalSeconds { get; private set; }
            public void Advance(double dt) => TotalSeconds += dt;
            public GameTime Now => new GameTime(TotalSeconds);
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public int DayIndex => 0;
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float DayFraction => 0f;
            public float HourOfDay => 12f;
            public void SeekTo(double totalSeconds) => TotalSeconds = totalSeconds;
        }

        /// <summary>A world that is only its seed: the one thing the presenter reads from it.</summary>
        private sealed class ScriptedWorld : IEnvironmentService
        {
            public ScriptedWorld(int worldSeed) => WorldSeed = worldSeed;
            public int WorldSeed { get; }
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => 0f;
        }

        [SetUp]
        public void SetUp()
        {
            _previousConfig = GameServices.Config;
            _previousService = CharacterFigurePresentation.Service;
            _previousClock = GameServices.Clock;
            _previousEnvironment = GameServices.Environment;

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
            // EditMode runs neither OnDisable nor OnDestroy for a figure: every ashore id goes back here,
            // to the pool it came from. A no-op for a figure that never went ashore.
            foreach (Object made in _made)
                if (made is GameObject go && go != null)
                    foreach (IsoCharacterFigureRenderer figure in go.GetComponentsInChildren<IsoCharacterFigureRenderer>(true))
                        figure.LeaveAshore();
            // Then the live pool, BEFORE anything is destroyed: the hull gives its SetUp ids back on OnDisable.
            if (_livePool != null) IsoFacetHullRegistry.SwapIdPoolForTests(_livePool);
            _livePool = null;

            GameServices.Config = _previousConfig;
            CharacterFigurePresentation.Service = _previousService;
            GameServices.Clock = _previousClock;
            GameServices.Environment = _previousEnvironment;
            _sink = null;
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
        public void APlainStandAshoreNeverDrawsAndBuildsNothing()
        {
            UseFreshIdPool();
            _config.MeshCastAshore = true;   // both switches on: the refusal below is the stand's, not a switch's
            int figures = IsoFacetHullRegistry.FigureCount;
            CharacterFigurePresenter presenter = Attach();

            presenter.PoseFigure(_stand, aboard: false);

            AssertSprite(presenter, CharacterFigurePresenter.Refusal.Ashore);
            Assert.IsNull(presenter.Figure);
            Assert.IsNull(presenter.AshoreFigure, "only a stand that says who she is is drawn ashore");
            Assert.AreEqual(0, _hull.PosedMesh.childCount);
            Assert.AreEqual(0, _characterGo.transform.childCount, "nothing may be built under the sprite either");
            Assert.AreEqual(figures, IsoFacetHullRegistry.FigureCount, "a plain stand took a figure id");

            // The same character under the same switches, stood again as a villager, draws. So the refusal
            // above was the stand's kind, not a fixture that cannot draw ashore.
            FakeAshoreStand villager = AshoreStand(_character, KeyA);
            presenter.Configure(villager);
            presenter.PoseFigure(villager, aboard: false);
            AssertDrawsAshore(presenter, _sprite);
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

        // =================================================================== the figure's life (character PR 2a)

        [Test]
        public void TheStandsOwnIdentityKeysTheFiguresLife_AndNoIdentityKeysNothing()
        {
            CharacterFigurePresenter presenter = Attach();
            presenter.PoseFigure(_stand, aboard: true);
            AssertDraws(presenter);
            Assert.AreEqual(string.Empty, presenter.FigureLife.Key, "a stand with no identity keyed a life");

            var named = new NamedStand
            {
                Character = _character, Hull = _hullGo.transform, Point = StandPoint, Bearing = StandBearing,
                Key = "npc.test_skipper",
            };
            presenter.PoseFigure(named, aboard: true);
            AssertDraws(presenter);
            Assert.AreEqual("npc.test_skipper", presenter.FigureLife.Key,
                            "the figure's life must be keyed by the stand's one identity, never a key of its own");
            Assert.AreEqual(CharacterFigureBlink.SeedFor(_skin.Id, "npc.test_skipper"), presenter.FigureLife.Blink.Seed,
                            "the blink must be seeded by the def and that identity");
        }

        [Test]
        public void ASkipperWithALook_LooksAtThePublishedPlayerNearby_AndNotAtOneFarOff()
        {
            _skin.LookChestBone = 1;
            _skin.LookNeckBone = 2;
            _skin.LookHeadBone = 2;
            _skin.LookSplitNeck = 0.4f;
            _skin.LookSplitHead = 0.6f;
            _skin.LookYawLimits = new Vector2(-60f, 60f);
            _skin.LookPitchLimits = new Vector2(-30f, 30f);
            _skin.LookHeadShare = 0.7f;
            _skin.LookEyesBeyondDeg = 8f;
            Assert.IsTrue(_skin.HasLook && _skin.IsUsable(), "harness: the skin must carry a look and stay usable");

            // Where the figure will stand: the stand's rig point under the hull's posed mesh, yawed by the
            // bearing (the first test above holds the presenter to exactly this).
            Matrix4x4 figureWorld = _hull.PosedMesh.localToWorldMatrix *
                                    Matrix4x4.TRS(StandPoint, Quaternion.AngleAxis(-StandBearing, Vector3.forward), Vector3.one);
            GameObject player = Track(new GameObject("TestPlayer"));
            player.transform.position = figureWorld.MultiplyPoint3x4(new Vector3(0.8f, 1.5f, 0f));
            GameServices.PlayerTransform = player.transform;
            try
            {
                CharacterFigurePresenter presenter = Attach();
                presenter.PoseFigure(_stand, aboard: true);
                AssertDraws(presenter);
                Assert.Greater(presenter.Figure.DrawnLookYaw, 0.0,
                               "the skipper did not turn toward a player a step ahead and to her right");

                player.transform.position = figureWorld.MultiplyPoint3x4(new Vector3(20f, 1.5f, 0f));
                presenter.Configure(_stand);   // a fresh figure, so the look is sampled again
                presenter.PoseFigure(_stand, aboard: true);
                AssertDraws(presenter);
                Assert.AreEqual(0.0, presenter.Figure.DrawnLookYaw, "the skipper turned toward a player past the radius");
                Assert.AreEqual(CharacterFigureLook.GazeOpen, presenter.Figure.DrawnGaze);
            }
            finally
            {
                GameServices.PlayerTransform = null;
            }
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

        // =================================================================== ashore: a villager on her own feet

        [Test]
        public void AnAshoreStandDrawsHerOwnFigureAndItsOverlayTakesHerSpritesSortInTheSameFrame()
        {
            UseFreshIdPool();
            _config.MeshCastAshore = true;
            FakeAshoreStand stand = AshoreStand(_character, KeyA);
            CharacterFigurePresenter presenter = Attach(_characterGo, stand);
            int figures = IsoFacetHullRegistry.FigureCount;
            // EditMode runs no LateUpdate: a hold and a release is how a heading is written here.
            _character.HoldHeading(VillagerHeading);
            _character.ReleaseHeading();

            presenter.PoseFigure(stand, aboard: false);

            AssertDrawsAshore(presenter, _sprite);
            IsoCharacterFigureRenderer figure = presenter.AshoreFigure;
            Assert.That(figure.FigureId, Is.InRange(1, 254), "EnterAshore must have given her one figure id");
            Assert.AreEqual(figures + 1, IsoFacetHullRegistry.FigureCount, "she takes exactly one figure id");
            Assert.AreSame(_characterGo.transform, figure.transform.parent,
                           "her figure stands under her own sprite, at its pivot: her feet");
            Assert.AreEqual(CharacterFigurePresenter.AshoreFigureObjectName, figure.gameObject.name);
            Assert.AreEqual(_characterGo.layer, figure.gameObject.layer,
                            "her overlay must be on her sprite's layer, the camera that draws her");
            Assert.AreEqual(0, _hull.PosedMesh.childCount, "nothing ashore may stand in a hull");
            // The fixture's skin is CCW, so her compass heading yaws the other way about +z.
            Assert.AreEqual(-VillagerHeading, presenter.AshoreYawDegrees, 1e-4f);
            Assert.That(Quaternion.Angle(Quaternion.AngleAxis(-VillagerHeading, Vector3.forward),
                                         FacetChildOf(figure).localRotation), Is.LessThan(0.01f),
                        "the yaw must be on her mesh, not only in the readout");

            // What YSortSprite does every frame as she walks: re-sort her sprite. Aboard that is a restaging
            // and the sprite takes the draw back; ashore her figure follows it, in the same pose.
            _sprite.sortingOrder = RestSortingOrder + 40;
            presenter.PoseFigure(stand, aboard: false);

            AssertDrawsAshore(presenter, _sprite);
            Assert.AreSame(figure, presenter.AshoreFigure, "a re-sort must not rebuild her");
            Assert.AreEqual(RestSortingOrder + 40, figure.AshoreOverlay.sortingOrder,
                            "the overlay must end the pose sorted exactly as her sprite, not a frame behind");
            Assert.AreEqual(RestSortingOrder + 40,
                            figure.AshoreOverlay.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder);
            Assert.AreEqual(RestSortingOrder + 40, _sprite.sortingOrder, "sorting is copied, never written");
            Assert.AreEqual(figures + 1, IsoFacetHullRegistry.FigureCount, "re-posing took a second id");
        }

        [Test]
        public void AtExhaustionSheKeepsHerWholeSpriteBuildsNothingAndIsNotAskedAgain()
        {
            UseFreshIdPool(DrainedToTheOverflowId());
            _config.MeshCastAshore = true;
            FakeAshoreStand stand = AshoreStand(_character, KeyA);
            CharacterFigurePresenter presenter = Attach(_characterGo, stand);
            int figures = IsoFacetHullRegistry.FigureCount;
            int warnings = 0;
            void CountRefusals(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Warning && FigureExhausted.IsMatch(message)) warnings++;
            }

            LogAssert.Expect(LogType.Warning, FigureExhausted);
            Application.logMessageReceived += CountRefusals;
            try
            {
                presenter.PoseFigure(stand, aboard: false);
                AssertRefusedAtExhaustion(presenter, figures);

                // Thirty LateUpdates on. The pool has said no once, and is not asked again.
                for (int i = 0; i < 30; i++) presenter.PoseFigure(stand, aboard: false);
            }
            finally
            {
                Application.logMessageReceived -= CountRefusals;
            }

            AssertRefusedAtExhaustion(presenter, figures);
            Assert.AreEqual(1, warnings, "the pool warns once per ask: she asked again after it refused her");

            // A switch turned off and on again forgets the refusal, and she asks once more.
            _config.MeshCastAshore = false;
            presenter.PoseFigure(stand, aboard: false);
            AssertSprite(presenter, CharacterFigurePresenter.Refusal.SwitchOff);
            Assert.IsFalse(presenter.AshoreRefused, "a switch turned off must forget the refusal");

            _config.MeshCastAshore = true;
            LogAssert.Expect(LogType.Warning, FigureExhausted);
            presenter.PoseFigure(stand, aboard: false);
            AssertRefusedAtExhaustion(presenter, figures);
        }

        [Test]
        public void ShelterHidesHerFigureAndKeepsHerIdAndSheComesOutWithTheSameOne()
        {
            UseFreshIdPool();
            _config.MeshCastAshore = true;
            FakeAshoreStand stand = AshoreStand(_character, KeyA);
            CharacterFigurePresenter presenter = Attach(_characterGo, stand);
            presenter.PoseFigure(stand, aboard: false);
            AssertDrawsAshore(presenter, _sprite);
            AssertNeverBoth(presenter, _sprite);
            IsoCharacterFigureRenderer figure = presenter.AshoreFigure;
            int id = figure.FigureId;
            int figures = IsoFacetHullRegistry.FigureCount;

            _sprite.enabled = false;   // what her routine's ApplyShelter does at a door
            for (int i = 0; i < 3; i++)
            {
                presenter.PoseFigure(stand, aboard: false);

                AssertSprite(presenter, CharacterFigurePresenter.Refusal.SpriteDisabled);
                AssertNeverBoth(presenter, _sprite);
                Assert.IsFalse(_sprite.enabled, "the presenter must leave a hidden sprite hidden");
                Assert.AreSame(figure, presenter.AshoreFigure, "a door must not cost her figure");
                Assert.IsFalse(figure.Visible, "sheltered, she is hidden in both pictures");
                Assert.AreEqual(id, figure.FigureId, "sheltered, she keeps her id");
                Assert.AreEqual(figures, IsoFacetHullRegistry.FigureCount);
            }

            _sprite.enabled = true;
            presenter.PoseFigure(stand, aboard: false);

            AssertDrawsAshore(presenter, _sprite);
            AssertNeverBoth(presenter, _sprite);
            Assert.AreSame(figure, presenter.AshoreFigure);
            Assert.AreEqual(id, figure.FigureId, "she comes out with the id she went in with");
            Assert.AreEqual(figures, IsoFacetHullRegistry.FigureCount);
        }

        [Test]
        public void TheAshoreSwitchOffGivesHerSpriteBackAndHerIdBack()
        {
            AssertASwitchGivesItAllBack(() => _config.MeshCastAshore = false, () => _config.MeshCastAshore = true,
                                        "GameConfig.MeshCastAshore is off");
        }

        [Test]
        public void TheCastSwitchOffGivesHerSpriteBackAndHerIdBackWithTheAshoreSwitchStillOn()
        {
            AssertASwitchGivesItAllBack(() => _config.MeshCast = false, () => _config.MeshCast = true,
                                        "GameConfig.MeshCast is off");
            Assert.IsTrue(_config.MeshCastAshore, "harness: the ashore switch stayed on throughout");
        }

        [Test]
        public void ASwitchTurnedOffWhileSheIsIndoorsStillGivesHerIdBack()
        {
            UseFreshIdPool();
            _config.MeshCastAshore = true;
            FakeAshoreStand stand = AshoreStand(_character, KeyA);
            CharacterFigurePresenter presenter = Attach(_characterGo, stand);
            int figures = IsoFacetHullRegistry.FigureCount;
            presenter.PoseFigure(stand, aboard: false);
            AssertDrawsAshore(presenter, _sprite);
            _sprite.enabled = false;
            presenter.PoseFigure(stand, aboard: false);
            AssertSprite(presenter, CharacterFigurePresenter.Refusal.SpriteDisabled);
            Assert.AreEqual(figures + 1, IsoFacetHullRegistry.FigureCount, "harness: sheltered, she holds her id");

            _config.MeshCastAshore = false;
            presenter.PoseFigure(stand, aboard: false);

            AssertSprite(presenter, CharacterFigurePresenter.Refusal.SwitchOff);
            Assert.IsTrue(presenter.AshoreFigure == null,
                          "the switch is read before the shelter: her figure must go while she is indoors too");
            Assert.AreEqual(figures, IsoFacetHullRegistry.FigureCount, "a switch turned off indoors kept her id");
            Assert.AreEqual(0, _characterGo.transform.childCount);
            Assert.IsFalse(_sprite.enabled, "her shelter is her owner's: still hidden");
        }

        [Test]
        public void TwoVillagersOfOneSkinPoseDifferentIdleFramesAtOneMomentAndOneKeyPosesTheSameFrameTwice()
        {
            UseFreshIdPool();
            ScriptedClock clock = UseScriptedTime(worldSeed: 0);
            _config.MeshCastAshore = true;
            IsoCharacterSprite twin = MakeVillager("TestVillagerA2");
            IsoCharacterSprite other = MakeVillager("TestVillagerB");
            SpriteRenderer twinSprite = twin.GetComponent<SpriteRenderer>();
            SpriteRenderer otherSprite = other.GetComponent<SpriteRenderer>();
            FakeAshoreStand standA = AshoreStand(_character, KeyA);
            FakeAshoreStand standTwin = AshoreStand(twin, KeyA);
            FakeAshoreStand standB = AshoreStand(other, KeyB);
            CharacterFigurePresenter a = Attach(_characterGo, standA);
            CharacterFigurePresenter aTwice = Attach(twin.gameObject, standTwin);
            CharacterFigurePresenter b = Attach(other.gameObject, standB);

            for (int tick = 0; tick < IdleFrameCount; tick++)
            {
                // Mid-interval, so FrameFor's floor never sits on a frame boundary.
                clock.SeekTo((tick + 0.5d) * IdleFrameSeconds);
                a.PoseFigure(standA, aboard: false);
                aTwice.PoseFigure(standTwin, aboard: false);
                b.PoseFigure(standB, aboard: false);

                int frameA = PosedIdleFrame(a, _sprite);
                int frameB = PosedIdleFrame(b, otherSprite);
                Assert.AreNotEqual(frameA, frameB, $"tick {tick}: two villagers of one skin idled in step");
                Assert.AreEqual(frameA, PosedIdleFrame(aTwice, twinSprite),
                                $"tick {tick}: one key posed two different frames at one moment");
                if (tick == 0)
                {
                    Assert.AreEqual(2, frameA, "npc.test_a's phase in world 0 (pinned in ThePhaseMixIsPinned)");
                    Assert.AreEqual(1, frameB, "npc.test_b's phase in world 0");
                }
            }

            // The world seed is read, not assumed: in world -7 the two phases trade places.
            GameServices.Environment = new ScriptedWorld(-7);
            clock.SeekTo(0.5d * IdleFrameSeconds);
            a.PoseFigure(standA, aboard: false);
            b.PoseFigure(standB, aboard: false);
            Assert.AreEqual(1, PosedIdleFrame(a, _sprite), "npc.test_a's phase in world -7");
            Assert.AreEqual(2, PosedIdleFrame(b, otherSprite), "npc.test_b's phase in world -7");
        }

        [Test]
        public void ThePhaseMixIsPinned()
        {
            // Her key: FNV-1a over its chars, and 0 for no key.
            Assert.AreEqual(0x9944F2BAu, CharacterFigurePresenter.KeyHash(KeyA));
            Assert.AreEqual(0x9844F127u, CharacterFigurePresenter.KeyHash(KeyB));
            Assert.AreEqual(0u, CharacterFigurePresenter.KeyHash(null));
            Assert.AreEqual(0u, CharacterFigurePresenter.KeyHash(""));

            // The mix: MurmurHash3's finalizer over (world seed XOR key hash), read back as a signed seed.
            Assert.AreEqual(1714630061, CharacterFigurePresenter.AshorePhaseSeed(0, 0x9944F2BAu));
            Assert.AreEqual(485244004, CharacterFigurePresenter.AshorePhaseSeed(0, 0x9844F127u));
            Assert.AreEqual(1149690312, CharacterFigurePresenter.AshorePhaseSeed(-7, 0x9944F2BAu));
            Assert.AreEqual(-1456048451, CharacterFigurePresenter.AshorePhaseSeed(1, 0x9944F2BAu));

            // No key is the world seed itself: the phase aboard, untouched.
            foreach (int seed in new[] { 0, 1, -7, 12345, int.MinValue, int.MaxValue })
                Assert.AreEqual(seed, CharacterFigurePresenter.AshorePhaseSeed(seed, 0u), $"world {seed}, no key");

            // What those seeds mean for a four-frame idle: the phases the two-villager case reads.
            Assert.AreEqual(2, CharacterSkinPose.PhaseFrame(1714630061, "idle", IdleFrameCount));
            Assert.AreEqual(1, CharacterSkinPose.PhaseFrame(485244004, "idle", IdleFrameCount));
        }

        [Test]
        public void PosingAVillagerAshoreEveryFrameAllocatesNothingAfterWarmUp()
        {
            UseFreshIdPool();
            ScriptedClock clock = UseScriptedTime(worldSeed: 0);
            _config.MeshCastAshore = true;
            FakeAshoreStand stand = AshoreStand(_character, KeyA);
            CharacterFigurePresenter presenter = Attach(_characterGo, stand);
            clock.SeekTo(0.5d * IdleFrameSeconds);
            // Warm-up: the first pose builds her figure and takes her id, then every idle frame once.
            presenter.PoseFigure(stand, aboard: false);
            PoseFrames(presenter, stand, clock, IdleFrameCount);
            AssertDrawsAshore(presenter, _sprite);

            // The recorder must see an allocation here, or the negative below would pass on anything.
            Assert.That(() => { _sink = new object[64]; }, Is.AllocatingGCMemory(),
                        "harness: the GC.Alloc recorder saw no allocation at all");
            Assert.That(() => PoseFrames(presenter, stand, clock, MeasuredFrames), Is.Not.AllocatingGCMemory(),
                        "a steady ashore pose allocated (rule 7: nothing per frame)");
            AssertDrawsAshore(presenter, _sprite);   // the loop measured the drawing path, not a refusal

            // The same loop on the thread's allocation counter, where this runtime keeps one.
            long before = GC.GetAllocatedBytesForCurrentThread();
            _sink = new object[64];
            if (GC.GetAllocatedBytesForCurrentThread() - before <= 0)
            {
                Debug.Log("[CharacterFigurePresenterTests] GC.GetAllocatedBytesForCurrentThread counts nothing " +
                          "on this runtime, so its arm is skipped. The GC.Alloc recorder above is the measurement.");
                return;
            }
            before = GC.GetAllocatedBytesForCurrentThread();
            PoseFrames(presenter, stand, clock, MeasuredFrames);
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before,
                            "a steady ashore pose allocated bytes on the thread's own counter");
        }

        // =================================================================== helpers

        private CharacterFigurePresenter Attach() => Attach(_characterGo, _stand);

        private static CharacterFigurePresenter Attach(GameObject host, ICharacterFigureStand stand)
        {
            ICharacterFigure attached = new CharacterFigurePresentationService().Attach(host, stand);
            Assert.IsInstanceOf<CharacterFigurePresenter>(attached, "harness: a skinned character gets the presenter");
            return (CharacterFigurePresenter)attached;
        }

        private static FakeAshoreStand AshoreStand(IsoCharacterSprite character, string key) =>
            new FakeAshoreStand { Character = character, Key = key };

        /// <summary>Another villager with the fixture's skin, on her own GameObject and at the rest sort.</summary>
        private IsoCharacterSprite MakeVillager(string name)
        {
            GameObject go = Track(new GameObject(name));
            var character = go.AddComponent<IsoCharacterSprite>();
            character.Configure(_visual);
            go.GetComponent<SpriteRenderer>().sortingOrder = RestSortingOrder;
            return character;
        }

        /// <summary>Ids for this test come from a pool of its own; TearDown puts the live one back.</summary>
        private void UseFreshIdPool(IsoFacetIdPool pool = null)
        {
            IsoFacetIdPool old = IsoFacetHullRegistry.SwapIdPoolForTests(pool ?? new IsoFacetIdPool());
            if (_livePool == null) _livePool = old;
        }

        /// <summary>A pool whose next id is 255: every single below it has been handed to a hull.</summary>
        private static IsoFacetIdPool DrainedToTheOverflowId()
        {
            var pool = new IsoFacetIdPool();
            for (int i = 1; i < 255; i++)
                Assert.AreEqual(i, pool.TakeId(), "harness: a fresh pool hands hull ids out in order from 1");
            return pool;
        }

        /// <summary>A clock of the test's own and a world that is only its seed; TearDown restores both.</summary>
        private static ScriptedClock UseScriptedTime(int worldSeed)
        {
            var clock = new ScriptedClock();
            GameServices.Clock = clock;
            GameServices.Environment = new ScriptedWorld(worldSeed);
            return clock;
        }

        private static void PoseFrames(CharacterFigurePresenter presenter, ICharacterFigureStand stand,
                                       ScriptedClock clock, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                clock.Advance(IdleFrameSeconds);
                presenter.PoseFigure(stand, aboard: false);
            }
        }

        private static int PosedIdleFrame(CharacterFigurePresenter presenter, SpriteRenderer sprite)
        {
            AssertDrawsAshore(presenter, sprite);
            IsoCharacterFigureRenderer figure = presenter.AshoreFigure;
            Assert.AreEqual("idle", figure.DrawnStateKey);
            Assert.IsFalse(figure.LastFrameWasFenced, "harness: every frame of the synthetic idle is honest");
            return figure.RequestedFrame;
        }

        private static Transform FacetChildOf(IsoCharacterFigureRenderer figure)
        {
            foreach (MeshRenderer r in figure.GetComponentsInChildren<MeshRenderer>(true))
                if (r.gameObject.name == "FacetFigure") return r.transform;
            Assert.Fail("harness: the figure has no FacetFigure child");
            return null;
        }

        private void AssertASwitchGivesItAllBack(Action switchOff, Action switchOn, string reason)
        {
            UseFreshIdPool();
            _config.MeshCastAshore = true;
            FakeAshoreStand stand = AshoreStand(_character, KeyA);
            CharacterFigurePresenter presenter = Attach(_characterGo, stand);
            int figures = IsoFacetHullRegistry.FigureCount;
            int layer = _sprite.sortingLayerID;
            presenter.PoseFigure(stand, aboard: false);
            AssertDrawsAshore(presenter, _sprite);

            switchOff();
            presenter.PoseFigure(stand, aboard: false);

            // The sprite exactly as the frame before the amendment leaves it.
            AssertSprite(presenter, CharacterFigurePresenter.Refusal.SwitchOff);
            Assert.AreEqual(reason, presenter.NotDrawingReason, "the refusal must name the switch that is off");
            Assert.IsTrue(presenter.AshoreFigure == null, "a switch off must take her figure away");
            Assert.AreEqual(0, _characterGo.transform.childCount, "nothing of hers may stay under the sprite");
            Assert.AreEqual(figures, IsoFacetHullRegistry.FigureCount, "a switch off must give her id back");
            Assert.IsFalse(presenter.AshoreRefused);
            Assert.IsTrue(_sprite.enabled);
            Assert.AreEqual(layer, _sprite.sortingLayerID);
            Assert.AreEqual(RestSortingOrder, _sprite.sortingOrder);

            switchOn();
            presenter.PoseFigure(stand, aboard: false);

            AssertDrawsAshore(presenter, _sprite);
            Assert.AreEqual(figures + 1, IsoFacetHullRegistry.FigureCount, "on again, she takes one id again");
        }

        private void AssertRefusedAtExhaustion(CharacterFigurePresenter presenter, int figuresBefore)
        {
            AssertSprite(presenter, CharacterFigurePresenter.Refusal.FacetIdRefused);
            Assert.IsTrue(presenter.AshoreRefused);
            Assert.IsTrue(presenter.AshoreFigure == null, "refused, she builds nothing");
            Assert.AreEqual(0, _characterGo.transform.childCount, "the figure made for the ask must go with the refusal");
            Assert.AreEqual(figuresBefore, IsoFacetHullRegistry.FigureCount, "a refused figure was counted");
            Assert.IsTrue(_sprite.enabled, "her whole sprite: its enabled flag is never written");
        }

        private static void AssertDrawsAshore(CharacterFigurePresenter presenter, SpriteRenderer sprite)
        {
            Assert.AreEqual(CharacterFigurePresenter.Refusal.None, presenter.WhyNot, presenter.NotDrawingReason);
            Assert.IsNull(presenter.NotDrawingReason);
            Assert.IsTrue(presenter.DrawsInsteadOfSprite);
            Assert.IsTrue(presenter.HidesSprite);
            Assert.IsTrue(sprite.forceRenderingOff, "her drawn figure must hide her sprite");
            Assert.IsTrue(sprite.enabled, "the presenter must never write SpriteRenderer.enabled");
            Assert.IsNull(presenter.Figure, "a villager ashore builds no aboard figure");
            IsoCharacterFigureRenderer figure = presenter.AshoreFigure;
            Assert.IsNotNull(figure, "an ashore stand that draws must have her ashore figure");
            Assert.IsTrue(figure.IsAshore, "EnterAshore must have given her a figure id");
            Assert.IsTrue(figure.Visible);
            Assert.IsNotNull(figure.AshoreOverlay);
            Assert.AreEqual(sprite.sortingLayerID, figure.AshoreOverlay.sortingLayerID);
            Assert.AreEqual(sprite.sortingOrder, figure.AshoreOverlay.sortingOrder,
                            "her overlay must end the pose sorted exactly as her sprite");
        }

        private static void AssertNeverBoth(CharacterFigurePresenter presenter, SpriteRenderer sprite)
        {
            bool spriteShows = sprite.enabled && !sprite.forceRenderingOff;
            bool figureShows = presenter.AshoreFigure != null && presenter.AshoreFigure.Visible;
            Assert.IsFalse(spriteShows && figureShows, "her sprite and her figure showed in the same frame");
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
