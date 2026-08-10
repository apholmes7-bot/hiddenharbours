#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Art;                 // YSortSprite — a building is something you walk around
using HiddenHarbours.Core;                // ITidalTerrain
using HiddenHarbours.Tools.RigBaking;     // IsoPackSprites — the read side of the ISO rig pack

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE BUILDING LOTS ALONG WHARF ROAD</b> — the cluster the owner's satellite view shows behind
    /// the basin, placed from the photograph: the bait sheds, the boatyard, the restaurant, and one shed
    /// per boat-owner on the register.
    ///
    /// <para><b>⭐ ONE OF THEM IS DRAWN AND THE REST ARE RESERVED, and the split is a standing ruling
    /// rather than an accident of effort.</b> <c>Harris &amp; Sons</c> is drawn because the shipyard kit
    /// is baked — 25 sheets, and the owner ruled its <c>smallYard</c> tier by name. The bait sheds and the
    /// restaurant are NOT drawn, because nothing bakes either: <c>wharfBuildingRig</c>'s
    /// <c>netShed</c>/<c>iceHouse</c> presets have never been baked (M2-40/46) and no rig in the repo
    /// bakes a restaurant at all. The rule those two obey is <see cref="NineMileCreekFlavour"/>'s, in its
    /// own words: <i>a clapboard house standing in for a bait shed would be a worse lie than the greybox
    /// sprite it replaced, because it would look finished.</i> So they are named, reserved ground with no
    /// pixels, and the gap goes to the art-director as a list rather than as a surprise.</para>
    ///
    /// <para><b>The lots are the plan's, not this file's.</b> Every position comes from
    /// <see cref="NineMileCreekMainland"/> — the boatyard's site and footprint, the bait cluster, the
    /// restaurant, and the owners' shed row, which is a derived RULE that steps around the working sites
    /// rather than a typed table. Re-site the yard and the row re-flows.</para>
    /// </summary>
    public static class NineMileCreekLots
    {
        /// <summary>The root every lot hangs under — one object the owner can hide.</summary>
        public const string RootName = "NineMileCreekLots";

        /// <summary>The sub-root reserved (undrawn) ground hangs under, so it is obvious in the hierarchy
        /// that these are lots waiting on art rather than buildings that failed to load.</summary>
        public const string ReservedRootName = "ReservedLots";

        /// <summary>The pack family the boatyard is drawn from.</summary>
        public const string ShipyardFamily = "shipyardIso";

        /// <summary>The tier the owner ruled for Harris &amp; Sons (2026-08-10).</summary>
        public const string ShipyardTier = "smallYard";

        /// <summary>The yard's name on the register.</summary>
        public const string ShipyardName = "Harris & Sons";

        /// <summary>
        /// One lot with no kit to draw it: what it is, where it is, how much ground it holds, and the rig
        /// preset that WOULD draw it. The last field is the art-director's ask, written where the gap is
        /// rather than in a PR body that scrolls away.
        /// </summary>
        public readonly struct ReservedLot
        {
            public readonly string Name;
            public readonly Vector2 Position;
            public readonly float Radius;
            /// <summary>The rig and preset that would fill this lot, in the form the art-director's own
            /// catalogue uses. Empty when no rig in the repo has one — which is itself the ask.</summary>
            public readonly string WantedPreset;
            public readonly string Reason;

            public ReservedLot(string name, Vector2 position, float radius, string wantedPreset,
                               string reason)
            {
                Name = name; Position = position; Radius = radius;
                WantedPreset = wantedPreset; Reason = reason;
            }
        }

        /// <summary>
        /// Every lot the photograph puts on the yard that no kit can draw. Pure, so the art-director's
        /// list and the region's tests read the same thing.
        /// </summary>
        public static IReadOnlyList<ReservedLot> ReservedLots()
        {
            var list = new List<ReservedLot>
            {
                new ReservedLot("BaitShed_0",
                    new Vector2(NineMileCreekMainland.BaitShedPos.x, NineMileCreekMainland.BaitShedPos.y),
                    NineMileCreekMainland.WharfShedRadius, "wharfBuildingRig/netShed",
                    "the bait shed the plan already sited — the cluster's first, and still undrawn"),

                new ReservedLot("Restaurant",
                    new Vector2(NineMileCreekMainland.RestaurantLotPos.x,
                                NineMileCreekMainland.RestaurantLotPos.y),
                    NineMileCreekMainland.WharfShedRadius, "",
                    "the wharf's one non-working building, near where the road arrives. NOTHING IN THE " +
                    "REPO BAKES A RESTAURANT — this is a new preset, not a re-bake"),
            };

            for (int i = 0; i < NineMileCreekMainland.BaitShedCluster.Length; i++)
                list.Add(new ReservedLot($"BaitShed_{i + 1}",
                    new Vector2(NineMileCreekMainland.BaitShedCluster[i].x,
                                NineMileCreekMainland.BaitShedCluster[i].y),
                    NineMileCreekMainland.WharfShedRadius, "wharfBuildingRig/netShed",
                    "the rest of the photograph's bait cluster, on the eastern ground north of the yard"));

            return list;
        }

        /// <summary>
        /// The owners' shed lots — one per owner on the region's register, at the size their own Def says
        /// (a more successful owner has a larger building: the owner's ruling, carried as data rather than
        /// computed from the tier here).
        /// </summary>
        public static IReadOnlyList<ReservedLot> OwnerLots(IReadOnlyList<HiddenHarbours.Boats.BoatOwnerDef> owners)
        {
            var list = new List<ReservedLot>();
            if (owners == null) return list;

            foreach (var owner in owners)
            {
                if (owner == null) continue;
                Vector2 at = NineMileCreekMainland.OwnerShedLot(owner.LotIndex);
                list.Add(new ReservedLot($"Shed_{owner.Id}", at, owner.ShedFootprintMetres * 0.5f,
                    "wharfBuildingRig/netShed",
                    $"{owner.DisplayName}'s shed — {owner.Prosperity}, so " +
                    $"{owner.ShedFootprintMetres:0.#} m of it"));
            }
            return list;
        }

        // =============================================================================================
        //  PLACEMENT
        // =============================================================================================

        /// <summary>
        /// Put the lots on the yard. Returns how many objects were placed — the drawn yard plus every
        /// reserved lot, because a reserved lot IS a placement: it is ground claimed, and the next thing
        /// that wants to stand there has to ask.
        /// </summary>
        public static int Place(ITidalTerrain terrain,
                                IReadOnlyList<HiddenHarbours.Boats.BoatOwnerDef> owners)
        {
            var root = new GameObject(RootName);
            int placed = PlaceShipyard(root, terrain);

            var reserved = new List<ReservedLot>(ReservedLots());
            reserved.AddRange(OwnerLots(owners));
            placed += PlaceReserved(root, reserved, terrain);

            ReportTheGaps(reserved);
            return placed;
        }

        /// <summary>
        /// Draw Harris &amp; Sons. One sprite: the shipyard kit's site presets are whole-yard composites
        /// (the contract's own <c>sites</c> list), so a yard is one picture rather than a set of parts —
        /// which is why the tier the owner picked decides how much ground it takes.
        ///
        /// <para>⚠ The kit is registered COUNTER-CLOCKWISE, measured against <c>wharfIsoRig</c> by the
        /// contract's own calibration note — so the facing is resolved through
        /// <see cref="IsoPackSprites.FacingForHeading"/> with the family's declared convention and never
        /// by reading the sheet's <c>N NE E SE…</c> label order as a compass.</para>
        /// </summary>
        static int PlaceShipyard(GameObject root, ITidalTerrain terrain)
        {
            Vector2 at = new Vector2(NineMileCreekMainland.ShipyardPos.x,
                                     NineMileCreekMainland.ShipyardPos.y);

            if (terrain != null)
            {
                float ground = terrain.ElevationAt(at);
                if (ground <= NineMileCreekMainland.SpringHighWater)
                {
                    Debug.LogError(
                        $"[NineMileCreekLots] {ShipyardName} is sited at {at} where the ground is " +
                        $"{ground:0.00} m — at or below spring high water " +
                        $"({NineMileCreekMainland.SpringHighWater:0.0} m). A boatyard floods at its slip, " +
                        "not through its office. Move the site; do not lower the tide.");
                    return 0;
                }
            }

            // The yard turns its slip to the water it launches into — the basin, south of it. Derived from
            // the quay rather than typed, so re-siting either turns the yard with it.
            float heading = IsoPackSprites.HeadingOf(
                NineMileCreekWharf.DeckFootprint().center - at);
            int facing = IsoPackSprites.FacingForHeading(ShipyardFamily, heading);
            Sprite sprite = IsoPackSprites.Facing(ShipyardFamily, ShipyardTier, facing);

            if (sprite == null)
            {
                Debug.LogWarning(
                    $"[NineMileCreekLots] {ShipyardName} ('{ShipyardTier}' facing {facing}) has no sprite " +
                    $"at {IsoPackSprites.SheetPath(ShipyardFamily, ShipyardTier)} — the boatyard's LOT is " +
                    "still reserved and correct, but the yard is undrawn. Re-run after the shipyard pack " +
                    "slices.");
                return 0;
            }

            var go = new GameObject($"Shipyard_{ShipyardTier}");
            go.transform.SetParent(root.transform, worldPositionStays: false);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            go.AddComponent<YSortSprite>();

            // Solid, like every other building in this region (the creek's standing rule after the owner's
            // "boat sails through the buildings" note). The size is the yard's own ground square, not the
            // drawn cell — the drawn size is the iso projection of it and is half again as wide.
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(NineMileCreekMainland.ShipyardSideMetres,
                                   NineMileCreekMainland.ShipyardSideMetres);
            box.offset = Vector2.zero;

            Debug.Log(
                $"[NineMileCreekLots] {ShipyardName} stands at {at} — the shipyard kit's '{ShipyardTier}' " +
                $"tier (the owner's ruling), facing {facing} toward the basin it launches into, on a " +
                $"{NineMileCreekMainland.ShipyardSideMetres:0.#} m ground square.");
            return 1;
        }

        /// <summary>
        /// Claim the ground for everything that has no art. <b>No sprite, deliberately</b> — an empty
        /// GameObject with the lot's name and its reserved radius, so the hierarchy says what is missing
        /// and nothing on screen pretends the building exists.
        /// </summary>
        static int PlaceReserved(GameObject root, IReadOnlyList<ReservedLot> lots, ITidalTerrain terrain)
        {
            if (lots.Count == 0) return 0;

            var group = new GameObject(ReservedRootName);
            group.transform.SetParent(root.transform, worldPositionStays: false);

            int placed = 0;
            foreach (var lot in lots)
            {
                if (terrain != null)
                {
                    float ground = terrain.ElevationAt(lot.Position);
                    if (ground <= NineMileCreekMainland.SpringHighWater)
                    {
                        Debug.LogError(
                            $"[NineMileCreekLots] the '{lot.Name}' lot is at {lot.Position} where the " +
                            $"ground is {ground:0.00} m — at or below spring high water. {lot.Reason}. " +
                            "Move the lot; do not lower the tide.");
                        continue;
                    }
                }

                var go = new GameObject($"Lot_{lot.Name}");
                go.transform.SetParent(group.transform, worldPositionStays: false);
                go.transform.position = new Vector3(lot.Position.x, lot.Position.y, 0f);
                placed++;
            }
            return placed;
        }

        /// <summary>
        /// Say what is missing, once, on every build — the art-director's ask in the console rather than
        /// in a merged PR body nobody re-reads. Grouped by the preset wanted, because that is the unit of
        /// work on the other end: one bake covers every lot that asked for it.
        /// </summary>
        static void ReportTheGaps(IReadOnlyList<ReservedLot> lots)
        {
            var byPreset = new Dictionary<string, int>();
            foreach (var lot in lots)
            {
                string key = string.IsNullOrEmpty(lot.WantedPreset) ? "<no rig bakes one>" : lot.WantedPreset;
                byPreset.TryGetValue(key, out int n);
                byPreset[key] = n + 1;
            }

            var parts = new List<string>();
            foreach (var kv in byPreset) parts.Add($"{kv.Value} × {kv.Key}");

            Debug.LogWarning(
                $"[NineMileCreekLots] {lots.Count} lot(s) on this wharf are RESERVED GROUND with no art: " +
                $"{string.Join(" · ", parts)}. They are named and claimed, and deliberately undrawn — a " +
                "kit building standing in for one would look finished while being wrong (the " +
                "NineMileCreekFlavour ruling). This is the art-director's ask, not a build failure.");
        }
    }
}
#endif
