using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;                 // SpriteLightMath
using HiddenHarbours.Art.Editor;          // ShopCatalog, ShopKit, BuildingFacing
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE WHARF OPENS ITS TWO SHOPS</b> — the restaurant and the fish market, sited, turned and
    /// opened. Sibling of <c>StPetersShopsTests</c>, and here for the same reason: a placement bug is
    /// SILENT. A shop standing two metres onto the working quay still draws. A shop turned to face the
    /// bay instead of the road still draws. A shop overlapping a reserved lot draws perfectly and takes
    /// ground the plan promised to something else.
    ///
    /// <para><b>⭐ THE LOAD-BEARING TEST IS <see cref="EveryPlacedShopClearsEveryClaimTheYardMakes"/>.</b>
    /// It is this region's answer to St Peters' <c>EveryPlacedBuilding_HasAGrassClearing</c>, and it is a
    /// different test because the failure mode is different. St Peters' meadow is a SCATTER, so a
    /// building missing from a hand-maintained clearing list gets grass drawn over its floor. Nine Mile
    /// Creek has no scatter at all — its ground cover is one tiled quad at
    /// <c>NineMileCreekBuilder.GroundSortingOrder</c>, far below anything — so nothing can grow through a
    /// shop here. What CAN go wrong is the thing this yard has already done twice: a building placed on
    /// ground the plan had promised to something else (the boatyard stood half a metre on the quay, and
    /// the bait cluster's first siting landed on the owners' own shed lots). So the guard checks the
    /// PLACERS against the CLAIMS rather than a list against itself.</para>
    ///
    /// <para><b>Every bound is DERIVED from <see cref="NineMileCreekMainland"/> and from the shop
    /// contract</b>, never copied out of either — the file-wide convention in this region's tests, and
    /// what makes this a check rather than a second copy of the plan.</para>
    /// </summary>
    public class NineMileCreekShopsTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        private MainlandTidalTerrain MakeCreekTerrain()
        {
            var go = new GameObject("TidalTerrain");
            _spawned.Add(go);
            var terrain = go.AddComponent<MainlandTidalTerrain>();
            NineMileCreekBuilder.ConfigureNineMileCreekTerrain(terrain);
            return terrain;
        }

        static IReadOnlyList<NineMileCreekShops.Site> Sites => NineMileCreekShops.Sites;

        static ShopCatalog.Placement Shell(string key)
        {
            var p = ShopCatalog.FindShell(key);
            if (!p.IsValid)
                Assert.Ignore($"'{key}' is not in the shop contract — run Hidden Harbours ▸ Art ▸ Bake " +
                              "Shops. ⚠️ The fish market is the kit's newest build: a contract that " +
                              "predates B2 has three shells, not four.");
            return p;
        }

        /// <summary>The made ground, as a rectangle — what "on the spit" is really asking.</summary>
        static Rect Spit() => new Rect(
            NineMileCreekMainland.SpitFill.Center.x - NineMileCreekMainland.SpitFill.HalfSize.x,
            NineMileCreekMainland.SpitFill.Center.y - NineMileCreekMainland.SpitFill.HalfSize.y,
            NineMileCreekMainland.SpitFill.HalfSize.x * 2f,
            NineMileCreekMainland.SpitFill.HalfSize.y * 2f);

        /// <summary>The north wall's deck — the working quay, which a building may not stand on.</summary>
        static Rect QuayDeck() => NineMileCreekWharf.DeckFootprint();

        /// <summary>
        /// The WALK ASHORE: from where you step off the boat onto the deck to where that walk meets Wharf
        /// Road. Both ends are DERIVED — <c>DisembarkPos</c>, and the nearest point on the road to it — so
        /// re-siting the dock or re-routing the road moves the corridor with them.
        ///
        /// <para>It is not one of the plan's own claims; it is the one B2 added, because the first fish
        /// market site this slice tried sat squarely across it — the player would have stepped off the
        /// quay into a wall. Half-width below.</para>
        /// </summary>
        static (Vector2 From, Vector2 To) WalkAshore()
        {
            var from = new Vector2(NineMileCreekMainland.DisembarkPos.x,
                                   NineMileCreekMainland.DisembarkPos.y);
            return (from, NineMileCreekShops.NearestPointOnRoad(from));
        }

        /// <summary>How much clear ground the walk ashore wants either side of it. Narrower than
        /// <see cref="NineMileCreekMainland.RoadHalfWidth"/> on purpose: this is a footway across a deck,
        /// not a road a truck reverses down.</summary>
        const float WalkAshoreHalfWidthMetres = 2f;

        // =============================================================================================
        //  1. WHAT IS PLACED, AND THAT THE KIT CAN ACTUALLY DRAW IT
        // =============================================================================================

        [Test]
        public void TheWharfPlacesTheRestaurantAndTheFishMarket_AndBothAreBaked()
        {
            CollectionAssert.AreEquivalent(
                new[] { "restaurant", "fishMarket" }, Sites.Select(s => s.Key).ToArray(),
                "the wharf's shops drifted from B2's set (restaurant · fish market; the general store " +
                "and the post office are St Peters')");

            foreach (var site in Sites)
            {
                Assert.IsTrue(ShopCatalog.FindShell(site.Key).IsValid,
                    $"{site.Key} has no shell baked — add its row to ShopKit.ShopSet and re-bake");
                Assert.IsTrue(ShopCatalog.FindLevel(site.Key, ShopKit.GroundLevel).IsValid,
                    $"{site.Key} has no ground plan baked — and EVERY building gets an interior is canon " +
                    "since 2026-08-11, so a shop with no plan is a shop that must not be placed.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(site.Reason), $"{site.Key} has no authored reason");
            }
        }

        /// <summary>
        /// The fish market has NO upper storey, and the kit will not tell you — asking
        /// <c>shopBuildingRig</c> for one returns the GROUND rooms under the name <c>upper</c>, which
        /// bakes a duplicate ground floor half a metre in the air with nothing reporting it. This is the
        /// trade B2 added, so this is where that table has to be checked for it.
        /// </summary>
        [Test]
        public void TheFishMarketBakesGroundOnly_BecauseTheTradeHasNoUpper()
        {
            Assert.IsFalse(ShopKit.HasUpper("fishMarket"),
                "ShopKit.HasUpper says the fish market has an upper storey — the kit's own plan table " +
                "says it does not");

            var build = ShopKit.FindBuild("fishMarket");
            Assert.IsNotNull(build, "fishMarket is not in ShopKit.ShopSet");
            CollectionAssert.AreEqual(new[] { ShopKit.GroundLevel }, build.Value.Levels,
                "the fish market's Levels must be ground only");
        }

        // =============================================================================================
        //  2. ⭐ THE GUARD — the placers against the claims
        // =============================================================================================

        /// <summary>
        /// One claim on this yard: a name, a point, and the radius it reserves.
        /// </summary>
        readonly struct Claim
        {
            public readonly string Name;
            public readonly Vector2 At;
            public readonly float Radius;
            public Claim(string name, Vector2 at, float radius) { Name = name; At = at; Radius = radius; }
        }

        /// <summary>
        /// Every claim the yard makes, walked from the plan and the placers — the reserved lots (bait
        /// sheds), the owners' shed lots, and the plan's own working sites. NOT a hand-kept list: it is
        /// the same enumeration <see cref="NineMileCreekLots"/> places from, so a lot added there is a
        /// lot this guard starts checking on its own.
        /// </summary>
        static List<Claim> Claims()
        {
            var claims = new List<Claim>();

            foreach (var lot in NineMileCreekLots.ReservedLots())
                claims.Add(new Claim($"reserved lot '{lot.Name}'", lot.Position, lot.Radius));

            var owners = NineMileCreekMooredFleet.LoadOwners();
            foreach (var lot in NineMileCreekLots.OwnerLots(owners))
                claims.Add(new Claim($"owner lot '{lot.Name}'", lot.Position, lot.Radius));

            // …and the plan's working sites, which reserve ground without being "lots".
            void Working(string name, Vector3 at) =>
                claims.Add(new Claim(name, new Vector2(at.x, at.y),
                                     NineMileCreekMainland.WharfShedRadius));

            Working("the fish buyer's till", NineMileCreekMainland.FishBuyerPos);
            Working("the fish store", NineMileCreekMainland.FishStorePos);
            Working("the trap store", NineMileCreekMainland.TrapStorePos);
            Working("the buyers' parking", NineMileCreekMainland.ParkingPos);
            Working("the dory yard", NineMileCreekMainland.DoryYardPos);
            Working("the derelict dory", NineMileCreekMainland.DerelictDoryPos);

            return claims;
        }

        /// <summary>
        /// 🔴 <b>EVERY PLACED SHOP CLEARS EVERY CLAIM THE YARD MAKES</b>, and clears the ground it may
        /// not stand on. The test that did not exist when the boatyard was first sited half a metre onto
        /// the quay, and when the bait cluster's first siting landed on the owners' own shed lots.
        ///
        /// <para>A shop reserves the half-diagonal of its footprint —
        /// <see cref="ShopCatalog.FootprintRadiusMetres"/>, the same circle St Peters' shops reserve and
        /// the same convention the creek's own lots use — because the FACING is derived and a
        /// quarter-turned building presents its diagonal.</para>
        /// </summary>
        [Test]
        public void EveryPlacedShopClearsEveryClaimTheYardMakes()
        {
            var claims = Claims();
            Assert.That(claims.Count, Is.GreaterThan(0),
                "no claims were enumerated, so this guard would pass vacuously");

            Rect spit = Spit();
            Rect deck = QuayDeck();

            foreach (var site in Sites)
            {
                var shell = Shell(site.Key);
                float r = ShopCatalog.FootprintRadiusMetres(shell);
                Assert.That(r, Is.GreaterThan(0f), $"{site.Key} reports a zero footprint radius");

                // --- on made ground, whole. The spit is a FILL: a metre off it is the basin.
                Assert.That(site.Position.x - r, Is.GreaterThanOrEqualTo(spit.xMin),
                    $"{site.Key} reaches west off the made ground");
                Assert.That(site.Position.x + r, Is.LessThanOrEqualTo(spit.xMax),
                    $"{site.Key} reaches east off the made ground");
                Assert.That(site.Position.y - r, Is.GreaterThanOrEqualTo(spit.yMin),
                    $"{site.Key} reaches south off the made ground");
                Assert.That(site.Position.y + r, Is.LessThanOrEqualTo(spit.yMax),
                    $"{site.Key} reaches north off the made ground");

                // --- and NOT onto the working quay. The spit's south edge and the deck's north edge
                //     overlap by a metre, so "on made ground" does not imply this and must be said.
                Assert.That(site.Position.y - r, Is.GreaterThanOrEqualTo(deck.yMax),
                    $"{site.Key} at {site.Position} reserves {r:0.00} m and so reaches down to " +
                    $"y = {site.Position.y - r:0.00}, which is ON the north wall's deck (top " +
                    $"y = {deck.yMax:0.00}). That is the boatyard's own half-metre mistake — see " +
                    "NineMileCreekMainland.ShipyardPos — at this building's size.");

                // --- clear of Wharf Road's corridor, which runs onto this yard. Measured with the
                //     plan's OWN DistanceToRoute — the same primitive
                //     EveryClaimedLotStandsOnTheYard_ClearOfTheRoadAndTheQuay uses for the lots, so the
                //     shops and the lots are held to one definition of "in the road".
                float road = NineMileCreekMainland.DistanceToRoute(
                    NineMileCreekMainland.WharfRoad, site.Position);

                // …and NineMileCreekShops.NearestPointOnRoad, which exists because the restaurant's
                // facing needs the POINT and no shared helper returns one, must agree with it. A second
                // walk of the same polyline is a second chance to get it wrong.
                Assert.That(Vector2.Distance(site.Position,
                                             NineMileCreekShops.NearestPointOnRoad(site.Position)),
                    Is.EqualTo(road).Within(1e-3f),
                    "NearestPointOnRoad disagrees with the plan's own DistanceToRoute");

                Assert.That(road, Is.GreaterThanOrEqualTo(r + NineMileCreekMainland.RoadHalfWidth),
                    $"{site.Key} stands in Wharf Road: {road:0.00} m to the centreline, and it needs " +
                    $"{r:0.00} + {NineMileCreekMainland.RoadHalfWidth:0.0} m");

                // --- and NOT across the walk up from the berths. B2's own claim; see WalkAshore().
                var walk = WalkAshore();
                float toWalk = MainlandTidalTerrain.DistanceToSegment(site.Position, walk.From, walk.To);
                Assert.That(toWalk, Is.GreaterThanOrEqualTo(r + WalkAshoreHalfWidthMetres),
                    $"{site.Key} stands across the walk ashore ({walk.From} → {walk.To}): {toWalk:0.00} m " +
                    $"to it, and it needs {r:0.00} + {WalkAshoreHalfWidthMetres:0.0} m. Step off the boat " +
                    "and the first thing you meet is this building's wall.");

                // --- clear of every claim.
                foreach (var claim in claims)
                {
                    float d = Vector2.Distance(site.Position, claim.At);
                    Assert.That(d, Is.GreaterThanOrEqualTo(r + claim.Radius),
                        $"{site.Key} at {site.Position} overlaps {claim.Name} at {claim.At}: " +
                        $"{d:0.00} m apart, and the two reserve {r:0.00} + {claim.Radius:0.00} = " +
                        $"{r + claim.Radius:0.00} m. This yard is full — moving one means the ground " +
                        "comes from somewhere, which is the owner's ruling.");
                }
            }

            // --- and of each other.
            for (int i = 0; i < Sites.Count; i++)
                for (int j = i + 1; j < Sites.Count; j++)
                {
                    float ri = ShopCatalog.FootprintRadiusMetres(Shell(Sites[i].Key));
                    float rj = ShopCatalog.FootprintRadiusMetres(Shell(Sites[j].Key));
                    Assert.That(Vector2.Distance(Sites[i].Position, Sites[j].Position),
                        Is.GreaterThanOrEqualTo(ri + rj),
                        $"{Sites[i].Key} and {Sites[j].Key} overlap each other");
                }
        }

        /// <summary>
        /// 🔴 <b>NOTHING THIS REGION DRAWS ON THE GROUND CAN COVER A SHOP'S FLOOR</b> — the MEASURED
        /// form of "Nine Mile Creek has no meadow to worry about".
        ///
        /// <para>St Peters' post office vanished into its meadow because a room's floor sorts at
        /// <see cref="ShopCatalog.RoomSortingOrder"/> — BELOW the Y-sort band the grass tufts live in —
        /// so from outside the building was perfect and from inside it was a lawn. This region has no
        /// scatter at all: its ground cover is ONE tiled quad at
        /// <c>NineMileCreekBuilder.GroundSortingOrder</c>. That is the reason it is safe, so it is the
        /// thing to check — not the absence of a file.</para>
        ///
        /// <para><b>⚠️ And the near miss worth knowing about:</b> the wharf deck's top row draws at
        /// <see cref="SortingBands.WharfDeckMax"/>, which is the SAME order as the interior floor. Two
        /// sprites at equal order sort by distance and then arbitrarily — so a shop whose floor
        /// overlapped the deck would flicker between them. Nothing here does, because
        /// <see cref="EveryPlacedShopClearsEveryClaimTheYardMakes"/> keeps them apart, and this is the
        /// second reason that clearance is not cosmetic.</para>
        /// </summary>
        [Test]
        public void TheGroundCoverCannotDrawOverAShopFloor()
        {
            Assert.That(NineMileCreekBuilder.GroundSortingOrder,
                Is.LessThan(ShopCatalog.RoomSortingOrder),
                $"the region's ground plane sorts at {NineMileCreekBuilder.GroundSortingOrder} and a " +
                $"shop's floor at {ShopCatalog.RoomSortingOrder} — the ground would draw OVER the room " +
                "the player is standing in, which is exactly how St Peters lost its post office to the " +
                "meadow.");

            Assert.AreEqual(SortingBands.WharfDeckMax, ShopCatalog.RoomSortingOrder,
                "the wharf deck's top row and the interior floor no longer share a sorting order. Good " +
                "— but this test's remarks say they do, and the shop clearance over the deck is partly " +
                "justified by it. Re-read both before deleting either.");
        }

        /// <summary>
        /// No dressing prop stands inside a shop. A prop is not a reserved lot — it claims no ground —
        /// but it is placed at an authored point and drawn in the Y-sort band, so one inside a footprint
        /// would be furniture the player walks through, and would draw OVER the floor from inside for
        /// the reason the test above records.
        /// </summary>
        [Test]
        public void NoWharfPropStandsInsideAShop()
        {
            var props = NineMileCreekDressing.AllProps().ToList();
            Assert.That(props.Count, Is.GreaterThan(0), "no props enumerated — this would pass vacuously");

            foreach (var site in Sites)
            {
                float r = ShopCatalog.FootprintRadiusMetres(Shell(site.Key));
                foreach (var prop in props)
                    Assert.That(Vector2.Distance(site.Position, prop.Position), Is.GreaterThan(r),
                        $"the '{prop.Key}' prop at {prop.Position} stands inside {site.Key} " +
                        $"(at {site.Position}, reserving {r:0.00} m)");
            }
        }

        /// <summary>
        /// 🔴 <b>THE RESTAURANT'S SITE HAD TO MOVE, AND THIS IS WHY</b> — the positive control on the
        /// guard above. The photograph pass reserved (112, 102) for a 5 m SHED; the kit's restaurant
        /// reserves 7.30 m. Asserting only that the new site passes would leave the guard agreeing with
        /// whatever number is in the plan, so this asserts that the OLD one FAILS.
        /// </summary>
        [Test]
        public void TheOldRestaurantLot_WouldHaveStoodOnTheQuay()
        {
            var shell = Shell("restaurant");
            float r = ShopCatalog.FootprintRadiusMetres(shell);
            var wasAt = new Vector2(112f, 102f);          // the photograph pass's reservation, verbatim

            Assert.That(wasAt.y - r, Is.LessThan(QuayDeck().yMax),
                "the restaurant's original lot no longer overlaps the quay, so this control proves " +
                "nothing and the move it justifies needs re-deriving");

            Assert.That(r, Is.GreaterThan(NineMileCreekMainland.WharfShedRadius),
                "the restaurant now reserves no more ground than the 5 m shed the lot was sized for, " +
                "so the reason the site moved has evaporated");

            // …and the site that shipped does clear it.
            var now = new Vector2(NineMileCreekMainland.RestaurantLotPos.x,
                                  NineMileCreekMainland.RestaurantLotPos.y);
            Assert.That(now.y - r, Is.GreaterThanOrEqualTo(QuayDeck().yMax),
                "the restaurant's shipped site stands on the quay");
        }

        /// <summary>
        /// The restaurant is no longer RESERVED ground — it is a building. A lot left in both lists is a
        /// lot claimed twice, and the guard above would then measure the restaurant against itself and
        /// fail for a reason that is pure bookkeeping.
        /// </summary>
        [Test]
        public void TheRestaurantLeftTheReservedLots_BecauseSomethingTookIt()
        {
            Assert.IsFalse(
                NineMileCreekLots.ReservedLots().Any(
                    l => l.Name.ToLowerInvariant().Contains("restaurant")),
                "NineMileCreekLots still reserves ground for the restaurant AND NineMileCreekShops now " +
                "places one there — the lot has to leave the list when the building takes it.");

            Assert.IsTrue(Sites.Any(s => s.Key == "restaurant"),
                "nothing places a restaurant, so removing its reserved lot gave the ground away");
        }

        // =============================================================================================
        //  3. DRY GROUND, AND THE DOORS
        // =============================================================================================

        /// <summary>
        /// Neither shop floods. Checked against the AUTHORED TERRAIN rather than the constants (#345's
        /// lesson) — and it bites harder here than on the island, because this yard is MADE GROUND on a
        /// shoal: a site a few metres out is not a damp building, it is a building in the basin.
        /// </summary>
        [Test]
        public void NeitherShopFloods()
        {
            var terrain = MakeCreekTerrain();
            float springHigh = NineMileCreekMainland.SpringHighWater;

            foreach (var site in Sites)
            {
                float ground = terrain.ElevationAt(site.Position);
                Assert.That(ground, Is.GreaterThan(springHigh),
                    $"{site.Key} at {site.Position} stands on {ground:0.00} m ground against a " +
                    $"{springHigh:0.00} m spring high water");
            }
        }

        /// <summary>
        /// Each door turns to what its shop is FOR, within half a facing step — the restaurant back up
        /// the road it was sited to catch, the market to the apron the fish comes over.
        ///
        /// <para>The bound is half a cell because that is the best any 8-facing bake can do: the error is
        /// the rounding to the nearest baked turn and nothing else. Measured through
        /// <see cref="BuildingFacing.DoorErrorDegrees"/>, which reads the bake's own anchors — the
        /// arithmetic version of this test agreed with its implementation and with nothing else, and
        /// shipped every door in the village mirrored.</para>
        /// </summary>
        [Test]
        public void EveryDoorFacesWhatItsShopIsFor()
        {
            foreach (var site in Sites)
            {
                var shell = Shell(site.Key);
                int facing = NineMileCreekShops.FacingFor(shell, site);

                Assert.That(facing, Is.InRange(0, shell.Entry.facings - 1),
                    $"{site.Key} resolved to facing {facing}, outside the bake's {shell.Entry.facings}");

                float error = BuildingFacing.DoorErrorDegrees(
                    shell.Entry.doorY, shell.Entry.pivotY, shell.Entry.facings,
                    SpriteLightMath.GroundDepthScale, site.Position, site.FacingTarget, facing);

                float halfCell = 180f / shell.Entry.facings;
                Assert.That(error, Is.LessThanOrEqualTo(halfCell + 1e-3f),
                    $"{site.Key}'s door at facing {facing} is {error:0.0}° off {site.FacingTarget}, " +
                    $"more than the half-cell {halfCell:0.0}° a {shell.Entry.facings}-facing bake can be");
            }
        }

        /// <summary>
        /// The market faces the LANDING and the restaurant faces the ROAD — that they face
        /// <i>something</i> within half a cell is not the claim. Two shops a few metres apart that both
        /// resolved to the same target would pass the test above and be wrong.
        /// </summary>
        [Test]
        public void TheTwoShopsDoNotFaceTheSameThing()
        {
            var market = Sites.First(s => s.Key == "fishMarket");
            var restaurant = Sites.First(s => s.Key == "restaurant");

            Assert.AreEqual(new Vector2(NineMileCreekMainland.UnloadApronPos.x,
                                        NineMileCreekMainland.UnloadApronPos.y),
                            market.FacingTarget,
                            "the fish market must face the unloading apron — the fish landing itself");

            Assert.That(Vector2.Distance(market.FacingTarget, restaurant.FacingTarget),
                Is.GreaterThan(5f),
                "both shops resolved to effectively the same facing target, so one of them is not " +
                "facing what it is for");

            // The restaurant's target is DERIVED from the road, so it must actually be ON the road.
            Vector2 onRoad = NineMileCreekShops.NearestPointOnRoad(restaurant.Position);
            Assert.That(Vector2.Distance(restaurant.FacingTarget, onRoad), Is.LessThan(0.01f),
                "the restaurant's facing target drifted off Wharf Road");
        }

        // =============================================================================================
        //  4. THE CLAIM THIS SLICE MAKES OUT LOUD
        // =============================================================================================

        /// <summary>
        /// <b>The FISH MARKET is the building this yard has no room for, and the number is pinned.</b>
        ///
        /// <para>B2 told the owner the market clears its tightest neighbour by under half a metre while
        /// the restaurant has over a metre — that is the whole shape of the ruling he was asked for, so
        /// it is asserted rather than left in a PR body that scrolls away. If a later change opens real
        /// room for the market, this fails and whoever made it gets to delete the paragraph in
        /// <c>NineMileCreekMainland.FishMarketPos</c> rather than leave a warning that quietly stopped
        /// being true.</para>
        /// </summary>
        [Test]
        public void TheFishMarketIsTheSqueezedOne_AndTheRestaurantIsNot()
        {
            float market = TightestClearance("fishMarket", out string marketWho);
            float restaurant = TightestClearance("restaurant", out string restaurantWho);

            Assert.That(market, Is.GreaterThanOrEqualTo(0f),
                $"the fish market overlaps {marketWho} — see EveryPlacedShopClearsEveryClaimTheYardMakes");
            Assert.That(restaurant, Is.GreaterThanOrEqualTo(0f),
                $"the restaurant overlaps {restaurantWho} — see EveryPlacedShopClearsEveryClaimTheYardMakes");

            Assert.That(market, Is.LessThan(1f),
                $"the fish market now clears everything by {market:0.00} m ({marketWho}), which is more " +
                "room than B2 reported to the owner. Good news — but the plan says this yard has none, " +
                "so update it rather than leaving a stale warning.");

            Assert.That(restaurant, Is.GreaterThan(1f),
                $"the restaurant is down to {restaurant:0.00} m ({restaurantWho}). B2's finding was that " +
                "the RESTAURANT fits and the MARKET does not — if the restaurant has become the tight " +
                "one, the site that was chosen to maximise its worst clearance has been moved.");
        }

        /// <summary>The worst clearance one shop has against anything that claims ground here — the
        /// claims, the quay deck, Wharf Road and the walk ashore.</summary>
        static float TightestClearance(string key, out string who)
        {
            var site = Sites.First(s => s.Key == key);
            float r = ShopCatalog.FootprintRadiusMetres(Shell(key));
            var walk = WalkAshore();

            var candidates = new List<(float Slack, string What)>
            {
                (site.Position.y - r - QuayDeck().yMax, "the quay deck"),
                (NineMileCreekMainland.DistanceToRoute(NineMileCreekMainland.WharfRoad, site.Position)
                    - r - NineMileCreekMainland.RoadHalfWidth, "Wharf Road"),
                (MainlandTidalTerrain.DistanceToSegment(site.Position, walk.From, walk.To)
                    - r - WalkAshoreHalfWidthMetres, "the walk ashore"),
            };

            foreach (var claim in Claims())
                candidates.Add((Vector2.Distance(site.Position, claim.At) - r - claim.Radius, claim.Name));

            var worst = candidates.OrderBy(c => c.Slack).First();
            who = worst.What;
            return worst.Slack;
        }
    }
}
