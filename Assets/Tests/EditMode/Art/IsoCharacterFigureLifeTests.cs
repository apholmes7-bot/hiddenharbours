using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A RIG 9 FIGURE'S LIFE BETWEEN ITS CLIP'S KEYS</b> (character PR 2a): the blink, the look and the
    /// ink, as the renderer draws them (<see cref="IsoCharacterFigureRenderer.SetPose(string, int, in IsoCharacterFigureRenderer.Life)"/>),
    /// as one figure's <see cref="CharacterFigureLife"/> steps them, and as the resolve's ink registry
    /// (<see cref="IsoFacetFigureInk"/>) hears of them.
    ///
    /// <list type="bullet">
    /// <item>A default life, or a life whose look is switched off, draws exactly the clip.</item>
    /// <item>A blink reaches the face uniform and never re-skins; a frame whose own eyes are shut keeps
    /// them.</item>
    /// <item>A look turns the neck and head by the rig's <c>lookAt</c> on the frame shown, moves no body
    /// vertex, and hands the rest to the eyes; head-only and eyes-only each do their half. The look is
    /// sampled on the clip's beat and held between beats.</item>
    /// <item>The per-frame path allocates nothing (rule 7).</item>
    /// <item>The ink is live for a def that carries it while the switch is on, and a figure is in the
    /// registry only while it is shown.</item>
    /// <item>A figure's life asks the look seam with its own key, keeps only what stands inside the
    /// radius, obeys each switch, and the player's own (<c>looks: false</c>) never asks.</item>
    /// </list>
    ///
    /// <para>A synthetic four-bone def with numbers of its own. No GPU is used or needed: the posed
    /// mesh, the material's vectors and the registry are read back on the CPU. That the shipped defs
    /// carry the rig's own numbers is <c>CharacterSkinBakeGuardTests</c>'s, against the rig in V8.</para>
    /// </summary>
    public sealed class IsoCharacterFigureLifeTests
    {
        private const int Open = 1, Half = 2, Shut = 3, Left = 4, Right = 5, Brows = 6, Mouth = 7;
        private const int ChestBone = 1, NeckBone = 2, HeadBone = 3;
        private const float Edge = 0.2f, Mix = 0.3f;

        private static readonly Vector3 RightTarget = new Vector3(3f, 1f, 1.3f);
        private static readonly Vector3 LeftTarget = new Vector3(-3f, 1f, 1.3f);

        private static readonly MethodInfo s_onDestroy =
            typeof(IsoCharacterFigureRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<IsoCharacterFigureRenderer> _figures = new List<IsoCharacterFigureRenderer>();
        private readonly List<Object> _made = new List<Object>();
        private GameConfig _previousConfig;
        private GameConfig _config;

        private sealed class Spy : ICharacterLookTargetSource
        {
            public int Asked;
            public string Key;
            public Vector3 Answer;

            public bool TryGetLookTarget(string figureKey, Vector3 figureWorldPosition, out Vector3 targetWorldPosition)
            {
                Asked++;
                Key = figureKey;
                targetWorldPosition = Answer;
                return true;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _previousConfig = GameServices.Config;
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _made.Add(_config);
            GameServices.Config = _config;
            IsoFacetFigureInk.Reset();
            CharacterLookTargets.Restore();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (IsoCharacterFigureRenderer figure in _figures)
                if (figure != null) Scrap(figure);
            _figures.Clear();
            GameServices.Config = _previousConfig;
            CharacterLookTargets.Restore();
            IsoFacetFigureInk.Reset();
            foreach (Object o in _made)
                if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // =================================================================== the synthetic def

        private static readonly Vector3[] RestLocal =
        {
            Vector3.zero, new Vector3(0f, 0f, 0.9f), new Vector3(0f, 0f, 0.5f), new Vector3(0f, 0f, 0.15f),
        };

        /// <summary>A body quad on the chest and a head quad on the head, each one fan of four corners.</summary>
        private Mesh BuildBindMesh()
        {
            var bind = new Mesh { name = "HHTestLifeBind", hideFlags = HideFlags.HideAndDontSave };
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

        /// <summary>Two frames of one pose: the first with open eyes, the second with its eyes shut.</summary>
        private static CharacterSkinDef.SkinClip Idle()
        {
            const int frames = 2, bones = 4;
            var keys = new CharacterSkinDef.BoneKey[frames * bones];
            for (int f = 0; f < frames; f++)
                for (int b = 0; b < bones; b++)
                    keys[f * bones + b] = new CharacterSkinDef.BoneKey { Position = RestLocal[b], Rotation = Quaternion.identity };
            return new CharacterSkinDef.SkinClip
            {
                Anim = "idle", State = "idle", FramesPerSecond = 6f, Loop = true, FrameCount = frames, Keys = keys,
                Face = new byte[] { Open, Brows, Mouth, Shut, Brows, Mouth },
            };
        }

        private CharacterSkinDef MakeDef(bool ink = true)
        {
            var def = ScriptableObject.CreateInstance<CharacterSkinDef>();
            _made.Add(def);
            def.Id = "charskin.test_life";
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
            def.Clips = new[] { Idle() };
            def.MeshStates = new[] { "idle" };
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

            def.Edge = ink ? Edge : 0f;
            def.KeylineMix = ink ? Mix : 0f;

            Assert.IsTrue(def.IsUsable(), "harness: the synthetic def is not usable, so every assertion below " +
                                          "would measure the fixture");
            Assert.IsTrue(def.HasFace && def.HasBlink && def.HasLook && def.HasHeadSnap,
                "harness: the synthetic def must carry the face, the blink, the look and the head snap");
            return def;
        }

        private IsoCharacterFigureRenderer MakeFigure(CharacterSkinDef def)
        {
            var go = new GameObject("TestLifeFigure");
            var figure = go.AddComponent<IsoCharacterFigureRenderer>();
            _figures.Add(figure);
            figure.Configure(def);
            return figure;
        }

        private static IsoCharacterFigureRenderer.Life Look(Vector3 target, bool head = true, bool eyes = true) =>
            new IsoCharacterFigureRenderer.Life { HasTarget = true, Target = target, HeadLook = head, EyeLook = eyes };

        private static Vector3[] Posed(IsoCharacterFigureRenderer figure) =>
            figure.GetComponentInChildren<MeshFilter>(true).sharedMesh.vertices;

        private static Material DrawnMaterial(IsoCharacterFigureRenderer figure) =>
            figure.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;

        /// <summary>The rig's <c>lookAt</c> on frame <paramref name="frame"/>, unturned, as the renderer is
        /// meant to sample it.</summary>
        private static CharacterFigureLook.Result Expected(CharacterSkinDef def, int frame, Vector3 target, double share)
        {
            var world = new Matrix4x4[def.Bones.Length];
            CharacterSkinPose.ComposeWorld(def.Clips[0], frame, def.Bones, world);
            return CharacterSkinPose.LookAtFrame(def, world, target, share);
        }

        /// <summary>EditMode runs no <c>OnDestroy</c> for the figure, so it is called here: the renderer's
        /// own teardown gives back its child, posed mesh, material and every ramp texture, and leaves the
        /// ink registry.</summary>
        private static void Scrap(IsoCharacterFigureRenderer figure)
        {
            Assert.IsNotNull(s_onDestroy, "harness: the renderer's OnDestroy is where it gives its objects back");
            s_onDestroy.Invoke(figure, null);
            Object.DestroyImmediate(figure.gameObject);
        }

        // =================================================================== the clip, unchanged

        [Test]
        public void ADefaultLifeOrALookSwitchedOffDrawsExactlyTheClip()
        {
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer plain = MakeFigure(def);
            IsoCharacterFigureRenderer off = MakeFigure(def);
            IsoCharacterFigureRenderer blind = MakeFigure(def);

            Assert.IsTrue(plain.SetPose("idle", 0));
            Assert.IsTrue(off.SetPose("idle", 0, Look(RightTarget, head: false, eyes: false)));
            Assert.IsTrue(blind.SetPose("idle", 0, new IsoCharacterFigureRenderer.Life { HeadLook = true, EyeLook = true }));

            CollectionAssert.AreEqual(Posed(plain), Posed(off), "A look with both halves off moved the mesh.");
            CollectionAssert.AreEqual(Posed(plain), Posed(blind), "A life with nothing to look at moved the mesh.");
            foreach (IsoCharacterFigureRenderer f in new[] { plain, off, blind })
            {
                Assert.AreEqual(0.0, f.DrawnLookYaw);
                Assert.AreEqual(0.0, f.DrawnLookPitch);
                Assert.AreEqual(CharacterFigureLook.GazeOpen, f.DrawnGaze);
                Assert.AreEqual(new Vector3Int(Open, Brows, Mouth), f.DrawnFace, "The frame's own face.");
            }
        }

        // =================================================================== the blink

        [Test]
        public void ABlinkReachesTheFaceUniformAndNeverReskins()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());
            figure.SetPose("idle", 0);
            Vector3[] before = Posed(figure);

            Assert.IsTrue(figure.SetPose("idle", 0, new IsoCharacterFigureRenderer.Life { BlinkEyes = Shut }));
            Assert.AreEqual(new Vector3Int(Shut, Brows, Mouth), figure.DrawnFace);
            Assert.AreEqual(new Vector4(Shut, Brows, Mouth, 0f),
                DrawnMaterial(figure).GetVector(IsoFacetFigureShaderIds.FigureFace), "The blink never reached the shader.");
            CollectionAssert.AreEqual(before, Posed(figure), "A blink re-skinned the mesh.");

            figure.SetPose("idle", 0);
            Assert.AreEqual(Open, figure.DrawnFace.x, "The eyes did not come back after the blink.");

            figure.SetPose("idle", 1, new IsoCharacterFigureRenderer.Life { BlinkEyes = Half });
            Assert.AreEqual(Shut, figure.DrawnFace.x, "A blink opened eyes the frame keeps shut.");
        }

        // =================================================================== the look

        [Test]
        public void ALookTurnsTheHeadByTheRigsLookAtAndTheEyesTakeTheRest()
        {
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer plain = MakeFigure(def);
            IsoCharacterFigureRenderer looking = MakeFigure(def);
            plain.SetPose("idle", 0);
            Vector4 unturnedHead = plain.HeadSnapPoint;
            Assert.IsTrue(looking.SetPose("idle", 0, Look(RightTarget)));

            CharacterFigureLook.Result want = Expected(def, 0, RightTarget, def.LookHeadShare);
            Assert.AreEqual(50.0, want.Yaw, "harness: the target must be past the yaw limit");
            Assert.AreEqual(CharacterFigureLook.GazeRight, want.Gaze, "harness: the eyes must be past their threshold");
            Assert.AreEqual(want.Yaw, looking.DrawnLookYaw);
            Assert.AreEqual(want.Pitch, looking.DrawnLookPitch);
            Assert.AreEqual(CharacterFigureLook.GazeRight, looking.DrawnGaze);
            Assert.AreEqual(Right, looking.DrawnFace.x, "The open eyes did not turn right.");

            Vector3[] a = Posed(plain), b = Posed(looking);
            for (int i = 0; i < 4; i++) Assert.AreEqual(a[i], b[i], $"Body vertex {i} moved with the look.");
            for (int i = 4; i < 8; i++) Assert.Greater(b[i].x - a[i].x, 0.005f, $"Head vertex {i} did not turn right.");

            Assert.AreEqual(0f, unturnedHead.x, 1e-6f);
            Assert.AreEqual(1f, looking.HeadSnapPoint.w, "The head snap is off.");
            Assert.Greater(looking.HeadSnapPoint.x, 0.02f, "The head snap's point did not turn with the head.");
            Assert.AreEqual(looking.HeadSnapPoint, DrawnMaterial(looking).GetVector(IsoFacetFigureShaderIds.FigureHead));
        }

        [Test]
        public void TheHeadAndTheEyesEachDoTheirHalf()
        {
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer plain = MakeFigure(def);
            IsoCharacterFigureRenderer head = MakeFigure(def);
            IsoCharacterFigureRenderer eyes = MakeFigure(def);
            plain.SetPose("idle", 0);
            head.SetPose("idle", 0, Look(RightTarget, head: true, eyes: false));
            eyes.SetPose("idle", 0, Look(RightTarget, head: false, eyes: true));

            Assert.AreEqual(Expected(def, 0, RightTarget, def.LookHeadShare).Yaw, head.DrawnLookYaw);
            Assert.AreEqual(CharacterFigureLook.GazeOpen, head.DrawnGaze, "The eyes turned with their half off.");
            Assert.AreEqual(Open, head.DrawnFace.x);

            Assert.AreEqual(0.0, eyes.DrawnLookYaw, "The head turned with its half off.");
            CollectionAssert.AreEqual(Posed(plain), Posed(eyes));
            Assert.AreEqual(Expected(def, 0, RightTarget, 0.0).Gaze, eyes.DrawnGaze, "The eyes alone are the rig's share 0.");
            Assert.AreEqual(Right, eyes.DrawnFace.x);
        }

        [Test]
        public void TheLookIsSampledOnTheBeatAndHeldBetweenBeats()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());
            figure.SetPose("idle", 0, Look(RightTarget));
            double right = figure.DrawnLookYaw;
            Assert.Greater(right, 0.0);

            figure.SetPose("idle", 0, Look(LeftTarget));
            Assert.AreEqual(right, figure.DrawnLookYaw, "The look moved between beats: the figure re-skinned off its clip's rate.");
            Assert.AreEqual(CharacterFigureLook.GazeRight, figure.DrawnGaze);

            figure.SetPose("idle", 1, Look(LeftTarget));
            Assert.Less(figure.DrawnLookYaw, 0.0, "A new frame did not sample the look again.");
            Assert.AreEqual(CharacterFigureLook.GazeLeft, figure.DrawnGaze);
            Assert.AreEqual(Shut, figure.DrawnFace.x, "A gaze replaced eyes the frame keeps shut.");
        }

        [Test]
        public void TheFramePathAllocatesNothing()
        {
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer figure = MakeFigure(def);
            var life = new CharacterFigureLife();
            var spy = new Spy { Answer = new Vector3(1f, 1f, 0f) };
            CharacterLookTargets.Source = spy;

            double now = 0d;
            int frame = 0;
            void Tick()
            {
                now += 1.0 / 60.0;
                frame = (int)(now * 6.0) % 2;
                IsoCharacterFigureRenderer.Life l = life.Step(def, "npc.test_skipper", now, figure, looks: true);
                figure.SetPose("idle", frame, l);
            }

            for (int i = 0; i < 240; i++) Tick();
            Assert.Greater(spy.Asked, 0, "harness: the look seam was never asked");
            Assert.That(() => { for (int i = 0; i < 240; i++) Tick(); },
                        UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory(),
                        "Four seconds of blinking and looking allocated.");
        }

        // =================================================================== the ink

        [Test]
        public void TheInkIsLiveWhileTheSwitchIsOnAndTheFigureInTheRegistryOnlyWhileShown()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef());
            Material mat = DrawnMaterial(figure);

            Assert.IsTrue(figure.InkLive, "A def with the rig's ink is not inked by it.");
            Assert.AreEqual(1f, mat.GetFloat(IsoFacetFigureShaderIds.FigureInkOn));
            Assert.AreEqual("HHCharStepRampTex", mat.GetTexture(IsoFacetShaderIds.DarkRampTex).name,
                "The ink draws its dark target from the one-step ramp.");
            Assert.AreEqual(1, IsoFacetFigureInk.Count);
            Assert.AreEqual(new Vector4(Edge, Mix, 1f, 0f), IsoFacetFigureInk.Value, "The resolve reads the def's edge and mix.");

            figure.Visible = false;
            Assert.AreEqual(0, IsoFacetFigureInk.Count, "A hidden figure stayed in the ink registry.");
            Assert.AreEqual(Vector4.zero, IsoFacetFigureInk.Value);
            figure.Visible = true;
            Assert.AreEqual(1, IsoFacetFigureInk.Count);

            _config.MeshFigureKeyline = false;
            figure.Visible = true;
            Assert.IsFalse(figure.InkLive, "The switch off left the ink on.");
            Assert.AreEqual(0f, mat.GetFloat(IsoFacetFigureShaderIds.FigureInkOn));
            Assert.AreNotEqual("HHCharStepRampTex", mat.GetTexture(IsoFacetShaderIds.DarkRampTex).name,
                "The switch off kept the one-step ramp.");
            Assert.AreEqual(0, IsoFacetFigureInk.Count);
        }

        [Test]
        public void ADefWithoutTheInkIsNeverInked()
        {
            IsoCharacterFigureRenderer figure = MakeFigure(MakeDef(ink: false));
            Assert.IsFalse(figure.InkLive);
            Assert.AreEqual(0f, DrawnMaterial(figure).GetFloat(IsoFacetFigureShaderIds.FigureInkOn));
            Assert.AreEqual(0, IsoFacetFigureInk.Count);
        }

        [Test]
        public void TheRegistryHoldsEachFigureOnceAndReadsTheLastIn()
        {
            IsoCharacterFigureRenderer a = new GameObject("InkA").AddComponent<IsoCharacterFigureRenderer>();
            IsoCharacterFigureRenderer b = new GameObject("InkB").AddComponent<IsoCharacterFigureRenderer>();
            _figures.Add(a);
            _figures.Add(b);
            Assert.AreEqual(Vector4.zero, IsoFacetFigureInk.Value, "An empty registry inks.");

            IsoFacetFigureInk.Join(a, 0.12f, 0.22f);
            IsoFacetFigureInk.Join(b, 0.2f, 0.5f);
            Assert.AreEqual(2, IsoFacetFigureInk.Count);
            Assert.AreEqual(new Vector4(0.2f, 0.5f, 1f, 0f), IsoFacetFigureInk.Value);

            IsoFacetFigureInk.Join(a, 0.12f, 0.22f);
            Assert.AreEqual(2, IsoFacetFigureInk.Count, "A figure joined twice is in twice.");
            Assert.AreEqual(new Vector4(0.12f, 0.22f, 1f, 0f), IsoFacetFigureInk.Value, "A re-join is not the last in.");

            IsoFacetFigureInk.Leave(a);
            IsoFacetFigureInk.Leave(a);
            Assert.AreEqual(1, IsoFacetFigureInk.Count);
            Assert.AreEqual(new Vector4(0.2f, 0.5f, 1f, 0f), IsoFacetFigureInk.Value);

            IsoFacetFigureInk.Join(null, 1f, 1f);
            Assert.AreEqual(1, IsoFacetFigureInk.Count);

            _figures.Remove(b);
            Object.DestroyImmediate(b.gameObject);
            Assert.AreEqual(0, IsoFacetFigureInk.Count, "A destroyed member was not pruned.");
            Assert.AreEqual(Vector4.zero, IsoFacetFigureInk.Value);

            Shader resolveShader = Shader.Find("Hidden/HiddenHarbours/IsoFacetResolve");
            Assert.IsNotNull(resolveShader, "harness: the resolve shader is not in the project");
            var resolve = new Material(resolveShader);
            _made.Add(resolve);
            IsoFacetFigureInk.Apply(resolve, new Vector4(0.12f, 0.22f, 1f, 0f));
            Assert.AreEqual(new Vector4(0.12f, 0.22f, 1f, 0f), resolve.GetVector(IsoFacetFigureShaderIds.FigureInk));
        }

        // =================================================================== one figure's life

        [Test]
        public void TheBlinkIsSeededByTheDefAndTheFiguresOwnKey()
        {
            CharacterSkinDef def = MakeDef();
            var life = new CharacterFigureLife();
            life.Step(def, "npc.skipper_a", 0d, null, looks: false);
            Assert.AreEqual("npc.skipper_a", life.Key);
            Assert.AreEqual(CharacterFigureBlink.SeedFor(def.Id, "npc.skipper_a"), life.Blink.Seed);

            life.Step(def, "npc.skipper_b", 0d, null, looks: false);
            Assert.AreEqual(CharacterFigureBlink.SeedFor(def.Id, "npc.skipper_b"), life.Blink.Seed, "A new key did not re-seed.");

            life.Step(def, null, 0d, null, looks: false);
            Assert.AreEqual(string.Empty, life.Key);
            Assert.AreEqual(CharacterFigureBlink.SeedFor(def.Id, string.Empty), life.Blink.Seed);
        }

        [Test]
        public void TheBlinkObeysItsSwitch()
        {
            CharacterSkinDef def = MakeDef();
            var life = new CharacterFigureLife();
            bool blinked = false;
            for (double t = 0; t < 10 && !blinked; t += 0.01)
                blinked = life.Step(def, "npc.skipper", t, null, looks: false).BlinkEyes != CharacterSkinDef.NoFaceGroup;
            Assert.IsTrue(blinked, "harness: ten seconds without a blink");

            _config.CharacterBlink = false;
            var quiet = new CharacterFigureLife();
            for (double t = 0; t < 10; t += 0.01)
                Assert.AreEqual(CharacterSkinDef.NoFaceGroup, quiet.Step(def, "npc.skipper", t, null, looks: false).BlinkEyes,
                    "The blink played with its switch off.");
        }

        [Test]
        public void AFigureAsksTheSeamWithItsKeyAndKeepsOnlyWhatStandsInsideTheRadius()
        {
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer figure = MakeFigure(def);
            var life = new CharacterFigureLife();
            var spy = new Spy { Answer = new Vector3(1f, 2f, 0f) };
            CharacterLookTargets.Source = spy;
            _config.CharacterLookRadiusMetres = 3f;
            _config.CharacterLookTargetHeightMetres = 1.25f;

            IsoCharacterFigureRenderer.Life l = life.Step(def, "npc.skipper", 0d, figure, looks: true);
            Assert.AreEqual("npc.skipper", spy.Key);
            Assert.IsTrue(l.HasTarget);
            Assert.AreEqual(new Vector3(1f, 2f, 1.25f), l.Target, "The target is the ground point raised to the aim height.");
            Assert.IsTrue(l.HeadLook && l.EyeLook);

            spy.Answer = new Vector3(3f, 1f, 0f);
            Assert.IsFalse(life.Step(def, "npc.skipper", 0d, figure, looks: true).HasTarget,
                "A target past the radius was kept.");
        }

        [Test]
        public void EachLookSwitchIsReadAndThePlayersOwnNeverAsks()
        {
            CharacterSkinDef def = MakeDef();
            IsoCharacterFigureRenderer figure = MakeFigure(def);
            var life = new CharacterFigureLife();
            var spy = new Spy { Answer = new Vector3(1f, 1f, 0f) };
            CharacterLookTargets.Source = spy;

            Assert.IsFalse(life.Step(def, "player", 0d, figure, looks: false).HasTarget);
            Assert.AreEqual(0, spy.Asked, "The player's own figure asked for something to look at.");

            _config.CharacterHeadLook = false;
            IsoCharacterFigureRenderer.Life eyesOnly = life.Step(def, "npc.skipper", 0d, figure, looks: true);
            Assert.IsTrue(eyesOnly.HasTarget && !eyesOnly.HeadLook && eyesOnly.EyeLook);

            _config.CharacterEyeLook = false;
            int asked = spy.Asked;
            Assert.IsFalse(life.Step(def, "npc.skipper", 0d, figure, looks: true).HasTarget);
            Assert.AreEqual(asked, spy.Asked, "Both halves off, and the seam was still asked.");

            _config.CharacterHeadLook = true;
            IsoCharacterFigureRenderer.Life headOnly = life.Step(def, "npc.skipper", 0d, figure, looks: true);
            Assert.IsTrue(headOnly.HasTarget && headOnly.HeadLook && !headOnly.EyeLook);
        }
    }
}
