using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE FOAM TRAIL IS SHED AT THE TRANSOM</b> — the owner, 2026-09-04: <i>"the foam seem to come
    /// from the cetnre of a boat when turning and not accuratly from the stern."</i>
    ///
    /// <para><b>⚠️ Not the deposited V arms, which the wake charter named.</b> Those are correctly
    /// stern-anchored and already guarded at every heading
    /// (<c>WakeDispersalTests.TheDepositRoot_IsAtLeastHalfAHullAstern_AtEveryHeading</c>). The defect is in
    /// <c>FoamInjector</c>, which lays the persistent BUFFER mark: it read <c>transform.position</c> and is
    /// added to the hull HOST, so the capsule was laid centre→centre. On a turn a hull's centre traces a
    /// tighter arc than her transom, which is exactly when he saw it.</para>
    ///
    /// <para><b>Fenced deliberately:</b> the trail's WIDTH still stays the hull's beam for its whole life.
    /// That is the other half of his complaint (<i>"it doesnt widen over time and fade away"</i>) and it is
    /// register row 26 / PR 11b — it needs an area-preserving stamp, not a knob, for reasons measured and
    /// written up there.</para>
    /// </summary>
    public class FoamTrailRootTests
    {
        /// <summary>
        /// The owner's sentence as a number. BEFORE, the trail was laid at the hull's ORIGIN whatever her
        /// heading, so its error against the true transom was the whole projected offset — on the cape,
        /// 6.45 m, over half her length. AFTER, it is laid at the transom.
        /// </summary>
        [Test]
        public void TheTrailRoot_MovesFromAmidships_ToTheTransom()
        {
            // The shipped fleet's own numbers (Data/Boats/HullMeshes/*.asset).
            var hulls = new (string name, float loa, float elev)[]
            {
                ("dory", 4.5f, 40f), ("console skiff", 7f, 40f),
                ("cape islander", 12.9f, 40f), ("tanker", 110f, 40f),
            };

            var sb = new StringBuilder();
            sb.AppendLine("hull | LOA | stern offset | root error BEFORE (m)");
            var origin = new Vector2(31.5f, -12.25f);

            foreach ((string name, float loa, float elev) in hulls)
            {
                float stern = loa * 0.5f;
                float worstBefore = 0f, worstAfter = 0f;

                for (int deg = 0; deg < 360; deg += 5)
                {
                    float r = deg * Mathf.Deg2Rad;
                    var heading = new Vector2(-Mathf.Sin(r), Mathf.Cos(r));
                    Vector2 transom = FoamBuffer.SternWorld(origin, heading, stern, elev);

                    // BEFORE: laid at the ORIGIN — its error against the transom is the whole offset.
                    worstBefore = Mathf.Max(worstBefore, Vector2.Distance(origin, transom));
                    // AFTER: laid at SternWorld, which IS the transom — 0 by construction, asserted as a
                    // construction check and NOT reported as a result (that would be a dead control).
                    worstAfter = Mathf.Max(worstAfter,
                        Vector2.Distance(FoamBuffer.SternWorld(origin, heading, stern, elev), transom));
                }

                sb.AppendLine($"{name,-14} | {loa,5:0.0} | {stern,6:0.00} m | {worstBefore,6:0.00}");
                Assert.That(worstAfter, Is.LessThan(1e-4f),
                    $"{name}: the trail must be laid AT the transom (0 by construction)");
                Assert.Greater(worstBefore, stern * 0.5f,
                    $"{name}: the shipped root sat {worstBefore:0.00} m from the transom — that IS the defect");
            }
            Debug.Log("[foam-root] the trail's root, measured\n" + sb);
        }

        /// <summary>
        /// The root belongs on the CENTRELINE and at or abaft the transom at every heading — the same
        /// shape of guard the V arms already carry, now for the buffer trail.
        /// </summary>
        [Test]
        public void TheRoot_IsOnTheCentreline_AndAbaftTheTransom_AtEveryHeading()
        {
            const float stern = 6.45f, elev = 40f;
            var origin = new Vector2(-4f, 9f);

            // ⚠️ Read back in the HULL's own frame, not world space. The projection squashes Y and not X,
            // so the projected offset is deliberately NOT parallel to the raw heading vector — the drawn
            // hull is squashed the same way, which is the whole point. Measuring "off the centreline"
            // against the unsquashed heading reports a skew that is not there (it reads 0.39 m at 10°).
            // WorldToDeck is the frame the deck walk and the V arms' own guard already use.
            for (int deg = 0; deg < 360; deg += 5)
            {
                float r = deg * Mathf.Deg2Rad;
                var heading = new Vector2(-Mathf.Sin(r), Mathf.Cos(r));
                Vector2 root = FoamBuffer.SternWorld(origin, heading, stern, elev);

                float drawnHeading = DirectionalBoatSprite.HeadingDegreesFromBow(heading);
                Vector2 hullFrame = DeckAreaMath.WorldToDeck(root - origin, 0f, drawnHeading, elev);

                Assert.LessOrEqual(hullFrame.y, -stern + 1e-3f,
                    $"at {deg}° the root sits {hullFrame.y:0.###} m along the keel — that is inside the hull. " +
                    "It must be at or abaft the transom, which is the owner's \"comes from the centre\" " +
                    "defect in one number.");
                Assert.Less(Mathf.Abs(hullFrame.x), 1e-3f,
                    $"at {deg}° the root is {hullFrame.x:0.####} m off the centreline in the hull's own frame");
            }
        }

        /// <summary>
        /// ⚠️ The foreshortening is load-bearing, and the SABOTAGE ARM is what proves it. The stern is a
        /// distance ON THE WATER, and a ¾ camera draws it in full to the east but only sin(elev) of it to
        /// the north. An unprojected offset is CONSTANT in world metres, so against the foreshortened
        /// artwork the gap between transom and foam would open and close through every turn — the breathing
        /// the plume anchor was fixed for once already ("not even connected to it and way off to the stern").
        /// </summary>
        [Test]
        public void TheUnprojectedArm_IsFlat_WhereTheProjectedOneFollowsTheArtwork()
        {
            const float stern = 6.45f, elev = 40f;
            var origin = Vector2.zero;

            float minProjected = float.MaxValue, maxProjected = 0f;
            float minFlat = float.MaxValue, maxFlat = 0f;

            for (int deg = 0; deg < 360; deg += 5)
            {
                float r = deg * Mathf.Deg2Rad;
                var heading = new Vector2(-Mathf.Sin(r), Mathf.Cos(r));
                float projected = Vector2.Distance(origin, FoamBuffer.SternWorld(origin, heading, stern, elev));
                float flat = Vector2.Distance(origin, FoamBuffer.SternWorld(origin, heading, stern, 90f));

                minProjected = Mathf.Min(minProjected, projected); maxProjected = Mathf.Max(maxProjected, projected);
                minFlat = Mathf.Min(minFlat, flat); maxFlat = Mathf.Max(maxFlat, flat);
            }

            Debug.Log($"[foam-root] cape stern {stern} m at {elev}° — PROJECTED spans " +
                      $"{minProjected:0.00}..{maxProjected:0.00} m of screen through a turn; " +
                      $"UNPROJECTED (the sabotage) {minFlat:0.00}..{maxFlat:0.00} m");

            Assert.That(maxFlat - minFlat, Is.LessThan(1e-3f),
                "the sabotage arm is constant in world metres — which is exactly why it is wrong");
            Assert.Greater(maxProjected - minProjected, 2f,
                "the projected anchor must vary with heading by metres: that variation is what MATCHES the " +
                "foreshortened artwork, and it is the whole reason the offset is projected");
        }

        /// <summary>A hull whose stern has never been measured keeps the shipped anchor exactly — absence
        /// is data, not a guess, and not a silent half-fix.</summary>
        [Test]
        public void AnUnmeasuredStern_LaysTheTrailWhereItAlwaysDid()
        {
            var origin = new Vector2(3f, -7f);
            for (int deg = 0; deg < 360; deg += 15)
            {
                float r = deg * Mathf.Deg2Rad;
                var heading = new Vector2(-Mathf.Sin(r), Mathf.Cos(r));
                Assert.That(FoamBuffer.SternWorld(origin, heading, 0f, 40f), Is.EqualTo(origin),
                    "stern offset 0 must return the origin bit-for-bit");
            }
        }
    }
}
