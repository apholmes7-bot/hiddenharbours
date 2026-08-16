using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;      // MiniJson — the reader the shipped importers use
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The COMMITTED hand-prop block in <c>FisherFightAnchors.json</c>, checked against the rig it was
    /// baked from. The sidecar is the only part of the rev-6.4 drop anything downstream will ever read,
    /// so it is the part worth guarding hardest.
    ///
    /// <para><b>Both defects these tests pin actually happened during this import, and neither was
    /// catchable by the compiler.</b> The block is assembled from string fragments at bake time, so a
    /// malformed one compiles perfectly and fails only when something reads it. One version shipped
    /// single-quoted JSON keys (valid JavaScript, rejected by every JSON parser); another put a stray
    /// brace in a non-interpolated fragment and killed the rig host. A STALE block is the same family
    /// and the most dangerous of the three — it parses, it looks plausible, and it positions carried
    /// objects with an older kit's geometry.</para>
    /// </summary>
    public class HandPropSidecarParityTests
    {
        const string Hands = "CharacterHands6";

        static readonly string[] Props =
            { "rodTrail", "rodSling", "fish", "clam", "knife", "gaff", "rope" };

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        static string SidecarPath => Path.Combine(
            RepoRoot,
            CharacterRigBakeMenu.CharacterArtFolder,
            CharacterRigBakeMenu.AnchorFileName);

        /// <summary>
        /// Local string/bool readers, kept even though <see cref="MiniJson"/> has since grown
        /// <c>String</c>/<c>Bool</c> for the carry-anchor importer — because these two answer DIFFERENTLY
        /// on purpose. <see cref="Str"/> reports an explicit JSON <c>null</c> as the text "null" so a test
        /// can tell "no rig baked" apart from "no such key", which is the distinction the art-debt test
        /// below turns on; the shared helper flattens both to a C# null, which is what its importers
        /// want. Do not "unify" them.
        /// </summary>
        static string Str(object node, string key)
            => node is Dictionary<string, object> d && d.TryGetValue(key, out object v)
                ? (v as string ?? (v == null ? "null" : v.ToString()))
                : null;

        static bool Bool(object node, string key)
            => node is Dictionary<string, object> d && d.TryGetValue(key, out object v)
               && v is bool b && b;

        /// <summary>
        /// Parse the committed sidecar. <b>MiniJson.Parse throws on malformed input</b>, so this is
        /// itself the general gate that the block is real JSON — the single-quote test below only
        /// exists to turn that throw into a diagnosis.
        /// </summary>
        static Dictionary<string, object> LoadHandProps(out string rawText)
        {
            Assert.IsTrue(File.Exists(SidecarPath), $"no anchor sidecar at {SidecarPath} — re-bake.");
            rawText = File.ReadAllText(SidecarPath);

            object root = MiniJson.Parse(rawText);
            var hp = MiniJson.Dict(root, "handProps");
            Assert.IsNotNull(hp,
                "the sidecar carries no 'handProps' block. Re-bake with " +
                "Hidden Harbours ▸ Art ▸ Bake Character Sheets — the player.");
            return hp;
        }

        [Test]
        public void TheSidecar_CarriesEveryProp_WithEightFacingRows()
        {
            var hp = LoadHandProps(out _);
            var props = MiniJson.Dict(hp, "props");
            Assert.IsNotNull(props, "handProps has no 'props' map.");

            foreach (string prop in Props)
            {
                var node = MiniJson.Dict(props, prop);
                Assert.IsNotNull(node, $"'{prop}' is missing from the baked table.");
                var rows = MiniJson.List(node, "rows");
                Assert.IsNotNull(rows, $"'{prop}' carries no rows.");
                Assert.AreEqual(8, rows.Count, $"'{prop}' should carry eight facing rows.");
            }

            // The pose the absolute pins were measured at must be STATED, not inferred — restX/restY
            // are meaningless without it.
            var rest = MiniJson.Dict(hp, "restPose");
            Assert.IsNotNull(rest, "the block does not say which pose restX/restY were measured at.");
            Assert.AreEqual(CharacterRigBaker.HandPropRestAnim, Str(rest, "anim"));
            Assert.AreEqual(CharacterRigBaker.HandPropRestFrame, MiniJson.Int(rest, "frame"));
        }

        /// <summary>
        /// <b>The keys are JSON, not JavaScript.</b> The baker holds each prop name in two quotings — a
        /// single-quoted JS literal it passes into the rig host, and a double-quoted JSON key it writes
        /// to disk — and using the first where the second belongs produces a file the rig host accepts
        /// and every consumer rejects. That shipped once during this import; this is the diagnosis.
        /// </summary>
        [Test]
        public void EveryPropKey_IsDoubleQuoted_NotAJavaScriptLiteral()
        {
            LoadHandProps(out string raw);
            int i = raw.IndexOf("\"handProps\"", System.StringComparison.Ordinal);
            Assert.Greater(i, 0, "no handProps block to inspect.");
            string block = raw.Substring(i);

            foreach (string prop in Props)
            {
                StringAssert.Contains($"\"{prop}\":", block,
                    $"'{prop}' is not a double-quoted JSON key in the sidecar.");
                Assert.IsFalse(block.Contains($"'{prop}':"),
                    $"'{prop}' was written as a JavaScript literal ('{prop}':) rather than a JSON key. " +
                    "Use the raw name for the key and JsString() only for expressions handed to the " +
                    "rig host — they are not interchangeable.");
            }
        }

        /// <summary>
        /// <b>The committed numbers still describe the committed rig.</b> Re-solves every prop × facing
        /// through the V8 host and compares against what is on disk, so a sidecar that drifted from its
        /// rig cannot sit there looking plausible.
        /// </summary>
        [Test]
        public void EveryRow_StillMatchesTheRig_ItWasBakedFrom()
        {
            var hp = LoadHandProps(out _);
            var props = MiniJson.Dict(hp, "props");

            using var host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, RigCatalog.Get("characterHands"));
            // The same prop rigs the baker loads — 'fish' resolves hands:'auto' by asking FishIso, so
            // without them this comparison would be against a differently-resolved table.
            foreach (string k in new[] { "rod", "fish", "shellfish" })
                RigCatalog.InstallModule(host, RigCatalog.Get(k));

            string anim = CharacterRigBaker.HandPropRestAnim;
            int frame = CharacterRigBaker.HandPropRestFrame;

            foreach (string prop in Props)
            {
                var rows = MiniJson.List(MiniJson.Dict(props, prop), "rows");

                for (int d = 0; d < 8; d++)
                {
                    object row = rows[d];
                    string opts = $"{{anim:'{anim}',frame:{frame},elev:40,build:{{preset:'fisher'}}}}";
                    string pin = $"{Hands}.pin('{prop}',{d},{opts})";

                    Assert.AreEqual(d, MiniJson.Int(row, "dir"), $"'{prop}' rows are out of order.");

                    Assert.AreEqual(host.EvaluateString($"String({pin}.hand)"), Str(row, "hand"),
                        $"'{prop}' at dir {d}: the sidecar and the rig disagree on WHICH HAND holds " +
                        "it. Re-bake the character sheets.");

                    Assert.AreEqual((int)host.EvaluateNumber($"{pin}.itemDir"),
                                    MiniJson.Int(row, "itemDir"),
                        $"'{prop}' at dir {d}: itemDir drifted — the turntable cell the prop draws " +
                        "at is stale.");

                    Assert.AreEqual(host.EvaluateBool($"{pin}.behind"), Bool(row, "behind"),
                        $"'{prop}' at dir {d}: draw order drifted.");

                    // 0.01 px: the baker rounds to 3 decimals, so a tighter bound would trip on the
                    // rounding itself and a looser one would not notice a real geometry change.
                    Assert.AreEqual(host.EvaluateNumber($"{pin}.dx"),
                                    MiniJson.Float(row, "gripDx"), 0.01,
                        $"'{prop}' at dir {d}: the grip offset drifted from the rig.");
                    Assert.AreEqual(host.EvaluateNumber($"{pin}.dy"),
                                    MiniJson.Float(row, "gripDy"), 0.01,
                        $"'{prop}' at dir {d}: the grip offset drifted from the rig.");
                }
            }
        }

        /// <summary>
        /// <b>The arm nothing can reach, and the one whose failure takes every consumer down at once.</b>
        ///
        /// <para><c>handProps</c> is the LAST key of the document, so the absent-layer branch's output is
        /// followed immediately by the closing brace. It shipped with a trailing comma — <c>"handProps":
        /// null,</c> then <c>}</c> — which is not JSON, and would have made the whole sidecar unreadable
        /// rather than merely dropping one block: the rod overlay, the bobber, the held fish and the
        /// carry anchors all parse the same file. It has never fired, because a character bake always
        /// installs the layer; a rig kit that predates it would have been the first to find out.</para>
        ///
        /// <para>This drives that arm directly on a host with nothing installed, which is exactly the
        /// condition it exists for, and then PARSES the result rather than eyeballing the text —
        /// <see cref="MiniJson"/> rejects a trailing comma (it demands a key after every comma), so the
        /// parse is the assertion and the string checks below only turn a throw into a diagnosis.</para>
        /// </summary>
        [Test]
        public void AnAbsentHandPropLayer_StillWritesParseableJson()
        {
            using var host = RigScriptHostFactory.Create();      // NOTHING installed
            Assert.IsFalse(host.EvaluateBool($"typeof {Hands} === 'object' && {Hands} !== null"),
                           "precondition: this host must NOT have the hand-prop layer");

            // The document's tail, exactly as the baker assembles it: the states block closed with its
            // comma, the handProps arm, the closing brace.
            var sb = new StringBuilder();
            sb.Append("{\n  \"rig\": \"probe\",\n  \"states\": {\n  },\n");
            CharacterRigBaker.AppendHandProps(sb, host, default, AzimuthConvention.Clockwise, "fisher");
            sb.Append("}\n");
            string json = sb.ToString();

            StringAssert.Contains("\"handProps\": null", json, "the absent arm must still declare the key");
            Assert.IsFalse(json.Contains("null,\n}"),
                           $"a trailing comma before the closing brace:\n{json}");

            object parsed = null;
            Assert.DoesNotThrow(() => parsed = MiniJson.Parse(json),
                                $"the absent-layer sidecar is not parseable JSON:\n{json}");
            Assert.IsNull(MiniJson.Dict(parsed, "handProps"),
                          "an absent layer must read as absent, not as an empty block");
        }
    }
}
