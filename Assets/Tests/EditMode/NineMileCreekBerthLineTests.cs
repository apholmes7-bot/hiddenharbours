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
    }
}
#endif
