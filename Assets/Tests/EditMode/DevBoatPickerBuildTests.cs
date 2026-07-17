#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The dev boat picker is actually BUILT onto the player's boat.
    ///
    /// <para>Separate from <see cref="DevBoatPickerTests"/> (which covers the picker's behaviour) because
    /// this covers the WIRING — and wiring is the half that rots. The picker is only useful if the builder
    /// hands it a roster and points it at the same boat/hold/renderer the rest of the rig uses; a picker
    /// that spawns unwired is a key that does nothing, which looks exactly like a bug in the picker itself.
    /// The <see cref="PersistentCoreBuilderTests"/> fixture builds with no roster, so this path would
    /// otherwise be untested.</para>
    /// </summary>
    public class DevBoatPickerBuildTests
    {
        private readonly List<Object> _spawned = new();
        private PersistentCoreBuilder.Handle _core;

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[]
                     {
                         _core.ServicesRoot, _core.CameraGo, _core.PlayerGo, _core.DoryGo, _core.GaugeGo,
                         _core.SwitcherGo, _core.LoaderGo,
                         _core.Coordinator != null ? _core.Coordinator.gameObject : null,
                     })
                if (go != null) Object.DestroyImmediate(go);
            _core = default;
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        private Sprite MakeSprite(string name)
        {
            var tex = new Texture2D(4, 4);
            _spawned.Add(tex);
            var s = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            s.name = name;
            _spawned.Add(s);
            return s;
        }

        private BoatVisualDef MakeVisual(string id)
        {
            var v = ScriptableObject.CreateInstance<BoatVisualDef>();
            v.Id = id;
            var facings = new Sprite[8];
            for (int i = 0; i < 8; i++) facings[i] = MakeSprite($"{id}_{i}");
            v.Facings = facings;
            _spawned.Add(v);
            return v;
        }

        private BoatHullDef MakeHull(string id, int hold)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id; h.DisplayName = id; h.HoldUnits = hold;
            h.Visual = MakeVisual("visual." + id);
            _spawned.Add(h);
            return h;
        }

        private void Build(BoatHullDef dory, BoatHullDef[] roster)
        {
            GameServices.Reset();
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(config);

            _core = PersistentCoreBuilder.Build(new PersistentCoreBuilder.Params
            {
                Config = config,
                StartDory = dory,
                PuntHull = null,
                DevPickerRoster = roster,
                RegionFish = null,
                Square = null,
                CameraBackground = Color.black,
                PlayerStartPos = new Vector3(-40f, -2f, 0f),
                BoatMooredPos = new Vector3(-40f, -26f, 0f),
                TideGatedWalk = true,
                CurrentSceneName = "StPeters",
                TideMean = 0f, TideAmplitude = 3.5f, TidePhaseHours = 1f,
            });
        }

        [Test]
        public void ARoster_SpawnsAPickerOnTheBoat_WiredToTheSameRigTheRestOfTheCoreUses()
        {
            var dory = MakeHull("boat.dory", 6);
            var console = MakeHull("boat.console_skiff", 20);
            Build(dory, new[] { dory, console });

            var picker = _core.DoryGo.GetComponent<DevBoatPicker>();
            Assert.IsNotNull(picker, "St Peters' roster must put a picker on the PLAYER's boat — that is the " +
                                     "only boat with a helm to press F at");
            Assert.AreEqual(2, picker.RosterCount, "the roster is DATA, and it arrives whole");
            Assert.AreSame(dory, picker.Current,
                "the cycle starts on the hull the boat is wearing, so the first press moves ON");

            // The wiring that makes the swap land: the picker must drive the SAME controller and hold the
            // rest of the core is built around, or F would swap a boat nobody is sailing.
            picker.Next();
            Assert.AreSame(console, _core.Boat.Hull, "…the core's controller");
            Assert.AreEqual(console.HoldUnits, _core.Hold.CapacityUnits, "…and the core's hold");
        }

        [Test]
        public void ThePicker_RidesThePersistentBoat_SoThePickedHullSurvivesARegionHop()
        {
            var dory = MakeHull("boat.dory", 6);
            Build(dory, new[] { dory });

            Assert.IsNotNull(_core.DoryGo.GetComponent<HiddenHarbours.App.PersistentObject>(),
                "the picker lives on the persistent Dory, so the hull you picked is still under you after " +
                "you sail to another region");
        }

        [Test]
        public void ThePicker_IsNotDisabledAtStart_BecauseItGatesItself()
        {
            // Unlike DevBoatInput (which the ControlSwitcher enables on boarding), the picker reads the
            // controller's own enabled flag. So it ships live and is inert ashore by construction — one
            // less thing for the switcher to know about, and one less thing to forget to re-enable.
            var dory = MakeHull("boat.dory", 6);
            Build(dory, new[] { dory });

            var picker = _core.DoryGo.GetComponent<DevBoatPicker>();
            Assert.IsTrue(picker.enabled, "the picker component itself is live…");
            Assert.IsFalse(_core.Boat.enabled, "…but the boat starts MOORED (the builder's contract)…");
            Assert.IsFalse(picker.IsAtHelm, "…so F does nothing until the player takes the helm");
        }

        [Test]
        public void NoRoster_SpawnsNoPicker()
        {
            // Greywick spawns no player boat, and a scene that hands over no roster wants no dev key bound.
            var dory = MakeHull("boat.dory", 6);
            Build(dory, null);

            Assert.IsNull(_core.DoryGo.GetComponent<DevBoatPicker>(),
                "no roster = no picker: a dev affordance must not appear in a scene that didn't ask for it");
        }

        [Test]
        public void AnEmptyRoster_SpawnsNoPicker()
        {
            var dory = MakeHull("boat.dory", 6);
            Build(dory, System.Array.Empty<BoatHullDef>());

            Assert.IsNull(_core.DoryGo.GetComponent<DevBoatPicker>(),
                "an empty roster is nothing to cycle — don't bind a key to it");
        }
    }
}
#endif
