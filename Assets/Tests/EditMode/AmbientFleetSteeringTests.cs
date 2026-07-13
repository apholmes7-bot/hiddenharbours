using System;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The ambient fleet's local STEERING (canon M2-33): separation (two converging boats give way
    /// and pass, never deadlock bow-to-bow), and the tide-aware shoal look-ahead (a bow pointed at
    /// shallow water swings toward the deeper side and eases down). Pure vector maths, headless.
    /// <para>Plus the seamanship model (owner feedback on #189 — "spinning in circles"): the gated
    /// starboard bias, arrive-and-lie-to with hysteresis, turn-with-way, and property-style
    /// convergence sims over <see cref="AmbientFleetSteering.Step"/> — the exact integrator the
    /// presenter runs — proving no start position/heading can trap a boat in an orbit.</para>
    /// </summary>
    public class AmbientFleetSteeringTests
    {
        /// <summary>A Def with pure C# defaults — the same values the shipped asset inherits for the
        /// appended seamanship fields.</summary>
        private static AmbientFleetDef Defaults() => ScriptableObject.CreateInstance<AmbientFleetDef>();

        // ---- repulsion ---------------------------------------------------------------------------

        [Test]
        public void Repulsion_PushesAwayFromANearObstacle_AndIgnoresFarOnes()
        {
            var obstacles = new[] { new Vector2(1f, 0f) };
            Vector2 push = AmbientFleetSteering.Repulsion(Vector2.zero, obstacles, 1, 5f);
            Assert.Less(push.x, 0f, "must push away (−x) from an obstacle at +x");
            Assert.AreEqual(0.8f, push.magnitude, 1e-4f, "linear falloff: 1 − d/R = 1 − 1/5");

            obstacles[0] = new Vector2(10f, 0f);
            Assert.AreEqual(Vector2.zero, AmbientFleetSteering.Repulsion(Vector2.zero, obstacles, 1, 5f),
                            "outside the radius there is nothing to shoulder away from");
        }

        [Test]
        public void Repulsion_SkipsACoLocatedEntry_SoASharedListCanIncludeTheBoatItself()
        {
            var obstacles = new[] { new Vector2(0f, 0f), new Vector2(2f, 0f) };
            Vector2 push = AmbientFleetSteering.Repulsion(Vector2.zero, obstacles, 2, 5f);
            Assert.Less(push.x, 0f, "only the real neighbour contributes");
            Assert.AreEqual(0f, push.y, 1e-5f);
        }

        // ---- head-on composition -------------------------------------------------------------------

        [Test]
        public void ComposeHeading_BreaksAHeadOnStandoff_Deterministically()
        {
            // Seek dead ahead, avoidance dead astern (the exact cancellation of a bow-to-bow meet):
            // the starboard bias must yield a real, unit direction — never a zero deadlock.
            Vector2 desired = AmbientFleetSteering.ComposeHeading(new Vector2(1f, 0f), new Vector2(-1f, 0f));
            Assert.AreEqual(1f, desired.magnitude, 1e-4f);
            Assert.AreNotEqual(0f, desired.y, "the tiebreak must veer off the line of approach");

            Vector2 again = AmbientFleetSteering.ComposeHeading(new Vector2(1f, 0f), new Vector2(-1f, 0f));
            Assert.AreEqual(desired, again, "the tiebreak is deterministic");
        }

        [Test]
        public void ComposeHeading_WithNoAvoidance_IsJustTheSeek()
        {
            Vector2 desired = AmbientFleetSteering.ComposeHeading(new Vector2(0f, 2f), Vector2.zero);
            Assert.AreEqual(0f, desired.x, 1e-5f);
            Assert.AreEqual(1f, desired.y, 1e-5f);
        }

        [Test]
        public void ComposeHeading_GlancingAvoidance_ComposesStraight_NoSidewaysCurl()
        {
            // A neighbour abeam pushes at 90° to the course. The old UNCONDITIONAL starboard bias
            // curled every such push sideways — turning "keep clear" into a stable orbit around the
            // avoided thing (the owner's "spinning in circles"). Gated to near-head-on, a glancing
            // push composes straight: the seek (yielded by the push's strength) plus the push, with
            // NO bias term.
            Vector2 seek = new Vector2(1f, 0f);
            Vector2 avoid = new Vector2(0f, 0.5f);
            Vector2 expected = (seek * (1f - 0.5f) + avoid).normalized;
            Vector2 desired = AmbientFleetSteering.ComposeHeading(seek, avoid);
            Assert.AreEqual(expected.x, desired.x, 1e-5f, "no tangential curl on a glancing push");
            Assert.AreEqual(expected.y, desired.y, 1e-5f, "no tangential curl on a glancing push");
        }

        [Test]
        public void ComposeHeading_ASaturatedPush_BeatsTheSeekOutright_PureGiveWay()
        {
            // Something big parked hard against her (push at full saturation): she does not press
            // toward a blocked mark — the compose is pure give-way, and the demand's resolve reads
            // as decisive (the push has somewhere to send her).
            Vector2 desired = AmbientFleetSteering.ComposeHeading(new Vector2(1f, 0f), new Vector2(0f, 1f),
                                                                  out float resolve01);
            Assert.AreEqual(0f, desired.x, 1e-5f, "the seek is fully yielded at saturation");
            Assert.AreEqual(1f, desired.y, 1e-5f, "pure give-way along the push");
            Assert.AreEqual(1f, resolve01, 1e-5f, "a clear give-way is a decisive demand");
        }

        [Test]
        public void ComposeHeading_NearAStandoff_ReportsALowResolve_SoTheBoatChecksHerWay()
        {
            // Seek and a strong push nearly cancelling (the player sitting between her and her spot):
            // the demand is indecisive and the resolve must tend low — the boat WAITS instead of
            // ringing the blockage at full manoeuvring speed.
            Vector2 seek = new Vector2(1f, 0f);
            Vector2 avoid = AmbientFleetSteering.Rotate(new Vector2(-0.6f, 0f), 25f);   // opposed at 155°
            AmbientFleetSteering.ComposeHeading(seek, avoid, out float resolve01);
            Assert.Less(resolve01, 0.6f, "an argued demand must not read as a full-speed order");
        }

        // ---- two converging boats diverge ----------------------------------------------------------

        [Test]
        public void TwoConvergingBoats_GiveWay_PassClear_AndStillReachTheirMarks()
        {
            // Boat A sails +x for (10,0); boat B sails −x for (−10,0): a dead head-on meet. Step the
            // exact update the presenter runs (repulsion → compose → rate-limited turn → move) and the
            // pair must keep clear and still make their marks — competent small-boat handling.
            const float dt = 0.05f, speed = 2f, turnRate = 90f, avoidRadius = 6f;
            var pos = new[] { new Vector2(-10f, 0f), new Vector2(10f, 0f) };
            var heading = new[] { new Vector2(1f, 0f), new Vector2(-1f, 0f) };
            var target = new[] { new Vector2(10f, 0f), new Vector2(-10f, 0f) };

            float minSeparation = float.MaxValue;
            var reached = new[] { false, false };
            var buffer = new Vector2[2];

            for (int step = 0; step < 600; step++)
            {
                buffer[0] = pos[0]; buffer[1] = pos[1];
                for (int i = 0; i < 2; i++)
                {
                    Vector2 toTarget = target[i] - pos[i];
                    if (toTarget.magnitude < 1.2f) { reached[i] = true; continue; }
                    Vector2 seek = toTarget.normalized;
                    Vector2 avoid = AmbientFleetSteering.Repulsion(pos[i], buffer, 2, avoidRadius);
                    Vector2 desired = AmbientFleetSteering.ComposeHeading(seek, avoid);
                    heading[i] = AmbientFleetSteering.RotateToward(heading[i], desired, turnRate * dt);
                    pos[i] += heading[i] * (speed * dt);
                }
                minSeparation = Mathf.Min(minSeparation, Vector2.Distance(pos[0], pos[1]));
                if (reached[0] && reached[1]) break;
            }

            Assert.Greater(minSeparation, 1.5f, "the boats must give way, not drive through each other");
            Assert.IsTrue(reached[0] && reached[1], "giving way must not stop either boat making her mark");
        }

        // ---- the shoal look-ahead -------------------------------------------------------------------

        [Test]
        public void DepthAvoid_IsSilent_InClearWater()
        {
            static float Deep(Vector2 p) => 10f;
            Vector2 steer = AmbientFleetSteering.DepthAvoid(Vector2.zero, Vector2.right, 6f, 40f,
                                                            Deep, 1f, out float speedScale);
            Assert.AreEqual(Vector2.zero, steer);
            Assert.AreEqual(1f, speedScale);
        }

        [Test]
        public void DepthAvoid_SwingsTowardTheDeeperBow_AndEasesDown_WhenShoalAhead()
        {
            // Shallow east of x = 5; the bow probe (at x = 6) shoals, both 40° probes (x ≈ 4.6) are
            // still deep — she must swing off the line and slow, not plow on at full speed.
            static float Shelf(Vector2 p) => p.x < 5f ? 10f : 0.1f;
            Vector2 steer = AmbientFleetSteering.DepthAvoid(Vector2.zero, Vector2.right, 6f, 40f,
                                                            Shelf, 1f, out float speedScale);
            Assert.Greater(steer.magnitude, 0.1f, "a shoal ahead must produce a correction");
            Assert.Less(Vector2.Dot(steer.normalized, Vector2.right), 0.9f, "the correction turns her OFF the shoal line");
            Assert.Less(speedScale, 1f, "she eases down feeling her way");
        }

        [Test]
        public void DepthAvoid_BacksOff_WhenShoalAllRoundTheBow()
        {
            static float Wall(Vector2 p) => p.x > -1f ? 0.1f : 10f;
            Vector2 steer = AmbientFleetSteering.DepthAvoid(Vector2.zero, Vector2.right, 6f, 40f,
                                                            Wall, 1f, out float speedScale);
            Assert.Less(steer.x, 0f, "with both bows shoal she comes astern-ward");
            Assert.LessOrEqual(speedScale, 0.2f, "and all but stops");
        }

        // ---- the rate-limited turn ------------------------------------------------------------------

        [Test]
        public void RotateToward_SwingsAtMostTheGivenRate()
        {
            Vector2 swung = AmbientFleetSteering.RotateToward(Vector2.right, Vector2.up, 30f);
            Assert.AreEqual(30f, Vector2.SignedAngle(Vector2.right, swung), 1e-3f, "a 90° demand meets the 30° cap");

            Vector2 snapped = AmbientFleetSteering.RotateToward(Vector2.right, Vector2.up, 180f);
            Assert.AreEqual(90f, Vector2.SignedAngle(Vector2.right, snapped), 1e-3f, "inside the cap it reaches the demand");
        }

        // ---- the seamanship model: settle, turn-with-way, bow-relative corrections -----------------

        [Test]
        public void HoldStation_HasHysteresis_TightToEnter_LooseToWake()
        {
            // Not yet holding: a push above the enter gate blocks the settle; a faint one does not.
            Assert.IsFalse(AmbientFleetSteering.HoldStation(false, 1f, 1.2f, 2f, 0.3f, 0.25f, 0.6f),
                           "a real push keeps her standing off");
            Assert.IsTrue(AmbientFleetSteering.HoldStation(false, 1f, 1.2f, 2f, 0.1f, 0.25f, 0.6f),
                          "a faint push does not stop her lying-to");
            Assert.IsFalse(AmbientFleetSteering.HoldStation(false, 1.5f, 1.2f, 2f, 0f, 0.25f, 0.6f),
                           "outside the hold radius she cannot settle");

            // Held: the SAME 0.3 push that blocked entry does NOT wake her — that gap is the
            // hysteresis that stops a drifting-past player rousing a working boat into circles.
            Assert.IsTrue(AmbientFleetSteering.HoldStation(true, 1f, 1.2f, 2f, 0.3f, 0.25f, 0.6f),
                          "the hysteresis band holds her");
            Assert.IsFalse(AmbientFleetSteering.HoldStation(true, 1f, 1.2f, 2f, 0.7f, 0.25f, 0.6f),
                           "a real shove dislodges her");
            Assert.IsFalse(AmbientFleetSteering.HoldStation(true, 2.5f, 1.2f, 2f, 0f, 0.25f, 0.6f),
                           "displaced beyond the release ring she re-takes her spot");
        }

        [Test]
        public void TurnRateScale_TurnsWithWay_NeverBelowBareSteerage()
        {
            Assert.AreEqual(0.45f, AmbientFleetSteering.TurnRateScale(0f, 0.45f), 1e-5f, "stopped: bare steerage only");
            Assert.AreEqual(0.7f, AmbientFleetSteering.TurnRateScale(0.7f, 0.45f), 1e-5f, "under way: the bow swings with the way she carries");
            Assert.AreEqual(1f, AmbientFleetSteering.TurnRateScale(1f, 0.45f), 1e-5f, "full cruise: full rate");
        }

        [Test]
        public void ArriveSpeedFraction_EasesDownOnApproach_AndThroughAHardTurn()
        {
            Assert.AreEqual(1f, AmbientFleetSteering.ArriveSpeedFraction(10f, 5f, 0f, 90f, 0.35f), 1e-5f,
                            "far off with the bow on the line: full speed");
            Assert.AreEqual(0.5f, AmbientFleetSteering.ArriveSpeedFraction(2.5f, 5f, 0f, 90f, 0.35f), 1e-5f,
                            "half the arrive radius: half speed");
            Assert.AreEqual(0.35f, AmbientFleetSteering.ArriveSpeedFraction(10f, 5f, 90f, 90f, 0.35f), 1e-5f,
                            "hard over: slow through the turn");
            Assert.AreEqual(0.35f, AmbientFleetSteering.ArriveSpeedFraction(10f, 5f, 180f, 90f, 0.35f), 1e-5f,
                            "a come-about eases no further than the floor");
        }

        [Test]
        public void RotateFromTo_KeepsAStoredCorrectionBowRelative()
        {
            // A correction probed with the bow north, re-expressed after the bow swung to east,
            // must swing with it (−90°): starboard-of-old-bow stays starboard-of-new-bow.
            Vector2 swung = AmbientFleetSteering.RotateFromTo(new Vector2(1f, 0f), Vector2.up, Vector2.right);
            Assert.AreEqual(0f, swung.x, 1e-5f);
            Assert.AreEqual(-1f, swung.y, 1e-5f);

            Assert.AreEqual(Vector2.zero, AmbientFleetSteering.RotateFromTo(Vector2.zero, Vector2.up, Vector2.right),
                            "a zero correction stays zero");
        }

        // ---- convergence: from ANY start she arrives and settles — no stable orbit anywhere ---------

        /// <summary>Runs <see cref="AmbientFleetSteering.Step"/> — the presenter's exact drive loop —
        /// with no obstacles, and reports whether she settled, how far she ended from the spot, and
        /// her cumulative bow swing (an orbit or a spin accumulates unbounded swing; honest
        /// seamanship needs at most a come-about plus corrections).</summary>
        private static (bool settled, float finalDist, float totalSwingDegrees) SimulateToSpot(
            AmbientFleetDef def, Vector2 start, Vector2 bow, float way, Vector2 spot,
            float cruiseSpeed, float dt, float seconds)
        {
            Vector2 pos = start;
            Vector2 heading = bow.normalized;
            bool holding = false;
            float swing = 0f;
            int steps = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < steps; i++)
            {
                Vector2 prev = heading;
                AmbientFleetSteering.Step(ref pos, ref heading, ref way, ref holding, spot,
                                          Vector2.zero, Vector2.zero, 1f, cruiseSpeed, def, dt);
                swing += Mathf.Abs(Vector2.SignedAngle(prev, heading));
            }
            return (holding, Vector2.Distance(pos, spot), swing);
        }

        [Test]
        public void Step_FromAwkwardStarts_ArrivesAndSettles_NeverOrbits()
        {
            var def = Defaults();
            try
            {
                float cruise = def.MaxSpeedMetersPerSecond;   // the fastest seeded boat is the worst case
                var starts = new (Vector2 pos, Vector2 bow, float way, string name)[]
                {
                    (new Vector2(0f, 20f), Vector2.up, 1f, "spot dead astern at full way"),
                    (new Vector2(12f, 0f), Vector2.up, 1f, "spot abeam"),
                    (new Vector2(2f, 0f), Vector2.up, 1f, "inside the arrive radius at full way (the classic orbit rim)"),
                    (new Vector2(0f, 3f), Vector2.up, 0f, "getting under way just off the spot, bow the wrong way"),
                };
                foreach (var c in starts)
                {
                    var r = SimulateToSpot(def, c.pos, c.bow, c.way, Vector2.zero, cruise, 0.05f, 120f);
                    Assert.IsTrue(r.settled, $"{c.name}: she must arrive and lie-to");
                    Assert.LessOrEqual(r.finalDist, def.HoldRadius * 1.01f, $"{c.name}: settled means AT the spot");
                    Assert.Less(r.totalSwingDegrees, 900f,
                                $"{c.name}: bounded total bow swing — an orbit or a spin fails this");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(def); }
        }

        // ---- two boats crossing: give way, pass, and both still settle ------------------------------

        [Test]
        public void TwoBoatsCrossing_GiveWay_Separate_AndBothSettleAtTheirSpots()
        {
            var def = Defaults();
            try
            {
                const float dt = 0.05f, cruise = 2f;
                var pos = new[] { new Vector2(-12f, 0f), new Vector2(0f, -12f) };
                var bow = new[] { Vector2.right, Vector2.up };
                var way = new[] { 1f, 1f };
                var holding = new[] { false, false };
                var spot = new[] { new Vector2(12f, 0f), new Vector2(0f, 12f) };
                var buffer = new Vector2[2];
                var swing = new float[2];
                float minSep = float.MaxValue;

                for (int step = 0; step < 2400; step++)   // 120 sim-seconds
                {
                    buffer[0] = pos[0]; buffer[1] = pos[1];
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 prev = bow[i];
                        Vector2 social = AmbientFleetSteering.Repulsion(pos[i], buffer, 2, def.BoatAvoidRadius);
                        AmbientFleetSteering.Step(ref pos[i], ref bow[i], ref way[i], ref holding[i],
                                                  spot[i], social, Vector2.zero, 1f, cruise, def, dt);
                        swing[i] += Mathf.Abs(Vector2.SignedAngle(prev, bow[i]));
                    }
                    minSep = Mathf.Min(minSep, Vector2.Distance(pos[0], pos[1]));
                }

                Assert.Greater(minSep, 1.5f, "crossing boats must give way, never drive through each other");
                for (int i = 0; i < 2; i++)
                {
                    Assert.IsTrue(holding[i], $"boat {i}: the pass must resolve — she continues and settles");
                    Assert.LessOrEqual(Vector2.Distance(pos[i], spot[i]), def.HoldRadius * 1.01f, $"boat {i} at her mark");
                    Assert.Less(swing[i], 900f, $"boat {i}: passing must not read as mutual circling");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(def); }
        }

        // ---- the settle is honest around the player: near ≠ woken, parked-on-top = give way ---------

        [Test]
        public void SheSettlesNearTheParkedPlayer_SleepsThroughADriftPast_AndGivesWayToAShove()
        {
            var def = Defaults();
            try
            {
                const float dt = 0.05f;
                float cruise = def.MaxSpeedMetersPerSecond;
                Vector2 spot = Vector2.zero;
                Vector2 pos = new Vector2(-15f, 0f);
                Vector2 bow = Vector2.right;
                float way = 1f;
                bool holding = false;
                var player = new Vector2[1];

                // 1) The player parked 6.5 m off her spot exerts a faint push (< the enter gate):
                //    under the OLD all-or-nothing hold gate any push kept her under way forever,
                //    circling — she must simply come alongside and lie-to.
                player[0] = new Vector2(6.5f, 0f);
                for (int i = 0; i < 1200 && !holding; i++)
                {
                    Vector2 social = AmbientFleetSteering.Repulsion(pos, player, 1, def.PlayerAvoidRadius);
                    AmbientFleetSteering.Step(ref pos, ref bow, ref way, ref holding, spot,
                                              social, Vector2.zero, 1f, cruise, def, dt);
                }
                Assert.IsTrue(holding, "a faint push from the parked player must not stop her settling");
                Vector2 settledHeading = bow;

                // 2) The player drifts past 4 m abeam (peak push ≈ 0.5, inside the hysteresis band):
                //    she stays lying-to, bow steady — never woken into circles.
                for (int i = 0; i < 200; i++)
                {
                    player[0] = Vector2.Lerp(new Vector2(6f, 4f), new Vector2(-6f, 4f), i / 199f);
                    Vector2 social = AmbientFleetSteering.Repulsion(pos, player, 1, def.PlayerAvoidRadius);
                    AmbientFleetSteering.Step(ref pos, ref bow, ref way, ref holding, spot,
                                              social, Vector2.zero, 1f, cruise, def, dt);
                    Assert.IsTrue(holding, "the drifting-past player must not wake a working boat");
                }
                Assert.AreEqual(settledHeading, bow, "lying-to means bow steady — no rotating in place");

                // 3) The player parked almost on top of her IS a real shove — she gives way.
                player[0] = pos + new Vector2(1.2f, 0f);
                for (int i = 0; i < 300; i++)
                {
                    Vector2 social = AmbientFleetSteering.Repulsion(pos, player, 1, def.PlayerAvoidRadius);
                    AmbientFleetSteering.Step(ref pos, ref bow, ref way, ref holding, spot,
                                              social, Vector2.zero, 1f, cruise, def, dt);
                }
                Assert.IsFalse(holding, "a real shove dislodges her");
                Assert.Greater(Vector2.Distance(pos, player[0]), 2.5f, "she gives way — opens a real berth");
            }
            finally { UnityEngine.Object.DestroyImmediate(def); }
        }

        [Test]
        public void Step_WithAPausedClock_FreezesEverything()
        {
            var def = Defaults();
            try
            {
                Vector2 pos = new Vector2(5f, 0f);
                Vector2 bow = Vector2.up;
                float way = 0.8f;
                bool holding = false;
                AmbientFleetSteering.Step(ref pos, ref bow, ref way, ref holding, Vector2.zero,
                                          Vector2.zero, Vector2.zero, 1f, 2f, def, 0f);
                Assert.AreEqual(new Vector2(5f, 0f), pos, "no drift on a paused clock");
                Assert.AreEqual(Vector2.up, bow, "no swing on a paused clock");
                Assert.AreEqual(0.8f, way, "way unchanged on a paused clock");
                Assert.IsFalse(holding, "no state flips on a paused clock");
            }
            finally { UnityEngine.Object.DestroyImmediate(def); }
        }
    }
}
