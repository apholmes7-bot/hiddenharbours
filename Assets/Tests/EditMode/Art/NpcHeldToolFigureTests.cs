using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A villager at work holds her tool as its facet mesh, and no sprite tool is drawn on her</b>
    /// (ADR 0053 §10): the drawing side. The shovel a digging villager holds is the player's own baked
    /// shovel, gripped at the right-hand tool socket and aimed by the dig clip's own tool track on every
    /// frame, riding her figure's facet; her body sprite is hidden while her figure draws, and nothing is
    /// allocated per frame. The data side is <c>NpcHeldToolMeshTests</c> in the EditMode assembly.
    /// </summary>
    public sealed class NpcHeldToolFigureTests
    {
        private const string GinnyPath = "Assets/_Project/Data/Characters/Skin/ginny.asset";
        private const string ShovelMeshPath = "Assets/_Project/Data/Characters/HeldProps/ShovelHeldMesh.asset";
        private const string DigState = "dig";
        private const string IdleState = "idle";
        private const string VillagerKey = "npc.test_digger";
        private const float LineWidthPx = GameConfig.DefaultHeldPropLineWidthPx;
        private const double FrameSeconds = 1d / 60d;
        private const int MeasuredFrames = 60;
        private const float AimTolerance = 1e-3f;
        private const float GripTolerance = 1e-5f;

        private static readonly MethodInfo s_figureOnDestroy =
            typeof(IsoCharacterFigureRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<Object> _made = new List<Object>();
        private readonly List<IsoCharacterFigureRenderer> _figures = new List<IsoCharacterFigureRenderer>();
        private readonly List<FigureHeldPropMesh> _drivers = new List<FigureHeldPropMesh>();
        private readonly List<IsoCharacterSprite> _characters = new List<IsoCharacterSprite>();
        private GameConfig _previousConfig;
        private IGameClock _previousClock;
        private IEnvironmentService _previousEnvironment;
        private IsoFacetIdPool _livePool;
        private GameConfig _config;
        private GameObject _home;
        private object _sink;

        /// <summary>A villager on her own feet whose work is scripted by the test.</summary>
        private sealed class FakeWorkStand : ICharacterFigureWorkStand
        {
            public IsoCharacterSprite Character;
            public string Key;
            public bool Working;
            public string State;
            public HeldPropMeshDef Prop;

            public IsoCharacterSprite FigureCharacter => Character;
            public Transform FigureHull => null;
            public Vector3 FigureStandRigMetres => Vector3.zero;
            public float FigureDeckBearingDegrees => 0f;
            public string FigureKey => Key;

            public bool TryGetFigureWork(out string stateKey, out HeldPropMeshDef heldProp)
            {
                stateKey = Working ? State : null;
                heldProp = Working ? Prop : null;
                return Working;
            }
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
            _home = Track(new GameObject("NpcHeldToolFigureTestsHome"));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (FigureHeldPropMesh d in _drivers) d.Dispose();
            _drivers.Clear();
            foreach (IsoCharacterFigureRenderer f in _figures)
            {
                if (f == null) continue;
                if (s_figureOnDestroy != null) s_figureOnDestroy.Invoke(f, null);
                Object.DestroyImmediate(f.gameObject);
            }
            _figures.Clear();
            foreach (IsoCharacterSprite c in _characters)
            {
                if (c == null) continue;
                foreach (IsoCharacterFigureRenderer figure in c.GetComponentsInChildren<IsoCharacterFigureRenderer>(true))
                    figure.LeaveAshore();
            }
            _characters.Clear();
            if (_livePool != null) IsoFacetHullRegistry.SwapIdPoolForTests(_livePool);
            _livePool = null;
            GameServices.Config = _previousConfig;
            GameServices.Clock = _previousClock;
            GameServices.Environment = _previousEnvironment;
            _sink = null;
            for (int i = _made.Count - 1; i >= 0; i--)
                if (_made[i] != null) Object.DestroyImmediate(_made[i]);
            _made.Clear();
        }

        // ================================================================ the driver

        [Test]
        public void TheVillagersShovel_EveryDigFrame_IsGrippedAtTheRightHandSocket_AndAimedByTheClip()
        {
            HeldPropMeshDef shovel = Load<HeldPropMeshDef>(ShovelMeshPath);
            IsoCharacterFigureRenderer figure = Figure(GinnyPath);
            FigureHeldPropMesh driver = Driver();
            Assert.IsTrue(HeldPropPose.TryParseSourcePose(shovel.SourcePose, out float bp, out float by));
            int socket = figure.SkinDef.IndexOfBone(HeldPropSockets.RightHandTool);
            Assert.AreNotEqual(CharacterSkinDef.NoBone, socket);
            Assert.IsTrue(figure.SkinDef.TryGetClip(DigState, out CharacterSkinDef.SkinClip clip));
            var failures = new List<string>();

            for (int f = 0; f < clip.FrameCount; f++)
            {
                Assert.IsTrue(figure.SetPose(DigState, f), $"harness: ginny did not pose dig {f}");
                Assert.IsTrue(driver.TryDraw(true, shovel, HeldPropSockets.RightHandTool, figure, LineWidthPx),
                              $"dig frame {f}: no mesh shovel ({driver.LastRefusal})");
                IsoFacetHeldPropRenderer r = driver.Renderer;
                Assert.AreSame(figure.FacetTransform, r.transform.parent, "the shovel must ride the figure's facet child");
                Assert.IsTrue(r.Visible);
                Assert.IsTrue(figure.TryBonePoint(socket, out Vector3 grip));
                if ((r.transform.localPosition - grip).magnitude > GripTolerance)
                    failures.Add($"dig {f}: the grip is off the right hand's socket");
                CharacterSkinDef.ToolKey key = clip.Tool[figure.DrawnFrame];
                Vector3 aim = r.transform.localRotation * HeldPropPose.ToolDirection(bp, by);
                if ((aim - HeldPropPose.ToolDirection(key.Pitch, key.Yaw)).magnitude > AimTolerance)
                    failures.Add($"dig {f}: not aimed where the clip aims it");
            }
            Assert.AreEqual(driver.Renderer.TipLocal.magnitude, figure.HeldReachMetres, 1e-4f,
                            "the figure's overlay must be padded by the shovel's length while it is held");
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void TheVillagersShovel_IsNotDrawn_OnAClipThatAimsNoTool_WithTheSwitchOff_OrWithNoProp()
        {
            HeldPropMeshDef shovel = Load<HeldPropMeshDef>(ShovelMeshPath);
            IsoCharacterFigureRenderer figure = Figure(GinnyPath);
            FigureHeldPropMesh driver = Driver();

            Assert.IsTrue(figure.SetPose(DigState, 0));
            Assert.IsTrue(driver.TryDraw(true, shovel, HeldPropSockets.RightHandTool, figure, LineWidthPx));

            Assert.IsTrue(figure.SetPose(IdleState, 0));
            Assert.IsFalse(driver.TryDraw(true, shovel, HeldPropSockets.RightHandTool, figure, LineWidthPx));
            Assert.AreEqual(FigureHeldPropMesh.Refusal.ClipAimsNoTool, driver.LastRefusal);
            Assert.IsFalse(driver.Renderer.Visible, "a hidden shovel must not draw");
            Assert.AreEqual(0f, figure.HeldReachMetres, 1e-6f, "hiding the shovel must give the overlay back");

            Assert.IsTrue(figure.SetPose(DigState, 0));
            Assert.IsFalse(driver.TryDraw(false, shovel, HeldPropSockets.RightHandTool, figure, LineWidthPx));
            Assert.AreEqual(FigureHeldPropMesh.Refusal.SwitchOff, driver.LastRefusal);
            Assert.IsFalse(driver.TryDraw(true, null, HeldPropSockets.RightHandTool, figure, LineWidthPx));
            Assert.AreEqual(FigureHeldPropMesh.Refusal.NoProp, driver.LastRefusal);
            Assert.IsFalse(driver.TryDraw(true, shovel, "no_such_bone", figure, LineWidthPx));
            Assert.AreEqual(FigureHeldPropMesh.Refusal.NoMountBone, driver.LastRefusal);
        }

        // ================================================================ the villager's figure at work

        [Test]
        public void AVillagerAtWork_DigsAsAMesh_HoldingTheMeshShovel_AndNoSpriteToolIsDrawn()
        {
            UseFreshIdPool();
            ScriptedClock clock = UseScriptedTime(worldSeed: 7);
            FakeWorkStand stand = WorkStand(GinnyPath, out IsoCharacterSprite character);
            stand.Prop = Load<HeldPropMeshDef>(ShovelMeshPath);
            stand.State = DigState;
            stand.Working = true;
            CharacterFigurePresenter presenter = Attach(character.gameObject, stand);
            SpriteRenderer body = character.GetComponent<SpriteRenderer>();

            for (int i = 0; i < 12; i++)
            {
                clock.Advance(FrameSeconds * 5d);
                presenter.PoseFigure(stand, aboard: false);
                AssertDrawsAshore(presenter, body);
                IsoCharacterFigureRenderer figure = presenter.AshoreFigure;
                Assert.AreEqual(DigState, figure.DrawnStateKey, "at work she must play the work clip");
                IsoFacetHeldPropRenderer tool = HeldTool(character.gameObject);
                Assert.IsNotNull(tool, $"at work she must hold the tool as a mesh (frame {i}: {Why(presenter)})");
                Assert.IsTrue(tool.Visible, "the mesh shovel must draw while she digs");
                Assert.AreSame(figure.FacetTransform, tool.transform.parent, "the shovel rides her figure's facet");
                Assert.AreSame(stand.Prop, tool.Def);
                AssertNoSpriteDraws(character.gameObject);
            }

            // Work over: she stands as she would, and the tool is put away with nothing left drawn.
            stand.Working = false;
            presenter.PoseFigure(stand, aboard: false);
            AssertDrawsAshore(presenter, body);
            Assert.AreNotEqual(DigState, presenter.AshoreFigure.DrawnStateKey);
            IsoFacetHeldPropRenderer after = HeldTool(character.gameObject);
            Assert.IsTrue(after == null || !after.Visible, "no shovel may draw once the work is over");
            AssertNoSpriteDraws(character.gameObject);
        }

        [Test]
        public void WorkTheFigureCannotDraw_LeavesHerAMesh_StandingEmptyHanded()
        {
            UseFreshIdPool();
            UseScriptedTime(worldSeed: 3);
            FakeWorkStand stand = WorkStand(GinnyPath, out IsoCharacterSprite character);
            HeldPropMeshDef shovel = Load<HeldPropMeshDef>(ShovelMeshPath);
            CharacterFigurePresenter presenter = Attach(character.gameObject, stand);
            SpriteRenderer body = character.GetComponent<SpriteRenderer>();

            // A state the skin does not have: not refused, she simply stands.
            stand.Working = true;
            stand.State = "no_such_work";
            stand.Prop = shovel;
            presenter.PoseFigure(stand, aboard: false);
            AssertDrawsAshore(presenter, body);
            Assert.AreNotEqual("no_such_work", presenter.AshoreFigure.DrawnStateKey);
            IsoFacetHeldPropRenderer none = HeldTool(character.gameObject);
            Assert.IsTrue(none == null || !none.Visible, "no tool for work she is not doing");

            // The hand-tool switch off: she does not dig the air either.
            stand.State = DigState;
            _config.MeshHandToolInHand = false;
            presenter.PoseFigure(stand, aboard: false);
            AssertDrawsAshore(presenter, body);
            Assert.AreNotEqual(DigState, presenter.AshoreFigure.DrawnStateKey,
                               "with the tool switch off she must not swing an empty dig");
            IsoFacetHeldPropRenderer tool = HeldTool(character.gameObject);
            Assert.IsTrue(tool == null || !tool.Visible);
            AssertNoSpriteDraws(character.gameObject);

            // On again: the dig and the shovel come back together.
            _config.MeshHandToolInHand = true;
            presenter.PoseFigure(stand, aboard: false);
            Assert.AreEqual(DigState, presenter.AshoreFigure.DrawnStateKey);
            Assert.IsTrue(HeldTool(character.gameObject).Visible);
        }

        [Test]
        public void AVillagerAtWork_AllocatesNothingPerFrameAfterWarmUp()
        {
            UseFreshIdPool();
            ScriptedClock clock = UseScriptedTime(worldSeed: 0);
            FakeWorkStand stand = WorkStand(GinnyPath, out IsoCharacterSprite character);
            stand.Prop = Load<HeldPropMeshDef>(ShovelMeshPath);
            stand.State = DigState;
            stand.Working = true;
            CharacterFigurePresenter presenter = Attach(character.gameObject, stand);
            Assert.IsTrue(character.gameObject.TryGetComponent(out SpriteRenderer body));

            // Warm-up: her figure, her id and the shovel's build, then every dig frame once.
            presenter.PoseFigure(stand, aboard: false);
            PoseFrames(presenter, stand, clock, MeasuredFrames);
            AssertDrawsAshore(presenter, body);
            Assert.IsTrue(HeldTool(character.gameObject).Visible, "harness: the warm-up must have built the shovel");

            Assert.That(() => { _sink = new object[64]; }, Is.AllocatingGCMemory(),
                        "harness: the GC.Alloc recorder saw no allocation at all");
            Assert.That(() => PoseFrames(presenter, stand, clock, MeasuredFrames), Is.Not.AllocatingGCMemory(),
                        "a villager digging with her shovel allocated (rule 7: nothing per frame)");
            Assert.IsTrue(HeldTool(character.gameObject).Visible, "the loop measured the drawing path");
        }

        // ================================================================ harness

        private static string Why(CharacterFigurePresenter presenter)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object held = typeof(CharacterFigurePresenter).GetField("_heldProp", flags)?.GetValue(presenter);
            object work = typeof(CharacterFigurePresenter).GetField("_workProp", flags)?.GetValue(presenter);
            var driver = held as FigureHeldPropMesh;
            return driver == null
                ? $"no driver; work prop {(work != null ? "set" : "none")}"
                : $"refusal {driver.LastRefusal}, drawing {driver.Drawing}, renderer {(driver.Renderer != null ? driver.Renderer.transform.parent?.name : "none")}, work prop {(work != null ? "set" : "none")}";
        }

        private static IsoFacetHeldPropRenderer HeldTool(GameObject villager)
        {
            foreach (IsoFacetHeldPropRenderer r in villager.GetComponentsInChildren<IsoFacetHeldPropRenderer>(true))
                if (r.gameObject.name == CharacterFigurePresenter.HeldPropObjectName) return r;
            return null;
        }

        /// <summary>The rule: on a mesh villager no sprite draws at all, tool or body.</summary>
        private static void AssertNoSpriteDraws(GameObject villager)
        {
            foreach (SpriteRenderer sr in villager.GetComponentsInChildren<SpriteRenderer>(true))
                Assert.IsFalse(sr.enabled && sr.gameObject.activeInHierarchy && !sr.forceRenderingOff && sr.sprite != null,
                               $"a sprite '{sr.gameObject.name}' draws on a mesh villager");
        }

        private static void AssertDrawsAshore(CharacterFigurePresenter presenter, SpriteRenderer body)
        {
            Assert.AreEqual(CharacterFigurePresenter.Refusal.None, presenter.WhyNot, presenter.NotDrawingReason);
            Assert.IsTrue(presenter.DrawsInsteadOfSprite);
            Assert.IsTrue(body.forceRenderingOff, "her drawn figure must hide her body sprite");
            Assert.IsNotNull(presenter.AshoreFigure);
            Assert.IsTrue(presenter.AshoreFigure.Visible);
        }

        private static void PoseFrames(CharacterFigurePresenter presenter, ICharacterFigureStand stand,
                                       ScriptedClock clock, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                clock.Advance(FrameSeconds);
                presenter.PoseFigure(stand, aboard: false);
            }
        }

        private FakeWorkStand WorkStand(string skinPath, out IsoCharacterSprite character)
        {
            var visual = Track(ScriptableObject.CreateInstance<CharacterVisualDef>());
            visual.Skin = Load<CharacterSkinDef>(skinPath);
            GameObject go = Track(new GameObject("NpcHeldToolFigureTestsVillager"));
            character = go.AddComponent<IsoCharacterSprite>();
            character.Configure(visual);
            _characters.Add(character);
            return new FakeWorkStand { Character = character, Key = VillagerKey };
        }

        private static CharacterFigurePresenter Attach(GameObject host, ICharacterFigureStand stand)
        {
            ICharacterFigure attached = new CharacterFigurePresentationService().Attach(host, stand);
            Assert.IsInstanceOf<CharacterFigurePresenter>(attached, "harness: a skinned character gets the presenter");
            return (CharacterFigurePresenter)attached;
        }

        private void UseFreshIdPool()
        {
            IsoFacetIdPool old = IsoFacetHullRegistry.SwapIdPoolForTests(new IsoFacetIdPool());
            if (_livePool == null) _livePool = old;
        }

        private static ScriptedClock UseScriptedTime(int worldSeed)
        {
            var clock = new ScriptedClock();
            GameServices.Clock = clock;
            GameServices.Environment = new ScriptedWorld(worldSeed);
            return clock;
        }

        private IsoCharacterFigureRenderer Figure(string skinPath)
        {
            var figure = new GameObject("NpcHeldToolFigureTestsFigure").AddComponent<IsoCharacterFigureRenderer>();
            _figures.Add(figure);
            figure.Configure(Load<CharacterSkinDef>(skinPath));
            figure.Visible = true;
            Assert.IsNotNull(figure.FacetTransform, "harness: the figure has no facet child");
            return figure;
        }

        private FigureHeldPropMesh Driver()
        {
            var d = new FigureHeldPropMesh(_home.transform, "NpcHeldTool", IsoCharacterFigureRenderer.HeldReachHandTool);
            _drivers.Add(d);
            return d;
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
