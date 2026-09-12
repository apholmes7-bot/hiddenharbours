using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE SKINNED FISHER DRAWS ABOARD, AND ONLY ABOARD.</b>
    ///
    /// <para>This is the integration half of the mesh-character presenter (ADR 0044 d): the EditMode
    /// fixtures pin the arithmetic — <c>CharacterSkinPoseTests</c> the frame and the fence,
    /// <c>CharacterSkinStateMapTests</c> the stance/gait mapping, <c>IsoCharacterFigureRendererTests</c>
    /// the skinning and the material — and this one pins the WIRING, which none of them can: that the
    /// presenter really finds the drawing hull through <c>DeckRiderVisual.LiveHullPresenter</c>, really
    /// parents itself to that hull's posed mesh, really takes the draw away from the sprite, and really
    /// gives it back when she steps ashore.</para>
    ///
    /// <para><b>The fence is the point of the fixture.</b> A mesh character cannot draw at all where
    /// the facet pass is not recorded, and the facet pass is not recorded ashore
    /// (<c>IsoFacetHullRegistry.Count &gt; 0</c> is gate 1, and that registry is fed by mesh HULLS).
    /// The presenter does not police that with a flag it could get wrong — it resolves a hull through
    /// the rider's own live presenter and finds nothing, which is mechanical rather than remembered.
    /// <see cref="Ashore_SheCannotDrawAsAMesh_AndTheReasonNamesTheFence"/> is that claim, run.</para>
    ///
    /// <para><b>Why a REAL <see cref="IsoFacetHullRenderer"/> and not a double.</b> The seam under test
    /// IS <c>presenter.Visual.GetComponent&lt;IsoFacetHullRenderer&gt;()</c> — a double behind the Core
    /// interface (the shape <c>DeckRiderHullSwapTests</c> uses, and rightly, for a question about WHICH
    /// presenter is read) would answer null there and every mesh assertion below would go green on a
    /// figure that never existed. So the fixture's presentation service installs the genuine renderer,
    /// configured from a genuine <c>HullMeshDef</c> through the shipping
    /// <see cref="IsoFacetHullPresentationService.ToSetup"/>. It installs nothing else the shipping
    /// service adds — no reflector, churn, lamps or shadow caster — because this fixture is about the
    /// figure on the deck, and every runtime subsystem it does not wake is a red it cannot suffer.</para>
    ///
    /// <para><b>Headless by construction; nothing here is GPU-gated.</b> Configuring a facet hull
    /// without a graphics device is already CI-green ground — <c>LampShadowSystemTests</c> calls
    /// <c>IsoFacetHullRenderer.Configure(...)</c> + <c>ApplyPose()</c> on the shipped dory def with no
    /// device gate at all — and the figure's own material is built from the same two shaders
    /// <c>IsoFacetShaderCompileGuardTests</c> force-compiles ungated. Nothing in this fixture renders,
    /// reads back a pixel or allocates a RenderTexture, so there is no skip to record.</para>
    ///
    /// <para><b>The two sabotage arms.</b> Rule 5 and the occlusion contract are both claims that a
    /// passing assertion could be making about nothing, so the presenter carries two mesh-only knobs
    /// and each guard proves its own arm moves it: <c>SabotageHoldFrameZero</c> must break "a later
    /// game time draws a later pose", and <c>SabotageStandOffsetMetres</c> must break "she stands where
    /// the occupant slot says she stands". Both feed the MESH only — a knob that also moved the sprite
    /// would leave the two sides agreeing and the guard measuring nothing.</para>
    /// </summary>
    public class DeckRiderMeshPresenterPlayTests
    {
        // ---- the clip's beat ---------------------------------------------------------------------
        //
        // 16 fps and a clock advanced by 1/16 s: both the interval and every partial sum of it are
        // EXACTLY representable as doubles, so `floor(elapsed * fps)` lands on the integer it looks
        // like it should. At 12 fps it does not — (1000 + 1/12) * 12 is 12000.999999999998 — and the
        // step test would silently sample the same frame twice and pass for the wrong reason.
        private const float ClipFps = 16f;
        private const int ClipFrames = 8;
        private const double FrameInterval = 1.0 / 16.0;

        private const double ClockOrigin = 1000.0;
        private const int Seed = 1337;
        private const float Tol = 1e-4f;

        private readonly List<Object> _spawned = new List<Object>();
        private IHullMeshPresentationService _previousService;
        private SteppedClock _clock;
        private SeedEnv _env;

        [SetUp]
        public void SetUp()
        {
            _previousService = HullMeshPresentation.Service;
            GameServices.Reset();
            _clock = new SteppedClock { TotalSeconds = ClockOrigin };
            _env = new SeedEnv { Seed = Seed };
            GameServices.Clock = _clock;
            GameServices.Environment = _env;
        }

        [TearDown]
        public void TearDown()
        {
            HullMeshPresentation.Service = _previousService;
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        // ---- services ----------------------------------------------------------------------------

        private sealed class SteppedClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        /// <summary>A flat sea whose only interesting property is the seed — the other half of the
        /// <c>(worldSeed, gameTime)</c> pair rule 5 makes the pose a function of.</summary>
        private sealed class SeedEnv : IEnvironmentService
        {
            public int Seed;
            public int WorldSeed => Seed;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                Vector2.zero, Vector2.zero, tideHeight: 0f,
                HiddenHarbours.Core.SeaState.Calm, visibility: 1f, seaState01: 0f);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        /// <summary>
        /// The presentation seam, installing the REAL facet renderer (see the class doc). Idempotent
        /// the way the shipping service is, and it records the instance — ⚠ every assertion about a
        /// facet property must be made against the renderer that is actually drawing, not against a
        /// second one built alongside it.
        /// </summary>
        private sealed class FacetService : IHullMeshPresentationService
        {
            public IsoFacetHullRenderer Renderer { get; private set; }

            public IHullMeshRenderer Install(GameObject host, HullMeshDef def,
                                             HullPaintSchemeDef scheme = null)
            {
                if (host == null || def == null || !def.IsUsable()) return null;
                var r = host.GetComponent<IsoFacetHullRenderer>();
                if (r == null) r = host.AddComponent<IsoFacetHullRenderer>();
                r.Configure(IsoFacetHullPresentationService.ToSetup(def, scheme));
                Renderer = r;
                return r;
            }

            public IHullPropRenderer AttachProp(GameObject host, HullPropMeshDef def, string slot) => null;
            public void DetachProps(GameObject host) { }
            public void DetachProp(GameObject host, string slot) { }
            public void Remove(GameObject host) { }
        }

        // ---- the rig -----------------------------------------------------------------------------

        private sealed class Rig
        {
            public GameObject BoatRoot;
            public DeckRiderVisual Rider;
            public IsoCharacterSprite Character;
            public SpriteRenderer Body;
            public SpriteRenderer RiderSr;
            public IsoFacetHullRenderer Hull;
            public CharacterSkinDef Skin;

            public DeckRiderMeshPresenter Presenter =>
                Rider != null ? Rider.GetComponent<DeckRiderMeshPresenter>() : null;
        }

        private static BoatHullSkinner.Options SkinOptions =>
            // No rock and no oars: the question is who draws the fisher, and a wave motion would only
            // add a moving target to every placement assertion.
            new BoatHullSkinner.Options { SkipWaveMotion = true, SkipOars = true };

        private Sprite NewCell()
        {
            var tex = new Texture2D(4, 4); _spawned.Add(tex);
            var spr = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f)); _spawned.Add(spr);
            return spr;
        }

        /// <summary>A quad bound to two of three bones — enough that the skin blend is real, small
        /// enough that a vertex hash is a cheap and total description of the pose.</summary>
        private Mesh NewBindMesh()
        {
            var m = new Mesh { name = "HHPlayBind" }; _spawned.Add(m);
            m.SetVertices(new[]
            {
                new Vector3(-0.2f, 0f, 0f), new Vector3(0.2f, 0f, 0f),
                new Vector3(0.2f, 0f, 1.6f), new Vector3(-0.2f, 0f, 1.6f),
            });
            m.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            m.boneWeights = new[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f, boneIndex1 = 0, weight1 = 0f },
                new BoneWeight { boneIndex0 = 0, weight0 = 1f, boneIndex1 = 0, weight1 = 0f },
                new BoneWeight { boneIndex0 = 1, weight0 = 0.5f, boneIndex1 = 2, weight1 = 0.5f },
                new BoneWeight { boneIndex0 = 1, weight0 = 0.5f, boneIndex1 = 2, weight1 = 0.5f },
            };
            m.bindposes = new[]
            {
                Matrix4x4.identity,
                Matrix4x4.TRS(new Vector3(0f, 0f, -0.8f), Quaternion.identity, Vector3.one),
                Matrix4x4.TRS(new Vector3(0f, 0f, -1.6f), Quaternion.identity, Vector3.one),
            };
            m.RecalculateBounds();
            return m;
        }

        private static CharacterSkinDef.SkinClip Clip(string key, int frames)
        {
            const int bones = 3;
            var keys = new CharacterSkinDef.BoneKey[frames * bones];
            for (int f = 0; f < frames; f++)
                for (int b = 0; b < bones; b++)
                    keys[f * bones + b] = new CharacterSkinDef.BoneKey
                    {
                        // Every frame is a DIFFERENT pose and no two frames coincide, so a vertex hash
                        // identifies the frame that was drawn rather than merely agreeing with it.
                        Position = new Vector3(0f, 0f, 0.8f * b),
                        Rotation = Quaternion.AngleAxis(f * 7f + b * 3f, Vector3.right),
                    };
            return new CharacterSkinDef.SkinClip
            {
                Anim = key, State = key, FramesPerSecond = ClipFps, Loop = true,
                FrameCount = frames, Keys = keys,
            };
        }

        /// <summary>A usable skin. <paramref name="meshStates"/> is the ADR 0041 per-state switch —
        /// pass none to get a def that is perfectly bakeable and that the presenter must still refuse
        /// to draw.</summary>
        private CharacterSkinDef NewSkin(params string[] meshStates)
        {
            var def = ScriptableObject.CreateInstance<CharacterSkinDef>(); _spawned.Add(def);
            def.Id = "character.playtest_fisher";
            def.BindMesh = NewBindMesh();
            def.Bones = new[]
            {
                new CharacterSkinDef.Bone { Id = "root", Parent = CharacterSkinDef.NoBone, OwnsVertex = true },
                new CharacterSkinDef.Bone { Id = "spine", Parent = 0, OwnsVertex = true },
                new CharacterSkinDef.Bone { Id = "head", Parent = 1, OwnsVertex = true },
            };
            def.MaxInfluences = 2;
            def.Materials = new[]
            {
                new CharacterSkinDef.Material
                {
                    Name = "skin",
                    Colors = new[]
                    {
                        new Color32(10, 20, 30, 255), new Color32(40, 50, 60, 255),
                        new Color32(70, 80, 90, 255),
                    },
                    Offset = 0,
                    Gain = 1f,
                    // NaN = "this material declares none", so the def's global Bias applies — the
                    // spelling CharacterSkinDef.Material.BiasOr expects, not a zero that would read
                    // as a deliberate flattening.
                    Bias = float.NaN,
                    // -1 = a shaded material a polygon can reference. 0 would mark it one of the
                    // head rig's raster STAMPS, which is the one thing this mesh does not carry.
                    FixedIndex = -1,
                },
            };
            var bayer = new float[16];
            for (int i = 0; i < 16; i++) bayer[i] = (i + 0.5f) / 16f;
            def.Bayer16 = bayer;
            def.Keyline = new Color32(12, 14, 18, 255);
            def.LightN = new Vector3(0.3f, -0.5f, 0.81f);
            def.Gain = 1.15f;
            def.Bias = -0.04f;
            def.CellW = 64; def.CellH = 92;
            def.PivotPx = new Vector2(32f, 80f);
            def.PxPerMetre = 32;
            def.ElevationDeg = 40f;
            def.Clips = new[]
            {
                Clip(CharacterSkinStateMap.Idle, ClipFrames),
                Clip(CharacterSkinStateMap.Walk, ClipFrames),
                Clip(CharacterSkinStateMap.Balance, ClipFrames),
            };
            def.MeshStates = meshStates;
            Assert.IsTrue(def.IsUsable(),
                "harness: the synthetic skin must satisfy IsUsable() or the presenter refuses it for " +
                "the fixture's own reason and every assertion below measures the wrong gate");
            return def;
        }

        /// <summary>The character art def. It carries a minimal idle+walk sheet so
        /// <c>IsoCharacterSprite</c>'s ladder actually RUNS — with no art it returns before resolving a
        /// gait, and the gait the mesh reads would be frozen at its default for the wrong reason.</summary>
        private CharacterVisualDef NewVisual(CharacterSkinDef skin)
        {
            var v = ScriptableObject.CreateInstance<CharacterVisualDef>(); _spawned.Add(v);
            v.Id = "character.playtest_visual";
            v.FacingCount = 8;
            v.IdleFrameCount = 2; v.IdleFramesPerSecond = 6f;
            v.WalkFrameCount = 2; v.WalkFramesPerSecond = 8f;
            Sprite cell = NewCell();
            var idle = new Sprite[v.FacingCount * v.IdleFrameCount];
            for (int i = 0; i < idle.Length; i++) idle[i] = cell;
            var walk = new Sprite[v.FacingCount * v.WalkFrameCount];
            for (int i = 0; i < walk.Length; i++) walk[i] = cell;
            v.IdleSheet = idle;
            v.WalkSheet = walk;
            v.Skin = skin;
            Assert.IsTrue(v.HasAnyArt(), "harness: the sprite ladder must have art to walk down");
            return v;
        }

        private BoatVisualDef NewMeshHullVisual()
        {
            var mesh = new Mesh { name = "HHPlayHull" }; _spawned.Add(mesh);
            mesh.SetVertices(new[]
            {
                new Vector3(-1f, -2f, 0f), new Vector3(1f, -2f, 0f),
                new Vector3(1f, 2f, 0f), new Vector3(-1f, 2f, 0f),
            });
            mesh.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();

            var def = ScriptableObject.CreateInstance<HullMeshDef>(); _spawned.Add(def);
            def.Id = "hullmesh.playtest_deck";
            def.Mesh = mesh;
            def.Ramps = new[]
            {
                new HullMeshDef.Ramp { Colors = new[] { new Color32(1, 2, 3, 255) }, Offset = 0 },
            };
            def.Bayer16 = new float[16];
            def.PxPerMetre = 32;
            def.CellW = 456; def.CellH = 420;
            def.ElevationDeg = 40f;
            def.AzimuthCounterClockwise = true;
            Assert.IsTrue(def.IsUsable(), "harness: the hull def must be installable");

            var v = ScriptableObject.CreateInstance<BoatVisualDef>(); _spawned.Add(v);
            v.Id = "visual.playtest_mesh";
            v.Variant = BoatHullVariant.Mesh;
            v.HullMesh = def;
            v.ArtBakeElevationDegrees = 40f;
            return v;
        }

        /// <summary>
        /// A mesh boat with a fisher on her deck. <paramref name="meshOn"/> is
        /// <c>GameConfig.MeshCharacter</c> — the one switch of rule 6, default off — and
        /// <paramref name="meshStates"/> is the ADR 0041 per-state list.
        /// </summary>
        private Rig NewRig(bool meshOn, params string[] meshStates)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>(); _spawned.Add(config);
            config.MeshCharacter = meshOn;
            GameServices.Config = config;

            var service = new FacetService();
            HullMeshPresentation.Service = service;

            var root = new GameObject("Boat"); _spawned.Add(root);
            BoatHullSkinner.Apply(root, NewMeshHullVisual(), boat: null, SkinOptions);

            var rig = new Rig { BoatRoot = root, Hull = service.Renderer };
            Assert.IsNotNull(rig.Hull,
                "harness: the facet hull must have been installed — without one the presenter has " +
                "nothing to resolve and every mesh assertion would pass on an absence");
            Assert.IsNotNull(rig.Hull.PosedMesh,
                "harness: the hull must be CONFIGURED (its posed-mesh child is the figure's parent " +
                "frame). If this is the first red on CI, the cause is IsoFacetHullRenderer.Configure " +
                "headless — the precedent that says it is safe is LampShadowSystemTests, which " +
                "configures the shipped dory with no graphics-device gate.");

            var playerGo = new GameObject("Player"); _spawned.Add(playerGo);
            rig.Body = playerGo.AddComponent<SpriteRenderer>();
            rig.Character = playerGo.AddComponent<IsoCharacterSprite>();
            rig.Skin = NewSkin(meshStates);
            rig.Character.Configure(NewVisual(rig.Skin));

            var riderGo = new GameObject("DeckRider");
            riderGo.transform.SetParent(playerGo.transform, false);
            rig.RiderSr = riderGo.AddComponent<SpriteRenderer>();
            rig.RiderSr.enabled = false;

            rig.Rider = playerGo.AddComponent<DeckRiderVisual>();
            rig.Rider.Configure(rig.RiderSr, rig.Body, rig.Character);
            return rig;
        }

        private IEnumerator Board(Rig rig)
        {
            rig.Rider.SetMode(ControlMode.Aboard, rig.BoatRoot.transform);
            // Two frames: the first runs Update()'s StateContext and LateUpdate()'s Apply, the second
            // gives the figure a frame whose deck step was measured rather than seeded.
            yield return null;
            yield return null;
        }

        // ---- reading the drawn pose ---------------------------------------------------------------

        /// <summary>The vertices actually handed to the drawing MeshRenderer — the pose, as the GPU
        /// would get it, rather than the index of the frame that was asked for.</summary>
        private static Vector3[] DrawnVertices(DeckRiderMeshPresenter presenter)
        {
            Assert.IsNotNull(presenter, "no presenter");
            IsoCharacterFigureRenderer figure = presenter.Figure;
            Assert.IsNotNull(figure, "the figure has not been built");
            Transform child = figure.transform.Find("FacetFigure");
            Assert.IsNotNull(child, "the figure's drawn child is named FacetFigure");
            var mf = child.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf, "the drawn child carries the posed mesh");
            Assert.IsNotNull(mf.sharedMesh, "the posed mesh must exist before a pose can be compared");
            return mf.sharedMesh.vertices;
        }

        private static void AssertSamePose(Vector3[] a, Vector3[] b, string what)
        {
            Assert.AreEqual(a.Length, b.Length, what + " — vertex counts differ");
            for (int i = 0; i < a.Length; i++)
                Assert.AreEqual(0f, Vector3.Distance(a[i], b[i]), Tol,
                                what + " — vertex " + i + " moved: " + a[i] + " vs " + b[i]);
        }

        private static bool PosesDiffer(Vector3[] a, Vector3[] b)
        {
            if (a.Length != b.Length) return true;
            for (int i = 0; i < a.Length; i++)
                if (Vector3.Distance(a[i], b[i]) > Tol) return true;
            return false;
        }

        // ---- the switch ---------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WithTheSwitchOff_NoPresenterIsEverAddedAndTheSpriteKeepsTheDraw()
        {
            // Rule 6's toggle, in its shipped position. The claim is STRUCTURAL rather than
            // behavioural: with the switch off the presenter is not merely inert, it does not exist —
            // which is what makes the toggle-0 plate byte-identical instead of merely similar. If this
            // fixture ever finds a component here, the off state has stopped being the pre-PR code.
            Rig rig = NewRig(meshOn: false, CharacterSkinStateMap.Idle);
            yield return Board(rig);

            Assert.IsNull(rig.Presenter,
                "the switch is off: DeckRiderVisual must not have added a mesh presenter at all");
            Assert.IsFalse(rig.Rider.SpriteSuppressed, "and nothing is holding the sprite off");
            Assert.IsTrue(rig.RiderSr.enabled, "so the deck-rider sprite is the thing on screen");
        }

        [UnityTest]
        public IEnumerator AboardAFacetHull_TheMeshTakesTheDrawAndTheSpriteStandsDown()
        {
            Rig rig = NewRig(meshOn: true, CharacterSkinStateMap.Idle, CharacterSkinStateMap.Walk);
            yield return Board(rig);

            DeckRiderMeshPresenter presenter = rig.Presenter;
            Assert.IsNotNull(presenter, "the switch is on and the art def names a skin: a presenter is added");
            Assert.IsNull(presenter.NotDrawingReason,
                "she must be drawing as a mesh; the presenter says why not when she is not");
            Assert.IsTrue(presenter.DrawsInsteadOfSprite);

            Assert.IsTrue(rig.Rider.SpriteSuppressed, "ONE figure, not two: the rider reports the swap");
            Assert.IsFalse(rig.RiderSr.enabled, "and the sprite it was drawing is off");

            Assert.AreSame(rig.Hull, presenter.Hull,
                "she must have resolved the hull she is standing on, through the rider's live presenter");
            Assert.IsNotNull(presenter.Figure, "and built a figure on it");
            Assert.AreSame(rig.Hull.PosedMesh, presenter.Figure.transform.parent,
                "the figure hangs off the hull's POSED MESH — that transform is the rig frame her " +
                "stand point is expressed in, and parenting anywhere else would put her in a second " +
                "frame that only agrees while the hull is level and square");
        }

        [UnityTest]
        public IEnumerator Ashore_SheCannotDrawAsAMesh_AndTheReasonNamesTheFence()
        {
            // THE FENCE. Ashore there is no facet pass to draw into — the pass is gated on a registry
            // of mesh HULLS — so the mesh figure is not "hidden", she is impossible. The presenter
            // still exists (the switch is on and she has a skin), which is exactly what makes the
            // assertion worth making: it is the DRAW that is refused, not the component that is absent.
            Rig rig = NewRig(meshOn: true, CharacterSkinStateMap.Idle);
            rig.Rider.SetMode(ControlMode.OnFoot, null);
            yield return null;
            yield return null;

            DeckRiderMeshPresenter presenter = rig.Presenter;
            Assert.IsNotNull(presenter, "the presenter is added on the switch, not on being aboard");
            Assert.IsFalse(presenter.DrawsInsteadOfSprite, "ashore the mesh cannot draw at all");
            Assert.AreEqual("ashore — the facet pass is not recorded there", presenter.NotDrawingReason,
                "and the reason must NAME the fence — a plate that came back as the sprite otherwise " +
                "does not say which gate shut");
            Assert.IsNull(presenter.Figure, "nothing is built for a draw that cannot happen");
            Assert.IsFalse(rig.Rider.SpriteSuppressed, "the sprite keeps the draw it always had ashore");
        }

        [UnityTest]
        public IEnumerator AStateTheBakeDoesNotMarkAsMesh_IsRefusedRatherThanDrawn()
        {
            // ADR 0041's per-state switch. The def is perfectly usable and carries an `idle` clip —
            // the ONLY thing missing is `idle` in MeshStates, and that alone must stop the draw.
            Rig rig = NewRig(meshOn: true /* no mesh states at all */);
            yield return Board(rig);

            DeckRiderMeshPresenter presenter = rig.Presenter;
            Assert.IsNotNull(presenter);
            Assert.IsFalse(presenter.DrawsInsteadOfSprite);
            StringAssert.Contains("MeshStates", presenter.NotDrawingReason,
                "the refusal must name the per-state switch, not read as a missing clip");
            Assert.IsTrue(rig.RiderSr.enabled, "and the sprite draws her, as it does today");
        }

        // ---- rule 5: the pose is a function of (worldSeed, gameTime) -------------------------------

        [UnityTest]
        public IEnumerator TheClipSteps_OneFrameAtATime_AndVisitsEveryFrameOfItsCycle()
        {
            // Discrete samples, stepped — never blended. Eight ticks of 1/16 s must walk the whole
            // eight-frame cycle and land back where they started, and no two adjacent samples may be
            // the same pose (a blend or a stall would show up as either).
            Rig rig = NewRig(meshOn: true, CharacterSkinStateMap.Idle);
            yield return Board(rig);

            DeckRiderMeshPresenter presenter = rig.Presenter;
            Assert.IsNull(presenter.NotDrawingReason, "harness: she must be drawing before we time her");

            var frames = new List<int>();
            var poses = new List<Vector3[]>();
            for (int i = 0; i < ClipFrames; i++)
            {
                frames.Add(presenter.DrawnFrame);
                poses.Add(DrawnVertices(presenter));
                _clock.TotalSeconds += FrameInterval;
                yield return null;
            }

            CollectionAssert.AllItemsAreUnique(frames,
                "eight ticks of a clip's own beat must visit eight DIFFERENT frames — a repeat means " +
                "the frame is not being read off the clock at the clip's rate");
            for (int i = 0; i < frames.Count; i++)
            {
                Assert.GreaterOrEqual(frames[i], 0);
                Assert.Less(frames[i], ClipFrames);
                Assert.AreEqual((frames[0] + i) % ClipFrames, frames[i],
                    "the cycle must advance by exactly one frame per interval, in order");
            }
            for (int i = 1; i < poses.Count; i++)
                Assert.IsTrue(PosesDiffer(poses[i - 1], poses[i]),
                    "frame " + frames[i - 1] + " and frame " + frames[i] + " drew the SAME vertices — " +
                    "the frame index moved but the skin did not, so nothing downstream of the index " +
                    "is actually being re-posed");
        }

        [UnityTest]
        public IEnumerator TheSamePairOfSeedAndTime_DrawsTheSamePose_AndTheSabotageBreaksIt()
        {
            // Rule 5, end to end and through the real component: two independently built rigs at the
            // same (worldSeed, gameTime) must agree on the bone pose down to the vertex.
            Rig first = NewRig(meshOn: true, CharacterSkinStateMap.Idle);
            yield return Board(first);
            Assert.IsNull(first.Presenter.NotDrawingReason, "harness: the first rig must be drawing");
            int frameA = first.Presenter.DrawnFrame;
            Vector3[] poseA = DrawnVertices(first.Presenter);

            // Tear the whole world down and build it again at the same instant.
            TearDown();
            SetUp();
            Rig second = NewRig(meshOn: true, CharacterSkinStateMap.Idle);
            yield return Board(second);
            Assert.IsNull(second.Presenter.NotDrawingReason, "harness: the second rig must be drawing");

            Assert.AreEqual(frameA, second.Presenter.DrawnFrame,
                "same seed, same game time, same frame — a replayed save must draw the same picture");
            AssertSamePose(poseA, DrawnVertices(second.Presenter),
                "same seed and time must skin to the same vertices");

            // THE ARM. With the frame pinned at zero, "a later game time draws a later pose" must fail;
            // if it does not, the guard above was agreeing with a figure that never moved.
            second.Presenter.SabotageHoldFrameZero = true;
            yield return null;
            Assert.AreEqual(0, second.Presenter.DrawnFrame, "the sabotage pins the frame");

            _clock.TotalSeconds += FrameInterval * 3d;
            yield return null;
            Assert.AreEqual(0, second.Presenter.DrawnFrame,
                "…and holds it across a clock advance, which is the failure the guard must be able to see");

            second.Presenter.SabotageHoldFrameZero = false;
            yield return null;
            Assert.AreNotEqual(0, second.Presenter.DrawnFrame,
                "with the arm released the clock moves her again — so the arm was genuinely the only " +
                "thing holding her, and the guard above was measuring the clock");
        }

        [UnityTest]
        public IEnumerator TheSeedPhasesTheLoop_SoTwoWorldsDoNotIdleInLockstep()
        {
            // The other half of the (worldSeed, gameTime) pair. A looping clip is phased by the seed so
            // two figures in one world are not in lockstep while one world replays identically — so at
            // a FIXED instant, sweeping the seed must move the frame.
            var seen = new HashSet<int>();
            int[] seeds = { 1337, 1338, 7, 99, 424242, -1, 0, 20260912 };
            foreach (int seed in seeds)
            {
                TearDown();
                SetUp();
                _env.Seed = seed;
                Rig rig = NewRig(meshOn: true, CharacterSkinStateMap.Idle);
                yield return Board(rig);
                Assert.IsNull(rig.Presenter.NotDrawingReason, "harness: seed " + seed + " must draw");
                seen.Add(rig.Presenter.DrawnFrame);
            }

            Assert.Greater(seen.Count, 1,
                "eight different world seeds all started the idle loop on the same frame — the seed is " +
                "not reaching the phase, and every world would idle in lockstep");
        }

        // ---- the stand point and the occupant slot -------------------------------------------------

        [UnityTest]
        public IEnumerator SheStandsExactlyWhereTheOccupantSlotSaysSheStands_AndTheSabotageBreaksIt()
        {
            // THE OCCLUSION CONTRACT, as a placement. The hull draws over her crew by ranking depth at
            // the point the rider PUBLISHED to the deck-occupant slot; if the mesh figure stood
            // anywhere else the ranking would be right about a fisher who is not there. The presenter
            // therefore reads DeckStandRigLocal — the very vector the slot was fed — and never derives
            // a stand point of its own.
            Rig rig = NewRig(meshOn: true, CharacterSkinStateMap.Idle);
            yield return Board(rig);

            DeckRiderMeshPresenter presenter = rig.Presenter;
            Assert.IsNull(presenter.NotDrawingReason, "harness: she must be drawing to be placed");

            Vector3 published = rig.Rider.DeckStandRigLocal;
            Assert.AreEqual(0f, Vector3.Distance(published, presenter.FigureLocalMetres), Tol,
                "the figure is placed at the published stand point, not at one of its own");
            Assert.AreEqual(0f,
                Vector3.Distance(published, presenter.Figure.transform.localPosition), Tol,
                "…and that is really the transform, not merely a field that agrees with one");

            // THE ARM. Shift the mesh — and only the mesh — off the slot's point.
            var offset = new Vector3(0.75f, -0.25f, 0.5f);
            presenter.SabotageStandOffsetMetres = offset;
            yield return null;

            Assert.AreEqual(0f,
                Vector3.Distance(rig.Rider.DeckStandRigLocal + offset, presenter.FigureLocalMetres), Tol,
                "the arm must move the figure by exactly the offset — a guard whose sabotage cannot " +
                "move the thing it measures is measuring nothing");
            Assert.AreEqual(0f, Vector3.Distance(published, rig.Rider.DeckStandRigLocal), Tol,
                "…and must NOT have moved the slot's own point: a knob that shifted both sides would " +
                "leave them agreeing and the guard green");
        }

        [UnityTest]
        public IEnumerator TheDeckSlotIsHeldWhileAboardAndReleasedOnDismount_WithTheMeshDrawing()
        {
            // The occupant slot is DeckRiderVisual's to feed, and this PR does not touch it — which is
            // exactly why it is worth running under the mesh path: the claim is that installing the
            // figure changed nothing about who is marked as standing on the deck.
            Rig rig = NewRig(meshOn: true, CharacterSkinStateMap.Idle);
            yield return Board(rig);

            Assert.IsTrue(rig.Presenter.DrawsInsteadOfSprite, "harness: the mesh has the draw");
            Assert.GreaterOrEqual(rig.Rider.DeckOccupantSlot, 0, "she holds a slot on the hull she is on");
            Assert.AreEqual(1, rig.Hull.DeckOccupants.ActiveCount,
                "and the hull actually drawing knows one person is aboard");
            Assert.Less(rig.Rider.DeckOccupantSlot, rig.Hull.DeckOccupants.Capacity,
                "and it is one of the hull's own twelve, not an index she invented");

            rig.Rider.SetMode(ControlMode.OnFoot, null);
            yield return null;
            yield return null;

            Assert.Less(rig.Rider.DeckOccupantSlot, 0, "stepping ashore gives the slot back");
            Assert.AreEqual(0, rig.Hull.DeckOccupants.ActiveCount,
                "a stranded occupant would keep the hull splitting her own image around a fisher who " +
                "left");
            Assert.IsFalse(rig.Presenter.DrawsInsteadOfSprite, "and the mesh stops drawing with her");
            Assert.IsTrue(rig.Rider.SpriteSuppressed == false, "the sprite has the draw back");
        }

        // ---- the sprite's inputs, and only the sprite's inputs --------------------------------------

        [UnityTest]
        public IEnumerator TheDrawnClipFollowsTheStanceTheRiderAsked_NotASecondOpinion()
        {
            // THE MESH READS THE SPRITE'S INPUTS, AND ONLY THE SPRITE'S INPUTS — driven the way the
            // game drives it. DeckRiderVisual republishes the stance from the MODE every Update, so a
            // test that poked IsoCharacterSprite.Stance directly would be overwritten before LateUpdate
            // and would prove only that the poke lost. Crossing the deck (OnDeck) is the one stance rig
            // 7 exports a clip of its own for.
            Rig rig = NewRig(meshOn: true, CharacterSkinStateMap.Idle, CharacterSkinStateMap.Balance);
            yield return Board(rig);
            Assert.IsNull(rig.Presenter.NotDrawingReason, "harness: she must be drawing");
            Assert.AreEqual(CharacterStance.Free, rig.Rider.RequestedStance,
                "harness: at the helm of a boat with no visual def wired she simply stands there");
            Assert.AreEqual(CharacterSkinStateMap.Idle, rig.Presenter.DrawnStateKey,
                "a still fisher standing free draws the free idle body");

            rig.Rider.SetMode(ControlMode.OnDeck, rig.BoatRoot.transform);
            yield return null;
            yield return null;

            Assert.AreEqual(CharacterStance.Balance, rig.Rider.RequestedStance,
                "harness: crossing the deck is the brace");
            Assert.AreEqual(CharacterSkinStateMap.Balance, rig.Presenter.DrawnStateKey,
                "and the mesh draws the balance clip — the SAME stance the sprite was handed, read at " +
                "the same seam, not a second opinion about what she is doing");
            Assert.AreEqual(CharacterStance.Free, rig.Character.DrawnStance,
                "…while the SHEET ladder collapsed that same request to the free body, because THIS " +
                "fixture's visual def bakes no stance sheet. (The shipped FisherIso does carry one, so " +
                "that is the fixture's gap, not the game's.) Either way DrawnStance reports on SPRITE " +
                "art coverage and not on what she is doing — which is exactly why the mesh reads the " +
                "REQUESTED stance: reading DrawnStance would make `balance` unreachable for the mesh " +
                "on any def whose balance sheet came up short.");
        }
    }
}
