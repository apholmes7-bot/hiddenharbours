using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE HELM DASH BAND, H1 — <b>the camera answers a full-width band</b> (ADR 0050). While the Core
    /// seam says the helm's UI covers the whole width of the bottom of the screen, the boat's place on
    /// screen eases UP by half the band's height: an offset added with the look-ahead in the follow,
    /// before the feel, the snap and the clamp, turned into metres by the framing on screen that frame,
    /// eased over a tunable time. Zero for a card and whenever nothing is covered.
    ///
    /// <para>Every bar below is a stated number — the ruling (0.5 of the band), the Cape's own asset
    /// (24 m authored, 12.9 m long, 4.2 m/s full ahead), the ladder (rung 2 at 1080p: 64 px a metre), the
    /// audit §2.2's seconds — never read back from the code under test. The driven cases run the REAL
    /// follow on the fixture's clock, with URP 2D's Pixel Perfect Camera on the rig as every scene has
    /// it (reached by type name: this assembly does not reference URP 2D), its assets locked at 32 px a
    /// unit, and the screen pinned through <see cref="CameraFollow.ScreenPixelsOverride"/>.</para>
    /// </summary>
    public class HelmBandCameraTests
    {
        private const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        // ---- the ruling (the handoff's H1, 2026-09-29) ------------------------------------------------
        private const float RuledBandShare = 0.5f;
        /// <summary>The ease: the camera's own framing-tween time, so the band's arrival reads as one
        /// camera move with a framing change.</summary>
        private const float ShippedEaseSeconds = 0.4f;

        // ---- the Cape Islander, as her asset authors her --------------------------------------------
        private const string CapeId = "boat.cape_islander";
        private const float CapeAuthoredHeight = 24f;
        private const float CapeLength = 12.9f;
        private const float CapeFullAheadMps = 4.2f;   // 6300·0.01 / (300·0.01 + 0.2·60)

        // ---- the band ----------------------------------------------------------------------------------
        private const float BandRefPx = 144f;          // × k = max(1, H div 1080)

        private static readonly Vector2Int[] Screens =
        {
            new Vector2Int(1280, 720), new Vector2Int(1280, 800), new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440), new Vector2Int(3440, 1440), new Vector2Int(3840, 2160),
        };

        /// <summary>
        /// The Cape at rest on the pixel-perfect framing: her 24 m asks for the 960×540 reference (rung
        /// 2 at the design screen), which the camera shows at the largest whole zoom each screen allows
        /// — ×1 at 720p and 800p (32 px a metre), ×2 up to 1440 and the ultrawide (64), ×4 at 4K (128,
        /// where the band is 288 and its half 144 px). So half the band is 2.25 m, 2.25 m, then 1.125 m.
        /// </summary>
        private static readonly float[] CapeShiftAtRestMeters = { 2.25f, 2.25f, 1.125f, 1.125f, 1.125f, 1.125f };

        private static readonly Vector2Int Screen1080 = new Vector2Int(1920, 1080);

        private readonly List<Object> _spawned = new List<Object>();

        [SetUp]
        public void SetUp() => ResetAll();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            ResetAll();
        }

        private static void ResetAll()
        {
            GameServices.Config = null;
            GameServices.Environment = null;
            GameServices.CurrentRegionBounds = default;
            GameServices.WorldUnitsPerRenderedPixel = 0f;
            InteractionGate.Reset();
            HelmFootprint.Reset();
        }

        // ===== the dials =============================================================================

        [Test]
        public void TheShippedConfig_MovesTheCameraByHalfTheBand_OverTheCamerasOwnTweenTime()
        {
            HelmFootprintSettings shipped = Shipped().HelmFootprint;
            Assert.AreEqual(RuledBandShare, shipped.CameraBandShare, "the ruled share: half the band");
            Assert.AreEqual(ShippedEaseSeconds, shipped.CameraEaseSeconds);
            Assert.AreEqual(RuledBandShare, HelmFootprintSettings.Default.CameraBandShare,
                "the code default IS the ruling — an unwired scene moves the same way");
            Assert.AreEqual(ShippedEaseSeconds, HelmFootprintSettings.Default.CameraEaseSeconds);
        }

        // ===== the offset, pure ======================================================================

        [Test]
        public void TheOffset_IsHalfTheBand_InMetresByThePixelPerfectFraming_AtEveryScreen()
        {
            // Re-derived: the Cape's framing is her authored 24 m (her 12.9 m hull needs less), which
            // is rung 2's 960×540 reference.
            CameraFollow.ReferenceResolutionForWorldHeight(CapeAuthoredHeight, out int rw, out int rh);
            Assert.AreEqual(960, rw);
            Assert.AreEqual(540, rh);

            for (int i = 0; i < Screens.Length; i++)
            {
                Vector2Int s = Screens[i];
                HelmFootprintArea band = Band(s);
                float px = CameraFollow.BandShiftTargetPx(in band, RuledBandShare);
                Assert.AreEqual(72f * K(s), px, $"{Name(s)}: half the band, in screen px");
                float mpp = CameraFollow.MetersPerScreenPixel(true, s.x, s.y, rw, rh, CameraFollow.AssetsPPU, 12f);
                Assert.AreEqual(CapeShiftAtRestMeters[i], px * mpp, 1e-6f, $"{Name(s)}: in metres at rest");
            }

            // The handoff's number: at 1080p the Cape sits 72 px higher, "about 1.1 m" — 72 / 64.
            Assert.AreEqual(1.125f, 72f / 64f);
        }

        [Test]
        public void UnderWay_TheMetresAreTheFeelsFraming_TheOrthoOverTheScreen()
        {
            // At full ahead the feel pulls the Cape's 24 m back by 0.1469 and hands the view to the
            // ortho (the Pixel Perfect Camera stands down while it does), so a pixel is 2·ortho/H.
            float pullBack = CameraFeel.PullBackTarget(CapeFullAheadMps, 0f, Shipped().Juice);
            Assert.AreEqual(0.1469f, pullBack, 1e-4f, "premise: the shipped pull-back at 4.2 m/s, calm sea");
            float ortho = CameraFollow.OrthoSizeForWorldHeight(CapeAuthoredHeight) * (1f + pullBack);
            float[] want = { 2.7525f, 2.4773f, 1.8350f, 1.3763f, 1.3763f, 1.8350f };
            for (int i = 0; i < Screens.Length; i++)
            {
                Vector2Int s = Screens[i];
                float px = CameraFollow.BandShiftTargetPx(Band(s), RuledBandShare);
                float mpp = CameraFollow.MetersPerScreenPixel(false, s.x, s.y, 0, 0, CameraFollow.AssetsPPU, ortho);
                Assert.AreEqual(want[i], px * mpp, 1e-3f, Name(s));
            }
            Assert.AreEqual(0f, CameraFollow.MetersPerScreenPixel(false, 1920, 0, 0, 0, 32, ortho),
                            "a screen with no height has no pixels to convert");
        }

        [Test]
        public void ACardOrNothing_AsksNothingOfTheCamera()
        {
            var tillerCard = HelmFootprintArea.OfCard(new Rect(900f, 16f, 120f, 262f));
            var hugeCard = HelmFootprintArea.OfCard(new Rect(0f, 0f, 1920f, 600f));
            Assert.AreEqual(0f, CameraFollow.BandShiftTargetPx(HelmFootprintArea.None, RuledBandShare));
            Assert.AreEqual(0f, CameraFollow.BandShiftTargetPx(in tillerCard, RuledBandShare),
                "the ruling moves the camera for the band only");
            Assert.AreEqual(0f, CameraFollow.BandShiftTargetPx(in hugeCard, RuledBandShare),
                "…however much of the screen a card takes: a card is not a band");

            HelmFootprintArea band = HelmFootprintArea.FullWidthBand(144f);
            Assert.AreEqual(0f, CameraFollow.BandShiftTargetPx(in band, 0f), "share 0: the owner's off switch");
            Assert.AreEqual(144f, CameraFollow.BandShiftTargetPx(in band, 1f));
            Assert.AreEqual(144f, CameraFollow.BandShiftTargetPx(in band, 3f), "never more than the band");
        }

        [Test]
        public void TheEase_TakesTheTunableTime_AndArrivesWithoutAKick()
        {
            const float ease = ShippedEaseSeconds, dt = ease / 8f;
            float w = 0f;
            for (int i = 1; i <= 8; i++)
            {
                w = CameraFollow.EaseBandWeight(w, true, dt, ease);
                Assert.AreEqual(i / 8f, w, 1e-6f, $"in, step {i}: linear on its clock");
            }
            Assert.AreEqual(1f, CameraFollow.EaseBandWeight(w, true, dt, ease), "and it stays there");
            for (int i = 7; i >= 0; i--)
            {
                w = CameraFollow.EaseBandWeight(w, false, dt, ease);
                Assert.AreEqual(i / 8f, w, 1e-6f, $"out, back to step {i}");
            }
            Assert.AreEqual(1f, CameraFollow.EaseBandWeight(0f, true, dt, 0f), "an ease of 0 is a cut");
            Assert.AreEqual(0.25f, CameraFollow.EaseBandWeight(0.25f, true, -1f, ease), "time never runs back");

            Assert.AreEqual(0f, CameraFollow.BandShiftPx(0f, 72f), "the clock at 0: exactly nothing");
            Assert.AreEqual(36f, CameraFollow.BandShiftPx(0.5f, 72f), 1e-6f, "halfway: half");
            Assert.AreEqual(72f, CameraFollow.BandShiftPx(1f, 72f), "the end: all of it");
            Assert.Less(CameraFollow.BandShiftPx(0.125f, 72f), 0.125f * 72f,
                "smoothstepped: it leaves slower than a straight line would");
        }

        // ===== through the real follow ===============================================================

        [Test]
        public void ThroughTheRealFollow_ABand_LiftsTheCapeByHalfIt_AtEveryScreen()
        {
            GameServices.Config = Shipped();
            for (int i = 0; i < Screens.Length; i++)
            {
                Vector2Int s = Screens[i];
                HelmFootprint.Reset();
                Transform boat = Spawn("Boat").transform;
                CameraFollow follow = CapeAtTheHelm(Spawn("Cam"), boat, s, pixelPerfect: true);

                HelmFootprint.Publish(Band(s));
                Run(follow, frames: 240, dt: ShippedEaseSeconds / 8f);

                Assert.AreEqual(CapeShiftAtRestMeters[i], follow.BandShiftMeters, 1e-5f,
                    $"{Name(s)}: 72·k px on the framing the screen shows");
                Vector3 d = follow.transform.position - boat.position;
                Assert.AreEqual(0f, d.x, 1e-4f, $"{Name(s)}: straight up the screen");
                Assert.AreEqual(-CapeShiftAtRestMeters[i], d.y, 1e-4f,
                    $"{Name(s)}: the view stands below her, so she sits higher on screen");
            }
        }

        [Test]
        public void ThroughTheRealFollow_TheShiftEasesInAndOut_OverTheTunableTime()
        {
            GameServices.Config = Shipped();
            Transform boat = Spawn("Boat").transform;
            CameraFollow follow = CapeAtTheHelm(Spawn("Cam"), boat, Screen1080, pixelPerfect: true);
            const float dt = ShippedEaseSeconds / 8f;

            HelmFootprint.Publish(Band(Screen1080));
            Run(follow, 4, dt);
            Assert.AreEqual(0.5625f, follow.BandShiftMeters, 1e-6f, "half the ease: half of 1.125 m");
            Run(follow, 4, dt);
            Assert.AreEqual(1.125f, follow.BandShiftMeters, 1e-6f, "the whole ease: all of it");

            HelmFootprint.Clear();
            Run(follow, 4, dt);
            Assert.AreEqual(0.5625f, follow.BandShiftMeters, 1e-6f, "it leaves the way it came");
            Run(follow, 4, dt);
            Assert.AreEqual(0f, follow.BandShiftMeters, "and once it has left, exactly nothing");
            Run(follow, 4, dt);
            Assert.AreEqual(0f, follow.BandShiftMeters);
        }

        [Test]
        public void ThroughTheRealFollow_ACardOrNothing_IsTheFollowThatShipped_BitExact()
        {
            GameServices.Config = Shipped();
            var said = new[]
            {
                HelmFootprintArea.None,
                HelmFootprintArea.OfCard(new Rect(900f, 16f, 120f, 262f)),   // the tiller's, at 1080p
                HelmFootprintArea.OfCard(new Rect(0f, 0f, 1920f, 600f)),
            };
            List<Vector3> reference = null;
            foreach (HelmFootprintArea covered in said)
            {
                HelmFootprint.Reset();
                Transform boat = Spawn("Boat").transform;
                CameraFollow follow = CapeAtTheHelm(Spawn("Cam"), boat, Screen1080, pixelPerfect: true);
                HelmFootprint.Publish(in covered);

                var path = new List<Vector3>();
                for (int f = 1; f <= 180; f++)
                {
                    // Under way and turning: the look-ahead is live, so any leak would show.
                    float t = f / 60f;
                    boat.position = new Vector3(3f * t, 2f * Mathf.Sin(t), 0f);
                    follow.TickFollow(1f / 60f);
                    follow.TickFrame(100.0 + t, 1f / 60f, 0f);
                    Assert.AreEqual(0f, follow.BandShiftMeters, $"{covered}: frame {f}");
                    path.Add(follow.transform.position);
                }

                if (reference == null) { reference = path; continue; }
                for (int f = 0; f < path.Count; f++)
                {
                    Assert.AreEqual(reference[f].x, path[f].x, $"{covered}: frame {f + 1} x");
                    Assert.AreEqual(reference[f].y, path[f].y, $"{covered}: frame {f + 1} y");
                }
            }
        }

        [Test]
        public void ThroughTheRealFollow_TheSnapStillPutsTheShiftedViewOnTheGrid()
        {
            GameServices.Config = Shipped();
            Assert.IsTrue(GameServices.PixelGridSnap, "premise: the shipped snap is on");
            Transform boat = Spawn("Boat").transform;
            boat.position = new Vector3(0.013f, 0.007f, 0f);   // off the grid, so the snap has work
            CameraFollow follow = CapeAtTheHelm(Spawn("Cam"), boat, Screen1080, pixelPerfect: true);
            HelmFootprint.Publish(Band(Screen1080));

            for (int f = 1; f <= 240; f++)
            {
                follow.TickFollow(ShippedEaseSeconds / 8f);
                follow.TickFrame(100.0 + f * 0.05, ShippedEaseSeconds / 8f, 0f);
            }

            float grid = follow.WorldUnitsPerRenderedPixel;
            Assert.AreEqual(1f / 64f, grid, 1e-7f, "premise: rung 2's grid, 64 to the metre");
            Vector3 p = follow.transform.position;
            Assert.AreEqual(Mathf.Round(p.y / grid), p.y / grid, 1e-3f, "the shifted view is on the grid");
            Assert.AreEqual(Mathf.Round(p.x / grid), p.x / grid, 1e-3f);
            Assert.AreEqual(boat.position.y - 1.125f, p.y, 0.5f * grid + 1e-5f,
                "…the grid nearest the shifted goal: the offset went in before the snap");
        }

        [Test]
        public void ThroughTheRealFollow_TheClampStillWins_AtARegionsEdge()
        {
            GameServices.Config = Shipped();
            // A region whose bottom edge is 12.5 m below her: the 24 m view (half 12) may come down to
            // -0.5 and no further — less than the 1.125 m the band asks.
            GameServices.CurrentRegionBounds = new Rect(-500f, -12.5f, 1000f, 1000f);
            Transform boat = Spawn("Boat").transform;
            CameraFollow follow = CapeAtTheHelm(Spawn("Cam"), boat, Screen1080, pixelPerfect: true);
            HelmFootprint.Publish(Band(Screen1080));

            for (int f = 1; f <= 240; f++)
            {
                follow.TickFollow(ShippedEaseSeconds / 8f);
                follow.TickFrame(100.0 + f * 0.05, ShippedEaseSeconds / 8f, 0f);
            }

            Assert.AreEqual(1.125f, follow.BandShiftMeters, 1e-6f, "the band still asks its full shift…");
            Assert.AreEqual(-0.5f, follow.transform.position.y, 1e-6f, "…and the region's edge wins");
        }

        [Test]
        public void ThroughTheRealFollow_UnderWay_TheMetresAreTheFeelsFramingThatFrame()
        {
            GameServices.Config = Shipped();
            Transform boat = Spawn("Boat").transform;
            GameObject go = Spawn("Cam");
            CameraFollow follow = CapeAtTheHelm(go, boat, Screen1080, pixelPerfect: true);
            Camera cam = go.GetComponent<Camera>();
            for (int i = 0; i < 480; i++) follow.TickFeel(1f / 120f, CapeFullAheadMps);
            Assert.IsFalse(PixelPerfect(go).enabled, "premise: the feel has the view, the PPC stood down");

            HelmFootprint.Publish(Band(Screen1080));
            Run(follow, 8, ShippedEaseSeconds / 8f);

            Assert.AreEqual(72f * 2f * cam.orthographicSize / 1080f, follow.BandShiftMeters, 1e-6f,
                "72 px at the ortho on screen this frame");
            Assert.AreEqual(1.835f, follow.BandShiftMeters, 2e-3f, "1.835 m: 72 px of a 27.5 m view");
        }

        [Test]
        public void WithoutAPixelPerfectCamera_AScreenPixelIsTheOrthoOverTheScreen()
        {
            GameServices.Config = Shipped();
            Transform boat = Spawn("Boat").transform;
            CameraFollow follow = CapeAtTheHelm(Spawn("Cam"), boat, Screen1080, pixelPerfect: false);
            HelmFootprint.Publish(Band(Screen1080));
            Run(follow, 8, ShippedEaseSeconds / 8f);
            Assert.AreEqual(72f * CapeAuthoredHeight / 1080f, follow.BandShiftMeters, 1e-5f,
                "her raw 24 m on the ortho: 72 px is 1.6 m");
        }

        // ===== the arithmetic ========================================================================

        [Test]
        public void WithTheOffset_TheClearSeaBelowHer_IsHalfTheScreenLessHalfTheBand()
        {
            foreach (Vector2Int s in Screens)
            {
                HelmFootprintArea band = Band(s);
                float boatOnScreen = s.y * 0.5f + CameraFollow.BandShiftTargetPx(in band, RuledBandShare);
                Assert.AreEqual(s.y * 0.5f - 72f * K(s), boatOnScreen - band.TopPx, Name(s));
            }
        }

        [Test]
        public void TheAuditsStripSeconds_ReDerive_AndTheBandLeavesAtLeastAsMuch()
        {
            // The audit §2.2: the Cape full ahead in a calm sea, heading south (so the lead lifts her
            // above the centre); the warning is the sea between her bow and the top of what covers the
            // bottom, in seconds of her way. The ruled strip stood on the 16 px margin and was 72 tall
            // (top 88); the band is 144 from the edge with the camera's 72 px back.
            GameConfig cfg = Shipped();
            var cape = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/CapeIslander.asset");
            Assert.IsNotNull(cape);
            Assert.AreEqual(CapeAuthoredHeight, cape.CameraWorldHeightMeters, "premise");
            Assert.AreEqual(CapeLength, cape.LengthMeters, "premise");
            float v = TopSpeed(cape);
            Assert.AreEqual(CapeFullAheadMps, v, 1e-4f, "premise: her full ahead");

            float view = CapeAuthoredHeight * (1f + CameraFeel.PullBackTarget(v, 0f, cfg.Juice));
            float cap = cfg.Juice.BoatLeadMaxViewFraction * 0.5f * 16.875f;
            float lead = Mathf.Min(cfg.Juice.BoatLeadSeconds * v, cap);

            float strip720 = WarningSeconds(720, view, lead, CapeLength, 88f, 0f, v);
            float strip1080 = WarningSeconds(1080, view, lead, CapeLength, 88f, 0f, v);
            Assert.AreEqual(2.09f, strip720, 0.005f, "the audit's 2.09 s at 720p, re-derived");
            Assert.AreEqual(2.36f, strip1080, 0.005f, "the audit's 2.36 s at 1080p, re-derived");

            float band720 = WarningSeconds(720, view, lead, CapeLength, 144f, 72f, v);
            float band1080 = WarningSeconds(1080, view, lead, CapeLength, 144f, 72f, v);
            Assert.GreaterOrEqual(band720, strip720, "the band and its shift leave her at least the strip's sea");
            Assert.GreaterOrEqual(band1080, strip1080);
            // Exactly 16 px more: H/2 − 72 against the strip's H/2 − 88.
            Assert.AreEqual(16f / (720f / view) / v, band720 - strip720, 1e-4f);
            Assert.AreEqual(16f / (1080f / view) / v, band1080 - strip1080, 1e-4f);
        }

        // ===== the rig ===============================================================================

        private static GameConfig Shipped()
        {
            var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            Assert.IsNotNull(cfg, $"the shipped config {ConfigAssetPath}");
            return cfg;
        }

        private static int K(Vector2Int s) => Mathf.Max(1, s.y / 1080);

        private static HelmFootprintArea Band(Vector2Int s) => HelmFootprintArea.FullWidthBand(BandRefPx * K(s));

        private static string Name(Vector2Int s) => $"{s.x}x{s.y}";

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private static void Run(CameraFollow follow, int frames, float dt)
        {
            for (int i = 0; i < frames; i++) follow.TickFollow(dt);
        }

        private static Type PixelPerfectCameraType()
            => AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.GetType("UnityEngine.Rendering.Universal.PixelPerfectCamera", false))
                        .FirstOrDefault(t => t != null);

        private static Behaviour PixelPerfect(GameObject go)
            => (Behaviour)go.GetComponent(PixelPerfectCameraType());

        /// <summary>The helm rig as a scene builds it — the camera, URP 2D's Pixel Perfect Camera with
        /// its assets locked at 32 px a unit, the follow — with the Cape taken at the helm on the named
        /// screen. Premises asserted: the helm follows her, and her framing is rung 2.</summary>
        private static CameraFollow CapeAtTheHelm(GameObject go, Transform boat, Vector2Int screen, bool pixelPerfect)
        {
            Camera cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            if (pixelPerfect)
            {
                Type t = PixelPerfectCameraType();
                Assert.IsNotNull(t, "URP 2D's PixelPerfectCamera is loaded in the editor");
                Component ppc = go.AddComponent(t);
                t.GetProperty("assetsPPU").SetValue(ppc, CameraFollow.AssetsPPU);
            }
            cam.aspect = screen.x / (float)screen.y;

            CameraFollow follow = go.AddComponent<CameraFollow>();
            follow.ScreenPixelsOverride = screen;
            follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
            follow.TickZoom(10.0);
            follow.OnActiveBoatChanged(new ActiveBoatChanged(CapeId, CapeAuthoredHeight, CapeLength));
            follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
            follow.TickZoom(100.0);
            follow.Target = boat;

            Assert.IsTrue(follow.BoatLeadIsLive, "premise: the helm follows the boat");
            Assert.AreEqual(16.875f, follow.WorldUnitsPerRenderedPixel * 1080f, 1e-3f,
                "premise: the Cape's 24 m frames on rung 2");
            if (pixelPerfect)
                Assert.IsTrue(PixelPerfect(go).enabled, "premise: at rest the pixel-perfect framing has the view");
            return follow;
        }

        /// <summary>Full ahead, engine only: thrust over the hull's linear resistance, the controller's
        /// own law (mass MassKg/100, the hull's damping, the feel scale).</summary>
        private static float TopSpeed(BoatHullDef hull)
        {
            float thrust = BoatController.EngineThrust(1f, hull.EnginePower, 1f) * BoatController.ForceFeelScale;
            float resistance = SailDrive.LinearResistance(hull.ForwardDrag, Mathf.Max(1f, hull.MassKg / 100f),
                                                          BoatController.HullLinearDamping,
                                                          BoatController.ForceFeelScale)
                               * BoatController.ForceFeelScale;
            return thrust / resistance;
        }

        /// <summary>The audit §2.2's reading, in seconds: heading south at <paramref name="speed"/>, the
        /// lead lifts her <paramref name="leadMeters"/> above the centre (and the band's shift lifts her
        /// <paramref name="shiftPx"/> more); the sea from her bow down to <paramref name="coverTopPx"/>,
        /// over her speed.</summary>
        private static float WarningSeconds(int screenH, float viewMeters, float leadMeters, float hullLength,
                                            float coverTopPx, float shiftPx, float speed)
        {
            float ppm = screenH / viewMeters;
            float boat = screenH * 0.5f + shiftPx + leadMeters * ppm;
            float bow = boat - 0.5f * hullLength * Mathf.Sin(40f * Mathf.Deg2Rad) * ppm;
            return (bow - coverTopPx) / ppm / speed;
        }
    }
}
