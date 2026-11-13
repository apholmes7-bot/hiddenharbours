using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE OWNER'S SYMPTOM AT THE REGISTRY: A FULL HARBOUR MUST STILL LEAVE HER FIGURE AN ID</b>
    /// (ADR 0056).
    ///
    /// <para>The owner saw the player's old sprite at St Peters and in the co-op. Her ashore figure
    /// asks the registry for one facet id; when the fleet has spent the 8-bit space she is refused and
    /// the presenter falls back to her sprite until her next arrival. Two harbours are built here from
    /// real hulls, and her figure's way in is asked for an id after each:</para>
    /// <list type="number">
    /// <item>thirty boats with nobody aboard (on main each took thirteen ids, so the space ran out at
    /// the nineteenth);</item>
    /// <item>a fleet with a skipper on every deck, grown until two hulls share the overflow id.</item>
    /// </list>
    ///
    /// <para>The pool is built the way the game builds it, with the player-figure reserve read from
    /// <c>GameServices</c>, and her way in is <c>RegisterPlayerFigure</c>. Both are found by
    /// reflection so this file compiles on main, where neither exists: there the pool has no reserve,
    /// her way in is <c>RegisterFigure</c>, and both cases FAIL by behaviour. No GPU.</para>
    /// </summary>
    public sealed class FacetIdHerFigureTests
    {
        private const int Overflow = 255;
        /// <summary>A guard on the fleet loop: far past any harbour that could exhaust 255 ids.</summary>
        private const int MaxHulls = 300;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<int> _figureIds = new List<int>();
        private IsoFacetIdPool _livePool;

        [SetUp]
        public void SetUp()
        {
            _livePool = IsoFacetHullRegistry.SwapIdPoolForTests(PoolAsTheGameBuildsIt());
        }

        [TearDown]
        public void TearDown()
        {
            // Every id goes back to the TEST pool before the live pool is restored.
            foreach (int id in _figureIds) IsoFacetHullRegistry.UnregisterFigure(id);
            _figureIds.Clear();
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            if (_livePool != null) IsoFacetHullRegistry.SwapIdPoolForTests(_livePool);
            _livePool = null;
        }

        [Test]
        public void ThirtyBoatsWithNobodyAboard_LeaveHerFigureAnId()
        {
            const int Boats = 30;
            for (int i = 0; i < Boats; i++) NewHull("EmptyDeck" + i);

            int id = HerFigureId();
            Assert.Greater(id, 0,
                $"{Boats} moored boats with nobody aboard left the player's ashore figure NO facet id — she " +
                "falls back to her old sprite. A fore block is read only while a deck slot is claimed, so an " +
                "empty deck must hold one id, not thirteen.");
        }

        [Test]
        public void AFleetWithASkipperOnEveryDeck_ThatExhaustsTheSpace_StillLeavesHerFigureAnId()
        {
            int hulls = 0, onOverflow = 0;
            while (onOverflow < 2 && hulls < MaxHulls)
            {
                IsoFacetHullRenderer hull = NewHull("Skippered" + hulls);
                hull.DeckOccupants.Claim(new object());
                if (hull.HullId == Overflow) onOverflow++;
                hulls++;
            }
            Assert.AreEqual(2, onOverflow, $"harness: {hulls} hulls did not exhaust the id space");

            int id = HerFigureId();
            Assert.Greater(id, 0,
                $"a fleet of {hulls} hulls with a skipper aboard each spent every id, and the player's " +
                "ashore figure was refused — she falls back to her old sprite. The player's ids are a " +
                "reserve the fleet can never take.");
            Assert.AreNotEqual(Overflow, id, "a figure never shares the overflow id");
        }

        private IsoFacetHullRenderer NewHull(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go.AddComponent<IsoFacetHullRenderer>();
        }

        /// <summary>Her figure's way into the registry: the player's entry point where it exists,
        /// else the one figure entry point main had.</summary>
        private int HerFigureId()
        {
            MethodInfo player = typeof(IsoFacetHullRegistry).GetMethod("RegisterPlayerFigure",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, null, System.Type.EmptyTypes, null);
            int id = player != null ? (int)player.Invoke(null, null) : IsoFacetHullRegistry.RegisterFigure();
            if (id > 0) _figureIds.Add(id);
            return id;
        }

        /// <summary>The pool as the registry builds it in the game: with the player-figure reserve
        /// from <c>GameServices</c> where that exists, else main's plain pool.</summary>
        private static IsoFacetIdPool PoolAsTheGameBuildsIt()
        {
            PropertyInfo reserve = typeof(GameServices).GetProperty("PlayerFigureIdReserve",
                BindingFlags.Static | BindingFlags.Public);
            ConstructorInfo withReserve = typeof(IsoFacetIdPool).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(int) }, null);
            if (reserve == null || withReserve == null) return new IsoFacetIdPool();
            return (IsoFacetIdPool)withReserve.Invoke(new object[] { (int)reserve.GetValue(null) });
        }
    }
}
