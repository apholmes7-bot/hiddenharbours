using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE ACCEPTANCE for drop 1 of ADR 0040's look: does the sea actually BREAK where the painted
    /// bottom says it should, and does the line MOVE with the tide?</b>
    ///
    /// <para>The pure tests next door prove the maths — the contour inverts the criterion exactly, the
    /// whitewater age keeps its resolution, the classification table is Battjes'. None of them can prove
    /// a single pixel of surf reaches the screen, and the whole point of PR 2 is that it does. So this
    /// stands the real Nine Mile Creek shore in a real scene, publishes a real sea, renders it at spring
    /// low and spring high on the GPU, and measures the surf.</para>
    ///
    /// <para><b>The frames it writes to <c>artifacts/</c> ARE the owner's check-in.</b> The charter gates
    /// this drop on his eye, not on an assertion — the blind-visual-debugging lesson. What the assertions
    /// below do is make sure the frames he is shown are of a surf that is really there, really tide-driven
    /// and really absent on glass, so a nod means what he thinks it means.</para>
    ///
    /// <para><b>⚠⚠ <see cref="GameServices.TidalTerrain"/> is registered by hand</b> — neither terrain
    /// component is <c>[ExecuteAlways]</c>, so <c>OnEnable</c> never fires in edit mode and the accessor
    /// stays null, whereupon <c>WaterSurface</c> bakes a flat height field and the sea draws opaque over
    /// everything. That failure looks exactly like "the surf did not draw". Same reason
    /// <see cref="NineMileCreekShoreRenderTests"/> does it, and it cost that pass a capture once.</para>
    ///
    /// <para><b>⚠⚠ The wave-field and breaker globals are published by hand too</b>, for the mirrored
    /// reason: <c>WaveFieldBridge</c> only ticks in Play, so in edit mode <c>_BreakerOuter.w</c> is 0 and
    /// the shader's very first compare says "this sea breaks nowhere". A fixture that forgot this would
    /// photograph a calm shore and call the arc broken.</para>
    ///
    /// <para><b>⚠ Self-skips without a graphics device</b> — the standing CI law. CI runs on the Null
    /// device where nothing renders and nothing is proved; a skip here is "NOT VERIFIED", never
    /// "passed".</para>
    /// </summary>
    public class BreakerSurfRenderTests
    {
        const float FrameMetres = 70f;
        const int ShotPx = 1200;
        const float G = 9.81f;

        /// <summary>A working sea for the shot: an 8 m/s onshore breeze at a middling sea state, which is
        /// an ordinary working day on this coast rather than a storm. The surf has to read at the sea the
        /// player actually sails in, not only at the extreme.</summary>
        static readonly Vector2 ShotWind = new Vector2(6f, -5.3f);
        const float ShotSeaState = 0.55f;

        GameObject _terrainGo, _seaGo, _camGo;
        Camera _cam;
        RenderTexture _rt;
        ITidalTerrain _previousTerrain;
        readonly List<GameObject> _built = new List<GameObject>();
        Color _shippedSurfColor = Color.white;
        Color _shippedLipColor = Color.white;
        float _shippedSurfAge;

        [SetUp]
        public void SetUp() => _previousTerrain = GameServices.TidalTerrain;

        [TearDown]
        public void TearDown()
        {
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (var go in _built) if (go != null) Object.DestroyImmediate(go);
            _built.Clear();
            foreach (var go in new[] { _seaGo, _camGo, _terrainGo })
                if (go != null) Object.DestroyImmediate(go);
            _seaGo = _camGo = _terrainGo = null;
            _cam = null;
            GameServices.TidalTerrain = _previousTerrain;
            // Put the globals back to silent, or a later fixture in the same editor inherits this sea.
            WaveFieldBridge.PublishGlobals(PackedWaveField.Empty);
            WaveFieldBridge.PublishBreakersOff();
        }

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — no graphics device (Null Device), so nothing rendered and " +
                    "nothing was proved. Expected on CI; drawn surf needs a GPU.");
        }

        // =============================================================================================
        //  The acceptance
        // =============================================================================================

        [Test]
        public void TheSurfDraws_MovesWithTheTide_AndIsAbsentOnGlass()
        {
            RequireAGraphicsDevice();
            BuildTheShore();
            AimAtTheSurf(NineMileCreekMainland.SpringLowWater);

            // ⭐ THE METRIC, and why it is this one and not the obvious two.
            //
            // (a) A per-pixel diff between two shots does NOT work here. The water shader runs on
            //     _Time.y — evolving churn, swash, drift — which advances between renders even in edit
            //     mode, so two shots of the SAME sea differ on 12-25 % of the frame. Every A/B built on
            //     a diff is measuring the clock. Measured, before this metric was chosen.
            // (b) Counting FOAM-WHITE pixels does not work either, and it stopped working the moment
            //     the owner ruled that surf supersedes the fringe: the surf now RELOCATES white rather
            //     than adding it, so the total barely moves (+0.20 points against a 0.09 noise floor).
            //
            // So the measurement shoots the surf in a colour nothing else in the frame can produce.
            // Red pixels ARE surf pixels, exactly, and counting them is immune to which churn cell
            // happened to be lit. The frames the OWNER looks at are the white ones below; this is how
            // the fixture knows those frames are honest.
            var debugRed = new Color(1f, 0f, 0f, 1f);

            Color32[] lowRed = Shoot("surf-red-low-water", NineMileCreekMainland.SpringLowWater,
                                     surf: true, surfColor: debugRed);
            Color32[] highRed = Shoot("surf-red-high-water", NineMileCreekMainland.SpringHighWater,
                                      surf: true, surfColor: debugRed);
            Color32[] glassRed = Shoot("surf-red-glass-calm", NineMileCreekMainland.SpringLowWater,
                                       surf: true, seaState: 0f, surfColor: debugRed);
            Color32[] offRed = Shoot("surf-red-strength-zero", NineMileCreekMainland.SpringLowWater,
                                     surf: false, surfColor: debugRed);

            // …and the frames a person actually judges.
            Shoot("surf-white-low-water", NineMileCreekMainland.SpringLowWater, surf: true);
            Shoot("surf-white-high-water", NineMileCreekMainland.SpringHighWater, surf: true);
            Shoot("surf-white-OFF-low-water", NineMileCreekMainland.SpringLowWater, surf: false);

            float low = SurfFraction(lowRed);
            float high = SurfFraction(highRed);
            float glass = SurfFraction(glassRed);
            float off = SurfFraction(offRed);
            float moved = CentroidShiftPx(lowRed, highRed);

            Debug.Log($"[breaker-surf] surf covers {low:P2} of the frame at spring low, {high:P2} at " +
                      $"spring high; its centroid moves {moved:F0} px between the two. Glass {glass:P3}, " +
                      $"strength-0 {off:P3}. Frames in artifacts/surf-*.png — the white pair is the check-in.");

            // 1. It draws. Zero here is the drop failing silently, and it is what an unpublished contour,
            //    a stale material or a sorting mistake all look like.
            Assert.That(low, Is.GreaterThan(0.004f),
                $"the surf covered only {low:P3} of the frame — check PublishBreakerGlobals ran and that " +
                "_BreakerOuter.w is 1");

            // 2. …as a BAND, not a wash. Breaking is supposed to follow a depth contour; if it covered
            //    most of the frame the gate would be inverted or the depths swapped.
            Assert.That(low, Is.LessThan(0.40f),
                $"the surf covered {low:P1} of the frame — that is not a breaker line, that is the sea " +
                "going white. Check the break/outer depth ordering.");

            // 3. ⭐ THE HEADLINE: nothing animates the break line. It moves because depth is
            //    waterLevel - seabed and the tide moved the water level.
            //
            //    ⚠ The claim has to cover BOTH outcomes, because at this spot the shoal drowns: measured,
            //    the surf covers 5.10 % of the frame at spring low and 0.00 % at spring high. A centroid
            //    comparison alone would be degenerate there — an empty frame has no centroid, and the
            //    "shift" would just be the low-water centroid's distance from the origin, which is not a
            //    measurement of anything. So: a bar either BOILS SOMEWHERE ELSE or it SLEEPS, and both
            //    are the same physics doing the same thing.
            if (high > 0f)
            {
                Assert.That(moved, Is.GreaterThan(20f),
                    $"the surf covers {low:P2} at low and {high:P2} at high but its centroid moved only " +
                    $"{moved:F0} px — the break line is not riding the tide");
            }
            else
            {
                Assert.That(low, Is.GreaterThan(0.004f),
                    "the shoal must actually boil at low water for its drowning at high to mean anything");
                // The bar sleeps. This IS "a bar that boils at half-ebb and sleeps at high water",
                // measured — and it is the strongest form the claim can take.
            }

            // 4. Glass calm is sacred (ADR 0018). A dead-calm sea breaks NOWHERE, and here that is an
            //    exact claim rather than a tolerance: the contour reports Breaks = false, the shader's
            //    first compare fails, and not one red pixel is written.
            Assert.That(glass, Is.EqualTo(0f),
                $"glass calm drew surf on {glass:P3} of the frame — a dead-calm sea must break nowhere");

            // 5. …and the dial genuinely turns it off.
            Assert.That(off, Is.EqualTo(0f),
                $"strength 0 drew surf on {off:P3} of the frame — the passthrough is not a passthrough");
        }

        [Test]
        public void TheSurfIsAContourOfDepth_NotAFixedWidthOffTheWaterline()
        {
            // The claim that separates this from the shore fringe it supersedes. The fringe is drawn at
            // a fixed width off the waterline; the surf is an ISO-DEPTH band, so where the bed is gentle
            // it is wide and where the bed is steep it is thin — and it sits at a depth, not at a
            // distance. Sampling the depth under every surf pixel is the direct test of that.
            RequireAGraphicsDevice();
            BuildTheShore();
            AimAtTheSurf(NineMileCreekMainland.SpringLowWater);

            var terrain = _terrainGo.GetComponent<MainlandTidalTerrain>();
            WaveTrains trains = WaveMath.TrainsFrom(ShotWind, ShotSeaState, GameServices.WaveField);
            BreakerContour contour = BreakerMath.ContourFor(trains.Dominant,
                WaveFetch.Envelope01(0f, GameServices.WaveFetch), GameServices.Breakers);

            Color32[] red = Shoot("surf-red-contour-check", NineMileCreekMainland.SpringLowWater,
                                  surf: true, surfColor: new Color(1f, 0f, 0f, 1f));

            // Walk the frame, and for every surf pixel ask how deep the water under it is.
            float waterLevel = NineMileCreekMainland.SpringLowWater;
            float worldPerPx = (FrameMetres) / ShotPx;
            Vector3 camPos = _cam.transform.position;
            int surfPx = 0, deeperThanTheGate = 0;

            for (int y = 0; y < ShotPx; y += 3)
            for (int x = 0; x < ShotPx; x += 3)
            {
                if (!IsSurf(red[y * ShotPx + x])) continue;
                surfPx++;
                var world = new Vector2(camPos.x + (x - ShotPx * 0.5f) * worldPerPx,
                                        camPos.y + (y - ShotPx * 0.5f) * worldPerPx);
                float depth = waterLevel - terrain.ElevationAt(world);
                // The gate's outer edge, with a texel of slack: the height map is 8-bit over a ~10 m
                // range (3.91 cm a code) and the sampled world position is a pixel centre, so an exact
                // bound would fail on quantization rather than on physics.
                if (depth > contour.OuterDepths.x + 0.25f) deeperThanTheGate++;
            }

            Assert.That(surfPx, Is.GreaterThan(200), "there must be surf in the frame to test");
            float stray = deeperThanTheGate / (float)surfPx;
            Debug.Log($"[breaker-surf] {surfPx} surf samples; {stray:P2} of them sit deeper than the " +
                      $"{contour.OuterDepths.x:F2} m gate edge.");
            Assert.That(stray, Is.LessThan(0.05f),
                $"{stray:P1} of the surf is drawn in water deeper than the break gate — it is not a " +
                "depth contour, so something other than the bathymetry is placing it");
        }

        [Test]
        public void WithTheStrengthDialledToZero_TheSurfContributesNothing()
        {
            // The passthrough contract, the way WaveFetch ships one: the owner turns this off and gets
            // back precisely the sea he had.
            //
            // WARNING: NOT a per-pixel diff of two shots. Two captures of the SAME sea differ on 12-25 %
            // of the frame purely because _Time.y advanced between them (measured before this test was
            // written). The claim is that the surf contributes NOTHING, and the debug colour states
            // exactly that with no tolerance at all.
            RequireAGraphicsDevice();
            BuildTheShore();
            AimAtTheSurf(NineMileCreekMainland.SpringLowWater);

            Color32[] off = Shoot("surf-red-passthrough", NineMileCreekMainland.SpringLowWater,
                                  surf: false, surfColor: new Color(1f, 0f, 0f, 1f));
            Assert.That(SurfFraction(off), Is.EqualTo(0f),
                "with the strength dialled to zero the surf must contribute not one pixel");
        }

        /// <summary>Fraction of the frame the DEBUG-red surf covers. Red is a colour nothing else in a
        /// water frame produces, so this counts surf pixels exactly — and unlike a per-pixel diff it does
        /// not measure the shader's clock, and unlike a foam-white count it is not confounded by the surf
        /// superseding the fringe.</summary>
        static float SurfFraction(Color32[] px)
        {
            int n = 0;
            for (int i = 0; i < px.Length; i++) if (IsSurf(px[i])) n++;
            return n / (float)px.Length;
        }

        static bool IsSurf(Color32 c) => c.r > 150 && c.g < 110 && c.b < 110;

        /// <summary>How far the surf's centre of mass moved between two frames, in pixels. An AREA change
        /// alone would not prove the break line moved — more of the same band would do that too — so the
        /// tide claim is made on the centroid.</summary>
        static float CentroidShiftPx(Color32[] a, Color32[] b)
        {
            Vector2 ca = Centroid(a), cb = Centroid(b);
            return (ca - cb).magnitude;
        }

        static Vector2 Centroid(Color32[] px)
        {
            double sx = 0, sy = 0; int n = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (!IsSurf(px[i])) continue;
                sx += i % ShotPx; sy += i / ShotPx; n++;
            }
            return n == 0 ? Vector2.zero : new Vector2((float)(sx / n), (float)(sy / n));
        }

        [Test]
        public void ThePlungingAnatomy_DrawsOnlyWhereTheSlopeEarnsIt()
        {
            // ⭐ THE DROP-2 CLAIM, and the one this whole arc rests on: a lip and a barrel appear only
            // where the BATHYMETRY has earned them. Nobody paints a barrel in; the seabed either produces
            // one or it does not.
            //
            // So the fixture does not go looking for a pretty frame. It sweeps the region for the highest
            // surf-similarity number on the break contour and REPORTS it — and if this coast is too
            // gentle to plunge anywhere, that is a true finding about the coast and it says so, rather
            // than quietly passing on a frame with no anatomy in it.
            RequireAGraphicsDevice();
            BuildTheShore();

            var terrain = _terrainGo.GetComponent<MainlandTidalTerrain>();
            WaveTrains trains = WaveMath.TrainsFrom(ShotWind, ShotSeaState, GameServices.WaveField);
            var settings = GameServices.Breakers;
            BreakerContour contour = BreakerMath.ContourFor(trains.Dominant,
                WaveFetch.Envelope01(0f, GameServices.WaveFetch), settings);
            Assert.IsTrue(contour.Breaks, "the shot sea must break somewhere");

            float waterLevel = NineMileCreekMainland.SpringLowWater;
            float h0 = 2f * trains.Dominant.Amplitude;
            Vector2 centre = NineMileCreekBuilder.NineMileCreekSeaCenter;
            Vector2 size = NineMileCreekBuilder.NineMileCreekSeaSize;

            float bestXi = 0f;
            Vector2 bestAt = centre;
            const int steps = 160;
            for (int iy = 0; iy <= steps; iy++)
            for (int ix = 0; ix <= steps; ix++)
            {
                var at = new Vector2(centre.x + size.x * (ix / (float)steps - 0.5f),
                                     centre.y + size.y * (iy / (float)steps - 0.5f));
                float depth = waterLevel - terrain.ElevationAt(at);
                if (depth <= 0f || Mathf.Abs(depth - contour.BreakDepths.x) > 0.08f) continue;

                // The magnitude of the bed gradient, which is what the shader's SeabedSlopeMag reads.
                float sx = BreakerMath.BedSlopeAlong(at, Vector2.right, settings.SlopeProbeMeters, terrain);
                float sy = BreakerMath.BedSlopeAlong(at, Vector2.up, settings.SlopeProbeMeters, terrain);
                float xi = BreakerMath.Iribarren(Mathf.Sqrt(sx * sx + sy * sy), h0, trains.Dominant.Wavelength);
                if (xi > bestXi) { bestXi = xi; bestAt = at; }
            }

            float weight = BreakerMath.PlungingWeight01(bestXi, in settings);
            Debug.Log($"[breaker-plunge] steepest break-contour point on this coast: xi = {bestXi:F3} " +
                      $"(plunging weight {weight:F3}, class {BreakerMath.ClassFor(bestXi, in settings)}) " +
                      $"at {bestAt}. Battjes' spilling/plunging threshold is {settings.SpillingLimit}.");

            _cam.transform.position = new Vector3(bestAt.x, bestAt.y, -100f);
            Shoot("surf-plunge-steepest", waterLevel, surf: true);
            Shoot("surf-plunge-steepest-LIPRED", waterLevel, surf: true,
                  lipColor: new Color(1f, 0f, 0f, 1f));

            if (weight <= 0.01f)
            {
                // A TRUE finding, not a skip: this coast spills everywhere. The anatomy is correct to
                // draw nothing here, and saying so is the point — a fixture that reported success on a
                // frame with no lip in it would be exactly the blind-visual-debugging trap.
                Assert.Pass(
                    $"Nine Mile Creek's steepest break-contour point reads xi = {bestXi:F3}, below " +
                    $"Battjes' {settings.SpillingLimit} spilling/plunging threshold — this coast SPILLS " +
                    "everywhere, so no lip or barrel is drawn anywhere on it, which is correct. The " +
                    "plunging anatomy is pinned by BreakerPlungingTests; a coast that earns it is owed " +
                    "a frame before the look can be judged.");
            }

            // …and if it does plunge, the anatomy must actually appear.
            Color32[] lipRed = Shoot("surf-plunge-lip", waterLevel, surf: true,
                                     lipColor: new Color(1f, 0f, 0f, 1f));
            float lipCover = SurfFraction(lipRed);
            Debug.Log($"[breaker-plunge] the thrown lip covers {lipCover:P3} of the frame.");
            Assert.That(lipCover, Is.GreaterThan(0f),
                $"xi = {bestXi:F3} earns a plunging weight of {weight:F3}, but no lip was drawn");
        }

        // =============================================================================================
        //  the scene — the NineMileCreekShoreRenderTests pattern, which is the region as it ships
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Aim the camera at where the PHYSICS says the surf is</b>, rather than at a hand-picked
        /// stretch of coast.
        ///
        /// <para>The first draft aimed at a named coast sector offset "seaward", and photographed
        /// farmland: 92 % of the frame was fields and dirt, the sea was a sliver in one corner, and every
        /// metric it reported was measuring sky. It reported "surf drew on 5.3 % of the frame" and that
        /// number was meaningless. A fixture that can be pointed at the wrong place will eventually be
        /// pointed at the wrong place.</para>
        ///
        /// <para>So this scans the region for the water whose depth sits closest to the solved break
        /// depth — which is, by construction, exactly where breakers belong. If the model says the region
        /// breaks nowhere, the scan finds nothing and the test says so instead of quietly shooting a
        /// field.</para>
        /// </summary>
        /// <summary>
        /// Sheltered water: inside the harbour shoal's own footprint and landward of the breakwater
        /// crest. Derived from the region's own fills rather than boxed by hand, so re-siting the arm or
        /// the shoal re-cuts what counts as sheltered.
        /// </summary>
        internal static bool InsideTheHarbour(Vector2 p)
        {
            var shoal = NineMileCreekMainland.HarbourShoalFill;
            bool inShoalX = Mathf.Abs(p.x - shoal.Center.x) <= shoal.HalfSize.x + shoal.Falloff;
            var arm = NineMileCreekMainland.BreakwaterFill;
            bool landwardOfTheArm = p.y > arm.Center.y + arm.HalfSize.y;
            return inShoalX && landwardOfTheArm;
        }

        internal static Vector2 FindTheSurfZone(ITidalTerrain terrain, float waterLevel, float breakDepth)
        {
            Vector2 centre = NineMileCreekBuilder.NineMileCreekSeaCenter;
            Vector2 size = NineMileCreekBuilder.NineMileCreekSeaSize;

            Vector2 best = centre;
            float bestRun = -1f;
            int wet = 0, atBreakDepth = 0;

            const int steps = 128;
            for (int iy = 0; iy <= steps; iy++)
            for (int ix = 0; ix <= steps; ix++)
            {
                var p = new Vector2(centre.x + size.x * (ix / (float)steps - 0.5f),
                                    centre.y + size.y * (iy / (float)steps - 0.5f));

                // ⭐⭐ NOT INSIDE THE BREAKWATER. Surf is what the open sea does to a shelving coast, and
                // the whole point of a breakwatered basin is that it does not happen there — so a frame
                // shot inside the bullpen is measuring the wrong subject however good its score is.
                //
                // This was latent until 2026-09-05. The scoring below picks the candidate with the
                // LONGEST shoreward run of breaking water, and the berth trench cut for the owner's
                // wet-wall ruling handed it a better one than the coast had: a bank climbing 2.6 m from
                // the trench to the wall, entirely inside the harbour. The fixture aimed at
                // (124.7, 74.4) — ten metres off the quay — and then reported 15.8 % of the surf standing
                // in water deeper than the break gate, which is exactly what a depth band drawn across a
                // dredged pocket looks like. The terrain moved and the fixture followed it.
                if (InsideTheHarbour(p)) continue;

                float depth = waterLevel - terrain.ElevationAt(p);
                if (depth <= 0f) continue;
                wet++;

                // Only points actually ON the break contour are candidates.
                if (Mathf.Abs(depth - breakDepth) > 0.06f) continue;
                atBreakDepth++;

                // ⭐ Score by HOW FAR THE SURF RUNS: march shoreward and count the metres of breaking
                // water between here and dry land. That is the width of the surf zone, and it is the one
                // thing that decides whether the band reads.
                //
                // Two earlier scorings failed, both by proxy: "closest to the break depth" put the camera
                // on farmland, and "prefer a gentle bed" did not discriminate at all because the painted
                // map's local gradient is near zero across whole texel runs. Measuring the run directly
                // is neither a proxy nor a guess.
                float run = 0f;
                Vector2 walk = p;
                for (int i = 0; i < 60; i++)
                {
                    Vector2 grad = ShoreGradient(terrain, walk);
                    if (grad.sqrMagnitude < 1e-8f) break;
                    walk += grad * 1f;
                    if (waterLevel - terrain.ElevationAt(walk) <= 0f) break;
                    run += 1f;
                }

                // Prefer a long run, and prefer being clear of the region border so the frame is sea and
                // not half a rectangle edge.
                float edge = Mathf.Min(Mathf.Min(ix, steps - ix), Mathf.Min(iy, steps - iy)) / (float)steps;
                float score = run + edge * 10f;
                if (score > bestRun) { bestRun = score; best = p; }
            }

            Assert.That(wet, Is.GreaterThan(0), "the region has no water in it at all at this tide");
            Assert.That(atBreakDepth, Is.GreaterThan(0),
                $"no water in the region sits at the {breakDepth:F2} m break depth — nothing to photograph");
            Debug.Log($"[breaker-surf] {atBreakDepth} sample points sit on the break contour; " +
                      $"best surf run scored {bestRun:F1}");
            return best;
        }

        /// <summary>The shoreward unit direction from the painted bed's gradient — the shader's own
        /// <c>ShoreDir</c>, in C#, so the fixture walks the way the surf does.</summary>
        internal static Vector2 ShoreGradient(ITidalTerrain terrain, Vector2 at)
        {
            const float h = 1f;
            float ex = terrain.ElevationAt(at + new Vector2(h, 0f)) - terrain.ElevationAt(at - new Vector2(h, 0f));
            float ey = terrain.ElevationAt(at + new Vector2(0f, h)) - terrain.ElevationAt(at - new Vector2(0f, h));
            var g = new Vector2(ex, ey);
            return g.sqrMagnitude > 1e-10f ? g.normalized : Vector2.zero;
        }

        void AimAtTheSurf(float waterLevel)
        {
            var terrain = _terrainGo.GetComponent<MainlandTidalTerrain>();
            WaveTrains trains = WaveMath.TrainsFrom(ShotWind, ShotSeaState, GameServices.WaveField);
            BreakerContour contour = BreakerMath.ContourFor(trains.Dominant,
                WaveFetch.Envelope01(0f, GameServices.WaveFetch), GameServices.Breakers);
            Assert.IsTrue(contour.Breaks,
                "the shot sea must break somewhere, or there is nothing to photograph");

            Vector2 at = FindTheSurfZone(terrain, waterLevel, contour.BreakDepths.x);
            _cam.transform.position = new Vector3(at.x, at.y, -100f);
            Debug.Log($"[breaker-surf] aimed at {at} — break depth {contour.BreakDepths.x:F2} m, " +
                      $"outer {contour.OuterDepths.x:F2} m, water level {waterLevel:F2} m");
        }

        void BuildTheShore()
        {
            _terrainGo = new GameObject("TidalTerrain");
            var terrain = _terrainGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekBuilder.ConfigureNineMileCreekTerrain(terrain);
            GameServices.TidalTerrain = terrain;

            var region = AssetDatabase.LoadAssetAtPath<RegionDef>(
                WaterSceneTemplate.RegionAssetPathFor("NineMileCreek"));
            Assert.IsNotNull(region, "Data/Regions/NineMileCreek.asset must exist to size the ground");
            Assert.That(NineMileCreekBuilder.BuildSplatGround(region), Is.True,
                "the painted ground must build — without it the capture is of black land");
            Remember("TerrainSplat");

            var waterMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/Materials/Water.mat");
            Assert.IsNotNull(waterMat, "Water.mat must exist — there is nothing to photograph without it");
            // The shipped surf colour, so every shot can restore it (see SetShot's sticky-block note).
            _shippedSurfColor = waterMat.HasProperty("_SurfColor")
                ? waterMat.GetColor("_SurfColor") : Color.white;
            _shippedLipColor = waterMat.HasProperty("_SurfLipColor")
                ? waterMat.GetColor("_SurfLipColor") : Color.white;
            _shippedSurfAge = waterMat.HasProperty("_SurfAgeStrength")
                ? waterMat.GetFloat("_SurfAgeStrength") : 0f;

            _seaGo = new GameObject("Sea");
            _seaGo.SetActive(false);
            _seaGo.transform.position = new Vector3(NineMileCreekBuilder.NineMileCreekSeaCenter.x,
                                                    NineMileCreekBuilder.NineMileCreekSeaCenter.y, 0f);
            var sr = _seaGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -5;
            sr.sharedMaterial = waterMat;
            var seaTile = WaterSceneTemplate.LoadSpriteAny("Assets/_Project/Art/Tilesets/Water/SeaTile.png");
            if (seaTile != null) sr.sprite = seaTile;
            WaterSceneTemplate.ConfigureSeaPlane(sr, NineMileCreekBuilder.NineMileCreekSeaSize);
            _seaGo.AddComponent<WaterSurface>();
            WaterSceneTemplate.ConfigureLandRegionWater(
                _seaGo, NineMileCreekBuilder.NineMileCreekSeaCenter,
                NineMileCreekBuilder.NineMileCreekSeaSize,
                NineMileCreekBuilder.NineMileCreekHeightResolution,
                NineMileCreekBuilder.NineMileCreekHeightMin,
                NineMileCreekBuilder.NineMileCreekHeightMax,
                terrain.MaxShoreGradient());
            _seaGo.SetActive(true);

            _camGo = new GameObject("SurfShotCam");
            _cam = _camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = FrameMetres * 0.5f;
            _cam.nearClipPlane = 1f;
            _cam.farClipPlane = 400f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.05f, 0.07f, 0.09f, 1f);
            _cam.allowMSAA = false;
            _rt = new RenderTexture(ShotPx, ShotPx, 24, RenderTextureFormat.ARGB32)
            { filterMode = FilterMode.Point };
            _cam.targetTexture = _rt;
        }

        void Remember(string rootName)
        {
            var go = GameObject.Find(rootName);
            if (go != null) _built.Add(go);
        }

        /// <summary>
        /// Stand up the sea the shader will draw, and the contour the surf reads — by hand, because the
        /// bridge only ticks in Play. Exactly the trains, exactly the publish path, exactly the settings
        /// instance the game uses (<see cref="GameServices"/>), so the frame is of the shipped model and
        /// not of a lookalike assembled here.
        /// </summary>
        void PublishTheSea(float seaState01)
        {
            WaveTrains trains = WaveMath.TrainsFrom(ShotWind, seaState01, GameServices.WaveField);
            WaveFieldBridge.PublishGlobals(WaveFieldBridge.Pack(in trains));
            WaveFieldBridge.PublishFetchGlobals(GameServices.WaveFetch, ShotWind);
            WaveFieldBridge.PublishBreakerGlobals(trains.Dominant, GameServices.WaveFetch,
                                                  GameServices.Breakers);
        }

        /// <summary>
        /// Scrub the tide and the surf dial. ⚠⚠ Through a <see cref="MaterialPropertyBlock"/>, NEVER onto
        /// the shared material: writing <c>_WaterLevel</c> to <c>Water.mat</c> re-tunes the sea for the
        /// whole game and leaves the owner's hero material dirty in the working tree. That has happened
        /// on this repo once already and is why <c>git add -A</c> is banned here.
        /// </summary>
        void SetShot(float waterLevel, bool surf, Color? surfColor = null, Color? lipColor = null)
        {
            var surface = _seaGo.GetComponent<WaterSurface>();
            if (surface != null)
            {
                var so = new SerializedObject(surface);
                var preview = so.FindProperty("_previewWaterLevel");
                if (preview != null)
                {
                    preview.floatValue = waterLevel;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            var sr = _seaGo.GetComponent<SpriteRenderer>();
            var block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            block.SetFloat("_WaterLevel", waterLevel);
            block.SetFloat("_SurfStrength", surf ? 1f : 0f);
            // A DEBUG colour when asked for: painting the surf a colour nothing else in the frame uses
            // is the only way to MEASURE where it lands when it otherwise draws white on white. Through
            // the property block, so Water.mat is untouched.
            //
            // ⚠ ALWAYS written, never conditionally. A MaterialPropertyBlock is STICKY: the first draft
            // set the colour only when a debug colour was asked for, so the red from the measurement
            // shots survived into the "white" frames and the owner's check-in pair came out scarlet.
            // Writing the shipped colour back on every shot is what makes each capture independent of
            // whatever the last one wanted.
            block.SetColor("_SurfColor", surfColor ?? _shippedSurfColor);
            block.SetColor("_SurfLipColor", lipColor ?? _shippedLipColor);
            // ⚠ ROW 2 (one foam language): the whitewater now WALKS the sea's foam ramp, and the walk
            // REPLACES the colour it is handed with a convex combination of the palette anchors — so a
            // debug colour composed through it stops being a debug colour and the measurement scores the
            // palette instead of the surf. The debug arms therefore pin the walk to its passthrough (the
            // A/B zero it ships with); the LOOK shots keep the shipped value.
            //
            // ⚠ EITHER colour, not just the sheet's: ThePlungingAnatomy_DrawsOnlyWhereTheSlopeEarnsIt
            // paints the LIP alone, and keying this on surfColor left the walk live over a red lip — the
            // lip came back as the palette's foam anchor and the test read "no lip was drawn".
            block.SetFloat("_SurfAgeStrength", (surfColor.HasValue || lipColor.HasValue) ? 0f : _shippedSurfAge);
            sr.SetPropertyBlock(block);
        }

        Color32[] Shoot(string name, float waterLevel, bool surf, float seaState = ShotSeaState,
                        Color? surfColor = null, Color? lipColor = null)
        {
            PublishTheSea(seaState);
            SetShot(waterLevel, surf, surfColor, lipColor);

            _cam.Render();
            _cam.Render();   // the second is read: a cold shader cache has faked a regression here before

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(ShotPx, ShotPx, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, ShotPx, ShotPx), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Color32[] px = tex.GetPixels32();

            string dir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return px;
        }

    }
}
