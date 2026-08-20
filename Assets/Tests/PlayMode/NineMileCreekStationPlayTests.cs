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
    /// <b>WALK UP TO A REALLY-PLACED PUMP AND BE OFFERED A FILL.</b> EditMode owns the geometry — where
    /// the pumps stand, whether anything blocks the standing spot, whether the prices are the right way
    /// round. What only PlayMode can show is that the placement and the interact seam agree: that a
    /// fisher standing on the spot the whole-scene reach pass verified, holding a can, is offered
    /// <i>that</i> hose and not the one four metres along, and that the press actually pours.
    ///
    /// <para><b>The pumps here are the SHIPPED ones.</b> The fixture runs
    /// <see cref="NineMileCreekStation.Place"/> against the region's own terrain rather than standing a
    /// hand-made pump somewhere convenient — so a siting change moves this test with it, and a pump that
    /// only works at the origin cannot pass.</para>
    ///
    /// <para><b>⚠️ No virtual key presses.</b> A synthesised keystroke is undeliverable in this harness.
    /// The offer is read straight off <see cref="InteractResolver"/>, which is the same total order the
    /// runtime driver uses, and the fill goes through <c>FuelPump.TryFill</c> — the seam the press
    /// reaches, tested without pretending to press anything.</para>
    ///
    /// <para><b>Headless-safe by construction</b> (⚠️ do not relax): nothing renders, reads pixels or
    /// calls <c>Camera.Render</c>. Every assertion is on state.</para>
    /// </summary>
    public class NineMileCreekStationPlayTests
    {
        readonly List<Object> _spawned = new();

        GameObject _stationRoot;
        CarryHands _hands;
        Transform _player;
        Purse _purse;

        /// <summary>A wallet with a known balance. The region's own proxy forwards to the persistent
        /// wallet, which does not exist in a bare play scene — so the pumps are handed one directly
        /// through <c>FuelPump.SetWallet</c>, the seam that exists for exactly this.</summary>
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
            _hands = playerGo.AddComponent<CarryHands>();      // publishes itself as GameServices.Hands
            _player = playerGo.transform;
        }

        [TearDown]
        public void TearDown()
        {
            Interactables.Clear();
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

        /// <summary>A gas can in the fisher's hands, built the way the dev spawner builds one.</summary>
        FuelLevelPresenter HeldCan(string grade)
        {
            var def = ScriptableObject.CreateInstance<FuelContainerDef>();
            def.Id = "fuelstore." + grade + "_jerry_s20";
            def.DisplayName = "20 L can";
            def.Grade = grade;
            def.Carriable = true;
            def.Facings = 8;
            def.FillFractions = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f };
            def.CapacityLitres = 20f;
            _spawned.Add(def);

            var go = Spawn("Can");
            var presenter = go.AddComponent<FuelLevelPresenter>();
            presenter.Container = def;
            presenter.Fill = 0f;                                // empty, so a fill has somewhere to go
            go.AddComponent<YSortSprite>();
            var can = go.AddComponent<CarriableFuelContainer>();

            // Picked up through the real seam rather than re-parented by hand: CarryHands is what
            // publishes GameServices.Hands.Carried, and the pump's whole ToolTarget rung hangs off that.
            go.transform.position = _hands.transform.position;
            CarryRefusal refusal = _hands.TryPickUp(can);
            Assert.That(refusal, Is.EqualTo(CarryRefusal.None), "the fixture could not put the can in her hands");

            // ⚠ The VESSEL is the presenter, not the carriable: FuelLevelPresenter implements
            // IFuelVessel because the fill FRACTION it already stored IS the fuel, and a second litre
            // count beside it would be two numbers for one quantity.
            return presenter;
        }

        static InteractActor OnFootAt(Vector2 p) =>
            new InteractActor(p, Vector2.zero, InteractContext.OnFoot);

        // =============================================================================================

        [UnityTest]
        public IEnumerator StandingAtTheWharfGasHose_HoldingACan_ThatHoseIsWhatIsOffered()
        {
            if (_stationRoot == null) Assert.Ignore("the gas-station kit is not installed here");

            FuelPump gas = PumpFor(NineMileCreekStation.WharfStationId, FuelGrades.Gas);
            FuelPump diesel = PumpFor(NineMileCreekStation.WharfStationId, FuelGrades.Diesel);
            Assert.That(gas, Is.Not.Null, "no gas hose was placed on the wharf");
            Assert.That(diesel, Is.Not.Null, "no diesel hose was placed on the wharf");

            HeldCan(FuelGrades.Gas);
            _player.position = gas.WorldPosition;
            yield return null;

            Assert.That(InteractResolver.TryResolveNow(OnFootAt(gas.WorldPosition),
                                                       InteractResolver.DefaultFacingArcDegrees,
                                                       out IInteractable best), Is.True,
                "nothing was offered at the standing spot the whole-scene pass verified — which is the " +
                "one thing that spot is FOR");

            Assert.That(best, Is.SameAs(gas),
                $"the offer was '{best?.Id}'. ⚠ A carried can reports Held(20) and would outrank a plain " +
                "Fixture(0), so this also proves the pump climbs to ToolTarget while a fuel vessel is in " +
                "hand — without that, filling a can you are holding is impossible by construction.");

            Assert.That(best.VerbLabel, Does.Contain("gas"),
                "the label is the only thing telling the player which grade this side sells, because a " +
                "dispenser side carries exactly one hose");

            // …and the diesel hose four metres along is NOT what you are offered.
            Assert.That(Vector2.Distance(gas.WorldPosition, diesel.WorldPosition),
                        Is.GreaterThan(diesel.ReachMeters),
                "the two hoses must be further apart than a pump reaches, or standing at one offers the " +
                "other and the grade you get is whichever id sorts lower");
        }

        [UnityTest]
        public IEnumerator ThePress_FillsTheCanAndChargesTheWharfsOwnPostedPrice()
        {
            if (_stationRoot == null) Assert.Ignore("the gas-station kit is not installed here");

            FuelPump gas = PumpFor(NineMileCreekStation.WharfStationId, FuelGrades.Gas);
            Assert.That(gas, Is.Not.Null);

            FuelLevelPresenter can = HeldCan(FuelGrades.Gas);
            _player.position = gas.WorldPosition;
            yield return null;

            int before = _purse.Money;
            Assert.That(gas.TryFill(), Is.True, "the press was refused at a hose wired for the grade in hand");
            Assert.That(gas.LastLitresDelivered, Is.GreaterThan(0f));
            Assert.That(can.Litres, Is.GreaterThan(0f), "the can is what actually holds it");

            int spent = before - _purse.Money;
            float posted = gas.Station.PricePerLitre(FuelGrades.Gas);
            Assert.That(spent, Is.GreaterThan(0), "fuel is not free on this wharf");
            Assert.That(spent, Is.EqualTo(Mathf.CeilToInt(gas.LastLitresDelivered * posted)).Within(1),
                "the charge must be the litres delivered at the site's own posted price — the whole " +
                "reason the price lives on the SITE and not on the hose");
        }

        [UnityTest]
        public IEnumerator EveryHoseAtBothSites_IsReachableFromItsOwnStandingSpot()
        {
            if (_stationRoot == null) Assert.Ignore("the gas-station kit is not installed here");

            HeldCan(FuelGrades.Gas);
            yield return null;

            foreach (FuelPump pump in Pumps())
            {
                _player.position = pump.WorldPosition;
                yield return null;

                bool got = InteractResolver.TryResolveNow(OnFootAt(pump.WorldPosition),
                                                          InteractResolver.DefaultFacingArcDegrees,
                                                          out IInteractable best);
                Assert.That(got, Is.True, $"'{pump.Id}' offers nothing from its own spot");
                Assert.That(best, Is.InstanceOf<FuelPump>(),
                    $"standing at '{pump.Id}' the offer was '{best?.Id}', which is not a hose at all");
            }
        }
    }
}
