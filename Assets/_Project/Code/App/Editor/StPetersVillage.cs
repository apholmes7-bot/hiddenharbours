#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HiddenHarbours.Art.Editor;          // VillageBuildingCatalog, VillageBuildingKit
using HiddenHarbours.Core;                // ITidalTerrain

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE VILLAGE ITSELF</b> — the five buildings §5.1 asks for: "three clapboard houses, a one-room
    /// <b>school</b> [where the aunt teaches the compass and hand skills] and a <b>general store</b> [for
    /// basic gear]". The last item on the owner's dressing list, and the one layer on this island that is
    /// AUTHORED rather than scattered.
    ///
    /// <para><b>Why authored.</b> ADR 0002 — author identity, simulate variety. A village's shape IS its
    /// identity: which building you meet first walking up from the spawn, what the school stands apart
    /// from, which house is the last one before the shore. A hash cannot mean any of that. So the sites
    /// are five named constants on <see cref="StPetersBuilder"/> beside the cottage and the spawn they
    /// are placed relative to, and this file is the geometry and the placement pass.</para>
    ///
    /// <para><b>Three things about a building this file has to respect, all from the kit (#352):</b></para>
    /// <list type="bullet">
    ///   <item><b>The pivot is the ground CENTRE, not bottom-centre.</b> The ¾ camera projects the whole
    ///   near half of the footprint below it — 3.3–6.5 m across this set. It is baked into the sliced
    ///   sub-sprite, so the site position IS the middle of the footprint and nothing here offsets it. A
    ///   "correction" to bottom-centre would stand these buildings metres into the dirt.</item>
    ///   <item><b>Turning one means swapping the sub-sprite, never rotating the transform</b> — the art is
    ///   baked at a fixed camera. <see cref="VillageBuildingCatalog.SetFacing"/> is the only way, and
    ///   <see cref="FacingToward"/> is how this file decides which facing to ask for.</item>
    ///   <item><b>Its drawn size is not its metric size.</b> The honest number is the FOOTPRINT, and the
    ///   footprint is what every clearance here is measured against — never the cell, which is up to
    ///   20 m wide for a 7.7 m building because the roof and the diagonal draw outside the wall box.</item>
    /// </list>
    ///
    /// <para><b>⚠️ The footprint is treated as a CIRCLE, deliberately.</b>
    /// <see cref="FootprintRadiusMetres"/> is the half-diagonal, so a site reserves the same ground at
    /// every facing. A rectangle would be tighter and would also be a promise this file cannot keep: the
    /// facing is derived from the green, the owner may re-bake with fewer facings, and a quarter-turned
    /// building presents its diagonal. Reserving the diagonal costs a couple of metres and can never be
    /// wrong.</para>
    /// </summary>
    public static class StPetersVillage
    {
        public const string RootName = "IslandVillage";

        /// <summary>
        /// Clearance (m) a building keeps from the cottage's own footprint. Ginny's cottage is a legacy
        /// single sprite with no contract to ask, so its extent is authored here — generously, because
        /// being wrong about it means a house through the hearth.
        /// </summary>
        public const float CottageFootprintRadius = 8f;

        /// <summary>Gap (m) left between two building footprints — the lane between them. Wide enough to
        /// walk down at the on-foot framing rather than a token separation.</summary>
        public const float LaneGap = 4f;

        /// <summary>Clearance (m) from the cottage props (Ginny, Ned's letter, the freezer). They are
        /// small interactables; they only need to not be inside a wall.</summary>
        public const float PropClearance = 2.5f;

        // =====================================================================================
        //  THE SITES
        // =====================================================================================

        /// <summary>One authored building site: which building, where its footprint centre sits, and one
        /// line on why it is there — the identity a hash could not have produced.</summary>
        public readonly struct Site
        {
            public readonly string Key;
            public readonly Vector2 Position;
            public readonly string Reason;

            public Site(string key, Vector3 position, string reason)
            {
                Key = key;
                Position = new Vector2(position.x, position.y);
                Reason = reason;
            }
        }

        /// <summary>
        /// The village, in the order it reads walking up from the start spawn. The keys are the kit's own
        /// (<c>VillageBuildingKit.M1Set</c>) and are never camel-cased from a label — the same rule the
        /// shrub kit's keys taught: a stem that cannot be matched to a build fails silently.
        /// </summary>
        public static IReadOnlyList<Site> Sites => new[]
        {
            new Site("school", StPetersBuilder.SchoolPos,
                     "west end of the lane — a schoolhouse stands a little apart from the houses"),
            new Site("generalStore", StPetersBuilder.GeneralStorePos,
                     "the lane's centre, where the path up from the spawn meets it: the first door you " +
                     "have a reason to open"),
            new Site("whiteFarmhouse", StPetersBuilder.WhiteFarmhousePos,
                     "closing the lane's east end — the biggest of the five, so it anchors that end"),
            new Site("redSaltbox", StPetersBuilder.RedSaltboxPos,
                     "the green's east side, facing back across it"),
            new Site("sageCottage", StPetersBuilder.SageCottagePos,
                     "south of the green — the last house before the shore"),
        };

        // =====================================================================================
        //  GEOMETRY (pure — no assets, no scene, so a test can assert the village without either)
        // =====================================================================================

        /// <summary>The half-diagonal of a footprint, in metres: the ground a building reserves at ANY
        /// facing. See the class remarks for why this is a circle and not a rectangle.</summary>
        public static float FootprintRadiusMetres(VillageBuildingKit.Entry entry) =>
            entry == null
                ? 0f
                : 0.5f * Mathf.Sqrt(entry.footprintWidthMetres * entry.footprintWidthMetres +
                                    entry.footprintLengthMetres * entry.footprintLengthMetres);

        public static float FootprintRadiusMetres(VillageBuildingCatalog.Placement p) =>
            p.IsValid ? FootprintRadiusMetres(p.Entry) : 0f;

        /// <summary>
        /// Which facing turns <paramref name="entry"/>'s door toward <paramref name="target"/>.
        ///
        /// <para><b>⭐ FACING-COUNT AGNOSTIC ON PURPOSE.</b> The kit ships 8 today and the owner may halve
        /// it — a re-bake, not a code change — so this works in TURNS of a circle rather than in cells:
        /// it takes the bearing from the building to its target, measures it against the facing whose door
        /// already faces the camera (<see cref="VillageBuildingKit.FrontFacing"/>, which the bake MEASURED
        /// from the door anchors rather than declaring), and rounds to the nearest of however many facings
        /// exist. With four facings the doors land on the nearest quarter-turn instead of silently
        /// pointing at a wall.</para>
        ///
        /// <para>The contract's own <c>conventionNote</c> is the other half: the azimuth probe measured the
        /// rig at bake time and the correction is already applied to the sheet, so <b>cell i genuinely
        /// depicts +45°·i</b> — counter-clockwise, and already un-mislabelled. This is the one kit in the
        /// repo where that has been settled at the bake, which is why nothing here compensates for it.</para>
        /// </summary>
        public static int FacingToward(VillageBuildingKit.Entry entry, Vector2 from, Vector2 target)
        {
            if (entry == null || entry.facings <= 0) return 0;

            Vector2 d = target - from;
            if (d.sqrMagnitude < 1e-6f) return VillageBuildingKit.FrontFacing(entry);

            // The front facing points at the camera, i.e. −Y, i.e. −90° in atan2 terms. Measure the
            // desired door direction as a turn CCW from there.
            float degrees = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float fromFront = Mathf.DeltaAngle(-90f, degrees);
            float perCell = 360f / entry.facings;

            int facing = VillageBuildingKit.FrontFacing(entry) + Mathf.RoundToInt(fromFront / perCell);
            return ((facing % entry.facings) + entry.facings) % entry.facings;   // C# % keeps the sign
        }

        /// <summary>The facing a site is placed at — its door turned toward
        /// <see cref="StPetersBuilder.VillageGreen"/>.</summary>
        public static int FacingFor(VillageBuildingCatalog.Placement placement, Site site) =>
            placement.IsValid
                ? FacingToward(placement.Entry, site.Position, StPetersBuilder.VillageGreen)
                : 0;

        /// <summary>
        /// The clearing radius the village ACTUALLY needs, derived from the contract: the furthest any
        /// footprint reaches from <see cref="StPetersBuilder.CottagePos"/>, which is what
        /// <see cref="StPetersWoods.VillageClearingRadius"/> has to cover or trees and shrubs plant
        /// through a wall. Returns 0 when nothing is baked, so a test can say so rather than pass
        /// vacuously.
        /// </summary>
        public static float RequiredClearingRadius()
        {
            float worst = 0f;
            Vector2 cottage = StPetersBuilder.CottagePos;
            foreach (var site in Sites)
            {
                var placement = VillageBuildingCatalog.Find(site.Key);
                if (!placement.IsValid) continue;
                float reach = Vector2.Distance(site.Position, cottage) + FootprintRadiusMetres(placement);
                if (reach > worst) worst = reach;
            }
            return worst;
        }

        // =====================================================================================
        //  PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Stand the village up. Returns how many buildings were placed.
        ///
        /// <para>Every one goes through <see cref="VillageBuildingCatalog.Configure"/> — the kit's ONE
        /// build path, the same rule <c>AcadianTreeCatalog.Configure</c> established for trees: a building
        /// the owner drags in and one this builder instantiates have to be the same object, or the scene
        /// and the palette drift apart.</para>
        ///
        /// <para>Null-tolerant throughout: a build the kit has not baked is SKIPPED with a warning rather
        /// than throwing halfway through a region build, because "declared in the contract" and "has
        /// pixels on disk" are different questions and a partial art state should still place what it
        /// can.</para>
        ///
        /// <para><b><paramref name="occupant"/> makes the doors work.</b> Any building the interior kit
        /// has baked a room for becomes ENTERABLE — <see cref="StPetersInteriors.Stand"/> puts the room
        /// under the shell, walls it, furnishes it, and hands the on-foot player to the
        /// <c>BuildingInterior</c> that swaps the two at the threshold. Optional, and null-safe: pass
        /// nothing and the rooms still stand (walls and all), they simply never open — which is what a
        /// test wants and what a scene with no player would get anyway.</para>
        /// </summary>
        public static int Place(ITidalTerrain terrain, Transform occupant = null)
        {
            var placements = VillageBuildingCatalog.Scan();
            if (placements.Count == 0)
            {
                Debug.LogWarning(
                    "[StPetersVillage] no village buildings in the contract — no houses, no school, no " +
                    "store. The kit bakes to order: run Hidden Harbours ▸ Art ▸ Bake Village Buildings.");
                return 0;
            }

            var root = new GameObject(RootName);
            int placed = 0;
            var report = new List<string>();

            foreach (var site in Sites)
            {
                var placement = VillageBuildingCatalog.Find(site.Key);
                if (!placement.IsValid)
                {
                    Debug.LogWarning(
                        $"[StPetersVillage] '{site.Key}' is not in the building contract, so the village " +
                        $"is missing the one that would stand {site.Reason}. Re-bake the kit rather than " +
                        "re-siting the village around the gap.");
                    continue;
                }

                int facing = FacingFor(placement, site);
                Sprite sprite = VillageBuildingCatalog.LoadFacing(placement, facing);
                if (sprite == null)
                {
                    Debug.LogWarning(
                        $"[StPetersVillage] {site.Key} facing {facing} has no sprite — its sheet is " +
                        $"missing or unsliced ({placement.SheetPath}). Skipping it rather than placing a " +
                        "blank.");
                    continue;
                }

                // ⚠️ Checked against the AUTHORED TERRAIN, not against the constants — the #345 lesson.
                // Every one of these positions used to be a comment claiming dry ground; the rescale made
                // three of them wrong at once. This is loud rather than silent because a house standing in
                // the sea is an authoring bug to fix, not a building to quietly drop.
                if (terrain != null)
                {
                    float ground = terrain.ElevationAt(site.Position);
                    float springHigh = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;
                    if (ground <= springHigh)
                        Debug.LogError(
                            $"[StPetersVillage] {site.Key} is sited at {site.Position} where the ground is " +
                            $"{ground:0.00} m — at or below spring high water ({springHigh:0.00} m). A " +
                            "house does not flood. Move the site; do not lower the tide.");
                }

                var go = new GameObject(site.Key);
                go.transform.SetParent(root.transform, worldPositionStays: false);

                // The pivot IS the ground centre, so the site position is the position. No offset.
                go.transform.position = new Vector3(site.Position.x, site.Position.y, 0f);

                // Configure owns the renderer, the scale, the pinned rotation and the YSortSprite. It also
                // owns the sorting order — #352 measured a seeded 4 coming back as 10 once YSortSprite
                // recomputes from world Y — so nothing here re-derives it.
                SpriteRenderer shell = VillageBuildingCatalog.Configure(go, placement, sprite);

                // THE INSIDE. Does nothing at all unless the interior kit has baked a room under this
                // building's own key, so four of the five stay solid shells and the pilot cottage opens.
                bool enterable = StPetersInteriors.Stand(go, shell, site.Key, facing, occupant);

                placed++;
                report.Add($"{site.Key} d{facing} at ({site.Position.x:0.#},{site.Position.y:0.#}) " +
                           $"{placement.Entry.footprintWidthMetres:0.#}×" +
                           $"{placement.Entry.footprintLengthMetres:0.#} m" +
                           (enterable ? " (enterable)" : ""));
            }

            float need = RequiredClearingRadius();
            Debug.Log(
                $"[StPetersVillage] Placed {placed} of {Sites.Count} building(s) in an arc round the " +
                $"green, every door turned toward it: {string.Join(" · ", report)}. Furthest footprint " +
                $"reaches {need:0.0} m from the cottage against a {StPetersWoods.VillageClearingRadius:0.0} m " +
                "clearing, so nothing is planted through a wall.");

            return placed;
        }
    }
}
#endif
