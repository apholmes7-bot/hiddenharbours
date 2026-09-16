using System.IO;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE MODERN 3500 — what her rig actually is, measured in the repo's own V8.</b> The sibling
    /// of <see cref="DuallyIsoKitProbeTests"/> and <see cref="OtterIsoKitProbeTests"/>, written for
    /// the same reason: this is an INTAKE, and the point of an intake is to find out what landed
    /// before anything is built on it. Every number below came out of the rig, not out of the drop's
    /// README.
    ///
    /// <para>⭐⭐ <b>She fits the facet shader since her re-issued rig of 2026-09-16, and this suite
    /// is where that is proved rather than asserted.</b> She arrived painting 27 ramps against the
    /// shader's 16 and her bake sat excused in <c>VehicleRigFleet.NotBaked</c>. The re-issue folds
    /// eleven material names onto their neighbours inside her own <c>build()</c>, so the baker reads
    /// 16; the excuse was deleted and she is baked as a mesh only. The three tests that proved the
    /// blocker (<c>HerPaletteDoesNotFitTheFacetShader</c>,
    /// <c>EveryRampSheDeclaresIsPainted_SoTheFilterCannotSaveHer</c>,
    /// <c>TheLosslessFoldsDoNotReachTheCap</c>) were retired with it; the two below measure the
    /// fold that lifted it, so a later re-issue that puts her back over the cap is a red test here
    /// rather than a fleet bake that fails on her.</para>
    ///
    /// <para><b>Why this is trustworthy without an editor bake.</b> The host below is built by the
    /// SAME widening the baker uses — <c>RigMeshExtractor.WidenExportedLiteral</c>, resolving the
    /// registered <c>RigMeshSymbols.Reconstructions</c> entry — so what is counted here is what the
    /// baker would count, not a second implementation that could drift from it.</para>
    /// </summary>
    public class Modern3500KitProbeTests
    {
        const string RigPath = "docs/art/rigs/modern3500-kit/modern3500.rig.js";
        const string Global = "ModernTruck3500";

        /// <summary>The cap is the shader's, and it is quoted rather than read so that widening
        /// <c>HullRampSlots</c> does not silently re-bless a palette nobody re-measured. The second
        /// assertion is what catches the two drifting apart.</summary>
        const int ShaderCap = 16;

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        /// <summary>
        /// Loads the rig through the baker's own shim: her private <c>build</c> widened onto the
        /// global, and <c>MATS</c> supplied by the registered reconstruction — the rig declares no
        /// module-level <c>MATS</c>, so without that entry this line is the
        /// <c>ReferenceError: MATS is not defined</c> that a bake of her produced on 2026-09-14.
        /// <c>makeMats</c> is widened too, plainly, so the fold can be counted against the table she
        /// DECLARES as well as the one the reconstruction keeps.
        /// </summary>
        static IRigScriptHost BuilderHost()
        {
            string widened = RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Full(RigPath)), Global, new[] { "MATS", "build", "makeMats" }, RigPath);

            IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(widened);
            host.Execute(@"
                function __faces(o){ return ModernTruck3500.build(ModernTruck3500.resolve(o||{})); }
                function __usedMaterialCount(o){
                  var f = __faces(o||{}), used = {};
                  for (var i = 0; i < f.length; i++) used[f[i].mat] = 1;
                  return Object.keys(used).length;
                }
                // The table the registered reconstruction keeps: what the baker packs.
                function __keptMaterialCount(){
                  return Object.keys(ModernTruck3500.MATS).length;
                }
                function __firstKeptMaterial(){
                  return Object.keys(ModernTruck3500.MATS)[0];
                }
                // The table her makeMats DECLARES, before anything filters it.
                function __declaredMaterialCount(){
                  return Object.keys(ModernTruck3500.makeMats(ModernTruck3500.resolve({}))).length;
                }
                function __declaredButUnpainted(){
                  var f = __faces({}), used = {}, out = [];
                  var M = ModernTruck3500.makeMats(ModernTruck3500.resolve({}));
                  for (var i = 0; i < f.length; i++) used[f[i].mat] = 1;
                  for (var k in M) if (!used[k]) out.push(k);
                  return out.sort().join(',');
                }
                function __faceCount(){ return __faces({}).length; }");
            return host;
        }

        // =========================================================================================
        //  1. THE SHIM — why a bake of her threw before anything about colour mattered
        // =========================================================================================

        /// <summary>
        /// ⭐ <b>Her <c>MATS</c> is RECONSTRUCTED, not exported, and the bake that discovered it did
        /// so the hard way.</b> <c>RigMeshSymbols.Required</c> asks every vehicle for five symbols;
        /// she carries <c>F</c>, <c>GAIN</c>, <c>BIAS</c> and <c>LN</c> at module scope but builds her
        /// material table per-pose through <c>makeMats(s)</c>, so there is no <c>MATS</c> binding for
        /// the shim to publish and <c>host.Execute</c> threw <c>ReferenceError: MATS is not
        /// defined</c>.
        ///
        /// <para>This is the regression test for that, and it is deliberately the FIRST thing here:
        /// the error was a symptom, it looked like the blocker, and it was not. The blocker was her
        /// palette, and section 2 measures the fold that lifted it.</para>
        /// </summary>
        [Test]
        public void HerMatsIsReconstructed_AndTheWidenedRigExecutes()
        {
            Assert.That(RigMeshSymbols.IsReconstructed(RigPath, "MATS"), Is.True,
                "no MATS reconstruction is registered for modern3500.rig.js. Without it the shim " +
                "widens the exported literal to publish a binding the rig never declared, and the " +
                "bake dies on a ReferenceError before it reaches anything about her geometry. " +
                "⚠️ Do NOT fix that by editing docs/art/rigs/** — that is the art director's source.");

            using IRigScriptHost host = BuilderHost();   // throws if the reconstruction is wrong

            Assert.That(host.EvaluateBool($"typeof {Global}.MATS === 'object'"), Is.True,
                "the reconstruction ran but did not yield a material table.");
            Assert.That(host.EvaluateBool($"typeof {Global}.build === 'function'"), Is.True,
                "her face builder did not survive the widening, so nothing below is measuring faces.");
        }

        // =========================================================================================
        //  2. THE FOLD — 27 ramps to 16, upstream, inside her own build()
        // =========================================================================================

        /// <summary>
        /// ⭐⭐ <b>SHE FITS THE FACET SHADER: 16 ramps by day, 15 by night, against
        /// <c>_RampMeta</c>'s <c>float4[16]</c>.</b> Her re-issued rig (2026-09-16) carries an
        /// eleven-entry fold table inside <c>build()</c>, so the faces the baker reads already name
        /// the folded material. Before it she painted 27, eleven over — the Otter's blocker again.
        ///
        /// <para>Replaces <c>HerPaletteDoesNotFitTheFacetShader</c>, which pinned 27 used and
        /// used &gt; 16. Pinned EXACTLY rather than as a ceiling, both ways: a count that comes down
        /// is a re-issued rig nobody measured, and one that goes up is a fitting that gained a
        /// material — and at 16 she has no room for one.</para>
        ///
        /// <para>The night count is the day's minus her day-only <c>head</c> lens: the night pass
        /// swaps it for <c>led</c>, which the day build already paints. The face count is pinned
        /// alongside because the fold touched no geometry — 2802 before and after — and a different
        /// face list would be a different truck, not a recolour.</para>
        /// </summary>
        [Test]
        public void HerFoldedPaletteFitsTheFacetShader()
        {
            Assert.That(HullMeshDef.HullRampSlots, Is.EqualTo(ShaderCap),
                "the shader's ramp table was resized. Every count in this suite was measured " +
                "against 16 — re-measure rather than editing the number here.");

            using IRigScriptHost host = BuilderHost();

            Assert.That(host.EvaluateNumber("__faceCount()"), Is.EqualTo(2802d),
                "her face count changed, so this is not the rig these numbers were measured on.");

            double used = host.EvaluateNumber("__usedMaterialCount({})");
            Assert.That(used, Is.EqualTo(16d),
                $"she paints {used} ramps by day, not 16. Above 16 the fleet bake refuses her again: " +
                "put the refusal and its measurement back on a VehicleRigFleet.NotBaked entry and " +
                "ask the art side for the fold — never a repo-side edit under docs/art/rigs/**.");

            Assert.That(host.EvaluateNumber("__usedMaterialCount({night:true})"), Is.EqualTo(15d),
                "her night build no longer paints 15 ramps. The day-only `head` lens is the gap; " +
                "if the two now agree, the night pass stopped being a swap.");

            Assert.That(used, Is.LessThanOrEqualTo((double)ShaderCap),
                "she no longer fits the facet shader.");
        }

        /// <summary>
        /// ⭐ <b>She still DECLARES 27; the fold left eleven names declared and unpainted, and the
        /// registered filter drops exactly those.</b> The re-issue added no material and renamed
        /// none — every folded name stays in <c>makeMats</c>, which costs nothing, because the
        /// reconstruction keeps only the ramps some face actually names.
        ///
        /// <para>Replaces <c>EveryRampSheDeclaresIsPainted_SoTheFilterCannotSaveHer</c>, which pinned
        /// 27 declared and none unpainted: the filter could not save her then, and it is what packs
        /// her 16 now. The eleven are the fold table's sources, named, so a re-issue that folds a
        /// DIFFERENT set is a red test rather than the same count by coincidence. <c>paint</c> stays
        /// first in what is kept, and that order is load-bearing: index 0 is where the face packer
        /// sends an unknown material name.</para>
        /// </summary>
        [Test]
        public void TheFoldLeavesElevenNamesDeclaredButUnpainted_AndTheFilterDropsThem()
        {
            using IRigScriptHost host = BuilderHost();

            Assert.That(host.EvaluateNumber("__declaredMaterialCount()"), Is.EqualTo(27d),
                "her declared material count changed — the fold was supposed to add and rename " +
                "nothing.");

            Assert.That(host.EvaluateString("__declaredButUnpainted()"),
                Is.EqualTo("alloy,cover,dash,mirror,plate,reflect,reverse,screen,seatInsert,sidewall,stepGrip"),
                "the names she declares but no face paints are not the eleven her fold table folds.");

            Assert.That(host.EvaluateNumber("__keptMaterialCount()"), Is.EqualTo(16d),
                "the registered MATS reconstruction no longer keeps exactly the 16 ramps she paints.");

            Assert.That(host.EvaluateString("__firstKeptMaterial()"), Is.EqualTo("paint"),
                "`paint` is no longer index 0 of the kept table, and index 0 is where an unknown " +
                "material name lands.");
        }
    }
}
