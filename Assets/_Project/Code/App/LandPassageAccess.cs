using HiddenHarbours.Core;
using HiddenHarbours.Player;
using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>App-level control wiring: World never reaches into the Player module.</summary>
    public sealed class LandPassageAccess : MonoBehaviour, IRegionPassageAccess
    {
        private ControlSwitcher _switcher;
        public bool CanTravel
        {
            get
            {
                if (_switcher == null) _switcher = FindFirstObjectByType<ControlSwitcher>();
                return Allows(_switcher != null, _switcher != null ? _switcher.Mode : ControlMode.OnFoot);
            }
        }
        public static bool Allows(bool hasController, ControlMode mode) => hasController && mode == ControlMode.OnFoot;
    }
}
