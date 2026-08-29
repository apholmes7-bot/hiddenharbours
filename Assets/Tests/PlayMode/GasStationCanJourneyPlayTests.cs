using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE OWNER'S OWN TEST, AS A TEST.</b> <i>"the gas station needs an interior and you should have
    /// a few emtpy fuel cansiters to test filling."</i> — 2026-08-28. So: walk up to the station, find an
    /// empty can standing there, pick it up, carry it to a hose, press, and watch the level and the money
    /// both move. Then walk into the shop.
    ///
    /// <para><b>Sibling of <c>NineMileCreekStationPlayTests</c>, and deliberately not a copy.</b> That
    /// one proves a pump offers itself to somebody standing on its audited spot. This one proves the
    /// LOOP closes — that the thing you fill is findable in the world without a dev menu, which is the
    /// half that did not exist. Every object here is the SHIPPED one: the fixture runs
    /// <see cref="NineMileCreekStation.Place"/> against the region's own terrain, so a can that only
    /// works when a test stands it at the origin cannot pass.</para>
    ///
    /// <para><b>⚠️ No virtual key presses</b> (they are undeliverable in this harness). The offer is read
    /// off <see cref="InteractResolver"/> — the same total order the runtime driver uses — and the verbs
    /// go through the seams a press reaches.</para>
    ///
    /// <para><b>Headless-safe by construction</b> (⚠️ do not relax): nothing renders, reads pixels or
    /// calls <c>Camera.Render</c>. The interior assertions are on <c>SpriteRenderer.enabled</c> and on
    /// <see cref="BuildingInterior.IsInside"/>, which are state.</para>
    /// </summary>
    public class GasStationCanJourneyPlayTests
    {
        readonly List<Object> _spawned = new();

        GameObject _stationRoot;
        CarryHands _hands;
        Transform _player;
        Purse _purse;

        sealed class Purse : IWallet
        {
            public int Money { get; private set; } = 500;
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();

            var terrainGo = Spawn("TidalTerrain");
            var terrain = terrainGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekBuilder.ConfigureNineMileCreekTerrain(terrain);

            NineMileCreekStation.Place(terrain);
            _stationRoot = GameObject.Find(NineMileCreekStation.RootName);
            if (_stationRoot != null) _spawned.Add(_stationRoot);

            _purse = new Purse();
            foreach (FuelPump p in Pumps()) p.SetWallet(_purse);

            var playerGo = Spawn("Player");
            playerGo.AddComponent<SpriteRenderer>();
            playerGo.AddComponent<IsoCharacterSprite>();
            _hands = playerGo.AddComponent<CarryHands>();      // publishes GameServices.Hands
            _player = playerGo.transform;

            // The relay a room reads to find its occupant when no builder wired one — which is every
            // region scene, because Unity will not serialize a reference across scenes.
            GameServices.PlayerTransform = _player;
        }

        [TearDown]
        public void TearDown()
        {
            Interactables.Clear();
            GameServices.PlayerTransform = null;
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        IEnumerable<FuelPump> Pumps() =>
            _stationRoot == null
                ? Enumerable.Empty<FuelPump>()
                : _stationRoot.GetComponentsInChildren<FuelPump>(includeInactive: true);

        FuelPump PumpFor(string stationId, string grade) =>
            Pumps().FirstOrDefault(p => p.Station != null && p.Station.Id == stationId && p.Grade == grade);

        IEnumerable<CarriableFuelContainer> PlacedCans() =>
            _stationRoot == null
                ? Enumerable.Empty<CarriableFuelContainer>()
                : _stationRoot.GetComponentsInChildren<CarriableFuelContainer>(includeInactive: true);

        CarriableFuelContainer CanOfGrade(string grade) =>
            PlacedCans().FirstOrDefault(c => c.Container != null && c.Container.Grade == grade);

        static InteractActor OnFootAt(Vector2 p) =>
            new InteractActor(p, Vector2.zero, InteractContext.OnFoot);

        void RequireStation()
        {
            if (_stationRoot == null) Assert.Ignore("the gas-station kit is not installed here");
            if (!PlacedCans().Any()) Assert.Ignore("the fuel-container kit is not baked here");
        }

        // =============================================================================================
        //  THE JOURNEY
        // =============================================================================================

        [UnityTest]
        public IEnumerator ThereAreEmptyCansStandingAtTheStation()
        {
            RequireStation();
            yield return null;

            var cans = PlacedCans().ToList();
            Assert.That(cans.Count, Is.GreaterThan(0), "no can was placed at the station at all");

            foreach (CarriableFuelContainer can in cans)
            {
                var vessel = can.GetComponent<FuelLevelPresenter>();
                Assert.That(vessel, Is.Not.Null, $"'{can.Id}' holds no fuel — it is not a vessel");
                Assert.That(vessel.Litres, Is.EqualTo(0f).Within(1e-3f),
                    $"'{can.Id}' was placed with {vessel.Litres:0.#} L in it. The owner asked for EMPTY " +
                    "cans: a can that starts part-full cannot show the fill working.");
                Assert.That(can.IsCarriable, Is.True, $"'{can.Id}' cannot be picked up");
            }

            // ⚠️ Ids must be distinct or the resolver's order stops being total and one can becomes
            // unreachable forever — it is the LAST tie-break, so two cans sharing one is not cosmetic.
            var ids = cans.Select(c => c.Id).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count),
                "two placed cans share an interact id: " + string.Join(", ", ids));
        }

        [UnityTest]
        public IEnumerator WalkUpToACan_AndTheCanIsWhatIsOffered()
        {
            RequireStation();

            CarriableFuelContainer can = CanOfGrade(FuelGrades.Gas);
            Assert.That(can, Is.Not.Null, "no gas can was placed");

            _player.position = can.WorldPosition;
            yield return null;

            Assert.That(InteractResolver.TryResolveNow(OnFootAt(can.WorldPosition),
                                                       InteractResolver.DefaultFacingArcDegrees,
                                                       out IInteractable best), Is.True,
                "standing over a placed can, nothing is offered");
            Assert.That(best, Is.SameAs(can),
                $"the offer at the can was '{best?.Id}'. A can standing on a forecourt has to win its " +
                "own spot, or the player cannot pick it up.");
        }

        [UnityTest]
        public IEnumerator PickItUp_CarryItToAHose_AndThePressFillsItAndCharges()
        {
            RequireStation();

            CarriableFuelContainer can = CanOfGrade(FuelGrades.Gas);
            FuelPump hose = PumpFor(NineMileCreekStation.RoadStationId, FuelGrades.Gas);
            Assert.That(can, Is.Not.Null, "no gas can was placed");
            Assert.That(hose, Is.Not.Null, "Route 91 has no gas hose");

            var vessel = can.GetComponent<FuelLevelPresenter>();
            float capacity = vessel.CapacityLitres;
            Assert.That(capacity, Is.GreaterThan(0f));

            // --- walk up and lift it, through the real seam
            _player.position = can.WorldPosition;
            yield return null;
            Assert.That(_hands.TryPickUp(can), Is.EqualTo(CarryRefusal.None), "the can would not lift");
            Assert.That(can.IsCarried, Is.True);

            // --- carry it to the hose. ⚠️ This distance is the point of the errand: the cans stand well
            // outside any pump's reach, so the fill cannot happen by standing still.
            float carried = Vector2.Distance(can.WorldPosition, hose.WorldPosition);
            Assert.That(carried, Is.GreaterThan(hose.ReachMeters),
                $"the cans are only {carried:0.##} m from a hose, inside its {hose.ReachMeters} m reach " +
                "— there is no journey here to test");

            _player.position = hose.WorldPosition;
            yield return null;

            Assert.That(InteractResolver.TryResolveNow(OnFootAt(hose.WorldPosition),
                                                       InteractResolver.DefaultFacingArcDegrees,
                                                       out IInteractable best), Is.True);
            Assert.That(best, Is.SameAs(hose),
                $"at the hose holding a can the offer was '{best?.Id}'. A carried can reports Held(20), " +
                "so this also proves the pump climbs to ToolTarget — without that, filling the can in " +
                "your hands is impossible by construction and 'set it down' wins every press.");

            // --- press
            int before = _purse.Money;
            Assert.That(hose.TryFill(), Is.True, "the press was refused at a hose selling the grade in hand");

            Assert.That(vessel.Litres, Is.EqualTo(capacity).Within(0.01f),
                "an empty can filled to the brim is what the pump quotes for — it fills the ROOM");
            Assert.That(vessel.Fill, Is.EqualTo(1f).Within(1e-3f), "and the can must READ full");

            int spent = before - _purse.Money;
            float posted = hose.Station.PricePerLitre(FuelGrades.Gas);
            Assert.That(spent, Is.GreaterThan(0), "fuel is not free at Route 91");
            Assert.That(spent, Is.EqualTo(Mathf.CeilToInt(hose.LastLitresDelivered * posted)).Within(1),
                "the charge must be the litres delivered at the SITE's posted price");

            // --- and set it down again
            Assert.That(_hands.TryPlace(), Is.EqualTo(CarryRefusal.None), "the full can would not set down");
            Assert.That(can.IsCarried, Is.False);
        }

        [UnityTest]
        public IEnumerator AFullCanIsNotChargedTwice()
        {
            RequireStation();

            CarriableFuelContainer can = CanOfGrade(FuelGrades.Gas);
            FuelPump hose = PumpFor(NineMileCreekStation.RoadStationId, FuelGrades.Gas);
            Assert.That(can, Is.Not.Null);
            Assert.That(hose, Is.Not.Null);

            _player.position = can.WorldPosition;
            yield return null;
            Assert.That(_hands.TryPickUp(can), Is.EqualTo(CarryRefusal.None));
            _player.position = hose.WorldPosition;
            yield return null;

            Assert.That(hose.TryFill(), Is.True, "the first fill must go through or this proves nothing");
            int afterFirst = _purse.Money;

            // ⚠️ The refusal is FuelRefusal.VesselFull, and the money is the assertion that matters: a
            // pump that charged for a fill it did not pour would be a silent leak that only shows up as
            // a purse draining while the player stands still.
            Assert.That(hose.TryFill(), Is.False, "a brim-full can was filled again");
            Assert.That(_purse.Money, Is.EqualTo(afterFirst), "the refused second press still took money");
            Assert.That(hose.LastLitresDelivered, Is.EqualTo(0f).Within(1e-4f),
                "a refusal must report no litres");
        }

        [UnityTest]
        public IEnumerator PressingAHoseWithEmptyHands_RefusesAndChargesNothing()
        {
            RequireStation();

            FuelPump hose = PumpFor(NineMileCreekStation.RoadStationId, FuelGrades.Gas);
            Assert.That(hose, Is.Not.Null);

            _player.position = hose.WorldPosition;
            yield return null;

            // The negative arm, and it is the component's OWN documented behaviour rather than an
            // invented one: TargetVessel() falls back from your hands to the boat you are aboard, and
            // ashore with empty hands there is neither — FuelPricing.Quote answers NoVessel.
            int before = _purse.Money;
            Assert.That(hose.TryFill(), Is.False, "a hose poured with nothing to pour into");
            Assert.That(_purse.Money, Is.EqualTo(before), "an empty-handed press took money");
            Assert.That(hose.LastFillSucceeded, Is.False);
        }

        [UnityTest]
        public IEnumerator ADieselCanAtTheGasHose_IsToldSoRatherThanIgnored()
        {
            RequireStation();

            CarriableFuelContainer diesel = CanOfGrade(FuelGrades.Diesel);
            FuelPump gas = PumpFor(NineMileCreekStation.RoadStationId, FuelGrades.Gas);
            if (diesel == null) Assert.Ignore("no diesel can is placed at this site");
            Assert.That(gas, Is.Not.Null);

            _player.position = diesel.WorldPosition;
            yield return null;
            Assert.That(_hands.TryPickUp(diesel), Is.EqualTo(CarryRefusal.None));
            _player.position = gas.WorldPosition;
            yield return null;

            // ⭐ The pump claims the press for ANY fuel vessel precisely so the useful outcome — being
            // TOLD — requires winning it. Until a diesel can stood in the world, nothing could try this.
            Assert.That(InteractResolver.TryResolveNow(OnFootAt(gas.WorldPosition),
                                                       InteractResolver.DefaultFacingArcDegrees,
                                                       out IInteractable best), Is.True);
            Assert.That(best, Is.SameAs(gas),
                "holding the wrong can, the hose must still win the press — a refusal you cannot reach " +
                "is not a refusal");

            int before = _purse.Money;
            Assert.That(gas.TryFill(), Is.False, "gas was poured into a diesel can");
            Assert.That(_purse.Money, Is.EqualTo(before));
        }

        // =============================================================================================
        //  AND THE SHOP OPENS
        // =============================================================================================

        BuildingInterior TheCStore() =>
            _stationRoot == null ? null
                : _stationRoot.GetComponentsInChildren<BuildingInterior>(includeInactive: true)
                              .FirstOrDefault();

        [UnityTest]
        public IEnumerator TheCStoreIsNoLongerASolidBlock()
        {
            RequireStation();
            yield return null;

            BuildingInterior store = TheCStore();
            Assert.That(store, Is.Not.Null,
                "the C-store's sales floor has been placed and drawn since #626 and was never " +
                "reachable — a placed building with no way in is the facade the owner's 2026-08-11 " +
                "ruling forbids");

            // The solid that used to fill the whole plan is off, and a ring of walls stands instead.
            Transform solid = store.transform.Find("blocker_building");
            Assert.That(solid == null || !solid.gameObject.activeSelf, Is.True,
                "the building blocker is still on, so the walls are doubled and the doorway is filled");

            Transform walls = store.transform.Find(StationInteriorPlacement.WallsChildName);
            Assert.That(walls, Is.Not.Null, "no wall ring was built to replace the solid");
            Assert.That(walls.GetComponents<PolygonCollider2D>().Length, Is.GreaterThanOrEqualTo(4),
                "a building wants four walls and a doorway cut in one of them");

            Transform shutLeaf = store.transform.Find("door_Entry_shut");
            Assert.That(shutLeaf == null || !shutLeaf.gameObject.activeSelf, Is.True,
                "the entry's shut leaf still plugs the doorway the walls left open");
        }

        [UnityTest]
        public IEnumerator WalkInThroughTheDoor_AndTheShellYieldsToTheSalesFloor()
        {
            RequireStation();
            yield return null;

            BuildingInterior store = TheCStore();
            Assert.That(store, Is.Not.Null);

            // Outside, on the forecourt: the shell is what you see.
            _player.position = store.DoorWorld + (store.DoorWorld - (Vector2)store.transform.position).normalized * 3f;
            yield return null;
            Assert.That(store.IsInside, Is.False, "the fisher is outside and the room thinks she is in");

            // Now stand in the middle of the sales floor.
            _player.position = store.transform.position;
            yield return null;
            yield return null;

            Assert.That(store.IsInside, Is.True,
                "standing on the middle of the sales floor the room does not think she is inside — the " +
                "footprint and the placement disagree, and both draw perfectly");

            var shell = store.GetComponent<SpriteRenderer>();
            Assert.That(shell.enabled, Is.False, "the shell is still drawn over the room she is standing in");
        }
    }
}
