using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The helm goes to the hull the player is aboard, never to the hull that enabled last.</b> The
    /// arbitration rule itself (<see cref="HelmSlot"/>), proved without a scene — which is the point of
    /// it being a POCO: a relay's <c>OnEnable</c> never fires in EditMode, so the ORDER-INDEPENDENCE
    /// claim cannot be stated at all in the place the bug lived.
    ///
    /// <para>The defect these pin (owner playtest 2026-08-21): every <c>BoatController</c> installs a
    /// helm relay and each one used to assign itself into <c>GameServices.HelmControl</c> on enable, so
    /// two live hulls meant last-writer-wins — the St Peters arrival drew a passenger the skipper's
    /// wheel and gauges, and any later hull took the player's helm away permanently, because the loser
    /// never registered again.</para>
    /// </summary>
    public class HelmSlotOwnershipTests
    {
        // ---- the two hulls in the story ---------------------------------------------------------
        // Plain tokens, exactly as Core sees them: the arbiter never dereferences a hull, it only asks
        // whether two of them are the same object.
        private object _playersDory;
        private object _skippersBoat;

        private FakeRelay _dorysRelay;
        private FakeRelay _skippersRelay;
        private HelmSlot _slot;

        [SetUp]
        public void SetUp()
        {
            _playersDory = new object();
            _skippersBoat = new object();
            _dorysRelay = new FakeRelay("boat.dory");
            _skippersRelay = new FakeRelay("boat.skipper");
            _slot = new HelmSlot();
        }

        // ---- registering is not winning ---------------------------------------------------------

        [Test]
        public void ARegisteredRelay_HoldsNothing_UntilItsHullIsDeclaredPiloted()
        {
            _slot.Register(_playersDory, _dorysRelay, _dorysRelay);

            Assert.AreEqual(1, _slot.RegisteredCount, "precondition: the relay is registered");
            Assert.IsNull(_slot.Control,
                "registering must not grant. 'The only boat in the scene must be the player's' is the " +
                "guess that produced the defect — right until a second hull exists, then wrong silently");
            Assert.IsNull(_slot.Instruments, "the glass follows the same grant, so it is empty too");
        }

        [Test]
        public void DeclaringAHullNobodyHasRegistered_GrantsNothing_AndDoesNotThrow()
        {
            _slot.SetPilotedHull(_playersDory);

            Assert.IsNull(_slot.Control, "there is no relay to grant it to yet");
            Assert.AreSame(_playersDory, _slot.PilotedHull, "but the declaration stands, waiting");
        }

        // ---- ⭐ THE FIX: either enable order, same answer ----------------------------------------

        [Test]
        public void ThePlayersHullHoldsTheHelm_WhenHersRegistersFirst()
        {
            _slot.SetPilotedHull(_playersDory);
            _slot.Register(_playersDory, _dorysRelay, _dorysRelay);
            _slot.Register(_skippersBoat, _skippersRelay, _skippersRelay);   // spawns alongside, later

            AssertHelmIsThePlayers();
        }

        [Test]
        public void ThePlayersHullHoldsTheHelm_WhenTheOtherHullRegistersLast_TheOldLastWriterWinsCase()
        {
            // This is the exact shape of the bug: the OTHER boat enabled last. Under the old seam it
            // took the slot on that alone, and the player's relay never registered again.
            _slot.Register(_playersDory, _dorysRelay, _dorysRelay);
            _slot.SetPilotedHull(_playersDory);
            _slot.Register(_skippersBoat, _skippersRelay, _skippersRelay);

            AssertHelmIsThePlayers();
        }

        [Test]
        public void ThePlayersHullHoldsTheHelm_WhenSheTakesItLast_AfterBothHullsAreAlive()
        {
            _slot.Register(_skippersBoat, _skippersRelay, _skippersRelay);
            _slot.Register(_playersDory, _dorysRelay, _dorysRelay);
            _slot.SetPilotedHull(_playersDory);

            AssertHelmIsThePlayers();
        }

        private void AssertHelmIsThePlayers()
        {
            Assert.AreEqual(2, _slot.RegisteredCount, "precondition: both hulls are alive and considered");
            Assert.AreSame(_dorysRelay, _slot.Control, "the helm belongs to the hull the player is aboard");
            Assert.AreSame(_dorysRelay, _slot.Instruments,
                "and so does the glass — one relay, granted in one breath, so the dash and the throttle " +
                "can never describe two different boats");
            Assert.IsTrue(_slot.Holds(_dorysRelay));
            Assert.IsFalse(_slot.Holds(_skippersRelay), "the other hull is registered, not granted");
        }

        // ---- stepping back ----------------------------------------------------------------------

        [Test]
        public void LeavingTheHelm_EmptiesTheSlot_EvenWithBothHullsStillAlive()
        {
            _slot.Register(_playersDory, _dorysRelay, _dorysRelay);
            _slot.Register(_skippersBoat, _skippersRelay, _skippersRelay);
            _slot.SetPilotedHull(_playersDory);
            Assert.AreSame(_dorysRelay, _slot.Control, "precondition: she is at her own helm");

            _slot.SetPilotedHull(null);   // stepped back onto the deck / ashore

            Assert.IsNull(_slot.Control,
                "stepping back from the wheel must leave NOBODY holding it — the other boat must not " +
                "inherit the helm just by still being there");
            Assert.IsNull(_slot.Instruments);
            Assert.IsFalse(_slot.Holds(_skippersRelay));
            Assert.AreEqual(2, _slot.RegisteredCount, "both relays are still registered; neither is granted");
        }

        [Test]
        public void TheGrantedRelayStandingDown_EmptiesTheSlot_AndDoesNotFallThroughToTheOtherHull()
        {
            _slot.Register(_playersDory, _dorysRelay, _dorysRelay);
            _slot.Register(_skippersBoat, _skippersRelay, _skippersRelay);
            _slot.SetPilotedHull(_playersDory);

            _slot.Unregister(_playersDory);   // her relay disabled under her

            Assert.IsNull(_slot.Control, "no helm rather than SOMEBODY ELSE'S helm");
            Assert.AreSame(_playersDory, _slot.PilotedHull, "the declaration stands — she has not moved");

            _slot.Register(_playersDory, _dorysRelay, _dorysRelay);   // …and comes back
            Assert.AreSame(_dorysRelay, _slot.Control,
                "the seam is self-healing: a relay that stood down and returned is granted again, which " +
                "the old slot could never do (the loser of a last-writer race never re-registered)");
        }

        [Test]
        public void AnUngrantedRelayStandingDown_DoesNotDisturbTheHelm()
        {
            _slot.Register(_playersDory, _dorysRelay, _dorysRelay);
            _slot.Register(_skippersBoat, _skippersRelay, _skippersRelay);
            _slot.SetPilotedHull(_playersDory);

            _slot.Unregister(_skippersBoat);   // the arrival boat sails off / is despawned

            Assert.AreSame(_dorysRelay, _slot.Control, "she keeps her own wheel");
        }

        // ---- ⚠ fake-null: a destroyed hull is gone, and is not equal to every other destroyed hull ----

        [Test]
        public void AHullDestroyedWhileHoldingTheHelm_EmptiesTheSlot()
        {
            // A REAL destroyed UnityEngine.Object, so the fake-null is genuine: `== null` says gone
            // while `ReferenceEquals(o, null)` and `o is null` both still say it is there.
            var hull = ScriptableObject.CreateInstance<HelmSlotTestHull>();
            _slot.Register(hull, _dorysRelay, _dorysRelay);
            _slot.SetPilotedHull(hull);
            Assert.AreSame(_dorysRelay, _slot.Control, "precondition: she holds her own helm");

            Object.DestroyImmediate(hull);

            Assert.IsFalse(ReferenceEquals(hull, null),
                "precondition: this is a FAKE-null — the C# reference is still there, which is exactly " +
                "why ?? / ?. / `is null` cannot be used to test it");
            Assert.IsNull(_slot.Control, "a hull that no longer exists is not a hull anybody is piloting");
            Assert.IsNull(_slot.Instruments);
            Assert.IsFalse(_slot.Holds(_dorysRelay));
        }

        [Test]
        public void TwoDestroyedHulls_AreNotTheSameHull()
        {
            // The trap behind matching by identity rather than by Unity's ==: to that operator EVERY
            // destroyed object equals null and therefore equals every other destroyed object, so a dead
            // ambient hull would satisfy a dead declaration and hand its relay the helm.
            var mine = ScriptableObject.CreateInstance<HelmSlotTestHull>();
            var theirs = ScriptableObject.CreateInstance<HelmSlotTestHull>();
            _slot.Register(theirs, _skippersRelay, _skippersRelay);
            _slot.SetPilotedHull(mine);

            Object.DestroyImmediate(mine);
            Object.DestroyImmediate(theirs);

            Assert.IsTrue(mine == null && theirs == null, "precondition: Unity's == reports both as gone");
            Assert.IsNull(_slot.Control, "two dead hulls must not match each other into a live grant");
        }

        [Test]
        public void ADeadRelay_IsNeverHandedOut_EvenWithItsHullStillAlive()
        {
            // The teardown-storm case: the component died without its OnDisable running, so nothing
            // unregistered it. The arbiter must not hand a destroyed relay to the overlay.
            var deadRelay = ScriptableObject.CreateInstance<HelmSlotTestRelay>();
            _slot.Register(_playersDory, deadRelay, deadRelay);
            _slot.SetPilotedHull(_playersDory);
            Assert.AreSame(deadRelay, _slot.Control, "precondition: it held the helm while it lived");

            Object.DestroyImmediate(deadRelay);

            Assert.IsNull(_slot.Control, "a destroyed relay is not a helm");
            Assert.AreEqual(0, _slot.RegisteredCount, "and it is dropped from the register on the way past");
        }

        // ---- housekeeping -----------------------------------------------------------------------

        [Test]
        public void ReRegisteringOneRelay_MovesItToItsNewHull_RatherThanAddingASecondEntry()
        {
            // The dev boat picker's F-swap re-skins the ONE persistent boat in place; a hull rebind
            // must not leave the relay registered twice, nor against the boat it no longer rides.
            _slot.Register(_playersDory, _dorysRelay, _dorysRelay);
            _slot.Register(_skippersBoat, _dorysRelay, _dorysRelay);

            Assert.AreEqual(1, _slot.RegisteredCount, "one relay, one entry");
            _slot.SetPilotedHull(_playersDory);
            Assert.IsNull(_slot.Control, "it no longer rides that hull");
            _slot.SetPilotedHull(_skippersBoat);
            Assert.AreSame(_dorysRelay, _slot.Control, "it rides this one");
        }

        [Test]
        public void ResetEmptiesEverything()
        {
            _slot.Register(_playersDory, _dorysRelay, _dorysRelay);
            _slot.SetPilotedHull(_playersDory);

            _slot.Reset();

            Assert.IsNull(_slot.Control);
            Assert.IsNull(_slot.Instruments);
            Assert.IsNull(_slot.PilotedHull);
            Assert.AreEqual(0, _slot.RegisteredCount);
        }

        [Test]
        public void UnregisteringSomethingThatWasNeverRegistered_IsANoOp()
        {
            _slot.Register(_playersDory, _dorysRelay, _dorysRelay);
            _slot.SetPilotedHull(_playersDory);

            _slot.Unregister(_skippersBoat);
            _slot.Unregister(null);
            _slot.Register(null, _skippersRelay, _skippersRelay);

            Assert.AreSame(_dorysRelay, _slot.Control, "none of that disturbed the standing grant");
            Assert.AreEqual(1, _slot.RegisteredCount);
        }

        // ---- doubles ----------------------------------------------------------------------------

        /// <summary>A relay with no boat behind it: the arbiter never asks it anything, it only decides
        /// which one to hand out. <see cref="IsPlayerHelm"/> is the relay's own business (the real one
        /// answers it against the arbiter) and is not under test here.</summary>
        private sealed class FakeRelay : IHelmControl, IHelmInstruments
        {
            private readonly string _hullId;
            public FakeRelay(string hullId) { _hullId = hullId; }

            public bool HasHelm => true;
            public bool IsPlayerHelm => true;
            public HelmControlStyle Style => HelmControlStyle.Tiller;
            public HelmLeverFinish LeverFinish => HelmLeverFinish.Graphite;
            public HelmWheelRim WheelRim => HelmWheelRim.Rubber;
            public HelmFit Fit => HelmFit.None;
            public float Drive => 0f;
            public float Steer => 0f;
            public void StepAhead() { }
            public void StepAstern() { }
            public void SetNeutral() { }
            public void DragDrive(float sig) { }
            public void EndDrag() { }
            public bool SteerDragActive => false;
            public void DragSteer(float steer) { }
            public void EndSteerDrag() { }

            public string HullId => _hullId;
            public bool TryReadDepth(out float metres) { metres = 0f; return false; }
            public bool TryReadPosition(out Vector2 worldPos) { worldPos = Vector2.zero; return false; }
            public SounderPrefs SounderPrefs => SounderPrefs.FromDefaults(DepthSounderSettings.Default);
            public void SetSounderPrefs(in SounderPrefs prefs) { }
        }

        /// <summary>A hull token that is a real <c>UnityEngine.Object</c>, so a test can genuinely
        /// destroy one and produce the fake-null the arbiter has to survive. A ScriptableObject rather
        /// than a GameObject: EditMode, no scene, and <c>DestroyImmediate</c> is honest on it.</summary>
        private sealed class HelmSlotTestHull : ScriptableObject { }

        /// <summary>The same, for the relay side of the register.</summary>
        private sealed class HelmSlotTestRelay : ScriptableObject, IHelmControl, IHelmInstruments
        {
            public bool HasHelm => true;
            public bool IsPlayerHelm => true;
            public HelmControlStyle Style => HelmControlStyle.Tiller;
            public HelmLeverFinish LeverFinish => HelmLeverFinish.Graphite;
            public HelmWheelRim WheelRim => HelmWheelRim.Rubber;
            public HelmFit Fit => HelmFit.None;
            public float Drive => 0f;
            public float Steer => 0f;
            public void StepAhead() { }
            public void StepAstern() { }
            public void SetNeutral() { }
            public void DragDrive(float sig) { }
            public void EndDrag() { }
            public bool SteerDragActive => false;
            public void DragSteer(float steer) { }
            public void EndSteerDrag() { }

            public string HullId => "boat.dead";
            public bool TryReadDepth(out float metres) { metres = 0f; return false; }
            public bool TryReadPosition(out Vector2 worldPos) { worldPos = Vector2.zero; return false; }
            public SounderPrefs SounderPrefs => SounderPrefs.FromDefaults(DepthSounderSettings.Default);
            public void SetSounderPrefs(in SounderPrefs prefs) { }
        }
    }
}
