using System;
using System.IO;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE TRAILER SET — one rig, one sidecar, FOUR towed bodies.</b>
    ///
    /// <para>Separate from <see cref="RoadFleetKitProbeTests"/> because a container rig is a
    /// different animal, and the difference is the part that ships the wrong vehicle silently. Every
    /// measurement here is per <b>(file, body)</b>: a probe run once for the file answers for
    /// whichever body the rig happens to default to, and hands that answer to all four.</para>
    ///
    /// <para><b>What this fixture settles:</b></para>
    /// <list type="bullet">
    /// <item>⚠️⚠️ an unknown body id <b>falls back</b> to the default rather than throwing — so a
    /// mistyped pick bakes a perfectly good reefer53 under a flatbed's name;</item>
    /// <item>⭐⭐ <c>yaw</c> moves ZERO geometry, so the Dually's camera-parameter law HOLDS here
    /// despite the kit README calling the axis "±45° rebaked headings";</item>
    /// <item>there is no <c>steer</c> axis at all — she is towed, and
    /// <c>VehicleKinds.IsDrivable</c> is the code that has to know it;</item>
    /// <item>the 53-footers carry a TANDEM on one axis per side, so their fittings need station
    /// windows rather than per-wheel probes;</item>
    /// <item>the ramp table must be unioned over ALL FOUR bodies, because filtering at the default
    /// one drops the flatbed deck's <c>wood</c> and the packer sends it to index 0.</item>
    /// </list>
    /// </summary>
    public class TrailerIsoKitProbeTests
    {
        const string RigPath = "docs/art/rigs/road-fleet-kit/trailers/trailerIsoRig.js";
        const string Global = "TrailerIso";
        const string SidecarFile = "trailerIsoRig.trailers.gameplay.json";

        /// <summary>The facet shader's <c>_RampMeta</c> is a <c>float4[16]</c>.</summary>
        const int ShaderRampCap = 16;

        /// <summary>⚠️ The rig's OWN default, measured — <b>not</b> <c>BODIES[0]</c>, which is
        /// <c>flatbed28</c>. That gap is the whole reason the fallback below is dangerous: the body
        /// a missing pick silently resolves to is the LAST one anybody would expect.</summary>
        const string DefaultBody = "reefer53";

        public sealed class Body
        {
            public string Pick, Label;
            public int Faces, CellW, CellH, PivotX, PivotY;

            /// <summary>Faces one side's roll axis moves. The 53s are a tandem on ONE axis per
            /// side, so theirs is two axles' worth.</summary>
            public int RollPerSide;

            /// <summary>Axle station centres a side's roll axis touches, aft-most first.</summary>
            public double[] Stations;

            /// <summary>Faces the rear doors move — the reefers swing to 255°, the flatbeds clamp
            /// theirs, so this is the honest zero rather than a missing measurement.</summary>
            public int DoorFaces;

            /// <summary>Ramps some face names on this body, and the count with the night pass.</summary>
            public int UsedRamps, UsedRampsAllBuilds;

            public override string ToString() => Pick;
        }

        static readonly Body[] Bodies =
        {
            new Body
            {
                Pick = "flatbed28", Label = "Flatbed Trailer 28 ft", Faces = 643,
                CellW = 384, CellH = 320, PivotX = 192, PivotY = 214,
                RollPerSide = 127, Stations = new[] { -2.90 }, DoorFaces = 0,
                UsedRamps = 8, UsedRampsAllBuilds = 8,
            },
            new Body
            {
                Pick = "flatbed53", Label = "Flatbed Trailer 53 ft", Faces = 1119,
                CellW = 640, CellH = 480, PivotX = 320, PivotY = 300,
                RollPerSide = 254, Stations = new[] { -6.70, -5.50 }, DoorFaces = 0,
                UsedRamps = 8, UsedRampsAllBuilds = 8,
            },
            new Body
            {
                Pick = "reefer28", Label = "Reefer Trailer 28 ft", Faces = 656,
                CellW = 384, CellH = 320, PivotX = 192, PivotY = 214,
                RollPerSide = 127, Stations = new[] { -2.90 }, DoorFaces = 54,
                UsedRamps = 11, UsedRampsAllBuilds = 12,
            },
            new Body
            {
                Pick = "reefer53", Label = "Reefer Trailer 53 ft", Faces = 1067,
                CellW = 640, CellH = 480, PivotX = 320, PivotY = 300,
                RollPerSide = 254, Stations = new[] { -6.70, -5.50 }, DoorFaces = 54,
                UsedRamps = 11, UsedRampsAllBuilds = 12,
            },
        };

        static string SidecarPath => VehicleRigFleet.SidecarFolder + "/" + SidecarFile;
        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        // =============================================================================================
        //  1. THE DROP, AND WHAT THE SIDECAR CLAIMS
        // =============================================================================================

        [Test]
        public void TheRigAndItsSidecarBothLanded()
        {
            FileAssert.Exists(Full(RigPath));
            FileAssert.Exists(Full(SidecarPath));
        }

        /// <summary>The sidecar still pins the rig on disk. ⚠️
        /// <see cref="RigHashMatch.LineEndingNormalized"/> is an accepted pass — the kit ships LF and
        /// <c>.gitattributes</c> checks <c>.js</c> out CRLF on Windows, and a line ending cannot
        /// move a vertex.</summary>
        [Test]
        public void TheSidecarStillPinsTheRigOnDisk()
        {
            byte[] rig = File.ReadAllBytes(Full(RigPath));
            string sidecar = File.ReadAllText(Full(SidecarPath));

            string expected = RoadFleetKitProbeTests.ReadJsonString(sidecar, "derivedFromRigSha256");
            Assert.That(expected, Is.Not.Empty,
                "the trailer sidecar carries no derivedFromRigSha256. An absent hash is a refusal " +
                "by design — the coupling handshake, off-tracking inputs and cargo bays it declares " +
                "would be describing some other shape.");

            RigHashMatch match = DeckSidecarReader.MatchRigHash(rig, expected, out string actual);

            Assert.That(match, Is.Not.EqualTo(RigHashMatch.None),
                $"the sidecar pins {expected} but the rig on disk hashes to {actual}, and not " +
                "through a line-ending difference. Do not read its geometry, and do not re-stamp " +
                "the hash here — record it in VehicleRigFleet.SidecarHashRefused and send the " +
                "re-stamp upstream.");
        }

        /// <summary>
        /// ⭐ <b>ONE sidecar, FOUR bodies — and its <c>kind</c> is the PLURAL <c>towed_bodies</c>.</b>
        ///
        /// <para>Accepted as shipped rather than corrected, exactly like the Otter's two spellings:
        /// <c>docs/art/rigs/**</c> is the art director's lane, and a token fixed by hand breaks the
        /// sidecar's own hash pin and comes back on the next regeneration. The translation lives in
        /// <see cref="HiddenHarbours.Core.VehicleKinds"/>, which is the one table that translates.</para>
        /// </summary>
        [Test]
        public void TheSidecarDeclaresFourTowedBodiesUnderOnePluralKind()
        {
            string sidecar = File.ReadAllText(Full(SidecarPath));

            Assert.That(RoadFleetKitProbeTests.ReadJsonString(sidecar, "exportSymbol"),
                Is.EqualTo(Global));
            Assert.That(RoadFleetKitProbeTests.ReadJsonString(sidecar, "rig"),
                Is.EqualTo("trailerIsoRig.js"));
            Assert.That(RoadFleetKitProbeTests.ReadJsonString(sidecar, "variant"),
                Is.EqualTo("trailers-x4"),
                "the sidecar's variant is no longer the plural one. Four registered bodies key off " +
                "variant + pick precisely because this document describes four.");

            string kind = RoadFleetKitProbeTests.ReadJsonString(sidecar, "kind");
            Assert.That(kind, Is.EqualTo("towed_bodies"));
            Assert.That(HiddenHarbours.Core.VehicleKinds.TryFromToken(kind, out var mapped), Is.True,
                $"'{kind}' is not a token VehicleKinds maps, so this sidecar would be UNSCANNED — " +
                "not unbaked, unscanned, which is worse because the coverage test stays green.");
            Assert.That(mapped, Is.EqualTo(HiddenHarbours.Core.VehicleKind.TowedBody));
        }

        // =============================================================================================
        //  2. THE CONTAINER — four bodies, and a fallback that does not throw
        // =============================================================================================

        /// <summary>
        /// ⭐ The same generator shape as the rest of the pack: no <c>F</c>, no <c>MATS</c>, no
        /// shading constants — faces from a private <c>build(state)</c>, palette from a private
        /// <c>makeMats(state)</c>.
        /// </summary>
        [Test]
        public void TheRigIsAGeneratorAndCarriesFourBodies()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Full(RigPath)));

            foreach (string missing in RigMeshSymbols.Required)
                Assert.That(host.EvaluateBool($"typeof {Global}.{missing} !== 'undefined'"), Is.False,
                    $"{Global}.{missing} is now exported — the reconstruction for trailerIsoRig.js " +
                    "can be narrowed or deleted.");

            foreach (string entry in new[] { "resolve", "render", "dims", "anchors", "BODIES",
                                             "G", "CELLS", "cellFor", "pivotFor" })
                Assert.That(host.EvaluateBool($"typeof {Global}.{entry} !== 'undefined'"), Is.True,
                    $"{Global}.{entry} is gone — the trailer extraction is written against it.");

            Assert.That(host.EvaluateNumber($"Object.keys({Global}.BODIES).length"), Is.EqualTo(4d),
                "the trailer set no longer carries four bodies. Every registered body is a separate " +
                "VehicleRigFleet entry, so a body added or removed here needs one added or removed " +
                "there — the coverage law cannot see inside a container rig.");

            foreach (Body b in Bodies)
                Assert.That(host.EvaluateBool($"!!{Global}.BODIES['{b.Pick}']"), Is.True,
                    $"body '{b.Pick}' is gone from the rig, but VehicleRigFleet still registers it.");
        }

        /// <summary>
        /// ⚠️⚠️ <b>AN UNKNOWN BODY ID FALLS BACK TO THE DEFAULT — it does not throw.</b> The single
        /// most dangerous fact about this rig, and the reason <see cref="VehicleRigFleet.Vehicle.Pick"/>
        /// exists and is asserted to reach the face expression.
        ///
        /// <para>A mistyped or missing pick therefore does not fail: it builds <c>reefer53</c> and
        /// hands it back under some other body's name. The result is a perfectly good trailer with
        /// the right cell, the right pivot and a plausible face count, which is exactly why nothing
        /// downstream catches it. This repo has shipped the wrong boat through the identical
        /// <c>byId</c> fallback, and the registration probe's cell-agreement gate is what caught
        /// that one.</para>
        ///
        /// <para>⚠️ And the default is <c>reefer53</c>, NOT <c>BODIES[0]</c> (<c>flatbed28</c>) — so
        /// the body a slip silently selects is not even the first one in the list.</para>
        /// </summary>
        [Test]
        public void AnUnknownBodyIdSilentlyFallsBackToTheDefault_WhichIsWhyPickIsLoadBearing()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Full(RigPath)));

            Assert.That(host.EvaluateString($"{Global}.resolve({{}}).body"), Is.EqualTo(DefaultBody),
                "the rig's default body moved. Anything that omits a pick gets THIS body, so the " +
                "value matters even though nothing should ever rely on it.");

            Assert.That(host.EvaluateString($"Object.keys({Global}.BODIES)[0]"),
                Is.EqualTo("flatbed28"),
                "BODIES[0] moved. It is asserted only to keep the gap below meaningful: the " +
                "default is NOT the first body, so 'it will just take the first one' is wrong.");

            Assert.That(host.EvaluateString($"{Global}.resolve({{body:'NOT_A_TRAILER'}}).body"),
                Is.EqualTo(DefaultBody),
                "an unknown body id now resolves to something other than the default. If the art " +
                "side made it THROW, that is a genuine improvement and this test should become the " +
                "assertion that it throws — a loud failure at the one point a typo can enter is " +
                "worth more than any fallback.");
        }

        [Test]
        public void EveryBodyBuildsItsOwnGeometryAndItsOwnCell([ValueSource(nameof(Bodies))] Body b)
        {
            using IRigScriptHost host = BuilderHost();

            Assert.That(host.EvaluateNumber($"__faces({Pose(b)}).length"), Is.EqualTo((double)b.Faces),
                $"{b.Pick} built a different number of faces. ⚠️ Face count is NOT an identity " +
                "oracle here — the pups differ by only 13 faces — but a change means the art was " +
                "revised and every measured number for this body needs re-measuring.");

            Assert.That(host.EvaluateNumber($"{Global}.cellFor('{b.Pick}').W"),
                Is.EqualTo((double)b.CellW),
                $"{b.Pick}'s cell width moved. ⚠️ The pups take the 384×320 road cell and the " +
                "53-footers a 640×480 one — 16.15 m projects both up- and down-screen — so a bake " +
                "that assumed one cell for the file would clip two of the four.");
            Assert.That(host.EvaluateNumber($"{Global}.cellFor('{b.Pick}').H"),
                Is.EqualTo((double)b.CellH));
            Assert.That(host.EvaluateNumber($"{Global}.pivotFor('{b.Pick}').x"),
                Is.EqualTo((double)b.PivotX));
            Assert.That(host.EvaluateNumber($"{Global}.pivotFor('{b.Pick}').y"),
                Is.EqualTo((double)b.PivotY));
        }

        /// <summary>⭐ The four bodies are genuinely DIFFERENT geometry, not one shape four times.
        /// Hashed over {mat, b, db, vertices@1e-6} in face order rather than compared by count,
        /// because count is what a fallback reproduces perfectly.</summary>
        [Test]
        public void TheFourBodiesAreAllDistinctGeometry()
        {
            using IRigScriptHost host = BuilderHost();

            var seen = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Body b in Bodies)
            {
                string hash = host.EvaluateString($"__geometryHash({Pose(b)})");
                Assert.That(seen.ContainsKey(hash), Is.False,
                    $"'{b.Pick}' builds geometry identical to '" +
                    (seen.TryGetValue(hash, out string other) ? other : "?") +
                    "'. Either the rig collapsed two bodies, or a pick is not reaching resolve() " +
                    "and both calls fell back to the default body.");
                seen[hash] = b.Pick;
            }
        }

        // =============================================================================================
        //  3. ARTICULATION — towed, so there is no steer to ask about
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>NO <c>steer</c> AXIS AT ALL.</b> The measurement behind
        /// <c>VehicleKinds.IsDrivable(TowedBody) == false</c>: she is not a truck whose steering
        /// happens to be unmodelled, she is a body that is dragged. The kit's own README says
        /// <i>"No steering — towed bodies"</i>, and this is that claim measured rather than read.
        /// </summary>
        [Test]
        public void SheHasNoSteerAxis_BecauseSheIsTowed()
        {
            using IRigScriptHost host = BuilderHost();

            Assert.That(host.EvaluateBool($"{Global}.resolve({{}}).steer !== undefined"), Is.False,
                "the trailer rig now resolves a steer axis. If the art side gave a towed body " +
                "steering geometry, VehicleKind.TowedBody's whole justification needs revisiting — " +
                "do not simply start posing it.");

            Assert.That(host.EvaluateBool($"typeof {Global}.steer === 'undefined'"), Is.True,
                "the rig now publishes a steer block. A towed body has no lock angles for a " +
                "controller to solve against.");
        }

        /// <summary>
        /// The two roll sides are perfectly disjoint and union to exactly the master axis — the same
        /// law as the road pack's four groups, with two probes instead of four because a trailer's
        /// wheels are driven by nothing and roll per side.
        ///
        /// <para>⚠️ Cyclic with period 1: <c>{wL:1}</c> is one whole turn and reproduces rest
        /// exactly, which pins the unit as REVOLUTIONS. The obvious <c>ω = v/r</c> is radians per
        /// second and would spin these wheels 2π times too fast.</para>
        /// </summary>
        [Test]
        public void TheTwoRollSidesAreDisjoint_AndUnionToTheMasterAxis(
            [ValueSource(nameof(Bodies))] Body b)
        {
            using IRigScriptHost host = BuilderHost();

            Assert.That(host.EvaluateNumber($"__moved({Pose(b)}, {Pose(b, "wL:1")})"), Is.EqualTo(0d),
                "one full revolution did not reproduce the neutral pose, so this axis is not in " +
                "revolutions and a controller's v/(2*pi*r) conversion would be wrong.");

            foreach (string side in new[] { "wL", "wR" })
                Assert.That(host.EvaluateNumber($"__movedSet({Pose(b)}, {Pose(b, side + ":0.25")}).length"),
                    Is.EqualTo((double)b.RollPerSide),
                    $"{b.Pick}'s {side} roll group changed size.");

            Assert.That(host.EvaluateNumber(
                    $"__inter(__movedSet({Pose(b)}, {Pose(b, "wL:0.25")}), " +
                    $"__movedSet({Pose(b)}, {Pose(b, "wR:0.25")}))"),
                Is.EqualTo(0d),
                "the two sides move overlapping faces — the split needs a disambiguator it does " +
                "not have.");

            Assert.That(host.EvaluateNumber($"__movedSet({Pose(b)}, {Pose(b, "roll:0.25")}).length"),
                Is.EqualTo((double)(b.RollPerSide * 2)),
                "the master roll no longer covers exactly both sides.");
        }

        /// <summary>
        /// ⚠️ <b>The 53-footers are a TANDEM on ONE axis per side</b>, so a single probe moves two
        /// axles and no side filter can separate them — they share a side. That is the Otter's
        /// problem, and it means their fittings need her station windows
        /// (<c>Axis.YMin</c>/<c>YMax</c>), sized from the geometry rather than from anchor names —
        /// the rig publishes only ONE wheel anchor per side, so the second station has no anchor to
        /// be named after.
        ///
        /// <para>Measured: the two stations sit 1.20 m apart and each moves 127 faces, so a window
        /// anywhere from ±0.32 to ±0.88 takes one axle and clears its neighbour. The pups assert
        /// ONE station, so a tandem appearing on a single-axle pup cannot pass unnoticed.</para>
        /// </summary>
        [Test]
        public void EachSidesRollTouchesExactlyTheAxleStationsSheHas(
            [ValueSource(nameof(Bodies))] Body b)
        {
            using IRigScriptHost host = BuilderHost();

            Assert.That(host.EvaluateNumber($"__stations({Pose(b)}, {Pose(b, "wL:0.25")}).length"),
                Is.EqualTo((double)b.Stations.Length),
                $"{b.Pick}'s left roll axis now moves geometry at a different number of axle " +
                "stations. Single axle or tandem decides whether a fitting can be split by probe " +
                "or must be split by station window.");

            for (int i = 0; i < b.Stations.Length; i++)
                Assert.That(host.EvaluateNumber($"__stations({Pose(b)}, {Pose(b, "wL:0.25")})[{i}].centre"),
                    Is.EqualTo(b.Stations[i]).Within(0.01d),
                    $"{b.Pick}'s station {i} moved. Overlapping windows make two fittings claim one " +
                    "wheel, and the partition assert fires on it.");
        }

        /// <summary>
        /// ⭐ <b>The landing gear is an EXACT RIGID TRANSLATION</b> — 24 faces, max per-vertex
        /// deviation 0 — so parked and coupled are one mesh plus an offset on that fitting, with no
        /// second bake and no gear variant.
        ///
        /// <para>⚠️ <b>Suspension is NOT</b>, and that is the rig being right: it pivots at the
        /// kingpin so the coupling plane holds 1.18 m while the tail drops. Both halves are pinned,
        /// because the Otter's <c>float</c> being an exact translation is precisely what let her dry
        /// mesh stand in for her floating one — and assuming the same here would lift the wrong
        /// end.</para>
        /// </summary>
        [Test]
        public void TheLandingGearIsARigidTranslation_ButTheSuspensionIsNot(
            [ValueSource(nameof(Bodies))] Body b)
        {
            using IRigScriptHost host = BuilderHost();

            // gear 1 is PARKED (shoes grounded) and is the bake default; 0 is COUPLED (legs up).
            Assert.That(host.EvaluateNumber($"{Global}.resolve({Pose(b)}).gear"), Is.EqualTo(1d),
                "the trailer no longer rests PARKED. The kit bakes at gear 1 and the game cranks " +
                "the legs up on coupling; a changed default silently reposes every placement.");

            Assert.That(host.EvaluateNumber($"__moved({Pose(b)}, {Pose(b, "gear:0")})"),
                Is.EqualTo(24d), "the landing gear changed size.");

            Assert.That(host.EvaluateNumber($"__translationDeviation({Pose(b)}, {Pose(b, "gear:0")})"),
                Is.EqualTo(0d).Within(1e-12),
                "the landing gear is no longer a rigid translation — some vertices moved " +
                "differently from others, so raising the legs can no longer be a runtime offset on " +
                "one fitting.");

            Assert.That(host.EvaluateNumber($"__translationDeviation({Pose(b)}, {Pose(b, "sus:1")})"),
                Is.GreaterThan(1e-6),
                "suspension is now an exact rigid translation. It is documented to pivot at the " +
                "KINGPIN — the coupling plane holds 1.18 m while the tail drops — so a rigid " +
                "translation would mean the coupling height now moves with load, which is a change " +
                "of kind rather than a tuning.");
        }

        /// <summary>The reefers' doors swing out to 255° for dock work; the flatbeds clamp theirs.
        /// The flatbed zero is asserted rather than skipped, so "the doors stopped moving" cannot
        /// hide behind "that body has none".</summary>
        [Test]
        public void OnlyTheReefersHaveDoorsThatMove([ValueSource(nameof(Bodies))] Body b)
        {
            using IRigScriptHost host = BuilderHost();

            Assert.That(host.EvaluateNumber($"__moved({Pose(b)}, {Pose(b, "barnL:1,barnR:1")})"),
                Is.EqualTo((double)b.DoorFaces),
                $"{b.Pick}'s rear doors changed. A flatbed's zero is the rig clamping an axis its " +
                "body does not have, not a measurement that failed.");

            if (b.DoorFaces > 0)
                Assert.That(host.EvaluateNumber($"__moved({Pose(b)}, {Pose(b, "barnL:1")})"),
                    Is.EqualTo((double)(b.DoorFaces / 2)),
                    "one door no longer moves half of what both move — the leaves are no longer " +
                    "symmetric, so a single door fitting cannot serve both sides mirrored.");
        }

        /// <summary>
        /// ⭐⭐ <b><c>yaw</c> MOVES ZERO GEOMETRY — the Dually's law HOLDS on the trailers.</b>
        ///
        /// <para><b>This is the finding this fixture was written to settle.</b> The kit README
        /// describes the axis as <i>"<c>yaw</c> ±45° rebaked headings"</i>, which reads exactly like
        /// a rotated vertex set, and a trailer has no reason to behave like a truck: she articulates
        /// about a kingpin rather than steering, so "the rig bakes her heading" would have been an
        /// entirely reasonable design. Measured at 30°, +45° and −45° on all four bodies: <b>zero
        /// faces move</b>. The rig folds the angle into <c>camBasis</c> and re-renders; the phrase
        /// describes the SPRITE bake.</para>
        ///
        /// <para><b>What that buys.</b> A mesh trailer reads at any heading through
        /// <c>IHullMeshRenderer.HeadingDirUnits</c> — no yaw variants, no extra bake, no new channel
        /// — which matters more here than anywhere else in the pack, because a trailer's whole point
        /// is sitting at an angle to her tractor. If a rig ever started baking yaw into the model,
        /// she would be turned twice: once by the rig and once by the renderer.</para>
        /// </summary>
        [Test]
        public void YawIsACameraParameterOnTheTrailersToo([ValueSource(nameof(Bodies))] Body b)
        {
            using IRigScriptHost host = BuilderHost();

            Assert.That(host.EvaluateBool($"typeof {Global}.resolve({Pose(b)}).yaw === 'number'"),
                Is.True, "the rig no longer resolves a yaw axis.");

            foreach (string deg in new[] { "30", "45", "-45" })
                Assert.That(host.EvaluateNumber($"__moved({Pose(b)}, {Pose(b, "yaw:" + deg)})"),
                    Is.EqualTo(0d),
                    $"yaw {deg}° MOVED geometry on {b.Pick}. The kit README calls this axis \"±45° " +
                    "rebaked headings\" and that phrase describes the sprite bake — mechanically it " +
                    "is a camera-basis rotation. If it became a real vertex rotation, heading can " +
                    "no longer be HeadingDirUnits and a mesh trailer would be yawed twice.");
        }

        // =============================================================================================
        //  4. AZIMUTH — anchor pairs a towed body actually has
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>Counter-clockwise on all four bodies, by two independent oracles and a third
        /// written from the other end.</b>
        ///
        /// <para>⚠️ <b>Neither of the road pack's anchor pairs exists here.</b> A trailer has no hood
        /// and no front axle, so <c>wheelFL</c>/<c>wheelFR</c> and <c>hoodLatch</c> are simply
        /// absent — the abeam oracle takes her axle hubs (<c>wheelL</c>/<c>wheelR</c>) and the
        /// centreline oracle runs <c>rear</c>→<c>kingpin</c>. The silhouette taper is not consulted
        /// and could not be: a box on wheels carries no taper signal at all.</para>
        ///
        /// <para>⚠️ And the anchors are asked FOR THIS BODY. <c>anchors(dir, {})</c> answers for
        /// whatever the rig resolves, so asking once for the file would hand every trailer the
        /// default body's bearing — the (file, pick) trap, in the one measurement where a wrong
        /// answer mirrors her heading map.</para>
        /// </summary>
        [Test]
        public void AzimuthIsCounterClockwise_ByKingpinAndAxleAnchors(
            [ValueSource(nameof(Bodies))] Body b)
        {
            using IRigScriptHost host = BuilderHost();

            // --- admissibility first, at heading 0 -------------------------------------------------
            Assert.That(host.EvaluateBool(
                    $"(function(a){{return !!a.wheelL && !!a.wheelR && " +
                    $"Math.abs(a.wheelR.y - a.wheelL.y) < 1e-6 && " +
                    $"Math.abs(a.wheelR.x - a.wheelL.x) > 1e-6;}})({Global}.anchors(0,{Pose(b)}))"),
                Is.True,
                $"{b.Pick}'s axle anchors are not an admissible ABEAM pair at heading 0 (equal " +
                "screen y, different screen x). ⚠️ An inadmissible pair is the trap the lobster " +
                "family sprang: the oracle stops applying, silently, and one oracle is a coin flip.");

            Assert.That(host.EvaluateBool(
                    $"(function(a){{return !!a.rear && !!a.kingpin && " +
                    $"Math.abs(a.kingpin.x - a.rear.x) < 1e-6;}})({Global}.anchors(0,{Pose(b)}))"),
                Is.True,
                $"{b.Pick}'s rear→kingpin pair is not on one centreline at heading 0.");

            // --- oracle 1: the abeam ground bearing at a quarter turn -----------------------------
            Assert.That(host.EvaluateNumber($"__abeamBearing({Pose(b)}, 'wheelL', 'wheelR')"),
                Is.EqualTo(-90d).Within(1e-6),
                $"{b.Pick}'s axle ground bearing at a quarter turn is no longer −90.00°. Positive " +
                "means CLOCKWISE, and a mirrored heading map draws her towed nose-first.");

            // --- oracle 2: the centreline nose direction, same quarter turn -----------------------
            Assert.That(host.EvaluateNumber($"__noseDx({Pose(b)}, 'rear', 'kingpin')"),
                Is.LessThan(0d),
                $"{b.Pick}'s rear→kingpin screen dx went positive at a quarter turn, which reads " +
                "CLOCKWISE and disagrees with the abeam pair. Do not guess between two analytic " +
                "oracles — render her beside a registered reference and compare bearings.");

            // --- oracle 3: the stepped form, opposite sign convention by construction -------------
            Assert.That(host.EvaluateNumber($"__meanStep({Pose(b)}, 'wheelL', 'wheelR')"),
                Is.EqualTo(45d).Within(1e-4),
                "the mean bearing step across the eight headings is no longer +45°.");

            Assert.That(host.EvaluateNumber($"__maxStepDeviation({Pose(b)}, 'wheelL', 'wheelR')"),
                Is.LessThan(1e-6),
                "the eight bearing steps are no longer uniform — that zero is what proves the " +
                "÷sin(elev) un-squash.");
        }

        // =============================================================================================
        //  5. THE MATERIAL CENSUS — and the (file, pick) trap it makes concrete
        // =============================================================================================

        /// <summary>
        /// ⭐ Every body fits the facet shader with room to spare — the four are 8, 8, 11 and 11
        /// used ramps against a <c>float4[16]</c> cap. Measured before a bake is attempted, never
        /// discovered during one.
        /// </summary>
        [Test]
        public void EveryBodyFitsTheFacetShaderWithHeadroom([ValueSource(nameof(Bodies))] Body b)
        {
            using IRigScriptHost host = BuilderHost();

            Assert.That(host.EvaluateNumber($"__used({Pose(b)}).length"),
                Is.EqualTo((double)b.UsedRamps),
                $"{b.Pick} paints a different number of ramps. Over {ShaderRampCap} she is " +
                "unplaceable; under it, something stopped painting.");

            Assert.That(host.EvaluateNumber($"__used({Pose(b)}).length"),
                Is.LessThanOrEqualTo((double)ShaderRampCap));

            Assert.That(host.EvaluateNumber(
                    $"__unionUsed([{Pose(b)},{Pose(b, "night:true")},{Pose(b, "gear:0")}," +
                    $"{Pose(b, "barnL:1,barnR:1")},{Pose(b, "headboard:false")}," +
                    $"{Pose(b, "mudflaps:false")},{Pose(b, "headboard:true,mudflaps:true")}]).length"),
                Is.EqualTo((double)b.UsedRampsAllBuilds),
                $"{b.Pick}'s all-builds ramp union changed. This is the number a ONE-MESH " +
                "day-and-night trailer would need, and all four are comfortably inside the cap — " +
                "unlike three of the five road vehicles.");
        }

        /// <summary>
        /// ⭐⭐ <b>THE (file, pick) TRAP, MADE CONCRETE.</b> All four bodies share ONE
        /// <c>makeMats</c> table — byte-identical, 14 keys, same order — but they do not paint the
        /// same subset of it, and a <c>Reconstructions</c> entry is keyed by FILE and can produce
        /// only one table.
        ///
        /// <para><b>Measured, 2026-08-27:</b> filtering at <c>resolve({})</c> — which is
        /// <c>reefer53</c> — drops <c>wood</c>, and <c>wood</c> is named by nothing except the
        /// flatbed deck. The face packer resolves an unknown material name to index 0, so both
        /// flatbeds would have shipped with their planked lumber decks painted body colour. One
        /// face, and it is the entire point of a flatbed.</para>
        ///
        /// <para>So the committed entry unions the used set over EVERY body — the zodiac's pattern,
        /// for the zodiac's reason. Twelve ramps, <c>paint</c> first and used, and the two dropped
        /// (<c>glass</c>, <c>glow</c>) belong to a night pass this mesh does not carry.</para>
        /// </summary>
        [Test]
        public void OneRampTableServesAllFourBodies_AndFilteringAtOneOfThemLosesTheFlatbedDeck()
        {
            using IRigScriptHost host = BuilderHost();

            // All four declare the same table — so the only body-dependent part is the FILTER.
            string first = host.EvaluateString($"JSON.stringify(__mats({Pose(Bodies[0])}))");
            foreach (Body b in Bodies)
                Assert.That(host.EvaluateString($"JSON.stringify(__mats({Pose(b)}))"), Is.EqualTo(first),
                    $"{b.Pick}'s ramp table differs from the others'. One per-file reconstruction " +
                    "can only produce one table; if the bodies now paint from different tables, " +
                    "the entry has to become per-body and Reconstructions cannot express that.");

            // The trap, measured rather than described: the default-body filter loses `wood`.
            Assert.That(host.EvaluateBool(
                    $"__used({Pose(Bodies[0])}).indexOf('wood') >= 0"), Is.True,
                "the flatbed no longer paints 'wood'. It is the set's one new material and the " +
                "reason the ramp table is unioned over all four bodies.");

            Assert.That(host.EvaluateBool($"__used({Pose(Bodies[3])}).indexOf('wood') >= 0"), Is.False,
                "the reefer now paints 'wood' too. If both classes name it, the default-body filter " +
                "would no longer drop it — which would remove the concrete evidence for the union, " +
                "not the reason for it.");

            // And the committed reconstruction really does keep it.
            Assert.That(RigMeshSymbols.IsReconstructed(RigPath, "MATS"), Is.True,
                "trailerIsoRig.js has no MATS reconstruction — it has no MATS const either, so the " +
                "bake would fail outright with \"MATS is not defined\".");

            string widened = RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Full(RigPath)), Global, new[] { "build", "makeMats", "MATS" },
                RigPath);

            using IRigScriptHost bakeHost = RigScriptHostFactory.Create();
            bakeHost.Execute(widened);

            Assert.That(bakeHost.EvaluateNumber($"Object.keys({Global}.MATS).length"), Is.EqualTo(12d),
                "the reconstructed table is no longer twelve ramps. It is the union of what all " +
                "four bodies paint; a smaller number means a body's ramps are being dropped, and " +
                "dropped ramps resolve to index 0.");

            Assert.That(bakeHost.EvaluateString($"Object.keys({Global}.MATS)[0]"), Is.EqualTo("paint"),
                "'paint' is no longer index 0 of the reconstructed table — which is where the face " +
                "packer sends an unknown material name.");

            Assert.That(bakeHost.EvaluateBool($"Object.keys({Global}.MATS).indexOf('wood') >= 0"),
                Is.True,
                "⚠️⚠️ 'wood' is gone from the reconstructed table. That is the exact failure this " +
                "union exists to prevent: the flatbed deck resolves to index 0 and renders as body " +
                "paint, on a trailer that otherwise looks entirely correct.");
        }

        // =============================================================================================
        //  helpers
        // =============================================================================================

        /// <summary>A JS opts literal for this body, optionally with extra axes. ⚠️ The body is
        /// written FIRST and never overridden, so an extra never retargets the measurement at
        /// another trailer.</summary>
        static string Pose(Body b, string extra = null) =>
            "{body:'" + b.Pick + "'" + (string.IsNullOrEmpty(extra) ? "" : "," + extra) + "}";

        /// <summary>Loads the rig with its privates widened onto the global by the repo's own shim,
        /// then installs helpers that take the pose (and therefore the BODY) explicitly — no helper
        /// here reads a default body, because that is the bug this fixture is about.</summary>
        static IRigScriptHost BuilderHost()
        {
            string widened = RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Full(RigPath)), Global, new[] { "build", "makeMats" }, RigPath);

            IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(widened);
            host.Execute(@"
                var __R = " + Global + @";

                // ⚠️ `__R.resolve` is QUALIFIED — the shim widens `build` onto the GLOBAL, it does
                // not put the closure's other privates in scope.
                function __faces(o){ return __R.build(__R.resolve(o)); }
                function __mats(o){ return __R.makeMats(__R.resolve(o)); }

                function __differs(p, q){
                  if (p.length !== q.length) return true;
                  for (var k = 0; k < p.length; k++)
                    for (var c = 0; c < 3; c++)
                      if (Math.abs(p[k][c] - q[k][c]) > 1e-9) return true;
                  return false;
                }
                function __moved(a, b){
                  var A = __faces(a), B = __faces(b);
                  if (A.length !== B.length) return -1;
                  var n = 0;
                  for (var i = 0; i < A.length; i++) if (__differs(A[i].v, B[i].v)) n++;
                  return n;
                }
                function __movedSet(a, b){
                  var A = __faces(a), B = __faces(b), out = [];
                  for (var i = 0; i < A.length; i++) if (__differs(A[i].v, B[i].v)) out.push(i);
                  return out;
                }
                function __inter(a, b){ var s = {}, n = 0;
                  for (var i=0;i<a.length;i++) s[a[i]]=1;
                  for (var j=0;j<b.length;j++) if (s[b[j]]) n++; return n; }

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

                // Axle stations, clustered on a 0.5 m gap. ⚠️ The centroid is taken from the pose's
                // OWN face list — a helper that read a default body would answer for reefer53 here.
                function __stations(a, b){
                  var A = __faces(a), set = __movedSet(a, b), rows = {};
                  for (var i=0;i<set.length;i++){
                    var p = A[set[i]].v, c = 0;
                    for (var k=0;k<p.length;k++) c += p[k][1];
                    var y = Math.round(c / p.length * 100) / 100;
                    rows[y] = (rows[y] || 0) + 1;
                  }
                  var ys = Object.keys(rows).map(Number).sort(function(u,v){ return u-v; });
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

                // A stable digest over {mat, b, db, vertices@1e-6} in face order. Count is what a
                // silent fallback reproduces perfectly, so identity is hashed, not counted.
                function __geometryHash(o){
                  var F = __faces(o), s = [];
                  for (var i=0;i<F.length;i++){
                    var f = F[i];
                    s.push(f.mat, f.b, f.db);
                    for (var k=0;k<f.v.length;k++)
                      for (var c=0;c<3;c++) s.push(Math.round(f.v[k][c] * 1e6));
                  }
                  var str = s.join(','), h1 = 0x811c9dc5, h2 = 0x01000193;
                  for (var n=0;n<str.length;n++){
                    h1 = ((h1 ^ str.charCodeAt(n)) >>> 0) * 16777619 >>> 0;
                    h2 = ((h2 + str.charCodeAt(n) * (n + 1)) >>> 0);
                  }
                  return (h1 >>> 0).toString(16) + '-' + (h2 >>> 0).toString(16) + '-' + str.length;
                }

                function __used(o){
                  var F = __faces(o), s = {}, M = __mats(o), out = [];
                  for (var i = 0; i < F.length; i++) s[F[i].mat] = 1;
                  for (var k in M) if (s[k]) out.push(k);   // in the rig's own MATS key order
                  return out;
                }
                function __unionUsed(list){
                  var M = __mats(list[0]), seen = {}, out = [];
                  for (var i=0;i<list.length;i++){ var u = __used(list[i]);
                    for (var j=0;j<u.length;j++) seen[u[j]] = 1; }
                  for (var k in M) if (seen[k]) out.push(k);
                  return out;
                }

                // --- azimuth. Every call takes the pose, so every answer is THIS body's. ----------
                function __sinElev(){ return Math.sin(__R.defaultElev * Math.PI / 180); }
                function __abeamBearing(o, l, r){
                  var A = __R.anchors(2, o);
                  return Math.atan2((A[r].y - A[l].y) / __sinElev(), A[r].x - A[l].x) * 180 / Math.PI;
                }
                function __noseDx(o, aft, fore){
                  var A = __R.anchors(2, o);
                  return A[fore].x - A[aft].x;
                }
                function __steps(o, l, r){
                  var b = [], i;
                  for (i = 0; i < 8; i++) {
                    var A = __R.anchors(i, o);
                    // ⚠️ Δy NEGATED — deliberately the opposite sign convention to __abeamBearing.
                    b.push(Math.atan2(-(A[r].y - A[l].y) / __sinElev(), A[r].x - A[l].x) * 180 / Math.PI);
                  }
                  var s = [];
                  for (i = 1; i < 8; i++) { var d = b[i] - b[i-1];
                    while (d > 180) d -= 360; while (d < -180) d += 360; s.push(d); }
                  return s;
                }
                function __meanStep(o, l, r){
                  var s = __steps(o, l, r), t = 0;
                  for (var i=0;i<s.length;i++) t += s[i];
                  return t / s.length;
                }
                function __maxStepDeviation(o, l, r){
                  var s = __steps(o, l, r), m = __meanStep(o, l, r), w = 0;
                  for (var i=0;i<s.length;i++) w = Math.max(w, Math.abs(s[i] - m));
                  return w;
                }");
            return host;
        }
    }
}
