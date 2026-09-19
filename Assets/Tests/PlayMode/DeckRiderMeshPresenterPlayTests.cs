using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
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
    /// <para><b>The fence is the point of the fixture.</b> Ashore a mesh character draws only behind
    /// two gates, and neither is a flag the presenter could get wrong: <c>GameConfig.MeshCharacterAshore</c>
    /// (code default OFF, live only with <c>MeshCharacter</c> on too — the owner's ruling of
    /// 2026-09-19), and a facet id of her OWN from the pool the hulls draw on, which is what records the
    /// facet pass where no hull is (<c>IsoFacetHullRegistry.FigureCount &gt; 0</c> opens gate 1 exactly
    /// as a hull does). With the switch off she is main's sprite, byte for byte — no ashore figure, no
    /// id, her body untouched — and <see cref="Ashore_SheCannotDrawAsAMesh_AndTheReasonNamesTheFence"/>
    /// with <see cref="WithTheAshoreSwitchOff_AshoreIsMainsFrame_NoFigureNoIdAndTheBodyUntouched"/> is
    /// that claim, run. With it on, the ashore section at the foot holds the hand-over to one bar:
    /// <b>at no frame of boarding or landing are the sprite and the mesh both drawn, or neither.</b></para>
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
    /// <c>IsoFacetShaderCompileGuardTests</c> force-compiles ungated. Her ASHORE figure adds one
    /// <c>Shader.Find("HiddenHarbours/IsoFacetOverlay")</c> for its overlay quad — the call the EditMode
    /// <c>IsoCharacterFigureRendererTests</c> already make ungated. Nothing in this fixture renders,
    /// reads back a pixel or allocates a RenderTexture, so there is no skip to record: every claim here
    /// is about WHICH renderer is switched on, and whether it draws RIGHT is a plate
    /// (<c>AshoreFigurePlatePlayTests</c>, on a granted slot).</para>
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

        /// <summary>The vault's BOARD clip: two cells, a one-shot short enough to finish inside a guard.</summary>
        private const int BoardClipFrames = 2;

        // ---- the ashore bars ----------------------------------------------------------------------
        //
        // Stated here as literals and never read back from the presenter: a guard that asks the code
        // for its own bar is a mirror, and a reason that moved would move both sides at once.

        // Who is on screen, as WhoDraws names it.
        private const string BodySprite = "the body sprite";
        private const string RiderSprite = "the deck-rider sprite";
        private const string AboardMesh = "the aboard mesh";
        private const string AshoreMesh = "the ashore mesh";
        private const string Nobody = "nobody";

        private const string AshoreOffReason = "ashore — GameConfig.MeshCharacterAshore is off";
        private const string RefusedReason =
            "ashore — EnterAshore refused her (the facet-id pool is used up): she keeps her whole sprite " +
            "until her next arrival ashore";
        private const string NeitherReason =
            "inside something (a cab, or a helm that hides its pilot): she draws neither";
        private const string HiddenElsewhereReason =
            "ashore — her body's forceRenderingOff was set by something other than this figure";
        private const string ClipReason = "ashore — a clip is playing, and the sprite draws every clip";
        /// <summary>The registry's warning for a figure the pool refuses, once per ask.</summary>
        private const string NoFacetIdWarning = "this figure gets NO facet id";

        /// <summary>The id no figure may take: a pool that has lent 1..254 is a pool at its end — the
        /// state NMC's cold start reaches with 255 ids in use.</summary>
        private const int FacetIdCeiling = 255;
        /// <summary>The one id handed back to a drained pool, to prove the next ARRIVAL takes it.</summary>
        private const int FreedId = 200;
        private const int RefusalHoldFrames = 30;

        private const float VaultSeconds = 0.5f;
        /// <summary>Wall-clock cap on waiting out a clip: a clip that never ends reds, never hangs.</summary>
        private const float VaultCapSeconds = 10f;

        private const float HeldHeading = 135f;
        private const float YawTolDegrees = 0.01f;

        /// <summary>Frames a hand-over is watched for after its call: the frame the call lands in, the
        /// one after (where a late write would show), and one more.</summary>
        private const int WatchFrames = 3;

        /// <summary>A hull re-skinned under her feet, on MAIN's aboard path: the old posed mesh — and
        /// her aboard figure with it — dies at the end of the swap's frame and the figure is rebuilt on
        /// the next pose. One frame of neither, pre-existing, allowed at the swap and nowhere else.</summary>
        private const int SwapFramesMainMayDrawNeither = 1;

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
            public GameConfig Config;
            /// <summary>The one cell the BOARD clip draws, and no gait draws: seeing it on her body is
            /// seeing the clip, not the idle she was standing in.</summary>
            public Sprite BoardCell;

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
        /// gait, and the gait the mesh reads would be frozen at its default for the wrong reason — and a
        /// complete two-frame BOARD clip (the vault's) in a cell of its own,
        /// <paramref name="boardCell"/>, so a guard can tell the clip's frame from the gait's.</summary>
        private CharacterVisualDef NewVisual(CharacterSkinDef skin, out Sprite boardCell)
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
            boardCell = NewCell();
            var board = new Sprite[v.FacingCount * BoardClipFrames];
            for (int i = 0; i < board.Length; i++) board[i] = boardCell;
            v.BoardClip = new CharacterClipSheets
            {
                Sheet = board, FrameCount = BoardClipFrames, FramesPerSecond = ClipFps, Loops = false,
            };
            v.Skin = skin;
            Assert.IsTrue(v.HasAnyArt(), "harness: the sprite ladder must have art to walk down");
            Assert.IsTrue(v.HasClip(CharacterClip.Board),
                "harness: the board clip must be COMPLETE, or CharacterClipPlayer.Play refuses it and " +
                "the vault guard measures a clip that never played");
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
        /// <paramref name="meshStates"/> is the ADR 0041 per-state list. The ashore switch is left at
        /// its shipped OFF, so every guard written before it existed runs main's frame.
        /// </summary>
        private Rig NewRig(bool meshOn, params string[] meshStates) => NewRig(meshOn, false, meshStates);

        /// <summary>The same rig with <paramref name="ashoreOn"/> as
        /// <c>GameConfig.MeshCharacterAshore</c> (default off, live only with <paramref name="meshOn"/>).
        /// The config is kept on the rig so a guard can turn a switch at run time.</summary>
        private Rig NewRig(bool meshOn, bool ashoreOn, params string[] meshStates)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>(); _spawned.Add(config);
            config.MeshCharacter = meshOn;
            config.MeshCharacterAshore = ashoreOn;
            GameServices.Config = config;

            var service = new FacetService();
            HullMeshPresentation.Service = service;

            var root = new GameObject("Boat"); _spawned.Add(root);
            BoatHullSkinner.Apply(root, NewMeshHullVisual(), boat: null, SkinOptions);

            var rig = new Rig { BoatRoot = root, Hull = service.Renderer, Config = config };
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
            rig.Character.Configure(NewVisual(rig.Skin, out rig.BoardCell));

            var riderGo = new GameObject("DeckRider");
            riderGo.transform.SetParent(playerGo.transform, false);
            rig.RiderSr = riderGo.AddComponent<SpriteRenderer>();
            rig.RiderSr.enabled = false;

            rig.Rider = playerGo.AddComponent<DeckRiderVisual>();
            rig.Rider.Configure(rig.RiderSr, rig.Body, rig.Character);
            return rig;
        }

        /// <summary>Both switches on, and every state she passes through here a mesh state: the rig
        /// the ashore guards stand on. Nothing has posed her yet — the first pose is the caller's.</summary>
        private Rig NewAshoreRig() => NewRig(meshOn: true, ashoreOn: true,
            CharacterSkinStateMap.Idle, CharacterSkinStateMap.Walk, CharacterSkinStateMap.Balance);

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

        // ---- who is on screen ----------------------------------------------------------------------

        /// <summary>A renderer that puts pixels on screen: enabled, not forced off, in an active
        /// hierarchy — and, for a sprite, holding one.</summary>
        private static bool Draws(Renderer r) =>
            r != null && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy &&
            (!(r is SpriteRenderer sr) || sr.sprite != null);

        /// <summary>A mesh figure that draws: shown, and in a facet pass — under a hull, or holding a
        /// facet id of her own ashore. A shown figure with neither is recorded by no pass.</summary>
        private static bool Draws(IsoCharacterFigureRenderer f) =>
            f != null && f.gameObject.activeInHierarchy && f.Visible &&
            (f.IsAshore || f.GetComponentInParent<IsoFacetHullRenderer>() != null);

        /// <summary>
        /// Everything that draws her this frame, named: the body sprite, the deck-rider sprite, and EVERY
        /// mesh figure under her or under the boat — not only the ones the presenter still holds, so a
        /// figure it lost track of reads as a second drawer instead of hiding behind its bookkeeping.
        /// </summary>
        private static string WhoDraws(Rig rig)
        {
            var who = new List<string>();
            if (Draws(rig.Body)) who.Add(BodySprite);
            if (Draws(rig.RiderSr)) who.Add(RiderSprite);

            DeckRiderMeshPresenter p = rig.Presenter;
            var figures = new List<IsoCharacterFigureRenderer>();
            if (rig.Body != null) figures.AddRange(rig.Body.GetComponentsInChildren<IsoCharacterFigureRenderer>(true));
            if (rig.BoatRoot != null) figures.AddRange(rig.BoatRoot.GetComponentsInChildren<IsoCharacterFigureRenderer>(true));
            var seen = new HashSet<IsoCharacterFigureRenderer>();
            foreach (IsoCharacterFigureRenderer f in figures)
            {
                if (!seen.Add(f) || !Draws(f)) continue;
                if (p != null && f == p.Figure) who.Add(AboardMesh);
                else if (p != null && f == p.AshoreFigure) who.Add(AshoreMesh);
                else who.Add("an orphan figure '" + f.name + "'");
            }
            return who.Count == 0 ? Nobody : string.Join(" + ", who);
        }

        /// <summary>THE BAR: exactly <paramref name="expected"/> draws her — never both, never neither.</summary>
        private static void AssertDrawnBy(Rig rig, string expected, string when)
        {
            DeckRiderMeshPresenter p = rig.Presenter;
            Assert.AreEqual(expected, WhoDraws(rig),
                when + " (presenter: " + (p != null ? p.NotDrawingReason ?? "drawing" : "none") + ")");
        }

        /// <summary>Decision 2: the ashore figure and the id she took are hers for the session.</summary>
        private static void AssertKeptId(Rig rig, int id, int baseline, string when)
        {
            Assert.IsNotNull(rig.Presenter.AshoreFigure, when + ": her ashore figure is kept, not destroyed");
            Assert.AreEqual(id, rig.Presenter.AshoreFigure.FigureId, when + ": …with the id she took");
            Assert.AreEqual(baseline + 1, IsoFacetHullRegistry.FigureCount, when + ": …and no second one");
        }

        /// <summary>Main's shore: the body sprite alone, no ashore figure held or left in her hierarchy,
        /// no id taken, her body not forced off, and the reason naming the switch.</summary>
        private static void AssertMainsShore(Rig rig, int baseline, string when)
        {
            AssertDrawnBy(rig, BodySprite, when);
            Assert.IsNull(rig.Presenter.AshoreFigure, when + ": no ashore figure is held");
            Assert.IsNull(rig.Body.transform.Find("MeshCharacterAshore"), when + ": …or left in her hierarchy");
            Assert.AreEqual(baseline, IsoFacetHullRegistry.FigureCount, when + ": no facet id is taken");
            Assert.IsFalse(rig.Body.forceRenderingOff, when + ": her body is not forced off");
            Assert.AreEqual(AshoreOffReason, rig.Presenter.NotDrawingReason, when + ": the reason names the switch");
        }

        /// <summary>The named NEITHER: nobody draws, the reason says why, the figure holds nothing of her
        /// body (the rider hides it) and her id is kept for when she steps out.</summary>
        private static void AssertNeither(Rig rig, int id, int baseline, string when)
        {
            AssertDrawnBy(rig, Nobody, when);
            Assert.AreEqual(NeitherReason, rig.Presenter.NotDrawingReason, when + ": the reason names the NEITHER");
            Assert.IsFalse(rig.Body.forceRenderingOff, when + ": the figure has given her body back");
            AssertKeptId(rig, id, baseline, when);
        }

        /// <summary>Refused: the WHOLE sprite, the refusal latched and named, a figure holding no id,
        /// nothing lent by the pool, and her body not held off.</summary>
        private static void AssertRefused(Rig rig, int baseline, string when)
        {
            AssertDrawnBy(rig, BodySprite, when);
            Assert.IsTrue(rig.Presenter.AshoreRefused, when + ": the refusal is latched");
            Assert.AreEqual(RefusedReason, rig.Presenter.NotDrawingReason, when + ": the reason names the pool");
            Assert.IsNotNull(rig.Presenter.AshoreFigure, when + ": harness: the figure was built before it asked");
            Assert.AreEqual(0, rig.Presenter.AshoreFigure.FigureId, when + ": it holds no id");
            Assert.AreEqual(baseline, IsoFacetHullRegistry.FigureCount, when + ": the pool lent none");
            Assert.IsFalse(rig.Body.forceRenderingOff, when + ": her body sprite is not held off");
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
            // THE FENCE, as it ships. Ashore the mesh figure draws only behind two gates: the switch
            // (GameConfig.MeshCharacterAshore, code default OFF and silent in the asset) and a facet id
            // of her own from the pool. This rig leaves the switch as it ships. The presenter still
            // exists (MeshCharacter is on and she has a skin), which is exactly what makes the assertion
            // worth making: it is the DRAW that is refused, not the component that is absent — and the
            // refusal takes nothing from the pool and leaves her body as main leaves it.
            yield return null;   // the last fixture's deferred destroys hand their ids back first
            int baseline = IsoFacetHullRegistry.FigureCount;

            Rig rig = NewRig(meshOn: true, CharacterSkinStateMap.Idle);
            rig.Rider.SetMode(ControlMode.OnFoot, null);
            yield return null;
            yield return null;

            DeckRiderMeshPresenter presenter = rig.Presenter;
            Assert.IsNotNull(presenter, "the presenter is added on the switch, not on being aboard");
            Assert.IsFalse(presenter.DrawsInsteadOfSprite, "ashore the mesh does not draw with the switch off");
            Assert.AreEqual(AshoreOffReason, presenter.NotDrawingReason,
                "and the reason must NAME the fence — a plate that came back as the sprite otherwise " +
                "does not say which gate shut");
            Assert.IsNull(presenter.Figure, "nothing is built for a draw that cannot happen");
            Assert.IsNull(presenter.AshoreFigure, "…aboard or ashore");
            Assert.AreEqual(baseline, IsoFacetHullRegistry.FigureCount, "and no facet id is taken");
            Assert.IsFalse(rig.Body.forceRenderingOff, "her body is main's: nothing has forced it off");
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

        // ---- ashore: the hand-over ----------------------------------------------------------------
        //
        // THE BAR at every hand-over A1 names — board, step ashore, the vault, the intro's carried
        // passenger released, a hull re-skinned under her, a region's arrival while ashore, a save loaded
        // ashore: exactly ONE drawer, never both and never neither, save the one NEITHER the rider has
        // always drawn (inside a cab, or at a helm that hides its pilot), which is named and guarded as
        // such. SetMode, Carry and Release all pose SYNCHRONOUSLY, so each guard checks on the call itself
        // AND at the frame boundaries after it: a hand-over that is right by the next frame but wrong on
        // the call is still a frame the player can see. The id baseline is read one frame in, before the
        // rig is built, so the last fixture's deferred destroys have handed their ids back.

        /// <summary>Wait out a clip, holding EVERY frame of it to the sprite: the sprite alone draws her,
        /// it draws the clip's own cell, the reason names the clip, and her ashore id is kept.</summary>
        private static IEnumerator WaitOutTheClip(Rig rig, CharacterClipPlayer clips, int id, int baseline,
                                                  string what)
        {
            float cap = Time.realtimeSinceStartup + VaultCapSeconds;
            int frames = 0;
            yield return null;
            while (clips.IsPlaying)
            {
                Assert.Less(Time.realtimeSinceStartup, cap, what + ": the clip never ended");
                string when = what + ", clip frame " + frames;
                AssertDrawnBy(rig, BodySprite, when);
                Assert.AreSame(rig.BoardCell, rig.Body.sprite, when + ": the sprite draws the CLIP'S cell");
                Assert.AreEqual(ClipReason, rig.Presenter.NotDrawingReason, when + ": the reason names the clip");
                AssertKeptId(rig, id, baseline, when);
                frames++;
                yield return null;
            }
            Assert.Greater(frames, 0, what + ": no frame of the clip was ever seen — the guard watched nothing");
        }

        [UnityTest]
        public IEnumerator WithTheAshoreSwitchOff_AshoreIsMainsFrame_NoFigureNoIdAndTheBodyUntouched()
        {
            // Decision 1, as it ships: MeshCharacter on, MeshCharacterAshore OFF. Ashore she is main's
            // frame at every step — built ashore, on the call that lands her, at each frame after it —
            // and a switch turned off at run time gives her id back and takes her figure away.
            yield return null;
            int baseline = IsoFacetHullRegistry.FigureCount;

            Rig rig = NewRig(meshOn: true, ashoreOn: false,
                             CharacterSkinStateMap.Idle, CharacterSkinStateMap.Walk, CharacterSkinStateMap.Balance);
            yield return null;
            yield return null;
            AssertMainsShore(rig, baseline, "built ashore, the ashore switch off");

            yield return Board(rig);
            AssertDrawnBy(rig, AboardMesh, "aboard with the ashore switch off: the aboard mesh, as on main");

            rig.Rider.SetMode(ControlMode.OnFoot, null);
            AssertMainsShore(rig, baseline, "on the call that lands her, the ashore switch off");
            yield return null;
            AssertMainsShore(rig, baseline, "the frame after landing, the ashore switch off");
            yield return null;
            AssertMainsShore(rig, baseline, "two frames after landing, the ashore switch off");

            // Control: the same rig with the switch turned on DOES draw her mesh ashore, so the guard
            // above measured the switch and not a rig that could never draw ashore at all.
            rig.Config.MeshCharacterAshore = true;
            yield return null;
            AssertDrawnBy(rig, AshoreMesh, "the ashore switch turned on at run time");
            Assert.AreEqual(baseline + 1, IsoFacetHullRegistry.FigureCount, "…and she took one id");

            rig.Config.MeshCharacterAshore = false;
            yield return null;
            AssertMainsShore(rig, baseline, "the ashore switch turned off again: her id back, her figure gone");

            rig.Config.MeshCharacterAshore = true;
            yield return null;
            AssertDrawnBy(rig, AshoreMesh, "and on once more");
            Assert.AreEqual(baseline + 1, IsoFacetHullRegistry.FigureCount, "…with one id, not two");
        }

        [UnityTest]
        public IEnumerator TheAshoreSwitchAlone_DoesNothing_WithoutMeshCharacter()
        {
            // Decision 1's other half: MeshCharacterAshore is live ONLY with MeshCharacter on. Alone it
            // adds no presenter, takes no id and changes no frame, aboard or ashore.
            yield return null;
            int baseline = IsoFacetHullRegistry.FigureCount;

            Rig rig = NewRig(meshOn: false, ashoreOn: true,
                             CharacterSkinStateMap.Idle, CharacterSkinStateMap.Walk, CharacterSkinStateMap.Balance);
            yield return null;
            yield return null;
            Assert.IsNull(rig.Presenter, "MeshCharacter off: no presenter, whatever the ashore switch says");
            AssertDrawnBy(rig, BodySprite, "built ashore");

            yield return Board(rig);
            AssertDrawnBy(rig, RiderSprite, "aboard: the deck-rider sprite, as on main");

            rig.Rider.SetMode(ControlMode.OnFoot, null);
            AssertDrawnBy(rig, BodySprite, "on the call that lands her");
            yield return null;
            AssertDrawnBy(rig, BodySprite, "the frame after landing");
            Assert.IsNull(rig.Presenter, "still no presenter");
            Assert.AreEqual(baseline, IsoFacetHullRegistry.FigureCount, "and no facet id taken");
        }

        [UnityTest]
        public IEnumerator BoardAndLand_OneDrawerOnTheCallAndEveryFrame_AndSheKeepsHerId()
        {
            // A1's first two hand-overs, and decision 2: board and step ashore, each held to one drawer on
            // the call and at every frame after it, with the one id she took ashore kept throughout.
            yield return null;
            int baseline = IsoFacetHullRegistry.FigureCount;

            Rig rig = NewAshoreRig();
            yield return null;
            yield return null;
            AssertDrawnBy(rig, AshoreMesh, "standing ashore, both switches on");
            int id = rig.Presenter.AshoreFigure.FigureId;
            Assert.That(id, Is.InRange(1, FacetIdCeiling - 1), "she holds a facet id of her own");
            Assert.AreEqual(baseline + 1, IsoFacetHullRegistry.FigureCount, "one id, lent to her");
            Assert.IsTrue(rig.Body.forceRenderingOff, "her body sprite is held off while her mesh draws");

            rig.Rider.SetMode(ControlMode.Aboard, rig.BoatRoot.transform);
            AssertDrawnBy(rig, AboardMesh, "on the call that boards her");
            AssertKeptId(rig, id, baseline, "on the call that boards her");
            Assert.IsFalse(rig.Body.forceRenderingOff, "on the call that boards her: her body is given back");
            for (int f = 1; f <= WatchFrames; f++)
            {
                yield return null;
                AssertDrawnBy(rig, AboardMesh, "aboard, frame " + f);
                AssertKeptId(rig, id, baseline, "aboard, frame " + f);
            }

            rig.Rider.SetMode(ControlMode.OnDeck, rig.BoatRoot.transform);
            AssertDrawnBy(rig, AboardMesh, "on the call that stands her on deck");
            yield return null;
            AssertDrawnBy(rig, AboardMesh, "on deck, the frame after");

            rig.Rider.SetMode(ControlMode.OnFoot, null);
            AssertDrawnBy(rig, AshoreMesh, "on the call that lands her");
            AssertKeptId(rig, id, baseline, "on the call that lands her");
            for (int f = 1; f <= WatchFrames; f++)
            {
                yield return null;
                AssertDrawnBy(rig, AshoreMesh, "ashore, frame " + f);
                AssertKeptId(rig, id, baseline, "ashore, frame " + f);
            }
        }

        [UnityTest]
        public IEnumerator ASaveLoadedAshore_SeatsHerOnFootOnTheFirstCall_WithOneDrawer()
        {
            // A1: a save loaded ashore. The loader's first word to the rider is SetMode(OnFoot) on a rig no
            // frame has posed yet, and that one call must add the presenter and hand her to her mesh: there
            // is no earlier frame for a late hand-over to hide in.
            yield return null;
            int baseline = IsoFacetHullRegistry.FigureCount;

            Rig rig = NewAshoreRig();
            rig.Rider.SetMode(ControlMode.OnFoot, null);
            Assert.IsNotNull(rig.Presenter, "the first call adds the presenter");
            AssertDrawnBy(rig, AshoreMesh, "on the loader's first call");
            Assert.AreEqual(baseline + 1, IsoFacetHullRegistry.FigureCount, "one id, lent on that call");
            for (int f = 1; f <= WatchFrames; f++)
            {
                yield return null;
                AssertDrawnBy(rig, AshoreMesh, "loaded ashore, frame " + f);
            }
        }

        [UnityTest]
        public IEnumerator ARegionArrival_ReStatesOnFoot_AndTheSameFigureKeepsTheDraw()
        {
            // A1: a region loaded while she stands ashore. The arrival re-states OnFoot to a rider already
            // on foot; the re-seat is an ARRIVAL to the refusal latch and must be nothing more to the draw —
            // the same figure, the same id, one drawer on the call and after it.
            yield return null;
            int baseline = IsoFacetHullRegistry.FigureCount;

            Rig rig = NewAshoreRig();
            yield return null;
            yield return null;
            AssertDrawnBy(rig, AshoreMesh, "standing ashore");
            IsoCharacterFigureRenderer figure = rig.Presenter.AshoreFigure;
            int id = figure.FigureId;

            rig.Rider.SetMode(ControlMode.OnFoot, null);
            AssertDrawnBy(rig, AshoreMesh, "on the arrival's call");
            Assert.AreSame(figure, rig.Presenter.AshoreFigure, "on the arrival's call: the same figure, not a rebuild");
            AssertKeptId(rig, id, baseline, "on the arrival's call");
            yield return null;
            AssertDrawnBy(rig, AshoreMesh, "the frame after the arrival");
            Assert.AreSame(figure, rig.Presenter.AshoreFigure, "the frame after the arrival: the same figure");
            AssertKeptId(rig, id, baseline, "the frame after the arrival");
        }

        [UnityTest]
        public IEnumerator AClip_HandsTheDrawToTheSpriteForItsLength_AndTheMeshTakesItBackTheFrameItEnds()
        {
            // Decision 4 and A1's vault: any clip hands the draw to the sprite for its whole length — every
            // frame of it the sprite, drawing the CLIP'S cell — and the mesh takes it back on the frame the
            // clip ends. Twice: a clip played where she stands, and the vault's shape, where the landing and
            // the clip happen in one frame and the mesh the landing posed is never rendered.
            yield return null;
            int baseline = IsoFacetHullRegistry.FigureCount;

            Rig rig = NewAshoreRig();
            yield return null;
            yield return null;
            AssertDrawnBy(rig, AshoreMesh, "standing ashore");
            int id = rig.Presenter.AshoreFigure.FigureId;
            CharacterClipPlayer clips = rig.Body.gameObject.AddComponent<CharacterClipPlayer>();

            Assert.IsTrue(clips.Play(CharacterClip.Board, 0f, VaultSeconds),
                          "harness: the clip must play, or the guard below watches nothing");
            yield return WaitOutTheClip(rig, clips, id, baseline, "a clip played ashore");
            AssertDrawnBy(rig, AshoreMesh, "the frame the clip ended: the mesh takes the draw back");
            AssertKeptId(rig, id, baseline, "the frame the clip ended");

            // The vault: the landing and its clip in ONE frame. No assert between the two calls — the frame
            // renders after both, so what the player sees on it is the clip.
            yield return Board(rig);
            AssertDrawnBy(rig, AboardMesh, "aboard, before the vault");
            rig.Rider.SetMode(ControlMode.OnFoot, null);
            Assert.IsTrue(clips.Play(CharacterClip.Board, 0f, VaultSeconds), "harness: the vault's clip must play");
            yield return WaitOutTheClip(rig, clips, id, baseline, "the vault ashore");
            AssertDrawnBy(rig, AshoreMesh, "the frame the vault ended");
            AssertKeptId(rig, id, baseline, "the frame the vault ended");

            rig.Rider.SetMode(ControlMode.OnDeck, rig.BoatRoot.transform);
            AssertDrawnBy(rig, AboardMesh, "on the call that stands her on deck after the vault");
            yield return null;
            AssertDrawnBy(rig, AboardMesh, "on deck, the frame after");
        }

        [UnityTest]
        public IEnumerator TheIntroCarry_BoardsHerOnTheCall_AndTheReleaseLandsHerOnTheCall()
        {
            // A1: the intro's carried passenger. Carry boards her on its FIRST call (a carrier may re-state
            // it every frame; only the first boards), and Release lands her on its call — each a one-drawer
            // hand-over on the call, with her ashore id kept across the ride.
            yield return null;
            int baseline = IsoFacetHullRegistry.FigureCount;

            Rig rig = NewAshoreRig();
            yield return null;
            yield return null;
            AssertDrawnBy(rig, AshoreMesh, "standing ashore before the carry");
            int id = rig.Presenter.AshoreFigure.FigureId;

            rig.Rider.Carry(rig.BoatRoot.transform, Vector3.zero, CharacterStance.Balance, 0f, 0f);
            AssertDrawnBy(rig, AboardMesh, "on the call that carries her aboard");
            AssertKeptId(rig, id, baseline, "on the call that carries her aboard");
            for (int f = 1; f <= WatchFrames; f++)
            {
                yield return null;
                AssertDrawnBy(rig, AboardMesh, "carried, frame " + f);
                AssertKeptId(rig, id, baseline, "carried, frame " + f);
                rig.Rider.Carry(rig.BoatRoot.transform, Vector3.zero, CharacterStance.Balance, 0f, 0f);
            }

            rig.Rider.Release();
            AssertDrawnBy(rig, AshoreMesh, "on the call that releases her");
            AssertKeptId(rig, id, baseline, "on the call that releases her");
            yield return null;
            AssertDrawnBy(rig, AshoreMesh, "the frame after the release");
        }

        [UnityTest]
        public IEnumerator InsideACabOrAHelmThatHidesHerPilot_SheDrawsNeither_ANamedGuard()
        {
            // The ONE neither, and it is main's: driving from inside a cab, or at a helm that hides its
            // pilot, the rider has always drawn nobody. Her ashore figure must not stand in there — she is
            // inside something — so the bar here is NEITHER, named, with her body given back and her id
            // kept for when she steps out.
            yield return null;
            int baseline = IsoFacetHullRegistry.FigureCount;

            Rig rig = NewAshoreRig();
            yield return null;
            yield return null;
            AssertDrawnBy(rig, AshoreMesh, "standing ashore");
            int id = rig.Presenter.AshoreFigure.FigureId;

            rig.Rider.SetMode(ControlMode.Driving, null);
            AssertNeither(rig, id, baseline, "on the call that seats her in a cab");
            yield return null;
            AssertNeither(rig, id, baseline, "in the cab, the frame after");

            rig.Rider.SetMode(ControlMode.OnFoot, null);
            AssertDrawnBy(rig, AshoreMesh, "on the call that steps her out of the cab");
            AssertKeptId(rig, id, baseline, "out of the cab");

            // A helm that hides its pilot: the rider's serialized Pilot switch OFF (the old "taking the
            // helm hides the figure"). It has no runtime setter, so it is set here as the inspector sets it.
            FieldInfo drawPilot = typeof(DeckRiderVisual).GetField("_drawPilot",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(drawPilot, "harness: DeckRiderVisual._drawPilot is the hide-the-pilot switch");
            drawPilot.SetValue(rig.Rider, false);

            rig.Rider.SetMode(ControlMode.Aboard, rig.BoatRoot.transform);
            AssertNeither(rig, id, baseline, "on the call that seats her at a helm that hides its pilot");
            yield return null;
            AssertNeither(rig, id, baseline, "at that helm, the frame after");

            rig.Rider.SetMode(ControlMode.OnFoot, null);
            AssertDrawnBy(rig, AshoreMesh, "on the call that steps her off that helm");
            AssertKeptId(rig, id, baseline, "off that helm");
            yield return null;
            AssertDrawnBy(rig, AshoreMesh, "ashore again, the frame after");
        }

        [UnityTest]
        public IEnumerator APoolAtItsEnd_RefusesHer_SheKeepsHerWholeSprite_AndAsksAgainOnlyOnArrival()
        {
            // Decisions 3 and 7: a pool at its end refuses her; she keeps her WHOLE sprite with one warning
            // for the ask, and asks again at her next ARRIVAL ashore — never per frame, and not merely
            // because an id came free. The pool is a private one driven to its end (the live pool's next id
            // never rewinds), and the live one is restored in the finally.
            yield return null;
            int baseline = IsoFacetHullRegistry.FigureCount;

            Rig rig = NewAshoreRig();
            yield return Board(rig);
            AssertDrawnBy(rig, AboardMesh, "aboard, before the pool is drained");
            Assert.IsNull(rig.Presenter.AshoreFigure, "harness: she has not yet asked the pool for an ashore id");
            Assert.AreEqual(baseline, IsoFacetHullRegistry.FigureCount, "harness: …and holds none");

            var drained = new IsoFacetIdPool();
            for (int i = 1; i < FacetIdCeiling; i++) drained.TakeId();
            IsoFacetIdPool live = IsoFacetHullRegistry.SwapIdPoolForTests(drained);
            int warnings = 0;
            Application.LogCallback count = (message, stack, type) =>
            {
                if (type == LogType.Warning && message.Contains(NoFacetIdWarning)) warnings++;
            };
            Application.logMessageReceived += count;
            try
            {
                rig.Rider.SetMode(ControlMode.OnFoot, null);
                Assert.AreEqual(1, warnings, "the landing asked once, and was told no once");
                AssertRefused(rig, baseline, "on the call that lands her into a pool at its end");

                for (int f = 1; f <= RefusalHoldFrames; f++)
                {
                    yield return null;
                    AssertRefused(rig, baseline, "refused, frame " + f);
                }
                Assert.AreEqual(1, warnings, "…and never again per frame: the refusal is latched");

                rig.Rider.SetMode(ControlMode.OnFoot, null);
                Assert.AreEqual(2, warnings, "an arrival (a region's re-stated OnFoot) asks once more");
                AssertRefused(rig, baseline, "refused again at that arrival");

                drained.FreeId(FreedId);
                yield return null;
                Assert.AreEqual(2, warnings, "an id coming free is not an arrival: she does not ask");
                AssertRefused(rig, baseline, "an id free, but no arrival yet");

                rig.Rider.SetMode(ControlMode.OnFoot, null);
                Assert.AreEqual(2, warnings, "the next arrival asks, and is not refused");
                AssertDrawnBy(rig, AshoreMesh, "on the arrival that finds an id free");
                Assert.AreEqual(FreedId, rig.Presenter.AshoreFigure.FigureId, "she took the id that came free");
                Assert.IsFalse(rig.Presenter.AshoreRefused, "and the refusal is cleared");
                Assert.AreEqual(baseline + 1, IsoFacetHullRegistry.FigureCount, "one id, lent to her");
                yield return null;
                AssertDrawnBy(rig, AshoreMesh, "the frame after");
            }
            finally
            {
                // Deactivate FIRST, so her figure hands its id back to the DRAINED pool it came from, and
                // only then restore the live one: freed into the live pool, that id could be lent twice.
                rig.Body.gameObject.SetActive(false);
                IsoFacetHullRegistry.SwapIdPoolForTests(live);
                Application.logMessageReceived -= count;
            }
        }

        [UnityTest]
        public IEnumerator HerAshoreSort_IsTheBodysSortOfTheSameFrame_NeverLastFrames()
        {
            // A3's same-frame sort: whatever writes her body's order this frame, before the rider's
            // LateUpdate, her overlay and its SortingGroup carry THAT order on the frame it was written —
            // never the frame before. A test writer at execution order 50 moves the body's order every frame
            // and records what the overlay held before its write: the control that proves a match is a copy
            // made after it.
            yield return null;
            int baseline = IsoFacetHullRegistry.FigureCount;

            Rig rig = NewAshoreRig();
            yield return null;
            yield return null;
            AssertDrawnBy(rig, AshoreMesh, "standing ashore");
            Assert.AreEqual(baseline + 1, IsoFacetHullRegistry.FigureCount, "one id, lent to her");
            MeshRenderer overlay = rig.Presenter.AshoreFigure.AshoreOverlay;
            Assert.IsNotNull(overlay, "her ashore figure sorts through an overlay quad");
            SortingGroup group = overlay.GetComponent<SortingGroup>();
            Assert.IsNotNull(group, "…under a SortingGroup of its own");

            LateSortWriter writer = rig.Body.gameObject.AddComponent<LateSortWriter>();
            writer.Target = rig.Body;
            writer.Overlay = overlay;
            yield return null;
            int lastFrames = writer.Frames;
            for (int f = 1; f <= WatchFrames; f++)
            {
                yield return null;
                string when = "frame " + f + " of a moving body order";
                Assert.Greater(writer.Frames, lastFrames, when + ": harness: the writer ran this frame");
                Assert.AreNotEqual(writer.Written, writer.OverlayBeforeWrite,
                    when + ": control: before the write the overlay held another order, so a match is a copy");
                Assert.AreEqual(writer.Written, overlay.sortingOrder, when + ": the overlay carries this frame's order");
                Assert.AreEqual(writer.Written, group.sortingOrder, when + ": …and so does its SortingGroup");
                AssertDrawnBy(rig, AshoreMesh, when);
                lastFrames = writer.Frames;
            }
        }

        [UnityTest]
        public IEnumerator HerAshoreFacing_IsTheSpritesHeading_ThroughTheSkinsAzimuthSign()
        {
            // A3's facing: ashore her mesh faces the sprite's own compass heading, through the bake's
            // MEASURED azimuth sign — both signs, so a guard that passed on one cannot be passing on a yaw
            // nobody applied. Read at the drawn child, not only at the presenter's number.
            yield return null;
            int baseline = IsoFacetHullRegistry.FigureCount;

            Rig rig = NewAshoreRig();
            yield return null;
            yield return null;
            AssertDrawnBy(rig, AshoreMesh, "standing ashore");
            Assert.AreEqual(baseline + 1, IsoFacetHullRegistry.FigureCount, "one id, lent to her");
            Transform facet = rig.Presenter.AshoreFigure.transform.Find("AshoreFrame/FacetFigure");
            Assert.IsNotNull(facet, "harness: ashore the drawn child hangs under the figure's AshoreFrame");

            rig.Character.HoldHeading(HeldHeading);
            rig.Skin.AzimuthCounterClockwise = true;
            yield return null;
            Assert.AreEqual(HeldHeading, rig.Character.HeadingDegrees, Tol, "harness: the sprite faces the held heading");
            Assert.AreEqual(-HeldHeading, rig.Presenter.AshoreYawDegrees, Tol,
                            "a counter-clockwise bake turns her by the NEGATED heading");
            Assert.AreEqual(0f, Mathf.DeltaAngle(-HeldHeading, facet.localEulerAngles.z), YawTolDegrees,
                            "…and that is the yaw the drawn child carries");

            rig.Skin.AzimuthCounterClockwise = false;
            yield return null;
            Assert.AreEqual(HeldHeading, rig.Presenter.AshoreYawDegrees, Tol,
                            "a clockwise bake turns her by the heading as it stands");
            Assert.AreEqual(0f, Mathf.DeltaAngle(HeldHeading, facet.localEulerAngles.z), YawTolDegrees,
                            "…and that is the yaw the drawn child carries");
            AssertDrawnBy(rig, AshoreMesh, "still ashore, still one drawer");
        }

        [UnityTest]
        public IEnumerator WhenSheGoesAway_HerIdGoesBackAndHerBodyIsGivenBack_OnTheCall()
        {
            // Decision 2's other end: she keeps her id for the session — until she goes. Deactivated (a
            // scene unloaded under her, the player object switched off), her id goes back to the pool and
            // her body is hers again ON THE CALL, and her figure is gone by the next frame.
            yield return null;
            int baseline = IsoFacetHullRegistry.FigureCount;

            Rig rig = NewAshoreRig();
            yield return null;
            yield return null;
            AssertDrawnBy(rig, AshoreMesh, "standing ashore");
            Assert.AreEqual(baseline + 1, IsoFacetHullRegistry.FigureCount, "one id, lent to her");

            rig.Body.gameObject.SetActive(false);
            Assert.AreEqual(baseline, IsoFacetHullRegistry.FigureCount, "on the call: her id is back in the pool");
            Assert.IsFalse(rig.Body.forceRenderingOff, "on the call: her body is not left forced off");
            Assert.IsNull(rig.Presenter.AshoreFigure, "on the call: the presenter holds no ashore figure");
            yield return null;
            Assert.IsNull(rig.Body.transform.Find("MeshCharacterAshore"), "the frame after: her figure is gone");
            // Not re-activated here: a re-enabled rider may add a SECOND presenter — a seam question for
            // the report, not a claim this guard makes.
        }

        [UnityTest]
        public IEnumerator ABodyHiddenBySomethingElse_StaysHidden_AndTheMeshDoesNotStandInForIt()
        {
            // A body something else forced off (a cutscene, a future cutaway) is not hers to show or to
            // give back: the mesh does not stand in for it, takes no id for it, and never clears the flag.
            // Once that owner lets go, the mesh takes the draw on the next pose.
            yield return null;
            int baseline = IsoFacetHullRegistry.FigureCount;

            Rig rig = NewAshoreRig();
            rig.Body.forceRenderingOff = true;
            yield return null;
            for (int f = 1; f <= WatchFrames; f++)
            {
                yield return null;
                string when = "hidden by another owner, frame " + f;
                AssertDrawnBy(rig, Nobody, when);
                Assert.IsTrue(rig.Body.forceRenderingOff, when + ": the flag is the other owner's, and stays set");
                Assert.AreEqual(HiddenElsewhereReason, rig.Presenter.NotDrawingReason, when + ": the reason names it");
                Assert.IsNull(rig.Presenter.AshoreFigure, when + ": no figure is built for it");
                Assert.AreEqual(baseline, IsoFacetHullRegistry.FigureCount, when + ": and no id taken");
            }

            rig.Body.forceRenderingOff = false;
            yield return null;
            AssertDrawnBy(rig, AshoreMesh, "the other owner let go");
            Assert.AreEqual(baseline + 1, IsoFacetHullRegistry.FigureCount, "one id, lent to her");
        }

        [UnityTest]
        public IEnumerator AHullReSkinnedUnderHer_NeverDrawsTwo_AndHerAshoreIdIsKept()
        {
            // A1: a hull swapped under her while she is aboard. Her ashore figure stays hidden with its id
            // kept, her body stays hers, and the aboard figure is rebuilt on the new hull. Main's aboard
            // path loses ONE frame here — the old posed mesh dies, with the figure on it, before the swap's
            // frame renders — and that frame is allowed, named and capped; any other is a red.
            yield return null;
            int baseline = IsoFacetHullRegistry.FigureCount;

            Rig rig = NewAshoreRig();
            yield return null;
            yield return null;
            AssertDrawnBy(rig, AshoreMesh, "standing ashore");
            int id = rig.Presenter.AshoreFigure.FigureId;
            yield return Board(rig);
            AssertDrawnBy(rig, AboardMesh, "aboard, before the swap");

            var service = (FacetService)HullMeshPresentation.Service;
            BoatHullSkinner.Apply(rig.BoatRoot, NewMeshHullVisual(), boat: null, SkinOptions);
            int neitherFrames = 0;
            for (int f = 0; f < WatchFrames; f++)
            {
                yield return null;
                string when = "frame " + f + " after the swap";
                if (f < SwapFramesMainMayDrawNeither && WhoDraws(rig) == Nobody) neitherFrames++;
                else AssertDrawnBy(rig, AboardMesh, when);
                Assert.IsFalse(rig.Presenter.AshoreFigure.Visible, when + ": her ashore figure stays hidden aboard");
                Assert.IsFalse(rig.Body.forceRenderingOff, when + ": her body is not held off by the ashore figure");
                AssertKeptId(rig, id, baseline, when);
            }
            Assert.LessOrEqual(neitherFrames, SwapFramesMainMayDrawNeither, "the neither is main's one frame, no more");
            AssertDrawnBy(rig, AboardMesh, "after the swap");
            Assert.AreSame(service.Renderer, rig.Presenter.Hull, "she stands on the hull the swap installed");
        }
    }
}
