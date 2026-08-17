using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE DUALLY 3500 DROP — does the mesh path reach a LAND vehicle?</b>
    ///
    /// <para>This is a probe, not an acceptance suite. The 2026-08-16 drop is the first ROAD vehicle in
    /// the repo, and every claim about how it bakes has to be measured in the repo's own V8 rather than
    /// read off the rig — this project has shipped mirrored boats five times by reading a rig and
    /// declaring what it meant.</para>
    ///
    /// <para>What it settles, and why each one is not obvious:</para>
    /// <list type="bullet">
    /// <item>The sidecar's <c>derivedFromRigSha256</c> still pins the rig ON DISK, through the repo's
    /// own line-ending rule — the drop ships LF and the working tree checks out CRLF.</item>
    /// <item>The rig has no static <c>F</c> and no top-level <c>MATS</c>: it is a GENERATOR, the same
    /// shape as the lobster pack and the zodiac, so <see cref="RigHullExtraction"/> is the mechanism
    /// and not a new one.</item>
    /// <item>Its <c>MATS</c> is an OBJECT KEYED BY NAME, not an index-ordered array — so the fleet's
    /// "MATS order IS the baked material index" law does NOT transfer, and the default ramp being the
    /// first key (which the packer needs) has to be measured rather than assumed.</item>
    /// </list>
    /// </summary>
    public class DuallyIsoKitProbeTests
    {
        const string RigPath = "docs/art/rigs/dually-iso-kit/vehicleIsoRig.js";
        // ⚠ Under gameplay/vehicles/, NOT gameplay/ — see VehicleRigFleet.SidecarFolder for why
        // (DeckSidecarImportParityTests requires every sidecar in the parent folder to be a boat deck).
        const string SidecarPath =
            "docs/art/rigs/gameplay/vehicles/vehicleIsoRig.dually3500.gameplay.json";
        const string Global = "VehicleIso";

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        // =============================================================================================
        //  1. THE DROP IS ON DISK AND THE SIDECAR STILL PINS IT
        // =============================================================================================

        [Test]
        public void TheRigAndItsSidecarBothLanded()
        {
            FileAssert.Exists(Full(RigPath));
            FileAssert.Exists(Full(SidecarPath));
        }

        /// <summary>
        /// ⭐ <b>The staleness rule, on the first vehicle.</b> An absent or wrong
        /// <c>derivedFromRigSha256</c> is a REFUSAL by design — a sidecar whose polygons were cut from a
        /// different shape is worse than no sidecar. Both failure modes have shipped in this repo's
        /// boat drops (one missing hash, one pinning the wrong rig), so this is measured.
        ///
        /// <para>⚠ <see cref="RigHashMatch.LineEndingNormalized"/> is an ACCEPTED pass, not a
        /// near-miss: the kit ships the rig LF-only and <c>.gitattributes</c> checks <c>.js</c> out
        /// CRLF on Windows, and a line ending cannot move a vertex.</para>
        /// </summary>
        [Test]
        public void TheSidecarStillPinsTheRigOnDisk()
        {
            byte[] rig = File.ReadAllBytes(Full(RigPath));
            string sidecar = File.ReadAllText(Full(SidecarPath));

            string expected = ReadJsonString(sidecar, "derivedFromRigSha256");
            Assert.That(expected, Is.Not.Empty,
                "the sidecar carries no derivedFromRigSha256. An absent hash is a refusal by design — " +
                "ask the art side to re-stamp it rather than reading the polygons anyway.");

            RigHashMatch match = DeckSidecarReader.MatchRigHash(rig, expected, out string actual);

            Assert.That(match, Is.Not.EqualTo(RigHashMatch.None),
                $"the sidecar pins {expected} but the rig on disk hashes to {actual}, and not through a " +
                "line-ending difference either. The truck was reshaped and the sidecar was not " +
                "re-derived — do not trust its geometry.");
        }

        /// <summary>The sidecar names the rig and the body it was cut from, so a reader never has to
        /// infer either from the filename — which matters here, because this drop's sidecar is named
        /// for the CATALOGUE rig plus a body (<c>vehicleIsoRig.dually3500</c>) rather than for one
        /// entity, and the repo's boat sidecars are named the other way.</summary>
        [Test]
        public void TheSidecarNamesItsRigAndItsBodyInsideTheFile()
        {
            string sidecar = File.ReadAllText(Full(SidecarPath));

            Assert.That(ReadJsonString(sidecar, "exportSymbol"), Is.EqualTo(Global));
            Assert.That(ReadJsonString(sidecar, "variant"), Is.EqualTo("dually3500"));
        }

        // =============================================================================================
        //  2. WHAT THE RIG ACTUALLY EXPORTS — measured in the repo's own V8
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The generator finding.</b> Of the five symbols
        /// <see cref="RigMeshSymbols.Required"/> names, this rig exports only the shading three. It has
        /// no static <c>F</c> (its faces come from a private <c>build(state)</c>) and no top-level
        /// <c>MATS</c> (its materials come from a private <c>makeMats(state)</c>).
        ///
        /// <para>That is not a defect and not a new problem: it is exactly the position the lobster
        /// pack and the zodiac are in, which is what <see cref="RigHullExtraction.FaceExpression"/> and
        /// <see cref="RigMeshSymbols.Reconstructions"/> exist for. Pinned so that a later drop which
        /// starts exporting <c>F</c> makes this test fail and the shim gets deleted rather than
        /// quietly outliving its reason.</para>
        /// </summary>
        [Test]
        public void TheRigIsAGeneratorAndExportsNeitherFNorMats()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Full(RigPath)));

            Assert.That(host.EvaluateBool($"typeof {Global} === 'object' && {Global} !== null"), Is.True,
                $"the rig ran but installed no globalThis.{Global}.");

            Assert.That(host.EvaluateBool($"typeof {Global}.F !== 'undefined'"), Is.False,
                "the rig now exports a static F. The RigHullExtraction face expression for the truck " +
                "is a shim around its private build() — delete it and take F instead.");

            Assert.That(host.EvaluateBool($"typeof {Global}.MATS !== 'undefined'"), Is.False,
                "the rig now exports MATS. Delete the Reconstructions entry for vehicleIsoRig.js.");

            foreach (string shading in new[] { "GAIN", "BIAS", "LN" })
                Assert.That(host.EvaluateBool($"typeof {Global}.{shading} !== 'undefined'"), Is.False,
                    $"{shading} is now on the global — the shim that widens it can be narrowed.");

            // What it DOES give us, and what the reconstruction is built on.
            foreach (string entry in new[] { "resolve", "render", "dims", "anchors", "BODIES", "G" })
                Assert.That(host.EvaluateBool($"typeof {Global}.{entry} !== 'undefined'"), Is.True,
                    $"{Global}.{entry} is gone — the vehicle extraction is written against it.");
        }

        /// <summary>
        /// ⭐ <b>The MATS shape finding, and the one that would have bitten silently.</b>
        ///
        /// <para>Every baked hull's <c>MATS</c> is an index-ordered ARRAY, and the fleet's law is that
        /// its order IS the baked material index. This rig's is an OBJECT keyed by name
        /// (<c>MATS[f.mat] || MATS.paint</c>), so that law does not transfer — a bake that assumed it
        /// would recolour the whole truck without failing anything.</para>
        ///
        /// <para>What DOES carry over is the packer's rule that an unknown material name resolves to
        /// index 0, so the default ramp must be the FIRST key. Measured here rather than assumed: the
        /// rig's own fallback is <c>MATS.paint</c>, and <c>paint</c> is first.</para>
        /// </summary>
        [Test]
        public void MatsIsKeyedByNameAndItsDefaultRampIsTheFirstKey()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Full(RigPath)));

            // The reconstruction the bake would install, evaluated through the same widening shim the
            // extractor uses — so this measures the expression that would really run.
            //
            // ⚠ BOTH calls are qualified. The shim widens the private symbols onto the GLOBAL, it does
            // not put them in scope: `VehicleIso.makeMats(resolve({}))` throws
            // "ReferenceError: resolve is not defined", because the inner call is looked up in global
            // scope where the rig's IIFE never published it. Measured, not guessed — the first draft of
            // this probe made exactly that mistake.
            const string mats = "makeMats(" + Global + ".resolve({}))";
            string widened = RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Full(RigPath)), Global, new[] { "makeMats" }, RigPath);

            using IRigScriptHost shimHost = RigScriptHostFactory.Create();
            shimHost.Execute(widened);

            Assert.That(shimHost.EvaluateBool($"typeof {Global}.makeMats === 'function'"), Is.True,
                "the private makeMats could not be widened onto the global — the exported-literal shim " +
                "does not reach it, which is what RigMeshSymbols.InnerWidenings is for.");

            Assert.That(shimHost.EvaluateBool($"Array.isArray({Global}.{mats})"), Is.False,
                "MATS is an ARRAY on this rig now. If the art side moved to the fleet's index-ordered " +
                "table, the vehicle bake should take the ordinary MATS path.");

            Assert.That(shimHost.EvaluateBool($"Object.keys({Global}.{mats})[0] === 'paint'"), Is.True,
                "'paint' is no longer the first key of MATS. The face packer resolves an unknown " +
                "material name to index 0, so the DEFAULT ramp must be first — and this rig's own " +
                "fallback is MATS[f.mat] || MATS.paint. A different first key silently recolours every " +
                "face whose material the packer does not recognise.");
        }

        /// <summary>
        /// ⭐ <b>The bake is VIABLE — the private builder yields a real face list through the shim.</b>
        ///
        /// <para>This is the finding that decides whether the truck is registered as bakeable or as a
        /// refusal. It calls the same private <c>build(resolve({}))</c> a
        /// <see cref="RigHullExtraction.FaceExpression"/> would, and asserts the faces come back in the
        /// shape the packer reads: <c>{v, mat, b, db}</c>, with string material names.</para>
        ///
        /// <para>Face COUNT is deliberately asserted only as "many" — count is not an oracle here (the
        /// repo's own lesson from <c>byId</c> resolving a typo to the first hull), and pinning an exact
        /// number would break on any art revision without meaning anything. The geometry hash is the
        /// oracle, and that belongs to the bake PR, not to this probe.</para>
        /// </summary>
        [Test]
        public void ThePrivateBuilderYieldsFacesThePackerCouldRead()
        {
            string widened = RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Full(RigPath)), Global, new[] { "build" }, RigPath);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(widened);

            Assert.That(host.EvaluateBool($"typeof {Global}.build === 'function'"), Is.True,
                "the private build() could not be widened onto the global.");

            // Qualified for the same reason as MATS above — the shim widens onto the global, it does
            // not bring the rig's privates into scope.
            const string faces = "build(" + Global + ".resolve({}))";

            Assert.That(host.EvaluateBool($"Array.isArray({Global}.{faces})"), Is.True,
                "build(resolve({})) did not return an array of faces.");

            Assert.That(host.EvaluateBool($"{Global}.{faces}.length > 200"), Is.True,
                "the truck built implausibly few faces — a six-wheel crew-cab dually is not a handful " +
                "of quads. Either resolve({}) is not producing a real build or the rig changed shape.");

            Assert.That(host.EvaluateBool(
                    $"{Global}.{faces}.every(f => Array.isArray(f.v) && f.v.length >= 3 && " +
                    "typeof f.mat === 'string' && typeof f.b === 'number' && typeof f.db === 'number')"),
                Is.True,
                "a face came back in a shape the packer does not read — it wants {v, mat, b, db} with " +
                "a STRING material name (this rig keys MATS by name, not by index).");
        }

        // =============================================================================================
        //  helpers
        // =============================================================================================

        /// <summary>Pulls a top-level string value out of the sidecar without taking a JSON dependency
        /// this assembly does not already have. Deliberately dumb: it is reading three known scalar
        /// fields, not parsing the document.</summary>
        static string ReadJsonString(string json, string key)
        {
            string needle = "\"" + key + "\"";
            int at = json.IndexOf(needle, StringComparison.Ordinal);
            if (at < 0) return "";

            int colon = json.IndexOf(':', at + needle.Length);
            if (colon < 0) return "";

            int open = json.IndexOf('"', colon + 1);
            if (open < 0) return "";

            int close = json.IndexOf('"', open + 1);
            return close < 0 ? "" : json.Substring(open + 1, close - open - 1);
        }
    }
}
