using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
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

        /// <summary>One frame of a set: where her picture went, and what the WORLD said at that instant —
        /// the water she was in, the ground the rider read under her, her own draught, and whether she was
        /// on the bottom. Recorded together so the check below can be made against the harbour rather
        /// than against the code that drew it.</summary>
        readonly struct Shot
        {
            public readonly float DrawnY, Water, Bed, Draught;
            public readonly bool Aground;
            public Shot(float drawnY, float water, float bed, float draught, bool aground)
            {
                DrawnY = drawnY; Water = water; Bed = bed; Draught = draught; Aground = aground;
            }
            /// <summary>The waterline she is sitting at — spelled out HERE, from the physics, rather
            /// than asked of <c>TidalRide</c>: a plate that checks the drawing against the very function
            /// that drew it has checked nothing.</summary>
            public float Waterline => Mathf.Max(Water, Bed + Mathf.Max(0f, Draught));
        }

        /// <summary>
        /// ⭐⭐ <b>THE LAW, CHECKED AGAINST THE HARBOUR.</b> Between two states her picture must move by
        /// the change in her WATERLINE — not the change in the water. Afloat those are the same thing and
        /// the leg runs at <see cref="IsoGround.HeightScale"/> a metre; aground they are not, because the
        /// ebb goes on falling without her and she stops on the bed she is sitting on.
        ///
        /// <para><b>This is what the first plate run measured and the first assertion got wrong.</b> The
        /// wall fleet rode mean→high at 0.765 units per metre — exact — and did not move at all below
        /// water −0.30 m, which is <c>bed + draught</c> for a 1.30 m hull over the basin's own filled bed
        /// at −1.60 m. An assertion that demanded the full tidal range called a correct picture a failure;
        /// worse, it would have been "fixed" by loosening a tolerance until the defect fitted through
        /// it.</para>
        ///
        /// <para>So both readings are demanded: every leg matches the physics for the ground she is
        /// actually over, AND at least one leg must find her afloat at both ends — otherwise the set
        /// proves she stayed still on the mud and shows nothing about the projection at all.</para>
        /// </summary>
        static void AssertSheRodeTheTide(string who, IReadOnlyList<Shot> shots)
        {
            Assert.Greater(shots.Count, 1, $"{who}: fewer than two frames, so nothing moved between anything");

            float total = shots[shots.Count - 1].DrawnY - shots[0].DrawnY;
            Assert.Greater(total, 0.5f,
                $"{who}: her picture travelled {total:0.000} units across the whole set. 0.00 is the " +
                "owner's defect verbatim — a fleet that sits at the same place on the wall at every " +
                "state of the tide.");

            bool sawAnAfloatLeg = false;
            for (int i = 1; i < shots.Count; i++)
            {
                Shot a = shots[i - 1], b = shots[i];
                float rose = b.DrawnY - a.DrawnY;
                float expected = (b.Waterline - a.Waterline) * IsoGround.HeightScale;
                bool afloat = !a.Aground && !b.Aground;
                sawAnAfloatLeg |= afloat;

                Debug.Log($"[{PlateDir}] {who} leg {i}: water {a.Water:0.00} -> {b.Water:0.00} m, " +
                          $"waterline {a.Waterline:0.00} -> {b.Waterline:0.00} m over bed {b.Bed:0.00} " +
                          $"at draught {b.Draught:0.00} ({(afloat ? "AFLOAT" : "TOOK THE GROUND")}); " +
                          $"picture rose {rose:0.000} units, physics says {expected:0.000}");

                Assert.That(rose, Is.EqualTo(expected).Within(0.05f),
                    $"{who} leg {i}: her picture rose {rose:0.000} units where the water she is in and " +
                    $"the ground under her say {expected:0.000}. Water {a.Water:0.00}→{b.Water:0.00} m, " +
                    $"bed {b.Bed:0.00} m, draught {b.Draught:0.00} m.");
            }

            Assert.IsTrue(sawAnAfloatLeg,
                $"{who}: she was on the bottom at every state in this set, so these plates say nothing " +
                "about whether a FLOATING hull rides. Re-shoot at states that find her afloat.");
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
        /// Returns the drawn y of <paramref name="subject"/> at each state, in order.
        ///
        /// <para>⚠️⚠️ <b>The frame is CHECKED before it is written</b> — see
        /// <see cref="AssertTheSubjectIsInFrame"/>. A capture that saves without asking whether its
        /// subject is on screen produces a confident blank plate, named after the thing it does not
        /// show, and files it as acceptance evidence.</para>
        /// </summary>
        IEnumerator ShootTheSet(string setName, IReadOnlyList<TideState> states, Transform subject,
                                List<Shot> shots)
        {
            HullTideRide rider = subject != null ? subject.GetComponentInParent<HullTideRide>() : null;
            for (int i = 0; i < states.Count; i++)
            {
                yield return GoTo(states[i]);
                if (subject != null)
                    shots.Add(new Shot(subject.position.y, states[i].Level,
                                       rider != null ? rider.BedElevation : float.NegativeInfinity,
                                       rider != null ? rider.DraughtMetres : 0f,
                                       rider != null && rider.IsAgroundNow()));

                // Render FIRST, then check, then write: `isVisible` is only meaningful after a render,
                // and a plate must never reach the disk unverified.
                byte[] frame = _stage.Capture();
                if (subject != null) AssertTheSubjectIsInFrame(subject, $"{setName} at {states[i].Name} water");
                if (CanComposite(subject, $"{setName} at {states[i].Name} water"))
                    _stage.SavePlate($"{setName}-{i + 1}-{states[i].Name}-{states[i].Level:0.00}m.png", frame);
            }
        }

        /// <summary>
        /// ⚠️⚠️ <b>AND "IN FRAME" IS NOT ENOUGH FOR A MESH HULL</b> — measured 2026-09-06, and it is the
        /// half of the blank-plate law that <c>renderer.isVisible</c> cannot cover.
        ///
        /// <para>A mesh hull's picture is composed through the facet buffer, and her place in it is a
        /// HULL ID out of 255, allocated 1 + 12 at a time. <c>IsoFacetHullRegistry.s_NextId</c> never
        /// rewinds, so a long editor session that has stood up a few dozen hulls exhausts the pool and
        /// every hull after that "shares id 255 and may composite over one another" — logged, loudly, 97
        /// times in the full PlayMode run this fixture was first shot inside. Her GameObjects are all
        /// there, her renderers are enabled, they are inside the frustum, and <c>isVisible</c> is true.
        /// <b>She simply is not in the picture.</b></para>
        ///
        /// <para>So a plate is written only when the subject actually holds an id. When she does not,
        /// the frame is DISCARDED with the reason named, rather than saved as a picture of a wharf with
        /// no boats at it — and the ride numbers below are asserted anyway, because the transform moves
        /// whether or not the compositor drew her. Shoot these in a FRESH editor.</para>
        /// </summary>
        static bool CanComposite(Transform subject, string what)
        {
            if (subject == null) return false;
            var facet = subject.GetComponentInChildren<IsoFacetHullRenderer>(true);
            if (facet == null) return true;          // a sprite hull composites by drawing; nothing to check
            if (facet.HullId > 0) return true;

            Debug.LogWarning(
                $"[{PlateDir}] NO PLATE WRITTEN for {what}: this hull holds facet id 0, so the " +
                "compositor has no place for her and the frame would show the harbour without her in " +
                "it. The facet pool is 255 ids at 1 + 12 a hull and never rewinds, so a session that " +
                "has already stood up a few dozen hulls cannot photograph one. Re-shoot this fixture " +
                "in a FRESH editor, on its own filter.");
            return false;
        }

        /// <summary>
        /// ⭐⭐ <b>THE PLATE MUST CONTAIN ITS SUBJECT</b> (the law the berth-line lane bought on
        /// 2026-09-06: a capture returned success, reported the hour, the water level and every hull's
        /// world position — all of it true — and the image had no boats in it). Two independent reads,
        /// because either one alone can be fooled: the subject's own point must land inside the frame,
        /// AND something of hers must actually have rasterised.
        ///
        /// <para>The margin is generous on purpose. This is not a composition check; it is the
        /// difference between evidence and a picture of a wall.</para>
        /// </summary>
        void AssertTheSubjectIsInFrame(Transform subject, string what)
        {
            Camera cam = _stage.Camera;
            Vector3 vp = cam.WorldToViewportPoint(subject.position);
            var renderers = subject.GetComponentsInChildren<Renderer>(true);
            int visible = renderers.Count(r => r != null && r.enabled && r.isVisible);

            string diagnosis =
                $"[{PlateDir}] {what}: subject at world {subject.position}, viewport " +
                $"({vp.x:0.000}, {vp.y:0.000}, z {vp.z:0.00}); camera at {cam.transform.position} " +
                $"ortho {cam.orthographicSize:0.00} aspect {cam.aspect:0.000}; " +
                $"{visible} of {renderers.Length} renderers visible";
            Debug.Log(diagnosis);

            Assert.Greater(vp.z, 0f, $"the subject is BEHIND the camera. {diagnosis}");
            Assert.That(vp.x, Is.InRange(0.02f, 0.98f),
                $"the subject is off the side of the frame — this plate would be a picture of the " +
                $"harbour without her in it. {diagnosis}");
            Assert.That(vp.y, Is.InRange(0.02f, 0.98f),
                $"the subject is off the top or bottom of the frame. She RIDES between states, so a " +
                $"framing that held her at low water can lose her at high. {diagnosis}");
            Assert.Greater(renderers.Length, 0,
                $"the subject carries no renderer at all, so there is nothing of her to photograph. " +
                $"{diagnosis}");
            Assert.Greater(visible, 0,
                $"nothing of the subject rasterised into the frame that is about to be saved as " +
                $"evidence of her. {diagnosis}");
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

            var shots = new List<Shot>();
            yield return ShootTheSet("wall-fleet", states, picture, shots);
            AssertSheRodeTheTide($"wall fleet '{subject.name}'", shots);
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

            var shots = new List<Shot>();
            yield return ShootTheSet("float-boats", states, picture, shots);

            if (picture == null)
            {
                Debug.LogWarning($"[{PlateDir}] no hull found near the float — the plates are of the dock " +
                                 "alone. That is an authoring gap in the region's float berths, not a " +
                                 "ride one; the wall set carries the measurement.");
                yield break;
            }

            AssertSheRodeTheTide($"float boat '{subject.name}'", shots);
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

            var shots = new List<Shot>();
            yield return ShootTheSet("player-alongside", states, picture, shots);

            float top = shots[shots.Count - 1].DrawnY;
            Debug.Log($"[{PlateDir}] player's hull at berth {NineMileCreekMooredFleet.PlayerBerthIndex()}: " +
                      $"the wall's drawn lip line is y = {NineMileCreekWharf.MooringEdgeY:0.00}, so at the " +
                      $"highest state photographed her waterline is " +
                      $"{NineMileCreekWharf.MooringEdgeY - top:0.000} units under it");

            AssertSheRodeTheTide("the player's hull alongside", shots);

            // ⭐ THE SORT, at the state where she is nearest the coping. Her ORDER never changes (the face
            // sits on a fixed rung, asserted above), so what the ride can still do is carry her WATERLINE
            // over the top of the wall — at which point she is not alongside the quay, she is on it.
            Assert.Less(top, NineMileCreekWharf.MooringEdgeY,
                "at high water the player's waterline is drawn ABOVE the top of the quay");
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
