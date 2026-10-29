#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The built yard table used by mowing, grass suppression and boundary placement.
    /// Five rows use V1 outlines. The two west homes, the cottage plot and camper lot keep
    /// their existing geometry. The post office and cream saltbox outlines stay unbound.
    /// Active V1 rows hold their hulls with a .6 m margin; held rows retain the circle check.
    /// </summary>
    public static class StPetersYards
    {
        /// <summary>Stable keys. Anything asking for a specific yard asks by one of these.</summary>
        public const string SchoolYard = "School";
        public const string GeneralStoreYard = "GeneralStore";
        public const string WhiteFarmhouseYard = "WhiteFarmhouse";
        public const string RedSaltboxYard = "RedSaltbox";
        public const string SageCottageYard = "SageCottage";
        public const string HarbourGableYard = "HarbourGable";
        public const string WhiteGableYard = "WhiteGable";
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
                    // V1 bindings; the two west homes keep their main yard geometry below.
                    StPetersVillageYards.FromDef(SchoolYard),
                    StPetersVillageYards.FromDef(GeneralStoreYard),

                    Yard.Facing(WhiteFarmhouseYard, V2(StPetersBuilder.WhiteFarmhousePos), r,
                                new Vector2(13.0f, 15.0f), green, YardFence.Picket,
                                "the white farmhouse keeps its paint and its pickets — the kept end of " +
                                "the green, and the yard the others are read against",
                                // Kept, not Striped: the owner held his original three (school, store,
                                // sage cottage) when offered the swap. Her pickets carry the pride here.
                                back, mown: MownStyle.Kept),

                    Yard.Facing(RedSaltboxYard, V2(StPetersBuilder.RedSaltboxPos), r,
                                new Vector2(13.5f, 15.5f), green, YardFence.PostRail,
                                "post and rail: a working household with no time for painting pickets",
                                back, mown: MownStyle.Kept),

                    StPetersVillageYards.FromDef(SageCottageYard),
                    StPetersVillageYards.FromDef(HarbourGableYard),
                    StPetersVillageYards.FromDef(WhiteGableYard),
                    // Post office and cream saltbox YardDefs stay unbound until the side-entry item.

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
                                back, mown: MownStyle.Rough),

                    // ---- the camper on the back lot ------------------------------------------------
                    Yard.Facing(CamperLotYard, StPetersCamperLot.LotPos, CamperFootprintRadiusMetres,
                                new Vector2(11.0f, 11.5f), StPetersGinnyPlot.CottagePos, YardFence.None,
                                "a second dwelling (ADR 0037) on the back of her land — mown ground " +
                                "round the door and no fence, because you do not fence off a corner of " +
                                "your own lot",
                                CamperFootprintRadiusMetres + BackSetMarginMetres,
                                mown: MownStyle.Kept),
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
