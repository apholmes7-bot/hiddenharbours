using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Storage WITH MEMORY (owner 2026-11-17, save v25): a rack with a <c>storage.*</c> id writes what goes in
    /// and out to the save, and binds back to it on a load by moving the SAME scene tool into the rack, never
    /// a copy. A tool whose scene is not loaded keeps its slot. A rack whose Def names the camper's deed
    /// refuses until that deed is his. A rack with no storage id is session-only, exactly as before.
    /// </summary>
    public class StowageRackMemoryTests
    {
        const string RackId = "storage.stpeters.ginny_rod_rack";
        const string RodId = "tool.st_peters.rod";
        const string CamperDeed = "home.ginny_lot_camper";

        sealed class FlagSave : ISaveService
        {
            public SaveData Current { get; set; } = SaveMigration.NewGame();
            public bool GetFlag(string key)
            {
                if (Current?.OnboardingFlags == null) return false;
                foreach (SaveFlag f in Current.OnboardingFlags) if (f.Key == key) return f.Value;
                return false;
            }
            public void SetFlag(string key, bool value)
            {
                Current.OnboardingFlags ??= new List<SaveFlag>();
                Current.OnboardingFlags.RemoveAll(f => f.Key == key);
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
            }
            public void Save() { }
        }

        readonly List<Object> _spawned = new List<Object>();
        FlagSave _save;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();
            _save = new FlagSave();
            GameServices.Save = _save;
        }

        [TearDown]
        public void TearDown()
        {
            Interactables.Clear();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- fixtures ------------------------------------------------------------------------------------

        CarriableTool MakeTool(string instanceId, StowageClass stowage = StowageClass.Rod, string defId = ToolDef.RodId)
        {
            var def = ScriptableObject.CreateInstance<ToolDef>();
            def.Id = defId;
            def.DisplayName = defId;
            def.Stowage = stowage;
            _spawned.Add(def);

            var go = new GameObject($"Tool({instanceId})");
            _spawned.Add(go);
            var tool = go.AddComponent<CarriableTool>();
            tool.Configure(def, go.AddComponent<SpriteRenderer>(), instanceId);
            return tool;
        }

        StowageRack MakeRack(string id, int capacity = 2, string deed = "", StowageAccepts accepts = StowageAccepts.Rods)
        {
            var def = ScriptableObject.CreateInstance<StowageDef>();
            def.Id = "stowage.test_rack";
            def.DisplayName = "rod rack";
            def.Accepts = accepts;
            def.Capacity = capacity;
            def.DeedHomeId = deed;
            _spawned.Add(def);

            var go = new GameObject("RodRack");
            _spawned.Add(go);
            var holder = new GameObject("Slots");
            holder.transform.SetParent(go.transform, false);
            var rack = go.AddComponent<StowageRack>();
            rack.Configure(def, holder.transform, id);
            rack.RefreshRegistration();   // EditMode: OnEnable does not fire
            return rack;
        }

        CarryHands HandsHolding(CarriableTool tool)
        {
            CarryHands hands = ToolInHand.Empty(_spawned);
            Assert.AreEqual(CarryRefusal.None, hands.TryPickUp(tool), "fixture: the tool is in hand");
            return hands;
        }

        // ---- writing ---------------------------------------------------------------------------------------

        [Test]
        public void AStowIsWrittenToTheSave_AndATakeRemovesIt()
        {
            CarriableTool rod = MakeTool(RodId);
            CarryHands hands = HandsHolding(rod);
            StowageRack rack = MakeRack(RackId);

            Assert.AreEqual(StowRefusal.None, rack.TryStow(hands, rod));
            Assert.AreEqual(RackId, StoredContainers.ContainerOf(_save.Current, RodId));
            Assert.AreEqual("stowage.test_rack", StoredContainers.Find(_save.Current, RackId).ContainerDefId);

            Assert.AreEqual(CarryRefusal.None, rack.TryTake(hands, out CarriableTool taken));
            Assert.AreSame(rod, taken);
            Assert.IsNull(StoredContainers.ContainerOf(_save.Current, RodId), "in hand is not in the rack");
        }

        [Test]
        public void ARackWithoutAStorageId_IsSessionOnly()
        {
            CarriableTool rod = MakeTool(RodId);
            CarryHands hands = HandsHolding(rod);
            StowageRack rack = MakeRack("stowage.test_rack#1");

            Assert.IsFalse(rack.IsRemembered);
            Assert.AreEqual(StowRefusal.None, rack.TryStow(hands, rod));
            Assert.AreEqual(0, _save.Current.StoredContainers.Count, "nothing written");
            Assert.AreEqual(0, PlayerStorage.Count, "and not one of his storage containers");
        }

        [Test]
        public void AToolWithoutAnAuthoredId_IsNotRemembered()
        {
            CarriableTool rod = MakeTool("");
            CarryHands hands = HandsHolding(rod);
            StowageRack rack = MakeRack(RackId);

            Assert.IsFalse(rod.HasAuthoredId);
            Assert.AreEqual(StowRefusal.None, rack.TryStow(hands, rod));
            Assert.AreEqual(0, _save.Current.StoredContainers.Count, "a per-instance id would never be found again");
        }

        // ---- binding (the load) ----------------------------------------------------------------------------

        [Test]
        public void ALoadPutsTheSameRodBackInTheRack_NeverACopy()
        {
            CarriableTool rod = MakeTool(RodId);   // lying where the builder stood it
            StowageRack rack = MakeRack(RackId);
            StoredContainers.TryStoreInstance(_save.Current, RackId, "stowage.test_rack", ToolDef.RodId, RodId, 2);

            rack.BindToSave();

            Assert.AreEqual(1, rack.Count);
            Assert.IsTrue(rack.Holds(rod));
            Assert.AreEqual("Slots", rod.transform.parent.name);
            Assert.IsFalse(rod.gameObject.activeSelf);
            Assert.IsTrue(rod != null, "the same object, moved: nothing spawned, nothing destroyed");

            rack.BindToSave();
            Assert.AreEqual(1, rack.Count, "binding twice changes nothing");
        }

        [Test]
        public void ALoadOfASaveWithoutTheRod_LetsItOut()
        {
            CarriableTool rod = MakeTool(RodId);
            CarryHands hands = HandsHolding(rod);
            StowageRack rack = MakeRack(RackId);
            Assert.AreEqual(StowRefusal.None, rack.TryStow(hands, rod));

            _save.Current = SaveMigration.NewGame();   // another save, where the rod was never put away
            rack.BindToSave();

            Assert.AreEqual(0, rack.Count);
            Assert.IsTrue(rod.gameObject.activeSelf, "set down where the rack stands, never lost");
            Assert.IsNull(rod.transform.parent);
        }

        [Test]
        public void ARodWhoseSceneIsNotLoaded_KeepsItsSlot_AndWalksBackInWhenItLoads()
        {
            StowageRack rack = MakeRack(RackId, capacity: 1);
            StoredContainers.TryStoreInstance(_save.Current, RackId, "stowage.test_rack", ToolDef.RodId, RodId, 1);
            rack.BindToSave();
            Assert.AreEqual(0, rack.Count, "no such rod in any loaded scene");
            Assert.AreEqual(1, rack.UsedSlots, "but it is still in the rack");

            CarriableTool other = MakeTool("tool.st_peters.spare_rod");
            CarryHands hands = HandsHolding(other);
            Assert.AreEqual(StowRefusal.Full, rack.TryStow(hands, other), "its slot is taken");
            Assert.IsTrue(other.IsCarried, "refused: still in hand");

            CarriableTool rod = MakeTool(RodId);   // its scene loads (the tool's Start)
            Assert.IsTrue(StowageRack.ReturnIfStored(rod));
            Assert.IsTrue(rack.Holds(rod));
            Assert.AreEqual(1, rack.UsedSlots);
        }

        // ---- the deed --------------------------------------------------------------------------------------

        [Test]
        public void TheCampersRackRefusesUntilTheCamperIsHis()
        {
            CarriableTool rod = MakeTool(RodId);
            CarryHands hands = HandsHolding(rod);
            StowageRack rack = MakeRack("storage.stpeters.camper_rod_rack", capacity: 4, deed: CamperDeed);

            Assert.IsFalse(rack.IsOpen);
            Assert.AreEqual(StowRefusal.NotYours, rack.TryStow(hands, rod));
            Assert.IsTrue(rod.IsCarried, "refused: nothing moved");
            Assert.AreEqual(0, _save.Current.StoredContainers.Count);

            HomeDeeds.Grant(_save, CamperDeed);
            Assert.IsTrue(rack.IsOpen);
            Assert.AreEqual(StowRefusal.None, rack.TryStow(hands, rod));
            Assert.AreEqual("storage.stpeters.camper_rod_rack", StoredContainers.ContainerOf(_save.Current, RodId));
        }

        [Test]
        public void AClosedRackGivesNothingBack()
        {
            CarriableTool rod = MakeTool(RodId);
            StowageRack rack = MakeRack("storage.stpeters.camper_rod_rack", capacity: 4, deed: CamperDeed);
            StoredContainers.TryStoreInstance(_save.Current, "storage.stpeters.camper_rod_rack", "stowage.test_rack",
                                              ToolDef.RodId, RodId, 4);
            rack.BindToSave();
            Assert.AreEqual(1, rack.Count, "what is in it still shows");

            CarryHands hands = ToolInHand.Empty(_spawned);
            Assert.AreEqual(CarryRefusal.NotCarriable, rack.TryTake(hands, out _));
            Assert.IsTrue(rack.Holds(rod));
        }

        // ---- the list --------------------------------------------------------------------------------------

        [Test]
        public void ARememberedRackIsOneOfHisContainers_ButNotACatchHold()
        {
            StowageRack rack = MakeRack(RackId);
            Assert.AreEqual(1, PlayerStorage.Count);
            Assert.AreSame(rack, PlayerStorage.Find(RackId));
            Assert.AreSame(rack, PlayerStorage.GetContainer(0));
            Assert.IsNull(PlayerStorage.Get(0), "a rod rack is not a catch hold: the gift rows never see it");

            Object.DestroyImmediate(rack.gameObject);
            Assert.IsNull(PlayerStorage.Find(RackId), "a destroyed rack reads as gone");
            Assert.IsNull(PlayerStorage.GetContainer(0));
        }
    }
}
