using System;
using System.IO;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE ROAD FLEET DROP (2026-08-27) — five wheeled rigs, measured in the repo's own V8.</b>
    ///
    /// <para>A probe, not an acceptance suite, and the direct descendant of
    /// <see cref="DuallyIsoKitProbeTests"/>. Everything here is MEASURED rather than read off a
    /// rig or a README, because this project has shipped mirrored boats five times by reading a rig
    /// and declaring what it meant — and once shipped a whole variant family with every facing
    /// reversed, with the right cell, the right pivot and the right count.</para>
    ///
    /// <para><b>One fixture for five rigs, because they really are one shape.</b> All five are the
    /// Dually's generator (private <c>build</c>/<c>makeMats</c>, <c>MATS</c> keyed by name), all
    /// five carry <c>wFL/wFR/wRL/wRR</c> + a master <c>roll</c> + <c>steer</c>, and all five publish
    /// a front-axle abeam anchor pair. Where they differ — the semis' tandem rear, the conventional
    /// box's larger cell, each rig's lock angles — the difference is a value in
    /// <see cref="Rigs"/>, and a rig that stops fitting the shape fails loudly rather than being
    /// quietly skipped.</para>
    ///
    /// <para><b>The trailer set is NOT here.</b> One rig with four towed bodies is a different
    /// animal and gets <see cref="TrailerIsoKitProbeTests"/>.</para>
    /// </summary>
    public class RoadFleetKitProbeTests
    {
        /// <summary>The facet shader's <c>_RampMeta</c> is a <c>float4[16]</c> — a real uniform
        /// array, guarded in three places. Over it, a def is "not usable" and the vehicle is
        /// unplaceable; there is no vehicle-side change that fixes that, which is why every count
        /// below is measured before a bake is attempted rather than discovered during one.</summary>
        const int ShaderRampCap = 16;

        // =============================================================================================
        //  THE TABLE — every number in it was measured, and each one is here because getting it
        //  wrong is silent
        // =============================================================================================

        public sealed class RoadRig
        {
            public string Key, Folder, RigFile, Global, Sidecar, Variant, Label;

            /// <summary>Aft and fore anchors, both on the centreline — the SECOND azimuth oracle.
            /// Named per rig because the names are each rig's own: the van runs
            /// <c>hitch</c>→<c>hoodLatch</c>, a cabover has no hood at all and runs
            /// <c>rollup</c>→<c>tiltLatch</c>, the semis <c>fifthWheel</c>→<c>hoodLatch</c>.</summary>
            public string AzAft, AzFore;

            public int Faces;

            /// <summary>Faces one front wheel's roll axis moves, and one REAR axis. They differ on
            /// the semis: a tandem side rides ONE axis, so <c>wRL</c> moves two axles' worth.</summary>
            public int RollFront, RollRear;

            /// <summary>What the master <c>roll</c> moves — must equal the union of the four.</summary>
            public int RollMaster;

            /// <summary>What <c>steer</c> moves in total, and what is left of it once the front
            /// roll axes have taken their tyres: the knuckle, per side.</summary>
            public int SteerMoved, SteerKnuckle;

            public double MaxInnerDeg, MaxOuterDeg, OuterAtFullLock;

            /// <summary>Ramps some face names at the BAKE POSE (what the committed
            /// <c>RigMeshSymbols.Reconstructions</c> filter produces), and across every build the
            /// game could place including the night pass.</summary>
            public int UsedAtBakePose, UsedAcrossAllBuilds;

            /// <summary>Tandem station centres, aft-most first — empty when the rear is a single
            /// axle. The Otter's station-window problem, on a truck.</summary>
            public double[] TandemStations = Array.Empty<double>();

            public override string ToString() => Key;
        }

        static readonly RoadRig[] Rigs =
        {
            new RoadRig
            {
                Key = "hightopVan", Folder = "hightop-van", RigFile = "vanIsoRig.js",
                Global = "VanIso", Sidecar = "vanIsoRig.hightopVan.gameplay.json",
                Variant = "hightopVan", Label = "Hightop Van",
                AzAft = "hitch", AzFore = "hoodLatch",
                Faces = 959, RollFront = 87, RollRear = 87, RollMaster = 348,
                SteerMoved = 254, SteerKnuckle = 80,
                MaxInnerDeg = 24, MaxOuterDeg = 20.6, OuterAtFullLock = 20.601977,
                UsedAtBakePose = 15, UsedAcrossAllBuilds = 16,
            },
            new RoadRig
            {
                Key = "caboverBox", Folder = "boxtruck-cabover", RigFile = "boxIsoRig.js",
                Global = "BoxIso", Sidecar = "boxIsoRig.caboverBox.gameplay.json",
                Variant = "caboverBox", Label = "Cabover Box Truck",
                // ⚠️ NOT hoodLatch. A cabover's cab sits over the engine and TILTS; her fore
                // centreline anchor is `tiltLatch`, and asking for a hood would be inadmissible.
                AzAft = "rollup", AzFore = "tiltLatch",
                Faces = 1090, RollFront = 87, RollRear = 87, RollMaster = 348,
                SteerMoved = 254, SteerKnuckle = 80,
                MaxInnerDeg = 33, MaxOuterDeg = 27.53, OuterAtFullLock = 27.530283,
                UsedAtBakePose = 16, UsedAcrossAllBuilds = 17,
            },
            new RoadRig
            {
                Key = "convBox", Folder = "boxtruck-conventional", RigFile = "convBoxIsoRig.js",
                Global = "ConvBoxIso", Sidecar = "convBoxIsoRig.convBox.gameplay.json",
                Variant = "convBox", Label = "Conventional Box Truck",
                AzAft = "rollup", AzFore = "hoodLatch",
                Faces = 1211, RollFront = 103, RollRear = 103, RollMaster = 412,
                SteerMoved = 286, SteerKnuckle = 80,
                MaxInnerDeg = 35, MaxOuterDeg = 30.32, OuterAtFullLock = 30.317214,
                UsedAtBakePose = 16, UsedAcrossAllBuilds = 17,
            },
            new RoadRig
            {
                Key = "aeroSemi", Folder = "semi-aero", RigFile = "aeroSemiIsoRig.js",
                Global = "AeroSemiIso", Sidecar = "aeroSemiIsoRig.aeroSemi.gameplay.json",
                Variant = "aeroSemi", Label = "Aero Sleeper Semi",
                AzAft = "fifthWheel", AzFore = "hoodLatch",
                Faces = 1538, RollFront = 119, RollRear = 238, RollMaster = 714,
                SteerMoved = 318, SteerKnuckle = 80,
                MaxInnerDeg = 32, MaxOuterDeg = 27.51, OuterAtFullLock = 27.507913,
                UsedAtBakePose = 15, UsedAcrossAllBuilds = 16,
                TandemStations = new[] { -2.90, -1.70 },
            },
            new RoadRig
            {
                Key = "classicSemi", Folder = "semi-classic", RigFile = "classicSemiIsoRig.js",
                Global = "ClassicSemiIso", Sidecar = "classicSemiIsoRig.classicSemi.gameplay.json",
                Variant = "classicSemi", Label = "Classic Long-Nose Semi",
                AzAft = "fifthWheel", AzFore = "hoodLatch",
                Faces = 1625, RollFront = 119, RollRear = 238, RollMaster = 714,
                SteerMoved = 318, SteerKnuckle = 80,
                MaxInnerDeg = 30, MaxOuterDeg = 26.23, OuterAtFullLock = 26.232117,
                UsedAtBakePose = 16, UsedAcrossAllBuilds = 17,
                TandemStations = new[] { -2.80, -1.60 },
            },
        };

        static string KitPath(RoadRig r) => $"docs/art/rigs/road-fleet-kit/{r.Folder}/{r.RigFile}";
        static string SidecarPath(RoadRig r) => VehicleRigFleet.SidecarFolder + "/" + r.Sidecar;
        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        // =============================================================================================
        //  1. THE DROP LANDED, AND ITS SIDECAR STILL PINS IT
        // =============================================================================================

        [Test]
        public void EveryKitAndItsSidecarLanded([ValueSource(nameof(Rigs))] RoadRig r)
        {
            FileAssert.Exists(Full(KitPath(r)));
            FileAssert.Exists(Full(SidecarPath(r)));
        }

        /// <summary>
        /// ⭐ <b>The staleness rule, applied on intake rather than at the bake.</b> An absent or wrong
        /// <c>derivedFromRigSha256</c> is a REFUSAL by design: a sidecar whose thresholds, cargo
        /// volumes and colliders were cut from a different shape is worse than no sidecar.
        ///
        /// <para>⚠️ <see cref="RigHashMatch.LineEndingNormalized"/> is an ACCEPTED pass, not a
        /// near-miss — the kits ship LF and <c>.gitattributes</c> checks <c>.js</c> out CRLF on
        /// Windows, and a line ending cannot move a vertex.</para>
        ///
        /// <para>⚠️⚠️ <b>ONE OF THE SIX DOES NOT PIN, AND IT IS REGISTERED AS A REFUSAL.</b> The
        /// hightop van's sidecar carries a hash whose first 16 hex digits match her rig and whose
        /// remaining 48 do not — which is not what a reshaped rig does, since a moved vertex changes
        /// the whole digest. Her own <c>hightopVan.contract.json</c> carries the CORRECT hash, so
        /// the kit measured the right rig and the defect is in one stamp. See
        /// <see cref="VehicleRigFleet.SidecarHashRefused"/>; <c>VehicleRigFleetTests</c> owns both
        /// halves of that law, and this test defers to it rather than duplicating the judgement.</para>
        /// </summary>
        [Test]
        public void EverySidecarPinsItsRigOnDisk_OrIsARegisteredRefusal(
            [ValueSource(nameof(Rigs))] RoadRig r)
        {
            byte[] rig = File.ReadAllBytes(Full(KitPath(r)));
            string sidecar = File.ReadAllText(Full(SidecarPath(r)));

            string expected = ReadJsonString(sidecar, "derivedFromRigSha256");
            Assert.That(expected, Is.Not.Empty,
                $"{r.Sidecar} carries no derivedFromRigSha256. An absent hash is a refusal by " +
                "design — ask the art side to stamp it rather than reading the polygons anyway.");

            RigHashMatch match = DeckSidecarReader.MatchRigHash(rig, expected, out string actual);
            bool registeredRefusal = VehicleRigFleet.SidecarHashRefused.ContainsKey(r.Key);

            if (registeredRefusal)
            {
                Assert.That(match, Is.EqualTo(RigHashMatch.None),
                    $"'{r.Key}' is registered in VehicleRigFleet.SidecarHashRefused but her sidecar " +
                    $"NOW PINS her rig ({expected} vs {actual}). The re-stamp landed — delete the " +
                    "refusal and the NotBaked reason that cites it.");
                return;
            }

            Assert.That(match, Is.Not.EqualTo(RigHashMatch.None),
                $"'{r.Key}': the sidecar pins {expected} but {KitPath(r)} hashes to {actual}, and " +
                "not through a line-ending difference. Do NOT trust its geometry, and do NOT " +
                "re-stamp the hash here — docs/art/rigs/** is the art director's lane. Record it " +
                "in VehicleRigFleet.SidecarHashRefused and send the re-stamp upstream.");
        }

        /// <summary>The sidecar names its own rig and body inside the file, so no reader ever has to
        /// infer either from the filename.</summary>
        [Test]
        public void EverySidecarNamesItsRigAndBodyInsideTheFile([ValueSource(nameof(Rigs))] RoadRig r)
        {
            string sidecar = File.ReadAllText(Full(SidecarPath(r)));

            Assert.That(ReadJsonString(sidecar, "exportSymbol"), Is.EqualTo(r.Global));
            Assert.That(ReadJsonString(sidecar, "variant"), Is.EqualTo(r.Variant));
            Assert.That(ReadJsonString(sidecar, "rig"), Is.EqualTo(r.RigFile));
            Assert.That(ReadJsonString(sidecar, "kind"), Is.EqualTo("road_vehicle"));
        }

        // =============================================================================================
        //  2. WHAT THE RIGS ACTUALLY EXPORT
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>All five are GENERATORS, exactly as the Dually is.</b> None exports <c>F</c>,
        /// <c>MATS</c>, <c>GAIN</c>, <c>BIAS</c> or <c>LN</c>: the faces come from a private
        /// <c>build(state)</c> and the palette from a private <c>makeMats(state)</c>.
        ///
        /// <para>Not a defect, and not a new mechanism — it is the position the lobster pack, the
        /// zodiac, the Dually and the Otter are all in, which is what
        /// <see cref="RigHullExtraction.FaceExpression"/> and
        /// <see cref="RigMeshSymbols.Reconstructions"/> exist for. Pinned so a later drop that
        /// starts exporting <c>F</c> makes this fail and the shim gets deleted rather than quietly
        /// outliving its reason.</para>
        /// </summary>
        [Test]
        public void EveryRigIsAGenerator_AndExportsNeitherFNorMats([ValueSource(nameof(Rigs))] RoadRig r)
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Full(KitPath(r))));

            Assert.That(host.EvaluateBool($"typeof {r.Global} === 'object' && {r.Global} !== null"),
                Is.True, $"the rig ran but installed no globalThis.{r.Global}.");

            foreach (string missing in RigMeshSymbols.Required)
                Assert.That(host.EvaluateBool($"typeof {r.Global}.{missing} !== 'undefined'"), Is.False,
                    $"{r.Global}.{missing} is now exported. The shim that reconstructs it can be " +
                    $"deleted from RigMeshSymbols.Reconstructions[\"{r.RigFile}\"].");

            // What it DOES give us, and what the extraction is written against.
            foreach (string entry in new[] { "resolve", "render", "dims", "anchors", "BODIES", "G",
                                             "PX", "defaultElev", "steer" })
                Assert.That(host.EvaluateBool($"typeof {r.Global}.{entry} !== 'undefined'"), Is.True,
                    $"{r.Global}.{entry} is gone — the vehicle extraction is written against it.");

            Assert.That(host.EvaluateNumber($"{r.Global}.PX"), Is.EqualTo(32d),
                "the pack bakes at 32 px = 1 m. A changed scale moves every vertex on screen.");
            Assert.That(host.EvaluateNumber($"{r.Global}.defaultElev"), Is.EqualTo(40d),
                "the pack's camera elevation moved — the ground-plane un-squash below divides by " +
                "sin(elev), and every azimuth measurement in this fixture depends on it.");
        }

        /// <summary>
        /// ⭐ <b>The private builder yields faces the packer could read, through the repo's OWN shim.</b>
        ///
        /// <para>The standalone V8 harness can reach these symbols by injecting <c>sym:sym</c> into
        /// the exported literal, but that proves only that the RIG has them. This runs
        /// <see cref="RigMeshExtractor.WidenExportedLiteral"/> — the mechanism a bake would use — so
        /// the finding is the finding the bake will make.</para>
        ///
        /// <para>Face COUNT is pinned because it is cheap and it is the one number that catches an
        /// art revision landing without anyone re-measuring. It is deliberately NOT treated as an
        /// oracle for identity: two different boats in this repo have had the same face count.</para>
        /// </summary>
        [Test]
        public void ThePrivateBuilderYieldsFacesThePackerCouldRead([ValueSource(nameof(Rigs))] RoadRig r)
        {
            using IRigScriptHost host = BuilderHost(r);

            Assert.That(host.EvaluateBool($"typeof {r.Global}.build === 'function'"), Is.True,
                "the private build() could not be widened onto the global.");

            Assert.That(host.EvaluateNumber("__faces({}).length"), Is.EqualTo((double)r.Faces),
                $"{r.Key} built a different number of faces. If the art side revised her, every " +
                "measured number in this fixture needs re-measuring — not nudging.");

            Assert.That(host.EvaluateBool(
                    "__faces({}).every(f => Array.isArray(f.v) && f.v.length >= 3 && " +
                    "typeof f.mat === 'string' && typeof f.b === 'number' && typeof f.db === 'number')"),
                Is.True,
                "a face came back in a shape the packer does not read — it wants {v, mat, b, db} " +
                "with a STRING material name (these rigs key MATS by name, not by index).");
        }

        /// <summary>
        /// ⭐ <b><c>MATS</c> is keyed by NAME and its default ramp is the FIRST key.</b>
        ///
        /// <para>Every baked HULL's <c>MATS</c> is an index-ordered array and the fleet's law is that
        /// its order IS the baked material index. These rigs' tables are objects keyed by name
        /// (<c>MATS[f.mat] || MATS.paint</c>), so that law does not transfer — a bake that assumed
        /// it would recolour the whole truck without failing anything.</para>
        ///
        /// <para>What DOES carry over is the packer's rule that an unknown material name resolves to
        /// index 0, so the default ramp must be first AND must itself be used. Measured, not
        /// assumed: each rig's own fallback is <c>MATS.paint</c>, and <c>paint</c> is first.</para>
        /// </summary>
        [Test]
        public void MatsIsKeyedByName_AndItsDefaultRampIsTheFirstUsedKey(
            [ValueSource(nameof(Rigs))] RoadRig r)
        {
            using IRigScriptHost host = BuilderHost(r);

            Assert.That(host.EvaluateBool($"typeof {r.Global}.makeMats === 'function'"), Is.True,
                "the private makeMats could not be widened onto the global.");

            Assert.That(host.EvaluateBool("Array.isArray(__mats({}))"), Is.False,
                "MATS is an ARRAY on this rig now. If the art side moved to the fleet's " +
                "index-ordered table, the vehicle bake should take the ordinary MATS path.");

            Assert.That(host.EvaluateString("Object.keys(__mats({}))[0]"), Is.EqualTo("paint"),
                "'paint' is no longer the first key of MATS. The face packer resolves an unknown " +
                "material name to index 0, so the DEFAULT ramp must be first — and each rig's own " +
                "fallback is MATS[f.mat] || MATS.paint. A different first key silently recolours " +
                "every face whose material the packer does not recognise.");

            Assert.That(host.EvaluateBool("__used({}).indexOf(Object.keys(__mats({}))[0]) >= 0"),
                Is.True,
                "the first MATS key is not used by any face. Index 0 is where an unknown material " +
                "lands, so a first key nothing paints means the fallback colour is one the art " +
                "director never looked at.");
        }

        // =============================================================================================
        //  3. ARTICULATION — the split that decides how she is BUILT
        //
        //  ⚠️ PROBE ORDER IS LOAD-BEARING. A steer axis moves the wheel AND its knuckle, so the
        //  per-wheel roll axes are asked FIRST and steer is asked with their faces already claimed.
        //  Listing steer first swallows both front corners and leaves the roll axes empty — which
        //  partitions perfectly (the body simply takes everything) and bakes a truck whose wheels
        //  are welded on.
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>Four roll groups, perfectly disjoint, and their union is EXACTLY the master axis.</b>
        ///
        /// <para>Asserted as index SETS rather than counts, because counts cannot tell two disjoint
        /// 87-face groups from two identical ones — and "identical" is what a rig that ignored the
        /// per-wheel key would produce.</para>
        ///
        /// <para>⚠️ <b>The axis is CYCLIC with period 1</b>, which is what makes it measurable and
        /// what makes <c>{wFL:1}</c> a uselessly degenerate probe value: one whole turn reproduces
        /// rest EXACTLY, and a probe there concludes the axis is dead. It also pins the unit — the
        /// obvious <c>ω = v/r</c> is RADIANS per second and spins these wheels 2π times too fast;
        /// the rigs want <c>v / (2πr)</c>.</para>
        /// </summary>
        [Test]
        public void TheFourRollGroupsAreDisjoint_AndTheirUnionIsTheMasterAxis(
            [ValueSource(nameof(Rigs))] RoadRig r)
        {
            using IRigScriptHost host = BuilderHost(r);

            Assert.That(host.EvaluateNumber("__moved({}, {wFL:1})"), Is.EqualTo(0d),
                "one full revolution did not reproduce the neutral pose, so this axis is not in " +
                "revolutions. Whatever unit it is now in, a controller's v/(2*pi*r) conversion is " +
                "wrong and the wheels spin at the wrong rate.");

            foreach (string w in new[] { "wFL", "wFR" })
                Assert.That(host.EvaluateNumber($"__movedSet({{}}, {{{w}:0.25}}).length"),
                    Is.EqualTo((double)r.RollFront), $"{w} moved a different number of faces.");

            foreach (string w in new[] { "wRL", "wRR" })
                Assert.That(host.EvaluateNumber($"__movedSet({{}}, {{{w}:0.25}}).length"),
                    Is.EqualTo((double)r.RollRear),
                    $"{w} moved a different number of faces. On the semis this is TWO axles' worth " +
                    "— a tandem side rides one axis — so a change here may mean the drivetrain was " +
                    "re-modelled, not just resized.");

            // Every pair disjoint: two fittings claiming one wheel is exactly what the partition
            // assert cannot see.
            string[] wheels = { "wFL", "wFR", "wRL", "wRR" };
            for (int a = 0; a < wheels.Length; a++)
                for (int b = a + 1; b < wheels.Length; b++)
                    Assert.That(host.EvaluateNumber(
                            $"__inter(__movedSet({{}}, {{{wheels[a]}:0.25}}), " +
                            $"__movedSet({{}}, {{{wheels[b]}:0.25}}))"),
                        Is.EqualTo(0d),
                        $"{wheels[a]} and {wheels[b]} move overlapping faces. Two fittings would " +
                        "claim the same geometry and one of them would draw a frozen copy.");

            Assert.That(host.EvaluateNumber("__movedSet({}, {roll:0.25}).length"),
                Is.EqualTo((double)r.RollMaster), "the master roll axis moved a different amount.");

            Assert.That(host.EvaluateNumber(
                    "__union(__movedSet({}, {wFL:0.25}), __movedSet({}, {wFR:0.25}), " +
                    "__movedSet({}, {wRL:0.25}), __movedSet({}, {wRR:0.25}))"),
                Is.EqualTo((double)r.RollMaster),
                "the four per-wheel groups no longer union to exactly what the master roll moves. " +
                "Too few means a wheel no per-wheel axis can reach; too many means something other " +
                "than the wheels is riding one of them.");
        }

        /// <summary>
        /// ⭐⭐ <b><c>steer</c> articulates the front corners and leaves the BODY alone</b> — the
        /// finding that decides the whole bake plan, and the reason it is measured here rather than
        /// discovered during a bake.
        ///
        /// <para>Asked AFTER the roll axes, so what it finds is only the knuckle: the fender lip,
        /// hub cover and mudflap that swing with the corner but do not turn with the tyre. Measured
        /// at exactly <b>80 faces on all five rigs, 40 a side</b> — the same fitting, five times.</para>
        ///
        /// <para>Not one moved face is <c>paint</c>, and none of them is aft of the centre. If the
        /// body were on the steer axis, the wheels could not be lifted out as fittings and posed
        /// against a static body, and the plan for every vehicle in the pack would change.</para>
        /// </summary>
        [Test]
        public void SteerArticulatesTheFrontCorners_AndLeavesTheBodyAlone(
            [ValueSource(nameof(Rigs))] RoadRig r)
        {
            using IRigScriptHost host = BuilderHost(r);

            Assert.That(host.EvaluateNumber("__movedSet({}, {steer:1}).length"),
                Is.EqualTo((double)r.SteerMoved),
                "steering moved a different amount. Zero would mean the axis was withdrawn or " +
                "became a camera parameter like yaw, and those two want opposite implementations.");

            Assert.That(host.EvaluateNumber("__movedSet({}, {steer:1}).length"),
                Is.LessThan(r.Faces * 0.5d),
                "steering moved more than half the vehicle. That is a whole-body transform, not a " +
                "steered pair, and it cannot be baked as a fitting.");

            // What steer moves beyond the two front tyres — the knuckle, and it splits evenly.
            Assert.That(host.EvaluateNumber(
                    "__leftover(__movedSet({}, {steer:1}), " +
                    "__movedSet({}, {wFL:0.25}).concat(__movedSet({}, {wFR:0.25}))).length"),
                Is.EqualTo((double)r.SteerKnuckle),
                "the knuckle changed size. It is what steer moves once the front roll axes have " +
                "taken their tyres, and it is the geometry a SteerOnly fitting would carry.");

            Assert.That(host.EvaluateNumber(
                    "__sideCount(__leftover(__movedSet({}, {steer:1}), " +
                    "__movedSet({}, {wFL:0.25}).concat(__movedSet({}, {wFR:0.25}))), -1)"),
                Is.EqualTo((double)(r.SteerKnuckle / 2)),
                "the knuckle no longer splits evenly across the centreline. A steer axis moves BOTH " +
                "front corners at once, and an even split is what lets a SideSign filter separate " +
                "them into two fittings.");

            Assert.That(host.EvaluateNumber("__rearCount(__movedSet({}, {steer:1}))"), Is.EqualTo(0d),
                "steering moved geometry aft of centre. Something other than the front corners is " +
                "riding the steer axis.");

            Assert.That(host.EvaluateBool("__movedMats({}, {steer:1}).indexOf('paint') < 0"), Is.True,
                "a 'paint' face moved with the steering. The BODY is on the steer axis, so the " +
                "wheels cannot be lifted out as a fitting and posed against a static body — the " +
                "whole bake plan for this vehicle changes.");
        }

        /// <summary>
        /// The Ackermann block the controller solves against. <c>+1</c> is full LEFT, and the INNER
        /// wheel turns further than the outer — the geometry that keeps both front tyres tangent to
        /// one turn centre. A controller feeding both the same angle would scrub; one taking the
        /// sign the other way would steer into the ditch it was avoiding.
        /// </summary>
        [Test]
        public void SteeringIsAckermannSplit_AndMirroredAboutStraightAhead(
            [ValueSource(nameof(Rigs))] RoadRig r)
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Full(KitPath(r))));

            Assert.That(host.EvaluateNumber($"{r.Global}.steer.maxInnerDeg"),
                Is.EqualTo(r.MaxInnerDeg).Within(1e-9),
                "peak INNER lock moved. VehicleDef's steering authority is solved against this.");

            Assert.That(host.EvaluateNumber($"{r.Global}.steer.maxOuterDeg"),
                Is.EqualTo(r.MaxOuterDeg).Within(0.005d),
                "peak OUTER lock moved — the Ackermann split changed, so the two front wheels no " +
                "longer share a turn centre at the angles this repo poses them at.");

            Assert.That(host.EvaluateNumber($"{r.Global}.steer.angles(1).L"),
                Is.EqualTo(r.MaxInnerDeg).Within(1e-6));
            Assert.That(host.EvaluateNumber($"{r.Global}.steer.angles(1).R"),
                Is.EqualTo(r.OuterAtFullLock).Within(1e-3),
                "at full LEFT lock the left wheel must be the INNER one. A flipped sign here steers " +
                "the vehicle the wrong way and nothing else in the stack would notice.");

            Assert.That(host.EvaluateBool(
                    $"{r.Global}.steer.angles(-1).R === -{r.Global}.steer.angles(1).L && " +
                    $"{r.Global}.steer.angles(-1).L === -{r.Global}.steer.angles(1).R"),
                Is.True,
                "right lock is not the mirror of left lock — the controller cannot use one angle " +
                "solver for both directions.");
        }

        /// <summary>
        /// ⚠️ <b>THE SEMIS' REAR IS A TANDEM ON ONE AXIS PER SIDE</b>, so a single probe moves TWO
        /// axles and no side filter can separate them — they share a side. That is the Otter's
        /// problem, not the Dually's, and it means their fittings need her station windows
        /// (<c>Axis.YMin</c>/<c>YMax</c>) rather than per-wheel probes.
        ///
        /// <para>Measured here so PR 2 sizes the windows from the geometry rather than from the
        /// anchor names: the stations sit 1.20 m apart and the rolling geometry spans ±0.31 about
        /// each, so a window anywhere from ±0.32 to ±0.89 captures one axle and clears its
        /// neighbour. The three single-axle rigs assert the opposite — ONE station — so a tandem
        /// appearing on a straight truck cannot pass unnoticed.</para>
        /// </summary>
        [Test]
        public void TheRearRollGroupHasTheStationsItsAxleCountImplies(
            [ValueSource(nameof(Rigs))] RoadRig r)
        {
            using IRigScriptHost host = BuilderHost(r);

            double stations = host.EvaluateNumber("__stations({}, {wRL:0.25}).length");
            int expected = r.TandemStations.Length == 0 ? 1 : r.TandemStations.Length;

            Assert.That(stations, Is.EqualTo((double)expected),
                $"{r.Key}'s left rear roll axis now moves geometry at {stations} axle station(s), " +
                $"not {expected}. A tandem appearing where a single axle was (or the reverse) " +
                "changes what a fitting can be split by: one probe per wheel, or a station window.");

            for (int i = 0; i < r.TandemStations.Length; i++)
                Assert.That(host.EvaluateNumber($"__stations({{}}, {{wRL:0.25}})[{i}].centre"),
                    Is.EqualTo(r.TandemStations[i]).Within(0.01d),
                    $"tandem station {i} moved. The station windows PR 2 sizes are measured from " +
                    "these, and overlapping windows make two fittings claim one wheel.");
        }

        /// <summary>
        /// ⭐⭐ <b><c>yaw</c> is a CAMERA parameter on all five — it moves ZERO geometry.</b>
        ///
        /// <para>This one was worth measuring rather than assuming, because every README in the pack
        /// describes the axis as <i>"±45° rebaked headings"</i>, which reads exactly like a rotated
        /// vertex set. It is not: the rigs fold the angle into <c>camBasis</c> (<c>th = dir·45 +
        /// yaw</c>) and re-render, so the model never moves and the fixed key light never moves with
        /// it. The phrase describes the SPRITE bake.</para>
        ///
        /// <para><b>Which means the mesh path already has this axis.</b>
        /// <c>IHullMeshRenderer.HeadingDirUnits</c> is the same continuous azimuth applied the same
        /// way — so a mesh vehicle reads at any heading with no extra bake and no yaw variants. If a
        /// rig ever started baking yaw into the model, she would be yawed TWICE: once by the rig and
        /// once by the renderer.</para>
        /// </summary>
        [Test]
        public void YawIsACameraParameterSoItMovesNoGeometry([ValueSource(nameof(Rigs))] RoadRig r)
        {
            using IRigScriptHost host = BuilderHost(r);

            Assert.That(host.EvaluateBool($"typeof {r.Global}.resolve({{}}).yaw === 'number'"), Is.True,
                "the rig no longer resolves a yaw axis.");

            foreach (int deg in new[] { 30, 45, -45 })
                Assert.That(host.EvaluateNumber($"__moved({{}}, {{yaw:{deg}}})"), Is.EqualTo(0d),
                    $"yaw {deg}° MOVED geometry. The kit README calls the axis \"±45° rebaked " +
                    "headings\", which describes the sprite bake — mechanically it is a camera-basis " +
                    "rotation. If that changed, heading can no longer be HeadingDirUnits and a mesh " +
                    "vehicle would be turned twice.");
        }

        /// <summary>
        /// ⚠️ <b>Suspension is NOT a rigid translation</b>, and that is the rig being right rather
        /// than a defect: the READMEs say "the body moves, wheels stay down", so a sprung pose moves
        /// part of the machine and not the rest.
        ///
        /// <para>Pinned because the Otter's <c>float</c> IS an exact rigid translation, and that one
        /// fact is what lets her dry mesh stand in for her floating one with a runtime Z offset. The
        /// same shortcut is NOT available here, and assuming it would sink the wheels into the road.</para>
        /// </summary>
        [Test]
        public void SuspensionIsNotARigidTranslation_SoItIsNotAFreeRuntimeOffset(
            [ValueSource(nameof(Rigs))] RoadRig r)
        {
            using IRigScriptHost host = BuilderHost(r);

            foreach (string axis in new[] { "susF", "susR" })
            {
                Assert.That(host.EvaluateNumber($"__moved({{}}, {{{axis}:1}})"), Is.GreaterThan(0d),
                    $"{axis} moved nothing — the suspension does not travel in geometry at all.");

                Assert.That(host.EvaluateNumber($"__translationDeviation({{}}, {{{axis}:1}})"),
                    Is.GreaterThan(1e-6),
                    $"{axis} is now an exact rigid translation. That would be a genuine improvement " +
                    "— it would make a sprung pose a runtime offset on the static mesh, the way the " +
                    "Otter's float is — but it is a change of kind, so take it deliberately rather " +
                    "than letting a bake discover it.");
            }
        }

        // =============================================================================================
        //  4. AZIMUTH — by anchor pairs, never by silhouette taper
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>Every rig in the pack is COUNTER-CLOCKWISE, confirmed by two independent analytic
        /// oracles and a third written from the other end.</b>
        ///
        /// <para><b>Why not the taper heuristic.</b> <c>RigAzimuthProbe.MeasureFromQuarterTurn</c>
        /// bins the silhouette along its long axis and calls the narrower end the bow. The "beam" it
        /// measures is a SCREEN-SPACE extent, so a cab or a roof that cantilevers forward projects
        /// wide where the chassis is narrow — and the verdict inverts. It did exactly that on
        /// eighteen lobster variants, and a box truck is the worst possible case for it: she has no
        /// taper at all.</para>
        ///
        /// <para><b>Oracle 1 — the front-axle ABEAM pair</b>, admissible only when the two hubs share
        /// a screen y and differ in screen x at heading 0. Its ground bearing at a quarter turn is
        /// <b>exactly −90.00°</b> on all five, which is what every hull in this repo that publishes
        /// an abeam pair also returns.</para>
        ///
        /// <para><b>Oracle 2 — an aft→fore CENTRELINE pair</b>, each rig's own names. Screen x
        /// carries no z term, so the two anchors need only be on the centreline, not at one height.
        /// The point of a second oracle is that it is independent: if the two disagreed the bake
        /// would refuse rather than pick one, because a mirrored heading map looks fine until she
        /// drives backwards.</para>
        ///
        /// <para><b>Oracle 3 — the STEPPED form</b>, the same abeam pair read at all eight headings
        /// with Δy negated, so its sign convention is the opposite of oracle 1's by construction.
        /// Mean step <b>+45.0000°</b>, max deviation <b>0</b> — and that zero doubles as proof of the
        /// ÷sin(40°) divisor, since an un-squashed bearing would not step uniformly.</para>
        /// </summary>
        [Test]
        public void AzimuthIsCounterClockwise_ByTwoAnchorOraclesAndNeverTheTaper(
            [ValueSource(nameof(Rigs))] RoadRig r)
        {
            using IRigScriptHost host = BuilderHost(r);

            // --- admissibility, at heading 0, before any bearing is believed -----------------------
            Assert.That(host.EvaluateBool(
                    $"(function(a){{return !!a.wheelFL && !!a.wheelFR && " +
                    $"Math.abs(a.wheelFR.y - a.wheelFL.y) < 1e-6 && " +
                    $"Math.abs(a.wheelFR.x - a.wheelFL.x) > 1e-6;}})({r.Global}.anchors(0,{{}}))"),
                Is.True,
                "the front wheel anchors are not an admissible ABEAM pair at heading 0 (equal " +
                "screen y, different screen x). Something moved the front axle off-square, and the " +
                "bearing below would not be a vehicle bearing.");

            Assert.That(host.EvaluateBool(
                    $"(function(a){{return !!a.{r.AzAft} && !!a.{r.AzFore} && " +
                    $"Math.abs(a.{r.AzFore}.x - a.{r.AzAft}.x) < 1e-6;}})({r.Global}.anchors(0,{{}}))"),
                Is.True,
                $"'{r.AzAft}' and '{r.AzFore}' are not an admissible CENTRELINE pair at heading 0. " +
                "⚠️ An inadmissible pair is the trap: a centreline oracle that quietly stops " +
                "applying leaves ONE oracle, and one oracle is a coin flip on mirroring.");

            // --- oracle 1: the abeam ground bearing at a quarter turn -----------------------------
            Assert.That(host.EvaluateNumber("__abeamBearing('wheelFL','wheelFR')"),
                Is.EqualTo(-90d).Within(1e-6),
                "the front-axle ground bearing at a quarter turn is no longer −90.00°. Positive " +
                "means CLOCKWISE, and a mirrored heading map draws her driving backwards.");

            // --- oracle 2: the centreline nose direction, same quarter turn -----------------------
            Assert.That(host.EvaluateNumber($"__noseDx('{r.AzAft}','{r.AzFore}')"), Is.LessThan(0d),
                $"the {r.AzAft}→{r.AzFore} nose screen dx went positive at a quarter turn, which " +
                "reads CLOCKWISE — and disagrees with the abeam pair. Do not guess between two " +
                "analytic oracles: render her beside a registered reference and compare bearings.");

            // --- oracle 3: the stepped form, written from the other end ---------------------------
            Assert.That(host.EvaluateNumber("__meanStep('wheelFL','wheelFR')"),
                Is.EqualTo(45d).Within(1e-4),
                "the mean bearing step across the eight headings is no longer +45°. Positive is " +
                "CounterClockwise in this formulation (Δy negated), which is the opposite sign " +
                "convention to the oracle above — the two agreeing is the point.");

            Assert.That(host.EvaluateNumber("__maxStepDeviation('wheelFL','wheelFR')"),
                Is.LessThan(1e-6),
                "the eight bearing steps are no longer uniform. That zero is what proves the " +
                "÷sin(elev) un-squash: without it the locus is an ellipse and the steps wobble.");
        }

        // =============================================================================================
        //  5. THE MATERIAL CENSUS — measured against the shader BEFORE any bake is attempted
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>Every rig in the pack fits the facet shader at the pose she would be baked at —
        /// and three of the five do NOT fit if one mesh has to light up at night.</b>
        ///
        /// <para><b>Why this is measured now.</b> <c>_RampMeta</c> is a <c>float4[16]</c> and over it
        /// a def is "not usable": the vehicle bakes and is then unplaceable, which is where the
        /// Otter sat from #558 to #562. No vehicle-side change fixes it — the Dually (17 declared /
        /// 16 used) and the zodiac (18 / 14) were saved by filtering to the USED set, and the Otter
        /// was not, because she used all seventeen. So the count is measured before a bake is
        /// attempted, never discovered during one.</para>
        ///
        /// <para><b>The bake pose fits, everywhere.</b> Measured: 15 / 16 / 16 / 15 / 16 used ramps,
        /// against 17 declared on every rig. The filter drops what no face names.</para>
        ///
        /// <para>⚠️⚠️ <b>THE NIGHT LAMP IS THE LIMIT, and it is a SWAP rather than an addition.</b>
        /// <c>head</c> (unlit) and <c>glow</c> (lit) are the two forms of one lamp and MEASURED
        /// NEVER APPEAR IN THE SAME BUILD — at night <c>head</c> goes unused and <c>glow</c> takes
        /// its place, so each build on its own stays at 15 or 16. A single mesh carrying BOTH is 16
        /// on the van and the aero and <b>17 on the two box trucks and the classic semi</b>.</para>
        ///
        /// <para><b>So the measured proposal upstream is NOT a merge.</b> The two ramps are visibly
        /// different colours (a grey-blue lens against a green-white glow) and merging them would
        /// lose the night read, which is the whole point of the axis. What the measurement supports
        /// is SLOT REUSE: because no build names both, one <c>_RampMeta</c> slot can carry whichever
        /// the build names, with no colour lost and no widened uniform array. That is a decision for
        /// PR 2 and the owner; what this test does is stop it being discovered at a bake.</para>
        /// </summary>
        [Test]
        public void HerPaletteFitsTheFacetShaderAtTheBakePose_AndTheNightLampIsWhatCostsTheHeadroom(
            [ValueSource(nameof(Rigs))] RoadRig r)
        {
            using IRigScriptHost host = BuilderHost(r);

            Assert.That(host.EvaluateNumber("Object.keys(__mats({})).length"), Is.EqualTo(17d),
                $"{r.Key} declares a different number of ramps. Declared is not what the shader " +
                "counts — used is — but a change here means the palette was reworked.");

            Assert.That(host.EvaluateNumber("__used({}).length"), Is.EqualTo((double)r.UsedAtBakePose),
                $"{r.Key} paints a different number of ramps at the bake pose, not "
                + $"{r.UsedAtBakePose}. Over " +
                $"{ShaderRampCap} she is unplaceable (VehicleMeshDef.IsUsable refuses her); under " +
                "it, something stopped painting. Either way this needs re-measuring, not a nudged " +
                "number.");

            Assert.That(host.EvaluateNumber("__used({}).length"),
                Is.LessThanOrEqualTo((double)ShaderRampCap),
                $"{r.Key} is over the float4[16] _RampMeta at the pose she would be baked at. Do " +
                "NOT widen _RampMeta: measure which ramps could merge and take it upstream, the way " +
                "the Otter's cockpit mat was folded into her mesh.");

            // Day and night each fit on their own — the swap keeps the count flat.
            Assert.That(host.EvaluateNumber("__used({night:true}).length"),
                Is.EqualTo((double)r.UsedAtBakePose),
                "the night build no longer paints the same NUMBER of ramps as the day build. The " +
                "night pass is a swap (head → glow), not an addition; if it became an addition the " +
                "night mesh may not fit at all.");

            Assert.That(host.EvaluateBool(
                    "__used({}).indexOf('head') >= 0 && __used({}).indexOf('glow') < 0 && " +
                    "__used({night:true}).indexOf('glow') >= 0 && " +
                    "__used({night:true}).indexOf('head') < 0"),
                Is.True,
                "head and glow are no longer a clean unlit/lit swap. The slot-reuse proposal above " +
                "rests entirely on no build naming both — if one now does, the pack needs a real " +
                "merge or a ruling.");

            // And the union across every build the game could place — the number a single
            // day-and-night mesh would have to carry.
            Assert.That(host.EvaluateNumber($"__unionUsed({BuildList(r)}).length"),
                Is.EqualTo((double)r.UsedAcrossAllBuilds),
                $"{r.Key}'s all-builds ramp union changed. This is the number a ONE-MESH " +
                "day-and-night vehicle would need, and three rigs in this pack are at 17 — one over " +
                "— which is the measured limit PR 2 and the owner have to rule on.");
        }

        // =============================================================================================
        //  6. THE BAKE'S OWN RECONSTRUCTION — the expression the extractor would really install
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The committed <c>RigMeshSymbols.Reconstructions</c> entry evaluates, and produces the
        /// filtered table the census above predicts.</b>
        ///
        /// <para>Everything else in this fixture reaches <c>build</c> and <c>makeMats</c> through a
        /// plain <c>sym:sym</c> widening, which proves the RIG has them. This runs the actual
        /// committed expression through the actual widener, which is the only thing that proves the
        /// BAKE can read them — the standalone V8 harness cannot confirm that the repo's shim
        /// reaches the same symbols.</para>
        ///
        /// <para>⚠️ The expression is evaluated INSIDE the rig's closure, so it names <c>makeMats</c>
        /// and <c>resolve</c> UNQUALIFIED. Qualifying them is the shape a probe needs (the widening
        /// puts symbols on the global, it does not put them in scope) and is wrong there.</para>
        /// </summary>
        [Test]
        public void TheCommittedMatsReconstruction_EvaluatesToTheFilteredTable(
            [ValueSource(nameof(Rigs))] RoadRig r)
        {
            Assert.That(RigMeshSymbols.IsReconstructed(KitPath(r), "MATS"), Is.True,
                $"{r.RigFile} has no MATS reconstruction. These rigs have no MATS const — the " +
                "table is built per-pose by a private makeMats(s) — so the ordinary MATS:MATS " +
                "widening fails outright with \"MATS is not defined\" and the bake never starts.");

            string widened = RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Full(KitPath(r))), r.Global,
                new[] { "build", "makeMats", "MATS" }, KitPath(r));

            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(widened);

            Assert.That(host.EvaluateBool($"Array.isArray({r.Global}.MATS)"), Is.False,
                "the reconstruction produced an ARRAY. These tables are keyed by name.");

            Assert.That(host.EvaluateNumber($"Object.keys({r.Global}.MATS).length"),
                Is.EqualTo((double)r.UsedAtBakePose),
                "the reconstruction no longer filters to the ramps some face actually names. A ramp " +
                "no face references cannot colour a pixel, and carrying it costs a slot the shader " +
                "does not have.");

            Assert.That(host.EvaluateString($"Object.keys({r.Global}.MATS)[0]"), Is.EqualTo("paint"),
                "'paint' is no longer index 0 of the reconstructed table. That is where the face " +
                "packer sends an unknown material name, and it is what the rig's own " +
                "MATS[f.mat] || MATS.paint fallback agrees with.");
        }

        // =============================================================================================
        //  helpers
        // =============================================================================================

        /// <summary>The list of builds the all-builds census unions over, as a JS array literal.
        /// Per rig because the axes are per rig — a van has a sliding door and a roof height, a
        /// semi has skirts or a visor.</summary>
        static string BuildList(RoadRig r) => r.Key switch
        {
            "hightopVan" =>
                "[{},{night:true},{roof:'low'},{windows:true}," +
                "{dFL:1,dFR:1,slide:1,barnL:1,barnR:1,hood:1}," +
                "{mirrors:false,mudflaps:false,hitch:false}]",
            "caboverBox" =>
                "[{},{night:true},{rollup:1},{gate:1},{tilt:1},{dL:1,dR:1},{liftgate:false}," +
                "{mirrors:false,mudflaps:false}]",
            "convBox" =>
                "[{},{night:true},{rollup:1},{gate:1},{hood:1},{dL:1,dR:1},{liftgate:false}," +
                "{fairing:false}]",
            "aeroSemi" =>
                "[{},{night:true},{dL:1,dR:1},{hood:1},{skirts:false}," +
                "{mirrors:false,mudflaps:false}]",
            "classicSemi" =>
                "[{},{night:true},{dL:1,dR:1},{hood:1},{visor:false}," +
                "{mirrors:false,mudflaps:false}]",
            _ => throw new ArgumentOutOfRangeException(nameof(r), r.Key, "no build list"),
        };

        /// <summary>Loads a rig with its private <c>build</c> and <c>makeMats</c> widened onto the
        /// global by the repo's OWN shim, then installs the shared helpers.</summary>
        static IRigScriptHost BuilderHost(RoadRig r)
        {
            // ⚠️ `build` and `makeMats` only. MATS is deliberately NOT widened here: this host is
            // for measuring the rig, and the committed reconstruction gets its own test above.
            string widened = RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Full(KitPath(r))), r.Global, new[] { "build", "makeMats" },
                KitPath(r));

            IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(widened);
            host.Execute(Helpers(r.Global));
            return host;
        }

        /// <summary>
        /// The measuring apparatus, computed BY THE RIG. Nothing about any vehicle's geometry is
        /// transcribed into C# — a transcription is a second implementation that can agree with
        /// itself and with nothing else.
        /// </summary>
        internal static string Helpers(string global) => @"
            var __R = " + global + @";

            // ⚠️ `__R.resolve` is QUALIFIED. The shim widens `build` onto the GLOBAL; it does not
            // put the closure's other privates in scope, and an unqualified resolve({}) dies with
            // ""resolve is not defined"" — which reads like the rig lacking the symbol.
            function __faces(o){ return __R.build(__R.resolve(o||{})); }
            function __mats(o){ return __R.makeMats(__R.resolve(o||{})); }

            function __differs(p, q){
              if (p.length !== q.length) return true;
              for (var k = 0; k < p.length; k++)
                for (var c = 0; c < 3; c++)
                  if (Math.abs(p[k][c] - q[k][c]) > 1e-9) return true;
              return false;
            }
            function __moved(a, b){
              var A = __faces(a), B = __faces(b);
              if (A.length !== B.length) return -1;   // a different BUILD, not a pose
              var n = 0;
              for (var i = 0; i < A.length; i++) if (__differs(A[i].v, B[i].v)) n++;
              return n;
            }
            // The INDEX SET, so overlap is measured rather than inferred: two disjoint 87-face
            // groups and two identical ones both read as ""87, 87"".
            function __movedSet(a, b){
              var A = __faces(a), B = __faces(b), out = [];
              for (var i = 0; i < A.length; i++) if (__differs(A[i].v, B[i].v)) out.push(i);
              return out;
            }
            function __inter(a, b){ var s = {}, n = 0;
              for (var i=0;i<a.length;i++) s[a[i]]=1;
              for (var j=0;j<b.length;j++) if (s[b[j]]) n++; return n; }
            function __union(){ var s = {};
              for (var i=0;i<arguments.length;i++){ var a=arguments[i];
                for (var j=0;j<a.length;j++) s[a[j]]=1; }
              return Object.keys(s).length; }
            function __leftover(set, claimed){
              var s = {}, out = [];
              for (var i=0;i<claimed.length;i++) s[claimed[i]]=1;
              for (var j=0;j<set.length;j++) if (!s[set[j]]) out.push(set[j]);
              return out;
            }
            function __centroid(i, axis){
              var p = __faces({})[i].v, c = 0;
              for (var k=0;k<p.length;k++) c += p[k][axis];
              return c / p.length;
            }
            function __sideCount(set, sgn){
              var n = 0;
              for (var i=0;i<set.length;i++){ var cx = __centroid(set[i], 0);
                if (sgn < 0 ? cx < 0 : cx > 0) n++; }
              return n;
            }
            function __rearCount(set){
              var n = 0;
              for (var i=0;i<set.length;i++) if (__centroid(set[i], 1) < 0) n++;
              return n;
            }
            function __movedMats(a, b){
              var A = __faces(a), B = __faces(b), o = {};
              for (var i = 0; i < A.length; i++) if (__differs(A[i].v, B[i].v)) o[A[i].mat] = 1;
              return Object.keys(o);
            }
            // Max deviation of every moved vertex from ONE common offset. 0 = a pure rigid
            // translation, which would make the pose a runtime transform rather than a second bake.
            function __translationDeviation(a, b){
              var A = __faces(a), B = __faces(b), dx = null, worst = 0;
              for (var i = 0; i < A.length; i++) {
                var p = A[i].v, q = B[i].v;
                for (var k = 0; k < p.length; k++) {
                  var d = [q[k][0]-p[k][0], q[k][1]-p[k][1], q[k][2]-p[k][2]];
                  if (Math.abs(d[0])<1e-12 && Math.abs(d[1])<1e-12 && Math.abs(d[2])<1e-12) continue;
                  if (dx === null) dx = d;
                  for (var c = 0; c < 3; c++) worst = Math.max(worst, Math.abs(d[c]-dx[c]));
                }
              }
              return dx === null ? -1 : worst;
            }
            // Axle STATIONS a pose moves: the moved faces' centroid rows, clustered on a gap of
            // 0.5 m. One station = a single axle; two = a tandem sharing one axis, which no side
            // filter can separate.
            function __stations(a, b){
              var set = __movedSet(a, b), rows = {};
              for (var i=0;i<set.length;i++){
                var y = Math.round(__centroid(set[i], 1) * 100) / 100;
                rows[y] = (rows[y] || 0) + 1;
              }
              var ys = Object.keys(rows).map(Number).sort(function(p,q){ return p-q; });
              if (!ys.length) return [];
              var out = [], cur = [ys[0]];
              for (var j=1;j<ys.length;j++){
                if (ys[j] - ys[j-1] > 0.5) { out.push(cur); cur = []; }
                cur.push(ys[j]);
              }
              out.push(cur);
              return out.map(function(st){
                var n = 0; for (var t=0;t<st.length;t++) n += rows[st[t]];
                return { lo: st[0], hi: st[st.length-1],
                         centre: Math.round((st[0]+st[st.length-1]) / 2 * 1e4) / 1e4, faces: n };
              });
            }

            // --- the material census ------------------------------------------------------------
            function __used(o){
              var F = __faces(o), s = {}, M = __mats(o), out = [];
              for (var i = 0; i < F.length; i++) s[F[i].mat] = 1;
              // In the rig's own MATS key order — that order IS the baked material index.
              for (var k in M) if (s[k]) out.push(k);
              return out;
            }
            function __unionUsed(list){
              var M = __mats(list[0]), seen = {}, out = [];
              for (var i=0;i<list.length;i++){ var u = __used(list[i]);
                for (var j=0;j<u.length;j++) seen[u[j]] = 1; }
              for (var k in M) if (seen[k]) out.push(k);
              return out;
            }

            // --- azimuth: two analytic oracles, and a third from the other end -------------------
            // Screen y is DOWN and carries a z term; screen x does not. So the ABEAM oracle
            // un-squashes by sin(elev) and needs its pair at one height, while the CENTRELINE
            // oracle reads x alone and does not.
            function __sinElev(){ return Math.sin(__R.defaultElev * Math.PI / 180); }
            function __abeamBearing(l, r){
              var A = __R.anchors(2, {});
              return Math.atan2((A[r].y - A[l].y) / __sinElev(), A[r].x - A[l].x) * 180 / Math.PI;
            }
            function __noseDx(aft, fore){
              var A = __R.anchors(2, {});
              return A[fore].x - A[aft].x;
            }
            function __steps(l, r){
              var b = [], i;
              for (i = 0; i < 8; i++) {
                var A = __R.anchors(i, {});
                // ⚠️ Δy NEGATED here — the opposite sign convention to __abeamBearing, on purpose.
                // Two transcriptions of one measurement written from opposite ends agree only if
                // the measurement is real.
                b.push(Math.atan2(-(A[r].y - A[l].y) / __sinElev(), A[r].x - A[l].x) * 180 / Math.PI);
              }
              var s = [];
              for (i = 1; i < 8; i++) { var d = b[i] - b[i-1];
                while (d > 180) d -= 360; while (d < -180) d += 360; s.push(d); }
              return s;
            }
            function __meanStep(l, r){
              var s = __steps(l, r), t = 0;
              for (var i=0;i<s.length;i++) t += s[i];
              return t / s.length;
            }
            function __maxStepDeviation(l, r){
              var s = __steps(l, r), m = __meanStep(l, r), w = 0;
              for (var i=0;i<s.length;i++) w = Math.max(w, Math.abs(s[i] - m));
              return w;
            }";

        /// <summary>Pulls a top-level string out of a sidecar without taking a JSON dependency this
        /// assembly does not already have. Deliberately dumb: it reads known scalar header fields,
        /// it does not parse the document.</summary>
        internal static string ReadJsonString(string json, string key)
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
