#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;                // ITidalTerrain

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE WOOD LOTS OF NINE MILE CREEK</b> — three bounded blocks of trees standing in a farmed
    /// hinterland, on the owner's 2026-08-16 ruling that <i>woodland zones go ALONGSIDE the NMC fields</i>
    /// and that <i>paths are cut, not implied</i>.
    ///
    /// <para><b>⭐⭐ THE LAW THIS PASS HAD TO SURVIVE, AND HOW IT DOES.</b> This region's first canon is
    /// <see cref="NineMileCreekFields"/>' — <i>the land behind the wharf is FIELDS, NOT FOREST</i>, which
    /// <c>NineMileCreekBuilder</c> calls "the one thing this region's hinterland must never stop being". It
    /// is guarded by a measured anti-stand rule: <c>NineMileCreekFieldsTests.TheHinterlandIsFieldsAndNotForest</c>
    /// walks every tree the FIELD pass plants and fails if any of them has nine neighbours inside 24 m,
    /// <i>"so the rule survives any re-tune of the density constants"</i>. A wood lot is, by definition,
    /// exactly that many neighbours.
    ///
    /// <para><b>So the lots are a SEPARATE PASS, and that is the whole architectural point of this
    /// file.</b> <see cref="NineMileCreekFields"/> is still the farmed pass and its output is still
    /// stand-free — the anti-stand test is untouched, word for word, and still measures the same
    /// population it always did. The lots are counted, bounded and tested here, against their own law:
    /// <see cref="MaxHinterlandShare"/>. Folding them into the field scatter would have meant re-scoping
    /// the region's load-bearing test to let a stand through, which is how a constraint dies.</para></para>
    ///
    /// <para><b>A farm wood lot is not a contradiction of a farmed coast — it is part of one.</b> Every PEI
    /// farm has one: a block at the back kept for firewood and lumber, too rough or too wet to plough,
    /// usually squared off against a field boundary. That is what these are. They are sited ON the
    /// hedgerow lattice (<see cref="NineMileCreekFields.BoundaryY"/>) at one declared setback from the
    /// shore, so re-spacing the strips or re-authoring the coast moves them — the region's own
    /// "SPARSE ON PURPOSE, EVERY POSITION DERIVED" law, extended one layer out again.</para>
    ///
    /// <para><b>⚠ THE HEDGES GIVE WAY TO A LOT, they do not run through it.</b>
    /// <see cref="NineMileCreekFields.IsPlantable"/> now refuses ground a lot has taken, which is the same
    /// give-way idiom <see cref="NineMileCreekFields.NearAHedgedRoad"/> already uses where a field boundary
    /// meets a road. A hedgerow that ran into a wood and out the other side would be a fence through a
    /// forest, and a hedgerow tree planted inside one would be counted twice.</para>
    ///
    /// <para><b>A lot carries TWO layers.</b> <see cref="ScatterTrees"/> closes its canopy;
    /// <see cref="ScatterUnderstorey"/> puts the shrub layer under it — the first ask this region has ever
    /// made for the shrub kit's <c>woods</c> habitat, and the reason a wood lot here now reads as something
    /// you walk INTO rather than a block of trunks standing on a field floor.</para>
    /// </summary>
    public static class NineMileCreekWoodLots
    {
        // =================================================================================================
        //  1. THE NAMES
        // =================================================================================================

        /// <summary>The southern lot, on the y = −96 m field boundary.</summary>
        public const string SouthLotZone = "SouthWoodLot";

        /// <summary>The middle lot, on the region's own centre line — the y = 0 boundary.</summary>
        public const string MiddleLotZone = "MiddleWoodLot";

        /// <summary>The northern lot, on the y = +192 m boundary, back behind the town.</summary>
        public const string NorthLotZone = "NorthWoodLot";

        /// <summary>Suffix a lot's cut lane carries — <c>SouthWoodLot_Lane</c>. The island's own
        /// convention (<see cref="StPetersWoodlandZones.LaneSuffix"/>), quoted rather than re-spelled.</summary>
        public static string LaneSuffix => StPetersWoodlandZones.LaneSuffix;

        // =================================================================================================
        //  2. THE DIALS
        // =================================================================================================

        /// <summary>
        /// Which field boundaries carry a wood lot, as indices into
        /// <see cref="NineMileCreekFields.BoundaryY"/>. Three of the region's five boundaries, spread the
        /// length of it — so a lot is a thing you come across rather than a hedge you always see, and the
        /// two boundaries left bare keep the patchwork a patchwork.
        ///
        /// <para>⚠ Indices, not latitudes. Re-tune <see cref="NineMileCreekFields.FieldStripMetres"/> and
        /// the lots move with the lattice they are squared against instead of floating off it.</para>
        /// </summary>
        public static readonly int[] LotBoundaryIndices = { -1, 0, 2 };

        /// <summary>Names in the same order as <see cref="LotBoundaryIndices"/>.</summary>
        static readonly string[] LotNames = { SouthLotZone, MiddleLotZone, NorthLotZone };

        /// <summary>
        /// How far back from the shore a wood lot stands, measured at its own boundary's coastline
        /// (<see cref="NineMileCreekFields.CoastXAt"/>).
        ///
        /// <para><b>110 m, and both ends of that are reasoned.</b> Nearer the water it would be inside the
        /// salt band the region's own hedges thin in (<see cref="NineMileCreekFields.ExposureDepthMetres"/>
        /// is 140 m, so at 110 m back the exposure is still ~0.21 — sheltered enough for a real wood, close
        /// enough that the wind is in the species mix). Further back it would be behind the through-road at
        /// x ≈ −190, i.e. off the farmed strips the ruling says these stand ALONGSIDE, and out of sight of
        /// every road the player uses. One setback for all three, so they read as a line of wood lots back
        /// from the shore — which is what a row of farms leaves.</para>
        /// </summary>
        public const float ShoreSetbackMetres = 110f;

        /// <summary>
        /// Radius (m) of a wood lot.
        ///
        /// <para>30 m is a 0.7-hectare block — a real farm wood lot, and small enough that three of them
        /// are ~4% of this region's plantable hinterland (<see cref="MaxHinterlandShare"/> pins it). The
        /// ruling's word was <b>bounded</b>, and a bounded lot is one whose size is argued against the
        /// ground it sits in rather than chosen to look good.</para>
        /// </summary>
        public const float LotRadiusMetres = 30f;

        /// <summary>
        /// Trunk spacing (m) in a lot's core. A shade looser than the island's
        /// <see cref="StPetersWoodlandZones.LotCoreSpacingMetres"/>: a WORKED wood lot is thinned and cut
        /// over, so it does not close as hard as a stand on an island nobody has cut in a generation. Still
        /// far inside <see cref="NineMileCreekFields.FieldTreeStepMetres"/>'s 48 m, which is the difference
        /// between a wood and a field with trees in it.
        /// </summary>
        public const float LotCoreSpacingMetres = 4.2f;

        /// <summary>Fraction of a lot's radius that is solid core. The island's, for the island's reason —
        /// an edge that tapers reads as a wood petering out rather than as a block stamped on a field.</summary>
        public static float LotCoreFraction => StPetersWoodlandZones.LotCoreFraction;

        /// <summary>
        /// Half-width (m) of a lot's cut lane. <see cref="NineMileCreekFields.RoadVergeMetres"/> plus the
        /// island's own footpath clearance, which is the width that reads as a lane through trees rather
        /// than a firebreak (<see cref="StPetersWoods.PathClearance"/> argues the case). A farm track into a
        /// wood lot is wider than a footpath because something with wheels uses it.
        /// </summary>
        public static float LaneHalfWidthMetres =>
            StPetersWoods.PathClearance + NineMileCreekFields.RoadVergeMetres;

        /// <summary>
        /// <b>⭐ THE BOUNDEDNESS LAW.</b> The largest share of the region's PLANTABLE HINTERLAND the wood
        /// lots may cover, together. 8% against a measured ~4%, so the law has room to be true and no room
        /// to be widened quietly.
        ///
        /// <para>This is the number that keeps the owner's two rulings compatible. "Denser, more
        /// forest-like woods" and "the land behind the wharf is FIELDS, NOT FOREST" can both hold only
        /// while the woods are LOTS — and "lot" is not a claim you can make about a shape, only about a
        /// share. <c>NineMileCreekWoodLotTests</c> measures it on the real ground rather than trusting
        /// these radii, so growing a lot or adding a fourth fails with the share it produced.</para>
        /// </summary>
        public const float MaxHinterlandShare = 0.08f;

        // =================================================================================================
        //  3. THE TABLE
        // =================================================================================================

        /// <summary>
        /// The region's wood lots and the lane cut through each.
        ///
        /// <para>Nothing here is typed as a coordinate: a lot's latitude is a field boundary and its
        /// longitude is that boundary's own coastline minus <see cref="ShoreSetbackMetres"/>.</para>
        /// </summary>
        public static IReadOnlyList<WoodlandZone> Zones
        {
            get
            {
                if (_zones != null) return _zones;

                var rows = new List<WoodlandZone>();
                for (int i = 0; i < LotBoundaryIndices.Length; i++)
                {
                    string name = LotNames[i];
                    Vector2 at = LotCentre(LotBoundaryIndices[i]);

                    rows.Add(WoodlandZone.Circle(
                        name, WoodlandZoneKind.Lot, at, LotRadiusMetres,
                        LotCoreFraction, LotCoreSpacingMetres,
                        $"a farm wood lot squared against the y = {NineMileCreekFields.BoundaryY(LotBoundaryIndices[i]):0} m " +
                        $"field boundary, {ShoreSetbackMetres:0} m back from that boundary's own shoreline — " +
                        "the block at the back of a farm that was never worth ploughing, kept for firewood"));

                    // ⭐ THE LANE RUNS ALONG THE BOUNDARY, i.e. EAST–WEST, and that is derived from the
                    // geography rather than chosen: this coast runs north–south, so its fields are strips
                    // running down to the water and their boundaries are lines of constant y
                    // (NineMileCreekFields §4). The way into a wood lot is the way the farm already runs —
                    // down the boundary the lot is squared against, which is where the gate is.
                    //
                    // ⚠ A CUT CANOPY, not a track. The road kit owns paved surfaces and the grass system
                    // owns the sward; this row draws neither. Nothing is repainted.
                    float reach = LotRadiusMetres + LaneHalfWidthMetres;
                    rows.Add(WoodlandZone.Lane(
                        name + LaneSuffix,
                        at - Vector2.right * reach, at + Vector2.right * reach,
                        LaneHalfWidthMetres,
                        $"the way into {name} — cut along the field boundary it is squared against, because " +
                        "that is the line the farm is already worked along"));
                }

                _zones = rows.ToArray();
                return _zones;
            }
        }

        static WoodlandZone[] _zones;

        /// <summary>Drop the memoised table — see <see cref="StPetersWoodlandZones.InvalidateCache"/>. Also
        /// the hook the sabotage arms use to stand a deliberately bad row up.</summary>
        public static void InvalidateCache() => _zones = null;

        /// <summary>
        /// Where the lot on field boundary <paramref name="boundaryIndex"/> stands: on that boundary, and
        /// <see cref="ShoreSetbackMetres"/> inland of where the authored coastline crosses it.
        /// </summary>
        public static Vector2 LotCentre(int boundaryIndex)
        {
            float y = NineMileCreekFields.BoundaryY(boundaryIndex);
            return new Vector2(NineMileCreekFields.CoastXAt(y) - ShoreSetbackMetres, y);
        }

        // =================================================================================================
        //  4. WHAT PLACEMENT ASKS
        // =================================================================================================

        /// <summary>
        /// Is this ground a CUT LANE? Asked by <see cref="NineMileCreekFields.IsWoodyGround"/>, so both the
        /// farmed layers and the lots themselves keep out of it — one predicate, two passes, no chance of
        /// the lane being cut for the hedges and grown over by the wood.
        /// </summary>
        public static bool IsCleared(Vector2 p) => WoodlandZones.IsCleared(Zones, p);

        /// <summary>
        /// Is this ground inside a wood lot, core or fringe?
        ///
        /// <para><b>⚠ This is the FARMED layers' gate and not the lots' own</b> — see
        /// <see cref="NineMileCreekFields.IsPlantable"/> versus
        /// <see cref="NineMileCreekFields.IsWoodyGround"/>. The lot pass must obviously be allowed to plant
        /// inside a lot; the hedges must not.</para>
        /// </summary>
        public static bool InALot(Vector2 p) => WoodlandZones.LotShareAt(Zones, p, out _) > 0f;

        /// <summary>How strongly this ground is inside a lot (1 in a core, tapering to 0 at the rim), and
        /// which lot.</summary>
        public static float LotShareAt(Vector2 p, out string lot) =>
            WoodlandZones.LotShareAt(Zones, p, out lot);

        /// <summary>The lots alone, for a log line or the boundedness test.</summary>
        public static IEnumerable<WoodlandZone> Lots
        {
            get
            {
                foreach (var z in Zones) if (z.Kind == WoodlandZoneKind.Lot) yield return z;
            }
        }

        /// <summary>How many wood lots the region declares — read out of the table, so adding a fourth
        /// cannot leave the build's own log saying "three".</summary>
        public static int LotCount
        {
            get
            {
                int n = 0;
                foreach (var z in Zones) if (z.Kind == WoodlandZoneKind.Lot) n++;
                return n;
            }
        }

        // =================================================================================================
        //  5. THE PLANTING
        // =================================================================================================

        /// <summary>
        /// Every tree in the region's wood lots, deterministically — the same
        /// <see cref="WoodlandZones.ScatterLots"/> the island's lots use, with THIS region's ground rules
        /// and THIS region's exposure and wetness fields.
        ///
        /// <para><b><paramref name="ambient"/> is the FIELD pass's trees.</b> A hedgerow tree standing just
        /// outside a lot's rim, or a field tree the lattice happened to put in one, must not get a lot trunk
        /// planted inside it — see <see cref="WoodlandZones.MinTrunkGapMetres"/>. It is also the honest
        /// reading of the two passes: the field trees came first and the lot fills in around them.</para>
        /// </summary>
        public static List<WoodlandZones.LotTreeSite> ScatterTrees(
            ITidalTerrain terrain,
            IReadOnlyCollection<string> available,
            System.Func<string, int> variantsFor,
            IReadOnlyList<NineMileCreekFields.TreeSite> ambient)
        {
            var standing = new List<Vector2>();
            if (ambient != null)
                for (int i = 0; i < ambient.Count; i++) standing.Add(ambient[i].Position);

            return WoodlandZones.ScatterLots(
                Zones, terrain,
                // ⚠ IsWoodyGround, NOT IsPlantable. The farmed gate refuses ground a lot has taken, which
                // is exactly the ground this pass is here to plant; the woody gate is everything else the
                // region reserves — the roads, the pads, the town lots, the wharf's working sites, the
                // landing, the ponds, the made ground, the field floor and the cut lanes.
                p => NineMileCreekFields.IsWoodyGround(terrain, p),
                NineMileCreekFields.ExposureAt, NineMileCreekFields.WetnessAt,
                available, variantsFor, standing);
        }

        /// <summary>
        /// <b>The wood lots' UNDERSTOREY</b> — the shrub layer under their canopy, and the first time this
        /// region has ever asked the shrub kit for its <c>woods</c> habitat.
        ///
        /// <para><b>⭐ THIS IS THE ONLY SHRUB LAYER INSIDE A LOT, which is the opposite of the island's
        /// arrangement and follows from this region's own law.</b> St Peters' ambient heath walks a lot's
        /// ground as well, so there an understorey is <i>more</i> shrubs among some. Here the hedges GIVE
        /// WAY to a lot (<see cref="NineMileCreekFields.IsPlantable"/>), so before this pass a wood lot's
        /// floor was bare ground and after it the lot's shrubs are entirely this pass's. That is why the
        /// count matters and why <c>NineMileCreekWoodLotTests</c> measures it.</para>
        ///
        /// <para><b>⚠ IT DOES NOT TOUCH THE HINTERLAND-SHARE CEILING, and the two are easy to confuse.</b>
        /// <see cref="MaxHinterlandShare"/> is a law about how much GROUND the lots cover — it is what keeps
        /// "denser, more forest-like woods" compatible with "FIELDS, NOT FOREST", and it is measured on the
        /// lot capsules themselves, never on what grows in them. This pass adds no ground: it plants inside
        /// capsules that were already declared, so the share is arithmetically unchanged. Nor does it touch
        /// <c>TheHinterlandIsFieldsAndNotForest</c>, which counts TREES in the FARMED pass. A shrub is
        /// neither a tree nor a hectare; if a later change makes one of those ceilings move, the cause is
        /// somewhere else.</para>
        ///
        /// <para><paramref name="standing"/> should be every trunk and hedge shrub already placed — the
        /// canopy is what an understorey stands between (<see cref="WoodlandZones.MinUnderstoreyGapMetres"/>).</para>
        /// </summary>
        public static List<NineMileCreekFields.ShrubSite> ScatterUnderstorey(
            ITidalTerrain terrain, int variants, IEnumerable<Vector2> standing)
        {
            var sites = new List<NineMileCreekFields.ShrubSite>();
            if (terrain == null) return sites;

            foreach (var site in WoodlandZones.ScatterUnderstorey(
                         Zones, terrain,
                         // The same woody gate the lot's own trees ask, for the same reason: this pass must
                         // be allowed on ground a lot has taken, and refused everything else the region
                         // reserves — including the CUT LANE through its own wood.
                         p => NineMileCreekFields.IsWoodyGround(terrain, p),
                         variants, standing))
            {
                // The region names the ground; the kit's contract decides what grows on it. Inside a lot
                // that is normally `woods`, but a damp hollow under the canopy is still a swale and
                // meadowsweet is what actually grows there — so the field is asked, never assumed.
                sites.Add(new NineMileCreekFields.ShrubSite(
                    site.Position, NineMileCreekFields.ShrubHabitatAt(site.Position),
                    site.Lot, site.Variant, site.Roll));
            }
            return sites;
        }
    }
}
#endif
