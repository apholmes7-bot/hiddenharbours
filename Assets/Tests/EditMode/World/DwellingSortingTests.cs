using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// The Core sort rule for parked dwellings and their occupant (ADR 0060 section 5, PR 4): the dwelling
    /// sorts by the hull rule at its ground pivot, he draws one rung in front of it while inside, and decor
    /// south of the dwelling still draws over him while decor north of it still draws under him.
    /// </summary>
    public class DwellingSortingTests
    {
        [SetUp] public void SetUp() => OccupantSorting.Reset();
        [TearDown] public void TearDown() => OccupantSorting.Reset();

        static int DecorAt(float y) =>
            Mathf.Clamp(Mathf.RoundToInt(SortingBands.DecorBase - y * SortingBands.OrdersPerMetre),
                        SortingBands.DecorFloor, SortingBands.DecorCeiling);

        [TestCase(0f)]
        [TestCase(37.5f)]
        [TestCase(-120.25f)]
        public void TheDwelling_SortsByTheHullRuleAtItsGroundPivot(float y)
        {
            Assert.That(DwellingSorting.OrderAt(new Vector2(99f, y)), Is.EqualTo(HullSorting.OrderAt(y)));
            Assert.That(DwellingSorting.OrderAt(new Vector2(-5f, y)), Is.EqualTo(DwellingSorting.OrderAt(new Vector2(5f, y))),
                "only the pivot's Y moves it");
        }

        [Test]
        public void TheOccupant_IsOneRungInFront_AndBetweenTheDecorNorthAndSouthOfTheDwelling()
        {
            const float pivotY = 10f;
            int dwelling = DwellingSorting.OrderAt(new Vector2(0f, pivotY));
            int him = DwellingSorting.OccupantOrder(dwelling);

            Assert.That(him, Is.EqualTo(dwelling + 1));
            Assert.That(him, Is.GreaterThan(dwelling), "he draws over the dwelling he stands in");
            Assert.That(DecorAt(pivotY - 0.5f), Is.GreaterThan(him), "a fence post south of the dwelling draws over him");
            Assert.That(DecorAt(pivotY + 0.5f), Is.LessThan(him), "a tree north of the dwelling draws under him");
        }

        [Test]
        public void TheOccupantOrder_StaysInsideTheDecorBand()
        {
            Assert.That(DwellingSorting.OccupantOrder(SortingBands.DecorCeiling), Is.EqualTo(SortingBands.DecorCeiling));
            Assert.That(DwellingSorting.OccupantOrder(SortingBands.DecorFloor - 10), Is.EqualTo(SortingBands.DecorFloor));
        }

        [Test]
        public void TheInput_AnswersOnlyForTheBodyInside()
        {
            var him = new GameObject("him").transform;
            var someoneElse = new GameObject("villager").transform;
            try
            {
                var camper = new object();
                OccupantSorting.Enter(camper, him, 500);
                Assert.That(OccupantSorting.TryOrderFor(him, out int order), Is.True);
                Assert.That(order, Is.EqualTo(501));
                Assert.That(OccupantSorting.TryOrderFor(someoneElse, out _), Is.False, "a villager keeps his own Y-sort");
                Assert.That(OccupantSorting.TryOrderFor(null, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(him.gameObject);
                Object.DestroyImmediate(someoneElse.gameObject);
            }
        }

        [Test]
        public void ALateLeaveFromTheDwellingHeLeft_DoesNotUndoTheOneHeEntered()
        {
            var him = new GameObject("him").transform;
            try
            {
                object a = new object(), b = new object();
                OccupantSorting.Enter(a, him, 100);
                OccupantSorting.Enter(b, him, 200);
                OccupantSorting.Leave(a);
                Assert.That(OccupantSorting.TryOrderFor(him, out int order), Is.True);
                Assert.That(order, Is.EqualTo(201));
                OccupantSorting.Leave(b);
                Assert.That(OccupantSorting.TryOrderFor(him, out _), Is.False);
            }
            finally { Object.DestroyImmediate(him.gameObject); }
        }

        [Test]
        public void ADestroyedOccupant_IsNoLongerSorted()
        {
            var him = new GameObject("him").transform;
            OccupantSorting.Enter(new object(), him, 100);
            Object.DestroyImmediate(him.gameObject);
            Assert.That(OccupantSorting.IsOccupied, Is.False);
            Assert.That(OccupantSorting.TryOrderFor(him, out _), Is.False);
        }
    }
}
