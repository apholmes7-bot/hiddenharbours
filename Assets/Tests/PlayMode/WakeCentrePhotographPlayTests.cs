using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A PHOTOGRAPH OF THE FOAM, NOT A DIAGRAM OF THE ARITHMETIC.</b>
    ///
    /// <para><b>The owner, 2026-09-11:</b> <i>"the wake still is not centered behind the boat, the foam is
    /// off centre"</i> — his SECOND report of the same thing. The first (2026-09-06) became register row 29
    /// and PR #768, which unified the three publishers' ROOT, PROJECTION and WIDTH into
    /// <see cref="WakeRootMath"/>. The complaint survived that merge, so either the fix never reached the
    /// pixels or the cause was never among the three things #768 unified.
    ///
    /// <para><b>Why this fixture exists at all.</b> #768's evidence sheet says of itself that it is "a
    /// diagram of the shipped arithmetic on shipped data, explicitly NOT a photograph of rendered foam".
    /// Two photographs were declared owed and never shot. Everything about this defect that has been
    /// argued has been argued from arithmetic; nothing has been argued from pixels. So this class takes the
    /// picture, under the charter's rule: <b>you may not propose a cause before the photograph exists.</b>
    /// It therefore asserts that the picture is a picture OF THE SUBJECT — hull in frame, hull actually
    /// drawn, each family actually drawn, the track non-degenerate — and REPORTS the measured number. It
    /// deliberately carries no centring bar: a bar is a claim about where the foam ought to be, and that
    /// claim is exactly what the plate is being taken to settle.</para>
    ///
    /// <para><b>The measured number</b> is, per heading and per foam family, the LATERAL OFFSET IN WORLD
    /// METRES of that family's DRAWN centroid from the transom's swept track — signed, positive to PORT of
    /// the direction of travel. World metres because the charter forbids ruling on post-grade absolutes:
    /// the plate is shot through the game's own camera wearing the game's own grade, so its BRIGHTNESSES
    /// are not evidence, but its GEOMETRY is — <see cref="Camera.ScreenToWorldPoint"/> inverts the very
    /// projection the plate was taken through, exactly.</para>
    ///
    /// <para><b>The three families are separated by their own seams, never by a debug shader</b> — a red
    /// debug sheet is blind on pale water, so this differences the SHIPPED look against itself instead.
    /// <list type="bullet">
    /// <item><b>A — the broad advected sheet</b>, drawn by the water shader out of the foam buffer that
    /// <see cref="FoamInjector"/> fills. Killed by disabling the injector — it unregisters in
    /// <c>OnDisable</c> — AND zeroing <see cref="FoamInjectionRegistry.PublishLookStrength"/> in the same
    /// breath as the render. Belt and braces, because <c>ShouldRun</c> also answers to the shore's deposit
    /// dial, and a buffer left running keeps drawing the history this boat already wrote into it.</item>
    /// <item><b>B — the banded sprite deposits</b>: the pooled <c>foam</c> renderers under
    /// <c>Wake[boat]</c>.</item>
    /// <item><b>C — the fine hatched crest dashes</b>: the pooled <c>crest</c> renderers under the same
    /// root.</item>
    /// </list>
    /// B and C are isolated by their rig's OWN NAMING — the seam <c>WakeDepositPlayTests</c> already counts
    /// through — with the emitter component disabled first so its tick cannot put back what this switched
    /// off.</para>
    ///
    /// <para><b>Every arm is shot in ONE frozen frame.</b> Engine time is stopped before the first render,
    /// so the sea does not move between arms, the buffer does not age, and its injection queue is already
    /// drained: the arms then differ ONLY in what this fixture switched off, which is the entire basis of
    /// differencing them. A frame apart they would also differ by a moving sea — a noise floor the lights
    /// fixture measured at 639,757 pixels on a running clock and at ZERO on a stopped one.</para>
    ///
    /// <para><b>CI has no GPU</b>, so this class self-skips BY NAME with its reason attached
    /// (<see cref="RequireAGraphicsDevice"/>, called FIRST, before any yield — after a yield the case
    /// records as FAILED with the skip text attached, which is how a skip turns a PR red).</para>
    /// </summary>
    public class WakeCentrePhotographPlayTests
    {
        // ── the box the picture is taken in ───────────────────────────────────────────────────────────
        const string SceneName = "NineMileCreek";
        const string PlateDir = "wake-photograph";

        /// <summary>The empty scene the teardown parks the player loop on so the region can be unloaded.
        /// Named because the teardown must LOOK IT UP before making it: <c>CreateScene</c> THROWS on a name
        /// that already exists, and this teardown also runs after the cases that skipped.</summary>
        const string CleanupSceneName = "WakeCentrePhotographCleanup";

        const string CapeHullPath = "Assets/_Project/Data/Boats/CapeIslander.asset";
        const string CapeVisualPath = "Assets/_Project/Data/Boats/Visuals/CapeIslanderIso.asset";
        const string DoryHullPath = "Assets/_Project/Data/Boats/Dory.asset";
        const string DoryVisualPath = "Assets/_Project/Data/Boats/Visuals/DoryIso.asset";

        /// <summary>Plate height in pixels; the WIDTH comes off the camera's own aspect and is never
        /// chosen — the day/night multiply is fitted to <c>orthographicSize × aspect</c> every LateUpdate,
        /// so a plate shot at any other aspect wears the hour as a rectangle in the middle of the frame.
        /// </summary>
        // ⚠ Sized for the PULLED-BACK frame, not for the shipped zoom. A 4 s leg is ~24 m of track and
        // the frame that holds it is ~55 m tall; at 900 px that is 16 px per metre, half the game's own
        // 32. 1600 px puts it back near the density the art is authored at, and the width that follows
        // from the camera's aspect stays inside what a render texture will take.
        const int PlateHeightPx = 1600;

        /// <summary>The hour the picture is taken at. The owner's screenshot is a night one; the GEOMETRY
        /// under test is hour-independent (it is arithmetic on transforms), and daylight is the hour that
        /// leaves the most headroom between white foam and the sea it is drawn on. The hour is printed
        /// beside every plate so nobody has to guess which one it was.</summary>
        const float ShotHour = 11f;

        /// <summary>How long each leg runs before the shutter. Long enough at <see cref="DriveSpeed"/> to
        /// lay a wake several hull-lengths long: a straight-run centroid is not a turning one (#741
        /// measured 2.11 m straight against 1.19 m turning), so a leg that barely got under way would be
        /// measuring the first puff rather than the wake.</summary>
        const float DriveSeconds = 4f;

        const float DriveSpeed = 6f;

        /// <summary>The turn leg's rate — a SUSTAINED turn, which is the case the owner photographs. The
        /// transom and the origin trace different arcs through one, so a family rooted on the wrong one
        /// separates visibly rather than only arithmetically.</summary>
        const float TurnRateDegPerSec = 22f;

        /// <summary>The legs are laid out this far apart so one leg's wake is never in the next leg's
        /// frame: the foam buffer is a WORLD-anchored sheet that outlives the boat that filled it, and a
        /// stale band inside the crop would be counted as this leg's foam.</summary>
        const float LegSpacingMetres = 140f;

        readonly List<Object> _spawned = new List<Object>();
        readonly List<string> _rows = new List<string>();

        Camera _cam;
        // Parked for the plate, put back in teardown. A LIST, because the camera this fixture
        // photographs through is the persistent rig in DontDestroyOnLoad and the loaded region brings
        // its own: parking the first one found left the other still writing the size.
        readonly List<CameraFollow> _parked = new List<CameraFollow>();
        readonly List<bool> _parkedWas = new List<bool>();
        Behaviour _parkedPpc;        // the PixelPerfectCamera, ditto
        bool _parkedPpcWas;
        RenderTexture _rt;
        int _w, _h;
        BoatWakeEmitter _emitter;

        // ── teardown ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// ⚠️ PUT THE WORLD BACK. This fixture loads a REAL region single-mode, freezes a STATIC
        /// (<c>Time.timeScale</c>), stops the game clock, disables the persistent wake emitter and hangs a
        /// render texture off the game camera. A PlayMode run shares one player loop; every one of those
        /// leaks into the next class.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            LogAssert.ignoreFailingMessages = false;

            // ⚠ FLUSH WHAT WAS ALREADY MEASURED BEFORE THE THROW. A leg that reddens never reaches its
            // own WriteMeasurements, so every number it had taken up to that point died with it and the
            // next run had to be re-instrumented to see them — which is the expensive way to learn what
            // the red run already knew. A partial file is exactly what a red run is FOR.
            if (_rows.Count > 0)
                WriteMeasurements("PARTIAL-" + TestContext.CurrentContext.Test.Name);

            Time.timeScale = 1f;   // ⚠ a STATIC: left at 0 it stops every test that follows
            if (GameServices.Clock != null) GameServices.Clock.TimeScale = 1f;
            if (_emitter != null) { _emitter.enabled = true; _emitter = null; }
            RestoreLiftDials();     // the dials this class wrote on the RUNTIME water materials
            RestoreLiftGlobals();   // and the real sea back, if a sabotage arm doctored the globals

            for (int i = 0; i < _parked.Count; i++)
                if (_parked[i] != null) _parked[i].enabled = _parkedWas[i];
            _parked.Clear();
            _parkedWas.Clear();
            if (_parkedPpc != null) { _parkedPpc.enabled = _parkedPpcWas; _parkedPpc = null; }
            if (_cam != null) { _cam.targetTexture = null; _cam = null; }
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();

            // ⚠️⚠️ LOOK THE CLEANUP SCENE UP BEFORE CREATING IT. `CreateScene` THROWS on a name that already
            // exists, and this teardown runs after every case INCLUDING the ones that skip before ever
            // loading a region — which on CI is both of them. A throwing teardown would turn a fixture that
            // should cost CI nothing into two FAILED cases with the skip text attached.
            Scene clean = SceneManager.GetSceneByName(CleanupSceneName);
            if (!clean.IsValid() || !clean.isLoaded) clean = SceneManager.CreateScene(CleanupSceneName);
            if (clean.IsValid()) SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == SceneName)
                    yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        // ── the two subjects ──────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// <b>The cape, drawn as the skinned mesh the owner is actually looking at</b>, through three
        /// headings — due north, due east, and a sustained turn. She is the hull in his screenshot, and her
        /// mesh def is the one carrying a lofted <c>WakeSternOffsetMeters</c>, so she is the case where the
        /// three families' roots have the most room to disagree.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCape_UnderWay_PhotographsWhereEachFoamFamilyIsDrawn()
        {
            RequireAGraphicsDevice();   // FIRST, before any yield — see its own note
            yield return Photograph("cape", CapeHullPath, CapeVisualPath, forceSprite: false);
        }

        /// <summary>
        /// <b>And a SPRITE hull through the same three headings.</b> The whole committed fleet now declares
        /// <c>Variant: Mesh</c>, so the sprite case has to be forced — which is the point of shooting it. A
        /// sprite hull is never fitted with a <see cref="FoamInjector"/> at all (only
        /// <c>IsoFacetHullPresentationService.Install</c> fits one), so she draws families B and C and NO
        /// sheet. If the displacement is in the sprite families it shows here with the buffer out of the
        /// picture entirely; if it lives only in the sheet, she is the control that says so.
        /// </summary>
        [UnityTest]
        public IEnumerator ASpriteHull_UnderWay_PhotographsWhereEachFoamFamilyIsDrawn()
        {
            RequireAGraphicsDevice();   // FIRST, before any yield
            yield return Photograph("sprite", DoryHullPath, DoryVisualPath, forceSprite: true);
        }

        // ── the fixture proper ────────────────────────────────────────────────────────────────────────

        IEnumerator Photograph(string subject, string hullPath, string visualPath, bool forceSprite)
        {
            BoatHullDef hull = LoadAsset<BoatHullDef>(hullPath);
            BoatVisualDef visual = LoadAsset<BoatVisualDef>(visualPath);
            if (hull == null || visual == null) yield break;   // LoadAsset already ignored the case

            yield return LoadRegion();
            yield return SetHour(ShotHour);

            var legs = new[]
            {
                new Leg("north-000", 0f, 0f),
                new Leg("east-090", 90f, 0f),
                new Leg("turn-000-thru-088", 0f, TurnRateDegPerSec),
            };

            if (!TryLegAnchors(legs.Length, out List<Vector2> anchors, out float depth))
            {
                Assert.Ignore("SKIPPED, NOT VERIFIED — no open water was found INSIDE " + SceneName +
                              "'s own camera bounds deep enough to drive a boat round, so there is nothing " +
                              "that can be both sailed and photographed. The region's tidal terrain either " +
                              "did not register, published no bounds, or is dry within a plate of the edge.");
                yield break;
            }
            string where = string.Join("; ", anchors.ConvertAll(a => $"({a.x:0.0}, {a.y:0.0})"));
            Debug.Log($"[{PlateDir}] {subject}: open water at {where} — {depth:0.0} m under her at the " +
                      "shallowest point any leg can reach, at this tide.");

            _rows.Add($"# {subject} — {SceneName}, hour {ShotHour:0.0}, open water {where}, {depth:0.0} m");
            _rows.Add("# heading | family | drawn px | centroid world (x, y) | lateral offset from the " +
                      "swept track, m (+ = to PORT of the course) | that offset over the band's own " +
                      "half-width");

            for (int i = 0; i < legs.Length; i++)
                yield return ShootLeg(subject, hull, visual, forceSprite, legs[i], anchors[i]);

            WriteMeasurements(subject);
        }

        readonly struct Leg
        {
            public readonly string Name;

            /// <summary>Compass degrees, 0 = north, bow = <c>transform.up</c>. The transform's Z euler is
            /// the NEGATIVE of this: a compass turns clockwise and a Unity Z-rotation turns anticlockwise.
            /// </summary>
            public readonly float HeadingDeg;

            public readonly float TurnDegPerSec;

            public Leg(string name, float headingDeg, float turnDegPerSec)
            { Name = name; HeadingDeg = headingDeg; TurnDegPerSec = turnDegPerSec; }
        }

        IEnumerator ShootLeg(string subject, BoatHullDef hull, BoatVisualDef visual, bool forceSprite,
                             Leg leg, Vector2 start)
        {
            // ── the boat, through the production path ────────────────────────────────
            // ONE SETUP, shared with the lift arms below: two photographs of one boat must not be able
            // to disagree about how she was rigged, and the only place that can be guaranteed is one
            // place she is rigged.
            Spawned she = Spawn(subject, hull, visual, forceSprite, leg, start);
            GameObject go = she.Go;
            BoatController boat = she.Boat;
            Rigidbody2D rb = she.Rb;
            BoatHullVariant variant = she.Variant;
            float sternOffset = she.SternOffset;
            float elevationDeg = she.ElevationDeg;
            float bandHalfWidth = she.BandHalfWidth;
            FoamInjector injector = she.Injector;

            // ── frame her, drive her, then stop the world ────────────────────────────────────────────
            // The frame is sized to the leg BEFORE she sails it: her path is a pure function of the leg
            // and this fixture's constants, so the camera can be placed over the finished run rather than
            // chase her through it. Padded by what the wake takes beyond her track — the stern offset she
            // trails it from, and the band she lays either side of it.
            Bounds run = PredictRun(leg, start, 3f * bandHalfWidth + sternOffset + 2f);
            yield return FrameOn(go.transform, run);

            var track = new List<Vector2>(256);      // the transom's swept track — THE reference
            var injRoot = new List<Vector2>(256);    // where the sheet's own root actually went
            yield return Drive(go, rb, leg, track, injRoot, sternOffset, elevationDeg, injector);

            Assert.Greater(track.Count, 8, $"{subject}/{leg.Name}: the leg recorded {track.Count} track " +
                                           "samples — she never got under way, so there is no track to " +
                                           "measure an offset from.");
            Assert.IsFalse(boat.IsAground, $"{subject}/{leg.Name}: she read AGROUND through the leg, and an " +
                                           "aground hull lays no wake at all.");

            // NOW stop the sea. The swell animates on engine time and otherwise moves most of the plate
            // between two otherwise identical frames.
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;

            Transform wakeRoot = FindWakeRoot(go.name);
            Assert.IsNotNull(wakeRoot, $"{subject}/{leg.Name}: no Wake[{go.name}] rig was ever built for " +
                                       "her, so families B and C have nothing drawn to photograph. The " +
                                       "emitter either never found her or never ran.");

            // ── the arms, all inside ONE frozen frame ────────────────────────────────────────────────
            byte[] all = ShootArm(wakeRoot, injector, sheet: true, foam: true, crest: true);
            byte[] armA = ShootArm(wakeRoot, injector, sheet: true, foam: false, crest: false);
            byte[] armB = ShootArm(wakeRoot, injector, sheet: false, foam: true, crest: false);
            byte[] armC = ShootArm(wakeRoot, injector, sheet: false, foam: false, crest: true);
            byte[] bare = ShootArm(wakeRoot, injector, sheet: false, foam: false, crest: false);
            byte[] bareAgain = ShootArm(wakeRoot, injector, sheet: false, foam: false, crest: false);

            // The noise floor is MEASURED, not typed: two renders of the IDENTICAL frozen frame differ only
            // by whatever the pipeline does not repeat exactly. Anything at or under that is not foam.
            float floor = NoiseFloor(bare, bareAgain);

            RectInt crop = CropAround(track, go.transform.position, bandHalfWidth);
            AssertInFrame(subject, leg, go.transform.position, track);

            var marks = new List<(Vector2 world, Color32 colour)>();
            var lines = new List<(List<Vector2> path, Color32 colour)>
            {
                (track, new Color32(60, 255, 120, 255)),     // the transom's swept track — the reference
            };
            if (injRoot.Count > 1) lines.Add((injRoot, new Color32(255, 210, 40, 255)));

            // ⚠️ THE HEADLINE ROW, AND THE CONTROL FOR BOTH. Every family isolated on its own can be
            // measured and still not answer what was asked: the owner is looking at ALL of the foam at
            // once, so the centroid of everything the wake adds to the frame is the number his sentence
            // is about. It doubles as the control on the isolations — if this draws no more pixels than
            // B and C together, then whatever A is doing is not reaching the picture.
            MeasureFamily(subject, leg, "ALL drawn", all, bare, floor, crop, track, bandHalfWidth,
                          new Color32(255, 255, 255, 255), marks, required: true);
            MeasureFamily(subject, leg, "A sheet", armA, bare, floor, crop, track, bandHalfWidth,
                          // ⚠️ NOT REQUIRED, AND THAT IS THE POINT. Whether the advected sheet draws
                          // anything a camera can see is one of the questions this photograph exists to
                          // answer; asserting it must draw would refuse to produce the plate in exactly
                          // the case worth photographing. Its absence is recorded WITH the strongest
                          // difference found in the crop, so "absent" and "faint" stay distinguishable.
                          new Color32(80, 160, 255, 255), marks, required: false);
            MeasureFamily(subject, leg, "B deposits", armB, bare, floor, crop, track, bandHalfWidth,
                          new Color32(255, 90, 90, 255), marks, required: true);
            MeasureFamily(subject, leg, "C crests", armC, bare, floor, crop, track, bandHalfWidth,
                          new Color32(255, 120, 255, 255), marks, required: true);

            // The roots themselves, marked on the plate: her origin, the transom THE ONE ROOT puts under her
            // at the instant of the shutter, and — where she has an injector — the root that component's own
            // transform produces.
            Vector2 originNow = go.transform.position;
            marks.Add((originNow, new Color32(255, 255, 255, 255)));
            marks.Add((WakeRootMath.SternWorld(originNow, go.transform.up, sternOffset, elevationDeg),
                       new Color32(60, 255, 120, 255)));
            if (injector != null)
                marks.Add((WakeRootMath.SternWorld((Vector2)injector.transform.position,
                                                   (Vector2)injector.transform.up, sternOffset, elevationDeg),
                           new Color32(255, 210, 40, 255)));

            SavePlate($"{subject}-{leg.Name}-photo.png", all);
            SavePlate($"{subject}-{leg.Name}-A-sheet.png", armA);
            SavePlate($"{subject}-{leg.Name}-B-deposits.png", armB);
            SavePlate($"{subject}-{leg.Name}-C-crests.png", armC);
            SavePlate($"{subject}-{leg.Name}-bare.png", bare);
            SavePlate($"{subject}-{leg.Name}-marked.png", Annotate(all, lines, marks, crop));

            // SHOT FROM, and THROUGH WHAT. A measurement that cannot name its own frame is not evidence.
            Debug.Log($"[{PlateDir}] {subject}/{leg.Name}: SHOT FROM " +
                      $"({_cam.transform.position.x:0.00}, {_cam.transform.position.y:0.00}), orthographic " +
                      $"size {_cam.orthographicSize:0.00} m, plate {_w}x{_h} px, " +
                      $"{(2f * _cam.orthographicSize / _h):0.0000} m per pixel. THROUGH THE GAME'S OWN " +
                      $"CAMERA at the shipped exposure — the day/night multiply and every post the player " +
                      $"gets are IN this frame (tint {Shader.GetGlobalColor("_DayNightTint")}), which is why " +
                      $"nothing below is ruled on a brightness. Noise floor between two renders of the same " +
                      $"frozen frame: {floor:0.0000} luma. Subject: '{visual.Id}' as {variant}, stern offset " +
                      $"{sternOffset:0.000} m at {elevationDeg:0.0}°, band half-width {bandHalfWidth:0.000} m.");

            // Put her rig back before the next leg — a leg that inherited the last leg's switches would
            // photograph the wrong thing — and let the world run again so the next leg can drive.
            RestoreRig(wakeRoot, injector);
            Time.timeScale = 1f;
            _spawned.Remove(go);
            Object.Destroy(go);
            yield return null;
        }

        // ── driving ───────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Run the leg by SECONDS, never by frames. A headless frame here is well under a millisecond, so
        /// "ninety frames" would drive her a third of a metre and the emitter would barely tick. The pose is
        /// advanced by hand so the controller's own drag cannot slew her off the heading the leg is named
        /// for; the rigidbody velocity is set alongside it because that is the speed the emitter's own gates
        /// read.
        /// </summary>
        IEnumerator Drive(GameObject go, Rigidbody2D rb, Leg leg, List<Vector2> track, List<Vector2> injRoot,
                          float sternOffset, float elevationDeg, FoamInjector injector)
        {
            // ⚠ ONE MOVER. The first cut advanced the transform by hand AND set the body's velocity to the
            // same course, so she was integrated twice and covered ~68 m of a 4 s leg at 6 m/s — the head of
            // her own track left the plate. The body is the mover, as it is in play; this fixture only steers
            // her and sets the speed the emitters read off her.
            //
            // ⚠ AND THE FRAME IS SIZED TO THE LEG, never the leg to the frame. The first cut clipped the
            // run to whatever the shipped zoom could hold, which was 5.3 m — too little driving to lay an
            // advected sheet at all, so the sheet arm photographed an empty buffer. FrameOn parks the
            // camera over the whole predicted path, so the leg runs its full length ON the plate.
            float seconds = DriveSeconds;
            Debug.Log($"[{PlateDir}] {leg.Name}: driving {seconds:0.00} s at {DriveSpeed:0.0} m/s " +
                      $"= {seconds * DriveSpeed:0.0} m, inside a frame {_cam.orthographicSize * 2f:0.0} m tall.");

            // ⚠️ AND THE LEG IS BOUNDED TOO. This loop advances on `Time.deltaTime`, and this very
            // class sets `Time.timeScale = 0` to freeze the sea for the shutter: any path that reaches
            // here with the clock still stopped would spin for ever on dt == 0. Fail, naming the clock.
            float wall = Time.realtimeSinceStartup;
            float heading = leg.HeadingDeg;
            for (float elapsed = 0f; elapsed < seconds; )
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                Assert.Less(Time.realtimeSinceStartup - wall, seconds * 8f + 30f,
                            $"{leg.Name}: the leg reached {elapsed:0.000} s of {seconds:0.0} s after " +
                            $"{Time.realtimeSinceStartup - wall:0.0} s of wall clock — Time.timeScale is " +
                            $"{Time.timeScale:0.000} and Time.deltaTime {dt:0.0000}, so the clock this " +
                            "leg advances on is not running.");

                heading += leg.TurnDegPerSec * dt;
                go.transform.rotation = Quaternion.Euler(0f, 0f, -heading);
                Vector2 course = (Vector2)go.transform.up * DriveSpeed;
                rb.linearVelocity = course;

                // THE REFERENCE, sampled the way production computes it, off the transform that carries her
                // heading — and sampled AFTER the pose is advanced, so the track is the one she drew from.
                track.Add(WakeRootMath.SternWorld((Vector2)go.transform.position, (Vector2)go.transform.up,
                                                  sternOffset, elevationDeg));
                if (injector != null)
                    injRoot.Add(WakeRootMath.SternWorld((Vector2)injector.transform.position,
                                                        (Vector2)injector.transform.up,
                                                        sternOffset, elevationDeg));
                yield return null;
            }
        }

        /// <summary>
        /// Where this leg will go, before she sails it. The path is a pure function of the leg (heading,
        /// turn rate) and this fixture's own constants, so the camera can be placed over the finished run
        /// instead of chasing her through it — which is what photographing a WHOLE wake needs.
        /// <para><c>pad</c> is the room the wake takes beyond her track: she trails it from a stern offset
        /// behind the pose sampled here, and lays it a band's width either side.</para>
        /// </summary>
        static Bounds PredictRun(Leg leg, Vector2 start, float pad)
        {
            Vector2 min = start, max = start, p = start;
            float heading = leg.HeadingDeg;
            const float Step = 0.02f;
            for (float t = 0f; t < DriveSeconds; t += Step)
            {
                heading += leg.TurnDegPerSec * Step;
                float rad = heading * Mathf.Deg2Rad;   // heading 0 = +Y, 90 = +X, as Drive steers her
                p += new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * (DriveSpeed * Step);
                min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            min -= new Vector2(pad, pad);
            max += new Vector2(pad, pad);
            var b = new Bounds();
            b.SetMinMax(new Vector3(min.x, min.y, 0f), new Vector3(max.x, max.y, 0f));
            return b;
        }

        static Transform FindWakeRoot(string boatName)
        {
            var root = GameObject.Find($"Wake[{boatName}]");
            return root != null ? root.transform : null;
        }

        // ── the arms ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// One arm of the comparison: switch the three families on or off through their own seams, then
        /// render. The emitter component goes down first so its tick cannot put a renderer back between the
        /// toggle and the shutter.
        ///
        /// <para>⚠️ The emitter host is <c>HideFlags.HideAndDontSave</c>, which
        /// <see cref="Object.FindFirstObjectByType{T}()"/> does not return — it has to be reached through
        /// <see cref="Resources.FindObjectsOfTypeAll{T}"/>, which is the one lookup that sees hidden
        /// objects.</para>
        /// </summary>
        byte[] ShootArm(Transform wakeRoot, FoamInjector injector, bool sheet, bool foam, bool crest)
        {
            if (_emitter == null)
            {
                var hidden = Resources.FindObjectsOfTypeAll<BoatWakeEmitter>();
                if (hidden != null && hidden.Length > 0) _emitter = hidden[0];
                if (_emitter != null) _emitter.enabled = false;
            }

            if (wakeRoot != null)
            {
                foreach (Transform child in wakeRoot)
                {
                    bool on = child.name == "foam" ? foam
                            : child.name == "crest" ? crest
                            : false;   // plume, spray, droplets, bubbles: never part of the three families
                    var r = child.GetComponent<Renderer>();
                    if (r != null) r.enabled = on && child.gameObject.activeSelf;
                }
            }

            if (injector != null && injector.enabled != sheet) injector.enabled = sheet;

            // ⚠️ AND ZERO THE LOOK DIAL IN THE SAME BREATH AS THE RENDER. Disabling the injector unregisters
            // it, but `ShouldRun` also answers to the shore's deposit dial — with that above zero the foam
            // pass still runs and still draws the marks this boat already laid into the buffer. WaterSurface
            // re-publishes the live material's value every LateUpdate, so this cannot be hoisted out and
            // must be the last thing before Render().
            if (!sheet) FoamInjectionRegistry.PublishLookStrength(0f);
            return Capture();
        }

        void RestoreRig(Transform wakeRoot, FoamInjector injector)
        {
            if (wakeRoot != null)
                foreach (Transform child in wakeRoot)
                {
                    var r = child.GetComponent<Renderer>();
                    if (r != null) r.enabled = child.gameObject.activeSelf;
                }
            if (injector != null) injector.enabled = true;
            if (_emitter != null) { _emitter.enabled = true; _emitter = null; }
        }

        // ── measurement ───────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The measured number, for one family on one heading: the intensity-weighted centroid of the
        /// pixels this family ADDED to the frame, carried back to world metres through the camera's own
        /// inverse, and resolved against the transom's swept track as a signed lateral offset.
        ///
        /// <para><b>Every channel is weighted by its own headroom</b> rather than differenced flat. Foam is
        /// drawn as white onto a sea that is already bright in blue and green: a flat difference is
        /// dominated by whichever channel the sea happens to leave room in, and on pale water a red read is
        /// blind altogether. Weighting each channel by what the BARE frame left unused makes the measure
        /// read the same on a dark sea and a bright one.</para>
        /// </summary>
        void MeasureFamily(string subject, Leg leg, string family, byte[] arm, byte[] bare, float floor,
                           RectInt crop, List<Vector2> track, float bandHalfWidth, Color32 markColour,
                           List<(Vector2, Color32)> marks, bool required)
        {
            double sumW = 0, sumX = 0, sumY = 0;
            int drawn = 0;

            // ⚠️ WHAT THE FAMILY DREW BELOW THE FLOOR IS EVIDENCE TOO. "Nothing drawn" and "drawn, but
            // a hundredth of a luma below the bar" are completely different answers about the same
            // renderer, and only one of them means the family is absent. Both are carried out.
            double sumAll = 0;
            float peak = 0f;
            int examined = 0;
            for (int y = crop.yMin; y < crop.yMax; y++)
            {
                for (int x = crop.xMin; x < crop.xMax; x++)
                {
                    int i = (y * _w + x) * 4;
                    float d = HeadroomDelta(arm, bare, i);
                    examined++;
                    sumAll += d;
                    if (d > peak) peak = d;
                    if (d <= floor) continue;
                    drawn++;
                    sumW += d; sumX += d * (x + 0.5); sumY += d * (y + 0.5);
                }
            }

            if (drawn == 0)
            {
                // A family that draws nothing is a FINDING, not a crash — but only where nothing drawn is a
                // possible answer. A sprite hull is never fitted with an injector, so her empty sheet is the
                // expected result and is recorded as such; a family that SHOULD be drawing and is not means
                // the isolation switched off more than it meant to, and every number below it would be a
                // number about an empty mask.
                string note = $"{leg.Name,-18} | {family,-11} | NOTHING above the measured noise floor " +
                              $"({floor:0.0000} luma); strongest pixel in the crop {peak:0.0000}, mean " +
                              $"{(examined > 0 ? sumAll / examined : 0):0.00000} over {examined} px";
                _rows.Add(note);
                Debug.Log($"[{PlateDir}] {subject} | {note}");
                Assert.IsFalse(required,
                    $"{subject}/{leg.Name}: family '{family}' drew NOTHING above the measured noise floor " +
                    $"({floor:0.0000} luma) anywhere in the crop, and this hull should be drawing it. " +
                    "Either the isolation switched off more than it meant to or this family is not being " +
                    "drawn at all — and an offset measured off an empty mask is a number about nothing.");
                return;
            }

            var px = new Vector2((float)(sumX / sumW), (float)(sumY / sumW));
            Vector2 world = ToWorld(px);
            float offset = SignedLateralOffset(track, world);
            marks.Add((world, markColour));

            string row = string.Format(CultureInfo.InvariantCulture,
                "{0,-18} | {1,-11} | {2,8} | ({3,9:0.00}, {4,9:0.00}) | {5,8:+0.000;-0.000} | {6,7:0.00}",
                leg.Name, family, drawn, world.x, world.y, offset, offset / Mathf.Max(0.01f, bandHalfWidth));
            _rows.Add(row);
            Debug.Log($"[{PlateDir}] {subject} | {row}");
        }

        /// <summary>How much brighter this pixel is than the bare frame, each channel weighted by the
        /// headroom the bare frame left it. Negative deltas are clamped away: foam ADDS light, and a
        /// darkening is the dark lane or a sorting shuffle, not the band whose centre is in question.
        /// </summary>
        static float HeadroomDelta(byte[] arm, byte[] bare, int i)
        {
            float total = 0f, weight = 0f;
            for (int c = 0; c < 3; c++)
            {
                float head = (255f - bare[i + c]) / 255f;
                float d = (arm[i + c] - bare[i + c]) / 255f;
                total += head * Mathf.Max(0f, d);
                weight += head;
            }
            return weight > 1e-4f ? total / weight : 0f;
        }

        /// <summary>The floor under which a difference is the pipeline failing to repeat itself rather than
        /// foam: the largest headroom-weighted delta between two renders of the IDENTICAL frozen frame, with
        /// a margin. Measured every run, because it is a property of the machine and not of the wake.
        /// </summary>
        float NoiseFloor(byte[] a, byte[] b)
        {
            float worst = 0f;
            for (int i = 0; i + 3 < a.Length; i += 4)
            {
                float d = HeadroomDelta(a, b, i);
                if (d > worst) worst = d;
            }
            return Mathf.Max(worst * 1.5f, 0.01f);
        }

        /// <summary>
        /// The signed perpendicular distance in world metres from the transom's swept track to a point.
        /// POSITIVE IS TO PORT of the direction of travel. The track is a polyline — a turn is an arc, not a
        /// line — so this takes the nearest point on the nearest segment and signs the offset by THAT
        /// segment's own heading, which is what "behind the boat" means through a turn.
        /// </summary>
        static float SignedLateralOffset(List<Vector2> track, Vector2 p)
        {
            float best = float.MaxValue, signed = 0f;
            for (int i = 1; i < track.Count; i++)
            {
                Vector2 a = track[i - 1], b = track[i];
                Vector2 ab = b - a;
                float len2 = ab.sqrMagnitude;
                if (len2 < 1e-8f) continue;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                Vector2 q = a + ab * t;
                float d = (p - q).magnitude;
                if (d >= best) continue;
                best = d;
                Vector2 dir = ab / Mathf.Sqrt(len2);
                signed = dir.x * (p.y - q.y) - dir.y * (p.x - q.x);   // cross(dir, p−q): + is to port
            }
            return signed;
        }

        /// <summary>The sea around this leg, in pixels. Cropped because the plate also holds a shoreline, a
        /// wharf and whatever else the region draws, and a family mask that reached them would put a
        /// building's edge into the wake's centroid.</summary>
        RectInt CropAround(List<Vector2> track, Vector2 boat, float bandHalfWidth)
        {
            float minX = boat.x, maxX = boat.x, minY = boat.y, maxY = boat.y;
            foreach (Vector2 t in track)
            {
                minX = Mathf.Min(minX, t.x); maxX = Mathf.Max(maxX, t.x);
                minY = Mathf.Min(minY, t.y); maxY = Mathf.Max(maxY, t.y);
            }
            // A generous lateral allowance — six band half-widths — so a DISPLACED family is never cropped
            // out of its own measurement. A crop that clips the answer manufactures a smaller one.
            float pad = Mathf.Max(6f * bandHalfWidth, 6f);
            Vector3 lo = _cam.WorldToScreenPoint(new Vector3(minX - pad, minY - pad, 0f));
            Vector3 hi = _cam.WorldToScreenPoint(new Vector3(maxX + pad, maxY + pad, 0f));
            int x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(lo.x, hi.x)), 0, _w - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(lo.x, hi.x)), 1, _w);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(lo.y, hi.y)), 0, _h - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(lo.y, hi.y)), 1, _h);
            return new RectInt(x0, y0, Mathf.Max(1, x1 - x0), Mathf.Max(1, y1 - y0));
        }

        /// <summary>ASSERT THE SUBJECT IS IN FRAME. A plate whose boat or whose track has walked off the
        /// edge measures the part of the wake that happened to stay, which is a displacement the fixture
        /// invented rather than one the renderer drew.</summary>
        void AssertInFrame(string subject, Leg leg, Vector2 boat, List<Vector2> track)
        {
            AssertPointInFrame(subject, leg, "the hull", boat);
            AssertPointInFrame(subject, leg, "the head of the swept track", track[0]);
            AssertPointInFrame(subject, leg, "the tail of the swept track", track[track.Count - 1]);
        }

        void AssertPointInFrame(string subject, Leg leg, string what, Vector2 world)
        {
            Vector3 p = _cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            const int Margin = 8;
            Assert.IsTrue(p.x >= Margin && p.x < _w - Margin && p.y >= Margin && p.y < _h - Margin,
                $"{subject}/{leg.Name}: {what} is at world ({world.x:0.0}, {world.y:0.0}), which lands at " +
                $"pixel ({p.x:0}, {p.y:0}) on a {_w}x{_h} plate — OUT OF FRAME. Everything measured below it " +
                "would be about whichever part of the wake happened to stay in the picture. Shorten the leg " +
                "or widen the framing.");
        }

        Vector2 ToWorld(Vector2 pixel)
        {
            // Orthographic or not, ScreenToWorldPoint inverts the projection this plate was taken through,
            // and it reads the camera's own pixel rect — which follows the attached render texture.
            Vector3 w = _cam.ScreenToWorldPoint(new Vector3(pixel.x, pixel.y, -_cam.transform.position.z));
            return new Vector2(w.x, w.y);
        }

        // ── the region, the hour, the water, the camera ───────────────────────────────────────────────

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device (Null Device), so nothing " +
                              "rendered and nothing was proved. Expected on CI; a photograph of foam needs " +
                              "a GPU.");
        }

        static T LoadAsset<T>(string path) where T : ScriptableObject
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                Assert.Ignore($"SKIPPED, NOT VERIFIED — no {typeof(T).Name} at {path}. This is a photograph " +
                              "of the COMMITTED hull, never of a mirror built in memory; without the asset " +
                              "there is no subject.");
            return asset;
#else
            Assert.Ignore("SKIPPED, NOT VERIFIED — needs the AssetDatabase: this photographs the REAL " +
                          "committed hull, never a stand-in.");
            return null;
#endif
        }

        IEnumerator LoadRegion()
        {
            // The region logs decor-import complaints that have nothing to do with wake foam.
            LogAssert.ignoreFailingMessages = true;

            // ⚠️ EVERY WAIT IN A PLATE FIXTURE IS BOUNDED, AND IN WALL CLOCK. A bare
            // `yield return LoadSceneAsync(…)` that never completes hangs the whole run with no output
            // at all — one such stall cost fifteen minutes of a granted editor slot and produced not a
            // single line to diagnose it from. A fixture may fail; it may not hang. The bound is
            // realtime because this class also freezes `Time.timeScale`, and a budget denominated in
            // scaled time is no budget at all.
            Debug.Log($"[{PlateDir}] loading {SceneName}…");
            float wall = Time.realtimeSinceStartup;
            AsyncOperation op = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, $"SceneManager refused to start loading '{SceneName}' — it is most " +
                                 "likely not in Build Settings, and there is no region to photograph.");
            while (op != null && !op.isDone)
            {
                Assert.Less(Time.realtimeSinceStartup - wall, 180f,
                            $"'{SceneName}' never finished loading: {op.progress:0.00} of the way in " +
                            $"after 180 s of wall clock. Nothing below this ever ran.");
                yield return null;
            }
            for (int i = 0; i < 6; i++) yield return null;   // let the self-installing components register
            Debug.Log($"[{PlateDir}] {SceneName} loaded in {Time.realtimeSinceStartup - wall:0.0} s.");
        }

        /// <summary>
        /// Seek to an hour, let the FRAME catch up to it, and only then hold the sun.
        ///
        /// <para>⚠ The clock has to keep RUNNING until the tint has followed it. The day/night controller
        /// publishes on init and thereafter eases toward a MOVING clock, so seeking and stopping in the same
        /// breath leaves the frame wearing whatever hour the scene loaded at — the lights fixture's 02:00
        /// plate came back at full noon that way. A few frames of a running clock costs a fifth of a game
        /// minute and is the difference between a plate and a wrong plate.</para>
        /// </summary>
        IEnumerator SetHour(float hour)
        {
            if (GameServices.Clock == null || GameServices.Config == null)
            {
                Assert.Ignore("SKIPPED, NOT VERIFIED — the region registered no clock, so the hour cannot be " +
                              "pinned and every plate would be of whatever time the run happened to be in.");
                yield break;
            }
            Time.timeScale = 1f;
            double spd = GameServices.Config.SecondsPerDay;
            GameServices.Clock.SeekTo((1.0 + hour / 24.0) * spd);

            Color tint = Shader.GetGlobalColor("_DayNightTint");
            int still = 0, frames = 0;
            while (frames < 900)
            {
                yield return null;
                frames++;
                Color now = Shader.GetGlobalColor("_DayNightTint");
                float move = Mathf.Abs(now.r - tint.r) + Mathf.Abs(now.g - tint.g) + Mathf.Abs(now.b - tint.b);
                still = move < 1e-4f ? still + 1 : 0;
                tint = now;
                if (still >= 4) break;
            }
            GameServices.Clock.TimeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;

            Debug.Log($"[{PlateDir}] asked for {hour:0.00} h; clock reads " +
                      $"{GameServices.Clock.HourOfDay:0.00} h; tint settled after {frames} frames at " +
                      $"({tint.r:0.000}, {tint.g:0.000}, {tint.b:0.000})");
            Assert.Less(frames, 900, "the day/night tint never settled, so every plate below would be of " +
                                     "some other time of day than the one this fixture names.");
        }

        private readonly struct Candidate
        {
            public readonly Vector2 P;
            public readonly float Clearance;
            public Candidate(Vector2 p, float clearance) { P = p; Clearance = clearance; }
        }

        /// <summary>A start for each leg, with enough water round it to run without licking a shoreline —
        /// found by asking the region's OWN tidal terrain rather than by a coordinate typed into a test.
        ///
        /// <para>⚠⚠ WHERE THE WATER IS DEEPEST IS NOT WHERE A PLATE CAN BE SHOT, because the camera is
        /// clamped to the region. <c>CameraFollow.ApplyBounds</c> keeps the VIEW inside
        /// <c>GameServices.CurrentRegionBounds</c>: park a boat outside them and the camera stops at the
        /// edge while she keeps going, so she is photographed leaving her own picture. The first cut of
        /// this search took the DEEPEST water within ±400 m — which for a 760×560 m creek is open sea
        /// outside the region — and both cases duly failed on their own guards, the cape at pixel
        /// y = 10417 on a 900 px plate and the sprite on a camera that never stopped moving. Search
        /// INSIDE the bounds, inset by a plate's worth of framing.</para>
        ///
        /// <para>The legs are no longer strung along +X at <see cref="LegSpacingMetres"/>, which needed
        /// ~280 m of continuous open water in a creek that has not got it. They are chosen independently
        /// and only held APART, so no leg is photographed over another leg's foam.</para></summary>
        static bool TryLegAnchors(int count, out List<Vector2> starts, out float depth)
        {
            starts = new List<Vector2>(); depth = 0f;
            ITidalTerrain terrain = GameServices.TidalTerrain;
            if (terrain == null) return false;

            Rect region = GameServices.CurrentRegionBounds;
            if (region.width <= 1f || region.height <= 1f) return false;   // 0 = no camera has reported

            const float EdgeInsetX = 70f;   // ≈ a plate half-width, so the bounds clamp never bites
            const float EdgeInsetY = 45f;
            float runRadius = DriveSeconds * DriveSpeed + 16f;

            float minX = region.xMin + EdgeInsetX, maxX = region.xMax - EdgeInsetX;
            float minY = region.yMin + EdgeInsetY, maxY = region.yMax - EdgeInsetY;
            if (minX >= maxX || minY >= maxY) return false;

            // Score each candidate by the SHALLOWEST point her run can reach, swept right round her —
            // deep under the keel and dry 20 m on is a grounding, not a leg. She turns, so probe a disc.
            var scored = new List<Candidate>();
            for (float x = minX; x <= maxX; x += 10f)
            {
                for (float y = minY; y <= maxY; y += 10f)
                {
                    var p = new Vector2(x, y);
                    float shallowest = float.MinValue;
                    for (int a = 0; a < 8; a++)
                    {
                        float th = a * Mathf.PI * 0.25f;
                        var dir = new Vector2(Mathf.Cos(th), Mathf.Sin(th));
                        for (float d = 0f; d <= runRadius; d += 10f)
                            shallowest = Mathf.Max(shallowest, terrain.ElevationAt(p + dir * d));
                    }
                    if (-shallowest > 4f) scored.Add(new Candidate(p, -shallowest));
                }
            }
            if (scored.Count == 0) return false;
            scored.Sort((l, r) => r.Clearance.CompareTo(l.Clearance));

            float sep = Mathf.Min(LegSpacingMetres, 0.4f * Mathf.Min(maxX - minX, maxY - minY));
            foreach (Candidate c in scored)
            {
                bool clear = true;
                foreach (Vector2 s in starts)
                    if ((s - c.P).sqrMagnitude < sep * sep) { clear = false; break; }
                if (!clear) continue;

                starts.Add(c.P);
                depth = starts.Count == 1 ? c.Clearance : Mathf.Min(depth, c.Clearance);
                if (starts.Count == count) return true;
            }
            return false;
        }

        /// <summary>
        /// Park the game's OWN camera on her and take the game's OWN framing — the plate has to be what the
        /// player sees, not what a proof camera would have seen.
        ///
        /// <para>⚠ A dedicated camera could not shoot this at all: the foam buffer is kept PER CAMERA
        /// (<c>GetFoamState(camera.GetEntityId())</c>), so a brand-new one starts unprimed and needing a
        /// clear, and would photograph a single frame of injection instead of the accumulated wake.</para>
        ///
        /// <para>⚠ <c>Smooth = 0</c> means NEVER MOVE, not move instantly — the follow lerps by
        /// <c>1 − exp(−Smooth·dt)</c>. ⚠⚠ AND THE RENDER TEXTURE GOES ON BEFORE ANYTHING IS FROZEN: a
        /// camera's aspect changes the moment one is hung on it, and the day/night multiply is refitted to
        /// that aspect every LateUpdate. Attach, let the overlay refit, and only then drive and freeze.
        /// </para>
        /// </summary>
        IEnumerator FrameOn(Transform subject, Bounds run)
        {
            var follow = Object.FindFirstObjectByType<CameraFollow>();
            if (follow == null)
            {
                Assert.Ignore("SKIPPED, NOT VERIFIED — the region has no CameraFollow, so the plate would be " +
                              "framed by this fixture rather than by the game.");
                yield break;
            }
            follow.Target = subject;
            follow.Smooth = 1000f;

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "no main camera — the water composition and the overlay are both pinned to " +
                                  "it, so there is nothing to photograph the sea with.");

            if (_cam != cam || _rt == null)
            {
                if (_cam != null) _cam.targetTexture = null;
                if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); }
                _cam = cam;
                _h = PlateHeightPx;
                _w = Mathf.RoundToInt(_h * _cam.aspect);
                _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf);
                _rt.Create();
                _cam.targetTexture = _rt;
            }

            // ⚠️ PARK THE GAME'S OWN CAMERA OVER THE WHOLE RUN. CameraFollow is the shipped framing, and
            // at the shipped zoom the frame is 14.1 m tall: a 4 s leg does not fit in it, so the leg sized
            // ITSELF down to 5.3 m and ended after 0.23 s — a quarter-second of driving lays no advected
            // sheet at all, and family A was then measured against an empty buffer. The same component
            // also LEADS her, which walked the head of her own track off the trailing edge twice.
            //
            // So the component comes off and the camera is placed over the leg's predicted path, sized to
            // hold the whole of it. This is still THE GAME'S OWN CAMERA — same entity id, so the same
            // primed per-camera foam state, the same shipped render path, the same grade — only held
            // still and pulled back. Every shot records the size it was taken at (SHOT FROM … m per
            // pixel), because a plate that cannot name its own scale cannot be ruled on.
            //
            // ⚠️ It must be DISABLED, not merely retargeted: CameraFollow writes orthographicSize back
            // from its own _framingOrtho every LateUpdate, so a size set under a live component is stomped.
            //
            // ⚠️ AND EVERY ONE OF THEM, NOT THE FIRST ONE FOUND. The camera this photographs through is
            // the persistent rig in DontDestroyOnLoad, and the loaded region installs its own follow as
            // well; `FindFirstObjectByType` returned one of the two, and the other went on writing the
            // size back to the shipped zoom. The plate then took the POSITION this fixture set and the
            // SIZE the game wanted — a frame nobody chose. `FindObjectsOfTypeAll` because the rig is
            // persistent and may be hidden; scene-bound only, so prefab assets on disk are left alone.
            foreach (CameraFollow f in Resources.FindObjectsOfTypeAll<CameraFollow>())
            {
                if (f == null || !f.gameObject.scene.IsValid()) continue;
                _parked.Add(f);
                _parkedWas.Add(f.enabled);
                f.enabled = false;
            }
            Debug.Log($"[{PlateDir}] parked {_parked.Count} CameraFollow(s) for the plate.");

            // ⚠️ AND THE PIXEL-PERFECT CAMERA IS THE ONE THAT ACTUALLY PINS THE SIZE. Parking every
            // CameraFollow was not enough: the size still read back 7.03125 m exactly, which is
            // `refResolutionY / (2 × assetsPPU)` — a PixelPerfectCamera recomputing it every frame.
            // CameraFollow owns that component's enabled state (it borrows it for feel effects), so
            // parking the follow leaves the PPC running and the camera at the shipped zoom, and the
            // plate would silently take this fixture's POSITION with the game's SIZE.
            //
            // Found by type NAME, not by type: PixelPerfectCamera lives in URP, and this test assembly
            // sets `overrideReferences` — widening its reference surface to shoot one plate costs more
            // than a name match. Pixel snapping is a FRAMING concern like the follow itself; what the
            // plate must keep is the shipped render path, exposure and grade, and those are untouched.
            foreach (Behaviour b in _cam.GetComponents<Behaviour>())
            {
                if (b == null || b.GetType().Name != "PixelPerfectCamera") continue;
                _parkedPpc = b;
                _parkedPpcWas = b.enabled;
                b.enabled = false;
                Debug.Log($"[{PlateDir}] parked the PixelPerfectCamera (was {_parkedPpcWas}).");
                break;
            }

            float half = Mathf.Max(run.extents.y, run.extents.x / Mathf.Max(0.01f, _cam.aspect));
            float want = Mathf.Max(2f, half);
            _cam.orthographicSize = want;
            _cam.transform.position =
                new Vector3(run.center.x, run.center.y, _cam.transform.position.z);

            Vector3 lastPos = _cam.transform.position;
            float lastSize = _cam.orthographicSize;
            int still = 0, frames = 0;
            while (still < 5 && frames < 400)
            {
                yield return null;
                frames++;
                bool moved = (_cam.transform.position - lastPos).sqrMagnitude > 1e-8f
                             || Mathf.Abs(_cam.orthographicSize - lastSize) > 1e-5f;
                still = moved ? 0 : still + 1;
                lastPos = _cam.transform.position;
                lastSize = _cam.orthographicSize;
            }
            Assert.Less(frames, 400, "the camera never stopped moving, so the overlay it is fitted to would " +
                                     "be a frame behind every plate below.");

            // ⚠️ AND THE FRAME THIS FIXTURE ASKED FOR IS THE FRAME IT GOT. Some other component writing
            // the size back is exactly the failure this plate exists to rule out elsewhere, so it is
            // named here rather than photographed.
            Assert.AreEqual(want, _cam.orthographicSize, 0.01f,
                            $"the camera was sized to {want:0.00} m half-height to hold the whole run, but " +
                            $"read back {_cam.orthographicSize:0.00} m — something still owns this camera's " +
                            $"framing after {_parked.Count} CameraFollow(s) and " +
                            $"{(_parkedPpc != null ? "the PixelPerfectCamera" : "no PixelPerfectCamera")} " +
                            "were parked, and the plate would be shot through a frame nobody chose.");
            for (int i = 0; i < 6; i++) yield return null;   // let the overlay refit to the plate's aspect
        }

        /// <summary>Render the game's own camera and read it back, gamma-corrected — the project is LINEAR,
        /// so a raw float read-back saves far too dark.</summary>
        byte[] Capture()
        {
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

        // ── the sheet ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>The photograph with the reference drawn ON it: the transom's swept track, the sheet's
        /// own root track where she has one, each family's measured centroid, and the crop those centroids
        /// were taken inside. Drawn AFTER every measurement, onto a copy, so nothing marked here can ever be
        /// counted as foam.</summary>
        byte[] Annotate(byte[] plate, List<(List<Vector2> path, Color32 colour)> lines,
                        List<(Vector2 world, Color32 colour)> marks, RectInt crop)
        {
            var outBytes = (byte[])plate.Clone();
            foreach (var (path, colour) in lines)
                for (int i = 1; i < path.Count; i++)
                    DrawLine(outBytes, ToPixel(path[i - 1]), ToPixel(path[i]), colour);
            foreach (var (world, colour) in marks) DrawCross(outBytes, ToPixel(world), colour);

            var dim = new Color32(200, 200, 200, 255);
            DrawLine(outBytes, new Vector2(crop.xMin, crop.yMin), new Vector2(crop.xMax, crop.yMin), dim);
            DrawLine(outBytes, new Vector2(crop.xMax, crop.yMin), new Vector2(crop.xMax, crop.yMax), dim);
            DrawLine(outBytes, new Vector2(crop.xMax, crop.yMax), new Vector2(crop.xMin, crop.yMax), dim);
            DrawLine(outBytes, new Vector2(crop.xMin, crop.yMax), new Vector2(crop.xMin, crop.yMin), dim);
            return outBytes;
        }

        Vector2 ToPixel(Vector2 world)
        {
            Vector3 p = _cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            return new Vector2(p.x, p.y);
        }

        void Plot(byte[] buf, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h) return;
            int i = (y * _w + x) * 4;
            buf[i] = c.r; buf[i + 1] = c.g; buf[i + 2] = c.b; buf[i + 3] = 255;
        }

        void DrawLine(byte[] buf, Vector2 a, Vector2 b, Color32 c)
        {
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : i / (float)steps;
                Plot(buf, Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t)),
                          Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t)), c);
            }
        }

        void DrawCross(byte[] buf, Vector2 p, Color32 c)
        {
            int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
            for (int d = -9; d <= 9; d++)
            {
                Plot(buf, cx + d, cy, c);
                Plot(buf, cx, cy + d, c);
            }
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

        // ── ONE BOAT, SET UP ONCE ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Everything a leg needs to know about the boat it is photographing. Extracted the day the lift
        /// arms needed the SAME hull rigged the SAME way as the foam arms: two photographs of one boat must
        /// not be able to disagree about how she was set up, and the only way to guarantee that is for there
        /// to be one setup.
        /// </summary>
        readonly struct Spawned
        {
            public readonly GameObject Go;
            public readonly BoatController Boat;
            public readonly Rigidbody2D Rb;
            public readonly BoatHullVariant Variant;
            public readonly float SternOffset;
            public readonly float ElevationDeg;
            public readonly float BandHalfWidth;
            public readonly FoamInjector Injector;

            public Spawned(GameObject go, BoatController boat, Rigidbody2D rb, BoatHullVariant variant,
                           float sternOffset, float elevationDeg, float bandHalfWidth, FoamInjector injector)
            {
                Go = go; Boat = boat; Rb = rb; Variant = variant;
                SternOffset = sternOffset; ElevationDeg = elevationDeg;
                BandHalfWidth = bandHalfWidth; Injector = injector;
            }
        }

        /// <summary>
        /// Her, through the production path, with the two roots and the band width this fixture measures
        /// against. An ordinary method rather than a coroutine because nothing in it waits on a frame —
        /// and because <see cref="Assert.Ignore(string)"/> THROWS, the <c>return default</c> on the
        /// sprite-path branch is a compiler formality that never executes.
        /// </summary>
        Spawned Spawn(string subject, BoatHullDef hull, BoatVisualDef visual, bool forceSprite,
                      Leg leg, Vector2 start)
        {
            // ── the boat, through the production path ────────────────────────────────────────────────
            var go = new GameObject($"Photo[{subject}-{leg.Name}]");
            _spawned.Add(go);
            go.transform.position = new Vector3(start.x, start.y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, -leg.HeadingDeg);

            var boat = go.AddComponent<BoatController>();   // RequireComponent brings the Rigidbody2D
            boat.SetHull(hull);
            boat.SetLocalSeabedDepth(40f);                  // she is in open water; never let her read aground
            var rb = go.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f; rb.linearDamping = 0f; rb.angularDamping = 0f;

            var options = default(BoatHullSkinner.Options);
            if (forceSprite) options.VariantOverride = BoatHullVariant.Sprite;
            BoatHullSkinner.Rig rig = BoatHullSkinner.Apply(go, visual, boat, options);
            Assert.IsTrue(rig.Skinned, $"{subject}: the skinner refused '{visual.Id}' — an unskinned hull " +
                                       "draws no wake and photographs nothing.");

            BoatHullVariant variant = rig.Presenter != null ? rig.Presenter.Variant : BoatHullVariant.Sprite;
            if (forceSprite && variant != BoatHullVariant.Sprite)
            {
                Assert.Ignore($"SKIPPED, NOT VERIFIED — '{visual.Id}' could not be forced down the SPRITE " +
                              "path, so the sprite-hull half of this photograph has no subject. Point it at " +
                              "a visual that still ships sheets.");
                return default;
            }

            // IN FRAME IS NOT IN THE PICTURE for a mesh hull: she is only drawn once the facet renderer has
            // been handed a hull id by the URP feature's registry.
            if (variant == BoatHullVariant.Mesh)
            {
                var facet = rig.Visual != null ? rig.Visual.GetComponent<IsoFacetHullRenderer>() : null;
                Assert.IsNotNull(facet, $"{subject}: mesh variant with no IsoFacetHullRenderer on the " +
                                        "visual child — none of her would be in the plate.");
                Assert.Greater(facet.HullId, 0, $"{subject}: the hull never registered with the facet " +
                                                "feature, so she is IN FRAME but not IN THE PICTURE.");
            }

            // ── the two roots this leg is measured against ───────────────────────────────────────────
            // The TRUTH is the transom as THE ONE ROOT computes it from her own pose — the thing all three
            // families claim to be rooted on — taken off the physics root, the transform that carries her
            // heading.
            float riggedStern = variant == BoatHullVariant.Mesh && visual.HasHullMesh()
                ? visual.HullMesh.WakeSternOffsetMeters : 0f;
            float sternOffset = WakeRootMath.SternOffsetMeters(riggedStern, hull.LengthMeters);
            float elevationDeg = variant == BoatHullVariant.Mesh && visual.HasHullMesh()
                ? visual.HullMesh.ElevationDeg : visual.ArtBakeElevationDegrees;
            float bandHalfWidth = Mathf.Max(0.5f, HullPresence.HalfBeamOf(hull, visual));

            // The injector, if she has one. Its OWN transform is what FoamInjector reads — recorded LIVE
            // rather than assumed, because which transform a component rides is exactly the sort of thing a
            // photograph is taken to settle.
            var injector = go.GetComponentInChildren<FoamInjector>(includeInactive: true);
            return new Spawned(go, boat, rb, variant, sternOffset, elevationDeg, bandHalfWidth, injector);
        }

        // ── THE LIFT: how far the wake stands the water up (water PR F, register row 27) ───────────────

        /// <summary>The water material's own amplitude dial. Zero is a bit-exact passthrough by
        /// construction — <c>WakeLiftHeight</c> returns 0.0 from its first line — so the dial-0 arm is a
        /// photograph of a sea with the feature ABSENT, not a sea with it turned down.</summary>
        const string LiftAmplitudeProp = "_WakeLiftMetres";

        /// <summary>The e-folding length astern. Also an exact passthrough at zero.</summary>
        const string LiftDecayProp = "_WakeLiftDecayMetres";

        /// <summary>The MEASURING dial: four times what ships, so the translation it asks for is tens of
        /// pixels rather than single figures. The shipped value is photographed too, on its own arm, because
        /// a feature measured only at a dial nobody plays at is unmeasured.</summary>
        const float LiftProbeMetres = 1.0f;

        /// <summary>What `Water.mat` ships (and every WaterPreset): the picture the player gets.</summary>
        const float LiftShippedMetres = 0.25f;

        /// <summary>The shipped decay. Held the same on every arm so amplitude is the only thing swept.
        /// </summary>
        const float LiftDecayMetres = 20f;

        /// <summary>How much water the correlation patch covers ACROSS the track. Wide, because the lift
        /// varies slowly across the wedge near the centreline and a wide patch is what carries enough
        /// texture to localise a shift at all.</summary>
        const float LiftPatchAcrossMetres = 3.0f;

        /// <summary>And how much ALONG the track. Deliberately small: along-track is the axis the train's
        /// phase runs on, so a tall patch would be sheared by the very gradient being measured rather than
        /// translated by it. At 0.6 m against a 23 m wavelength the internal shear is a few per cent.
        /// </summary>
        const float LiftPatchAlongMetres = 0.60f;

        /// <summary>⚠ A FEATURELESS PATCH IS REPORTED UNMEASURABLE, NEVER AS ZERO LIFT. Water with no
        /// texture inside the patch has no translation to read, and a correlation peak found in noise would
        /// be reported as a measurement. Luma standard deviation, 0…1.</summary>
        const float LiftPatchContrastFloor = 0.002f;

        /// <summary>And a match below this correlation is not a match. Most of what can go wrong here —
        /// a patch straddling her hull, a patch on the shoreline, a patch on sprite foam that does not ride
        /// the displacement — shows up as a poor peak rather than a wrong one.</summary>
        const float LiftPatchNccFloor = 0.5f;

        /// <summary>How many of the along-track samples must be MEASURABLE before the profile is called a
        /// profile. ⚠ This was 12 of 25 and is 8, because honest skipping removes samples that used to be
        /// counted: a patch the lift left byte-identical is now reported unmeasurable instead of as
        /// "0.0000 m, ncc 1.000" (see ElevationMetresAt), and on the cape six consecutive samples in the
        /// middle of the field are exactly that. The dropped samples are listed in the measurement rows and
        /// the band they sit in is a finding in its own right, NOT a licence to lower this again.</summary>
        const int AlongMeasurableFloor = 8;

        /// <summary>And the three shape bars, named ONCE because two arms are judged by them: the honest
        /// lift must clear all three, and the sabotage arm — the same train with its heading reversed —
        /// must fail at least one. Two arms scored by two different bar sets would prove nothing about
        /// either, so the sabotage verdict reads these very constants.</summary>
        const float ProfileCorrelationFloor = 0.5f;
        const float ProfileRmsFloorFraction = 0.25f;
        const float ProfileRmsCeilingFactor = 4f;

        /// <summary>The water materials this fixture writes the dials on: RUNTIME INSTANCES ONLY, found by
        /// asking which materials in the loaded scenes actually carry the property. Never an asset.</summary>
        readonly List<Material> _liftMats = new List<Material>();

        /// <summary>What each of them read before the first write (x = amplitude, y = decay), put back in
        /// teardown so a later class never inherits this fixture's dials.</summary>
        readonly List<Vector2> _liftWas = new List<Vector2>();

        /// <summary>The sorted instance ids of <see cref="_liftMats"/>. The at-rest identity is only worth
        /// anything if it is claimed on the SAME instances the dial was proven to reach at speed, so the set
        /// is pinned rather than assumed.</summary>
        string _liftMatSig;

        /// <summary>The published wake-lift globals as they stood before the sabotage arm doctored them,
        /// so teardown can put the real sea back whatever happens in between.</summary>
        Vector4[] _liftRootWas;
        Vector4[] _liftShapeWas;

        int _patchHalfW, _patchHalfH, _searchPx;

        /// <summary>The world bounds of every renderer found carrying the lift dial — ONE ENTRY EACH,
        /// deliberately not unioned, because the displaced surface is a GRID of chunk renderers and a hole
        /// in that grid is invisible in a union. Evidence only: probe rows say when a point falls outside
        /// all of them, and nothing here asserts.</summary>
        readonly List<Bounds> _liftChunks = new List<Bounds>();

        /// <summary>
        /// <b>How far the cape's wake lifts the water, in world metres.</b> Her mesh hull is the one the
        /// owner is looking at, and the only variant fitted with a <see cref="FoamInjector"/> — so the only
        /// one that publishes a wake-lift slot at all.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCape_UnderWay_PhotographsHowFarTheWakeLiftsTheWater()
        {
            RequireAGraphicsDevice();   // FIRST, before any yield — see its own note
            yield return PhotographLift("cape", CapeHullPath, CapeVisualPath);
        }

        /// <summary>
        /// <b>And the dory, whose beam is a third of the cape's.</b> The wedge half-width and the root ramp
        /// are both built off her half-beam, so she is the case that would catch a lift whose geometry was
        /// pinned to one hull's numbers.
        ///
        /// <para>⚠ She is photographed as her DEFAULT variant, which is the mesh. A forced-sprite hull
        /// carries no <see cref="FoamInjector"/> — only mesh hulls are fitted with one, by
        /// <c>IsoFacetHullPresentationService.Install</c> — so she would publish no slot and draw no lift,
        /// and the plate would have no subject. That is why this pair is cape+dory and not
        /// mesh+sprite.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheDory_UnderWay_PhotographsHowFarTheWakeLiftsTheWater()
        {
            RequireAGraphicsDevice();   // FIRST, before any yield
            yield return PhotographLift("dory", DoryHullPath, DoryVisualPath);
        }

        /// <summary>
        /// <b>🔴 THE ACCEPTANCE INSTRUMENT FOR THE OWNER'S RULING.</b> 2026-09-11: <i>"i do want the wake to
        /// lift the water and create visual waves."</i> What that became is measured here as a SURFACE
        /// ELEVATION PROFILE IN WORLD METRES — the lift arm against a dial-0 arm, along and across her own
        /// published track, per hull, at speed and at rest.
        ///
        /// <para><b>How a photograph becomes metres, exactly.</b> The shader adds the train to the same
        /// <c>lift</c> the swell displaces and then does <c>ws.y += lift</c>: ONE METRE OF LIFT IS ONE METRE
        /// OF WORLD Y. Nothing in the fragment stage changes — it paints AND clips at the UNDISPLACED
        /// <c>ground</c> position — so the lift TRANSLATES the painted pattern upward instead of re-shading
        /// it. Through an orthographic camera that translation is exactly
        /// <c>dy_px × (2 × orthographicSize / height_px)</c> metres, with no isometric factor to guess. So
        /// the measurement is a normalised cross-correlation of a patch of the DIAL-0 plate against the LIFT
        /// plate, refined to sub-pixel by a parabola on the correlation peak: an optical-flow read of a
        /// translation the renderer performed, not a brightness anybody has to interpret.</para>
        ///
        /// <para><b>⚠ EVERY PLATE IS HASHED BEFORE ANY RESULT IS CLAIMED, AND NO HASH IS REPORTED ALONE.</b>
        /// N identical hashes across a swept property means the property never reached the renderer that
        /// drew the plate — a finding about the instrument, not about the sea — so the sweep is asserted to
        /// change the bytes before one metre is believed. And because a hash can only answer "did anything
        /// change", every hash is reported beside the raw worst-channel delta that says HOW MUCH.</para>
        ///
        /// <para><b>And the renderer INSTANCE is identified first.</b>
        /// <see cref="DisplacedWaterSurface"/> draws the sea's chunks through a runtime
        /// <c>new Material(live)</c> whose resync from the asset is throttled on <c>Time.deltaTime</c> and
        /// therefore DEAD at <c>Time.timeScale = 0</c>: writing <c>Water.mat</c> here would change nothing on
        /// the plate and would dirty the owner's asset for nothing. Every material this fixture writes is
        /// found by asking which ones actually carry the property, logged by name, instance id and the
        /// GameObject carrying it, and anything the AssetDatabase owns is excluded by name.</para>
        ///
        /// <para><b>The prediction is the C# twin on the PUBLISHED slot</b>, never on numbers this fixture
        /// guessed. The root, the unit heading, the gate, the wavelength and the half-beam are read back out
        /// of the global uniform arrays — the very arrays the shader reads — so nothing about her stern
        /// offset, elevation or churn radius is assumed. <see cref="WakeLiftMath"/> is pinned to the HLSL
        /// line for line by the EditMode twin scrape, so a profile that tracks it is a profile of the
        /// shader's own law.</para>
        ///
        /// <para><b>The fence, restated because this fixture could not test it if it were false:</b> the lift
        /// is DRAWN and reaches no hull. Nothing here reads a force, a heave or a deck pose, because there is
        /// nothing to read — whether a hull rides another hull's wash is a simulation change under ADR 0018
        /// and is not in this PR.</para>
        /// </summary>
        IEnumerator PhotographLift(string subject, string hullPath, string visualPath)
        {
            BoatHullDef hull = LoadAsset<BoatHullDef>(hullPath);
            BoatVisualDef visual = LoadAsset<BoatVisualDef>(visualPath);
            if (hull == null || visual == null) yield break;   // LoadAsset has already ignored the case

            yield return LoadRegion();
            yield return SetHour(ShotHour);

            // ONE leg, due north. An elevation profile needs no heading sweep: it is taken along and across
            // HER OWN published heading, whichever that turns out to be, so a second heading would
            // re-measure the same law through a rotated frame and spend the editor slot twice. North also
            // lays the along-track axis along screen Y — which is the axis the lift displaces along — and
            // the cross-track transect along screen X, which is what lets a patch be wide across the track
            // and short along it.
            var leg = new Leg("north-000", 0f, 0f);

            if (!TryLegAnchors(1, out List<Vector2> anchors, out float depth))
            {
                Assert.Ignore("SKIPPED, NOT VERIFIED — no open water was found INSIDE " + SceneName +
                              "'s own camera bounds deep enough to drive a boat round, so there is nothing " +
                              "here that can be both sailed and photographed.");
                yield break;
            }

            _rows.Add($"# {subject} LIFT — {SceneName}, hour {ShotHour:0.0}, open water " +
                      $"({anchors[0].x:0.0}, {anchors[0].y:0.0}), {depth:0.0} m under her");
            _rows.Add("# surface elevation in WORLD METRES: lift arm against dial-0 arm, by patch match");
            yield return ShootLiftLeg(subject, hull, visual, leg, anchors[0], depth);
            WriteMeasurements(subject + "-lift");
        }

        IEnumerator ShootLiftLeg(string subject, BoatHullDef hull, BoatVisualDef visual, Leg leg,
                                 Vector2 start, float depth)
        {
            Spawned she = Spawn(subject, hull, visual, forceSprite: false, leg, start);
            Assert.IsNotNull(she.Injector,
                $"{subject}: she came up as {she.Variant} carrying NO FoamInjector, so she publishes no " +
                "wake-lift slot and no sea can stand up behind her. Only mesh hulls are fitted with one " +
                "(IsoFacetHullPresentationService.Install), so this photograph has no subject.");

            // ⚠ THE LIFT IS DRAWN BY THE DISPLACED PASS AND BY NOTHING ELSE. The flat Universal2D pass
            // carries no vertex displacement at all, so with the displaced sea off there is no plate that
            // could show this feature — and a green arm would be a lie about the shipped picture.
            DisplacedWaterSurface displaced = FindDisplacedSurface();
            Assert.IsNotNull(displaced, $"{subject}: no DisplacedWaterSurface in the loaded region, so the " +
                                        "sea is drawn by the flat pass, which has no vertex displacement " +
                                        "and cannot draw a lift.");
            Assert.IsTrue(displaced.Displaced,
                $"{subject}: the displaced sea is OFF (GameConfig DisplacedWater.DefaultOn reads " +
                $"{displaced.DefaultOn}), so the only pass that draws the lift is not running, and this " +
                "plate could not show the feature whether it works or not.");

            Bounds run = PredictRun(leg, start, 3f * she.BandHalfWidth + she.SternOffset + 2f);
            yield return FrameOn(she.Go.transform, run);

            // ⚠ FrameOn IS ENTERED ONCE PER LEG AND NEVER AGAIN. It RECORDS each CameraFollow's enabled
            // state as it parks it, so a second call would record the parked `false` as the state to put
            // back and leave the game's own camera dead for every class that runs after this one. The
            // at-rest arms therefore RE-VERIFY the frame after the clock move rather than re-aiming it:
            // same law — a plate arm must re-aim after every clock move — discharged by proof.
            Vector3 framedAt = _cam.transform.position;
            float framedSize = _cam.orthographicSize;

            var track = new List<Vector2>(256);
            var injRoot = new List<Vector2>(256);
            yield return Drive(she.Go, she.Rb, leg, track, injRoot, she.SternOffset, she.ElevationDeg,
                               she.Injector);
            Assert.Greater(track.Count, 8, $"{subject}: the leg recorded {track.Count} track samples — she " +
                                           "never got under way, so there is no wake to lift anything.");
            Assert.IsFalse(she.Boat.IsAground, $"{subject}: she read AGROUND through the leg.");
            AssertInFrame(subject, leg, she.Go.transform.position, track);

            // NOW stop the sea. The swell animates on engine time and would otherwise move most of the
            // plate between two arms that are supposed to differ only by a dial.
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;

            // ⚠ AND HUSH THE SPRITE FAMILIES, THROUGH #835's OWN SEAMS. Families B and C are separate
            // sprite renderers: they are NOT drawn by the water shader, so they do NOT ride the
            // displacement, and a patch that lands on them is asked to find a translation in content that
            // never translated. The advected sheet (family A) stays — it is composited INSIDE the water
            // shader at the undisplaced ground position, so it rides the lift and is the best texture on
            // the plate for reading one. The injector stays ENABLED for the same reason it must: disabling
            // it hands her wake-lift slot back, and the feature under test would vanish with it.
            Transform wakeRoot = FindWakeRoot(she.Go.name);
            HushTheWakeSprites(subject, wakeRoot);

            // ── WHO IS DRAWING THE WATER — before a single dial is written ─────────────────────────────
            IdentifyTheDrawingWater(subject, "under way");
            string sigAtSpeed = _liftMatSig;

            // ── WHAT SHE PUBLISHED — read back out of the globals, never assumed ──────────────────────
            Vector2 expectRoot = WakeRootMath.SternWorld((Vector2)she.Go.transform.position,
                                                        (Vector2)she.Go.transform.up,
                                                        she.SternOffset, she.ElevationDeg);
            int slot = IdentifyTheLiftSlot(subject, "under way", expectRoot, out Vector4 root,
                                          out Vector4 shape);
            var rootXY = new Vector2(root.x, root.y);
            var heading = new Vector2(root.z, root.w);
            float gate = shape.x, lambda = shape.y, halfBeam = shape.z;
            float rootError = Vector2.Distance(rootXY, expectRoot);
            // ⚠ TWO ROOTS, AND ROW 38 IS ABOUT THEIR DISAGREEMENT. The publisher is FoamInjector, which
            // computes the transom off ITS OWN transform and its own serialised stern offset; the fixture's
            // reference is the same law taken off the transform that carries her heading. On a straight leg
            // they agree to a few tens of centimetres, and through a turn they do not (row 38, finding 4) —
            // so both distances are reported and the tolerance is scaled to the hull rather than guessed.
            Vector2 injStern = WakeRootMath.SternWorld((Vector2)she.Injector.transform.position,
                                                      (Vector2)she.Injector.transform.up,
                                                      she.SternOffset, she.ElevationDeg);
            float injError = Vector2.Distance(rootXY, injStern);
            float rootBar = Mathf.Max(3f, 0.5f * hull.LengthMeters);

            Assert.Greater(gate, 0f,
                $"{subject}: slot {slot} is her nearest published root but its GATE reads {gate:0.0000} — " +
                $"she was making {DriveSpeed:0.0} m/s and the wake channel should be wide open. Either the " +
                $"pool ({FoamBuffer.MaxInjectors} slots, {LiveInjectorCount()} injectors alive in this " +
                "region) is rationed against her, or nothing published at all." + DumpLiftSlots(expectRoot));
            Assert.Greater(lambda, 0f, $"{subject}: the published transverse wavelength is {lambda:0.000} m, " +
                                       "and the shader skips a slot that has no wavelength.");
            Assert.AreEqual(1f, heading.magnitude, 1e-3f,
                $"{subject}: the published heading ({heading.x:0.0000}, {heading.y:0.0000}) is not a UNIT " +
                "vector, so the shader's along/across decomposition is scaled and every metre below it is " +
                "wrong by that scale.");
            Assert.Greater(halfBeam, 0f, $"{subject}: the published half-beam is {halfBeam:0.000} m, and the " +
                                         "wedge has no width without one.");
            Assert.Less(rootError, rootBar,
                $"{subject}: the published root ({rootXY.x:0.00}, {rootXY.y:0.00}) is {rootError:0.000} m " +
                $"from the transom THE ONE ROOT puts under her at the shutter ({expectRoot.x:0.00}, " +
                $"{expectRoot.y:0.00}), against a bar of {rootBar:0.00} m (half her {hull.LengthMeters:0.0} m " +
                $"length). It is {injError:0.000} m from the injector's own transom. The train is not rooted " +
                "where the wake is. She read " + she.Rb.linearVelocity.magnitude.ToString("0.00") +
                " m/s at the shutter." + DumpLiftSlots(expectRoot));

            float mpp = MetresPerPixel;
            float amp = LiftProbeMetres * gate;
            float ampShipped = LiftShippedMetres * gate;
            Debug.Log($"[{PlateDir}] {subject} LIFT: slot {slot}, published root ({rootXY.x:0.00}, " +
                      $"{rootXY.y:0.00}) {rootError:0.000} m off the shutter transom, heading " +
                      $"({heading.x:0.000}, {heading.y:0.000}), gate {gate:0.0000}, wavelength " +
                      $"{lambda:0.000} m, half-beam {halfBeam:0.000} m. Plate {_w}x{_h} at {mpp:0.00000} m " +
                      $"per pixel, so the measuring arm's {amp:0.000} m crest is {(amp / mpp):0.0} px. " +
                      $"{LiveInjectorCount()} injectors alive, pool {FoamBuffer.MaxInjectors}. {depth:0.0} m " +
                      "under her, so ShoreFade01 is taken as 1.0 — any fade below 1 is a uniform scale on " +
                      "measured-against-predicted, which the envelope below allows for.");
            _rows.Add($"slot {slot} | root ({rootXY.x:0.00}, {rootXY.y:0.00}) | root error {rootError:0.000} m " +
                      $"| heading ({heading.x:0.000}, {heading.y:0.000}) | gate {gate:0.0000} | wavelength " +
                      $"{lambda:0.000} m | half-beam {halfBeam:0.000} m | {mpp:0.00000} m/px | plate {_w}x{_h}");
            _rows.Add($"dials: measuring arm {LiftProbeMetres:0.00} m x gate = {amp:0.000} m; shipped arm " +
                      $"{LiftShippedMetres:0.00} m x gate = {ampShipped:0.000} m; decay {LiftDecayMetres:0.0} m");

            // ── THE ARMS, all inside ONE frozen frame ─────────────────────────────────────────────────
            // No yield between them, so nothing in the player loop can put a dial back between the write
            // and the shutter — which is also why the runtime material copy cannot be resynced from the
            // asset underneath us.
            byte[] z = ShootLiftArm(subject, "Z  dial 0, the reference", 0f, 0f);
            byte[] z2 = ShootLiftArm(subject, "Z2 dial 0 again, the instrument's own repeat", 0f, 0f);
            byte[] l = ShootLiftArm(subject, "L  the lift, measuring dial", LiftProbeMetres, LiftDecayMetres);
            byte[] p = ShootLiftArm(subject, "P  the lift, SHIPPED dial", LiftShippedMetres, LiftDecayMetres);

            string hz = Hash(z), hz2 = Hash(z2), hl = Hash(l), hp = Hash(p);
            float repeatDelta = WorstChannelDelta(z, z2, out int repeatPx);
            float liftDelta = WorstChannelDelta(z, l, out int liftPx);
            float shippedDelta = WorstChannelDelta(z, p, out int shippedPx);
            _rows.Add($"HASH Z {hz} | Z2 {hz2} | L {hl} | P {hp}");
            _rows.Add($"REPEAT Z vs Z2: worst channel delta {repeatDelta:0.00000}, {repeatPx} px differ — " +
                      (hz == hz2 ? "and the hashes agree, so this pipeline repeats bit for bit"
                                 : "and the hashes DIFFER, so this pipeline does NOT repeat bit for bit"));
            _rows.Add($"REACHED THE RENDERER? Z vs L worst {liftDelta:0.00000} over {liftPx} px; " +
                      $"Z vs P worst {shippedDelta:0.00000} over {shippedPx} px");

            Assert.AreNotEqual(hz, hl,
                $"{subject}: ⚠ IDENTICAL HASHES ACROSS A SWEPT DIAL. {LiftAmplitudeProp} was written to " +
                $"{_liftMats.Count} runtime water material(s) — {_liftMatSig} — and the plate did not change " +
                "one byte. The dial never reached the renderer that drew this picture, so nothing below " +
                "this line would be a measurement of the sea; it would be a measurement of the instrument.");
            Assert.AreNotEqual(hz, hp,
                $"{subject}: the SHIPPED dial ({LiftShippedMetres:0.00} m) never reached the drawing " +
                "renderer either, so what the player actually gets is unphotographed.");
            Assert.Greater(liftDelta, Mathf.Max(4f * repeatDelta, 2f / 255f),
                $"{subject}: the lift arm differs from dial 0 by {liftDelta:0.00000} against a repeat floor " +
                $"of {repeatDelta:0.00000} — the plate changed, but by no more than this pipeline fails to " +
                "repeat itself, which is not a lift.");

            z2 = null;   // ~23 MB a plate; the repeat floor is measured, the bytes are not needed again

            // ── THE PROFILES ──────────────────────────────────────────────────────────────────────────
            var marks = new List<(Vector2 world, Color32 colour)>();

            var along = new List<(float astern, float lateral)>();
            for (int i = 0; i <= 24; i++) along.Add((i * lambda / 12f, 0f));
            List<LiftSample> alongL = MeasureLift(subject, "ALONG the track, lateral 0, arm L", l, z,
                                                 rootXY, heading, amp, lambda, halfBeam, LiftDecayMetres,
                                                 along, marks, new Color32(80, 200, 255, 255));
            List<LiftSample> alongP = MeasureLift(subject, "ALONG the track, lateral 0, arm P (SHIPPED dial)",
                                                 p, z, rootXY, heading, ampShipped, lambda, halfBeam,
                                                 LiftDecayMetres, along, null, default);
            p = null;

            // ⚠ HALF A WAVELENGTH ASTERN, NOT ONE — AND THIS IS NOT TO BE "CORRECTED" BACK. The first
            // cut put this transect at one full wavelength and it PASSED ON BOTH HULLS WHILE MEASURING
            // NOTHING: that distance lands inside a band of the plate where the lift arm and dial 0 came
            // out byte-identical, so all 25 samples read ncc 1.000 / 0.0000 m and "no lift outside the
            // wedge" was a vacuum (cape 0.0016 m, dory 0.0002 m — both of them the absence of a
            // measurement, which only the SamePixels guard in ElevationMetresAt makes visible). Half a
            // wavelength is the first TROUGH: the largest |lift| the train reaches anywhere, and inside the
            // part of the field both hulls demonstrably draw.
            float transectAstern = 0.5f * lambda;
            float hw = WakeLiftMath.HalfWidth(transectAstern, halfBeam);
            var across = new List<(float astern, float lateral)>();
            for (int i = -12; i <= 12; i++) across.Add((transectAstern, i * 1.6f * hw / 12f));
            List<LiftSample> acrossL = MeasureLift(subject,
                $"ACROSS the track at HALF a wavelength astern ({transectAstern:0.0} m — the first trough, " +
                $"where |lift| is greatest), wedge half-width {hw:0.00} m, arm L", l, z, rootXY, heading,
                amp, lambda, halfBeam, LiftDecayMetres, across, marks, new Color32(255, 200, 60, 255));

            // AHEAD of the transom, past the root ramp, the honest train is EXACTLY zero: RootRamp01 is
            // FoamBuffer.Profile(-astern, halfBeam), which closes completely one half-beam ahead of the
            // root. Probes start two half-beams ahead so the whole patch — not just its centre — sits in
            // the closed region. This is the guard the sabotage arm has to redden.
            var ahead = new List<(float astern, float lateral)>();
            for (int i = 1; i <= 6; i++) ahead.Add((-(2f * halfBeam + i * lambda / 16f), 0f));
            List<LiftSample> aheadL = MeasureLift(subject, "AHEAD of the transom, arm L — must be ZERO", l, z,
                                                 rootXY, heading, amp, lambda, halfBeam, LiftDecayMetres,
                                                 ahead, marks, new Color32(160, 160, 160, 255));

            int nAlong = Measurable(alongL);
            float r = Pearson(alongL);
            float rmsM = RmsMeasured(alongL), rmsP = RmsPredicted(alongL);
            float peakM = PeakMeasured(alongL), peakP = PeakPredicted(alongL);
            float outside = MaxAbsOutsideTheWedge(acrossL, halfBeam);
            float aheadHonest = MaxAbsMeasured(aheadL);
            float aheadBar = Mathf.Max(0.10f * amp, 2f * mpp);

            _rows.Add($"HEADLINE arm L (dial {LiftProbeMetres:0.00} m x gate {gate:0.0000} = {amp:0.000} m): " +
                      $"{nAlong} of {alongL.Count} along-track samples measurable, peak measured " +
                      $"{peakM:0.000} m against predicted {peakP:0.000} m, rms {rmsM:0.0000} m against " +
                      $"{rmsP:0.0000} m, Pearson r {r:0.000}");
            _rows.Add($"HEADLINE arm P (SHIPPED {LiftShippedMetres:0.00} m x gate = {ampShipped:0.000} m): " +
                      $"{Measurable(alongP)} of {alongP.Count} measurable, peak measured " +
                      $"{PeakMeasured(alongP):0.000} m against predicted {PeakPredicted(alongP):0.000} m, " +
                      $"Pearson r {Pearson(alongP):0.000}");
            _rows.Add($"THE WEDGE: worst |lift| outside the half-width on the cross-track transect " +
                      $"{outside:0.0000} m; AHEAD of the transom {aheadHonest:0.0000} m against a bar of " +
                      $"{aheadBar:0.0000} m");

            Assert.GreaterOrEqual(nAlong, AlongMeasurableFloor,
                $"{subject}: only {nAlong} of {alongL.Count} along-track samples were measurable against a " +
                $"floor of {AlongMeasurableFloor} — patch contrast below the floor, correlation below the " +
                "floor, or the two plates byte-identical there. That is too few to call a profile, and it " +
                "is a finding about this plate's texture and geometry rather than about the sea. The rows " +
                "above name every skipped point and why.");
            Assert.Greater(r, ProfileCorrelationFloor,
                $"{subject}: the measured elevation profile and the C# twin's prediction correlate at " +
                $"r = {r:0.000} over {nAlong} samples. The water moves, but not as the hull's own Kelvin " +
                "train: an unrooted, wrong-signed or undecayed lift looks exactly like this.");
            Assert.Greater(rmsM, ProfileRmsFloorFraction * rmsP,
                $"{subject}: the measured profile's rms is {rmsM:0.0000} m against the twin's " +
                $"{rmsP:0.0000} m — the sea is standing up, but by a small fraction of what the dial asked " +
                "for.");
            Assert.Less(rmsM, ProfileRmsCeilingFactor * rmsP,
                $"{subject}: the measured profile's rms is {rmsM:0.0000} m against the twin's " +
                $"{rmsP:0.0000} m — far MORE than the dial asked for, so something else in the frame is " +
                "moving too.");
            Assert.Less(outside, Mathf.Max(0.12f * amp, 2f * mpp),
                $"{subject}: {outside:0.0000} m of lift was measured OUTSIDE the wedge's own half-width, " +
                "where FoamBuffer.Profile windows the train to exactly zero. The lift is not inside the " +
                "Kelvin wedge it claims to be inside.");
            Assert.Less(aheadHonest, aheadBar,
                $"{subject}: {aheadHonest:0.0000} m of lift was measured AHEAD of her transom, past the root " +
                "ramp, where the train is zero by construction. A wake ahead of the boat is the wrong sign.");

            // ── THE SABOTAGE ARM ──────────────────────────────────────────────────────────────────────
            // ⚠ A NEGATIVE DIAL WOULD MEASURE NOTHING. `WakeLiftHeight` returns 0.0 for any amplitude at
            // or below zero, so a negative amplitude is a FIFTH exact passthrough — identical to arm Z, and
            // a sabotage arm that cannot change the plate is a knob feeding both sides. The wrong lift is
            // therefore built where a wrong lift would really live: in the PUBLISHED SLOT, with her heading
            // reversed, so the same train is rooted at the same transom pointing the wrong way, IN HER OWN
            // SLOT so the honest train is not drawn beside it.
            //
            // ⚠ THE VERDICT IS THE ALONG-TRACK PROFILE, NOT THE AHEAD-OF-TRANSOM PROBE, and that moved
            // for a measured reason. A reversed train leaves the sea astern of her bare, so the three shape
            // bars the honest arm had to clear must REJECT this arm — judged by the very same constants, on
            // the very same points. The ahead probes stay as evidence rows only: they cannot carry a verdict
            // on a small hull, because on the dory three of the six are OUT OF FRAME (the plate holds about
            // 7.3 m ahead of her transom) and the other three sit on her own static hull sprite, which pins
            // the correlation to shift 0 whatever the water does. Clearing her hull needs a lateral offset
            // of at least halfBeam + patchAcross/2 = 2.36 m while the reversed wedge is only ~2.95 m wide at
            // 6 m and narrower where the frame still reaches — off-hull AND in-wedge AND in-frame has no
            // solution at her scale. The cape's ahead probe does redden (0.5084 m against 0.0002 m honest),
            // which is what identifies this as hull scale rather than as the train.
            byte[] s = ShootSabotageArm(subject, slot, rootXY, heading, shape, out int sabSlot);
            string hs = Hash(s);
            float sabDelta = WorstChannelDelta(l, s, out int sabPx);
            List<LiftSample> alongS = MeasureLift(subject,
                "ALONG the track, SABOTAGE arm (her train, heading reversed in HER OWN slot) — the three " +
                "shape bars above must REJECT this", s, z, rootXY, heading, amp, lambda, halfBeam,
                LiftDecayMetres, along, null, default);
            List<LiftSample> aheadS = MeasureLift(subject,
                "AHEAD of the transom, SABOTAGE arm — EVIDENCE ROWS ONLY, see the note above", s, z,
                rootXY, heading, amp, lambda, halfBeam, LiftDecayMetres, ahead, null, default);
            float aheadSab = MaxAbsMeasured(aheadS);
            int nSab = Measurable(alongS);
            float rSab = Pearson(alongS), rmsSab = RmsMeasured(alongS);
            bool honestBarsAccept = nSab >= AlongMeasurableFloor
                                 && rSab > ProfileCorrelationFloor
                                 && rmsSab > ProfileRmsFloorFraction * rmsP
                                 && rmsSab < ProfileRmsCeilingFactor * rmsP;
            _rows.Add($"SABOTAGE: slot {sabSlot} (HERS, overwritten) published with heading " +
                      $"({-heading.x:0.000}, {-heading.y:0.000}); hash {hs}, differs from arm L by " +
                      $"{sabDelta:0.00000} over {sabPx} px");
            _rows.Add($"SABOTAGE VERDICT: {nSab} of {alongS.Count} along-track samples measurable (floor " +
                      $"{AlongMeasurableFloor}), Pearson r {rSab:0.000} (floor " +
                      $"{ProfileCorrelationFloor:0.00}), rms {rmsSab:0.0000} m against the twin's " +
                      $"{rmsP:0.0000} m (band {ProfileRmsFloorFraction * rmsP:0.0000} to " +
                      $"{ProfileRmsCeilingFactor * rmsP:0.0000} m) — the honest bars " +
                      (honestBarsAccept ? "ACCEPT it, which is a failure of this instrument"
                                        : "REJECT it, which is what a sabotage arm is for"));
            _rows.Add($"SABOTAGE EVIDENCE (no verdict rests on this line): |lift| ahead of the transom " +
                      $"{aheadSab:0.0000} m over {Measurable(aheadS)} of {aheadS.Count} measurable samples, " +
                      $"against the honest {aheadHonest:0.0000} m and the bar of {aheadBar:0.0000} m");

            Assert.AreNotEqual(hl, hs,
                $"{subject}: the SABOTAGE arm drew the same bytes as the honest one. A reversed heading in " +
                "the published slot changed nothing, so the slot's heading is not reaching the shader and " +
                "the geometry guards above are measuring something they cannot see.");
            Assert.IsFalse(honestBarsAccept,
                $"{subject}: ⚠ THE SABOTAGE ARM PASSED THE HONEST BARS. Her own train, rooted at her own " +
                "transom with its heading REVERSED and standing in her own slot, leaves the sea astern of " +
                $"her bare — and yet {nSab} of {alongS.Count} samples came back measurable with Pearson " +
                $"r {rSab:0.000} and rms {rmsSab:0.0000} m against the twin's {rmsP:0.0000} m, which clears " +
                "every bar the honest arm had to clear. Those bars therefore do not distinguish this " +
                "hull's Kelvin train from a wrong one, so the acceptance numbers above are not evidence " +
                $"of the lift. (The plates do differ: {sabDelta:0.00000} over {sabPx} px.)");

            SavePlate($"lift-{subject}-Z-dial0.png", z);
            SavePlate($"lift-{subject}-S-sabotage.png", s);
            RestoreLiftGlobals();
            s = null;

            RectInt crop = CropAround(track, she.Go.transform.position, she.BandHalfWidth);
            var lines = new List<(List<Vector2> path, Color32 colour)> { (track, new Color32(255, 80, 80, 255)) };
            SavePlate($"lift-{subject}-L-measuring-dial-annotated.png", Annotate(l, lines, marks, crop));
            l = null;
            z = null;

            // ── AT REST: WHAT A BOAT WITH NO WAY ON ACTUALLY DRAWS ─────────────────────────────
            // ⚠ THIS ARM'S FIRST CUT ASSERTED THE GATE IS EXACTLY ZERO AT REST, AND THAT IS TRUE OF NO
            // BOAT THE OWNER WILL EVER SEE. Production gates the lift on
            // `(groundVelocity - sample.CurrentVector).magnitude` — speed THROUGH THE WATER — so a hull
            // holding her berth in a running stream is making way through it, and NineMileCreek's stream
            // is real: every moored boat in the region publishes a live train (measured off the shader
            // globals: 0.285 m/s, a 0.052 m wavelength, gate 0.095). That cut tried to dodge the physics
            // by setting her drifting AT the current; the row below records what she actually holds at
            // the shutter, because a fixture that photographs an arrangement nobody plays measures a sea
            // nobody plays.
            //
            // So this arm answers the owner's question rather than the arithmetic's: WITH NO WAY ON, DOES
            // THE DIAL CHANGE THE PICTURE? That verdict is the plate comparison below, taken against this
            // pipeline's own measured repeat floor — no chosen bar, and no premise about the gate.
            RestoreLiftDials();
            Time.timeScale = 1f;
            var env = GameServices.Environment;
            Vector2 current = Vector2.zero;
            if (env != null) current = env.Sample().CurrentVector;
            Vector2 driftFrom = (Vector2)she.Go.transform.position;
            float driftSeconds = 0f;
            for (int i = 0; i < 48; i++)
            {
                she.Rb.linearVelocity = current;
                she.Rb.angularVelocity = 0f;
                yield return null;
                driftSeconds += Time.deltaTime;
            }
            // ⚠ READING Rb.linearVelocity BACK HERE WOULD BE A MIRROR, AND THE FIRST CUT DID EXACTLY
            // THAT: the loop above ASSIGNS that field on the last live frame and the clock then stops, so
            // nothing overwrites it and the row would report this fixture's own assignment as a
            // measurement — a perfect 0.000 m/s through the water, every time, whatever the boat did.
            // The injector does not read that field either: it differences POSITION over time, so the
            // ground speed is differenced the same way here, over the frames that actually ran.
            Vector2 restGround = driftSeconds > 0f
                ? ((Vector2)she.Go.transform.position - driftFrom) / driftSeconds
                : Vector2.zero;
            float restThroughWater = (restGround - current).magnitude;
            _rows.Add($"AT REST: the region's current is ({current.x:0.000}, {current.y:0.000}) m/s, " +
                      $"|current| {current.magnitude:0.000} m/s; she was asked to drift at it and MOVED " +
                      $"({restGround.x:0.000}, {restGround.y:0.000}) m/s over the ground across " +
                      $"{driftSeconds:0.000} s of live frames, so her speed THROUGH THE WATER over those " +
                      $"frames was {restThroughWater:0.000} m/s");

            Assert.AreEqual(framedSize, _cam.orthographicSize, 0.01f,
                $"{subject}: the frame was {framedSize:0.00} m half-height for the arms at speed and reads " +
                $"{_cam.orthographicSize:0.00} m for the at-rest arms — something re-aimed this camera " +
                "while the clock ran on, and the two halves of this measurement were shot through " +
                "different frames.");
            Assert.Less(Vector3.Distance(framedAt, _cam.transform.position), 0.05f,
                $"{subject}: the camera moved {Vector3.Distance(framedAt, _cam.transform.position):0.000} m " +
                "between the arms at speed and the at-rest arms, so the two halves were shot through " +
                "different frames.");
            AssertPointInFrame(subject, leg, "the hull after the clock ran on for the at-rest arms",
                               she.Go.transform.position);

            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;

            IdentifyTheDrawingWater(subject, "at rest");
            Assert.AreEqual(sigAtSpeed, _liftMatSig,
                $"{subject}: the water's drawing materials were {sigAtSpeed} when the dial was PROVEN to " +
                $"reach them, and {_liftMatSig} now. The at-rest identity below would be claimed on " +
                "instances that were never shown to respond to the dial at all — which is exactly the " +
                "identical-hash trap this fixture exists to refuse.");

            Vector2 restRoot = WakeRootMath.SternWorld((Vector2)she.Go.transform.position,
                                                       (Vector2)she.Go.transform.up,
                                                       she.SternOffset, she.ElevationDeg);
            int restSlot = IdentifyTheLiftSlot(subject, "at rest", restRoot, out Vector4 root2,
                                               out Vector4 shape2);
            // ⚠ THIS SLOT IS PROBABLY NOT HERS, AND NO ASSERT MAY BE HUNG ON IT. At the shutter the
            // clock is stopped, so dt is 0 and FoamInjector publishes NOTHING (see its dt > 0 guard); the
            // packing therefore still holds whatever stood in it on the last live frame, and
            // IdentifyTheLiftSlot picks the nearest published root, which at rest is a MOORED NEIGHBOUR's.
            // The cape's run measured exactly that: slot 5 at (312.00, 64.80), gate 0.094975, wavelength
            // 0.051997 m — the region's 0.285 m/s current on someone else's hull, 60 m from her berth. The
            // first cut asserted "her" at-rest gate is below her gate at speed and was in fact comparing
            // two different boats. Recorded with its distance so the next reader sees whose train it is.
            float restRootError = Vector2.Distance(new Vector2(root2.x, root2.y), restRoot);
            _rows.Add($"AT REST: nearest published slot {restSlot}, root ({root2.x:0.00}, {root2.y:0.00}) " +
                      $"— {restRootError:0.00} m from her own transom at ({restRoot.x:0.00}, " +
                      $"{restRoot.y:0.00}) — gate {shape2.x:0.000000}, wavelength {shape2.y:0.000000} m. " +
                      (restRootError > Mathf.Max(3f, 0.5f * hull.LengthMeters)
                          ? "THAT IS NOT HER TRAIN: with the clock stopped she publishes nothing, so this " +
                            "is the nearest MOORED hull's, standing in the packing from the last live frame"
                          : "close enough to her transom to be her own last live publish"));

            byte[] rz = ShootLiftArm(subject, "RZ at rest, dial 0", 0f, 0f);
            byte[] rz2 = ShootLiftArm(subject, "RZ2 at rest, dial 0 again", 0f, 0f);
            byte[] rl = ShootLiftArm(subject, "RL at rest, the lift", LiftProbeMetres, LiftDecayMetres);
            string hrz = Hash(rz), hrz2 = Hash(rz2), hrl = Hash(rl);
            float restRepeat = WorstChannelDelta(rz, rz2, out int restRepeatPx);
            float restLift = WorstChannelDelta(rz, rl, out int restLiftPx);
            _rows.Add($"AT REST HASH RZ {hrz} | RZ2 {hrz2} | RL {hrl}");
            _rows.Add($"AT REST: RZ vs RZ2 worst {restRepeat:0.00000} over {restRepeatPx} px; RZ vs RL " +
                      $"worst {restLift:0.00000} over {restLiftPx} px");

            // ⚠ THE VERDICT IS A COMPARISON OF HER OWN TWO STATES, WITH NO CHOSEN BAR AND NO PREMISE
            // ABOUT THE GATE. Both earlier cuts of this line failed on the same false premise — that a
            // boat with no way on lifts EXACTLY no water — first by asserting the gate is zero and then by
            // asserting the plate is unchanged against the repeat floor. Neither is true in a region with a
            // current: she is making 0.285 m/s through the water at her moorings, the gate is small but
            // live, and a small live gate moves a few pixels. What the owner's question actually reduces to
            // is an ORDERING: the wake must be smaller with no way on than under way. That is what this
            // asserts, on the same hull, the same dial, the same materials, the same frame.
            Assert.Less(restLift, liftDelta,
                $"{subject}: with no way on, the {LiftProbeMetres:0.00} m dial changed the plate by " +
                $"{restLift:0.00000} — as much as or more than the {liftDelta:0.00000} the SAME dial moved " +
                $"it at {DriveSpeed:0.0} m/s. She measured {restThroughWater:0.000} m/s through the water " +
                $"holding her berth in this region's {current.magnitude:0.000} m/s stream against " +
                $"{DriveSpeed:0.0} m/s under way, so her wake must be the SMALLER of the two pictures. It " +
                $"is not, which means the gate is not following her speed through the water. (This " +
                $"pipeline's own repeat floor at rest is {restRepeat:0.00000} over {restRepeatPx} px, so " +
                "the number above is not noise.)");

            SavePlate($"lift-{subject}-RL-at-rest.png", rl);
            rz = null; rz2 = null; rl = null;

            RestoreLiftDials();
            RestoreRig(wakeRoot, she.Injector);
            Time.timeScale = 1f;
        }

        // ── the water, the slot, the dials ─────────────────────────────────────────────────────────────

        /// <summary>⚠️ The displaced surface may ride a hidden host, which
        /// <see cref="Object.FindFirstObjectByType{T}()"/> does not return — so the all-objects lookup is
        /// the fallback, filtered to things that are actually in a loaded scene.</summary>
        static DisplacedWaterSurface FindDisplacedSurface()
        {
            var live = Object.FindFirstObjectByType<DisplacedWaterSurface>();
            if (live != null) return live;
            foreach (DisplacedWaterSurface d in Resources.FindObjectsOfTypeAll<DisplacedWaterSurface>())
                if (d != null && d.gameObject.scene.IsValid()) return d;
            return null;
        }

        static int LiveInjectorCount()
        {
            int n = 0;
            foreach (FoamInjector f in Resources.FindObjectsOfTypeAll<FoamInjector>())
                if (f != null && f.gameObject.scene.IsValid() && f.enabled && f.gameObject.activeInHierarchy)
                    n++;
            return n;
        }

        /// <summary>Families B and C down, family A and the injector left alone — see the note at the call
        /// site. Reaches the emitter through <see cref="Resources.FindObjectsOfTypeAll{T}"/> because its host
        /// is <c>HideAndDontSave</c>.</summary>
        void HushTheWakeSprites(string subject, Transform wakeRoot)
        {
            if (_emitter == null)
            {
                var hidden = Resources.FindObjectsOfTypeAll<BoatWakeEmitter>();
                if (hidden != null && hidden.Length > 0) _emitter = hidden[0];
                if (_emitter != null) _emitter.enabled = false;
            }
            if (wakeRoot == null)
            {
                Debug.Log($"[{PlateDir}] {subject}: no Wake[{subject}] rig to hush — the sprite families " +
                          "are absent from this plate rather than switched off, which is reported here so " +
                          "the patch match's texture is not mistaken for something it is not.");
                return;
            }
            int off = 0;
            foreach (Transform child in wakeRoot)
            {
                var r = child.GetComponent<Renderer>();
                if (r != null && r.enabled) { r.enabled = false; off++; }
            }
            Debug.Log($"[{PlateDir}] {subject}: hushed {off} sprite renderer(s) under {wakeRoot.name} and " +
                      "the emitter, so nothing drawn OUTSIDE the water shader is inside a measurement " +
                      "patch. The injector stays enabled: disabling it hands her wake-lift slot back.");
        }

        /// <summary>
        /// <b>⚠ IDENTIFY THE RENDERER INSTANCE BEFORE SWEEPING ITS PROPERTY.</b> The sea's chunks are drawn
        /// through a runtime <c>new Material(live)</c>, so the asset on disk is not what draws and writing it
        /// would change no pixel while dirtying the owner's file. Materials are found by asking which ones
        /// CARRY the property rather than by shader name — a rename cannot hide from that — and anything the
        /// AssetDatabase owns is excluded by name in the log, so the exclusion is auditable.
        /// </summary>
        void IdentifyTheDrawingWater(string subject, string when)
        {
            _liftMats.Clear();
            _liftWas.Clear();
            _liftChunks.Clear();
            var seen = new HashSet<string>();
            var ids = new List<string>();
            int assetsSkipped = 0;

            foreach (Renderer r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (r == null || !r.gameObject.scene.IsValid()) continue;
                Material[] mats = r.sharedMaterials;
                if (mats == null) continue;
                bool draws = false;
                foreach (Material m in mats)
                {
                    if (m == null || !m.HasProperty(LiftAmplitudeProp)) continue;
#if UNITY_EDITOR
                    if (AssetDatabase.Contains(m))
                    {
                        assetsSkipped++;
                        Debug.Log($"[{PlateDir}] {subject} ({when}): SKIPPED the ASSET material '{m.name}' " +
                                  $"on {HierarchyPath(r.transform)} — writing a dial on an asset dirties " +
                                  "the owner's file and does not reach the runtime copy that draws.");
                        continue;
                    }
#endif
                    // ⚠ PER RENDERER, NOT PER MATERIAL. The chunks of the displaced surface all share ONE
                    // runtime material instance, so the dedup below drops every chunk after the first —
                    // and it is the CHUNKS, one renderer each, whose geometry decides what can be lifted.
                    draws = true;
                    if (!seen.Add(m.GetEntityId().ToString())) continue;
                    _liftMats.Add(m);
                    _liftWas.Add(new Vector2(m.GetFloat(LiftAmplitudeProp), m.GetFloat(LiftDecayProp)));
                    ids.Add(m.GetEntityId().ToString());
                    Debug.Log($"[{PlateDir}] {subject} ({when}): WRITING '{m.name}' (shader " +
                              $"'{(m.shader != null ? m.shader.name : "none")}', instance " +
                              $"{m.GetEntityId()}) on {HierarchyPath(r.transform)} — enabled " +
                              $"{r.enabled}, active {r.gameObject.activeInHierarchy}, scene " +
                              $"'{r.gameObject.scene.name}', reads {LiftAmplitudeProp} " +
                              $"{m.GetFloat(LiftAmplitudeProp):0.000} / {LiftDecayProp} " +
                              $"{m.GetFloat(LiftDecayProp):0.000}");
                }
                if (draws) _liftChunks.Add(r.bounds);
            }

            ids.Sort();
            _liftMatSig = string.Join(",", ids);
            _rows.Add($"DRAWING WATER ({when}): {_liftMats.Count} runtime material(s) [{_liftMatSig}], " +
                      $"{assetsSkipped} asset material(s) skipped");
            if (_liftChunks.Count > 0)
            {
                Bounds all = _liftChunks[0];
                foreach (Bounds b in _liftChunks) all.Encapsulate(b);
                _rows.Add($"DRAWN GEOMETRY ({when}): {_liftChunks.Count} renderer(s) carry that dial, " +
                          $"together spanning x [{all.min.x:0.00}, {all.max.x:0.00}] and y " +
                          $"[{all.min.y:0.00}, {all.max.y:0.00}]. ⚠ THAT IS THE UNION AND A HOLE IN THE " +
                          "GRID DOES NOT SHOW IN IT — the per-probe annotation in the profiles below is " +
                          "what finds one, because a vertex shader can only lift water that HAS vertices " +
                          "under it and a gap in the chunk grid draws a band of sea nothing displaces");
            }
            Assert.Greater(_liftMats.Count, 0,
                $"{subject} ({when}): not one RUNTIME material in the loaded scenes carries " +
                $"{LiftAmplitudeProp} ({assetsSkipped} asset material(s) do, and those are never written). " +
                "Either the property is gone from the shader, or the displaced surface is not instancing " +
                "its material — and in both cases this fixture has nothing it can sweep.");
        }

        /// <summary>Whether a world point sits inside ANY renderer that carries the lift dial. Answers
        /// "could this point have been displaced at all", which is a question about GEOMETRY and not about
        /// the shader: a probe outside every drawn chunk reads bare sea however right the train is. Returns
        /// true when nothing was catalogued, so the annotation stays silent rather than crying wolf.
        /// </summary>
        bool InsideDrawnWater(Vector2 world)
        {
            if (_liftChunks.Count == 0) return true;
            foreach (Bounds b in _liftChunks)
                if (world.x >= b.min.x && world.x <= b.max.x && world.y >= b.min.y && world.y <= b.max.y)
                    return true;
            return false;
        }

        static string HierarchyPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        /// <summary>
        /// Her slot, read back out of the GLOBAL UNIFORM ARRAYS — the very arrays the shader indexes, not
        /// the C# pool that feeds them. Chosen by nearest published root, so the fixture never has to assume
        /// which index she claimed.
        /// </summary>
        /// <summary>
        /// EVERY wake-lift slot the shader can read, as text, folded into this instrument's own failure
        /// messages. ⚠ THE NEAREST SLOT ALONE IS NOT EVIDENCE: "the nearest published root is 60 m
        /// away" is the same sentence whether nothing published at all, another hull under way published
        /// and she did not, or she published and was overwritten inside the frame. The whole table
        /// separates those three, and a future reader of a red run gets it without re-instrumenting.
        /// </summary>
        string DumpLiftSlots(Vector2 expectRoot)
        {
            Vector4[] roots = Shader.GetGlobalVectorArray(FoamShaderIds.WakeLiftRoot);
            Vector4[] shapes = Shader.GetGlobalVectorArray(FoamShaderIds.WakeLiftShape);
            if (roots == null || shapes == null)
                return " The lift globals have never been uploaded at all.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine();
            sb.Append("  THE WHOLE PACKING at frame ").Append(Time.frameCount)
              .Append(", timeScale ").Append(Time.timeScale.ToString("0.00"))
              .Append(", ").Append(LiveInjectorCount()).Append(" injectors alive in this region, ")
              .Append("her transom at (").Append(expectRoot.x.ToString("0.00")).Append(", ")
              .Append(expectRoot.y.ToString("0.00")).Append("):");
            int n = Mathf.Min(roots.Length, shapes.Length);
            for (int i = 0; i < n; i++)
            {
                var r = new Vector2(roots[i].x, roots[i].y);
                var h = new Vector2(roots[i].z, roots[i].w);
                sb.AppendLine();
                sb.Append("    slot ").Append(i).Append(": ");
                if (h.sqrMagnitude <= 0f) { sb.Append("EMPTY (no heading ever written)"); continue; }
                sb.Append("root (").Append(r.x.ToString("0.00")).Append(", ")
                  .Append(r.y.ToString("0.00")).Append(")  ")
                  .Append(Vector2.Distance(r, expectRoot).ToString("0.000")).Append(" m off her  ")
                  .Append("heading (").Append(h.x.ToString("0.000")).Append(", ")
                  .Append(h.y.ToString("0.000")).Append(")  ")
                  .Append("gate ").Append(shapes[i].x.ToString("0.0000")).Append("  ")
                  .Append("lambda ").Append(shapes[i].y.ToString("0.000")).Append(" m  ")
                  .Append("half-beam ").Append(shapes[i].z.ToString("0.000")).Append(" m");
            }
            return sb.ToString();
        }

        int IdentifyTheLiftSlot(string subject, string when, Vector2 expectRoot,
                                out Vector4 root, out Vector4 shape)
        {
            Vector4[] roots = Shader.GetGlobalVectorArray(FoamShaderIds.WakeLiftRoot);
            Vector4[] shapes = Shader.GetGlobalVectorArray(FoamShaderIds.WakeLiftShape);
            Assert.IsNotNull(roots, $"{subject} ({when}): the global '{FoamShaderIds.WakeLiftRoot}' has " +
                                    "never been uploaded, so the shader reads nothing and no hull can lift " +
                                    "any water. Either nothing published, or the uniform was renamed out " +
                                    "from under this fixture.");
            Assert.IsNotNull(shapes, $"{subject} ({when}): the global '{FoamShaderIds.WakeLiftShape}' has " +
                                     "never been uploaded.");
            Assert.AreEqual(FoamBuffer.MaxInjectors, roots.Length,
                $"{subject} ({when}): the uploaded root array is {roots.Length} long against a pool of " +
                $"{FoamBuffer.MaxInjectors}. The shader's HH_WAKE_LIFT_MAX loop is a compile-time bound " +
                "pinned to that pool, so a shorter upload means the shader reads past the end of it.");
            Assert.AreEqual(roots.Length, shapes.Length,
                $"{subject} ({when}): root and shape arrays disagree in length ({roots.Length} against " +
                $"{shapes.Length}), so slot i does not mean the same hull in both.");

            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < roots.Length; i++)
            {
                var r = new Vector2(roots[i].x, roots[i].y);
                var h = new Vector2(roots[i].z, roots[i].w);
                if (h.sqrMagnitude <= 0f) continue;   // never claimed: no heading was ever written
                float d = Vector2.Distance(r, expectRoot);
                if (d < bestD) { bestD = d; best = i; }
            }
            Assert.GreaterOrEqual(best, 0,
                $"{subject} ({when}): not one of the {roots.Length} slots carries a heading, so no hull " +
                "has ever published a wake-lift root in this region." + DumpLiftSlots(expectRoot));

            root = roots[best];
            shape = shapes[best];
            return best;
        }

        void SetLiftDials(float metres, float decay)
        {
            foreach (Material m in _liftMats)
            {
                if (m == null) continue;
                m.SetFloat(LiftAmplitudeProp, metres);
                m.SetFloat(LiftDecayProp, decay);
            }
        }

        void RestoreLiftDials()
        {
            for (int i = 0; i < _liftMats.Count && i < _liftWas.Count; i++)
            {
                Material m = _liftMats[i];
                if (m == null) continue;
                m.SetFloat(LiftAmplitudeProp, _liftWas[i].x);
                m.SetFloat(LiftDecayProp, _liftWas[i].y);
            }
        }

        void RestoreLiftGlobals()
        {
            if (_liftRootWas != null) Shader.SetGlobalVectorArray(FoamShaderIds.WakeLiftRoot, _liftRootWas);
            if (_liftShapeWas != null) Shader.SetGlobalVectorArray(FoamShaderIds.WakeLiftShape, _liftShapeWas);
            _liftRootWas = null;
            _liftShapeWas = null;
        }

        byte[] ShootLiftArm(string subject, string label, float metres, float decay)
        {
            SetLiftDials(metres, decay);
            byte[] plate = Capture();
            Debug.Log($"[{PlateDir}] {subject} arm {label}: {LiftAmplitudeProp} {metres:0.000}, " +
                      $"{LiftDecayProp} {decay:0.0} on {_liftMats.Count} material(s), plate hash " +
                      $"{Hash(plate)}");
            return plate;
        }

        /// <summary>
        /// The wrong lift, built where a wrong lift would really live: in the published slot. Her own train,
        /// her own root, her heading REVERSED — so it stands the water up in front of her instead of behind
        /// her.
        /// <para>⚠ IT OVERWRITES HER OWN SLOT, and the first cut did not. That cut put the reversed train
        /// in a FREE slot so the honest one stood beside it, which means the along-track profile of the
        /// sabotage arm was the HONEST profile plus a wedge that opens ahead of her — identical astern, so
        /// every bar this fixture judges the lift by would have ACCEPTED the sabotage arm, and the only
        /// guard left to redden was the ahead-of-transom one. That guard cannot be reddened on a small hull
        /// in a track-framed plate (the dory's six ahead probes are three OUT OF FRAME and three pinned to
        /// shift 0 by her own static sprite; off-hull AND in-wedge AND in-frame is unsatisfiable at her
        /// scale). Overwriting her slot makes the sabotage a REPLACEMENT: astern goes bare, so the profile
        /// bars themselves reject it, on the transect both hulls can actually measure.</para>
        /// </summary>
        byte[] ShootSabotageArm(string subject, int herSlot, Vector2 rootXY, Vector2 heading, Vector4 shape,
                                out int sabSlot)
        {
            Vector4[] roots = Shader.GetGlobalVectorArray(FoamShaderIds.WakeLiftRoot);
            Vector4[] shapes = Shader.GetGlobalVectorArray(FoamShaderIds.WakeLiftShape);
            _liftRootWas = (Vector4[])roots.Clone();
            _liftShapeWas = (Vector4[])shapes.Clone();

            // HER slot, so the honest train is REPLACED rather than joined — see the note above.
            sabSlot = herSlot;
            Debug.Log($"[{PlateDir}] {subject}: the sabotage train OVERWRITES her own slot {herSlot}, so " +
                      "the honest train is not drawn on this arm at all and the along-track profile reads " +
                      "the reversed train alone.");

            roots[sabSlot] = new Vector4(rootXY.x, rootXY.y, -heading.x, -heading.y);
            shapes[sabSlot] = shape;
            Shader.SetGlobalVectorArray(FoamShaderIds.WakeLiftRoot, roots);
            Shader.SetGlobalVectorArray(FoamShaderIds.WakeLiftShape, shapes);

            byte[] plate = ShootLiftArm(subject, $"S  SABOTAGE, slot {sabSlot}, heading reversed",
                                        LiftProbeMetres, LiftDecayMetres);
            return plate;
        }

        // ── the measurement: a translation the renderer performed ─────────────────────────────────────

        /// <summary>Metres of world Y per plate pixel. Exact for an orthographic camera, which is what
        /// makes a pixel shift a metre of surface elevation and not an estimate.</summary>
        float MetresPerPixel => 2f * _cam.orthographicSize / _h;

        readonly struct LiftSample
        {
            public readonly float Astern, Lateral;
            public readonly Vector2 World;
            public readonly float Measured, Predicted, Ncc;
            public readonly bool Ok;
            public readonly string Why;

            public LiftSample(float astern, float lateral, Vector2 world, float measured, float predicted,
                              float ncc, bool ok, string why)
            {
                Astern = astern; Lateral = lateral; World = world;
                Measured = measured; Predicted = predicted; Ncc = ncc; Ok = ok; Why = why;
            }
        }

        /// <summary>The patch, sized in METRES and converted, so a plate of a different size measures the
        /// same water. The search window is sized to the amplitude being probed: a peak pinned to the edge
        /// of its own window is reported unmeasurable rather than believed.</summary>
        void SizeTheProbe(float amplitudeMetres)
        {
            float mpp = MetresPerPixel;
            _patchHalfW = Mathf.Max(8, Mathf.RoundToInt(0.5f * LiftPatchAcrossMetres / mpp));
            _patchHalfH = Mathf.Max(3, Mathf.RoundToInt(0.5f * LiftPatchAlongMetres / mpp));
            _searchPx = Mathf.Clamp(Mathf.CeilToInt((1.8f * Mathf.Abs(amplitudeMetres) + 0.2f) / mpp),
                                    24, 200);
        }

        /// <summary>
        /// The inverse of the shader's own decomposition: <c>astern = -(d . heading)</c> and
        /// <c>lateral = d.x·h.y - d.y·h.x</c>, solved for <c>d</c>. Written as the inverse rather than as a
        /// second construction so a probe can never sit somewhere other than where the law says it is.
        /// </summary>
        static Vector2 TrainPoint(Vector2 rootXY, Vector2 heading, float astern, float lateral)
            => rootXY - heading * astern + new Vector2(heading.y, -heading.x) * lateral;

        List<LiftSample> MeasureLift(string subject, string label, byte[] arm, byte[] reference,
                                     Vector2 rootXY, Vector2 heading, float amplitude, float wavelength,
                                     float halfBeam, float decay,
                                     List<(float astern, float lateral)> points,
                                     List<(Vector2 world, Color32 colour)> marks, Color32 markColour)
        {
            SizeTheProbe(amplitude);
            float mpp = MetresPerPixel;
            var outSamples = new List<LiftSample>(points.Count);
            _rows.Add($"# {subject}: {label}");
            _rows.Add($"#   patch {2 * _patchHalfW + 1}x{2 * _patchHalfH + 1} px " +
                      $"({2 * _patchHalfW * mpp:0.00} m across the track x {2 * _patchHalfH * mpp:0.00} m " +
                      $"along it), search +/-{_searchPx} px (+/-{_searchPx * mpp:0.000} m)");

            foreach ((float astern, float lateral) in points)
            {
                Vector2 world = TrainPoint(rootXY, heading, astern, lateral);
                float predicted = WakeLiftMath.HeightAt(world, rootXY, heading, amplitude, wavelength,
                                                        halfBeam, decay);
                bool ok = ElevationMetresAt(arm, reference, world, out float measured, out float ncc,
                                           out string why);
                outSamples.Add(new LiftSample(astern, lateral, world, measured, predicted, ncc, ok, why));
                string geom = InsideDrawnWater(world)
                    ? string.Empty
                    : "  ⚠ OUTSIDE EVERY RENDERER THAT CARRIES THE DIAL — no vertices here to displace";
                if (ok)
                {
                    _rows.Add($"   astern {astern:0.00} m | lateral {lateral:0.00} m | measured " +
                              $"{measured:0.0000} m | predicted {predicted:0.0000} m | ncc {ncc:0.000}" +
                              geom);
                    if (marks != null) marks.Add((world, markColour));
                }
                else
                {
                    _rows.Add($"   astern {astern:0.00} m | lateral {lateral:0.00} m | predicted " +
                              $"{predicted:0.0000} m | {why}" + geom);
                }
            }
            return outSamples;
        }

        /// <summary>
        /// One elevation reading: how far UP the arm plate carries the patch of water the reference plate
        /// draws at <paramref name="world"/>. Normalised cross-correlation over vertical shifts, refined by
        /// a parabola on the peak. A positive shift is a positive lift, because the content the reference
        /// draws at row <c>r</c> appears in the arm at <c>r + lift/mpp</c>.
        /// </summary>
        bool ElevationMetresAt(byte[] arm, byte[] reference, Vector2 world, out float metres, out float ncc,
                               out string why)
        {
            metres = 0f;
            ncc = 0f;
            why = null;

            Vector2 px = ToPixel(world);
            int cx = Mathf.RoundToInt(px.x), cy = Mathf.RoundToInt(px.y);
            int marginY = _searchPx + _patchHalfH + 2;
            if (cx - _patchHalfW < 0 || cx + _patchHalfW >= _w || cy - marginY < 0 || cy + marginY >= _h)
            {
                why = $"OUT OF FRAME: the patch around pixel ({cx}, {cy}) does not fit inside {_w}x{_h} " +
                      $"with a +/-{_searchPx} px search, so this point is skipped rather than clamped";
                return false;
            }

            int nx = 2 * _patchHalfW + 1, ny = 2 * _patchHalfH + 1, n = nx * ny;

            // ⚠ NO BYTES CHANGED IS NOT A MEASUREMENT OF ZERO, AND THIS INSTRUMENT USED TO REPORT IT
            // AS ONE. Correlate a patch against an identical copy of itself and the peak is 1.000 at
            // shift 0 by construction — nothing else can win — so a patch the lift never touched came
            // out as "measured 0.0000 m, ncc 1.000", which reads exactly like "the sea the lift correctly
            // left alone". That is the identical-hash trap at the scale of one patch, and it is what let a
            // whole cross-track transect pass while measuring nothing (see the note at its call site) and
            // what made a band of the along-track profile look like honest zeros. Absence of information
            // is reported AS absence of information, and the bytes are the test — no threshold to pick.
            if (SamePixels(arm, reference, cx, cy))
            {
                // ⚠ AND SAY WHICH KIND OF NOTHING IT IS, because the two kinds have opposite causes and
                // the first run could not tell them apart. A patch that is FLAT — one tone, no texture —
                // cannot show a translation however far the water moved, so identical bytes there say
                // nothing at all about the lift. A patch that is TEXTURED and identical is a different
                // finding entirely: that pattern was drawn from UNDISPLACED ground coordinates (the
                // fragment paints at OUT.worldXY = ground), so something is standing over the water here
                // that the vertex stage cannot move. One byte of variation is the honest line between
                // them — below that the plate is flat to its own quantisation.
                float lo = 1f, hi = 0f;
                for (int dy = -_patchHalfH; dy <= _patchHalfH; dy++)
                    for (int dx = -_patchHalfW; dx <= _patchHalfW; dx++)
                    {
                        float v = Luma(arm, cx + dx, cy + dy);
                        if (v < lo) lo = v;
                        if (v > hi) hi = v;
                    }
                string kind = (hi - lo) <= 1f / 255f
                    ? "FEATURELESS — one flat tone, so a translation of it would be invisible whatever the " +
                      "water did; this says nothing about the lift"
                    : "TEXTURED BUT UNMOVED — the pattern is there and did not shift, so what covers this " +
                      "patch is drawn from UNDISPLACED ground coordinates and the vertex stage cannot move it";
                why = $"NOTHING TO MEASURE: the arm and the dial-0 reference draw this {nx}x{ny} px patch " +
                      "BYTE FOR BYTE THE SAME, so there is no translation in it to read. A correlation " +
                      "over identical patches peaks at 1.000 on shift 0 whatever the sea is doing, so " +
                      $"this point is skipped rather than reported as zero lift. Patch luma range " +
                      $"{hi - lo:0.0000} over {nx * ny} px: {kind}";
                return false;
            }

            var refPatch = new float[n];
            double sumA = 0, sumA2 = 0;
            int k = 0;
            for (int dy = -_patchHalfH; dy <= _patchHalfH; dy++)
                for (int dx = -_patchHalfW; dx <= _patchHalfW; dx++)
                {
                    float v = Luma(reference, cx + dx, cy + dy);
                    refPatch[k++] = v;
                    sumA += v;
                    sumA2 += (double)v * v;
                }
            double varA = sumA2 / n - (sumA / n) * (sumA / n);
            float contrast = (float)System.Math.Sqrt(System.Math.Max(varA, 0.0));
            if (contrast < LiftPatchContrastFloor)
            {
                why = $"UNMEASURABLE: the dial-0 patch carries {contrast:0.00000} luma of contrast, below " +
                      $"the {LiftPatchContrastFloor:0.00000} floor — featureless water holds no " +
                      "translation to read, and a peak found in that is noise reported as a measurement";
                return false;
            }

            int shifts = 2 * _searchPx + 1;
            var scores = new float[shifts];
            int bestI = -1;
            float best = -2f;
            for (int i = 0; i < shifts; i++)
            {
                int d = i - _searchPx;
                double sumB = 0, sumB2 = 0, sumAB = 0;
                k = 0;
                for (int dy = -_patchHalfH; dy <= _patchHalfH; dy++)
                    for (int dx = -_patchHalfW; dx <= _patchHalfW; dx++)
                    {
                        float b = Luma(arm, cx + dx, cy + dy + d);
                        sumB += b;
                        sumB2 += (double)b * b;
                        sumAB += (double)refPatch[k++] * b;
                    }
                double num = n * sumAB - sumA * sumB;
                double den = System.Math.Sqrt(System.Math.Max(n * sumA2 - sumA * sumA, 0.0))
                           * System.Math.Sqrt(System.Math.Max(n * sumB2 - sumB * sumB, 0.0));
                scores[i] = den > 0.0 ? (float)(num / den) : 0f;
                if (scores[i] > best) { best = scores[i]; bestI = i; }
            }

            ncc = best;
            if (best < LiftPatchNccFloor)
            {
                why = $"UNMEASURABLE: the best correlation over the search was {best:0.000}, below the " +
                      $"{LiftPatchNccFloor:0.00} floor — this patch is not the same water translated " +
                      "(her hull, the shoreline and anything drawn outside the water shader all read " +
                      "like this)";
                return false;
            }
            if (bestI == 0 || bestI == shifts - 1)
            {
                why = $"UNMEASURABLE: the correlation peak sits on the edge of the +/-{_searchPx} px " +
                      "search, so the true shift is larger than this window can see and any number here " +
                      "would be the window's, not the water's";
                return false;
            }

            // Sub-pixel by a parabola through the peak and its two neighbours.
            float ym1 = scores[bestI - 1], y0 = scores[bestI], yp1 = scores[bestI + 1];
            float denom = ym1 - 2f * y0 + yp1;
            float sub = Mathf.Abs(denom) > 1e-9f ? 0.5f * (ym1 - yp1) / denom : 0f;
            sub = Mathf.Clamp(sub, -1f, 1f);
            metres = ((bestI - _searchPx) + sub) * MetresPerPixel;
            return true;
        }

        float Luma(byte[] plate, int x, int y)
        {
            int i = (y * _w + x) * 4;
            return (0.299f * plate[i] + 0.587f * plate[i + 1] + 0.114f * plate[i + 2]) / 255f;
        }

        /// <summary>Whether two plates carry IDENTICAL BYTES over the correlation patch. Raw bytes rather
        /// than luma: luma is a weighted sum and could in principle collide, and the question here is
        /// whether the renderer drew the same thing, which only the bytes answer.</summary>
        bool SamePixels(byte[] a, byte[] b, int cx, int cy)
        {
            int row = (2 * _patchHalfW + 1) * 4;
            for (int dy = -_patchHalfH; dy <= _patchHalfH; dy++)
            {
                int i = ((cy + dy) * _w + cx - _patchHalfW) * 4;
                for (int k = 0; k < row; k++) if (a[i + k] != b[i + k]) return false;
            }
            return true;
        }

        /// <summary>
        /// <b>⚠ THE HASH, AND WHY IT IS NEVER REPORTED ALONE.</b> A 64-bit FNV-1a over the plate's bytes.
        /// The question a plate hash answers is "did ANY byte change when the dial moved" — the identical-hash
        /// trap — and for that a non-cryptographic digest over 23 MB is both sufficient and cheap. Every hash
        /// in this class is printed beside <see cref="WorstChannelDelta"/>, which says HOW MUCH changed, so
        /// no claim ever rests on a digest by itself.
        /// </summary>
        static string Hash(byte[] plate)
        {
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < plate.Length; i++)
            {
                h ^= plate[i];
                h *= 1099511628211UL;
            }
            return h.ToString("x16");
        }

        /// <summary>The biggest single-channel difference between two plates, 0…1, plus how many pixels
        /// differ at all. Raw and symmetric on purpose: <c>NoiseFloor</c> carries a hard 0.01 floor that
        /// would swallow an identity, and <c>HeadroomDelta</c> is one-sided.</summary>
        static float WorstChannelDelta(byte[] a, byte[] b, out int changedPx)
        {
            int worst = 0, changed = 0;
            int len = Mathf.Min(a.Length, b.Length);
            for (int i = 0; i < len; i += 4)
            {
                int d = 0;
                for (int c = 0; c < 3; c++)
                {
                    int e = a[i + c] - b[i + c];
                    if (e < 0) e = -e;
                    if (e > d) d = e;
                }
                if (d > 0) { changed++; if (d > worst) worst = d; }
            }
            changedPx = changed;
            return worst / 255f;
        }

        // ── the statistics, over the MEASURABLE samples only ──────────────────────────────────────────

        static int Measurable(List<LiftSample> s)
        {
            int n = 0;
            foreach (LiftSample x in s) if (x.Ok) n++;
            return n;
        }

        static float PeakMeasured(List<LiftSample> s)
        {
            float best = 0f;
            foreach (LiftSample x in s) if (x.Ok && Mathf.Abs(x.Measured) > Mathf.Abs(best)) best = x.Measured;
            return best;
        }

        static float PeakPredicted(List<LiftSample> s)
        {
            float best = 0f;
            foreach (LiftSample x in s)
                if (x.Ok && Mathf.Abs(x.Predicted) > Mathf.Abs(best)) best = x.Predicted;
            return best;
        }

        static float RmsMeasured(List<LiftSample> s)
        {
            double sum = 0; int n = 0;
            foreach (LiftSample x in s) if (x.Ok) { sum += (double)x.Measured * x.Measured; n++; }
            return n == 0 ? 0f : (float)System.Math.Sqrt(sum / n);
        }

        static float RmsPredicted(List<LiftSample> s)
        {
            double sum = 0; int n = 0;
            foreach (LiftSample x in s) if (x.Ok) { sum += (double)x.Predicted * x.Predicted; n++; }
            return n == 0 ? 0f : (float)System.Math.Sqrt(sum / n);
        }

        static float MaxAbsMeasured(List<LiftSample> s)
        {
            float worst = 0f;
            foreach (LiftSample x in s) if (x.Ok) worst = Mathf.Max(worst, Mathf.Abs(x.Measured));
            return worst;
        }

        /// <summary>The worst lift measured where the train is windowed to EXACTLY zero — beyond the wedge's
        /// own half-width at that distance astern. Only samples a comfortable margin outside are counted, so
        /// a patch straddling the edge cannot be read as a violation.</summary>
        static float MaxAbsOutsideTheWedge(List<LiftSample> s, float halfBeam)
        {
            float worst = 0f;
            foreach (LiftSample x in s)
            {
                if (!x.Ok) continue;
                float hw = WakeLiftMath.HalfWidth(x.Astern, halfBeam);
                if (Mathf.Abs(x.Lateral) >= 1.15f * hw) worst = Mathf.Max(worst, Mathf.Abs(x.Measured));
            }
            return worst;
        }

        /// <summary>Pearson correlation of measured against predicted. The headline guard, because it asks
        /// whether the water moved in the SHAPE the hull's own Kelvin train has — which an unrooted,
        /// wrong-signed or undecayed lift does not.</summary>
        static float Pearson(List<LiftSample> s)
        {
            double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
            int n = 0;
            foreach (LiftSample x in s)
            {
                if (!x.Ok) continue;
                double a = x.Measured, b = x.Predicted;
                sx += a; sy += b; sxx += a * a; syy += b * b; sxy += a * b;
                n++;
            }
            if (n < 3) return 0f;
            double num = n * sxy - sx * sy;
            double den = System.Math.Sqrt(System.Math.Max(n * sxx - sx * sx, 0.0))
                       * System.Math.Sqrt(System.Math.Max(n * syy - sy * sy, 0.0));
            return den > 0.0 ? (float)(num / den) : 0f;
        }


        void WriteMeasurements(string subject)
        {
            var sb = new StringBuilder();
            foreach (string row in _rows) sb.AppendLine(row);
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"MEASURED-{subject}.txt");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[{PlateDir}] measurements written: {path}\n{sb}");
            _rows.Clear();
        }
    }
}
