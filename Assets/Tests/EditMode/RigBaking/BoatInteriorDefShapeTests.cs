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

        static readonly HullSidecarIdentity[] Catalogue =
        {
            new HullSidecarIdentity("capeIslanderIsoRig", "capeIslanderIsoRig.js", ""),
            new HullSidecarIdentity("lobsterBoatIsoRig", "lobsterBoatIsoRig.js", ""),
            new HullSidecarIdentity("sportFisherConvertibleIso", "sportFisherIsoRig2.js", "convertible"),
            new HullSidecarIdentity("sportFisherSkybridgeIso", "sportFisherIsoRig2.js", "skybridge"),
            new HullSidecarIdentity("lobsterStandardOpenFundyIso", "lobsterBoatVariantsIsoRig.js",
                                    "standard_open_fundy"),
        };

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
                BoatInteriorHullResolver.Resolve(read.HullStem, Catalogue);
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
        public void TheOnlyUnstampedHullPinsAreTheTwoSportFishersTheReExportBroke()
        {
            // Pins the regression from BOTH sides: it must not spread, and it must not vanish silently
            // either — when upstream substitutes the template, this test fails and gets updated in the
            // same PR that re-clears them. Drop 1 had both sport fishers CLEAN; the re-export shipped
            // "STAMP_AT_EXPORT_LF_SHA256_OF_sportFisherIsoRig2.js" into their hullRigSha256.
            string kit = Path.Combine(RepoRoot, KitFolder);
            var unstamped = new List<string>();

            foreach (string path in Directory.GetFiles(kit, "*.interior.json", SearchOption.AllDirectories))
            {
                object root = DeckSidecarJson.Parse(File.ReadAllText(path));
                foreach (var kv in DeckSidecarJson.AsObject(DeckSidecarJson.Member(root, "hullRigSha256")))
                    if (!BoatInteriorSidecarReader.IsSha256Hex(kv.Value as string))
                        unstamped.Add(Path.GetFileName(path));
            }
            unstamped.Sort(System.StringComparer.Ordinal);

            CollectionAssert.AreEqual(
                new[] { "sportFisherIsoRig2.convertible.interior.json",
                        "sportFisherIsoRig2.skybridge.interior.json" },
                unstamped,
                "the set of unstamped hull pins changed — if upstream fixed them, re-clear those hulls " +
                "in the S0 ledger in this same change rather than loosening this test");
        }

        [Test]
        public void EveryStampedHullPinResolvesToTheBundledRigItNames()
        {
            // The other half: every pin that IS a SHA must describe the rig shipped beside it. The
            // builder originally hashed docs/art/rigs/ here and would have refused all 27.
            string kit = Path.Combine(RepoRoot, KitFolder);
            int stamped = 0;

            foreach (string path in Directory.GetFiles(kit, "*.interior.json", SearchOption.AllDirectories))
            {
                object root = DeckSidecarJson.Parse(File.ReadAllText(path));
                foreach (var kv in DeckSidecarJson.AsObject(DeckSidecarJson.Member(root, "hullRigSha256")))
                {
                    string sha = kv.Value as string;
                    if (!BoatInteriorSidecarReader.IsSha256Hex(sha)) continue;
                    string bundled = Path.Combine(kit, "hull-rigs", kv.Key + ".js");
                    Assert.IsTrue(File.Exists(bundled),
                                  $"{Path.GetFileName(path)} names a rig the kit does not ship: {kv.Key}.js");
                    Assert.AreNotEqual(RigHashMatch.None,
                        DeckSidecarReader.MatchRigHash(File.ReadAllBytes(bundled), sha, out _),
                        $"{Path.GetFileName(path)} does not describe the {kv.Key}.js shipped beside it");
                    stamped++;
                }
            }
            Assert.AreEqual(27, stamped, "27 sidecars, one correctly-stamped rig-stem pin each");
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
