using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A LAMP IS A SOURCE, NOT A POOL</b> — the owner's ruling of 2026-09-04, measured on the pier he
    /// was looking at when he made it, and photographed.
    ///
    /// <para><b>Verbatim:</b> <i>"spotlight doesnt read on water or enviroement its just a flat white, dock
    /// lights are just a round glow, it should glow from within the lamp reasilitcally."</i></para>
    ///
    /// <para><b>Why this is a PLAY-mode fixture in the REAL scene.</b> The claim under test is about what a
    /// player sees: the day/night multiply overlay, the additive glow above it and the lamp shadows above
    /// that, composited in that order, over the actual planks and bollards of the St Peters pier. None of
    /// that exists in a synthetic stage, and the overlay and the light quads are pinned to
    /// <c>Camera.main</c>'s own frustum and depth — so a second camera would photograph a frame with the
    /// night missing. The fixture loads the region, places the lamps through the BUILDER'S own code path
    /// (<see cref="LampPosts.Place"/>, so a lamp that only works when hand-placed cannot pass), and renders
    /// the game's own camera.</para>
    ///
    /// <para><b>One build, one dial, two arms.</b> The BEFORE arm is not a git checkout — it is this same
    /// frame with every placed lamp's bloom put back to <see cref="LightPresets.ReachMetres"/>, which is
    /// exactly what shipped. So the two plates differ by the thing under review and by nothing else: same
    /// scene, same clock, same camera, same night.</para>
    ///
    /// <para>Plates land in <c>Application.temporaryCachePath/lights-are-sources/</c> and are copied into
    /// <c>docs/art/spikes/lights-are-sources/</c> by the lane; the paths are logged.</para>
    /// </summary>
    public class LightsAreSourcesPlatePlayTests
    {
        const string SceneName = "StPeters";
        const string PlateDir = "lights-are-sources";

        /// <summary>Plate height in pixels. The WIDTH is derived from the camera's own aspect and never
        /// chosen: <c>DayNightController</c> fits its whole-frame multiply to
        /// <c>orthographicSize × aspect</c>, so a plate shot at any other aspect comes back with the night
        /// as a rectangle in the middle and daylight showing round it — which reads exactly like a
        /// lighting bug and is not one.</summary>
        const int PlateHeightPx = 900;

        /// <summary>How much brighter than the lamps-off frame a pixel must be to count as LIT. Summed over
        /// the three channels of a 0..255 read-back, so ~4 % of one channel — above the dither and well
        /// below anything a lamp does.</summary>
        const int LitThreshold = 30;

        /// <summary>What counts as BLOWN OUT: every channel at or above this. The disc the owner refused is
        /// not merely large, it is saturated — it is cream all through, which is why the planks and the
        /// bollards inside it stop existing.</summary>
        const byte SaturatedChannel = 235;

        readonly List<Object> _spawned = new List<Object>();
        Camera _cam;
        RenderTexture _rt;
        int _w, _h;

        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;   // ⚠ a STATIC: left at 0 it stops every test that follows

            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();

            // ⚠️ PUT THE WORLD BACK. A PlayMode run shares one player loop, and leaving St Peters resident
            // hands every test that follows a wharf, a tide and a seabed it never asked for.
            var clean = SceneManager.CreateScene("LightsAreSourcesCleanup");
            SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == SceneName)
                    yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        // =============================================================================================
        //  1 — the pier lantern: the bloom comes off the ground and back onto the lamp
        // =============================================================================================

        /// <summary>
        /// <b>The 02:00 pier, both arms.</b> Two numbers, because the complaint has two halves.
        ///
        /// <para>The first is the LIT FOOTPRINT — how many pixels the lamps brighten — because <i>"just a
        /// round glow"</i> is a statement about AREA, and so is the fix.</para>
        ///
        /// <para>The second says why the area mattered: RELATIVE LOCAL CONTRAST inside the region the disc
        /// used to cover. What a saturated disc does is not "be bright", it is FLATTEN — the deck, the
        /// bollards and the post all go to one cream — and the shrink is only worth anything if they come
        /// back. They come back to within a couple of per cent of how the pier reads UNLIT, which is
        /// asserted too: PR A moves what is drawn at the lamp and puts no light on the ground at all.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ThePierLantern_GlowsAtItsFitting_AndGivesThePlanksBack()
        {
            yield return LoadTheWharf();
            yield return SetNight(2f);

            IReadOnlyList<LampPosts.Site> sites = StPetersWharf.LampPostSites();
            GameObject lamps = PlaceLamps(sites);
            yield return FrameOn(sites[0].Position + new Vector2(0f, -1f));

            // DARK: the same frame with the lamps off, so "lit" is measured against this pier and not
            // against an assumption about how dark 02:00 is.
            //
            // ⚠ AND TWICE, a couple of frames apart, because the difference between two IDENTICAL states is
            // this scene's own NOISE FLOOR, and that is a measurement rather than an assumption: "how many
            // pixels did the lamp change" means nothing until you know how many change when nothing does.
            // It reads zero here only because FrameOn stopped engine time; before that it read 639,757.
            SetLampsEnabled(lamps, false);
            yield return null; yield return null;
            byte[] dark = Capture();
            yield return null; yield return null;
            int noiseFloor = Footprint(dark, Capture());

            // AFTER: the shipped look — every lamp blooming at its own measured fitting.
            SetLampsEnabled(lamps, true);
            yield return null; yield return null;
            byte[] fitting = Capture();
            SavePlate("02-pier-0200-fitting-AFTER.png", fitting);

            // BEFORE: the same lamps drawn at the pool, which is what shipped.
            float[] blooms = SetBloomToReach(lamps);
            yield return null; yield return null;
            byte[] pool = Capture();
            SavePlate("01-pier-0200-pool-BEFORE.png", pool);
            SavePlate("00-pier-0200-dark.png", dark);

            int litFitting = Footprint(dark, fitting);
            int litPool = Footprint(dark, pool);
            int px = _w * _h;

            // Judge BOTH arms on the SAME pixels: the ones the disc used to cover, which are the ones the
            // owner was looking at when he called it a round glow.
            bool[] poolMask = LitMask(dark, pool, out int _);
            float detailPool = RelativeLocalContrast(pool, poolMask);
            float detailFitting = RelativeLocalContrast(fitting, poolMask);
            float detailDark = RelativeLocalContrast(dark, poolMask);

            Debug.Log($"[{PlateDir}] {_w}x{_h}  noise floor {noiseFloor} px  |  lit: fitting {litFitting} " +
                      $"({100f * litFitting / px:0.00} %) vs pool {litPool} ({100f * litPool / px:0.00} %)" +
                      $"  |  relative local contrast inside the pool's own footprint: pool {detailPool:0.0000}" +
                      $" -> fitting {detailFitting:0.0000} ({detailFitting / Mathf.Max(detailPool, 1e-6f):0.0}x)" +
                      $", unlit pier {detailDark:0.0000}" +
                      $"  |  blown out: fitting {Saturated(fitting)} vs pool {Saturated(pool)}" +
                      $"  |  blooms restored to {string.Join(", ", blooms)}");

            Assert.Greater(litPool, noiseFloor * 4,
                $"the BEFORE arm lit {litPool} px against a {noiseFloor} px noise floor — that is the sea " +
                "moving, not a lamp, so neither number below means anything");
            Assert.Greater(litFitting, Mathf.Max(noiseFloor, 100),
                $"the lantern lit {litFitting} px (noise floor {noiseFloor}): it does not READ. A lamp " +
                "nobody can see is not the fix the owner asked for, and with the sea held still a floor of " +
                "zero would let ANY number through — so the bar is a hundred pixels of actual lantern.");

            Assert.Less(litFitting, litPool * 0.25f,
                $"the lantern covers {100f * litFitting / litPool:0.0} % of the area the pool covered. " +
                "'Just a round glow' is a complaint about area; if the area barely moved, neither did the " +
                "picture he refused.");

            // ⭐ AND THE POINT OF SHRINKING IT: the planks come back. Measured as RELATIVE LOCAL CONTRAST
            // inside the region the pool used to cover, because what a saturated disc does is not "be
            // bright", it is FLATTEN — the deck, the bollards and the post all go to one cream.
            Assert.Greater(detailFitting, detailPool * 2f,
                $"inside the pool's own footprint the frame reads at {detailFitting:0.0000} relative local " +
                $"contrast against the disc's {detailPool:0.0000}. The disc's whole crime is that the " +
                "planks, the bollards and the post stop existing inside it — if the contrast did not come " +
                "back, they are still gone.");

            // ⚠ AND THE HONEST HALF-PICTURE, asserted rather than merely admitted: with the disc gone the
            // pier under the lamp reads as it reads UNLIT, because a bloom on the lantern is not a pool on
            // the planks and nothing here pretends otherwise. That is what the illumination PR is for.
            Assert.AreEqual(detailDark, detailFitting, detailDark * 0.25f,
                $"the planks under the lantern read at {detailFitting:0.0000} against {detailDark:0.0000} " +
                "with the lamp switched off entirely. They should be near enough identical: PR A moves what " +
                "is DRAWN at the lamp and puts no light on the ground at all. If this drifts, something " +
                "started lighting the deck and it should be said out loud.");
        }

        /// <summary>
        /// <b>The noon control: neither arm emits at all.</b> The night gate lives in the shared additive
        /// shader and reads the published tint (ADR 0016), and no preset may touch it — so shrinking a
        /// bloom must be exactly invisible by day.
        ///
        /// <para><b>⚠ The strict assertion is ARM TO ARM, not against darkness.</b> The claim this PR has
        /// to make by day is that shrinking a bloom changes nothing, and that is exactly what the two arms
        /// answer — immune to the one thing this fixture cannot pin tightly, which is the precise tint the
        /// controller has eased to by the time it is frozen (the game's noon under this day's weather is
        /// not a pure white). Whether a lamp emits AT ALL by day is the gate's question, and the gate's
        /// arithmetic is pinned bit-exactly in <c>LightMathTests</c>; here it is bounded and reported
        /// rather than asserted at zero.</para>
        ///
        /// <para>Both are measured against this scene's own NOISE FLOOR — two frames of the same state —
        /// because a first pass asserted a bit-identical frame and came back with 614,705 changed pixels.
        /// That was not a lamp burning at noon: it was the sea, which keeps moving between two frames
        /// however hard the game clock is frozen, and which by day is bright enough for that motion to
        /// clear any threshold. A control that cannot state its own noise floor is not a control.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator AtNoon_NeitherArmEmits_AndTheFrameIsUnchanged()
        {
            yield return LoadTheWharf();
            yield return SetNight(12f);

            IReadOnlyList<LampPosts.Site> sites = StPetersWharf.LampPostSites();
            GameObject lamps = PlaceLamps(sites);
            yield return FrameOn(sites[0].Position + new Vector2(0f, -1f));

            SetLampsEnabled(lamps, false);
            yield return null; yield return null;
            byte[] darkA = Capture();
            yield return null; yield return null;
            byte[] darkB = Capture();
            int noiseFloor = Footprint(darkA, darkB);

            SetLampsEnabled(lamps, true);
            yield return null; yield return null;
            byte[] fitting = Capture();
            SavePlate("03-pier-noon-control.png", fitting);

            SetBloomToReach(lamps);
            yield return null; yield return null;
            byte[] pool = Capture();

            int emitted = Footprint(darkB, fitting);
            int betweenArms = Footprint(pool, fitting);
            Color tint = Shader.GetGlobalColor("_DayNightTint");
            Debug.Log($"[{PlateDir}] noon (tint {tint.r:0.00},{tint.g:0.00},{tint.b:0.00}, darkness " +
                      $"{Darkness(tint):0.000}): lamps-on changed {emitted} px against a {noiseFloor} px " +
                      $"lamps-off floor; the two BLOOM ARMS differ by {betweenArms} px ({_w}x{_h})");

            // ⭐ THE CLAIM THIS PR HAS TO MAKE BY DAY is that shrinking a bloom changes nothing, and that is
            // asserted between the two ARMS — not against darkness. The gate's own arithmetic is pinned
            // exactly in LightMathTests; what a whole-frame control can add is that the ruling is invisible
            // at the hour nobody should see a lamp. Arm-to-arm is also immune to the one thing this fixture
            // cannot pin tightly: the exact tint the controller has eased to by the time it is frozen.
            Assert.LessOrEqual(betweenArms, Mathf.Max(noiseFloor, 2),
                $"the 3.6 m pool and the 0.40 m lantern draw DIFFERENTLY at noon ({betweenArms} px). A bloom " +
                "the day/night gate has switched off has no size, so if these two arms differ, something is " +
                "drawing that should not be.");

            // And the softer statement, reported and bounded rather than asserted at zero: at whatever
            // daylight tint the controller settled on, a lamp is doing essentially nothing.
            Assert.Less(emitted, _w * _h / 1000,
                $"lamps changed {emitted} px of {_w * _h} at a daylight tint of darkness " +
                $"{Darkness(tint):0.000}. The gate is in the shared additive shader and reads the published " +
                "tint — a lamp that lights the wharf at noon is a bigger problem than any of this.");
        }

        // =============================================================================================
        //  2 — the searchlight: the quad stops laying a flat wedge over the water's own relief
        // =============================================================================================

        /// <summary>
        /// <b>The beam over water, both arms.</b> The owner's sentence — <i>"it's just a flat white"</i> —
        /// names a measurable property, and it is not brightness: it is FLATNESS. #691 lights the sea by
        /// N·L against the wave field's own normal, so crests catch the beam and troughs fall into shadow,
        /// and a full-length additive quad then adds the same light again with no relief in it. The flat
        /// copy is the brighter one, so the sea under the beam loses its shape.
        ///
        /// <para>So the measurement is the VARIANCE of luminance inside the lit region. A wedge of flat
        /// cream has almost none; water with relief in it has a great deal. Both arms are the same frame,
        /// the same sea, the same second — only <c>_quadGlowScale</c> moves.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheBeamOverWater_ReadsTheWavesInsteadOfWashingThemFlat()
        {
            yield return LoadTheWharf();
            yield return SetNight(2f);

            if (!TryOpenWaterOffThePier(out Vector2 water, out float depth))
            {
                Assert.Ignore("SKIPPED — no water deep enough off the pier to float a beam over, so the " +
                              "plate would be a beam on a beach.");
                yield break;
            }
            GameObject boat = SpotlightOverWater(water);
            var spot = boat.GetComponent<BoatSpotlight>();
            // Frame the middle of the throw, not the lamp: a beam photographed from its own origin is
            // mostly off the bottom of the plate.
            yield return FrameOn(water + new Vector2(0f, -BoatSpotlight.DefaultRangeMetres * 0.5f));

            // ⚠ NOTHING IS SWITCHED OFF HERE, AND THAT IS DELIBERATE. Two earlier shapes of this test both
            // measured nothing: disabling the BoatSpotlight left the SceneLight it configured still drawing
            // (so the "dark" arm contained the beam and both arms matched it exactly), and disabling the
            // SceneLight as well left the beam gone for good — a light that has been switched off and on
            // again inside a frozen frame does not come back, because the component that re-pushes its
            // shape only ticks on engine time. So the reference arm is not darkness: it is THE OTHER VALUE
            // OF THE DIAL. The mask below is the region where the two arms differ, which is exactly the
            // stretch of sea the full-length quad covers and the source glow does not — the pixels the
            // owner was looking at, defined by the change itself rather than by an assumption.
            SetQuadGlowScale(spot, 1f);
            yield return null; yield return null;
            byte[] flat = Capture();
            SavePlate("04-beam-0200-fullquad-BEFORE.png", flat);

            SetQuadGlowScale(spot, BoatSpotlight.DefaultQuadGlowScale);
            yield return null; yield return null;
            byte[] relief = Capture();
            SavePlate("05-beam-0200-sourceglow-AFTER.png", relief);

            bool[] mask = LitMask(relief, flat, out int lit);
            float flatVar = RelativeLocalContrast(flat, mask);
            float reliefVar = RelativeLocalContrast(relief, mask);

            Debug.Log($"[{PlateDir}] beam over {depth:0.0} m of water: the dial moves {lit} px  |  " +
                      $"relative local contrast there: full quad {flatVar:0.0000} -> source glow " +
                      $"{reliefVar:0.0000} ({reliefVar / Mathf.Max(flatVar, 1e-6f):0.00}x)");

            Assert.Greater(lit, 1000,
                $"the dial moved only {lit} px, so there is no wedge here to judge: either the beam is not " +
                "drawing in this fixture or the quad length is not reaching the frame, and a contrast " +
                "comparison over nothing would pass on anything");

            Assert.Greater(reliefVar, flatVar,
                $"the sea under the beam is no less washed out than it was ({reliefVar:0.0000} against " +
                $"{flatVar:0.0000}). Pulling the quad back is supposed to UNCOVER the water's own relief " +
                "term; if the contrast did not rise, either the relief is not running or the quad was " +
                "never what was hiding it.");
        }

        // =============================================================================================
        //  scaffolding
        // =============================================================================================

        /// <summary>
        /// ⚠️ <b>CI runs on the Null device and this fixture PHOTOGRAPHS things.</b> Every assertion here
        /// counts pixels, so without a GPU there is nothing to count and the numbers would be a confident
        /// zero — which would redden CI while proving nothing. Skips loudly, the way every other render
        /// fixture in this repo does.
        /// </summary>
        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device (Null Device), so nothing rendered " +
                              "and nothing was proved. Expected on CI; a plate of a lamp needs a GPU.");
        }

        IEnumerator LoadTheWharf()
        {
            RequireAGraphicsDevice();

            // The region logs decor-import complaints that have nothing to do with lights.
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;   // let the self-installing components register
        }

        /// <summary>
        /// Seek to an hour, let the frame CATCH UP to it, and only then stop the world.
        ///
        /// <para><b>⚠ THE ORDER OF THOSE THREE THINGS IS THE WHOLE TRAP, and a fixed frame count is not
        /// good enough.</b> <c>Clock.TimeScale = 0</c> holds the sun; it does not hold the WATER, which
        /// animates on engine time and moved 639,757 pixels of a 1,080,000-pixel plate between two
        /// IDENTICAL frames — a noise floor that swallows anything a lamp does. So engine time stops too.
        /// But <c>DayNightController</c> EASES its tint toward the hour rather than snapping to it, and it
        /// eases on engine time: freezing after a fixed twelve frames left the 02:00 beam plate wearing
        /// (0.783, 0.815, 0.844) — full daylight, the hour of the test BEFORE it in the run — and a
        /// night-gated lamp over a daylit sea emits exactly nothing, which is what the plate showed. So
        /// this WAITS FOR THE TINT TO SETTLE and says which tint it settled on. A plate that cannot name
        /// its own night is not evidence about lights.</para>
        /// </summary>
        IEnumerator SetNight(float hour)
        {
            if (GameServices.Clock == null || GameServices.Config == null)
            {
                Assert.Ignore("SKIPPED — the region registered no clock, so the night cannot be pinned and " +
                              "a plate of it would be a plate of whatever hour the run happened to be in.");
                yield break;
            }
            Time.timeScale = 1f;
            double spd = GameServices.Config.SecondsPerDay;
            GameServices.Clock.SeekTo((1.0 + hour / 24.0) * spd);

            // ⚠ THE CLOCK KEEPS RUNNING UNTIL THE TINT HAS FOLLOWED IT. DayNightController publishes on
            // init and thereafter follows a MOVING clock, so seeking and stopping the clock in the same
            // breath leaves the frame wearing whatever hour the scene loaded at: the 02:00 beam plate came
            // back at (0.783, 0.815, 0.844) — full noon, the hour the test before it had left behind — and
            // a night-gated lamp over a daylit sea emits exactly nothing, which is precisely what that
            // plate showed. A few frames of a running clock is a fifth of a game minute; it costs nothing
            // and it is the difference between a plate and a wrong plate.
            Color tint = Shader.GetGlobalColor("_DayNightTint");
            // Settled AND arrived: a tint that has not moved for four frames has either finished easing or
            // never started, and only a second question can tell those apart — is the lights' own night
            // GATE in the state this hour calls for? That question is asked of LightMath, the very function
            // the additive shader mirrors, so it is the project agreeing with itself rather than a night
            // typed into a test.
            bool wantLamps = hour < 5f || hour > 20f;
            int still = 0, frames = 0;
            while (frames < 900)
            {
                yield return null;
                frames++;
                Color now = Shader.GetGlobalColor("_DayNightTint");
                float move = Mathf.Abs(now.r - tint.r) + Mathf.Abs(now.g - tint.g) + Mathf.Abs(now.b - tint.b);
                still = move < 1e-4f ? still + 1 : 0;
                tint = now;
                if (still >= 4 && LampsAreGatedOn(tint) == wantLamps) break;
            }

            GameServices.Clock.TimeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;

            Debug.Log($"[{PlateDir}] asked for {hour:0.00} h; clock reads " +
                      $"{GameServices.Clock.HourOfDay:0.00} h; tint settled after {frames} frames at " +
                      $"({tint.r:0.000}, {tint.g:0.000}, {tint.b:0.000})");

            Assert.Less(frames, 900,
                $"the day/night tint never reached {hour:0.00} h — after 900 frames the frame's darkness is " +
                $"{Darkness(tint):0.000} and the lamps are gated {(LampsAreGatedOn(tint) ? "ON" : "OFF")} " +
                $"where this hour wants them {(wantLamps ? "ON" : "OFF")}. Every plate below would be of " +
                "some other time of day.");
        }

        /// <summary>How dark the frame is, 0 (noon) .. 1 (pitch): the same one-number reading of the
        /// published tint the additive lights' own night gate takes.</summary>
        static float Darkness(Color tint) => 1f - (0.299f * tint.r + 0.587f * tint.g + 0.114f * tint.b);

        /// <summary>Is the additive lights' night gate open at this tint? Asked of <see cref="LightMath"/>
        /// at <see cref="SceneLight"/>'s own shipped threshold and softness — the function the shader
        /// mirrors — so the fixture never has to decide for itself what counts as night.</summary>
        static bool LampsAreGatedOn(Color tint)
        {
            float luminance = 0.299f * tint.r + 0.587f * tint.g + 0.114f * tint.b;
            return LightMath.NightGate(luminance, 0.12f, 0.35f) > 0.5f;
        }

        /// <summary>The pier's lamps, placed by the BUILDER — same call the region's Build makes.</summary>
        GameObject PlaceLamps(IReadOnlyList<LampPosts.Site> sites)
        {
            var host = new GameObject("PlateLamps");
            _spawned.Add(host);
            int placed = LampPosts.Place(host.transform, sites, null, 0f, "[lights-are-sources]");
            Assert.AreEqual(sites.Count, placed,
                "the builder declined to place a lamp, so this fixture is photographing a pier the game " +
                "does not have");

            // A deterministic flicker is still a flicker: two arms shot a frame apart would differ by it,
            // and the difference would be read as the change under test.
            foreach (SceneLight l in host.GetComponentsInChildren<SceneLight>(true)) l.FlickerAmount = 0f;
            return host;
        }

        static void SetLampsEnabled(GameObject host, bool on)
        {
            foreach (SceneLight l in host.GetComponentsInChildren<SceneLight>(true)) l.enabled = on;
        }

        /// <summary>Put every placed lamp's bloom back to the POOL — the BEFORE arm, in this build.</summary>
        static float[] SetBloomToReach(GameObject host)
        {
            var pres = host.GetComponentsInChildren<PreconfiguredLight>(true);
            var restored = new float[pres.Length];
            for (int i = 0; i < pres.Length; i++)
            {
                SceneLight l = pres[i].GetComponent<SceneLight>();
                // ⚠ Zero the CARRIED fitting first: the component re-stamps its preset whenever it is
                // touched, so a Range written on its own would be stamped straight back over.
                pres[i].FittingWidthMetres = 0f;
                restored[i] = LightPresets.ReachMetres(pres[i].Preset);
                if (l != null) { l.Range = restored[i]; l.FlickerAmount = 0f; }
            }
            return restored;
        }

        /// <summary>
        /// Move the dial and let the component apply it. <c>BoatSpotlight.Update</c> re-writes
        /// <c>Light.Range = Range × quadGlowScale</c> every frame and <c>SceneLight.LateUpdate</c> re-poses
        /// the quad every frame, so a frame is all this needs — and crucially it needs no enable/disable,
        /// which inside a frozen frame is a one-way trip for a light.
        /// </summary>
        static void SetQuadGlowScale(BoatSpotlight spot, float scale)
        {
            var f = typeof(BoatSpotlight).GetField("_quadGlowScale",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(f, "BoatSpotlight._quadGlowScale is the dial the owner's ruling lands on");
            f.SetValue(spot, scale);
            if (spot.Light != null) spot.Light.FlickerAmount = 0f;
        }

        /// <summary>
        /// Sea room off the pier's mooring face — the DEEPEST point on a walk out from the wharf, asked of
        /// the region's own terrain rather than typed.
        ///
        /// <para>⚠ A typed offset put the first attempt on the BEACH: 6.75 m south of the deck is shingle
        /// at St Peters, and the plate came back a picture of rocks and grass with no beam in it at all.
        /// A beam plate has to be over water, and only the terrain knows where that is.</para>
        /// </summary>
        static bool TryOpenWaterOffThePier(out Vector2 water, out float depth)
        {
            water = default; depth = 0f;
            ITidalTerrain terrain = GameServices.TidalTerrain;
            if (terrain == null) return false;

            Rect deck = StPetersWharf.DeckFootprint();
            float best = 0f;
            for (float d = 4f; d <= 80f; d += 1f)
            {
                var p = new Vector2(deck.center.x, deck.yMin - d);
                float e = terrain.ElevationAt(p);
                if (e < best) { best = e; water = p; }
            }
            // Deep enough that the whole throw is over water and not licking a shoreline.
            depth = -best;
            return depth > 2f;
        }

        /// <summary>A lamp on the water, aimed along the pier's own axis. It is a bare
        /// <see cref="BoatSpotlight"/> rather than a hull because the thing under test is the QUAD over the
        /// sea, and a hull would put its own mesh, wake and lamps into the frame being measured.</summary>
        GameObject SpotlightOverWater(Vector2 at)
        {
            var go = new GameObject("PlateSpotlight");
            _spawned.Add(go);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, 180f);   // throw south, out to sea
            var spot = go.AddComponent<BoatSpotlight>();
            spot.KeyTogglesBeam = false;
            spot.SetBeam(true);

            // ⚠ AND STOP HER DIMMING, or the plate is of a beam that is off. The searchlight fades toward
            // its floor when the boat is not making way — right, and the reason the fleet's lamps read as
            // working lights — but this fixture has frozen engine time to hold the sea still, so the
            // measured speed is zero for ever and she reads as moored. Measured: 0 lit pixels with this
            // line missing, against 5,055 with the sea running. The dial under test is the QUAD's length,
            // and a way-gate that cannot be satisfied is not part of the question.
            var dim = typeof(BoatSpotlight).GetField("_dimWhenStationary",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(dim, "BoatSpotlight._dimWhenStationary is what makes a still beam invisible here");
            dim.SetValue(spot, false);
            return go;
        }

        /// <summary>
        /// Park the game's own camera on a place and take the game's own framing.
        ///
        /// <para>⚠ <c>Smooth = 0</c> means NEVER MOVE, not move instantly — the follow lerps by
        /// <c>1 - exp(-Smooth·dt)</c>, so zero is a factor of zero. And <c>SetFraming</c> does not stick:
        /// the zoom policy re-asserts <c>orthographicSize</c> every LateUpdate. So: retarget the follow at
        /// an anchor, wind the smoothing right up, and accept the framing the game chooses — which is what
        /// "at the shipped exposure" means anyway.</para>
        ///
        /// <para><b>⚠⚠ AND ENGINE TIME STOPS HERE, AFTER THE CAMERA HAS ARRIVED — never before it.</b>
        /// <c>DayNightController</c> fits its whole-frame multiply to the camera every LateUpdate, so a
        /// camera that moves while time is frozen leaves the night behind: the beam plate came back as a
        /// daylit sea with a rectangle of night sitting over the pier the camera had just left, which reads
        /// exactly like a lighting bug and is a fixture bug. Frame first, then stop the world.</para>
        /// </summary>
        IEnumerator FrameOn(Vector2 at)
        {
            var follow = Object.FindFirstObjectByType<CameraFollow>();
            if (follow == null)
            {
                Assert.Ignore("SKIPPED — the region has no CameraFollow, so the plate would be framed by " +
                              "this fixture rather than by the game.");
                yield break;
            }

            var anchor = new GameObject("PlateAnchor");
            _spawned.Add(anchor);
            anchor.transform.position = new Vector3(at.x, at.y, 0f);
            follow.Target = anchor.transform;
            follow.Smooth = 1000f;

            // ⚠ WAIT FOR THE ZOOM, not just for the pan. CameraFollow's framing policy re-asserts
            // orthographicSize every LateUpdate and tweens it, and DayNightController fits its whole-frame
            // multiply to orthographicSize × aspect — so freezing while the zoom is still moving leaves the
            // night as a RECTANGLE INSET IN THE PLATE with daylight showing round it. That is what a beam
            // plate came back as, and it reads exactly like a lighting bug.
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "no main camera — the overlay and every light quad are pinned to it, so " +
                                  "there is nothing to photograph the night with");
            Vector3 lastPos = cam.transform.position;
            float lastSize = cam.orthographicSize;
            int still = 0, frames = 0;
            while (still < 5 && frames < 400)
            {
                yield return null;
                frames++;
                bool moved = (cam.transform.position - lastPos).sqrMagnitude > 1e-8f
                             || Mathf.Abs(cam.orthographicSize - lastSize) > 1e-5f;
                still = moved ? 0 : still + 1;
                lastPos = cam.transform.position;
                lastSize = cam.orthographicSize;
            }
            Assert.Less(frames, 400, "the camera never stopped moving, so the night overlay it is fitted to " +
                                     "would be a frame behind every plate below");

            _cam = cam;
            _h = PlateHeightPx;
            _w = Mathf.RoundToInt(_h * _cam.aspect);
            _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf);
            _rt.Create();

            // ⚠⚠ HAND THE CAMERA ITS TARGET **BEFORE** THE WORLD STOPS, and let it live there.
            // DayNightController fits its whole-frame multiply to orthographicSize × aspect every
            // LateUpdate — and a camera's aspect CHANGES the moment a render texture is attached. Attaching
            // it inside the capture, after time was frozen, meant the overlay stayed fitted to the game
            // view's aspect and the plate came back with the night as a rectangle inset in a daylit sea:
            // the same picture a broken gate would give, from a fixture that broke it. Attach, let the
            // overlay refit to the plate's own aspect, and only then stop the world.
            _cam.targetTexture = _rt;
            for (int i = 0; i < 6; i++) yield return null;

            // NOW stop the sea. The game clock is already held (SetNight); this holds the swell, which
            // animates on engine time and otherwise moves 59 % of the plate between two identical frames —
            // a noise floor that swallows anything a lamp does.
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
        }

        /// <summary>Render <c>Camera.main</c> as the player sees it and read it back, gamma-corrected (the
        /// project is LINEAR, so a raw float read-back saves far too dark).</summary>
        byte[] Capture()
        {
            // The camera already lives on _rt (see FrameOn) — swapping it here would move the aspect the
            // night overlay is fitted to, in a frame that can no longer refit it.
            _cam.Render();

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_w, _h, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, _w, _h), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            Color[] px = tex.GetPixels();
            var outBytes = new byte[px.Length * 4];
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b); c.a = 1f;
                c = c.gamma;
                outBytes[i * 4 + 0] = (byte)Mathf.RoundToInt(c.r * 255f);
                outBytes[i * 4 + 1] = (byte)Mathf.RoundToInt(c.g * 255f);
                outBytes[i * 4 + 2] = (byte)Mathf.RoundToInt(c.b * 255f);
                outBytes[i * 4 + 3] = 255;
            }
            Object.DestroyImmediate(tex);
            return outBytes;
        }

        /// <summary>How many pixels the lamps brightened, against the same frame with them off. It counts
        /// AREA, not brightness, because "just a round glow" is a statement about area.</summary>
        static int Footprint(byte[] dark, byte[] lit)
        {
            int n = 0;
            for (int i = 0; i + 3 < dark.Length; i += 4)
            {
                int d = (lit[i] - dark[i]) + (lit[i + 1] - dark[i + 1]) + (lit[i + 2] - dark[i + 2]);
                if (d > LitThreshold) n++;
            }
            return n;
        }

        /// <summary>The same, as a mask, so the second arm can be judged on the FIRST arm's pixels.</summary>
        static bool[] LitMask(byte[] dark, byte[] lit, out int count)
        {
            var mask = new bool[dark.Length / 4];
            count = 0;
            for (int i = 0, p = 0; i + 3 < dark.Length; i += 4, p++)
            {
                int d = (lit[i] - dark[i]) + (lit[i + 1] - dark[i + 1]) + (lit[i + 2] - dark[i + 2]);
                if (d > LitThreshold) { mask[p] = true; count++; }
            }
            return mask;
        }

        /// <summary>Pixels blown out to cream — where a disc stops being light and starts being an eraser.</summary>
        static int Saturated(byte[] frame)
        {
            int n = 0;
            for (int i = 0; i + 3 < frame.Length; i += 4)
                if (frame[i] >= SaturatedChannel && frame[i + 1] >= SaturatedChannel &&
                    frame[i + 2] >= SaturatedChannel) n++;
            return n;
        }

        /// <summary>
        /// <b>Relative local contrast over a mask</b> — mean neighbour-to-neighbour luminance step divided
        /// by mean luminance. What "washed out" means, as a number.
        ///
        /// <para><b>⚠ It has to be LOCAL, and it has to be RELATIVE, and a first pass got both wrong.</b>
        /// Plain variance over the region said the DISC was the more detailed picture (53.2 against the
        /// lantern's 1.3) and the flat wedge the more detailed sea (0.53 against 0.42) — because a big
        /// smooth gradient has enormous variance and no detail at all. Variance was measuring the very
        /// blob under review. Neighbour steps ignore a smooth gradient and count the planks, the bollards
        /// and the wave relief; dividing by the mean is what makes ADDING a flat sheet of light register as
        /// a LOSS, which is exactly the owner's "it's just a flat white": the mean goes up, the structure
        /// does not, and the picture washes out.</para>
        /// </summary>
        float RelativeLocalContrast(byte[] frame, bool[] mask)
        {
            double stepSum = 0, lumaSum = 0; int n = 0;
            for (int y = 0; y < _h - 1; y++)
            {
                for (int x = 0; x < _w - 1; x++)
                {
                    int p = y * _w + x;
                    if (!mask[p]) continue;
                    double c = Luma(frame, p);
                    stepSum += System.Math.Abs(Luma(frame, p + 1) - c)
                             + System.Math.Abs(Luma(frame, p + _w) - c);
                    lumaSum += c;
                    n++;
                }
            }
            if (n == 0 || lumaSum <= 0) return 0f;
            return (float)(stepSum / lumaSum);
        }

        static double Luma(byte[] frame, int pixel)
        {
            int i = pixel * 4;
            return (0.299 * frame[i] + 0.587 * frame[i + 1] + 0.114 * frame[i + 2]) / 255.0;
        }

        void SavePlate(string name, byte[] rgbaBottomLeft)
        {
            var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgbaBottomLeft);
            tex.Apply();
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[{PlateDir}] plate written: {path}");
        }
    }
}
