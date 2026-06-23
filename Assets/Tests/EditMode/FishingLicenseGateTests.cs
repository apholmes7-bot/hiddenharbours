using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters ROD LICENCE GATE: landing cod on the rod requires the cod licence. The FishingController
    /// routes its land-time decision through <see cref="CatchLicensePolicy.MayLand"/> + the Core
    /// <see cref="ILicenseService"/>. Covers: unlicensed cod is released cozily (nothing added, no
    /// FishCaught); the same cod lands once the cod licence is held; an ungated species (a clam) is never
    /// gated. Drives the full FSM via Tick — no scene.
    /// </summary>
    public class FishingLicenseGateTests
    {
        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 20;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FakeLicenses : ILicenseService
        {
            private readonly HashSet<string> _held = new();
            public void GrantHeld(string id) => _held.Add(id);
            public bool IsLicensed(string licenseId) => string.IsNullOrEmpty(licenseId) || _held.Contains(licenseId);
            public void Grant(string licenseId) { if (!string.IsNullOrEmpty(licenseId)) _held.Add(licenseId); }
            public int Count => _held.Count;
        }

        private readonly List<Object> _spawned = new();
        private int _caught;
        private void OnCaught(FishCaught e) => _caught++;

        [SetUp]
        public void SetUp()
        {
            _caught = 0;
            EventBus.Clear<FishCaught>();
            EventBus.Clear<FishingStateChanged>();
            EventBus.Subscribe<FishCaught>(OnCaught);
            GameServices.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<FishCaught>(OnCaught);
            EventBus.Clear<FishCaught>();
            EventBus.Clear<FishingStateChanged>();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private FishSpeciesDef MakeFish(string id, FishCategory cat)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id; f.DisplayName = id; f.Category = cat;
            f.RegionIds = new[] { "region.coddle_cove" };
            f.AllowedGear = Gear.Handline | Gear.Longline;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 1f; f.MaxWeightKg = 6f; f.BaseValue = 12; f.SupplyElasticity = 0.2f; f.SpawnWeight = 1f;
            _spawned.Add(f);
            return f;
        }

        private LicenseDef MakeCodLicense()
        {
            var lic = ScriptableObject.CreateInstance<LicenseDef>();
            lic.Id = "license.cod"; lic.DisplayName = "Cod"; lic.PermittedSpeciesIds = new[] { "fish.atlantic_cod" };
            _spawned.Add(lic);
            return lic;
        }

        private FishingController MakeController(IHold hold, FishSpeciesDef[] pool, LicenseDef[] licenses, int seed)
        {
            var go = new GameObject("Fisher");
            _spawned.Add(go);
            var c = go.AddComponent<FishingController>();
            c.Configure(hold, pool, "region.coddle_cove", Gear.Handline, seed, licenses);
            return c;
        }

        // Cast → bite → fight, then forgiving-pulse until the result resolves.
        private static void RunToResult(FishingController c)
        {
            c.Tick(0.05f, true);   // cast
            float t = 0f;
            while (c.Phase != FishingPhase.Fighting && c.Phase != FishingPhase.Tending && t < 30f)
            { c.Tick(0.05f, false); t += 0.05f; }
            t = 0f;
            while (!IsResult(c.Phase) && t < 120f)
            { c.Tick(0.05f, actionHeld: c.State.Tension01 < 0.5f); t += 0.05f; }
        }

        private static bool IsResult(FishingPhase p)
            => p == FishingPhase.Landed || p == FishingPhase.Snapped || p == FishingPhase.NoBite || p == FishingPhase.Idle;

        [Test]
        public void Cod_WithoutLicense_IsReleased_NothingLanded()
        {
            var hold = new FakeHold();
            var cod = MakeFish("fish.atlantic_cod", FishCategory.InshoreGroundfish);
            var licenses = new[] { MakeCodLicense() };
            GameServices.Licenses = new FakeLicenses();   // holds NO cod licence

            var c = MakeController(hold, new[] { cod }, licenses, seed: 999);
            RunToResult(c);

            Assert.AreEqual(FishingPhase.Snapped, c.Phase, "unlicensed cod slips back (cozy released result)");
            Assert.AreEqual(0, hold.UsedUnits, "nothing is stowed without the licence");
            Assert.AreEqual(0, _caught, "no FishCaught when the catch is released for want of a licence");
        }

        [Test]
        public void Cod_WithLicense_Lands()
        {
            var hold = new FakeHold();
            var cod = MakeFish("fish.atlantic_cod", FishCategory.InshoreGroundfish);
            var licenses = new[] { MakeCodLicense() };
            var held = new FakeLicenses();
            held.GrantHeld("license.cod");
            GameServices.Licenses = held;

            var c = MakeController(hold, new[] { cod }, licenses, seed: 999);
            RunToResult(c);

            Assert.AreEqual(FishingPhase.Landed, c.Phase, "with the cod licence the cod lands normally");
            Assert.AreEqual(1, hold.UsedUnits);
            Assert.AreEqual(1, _caught);
        }

        [Test]
        public void UngatedSpecies_AlwaysLands_EvenWithNoLicenseService()
        {
            var hold = new FakeHold();
            // A herring not listed on any licence — ungated.
            var herring = MakeFish("fish.herring", FishCategory.Pelagic);
            var licenses = new[] { MakeCodLicense() };   // only cod is gated
            GameServices.Licenses = null;                 // no wallet at all

            var c = MakeController(hold, new[] { herring }, licenses, seed: 999);
            RunToResult(c);

            Assert.AreEqual(FishingPhase.Landed, c.Phase, "an ungated species lands regardless of licences");
            Assert.AreEqual(1, hold.UsedUnits);
            Assert.AreEqual(1, _caught);
        }
    }
}
