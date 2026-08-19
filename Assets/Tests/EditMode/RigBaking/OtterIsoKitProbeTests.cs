using System.IO;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE OTTER 8x8 — what her rig actually is, measured in the repo's own V8 (ADR 0035).</b>
    /// The sibling of <see cref="DuallyIsoKitProbeTests"/>, and written for the same reason: this is
    /// an INTAKE, and the point of an intake is to find out what landed before anything is built on
    /// it. Every number below came out of the rig rather than out of the handoff that described it.
    ///
    /// <para>She is the second vehicle and the first amphibian. Her bake is BLOCKED — see
    /// <c>VehicleRigFleet.NotBaked</c> — on a palette that does not fit the facet shader, which is
    /// the one finding here that no vehicle-side change can resolve.</para>
    /// </summary>
    public class OtterIsoKitProbeTests
    {
        const string RigPath = "docs/art/rigs/otter-iso-kit/amphibIsoRig.js";
        const string ContractPath = "docs/art/rigs/otter-iso-kit/otter8x8.contract.json";

        const string SidecarPath =
            VehicleRigFleet.SidecarFolder + "/amphibIsoRig.otter8x8.gameplay.json";

        const string Global = "AmphibIso";

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        static string ReadJsonString(string json, string key)
        {
            int at = json.IndexOf("\"" + key + "\"", System.StringComparison.Ordinal);
            if (at < 0) return "";
            int colon = json.IndexOf(':', at);
            int open = json.IndexOf('"', colon + 1);
            int close = json.IndexOf('"', open + 1);
            return open < 0 || close < 0 ? "" : json.Substring(open + 1, close - open - 1);
        }

        // =============================================================================================
        //  1. THE DROP LANDED, AND IT PINS ITS OWN RIG
        // =============================================================================================

        [Test]
        public void TheRigItsSidecarAndItsContractAllLanded()
        {
            foreach (string p in new[] { RigPath, SidecarPath, ContractPath })
                Assert.That(File.Exists(Full(p)), Is.True, $"{p} is missing from the drop.");
        }

        /// <summary>
        /// The sidecar's <c>derivedFromRigSha256</c> still matches the rig on disk. An absent hash is
        /// a refusal by design; a mismatched one means the machine was reshaped and the sidecar was
        /// not re-derived, so none of its geometry can be trusted.
        ///
        /// <para>⚠ A line-ending-normalised match is an ACCEPTED pass, not a near miss: the kit ships
        /// LF-only and <c>.gitattributes</c> checks <c>.js</c> out CRLF on Windows, and a line ending
        /// cannot move a vertex.</para>
        /// </summary>
        [Test]
        public void TheSidecarStillPinsTheRigOnDisk()
        {
            byte[] rig = File.ReadAllBytes(Full(RigPath));
            string expected = ReadJsonString(File.ReadAllText(Full(SidecarPath)), "derivedFromRigSha256");

            Assert.That(expected, Is.Not.Empty,
                "the sidecar carries no derivedFromRigSha256. An absent hash is a refusal by design — " +
                "ask the art side to re-stamp it rather than reading the polygons anyway.");

            RigHashMatch match = DeckSidecarReader.MatchRigHash(rig, expected, out string actual);
            Assert.That(match, Is.Not.EqualTo(RigHashMatch.None),
                $"the sidecar pins {expected} but the rig on disk hashes to {actual}, and not through " +
                "a line-ending difference either.");
        }

        /// <summary>Her CONTRACT pins the same rig as her sidecar — two independently written
        /// documents agreeing is a real cross-check, and it is how a half-updated drop is caught.</summary>
        [Test]
        public void TheContractAndTheSidecarPinTheSameRig()
        {
            string fromSidecar = ReadJsonString(File.ReadAllText(Full(SidecarPath)), "derivedFromRigSha256");
            string fromContract = ReadJsonString(File.ReadAllText(Full(ContractPath)), "rigSha256");

            Assert.That(fromContract, Is.EqualTo(fromSidecar).IgnoreCase,
                "her contract and her sidecar pin DIFFERENT rig hashes, so at least one of them was " +
                "generated against a different build of the machine.");
        }

        [Test]
        public void TheSidecarNamesItsRigAndItsBodyInsideTheFile()
        {
            string sidecar = File.ReadAllText(Full(SidecarPath));
            Assert.That(ReadJsonString(sidecar, "exportSymbol"), Is.EqualTo(Global));
            Assert.That(ReadJsonString(sidecar, "variant"), Is.EqualTo("otter8x8"));
        }

        // =============================================================================================
        //  2. THE KIND TOKEN — three spellings of one idea
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Her sidecar and her rig disagree about her own kind, and BOTH disagree with the ruled
        /// name.</b> Sidecar <c>amphibious_xtv</c>, rig body record <c>amphib_xtv</c>, ruled value
        /// <c>amphibious_vehicle</c>.
        ///
        /// <para>Pinned as a FACT ABOUT THE DROP, not as a complaint: the coordinator ruled (on #549)
        /// that the shipped tokens are accepted as shipped and the translation lives in code. If a
        /// later drop unifies them, this test fails and the aliases in
        /// <c>VehicleKinds</c> can be retired rather than quietly outliving their reason.</para>
        /// </summary>
        [Test]
        public void HerSidecarAndHerRigSpellHerKindDifferently_AndBothAreMapped()
        {
            string fromSidecar = ReadJsonString(File.ReadAllText(Full(SidecarPath)), "kind");
            Assert.That(fromSidecar, Is.EqualTo("amphibious_xtv"),
                "her sidecar's shipped kind token changed.");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Full(RigPath)));
            string fromRig = host.EvaluateString($"{Global}.BODIES.otter8x8.kind");

            Assert.That(fromRig, Is.EqualTo("amphib_xtv"), "her rig's body kind changed.");
            Assert.That(fromRig, Is.Not.EqualTo(fromSidecar),
                "the two now AGREE — good news. Retire whichever alias in VehicleKinds is now dead " +
                "rather than leaving it to rot.");

            foreach (string token in new[] { fromSidecar, fromRig })
                Assert.That(HiddenHarbours.Core.VehicleKinds.TryFromToken(token, out var k) &&
                            k == HiddenHarbours.Core.VehicleKind.AmphibiousVehicle, Is.True,
                    $"'{token}' does not map to the amphibian.");
        }

        // =============================================================================================
        //  3. WHAT THE RIG ACTUALLY DOES — measured, never read off the handoff
        // =============================================================================================

        /// <summary>The same generator shape as the Dually: no static <c>F</c>, no top-level
        /// <c>MATS</c>, faces from a private <c>build(state)</c>. So the extraction path that already
        /// serves the lobster pack, the zodiac and the Dually reaches her too.</summary>
        [Test]
        public void SheIsAGeneratorOfExactlyTheShapeTheExtractorAlreadyServes()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Full(RigPath)));

            Assert.That(host.EvaluateBool($"typeof {Global} === 'object' && {Global} !== null"), Is.True);
            Assert.That(host.EvaluateBool($"typeof {Global}.F !== 'undefined'"), Is.False,
                "she now exports a static F — the generator shim can be retired for her.");
            Assert.That(host.EvaluateBool($"typeof {Global}.MATS !== 'undefined'"), Is.False);
            Assert.That(host.EvaluateBool($"typeof {Global}.BODIES.otter8x8 === 'object'"), Is.True);
            Assert.That(host.EvaluateBool($"typeof {Global}.anchors === 'function'"), Is.True,
                "no anchors() means no analytic azimuth oracle, and the silhouette taper is " +
                "meaningless on a machine this blunt.");
        }

        /// <summary>Loads the rig with its private <c>build</c> widened onto the global — the shim
        /// <see cref="RigMeshExtractor"/> itself uses, not a second mechanism.</summary>
        static IRigScriptHost BuilderHost()
        {
            string widened = RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Full(RigPath)), Global, new[] { "build" }, RigPath);

            IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(widened);
            host.Execute(@"
                function __faces(o){ return AmphibIso.build(AmphibIso.resolve(o)); }
                function __moved(pose){
                  var a = __faces({}), b = __faces(pose), n = 0;
                  for (var i = 0; i < a.length; i++) {
                    var p = a[i].v, q = b[i].v, d = false;
                    for (var k = 0; k < p.length && !d; k++)
                      for (var c = 0; c < 3; c++)
                        if (Math.abs(p[k][c] - q[k][c]) > 1e-9) { d = true; break; }
                    if (d) n++;
                  }
                  return n;
                }
                function __sideMoved(pose, sgn){
                  var a = __faces({}), b = __faces(pose), n = 0;
                  for (var i = 0; i < a.length; i++) {
                    var p = a[i].v, q = b[i].v, d = false;
                    for (var k = 0; k < p.length && !d; k++)
                      for (var c = 0; c < 3; c++)
                        if (Math.abs(p[k][c] - q[k][c]) > 1e-9) { d = true; break; }
                    if (!d) continue;
                    var cx = 0;
                    for (var m = 0; m < p.length; m++) cx += p[m][0];
                    cx /= p.length;
                    if (sgn < 0 ? cx < 0 : cx > 0) n++;
                  }
                  return n;
                }
                // Max deviation of every moved vertex from ONE common offset. 0 = a pure rigid
                // translation, so the pose can be a runtime transform rather than a second bake.
                function __translationDeviation(pose){
                  var a = __faces({}), b = __faces(pose), dx = null, worst = 0;
                  for (var i = 0; i < a.length; i++) {
                    var p = a[i].v, q = b[i].v;
                    for (var k = 0; k < p.length; k++) {
                      var d = [q[k][0]-p[k][0], q[k][1]-p[k][1], q[k][2]-p[k][2]];
                      if (Math.abs(d[0])<1e-12 && Math.abs(d[1])<1e-12 && Math.abs(d[2])<1e-12) continue;
                      if (dx === null) dx = d;
                      for (var c = 0; c < 3; c++) worst = Math.max(worst, Math.abs(d[c]-dx[c]));
                    }
                  }
                  return dx === null ? -1 : worst;
                }
                function __translationZ(pose){
                  var a = __faces({}), b = __faces(pose);
                  for (var i = 0; i < a.length; i++)
                    for (var k = 0; k < a[i].v.length; k++) {
                      var d = b[i].v[k][2] - a[i].v[k][2];
                      if (Math.abs(d) > 1e-12) return d;
                    }
                  return 0;
                }
                function __usedMaterialCount(o){
                  var f = __faces(o||{}), used = {};
                  for (var i = 0; i < f.length; i++) used[f[i].mat] = 1;
                  return Object.keys(used).length;
                }");
            return host;
        }

        /// <summary>
        /// ⭐ <b>SKID STEER: no <c>steer</c> axis at all, and the two roll sides are perfectly
        /// disjoint.</b> She turns by driving her tracks at different speeds, which is why the
        /// Dually's Ackermann machinery has nothing to say about her.
        ///
        /// <para>188 faces a side, and every one of them on that side — so lifting her wheels out at
        /// bake time is a cleaner split than the Dually's was (hers needed a steer/roll intersection
        /// to be untangled).</para>
        /// </summary>
        [Test]
        public void SheIsSkidSteered_WithTwoPerfectlyDisjointRollSides()
        {
            using IRigScriptHost host = BuilderHost();

            Assert.That(host.EvaluateBool($"{Global}.resolve({{}}).steer !== undefined"), Is.False,
                "she now has a `steer` axis. She is skid-steered — if the art side gave her steering " +
                "geometry, the controller plan changes.");

            Assert.That(host.EvaluateNumber("__moved({rollL:0.25})"), Is.EqualTo(188d),
                "her left-side roll group changed size.");
            Assert.That(host.EvaluateNumber("__moved({rollR:0.25})"), Is.EqualTo(188d));

            // Each side's group is ENTIRELY on that side — the split needs no disambiguation.
            Assert.That(host.EvaluateNumber("__sideMoved({rollL:0.25},1)"), Is.EqualTo(0d),
                "her LEFT roll axis moved geometry on the right. The bake split assumes it does not.");
            Assert.That(host.EvaluateNumber("__sideMoved({rollR:0.25},-1)"), Is.EqualTo(0d));

            Assert.That(host.EvaluateNumber($"{Global}.G.revPerYaw45"), Is.EqualTo(0.2461d).Within(1e-9),
                "revPerYaw45 is how many wheel revolutions one 45° skid turn costs — the number the " +
                "controller will solve her turning against, as the Dually solves against Ackermann.");
        }

        /// <summary>
        /// <c>yaw</c> moves ZERO faces — a camera parameter, exactly as the Dually's is. So she reads
        /// at any heading between the eight facings for free, and baking yaw into vertices would turn
        /// her twice.
        /// </summary>
        [Test]
        public void YawIsACameraParameterSoItMovesNoGeometry()
        {
            using IRigScriptHost host = BuilderHost();
            Assert.That(host.EvaluateNumber("__moved({yaw:30})"), Is.EqualTo(0d),
                "yaw moved geometry. If the art side made it a real rotation, the mesh path would " +
                "turn her twice — once in the vertices and once through HeadingDirUnits.");
        }

        /// <summary>
        /// ⭐ <b><c>float</c> is an EXACT RIGID TRANSLATION</b> — <c>[0, 0, −0.52]</c> at full, linear
        /// in between, with a max per-vertex deviation of 0. So floating her is a runtime Z offset on
        /// the DRY mesh: no second bake, no afloat variant, no reshape.
        ///
        /// <para>That is the single most useful fact about her for the amphibious work, and it is the
        /// kind of thing that must be measured — "she sinks when she swims" would just as easily have
        /// been a hull deformation.</para>
        /// </summary>
        [Test]
        public void FloatingHerIsAPureZTranslation_SoTheDryMeshServesBothStates()
        {
            using IRigScriptHost host = BuilderHost();

            Assert.That(host.EvaluateNumber("__translationDeviation({float:1})"), Is.EqualTo(0d).Within(1e-12),
                "float is no longer a rigid translation — some vertices moved differently from " +
                "others, so the dry mesh can no longer stand in for the floating one.");

            Assert.That(host.EvaluateNumber("__translationZ({float:1})"),
                Is.EqualTo(-0.52d).Within(1e-9), "her full-float sink changed.");
            Assert.That(host.EvaluateNumber($"{Global}.G.sinkMax"), Is.EqualTo(0.52d).Within(1e-9));

            // Linear, so a partial float is a partial offset and nothing has to be interpolated.
            Assert.That(host.EvaluateNumber("__translationZ({float:0.5})"),
                Is.EqualTo(-0.26d).Within(1e-9));
        }

        /// <summary>
        /// ⚠️ <b><c>tracks:true</c> is a DIFFERENT BUILD, not a pose</b> — the face count changes
        /// 1296 → 1224. A bake that treated it as a pose axis would compare face lists of different
        /// lengths and either crash or, worse, mis-assign geometry by index.
        /// </summary>
        [Test]
        public void TheTrackedVariantIsASeparateBuild_NotAPose()
        {
            using IRigScriptHost host = BuilderHost();

            Assert.That(host.EvaluateNumber("__faces({}).length"), Is.EqualTo(1296d),
                "her wheeled face count changed.");
            Assert.That(host.EvaluateNumber("__faces({tracks:true}).length"), Is.EqualTo(1224d),
                "her tracked face count changed.");
        }

        /// <summary>
        /// ⭐⭐ <b>SHE FITS THE FACET SHADER — in every build the game can place, and in none of the
        /// canopy ones.</b> <c>_RampMeta</c> is a <c>float4[16]</c>, and this pins both halves.
        ///
        /// <para><b>What changed.</b> She painted SEVENTEEN ramps from #558 until 2026-08-19 and was
        /// unbakeable for it. The trick that saved the Dually (17 declared / 16 used) and the zodiac
        /// (18 / 14) never applied to her — she USED all seventeen — so the fix had to be an art
        /// merge, and the art side made it: the cockpit <c>mat</c> is folded into <c>mesh</c>, one
        /// face recoloured by ≤ 3/255. She now measures 16.</para>
        ///
        /// <para>⚠️ <b>The canopy builds are still over, and that is deliberate.</b> Fitting
        /// <c>screen</c> or <c>bimini</c> brings in <c>canvas</c> and <c>glass</c>. Measured: either
        /// alone is 17, both together is 18 — and "both" is the shipped <c>harbourHaul</c> PRESET.
        /// The base machine is what bakes; a canopy Otter needs its own ruling. Pinned here so that
        /// the day someone fits one, this says why it cannot be baked rather than leaving them to
        /// rediscover it at a bake.</para>
        ///
        /// <para>Both halves are asserted because either drifting is a real event: a 17th ramp added
        /// to the base machine makes her unplaceable again, and a canopy that quietly came under the
        /// cap is a blocker that should be lifted.</para>
        /// </summary>
        [Test]
        public void HerPaletteFitsTheFacetShader_ExceptWithACanopyFitted()
        {
            using IRigScriptHost host = BuilderHost();

            const int ShaderCap = 16;

            // Every build the game can place her in — including the two the sidecar drives
            // (tracks, float) and the two that only restyle her (night, hatch).
            foreach (string build in new[] { "{}", "{tracks:true}", "{float:1}", "{night:1}",
                                             "{hatch:1}", "{rack:false,winch:false}" })
            {
                double used = host.EvaluateNumber($"__usedMaterialCount({build})");
                Assert.That(used, Is.EqualTo((double)ShaderCap),
                    $"build {build} paints {used} ramps, not {ShaderCap}. Over the cap she is " +
                    "unplaceable (VehicleMeshDef.IsUsable refuses her); under it, something stopped " +
                    "painting. Either way the bake needs re-measuring, not a nudged number.");
            }

            // The canopy builds. Named individually because they differ: one fitting is 17, both are 18.
            foreach ((string build, double expected) in new[]
                     {
                         ("{screen:true}", 17d),
                         ("{bimini:true}", 17d),
                         ("{screen:true,bimini:true}", 18d),
                         ("AmphibIso.PRESETS.harbourHaul", 18d),
                     })
            {
                double used = host.EvaluateNumber($"__usedMaterialCount({build})");
                Assert.That(used, Is.EqualTo(expected),
                    $"canopy build {build} paints {used} ramps, expected {expected}. If it came " +
                    $"DOWN to {ShaderCap} the canopy blocker is lifted — bake her canopy variant and " +
                    "delete this half. If it went up, a fitting gained a material.");
                Assert.That(used, Is.GreaterThan((double)ShaderCap),
                    $"canopy build {build} now fits the shader. Lift the blocker deliberately " +
                    "rather than letting a bake discover it.");
            }
        }
    }
}
