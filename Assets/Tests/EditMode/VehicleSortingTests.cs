using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// A mesh vehicle sorts by where she stands (ADR 0032), not at the SortingGroup's birth order of 0.
    ///
    /// <para>At 0 every machine sat under the whole decor band, so a player standing north of a parked trailer drew
    /// over her roof. These hold the rule <see cref="VehicleMeshDriver"/> now follows: her order is the band's own
    /// Y-sort from her root, one rung behind it, so the player sorts behind her when he is north of her, in front
    /// when he is south, and a rider stood on her root always draws over her.</para>
    /// </summary>
    public class VehicleSortingTests
    {
        sealed class RecordingRenderer : IHullMeshRenderer
        {
            public readonly List<int> Orders = new List<int>();
            public float HeadingDirUnits { get; set; }
            public float RollDegrees { get; set; }
            public float PitchDegrees { get; set; }
            public float HeavePixels { get; set; }
            public float RidePixels { get; set; }
            public bool IsConfigured => true;
            public void SetSorting(int sortingLayerId, int sortingOrder) => Orders.Add(sortingOrder);
            public void SetDeckOccupant(Vector3 rigLocalMeters, bool active) { }
            public float DeckOccluderId => 0f;
            public IDeckOccupantSlots DeckOccupants => NoDeckOccupantSlots.Instance;
        }

        readonly List<Object> _spawned = new List<Object>();

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        /// <summary>The order a person's sprite takes at <paramref name="y"/>: the player's own dynamic YSortSprite.</summary>
        static int PersonAt(float y) => YSortSprite.OrderFor(y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
            SortingBands.DecorFloor, SortingBands.DecorCeiling);

        (VehicleMeshDriver driver, RecordingRenderer renderer) ParkedMachine(Vector2 at)
        {
            var def = ScriptableObject.CreateInstance<VehicleMeshDef>();
            def.TireWidthMeters = 0.3f;
            _spawned.Add(def);
            var root = new GameObject("machine");
            _spawned.Add(root);
            root.transform.position = at;
            var visual = new GameObject("VehicleVisual");
            visual.transform.SetParent(root.transform, false);
            var renderer = new RecordingRenderer();
            var driver = root.AddComponent<VehicleMeshDriver>();
            driver.Configure(visual.transform, renderer, def, null, null, null);
            return (driver, renderer);
        }

        [TestCase(-120f)]
        [TestCase(0f)]
        [TestCase(37.3f)]
        [TestCase(133f)]
        public void HerOrder_IsTheDecorBandsYSort_OneRungBehindHerRoot(float y)
        {
            Assert.That(VehicleMeshDriver.OrderAt(y), Is.EqualTo(PersonAt(y) - 1));
            Assert.That(VehicleMeshDriver.OrderAt(y), Is.InRange(SortingBands.DecorFloor, SortingBands.DecorCeiling),
                "she sorts inside the decor band, among the things people walk round");
        }

        [Test]
        public void AParkedMachine_SortsInTheBand_AndWritesOnce()
        {
            (VehicleMeshDriver driver, RecordingRenderer renderer) = ParkedMachine(new Vector2(56f, 133f));
            driver.Drive();
            driver.Drive();
            driver.Drive();
            Assert.That(renderer.Orders, Is.EqualTo(new[] { VehicleMeshDriver.OrderAt(133f) }),
                "one write for a machine that does not move, and never the group's birth order of 0");
            Assert.That(driver.SortingOrder, Is.Not.EqualTo(0));
        }

        [Test]
        public void ThePlayer_NorthOfHerIsBehindHerRoof_SouthOfHerIsInFront_AndHerRiderIsOnTop()
        {
            const float y = 133f;
            (VehicleMeshDriver driver, _) = ParkedMachine(new Vector2(56f, y));
            driver.Drive();
            int machine = driver.SortingOrder;
            Assert.That(PersonAt(y + 1.3f), Is.LessThan(machine), "a player behind her (north) must draw under her roof");
            Assert.That(PersonAt(y - 1.3f), Is.GreaterThan(machine), "a player in front of her (south) draws over her");
            Assert.That(PersonAt(y), Is.GreaterThan(machine), "the rider stood on her root draws over her, never a tie");
        }

        [Test]
        public void AMovingMachine_ReSortsAsSheCrossesTheYard()
        {
            (VehicleMeshDriver driver, RecordingRenderer renderer) = ParkedMachine(new Vector2(0f, 10f));
            driver.Drive();
            driver.transform.position = new Vector3(0f, 20f, 0f);
            driver.Drive();
            Assert.That(renderer.Orders, Is.EqualTo(new[] { VehicleMeshDriver.OrderAt(10f), VehicleMeshDriver.OrderAt(20f) }));
            Assert.That(renderer.Orders[1], Is.LessThan(renderer.Orders[0]), "further up the screen sorts further back");
        }
    }
}
