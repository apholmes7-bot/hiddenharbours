using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The sidecar reader, on synthetic input: the parse, the <b>staleness rule</b>, and the washboard
    /// construction. No disk, no assets — a string and a byte array in, polygons out.
    ///
    /// <para>The staleness pair is the important one. <c>derivedFromRigSha256</c> exists so a reshaped
    /// hull cannot silently keep serving polygons cut from the old one, and these pin both halves of
    /// that: a genuinely different rig is REFUSED, while a rig that differs only in its line endings is
    /// accepted and reported — because the rule is about geometry, and a line ending cannot move a
    /// vertex.</para>
    /// </summary>
    public class DeckSidecarReaderTests
    {
        const string Rig = "// a rig\nconst L = 4.5;\n";

        static byte[] Lf(string s) => Encoding.UTF8.GetBytes(s);
        static byte[] Crlf(string s) => Encoding.UTF8.GetBytes(s.Replace("\n", "\r\n"));

        static string Sidecar(string sha, string body) =>
            "{\n  \"rig\": \"testRig.js\",\n" +
            $"  \"derivedFromRigSha256\": \"{sha}\",\n" +
            "  \"frame\": { \"LOA_m\": 4.5 },\n" +
            body + "\n}";

        const string OneFlatDeck =
            "  \"DECK\": [ { \"id\": \"sole\", \"winding\": \"ccw_from_above\", \"z\": 0.5,\n" +
            "    \"polygon\": [ [-1, -2], [1, -2], [1, 2], [-1, 2] ] } ]";

        // ---- the staleness rule ---------------------------------------------------------------------

        [Test]
        public void AReshapedHullIsRefused()
        {
            var read = DeckSidecarReader.Read(Sidecar(new string('a', 64), OneFlatDeck), "test", Lf(Rig));
            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("STALE")), string.Join(" | ", read.Errors));
            Assert.IsEmpty(read.Areas, "a refused sidecar must contribute no geometry at all");
        }

        [Test]
        public void AnExactHashImportsSilently()
        {
            string sha = DeckSidecarReader.Sha256Hex(Lf(Rig));
            var read = DeckSidecarReader.Read(Sidecar(sha, OneFlatDeck), "test", Lf(Rig));
            Assert.IsTrue(read.Ok, string.Join(" | ", read.Errors));
            Assert.AreEqual(RigHashMatch.Exact, read.HashMatch);
            Assert.IsEmpty(read.Notes);
        }

        [Test]
        public void ACrlfDerivedHashIsAcceptedAndReported()
        {
            // The real case: eight of the eleven shipped sidecars were derived from CRLF working copies
            // of rigs the repo stores with LF. Same bytes, same boat, different newline.
            string sha = DeckSidecarReader.Sha256Hex(Crlf(Rig));
            var read = DeckSidecarReader.Read(Sidecar(sha, OneFlatDeck), "test", Lf(Rig));
            Assert.IsTrue(read.Ok, string.Join(" | ", read.Errors));
            Assert.AreEqual(RigHashMatch.LineEndingNormalized, read.HashMatch);
            Assert.IsNotEmpty(read.Notes, "it is accepted, but never silently");
        }

        // ---- the parse ------------------------------------------------------------------------------

        [Test]
        public void AFlatPolygonTakesItsSingleZOnEveryVertex()
        {
            string sha = DeckSidecarReader.Sha256Hex(Lf(Rig));
            var read = DeckSidecarReader.Read(Sidecar(sha, OneFlatDeck), "test", Lf(Rig));
            Assert.AreEqual(1, read.Areas.Count);
            Assert.AreEqual("sole", read.Areas[0].Id);
            Assert.IsFalse(read.Areas[0].IsWashboard);
            Assert.AreEqual(4, read.Areas[0].Vertices.Length);
            Assert.IsTrue(read.Areas[0].Vertices.All(v => Mathf.Approximately(v.z, 0.5f)));
            Assert.AreEqual(4.5f, read.LoaMeters, 1e-4f);
        }

        [Test]
        public void APolygon3dKeepsItsPerVertexHeights()
        {
            string sha = DeckSidecarReader.Sha256Hex(Lf(Rig));
            const string body =
                "  \"DECK\": [ { \"id\": \"foredeck\",\n" +
                "    \"polygon3d\": [ [-1, 0, 1.2], [1, 0, 1.2], [1, 2, 1.7], [-1, 2, 1.7] ] } ]";
            var read = DeckSidecarReader.Read(Sidecar(sha, body), "test", Lf(Rig));
            Assert.IsTrue(read.Ok, string.Join(" | ", read.Errors));
            Assert.AreEqual(1.2f, read.Areas[0].Vertices[0].z, 1e-4f);
            Assert.AreEqual(1.7f, read.Areas[0].Vertices[2].z, 1e-4f);
        }

        [Test]
        public void AMeasuredHullWithNoDeckSectionIsAnError()
        {
            // Absence is data for WASHBOARD and CLEATS — but a sidecar exists because the hull WAS
            // measured, so an empty DECK is a failed export, not an open boat.
            string sha = DeckSidecarReader.Sha256Hex(Lf(Rig));
            var read = DeckSidecarReader.Read(Sidecar(sha, "  \"CLEATS\": []"), "test", Lf(Rig));
            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("DECK")));
        }

        [Test]
        public void NoWashboardAndNoCleatsIsSilence()
        {
            string sha = DeckSidecarReader.Sha256Hex(Lf(Rig));
            var read = DeckSidecarReader.Read(Sidecar(sha, OneFlatDeck), "test", Lf(Rig));
            Assert.IsTrue(read.Ok);
            Assert.IsEmpty(read.Areas.Where(a => a.IsWashboard), "an open boat has no washboards…");
            Assert.IsEmpty(read.Cleats, "…and this one has no modelled tie-off");
            Assert.IsEmpty(read.Errors, "neither absence is a fault");
        }

        [Test]
        public void CleatsComeThroughNamed()
        {
            string sha = DeckSidecarReader.Sha256Hex(Lf(Rig));
            string body = OneFlatDeck + ",\n  \"CLEATS\": [ { \"id\": \"bow_1\", \"type\": " +
                          "\"samson_post\", \"pos\": [0, 5.6, 2.6] } ]";
            var read = DeckSidecarReader.Read(Sidecar(sha, body), "test", Lf(Rig));
            Assert.AreEqual(1, read.Cleats.Count);
            Assert.AreEqual("bow_1", read.Cleats[0].Id);
            Assert.AreEqual("samson_post", read.Cleats[0].Type);
            Assert.AreEqual(5.6f, read.Cleats[0].Position.y, 1e-4f);
        }

        [Test]
        public void MalformedJsonFailsLoudlyAndImportsNothing()
        {
            var read = DeckSidecarReader.Read("{ \"rig\": ", "test", Lf(Rig), enforceHash: false);
            Assert.IsFalse(read.Ok);
            Assert.IsEmpty(read.Areas);
        }

        // ---- washboards ------------------------------------------------------------------------------

        [Test]
        public void AWashboardStripIsItsOuterEdgeOffsetInboardByItsWidth()
        {
            // A straight port-side edge at x = −2: the strip's inner rail must land at −2 + 0.44.
            var edge = new[]
            {
                new Vector3(-2f, -3f, 1.2f), new Vector3(-2f, 0f, 1.3f), new Vector3(-2f, 3f, 1.4f),
            };
            Vector3[] strip = DeckSidecarReader.BuildStrip(edge, 0.44f);

            Assert.AreEqual(6, strip.Length, "three outer points close into a six-vertex ring");
            Assert.AreEqual(-2f, strip[0].x, 1e-4f);
            float innerMax = strip.Max(v => v.x);
            Assert.AreEqual(-1.56f, innerMax, 1e-3f, "the inner rail is 0.44 m toward the centreline");
            Assert.IsTrue(strip.All(v => v.x < 0f), "a port washboard never crosses the keel");
        }

        [Test]
        public void TheMirroredSideIsTheSameStripWithXNegated()
        {
            string sha = DeckSidecarReader.Sha256Hex(Lf(Rig));
            string body = OneFlatDeck + ",\n" +
                "  \"WASHBOARD\": [\n" +
                "    { \"side\": \"port\", \"width_m\": 0.4, \"outer_edge\": " +
                "[ [-2, -3, 1.2], [-2, 3, 1.4] ] },\n" +
                "    { \"side\": \"starboard\", \"mirror\": \"port across x=0\" } ]";
            var read = DeckSidecarReader.Read(Sidecar(sha, body), "test", Lf(Rig));
            Assert.IsTrue(read.Ok, string.Join(" | ", read.Errors));

            SidecarArea port = read.Areas.Single(a => a.Id == "washboard_port");
            SidecarArea star = read.Areas.Single(a => a.Id == "washboard_starboard");
            Assert.AreEqual(port.Vertices.Length, star.Vertices.Length);
            Assert.IsTrue(port.Vertices.All(v => v.x < 0f));
            Assert.IsTrue(star.Vertices.All(v => v.x > 0f));
            Assert.AreEqual(-port.Vertices.Min(v => v.x), star.Vertices.Max(v => v.x), 1e-4f);
        }

        [Test]
        public void WashboardsAreTaggedApartFromTheDeck()
        {
            // They must reach the def, and they must not be mistaken for walkable sole: the climb verb
            // (Space) is what promotes them, and it does not exist yet.
            string sha = DeckSidecarReader.Sha256Hex(Lf(Rig));
            string body = OneFlatDeck + ",\n" +
                "  \"WASHBOARD\": [ { \"side\": \"port\", \"width_m\": 0.4, \"outer_edge\": " +
                "[ [-2, -3, 1.2], [-2, 3, 1.4] ] } ]";
            var read = DeckSidecarReader.Read(Sidecar(sha, body), "test", Lf(Rig));
            Assert.AreEqual(1, read.Areas.Count(a => a.IsWashboard));
            Assert.AreEqual(1, read.Areas.Count(a => !a.IsWashboard));
        }

        // ---- naming ----------------------------------------------------------------------------------

        [Test]
        public void DefIdsFollowTheProjectConvention()
        {
            Assert.AreEqual("lobsterBoatIso", DeckSidecarImporter.StripRigSuffix("lobsterBoatIsoRig"));
            Assert.AreEqual("lobster_boat_iso", DeckSidecarImporter.SnakeCase("lobsterBoatIso"));
            Assert.AreEqual("stern_trawler_mk2_iso", DeckSidecarImporter.SnakeCase("sternTrawlerMk2Iso"));
        }
    }
}
