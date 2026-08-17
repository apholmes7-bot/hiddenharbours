using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;                 // GrassFootstep, YSortSprite
using HiddenHarbours.Core;                // GameConfig
using HiddenHarbours.World;               // RoutineDef, RoutineSchedule, RoutineLaneTree

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>CONTENT VALIDATION for the villagers' days</b> — the content-validation-style suite for
    /// <c>Data/Routines</c> and for the island geometry <see cref="StPetersRoutines"/> derives.
    ///
    /// <para>Everything here fails on an AUTHORING mistake rather than on a code one, which is what makes it
    /// worth having: a routine that names a station this island does not declare, or a jitter wide enough to
    /// reorder somebody's day, or a walk that cannot finish inside the block it starts in, all produce a
    /// villager who quietly stands still or arrives somewhere else — and nothing on screen says why.</para>
    ///
    /// <para>The geometry cases are re-derived from the region's OWN constants, never from a copy of the
    /// numbers, so a moved building fails with the figure to use rather than passing against a stale
    /// literal (#345's standing lesson).</para>
    /// </summary>
    public class StPetersRoutineContentTests
    {
        static RoutineDef[] Routines() => StPetersRoutines.LoadRoutines();

        static readonly Regex SnakeId = new Regex(@"^routine\.[a-z0-9]+(_[a-z0-9]+)*$");

        // ---- the assets themselves ---------------------------------------------------------------

        [Test]
        public void EveryRosterMemberHasARoutineAsset()
        {
            RoutineDef[] routines = Routines();
            Assert.That(routines.Length, Is.GreaterThan(0),
                        "No routine assets under Data/Routines — run Hidden Harbours ▸ World ▸ Build " +
                        "St Peters Routines. Without them every islander stays anchored.");

            var authored = new HashSet<string>(routines.Select(r => r.Id), System.StringComparer.Ordinal);
            foreach (RoutineDefsBuilder.Day day in RoutineDefsBuilder.Roster)
                Assert.That(authored, Contains.Item(day.RoutineId),
                            $"{day.NpcAsset} is in the roster but has no asset with id '{day.RoutineId}'.");
        }

        /// <summary>
        /// 🔴 <b>THE COMMITTED ASSET AND THE BUILDER'S TABLE HAVE TO AGREE.</b>
        /// <c>RoutineDefsBuilder.Author</c> runs with <c>overwrite: false</c>, so editing the roster does
        /// NOT rewrite an asset that already exists — the C# says one thing, the shipped
        /// <c>.asset</c> says another, and the GAME reads the asset. Ginny's move on 2026-08-16 changed
        /// both by hand, which is exactly the moment this can silently drift.
        ///
        /// <para>Blocks are compared by departure hour and station id. The <c>Why</c> prose is not
        /// compared: it is documentation, it wraps differently through Unity's YAML writer, and holding
        /// it byte-equal would make this test fail on a comma.</para>
        /// </summary>
        [Test]
        public void EveryRoutineAsset_MatchesTheRosterItWasBuiltFrom()
        {
            RoutineDef[] routines = Routines();
            Assert.That(routines.Length, Is.GreaterThan(0), "no routine assets — this would be vacuous");

            foreach (RoutineDefsBuilder.Day day in RoutineDefsBuilder.Roster)
            {
                RoutineDef asset = routines.FirstOrDefault(r => r.Id == day.RoutineId);
                if (asset == null) continue;   // EveryRosterMemberHasARoutineAsset owns that failure

                Assert.That(asset.Entries.Length, Is.EqualTo(day.Blocks.Length),
                            $"'{day.RoutineId}': the asset has {asset.Entries.Length} block(s) and the " +
                            $"roster authors {day.Blocks.Length}. The builder does not overwrite an " +
                            "existing asset — edit both, or delete the asset and re-run the builder.");

                int n = System.Math.Min(asset.Entries.Length, day.Blocks.Length);
                for (int i = 0; i < n; i++)
                {
                    Assert.That(asset.Entries[i].StartHour, Is.EqualTo(day.Blocks[i].StartHour).Within(1e-4f),
                                $"'{day.RoutineId}' block {i}: asset departs at " +
                                $"{asset.Entries[i].StartHour}, roster says {day.Blocks[i].StartHour}.");
                    Assert.That(asset.Entries[i].StationId, Is.EqualTo(day.Blocks[i].StationId),
                                $"'{day.RoutineId}' block {i}: asset walks to " +
                                $"'{asset.Entries[i].StationId}', roster says " +
                                $"'{day.Blocks[i].StationId}'.");
                }

                Assert.That(asset.WalkSpeedMetresPerSecond, Is.EqualTo(day.WalkSpeed).Within(1e-4f),
                            $"'{day.RoutineId}': walk speed differs between asset and roster — and it is " +
                            "what every travel-time argument on this island is computed from.");
            }
        }

        [Test]
        public void EveryRoutineIdIsSnakeCaseAndUnique()
        {
            RoutineDef[] routines = Routines();
            foreach (RoutineDef r in routines)
                Assert.That(SnakeId.IsMatch(r.Id), Is.True,
                            $"'{r.Id}' ({r.name}) is not a routine.snake_case id. Ids are append-only and " +
                            "are the only handle the region builder has on a day.");

            var duplicates = routines.GroupBy(r => r.Id, System.StringComparer.Ordinal)
                                     .Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            Assert.That(duplicates, Is.Empty, "duplicate routine id(s): " + string.Join(", ", duplicates));
        }

        [Test]
        public void EveryRoutineHasAnNpcAndAWalkableDay()
        {
            foreach (RoutineDef r in Routines())
            {
                Assert.That(r.Npc, Is.Not.Null,
                            $"'{r.Id}' has no NpcDef, so nobody in the world is wired to it.");
                Assert.That(r.Entries.Length, Is.GreaterThanOrEqualTo(2),
                            $"'{r.Id}' has {r.Entries.Length} block(s). A one-block day is a person " +
                            "standing still, which is what NOT having a routine already does.");
                Assert.That(r.IsWalkable, Is.True, $"'{r.Id}' reports itself unwalkable.");
                Assert.That(r.WalkSpeedMetresPerSecond, Is.GreaterThan(0.2f).And.LessThan(4f),
                            $"'{r.Id}' walks at {r.WalkSpeedMetresPerSecond} m/s — outside anything a " +
                            "person does on foot, and the gait the sprite draws comes off this number.");
            }
        }

        [Test]
        public void EveryDaysHoursAreInRangeAndStrictlyAscending()
        {
            foreach (RoutineDef r in Routines())
            {
                for (int i = 0; i < r.Entries.Length; i++)
                    Assert.That(r.Entries[i].StartHour, Is.GreaterThanOrEqualTo(0f).And.LessThan(24f),
                                $"'{r.Id}' block {i} starts at hour {r.Entries[i].StartHour}.");

                for (int i = 0; i + 1 < r.Entries.Length; i++)
                    Assert.That(r.Entries[i].StartHour, Is.LessThan(r.Entries[i + 1].StartHour),
                                $"'{r.Id}' block {i} ({r.Entries[i].StartHour}) is not before block " +
                                $"{i + 1} ({r.Entries[i + 1].StartHour}). The last block wraps midnight; " +
                                "the rest must read down the day in order.");
            }
        }

        [Test]
        public void EveryStationNamedByARoutineIsOneThisIslandDeclares()
        {
            var declared = new HashSet<string>(StPetersRoutines.AllStationIds,
                                              System.StringComparer.Ordinal);
            foreach (RoutineDef r in Routines())
                for (int i = 0; i < r.Entries.Length; i++)
                    Assert.That(declared, Contains.Item(r.Entries[i].StationId),
                                $"'{r.Id}' block {i} sends them to '{r.Entries[i].StationId}', which " +
                                "StPetersRoutines does not declare. They would stand still all day and " +
                                "nothing on screen would say why.");
        }

        /// <summary>
        /// The jitter clamp is a guarantee rather than the mechanism (see
        /// <see cref="RoutineSchedule.DepartureHour"/>), but an authored magnitude that NEEDS clamping means
        /// the owner asked for a spread the day cannot hold — so it is reported here with the number that
        /// would fit, rather than silently narrowed.
        /// </summary>
        [Test]
        public void NoDaysJitterIsWideEnoughToNeedClamping()
        {
            foreach (RoutineDef r in Routines())
            {
                var hours = r.Entries.Select(e => e.StartHour).ToArray();
                float smallestGapMinutes = RoutineSchedule.SmallestGapHours(hours) * 60f;
                Assert.That(r.ScheduleJitterMinutes, Is.LessThan(smallestGapMinutes * 0.5f),
                            $"'{r.Id}' asks for ±{r.ScheduleJitterMinutes:0.#} game min of jitter, but its " +
                            $"tightest block is {smallestGapMinutes:0.#} min long — anything at or over " +
                            $"{smallestGapMinutes * 0.5f:0.#} gets clamped. Widen the block or narrow the " +
                            "jitter.");
            }
        }

        [Test]
        public void EveryDaysBlocksTileTheDayExactly()
        {
            foreach (RoutineDef r in Routines())
            {
                var hours = r.Entries.Select(e => e.StartHour).ToArray();
                float total = 0f;
                for (int i = 0; i < hours.Length; i++) total += RoutineSchedule.GapHours(hours, i);
                Assert.That(total, Is.EqualTo(24f).Within(1e-2f), $"'{r.Id}' does not tile a full day.");
            }
        }

        // ---- the lane network --------------------------------------------------------------------

        [Test]
        public void TheIslandsLaneNetworkIsATree()
        {
            StPetersRoutines.LaneTable table = StPetersRoutines.BuildLaneTable();
            Assert.That(table.Count, Is.GreaterThan(1));
            Assert.That(RoutineLaneTree.IsTree(table.Parents, out int offender), Is.True,
                        offender >= 0 && offender < table.Count
                            ? $"node {offender} ('{table.Names[offender]}') does not reach a single root"
                            : "there is not exactly one root");
        }

        [Test]
        public void TheLaneTablesParallelArraysAgree_AndItsNamesAreUniqueAndDeclared()
        {
            StPetersRoutines.LaneTable t = StPetersRoutines.BuildLaneTable();
            Assert.That(t.Positions.Length, Is.EqualTo(t.Count));
            Assert.That(t.Parents.Length, Is.EqualTo(t.Count));
            Assert.That(t.ViaStart.Length, Is.EqualTo(t.Count));
            Assert.That(t.ViaCount.Length, Is.EqualTo(t.Count));

            Assert.That(t.Names.Distinct().Count(), Is.EqualTo(t.Count), "duplicate lane node name");

            var declared = new HashSet<string>(StPetersRoutines.AllLaneNodeNames,
                                               System.StringComparer.Ordinal);
            foreach (string n in t.Names)
                Assert.That(declared, Contains.Item(n), $"lane node '{n}' is not in AllLaneNodeNames.");
            Assert.That(t.Count, Is.EqualTo(StPetersRoutines.AllLaneNodeNames.Length),
                        "every declared junction has to be built, and no more.");

            for (int i = 0; i < t.Count; i++)
            {
                Assert.That(t.ViaCount[i], Is.GreaterThanOrEqualTo(0));
                if (t.ViaCount[i] == 0) continue;
                Assert.That(t.ViaStart[i] + t.ViaCount[i], Is.LessThanOrEqualTo(t.Via.Length));
                Assert.That(t.Parents[i], Is.Not.EqualTo(RoutineLaneTree.NoParent),
                            $"'{t.Names[i]}' is the root and carries bend points — bends live on an edge.");
            }
        }

        /// <summary>
        /// ⭐ THE TWO PAINTED LANES ARE THE PAINTED DIRT. The bend points on the flats and slip edges have to
        /// be the terrain painter's own path points, in child→parent order — the same points
        /// <c>StPetersStarterSplat</c> dabs the dirt along. If these ever come apart, a villager walks
        /// beside the path the ground draws, and both halves look correct on their own.
        /// </summary>
        [Test]
        public void ThePaintedLanesCarryTheTerrainPaintersOwnBendPoints_Reversed()
        {
            StPetersRoutines.LaneTable t = StPetersRoutines.BuildLaneTable();

            AssertPaintedEdge(t, StPetersRoutines.LaneSlipHead,
                              StPetersStarterSplat.VillageToSlipPath());
            AssertPaintedEdge(t, StPetersRoutines.LaneFlatsHead,
                              StPetersStarterSplat.VillageToBarHeadPath());
        }

        static void AssertPaintedEdge(StPetersRoutines.LaneTable t, string nodeName, Vector2[] painted)
        {
            int i = t.IndexOf(nodeName);
            Assert.That(i, Is.GreaterThanOrEqualTo(0), $"no lane node '{nodeName}'");
            int expected = Mathf.Max(0, painted.Length - 2);
            Assert.That(t.ViaCount[i], Is.EqualTo(expected),
                        $"'{nodeName}' should carry the painted path's {expected} interior bend(s).");

            for (int k = 0; k < expected; k++)
            {
                Vector2 got = t.Via[t.ViaStart[i] + k];
                Vector2 want = painted[painted.Length - 2 - k];   // child→parent = the painter's, reversed
                Assert.That(Vector2.Distance(got, want), Is.LessThan(1e-3f),
                            $"'{nodeName}' bend {k} is {got}, the painter's is {want}. A villager would " +
                            "walk the mirror image of the dirt — the same length, the wrong side.");
            }
        }

        [Test]
        public void TheWoodsKeepOffThePaintedLanes()
        {
            // The rule itself, at the middle of the longest painted path: a tree may not stand in the tread.
            Vector2[] slip = StPetersStarterSplat.VillageToSlipPath();
            Vector2 mid = Vector2.Lerp(slip[slip.Length / 2], slip[slip.Length / 2 + 1], 0.5f);
            Assert.That(StPetersWoods.OnPaintedPath(mid), Is.True,
                        "a point on the painted dirt must read as ON the path, or the keep-out does nothing");

            // …and it is a footpath, not a firebreak: step PathClearance + a metre off and trees are legal
            // again (as far as this rule is concerned).
            Vector2 dir = (slip[slip.Length / 2 + 1] - slip[slip.Length / 2]).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2 off = mid + perp * (StPetersWoods.PathClearance + 1f);
            Assert.That(StPetersWoods.OnPaintedPath(off), Is.False,
                        $"{StPetersWoods.PathClearance:0.#} m either side is a lane through the trees; a " +
                        "keep-out that reached further would read as a firebreak.");
        }

        // ---- the derived station geometry --------------------------------------------------------

        [Test]
        public void TheThreeCustomerSlotsAreDistinct_AndAllWithinStallReachOfTheKeeper()
        {
            Vector2 keeper = StPetersInhabitants.Dooryard(StPetersBuilder.GeneralStorePos);
            var slots = new[]
            {
                StPetersRoutines.CustomerSlot(-1),
                StPetersRoutines.CustomerSlot(0),
                StPetersRoutines.CustomerSlot(1),
            };

            for (int i = 0; i < slots.Length; i++)
                for (int j = i + 1; j < slots.Length; j++)
                    Assert.That(Vector2.Distance(slots[i], slots[j]),
                                Is.GreaterThan(StPetersRoutines.CustomerSlotStrideMetres * 0.75f),
                                "two customers at once must read as two people, not one flickering sprite");

            // The stall reach every counter interaction uses. Kept as the bound rather than a literal so a
            // retuned counter stride fails here with the number.
            const float stallReach = 4f;
            foreach (Vector2 slot in slots)
                Assert.That(Vector2.Distance(slot, keeper), Is.LessThan(stallReach),
                            $"a customer slot {Vector2.Distance(slot, keeper):0.00} m from the keeper is " +
                            $"outside the {stallReach:0.#} m stall reach — the shop and the shopkeeper come " +
                            "apart into two things to walk up to");
        }

        [Test]
        public void TheCustomerSideIsTheFarSideOfTheCounterFromTheKeeper()
        {
            Vector2 keeper = StPetersInhabitants.Dooryard(StPetersBuilder.GeneralStorePos);
            Vector2 counter = StPetersBuilder.GeneralStoreCounterPos;
            Vector2 customer = StPetersRoutines.CustomerSlot(0);

            Assert.That(Vector2.Distance(customer, keeper), Is.GreaterThan(Vector2.Distance(counter, keeper)),
                        "the customer stands PAST the counter, not behind it with the storekeeper");
            Assert.That(Vector2.Distance(customer, StPetersBuilder.VillageGreen),
                        Is.LessThan(Vector2.Distance(keeper, StPetersBuilder.VillageGreen)),
                        "…and on the lane side of it, which is where you walk up from");
        }

        /// <summary>
        /// ⭐ NO TWO VILLAGERS MAY BE SENT TO ONE POINT AT ONE TIME. The first build's muster read
        /// "Marguerite LeBlanc at (3,7) · Rose MacIsaac at (3,7)" — two women standing inside each other on
        /// the green, which is exactly the failure the store counter's slots were built to avoid and which
        /// the green had not been given. Slots fixed it; this is what stops it coming back the next time
        /// somebody's evening is retimed.
        ///
        /// <para>Checked as a property of the CONTENT rather than of a screenshot: any two blocks that name
        /// the same station and whose windows overlap in time are a collision, whatever the geometry.</para>
        /// </summary>
        [Test]
        public void NoTwoVillagersAreSentToTheSameStationAtTheSameTime()
        {
            RoutineDef[] routines = Routines();

            // Every (station, window) any villager occupies. A window runs from the block's departure to
            // the next block's, wrapping midnight — the same tiling GapHours describes.
            var occupancy = new List<(string station, string who, float from, float to)>();
            foreach (RoutineDef r in routines)
            {
                var hours = r.Entries.Select(e => e.StartHour).ToArray();
                for (int i = 0; i < r.Entries.Length; i++)
                {
                    float from = hours[i];
                    float to = from + RoutineSchedule.GapHours(hours, i);
                    occupancy.Add((r.Entries[i].StationId, r.Id, from, to));
                }
            }

            for (int a = 0; a < occupancy.Count; a++)
                for (int b = a + 1; b < occupancy.Count; b++)
                {
                    if (occupancy[a].who == occupancy[b].who) continue;
                    if (occupancy[a].station != occupancy[b].station) continue;
                    Assert.That(WindowsOverlap(occupancy[a].from, occupancy[a].to,
                                               occupancy[b].from, occupancy[b].to), Is.False,
                                $"'{occupancy[a].who}' and '{occupancy[b].who}' are both at " +
                                $"'{occupancy[a].station}' between " +
                                $"{Mathf.Max(occupancy[a].from, occupancy[b].from):0.00} and " +
                                $"{Mathf.Min(occupancy[a].to, occupancy[b].to):0.00} — they would stand " +
                                "inside each other. Give one of them another slot (the counter and the " +
                                "green both have several).");
                }
        }

        /// <summary>Do two midnight-wrapping windows overlap? Both are unrolled onto a two-day line so the
        /// one that spans 00:00 is compared as one interval rather than as two.</summary>
        static bool WindowsOverlap(float a0, float a1, float b0, float b1)
        {
            for (int shift = -1; shift <= 1; shift++)
            {
                float s = shift * 24f;
                if (a0 < b1 + s && b0 + s < a1) return true;
            }
            return false;
        }

        [Test]
        public void TheGreensSlotsAreDistinct_AndTheCentreOneIsGinnys()
        {
            var slots = new List<Vector2>();
            for (int i = 0; i < StPetersRoutines.GreenSlotIds.Length; i++)
                slots.Add(StPetersRoutines.GreenSlot(i - 1));

            for (int i = 0; i < slots.Count; i++)
                for (int j = i + 1; j < slots.Count; j++)
                    Assert.That(Vector2.Distance(slots[i], slots[j]),
                                Is.GreaterThanOrEqualTo(StPetersRoutines.GreenSlotStrideMetres * 0.9f),
                                "four neighbours on the green have to be four people");

            // Index 1 is the offset-0 slot — the green itself.
            Assert.That(slots[1], Is.EqualTo(StPetersBuilder.VillageGreen));

            // ⚠ THE CENTRE SLOT IS NOBODY'S NOW, AND THAT IS A DECISION. It was Ginny's, "because it is
            // the one nearest the step the opening's first conversation happens on" — a reason the
            // 2026-08-16 move made false, since her step is 85 m east in the woods. Her dusk block went
            // with it, so station.st_peters.green_b is declared and unvisited. The alternative was
            // walking her back across the island at dusk for a slot she no longer lives beside, and at
            // 1.13 game hours per crossing that would have put her at 2.3 h/day on the road.
            var ginny = RoutineDefsBuilder.Roster.First(d => d.NpcAsset == "AuntGinny");
            Assert.That(ginny.Blocks.Any(b => b.StationId == StPetersRoutines.StationGreenB), Is.False,
                        "Ginny's dusk-on-the-green block came back. If that is intended, re-check the " +
                        "commute arithmetic in her routine's own note first — a fourth crossing is a " +
                        "third of her waking day spent walking.");
        }

        [Test]
        public void HeadingTo_UsesTheProjectsBearingConvention()
        {
            // 0 = North, clockwise — the same convention IsoCharacterSprite reads off motion.
            Assert.That(StPetersRoutines.HeadingTo(Vector2.zero, new Vector2(0f, 5f)),
                        Is.EqualTo(0f).Within(1e-3f));
            Assert.That(StPetersRoutines.HeadingTo(Vector2.zero, new Vector2(5f, 0f)),
                        Is.EqualTo(90f).Within(1e-3f));
            Assert.That(StPetersRoutines.HeadingTo(Vector2.zero, new Vector2(0f, -5f)),
                        Is.EqualTo(180f).Within(1e-3f));
            // Degenerate: face the camera rather than snapping to North.
            Assert.That(StPetersRoutines.HeadingTo(Vector2.zero, Vector2.zero), Is.EqualTo(180f));
        }

        [Test]
        public void HeadingTo_IsAGroundBearing_SoAStatedStanceAgreesWithAWalkedApproach()
        {
            // World XY is the SQUASHED ground plane and the baked character rows are ground bearings
            // (ADR 0034), so this must un-squash — and must un-squash by exactly the same arithmetic the
            // presenter applies to the step it measures, or a villager would turn on arrival.
            var target = new Vector2(5f, 5f);
            Assert.That(StPetersRoutines.HeadingTo(Vector2.zero, target),
                        Is.EqualTo(IsoCharacterMath.GroundHeadingFor(target, 0.01f, 999f)).Within(1e-3f),
                        "a stated stance and a measured walk must be the same number");
            Assert.That(StPetersRoutines.HeadingTo(Vector2.zero, target), Is.EqualTo(32.73f).Within(0.02f),
                        "a world-XY diagonal is 32.7° of ground bearing, not 45°");
        }

        [Test]
        public void GinnysGardenAndHerStep_AreBothOnHerOwnPlot()
        {
            // Her home is her plot in the eastern woods now (2026-08-16), so "inside her own dooryard"
            // means inside the clearing that plot declares — she may not garden under a spruce, and her
            // step may not be somewhere the woods planter is allowed to plant.
            Vector2 step = StPetersGinnyPlot.Dooryard;
            Vector2 garden = StPetersGinnyPlot.GardenPos;
            Vector2 cottage = StPetersGinnyPlot.CottagePos;

            Assert.That(Vector2.Distance(step, cottage),
                        Is.LessThan(StPetersGinnyPlot.ClearingRadius),
                        "Ginny's step is outside her own clearing — the woods would close over it.");
            Assert.That(Vector2.Distance(garden, cottage),
                        Is.LessThan(StPetersGinnyPlot.ClearingRadius),
                        "Ginny's garden is outside her own clearing — she would be weeding under a canopy.");

            Assert.That(Vector2.Distance(garden, step), Is.GreaterThan(2f),
                        "…and the garden is not her own doorstep: two stations a stride apart are one " +
                        "station with two names.");
            Assert.That(Vector2.Distance(garden, StPetersGinnyPlot.FreezerPos), Is.GreaterThan(2f),
                        "…nor inside the freezer, which moved out here with her.");

            foreach (var shed in StPetersGinnyPlot.Sheds)
                Assert.That(Vector2.Distance(garden, shed.Position),
                            Is.GreaterThan(shed.FootprintRadiusMetres + 1f),
                            $"…nor inside the {shed.Key}.");
        }

        /// <summary>
        /// 🔴 <b>THE TEST THE WHOLE MOVE HANGS ON.</b> A new game starts at hour 6
        /// (<c>GameClock._startHour</c>) and the opening's first beat is talking to the aunt, so at 06:00
        /// Ginny must be STANDING on her village mark — not still walking in from the woods.
        ///
        /// <para>That is not a matter of picking a nice-looking departure hour, because
        /// <c>RoutineSchedule</c> treats <c>StartHour</c> as a DEPARTURE and the walk is charged in game
        /// hours: 85 m at 1 m/s is 85 real seconds, and a game hour is
        /// <c>SecondsPerDay / 24</c> = 75 real seconds. So the crossing costs about 1.13 game hours and
        /// she has to leave before ~04:82 to make six. This test does that arithmetic against the shipped
        /// numbers rather than trusting the comment — if the owner lengthens the day, shortens it, moves
        /// her plot or slows her down, it fails here with the figure to use.</para>
        /// </summary>
        [Test]
        public void GinnyIsStandingOnTheOpeningsMark_ByTheHourANewGameStartsAt()
        {
            const float GameStartHour = 6f;   // GameClock._startHour

            var ginny = RoutineDefsBuilder.Roster.First(d => d.NpcAsset == "AuntGinny");

            // The block running at 06:00 is the last one whose departure hour is at or before it.
            var running = ginny.Blocks
                               .Where(b => b.StartHour <= GameStartHour)
                               .OrderBy(b => b.StartHour)
                               .LastOrDefault();

            Assert.That(running.StationId, Is.EqualTo(StPetersRoutines.StationGinnyVillageMark),
                        $"at {GameStartHour:0.0} the block running is '{running.StationId}'. The opening's " +
                        "first beat is talking to the aunt beside the spawn — she has to be walking to, " +
                        "or standing on, her village mark.");

            // …and she must have ARRIVED, not still be on the road.
            float metres = Vector2.Distance(StPetersGinnyPlot.Dooryard,
                                            (Vector2)StPetersBuilder.GinnyPos);
            float secondsPerGameHour = GameConfig.DefaultSecondsPerDay / 24f;
            float travelHours = metres / ginny.WalkSpeed / secondsPerGameHour;
            float jitterHours = ginny.JitterMinutes / 60f;
            float arrival = running.StartHour + travelHours + jitterHours;

            Assert.That(arrival, Is.LessThan(GameStartHour),
                        $"Ginny leaves her plot at {running.StartHour:0.00} and the {metres:0.0} m walk " +
                        $"costs {travelHours:0.00} game hours (+{jitterHours:0.00} h of jitter), so she " +
                        $"is still on the road at {arrival:0.00} — after the {GameStartHour:0.0} the game " +
                        "starts at. The player would wake to an empty mark. Move her departure earlier.");

            Debug.Log($"[stpeters-ginny] commute {metres:0.0} m at {ginny.WalkSpeed:0.0} m/s = " +
                      $"{travelHours:0.00} game hours; departs {running.StartHour:0.00}, on the mark by " +
                      $"{arrival:0.00}, game starts {GameStartHour:0.0}.");
        }

        /// <summary>
        /// Sabotage for the test above: prove the arrival check BITES. Her old 6.50 departure — the hour
        /// she left for the garden with before the move — would strand her on the road past six now that
        /// the walk is 85 m instead of 5.
        /// </summary>
        [Test]
        public void Sabotage_HerOldDepartureHour_WouldStrandHerOnTheRoadPastTheOpening()
        {
            const float GameStartHour = 6f;
            const float OldDepartureHour = 6.5f;   // what she left at while she lived in the village

            var ginny = RoutineDefsBuilder.Roster.First(d => d.NpcAsset == "AuntGinny");
            float metres = Vector2.Distance(StPetersGinnyPlot.Dooryard,
                                            (Vector2)StPetersBuilder.GinnyPos);
            float travelHours = metres / ginny.WalkSpeed / (GameConfig.DefaultSecondsPerDay / 24f);

            Assert.That(OldDepartureHour + travelHours, Is.GreaterThan(GameStartHour),
                        "her pre-move departure hour was supposed to be too late for the new commute — " +
                        "if it is not, the arrival check above is not constraining anything and the " +
                        "departure hour is unexplained.");
        }

        [Test]
        public void JuniorsAndBasilsWorkingSpots_AreTheONESStPetersInhabitantsPlacedThemOn()
        {
            // Not a copy: the lane that ends at a man's working spot has to end where the man is standing.
            Vector2 junior = StPetersRoutines.JuniorSpot;
            Vector2 basil = StPetersRoutines.BasilSpot;

            var placed = StPetersInhabitants.People
                .ToDictionary(p => p.AssetName, p => p.Position, System.StringComparer.Ordinal);
            Assert.That(junior, Is.EqualTo(placed["JuniorPoirier"]));
            Assert.That(basil, Is.EqualTo(placed["BasilSamson"]));
        }

        [Test]
        public void BasilsSpotIsOnTheWharfDeck_AndTheSlipLaneEndsOnItToo()
        {
            Rect deck = StPetersWharf.DeckFootprint();
            Assert.That(deck.Contains(StPetersRoutines.BasilSpot), Is.True,
                        "the man on the wharf stands on the planks");

            StPetersRoutines.LaneTable t = StPetersRoutines.BuildLaneTable();
            int slip = t.IndexOf(StPetersRoutines.LaneSlipHead);
            Assert.That(slip, Is.GreaterThanOrEqualTo(0));
            Assert.That(deck.Contains(t.Positions[slip]), Is.True,
                        "the painted path east ends ON the deck (BerthTo is inside the footprint), which is " +
                        "why the deck is a legal footing for a LANE and not only for a person");
        }

        [Test]
        public void EileensSunsetSpotIsInlandOfJuniorsDigging_SoItCannotBeWetterGround()
        {
            Vector2 junior = StPetersRoutines.JuniorSpot;
            Vector2 lookout = StPetersRoutines.Toward(junior, StPetersBuilder.VillageGreen,
                                                      StPetersRoutines.BesideStationMetres);
            Assert.That(Vector2.Distance(lookout, junior),
                        Is.EqualTo(StPetersRoutines.BesideStationMetres).Within(1e-3f),
                        "far enough apart to be two people");
            Assert.That(Vector2.Distance(lookout, StPetersBuilder.VillageGreen),
                        Is.LessThan(Vector2.Distance(junior, StPetersBuilder.VillageGreen)),
                        "the offset goes TOWARD the village, so it can only ever move a station onto higher " +
                        "ground than the one it was derived from");
        }

        // ---- the whole roster, walked ------------------------------------------------------------

        /// <summary>
        /// ⭐ THE ONE THAT CATCHES A BAD DAY. Every leg of every villager's day has to fit inside the block
        /// it starts in, or that villager is still on the lane when her next block begins and never arrives
        /// anywhere. It is checked here against the ROUTE LENGTHS the region actually derives (through the
        /// lane tree), so it fails when a building moves — not only when a time is retyped.
        ///
        /// <para>This test builds no scene, so it cannot see the interiors and skips any block that ends
        /// inside one. Those legs are checked by the builder itself, which has the rooms standing and logs
        /// the arithmetic per person.</para>
        /// </summary>
        [Test]
        public void EveryOutdoorLegFitsInsideItsOwnBlock()
        {
            StPetersRoutines.LaneTable t = StPetersRoutines.BuildLaneTable();
            var indoorish = new HashSet<string>(new[]
            {
                StPetersRoutines.StationHomeSaltbox, StPetersRoutines.StationHomeFarmhouse,
                StPetersRoutines.StationHomeSageA, StPetersRoutines.StationHomeSageB,
                StPetersRoutines.StationHomeStore, StPetersRoutines.StationSchoolDesk,
                StPetersRoutines.StationPostOfficeCounter,
            }, System.StringComparer.Ordinal);

            float secondsPerGameHour = GameConfig.DefaultSecondsPerDay / RoutineSchedule.HoursPerDay;
            var nodePath = new int[t.Count * 2 + 2];
            var poly = new Vector2[t.Count + t.Via.Length + 8];
            var checkedLegs = new List<string>();

            foreach (RoutineDef r in Routines())
            {
                var hours = r.Entries.Select(e => e.StartHour).ToArray();
                for (int i = 0; i < r.Entries.Length; i++)
                {
                    int from = (i - 1 + r.Entries.Length) % r.Entries.Length;
                    string a = r.Entries[from].StationId, b = r.Entries[i].StationId;
                    if (indoorish.Contains(a) || indoorish.Contains(b)) continue;

                    int na = t.IndexOf(LaneNodeFor(a)), nb = t.IndexOf(LaneNodeFor(b));
                    Assert.That(na, Is.GreaterThanOrEqualTo(0), $"no lane node for '{a}'");
                    Assert.That(nb, Is.GreaterThanOrEqualTo(0), $"no lane node for '{b}'");

                    int count = RoutineLaneTree.WriteNodePath(t.Parents, na, nb, nodePath);
                    int pc = RoutineLaneTree.WritePolyline(t.Parents, t.Positions, t.ViaStart, t.ViaCount,
                                                           t.Via, nodePath, count, poly);
                    Assert.That(pc, Is.GreaterThan(0));

                    // The lane distance is the bulk of it; the station's own offset from its node adds a
                    // few metres, so the bound is generous by design — it is looking for a leg that cannot
                    // possibly fit, not for a tight one.
                    float metres = RoutineLaneTree.PolylineLength(poly, 0, pc);
                    float travel = RoutineSchedule.TravelHours(metres, r.WalkSpeedMetresPerSecond,
                                                               secondsPerGameHour);
                    float block = RoutineSchedule.GapHours(hours, i);

                    Assert.That(travel, Is.LessThan(block),
                                $"'{r.Id}' block {i} ({a} → {b}) is {metres:0} m, which is " +
                                $"{travel:0.00} game h at {r.WalkSpeedMetresPerSecond:0.00} m/s, inside a " +
                                $"{block:0.00} h block. They would still be walking when the next block " +
                                "began. Give the block more hours, shorten the walk, or walk them faster.");
                    checkedLegs.Add($"{r.Id}#{i} {metres:0}m/{travel:0.00}h in {block:0.00}h");
                }
            }

            Assert.That(checkedLegs.Count, Is.GreaterThan(5),
                        "this test has to actually check some legs, or it passes vacuously: " +
                        string.Join(", ", checkedLegs));
        }

        // ---- the living-grass wiring -------------------------------------------------------------

        /// <summary>
        /// ⭐ EVERY WALKING VILLAGER TREADS THE GRASS — AND NONE OF THEM OUTRANKS THE PLAYER. The trail is
        /// a pool of <see cref="GrassFootstep.MaxWalkers"/> slots (#517) and eviction is strictly-below
        /// priority, so a villager built at the ambient default 0 can lose their trail to the player
        /// (cosmetic) but can never cost the player theirs. A villager built AT
        /// <see cref="GrassFootstep.PlayerPriority"/> would — which is exactly the mistake this pins out.
        /// (Scene-wired is not builder-wired: this pins the builder pass; the shipped scene needs the
        /// owner's next Build St Peters click to carry it.)
        /// </summary>
        [Test]
        public void WireLivingGrass_MakesTheMeadowAnswerAVillager_AtTheRankBelowThePlayer()
        {
            var go = new GameObject("TestVillager");
            try
            {
                go.AddComponent<SpriteRenderer>();
                var ysort = go.AddComponent<YSortSprite>();

                StPetersRoutines.WireLivingGrass(go);

                var footstep = go.GetComponent<GrassFootstep>();
                Assert.That(footstep, Is.Not.Null,
                            "a villager on a routine walks the green — the grass has to answer them");
                Assert.That(footstep.Priority, Is.EqualTo(0),
                            "ambient walkers stay at the default rank: eviction is strictly-below, so 0 " +
                            "can never evict the player's PlayerPriority claim, and equals never churn " +
                            "each other");
                Assert.That(footstep.Priority, Is.LessThan(GrassFootstep.PlayerPriority),
                            "a villager who ranked at or above the player could cost them their trodden " +
                            "path on a region hop");
                Assert.That(ysort.Dynamic, Is.True,
                            "a walking villager re-sorts by Y as they go, or they draw in front of the " +
                            "house they just walked behind");

                // Idempotent: the owner rebuilds the region repeatedly, and a second pass must not stack
                // a second component (a duplicate would claim a second pool slot for the same walker).
                StPetersRoutines.WireLivingGrass(go);
                Assert.That(go.GetComponents<GrassFootstep>().Length, Is.EqualTo(1),
                            "re-running the builder must reuse the component, not stack another");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Which lane node a station hangs off — mirrored from the builder's own table, and only
        /// for the outdoor stations the test above walks. It is a duplicate of one fact, which is the price
        /// of checking the arithmetic without standing a scene up; the pairs are asserted to exist above, so
        /// a rename fails here rather than passing quietly.</summary>
        static string LaneNodeFor(string stationId)
        {
            if (System.Array.IndexOf(StPetersRoutines.GreenSlotIds, stationId) >= 0)
                return StPetersRoutines.LaneGreen;
            // ⚠ Ginny's step and garden hang off her PLOT since 2026-08-16, not off the village. The
            // mark she keeps in the village is a station of its own and is the one that still hangs off
            // the old cottage node. Getting these two rows the wrong way round is not cosmetic: it is
            // what makes the leg-length arithmetic above measure a 7 m stroll instead of an 85 m walk.
            if (stationId == StPetersRoutines.StationCottageStep ||
                stationId == StPetersRoutines.StationCottageGarden) return StPetersRoutines.LaneGinnyPlot;
            if (stationId == StPetersRoutines.StationGinnyVillageMark) return StPetersRoutines.LaneCottage;
            if (stationId == StPetersRoutines.StationSaltboxDooryard) return StPetersRoutines.LaneSaltbox;
            if (stationId == StPetersRoutines.StationFarmhouseDooryard) return StPetersRoutines.LaneFarmhouse;
            if (stationId == StPetersRoutines.StationSageDooryard) return StPetersRoutines.LaneSageCottage;
            if (stationId == StPetersRoutines.StationStoreCounter ||
                stationId == StPetersRoutines.StationStoreCustomerA ||
                stationId == StPetersRoutines.StationStoreCustomerB ||
                stationId == StPetersRoutines.StationStoreCustomerC) return StPetersRoutines.LaneStore;
            if (stationId == StPetersRoutines.StationPostOfficeDoor) return StPetersRoutines.LanePostOffice;
            if (stationId == StPetersRoutines.StationFlatsHead ||
                stationId == StPetersRoutines.StationFlatsLookout) return StPetersRoutines.LaneFlatsHead;
            if (stationId == StPetersRoutines.StationSlipHead) return StPetersRoutines.LaneSlipHead;
            if (stationId == StPetersRoutines.StationWharfHead) return StPetersRoutines.LaneWharfHead;
            return string.Empty;
        }
    }
}
