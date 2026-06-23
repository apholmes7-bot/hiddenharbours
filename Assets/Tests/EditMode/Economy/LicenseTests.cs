using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters opening — the licence system: the licence wallet (<see cref="LicenseService"/>), the
    /// vendor buy flow (<see cref="LicenseVendor"/>), and the catch-side gate (<see cref="CatchLicensePolicy"/>).
    /// Covers that an affordable buy charges the fee + grants the licence + raises <c>LicensePurchased</c>;
    /// an unaffordable or already-held buy changes nothing; and the gate: cod is NOT landable without the
    /// cod licence and IS once it's held, while an ungated species is always landable. Pure logic over the
    /// Core contracts — no scene, runs headless.
    /// </summary>
    public class LicenseTests
    {
        private sealed class FakeWallet : IWallet
        {
            public FakeWallet(int starting) { Money = starting; }
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        private readonly List<Object> _spawned = new();
        private readonly List<LicensePurchased> _events = new();
        private void OnPurchased(LicensePurchased e) => _events.Add(e);

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            EventBus.Clear<LicensePurchased>();
            EventBus.Subscribe<LicensePurchased>(OnPurchased);
            GameServices.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<LicensePurchased>(OnPurchased);
            EventBus.Clear<LicensePurchased>();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private LicenseDef MakeLicense(string id, int price, params string[] permitted)
        {
            var lic = ScriptableObject.CreateInstance<LicenseDef>();
            lic.Id = id;
            lic.DisplayName = id;
            lic.Price = price;
            lic.PermittedSpeciesIds = permitted;
            _spawned.Add(lic);
            return lic;
        }

        private LicenseService MakeService()
        {
            var go = new GameObject("LicenseService");
            _spawned.Add(go);
            // EditMode doesn't fire OnEnable for AddComponent, so Register() explicitly (the
            // public driver OnEnable calls at runtime).
            var svc = go.AddComponent<LicenseService>();
            svc.Register();
            return svc;
        }

        private LicenseVendor MakeVendor()
        {
            var go = new GameObject("LicenseVendor");
            _spawned.Add(go);
            return go.AddComponent<LicenseVendor>();
        }

        // ---- the wallet -----------------------------------------------------------------------

        [Test]
        public void NewWallet_HoldsNothing_AndUngatedReadsLicensed()
        {
            var svc = MakeService();
            Assert.AreEqual(0, svc.Count, "a fresh wallet holds no licences");
            Assert.IsFalse(svc.IsLicensed("license.cod"), "not licensed until granted");
            Assert.IsTrue(svc.IsLicensed(null), "a null/empty licence id = ungated = always 'licensed'");
            Assert.IsTrue(svc.IsLicensed(""), "empty licence id = ungated");
        }

        [Test]
        public void Grant_IsIdempotent()
        {
            var svc = MakeService();
            svc.Grant("license.cod");
            svc.Grant("license.cod");
            Assert.AreEqual(1, svc.Count, "granting the same licence twice holds it once");
            Assert.IsTrue(svc.IsLicensed("license.cod"));
        }

        [Test]
        public void Service_RegistersItself_OnGameServices()
        {
            var svc = MakeService();
            Assert.AreSame(svc, GameServices.Licenses, "the service wires itself into GameServices.Licenses");
        }

        // ---- the vendor -----------------------------------------------------------------------

        [Test]
        public void Vendor_AffordableBuy_ChargesFee_Grants_AndRaisesEvent()
        {
            var svc = MakeService();
            var vendor = MakeVendor();
            var lic = MakeLicense("license.cod", 120, "fish.atlantic_cod");
            var wallet = new FakeWallet(200);

            bool ok = vendor.TryBuy(lic, wallet, svc);

            Assert.IsTrue(ok);
            Assert.IsTrue(vendor.LastPurchaseSucceeded);
            Assert.AreEqual(80, wallet.Money, "fee deducted exactly");
            Assert.IsTrue(svc.IsLicensed("license.cod"), "the licence is granted on success");
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual("license.cod", _events[0].LicenseId);
            Assert.AreEqual(120, _events[0].PricePaid);
        }

        [Test]
        public void Vendor_Unaffordable_ChangesNothing_NoEvent()
        {
            var svc = MakeService();
            var vendor = MakeVendor();
            var lic = MakeLicense("license.cod", 120, "fish.atlantic_cod");
            var wallet = new FakeWallet(119);   // one coin short

            bool ok = vendor.TryBuy(lic, wallet, svc);

            Assert.IsFalse(ok);
            Assert.AreEqual(119, wallet.Money, "wallet untouched when unaffordable");
            Assert.IsFalse(svc.IsLicensed("license.cod"));
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void Vendor_AlreadyHeld_DoesNotDoubleCharge()
        {
            var svc = MakeService();
            var vendor = MakeVendor();
            var lic = MakeLicense("license.cod", 120, "fish.atlantic_cod");
            var wallet = new FakeWallet(500);

            Assert.IsTrue(vendor.TryBuy(lic, wallet, svc));
            int afterFirst = wallet.Money;
            bool second = vendor.TryBuy(lic, wallet, svc);

            Assert.IsFalse(second, "buying a licence you already hold is a no-op");
            Assert.AreEqual(afterFirst, wallet.Money, "no double charge");
            Assert.AreEqual(1, _events.Count, "only one purchase event");
        }

        // ---- the catch gate (the headline AC: rod fishes cod only when licensed) --------------

        [Test]
        public void Gate_Cod_NotLandableWithoutLicence_LandableWithIt()
        {
            var svc = MakeService();
            var cod = MakeLicense("license.cod", 120, "fish.atlantic_cod");
            var all = new List<LicenseDef> { cod };

            Assert.IsFalse(CatchLicensePolicy.MayLand("fish.atlantic_cod", all, svc),
                "cod is gated — not landable before the cod licence");

            svc.Grant("license.cod");

            Assert.IsTrue(CatchLicensePolicy.MayLand("fish.atlantic_cod", all, svc),
                "cod is landable once the cod licence is held");
        }

        [Test]
        public void Gate_UngatedSpecies_AlwaysLandable()
        {
            var svc = MakeService();
            var cod = MakeLicense("license.cod", 120, "fish.atlantic_cod");
            var all = new List<LicenseDef> { cod };

            // The clam isn't listed by any licence → ungated → landable even with an empty wallet.
            Assert.IsTrue(CatchLicensePolicy.MayLand("fish.soft_shell_clam", all, svc));
            Assert.IsNull(CatchLicensePolicy.RequiredLicenseFor("fish.soft_shell_clam", all));
            Assert.AreEqual("license.cod", CatchLicensePolicy.RequiredLicenseFor("fish.atlantic_cod", all));
        }

        [Test]
        public void Gate_NullService_FailsClosedForGated_OpenForUngated()
        {
            var cod = MakeLicense("license.cod", 120, "fish.atlantic_cod");
            var all = new List<LicenseDef> { cod };

            // No wallet at all (e.g. before bootstrap): gated species not landable, ungated still are.
            Assert.IsFalse(CatchLicensePolicy.MayLand("fish.atlantic_cod", all, null));
            Assert.IsTrue(CatchLicensePolicy.MayLand("fish.soft_shell_clam", all, null));
        }
    }
}
