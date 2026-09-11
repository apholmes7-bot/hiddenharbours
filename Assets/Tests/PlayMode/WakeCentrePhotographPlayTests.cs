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
        const int PlateHeightPx = 900;

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
            Time.timeScale = 1f;   // ⚠ a STATIC: left at 0 it stops every test that follows
            if (GameServices.Clock != null) GameServices.Clock.TimeScale = 1f;
            if (_emitter != null) { _emitter.enabled = true; _emitter = null; }

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

            if (!TryOpenWater(out Vector2 anchor, out float depth))
            {
                Assert.Ignore("SKIPPED, NOT VERIFIED — no run of open water was found in " + SceneName +
                              " deep enough to drive a boat down, so there is nothing to photograph a wake " +
                              "on. The region's tidal terrain either did not register or is dry along here.");
                yield break;
            }
            Debug.Log($"[{PlateDir}] {subject}: open water at ({anchor.x:0.0}, {anchor.y:0.0}), " +
                      $"{depth:0.0} m under her at this tide.");

            _rows.Add($"# {subject} — {SceneName}, hour {ShotHour:0.0}, open water " +
                      $"({anchor.x:0.0}, {anchor.y:0.0}), {depth:0.0} m");
            _rows.Add("# heading | family | drawn px | centroid world (x, y) | lateral offset from the " +
                      "swept track, m (+ = to PORT of the course) | that offset over the band's own " +
                      "half-width");

            var legs = new[]
            {
                new Leg("north-000", 0f, 0f),
                new Leg("east-090", 90f, 0f),
                new Leg("turn-000-thru-088", 0f, TurnRateDegPerSec),
            };

            for (int i = 0; i < legs.Length; i++)
            {
                Vector2 start = anchor + new Vector2(i * LegSpacingMetres, 0f);
                yield return ShootLeg(subject, hull, visual, forceSprite, legs[i], start);
            }

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
                yield break;
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

            // ── frame her, drive her, then stop the world ────────────────────────────────────────────
            yield return FrameOn(go.transform);

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

            MeasureFamily(subject, leg, "A sheet", armA, bare, floor, crop, track, bandHalfWidth,
                          new Color32(80, 160, 255, 255), marks, injector != null);
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
            float heading = leg.HeadingDeg;
            for (float elapsed = 0f; elapsed < DriveSeconds; )
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                heading += leg.TurnDegPerSec * dt;
                go.transform.rotation = Quaternion.Euler(0f, 0f, -heading);
                Vector2 course = (Vector2)go.transform.up * DriveSpeed;
                go.transform.position += (Vector3)(course * dt);
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
            for (int y = crop.yMin; y < crop.yMax; y++)
            {
                for (int x = crop.xMin; x < crop.xMax; x++)
                {
                    int i = (y * _w + x) * 4;
                    float d = HeadroomDelta(arm, bare, i);
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
                string note = $"{leg.Name,-18} | {family,-11} | NOTHING DRAWN above the measured noise " +
                              $"floor ({floor:0.0000} luma) anywhere in the crop";
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
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;   // let the self-installing components register
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

        /// <summary>Somewhere with enough water under her to run three legs without licking a shoreline —
        /// found by asking the region's OWN tidal terrain rather than by a coordinate typed into a test, and
        /// probed along the whole span the legs will use rather than at a single point.</summary>
        static bool TryOpenWater(out Vector2 water, out float depth)
        {
            water = default; depth = 0f;
            ITidalTerrain terrain = GameServices.TidalTerrain;
            if (terrain == null) return false;

            float best = 0f;
            for (float x = -400f; x <= 400f; x += 10f)
            {
                for (float y = -400f; y <= 400f; y += 10f)
                {
                    var p = new Vector2(x, y);
                    float shallowest = float.MinValue;
                    for (float d = 0f; d <= 2f * LegSpacingMetres + 40f; d += 20f)
                        shallowest = Mathf.Max(shallowest, terrain.ElevationAt(p + new Vector2(d, 0f)));
                    for (float d = 0f; d <= 40f; d += 10f)
                        shallowest = Mathf.Max(shallowest, terrain.ElevationAt(p + new Vector2(0f, d)));
                    if (shallowest < best) { best = shallowest; water = p; }
                }
            }
            depth = -best;
            return depth > 4f;
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
        IEnumerator FrameOn(Transform subject)
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
