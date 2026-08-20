#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;   // BoatOwnerDef / BoatMoorage / MooredBoat
using HiddenHarbours.Core;    // ITidalTerrain — what the float berths are measured against

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
    ///
    /// <para><b>⭐ TWO MOORINGS, ONE PLACER (S3).</b> The owner's photograph shows small craft on the
    /// float fingers in the middle of the bullpen and working boats against the tall quay walls, so an
    /// owner's <c>BerthIndex</c> is an index into whichever table her <c>Moorage</c> names. Both paths
    /// are the same three lines — a position, a heading, a <see cref="MooredBoat"/> carrying her Def —
    /// because the float changes WHERE she lies and nothing at all about how she is drawn: a boat on the
    /// float rides the same published wave field, settles at her own waterline and is skinned at wake by
    /// the same skinner. The float's deck rides the tide; the boats beside it were always going to.</para>
    ///
    /// <para><b>⚠ AND THE FLOAT HAS A GATE THE WALL DOES NOT.</b> The wall's berths are 2 m off a face
    /// built out onto the ruled −1.6 m shoal and they BARE at spring low, deliberately — that is the
    /// harbour's teeth. The float's berths are on the bank of S1's cut, where the water is real but
    /// finite, so a boat authored there is checked against the water MEASURED at her own berth and
    /// REFUSED if she draws more than it carries. Refused rather than nudged, for the same reason a
    /// double-booked berth is: a boat quietly moved to somewhere she fits is a boat nobody notices is
    /// in the wrong place.</para>
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

        /// <summary>
        /// The watertight half-beam of an owner's hull, in metres, or <b>0</b> when she has none to
        /// measure — a boat with no hull mesh (sprite-only art) or no boat at all.
        ///
        /// <para>Zero means "unknown", never "no width": every caller here treats it as a gap to REPORT
        /// rather than a pass, which is the same reading <c>NineMileCreekChannelTests</c> gives it when
        /// it pins the region's widest-beam constant.</para>
        /// </summary>
        public static float HalfBeamOf(BoatOwnerDef owner)
        {
            HullMeshDef mesh = owner != null && owner.Boat != null && owner.Boat.Visual != null
                ? owner.Boat.Visual.HullMesh
                : null;
            return mesh != null ? Mathf.Max(0f, mesh.WatertightHalfBeamMeters) : 0f;
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
        /// Moor the fleet — the working boats along the quay wall, the small craft alongside the float.
        /// Returns how many boats were placed.
        ///
        /// <para>Null-tolerant, the region's rule throughout: an owner with no boat is skipped with a
        /// warning and the rest still moor. An owner authored into the player's berth, into a berth this
        /// wharf does not have, into one another owner already holds, or (at the float) into water her
        /// hull cannot float in, is REFUSED rather than nudged — every one of those is an authoring bug,
        /// and quietly moving the boat is how it survives to a playtest.</para>
        /// </summary>
        /// <param name="terrain">The region's authored terrain. Used to MEASURE the float berths: which
        /// side of the finger carries more water, and whether it carries enough for the boat authored
        /// there. The wall berths need none of it — they are a plan line, and they bare by ruling.</param>
        public static int Place(ITidalTerrain terrain)
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
            float wallHeading = MooredHeadingDegrees();
            float floatHeading = NineMileCreekWharf.FloatMooredHeadingDegrees();

            // ⚠ ONE DICTIONARY PER TABLE, and it has to be: wall berth 3 and float berth 3 are two
            // different places, and a single map keyed on the bare index would refuse the second boat as
            // a double-booking of a berth she is nowhere near.
            var wallTaken = new Dictionary<int, BoatOwnerDef>();
            var floatTaken = new Dictionary<int, BoatOwnerDef>();
            int wallPlaced = 0, floatPlaced = 0;

            foreach (var owner in owners)
            {
                bool atFloat = owner.LiesAtTheFloat;
                var taken = atFloat ? floatTaken : wallTaken;
                int berths = atFloat ? NineMileCreekWharf.FloatBerthCount
                                     : NineMileCreekMainland.BerthCount;
                string where = atFloat ? "the floating dock" : "the quay wall";

                if (!atFloat && owner.BerthIndex == playerBerth)
                {
                    Debug.LogError(
                        $"[NineMileCreekMooredFleet] '{owner.Id}' is authored into wall berth " +
                        $"{owner.BerthIndex}, which is the berth the PLAYER docks in (derived from the " +
                        "region's dock zone). Refused — she would arrive on top of him. Move the owner, " +
                        "not the dock.");
                    continue;
                }

                if (owner.BerthIndex < 0 || owner.BerthIndex >= berths)
                {
                    Debug.LogError(
                        $"[NineMileCreekMooredFleet] '{owner.Id}' is authored into berth " +
                        $"{owner.BerthIndex} at {where}, and {where} has {berths}. Refused rather than " +
                        "clamped: a clamped berth is two boats in one place. ⚠ The two tables are " +
                        "different lengths — moving an owner between them is a Moorage edit AND a " +
                        "BerthIndex edit, never just the one.");
                    continue;
                }

                if (taken.TryGetValue(owner.BerthIndex, out BoatOwnerDef sitting))
                {
                    Debug.LogError(
                        $"[NineMileCreekMooredFleet] '{owner.Id}' and '{sitting.Id}' are both authored " +
                        $"into berth {owner.BerthIndex} at {where}. Refused the second — rafting two deep " +
                        "is a real thing a wharf does, but it is a layout decision and not a collision " +
                        "to absorb.");
                    continue;
                }

                // ⭐ THE FLOAT'S OWN GATE. A hull deeper than the water measured at her berth would be
                // drawn floating on ground she is sitting on — the same defect the wharf's shoal test
                // catches for the wall, moved to where the float's boats actually lie. It is checked
                // HERE rather than only in a content test because the number is a terrain measurement:
                // a paint pass that shoals the bank must refuse the boat on the spot, not on the next
                // test run.
                if (atFloat && owner.Boat != null)
                {
                    float carries = NineMileCreekWharf.DeepestDraughtAFloatBerthCarries(
                        owner.BerthIndex, terrain);
                    if (owner.Boat.DraughtMeters > carries)
                    {
                        float depth = NineMileCreekWharf.FloatBerthDepthAtSpringLow(
                            owner.BerthIndex, terrain);
                        Debug.LogError(
                            $"[NineMileCreekMooredFleet] '{owner.Id}' keeps '{owner.Boat.Id}', which " +
                            $"draws {owner.Boat.DraughtMeters:0.00} m, and is authored into float berth " +
                            $"{owner.BerthIndex}. That berth measures {depth:0.00} m at spring low, so it " +
                            $"carries {carries:0.00} m of draught once the region's own " +
                            $"{NineMileCreekMainland.ChannelKeelClearanceMetres:0.00} m keel clearance is " +
                            "taken off. Refused: she would be drawn floating on ground she is sitting " +
                            "on. The float is for SMALL CRAFT — a working hull belongs against the wall, " +
                            "where the fleet lies and the shoal is the ruled gate.");
                        continue;
                    }
                }

                // ⭐ …AND THE FLOAT'S SECOND GATE, which is about SIZE and not water. The depth gate
                // above cannot refuse a working hull at the meander's deepest reach, because there the
                // lane really does carry her — but she still does not belong alongside a 2.4 m pontoon,
                // and the reason is geometric: a hull whose half-beam exceeds the standoff is drawn
                // overlapping the planking she is tied to. ⚠ A SPRITE-ONLY boat has no hull mesh and so
                // no measured beam (Bernard's skiff is the standing example); she is admitted with the
                // gap NAMED rather than refused on a number nobody has, the same way the channel tests
                // handle her.
                if (atFloat && owner.Boat != null)
                {
                    float halfBeam = HalfBeamOf(owner);
                    float widest = NineMileCreekWharf.WidestHalfBeamAFloatBerthCarries;
                    if (halfBeam > widest)
                    {
                        Debug.LogError(
                            $"[NineMileCreekMooredFleet] '{owner.Id}' keeps '{owner.Boat.Id}', whose hull " +
                            $"is {halfBeam * 2f:0.00} m in the beam, and is authored into float berth " +
                            $"{owner.BerthIndex}. A boat lies {widest:0.00} m off the finger's edge, so " +
                            $"anything over {widest * 2f:0.00} m in the beam is drawn lying ON the dock " +
                            "rather than alongside it. Refused: the float is for SMALL CRAFT, and this " +
                            "is what that sentence measures. Her place is against the wall, with the " +
                            "working fleet.");
                        continue;
                    }
                    if (halfBeam <= 0f)
                        Debug.LogWarning(
                            $"[NineMileCreekMooredFleet] '{owner.Id}' keeps a SPRITE-ONLY boat with no " +
                            "hull mesh, so she has no measured beam and the float's size gate cannot " +
                            "judge her. Moored anyway — she is a small craft by every other reading — " +
                            "but the gap is real: bake her a hull and the gate starts covering her.");
                }

                taken[owner.BerthIndex] = owner;

                Vector2 at = atFloat
                    ? NineMileCreekWharf.FloatBerthPos(owner.BerthIndex, terrain)
                    : NineMileCreekMainland.BerthPos(owner.BerthIndex);

                var go = new GameObject($"Moored_{owner.Id}");
                go.transform.SetParent(root.transform, worldPositionStays: false);
                go.transform.position = new Vector3(at.x, at.y, 0f);
                go.AddComponent<MooredBoat>().Configure(owner, atFloat ? floatHeading : wallHeading);
                if (atFloat) floatPlaced++; else wallPlaced++;
            }

            int placed = wallPlaced + floatPlaced;
            Debug.Log(
                $"[NineMileCreekMooredFleet] Moored {placed} of {owners.Count} owner(s) at TWO moorings, " +
                "the way the photograph shows them. " +
                $"ALONG THE WALL: {wallPlaced} working boat(s), bow-on {wallHeading:0}° toward the " +
                $"harbour mouth, leaving berth {playerBerth} clear for the player (derived from the dock " +
                $"zone at {NineMileCreekMainland.DockZonePos}); those berths BARE at spring low, by " +
                "ruling, which is why the walls stand tall over them at low water. " +
                $"AT THE FLOAT: {floatPlaced} small craft of {NineMileCreekWharf.FloatBerthCount} berth(s), " +
                $"bow-on {floatHeading:0}°, lying {NineMileCreekWharf.FloatBerthOffsetMetres:0.0} m off " +
                "the finger's centre-line on whichever side MEASURES deeper — the lane meanders across " +
                "her, so that is not one side for the whole run. " +
                "The hulls are NOT drawn here and must not be: the mesh path is chosen live, per run, by " +
                "the skinner — this places the boats and MooredBoat skins them on wake, so the committed " +
                "scene never bakes the sprite fallback. Each rides the PUBLISHED wave field through " +
                "BoatWaveMotion and settles at her own waterline. No crews' routines, by ruling: a " +
                "skipper stands on each deck and nothing moves. " +
                $"⚠ THE REGISTER IS FULL AT {owners.Count}: the yard walks " +
                $"{NineMileCreekMainland.OwnerShedLotCount} shed lots and every owner has one, so a " +
                "further boat on the float needs GROUND on the spit before it needs an asset.");
            return placed;
        }
    }
}
#endif
