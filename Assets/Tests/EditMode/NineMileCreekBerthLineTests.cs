#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>S1b — THE BERTH LINE. A berth is where a HULL lies, so it is measured off the hull.</b>
    ///
    /// <para>`docs/design/npc-pilotage.md` §3 promises that a boat under pilotage never reaches a
    /// non-water sprite, and §3's own measurement box then shows the shipped berth line breaking that
    /// promise before any boat has moved: berth centres sat a uniform <b>2.0 m</b> off a wall face at
    /// <c>y = 87</c>, and the widest hull on the register carries a half-beam of 2.50 m — so she was
    /// authored half a metre <i>inside</i> the timber. It is invisible today only because the moored
    /// fleet is <i>placed</i>: nothing arrives, and nothing tested hull against wall.</para>
    ///
    /// <para><b>The fix is the region's own established principle — "gate the hull where the hull is",
    /// which the float berths already follow.</b> Each wall berth's standoff is derived from the hull
    /// that lies there: her own half-beam plus <see cref="NineMileCreekMainland.BerthFenderMetres"/>,
    /// the fender constant the wet-wall PR (#737) already put in this file. A berth nobody owns keeps
    /// the widest resident's standoff, so an arriving stranger is never sent somewhere narrower than
    /// the fleet's own worst case.</para>
    ///
    /// <para>⚠ <b>The number is a clamp, not a survey.</b> <c>WatertightHalfBeamMeters</c> is authored
    /// deliberately generous for the water render ("slightly generous is safe — a touch drier"). Generous
    /// is the safe direction for a clearance, and the float berths' beam gate already borrows it exactly
    /// this way, so this is precedented rather than novel — but it means the real overlaps may be smaller
    /// than they read, and a surveyed <c>BeamMetres</c> on <see cref="BoatHullDef"/> is the clean answer
    /// the day pilotage needs an exact figure.</para>
    /// </summary>
    public class NineMileCreekBerthLineTests
    {
        private static float WallFaceY => NineMileCreekWharf.MooringEdgeY;

        private static List<BoatOwnerDef> WallOwners() =>
            NineMileCreekMooredFleet.LoadOwners()
                .Where(o => o != null && o.Moorage == BoatMoorage.QuayWall)
                .OrderBy(o => o.BerthIndex)
                .ToList();

        /// <summary>
        /// ⭐⭐ <b>THE GUARD THE DESIGN OWES: no hull reaches inside the wall by more than a fender.</b>
        ///
        /// <para>Phrased as the design's §3 phrases it. A hull's inboard edge is her berth centre plus her
        /// own half-beam; the wall face is <see cref="NineMileCreekWharf.MooringEdgeY"/>. Reaching the
        /// face is a boat alongside; reaching past it by a fender's width is a boat leaning on her
        /// fenders, which is what a working wharf looks like; reaching further is a hull inside timber.</para>
        /// </summary>
        [Test]
        public void NoMooredHullReachesInsideTheWallByMoreThanAFender()
        {
            var owners = WallOwners();
            Assert.IsNotEmpty(owners, "nobody is moored at the wall — there is no berth line to check");

            float face = WallFaceY;
            float fender = NineMileCreekMainland.BerthFenderMetres;

            var report = new StringBuilder();
            report.AppendLine(
                $"The wall face is y = {face:0.00} and a fender is {fender:0.00} m, so a hull may reach " +
                $"y = {face + fender:0.00} and no further. Measured on the shipped register:");

            var offenders = new List<string>();
            foreach (var o in owners)
            {
                float half = NineMileCreekMooredFleet.HalfBeamOf(o);
                if (half <= 0f)
                {
                    // Sprite-only visual: no measured beam. Reported, never silently passed — the same
                    // reading HalfBeamOf's own doc comment gives a zero.
                    report.AppendLine($"  berth {o.BerthIndex,2}  {o.Id,-28} (no hull mesh — no measured beam)");
                    continue;
                }

                float reaches = NineMileCreekMainland.BerthPos(o.BerthIndex, half).y + half;
                float past = reaches - face;
                report.AppendLine(
                    $"  berth {o.BerthIndex,2}  {o.Id,-28} half-beam {half:0.00}  reaches y = {reaches:0.00}  " +
                    (past <= 0f ? $"({-past:0.00} m clear of the face)" : $"({past:0.00} m PAST the face)"));

                if (past > fender + 1e-4f)
                    offenders.Add($"{o.Id} reaches {past:0.00} m past the face");
            }

            Assert.IsEmpty(offenders,
                $"{offenders.Count} hull(s) are authored inside the quay wall by more than a fender:\n" +
                string.Join("\n", offenders) + "\n\n" + report +
                "\nA destination inside a wall is an illegal object, and no amount of good steering " +
                "fixes it — see docs/design/npc-pilotage.md §3.");
        }

        /// <summary>
        /// <b>…and the standoff SAYS what it means.</b> The guard above would also pass if somebody moved
        /// the whole line out by a constant sized for the widest hull. That is the alternative Q6 names,
        /// and it is not what was built: each berth's standoff is <i>her own</i> beam plus a fender, so a
        /// narrow boat lies closer to the timber than a wide one, exactly as she would in life.
        /// </summary>
        [Test]
        public void EachOwnedBerthStandsOffByHerOwnBeamPlusAFender()
        {
            float face = WallFaceY;
            float fender = NineMileCreekMainland.BerthFenderMetres;

            foreach (var o in WallOwners())
            {
                float half = NineMileCreekMooredFleet.HalfBeamOf(o);
                if (half <= 0f) continue;

                float expected = face - (half + fender);
                Assert.That(NineMileCreekMainland.BerthPos(o.BerthIndex, half).y, Is.EqualTo(expected).Within(1e-3f),
                    $"berth {o.BerthIndex} is '{o.Id}', half-beam {half:0.00} m, so her centre belongs at " +
                    $"y = {expected:0.00} — the face at {face:0.00} less her own beam and a fender. A berth " +
                    "whose standoff is not derived from the hull that lies there is a constant wearing a " +
                    "derivation's name.");
            }
        }

        /// <summary>
        /// ⭐ <b>A berth nobody owns keeps the WIDEST resident's standoff.</b> The transient berths in
        /// §5.3 take visitors and the player, and the register cannot say how wide those are. Sending a
        /// stranger to a berth cut for the narrowest boat on the wall would be the same defect with a
        /// different victim, so an unowned berth is sized for the worst case the wharf already holds.
        /// </summary>
        [Test]
        public void AnUnownedBerthIsSizedForTheWidestResident()
        {
            var owned = WallOwners().Select(o => o.BerthIndex).ToHashSet();
            float face = WallFaceY;
            float widestStandoff =
                NineMileCreekMainland.WidestResidentBeamMetres * 0.5f + NineMileCreekMainland.BerthFenderMetres;

            bool sawOne = false;
            for (int i = 0; i < NineMileCreekMainland.BerthCount; i++)
            {
                if (owned.Contains(i)) continue;
                sawOne = true;
                Assert.That(NineMileCreekMainland.BerthPos(i).y,
                    Is.EqualTo(face - widestStandoff).Within(1e-3f),
                    $"berth {i} is unowned, so it must carry the widest resident's standoff " +
                    $"({widestStandoff:0.00} m) rather than a neighbour's.");
            }

            Assert.IsTrue(sawOne,
                "every berth on the wall is owned, so this test asserted nothing. If the register has " +
                "grown to fill the wall, delete it rather than letting it stand as a green no-op.");
        }

        /// <summary>
        /// ⚠️⚠️ <b>THE BERTH LINE IS A TERRAIN INPUT NOW, AND THIS IS THE GUARD THAT SAYS SO.</b>
        ///
        /// <para>The wet-wall PR (#737) made <see cref="NineMileCreekMainland.BerthTrench"/> take its
        /// waypoints from <c>BerthPos</c>, so moving the line moves a dredged cut and therefore the
        /// committed seabed bake. The trench is honest to
        /// <see cref="NineMileCreekMainland.BerthFootprintHalfWidthMetres"/> either side of its own
        /// centreline; a hull now lying off that centreline spends part of that budget on the offset
        /// before her own beam is paid for.</para>
        ///
        /// <para>This asserts the arithmetic that keeps the cut honest: the most a hull's outboard edge
        /// can stand off the trench centreline, across the whole wall, is inside the footprint the trench
        /// was solved for. If it ever is not, the bed must be re-solved and the seabed re-baked — and this
        /// failure is where that gets found, rather than by a boat touching on her outboard bilge.</para>
        /// </summary>
        [Test]
        public void NoHullOverhangsTheTrenchTheBedWasSolvedFor()
        {
            float budget = NineMileCreekMainland.BerthFootprintHalfWidthMetres;
            Vector2[] way = NineMileCreekMainland.BerthTrenchWaypoints;
            float trenchY = way[way.Length - 1].y;      // the wall leg's own latitude

            var report = new StringBuilder();
            report.AppendLine(
                $"The trench's wall leg lies at y = {trenchY:0.00} and is cut honest to {budget:0.00} m " +
                "either side of it. Each hull's outboard edge, measured off that centreline:");

            float worst = 0f;
            string worstWho = "nobody";
            foreach (var o in WallOwners())
            {
                float half = NineMileCreekMooredFleet.HalfBeamOf(o);
                if (half <= 0f) continue;

                float outboard = NineMileCreekMainland.BerthPos(o.BerthIndex, half).y - half;   // seaward edge
                float off = Mathf.Abs(outboard - trenchY);
                report.AppendLine($"  berth {o.BerthIndex,2}  {o.Id,-28} outboard edge y = {outboard:0.00}  ({off:0.00} m off the centreline)");
                if (off > worst) { worst = off; worstWho = o.Id; }
            }

            Assert.That(worst, Is.LessThanOrEqualTo(budget + 1e-4f),
                $"'{worstWho}' hangs {worst:0.00} m off the trench centreline against a cut solved for " +
                $"{budget:0.00} m, so she would touch on her outboard bilge at spring low.\n\n" + report +
                "\nFix by re-solving BerthTrenchBedElevation for the wider footprint and RE-BAKING the " +
                "committed NineMileCreekSeabed — a skipped re-bake is a false green " +
                "(TerrainPaintTool.RebakeNineMileCreekSeabedFromCommandLine).");
        }

        // =========================================================================================
        //  ⭐⭐ THE ALONG-WALL HALF — a berth is a SPAN (owner playtest 2026-09-06: "boats … overlap
        //  each other"). The four above measure how far OFF the wall a hull lies; these measure how
        //  much OF it she takes up.
        // =========================================================================================

        private static NineMileCreekMainland.WallSpan SpanOf(BoatOwnerDef o) =>
            NineMileCreekMainland.BerthSpan(o.BerthIndex, NineMileCreekMooredFleet.LengthOf(o));

        private static NineMileCreekMainland.WallSpan PlayerSpan() =>
            NineMileCreekMainland.BerthSpan(0, NineMileCreekMainland.PlayerBerthReserveMetres);

        /// <summary>
        /// ⭐⭐ <b>THE GUARD THE OWNER ASKED FOR: no two hulls on this wall share any of it.</b>
        ///
        /// <para>What he saw at 06:28 was five boats alongside the north wall drawn through one another,
        /// and the cause was arithmetic and not art: the line was a table of <b>14 marks at 5.5 m</b> —
        /// a <i>beam</i> pitch off the photographs — while <c>MooredHeadingDegrees</c> lays this fleet
        /// ALONGSIDE, so each boat spends her LENGTH on it, and this fleet is 8.6–12.9 m long.</para>
        ///
        /// <para><b>The line is packed now, so an overlap should be impossible by construction</b> — and
        /// that is exactly why this stays. It is the guard that says the construction is still true:
        /// the packing, the fender gap and the register all agree. Walked over EVERY pair rather than
        /// neighbours, because the register the owner played overlapped in four pairs and one of them
        /// was two berths apart.</para>
        /// </summary>
        [Test]
        public void NoTwoHullsOnTheWallShareAnyOfIt()
        {
            var owners = WallOwners();
            Assert.IsNotEmpty(owners, "nobody is moored at the wall — there is no berth line to check");

            var byPlace = owners.OrderBy(o => SpanOf(o).Min).ToList();

            var report = new StringBuilder();
            report.AppendLine(
                $"The wall runs x {NineMileCreekMainland.MooringFaceWestX:0.0} → " +
                $"{NineMileCreekMainland.MooringFaceEastX:0.0} m; the berth line is PACKED from " +
                $"x {NineMileCreekMainland.BerthLineWestX:0.0} at the fender gap. Each hull's span is " +
                $"half her length plus a {NineMileCreekMainland.BerthFenderMetres:0.0} m fender either " +
                "side of her mark:");
            report.AppendLine(
                $"  berth  0  {"(the player's reserve)",-28} " +
                $"{NineMileCreekMainland.PlayerBerthReserveMetres,5:0.0} m  {PlayerSpan()}");

            for (int i = 0; i < byPlace.Count; i++)
            {
                var s = SpanOf(byPlace[i]);
                report.Append($"  berth {byPlace[i].BerthIndex,2}  {byPlace[i].Id,-28} " +
                              $"{NineMileCreekMooredFleet.LengthOf(byPlace[i]),5:0.0} m  {s}");
                report.AppendLine(i + 1 < byPlace.Count
                    ? $"   → {s.ClearOf(SpanOf(byPlace[i + 1])):0.00} m clear to the next"
                    : "");
            }

            var offenders = new List<string>();
            for (int i = 0; i < byPlace.Count; i++)
            for (int j = i + 1; j < byPlace.Count; j++)
            {
                float shared = SpanOf(byPlace[i]).OverlapWith(SpanOf(byPlace[j]));
                if (shared > 1e-4f)
                    offenders.Add($"{byPlace[i].Id} (berth {byPlace[i].BerthIndex}) and " +
                                  $"{byPlace[j].Id} (berth {byPlace[j].BerthIndex}) share " +
                                  $"{shared:0.00} m of wall");
            }

            Assert.IsEmpty(offenders,
                $"{offenders.Count} pair(s) of hulls are authored into the same stretch of wall:\n" +
                string.Join("\n", offenders) + "\n\n" + report +
                "\nThe line is packed, so this can only happen if two owners hold the same BerthIndex " +
                "or the indices skip — the packing lays berth i against berth i-1, and a gap in the " +
                "order puts two boats on one length of wall.");
        }

        /// <summary>
        /// ⭐⭐ <b>THE PLAYER'S BERTH IS RESERVED, AND NOBODY REACHES INTO IT</b> — the owner's ruling
        /// of 2026-09-06, "five working boats, keep my berth clear anyway", which is what forced the
        /// grid out.
        ///
        /// <para>Keeping his berth clear used to mean "no owner is authored at index 0", which is a
        /// statement about a table and not about the water: Leo's span crossed the mark while the mark
        /// itself was empty. Now the reserve is a <b>span</b> at the apron end, sized for
        /// <see cref="NineMileCreekMainland.PlayerBerthReserveMetres"/>, and the fleet is packed east
        /// of it — so "clear" is a measurement.</para>
        /// </summary>
        [Test]
        public void ThePlayersBerthIsReservedAndNobodyReachesIntoIt()
        {
            var reserve = PlayerSpan();

            Assert.That(reserve.Min, Is.EqualTo(NineMileCreekMainland.BerthLineWestX).Within(1e-3f),
                $"the reserve must start at the west end of the line ({NineMileCreekMainland.BerthLineWestX:0.00}) " +
                "— the owner asked for it at the APRON end, which is where he arrives.");

            Assert.That(reserve.Metres,
                Is.EqualTo(NineMileCreekMainland.PlayerBerthReserveMetres
                           + 2f * NineMileCreekMainland.BerthFenderMetres).Within(1e-3f),
                "the reserve is his largest hull plus a fender either side, and nothing else");

            foreach (var o in WallOwners())
            {
                Assert.That(o.BerthIndex, Is.GreaterThan(0),
                    $"'{o.Id}' is authored into berth 0, which is the player's reserve");

                float shared = SpanOf(o).OverlapWith(reserve);
                Assert.That(shared, Is.LessThanOrEqualTo(1e-4f),
                    $"'{o.Id}' lies {SpanOf(o)} and reaches {shared:0.00} m into the player's reserve " +
                    $"({reserve}). His berth is a SPAN, not a mark: a boat whose bow crosses it is in " +
                    "his way whether or not she holds his index.");
            }

            Assert.That(NineMileCreekMooredFleet.PlayerBerthIndex(), Is.EqualTo(0),
                "the berth nearest the dock zone must be the reserve itself — if it is not, the region " +
                "has two ideas about where the player docks, and the derivation is the one that decides.");
        }

        /// <summary>
        /// ⭐ <b>THE LINE IS PACKED FROM THE HULLS ON IT — which is what "fix the grid" bought.</b>
        /// Consecutive spans abut exactly, so between two hulls there is precisely two fenders of clear
        /// water: no berth is wider than the boat in it, and none is narrower. If this fails, the
        /// packing has grown a gap or an overlap that the pairwise guard above may not localise.
        /// </summary>
        [Test]
        public void EveryBerthIsCutForTheBoatThatLiesInIt()
        {
            float[] lengths = NineMileCreekMainland.BerthLengths();
            float[] centres = NineMileCreekMainland.BerthCentres();

            Assert.That(centres.Length, Is.EqualTo(lengths.Length));
            Assert.That(NineMileCreekMainland.BerthCount, Is.EqualTo(lengths.Length),
                "BerthCount is an OUTPUT now — one plus however many boats the register moors here");
            Assert.That(NineMileCreekMainland.BerthCount, Is.EqualTo(1 + WallOwners().Count),
                "the wall has a berth for the player and one per wall owner, and no spare marks: the " +
                "table of 14 is gone, and with it the mark that could never hold a boat");

            float edge = NineMileCreekMainland.BerthLineWestX;
            for (int i = 0; i < lengths.Length; i++)
            {
                float half = NineMileCreekMainland.BerthHalfSpanFor(lengths[i]);
                Assert.That(centres[i] - half, Is.EqualTo(edge).Within(1e-3f),
                    $"berth {i} starts at {centres[i] - half:0.00} but the previous span ended at " +
                    $"{edge:0.00} — the line has a gap or a lap in it.");
                edge = centres[i] + half;
            }

            Assert.That(edge, Is.LessThanOrEqualTo(NineMileCreekMainland.MooringFaceEastX + 1e-4f),
                $"the packed line ends at x = {edge:0.00} and the wall ends at " +
                $"{NineMileCreekMainland.MooringFaceEastX:0.0} — it no longer fits the timber.");
        }

        /// <summary>
        /// ⭐⭐ <b>THE NEGATIVE CONTROL — and it asserts its own premise before it asserts anything
        /// else.</b> The packing makes overlaps impossible by construction, which is exactly the
        /// condition under which a pairwise guard quietly becomes a green no-op. So this states, in
        /// order: the shipped line really has neighbours, they really do NOT overlap — and only then
        /// lengthens one of them into the next and requires the overlap to be found, and to equal the
        /// arithmetic rather than merely be positive.
        /// </summary>
        [Test]
        public void AHullLengthenedIntoHerNeighbourIsCaught()
        {
            var byPlace = WallOwners().OrderBy(o => SpanOf(o).Min).ToList();
            Assert.That(byPlace.Count, Is.GreaterThanOrEqualTo(2),
                "fewer than two boats on the wall: this control has no neighbours to test with");

            var west = byPlace[0];
            var east = byPlace[1];
            var westSpan = SpanOf(west);
            var eastSpan = SpanOf(east);

            // Premise: as shipped, these two do NOT overlap. Without this the control below would
            // "pass" on a line that was already broken.
            Assert.That(westSpan.OverlapWith(eastSpan), Is.EqualTo(0f).Within(1e-4f),
                $"'{west.Id}' {westSpan} and '{east.Id}' {eastSpan} already share wall, so a control " +
                "that lengthens one of them proves nothing");

            // The control: give the western boat two more metres of hull than she has.
            const float Extra = 2f;
            float loaded = NineMileCreekMooredFleet.LengthOf(west) + Extra;
            var stretched = NineMileCreekMainland.BerthSpan(west.BerthIndex, loaded);
            float shared = stretched.OverlapWith(eastSpan);

            Assert.That(shared, Is.GreaterThan(0f),
                $"'{west.Id}' lengthened by {Extra:0.0} m spans {stretched} against '{east.Id}' at " +
                $"{eastSpan} and was NOT reported as overlapping. This call is the one the builder " +
                "refuses on; if it cannot see this, it cannot see the register the owner played.");

            Assert.That(shared, Is.EqualTo(Extra * 0.5f).Within(1e-3f),
                $"the overlap must BE the arithmetic: half of {Extra:0.0} m of extra hull reaches east " +
                $"(the span grows about its own centre), so {Extra * 0.5f:0.00} m is shared — not " +
                "merely some positive number.");
        }

        /// <summary>
        /// <b>…and nobody is moored off the END of the wall.</b> The span makes this askable for the
        /// first time, and asking it is what found the defect that forced the grid out: the old table's
        /// last mark stood at x = 169.5 with the wall ending at 170, so a hull there needed
        /// <c>169.5 + L/2 + fender</c> — past the end for EVERY length, even zero. Fourteen marks of
        /// which thirteen could ever be used.
        /// </summary>
        [Test]
        public void NoHullIsMooredPastTheEndOfTheWall()
        {
            float west = NineMileCreekMainland.MooringFaceWestX;
            float east = NineMileCreekMainland.MooringFaceEastX;

            foreach (var o in WallOwners())
            {
                var s = SpanOf(o);
                Assert.That(s.Min, Is.GreaterThanOrEqualTo(west - 1e-4f),
                    $"'{o.Id}' spans {s} but the wall starts at x = {west:0.0} — she is tied to timber " +
                    "that is not there.");
                Assert.That(s.Max, Is.LessThanOrEqualTo(east + 1e-4f),
                    $"'{o.Id}' spans {s} but the wall ends at x = {east:0.0}, so {s.Max - east:0.00} m " +
                    "of her is past the end of it.");
            }
        }
    }
}
#endif
