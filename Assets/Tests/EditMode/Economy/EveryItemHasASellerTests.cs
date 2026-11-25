using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>
    /// <b>EVERY ITEM IS SOLD SOMEWHERE</b> (owner ask 11-15: "all items are purchasable"). A content
    /// guard over the shipped data: every obtainable item Def has at least one seller offer on file
    /// naming a seller that exists, or it sits on the commented exemption list below with the reason.
    ///
    /// <para><b>What "a seller offer" means here.</b> An offer asset (a listing Def or a
    /// <see cref="VehicleOffer"/>) that names the item and at least one real <see cref="SellerDef"/>.
    /// It does NOT ask that the offer is on a shelf today: a listed ware needs a vendor committed in
    /// its scene, and that half is <c>StoreWiringTests</c> guard 4. An offer authored unlisted, with
    /// its eventual seller named, is the data half of "purchasable"; listing it is one tick plus the
    /// store patch. The audit table in <c>docs/design/economy-item-sellers.md</c> says which items
    /// are on a shelf now and which wait for that patch.</para>
    ///
    /// <para>Read through <see cref="SerializedObject"/> by field name, so the guard names no
    /// gameplay module's classes (rule 4) and a Def type added later is one row in
    /// <see cref="ItemKinds"/>.</para>
    /// </summary>
    public class EveryItemHasASellerTests
    {
        const string DataRoot = "Assets/_Project/Data";

        // Known missing sellers: these unplaced repair supplies need four separate counted store
        // slots, goods pictures and empty-stock cards. Remove each exception when its offer exists.
        static readonly Dictionary<string, string> KnownMendingSellerExceptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["supply.patch_plank"] = "needs shop: St Peters general store; patch-plank slot, goods picture and empty-stock card are not placed",
            ["supply.copper_nails"] = "needs shop: St Peters general store; copper-nails slot, goods picture and empty-stock card are not placed",
            ["supply.oakum"] = "needs shop: St Peters general store; oakum slot, goods picture and empty-stock card are not placed",
            ["supply.linseed_paint"] = "needs shop: St Peters general store; linseed-paint slot, goods picture and empty-stock card are not placed",
        };

        // Disposable proof copies some fixtures leave beside real Defs (DefIdentityContractTests' rule).
        static readonly Regex DisposableProof = new Regex("_[0-9a-f]{32}(/|\\.|$)");

        /// <summary>The item Defs the player can own: the Def type, the field holding the id the
        /// player owns, and an optional bool field that must be true for the Def to count.</summary>
        static readonly (string Type, string IdField, string OnlyWhen)[] ItemKinds =
        {
            ("ToolDef", "GearId", null),
            ("StowageDef", "GearId", null),
            ("TackleDef", "Id", null),
            ("TrapDef", "Id", null),
            ("BoatHullDef", "Id", null),
            ("VehicleDef", "Id", null),
            ("WatchDef", "Id", null),
            ("GolfClubDef", "Id", null),
            ("LonglineDef", "Id", null),
            ("FuelContainerDef", "Id", "Carriable"),
        };

        /// <summary>
        /// Items with no seller offer, and why. An entry ending in <c>*</c> covers every id with
        /// that prefix. "needs shop: X" names the shop that would sell it once it exists; no shop is
        /// invented here. Remove a line when its item gets an offer.
        /// </summary>
        static readonly Dictionary<string, string> Exempt = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // ---- Starting kit -----------------------------------------------------------------------
            ["watch.harbour_digital"] = "starting item: worn from the first morning (StartsOwned); needs shop: a watch seller for a replacement",
            // Gear carried from the start of every game (the headlamp) is exempt by reading the shipped
            // GameConfig.CarriedFromStartGearIds, not by a line here: see CarriedFromStart().

            // ---- No ownership or purchase seam yet (an offer type is code, not data) ------------------
            ["tackle.*"] = "not owned piece by piece yet: the rod's tackle box hands every lure; needs an owned-tackle seam, then shop: the NMC fishing gear wholesaler",
            ["longline.groundfish_tub"] = "counted tub stock has no offer type to buy it through; needs shop: the NMC fishing gear wholesaler once a tub offer exists",
            ["fuelstore.*"] = "cans stand at the pump as station stock; an empty can needs an offer type; needs shop: a fuel station till",
            ["golf_club.*"] = "lent with the round at the course; needs shop: a pro shop at the course",

            // ---- Built into a home, not bought (owner 2026-11-17: storage with memory) ---------------
            ["stowage.ginny_*"] = "built into his room at Ginny's: comes with lodging, never sold",
            ["stowage.camper_*"] = "built into the camper: comes with the camper's deed, never sold on its own",
            ["stowage.shed_*"] = "built into the small shed on the camper lot: comes with the camper's deed, never sold on its own",

            // ---- Regional lobster builds with no yard in their region yet ----------------------------
            ["boat.lobster_inshore_open_fundy"] = "needs shop: a Bay of Fundy boatyard",
            ["boat.lobster_inshore_hardtop_fundy"] = "needs shop: a Bay of Fundy boatyard",
            ["boat.lobster_standard_open_fundy"] = "needs shop: a Bay of Fundy boatyard",
            ["boat.lobster_standard_hardtop_fundy"] = "needs shop: a Bay of Fundy boatyard",
            ["boat.lobster_offshore_open_fundy"] = "needs shop: a Bay of Fundy boatyard",
            ["boat.lobster_offshore_hardtop_fundy"] = "needs shop: a Bay of Fundy boatyard",
            ["boat.lobster_inshore_open_newfoundland"] = "needs shop: a Newfoundland boatyard",
            ["boat.lobster_inshore_hardtop_newfoundland"] = "needs shop: a Newfoundland boatyard",
            ["boat.lobster_standard_open_newfoundland"] = "needs shop: a Newfoundland boatyard",
            ["boat.lobster_standard_hardtop_newfoundland"] = "needs shop: a Newfoundland boatyard",
            ["boat.lobster_offshore_open_newfoundland"] = "needs shop: a Newfoundland boatyard",
            ["boat.lobster_offshore_hardtop_newfoundland"] = "needs shop: a Newfoundland boatyard",

            // ---- Pleasure craft: a working yard does not sell these ----------------------------------
            ["boat.bowrider"] = "needs shop: a pleasure-boat dealer",
            ["boat.express_cruiser"] = "needs shop: a pleasure-boat dealer",
            ["boat.go_fast"] = "needs shop: a pleasure-boat dealer",
            ["boat.jet_ski"] = "needs shop: a pleasure-boat dealer",
            ["boat.yacht_tender"] = "needs shop: a pleasure-boat dealer",
            ["boat.sport_fisher_convertible"] = "needs shop: a pleasure-boat dealer",
            ["boat.sport_fisher_skybridge"] = "needs shop: a pleasure-boat dealer",
            ["boat.sloop_30"] = "needs shop: a pleasure-boat dealer",
            ["boat.sloop_88"] = "needs shop: a yacht broker",
            ["boat.motor_yacht_84"] = "needs shop: a yacht broker",
            ["boat.expedition_yacht_120"] = "needs shop: a yacht broker",

            // ---- Ocean-going ships, beyond a village yard ---------------------------------------------
            ["boat.stern_trawler"] = "needs shop: a ship broker",
            ["boat.stern_trawler_mk2"] = "needs shop: a ship broker",
            ["boat.coastal_packet"] = "needs shop: a ship broker",
            ["boat.tanker"] = "needs shop: a ship broker",
        };

        // =========================================================================================

        [Test]
        public void EveryObtainableItem_HasASellerOffer_OrAStatedExemption()
        {
            HashSet<string> sellers = SellerIds();
            Dictionary<string, List<string>> offered = OfferedItems(sellers);
            List<(string Id, string Path)> items = Items();
            Assert.That(items, Is.Not.Empty, "no item Defs found: the guard would pass on nothing");

            var unsold = new List<string>();
            foreach ((string id, string path) in items)
            {
                if (string.IsNullOrEmpty(id)) { unsold.Add(path + " has no id"); continue; }
                if (offered.ContainsKey(id) || ExemptionFor(id) != null) continue;
                unsold.Add(id + " (" + path + ")");
            }
            Assert.IsEmpty(unsold,
                "these items have no offer naming a real seller and no exemption. Add an offer asset to the most " +
                "fitting existing seller (docs/design/economy-item-sellers.md), or an exemption saying which shop it needs:\n  " +
                string.Join("\n  ", unsold.Distinct()));
        }

        [Test]
        public void EveryItemKind_FindsItsDefs()
        {
            foreach (var kind in ItemKinds)
                Assert.IsNotEmpty(AssetDatabase.FindAssets("t:" + kind.Type, new[] { DataRoot }),
                    kind.Type + " finds no assets: renamed or moved? The guard would skip that whole kind");
        }

        [Test]
        public void EveryExemption_NamesARealItem()
        {
            var ids = new HashSet<string>(Items().Select(i => i.Id).Where(i => !string.IsNullOrEmpty(i)), StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> e in Exempt.Concat(KnownMendingSellerExceptions))
            {
                Assert.IsNotEmpty(e.Value, e.Key + " is exempt with no reason");
                bool hit = e.Key.EndsWith("*", StringComparison.Ordinal)
                    ? ids.Any(i => i.StartsWith(e.Key.Substring(0, e.Key.Length - 1), StringComparison.Ordinal))
                    : ids.Contains(e.Key);
                Assert.IsTrue(hit, "the exemption " + e.Key + " names no item Def: a typo, or the item is gone");
            }
        }

        [Test]
        public void KnownMendingExceptionsAreExactlyTheUnstockedRecipeSupplies()
        {
            var def = AssetDatabase.LoadAssetAtPath<DoryMendingDef>(
                DataRoot + "/Resources/DoryMending/StPetersDory.asset");
            Assert.IsNotNull(def);
            var materials = new HashSet<string>(def.Jobs.SelectMany(j => j.Materials).Select(m => m.ItemId));
            Assert.That(KnownMendingSellerExceptions.Count, Is.EqualTo(4));
            CollectionAssert.AreEquivalent(materials, KnownMendingSellerExceptions.Keys);
            var offered = OfferedItems(SellerIds());
            var supplies = Load<SupplyDef>().ToDictionary(s => s.Id);
            foreach (var entry in KnownMendingSellerExceptions)
            {
                Assert.That(Exempt.ContainsKey(entry.Key), Is.False, "one exception list per supply");
                Assert.That(offered.ContainsKey(entry.Key), Is.False, "remove the exception once a seller exists");
                Assert.That(supplies[entry.Key].Catalog.Listed, Is.False, "unplaced supplies must stay unlisted");
                Assert.That(supplies[entry.Key].Catalog.Sellers, Is.Empty);
                StringAssert.Contains("slot, goods picture and empty-stock card", entry.Value);
            }
        }

        [Test]
        public void NoBoatElectronic_IsExempt()
        {
            // The Loran's in-flight exemption went when it got a seller; every instrument now has an offer
            // naming a real seller (the boat/gear wholesaler for the electronics, ruling 11-16), so none may
            // come back onto the list.
            List<string> instruments = Exempt.Keys.Where(k => k.StartsWith("instrument.", StringComparison.Ordinal)).ToList();
            Assert.IsEmpty(instruments, "boat electronics are exempt from the seller guard: " + string.Join(", ", instruments));
        }

        [Test]
        public void EveryBoatAndPotOffer_PointsAtARealDef()
        {
            var boats = new HashSet<string>(IdsOf("BoatHullDef", "Id"), StringComparer.Ordinal);
            var traps = new HashSet<string>(IdsOf("TrapDef", "Id"), StringComparer.Ordinal);
            foreach (ShipwrightOffer o in Load<ShipwrightOffer>())
                Assert.IsTrue(boats.Contains(o.BoatId), AssetDatabase.GetAssetPath(o) + " sells " + o.BoatId + ", which no BoatHullDef has");
            foreach (PotOffer o in Load<PotOffer>())
                Assert.IsTrue(traps.Contains(o.TrapDefId), AssetDatabase.GetAssetPath(o) + " sells " + o.TrapDefId + ", which no TrapDef has");
        }

        // =========================================================================================

        [Test]
        public void EveryCarriedFromStartId_NamesARealItem()
        {
            var ids = new HashSet<string>(Items().Select(i => i.Id).Where(i => !string.IsNullOrEmpty(i)), StringComparer.Ordinal);
            foreach (string id in CarriedFromStart())
                Assert.IsTrue(ids.Contains(id), "GameConfig carries " + id + " from the start, but no item Def has that id");
        }

        // =========================================================================================

        const string GameConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        static HashSet<string> _carriedFromStart;

        /// <summary>The shipped config's carried-from-the-start gear (owner ruling 2026-11-22: owned from
        /// the first morning, never bought), so no shop is owed for them.</summary>
        static HashSet<string> CarriedFromStart()
        {
            if (_carriedFromStart != null) return _carriedFromStart;
            var config = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Core.GameConfig>(GameConfigPath);
            Assert.IsNotNull(config, "the shipped GameConfig is not at " + GameConfigPath);
            _carriedFromStart = new HashSet<string>(
                HiddenHarbours.Core.GameConfig.CarriedFromStart(config).Where(i => !string.IsNullOrEmpty(i)),
                StringComparer.Ordinal);
            return _carriedFromStart;
        }

        static string ExemptionFor(string id)
        {
            if (KnownMendingSellerExceptions.TryGetValue(id, out string pending)) return pending;
            if (Exempt.TryGetValue(id, out string why)) return why;
            if (CarriedFromStart().Contains(id))
                return "starting item: carried from the start of every game (GameConfig.CarriedFromStartGearIds), never bought";
            foreach (KeyValuePair<string, string> e in Exempt)
                if (e.Key.EndsWith("*", StringComparison.Ordinal)
                    && id.StartsWith(e.Key.Substring(0, e.Key.Length - 1), StringComparison.Ordinal))
                    return e.Value;
            return null;
        }

        static HashSet<string> SellerIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (SellerDef s in Resources.LoadAll<SellerDef>(SellerDirectory.ResourceFolder))
                if (s != null && !string.IsNullOrEmpty(s.Id)) ids.Add(s.Id);
            Assert.IsNotEmpty(ids, "no SellerDefs found: every item would read as unsold");
            return ids;
        }

        /// <summary>Item id to the real sellers any offer names for it.</summary>
        static Dictionary<string, List<string>> OfferedItems(HashSet<string> sellers)
        {
            var offered = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            void Offer(string itemId, IEnumerable<string> names)
            {
                if (string.IsNullOrEmpty(itemId) || names == null) return;
                foreach (string s in names)
                {
                    if (string.IsNullOrEmpty(s) || !sellers.Contains(s)) continue;
                    if (!offered.TryGetValue(itemId, out List<string> list)) offered[itemId] = list = new List<string>();
                    if (!list.Contains(s)) list.Add(s);
                }
            }

            foreach (ShipwrightOffer o in Load<ShipwrightOffer>()) Offer(o.BoatId, o.Catalog.Sellers);
            foreach (PotOffer o in Load<PotOffer>()) Offer(o.TrapDefId, o.Catalog.Sellers);
            foreach (VehicleOffer o in Load<VehicleOffer>())
            {
                Offer(o.VehicleId, o.Catalog.Sellers);
                Offer(o.VehicleId, new[] { o.SellerId });
            }
            foreach (GearOffer o in Load<GearOffer>()) Offer(o.Id, o.Catalog.Sellers);
            foreach (BaitDef o in Load<BaitDef>()) Offer(o.Id, o.Catalog.Sellers);
            foreach (SupplyDef o in Load<SupplyDef>()) Offer(o.Id, o.Catalog.Sellers);
            foreach (InstrumentOffer o in Load<InstrumentOffer>()) Offer(o.Id, o.Catalog.Sellers);
            foreach (LicenseDef o in Load<LicenseDef>()) Offer(o.Id, o.Catalog.Sellers);

            // A watch carries its own seller and price rather than a catalog listing.
            foreach (string path in Paths("WatchDef"))
            {
                var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
                SerializedProperty price = so.FindProperty("Price");
                if (price != null && price.intValue > 0)
                    Offer(Str(so, "Id"), new[] { Str(so, "SellerId") });
            }
            return offered;
        }

        /// <summary>Every obtainable item: the Def kinds above, plus every listing Def, which is an
        /// item in its own right (a bag of ice, a licence, an instrument).</summary>
        static List<(string Id, string Path)> Items()
        {
            var items = new List<(string, string)>();
            foreach (var kind in ItemKinds)
                foreach (string path in Paths(kind.Type))
                {
                    var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
                    if (kind.OnlyWhen != null)
                    {
                        SerializedProperty gate = so.FindProperty(kind.OnlyWhen);
                        if (gate != null && !gate.boolValue) continue;
                    }
                    string id = Str(so, kind.IdField);
                    if (string.IsNullOrEmpty(id)) id = Str(so, "Id");
                    items.Add((id, path));
                }
            foreach (GearOffer o in Load<GearOffer>()) items.Add((o.Id, AssetDatabase.GetAssetPath(o)));
            foreach (BaitDef o in Load<BaitDef>()) items.Add((o.Id, AssetDatabase.GetAssetPath(o)));
            foreach (SupplyDef o in Load<SupplyDef>()) items.Add((o.Id, AssetDatabase.GetAssetPath(o)));
            foreach (InstrumentOffer o in Load<InstrumentOffer>()) items.Add((o.Id, AssetDatabase.GetAssetPath(o)));
            foreach (LicenseDef o in Load<LicenseDef>()) items.Add((o.Id, AssetDatabase.GetAssetPath(o)));
            return items;
        }

        static IEnumerable<string> IdsOf(string type, string field)
        {
            foreach (string path in Paths(type))
                yield return Str(new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path)), field);
        }

        static IEnumerable<string> Paths(string type)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + type, new[] { DataRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (DisposableProof.IsMatch(path)) continue;
                if (AssetDatabase.LoadMainAssetAtPath(path) is ScriptableObject) yield return path;
            }
        }

        static List<T> Load<T>() where T : ScriptableObject
        {
            var list = new List<T>();
            foreach (string path in Paths(typeof(T).Name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        static string Str(SerializedObject so, string field)
        {
            SerializedProperty p = so.FindProperty(field);
            return p != null && p.propertyType == SerializedPropertyType.String ? p.stringValue : "";
        }
    }
}
