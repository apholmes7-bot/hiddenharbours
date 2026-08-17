using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>Stopping to talk, without touching the day.</b> The overlay is a POCO, so all three of the
    /// things that matter are numbers here: an uninterruptible block REFUSES the stop; a held villager
    /// does not drift; and a released one walks back to where the CLOCK says she should be by now — not
    /// to where she was standing when the conversation started.
    ///
    /// <para><b>The facing is checked on the DIAGONALS on purpose.</b> The world XY plane is the squashed
    /// ground plane (ADR 0034), and the error between reading it as a compass and un-squashing it first
    /// is exactly ZERO on the cardinals — so a cardinal-only test cannot see a bearing bug at all. It
    /// peaks at 12.56° near the diagonals, which is over a quarter of an eight-way facing cell.</para>
    /// </summary>
    public class ConversationHoldTests
    {
        static readonly Vector2 Spot = new Vector2(10f, 4f);

        /// <summary>A world-space delta that is <paramref name="groundDegrees"/> of GROUND bearing —
        /// i.e. what the depth squash does to an honest compass direction on its way to world XY.</summary>
        static Vector2 GroundDelta(float groundDegrees, float metres = 3f)
        {
            float r = groundDegrees * Mathf.Deg2Rad;
            // 0° = North, clockwise: x = sin, y = cos, then the depth axis is squashed on the way in.
            return new Vector2(Mathf.Sin(r) * metres,
                               Mathf.Cos(r) * metres * IsoGround.GroundDepthScale);
        }

        static float Normalised(float degrees) => Mathf.Repeat(degrees, 360f);

        static ConversationHold Held(Vector2 player)
        {
            var hold = new ConversationHold();
            Assert.That(hold.TryBegin(Spot, player, interruptible: true, RoutineShelter.Outdoors, 0),
                        Is.True);
            return hold;
        }

        // ---- the refusal --------------------------------------------------------------------

        [Test]
        public void AnUninterruptibleBlock_RefusesTheStop()
        {
            // ⭐ THE SABOTAGE ARM. Interruptibility is authored per block, not guessed, and the authored
            // "no" has to actually mean no: she keeps walking and answers on the move.
            var hold = new ConversationHold();

            Assert.That(hold.TryBegin(Spot, Spot + Vector2.right, interruptible: false,
                                      RoutineShelter.Outdoors, 0), Is.False);

            Assert.That(hold.Phase, Is.EqualTo(ConversationHoldPhase.Idle));
            Assert.That(hold.IsActive, Is.False);

            // …and the overlay stays transparent: she is wherever the clock says, frame after frame.
            Assert.That(hold.Step(new Vector2(1f, 1f), 0.1f, 2f), Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(hold.Step(new Vector2(2f, 2f), 0.1f, 2f), Is.EqualTo(new Vector2(2f, 2f)));
        }

        [Test]
        public void WithNoConversation_TheOverlayIsTransparent()
        {
            var hold = new ConversationHold();
            var clock = new Vector2(7f, -2f);

            Assert.That(hold.IsActive, Is.False);
            Assert.That(hold.Step(clock, 0.016f, 2f), Is.EqualTo(clock),
                        "an idle overlay must be indistinguishable from not having one");
        }

        // ---- holding ------------------------------------------------------------------------

        [Test]
        public void WhileTalking_SheHoldsTheSpotSheStoppedOn_HoweverFarTheClockMovesOn()
        {
            ConversationHold hold = Held(Spot + new Vector2(1f, 0f));

            Assert.That(hold.IsHolding, Is.True);
            Assert.That(hold.Step(Spot, 0.1f, 2f), Is.EqualTo(Spot));

            // The clock has meanwhile walked her half way across the village. She has not moved.
            Assert.That(hold.Step(new Vector2(60f, 60f), 0.1f, 2f), Is.EqualTo(Spot));
            Assert.That(hold.Step(new Vector2(90f, 90f), 5f, 2f), Is.EqualTo(Spot),
                        "she does not drift while you talk, whatever her schedule is doing without her");
        }

        [Test]
        public void SheTurnsToThePlayer_OnGroundBearings_AndTheDIAGONALSAreWhereThatShows()
        {
            // A player NE of her, at 45° of GROUND bearing. Read as plain world XY the same delta
            // answers 57.26° — a whole facing row out — which is the ADR 0034 defect in one number.
            ConversationHold hold = Held(Spot + GroundDelta(45f));

            Assert.That(Normalised(hold.HeadingDegrees), Is.EqualTo(45f).Within(0.05f),
                        "un-squash before you take the angle: 57.26° here means the world XY plane was " +
                        "read as a compass rose (ADR 0034)");
        }

        [Test]
        public void EveryEighthOfTheCompass_IsFacedExactly()
        {
            for (int row = 0; row < 8; row++)
            {
                float bearing = row * 45f;
                ConversationHold hold = Held(Spot + GroundDelta(bearing));
                Assert.That(Normalised(hold.HeadingDegrees), Is.EqualTo(bearing).Within(0.05f),
                            $"facing row {row} ({bearing}°) is wrong — the facings are ruled EIGHT and " +
                            "each row is 45° of ground azimuth");
            }
        }

        [Test]
        public void SheKeepsTurning_IfThePlayerWalksAroundHer()
        {
            ConversationHold hold = Held(Spot + GroundDelta(0f));
            Assert.That(Normalised(hold.HeadingDegrees), Is.EqualTo(0f).Within(0.05f));

            hold.FaceToward(Spot + GroundDelta(135f));
            Assert.That(Normalised(hold.HeadingDegrees), Is.EqualTo(135f).Within(0.05f),
                        "somebody circling her mid-sentence is looked at, not talked past");
        }

        [Test]
        public void APlayerStandingExactlyOnHer_LeavesTheFacingAlone()
        {
            ConversationHold hold = Held(Spot + GroundDelta(90f));
            float before = hold.HeadingDegrees;

            hold.FaceToward(Spot);      // zero-length delta: there is no direction to face

            Assert.That(hold.HeadingDegrees, Is.EqualTo(before),
                        "a degenerate delta must not snap her to North");
        }

        [Test]
        public void FacingIsOnlyAnsweredWhileSheIsActuallyHolding()
        {
            ConversationHold hold = Held(Spot + GroundDelta(0f));
            hold.Release();
            float during = hold.HeadingDegrees;

            hold.FaceToward(Spot + GroundDelta(180f));

            Assert.That(hold.HeadingDegrees, Is.EqualTo(during),
                        "once she is hurrying back, her facing follows her WALK — the sprite reads it " +
                        "off its own motion and a stated heading here would fight that");
        }

        // ---- catching up --------------------------------------------------------------------

        [Test]
        public void OnRelease_SheWalksBackToWhereTheClockSaysSheShouldBe_NotToWhereSheWasStanding()
        {
            // ⭐ THE OTHER SABOTAGE ARM. A long chat makes her LATE; resuming means rejoining the day
            // she should be having, not the one she left.
            ConversationHold hold = Held(Spot + Vector2.right);
            var clockSays = new Vector2(20f, 4f);          // ten metres along, while they talked

            hold.Release();
            Assert.That(hold.Phase, Is.EqualTo(ConversationHoldPhase.CatchingUp));

            Vector2 first = hold.Step(clockSays, 0.5f, 2f);
            Assert.That(first.x, Is.GreaterThan(Spot.x), "she sets off after it");
            Assert.That(first.x, Is.LessThan(clockSays.x), "and she is not there yet — this is a WALK");
            Assert.That(first.x, Is.EqualTo(Spot.x + 1f).Within(0.001f), "2 m/s for half a second");
        }

        [Test]
        public void TheCatchUpEnds_AndSheIsThePureFunctionAgainFromThatFrameOn()
        {
            ConversationHold hold = Held(Spot + Vector2.right);
            var clockSays = new Vector2(13f, 4f);

            hold.Release();
            for (int i = 0; i < 200 && hold.IsActive; i++) hold.Step(clockSays, 0.05f, 2f);

            Assert.That(hold.Phase, Is.EqualTo(ConversationHoldPhase.Idle),
                        "a catch-up that never ends would be an overlay that never stops overlaying");
            Assert.That(hold.Step(clockSays, 0.05f, 2f), Is.EqualTo(clockSays),
                        "and from that frame she is exactly where the clock says, to the last decimal");
        }

        [Test]
        public void SheCatchesAMOVINGTarget_BecauseHurryingIsFasterThanWalking()
        {
            // The target is her own schedule, still walking away at 1.2 m/s. Catching it at 1.2 m/s is
            // impossible, which is why CatchUpSpeedMultiplier exists and must be above 1.
            ConversationHold hold = Held(Spot + Vector2.right);
            hold.Release();

            var clockSays = new Vector2(14f, 4f);
            const float dt = 0.05f;
            for (int i = 0; i < 400 && hold.IsActive; i++)
            {
                clockSays += new Vector2(1.2f * dt, 0f);     // her day, walking on without her
                hold.Step(clockSays, dt, 1.2f * 1.6f);
            }

            Assert.That(hold.Phase, Is.EqualTo(ConversationHoldPhase.Idle),
                        "hurrying at 1.6× a walk has to close a four-metre gap on a walking target");
        }

        [Test]
        public void APathologicalCatchUp_GivesUpRatherThanRunningForever()
        {
            // The clock jumped (a new day, a slept night). There is no honest hurry back from that, so
            // the overlay stops overlaying rather than sprinting across the island.
            ConversationHold hold = Held(Spot + Vector2.right);
            hold.Release();

            var milesAway = new Vector2(100000f, 100000f);
            for (int i = 0; i < 2000 && hold.IsActive; i++) hold.Step(milesAway, 0.05f, 2f);

            Assert.That(hold.Phase, Is.EqualTo(ConversationHoldPhase.Idle));
        }

        [Test]
        public void ReleasingSomebodyWhoNeverStopped_IsANoOp()
        {
            var hold = new ConversationHold();
            hold.Release();
            Assert.That(hold.Phase, Is.EqualTo(ConversationHoldPhase.Idle));
        }

        [Test]
        public void CancelHandsHerStraightBack_WithNoWalk()
        {
            ConversationHold hold = Held(Spot + Vector2.right);
            var clockSays = new Vector2(40f, 9f);

            hold.Cancel();

            Assert.That(hold.IsActive, Is.False);
            Assert.That(hold.Step(clockSays, 0.1f, 2f), Is.EqualTo(clockSays),
                        "for teardown, and for the frame the clock takes her behind a door — where a " +
                        "catch-up walk would be a walk through a wall");
        }

        // ---- the shelter freeze ---------------------------------------------------------------

        [Test]
        public void TheShelterAnswerIsFrozenWithTheBlockItCameFrom()
        {
            var hold = new ConversationHold();
            hold.TryBegin(Spot, Spot + Vector2.right, interruptible: true,
                          RoutineShelter.InsideDestination, shelterBlockIndex: 3);

            Assert.That(hold.Shelter, Is.EqualTo(RoutineShelter.InsideDestination),
                        "an indoor conversation must not blink her out of existence mid-sentence");
            Assert.That(hold.ShelterBlockIndex, Is.EqualTo(3),
                        "and the index goes with it — that answer indexes THAT block's interior, so " +
                        "reading it against a later block would be a question about the wrong house");
        }
    }
}
