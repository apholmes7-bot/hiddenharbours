#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Art;                 // SpriteLightMath — the shared bake camera's squash
using HiddenHarbours.Art.Editor;          // ShopCatalog, ShopKit, BuildingFacing
using HiddenHarbours.Core;                // ITidalTerrain
using HiddenHarbours.World;               // BuildingInterior, InteriorFootprint

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE VILLAGE OPENS FOR BUSINESS</b> — St Peters' two shops, placed from the baked shop kit and
    /// opened. Sibling of <see cref="StPetersVillage"/> (the dwellings) and shaped the same way: authored
    /// sites, doors derived from where the village looks, and nothing about the art assumed.
    ///
    /// <para><b>Why this is its own file rather than two more rows in <c>StPetersVillage.Sites</c>.</b>
    /// A shop is TWO baked sheets on one ground point — a shell and a floor plan — against a house's one,
    /// and its room comes from the same kit as its shell rather than from <c>InteriorKit</c>. The facing
    /// offset between the two is <b>0</b> here and <b>4</b> for the house family, both measured. Sharing
    /// a placement loop would have meant a branch on which kit at every step of it, and the branch nobody
    /// takes is the one that rots.</para>
    ///
    /// <para><b>⭐ THE GENERAL STORE REPLACES A STAND-IN, AT THE SAME SITE.</b> Since #353 the store has
    /// been <c>houseIsoRig</c> with a bay window doing duty as a storefront, because the shop kit did not
    /// exist. The owner ruled on 2026-08-11 that the real shell takes its place at the SAME authored
    /// position — <see cref="StPetersBuilder.GeneralStorePos"/> is unmoved, so Marguerite's dooryard, the
    /// counter and the store's whole economy still derive from one coordinate and nothing had to be
    /// copied.</para>
    ///
    /// <para><b>Every shop gets an interior</b> (the owner's 2026-08-11 canon: no façade-only buildings).
    /// The plan stands under the shell on the same ground point, walled to its own footprint, and a
    /// <c>BuildingInterior</c> swaps the two at the threshold — seamless, no scene load.</para>
    /// </summary>
    public static class StPetersShops
    {
        public const string RootName = "IslandShops";

        /// <summary>The child object name of a shop's floor plan under its shell.</summary>
        public const string InteriorChildName = "Interior";

        /// <summary>The child object name of the wall colliders.</summary>
        public const string WallsChildName = "Walls";

        /// <summary>Wall thickness (m) the colliders are built with and the inside test uses. Same value
        /// and same reasoning as <see cref="StPetersInteriors.WallThicknessMetres"/>: thicker than the
        /// rig's drawn wall so a sprinting diagonal into a corner cannot tunnel it.</summary>
        public const float WallThicknessMetres = StPetersInteriors.WallThicknessMetres;

        /// <summary>The gap left in the front wall (m) — wider than the drawn opening, because the player
        /// has width of their own. ⚠️ It is cut at the DRAWN DOOR, not at the middle of the wall; two of
        /// the three shops have an off-centre street door.</summary>
        public const float DoorwayWidthMetres = StPetersInteriors.DoorwayWidthMetres;

        // =====================================================================================
        //  THE SITES
        // =====================================================================================

        public readonly struct Site
        {
            /// <summary>The kit's own build key — never camel-cased from a label. A stem that cannot be
            /// matched to a build fails silently, which is the shrub kit's lesson.</summary>
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
        /// Where the post office stands. <b>PROPOSED, and the owner eyeballs it after a builder run</b> —
        /// the handoff asks for a site, not a fait accompli.
        ///
        /// <para><b>The north side of the lane, across from the store.</b> Every other constraint in this
        /// village is already spent, and that is what chose it rather than taste:</para>
        /// <list type="bullet">
        ///   <item><b>South of the lane is full.</b> Between the store and the green sit the cottage
        ///   (8 m of authored clearance, nothing may crowd the hearth) and the red saltbox. Solving for
        ///   both leaves no point that also clears the store.</item>
        ///   <item><b>The lane's two ends are not "near the store".</b> Past the school to the west or
        ///   past the farmhouse to the east both fit, and both put the post office further from the
        ///   spawn than any house.</item>
        ///   <item><b>The clearance that binds is the store's.</b> A 6.40 m shop radius plus the post
        ///   office's 5.51 m plus <see cref="StPetersVillage.LaneGap"/> is <b>15.91 m</b>, and this site
        ///   is 16.3 m out — a lane's width and a dooryard.</item>
        ///   <item><b>It stays inside the woods' clearing.</b> 32.6 m from the cottage plus 5.51 m of
        ///   footprint is 38.1 m against <see cref="StPetersWoods.VillageClearingRadius"/>'s 44 m, so
        ///   nothing is planted through a wall.</item>
        /// </list>
        /// <para>Set back up the slope with its door turned back down at the green, which is how a post
        /// office sits in a village this size: on the lane, but not one of the houses on it.</para>
        /// </summary>
        public static readonly Vector3 PostOfficePos = new Vector3(15f, 43f, 0f);

        /// <summary>The shops, in the order you meet them walking up the lane from the spawn.</summary>
        public static IReadOnlyList<Site> Sites => new[]
        {
            new Site("generalStore", StPetersBuilder.GeneralStorePos,
                     "the lane's centre, where the path up from the spawn meets it — #353's 'first door " +
                     "you have a reason to open', now with a shop behind it instead of a bay window"),
            new Site("postOffice", PostOfficePos,
                     "the north side of the lane, set back up the slope opposite the store: on the lane " +
                     "but not one of the houses on it"),
        };

        // =====================================================================================
        //  GEOMETRY (pure — no assets, no scene, so a test can assert the siting without either)
        // =====================================================================================

        /// <summary>The facing a site is placed at — its door turned toward
        /// <see cref="StPetersBuilder.VillageGreen"/>, measured from the bake's own door anchors.</summary>
        public static int FacingFor(ShopCatalog.Placement shell, Site site) =>
            ShopCatalog.FacingToward(shell, site.Position, StPetersBuilder.VillageGreen);

        /// <summary>
        /// How far the furthest shop footprint reaches from <see cref="StPetersBuilder.CottagePos"/> —
        /// what <see cref="StPetersWoods.VillageClearingRadius"/> has to cover, alongside
        /// <see cref="StPetersVillage.RequiredClearingRadius"/>. Returns 0 when nothing is baked, so a
        /// test can say so rather than pass vacuously.
        /// </summary>
        public static float RequiredClearingRadius()
        {
            float worst = 0f;
            Vector2 cottage = StPetersBuilder.CottagePos;
            foreach (var site in Sites)
            {
                var shell = ShopCatalog.FindShell(site.Key);
                if (!shell.IsValid) continue;
                float reach = Vector2.Distance(site.Position, cottage) +
                              ShopCatalog.FootprintRadiusMetres(shell);
                if (reach > worst) worst = reach;
            }
            return worst;
        }

        // =====================================================================================
        //  PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Stand the shops up and open their doors. Returns how many were placed.
        ///
        /// <para>Null-tolerant throughout, for the village placement's reason: "declared in the contract"
        /// and "has pixels on disk" are different questions, and a partial art state should still place
        /// what it can rather than throw halfway through a region build.</para>
        ///
        /// <para><b>Re-runnable.</b> The root is rebuilt from scratch each run, so a second builder pass
        /// leaves one of everything. That is the whole of "non-destructive" here: this pass owns these
        /// objects.</para>
        /// </summary>
        public static int Place(ITidalTerrain terrain, Transform occupant = null)
        {
            if (ShopCatalog.ScanShells().Count == 0)
            {
                Debug.LogWarning(
                    "[StPetersShops] no shops in the contract — the village has no store and no post " +
                    "office. Run Hidden Harbours ▸ Art ▸ Bake Shops, then ▸ Import ▸ Slice Shop Sheets.");
                return 0;
            }

            var root = new GameObject(RootName);
            int placed = 0;
            var report = new List<string>();

            foreach (var site in Sites)
            {
                var shell = ShopCatalog.FindShell(site.Key);
                if (!shell.IsValid)
                {
                    Debug.LogWarning(
                        $"[StPetersShops] '{site.Key}' is not in the shop contract, so the village is " +
                        $"missing the one that would stand {site.Reason}. Re-bake the kit rather than " +
                        "re-siting the village around the gap.");
                    continue;
                }

                int facing = FacingFor(shell, site);
                Sprite sprite = ShopCatalog.LoadFacing(shell, facing);
                if (sprite == null)
                {
                    Debug.LogWarning(
                        $"[StPetersShops] {site.Key} facing {facing} has no sprite — its sheet is " +
                        $"missing or unsliced ({shell.SheetPath}). Skipping it rather than placing a " +
                        "blank. ⚠️ A sheet can be Multiple-mode and still not be sliced this kit's way.");
                    continue;
                }

                // ⚠️ Checked against the AUTHORED TERRAIN, not against the constants — #345's lesson.
                // Loud rather than silent: a shop standing in the sea is an authoring bug to fix, not a
                // building to quietly drop.
                if (terrain != null)
                {
                    float ground = terrain.ElevationAt(site.Position);
                    float springHigh = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;
                    if (ground <= springHigh)
                        Debug.LogError(
                            $"[StPetersShops] {site.Key} is sited at {site.Position} where the ground is " +
                            $"{ground:0.00} m — at or below spring high water ({springHigh:0.00} m). A " +
                            "shop does not flood. Move the site; do not lower the tide.");
                }

                var go = new GameObject(site.Key);
                go.transform.SetParent(root.transform, worldPositionStays: false);

                // The pivot IS the ground centre, so the site position is the position. No offset — and
                // "correcting" it to bottom-centre would stand these 5–7 m into the dirt.
                go.transform.position = new Vector3(site.Position.x, site.Position.y, 0f);

                SpriteRenderer shellRenderer = ShopCatalog.Configure(go, shell, sprite);

                bool enterable = StandInterior(go, shellRenderer, site.Key, facing, occupant);

                placed++;
                report.Add($"{site.Key} d{facing} at ({site.Position.x:0.#},{site.Position.y:0.#}) " +
                           $"{shell.FootprintMetres.x:0.#}×{shell.FootprintMetres.y:0.#} m" +
                           (enterable ? " (enterable)" : " ⚠ NO INTERIOR"));
            }

            float need = RequiredClearingRadius();
            Debug.Log(
                $"[StPetersShops] Placed {placed} of {Sites.Count} shop(s), every door turned toward the " +
                $"green at {StPetersBuilder.VillageGreen}: {string.Join(" · ", report)}. Furthest " +
                $"footprint reaches {need:0.0} m from the cottage against a " +
                $"{StPetersWoods.VillageClearingRadius:0.0} m clearing.");

            return placed;
        }

        /// <summary>
        /// Stand the floor plan under a placed shell and open the door. Returns true if a room went up.
        ///
        /// <para><b>⚠️ THE ROOM IS SHOWN AT THE SAME FACING AS THE SHELL, and that is measured.</b>
        /// <see cref="ShopCatalog.InteriorFacingFor"/> reads the contract's <c>shellFacingOffset</c>,
        /// which this kit's bake measures as <b>0</b> because both its rigs put the street door on the
        /// <c>+Y</c> gable. The HOUSE family's answer is <b>4</b> — <c>interiorIsoRig</c>'s door is on
        /// <c>−Y</c> — and carrying that across would put every shop's doorway against its back wall.</para>
        ///
        /// <para><b>⚠️ And the doorway is cut where the door is DRAWN.</b> The post office's street door
        /// sits 1.68 m left of its wall's centre and the restaurant's 2.52 m, measured from the bake's own
        /// anchors. A gap cut at the wall centre would block the door the player can see and open a hole
        /// in the wall beside it — and both would draw perfectly.</para>
        /// </summary>
        public static bool StandInterior(GameObject shopGo, SpriteRenderer shellRenderer, string key,
                                         int exteriorFacing, Transform occupant)
        {
            if (shopGo == null) return false;

            ClearExisting(shopGo.transform);

            var level = ShopCatalog.FindLevel(key, ShopKit.GroundLevel);
            if (!level.IsValid) return false;

            int interiorFacing = ShopCatalog.InteriorFacingFor(exteriorFacing);
            Sprite roomSprite = ShopCatalog.LoadFacing(level, interiorFacing);
            if (roomSprite == null)
            {
                Debug.LogWarning(
                    $"[StPetersShops] '{key}' has a baked ground plan but no facing-{interiorFacing} " +
                    $"sprite ({level.SheetPath}). Leaving the shop solid rather than standing a blank " +
                    "room inside it.");
                return false;
            }

            // --- the plan: same position as the shell, because both pivot on the ground centre.
            var roomGo = new GameObject(InteriorChildName);
            roomGo.transform.SetParent(shopGo.transform, worldPositionStays: false);
            roomGo.transform.localPosition = Vector3.zero;
            SpriteRenderer roomRenderer = ShopCatalog.Configure(
                roomGo, level, roomSprite, ShopCatalog.RoomSortingOrder, ySort: false);

            // --- where the door is, in the plan's own model frame. MEASURED, per trade.
            Vector2 door = ShopCatalog.DoorModelMetres(level);
            float doorSign = door.y >= 0f ? 1f : -1f;

            var footprint = new InteriorFootprint(
                shopGo.transform.position,
                level.FootprintMetres.x, level.FootprintMetres.y,
                interiorFacing, level.Entry.facings, SpriteLightMath.GroundDepthScale,
                doorSign, door.x);

            BuildWalls(shopGo.transform, footprint);

            var interior = shopGo.AddComponent<BuildingInterior>();
            interior.Configure(shellRenderer, roomRenderer, props: null,
                               level.FootprintMetres.x, level.FootprintMetres.y,
                               interiorFacing, level.Entry.facings,
                               SpriteLightMath.GroundDepthScale,
                               WallThicknessMetres, DoorwayWidthMetres,
                               doorOnPlusY: doorSign > 0f, doorAcrossMetres: door.x);
            interior.SetOccupant(occupant);

            Debug.Log(
                $"[StPetersShops] '{key}' is enterable: plan d{interiorFacing} under shell " +
                $"d{exteriorFacing} (the contract's MEASURED offset of " +
                $"{ShopKit.Load()?.shellFacingOffset ?? 0}), " +
                $"{level.FootprintMetres.x:0.#}×{level.FootprintMetres.y:0.#} m of floor, street door on " +
                $"the {(doorSign > 0f ? "+Y" : "−Y")} wall {door.x:+0.00;-0.00} m off its centre, " +
                $"threshold at ({footprint.DoorWorld.x:0.#},{footprint.DoorWorld.y:0.#}).");
            return true;
        }

        /// <summary>Destroy anything a previous run left on this shop, so re-running the builder leaves
        /// one room and one set of walls rather than two of each.</summary>
        static void ClearExisting(Transform shopRoot)
        {
            var existing = shopRoot.GetComponent<BuildingInterior>();
            if (existing != null) Object.DestroyImmediate(existing);

            for (int i = shopRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = shopRoot.GetChild(i);
                if (child.name == InteriorChildName || child.name == WallsChildName)
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        /// <summary>The wall colliders: five quads on one child object. One
        /// <see cref="PolygonCollider2D"/> per wall rather than five paths on one — several paths on one
        /// collider turn their overlaps into HOLES, and a hole at a corner is a player who occasionally
        /// slips through it.</summary>
        static void BuildWalls(Transform shopRoot, in InteriorFootprint footprint)
        {
            var wallsGo = new GameObject(WallsChildName);
            wallsGo.transform.SetParent(shopRoot, worldPositionStays: false);
            wallsGo.transform.localPosition = Vector3.zero;

            Vector2 origin = shopRoot.position;
            foreach (Vector2[] quad in footprint.WallQuads(WallThicknessMetres, DoorwayWidthMetres))
            {
                var collider = wallsGo.AddComponent<PolygonCollider2D>();
                collider.pathCount = 1;

                var local = new Vector2[quad.Length];
                for (int i = 0; i < quad.Length; i++) local[i] = quad[i] - origin;
                collider.SetPath(0, local);
            }
        }
    }
}
#endif
