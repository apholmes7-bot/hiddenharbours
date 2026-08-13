#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Art.Editor;          // ShopCatalog, ShopKit
using HiddenHarbours.Core;                // ITidalTerrain

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE WHARF OPENS ITS TWO SHOPS</b> — Nine Mile Creek's half of the commercial block: the
    /// RESTAURANT the photograph pass reserved ground for, and the FISH MARKET a fish wharf ought to
    /// have. Sibling of <see cref="StPetersShops"/> and shaped the same way, over the same
    /// <see cref="ShopPlacement"/>.
    ///
    /// <para><b>⭐ THE RESTAURANT FILLS A LOT THAT WAS ALREADY CLAIMED.</b> It is not new ground: the
    /// owner's satellite view put a restaurant on the wharf front, the photograph pass sited it at
    /// <c>RestaurantLotPos</c>, and <see cref="NineMileCreekLots"/> has been reporting it every build as
    /// reserved ground with <i>"NOTHING IN THE REPO BAKES A RESTAURANT"</i>. The shop kit (#437) baked
    /// one. So this is the ask being answered rather than a proposal — the only thing proposed is that
    /// the lot MOVED, because the reservation was a 5 m shed's and the building's half-diagonal is
    /// 7.30 m. See <c>NineMileCreekMainland.RestaurantLotPos</c> for that arithmetic.</para>
    ///
    /// <para><b>⚠️ THE FISH MARKET IS A PROPOSAL, AND IT IS THE ONE WITH NO ROOM.</b> Nothing reserved
    /// ground for it — it is B2's reading of "a fish wharf sells fish" — and the yard it goes on was
    /// already described by its own plan as full (§8b: <i>"the photograph shows more sheds than this
    /// yard has room for"</i>). Measured, that lands on the market and not the restaurant: the
    /// restaurant's site clears everything by <b>1.20 m</b>, the market's by <b>0.38 m</b>, and every
    /// market site with a metre to spare is ~60 m from the buyer's till. The market's ground is the
    /// owner's to rule on, not this file's. <c>NineMileCreekShopsTests</c> measures every one of those
    /// clearances so the claim is checked rather than asserted here.</para>
    ///
    /// <para><b>Doors face what each shop is FOR</b>, derived rather than typed: the restaurant turns to
    /// the road it was sited to catch, the market to the apron where fish comes ashore. Both through
    /// <see cref="BuildingFacing"/>, off the bake's own anchors.</para>
    /// </summary>
    public static class NineMileCreekShops
    {
        public const string RootName = "CreekShops";

        // =====================================================================================
        //  THE SITES
        // =====================================================================================

        /// <summary>
        /// One shop on this wharf: the kit's own build key, where it stands, what its door turns to, and
        /// why it is there.
        ///
        /// <para>⚠️ <see cref="Key"/> is the KIT's key and is never camel-cased from a label — a stem
        /// that cannot be matched to a build fails silently, which is the shrub kit's lesson. The facing
        /// TARGET is carried per site rather than shared, because unlike St Peters (where every door
        /// turns to one green) these two shops serve different things a few metres apart.</para>
        /// </summary>
        public readonly struct Site
        {
            public readonly string Key;
            public readonly Vector2 Position;
            /// <summary>What this shop's door turns toward — a point, evaluated per build so it follows
            /// the wharf if the wharf moves.</summary>
            public readonly Vector2 FacingTarget;
            public readonly string Reason;

            public Site(string key, Vector3 position, Vector2 facingTarget, string reason)
            {
                Key = key;
                Position = new Vector2(position.x, position.y);
                FacingTarget = facingTarget;
                Reason = reason;
            }
        }

        /// <summary>Where the restaurant's door looks: back up Wharf Road, into the face of whoever is
        /// walking down it — which is the reason the plan sited it here at all. The NEAREST POINT on the
        /// road rather than a typed vertex, so re-routing the road turns the building with it.</summary>
        public static Vector2 RestaurantFacingTarget =>
            NearestPointOnRoad(new Vector2(NineMileCreekMainland.RestaurantLotPos.x,
                                           NineMileCreekMainland.RestaurantLotPos.y));

        /// <summary>Where the market's door looks: the UNLOADING APRON — the fish landing itself, where
        /// the winch is and where a boat's catch comes over the wall. Not the basin and not the buyer's
        /// truck: the market faces the thing it exists downstream of.</summary>
        public static Vector2 MarketFacingTarget =>
            new Vector2(NineMileCreekMainland.UnloadApronPos.x, NineMileCreekMainland.UnloadApronPos.y);

        /// <summary>The two, in the order you meet them coming ashore.</summary>
        public static IReadOnlyList<Site> Sites => new[]
        {
            new Site("fishMarket", NineMileCreekMainland.FishMarketPos, MarketFacingTarget,
                     "the yard's west end by the buyers' parking, 13 m from the buyer's own tailgate — " +
                     "where fish is handled on this wharf, with its door to the apron it comes over"),

            new Site("restaurant", NineMileCreekMainland.RestaurantLotPos, RestaurantFacingTarget,
                     "the wharf front where the road arrives, on the photograph pass's own reserved lot " +
                     "(moved 4.24 m clear of the quay deck), door turned back up the road"),
        };

        // =====================================================================================
        //  GEOMETRY (pure — no assets, no scene, so a test can assert the siting without either)
        // =====================================================================================

        /// <summary>The facing that turns this shop's door toward its own target, measured from the
        /// bake's own door anchors.</summary>
        public static int FacingFor(ShopCatalog.Placement shell, Site site) =>
            ShopCatalog.FacingToward(shell, site.Position, site.FacingTarget);

        /// <summary>
        /// The ground a shop reserves here, as a RADIUS — the same circle
        /// <see cref="ShopCatalog.FootprintRadiusMetres"/> gives St Peters, and the same convention the
        /// creek's own lots use (<c>NineMileCreekMainland.WharfShedRadius</c>). Returns 0 for a build
        /// that is not baked, so a test says so rather than passing vacuously.
        /// </summary>
        public static float ReservedRadius(string key) =>
            ShopCatalog.FootprintRadiusMetres(ShopCatalog.FindShell(key));

        /// <summary>The nearest point on Wharf Road to <paramref name="from"/> — the road as a polyline,
        /// walked once. Used for the restaurant's facing and by the tests' road-corridor clearance, so
        /// both read one definition.</summary>
        public static Vector2 NearestPointOnRoad(Vector2 from)
        {
            Vector2[] road = NineMileCreekMainland.WharfRoad;
            Vector2 best = road[0];
            float bestSqr = float.MaxValue;

            for (int i = 0; i < road.Length - 1; i++)
            {
                Vector2 p = ClosestOnSegment(from, road[i], road[i + 1]);
                float sqr = (p - from).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = p; }
            }
            return best;
        }

        /// <summary>The closest point to <paramref name="p"/> on the segment <paramref name="a"/>–<paramref
        /// name="b"/>. Clamped, so a point off the end of the road measures to the end and not to the
        /// infinite line through it.</summary>
        public static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSqr = ab.sqrMagnitude;
            if (lenSqr < 1e-9f) return a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSqr);
            return a + ab * t;
        }

        // =====================================================================================
        //  PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Stand the wharf's shops up and open their doors. Returns how many were placed.
        ///
        /// <para>Null-tolerant throughout, for the creek's standing reason: "declared in the contract"
        /// and "has pixels on disk" are different questions, and a partial art state should place what it
        /// can rather than throw halfway through a region build.</para>
        ///
        /// <para><b>Re-runnable.</b> The root is rebuilt from scratch each run, so a second builder pass
        /// leaves one of everything.</para>
        /// </summary>
        public static int Place(ITidalTerrain terrain, Transform occupant = null)
        {
            if (ShopCatalog.ScanShells().Count == 0)
            {
                Debug.LogWarning(
                    "[NineMileCreekShops] no shops in the contract — the wharf has no restaurant and no " +
                    "fish market. Run Hidden Harbours ▸ Art ▸ Bake Shops, then ▸ Import ▸ Slice Shop " +
                    "Sheets.");
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
                        $"[NineMileCreekShops] '{site.Key}' is not in the shop contract, so the wharf is " +
                        $"missing the one that would stand {site.Reason}. ⚠️ The fish market is the " +
                        "kit's NEWEST build — if this is it, the contract predates B2 and the fix is to " +
                        "re-bake, not to re-site the wharf around the gap.");
                    continue;
                }

                int facing = FacingFor(shell, site);
                Sprite sprite = ShopCatalog.LoadFacing(shell, facing);
                if (sprite == null)
                {
                    Debug.LogWarning(
                        $"[NineMileCreekShops] {site.Key} facing {facing} has no sprite — its sheet is " +
                        $"missing or unsliced ({shell.SheetPath}). Skipping it rather than placing a " +
                        "blank. ⚠️ A sheet can be Multiple-mode and still not be sliced this kit's way.");
                    continue;
                }

                // ⚠️ Checked against the AUTHORED TERRAIN, not against the constants — #345's lesson, and
                // it bites harder here than on the island: this yard is MADE GROUND at +3.6 m standing on
                // a shoal, so a site a few metres out is not a slightly wet building, it is a building in
                // the basin. Loud rather than silent.
                if (terrain != null)
                {
                    float ground = terrain.ElevationAt(site.Position);
                    if (ground <= NineMileCreekMainland.SpringHighWater)
                        Debug.LogError(
                            $"[NineMileCreekShops] {site.Key} is sited at {site.Position} where the " +
                            $"ground is {ground:0.00} m — at or below spring high water " +
                            $"({NineMileCreekMainland.SpringHighWater:0.00} m). A shop does not flood. " +
                            "Move the site; do not lower the tide.");
                }

                var go = new GameObject(site.Key);
                go.transform.SetParent(root.transform, worldPositionStays: false);

                // The pivot IS the ground centre, so the site position is the position. No offset — and
                // "correcting" it to bottom-centre would stand these 5–7 m into the dirt.
                go.transform.position = new Vector3(site.Position.x, site.Position.y, 0f);

                SpriteRenderer shellRenderer = ShopCatalog.Configure(go, shell, sprite);

                bool enterable = ShopPlacement.StandInterior(go, shellRenderer, site.Key, facing,
                                                             occupant, "[NineMileCreekShops]");

                placed++;
                report.Add($"{site.Key} d{facing} at ({site.Position.x:0.#},{site.Position.y:0.#}) " +
                           $"{shell.FootprintMetres.x:0.#}×{shell.FootprintMetres.y:0.#} m " +
                           $"(reserves {ReservedRadius(site.Key):0.00} m)" +
                           (enterable ? " (enterable)" : " ⚠ NO INTERIOR"));
            }

            Debug.Log(
                $"[NineMileCreekShops] Placed {placed} of {Sites.Count} shop(s) on the wharf: " +
                $"{string.Join(" · ", report)}. ⚠️ The RESTAURANT fits with room (1.20 m to the quay " +
                "deck, its tightest); the FISH MARKET does not — 0.38 m to the shanty that is also " +
                "owner shed lot 1, and every site with a metre to spare is ~60 m from the buyer's till. " +
                "That is §8b's \"more sheds than this yard has room for\" measured. Both sites are " +
                "PROPOSALS the owner eyeballs; the market's is the one that wants a ruling.");

            return placed;
        }
    }
}
#endif
