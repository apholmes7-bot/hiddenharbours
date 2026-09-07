using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>THE BROW, PHOTOGRAPHED AT THREE STATES OF TIDE, THROUGH THE GAME'S OWN CAMERA — and the
    /// only place in this pass where the builder, the sheet and the runtime all run at once.</b>
    ///
    /// <para>#735 refused the gangway on a plate (<c>04-REFUSED-the-brow-at-spring-high</c>), so the
    /// answer owes a plate. But a picture alone is a claim nobody can re-check, and the arithmetic alone
    /// never runs the code that draws: <c>NineMileCreekWharf.PlaceGangway</c>, <c>PlaceFloatPiles</c> and
    /// <see cref="GangwayVisual"/> are exercised NOWHERE else. So this fixture does both — it re-places
    /// the wharf through the BUILDER's own entry point, renders <c>Camera.main</c>, writes the plates,
    /// AND asserts the thing the plates are supposed to show, so the pair cannot silently stop matching
    /// its own caption.</para>
    ///
    /// <para><b>What it asserts, and why each is the interesting one:</b></para>
    /// <list type="number">
    ///   <item><b>The brow does not move.</b> Its nine rungs share one cell and one pivot, so the object
    ///     is placed once and only the sprite changes — which is what makes the hinge exact rather than
    ///     merely close. A transform that moved between tides would be the #735 defect returning by
    ///     another route.</item>
    ///   <item><b>The piles do not move either</b>, while the raft beside them travels metres. That is
    ///     the whole of the second defect, in one comparison.</item>
    ///   <item><b>The rung actually changes</b>, and always to the shallowest one no shallower than the
    ///     truth. A ladder that never re-solves would draw one slope all day and still place perfectly.</item>
    /// </list>
    ///
    /// <para>⚠️ Re-placed live, not read out of the banked scene: the scene on disk was built before this
    /// PR, so it carries neither the piles nor the brow. Re-placing is what makes the plate a picture of
    /// the code the owner's next Build click will run — #735's own discipline.</para>
    /// </summary>
    public class NineMileCreekGangwayPlatePlayTests
    {
        const string SceneName = "NineMileCreek";
        const string PlateDir = "nmc-gangway";

        /// <summary>How far apart two transforms may sit and still be called "the same place" — a
        /// thousandth of a unit, which is four ten-thousandths of a sprite pixel at 32 px/m.</summary>
        const float Still = 1e-3f;

        /// <summary>The window the plates are shot in. Not a look: see <see cref="FindTideStates"/>.</summary>
        const double DaylightFromHour = 9.0, DaylightToHour = 16.0;

        private WharfNightStage _stage;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_stage != null) yield return _stage.TearDown();
            _stage = null;
        }

        [UnityTest]
        public IEnumerator TheBrowMeetsBothItsEndsAtThreeStatesOfTide()
        {
            // ⚠️ FIRST statement, before any yield: an Assert.Ignore raised after a yield unwinds through
            // the runner and the teardown records the case as FAILED with the skip text attached.
            WharfNightStage.RequireAGraphicsDevice();

            _stage = new WharfNightStage(SceneName, PlateDir);
            yield return _stage.Load();

            // ---- re-place the wharf through the BUILDER -------------------------------------------
            var terrain = Object.FindFirstObjectByType<MainlandTidalTerrain>();
            if (terrain == null)
            {
                Assert.Ignore("SKIPPED — the region has no MainlandTidalTerrain, so the builder cannot " +
                              "measure the deck it places against and the plate would be of nothing.");
                yield break;
            }

            GameObject banked = GameObject.Find(NineMileCreekWharf.RootName);
            if (banked != null) Object.DestroyImmediate(banked);
            StandableSurfaces.Clear();
            MooringCleats.Clear();
            NineMileCreekWharf.Place(terrain);
            for (int i = 0; i < 4; i++) yield return null;

            var brow = Object.FindFirstObjectByType<GangwayVisual>();
            var piles = FindAll("FloatPiles_");
            var bays = FindAll("FloatBay_");

            Assert.IsNotNull(brow, "the builder placed no GangwayVisual — the brow is undrawn again. " +
                                   "If the wharf ISO pack has not imported, that warning is in the log.");
            Assert.IsNotEmpty(piles, "the builder placed no FloatPiles — the guide piles and the mooring " +
                                     "chain are undrawn.");
            Assert.IsNotEmpty(bays, "the builder placed no FloatBay — the raft is undrawn.");

            // ---- the three states, found by SEARCHING the live tide rather than by picking hours ----
            var env = GameServices.Environment;
            var cfg = GameServices.Config;
            if (env == null || cfg == null || GameServices.Clock == null)
            {
                Assert.Ignore("SKIPPED — no environment/clock service, so the tide cannot be pinned and " +
                              "every plate would be of whatever hour the run happened to be in.");
                yield break;
            }

            double low = 0, high = 0, mid = 0;
            FindTideStates(env, cfg, out low, out mid, out high);

            var shots = new (string Name, double At, string Caption)[]
            {
                ("01-the-brow-at-low-water.png",  low,  "low water"),
                ("02-the-brow-at-mean-water.png", mid,  "mean water"),
                ("03-the-brow-at-high-water-735s-refusal-re-shot.png", high, "high water"),
            };

            Vector2 browMid = (NineMileCreekWharf.GangwayShoreEnd +
                               NineMileCreekWharf.GangwayFloatEnd) * 0.5f;

            var browY = new List<float>();
            var pileY = new List<float>();
            var bayY = new List<float>();
            var rungs = new List<int>();
            var waters = new List<float>();

            foreach (var shot in shots)
            {
                yield return SeekAndSettle(shot.At);
                yield return _stage.FrameOn(browMid);

                float water = env.WaterLevelAt(GameServices.Clock.TotalSeconds);
                float deck = brow.Brow.Landing.DeckElevationNow();
                float drop = brow.Brow.AbutmentElevation - deck;

                waters.Add(water);
                browY.Add(brow.transform.position.y);
                pileY.Add(piles[0].transform.position.y);
                bayY.Add(bays[0].transform.position.y);
                rungs.Add(brow.ShownRung);

                Debug.Log($"[{PlateDir}] {shot.Caption}: water {water:0.000} m, float deck {deck:0.000} m, " +
                          $"drop {drop:0.000} m, rung {brow.ShownRung} " +
                          $"(baked {NineMileCreekQuayFace.GangwayRungDrops[brow.ShownRung]:0.000} m); " +
                          $"brow y {brow.transform.position.y:0.0000}, piles y {piles[0].transform.position.y:0.0000}, " +
                          $"raft y {bays[0].transform.position.y:0.0000}");

                _stage.SavePlate(shot.Name, _stage.Capture());

                // ⭐ The rung is never meaningfully SHALLOWER than the truth — the bias the whole design
                // rests on, held to the ladder's own published tolerance rather than to a tighter number.
                // ⚠️ It has to be the tolerance: the abutment here is MEASURED off the terrain
                // (3.00022 m, not the authored 3.00), so the live drop and the baked ladder are derived
                // by two different routes and differ in the fourth decimal — 0.005 of a sprite pixel.
                // Asserting tighter than GangwayVisual.RungToleranceMetres fails the rule it is checking.
                Assert.That(NineMileCreekQuayFace.GangwayRungDrops[brow.ShownRung],
                    Is.GreaterThanOrEqualTo(drop - GangwayVisual.RungToleranceMetres - 1e-4f),
                    $"at {shot.Caption} the drawn ramp is FLATTER than the real one, which lifts its " +
                    "foot off the planks and shows daylight under the rollers");
                Assert.That(brow.ShownRung,
                    Is.EqualTo(NineMileCreekQuayFace.GangwayRungFor(drop)),
                    $"at {shot.Caption} the runtime and the region disagree about which rung to draw");
            }

            // ---- 1 & 2: what must NOT have moved ---------------------------------------------------
            for (int i = 1; i < shots.Length; i++)
            {
                Assert.That(browY[i], Is.EqualTo(browY[0]).Within(Still),
                    $"the brow MOVED between {shots[0].Caption} and {shots[i].Caption} " +
                    $"({browY[0]:0.0000} → {browY[i]:0.0000}). Its rungs share one pivot precisely so " +
                    "the object can stand still and the hinge stay exact — a brow that rides is #735's " +
                    "refusal coming back by another route.");
                Assert.That(pileY[i], Is.EqualTo(pileY[0]).Within(Still),
                    $"the guide piles MOVED between {shots[0].Caption} and {shots[i].Caption} " +
                    $"({pileY[0]:0.0000} → {pileY[i]:0.0000}). They are driven into the seabed; this is " +
                    "the whole of the second defect this pass exists to end.");
            }

            // ---- and what MUST have -----------------------------------------------------------------
            float travel = Mathf.Abs(bayY[shots.Length - 1] - bayY[0]);
            Assert.That(travel, Is.GreaterThan(0.5f),
                $"the raft only travelled {travel:0.000} u between {waters[0]:0.00} m and " +
                $"{waters[shots.Length - 1]:0.00} m of water — she is supposed to RIDE, and a dock that " +
                "does not is the defect #735 fixed being undone");

            Assert.That(rungs[0], Is.Not.EqualTo(rungs[shots.Length - 1]),
                $"the brow drew rung {rungs[0]} at {waters[0]:0.00} m and rung {rungs[shots.Length - 1]} " +
                $"at {waters[shots.Length - 1]:0.00} m — the ladder never re-solved, so the slope axis " +
                "is doing nothing and one drawn slope is serving the whole tide");
        }

        // ---- helpers ---------------------------------------------------------------------------------

        private static List<GameObject> FindAll(string namePrefix)
        {
            var found = new List<GameObject>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith(namePrefix, System.StringComparison.Ordinal)) found.Add(t.gameObject);
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return found;
        }

        /// <summary>
        /// The clock times of the LOWEST, nearest-mean and HIGHEST water in the next lunar month, found by
        /// scanning the region's own environment service rather than by naming an hour.
        ///
        /// <para>The tide carries a spring/neap envelope (<c>TideModel</c>), so "spring low" is not a fixed
        /// hour of a fixed day — picking one would photograph whatever amplitude that day happened to have,
        /// which is how #735's 4.28 m of deck travel came to be quoted against a 4.40 m range.</para>
        /// </summary>
        private static void FindTideStates(IEnvironmentService env, GameConfig cfg,
                                           out double low, out double mid, out double high)
        {
            double step = cfg.SecondsPerDay / 24.0 / 10.0;          // ~6 game minutes
            double span = cfg.SecondsPerDay * 30.0;                 // a lunar month, so a spring is in it
            float lo = float.MaxValue, hi = float.MinValue, best = float.MaxValue;
            low = mid = high = 0;
            for (double t = 0; t < span; t += step)
            {
                // ⭐ DAYLIGHT ONLY, and it is the plate's whole point. The tide and the sun come off ONE
                // clock, so an unconstrained search returns the month's deepest low at 03:00 and its
                // highest high at 21:00 — three plates in three different lights, in which the thing
                // that actually changed (the brow's slope) is the least visible difference on screen.
                // Held to one working day's hours, the three are comparable and the ramp is the subject.
                double hour = (t / cfg.SecondsPerDay % 1.0) * 24.0;
                if (hour < DaylightFromHour || hour > DaylightToHour) continue;

                float w = env.WaterLevelAt(t);
                if (w < lo) { lo = w; low = t; }
                if (w > hi) { hi = w; high = t; }
                if (Mathf.Abs(w) < best) { best = Mathf.Abs(w); mid = t; }
            }
        }

        /// <summary>
        /// Seek the clock and let the frame WEAR the new hour before stopping it — <c>WharfNightStage</c>'s
        /// laws 1 and 2, for a tide rather than for a lamp. The clock runs while the day/night tint catches
        /// up, so the water drifts a few game minutes; the plate is captioned with the level it was
        /// actually shot at rather than the one that was asked for.
        /// </summary>
        private IEnumerator SeekAndSettle(double totalSeconds)
        {
            Time.timeScale = 1f;
            GameServices.Clock.TimeScale = 1f;
            GameServices.Clock.SeekTo(totalSeconds);

            Color tint = Shader.GetGlobalColor("_DayNightTint");
            int still = 0, frames = 0;
            while (still < 4 && frames < 600)
            {
                yield return null;
                frames++;
                Color now = Shader.GetGlobalColor("_DayNightTint");
                float move = Mathf.Abs(now.r - tint.r) + Mathf.Abs(now.g - tint.g) + Mathf.Abs(now.b - tint.b);
                still = move < 1e-4f ? still + 1 : 0;
                tint = now;
            }
            GameServices.Clock.TimeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
        }
    }
}
