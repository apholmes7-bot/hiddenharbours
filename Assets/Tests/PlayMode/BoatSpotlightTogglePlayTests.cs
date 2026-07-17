using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// THE SPOTLIGHT IS A SWITCH, AND IT STARTS OFF — the owner's call ("spotlight should be a toggle button
    /// currently"; "the rowboat should not have one currently").
    ///
    /// <para><b>Why PlayMode.</b> The whole feature is lifecycle: <c>Awake</c> seeds the switch, <c>Update</c>
    /// reads the key and publishes the water globals, <c>SceneLight.OnDisable</c> pools the land quad off. None
    /// of that runs in EditMode (no <c>[ExecuteAlways]</c>), so an EditMode test could only re-assert the
    /// getter it just set.</para>
    ///
    /// <para><b>What "off" has to mean.</b> The beam reaches the frame by TWO independent mechanisms (ADR 0016
    /// follow-up fix 2): the additive <see cref="SceneLight"/> quad lights LAND, and the published
    /// <c>_BoatLight*</c> globals light WATER from inside the water shader. Kill only one and a "switched-off"
    /// beam still burns on the other surface — so every off assertion here checks BOTH.</para>
    /// </summary>
    public class BoatSpotlightTogglePlayTests
    {
        const string DataBoats = "Assets/_Project/Data/Boats";

        // _BoatLightParams.x = the effective published water intensity. <= 0 is the shader's "no beam".
        private static readonly int IdBoatLightParams = Shader.PropertyToID("_BoatLightParams");
        private static float PublishedWaterIntensity => Shader.GetGlobalVector(IdBoatLightParams).x;

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Clear<DevNotice>();
            Shader.SetGlobalVector(IdBoatLightParams, Vector4.zero);   // no beam leaking in from a prior test
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Clear<DevNotice>();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        private BoatHullDef LoadHull(string file)
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<BoatHullDef>($"{DataBoats}/{file}.asset");
            Assert.IsNotNull(asset, $"{DataBoats}/{file}.asset is missing — run the cove builder.");
            return asset;
#else
            Assert.Ignore("Needs the AssetDatabase: these assert the REAL committed hulls, not a mirror.");
            return null;
#endif
        }

        /// <summary>A bare carrier wearing a spotlight, as the builder leaves the player boat.</summary>
        private BoatSpotlight NewLitObject()
        {
            var go = new GameObject("SpotlightCarrier");
            _spawned.Add(go);
            return go.AddComponent<BoatSpotlight>();
        }

        // ---- the switch itself --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheBeam_IsOffByDefault_OnBothLandAndWater()
        {
            // THE regression this feature exists for. The builder mounts a spotlight unconditionally on the ONE
            // persistent player boat, and the dev picker re-skins that boat IN PLACE — so the component rides
            // every hull. It used to light up regardless, which is why the ROWBOAT had a searchlight. The mount
            // is still unconditional (it has to be); the DEFAULT is what changed.
            var spot = NewLitObject();
            yield return null;   // one full Awake/Update cycle — the frame that used to light the beam

            Assert.IsFalse(spot.BeamOn, "the spotlight wakes DARK — a beam is something you reach for");
            Assert.IsNotNull(spot.Light, "…it still owns its SceneLight; it is switched off, not absent");
            Assert.IsFalse(spot.Light.enabled, "the LAND quad is off");
            Assert.AreEqual(0f, PublishedWaterIntensity, 1e-5f,
                "…and the WATER term is off too. Land and water are lit by two different mechanisms; a test " +
                "that checked only the quad would pass with the sea still lit under a dark lamp.");
        }

        [UnityTest]
        public IEnumerator TheBeam_TogglesOnAndOff_AndCleansUpTheWaterEachTimeItGoesOut()
        {
            // On -> off -> on. The off legs are the load-bearing ones: BoatSpotlight publishes the water globals
            // on a throttled tick, so "off" cannot mean "stop publishing" — a stale non-zero would sit on the
            // shader forever with no lamp behind it. It must publish a ZERO.
            var spot = NewLitObject();
            yield return null;
            Assert.IsFalse(spot.BeamOn, "precondition: she starts dark");

            spot.ToggleBeam();
            yield return null;   // let Update publish the live beam
            Assert.IsTrue(spot.BeamOn, "one press lights her");
            Assert.IsTrue(spot.Light.enabled, "the land quad comes back on");
            Assert.Greater(PublishedWaterIntensity, 0f,
                "…and the water term is published live. (Non-zero even at a standstill: the way-gate dims to " +
                "BoatSpotlight's stationary FLOOR, it does not snap to black.)");

            spot.ToggleBeam();
            yield return null;
            Assert.IsFalse(spot.BeamOn, "a second press puts her out");
            Assert.IsFalse(spot.Light.enabled, "the land quad goes dark");
            Assert.AreEqual(0f, PublishedWaterIntensity, 1e-5f,
                "…and the water term is ZEROED, not merely left un-refreshed");

            spot.ToggleBeam();
            yield return null;
            Assert.IsTrue(spot.BeamOn, "…and she lights again — a switch, not a one-shot");
            Assert.IsTrue(spot.Light.enabled);
            Assert.Greater(PublishedWaterIntensity, 0f, "the water relights with her");
        }

        [UnityTest]
        public IEnumerator TheSwitch_IsRememberedAcrossAReEnable_NotReSeeded()
        {
            // A boat is disabled and re-enabled constantly — stepping ashore, a region hop, the ControlSwitcher.
            // If OnEnable re-seeded from the serialized default, every one of those would silently douse a beam
            // the player had switched on, which reads as a dropped keypress.
            var spot = NewLitObject();
            yield return null;
            spot.SetBeam(true);
            yield return null;

            spot.enabled = false;
            yield return null;
            Assert.AreEqual(0f, PublishedWaterIntensity, 1e-5f,
                "a disabled spotlight leaves no beam stuck on the water (BoatSpotlight.OnDisable)");

            spot.enabled = true;
            yield return null;
            Assert.IsTrue(spot.BeamOn, "she comes back LIT — the player left the switch on");
            Assert.IsTrue(spot.Light.enabled);
            Assert.Greater(PublishedWaterIntensity, 0f, "…and the water relights with her");
        }

        // ---- the switch vs. the hull swap (F) -----------------------------------------------------

        [UnityTest]
        public IEnumerator TheSwitch_SurvivesAHullSwap_InBothPositions()
        {
            // F re-skins the ONE persistent boat IN PLACE — it does not build a new one — which is exactly why
            // every hull inherited the old always-on light. So the switch must ride the swap in BOTH positions:
            // off must stay off (the rowboat), and on must stay on (don't punish the player for changing boats
            // mid-passage at night).
            var roster = new[] { LoadHull("Dory"), LoadHull("SportSkiff") };
            var go = new GameObject("PickedBoat");
            _spawned.Add(go);
            var boat = go.AddComponent<BoatController>();
            var sr = go.AddComponent<SpriteRenderer>();
            var hold = go.AddComponent<ShipHold>();
            var picker = go.AddComponent<DevBoatPicker>();
            boat.SetHull(roster[0]);
            hold.SetHull(roster[0]);
            picker.Configure(roster, boat, hold, sr);
            BoatHullSkinner.ApplyHull(go, sr, roster[0], boat);
            var spot = go.AddComponent<BoatSpotlight>();
            yield return null;

            Assert.IsFalse(spot.BeamOn, "precondition: the dory wakes dark — the owner's 'the rowboat should " +
                                        "not have one'");

            picker.Next();       // -> the sport skiff
            yield return null;
            Assert.IsFalse(spot.BeamOn, "the swap does not light a lamp nobody switched on");
            Assert.AreEqual(0f, PublishedWaterIntensity, 1e-5f, "…on water either");
            Assert.AreSame(roster[1], boat.Hull, "precondition: the swap really happened");

            spot.SetBeam(true);
            yield return null;
            picker.Next();       // -> back to the dory
            yield return null;

            Assert.AreSame(roster[0], boat.Hull, "precondition: the swap really happened");
            Assert.IsTrue(spot.BeamOn, "…and a beam the player lit stays lit through the swap");
            Assert.IsTrue(spot.Light.enabled);
            Assert.Greater(PublishedWaterIntensity, 0f,
                "the water term rebinds to the new hull rather than going dark on it");
        }

        // ---- the binding ---------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheToggleKey_IsLightAndCollidesWithNothing()
        {
            // The key is a serialized owner tunable, so this pins the SHIPPED default rather than re-declaring
            // it. L for Light. Audited against every binding in the project: WASD/arrows helm, Space
            // brace/haul, E interact, Q mooring, P buy, B sell, C/1/2/Enter/LeftShift
            // (InputSystem_Actions.inputactions), F next-hull, G grant, H haul, T trap-drop and
            // rotation-mode, Y auto-yaw, Esc close.
            var spot = NewLitObject();
            yield return null;

            Assert.AreEqual(UnityEngine.InputSystem.Key.L, spot.ToggleKey,
                "L is the shipped toggle. If this changed, re-run the collision audit above before re-pinning " +
                "it — a key that shadows the helm or the boat picker is a bug the owner finds, not CI.");
        }
    }
}
