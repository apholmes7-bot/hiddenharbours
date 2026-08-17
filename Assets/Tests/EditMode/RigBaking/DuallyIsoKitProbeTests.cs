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
        //  3. THE STEERING REVISION (drop of 2026-08-16, night) — what the new axes actually DO
        // =============================================================================================

        /// <summary>
        /// JS helpers the articulation tests share: the rig's own private builder at a pose, and a
        /// face-by-face comparison. Deliberately computed by the RIG — nothing about the truck's
        /// geometry is transcribed into C#, because a transcription is a second implementation that
        /// can agree with itself and with nothing else.
        /// </summary>
        const string Articulation = @"
            function __faces(o){ return VehicleIso.build(VehicleIso.resolve(o)); }
            function __moved(a, b){
              if (a.length !== b.length) return -1;
              var n = 0;
              for (var i = 0; i < a.length; i++) {
                var fa = a[i].v, fb = b[i].v, d = false;
                if (fa.length !== fb.length) { n++; continue; }
                for (var k = 0; k < fa.length && !d; k++)
                  for (var c = 0; c < 3; c++)
                    if (Math.abs(fa[k][c] - fb[k][c]) > 1e-9) { d = true; break; }
                if (d) n++;
              }
              return n;
            }
            function __movedMats(a, b){
              var o = {};
              for (var i = 0; i < a.length; i++) {
                var fa = a[i].v, fb = b[i].v, d = false;
                for (var k = 0; k < fa.length && !d; k++)
                  for (var c = 0; c < 3; c++)
                    if (Math.abs(fa[k][c] - fb[k][c]) > 1e-9) { d = true; break; }
                if (d) o[a[i].mat] = 1;
              }
              return Object.keys(o);
            }
            function __movedBoundsY(a, b){
              var lo = 1e9, hi = -1e9;
              for (var i = 0; i < a.length; i++) {
                var fa = a[i].v, fb = b[i].v, d = false;
                for (var k = 0; k < fa.length && !d; k++)
                  for (var c = 0; c < 3; c++)
                    if (Math.abs(fa[k][c] - fb[k][c]) > 1e-9) { d = true; break; }
                if (!d) continue;
                for (var k2 = 0; k2 < fa.length; k2++) {
                  lo = Math.min(lo, fa[k2][1]); hi = Math.max(hi, fa[k2][1]);
                }
              }
              return [lo, hi];
            }";

        /// <summary>Loads the rig with its private <c>build</c> widened onto the global, plus the
        /// articulation helpers. The widening is the shim <see cref="RigMeshExtractor"/> uses, not a
        /// second mechanism.</summary>
        static IRigScriptHost ArticulationHost()
        {
            string widened = RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Full(RigPath)), Global, new[] { "build" }, RigPath);

            IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(widened);
            host.Execute(Articulation);
            return host;
        }

        /// <summary>
        /// ⭐ <b>The standing art ask was ANSWERED — the rig now models steering.</b>
        ///
        /// <para>#548 shipped with a recorded art limit: <i>"the rig models NO STEERING. The front
        /// wheels roll but never yaw, so a turning truck is a yaw on the whole sprite."</i> The
        /// 2026-08-16 night revision adds a <c>steer</c> axis, and this pins what it promises so a
        /// later drop cannot quietly change the lock angles the controller solves against.</para>
        ///
        /// <para><b>Ackermann, and the sign matters.</b> <c>+1</c> is full LEFT, and the INNER wheel
        /// turns further than the outer (30° against 24.94°) — the geometry that keeps both front
        /// tyres tangent to the same turn centre. A controller that fed the same angle to both would
        /// scrub, and one that took the sign the other way would steer the truck into the ditch it
        /// was avoiding.</para>
        /// </summary>
        [Test]
        public void TheRigNowModelsSteeringAndItIsAckermannSplit()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Full(RigPath)));

            Assert.That(host.EvaluateBool($"typeof {Global}.steer === 'object' && {Global}.steer !== null"),
                Is.True,
                "the rig exports no steer block. If the art side withdrew the steering revision, the " +
                "controller's steered-wheel path has no angles to pose and must fall back to " +
                "whole-body yaw — that is a decision, not something to paper over.");

            Assert.That(host.EvaluateNumber($"{Global}.steer.maxInnerDeg"), Is.EqualTo(30d).Within(1e-9),
                "peak INNER lock moved. VehicleDef's steering authority is solved against this.");

            Assert.That(host.EvaluateNumber($"{Global}.steer.maxOuterDeg"), Is.EqualTo(24.94d).Within(0.005d),
                "peak OUTER lock moved — the Ackermann split changed, so the two front wheels no " +
                "longer share a turn centre at the angles this repo poses them at.");

            // +1 = LEFT lock, and the inner (left) wheel turns FURTHER. Both halves measured.
            Assert.That(host.EvaluateNumber($"{Global}.steer.angles(1).L"), Is.EqualTo(30d).Within(1e-9));
            Assert.That(host.EvaluateNumber($"{Global}.steer.angles(1).R"), Is.EqualTo(24.9372d).Within(1e-3),
                "at full LEFT lock the left wheel must be the inner one. A flipped sign here steers " +
                "the truck the wrong way and nothing else in the stack would notice.");

            Assert.That(host.EvaluateBool(
                    $"{Global}.steer.angles(-1).R === -{Global}.steer.angles(1).L && " +
                    $"{Global}.steer.angles(-1).L === -{Global}.steer.angles(1).R"),
                Is.True,
                "right lock is not the mirror of left lock — the controller cannot use one angle " +
                "solver for both directions.");
        }

        /// <summary>
        /// ⭐⭐ <b>The finding that decides how the truck is BUILT: <c>steer</c> articulates the front
        /// wheels and nothing else.</b>
        ///
        /// <para>This is the repo's own technique for splitting an articulated fitting from its body —
        /// <c>HullPropMeshDef.FixedMesh</c>'s "build the rig's face list at two poses, keep the ones
        /// that did not move" — applied to a truck. Measured: 286 of 1153 faces move, all of them
        /// inside the front axle's envelope, and <b>not one of them is <c>paint</c></b>. The body is
        /// untouched.</para>
        ///
        /// <para>So the Dually's front wheels are the outboard motor's problem exactly: a sub-mesh
        /// that rotates about a known pivot, which <see cref="Core.HullPropMeshDef"/> and
        /// <c>IHullPropRenderer</c> already exist to carry. No new articulation machinery is needed —
        /// which is the whole reason this is measured here rather than discovered during the bake.</para>
        /// </summary>
        [Test]
        public void SteerArticulatesTheFrontWheelsAndLeavesTheBodyAlone()
        {
            using IRigScriptHost host = ArticulationHost();

            double total = host.EvaluateNumber("__faces({}).length");
            Assert.That(total, Is.GreaterThan(200d), "the truck built implausibly few faces.");

            double moved = host.EvaluateNumber("__moved(__faces({}), __faces({steer:1}))");
            Assert.That(moved, Is.GreaterThan(0d),
                "steering moved NO geometry. Either the axis was withdrawn or it is a camera " +
                "parameter like yaw — and those two want opposite implementations.");
            Assert.That(moved, Is.LessThan(total * 0.5d),
                "steering moved more than half the truck. That is a whole-body transform, not a " +
                "steered pair, and it cannot be baked as a fitting.");

            // The moved set must sit inside the FRONT axle's envelope: axF ± wheelR = 2.18 ± 0.42.
            double lo = host.EvaluateNumber("__movedBoundsY(__faces({}), __faces({steer:1}))[0]");
            double hi = host.EvaluateNumber("__movedBoundsY(__faces({}), __faces({steer:1}))[1]");
            double axF = host.EvaluateNumber($"{Global}.G.axF");
            double wheelR = host.EvaluateNumber($"{Global}.G.wheelR");

            Assert.That(lo, Is.GreaterThanOrEqualTo(axF - wheelR - 1e-6),
                $"steering moved geometry aft of the front axle (y={lo:F3} < {axF - wheelR:F3}). " +
                "Something other than the front wheels is riding the steer axis.");
            Assert.That(hi, Is.LessThanOrEqualTo(axF + wheelR + 1e-6),
                $"steering moved geometry forward of the front wheels (y={hi:F3} > {axF + wheelR:F3}).");

            Assert.That(host.EvaluateBool(
                    "__movedMats(__faces({}), __faces({steer:1})).indexOf('paint') < 0"),
                Is.True,
                "a 'paint' face moved with the steering. The BODY is on the steer axis, so the " +
                "wheels cannot be lifted out as a fitting and posed against a static body — the " +
                "whole bake plan for this vehicle changes.");
        }

        /// <summary>
        /// ⭐⭐ <b><c>yaw</c> is a CAMERA parameter, and that is why continuous heading costs nothing.</b>
        ///
        /// <para>The revision's own comment reads <i>"Rotates the whole truck under the fixed key, so
        /// the shading stays right — this is how a turning truck reads BETWEEN facings, and it is not
        /// a rotated sprite."</i> That describes the RESULT. What it does mechanically is fold the
        /// angle into <c>camBasis</c> (<c>th = dir·45° + yaw</c>), so the model never moves and the
        /// key light never moves with it.</para>
        ///
        /// <para><b>Which means the mesh path already has this axis.</b>
        /// <c>IHullMeshRenderer.HeadingDirUnits</c> is documented "1 = 45°, fractional allowed —
        /// continuous is the point": the same continuous azimuth, applied the same way. A mesh truck
        /// therefore reads at any heading with no extra bake, no yaw variants, and no new channel —
        /// the thing the sprite path needed a new art axis for.</para>
        ///
        /// <para>Pinned because it would be entirely natural to "support" yaw by rotating the baked
        /// mesh's vertices, and this test says the rig does not mean that.</para>
        /// </summary>
        [Test]
        public void YawIsACameraParameterSoItMovesNoGeometry()
        {
            using IRigScriptHost host = ArticulationHost();

            Assert.That(host.EvaluateBool("typeof " + Global + ".resolve({}).yaw === 'number'"), Is.True,
                "the rig no longer resolves a yaw axis.");

            Assert.That(host.EvaluateNumber("__moved(__faces({}), __faces({yaw:30}))"),
                Is.EqualTo(0d),
                "yaw MOVED geometry. It is documented and measured as a camera-basis rotation, and " +
                "the mesh path leans on that: heading is IHullMeshRenderer.HeadingDirUnits, not a " +
                "re-baked vertex set. If the rig started baking yaw into the model, a mesh vehicle " +
                "would be yawed twice — once by the rig and once by the renderer.");
        }

        /// <summary>
        /// ⚠️ <b>Wheel roll is measured in REVOLUTIONS, and the axis is cyclic with period 1.</b>
        ///
        /// <para>Pinned because the obvious formula is wrong. Rolling a wheel of radius r at speed v
        /// is <c>ω = v/r</c> in RADIANS per second — and feeding that number to this axis spins the
        /// wheels 2π times too fast. The rig wants <c>v / (2πr)</c>.</para>
        ///
        /// <para>The period is what makes it measurable: <c>{roll:1}</c> is one whole turn and
        /// therefore reproduces <c>{roll:0}</c> exactly. (That also makes 1 a uselessly degenerate
        /// probe value — the first pass of this measurement tested exactly there and concluded the
        /// axis moved nothing.)</para>
        /// </summary>
        [Test]
        public void WheelRollIsCyclicInRevolutionsNotRadians()
        {
            using IRigScriptHost host = ArticulationHost();

            Assert.That(host.EvaluateNumber("__moved(__faces({}), __faces({wFL:1}))"), Is.EqualTo(0d),
                "one full revolution did not reproduce the neutral pose, so this axis is not in " +
                "revolutions. Whatever unit it is now in, the controller's v/(2*pi*r) conversion is " +
                "wrong and the wheels will spin at the wrong rate.");

            double quarter = host.EvaluateNumber("__moved(__faces({}), __faces({wFL:0.25}))");
            Assert.That(quarter, Is.GreaterThan(0d),
                "a quarter revolution moved nothing — the wheel does not roll in geometry at all, " +
                "and 'wheels turn with speed' cannot be delivered on the mesh path.");

            // Per-wheel and all-wheels are the same axis applied to different sets: the four roll
            // groups of a dually (front pair + rear duals as one group a side).
            double one = host.EvaluateNumber("__moved(__faces({}), __faces({wFL:0.25}))");
            double all = host.EvaluateNumber("__moved(__faces({}), __faces({roll:0.25}))");
            Assert.That(all, Is.EqualTo(one * 4d),
                $"the all-wheels roll axis moved {all} faces against {one} for one wheel. The rig " +
                "models four roll groups; if that changed, the controller's per-wheel posing no " +
                "longer covers the same geometry.");
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
