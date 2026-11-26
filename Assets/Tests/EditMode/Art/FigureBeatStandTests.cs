using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A villager's beat is drawn by the one frame rule</b> (ADR 0053 §11): a stand that answers
    /// <see cref="ICharacterFigureBeatStand"/> is asked after a route-driven clip and before plain work; a
    /// one-shot beat with no scrub plays from the beat's own start through
    /// <see cref="CharacterSkinPose.FrameFor"/>; a scrubbed beat shows <c>floor(scrub * count)</c>; and a beat
    /// the gates refuse falls to the work exactly as a refused work falls to the stance.
    /// </summary>
    public sealed class FigureBeatStandTests
    {
        private const string GinnyPath = "Assets/_Project/Data/Characters/Skin/ginny.asset";
        private const string RodMeshPath = "Assets/_Project/Data/Characters/HeldProps/RodCaneHeldMesh.asset";
        private const string DigState = "dig";
        private const string ReelState = "reel";
        private const string LandState = "land";
        private const string StrikeState = "strike";
        private const string CastReleaseState = "castRelease";
        private const string CastBackState = "castBack";
        private const string VillagerKey = "npc.test_angler";
        private const int WorldSeed = 11;

        private static readonly MethodInfo s_figureOnDestroy =
            typeof(IsoCharacterFigureRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<Object> _made = new List<Object>();
        private readonly List<IsoCharacterSprite> _characters = new List<IsoCharacterSprite>();
        private GameConfig _previousConfig;
        private IGameClock _previousClock;
        private IEnvironmentService _previousEnvironment;
        private IsoFacetIdPool _livePool;
        private GameConfig _config;
        private ScriptedClock _clock;

        /// <summary>A villager whose beat, work and route clip are all scripted by the test.</summary>
        private sealed class FakeBeatStand : ICharacterFigureBeatStand, ICharacterFigureAshoreClipStand
        {
            public IsoCharacterSprite Character;
            public string Key;

            public bool Working;
            public string WorkState;
            public HeldPropMeshDef WorkProp;

            public bool InBeat;
            public string BeatState;
            public double BeatStart;
            public float BeatScrub = float.NaN;
            public HeldPropMeshDef BeatProp;

            public bool Driven;
            public CharacterClip DrivenClip;
            public float DrivenProgress;

            public IsoCharacterSprite FigureCharacter => Character;
            public Transform FigureHull => null;
            public Vector3 FigureStandRigMetres => Vector3.zero;
            public float FigureDeckBearingDegrees => 0f;
            public string FigureKey => Key;

            public bool TryGetFigureWork(out string stateKey, out HeldPropMeshDef heldProp)
            {
                stateKey = Working ? WorkState : null;
                heldProp = Working ? WorkProp : null;
                return Working;
            }

            public bool TryGetFigureBeat(out string stateKey, out double beatStartSeconds, out float scrub01,
                                         out HeldPropMeshDef heldProp)
            {
                stateKey = InBeat ? BeatState : null;
                beatStartSeconds = InBeat ? BeatStart : 0d;
                scrub01 = InBeat ? BeatScrub : float.NaN;
                heldProp = InBeat ? BeatProp : null;
                return InBeat;
            }

            public bool TryGetFigureClip(out CharacterClip clip, out float progress)
            {
                clip = Driven ? DrivenClip : CharacterClip.None;
                progress = Driven ? DrivenProgress : 0f;
                return Driven;
            }
        }

        private sealed class ScriptedClock : IGameClock
        {
            public double TotalSeconds { get; private set; }
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
            _previousClock = GameServices.Clock;
            _previousEnvironment = GameServices.Environment;
            _config = Track(ScriptableObject.CreateInstance<GameConfig>());
            _config.MeshCast = true;
            _config.MeshCastAshore = true;
            GameServices.Config = _config;
            _clock = new ScriptedClock();
            GameServices.Clock = _clock;
            GameServices.Environment = new ScriptedWorld(WorldSeed);
            _livePool = IsoFacetHullRegistry.SwapIdPoolForTests(new IsoFacetIdPool());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (IsoCharacterSprite c in _characters)
            {
                if (c == null) continue;
                foreach (IsoCharacterFigureRenderer figure in c.GetComponentsInChildren<IsoCharacterFigureRenderer>(true))
                {
                    figure.LeaveAshore();
                    if (s_figureOnDestroy != null) s_figureOnDestroy.Invoke(figure, null);
                }
            }
            _characters.Clear();
            if (_livePool != null) IsoFacetHullRegistry.SwapIdPoolForTests(_livePool);
            _livePool = null;
            GameServices.Config = _previousConfig;
            GameServices.Clock = _previousClock;
            GameServices.Environment = _previousEnvironment;
            for (int i = _made.Count - 1; i >= 0; i--)
                if (_made[i] != null) Object.DestroyImmediate(_made[i]);
            _made.Clear();
        }

        [Test]
        public void ABeat_IsDrawnOverHerWork_AndHerWorkComesBackWhenTheBeatEnds()
        {
            FakeBeatStand stand = Stand(out CharacterFigurePresenter presenter);
            stand.Working = true;
            stand.WorkState = DigState;
            presenter.PoseFigure(stand, aboard: false);
            Assert.AreEqual(DigState, presenter.AshoreFigure.DrawnStateKey, "harness: the work is not drawn");

            stand.InBeat = true;
            stand.BeatState = ReelState;
            _clock.SeekTo(1d);
            presenter.PoseFigure(stand, aboard: false);
            Assert.AreEqual(ReelState, presenter.AshoreFigure.DrawnStateKey, "a beat must be drawn over the work");

            stand.InBeat = false;
            _clock.SeekTo(2d);
            presenter.PoseFigure(stand, aboard: false);
            Assert.AreEqual(DigState, presenter.AshoreFigure.DrawnStateKey, "the work must come back after the beat");
        }

        [Test]
        public void ARouteDrivenClip_IsDrawnOverABeat()
        {
            FakeBeatStand stand = Stand(out CharacterFigurePresenter presenter);
            CharacterSkinDef skin = Load<CharacterSkinDef>(GinnyPath);
            CharacterClip driven = CharacterClip.None;
            foreach (CharacterClip c in new[] { CharacterClip.Board, CharacterClip.BoardDown, CharacterClip.LadderDown,
                                                CharacterClip.Haul, CharacterClip.Bench, CharacterClip.Chop,
                                                CharacterClip.Lift, CharacterClip.Place, CharacterClip.Toss })
            {
                string key = CharacterClipStateMap.StateKeyFor(c);
                if (key != null && skin.DrawsAsMesh(key) && skin.TryGetClip(key, out CharacterSkinDef.SkinClip clip)
                    && clip.KeysWellFormed(skin.BoneCount)) { driven = c; break; }
            }
            Assume.That(driven != CharacterClip.None, "ginny has no meshed route clip to drive");

            stand.InBeat = true;
            stand.BeatState = ReelState;
            stand.Driven = true;
            stand.DrivenClip = driven;
            stand.DrivenProgress = 0.5f;
            presenter.PoseFigure(stand, aboard: false);
            Assert.AreEqual(CharacterClipStateMap.StateKeyFor(driven), presenter.AshoreFigure.DrawnStateKey,
                            "a route-driven clip must be drawn over a beat");
        }

        [Test]
        public void AOneShotBeat_WithNoScrub_PlaysFromTheBeatsOwnStart_ByTheOneFrameRule()
        {
            FakeBeatStand stand = Stand(out CharacterFigurePresenter presenter);
            CharacterSkinDef skin = Load<CharacterSkinDef>(GinnyPath);
            // The first clip ginny plays once: a fishing beat if the bake made one a one-shot (which ones loop
            // is the bake's call, not this test's; today they all loop), else a one-shot work clip. The rule
            // under test is the beat start, whatever the clip.
            string oneShot = null;
            CharacterSkinDef.SkinClip clip = default;
            foreach (string key in new[] { StrikeState, CastReleaseState, LandState, "toss", "place", "lift" })
                if (skin.DrawsAsMesh(key) && skin.TryGetClip(key, out clip) && !clip.Loop && clip.FrameCount > 2)
                { oneShot = key; break; }
            Assert.IsNotNull(oneShot, "harness: ginny has no meshed one-shot clip to test a beat's start with");

            // He walks into view mid-beat: the beat began well before this presenter first saw him.
            const double beatStart = 100d;
            stand.InBeat = true;
            stand.BeatState = oneShot;
            stand.BeatStart = beatStart;
            int seed = CharacterFigurePresenter.AshorePhaseSeed(WorldSeed, FigureKeyHash.Of(VillagerKey));
            float fps = clip.FramesPerSecond > 0f ? clip.FramesPerSecond : 12f;
            var seen = new HashSet<int>();
            for (int i = 0; i < clip.FrameCount + 3; i++)
            {
                double now = beatStart + 0.5d / fps + i / (double)fps;
                _clock.SeekTo(now);
                presenter.PoseFigure(stand, aboard: false);
                Assert.AreEqual(oneShot, presenter.AshoreFigure.DrawnStateKey);
                int expected = CharacterSkinPose.FrameFor(clip, seed, now, beatStart);
                Assert.AreEqual(expected, presenter.AshoreFigure.DrawnFrame, $"step {i}: not the one frame rule from the beat's start");
                seen.Add(presenter.AshoreFigure.DrawnFrame);
            }
            Assert.Greater(seen.Count, 2, "harness: the beat did not move through its frames");
        }

        [Test]
        public void AScrubbedBeat_ShowsTheFrameItsScrubSays()
        {
            FakeBeatStand stand = Stand(out CharacterFigurePresenter presenter);
            CharacterSkinDef skin = Load<CharacterSkinDef>(GinnyPath);
            Assert.IsTrue(skin.TryGetClip(CastBackState, out CharacterSkinDef.SkinClip clip), "harness: no castBack clip");
            int n = clip.FrameCount;
            stand.InBeat = true;
            stand.BeatState = CastBackState;
            foreach (float scrub in new[] { 0f, 0.26f, 0.5f, 0.74f, 0.999f, 1f })
            {
                stand.BeatScrub = scrub;
                _clock.SeekTo(_clock.TotalSeconds + 1d);   // the clock moves; the scrub alone picks the frame
                presenter.PoseFigure(stand, aboard: false);
                int expected = Mathf.Clamp(Mathf.FloorToInt(scrub * n), 0, n - 1);
                Assert.AreEqual(expected, presenter.AshoreFigure.DrawnFrame, $"scrub {scrub}");
            }
        }

        [Test]
        public void ABeatTheGatesRefuse_FallsToHerWork_AsARefusedWorkFallsToHerStance()
        {
            FakeBeatStand stand = Stand(out CharacterFigurePresenter presenter);
            stand.Working = true;
            stand.WorkState = DigState;
            stand.InBeat = true;

            // A beat the skin has no clip for.
            stand.BeatState = "no_such_beat";
            presenter.PoseFigure(stand, aboard: false);
            Assert.AreEqual(DigState, presenter.AshoreFigure.DrawnStateKey, "an undrawable beat must fall to the work");

            // A beat with a tool while the hand-tool switch is off.
            stand.BeatState = ReelState;
            stand.BeatProp = Load<HeldPropMeshDef>(RodMeshPath);
            _config.MeshHandToolInHand = false;
            presenter.PoseFigure(stand, aboard: false);
            Assert.AreEqual(DigState, presenter.AshoreFigure.DrawnStateKey, "a tool beat with the switch off must fall to the work");
            Assert.IsFalse(presenter.WorkPropDrawn, "no tool may draw for a refused beat");

            // And with no work under it, she stands.
            stand.Working = false;
            presenter.PoseFigure(stand, aboard: false);
            Assert.AreEqual(CharacterFigurePresenter.Refusal.None, presenter.WhyNot, presenter.NotDrawingReason);
            Assert.AreNotEqual(ReelState, presenter.AshoreFigure.DrawnStateKey);
            Assert.AreNotEqual(DigState, presenter.AshoreFigure.DrawnStateKey);
        }

        // ================================================================ harness

        private FakeBeatStand Stand(out CharacterFigurePresenter presenter)
        {
            var visual = Track(ScriptableObject.CreateInstance<CharacterVisualDef>());
            visual.Skin = Load<CharacterSkinDef>(GinnyPath);
            GameObject go = Track(new GameObject("FigureBeatStandTestsVillager"));
            IsoCharacterSprite character = go.AddComponent<IsoCharacterSprite>();
            character.Configure(visual);
            _characters.Add(character);
            var stand = new FakeBeatStand { Character = character, Key = VillagerKey };
            ICharacterFigure attached = new CharacterFigurePresentationService().Attach(go, stand);
            Assert.IsInstanceOf<CharacterFigurePresenter>(attached, "harness: a skinned character gets the presenter");
            presenter = (CharacterFigurePresenter)attached;
            return stand;
        }

        private T Track<T>(T made) where T : Object
        {
            _made.Add(made);
            return made;
        }

        private static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"harness: no {typeof(T).Name} at {path}");
            return asset;
        }
    }
}
