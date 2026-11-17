using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests
{
    /// <summary>
    /// <b>Every fish is drawn with the new fish art, everywhere it is drawn</b>: the catch card and sell
    /// screen icon, the fight on the line and the land pose, the hands, and the tote — each picked by data
    /// (the fish Def, the icon library, the shared fish art table, the catch art table), each at the
    /// catch's own size where the rig drew more than one.
    ///
    /// <para>These read the COMMITTED assets, because the assets are what the game loads. The roster is
    /// read from <c>Data/Fish</c>, not typed here, so a species added tomorrow is held to the same bar the
    /// day it lands, or must be named in <see cref="OwedNoRigArt"/>.</para>
    /// </summary>
    public class FishArtEverywhereTests
    {
        const string DataFish = "Assets/_Project/Data/Fish";
        const string FishIso = "Assets/_Project/Art/Fishing/Iso/";
        const string Storage = "Assets/_Project/Art/Fishing/Storage/";
        const string OldFishFolder = "Assets/_Project/Art/Sprites/Fish/";

        /// <summary>
        /// Finfish the fish rig does not draw yet. They are OWED rig art; until it lands they fall back
        /// to a stand-in kind in the hands and the tote and draw nothing on the line. Listed so the gap
        /// is visible, and checked so the list cannot go stale: when one of them gets art, the test
        /// below fails and asks for it to be moved off this list.
        /// </summary>
        static readonly string[] OwedNoRigArt = { "fish.atlantic_tomcod", "fish.cunner", "fish.rainbow_smelt" };

        /// <summary>The trap kit's crustacean art (Art/Fishing/Lobster.png, RockCrab.png), pinned by
        /// TrapKitContentValidationTests; not part of this set and not old art.</summary>
        static readonly string[] Crustaceans = { "fish.lobster", "fish.rock_crab", "fish.green_crab" };

        // ---- the data -------------------------------------------------------------------------------

        static List<FishSpeciesDef> Roster()
        {
            var defs = AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { DataFish })
                .Select(g => AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null && !string.IsNullOrEmpty(d.Id))
                .OrderBy(d => d.Id, StringComparer.Ordinal)
                .ToList();
            Assert.GreaterOrEqual(defs.Count, 10, "the shipped fish roster must load");
            return defs;
        }

        static IEnumerable<FishSpeciesDef> RigFinfish() => Roster().Where(d =>
            d.Category != FishCategory.Shellfish && !OwedNoRigArt.Contains(d.Id));

        static IEnumerable<FishSpeciesDef> Shellfish() => Roster().Where(d =>
            d.Category == FishCategory.Shellfish && !Crustaceans.Contains(d.Id));

        static FishArtLibrary FishArt()
        {
            var lib = AssetDatabase.LoadAssetAtPath<FishArtLibrary>(FishArtLibraryBuilder.LibraryPath);
            Assert.IsNotNull(lib, $"'{FishArtLibraryBuilder.LibraryPath}' must be committed: it is how St " +
                                  "Peters and Nine Mile Creek reach the new fish art at all.");
            return lib;
        }

        static CatchItemLibrary CatchArt()
        {
            var lib = AssetDatabase.LoadAssetAtPath<CatchItemLibrary>(CatchItemLibraryBuilder.LibraryPath);
            Assert.IsNotNull(lib, $"'{CatchItemLibraryBuilder.LibraryPath}' must be committed");
            return lib;
        }

        static IconLibrary Icons()
        {
            var lib = Resources.Load<IconLibrary>(IconRegistrar.ResourcesPath);
            Assert.IsNotNull(lib, "Resources/IconLibrary must ship");
            return lib;
        }

        static Sprite IconFor(IconLibrary lib, string id)
        {
            if (lib.Entries == null) return null;
            foreach (IconLibrary.Entry e in lib.Entries)
                if (e.Id == id) return e.Icon;
            return null;
        }

        static string PathOf(Object o) => o != null ? AssetDatabase.GetAssetPath(o) : null;

        /// <summary>'fish.atlantic_cod' → 'cod': the rig's own species key, the id's last word.</summary>
        static string RigKey(string id)
        {
            string leaf = id.Substring(id.LastIndexOf('.') + 1);
            return leaf.Substring(leaf.LastIndexOf('_') + 1);
        }

        static void AssertNewFishSheet(Sprite s, string stem, string what)
        {
            Assert.IsNotNull(s, $"{what}: no sprite");
            string path = PathOf(s);
            StringAssert.StartsWith(FishIso, path, $"{what}: must be the new iso fish art, got '{path}'");
            StringAssert.StartsWith(stem + "_", Path.GetFileNameWithoutExtension(path),
                $"{what}: wrong sheet (species or size) '{path}'");
        }

        // ---- the icon: catch card, sell screen ------------------------------------------------------

        [Test]
        public void EveryRigFish_HasANewArtIcon_AndItsDefAgrees()
        {
            IconLibrary icons = Icons();
            int n = 0;
            foreach (FishSpeciesDef d in RigFinfish())
            {
                if (Crustaceans.Contains(d.Id)) continue;
                Sprite icon = IconFor(icons, d.Id);
                AssertNewFishSheet(icon, $"Fish_{RigKey(d.Id)}_lg_deck", $"{d.Id} icon (catch card, sell screen)");
                Assert.AreSame(icon, d.Sprite, $"{d.Id}: the Def's Sprite and the icon library must be the same art");
                n++;
            }
            Assert.GreaterOrEqual(n, 7, "the seven rig species were all checked");
        }

        [Test]
        public void EveryShellfish_HasANewArtIcon_AndItsDefAgrees()
        {
            IconLibrary icons = Icons();
            int n = 0;
            foreach (FishSpeciesDef d in Shellfish())
            {
                Sprite icon = IconFor(icons, d.Id);
                Assert.IsNotNull(icon, $"{d.Id}: no icon");
                StringAssert.StartsWith(Storage + "CatchItem2_", PathOf(icon), $"{d.Id}: icon must be the pass 2 item art");
                Assert.AreSame(icon, d.Sprite, $"{d.Id}: the Def's Sprite and the icon library must agree");
                n++;
            }
            Assert.GreaterOrEqual(n, 5, "the shellfish were all checked");
        }

        // ---- the fight: on the line and the land pose ----------------------------------------------

        [Test]
        public void EveryRigFish_HasAFightLadder_InTheSharedTable_AtEverySize()
        {
            FishArtLibrary lib = FishArt();
            foreach (FishSpeciesDef d in RigFinfish())
            {
                FishSpeciesVisual v = lib.Find(d.Id);
                Assert.IsNotNull(v, $"{d.Id}: no ladder in the shared fish art table (re-run Build Fish Art Library)");
                Assert.AreEqual(3, v.Rungs.Length, $"{d.Id}: the rig draws three sizes");
                string key = RigKey(d.Id);
                foreach (FishRungVisual r in v.Rungs)
                {
                    string stem = $"Fish_{key}{r.Suffix}";
                    Assert.Greater(r.Kg, 0f, $"{stem}: a rung must say the weight it was drawn at");
                    Assert.AreEqual(8 * r.ShadowFramesPerDir, r.ShadowFrames.Length, $"{stem} shadow");
                    Assert.Greater(r.DartFrames.Length, 0, $"{stem}: dart frames (on the line)");
                    Assert.Greater(r.HeldFrames.Length, 0, $"{stem}: held frames (the land pose)");
                    AssertNewFishSheet(r.ShadowFrames[0], stem + "_shadow", $"{d.Id} shadow");
                    AssertNewFishSheet(r.DartFrames[0], stem + "_dart", $"{d.Id} on the line");
                    AssertNewFishSheet(r.HeldFrames[0], stem + (r.TwoHanded ? "_gill" : "_tail"), $"{d.Id} land pose");
                }
            }
        }

        /// <summary>A scene whose own table is the pre-ladder shape (St Peters) is drawn from the shared
        /// table; a scene whose table answers keeps its own.</summary>
        [Test]
        public void Resolve_PrefersAnInlineLadder_AndFallsBackToTheSharedTable()
        {
            FishArtLibrary lib = FishArt();
            var stale = new FishSpeciesVisual { FishId = "fish.atlantic_cod", Rungs = Array.Empty<FishRungVisual>() };
            Assert.AreSame(lib.Find("fish.atlantic_cod"), FishArtLibrary.Resolve(stale, lib, "fish.atlantic_cod"),
                "a pre-ladder inline entry is treated as absent");
            Assert.AreSame(lib.Find("fish.haddock"), FishArtLibrary.Resolve(null, lib, "fish.haddock"));

            var own = new FishSpeciesVisual { FishId = "fish.atlantic_cod", Rungs = new[] { new FishRungVisual { Kg = 1f } } };
            Assert.AreSame(own, FishArtLibrary.Resolve(own, lib, "fish.atlantic_cod"), "a scene's own ladder wins");
            Assert.IsNull(FishArtLibrary.Resolve(null, lib, "fish.not_a_fish"));
            Assert.IsNull(FishArtLibrary.Resolve(stale, null, "fish.atlantic_cod"), "no table, no ladder: nothing drawn, no throw");
        }

        [Test]
        public void TheRigKey_MatchesTheLastWord_NotASubstring()
        {
            Assert.IsTrue(RodKitImporter.RigKeyMatches("fish.atlantic_cod", "cod"));
            Assert.IsTrue(RodKitImporter.RigKeyMatches("fish.striped_bass", "bass"));
            Assert.IsTrue(RodKitImporter.RigKeyMatches("fish.haddock", "haddock"));
            Assert.IsFalse(RodKitImporter.RigKeyMatches("fish.atlantic_tomcod", "cod"),
                "a tomcod is not a cod: the ladder must never be filed under the wrong species");
        }

        // ---- the hands and the tote ----------------------------------------------------------------

        [Test]
        public void EveryRigFish_IsHeldAndLaid_AtItsOwnSize()
        {
            CatchItemLibrary art = CatchArt();
            FishArtLibrary lib = FishArt();
            Assert.AreSame(art, lib.CatchArt, "the shared table points at the committed catch art table");
            foreach (FishSpeciesDef d in RigFinfish())
            {
                string key = RigKey(d.Id);
                Assert.AreEqual(key, art.KindFor(d.Id), $"{d.Id}: species → kind");
                Assert.IsNotNull(art.HeldMeshFor(key), $"{d.Id}: the held mesh (ADR 0053) is on the kind");
                foreach (FishRungVisual r in lib.Find(d.Id).Rungs)
                {
                    string kind = art.SizedKindFor(d.Id, r.Kg);
                    Assert.AreEqual(key + r.Suffix, kind, $"{d.Id} at {r.Kg} kg: the sized kind");
                    Assert.AreEqual(8, art.HeldFacingsFor(kind), $"{kind}: eight held facings");
                    AssertNewFishSheet(art.HeldSprite(kind, 0, 0), $"Fish_{kind}_tail", $"{kind} in the hands");
                    AssertNewFishSheet(art.SpriteFor(kind, 0), $"Fish_{kind}_deck", $"{kind} in the tote");
                    Assert.IsNotNull(art.HeldMeshFor(kind), $"{kind}: a held mesh");
                }
            }
        }

        /// <summary>The hands, the tote and the fight pick the SAME size for the same fish, at every
        /// weight the species can roll.</summary>
        [Test]
        public void TheSizePicked_IsTheSameOnTheLine_InTheHands_AndInTheTote()
        {
            CatchItemLibrary art = CatchArt();
            FishArtLibrary lib = FishArt();
            foreach (FishSpeciesDef d in RigFinfish())
            {
                FishSpeciesVisual v = lib.Find(d.Id);
                for (int i = 0; i <= 40; i++)
                {
                    float kg = Mathf.Lerp(d.MinWeightKg, d.MaxWeightKg, i / 40f);
                    FishRungVisual rung = v.RungFor(kg);
                    Assert.AreEqual(RigKey(d.Id) + rung.Suffix, art.SizedKindFor(d.Id, kg), $"{d.Id} at {kg:0.###} kg");
                }
            }
        }

        [Test]
        public void ACarriedCatch_DrawsItsOwnSize_FromTheNewArt()
        {
            CatchItemLibrary art = CatchArt();
            var made = new List<GameObject>();
            try
            {
                foreach ((string id, float kg, string kind) in new[]
                         {
                             ("fish.atlantic_cod", 2f, "cod_sm"), ("fish.atlantic_cod", 5.6f, "cod"),
                             ("fish.atlantic_cod", 12f, "cod_lg"), ("fish.mackerel", 0.3f, "mackerel_sm"),
                             ("fish.striped_bass", 20f, "bass_lg"),
                         })
                {
                    var item = new CatchItem(id, id, FishCategory.InshoreGroundfish, kg, 10, 0.1f);
                    CarriableCatch c = CarriableCatch.Create(item, null, art);
                    made.Add(c.gameObject);
                    Assert.AreEqual(kind, c.ArtKind, $"{id} {kg} kg");
                    AssertNewFishSheet(c.GetComponent<SpriteRenderer>().sprite, $"Fish_{kind}_tail", $"{id} {kg} kg in the hands");
                }
                var clam = new CatchItem("fish.soft_shell_clam", "clam", FishCategory.Shellfish, 0.1f, 2, 0.45f);
                CarriableCatch held = CarriableCatch.Create(clam, null, art);
                made.Add(held.gameObject);
                Assert.AreEqual("clam", held.ArtKind, "shellfish are drawn at one size");
                StringAssert.StartsWith(Storage, PathOf(held.GetComponent<SpriteRenderer>().sprite));
            }
            finally
            {
                foreach (GameObject go in made) Object.DestroyImmediate(go);
            }
        }

        /// <summary>St Peters and Nine Mile Creek were saved before the hands had a catch art field: the
        /// hands must still reach the new art.</summary>
        [Test]
        public void HandsWithNoCatchArtWired_StillDrawTheNewArt()
        {
            var go = new GameObject("hands");
            try
            {
                var hands = go.AddComponent<CarryHands>();
                Assert.IsNull(hands.CatchArt, "precondition: nothing wired, as in the two older scenes");
                Assert.AreSame(CatchArt(), hands.EffectiveCatchArt, "the shared table's catch art answers");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SizedKindFor_WithNoLadder_IsKindFor()
        {
            var lib = ScriptableObject.CreateInstance<CatchItemLibrary>();
            try
            {
                lib.Configure(new[] { new CatchItemLibrary.KindEntry { Kind = "cod" } },
                              new[] { new CatchItemLibrary.SpeciesEntry { SpeciesId = "fish.atlantic_cod", Kind = "cod" } });
                Assert.AreEqual("cod", lib.SizedKindFor("fish.atlantic_cod", 11f), "an unsized table answers its plain kind");

                lib.Configure(new[]
                {
                    new CatchItemLibrary.KindEntry { Kind = "cod_lg", SizeOf = "cod", SizeKg = 8f },
                    new CatchItemLibrary.KindEntry { Kind = "cod", SizeKg = 1f },
                }, null);
                Assert.AreEqual("cod", lib.SizedKindFor("fish.atlantic_cod", 1.5f));
                Assert.AreEqual("cod_lg", lib.SizedKindFor("fish.atlantic_cod", 6f));
                // The boundary is half way in cube-root LENGTH (1 and 2 → 3.375 kg), not in kg (4.5).
                Assert.AreEqual("cod", lib.SizedKindFor("fish.atlantic_cod", 3.3f));
                Assert.AreEqual("cod_lg", lib.SizedKindFor("fish.atlantic_cod", 3.45f));
            }
            finally { Object.DestroyImmediate(lib); }
        }

        // ---- the owed list and the retired art -----------------------------------------------------

        [Test]
        public void TheOwedSpecies_StillHaveNoRigArt_SoTheListIsTrue()
        {
            FishArtLibrary lib = FishArt();
            foreach (string id in OwedNoRigArt)
            {
                Assert.IsNull(lib.Find(id), $"{id} now has a fight ladder: take it off OwedNoRigArt");
                Assert.IsFalse(File.Exists($"{FishIso}Fish_{RigKey(id)}_deck.png"),
                    $"{id} now has rig art: take it off OwedNoRigArt and give it an icon");
            }
        }

        /// <summary>
        /// The old flat fish sprites are retired: no shipped asset, prefab or scene points at any of them.
        /// (The files themselves are left for a deliberate delete.)
        /// </summary>
        [Test]
        public void NothingShipped_ReferencesTheOldFishSprites()
        {
            // Read the guids off the .meta files themselves, so the check does not depend on the PNGs
            // having imported.
            string[] oldGuids = Directory.GetFiles(OldFishFolder, "*.png.meta")
                .Select(m => File.ReadLines(m).FirstOrDefault(l => l.StartsWith("guid: ")))
                .Where(l => l != null)
                .Select(l => l.Substring("guid: ".Length).Trim())
                .ToArray();
            Assert.Greater(oldGuids.Length, 0, "premise: the old sprite files are still on disk to be checked against");

            string[] yaml = { ".asset", ".prefab", ".unity", ".spriteatlas", ".spriteatlasv2", ".mat", ".anim", ".controller" };
            var offenders = new List<string>();
            int scanned = 0;
            foreach (string file in Directory.EnumerateFiles("Assets/_Project", "*.*", SearchOption.AllDirectories))
            {
                if (!yaml.Any(e => file.EndsWith(e, StringComparison.OrdinalIgnoreCase))) continue;
                scanned++;
                string text = File.ReadAllText(file);
                foreach (string g in oldGuids)
                    if (text.IndexOf(g, StringComparison.Ordinal) >= 0)
                        offenders.Add($"{file.Replace('\\', '/')} → {AssetDatabase.GUIDToAssetPath(g)}");
            }
            Assert.Greater(scanned, 100, "premise: the shipped assets were actually scanned");
            CollectionAssert.IsEmpty(offenders, "old fish art is still referenced:\n" + string.Join("\n", offenders));
        }
    }
}
