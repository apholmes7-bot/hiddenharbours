#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Art.Editor;          // VillageBuildingCatalog, VillageBuildingKit
using HiddenHarbours.Core;                // ITidalTerrain

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE TWO HOUSES BEHIND THE CREEK</b> — §7.2's "a couple of buildings for flavour", and the last
    /// of it: <i>a working creek, not a town.</i> Two houses set back from the working row, doors turned
    /// to the water, so the yard reads as a place somebody goes home from rather than a set of stalls.
    ///
    /// <para><b>From the BAKED kit, not repainted.</b> The region's two flavour houses were loose
    /// hand-made sprites (<c>NineMileCreekHouseRed/Teal.png</c>) — the "outdated art" plan-to-m1 §7.2
    /// says to <i>replace from the baked rigs rather than repaint by hand</i>. The village building kit
    /// (#352) bakes exactly this: clapboard houses, eight facings, footprint metres published in its own
    /// contract. So they come from there, through
    /// <see cref="VillageBuildingCatalog"/>'s one build path, which is what keeps a building the owner
    /// drags in and a building this file instantiates the same object.</para>
    ///
    /// <para><b>⚠️ WHAT THIS KIT CANNOT DRESS, AND IS NOT ASKED TO.</b> The creek's WORKING buildings —
    /// the bait shed, the fish stall, the dory yard — want <c>wharfBuildingRig</c>'s
    /// <c>netShed</c>/<c>redShed</c>/<c>tealShack</c>/<c>iceHouse</c> presets, which
    /// <c>design/nine-mile-creek-wharf.md</c> §3's build table names for them. <b>That rig has never
    /// been baked</b> (M2-40/46), and the village kit bakes HOUSES: a clapboard farmhouse standing in for
    /// a bait shed would be a worse lie than the greybox sprite it replaced, because it would look
    /// finished. So the working buildings keep their sprites and this file dresses only the two the kit
    /// is genuinely for. Nothing here bakes a rig.</para>
    ///
    /// <para><b>Three things about a kit building this file has to respect (all from #352):</b> the pivot
    /// is the ground CENTRE and nothing may offset it; turning one means swapping the sub-sprite, never
    /// rotating the transform; and its drawn size is not its metric size — the honest number is the
    /// FOOTPRINT, which is what every clearance here is measured against.</para>
    /// </summary>
    public static class NineMileCreekFlavour
    {
        /// <summary>The root both houses hang under.</summary>
        public const string RootName = "CreekHouses";

        /// <summary>
        /// One flavour house: which build from the kit, where it stands, and one line on why it is there.
        /// The keys are the kit's OWN (<c>VillageBuildingKit.M1Set</c>) and are never camel-cased from a
        /// label — a stem that cannot be matched to a build fails silently.
        /// </summary>
        public readonly struct House
        {
            public readonly string Key;
            public readonly Vector2 Position;
            public readonly string Reason;

            public House(string key, Vector3 position, string reason)
            {
                Key = key;
                Position = new Vector2(position.x, position.y);
                Reason = reason;
            }
        }

        /// <summary>
        /// The two. Both are set well back from the x = −8 / −12 working rows, in the empty western half
        /// of the land — there is room there and nothing else wants it, which is the whole reason the
        /// creek can have houses without becoming a town.
        ///
        /// <para>Neither is the school or the general store: a creek of this size has no school, and its
        /// chandlery is already a stall on the working row. The two chosen are the kit's plainest — a red
        /// clapboard saltbox with no porch, and the smallest, most weathered of the three cottages — so
        /// the pair read as houses of different ages rather than a matched development.</para>
        /// </summary>
        public static IReadOnlyList<House> Houses => new[]
        {
            new House("redSaltbox", NineMileCreekBuilder.FlavourHouseRedPos,
                      "north, back of the yard — the plainest thing the kit bakes, which is what a house " +
                      "behind a working wharf is"),

            new House("sageCottage", NineMileCreekBuilder.FlavourHouseTealPos,
                      "south, the same distance back — smaller and more weathered, so the two are not " +
                      "the same house twice"),
        };

        /// <summary>Where their doors point: the middle of the quay. On a working coast a house faces the
        /// water it lives off, and deriving it from the wharf means it stays right if the wharf moves.</summary>
        public static Vector2 FacingTarget => NineMileCreekWharf.DeckFootprint().center;

        // =====================================================================================
        //  GEOMETRY (pure — no assets, no scene, so a test can assert the pair without either)
        // =====================================================================================

        /// <summary>The half-diagonal of a footprint, in metres: the ground a building reserves at ANY
        /// facing. A circle rather than a rectangle for the reason the island's village gives — the
        /// facing is derived, the owner may re-bake with fewer facings, and a quarter-turned building
        /// presents its diagonal. Reserving the diagonal costs a couple of metres and can never be
        /// wrong.</summary>
        public static float FootprintRadiusMetres(VillageBuildingCatalog.Placement p) =>
            p.IsValid
                ? 0.5f * Mathf.Sqrt(p.Entry.footprintWidthMetres * p.Entry.footprintWidthMetres +
                                    p.Entry.footprintLengthMetres * p.Entry.footprintLengthMetres)
                : 0f;

        /// <summary>The lane left between two houses' reserved ground. Wide enough to walk down at the
        /// on-foot framing rather than a token separation.</summary>
        public const float LaneGap = 4f;

        /// <summary>
        /// Which facing turns this house's door toward the quay.
        ///
        /// <para>The turns-of-a-circle derivation lives in <see cref="StPetersVillage.FacingToward"/>. It
        /// is REUSED rather than copied deliberately: it is facing-count agnostic on purpose (the kit
        /// ships 8 and the owner may halve it in a re-bake, which is a bake and not a code change), and a
        /// second copy of that reasoning is exactly the kind of duplicate that goes quietly stale. It is
        /// pure, and it is about a KIT rather than about an island — when a third consumer lands it
        /// belongs on <see cref="VillageBuildingCatalog"/> itself.</para>
        /// </summary>
        public static int FacingFor(VillageBuildingCatalog.Placement placement, House house) =>
            placement.IsValid
                ? StPetersVillage.FacingToward(placement.Entry, house.Position, FacingTarget)
                : 0;

        // =====================================================================================
        //  PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Stand the two houses up. Returns how many were placed.
        ///
        /// <para>Null-tolerant throughout: a build the kit has not baked is SKIPPED with a warning rather
        /// than throwing halfway through a region build, because "declared in the contract" and "has
        /// pixels on disk" are different questions and a partial art state should still place what it
        /// can.</para>
        /// </summary>
        public static int Place(ITidalTerrain terrain, float springHighWater)
        {
            var placements = VillageBuildingCatalog.Scan();
            if (placements.Count == 0)
            {
                Debug.LogWarning(
                    "[NineMileCreekFlavour] no village buildings in the contract — the creek has no houses " +
                    "behind it. The kit bakes to order: run Hidden Harbours ▸ Art ▸ Bake Village Buildings.");
                return 0;
            }

            var root = new GameObject(RootName);
            int placed = 0;
            var report = new List<string>();

            foreach (var house in Houses)
            {
                var placement = VillageBuildingCatalog.Find(house.Key);
                if (!placement.IsValid)
                {
                    Debug.LogWarning(
                        $"[NineMileCreekFlavour] '{house.Key}' is not in the building contract, so the creek " +
                        $"is missing the one that would stand {house.Reason}. Re-bake the kit rather than " +
                        "re-siting the pair around the gap.");
                    continue;
                }

                int facing = FacingFor(placement, house);
                Sprite sprite = VillageBuildingCatalog.LoadFacing(placement, facing);
                if (sprite == null)
                {
                    Debug.LogWarning(
                        $"[NineMileCreekFlavour] {house.Key} facing {facing} has no sprite — its sheet is " +
                        $"missing or unsliced ({placement.SheetPath}). Skipping it rather than placing a blank.");
                    continue;
                }

                // ⚠️ Checked against the AUTHORED TERRAIN, not against the constants — the #345 lesson.
                // Loud rather than silent: a house standing in the sea is an authoring bug to fix, not a
                // building to quietly drop.
                if (terrain != null)
                {
                    float ground = terrain.ElevationAt(house.Position);
                    if (ground <= springHighWater)
                        Debug.LogError(
                            $"[NineMileCreekFlavour] {house.Key} is sited at {house.Position} where the " +
                            $"ground is {ground:0.00} m — at or below spring high water " +
                            $"({springHighWater:0.00} m). A house does not flood. Move the site; do not " +
                            "lower the tide.");
                }

                var go = new GameObject(house.Key);
                go.transform.SetParent(root.transform, worldPositionStays: false);

                // The pivot IS the ground centre, so the site position is the position. No offset.
                go.transform.position = new Vector3(house.Position.x, house.Position.y, 0f);

                // Configure owns the renderer, the scale, the pinned rotation, the YSortSprite AND the
                // sorting order (#352 measured a seeded order coming back changed once YSortSprite
                // recomputes from world Y), so nothing here re-derives any of it.
                VillageBuildingCatalog.Configure(go, placement, sprite);

                // Solid, like every other building in this region — the creek's own standing rule after
                // the owner's "boat sails through the buildings" note. The size is the kit's published
                // FOOTPRINT, centred on the ground-centre pivot, so it is the wall box and not the roof.
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(placement.Entry.footprintWidthMetres,
                                       placement.Entry.footprintLengthMetres);
                box.offset = Vector2.zero;

                placed++;
                report.Add($"{house.Key} d{facing} at ({house.Position.x:0.#},{house.Position.y:0.#}) " +
                           $"{placement.Entry.footprintWidthMetres:0.#}×" +
                           $"{placement.Entry.footprintLengthMetres:0.#} m");
            }

            Debug.Log(
                $"[NineMileCreekFlavour] Placed {placed} of {Houses.Count} flavour house(s) behind the " +
                $"creek, doors turned to the water: {string.Join(" · ", report)}. The WORKING buildings " +
                "keep their sprites on purpose — the sheds want wharfBuildingRig, which has never been " +
                "baked (M2-40/46), and a clapboard house standing in for a bait shed would look finished " +
                "while being wrong.");

            return placed;
        }
    }
}
#endif
