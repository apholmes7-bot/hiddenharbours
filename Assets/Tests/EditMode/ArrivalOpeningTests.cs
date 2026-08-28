using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE OPENING — the parts of it that are arithmetic.</b> A new game is piloted in to St Peters
    /// by a skipper; the player steps off onto the wharf and walks up the path to Ginny (owner's ruling,
    /// 2026-08-19).
    ///
    /// <para><b>What is testable without pressing Play, and therefore what is tested here:</b> the
    /// decision (does this save get an arrival), the hand on the helm (a pure function), and the ROUTE —
    /// which is the interesting one, because it is not authored for the arrival at all. It is the
    /// region's own buoyed fairway, read off <see cref="StPetersNavMarks.Entrance"/>, so the first thing
    /// the player sees is a working skipper running the marks in order from seaward. That makes the
    /// arrival inherit a guarantee it never had to state: the fairway is already walked metre by metre
    /// against the terrain by <see cref="NavMarkPlacementTests"/>. What is added below is the same
    /// question asked from the boat's side — <i>can the hull we actually send down it float on it</i> —
    /// because a route that carries its DECLARED draught and a route that carries THIS BOAT are two
    /// statements, and only one of them stops the opening running aground.</para>
    ///
    /// <para>The sequence itself is PlayMode's (<c>ArrivalOpeningPlayTests</c>) — it needs a boat, a
    /// rigidbody and a clock.</para>
    /// </summary>
    public class ArrivalOpeningTests
    {
        private TidalTerrain _terrain;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TidalTerrain_ArrivalTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            GameServices.Reset();
        }

        // =============================================================================================
        // 1. the decision — a fresh save, and ONLY a fresh save
        // =============================================================================================

        /// <summary>
        /// 🔴 <b>ONLY ONE THING MAY PLACE A LOADING PLAYER, and the rest anchor is the referee.</b>
        /// ADR 0037 gave the save a <c>(region, storey, x, y)</c> anchor and
        /// <see cref="RestWakeRestorer"/> to honour it. Its own rule is that an UNSET anchor means
        /// "never rested — the authored spawn stands", and replacing that authored spawn is precisely
        /// what the arrival is for. So the two are exclusive by construction rather than by agreement:
        /// anchor set → she is woken where she slept; anchor unset → she may be landed.
        /// </summary>
        [Test]
        public void APlayerWithARestAnchorIsWokenRatherThanLanded()
        {
            Assert.IsFalse(ArrivalOpening.ShouldPlay(hasRestAnchor: true, alreadyArrived: false),
                "she went to bed somewhere and RestWakeRestorer is about to wake her there — landing " +
                "her on the wharf as well would undo the whole of #580, and would do it silently");
            Assert.IsFalse(ArrivalOpening.ShouldPlay(hasRestAnchor: true, alreadyArrived: true),
                "…and no more so when she has also arrived before");
            Assert.IsTrue(ArrivalOpening.ShouldPlay(hasRestAnchor: false, alreadyArrived: false),
                "no anchor and no landfall is a new game — this IS the opening");
        }

        /// <summary>
        /// 🔴 <b>"No anchor" is NOT "never played", and this is the test that says so.</b> ADR 0037 is
        /// precise: <c>RestRegion == ""</c> means <i>has never turned in</i>. But the save reaches disk
        /// down about a dozen paths that have nothing to do with sleeping — every shop, the licence
        /// service, the outfit locker, <c>ShellFlow.QuitToTitle</c>, and <c>StartingGear</c>, which
        /// fires on the FIRST BOOT before the player has done anything at all. So a player can be an
        /// hour ashore, with a save on disk, and still carry no anchor.
        ///
        /// <para>Gating on the anchor alone would re-land her: she would be picked up off Ginny's
        /// doorstep and put back on a boat. The flag is not a second opinion about freshness — it is
        /// the only witness to a thing nothing else records.</para>
        /// </summary>
        [Test]
        public void APlayerWhoLandedButHasNeverSlept_IsNotLandedAgain()
        {
            Assert.IsFalse(ArrivalOpening.ShouldPlay(hasRestAnchor: false, alreadyArrived: true),
                "she has been landed once and has simply never gone to bed since — buying a rod and " +
                "quitting writes the save without an anchor, and Continue must not put her back aboard");
        }

        // =============================================================================================
        // 2. the hand on the helm — pure, and therefore pinnable
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The determinism claim, stated exactly.</b> It is NOT "she ends in the same pixel" — she
        /// is a rigidbody on a sea recomputed from <c>(worldSeed, gameTime)</c>, and a new game that
        /// starts at a different instant floats her differently, correctly. It is that the COMMAND is a
        /// pure function of the pose: the same boat in the same place pointing the same way at the same
        /// mark always gets the same helm. Driven a thousand times from interleaved states, so a hidden
        /// field or a smuggled clock would have to be consistent with itself across every one of them.
        /// </summary>
        [Test]
        public void TheHelmIsAPureFunctionOfThePose()
        {
            var settings = ArrivalPilot.Settings.Default;
            var poses = new (Vector2 at, float heading, Vector2 vel, Vector2 mark, float toGo)[]
            {
                (new Vector2(340f, 40f),  225f, new Vector2(-2.9f, -0.5f), new Vector2(262f, 26f), 190f),
                (new Vector2(262f, 26f),  245f, new Vector2(-2.0f, -2.4f), new Vector2(255f,  0f),  75f),
                (new Vector2(255f,  0f),  270f, new Vector2(-2.9f,  0.1f), new Vector2(215f,  0f),  40f),
                // ⭐ CRABBING: bow 268°, track due west. A bow-relative reading would call this stopped.
                (new Vector2(230f,  0f),  268f, new Vector2(-2.2f,  1.4f), new Vector2(215f,  0f),  15f),
                (new Vector2(218f,  1.5f), 280f, new Vector2(-0.4f, -0.1f), new Vector2(215f,  0f),   3f),
                (new Vector2(216f, -0.2f), 271f, new Vector2( 0.1f,  0.0f), new Vector2(215f, 0f),   1f),
            };

            var first = new ArrivalPilot.Helm[poses.Length];
            for (int i = 0; i < poses.Length; i++)
                first[i] = ArrivalPilot.Command(poses[i].at, poses[i].heading, poses[i].vel,
                                                poses[i].mark, poses[i].toGo, settings);

            for (int pass = 0; pass < 200; pass++)
            for (int i = poses.Length - 1; i >= 0; i--)      // interleaved, and backwards
            {
                var again = ArrivalPilot.Command(poses[i].at, poses[i].heading, poses[i].vel,
                                                 poses[i].mark, poses[i].toGo, settings);
                Assert.AreEqual(first[i].Throttle, again.Throttle,
                    $"pose {i} gave a different THROTTLE on pass {pass} — the pilot is carrying state, " +
                    "and an opening that is not the same twice cannot be tuned or reviewed");
                Assert.AreEqual(first[i].Steer, again.Steer,
                    $"pose {i} gave a different STEER on pass {pass} — see above");
            }
        }

        /// <summary>She turns TOWARD the mark, and the sign of the helm matches the controller's own
        /// convention (<c>BoatController.RudderTorque</c>: "positive helm → bow right"). A sign error
        /// here is an arrival that drives out to sea, and it is the single easiest thing to get
        /// backwards.</summary>
        [Test]
        public void SheTurnsTowardTheMark_AndTheHelmSignMatchesTheController()
        {
            var s = ArrivalPilot.Settings.Default;
            var at = new Vector2(250f, 0f);

            // Heading due north, mark to the EAST → she must put the helm to STARBOARD (positive).
            Assert.Greater(ArrivalPilot.Command(at, 0f, new Vector2(3f, 0f), at + new Vector2(20f, 0f), 100f, s).Steer, 0f,
                "a mark on the starboard bow must give starboard helm");
            // …and to the WEST → to port.
            Assert.Less(ArrivalPilot.Command(at, 0f, new Vector2(-3f, 0f), at + new Vector2(-20f, 0f), 100f, s).Steer, 0f,
                "a mark on the port bow must give port helm");
            // Dead ahead → amidships.
            Assert.AreEqual(0f, ArrivalPilot.Command(at, 0f, new Vector2(0f, 3f), at + new Vector2(0f, 20f), 100f, s).Steer,
                1e-4f, "a mark dead ahead needs no helm at all");

            // And the long way round is never taken: a mark 179° off is a hard turn, not a 181° one.
            Assert.LessOrEqual(Mathf.Abs(ArrivalPilot.SignedBearingError(at, 350f,
                                                                        at + new Vector2(-1f, 10f))), 180f,
                "a bearing error outside ±180 means the boat will chase a mark the long way round");
        }

        /// <summary>
        /// ⚠ <b>The way comes off her, and the profile that takes it off is the whole approach.</b> The
        /// target speed must fall monotonically to zero as the berth comes up — a boat is stopped by
        /// arriving slowly, not by arriving and then stopping.
        /// </summary>
        [Test]
        public void TheTargetSpeedFallsToNothingByTheBerth_Monotonically()
        {
            var s = ArrivalPilot.Settings.Default;

            Assert.AreEqual(s.CruiseSpeedMetresPerSecond, ArrivalPilot.TargetSpeed(500f, s), 1e-4f,
                "out on the legs she runs at cruise and nothing else");
            Assert.AreEqual(0f, ArrivalPilot.TargetSpeed(s.StopMetres * 0.5f, s), 1e-5f,
                "inside the stopping distance she is stopping — target zero");

            float last = float.MaxValue;
            // Walk in from beyond where the cruise cap lets go — v²/2a + StopMetres — so the whole curve
            // is covered, cap and taper both.
            float from = s.CruiseSpeedMetresPerSecond * s.CruiseSpeedMetresPerSecond
                         / (2f * s.ApproachDecelMetresPerSecondSquared) + s.StopMetres + 5f;
            for (float toGo = from; toGo >= 0f; toGo -= 0.25f)
            {
                float v = ArrivalPilot.TargetSpeed(toGo, s);
                Assert.LessOrEqual(v, last + 1e-5f,
                    $"the target speed went UP {toGo:F2} m from the berth — she would surge on the " +
                    "approach");
                Assert.GreaterOrEqual(v, 0f, "she is never asked to make sternway on the way in");
                last = v;
            }
            Assert.AreEqual(0f, last, 1e-5f, "…and reaches nothing by the berth");
        }

        /// <summary>
        /// 🔴 <b>She must be able to go ASTERN, and this is the test that says why.</b> The cape islander
        /// models at 60 kg against 3 N per m/s of forward drag — a time constant of 20 s — so coasting to
        /// rest from her 3 m/s cruise takes <c>v·τ ≈ 60 m</c> of glide before she is even down to
        /// walking pace, and from the region's own numbers the full stop is the better part of two
        /// hundred metres. That is longer than the dredged channel. A pilot that could only ease its
        /// throttle would therefore arrive at the wharf still making way, every single time, and no
        /// tuning of it would help — which is exactly the shape of bug that looks like "the arrival
        /// feels wrong" rather than like an error.
        /// </summary>
        [Test]
        public void SheGoesAsternWhenSheIsCarryingTooMuchWayForTheDistanceLeft()
        {
            var s = ArrivalPilot.Settings.Default;
            var at = new Vector2(220f, 0f);
            var mark = new Vector2(215f, 0f);

            // Five metres to run and still doing cruise: that is too fast, and the answer is astern.
            float throttle = ArrivalPilot.Command(
                at, 270f, new Vector2(-s.CruiseSpeedMetresPerSecond, 0f), mark, 5f, s).Throttle;
            Assert.Less(throttle, 0f,
                $"with 5 m to run at {s.CruiseSpeedMetresPerSecond:F1} m/s she is commanded " +
                $"{throttle:F2} — ahead. She cannot stop; the arrival will run through the wharf.");

            // …and at the same distance already slow enough, she is not dragged backwards.
            Assert.GreaterOrEqual(
                ArrivalPilot.Command(at, 270f,
                                     new Vector2(-ArrivalPilot.TargetSpeed(5f, s), 0f), mark, 5f, s)
                            .Throttle,
                -1e-4f,
                "a boat already at her target speed must not be commanded astern — she would stop short " +
                "of the berth and the opening would end in open water");

            // Sternway is answered with AHEAD, or a boat that overshoots and backs up never recovers.
            Assert.Greater(ArrivalPilot.Command(at, 270f, new Vector2(0.5f, 0f), mark, 20f, s).Throttle,
                0f, "opening the range with 20 m still to run, she must be given ahead");
        }

        /// <summary>The distance the ease is measured against is along the ROUTE, not the straight line
        /// to the berth — on a route with a turn in it those differ by tens of metres, and the
        /// straight-line answer would have her slowing while still outside the entrance.</summary>
        [Test]
        public void TheEaseIsMeasuredAlongTheRoute_NotAcrossTheHeadland()
        {
            Vector2[] route = StPetersArrivalOpening.Route();
            Assert.GreaterOrEqual(route.Length, 3, "the fairway needs a turn in it for this to mean anything");

            Vector2 start = route[0];
            float along = ArrivalPilot.MetresToBerth(start, route, 0);
            float direct = Vector2.Distance(start, route[route.Length - 1]);

            Assert.Greater(along, direct,
                $"the route measures {along:F0} m and the straight line {direct:F0} m — if those are " +
                "equal the fairway has no turn in it and this route is not the buoyed one");

            // …and it shortens as she runs it, which is the property the ease-down actually depends on.
            float previous = float.MaxValue;
            for (int leg = 0; leg < route.Length; leg++)
            {
                float d = ArrivalPilot.MetresToBerth(route[leg], route, leg);
                Assert.Less(d, previous, $"mark {leg} is no closer to the berth than mark {leg - 1}");
                previous = d;
            }
            Assert.AreEqual(0f, previous, 1e-3f, "the last mark IS the berth");
        }

        /// <summary>
        /// 🔴 <b>THE DEFECT THE OWNER WATCHED, as one assertion.</b> Coming onto the channel she meets a
        /// 26° turn with way on, puts the helm hard over and CRABS — bow one way, track another. The
        /// first version of this pilot closed its throttle loop on her speed along the BOW, which in that
        /// attitude reads near zero while she is still making 2 m/s at the wharf. It therefore opened the
        /// throttle six metres from the berth (measured: 0.78 ahead), drove past, and orbited.
        ///
        /// <para>Speed made good is the question an approach is really asking, and it is what she is
        /// judged on now. A boat crabbing ACROSS her target closes it slowly, so she is told to slow
        /// down; a boat crabbing across it with a component AWAY from it is told to stop.</para>
        /// </summary>
        [Test]
        public void ACrabbingBoatIsJudgedOnWhatSheCloses_NotOnWhatHerBowReads()
        {
            var s = ArrivalPilot.Settings.Default;
            var at = new Vector2(221f, 0f);
            var mark = new Vector2(215f, 0f);          // six metres dead ahead to the west

            // ⚠ A REAL crab: her bow points NNW (330°) while her track is due WEST, straight at the
            // berth. That is what a hull looks like mid-turn with way on — and it is the pose the first
            // version of the pilot read as "nearly stopped".
            const float bowDegrees = 330f;
            var crabbing = new Vector2(-2.4f, 0f);

            Assert.Less(ArrivalPilot.Command(at, bowDegrees, crabbing, mark, 6f, s).Throttle, 0f,
                "crabbing at 2.4 m/s six metres from the berth she must be given ASTERN. Ahead here is " +
                "the overshoot that became a 50 m orbit on the owner's walk.");

            // The reading that used to drive it, and the gap it fell into: along the bow she reads half
            // what she is actually doing over the ground.
            var bow = new Vector2(Mathf.Sin(bowDegrees * Mathf.Deg2Rad),
                                  Mathf.Cos(bowDegrees * Mathf.Deg2Rad));
            float alongBow = Vector2.Dot(crabbing, bow);
            float madeGood = ArrivalPilot.SpeedMadeGood(at, crabbing, mark);
            Assert.Greater(madeGood, alongBow + 1f,
                $"this pose is supposed to CRAB: {madeGood:F2} m/s made good against {alongBow:F2} m/s " +
                "along the bow. If they agree the test is no longer testing anything.");

            // …and a boat sliding AWAY from the berth is stopped, not driven on. 🔴 This is the arm that
            // catches the MIRROR of the crab bug: judged on closing speed alone, a boat past her mark
            // reads as "not arriving" and is given more throttle — measured as a 150 m departure at full
            // ahead. WayToAccountFor is her UNSIGNED ground speed, so way off the mark counts too.
            Assert.Less(ArrivalPilot.Command(at, 268f, new Vector2(2f, 0f), mark, 0f, s).Throttle, 0f,
                "opening the range at the berth must be answered ASTERN, never ahead");

            Assert.AreEqual(2f, ArrivalPilot.WayToAccountFor(at, new Vector2(2f, 0f), mark), 1e-4f,
                "sliding away at 2 m/s she must be accounted 2 m/s of way, not −2");
            Assert.AreEqual(crabbing.magnitude, ArrivalPilot.WayToAccountFor(at, crabbing, mark), 0.01f,
                "crabbing she must be accounted her GROUND speed, not what she is closing the mark at");
        }

        // =============================================================================================
        // 3. the route is the region's, and the region's water carries her
        // =============================================================================================

        /// <summary>
        /// ⭐ The arrival does not author a way in. It reads the buoyed one, so re-cutting the channel or
        /// re-siting the dock re-routes the opening with it, and there is never a second opinion about
        /// where the way in is.
        /// </summary>
        [Test]
        public void TheArrivalRunsTheBuoyedFairway_NotARouteOfItsOwn()
        {
            Vector2[] route = StPetersArrivalOpening.Route();
            Vector2[] fairway = StPetersNavMarks.Entrance.Waypoints;

            Assert.AreEqual(fairway.Length, route.Length,
                "the arrival's route has a different number of marks from the entrance channel — it has " +
                "started being its own route, and the two will drift");
            for (int i = 0; i < fairway.Length; i++)
                Assert.AreEqual(0f, Vector2.Distance(fairway[i], route[i]), 1e-4f,
                    $"mark {i} is at {route[i]} on the arrival's route and {fairway[i]} on the fairway");

            // Seaward FIRST — reverse this and she leaves rather than arrives (and every buoy on the
            // channel changes hands, which is the same fact from the marks' side).
            Assert.Greater(Vector2.Distance(route[0], StPetersBuilder.IslandCenter),
                           Vector2.Distance(route[route.Length - 1], StPetersBuilder.IslandCenter),
                "the route runs OUTWARD — she would be leaving. The direction of buoyage is the " +
                "waypoint order and this is the same order.");
        }

        /// <summary>
        /// 🔴 <b>The S1 ↔ S2 seam, asserted from the boat's side.</b> The fairway states a DECLARED
        /// draught and <see cref="NavMarkPlacementTests"/> holds it to that. This asks the different
        /// question: does the water carry <i>the hull the opening actually sends down it</i>, at the
        /// worst tide the region has? The two are only the same while nobody changes the arrival hull —
        /// and the day somebody does, this is the test that says so instead of the owner discovering it
        /// aground on his own doorstep.
        /// </summary>
        [Test]
        public void TheWaterOnTheRouteFloatsTheArrivalHull_AtTheLowestTide()
        {
            var hull = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatHullDef>(
                "Assets/_Project/Data/Boats/CapeIslander.asset");
            Assert.IsNotNull(hull, "the arrival hull's def must exist");

            float water = StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;
            Vector2[] route = StPetersArrivalOpening.Route();

            float worst = float.MaxValue;
            Vector2 worstAt = Vector2.zero;
            for (int i = 0; i < route.Length - 1; i++)
            {
                float length = Vector2.Distance(route[i], route[i + 1]);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.5f));
                for (int k = 0; k <= steps; k++)
                {
                    Vector2 p = Vector2.Lerp(route[i], route[i + 1], k / (float)steps);
                    float depth = water - _terrain.ElevationAt(p);
                    if (depth < worst) { worst = depth; worstAt = p; }
                }
            }

            Assert.Greater(worst, hull.DraughtMeters,
                $"the arrival route carries only {worst:F2} m at ({worstAt.x:F1}, {worstAt.y:F1}) at " +
                $"spring low, against the {hull.DraughtMeters:F2} m the skipper's boat draws. A new game " +
                "that happens to start at low water would run aground on the way in — which is the exact " +
                "thing the east-berth dredge was ruled for.");

            Debug.Log($"[arrival] the route carries {worst:F2} m at its worst at spring low; she draws " +
                      $"{hull.DraughtMeters:F2} m.");
        }

        // =============================================================================================
        // 4. where she ends up, and where the player is put down
        // =============================================================================================

        /// <summary>Everything about the berth is DERIVED from the region — her heading off the WHARF
        /// FACE she is tied to, her berth off the dock zone, the landing off the ratified disembark
        /// point. A second copy of any of them is a thing that comes apart from the wharf it describes.
        ///
        /// <para>⚠ RE-DERIVED for the 2026-08-22 alongside berth, not merely re-pointed. She used to lie
        /// bow-on at the pier's head and her heading came off the CHANNEL's axis; she now lies ALONGSIDE
        /// the mooring face, so the face is what her heading is read from, and the step ashore is
        /// measured from her GUNWALE rather than from her centre-line — which, on a hull with a 2.4 m
        /// half-beam, was a point inside the boat. <c>StPetersAlongsideBerthTests</c> owns the berth's
        /// own geometry; this holds the ARRIVAL's copy of it to the region.</para></summary>
        [Test]
        public void SheLiesAlongsideTheWharf_AndPutsThePlayerDownOnThePlanks()
        {
            // Bow pointing IN, along the face she is made fast to.
            float expected = ArrivalPilot.CompassOf(StPetersWharf.AxisInward());
            Assert.AreEqual(expected, StPetersArrivalOpening.BerthHeadingDegrees(), 1e-3f,
                "she must lie along the wharf face she is tied to, not across it");

            // She ends where the region says a boat docks…
            Assert.AreEqual(0f, Vector2.Distance(
                    new Vector2(StPetersBuilder.DockZonePos.x, StPetersBuilder.DockZonePos.y),
                    StPetersArrivalOpening.Berth()), 1e-4f,
                "the arrival berth must BE the region's dock zone — ControlSwitcher.InDockZone() is a " +
                "pure distance test, so a berth authored anywhere else is a boat you can never step off");

            // …and the player is put down ON the deck, not in the water beside it.
            Vector2 ashore = StPetersArrivalOpening.StepAshore();
            Rect deck = StPetersWharf.DeckFootprint();
            Assert.IsTrue(deck.Contains(ashore),
                $"the player is put down at ({ashore.x:F1}, {ashore.y:F1}), which is off the deck " +
                $"{deck} — the first step of a new game must land on planks");

            // And the landing is within reach of her RAIL, or stepping ashore is a swim.
            //
            // ⚠ Measured from the gunwale, not from Berth(). Berth() is her CENTRE-LINE: alongside, that
            // is a half-beam inside the hull, so a centre-to-landing measure charges the player for
            // walking across the boat she is standing on. The rail is the thing a person steps over.
            float fromRail = Mathf.Abs(ashore.y - StPetersBuilder.AlongsideGunwaleY);
            Assert.LessOrEqual(fromRail, StPetersBuilder.StepAshoreMetres + 1e-4f,
                $"the landing is {fromRail:F2} m from her rail, past the region's " +
                $"{StPetersBuilder.StepAshoreMetres:F2} m step-ashore pattern");
        }

        // =============================================================================================
        // 5. the skipper is content, not code
        // =============================================================================================

        /// <summary>
        /// He is a <c>BoatOwnerDef</c> like every other skipper in the game, and he keeps the hull the
        /// channel was dredged for. ⚠ Deliberately NOT in <c>Data/Boats/Owners</c> — that folder is the
        /// Nine Mile Creek wharf REGISTER, held to a free berth, a free lot and a unique mark on that
        /// wharf; he holds no berth there.
        /// </summary>
        [Test]
        public void TheSkipperIsAnAsset_AndKeepsTheHullTheChannelWasCutFor()
        {
            var skipper = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatOwnerDef>(
                StPetersArrivalOpening.SkipperPath);
            Assert.IsNotNull(skipper,
                $"no skipper Def at {StPetersArrivalOpening.SkipperPath} — the opening has nobody to " +
                "bring the player in and will decline to run");

            Assert.IsTrue(skipper.Id.StartsWith("owner."), $"'{skipper.Id}': ids are type.snake_case");
            Assert.IsNotEmpty(skipper.DisplayName, "nobody who speaks to the player is nameless");
            Assert.IsNotNull(skipper.Skipper, "he must have a figure, or the deck arrives empty");
            Assert.IsTrue(skipper.IsPresentable(), "his boat must have art, or nothing is drawn");

            Assert.AreEqual(StPetersBuilder.ArrivalHullId, skipper.Boat.Id,
                $"he keeps '{skipper.Boat.Id}' but the channel was dredged for " +
                $"'{StPetersBuilder.ArrivalHullId}' — the depth the region holds and the boat it holds " +
                "it for have come apart");

            Assert.IsFalse(StPetersArrivalOpening.SkipperPath.Contains("/Owners/"),
                "he must not live in the Nine Mile Creek wharf register — everything in there is held " +
                "to a berth and a lot on that wharf, and he keeps neither");
        }

        /// <summary>
        /// 🔴 <b>THE PILOT PASSES BETWEEN THE MARKS.</b> The owner watched the opening on
        /// 2026-08-27 and said the skipper runs THROUGH the buoys on the way in. He was right, and it
        /// was not the pilot: a turn's pair was offset square to the leg it arrives on, so at the
        /// entrance's 64.8° and 67.3° corners the inside mark stood 4.26 m and 3.85 m off the route
        /// — inside the beam of the hull the channel was dredged for. The marks mean "the channel is
        /// between us"; a route that shaves one of them makes them furniture.
        ///
        /// <para>Asserted along the WHOLE route rather than at its waypoints: the fault lived exactly
        /// between two of them, which is the blind spot a corner defect always hides in.</para>
        ///
        /// <para>⚠⚠ <b>THE FLOOR IS THE FAIRWAY'S OWN HALF-WIDTH, and the first draft of this test
        /// used the hull's beam instead — which would have PASSED on the broken code.</b> The old
        /// geometry left 3.86 m, comfortably clear of a 2.40 m half-beam plus a metre; the owner
        /// still watched the skipper go through the marks, because a hull with a 17.7 m turning
        /// circle does not travel her own polyline round a 65° corner — she cuts inside it. So the
        /// claim worth guarding here is not "she fits", it is <b>the route runs down the MIDDLE of
        /// the channel it is marked with</b>: every mark stands at the channel's stated half-width,
        /// which is the only version of this the plan can promise. What the TRACK does with that room
        /// belongs to PlayMode, where there is a hull with a turning circle.</para>
        /// </summary>
        [Test]
        public void TheRouteThreadsTheMarks_RatherThanRunningThroughThem()
        {
            float clearance = ClosestMarkToTheRoute(StPetersNavMarks.Tuning,
                                                    out string who, out Vector2 where);
            float claimed = StPetersNavMarks.Entrance.HalfWidthMetres;
            float beam = StPetersBuilder.ArrivalHullHalfBeamMetres;

            Assert.Greater(clearance, claimed - MarkPassingToleranceMetres,
                $"the arrival passes {clearance:F2} m from '{who}' at ({where.x:F1}, {where.y:F1}), " +
                $"on a fairway that claims {claimed:F2} m each side. She is not running the middle " +
                "of her own channel, which means a mark has come inside it — and a hull rounding " +
                "that corner cuts inside her polyline before she ever gets there.");

            // The plain-English consequence, kept as its own line so a failure says WHICH claim broke.
            Assert.Greater(clearance, beam + MarkPassingClearanceMetres,
                $"she passes {clearance:F2} m from '{who}' against {beam:F2} m of half-beam — she " +
                "is shaving it.");

            Debug.Log($"[arrival] her closest mark is '{who}' at {clearance:F2} m, on a " +
                      $"{claimed:F2} m fairway, against a {beam:F2} m half-beam.");
        }

        /// <summary>
        /// ⭐ <b>The negative control: the guard above, run against the geometry that HAD the defect.</b>
        /// A clearance test passes just as happily when there is nothing near the route to measure, so
        /// the microphone has to be proved live — and the honest way to prove it is to rebuild the
        /// pre-2026-08-27 mark positions and watch the same floor fail.
        ///
        /// <para>⚠⚠ <b>The first draft of this control was wrong, and only running it said so.</b> It
        /// set <see cref="NavMarkTuning.TurnMitreLimit"/> to 1, on the theory that "no mitre" is the old
        /// behaviour. It is not: clamping the mitre still offsets along the BISECTOR and merely narrows
        /// the marked width to <c>halfWidth·cos(turn/2)</c> — 8.32 m here, comfortably clear of the
        /// floor. The old code offset along the INBOUND leg's normal, a different direction entirely. A
        /// knob is not a time machine: to reproduce old arithmetic, write the old arithmetic.</para>
        /// </summary>
        [Test]
        public void AndWithTheOldCornerGeometry_ThatGuardWouldFail()
        {
            NavChannel entrance = StPetersNavMarks.Entrance;
            NavChannelFairway fairway = NavMarkPlan.DeriveFairway(
                entrance, _terrain.ElevationAt, StPetersNavMarks.Tuning);
            Vector2[] route = StPetersArrivalOpening.Route();

            float clearance = float.MaxValue;
            string who = "(no mark)";
            for (int s = 0; s < fairway.Stations.Count; s++)
            foreach (NavChannelHand hand in new[] { NavChannelHand.Port, NavChannelHand.Starboard })
            {
                // The arithmetic exactly as it shipped until 2026-08-27: square to the leg the
                // station lies ON, which at a vertex is the one she ARRIVES on.
                Vector2 at = fairway.Stations[s]
                           + NavChannelGeometry.Normal(fairway.Course[s], hand)
                             * entrance.HalfWidthMetres;

                float d = ClosestApproach(route, at);
                if (d < clearance) { clearance = d; who = $"station {s}, {hand} hand"; }
            }

            float floor = entrance.HalfWidthMetres - MarkPassingToleranceMetres;

            Assert.Less(clearance, floor,
                $"the old geometry left {clearance:F2} m at {who}, which clears the {floor:F2} m " +
                "floor. Either the route no longer turns sharply enough for that defect to bite — in " +
                "which case delete this control — or the guard above is measuring something that " +
                "cannot fail.");
        }

        /// <summary>How much water the arrival is owed beside her own beam when she passes a mark. A
        /// metre: she is steered past it by a person, not threaded through it by a machine.</summary>
        private const float MarkPassingClearanceMetres = 1f;

        /// <summary>
        /// How far off the channel's stated half-width a mark may sit before the route has stopped
        /// running the middle of it. Half a metre — tight on purpose, because on a route whose marks
        /// are DERIVED from that half-width the honest reading is exactly it (10.00 m, measured), and
        /// anything looser is a tolerance for a defect rather than for arithmetic.
        /// </summary>
        private const float MarkPassingToleranceMetres = 0.5f;

        /// <summary>
        /// The closest any planned lateral of the ENTRANCE comes to the line the arrival actually
        /// sails, walked at half-metre stations from seaward to the berth.
        /// </summary>
        private float ClosestMarkToTheRoute(NavMarkTuning tuning, out string who, out Vector2 where)
        {
            NavMarkPlanResult plan = NavMarkPlan.Plan(
                StPetersNavMarks.Channels, StPetersNavMarks.Cardinals, _terrain.ElevationAt,
                StPetersBuilder.TideMean, StPetersBuilder.TideAmplitude, tuning);

            Vector2[] route = StPetersArrivalOpening.Route();
            float best = float.MaxValue;
            who = "(no mark)";
            where = Vector2.zero;

            foreach (PlannedNavMark m in plan.Marks)
            {
                if (!m.IsLateral || m.OwnerId != StPetersNavMarks.Entrance.Id) continue;

                float d = ClosestApproach(route, m.At, out Vector2 at);
                if (d < best) { best = d; who = m.Id; where = at; }
            }
            return best;
        }

        /// <summary>How close the arrival comes to a point, walked at half-metre stations along the
        /// WHOLE route — not sampled at its waypoints, where a corner defect is invisible.</summary>
        private static float ClosestApproach(Vector2[] route, Vector2 point) =>
            ClosestApproach(route, point, out _);

        private static float ClosestApproach(Vector2[] route, Vector2 point, out Vector2 where)
        {
            float best = float.MaxValue;
            where = Vector2.zero;
            for (int i = 0; i < route.Length - 1; i++)
            {
                float length = Vector2.Distance(route[i], route[i + 1]);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.5f));
                for (int k = 0; k <= steps; k++)
                {
                    Vector2 p = Vector2.Lerp(route[i], route[i + 1], k / (float)steps);
                    float d = Vector2.Distance(p, point);
                    if (d < best) { best = d; where = p; }
                }
            }
            return best;
        }

        /// <summary>
        /// ⚠⚠ <b>And the region's own moored dory is not in the fairway either.</b> The same eyeball
        /// pass found her lying on the channel's centre-line at (215, 0), 5.29 m off the line the
        /// skipper runs — a berth IN a fairway rather than beside one. She is a boat and not a mark,
        /// so she owes the arrival both half-beams as well as the passing clearance.
        /// </summary>
        [Test]
        public void TheMooredDoryIsClearOfTheArrivalTrack()
        {
            Vector2[] route = StPetersArrivalOpening.Route();
            var moored = new Vector2(StPetersBuilder.DoryMooredPos.x, StPetersBuilder.DoryMooredPos.y);

            float best = float.MaxValue;
            for (int i = 0; i < route.Length - 1; i++)
            {
                float length = Vector2.Distance(route[i], route[i + 1]);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.5f));
                for (int k = 0; k <= steps; k++)
                    best = Mathf.Min(best, Vector2.Distance(
                        Vector2.Lerp(route[i], route[i + 1], k / (float)steps), moored));
            }

            float needed = StPetersBuilder.ArrivalHullHalfBeamMetres
                         + StPetersBuilder.DoryHalfBeamMetres
                         + MarkPassingClearanceMetres;

            Assert.Greater(best, needed,
                $"the arrival passes {best:F2} m from the moored dory at ({moored.x:F1}, " +
                $"{moored.y:F1}), and two hulls plus a passing gap need {needed:F2} m. The owner " +
                "watched this happen on 2026-08-27: 'the test dory is in the way of the arrival " +
                "boat'.");
        }
    }
}
