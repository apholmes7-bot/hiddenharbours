using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>A villager angler wired to his routine</b> (ADR 0053 §11, PR 2 of the angler seam): what a routine
    /// block names him doing rides the plan; his station window on the flow clock is a pure function of the
    /// hour; a slot begins only inside it; his catch is a pure function of (seed, angler, slot, spot) through
    /// the Fishing module's shore facts; his look is the player's rod presenter's, as data; and the content
    /// that names an activity resolves, is valid where it stands, and draws every fish it can land.
    /// </summary>
    public sealed class NpcAnglerWiringTests
    {
        private const float SecondsPerGameHour = 1800f / 24f;   // the pinned 1800 s day
        private const int Seed = 1234;
        private const string AnglerKey = "npc.test_angler";

        private const string RoutinesFolder = "Assets/_Project/Data/Routines";
        private const string ActivitiesFolder = "Assets/_Project/Data/NPCs/Activities";
        private const string FishFolder = "Assets/_Project/Data/Fish";
        private const string ScenesFolder = "Assets/_Project/Scenes";
        private const string StPetersScenePath = "Assets/_Project/Scenes/StPeters.unity";
        private const string WharfRodPath = "Assets/_Project/Data/NPCs/Activities/WharfRodActivity.asset";
        private const string WharfSpotId = "spot.st_peters.wharf_head";

        private static readonly Regex ActivityId = new Regex(@"^npc_activity\.[a-z0-9]+(_[a-z0-9]+)*$");

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ================================================================ the routine slot

        [Test]
        public void ABlocksDoing_RidesThePlan_AndNoOtherBlockDoesAnything()
        {
            NpcAnglingActivityDef rod = MakeAngling();
            RoutinePlan plan = Plan(Seed, rod, out RoutineDef routine);
            RoutineStop[] stops = RoutinePlanner.ResolveStops(routine, _stations, _lanes, Seed, out string problem);
            Assert.IsNotNull(stops, problem);

            Assert.AreEqual(4, plan.BlockCount);
            for (int i = 0; i < plan.BlockCount; i++)
            {
                NpcActivityDef want = i == AnglingBlock ? rod : null;
                Assert.AreSame(want, plan.DoingAt(i), $"block {i} does the wrong thing");
                Assert.AreSame(want, stops[i].Doing, $"stop {i} lost what its entry does");
            }
            Assert.IsNull(plan.DoingAt(-1));
            Assert.IsNull(plan.DoingAt(plan.BlockCount));

            // A plan built by hand with no activities answers none, everywhere.
            var bare = new RoutinePlan(new[] { 1f, 2f }, null, null, null, null, null, null, null, null, 1f);
            Assert.IsNull(bare.DoingAt(0));
            Assert.IsNull(bare.DoingAt(1));
        }

        [Test]
        public void TheStationWindow_IsTheBlockLessHisWalk_AndNegativeOnlyWhileHeWalks()
        {
            RoutinePlan plan = Plan(Seed, MakeAngling(), out _);
            int checkedHours = 0;
            for (float h = 0f; h < 24f; h += 0.05f)
            {
                plan.StationWindowAt(h, SecondsPerGameHour, out int block, out float since, out float until);
                int next = (block + 1) % plan.BlockCount;
                float length = RoutineSchedule.ElapsedHours(plan.DepartureHours[next], plan.DepartureHours[block]);
                float travel = plan.TravelHours(block, SecondsPerGameHour);
                Assert.AreEqual(length - travel, since + until, 1e-3f, $"{h:F2} h: the window is not the block less the walk");
                Assert.Greater(until, 0f, $"{h:F2} h: he has already left");

                RoutinePose pose = plan.SampleAt(h, SecondsPerGameHour);
                Assert.AreEqual(block, pose.BlockIndex, $"{h:F2} h: the window and the pose disagree on the block");
                // Away from the arrival instant itself, walking and a negative window go together.
                if (Mathf.Abs(since) > 0.01f)
                {
                    Assert.AreEqual(since < 0f, pose.Walking, $"{h:F2} h: since {since:F3} h but walking={pose.Walking}");
                    checkedHours++;
                }
            }
            Assert.Greater(checkedHours, 400, "harness: the day was barely sampled");
        }

        [Test]
        public void TheFlowWindow_IsAPureFunctionOfTheHour_AndASlotBeginsOnlyInsideIt()
        {
            NpcAnglingActivityDef rod = MakeAngling();
            RoutinePlan plan = Plan(Seed, rod, out _);
            AnglerScript script = rod.Script;
            uint key = FigureKeyHash.Of(AnglerKey);
            const double DayStart = 1800d;   // day 1, as a plate pins it

            int begun = 0, refused = 0;
            for (float h = 0f; h < 24f; h += 0.1f)
            {
                double flow = DayStart + h * SecondsPerGameHour;
                NpcActivityDef a = NpcAnglerWindow.At(plan, h, SecondsPerGameHour, flow, out int block,
                                                      out double arrival, out double departure);
                NpcActivityDef b = NpcAnglerWindow.At(plan, h, SecondsPerGameHour, flow, out int block2,
                                                      out double arrival2, out double departure2);
                Assert.AreSame(a, b);
                Assert.AreEqual(block, block2);
                Assert.AreEqual(arrival, arrival2, "the arrival moved between two equal asks");
                Assert.AreEqual(departure, departure2, "the departure moved between two equal asks");
                Assert.AreSame(plan.DoingAt(block), a);
                Assert.Greater(departure, flow);

                if (a == null) continue;
                Assert.AreEqual(AnglingBlock, block);
                long slot = AnglerTimeline.SlotIndex(in script, Seed, key, flow);
                double start = AnglerTimeline.SlotStartSeconds(in script, Seed, key, slot);
                bool may = AnglerTimeline.SlotMayBegin(in script, Seed, key, slot, arrival, departure);
                bool inside = start >= arrival && start + script.SlotSeconds <= departure;
                Assert.AreEqual(inside, may, $"{h:F1} h: slot {slot} at {start:F1}s in [{arrival:F1}, {departure:F1}]");
                if (may) begun++; else refused++;
            }
            Assert.Greater(begun, 0, "he never fished his block");
            Assert.Greater(refused, 0, "harness: no slot fell across an edge of the window (the gate is untested)");
        }

        [Test]
        public void TheWindow_IsOnTheSameClockForAnyFlowReading_ShiftedByTheReading()
        {
            RoutinePlan plan = Plan(Seed, MakeAngling(), out _);
            const float Hour = 16.25f;
            NpcAnglerWindow.At(plan, Hour, SecondsPerGameHour, 5000d, out _, out double a1, out double d1);
            NpcAnglerWindow.At(plan, Hour, SecondsPerGameHour, 9000d, out _, out double a2, out double d2);
            Assert.AreEqual(4000d, a2 - a1, 1e-6);
            Assert.AreEqual(4000d, d2 - d1, 1e-6);
            Assert.AreEqual(d1 - a1, d2 - a2, 1e-6, "the window's length depends on the reading");

            Assert.IsNull(NpcAnglerWindow.At(null, Hour, SecondsPerGameHour, 1d, out int none, out _, out _));
            Assert.AreEqual(-1, none);
        }

        // ================================================================ the catch

        [Test]
        public void TheShoreCatch_IsAPureFunctionOfSeedAnglerSlotAndSpot()
        {
            ShoreCatchFacts facts = WharfFacts(out ShoreSpotDef spot, out Dictionary<string, FishSpeciesDef> species);
            ShoreCatchFacts fresh = WharfFacts(out _, out _);
            uint key = FigureKeyHash.Of(AnglerKey);
            const double SlotSeconds = 40d;

            var outcomes = new List<string>();
            int bites = 0, misses = 0;
            for (long slot = 0; slot < 600; slot++)
            {
                double at = slot * SlotSeconds;
                bool got = facts.TryPick(WharfSpotId, Seed, key, slot, at, out ShoreCatch c);
                bool again = facts.TryPick(WharfSpotId, Seed, key, slot, at, out ShoreCatch c2);
                bool other = fresh.TryPick(WharfSpotId, Seed, key, slot, at, out ShoreCatch c3);
                Assert.AreEqual(got, again);
                Assert.AreEqual(got, other);
                Assert.AreEqual(c.FishId, c2.FishId);
                Assert.AreEqual(c.WeightKg, c2.WeightKg, "the weight moved between two equal asks");
                Assert.AreEqual(c.FishId, c3.FishId);
                Assert.AreEqual(c.WeightKg, c3.WeightKg, "a second facts object landed a different fish");
                outcomes.Add(got ? $"{c.FishId}:{c.WeightKg:R}" : "-");
                if (!got) { misses++; continue; }
                bites++;

                Assert.Contains(c.FishId, spot.SpeciesIds, "a fish from outside the spot's list");
                FishSpeciesDef f = species[c.FishId];
                CatchContext ctx = ShoreCatchFacts.ContextAt(spot, at);
                Assert.IsTrue(CatchResolver.Matches(f, in ctx), $"{c.FishId} is out of its season, hour, tide or gear");
                Assert.That(c.WeightKg, Is.InRange(f.MinWeightKg, f.MaxWeightKg));
                Assert.That(c.Size01, Is.InRange(0f, 1f));
            }
            Assert.Greater(bites, 0, "nothing ever bit");
            Assert.Greater(misses, 0, "everything bit: the bite chance is not rolled");

            // The sabotage controls: the seed, the angler and the spot each move the fish.
            Assert.AreNotEqual(outcomes, Outcomes(facts, WharfSpotId, Seed + 1, key, 600, SlotSeconds), "the seed moves nothing");
            Assert.AreNotEqual(outcomes, Outcomes(facts, WharfSpotId, Seed, FigureKeyHash.Of("npc.someone_else"), 600, SlotSeconds),
                               "the angler moves nothing");
            Assert.AreNotEqual(ShoreCatchFacts.SeedFor(Seed, key, 3, WharfSpotId), ShoreCatchFacts.SeedFor(Seed, key, 3, "spot.elsewhere"));

            Assert.IsFalse(facts.TryPick("spot.nowhere", Seed, key, 0, 0d, out ShoreCatch none));
            Assert.IsNull(none.FishId);
            Assert.IsFalse(facts.TryPick(null, Seed, key, 0, 0d, out _));
        }

        // ================================================================ the look

        [Test]
        public void TheWharfRodLook_EqualsThePlayersRodPresenter_FieldForField()
        {
            var def = AssetDatabase.LoadAssetAtPath<NpcAnglingActivityDef>(WharfRodPath);
            Assert.IsNotNull(def, WharfRodPath);
            AnglerLook look = def.Look;
            PresenterValues p = PlayersPresenter();

            Assert.AreEqual(p.Float("_maxSagM"), look.Line.MaxSagM, "MaxSagM");
            Assert.AreEqual(p.Colour("_lineColor"), look.Line.LineColor, "LineColor");
            Assert.AreEqual(p.Colour("_strainColor"), look.Line.StrainColor, "StrainColor");
            Assert.AreEqual(p.Colour("_slackColor"), look.Line.SlackColor, "SlackColor");
            Assert.AreEqual(p.Float("_whitenLateBias"), look.Line.WhitenLateBias, "WhitenLateBias");
            Assert.AreEqual(p.Float("_shudderStart01"), look.Line.ShudderStart01, "ShudderStart01");
            Assert.AreEqual(p.Float("_shudderAmpM"), look.Line.ShudderAmpM, "ShudderAmpM");
            Assert.AreEqual(p.Float("_shudderHz"), look.Line.ShudderHz, "ShudderHz");
            Assert.AreEqual(p.Float("_restTaut"), look.Line.RestTaut, "RestTaut");
            Assert.AreEqual(p.Float("_sinkTaut"), look.Line.SinkTaut, "SinkTaut");
            Assert.AreEqual(p.Float("_fightTautFloor"), look.Line.FightTautFloor, "FightTautFloor");
            Assert.AreEqual(p.Float("_slackOvershoot01"), look.Line.SlackOvershoot01, "SlackOvershoot01");
            Assert.AreEqual(p.Float("_slackSettleSeconds"), look.Line.SlackSettleSeconds, "SlackSettleSeconds");

            Assert.AreEqual(p.Float("_lineWidth"), look.LineWidthM, "LineWidthM");
            Assert.AreEqual(p.Int("_lineSegments"), look.LineSegments, "LineSegments");
            Assert.AreEqual(p.Float("_fishSecondsPerFrame"), look.FishSecondsPerFrame, "FishSecondsPerFrame");
            Assert.AreEqual(p.Float("_fishDartSpeedMps"), look.FishDartSpeedMps, "FishDartSpeedMps");
            Assert.AreEqual(p.Float("_fightJumpPeriodSeconds"), look.FallbackJumpPeriodSeconds, "FallbackJumpPeriodSeconds");
            Assert.AreEqual(p.Int("_waterElementOrder"), look.WaterElementOrder, "WaterElementOrder");
            Assert.AreEqual(p.Float("_splashFps"), look.SplashFps, "SplashFps");
            Assert.AreEqual(PlayersFishSpeedSmoothing(), look.FishSpeedSmoothing01, "FishSpeedSmoothing01");

            List<string> want = p.SpriteList("_splashFrames");
            Assert.IsNotNull(look.SplashFrames, "no splash frames");
            var got = new List<string>();
            foreach (Sprite s in look.SplashFrames)
            {
                Assert.IsNotNull(s, "a splash frame is missing");
                Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string guid, out long id));
                got.Add($"{id}:{guid}");
            }
            CollectionAssert.AreEqual(want, got, "the splash flipbook is not the player's");
        }

        // ================================================================ the content

        [Test]
        public void EveryRoutinesDoing_Resolves_AndIsValidAtItsStation()
        {
            var rows = new List<(string Where, string Station, NpcActivityDef Doing)>();
            foreach (string guid in AssetDatabase.FindAssets("t:RoutineDef", new[] { RoutinesFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var routine = AssetDatabase.LoadAssetAtPath<RoutineDef>(path);
                Assert.IsNotNull(routine, path);
                int named = Regex.Matches(File.ReadAllText(path), @"(?m)^\s+Doing: \{fileID: [1-9-]").Count;
                int resolved = 0;
                foreach (RoutineEntry e in routine.Entries ?? Array.Empty<RoutineEntry>())
                {
                    if (e.Doing == null) continue;
                    resolved++;
                    rows.Add(($"{routine.Id} {e.StartHour:0.##} h ({e.Doing.Id})", e.StationId, e.Doing));
                }
                Assert.AreEqual(named, resolved, $"{path}: a block names an activity that does not resolve");
            }
            Assert.Greater(rows.Count, 0, "harness: no routine names an activity, so nothing was checked");

            var stationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in rows) stationIds.Add(r.Station);
            Dictionary<string, bool> indoors = StationsIndoors(stationIds);
            foreach (var r in rows)
            {
                Assert.IsTrue(indoors.TryGetValue(r.Station, out bool inside), $"{r.Where}: station {r.Station} is in no scene");
                Assert.IsTrue(r.Doing.ValidAt(inside, out string why), $"{r.Where}: {why}");
            }
        }

        [Test]
        public void EveryActivityDef_HasAUniqueSnakeId_AndEveryAnglerIsPlayableAtAKnownSpot()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int anglers = 0;
            ShoreCatchFacts facts = WharfFacts(out _, out _);
            var library = Resources.Load<ShoreSpotLibrary>(ShoreCatchRegistrar.ResourcesPath);
            Assert.IsNotNull(library, "no shore spot library in Resources");
            ShoreCatchFacts all = library.BuildFacts();
            foreach (string guid in AssetDatabase.FindAssets("t:NpcActivityDef", new[] { ActivitiesFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<NpcActivityDef>(path);
                Assert.IsNotNull(def, path);
                StringAssert.IsMatch(ActivityId.ToString(), def.Id, $"{path}: not npc_activity.snake_case");
                Assert.IsTrue(ids.Add(def.Id), $"{path}: id {def.Id} is taken");
                if (!(def is NpcAnglingActivityDef angling)) continue;
                anglers++;
                Assert.IsTrue(angling.IsPlayable(out string why), $"{path}: {why}");
                Assert.IsNotNull(all.Spot(angling.CatchSpotId), $"{path}: spot {angling.CatchSpotId} is not in the library");
                Assert.That(angling.MaxSeaState01, Is.InRange(0f, 1f));
                Assert.IsFalse(angling.ValidAt(true, out _), $"{path}: an angler was let fish indoors");
            }
            Assert.Greater(anglers, 0, "harness: no angling activity was found");
            Assert.IsNotNull(facts.Spot(WharfSpotId));
        }

        [Test]
        public void EverySpeciesTheWharfCanLand_InAYear_HasAHeldMeshAtEverySize()
        {
            ShoreCatchFacts facts = WharfFacts(out ShoreSpotDef spot, out Dictionary<string, FishSpeciesDef> species);
            FishArtLibrary table = FishArtLibrary.Shared;
            Assert.IsNotNull(table, "no shared fish art table");
            CatchItemLibrary art = table.CatchArt;
            Assert.IsNotNull(art, "the fish art table names no catch art");

            uint key = FigureKeyHash.Of("npc.basil_samson");
            const double SlotSeconds = 40d;
            double year = 4d * GameConfig.DefaultDaysPerSeason * 1800d;
            var landed = new HashSet<string>(StringComparer.Ordinal);
            for (long slot = 0; slot * SlotSeconds < year; slot++)
            {
                if (!facts.TryPick(WharfSpotId, Seed, key, slot, slot * SlotSeconds, out ShoreCatch c)) continue;
                landed.Add(c.FishId);
                Assert.IsNotNull(art.HeldMeshFor(c.FishId, art.SizedKindFor(c.FishId, c.WeightKg, 0f), out _),
                                 $"{c.FishId} at {c.WeightKg:0.###} kg has no held mesh");
            }
            Assert.Greater(landed.Count, 1, "harness: a year of slots landed at most one species");

            // Every rung of each landed species, across its whole weight band, not just the weights rolled.
            const int Steps = 24;
            foreach (string id in landed)
            {
                FishSpeciesDef f = species[id];
                var kinds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i <= Steps; i++)
                {
                    float kg = Mathf.Lerp(f.MinWeightKg, f.MaxWeightKg, i / (float)Steps);
                    string kind = art.SizedKindFor(id, kg, 0f);
                    kinds.Add(kind);
                    Assert.IsNotNull(art.HeldMeshFor(id, kind, out _), $"{id} at {kg:0.###} kg ({kind}) has no held mesh");
                }
                TestContext.WriteLine($"{id}: rungs {string.Join(", ", kinds)}");
            }
            TestContext.WriteLine($"landed in a year at {spot.Id}: {string.Join(", ", landed)}");
        }

        // ================================================================ fishing over the water

        private const float EdgeMarginM = 0.4f;
        private const float EdgeAheadM = 0.65f;   // the deck lip, this far in front of his stand
        private const float ClampTolerance = 1e-4f;

        [Test]
        public void TheEdge_KeepsTheCastAndTheWholeFight_OnTheWaterSide()
        {
            AnglerScript script = MakeAngling().Script;
            var fish = new ShoreCatch("fish.mackerel", 0.6f, 0.5f);
            uint key = FigureKeyHash.Of(AnglerKey);
            var edge = new ShoreEdge(new Vector2(0f, EdgeAheadM), Vector2.up, EdgeMarginM);

            // Facing out (0) the old fight reeled her straight in across the planks; facing the deck (180)
            // the old cast landed on them. With the edge, neither may cross it.
            foreach (float heading in new[] { 0f, 180f })
            {
                int fightBeats = 0, castBeats = 0, wouldHaveCrossed = 0;
                for (double now = 0d; now < script.SlotSeconds * 60d; now += 0.1d)
                {
                    AnglerTimeline.Evaluate(in script, in fish, true, Seed, key, heading, now, in edge, out AnglerBeat beat);
                    AnglerTimeline.Evaluate(in script, in fish, true, Seed, key, heading, now, out AnglerBeat old);
                    FishingState s = beat.State;
                    var aim = new Vector2(s.CastAimX, s.CastAimY);
                    if (aim.sqrMagnitude > 0f)
                    {
                        castBeats++;
                        Assert.That(edge.OutwardDistance(aim), Is.GreaterThanOrEqualTo(EdgeMarginM - ClampTolerance),
                                    $"heading {heading}, t {now:0.0}: the cast lands short of the water");
                    }
                    if (beat.Phase != FishingPhase.FightDeep && beat.Phase != FishingPhase.FightSurface) continue;
                    fightBeats++;
                    var at = new Vector2(s.FishOffsetX, s.FishOffsetY);
                    Assert.That(edge.OutwardDistance(at), Is.GreaterThanOrEqualTo(EdgeMarginM - ClampTolerance),
                                $"heading {heading}, t {now:0.0}: the fish is over the planks");
                    if (edge.OutwardDistance(at) <= EdgeMarginM + ClampTolerance)
                        Assert.That(Vector2.Dot(new Vector2(s.FishRunDirX, s.FishRunDirY), edge.Outward),
                                    Is.GreaterThanOrEqualTo(-ClampTolerance),
                                    $"heading {heading}, t {now:0.0}: held at the edge she still runs in at the deck");
                    if (edge.OutwardDistance(new Vector2(old.State.FishOffsetX, old.State.FishOffsetY)) < EdgeMarginM) wouldHaveCrossed++;
                }
                Assert.Greater(castBeats, 0, $"harness: heading {heading} never cast");
                Assert.Greater(fightBeats, 0, $"harness: heading {heading} never fought a fish");
                Assert.Greater(wouldHaveCrossed, 0, $"sabotage control: unclamped, heading {heading}'s fight never crossed the edge");
            }
        }

        [Test]
        public void AnUnsetEdge_ChangesNothing_BeatForBeat()
        {
            AnglerScript script = MakeAngling().Script;
            var fish = new ShoreCatch("fish.cunner", 0.3f, 0.4f);
            uint key = FigureKeyHash.Of(AnglerKey);
            ShoreEdge none = default;
            Assert.IsFalse(none.IsSet);
            Assert.AreEqual(new Vector2(3f, -4f), none.Clamp(new Vector2(3f, -4f)));
            Assert.IsFalse(none.RelativeTo(Vector2.one).IsSet);
            foreach (bool hasCatch in new[] { true, false })
            for (double now = 0d; now < script.SlotSeconds * 20d; now += 0.25d)
            {
                AnglerTimeline.Evaluate(in script, in fish, hasCatch, Seed, key, 90f, now, out AnglerBeat a);
                AnglerTimeline.Evaluate(in script, in fish, hasCatch, Seed, key, 90f, now, in none, out AnglerBeat b);
                Assert.IsTrue(a.Equals(b), $"t {now:0.00}, catch {hasCatch}: an unset edge moved the beat");
            }
        }

        [Test]
        public void TheWharfSpot_StandsHimFacingOut_BehindTheEdgeHeFishesOver()
        {
            WharfFacts(out ShoreSpotDef spot, out _);
            Assert.IsTrue(spot.TryGetStand(out ShoreStand stand), $"{WharfSpotId} authors no stand");
            Assert.IsTrue(stand.Edge.IsSet, "the stand has no edge");
            Assert.Less(stand.Edge.OutwardDistance(stand.Position), 0f, "he stands out over the water, not on the planks");
            float r = stand.HeadingDegrees * Mathf.Deg2Rad;
            var facing = new Vector2(Mathf.Sin(r), Mathf.Cos(r));
            Assert.Greater(Vector2.Dot(facing, stand.Edge.Outward), 0.95f, "he does not face straight out over the water");
            Assert.That(stand.Edge.MarginM, Is.GreaterThan(0f), "a zero margin lets the fish sprite lap the planks");
        }

        [Test]
        public void WithTheSpotPublished_TheFishingBlockStandsAtTheSpot_AndNoOtherBlockMoves()
        {
            // A spot of the rig's own, its stand a step off the rig's pier station, out over the pier's side.
            var spot = ScriptableObject.CreateInstance<ShoreSpotDef>();
            _spawned.Add(spot);
            spot.Id = "spot.test.pier_side";
            spot.HasStand = true;
            spot.StandPosition = new Vector2(38.5f, 20.6f);
            spot.StandHeadingDegrees = 0f;
            spot.EdgePoint = new Vector2(38.5f, 21.25f);
            spot.EdgeOutwardDegrees = 0f;
            spot.EdgeMarginM = EdgeMarginM;
            var facts = new ShoreCatchFacts(new[] { spot }, _ => null);
            Assert.IsTrue(facts.TryGetStand(spot.Id, out ShoreStand stand));
            Assert.IsFalse(facts.TryGetStand("spot.nowhere", out _));

            IShoreCatchFacts was = GameServices.ShoreCatch;
            try
            {
                GameServices.ShoreCatch = EmptyShoreCatchFacts.Instance;
                NpcAnglingActivityDef rod = MakeAngling();
                rod.CatchSpotId = spot.Id;
                Assert.IsFalse(rod.TryGetStand(out _), "with no Fishing module published there is no stand");
                Plan(Seed, rod, out RoutineDef r0);
                RoutineStop[] before = RoutinePlanner.ResolveStops(r0, _stations, _lanes, Seed, out string p0);
                Assert.IsNotNull(before, p0);

                GameServices.ShoreCatch = facts;
                RoutineStop[] after = RoutinePlanner.ResolveStops(r0, _stations, _lanes, Seed, out string p1);
                Assert.IsNotNull(after, p1);
                RoutinePlan plan = RoutinePlanner.Build(r0, _stations, _lanes, Seed, out _, out string p2);
                Assert.IsNotNull(plan, p2);

                for (int i = 0; i < after.Length; i++)
                {
                    if (i == AnglingBlock) continue;
                    Assert.AreEqual(before[i].Station.Position, after[i].Station.Position, $"block {i} moved");
                    Assert.AreEqual(before[i].Station.StandHeadingDegrees, after[i].Station.StandHeadingDegrees, $"block {i} turned");
                }
                Assert.AreEqual(stand.Position, after[AnglingBlock].Station.Position, "the fishing block is not at the spot's stand");
                Assert.AreEqual(stand.HeadingDegrees, after[AnglingBlock].Station.StandHeadingDegrees);
                Assert.AreEqual(before[AnglingBlock].Station.LaneNodeName, after[AnglingBlock].Station.LaneNodeName,
                                "the stand changed the lane he walks in on");
                Assert.AreEqual(stand.HeadingDegrees, plan.StandHeadingDegrees[AnglingBlock]);
                int last = plan.RouteStart[AnglingBlock] + plan.RouteCount[AnglingBlock] - 1;
                Assert.AreEqual(stand.Position, plan.Waypoints[last], "his walk does not end on the stand");
            }
            finally { GameServices.ShoreCatch = was; }
        }

        // ================================================================ the rig

        private const int AnglingBlock = 2;
        private RoutineLanes _lanes;
        private RoutineStations _stations;

        private NpcAnglingActivityDef MakeAngling()
        {
            var def = ScriptableObject.CreateInstance<NpcAnglingActivityDef>();
            _spawned.Add(def);
            def.Id = "npc_activity.test_rod";
            def.CatchSpotId = WharfSpotId;
            def.MaxSeaState01 = 0.6f;
            def.Rod = ScriptableObject.CreateInstance<HeldPropMeshDef>();
            _spawned.Add(def.Rod);
            def.Script = new AnglerScript
            {
                SlotSeconds = 40f,
                CastBackSeconds = 1f, CastSeconds = 1.2f, WaitMinSeconds = 4f, WaitMaxSeconds = 9f,
                BiteSeconds = 1f, StrikeSeconds = 0.6f,
                DeepMinSeconds = 3f, DeepMaxSeconds = 6f, SurfaceMinSeconds = 3f, SurfaceMaxSeconds = 5f,
                LandSeconds = 3f, EmptyRetrieveSeconds = 3f, LandLiftSeconds = 1f,
                CastReachM = 7f, CastBearingDegrees = 0f, CastBearingJitterDegrees = 25f,
                RunSwingM = 1.2f, TensionMin01 = 0.35f, TensionMax01 = 0.8f, FightWaveHz = 0.45f,
            };
            Assert.IsTrue(def.IsPlayable(out string why), why);
            return def;
        }

        // green(0) -- lane(1) -- pier(2)
        //        \--- shore(3)
        private RoutinePlan Plan(int worldSeed, NpcActivityDef doing, out RoutineDef routine)
        {
            var lanesGo = new GameObject("lanes");
            _spawned.Add(lanesGo);
            _lanes = lanesGo.AddComponent<RoutineLanes>();
            _lanes.Configure(
                nodePositions: new[] { new Vector2(0f, 0f), new Vector2(0f, 20f), new Vector2(40f, 20f), new Vector2(0f, -30f) },
                nodeParents: new[] { RoutineLaneTree.NoParent, 0, 1, 0 },
                nodeNames: new[] { "green", "lane", "pier", "shore" },
                viaStart: new int[4], viaCount: new int[4], via: Array.Empty<Vector2>());
            Assert.That(_lanes.Validate(), Is.Empty, "the rig's lane table must be sound");

            var stationsGo = new GameObject("stations");
            _spawned.Add(stationsGo);
            _stations = stationsGo.AddComponent<RoutineStations>();
            _stations.Configure(new[]
            {
                new RoutineStationEntry { Id = "s.green", Position = new Vector2(0f, 0f), StandHeadingDegrees = 0f, LaneNodeName = "green", Why = "the green" },
                new RoutineStationEntry { Id = "s.shore", Position = new Vector2(0f, -30f), StandHeadingDegrees = 270f, LaneNodeName = "shore", Why = "the shore" },
                new RoutineStationEntry { Id = "s.pier", Position = new Vector2(40f, 20f), StandHeadingDegrees = 90f, LaneNodeName = "pier", Why = "the pier head" },
            });

            routine = ScriptableObject.CreateInstance<RoutineDef>();
            _spawned.Add(routine);
            routine.Id = "routine.test_angler";
            routine.WalkSpeedMetresPerSecond = 1.0f;
            routine.ScheduleJitterMinutes = 6f;
            routine.Entries = new[]
            {
                new RoutineEntry { StartHour = 7f, StationId = "s.green", Activity = RoutineActivity.Work, Why = "test" },
                new RoutineEntry { StartHour = 11f, StationId = "s.shore", Activity = RoutineActivity.Work, Why = "test" },
                new RoutineEntry { StartHour = 15f, StationId = "s.pier", Activity = RoutineActivity.Work, Why = "test", Doing = doing },
                new RoutineEntry { StartHour = 20f, StationId = "s.green", Activity = RoutineActivity.Home, Why = "test" },
            };
            RoutinePlan plan = RoutinePlanner.Build(routine, _stations, _lanes, worldSeed, out _, out string problem);
            Assert.IsNotNull(plan, problem);
            return plan;
        }

        private static ShoreCatchFacts WharfFacts(out ShoreSpotDef spot, out Dictionary<string, FishSpeciesDef> species)
        {
            var lib = Resources.Load<ShoreSpotLibrary>(ShoreCatchRegistrar.ResourcesPath);
            Assert.IsNotNull(lib, "no shore spot library in Resources");
            spot = null;
            foreach (ShoreSpotDef s in lib.Spots) if (s != null && s.Id == WharfSpotId) spot = s;
            Assert.IsNotNull(spot, $"the library has no {WharfSpotId}");

            var map = new Dictionary<string, FishSpeciesDef>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { FishFolder }))
            {
                var f = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (f != null && !string.IsNullOrEmpty(f.Id) && !map.ContainsKey(f.Id)) map[f.Id] = f;
            }
            foreach (string id in spot.SpeciesIds) Assert.IsTrue(map.ContainsKey(id), $"{WharfSpotId} lists {id}, which is no species");
            species = map;
            return new ShoreCatchFacts(lib.Spots, id => id != null && map.TryGetValue(id, out FishSpeciesDef f) ? f : null);
        }

        private static List<string> Outcomes(ShoreCatchFacts facts, string spot, int seed, uint key, int slots, double slotSeconds)
        {
            var list = new List<string>(slots);
            for (long slot = 0; slot < slots; slot++)
                list.Add(facts.TryPick(spot, seed, key, slot, slot * slotSeconds, out ShoreCatch c) ? $"{c.FishId}:{c.WeightKg:R}" : "-");
            return list;
        }

        /// <summary>Is each station indoors? Read off the region scenes' station tables (an entry's
        /// <c>Interior</c> reference), so a test needs no scene open.</summary>
        private static Dictionary<string, bool> StationsIndoors(IEnumerable<string> stationIds)
        {
            var want = new HashSet<string>(stationIds, StringComparer.Ordinal);
            var found = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(ScenesFolder, "*.unity"))
            {
                if (found.Count == want.Count) break;
                string pending = null;
                foreach (string line in File.ReadLines(path))
                {
                    Match id = Regex.Match(line, @"^\s+- Id: (station\.\S+)\s*$");
                    if (id.Success) { pending = want.Contains(id.Groups[1].Value) ? id.Groups[1].Value : null; continue; }
                    if (pending == null) continue;
                    Match interior = Regex.Match(line, @"^\s+Interior: \{fileID: (-?\d+)");
                    if (!interior.Success) continue;
                    found[pending] = interior.Groups[1].Value != "0";
                    pending = null;
                }
            }
            return found;
        }

        // ---- the player's rod presenter, as St Peters holds it ------------------------------------

        private sealed class PresenterValues
        {
            public Dictionary<string, string> Scene;
            public Dictionary<string, List<string>> Lists;
            public SerializedObject Defaults;

            public float Float(string key) => Scene.TryGetValue(key, out string v)
                ? float.Parse(v, CultureInfo.InvariantCulture) : Prop(key).floatValue;

            public int Int(string key) => Scene.TryGetValue(key, out string v)
                ? int.Parse(v, CultureInfo.InvariantCulture) : Prop(key).intValue;

            public Color Colour(string key)
            {
                if (!Scene.TryGetValue(key, out string v)) return Prop(key).colorValue;
                Match m = Regex.Match(v, @"r: ([-\d.eE]+), g: ([-\d.eE]+), b: ([-\d.eE]+), a: ([-\d.eE]+)");
                Assert.IsTrue(m.Success, $"{key}: not a colour: {v}");
                float F(int i) => float.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture);
                return new Color(F(1), F(2), F(3), F(4));
            }

            public List<string> SpriteList(string key)
            {
                Assert.IsTrue(Lists.TryGetValue(key, out List<string> list), $"St Peters' presenter has no {key}");
                return list;
            }

            private SerializedProperty Prop(string key)
            {
                SerializedProperty p = Defaults.FindProperty(key);
                Assert.IsNotNull(p, $"the rod presenter has no field {key}");
                return p;
            }
        }

        private PresenterValues PlayersPresenter()
        {
            var go = new GameObject("fresh presenter");
            _spawned.Add(go);
            var fresh = go.AddComponent<RodFightPresenter>();
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(fresh));
            string guid = AssetDatabase.AssetPathToGUID(scriptPath);
            Assert.IsNotEmpty(guid, "harness: the rod presenter's script has no guid");

            var scene = new Dictionary<string, string>(StringComparer.Ordinal);
            var lists = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            int blocks = 0;
            bool inBlock = false;
            List<string> listing = null;
            foreach (string line in File.ReadLines(StPetersScenePath))
            {
                if (line.StartsWith("--- !u!", StringComparison.Ordinal)) { inBlock = false; listing = null; continue; }
                if (line.StartsWith("  m_Script: ", StringComparison.Ordinal) && line.Contains(guid))
                {
                    inBlock = true;
                    blocks++;
                    continue;
                }
                if (!inBlock) continue;
                Match item = Regex.Match(line, @"^  - \{fileID: (-?\d+), guid: ([0-9a-f]{32})");
                if (listing != null && item.Success) { listing.Add($"{item.Groups[1].Value}:{item.Groups[2].Value}"); continue; }
                listing = null;
                Match field = Regex.Match(line, @"^  (_\w+):\s*(.*)$");
                if (!field.Success) continue;
                if (field.Groups[2].Value.Length == 0) lists[field.Groups[1].Value] = listing = new List<string>();
                else scene[field.Groups[1].Value] = field.Groups[2].Value.Trim();
            }
            Assert.AreEqual(1, blocks, "St Peters should hold exactly one player rod presenter");
            return new PresenterValues { Scene = scene, Lists = lists, Defaults = new SerializedObject(fresh) };
        }

        private static float PlayersFishSpeedSmoothing()
        {
            var field = typeof(RodFightPresenter).GetField("FishSpeedSmoothing01",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(field, "the rod presenter no longer keeps FishSpeedSmoothing01");
            return (float)field.GetRawConstantValue();
        }
    }
}
