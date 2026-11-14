using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE ROD IS DRAWN IN HER HANDS</b> — owner, 2026-11-13: <i>"lets fix the rod sprite, removing the
    /// old floating rod sprite"</i>.
    ///
    /// <para><b>What floated, and why.</b> Aboard, her gameplay root stays in the hull's UNRIDDEN frame
    /// (the level deck point), while the body she is drawn with rides the hull's PICTURE: the mesh figure
    /// stands under the hull's posed mesh, which the tide (<see cref="HullTideRide"/>, applied by
    /// <see cref="BoatWaveMotion"/>) and the rock both move. The carried rod was posed from the gameplay
    /// root, so at any tide but the one the picture was struck at, it hung in the air where her hand
    /// WOULD have been: about 1.5 m above her at a spring low, measured in the co-op trial. The fishing
    /// performance had the same fault one level down (the rider sprite and the fight's rod were both
    /// drawn off the root, not the tide-shifted deck) and a second one of its own: the carried gear
    /// sprite stayed drawn at the hip fallback while the fight drew its own rod in her hands, so she
    /// fished with two.</para>
    ///
    /// <para><b>The claims, in one run of one rig</b> — the SHIPPED dory facet hull and the SHIPPED fisher,
    /// with the real <see cref="BoatWaveMotion"/> (master strength 0: no rock, only the tide, which
    /// survives the master switch by design), the real <see cref="CarryHands"/> holding the real rod
    /// tool, and the real fight animator and presenter wired from the committed rod kit:</para>
    /// <list type="number">
    /// <item><b>Held, aboard, at low water</b>: the rod is drawn at the DRAWN figure's foot plus the
    /// rig's own hand pin, within one art pixel.</item>
    /// <item><b>Fishing, aboard, at low water</b>: her sprite body stands on the deck point the hull is
    /// drawn at; the fight's rod is at that body's grip; and exactly ONE rod is drawn.</item>
    /// <item><b>Ashore</b>: nothing moved — the held rod's pose is exactly the pin, and fishing draws one
    /// rod there too.</item>
    /// </list>
    ///
    /// <para><b>Headless-safe.</b> Every claim is on transforms and renderer state, so CI runs it. The
    /// plates are shot only where a graphics device exists (never <c>ReadPixels</c> on a null device: that
    /// kills the editor with no results file). Failures are COLLECTED and asserted at the very end, after
    /// the plates, so a failing run still leaves the whole picture behind to judge.</para>
    /// </summary>
    public class HeldPropsRideTheDrawnFigurePlayTests
    {
        private const string PlateDir = "held-props-drawn-figure";
        private const string DoryVisualPath = "Assets/_Project/Data/Boats/Visuals/DoryIso.asset";
        private const string FisherVisualPath = "Assets/_Project/Data/Characters/FisherIso.asset";
        private const string CarryAnchorsPath = "Assets/_Project/Data/Characters/FisherCarryAnchors.asset";
        private const string RodToolPath = "Assets/_Project/Data/Tools/Rod.asset";
        private const string RodSpritePath = "Assets/_Project/Art/Sprites/Gear/Rod.png";
        private const string FisherHoldSheetPath = "Assets/_Project/Art/Characters/Iso/Fisher_hold.png";
        private const string RodTier = "cane";

        private const int Seed = 1337;
        private const double ClockOrigin = 1000.0;

        /// <summary>The sea she is tested at, in metres against the level the dory's picture was struck
        /// at: a spring low's worth, and the size of the co-op trial's 1.55 m screen drop.</summary>
        private const float LowWaterMetres = -2f;

        /// <summary>The smallest tide this test will call a tide: a screen drop under this proves nothing
        /// about a fault measured at 1.5 m.</summary>
        private const float MinimumTestedRiseMetres = 1f;

        /// <summary>SE, three-quarters to the camera, so the hand and the rod both read on the plate.</summary>
        private const float HeadingDegrees = 135f;

        /// <summary>Where the fight's facing is aimed (world metres from her): the same SE quarter.</summary>
        private static readonly Vector2 FishingFacingOffset = new Vector2(1.5f, -1.5f);

        /// <summary>The hold cycle is held on frame 0 so two plates of one pose are one picture.</summary>
        private const float HeldHoldFps = 1e-3f;

        /// <summary>Where she stands ashore, beside the hull, so the ashore plate is not drawn over it.</summary>
        private static readonly Vector3 AshoreStep = new Vector3(2.5f, 0f, 0f);

        // ---- the close plate: framed on the DECK POINT the hull is drawn at, which no fix moves ----
        private const int PlatePixels = 320;
        private const float PlateHalfHeightMetres = 1.6f;
        private const float PlateLiftMetres = 0.8f;   // centre on her chest, not her boots

        private readonly List<Object> _spawned = new List<Object>();
        private readonly List<string> _failures = new List<string>();
        private IHullMeshPresentationService _previousService;
        private Camera _cam;
        private RenderTexture _rt;

        [SetUp]
        public void SetUp()
        {
            _previousService = HullMeshPresentation.Service;
            GameServices.Reset();
            _failures.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Publish(new FishingStateChanged(FishingState.Idle));
            HullMeshPresentation.Service = _previousService;
            GameServices.Reset();

            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            for (int i = _spawned.Count - 1; i >= 0; i--)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        // ---- services ------------------------------------------------------------------------------

        private sealed class FrozenClock : IGameClock
        {
            public double TotalSeconds { get; set; } = ClockOrigin;
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

        /// <summary>A sea that stands still at a level the test sets: the tide is a pure function of the
        /// level, so holding it is what makes the before and after the same scene.</summary>
        private sealed class StillSea : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => Seed;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                Vector2.zero, Vector2.zero, tideHeight: Level,
                HiddenHarbours.Core.SeaState.Calm, visibility: 1f, seaState01: 0f);
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        /// <summary>The real facet renderer, without the shipping service's churn, reflector, lamps and
        /// shadow caster (things that advance on engine time). The fittings are DELEGATED, not stubbed:
        /// a refused outboard is logged as an error and removed by the skinner.</summary>
        private sealed class FacetService : IHullMeshPresentationService
        {
            public IsoFacetHullRenderer Renderer { get; private set; }
            private readonly IsoFacetHullPresentationService _fittings = new IsoFacetHullPresentationService();

            public IHullMeshRenderer Install(GameObject host, HullMeshDef def, HullPaintSchemeDef scheme = null)
            {
                if (host == null || def == null || !def.IsUsable()) return null;
                var r = host.GetComponent<IsoFacetHullRenderer>();
                if (r == null) r = host.AddComponent<IsoFacetHullRenderer>();
                r.Configure(IsoFacetHullPresentationService.ToSetup(def, scheme));
                Renderer = r;
                return r;
            }

            public IHullPropRenderer AttachProp(GameObject host, HullPropMeshDef def, string slot)
                => _fittings.AttachProp(host, def, slot);
            public void DetachProps(GameObject host) => _fittings.DetachProps(host);
            public void DetachProp(GameObject host, string slot) => _fittings.DetachProp(host, slot);
            public void Remove(GameObject host) => _fittings.Remove(host);
        }

        private static T LoadCommitted<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            var a = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null)
                Assert.Ignore($"SKIPPED, NOT VERIFIED — {path} is not in this tree. This test holds the " +
                              "SHIPPED rod on the SHIPPED fisher aboard the SHIPPED dory.");
            return a;
#else
            Assert.Ignore("SKIPPED, NOT VERIFIED — needs the editor's asset database.");
            return null;
#endif
        }

        // ---- the rig -------------------------------------------------------------------------------

        private sealed class Rig
        {
            public StillSea Sea;
            public GameObject Boat;
            public IsoFacetHullRenderer Hull;
            public HullTideRide Tide;
            public float PixelMetres;
            public Vector3 Stand;
            public GameObject Player;
            public IsoCharacterSprite Character;
            public DeckRiderVisual Rider;
            public SpriteRenderer RiderSprite;
            public CarryHands Hands;
            public PlayerFishingAnimator Fishing;
            public RodFightPresenter Fight;
            public RodStateVisual[] RodStates;
            public CarriableTool Rod;
            public SpriteRenderer RodSprite;
            public ToolDef RodDef;
            public CarryAnchorTableDef Anchors;
            public BoatVisualDef BoatVisual;
        }

        private Rig Build()
        {
            var rig = new Rig();

            var config = ScriptableObject.CreateInstance<GameConfig>(); _spawned.Add(config);
            config.MeshCharacter = true;          // the shipped switch: she is a mesh aboard a facet hull
            config.MeshCharacterAshore = false;   // ashore is her body sprite: the "nothing moved" control
            GameServices.Config = config;
            GameServices.Clock = new FrozenClock();
            rig.Sea = new StillSea { Level = 0f };
            GameServices.Environment = rig.Sea;

            var service = new FacetService();
            HullMeshPresentation.Service = service;

            var listener = new GameObject("Listener"); _spawned.Add(listener);
            listener.AddComponent<AudioListener>();

            // ---- the dory, with her sea: the real wave motion, rock off, tide on.
            rig.BoatVisual = LoadCommitted<BoatVisualDef>(DoryVisualPath);
            Assert.AreEqual(BoatHullVariant.Mesh, rig.BoatVisual.Variant,
                "harness: the float is a facet-hull fault — this test needs the dory's MESH visual");
            rig.Boat = new GameObject("Dory"); _spawned.Add(rig.Boat);
            BoatHullSkinner.Apply(rig.Boat, rig.BoatVisual, boat: null,
                                  new BoatHullSkinner.Options { SkipOars = true });
            rig.Hull = service.Renderer;
            Assert.IsNotNull(rig.Hull, "harness: the facet hull must have been installed");
            Assert.IsNotNull(rig.Hull.PosedMesh, "harness: the hull must be CONFIGURED");
            rig.PixelMetres = 1f / rig.BoatVisual.HullMesh.PxPerMetre;

            var wave = rig.Boat.GetComponent<BoatWaveMotion>();
            Assert.IsNotNull(wave, "harness: the skinner installs the wave motion that carries the tide");
            wave.MasterStrength = 0f;   // no rock, no engine-time sea: the tide alone moves her picture
            rig.Tide = rig.Boat.GetComponent<HullTideRide>();
            Assert.IsNotNull(rig.Tide, "harness: every hull that floats rides the tide (EnsureTideRide)");

            BoatDeckDef deck = rig.BoatVisual.Deck;
            Assert.IsNotNull(deck, "harness: the dory's visual must carry her deck");
            Assert.IsTrue(deck.HasHelmStation, "harness: the dory's deck publishes a helm station");
            rig.Stand = deck.HelmStationLocalMeters;   // her thwart, on the floor the rig measured

            // ---- the fisher, as the start builder wires her.
            rig.Player = new GameObject("Fisher"); _spawned.Add(rig.Player);
            SpriteRenderer body = rig.Player.AddComponent<SpriteRenderer>();
            body.sortingLayerID = SortingLayer.NameToID("Default");
            body.sortingOrder = rig.BoatVisual.SortingOrder + 1;   // above the hull (see the mesh plate fixture)
            rig.Character = rig.Player.AddComponent<IsoCharacterSprite>();
            var fisher = LoadCommitted<CharacterVisualDef>(FisherVisualPath);
            Assert.IsNotNull(fisher.Skin, "harness: FisherIso must carry her baked skin, or no mesh draws");
            rig.Character.Configure(fisher);

            var riderGo = new GameObject("DeckRider");
            riderGo.transform.SetParent(rig.Player.transform, false);
            rig.RiderSprite = riderGo.AddComponent<SpriteRenderer>();
            rig.RiderSprite.enabled = false;
            rig.Rider = rig.Player.AddComponent<DeckRiderVisual>();
            rig.Rider.Configure(rig.RiderSprite, body, rig.Character);

            rig.Fishing = rig.Player.AddComponent<PlayerFishingAnimator>();
#if UNITY_EDITOR
            Sprite[] hold = HiddenHarbours.App.Editor.PersistentCoreBuilder.LoadIsoDirFramesChecked(FisherHoldSheetPath);
            rig.RodStates = HiddenHarbours.App.Editor.RodKitImporter.BuildRodStates(RodTier, out int[] behindDirs);
#else
            Sprite[] hold = null; int[] behindDirs = null;
#endif
            Assert.IsTrue(hold != null && hold.Length > 0, $"harness: {FisherHoldSheetPath} gave no frames");
            rig.Fishing.ConfigureCastAndHold(hold, null, null, holdFps: HeldHoldFps);
            rig.Fight = rig.Player.AddComponent<RodFightPresenter>();
            rig.Fight.Configure(rig.RodStates, behindDirs, null, null, null, null, 0, null, null);

            rig.Hands = rig.Player.AddComponent<CarryHands>();
            rig.Anchors = LoadCommitted<CarryAnchorTableDef>(CarryAnchorsPath);
            rig.Hands.ConfigureCarryAnchors(rig.Anchors);

            // ---- the rod tool she carries: the shipped Def and its committed gear sprite.
            rig.RodDef = LoadCommitted<ToolDef>(RodToolPath);
            var rodGo = new GameObject("RodTool"); _spawned.Add(rodGo);
            rig.RodSprite = rodGo.AddComponent<SpriteRenderer>();
            rig.RodSprite.sprite = LoadCommitted<Sprite>(RodSpritePath);
            rig.Rod = rodGo.AddComponent<CarriableTool>();
            rig.Rod.Configure(rig.RodDef, rig.RodSprite, "tool.rod.held-props-test");
            Assert.AreEqual(CarryRefusal.None, rig.Hands.TryPickUp(rig.Rod), "harness: she picks the rod up");
            return rig;
        }

        // ---- reads ---------------------------------------------------------------------------------

        /// <summary>The hand the rig measured for the rod, off the body as it is drawn right now —
        /// read off the table directly, never off the hands under test.</summary>
        private static Vector2 RodPin(Rig rig)
        {
            IsoCharacterSprite c = rig.Character;
            Assert.IsTrue(rig.Anchors.TryRow(rig.RodDef.HandPropKey, c.FacingRow, CarryHandSide.None,
                                             out CarryAnchorRow row, out _),
                          $"harness: the anchor table has no '{rig.RodDef.HandPropKey}' row at row {c.FacingRow}");
            return rig.Anchors.PinMeters(row, c.Gait, c.FacingRow, c.Frame);
        }

        /// <summary>Where the hull is DRAWING her stand point this frame: the posed mesh's own map of
        /// it, tide and rock included — the same transform the mesh figure is parented under.</summary>
        private static Vector2 DrawnDeckPoint(Rig rig) => rig.Hull.PosedMesh.TransformPoint(rig.Stand);

        /// <summary>The fight's grip for the pose and cell the animator is showing — the presenter's
        /// own lookup, restated from the wired rod states.</summary>
        private static bool TryFightGrip(Rig rig, out Vector2 grip)
        {
            grip = default;
            string want = RodPresenterMath.RodStateFor(rig.Fishing.Pose);
            foreach (RodStateVisual s in rig.RodStates)
            {
                if (s == null || !string.Equals(s.State, want, System.StringComparison.Ordinal)) continue;
                int idx = RodPresenterMath.SheetIndex(rig.Fishing.Row,
                                                      Mathf.Min(rig.Fishing.Frame, s.FramesPerDir - 1),
                                                      s.FramesPerDir, s.Frames.Length);
                if (idx < 0 || s.GripOffsets == null || idx >= s.GripOffsets.Length) return false;
                grip = s.GripOffsets[idx];
                return true;
            }
            return false;
        }

        private static SpriteRenderer RodOverlay(Rig rig)
        {
            Transform t = rig.Player.transform.Find("RodOverlay");
            return t != null ? t.GetComponent<SpriteRenderer>() : null;
        }

        private static bool Drawn(SpriteRenderer sr)
            => sr != null && sr.enabled && !sr.forceRenderingOff && sr.gameObject.activeInHierarchy
               && sr.sprite != null;

        private void Check(bool ok, string claim, string detail)
        {
            Debug.Log($"[held-props] {(ok ? "PASS" : "FAIL")} {claim} — {detail}");
            if (!ok) _failures.Add($"{claim}: {detail}");
        }

        private static string M(Vector2 v) => $"({v.x:F3}, {v.y:F3})";

        private static void Publish(FishingPhase phase)
            => EventBus.Publish(new FishingStateChanged(new FishingState(
                phase, 0f, 0f, null, null, FishCategory.InshoreGroundfish, 0f,
                0f, false, 0f, FishingFacingOffset.x, FishingFacingOffset.y)));

        private static IEnumerator Settle()
        {
            yield return null;
            yield return null;
            yield return null;
        }

        // ---- the plates ----------------------------------------------------------------------------

        private bool CanShoot => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

        private void Frame(Vector2 centre)
        {
            if (!CanShoot) return;
            if (_cam == null)
            {
                _rt = new RenderTexture(PlatePixels, PlatePixels, 24, RenderTextureFormat.ARGB32)
                { filterMode = FilterMode.Point };
                var camGo = new GameObject("PlateCam"); _spawned.Add(camGo);
                _cam = camGo.AddComponent<Camera>();
                _cam.orthographic = true;
                _cam.orthographicSize = PlateHalfHeightMetres;
                _cam.nearClipPlane = 1f;
                _cam.farClipPlane = 400f;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = new Color(0.10f, 0.12f, 0.14f, 1f);
                _cam.allowHDR = false;
                _cam.allowMSAA = false;
                _cam.targetTexture = _rt;
            }
            _cam.transform.position = new Vector3(centre.x, centre.y + PlateLiftMetres, -100f);
        }

        private void ShootPlate(string name)
        {
            if (!CanShoot || _cam == null) return;
            _cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            // The readback is linear in a linear project; the file is for a human, so encode it.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                Color[] px = tex.GetPixels();
                for (int i = 0; i < px.Length; i++)
                {
                    Color c = px[i];
                    c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b);
                    c.a = 1f;
                    px[i] = c.gamma;
                }
                tex.SetPixels(px);
                tex.Apply();
            }
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[held-props] wrote {path}");
        }

        // ---- the test ------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheRodIsDrawnInHerHands_HeldAndFishing_AboardAtLowWater_AndAshore()
        {
            Rig rig = Build();

            // ---- board her at her thwart, facing SE, standing still.
            rig.Rider.Carry(rig.Boat.transform, rig.Stand, CharacterStance.Balance, HeadingDegrees, 0f);
            yield return Settle();

            var presenter = rig.Player.GetComponent<DeckRiderMeshPresenter>();
            Assert.IsNotNull(presenter, "harness: the switch is on and she is aboard a facet hull");
            Assert.Greater(rig.Hull.HullId, 0,
                "harness: the hull holds facet id 0 (the pool is spent in this domain) — she would be in " +
                "frame and in no picture");
            Assert.IsTrue(presenter.DrawsInsteadOfSprite,
                $"harness: the mesh figure must be drawing her. It says: {presenter.NotDrawingReason}");

            // ---- her gameplay root on the UNRIDDEN deck point: where the drawn foot is at the tide the
            // picture was struck at. That is where the deck walk keeps it ("the deck root resumes its
            // unridden frame"), and it makes the zero-tide arm a statement of agreement.
            Vector2 levelFoot = presenter.PresentationRoot.position;
            rig.Player.transform.position = new Vector3(levelFoot.x, levelFoot.y, 0f);
            yield return Settle();

            Vector2 foot0 = presenter.PresentationRoot.position;
            Vector2 pin0 = RodPin(rig);
            Vector2 rod0 = rig.Rod.transform.position;
            float err0 = Vector2.Distance(rod0, foot0 + pin0);
            Debug.Log($"[held-props] level water: foot {M(foot0)} root {M(rig.Player.transform.position)} " +
                      $"rod {M(rod0)} hand {M(foot0 + pin0)} state '{presenter.DrawnStateKey}' " +
                      $"row {rig.Character.FacingRow} gait {rig.Character.Gait} frame {rig.Character.Frame}");
            Assert.LessOrEqual(err0, rig.PixelMetres,
                $"harness: at the tide her picture was struck at, the rod is already {err0:F3} m off the " +
                "drawn hand — the model of this test is wrong, not the feature");

            // ---- low water. The hull's picture drops; the figure goes with it.
            rig.Sea.Level = LowWaterMetres;
            yield return Settle();

            float rise = rig.Tide.ScreenRiseNow();
            Vector2 foot1 = presenter.PresentationRoot.position;
            Assert.Greater(Mathf.Abs(rise), MinimumTestedRiseMetres,
                $"harness: a {rise:F3} m screen rise is too small to test a 1.5 m float");
            Assert.IsTrue(presenter.DrawsInsteadOfSprite, $"harness: still a mesh. {presenter.NotDrawingReason}");
            Assert.AreEqual(rise, foot1.y - foot0.y, rig.PixelMetres,
                "harness: the drawn figure must ride the tide — that is the premise of the whole float");

            Vector2 pin1 = RodPin(rig);
            Vector2 rod1 = rig.Rod.transform.position;
            float err1 = Vector2.Distance(rod1, foot1 + pin1);
            Debug.Log($"[held-props] low water ({LowWaterMetres} m, screen rise {rise:F3}): foot {M(foot1)} " +
                      $"root {M(rig.Player.transform.position)} rod {M(rod1)} hand {M(foot1 + pin1)} " +
                      $"error {err1:F3} m");
            Check(err1 <= rig.PixelMetres, "HELD, aboard at low water: the rod is in her drawn hand",
                  $"{err1:F3} m off (tolerance one art pixel, {rig.PixelMetres:F4} m)");

            Vector2 deck = DrawnDeckPoint(rig);
            Frame(deck);
            ShootPlate("A-held-aboard-low-water.png");

            // ---- the fishing performance aboard, still at low water: the line is out and she holds.
            Publish(FishingPhase.Waiting);
            yield return Settle();

            Assert.IsTrue(rig.Fishing.OwnsRenderer, "harness: the fight animator must own her body (Hold)");
            Assert.IsFalse(presenter.DrawsInsteadOfSprite,
                "harness: fishing is a SPRITE performance (ADR 0053 D5); the mesh must have handed back");
            Assert.IsTrue(Drawn(rig.RiderSprite), "harness: aboard, the rider sprite draws the performance");

            SpriteRenderer overlay = RodOverlay(rig);
            Assert.IsTrue(Drawn(overlay), "harness: the fight must be drawing its rod while she holds");

            Vector2 deckF = DrawnDeckPoint(rig);
            Vector2 bodyF = rig.RiderSprite.transform.position;
            float bodyErr = Vector2.Distance(bodyF, deckF);
            Check(bodyErr <= rig.PixelMetres, "FISHING, aboard at low water: her body stands on the drawn deck",
                  $"sprite foot {M(bodyF)} vs drawn deck point {M(deckF)}: {bodyErr:F3} m off");

            Assert.IsTrue(TryFightGrip(rig, out Vector2 grip), "harness: the hold rod's grip must resolve");
            Vector2 overlayAt = overlay.transform.position;
            float gripErr = Vector2.Distance(overlayAt, bodyF + grip);
            Check(gripErr <= rig.PixelMetres, "FISHING, aboard at low water: the fight's rod is in her drawn hands",
                  $"rod grip {M(overlayAt)} vs body grip {M(bodyF + grip)}: {gripErr:F3} m off");

            bool twoRodsAboard = Drawn(rig.RodSprite);
            Check(!twoRodsAboard, "FISHING, aboard: ONE rod is drawn",
                  $"the carried gear rod is {(twoRodsAboard ? $"ALSO drawn, at {M(rig.Rod.transform.position)}" : "hidden")} " +
                  $"while the fight draws its own at {M(overlayAt)}");

            Frame(deck);
            ShootPlate("B-fishing-aboard-low-water.png");

            Publish(FishingPhase.Idle);
            yield return Settle();
            Assert.IsFalse(rig.Fishing.OwnsRenderer, "harness: the performance must have ended");
            Check(Drawn(rig.RodSprite), "FISHING OVER, aboard: the carried rod is drawn again",
                  $"rod renderer enabled={rig.RodSprite.enabled} forceRenderingOff={rig.RodSprite.forceRenderingOff}");

            // ---- ashore: the rider stands down and nothing about the held pose may have moved.
            rig.Rider.Release();
            rig.Player.transform.position = new Vector3(foot0.x, foot0.y, 0f) + AshoreStep;
            yield return Settle();

            Vector2 pinA = RodPin(rig);
            Vector2 localA = rig.Rod.transform.localPosition;
            float errA = Vector2.Distance(localA, pinA);
            Check(errA <= 1e-4f, "HELD, ashore: the pose is exactly the rig's pin (unchanged)",
                  $"local {M(localA)} vs pin {M(pinA)}: {errA:F5} m");
            Check(rig.Rod.transform.localPosition.z == 0f, "HELD, ashore: no depth leaks into the prop",
                  $"local z {rig.Rod.transform.localPosition.z}");
            Vector2 root = rig.Player.transform.position;
            Frame(root);
            ShootPlate("C-held-ashore.png");

            Publish(FishingPhase.Waiting);
            yield return Settle();
            overlay = RodOverlay(rig);
            Assert.IsTrue(Drawn(overlay), "harness: ashore, the fight draws its rod while she holds");
            Assert.IsTrue(TryFightGrip(rig, out Vector2 gripA), "harness: the hold rod's grip must resolve ashore");
            float gripErrA = Vector2.Distance(overlay.transform.position, root + gripA);
            Check(gripErrA <= rig.PixelMetres, "FISHING, ashore: the fight's rod is at her grip (unchanged)",
                  $"{gripErrA:F4} m off");
            bool twoRodsAshore = Drawn(rig.RodSprite);
            Check(!twoRodsAshore, "FISHING, ashore: ONE rod is drawn",
                  $"the carried gear rod is {(twoRodsAshore ? $"ALSO drawn, at {M(rig.Rod.transform.position)}" : "hidden")}");
            ShootPlate("D-fishing-ashore.png");

            Publish(FishingPhase.Idle);
            yield return Settle();
            Check(Drawn(rig.RodSprite), "FISHING OVER, ashore: the carried rod is drawn again",
                  $"rod renderer enabled={rig.RodSprite.enabled} forceRenderingOff={rig.RodSprite.forceRenderingOff}");

            if (_failures.Count > 0)
            {
                var sb = new StringBuilder($"{_failures.Count} claim(s) failed:");
                foreach (string f in _failures) sb.Append("\n  - ").Append(f);
                Assert.Fail(sb.ToString());
            }
        }
    }
}
