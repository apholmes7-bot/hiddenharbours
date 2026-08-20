#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>ST PETERS' YARDS — the ground each property holds, and what encloses it.</b> The island's yard
    /// table: one <see cref="Yard"/> row per property, and the single authored fact the mown lawn, the
    /// wild-grass suppression and the fence line all read (lead-architect's lawn ruling, 2026-08-20).
    ///
    /// <para><b>⭐ WHY A YARD IS NOT JUST A BIGGER CLEARANCE DISC.</b> Every building on this island
    /// already reserves a <see cref="StPetersGrass.BuildingClearanceMetres"/> circle so the meadow cannot
    /// grow through its floor. That disc is a SAFETY radius — it says "no grass here" and nothing else. A
    /// yard says something different and visible: <i>this ground belongs to that house, and somebody mows
    /// it.</i> The difference between a wild plain and a kept village is the difference between a circle
    /// nobody drew and an EDGE you can see, which is why the boundary is a polygon and why the fence
    /// stands on it.</para>
    ///
    /// <para><b>⚠ THE YARDS ADD; THEY DO NOT REPLACE.</b> The clearance discs stay exactly as they were.
    /// A property with no row here behaves as it did before this file existed — same disc, same meadow,
    /// site for site — and <c>StPetersYardTests</c> pins that against a legacy predicate rebuilt from the
    /// same constants rather than asking you to believe it. A yard is therefore always safe to add and
    /// always safe to leave out, which is what makes this a table the owner can edit one row at a time.
    /// </para>
    ///
    /// <para><b>Every shape is DERIVED from what it faces.</b> No row carries an angle. Each states the
    /// building it belongs to, how much ground it holds and what it faces — and on this island every door
    /// already faces <see cref="StPetersBuilder.VillageGreen"/> (see
    /// <see cref="StPetersGinnyPlot.Approach"/>), so the yards turn with the village rather than being
    /// fitted to it by eye (rule 6).</para>
    ///
    /// <para><b>⚠⚠ THIS VILLAGE IS TOO TIGHT FOR GENEROUS YARDS, AND THAT IS A MEASUREMENT, NOT A TASTE.
    /// </b> The five doors on the green stand 16–18 m apart. A yard has to hold its building's own
    /// footprint (<see cref="DwellingFootprintRadiusMetres"/>, 6.40 m) so the lawn reaches the walls,
    /// which puts a floor of ~12.9 m on any yard's frontage — and two 12.9 m yards whose owners are
    /// 16 m apart very nearly touch. The sizes below are the largest that hold every building and leave
    /// clear ground between every pair; <c>StPetersYardTests</c> asserts both, so growing one by a metre
    /// fails with the neighbour it hit rather than shipping two lawns claiming one strip.</para>
    ///
    /// <para><b>⚠ THE POST OFFICE HAS NO YARD, AND THAT IS THE SAME MEASUREMENT.</b> It stands 16.3 m
    /// behind the general store, and at the smallest size that would hold its own walls its yard overlaps
    /// the store's. Rather than shrink one below what it must hold, this pass leaves the post office's
    /// door on the open green — which is what a village post office beside a shop actually looks like.
    /// <b>Owner's call to overturn</b>: the alternatives are a smaller measured footprint for the shop
    /// kit's post office (it is not the 8 × 10 m general store, so this may simply be a number nobody has
    /// measured yet) or moving the building.</para>
    ///
    /// <para><b>⚠ OTHER OWNER DECISIONS PROPOSED HERE, NOT SETTLED.</b> Picket versus rail per property is
    /// his call on his walk. The two SHOPS are deliberately <see cref="YardFence.None"/>: a shop front open
    /// to the lane is what a working village looks like, and fencing the store would read as a suburb.
    /// </para>
    /// </summary>
    public static class StPetersYards
    {
        /// <summary>Stable keys. Anything asking for a specific yard asks by one of these.</summary>
        public const string SchoolYard = "School";
        public const string GeneralStoreYard = "GeneralStore";
        public const string WhiteFarmhouseYard = "WhiteFarmhouse";
        public const string RedSaltboxYard = "RedSaltbox";
        public const string SageCottageYard = "SageCottage";
        public const string GinnyPlotYard = "GinnyPlot";
        public const string CamperLotYard = "CamperLot";

        /// <summary>
        /// The largest building footprint on the island, as a half-diagonal (m) — the general store's
        /// 8.00 × 10.00 m shell, 6.40 m at any facing.
        ///
        /// <para><b>⚠ THIS IS NOT <see cref="StPetersGrass.BuildingClearanceMetres"/> AND MUST NOT BECOME
        /// IT.</b> That number is 7 m: the footprint PLUS the margin that keeps the meadow off a wall.
        /// A yard is asked a different question — <i>does the lawn reach the building's walls</i> — and
        /// the 0.60 m of margin is exactly what this village has no room to spend twice.
        /// <c>StPetersYardTests</c> pins the ordering, so if a bigger shell ever raises the clearance this
        /// number is next to it and gets looked at.</para>
        /// </summary>
        public const float DwellingFootprintRadiusMetres = 6.4f;

        /// <summary>
        /// The camper's own reserved ground (m).
        ///
        /// <para><b>⚠ STATED, NOT READ, AND THE REASON IS A SILENT FAILURE.</b>
        /// <see cref="StPetersCamperLot.FootprintRadiusMetres"/> comes off the camper sidecar and is
        /// documented to return <b>zero</b> when that sidecar cannot be read. A yard whose SHAPE derived
        /// from it would quietly become a different rectangle on any run where the asset was missing —
        /// the yard would still build, still paint and still fence, in the wrong place. So the geometry
        /// takes this constant and <c>StPetersYardTests</c> pins it against the sidecar when the sidecar
        /// reads, reporting rather than passing vacuously when it does not.</para>
        /// </summary>
        public const float CamperFootprintRadiusMetres = 5.21f;

        /// <summary>How far a building stands in from the BACK boundary of its yard, over and above its
        /// own footprint (m). A stride: enough that the lawn closes behind the house rather than ending
        /// at its wall, and no more, because in this village every centimetre spent behind a house is one
        /// the lawn in front of it does not get.</summary>
        public const float BackSetMarginMetres = 0.6f;

        /// <summary>
        /// The table.
        ///
        /// <para><b>⚠ A PROPERTY, not a <c>static readonly</c> array</b> — the same type-initialisation
        /// trap <see cref="NineMileCreekRoads.PublicTownLots"/> carries its own warning about. A static
        /// field initialised from another type's statics binds at type-init time, and a run where this
        /// type initialised first would take a set of <c>Vector3.zero</c>s and put every yard on the
        /// origin with nothing failing. Memoised for the same reason
        /// <see cref="StPetersWoodlandZones.Zones"/> is: the meadow asks about tens of thousands of
        /// candidate sites per build.</para>
        /// </summary>
        public static IReadOnlyList<Yard> Yards
        {
            get
            {
                if (_yards != null) return _yards;

                Vector2 green = StPetersBuilder.VillageGreen;
                float r = DwellingFootprintRadiusMetres;
                float back = r + BackSetMarginMetres;

                var rows = new List<Yard>
                {
                    // ---- the village: five doors on the green -------------------------------------
                    Yard.Facing(SchoolYard, V2(StPetersBuilder.SchoolPos), r,
                                new Vector2(13.0f, 14.5f), green, YardFence.PostRail,
                                "the schoolyard — trodden rather than planted, so it gets rail rather " +
                                "than paint, and the deepest front on the green because the children " +
                                "are in it",
                                back),

                    Yard.Facing(GeneralStoreYard, V2(StPetersBuilder.GeneralStorePos), r,
                                new Vector2(13.0f, 14.0f), green, YardFence.None,
                                "a shop forecourt, open to the lane on purpose: you walk up to a " +
                                "counter, you do not open a gate to buy flour",
                                back),

                    Yard.Facing(WhiteFarmhouseYard, V2(StPetersBuilder.WhiteFarmhousePos), r,
                                new Vector2(13.0f, 15.0f), green, YardFence.Picket,
                                "the white farmhouse keeps its paint and its pickets — the kept end of " +
                                "the green, and the yard the others are read against",
                                back),

                    Yard.Facing(RedSaltboxYard, V2(StPetersBuilder.RedSaltboxPos), r,
                                new Vector2(13.5f, 15.5f), green, YardFence.PostRail,
                                "post and rail: a working household with no time for painting pickets",
                                back),

                    Yard.Facing(SageCottageYard, V2(StPetersBuilder.SageCottagePos), r,
                                new Vector2(18.0f, 20.0f), green, YardFence.Picket,
                                "the cottage south of the green stands on its own — 31 m to its nearest " +
                                "neighbour — so its yard is the one that can be generous without " +
                                "crowding anybody, and it is the island's biggest lawn for that reason " +
                                "alone",
                                back),

                    // ---- Aunt Ginny's, out on her own plot in the eastern woods --------------------
                    // ⚠ HER DOORYARD, NOT HER PLOT. StPetersGinnyPlot.ClearingRadius (20 m) is the land
                    // she holds and the woods stand off; this is the part of it she MOWS. It is narrow
                    // and DEEP — a strip running down the way her lane comes up from the village —
                    // because that is both what a dooryard is and the one shape that leaves her three
                    // sheds outside it. They sit on rough grass on their own clearance discs, which is
                    // what a let-go rural property looks like and what stops the plot reading as tended.
                    Yard.Facing(GinnyPlotYard, StPetersGinnyPlot.CottagePos, r,
                                new Vector2(13.0f, 18.0f), StPetersGinnyPlot.Approach, YardFence.SplitRail,
                                "split rail, half of it leaning: the fence is still there because nobody " +
                                "took it away, not because anybody keeps it",
                                back),

                    // ---- the camper on the back lot ------------------------------------------------
                    Yard.Facing(CamperLotYard, StPetersCamperLot.LotPos, CamperFootprintRadiusMetres,
                                new Vector2(11.0f, 11.5f), StPetersGinnyPlot.CottagePos, YardFence.None,
                                "a second dwelling (ADR 0037) on the back of her land — mown ground " +
                                "round the door and no fence, because you do not fence off a corner of " +
                                "your own lot",
                                CamperFootprintRadiusMetres + BackSetMarginMetres),
                };

                _yards = rows.ToArray();
                return _yards;
            }
        }

        static Yard[] _yards;

        /// <summary>Drop the memo. Nothing in a build needs this — the table is a pure function of
        /// authored constants — but a test that moves a building and wants the yards to follow does.
        /// </summary>
        public static void InvalidateCache() => _yards = null;

        /// <summary>Is this ground inside somebody's yard? The question the meadow asks.</summary>
        public static bool IsInsideAYard(Vector2 p) => YardPlan.IsInsideAny(Yards, p);

        /// <summary>The yard called <paramref name="name"/>, or a failure naming the table.</summary>
        public static Yard Require(string name) => YardPlan.Require(Yards, name);

        static Vector2 V2(Vector3 v) => new Vector2(v.x, v.y);
    }
}
#endif
