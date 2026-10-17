using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Economy;
using HiddenHarbours.World;
using LayerPatch = HiddenHarbours.App.Editor.StPetersLayerRefresh.LayerPatch;
using Op = HiddenHarbours.App.Editor.StPetersLayerRefresh.Op;
using OpKind = HiddenHarbours.App.Editor.StPetersLayerRefresh.OpKind;
using Refusal = HiddenHarbours.App.Editor.StPetersLayerRefresh.Refusal;
using Region = HiddenHarbours.App.Editor.StoreWiringPatch.Region;
using Seller = HiddenHarbours.App.Editor.StoreWiringPatch.Seller;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE STORE STANDS IN THE COMMITTED SCENES</b> (O4). Subject: <see cref="StoreWiringPatch"/> and the
    /// two region scenes it writes, StPeters.unity and NineMileCreek.unity.
    ///
    /// <para>The committed scenes ARE the step's output: each, with the store taken back out by the test's
    /// own hand (every seller id line and every wares book), planned and written by the step, gives the
    /// committed text back to the byte, and the step planned on it again has nothing to do. An id changed in
    /// the wiring is refused where the old id stands, and touches nothing outside the store's roots. Read
    /// with the test's own reader and the project's data, the committed scenes sell every listed ware once
    /// under each seller the listing names; every trade row of every dialogue names a seller a committed
    /// vendor sells as, and a sell row's counter has its sell point; and each region's DialogueUI carries
    /// its one wares book. The committed scenes are never written: they are hashed before each test and
    /// after.</para>
    /// </summary>
    public class StoreWiringTests
    {
        const string StPetersPath = StPetersLayerRefreshTests.ScenePath;
        const string NineMileCreekPath = "Assets/_Project/Scenes/NineMileCreek.unity";
        const string DataRoot = "Assets/_Project/Data";
        const string SellerIdKey = "_sellerId";
        const string Fix = "Apply the store patches (Hidden Harbours > World > Store Wiring > Apply the Store Patches) " +
                           "on the day's main and commit the scene they write.";

        static readonly string[] Scenes = { StPetersPath, NineMileCreekPath };
        static readonly string CopyFolder = Path.Combine(Path.GetTempPath(), "hh-store-wiring-tests");
        static readonly Regex HeaderRx = new Regex(@"^--- !u!(\d+) &(-?\d+)( stripped)?$", RegexOptions.CultureInvariant);
        static readonly Regex GuidRx = new Regex(@"guid: ([0-9a-f]{32})", RegexOptions.CultureInvariant);
        static readonly string BookClass = ClassIdOf(typeof(CatalogBookPresenter));

        readonly Dictionary<string, string> _hashes = new Dictionary<string, string>();

        static StoreWiringPatch.IStoreWiringAssets Assets => new StoreWiringPatch.EditorStoreWiringAssets();

        [SetUp]
        public void SetUp()
        {
            _hashes.Clear();
            foreach (string path in Scenes) _hashes[path] = LayerRefreshPatchChecks.Sha256(path);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(CopyFolder)) Directory.Delete(CopyFolder, true);
            foreach (string path in Scenes)
                Assert.AreEqual(_hashes[path], LayerRefreshPatchChecks.Sha256(path), $"{path} changed during the test; the committed scenes are never written");
        }

        // ---- 1, 2: the committed scenes are the step's output ---------------------------------------------

        [Test]
        public void StPeters_TheStorePatch_IsTheCommittedScene() =>
            StorePatchIsTheCommittedScene(StoreWiringPatch.StPeters, StPetersPath);

        [Test]
        public void NineMileCreek_TheStorePatch_IsTheCommittedScene() =>
            StorePatchIsTheCommittedScene(StoreWiringPatch.NineMileCreek, NineMileCreekPath);

        static void StorePatchIsTheCommittedScene(Region region, string path)
        {
            string name = Path.GetFileName(path);
            string committed = File.ReadAllText(path);
            string before = TakeBack(committed, out List<long> sellers, out List<long> books);
            Assert.AreEqual(region.Sellers.Count, sellers.Count,
                            $"the committed {name} holds {sellers.Count} vendors with a seller id, and the store wiring writes {region.Sellers.Count}. {Fix}");
            Assert.AreEqual(1, books.Count, $"the committed {name} holds {books.Count} wares books, and its DialogueUI carries one. {Fix}");

            List<LayerPatch> patches = StoreWiringPatch.Plan(before, region, Assets);
            string once = StoreWiringPatch.ApplyAll(before, patches);
            Assert.IsTrue(once == committed,
                          $"the store patch, written over the committed {name} with the store taken back out, is not the committed scene: " +
                          $"{FirstDifference(committed, once)}. {Fix}");

            var t = new LayerRefreshSceneText(committed);
            List<Op> ops = patches.SelectMany(p => p.Ops).ToList();
            Assert.IsEmpty(ops.Where(o => o.Kind == OpKind.Delete).Select(o => o.Name).ToList(), "the store patch deletes");
            long ui = t.Ref(books[0], "m_GameObject");
            CollectionAssert.AreEquivalent(sellers.Concat(new[] { ui }).ToList(), ops.Where(o => o.Kind == OpKind.Edit).Select(o => o.FileId).ToList(),
                                           "the documents the store patch edits are not the vendors with a seller id and the book's GameObject");
            foreach (Op op in ops.Where(o => o.Kind == OpKind.Edit))
                StringAssert.Contains(t.ObjectName(op.FileId), op.Name, $"the edit of &{op.FileId} does not name its object");
            CollectionAssert.AreEqual(books, ops.Where(o => o.Kind == OpKind.Add).Select(o => o.FileId).ToList(), "the store patch adds a document that is not the book");
            StringAssert.Contains(t.ObjectName(ui), ops.Single(o => o.Kind == OpKind.Add).Name, "the book's addition does not name its object");
            Assert.AreEqual(region.Sellers.Select(s => s.Root).Distinct().Count() + 1, patches.Count, "one patch per root the store touches");

            Assert.IsTrue(StoreWiringPatch.ApplyAll(once, patches) == once, "written a second time, the store patch changed the scene again");
            List<LayerPatch> again = StoreWiringPatch.Plan(once, region, Assets);
            Assert.IsTrue(again.All(p => p.IsEmpty), "planned again on its own output, the step still has work to do:\n" +
                                                     string.Join("\n", again.Where(p => !p.IsEmpty).Select(p => p.Summary())));
        }

        // ---- 3: an id changed on purpose ------------------------------------------------------------------

        [Test]
        public void ChangedOnPurpose_OneIdChanged_RefusesTheVendorCarryingTheOldId_AndTouchesNothingOutsideItsRoots()
        {
            Directory.CreateDirectory(CopyFolder);
            List<Listing> catalog = Listings();
            foreach ((Region region, string path) in new[] { (StoreWiringPatch.StPeters, StPetersPath), (StoreWiringPatch.NineMileCreek, NineMileCreekPath) })
            {
                string bare = TakeBack(File.ReadAllText(path), out _, out _);

                // The row changed on purpose: one that shares its root and class with another vendor, so the two
                // differ by their wares alone; the first row where none does.
                IReadOnlyList<Seller> rows = region.Sellers;
                int row = Math.Max(0, Enumerable.Range(0, rows.Count)
                                                .FirstOrDefault(i => rows.Count(o => o.Root == rows[i].Root && o.Kind == rows[i].Kind) > 1));
                Seller old = rows[row];
                string changedId = old.SellerId + "_changed_on_purpose";
                var changed = new Region(region.Name, region.ScenePath,
                                         rows.Select((s, i) => i == row ? new Seller(s.Root, s.Kind, s.Wares, changedId) : s).ToList());

                // Over the scene as the wiring writes it, the changed wiring is refused, naming the vendor and both ids.
                string wired = StoreWiringPatch.ApplyAll(bare, StoreWiringPatch.Plan(bare, region, Assets));
                var refusal = Assert.Throws<Refusal>(() => StoreWiringPatch.Plan(wired, changed, Assets),
                                                     $"{region.Name}: the step wrote over the id {old} sells as");
                foreach (string part in new[] { old.Root, old.Kind.Name, old.Wares, old.SellerId, changedId })
                    StringAssert.Contains(part, refusal.Message, $"{region.Name}: the refusal does not name {part}");

                // Over the scene with the store taken out, it is written into a copy as the menu writes it, then again.
                string copy = Path.Combine(CopyFolder, Path.GetFileName(path));
                Write(copy, bare);
                List<LayerPatch> patches = StoreWiringPatch.Plan(File.ReadAllText(copy), changed, Assets);
                Assert.IsEmpty(patches.SelectMany(p => p.Deletions).Select(o => o.Name).ToList(), $"{region.Name}: the store patch deletes");
                Write(copy, StoreWiringPatch.ApplyAll(bare, patches));
                string once = File.ReadAllText(copy);
                Write(copy, StoreWiringPatch.ApplyAll(once, patches));
                Assert.IsTrue(File.ReadAllText(copy) == once, $"{region.Name}: written a second time, the store patch changed the copy again");
                Assert.IsTrue(StoreWiringPatch.Plan(once, changed, Assets).All(p => p.IsEmpty),
                              $"{region.Name}: planned again on the patched copy, the step still has work to do");

                OnlyItsRoots(bare, once, patches.Select(p => p.Root).ToList(), region.Name);
                var t = new LayerRefreshSceneText(once);
                List<long> carriers = t.Order.Where(id => t.ClassOf(id) == 114 && t.Field(id, SellerIdKey) == changedId).ToList();
                Assert.AreEqual(1, carriers.Count, $"{region.Name}: vendors selling as the changed id");
                Assert.AreEqual(t.RootGameObject(old.Root), t.Ref(carriers[0], "m_GameObject"), $"{region.Name}: the changed id is not on {old.Root}");
                Assert.AreEqual(ClassIdOf(old.Kind), t.Field(carriers[0], "m_EditorClassIdentifier"), $"{region.Name}: the changed id is not on a {old.Kind.Name}");
                StringAssert.Contains(catalog.Single(l => l.Id == old.Wares).Guid, t.Text(carriers[0]),
                                      $"{region.Name}: the changed id is not on the {old.Kind.Name} stocking {old.Wares}");
                Assert.AreEqual(rows.Count, t.Order.Count(id => t.ClassOf(id) == 114 && !string.IsNullOrEmpty(t.Field(id, SellerIdKey))),
                                $"{region.Name}: vendors with a seller id after the changed wiring is written");
            }
        }

        // ---- 4: every listed ware is sold ------------------------------------------------------------------

        [Test]
        public void CommittedScenes_SellEveryListedWare_AndNoVendorSellsAsAnUnlistedSeller()
        {
            List<Listing> listings = Listings();
            List<Vendor> vendors = Vendors();
            var pairs = listings.Where(l => l.Listed).SelectMany(l => l.Sellers.Select(s => (Listing: l, Seller: s))).ToList();
            Assert.That(pairs, Is.Not.Empty, "the catalog lists nothing for any seller: the guard would pass on nothing");

            var problems = new List<string>();
            foreach ((Listing l, string seller) in pairs)
            {
                List<Vendor> hits = vendors.Where(v => v.SellerId == seller && v.Guids.Contains(l.Guid)).ToList();
                if (hits.Count != 1)
                    problems.Add($"{l} is listed for {seller}, and {hits.Count} committed vendors stock it selling as {seller}" +
                                 (hits.Count > 0 ? ": " + string.Join(", ", hits) : ""));
            }
            foreach (Vendor v in vendors)
            {
                List<Listing> stocked = listings.Where(l => v.Guids.Contains(l.Guid)).ToList();
                if (stocked.Count != 1) problems.Add($"{v} sells as {v.SellerId} and stocks {stocked.Count} catalog listings");
                else if (!stocked[0].Listed || !stocked[0].Sellers.Contains(v.SellerId))
                    problems.Add($"{v} sells {stocked[0]} as {v.SellerId}, and no listing lists that ware for {v.SellerId}");
            }
            Assert.IsEmpty(problems, $"{pairs.Count} listed (ware, seller) pairs, {vendors.Count} committed vendors with a seller id. {Fix}");
            Assert.AreEqual(pairs.Count, vendors.Count, "listed (ware, seller) pairs and committed vendors with a seller id");
        }

        // ---- 5: every trade row finds its seller -----------------------------------------------------------

        [Test]
        public void EveryTradeRow_NamesASellerACommittedVendorSellsAs_AndASellRowsCounterHasItsSellPoint()
        {
            List<TradeRow> rows = TradeRows();
            List<Vendor> vendors = Vendors();
            Assert.That(rows, Is.Not.Empty, "no dialogue row opens a wares book or sells at a counter: the guard would pass on nothing");

            var problems = new List<string>();
            foreach (TradeRow row in rows)
            {
                List<Vendor> carrying = vendors.Where(v => v.SellerId == row.Seller).ToList();
                if (carrying.Count == 0)
                {
                    problems.Add($"{row}: no committed vendor sells as {row.Seller}");
                    continue;
                }
                if (row.Sell)
                    foreach (Vendor v in carrying.Where(v => !v.SellPointBeside))
                        problems.Add($"{row}: {v} has no {nameof(WharfSellPoint)} on its own GameObject, so the counter sale finds no desk");
            }
            Assert.IsEmpty(problems, $"{rows.Count} trade rows. {Fix}");
        }

        // ---- 6: one wares book per region, beside the dialogue -----------------------------------------------

        [Test]
        public void EachRegionsDialogueUI_CarriesOneWaresBook_AndNoOtherObjectDoes()
        {
            string presenter = ClassIdOf(typeof(DialoguePresenter));
            string script = BookScriptGuid();
            var problems = new List<string>();
            foreach (string path in Scenes)
            {
                string name = Path.GetFileName(path);
                var t = new LayerRefreshSceneText(File.ReadAllText(path));
                List<long> presenters = t.Order.Where(id => t.ClassOf(id) == 114 && t.Field(id, "m_EditorClassIdentifier") == presenter).ToList();
                Assert.AreEqual(1, presenters.Count, $"{name}'s {nameof(DialoguePresenter)}s");
                long ui = t.Ref(presenters[0], "m_GameObject");

                List<long> books = t.Order.Where(id => t.ClassOf(id) == 114 &&
                                                       (t.Field(id, "m_EditorClassIdentifier") == BookClass || t.Guid(id, "m_Script") == script))
                                          .ToList();
                if (books.Count != 1) problems.Add($"{name} holds {books.Count} wares books, and its DialogueUI carries exactly one");
                foreach (long b in books)
                {
                    long go = t.Ref(b, "m_GameObject");
                    if (go != ui) problems.Add($"{name}: the wares book &{b} is on {t.ObjectName(b)}, not on {t.ObjectName(ui)} beside the {nameof(DialoguePresenter)}");
                    if (!t.Has(go) || !t.Refs(go, "m_Component").Contains(b)) problems.Add($"{name}: the wares book &{b} is not among its GameObject's components");
                    if (t.Field(b, "m_Enabled") != "1") problems.Add($"{name}: the wares book &{b} is disabled, and it hears a book opened only while enabled");
                    if (t.Guid(b, "m_Script") != script) problems.Add($"{name}: the wares book &{b} runs the script {t.Guid(b, "m_Script")}, not {nameof(CatalogBookPresenter)}'s {script}");
                    if (t.Field(b, "m_EditorClassIdentifier") != BookClass) problems.Add($"{name}: the wares book &{b} names the class '{t.Field(b, "m_EditorClassIdentifier")}'");
                }
            }
            Assert.IsEmpty(problems, Fix);
        }

        // ---- the project's data, by the asset database --------------------------------------------------------

        internal sealed class Listing
        {
            public string Guid, Id, Asset;
            public bool Listed;
            public string[] Sellers;
            public override string ToString() => $"{Id} ({Asset})";
        }

        internal sealed class TradeRow
        {
            public string Dialogue, Option, Seller;
            public bool Sell;
            public override string ToString() => $"{Dialogue}'s {Option} ({(Sell ? "sells at" : "opens the wares book of")} {Seller})";
        }

        /// <summary>Every listing in the catalog folder, of every listing type: its guid, id and catalog entry.</summary>
        static List<Listing> Listings()
        {
            var list = new List<Listing>();
            IEnumerable<Type> types = typeof(ICatalogListing).Assembly.GetTypes()
                .Where(t => typeof(ICatalogListing).IsAssignableFrom(t) && typeof(ScriptableObject).IsAssignableFrom(t) && !t.IsAbstract)
                .OrderBy(t => t.FullName, StringComparer.Ordinal);
            foreach (Type type in types)
                foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name, new[] { CatalogSource.CatalogFolderPath }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (list.Any(l => l.Guid == guid) || !(AssetDatabase.LoadAssetAtPath(path, type) is ICatalogListing listing)) continue;
                    list.Add(new Listing
                    {
                        Guid = guid, Id = listing.ListingId, Asset = path, Listed = listing.Catalog.Listed,
                        Sellers = listing.Catalog.Sellers ?? new string[0],
                    });
                }
            return list;
        }

        /// <summary>Every option of every dialogue under the data root that opens a wares book or sells at a counter.</summary>
        static List<TradeRow> TradeRows()
        {
            var rows = new List<TradeRow>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(DialogueDef), new[] { DataRoot }))
            {
                var def = AssetDatabase.LoadAssetAtPath<DialogueDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || def.Options == null) continue;
                foreach (DialogueOption o in def.Options)
                {
                    if (!string.IsNullOrEmpty(o.CatalogSellerId)) rows.Add(new TradeRow { Dialogue = def.Id, Option = o.Id, Seller = o.CatalogSellerId });
                    if (!string.IsNullOrEmpty(o.SellAtSellerId)) rows.Add(new TradeRow { Dialogue = def.Id, Option = o.Id, Seller = o.SellAtSellerId, Sell = true });
                }
            }
            return rows;
        }

        /// <summary>The guid of the MonoScript that holds <see cref="CatalogBookPresenter"/>.</summary>
        static string BookScriptGuid()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript " + nameof(CatalogBookPresenter)))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == typeof(CatalogBookPresenter)) return guid;
            }
            Assert.Fail($"no MonoScript holds {nameof(CatalogBookPresenter)}");
            return null;
        }

        // ---- the committed scenes, by the test's own reader -------------------------------------------------

        internal sealed class Vendor
        {
            public string Scene, Name, Class, SellerId;
            public long Doc;
            public List<string> Guids;
            public bool SellPointBeside;
            public override string ToString() => $"{Scene}'s {Name} {Class.Substring(Class.LastIndexOf('.') + 1)} (&{Doc})";
        }

        /// <summary>Every MonoBehaviour in the two region scenes that sells as a seller id: the guids it holds,
        /// and whether a <see cref="WharfSellPoint"/> stands on its own GameObject.</summary>
        static List<Vendor> Vendors()
        {
            string sellPoint = ClassIdOf(typeof(WharfSellPoint));
            var list = new List<Vendor>();
            foreach (string path in Scenes)
            {
                var t = new LayerRefreshSceneText(File.ReadAllText(path));
                foreach (long id in t.Order)
                {
                    if (t.ClassOf(id) != 114 || t.IsStripped(id)) continue;
                    string seller = t.Field(id, SellerIdKey);
                    if (string.IsNullOrEmpty(seller)) continue;
                    long go = t.Ref(id, "m_GameObject");
                    list.Add(new Vendor
                    {
                        Scene = Path.GetFileNameWithoutExtension(path), Doc = id, Name = t.ObjectName(id),
                        Class = t.Field(id, "m_EditorClassIdentifier") ?? "", SellerId = seller,
                        Guids = GuidRx.Matches(t.Text(id)).Cast<Match>().Select(m => m.Groups[1].Value).ToList(),
                        SellPointBeside = t.Has(go) && t.Refs(go, "m_Component").Any(c => t.ClassOf(c) == 114 && t.Field(c, "m_EditorClassIdentifier") == sellPoint),
                    });
                }
            }
            return list;
        }

        /// <summary>
        /// The scene with the store taken back out, by the test's hand and not the step's: every
        /// <c>_sellerId</c> line, and every wares book with its entry in its GameObject's component list.
        /// </summary>
        static string TakeBack(string committed, out List<long> sellers, out List<long> books)
        {
            Assert.IsTrue(committed.EndsWith("\n", StringComparison.Ordinal), "the scene does not end in a newline");
            var t = new LayerRefreshSceneText(committed);
            books = t.Order.Where(id => t.ClassOf(id) == 114 && t.Field(id, "m_EditorClassIdentifier") == BookClass).ToList();
            sellers = t.Order.Where(id => t.ClassOf(id) == 114 && t.Field(id, SellerIdKey) != null).ToList();
            var bookSet = new HashSet<long>(books);
            var sellerSet = new HashSet<long>(sellers);
            var owners = new HashSet<long>(books.Select(b => t.Ref(b, "m_GameObject")));
            var entries = new HashSet<string>(books.Select(b => "  - component: {fileID: " + b.ToString(CultureInfo.InvariantCulture) + "}"));

            var sb = new StringBuilder(committed.Length);
            long doc = 0;
            foreach (string line in committed.Substring(0, committed.Length - 1).Split('\n'))
            {
                Match m = HeaderRx.Match(line);
                if (m.Success) doc = long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                if (bookSet.Contains(doc)) continue;
                if (sellerSet.Contains(doc) && line.StartsWith("  " + SellerIdKey + ":", StringComparison.Ordinal)) continue;
                if (owners.Contains(doc) && entries.Contains(line)) continue;
                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Every document outside the store's roots: still there, byte-identical, in its order;
        /// nothing new outside them. The roots are walked by the test's reader, before and after.</summary>
        static void OnlyItsRoots(string before, string after, IEnumerable<string> roots, string what)
        {
            Assert.AreEqual(before.Substring(0, before.IndexOf("--- !u!", StringComparison.Ordinal)),
                            after.Substring(0, after.IndexOf("--- !u!", StringComparison.Ordinal)), $"{what}: the file's preamble");
            Assert.IsTrue(after.EndsWith("\n", StringComparison.Ordinal), $"{what}: the file no longer ends in a newline");
            var a = new LayerRefreshSceneText(before);
            var b = new LayerRefreshSceneText(after);
            var mine = new HashSet<long>();
            foreach (string root in roots.Distinct())
            {
                long ra = a.RootGameObject(root), rb = b.RootGameObject(root);
                Assert.AreEqual(ra, rb, $"{what}: '{root}' is no longer the same GameObject");
                mine.UnionWith(a.Subtree(ra));
                mine.UnionWith(b.Subtree(rb));
            }
            foreach (long id in a.Order)
            {
                if (mine.Contains(id)) continue;
                if (!b.Has(id)) Assert.Fail($"{what}: {a.ObjectName(id)} (&{id}), outside the store's roots, left the scene");
                if (a.Text(id) != b.Text(id)) Assert.Fail($"{what}: {a.ObjectName(id)} (&{id}), outside the store's roots, changed");
            }
            foreach (long id in b.Order)
                if (!mine.Contains(id) && !a.Has(id))
                    Assert.Fail($"{what}: {b.ObjectName(id)} (&{id}) was added outside the store's roots");
            CollectionAssert.AreEqual(a.Order.Where(i => !mine.Contains(i)).ToList(), b.Order.Where(i => !mine.Contains(i)).ToList(),
                                      $"{what}: the documents outside the store's roots did not keep their order");
        }

        static string ClassIdOf(Type t) => t.Assembly.GetName().Name + "::" + t.FullName;

        /// <summary>As the menu writes a scene: UTF-8, no byte-order mark.</summary>
        static void Write(string path, string text) => File.WriteAllText(path, text, new UTF8Encoding(false));

        static string FirstDifference(string a, string b)
        {
            string[] x = a.Split('\n'), y = b.Split('\n');
            for (int i = 0; i < Math.Min(x.Length, y.Length); i++)
                if (x[i] != y[i]) return $"line {i + 1}: committed '{x[i]}', the step's '{y[i]}'";
            return x.Length == y.Length ? "no line differs" : $"the committed scene has {x.Length} lines, the step's {y.Length}";
        }
    }
}
