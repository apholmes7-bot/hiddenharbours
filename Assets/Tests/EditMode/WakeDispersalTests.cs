using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Guards for the 2026-08-07 boat playtest's two wake defects, verbatim:
    ///
    /// <list type="number">
    /// <item>the trail is <i>"too solid white"</i> — it should be <i>"bubbling as it goes and dispersing
    /// back to water over time as the tide and wind gradually manipulate it"</i>;</item>
    /// <item>the wake <i>"originates at the CENTRE of the hull"</i>, obvious when turning — it must come
    /// off the transom.</item>
    /// </list>
    ///
    /// <para><b>These pin MECHANISMS, not looks.</b> Whether the wake reads as bubbles is the owner's
    /// eyeball and no test asserts it. What is testable is the geometry that produced the centre-of-hull
    /// read, and the fact that each new dispersal term has a real live input and an honest off switch.</para>
    ///
    /// <para><b>No absolute bars.</b> Every threshold here is either exact (an off switch, an identity) or
    /// a comparison between two computed quantities — never "the foam must be dimmer than 0.4", which is
    /// tuning and would expire the first time the owner turns a knob
    /// (<c>rendering-guards-rot-two-ways</c>).</para>
    /// </summary>
    public class WakeDispersalTests
    {
        private static WakeTrailConfig Cfg() => WakeTrailConfig.Default;

        // ==== DEFECT 2: the trail's root, and the turn that exposed it ==================================

        /// <summary>
        /// The mechanism behind the owner's read, stated as arithmetic. A boat under way and turning
        /// swings her stern anchor sideways at <c>ω·r</c>; with <c>r</c> = half a hull that rivals her
        /// headway, so the stern-swept segment — what the trail used to be laid along — points a long way
        /// off her course. Everything keyed to it (the V arms, the shoulder laterals, the churn band, the
        /// stern roll) rotated with it, and the deposits strung out along an arc about amidships.
        /// </summary>
        [Test]
        public void InATurn_TheSternSweptSegment_PointsFarOffTheCourse_TheCourseDoesNot()
        {
            var c = Cfg();
            const float loa = 7f;                 // the work skiff the owner was driving
            const float speed = 6f;               // under way
            const float turnDegPerSec = 90f;      // turning at speed
            const float dt = 1f / 30f;            // the emitter's tick

            Vector2 bow0 = Vector2.up;
            Vector2 bow1 = Rotate(bow0, -turnDegPerSec * dt);
            Vector2 pos0 = Vector2.zero;
            Vector2 pos1 = pos0 + bow0 * (speed * dt);

            Vector2 stern0 = WakeGrading.SternAnchor(pos0, bow0, loa, c.DepositAsternOffset, 90f);
            Vector2 stern1 = WakeGrading.SternAnchor(pos1, bow1, loa, c.DepositAsternOffset, 90f);

            Vector2 course = pos1 - pos0;
            Vector2 sternSwept = stern1 - stern0;

            float courseOff = Vector2.Angle(course, bow0);
            float sternOff = Vector2.Angle(sternSwept, bow0);

            Assert.Less(courseOff, 1f,
                "the hull's own travel over one tick is her heading — if this drifts, the harness is wrong, " +
                "not the code under test.");
            Assert.Greater(sternOff, 20f,
                "this is the defect: the stern anchor's swept segment is dominated by its SWING ABOUT THE " +
                "BOAT'S CENTRE, so laying the trail along it fans the wake around amidships. If this ever " +
                "reads small the geometry has changed and the fix below is guarding nothing.");
            Assert.Greater(sternOff, courseOff * 10f,
                "the two must be far apart, or blending between them (TrackVector) cannot matter.");
        }

        /// <summary>The blend is an honest A/B at both ends: 1 reproduces the shipped stern-swept track
        /// bit-for-bit, 0 lays purely along the course made good.</summary>
        [Test]
        public void TrackVector_At1_IsTheShippedSternSweptTrack_At0_IsTheCourse()
        {
            var travel = new Vector2(0.2f, 0f);
            var swept = new Vector2(0.05f, -0.19f);

            Assert.AreEqual(swept, WakeTrailMath.TrackVector(travel, swept, 1f),
                "SternSwingFraction = 1 must revert the whole change from one number.");
            Assert.AreEqual(travel, WakeTrailMath.TrackVector(travel, swept, 0f));
            // Out-of-range fractions clamp rather than extrapolate the trail off the boat.
            Assert.AreEqual(swept, WakeTrailMath.TrackVector(travel, swept, 4f));
            Assert.AreEqual(travel, WakeTrailMath.TrackVector(travel, swept, -4f));
        }

        /// <summary>
        /// With the shipped fraction, the laid track sits much nearer the course than the stern's swing —
        /// which is the whole point — while still carrying SOME of the kick, because a transom swinging
        /// out does throw water and pretending otherwise would be its own lie.
        /// </summary>
        [Test]
        public void ShippedSwingFraction_PullsTheTrackBackOntoTheCourse_ButKeepsSomeKick()
        {
            var c = Cfg();
            var travel = new Vector2(0f, 0.2f);
            var swept = new Vector2(0.17f, 0.18f);   // a hard turn: the swing rivals the headway

            Vector2 track = WakeTrailMath.TrackVector(travel, swept, c.SternSwingFraction);
            float trackOff = Vector2.Angle(track, travel);
            float sweptOff = Vector2.Angle(swept, travel);

            Assert.Less(trackOff, sweptOff * 0.5f,
                "the laid track must sit much closer to the course than the stern's swing did.");
            Assert.Greater(trackOff, 0f,
                "…but not AT the course: the stern kick is real and the default keeps a quarter of it. " +
                "If the owner tunes SternSwingFraction to 0 this becomes an equality, and that is a " +
                "config change, not a regression — retune this guard, don't delete it.");
        }

        /// <summary>
        /// The deposit ROOT is the transom, at every heading — cross-checked against the DECK system's
        /// own hull-frame transform rather than against the wake's own formula restated. Two independent
        /// implementations agreeing is the claim; one implementation agreeing with itself is not.
        /// </summary>
        [Test]
        public void TheDepositRoot_IsAtLeastHalfAHullAstern_AtEveryHeading()
        {
            var c = Cfg();
            const float loa = 7f;
            const float elev = 40f;              // the iso rigs' bake — the foreshortened case, not plan view
            var pos = new Vector2(12.5f, -3.25f);

            for (int deg = 0; deg < 360; deg += 15)
            {
                Vector2 bow = Rotate(Vector2.up, deg);
                Vector2 stern = WakeGrading.SternAnchor(pos, bow, loa, c.DepositAsternOffset, elev);

                // Read the anchor back in the HULL frame through the deck-walk's transform.
                float heading = DirectionalBoatSprite.HeadingDegreesFromBow(bow);
                Vector2 hullFrame = DeckAreaMath.WorldToDeck(stern - pos, 0f, heading, elev);

                Assert.LessOrEqual(hullFrame.y, -loa * 0.5f + 1e-3f,
                    $"at heading {deg}° the trail's root sits {hullFrame.y:0.###} m along the keel — that is " +
                    "inside the hull. It must be at or abaft the transom (−LOA/2), which is the owner's " +
                    "\"originates at the centre of the hull\" defect in one number.");
                Assert.Less(Mathf.Abs(hullFrame.x), 1e-3f,
                    "the root belongs on the centreline; a lateral component means the projection is skewed.");
            }
        }

        /// <summary>The last deposit of a tick lands exactly on the current stern anchor — the trail's
        /// head is the transom, not a point lerped short of it.</summary>
        [Test]
        public void TheNewestDeposit_LandsExactlyOnTheStern()
        {
            var prevStern = new Vector2(3f, 1f);
            var stern = new Vector2(3.2f, 1.15f);
            for (int count = 1; count <= 6; count++)
            {
                float t = WakeTrailMath.DepositT(count - 1, count);
                Vector2 head = WakeTrailMath.PointOnTrack(prevStern, stern, t);
                Assert.That(Vector2.Distance(head, stern), Is.LessThan(1e-5f),
                    $"with {count} deposits the newest one drifted off the transom.");
            }
        }

        // ==== DEFECT 1a: the tide AND THE WIND move it ==================================================

        /// <summary>The wind term has an honest off switch, and it is a FRACTION of the wind — foam floats
        /// in the water and only makes part of the air's way.</summary>
        [Test]
        public void DriftVelocity_AtZeroFraction_IsTheCurrentAlone()
        {
            var current = new Vector2(0.3f, -0.1f);
            var wind = new Vector2(-4f, 2.5f);

            Assert.AreEqual(current, WakeTrailMath.DriftVelocity(current, wind, 0f),
                "FoamWindDriftFraction = 0 must restore the shipped current-only drift bit-for-bit.");
            Assert.AreEqual(current + wind, WakeTrailMath.DriftVelocity(current, wind, 1f));
            Assert.AreEqual(current + wind, WakeTrailMath.DriftVelocity(current, wind, 9f),
                "an over-range fraction clamps; the foam never outruns the wind.");
        }

        /// <summary>
        /// The wind actually MOVES laid foam — the behaviour the owner asked for and the one thing the
        /// shipped trail could not do. Measured through the real integrator, downwind, against the same
        /// puff drifting on the current alone.
        /// </summary>
        [Test]
        public void WindWalksLaidFoamDownwind_RelativeToTheSamePuffOnTheCurrentAlone()
        {
            var c = Cfg();
            var current = new Vector2(0.2f, 0f);
            var wind = new Vector2(0f, -5f);          // a fresh northerly
            const float dt = 1f / 30f;

            var seed = new WakeParticleSystem.Particle
            {
                Alive = true, Pos = Vector2.zero, Vel = Vector2.zero,
                Age = 0f, Lifetime = 2.2f, Seed = 0.4f, BaseSize = 0.35f, BirthStrength = 1f,
            };

            var still = seed;
            var blown = seed;
            for (int i = 0; i < 30; i++)              // one second of drift
            {
                still = WakeParticleSystem.Advect(still, current, 0.5f, dt);
                blown = WakeParticleSystem.Advect(
                    blown, WakeTrailMath.DriftVelocity(current, wind, c.FoamWindDriftFraction), 0.5f, dt);
            }

            Assert.Less(blown.Pos.y, still.Pos.y - 1e-3f,
                "the shipped foam ignored the wind entirely. If these two ever agree again, the trail has " +
                "gone back to being something the weather cannot touch.");
            Assert.AreEqual(still.Pos.x, blown.Pos.x, 1e-4f,
                "a crosswind must not change the along-current drift — the terms add, they do not scale.");
        }

        // ==== DEFECT 1b: the band tears apart instead of sliding as one sheet ===========================

        [Test]
        public void DispersalOffset_IsZeroWhenSwitchedOff_AndGrowsWithAge()
        {
            Assert.AreEqual(Vector2.zero, WakeTrailMath.DispersalOffset(0.37f, 1.4f, 0f),
                "FoamDispersalMetersPerSecond = 0 must restore the shipped rigid drift bit-for-bit.");
            Assert.AreEqual(Vector2.zero, WakeTrailMath.DispersalOffset(0.37f, 0f, 0.2f));

            float young = WakeTrailMath.DispersalOffset(0.37f, 0.5f, 0.2f).magnitude;
            float old = WakeTrailMath.DispersalOffset(0.37f, 2.0f, 0.2f).magnitude;
            Assert.Greater(old, young, "a puff must keep wandering as it ages, not settle.");
            Assert.AreEqual(0.2f * 2.0f, old, 1e-4f, "the rate is metres per second of age, honestly.");
        }

        [Test]
        public void DispersalOffset_IsDeterministic_SameSeedSameWander()
        {
            Assert.AreEqual(WakeTrailMath.DispersalOffset(0.81f, 1.1f, 0.16f),
                            WakeTrailMath.DispersalOffset(0.81f, 1.1f, 0.16f),
                            "no RNG, no time input (rule 5) — the same puff must disperse the same way.");
        }

        /// <summary>
        /// The SHEAR, stated as the property that matters: two puffs laid touching, with different seeds,
        /// must be further apart later. That is what turns a dense overlapping band back into a scatter;
        /// a shared drift direction would move the band and preserve it.
        /// </summary>
        [Test]
        public void NeighbouringPuffs_SeparateOverTime_WhichIsWhatBreaksUpTheBand()
        {
            var c = Cfg();
            var laidAt = new Vector2(4f, 4f);
            int separated = 0, pairs = 0;

            for (int i = 0; i < 16; i++)
            {
                float seedA = WakeParticleSystem.Hash01((uint)i * 2654435761u);
                float seedB = WakeParticleSystem.Hash01((uint)(i + 1) * 2654435761u);

                Vector2 a = laidAt + WakeTrailMath.DispersalOffset(seedA, 2f, c.FoamDispersalMetersPerSecond);
                Vector2 b = laidAt + WakeTrailMath.DispersalOffset(seedB, 2f, c.FoamDispersalMetersPerSecond);
                pairs++;
                if (Vector2.Distance(a, b) > 1e-3f) separated++;
            }

            Assert.AreEqual(pairs, separated,
                "every neighbouring pair must drift apart. If any pair stays glued the dispersal has become " +
                "a shared translation, which slides the painted band around instead of dissolving it.");
        }

        // ==== DEFECT 1c: the raft opens up with age =====================================================

        [Test]
        public void AgeStageIndex_IsMonotone_AndStaysInTheLadder()
        {
            const int stages = WakeFoamTexture.AgeStages;
            int previous = -1;
            for (float life = 0f; life <= 1.0001f; life += 0.01f)
            {
                int s = WakeTrailMath.AgeStageIndex(life, stages);
                Assert.GreaterOrEqual(s, 0);
                Assert.Less(s, stages);
                Assert.GreaterOrEqual(s, previous, "a puff must never climb BACK down the ladder.");
                previous = s;
            }
            Assert.AreEqual(0, WakeTrailMath.AgeStageIndex(0f, stages), "freshly laid foam draws rung 0.");
            Assert.AreEqual(stages - 1, WakeTrailMath.AgeStageIndex(1f, stages));
            Assert.AreEqual(0, WakeTrailMath.AgeStageIndex(0.7f, 1), "a one-rung ladder never leaves rung 0.");
        }

        [Test]
        public void StageErosion_StartsAtNothing_RisesWithAge_AndClamps()
        {
            const int stages = WakeFoamTexture.AgeStages;
            var c = Cfg();

            Assert.AreEqual(0f, WakeTrailMath.StageErosion(0, stages, c.FoamAgeErosion), 1e-6f,
                "rung 0 must dissolve NOTHING — a freshly laid puff is exactly the raft that shipped.");
            Assert.AreEqual(c.FoamAgeErosion, WakeTrailMath.StageErosion(stages - 1, stages, c.FoamAgeErosion),
                            1e-6f, "the last rung must reach the configured erosion, not stop short of it.");

            float previous = -1f;
            for (int s = 0; s < stages; s++)
            {
                float e = WakeTrailMath.StageErosion(s, stages, c.FoamAgeErosion);
                Assert.GreaterOrEqual(e, previous, "foam must never come BACK once it has dissolved.");
                Assert.LessOrEqual(e, 1f);
                previous = e;
            }

            // The off switch: no erosion ⇒ every rung is the laid raft, so the ladder collapses to today.
            for (int s = 0; s < stages; s++)
                Assert.AreEqual(0f, WakeTrailMath.StageErosion(s, stages, 0f), 1e-6f,
                    "FoamAgeErosion = 0 must make every rung identical (and the emitter then builds one).");
        }

        /// <summary>
        /// The claim the whole ladder exists for: an older puff is LESS FOAM AND MORE WATER, not the same
        /// shape dimmed.
        ///
        /// <para>⚠️ The first cut of this pass walked the raft's AERATION up with age instead, and this
        /// test is what caught it: measured across the six variants, 0.85 → 1.0 aeration <i>raises</i> mean
        /// coverage (0.1717 → 0.1786 on variant 0) and its hole count is not even monotone (variant 4 went
        /// 95 → 94). That axis brightens rims faster than it hollows middles. Do not "simplify" the
        /// erosion back into an aeration ramp — it measurably does the opposite of the owner's ask.</para>
        /// </summary>
        [Test]
        public void AnOlderPuff_IsLessFoamAndMoreWater_TexelByTexel()
        {
            const int size = WakeFoamTexture.PuffPixels;
            const int stages = WakeFoamTexture.AgeStages;
            var c = Cfg();

            for (int v = 0; v < WakeFoamTexture.VariantCount; v++)
            {
                float[] laid = WakeFoamTexture.BuildRaftCoverage(v, size, c.FoamAeration);
                float previousCoverage = float.MaxValue;
                int previousHoles = -1;
                float[] previousRung = null;

                for (int s = 0; s < stages; s++)
                {
                    float[] rung = WakeFoamTexture.ErodeCoverage(
                        laid, WakeTrailMath.StageErosion(s, stages, c.FoamAgeErosion));

                    float coverage = 0f;
                    int holes = 0;
                    for (int i = 0; i < rung.Length; i++)
                    {
                        coverage += rung[i];
                        if (rung[i] <= 0f) holes++;
                        if (previousRung != null)
                            Assert.LessOrEqual(rung[i], previousRung[i] + 1e-6f,
                                $"variant {v} rung {s}: a texel gained coverage as the puff aged. Foam " +
                                "dissolves; it does not grow back.");
                    }

                    Assert.Less(coverage, previousCoverage,
                        $"variant {v} rung {s}: total foam must fall on every rung, or the ladder is " +
                        "spending texture memory without dispersing anything.");
                    Assert.GreaterOrEqual(holes, previousHoles,
                        $"variant {v} rung {s}: the water showing through must never shrink.");

                    previousCoverage = coverage;
                    previousHoles = holes;
                    previousRung = rung;
                }
            }
        }

        /// <summary>The erosion has an exact off switch, and the disc floor the bubbling pass established
        /// survives the ladder untouched.</summary>
        [Test]
        public void ErodeCoverage_AtZero_IsTheLaidRaftBitForBit_AndTheDiscFloorStillHolds()
        {
            const int size = WakeFoamTexture.PuffPixels;
            var c = Cfg();

            float[] laid = WakeFoamTexture.BuildRaftCoverage(0, size, c.FoamAeration);
            Assert.AreEqual(laid, WakeFoamTexture.ErodeCoverage(laid, 0f),
                "erosion 0 must return the laid raft bit-for-bit — that is the whole A/B.");

            Assert.AreEqual(WakeFoamTexture.SolidPuffCoverage(size),
                            WakeFoamTexture.ErodeCoverage(WakeFoamTexture.BuildRaftCoverage(0, size, 0f), 0f),
                            "with the foam dialled all the way back the unit is still the shipped solid disc.");

            // Total erosion leaves nothing — the far end of "back to water" is water.
            foreach (float texel in WakeFoamTexture.ErodeCoverage(laid, 1f))
                Assert.AreEqual(0f, texel, 1e-6f);
        }

        // ==== helpers ===================================================================================

        /// <summary>Rotate a vector by <paramref name="deg"/> counter-clockwise (screen convention).</summary>
        private static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
        }
    }
}
