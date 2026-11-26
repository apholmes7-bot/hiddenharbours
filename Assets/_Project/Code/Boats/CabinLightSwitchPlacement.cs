using System;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>Where on a boat a cabin-light switch is mounted, by what it is next to.</summary>
    public enum CabinLightSwitchRole
    {
        /// <summary>At the helm: every boat with a cabin has one.</summary>
        Helm = 0,

        /// <summary>By the entry door: only where the helm is far from it.</summary>
        Door = 1,
    }

    /// <summary>
    /// <b>One cabin-light switch a boat names for herself</b>, authored on her
    /// <see cref="BoatVisualDef.CabinLightSwitches"/>. A boat that names any switch names ALL of them:
    /// her list replaces the distance rule outright (see <see cref="CabinLightSwitchRule"/>). Empty on
    /// every shipped boat today, so every boat is laid out by the rule.
    /// </summary>
    [Serializable]
    public sealed class CabinLightSwitchPlacement
    {
        [Tooltip("This switch's own id on this boat, append-only: 'helm', 'door', 'saloon'. It becomes " +
                 "the end of the fixture id, so it must be unique on the boat.")]
        public string Id = "";

        [Tooltip("What the switch is next to. Says nothing about behaviour: every switch on a boat " +
                 "throws the same cabin lights.")]
        public CabinLightSwitchRole Role = CabinLightSwitchRole.Helm;

        [Tooltip("Where it is mounted, hull-local metres in the sidecars' frame (origin amidships / keel " +
                 "bottom / centreline; +x starboard, +y bow, +z up) - the same frame as the interior's " +
                 "door threshold and the deck's helm station.")]
        public Vector3 LocalMetres = Vector3.zero;
    }
}
