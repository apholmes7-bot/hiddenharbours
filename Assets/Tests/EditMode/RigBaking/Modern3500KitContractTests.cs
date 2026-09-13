using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE MODERN 3500 TRUCK KIT (Codex drop 2026-09-13) — the intake guards.</b>
    ///
    /// <para>A SECOND crew-cab dually, not a revision of the first: the drop's own README says "use
    /// this as a separate asset entry; do not overwrite the older truck's rig or reuse its sidecar",
    /// and nothing here touches <c>dually3500</c>.</para>
    ///
    /// <para><b>What this file does NOT do.</b> <see cref="VehicleRigFleetTests"/> already sweeps
    /// every row in the fleet and every sidecar in the vehicle folder — kind recognition, the
    /// baked-or-excused ledger, def/mesh presence, and that each registered vehicle's sidecar still
    /// pins her rig. A new row is swept automatically, so none of that is repeated here. What is
    /// here is the part that is specific to THIS kit and would otherwise be pinned nowhere: the
    /// seat's hash, the three-way agreement it is supposed to have, the sheet census, and the six
    /// signed sweeps that were read off the rig rather than copied from a neighbour.</para>
    /// </summary>
    public class Modern3500KitContractTests
    {
        const string KitFolder = "docs/art/rigs/modern3500-kit";
        const string RigPath = KitFolder + "/modern3500.rig.js";
        const string ContractPath = KitFolder + "/modern3500.contract.json";
        const string ReadmePath = KitFolder + "/README.md";
        const string PreviewPath = KitFolder + "/preview.html";
        const string SumsPath = KitFolder + "/reference/SHA256SUMS.txt";
        const string SidecarPath = "docs/art/rigs/gameplay/vehicles/modern3500.rig.gameplay.json";

        /// <summary>
        /// The LF-normalised sha256 the coordinator verified on the delivered bytes (2026-09-13),
        /// and the ONE number this kit is built around: it is also <c>rigSha256</c> inside the
        /// contract and <c>derivedFromRigSha256</c> inside the sidecar.
        ///
        /// <para>⚠️ If a bake ever refuses her sidecar hash, the repair is upstream in the kit — a
        /// re-cut sidecar from the rig that is actually on disk. It is NEVER to re-stamp the number
        /// on the repo side, which converts a real disagreement into a silent one. Same law as
        /// <c>VehicleRigFleet.SidecarHashRefused</c>.</para>
        /// </summary>
        const string RigSha = "d70056fedd78a92872c0e3a94c369eff9ac68462afce0606bc49460391d4159d";

        /// <summary>The sprite sheets the kit carries, exactly. A census, not a floor: a sheet that
        /// arrives without a line here is as much a defect as one that goes missing.</summary>
        static readonly string[] Sheets =
        {
            "Modern3500_doors_W.png", "Modern3500_drive_W.png", "Modern3500_gate_NE.png",
            "Modern3500_graphite_8dir.png", "Modern3500_hood_SE.png", "Modern3500_night_8dir.png",
            "Modern3500_open_8dir.png", "Modern3500_paints_SE.png", "Modern3500_pearl_8dir.png",
            "Modern3500_steer_SE.png",
        };

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        static string Sha256(byte[] b)
        {
            using var sha = SHA256.Create();
            var sb = new StringBuilder(64);
            foreach (byte x in sha.ComputeHash(b)) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// ⭐ The content digest of a file that may or may not be smudged. <b>A Git LFS pointer's
        /// <c>oid sha256:</c> IS the sha256 of the real content</b> — so a checkout with the filter
        /// on and one with it off answer the same number, and this suite can pin a PNG whose bytes
        /// it may never actually see. CI and a developer box differ on exactly this.
        /// </summary>
        static string ContentDigest(string full)
        {
            byte[] raw = File.ReadAllBytes(full);
            return LfsOid(raw) ?? Sha256(raw);
        }

        static string LfsOid(byte[] raw)
        {
            if (raw.Length > 1024) return null;                 // a pointer is a few hundred bytes
            string text = Encoding.UTF8.GetString(raw);
            if (!text.StartsWith("version https://git-lfs.github.com/spec/v1", StringComparison.Ordinal))
                return null;
            foreach (string line in text.Split('\n'))
            {
                const string tag = "oid sha256:";
                int at = line.IndexOf(tag, StringComparison.Ordinal);
                if (at >= 0) return line.Substring(at + tag.Length).Trim();
            }
            return null;
        }

        /// <summary>The sums file as (path relative to reference/, digest), comments dropped.</summary>
        static Dictionary<string, string> Sums()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string raw in File.ReadAllLines(Full(SumsPath)))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int sp = line.IndexOf(' ');
                map[line.Substring(sp).Trim()] = line.Substring(0, sp);
            }
            return map;
        }

        static string TopLevelKind(string json)
        {
            int at = json.IndexOf("\"kind\"", StringComparison.Ordinal);
            if (at < 0) return null;
            int i = json.IndexOf(':', at);
            if (i < 0) return null;
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != '"') return null;
            int end = json.IndexOf('"', i + 1);
            return end < 0 ? null : json.Substring(i + 1, end - i - 1);
        }

        static VehicleRigFleet.Vehicle Her() =>
            VehicleRigFleet.Vehicles.Single(v => v.Key == "modern3500");

        // =========================================================================================
        //  1. THE PIN, AND THE THREE PLACES IT HAS TO AGREE
        // =========================================================================================

        [Test]
        public void TheRigLandedOnThePinTheSeatVerified()
        {
            FileAssert.Exists(Full(RigPath));
            Assert.That(Sha256(File.ReadAllBytes(Full(RigPath))), Is.EqualTo(RigSha),
                $"{RigPath} is not the file the drop shipped. The rig is the authority for every " +
                "number in this suite and for every axis on her fleet row; a rig that has moved " +
                "invalidates all of them at once.");
        }

        /// <summary>
        /// The rig is hashed as WORKING-TREE bytes, so a checkout that normalised it to CRLF would
        /// break the pin on line endings alone with the truck unchanged. <c>.gitattributes</c> pins
        /// this kit's text files to <c>eol=lf</c> by name for exactly that reason.
        /// </summary>
        [Test]
        public void NoTextFileInTheKitCarriesACarriageReturn()
        {
            foreach (string path in new[] { RigPath, ContractPath, PreviewPath, ReadmePath, SidecarPath })
                Assert.That(File.ReadAllBytes(Full(path)).Count(b => b == (byte)'\r'), Is.Zero,
                    $"{path} has CRLF endings — check its `eol=lf` line in .gitattributes.");
        }

        /// <summary>
        /// The whole point of the pin: the contract and the sidecar each claim to have been cut from
        /// a specific rig, and that rig is the one on disk. A sidecar cut from a truck the rig no
        /// longer draws is the fleet's recurring failure, and it is found in play rather than here.
        /// </summary>
        [Test]
        public void TheContractAndTheSidecarBothPinTheRigThatIsOnDisk()
        {
            string onDisk = Sha256(File.ReadAllBytes(Full(RigPath)));

            string contract = File.ReadAllText(Full(ContractPath));
            Assert.That(contract, Does.Contain("\"rigSha256\": \"" + onDisk + "\""),
                $"{ContractPath} does not pin the rig beside it. ⚠️ The repair is to re-cut the " +
                "contract upstream from the rig that is actually here — never to re-stamp this " +
                "number, which turns a real disagreement into a silent one.");

            string sidecar = File.ReadAllText(Full(SidecarPath));
            Assert.That(sidecar, Does.Contain("\"derivedFromRigSha256\": \"" + onDisk + "\""),
                $"{SidecarPath} does not pin the rig it was derived from. Same repair, same ⚠️.");
        }

        // =========================================================================================
        //  2. THE ONE EDIT INTAKE MADE
        // =========================================================================================

        /// <summary>
        /// The drop shipped the sidecar with no TOP-LEVEL <c>kind</c> — its only <c>kind</c> was the
        /// prose <c>"crew-cab dually"</c> nested inside <c>BODY</c>, which is a description of the
        /// body style and not a token this repo knows. Intake added <c>"road_vehicle"</c> at the top
        /// level; the drop file in <c>C:/hh-drops/</c> stays byte-identical.
        ///
        /// <para>Without it she is simply INVISIBLE to the fleet coverage law rather than red in it,
        /// which is the failure worth a test of its own: a guard that never runs looks exactly like
        /// a guard that passes. The token is read the same way the fleet's own sweep reads it — the
        /// first <c>"kind"</c> in the document — so this asserts what that sweep will see.</para>
        /// </summary>
        [Test]
        public void TheSidecarDeclaresTheRoadVehicleKindThatIntakeAdded()
        {
            string kind = TopLevelKind(File.ReadAllText(Full(SidecarPath)));

            Assert.That(kind, Is.EqualTo("road_vehicle"),
                "The top-level kind token is missing or is the nested BODY prose. Intake adds it; " +
                "see the PR body for the §1 normalisation.");

            Assert.That(VehicleKinds.TryFromToken(kind, out VehicleKind parsed), Is.True,
                $"`{kind}` is not a token VehicleKinds recognises — that class is the one place a " +
                "shipped token becomes a kind, so it is the authority here, not this string.");
            Assert.That(parsed, Is.EqualTo(VehicleKind.RoadVehicle));
        }

        // =========================================================================================
        //  3. THE SHEETS — CENSUS, LFS, AND THE BLANKET-GLOB TRAP
        // =========================================================================================

        [Test]
        public void EverySheetIsPresentAndPinnedBySha256sums()
        {
            Dictionary<string, string> sums = Sums();

            foreach (string sheet in Sheets)
            {
                string full = Full(KitFolder + "/reference/" + sheet);
                FileAssert.Exists(full);
                Assert.That(sums.ContainsKey(sheet), Is.True,
                    $"{sheet} is in the kit but not in SHA256SUMS.txt — an unpinned sheet is a sheet " +
                    "that can be replaced without anyone noticing.");
                Assert.That(ContentDigest(full), Is.EqualTo(sums[sheet]),
                    $"{sheet} does not match its recorded digest.");
            }
        }

        /// <summary>
        /// A census in both directions. A sheet that arrives with no line in <see cref="Sheets"/> is
        /// as much a defect as a missing one: the kit is a fixed delivery, and an extra file means
        /// either a stray export or a drop that shipped more than the contract describes.
        /// </summary>
        [Test]
        public void TheReferenceFolderHoldsTheseSheetsAndNoOthers()
        {
            string[] onDisk = Directory
                .GetFiles(Full(KitFolder + "/reference"), "*.png")
                .Select(Path.GetFileName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.That(onDisk, Is.EqualTo(Sheets.OrderBy(n => n, StringComparer.Ordinal).ToArray()));
        }

        /// <summary>
        /// ⚠️ <b>The trap this kit was one line away from.</b> <c>*.png  lfs</c> at the top of
        /// <c>.gitattributes</c> carries <c>-text</c>, which is what keeps a PNG out of the
        /// end-of-line machinery. A LATER blanket glob over this kit — <c>docs/art/rigs/
        /// modern3500-kit/** text eol=lf</c>, the obvious way to hold the rig's LF hash — would
        /// override that <c>-text</c> and translate CRLF inside the binaries, corrupting every
        /// sheet on any checkout that normalises. So the kit's text files are pinned BY NAME.
        ///
        /// <para>This test reads <c>.gitattributes</c> rather than the files, because the corruption
        /// happens on someone else's checkout, not on the one running the test.</para>
        /// </summary>
        [Test]
        public void NoBlanketRuleAppliesTextTranslationToThisKit()
        {
            foreach (string raw in File.ReadAllLines(Full(".gitattributes")))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                int sp = line.IndexOfAny(new[] { ' ', '\t' });
                if (sp < 0) continue;
                string pattern = line.Substring(0, sp);
                string attrs = line.Substring(sp);

                bool sweepsTheKit = pattern.Contains("modern3500-kit") &&
                                    (pattern.EndsWith("**", StringComparison.Ordinal) ||
                                     pattern.EndsWith("*", StringComparison.Ordinal));
                if (!sweepsTheKit) continue;

                Assert.That(attrs, Does.Not.Contain("text"),
                    $"`{line}` applies end-of-line translation across the whole kit, binaries " +
                    "included. Pin the text files by name instead — see the comment beside them.");
            }
        }

        /// <summary>
        /// "Every binary through LFS" is a claim about <c>.gitattributes</c>, not about the bytes in
        /// front of the test: a smudged checkout and an unsmudged one hold different bytes for the
        /// same correctly-tracked file. So the rule is what gets asserted, and the bytes are only
        /// checked for being one of the two shapes a tracked PNG is ever allowed to have.
        /// </summary>
        [Test]
        public void EverySheetIsCarriedByLfs()
        {
            bool pngRule = File.ReadAllLines(Full(".gitattributes"))
                .Select(l => l.Trim())
                .Any(l => l.StartsWith("*.png", StringComparison.Ordinal) && l.Contains("lfs"));

            Assert.That(pngRule, Is.True,
                "No `*.png … lfs` rule in .gitattributes — the kit's sheets would land in the repo " +
                "as raw blobs. This kit adds no PNG rule of its own; it relies on that global one.");

            foreach (string sheet in Sheets)
            {
                byte[] raw = File.ReadAllBytes(Full(KitFolder + "/reference/" + sheet));
                bool png = raw.Length > 8 && raw[0] == 0x89 && raw[1] == (byte)'P' &&
                           raw[2] == (byte)'N' && raw[3] == (byte)'G';

                Assert.That(LfsOid(raw) != null || png, Is.True,
                    $"{sheet} is neither a PNG nor an LFS pointer — it is something else entirely.");
            }
        }

        /// <summary>The sums file covers the kit's text files too, so the README, the contract and
        /// the preview cannot drift from what the kit says it shipped.</summary>
        [Test]
        public void TheSumsFileCoversTheTextFilesToo()
        {
            Dictionary<string, string> sums = Sums();

            foreach (string rel in new[] { "../README.md", "../modern3500.contract.json",
                                           "../preview.html", "../modern3500.rig.js",
                                           "../../gameplay/vehicles/modern3500.rig.gameplay.json" })
            {
                Assert.That(sums.ContainsKey(rel), Is.True, $"{rel} has no line in SHA256SUMS.txt.");
                Assert.That(Sha256(File.ReadAllBytes(Full(KitFolder + "/reference/" + rel))),
                    Is.EqualTo(sums[rel]), $"{rel} no longer matches its recorded digest.");
            }
        }

        // =========================================================================================
        //  4. THE FLEET ROW — SHE IS A SECOND TRUCK, NOT A REVISION OF THE FIRST
        // =========================================================================================

        /// <summary>
        /// The row points at the kit this suite has just pinned, and at NOTHING the older Dually
        /// owns. The drop's README asked for exactly this and it is the one way the intake could
        /// have gone quietly wrong: a row that reuses <c>dually3500</c>'s mesh id, def path or
        /// sidecar would bake the new truck over the old one and both would still be "registered".
        /// </summary>
        [Test]
        public void HerRowNamesHerOwnKitAndNothingTheDuallyOwns()
        {
            VehicleRigFleet.Vehicle her = Her();

            Assert.That(her.ScriptPath, Is.EqualTo(RigPath));
            Assert.That(her.SidecarPath, Is.EqualTo(SidecarPath));
            Assert.That(her.GlobalName, Is.EqualTo("ModernTruck3500"));
            Assert.That(her.VehicleId, Is.EqualTo("vehicle.modern_3500"));
            Assert.That(her.MeshId, Is.EqualTo("vehiclemesh.modern_3500"));

            VehicleRigFleet.Vehicle dually = VehicleRigFleet.Vehicles.Single(v => v.Key == "dually3500");
            foreach (var (mine, hers, what) in new[]
                     {
                         (her.ScriptPath,     dually.ScriptPath,     "rig"),
                         (her.SidecarPath,    dually.SidecarPath,    "sidecar"),
                         (her.GlobalName,     dually.GlobalName,     "global name"),
                         (her.MeshAssetPath,  dually.MeshAssetPath,  "mesh asset"),
                         (her.MeshId,         dually.MeshId,         "mesh id"),
                         (her.VehicleDefPath, dually.VehicleDefPath, "def asset"),
                         (her.VehicleId,      dually.VehicleId,      "vehicle id"),
                     })
                Assert.That(mine, Is.Not.EqualTo(hers),
                    $"The Modern 3500 shares the older Dually's {what}. She is a SECOND truck — the " +
                    "drop's README is explicit that she must not overwrite it.");
        }

        /// <summary>
        /// ⭐ <b>The six signed sweeps, which are the part a copied row gets wrong.</b> These are the
        /// declarations the baker checks the art against: it measures each leaf's rotation with a
        /// plane Procrustes fit and refuses the bake if the measurement disagrees with the number
        /// here, or if the leaf is not rigid to 1e-4 m.
        ///
        /// <para>⚠️ <b>The hood's sign is the trap.</b> The two conventionals in this fleet tilt
        /// their hoods FORWARD about a pin ahead of the sheet, so their sweeps are NEGATIVE (−70°,
        /// −72°). Hers is a clamshell hinged back at the COWL with the metal AHEAD of the pin, so
        /// the same upward motion is <b>+48°</b> in the (y, z) plane a lateral hinge turns in.
        /// Copying a neighbour's sign would drive her bonnet down through the engine bay — and the
        /// bake would refuse, which is the good outcome. This test is what makes the refusal a
        /// sentence rather than a puzzle.</para>
        /// </summary>
        [Test]
        public void HerSixLeavesCarryTheSweepsTheRigItselfDraws()
        {
            var expected = new (string Slot, VehicleHingeAxis Axis, float Sweep)[]
            {
                ("DoorFL", VehicleHingeAxis.Vertical, -68f),
                ("DoorFR", VehicleHingeAxis.Vertical, +68f),
                ("DoorRL", VehicleHingeAxis.Vertical, -68f),
                ("DoorRR", VehicleHingeAxis.Vertical, +68f),
                ("Hood",   VehicleHingeAxis.Lateral,  +48f),
                ("Gate",   VehicleHingeAxis.Lateral,  +92f),
            };

            foreach (var (slot, axis, sweep) in expected)
            {
                // ⚠️ Axis is a STRUCT: SingleOrDefault would hand back a blank axis and an
                // Is.Not.Null on it would pass forever. Count the matches instead.
                VehicleRigFleet.Axis[] found = Her().Axes.Where(x => x.Slot == slot).ToArray();
                Assert.That(found.Length, Is.EqualTo(1),
                    $"Expected exactly one {slot} axis on her row; found {found.Length}.");
                VehicleRigFleet.Axis a = found[0];
                Assert.That(a.HingeAxis, Is.EqualTo(axis), $"{slot} turns in the wrong plane.");
                Assert.That(a.SweepDegrees, Is.EqualTo(sweep).Within(1e-4f),
                    $"{slot} declares {a.SweepDegrees}°, not {sweep}°. ⚠️ The repair is to read the " +
                    "rig, not to copy a neighbour — see the hood note on this test.");
            }
        }

        /// <summary>
        /// Six wheels, and the four probes that drive them. A dually's rear corners are two wheels
        /// on one hub, so the rig moves them together off one probe per side; the six-wheel claim
        /// lives in the art, and what the fleet row owes is one roll axis per PROBE plus the two
        /// steer knuckles — in that order, specific first, or the knuckles swallow the front wheels.
        /// </summary>
        [Test]
        public void HerWheelAxesArePartitionedSpecificFirst()
        {
            string[] order = Her().Axes.Select(a => a.Slot).ToArray();

            foreach (string wheel in new[] { "WheelFL", "WheelFR", "WheelRL", "WheelRR" })
                Assert.That(order, Does.Contain(wheel));

            foreach (string knuckle in new[] { "KnuckleFL", "KnuckleFR" })
            {
                Assert.That(order, Does.Contain(knuckle));
                Assert.That(Array.IndexOf(order, knuckle), Is.GreaterThan(Array.IndexOf(order, "WheelFL")),
                    $"{knuckle} is declared before the front wheels. ⚠️ Each axis claims only what no " +
                    "earlier one took, so a steer axis listed first takes both front corners whole " +
                    "and the roll axes come up empty.");
            }
        }
    }
}
