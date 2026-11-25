using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using static HiddenHarbours.Tests.EditMode.NmcH7RowTests;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Content validation for staged data only: no frames, renderer, kit execution or scene write.
    /// The source JSON retains all fields; typed points/colliders describe the delivered default geometry.
    /// Per-placement options still require the Phase B art/geometry pipeline.</summary>
    public class NmcH7ContentValidationTests
    {
        internal const string Kit = "docs/art/rigs/nmc-set-pieces-kit/gameplay/";
        internal static readonly (string Key, string Id)[] Kinds =
        {
            ("bench", "prop.nmc_bench"), ("boardwalk", "structure.nmc_town_boardwalk"),
            ("boardwalkLamp", "prop.nmc_boardwalk_lamp"), ("cascade", "structure.nmc_the_falls_water"),
            ("chandlery", "structure.nmc_chandlery"), ("fallsDeck", "structure.nmc_falls_deck"),
            ("guardRail", "prop.nmc_guard_rail"), ("picnicTable", "prop.nmc_picnic_table"),
            ("riverDeck", "structure.nmc_river_deck"), ("riverWall", "structure.nmc_river_wall"),
            ("routeBridge", "structure.nmc_route91_bridge"), ("signBoard", "prop.nmc_sign_board"),
            ("wallStairs", "structure.nmc_wall_stairs"),
        };
        internal static string DefPath(string key) => "Assets/_Project/Data/SetPieces/SetPiece_nmc_" + key + ".asset";
        public static IEnumerable<string> PieceKeys() => Kinds.Select(p => p.Key);

        [TestCaseSource(nameof(PieceKeys))]
        public void Metadata_PreservesDeliveredGameplayAndTypedGeometry(string key)
        {
            var pin = Kinds.Single(p => p.Key == key);
            string text = File.ReadAllText(DefPath(key));
            object source = Json(Kit + key + ".gameplay.json");
            Assert.That(String(source, "schema"), Is.EqualTo("hidden-harbours/nmc-set-piece@1"));
            Assert.That(String(source, "defType"), Is.EqualTo("SetPieceDef"));
            Assert.That(String(source, "id"), Is.EqualTo(pin.Id));
            Assert.That(Field(text, "Id"), Is.EqualTo(pin.Id));
            Assert.That(Field(text, "RigKey"), Is.EqualTo(key));
            Assert.That(Field(text, "SceneId"), Is.EqualTo(String(source, "scene")), "provenance is not placement ownership");
            Assert.That(Field(text, "DisplayName"), Is.EqualTo(String(source, "name")));
            Assert.That(DeckSidecarJson.Parse(Field(text, "GameplayJson")), Is.EqualTo(source), "no sidecar field is silently dropped");
            foreach (string field in new[] { "Layer", "Walk", "Mount" })
                Assert.That(Field(text, field), Is.EqualTo(String(source, field.ToLowerInvariant())), field);
            for (int end = 0; end < 2; end++)
                Assert.That(Vector(Field(text, end == 0 ? "BoundsMin" : "BoundsMax")),
                    Is.EqualTo(new[] { "x", "y", "z" }.Select(a => (double)Array(Member(Member(source, "bounds"), a))[end])));

            var actualColliders = Entries(text, "Colliders", "Type");
            var colliders = Array(Member(source, "colliders"));
            Assert.That(actualColliders.Length, Is.EqualTo(colliders.Count));
            for (int i = 0; i < colliders.Count; i++)
            {
                Assert.That(Field(actualColliders[i], "Type"), Is.EqualTo(String(colliders[i], "type")));
                Assert.That(Field(actualColliders[i], "Blocks"), Is.EqualTo(String(colliders[i], "blocks")));
                Assert.That(PointList(actualColliders[i]), Is.EqualTo(Array(Member(colliders[i], "pts"))
                    .Select(p => Array(p).Cast<double>().ToArray()).ToArray()));
            }

            var anchors = DeckSidecarJson.AsObject(Member(source, "anchors"));
            var points = anchors.Where(p => p.Value is List<object> a && a.Count == 3 && a.All(n => n is double)).ToArray();
            var actualAnchors = Entries(text, "Anchors", "Name");
            Assert.That(actualAnchors.Select(a => Field(a, "Name")), Is.EquivalentTo(points.Select(p => p.Key)));
            foreach (var point in points)
                Assert.That(Vector(Field(actualAnchors.Single(a => Field(a, "Name") == point.Key), "Position")),
                    Is.EqualTo(Array(point.Value).Cast<double>()), point.Key);

            var words = Member(Member(source, "anchors"), "words") as List<object> ?? new List<object>();
            var actualWords = Entries(text, "Words", "Id");
            Assert.That(actualWords.Select(w => Field(w, "Id")), Is.EquivalentTo(words.Select(w => String(w, "id"))));
            foreach (var word in words)
            {
                string actual = actualWords.Single(w => Field(w, "Id") == String(word, "id"));
                var corners = Member(word, "corners") as List<object> ?? new List<object>();
                Assert.That(PointList(actual), Is.EqualTo(corners.Select(p => Array(p).Cast<double>().ToArray()).ToArray()));
                Assert.That(Field(actual, "BodyId"), Is.Empty, "no new player-facing words");
            }
            object light = Member(source, "light");
            Assert.That(Field(text, "HasLight"), Is.EqualTo(light == null ? "0" : "1"));
            if (light != null)
            {
                Assert.That(Field(Section(text, "Light"), "When"), Is.EqualTo(String(light, "when")));
                Assert.That(Regex.Matches(Section(text, "Light"), @"(?m)^    - ([^\r\n]+)").Cast<Match>()
                    .Select(m => Unquote(m.Groups[1].Value)), Is.EqualTo(Array(Member(light, "emitters"))));
            }
        }

        [Test]
        public void SharedKinds_CoverExactlyTheEightyTwoExistingKitRows_OncePerKind()
        {
            var rows = Scenes.SelectMany(s => Rows(KeyScene(s))).Where(r => Field(r, "Kit").StartsWith("NmcSetPieces", StringComparison.Ordinal)).ToArray();
            Assert.That(rows.Length, Is.EqualTo(82));
            Assert.That(rows.Select(r => Field(r, "Piece")).Distinct(), Is.EquivalentTo(Kinds.Select(k => k.Key)));
            Assert.That(Kinds.Select(k => k.Id).Distinct().Count(), Is.EqualTo(13));
            string rig = File.ReadAllText("docs/art/rigs/nmc-set-pieces-kit/nmcSetPieces.js");
            foreach (var kind in Kinds)
                Assert.That(rig, Does.Match(@"PIECES\." + kind.Key + @"\s*="), kind.Key + " absent from delivered kit");
        }

        [Test]
        public void NewIdentities_AreUniqueAndRegisteredWithTheirExactGuidAndType()
        {
            var entries = DefIdentityLedger.Load().Entries;
            var defs = Directory.GetFiles("Assets/_Project/Data/SetPieces", "*.asset")
                .Select(p => (Path: p, Id: Field(File.ReadAllText(p), "Id"))).ToArray();
            foreach (var pin in Kinds)
            {
                Assert.That(defs.Count(d => d.Id == pin.Id), Is.EqualTo(1), pin.Id);
                string guid = GuidFor(DefPath(pin.Key));
                Assert.That(guid, Does.Match("^[0-9a-f]{32}$"));
                var entry = entries.Where(e => e.Guid == guid).ToArray();
                Assert.That(entry.Length, Is.EqualTo(1), pin.Id);
                Assert.That(entry[0].Id, Is.EqualTo(pin.Id));
                Assert.That(entry[0].Type, Is.EqualTo("HiddenHarbours.Art.SetPieceDef"));
                Assert.That(entries.Count(e => e.Type == "HiddenHarbours.Art.SetPieceDef" && e.Id == pin.Id), Is.EqualTo(1));
            }
        }

        [Test]
        public void PhaseAMetadata_HasNoFramesBakeClaimsOrLiveSceneReferences()
        {
            string scene = File.ReadAllText(ScenePath);
            foreach (var pin in Kinds)
            {
                string path = DefPath(pin.Key), text = File.ReadAllText(path);
                Assert.That(Field(text, "Frames"), Is.EqualTo("[]"), pin.Key);
                Assert.That(Field(text, "Variants"), Is.EqualTo("[]"), pin.Key);
                Assert.That(Field(text, "BakedOptionsJson"), Is.EqualTo("{}"), "nothing was baked");
                Assert.That(Vector(Field(text, "CellSize")), Is.EqualTo(new double[] { 0, 0 }));
                Assert.That(Field(text, "HasVendor"), Is.EqualTo("0"), "counter metadata does not wire a seller");
                Assert.That(scene, Does.Not.Contain(GuidFor(path)), pin.Key + " must not enter the scene without art");
            }
            foreach (string name in Scenes)
            {
                string text = File.ReadAllText(KeyScene(name));
                Assert.That(Field(text, "Placed"), Is.EqualTo("0"));
                Assert.That(Field(text, "PlacementIds"), Is.EqualTo("[]"));
            }
        }

        internal static string GuidFor(string path) => Regex.Match(File.ReadAllText(path + ".meta"), @"(?m)^guid: ([0-9a-f]+)").Groups[1].Value;
        static string Section(string text, string name)
        {
            var m = Regex.Match(text.Replace("\r\n", "\n"), @"(?ms)^  " + name + @":([^\n]*\n.*?)(?=^  [A-Za-z]|\z)");
            Assert.That(m.Success, Is.True, name);
            return m.Groups[1].Value;
        }
        static string[] Entries(string text, string section, string first) => Regex.Matches(Section(text, section),
            @"(?ms)^  - " + first + @":.*?(?=^  - " + first + @":|\z)").Cast<Match>().Select(m => m.Value).ToArray();
        static double[][] PointList(string text) => Regex.Matches(text, @"(?m)^    - (\{[^\r\n]+\})").Cast<Match>()
            .Select(m => Vector(m.Groups[1].Value)).ToArray();
    }
}
