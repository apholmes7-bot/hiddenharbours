using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>The one cabin-light state a boat has</b>, on her ROOT, and the thing every one of her switches
    /// throws. The helm switch and the door switch are two buttons on this one state; neither holds
    /// anything of its own, so they can never disagree. In co-op the state is the boat's, not a
    /// player's: whoever is aboard and presses a switch changes it for everyone.
    ///
    /// <para><b>Per boat, not per hull.</b> A root can wear several of the player's boats in a session
    /// (a purchase or a swap goes through <see cref="BoatController.SetHull"/>). Each boat keeps her own
    /// setting under her owned-boat key, so swapping away and back finds her lights as she was left.</para>
    ///
    /// <para><b>Change-driven.</b> Nothing here runs per frame: a press sets the state, hands it to the
    /// boat's lights through <see cref="ICabinLightsOutlet"/>, and publishes
    /// <see cref="CabinLightSwitchThrown"/> once. A rebuilt cabin hands it over again.</para>
    ///
    /// <para><b>Not saved.</b> No save row fits a per-boat setting today, and the save format does not
    /// change here, so a loaded game starts every boat on <see cref="CabinLightSwitchSetting.Auto"/>:
    /// the automatic rule the lights had before the switch existed.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CabinLightSwitchboard : MonoBehaviour
    {
        private readonly Dictionary<string, CabinLightSwitchSetting> _byBoat =
            new Dictionary<string, CabinLightSwitchSetting>(System.StringComparer.Ordinal);

        private string _boatKey = "";
        private CabinLightSwitchSetting _setting = CabinLightSwitchSetting.Auto;
        private ICabinLightsOutlet _outlet;
        private bool _outletInjected;

        /// <summary>What the switch is set to on the boat she is wearing now.</summary>
        public CabinLightSwitchSetting Setting => _setting;

        /// <summary>The owned-boat key the current setting belongs to ("" before the first cabin is built).</summary>
        public string BoatKey => _boatKey;

        /// <summary>
        /// Hand the board an outlet explicitly (a test, or a lights component that registers itself).
        /// Null returns the board to finding its outlet under its own root.
        /// </summary>
        public void SetOutlet(ICabinLightsOutlet outlet)
        {
            _outlet = outlet;
            _outletInjected = outlet != null;
            Push();
        }

        /// <summary>
        /// Her cabin has been (re)built for the boat with this key. Remembers the old boat's setting,
        /// picks up the new one's (Auto for a boat never pressed), and hands it to the lights.
        /// </summary>
        public void BeginBoat(string boatKey)
        {
            string key = boatKey ?? "";
            if (!string.Equals(key, _boatKey, System.StringComparison.Ordinal))
            {
                if (_boatKey.Length > 0) _byBoat[_boatKey] = _setting;
                _boatKey = key;
                _setting = _byBoat.TryGetValue(key, out CabinLightSwitchSetting kept)
                    ? kept : CabinLightSwitchSetting.Auto;
            }
            if (!_outletInjected) _outlet = null;   // the lights may have been rebuilt with the hull
            Push();
        }

        /// <summary>
        /// <b>A switch was pressed.</b> Flips the setting by <see cref="CabinLightSwitchRule.Toggled"/>,
        /// hands it to the lights, publishes one <see cref="CabinLightSwitchThrown"/>, and returns
        /// whether the lights are now on.
        /// </summary>
        public bool Throw(string switchId)
        {
            ICabinLightsOutlet outlet = Outlet;
            bool lit = false;
            bool litKnown = outlet != null && outlet.TryReadLit(out lit);
            _setting = CabinLightSwitchRule.Toggled(_setting, litKnown, lit);
            if (_boatKey.Length > 0) _byBoat[_boatKey] = _setting;
            outlet?.Apply(_setting);

            bool on = _setting == CabinLightSwitchSetting.On;
            EventBus.Publish(new CabinLightSwitchThrown(gameObject.GetEntityId(), switchId, on));
            return on;
        }

        /// <summary>The setting a boat with this key was last left at in this session (Auto if never).</summary>
        public CabinLightSwitchSetting SettingFor(string boatKey)
        {
            string key = boatKey ?? "";
            if (string.Equals(key, _boatKey, System.StringComparison.Ordinal)) return _setting;
            return _byBoat.TryGetValue(key, out CabinLightSwitchSetting kept) ? kept : CabinLightSwitchSetting.Auto;
        }

        private ICabinLightsOutlet Outlet
        {
            get
            {
                if (_outlet == null && !_outletInjected) _outlet = CabinLightsOutlet.ForRoot(transform);
                return _outlet;
            }
        }

        private void Push() => Outlet?.Apply(_setting);
    }
}
