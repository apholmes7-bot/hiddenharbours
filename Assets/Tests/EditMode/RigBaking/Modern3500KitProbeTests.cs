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
    /// <para>⭐⭐ <b>She does not fit the facet shader, and this suite is where that is proved rather
    /// than asserted.</b> Her bake is excused in <c>VehicleRigFleet.NotBaked</c>; that entry carries
    /// the argument and this carries the measurement behind it. The day an art fold brings her to 16,
    /// <see cref="HerPaletteDoesNotFitTheFacetShader"/> goes red and says so — which is the whole
    /// reason the blocker is written as a live measurement instead of a comment.</para>
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
        /// </summary>
        static IRigScriptHost BuilderHost()
        {
            string widened = RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Full(RigPath)), Global, new[] { "MATS", "build" }, RigPath);

            IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(widened);
            host.Execute(@"
                function __faces(o){ return ModernTruck3500.build(ModernTruck3500.resolve(o||{})); }
                function __usedMaterialCount(o){
                  var f = __faces(o||{}), used = {};
                  for (var i = 0; i < f.length; i++) used[f[i].mat] = 1;
                  return Object.keys(used).length;
                }
                function __declaredMaterialCount(){
                  return Object.keys(ModernTruck3500.MATS).length;
                }
                function __unusedMaterials(){
                  var f = __faces({}), used = {}, out = [];
                  for (var i = 0; i < f.length; i++) used[f[i].mat] = 1;
                  for (var k in ModernTruck3500.MATS) if (!used[k]) out.push(k);
                  return out.join(',');
                }
                // The lossless fold: two ramps may share a slot only if they resolve to the SAME
                // colours and the same polish. Anything coarser recolours a face.
                function __distinctRamps(alias){
                  var M = ModernTruck3500.MATS, seen = {}, n = 0;
                  for (var k in M) {
                    var key = alias && alias[k] ? alias[k] : k;
                    var m = M[key] || M[k];
                    var sig = JSON.stringify(m.ramp) + '|p' +
                              (m.polish === undefined ? '-' : m.polish);
                    if (!seen[sig]) { seen[sig] = 1; n++; }
                  }
                  return n;
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
        /// the error was a symptom, it looked like the blocker, and it was not. Everything below is
        /// the actual blocker.</para>
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
        //  2. THE BLOCKER — 27 ramps against 16 slots
        // =========================================================================================

        /// <summary>
        /// ⭐⭐ <b>SHE DOES NOT FIT THE FACET SHADER, and no vehicle-side change can make her.</b>
        /// <c>_RampMeta</c> is a <c>float4[16]</c>; she paints 27. This is the Otter's blocker again
        /// (#558 until the art merge of 2026-08-19) and it is ELEVEN over rather than one.
        ///
        /// <para>⚠️ <b>Asserted in both directions, and the upward one matters most.</b> The day a
        /// fold brings her to 16 this test goes red, and that red is the signal to bake her and
        /// delete her <c>VehicleRigFleet.NotBaked</c> entry — which is how a blocker gets lifted
        /// deliberately rather than rediscovered by a fleet bake that suddenly exits 0.</para>
        ///
        /// <para>The face count is pinned alongside because the two move together: a fold that
        /// changed the face list would be a different truck, not a recolour, and her contract and
        /// sidecar both pin the rig that produced this number.</para>
        /// </summary>
        [Test]
        public void HerPaletteDoesNotFitTheFacetShader()
        {
            Assert.That(HullMeshDef.HullRampSlots, Is.EqualTo(ShaderCap),
                "the shader's ramp table was resized. Every count in this suite was measured " +
                "against 16 — re-measure rather than editing the number here.");

            using IRigScriptHost host = BuilderHost();

            Assert.That(host.EvaluateNumber("__faceCount()"), Is.EqualTo(2802d),
                "her face count changed, so this is not the rig these numbers were measured on.");

            double used = host.EvaluateNumber("__usedMaterialCount({})");

            Assert.That(used, Is.EqualTo(27d),
                $"she paints {used} ramps, not 27. If it came DOWN, say so on her NotBaked entry " +
                "and re-run the fold arithmetic below; if it went UP, a fitting gained a material.");

            Assert.That(used, Is.GreaterThan((double)ShaderCap),
                "she now fits the facet shader. Lift the blocker DELIBERATELY: bake her, wire her " +
                "def, and delete her VehicleRigFleet.NotBaked entry — entries there leave by " +
                "deletion, never by rewording.");
        }

        /// <summary>
        /// ⭐⭐ <b>THE FILTER BUYS NOTHING — she declares 27 and uses all 27.</b> This is the finding
        /// that decides the whole shape of her intake, so it is measured rather than inferred.
        ///
        /// <para>The registered reconstruction keeps only the ramps some face actually names. That
        /// trick is what saved the older Dually (17 declared, 16 used) and the zodiac (18 / 14). Here
        /// there is not one orphan to drop — exactly the Otter's position, and for the same reason
        /// her fix had to come from the art side.</para>
        /// </summary>
        [Test]
        public void EveryRampSheDeclaresIsPainted_SoTheFilterCannotSaveHer()
        {
            using IRigScriptHost host = BuilderHost();

            Assert.That(host.EvaluateNumber("__declaredMaterialCount()"), Is.EqualTo(27d),
                "her declared material count changed.");

            string unused = host.EvaluateString("__unusedMaterials()");
            Assert.That(unused, Is.Empty,
                $"she now declares ramps no face paints ({unused}). That is HEADROOM the filter " +
                "takes for free — re-measure the used count, because the blocker may be smaller " +
                "than her NotBaked entry says.");
        }

        /// <summary>
        /// ⭐⭐ <b>What a lossless fold actually buys: 27 → 19, and 18 with one judgement call.</b>
        /// This is the arithmetic the art ask rests on, kept here so the ask is a number rather than
        /// "please use fewer colours".
        ///
        /// <para><b>Free (27 → 19):</b> six groups resolve to a byte-identical ramp AND polish, so
        /// merging them cannot move a pixel — <c>bright</c>+<c>chrome</c>,
        /// <c>badge</c>+<c>plate</c>+<c>reverse</c>, <c>iron</c>+<c>cover</c>,
        /// <c>galv</c>+<c>alloy</c>, <c>rubber</c>+<c>dash</c>+<c>stepGrip</c>,
        /// <c>reflect</c>+<c>head</c>.</para>
        ///
        /// <para><b>Nearly free (19 → 18):</b> <c>sidewall</c> carries the same ramp as
        /// <c>rubber</c> and differs only by <c>polish</c> .05 against none — a specular difference
        /// on a tyre wall, which is a judgement the art side makes, not this test.</para>
        ///
        /// <para><b>Still two over.</b> The cheapest remaining merges by face count are
        /// <c>screen</c> (1 face), <c>mirror</c> (2), <c>seatInsert</c> (10), <c>hoodAccent</c> (10)
        /// and <c>bedliner</c> (14) — all of which change visible colour. ⚠️ And every one of them
        /// rewrites <c>modern3500.rig.js</c> and so MOVES the rig hash her contract and her sidecar
        /// pin: it arrives as a re-issued drop, never as an edit under <c>docs/art/rigs/**</c>.</para>
        /// </summary>
        [Test]
        public void TheLosslessFoldsDoNotReachTheCap()
        {
            using IRigScriptHost host = BuilderHost();

            Assert.That(host.EvaluateNumber("__distinctRamps(null)"), Is.EqualTo(19d),
                "the free fold no longer lands on 19 distinct ramp/polish signatures. Two ramps " +
                "sharing a slot must be byte-identical in BOTH — if this moved, the palette moved.");

            Assert.That(host.EvaluateNumber("__distinctRamps({sidewall:'rubber'})"), Is.EqualTo(18d),
                "folding `sidewall` into `rubber` no longer lands on 18, so their ramps diverged " +
                "by more than polish and that merge is no longer nearly free.");

            Assert.That(host.EvaluateNumber("__distinctRamps({sidewall:'rubber'})"),
                        Is.GreaterThan((double)ShaderCap),
                "every lossless fold now fits the shader. Ask the art side to land it, then bake " +
                "her and delete her NotBaked entry.");
        }
    }
}
