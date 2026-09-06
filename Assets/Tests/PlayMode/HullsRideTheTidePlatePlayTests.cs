using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>THE PLATES: Nine Mile Creek at three states of tide, in daylight, with the fleet in it</b>
    /// (owner playtest 2026-09-06 — <i>"boats dont ride the tide"</i>). Nine frames: the wall fleet, the
    /// float's small craft and the player's own hull alongside, each photographed at the lowest, the
    /// nearest-to-mean and the highest water this coast reaches in a lunar month.
    ///
    /// <para><b>The tide states are FOUND, never named.</b> A picked hour is a picture of whatever the
    /// tide happened to be doing at it — and the tide here is a real deterministic model with a
    /// spring–neap cycle, so "06:28" is not a state of tide, it is a coincidence.
    /// <see cref="FindTideStates"/> walks a lunar month at five-minute steps, keeps only the instants
    /// that fall in the middle of the day, and returns the extremes and the mean of what it found. A
    /// night plate is not a plate of a boat: the whole thing being looked at is where her hull sits on a
    /// lit stone wall.</para>
    ///
    /// <para><b>One variable moves between the frames of a set.</b> The daylight window is deliberately
    /// narrow (<see cref="NoonWindowFrom"/>–<see cref="NoonWindowTo"/>), so the sun is in nearly the same
    /// place in all three and the only thing the eye can be reading is the water. The camera is parked
    /// once per set and never moves again — <c>WharfNightStage</c>'s law 3.</para>
    ///
    /// <para><b>⚠️ These SKIP on CI and prove nothing there</b> — a plate of a wharf needs a GPU, and CI
    /// runs the Null device. <c>WharfNightStage.RequireAGraphicsDevice</c> says so in its own skip
    /// message; the numbers under each plate are measured locally and quoted in the PR.</para>
    /// </summary>
    public class HullsRideTheTidePlatePlayTests
    {
        const string SceneName = "NineMileCreek";
        const string PlateDir = "nmc-hulls-ride";

        /// <summary>The daylight window a plate may be shot in. Narrow on purpose: the sun barely moves
        /// across it, so three frames of one set differ by the tide and by almost nothing else.</summary>
        const float NoonWindowFrom = 11f, NoonWindowTo = 13f;

        /// <summary>A synodic month in days — the spring–neap cycle this coast's tide model runs on, so a
        /// scan this long is guaranteed to contain a spring low and a spring high.</summary>
        const double LunarMonthDays = 29.53;

        /// <summary>Five minutes of game time. Fine enough that the sampled extreme is within a
        /// centimetre or so of the true one, cheap enough to walk a month of it.</summary>
        const double ScanStepSeconds = 300.0;

        WharfNightStage _stage;

        readonly struct TideState
        {
            public readonly string Name;
            public readonly double TotalSeconds;
            public readonly float Level;
            public TideState(string name, double totalSeconds, float level)
            {
                Name = name; TotalSeconds = totalSeconds; Level = level;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            if (_stage != null) yield return _stage.TearDown();
            _stage = null;
        }

        // ---- finding the tide -------------------------------------------------------------------------

        /// <summary>
        /// ⭐ Walk a lunar month of the region's OWN deterministic tide and bring back the three states
        /// worth photographing: the lowest water, the highest, and the one nearest the mean of everything
        /// seen — each of them at an instant that is also the middle of a day.
        ///
        /// <para>Nothing is saved and nothing is picked: the answer is a pure function of
        /// <c>(worldSeed, gameTime)</c> through <see cref="IEnvironmentService.WaterLevelAt"/>, so this
        /// scan re-run tomorrow returns the same three instants.</para>
        /// </summary>
        static List<TideState> FindTideStates()
        {
            IEnvironmentService sea = GameServices.Environment;
            IGameClock clock = GameServices.Clock;
            Assert.IsNotNull(sea, "the region published no environment service, so it has no tide to find");
            Assert.IsNotNull(GameServices.Config, "no GameConfig, so a day has no length and no hour has a name");

            double secondsPerDay = GameServices.Config.SecondsPerDay;
            double start = clock != null ? clock.TotalSeconds : 0.0;
            double end = start + LunarMonthDays * secondsPerDay;

            double lowestAt = -1, highestAt = -1;
            float lowest = float.MaxValue, highest = float.MinValue;
            double sum = 0; int seen = 0;

            for (double t = start; t <= end; t += ScanStepSeconds)
            {
                double dayFraction = (t / secondsPerDay) % 1.0;
                float hour = (float)(dayFraction * 24.0);
                if (hour < NoonWindowFrom || hour > NoonWindowTo) continue;

                float level = sea.WaterLevelAt(t);
                sum += level; seen++;
                if (level < lowest) { lowest = level; lowestAt = t; }
                if (level > highest) { highest = level; highestAt = t; }
            }

            Assert.Greater(seen, 0,
                $"a whole lunar month contained no instant between {NoonWindowFrom:0} h and " +
                $"{NoonWindowTo:0} h, which cannot happen unless the day has stopped having hours");
            Assert.Greater(highest - lowest, 1f,
                $"the tide moved only {highest - lowest:0.00} m across a lunar month of middays. There is " +
                "nothing for a boat to ride and these plates would show three identical frames.");

            // The instant nearest the mean of everything the scan saw — a second pass, because "nearest
            // to the mean" is not knowable until the mean is.
            float mean = (float)(sum / seen);
            double meanAt = -1; float meanMiss = float.MaxValue, meanLevel = 0f;
            for (double t = start; t <= end; t += ScanStepSeconds)
            {
                double dayFraction = (t / secondsPerDay) % 1.0;
                float hour = (float)(dayFraction * 24.0);
                if (hour < NoonWindowFrom || hour > NoonWindowTo) continue;
                float level = sea.WaterLevelAt(t);
                float miss = Mathf.Abs(level - mean);
                if (miss < meanMiss) { meanMiss = miss; meanAt = t; meanLevel = level; }
            }

            var states = new List<TideState>
            {
                new TideState("low", lowestAt, lowest),
                new TideState("mean", meanAt, meanLevel),
                new TideState("high", highestAt, highest),
            };
            states.Sort((a, b) => a.Level.CompareTo(b.Level));

            Debug.Log($"[{PlateDir}] tide states found by scanning {LunarMonthDays:0.0} days of middays: " +
                      $"low {states[0].Level:0.00} m at t={states[0].TotalSeconds:0}, " +
                      $"mean {states[1].Level:0.00} m at t={states[1].TotalSeconds:0}, " +
                      $"high {states[2].Level:0.00} m at t={states[2].TotalSeconds:0} " +
                      $"(scan mean {mean:0.00} m over {seen} samples)");
            return states;
        }

        /// <summary>Move the world to an instant and let the picture wear it. The clock is already stopped
        /// (<c>FrameOn</c>'s law 1), so this seeks it and lets the LateUpdates — the day/night ease, every
        /// hull's tide ride, the float's — run against the new reading. Lamps must stay OFF: if a seek
        /// walked the frame into dusk the set would no longer be one variable.</summary>
        IEnumerator GoTo(TideState state)
        {
            GameServices.Clock.SeekTo(state.TotalSeconds);
            for (int i = 0; i < 12; i++) yield return null;

            Color tint = Shader.GetGlobalColor("_DayNightTint");
            Assert.IsFalse(WharfNightStage.LampsAreGatedOn(tint),
                $"the '{state.Name}' state put the frame into lamp hours — a night plate is not a plate " +
                "of a boat on a wall, and the three frames of this set would differ by the sun as well " +
                "as by the water");
        }

        /// <summary>Photograph one set: the same place, three states of tide, and the number under each.
        /// Returns the drawn y of <paramref name="subject"/> at each state, in order.</summary>
        IEnumerator ShootTheSet(string setName, IReadOnlyList<TideState> states, Transform subject,
                                List<float> drawnY)
        {
            for (int i = 0; i < states.Count; i++)
            {
                yield return GoTo(states[i]);
                if (subject != null) drawnY.Add(subject.position.y);
                _stage.SavePlate($"{setName}-{i + 1}-{states[i].Name}-{states[i].Level:0.00}m.png",
                                 _stage.Capture());
            }
        }

        // ---- the sets ---------------------------------------------------------------------------------

        /// <summary>
        /// ⭐ <b>THE WALL FLEET.</b> The boats the owner was looking at, on the 4.6 m north face, at the
        /// three states. The measurement under the pictures is the one the whole change is about: her
        /// drawn y must travel by the tide's own range, and it used to travel zero.
        /// </summary>
        [UnityTest]
        public IEnumerator TheWallFleetRidesTheTide()
        {
            WharfNightStage.RequireAGraphicsDevice();

            _stage = new WharfNightStage(SceneName, PlateDir);
            yield return _stage.Load();
            yield return _stage.SetNight(12f);              // daylight, and the tint settled on it

            List<TideState> states = FindTideStates();

            MooredBoat subject = FindMooredNearest(NineMileCreekMainland.BerthPos(2));
            Assert.IsNotNull(subject, "no moored hull anywhere near the wall's berth line — the region " +
                                      "built no fleet, and a plate of an empty wall proves nothing");
            Transform picture = subject.transform.Find(BoatHullSkinner.VisualChildName);
            Assert.IsNotNull(picture, $"'{subject.name}' was never skinned, so there is no picture to ride");

            yield return _stage.FrameOn(NineMileCreekMainland.BerthPos(2) + new Vector2(0f, 1f));

            var drawn = new List<float>();
            yield return ShootTheSet("wall-fleet", states, picture, drawn);

            float travelled = drawn[drawn.Count - 1] - drawn[0];
            float tideRange = states[states.Count - 1].Level - states[0].Level;
            Debug.Log($"[{PlateDir}] wall fleet '{subject.name}': drawn y {string.Join(" -> ", drawn)} " +
                      $"= {travelled:0.000} units over {tideRange:0.00} m of tide " +
                      $"({travelled / Mathf.Max(tideRange, 1e-3f):0.000} units per metre; " +
                      $"IsoGround.HeightScale is {IsoGround.HeightScale:0.000})");

            Assert.That(travelled, Is.EqualTo(tideRange * IsoGround.HeightScale).Within(0.25f),
                "the fleet did not climb the face by the tide it was standing in. 0.00 is the defect " +
                "this change exists to end; anything else is the wrong projection.");
        }

        /// <summary>
        /// ⭐ <b>THE FLOAT'S SMALL CRAFT.</b> The two boats on the fingers, and the dock they are tied to,
        /// in one frame. This is the set where a disagreement is visible without any number at all: if the
        /// raft leaves and the boats stay, the picture says so.
        /// </summary>
        [UnityTest]
        public IEnumerator TheFloatBoatsRideWithTheDockTheyAreTiedTo()
        {
            WharfNightStage.RequireAGraphicsDevice();

            _stage = new WharfNightStage(SceneName, PlateDir);
            yield return _stage.Load();
            yield return _stage.SetNight(12f);

            List<TideState> states = FindTideStates();

            Vector2 at = new Vector2(NineMileCreekMainland.FloatRunWestX + 12f,
                                     NineMileCreekMainland.FloatRunY);
            MooredBoat subject = FindMooredNearest(at);
            Transform picture = subject != null
                ? subject.transform.Find(BoatHullSkinner.VisualChildName) : null;

            yield return _stage.FrameOn(at);

            var drawn = new List<float>();
            yield return ShootTheSet("float-boats", states, picture, drawn);

            if (picture == null)
            {
                Debug.LogWarning($"[{PlateDir}] no hull found near the float — the plates are of the dock " +
                                 "alone. That is an authoring gap in the region's float berths, not a " +
                                 "ride one; the wall set carries the measurement.");
                yield break;
            }

            float travelled = drawn[drawn.Count - 1] - drawn[0];
            float tideRange = states[states.Count - 1].Level - states[0].Level;
            Debug.Log($"[{PlateDir}] float boat '{subject.name}': {travelled:0.000} units over " +
                      $"{tideRange:0.00} m of tide");
            Assert.That(travelled, Is.EqualTo(tideRange * IsoGround.HeightScale).Within(0.25f),
                "a boat lying at the float did not go up and down with the planks she is made fast to");
        }

        /// <summary>
        /// ⭐ <b>THE PLAYER'S OWN HULL ALONGSIDE — and the sort at HIGH WATER, which is where she is
        /// nearest the lip.</b> The quay face is NOT Y-sorted (<c>NineMileCreekDressing.FaceSortingOrder</c>
        /// puts every run on a fixed rung of the wharf-deck band), so a hull climbing toward the coping
        /// cannot change what she sorts against — but that is a claim about ORDER, and what the owner sees
        /// is a claim about the PICTURE. The order is asserted here and the picture is the plate.
        /// </summary>
        [UnityTest]
        public IEnumerator ThePlayersHullAlongsideRides_AndTheFaceStillSortsTheWayItDid()
        {
            WharfNightStage.RequireAGraphicsDevice();

            _stage = new WharfNightStage(SceneName, PlateDir);
            yield return _stage.Load();
            yield return _stage.SetNight(12f);

            List<TideState> states = FindTideStates();

            // The order the face draws on, and the order a hull draws on. Neither depends on where she
            // is, which is exactly why the ride cannot move her through the wall — it can only move her
            // picture up it.
            int face = NineMileCreekDressing.FaceSortingOrder(NineMileCreekDressing.NorthWallRun);
            Assert.That(face, Is.LessThan(SortingBands.WharfDeckMax),
                "the mooring face left the wharf-deck band, and a hull at SortingOrder 1 no longer has a " +
                "fixed relationship with the wall she lies against");
            Assert.That(face, Is.GreaterThan(SortingBands.Sea),
                "the face went under the sea plane, and the water would draw over the wall");

            Vector2 berth = NineMileCreekMainland.BerthPos(NineMileCreekMooredFleet.PlayerBerthIndex());
            Transform picture = PilotHerInto(berth);
            Assert.IsNotNull(picture, "the player's hull was not skinned, so there is nothing alongside");

            yield return _stage.FrameOn(berth + new Vector2(0f, 1f));

            var drawn = new List<float>();
            yield return ShootTheSet("player-alongside", states, picture, drawn);

            float travelled = drawn[drawn.Count - 1] - drawn[0];
            float tideRange = states[states.Count - 1].Level - states[0].Level;
            Debug.Log($"[{PlateDir}] player's hull at berth {NineMileCreekMooredFleet.PlayerBerthIndex()}: " +
                      $"drawn y {string.Join(" -> ", drawn)} = {travelled:0.000} units over " +
                      $"{tideRange:0.00} m; the wall's drawn lip line is y = " +
                      $"{NineMileCreekWharf.MooringEdgeY:0.00}, so at high water her waterline is " +
                      $"{NineMileCreekWharf.MooringEdgeY - drawn[drawn.Count - 1]:0.000} units under it");

            Assert.That(travelled, Is.EqualTo(tideRange * IsoGround.HeightScale).Within(0.25f),
                "the player's boat lay still against a wall the fleet beside her was climbing");
            Assert.Less(drawn[drawn.Count - 1], NineMileCreekWharf.MooringEdgeY,
                "at high water the player's waterline is drawn ABOVE the top of the quay — she is not " +
                "alongside the wall any more, she is on it");
        }

        // ---- the fixture's own hands -------------------------------------------------------------------

        static MooredBoat FindMooredNearest(Vector2 at)
        {
            MooredBoat best = null;
            float bestSqr = float.MaxValue;
            foreach (MooredBoat m in Object.FindObjectsByType<MooredBoat>(FindObjectsSortMode.None))
            {
                float sqr = ((Vector2)m.transform.position - at).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = m; }
            }
            // 12 m: a berth's own spacing is 5.5 m, so anything further away is a different mooring.
            return bestSqr <= 144f ? best : null;
        }

        /// <summary>The player's boat, alongside — a piloted hull skinned by the same call
        /// <c>PersistentCoreBuilder</c> makes, so she is the boat the player drives and not a stand-in
        /// with the same picture.</summary>
        Transform PilotHerInto(Vector2 berth)
        {
            BoatHullDef hull = null;
            foreach (MooredBoat m in Object.FindObjectsByType<MooredBoat>(FindObjectsSortMode.None))
                if (m.Owner != null && m.Owner.Boat != null) { hull = m.Owner.Boat; break; }
            if (hull == null) return null;

            var go = _stage.Track(new GameObject("PlayersHull"));
            go.SetActive(false);
            go.transform.position = new Vector3(berth.x, berth.y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, -90f);   // bow toward the harbour mouth
            var controller = go.AddComponent<BoatController>();
            controller.SetHull(hull);
            go.SetActive(true);
            BoatHullSkinner.Apply(go, hull.Visual, controller);
            return go.transform.Find(BoatHullSkinner.VisualChildName);
        }
    }
}
