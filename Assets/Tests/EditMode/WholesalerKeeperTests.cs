using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Keeper = HiddenHarbours.App.Editor.NmcWholesalerKeepers.Keeper;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE TWO NINE MILE CREEK WHOLESALERS' KEEPERS</b> (owner ruling 11-16; <c>docs/design/economy-item-sellers.md</c>
    /// §5.1): Lionel Cormier at the boat/gear counter and Bernadette Pitre at the fishing gear counter, as content,
    /// and the tables <see cref="NmcWholesalerKeepers"/> places them from.
    ///
    /// <para>Every check holds before and after the editor run that places the counters: a browse row may be
    /// absent, but if it is there it is the step's row and it is first. The scene side (a listed ware has a vendor,
    /// a browse row's seller has a vendor) is <c>StoreWiringTests</c>' and <c>StoreClerkContentTests</c>'.</para>
    /// </summary>
    public class WholesalerKeeperTests
    {
        const string NpcFolder = "Assets/_Project/Data/NPCs";
        static readonly string[] WholesalerIds = { NmcWholesalerKeepers.BoatGearSellerId, NmcWholesalerKeepers.FishingGearSellerId };

        static IEnumerable<Keeper> Keepers => NmcWholesalerKeepers.All;

        static List<T> Load<T>(string folder) where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null).ToList();

        static NpcDef Npc(string id)
        {
            List<NpcDef> hits = Load<NpcDef>(NpcFolder).Where(n => n.Id == id).ToList();
            Assert.AreEqual(1, hits.Count, $"one NpcDef '{id}' under {NpcFolder}");
            return hits[0];
        }

        static RoutineDef Routine(Keeper k)
        {
            List<RoutineDef> hits = RoutineCatalog.LoadRegion(NmcWholesalerKeepers.RegionId).Where(r => r.Id == k.RoutineId).ToList();
            Assert.AreEqual(1, hits.Count, $"one '{k.RoutineId}' in {NmcWholesalerKeepers.RegionId}'s days");
            return hits[0];
        }

        // ---- the people --------------------------------------------------------------------------------------

        [Test]
        public void EachKeeper_IsAWholeTalkingPerson_WithABakedBuild_AndTheirOwnMetFlag()
        {
            List<NpcDef> everyone = Load<NpcDef>(NpcFolder);
            foreach (Keeper k in Keepers)
            {
                NpcDef npc = Npc(k.NpcId);
                Assert.AreEqual(k.AssetName, npc.name, "the step finds the keeper by asset name");
                Assert.IsFalse(string.IsNullOrWhiteSpace(npc.DisplayName), k.NpcId);
                Assert.AreEqual(InteractKind.Talk, npc.Kind, $"{k.NpcId} is talked to");
                Assert.IsTrue(npc.HasDialogue, $"{k.NpcId} has nothing to say");
                Assert.AreEqual(k.DialogueId, npc.Dialogue.Id, $"{k.NpcId}'s dialogue");
                Assert.IsTrue(npc.HasBakedBody, $"{k.NpcId} needs a baked build to stand and walk");
                Assert.IsFalse(string.IsNullOrEmpty(npc.CompletionFlag), $"{k.NpcId} has no met flag");
                Assert.AreEqual(1, everyone.Count(n => n.CompletionFlag == npc.CompletionFlag),
                                $"'{npc.CompletionFlag}' is somebody else's met flag too");
            }
        }

        [Test]
        public void EachWholesaler_NamesItsKeeper()
        {
            Dictionary<string, SellerDef> sellers = Resources.LoadAll<SellerDef>(SellerDirectory.ResourceFolder).ToDictionary(s => s.Id);
            foreach (Keeper k in Keepers)
            {
                Assert.IsTrue(sellers.ContainsKey(k.SellerId), k.SellerId);
                Assert.AreEqual(k.NpcId, sellers[k.SellerId].KeeperNpcId, $"{k.SellerId}'s keeper");
            }
        }

        [Test]
        public void EachKeeper_Greets_AndAnswersAQuestion_AndAnyBrowseRowIsTheStepsRowFirst()
        {
            foreach (Keeper k in Keepers)
            {
                DialogueDef d = Npc(k.NpcId).Dialogue;
                Assert.GreaterOrEqual(d.FirstLines.Length, 2, $"{d.Id}: a first meeting is a beat, not a line");
                Assert.IsNotEmpty(d.RepeatLines, $"{d.Id}: a shorter re-greet once met");
                Assert.IsTrue(d.Options.Any(o => !o.OpensCatalog && !o.SellsAtCounter && o.ReplyLines != null && o.ReplyLines.Length > 0),
                              $"{d.Id}: something to ask that is not a trade");
                Assert.IsFalse(d.Options.Any(o => o.SellsAtCounter), $"{d.Id}: a wholesaler buys nothing from the player");

                List<DialogueOption> books = d.Options.Where(o => o.OpensCatalog).ToList();
                Assert.LessOrEqual(books.Count, 1, $"{d.Id}: one wares book at most");
                if (books.Count == 0) continue;   // before the editor run places the counter
                Assert.AreEqual(NmcWholesalerKeepers.BrowseOptionId, d.Options[0].Id, $"{d.Id}: the browse row comes first");
                Assert.AreEqual(k.SellerId, books[0].CatalogSellerId, $"{d.Id}: the book is this counter's");
                Assert.AreEqual(NmcWholesalerKeepers.BrowseSection, books[0].CatalogSection, d.Id);
                Assert.AreEqual(k.BrowseLabel, books[0].Label, d.Id);
            }
        }

        [Test]
        public void NoTwoShopkeepers_ShareALine()
        {
            var said = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string id in Keepers.Select(k => k.NpcId).Concat(new[] { "npc.claudette_boudreau", "npc.marguerite_leblanc" }))
            {
                DialogueDef d = Npc(id).Dialogue;
                IEnumerable<string> lines = d.FirstLines.Concat(d.RepeatLines ?? Array.Empty<string>())
                    .Concat(d.Options.SelectMany(o => o.ReplyLines ?? Array.Empty<string>()));
                foreach (string line in lines.Distinct())
                {
                    Assert.IsFalse(said.TryGetValue(line, out string other) && other != id, $"{id} and {other} both say: {line}");
                    said[line] = id;
                }
            }
        }

        // ---- their days --------------------------------------------------------------------------------------

        [Test]
        public void EachKeeper_IsAtTheCounterByDay_AndHomeAtNight()
        {
            foreach (Keeper k in Keepers)
            {
                RoutineDef r = Routine(k);
                Assert.AreEqual(Npc(k.NpcId), r.Npc, $"{r.Id} is {k.NpcId}'s day");
                Assert.IsTrue(r.IsWalkable, r.Id);
                float[] hours = r.Entries.Select(e => e.StartHour).ToArray();

                RoutineEntry noon = r.Entries[RoutineSchedule.BlockIndexAt(12f, hours)];
                Assert.AreEqual(k.CounterStationId, noon.StationId, $"{r.Id}: at noon");
                Assert.AreEqual(RoutineActivity.Work, noon.Activity, $"{r.Id}: at noon");

                RoutineEntry night = r.Entries[RoutineSchedule.BlockIndexAt(2f, hours)];
                Assert.AreEqual(k.HomeStationId, night.StationId, $"{r.Id}: at two in the morning");
                Assert.AreEqual(RoutineActivity.Home, night.Activity, $"{r.Id}: at two in the morning");

                CollectionAssert.IsSubsetOf(r.Entries.Select(e => e.StationId).ToList(),
                                            new[] { k.CounterStationId, k.HomeStationId },
                                            $"{r.Id} names a station the step does not place");
            }
        }

        [Test]
        public void EachKeepersDay_ResolvesOverTheStepsTables_AndEveryWalkFitsItsBlock()
        {
            float secondsPerGameHour = GameConfig.DefaultSecondsPerDay / RoutineSchedule.HoursPerDay;
            foreach (Keeper k in Keepers)
            {
                InteriorFootprint room = NmcBuildingShells.FootprintOf(k.Shell);
                var go = new GameObject("WholesalerKeeperTests");
                try
                {
                    RoutineStations stations = go.AddComponent<RoutineStations>();
                    RoutineLanes lanes = go.AddComponent<RoutineLanes>();
                    // No BuildingInterior in an EditMode fixture: the counter station is checked for its ROUTE here
                    // (lane nodes, the tree, the walk's length); its indoor geometry is the next test's.
                    RoutineStationEntry[] table = NmcWholesalerKeepers.Stations(k, room, null);
                    stations.Configure(table);
                    NmcWholesalerKeepers.Lanes(k, room, out Vector2[] nodes, out int[] parents, out string[] names);
                    lanes.Configure(nodes, parents, names, new int[nodes.Length], new int[nodes.Length], Array.Empty<Vector2>());
                    Assert.AreEqual("", lanes.Validate(), $"{k}'s lanes");
                    Assert.AreEqual(2, table.Select(s => s.Id).Distinct().Count(), $"{k}: two stations");
                    Assert.IsTrue(table.All(s => s.Id.StartsWith("station.nine_mile_creek.", StringComparison.Ordinal)), $"{k}: station ids carry the region");
                    Assert.IsTrue(table.Single(s => s.Id == k.HomeStationId).OutOfSight, $"{k}: home is out of sight");
                    Assert.IsFalse(table.Single(s => s.Id == k.CounterStationId).OutOfSight, $"{k}: the counter is in sight");

                    RoutinePlan plan = RoutinePlanner.Build(Routine(k), stations, lanes, 20261117, out _, out string problem);
                    Assert.IsNotNull(plan, $"{k}'s day does not resolve: {problem}");
                    for (int i = 0; i < plan.BlockCount; i++)
                        Assert.LessOrEqual(plan.TravelHours(i, secondsPerGameHour), RoutineSchedule.GapHours(plan.DepartureHours, i),
                                           $"{k}: block {i}'s walk overruns the block");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        [Test]
        public void EachKeeper_StandsBehindTheCounterOnTheShopFloor_AndWalksInFromTheDoorClearOfEverySolidBlock()
        {
            foreach (Keeper k in Keepers)
            {
                InteriorFootprint room = NmcBuildingShells.FootprintOf(k.Shell);
                Vector2 door = room.WorldToModel(room.DoorWorld);
                Vector2 stand = k.StandModel;
                Rect counter = NmcWholesalerKeepers.CounterModel(k);
                NmcBuildingShells.Block[] solid = k.Shell.Blocks.Where(b => b.Solid).ToArray();

                Assert.IsTrue(room.Contains(room.ModelToWorld(stand), 0.5f), $"{k} stands outside their own walls");
                Assert.IsTrue(k.Shell.Zones.Any(z => z.Public && z.Model.Contains(stand)), $"{k} stands off the public floor");
                Assert.Greater(stand.y, counter.yMax, $"{k} stands on the customer's side of the counter, not behind it");
                Assert.LessOrEqual(Mathf.Abs(stand.x - Mathf.Clamp(stand.x, counter.xMin, counter.xMax)), 0.5f,
                                   $"{k} stands off the end of the counter, not behind it");

                const int Samples = 400;
                for (int i = 0; i <= Samples; i++)
                {
                    Vector2 p = Vector2.Lerp(door, stand, i / (float)Samples);
                    foreach (NmcBuildingShells.Block b in solid)
                        Assert.IsFalse(b.Model.Contains(p), $"{k}'s walk in from the door crosses {b.Name} at {p}");
                }
            }
        }

        [Test]
        public void EachKeepersWalkHome_StartsAtTheirOwnDoor_AndRunsOutsideTheWalls()
        {
            foreach (Keeper k in Keepers)
            {
                InteriorFootprint room = NmcBuildingShells.FootprintOf(k.Shell);
                Vector2 door = room.WorldToModel(room.DoorWorld);
                Assert.Less(Vector2.Distance(door, k.DoorstepModel), 1.5f, $"{k}'s doorstep is not at their door");
                foreach (Vector2 m in new[] { k.DoorstepModel, k.HomeWayModel, k.HomeModel })
                    Assert.IsFalse(room.Contains(room.ModelToWorld(m), 0f), $"{k}'s walk home runs inside the building at {m}");
            }
        }

        // ---- what the counters stock --------------------------------------------------------------------------

        static readonly Dictionary<Type, Type> OfferOf = new Dictionary<Type, Type>
        {
            [typeof(InstrumentShop)] = typeof(InstrumentOffer),
            [typeof(GearShop)] = typeof(GearOffer),
            [typeof(BaitShop)] = typeof(BaitDef),
            [typeof(PotShop)] = typeof(PotOffer),
        };

        /// <summary>Every offer that names a wholesaler: its id, sellers and listing state.</summary>
        static List<(string Id, string[] Sellers, bool Listed)> WholesalerOffers()
        {
            var all = new List<(string, string[], bool)>();
            void Add<T>(IEnumerable<T> items, Func<T, string> id, Func<T, CatalogListing> cat) where T : Object
            {
                foreach (T o in items)
                {
                    string[] sellers = cat(o).Sellers ?? Array.Empty<string>();
                    if (sellers.Any(s => WholesalerIds.Contains(s))) all.Add((id(o), sellers, cat(o).Listed));
                }
            }
            Add(CatalogSource.All<InstrumentOffer>(), o => o.Id, o => o.Catalog);
            Add(CatalogSource.All<GearOffer>(), o => o.Id, o => o.Catalog);
            Add(CatalogSource.All<BaitDef>(), o => o.Id, o => o.Catalog);
            Add(CatalogSource.All<PotOffer>(), o => o.Id, o => o.Catalog);
            Add(CatalogSource.All<SupplyDef>(), o => o.Id, o => o.Catalog);
            Assert.That(all, Is.Not.Empty, "no offer names a wholesaler: every check here would pass on nothing");
            return all;
        }

        [Test]
        public void TheCounterRows_AreEveryOfferOnlyAWholesalerSells_AndEveryOtherIsAccountedForAsWaiting()
        {
            List<(string Id, string[] Sellers, bool Listed)> offers = WholesalerOffers();
            IReadOnlyList<StoreWiringPatch.Seller> rows = StoreWiringPatch.NineMileCreekWholesalers;
            var waiting = NmcWholesalerKeepers.Waiting.Select(w => w.Wares).ToList();

            CollectionAssert.AllItemsAreUnique(rows.Select(r => r.Wares).ToList(), "a ware on two counter rows");
            CollectionAssert.AllItemsAreUnique(waiting, "a ware waiting twice");
            CollectionAssert.IsEmpty(rows.Select(r => r.Wares).Intersect(waiting).ToList(), "a ware both on a counter row and waiting");

            foreach ((string id, string[] sellers, bool listed) in offers)
            {
                bool onlyWholesalers = sellers.All(s => WholesalerIds.Contains(s));
                string[] wholesalers = sellers.Where(s => WholesalerIds.Contains(s)).ToArray();
                StoreWiringPatch.Seller[] row = rows.Where(r => r.Wares == id).ToArray();
                if (onlyWholesalers)
                    Assert.AreEqual(1, sellers.Length, $"{id} names both wholesalers; one counter row cannot sell as two");
                // A ware the wholesaler shares with another counter (the St Peters store's baits, 2026-11-25) is
                // wired like one only the wholesaler sells, once its other counter stands too (guard 4 counts both).
                if ((onlyWholesalers || listed) && !waiting.Contains(id))
                {
                    Assert.AreEqual(1, wholesalers.Length, $"{id} names both wholesalers; one counter row cannot sell as two");
                    Assert.AreEqual(1, row.Length, $"{id} is sold by a wholesaler and has no counter row");
                    Assert.AreEqual(wholesalers[0], row[0].SellerId, $"{id}'s row sells as the wrong wholesaler");
                }
                else
                {
                    Assert.IsEmpty(row, $"{id} awaits counter wiring and must have no counter row");
                    CollectionAssert.Contains(waiting, id, $"{id} names a wholesaler and is neither on a counter row nor waiting");
                    Assert.IsFalse(listed, $"{id} is waiting for counter wiring and must remain unlisted");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(NmcWholesalerKeepers.Waiting.Single(w => w.Wares == id).Why),
                                   $"{id} needs a reason for waiting");
                }
            }
            CollectionAssert.IsSubsetOf(waiting, offers.Select(o => o.Id).ToList(), "a waiting ware no wholesaler offer names");
        }

        [Test]
        public void EachCounterRow_StandsAtItsKeepersCounter_AndResolvesToExactlyOneOffer()
        {
            var assets = new StoreWiringPatch.EditorStoreWiringAssets();
            foreach (StoreWiringPatch.Seller row in StoreWiringPatch.NineMileCreekWholesalers)
            {
                Keeper k = Keepers.SingleOrDefault(x => x.CounterRoot == row.Root);
                Assert.IsNotNull(k, $"{row}: no keeper's counter is called {row.Root}");
                Assert.AreEqual(k.SellerId, row.SellerId, $"{row} sells as somebody else at {k}'s counter");
                Assert.IsTrue(OfferOf.ContainsKey(row.Kind), $"{row}: {row.Kind.Name} is not a counter vendor the step places");
                Assert.DoesNotThrow(() => assets.WaresGuid(OfferOf[row.Kind], row.Wares), $"{row} does not resolve to one {OfferOf[row.Kind].Name}");
            }
            foreach (Keeper k in Keepers)
                Assert.IsTrue(StoreWiringPatch.NineMileCreekWholesalers.Any(r => r.Root == k.CounterRoot), $"{k}'s counter has nothing on it");
        }
    }
}
