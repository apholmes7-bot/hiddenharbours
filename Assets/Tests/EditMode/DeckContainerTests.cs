using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The deck fish tray's pure logic (the diegetic hold read): the counts→fill-state mapping — pinned
    /// ends (empty only when EMPTY, brim only when FULL), a linear interior, monotonic, degenerate-safe —
    /// and the deck-frame anchor math, held in lockstep with the deck-walk clamp's frame (the two are
    /// separate lanes so Boats can't reference Player; this parity test is what stops them drifting).
    /// All pure + deterministic — no scene, no time, no randomness.
    /// </summary>
    public class DeckContainerTests
    {
        // ---- FillStateIndex: the pinned ends ---------------------------------------------------

        [Test]
        public void EmptyHold_AlwaysReadsTheEmptyState()
        {
            for (int states = 1; states <= 8; states++)
                for (int cap = 1; cap <= 20; cap++)
                    Assert.AreEqual(0, DeckContainerPresenter.FillStateIndex(0, cap, states),
                        $"an empty hold must read EMPTY (cap {cap}, {states} states)");
            Assert.AreEqual(0, DeckContainerPresenter.FillStateIndex(-3, 6, 4), "negative used clamps to empty");
        }

        [Test]
        public void FullHold_AlwaysReadsTheBrimState()
        {
            for (int states = 2; states <= 8; states++)
                for (int cap = 1; cap <= 20; cap++)
                {
                    Assert.AreEqual(states - 1, DeckContainerPresenter.FillStateIndex(cap, cap, states),
                        $"a full hold must read BRIM (cap {cap}, {states} states)");
                    Assert.AreEqual(states - 1, DeckContainerPresenter.FillStateIndex(cap + 3, cap, states),
                        "over-capacity (defensive) still reads brim");
                }
        }

        [Test]
        public void PartialFills_NeverTouchThePinnedEnds()
        {
            // With ≥3 states, anything aboard but not full stays strictly inside (1..count-2): one banded
            // keeper always SHOWS, and only a truly full hold heaps the tray.
            for (int states = 3; states <= 6; states++)
                for (int cap = 2; cap <= 20; cap++)
                    for (int used = 1; used < cap; used++)
                    {
                        int idx = DeckContainerPresenter.FillStateIndex(used, cap, states);
                        Assert.GreaterOrEqual(idx, 1, $"partial ({used}/{cap}, {states} states) must not read empty");
                        Assert.LessOrEqual(idx, states - 2, $"partial ({used}/{cap}, {states} states) must not read brim");
                    }
        }

        [Test]
        public void Mapping_IsMonotonic_AsTheHoldFills()
        {
            for (int states = 2; states <= 6; states++)
                for (int cap = 1; cap <= 20; cap++)
                {
                    int last = -1;
                    for (int used = 0; used <= cap; used++)
                    {
                        int idx = DeckContainerPresenter.FillStateIndex(used, cap, states);
                        Assert.GreaterOrEqual(idx, last, $"the tray never LOSES fill as the hold gains ({used}/{cap}, {states} states)");
                        last = idx;
                    }
                }
        }

        [Test]
        public void TwoStateArt_ReadsFullWithAnythingAboard()
        {
            // Only empty/brim painted: a single keeper flips the tray to the fuller look.
            Assert.AreEqual(1, DeckContainerPresenter.FillStateIndex(1, 6, 2));
            Assert.AreEqual(1, DeckContainerPresenter.FillStateIndex(5, 6, 2));
            Assert.AreEqual(0, DeckContainerPresenter.FillStateIndex(0, 6, 2));
        }

        [Test]
        public void DegenerateInputs_FallToTheEmptyState()
        {
            Assert.AreEqual(0, DeckContainerPresenter.FillStateIndex(3, 0, 4), "no capacity → empty, never a divide");
            Assert.AreEqual(0, DeckContainerPresenter.FillStateIndex(3, -2, 4), "negative capacity → empty");
            Assert.AreEqual(0, DeckContainerPresenter.FillStateIndex(3, 6, 0), "no states → index 0");
            Assert.AreEqual(0, DeckContainerPresenter.FillStateIndex(3, 6, -1), "negative states → index 0");
            Assert.AreEqual(0, DeckContainerPresenter.FillStateIndex(3, 6, 1), "one-state art is always that state");
        }

        [Test]
        public void GreyboxLadder_TheSkiffHold_ReadsEmptyLowHalfBrim()
        {
            // The shipped read: HoldUnits 6 (dory/skiff) over the 4 greybox states — the exact ladder
            // the owner sees as he bands keepers one by one.
            int[] expected = { 0, 1, 1, 2, 2, 2, 3 };
            for (int used = 0; used <= 6; used++)
                Assert.AreEqual(expected[used], DeckContainerPresenter.FillStateIndex(used, 6, 4),
                    $"{used}/6 keepers");
        }

        // ---- DeckOffsetToWorld: the drawn-facing frame -----------------------------------------

        [Test]
        public void DeckOffset_MatchesTheDeckWalkClampFrame()
        {
            // The tray must sit in the SAME deck frame the player walks (DeckWalkController's) — the
            // math is duplicated across lanes (Boats can't reference Player), so parity is tested.
            Vector2[] offsets =
            {
                Vector2.zero, new Vector2(0.35f, -0.9f), new Vector2(-0.5f, 1.2f), new Vector2(0.7f, 0f),
            };
            for (int h = 0; h < 360; h += 45)   // the 8 drawn facings (and the parity holds off-grid too)
                foreach (Vector2 off in offsets)
                {
                    Vector2 tray = DeckContainerPresenter.DeckOffsetToWorld(off, h);
                    Vector2 walk = DeckWalkController.DeckFrameToWorld(off, h);
                    Assert.AreEqual(walk.x, tray.x, 1e-5f, $"x drifted from the deck-walk frame at {h}°");
                    Assert.AreEqual(walk.y, tray.y, 1e-5f, $"y drifted from the deck-walk frame at {h}°");
                }
            // Off-grid headings too (a smooth-rotating hull clamps to its true heading).
            Vector2 a = DeckContainerPresenter.DeckOffsetToWorld(new Vector2(0.35f, -0.9f), 123.4f);
            Vector2 b = DeckWalkController.DeckFrameToWorld(new Vector2(0.35f, -0.9f), 123.4f);
            Assert.AreEqual(b.x, a.x, 1e-5f);
            Assert.AreEqual(b.y, a.y, 1e-5f);
        }

        [Test]
        public void DeckOffset_FollowsTheDrawnBow()
        {
            // North (0°): the deck frame IS world axes — toward-the-bow (+y) is up, starboard (+x) is east.
            Vector2 north = DeckContainerPresenter.DeckOffsetToWorld(new Vector2(0f, 1f), 0f);
            Assert.AreEqual(0f, north.x, 1e-5f);
            Assert.AreEqual(1f, north.y, 1e-5f);

            // East (90°): toward-the-bow now points east; starboard points south.
            Vector2 eastBow = DeckContainerPresenter.DeckOffsetToWorld(new Vector2(0f, 1f), 90f);
            Assert.AreEqual(1f, eastBow.x, 1e-5f);
            Assert.AreEqual(0f, eastBow.y, 1e-5f);
            Vector2 eastStarboard = DeckContainerPresenter.DeckOffsetToWorld(new Vector2(1f, 0f), 90f);
            Assert.AreEqual(0f, eastStarboard.x, 1e-5f);
            Assert.AreEqual(-1f, eastStarboard.y, 1e-5f);

            // South (180°): everything mirrors.
            Vector2 south = DeckContainerPresenter.DeckOffsetToWorld(new Vector2(0.35f, -0.9f), 180f);
            Assert.AreEqual(-0.35f, south.x, 1e-4f);
            Assert.AreEqual(0.9f, south.y, 1e-4f);
        }
    }
}
