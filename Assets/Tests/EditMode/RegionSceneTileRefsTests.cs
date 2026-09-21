using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>EVERY TILE A COMMITTED REGION SCENE PAINTS WITH MUST BE IN THE REPO.</b> On 2026-08-21 PR #633
    /// committed <c>NineMileCreek.unity</c> with its roads painted: two tilemaps, <c>RoadTop</c> and
    /// <c>RoadSkirt</c>, 6,641 cells each, naming <b>120</b> tile assets by guid under
    /// <c>Assets/_Project/Art/Tilesets/Tiles/RoadsV3/</c>. That folder was never committed.
    /// <c>RoadTileLibrary</c> generates it ("generated rather than committed", by design), and
    /// <c>AssetDatabase.CreateAsset</c> gives every new tile a RANDOM guid, so the guids the scene named
    /// existed on one machine only. Every clone and every CI run opened Nine Mile Creek with no road
    /// tiles, and nothing in the repo noticed for four weeks.
    ///
    /// <para>So this reads every scene under <see cref="ScenesRoot"/> as TEXT, collects the guids each
    /// <c>Tilemap</c> names in <c>m_TileAssetArray</c>, <c>m_TileSpriteArray</c> and
    /// <c>m_TileObjectToInstantiateArray</c>, and requires every one to resolve: some <c>.meta</c> under
    /// <see cref="MetaRoots"/> carries the guid, AND the asset beside that meta exists. It loads no scene
    /// and no asset and needs no graphics device, so it is cheap enough that nobody has a reason to turn
    /// it off.</para>
    ///
    /// <para><b>⚠ Re-running a generator does not repair a red here.</b> It mints NEW guids while the
    /// scene still names the old ones, and saving the scene then writes placeholder tiles over its
    /// references. The fix is the ORIGINAL files with their ORIGINAL metas: the guids in those metas ARE
    /// the references.</para>
    ///
    /// <para><b>⚠ CI is the arbiter, not a working copy.</b> Only on a clean checkout does "on disk" mean
    /// "committed". A checkout holding the files UNTRACKED passes here while every clone is broken, which
    /// is exactly how this hole stayed invisible.</para>
    /// </summary>
    public class RegionSceneTileRefsTests
    {
        // =====================================================================================
        // the bar, and what it reads
        // =====================================================================================

        /// <summary>Where the region scenes live, one per region (ADR 0004). Read recursively.</summary>
        public const string ScenesRoot = "Assets/_Project/Scenes";

        /// <summary>
        /// The roots whose <c>.meta</c> files are indexed: the project's assets and any embedded package.
        /// Registry packages under <c>Library/PackageCache</c> are deliberately not read: <c>Library/</c> is
        /// never committed, and no region tilemap paints with a package's tile. If one ever does, this guard
        /// names its guid; widen the roots then, knowingly.
        /// </summary>
        public static readonly string[] MetaRoots = { "Assets", "Packages" };

        /// <summary>
        /// How many tilemap references may fail to resolve. <b>Zero.</b> A stated constant, never read back
        /// from the code under test. Main carried <b>120</b> from #633 until the RoadsV3 tiles were
        /// committed alongside this guard. Do not raise it to make this green.
        /// </summary>
        public const int MaxUnresolved = 0;

        /// <summary>The Tilemap's class id in Force-Text YAML: <c>--- !u!1839735485 &amp;fileID</c>.</summary>
        const string TilemapClass = "1839735485";

        /// <summary>The GameObject's class id: where a tilemap's name lives.</summary>
        const string GameObjectClass = "1";

        /// <summary>The Tilemap fields that name an asset by guid.</summary>
        static readonly string[] GuidArrays =
            { "m_TileAssetArray", "m_TileSpriteArray", "m_TileObjectToInstantiateArray" };

        /// <summary>Unity's built-in resources (<c>0000000000000000e000…</c>, <c>0000000000000000f000…</c>)
        /// have no <c>.meta</c> anywhere, so a guid with this prefix is not a repo asset and is skipped.</summary>
        const string BuiltInPrefix = "0000000000000000";

        static readonly Regex GuidRef = new Regex(@"guid: ([0-9a-fA-F]{32})");
        static readonly Regex FileIdRef = new Regex(@"fileID: (-?\d+)");

        // =====================================================================================
        // the reading (pure: the fixtures below prove it)
        // =====================================================================================

        /// <summary>One guid that one tilemap names, and where.</summary>
        public struct TileRef
        {
            public string Scene;
            public string Tilemap;
            public string Array;
            public string Guid;

            public override string ToString() => $"{Scene} · {Tilemap} · {Array} · {Guid}";
        }

        enum Doc { Other, GameObject, Tilemap }

        /// <summary>
        /// Every distinct guid the Tilemaps of one scene name, read from the scene's lines. A document
        /// starts at a <c>--- !u!&lt;class&gt; &amp;&lt;fileID&gt;</c> line; a Tilemap's own fields sit at
        /// indent two and their array items below them, so the reader only has to know which field it is
        /// in. A tilemap is labelled with its GameObject's <c>m_Name</c> and its own fileID, and that
        /// GameObject may come before or after it in the file. A freed slot (<c>{fileID: 0}</c>) carries
        /// no guid; built-ins are skipped.
        /// </summary>
        public static List<TileRef> TilemapRefs(string scene, IEnumerable<string> lines)
        {
            var refs = new List<TileRef>();
            var seen = new HashSet<string>();
            var names = new Dictionary<string, string>();   // GameObject fileID -> m_Name
            var owners = new Dictionary<string, string>();  // Tilemap fileID -> its GameObject's fileID
            var doc = Doc.Other;
            string id = null, field = null;

            foreach (string raw in lines)
            {
                string line = raw.Length > 0 && raw[raw.Length - 1] == '\r' ? raw.Substring(0, raw.Length - 1) : raw;

                if (line.StartsWith("--- !u!", System.StringComparison.Ordinal))
                {
                    int space = line.IndexOf(' ', 7);
                    string cls = space < 0 ? line.Substring(7) : line.Substring(7, space - 7);
                    int amp = line.IndexOf('&');
                    int end = amp < 0 ? -1 : line.IndexOf(' ', amp);
                    id = amp < 0 ? "?" : end < 0 ? line.Substring(amp + 1) : line.Substring(amp + 1, end - amp - 1);
                    doc = cls == TilemapClass ? Doc.Tilemap : cls == GameObjectClass ? Doc.GameObject : Doc.Other;
                    field = null;
                    continue;
                }

                if (doc == Doc.GameObject)
                {
                    if (line.StartsWith("  m_Name: ", System.StringComparison.Ordinal))
                        names[id] = line.Substring(10).Trim();
                    continue;
                }
                if (doc != Doc.Tilemap) continue;

                // "  m_Field: …" at indent two is one of the Tilemap's own fields; an array item is
                // "  - …" and its body sits deeper.
                if (line.Length > 2 && line[0] == ' ' && line[1] == ' ' && line[2] != ' ' && line[2] != '-')
                {
                    int colon = line.IndexOf(':');
                    field = colon < 0 ? null : line.Substring(2, colon - 2);
                    if (field == "m_GameObject")
                    {
                        var owner = FileIdRef.Match(line);
                        if (owner.Success) owners[id] = owner.Groups[1].Value;
                    }
                    continue;
                }
                if (field == null || System.Array.IndexOf(GuidArrays, field) < 0) continue;

                var g = GuidRef.Match(line);
                if (!g.Success) continue;
                string guid = g.Groups[1].Value.ToLowerInvariant();
                if (guid.StartsWith(BuiltInPrefix, System.StringComparison.Ordinal)) continue;
                if (seen.Add(id + "|" + field + "|" + guid))
                    refs.Add(new TileRef { Scene = scene, Tilemap = id, Array = field, Guid = guid });
            }

            for (int i = 0; i < refs.Count; i++)
            {
                var r = refs[i];
                string name = owners.TryGetValue(r.Tilemap, out string go) && names.TryGetValue(go, out string n)
                    ? n : "(unnamed)";
                r.Tilemap = $"{name} &{r.Tilemap}";
                refs[i] = r;
            }
            return refs;
        }

        /// <summary>
        /// guid → asset path for every <c>.meta</c> under those of <paramref name="roots"/> that exist.
        /// Only a meta's head is read: the guid is its second line, and a sliced sheet's meta runs to
        /// hundreds of KB.
        /// </summary>
        public static Dictionary<string, string> IndexMetas(IEnumerable<string> roots)
        {
            var index = new Dictionary<string, string>();
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string meta in Directory.GetFiles(root, "*.meta", SearchOption.AllDirectories))
                {
                    if (!meta.EndsWith(".meta", System.StringComparison.Ordinal)) continue;
                    string guid = GuidOfMeta(meta);
                    if (guid != null && !index.ContainsKey(guid))
                        index[guid] = meta.Substring(0, meta.Length - ".meta".Length).Replace('\\', '/');
                }
            }
            return index;
        }

        static string GuidOfMeta(string path)
        {
            using (var reader = new StreamReader(path))
            {
                for (int i = 0; i < 8; i++)
                {
                    string line = reader.ReadLine();
                    if (line == null) break;
                    if (line.StartsWith("guid: ", System.StringComparison.Ordinal))
                        return line.Substring(6).Trim().ToLowerInvariant();
                }
            }
            return null;
        }

        /// <summary>
        /// The references that do not resolve: no meta carries the guid, or one does and the asset beside
        /// it is gone (a meta alone is not a tile). <paramref name="assetExists"/> is the disk in the guard
        /// and a plain predicate in the fixtures.
        /// </summary>
        public static List<TileRef> Unresolved(IEnumerable<TileRef> refs, IDictionary<string, string> index,
                                               System.Func<string, bool> assetExists)
        {
            var missing = new List<TileRef>();
            foreach (var r in refs)
                if (!index.TryGetValue(r.Guid, out string asset) || !assetExists(asset)) missing.Add(r);
            return missing;
        }

        static bool OnDisk(string path) => File.Exists(path) || Directory.Exists(path);

        /// <summary>The failure message: a tally by tilemap, then the references themselves.</summary>
        static string Explain(List<TileRef> unresolved)
        {
            var byTilemap = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
            foreach (var r in unresolved)
            {
                string key = $"{r.Scene} · {r.Tilemap} · {r.Array}";
                byTilemap.TryGetValue(key, out int count);
                byTilemap[key] = count + 1;
            }

            const int Shown = 40;
            var sb = new StringBuilder();
            sb.AppendLine($"{unresolved.Count} tile reference(s) in the committed region scenes name a guid " +
                          $"that no asset in the repo carries (the bar is {MaxUnresolved}). By tilemap:");
            foreach (var kv in byTilemap) sb.AppendLine($"  {kv.Key}: {kv.Value}");
            sb.AppendLine("As scene · tilemap &fileID · array · guid:");
            for (int i = 0; i < unresolved.Count && i < Shown; i++) sb.AppendLine("  " + unresolved[i]);
            if (unresolved.Count > Shown) sb.AppendLine($"  … and {unresolved.Count - Shown} more.");
            sb.Append("Commit the ORIGINAL assets with their ORIGINAL .meta files: the guids in those metas ARE " +
                      "these references. Re-running a tile generator mints new guids and repairs nothing, and " +
                      "saving the scene now writes placeholder tiles over them. Do not raise MaxUnresolved.");
            return sb.ToString();
        }

        // =====================================================================================
        // the guard
        // =====================================================================================

        [Test]
        public void EveryTileARegionScenePaintsWith_ResolvesToAnAssetInTheRepo()
        {
            Assert.IsTrue(Directory.Exists(ScenesRoot),
                $"'{ScenesRoot}' does not exist, so this guard would be guarding nothing. Treat that as a " +
                "failure, not a pass: if the region scenes moved, move ScenesRoot with them.");

            var scenes = new List<string>(Directory.GetFiles(ScenesRoot, "*.unity", SearchOption.AllDirectories));
            scenes.Sort(System.StringComparer.Ordinal);
            Assert.Greater(scenes.Count, 0,
                $"No .unity file under '{ScenesRoot}': this guard would be guarding nothing.");

            var index = IndexMetas(MetaRoots);
            var refs = new List<TileRef>();
            var perScene = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
            foreach (string path in scenes)
            {
                string scene = path.Replace('\\', '/');
                var found = TilemapRefs(scene, File.ReadLines(path));
                perScene[scene] = found.Count;
                refs.AddRange(found);
            }

            Assert.Greater(refs.Count, 0,
                $"{scenes.Count} scene(s) read and not one tile reference found in any Tilemap. Either the " +
                "scenes lost their tilemaps or this reader stopped understanding the YAML; either way the " +
                "guard would pass by not looking.");

            var unresolved = Unresolved(refs, index, OnDisk);
            Assert.LessOrEqual(unresolved.Count, MaxUnresolved, Explain(unresolved));

            var tilemaps = new HashSet<string>();
            foreach (var r in refs) tilemaps.Add(r.Scene + "|" + r.Tilemap);
            var report = new StringBuilder();
            report.AppendLine($"[RegionSceneTileRefs] {refs.Count} tile reference(s) from {tilemaps.Count} " +
                              $"tilemap(s) in {scenes.Count} scene(s), against {index.Count} metas: " +
                              $"{unresolved.Count} unresolved (the bar is {MaxUnresolved}).");
            foreach (var kv in perScene) report.AppendLine($"  {kv.Key}: {kv.Value}");
            TestContext.WriteLine(report.ToString().TrimEnd());
        }

        // =====================================================================================
        // the fixtures: proving the tripwire is connected
        // =====================================================================================

        const string TileGuid = "a1b2c3d4e5f60718293a4b5c6d7e8f90";
        const string SheetGuid = "0f1e2d3c4b5a69788796a5b4c3d2e1f0";
        const string RendererSpriteGuid = "11112222333344445555666677778888";

        /// <summary>
        /// The shape #633 committed, in miniature: a road tilemap naming one tile and one sheet sprite. The
        /// Tilemap comes BEFORE the GameObject that names it, it carries a freed slot and a built-in sprite,
        /// and a SpriteRenderer after it names a guid that is not a tile reference.
        /// </summary>
        static readonly string[] Fixture =
        {
            "%YAML 1.1",
            "%TAG !u! tag:unity3d.com,2011:",
            "--- !u!1839735485 &200",
            "Tilemap:",
            "  m_ObjectHideFlags: 0",
            "  m_GameObject: {fileID: 100}",
            "  m_Enabled: 1",
            "  m_Tiles:",
            "  - first: {x: 0, y: 0, z: 0}",
            "    second:",
            "      serializedVersion: 2",
            "      m_TileIndex: 0",
            "  m_TileAssetArray:",
            "  - m_RefCount: 1",
            $"    m_Data: {{fileID: 11400000, guid: {TileGuid}, type: 2}}",
            "  - m_RefCount: 0",
            "    m_Data: {fileID: 0}",
            "  m_TileSpriteArray:",
            "  - m_RefCount: 1",
            $"    m_Data: {{fileID: 21300000, guid: {SheetGuid}, type: 3}}",
            "  - m_RefCount: 1",
            "    m_Data: {fileID: 10911, guid: 0000000000000000f000000000000000, type: 0}",
            "  m_TileMatrixArray:",
            "  - m_RefCount: 1",
            "    m_Data:",
            "      e00: 1",
            "  m_TileObjectToInstantiateArray: []",
            "--- !u!1 &100",
            "GameObject:",
            "  m_Name: RoadTop",
            "--- !u!212 &300",
            "SpriteRenderer:",
            $"  m_Sprite: {{fileID: 21300000, guid: {RendererSpriteGuid}, type: 3}}",
        };

        [Test]
        public void TheReader_TakesOnlyTilemapArrays_AndSkipsFreedSlotsAndBuiltIns()
        {
            var refs = TilemapRefs("(fixture).unity", Fixture);

            Assert.AreEqual(2, refs.Count, "expected exactly the tile and the sheet: " + string.Join("; ", refs));
            Assert.AreEqual("m_TileAssetArray", refs[0].Array);
            Assert.AreEqual(TileGuid, refs[0].Guid);
            Assert.AreEqual("m_TileSpriteArray", refs[1].Array);
            Assert.AreEqual(SheetGuid, refs[1].Guid);
            Assert.AreEqual("RoadTop &200", refs[0].Tilemap,
                "a tilemap takes its name from its GameObject, wherever that sits in the file");
        }

        [Test]
        public void TheGuard_NamesATileMissingFromTheRepo_ByScene_Tilemap_ArrayAndGuid()
        {
            // The sheet is committed and the tile is not: the shape #633 committed.
            var refs = TilemapRefs("(fixture).unity", Fixture);
            var index = new Dictionary<string, string> { [SheetGuid] = "Sheet.png" };

            var unresolved = Unresolved(refs, index, path => true);

            Assert.AreEqual(1, unresolved.Count,
                "THE TRIPWIRE IS NOT CONNECTED: a tilemap naming a tile that no meta carries was not " +
                "reported exactly once.\n" + Explain(unresolved));
            Assert.AreEqual("(fixture).unity · RoadTop &200 · m_TileAssetArray · " + TileGuid,
                            unresolved[0].ToString());
        }

        [Test]
        public void TheGuard_NeedsTheAssetBesideItsMeta_AndPassesWhenBothAreThere()
        {
            var refs = TilemapRefs("(fixture).unity", Fixture);
            var index = new Dictionary<string, string>
            {
                [TileGuid] = "Road_gravel_0_top.asset",
                [SheetGuid] = "Sheet.png",
            };

            Assert.AreEqual(1, Unresolved(refs, index, path => path != "Road_gravel_0_top.asset").Count,
                "a meta whose asset is gone was taken for the asset; a meta alone is not a tile");
            Assert.AreEqual(0, Unresolved(refs, index, path => true).Count,
                "the guard failed a tilemap whose tile and sheet are both in the repo; a guard that cries " +
                "wolf is a guard that gets deleted");
        }
    }
}
