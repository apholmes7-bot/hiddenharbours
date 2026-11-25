using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A HULL'S FORE BLOCK IS HELD ONLY WHILE SOMEBODY STANDS ON HER DECK</b> (ADR 0056, the
    /// facet-id budget note §4.1).
    ///
    /// <para>The facet buffer's alpha is one 8-bit id space shared by hull ids, their twelve-wide
    /// fore blocks and ashore figures. On main every live hull took thirteen ids at enable, so
    /// eighteen moored boats spent the whole space and the player's ashore figure was refused: she
    /// fell back to her old sprite at St Peters after Nine Mile Creek had loaded. A block is only
    /// ever READ while a deck slot is claimed, so it is now taken at the first claim and given back
    /// at the last release. These are the books that must balance for that to be safe:</para>
    /// <list type="number">
    /// <item>nobody aboard: one id, no block;</item>
    /// <item>the first claim takes the block, every later claim shares it, the last release frees it;</item>
    /// <item>hundreds of board/leave cycles leave the pool where one block would leave it;</item>
    /// <item>a claim made while the hull was off is honoured when she comes back;</item>
    /// <item>a hull refused a block at a full pool gets one, quietly, at her next claim after one frees.</item>
    /// </list>
    ///
    /// <para>Every case runs on a pool of the test's own
    /// (<see cref="IsoFacetHullRegistry.SwapIdPoolForTests"/>), restored only after every hull that
    /// drew from it is destroyed, so no id taken here is ever freed into the live pool. Uses only
    /// members main already had, so it compiles there and FAILS there by behaviour. No GPU.</para>
    /// </summary>
    public sealed class FacetIdLazyForeBlockTests
    {
        private static readonly Regex BlockRefused = new Regex(@"gets NO deck-occupant block");
        private const int Block = IsoFacetHullRenderer.DeckOccupantSlots;
        /// <summary>The highest id a plain (reserve-free) pool hands to a hull or a block: 255 is the
        /// shared overflow id.</summary>
        private const int PlainPoolTop = 254;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private IsoFacetIdPool _livePool;
        private IsoFacetIdPool _pool;

        [SetUp]
        public void SetUp()
        {
            _pool = new IsoFacetIdPool();
            _livePool = IsoFacetHullRegistry.SwapIdPoolForTests(_pool);
        }

        [TearDown]
        public void TearDown()
        {
            // Hulls first: their ids go back to the TEST pool. Only then the live pool comes back.
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            if (_livePool != null) IsoFacetHullRegistry.SwapIdPoolForTests(_livePool);
            _livePool = null;
            _pool = null;
        }

        private IsoFacetHullRenderer NewHull(string name = "LazyBlockHull")
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go.AddComponent<IsoFacetHullRenderer>();
        }

        // ================================================================== the empty deck

        [Test]
        public void NobodyAboard_TheHullHoldsOneIdAndNoBlock()
        {
            IsoFacetHullRenderer hull = NewHull();
            Assert.AreEqual(1, hull.HullId, "a fresh pool's first hull id is 1");
            Assert.AreEqual(0, hull.ForeHullId,
                "a hull nobody stands on holds a fore block — twelve ids spent on a deck with nobody to " +
                "hide. Eighteen such boats spend the whole 8-bit space and the player's ashore figure is " +
                "refused (she falls back to her sprite).");
            Assert.AreEqual(0f, hull.DeckOccupants.OccluderIdTop, 0f);
            Assert.AreEqual(2, _pool.TakeId(),
                "registering one empty hull spent more than one id of the pool");
        }

        // ================================================================== first claim, last release

        [Test]
        public void TheFirstClaimTakesTheBlock_LaterClaimsShareIt_TheLastReleaseGivesItBack()
        {
            IsoFacetHullRenderer hull = NewHull();
            object skipper = new object(), hand = new object();

            int a = hull.DeckOccupants.Claim(skipper);
            Assert.GreaterOrEqual(a, 0);
            int block = hull.ForeHullId;
            Assert.AreEqual(hull.HullId + 1, block,
                "the first deck claim must take a twelve-wide block, the next ids after her own");
            Assert.AreEqual(block + Block - 1, hull.ForeHullIdTop);

            Assert.AreEqual(a, hull.DeckOccupants.Claim(skipper), "the per-frame re-claim is idempotent");
            int b = hull.DeckOccupants.Claim(hand);
            Assert.GreaterOrEqual(b, 0);
            Assert.AreEqual(block, hull.ForeHullId, "a second occupant shares the block; nothing more is taken");

            hull.DeckOccupants.Release(a, skipper);
            Assert.AreEqual(block, hull.ForeHullId,
                "the block went with the FIRST release while the hand still stands on her deck");

            hull.DeckOccupants.Release(b, hand);
            Assert.AreEqual(0, hull.ForeHullId, "the last release must give the block back");
            Assert.AreEqual(0f, hull.DeckOccupants.OccluderIdTop, 0f);
            Assert.AreEqual(block, _pool.TakeIdBlock(Block),
                "the freed block is not first in line, whole, for the next hull that needs one");
        }

        [Test]
        public void AStaleReleaseDoesNotFreeTheBlockUnderALiveClaim()
        {
            IsoFacetHullRenderer hull = NewHull();
            object mine = new object(), stranger = new object();
            int slot = hull.DeckOccupants.Claim(mine);
            int block = hull.ForeHullId;
            Assert.AreNotEqual(0, block);

            hull.DeckOccupants.Release(slot, stranger);
            Assert.AreEqual(block, hull.ForeHullId, "a stranger's release freed the block under her feet");
            hull.DeckOccupants.Release(slot, mine);
            Assert.AreEqual(0, hull.ForeHullId);
        }

        // ================================================================== many cycles, no leak

        [Test]
        public void HundredsOfBoardAndLeaveCycles_LeaveThePoolWhereTwoBlocksWould()
        {
            const int Hulls = 3, Cycles = 600;
            var hulls = new IsoFacetHullRenderer[Hulls];
            for (int h = 0; h < Hulls; h++) hulls[h] = NewHull("LazyBlockHull" + h);
            var bases = new HashSet<int>();

            for (int c = 0; c < Cycles; c++)
            {
                IsoFacetHullRenderer first = hulls[c % Hulls];
                IsoFacetHullRenderer second = hulls[(c + 1) % Hulls];
                object rider = new object(), crew = new object();

                int s = first.DeckOccupants.Claim(rider);
                for (int frame = 0; frame < 3; frame++)
                    Assert.AreEqual(s, first.DeckOccupants.Claim(rider), "the per-frame re-claim moved her slot");
                Assert.AreNotEqual(0, first.ForeHullId, $"cycle {c}: a boarded hull holds no block");
                bases.Add(first.ForeHullId);

                // Every fifth cycle somebody is aboard a second boat at once: two blocks live.
                int t = -1;
                if (c % 5 == 0)
                {
                    t = second.DeckOccupants.Claim(crew);
                    Assert.AreNotEqual(0, second.ForeHullId);
                    Assert.AreNotEqual(first.ForeHullId, second.ForeHullId, "two boarded hulls share one block");
                    bases.Add(second.ForeHullId);
                }

                first.DeckOccupants.Release(s, rider);
                if (t >= 0) second.DeckOccupants.Release(t, crew);
                foreach (IsoFacetHullRenderer h in hulls)
                    Assert.AreEqual(0, h.ForeHullId, $"cycle {c}: '{h.name}' kept a block with nobody aboard");
            }

            Assert.AreEqual(2, bases.Count,
                $"{bases.Count} distinct blocks were issued for at most two decks occupied at once — freed " +
                "blocks are not being reused whole, and every boarding is leaking twelve ids");
            Assert.AreEqual(Hulls + 2 * Block + 1, _pool.TakeId(),
                $"after {Cycles} board/leave cycles the pool's next fresh id is not {Hulls} hull ids + two " +
                "blocks + 1: ids leaked (or the hulls took blocks nobody claimed)");
        }

        // ================================================================== off and on again

        [Test]
        public void AHullSwitchedOffGivesItsBlockBack_AndAClaimMadeWhileOffIsHonouredOnReturn()
        {
            IsoFacetHullRenderer hull = NewHull();
            object rider = new object();
            hull.DeckOccupants.Claim(rider);
            int block = hull.ForeHullId;
            Assert.AreNotEqual(0, block);

            hull.enabled = false;
            Assert.AreEqual(0, hull.ForeHullId, "a disabled hull kept her block");
            Assert.AreEqual(block, _pool.TakeIdBlock(Block), "the disabled hull's block did not go back to the pool");
            _pool.FreeBlock(block, Block);

            // A holder that enables before her (a moored skipper whose OnEnable ran first) claims while
            // she is off: no block can be taken for an unregistered hull, and none is lost.
            int slot = hull.DeckOccupants.Claim(rider);
            Assert.GreaterOrEqual(slot, 0);
            Assert.AreEqual(0, hull.ForeHullId, "a block was taken for a hull that is not registered");

            hull.enabled = true;
            Assert.AreNotEqual(0, hull.HullId);
            Assert.AreNotEqual(0, hull.ForeHullId,
                "the claim made while she was off was not honoured when she came back — the skipper " +
                "would draw over her wheelhouse until he re-boarded");

            hull.DeckOccupants.Release(slot, rider);
            Assert.AreEqual(0, hull.ForeHullId);
        }

        // ================================================================== refused, then quietly granted

        [Test]
        public void AHullRefusedABlock_GetsOneQuietlyAtHerNextClaimOnceOneFrees()
        {
            IsoFacetHullRenderer starved = NewHull("Starved");
            IsoFacetHullRenderer holder = NewHull("Holder");
            object holderCrew = new object();
            int held = holder.DeckOccupants.Claim(holderCrew);
            int holdersBlock = holder.ForeHullId;
            Assert.AreNotEqual(0, holdersBlock);

            // Spend the singles until twelve contiguous ids no longer fit below the top.
            while (_pool.TakeId() < PlainPoolTop - Block + 1) { }

            object rider = new object();
            LogAssert.Expect(LogType.Warning, BlockRefused);
            int slot = starved.DeckOccupants.Claim(rider);
            Assert.GreaterOrEqual(slot, 0, "a refused BLOCK must not refuse the SLOT");
            Assert.AreEqual(0, starved.ForeHullId, "the harness did not fill the pool");

            // The rider re-claims every frame: no block yet, and no log per frame.
            for (int frame = 0; frame < 30; frame++)
                Assert.AreEqual(slot, starved.DeckOccupants.Claim(rider));
            Assert.AreEqual(0, starved.ForeHullId);
            LogAssert.NoUnexpectedReceived();

            holder.DeckOccupants.Release(held, holderCrew);
            Assert.AreEqual(0, holder.ForeHullId);
            Assert.AreEqual(slot, starved.DeckOccupants.Claim(rider));
            Assert.AreEqual(holdersBlock, starved.ForeHullId,
                "the freed block was not taken at her next claim — a refusal latched until she re-boarded");

            starved.DeckOccupants.Release(slot, rider);
            Assert.AreEqual(0, starved.ForeHullId);
        }
    }
}
