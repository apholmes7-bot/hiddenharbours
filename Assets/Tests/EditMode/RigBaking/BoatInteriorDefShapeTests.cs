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
    /// <para>It also reads the two CLEARED sidecars off disk end to end. That is the only test here
    /// that touches the kit, and it is worth it: the synthetic fixtures prove the reader's rules, and
    /// this proves the rules admit the real files the intake cleared.</para>
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

        // ---- the two cleared sidecars, off disk ------------------------------------------------------------

        static IEnumerable<string> ClearedSidecars()
        {
            yield return "sportFisherIsoRig2.convertible";
            yield return "sportFisherIsoRig2.skybridge";
        }

        [Test]
        [TestCaseSource(nameof(ClearedSidecars))]
        public void EachClearedSidecarReadsIntoADefWithTheShapeAConsumerExpects(string stem)
        {
            string kit = Path.Combine(RepoRoot, KitFolder);
            string sidecarPath = Path.Combine(kit, stem + ".interior.json");
            Assert.IsTrue(File.Exists(sidecarPath), $"cleared sidecar missing: {sidecarPath}");

            byte[] interiorRig = File.ReadAllBytes(Path.Combine(kit, "boatInteriorRig.js"));

            // The BUNDLED rig, not the repository's. Every sidecar's hullRigSha256 pins the revised rig
            // shipped beside it — the one publishing the loft it measured — and none of them pins
            // docs/art/rigs/, which is by definition the version before the drop added the door and the
            // loft. Feeding the repository's copy here is what reds this fixture, and it reds it with the
            // same message the builder would have printed for all 27 sidecars.
            byte[] hullRig = File.ReadAllBytes(
                Path.Combine(kit, "hull-rigs", "sportFisherIsoRig2.js"));

            BoatInteriorRead read = BoatInteriorSidecarReader.Read(
                File.ReadAllText(sidecarPath), $"{KitFolder}/{stem}.interior.json", interiorRig, hullRig);

            Assert.IsTrue(read.Ok, string.Join(" | ", read.Errors));

            BoatInteriorHullResolver.Resolution resolved =
                BoatInteriorHullResolver.Resolve(read.HullStem, Catalogue);
            Assert.IsTrue(resolved.Ok, resolved.Error);

            BoatInteriorDef def = DefFrom(read, resolved.HullFileStem);

            StringAssert.StartsWith("interior.", def.Id);
            Assert.IsTrue(def.HasInterior(), "a cleared hull has somewhere to stand");
            Assert.AreEqual(32, def.PixelsPerMetre, "both sport fishers are on the 32 px/m grid");
            Assert.Greater(def.CellPixels.x, 0);
            Assert.Greater(def.CellPixels.y, 0);
            Assert.GreaterOrEqual(def.FootprintOutline.Length, 3);
            Assert.IsNotNull(def.Door, "an interior with no way in would have been refused");
            Assert.AreEqual(8, def.Door.CueFrames, "the kit's door cue is 8 frames everywhere");
            Assert.IsTrue(def.RidesHullRock, "the kit states interiors ride the hull's rock");

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
        public void TheTwoClearedSidecarsPinTheRendererThatActuallyShipped()
        {
            // This is the axis-A check that refused the other twenty-five, asserted from the other side:
            // the two that DID clear must hash to the kit's own boatInteriorRig.js.
            string kit = Path.Combine(RepoRoot, KitFolder);
            byte[] rigBytes = File.ReadAllBytes(Path.Combine(kit, "boatInteriorRig.js"));
            string shipped = DeckSidecarReader.Sha256Hex(NormaliseLf(rigBytes));

            foreach (string stem in ClearedSidecars())
            {
                object root = DeckSidecarJson.Parse(File.ReadAllText(Path.Combine(kit, stem + ".interior.json")));
                string pinned = DeckSidecarJson.String(
                    DeckSidecarJson.Member(root, "derivedFromRigSha256"));
                Assert.AreEqual(shipped, pinned, $"{stem} does not pin the renderer that shipped");
            }
        }

        [Test]
        public void EverySidecarPinsTheBUNDLEDHullRigAndNotTheRepositorysCopy()
        {
            // The regression this exists for: the builder originally hashed docs/art/rigs/<stem>.js
            // against hullRigSha256 and would have refused ALL 27 sidecars — including the two clean
            // ones — building nothing. The pin names the REVISED rig shipped in the kit's hull-rigs/,
            // which is the only file that can carry the loft the rooms were measured from. The
            // repository's copy is by definition the version before the drop added it.
            string kit = Path.Combine(RepoRoot, KitFolder);
            string[] sidecars = Directory.GetFiles(kit, "*.interior.json", SearchOption.AllDirectories);
            Assert.IsNotEmpty(sidecars, "no interior sidecars found — the kit moved");

            int checkedCount = 0;
            foreach (string path in sidecars)
            {
                object root = DeckSidecarJson.Parse(File.ReadAllText(path));
                var pin = DeckSidecarJson.AsObject(DeckSidecarJson.Member(root, "hullRigSha256"));
                Assert.IsNotNull(pin, $"{Path.GetFileName(path)} has no hullRigSha256");
                Assert.AreEqual(1, pin.Count, $"{Path.GetFileName(path)} pins more than one loft");

                foreach (var kv in pin)
                {
                    string bundled = Path.Combine(kit, "hull-rigs", kv.Key + ".js");
                    Assert.IsTrue(File.Exists(bundled),
                                  $"{Path.GetFileName(path)} names a rig the kit does not ship: {kv.Key}.js");

                    Assert.AreNotEqual(RigHashMatch.None,
                        DeckSidecarReader.MatchRigHash(File.ReadAllBytes(bundled), kv.Value as string, out _),
                        $"{Path.GetFileName(path)} does not describe the {kv.Key}.js shipped beside it");
                    checkedCount++;
                }
            }
            Assert.AreEqual(27, checkedCount, "the kit ships 27 interior sidecars");
        }

        [Test]
        public void TheRepositorysCopyOfAHullRigIsNotWhatTheSidecarsPin()
        {
            // The other half of the same contract, asserted from the failing side so the distinction
            // cannot quietly collapse back: main's sportFisherIsoRig2.js must NOT satisfy the pin.
            // If this ever starts passing, the repository has adopted a bundled rig and the builder's
            // two hash arms have become the same question.
            string kit = Path.Combine(RepoRoot, KitFolder);
            object root = DeckSidecarJson.Parse(
                File.ReadAllText(Path.Combine(kit, "sportFisherIsoRig2.convertible.interior.json")));
            string pinned = null;
            foreach (var kv in DeckSidecarJson.AsObject(DeckSidecarJson.Member(root, "hullRigSha256")))
                pinned = kv.Value as string;

            byte[] inRepo = File.ReadAllBytes(Path.Combine(RepoRoot, "docs/art/rigs/sportFisherIsoRig2.js"));
            Assert.AreEqual(RigHashMatch.None,
                DeckSidecarReader.MatchRigHash(inRepo, pinned, out _),
                "the repository's rig satisfied a pin that names the bundled one — if the merge landed, " +
                "update this test deliberately rather than letting the two arms merge by accident");
        }

        static byte[] NormaliseLf(byte[] bytes)
            => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n"));
    }
}
