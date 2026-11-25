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

        // ---- a spot nothing else takes the press at (2026-11-25) ---------------------------------------

        /// <summary>How finely a cabin wall is walked looking for a spot. Structural sampling, not feel:
        /// finer only costs build time.</summary>
        public const float SpotSampleStepMetres = 0.25f;

        /// <summary>How many headings the on-screen clearance is checked at. Reach is measured on screen,
        /// and the drawn hull foreshortens with her heading, so a spot must be clear at all of them.</summary>
        public const int ClearanceHeadingSamples = 24;

        /// <summary>
        /// <b>Move each rule-placed switch to a spot on her cabin wall where it is alone in reach</b>: on a
        /// cabin level's outline (the foot of a bulkhead), not inside anything standing on that sole, at
        /// least <paramref name="helmClearMetres"/> from her helm station and outside every door's reach,
        /// at every heading. The nearest such spot to the switch's rule point wins (her helm's or her
        /// door's), so each switch stays by what it is named for. A switch with no clear spot keeps its
        /// rule point. An authored list is never moved.
        ///
        /// <para>Why: the switch is on the last rung of the press, so where the helm or a door is in reach
        /// it never answers. Standing at a clear spot, it is the one thing E can mean.</para>
        /// </summary>
        /// <returns>How many of the first <paramref name="count"/> switches stand on a clear spot.</returns>
        public static int PlaceClear(BoatVisualDef visual, float helmClearMetres, CabinLightSwitchSpot[] spots,
                                     int count)
        {
            if (visual == null || spots == null || visual.Interior == null) return 0;
            BoatInteriorDef interior = visual.Interior;
            bool hasHelm = TryHelmPoint(visual, out Vector3 helm);
            float elevation = BoatInteriorInstaller.BakeElevationDegrees(visual);
            bool authored = HasUsableAuthoredEntry(visual.CabinLightSwitches);

            int clear = 0;
            for (int i = 0; i < count && i < spots.Length; i++)
            {
                CabinLightSwitchSpot s = spots[i];
                if (authored)
                {
                    if (IsClear(interior, s.LocalMetres, hasHelm, helm, helmClearMetres, elevation)) clear++;
                    continue;
                }
                if (!TryClearSpot(interior, s.LocalMetres, hasHelm, helm, helmClearMetres, elevation,
                                  out Vector3 spot))
                    continue;
                spots[i] = new CabinLightSwitchSpot(s.Id, s.Role, spot);
                clear++;
            }
            return clear;
        }

        /// <summary>The nearest clear wall spot to <paramref name="near"/>, hull-local metres (z = that
        /// level's sole). False when no cabin wall has one.</summary>
        public static bool TryClearSpot(BoatInteriorDef interior, Vector3 near, bool hasHelm, Vector3 helm,
                                        float helmClearMetres, float elevationDegrees, out Vector3 spot)
        {
            spot = near;
            if (interior == null || interior.Levels == null) return false;
            bool found = false;
            float best = float.MaxValue;
            for (int l = 0; l < interior.Levels.Length; l++)
            {
                BoatInteriorLevel level = interior.Levels[l];
                if (level == null || !level.IsUsable()) continue;
                Vector2[] outline = level.Outline;
                for (int e = 0; e < outline.Length; e++)
                {
                    Vector2 a = outline[e];
                    Vector2 b = outline[(e + 1) % outline.Length];
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / SpotSampleStepMetres));
                    for (int k = 0; k < steps; k++)
                    {
                        Vector2 q = Vector2.Lerp(a, b, (float)k / steps);
                        var p = new Vector3(q.x, q.y, level.SoleZMeters);
                        float d = (p - near).sqrMagnitude;
                        if (d >= best) continue;
                        if (InsideAnObstruction(level, q)) continue;
                        if (!IsClear(interior, p, hasHelm, helm, helmClearMetres, elevationDegrees)) continue;
                        best = d;
                        spot = p;
                        found = true;
                    }
                }
            }
            return found;
        }

        /// <summary>Is <paramref name="p"/> (hull-local) out of the helm's clearance and every door's reach,
        /// on screen, at every sampled heading?</summary>
        public static bool IsClear(BoatInteriorDef interior, Vector3 p, bool hasHelm, Vector3 helm,
                                   float helmClearMetres, float elevationDegrees)
        {
            float helmClear = Mathf.Max(0f, helmClearMetres);
            for (int k = 0; k < ClearanceHeadingSamples; k++)
            {
                float heading = k * 360f / ClearanceHeadingSamples;
                Vector2 at = OnScreen(p, heading, elevationDegrees);
                if (hasHelm && (at - OnScreen(helm, heading, elevationDegrees)).sqrMagnitude <= helmClear * helmClear)
                    return false;
                if (interior == null) continue;
                if (InADoorsReach(interior.Door, at, heading, elevationDegrees)) return false;
                BoatInteriorDoor[] more = interior.AdditionalDoors;
                if (more == null) continue;
                for (int i = 0; i < more.Length; i++)
                    if (InADoorsReach(more[i], at, heading, elevationDegrees)) return false;
            }
            return true;
        }

        private static bool InADoorsReach(BoatInteriorDoor door, Vector2 at, float heading, float elevation)
        {
            if (door == null || string.IsNullOrEmpty(door.Id)) return false;
            float reach = BoatInteriorInstaller.ReachMetres(door);
            return (at - OnScreen(door.ThresholdPoint, heading, elevation)).sqrMagnitude <= reach * reach;
        }

        private static Vector2 OnScreen(Vector3 local, float heading, float elevation)
            => DeckAreaMath.DeckToWorld(new Vector2(local.x, local.y), local.z, heading, elevation);

        private static bool InsideAnObstruction(BoatInteriorLevel level, Vector2 q)
        {
            BoatInteriorObstruction[] obs = level.Obstructions;
            if (obs == null) return false;
            for (int i = 0; i < obs.Length; i++)
            {
                Vector2[] f = obs[i] != null ? obs[i].Footprint : null;
                if (f == null || f.Length < 3) continue;
                bool inside = false;
                for (int a = 0, b = f.Length - 1; a < f.Length; b = a++)
                {
                    if ((f[a].y > q.y) != (f[b].y > q.y)
                        && q.x < (f[b].x - f[a].x) * (q.y - f[a].y) / (f[b].y - f[a].y) + f[a].x)
                        inside = !inside;
                }
                if (inside) return true;
            }
            return false;
        }

        private static bool HasUsableAuthoredEntry(CabinLightSwitchPlacement[] authored)
        {
            if (authored == null) return false;
            for (int i = 0; i < authored.Length; i++)
                if (authored[i] != null && !string.IsNullOrEmpty(authored[i].Id)) return true;
            return false;
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
