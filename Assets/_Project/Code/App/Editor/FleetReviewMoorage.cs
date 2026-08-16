#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE REVIEW ANCHORAGE</b> — the owner's ruling (2026-08-14): <i>every boat model must be
    /// VISIBLE in game</i>, moored or at anchor, explicitly not pilotable and not purchasable. This is
    /// the twenty-three hulls of the 2026-08-12 fleet pack lying at anchor off Nine Mile Creek, so he
    /// can go and look at them.
    ///
    /// <para><b>⭐ THIS IS NOT THE WORKING FLEET, and the two must not be confused.</b>
    /// <see cref="NineMileCreekMooredFleet"/> moors seven OWNED boats against the wharf wall at their
    /// authored berths — that is region content, it has paint schemes and skippers, and this change
    /// does not touch it. These twenty-three are ART ON THE WATER: no owner, no skipper, no paint, no
    /// berth. They are here to be looked at and nothing else.</para>
    ///
    /// <para><b>⭐ THE BUILDER PLACES, IT DOES NOT DRAW</b> — inherited whole from the working fleet,
    /// and it is the reason this reuses <see cref="MooredBoat"/> rather than instantiating hulls. The
    /// mesh path is registered at runtime by <c>IsoFacetHullPresentationService</c> and edit-time
    /// builders deliberately do not register it, so a builder that drew these itself would serialise
    /// the SPRITE FALLBACK into the committed scene and the whole anchorage would silently never be
    /// mesh — twenty-three boats, in exactly the right places, all of them the wrong renderer. See
    /// <see cref="MooredBoat"/>'s own note.</para>
    ///
    /// <para><b>Why a visual and not an owner.</b> These hulls have a <c>BoatVisualDef</c> and no
    /// <c>BoatHullDef</c> — the hull defs are the next phase, and they are what makes a boat
    /// pilotable. A <c>BoatOwnerDef</c> names a <c>BoatHullDef</c>, so inventing owners would have
    /// meant inventing hull defs, i.e. building that phase early to satisfy a display.
    /// <see cref="MooredBoat.ConfigureForReview"/> is the seam that avoids it.</para>
    /// </summary>
    public static class FleetReviewMoorage
    {
        /// <summary>The root the whole anchorage hangs under.</summary>
        public const string RootName = "FleetReviewAnchorage";

        /// <summary>
        /// <b>Every hull lies BROADSIDE to the camera, and that is the whole layout decision.</b>
        ///
        /// <para>Bow east means her length runs across the screen and her waterline is presented as one
        /// long unbroken line rather than foreshortened to a point. It is the same reasoning
        /// <see cref="HiddenHarbours.Tools.RigBaking.RigAzimuthProbe"/> gives for probing at a quarter
        /// turn — "the hull is fully broadside, which is exactly where the principal axis is longest
        /// and least ambiguous". The owner is here to judge how these boats SIT IN THE WATER; bow-on
        /// would hide the one thing he came to see.</para>
        /// </summary>
        public const float HeadingDegrees = 90f;

        /// <summary>One hull at anchor: which art, where, and how far apart she must stay from her
        /// neighbours. Positions are AUTHORED rather than computed from a formula, because the point
        /// of the anchorage is that a human can move one boat that reads badly without re-deriving a
        /// grid.</summary>
        public readonly struct Berth
        {
            /// <summary>The <c>BoatVisualDef</c> id, e.g. <c>visual.zodiac_frc_iso</c>.</summary>
            public readonly string VisualId;
            /// <summary>World metres, the hull's pivot — her waterline amidships.</summary>
            public readonly Vector2 At;
            /// <summary>Her LOA in metres, for the spacing guard. Labels and geometry only.</summary>
            public readonly float LoaMetres;
            /// <summary>Human-readable, for log lines. Never parsed.</summary>
            public readonly string Label;

            public Berth(string visualId, float x, float y, float loa, string label)
            {
                VisualId = visualId; At = new Vector2(x, y); LoaMetres = loa; Label = label;
            }
        }

        /// <summary>
        /// <b>The anchorage, laid out on MEASURED water.</b>
        ///
        /// <para>Nine Mile Creek's own terrain was probed before a single position was written
        /// (<c>MainlandTidalTerrain.ElevationAt</c>, 2026-08-16): the wharf basin is a flat −1.60 m
        /// shelf from y≈45 to the wall at y=85, there is LAND at x 120–160 / y 35–40, and the water
        /// opens to −3.8 m at x≈200 and a flat <b>−6.00 m</b> from x≈240 across y 25–70. So the
        /// anchorage sits east of the basin in that −6 m water, which is the only part of this region
        /// deep enough to ride a 27.4 m boat without her looking beached.</para>
        ///
        /// <para><b>Order is the order he meets them.</b> Sailing out from the wharf (x≈130, y=85) he
        /// comes on the two battlewagons first — the hulls whose waterline he owes a ruling on — then
        /// the small craft, then the eighteen lobster boats ranged away east. Columns step in x by
        /// more than the longest hull in them; rows step in y by more than any beam.</para>
        ///
        /// <para>⚠️ Every claim above is ASSERTED, not trusted: <c>FleetReviewMoorageTests</c> re-samples
        /// the terrain at each position and fails on any berth that is not in deep enough water, and on
        /// any two hulls closer than their lengths allow.</para>
        /// </summary>
        public static readonly IReadOnlyList<Berth> Anchorage = new[]
        {
            // ---- the two battlewagons: nearest the wharf, and given a column to themselves ---------
            // He owes their waterline a ruling (drafts 0.67 / 1.13 are an authored judgment call, not
            // a derivation), so they get the most room and the shortest sail.
            new Berth("visual.sport_fisher_skybridge_iso",   262f, 68f, 27.4f, "sport fisher 90' skybridge"),
            new Berth("visual.sport_fisher_convertible_iso", 262f, 40f, 16.2f, "sport fisher 53' convertible"),

            // ---- the small craft ------------------------------------------------------------------
            new Berth("visual.zodiac_hurricane_iso",  312f, 68f,  7.28f, "zodiac hurricane"),
            new Berth("visual.zodiac_frc_iso",        312f, 56f,  6.66f, "zodiac frc"),
            new Berth("visual.sport_skiff_mk2_iso",   312f, 44f,  7.0f,  "sport skiff Mk2"),

            // ---- the eighteen lobster boats, ranged away east ---------------------------------------
            // Read in the rig's own axis order (size × style × region) so this list, the fleet table
            // and both authored tables all diff the same way.
            new Berth("visual.lobster_inshore_open_northumberland_iso",     356f, 70f,  8.6f, "lobster inshore/open/nor"),
            new Berth("visual.lobster_inshore_open_fundy_iso",              356f, 56f,  8.6f, "lobster inshore/open/fundy"),
            new Berth("visual.lobster_inshore_open_newfoundland_iso",       356f, 42f,  8.6f, "lobster inshore/open/nfld"),
            new Berth("visual.lobster_inshore_hardtop_northumberland_iso",  356f, 28f,  8.6f, "lobster inshore/hardtop/nor"),

            new Berth("visual.lobster_inshore_hardtop_fundy_iso",           384f, 70f,  8.6f, "lobster inshore/hardtop/fundy"),
            new Berth("visual.lobster_inshore_hardtop_newfoundland_iso",    384f, 56f,  8.6f, "lobster inshore/hardtop/nfld"),
            new Berth("visual.lobster_standard_open_northumberland_iso",    384f, 42f, 12.0f, "lobster standard/open/nor"),
            new Berth("visual.lobster_standard_open_fundy_iso",             384f, 28f, 12.0f, "lobster standard/open/fundy"),

            new Berth("visual.lobster_standard_open_newfoundland_iso",      416f, 70f, 12.0f, "lobster standard/open/nfld"),
            new Berth("visual.lobster_standard_hardtop_northumberland_iso", 416f, 56f, 12.0f, "lobster standard/hardtop/nor"),
            new Berth("visual.lobster_standard_hardtop_fundy_iso",          416f, 42f, 12.0f, "lobster standard/hardtop/fundy"),
            new Berth("visual.lobster_standard_hardtop_newfoundland_iso",   416f, 28f, 12.0f, "lobster standard/hardtop/nfld"),

            new Berth("visual.lobster_offshore_open_northumberland_iso",    452f, 70f, 14.6f, "lobster offshore/open/nor"),
            new Berth("visual.lobster_offshore_open_fundy_iso",             452f, 56f, 14.6f, "lobster offshore/open/fundy"),
            new Berth("visual.lobster_offshore_open_newfoundland_iso",      452f, 42f, 14.6f, "lobster offshore/open/nfld"),
            new Berth("visual.lobster_offshore_hardtop_northumberland_iso", 452f, 28f, 14.6f, "lobster offshore/hardtop/nor"),

            new Berth("visual.lobster_offshore_hardtop_fundy_iso",          488f, 70f, 14.6f, "lobster offshore/hardtop/fundy"),
            new Berth("visual.lobster_offshore_hardtop_newfoundland_iso",   488f, 56f, 14.6f, "lobster offshore/hardtop/nfld"),
        };

        /// <summary>
        /// <b>Which hulls this anchorage OUGHT to hold, read from the fleet catalog.</b>
        ///
        /// <para>Deliberately NOT the same source as <see cref="Anchorage"/>, and that is the entire
        /// point of it existing. The anchorage above is a hand-authored LAYOUT — where each boat lies,
        /// which is a judgment nobody can compute. This is a catalog READ: every hull the 2026-08-12
        /// fleet pack added, taken from the tables the bake itself is driven by. A test comparing the
        /// two catches the failure neither can catch alone — a hull that gets baked and never
        /// anchored, which is a boat the owner asked to see and cannot.</para>
        ///
        /// <para>The eleven hulls baked before the pack are excluded on purpose: they are already in
        /// the game, and re-mooring them here would make the anchorage a fleet review rather than a
        /// review of what this arc added.</para>
        /// </summary>
        public static IEnumerable<string> FleetPackVisualIds()
        {
            foreach (LobsterVariant v in LobsterVariantFleet.All) yield return v.VisualId;
            foreach (ZodiacBuild b in ZodiacFleet.All) yield return b.VisualId;
            foreach (SportFisherHull h in SportFisherFleet.All) yield return h.VisualId;
            // One hull per rig, so she has no fleet table of her own to enumerate; her row in
            // HullMeshFleet.OneHullPerRig is the source, and this is her visual id from it.
            yield return HullMeshFleet.Get("sportSkiffMk2").VisualIds.FirstOrDefault()
                         ?? "visual.sport_skiff_mk2_iso";
        }

        /// <summary>The least water a hull may lie in, over her own resting draft. Two metres is not a
        /// pilotage rule — nothing sails here — it is "deep enough that she reads as afloat rather than
        /// aground" with room for the tide, which swings ±2.2 m at springs.</summary>
        public const float MinClearanceMetres = 2.0f;

        /// <summary>Where every visual this anchorage names is expected to live.</summary>
        public const string VisualsFolder = "Assets/_Project/Data/Boats/Visuals";

        /// <summary>Load a berth's art, or null. Kept here so the placer and the tests resolve ids the
        /// same way.</summary>
        public static BoatVisualDef Resolve(in Berth berth)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:BoatVisualDef", new[] { VisualsFolder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (def != null && def.Id == berth.VisualId) return def;
            }
            return null;
        }

        /// <summary>
        /// Anchor the review fleet. Returns how many hulls were placed.
        ///
        /// <para>Null-tolerant, the region's rule throughout: a berth whose art has not baked is warned
        /// and skipped, and the rest still anchor.</para>
        /// </summary>
        public static int Place()
        {
            var root = new GameObject(RootName);
            int placed = 0;

            foreach (Berth berth in Anchorage)
            {
                BoatVisualDef visual = Resolve(berth);
                if (visual == null)
                {
                    Debug.LogWarning(
                        $"[FleetReviewMoorage] '{berth.VisualId}' ({berth.Label}) is not under " +
                        $"{VisualsFolder}, so her anchorage is empty. Bake her hull and re-run — this " +
                        "is an authoring gap, not a drawing one.");
                    continue;
                }

                var go = new GameObject($"Review_{berth.Label.Replace('/', '_').Replace(' ', '_')}");
                go.transform.SetParent(root.transform, worldPositionStays: false);
                go.transform.position = new Vector3(berth.At.x, berth.At.y, 0f);
                go.AddComponent<MooredBoat>().ConfigureForReview(visual, HeadingDegrees);
                placed++;
            }

            Debug.Log(
                $"[FleetReviewMoorage] Anchored {placed} of {Anchorage.Count} review hull(s) east of " +
                $"the basin in the region's −6 m water, all bow-on {HeadingDegrees:0}° so their " +
                "waterlines read broadside. These are ART, not content: no owner, no skipper, no paint " +
                "and no BoatHullDef, so none of them is pilotable or purchasable. The hulls are NOT " +
                "drawn here and must not be — MooredBoat skins them on wake, so the committed scene " +
                "never bakes the sprite fallback.");
            return placed;
        }
    }
}
#endif
