using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A FIGURE CAN HOLD A FACET ID WITH NO HULL UNDER HER — AND THE FLEET'S IDS ARE NO WORSE
    /// OFF FOR IT</b> (ADR 0044, ashore PR 1).
    ///
    /// <para>The facet buffer's alpha is one 8-bit id space shared by hull ids, their twelve-wide
    /// fore blocks and now ashore figures. Two rules keep a figure from damaging the other two, and
    /// each is held here by name:</para>
    /// <list type="number">
    /// <item><b>A figure never takes the block path.</b> Freed fore blocks are recycled whole and
    /// their width is not recorded, so a one-wide take from that stack would later be popped by a
    /// hull asking for twelve, and two boats would share a fore range. A figure pops the singles
    /// stack only; a freed figure id goes back to the singles stack only.</item>
    /// <item><b>A figure refuses at exhaustion.</b> A hull at exhaustion shares id 255 and
    /// composites over another hull; a figure holding 255 would be re-composed by every surplus
    /// hull's overlay. She gets 0 instead, nothing is counted, and she keeps her sprite.</item>
    /// </list>
    ///
    /// <para>And the gate: <see cref="IsoFacetHullFeature.FacetSubjectsLive"/> is main's
    /// <c>Count &gt; 0</c> with one more way in. With no figure registered it IS main's gate.</para>
    ///
    /// <para><b>Exhaustion is driven on a pool of the test's own</b>
    /// (<see cref="IsoFacetHullRegistry.SwapIdPoolForTests"/>), restored in a <c>finally</c>: the live
    /// pool's next id never rewinds, so draining it would starve every hull the rest of the domain
    /// registers. No GPU is used or needed; nothing here is drawn.</para>
    /// </summary>
    public sealed class IsoFacetFigureIdTests
    {
        private const int ForeBlock = 12;
        private static readonly Regex FigureExhausted = new Regex(@"this figure gets NO facet id");
        private static readonly Regex BlockExhausted = new Regex(@"gets NO deck-occupant block");
        private static readonly Regex HullExhausted = new Regex(@"further hulls share id 255");

        /// <summary>A pool whose next id is 255: every single below it has been handed to a hull.</summary>
        private static IsoFacetIdPool DrainedToTheOverflowId()
        {
            var pool = new IsoFacetIdPool();
            for (int i = 1; i < 255; i++)
                Assert.AreEqual(i, pool.TakeId(), "a fresh pool hands hull ids out in order from 1");
            return pool;
        }

        // =================================================================== (b) the block path

        [Test]
        public void AFigureNeverTakesTheBlockPath_AFreedBlockIsNeverHandedToAFigure()
        {
            var pool = new IsoFacetIdPool();
            int block = pool.TakeIdBlock(ForeBlock);
            Assert.AreEqual(1, block, "a fresh pool's first block starts at 1");
            pool.FreeBlock(block, ForeBlock);

            // Spend every single that is left, so the ONLY ids anyone could still be handed are the
            // freed block's.
            for (int i = 1 + ForeBlock; i < 255; i++) pool.TakeId();

            LogAssert.Expect(LogType.Warning, FigureExhausted);
            Assert.AreEqual(0, pool.TakeFigureId(),
                "with the singles spent and a freed twelve-wide block on the stack, a figure was handed " +
                "an id instead of refusing — she popped the BLOCK stack. Its width is not recorded, so the " +
                "next hull asking for twelve would be given the rest of a block a person is standing in.");

            Assert.AreEqual(block, pool.TakeIdBlock(ForeBlock),
                "the freed block must still be whole and first in line for the next hull");
        }

        [Test]
        public void AFreedFigureIdIsNeverHandedOutAsABlockBase()
        {
            var pool = new IsoFacetIdPool();
            var ids = new List<int>();
            for (int i = 1; i < 255; i++) ids.Add(pool.TakeFigureId());
            const int freed = 7;
            Assert.AreEqual(freed, ids[freed - 1]);
            pool.FreeId(freed);

            LogAssert.Expect(LogType.Warning, BlockExhausted);
            Assert.AreEqual(0, pool.TakeIdBlock(ForeBlock),
                "a freed single figure id was handed to a hull as the base of a twelve-wide fore block — " +
                "eleven of those ids belong to other figures");

            Assert.AreEqual(freed, pool.TakeFigureId(), "a freed figure id goes back to the singles, whole");
        }

        // =================================================================== (b) exhaustion

        [Test]
        public void AFigureRefusesAtExhaustion_ItNeverSharesTheOverflowId()
        {
            var pool = new IsoFacetIdPool();
            var seen = new HashSet<int>();
            for (int i = 1; i < 255; i++)
            {
                int id = pool.TakeFigureId();
                Assert.That(id, Is.InRange(1, 254), "a figure id is a real id and never the overflow id");
                Assert.IsTrue(seen.Add(id), $"figure id {id} was handed out twice");
            }

            LogAssert.Expect(LogType.Warning, FigureExhausted);
            Assert.AreEqual(0, pool.TakeFigureId(),
                "the 255th figure was handed an id. The only one left is 255, the id hulls collapse onto " +
                "at exhaustion — a figure holding it is re-composed by every surplus hull's overlay.");

            // The hull rule is unchanged beside it: 255 once, then shared.
            Assert.AreEqual(255, pool.TakeId(), "a hull still takes the last id");
            LogAssert.Expect(LogType.Warning, FigureExhausted);
            Assert.AreEqual(0, pool.TakeFigureId(), "and a figure still refuses after a hull took it");
            LogAssert.Expect(LogType.Warning, HullExhausted);
            Assert.AreEqual(255, pool.TakeId(), "a hull past exhaustion still shares 255, as on main");
        }

        [Test]
        public void TheOverflowIdIsNeverFreedToAFigure()
        {
            IsoFacetIdPool pool = DrainedToTheOverflowId();
            Assert.AreEqual(255, pool.TakeId());
            pool.FreeId(255);
            pool.FreeId(0);

            LogAssert.Expect(LogType.Warning, FigureExhausted);
            Assert.AreEqual(0, pool.TakeFigureId(),
                "255 or 0 went onto the singles stack and a figure was handed it");
        }

        [Test]
        public void AtExhaustion_RegisterFigureRefusesAndTheGateStaysAsItWas()
        {
            IsoFacetIdPool live = IsoFacetHullRegistry.SwapIdPoolForTests(DrainedToTheOverflowId());
            try
            {
                int hulls = IsoFacetHullRegistry.Count;
                int figures = IsoFacetHullRegistry.FigureCount;
                bool gate = IsoFacetHullFeature.FacetSubjectsLive;

                LogAssert.Expect(LogType.Warning, FigureExhausted);
                Assert.AreEqual(0, IsoFacetHullRegistry.RegisterFigure(), "the registry handed out an id the pool refused");

                Assert.AreEqual(figures, IsoFacetHullRegistry.FigureCount,
                    "a REFUSED figure was counted — she would open the facet gate with nothing to draw");
                Assert.AreEqual(hulls, IsoFacetHullRegistry.Count);
                Assert.AreEqual(gate, IsoFacetHullFeature.FacetSubjectsLive);
            }
            finally
            {
                IsoFacetHullRegistry.SwapIdPoolForTests(live);
            }
        }

        // =================================================================== the count and the gate

        [Test]
        public void RegisterFigureRaisesFigureCountAndNeverCount()
        {
            IsoFacetIdPool live = IsoFacetHullRegistry.SwapIdPoolForTests(new IsoFacetIdPool());
            int id = 0;
            try
            {
                int hulls = IsoFacetHullRegistry.Count;
                int figures = IsoFacetHullRegistry.FigureCount;

                id = IsoFacetHullRegistry.RegisterFigure();
                Assert.That(id, Is.InRange(1, 254));
                Assert.AreEqual(figures + 1, IsoFacetHullRegistry.FigureCount);
                Assert.AreEqual(hulls, IsoFacetHullRegistry.Count,
                    "a figure was counted as a HULL — Count is the fleet, and the fore-block budget reads it");

                IsoFacetHullRegistry.UnregisterFigure(id);
                id = 0;
                Assert.AreEqual(figures, IsoFacetHullRegistry.FigureCount);
            }
            finally
            {
                if (id != 0) IsoFacetHullRegistry.UnregisterFigure(id);
                IsoFacetHullRegistry.SwapIdPoolForTests(live);
            }
        }

        [Test]
        public void AFigureIdReleasedTwiceIsFreedOnce()
        {
            IsoFacetIdPool live = IsoFacetHullRegistry.SwapIdPoolForTests(new IsoFacetIdPool());
            var held = new List<int>();
            try
            {
                int id = IsoFacetHullRegistry.RegisterFigure();
                int figures = IsoFacetHullRegistry.FigureCount;
                IsoFacetHullRegistry.UnregisterFigure(id);
                IsoFacetHullRegistry.UnregisterFigure(id);
                Assert.AreEqual(figures - 1, IsoFacetHullRegistry.FigureCount, "a double release counted twice");

                held.Add(IsoFacetHullRegistry.RegisterFigure());
                held.Add(IsoFacetHullRegistry.RegisterFigure());
                Assert.AreNotEqual(held[0], held[1],
                    "a double release pushed one id onto the free stack twice — two figures now share it");
            }
            finally
            {
                foreach (int h in held) IsoFacetHullRegistry.UnregisterFigure(h);
                IsoFacetHullRegistry.SwapIdPoolForTests(live);
            }
        }

        [Test]
        public void OneFigureOpensTheFacetGate_AndReleasingItRestoresMainsGate()
        {
            IsoFacetIdPool live = IsoFacetHullRegistry.SwapIdPoolForTests(new IsoFacetIdPool());
            int id = 0;
            try
            {
                id = IsoFacetHullRegistry.RegisterFigure();
                Assert.AreNotEqual(0, id);
                Assert.IsTrue(IsoFacetHullFeature.FacetSubjectsLive,
                    "a figure holding an id did not open the facet gate — with no hull in the frame she " +
                    "would be configured, posed and never drawn");

                IsoFacetHullRegistry.UnregisterFigure(id);
                id = 0;
                Assert.AreEqual(IsoFacetHullRegistry.Count > 0, IsoFacetHullFeature.FacetSubjectsLive,
                    "with no figure registered the gate must be main's gate, Count > 0, exactly");
            }
            finally
            {
                if (id != 0) IsoFacetHullRegistry.UnregisterFigure(id);
                IsoFacetHullRegistry.SwapIdPoolForTests(live);
            }
        }

        // =================================================================== the player reserve (ADR 0056)

        /// <summary>The owner's default: the player and one co-op friend.</summary>
        private const int Reserve = GameConfig.DefaultPlayerFigureIdReserve;
        private const int Overflow = IsoFacetIdPool.MaxId;

        /// <summary>
        /// A pool with a player reserve of <paramref name="reserve"/>, spent the way a fleet spends it:
        /// a hull id and her fore block while a block fits, hull ids after that, then the overflow id
        /// taken once (silently, as the 255th hull always took it) and shared once (loudly). Every id
        /// the fleet was handed is added to <paramref name="fleet"/>.
        /// </summary>
        private static IsoFacetIdPool ExhaustedByTheFleet(int reserve, HashSet<int> fleet)
        {
            var pool = new IsoFacetIdPool(reserve);
            int id;
            while ((id = pool.TakeId()) != Overflow)
            {
                Assert.IsTrue(fleet.Add(id), $"hull id {id} was handed out twice");
                if (!pool.CanTakeBlock(ForeBlock)) continue;
                int block = pool.TakeIdBlock(ForeBlock);
                for (int i = 0; i < ForeBlock; i++)
                    Assert.IsTrue(fleet.Add(block + i), $"block id {block + i} was handed out twice");
            }
            LogAssert.Expect(LogType.Warning, BlockExhausted);
            Assert.AreEqual(0, pool.TakeIdBlock(ForeBlock), "a block was handed out past exhaustion");
            LogAssert.Expect(LogType.Warning, HullExhausted);
            Assert.AreEqual(Overflow, pool.TakeId(), "a hull past exhaustion shares 255, as on main");
            return pool;
        }

        [Test]
        public void TheDefaultReserveIsTwo_ThePlayerAndOneFriend_AtTheTopBelowTheOverflowId()
        {
            Assert.AreEqual(2, GameConfig.DefaultPlayerFigureIdReserve,
                "the owner's ruling (ADR 0056): the reserve holds the player and one co-op friend");
            var pool = new IsoFacetIdPool(Reserve);
            Assert.AreEqual(Overflow - 1 - Reserve, pool.GeneralTop);
            for (int id = pool.GeneralTop + 1; id < Overflow; id++)
                Assert.IsTrue(pool.IsPlayerReserveId(id), $"{id} is above the general top but not reserved");
            Assert.IsFalse(pool.IsPlayerReserveId(Overflow), "the shared overflow id is never a player's");
            Assert.IsFalse(pool.IsPlayerReserveId(pool.GeneralTop));

            Assert.AreEqual(Overflow - 1, new IsoFacetIdPool().GeneralTop,
                "a pool built with no reserve is main's pool: every id below 255 is the fleet's");
        }

        [Test]
        public void TheFleetExhaustingTheSpace_NeverTakesAPlayersId()
        {
            var fleet = new HashSet<int>();
            IsoFacetIdPool pool = ExhaustedByTheFleet(Reserve, fleet);
            foreach (int id in fleet)
                Assert.IsFalse(pool.IsPlayerReserveId(id),
                    $"the fleet was handed {id}, a player's reserved id — at exhaustion her ashore figure " +
                    "would be refused and she would fall back to her sprite");
            Assert.AreEqual(pool.GeneralTop, MaxOf(fleet),
                "the fleet did not spend every general id before sharing 255");
        }

        [Test]
        public void AtExhaustion_ACastFigureIsRefused_ThePlayerAndOneFriendAreGranted()
        {
            IsoFacetIdPool pool = ExhaustedByTheFleet(Reserve, new HashSet<int>());

            LogAssert.Expect(LogType.Warning, FigureExhausted);
            Assert.AreEqual(0, pool.TakeFigureId(), "a cast figure took an id the fleet had spent");

            int player = pool.TakePlayerFigureId();
            int friend = pool.TakePlayerFigureId();
            Assert.AreEqual(pool.GeneralTop + 1, player, "the player's figure was not granted from the reserve");
            Assert.AreEqual(pool.GeneralTop + 2, friend, "the friend's figure was not granted from the reserve");

            LogAssert.Expect(LogType.Warning, FigureExhausted);
            Assert.AreEqual(0, pool.TakePlayerFigureId(),
                "a THIRD player figure was granted with a two-id reserve and the fleet's ids spent — it can " +
                "only have been 255, the id surplus hulls share");
        }

        [Test]
        public void AFreedPlayerIdGoesBackToTheReserve_NeverToTheFleet()
        {
            var pool = new IsoFacetIdPool(Reserve);
            int player = pool.TakePlayerFigureId();
            Assert.AreEqual(pool.GeneralTop + 1, player);
            pool.FreeFigureId(player);
            pool.FreeId(pool.GeneralTop + 2);   // a hull-path free of a reserved id must be ignored

            var fleet = new HashSet<int>();
            int id;
            while ((id = pool.TakeId()) != Overflow) fleet.Add(id);
            Assert.IsFalse(fleet.Contains(player), "a freed player id was handed to a hull");
            Assert.IsFalse(fleet.Contains(pool.GeneralTop + 2), "a reserved id freed by the hull path reached a hull");

            Assert.AreEqual(player, pool.TakePlayerFigureId(), "the freed player id did not come back to the reserve");
            Assert.AreEqual(pool.GeneralTop + 2, pool.TakePlayerFigureId());
            Assert.AreEqual(0, pool.FreePlayerIds);
        }

        [Test]
        public void WithTheReserveSpent_APlayerFigureFallsBackToAGeneralId()
        {
            var pool = new IsoFacetIdPool(Reserve);
            for (int i = 0; i < Reserve; i++) pool.TakePlayerFigureId();
            Assert.AreEqual(1, pool.TakePlayerFigureId(),
                "with both reserved ids held, a further player figure must take a general id while one is free");
            Assert.AreEqual(1, new IsoFacetIdPool(0).TakePlayerFigureId(),
                "with no reserve a player figure is an ordinary figure, as on main");
        }

        [Test]
        public void AtExhaustion_TheRegistryGrantsThePlayerFigure_AndRefusesTheCast()
        {
            IsoFacetIdPool live = IsoFacetHullRegistry.SwapIdPoolForTests(ExhaustedByTheFleet(Reserve, new HashSet<int>()));
            int player = 0;
            try
            {
                int figures = IsoFacetHullRegistry.FigureCount;

                LogAssert.Expect(LogType.Warning, FigureExhausted);
                Assert.AreEqual(0, IsoFacetHullRegistry.RegisterFigure(), "the registry granted a cast figure at exhaustion");
                Assert.AreEqual(figures, IsoFacetHullRegistry.FigureCount);

                player = IsoFacetHullRegistry.RegisterPlayerFigure();
                Assert.AreEqual(Overflow - Reserve, player,
                    "the registry refused the PLAYER's figure at exhaustion — she falls back to her sprite");
                Assert.AreEqual(figures + 1, IsoFacetHullRegistry.FigureCount);
                Assert.IsTrue(IsoFacetHullFeature.FacetSubjectsLive);

                IsoFacetHullRegistry.UnregisterFigure(player);
                Assert.AreEqual(figures, IsoFacetHullRegistry.FigureCount);
                player = IsoFacetHullRegistry.RegisterPlayerFigure();
                Assert.AreEqual(Overflow - Reserve, player, "her id did not come back to the reserve when she left");
            }
            finally
            {
                if (player != 0) IsoFacetHullRegistry.UnregisterFigure(player);
                IsoFacetHullRegistry.SwapIdPoolForTests(live);
            }
        }

        /// <summary>
        /// The same promise through real hulls: a harbour of boats, each with somebody on deck (so each
        /// takes its lazy fore block), registered until the space is spent and 255 is shared. No hull
        /// holds a player's id, and the player's figure is still granted. The hulls are destroyed
        /// BEFORE the live pool comes back, so every id goes home to the test's pool.
        /// </summary>
        [Test]
        public void ARealFleetThatExhaustsTheSpace_NeverTakesAPlayersId_AndThePlayerIsStillGranted()
        {
            var pool = new IsoFacetIdPool(Reserve);
            IsoFacetIdPool live = IsoFacetHullRegistry.SwapIdPoolForTests(pool);
            var spawned = new List<GameObject>();
            int player = 0;
            try
            {
                var held = new HashSet<int>();
                int sharing = 0;
                for (int n = 0; n < Overflow + 1 && sharing < 2; n++)
                {
                    var go = new GameObject("ReserveFleetHull" + n);
                    spawned.Add(go);
                    var hull = go.AddComponent<IsoFacetHullRenderer>();
                    hull.DeckOccupants.Claim(new object());
                    if (hull.HullId == Overflow) { sharing++; continue; }
                    held.Add(hull.HullId);
                    if (hull.ForeHullId != 0)
                        for (int i = hull.ForeHullId; i <= hull.ForeHullIdTop; i++) held.Add(i);
                }
                Assert.AreEqual(2, sharing, "the harness never exhausted the space");
                foreach (int id in held)
                    Assert.IsFalse(pool.IsPlayerReserveId(id), $"a hull holds {id}, a player's reserved id");

                LogAssert.Expect(LogType.Warning, FigureExhausted);
                Assert.AreEqual(0, IsoFacetHullRegistry.RegisterFigure(), "a cast figure was granted at exhaustion");
                player = IsoFacetHullRegistry.RegisterPlayerFigure();
                Assert.IsTrue(pool.IsPlayerReserveId(player),
                    $"the player's figure got {player} at exhaustion, not a reserved id");
            }
            finally
            {
                if (player != 0) IsoFacetHullRegistry.UnregisterFigure(player);
                foreach (GameObject go in spawned) if (go != null) Object.DestroyImmediate(go);
                IsoFacetHullRegistry.SwapIdPoolForTests(live);
            }
        }

        [Test]
        public void ThePlayerReserveIsReadFromGameConfig_AndClampedToItsRange()
        {
            GameConfig saved = GameServices.Config;
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                GameServices.Config = null;
                Assert.AreEqual(GameConfig.DefaultPlayerFigureIdReserve, GameServices.PlayerFigureIdReserve,
                    "with no config the reserve must fall back to the default");

                GameServices.Config = config;
                Assert.AreEqual(GameConfig.DefaultPlayerFigureIdReserve, GameServices.PlayerFigureIdReserve,
                    "a fresh GameConfig does not ship the default reserve");
                config.PlayerFigureIdReserve = 3;
                Assert.AreEqual(3, GameServices.PlayerFigureIdReserve, "the owner's tuning was not read");
                config.PlayerFigureIdReserve = 99;
                Assert.AreEqual(GameConfig.MaxPlayerFigureIdReserve, GameServices.PlayerFigureIdReserve,
                    "a hand-edited reserve was not clamped — it could starve the fleet");
                config.PlayerFigureIdReserve = -4;
                Assert.AreEqual(0, GameServices.PlayerFigureIdReserve);
            }
            finally
            {
                GameServices.Config = saved;
                Object.DestroyImmediate(config);
            }
        }

        private static int MaxOf(HashSet<int> ids)
        {
            int max = 0;
            foreach (int id in ids) if (id > max) max = id;
            return max;
        }

        /// <summary>
        /// <b>Condition (c), at the source.</b> The gate this PR widens has exactly one reader, and it
        /// must be the one that decides whether the facet block is recorded. If <c>AddRenderPasses</c>
        /// grew a second read of <c>Count</c>, or stopped reading <c>FacetSubjectsLive</c>, a figure could
        /// hold an id and not be drawn, or a scene with nothing in it could start paying for the block.
        /// </summary>
        [Test]
        public void TheRenderFeatureGatesTheFacetBlockOnFacetSubjectsLive_AndNothingElse()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Code/Art/IsoFacetHullFeature.cs");
            Assert.IsTrue(File.Exists(path), "IsoFacetHullFeature.cs moved — the guard cannot pass by absence.");
            string src = File.ReadAllText(path);

            Assert.AreEqual(1, Regex.Matches(src, @"bool hulls = FacetSubjectsLive;").Count,
                "AddRenderPasses no longer gates the facet block on FacetSubjectsLive");
            Assert.AreEqual(1, Regex.Matches(src, @"IsoFacetHullRegistry\.Count\b").Count,
                "IsoFacetHullRegistry.Count is read somewhere other than FacetSubjectsLive");
            Assert.AreEqual(1, Regex.Matches(src, @"IsoFacetHullRegistry\.FigureCount\b").Count,
                "IsoFacetHullRegistry.FigureCount is read somewhere other than FacetSubjectsLive");
            StringAssert.Contains(
                "IsoFacetHullRegistry.Count > 0 || IsoFacetHullRegistry.FigureCount > 0", src,
                "FacetSubjectsLive is no longer 'a hull OR a figure'");
        }
    }
}
