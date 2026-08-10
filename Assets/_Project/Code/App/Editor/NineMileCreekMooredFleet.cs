#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;   // BoatOwnerDef / MooredBoat

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE FLEET LYING AT NINE MILE CREEK</b> — the owner's ruling (2026-08-10): the boats are
    /// <i>rendered into the sea as floating assets at their berths</i>, and the ambient fleet uses the
    /// mesh assets with NPC workers/captains aboard.
    ///
    /// <para><b>⭐ THE BUILDER PLACES, IT DOES NOT DRAW.</b> Each berth gets one
    /// <see cref="MooredBoat"/> carrying its owner's Def, and the hull is skinned at RUNTIME. That is not
    /// a style choice — <c>IsoFacetHullPresentationService</c> registers the mesh path at
    /// <c>RuntimeInitializeOnLoadMethod</c> and states in its own words that <i>edit-time scene BUILDERS
    /// deliberately do not</i>, so that a built scene never bakes a renderer whose setup is runtime-owned.
    /// A builder that instantiated the hulls itself would serialise the SPRITE fallback into
    /// <c>NineMileCreek.unity</c> and the fleet would silently never be mesh, with the right number of
    /// boats in exactly the right places. See <see cref="MooredBoat"/> for the whole argument.</para>
    ///
    /// <para><b>⚠ THE PLAYER'S BERTH IS NOT SOMEBODY ELSE'S</b>, and which one that is gets DERIVED
    /// rather than assumed: the region's dock zone is a pure distance test against where the player's
    /// boat parks, so the berth nearest <c>DockZonePos</c> is the one she takes, and no owner may be
    /// authored into it. Moving the dock zone moves the exclusion with it.</para>
    /// </summary>
    public static class NineMileCreekMooredFleet
    {
        /// <summary>The root the whole moored fleet hangs under.</summary>
        public const string RootName = "NineMileCreekFleet";

        /// <summary>Where the region's boat-owner assets live. One entity per file (rule 2).</summary>
        public const string OwnersFolder = "Assets/_Project/Data/Boats/Owners";

        /// <summary>
        /// Which berth the PLAYER takes when she docks — derived from the region's own dock geometry, so
        /// it cannot drift from the thing that actually decides where she parks.
        /// </summary>
        public static int PlayerBerthIndex()
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            var dock = new Vector2(NineMileCreekMainland.DockZonePos.x,
                                   NineMileCreekMainland.DockZonePos.y);

            for (int i = 0; i < NineMileCreekMainland.BerthCount; i++)
            {
                float d = Vector2.Distance(NineMileCreekMainland.BerthPos(i), dock);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// The compass heading a moored hull's bow lies at.
        ///
        /// <para><b>DERIVED, and it has to be:</b> a boat lies PARALLEL to the wall she is tied to, never
        /// across it. The mooring face looks along <c>MooringFaceHeadingDegrees</c> (out over the water),
        /// so the wall itself runs at ninety degrees to that — and of the two ways along it, she points
        /// the way she leaves, toward the harbour mouth. The mouth is the end of the wall FURTHEST from
        /// the shore the wharf is built out from, which is read off the deck rather than typed.</para>
        /// </summary>
        public static float MooredHeadingDegrees()
        {
            Rect deck = NineMileCreekWharf.DeckFootprint();
            // The wall runs east–west; the seaward end is the wharf head, the landward end is the apron.
            // Bow toward the head = compass 90 (east) when the head is the deck's east end.
            bool headIsEast = deck.xMax > NineMileCreekWharf.ApronFootprint().xMax;
            return headIsEast ? 90f : 270f;
        }

        /// <summary>Every owner asset the region ships, in a stable order (by id) so a rebuild does not
        /// reshuffle the fleet.</summary>
        public static List<BoatOwnerDef> LoadOwners()
        {
            var owners = new List<BoatOwnerDef>();
            if (!AssetDatabase.IsValidFolder(OwnersFolder)) return owners;

            foreach (string guid in AssetDatabase.FindAssets("t:BoatOwnerDef", new[] { OwnersFolder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) owners.Add(def);
            }
            return owners.OrderBy(o => o.Id, System.StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Moor the fleet. Returns how many boats were placed.
        ///
        /// <para>Null-tolerant, the region's rule throughout: an owner with no boat is skipped with a
        /// warning and the rest still moor. An owner authored into the player's berth is REFUSED rather
        /// than nudged — two boats in one berth is an authoring bug, and quietly moving one of them is how
        /// it survives to a playtest.</para>
        /// </summary>
        public static int Place()
        {
            var owners = LoadOwners();
            if (owners.Count == 0)
            {
                Debug.LogWarning(
                    $"[NineMileCreekMooredFleet] no BoatOwnerDef assets under {OwnersFolder} — the wharf's " +
                    "berths are empty. The quay, its fittings and its cleats are unaffected; author the " +
                    "owners and re-run.");
                return 0;
            }

            var root = new GameObject(RootName);
            int playerBerth = PlayerBerthIndex();
            float heading = MooredHeadingDegrees();
            var taken = new Dictionary<int, BoatOwnerDef>();
            int placed = 0;

            foreach (var owner in owners)
            {
                if (owner.BerthIndex == playerBerth)
                {
                    Debug.LogError(
                        $"[NineMileCreekMooredFleet] '{owner.Id}' is authored into berth " +
                        $"{owner.BerthIndex}, which is the berth the PLAYER docks in (derived from the " +
                        "region's dock zone). Refused — she would arrive on top of him. Move the owner, " +
                        "not the dock.");
                    continue;
                }

                if (owner.BerthIndex < 0 || owner.BerthIndex >= NineMileCreekMainland.BerthCount)
                {
                    Debug.LogError(
                        $"[NineMileCreekMooredFleet] '{owner.Id}' is authored into berth " +
                        $"{owner.BerthIndex}, and this wharf has {NineMileCreekMainland.BerthCount}. " +
                        "Refused rather than clamped: a clamped berth is two boats in one place.");
                    continue;
                }

                if (taken.TryGetValue(owner.BerthIndex, out BoatOwnerDef sitting))
                {
                    Debug.LogError(
                        $"[NineMileCreekMooredFleet] '{owner.Id}' and '{sitting.Id}' are both authored " +
                        $"into berth {owner.BerthIndex}. Refused the second — rafting two deep is a real " +
                        "thing a wharf does, but it is a layout decision and not a collision to absorb.");
                    continue;
                }
                taken[owner.BerthIndex] = owner;

                Vector2 at = NineMileCreekMainland.BerthPos(owner.BerthIndex);
                var go = new GameObject($"Moored_{owner.Id}");
                go.transform.SetParent(root.transform, worldPositionStays: false);
                go.transform.position = new Vector3(at.x, at.y, 0f);
                go.AddComponent<MooredBoat>().Configure(owner, heading);
                placed++;
            }

            Debug.Log(
                $"[NineMileCreekMooredFleet] Moored {placed} of {owners.Count} owner(s) along the wall, " +
                $"bow-on {heading:0}° toward the harbour mouth, leaving berth {playerBerth} clear for the " +
                $"player (derived from the dock zone at {NineMileCreekMainland.DockZonePos}). The hulls " +
                "are NOT drawn here and must not be: the mesh path is chosen live, per run, by the " +
                "skinner — this places the boats and MooredBoat skins them on wake, so the committed " +
                "scene never bakes the sprite fallback. Each rides the PUBLISHED wave field through " +
                "BoatWaveMotion and settles at her own waterline. No crews' routines, by ruling: a " +
                "skipper stands on each deck and nothing moves.");
            return placed;
        }
    }
}
#endif
