using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE COASTAL HERITAGE PASS — the manors, and the stairs that have to hold a person.</b>
    ///
    /// <para>Every number here is measured in the repo's own engine against the rigs as committed,
    /// and every room is the room the bake actually asks for: the cases come from
    /// <see cref="InteriorKit.RoomSet"/> and the options from
    /// <see cref="InteriorBakeMenu.OptionsLiteralFor"/>, never re-typed. A guard that restated the
    /// drop's own figures would be a second transcription agreeing with the first.</para>
    ///
    /// <para>Three things this pass could have got wrong in silence, and where each is pinned:</para>
    /// <list type="number">
    ///   <item><b><c>storeyZ</c> STOPPED BEING A FLOOR-TO-FLOOR RISE.</b> The rig this repo shipped
    ///   against defined it as one; the new rig redefines it as this storey's floor above grade — a
    ///   flat 0.55 m on every domestic ground room. It is still a number, still finite, still on the
    ///   anchor, so the old reader would have written 0.55 over 3.1025 and dropped the upper storey
    ///   almost onto the kitchen floor without a word.
    ///   <see cref="TheStoreyHeightIsTheFloorToFloorRise_NotTheAnchorsStoreyZ"/>.</item>
    ///   <item><b>A manor with no ManorIso is a manor of size ZERO.</b> <c>ManorUnitIso.dims()</c>
    ///   answers <c>{Wd:0, Ln:0, topZ:0}</c> when its host rig has not run first — no throw, no
    ///   warning, a whole house measuring nothing.
    ///   <see cref="ManorIsoMustBeInstalledBeforeManorUnitIso"/>.</item>
    ///   <item><b>The manor's reported riser is ROUNDED.</b> A flight reports <c>rise: 0.197</c> over
    ///   18 steps — 3.546 m, four millimetres below the 3.55 m it crosses.
    ///   <see cref="AManorFlightRisesByFloorRiseOverSteps_NotByTheReportedRiser"/>.</item>
    /// </list>
    /// </summary>
    public class CoastalHeritageStairTests
    {
        const string KitFolder = "docs/art/rigs/coastal-heritage-kit";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The rooms the village walks into, as the bake menu dials them.</summary>
        static IEnumerable<InteriorKit.Build> Rooms =>
            InteriorKit.RoomSet.Where(b => !b.IsProp && !b.IsPreset);

        public static IEnumerable<string> RoomKeys =>
            InteriorKit.RoomSet.Where(b => !b.IsProp && !b.IsPreset).Select(b => b.Key).ToArray();

        /// <summary>
        /// The options literal the BAKE hands the rig for this room — read through the shipped
        /// serialiser, so a room re-dialled tomorrow is re-measured here rather than quietly
        /// diverging from a copy.
        /// </summary>
        static string OptsFor(string key) =>
            InteriorBakeMenu.OptionsLiteralFor(Rooms.First(b => b.Key == key));

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        /// <summary>A host carrying one catalog key and everything it declares ahead of itself.</summary>
        static IRigScriptHost Host(string key)
        {
            IRigScriptHost host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, RigCatalog.Get(key));
            return host;
        }

        // =================================================================================
        //  the intake — seven sources, one signature
        // =================================================================================

        /// <summary>
        /// Every rig the manifest names hashes to the bytes in the working tree. The drop's sidecars
        /// stamp <c>derivedFromRigSha256</c> over these same bytes, so a rig that drifts from its
        /// sidecar by so much as a line ending breaks the pass's own render signature — which is why
        /// <c>.gitattributes</c> pins all seven to LF, and why this hashes the LF form rather than
        /// trusting the line endings of whatever checkout it runs in.
        /// </summary>
        [Test]
        public void TheSevenSourcesHashAsTheManifestSays()
        {
            var expected = HashesFromManifest(File.ReadAllText(Full($"{KitFolder}/manifest.json")));

            Assert.That(expected.Count, Is.EqualTo(7),
                "The coastal-heritage manifest pins SEVEN rig sources. A partial intake is a broken " +
                "signature, not a smaller one (charter, integration rule 2).");

            foreach (var pair in expected)
            {
                string file = pair.Key.StartsWith("Art/", StringComparison.Ordinal)
                    ? pair.Key.Substring(4)
                    : pair.Key;
                string path = Full($"docs/art/rigs/{file}");

                Assert.That(File.Exists(path), Is.True, $"{file} is missing from docs/art/rigs/.");
                Assert.That(Sha256OfLf(path), Is.EqualTo(pair.Value),
                    $"{file} does not hash as the manifest says. Either it was edited after intake " +
                    "(the rigs run UNMODIFIED — ADR 0021 section 5) or its .gitattributes pin is gone.");
            }
        }

        // =================================================================================
        //  ManorIso before ManorUnitIso
        // =================================================================================

        /// <summary>
        /// THE LOAD ORDER, MEASURED AS A CONSEQUENCE RATHER THAN AS A DECLARATION. Installing
        /// <c>manorUnitIso</c> must bring ManorIso with it, because the unit rig reads the host rig's
        /// tier table for its own footprint — and answers a house of zero metres when it cannot.
        /// Asserting the prerequisite is merely LISTED would pass against a catalog that listed it
        /// second; asserting the manor has a SIZE cannot.
        /// </summary>
        [Test]
        public void ManorIsoMustBeInstalledBeforeManorUnitIso()
        {
            using IRigScriptHost host = Host("manorUnitIso");

            Assert.That(host.EvaluateBool("typeof ManorIso === 'object' && ManorIso !== null"), Is.True,
                "ManorIso did not install. manorUnitIso must declare it as a prerequisite, and " +
                "InstallPrerequisites runs them in DECLARED ORDER.");
            Assert.That(host.EvaluateBool("typeof ManorUnitIso === 'object' && ManorUnitIso !== null"),
                Is.True, "ManorUnitIso did not install.");

            const string dims =
                "ManorUnitIso.dims(Object.assign({},ManorUnitIso.PRESETS['mansardSeat']))";

            Assert.That(host.EvaluateNumber($"{dims}.Wd"), Is.EqualTo(13.25).Within(1e-9),
                "A manor floor measuring zero across is exactly what ManorUnitIso reports when " +
                "ManorIso has not run. It does not throw — it answers {Wd:0, Ln:0, topZ:0} and would " +
                "bake a house of nothing.");
            Assert.That(host.EvaluateNumber($"{dims}.Ln"), Is.EqualTo(17.50).Within(1e-9));
            Assert.That(host.EvaluateNumber($"{dims}.topZ"), Is.GreaterThan(0.0));
        }

        /// <summary>The companion names its own dependency in a throw; the catalog is what keeps that
        /// from being a bake-time surprise.</summary>
        [Test]
        public void TheCompanionInstallsWithPropIsoAheadOfIt()
        {
            using IRigScriptHost host = Host("coastalPass");

            Assert.That(host.EvaluateBool("typeof PropIso === 'object' && PropIso !== null"), Is.True,
                "coastalPass.js refuses by name without Art/interiorPropRig.js.");
            Assert.That(host.EvaluateBool("CoastalPass.enabled === true"), Is.True,
                "The companion ships ON. Opting out is a deliberate act, not the default.");
        }

        // =================================================================================
        //  the storey height — the quiet one
        // =================================================================================

        /// <summary>
        /// THE FIELD IS A RISE. <c>storeyHeightMetres</c> is handed to
        /// <c>InteriorLevelLayout.UpperLevelY</c>, which multiplies it to lift a whole storey. The
        /// rig's <c>anchors().storeyZ</c> used to BE that rise and now is not, so the number has to
        /// come from the gap between the two storeys' floors — which is exactly what the rig's own
        /// stair says the flight crosses.
        ///
        /// <para>Four independent expressions of one height: the floor gap, the stair contract's
        /// <c>floorRise</c>, the head of the flight, and the risers added up. They agree to the last
        /// digit on every shipped room; a drop that moves one and not the others has broken its own
        /// staircase and this is where that shows.</para>
        /// </summary>
        [Test, TestCaseSource(nameof(RoomKeys))]
        public void TheStoreyHeightIsTheFloorToFloorRise_NotTheAnchorsStoreyZ(string room)
        {
            using IRigScriptHost host = Host("interior");
            string o = OptsFor(room);
            string stair = $"InteriorIso.furnishings({o}).stair";

            double ground = host.EvaluateNumber($"InteriorIso.dims({o}).storeyZ");
            double upper = host.EvaluateNumber(
                $"InteriorIso.dims(Object.assign({{}},{o},{{storey:'upper'}})).storeyZ");
            double rise = upper - ground;

            Assert.That(rise, Is.GreaterThan(2.0).And.LessThan(3.5),
                $"{room}: a domestic storey that is not between 2 and 3.5 m is not a storey.");

            Assert.That(host.EvaluateNumber($"{stair}.floorRise"), Is.EqualTo(rise).Within(1e-9),
                $"{room}: the stair contract and the two floors disagree about the gap between them.");
            Assert.That(host.EvaluateNumber($"{stair}.top.z"), Is.EqualTo(rise).Within(1e-9),
                $"{room}: the head of the flight is not level with the floor it arrives at.");
            Assert.That(host.EvaluateNumber($"{stair}.steps * {stair}.riser"),
                Is.EqualTo(rise).Within(1e-9),
                $"{room}: the risers do not add up to the rise they cross.");

            // The regression this whole file exists for.
            double anchorStoreyZ = host.EvaluateNumber($"InteriorIso.anchors(0,{o}).storeyZ");
            Assert.That(anchorStoreyZ, Is.EqualTo(0.55).Within(1e-9),
                $"{room}: anchors().storeyZ is the GROUND FLOOR ABOVE GRADE in this rig — the sill " +
                "height, 0.55 m on every domestic room. If it changed again, re-read the reader in " +
                "InteriorRigBaker BEFORE baking anything.");
            Assert.That(anchorStoreyZ, Is.Not.EqualTo(rise).Within(1e-6),
                $"{room}: anchors().storeyZ is NOT the floor-to-floor rise in this rig. Writing it " +
                "into storeyHeightMetres drops the upper storey onto the ground floor, and nothing " +
                "throws on the way past.");
        }

        /// <summary>
        /// THE DOUBLE-RISE TRAP, from both sides. The upper sprite draws from its own floor plane at
        /// zero and is PLACED at <c>dims(upper).storeyZ</c>, an absolute height above grade; the
        /// stair's <c>top.z</c> is measured from the GROUND floor. So the two compose once — ground
        /// plus the flight IS the upper floor — and adding the flight to the upper floor instead
        /// puts the landing above the wall plate, where the charter says it will look almost right.
        /// </summary>
        [Test, TestCaseSource(nameof(RoomKeys))]
        public void TheHeadOfTheFlightIsMeasuredFromTheGroundFloor_Once(string room)
        {
            using IRigScriptHost host = Host("interior");
            string o = OptsFor(room);
            string d = $"InteriorIso.dims({o})";

            double groundZ = host.EvaluateNumber($"{d}.storeyZ");
            double upperZ = host.EvaluateNumber(
                $"InteriorIso.dims(Object.assign({{}},{o},{{storey:'upper'}})).storeyZ");
            double topZ = host.EvaluateNumber($"InteriorIso.furnishings({o}).stair.top.z");
            double wallTop = host.EvaluateNumber($"{d}.fH + {d}.wallH");

            Assert.That(groundZ + topZ, Is.EqualTo(upperZ).Within(1e-9),
                $"{room}: the ground floor plus the flight must land exactly on the upper floor.");
            Assert.That(upperZ + topZ, Is.GreaterThan(wallTop),
                $"{room}: the DOUBLED rise is supposed to clear the wall plate — that is what makes " +
                "the mistake visible rather than merely wrong. If this stops holding, the trap has " +
                "changed shape and the guard above it needs re-reading.");
        }

        /// <summary>
        /// The opt-out reverts the LOOK and not the STAIRS — the PR says so, so it is measured rather
        /// than quoted. What the companion does move is the ground room's ceiling: with the pass ON
        /// the plate drops to sit under the upper floor's slab, and <c>fH + plate + slab</c> lands on
        /// the upper floor exactly. With it off the ceiling would stand proud of the floor above.
        /// </summary>
        [Test, TestCaseSource(nameof(RoomKeys))]
        public void TheOptOutRevertsTheLookAndNotTheRise(string room)
        {
            using IRigScriptHost host = Host("interior");
            string o = OptsFor(room);
            string d = $"InteriorIso.dims({o})";
            string up = $"InteriorIso.dims(Object.assign({{}},{o},{{storey:'upper'}}))";

            double riseOn = host.EvaluateNumber($"{up}.storeyZ - {d}.storeyZ");
            double plateOn = host.EvaluateNumber($"{d}.plate");
            double slab = host.EvaluateNumber($"InteriorIso.furnishings({o}).stair.slabThickness");
            double fH = host.EvaluateNumber($"{d}.fH");
            double upperZ = host.EvaluateNumber($"{up}.storeyZ");

            Assert.That(fH + plateOn + slab, Is.EqualTo(upperZ).Within(1e-9),
                $"{room}: with the companion on, the ground ceiling sits exactly one floor slab under " +
                "the storey above. That stack is what makes the two sprites meet.");

            host.Execute("CoastalPass.enabled = false;");
            double riseOff = host.EvaluateNumber($"{up}.storeyZ - {d}.storeyZ");
            double floorRiseOff = host.EvaluateNumber($"InteriorIso.furnishings({o}).stair.floorRise");
            double plateOff = host.EvaluateNumber($"{d}.plate");
            host.Execute("CoastalPass.enabled = true;");

            Assert.That(riseOff, Is.EqualTo(riseOn).Within(1e-9),
                $"{room}: turning the companion off must not move the floor above. The structural " +
                "stair corrections live in the HOST rig (charter, integration rule 3).");
            Assert.That(floorRiseOff, Is.EqualTo(riseOn).Within(1e-9),
                $"{room}: the stair contract is structural. It does not belong to the aesthetic pass.");
            Assert.That(plateOff, Is.GreaterThan(plateOn),
                $"{room}: the pass is supposed to LOWER the ground ceiling under the upper floor. " +
                "If these are equal it is no longer reaching this room, and the look is not revertible " +
                "because it was never applied.");
        }

        // =================================================================================
        //  the manor flights
        // =================================================================================

        /// <summary>
        /// A REPORTED RISER IS ROUNDED AND A COLLIDER IS NOT. The mansard manor's main flight reports
        /// 18 steps at 0.197 m — 3.546 m against the 3.55 m it actually crosses. Build treads from the
        /// reported riser and the top one lands four millimetres under the floor it serves; build them
        /// from <c>floorRise / steps</c> and it does not.
        /// </summary>
        [Test]
        public void AManorFlightRisesByFloorRiseOverSteps_NotByTheReportedRiser()
        {
            using IRigScriptHost host = Host("manorUnitIso");
            host.Execute(
                "var __p = ManorUnitIso.plan(Object.assign({},ManorUnitIso.PRESETS['mansardSeat']));" +
                "var __s = __p.levels[0].stairs[0];");

            double steps = host.EvaluateNumber("__s.steps");
            double reported = host.EvaluateNumber("__s.rise");
            double floorRise = host.EvaluateNumber("__s.floorRise");

            Assert.That(steps, Is.EqualTo(18).Within(1e-9));
            Assert.That(floorRise, Is.EqualTo(3.55).Within(1e-9));
            Assert.That(floorRise / steps, Is.EqualTo(0.19722222222222222).Within(1e-12),
                "This is the riser a collider must be cut to.");
            Assert.That(steps * reported, Is.Not.EqualTo(floorRise).Within(1e-9),
                "If the reported riser now closes exactly, the rounding is gone — and the warning in " +
                "RigCatalog.CoastalHeritage.cs should go with it.");
            Assert.That(Math.Abs(steps * reported - floorRise), Is.LessThan(0.01),
                "The gap is meant to be a rounding artefact, not a different staircase.");
        }

        /// <summary>
        /// The floor a flight arrives at carries the hole it arrives through. Every void on a level
        /// names a flight on the level below, every flight crosses exactly the gap between the two
        /// floors, and the top level has no flight leaving it — the pairing that lets an opening be
        /// GENERATED from the plan rather than authored beside it.
        /// </summary>
        [Test]
        public void EveryManorVoidNamesTheFlightBeneathIt(
            [Values("mansardSeat", "mansardGrand", "stoneSeat", "stoneGrand",
                    "receptionOnly", "chamberFloor", "atticFloor")] string preset)
        {
            using IRigScriptHost host = Host("manorUnitIso");
            host.Execute(
                $"var __p = ManorUnitIso.plan(Object.assign({{}},ManorUnitIso.PRESETS['{preset}']));");

            int levels = (int)host.EvaluateNumber("__p.levels.length");
            Assert.That(levels, Is.GreaterThanOrEqualTo(2), $"{preset}: a manor has storeys.");

            Assert.That(host.EvaluateNumber("__p.levels[0].voids.length"), Is.EqualTo(0),
                $"{preset}: the ground floor has nothing arriving from beneath it.");
            Assert.That(host.EvaluateNumber($"__p.levels[{levels - 1}].stairs.length"), Is.EqualTo(0),
                $"{preset}: the top floor has nowhere further to climb.");

            for (int i = 1; i < levels; i++)
            {
                Assert.That(host.EvaluateNumber($"__p.levels[{i}].voids.length"), Is.GreaterThan(0),
                    $"{preset} level {i}: a floor reached by a flight and carrying no opening is a " +
                    "floor you arrive underneath.");

                bool paired = host.EvaluateBool(
                    $"__p.levels[{i}].voids.every(function(v){{ return __p.levels[{i - 1}]" +
                    ".stairs.some(function(s){ return s.id === v.id; }); })");
                Assert.That(paired, Is.True,
                    $"{preset} level {i}: a void names a flight the level below does not have. The " +
                    "opening and the stair are generated from the same id — an unpaired one is a hole " +
                    "in a floor with no way up to it.");

                double below = host.EvaluateNumber($"__p.levels[{i - 1}].floorZ");
                double here = host.EvaluateNumber($"__p.levels[{i}].floorZ");
                double crosses = host.EvaluateNumber($"__p.levels[{i - 1}].stairs[0].floorRise");
                Assert.That(here - below, Is.EqualTo(crosses).Within(1e-9),
                    $"{preset} level {i}: the flight does not cross the gap between the two floors. " +
                    "The charter's correction was to derive risers from the ACTUAL next floor.");
            }
        }

        // =================================================================================
        //  helpers
        // =================================================================================

        /// <summary>The manifest's <c>rigHashes</c> block, read without a JSON dependency this
        /// assembly does not have.</summary>
        static Dictionary<string, string> HashesFromManifest(string json)
        {
            int at = json.IndexOf("\"rigHashes\"", StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "manifest.json declares no rigHashes.");

            int open = json.IndexOf('{', at);
            int close = json.IndexOf('}', open);
            Assert.That(close, Is.GreaterThan(open), "manifest.json rigHashes block is not closed.");

            var found = new Dictionary<string, string>();
            foreach (string line in json.Substring(open + 1, close - open - 1).Split('\n'))
            {
                var quoted = new List<string>();
                int i = 0;
                while (quoted.Count < 2)
                {
                    int a = line.IndexOf('"', i);
                    if (a < 0) break;
                    int b = line.IndexOf('"', a + 1);
                    if (b < 0) break;
                    quoted.Add(line.Substring(a + 1, b - a - 1));
                    i = b + 1;
                }
                if (quoted.Count == 2) found[quoted[0]] = quoted[1];
            }
            return found;
        }

        /// <summary>sha256 over the file in its LF form — the bytes the drop hashed and served, which
        /// are the bytes git stores whatever this working copy looks like.</summary>
        static string Sha256OfLf(string path)
        {
            string text = File.ReadAllText(path).Replace("\r\n", "\n");
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));

            var sb = new StringBuilder(64);
            foreach (byte b in hash) sb.Append(b.ToString("x2", Inv));
            return sb.ToString();
        }
    }
}
