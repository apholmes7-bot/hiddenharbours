using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>H7's imported identities and owner coordinates, independent of the editor asset database.
    /// Scene acceptance remains explicitly deferred until the scene lane places these rows.</summary>
    public class NmcH7RowTests
    {
        internal const string Fixture = "Assets/Tests/EditMode/Fixtures/";
        internal const string ScenePath = "Assets/_Project/Scenes/NineMileCreek.unity";
        internal static readonly string[] Scenes = { "town_bridge", "the_falls", "main_street" };
        internal static string KeyScene(string name) => "Assets/_Project/Data/KeyScenes/KeyScene_nmc_" + name + ".asset";

        [TestCase("town_bridge", 53)]
        [TestCase("the_falls", 11)]
        [TestCase("main_street", 91)]
        public void OwnedRows_KeepEveryIdKindKitPieceAndOwnerCoordinate(string scene, int count)
        {
            var pins = Table("NmcH7Rows.tsv").Where(p => p[0] == scene).ToArray();
            var rows = Rows(KeyScene(scene));
            Assert.That(pins.Length, Is.EqualTo(count), "independent charter count");
            Assert.That(rows.Select(r => Field(r, "Id")), Is.EquivalentTo(pins.Select(p => p[1])));
            foreach (var pin in pins)
            {
                string row = rows.Single(r => Field(r, "Id") == pin[1]);
                Assert.That(Field(row, "Owner"), Is.EqualTo("H7"), pin[1]);
                Assert.That(Field(row, "Kind"), Is.EqualTo(pin[2]), pin[1]);
                Assert.That(Field(row, "Kit").Split(' ', '.', ',', '(')[0], Is.EqualTo(pin[3]), pin[1]);
                Assert.That(Field(row, "Piece"), Is.EqualTo(pin[4]), pin[1]);
                Assert.That(Vector(Field(row, "At")), Is.EqualTo(new[] { Number(pin[5]), Number(pin[6]) }), pin[1]);
                Assert.That(Number(Field(row, "Z")), Is.EqualTo(Number(pin[7])), pin[1]);
                Assert.That(Field(row, "Dir"), Is.EqualTo(pin[8]), pin[1]);
            }
        }

        [Test]
        public void AllOwnedIds_AreUniqueAcrossTheRegion_AndNoRetiredIdIsReused()
        {
            var scenes = Directory.GetFiles("Assets/_Project/Data/KeyScenes", "KeyScene_nmc_*.asset");
            var ids = scenes.SelectMany(p => Rows(p).Select(r => Field(r, "Id"))).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            foreach (string id in ids) Assert.That(id, Does.Match(@"^[a-z][a-z0-9_]*\.[a-z][a-z0-9_]*$"));
            var retired = scenes.SelectMany(p => List(File.ReadAllText(p), "RetiredIds")).ToHashSet();
            Assert.That(ids.Intersect(retired), Is.Empty);
            Assert.That(retired, Does.Contain("structure.nmc_apartments_2"));
            Assert.That(Rows(KeyScene("town_bridge")).Count(r => Field(r, "Kind") == "tree"), Is.Zero);
            var street = Rows(KeyScene("main_street")).Select(r => Field(r, "Id")).ToArray();
            for (int n = 1; n <= 3; n++) Assert.That(street, Does.Contain("prop.nmc_water_street_tree_" + n));
        }

        [TestCase("town_bridge", 0, 55)]
        [TestCase("the_falls", 10, 21)]
        [TestCase("main_street", 40, 131)]
        public void References_ResolveToTheirOwner_AndNeverBecomeOwnedRows(string scene, int count, int sourceCount)
        {
            var rows = Rows(KeyScene(scene));
            var refs = Array(DeckSidecarJson.Parse(Option(rows[0], "nmc.scene.refs")));
            var pins = Table("NmcH7Refs.tsv").Where(p => p[0] == scene).ToArray();
            Assert.That(pins.Length, Is.EqualTo(count));
            Assert.That(refs.Select(r => (String(r, "id"), String(r, "owner"))),
                Is.EquivalentTo(pins.Select(p => (p[1], "keyscene.nmc_" + p[2]))));
            Assert.That(Number(Option(rows[0], "nmc.scene.sourceRows")), Is.EqualTo(sourceCount));
            foreach (var pin in pins)
            {
                Assert.That(rows.Count(r => Field(r, "Id") == pin[1]), Is.Zero, pin[1] + " spawned by ref");
                Assert.That(Rows(KeyScene(pin[2])).Count(r => Field(r, "Id") == pin[1]), Is.EqualTo(1), pin[1]);
            }
        }

        [Test]
        public void Falls_KeepTheRampLevelsAndBothDeckHeights_InOptionsNotPivotZ()
        {
            var rows = Rows(KeyScene("the_falls")).ToDictionary(r => Field(r, "Id"));
            // The row uses the delivered cascade defaults; it does not override levels.
            var cascade = Json("docs/art/rigs/nmc-set-pieces-kit/gameplay/cascade.gameplay.json");
            object levels = rows["structure.nmc_the_falls_water"].Contains("- Key: levels")
                ? DeckSidecarJson.Parse(Option(rows["structure.nmc_the_falls_water"], "levels"))
                : Member(Member(cascade, "options"), "levels");
            Assert.That(Array(levels),
                Is.EqualTo(new double[] { 2.90, 2.15, 1.35, 0.58 }));
            Assert.That(Number(Option(rows["structure.nmc_falls_deck"], "deck")), Is.EqualTo(3.4));
            Assert.That(Number(Option(rows["structure.nmc_falls_deck_landing"], "deck")), Is.EqualTo(3.2));
            foreach (string id in new[] { "structure.nmc_the_falls_water", "structure.nmc_falls_deck", "structure.nmc_falls_deck_landing" })
                Assert.That(Number(Field(rows[id], "Z")), Is.Zero, id + " is a ground pivot, not the deck level");
        }

        [Test]
        public void RestaurantAndChandlery_AreMainStreetTakeovers_AndRetainTheirRoles()
        {
            var street = Rows(KeyScene("main_street"));
            foreach (string id in new[] { "structure.nmc_restaurant_bar", "structure.nmc_chandlery" })
            {
                string row = street.Single(r => Field(r, "Id") == id);
                object takeover = DeckSidecarJson.Parse(Option(row, "nmc.takeover"));
                Assert.That(String(takeover, "from"), Is.EqualTo("keyscene.nmc_town_bridge"));
                Assert.That(String(takeover, "to"), Is.EqualTo("keyscene.nmc_main_street"));
                Assert.That(Rows(KeyScene("town_bridge")).Count(r => Field(r, "Id") == id), Is.Zero);
            }
            string restaurant = street.Single(r => Field(r, "Id") == "structure.nmc_restaurant_bar");
            Assert.That(Option(restaurant, "preset"), Is.EqualTo("harbourmaster"));
        }

        [Test]
        public void RodSeller_KeepsItsOfferPriceAndWallet_ExactlyOnce()
        {
            var shops = SceneDocuments().Where(d => d.Contains("HiddenHarbours.Economy.GearShop")
                && Field(d, "_sellerId") == "seller.nmc_chandlery").ToArray();
            Assert.That(shops.Length, Is.EqualTo(1));
            Assert.That(Field(shops[0], "_offer"), Does.Contain("guid: 6cd2ccfae30a43519c8c111a79999c89"));
            Assert.That(Field(shops[0], "_walletProvider"), Is.EqualTo("{fileID: 957678902}"));
            string offer = File.ReadAllText("Assets/_Project/Data/Resources/Catalog/Gear/Rod.asset");
            Assert.That(Field(offer, "Id"), Is.EqualTo("gear.rod"));
            Assert.That(Number(Field(offer, "Price")), Is.EqualTo(60));
            Assert.That(File.ReadAllText("Assets/Tests/PlayMode/NineMileCreekLoopPlayTests.cs"),
                Does.Contain("const string ChandlerySellerId = \"seller.nmc_chandlery\";"));
        }

        [TestCase("town_bridge", 53)]
        [TestCase("the_falls", 11)]
        [TestCase("main_street", 91)]
        public void PhaseB_EachOwnedRowIsPlacedExactlyOnce(string scene, int count)
        {
            var ids = Rows(KeyScene(scene)).Select(r => Field(r, "Id")).ToArray();
            var placements = PlacedIds(SceneDocuments()).Where(p => ids.Contains(p.Id)).ToArray();
            if (placements.Length == 0 && Field(File.ReadAllText(KeyScene(scene)), "Placed") == "0")
                Assert.Ignore("H7 Phase B: " + scene + " has no placements; art/scene work follows H9. No editor proof in Phase A.");
            Assert.That(ids.Length, Is.EqualTo(count));
            Assert.That(placements.Select(p => p.Id), Is.EquivalentTo(ids), "partial placement, duplicate id or missing row");
            Assert.That(Field(File.ReadAllText(KeyScene(scene)), "Placed"), Is.EqualTo("1"));
        }

        [Test]
        public void PhaseB_RodCounterReplacesGeneralStore_WithTheSameSeller()
        {
            var docs = SceneDocuments();
            bool old = docs.Any(d => Field(d, "m_Name") == "GeneralStore");
            var chandlery = PlacedIds(docs).Where(p => p.Id == "structure.nmc_chandlery").ToArray();
            bool landed = chandlery.Length != 0;
            if (old && !landed)
                Assert.Ignore("H7 Phase B: GeneralStore still sells the rod; move GearShop, anchored speaker, loop and arrival/store guards together.");
            Assert.That(old, Is.False, "retire GeneralStore by name after transferring the vendor");
            Assert.That(chandlery.Length, Is.EqualTo(1), "the new counter needs exactly one chandlery placement");
            RodSeller_KeepsItsOfferPriceAndWallet_ExactlyOnce();
            string shop = docs.Single(d => d.Contains("HiddenHarbours.Economy.GearShop") && Field(d, "_sellerId") == "seller.nmc_chandlery");
            string go = Ref(shop, "m_GameObject");
            var seen = new HashSet<string>();
            while (go != chandlery[0].GameObject && seen.Add(go))
            {
                string transform = docs.Single(d => Regex.IsMatch(d, @"^4 &") && Ref(d, "m_GameObject") == go);
                string parent = Ref(transform, "m_Father");
                Assert.That(parent, Is.Not.EqualTo("0"), "the rod counter is not under its chandlery");
                go = Ref(docs.Single(d => Regex.IsMatch(d, @"^4 &" + parent + @"\b")), "m_GameObject");
            }
            Assert.That(go, Is.EqualTo(chandlery[0].GameObject), "cycle in the counter hierarchy");
        }

        [Test]
        public void PhaseB_WalkSupportsAndTwoFallsSteps_RequireThePlacedScene()
        {
            Assert.Ignore("H7 Phase B scene acceptance: walk banks, stairs, decks and doors at low/high tide; prove two 0.2 m Falls steps, counter access, eleven flat anchors and grounded town dory at both night boards. No scene edit or editor lease in Phase A.");
        }

        [Test]
        public void PhaseB_LightingGlassWordsAndEffects_RequireArtLaneProof()
        {
            Assert.Ignore("Tier C: reconcile +6.2 lamp cap with source emitter heights, no second fill; actual ManorIso lit glass, trade words, blank plaques, town sign, foam/audio/dusk seams; bridge sunset/night, Falls mist/ebb and street day/night plates with export and performance. No visual baseline is changed here.");
        }

        internal static string[][] Table(string name) => File.ReadAllLines(Fixture + name).Skip(1)
            .Where(l => l.Length != 0).Select(l => l.Split('\t')).ToArray();
        internal static List<string> Rows(string path)
        {
            string text = File.ReadAllText(path).Replace("\r\n", "\n");
            int start = text.IndexOf("\n  Pieces:\n", StringComparison.Ordinal);
            int end = text.IndexOf("\n  Lights:", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), path);
            Assert.That(end, Is.GreaterThan(start), path);
            return Regex.Matches(text.Substring(start, end - start), @"(?ms)^  - Id: .*?(?=^  - Id: |\z)")
                .Cast<Match>().Select(m => m.Value).ToList();
        }
        internal static string Field(string text, string key)
        {
            var m = Regex.Match(text, @"(?m)^\s*(?:- )?" + Regex.Escape(key) + @":([^\r\n]*)");
            return m.Success ? Unquote(m.Groups[1].Value.Trim()) : "";
        }
        internal static string Option(string row, string key)
        {
            var match = Regex.Match(row.Replace("\r\n", "\n"), @"(?ms)^    - Key: " + Regex.Escape(key)
                + @"\n      Value: (.*?)(?=\n    - Key: |\z)");
            Assert.That(match.Success, Is.True, "missing option " + key);
            return Unquote(Regex.Replace(match.Groups[1].Value.Trim(), @"\n\s+", " "));
        }
        internal static string Unquote(string value)
        {
            if (value.StartsWith("'") && value.EndsWith("'")) return value.Substring(1, value.Length - 2).Replace("''", "'");
            if (value.StartsWith("\"") && value.EndsWith("\"")) return (string)DeckSidecarJson.Parse(value);
            return value;
        }
        internal static double Number(string value) => double.Parse(value, CultureInfo.InvariantCulture);
        internal static double[] Vector(string value) => Regex.Matches(value, @"[xyz]:\s*([-+0-9.eE]+)")
            .Cast<Match>().Select(m => Number(m.Groups[1].Value)).ToArray();
        internal static string[] List(string text, string key)
        {
            var m = Regex.Match(text.Replace("\r\n", "\n"), @"(?ms)^  " + key + @":\s*\n((?:  - [^\n]*\n)*)");
            return m.Groups[1].Value.Split('\n').Where(l => l.StartsWith("  - ")).Select(l => Unquote(l.Substring(4).Trim())).ToArray();
        }
        internal static object Json(string path) => DeckSidecarJson.Parse(File.ReadAllText(path));
        internal static object Member(object value, string key) => DeckSidecarJson.Member(value, key);
        internal static string String(object value, string key) => DeckSidecarJson.String(Member(value, key));
        internal static List<object> Array(object value) => DeckSidecarJson.AsArray(value) ?? throw new FormatException("Expected JSON array");
        internal static string[] SceneDocuments() => Regex.Split(File.ReadAllText(ScenePath), @"(?m)^--- !u!").Skip(1).ToArray();
        static string Ref(string text, string field) => Regex.Match(Field(text, field), @"fileID: (\d+)").Groups[1].Value;
        static (string Id, string GameObject)[] PlacedIds(string[] docs) => docs.Where(d => d.StartsWith("114 &", StringComparison.Ordinal))
            .SelectMany(d => new[] { "_placementId", "_id" }.Select(k => (Id: Field(d, k), GameObject: Ref(d, "m_GameObject"))))
            .Where(p => p.Id.Length != 0).Distinct().ToArray();
    }
}
