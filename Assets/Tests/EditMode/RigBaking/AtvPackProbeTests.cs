using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE ATV PACK — the first machines in the repo you sit ASTRIDE (owner drop 2026-09-06).</b>
    ///
    /// <para>An enduro bike, a three-wheeler and a utility quad, off ONE rig and ONE sidecar. The
    /// owner's words: <i>"These will be used in recreation by residents. Some use them as
    /// transportation. It's the only used transportation on St. Peter's island other than
    /// boats."</i></para>
    ///
    /// <para><b>Everything here is measured against the RIG, in the repo's own V8.</b> A test that
    /// restated the fleet table's numbers would be a second transcription agreeing with the first,
    /// which is the failure mode this project has shipped five mirrored boats through.</para>
    ///
    /// <para>The three things this pack got wrong for free, and where each is pinned:</para>
    /// <list type="number">
    ///   <item><b>The bike bakes PARKED.</b> <c>render(d,{body:'dirtbike'})</c> at rest is
    ///   <c>stand:1</c> — leaned 12° onto her side stand — so a mesh taken from the default is a bike
    ///   NOBODY CAN RIDE, and it looks entirely correct in every still.
    ///   <see cref="TheBikesBodyIsTheRiddenState"/>.</item>
    ///   <item><b>An unknown body renders a QUAD, to zero pixels.</b> The same family as
    ///   <c>Crustacean2.render(unknownKind)</c> → lobster. <see cref="AnUnknownBodyFallsBackToTheQuad_Silently"/>.</item>
    ///   <item><b>Her steer is not a rotation about the vertical.</b> The bike's and the trike's
    ///   whole front assembly turns about the RAKED axis through the head, so the fleet's
    ///   <c>SteerAndRoll</c> arm cannot draw them. <see cref="TheSteerIsNotFlatOnTheSingleTrackBodies"/>.</item>
    /// </list>
    /// </summary>
    public class AtvPackProbeTests
    {
        const string RigPath = "docs/art/rigs/atv-pack/atvIsoRig.js";
        const string ContractPath = "docs/art/rigs/atv-pack/atvPack.contract.json";
        const string SidecarPath =
            "docs/art/rigs/gameplay/vehicles/atvIsoRig.atvPack.gameplay.json";
        const string Global = "AtvIso";

        /// <summary>The facet shader's <c>_RampMeta</c> is a <c>float4[16]</c>.</summary>
        const int ShaderRampCap = 16;

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The three registered bodies, as (fleet key, rig body id).</summary>
        public static readonly string[][] Bodies =
        {
            new[] { "enduro250", "dirtbike" },
            new[] { "trike200", "trike" },
            new[] { "utilityQuad", "quad" },
        };

        public static IEnumerable<string> Keys => Bodies.Select(b => b[0]);

        /// <summary>
        /// A host with the rig widened exactly as the baker widens it, plus the rest-pose machinery
        /// the articulation split uses — so what these tests measure is what the bake measured.
        ///
        /// <para>⚠️ The rest pose is the vehicle's OWN, which on the bike carries <c>stand:0</c> and
        /// on all three carries the body. <c>resolve({})</c> is the QUAD on this rig.</para>
        /// </summary>
        static IRigScriptHost Host(string key)
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(key);
            IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Full(v.ScriptPath)), v.GlobalName,
                new[] { "build", "makeMats" }, v.ScriptPath));

            host.Execute($@"
                var __R = {v.GlobalName};
                function __base(){{ return {v.RestPose}; }}
                function __pose(o){{ var s = __base(); for (var k in o) s[k] = o[k]; return s; }}
                function __faces(o){{ return __R.build(__R.resolve(__pose(o))); }}
                function __moved(pose){{
                  var A = __faces({{}}), B = __faces(pose), out = [];
                  for (var i = 0; i < A.length; i++) {{
                    var p = A[i].v, q = B[i].v, d = false;
                    for (var k = 0; k < p.length && !d; k++)
                      for (var c = 0; c < 3; c++) if (p[k][c] !== q[k][c]) {{ d = true; break; }}
                    if (d) out.push(i);
                  }}
                  return out;
                }}
                // Every vertex of the faces a pose moves, rest and posed, flattened — the whole set,
                // never the moved subset. A helper that skips unmoved vertices calls a telescope
                // rigid, which is how the trailers' landing gear nearly shipped as one mesh.
                function __pairs(pose){{
                  var A = __faces({{}}), B = __faces(pose), idx = __moved(pose), out = [];
                  for (var n = 0; n < idx.length; n++) {{
                    var p = A[idx[n]].v, q = B[idx[n]].v;
                    for (var k = 0; k < p.length; k++)
                      out.push(p[k][0], p[k][1], p[k][2], q[k][0], q[k][1], q[k][2]);
                  }}
                  return out.join(',');
                }}
                function __mats(o){{
                  var F = __faces(o||{{}}), m = {{}};
                  for (var i = 0; i < F.length; i++) m[F[i].mat] = 1;
                  return Object.keys(m).sort().join(',');
                }}");
            return host;
        }

        static double[] Doubles(string csv) =>
            csv.Length == 0
                ? Array.Empty<double>()
                : csv.Split(',').Select(s => double.Parse(s, Inv)).ToArray();

        // =============================================================================================
        //  1. THE DROP LANDED, AND ITS THREE STAMPS AGREE
        // =============================================================================================

        [Test]
        public void TheKitAndItsSidecarBothLanded()
        {
            FileAssert.Exists(Full(RigPath));
            FileAssert.Exists(Full(ContractPath));
            FileAssert.Exists(Full(SidecarPath));
            FileAssert.Exists(Full("docs/art/rigs/atv-pack/_atvKit.js"));
            FileAssert.Exists(Full("docs/art/rigs/atv-pack/harness.html"));
            FileAssert.Exists(Full("docs/art/rigs/atv-pack/README.md"));
            FileAssert.Exists(Full("docs/art/rigs/atv-pack/SHA256SUMS.txt"));

            Assert.That(Directory.GetFiles(Full("docs/art/rigs/atv-pack/reference"), "*.png").Length,
                Is.EqualTo(22),
                "the drop ships 22 reference sheets. They are REFERENCE only — these three bake as " +
                "mesh — but a kit that lost half its sheets lost half its evidence.");
        }

        /// <summary>
        /// ⭐⭐ <b>The sidecar's stamp, the contract's stamp and the rig's own hash are ONE number,
        /// and it is compared as a FULL 64-character digest.</b>
        ///
        /// <para>A prefix check is not a hash check: the hightop van's bad stamp shared its rig's
        /// first SIXTEEN hex digits and diverged over the remaining 48, and it reached the lane as
        /// "verified" because the staging pass compared eight characters. That is the law this
        /// asserts, on the one drop where all three numbers are available to cross-check.</para>
        ///
        /// <para>⚠️ <see cref="RigHashMatch.LineEndingNormalized"/> is an accepted pass: the kit ships
        /// LF and <c>core.autocrlf</c> checks <c>.js</c> out CRLF on Windows. A line ending cannot
        /// move a vertex.</para>
        /// </summary>
        [Test]
        public void TheSidecarAndTheContractPinTheSameRig_ByFullDigest()
        {
            byte[] rig = File.ReadAllBytes(Full(RigPath));
            string sidecar = File.ReadAllText(Full(SidecarPath));
            string contract = File.ReadAllText(Full(ContractPath));

            string fromSidecar = QuotedAfter(sidecar, "derivedFromRigSha256");
            string fromContract = QuotedAfter(contract, "rigSha256");

            Assert.That(fromSidecar, Has.Length.EqualTo(64),
                "the sidecar's stamp is not a full sha256. A short stamp cannot be compared, and a " +
                "prefix comparison is exactly what let the van's corrupt stamp through.");
            Assert.That(fromContract, Is.EqualTo(fromSidecar),
                "the contract and the sidecar pin DIFFERENT rigs. One of the two was regenerated " +
                "without the other; do not read either one's geometry.");

            RigHashMatch match = DeckSidecarReader.MatchRigHash(rig, fromSidecar, out string actual);
            Assert.That(match, Is.Not.EqualTo(RigHashMatch.None),
                $"the pack pins {fromSidecar} but {RigPath} hashes to {actual}, and not through a " +
                "line-ending difference. Do NOT re-stamp it here — docs/art/rigs/** is the art " +
                "director's lane; register the refusal in VehicleRigFleet.SidecarHashRefused and " +
                "send the re-stamp upstream.");
        }

        static string QuotedAfter(string json, string key)
        {
            int at = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (at < 0) return "";
            int open = json.IndexOf('"', json.IndexOf(':', at) + 1);
            int close = json.IndexOf('"', open + 1);
            return close < 0 ? "" : json.Substring(open + 1, close - open - 1);
        }

        // =============================================================================================
        //  2. THE RIG SAYS WHAT THE FLEET TABLE SAYS IT SAYS
        // =============================================================================================

        [Test]
        public void TheRigPublishesTheThreeBodiesInOneSharedCell()
        {
            using IRigScriptHost host = Host("enduro250");

            Assert.That(host.EvaluateString($"{Global}.list().join(',')"),
                Is.EqualTo("dirtbike,trike,quad"),
                "the rig's body list moved. Every registered Pick has to name one of these, and an " +
                "unknown pick does NOT throw on this rig — it bakes a quad.");

            Assert.That(host.EvaluateNumber($"{Global}.W"), Is.EqualTo(256d));
            Assert.That(host.EvaluateNumber($"{Global}.H"), Is.EqualTo(192d));
            Assert.That(host.EvaluateNumber($"{Global}.pivot.x"), Is.EqualTo(128d));
            Assert.That(host.EvaluateNumber($"{Global}.pivot.y"), Is.EqualTo(128d));
            Assert.That(host.EvaluateNumber($"{Global}.PX"), Is.EqualTo(32d));
            Assert.That(host.EvaluateNumber($"{Global}.DIRS"), Is.EqualTo(8d));
            Assert.That(host.EvaluateNumber($"{Global}.defaultElev"), Is.EqualTo(40d));
            Assert.That(host.EvaluateString($"{Global}.order.join(' ')"),
                Is.EqualTo("N NE E SE S SW W NW"),
                "⚠️ facing 0 is N and shows the TAIL — the machine points away.");
        }

        /// <summary>
        /// Every registered entry names the rig's own body, and every one of the FIVE places the pick
        /// has to reach actually carries it. On a container rig whose resolver falls back, a missing
        /// pick is not an error — it is a plausible quad under another machine's name.
        /// </summary>
        [Test]
        public void EveryAtvEntryCarriesItsPickEverywhereItMatters([ValueSource(nameof(Bodies))] string[] body)
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(body[0]);

            Assert.That(v.Pick, Is.EqualTo(body[1]));
            Assert.That(v.SidecarBodyScope, Is.EqualTo(body[1]),
                "the sidecar scope is what selects her collider, her saddle and her interactions.");
            StringAssert.Contains($"body:'{body[1]}'", v.Extraction.FaceExpression);
            StringAssert.Contains($"body:'{body[1]}'", v.Extraction.ViewOptions);
            StringAssert.Contains($"body:'{body[1]}'", v.RestPose);
            Assert.That(v.ScriptPath, Is.EqualTo(RigPath));
            Assert.That(v.SidecarPath, Is.EqualTo(SidecarPath));
            Assert.That(v.GlobalName, Is.EqualTo(Global));
        }

        /// <summary>
        /// ⚠️⚠️ <b>THE BIKE'S BODY IS THE RIDDEN STATE.</b> Her rest pose carries <c>stand:0</c>, and
        /// the two states are genuinely different machines: 497 faces against 502, and 1204 pixels
        /// apart at SE. A bake from the rig's own default would have produced a bike leaned 12° onto
        /// a stand, permanently, and nothing downstream would have said a word.
        /// </summary>
        [Test]
        public void TheBikesBodyIsTheRiddenState()
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get("enduro250");
            StringAssert.Contains("stand:0", v.RestPose);
            StringAssert.Contains("stand:0", v.Extraction.FaceExpression);
            StringAssert.Contains("stand:0", v.Extraction.ViewOptions);

            using IRigScriptHost host = Host("enduro250");
            Assert.That(host.EvaluateNumber($"__faces({{}}).length"), Is.EqualTo(497d),
                "the ridden bike's face count moved.");
            Assert.That(host.EvaluateNumber($"{Global}.build({Global}.resolve({{body:'dirtbike'}})).length"),
                Is.EqualTo(502d),
                "the PARKED bike's face count moved. The five extra faces are her deployed stand.");

            Assert.That(host.EvaluateNumber($"{Global}.dims({{body:'dirtbike'}}).leanDeg"),
                Is.EqualTo(-12d),
                "the rig's own default is stand 1 — leaned 12° onto the stand. If this ever reads 0 " +
                "the trap is gone and the rest pose can drop its stand:0; until then it is load-bearing.");
            Assert.That(host.EvaluateNumber($"{Global}.dims({{body:'dirtbike',stand:0}}).leanDeg"),
                Is.EqualTo(0d), "the ridden bike stands upright.");
        }

        /// <summary>
        /// ⭐⭐ <b>THE NEGATIVE CONTROL, and it is the whole reason the pick is asserted five times
        /// above.</b> An unknown body id does not throw on this rig — <c>resolve</c> returns the
        /// QUAD, and the quad it returns is a real quad, pixel for pixel. So a mistyped pick produces
        /// a plausible machine and nothing downstream can catch it.
        ///
        /// <para>Ported from the drop's own trap list rather than invented here, and the same family
        /// as <c>trailerIsoRig</c>'s fallback to <c>reefer53</c> and the sport fisher's <c>byId</c>.</para>
        /// </summary>
        [Test]
        public void AnUnknownBodyFallsBackToTheQuad_Silently()
        {
            using IRigScriptHost host = Host("utilityQuad");

            Assert.That(host.EvaluateString($"{Global}.resolve({{body:'NONSENSE'}}).body"),
                Is.EqualTo("quad"),
                "an unknown body no longer resolves to the quad. If the rig now THROWS, this trap " +
                "is gone and the five pick assertions can relax — check before relaxing them.");

            double junk = host.EvaluateNumber(
                $"{Global}.build({Global}.resolve({{body:'NONSENSE'}})).length");
            double quad = host.EvaluateNumber(
                $"{Global}.build({Global}.resolve({{body:'quad'}})).length");
            double bike = host.EvaluateNumber(
                $"{Global}.build({Global}.resolve({{body:'dirtbike'}})).length");

            Assert.That(junk, Is.EqualTo(quad), "the fallback is not the quad any more.");
            Assert.That(junk, Is.Not.EqualTo(bike),
                "…and it is not the bike, which is what makes the fallback silent AND plausible.");
        }

        // =============================================================================================
        //  3. THE ARTICULATION — measured, then declared
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>The bike's and the trike's steer is NOT a rotation about the vertical, and this is
        /// the measurement that forced a new motion arm.</b>
        ///
        /// <para>A rotation about a vertical axis preserves every vertex's z EXACTLY. The quad's does:
        /// max |Δz| at full lock is 0 over all her moved vertices, so she stays on the fleet's
        /// <c>SteerAndRoll</c>. The bike's and the trike's do not — their whole front assembly turns
        /// about the raked axis through the head — and the shift is 3.5 px and 2.7 px at this kit's
        /// 32 px/m. Posing them flat would draw a front wheel climbing out of the road at lock.</para>
        /// </summary>
        [Test]
        public void TheSteerIsNotFlatOnTheSingleTrackBodies(
            [Values("enduro250", "trike200")] string key)
        {
            using IRigScriptHost host = Host(key);
            double[] p = Doubles(host.EvaluateString("__pairs({steer:1})"));
            Assert.That(p.Length, Is.GreaterThan(0), "steer:1 moved nothing.");

            double maxDz = 0;
            for (int i = 0; i < p.Length; i += 6)
                maxDz = Math.Max(maxDz, Math.Abs(p[i + 5] - p[i + 2]));

            Assert.That(maxDz, Is.GreaterThan(0.05),
                $"'{key}': her steer now preserves z, which is what a rotation about the VERTICAL " +
                "does. If the art un-raked her head, AxisSteerAndRoll/AxisSteerOnly can retire for " +
                "her and SteerAndRoll will draw her correctly — take that deliberately.");
        }

        /// <summary>The quad's, for contrast, and it is the reason she is NOT on the new arm.</summary>
        [Test]
        public void TheQuadsSteerIsExactlyFlat()
        {
            using IRigScriptHost host = Host("utilityQuad");
            double[] p = Doubles(host.EvaluateString("__pairs({steer:1})"));
            Assert.That(p.Length, Is.GreaterThan(0));

            double maxDz = 0;
            for (int i = 0; i < p.Length; i += 6)
                maxDz = Math.Max(maxDz, Math.Abs(p[i + 5] - p[i + 2]));

            Assert.That(maxDz, Is.EqualTo(0d),
                "the quad's kingpins are vertical and her bars turn about a vertical stem, so every " +
                "vertex her steer moves keeps its z EXACTLY. A non-zero here means she has grown a " +
                "rake and belongs on the AxisSteer arms with the other two.");
        }

        /// <summary>
        /// ⭐⭐ <b>ONE rotation about the declared axis through the declared pivot reproduces the
        /// rig's whole steer — using Unity's own <see cref="Quaternion.AngleAxis"/>, which is what
        /// the driver will apply.</b>
        ///
        /// <para>This is the assertion that makes the new arms honest rather than plausible. It is
        /// deliberately run through the SAME call the runtime makes, at the SAME lock the fleet
        /// declares, about the SAME pivot the baked fitting carries — so a sign, an axis or a pivot
        /// that drifts fails here rather than in a frame nobody screenshots.</para>
        ///
        /// <para>Measured 2026-09-06 in the standalone harness at 2.7e-16 m (bike, 908 vertices) and
        /// 3.4e-16 m (trike, 1152). The bar is float-generous because the transcription runs through
        /// <c>float</c> here and <c>double</c> in the rig
        /// (<c>bit-equality-is-unattainable-between-two-transcriptions</c>).</para>
        /// </summary>
        [Test]
        public void OneRotationAboutTheDeclaredAxisReproducesTheWholeSteer(
            [Values("enduro250", "trike200")] string key)
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(key);
            VehicleRigFleet.Axis fork = v.Axes.Single(a => a.Slot == "ForkF");

            using IRigScriptHost host = Host(key);
            double[] p = Doubles(host.EvaluateString("__pairs({steer:1})"));

            Quaternion q = Quaternion.AngleAxis(fork.SteerLockDegrees, fork.SteerAxisLocal.normalized);
            float worst = 0f;
            for (int i = 0; i < p.Length; i += 6)
            {
                var rest = new Vector3((float)p[i], (float)p[i + 1], (float)p[i + 2]);
                var posed = new Vector3((float)p[i + 3], (float)p[i + 4], (float)p[i + 5]);
                Vector3 predicted = fork.Pivot + q * (rest - fork.Pivot);
                worst = Mathf.Max(worst, (predicted - posed).magnitude);
            }

            Assert.That(worst, Is.LessThan(1e-4f),
                $"'{key}': Quaternion.AngleAxis({fork.SteerLockDegrees}, {fork.SteerAxisLocal}) about " +
                $"{fork.Pivot} does NOT reproduce the rig's own full-lock pose — worst vertex " +
                $"{worst:E3} m over {p.Length / 6} vertices. The declared axis, the lock or the pivot " +
                "has drifted from the rig, and a fork posed on a wrong axis looks right at rest.");
        }

        /// <summary>
        /// Both single-track bodies' raked axis IS the rig's own <c>axisOf(P)</c>, re-derived from
        /// <c>SPECS.&lt;body&gt;.rake</c> rather than compared against the number that was typed.
        /// </summary>
        [Test]
        public void TheRakedAxisIsTheRigsOwn([Values("enduro250", "trike200")] string key)
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(key);
            using IRigScriptHost host = Host(key);

            double rake = host.EvaluateNumber($"{Global}.SPECS.{v.Pick}.rake");
            var expected = new Vector3(0f,
                                       -Mathf.Sin((float)rake * Mathf.Deg2Rad),
                                       Mathf.Cos((float)rake * Mathf.Deg2Rad));

            foreach (VehicleRigFleet.Axis a in v.Axes.Where(x => x.SteerLockDegrees != 0f))
            {
                Assert.That((a.SteerAxisLocal - expected).magnitude, Is.LessThan(1e-6f),
                    $"'{key}' axis '{a.Slot}' declares {a.SteerAxisLocal}; the rig's rake is " +
                    $"{rake}°, which is {expected}.");

                double lock_ = host.EvaluateNumber(
                    $"{Global}.steer.{(v.Pick == "dirtbike" ? "dirtbike" : "trike")}.maxDeg");
                Assert.That(a.SteerLockDegrees, Is.EqualTo((float)lock_).Within(1e-4f),
                    $"'{key}' axis '{a.Slot}' declares a {a.SteerLockDegrees}° lock; the rig " +
                    $"publishes {lock_}°.");
            }
        }

        /// <summary>The quad's bars turn about the VERTICAL stem at their OWN lock, which is not her
        /// wheels'. The 2° gap is why they are a fitting rather than a knuckle.</summary>
        [Test]
        public void TheQuadsBarsTurnAtTheirOwnLockAboutTheStem()
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get("utilityQuad");
            VehicleRigFleet.Axis bars = v.Axes.Single(a => a.Slot == "Bars");

            using IRigScriptHost host = Host("utilityQuad");
            Assert.That(bars.SteerAxisLocal, Is.EqualTo(new Vector3(0f, 0f, 1f)));
            Assert.That(bars.SteerLockDegrees,
                Is.EqualTo((float)host.EvaluateNumber($"{Global}.steer.quad.barsMaxDeg")).Within(1e-4f));
            Assert.That(bars.Pivot.x, Is.EqualTo(0f));
            Assert.That(bars.Pivot.y,
                Is.EqualTo((float)host.EvaluateNumber($"{Global}.SPECS.quad.stem[1]")).Within(1e-4f));

            double inner = host.EvaluateNumber($"{Global}.steer.quad.innerMaxDeg");
            double outer = host.EvaluateNumber($"{Global}.steer.quad.outerMaxDeg");
            Assert.That(bars.SteerLockDegrees, Is.Not.EqualTo((float)inner),
                "her bars and her inner wheel now agree, so the separate fitting has lost its " +
                "reason — check before merging them, and mind that a Centre fitting on the " +
                $"Ackermann solve takes the OUTER angle ({outer}°), not the inner.");
        }

        /// <summary>
        /// Every declared pivot is a point the RIG publishes, and on the single-track bodies it is a
        /// point ON the steering axis — which is what lets one point serve both the steer and the
        /// roll.
        /// </summary>
        [Test]
        public void EveryDeclaredPivotIsTheRigsOwnNumber([ValueSource(nameof(Bodies))] string[] body)
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(body[0]);
            using IRigScriptHost host = Host(body[0]);
            string b = body[1];

            float axF = (float)host.EvaluateNumber($"{Global}.SPECS.{b}.axF");
            float axR = (float)host.EvaluateNumber($"{Global}.SPECS.{b}.axR");
            float rF = (float)host.EvaluateNumber(
                $"(function(P){{return P.rF != null ? P.rF : P.r;}})({Global}.SPECS.{b})");
            float rR = (float)host.EvaluateNumber(
                $"(function(P){{return P.rR != null ? P.rR : P.r;}})({Global}.SPECS.{b})");

            foreach (VehicleRigFleet.Axis a in v.Axes)
            {
                if (a.Slot.StartsWith("WheelF", StringComparison.Ordinal) ||
                    a.Slot.StartsWith("KnuckleF", StringComparison.Ordinal) ||
                    a.Slot == "ForkF")
                {
                    Assert.That(a.Pivot.y, Is.EqualTo(axF).Within(1e-4f),
                        $"'{a.Slot}' does not turn about the FRONT axle station.");
                    Assert.That(a.Pivot.z, Is.EqualTo(rF).Within(1e-4f),
                        $"'{a.Slot}' does not turn about the front hub height.");
                }
                else if (a.Slot.StartsWith("WheelR", StringComparison.Ordinal))
                {
                    Assert.That(a.Pivot.y, Is.EqualTo(axR).Within(1e-4f));
                    Assert.That(a.Pivot.z, Is.EqualTo(rR).Within(1e-4f));
                }
            }
        }

        /// <summary>
        /// The face split the fleet declares is the split the rig produces — every probe's claim
        /// counted in the repo's own V8, disjointly, adding to what the master axes move.
        /// </summary>
        [Test]
        public void TheProbesClaimWhatTheFleetSaysTheyClaim([ValueSource(nameof(Bodies))] string[] body)
        {
            using IRigScriptHost host = Host(body[0]);

            double roll = host.EvaluateNumber("__moved({roll:0.25}).length");
            double rollF = host.EvaluateNumber("__moved({rollF:0.25}).length");
            double rollR = host.EvaluateNumber("__moved({rollR:0.25}).length");

            Assert.That(rollF + rollR, Is.EqualTo(roll),
                $"'{body[0]}': the master roll no longer equals the two per-axle claims, so one of " +
                "them has started overlapping the other or missing geometry. BodyMustNotMove's " +
                "{roll:0.25} probe is what would catch the leftover — this says WHERE.");

            double steer = host.EvaluateNumber("__moved({steer:1}).length");
            Assert.That(steer, Is.GreaterThan(rollF),
                "the steer claims the front wheel AND the assembly around it; if it claimed less " +
                "than the wheel alone the ordering in VehicleRigFleet has been reversed.");
        }

        /// <summary>
        /// ⭐ The trike's rear suspension is a MEASURED zero — <c>susR</c> moves nothing at all,
        /// which is the class's fact (a rigid axle on balloon tyres) and not an omission.
        /// </summary>
        [Test]
        public void TheTrikesRearSuspensionIsAMeasuredZero()
        {
            using IRigScriptHost host = Host("trike200");
            Assert.That(host.EvaluateNumber("__moved({susR:1}).length"), Is.EqualTo(0d),
                "the trike's susR now moves faces. Her rig has grown a rear spring — her def's " +
                "SuspensionTravelRearMeters would need to stop being 0.");
            Assert.That(host.EvaluateNumber($"{Global}.SPECS.trike.TR"), Is.EqualTo(0d));

            Assert.That(host.EvaluateNumber("__moved({susF:1}).length"), Is.GreaterThan(0d),
                "…and her FRONT still moves, so the probe is alive and the zero above means what " +
                "it says.");
        }

        // =============================================================================================
        //  4. THE RAMP TABLE
        // =============================================================================================

        /// <summary>
        /// The reconstructed <c>MATS</c> fits the facet shader and starts with the ramp the packer
        /// resolves an unknown material to. Measured over EVERY body, because a table filtered at one
        /// of them drops the others' ramps — which is a silent re-paint, not an error.
        /// </summary>
        [Test]
        public void TheRampTableIsTheUnionOverEveryBody_AndFitsTheShader()
        {
            using IRigScriptHost host = Host("utilityQuad");

            string used = host.EvaluateString(
                $"(function(){{var u={{}};for(var b in {Global}.SPECS){{" +
                $"var F={Global}.build({Global}.resolve({{body:b}}));" +
                "for(var i=0;i<F.length;i++)u[F[i].mat]=1;}return Object.keys(u).sort().join(',');})()");

            Assert.That(used, Is.EqualTo("alloy,chrome,cloth,galv,head,iron,lensR,paint,rubber"),
                "the set of materials the three bodies' faces name has changed. The reconstruction " +
                "in RigMeshSymbols filters to exactly this set, so a new one is a new ramp — check " +
                "it against the shader's 16 before re-baking.");

            string table = host.EvaluateString(
                $"(function(){{var M={Global}.makeMats({Global}.resolve({{}})),u={{}},o=[];" +
                $"for(var b in {Global}.SPECS){{var F={Global}.build({Global}.resolve({{body:b}}));" +
                "for(var i=0;i<F.length;i++)u[F[i].mat]=1;}" +
                "for(var k in M)if(u[k])o.push(k);return o.join(',');})()");

            Assert.That(table.Split(',').Length, Is.LessThanOrEqualTo(ShaderRampCap),
                $"the reconstructed table is {table.Split(',').Length} ramps against a float4[16].");
            Assert.That(table.Split(',')[0], Is.EqualTo("paint"),
                "'paint' is no longer first. Order is load-bearing: the face packer resolves an " +
                "unknown material name to index 0, and the rig's own MATS[f.mat] || MATS.paint " +
                "fallback agrees with 'paint' being it.");
        }

        // =============================================================================================
        //  5. WHAT ACTUALLY BAKED
        // =============================================================================================

        static VehicleMeshDef LoadMesh(string key)
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(key);
            var def = UnityEditor.AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(v.MeshAssetPath);
            Assert.That(def, Is.Not.Null,
                $"{v.MeshAssetPath} did not load. Re-run " +
                "Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Bake ALL road-vehicle meshes.");
            return def;
        }

        [Test]
        public void EachBodyBakedAUsableMeshAtTheSharedCell([ValueSource(nameof(Keys))] string key)
        {
            VehicleMeshDef def = LoadMesh(key);
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(key);

            Assert.That(def.Id, Is.EqualTo(v.MeshId), "her mesh id is append-only.");
            Assert.That(def.IsUsable(), Is.True,
                $"'{key}' baked a def that is not usable — she would be REFUSED at install and never " +
                "drawn. The usual cause is a ramp count over the facet shader's 16; this pack uses 9.");
            Assert.That(def.Ramps, Has.Length.LessThanOrEqualTo(ShaderRampCap));

            Assert.That(def.CellW, Is.EqualTo(256), "all three share the Otter's 256×192 cell.");
            Assert.That(def.CellH, Is.EqualTo(192));
            Assert.That(def.PivotPx, Is.EqualTo(new Vector2(128f, 128f)));
            Assert.That(def.PxPerMetre, Is.EqualTo(32));
            Assert.That(def.AzimuthCounterClockwise, Is.True,
                "both azimuth oracles measured counter-clockwise on all three bodies — the abeam " +
                "pair at exactly −90.00° and the centreline pair with the nose WEST at the cell " +
                "labelled 'E'. A clockwise bake drives her backwards at E and W.");
        }

        [Test]
        public void EachBodyBakedTheFittingsSheDeclares([ValueSource(nameof(Bodies))] string[] body)
        {
            VehicleMeshDef def = LoadMesh(body[0]);
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(body[0]);

            Assert.That(def.Wheels.Select(w => w.Slot),
                Is.EquivalentTo(v.Axes.Select(a => a.Slot)),
                $"'{body[0]}' baked a different set of fittings than her table declares.");

            foreach (VehicleFitment f in def.Wheels)
            {
                Assert.That(f.Prop, Is.Not.Null, $"'{f.Slot}' baked no mesh.");
                Assert.That(f.Prop.IsUsable(), Is.True, $"'{f.Slot}' baked an unusable prop.");

                VehicleRigFleet.Axis a = v.Axes.Single(x => x.Slot == f.Slot);
                Assert.That(f.Motion, Is.EqualTo(a.Motion));
                Assert.That(f.Side, Is.EqualTo(a.Side));
                Assert.That(f.Prop.PivotLocalMeters, Is.EqualTo(a.Pivot));

                // ⚠️ The two new arms are the only ones that carry an axis, and they must carry a
                // REAL one — the driver refuses a zero rather than substituting vertical.
                if (f.TurnsAboutADeclaredAxis)
                {
                    Assert.That(f.SteerAxisNormalised, Is.Not.EqualTo(Vector3.zero),
                        $"'{f.Slot}' takes {f.Motion} with no declared axis. The driver throws on " +
                        "this rather than posing a raked fork flat — re-bake her.");
                    Assert.That(f.SteerLockDegrees, Is.Not.EqualTo(0f),
                        $"'{f.Slot}' takes {f.Motion} with a zero lock, so she would never turn.");
                    Assert.That(f.Prop.MaxSteerDegrees,
                        Is.EqualTo(Mathf.Abs(a.SteerLockDegrees)).Within(1e-4f),
                        $"'{f.Slot}' carries her own lock on the fitment but not on the prop.");
                }
                else
                {
                    Assert.That(f.SteerAxisLocal, Is.EqualTo(Vector3.zero),
                        $"'{f.Slot}' is not an AxisSteer fitting but carries a steer axis. Zero on " +
                        "everything else is what keeps every previously-baked vehicle untouched.");
                }
            }
        }

        /// <summary>
        /// ⭐ The chassis on the baked def is the rig's, including the two measurements that look
        /// like placeholders and are not: a ZERO front track on the single-track bodies, and the REAR
        /// wheel radius on all three.
        /// </summary>
        [Test]
        public void TheChassisIsTheRigsOwn([ValueSource(nameof(Bodies))] string[] body)
        {
            VehicleMeshDef def = LoadMesh(body[0]);
            using IRigScriptHost host = Host(body[0]);
            string b = body[1];

            double axF = host.EvaluateNumber($"{Global}.SPECS.{b}.axF");
            double axR = host.EvaluateNumber($"{Global}.SPECS.{b}.axR");
            double rR = host.EvaluateNumber(
                $"(function(P){{return P.rR != null ? P.rR : P.r;}})({Global}.SPECS.{b})");

            Assert.That(def.WheelbaseMeters, Is.EqualTo((float)(axF - axR)).Within(1e-4f));
            Assert.That(def.WheelRadiusMeters, Is.EqualTo((float)rR).Within(1e-4f),
                "⚠️ the REAR radius. The rig's roll is revolutions of the rear wheel and its " +
                "distancePerRev is 2πrR, so an odometer solved on rF turns every wheel at the wrong " +
                "rate against the ground she covers.");
            Assert.That(def.SuspensionTravelFrontMeters,
                Is.EqualTo((float)host.EvaluateNumber($"{Global}.SPECS.{b}.TF")).Within(1e-4f));
            Assert.That(def.SuspensionTravelRearMeters,
                Is.EqualTo((float)host.EvaluateNumber($"{Global}.SPECS.{b}.TR")).Within(1e-4f));

            if (b == "quad")
            {
                Assert.That(def.FrontTrackMeters,
                    Is.EqualTo((float)host.EvaluateNumber($"{Global}.SPECS.quad.wheelX * 2")).Within(1e-4f));
                Assert.That(def.MaxInnerSteerDegrees,
                    Is.EqualTo((float)host.EvaluateNumber($"{Global}.steer.quad.innerMaxDeg")).Within(1e-3f));
                Assert.That(def.MaxOuterSteerDegrees,
                    Is.EqualTo((float)host.EvaluateNumber($"{Global}.steer.quad.outerMaxDeg")).Within(1e-3f));
                Assert.That(def.MaxOuterSteerDegrees, Is.LessThan(def.MaxInnerSteerDegrees),
                    "a real Ackermann pair: the inner wheel takes the larger angle.");
            }
            else
            {
                Assert.That(def.FrontTrackMeters, Is.EqualTo(0f),
                    "⚠️ a MEASURED zero, not a placeholder: one front wheel on the centreline has no " +
                    "Ackermann partner, and AckermannDegrees with a zero track returns outer == " +
                    "inner, which is what a single front wheel does.");
                Assert.That(def.MaxOuterSteerDegrees, Is.EqualTo(def.MaxInnerSteerDegrees),
                    "the same wheel is both the inner and the outer.");
            }
        }

        /// <summary>
        /// ⭐⭐ <b><c>ShowsDriver</c> is TRUE on all three, and it comes from the SIDECAR.</b> That is
        /// the whole point of the saddle arm in <see cref="VehicleSidecarFacts"/>: before it, an
        /// absent <c>drive</c> interaction was recorded as an ABSENCE and the bake proceeded with no
        /// door, no seat and nobody able ever to get on — a bake that succeeds and produces a machine
        /// that cannot be ridden.
        /// </summary>
        [Test]
        public void EveryAtvShowsHerRider_FromTheSidecar([ValueSource(nameof(Bodies))] string[] body)
        {
            VehicleMeshDef def = LoadMesh(body[0]);

            Assert.That(def.ShowsDriver, Is.True,
                $"'{body[0]}' hides her rider. There is nothing on these three to be INSIDE — the " +
                "way in is a leg over the saddle — so a hidden rider is a machine nobody appears on.");

            VehicleSidecarFacts facts = VehicleSidecarFacts.Read(
                File.ReadAllText(Full(SidecarPath)), SidecarPath, body[1]);
            Assert.That(facts.Errors, Is.Empty);
            Assert.That(facts.HasDriverSeat, Is.True);
            Assert.That(def.DriverSeatLocal, Is.EqualTo(facts.DriverSeatLocal),
                "the baked seat is not the one her sidecar publishes.");
            Assert.That(def.DriveDoorLocal, Is.EqualTo(facts.DriveDoorLocal),
                "the baked reach point is not her sidecar's.");
            Assert.That(def.HasAltDriveDoor, Is.True,
                "both sides of a saddle machine are published; the alt reach point is the other one.");
            Assert.That(def.AltDriveDoorLocal, Is.EqualTo(facts.AltDriveDoorLocal));
            Assert.That(def.AltDriveDoorLocal.x, Is.EqualTo(-def.DriveDoorLocal.x).Within(1e-4f),
                "the two sides are mirrored about the centreline, as the art derives them " +
                "(width/2 + 0.55 m outboard at the seat reference).");
        }

        /// <summary>She sinks, and it is a MEASURED zero: the pack's own <c>_excluded</c> block says
        /// <i>"these do not swim. No FLOAT section; the Otter is the amphibian."</i></summary>
        [Test]
        public void NoAtvFloats([ValueSource(nameof(Keys))] string key)
        {
            VehicleMeshDef def = LoadMesh(key);
            Assert.That(def.Floats, Is.False,
                "an ATV started floating. Her sidecar publishes no FLOAT block at all, so this is " +
                "an absence the reader turned into a deliberate zero — a true here means something " +
                "wrote flotation numbers nobody published.");

            string json = File.ReadAllText(Full(SidecarPath));
            StringAssert.DoesNotContain("\"FLOAT\"", json,
                "the sidecar has grown a FLOAT block. Water is these three machines' wall " +
                "(VehicleGrounding); an amphibious ATV is a ruling, not an import.");
        }

        [Test]
        public void EveryAtvWearsADefAtTheClassNumbers([ValueSource(nameof(Bodies))] string[] body)
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(body[0]);
            var def = UnityEditor.AssetDatabase
                                 .LoadAssetAtPath<HiddenHarbours.Vehicles.VehicleDef>(v.VehicleDefPath);

            Assert.That(def, Is.Not.Null, $"{v.VehicleDefPath} did not load.");
            Assert.That(def.Id, Is.EqualTo(v.VehicleId), "ids are append-only and stable.");
            Assert.That(def.Mesh, Is.Not.Null, "her def wears no mesh, so she is unplaceable.");
            Assert.That(def.IsUsable(), Is.True);

            Assert.That(VehicleKinds.TryFromToken(def.KindToken, out VehicleKind kind), Is.True);
            Assert.That(kind, Is.EqualTo(VehicleKind.RoadVehicle),
                "⚠️ RoadVehicle is a claim about CAPABILITY, not tarmac: water is her wall. There is " +
                "no road registry on St Peters and none is needed — the land gate is TERRAIN.");
            Assert.That(VehicleKinds.IsDrivable(kind), Is.True);
        }

        /// <summary>The pack's own claim, re-measured: 11 ramps at most on ANY build of any body,
        /// against the fleet's cap of 16. Asserted over every fitted variant rather than over the
        /// rest pose alone, because a fitting is what would push it over.</summary>
        [Test]
        public void NoBuildOfAnyBodyExceedsTheKitsElevenRamps()
        {
            using IRigScriptHost host = Host("utilityQuad");

            foreach (string pose in new[]
                     {
                         "{body:'dirtbike'}", "{body:'dirtbike',stand:0}", "{body:'dirtbike',lamp:false}",
                         "{body:'trike'}", "{body:'trike',rackR:false}",
                         "{body:'quad'}", "{body:'quad',winch:true}",
                         "{body:'quad',rackF:false,rackR:false,hitch:false}",
                     })
            {
                int n = host.EvaluateString($"__mats({pose})").Split(',').Length;
                Assert.That(n, Is.LessThanOrEqualTo(11),
                    $"{pose} names {n} materials. The kit's own budget is 11 and the shader's cap " +
                    $"is {ShaderRampCap}; a build over 11 is worth checking against the art rather " +
                    "than nudging this number.");
            }
        }
    }
}
