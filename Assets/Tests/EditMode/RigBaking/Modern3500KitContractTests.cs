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
        /// The LF-normalised sha256 of her rig, and the ONE number this kit is built around: it is
        /// also <c>rigSha256</c> inside the contract and <c>derivedFromRigSha256</c> inside the
        /// sidecar.
        ///
        /// <para><b>Re-pinned 2026-09-18, by ruling.</b> The coordinator verified <c>3eb16400…</c>
        /// on the delivered bytes (2026-09-13; re-issued 2026-09-16). The owner's ruling D1 of
        /// 2026-09-18 then cut her two <c>lettering(…'RAM'…)</c> draws, the grille badge and the
        /// tailgate badge, and nothing else: 2802 faces to 2738, every door, the hood and the gate
        /// still one rigid leaf about its pin to 1e-6 m on node. The contract and the sidecar were
        /// re-derived from THAT rig rather than re-typed, so all three moved together.</para>
        ///
        /// <para>⚠️ If a bake ever refuses her sidecar hash, the repair is a sidecar re-cut from the
        /// rig that is actually on disk. It is NEVER to re-stamp the number on the repo side, which
        /// converts a real disagreement into a silent one. Same law as
        /// <c>VehicleRigFleet.SidecarHashRefused</c>.</para>
        /// </summary>
        const string RigSha = "0601bc98435533aa024804532725c491d3cdc31b86b506706cd4cff79d3ced27";

        /// <summary>Her def and her id, by the owner's ruling of 2026-09-18. The def is the baker's
        /// to create at this path; the id is append-only from the day it ships.</summary>
        const string HerDefPath = "Assets/_Project/Data/Vehicles/Modern3500.asset";
        const string HerVehicleId = "vehicle.modern_3500";

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
                $"{RigPath} is not the rig this suite pins: the drop's, with its two badge draws cut " +
                "by the owner's ruling of 2026-09-18. The rig is the authority for every number in " +
                "this suite and for every axis on her fleet row; a rig that has moved invalidates " +
                "all of them at once.");
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
            Assert.That(her.MeshId, Is.EqualTo("vehiclemesh.modern_3500"));

            VehicleRigFleet.Vehicle dually = VehicleRigFleet.Vehicles.Single(v => v.Key == "dually3500");
            foreach (var (mine, hers, what) in new[]
                     {
                         (her.ScriptPath,     dually.ScriptPath,     "rig"),
                         (her.SidecarPath,    dually.SidecarPath,    "sidecar"),
                         (her.GlobalName,     dually.GlobalName,     "global name"),
                         (her.MeshAssetPath,  dually.MeshAssetPath,  "mesh asset"),
                         (her.MeshId,         dually.MeshId,         "mesh id"),
                         (her.VehicleDefPath, dually.VehicleDefPath, "def"),
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
        // =========================================================================================
        //  HER LEDGER — registered, baked, and wearing her own def and id since 2026-09-18
        // =========================================================================================

        /// <summary>
        /// ⭐⭐ <b>She is BAKED, her excuse is DELETED, and the pairing is still the tripwire.</b> Her
        /// re-issued rig (2026-09-16) folds 27 colour ramps to 16 upstream, inside her own
        /// <c>build()</c>, so she fits the facet shader — the measurement lives in
        /// <see cref="Modern3500KitProbeTests"/> — and her <c>VehicleRigFleet.NotBaked</c> entry left
        /// the way an entry there should: deleted, not reworded.
        ///
        /// <para>This replaces <c>HerBakeIsExcused_WithAReasonThatCarriesTheMeasurement</c>, which
        /// asserted the opposite of every line below; its premise was discharged by the fold. Still
        /// asserted in BOTH directions on purpose: a Modern 3500 listed in <c>Baked</c> while an excuse
        /// or a hash refusal still stands is two tables disagreeing about the same truck, and the
        /// fleet bake would either skip her or refuse her while this ledger claims a mesh.</para>
        /// </summary>
        [Test]
        public void SheIsBaked_AndHerExcuseWasDeletedNotReworded()
        {
            Assert.That(VehicleRigFleet.Baked, Does.Contain("modern3500"),
                "she is no longer listed as baked. Her rig fits 16 ramps now — if a later re-issue " +
                "put her back over the cap, the refusal belongs on a NotBaked entry that carries " +
                "the new measurement, and Modern3500KitProbeTests says whether it did.");

            Assert.That(VehicleRigFleet.NotBaked.ContainsKey("modern3500"), Is.False,
                "she is BOTH baked and excused. The excuse is stale — delete it; entries there " +
                "leave by deletion, never by rewording.");

            Assert.That(VehicleRigFleet.SidecarHashRefused.ContainsKey("modern3500"), Is.False,
                "her sidecar hash is refused while she is listed as baked. The repair is upstream — " +
                "a re-cut sidecar from the rig on disk — never a re-stamp on the repo side.");
        }

        /// <summary>
        /// ⭐ <b>She wears her own <c>VehicleDef</c> and her own id — by ruling, now that her gameplay
        /// exists.</b> This REPLACES <c>SheWearsNoDefUntilSomethingCanBakeHerOne</c>, which asserted
        /// the opposite under the owner's ruling of 2026-09-16 ("a mesh only: no def and no vehicle
        /// id until her gameplay exists"). The owner's ruling of 2026-09-18 discharged that premise:
        /// she parks, drivable, at Nine Mile Creek beside the Dually, and her sidecar now carries the
        /// fleet's collider and way in
        /// (<see cref="HerSidecarReadsAsAHardCabTruck_WithExactlyTheFourAbsencesAHardCabReads"/>).
        ///
        /// <para>This pins the two strings against constants written HERE, never read back from
        /// the row. That the asset exists, wears her baked mesh and names a kind this repo
        /// recognises is <c>VehicleRigFleetTests.EveryRegisteredVehiclesDef_HasAMeshExactlyWhenHerBakeIsNotExcused</c>
        /// and <c>EveryRegisteredVehiclesDef_DeclaresAKindThisRepoRecognises</c>, which sweep every
        /// row, so neither is repeated here. The def is the BAKER's to create; a hand-written asset
        /// at this path would be a Data file that no tool produced.</para>
        /// </summary>
        [Test]
        public void SheWearsHerOwnDefAndIdNowThatHerGameplayExists()
        {
            VehicleRigFleet.Vehicle her = Her();

            Assert.That(her.VehicleDefPath, Is.EqualTo(HerDefPath),
                "her row names another def. By the owner's ruling of 2026-09-18 her def is " +
                HerDefPath + ", created by the fleet bake — Nine Mile Creek's truck park loads her " +
                "from there, and a def anywhere else is one nothing places.");

            Assert.That(her.VehicleId, Is.EqualTo(HerVehicleId),
                "her vehicle id changed. Ids are append-only and stable: once " + HerVehicleId +
                " ships it is spent, and a different id is a different truck, not an edit.");
        }

        // =========================================================================================
        //  5. HER FACTS — READ THE WAY THE BAKER READS THEM
        // =========================================================================================

        /// <summary>Her sidecar through <see cref="VehicleSidecarFacts.Read"/>, the reader the fleet
        /// bake writes her def's collider and door from — not a second parse written for the
        /// test.</summary>
        static VehicleSidecarFacts HerFacts() =>
            VehicleSidecarFacts.Read(File.ReadAllText(Full(SidecarPath)), SidecarPath);

        /// <summary>
        /// ⭐⭐ <b>Her sidecar reads as a hard-cab truck: no error, a solid body, a published way in,
        /// and exactly the four absences a hard cab reads.</b> She was re-keyed on 2026-09-18 to the
        /// fleet schema, on the Dually's model: a fitted <c>BODY.collider_bbox</c>, and an
        /// <c>INTERACT</c> <c>drive</c> at <c>door_fl</c> and <c>ride</c> at <c>door_fr</c>.
        ///
        /// <para><b>Why four, and why these four.</b> Each one is a TRUE statement about her, in
        /// the reader's own words for a measured zero, not a field nobody filled in:
        /// <list type="bullet">
        /// <item><b>her driver's seat is inside a cab.</b> She lists no top-level <c>SEATS</c>, and
        /// neither does the Dually. A hard cab keeps its driver hidden;</item>
        /// <item><b>no FLOAT.</b> She does not swim;</item>
        /// <item><b>no TOW.fifth_wheel.</b> The owner's ruling D4 of 2026-09-18 says no towing in
        /// this PR. Her <c>TOW</c> block stays in the art, inert, and is recorded as debt;</item>
        /// <item><b>no KINGPIN.</b> Nobody tows her.</item>
        /// </list>
        /// The Dually reads the same four. Clearing any of them would mean INVENTING a flotation
        /// block, a kingpin or an open seat she does not have. The re-key discharged three
        /// absences: no collider, no INTERACT block, and no drive or ride. Because the count is
        /// exact, any of those coming back reds this test.</para>
        ///
        /// <para>Each absence is matched by the start of its sentence, not by all of it, so a
        /// reworded explanation in the reader stays green. A NEW absence does not, because the count
        /// is exact.</para>
        /// </summary>
        [Test]
        public void HerSidecarReadsAsAHardCabTruck_WithExactlyTheFourAbsencesAHardCabReads()
        {
            VehicleSidecarFacts facts = HerFacts();

            Assert.That(facts.Errors, Is.Empty,
                "her sidecar reads with ERRORS, so the bake would refuse it: " +
                string.Join(" | ", facts.Errors));

            Assert.That(facts.HasCollider, Is.True,
                "she declares nothing solid. BODY.collider_bbox is gone, or it no longer reads as " +
                "two three-number corners; she would park as a ghost the player walks through.");
            Assert.That(facts.HasDriveDoor, Is.True,
                "she publishes no numeric drive reach point, so nobody can get in to drive her.");
            Assert.That(facts.WayInId, Is.EqualTo("drive"),
                "her way in was not read from the cab arm. She is a hard-cab truck, driven from " +
                "door_fl.");
            Assert.That(facts.InteractIds, Does.Contain("drive").And.Contain("ride"),
                "her INTERACT list lost its drive (door_fl) or its ride (door_fr).");

            Assert.That(facts.HasDriverSeat, Is.False,
                "she now publishes an OPEN seat the drive interaction resolves to. That draws a " +
                "visible driver inside a hard cab, which no hard-cab truck in the fleet does.");
            Assert.That(facts.HasAltDriveDoor, Is.False,
                "she reads a second drive door. That is the saddle arm's field, and her way in is " +
                "a cab.");
            Assert.That(facts.HasFlotation, Is.False,
                "she reads as a machine that floats. She is a road truck.");
            Assert.That(facts.HasFifthWheel, Is.False,
                "she reads a fifth wheel. The owner's ruling D4 of 2026-09-18 says no towing in " +
                "this PR; towing is recorded debt, and when it lands it gets its own ruling and " +
                "its own guard.");
            Assert.That(facts.HasKingpin, Is.False,
                "she reads a kingpin. She is not something anybody tows.");

            string[] expected =
            {
                "the drive interaction happens at 'door_fl'",
                "no FLOAT block",
                "no TOW.fifth_wheel",
                "no KINGPIN",
            };
            string all = string.Join(" | ", facts.Absences);
            foreach (string start in expected)
                Assert.That(facts.Absences.Count(a => a.StartsWith(start, StringComparison.Ordinal)),
                            Is.EqualTo(1),
                            $"expected exactly one absence starting '{start}'. She read: {all}");
            Assert.That(facts.Absences.Count, Is.EqualTo(expected.Length),
                $"she reads {facts.Absences.Count} absences; a hard-cab truck reads exactly these " +
                $"{expected.Length}. She read: {all}");
        }

        /// <summary>
        /// ⭐ <b>Where you stand to get in is OUTSIDE what is solid.</b> Her <c>drive</c> reach point
        /// is where the player stands to open <c>door_fl</c>; her <c>ride</c> reach point is the
        /// same for <c>door_fr</c>. If either one lands inside her collider, the player is asked to
        /// stand inside the truck, and the physics pushes them out first.
        ///
        /// <para>The bar is zero, the plan-view gap from each point to the collider's rectangle,
        /// written here. Both are read through the same reader the bake uses. The sidecar says the
        /// points stand 0.51 m outside the flare line; that margin is the art's, and this test does
        /// not copy it.</para>
        /// </summary>
        [Test]
        public void HerDriveReachPointStandsOutsideHerCollider()
        {
            VehicleSidecarFacts facts = HerFacts();
            Assert.That(facts.HasCollider && facts.HasDriveDoor, Is.True,
                "she has no collider or no drive door to compare. See " +
                "HerSidecarReadsAsAHardCabTruck_WithExactlyTheFourAbsencesAHardCabReads.");
            Assert.That(facts.ReachPoints.ContainsKey("ride"), Is.True,
                "her ride entry has no numeric reach point.");

            foreach ((string name, UnityEngine.Vector2 at) in new[]
                     {
                         ("drive", facts.DriveDoorLocal),
                         ("ride", facts.ReachPoints["ride"]),
                     })
            {
                float dx = Math.Max(Math.Max(facts.ColliderMin.x - at.x, 0f), at.x - facts.ColliderMax.x);
                float dy = Math.Max(Math.Max(facts.ColliderMin.y - at.y, 0f), at.y - facts.ColliderMax.y);
                double gap = Math.Sqrt(dx * dx + dy * dy);

                Assert.That(gap, Is.GreaterThan(0d),
                    $"her {name} reach point ({at.x:0.###}, {at.y:0.###}) is INSIDE her collider " +
                    $"x [{facts.ColliderMin.x:0.###}, {facts.ColliderMax.x:0.###}] " +
                    $"y [{facts.ColliderMin.y:0.###}, {facts.ColliderMax.y:0.###}]. The player would " +
                    "be asked to stand inside the truck.");
            }
        }
    }
}
