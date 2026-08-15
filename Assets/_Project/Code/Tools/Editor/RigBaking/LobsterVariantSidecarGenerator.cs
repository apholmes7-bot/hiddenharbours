using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Writes the eighteen lobster-variant gameplay sidecars from the rig's own generator.</b>
    ///
    /// <para><b>Why a generator and not eighteen authored files.</b> Every other hull's sidecar was
    /// measured by hand off its rig's station maths, which is honest work but is also a
    /// transcription — and the deck importer exists because a hand-transcribed constant drifted from
    /// the baked data beside it. This rig removes the transcription entirely: it exports
    /// <c>gameplayGeometry(variant)</c>, which derives the cockpit polygon, the foredeck, the
    /// washboards, the cleats and the anchors from the SAME <c>resolve(v)</c> the mesh bake reads. So
    /// the sidecars are not authored here, they are EXTRACTED here — and re-extracting is the only
    /// supported way to change one, exactly as re-running the importer is the only supported way to
    /// change a Def.</para>
    ///
    /// <para><b>The one thing this tool adds that the rig does not:
    /// <c>derivedFromRigSha256</c>.</b> The rig's generator omits it (measured, and pinned by
    /// <c>LobsterVariantRigTests.TheRigsOwnGenerator_StillOmitsProvenance</c>), and an ABSENT hash is
    /// not a soft warning — <see cref="DeckSidecarReader.MatchRigHash"/> returns
    /// <see cref="RigHashMatch.None"/> for it, which is the same refusal a STALE hash gets. So an
    /// unstamped sidecar imports as nothing at all. This stamps it, in the LF-normalised form: that
    /// is what git stores, so it is the one number that reads the same on every machine, and the
    /// reader accepts either convention anyway.</para>
    ///
    /// <para>⚠️ <b>The rig source itself is never written.</b> <c>docs/art/rigs/**.js</c> is the art
    /// director's lane and read-only to the bake (ADR 0021 §5); the sidecars beside them are that
    /// same lane's OUTPUT, which is what this writes. <c>RigMeshExtractionTests</c> asserts the
    /// <c>.js</c> files are byte-identical after any run.</para>
    /// </summary>
    public static class LobsterVariantSidecarGenerator
    {
        const string SidecarFolder = "docs/art/rigs/gameplay";

        /// <summary>Where the stamp goes: immediately after <c>exportSymbol</c>, so the file reads
        /// rig → provenance → variant, and a diff of two variants lines up.</summary>
        const string StampAfter = "\"exportSymbol\"";

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        [MenuItem("Hidden Harbours/Dev/Generate the 18 lobster-variant deck sidecars", priority = 123)]
        public static void GenerateAll()
        {
            string report = Run(out int written);
            Debug.Log(report);
            EditorUtility.DisplayDialog(
                "Generate lobster-variant sidecars",
                $"{written} sidecar(s) written to {SidecarFolder}.\n\n" +
                "Now run Hidden Harbours ▸ Dev ▸ Import Deck Sidecars to turn them into Defs.\n\n" +
                "Full report in the Console.", "OK");
        }

        /// <summary>Headless entry (-executeMethod).</summary>
        public static void GenerateAllCli()
        {
            try
            {
                Debug.Log(Run(out int written));
                if (written != LobsterVariantFleet.All.Count)
                {
                    Debug.LogError($"[lobster-sidecars] wrote {written}, expected " +
                                   $"{LobsterVariantFleet.All.Count}.");
                    EditorApplication.Exit(1);
                    return;
                }
                Debug.Log("[lobster-sidecars] CLI generate OK.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[lobster-sidecars] CLI generate FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Do the work and hand back the report, so a test or a CI step can run it without
        /// the dialog.</summary>
        public static string Run(out int written)
        {
            written = 0;
            var log = new StringBuilder("[lobster-sidecars] rig generator → gameplay sidecars\n");

            string rigPath = Path.Combine(RepoRoot, LobsterVariantFleet.ScriptPath);
            if (!File.Exists(rigPath))
                throw new FileNotFoundException($"Rig source missing at {rigPath}.", rigPath);

            byte[] rigBytes = File.ReadAllBytes(rigPath);
            string sha = LfNormalisedSha256(rigBytes);
            log.Append($"  rig {LobsterVariantFleet.ScriptPath}\n" +
                       $"  derivedFromRigSha256 (LF-normalised) = {sha}\n");

            string folder = Path.Combine(RepoRoot, SidecarFolder);
            Directory.CreateDirectory(folder);

            // ONE host for all eighteen: the generator is a pure function of its descriptor, and a
            // fresh host per variant would cost eighteen V8 start-ups to prove nothing. (The bake
            // keeps one-rig-per-host for a different reason — a colliding global.)
            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(rigPath));

            string axes = LobsterVariantFleet.AxisSignature(host);
            if (axes != LobsterVariantFleet.ExpectedAxisSignature)
                throw new InvalidOperationException(
                    $"The rig's option axes are now '{axes}', not " +
                    $"'{LobsterVariantFleet.ExpectedAxisSignature}'. Generating the old eighteen " +
                    "would leave the new cells with no sidecar and say nothing. Update " +
                    "LobsterVariantFleet (and the two authored tables) first.");

            foreach (LobsterVariant v in LobsterVariantFleet.All)
            {
                string json = host.EvaluateString(
                    $"JSON.stringify({LobsterVariantFleet.GlobalName}.gameplayGeometry(" +
                    $"{v.JsDescriptor}), null, 2)");

                string stamped = Stamp(json, sha, v);
                string path = Path.Combine(folder, v.SidecarFileName);

                // LF, UTF-8, no BOM. The repo stores these with LF; writing CRLF would make the
                // file's own committed bytes disagree with what everyone else's checkout produces
                // and turn a re-generate into a whole-file diff.
                File.WriteAllText(path, stamped.Replace("\r\n", "\n"), new UTF8Encoding(false));
                written++;
                log.Append($"  ✓ {v.Variant} → {v.SidecarFileName}\n");
            }

            log.Append($"  — {written} written. Next: Hidden Harbours ▸ Dev ▸ Import Deck Sidecars.");
            return log.ToString();
        }

        /// <summary>
        /// Insert the two fields the rig's generator cannot know about, immediately after
        /// <c>exportSymbol</c>. Done as a string splice rather than by re-serialising the object,
        /// so every number in the file is the rig's own <c>JSON.stringify</c> output byte-for-byte
        /// and nothing round-trips through a second float formatter.
        /// </summary>
        static string Stamp(string json, string sha, LobsterVariant v)
        {
            int at = json.IndexOf(StampAfter, StringComparison.Ordinal);
            if (at < 0)
                throw new InvalidOperationException(
                    $"{v.Variant}: the generated sidecar has no {StampAfter} member, so there is no " +
                    "agreed place to stamp provenance. The rig's gameplayGeometry() has changed " +
                    "shape — re-aim this splice deliberately rather than appending somewhere.");

            int eol = json.IndexOf('\n', at);
            if (eol < 0)
                throw new InvalidOperationException($"{v.Variant}: malformed sidecar JSON.");

            var stamp = new StringBuilder();
            stamp.Append("\n  \"derivedFromRigSha256\": \"").Append(sha).Append("\",");
            stamp.Append("\n  \"_provenance\": \"Extracted from the rig's own gameplayGeometry(")
                 .Append(v.Variant)
                 .Append(") by LobsterVariantSidecarGenerator. The rig does not emit ")
                 .Append("derivedFromRigSha256 itself, so this bake stamps the LF-normalised hash ")
                 .Append("of the committed source; an absent hash is refused by DeckSidecarReader ")
                 .Append("exactly like a stale one. Do not hand-edit — re-generate.\",");

            return json.Substring(0, eol) + stamp + json.Substring(eol);
        }

        /// <summary>
        /// SHA-256 of the rig with its line endings normalised to LF — the form git stores, and
        /// therefore the one hash that is the same on Windows and on CI.
        /// </summary>
        public static string LfNormalisedSha256(byte[] rigBytes)
        {
            string lf = Encoding.UTF8.GetString(rigBytes).Replace("\r\n", "\n");
            return DeckSidecarReader.Sha256Hex(Encoding.UTF8.GetBytes(lf));
        }

        /// <summary>The sidecar files this generator owns, absolute — for tests and for the report.
        /// </summary>
        public static IEnumerable<string> OutputPaths
        {
            get
            {
                foreach (LobsterVariant v in LobsterVariantFleet.All)
                    yield return Path.Combine(RepoRoot, SidecarFolder, v.SidecarFileName);
            }
        }
    }
}
