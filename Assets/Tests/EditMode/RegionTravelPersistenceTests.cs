using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-22 travel persistence: the pieces that let the player cross a Cove↔Greywick hop with the SAME
    /// boat, hold and coin. The additive scene load + root toggling is play-mode glue (verified in Unity);
    /// these guard the pure wiring the slice rests on:
    ///   • the wallet/hold PROXIES a region's wharf resolves forward to the live PERSISTENT services, so
    ///     "the coin/catch crossed", and
    ///   • the arrival bind repositions the SAME rig and re-points the dock — no new player/boat/wallet/hold
    ///     is created on arrival, which is exactly what "the player survives the travel" means.
    /// </summary>
    public class RegionTravelPersistenceTests
    {
        private readonly List<Object> _spawned = new();
        private GameObject New(string n) { var g = new GameObject(n); _spawned.Add(g); return g; }
        private Transform At(string n, Vector3 p) { var g = New(n); g.transform.position = p; return g.transform; }

        [SetUp] public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- the coin crosses: wallet proxy → persistent wallet -----------------------------

        [Test]
        public void WalletProxy_ForwardsToThePersistentWallet()
        {
            var wallet = New("Wallet").AddComponent<PlayerWallet>();
            wallet.Add(500);
            GameServices.Wallet = wallet;                       // the persistent core wired this

            var proxy = New("Providers").AddComponent<PersistentWalletProxy>();
            Assert.AreEqual(500, proxy.Money, "the region wharf sees the persistent balance");
            Assert.IsTrue(proxy.TrySpend(200), "spending through the proxy hits the real wallet");
            Assert.AreEqual(300, wallet.Money, "the persistent wallet was charged (the Punt is bought with carried coin)");
            proxy.Add(80);
            Assert.AreEqual(380, wallet.Money, "selling at the far wharf pays the persistent wallet");
        }

        [Test]
        public void WalletProxy_BeforeServicesWired_IsSafeZero()
        {
            var proxy = New("Providers").AddComponent<PersistentWalletProxy>();   // GameServices.Wallet still null
            Assert.AreEqual(0, proxy.Money);
            Assert.IsFalse(proxy.TrySpend(10), "can't spend with no wallet");
            Assert.DoesNotThrow(() => proxy.Add(10), "a standalone-opened region scene must not throw");
        }

        // ---- the catch crosses: hold proxy → persistent hold --------------------------------

        [Test]
        public void HoldProxy_ForwardsToTheBoundPersistentHold()
        {
            var hold = New("Boat").AddComponent<ShipHold>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.dory"; hull.HoldUnits = 6;
            _spawned.Add(hull);
            hold.SetHull(hull);
            hold.TryAdd(new CatchItem("fish.cod", "Cod", FishCategory.InshoreGroundfish, 3f, 14, 0.2f));

            var proxy = New("Providers").AddComponent<PersistentHoldProxy>();
            proxy.Bind(hold);                                   // the coordinator binds this on arrival

            Assert.AreEqual(6, proxy.CapacityUnits, "the wharf sees the carried hull's capacity");
            Assert.AreEqual(1, proxy.UsedUnits, "…and the catch that sailed in with it");
            Assert.AreEqual(1, proxy.Items.Count);
            proxy.Clear();                                      // selling clears the hold
            Assert.AreEqual(0, hold.UsedUnits, "clearing through the proxy empties the REAL persistent hold");
        }

        [Test]
        public void HoldProxy_Unbound_IsSafeEmpty()
        {
            // No bind, no persistent ShipHold to find → safe-empty (a standalone-opened region scene).
            var proxy = New("Providers").AddComponent<PersistentHoldProxy>();
            Assert.AreEqual(0, proxy.CapacityUnits);
            Assert.AreEqual(0, proxy.UsedUnits);
            Assert.IsNotNull(proxy.Items);
            Assert.AreEqual(0, proxy.Items.Count);
            Assert.DoesNotThrow(() => proxy.Clear());
        }

        // ---- the player survives: arrival re-binds the SAME rig -----------------------------

        [Test]
        public void ApplyArrival_RepositionsTheSameRig_AndRepointsTheDock_AcrossARoundTrip()
        {
            // One persistent rig (created once — never recreated below).
            var player = New("Player").AddComponent<PlayerWalkController>();  // + Rigidbody2D + SpriteRenderer
            var boatGo = New("Boat");
            var boat = boatGo.AddComponent<BoatController>();                 // + Rigidbody2D + CapsuleCollider2D
            var switcher = New("Switcher").AddComponent<ControlSwitcher>();

            // Two region anchors (cove + Greywick), far apart so the dock test is unambiguous.
            var coveAnchor = New("CoveAnchor").AddComponent<RegionAnchor>();
            coveAnchor.Configure("region.coddle_cove", At("CoveArr", new Vector3(0f, -13f, 0f)),
                                 At("CoveDock", new Vector3(0f, -12f, 0f)), At("CoveDis", new Vector3(0f, -10.5f, 0f)));
            var gwAnchor = New("GwAnchor").AddComponent<RegionAnchor>();
            gwAnchor.Configure("region.port_greywick", At("GwArr", new Vector3(100f, -5f, 0f)),
                               At("GwDock", new Vector3(100f, -1f, 0f)), At("GwDis", new Vector3(100f, 1.5f, 0f)));

            // Start moored in the cove.
            switcher.Configure(player, boat, null, coveAnchor.DockZone, 3.5f, coveAnchor.DisembarkPoint);

            // --- Sail to Greywick: bind the rig to the Greywick anchor. ---
            RegionTravelCoordinator.ApplyArrival(player.transform, boatGo.transform, switcher, gwAnchor);
            Assert.AreEqual(gwAnchor.ArrivalPoint.position, boatGo.transform.position, "the boat arrives at the Greywick anchor");
            Assert.AreEqual(gwAnchor.DisembarkPoint.position, player.transform.position, "the player is set at the Greywick wharf");
            boatGo.transform.position = gwAnchor.DockZone.position;          // dock at the Greywick wharf
            Assert.IsTrue(switcher.InDockZone(), "disembark registers at the GREYWICK wharf (dock re-pointed)");
            Assert.Greater(Vector2.Distance(boatGo.transform.position, coveAnchor.DockZone.position), 3.5f,
                           "and it's the Greywick dock, not the (far) cove dock");

            // --- Sail back to the cove: SAME rig, re-bound to the cove anchor. ---
            RegionTravelCoordinator.ApplyArrival(player.transform, boatGo.transform, switcher, coveAnchor);
            Assert.AreEqual(coveAnchor.ArrivalPoint.position, boatGo.transform.position, "the boat returns to the cove anchor");
            boatGo.transform.position = coveAnchor.DockZone.position;
            Assert.IsTrue(switcher.InDockZone(), "disembark registers back at the cove dock");

            // The rig was repositioned, never recreated — the same instances persist through the round trip.
            Assert.IsNotNull(boat, "the same boat survived the round trip");
            Assert.IsNotNull(player, "the same player survived the round trip");
        }

        [Test]
        public void ApplyArrival_NullAnchor_IsNoOp()
        {
            var switcher = New("Switcher").AddComponent<ControlSwitcher>();
            Assert.DoesNotThrow(() => RegionTravelCoordinator.ApplyArrival(null, null, switcher, null));
        }
    }
}
