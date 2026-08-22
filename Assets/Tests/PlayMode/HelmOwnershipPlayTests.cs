using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>TWO BOATS UNDER WAY, ONE HELM — AND IT IS THE PLAYER'S.</b> The on-water half of the
    /// ownership seam: real <c>BoatController</c>s, real <c>Awake</c>/<c>OnEnable</c> (the very
    /// lifecycle the defect lived in, and the one EditMode cannot reach), and the real
    /// <see cref="HelmOverlayHost"/> deciding what to draw.
    ///
    /// <para><b>The defect</b> (owner playtest 2026-08-21, "it should only show the boat controls if the
    /// player is piloting"): every hull self-installs a helm relay, and each one used to assign itself
    /// into the Core slot on enable — so the hull that woke up LAST owned the helm. During the St Peters
    /// arrival that drew the passenger the skipper's wheel and gauges; in open water it meant any
    /// later-spawned hull took the player's own helm away for good.</para>
    ///
    /// <para><b>How the two boats are told apart on screen.</b> The player's is a CONSOLE hull, which
    /// earns the composed dash; the other is a bare motor, which earns the tiller card. So the host's
    /// <c>CardKind</c> names which boat won without any test-only accessor: <c>Dash</c> is hers,
    /// <c>Tiller</c> is his, and <c>None</c> is nobody's. Under the old seam the enable order decided
    /// it; here it must not be able to.</para>
    /// </summary>
    public class HelmOwnershipPlayTests
    {
        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            // The overlay host is a leaked singleton shared with the other helm fixtures (their
            // pattern, kept), so start from ITS defaults too: a helm window another fixture moved or
            // hid would take this fixture's card down for a reason that has nothing to do with
            // ownership, and read as a red here.
            BoatUiWindows.Reset();
            HelmInstrumentExpansion.Collapse();
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            BoatUiWindows.Reset();
            HelmInstrumentExpansion.Collapse();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        // ---- the fixture ------------------------------------------------------------------------

        private static HelmOverlayHost Host()
        {
            HelmOverlayHost host = HelmOverlayHost.Instance;
            if (host == null) host = new GameObject("HelmOverlayHost(test)").AddComponent<HelmOverlayHost>();
            return host;
        }

        /// <summary>An engine hull with a helm console — she composites a DASH.</summary>
        private BoatHullDef NewConsoleHull(string id)
        {
            var console = ScriptableObject.CreateInstance<HelmConsoleDef>();
            console.Id = "helm.test_" + id;
            console.Rig = ConsoleRigKind.Console;
            console.Lever = HelmLeverFinish.Graphite;
            console.Wheel = HelmWheelRim.Rubber;
            console.DefaultCompass = CompassMount.Dome;
            _spawned.Add(console);

            BoatHullDef h = NewEngineHull(id);
            h.Helm = console;
            return h;
        }

        /// <summary>A bare motor — no console, so she shows the outboard TILLER.</summary>
        private BoatHullDef NewEngineHull(string id)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.DisplayName = "Test Hull " + id;
            h.Propulsion = PropulsionType.Engine;
            h.MassKg = 700f;
            h.EnginePower = 650f;
            h.RudderAuthority = 600f;
            h.ForwardDrag = 140f;
            h.LateralDrag = 360f;
            h.WindExposure = 0f;
            h.DraughtMeters = 0.5f;
            _spawned.Add(h);
            return h;
        }

        /// <summary>Stand a hull up the way the game does — <c>BoatController.Awake</c> self-installs
        /// her relay, and enabling registers it. Nobody is granted anything by this.</summary>
        private (BoatController boat, HelmControlRelay relay) Launch(string name, BoatHullDef hull)
        {
            var go = new GameObject(name);
            var boat = go.AddComponent<BoatController>();   // RequireComponent → Rigidbody2D + capsule
            var col = go.GetComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(1.7f, 4.0f);
            boat.SetHull(hull);
            _spawned.Add(go);
            return (boat, go.GetComponent<HelmControlRelay>());
        }

        // ---- ⭐ the fix, in both enable orders ---------------------------------------------------

        [UnityTest]
        public IEnumerator TheHelmStaysThePlayers_WhenAnNpcSkiffLaunchesBesideHer()
        {
            HelmOverlayHost host = Host();

            var (dory, doryRelay) = Launch("PlayersDory", NewConsoleHull("boat.players_dory"));
            yield return null;                                     // her Awake + OnEnable have run
            GameServices.Helm.SetPilotedHull(dory);                // she takes her own helm
            yield return null;
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Dash),
                        "precondition: she is at her own helm and her dash is up");

            // …and an NPC hull is spawned alongside and gets under way. THIS is the old bug: her relay
            // enabled LAST, so it took the slot and the player's dash became somebody else's tiller.
            var (_, skiffRelay) = Launch("NpcSkiff", NewEngineHull("boat.npc_skiff"));
            yield return null;
            yield return null;

            AssertTheHelmIsStillHers(host, doryRelay, skiffRelay);
        }

        [UnityTest]
        public IEnumerator TheHelmStaysThePlayers_WhenTheNpcSkiffWasAlreadyUnderWay()
        {
            HelmOverlayHost host = Host();

            var (_, skiffRelay) = Launch("NpcSkiff", NewEngineHull("boat.npc_skiff"));
            yield return null;
            Assert.IsNull(GameServices.HelmControl,
                          "a hull nobody is piloting must not put a helm on screen just by existing");

            var (dory, doryRelay) = Launch("PlayersDory", NewConsoleHull("boat.players_dory"));
            yield return null;
            GameServices.Helm.SetPilotedHull(dory);
            yield return null;
            yield return null;

            AssertTheHelmIsStillHers(host, doryRelay, skiffRelay);
        }

        private static void AssertTheHelmIsStillHers(HelmOverlayHost host,
                                                     HelmControlRelay doryRelay,
                                                     HelmControlRelay skiffRelay)
        {
            Assert.AreEqual(2, GameServices.Helm.RegisteredCount,
                            "precondition: both hulls are alive and both relays did register");
            Assert.That(GameServices.HelmControl, Is.SameAs(doryRelay),
                        "the helm belongs to the hull the player is aboard, not the one that woke up last");
            Assert.That(GameServices.HelmInstruments, Is.SameAs(doryRelay),
                        "and so does the glass — one relay, one grant");
            Assert.That(GameServices.HelmInstruments.HullId, Is.EqualTo("boat.players_dory"),
                        "the instruments are reading HER boat");

            Assert.IsTrue(doryRelay.IsPlayerHelm, "her own relay says she is at the wheel");
            Assert.IsFalse(skiffRelay.IsPlayerHelm, "his does not");
            Assert.IsTrue(skiffRelay.HasHelm,
                          "⚠ though HasHelm is TRUE of him — he is an engine hull under way. Reading THAT " +
                          "as 'the player is piloting' is the whole defect");

            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Dash),
                        "the overlay still draws HER dash. A Tiller card here would be the skiff's helm " +
                        "drawn to somebody who is not steering her");
        }

        // ---- stepping back, and the hull dying under her ----------------------------------------

        [UnityTest]
        public IEnumerator SteppingBackFromTheWheel_TakesTheOverlayDown_WithTheNpcHullStillUnderWay()
        {
            HelmOverlayHost host = Host();
            var (dory, _) = Launch("PlayersDory", NewConsoleHull("boat.players_dory"));
            Launch("NpcSkiff", NewEngineHull("boat.npc_skiff"));
            yield return null;
            GameServices.Helm.SetPilotedHull(dory);
            yield return null;
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Dash), "precondition");

            GameServices.Helm.SetPilotedHull(null);   // she steps back onto the deck
            yield return null;

            Assert.IsNull(GameServices.HelmControl, "nobody is piloting anything");
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.None),
                        "the card comes down rather than falling through to the other boat's tiller");
        }

        [UnityTest]
        public IEnumerator HerHullDestroyedUnderHer_ClearsTheHelm_RatherThanHandingItToTheNpc()
        {
            HelmOverlayHost host = Host();
            var (dory, _) = Launch("PlayersDory", NewConsoleHull("boat.players_dory"));
            Launch("NpcSkiff", NewEngineHull("boat.npc_skiff"));
            yield return null;
            GameServices.Helm.SetPilotedHull(dory);
            yield return null;
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Dash), "precondition");

            Object.DestroyImmediate(dory.gameObject);
            yield return null;

            Assert.IsFalse(ReferenceEquals(dory, null),
                           "precondition: a genuine fake-null — the C# reference survives the destroy");
            Assert.IsNull(GameServices.HelmControl, "a hull that no longer exists is nobody's helm");
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.None),
                        "and the overlay comes down instead of inheriting the surviving hull's");
        }

        // ---- the seam is self-healing, which the old slot never was ------------------------------

        [UnityTest]
        public IEnumerator HerRelayDisabledAndReEnabled_GetsHerHelmBack()
        {
            // Under last-writer-wins this was permanent: the loser of the race never re-registered, so
            // a toggled relay (a region hop's root-toggle, a disabled rig) lost the helm for the run.
            HelmOverlayHost host = Host();
            var (dory, doryRelay) = Launch("PlayersDory", NewConsoleHull("boat.players_dory"));
            Launch("NpcSkiff", NewEngineHull("boat.npc_skiff"));
            yield return null;
            GameServices.Helm.SetPilotedHull(dory);
            yield return null;

            doryRelay.enabled = false;
            yield return null;
            Assert.IsNull(GameServices.HelmControl, "with her relay down, nobody holds the helm");
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.None));

            doryRelay.enabled = true;
            yield return null;
            yield return null;

            Assert.That(GameServices.HelmControl, Is.SameAs(doryRelay), "and it comes back to her");
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Dash));
        }
    }
}
