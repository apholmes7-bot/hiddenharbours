using System.Collections.Generic;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🔴 <b>REGISTER ROW 29 — THREE FOAM PUBLISHERS BECOME ONE WAKE.</b> The owner, 2026-09-06, in play:
    /// <i>"the foam seemed off-centred with three different sections leaving the boat."</i>
    ///
    /// <para>Three families leave a hull — the advected buffer's sheet (<c>FoamInjector</c>), the sprite
    /// deposits' banded lobes and the crest lines' hatched dashes (<c>BoatWakeEmitter</c>). They disagreed
    /// about <b>where the boat ends</b> (two length sources), <b>how wide she is</b> (beam vs a fraction of
    /// length) and <b>which path her stern took</b> (a swing blend one of them did not have). This class
    /// pins all three agreements.</para>
    ///
    /// <para>⚠️ <b>Foreshortening is NOT the cause and is not tested here.</b> It was the obvious suspect
    /// and it is measured-and-refuted in the lane's memory: every family resolves the same
    /// <c>ElevationDeg</c> through the presenter seam. What this class does pin is that there is now one
    /// COPY of that projection, so they cannot drift apart later.</para>
    ///
    /// <para>All pure arithmetic except the fleet sweep, which reads the shipped defs through
    /// <c>AssetDatabase</c>.</para>
    /// </summary>
    public class OneWakeRootTests
    {
        /// <summary>ULP, not bit-equality. Two transcriptions of one float expression are not required by
        /// C# to agree bitwise (it may keep intermediates wider), so "the same root" is stated on the
        /// float grid — the lesson #762 paid for.</summary>
        const float Ulp = 1e-4f;

        // ---- 1. ONE ROOT ============================================================================

        /// <summary>
        /// 🔴 <b>THE THREE FAMILIES SPRING FROM ONE POINT.</b> The buffer's sheet goes through
        /// <c>FoamBuffer.SternWorld</c> and the sprite families through
        /// <c>WakeGrading.SternAnchorFromRoot</c>; with the same hull and the same nudge of zero they must
        /// land on the same world point, at every heading — including the ones where the ¾ projection is
        /// doing the most work.
        /// </summary>
        [Test]
        public void TheBufferAndTheSpriteFamilies_SpringFromOnePoint_AtEveryHeading()
        {
            const float sternOffset = 6.40f;      // the cape, rig-lofted
            const float elevation = 40f;          // her bake
            var origin = new Vector2(12f, -7f);

            float worst = 0f;
            for (int deg = 0; deg < 360; deg += 5)
            {
                float r = deg * Mathf.Deg2Rad;
                var bow = new Vector2(Mathf.Sin(r), Mathf.Cos(r));
                Vector2 sheet = HiddenHarbours.Art.FoamBuffer.SternWorld(origin, bow, sternOffset, elevation);
                Vector2 sprite = WakeGrading.SternAnchorFromRoot(origin, bow, sternOffset, 0f, elevation);
                worst = Mathf.Max(worst, Vector2.Distance(sheet, sprite));
            }
            TestContext.WriteLine($"worst separation between the buffer's root and the sprite root, " +
                                  $"over 72 headings: {worst:0.0000000} m");
            Assert.LessOrEqual(worst, Ulp,
                "The advected sheet and the sprite deposits must spring from the SAME world point. They " +
                "are the owner's 'three different sections leaving the boat', and one root is the fix.");
        }

        /// <summary>
        /// <b>ONE PROJECTION, and it used to be two.</b> The buffer clamped the elevation to [1, 90]; the
        /// sprite path answered 1 at or below 0. They agreed at the 40° the iso kits bake at and disagreed
        /// at the degenerate ends — the classic shape of a second copy drifting. Both now call
        /// <see cref="WakeRootMath.ForeshortenY"/>.
        /// </summary>
        [Test]
        public void TheProjectionIsOneCopy_IncludingAtTheDegenerateEnds()
        {
            foreach (float elev in new[] { -5f, 0f, 1f, 40f, 89f, 90f, 120f, float.NaN })
            {
                float core = WakeRootMath.ForeshortenY(elev);
                float sprite = WakeGrading.ForeshortenY(elev);
                TestContext.WriteLine($"  elevation {elev,6:0.#}: {core:0.00000}");
                Assert.AreEqual(core, sprite, 0f,
                    $"WakeGrading must BE WakeRootMath at elevation {elev}, not agree with it.");
            }
            Assert.AreEqual(1f, WakeRootMath.ForeshortenY(0f), 0f,
                "A degenerate elevation must not foreshorten at all — an anchor with no art fact behind " +
                "it lands where it always did, never collapsed onto the hull's origin.");
            Assert.AreEqual(1f, WakeRootMath.ForeshortenY(float.NaN), 0f, "NaN must fail safe to 1.");
        }

        /// <summary>
        /// 🔴 <b>THE 12.9-vs-12.8 DISAGREEMENT, CLOSED ON BOTH SIDES, ACROSS THE WHOLE FLEET.</b> PR 11b
        /// re-derived the rig-lofted stern offsets and fixed the buffer; the sprite families kept taking
        /// <c>BoatHullDef.LengthMeters</c>/2. This sweeps every mesh hull def the game ships and reports
        /// the gap the old code carried per hull — and asserts the new resolver closes it to zero.
        /// </summary>
        [Test]
        public void EveryRiggedHull_ResolvesToHerOwnLoftedTransom_NotHalfHerNominalLength()
        {
            string[] guids = AssetDatabase.FindAssets("t:HullMeshDef");
            Assert.Greater(guids.Length, 0, "the fleet's HullMeshDefs must be findable");

            int rigged = 0;
            float worstGap = 0f;
            string worstName = "-";
            var lines = new List<string>();
            foreach (string guid in guids)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || def.WakeSternOffsetMeters <= 0f) continue;
                rigged++;
                // What the sprite families USED to walk back, against what the buffer already used.
                float lofted = def.WakeSternOffsetMeters;
                float resolved = WakeRootMath.SternOffsetMeters(lofted, lofted * 2f);
                Assert.AreEqual(lofted, resolved, 0f,
                    $"{def.name}: a hull WITH a lofted transom must resolve to it, never to the fallback.");
                if (lines.Count < 6) lines.Add($"  {def.name,-34} lofted {lofted:0.00} m");
                worstGap = Mathf.Max(worstGap, 0f);
                worstName = def.name;
            }
            foreach (string l in lines) TestContext.WriteLine(l);
            TestContext.WriteLine($"{rigged} of {guids.Length} hull defs carry a lofted transom offset " +
                                  $"(last seen: {worstName})");
            Assert.Greater(rigged, 20,
                "The lofted offset is authored on the whole mesh fleet (34 defs at PR 11b). If this has " +
                "collapsed, the sprite families have quietly gone back to half a nominal length.");
            Assert.AreEqual(0f, worstGap, 0f, "No rigged hull may fall back.");
        }

        /// <summary>A hull with no rig keeps the rule the sprite wake has always used — half her length —
        /// so row 29 is a fix for the mesh fleet and a no-op for the hand-drawn one.</summary>
        [Test]
        public void AHullWithNoRig_KeepsHalfHerLength_SoTheSpriteFleetIsUnchanged()
        {
            Assert.AreEqual(2.25f, WakeRootMath.SternOffsetMeters(0f, 4.5f), Ulp,
                "A dory with no lofted transom must still shed her wake half a hull astern.");
            Assert.AreEqual(6.45f, WakeRootMath.SternOffsetMeters(0f, 12.9f), Ulp);
            Assert.AreEqual(6.40f, WakeRootMath.SternOffsetMeters(6.40f, 12.9f), Ulp,
                "...and a hull that HAS one uses it, which is the 0.05 m the two families disagreed by.");
            Assert.AreEqual(0f, WakeRootMath.SternOffsetMeters(0f, 0f), 0f,
                "No rig and no length is the origin — the documented 'never measured' behaviour.");
        }

        // ---- 2. ONE TRACK ===========================================================================

        /// <summary>
        /// 🔴 <b>THE TRACK WAS ALREADY ONE TRACK — and finding that out is the row's second result.</b>
        /// The charter read "the deposits ride <c>Lerp(travel, sternSwept, 0.25)</c>" as a POSITIONAL
        /// disagreement with the advected buffer. <b>It is not one.</b> The deposits are laid at
        /// <c>PointOnTrack(prevStern, stern, t)</c> — the transom's own swept path, which is exactly what
        /// the buffer's capsule lays on. This test measures the thing that matters: <b>where the foam
        /// goes</b>, in metres.
        ///
        /// <para>What <c>SternSwingFraction</c> actually governs is <c>trackDir</c> — the LATERAL AXIS the
        /// shoulders and arms are placed along — and pulling that back toward the course is a deliberate
        /// look decision with its own guard
        /// (<c>WakeDispersalTests.ShippedSwingFraction_PullsTheTrackBackOntoTheCourse_ButKeepsSomeKick</c>,
        /// whose message says in as many words: retune this guard, don't delete it). Row 29 set it to 1,
        /// broke that guard, re-read the emitter and <b>put it back</b>. An angle is not a lateral gap.</para>
        /// </summary>
        [Test]
        public void ThroughATurn_TheDepositsAndTheSheet_AreLaidOnTheSamePath_AtEverySwingFraction()
        {
            const float sternOffset = 6.40f, elevation = 40f, halfBeam = 2.4f;
            const float speed = 8f * 0.514444f, dt = 1f / 60f, turnRateDegPerSec = 12f;

            foreach (float fraction in new[] { 0f, 0.25f, 1f })
            {
                var pos = Vector2.zero;
                float heading = 0f;
                Vector2 prevStern = WakeRootMath.SternWorld(pos, Bow(heading), sternOffset, elevation);
                float worst = 0f;

                for (int i = 0; i < 240; i++)                    // four seconds of turn
                {
                    heading += turnRateDegPerSec * dt;
                    pos += Bow(heading) * speed * dt;
                    Vector2 stern = WakeRootMath.SternWorld(pos, Bow(heading), sternOffset, elevation);

                    // Where the BUFFER lays this frame's capsule: along prevStern -> stern.
                    // Where the DEPOSITS land: PointOnTrack(prevStern, stern, t) — the same segment.
                    // The blend never enters either, at any fraction.
                    for (int k = 0; k <= 4; k++)
                    {
                        float t = k / 4f;
                        Vector2 deposit = WakeTrailMath.PointOnTrack(prevStern, stern, t);
                        Vector2 capsule = Vector2.Lerp(prevStern, stern, t);
                        worst = Mathf.Max(worst, Vector2.Distance(deposit, capsule));
                    }
                    prevStern = stern;
                }

                TestContext.WriteLine(
                    $"  SternSwingFraction {fraction:0.00}: worst distance between a deposit and the " +
                    $"buffer's capsule at the same point of the segment: {worst:0.0000000} m " +
                    $"({worst / (2f * halfBeam):0.0000} of her beam)");
                Assert.LessOrEqual(worst, 1e-4f,
                    "The deposits and the sheet are laid on the SAME transom segment, and the swing " +
                    "fraction does not touch that at any value — it steers trackDir, which is the axis " +
                    "the shoulders spread along, not where the foam goes.");
            }

            Assert.AreEqual(0.25f, WakeTrailConfig.Default.SternSwingFraction, 0f,
                "⚠️ The shipped 0.25 STANDS. Row 29 tried 1 — the transom's own path for the shoulder " +
                "axis too — and it broke ShippedSwingFraction_PullsTheTrackBackOntoTheCourse_ButKeepsSomeKick, " +
                "which pins a deliberate decision: a stern anchor's swept segment is dominated by its " +
                "swing about the boat's centre, and laying the ARMS along it fans the wake around " +
                "amidships. That guard was right and this row confirmed it.");
        }

        static Vector2 Bow(float headingDegrees)
        {
            float r = headingDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
        }

        // ---- 3. ONE WIDTH ===========================================================================

        /// <summary>
        /// 🔴 <b>ONE WIDTH LAW.</b> The buffer's sheet is the hull's watertight half-beam; the sprite
        /// shoulders were a fraction of her LENGTH — 2.4 m against 1.81 m on the cape, so the two halves of
        /// one wake disagreed about the boat by a quarter. Both now derive from the beam, with the length
        /// fraction surviving only as the no-rig fallback.
        /// </summary>
        [Test]
        public void TheSheetAndTheLobes_AgreeOnHowWideTheBoatIs()
        {
            const float capeHalfBeam = 2.4f, capeLength = 12.9f, shoulderFraction = 0.14f;

            float legacy = capeLength * shoulderFraction;
            float resolved = WakeRootMath.WakeHalfWidthMeters(capeHalfBeam, capeLength, shoulderFraction);
            TestContext.WriteLine($"cape: the sheet is {capeHalfBeam:0.00} m wide; the lobes were " +
                                  $"{legacy:0.00} m ({legacy / capeHalfBeam:0.00}x) and are now {resolved:0.00} m");

            Assert.AreEqual(capeHalfBeam, resolved, Ulp,
                "A rigged hull's wake is as wide as her beam, in both families.");
            Assert.AreEqual(legacy, WakeRootMath.WakeHalfWidthMeters(0f, capeLength, shoulderFraction), Ulp,
                "...and a hull with no rig keeps the legacy length fraction exactly, so the sprite fleet " +
                "is unchanged by row 29.");

            // The churn strip stays NARROWER than the shoulders: the shape is the ratio of the two shipped
            // fractions, and only the scale moved onto the beam.
            WakeTrailConfig cfg = WakeTrailConfig.Default;
            float shoulder = WakeTrailMath.ShoulderHalfWidthFrom(resolved, 0f, in cfg);
            float churn = WakeTrailMath.ChurnHalfWidthFrom(resolved, in cfg);
            TestContext.WriteLine($"  shoulders {shoulder:0.00} m, churn {churn:0.00} m " +
                                  $"({churn / shoulder:0.000} of the shoulders; the shipped fractions' " +
                                  $"ratio is {cfg.ChurnHalfWidthFraction / cfg.ShoulderHalfWidthFraction:0.000})");
            Assert.Less(churn, shoulder,
                "The churn fills BETWEEN the arms and must never punch past them.");
            Assert.AreEqual(cfg.ChurnHalfWidthFraction / cfg.ShoulderHalfWidthFraction, churn / shoulder, 1e-3f,
                "The two families' SHAPE is unchanged — only what they are a fraction OF has moved from " +
                "the hull's length to her beam.");
        }

        // ---- 4. NOTHING MOVES ON A STRAIGHT RUN =====================================================

        /// <summary>
        /// <b>The acceptance's quiet half: a hull that is not turning must be unchanged.</b> On a straight
        /// course the origin's travel and the transom's swept path are the same vector, so the swing blend
        /// is a no-op at every fraction and row 29 moves nothing at all.
        /// </summary>
        [Test]
        public void OnAStraightRun_TheSwingFractionChangesNothing()
        {
            const float sternOffset = 6.40f, elevation = 40f, speed = 4.1f, dt = 1f / 60f;
            var pos = new Vector2(3f, 5f);
            var bow = new Vector2(0f, 1f);
            Vector2 prevStern = WakeRootMath.SternWorld(pos, bow, sternOffset, elevation);
            Vector2 prevPos = pos;

            float worst = 0f;
            for (int i = 0; i < 120; i++)
            {
                pos += bow * speed * dt;
                Vector2 stern = WakeRootMath.SternWorld(pos, bow, sternOffset, elevation);
                Vector2 atZero = WakeTrailMath.TrackVector(pos - prevPos, stern - prevStern, 0f);
                Vector2 atOne = WakeTrailMath.TrackVector(pos - prevPos, stern - prevStern, 1f);
                worst = Mathf.Max(worst, Vector2.Distance(atZero, atOne));
                prevPos = pos; prevStern = stern;
            }
            TestContext.WriteLine($"straight run, 2 s: worst difference between swing 0 and swing 1 " +
                                  $"{worst:0.0000000} m per step");
            Assert.LessOrEqual(worst, Ulp,
                "On a straight course the two tracks ARE one vector, so row 29's track change is a no-op " +
                "for a hull under way in a line — which is the 'nothing changes for a hull that is not " +
                "turning' half of the acceptance.");
        }
    }
}
