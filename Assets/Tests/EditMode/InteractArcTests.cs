using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The facing rule, on its own.</b> <see cref="InteractArc"/> is the one answer to "is the thing in
    /// front of me?", shared by <see cref="InteractResolver"/> (fixtures) and <c>WorldInteractor</c>
    /// (conversations) — so it is worth proving once, exhaustively, rather than twice by inference from
    /// whichever caller happens to be under test.
    ///
    /// <para><b>Pure by construction, and that is the point of the suite.</b> No <c>GameObject</c>, no
    /// component, no lifecycle. EditMode fires no <c>Awake</c>/<c>OnEnable</c>/<c>OnDisable</c>, so a
    /// suite that stood components up and asserted on their lifecycle would be asserting on something
    /// that never ran (the trap that went red in this repo on 2026-08-18). Everything here is a static
    /// call on values.</para>
    /// </summary>
    public class InteractArcTests
    {
        private static Vector2 At(float degrees, float metres = 1f)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * metres;
        }

        // The fisher's four facings, as the walk controller reports them. Named because the arc's whole
        // shipped value is chosen against these and a test that spelled (1,0) would hide that.
        private static readonly Vector2 FaceRight = Vector2.right;
        private static readonly Vector2 FaceUp    = Vector2.up;

        // =====================================================================================
        //  THE ARC ITSELF
        // =====================================================================================

        [Test]
        public void DeadAhead_IsAlwaysInside_EvenAtAZeroArc()
        {
            // A zero arc is "exactly the way I am looking", not "nothing" — the honest reading of the
            // degenerate end, and the one that keeps Clamp's lower bound from being a trapdoor.
            Assert.IsTrue(InteractArc.Contains(At(0f), FaceRight, InteractArc.CosHalfArc(0f)));
            Assert.IsTrue(InteractArc.Contains(At(0f), FaceRight, InteractArc.CosHalfArc(120f)));
        }

        [Test]
        public void DirectlyBehind_IsOutside_ForEveryArcShortOfTheWholeCircle()
        {
            Vector2 behind = At(180f);
            Assert.IsFalse(InteractArc.Contains(behind, FaceRight, InteractArc.CosHalfArc(120f)));
            Assert.IsFalse(InteractArc.Contains(behind, FaceRight, InteractArc.CosHalfArc(179f)));
            // ...and at 360 the test cannot exclude anything at all, which is how the filter is switched off.
            Assert.IsTrue(InteractArc.Contains(behind, FaceRight, InteractArc.CosHalfArc(360f)));
        }

        [Test]
        public void TheBoundaryIsTheHALFArc_AndItIsInclusive()
        {
            float cos = InteractArc.CosHalfArc(120f);   // ±60°
            Assert.IsTrue(InteractArc.Contains(At(59f), FaceRight, cos), "just inside");
            Assert.IsTrue(InteractArc.Contains(At(-59f), FaceRight, cos), "just inside, other side");
            Assert.IsFalse(InteractArc.Contains(At(61f), FaceRight, cos), "just outside");
            Assert.IsFalse(InteractArc.Contains(At(-61f), FaceRight, cos), "just outside, other side");
        }

        [Test]
        public void TheArcIsSymmetric_AboutTheFacing()
        {
            float cos = InteractArc.CosHalfArc(120f);
            for (int d = 0; d <= 180; d += 5)
                Assert.AreEqual(InteractArc.Contains(At(d), FaceRight, cos),
                                InteractArc.Contains(At(-d), FaceRight, cos),
                                $"±{d}° must answer the same — the arc is centred, not lopsided");
        }

        [Test]
        public void DistanceDoesNotChangeTheAnswer_OnlyDirectionDoes()
        {
            // The un-normalised form multiplies the cosine by |delta|, so a bug there would show up as an
            // arc that narrows or widens with range. It must not.
            float cos = InteractArc.CosHalfArc(120f);
            foreach (float m in new[] { 0.01f, 0.5f, 1f, 7f, 250f })
            {
                Assert.IsTrue(InteractArc.Contains(At(45f, m), FaceRight, cos), $"inside at {m} m");
                Assert.IsFalse(InteractArc.Contains(At(90f, m), FaceRight, cos), $"outside at {m} m");
            }
        }

        // =====================================================================================
        //  THE TWO THINGS THAT ALWAYS PASS
        // =====================================================================================

        [Test]
        public void UnknownFacing_PassesEverything_SoTheFilterFailsOPEN()
        {
            // InteractActor.Facing documents zero as "unknown". A candidate must then be reachable from
            // every direction rather than from a guessed one — the degraded mode is the OLD behaviour
            // (proximity only), never a fixture nobody can reach.
            float cos = InteractArc.CosHalfArc(1f);    // absurdly tight; still must not bite
            Assert.IsTrue(InteractArc.Contains(At(180f), Vector2.zero, cos));
            Assert.IsTrue(InteractArc.Contains(At(90f), Vector2.zero, cos));
        }

        [Test]
        public void StandingOnTheThing_PassesRegardlessOfFacing()
        {
            // The direction to something you are standing on is floating-point noise; without this guard a
            // candidate underfoot would flicker in and out as the noise swung round the compass.
            float cos = InteractArc.CosHalfArc(90f);
            Assert.IsTrue(InteractArc.Contains(Vector2.zero, FaceRight, cos));
            Assert.IsTrue(InteractArc.Contains(new Vector2(1e-6f, -1e-6f), FaceUp, cos),
                          "well inside SameSpotSqrMeters (1e-8 squared metres)");
        }

        [Test]
        public void AnUnNormalisedFacing_IsCorrectedRatherThanTrusted()
        {
            float cos = InteractArc.CosHalfArc(120f);
            Vector2 longFacing = FaceRight * 37f;
            Assert.IsTrue(InteractArc.Contains(At(30f), longFacing, cos));
            Assert.IsFalse(InteractArc.Contains(At(80f), longFacing, cos));
        }

        // =====================================================================================
        //  THE SHIPPED ARC, AGAINST THE FOUR-WAY FACING IT WAS CHOSEN FOR
        // =====================================================================================

        [Test]
        public void TheShippedConversationArc_OverlapsEveryDiagonal_SoThereIsNoDeadWedge()
        {
            // The fisher has four facings, so the quadrants tile at ±45°. WorldInteractor ships 120° (±60°)
            // precisely so a candidate sitting ON a diagonal is inside BOTH adjacent facings rather than on
            // a knife-edge between them. This is the assertion that value exists for.
            float cos = InteractArc.CosHalfArc(HiddenHarbours.World.WorldInteractor.DefaultFacingArcDegrees);
            Vector2 upRight = At(45f);
            Assert.IsTrue(InteractArc.Contains(upRight, FaceRight, cos), "reachable facing right");
            Assert.IsTrue(InteractArc.Contains(upRight, FaceUp, cos), "and facing up — the overlap");
        }

        [Test]
        public void TheShippedConversationArc_StillExcludesAbeamAndBehind()
        {
            // The other half of the choice: forgiving is not the same as useless. Somebody square on your
            // beam is not somebody you are looking at, and the popup shows ONE line.
            float cos = InteractArc.CosHalfArc(HiddenHarbours.World.WorldInteractor.DefaultFacingArcDegrees);
            Assert.IsFalse(InteractArc.Contains(At(90f), FaceRight, cos), "abeam to port");
            Assert.IsFalse(InteractArc.Contains(At(-90f), FaceRight, cos), "abeam to starboard");
            Assert.IsFalse(InteractArc.Contains(At(135f), FaceRight, cos), "over the shoulder");
        }

        [Test]
        public void TheShippedConversationArcIsTighterThanTheResolversDefault_AndBothAreNamed()
        {
            // Rule 6: neither number is a literal at a call site, and the relationship between them is the
            // design decision. A conversation must pick ONE person; a fixture you stand at need not.
            Assert.Less(HiddenHarbours.World.WorldInteractor.DefaultFacingArcDegrees,
                        InteractResolver.DefaultFacingArcDegrees);
        }

        // =====================================================================================
        //  THE COSINE HELPER
        // =====================================================================================

        [Test]
        public void CosHalfArc_ClampsRatherThanMisbehaving()
        {
            Assert.AreEqual(InteractArc.CosHalfArc(0f), InteractArc.CosHalfArc(-90f), 1e-6f,
                            "negative clamps to zero");
            Assert.AreEqual(InteractArc.CosHalfArc(360f), InteractArc.CosHalfArc(900f), 1e-6f,
                            "over a full circle clamps to a full circle");
            Assert.AreEqual(0f, InteractArc.CosHalfArc(180f), 1e-6f, "±90° is a cosine of zero");
        }

        [Test]
        public void TheOneCallForm_AgreesWithTheTwoCallForm()
        {
            // The convenience overload exists for single-candidate callers and tests; it must not be a
            // second implementation. Same answer, every angle.
            Vector2 me = new Vector2(3f, -4f);
            for (int d = 0; d < 360; d += 7)
            {
                Vector2 target = me + At(d, 2f);
                Assert.AreEqual(InteractArc.Contains(target - me, FaceRight, InteractArc.CosHalfArc(120f)),
                                InteractArc.Contains(me, FaceRight, target, 120f),
                                $"at {d}°");
            }
        }

        // =====================================================================================
        //  THE RESOLVER STILL BEHAVES — the extraction must be a refactor, not a change
        // =====================================================================================

        private sealed class Fake : IInteractable
        {
            public string Id { get; set; } = "fixture.test";
            public Vector2 WorldPosition { get; set; }
            public float ReachMeters { get; set; } = 5f;
            public int Priority { get; set; } = InteractPriority.Fixture;
            public InteractContext Contexts { get; set; } = InteractContext.OnFoot;
            public bool RequiresFacing { get; set; } = true;
            public bool IsAvailable { get; set; } = true;
            public string VerbLabel { get; set; } = "Do the thing";
            public void Interact(in InteractActor actor) { }
        }

        [Test]
        public void TheResolverStillFiltersByArc_ThroughTheSharedRule()
        {
            var behind = new Fake { Id = "a", WorldPosition = At(180f, 1f) };
            var ahead = new Fake { Id = "b", WorldPosition = At(0f, 2f) };
            var list = new System.Collections.Generic.List<IInteractable> { behind, ahead };

            var actor = InteractActor.For(Vector2.zero, FaceRight, ControlMode.OnFoot);
            Assert.IsTrue(InteractResolver.TryResolve(list, actor, 120f, out IInteractable best));
            Assert.AreSame(ahead, best, "the nearer one is BEHIND you — facing is a filter, not a tie-break");
        }

        [Test]
        public void TheResolverIgnoresTheArc_ForACandidateThatDoesNotAskForIt()
        {
            // The migration guarantee: an omnidirectional registrant behaves exactly as it did before the
            // extraction, whichever way the actor happens to be pointed.
            var behind = new Fake { Id = "a", WorldPosition = At(180f, 1f), RequiresFacing = false };
            var list = new System.Collections.Generic.List<IInteractable> { behind };
            var actor = InteractActor.For(Vector2.zero, FaceRight, ControlMode.OnFoot);
            Assert.IsTrue(InteractResolver.TryResolve(list, actor, 0f, out IInteractable best));
            Assert.AreSame(behind, best);
        }
    }
}
