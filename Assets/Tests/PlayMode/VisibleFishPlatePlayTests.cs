using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE SEA SHOWS WHAT IS IN IT</b> — the owner's ruling of 2026-09-05, photographed in the region
    /// the charter names its acceptance in: <i>"in play at St Peters you see fish swimming where the finder
    /// says they are and you can cast at them"</i>.
    ///
    /// <para><b>Why this is a PLAY-mode fixture in the REAL scene, and not four plates from a GUI editor.</b>
    /// A plate shot by hand can drift from its own caption; this one cannot, because the same run that
    /// writes the PNG also asserts the thing the PNG is supposed to show. It loads St Peters, reads the
    /// SHIPPED school model through the Core seam, and renders the game's own camera — so a presenter that
    /// only draws in a synthetic stage cannot pass.</para>
    ///
    /// <para><b>ONE region load for the whole fixture.</b> The facet id pool that a mesh hull's
    /// <c>HullId</c> comes from exhausts after about three region loads, so every plate below is shot from
    /// a single <see cref="LoadStPeters"/> and the camera is re-framed between them rather than the region
    /// re-loaded.</para>
    ///
    /// <para><b>What each plate is evidence OF</b> — the depth ruling, which is the whole of the owner's
    /// <i>"some fish will be not visible if swimming deep"</i>: a shallows school draws the fish, a
    /// midwater school draws only the shadow, and a deep school draws NOTHING while still marking the
    /// finder and still biting. That last one is the plate that is easiest to fake and hardest to read, so
    /// it is asserted against the model rather than eyeballed: the schools are still there.</para>
    ///
    /// <para>Plates land in <c>Application.temporaryCachePath/visible-fish/</c>; the paths are logged.</para>
    /// </summary>
    public class VisibleFishPlatePlayTests
    {
        const string SceneName = "StPeters";
        const string PlateDir = "visible-fish";
        const string CleanupSceneName = "VisibleFishCleanup";

        /// <summary>Plate height in pixels. The WIDTH is always derived from the camera's own aspect —
        /// <c>DayNightController</c> fits its whole-frame multiply to <c>orthographicSize × aspect</c>, so a
        /// plate shot at any other aspect comes back with the light as a rectangle inset in it.</summary>
        const int PlateHeightPx = 900;

        readonly List<Object> _spawned = new List<Object>();
        readonly List<FishSchool> _schools = new List<FishSchool>();
        Camera _cam;
        RenderTexture _rt;
        int _w, _h;

        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;                       // ⚠ a STATIC: left at 0 it stops every later test

            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();

            // ⚠⚠ LOOK THE CLEANUP SCENE UP BEFORE CREATING IT — CreateScene THROWS on a name that already
            // exists, and this teardown also runs after a case that SKIPPED before loading the region (on
            // CI, with no graphics device, that is every case). A teardown that can throw turns a fixture
            // that should have cost CI nothing into a red PR.
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

        // =============================================================================================
        //  The acceptance, and the four plates, from ONE region load
        // =============================================================================================

        /// <summary>
        /// <b>The charter's acceptance, measured.</b> Four claims, in the order they matter:
        /// <list type="number">
        /// <item>the water draws fish where the model says there are fish;</item>
        /// <item>a DEEP school draws nothing at all, while still being a school;</item>
        /// <item>every species drawn is a species the resolver would roll — the honesty invariant, on the
        /// real region's own pool rather than a fixture's;</item>
        /// <item>casting onto a drawn school finds that same school through
        /// <see cref="SchoolInfluence"/> — the owner's <i>"able to be cast at"</i>.</item>
        /// </list>
        /// </summary>
        [UnityTest]
        public IEnumerator AtStPeters_TheWaterShowsTheFishTheFinderSays_AndYouCanCastAtThem()
        {
            RequireAGraphicsDevice();          // FIRST, before any yield

            yield return LoadStPeters();
            yield return SetHour(12f);         // the charter's "a shallows school at noon"

            if (!(GameServices.FishSchools is IFishSchoolView view))
            {
                Assert.Ignore("SKIPPED — St Peters registered no school VIEW, so there is nothing for the " +
                              "water to draw and a plate would be a plate of an empty sea.");
                yield break;
            }

            // ⚠ Instance, not FindFirstObjectByType: the host is HideAndDontSave and the object find
            // skips hidden objects, which reported a perfectly healthy presenter as MISSING.
            FishSchoolPresenter presenter = FishSchoolPresenter.Instance;
            var library = Resources.Load<FishSwimSpriteLibrary>(FishSwimSpriteLibrary.ResourcesPath);

            // ⚠ SAY WHAT IS TRUE BEFORE ASSERTING IT. A bare Assert tells you a run failed; it does not
            // tell you which of "the presenter never installed", "the library never loaded" and "the sea
            // is empty here" was the reason, and those are three different bugs with three different homes.
            Debug.Log($"[{PlateDir}] presenter {(presenter != null ? "INSTALLED" : "MISSING")}; " +
                      $"library {(library != null ? "loaded" : "NOT FOUND in Resources")}; " +
                      $"cod swim art {(library != null && library.Has("cod", FishSwimSpriteLibrary.AnimSwim) ? "present" : "ABSENT")}");

            Assert.IsNotNull(presenter,
                "the presenter did not self-install — RuntimeInitializeOnLoadMethod never ran, so the sea " +
                "would be empty in the shipped game too");
            Assert.IsNotNull(library,
                "Resources/FishSwimSpriteLibrary did not load — every school would draw nothing at all");

            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            DepthDropSettings depth = GameServices.Config != null
                ? GameServices.Config.DepthDrop : DepthDropSettings.Default;

            // Every school within a wide box around the harbour, classified by what the water should show.
            // ⚠ CENTRE THE SEARCH ON THE CAMERA, not on the origin. CameraFollow clamps to the region's
            // own bounds, so a school picked out of a huge box can be somewhere the camera will never go —
            // it settles at the clamp, the view rect holds no school, and the plate is of the wrong water.
            Camera startCam = Camera.main;
            Assert.IsNotNull(startCam, "no main camera — there is nothing to photograph the sea with");
            Vector2 eye = startCam.transform.position;
            Debug.Log($"[{PlateDir}] camera rests at ({eye.x:0.0}, {eye.y:0.0}), ortho {startCam.orthographicSize:0.0}");

            // Wide, because St Peters' fish are OUT IN THE BAY: at the on-foot start the shore water is
            // under the school sim's own MinWaterColumnMetres, so a box around the camera is empty. The
            // nearest school is then chosen below and the framing is PROVED to have reached it.
            var wide = new Rect(eye.x - 700f, eye.y - 700f, 1400f, 1400f);
            int n = view.SchoolsInView(wide, now, _schools);

            // The whole census, before any of it is judged: how many, how deep, and holding what. If the
            // sea turns out to be empty at this hour on this seed, that is a CONTENT fact and the log has
            // to be able to say so rather than leaving a bare failed assert behind.
            foreach (FishSchool sc in _schools)
                Debug.Log($"[{PlateDir}]   school at ({sc.Centre.x:0.0}, {sc.Centre.y:0.0}) " +
                          $"depth {sc.DepthMetres:0.0} m, r {sc.RadiusMetres:0.0} m, {sc.MarkCount} marks, " +
                          $"species [{(sc.SpeciesIds == null ? "" : string.Join(", ", sc.SpeciesIds))}] " +
                          $"-> {SwimmerVisibility.For(sc.DepthMetres, in depth)}");

            Assert.Greater(n, 0,
                "no schools anywhere near St Peters at noon — nothing below could be evidence of anything");

            FishSchool? shallow = null, midwater = null, deep = null;
            float bestShallow = float.MaxValue;
            int noSwimmer = 0;
            foreach (FishSchool s in _schools)
            {
                switch (SwimmerVisibility.For(s.DepthMetres, in depth))
                {
                    case SwimmerDraw.Full:
                    case SwimmerDraw.Dim:
                        // ⚠ NEAREST school THAT HAS A SWIMMER. A region's rod pool holds the soft-shell
                        // clam beside the finfish and the school sim picks it like anything else, so a
                        // good share of St Peters' schools are clam-primary and draw nothing at all by
                        // design. Framing on one of those photographs empty water and blames the
                        // presenter for it.
                        string sp = s.SpeciesIds != null && s.SpeciesIds.Count > 0 ? s.SpeciesIds[0] : null;
                        if (sp != null && !library.TrySwimKindFor(sp, out _)) { noSwimmer++; break; }
                        float d2 = ((Vector2)s.Centre - eye).sqrMagnitude;
                        if (d2 < bestShallow) { bestShallow = d2; shallow = s; }
                        break;
                    case SwimmerDraw.Shadow: midwater ??= s; break;
                    case SwimmerDraw.None: deep ??= s; break;
                }
            }

            Debug.Log($"[{PlateDir}] {noSwimmer} of {n} schools are led by a species with no swimmer " +
                      $"(shellfish in the rod pool) and draw nothing by design");
            Debug.Log($"[{PlateDir}] {n} schools in view at noon — " +
                      $"drawable {(shallow.HasValue ? "yes" : "no")}, " +
                      $"midwater {(midwater.HasValue ? "yes" : "no")}, " +
                      $"deep {(deep.HasValue ? "yes" : "no")}");

            Assert.IsTrue(shallow.HasValue,
                "not one school shallow enough to draw a fish — the acceptance cannot be shown at this " +
                "hour on this seed, which is a CONTENT finding, not a presenter defect");

            // ---- 1. the water draws fish where the model says there are fish ------------------------
            // ---- 1. the water draws fish where the model says there are fish ------------------------
            //
            // ⚠ AIM AT THE FISH, NOT AT THE POINT THE CAMERA DECLINES TO GO TO. CameraFollow lands the
            // camera a fixed distance from whatever it is given (measured: 31.9 m north, in Y only), and
            // the shipped view is about 9 m tall — so "frame on the school centre" reliably photographs
            // the water a couple of school-radii away from every fish in it. The offset is MEASURED here
            // and compensated, and if a candidate still puts nothing in frame the next school is tried.
            yield return FrameOn(shallow.Value.Centre);

            Vector2 asked = shallow.Value.Centre;
            Vector2 landed = _cam.transform.position;
            Vector2 camOffset = landed - asked;
            Debug.Log($"[{PlateDir}] CameraFollow lands {camOffset.magnitude:0.0} m from the aim point " +
                      $"({camOffset.x:0.0}, {camOffset.y:0.0}) — it clamps to the region bounds, so every " +
                      $"frame below places the camera directly instead");

            int onScreen = 0;
            FishSchool shot = shallow.Value;
            foreach (FishSchool cand in Candidates(shallow.Value, eye, library, in depth))
            {
                yield return ReFrameOn(cand.Centre);
                presenter.Rebuild();
                yield return null;

                Vector2 shoalAt = SwimmerCentroid(presenter, out int drawn);
                if (drawn > 0)
                {
                    yield return ReFrameOn(shoalAt);
                    presenter.Rebuild();
                    yield return null;
                }

                onScreen = SwimmersInFrame(presenter);
                Debug.Log($"[{PlateDir}] school ({cand.Centre.x:0.0}, {cand.Centre.y:0.0}) " +
                          $"{cand.MarkCount} marks: {presenter.VisibleSwimmers} drawn, {onScreen} in frame");
                if (onScreen > 0) { shot = cand; break; }
            }

            Assert.Greater(presenter.VisibleSchools, 0, "the presenter saw no schools where the model has one");
            Assert.Greater(onScreen, 0,
                "not one swimmer landed inside the frame on any candidate school — the plate would be a " +
                "picture of empty water, which is exactly what a passing swimmer COUNT can hide");

            // ⚠ THE WHOLE SHOAL IS IN THE PICTURE, not just one fish of it (owner's ruling 2026-09-06,
            // PR 2). Before the per-species spread the drawer swam the shoal on a loop of
            // 0.8 x the SCHOOL's radius — and a school's radius is 22-55 m because that is how far a
            // BOAT may be and still be on the mark, not how far apart fish swim. Measured against this
            // very camera, 9-26% of a school's fish were in frame while sitting exactly on it.
            //
            // The bar is derived from the species' OWN authored spread plus a fish length of slack for
            // the shoal's internal spacing — never from the presenter, which is the thing under test.
            AssertTheShoalHoldsTogether(presenter, shot);

            int drawnOverShallow = presenter.VisibleSwimmers;
            shallow = shot;
            Debug.Log($"[{PlateDir}] shooting ({shot.Centre.x:0.0}, {shot.Centre.y:0.0}) at " +
                      $"{shot.DepthMetres:0.0} m — {drawnOverShallow} swimmers drawn, {onScreen} in frame");

            SavePlate("01-stpeters-noon-shallows.png", Capture());

            // ---- 2. the honesty invariant, on the REAL region's pool --------------------------------
            // ⚠ NOT "exactly one". SchoolsAt returns EVERY school whose disc contains the point and
            // SchoolInfluence.At sums them, so overlapping discs are designed behaviour — and at the
            // owner's 2026-09-09 lattice (22 m cells, 8-14 m discs) they are ordinary. The invariant is
            // that the school the water DREW is AMONG the schools the rod FINDS, species and density and
            // all; asserting it is the ONLY one was an artefact of a sparser sea.
            var at = new List<FishSchool>();
            int rodFound = GameServices.FishSchools.SchoolsAt(shallow.Value.Centre, now, at);
            Assert.Greater(rodFound, 0,
                "a school the water drew is not there at all when the rod stands on it");

            FishSchool same = default;
            bool matched = false;
            foreach (FishSchool q in at)
                if (q.Centre == shallow.Value.Centre && q.StartSeconds == shallow.Value.StartSeconds)
                { same = q; matched = true; break; }
            Assert.IsTrue(matched,
                $"the rod found {rodFound} school(s) standing on the one the water drew, and none of " +
                "them is it");
            CollectionAssert.AreEqual(shallow.Value.SpeciesIds, same.SpeciesIds,
                "the water would draw a species the resolver would not roll");
            Assert.AreEqual(shallow.Value.MarkCount, same.MarkCount, "density");

            // ---- 3. you can cast at them ------------------------------------------------------------
            var scratch = new List<FishSchool>();
            var species = new List<string>();
            SchoolInfluence infl = SchoolInfluence.At(
                GameServices.FishSchools, shallow.Value.Centre, now, CatchContext.NoDepth,
                depth, GameServices.Config != null ? GameServices.Config.FishSchools
                                                   : FishSchoolSettings.Default,
                scratch, species);

            Assert.IsTrue(infl.OnFish,
                "a bobber landing in the middle of a school the water is drawing found no fish — the thing " +
                "you can see is not the thing you can catch");
            Assert.Greater(infl.BiteRateMultiplier, 1f, "and it did not fish any quicker for it");
            Debug.Log($"[{PlateDir}] cast onto the drawn school: {infl.EffectiveMarks:0.0} effective marks, " +
                      $"bite x{infl.BiteRateMultiplier:0.00}");

            // ---- 4. midwater draws a shape, deep draws nothing --------------------------------------
            if (midwater.HasValue)
            {
                yield return ReFrameOn(midwater.Value.Centre);
                presenter.Rebuild();
                yield return null;
                SavePlate("02-stpeters-noon-midwater-shadow.png", Capture());
                Debug.Log($"[{PlateDir}] midwater: {presenter.VisibleSwimmers} shapes at " +
                          $"{midwater.Value.DepthMetres:0.0} m");
            }
            else Debug.Log($"[{PlateDir}] no midwater school at this hour — plate 02 not shot");

            if (deep.HasValue)
            {
                yield return ReFrameOn(deep.Value.Centre);
                presenter.Rebuild();
                yield return null;

                // ⚠ THE POINT OF THIS PLATE. The sea is empty AND the school is still there: the finder
                // still marks it and the rod still finds it. An empty frame alone would equally well be a
                // broken presenter, so the emptiness is only worth anything beside the school it hides.
                var atDeep = new List<FishSchool>();
                Assert.Greater(GameServices.FishSchools.SchoolsAt(deep.Value.Centre, now, atDeep), 0,
                    "the deep school vanished from the model too — then the plate shows a bug, not a ruling");

                FishSchool stillThere = default;
                bool deepMatched = false;
                foreach (FishSchool q in atDeep)
                    if (q.Centre == deep.Value.Centre && q.StartSeconds == deep.Value.StartSeconds)
                    { stillThere = q; deepMatched = true; break; }
                Assert.IsTrue(deepMatched,
                    "the rod finds schools here but not the deep one the glass is still marking");
                Assert.Greater(stillThere.MarkCount, 0, "a deep school must still be marking the glass");

                SavePlate("03-stpeters-noon-deep-nothing-drawn.png", Capture());
                Debug.Log($"[{PlateDir}] deep: {presenter.VisibleSwimmers} swimmers drawn at " +
                          $"{deep.Value.DepthMetres:0.0} m, and the model still holds " +
                          $"{stillThere.MarkCount} marks there");
            }
            else Debug.Log($"[{PlateDir}] no deep school at this hour — plate 03 not shot");

            Assert.Greater(drawnOverShallow, 0);
        }

        // =============================================================================================
        //  helpers — the shapes the lamp-plate fixture proved out, kept deliberately identical
        // =============================================================================================

        /// <summary>Every school worth trying for a plate — drawable by depth, led by a species that has
        /// a swimmer, nearest the camera first, with the one already chosen at the head.</summary>
        List<FishSchool> Candidates(FishSchool first, Vector2 eye, FishSwimSpriteLibrary library,
                                    in DepthDropSettings depth)
        {
            var outList = new List<FishSchool> { first };
            var rest = new List<FishSchool>();
            foreach (FishSchool s in _schools)
            {
                if (s.Centre == first.Centre) continue;
                if (SwimmerVisibility.For(s.DepthMetres, in depth) == SwimmerDraw.None) continue;
                string sp = s.SpeciesIds != null && s.SpeciesIds.Count > 0 ? s.SpeciesIds[0] : null;
                if (sp != null && !library.TrySwimKindFor(sp, out _)) continue;
                rest.Add(s);
            }
            rest.Sort((a, b) => ((Vector2)a.Centre - eye).sqrMagnitude
                        .CompareTo(((Vector2)b.Centre - eye).sqrMagnitude));
            for (int i = 0; i < rest.Count && outList.Count < 8; i++) outList.Add(rest[i]);
            return outList;
        }

        /// <summary>Where the drawn shoal actually is — the centroid of the pooled renderers the
        /// presenter has enabled, read off the transforms rather than recomputed, so it cannot disagree
        /// with what was drawn.</summary>
        static Vector2 SwimmerCentroid(FishSchoolPresenter presenter, out int drawn)
        {
            drawn = 0;
            Vector2 sum = Vector2.zero;
            foreach (SpriteRenderer sr in presenter.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (!sr.enabled) continue;
                sum += (Vector2)sr.transform.position;
                drawn++;
            }
            return drawn > 0 ? sum / drawn : Vector2.zero;
        }

        /// <summary>How many drawn swimmers the camera can actually see — the only count a plate is
        /// evidence for.</summary>
        /// <summary>
        /// EVERY DRAWN FISH IS WITHIN ITS SPECIES' OWN SPREAD of the school's anchor — the assertion
        /// that would have gone red on the retired <c>0.8 x school.RadiusMetres</c> loop, and the reason
        /// this plate is now a picture of a shoal rather than of one fish and some water.
        ///
        /// <para>The bar is the authored <c>ShoalSpreadMetres</c> (the loop the leader swims) plus a
        /// generous allowance for the shoal's own length behind the leader — computed from the species
        /// Def and a literal, never by asking the presenter or ShoalMath for it.</para>
        /// </summary>
        void AssertTheShoalHoldsTogether(FishSchoolPresenter presenter, FishSchool school)
        {
            string primary = school.SpeciesIds != null && school.SpeciesIds.Count > 0
                ? school.SpeciesIds[0] : null;
            if (string.IsNullOrEmpty(primary)) return;         // an unstated school states no spread

            FishSpeciesDef def = FishSpeciesRegistry.Get(primary);
            if (def == null || !def.StatesShoalSpread) return;  // nothing authored to hold it to

            // The loop, plus the shoal trailing behind its leader. ShoalMath spaces fish by the species'
            // drawn LENGTH; 4 m of slack is far more than the biggest authored fish needs and still an
            // order of magnitude under the 17.6-44 m the retired rule produced.
            const float shoalTailAllowanceM = 4f;
            float bar = def.ShoalSpreadMetres + shoalTailAllowanceM;

            float worst = 0f;
            int counted = 0;
            foreach (SpriteRenderer sr in presenter.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (sr == null || !sr.enabled) continue;
                float d = Vector2.Distance(sr.transform.position, school.Centre);
                if (d > worst) worst = d;
                counted++;
            }
            if (counted == 0) return;

            Debug.Log($"[{PlateDir}] {primary}: {counted} swimmers, furthest {worst:0.00} m from the " +
                      $"anchor (authored spread {def.ShoalSpreadMetres:0.0} m, bar {bar:0.0} m)");
            Assert.LessOrEqual(worst, bar,
                $"a {primary} was drawn {worst:0.0} m from its school's anchor, past the {bar:0.0} m its " +
                "authored spread allows — the shoal is swimming a loop scaled to the BOAT's reach again");
        }

        int SwimmersInFrame(FishSchoolPresenter presenter)
        {
            int inFrame = 0;
            foreach (SpriteRenderer sr in presenter.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (!sr.enabled) continue;
                Vector3 v = _cam.WorldToViewportPoint(sr.transform.position);
                if (v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f) inFrame++;
            }
            return inFrame;
        }

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device (Null Device), so nothing " +
                              "rendered and nothing was proved. Expected on CI; a plate needs a GPU.");
        }

        IEnumerator LoadStPeters()
        {
            LogAssert.ignoreFailingMessages = true;      // the region logs decor complaints of its own
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;   // let the self-installing components register
        }

        IEnumerator SetHour(float hour)
        {
            if (GameServices.Clock == null || GameServices.Config == null)
            {
                Assert.Ignore("SKIPPED — the region registered no clock, so the hour cannot be pinned and " +
                              "the fish would be photographed at whatever moment the run happened to be in.");
                yield break;
            }
            Time.timeScale = 1f;
            double spd = GameServices.Config.SecondsPerDay;
            GameServices.Clock.SeekTo((1.0 + hour / 24.0) * spd);

            // ⚠ Let the clock RUN for a few frames after the seek: DayNightController publishes on init and
            // thereafter follows a MOVING clock, so seeking and stopping in the same breath leaves the
            // frame wearing whatever hour the scene loaded at.
            for (int i = 0; i < 8; i++) yield return null;
        }

        IEnumerator FrameOn(Vector2 at)
        {
            var follow = Object.FindFirstObjectByType<CameraFollow>();
            if (follow == null)
            {
                Assert.Ignore("SKIPPED — the region has no CameraFollow, so the plate would be framed by " +
                              "this fixture rather than by the game.");
                yield break;
            }

            var anchor = new GameObject("FishPlateAnchor");
            _spawned.Add(anchor);
            anchor.transform.position = new Vector3(at.x, at.y, 0f);
            follow.Target = anchor.transform;
            follow.Smooth = 1000f;      // ⚠ 0 means NEVER MOVE, not move instantly

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "no main camera — there is nothing to photograph the sea with");
            yield return SettleCamera(cam);

            _cam = cam;
            _h = PlateHeightPx;
            _w = Mathf.RoundToInt(_h * _cam.aspect);
            _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf);
            _rt.Create();

            // ⚠⚠ Attach the target BEFORE the world stops: a camera's aspect changes the moment a render
            // texture is attached, and the whole-frame overlay refits to it only while frames are running.
            _cam.targetTexture = _rt;
            for (int i = 0; i < 6; i++) yield return null;

            // NOW stop the sea. The game clock is already held; this holds the swell, which animates on
            // ENGINE time and otherwise moves most of the plate between two identical frames.
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
        }

        /// <summary>Move the already-framed camera to another school without re-loading the region (the
        /// facet-id budget) or re-attaching the render texture (the aspect the overlay is fitted to).</summary>
        /// <summary>
        /// Put the camera ON a point, by hand.
        ///
        /// <para>⚠ <b>CameraFollow will not take it there.</b> Measured across eight candidate schools:
        /// asking for a point 34 m south landed the camera in the same place every time, and two
        /// different schools both reported the same swimmer count because the camera had not moved at
        /// all. It clamps to the region's own bounds, and St Peters' fish are outside them — so a plate
        /// framed through the follow is a plate of the clamp, not of the school.</para>
        ///
        /// <para>Disabling the follow and setting the transform is the documented plate route (the zoom
        /// policy re-asserts <c>orthographicSize</c>, which is wanted — it keeps the game's own exposure —
        /// but nothing re-asserts POSITION). The camera is still <c>Camera.main</c>, so the day/night
        /// overlay and every light quad stay pinned to the frame exactly as the player sees them; a
        /// second camera would photograph a frame with the night missing.</para>
        /// </summary>
        IEnumerator ReFrameOn(Vector2 at)
        {
            Time.timeScale = 1f;                        // the overlay only refits while frames run

            var follow = Object.FindFirstObjectByType<CameraFollow>();
            if (follow != null) follow.enabled = false;

            Vector3 p = _cam.transform.position;
            _cam.transform.position = new Vector3(at.x, at.y, p.z);

            for (int i = 0; i < 4; i++) yield return null;
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
        }

        /// <summary>Wait until the camera has stopped panning AND zooming — the framing policy re-asserts
        /// <c>orthographicSize</c> every LateUpdate, and the overlay is fitted to whichever size won.</summary>
        static IEnumerator SettleCamera(Camera cam)
        {
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
            Assert.Less(frames, 400, "the camera never stopped moving, so the overlay it is fitted to would " +
                                     "be a frame behind every plate");
        }

        byte[] Capture()
        {
            _cam.Render();

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_w, _h, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, _w, _h), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prevActive;

            // ⚠ The project renders in LINEAR colour space, so a raw float read-back saves far too dark.
            Color[] px = tex.GetPixels();
            var outBytes = new byte[px.Length * 4];
            for (int i = 0; i < px.Length; i++)
            {
                Color c = new Color(Mathf.Clamp01(px[i].r), Mathf.Clamp01(px[i].g),
                                    Mathf.Clamp01(px[i].b), 1f).gamma;
                outBytes[i * 4 + 0] = (byte)Mathf.RoundToInt(c.r * 255f);
                outBytes[i * 4 + 1] = (byte)Mathf.RoundToInt(c.g * 255f);
                outBytes[i * 4 + 2] = (byte)Mathf.RoundToInt(c.b * 255f);
                outBytes[i * 4 + 3] = 255;
            }
            Object.DestroyImmediate(tex);
            return outBytes;
        }

        void SavePlate(string name, byte[] rgbaBottomLeft)
        {
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false, true);
            tex.LoadRawTextureData(rgbaBottomLeft);
            tex.Apply(false, false);
            string path = Path.Combine(dir, name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[{PlateDir}] plate -> {path}");
        }
    }
}
