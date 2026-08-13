using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
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

            // Index 1 is the offset-0 slot — the green itself — and it is Ginny's, because it is the one
            // nearest the step the opening's first conversation happens on.
            Assert.That(slots[1], Is.EqualTo(StPetersBuilder.VillageGreen));
            var ginny = RoutineDefsBuilder.Roster.First(d => d.NpcAsset == "AuntGinny");
            Assert.That(ginny.Blocks.Any(b => b.StationId == StPetersRoutines.StationGreenB), Is.True,
                        "Ginny keeps the centre slot: she may not be walked further from her own step than " +
                        "she has to be.");
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
        public void GinnysGarden_StaysInsideHerOwnDooryard()
        {
            // Her whole day is deliberately the smallest on the island: she is the onboarding gate, and the
            // opening cannot afford her to be somewhere else on the first morning.
            Vector2 step = StPetersBuilder.GinnyPos;
            Vector2 garden = new Vector2(StPetersBuilder.CottagePos.x, StPetersBuilder.CottagePos.y)
                             + StPetersRoutines.CottageGardenOffset;

            Assert.That(Vector2.Distance(garden, step), Is.LessThan(12f),
                        "Ginny may not wander off the mark the opening's first conversation happens on.");
            Assert.That(Vector2.Distance(garden, StPetersBuilder.NedsLetterPos), Is.GreaterThan(2f),
                        "…and not stand on Ned's letter, which is the other thing on that step.");
            Assert.That(Vector2.Distance(garden, StPetersBuilder.FreezerPos), Is.GreaterThan(2f),
                        "…nor inside the freezer round the side.");
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

        /// <summary>Which lane node a station hangs off — mirrored from the builder's own table, and only
        /// for the outdoor stations the test above walks. It is a duplicate of one fact, which is the price
        /// of checking the arithmetic without standing a scene up; the pairs are asserted to exist above, so
        /// a rename fails here rather than passing quietly.</summary>
        static string LaneNodeFor(string stationId)
        {
            if (System.Array.IndexOf(StPetersRoutines.GreenSlotIds, stationId) >= 0)
                return StPetersRoutines.LaneGreen;
            if (stationId == StPetersRoutines.StationCottageStep ||
                stationId == StPetersRoutines.StationCottageGarden) return StPetersRoutines.LaneCottage;
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
