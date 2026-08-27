using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ⭐ <b>THE VEHICLE COVERAGE LAW — a road vehicle cannot arrive unnoticed.</b>
    ///
    /// <para>The direct descendant of
    /// <c>HullMeshFleetTests.EveryHullRigOnDisk_IsEitherBakedOrExplicitlyExcluded</c>, and it exists
    /// because that test <b>cannot see a truck</b>. It scans <c>docs/art/rigs/</c> for rigs containing
    /// the signal <c>rollA</c> — a hull's sea-rock amplitude — and <c>vehicleIsoRig.js</c> has zero
    /// occurrences of it, correctly. Art arrives by PR; without a law of its own, the next vehicle
    /// drop lands in the repo and is silently never baked, which is the exact failure the hull law was
    /// written to prevent.</para>
    ///
    /// <para>The population is defined by the SIDECAR, not by the rig: a road vehicle declares itself
    /// with a top-level <c>"kind": "road_vehicle"</c>. That is art's own word for the thing rather
    /// than a substring this file guesses at, and boat sidecars carry no top-level <c>kind</c>, so the
    /// two populations cannot overlap.</para>
    /// </summary>
    public class VehicleRigFleetTests
    {
        static string RepoRoot => RigCatalog.RepoRoot;

        static string SidecarFolder => Path.Combine(RepoRoot, VehicleRigFleet.SidecarFolder);

        /// <summary>Sidecar file names whose top-level <c>kind</c> is <c>road_vehicle</c>.
        ///
        /// <para>⚠ Matched on the whole <c>"kind": "road_vehicle"</c> pair rather than on the value
        /// alone: the Dually's own sidecar contains a dozen NESTED <c>kind</c> fields (<c>bucket</c>,
        /// <c>bench</c>, <c>vertical</c>…), so a looser match would be answering a different
        /// question.</para></summary>
        static IEnumerable<string> RoadVehicleSidecarsOnDisk() =>
            Directory.EnumerateFiles(SidecarFolder, "*.gameplay.json")
                     .Where(f => HiddenHarbours.Core.VehicleKinds.KnownTokens.Any(
                                     t => DeclaresPair(File.ReadAllText(f), "\"kind\"", "\"" + t + "\"")))
                     .Select(Path.GetFileName)
                     .OrderBy(f => f, System.StringComparer.Ordinal);

        /// <summary>The top-level <c>kind</c> token a sidecar declares, or null when it has none.
        /// Reads the FIRST <c>"kind":</c> in the file, which is the top-level one — these documents
        /// put it in the header, above every nested <c>kind</c> the geometry blocks reuse.</summary>
        static string TopLevelKind(string json)
        {
            int at = json.IndexOf("\"kind\"", System.StringComparison.Ordinal);
            if (at < 0) return null;
            int i = json.IndexOf(':', at);
            if (i < 0) return null;
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != '"') return null;
            int end = json.IndexOf('"', i + 1);
            return end < 0 ? null : json.Substring(i + 1, end - i - 1);
        }

        /// <summary>Is <paramref name="key"/> immediately followed by <paramref name="value"/>, allowing
        /// only a colon and whitespace between? Cheap, and enough to tell a top-level declaration from
        /// a nested field that merely reuses the word.
        ///
        /// <para>⚠ NOT called <c>Contains</c>: a static method of that name shadows NUnit's
        /// <c>Contains</c> constraint class and breaks every <c>Assert.That(x, Contains.Item(y))</c> in
        /// the fixture with a CS0119 that points at the assert rather than at the helper.</para>
        /// </summary>
        static bool DeclaresPair(string json, string key, string value)
        {
            int at = json.IndexOf(key, System.StringComparison.Ordinal);
            while (at >= 0)
            {
                int i = at + key.Length;
                while (i < json.Length && (json[i] == ':' || char.IsWhiteSpace(json[i]))) i++;
                if (i + value.Length <= json.Length &&
                    string.CompareOrdinal(json, i, value, 0, value.Length) == 0)
                    return true;
                at = json.IndexOf(key, at + key.Length, System.StringComparison.Ordinal);
            }
            return false;
        }

        // =============================================================================================

        /// <summary>
        /// ⭐ <b>EVERY sidecar's kind token is one this repo RECOGNISES.</b> The law the Otter forced,
        /// and the reason it is a separate test from the coverage scan above.
        ///
        /// <para>That scan can only see a vehicle whose token it knows. So a drop shipping a word
        /// nobody mapped is not merely unbaked — it is <b>unscanned</b>, and the coverage test passes
        /// while the vehicle sits there. The Otter is exactly that case: her sidecar says
        /// <c>amphibious_xtv</c> and her rig says <c>amphib_xtv</c>, neither of which is the ruled
        /// <c>amphibious_vehicle</c>.</para>
        ///
        /// <para>⚠️ The fix is NEVER to edit the sidecar — <c>docs/art/rigs/**</c> is the art
        /// director's lane, and a hand-corrected token breaks the sidecar's own hash pin and comes
        /// back wrong on the next regeneration. Add the shipped spelling to
        /// <see cref="HiddenHarbours.Core.VehicleKinds"/>, which is the one table that translates.</para>
        /// </summary>
        [Test]
        public void EverySidecarInTheVehicleFolder_DeclaresAKindThisRepoRecognises()
        {
            foreach (string file in Directory.EnumerateFiles(SidecarFolder, "*.gameplay.json"))
            {
                string token = TopLevelKind(File.ReadAllText(file));

                Assert.That(token, Is.Not.Null,
                    $"{Path.GetFileName(file)} sits in the vehicle sidecar folder but declares no " +
                    "top-level `kind` at all. That folder IS the population — a document without a " +
                    "kind cannot be classified and would go unscanned.");

                Assert.That(HiddenHarbours.Core.VehicleKinds.TryFromToken(token, out _), Is.True,
                    $"{Path.GetFileName(file)} declares kind '{token}', which VehicleKinds does not " +
                    "map. She is therefore INVISIBLE to the coverage scan — not unbaked, UNSCANNED, " +
                    "which is worse because the coverage test stays green.\n" +
                    "Add '" + token + "' to VehicleKinds.Tokens. Do NOT edit the sidecar: it is the " +
                    "art director's file, its hash is pinned, and the token would come back on the " +
                    "next regeneration.\n" +
                    "Known today: " +
                    string.Join(", ", HiddenHarbours.Core.VehicleKinds.KnownTokens) + ".");
            }
        }

        /// <summary>Both of the Otter's shipped spellings — the sidecar's and the rig's — reach the
        /// SAME ruled kind. Two files, one idea; a reader that knew only one would work until it
        /// read the other.</summary>
        [Test]
        public void BothOfTheOttersShippedSpellings_MapToTheOneRuledKind()
        {
            foreach (string shipped in new[] { "amphibious_xtv", "amphib_xtv", "amphibious_vehicle" })
            {
                Assert.That(HiddenHarbours.Core.VehicleKinds.TryFromToken(shipped, out var kind),
                    Is.True, $"'{shipped}' no longer maps.");
                Assert.That(kind, Is.EqualTo(HiddenHarbours.Core.VehicleKind.AmphibiousVehicle),
                    $"'{shipped}' maps to {kind}, not the amphibian. Driving an amphibian onto the " +
                    "road, or a truck into the water, are both plausible-looking failures.");
            }

            Assert.That(HiddenHarbours.Core.VehicleKinds.CanonicalToken(
                            HiddenHarbours.Core.VehicleKind.AmphibiousVehicle),
                Is.EqualTo("amphibious_vehicle"),
                "the repo-side canonical name is the owner's ruled one, never the shipped alias.");
        }

        /// <summary>⚠️ An unknown token is a REFUSAL, not a default. A fallback to RoadVehicle would
        /// put an amphibian on the road and a truck in the sea, silently, because both read as
        /// plausible.</summary>
        [Test]
        public void AnUnknownKindTokenIsRefused_NeverDefaulted()
        {
            foreach (string junk in new[] { "hovercraft", "", "  ", "road vehicle", null })
                Assert.That(HiddenHarbours.Core.VehicleKinds.TryFromToken(junk, out _), Is.False,
                    $"'{junk ?? "<null>"}' was accepted. Unrecognised must mean refused.");
        }

        [Test]
        public void EveryRoadVehicleOnDisk_IsEitherBakedOrExplicitlyExcluded()
        {
            var registered = new HashSet<string>(
                VehicleRigFleet.Vehicles.Select(v => Path.GetFileName(v.SidecarPath)),
                System.StringComparer.Ordinal);

            var missed = RoadVehicleSidecarsOnDisk().Where(f => !registered.Contains(f)).ToList();

            CollectionAssert.IsEmpty(missed,
                "A road-vehicle sidecar is in " + VehicleRigFleet.SidecarFolder + " but VehicleRigFleet " +
                "does not know about it: " + string.Join(", ", missed) + ".\n" +
                "Art arrives by PR from the art director, so this is the expected way a new vehicle " +
                "shows up. Add it to VehicleRigFleet.Vehicles, and then either to Baked (it gets a " +
                "mesh) or to NotBaked with the reason (it does not). Do not silence this by editing " +
                "the sidecar — docs/art/rigs/** is read-only to us.");
        }

        [Test]
        public void EveryRegisteredVehicle_IsEitherBakedOrCarriesAReason()
        {
            foreach (VehicleRigFleet.Vehicle v in VehicleRigFleet.Vehicles)
            {
                bool baked = VehicleRigFleet.Baked.Contains(v.Key);
                bool excused = VehicleRigFleet.NotBaked.ContainsKey(v.Key);

                Assert.That(baked || excused, Is.True,
                    $"'{v.Key}' is registered but is in neither Baked nor NotBaked — it would be " +
                    "quietly unbaked, which is the one thing this table exists to make impossible.");

                Assert.That(baked && excused, Is.False,
                    $"'{v.Key}' is BOTH baked and excused. One of the two is stale.");

                if (excused)
                    Assert.That(VehicleRigFleet.NotBaked[v.Key], Is.Not.Empty,
                        $"'{v.Key}' is excused with an empty reason. A refusal without a reason rots " +
                        "into folklore.");
            }
        }

        /// <summary>Both files a registered vehicle names are really there. A table that outlives its
        /// files goes on explaining a vehicle nobody can bake.</summary>
        [Test]
        public void EveryRegisteredVehiclesFilesExist()
        {
            foreach (VehicleRigFleet.Vehicle v in VehicleRigFleet.Vehicles)
            {
                FileAssert.Exists(Path.Combine(RepoRoot, v.ScriptPath),
                    $"VehicleRigFleet lists '{v.Key}' with rig {v.ScriptPath}, which does not exist.");
                FileAssert.Exists(Path.Combine(RepoRoot, v.SidecarPath),
                    $"VehicleRigFleet lists '{v.Key}' with sidecar {v.SidecarPath}, which does not " +
                    "exist.");
            }
        }

        /// <summary>
        /// ⭐ Every registered vehicle's sidecar still pins its own rig.
        ///
        /// <para>The staleness rule applied across the table rather than to one drop, so a re-shaped
        /// rig landing without a re-stamped sidecar fails here even if nobody thought to update
        /// <c>DuallyIsoKitProbeTests</c>. <see cref="RigHashMatch.LineEndingNormalized"/> passes: the
        /// kits ship LF and <c>.gitattributes</c> checks <c>.js</c> out CRLF on Windows, and a line
        /// ending cannot move a vertex.</para>
        /// </summary>
        [Test]
        public void EveryRegisteredVehiclesSidecarStillPinsItsRig()
        {
            foreach (VehicleRigFleet.Vehicle v in VehicleRigFleet.Vehicles)
            {
                byte[] rig = File.ReadAllBytes(Path.Combine(RepoRoot, v.ScriptPath));
                string sidecar = File.ReadAllText(Path.Combine(RepoRoot, v.SidecarPath));

                int at = sidecar.IndexOf("derivedFromRigSha256", System.StringComparison.Ordinal);
                Assert.That(at, Is.GreaterThanOrEqualTo(0),
                    $"'{v.Key}' carries no derivedFromRigSha256. An absent hash is a REFUSAL by " +
                    "design — do not read its geometry.");

                int open = sidecar.IndexOf('"', sidecar.IndexOf(':', at) + 1);
                int close = sidecar.IndexOf('"', open + 1);
                string expected = sidecar.Substring(open + 1, close - open - 1);

                RigHashMatch match = DeckSidecarReader.MatchRigHash(rig, expected, out string actual);
                bool refused = VehicleRigFleet.SidecarHashRefused.ContainsKey(v.Key);

                if (!refused)
                {
                    Assert.That(match, Is.Not.EqualTo(RigHashMatch.None),
                        $"'{v.Key}': the sidecar pins {expected} but {v.ScriptPath} hashes to " +
                        $"{actual}, and not through a line-ending difference. The vehicle was " +
                        "reshaped and its sidecar was not re-derived — do NOT read its geometry, " +
                        "and do NOT re-stamp the hash here: docs/art/rigs/** is the art director's " +
                        "lane, and a hash corrected on our side comes back wrong on the next " +
                        "regeneration. If the mismatch is real and known, record it in " +
                        "VehicleRigFleet.SidecarHashRefused with the measurement, and send the " +
                        "re-stamp upstream.");
                }
                else
                {
                    // ⭐ THE OTHER DIRECTION, and the half that keeps the ledger from rotting: a
                    // refusal that has been FIXED upstream must be deleted, not left standing. A
                    // stale entry here silently suppresses the law for a vehicle that no longer
                    // needs it, which is how a real mismatch gets through the next time.
                    Assert.That(match, Is.EqualTo(RigHashMatch.None),
                        $"'{v.Key}' is listed in VehicleRigFleet.SidecarHashRefused, but its " +
                        $"sidecar NOW PINS its rig ({expected} vs {actual}). The art side " +
                        "re-stamped it — delete the entry, and with it whichever NotBaked reason " +
                        "cited it. Do not leave a lifted blocker standing.");
                }
            }
        }

        /// <summary>
        /// ⚠️⚠️ <b>A vehicle whose sidecar does not pin its rig may not be BAKED.</b> The teeth
        /// on <see cref="VehicleRigFleet.SidecarHashRefused"/> — without this the ledger would be a
        /// note, and a note does not stop anybody.
        ///
        /// <para>The pin is the sidecar's claim that its thresholds, cargo volumes, colliders and
        /// seats were cut from THIS shape. When it fails, those numbers describe some other
        /// revision, and a bake that read them would produce a vehicle whose picture and whose
        /// physics disagree — which looks entirely fine until somebody walks through a wall.</para>
        /// </summary>
        [Test]
        public void ARefusedSidecarHash_KeepsHerOutOfBaked()
        {
            foreach (var kvp in VehicleRigFleet.SidecarHashRefused)
            {
                Assert.That(VehicleRigFleet.Vehicles.Any(v => v.Key == kvp.Key), Is.True,
                    $"SidecarHashRefused names '{kvp.Key}', which is not in Vehicles. Delete the " +
                    "entry — a refusal for a vehicle nobody registered rots into folklore.");

                Assert.That(kvp.Value, Is.Not.Empty,
                    $"'{kvp.Key}' is refused with an empty reason. The reason IS the artefact: it " +
                    "carries the measurement and the upstream ask.");

                Assert.That(VehicleRigFleet.Baked, Does.Not.Contain(kvp.Key),
                    $"'{kvp.Key}' is BAKED while its sidecar does not pin its rig. Either the hash " +
                    "was fixed upstream (delete the refusal) or a bake read geometry it was told " +
                    "not to trust.");
            }
        }

        /// <summary>
        /// ⭐ <b>THE TRIPWIRE: a registered vehicle's DEF has a mesh exactly when her bake is not
        /// excused</b> — in both directions, so neither half can be forgotten.
        ///
        /// <para><b>Why a def can exist without a mesh at all.</b> The Otter spent #558 to #562 in
        /// exactly that state: her mechanics built and tested (skid steer, the drive⇄swim swap, her
        /// handling tunables) while her PICTURE was blocked on something no vehicle-side change could
        /// fix — 17 colour ramps against the facet shader's 16. She was authored, tested and
        /// <b>unplaceable</b>, because <c>VehicleDef.IsUsable</c> refuses a def with no mesh, which is
        /// the honest state rather than a machine that ships half-wired and silently.</para>
        ///
        /// <para>The pairing is what made it a tripwire rather than a note, and it did its job: when
        /// the art merge landed on 2026-08-19 and her <see cref="VehicleRigFleet.NotBaked"/> entry was
        /// deleted, THIS went red until she was baked and her def pointed at the result. It still
        /// guards the other direction — a mesh wired while a blocker stands fails here too, and the
        /// stale excuse gets deleted rather than the assert nudged.</para>
        /// </summary>
        [Test]
        public void EveryRegisteredVehiclesDef_HasAMeshExactlyWhenHerBakeIsNotExcused()
        {
            foreach (VehicleRigFleet.Vehicle v in VehicleRigFleet.Vehicles)
            {
                if (string.IsNullOrEmpty(v.VehicleDefPath)) continue;   // art-only, wears no def

                var def = UnityEditor.AssetDatabase
                                     .LoadAssetAtPath<HiddenHarbours.Vehicles.VehicleDef>(v.VehicleDefPath);
                Assert.That(def, Is.Not.Null,
                    $"VehicleRigFleet lists '{v.Key}' with def {v.VehicleDefPath}, which does not " +
                    "load. A table that names a def nobody committed explains a vehicle nobody has.");

                Assert.That(def.Id, Is.EqualTo(v.VehicleId),
                    $"'{v.Key}': the committed def says '{def.Id}' and the table says " +
                    $"'{v.VehicleId}'. Ids are stable and append-only — one of the two is a typo.");

                bool excused = VehicleRigFleet.NotBaked.ContainsKey(v.Key);
                Assert.That(def.Mesh == null, Is.EqualTo(excused),
                    $"'{v.Key}': her def and her bake status disagree.\n" +
                    "· mesh null + excused → the expected state for a vehicle whose art is blocked: " +
                    "her mechanics ship, her picture does not, and IsUsable refuses to place her.\n" +
                    "· mesh null + NOT excused → the blocker was lifted and nobody wired her mesh. " +
                    "Bake her, point VehicleDef.Mesh at the result, and fill her flotation fields " +
                    "from her sidecar.\n" +
                    "· mesh set + still excused → the NotBaked entry is stale. Delete it.");

                if (def.Mesh == null)
                    Assert.That(def.IsUsable(), Is.False,
                        $"'{v.Key}' has no mesh but reports usable. She would be placed invisible, " +
                        "and the mesh is where her wheelbase, her track and her flotation live.");
            }
        }

        /// <summary>Every committed vehicle def declares a kind this repo maps — the discriminator
        /// that decides whether water is a wall or a road, pinned so a typo is a red test rather than
        /// a machine held out of the water she was built for.</summary>
        [Test]
        public void EveryRegisteredVehiclesDef_DeclaresAKindThisRepoRecognises()
        {
            foreach (VehicleRigFleet.Vehicle v in VehicleRigFleet.Vehicles)
            {
                if (string.IsNullOrEmpty(v.VehicleDefPath)) continue;

                var def = UnityEditor.AssetDatabase
                                     .LoadAssetAtPath<HiddenHarbours.Vehicles.VehicleDef>(v.VehicleDefPath);
                if (def == null) continue;   // the test above owns that failure

                Assert.That(HiddenHarbours.Core.VehicleKinds.TryFromToken(def.KindToken,
                                                                          out var fromDef),
                    Is.True,
                    $"'{v.Key}' declares kind '{def.KindToken}', which VehicleKinds does not map. " +
                    "Known today: " +
                    string.Join(", ", HiddenHarbours.Core.VehicleKinds.KnownTokens) + ".");

                string shipped = TopLevelKind(File.ReadAllText(Path.Combine(RepoRoot, v.SidecarPath)));
                Assert.That(HiddenHarbours.Core.VehicleKinds.TryFromToken(shipped, out var fromSidecar),
                    Is.True, $"'{v.Key}' ships kind '{shipped}', which VehicleKinds does not map.");

                Assert.That(fromDef, Is.EqualTo(fromSidecar),
                    $"'{v.Key}': her def says {fromDef} and her sidecar says {fromSidecar}. The two " +
                    "files may say different WORDS — the art side's is never hand-edited — but they " +
                    "must mean the same machine, and VehicleKinds is what reconciles them.");
            }
        }

        /// <summary>A reason may not name a vehicle that is not registered — the same anti-folklore
        /// rule <c>HullMeshFleetTests.TheExclusionList_OnlyNamesRigsThatExist</c> applies to hulls.
        /// </summary>
        [Test]
        public void TheExclusionList_OnlyNamesRegisteredVehicles()
        {
            var keys = new HashSet<string>(VehicleRigFleet.Vehicles.Select(v => v.Key),
                                           System.StringComparer.Ordinal);

            foreach (var kvp in VehicleRigFleet.NotBaked)
                Assert.That(keys, Contains.Item(kvp.Key),
                    $"VehicleRigFleet.NotBaked excuses '{kvp.Key}', which is not in Vehicles. Delete " +
                    "the entry.");
        }

        // =============================================================================================
        //  TOWED BODIES — the third kind, and the one that must never be driven
        // =============================================================================================

        /// <summary>
        /// The road-fleet drop's shipped token reaches the ruled kind. <c>towed_bodies</c> is PLURAL
        /// because the trailer sidecar really does describe four bodies (its <c>variant</c> is
        /// <c>trailers-x4</c>); the enum stays singular because a kind describes one registered
        /// body. Accepted as shipped rather than corrected — the same rule as the Otter's two
        /// spellings, for the same reason: the sidecar's hash is pinned, and a hand-edit comes back
        /// on the next regeneration.
        /// </summary>
        [Test]
        public void TheTrailerSetsShippedToken_MapsToTheOneRuledTowedKind()
        {
            foreach (string shipped in new[] { "towed_bodies", "towed_body" })
            {
                Assert.That(HiddenHarbours.Core.VehicleKinds.TryFromToken(shipped, out var kind),
                    Is.True, $"'{shipped}' no longer maps.");
                Assert.That(kind, Is.EqualTo(HiddenHarbours.Core.VehicleKind.TowedBody),
                    $"'{shipped}' maps to {kind}. A trailer read as a road vehicle is a trailer " +
                    "handed to a driving controller.");
            }

            Assert.That(HiddenHarbours.Core.VehicleKinds.CanonicalToken(
                            HiddenHarbours.Core.VehicleKind.TowedBody),
                Is.EqualTo("towed_body"),
                "the repo-side canonical name is singular, never the shipped plural alias.");
        }

        /// <summary>
        /// ⭐⭐ <b>A TOWED BODY IS NEVER DRIVABLE, and the two machines always are.</b> Asserted in
        /// both directions, because either half drifting is a real failure: a trailer that reads as
        /// drivable gets a throttle and lock angles it has no geometry for, and a truck that reads
        /// as towed cannot be got into.
        ///
        /// <para>Measured rather than inferred from the name: <c>trailerIsoRig.js</c> resolves no
        /// <c>steer</c> axis at all (pinned in <c>TrailerIsoKitProbeTests</c>) and the kit's own
        /// README says <i>"No steering — towed bodies"</i>.</para>
        /// </summary>
        [Test]
        public void ATowedBodyIsNeverDrivable_AndTheTwoMachinesAlwaysAre()
        {
            Assert.That(HiddenHarbours.Core.VehicleKinds.IsDrivable(
                            HiddenHarbours.Core.VehicleKind.TowedBody), Is.False,
                "a towed body reads as drivable. It has no engine, no steering axle and no seat, " +
                "and every driving path asks this question before it poses anything.");

            foreach (var kind in new[] { HiddenHarbours.Core.VehicleKind.RoadVehicle,
                                         HiddenHarbours.Core.VehicleKind.AmphibiousVehicle })
                Assert.That(HiddenHarbours.Core.VehicleKinds.IsDrivable(kind), Is.True,
                    $"{kind} reads as NOT drivable — it would be unplaceable behind a wheel.");

            // Every value in the enum has an answer. A new kind that forgot one throws here rather
            // than defaulting into a driving path.
            foreach (HiddenHarbours.Core.VehicleKind kind in
                     System.Enum.GetValues(typeof(HiddenHarbours.Core.VehicleKind)))
            {
                Assert.DoesNotThrow(() => HiddenHarbours.Core.VehicleKinds.IsDrivable(kind),
                    $"VehicleKinds.IsDrivable has no answer for {kind}.");
                Assert.DoesNotThrow(() => HiddenHarbours.Core.VehicleKinds.CanonicalToken(kind),
                    $"VehicleKinds.CanonicalToken has no answer for {kind}.");
            }
        }

        /// <summary>
        /// ⭐⭐ <b>THE (file, pick) LAW.</b> Two registered bodies may share a rig file — the
        /// trailer set's four do — but the PAIR must be unique, and a body inside a multi-body rig
        /// must name its pick.
        ///
        /// <para><b>Why this is a test and not a convention.</b> <c>trailerIsoRig.js</c> resolves an
        /// unknown body id to its DEFAULT (measured: <c>reefer53</c>) rather than throwing, so an
        /// entry that forgot its pick would bake a perfectly good reefer53 in a flatbed's place and
        /// nothing downstream would notice. The same shape has shipped the wrong boat in this repo
        /// before, through <c>byId</c>'s fallback.</para>
        /// </summary>
        [Test]
        public void EveryRegisteredBody_IsUniqueByFileAndPick()
        {
            var seenPair = new Dictionary<string, string>(System.StringComparer.Ordinal);
            var seenKey = new HashSet<string>(System.StringComparer.Ordinal);

            foreach (VehicleRigFleet.Vehicle v in VehicleRigFleet.Vehicles)
            {
                Assert.That(seenKey.Add(v.Key), Is.True,
                    $"two registered vehicles share the key '{v.Key}'. Get(key) hands back the " +
                    "first, and the second is unbakeable and unreachable.");

                string pair = v.ScriptPath + "|" + (v.Pick ?? "<single>");
                Assert.That(seenPair.ContainsKey(pair), Is.False,
                    $"'{v.Key}' and '{(seenPair.TryGetValue(pair, out string other) ? other : "?")}' " +
                    $"are the SAME (file, pick): {pair}. Anything cached by that pair replays one " +
                    "body's answer onto the other.");
                seenPair[pair] = v.Key;
            }

            foreach (var group in VehicleRigFleet.Vehicles.GroupBy(v => v.ScriptPath,
                                                                   System.StringComparer.Ordinal))
            {
                if (group.Count() == 1) continue;

                foreach (VehicleRigFleet.Vehicle v in group)
                {
                    Assert.That(v.Pick, Is.Not.Null.And.Not.Empty,
                        $"'{v.Key}' shares rig {v.ScriptPath} with {group.Count() - 1} other " +
                        "registered body/bodies but declares no Pick. A container rig resolves an " +
                        "unknown body to its DEFAULT rather than throwing, so a missing pick does " +
                        "not fail — it bakes the default body under this one's name.");

                    Assert.That(v.Extraction?.FaceExpression ?? "", Does.Contain(v.Pick),
                        $"'{v.Key}' declares Pick '{v.Pick}' but its face expression " +
                        $"({v.Extraction?.FaceExpression ?? "<none>"}) does not name it. The pick " +
                        "has to reach the rig, or the extraction quietly takes the default body.");
                }
            }
        }
    }
}
