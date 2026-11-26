using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The turned plank deck's rules, with no scene: where it is stood on, and how it guides a walker. The owner
    /// (11-26): the bridge "turns to face the camera changing the players running angle". The cause was a bridge forced
    /// onto the world's x with the path bent into each end, so a walker running the path met a rail end and was slid
    /// round it. These pin the turned deck that replaced it.
    /// </summary>
    public class FootbridgeDeckTests
    {
        static readonly Vector2 At = new Vector2(156.45f, -40.95f);
        const float Axis = 26.56505f, Span = 3f, Width = 1.15f, Reach = 0.8f, Cone = 50f, Approach = 0.8f;

        static Vector2 Dir(float degrees) => FootbridgeDef.AxisOf(degrees);

        [Test]
        public void AnUnturnedDeck_StandsExactlyWhereTheOldUprightRectangleDid()
        {
            var old = new Rect(At.x - Span * 0.5f, At.y - Width * 0.5f, Span, Width + Reach);
            for (float x = old.xMin - 0.3f; x <= old.xMax + 0.3f; x += 0.07f)
            for (float y = old.yMin - 0.3f; y <= old.yMax + 0.3f; y += 0.07f)
            {
                var p = new Vector2(x, y);
                Assert.That(FootbridgeDeck.StandsOn(p, At, 0f, Span, Width, Reach), Is.EqualTo(StandablePlatform.Contains(old, p)), p.ToString());
            }
        }

        [Test]
        public void ATurnedDeck_IsStoodOnAlongItsAxis_AndReadUpTheWorldByTheReach()
        {
            Vector2 a = Dir(Axis), n = new Vector2(-a.y, a.x);
            foreach (float u in new[] { -1.45f, 0f, 1.45f })
            foreach (float v in new[] { -0.55f, 0f, 0.55f })
            {
                Vector2 p = At + a * u + n * v;
                Assert.That(FootbridgeDeck.OnPlanks(p, At, Axis, Span, Width), Is.True, $"({u}, {v})");
                Assert.That(FootbridgeDeck.StandsOn(p, At, Axis, Span, Width, Reach), Is.True);
                Assert.That(FootbridgeDeck.StandsOn(p + Vector2.up * (Reach - 0.01f), At, Axis, Span, Width, Reach), Is.True,
                    "a walker's body, drawn above his feet on the planks, is read on the deck");
                Assert.That(FootbridgeDeck.StandsOn(p + Vector2.down * 0.9f, At, Axis, Span, Width, Reach) &&
                            !FootbridgeDeck.OnPlanks(p + Vector2.down * 0.9f, At, Axis, Span, Width), Is.False,
                    "the reach runs up the world only, never down it");
            }
            // Past either end along the axis, and past either long edge across it, is the bank or the brook.
            foreach (float s in new[] { -1f, 1f })
            {
                Assert.That(FootbridgeDeck.OnPlanks(At + a * (s * (Span * 0.5f + 0.02f)), At, Axis, Span, Width), Is.False);
                Assert.That(FootbridgeDeck.OnPlanks(At + n * (s * (Width * 0.5f + 0.02f)), At, Axis, Span, Width), Is.False);
            }
            Assert.That(FootbridgeDeck.StandsOn(At - n * (Width * 0.5f + 0.05f), At, Axis, Span, Width, Reach), Is.False,
                "seaward of the planks is the brook");
        }

        [Test]
        public void EveryKeyboardHeading_NearTheAxis_CrossesStraightAlongIt_AtFullPace()
        {
            Vector2 a = Dir(Axis), from = At;
            const float step = 0.1f;
            // East, north-east, west and south-west: the four of a keyboard's eight headings near this axis.
            foreach (float heading in new[] { 0f, 45f, 180f, 225f })
            {
                Vector2 to = FootbridgeDeck.Guide(from, from + Dir(heading) * step, a, Cone);
                Vector2 moved = to - from;
                Assert.That(moved.magnitude, Is.EqualTo(step).Within(1e-5f), $"heading {heading}: the pace is kept");
                Assert.That(Vector2.Angle(moved, Vector2.Dot(Dir(heading), a) > 0f ? a : -a), Is.LessThan(0.01f),
                    $"heading {heading}: the step goes along the bridge, not across it");
            }
        }

        [Test]
        public void AHeadingWideOfTheAxis_IsLeftAsItIs_AndAnAnalogueRunAlongItIsUntouched()
        {
            Vector2 a = Dir(Axis), from = At;
            foreach (Vector2 wide in new[] { Vector2.up, Vector2.down, Dir(135f), Dir(-45f) })
                Assert.That(FootbridgeDeck.Guide(from, from + wide * 0.1f, a, Cone), Is.EqualTo(from + wide * 0.1f),
                    $"heading {wide}: across the walk the rails (or the open bank) decide, not the guide");
            Vector2 along = FootbridgeDeck.Guide(from, from + a * 0.1f, a, Cone);
            Assert.That(Vector2.Distance(along, from + a * 0.1f), Is.LessThan(1e-4f), "a run along the axis is left as it is");
            Assert.That(FootbridgeDeck.Guide(from, from, a, Cone), Is.EqualTo(from), "standing still stays still");
        }

        [Test]
        public void TheDeck_GuidesOnlyIntoAnEmptySlot_AndLetsGoOffThePlanks()
        {
            var deckGo = new GameObject("deck");
            var playerGo = new GameObject("player");
            Transform before = GameServices.PlayerTransform;
            try
            {
                var deck = deckGo.AddComponent<FootbridgeDeck>();
                deck.Configure("bridge.test", At, Axis, Span, Width, Reach, 5.66f, Cone, Approach);
                var walker = playerGo.AddComponent<FakeWalker>();
                GameServices.PlayerTransform = playerGo.transform;
                var tick = typeof(FootbridgeDeck).GetMethod("FixedUpdate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                playerGo.transform.position = At;
                tick.Invoke(deck, null);
                Assert.That(walker.MovementConstraint, Is.SameAs(deck), "on the planks the deck guides him");
                Assert.That(deck.Guiding, Is.True);

                playerGo.transform.position = At + Dir(Axis) * (Span * 0.5f + Approach * 0.5f);
                tick.Invoke(deck, null);
                Assert.That(walker.MovementConstraint, Is.SameAs(deck), "on the walk's last stretch onto the planks he is already guided");

                playerGo.transform.position = At + Dir(Axis) * (Span * 0.5f + Approach + 0.2f);
                tick.Invoke(deck, null);
                Assert.That(walker.MovementConstraint, Is.Null, "past that he walks as he likes");

                var other = new OtherConstraint();
                walker.MovementConstraint = other;
                playerGo.transform.position = At;
                tick.Invoke(deck, null);
                Assert.That(walker.MovementConstraint, Is.SameAs(other), "a hold already on him is never displaced");
                playerGo.transform.position = At + Vector2.right * 5f;
                tick.Invoke(deck, null);
                Assert.That(walker.MovementConstraint, Is.SameAs(other), "and never released by the deck");
            }
            finally
            {
                GameServices.PlayerTransform = before;
                Object.DestroyImmediate(deckGo);
                Object.DestroyImmediate(playerGo);
            }
        }

        sealed class FakeWalker : MonoBehaviour, IConstrainedWalker
        {
            public IWalkMovementConstraint MovementConstraint { get; set; }
        }

        sealed class OtherConstraint : IWalkMovementConstraint
        {
            public Vector2 ConstrainWalk(Vector2 destination) => destination;
        }
    }
}
