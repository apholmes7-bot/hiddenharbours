using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The radar's GLASS (ADR 0025 S5): the scope's polar maths, the coastline scan, and that the
    /// picture actually responds to every control that could silently do nothing.
    ///
    /// <para><b>Why the differential assertions are the load-bearing ones (the #421 lesson).</b> Night,
    /// orientation, range, transmit and the clutter dials are all parameters that compile, pass, and can
    /// be quietly ignored — the helm's night panel shipped dead TWICE that way. So each is tested by
    /// proving the PICTURE CHANGES, with a control proving the probe can also register "no change".</para>
    ///
    /// <para>Pure <see cref="DrawSurface"/> and a fake terrain — no scene, no canvas, no texture, so
    /// this runs headless.</para>
    /// </summary>
    public class RadarGlassTests
    {
        private static readonly Vector2 Boat = new Vector2(360f, 300f);

        /// <summary>A shelving coast: land in the west, deepening east, plus a bar that dries at low
        /// water and covers on the flood — the shape the tide ruling is asserted against.</summary>
        private sealed class FakeCoast : ITidalTerrain
        {
            public float ElevationAt(Vector2 p)
            {
                float shore = Mathf.Lerp(6f, -22f, Mathf.InverseLerp(60f, 620f, p.x));
                // A low bar east of the boat, crest +0.4 m above datum: bare at low water, covered at 1 m.
                float bar = 6f * Mathf.Exp(-Mathf.Pow((p.x - 500f) / 30f, 2f)
                                           - Mathf.Pow((p.y - 300f) / 25f, 2f));
                return shore + bar;
            }
        }

        private static RadarRigState State(float rangeNM = 0.2f, bool headUp = true, bool night = false,
                                           bool tx = true, float heading = 62f, float sea = 0.2f,
                                           float sweep = 90f)
            => new RadarRigState(rangeNM, 4, headUp, heading, 0.72f, sea, 0f, tx, true, night, sweep, false);

        private static DrawSurface Paint(in RadarRigState st, IReadOnlyList<RadarBlip> blips)
        {
            var s = new DrawSurface(RadarRigRender.W, RadarRigRender.H);
            RadarRigRender.RenderStandalone(s, in st, blips);
            return s;
        }

        private static List<RadarBlip> Scene() => new()
        {
            new RadarBlip(45f, 0.10f, 1.05f, RadarEchoKind.Vessel, 200f, 0.8f),
            new RadarBlip(310f, 0.16f, 2.2f, RadarEchoKind.Land, float.NaN, 0f),
            new RadarBlip(120f, 0.06f, 0.7f, RadarEchoKind.Buoy, float.NaN, 0f),
        };

        private static int Differ(DrawSurface a, DrawSurface b)
        {
            int n = 0;
            for (int i = 0; i < a.Pixels.Length; i++)
            {
                Color32 p = a.Pixels[i], q = b.Pixels[i];
                if (p.r != q.r || p.g != q.g || p.b != q.b || p.a != q.a) n++;
            }
            return n;
        }

        // ---- the layout is the rig's, and it is also the hit geometry --------------------------------

        [Test]
        public void Layout_PutsTheScopeInsideTheGlass_AndTheThreePushersInTheColumn()
        {
            RadarRigLayout L = RadarRigGeometry.StandaloneLayout();

            Assert.Greater(L.ScopeR, 0, "a scope with no radius would draw nothing at all");
            Assert.GreaterOrEqual(L.ScopeCx - L.ScopeR, L.Lcd.X, "the scope must not spill off the glass");
            Assert.LessOrEqual(L.ScopeCx + L.ScopeR, L.Lcd.X + L.Lcd.W);
            Assert.GreaterOrEqual(L.ScopeCy - L.ScopeR, L.Status.Y + L.Status.H - 3,
                                  "the scope sits BELOW the status strip (radarRig.js:179)");

            for (int i = 0; i < 3; i++)
            {
                RigRect b = L.Button(i);
                Assert.AreEqual(L.Col.X, b.X, $"pusher {i} is in the side column");
                Assert.AreEqual(L.Col.W, b.W);
                Assert.GreaterOrEqual(b.Y, L.Col.Y);
                Assert.LessOrEqual(b.Y + b.H, L.Col.Y + L.Col.H, $"pusher {i} stays inside the column");
            }
            Assert.Less(L.Button0.Y + L.Button0.H, L.Button1.Y + 1, "the pushers do not overlap");
            Assert.Less(L.Button1.Y + L.Button1.H, L.Button2.Y + 1);
        }

        [Test]
        public void Layout_IsTheSonarsFootprintExactly_SoABrowCarriesEitherAtOneHeight()
        {
            // radarRig.js:14 — "matches the sonar footprint exactly". If this ever drifts, the two
            // instruments would mount at different heights in the same brow.
            Assert.AreEqual(FishRigGeometry.W, RadarRigGeometry.W);
            Assert.AreEqual(FishRigGeometry.H, RadarRigGeometry.H);
        }

        // ---- ONE bearing convention ------------------------------------------------------------------

        [Test]
        public void NorthUp_PutsNorthAtTheTopAndEastToTheRight()
        {
            RadarRigLayout L = RadarRigGeometry.StandaloneLayout();

            RadarRigGeometry.BlipXY(L.ScopeCx, L.ScopeCy, L.ScopeR, 0f, 1f, headUp: false, 33f,
                                    out double nx, out double ny, out _, out _);
            Assert.AreEqual(L.ScopeCx, nx, 1.0, "000° is straight up the glass");
            Assert.AreEqual(L.ScopeCy - L.ScopeR, ny, 1.0);

            RadarRigGeometry.BlipXY(L.ScopeCx, L.ScopeCy, L.ScopeR, 90f, 1f, headUp: false, 33f,
                                    out double ex, out double ey, out _, out _);
            Assert.AreEqual(L.ScopeCx + L.ScopeR, ex, 1.0, "090° is to the right — the compass convention");
            Assert.AreEqual(L.ScopeCy, ey, 1.0);
        }

        [Test]
        public void HeadUp_PutsTheBowAtTheTopWhateverTheHeadingIs()
        {
            RadarRigLayout L = RadarRigGeometry.StandaloneLayout();
            foreach (float heading in new[] { 0f, 47f, 180f, 300f })
            {
                // A contact dead ahead is at the ship's own bearing.
                RadarRigGeometry.BlipXY(L.ScopeCx, L.ScopeCy, L.ScopeR, heading, 1f, headUp: true,
                                        heading, out double x, out double y, out _, out _);
                Assert.AreEqual(L.ScopeCx, x, 1.0, $"dead ahead is straight up at heading {heading}");
                Assert.AreEqual(L.ScopeCy - L.ScopeR, y, 1.0);
            }
        }

        [Test]
        public void PickAt_IsTheExactInverseOfBlipXY_WhichIsWhatOneConventionMeans()
        {
            // The strongest statement this port can make: the drawing and the read agree, in BOTH
            // orientations. A second, silently-flipped bearing would fail here immediately.
            RadarRigLayout L = RadarRigGeometry.StandaloneLayout();
            foreach (bool headUp in new[] { false, true })
                foreach (float bearing in new[] { 0f, 37f, 118f, 214f, 359f })
                    foreach (float frac in new[] { 0.25f, 0.6f, 0.95f })
                    {
                        RadarRigGeometry.BlipXY(L.ScopeCx, L.ScopeCy, L.ScopeR, bearing, frac, headUp,
                                                71f, out double x, out double y, out _, out bool vis);
                        Assert.IsTrue(vis);
                        RadarRigGeometry.PickAt(L.ScopeCx, L.ScopeCy, L.ScopeR, x, y, headUp, 71f,
                                                out float back, out float backFrac, out bool inside);
                        Assert.IsTrue(inside);
                        float delta = Mathf.Abs(Mathf.DeltaAngle(bearing, back));
                        Assert.Less(delta, 1.0f,
                                    $"bearing {bearing} head-up={headUp} round-tripped to {back}");
                        Assert.AreEqual(frac, backFrac, 0.02f);
                    }
        }

        // ---- the rig's own formatting tables ---------------------------------------------------------

        [Test]
        public void Formatting_MatchesTheRigsOwnTables()
        {
            Assert.AreEqual("12", RadarRigGeometry.FormatNM(12f), "js:132 — whole miles at ten and up");
            Assert.AreEqual("3.0", RadarRigGeometry.FormatNM(3f), "…one decimal from a mile up");

            // ⚠ NOT the rig's eighths. Its ladder is [0.5,1,2,…] — every rung a whole number of eighths;
            // ours is re-scaled for a harbour and none of it is. Eighths would print 0.20 NM as "2/8"
            // (0.25) and the closest rung, 0.05, as "0/8". The plotter's own sub-1 rule instead, so the
            // two instruments on one brow read the same.
            Assert.AreEqual("0.20", RadarRigGeometry.FormatNM(0.2f), "the harbour ladder needs decimals");
            Assert.AreEqual("0.05", RadarRigGeometry.FormatNM(0.05f), "…and the closest rung is not '0/8'");
            Assert.AreEqual("0.40", RadarRigGeometry.FormatNM(0.4f));
            foreach (int step in new[] { 0, 1, 2, 3, 4, 5 })
            {
                string label = RadarRigGeometry.FormatNM(RadarSettings.Default.RangeNMAt(step));
                Assert.AreNotEqual("0", label, $"rung {step} must never label as nothing");
                Assert.IsFalse(label.Contains("/"), $"rung {step} must not print a lossy fraction");
            }

            Assert.AreEqual("007", RadarRigGeometry.FormatDeg(7f));
            Assert.AreEqual("070", RadarRigGeometry.FormatDeg(70f));
            Assert.AreEqual("359", RadarRigGeometry.FormatDeg(359f));
            Assert.AreEqual("000", RadarRigGeometry.FormatDeg(359.7f),
                            "359.7 rounds to 360, and a compass has no 360");
            Assert.AreEqual("359", RadarRigGeometry.FormatDeg(-1f), "−1° IS 359°, not north");

            Assert.AreEqual("N", RadarRigGeometry.Cardinal8(2f));
            Assert.AreEqual("NE", RadarRigGeometry.Cardinal8(45f));
            Assert.AreEqual("NW", RadarRigGeometry.Cardinal8(316f));
            Assert.AreEqual("N", RadarRigGeometry.Cardinal8(359f), "…and it wraps back to north");
        }

        [Test]
        public void SweepBrightness_IsFlatInStandby_AndPeaksBehindTheSweepWhenTransmitting()
        {
            Assert.AreEqual(0.5f, RadarRigGeometry.SweepBrightness(0f, 0f, transmitting: false), 1e-5f);
            Assert.AreEqual(0.5f, RadarRigGeometry.SweepBrightness(180f, 0f, transmitting: false), 1e-5f,
                            "a set that is not radiating refreshes nothing, so nothing is brighter");

            float justPassed = RadarRigGeometry.SweepBrightness(90f, 90f, transmitting: true);
            float longAgo = RadarRigGeometry.SweepBrightness(95f, 90f, transmitting: true);
            Assert.Greater(justPassed, longAgo, "the phosphor decays behind the sweep");
            Assert.AreEqual(1f, justPassed, 1e-4f);
            Assert.GreaterOrEqual(longAgo, 0.38f, "…but never all the way to nothing (js:230's floor)");
        }

        [Test]
        public void InGuard_HandlesASectorThatWrapsThroughNorth()
        {
            // 350° → 020° is a legal sector and the naive comparison gets it exactly backwards.
            Assert.IsTrue(RadarRigGeometry.InGuard(355f, 0.7f, 350f, 20f, 0.55f, 0.85f));
            Assert.IsTrue(RadarRigGeometry.InGuard(10f, 0.7f, 350f, 20f, 0.55f, 0.85f));
            Assert.IsFalse(RadarRigGeometry.InGuard(180f, 0.7f, 350f, 20f, 0.55f, 0.85f));
            Assert.IsFalse(RadarRigGeometry.InGuard(10f, 0.2f, 350f, 20f, 0.55f, 0.85f),
                           "inside the sector but inside the near edge");
        }

        // ---- the picture responds to every control ---------------------------------------------------

        [Test]
        public void Paints_SomethingAtAll_AndTheSamePictureTwice()
        {
            var blips = Scene();
            DrawSurface a = Paint(State(), blips);

            int opaque = 0;
            for (int i = 0; i < a.Pixels.Length; i++) if (a.Pixels[i].a > 0) opaque++;
            Assert.Greater(opaque, a.Pixels.Length / 4, "the instrument body should cover the sprite");

            DrawSurface b = Paint(State(), blips);
            Assert.AreEqual(0, Differ(a, b),
                            "the same state must paint the same pixels — the stipple uses the rig's own " +
                            "hash precisely so the clutter cannot crawl between repaints");
        }

        [Test]
        public void Night_Orientation_Range_Transmit_AndSeaClutter_EachChangeThePicture()
        {
            var blips = Scene();
            DrawSurface baseline = Paint(State(), blips);

            Assert.Greater(Differ(baseline, Paint(State(night: true), blips)), 500,
                           "NIGHT must reach the raster — the flag that shipped dead twice");
            Assert.Greater(Differ(baseline, Paint(State(headUp: false), blips)), 200,
                           "ORIENTATION must reach the raster");
            Assert.Greater(Differ(baseline, Paint(State(rangeNM: 0.4f), blips)), 100,
                           "RANGE must reach the raster");
            Assert.Greater(Differ(baseline, Paint(State(tx: false), blips)), 500,
                           "STANDBY vs TX must reach the raster — the flag this whole slice turns on");
            Assert.Greater(Differ(baseline, Paint(State(sea: 0.9f), blips)), 100,
                           "SEA CLUTTER must reach the raster — the weather's only mark on the glass");

            // The negative control: the probe can also report "no change".
            Assert.AreEqual(0, Differ(baseline, Paint(State(), blips)),
                            "…and an unchanged state must differ in NOTHING, or the checks above are noise");
        }

        [Test]
        public void TheSweepTurns_AndAContactAppearsWhereTheGeometrySaysItWill()
        {
            var blips = Scene();
            Assert.Greater(Differ(Paint(State(sweep: 0f), blips), Paint(State(sweep: 140f), blips)), 200,
                           "the aerial turning must change the picture, or the scope is a still life");

            // A vessel dead ahead at half range, head-up: it must land above own ship, on the centre line.
            var one = new List<RadarBlip>
            {
                new RadarBlip(62f, 0.1f, 1.05f, RadarEchoKind.Vessel, 62f, 0.8f),
            };
            DrawSurface with = Paint(State(rangeNM: 0.2f, headUp: true, heading: 62f, sea: 0f), one);
            DrawSurface without = Paint(State(rangeNM: 0.2f, headUp: true, heading: 62f, sea: 0f),
                                        new List<RadarBlip>());

            RadarRigLayout L = RadarRigGeometry.StandaloneLayout();
            int expectY = L.ScopeCy - Mathf.RoundToInt(L.ScopeR * 0.5f);
            int changedNearThere = 0, changedElsewhere = 0;
            for (int y = 0; y < with.Height; y++)
                for (int x = 0; x < with.Width; x++)
                {
                    int i = y * with.Width + x;
                    Color32 p = with.Pixels[i], q = without.Pixels[i];
                    if (p.r == q.r && p.g == q.g && p.b == q.b && p.a == q.a) continue;
                    bool near = Mathf.Abs(x - L.ScopeCx) <= 14 && Mathf.Abs(y - expectY) <= 14;
                    if (near) changedNearThere++; else changedElsewhere++;
                }

            Assert.Greater(changedNearThere, 0,
                           "a contact dead ahead at half range must draw half way up the centre line");
            Assert.Greater(changedNearThere, changedElsewhere,
                           "…and most of what it changed must be THERE, not scattered over the scope");
        }

        [Test]
        public void TheGuardZoneDraws_AndSaysALARMWhenSomethingIsInIt()
        {
            // Nothing in the shipped slice ARMS the guard (no pusher exposes it), so the renderer's
            // transcription of it is exercised HERE rather than left as untested dead code.
            var inside = new List<RadarBlip>
            {
                new RadarBlip(50f, 0.14f, 1.05f, RadarEchoKind.Vessel, 200f, 0.8f),
            };
            RadarRigState off = State(sea: 0f);
            var on = new RadarRigState(off.RangeNM, off.Rings, off.HeadUp, off.HeadingDeg, off.Gain,
                                       0f, 0f, off.Transmitting, off.Trails, off.Night, off.SweepDeg,
                                       blink: true, guardOn: true, guardFrom: 20f, guardTo: 80f,
                                       guardNear: 0.55f, guardFar: 0.85f);

            Assert.Greater(Differ(Paint(off, inside), Paint(on, inside)), 200,
                           "an armed guard zone must actually draw its sector");
            Assert.IsTrue(RadarRigGeometry.InGuard(50f, 0.14f / 0.2f, 20f, 80f, 0.55f, 0.85f),
                          "…and the contact used above really is inside it");
        }

        [Test]
        public void AContactBeyondTheSelectedRangeIsNotDrawnOnTheSCOPE()
        {
            // ⚠ Compared over the SCOPE's disc only, not the whole sprite. The rig's data panel prints
            // TGT as the LIST's length (radarRig.js:377), so an out-of-range target still moves that
            // counter — which is faithful and, in the shipped path, also correct: the host only ever
            // hands over contacts it read within the selected range, so the count and the glass agree.
            var beyond = new List<RadarBlip>
            {
                new RadarBlip(45f, 0.9f, 2f, RadarEchoKind.Vessel, 200f, 0.8f),
            };
            RadarRigLayout L = RadarRigGeometry.StandaloneLayout();

            int InScope(DrawSurface a, DrawSurface b)
            {
                int n = 0;
                for (int yy = L.ScopeCy - L.ScopeR; yy <= L.ScopeCy + L.ScopeR; yy++)
                    for (int xx = L.ScopeCx - L.ScopeR; xx <= L.ScopeCx + L.ScopeR; xx++)
                    {
                        int i = yy * a.Width + xx;
                        Color32 p = a.Pixels[i], q = b.Pixels[i];
                        if (p.r != q.r || p.g != q.g || p.b != q.b || p.a != q.a) n++;
                    }
                return n;
            }

            Assert.AreEqual(0, InScope(Paint(State(rangeNM: 0.2f, sea: 0f), new List<RadarBlip>()),
                                       Paint(State(rangeNM: 0.2f, sea: 0f), beyond)),
                            "0.9 NM is well outside a 0.2 NM scope and must paint no echo");
            Assert.Greater(InScope(Paint(State(rangeNM: 1.6f, sea: 0f), new List<RadarBlip>()),
                                   Paint(State(rangeNM: 1.6f, sea: 0f), beyond)), 0,
                           "…and ranging OUT must bring it onto the glass");
        }

        // ---- the coastline scan ----------------------------------------------------------------------

        [Test]
        public void LandScan_TakesOnlyTheFirstHitPerRay_BecauseARadarCastsAShadow()
        {
            var land = new RadarLandEcho();
            var cfg = RadarSettings.Default;
            land.EnsureScanned(Boat, 400f, 0f, new FakeCoast(), in cfg);

            Assert.Greater(land.Echoes.Count, 0, "there is a coast to the west; it must be found");
            Assert.LessOrEqual(land.Echoes.Count, cfg.SafeLandScanAzimuths,
                               "one echo per azimuth at most — that IS the radar shadow");
            foreach (RadarContact c in land.Echoes)
                Assert.AreEqual(RadarEchoKind.Land, c.Kind);
        }

        [Test]
        public void LandScan_UsesTheWaterlineOfTheMOMENT_SoABarCoversWithTheTide()
        {
            // ⚠ THE RULING: a radar is a sensor, not a survey. The chart's datum contour never breathes;
            // this does. A bar that dries at low water is a solid return and is simply GONE on the flood.
            var land = new RadarLandEcho();
            var cfg = RadarSettings.Default;
            var coast = new FakeCoast();

            land.EnsureScanned(Boat, 200f, 0f, coast, in cfg);
            int atLowWater = land.Echoes.Count;

            land.EnsureScanned(Boat, 200f, 3f, coast, in cfg);
            int atHighWater = land.Echoes.Count;

            Assert.Greater(atLowWater, atHighWater,
                           "the bar east of the boat bares at low water and covers on the flood — a " +
                           "radar that showed it either way would be reading the chart, not the sea");
        }

        [Test]
        public void LandScan_DoesNotRerun_ForABoatThatHasNotMovedAndATideThatHasNotTurned()
        {
            // Rule 7: the coast is a bounded scan on a slow trigger, not a per-repaint cost.
            var land = new RadarLandEcho();
            var cfg = RadarSettings.Default;
            var coast = new FakeCoast();

            Assert.IsTrue(land.EnsureScanned(Boat, 400f, 0f, coast, in cfg), "the first call must scan");
            int after = land.ScanCount;

            for (int i = 0; i < 20; i++)
                Assert.IsFalse(land.EnsureScanned(Boat, 400f, 0.001f * i, coast, in cfg),
                               "a still boat and a creeping tide must not re-scan");
            Assert.AreEqual(after, land.ScanCount);

            Assert.IsTrue(land.EnsureScanned(Boat, 400f, 1f, coast, in cfg),
                          "…but a metre of tide is a different coast");
            Assert.IsTrue(land.EnsureScanned(Boat + new Vector2(200f, 0f), 400f, 1f, coast, in cfg),
                          "…and so is half a range away");
            Assert.IsTrue(land.EnsureScanned(Boat + new Vector2(200f, 0f), 800f, 1f, coast, in cfg),
                          "…and so is a different range rung");
        }

        [Test]
        public void LandScan_WithNoTerrain_IsOpenWater_NotTheLastRegionsCoast()
        {
            var land = new RadarLandEcho();
            var cfg = RadarSettings.Default;
            land.EnsureScanned(Boat, 400f, 0f, new FakeCoast(), in cfg);
            Assert.Greater(land.Echoes.Count, 0);

            Assert.IsFalse(land.EnsureScanned(Boat, 400f, 0f, null, in cfg));
            Assert.AreEqual(0, land.Echoes.Count,
                            "no height map means open water, never another region's coastline");
        }

        [Test]
        public void LandScan_IsDeterministic_SameInputsSameCoast()
        {
            var cfg = RadarSettings.Default;
            var a = new RadarLandEcho();
            var b = new RadarLandEcho();
            a.EnsureScanned(Boat, 400f, 0.7f, new FakeCoast(), in cfg);
            b.EnsureScanned(Boat, 400f, 0.7f, new FakeCoast(), in cfg);

            Assert.AreEqual(a.Echoes.Count, b.Echoes.Count);
            for (int i = 0; i < a.Echoes.Count; i++)
            {
                Assert.AreEqual(a.Echoes[i].WorldPos.x, b.Echoes[i].WorldPos.x, 1e-4f);
                Assert.AreEqual(a.Echoes[i].WorldPos.y, b.Echoes[i].WorldPos.y, 1e-4f);
                Assert.AreEqual(a.Echoes[i].Size, b.Echoes[i].Size, 1e-4f);
            }
        }

        // ---- the mount ---------------------------------------------------------------------------------

        [Test]
        public void BrowRadarRect_MountsPortraitInTheCentreSlot_OnAPilothouseOnly()
        {
            var card = new Rect(100f, 50f, 600f, 548f);      // the pilothouse canvas at 1×

            var novi = new HelmFit(ConsoleRigKind.Novi, SounderKind.Depth, CompassMount.None, true, false);
            Assert.IsTrue(HelmInstrumentMountLayout.TryBrowRadarRect(in novi, card, out Rect mount));
            Assert.Greater(mount.height, mount.width, "the radar is a PORTRAIT instrument");
            Assert.AreEqual((float)RadarRigRender.W / RadarRigRender.H, mount.width / mount.height, 0.01f,
                            "letterboxed, never stretched");

            var cape = new HelmFit(ConsoleRigKind.Cape, SounderKind.Depth, CompassMount.None, true, false);
            Assert.IsTrue(HelmInstrumentMountLayout.TryBrowRadarRect(in cape, card, out _));

            var skiff = new HelmFit(ConsoleRigKind.Console, SounderKind.Depth, CompassMount.None, true, false);
            Assert.IsFalse(HelmInstrumentMountLayout.TryBrowRadarRect(in skiff, card, out _),
                           "a centre-console skiff has no pilothouse brow");

            var unfitted = new HelmFit(ConsoleRigKind.Novi, SounderKind.Depth, CompassMount.None, false, false);
            Assert.IsFalse(HelmInstrumentMountLayout.TryBrowRadarRect(in unfitted, card, out _),
                           "no radar, no mount");
        }

        [Test]
        public void BrowRadarRect_IsFalseUnderADomeCompass_BecauseTheDomeTAKESThatStation()
        {
            // noviRig.js:453 skips the CENTRE brow slot when a dome is fitted, and the centre slot IS the
            // radar's. Painting a scope over the binnacle would be the dash and the instrument disagreeing.
            var card = new Rect(100f, 50f, 600f, 548f);
            var dome = new HelmFit(ConsoleRigKind.Novi, SounderKind.Depth, CompassMount.Dome, true, false);
            Assert.IsFalse(HelmInstrumentMountLayout.TryBrowRadarRect(in dome, card, out _));

            var flush = new HelmFit(ConsoleRigKind.Novi, SounderKind.Depth, CompassMount.Flush, true, false);
            Assert.IsTrue(HelmInstrumentMountLayout.TryBrowRadarRect(in flush, card, out _),
                          "…but the FLUSH unit sinks into the face and the brow keeps all three");
        }

        [Test]
        public void BrowRadarRect_DoesNotCollideWithTheSounderOrTheGpsMount()
        {
            var card = new Rect(0f, 0f, 600f, 548f);
            var fit = new HelmFit(ConsoleRigKind.Novi, SounderKind.Fish, CompassMount.Flush, true, true);

            Assert.IsTrue(HelmInstrumentMountLayout.TryBrowRadarRect(in fit, card, out Rect radar));
            Assert.IsTrue(HelmInstrumentMountLayout.TryBrowSounderRect(in fit, card, out Rect sounder));
            Assert.IsTrue(HelmInstrumentMountLayout.TryBrowGpsRect(in fit, card, out Rect gps));

            Assert.IsFalse(radar.Overlaps(sounder), "three instruments, three stations");
            Assert.IsFalse(radar.Overlaps(gps));
            Assert.IsFalse(sounder.Overlaps(gps));
        }

        // ---- the range ladder is the game's, not the rig's --------------------------------------------

        [Test]
        public void RangeLadder_IsTheReScaledOne_NotTheRigsOceanChart()
        {
            RadarSettings cfg = RadarSettings.Default;
            Assert.AreEqual(0.05f, cfg.RangeNMAt(0), 1e-5f, "the closest rung is a wharf, not half a mile");
            Assert.AreEqual(1.6f, cfg.RangeNMAt(5), 1e-4f, "…and the widest is well outside any region");
            Assert.AreEqual(cfg.RangeNMAt(5), cfg.RangeNMAt(99), 1e-5f, "a rung past the end clamps");
            Assert.AreEqual(cfg.RangeNMAt(0), cfg.RangeNMAt(-3), 1e-5f);

            // The zero-filled-block trap (#420): a block absent from GameConfig.asset deserializes to
            // ZERO, and a zero range would divide the scope's scale by nothing.
            var unwritten = default(RadarSettings);
            Assert.AreEqual(cfg.RangeNMAt(2), unwritten.RangeNMAt(2), 1e-5f,
                            "an unwritten block must heal to the shipped ladder");
            Assert.Greater(unwritten.SafeRings, 0);
            Assert.Greater(unwritten.SweepDegreesPerSecond, 0f);
            Assert.Greater(unwritten.SafeSweepRepaintHz, 0f);
            Assert.GreaterOrEqual(unwritten.SafeLandScanAzimuths, 8);
        }
    }
}
