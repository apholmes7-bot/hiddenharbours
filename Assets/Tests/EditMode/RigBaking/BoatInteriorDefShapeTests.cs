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
        /// <b>The skybridge keeps BOTH of her walkables, and her bridge ladder lands on the upper
        /// one.</b> The 2026-08-27 HOLD, rewritten on 2026-08-28 when the rig's own id split landed —
        /// and rewritten to say what it always meant.
        ///
        /// <para><b>The collision it protects against.</b> <c>sportFisherIsoRig2.js</c> uses the id
        /// <c>bridge_sole</c> for TWO walkables: the ENCLOSED skylounge sole at z 7.30
        /// (<c>interior.bridge.deckId</c>) and the OPEN control coaming at z 9.74
        /// (<c>interior.bridgeSole</c>, and <c>helms[0].deck</c>). Our mirror used to resolve it by
        /// carrying the coaming as <c>bridge_sole</c> at 9.74 and inventing <c>sky_sole</c> for the
        /// skylounge — which disagreed with her INTERIOR sidecar, where <c>bridge_sole</c> has always
        /// meant the 7.30 skylounge, and left this file's own <c>STAIRS.house_to_bridge</c> going
        /// <c>to: sky_sole</c> through an <c>opening.in: bridge_sole</c> 2.44 m above it. The
        /// 2026-08-28 changeset splits the id upstream instead: <c>bridge_sole</c> IS the skylounge,
        /// the coaming becomes <c>helm_coaming</c>. Adopted.</para>
        ///
        /// <para><b>Why this fixture no longer names either id.</b> Its first form asserted
        /// <c>bridge_sole</c> at 9.74 and the presence of <c>sky_sole</c> — a name test standing in
        /// for a geometry claim, and it would have gone red on a split that fixed the very thing it
        /// guarded. What it was written to refuse is a hull losing a WALKABLE, so that is what it
        /// says now: two decks up there, one at each height, each with its own polygon, and no id
        /// naming two of them. That still refuses the 2026-08-27 export verbatim (it carried ONE deck
        /// above the aft deck) and it now also refuses a re-collapse under any pair of names.</para>
        ///
        /// <para>⚠️ A string-grep is NOT this check. The staging pass that first cleared the refused
        /// file counted <c>bridge_sole</c> occurrences — eight — and called the deck "fully present".
        /// A name's presence is not a deck's presence.</para>
        ///
        /// <para>⚠️⚠️ <b>This fixture reads the kit's PRE-MERGED mirror, which is a DERIVED file.</b>
        /// That is why it stayed green while the collision lived on in the two sources it is merged
        /// from — #685 first landed the id split here only, where a re-merge would have reverted it.
        /// <see cref="MergingHerTwoSources_AppendsBothWalkables_AndReplacesNothing"/> is the one that
        /// asks the sources, and it is the load-bearing half of the pair.</para>
        /// </summary>
        [Test]
        public void TheSkybridgeKeepsBothOfHerWalkables_AndHerLadderLandsOnTheUpperOne()
        {
            string path = Path.Combine(RepoRoot, KitFolder, "gameplay",
                                       "sportFisherIsoRig2.skybridge.gameplay.json");
            object root = DeckSidecarJson.Parse(File.ReadAllText(path));

            var byId = new Dictionary<string, (double Z, int Verts)>(System.StringComparer.Ordinal);
            var seen = new List<string>();
            foreach (object d in DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "DECK")))
            {
                string id = DeckSidecarJson.String(DeckSidecarJson.Member(d, "id"));
                if (string.IsNullOrEmpty(id)) continue;
                seen.Add(id);
                List<object> poly = DeckSidecarJson.AsArray(DeckSidecarJson.Member(d, "polygon"));
                byId[id] = (DeckSidecarJson.Float(DeckSidecarJson.Member(d, "z"), float.NaN),
                            poly?.Count ?? 0);
            }

            // The collision law itself, and not a list of the names it happened to take.
            CollectionAssert.AllItemsAreUnique(seen,
                "one id names two DECK entries — which is the rig's own collision arriving in the " +
                "sidecar, and it is what the split of 2026-08-28 exists to end.");

            // Named by HEIGHT, not by id and not by a band: her aft deck sits at 7.28, two
            // centimetres under the skylounge, so "everything above 7 m" is three decks, not two.
            var found = string.Join(", ", byId.Select(kv => kv.Key + "@" + kv.Value.Z));
            var atSkylounge = byId.Where(kv => System.Math.Abs(kv.Value.Z - 7.30) < 1e-3).ToList();
            var atCoaming = byId.Where(kv => System.Math.Abs(kv.Value.Z - 9.74) < 1e-3).ToList();

            // Counted, not Single()'d: on the file this fixture exists to refuse there is NO deck at
            // 9.74, and Single() answers that with "Sequence contains no matching element" instead of
            // the sentence below. A guard that cannot say what is wrong on its own artefact is half
            // a guard.
            Assert.AreEqual(1, atCoaming.Count,
                "no single walkable at z 9.74 — the OPEN control coaming, where helms[0] stands and " +
                "the bridge ladder tops out. The 2026-08-27 export deleted it and stranded them both. " +
                "Decks: [" + found + "]");
            Assert.AreEqual(1, atSkylounge.Count,
                "no single walkable at z 7.30 — the ENCLOSED skylounge sole, the full helm deck the " +
                "interior companionway climbs to. Decks: [" + found + "]");

            var skylounge = atSkylounge[0];
            var coaming = atCoaming[0];
            Assert.AreNotEqual(skylounge.Key, coaming.Key,
                "the enclosed skylounge sole and the open control coaming are ONE deck. That is the " +
                "collapse this fixture refuses — the 2026-08-27 export made it by deleting the 9.74 " +
                "walkable, and stranded helms[0] and the head of the bridge ladder up there with " +
                "nothing to stand on. Decks: [" + found + "]");
            Assert.AreEqual(4, skylounge.Value.Verts,
                "the skylounge sole (" + skylounge.Key + ") is the enclosed volume's rectangular sole.");
            Assert.AreEqual(42, coaming.Value.Verts,
                "the coaming (" + coaming.Key + ") is the open ring — 21 stations, both sides. A " +
                "present id with an absent shape is how the refused file read to a string-grep.");

            // The general law, and the one the refused file broke on its own terms:
            // A LADDER LANDS ON THE DECK IT NAMES.
            int laddersChecked = 0;
            foreach (object l in DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "LADDER")))
            {
                List<object> connects = DeckSidecarJson.AsArray(DeckSidecarJson.Member(l, "connects"));
                if (connects == null || connects.Count == 0) continue;
                string top = DeckSidecarJson.String(connects[connects.Count - 1]);
                if (top == null || !byId.TryGetValue(top, out var deck)) continue;

                double z1 = DeckSidecarJson.Float(DeckSidecarJson.Member(l, "z1"), float.NaN);
                string lid = DeckSidecarJson.String(DeckSidecarJson.Member(l, "id"));
                laddersChecked++;
                Assert.LessOrEqual(System.Math.Abs(deck.Z - z1), 0.60,
                    "LADDER " + lid + " tops out at z " + z1 + " and names deck " + top + " at z " +
                    deck.Z + ". A ladder lands on the deck it names; the 2026-08-27 export had this " +
                    "one rise 2.23 m to arrive 0.02 m BELOW its own foot.");
            }
            Assert.Greater(laddersChecked, 0,
                "no LADDER resolved to a deck, so the law above was never asked.");

            // And the companionway's hole is cut through the sole of the UPPER deck it connects —
            // down the salon hatch, up through the skylounge floor. Not "the deck it arrives at":
            // that is only true climbing. Our own file said `to: sky_sole` through an
            // `opening.in: bridge_sole` that was the 9.74 coaming — a third deck, 2.44 m clear of
            // either end — and carried it from #589 until the id split gave the two soles
            // distinguishable names.
            object stairs = DeckSidecarJson.Member(DeckSidecarJson.Member(root, "STAIRS"), "companionways");
            int stairsChecked = 0;
            foreach (object c in DeckSidecarJson.AsArray(stairs))
            {
                string from = DeckSidecarJson.String(DeckSidecarJson.Member(c, "from"));
                string to = DeckSidecarJson.String(DeckSidecarJson.Member(c, "to"));
                string inDeck = DeckSidecarJson.String(
                    DeckSidecarJson.Member(DeckSidecarJson.Member(c, "opening"), "in"));
                if (from == null || to == null || inDeck == null) continue;
                if (!byId.TryGetValue(from, out var a) || !byId.TryGetValue(to, out var b)) continue;

                stairsChecked++;
                string upper = a.Z >= b.Z ? from : to;
                string id = DeckSidecarJson.String(DeckSidecarJson.Member(c, "id"));
                Assert.AreEqual(upper, inDeck,
                    "companionway " + id + " runs " + from + " (z " + a.Z + ") to " + to + " (z " +
                    b.Z + ") through an opening declared in " + inDeck + ". The hole is cut through " +
                    "the sole of the UPPER deck, which is " + upper + ".");
            }
            Assert.Greater(stairsChecked, 0, "no companionway resolved both its decks.");
        }

        /// <summary>
        /// <b>The JOIN, on the boat the game opens inside.</b> Her rig names the deck each enclosed
        /// level is — <c>house → house_sole</c>, <c>cuddy → cuddy_sole</c> — and those names are only
        /// worth anything if the interior actually has rooms by them. Unjoined, the gate resolves to
        /// 0 and the house never opens, silently.
        ///
        /// <para><c>HullLevelTagBakeTests.EveryPublishedLevel_HasACeilingOrADeclaredOpenSky_AndNamesA
        /// DefLevel</c> asserts this across the fleet against the built <c>BoatInteriorDef</c>, and
        /// was VACUOUS on the cape until 2026-08-28 because her rig published no levels to join. It
        /// covers her now — but it loads an asset, so it only runs in the editor. This one asks the
        /// same question of the two files on disk, which is where the answer comes from: the rig, run
        /// in V8, against the interior sidecar the def is built from. It runs anywhere.</para>
        ///
        /// <para><b>The two vocabularies are deliberately different.</b> <c>house</c> and <c>cuddy</c>
        /// are the rig's level ids and become TexCoord1 tags; <c>house_sole</c> and <c>cuddy_sole</c>
        /// are the interior's room ids. Conflating them is exactly how a cutaway silently no-ops, so
        /// the mapping is DECLARED by the rig (<c>geometry().levels[].deck</c>) and never derived
        /// from a name here.</para>
        /// </summary>
        [Test]
        public void TheCapesEnclosedLevelsNameRoomsHerInteriorSidecarActuallyHas()
        {
            FleetHull cape = HullMeshFleet.Hulls.Single(h => h.Key == "capeIslander");
            RigMeshData data;
            using (IRigScriptHost host = RigScriptHostFactory.Create())
                data = RigMeshExtractor.ExtractFrom(host, cape.ScriptPath, cape.GlobalName,
                                                    hull: cape.Extraction);

            Assert.IsNotEmpty(data.Levels.ToArray(),
                "her rig published no levels at all — cutaway pass 4 has not landed, and every claim " +
                "below would be about an empty list.");

            object interior = DeckSidecarJson.Parse(File.ReadAllText(
                Path.Combine(RepoRoot, KitFolder, "capeIslanderIsoRig.interior.json")));
            var rooms = new HashSet<string>(
                DeckSidecarJson.AsObject(DeckSidecarJson.Member(interior, "WALKABLE")).Select(kv => kv.Key),
                System.StringComparer.Ordinal);

            var unjoined = new List<string>();
            var enclosed = new List<string>();
            foreach (RigLevelRecord lvl in data.Levels)
            {
                if (!lvl.Enclosed) continue;
                enclosed.Add(lvl.Id + " -> " + lvl.DeckId);
                if (string.IsNullOrEmpty(lvl.DeckId) || !rooms.Contains(lvl.DeckId))
                    unjoined.Add(lvl.Id + " names deck '" + lvl.DeckId + "'");
            }

            CollectionAssert.IsEmpty(unjoined,
                "her rig's enclosed levels name rooms her interior sidecar does not have, so the " +
                "cutaway resolves to 0 and the house never opens. WALKABLE has [" +
                string.Join(", ", rooms.OrderBy(r => r)) + "]:\n  " + string.Join("\n  ", unjoined));

            CollectionAssert.AreEquivalent(
                new[] { "house -> house_sole", "cuddy -> cuddy_sole" }, enclosed,
                "the cape's two enclosed levels are the wheelhouse and the cuddy below the whaleback. " +
                "A level joining or leaving this list is her rig changing shape and wants a look, not " +
                "a wider assertion.");
        }

        /// <summary>
        /// <b>Merging her two committed SOURCES keeps both walkables.</b> This is the fixture the
        /// 2026-08-27 refusal actually needed, and its absence is why the id split first landed in the
        /// wrong file.
        ///
        /// <para><b>The mechanism, measured.</b> The kit's pre-merged gameplay mirror is
        /// <c>BoatInteriorGameplayMerge.Merge(docs/art/rigs/gameplay/&lt;hull&gt;.gameplay.json,
        /// &lt;stem&gt;.interior.json)</c>, and the merge contract is <b>"DECK entries replace by
        /// <c>id</c>"</b>. Her base named the OPEN coaming (sole 9.74) <c>bridge_sole</c> and her
        /// interior names the ENCLOSED skylounge (sole 7.30) <c>bridge_sole</c> — so the merge
        /// REPLACED one with the other and returned seven decks instead of eight, silently deleting
        /// the deck that carries <c>helms[0]</c> and the head of the bridge ladder.</para>
        ///
        /// <para>That is precisely the artefact upstream shipped on 2026-08-27 and #678 refused. It
        /// was never carelessness: it is the merge contract meeting a colliding id, and our committed
        /// mirror escaped it only by a HAND-RENAME to <c>sky_sole</c> that no merge would reproduce
        /// and the next regeneration would have reverted. #685 splits the id in the two SOURCES
        /// instead — the base names the coaming <c>helm_coaming</c> — so the merge appends where it
        /// used to replace.</para>
        ///
        /// <para><b>Why it is stated as an id-collision rule and not as two names.</b> Asserting
        /// "helm_coaming exists" would pin this fix; asserting that no id arrives twice pins the
        /// LAW, and it covers every hull whose interior contributes a deck — see
        /// <see cref="BoatInteriorGameplayMerge"/>'s own contract sentence.</para>
        /// </summary>
        [Test]
        public void MergingHerTwoSources_AppendsBothWalkables_AndReplacesNothing()
        {
            string baseJson = File.ReadAllText(Path.Combine(
                RepoRoot, "docs/art/rigs/gameplay/sportFisherSkybridgeIso.gameplay.json"));
            string interiorJson = File.ReadAllText(Path.Combine(
                RepoRoot, KitFolder, "sportFisherIsoRig2.skybridge.interior.json"));

            InteriorMergeResult merged = BoatInteriorGameplayMerge.Merge(baseJson, interiorJson);
            CollectionAssert.IsEmpty(merged.Errors, string.Join("\n  ", merged.Errors));
            Assert.IsTrue(merged.Ok);

            // The report says what it did. A REPLACE on a DECK is a deck destroyed, every time.
            var replaced = merged.Changes
                .Where(c => c.StartsWith("DECK ", System.StringComparison.Ordinal)
                            && c.Contains("REPLACED"))
                .ToList();
            CollectionAssert.IsEmpty(replaced,
                "the merge REPLACED a DECK entry, which means one walkable overwrote another under a " +
                "shared id and the loser is simply gone. Full report:\n  " +
                string.Join("\n  ", merged.Changes));

            var byId = new Dictionary<string, (double Z, int Verts)>(System.StringComparer.Ordinal);
            var order = new List<string>();
            foreach (object d in DeckSidecarJson.AsArray(merged.Merged["DECK"]))
            {
                string id = DeckSidecarJson.String(DeckSidecarJson.Member(d, "id"));
                if (string.IsNullOrEmpty(id)) continue;
                order.Add(id);
                byId[id] = (DeckSidecarJson.Float(DeckSidecarJson.Member(d, "z"), float.NaN),
                            DeckSidecarJson.AsArray(DeckSidecarJson.Member(d, "polygon"))?.Count ?? 0);
            }
            CollectionAssert.AllItemsAreUnique(order,
                "the merged DECK list names an id twice — the collision survived the merge instead of " +
                "being destroyed by it, which is worse, not better.");

            string found = string.Join(", ", order.Select(k => k + "@" + byId[k].Z));
            var atCoaming = byId.Where(kv => System.Math.Abs(kv.Value.Z - 9.74) < 1e-3).ToList();
            var atSkylounge = byId.Where(kv => System.Math.Abs(kv.Value.Z - 7.30) < 1e-3).ToList();
            Assert.AreEqual(1, atCoaming.Count,
                "the merge produced no single deck at z 9.74. That is the coaming, and losing it here " +
                "is how it was lost on 2026-08-27. Merged: [" + found + "]");
            Assert.AreEqual(1, atSkylounge.Count,
                "the merge produced no single deck at z 7.30 — the enclosed skylounge the interior " +
                "contributes. Merged: [" + found + "]");
            Assert.AreEqual(42, atCoaming[0].Value.Verts, "the coaming's ring is 42 vertices.");

            // And the routes the interior contributes wholesale must land on the deck they name.
            foreach (object l in DeckSidecarJson.AsArray(merged.Merged["LADDER"]))
            {
                List<object> connects = DeckSidecarJson.AsArray(DeckSidecarJson.Member(l, "connects"));
                if (connects == null || connects.Count == 0) continue;
                string top = DeckSidecarJson.String(connects[connects.Count - 1]);
                double z1 = DeckSidecarJson.Float(DeckSidecarJson.Member(l, "z1"), float.NaN);
                if (top == null || !byId.TryGetValue(top, out var deck)) continue;
                Assert.LessOrEqual(System.Math.Abs(deck.Z - z1), 0.60,
                    "after the merge, LADDER " + DeckSidecarJson.String(DeckSidecarJson.Member(l, "id")) +
                    " tops out at z " + z1 + " and names " + top + " at z " + deck.Z + ".");
            }
        }

        /// <summary>
        /// <b>The coaming's polygon is made of the RIG'S OWN points.</b> The 2026-08-28 changeset
        /// arrived with this ring recomputed and 38 of its 42 vertices wrong — by up to 0.050 m in x,
        /// under a note claiming it was "computed by the rig's own volume xAt law". It was not: the
        /// deviation is zero at the four knots of the volume's <c>hw</c> half-breadth table
        /// (y −1.1, −2.5, −4.5, −8.75) and peaks mid-segment, which is the signature of interpolating
        /// that table LINEARLY where the rig runs it through <c>mono()</c>, a Fritsch–Carlson
        /// monotone cubic. Reproducing it linearly matched all 42 vertices to zero error.
        ///
        /// <para>So the sidecar's ring is checked against the rig rather than against a prose claim.
        /// The rig emits this sole as flat quads at exactly z 9.74 (<c>volume()</c>'s <c>o.open</c>
        /// branch: <c>face([[-ax,A.y,sole],[ax,A.y,sole],[bx,B.y,sole],[-bx,B.y,sole]])</c>), so every
        /// polygon vertex must be a point the rig actually put there. It is a SUBSET test on purpose —
        /// the pod and the two seats also have bottoms on that plane, and their vertices are no
        /// business of the deck's outline.</para>
        ///
        /// <para>Extracted from <c>hull-rigs/</c>, the copy this sidecar's
        /// <c>derivedFromRigSha256</c> actually pins, rather than from the fleet's
        /// <c>docs/art/rigs/</c> copy — the two are different files here (205a93c9… and 152eb5f3…),
        /// they agree on this volume, and asserting against the pinned one is what makes the pin mean
        /// something.</para>
        /// </summary>
        [Test]
        public void TheCoamingsOutlineIsBuiltFromVerticesTheRigActuallyEmits()
        {
            FleetHull sky = HullMeshFleet.Hulls.Single(h => h.Key == "sportFisherSkybridge");
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = RigMeshExtractor.ExtractFrom(
                host, KitFolder + "/hull-rigs/sportFisherIsoRig2.js", sky.GlobalName,
                hull: sky.Extraction);

            const double Sole = 9.74, Eps = 5e-4;
            var onThePlane = new HashSet<(long X, long Y)>();
            foreach (RigFace f in data.Faces)
            {
                bool flat = true;
                foreach (Vector3d v in f.V)
                    if (System.Math.Abs(v.Z - Sole) > Eps) { flat = false; break; }
                if (!flat) continue;
                foreach (Vector3d v in f.V)
                    onThePlane.Add(((long)System.Math.Round(v.X * 1000.0),
                                    (long)System.Math.Round(v.Y * 1000.0)));
            }
            Assert.Greater(onThePlane.Count, 40,
                "the rig emitted almost nothing on the 9.74 plane — the extraction found the wrong " +
                "hull, or the coaming has moved.");

            object root = DeckSidecarJson.Parse(File.ReadAllText(Path.Combine(
                RepoRoot, KitFolder, "gameplay", "sportFisherIsoRig2.skybridge.gameplay.json")));
            List<object> atSole = DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "DECK"))
                .Where(d => System.Math.Abs(
                    DeckSidecarJson.Float(DeckSidecarJson.Member(d, "z"), float.NaN) - Sole) < 1e-6)
                .ToList();
            Assert.AreEqual(1, atSole.Count,
                "the sidecar carries no single deck at z 9.74, so there is no outline to check. That " +
                "is the deleted-coaming failure, and the fixture above is the one that says so.");
            object coaming = atSole[0];

            var strangers = new List<string>();
            List<object> poly = DeckSidecarJson.AsArray(DeckSidecarJson.Member(coaming, "polygon"));
            for (int i = 0; i < poly.Count; i++)
            {
                List<object> pt = DeckSidecarJson.AsArray(poly[i]);
                double x = DeckSidecarJson.Float(pt[0], float.NaN);
                double y = DeckSidecarJson.Float(pt[1], float.NaN);
                var key = ((long)System.Math.Round(x * 1000.0), (long)System.Math.Round(y * 1000.0));
                if (!onThePlane.Contains(key)) strangers.Add("[" + i + "] (" + x + ", " + y + ")");
            }

            CollectionAssert.IsEmpty(strangers,
                "these polygon vertices are at no point the rig puts on the 9.74 plane, so the " +
                "outline was recomputed rather than read. That is the 2026-08-28 defect verbatim — a " +
                "ring derived with a linear hw interpolant instead of the rig's monotone cubic:\n  " +
                string.Join("\n  ", strangers));
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
