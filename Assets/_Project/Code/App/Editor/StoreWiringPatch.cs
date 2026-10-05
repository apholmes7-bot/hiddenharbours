#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.Economy;             // the vendors, the listings (ICatalogListing, CatalogSource), CatalogBookPresenter
using Doc = HiddenHarbours.App.Editor.StPetersLayerRefresh.Doc;
using IdAllocator = HiddenHarbours.App.Editor.StPetersLayerRefresh.IdAllocator;
using LayerPatch = HiddenHarbours.App.Editor.StPetersLayerRefresh.LayerPatch;
using ObjRef = HiddenHarbours.App.Editor.StPetersLayerRefresh.ObjRef;
using Refusal = HiddenHarbours.App.Editor.StPetersLayerRefresh.Refusal;
using SceneYaml = HiddenHarbours.App.Editor.StPetersLayerRefresh.SceneYaml;
using ScriptRef = HiddenHarbours.App.Editor.StPetersLayerRefresh.ScriptRef;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE STORE'S SELLER IDS AND WARES BOOKS, WRITTEN INTO THE COMMITTED SCENES</b> (O4). The builders
    /// give every store vendor its seller id and each region's DialogueUI its wares book
    /// (<see cref="CatalogBookPresenter"/>), but neither region can be rebuilt — Build() wipes the scene,
    /// and both carry hand work — so the committed StPeters.unity and NineMileCreek.unity hold vendors that
    /// sell as no one and no book, and a "show me your wares" row finds neither. This step writes the
    /// missing lines into the scene files as text, with <see cref="StPetersLayerRefresh"/>'s patches.
    ///
    /// <para><b>The wiring is a table per region</b> (<see cref="StPeters"/>, <see cref="NineMileCreek"/>): the
    /// root a vendor stands on, its class, the listing it stocks (by listing id, resolved to the listing
    /// asset's guid), and the seller id it sells as — always the builder's own constant, so the scene and the
    /// builder can only ever name the same id. A vendor is found by all three of root, class and wares, and
    /// anything but exactly one match refuses.</para>
    ///
    /// <para><b>One patch per root</b>, each sealed by <see cref="LayerPatch.Seal"/> before anything is
    /// written: a vendor gains one <c>_sellerId</c> line between its wares and its wallet, where Unity
    /// serialises the field, and the book joins DialogueUI as one new document, listed last among its
    /// components. Nothing is deleted. A vendor that already sells as its id, or a DialogueUI that already
    /// holds its book, is left alone, so a second plan is empty; a vendor that sells as a DIFFERENT id
    /// refuses: the step never overwrites an id.</para>
    ///
    /// <para><b>No scene is opened.</b> The scenes are files the menu reads and, only when asked to apply,
    /// writes, after every patch of both regions is planned and sealed; the patches go to
    /// <see cref="PatchFolder"/> first. <c>StoreWiringTests</c> hold the committed scenes to this step.</para>
    /// </summary>
    public static class StoreWiringPatch
    {
        public const string PatchFolder = "artifacts/store-wiring";
        public const string NineMileCreekScenePath = "Assets/_Project/Scenes/NineMileCreek.unity";

        /// <summary>The root each region's wares book hangs on: the GameObject both builders name DialogueUI.</summary>
        public const string BookRoot = "DialogueUI";

        const string SellerIdField = "_sellerId";
        const string WalletField = "_walletProvider";

        /// <summary>An id the scene can hold as a plain YAML scalar, as Unity writes one: <c>type.snake_case</c>.</summary>
        static readonly Regex PlainId = new Regex(@"^[a-z0-9_]+(\.[a-z0-9_]+)+$", RegexOptions.CultureInvariant);

        // =====================================================================================
        //  the wiring
        // =====================================================================================

        /// <summary>One vendor of the wiring: the root it stands on, its class, the listing id of what it
        /// stocks, and the seller id it sells as.</summary>
        public readonly struct Seller
        {
            public readonly string Root;
            public readonly Type Kind;
            public readonly string Wares;
            public readonly string SellerId;

            public Seller(string root, Type kind, string wares, string sellerId)
            {
                Root = root; Kind = kind; Wares = wares; SellerId = sellerId;
            }

            public override string ToString() => $"{Root}'s {Kind?.Name} ({Wares})";
        }

        /// <summary>One region's wiring: its scene file and its vendors, in the order its builder adds them.</summary>
        public sealed class Region
        {
            public readonly string Name;
            public readonly string ScenePath;
            public readonly IReadOnlyList<Seller> Sellers;

            public Region(string name, string scenePath, IReadOnlyList<Seller> sellers)
            {
                Name = name; ScenePath = scenePath; Sellers = sellers;
            }
        }

        /// <summary>St Peters: the general store counter's five vendors, all Marguerite's.</summary>
        public static readonly Region StPeters = new Region("StPeters", StPetersLayerRefresh.ScenePath, new[]
        {
            new Seller("GeneralStoreCounter", typeof(GearShop), "gear.rod", StPetersBuilder.StoreSellerId),
            new Seller("GeneralStoreCounter", typeof(BaitShop), "bait.capelin", StPetersBuilder.StoreSellerId),
            new Seller("GeneralStoreCounter", typeof(SupplyShop), "supply.ice", StPetersBuilder.StoreSellerId),
            new Seller("GeneralStoreCounter", typeof(InstrumentShop), "instrument.depth_sounder", StPetersBuilder.StoreSellerId),
            new Seller("GeneralStoreCounter", typeof(LicenseVendor), "license.clam", StPetersBuilder.StoreSellerId),
        });

        /// <summary>Nine Mile Creek: the yard's shed and dory yard, the harbourmaster, the chandlery, and Hector's barrel.</summary>
        public static readonly Region NineMileCreek = new Region("NineMileCreek", NineMileCreekScenePath, new[]
        {
            new Seller("ShipwrightShed", typeof(Shipwright), "boat.punt", NineMileCreekBuilder.SellerYard),
            new Seller("ShipwrightShed", typeof(PotShop), "offer.lobster_pot", NineMileCreekBuilder.SellerYard),
            new Seller("ShipwrightShed", typeof(PotShop), "offer.crab_pot", NineMileCreekBuilder.SellerYard),
            new Seller("HarbourmasterOffice", typeof(LicenseVendor), "license.cod", NineMileCreekBuilder.SellerHarbourmaster),
            new Seller("GeneralStore", typeof(GearShop), "gear.rod", NineMileCreekBuilder.SellerChandlery),
            new Seller("ShipwrightDoryYard", typeof(Shipwright), "boat.dory", NineMileCreekBuilder.SellerYard),
            new Seller("UsedOutboardSeller", typeof(Shipwright), "boat.dory_outboard", NineMileCreekBuilder.SellerHectorsBarrel),
        });

        public static readonly IReadOnlyList<Region> Regions = new[] { StPeters, NineMileCreek };

        /// <summary>What the step reads from the project, so a test can hand it its own: a listing's asset by
        /// its id, and the script the book is written with.</summary>
        public interface IStoreWiringAssets
        {
            /// <summary>The guid of the one <paramref name="listingType"/> listing in the catalog whose id is
            /// <paramref name="listingId"/>; a refusal when there is not exactly one.</summary>
            string WaresGuid(Type listingType, string listingId);

            /// <summary>The script reference and class identifier a <see cref="CatalogBookPresenter"/> is written with.</summary>
            ScriptRef BookScript();
        }

        /// <summary>The menu's assets: the catalog's listings and the book's MonoScript, through the AssetDatabase.</summary>
        public sealed class EditorStoreWiringAssets : IStoreWiringAssets
        {
            public string WaresGuid(Type listingType, string listingId)
            {
                var hits = new List<string>();
                foreach (string guid in AssetDatabase.FindAssets("t:" + listingType.Name, new[] { CatalogSource.CatalogFolderPath }))
                    if (AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), listingType) is ICatalogListing listing &&
                        listing.ListingId == listingId)
                        hits.Add(guid);
                if (hits.Count != 1)
                    throw new Refusal($"{CatalogSource.CatalogFolderPath} holds {hits.Count} {listingType.Name} listings with the id " +
                                      $"'{listingId}'; the store wiring needs exactly one.");
                return hits[0];
            }

            public ScriptRef BookScript()
            {
                Type t = typeof(CatalogBookPresenter);
                foreach (string guid in AssetDatabase.FindAssets("t:MonoScript " + t.Name))
                {
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                    if (script != null && script.GetClass() == t &&
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string g, out long local))
                        return new ScriptRef(new ObjRef(local, g, 3), t.Assembly.GetName().Name + "::" + t.FullName);
                }
                throw new Refusal($"no MonoScript holds {t.FullName}: the wares book cannot be written.");
            }
        }

        // =====================================================================================
        //  the step: a pure function of the scene text and the wiring
        // =====================================================================================

        /// <summary>
        /// The store's patches to one region's scene: one per root the wiring names, in the wiring's order,
        /// then DialogueUI's for the book — each sealed against the scene. Apply them with <see cref="ApplyAll"/>.
        /// </summary>
        public static List<LayerPatch> Plan(string sceneText, Region region, IStoreWiringAssets assets)
        {
            SceneYaml scene = SceneYaml.Parse(sceneText);
            var patches = new List<LayerPatch>();
            var claimed = new HashSet<long>();
            foreach (IGrouping<string, Seller> root in region.Sellers.GroupBy(s => s.Root))
            {
                Doc go = scene.RootNamed(root.Key);
                var patch = new LayerPatch(StepOf(region, root.Key), root.Key, go.FileId);
                List<Doc> comps = scene.ComponentsOf(go);
                foreach (Seller s in root) Wire(patch, comps, s, assets, claimed);
                patches.Add(patch.Seal(scene));
            }
            patches.Add(Book(scene, region, assets).Seal(scene));
            return patches;
        }

        /// <summary>A region's scene text with its patches applied, in order. The patches touch disjoint roots,
        /// so each finds its documents as it planned them.</summary>
        public static string ApplyAll(string sceneText, IEnumerable<LayerPatch> patches) =>
            patches.Aggregate(sceneText, (text, p) => p.ApplyTo(text));

        static string StepOf(Region region, string root) => $"StoreWiring.{region.Name}.{root}";

        /// <summary>One vendor: found by root, class and wares; given its id unless it already sells as it.</summary>
        static void Wire(LayerPatch patch, List<Doc> comps, Seller s, IStoreWiringAssets assets, HashSet<long> claimed)
        {
            if (s.Kind == null || s.SellerId == null || !PlainId.IsMatch(s.SellerId))
                throw new Refusal($"{patch.Step}: {s} would sell as '{s.SellerId}', which is not a seller id (type.snake_case).");
            FieldInfo wares = WaresField(s.Kind);
            FieldInfo id = s.Kind.GetField(SellerIdField, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (id == null || id.FieldType != typeof(string) || !(id.IsPublic || id.IsDefined(typeof(SerializeField), false)))
                throw new Refusal($"{patch.Step}: {s.Kind.Name} serialises no string {SellerIdField}.");

            string guid = assets.WaresGuid(wares.FieldType, s.Wares);
            List<Doc> hits = comps.Where(c => c.IsScript(s.Kind.FullName) && ObjRef.Parse(c.Field(wares.Name)).Guid == guid).ToList();
            if (hits.Count != 1)
                throw new Refusal($"{patch.Step}: '{s.Root}' holds {hits.Count} {s.Kind.Name}s stocking {s.Wares} ({guid}); " +
                                  "the step needs exactly one.");
            Doc vendor = hits[0];
            if (!claimed.Add(vendor.FileId))
                throw new Refusal($"{patch.Step}: two rows of the wiring name {s} (&{vendor.FileId}).");

            string now = vendor.Field(SellerIdField);
            if (now == s.SellerId) return;
            if (!string.IsNullOrEmpty(now))
                throw new Refusal($"{patch.Step}: {s} (&{vendor.FileId}) sells as '{now}', and the wiring says '{s.SellerId}'. " +
                                  "The step never overwrites an id: put the scene or the wiring right by hand.");

            var lines = vendor.Lines.ToList();
            string line = $"  {SellerIdField}: {s.SellerId}";
            if (now == "") lines[lines.FindIndex(l => l.StartsWith($"  {SellerIdField}:", StringComparison.Ordinal))] = line;
            else
            {
                int at = lines.FindIndex(l => l.StartsWith($"  {wares.Name}: ", StringComparison.Ordinal));
                if (at < 0 || at + 1 >= lines.Count || !lines[at + 1].StartsWith($"  {WalletField}:", StringComparison.Ordinal))
                    throw new Refusal($"{patch.Step}: {s} (&{vendor.FileId}) does not hold its {WalletField} right after its " +
                                      $"{wares.Name}, where Unity writes its {SellerIdField} between them.");
                lines.Insert(at + 1, line);
            }
            patch.Edit(vendor, string.Join("\n", lines), $"{s} (&{vendor.FileId}): sells as {s.SellerId}");
            patch.Note($"{s.Root}'s {s.Kind.Name}", $"sells {s.Wares} as {s.SellerId}");
        }

        /// <summary>The one serialised field of a vendor class that holds its listing.</summary>
        static FieldInfo WaresField(Type kind)
        {
            List<FieldInfo> hits = kind.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => typeof(ICatalogListing).IsAssignableFrom(f.FieldType) &&
                            (f.IsPublic || f.IsDefined(typeof(SerializeField), false)))
                .ToList();
            if (hits.Count != 1)
                throw new Refusal($"StoreWiring: {kind.Name} serialises {hits.Count} fields holding a listing; the step needs exactly one.");
            return hits[0];
        }

        /// <summary>DialogueUI's patch: the wares book as a new document, listed last among its components,
        /// unless it holds one already.</summary>
        static LayerPatch Book(SceneYaml scene, Region region, IStoreWiringAssets assets)
        {
            Doc go = scene.RootNamed(BookRoot);
            var patch = new LayerPatch(StepOf(region, BookRoot), BookRoot, go.FileId);
            Type book = typeof(CatalogBookPresenter);
            int held = scene.ComponentsOf(go).Count(c => c.IsScript(book.FullName));
            if (held == 1) return patch;
            if (held > 1)
                throw new Refusal($"{patch.Step}: {BookRoot} (&{go.FileId}) holds {held} wares books; the step needs it to hold one at most.");

            ScriptRef script = assets.BookScript();
            long id = new IdAllocator(scene).Next($"{region.Name}/{BookRoot}/{book.Name}");
            string at = id.ToString(CultureInfo.InvariantCulture);
            patch.Add(string.Join("\n",
                          $"--- !u!114 &{at}",
                          "MonoBehaviour:",
                          "  m_ObjectHideFlags: 0",
                          "  m_CorrespondingSourceObject: {fileID: 0}",
                          "  m_PrefabInstance: {fileID: 0}",
                          "  m_PrefabAsset: {fileID: 0}",
                          $"  m_GameObject: {{fileID: {go.FileId.ToString(CultureInfo.InvariantCulture)}}}",
                          "  m_Enabled: 1",
                          "  m_EditorHideFlags: 0",
                          "  m_Script: " + script.Script.Yaml,
                          "  m_Name: ",
                          "  m_EditorClassIdentifier: " + script.ClassIdentifier),
                      $"{BookRoot}'s wares book, its {book.Name} (&{at})");

            var lines = go.Lines.ToList();
            int head = lines.IndexOf("  m_Component:");
            if (head < 0) throw new Refusal($"{patch.Step}: {BookRoot} (&{go.FileId}) lists no components.");
            int end = head + 1;
            while (end < lines.Count && lines[end].StartsWith("  - component: ", StringComparison.Ordinal)) end++;
            lines.Insert(end, $"  - component: {{fileID: {at}}}");
            patch.Edit(go, string.Join("\n", lines), $"{BookRoot} (&{go.FileId}): lists its wares book &{at} last");
            patch.Note(BookRoot, "holds the wares book", book.Name);
            return patch;
        }

        // =====================================================================================
        //  the menu: plan against the scene FILES, write the patches, apply only when asked
        // =====================================================================================

        [MenuItem("Hidden Harbours/World/Store Wiring/Write the Store Patches (dry run)")]
        static void WriteMenu() => FromMenu(apply: false);

        [MenuItem("Hidden Harbours/World/Store Wiring/Apply the Store Patches")]
        static void ApplyMenu()
        {
            if (EditorUtility.DisplayDialog("Store wiring",
                    "Write the store's seller ids and wares books into the StPeters.unity and NineMileCreek.unity FILES?\n\n" +
                    "Both scenes must be closed. The patches are written to " + PatchFolder + " first.", "Apply", "Cancel"))
                FromMenu(apply: true);
        }

        /// <summary>The dry run for <c>-executeMethod</c>: a refusal fails the run instead of logging.</summary>
        public static void WriteStorePatchesBatch() => Run(apply: false);

        /// <summary>The apply for <c>-executeMethod</c>, the menu's own path.</summary>
        public static void ApplyStorePatchesBatch() => Run(apply: true);

        static void FromMenu(bool apply)
        {
            try
            {
                Run(apply);
            }
            catch (Refusal r)
            {
                Debug.LogError(r.Message);
            }
        }

        /// <summary>Plan both regions' patches against their scene files, write them to <see cref="PatchFolder"/>,
        /// and apply them only when asked, with both scenes closed. Nothing is written until every patch of
        /// both regions is planned and sealed.</summary>
        public static List<LayerPatch> Run(bool apply)
        {
            if (apply)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    foreach (Region r in Regions)
                        if (string.Equals(SceneManager.GetSceneAt(i).path, r.ScenePath, StringComparison.Ordinal))
                            throw new Refusal($"{r.Name} is open in the editor. Close it first: an open scene would overwrite the file on its next save.");

            var assets = new EditorStoreWiringAssets();
            var texts = new List<string>();
            var plans = new List<List<LayerPatch>>();
            foreach (Region r in Regions)
            {
                string text = File.ReadAllText(r.ScenePath);
                texts.Add(text);
                plans.Add(Plan(text, r, assets));
            }

            Directory.CreateDirectory(PatchFolder);
            var utf8 = new UTF8Encoding(false);
            var all = new List<LayerPatch>();
            for (int i = 0; i < Regions.Count; i++)
            {
                foreach (LayerPatch p in plans[i])
                    File.WriteAllText(Path.Combine(PatchFolder, p.Step + ".patch.yaml"), p.ToYaml(), utf8);
                string result = ApplyAll(texts[i], plans[i]);
                if (apply && result != texts[i]) File.WriteAllText(Regions[i].ScenePath, result, utf8);
                all.AddRange(plans[i]);
            }
            string summary = string.Join("\n", all.Select(p => p.Summary()));
            if (apply) Debug.Log($"[StoreWiringPatch] applied to {StPeters.ScenePath} and {NineMileCreek.ScenePath} (patches in {PatchFolder}):\n{summary}");
            else Debug.Log($"[StoreWiringPatch] dry run, nothing written to the scenes (patches in {PatchFolder}):\n{summary}");
            return all;
        }
    }
}
#endif
