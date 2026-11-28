using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;   // ConstraintExtensions: Is.Not.AllocatingGCMemory()
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The idle fidgets, as one figure's life steps them</b> (<see cref="CharacterFigureLife"/>): the
    /// same piece the player's and every villager's presenter call.
    ///
    /// <list type="bullet">
    /// <item>A Look fidget takes the look while it plays: the target sits along its gaze, to the figure's
    /// right for a positive yaw, and the head turns on the rig.</item>
    /// <item>The presenter's "not idle" ends it the same step; no library, or the head look switched off,
    /// plays nothing.</item>
    /// <item>Two figures of one skin have different schedule keys.</item>
    /// <item>A Clip fidget hands over its clip and frame only on a skin that carries it.</item>
    /// <item>The step allocates nothing (rule 7).</item>
    /// </list>
    ///
    /// <para>A synthetic four-bone def, built as <c>IsoCharacterFigureLifeTests</c> builds its own. The
    /// library is a test library with no gap, so a fidget is always playing once the first slot after the
    /// settle has begun.</para>
    /// </summary>
    public sealed class IdleFidgetLifeTests
    {
        private const int Open = 1, Half = 2, Shut = 3, Left = 4, Right = 5, Brows = 6, Mouth = 7;
        private const int ChestBone = 1, NeckBone = 2, HeadBone = 3;
        private const float FidgetSeconds = 2f;
        private const string ScratchKey = "idleScratch";

        private static readonly MethodInfo s_onDestroy =
            typeof(IsoCharacterFigureRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<IsoCharacterFigureRenderer> _figures = new List<IsoCharacterFigureRenderer>();
        private readonly List<Object> _made = new List<Object>();
        private GameConfig _previousConfig;
        private IEnvironmentService _previousEnvironment;

        [SetUp]
        public void SetUp()
        {
            _previousConfig = GameServices.Config;
            _previousEnvironment = GameServices.Environment;
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _made.Add(config);
            GameServices.Config = config;
            GameServices.Environment = null;
            IsoFacetFigureInk.Reset();
            CharacterLookTargets.Restore();
            IdleFidgetPreview.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (IsoCharacterFigureRenderer figure in _figures)
                if (figure != null) Scrap(figure);
            _figures.Clear();
            GameServices.Config = _previousConfig;
            GameServices.Environment = _previousEnvironment;
            CharacterLookTargets.Restore();
            IsoFacetFigureInk.Reset();
            IdleFidgetPreview.Clear();
            foreach (Object o in _made)
                if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // =================================================================== fixtures

        private static readonly Vector3[] RestLocal =
        {
            Vector3.zero, new Vector3(0f, 0f, 0.9f), new Vector3(0f, 0f, 0.5f), new Vector3(0f, 0f, 0.15f),
        };

        private Mesh BuildBindMesh()
        {
            var bind = new Mesh { name = "HHTestFidgetBind", hideFlags = HideFlags.HideAndDontSave };
            _made.Add(bind);
            bind.SetVertices(new[]
            {
                new Vector3(-0.15f, 0.1f, 0.6f), new Vector3(0.15f, 0.1f, 0.6f),
                new Vector3(0.15f, 0.1f, 1.3f), new Vector3(-0.15f, 0.1f, 1.3f),
                new Vector3(-0.08f, 0.12f, 1.5f), new Vector3(0.08f, 0.12f, 1.5f),
                new Vector3(0.08f, 0.12f, 1.72f), new Vector3(-0.08f, 0.12f, 1.72f),
            });
            var normals = new Vector3[8];
            for (int i = 0; i < 8; i++) normals[i] = Vector3.down;
            bind.SetNormals(normals);
            bind.SetTriangles(new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 }, 0);
            var weights = new BoneWeight[8];
            for (int i = 0; i < 8; i++)
                weights[i] = new BoneWeight { boneIndex0 = i < 4 ? ChestBone : HeadBone, weight0 = 1f };
            bind.boneWeights = weights;
            var bindposes = new Matrix4x4[4];
            Vector3 at = Vector3.zero;
            for (int b = 0; b < 4; b++)
            {
                at += RestLocal[b];
                bindposes[b] = Matrix4x4.Translate(-at);
            }
            bind.bindposes = bindposes;
            bind.RecalculateBounds();
            return bind;
        }

        private static CharacterSkinDef.SkinClip Clip(string state, int frames, bool loop)
        {
            const int bones = 4;
            var keys = new CharacterSkinDef.BoneKey[frames * bones];
            var face = new byte[frames * 3];
            for (int f = 0; f < frames; f++)
            {
                for (int b = 0; b < bones; b++)
                    keys[f * bones + b] = new CharacterSkinDef.BoneKey { Position = RestLocal[b], Rotation = Quaternion.identity };
                face[f * 3] = Open; face[f * 3 + 1] = Brows; face[f * 3 + 2] = Mouth;
            }
            return new CharacterSkinDef.SkinClip
            {
                Anim = state, State = state, FramesPerSecond = 6f, Loop = loop, FrameCount = frames, Keys = keys, Face = face,
            };
        }

        private CharacterSkinDef MakeDef(bool withScratch = false)
        {
            var def = ScriptableObject.CreateInstance<CharacterSkinDef>();
            _made.Add(def);
            def.Id = "charskin.test_fidget";
            def.BindMesh = BuildBindMesh();
            def.Bones = new[]
            {
                new CharacterSkinDef.Bone { Id = "root", Parent = CharacterSkinDef.NoBone, OwnsVertex = false },
                new CharacterSkinDef.Bone { Id = "chest", Parent = 0, OwnsVertex = true },
                new CharacterSkinDef.Bone { Id = "neck", Parent = 1, OwnsVertex = false },
                new CharacterSkinDef.Bone { Id = "head", Parent = 2, OwnsVertex = true },
            };
            def.MaxInfluences = 2;
            def.Materials = new[]
            {
                new CharacterSkinDef.Material
                {
                    Name = "t4", Colors = new[]
                    {
                        new Color32(20, 30, 40, 255), new Color32(60, 70, 80, 255),
                        new Color32(100, 110, 120, 255), new Color32(140, 150, 160, 255),
                    },
                    Offset = 0, Gain = 1f, Bias = 1f, FixedIndex = -1, ToneLo = 0, ToneHi = 3,
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
            def.Clips = withScratch
                ? new[] { Clip(CharacterSkinStateMap.Idle, 2, true), Clip(ScratchKey, 6, false) }
                : new[] { Clip(CharacterSkinStateMap.Idle, 2, true) };
            def.MeshStates = withScratch ? new[] { CharacterSkinStateMap.Idle, ScratchKey } : new[] { CharacterSkinStateMap.Idle };
            def.ToneRule = ToneRule.V9;
            def.KeyScreen = new Vector3(0f, 0.8106792283998809f, 0.5854905538443586f);
            def.Form = 0.5f;
            def.FormMid = 0.45f;

            def.FaceGroups = new[] { "eyes.open", "eyes.half", "eyes.shut", "eyes.left", "eyes.right", "brows.flat", "mouth.flat" };
            def.RestFace = new[] { Open, Brows, Mouth };
            def.FaceMinToward = new Vector4(0.1f, 0.2f, 0.3f, 0.4f);
            def.FaceCullFloor = 1e-4f;
            def.BlinkSteps = new[]
            {
                new CharacterSkinDef.BlinkStep { Group = Half, Seconds = 0.05f },
                new CharacterSkinDef.BlinkStep { Group = Shut, Seconds = 0.1f },
            };
            def.BlinkIntervalSeconds = new Vector2(1.5f, 2.5f);
            def.BlinkDoubleChance = 0.25f;
            def.BlinkDoubleGapSeconds = 0.1f;
            def.BlinkSkipGroups = new[] { Shut };

            def.LookChestBone = ChestBone;
            def.LookNeckBone = NeckBone;
            def.LookHeadBone = HeadBone;
            def.LookSplitNeck = 0.3f;
            def.LookSplitHead = 0.7f;
            def.LookYawLimits = new Vector2(-50f, 50f);
            def.LookPitchLimits = new Vector2(-20f, 25f);
            def.LookHeadShare = 0.8f;
            def.LookEyesBeyondDeg = 6f;
            def.LookEyes = new Vector3Int(Open, Left, Right);
            def.HeadMid = new Vector3(0f, 0.05f, 0.1f);
            def.HeadSnap = true;

            Assert.IsTrue(def.IsUsable(), "harness: the synthetic def is not usable");
            Assert.IsTrue(def.HasLook, "harness: the synthetic def must carry the look");
            return def;
        }

        private IsoCharacterFigureRenderer MakeFigure(CharacterSkinDef def)
        {
            var go = new GameObject("TestFidgetFigure");
            var figure = go.AddComponent<IsoCharacterFigureRenderer>();
            _figures.Add(figure);
            figure.Configure(def);
            Assert.IsTrue(figure.SetPose(CharacterSkinStateMap.Idle, 0), "harness: the figure did not draw");
            return figure;
        }

        private static void Scrap(IsoCharacterFigureRenderer figure)
        {
            Assert.IsNotNull(s_onDestroy, "harness: the renderer's OnDestroy is where it gives its objects back");
            s_onDestroy.Invoke(figure, null);
            Object.DestroyImmediate(figure.gameObject);
        }

        /// <summary>A gaze that turns right by <paramref name="yaw"/> over its whole length.</summary>
        private IdleFidgetDef LookRight(float yaw)
        {
            var d = ScriptableObject.CreateInstance<IdleFidgetDef>();
            _made.Add(d);
            d.Id = "idle.test_right";
            d.Weight = 1f;
            d.DurationSeconds = FidgetSeconds;
            d.Kind = IdleFidgetDef.Channel.Look;
            d.Gaze = new[]
            {
                new IdleFidgetDef.GazeKey { AtSeconds = 0f, YawDegrees = yaw },
                new IdleFidgetDef.GazeKey { AtSeconds = FidgetSeconds, YawDegrees = yaw },
            };
            return d;
        }

        private IdleFidgetDef Scratch()
        {
            var d = ScriptableObject.CreateInstance<IdleFidgetDef>();
            _made.Add(d);
            d.Id = "idle.test_scratch";
            d.Weight = 1f;
            d.DurationSeconds = FidgetSeconds;
            d.Kind = IdleFidgetDef.Channel.Clip;
            d.ClipKey = ScratchKey;
            return d;
        }

        /// <summary>No gap and no settle: back-to-back slots of one fidget's length.</summary>
        private IdleFidgetLibraryDef Library(params IdleFidgetDef[] fidgets)
        {
            var lib = ScriptableObject.CreateInstance<IdleFidgetLibraryDef>();
            _made.Add(lib);
            lib.Enabled = true;
            lib.MinGapSeconds = 0f;
            lib.MaxGapSeconds = 0f;
            lib.SettleSeconds = 0f;
            lib.LookDistanceMetres = 6f;
            lib.Fidgets = fidgets;
            return lib;
        }

        // Stand idle from 0, then step to a time two slots on: the slot holding it began after the idle did.
        private const double Settled = 2.5 * FidgetSeconds;

        // =================================================================== the look

        [Test]
        public void ALookFidgetTakesTheLookAlongItsGaze()
        {
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer figure = MakeFigure(def);
            var life = new CharacterFigureLife { Library = Library(LookRight(40f)) };
            const uint hash = 17u;

            life.Step(def, "npc.a", hash, 0, figure, looks: false, idle: true);
            IsoCharacterFigureRenderer.Life l = life.Step(def, "npc.a", hash, Settled, figure, looks: false, idle: true);

            Assert.IsTrue(life.Fidget.Active, "an idle figure past the settle is not fidgeting");
            Assert.IsTrue(l.HasTarget && l.HeadLook, "the fidget did not take the look");
            Vector3 eye = figure.HeadLocalToFigure(figure.HeadMidLocal);
            Vector3 toward = l.Target - eye;
            Assert.Greater(toward.x, 0f, "a positive yaw did not look to the figure's right");
            Assert.AreEqual(40f, Mathf.Atan2(toward.x, toward.y) * Mathf.Rad2Deg, 0.01f, "the target is not along the gaze");
            Assert.AreEqual(6f, toward.magnitude, 1e-3f, "the target is not at the library's distance");

            Assert.IsTrue(figure.SetPose(CharacterSkinStateMap.Idle, 1, l));
            Assert.Greater(figure.DrawnLookYaw, 0.0, "the head did not turn toward the fidget's gaze");
        }

        [Test]
        public void NotIdleEndsTheFidgetTheSameStep()
        {
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer figure = MakeFigure(def);
            var life = new CharacterFigureLife { Library = Library(LookRight(40f)) };

            life.Step(def, "npc.a", 1u, 0, figure, looks: false, idle: true);
            life.Step(def, "npc.a", 1u, Settled, figure, looks: false, idle: true);
            Assert.IsTrue(life.Fidget.Active, "harness: the fidget is not playing");

            IsoCharacterFigureRenderer.Life l = life.Step(def, "npc.a", 1u, Settled + 0.02, figure, looks: false, idle: false);
            Assert.IsFalse(life.Fidget.Active, "a move did not end the fidget");
            Assert.IsFalse(l.HasTarget, "the look outlived the fidget on a figure that looks at nobody");

            l = life.Step(def, "npc.a", 1u, Settled + 0.04, figure, looks: false, idle: true);
            Assert.IsFalse(life.Fidget.Active, "the cancelled fidget resumed mid-slot");
        }

        private sealed class Near : ICharacterLookTargetSource
        {
            public Vector3 Answer;

            public bool TryGetLookTarget(string figureKey, Vector3 figureWorldPosition, out Vector3 targetWorldPosition)
            {
                targetWorldPosition = Answer;
                return true;
            }
        }

        [Test]
        public void AFigureAttendingToThePlayerDoesNotFidget()
        {
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer figure = MakeFigure(def);
            var life = new CharacterFigureLife { Library = Library(LookRight(40f)) };

            CharacterLookTargets.Source = new Near { Answer = new Vector3(1f, 1f, 0f) };
            life.Step(def, "npc.a", 1u, 0, figure, looks: true, idle: true);
            IsoCharacterFigureRenderer.Life l = life.Step(def, "npc.a", 1u, Settled, figure, looks: true, idle: true);
            Assert.IsTrue(l.HasTarget, "harness: the player is inside the radius, so she should look at him");
            Assert.IsFalse(life.Fidget.Active, "she fidgeted while the player stood beside her");

            // He walks off: she settles, then looks about again.
            CharacterLookTargets.Restore();
            life.Step(def, "npc.a", 1u, Settled + 0.1, figure, looks: true, idle: true);
            life.Step(def, "npc.a", 1u, Settled + 0.1 + Settled, figure, looks: true, idle: true);
            Assert.IsTrue(life.Fidget.Active, "she never went back to fidgeting once he had gone");
        }

        [Test]
        public void NoLibraryOrTheHeadLookSwitchedOffPlaysNothing()
        {
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer figure = MakeFigure(def);

            var none = new CharacterFigureLife { Library = null };
            none.Step(def, "npc.a", 1u, 0, figure, looks: false, idle: true);
            IsoCharacterFigureRenderer.Life l = none.Step(def, "npc.a", 1u, Settled, figure, looks: false, idle: true);
            Assert.IsFalse(none.Fidget.Active);
            Assert.IsFalse(l.HasTarget);

            GameServices.Config.CharacterHeadLook = false;
            var off = new CharacterFigureLife { Library = Library(LookRight(40f)) };
            off.Step(def, "npc.a", 1u, 0, figure, looks: false, idle: true);
            l = off.Step(def, "npc.a", 1u, Settled, figure, looks: false, idle: true);
            Assert.IsFalse(l.HasTarget, "a fidget turned the head with the head look switched off");
        }

        [Test]
        public void TwoFiguresOfOneSkinHaveTheirOwnSchedules()
        {
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer a = MakeFigure(def), b = MakeFigure(def);
            IdleFidgetLibraryDef lib = Library(LookRight(40f));
            // The per-figure stretch lives in the gap's spread, so this library needs one (the test
            // library's gapless default gives every figure the same slot by design).
            lib.MinGapSeconds = 4f;
            lib.MaxGapSeconds = 12f;
            var la = new CharacterFigureLife { Library = lib };
            var lb = new CharacterFigureLife { Library = lib };
            la.Step(def, "npc.a", CharacterFigurePresenter.KeyHash("npc.a"), 0, a, looks: false, idle: true);
            lb.Step(def, "npc.b", CharacterFigurePresenter.KeyHash("npc.b"), 0, b, looks: false, idle: true);
            Assert.AreNotEqual(la.FidgetKey, lb.FidgetKey, "two people of one skin share one fidget schedule");
            Assert.AreNotEqual(IdleFidgetSchedule.SlotSeconds(lib, def, la.FidgetKey),
                               IdleFidgetSchedule.SlotSeconds(lib, def, lb.FidgetKey),
                               "two people of one skin share one slot length");
        }

        // =================================================================== the clip

        [Test]
        public void AClipFidgetHandsOverItsClipOnlyOnASkinThatCarriesIt()
        {
            IdleFidgetLibraryDef lib = Library(Scratch());

            CharacterSkinDef bare = MakeDef();
            IsoCharacterFigureRenderer plain = MakeFigure(bare);
            var without = new CharacterFigureLife { Library = lib };
            without.Step(bare, "npc.a", 1u, 0, plain, looks: false, idle: true);
            without.Step(bare, "npc.a", 1u, Settled, plain, looks: false, idle: true);
            Assert.IsFalse(without.Fidget.Active, "a clip fidget played on a skin without its clip");
            Assert.IsFalse(without.TryFidgetClip(bare, out _, out _));

            CharacterSkinDef rich = MakeDef(withScratch: true);
            IsoCharacterFigureRenderer figure = MakeFigure(rich);
            var with = new CharacterFigureLife { Library = lib };
            with.Step(rich, "npc.a", 1u, 0, figure, looks: false, idle: true);
            IsoCharacterFigureRenderer.Life l = with.Step(rich, "npc.a", 1u, Settled, figure, looks: false, idle: true);
            Assert.IsTrue(with.Fidget.Active, "a clip fidget did not play on a skin with its clip");
            Assert.IsFalse(l.HasTarget, "a clip fidget took the look");
            Assert.IsTrue(with.TryFidgetClip(rich, out string key, out int frame));
            Assert.AreEqual(ScratchKey, key);
            Assert.AreEqual(IdleFidgetDef.ClipFrame(rich.Clips[1], with.Fidget.LocalSeconds), frame);
            Assert.IsTrue(figure.SetPose(key, frame, l), "the renderer refused the fidget's clip");
        }

        // =================================================================== rule 7

        [Test]
        public void TheStepWithFidgetsAllocatesNothing()
        {
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer figure = MakeFigure(def);
            var life = new CharacterFigureLife { Library = Library(LookRight(40f)) };
            double t = 0;
            for (int i = 0; i < 60; i++) { t += 0.05; life.Step(def, "npc.a", 1u, t, figure, looks: false, idle: true); }

            Assert.That(() => { System.GC.KeepAlive(new byte[1024]); },
                        UnityEngine.TestTools.Constraints.Is.AllocatingGCMemory(),
                        "harness: a known allocation must be detected before zero is believed");
            void Frames()
            {
                for (int i = 0; i < 240; i++)
                {
                    t += 0.05;
                    life.Step(def, "npc.a", 1u, t, figure, looks: false, idle: (i % 50) != 0);
                    life.TryFidgetClip(def, out _, out _);
                }
            }
            // One unmeasured pass over the same path first: the first call through a path the warm-up did
            // not take (a cancel, a fidget starting) pays the runtime's one-time cost, which is not a
            // per-frame allocation. Measured on a clean editor: the first pass shows one block, every
            // pass after it none.
            Frames();
            Assert.That(new TestDelegate(Frames), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }
    }
}
