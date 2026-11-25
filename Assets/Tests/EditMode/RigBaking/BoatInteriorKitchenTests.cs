using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The galley's food stores, seats and tables aboard (kitchens slice 1, owner ask 2026-11-15: "boats
    /// will need food storage, a fridge, cooler which needs ice, and pantry ... players can also seat and
    /// dine"). The reader is checked on one small synthetic sidecar; then every committed kit def is held
    /// to the placement rules: every hull that cooks has a cold store, a dry store and a seat; every store
    /// names a shipped def; every new reach stands clear on its floor and apart from its neighbours; every
    /// sit point is on its seat and every table top on its table; the baked anchors match the sidecars;
    /// and which hulls DINE is pinned, so a moved bench is a visible change.
    /// </summary>
    public class BoatInteriorKitchenTests
    {
        const string InteriorFolder = "Assets/_Project/Data/Boats/Interiors";
        const string StorageFolder = "Assets/_Project/Data/Resources/Kitchen/Storage";

        /// <summary>The rig-walk hulls: their kitchens are owed by their rigs, as their prep stations are
        /// (see <c>BoatInteriorPrepAndBunkTests.OwedByTheRigWalk</c>; the same list).</summary>
        static readonly HashSet<string> OwedByTheRigWalk = new HashSet<string>(StringComparer.Ordinal)
        {
            "docs/art/rigs/gameplay/expeditionYacht120IsoRig.expedition_yacht_120.walk.json",
            "docs/art/rigs/gameplay/cruiserIsoRig.express_cruiser.walk.json",
            "docs/art/rigs/gameplay/goFastIsoRig.go_fast.walk.json",
            "docs/art/rigs/gameplay/motorYacht84IsoRig.motor_yacht_84.walk.json",
            "docs/art/rigs/gameplay/sideDraggerIsoRig.side_dragger.walk.json",
            "docs/art/rigs/gameplay/sportFisherIsoRig2.convertible.walk.json",
            "docs/art/rigs/gameplay/sportFisherIsoRig2.skybridge.walk.json",
            "docs/art/rigs/gameplay/sternTrawlerIsoRig.stern_trawler.walk.json",
            "docs/art/rigs/gameplay/sternTrawlerMk2IsoRig.stern_trawler_mk2.walk.json",
        };

        /// <summary>
        /// The kit hulls whose seat is a DINING seat at the shipped reach (1.25 m to a table top's centre or
        /// a prep station). The rest have a seat to rest on but nothing to eat at within reach: their bench
        /// is honestly too far from the counter. A hull joins or leaves this list in the PR that moves it.
        /// </summary>
        static readonly HashSet<string> Dines = new HashSet<string>(StringComparer.Ordinal)
        {
            "interior.cape_islander_iso",
            "interior.coastal_packet_iso",
            "interior.lobster_boat_iso",
            "interior.tanker_iso",
            "interior.lobster_inshore_hardtop_fundy_iso",
            "interior.lobster_inshore_open_fundy_iso",
            "interior.lobster_inshore_hardtop_newfoundland_iso",
            "interior.lobster_inshore_open_newfoundland_iso",
            "interior.lobster_inshore_hardtop_northumberland_iso",
            "interior.lobster_inshore_open_northumberland_iso",
            "interior.lobster_standard_hardtop_fundy_iso",
            "interior.lobster_standard_open_fundy_iso",
        };

        static readonly string[] KitchenActions =
        {
            BoatInteriorAnchor.ActionFoodStorage, BoatInteriorAnchor.ActionSit, BoatInteriorAnchor.ActionTable,
        };

        // ---- the reader, on one small sidecar -------------------------------------------------------

        const string InteriorRig = "// renderer\n";
        const string HullRig = "// loft\n";
        static byte[] Lf(string s) => Encoding.UTF8.GetBytes(s);

        static string Sidecar(string interact)
        {
            return "{\n" +
                "  \"schema\": \"hidden-harbours/boat-interior@1\",\n" +
                "  \"hull_stem\": \"testBoatIsoRig\",\n" +
                "  \"fits_hulls\": [\"testBoatIsoRig\"],\n" +
                "  \"interior_rig\": \"boatInteriorRig.js\",\n" +
                $"  \"derivedFromRigSha256\": \"{DeckSidecarReader.Sha256Hex(Lf(InteriorRig))}\",\n" +
                $"  \"hullRigSha256\": {{ \"testBoatIsoRig\": \"{DeckSidecarReader.Sha256Hex(Lf(HullRig))}\" }},\n" +
                "  \"frame\": { \"scale_px_per_m\": 32 },\n" +
                "  \"cell\": { \"w\": 480, \"h\": 420, \"pivot\": { \"x\": 240, \"y\": 232 } },\n" +
                "  \"motion\": { \"rides_hull_rock\": true },\n" +
                "  \"FOOTPRINT\": { \"house\": [[-1.3, 0.5], [1.3, 0.5], [1.3, 5.5], [-1.3, 5.5]] },\n" +
                "  \"WALKABLE\": {\n" +
                "    \"house_sole\": { \"z\": 0.7, \"polygon\": [[-1.2, 0.6], [1.2, 0.6], [1.2, 2.5], [-1.2, 2.5]],\n" +
                "      \"obstructions\": [ { \"id\": \"stove_counter\", \"footprint\": [[-1.2, 0.7], [-0.8, 0.7], [-0.8, 1.6], [-1.2, 1.6]],\n" +
                "        \"height_above_sole_m\": 0.92, \"treatment\": \"waist_block\" },\n" +
                "        { \"id\": \"bench\", \"footprint\": [[0.6, 0.7], [1.2, 0.7], [1.2, 1.6], [0.6, 1.6]],\n" +
                "        \"height_above_sole_m\": 0.45, \"treatment\": \"waist_block\" } ] }\n" +
                "  },\n" +
                "  \"THRESHOLD\": { \"id\": \"entry\", \"threshold_point\": [0, 0.5, 0.7], \"mechanism\": \"sliding\",\n" +
                "    \"slide\": { \"keep_clear\": [[-0.4, 0.5], [1.2, 0.5], [1.2, 0.3], [-0.4, 0.3]] } },\n" +
                "  \"INTERACT\": [" + interact + "]\n}";
        }

        const string Cooler =
            "{ \"id\": \"cooler\", \"action\": \"food_storage\", \"reach_point\": [0, 1.0, 0.7], " +
            "\"kitchen_storage\": \"kitchen_storage.cooler\" }";
        const string Seat =
            "{ \"id\": \"seat\", \"action\": \"sit\", \"reach_point\": [0.4, 1.2, 0.7],\n" +
            "  \"seat\": { \"level\": \"house_sole\", \"obstruction\": \"bench\", \"sit_point\": [0.9, 1.2, 1.15] } }";
        const string Table =
            "{ \"id\": \"table\", \"action\": \"table\", \"reach_point\": [0.2, 2.0, 0.7],\n" +
            "  \"table\": { \"level\": \"house_sole\", \"obstruction\": \"bench\", \"top_centre\": [0.9, 1.0, 1.15] } }";

        static BoatInteriorRead Read(string interact) =>
            BoatInteriorSidecarReader.Read(Sidecar(interact), "test.interior.json", Lf(InteriorRig), Lf(HullRig));

        [Test]
        public void TheReaderCarriesTheStoreTheSeatAndTheTable()
        {
            BoatInteriorRead read = Read(Cooler + ",\n" + Seat + ",\n" + Table);
            Assert.IsTrue(read.Ok, string.Join(" | ", read.Errors));

            BoatInteriorAnchor cooler = read.Anchors.Single(a => a.Id == "cooler");
            Assert.AreEqual(BoatInteriorAnchor.ActionFoodStorage, cooler.Action);
            Assert.AreEqual("kitchen_storage.cooler", cooler.KitchenStorageId);
            Assert.IsFalse(cooler.HasFurnitureGeometry);

            BoatInteriorAnchor seat = read.Anchors.Single(a => a.Id == "seat");
            Assert.AreEqual(BoatInteriorAnchor.ActionSit, seat.Action);
            Assert.IsTrue(seat.HasFurnitureGeometry);
            Assert.AreEqual("house_sole", seat.FurnitureLevel);
            Assert.AreEqual("bench", seat.FurnitureObstruction);
            Assert.AreEqual(new Vector3(0.9f, 1.2f, 1.15f), seat.FurniturePoint);

            BoatInteriorAnchor table = read.Anchors.Single(a => a.Id == "table");
            Assert.AreEqual(BoatInteriorAnchor.ActionTable, table.Action);
            Assert.AreEqual(new Vector3(0.9f, 1.0f, 1.15f), table.FurniturePoint);
        }

        [Test]
        public void AFoodStoreThatNamesNoDefIsRefused()
        {
            BoatInteriorRead read = Read("{ \"id\": \"cooler\", \"action\": \"food_storage\", \"reach_point\": [0, 1.0, 0.7] }");
            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("kitchen_storage")), string.Join(" | ", read.Errors));
        }

        [Test]
        public void ASeatOnALevelTheInteriorDoesNotHaveIsRefused()
        {
            BoatInteriorRead read = Read(Seat.Replace("\"level\": \"house_sole\"", "\"level\": \"flybridge\""));
            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("flybridge")), string.Join(" | ", read.Errors));
        }

        [Test]
        public void AHalfStatedSeatIsRefused()
        {
            BoatInteriorRead read = Read("{ \"id\": \"seat\", \"action\": \"sit\", \"reach_point\": [0.4, 1.2, 0.7],\n" +
                                         "  \"seat\": { \"level\": \"house_sole\" } }");
            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("sit_point")), string.Join(" | ", read.Errors));
        }

        // ---- every committed def ------------------------------------------------------------------

        static List<BoatInteriorDef> KitDefs()
        {
            var defs = new List<BoatInteriorDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:BoatInteriorDef", new[] { InteriorFolder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null && !OwedByTheRigWalk.Contains(def.SourceSidecar ?? "")) defs.Add(def);
            }
            defs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return defs;
        }

        static Dictionary<string, KitchenStorageDef> ShippedStores()
        {
            var byId = new Dictionary<string, KitchenStorageDef>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:KitchenStorageDef", new[] { StorageFolder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<KitchenStorageDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) byId[def.Id] = def;
            }
            return byId;
        }

        static IEnumerable<BoatInteriorAnchor> Anchors(BoatInteriorDef def, string action) =>
            (def.Anchors ?? Array.Empty<BoatInteriorAnchor>()).Where(a => a != null && a.Action == action);

        static IEnumerable<BoatInteriorAnchor> KitchenAnchors(BoatInteriorDef def) =>
            (def.Anchors ?? Array.Empty<BoatInteriorAnchor>()).Where(a => a != null && KitchenActions.Contains(a.Action));

        static IEnumerable<BoatInteriorLevel> LevelsAt(BoatInteriorDef def, float z) =>
            def.Levels.Where(l => l != null && Mathf.Abs(l.SoleZMeters - z) <= def.FloorTolerance);

        static bool StandsClear(BoatInteriorLevel level, Vector2 p) =>
            BoatInteriorKitchen.Inside(level.Outline, p) &&
            !(level.Obstructions ?? Array.Empty<BoatInteriorObstruction>())
                .Any(o => o != null && BoatInteriorKitchen.Inside(o.Footprint, p));

        [Test]
        public void TheFourStoresShipWithUniqueIds()
        {
            Dictionary<string, KitchenStorageDef> stores = ShippedStores();
            CollectionAssert.IsSupersetOf(stores.Keys, new[]
            {
                "kitchen_storage.pantry_locker", "kitchen_storage.pantry", "kitchen_storage.cooler", "kitchen_storage.fridge",
            });
            Assert.AreEqual(KitchenStorageKind.Cooler, stores["kitchen_storage.cooler"].Kind);
            Assert.AreEqual(KitchenStorageKind.Fridge, stores["kitchen_storage.fridge"].Kind);
            Assert.AreEqual(KitchenStorageKind.Pantry, stores["kitchen_storage.pantry"].Kind);
            Assert.AreEqual(KitchenStorageKind.Pantry, stores["kitchen_storage.pantry_locker"].Kind);
            Assert.Greater(stores["kitchen_storage.cooler"].IceHoursPerUnit, 0f, "a cooler's ice must melt");
        }

        [Test]
        public void EveryKitHullThatCooksHasAColdStoreADryStoreAndASeat()
        {
            Dictionary<string, KitchenStorageDef> stores = ShippedStores();
            var faults = new List<string>();
            List<BoatInteriorDef> defs = KitDefs();
            Assert.Greater(defs.Count, 0, "no kit def to hold to the ask");
            foreach (BoatInteriorDef def in defs)
            {
                if (!Anchors(def, BoatInteriorAnchor.ActionCook).Any()) continue;
                KitchenStorageKind[] kinds = Anchors(def, BoatInteriorAnchor.ActionFoodStorage)
                    .Select(a => stores.TryGetValue(a.KitchenStorageId ?? "", out KitchenStorageDef s) ? s.Kind : (KitchenStorageKind)(-1))
                    .ToArray();
                if (!kinds.Contains(KitchenStorageKind.Cooler) && !kinds.Contains(KitchenStorageKind.Fridge))
                    faults.Add($"{def.Id}: cooks but has no cooler or fridge");
                if (!kinds.Contains(KitchenStorageKind.Pantry))
                    faults.Add($"{def.Id}: cooks but has no pantry or pantry locker");
                if (!Anchors(def, BoatInteriorAnchor.ActionSit).Any(a => a.HasFurnitureGeometry))
                    faults.Add($"{def.Id}: cooks but has no seat on drawn furniture");
            }
            Assert.IsEmpty(faults, string.Join("\n", faults));
        }

        [Test]
        public void EveryFoodStoreNamesAShippedDef()
        {
            Dictionary<string, KitchenStorageDef> stores = ShippedStores();
            var faults = new List<string>();
            foreach (BoatInteriorDef def in KitDefs())
                foreach (BoatInteriorAnchor a in Anchors(def, BoatInteriorAnchor.ActionFoodStorage))
                    if (!stores.ContainsKey(a.KitchenStorageId ?? ""))
                        faults.Add($"{def.Id}: '{a.Id}' names store '{a.KitchenStorageId}', which does not ship");
            Assert.IsEmpty(faults, string.Join("\n", faults));
        }

        [Test]
        public void EveryKitchenReachStandsOnTheSoleClearOfObstructions()
        {
            var faults = new List<string>();
            foreach (BoatInteriorDef def in KitDefs())
                foreach (BoatInteriorAnchor a in KitchenAnchors(def))
                    if (!LevelsAt(def, a.ReachPoint.z).Any(l => StandsClear(l, a.ReachPoint)))
                        faults.Add($"{def.Id}: {a.Action} anchor '{a.Id}' reach {a.ReachPoint} is not on a walkable " +
                                   "sole clear of every obstruction");
            Assert.IsEmpty(faults, string.Join("\n", faults));
        }

        [Test]
        public void EveryKitchenReachStandsApartFromEveryOtherReachOnItsFloor()
        {
            float min = BoatInteriorAnchor.KitchenAnchorMinSeparationMetres;
            var faults = new List<string>();
            foreach (BoatInteriorDef def in KitDefs())
            {
                BoatInteriorAnchor[] all = (def.Anchors ?? Array.Empty<BoatInteriorAnchor>()).Where(a => a != null).ToArray();
                foreach (BoatInteriorAnchor k in KitchenAnchors(def))
                    foreach (BoatInteriorAnchor other in all)
                    {
                        if (ReferenceEquals(k, other)) continue;
                        if (Mathf.Abs(k.ReachPoint.z - other.ReachPoint.z) > def.FloorTolerance) continue;
                        float d = Vector2.Distance(k.ReachPoint, other.ReachPoint);
                        if (d < min)
                            faults.Add($"{def.Id}: '{k.Id}' and '{other.Id}' reaches are {d:0.000} m apart (< {min})");
                    }
            }
            Assert.IsEmpty(faults, string.Join("\n", faults));
        }

        [Test]
        public void EverySitPointIsOnItsSeatAndEveryTableTopOnItsTable()
        {
            var faults = new List<string>();
            foreach (BoatInteriorDef def in KitDefs())
                foreach (string action in new[] { BoatInteriorAnchor.ActionSit, BoatInteriorAnchor.ActionTable })
                    foreach (BoatInteriorAnchor a in Anchors(def, action))
                    {
                        if (!a.HasFurnitureGeometry) { faults.Add($"{def.Id}: {action} '{a.Id}' gives no furniture"); continue; }
                        BoatInteriorLevel level = def.Level(a.FurnitureLevel);
                        BoatInteriorObstruction piece = level?.Obstructions?.FirstOrDefault(o => o != null && o.Id == a.FurnitureObstruction);
                        if (piece == null)
                        {
                            faults.Add($"{def.Id}: '{a.Id}' names '{a.FurnitureObstruction}' on '{a.FurnitureLevel}', which is not there");
                            continue;
                        }
                        if (!BoatInteriorKitchen.Inside(piece.Footprint, a.FurniturePoint))
                            faults.Add($"{def.Id}: '{a.Id}' point {a.FurniturePoint} is outside '{piece.Id}'");
                        if (!(a.FurniturePoint.z > level.SoleZMeters))
                            faults.Add($"{def.Id}: '{a.Id}' point z {a.FurniturePoint.z} is not above its sole {level.SoleZMeters}");
                    }
            Assert.IsEmpty(faults, string.Join("\n", faults));
        }

        [Test]
        public void ThePacketsPrepStandsOnHerHouseSoleNotTheDeckBelowIt()
        {
            BoatInteriorDef packet = KitDefs().Single(d => d.Id == "interior.coastal_packet_iso");
            BoatInteriorAnchor prep = Anchors(packet, BoatInteriorAnchor.ActionPrep).First();
            Assert.AreEqual("house_sole", BoatInteriorKitchen.LevelOfReach(packet, prep),
                            "two planes at z 5: the galley stands on the house sole, inside the deckhouse box of the main deck");
        }

        [Test]
        public void WhichHullsDineIsPinned()
        {
            KitchenSettings settings = KitchenSettings.Default;
            var dines = new List<string>();
            var faults = new List<string>();
            foreach (BoatInteriorDef def in KitDefs())
            {
                if (!Anchors(def, BoatInteriorAnchor.ActionSit).Any()) continue;
                List<KitchenPiece> pieces = BoatInteriorKitchen.Pieces(def);
                foreach (KitchenPiece p in pieces)
                    if (p.Kind != KitchenPieceKind.Seat && p.Kind != KitchenPieceKind.Table && p.Level.Length == 0)
                        faults.Add($"{def.Id}: piece '{p.Id}' stands on no level");
                if (pieces.Any(p => p.Kind == KitchenPieceKind.Seat && KitchenLayout.IsDiningSeat(p, pieces, settings)))
                    dines.Add(def.Id);
            }
            Assert.IsEmpty(faults, string.Join("\n", faults));
            CollectionAssert.AreEquivalent(Dines, dines);
        }

        /// <summary>Each kit def's kitchen anchors are its sidecar's, field for field (the builder carries the
        /// sidecar into the def; this holds the hand-mirrored pair together until the next re-bake).</summary>
        [Test]
        public void EveryKitDefsKitchenAnchorsAreItsSidecars()
        {
            string root = System.IO.Directory.GetParent(Application.dataPath)!.FullName;
            const float tol = 1e-4f;
            var faults = new List<string>();
            foreach (BoatInteriorDef def in KitDefs())
            {
                if (string.IsNullOrEmpty(def.SourceSidecar)) continue;
                string path = System.IO.Path.Combine(root, def.SourceSidecar);
                if (!System.IO.File.Exists(path)) { faults.Add($"{def.Id}: sidecar {def.SourceSidecar} missing"); continue; }
                object json = DeckSidecarJson.Parse(System.IO.File.ReadAllText(path));
                var fromSidecar = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (object e in DeckSidecarJson.AsArray(DeckSidecarJson.Member(json, "INTERACT")) ?? new List<object>())
                    if (KitchenActions.Contains(DeckSidecarJson.String(DeckSidecarJson.Member(e, "action"))))
                        fromSidecar[DeckSidecarJson.String(DeckSidecarJson.Member(e, "id")) ?? ""] = e;

                BoatInteriorAnchor[] baked = KitchenAnchors(def).ToArray();
                if (baked.Length != fromSidecar.Count)
                    faults.Add($"{def.Id}: {baked.Length} kitchen anchors baked, {fromSidecar.Count} in {def.SourceSidecar}");
                foreach (BoatInteriorAnchor a in baked)
                {
                    if (!fromSidecar.TryGetValue(a.Id, out object e)) { faults.Add($"{def.Id}: '{a.Id}' is not in its sidecar"); continue; }
                    void Same(string what, Vector3 have, object raw)
                    {
                        List<object> p = DeckSidecarJson.AsArray(raw);
                        var v = p == null || p.Count < 3 ? new Vector3(float.NaN, float.NaN, float.NaN)
                            : new Vector3(DeckSidecarJson.Float(p[0]), DeckSidecarJson.Float(p[1]), DeckSidecarJson.Float(p[2]));
                        if (!(Vector3.Distance(have, v) <= tol))
                            faults.Add($"{def.Id}: '{a.Id}' {what} baked {have}, sidecar {v}");
                    }
                    if (DeckSidecarJson.String(DeckSidecarJson.Member(e, "action")) != a.Action)
                        faults.Add($"{def.Id}: '{a.Id}' action differs from the sidecar");
                    Same("reach", a.ReachPoint, DeckSidecarJson.Member(e, "reach_point"));
                    if ((DeckSidecarJson.String(DeckSidecarJson.Member(e, "kitchen_storage")) ?? "") != (a.KitchenStorageId ?? ""))
                        faults.Add($"{def.Id}: '{a.Id}' store differs from the sidecar");
                    object furniture = a.Action == BoatInteriorAnchor.ActionSit ? DeckSidecarJson.Member(e, "seat")
                                     : a.Action == BoatInteriorAnchor.ActionTable ? DeckSidecarJson.Member(e, "table") : null;
                    if (furniture == null) continue;
                    Same("furniture point", a.FurniturePoint,
                         DeckSidecarJson.Member(furniture, a.Action == BoatInteriorAnchor.ActionSit ? "sit_point" : "top_centre"));
                    if (DeckSidecarJson.String(DeckSidecarJson.Member(furniture, "level")) != a.FurnitureLevel ||
                        DeckSidecarJson.String(DeckSidecarJson.Member(furniture, "obstruction")) != a.FurnitureObstruction)
                        faults.Add($"{def.Id}: '{a.Id}' furniture level/obstruction differ from the sidecar");
                }
            }
            Assert.IsEmpty(faults, string.Join("\n", faults));
        }
    }
}
