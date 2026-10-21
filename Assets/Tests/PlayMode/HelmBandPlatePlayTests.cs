using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.UI;
using HiddenHarbours.World;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// THE HELM DASH BAND, H2 — <b>the plates</b>: the owner accepts them before the merge, because the
    /// greybox band is the helm's look until H3. Phase A's §7 table, in its order: P1 every helm's band at
    /// every screen by day and by night; P1b the Cape at 800p; P2 the camera making room under the
    /// look-ahead, the speed pull-back and the shake; P3 the overlays clear of the band; P4 the tiller's
    /// card with the nav cluster clear of it; P5 the raised cards where Q1 (B) puts them; P6 M.
    ///
    /// <para><b>The stage.</b> The real helm host, the real instrument hosts, the real HUD, the real follow
    /// camera and the committed hulls and <see cref="GameConfig"/>, so the band and the camera are the
    /// owner's tuning. The sea is a greybox grid, 1 m and 5 m, and she is a greybox hull drawn where the
    /// real one would lie (the real hull is parked out of view, at her helm): the plates are of the band
    /// and the framing, not of the water or the boat art.</para>
    ///
    /// <para><b>The screen.</b> A batch editor has one screen, so each plate hands the HUD its own through
    /// <see cref="HudScreen"/> and the camera through <see cref="CameraFollow.ScreenPixelsOverride"/>, and
    /// renders the world into a target that size. An overlay canvas does not draw into a camera's
    /// target, so the helm's canvases are drawn through a camera at the same size (constant pixel size at
    /// the factor UGUI's own scaler would pick there), once over black and once over white; the pair is a
    /// matte, and <c>world·(white − black) + black</c> is the HUD over the world, for any blend that is
    /// affine in what lies under it. Every canvas is put back at teardown.</para>
    ///
    /// <para><b>GPU only:</b> on a Null device every case is SKIPPED, NOT VERIFIED, as every plate is. Each
    /// PNG has a caption beside it (screen, fit, camera, where her hull sits against the band's top). Read
    /// only plates newer than the run's floor file.</para>
    /// </summary>
    public class HelmBandPlatePlayTests
    {
        private const string PlateDir = "helm-band";
        private const string BoatsFolder = "Assets/_Project/Data/Boats";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        private const string Region = "region.test_band_plates";
        private static readonly Rect Bounds = new Rect(0f, 0f, 760f, 520f);
        private static readonly Vector3 Station = new Vector3(400f, 240f, 0f);   // where she lies
        private static readonly Vector3 Parked = new Vector3(120f, 120f, 0f);    // the real hull, out of view
        private const float SettleSeconds = 1.5f;   // the framing tween and the band's ease, then the follow's lag
        private const float UnderWaySeconds = 4f;   // the lead's smoothing and the pull-back's 2 s slew, settled
        private const float TillerCardCover = 0.25f; // the outboard on its clear card: about a third of the rect drawn
        private const float StallSeconds = 10f;     // a stalled clock ends a wait, by the wall clock: a batch
                                                    // frame can be a quarter of a millisecond
        private const int GridHalfMeters = 80;      // wide enough for the skybridge's view at UW, pulled back
        private const float ShaderWaitSeconds = 120f; // the editor's background compiler, before a shot

        private static readonly Vector2Int P720 = new Vector2Int(1280, 720);
        private static readonly Vector2Int P800 = new Vector2Int(1280, 800);
        private static readonly Vector2Int P1080 = new Vector2Int(1920, 1080);
        private static readonly Vector2Int P1440 = new Vector2Int(2560, 1440);
        private static readonly Vector2Int Uw = new Vector2Int(3440, 1440);
        private static readonly Vector2Int P4K = new Vector2Int(3840, 2160);

        private static readonly Color SeaDay = new Color(0.16f, 0.36f, 0.52f);
        private static readonly Color SeaNight = new Color(0.03f, 0.06f, 0.12f);
        private static readonly Color MinorDay = new Color(0.22f, 0.44f, 0.60f);
        private static readonly Color MinorNight = new Color(0.05f, 0.09f, 0.16f);
        private static readonly Color MajorDay = new Color(0.36f, 0.58f, 0.72f);
        private static readonly Color MajorNight = new Color(0.09f, 0.15f, 0.25f);
        private static readonly Color HullDay = new Color(0.92f, 0.90f, 0.84f);
        private static readonly Color HullNight = new Color(0.34f, 0.35f, 0.40f);
        private static readonly Color BowDay = new Color(0.80f, 0.22f, 0.16f);
        private static readonly Color BowNight = new Color(0.36f, 0.10f, 0.08f);

        private static string s_floor;
        private static byte[] s_srgb;

        // ---- fakes ----------------------------------------------------------------------------------

        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation = -4f;   // 4 m of water at mean level: the sounder's digits read it
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level = 0f;   // mean water
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(Vector2.zero, Vector2.zero, Level, SeaState.Calm, 1f, 0f);
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private sealed class StillClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay { get; set; } = 12f;
            public float DayFraction => HourOfDay / 24f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private sealed class FakeBoatProbe : IActiveBoatService
        {
            public BoatKinematics Value = BoatKinematics.None;
            public bool HasActiveBoat => Value.HasBoat;
            public BoatKinematics Sample() => Value;
        }

        /// <summary>Her way: moved in Update, ahead of the follow's LateUpdate, and the sea's grid kept
        /// under her on its own 5 m lattice (so a long run never sails off it).</summary>
        private sealed class Cruise : MonoBehaviour
        {
            public Vector2 Velocity;
            public Transform Grid;

            private void Update()
            {
                if (Velocity != Vector2.zero) transform.position += (Vector3)(Velocity * Time.deltaTime);
                if (Grid == null) return;
                Vector3 p = transform.position;
                Grid.position = new Vector3(Mathf.Round(p.x / 5f) * 5f, Mathf.Round(p.y / 5f) * 5f, 0f);
            }
        }

        /// <summary>A canvas as it was before the plates drew it, to put back exactly.</summary>
        private sealed class CanvasRecord
        {
            public readonly Canvas Canvas;
            public readonly RenderMode Mode;
            public readonly Camera WorldCamera;
            public readonly float PlaneDistance;
            public readonly CanvasScaler Scaler;
            public readonly CanvasScaler.ScaleMode ScaleMode;
            public readonly float ScaleFactor;

            public CanvasRecord(Canvas canvas)
            {
                Canvas = canvas;
                Mode = canvas.renderMode;
                WorldCamera = canvas.worldCamera;
                PlaneDistance = canvas.planeDistance;
                Scaler = canvas.GetComponent<CanvasScaler>();
                if (Scaler != null) { ScaleMode = Scaler.uiScaleMode; ScaleFactor = Scaler.scaleFactor; }
            }

            public void Restore()
            {
                if (Canvas == null) return;
                Canvas.renderMode = Mode;
                Canvas.worldCamera = WorldCamera;
                Canvas.planeDistance = PlaneDistance;
                if (Scaler == null) return;
                Scaler.uiScaleMode = ScaleMode;
                Scaler.scaleFactor = ScaleFactor;
            }
        }

        // ---- state --------------------------------------------------------------------------------------

        private readonly List<Object> _spawned = new();
        private readonly List<CanvasRecord> _drawn = new();
        private readonly List<(SpriteRenderer line, bool major)> _grid = new();
        private FakeSaveService _save;
        private StillClock _clock;
        private FakeBoatProbe _probe;
        private Sprite _sprite;
        private Material _unlit;
        private Camera _worldCam, _uiCam;
        private CameraFollow _follow;
        private RenderTexture _worldRt, _uiRt;
        private Cruise _cruise;
        private SpriteRenderer _hullMarker, _bowMark;
        private BoatController _boat;
        private HelmControlRelay _relay;
        private HelmOverlayHost _host;
        private GameObject _popupHost;
        private BoatHullDef _hull;
        private Vector2Int _screen;
        private bool _night;
        private float _headingDeg;
        private string _shaderNote = "";

        [OneTimeSetUp]
        public void WriteTheFloor()
        {
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            s_floor = Path.Combine(dir, "_floor.txt");
            File.WriteAllText(
                s_floor,
                "Floor for " + nameof(HelmBandPlatePlayTests) + " at " +
                System.DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" +
                "Only plates in this directory NEWER than this file were shot by this run.\n",
                new UTF8Encoding(false));
            Debug.Log($"[{PlateDir}] FLOOR written: {s_floor} — read only plates newer than it " +
                      $"(`find \"{dir}\" -newer \"{s_floor}\" -name '*.png'`).");
        }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            BoatUiWindows.Reset();
            BoatUiWindowInput.ConfigureHudInput(null);
            HelmInstrumentExpansion.Collapse();
            HelmKeyCapture.Reset();
            HelmFootprint.Reset();
            HudScreen.Reset();
            // The plates are evidence, read by eye: a committed hull's own complaints must not stop them.
            LogAssert.ignoreFailingMessages = true;
            _save = new FakeSaveService(SaveMigration.NewGame());
            _clock = new StillClock();
            _probe = new FakeBoatProbe();
            GameServices.TidalTerrain = new FlatTerrain();
            GameServices.Environment = new FlatEnv();
            GameServices.Clock = _clock;
            GameServices.Save = _save;
            GameServices.ActiveBoat = _probe;
            GameServices.CurrentRegionId = Region;
            GameServices.CurrentRegionBounds = Bounds;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (CanvasRecord r in _drawn) r.Restore();   // the hosts outlive the test: put them back
            _drawn.Clear();
            HudScreen.Reset();
            EventBus.Publish(InteractOfferChanged.None);
            BoatUiWindowInput.ConfigureHudInput(null);
            BoatUiWindows.Reset();
            HelmInstrumentExpansion.Collapse();
            HelmKeyCapture.Reset();
            GameServices.Reset();
            if (_worldCam != null) _worldCam.targetTexture = null;
            if (_uiCam != null) _uiCam.targetTexture = null;
            Free(ref _worldRt);
            Free(ref _uiRt);
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            _grid.Clear();
            if (_popupHost != null) { Object.Destroy(_popupHost); _popupHost = null; }
            HelmFootprint.Reset();
            Time.timeScale = 1f;
            LogAssert.ignoreFailingMessages = false;
        }

        // ---- P1 and P1b: the band -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator P1_EveryHelmsBand_AtEveryScreen_ByDayAndByNight()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Stage();
            Vector2Int[] screens = { P720, P1080, P1440, Uw, P4K };
            foreach ((string file, string tag) in new[]
                     {
                         ("CapeIslander", "cape"), ("LobsterBoat", "novi"),
                         ("ConsoleSkiff", "console"), ("SportSkiff", "sport"),
                     })
            {
                BoatHullDef hull = LoadHull(file);
                yield return Board(hull, FullKit(hull));
                foreach (Vector2Int s in screens)
                    yield return DayAndNight("P1", tag, s, "the band, with every instrument its helm can carry");
            }
        }

        [UnityTest]
        public IEnumerator P1b_TheCapesBand_At800p()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Stage();
            BoatHullDef cape = LoadHull("CapeIslander");
            yield return Board(cape, FullKit(cape));
            yield return DayAndNight("P1b", "cape", P800, "the band at 800p (k = 1, as at 1080p and 1440p)");
        }

        // ---- P2: the camera making room -------------------------------------------------------------

        [UnityTest]
        public IEnumerator P2_TheCameraMakesRoom_UnderTheLeadThePullBackAndTheShake()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Stage();
            foreach ((string file, string tag, Vector2Int[] screens) in new[]
                     {
                         ("CapeIslander", "cape", new[] { P720, P1080 }),
                         ("SportFisherSkybridge", "skybridge", new[] { P720 }),
                     })
            {
                BoatHullDef hull = LoadHull(file);
                yield return Board(hull, FullKit(hull));
                foreach (Vector2Int s in screens)
                {
                    yield return SetScreen(s);
                    float speed = LeadTopsOutAt();
                    string at = $"{tag}_{s.x}x{s.y}";

                    Steam(0f, speed);
                    yield return Settle(UnderWaySeconds);
                    yield return Shoot("P2", at + "_lead", BandRect(),
                                       $"the look-ahead at its most northward: under way north at {speed:0.0} m/s, " +
                                       "the lead topped out at its cap (and the pull-back with it)");

                    Steam(90f, speed);
                    yield return Settle(UnderWaySeconds);
                    yield return Shoot("P2", at + "_pullback", BandRect(),
                                       $"the speed pull-back: the same {speed:0.0} m/s steaming east, so the lead " +
                                       "is all sideways and only the zoom and the band move her up or down");

                    Steam(0f, 0f);
                    yield return Settle(UnderWaySeconds);   // the lead and the pull-back ease back out
                    _follow.OnBoatGrounded(new BoatGrounded(_boat, 1f));
                    yield return null;
                    yield return null;
                    yield return Shoot("P2", at + "_shake", BandRect(),
                                       "the shake: stopped, two frames after touching bottom hard (severity 1)");
                }
            }
        }

        // ---- P3: the overlays clear of the band -----------------------------------------------------

        [UnityTest]
        public IEnumerator P3_TheOverlaysStandClearOfTheBand_AtEveryScreen()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Stage();
            BoatHullDef novi = LoadHull("LobsterBoat");
            Assert.AreEqual(CompassMount.None, novi.Helm.DefaultCompass,
                            "premise: the Novi as shipped has no compass, so the HUD's nav cluster stands");
            yield return Board(novi);   // as shipped: nothing bought
            DevToast toast = Spawn("DevToast(band plate)").AddComponent<DevToast>();
            QuestPanelPresenter note = Spawn("QuestNote(band plate)").AddComponent<QuestPanelPresenter>();
            Popup();
            foreach (Vector2Int s in new[] { P720, P800, P1080, P1440, Uw, P4K })
            {
                yield return SetScreen(s);
                yield return Settle(SettleSeconds);
                toast.Show("Sold 14 kg of cod at the wharf");
                note.Show("Bring the dory round to the slip");
                EventBus.Publish(new InteractOfferChanged(InteractOfferSource.Fixture, "plate.offer", "Tie up",
                                                          (Vector2)Station + Vector2.up * 3f, 1f));
                yield return Settle(0.25f);
                yield return Shoot("P3", $"novi_{s.x}x{s.y}_overlays", BandRect(),
                                   "the overlays clear of the band: the dev toast, the interact popup, the quest " +
                                   "note and the nav cluster (the Novi as shipped has no compass of her own)");
            }
        }

        // ---- P4: the tiller's card ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator P4_TheTillersCard_AndTheNavClusterClearOfIt()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Stage();
            BoatHullDef dory = LoadHull("DoryOutboard");
            Assert.IsNull(dory.Helm, "premise: the outboard dory has no console: she steers by her tiller");
            yield return Board(dory);
            foreach (Vector2Int s in new[] { P720, P1080, P4K })
            {
                yield return SetScreen(s);
                yield return Settle(SettleSeconds);
                Assert.That(_host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Tiller), "the tiller's card");
                Rect card = HelmOverlayLayout.CardRect(TillerRigRender.W, TillerRigRender.H,
                                                       GameServices.HelmOverlay, s.x, s.y);
                yield return Shoot("P4", $"dory_{s.x}x{s.y}_tiller", card,
                                   "the tiller's card where it always was, the camera still, the nav cluster clear of it",
                                   atLeast: TillerCardCover);
            }
        }

        // ---- P5: the raised cards (Q1, answer B) ----------------------------------------------------

        [UnityTest]
        public IEnumerator P5_ARaisedCard_StandsWhereQ1Says()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Stage();
            BoatHullDef cape = LoadHull("CapeIslander");
            yield return Board(cape, BoatEquipment.RadarId, BoatEquipment.GpsId);
            Vector2Int[] screens = { P720, P800, P1080, P1440 };

            Assert.AreEqual(SounderKind.Depth, GameServices.HelmControl.Fit.Sounder, "premise: the depth sounder, as shipped");
            yield return RaiseAtEach(HelmInstrumentSlot.Sounder, "sounder", screens);

            InstrumentLocker.Add(_save.Current, cape.Id, BoatEquipment.FishFinderId);
            yield return null;
            yield return null;
            Assert.AreEqual(SounderKind.Fish, GameServices.HelmControl.Fit.Sounder, "premise: the finder took its cutout");
            yield return RaiseAtEach(HelmInstrumentSlot.FishFinder, "finder", screens);
            yield return RaiseAtEach(HelmInstrumentSlot.Radar, "radar", screens);
            yield return RaiseAtEach(HelmInstrumentSlot.Chartplotter, "plotter", screens);

            yield return SetScreen(P1080);
            yield return Settle(SettleSeconds);
            HelmInstrumentExpansion.Click(HelmInstrumentSlot.Chartplotter);
            yield return null;
            yield return null;
            Assert.IsTrue(ChartplotterOverlayHost.Instance.Expanded, "premise: the plotter's card is raised");
            Assert.IsTrue(PressPlotter(ChartplotterOverlayLayout.KeyMax), "MAX takes the press");
            yield return null;
            yield return null;
            Assert.IsTrue(ChartplotterOverlayHost.Instance.MaxFace, "the max face");
            HelmInstrumentExpansion.TryRaisedCard(out Rect max);
            yield return Shoot("P5", "cape_1920x1080_plotter_max", BandRect(),
                               "the plotter's max face, raised: " + Q1(max), max);
            PressPlotter(ChartplotterOverlayLayout.KeyMax);   // CNSL: the console face back for the next test
            HelmInstrumentExpansion.Collapse();
            yield return null;
        }

        // ---- P6: M ------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator P6_M_TakesTheHelmsUiAway_AndTheCameraEasesBack()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Stage();
            Spawn("BoatUiWindowInput(band plate)").AddComponent<BoatUiWindowInput>();
            var held = new HeldHudIntents();
            BoatUiWindowInput.ConfigureHudInput(held);
            BoatHullDef cape = LoadHull("CapeIslander");
            yield return Board(cape, FullKit(cape));
            yield return SetScreen(P1080);
            yield return Settle(SettleSeconds);
            Assert.IsTrue(HelmOverlayHost.BandUp, "premise: the band is up");
            yield return Shoot("P6", "cape_1920x1080_before_m", BandRect(), "before M: the band up, the camera making room");

            yield return PressHideAll(held);
            Assert.IsTrue(BoatUiWindows.AllHidden, "M hides the helm's UI");
            yield return Settle(SettleSeconds);
            Assert.AreEqual(0f, _follow.BandShiftMeters, "the camera eased back, to exactly nothing");
            yield return Shoot("P6", "cape_1920x1080_after_m", Rect.zero,
                               "after M: the helm's UI hidden, the camera eased back to centre her");

            yield return PressHideAll(held);   // a second M: everything home for the next test
        }

        // ---- the sets' steps ----------------------------------------------------------------------------

        private IEnumerator DayAndNight(string set, string tag, Vector2Int s, string what)
        {
            yield return SetScreen(s);
            yield return Settle(SettleSeconds);
            SetNight(false);
            yield return null;
            yield return null;
            Assert.That(_host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Band), $"{tag}: the band");
            Assert.That(_host.BandController.Rig, Is.EqualTo(_hull.Helm.Rig), $"{tag}: its own helm's band");
            yield return Shoot(set, $"{tag}_{s.x}x{s.y}_day", BandRect(), what + ", by day");
            SetNight(true);
            yield return null;
            yield return null;
            Assert.IsTrue(_host.BandController.NightShown, $"{tag}: the panel lights are on");
            yield return Shoot(set, $"{tag}_{s.x}x{s.y}_night", BandRect(), what + ", by night: its panel lights on");
            SetNight(false);
            yield return null;
        }

        private IEnumerator RaiseAtEach(HelmInstrumentSlot slot, string tag, Vector2Int[] screens)
        {
            foreach (Vector2Int s in screens)
            {
                yield return SetScreen(s);
                yield return Settle(SettleSeconds);
                HelmInstrumentExpansion.Click(slot);
                yield return null;   // one frame to raise, one to draw
                yield return null;
                Assert.IsTrue(HelmInstrumentExpansion.IsExpanded(slot), $"{tag} at {s.x}x{s.y}: raised");
                Assert.IsTrue(HelmInstrumentExpansion.TryRaisedCard(out Rect card),
                              $"{tag} at {s.x}x{s.y}: its host drew the card");
                yield return Shoot("P5", $"cape_{s.x}x{s.y}_{tag}_raised", BandRect(),
                                   $"the {tag}'s card, raised: " + Q1(card), card);
                HelmInstrumentExpansion.Collapse();
                yield return null;
                yield return null;
            }
        }

        /// <summary>What Q1's answer (B) says of a raised card on this screen, in words, for the caption.</summary>
        private string Q1(Rect card)
        {
            int bandTop = HelmBandLayout.BandHeightPx(_screen.y);
            float top = _screen.y - HudBandLayout.ReservedTopPx();
            string at = $"card x {card.x:0} y {card.y:0} w {card.width:0} h {card.height:0}; ";
            if (card.yMin >= bandTop - 0.5f) return at + "the band stays clear: a whole scale fits between the bands";
            return at + $"no whole scale fits between the band's top ({bandTop} px) and the top HUD band ({top:0} px), " +
                   $"so the card keeps today's scale and reaches {bandTop - card.yMin:0} px down over the band, centred";
        }

        // ---- the stage ----------------------------------------------------------------------------------

        private IEnumerator Stage()
        {
#if UNITY_EDITOR
            // The owner's tuning, before anything's Awake reads it; never written, never destroyed.
            GameServices.Config = LoadCommitted<GameConfig>(ConfigPath);
#endif
            _sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            _spawned.Add(_sprite);
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Assert.IsNotNull(shader, "premise: an unlit sprite shader (no 2D light stands on this stage)");
            _unlit = new Material(shader);
            _spawned.Add(_unlit);

            // Her, as a greybox hull where the real one would lie, and the sea's grid kept under her.
            GameObject cruiseGo = Spawn("BandPlateCruise");
            cruiseGo.transform.position = Station;
            _cruise = cruiseGo.AddComponent<Cruise>();
            _hullMarker = Quad(cruiseGo.transform, "Hull", 10);
            _bowMark = Quad(cruiseGo.transform, "Bow", 11);
            GameObject grid = Spawn("BandPlateGrid");
            _cruise.Grid = grid.transform;
            for (int i = -GridHalfMeters; i <= GridHalfMeters; i++)
            {
                bool major = i % 5 == 0;
                float t = major ? 0.10f : 0.04f;
                SpriteRenderer v = Quad(grid.transform, "V" + i, major ? 1 : 0);
                v.transform.localPosition = new Vector3(i, 0f, 0f);
                v.transform.localScale = new Vector3(t, 2f * GridHalfMeters, 1f);
                SpriteRenderer h = Quad(grid.transform, "H" + i, major ? 1 : 0);
                h.transform.localPosition = new Vector3(0f, i, 0f);
                h.transform.localScale = new Vector3(2f * GridHalfMeters, t, 1f);
                _grid.Add((v, major));
                _grid.Add((h, major));
            }

            // The player's camera, and the camera the helm's canvases are drawn through.
            GameObject camGo = Spawn("BandPlateCamera");
            camGo.tag = "MainCamera";
            _worldCam = camGo.AddComponent<Camera>();
            _worldCam.orthographic = true;
            _worldCam.orthographicSize = CameraFollow.OrthoSizeForWorldHeight(CameraFollow.DefaultWorldHeightMeters);
            _worldCam.clearFlags = CameraClearFlags.SolidColor;
            camGo.transform.position = new Vector3(Station.x, Station.y, -10f);
            _follow = camGo.AddComponent<CameraFollow>();
            _follow.Target = cruiseGo.transform;

            GameObject uiGo = Spawn("BandPlateUiCamera");
            uiGo.transform.position = new Vector3(100000f, 100000f, -10f);   // nothing of the world out here
            _uiCam = uiGo.AddComponent<Camera>();
            _uiCam.orthographic = true;
            _uiCam.orthographicSize = 5f;
            _uiCam.clearFlags = CameraClearFlags.SolidColor;
            _uiCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _uiCam.cullingMask = ~0;
            _uiCam.depth = 10f;

            // The real hull, at her helm and hers, parked out of view.
            GameObject boatGo = Spawn("BandPlateBoat");
            boatGo.transform.position = Parked;
            _boat = boatGo.AddComponent<BoatController>();   // Awake self-adds HelmControlRelay
            var col = boatGo.GetComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(1.7f, 4.0f);
            _relay = boatGo.GetComponent<HelmControlRelay>();
            GameServices.Helm.SetPilotedHull(_boat);
            GameServices.Helm.SetPlayersBoat(_boat);

            _host = HelmOverlayHost.Instance;
            if (_host == null) _host = Spawn("HelmOverlayHost(band plate)").AddComponent<HelmOverlayHost>();
            Spawn("Hud(band plate)").AddComponent<HudController>();
            ApplyScreen(P1080);   // the targets exist before the first frame
            Steam(40f, 0f);
            SetNight(false);
            yield return null;    // Awake + OnEnable have run
            _relay.DevIgnoreEquipmentGating = false;   // shipped-build gating, not the K cycle
            _follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));   // the camera alone learns
        }

        private IEnumerator Board(BoatHullDef hull, params string[] kit)
        {
            _hull = hull;
            _boat.SetHull(hull);
            foreach (string id in kit) InstrumentLocker.Add(_save.Current, hull.Id, id);
            float beam = 0.35f * hull.LengthMeters;
            _hullMarker.transform.localScale = new Vector3(beam, hull.LengthMeters, 1f);
            _bowMark.transform.localScale = new Vector3(0.6f * beam, 0.6f * beam, 1f);
            _bowMark.transform.localPosition = new Vector3(0f, 0.5f * hull.LengthMeters - 0.3f * beam, 0f);
            TellTheCamera();
            yield return null;
            yield return null;
        }

        /// <summary>The hull's framing, at this screen's aspect (a window's shape sets the "whole vessel"
        /// floor, so a new screen is a new boarding as far as the framing goes).</summary>
        private void TellTheCamera()
            => _follow.OnActiveBoatChanged(new ActiveBoatChanged(_hull.Id, _hull.CameraWorldHeightMeters,
                                                                 _hull.LengthMeters));

        private IEnumerator SetScreen(Vector2Int s)
        {
            if (s == _screen) yield break;
            ApplyScreen(s);
            if (_hull != null) TellTheCamera();
            yield return null;
            yield return null;
        }

        private void ApplyScreen(Vector2Int s)
        {
            _screen = s;
            HudScreen.Override(s);
            _follow.ScreenPixelsOverride = s;
            RenderTexture oldWorld = _worldRt, oldUi = _uiRt;
            _worldRt = NewTarget(s, "BandPlateWorld");
            _uiRt = NewTarget(s, "BandPlateUi");
            _worldCam.targetTexture = _worldRt;
            _worldCam.ResetAspect();
            _uiCam.targetTexture = _uiRt;
            _uiCam.ResetAspect();
            Free(ref oldWorld);
            Free(ref oldUi);
            DrawTheHelmsCanvases();
        }

        private void Steam(float headingDeg, float speedMps)
        {
            _headingDeg = headingDeg;
            float a = headingDeg * Mathf.Deg2Rad;
            var velocity = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * speedMps;
            _cruise.Velocity = velocity;
            _cruise.transform.rotation = Quaternion.Euler(0f, 0f, -headingDeg);
            _probe.Value = new BoatKinematics(true, headingDeg, velocity, speedMps);
        }

        private void SetNight(bool night)
        {
            _night = night;
            _clock.HourOfDay = night ? 22f : 12f;
            _worldCam.backgroundColor = night ? SeaNight : SeaDay;
            foreach ((SpriteRenderer line, bool major) in _grid)
                line.color = major ? (night ? MajorNight : MajorDay) : (night ? MinorNight : MinorDay);
            _hullMarker.color = night ? HullNight : HullDay;
            _bowMark.color = night ? BowNight : BowDay;
        }

        /// <summary>The speed that tops the boat lead out at its cap on this framing, and fills the pull-back.</summary>
        private float LeadTopsOutAt()
        {
            JuiceSettings j = GameServices.Config != null ? GameServices.Config.Juice : JuiceSettings.Default;
            float halfView = 0.5f * _follow.WorldUnitsPerRenderedPixel * CameraFollow.DesignScreenHeightPx;
            float cap = j.BoatLeadMaxViewFraction * halfView;
            float lead = j.BoatLeadSeconds > 0f && cap > 0f ? 1.25f * cap / j.BoatLeadSeconds : 0f;
            return Mathf.Max(lead, j.SpeedPullBackFullMps, 1f);
        }

        // ---- the capture --------------------------------------------------------------------------------

        /// <summary>
        /// Draw the world and the helm's canvases at this plate's screen, matte them, and write the PNG and
        /// its caption. <paramref name="mustCover"/> (screen px, bottom-left) is where the helm's UI must
        /// stand: under <paramref name="atLeast"/> of it drawn (half, unless the subject is a drawing on a
        /// clear ground) means the canvases never reached the plate, and the case fails rather than hand the
        /// owner a plate without its subject.
        /// </summary>
        private IEnumerator Shoot(string set, string name, Rect mustCover, string what, Rect raised = default,
                                  float atLeast = 0.5f)
        {
            if (DrawTheHelmsCanvases() > 0)
            {
                yield return null;   // a canvas that just changed screen lays itself out again
                yield return null;
            }
            Canvas.ForceUpdateCanvases();
            yield return ShadersReady();
            Canvas.ForceUpdateCanvases();
            int w = _screen.x, h = _screen.y;
            Texture2D world = Read(_worldCam, _worldRt, null);
            Texture2D overBlack = Read(_uiCam, _uiRt, new Color(0f, 0f, 0f, 0f));
            Texture2D overWhite = Read(_uiCam, _uiRt, new Color(1f, 1f, 1f, 0f));
            byte[] rgba = new byte[w * h * 4];
            int inside = 0, covered = 0;
            var r = new RectInt(Mathf.FloorToInt(mustCover.xMin), Mathf.FloorToInt(mustCover.yMin),
                                Mathf.CeilToInt(mustCover.width), Mathf.CeilToInt(mustCover.height));
            try
            {
                NativeArray<float> a = world.GetRawTextureData<float>();
                NativeArray<float> b = overBlack.GetRawTextureData<float>();
                NativeArray<float> c = overWhite.GetRawTextureData<float>();
                byte[] enc = Srgb();
                for (int y = 0; y < h; y++)
                {
                    bool row = y >= r.yMin && y < r.yMax;
                    for (int x = 0; x < w; x++)
                    {
                        int f = (y * w + x) * 4;
                        float k0 = c[f] - b[f], k1 = c[f + 1] - b[f + 1], k2 = c[f + 2] - b[f + 2];
                        rgba[f] = Encode(enc, b[f] + a[f] * k0);
                        rgba[f + 1] = Encode(enc, b[f + 1] + a[f + 1] * k1);
                        rgba[f + 2] = Encode(enc, b[f + 2] + a[f + 2] * k2);
                        rgba[f + 3] = 255;
                        if (!row || x < r.xMin || x >= r.xMax) continue;
                        inside++;
                        if (k0 < 0.98f || k1 < 0.98f || k2 < 0.98f) covered++;
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(world);
                Object.DestroyImmediate(overBlack);
                Object.DestroyImmediate(overWhite);
            }

            float coverage = inside > 0 ? covered / (float)inside : 1f;
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"{set}_{name}.png");
            var png = new Texture2D(w, h, TextureFormat.RGBA32, false);
            png.LoadRawTextureData(rgba);
            png.Apply(false);
            File.WriteAllBytes(path, png.EncodeToPNG());
            Object.DestroyImmediate(png);
            string caption = Caption(set, name, what, coverage, mustCover, raised);
            File.WriteAllText(Path.ChangeExtension(path, ".txt"), caption, new UTF8Encoding(false));
            Debug.Log($"[{PlateDir}] plate written: {path} — {what}");

            if (inside > 0 && coverage < atLeast)
                Assert.Fail($"[{PlateDir}] {set}_{name}: the helm's UI covers only {coverage:P1} of the rect it must " +
                            $"stand in ({mustCover}); its canvases never reached the plate. Caption:\n{caption}");
        }

        /// <summary>
        /// A plate is a picture of the shaders, not of their cyan stand-ins: draw both cameras once so every
        /// variant the shot needs has been asked for, then let the editor's background compiler finish. Not
        /// <c>ShaderUtil.allowAsyncCompilation = false</c>: compiling in place hung a batch PlayMode run on
        /// D3D12 at its first capture (as <c>StPetersGroundPlatePlayTests</c> records).
        /// </summary>
        private IEnumerator ShadersReady()
        {
            _shaderNote = "";
#if UNITY_EDITOR
            _worldCam.Render();
            _uiCam.Render();
            float until = Time.realtimeSinceStartup + ShaderWaitSeconds;
            int frames = 0;
            while (ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < until)
            {
                yield return null;
                frames++;
            }
            if (ShaderUtil.anythingCompiling)
            {
                _shaderNote = $"still compiling after {frames} frame(s) ({ShaderWaitSeconds} s): this shot may hold a stand-in";
                Debug.LogWarning($"[{PlateDir}] shaders {_shaderNote}.");
            }
            else if (frames > 0)
            {
                _shaderNote = $"waited {frames} frame(s) for them to compile";
            }
#else
            yield break;
#endif
        }

        private static Texture2D Read(Camera cam, RenderTexture rt, Color? clear)
        {
            if (clear.HasValue) cam.backgroundColor = clear.Value;
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            tex.Apply(false);
            RenderTexture.active = prev;
            return tex;
        }

        /// <summary>Linear to sRGB bytes through a table: 8 million pixels at 4K, three channels each.</summary>
        private static byte[] Srgb()
        {
            if (s_srgb != null) return s_srgb;
            s_srgb = new byte[16384];
            for (int i = 0; i < s_srgb.Length; i++)
                s_srgb[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.LinearToGammaSpace(i / 16383f) * 255f), 0, 255);
            return s_srgb;
        }

        private static byte Encode(byte[] enc, float linear)
            => linear <= 0f ? (byte)0 : linear >= 1f ? (byte)255 : enc[(int)(linear * 16383f + 0.5f)];

        /// <summary>
        /// Draw every canvas of the helm's UI through the plate's UI camera, at this screen: a canvas the
        /// UGUI scaler would scale by the screen gets that scale, fixed, because a camera's target is not
        /// the screen the scaler reads. Returns how many canvases changed.
        /// </summary>
        private int DrawTheHelmsCanvases()
        {
            int changed = 0;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (canvas == null || !canvas.isRootCanvas || !IsTheHelmsUi(canvas))
                    continue;
                CanvasRecord rec = _drawn.FirstOrDefault(d => d.Canvas == canvas);
                if (rec == null)
                {
                    rec = new CanvasRecord(canvas);
                    _drawn.Add(rec);
                }
                if (canvas.renderMode != RenderMode.ScreenSpaceCamera || canvas.worldCamera != _uiCam)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = _uiCam;
                    canvas.planeDistance = 10f;
                    changed++;
                }
                if (rec.Scaler == null || rec.ScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
                float factor = UguiScale(rec.Scaler, _screen);
                if (rec.Scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize
                    && Mathf.Approximately(rec.Scaler.scaleFactor, factor)) continue;
                rec.Scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                rec.Scaler.scaleFactor = factor;
                changed++;
            }
            return changed;
        }

        private static bool IsTheHelmsUi(Canvas canvas)
            => canvas.GetComponentInParent<HelmOverlayHost>(true) != null
               || canvas.GetComponentInParent<SounderOverlayHost>(true) != null
               || canvas.GetComponentInParent<FishFinderOverlayHost>(true) != null
               || canvas.GetComponentInParent<RadarOverlayHost>(true) != null
               || canvas.GetComponentInParent<ChartplotterOverlayHost>(true) != null
               || canvas.GetComponentInParent<HudController>(true) != null
               || canvas.GetComponentInParent<DevToast>(true) != null
               || canvas.GetComponentInParent<QuestPanelPresenter>(true) != null
               || canvas.GetComponentInParent<InteractPopup>(true) != null;

        /// <summary>The factor UGUI's <see cref="CanvasScaler"/> picks on a screen of <paramref name="screen"/>.</summary>
        private static float UguiScale(CanvasScaler scaler, Vector2Int screen)
        {
            Vector2 refRes = scaler.referenceResolution;
            switch (scaler.screenMatchMode)
            {
                case CanvasScaler.ScreenMatchMode.Expand:
                    return Mathf.Min(screen.x / refRes.x, screen.y / refRes.y);
                case CanvasScaler.ScreenMatchMode.Shrink:
                    return Mathf.Max(screen.x / refRes.x, screen.y / refRes.y);
                default:
                    float logW = Mathf.Log(screen.x / refRes.x, 2f), logH = Mathf.Log(screen.y / refRes.y, 2f);
                    return Mathf.Pow(2f, Mathf.Lerp(logW, logH, scaler.matchWidthOrHeight));
            }
        }

        private string Caption(string set, string name, string what, float coverage, Rect mustCover, Rect raised)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            int w = _screen.x, h = _screen.y;
            int k = HelmBandLayout.K(h), bandTop = HelmBandLayout.BandHeightPx(h);
            float ortho = _worldCam.orthographicSize;
            float metersPerPx = 2f * ortho / h;
            HelmFootprintSettings tune = GameServices.Config != null ? GameServices.Config.HelmFootprint
                                                                       : HelmFootprintSettings.Default;
            float targetPx = CameraFollow.BandShiftTargetPx(HelmFootprint.Current, tune.CameraBandShare);
            Vector2 lead = (Vector2)(_worldCam.transform.position - _cruise.transform.position)
                           + new Vector2(0f, _follow.BandShiftMeters);
            HelmFit fit = GameServices.HelmControl != null ? GameServices.HelmControl.Fit : HelmFit.None;
            var sb = new StringBuilder();
            sb.Append(set).Append(' ').Append(name).Append(": ").Append(what).Append('\n');
            sb.Append(string.Format(inv, "screen {0}x{1}, k {2}, the band {3} px tall, the top HUD band {4:0} px\n",
                                    w, h, k, bandTop, HudBandLayout.ReservedTopPx()));
            sb.Append(string.Format(inv, "hull {0} ({1}); the helm's UI: {2}; fit: rig {3}, sounder {4}, compass {5}, " +
                                         "radar {6}, plotter {7}\n",
                                    _hull.Id, _hull.Helm != null ? _hull.Helm.Id : "no console: the tiller",
                                    _host.CardKind, fit.Rig, fit.Sounder, fit.Compass, fit.Radar, fit.Gps));
            sb.Append(string.Format(inv, "clock {0:00.0} h: {1}\n", _clock.HourOfDay,
                                    _night ? "night, the panel lights on" : "day"));
            sb.Append(string.Format(inv, "her way: heading {0:000}, {1:0.0} m/s\n", _headingDeg, _cruise.Velocity.magnitude));
            sb.Append(string.Format(inv, "camera: ortho {0:0.000} (the view {1:0.00} m tall), band shift {2:0.000} m = " +
                                         "{3:0.0} px of the {4:0.0} px asked, lead ({5:0.000}, {6:0.000}) m\n",
                                    ortho, 2f * ortho, _follow.BandShiftMeters, _follow.BandShiftMeters / metersPerPx,
                                    targetPx, lead.x, lead.y));
            float lowest = LowestHullPx();
            sb.Append(string.Format(inv, "her hull: its lowest point {0:0.0} px up the screen, {1:0.0} px {2} the band's top\n",
                                    lowest, Mathf.Abs(lowest - bandTop), lowest >= bandTop ? "above" : "BELOW"));
            sb.Append(string.Format(inv, "the seam says: {0}\n", HelmFootprint.Current.Kind));
            if (_shaderNote.Length > 0) sb.Append("shaders: ").Append(_shaderNote).Append('\n');
            if (raised.width > 0f)
                sb.Append(string.Format(inv, "the raised card: x {0:0} y {1:0} w {2:0} h {3:0}\n",
                                        raised.x, raised.y, raised.width, raised.height));
            if (mustCover.width > 0f)
                sb.Append(string.Format(inv, "the helm's UI over its rect ({0:0},{1:0} {2:0}x{3:0}): {4:0.0}% of its pixels\n",
                                        mustCover.x, mustCover.y, mustCover.width, mustCover.height, 100f * coverage));
            sb.Append("canvases drawn: ").Append(string.Join(", ", _drawn
                .Where(d => d.Canvas != null && d.Canvas.isActiveAndEnabled)
                .Select(d => string.Format(inv, "{0} ({1}, x{2:0.###})", d.Canvas.name, d.Canvas.sortingOrder,
                                           d.Scaler != null ? d.Scaler.scaleFactor : 1f)))).Append('\n');
            return sb.ToString();
        }

        /// <summary>The lowest of her greybox hull's corners, in screen px up from the bottom.</summary>
        private float LowestHullPx()
        {
            Transform t = _hullMarker.transform;
            float lowest = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, 0f);
                lowest = Mathf.Min(lowest, _worldCam.WorldToScreenPoint(t.TransformPoint(corner)).y);
            }
            return lowest;
        }

        private Rect BandRect() => new Rect(0f, 0f, _screen.x, HelmBandLayout.BandHeightPx(_screen.y));

        // ---- helpers --------------------------------------------------------------------------------------

        /// <summary>Every instrument the hull's helm can carry and does not ship with: the band with every
        /// housing named.</summary>
        private static string[] FullKit(BoatHullDef hull)
        {
            var kit = new List<string>();
            HelmConsoleDef helm = hull.Helm;
            if (helm == null) return kit.ToArray();
            if (helm.DefaultSounder == SounderKind.None) kit.Add(BoatEquipment.DepthSounderId);
            if (helm.SupportsRadar && !helm.DefaultRadar) kit.Add(BoatEquipment.RadarId);
            if (helm.SupportsGps && !helm.DefaultGps) kit.Add(BoatEquipment.GpsId);
            if (helm.DefaultCompass == CompassMount.None)
            {
                if (helm.SupportsFlushCompass) kit.Add(BoatEquipment.CompassFlushId);
                else if (helm.SupportsDomeCompass) kit.Add(BoatEquipment.CompassDomeId);
            }
            return kit.ToArray();
        }

        private static bool PressPlotter(int hit)
        {
            ChartplotterSettings cfg = GameServices.Chartplotter;
            IHelmInstruments glass = GameServices.HelmInstruments;
            SaveData save = GameServices.Save.Current;
            ChartplotterPrefs prefs = InstrumentLocker.ChartplotterPrefsFor(
                save, glass.HullId, ChartplotterPrefs.FromDefaults(in cfg));
            var st = new NavRigState((Vector2)Station, true, 40f, 3f, prefs.RangeNM(in cfg),
                                     prefs.HeadUp ? NavRigGeometry.Orient.Head : NavRigGeometry.Orient.North,
                                     false, true, 0f, false);
            return ChartplotterOverlayHost.Instance.Apply(glass, save, GameServices.CurrentRegionId, in prefs, in cfg,
                                                          in st, default, hit);
        }

        /// <summary>One M: pressed for a frame, then released.</summary>
        private static IEnumerator PressHideAll(HeldHudIntents held)
        {
            held.PressHideAll();
            yield return null;
            held.Release();
            yield return null;
        }

        /// <summary>Wait until <paramref name="seconds"/> of the camera's clock have passed.</summary>
        private static IEnumerator Settle(float seconds)
        {
            float waited = 0f;
            float giveUp = Time.realtimeSinceStartup + seconds + StallSeconds;
            while (waited < seconds && Time.realtimeSinceStartup < giveUp)
            {
                yield return null;
                waited += Time.deltaTime;
            }
        }

        private SpriteRenderer Quad(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            sr.sharedMaterial = _unlit;
            sr.sortingOrder = order;
            return sr;
        }

        private static RenderTexture NewTarget(Vector2Int s, string name)
        {
            var rt = new RenderTexture(s.x, s.y, 24, RenderTextureFormat.ARGBHalf) { name = name };
            rt.Create();
            return rt;
        }

        private static void Free(ref RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            Object.Destroy(rt);
            rt = null;
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private void Popup()
        {
            if (InteractPopup.Instance != null) return;
            _popupHost = new GameObject("InteractPopup(band plate)");
            _popupHost.AddComponent<InteractPopup>();
        }

        private static BoatHullDef LoadHull(string file) => LoadCommitted<BoatHullDef>($"{BoatsFolder}/{file}.asset");

        /// <summary>A committed asset, by path. Never added to the spawned list: an asset is not ours to
        /// destroy, and nothing here writes to one.</summary>
        private static T LoadCommitted<T>(string path) where T : ScriptableObject
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"missing {path}");
            return asset;
#else
            Assert.Ignore("Needs the AssetDatabase: the plates read the REAL committed hulls and tuning.");
            return null;
#endif
        }
    }
}
