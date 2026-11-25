using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// What a boat's cabin lights are set to by her switch. The numbers match the lights' own switch
    /// states one for one, so handing this to the lights is a cast.
    /// </summary>
    public enum CabinLightSwitchSetting
    {
        /// <summary>Nobody has touched the switch: the lights keep their automatic rule (lit under way,
        /// or at a berth while somebody is aboard). Every NPC boat stays here; a player's boat starts
        /// here until he presses a switch.</summary>
        Auto = 0,

        /// <summary>Lit, at night, whoever is aboard.</summary>
        On = 1,

        /// <summary>Dark, even at night and under way.</summary>
        Off = 2,
    }

    /// <summary>One switch the rule has placed: its id on the boat, what it is next to, and where.</summary>
    public readonly struct CabinLightSwitchSpot
    {
        public readonly string Id;
        public readonly CabinLightSwitchRole Role;
        public readonly Vector3 LocalMetres;

        public CabinLightSwitchSpot(string id, CabinLightSwitchRole role, Vector3 localMetres)
        {
            Id = id ?? "";
            Role = role;
            LocalMetres = localMetres;
        }
    }

    /// <summary>
    /// <b>The cabin-light switch, as pure rules</b> (owner: "boat cabin lights need a switch at the
    /// helm" and "larger yachts also get a physical light switch if the helm is a far distance from
    /// the player's entry door").
    ///
    /// <para><b>Where the switches go.</b> A boat that names her switches in data
    /// (<see cref="BoatVisualDef.CabinLightSwitches"/>) gets exactly those. Otherwise every boat with a
    /// cabin gets one at her helm, and a second by her main entry door when the straight-line distance
    /// between the two is more than the configured threshold
    /// (<see cref="GameConfig.CabinLightSwitchDoorDistanceMetres"/>). Both points are data the boat
    /// already carries: the helm is her deck's imported helm station, else her interior's
    /// <c>enter_helm</c> anchor (the arrival's own rule for finding a helm); the door is her
    /// interior's main threshold.</para>
    ///
    /// <para><b>What a press does.</b> On becomes Off and Off becomes On. The first press on a boat
    /// still on Auto flips what the cabin shows at that moment, so the press always changes what the
    /// player sees.</para>
    ///
    /// <para>Pure and allocation-free: the planner writes into a caller's buffer.</para>
    /// </summary>
    public static class CabinLightSwitchRule
    {
        /// <summary>The interior anchor verb that marks where a helm is, when the deck has no station.</summary>
        public const string EnterHelmAction = "enter_helm";

        /// <summary>The rule's id for the switch at the helm.</summary>
        public const string HelmSpotId = "helm";

        /// <summary>The rule's id for the switch by the entry door.</summary>
        public const string DoorSpotId = "door";

        /// <summary>How many switches one boat can carry. A structural cap on the planner's buffer, not
        /// a tunable: the rule places at most two, and an authored list longer than this is cut.</summary>
        public const int MaxSwitchesPerBoat = 4;

        /// <summary>Is the helm far enough from the door to need its own switch there? Strictly more than
        /// the threshold; a threshold at or below zero means every boat with a door gets two.</summary>
        public static bool IsFar(Vector3 helm, Vector3 door, float farMetres)
        {
            float far = Mathf.Max(0f, farMetres);
            return (helm - door).sqrMagnitude > far * far;
        }

        /// <summary>
        /// Lay out one boat's switches into <paramref name="into"/> and return how many. An authored list
        /// (any usable entry) wins outright; otherwise the helm switch, plus the door switch when far.
        /// No helm point and no authored list = no switches (the content validation refuses that for a
        /// boat with a cabin).
        /// </summary>
        public static int Plan(CabinLightSwitchPlacement[] authored, bool hasHelm, Vector3 helm,
                               bool hasDoor, Vector3 door, float farMetres, CabinLightSwitchSpot[] into)
        {
            if (into == null || into.Length == 0) return 0;

            int n = 0;
            if (authored != null)
            {
                for (int i = 0; i < authored.Length && n < into.Length; i++)
                {
                    CabinLightSwitchPlacement p = authored[i];
                    if (p == null || string.IsNullOrEmpty(p.Id)) continue;
                    into[n++] = new CabinLightSwitchSpot(p.Id, p.Role, p.LocalMetres);
                }
                if (n > 0) return n;
            }

            if (!hasHelm) return 0;
            into[n++] = new CabinLightSwitchSpot(HelmSpotId, CabinLightSwitchRole.Helm, helm);
            if (hasDoor && n < into.Length && IsFar(helm, door, farMetres))
                into[n++] = new CabinLightSwitchSpot(DoorSpotId, CabinLightSwitchRole.Door, door);
            return n;
        }

        /// <summary>The same layout, read off a boat's visual def. Zero for a boat without a cabin.</summary>
        public static int PlanFor(BoatVisualDef visual, float farMetres, CabinLightSwitchSpot[] into)
        {
            if (visual == null || !visual.HasInterior()) return 0;
            bool hasHelm = TryHelmPoint(visual, out Vector3 helm);
            bool hasDoor = TryDoorPoint(visual.Interior, out Vector3 door);
            return Plan(visual.CabinLightSwitches, hasHelm, helm, hasDoor, door, farMetres, into);
        }

        /// <summary>
        /// Where her helm is, hull-local metres: the deck's imported helm station when it has one, else
        /// the interior's <see cref="EnterHelmAction"/> anchor. False when she carries neither.
        /// </summary>
        public static bool TryHelmPoint(BoatVisualDef visual, out Vector3 helm)
        {
            helm = Vector3.zero;
            if (visual == null) return false;

            BoatDeckDef deck = visual.Deck;
            if (deck != null && deck.HasHelmStation)
            {
                helm = deck.HelmStationLocalMeters;
                return true;
            }

            BoatInteriorDef interior = visual.Interior;
            BoatInteriorAnchor[] anchors = interior != null ? interior.Anchors : null;
            if (anchors == null) return false;
            for (int i = 0; i < anchors.Length; i++)
            {
                BoatInteriorAnchor a = anchors[i];
                if (a == null || !string.Equals(a.Action, EnterHelmAction, System.StringComparison.Ordinal))
                    continue;
                helm = a.ReachPoint;
                return true;
            }
            return false;
        }

        /// <summary>Her main entry door's threshold, hull-local metres. False when the interior names no
        /// main door.</summary>
        public static bool TryDoorPoint(BoatInteriorDef interior, out Vector3 door)
        {
            door = Vector3.zero;
            if (interior == null || interior.Door == null || string.IsNullOrEmpty(interior.Door.Id)) return false;
            door = interior.Door.ThresholdPoint;
            return true;
        }

        /// <summary>
        /// The setting a press leaves behind. On and Off swap; Auto flips what is showing now, and reads
        /// as dark when nobody can say (no lights fitted), so the first press on an unknown cabin turns
        /// it on.
        /// </summary>
        public static CabinLightSwitchSetting Toggled(CabinLightSwitchSetting current, bool litKnown, bool litNow)
        {
            switch (current)
            {
                case CabinLightSwitchSetting.On:  return CabinLightSwitchSetting.Off;
                case CabinLightSwitchSetting.Off: return CabinLightSwitchSetting.On;
                default: return litKnown && litNow ? CabinLightSwitchSetting.Off : CabinLightSwitchSetting.On;
            }
        }

        /// <summary>The fixture id for one switch: <c>fixture.boat.&lt;interior id&gt;.light_switch.&lt;spot id&gt;</c>.</summary>
        public static string FixtureId(string interiorId, string spotId)
            => $"fixture.boat.{interiorId}.light_switch.{spotId}";
    }
}
