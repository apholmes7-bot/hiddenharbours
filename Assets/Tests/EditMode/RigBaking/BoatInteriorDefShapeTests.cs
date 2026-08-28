using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// Two things this fixture pins, both about a def being the right SHAPE rather than merely
    /// populated: how a hull's interior id is resolved (looked up from the committed sidecars' own
    /// fields, never computed from a name), and what a <see cref="BoatInteriorDef"/> guarantees to a
    /// consumer that has one.
    ///
    /// <para>It also reads representative CLEARED sidecars off disk end to end, and asserts the kit's
    /// pin contract across all twenty-seven. Those are the only tests here that touch the kit, and they
    /// are worth it: the synthetic fixtures prove the reader's rules, and these prove the rules admit
    /// the real files the intake cleared — which is where a rule that is subtly wrong about a real
    /// convention shows up.</para>
    /// </summary>
    public class BoatInteriorDefShapeTests
    {
        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;
        const string KitFolder = "docs/art/rigs/boat-interiors-kit";

        /// <summary>A SYNTHETIC catalogue, for the resolution unit tests only — small enough to reason
        /// about, and deliberately holding both variant conventions plus a hull no sidecar names. It is
        /// not the fleet: anything reading real sidecars off disk must use <see cref="CommittedCatalogue"/>,
        /// because resolving an end-to-end case against a hand-written list proves nothing about the
        /// intake and rots the moment the cleared set grows.</summary>
        static readonly HullSidecarIdentity[] Catalogue =
        {
            new HullSidecarIdentity("capeIslanderIsoRig", "capeIslanderIsoRig.js", ""),
            new HullSidecarIdentity("lobsterBoatIsoRig", "lobsterBoatIsoRig.js", ""),
            new HullSidecarIdentity("sportFisherConvertibleIso", "sportFisherIsoRig2.js", "convertible"),
            new HullSidecarIdentity("sportFisherSkybridgeIso", "sportFisherIsoRig2.js", "skybridge"),
            new HullSidecarIdentity("lobsterStandardOpenFundyIso", "lobsterBoatVariantsIsoRig.js",
                                    "standard_open_fundy"),
        };

        /// <summary>The catalogue the builder actually resolves against: every committed hull gameplay
        /// sidecar, enumerated NON-recursively exactly as the boat-deck parity tests do — the kit's own
        /// gameplay files live under the kit and must not be swept in from here.</summary>
        static List<HullSidecarIdentity> CommittedCatalogue()
        {
            var catalogue = new List<HullSidecarIdentity>();
            string gameplay = Path.Combine(RepoRoot, "docs/art/rigs/gameplay");
            foreach (string f in Directory.GetFiles(gameplay, "*.gameplay.json"))
            {
                object root = DeckSidecarJson.Parse(File.ReadAllText(f));
                catalogue.Add(new HullSidecarIdentity(
                    Path.GetFileName(f).Replace(".gameplay.json", ""),
                    DeckSidecarJson.String(DeckSidecarJson.Member(root, "rig")),
                    BoatInteriorHullResolver.VariantKeyOf(DeckSidecarJson.Member(root, "variant"))));
            }
            return catalogue;
        }

        // ---- resolution -------------------------------------------------------------------------------

        [Test]
        public void AVariantHullResolvesThroughItsRigAndVariantFields()
        {
            // `sportFisherIsoRig2.convertible` and `sportFisherConvertibleIso` are the same boat under
            // two naming conventions that do not transform into one another. Only a lookup gets this
            // right, which is exactly why the resolver does one.
            BoatInteriorHullResolver.Resolution r =
                BoatInteriorHullResolver.Resolve("sportFisherIsoRig2.convertible", Catalogue);

            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual("sportFisherConvertibleIso", r.HullFileStem);
            Assert.AreEqual("interior.sport_fisher_convertible_iso",
                            BoatInteriorHullResolver.DefId(r.HullFileStem));
            Assert.AreEqual("SportFisherConvertibleIso", BoatInteriorHullResolver.AssetName(r.HullFileStem));
        }

        [Test]
        public void TheTwoSportFishersResolveToDifferentBoats()
        {
            Assert.AreEqual("sportFisherConvertibleIso",
                BoatInteriorHullResolver.Resolve("sportFisherIsoRig2.convertible", Catalogue).HullFileStem);
            Assert.AreEqual("sportFisherSkybridgeIso",
                BoatInteriorHullResolver.Resolve("sportFisherIsoRig2.skybridge", Catalogue).HullFileStem);
        }

        [Test]
        public void ASingleHullRigResolvesWithoutAVariantAndDropsItsRigSuffix()
        {
            BoatInteriorHullResolver.Resolution r =
                BoatInteriorHullResolver.Resolve("capeIslanderIsoRig", Catalogue);

            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual("interior.cape_islander_iso", BoatInteriorHullResolver.DefId(r.HullFileStem),
                            "the interior is named for the same boat her deck def is");
        }

        [Test]
        public void AnInteriorForAHullThisProjectDoesNotHaveIsRefused()
        {
            BoatInteriorHullResolver.Resolution r =
                BoatInteriorHullResolver.Resolve("someOtherIsoRig", Catalogue);

            Assert.IsFalse(r.Ok);
            StringAssert.Contains("someOtherIsoRig", r.Error);
            Assert.IsEmpty(r.HullFileStem, "an unresolved hull yields no id to attach a cabin to");
        }

        [Test]
        public void AVariantNamedByARigThatHasNoSuchVariantIsRefused()
        {
            BoatInteriorHullResolver.Resolution r =
                BoatInteriorHullResolver.Resolve("sportFisherIsoRig2.trawlerback", Catalogue);

            Assert.IsFalse(r.Ok);
            StringAssert.Contains("trawlerback", r.Error);
        }

        [Test]
        public void AnAmbiguousMatchIsRefusedRatherThanTakingTheFirst()
        {
            var doubled = Catalogue.Concat(new[]
            {
                new HullSidecarIdentity("capeIslanderIsoRigCopy", "capeIslanderIsoRig.js", ""),
            }).ToArray();
            BoatInteriorHullResolver.Resolution r =
                BoatInteriorHullResolver.Resolve("capeIslanderIsoRig", doubled);

            Assert.IsFalse(r.Ok);
            StringAssert.Contains("Ambiguous", r.Error);
        }

        [Test]
        public void SplittingAStemSeparatesRigFromVariant()
        {
            BoatInteriorHullResolver.Split("sportFisherIsoRig2.convertible", out string rig, out string variant);
            Assert.AreEqual("sportFisherIsoRig2", rig);
            Assert.AreEqual("convertible", variant);

            BoatInteriorHullResolver.Split("capeIslanderIsoRig", out rig, out variant);
            Assert.AreEqual("capeIslanderIsoRig", rig);
            Assert.IsEmpty(variant);
        }

        // ---- def shape ----------------------------------------------------------------------------------

        static BoatInteriorDef DefFrom(BoatInteriorRead read, string hullFileStem)
        {
            var def = ScriptableObject.CreateInstance<BoatInteriorDef>();
            def.Id = BoatInteriorHullResolver.DefId(hullFileStem);
            def.FitsHulls = read.FitsHulls;
            def.SourceSidecar = read.SidecarPath;
            def.InteriorRigSha256 = read.ExpectedInteriorRigSha;
            def.HullRigSha256 = read.ExpectedHullRigSha;
            def.PixelsPerMetre = read.PixelsPerMetre;
            def.CellPixels = read.CellPixels;
            def.PivotPixels = read.PivotPixels;
            def.FootprintOutline = read.Footprint;
            def.Levels = read.Levels.ToArray();
            def.Door = read.Door;
            def.AdditionalDoors = read.AdditionalDoors.ToArray();
            def.Anchors = read.Anchors.ToArray();
            def.Routes = read.Routes.ToArray();
            def.RidesHullRock = read.RidesHullRock;
            return def;
        }

        [Test]
        public void AFreshDefIsEmptyButNeverNull()
        {
            // Every array field defaults to Array.Empty so nothing downstream needs a null guard.
            var def = ScriptableObject.CreateInstance<BoatInteriorDef>();

            Assert.IsNotNull(def.FitsHulls);
            Assert.IsNotNull(def.FootprintOutline);
            Assert.IsNotNull(def.Levels);
            Assert.IsNotNull(def.Anchors);
            Assert.IsNotNull(def.Routes);
            Assert.IsNotNull(def.AdditionalDoors);
            Assert.IsNotNull(def.Door);
            Assert.IsFalse(def.HasInterior(), "no levels means no inside — and that is data, not an error");
        }

        [Test]
        public void MetresPerPixelFollowsTheHullsOwnGridAndNeverDividesByZero()
        {
            var def = ScriptableObject.CreateInstance<BoatInteriorDef>();

            def.PixelsPerMetre = 32;
            Assert.AreEqual(1f / 32f, def.MetresPerPixel, 1e-6f);

            def.PixelsPerMetre = 16;                       // the tanker
            Assert.AreEqual(1f / 16f, def.MetresPerPixel, 1e-6f);

            def.PixelsPerMetre = 0;                        // a builder bug — reported, not divided by
            Assert.AreEqual(0f, def.MetresPerPixel);
        }

        [Test]
        public void LevelLookupFindsByIdAndMissesCleanly()
        {
            var def = ScriptableObject.CreateInstance<BoatInteriorDef>();
            def.Levels = new[]
            {
                new BoatInteriorLevel { Id = "house_sole", SoleZMeters = 1.78f,
                                        Outline = new[] { Vector2.zero, Vector2.right, Vector2.up } },
            };

            Assert.AreEqual(1.78f, def.Level("house_sole").SoleZMeters, 1e-4f);
            Assert.IsNull(def.Level("below_sole"));
            Assert.IsNull(def.Level(null));
            Assert.IsTrue(def.HasInterior());
        }

        [Test]
        public void ALevelWithTooFewVerticesIsNotUsable()
        {
            var level = new BoatInteriorLevel { Id = "sliver", Outline = new[] { Vector2.zero, Vector2.right } };
            Assert.IsFalse(level.IsUsable());
        }

        // ---- the cleared sidecars, off disk ------------------------------------------------------------

        /// <summary>Representative CLEARED hulls, chosen for shape coverage rather than convenience:
        /// a house+cuddy workboat, a three-level ship, a parametric variant, and the tanker — who is the
        /// only hull in the fleet on the 16 px/m grid.</summary>
        static IEnumerable<string> ClearedSidecars()
        {
            yield return "lobsterBoatIsoRig";
            yield return "coastalPacketIsoRig";
            yield return "tankerIsoRig";
            yield return "lobsterBoatVariants/lobsterBoatVariantsIsoRig.standard_open_fundy";
        }

        /// <summary>The bundled rig a sidecar pins, read from the sidecar itself. Only entries that are
        /// real SHAs count — the re-export shipped an unsubstituted template on the sport fishers, and a
        /// helper that silently picked it would hide exactly what the reader is meant to refuse.</summary>
        static byte[] BundledRigFor(string kit, object root)
        {
            foreach (var kv in DeckSidecarJson.AsObject(DeckSidecarJson.Member(root, "hullRigSha256")))
            {
                if (!BoatInteriorSidecarReader.IsSha256Hex(kv.Value as string)) continue;
                string path = Path.Combine(kit, "hull-rigs", kv.Key + ".js");
                if (File.Exists(path)) return File.ReadAllBytes(path);
            }
            return null;
        }

        [Test]
        [TestCaseSource(nameof(ClearedSidecars))]
        public void EachClearedSidecarReadsIntoADefWithTheShapeAConsumerExpects(string stem)
        {
            string kit = Path.Combine(RepoRoot, KitFolder);
            string sidecarPath = Path.Combine(kit, stem + ".interior.json");
            Assert.IsTrue(File.Exists(sidecarPath), $"cleared sidecar missing: {sidecarPath}");

            string json = File.ReadAllText(sidecarPath);
            byte[] interiorRig = File.ReadAllBytes(Path.Combine(kit, "boatInteriorRig.js"));
            byte[] hullRig = BundledRigFor(kit, DeckSidecarJson.Parse(json));
            Assert.IsNotNull(hullRig, $"{stem} pins no resolvable bundled rig");

            BoatInteriorRead read = BoatInteriorSidecarReader.Read(
                json, $"{KitFolder}/{stem}.interior.json", interiorRig, hullRig);
            Assert.IsTrue(read.Ok, string.Join(" | ", read.Errors));

            BoatInteriorHullResolver.Resolution resolved =
                BoatInteriorHullResolver.Resolve(read.HullStem, CommittedCatalogue());
            Assert.IsTrue(resolved.Ok, resolved.Error);

            BoatInteriorDef def = DefFrom(read, resolved.HullFileStem);

            StringAssert.StartsWith("interior.", def.Id);
            Assert.IsTrue(def.HasInterior(), "a cleared hull has somewhere to stand");
            Assert.Greater(def.CellPixels.x, 0);
            Assert.Greater(def.CellPixels.y, 0);
            Assert.GreaterOrEqual(def.FootprintOutline.Length, 3);
            Assert.IsNotNull(def.Door, "an interior with no way in would have been refused");
            Assert.AreEqual(8, def.Door.CueFrames, "the kit's door cue is 8 frames everywhere");
            Assert.IsTrue(def.RidesHullRock, "the kit states interiors ride the hull's rock");

            // The two pixel grids, asserted as data rather than assumed.
            int expectedPxPerM = stem == "tankerIsoRig" ? 16 : 32;
            Assert.AreEqual(expectedPxPerM, def.PixelsPerMetre,
                            "px/m is per hull — the tanker is 16 where the fleet is 32");

            foreach (BoatInteriorLevel level in def.Levels)
            {
                Assert.IsNotEmpty(level.Id);
                Assert.IsTrue(level.IsUsable(), $"level '{level.Id}' has fewer than 3 vertices");
                Assert.IsNotNull(level.Obstructions);
            }
            foreach (BoatInteriorAnchor a in def.Anchors) Assert.IsNotEmpty(a.Id);
            foreach (BoatInteriorRoute r in def.Routes) Assert.IsNotEmpty(r.Id);
        }

        /// <summary>
        /// <b>The skybridge keeps BOTH of her decks, and her bridge ladder lands on the upper one.</b>
        /// This is the HOLD of 2026-08-27 written down where it can be enforced rather than
        /// remembered.
        ///
        /// <para><b>The collision it protects against.</b> <c>sportFisherIsoRig2.js</c> uses the id
        /// <c>bridge_sole</c> for TWO walkables: the ENCLOSED skylounge sole at z 7.30
        /// (<c>interior.bridge.deckId</c>) and the OPEN control coaming at z 9.74
        /// (<c>interior.bridgeSole</c>, and <c>helms[0].deck</c>). Our committed mirror resolves that
        /// by carrying both — the coaming as <c>bridge_sole</c> at 9.74 with its 42-vertex polygon,
        /// the skylounge as <c>sky_sole</c> at 7.30.</para>
        ///
        /// <para><b>Why the offered replacement was refused.</b> Upstream's re-export resolved the
        /// same collision by DELETING the 9.74 deck and renaming <c>sky_sole</c> to
        /// <c>bridge_sole</c> — 218 lines shorter, and it strands the helm and the ladder that the
        /// rig's own records put up there. Its own file says so: it keeps <c>bridge_ladder</c>
        /// climbing z 7.32 → 9.55 and connecting to a <c>bridge_sole</c> it has left at 7.30, so the
        /// ladder rises 2.23 m to arrive 0.02 m BELOW its own foot. That inconsistency is what the
        /// second assertion here is written against, and it is the general law: <b>a ladder lands on
        /// the deck it names.</b> The HOLD stands until the rig disambiguates the id and re-exports
        /// (upstream ask 6); nothing on our side is re-stamped meanwhile, because a hash corrected
        /// here would come back wrong on the next regeneration.</para>
        ///
        /// <para>⚠️ A string-grep is NOT this check. The staging pass that first cleared the offered
        /// file counted <c>bridge_sole</c> occurrences — eight, none of them <c>sky_sole</c> — and
        /// called the deck "fully present". A name's presence is not a deck's presence; both
        /// assertions below read the polygon and the z.</para>
        /// </summary>
        [Test]
        public void TheSkybridgeKeepsBothOfHerDecks_AndHerLadderLandsOnTheUpperOne()
        {
            string path = Path.Combine(RepoRoot, KitFolder, "gameplay",
                                       "sportFisherIsoRig2.skybridge.gameplay.json");
            object root = DeckSidecarJson.Parse(File.ReadAllText(path));

            var byId = new Dictionary<string, (double Z, int Verts)>(System.StringComparer.Ordinal);
            foreach (object d in DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "DECK")))
            {
                string id = DeckSidecarJson.String(DeckSidecarJson.Member(d, "id"));
                if (string.IsNullOrEmpty(id)) continue;
                List<object> poly = DeckSidecarJson.AsArray(DeckSidecarJson.Member(d, "polygon"));
                byId[id] = (DeckSidecarJson.Float(DeckSidecarJson.Member(d, "z"), float.NaN),
                            poly?.Count ?? 0);
            }

            Assert.IsTrue(byId.ContainsKey("bridge_sole"),
                "the open control coaming is gone. It is where helms[0] and the bridge ladder land.");
            Assert.IsTrue(byId.ContainsKey("sky_sole"),
                "the enclosed skylounge sole is gone — renaming it onto bridge_sole is exactly the " +
                "collapse this fixture refuses, because it leaves one id for two walkables again.");

            Assert.AreEqual(9.74, byId["bridge_sole"].Z, 1e-6,
                "bridge_sole is the OPEN coaming at 9.74 m. At 7.30 it is the skylounge wearing the " +
                "coaming's name, and the helm above it has no deck.");
            Assert.AreEqual(7.30, byId["sky_sole"].Z, 1e-6, "sky_sole is the enclosed skylounge sole.");
            Assert.AreEqual(42, byId["bridge_sole"].Verts,
                "the coaming's polygon is 42 vertices. A present id with an absent shape is how the " +
                "offered replacement read to a string-grep.");

            // The general law, and the one the refused file breaks on its own terms.
            foreach (object l in DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "LADDER")))
            {
                List<object> connects = DeckSidecarJson.AsArray(DeckSidecarJson.Member(l, "connects"));
                if (connects == null || connects.Count == 0) continue;
                string top = DeckSidecarJson.String(connects[connects.Count - 1]);
                if (top == null || !byId.TryGetValue(top, out var deck)) continue;

                double z0 = DeckSidecarJson.Float(DeckSidecarJson.Member(l, "z0"), float.NaN);
                double z1 = DeckSidecarJson.Float(DeckSidecarJson.Member(l, "z1"), float.NaN);
                string id = DeckSidecarJson.String(DeckSidecarJson.Member(l, "id"));

                Assert.GreaterOrEqual(deck.Z, z0,
                    $"{id} climbs {z0:0.##} → {z1:0.##} m and lands on '{top}', which sits at " +
                    $"{deck.Z:0.##} m — below the foot of its own ladder. A ladder lands on the deck " +
                    "it names.");
                Assert.AreEqual(z1, deck.Z, 0.35,
                    $"{id} tops out at {z1:0.##} m but '{top}' sits at {deck.Z:0.##} m. The stringer " +
                    "runs a little past the deck it serves; a metre and more apart means the ladder " +
                    "and the deck are describing different levels.");
            }
        }

        [Test]
        public void EverySidecarPinsTheRendererThatActuallyShipped()
        {
            // Finding 1, closed and now guarded: the re-export re-stamped all 27 to one renderer. If a
            // future export mixes hashes again, this is what says so.
            string kit = Path.Combine(RepoRoot, KitFolder);
            string shipped = DeckSidecarReader.Sha256Hex(
                NormaliseLf(File.ReadAllBytes(Path.Combine(kit, "boatInteriorRig.js"))));

            string[] sidecars = Directory.GetFiles(kit, "*.interior.json", SearchOption.AllDirectories);
            Assert.AreEqual(27, sidecars.Length, "the kit ships 27 interior sidecars");
            foreach (string path in sidecars)
            {
                object root = DeckSidecarJson.Parse(File.ReadAllText(path));
                Assert.AreEqual(shipped,
                    DeckSidecarJson.String(DeckSidecarJson.Member(root, "derivedFromRigSha256")),
                    $"{Path.GetFileName(path)} does not pin the renderer that shipped");
            }
        }

        [Test]
        public void NoHullPinIsAnUnsubstitutedExportTemplate()
        {
            // ⚠️ THIS TEST CHANGED SHAPE ON 2026-08-26, exactly as its previous form asked to be.
            //
            // It used to be TheOnlyUnstampedHullPinsAreTheTwoSportFishersTheReExportBroke, and it named
            // the two files the re-export had regressed: their hullRigSha256 carried the literal string
            // "STAMP_AT_EXPORT_LF_SHA256_OF_sportFisherIsoRig2.js". It pinned that from BOTH sides — the
            // regression must not spread, and it must not vanish silently either — and said in its own
            // comment that when upstream substituted the template, this test should fail and be updated
            // in the same change that re-cleared the hulls. The cutaway kit (batch 1) shipped the
            // substitution; the S0 ledger's two REFUSED-PIN rows went back to CLEAN in that same commit.
            //
            // So the expectation is now the general one it should always have been able to be: NOTHING
            // in the kit carries an unstamped stamp. An unstamped stamp is worse than an absent one — it
            // occupies a provenance field looking like a value — and that is true of any hull, not just
            // of the two that once had it.
            string kit = Path.Combine(RepoRoot, KitFolder);
            var unstamped = new List<string>();

            foreach (string path in Directory.GetFiles(kit, "*.interior.json", SearchOption.AllDirectories))
            {
                object root = DeckSidecarJson.Parse(File.ReadAllText(path));
                foreach (var kv in DeckSidecarJson.AsObject(DeckSidecarJson.Member(root, "hullRigSha256")))
                    if (!BoatInteriorSidecarReader.IsSha256Hex(kv.Value as string))
                        unstamped.Add($"{Path.GetFileName(path)} [{kv.Key}] = {kv.Value}");
            }
            unstamped.Sort(System.StringComparer.Ordinal);

            CollectionAssert.IsEmpty(unstamped,
                "a hull pin is not a sha256 — if an export template has come back, refuse the hull in " +
                "the S0 ledger in this same change rather than loosening this test");
        }

        [Test]
        public void EveryStampedHullPinResolvesToTheBundledRigItNames()
        {
            // The other half: every pin that IS a SHA must describe the rig shipped beside it. The
            // builder originally hashed docs/art/rigs/ here and would have refused all 27.
            //
            // ⚠️ A pin key may be a VARIANT stem. The two sport fishers pin their hull twice — once as
            // "sportFisherIsoRig2" and once as "sportFisherIsoRig2.convertible"/".skybridge" — and both
            // entries name the same file, because one rig makes both boats. Before 2026-08-26 the
            // variant-keyed entry was an unsubstituted export template and was skipped as unstamped, so
            // this loop only ever saw bare stems and could take the key as a filename directly. Now that
            // the kit has stamped it, the key is split the way the resolver splits every other hull stem
            // (BoatInteriorHullResolver.Split) rather than being special-cased by name.
            string kit = Path.Combine(RepoRoot, KitFolder);
            int stamped = 0, variantKeyed = 0;

            foreach (string path in Directory.GetFiles(kit, "*.interior.json", SearchOption.AllDirectories))
            {
                object root = DeckSidecarJson.Parse(File.ReadAllText(path));
                foreach (var kv in DeckSidecarJson.AsObject(DeckSidecarJson.Member(root, "hullRigSha256")))
                {
                    string sha = kv.Value as string;
                    if (!BoatInteriorSidecarReader.IsSha256Hex(sha)) continue;
                    BoatInteriorHullResolver.Split(kv.Key, out string rigStem, out string variant);
                    if (variant.Length > 0) variantKeyed++;

                    string bundled = Path.Combine(kit, "hull-rigs", rigStem + ".js");
                    Assert.IsTrue(File.Exists(bundled),
                                  $"{Path.GetFileName(path)} names a rig the kit does not ship: {rigStem}.js");
                    Assert.AreNotEqual(RigHashMatch.None,
                        DeckSidecarReader.MatchRigHash(File.ReadAllBytes(bundled), sha, out _),
                        $"{Path.GetFileName(path)} pin '{kv.Key}' does not describe the {rigStem}.js " +
                        "shipped beside it");
                    stamped++;
                }
            }
            Assert.AreEqual(29, stamped,
                            "27 sidecars, one correctly-stamped rig-stem pin each, plus the two sport " +
                            "fishers' variant-keyed pins that the cutaway kit stamped on 2026-08-26");
            Assert.AreEqual(2, variantKeyed, "only the two sport fishers pin by variant stem as well");
        }

        [Test]
        public void TheRepositorysCopyOfAHullRigIsNotWhatTheSidecarsPin()
        {
            // If this ever starts passing, the repository has adopted a bundled rig and the builder's two
            // hash arms have become the same question. Update it deliberately, never by accident.
            string kit = Path.Combine(RepoRoot, KitFolder);
            object root = DeckSidecarJson.Parse(
                File.ReadAllText(Path.Combine(kit, "coastalPacketIsoRig.interior.json")));
            string pinned = null;
            foreach (var kv in DeckSidecarJson.AsObject(DeckSidecarJson.Member(root, "hullRigSha256")))
                if (BoatInteriorSidecarReader.IsSha256Hex(kv.Value as string)) pinned = kv.Value as string;
            Assert.IsNotNull(pinned);

            byte[] inRepo = File.ReadAllBytes(Path.Combine(RepoRoot, "docs/art/rigs/coastalPacketIsoRig.js"));
            Assert.AreEqual(RigHashMatch.None, DeckSidecarReader.MatchRigHash(inRepo, pinned, out _),
                "the repository's rig satisfied a pin that names the bundled one");
        }

        // ---- the variant-key convention -------------------------------------------------------------------

        [Test]
        public void AVariantKeyIsBuiltFromWhicheverConventionTheHullUses()
        {
            // Knowing only `variant.hull` silently failed to resolve all EIGHTEEN lobster variants, which
            // reads as "refused" rather than as a bug. Both conventions, pinned.
            object sportFisher = DeckSidecarJson.Parse("{\"hull\":\"convertible\",\"label\":\"53′\"}");
            Assert.AreEqual("convertible", BoatInteriorHullResolver.VariantKeyOf(sportFisher));

            object lobster = DeckSidecarJson.Parse(
                "{\"size\":\"standard\",\"style\":\"hardtop\",\"region\":\"fundy\",\"paint\":\"gelcoat\"}");
            Assert.AreEqual("standard_hardtop_fundy", BoatInteriorHullResolver.VariantKeyOf(lobster),
                            "paint is deliberately absent from the key — it does not move a bulkhead");

            Assert.IsEmpty(BoatInteriorHullResolver.VariantKeyOf(null), "a one-boat rig has no variant");
            Assert.IsEmpty(BoatInteriorHullResolver.VariantKeyOf(DeckSidecarJson.Parse("{\"size\":\"standard\"}")),
                           "a partial triple is not a key — guessing one would resolve the wrong boat");
        }

        [Test]
        public void EveryCommittedVariantHullResolvesFromItsOwnGameplaySidecar()
        {
            // End to end against the real committed catalogue: all 27 sidecars must find exactly one
            // hull. Eighteen of them did not before VariantKeyOf existed.
            List<HullSidecarIdentity> catalogue = CommittedCatalogue();

            string kit = Path.Combine(RepoRoot, KitFolder);
            var ids = new List<string>();
            foreach (string path in Directory.GetFiles(kit, "*.interior.json", SearchOption.AllDirectories))
            {
                string hullStem = DeckSidecarJson.String(
                    DeckSidecarJson.Member(DeckSidecarJson.Parse(File.ReadAllText(path)), "hull_stem"));
                BoatInteriorHullResolver.Resolution r =
                    BoatInteriorHullResolver.Resolve(hullStem, catalogue);
                Assert.IsTrue(r.Ok, $"{hullStem}: {r.Error}");
                ids.Add(BoatInteriorHullResolver.DefId(r.HullFileStem));
            }
            Assert.AreEqual(27, ids.Count);
            Assert.AreEqual(27, new HashSet<string>(ids).Count, "two hulls resolved to one def id");
        }

        static byte[] NormaliseLf(byte[] bytes)
            => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n"));
    }
}
