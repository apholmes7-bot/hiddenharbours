#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HiddenHarbours.Art;                 // YSortSprite
using HiddenHarbours.Economy;             // the vendors and their offers
using HiddenHarbours.World;               // NpcDef, DialogueDef, RoutineDef, the routine tables, BuildingInterior
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE TWO NINE MILE CREEK WHOLESALERS GET THEIR KEEPERS AND THEIR COUNTERS</b> (owner ruling 11-16:
    /// the Loran and every boat electronic at a boat/gear wholesaler, fishing stock at a fishing gear
    /// wholesaler; <c>docs/design/economy-item-sellers.md</c> §5.1).
    ///
    /// <para><b>The keepers are data</b> (<c>Data/NPCs</c>: Lionel Cormier at the boat/gear counter, Bernadette
    /// Pitre at the fishing gear counter; their days are <c>Data/Routines</c>). This step puts them in the
    /// committed <c>NineMileCreek.unity</c>, inside the greybox shells <see cref="NmcBuildingShells"/> placed
    /// (steps 22 and 23), and does everything else a counter needs in the same run:</para>
    /// <list type="number">
    /// <item>one ROOT GameObject per counter (<see cref="BoatGearCounterRoot"/>, <see cref="FishingGearCounterRoot"/>)
    /// at the shell's <c>TradeCounter</c> block, holding one vendor per row of
    /// <see cref="StoreWiringPatch.NineMileCreekWholesalers"/>, each selling as its seller id with the
    /// chandlery's wallet. A root, because <see cref="StoreWiringPatch"/> finds a vendor by root name;</item>
    /// <item>one root per keeper holding that keeper's station table and lane tree, and the keeper under it:
    /// dressed, talkable, Y-sorted as a walker, and walking their day (<see cref="VillagerRoutine"/>). The
    /// counter station is INDOORS (the shell's own <see cref="BuildingInterior"/>, so the keeper is drawn only
    /// while the player is inside with them); home is out of sight, a short walk off from the door;</item>
    /// <item>each keeper joins the region's <see cref="WorldInteractor"/>, whose list is explicit;</item>
    /// <item>each row's offer has <c>Listed</c> ticked, and each keeper's dialogue gains its "show me your
    /// wares" row (<see cref="Keeper.BrowseLabel"/>), first, as Claudette's is.</item>
    /// </list>
    ///
    /// <para><b>Why this is not committed already run.</b> No editor was granted when the keepers were authored.
    /// The listed ticks, the browse rows and the wiring rows cannot land before the vendors stand in the scene:
    /// <c>StoreWiringTests</c> guards 1, 4 and 5 and <c>StoreClerkContentTests</c> hold a listed ware, a browse
    /// row and a wiring row to a committed vendor. So the run lands all of it together, and folds
    /// <see cref="StoreWiringPatch.NineMileCreekWholesalers"/> into <see cref="StoreWiringPatch.NineMileCreek"/>
    /// in the same commit.</para>
    ///
    /// <para>Every position is in the shell's MODEL frame (metres; +y north at facing 0), turned into the world
    /// by the shell's own <see cref="InteriorFootprint"/>, so moving a shell moves its keeper with it.
    /// Running the step twice changes nothing: it finds what it made by name and re-configures it.</para>
    /// </summary>
    public static class NmcWholesalerKeepers
    {
        public const string RegionId = "region.nine_mile_creek";

        public const string BoatGearSellerId = "seller.nmc_boat_gear_wholesaler";
        public const string FishingGearSellerId = "seller.nmc_fishing_gear_wholesaler";

        public const string BoatGearCounterRoot = "NmcBoatGearWholesalerCounter";
        public const string FishingGearCounterRoot = "NmcFishingGearWholesalerCounter";

        /// <summary>The section a browse row opens on (the books' everyday shelf, as Claudette's).</summary>
        public const string BrowseSection = "gear";
        public const string BrowseOptionId = "option.browse_wares";

        /// <summary>Which way a keeper behind a counter faces: south, at the customer and the door.</summary>
        public const float FacingTheCustomer = 180f;

        const string NpcFolder = "Assets/_Project/Data/NPCs";
        const string RoutineFolder = RoutineCatalog.Folder;
        const string ChandleryRoot = "GeneralStore";
        const string CounterBlock = "TradeCounter";

        /// <summary>One wholesaler's keeper, counter and walk home.</summary>
        public sealed class Keeper
        {
            public string AssetName, NpcId, RoutineId, DialogueId, SellerId;
            public NmcBuildingShells.Shell Shell;
            public string CounterRoot, KeeperRoot;

            /// <summary>Where the keeper stands at work: behind the counter's door end, so the straight walk in
            /// from the door clears the counter block (indoors a walk is door to station, nothing between).</summary>
            public Vector2 StandModel;

            public string CounterStationId, HomeStationId;
            public string DoorstepNode, HomeWayNode;

            /// <summary>Just outside the door; the lane tree's root.</summary>
            public Vector2 DoorstepModel;

            /// <summary>Where the walk home leaves sight (an out-of-sight station's occupant is drawn up to its
            /// lane node, then hidden for the last leg).</summary>
            public Vector2 HomeWayModel;

            /// <summary>The home station itself, past the lane node, out of sight.</summary>
            public Vector2 HomeModel;

            public string BrowseLabel;

            public override string ToString() => AssetName;
        }

        /// <summary>Lionel Cormier, Cormier Marine on Water Street. The door opens onto the frontage walk; home
        /// is east along it.</summary>
        public static readonly Keeper BoatGear = new Keeper
        {
            AssetName = "LionelCormier", NpcId = "npc.lionel_cormier", RoutineId = "routine.lionel_cormier",
            DialogueId = "dialogue.lionel_first", SellerId = BoatGearSellerId,
            Shell = NmcBuildingShells.BoatGearWholesaler,
            CounterRoot = BoatGearCounterRoot, KeeperRoot = "NmcBoatGearWholesalerKeeper",
            StandModel = new Vector2(1.9f, 0.3f),
            CounterStationId = "station.nine_mile_creek.boat_gear_counter",
            HomeStationId = "station.nine_mile_creek.boat_gear_keeper_home",
            DoorstepNode = "boat_gear_doorstep", HomeWayNode = "boat_gear_home_way",
            DoorstepModel = new Vector2(0f, -5.5f),
            HomeWayModel = new Vector2(10f, -5.5f),
            HomeModel = new Vector2(12f, -5.5f),
            BrowseLabel = "What have you got for the boat?",
        };

        /// <summary>Bernadette Pitre, the warehouse west of the lumber yard. The door is in the south wall; home
        /// is round the east corner and up the strip between the wall and the yard fence.</summary>
        public static readonly Keeper FishingGear = new Keeper
        {
            AssetName = "BernadettePitre", NpcId = "npc.bernadette_pitre", RoutineId = "routine.bernadette_pitre",
            DialogueId = "dialogue.bernadette_first", SellerId = FishingGearSellerId,
            Shell = NmcBuildingShells.FishingGearWholesaler,
            CounterRoot = FishingGearCounterRoot, KeeperRoot = "NmcFishingGearWholesalerKeeper",
            StandModel = new Vector2(2.9f, -1.7f),
            CounterStationId = "station.nine_mile_creek.fishing_gear_counter",
            HomeStationId = "station.nine_mile_creek.fishing_gear_keeper_home",
            DoorstepNode = "fishing_gear_doorstep", HomeWayNode = "fishing_gear_home_way",
            DoorstepModel = new Vector2(2f, -8f),
            HomeWayModel = new Vector2(11f, -8f),
            HomeModel = new Vector2(11f, -3f),
            BrowseLabel = "What have you got in?",
        };

        public static readonly Keeper[] All = { BoatGear, FishingGear };

        /// <summary>
        /// Wholesaler offers that do NOT get a vendor in this run, and why. Each also names
        /// <c>seller.leblancs</c>, and <c>Listed</c> is one flag per offer: ticking it needs a St Peters vendor
        /// too (<c>StoreWiringTests</c> guard 4), and the St Peters counter is a separate scene with its own
        /// open work. The shellfish tools are also held back until hand-gathering exists. The seat or the owner
        /// decides; adding a row to <see cref="StoreWiringPatch.NineMileCreekWholesalers"/> is all the step needs.
        /// </summary>
        public static readonly (string Wares, string Why)[] Waiting =
        {
            ("bait.fish_scrap", "also names seller.leblancs"),
            ("bait.herring", "also names seller.leblancs"),
            ("bait.mackerel", "also names seller.leblancs"),
            ("bait.sea_worm", "also names seller.leblancs"),
            ("bait.shucked_clam", "also names seller.leblancs"),
            ("bait.squid_strip", "also names seller.leblancs"),
            ("gear.bucket", "also names seller.leblancs"),
            ("gear.shovel", "also names seller.leblancs"),
            ("gear.mussel_sack", "also names seller.leblancs; held for hand-gathering"),
            ("gear.oyster_tongs", "also names seller.leblancs; held for hand-gathering"),
            ("gear.shellfish_basket", "also names seller.leblancs; held for hand-gathering"),
            ("gear.shellfish_rake", "also names seller.leblancs; held for hand-gathering"),
        };

        // =====================================================================================
        //  the tables: pure functions of a keeper and its shell's footprint
        // =====================================================================================

        /// <summary>The shell's counter block, in model metres.</summary>
        public static Rect CounterModel(Keeper k) => k.Shell.Blocks.Single(b => b.Name == CounterBlock).Model;

        /// <summary>The keeper's two stations: the counter (indoors, in <paramref name="interior"/>) and home
        /// (out of sight).</summary>
        public static RoutineStationEntry[] Stations(Keeper k, InteriorFootprint room, BuildingInterior interior) => new[]
        {
            new RoutineStationEntry
            {
                Id = k.CounterStationId, Position = room.ModelToWorld(k.StandModel),
                StandHeadingDegrees = FacingTheCustomer, Interior = interior, OutOfSight = false,
                LaneNodeName = k.DoorstepNode, Why = $"behind the {k.Shell.Name} counter, facing the door",
            },
            new RoutineStationEntry
            {
                Id = k.HomeStationId, Position = room.ModelToWorld(k.HomeModel),
                StandHeadingDegrees = FacingTheCustomer, Interior = null, OutOfSight = true,
                LaneNodeName = k.HomeWayNode, Why = "home, off out of sight past the end of the walk",
            },
        };

        /// <summary>The keeper's lane tree: the doorstep (the root) and the way home, one edge.</summary>
        public static void Lanes(Keeper k, InteriorFootprint room, out Vector2[] nodes, out int[] parents, out string[] names)
        {
            nodes = new[] { room.ModelToWorld(k.DoorstepModel), room.ModelToWorld(k.HomeWayModel) };
            parents = new[] { RoutineLaneTree.NoParent, 0 };
            names = new[] { k.DoorstepNode, k.HomeWayNode };
        }

        /// <summary>The counter root's world position: the centre of the shell's counter block.</summary>
        public static Vector2 CounterWorld(Keeper k, InteriorFootprint room) => room.ModelToWorld(CounterModel(k).center);

        // =====================================================================================
        //  the step
        // =====================================================================================

        [MenuItem("Hidden Harbours/World/Nine Mile Creek Buildings/Place the Wholesaler Counters and Keepers")]
        public static void Place()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != StoreWiringPatch.NineMileCreekScenePath)
                throw new InvalidOperationException($"Open {StoreWiringPatch.NineMileCreekScenePath} first.");

            GameObject wallet = ChandleryWallet(scene);
            WorldInteractor interactor = TheInteractor();
            var assets = new StoreWiringPatch.EditorStoreWiringAssets();
            var report = new List<string>();

            foreach (Keeper k in All)
            {
                BuildingInterior interior = ShellInterior(k);
                InteriorFootprint room = interior.Footprint;
                Vector2 expected = NmcBuildingShells.FootprintOf(k.Shell).Centre;
                if (Vector2.Distance(room.Centre, expected) > 0.05f)
                    throw new InvalidOperationException(
                        $"{k.Shell.Name}'s interior stands at {room.Centre}, and NmcBuildingShells says {expected}: " +
                        "re-run the shell step before placing its keeper.");

                int vendors = PlaceCounter(scene, k, room, wallet, assets);

                NpcDef npc = Load<NpcDef>(NpcFolder, k.NpcId, n => n.Id);
                RoutineDef routine = Load<RoutineDef>(RoutineFolder, k.RoutineId, r => r.Id);
                if (routine.Npc != npc) throw new InvalidOperationException($"{k.RoutineId} is not {k.NpcId}'s day.");

                GameObject root = Root(scene, k.KeeperRoot, Vector2.zero);
                RoutineStations stations = Need<RoutineStations>(root);
                RoutineLanes lanes = Need<RoutineLanes>(root);
                stations.Configure(Stations(k, room, interior));
                Lanes(k, room, out Vector2[] nodes, out int[] parents, out string[] names);
                lanes.Configure(nodes, parents, names, new int[nodes.Length], new int[nodes.Length], Array.Empty<Vector2>());
                string problem = lanes.Validate();
                if (!string.IsNullOrEmpty(problem)) throw new InvalidOperationException($"{k.KeeperRoot}'s lanes: {problem}");

                GameObject go = root.transform.Cast<Transform>().Select(t => t.gameObject).SingleOrDefault(g => g.name == k.AssetName);
                if (go == null) { go = new GameObject(k.AssetName); go.transform.SetParent(root.transform, false); }
                go.transform.position = room.ModelToWorld(k.StandModel);
                SpriteRenderer sprite = Need<SpriteRenderer>(go);
                NpcBodyDresser.Dress(go, sprite, npc, "", null, Color.white, FacingTheCustomer);
                Need<Interactable>(go).Configure(npc);
                Need<YSortSprite>(go).Dynamic = true;   // a walker re-sorts as they go
                Need<VillagerRoutine>(go).Configure(routine, stations, lanes, sprite);

                RoutinePlan plan = RoutinePlanner.Build(routine, stations, lanes, 0, out _, out string dayProblem);
                if (plan == null) throw new InvalidOperationException($"{k.AssetName}'s day does not resolve: {dayProblem}");

                Join(interactor, go.GetComponent<Interactable>());
                ListTheRows(k, assets);
                AddBrowseRow(k);

                EditorUtility.SetDirty(stations); EditorUtility.SetDirty(lanes); EditorUtility.SetDirty(go);
                report.Add($"{npc.DisplayName} at {k.Shell.Name} ({vendors} vendors)");
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[NmcWholesalerKeepers] placed " + string.Join("; ", report));
        }

        /// <summary>The <c>-executeMethod</c> entry: open the creek, place, save. Exits 1 on any refusal.</summary>
        public static void PlaceAndSaveBatch() => HeronViewRoutinesBuilder.RunCli(() =>
        {
            Scene scene = EditorSceneManager.OpenScene(StoreWiringPatch.NineMileCreekScenePath, OpenSceneMode.Single);
            Place();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("NineMileCreek did not save.");
            Debug.Log("[NmcWholesalerKeepers] PLACED OK");
        });

        // ---- the pieces ----------------------------------------------------------------------------

        /// <summary>The counter root and its vendors, one per wiring row naming it. Returns the vendor count.</summary>
        static int PlaceCounter(Scene scene, Keeper k, InteriorFootprint room, GameObject wallet,
                                StoreWiringPatch.IStoreWiringAssets assets)
        {
            GameObject root = Root(scene, k.CounterRoot, CounterWorld(k, room));
            int count = 0;
            foreach (StoreWiringPatch.Seller row in StoreWiringPatch.NineMileCreekWholesalers.Where(r => r.Root == k.CounterRoot))
            {
                if (row.SellerId != k.SellerId)
                    throw new InvalidOperationException($"{row} sells as {row.SellerId}, at {k.SellerId}'s counter.");
                FieldInfo waresField = StoreWiringPatch.WaresField(row.Kind);
                Object wares = AssetDatabase.LoadAssetAtPath(
                    AssetDatabase.GUIDToAssetPath(assets.WaresGuid(waresField.FieldType, row.Wares)), waresField.FieldType);
                if (wares == null) throw new InvalidOperationException($"{row}: no {waresField.FieldType.Name} '{row.Wares}'.");

                Component vendor = root.GetComponents(row.Kind)
                    .FirstOrDefault(c => new SerializedObject(c).FindProperty(waresField.Name).objectReferenceValue == wares);
                if (vendor == null) vendor = root.AddComponent(row.Kind);

                var so = new SerializedObject(vendor);
                so.FindProperty(waresField.Name).objectReferenceValue = wares;
                so.FindProperty("_sellerId").stringValue = row.SellerId;
                so.FindProperty("_walletProvider").objectReferenceValue = wallet;
                so.ApplyModifiedPropertiesWithoutUndo();
                count++;
            }
            if (count == 0) throw new InvalidOperationException($"no wiring row names {k.CounterRoot}.");
            EditorUtility.SetDirty(root);
            return count;
        }

        /// <summary>Tick <c>Listed</c> on every offer this keeper's counter stocks.</summary>
        static void ListTheRows(Keeper k, StoreWiringPatch.IStoreWiringAssets assets)
        {
            foreach (StoreWiringPatch.Seller row in StoreWiringPatch.NineMileCreekWholesalers.Where(r => r.Root == k.CounterRoot))
            {
                Type listing = StoreWiringPatch.WaresField(row.Kind).FieldType;
                Object offer = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(assets.WaresGuid(listing, row.Wares)), listing);
                var so = new SerializedObject(offer);
                SerializedProperty listed = so.FindProperty("Catalog.Listed");
                if (listed == null) throw new InvalidOperationException($"{row.Wares} has no Catalog.Listed.");
                if (listed.boolValue) continue;
                listed.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(offer);
            }
        }

        /// <summary>The keeper's "show me your wares" row, first among the options, unless one opens this book.</summary>
        static void AddBrowseRow(Keeper k)
        {
            DialogueDef dialogue = Load<DialogueDef>(NpcFolder, k.DialogueId, d => d.Id);
            var options = new List<DialogueOption>(dialogue.Options ?? Array.Empty<DialogueOption>());
            if (options.Any(o => o.CatalogSellerId == k.SellerId)) return;
            options.Insert(0, new DialogueOption
            {
                Id = BrowseOptionId, Label = k.BrowseLabel, ReplyLines = Array.Empty<string>(),
                CatalogSellerId = k.SellerId, CatalogSection = BrowseSection, SellAtSellerId = "",
                NothingToSellLines = Array.Empty<string>(),
            });
            dialogue.Options = options.ToArray();
            EditorUtility.SetDirty(dialogue);
        }

        /// <summary>The keeper goes at the HEAD of the interactor's list: the vendor-anchor step appends its
        /// anchors at the tail and <c>NmcVendorAnchorTests</c> rebuilds them there to the byte, so nothing may
        /// follow them.</summary>
        static void Join(WorldInteractor interactor, Interactable it)
        {
            var so = new SerializedObject(interactor);
            SerializedProperty list = so.FindProperty("_interactables");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == it) return;
            list.InsertArrayElementAtIndex(0);
            list.GetArrayElementAtIndex(0).objectReferenceValue = it;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject ChandleryWallet(Scene scene)
        {
            GameObject store = scene.GetRootGameObjects().SingleOrDefault(g => g.name == ChandleryRoot);
            GearShop shop = store != null ? store.GetComponent<GearShop>() : null;
            var wallet = shop != null
                ? new SerializedObject(shop).FindProperty("_walletProvider").objectReferenceValue as GameObject
                : null;
            if (wallet == null) throw new InvalidOperationException($"{ChandleryRoot}'s GearShop has no wallet to share.");
            return wallet;
        }

        static WorldInteractor TheInteractor()
        {
            WorldInteractor[] all = Object.FindObjectsByType<WorldInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all.Length != 1) throw new InvalidOperationException($"the creek holds {all.Length} WorldInteractors; the step needs one.");
            return all[0];
        }

        static BuildingInterior ShellInterior(Keeper k)
        {
            BuildingInterior[] hits = Object.FindObjectsByType<BuildingInterior>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(b => b.gameObject.name == k.Shell.Name).ToArray();
            if (hits.Length != 1)
                throw new InvalidOperationException($"the creek holds {hits.Length} '{k.Shell.Name}' interiors; place the shell (step {k.Shell.Step}) first.");
            return hits[0];
        }

        static GameObject Root(Scene scene, string name, Vector2 at)
        {
            GameObject go = scene.GetRootGameObjects().SingleOrDefault(g => g.name == name);
            if (go == null) { go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); }
            go.transform.position = new Vector3(at.x, at.y, 0f);
            return go;
        }

        static T Need<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static T Load<T>(string folder, string id, Func<T, string> idOf) where T : Object
        {
            T[] hits = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null && idOf(a) == id).ToArray();
            if (hits.Length != 1) throw new InvalidOperationException($"{folder} holds {hits.Length} {typeof(T).Name}s with the id '{id}'.");
            return hits[0];
        }
    }
}
#endif
