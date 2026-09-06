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

        /// <summary>
        /// ⭐⭐ <b>THE GUARD THE OWNER ASKED FOR: no two hulls on this wall share any of it.</b>
        ///
        /// <para>What he saw at 06:28 was five boats alongside the north wall drawn through one another.
        /// The cause is arithmetic and not art: the line is pitched at
        /// <see cref="NineMileCreekMainland.BerthSpacingMetres"/> = 5.5 m — a <i>beam</i> pitch, taken
        /// from photographs of boats "rafted two deep in places" — while <c>MooredHeadingDegrees</c>
        /// lays this fleet ALONGSIDE, so each boat spends her LENGTH on the line, and this fleet is
        /// 8.6–12.9 m long. Rafting is a second ROW off the wall; it is not two hulls in one place, and
        /// nothing before this walked the register asking that question.</para>
        ///
        /// <para>Asserted on the SHIPPED register rather than a constructed one, because the defect was
        /// shipped: the register is the thing that has to be right.</para>
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
                $"{NineMileCreekMainland.MooringFaceEastX:0.0} m and the pitch is " +
                $"{NineMileCreekMainland.BerthSpacingMetres:0.0} m. Each hull's span is half her length " +
                $"plus a {NineMileCreekMainland.BerthFenderMetres:0.0} m fender either side of her mark:");

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
                "\nFix by moving a BerthIndex — a 12 m hull needs about two and a half berths at this " +
                "pitch, and the Cape Islander three. Do NOT close the gap by narrowing the fender or " +
                "shortening a boat: both of those are measurements, not budget.");
        }

        /// <summary>
        /// ⭐⭐ <b>THE NEGATIVE CONTROL — and it asserts its own premise before it asserts anything
        /// else.</b> A guard never seen to fire is a guard nobody has tested; a control that fires on a
        /// scenario the shipped world cannot produce has tested nothing either. So this states, in
        /// order: the pitch really is 5.5 m, adjacent berths really are one pitch apart, and a 12 m hull
        /// really is a boat this register keeps — and only then puts two of them side by side and
        /// requires the overlap to be found.
        ///
        /// <para>The positive half is in the same test on purpose: an <c>OverlapWith</c> that returned a
        /// positive number for every pair would pass the negative half and be worthless.</para>
        /// </summary>
        [Test]
        public void TwoTwelveMetreHullsAtAdjacentBerthsAreCaught()
        {
            const float TwelveMetreHull = 12f;

            // Premise 1 — the pitch this control is about is the pitch the region ships.
            Assert.That(NineMileCreekMainland.BerthSpacingMetres, Is.EqualTo(5.5f).Within(1e-4f),
                "this control is written against a 5.5 m pitch. The pitch has moved, so re-derive the " +
                "control rather than trusting the numbers it prints.");

            // Premise 2 — "adjacent" means one pitch apart on the built line, not in the author's head.
            float apart = NineMileCreekMainland.BerthPos(6).x - NineMileCreekMainland.BerthPos(5).x;
            Assert.That(apart, Is.EqualTo(NineMileCreekMainland.BerthSpacingMetres).Within(1e-4f),
                "berths 5 and 6 are not one pitch apart, so this control is not testing adjacency");

            // Premise 3 — a 12 m hull is a boat that is actually kept here, not a hypothetical one.
            var twelves = WallOwners()
                .Where(o => NineMileCreekMooredFleet.LengthOf(o) >= TwelveMetreHull - 1e-3f)
                .Select(o => $"{o.Id} ({NineMileCreekMooredFleet.LengthOf(o):0.0} m)")
                .ToList();
            Assert.IsNotEmpty(twelves,
                "no hull on this wall is 12 m or longer, so a control built on one is testing a boat " +
                "the register does not keep. Re-derive it from the fleet that is actually there.");

            // The control itself: two of them, one pitch apart.
            var a = NineMileCreekMainland.BerthSpan(5, TwelveMetreHull);
            var b = NineMileCreekMainland.BerthSpan(6, TwelveMetreHull);
            float span = TwelveMetreHull + 2f * NineMileCreekMainland.BerthFenderMetres;
            float expected = span - NineMileCreekMainland.BerthSpacingMetres;

            Assert.That(a.OverlapWith(b), Is.GreaterThan(0f),
                $"two {TwelveMetreHull:0.0} m hulls at berths 5 and 6 ({a} and {b}) were NOT reported as " +
                "overlapping. The gate the builder refuses on is this very call — if it cannot see this, " +
                $"it cannot see the register the owner played (real hulls this long: " +
                $"{string.Join(", ", twelves)}).");

            Assert.That(a.OverlapWith(b), Is.EqualTo(expected).Within(1e-3f),
                $"the overlap must BE the arithmetic and not merely some positive number: " +
                $"{TwelveMetreHull:0.0} m of hull plus two " +
                $"{NineMileCreekMainland.BerthFenderMetres:0.0} m fenders is {span:0.0} m of span on a " +
                $"{NineMileCreekMainland.BerthSpacingMetres:0.0} m pitch, so {expected:0.00} m is shared.");

            // …and the positive half: far enough apart, the same call must report nothing at all.
            int clearBerths = Mathf.CeilToInt(span / NineMileCreekMainland.BerthSpacingMetres);
            var far = NineMileCreekMainland.BerthSpan(5 + clearBerths, TwelveMetreHull);
            Assert.That(a.OverlapWith(far), Is.EqualTo(0f).Within(1e-4f),
                $"{a} and {far} are {clearBerths} berths apart and do not touch. A gate that reported an " +
                "overlap between them would refuse every register ever authored — and the negative half " +
                "above would still have passed.");
        }

        /// <summary>
        /// <b>…and nobody is moored off the END of the wall.</b> The span makes this askable for the
        /// first time: a berth mark inside the table said nothing about a 12.9 m hull whose bow is past
        /// the last of the timber. Berth 13's mark is at x = 169.5 and the wall stops at x = 170, so the
        /// question is not academic — the packing this PR authors avoids it, and this is what says so.
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
                    "of her is past the end of it. Move her west: the berth table runs further east than " +
                    "the wall can carry a long hull.");
            }
        }
    }
}
#endif
