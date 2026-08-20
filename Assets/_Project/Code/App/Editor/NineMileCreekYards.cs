#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>NINE MILE CREEK'S YARDS — the nine town lots, each with the ground it holds.</b> The mainland's
    /// yard table, the same one authored fact per property that <see cref="StPetersYards"/> carries: the
    /// mown lawn, the wild-grass suppression and the fence line all read this polygon and nothing else
    /// (lead-architect's lawn ruling, 2026-08-20).
    ///
    /// <para><b>⭐ EVERY YARD FACES THE ROAD IT IS STRUNG ALONG, and none of them says so.</b> The row
    /// states which lot it belongs to and how much ground it holds; the orientation comes from
    /// <see cref="NineMileCreekRoads.NearestPointOnAnyRoad"/> — the same call the crushed-shell TOWN WALKS
    /// are derived through. So a yard's front boundary and the walk up to its door cannot disagree about
    /// which way the house faces, and re-routing a road turns both (rule 6).</para>
    ///
    /// <para><b>⚠ THE WHARF YARD IS DELIBERATELY NOT IN THIS TABLE, and that is a proposal the owner may
    /// overturn.</b> The spit is MADE GROUND: <c>NineMileCreekFields.IsFieldGround</c> already refuses to
    /// grow anything on it, so a lawn polygon there would suppress grass that was never going to be
    /// there, and its buildings' ground is already reserved by
    /// <c>NineMileCreekMainland.IsWorkingSiteInTheWay</c> plus the paved pads. What the wharf actually
    /// wants is the OTHER half of this lane — a blocking edge along the quay, which the owner's
    /// 2026-08-19 walk found missing entirely — and that is a <see cref="World.PropertyBoundary"/> the
    /// wharf/shipyard/cliff pass places, not a lawn. Fencing a working yard the player drives a truck
    /// through would be actively wrong.</para>
    ///
    /// <para><b>⚠ AND THE FENCE STYLES ARE A FIRST PASS.</b> Which lot gets picket and which gets wire is
    /// the owner's call on his walk; what is settled is that the choice is DATA (one word per row) and
    /// changing it costs nothing.</para>
    /// </summary>
    public static class NineMileCreekYards
    {
        /// <summary>Stable keys, one per town lot.</summary>
        public const string HarbourmasterYard = "Harbourmaster";
        public const string ChandleryYard = "Chandlery";
        public const string GeneralStoreYard = "GeneralStore";
        public const string TavernYard = "Tavern";
        public const string ParishHallYard = "ParishHall";
        public const string HouseNorthYard = "HouseNorth";
        public const string HouseNorthWestYard = "HouseNorthWest";
        public const string HouseSouthYard = "HouseSouth";
        public const string BoatShedYard = "BoatShed";

        /// <summary>Frontage × depth (m) of a house's dooryard. The village lots stand 34 m apart at their
        /// closest, so 20 m of frontage leaves 14 m of road between neighbours — a gap you walk down, not
        /// a terrace. <c>NineMileCreekYardTests</c> re-derives the clearance rather than trusting it.
        /// </summary>
        public static readonly Vector2 HouseYardSize = new Vector2(20f, 21f);

        /// <summary>A public door's yard: shallower, because you are meant to arrive at it rather than
        /// live behind it.</summary>
        public static readonly Vector2 PublicYardSize = new Vector2(18f, 17f);

        /// <summary>
        /// The table. A property rather than a <c>static readonly</c> for the type-initialisation reason
        /// <see cref="NineMileCreekRoads.PublicTownLots"/> spells out, and memoised because the field pass
        /// asks about eighty thousand candidate sites per build.
        /// </summary>
        public static IReadOnlyList<Yard> Yards
        {
            get
            {
                if (_yards != null) return _yards;

                float lot = NineMileCreekMainland.TownLotRadius;

                var rows = new List<Yard>
                {
                    // ---- the public doors, along the through-road and the west end of Wharf Road ----
                    Facing(HarbourmasterYard, NineMileCreekMainland.HarbourmasterPos, lot,
                           PublicYardSize, YardFence.None,
                           "the harbourmaster's — a public door with a shell walk up to it and nothing " +
                           "between it and the road"),

                    Facing(ChandleryYard, NineMileCreekMainland.ChandleryPos, lot,
                           PublicYardSize, YardFence.None,
                           "the chandlery: a shop front, open to the road for the same reason St Peters' " +
                           "store is"),

                    Facing(GeneralStoreYard, NineMileCreekMainland.GeneralStorePos, lot,
                           PublicYardSize, YardFence.None,
                           "the general store — the busiest doorstep in the village and the one nobody " +
                           "would put a gate across"),

                    Facing(TavernYard, NineMileCreekMainland.TavernPos, lot,
                           PublicYardSize, YardFence.None,
                           "the tavern, whose yard is where people stand outside it"),

                    Facing(ParishHallYard, NineMileCreekMainland.ParishHallPos, lot,
                           PublicYardSize, YardFence.PostRail,
                           "the parish hall keeps a rail along the road — the one public lot with a " +
                           "boundary, because the hall's ground is the community's rather than a trade's"),

                    // ---- the three houses ----------------------------------------------------------
                    Facing(HouseNorthYard, NineMileCreekMainland.HouseNorthPos, lot,
                           HouseYardSize, YardFence.Picket,
                           "the north house, painted and kept: the yard the road is read against " +
                           "driving into town"),

                    Facing(HouseNorthWestYard, NineMileCreekMainland.HouseNorthWestPos, lot,
                           HouseYardSize, YardFence.Wire,
                           "page wire on posts — a dog and a vegetable garden, not a front"),

                    Facing(HouseSouthYard, NineMileCreekMainland.HouseSouthPos, lot,
                           HouseYardSize, YardFence.SplitRail,
                           "the south house at the quiet end, its split rail as old as the road"),

                    // ---- the boat shed -------------------------------------------------------------
                    // ⚠ Whose shed this IS remains the coordinator's open question (the 2026-07-25 ruling
                    // says there is no shipwright in this region). The yard makes no claim about that: it
                    // says the ground round it is worked, which is true whoever ends up owning it.
                    Facing(BoatShedYard, NineMileCreekMainland.BoatShedPos, lot,
                           new Vector2(22f, 20f), YardFence.Wire,
                           "a working yard: wire, wide enough to turn a trailer in, and no lawn to it"),
                };

                _yards = rows.ToArray();
                return _yards;
            }
        }

        static Yard[] _yards;

        /// <summary>Drop the memo — see <see cref="StPetersYards.InvalidateCache"/>.</summary>
        public static void InvalidateCache() => _yards = null;

        /// <summary>Is this ground inside somebody's yard? The question the meadow and the hedges ask.
        /// </summary>
        public static bool IsInsideAYard(Vector2 p) => YardPlan.IsInsideAny(Yards, p);

        /// <summary>The yard called <paramref name="name"/>, or a failure naming the table.</summary>
        public static Yard Require(string name) => YardPlan.Require(Yards, name);

        /// <summary>A town lot's yard, turned to face the nearest carriageway. One helper so no row
        /// restates the road lookup, and so every yard in this region faces the road the same way.
        /// </summary>
        static Yard Facing(string name, Vector3 lotPos, float lotRadius, Vector2 sizeMetres,
                           YardFence fence, string reason)
        {
            var lot = new Vector2(lotPos.x, lotPos.y);
            return Yard.Facing(name, lot, lotRadius, sizeMetres,
                               NineMileCreekRoads.NearestPointOnAnyRoad(lot), fence, reason);
        }
    }
}
#endif
