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
            Assert.AreEqual(stem, Path.GetFileNameWithoutExtension(path),
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
                Assert.IsNotNull(icon, $"{d.Id}: no icon (catch card, sell screen)");
                Assert.AreEqual(FishIconBuilder.IconPath(RigKey(d.Id)), PathOf(icon),
                    $"{d.Id} icon: must be the tight new-art icon, not the padded deck cell");
                Assert.AreSame(icon, d.Sprite, $"{d.Id}: the Def's Sprite and the icon library must be the same art");
                n++;
            }
            Assert.GreaterOrEqual(n, 7, "the seven rig species were all checked");
        }

        /// <summary>The share of a square icon slot's WIDTH the fish must fill. The old flat icons
        /// (48x32 art) filled all of it; the padded 64x80 deck cell filled about 40%.</summary>
        const float MinIconSlotFill = 0.8f;

        /// <summary>
        /// <b>The fish fills the slot.</b> The catch card and the sell screen draw an icon in a SQUARE slot
        /// with preserveAspect, so the sprite's longer side meets the slot and the fish's opaque width is
        /// <c>opaque / max(w, h)</c> of it. Read off the committed PNG (the icon imports non-readable).
        /// </summary>
        [Test]
        public void EveryFinfishIcon_FillsItsSlotWidth()
        {
            IconLibrary icons = Icons();
            int n = 0;
            foreach (FishSpeciesDef d in RigFinfish())
            {
                Sprite icon = IconFor(icons, d.Id);
                Assert.IsNotNull(icon, $"{d.Id}: no icon");
                FishSheetPixels.Image png = FishSheetPixels.Read(PathOf(icon));
                Assert.IsFalse(png.IsEmpty, $"{PathOf(icon)}: could not be read (LFS pointer not pulled?)");
                Assert.AreEqual(new Vector2(png.Width, png.Height), icon.rect.size,
                    $"{d.Id}: the icon sprite must be the whole PNG (Single sprite mode)");

                RectInt opaque = FishSheetPixels.OpaqueBounds(png, new RectInt(0, 0, png.Width, png.Height));
                float fill = opaque.width / Mathf.Max(icon.rect.width, icon.rect.height);
                Assert.GreaterOrEqual(fill, MinIconSlotFill,
                    $"{d.Id}: the fish fills {fill:P0} of the icon slot's width");
                n++;
            }
            Assert.GreaterOrEqual(n, 7, "the seven rig species were all checked");
        }

        /// <summary>
        /// <b>The icon IS the new art</b>: each committed icon is, pixel for pixel, its large rung's deck
        /// cell trimmed to the fish (<see cref="FishIconBuilder.TrimmedIcon"/>). A fish re-bake that is not
        /// followed by Build Fish Icons fails here instead of leaving a stale icon behind.
        /// </summary>
        [Test]
        public void EveryFinfishIcon_IsItsDeckCell_TrimmedTight()
        {
            foreach (FishSpeciesDef d in RigFinfish())
            {
                string key = RigKey(d.Id);
                FishSheetPixels.Image want = FishIconBuilder.TrimmedIcon(key);
                Assert.IsFalse(want.IsEmpty, $"{d.Id}: could not cut '{FishIconBuilder.SourceSprite(key)}'");
                FishSheetPixels.Image got = FishSheetPixels.Read(FishIconBuilder.IconPath(key));
                Assert.IsFalse(got.IsEmpty, $"{FishIconBuilder.IconPath(key)}: missing");
                Assert.AreEqual(want.Width, got.Width, $"{d.Id}: icon width");
                Assert.AreEqual(want.Height, got.Height, $"{d.Id}: icon height");
                int wrong = 0;
                for (int i = 0; i < want.Pixels.Length; i++)
                {
                    Color32 a = want.Pixels[i], b = got.Pixels[i];
                    if (a.a != b.a || (a.a != 0 && (a.r != b.r || a.g != b.g || a.b != b.b))) wrong++;
                }
                Assert.AreEqual(0, wrong, $"{d.Id}: {wrong} icon pixels differ from the deck cell (re-run Build Fish Icons)");
            }
        }

        /// <summary>The icons import under the art lock: a Single sprite, point filtered, uncompressed,
        /// PPU 32.</summary>
        [Test]
        public void EveryFinfishIcon_ImportsUnderTheArtLock()
        {
            foreach (FishSpeciesDef d in RigFinfish())
            {
                string path = FishIconBuilder.IconPath(RigKey(d.Id));
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(imp, $"{path}: not imported as a texture");
                Assert.AreEqual(TextureImporterType.Sprite, imp.textureType, path);
                Assert.AreEqual(SpriteImportMode.Single, imp.spriteImportMode, path);
                Assert.AreEqual(FilterMode.Point, imp.filterMode, path);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, imp.textureCompression, path);
                Assert.AreEqual(32f, imp.spritePixelsPerUnit, path);
                Assert.IsFalse(imp.mipmapEnabled, path);
            }
        }

        /// <summary>
        /// The shellfish keep their flat icons for now. The pass 2 shell art is drawn at true size in the
        /// tote (an 8 px cell with a 2-3 px shell), so in the 64 px card slot it would be a blob. Icon-scale
        /// shellfish art is owed; until it lands no shellfish icon may be the tiny tote art.
        /// </summary>
        [Test]
        public void NoShellfishIcon_IsTheTrueSizeToteArt()
        {
            IconLibrary icons = Icons();
            int n = 0;
            foreach (FishSpeciesDef d in Shellfish())
            {
                foreach (Sprite s in new[] { IconFor(icons, d.Id), d.Sprite })
                {
                    if (s == null) continue;
                    StringAssert.DoesNotStartWith(Storage, PathOf(s), $"{d.Id}: '{s.name}' is tote art, too small for an icon");
                    Assert.GreaterOrEqual(s.rect.width, KeptShellfishIconMinPx, $"{d.Id}: '{s.name}' is too small to read as an icon");
                }
                n++;
            }
            Assert.GreaterOrEqual(n, 5, "the shellfish were all checked");
        }

        /// <summary>The smallest sprite, px wide, that still reads in the card's icon slot.</summary>
        const float KeptShellfishIconMinPx = 16f;

        /// <summary>The old flat sprites the shellfish still use (see <see cref="NoShellfishIcon_IsTheTrueSizeToteArt"/>).</summary>
        static readonly string[] KeptShellfishIcons = { "SoftShellClam" };

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
                    // The LADDER's wiring, so with the readability floor off: every rung is reachable.
                    string kind = art.SizedKindFor(d.Id, r.Kg, 0f);
                    Assert.AreEqual(key + r.Suffix, kind, $"{d.Id} at {r.Kg} kg: the sized kind");
                    Assert.AreEqual(8, art.HeldFacingsFor(kind), $"{kind}: eight held facings");
                    AssertNewFishSheet(art.HeldSprite(kind, 0, 0), $"Fish_{kind}_tail", $"{kind} in the hands");
                    AssertNewFishSheet(art.SpriteFor(kind, 0), $"Fish_{kind}_deck", $"{kind} in the tote");
                    Assert.IsNotNull(art.HeldMeshFor(kind), $"{kind}: a held mesh");
                }
            }
        }

        /// <summary>The hands, the tote and the fight pick the SAME size for the same fish, at every
        /// weight the species can roll, with the readability floor applied (the line asks it through
        /// <see cref="FishArtLibrary.LineRungFor"/>, exactly as <c>RodFightPresenter</c> does).</summary>
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
                    FishRungVisual rung = FishArtLibrary.LineRungFor(v, art, d.Id, kg);
                    Assert.AreEqual(RigKey(d.Id) + rung.Suffix, art.SizedKindFor(d.Id, kg), $"{d.Id} at {kg:0.###} kg");
                }
            }
        }

        // ---- the readability floor: a fish too small to read is shown with a readable drawing -------

        /// <summary>Every sized kind's drawn length is the one measured off its own held sheet, so the
        /// floor reads the art and not a number someone typed.</summary>
        [Test]
        public void EverySizedKind_HeldLength_IsMeasuredOffItsHeldSheet()
        {
            CatchItemLibrary art = CatchArt();
            foreach (FishSpeciesDef d in RigFinfish())
                foreach (FishRungVisual r in FishArt().Find(d.Id).Rungs)
                {
                    string kind = RigKey(d.Id) + r.Suffix;
                    int measured = FishSheetPixels.HeldLengthPx($"{FishIso}Fish_{kind}_tail.png");
                    Assert.Greater(measured, 0, $"{kind}: the held sheet could not be measured");
                    Assert.AreEqual(measured, art.HeldLengthPxFor(kind),
                        $"{kind}: HeldLengthPx is stale (re-run Build Catch Item Library)");
                }
        }

        /// <summary>
        /// With the shipped floor, no fish is drawn shorter than it while its species has a drawing that
        /// is not, in the hands and in the tote alike; and a species with no drawing long enough is drawn
        /// with its largest.
        /// </summary>
        [Test]
        public void NoFish_IsDrawnShorterThanTheFloor_WhileItsSpeciesHasALongerDrawing()
        {
            CatchItemLibrary art = CatchArt();
            float floor = GameConfig.DefaultMinDrawnFishLengthPx;
            Assert.Greater(floor, 0f, "premise: the shipped floor is on");
            foreach (FishSpeciesDef d in RigFinfish())
            {
                FishRungVisual[] rungs = FishArt().Find(d.Id).Rungs;
                string key = RigKey(d.Id);
                int longest = rungs.Max(r => art.HeldLengthPxFor(key + r.Suffix));
                string largest = key + rungs.OrderBy(r => r.Kg).Last().Suffix;
                for (int i = 0; i <= 40; i++)
                {
                    float kg = Mathf.Lerp(d.MinWeightKg, d.MaxWeightKg, i / 40f);
                    string kind = art.SizedKindFor(d.Id, kg, floor);
                    if (longest >= floor)
                        Assert.GreaterOrEqual(art.HeldLengthPxFor(kind), floor, $"{d.Id} at {kg:0.###} kg drew {kind}");
                    else
                        Assert.AreEqual(largest, kind, $"{d.Id} at {kg:0.###} kg: nothing reads, so the largest");
                }
            }
        }

        [Test]
        public void ReadableWeight_RaisesOnlyPastTheRungsTooShortToRead()
        {
            var lib = ScriptableObject.CreateInstance<CatchItemLibrary>();
            try
            {
                lib.Configure(new[]
                {
                    new CatchItemLibrary.KindEntry { Kind = "fish_sm", SizeOf = "fish", SizeKg = 1f, HeldLengthPx = 8 },
                    new CatchItemLibrary.KindEntry { Kind = "fish", SizeKg = 3f, HeldLengthPx = 13 },
                    new CatchItemLibrary.KindEntry { Kind = "fish_lg", SizeOf = "fish", SizeKg = 8f, HeldLengthPx = 17 },
                }, new[] { new CatchItemLibrary.SpeciesEntry { SpeciesId = "fish.test_fish", Kind = "fish" } });

                Assert.AreEqual(3f, lib.ReadableWeightKg("fish.test_fish", 0.5f, 12f), "raised to the lightest readable rung");
                Assert.AreEqual(5f, lib.ReadableWeightKg("fish.test_fish", 5f, 12f), "a heavier fish is left alone");
                Assert.AreEqual(8f, lib.ReadableWeightKg("fish.test_fish", 0.5f, 20f), "nothing reads: the largest rung");
                Assert.AreEqual(0.5f, lib.ReadableWeightKg("fish.test_fish", 0.5f, 0f), "a floor of 0 is off");
                Assert.AreEqual("fish", lib.SizedKindFor("fish.test_fish", 0.9f, 12f));
                Assert.AreEqual("fish_sm", lib.SizedKindFor("fish.test_fish", 0.9f, 0f));
                Assert.AreEqual("fish_lg", lib.SizedKindFor("fish.test_fish", 0.9f, 16f));
                Assert.AreEqual("fish_lg", lib.SizedKindFor("fish.test_fish", 7f, 12f), "the floor never shrinks a fish");

                // Unmeasured rungs (0) are left alone: data from before the measurement behaves as before.
                lib.Configure(new[]
                {
                    new CatchItemLibrary.KindEntry { Kind = "fish_sm", SizeOf = "fish", SizeKg = 1f },
                    new CatchItemLibrary.KindEntry { Kind = "fish", SizeKg = 3f },
                }, null);
                Assert.AreEqual("fish_sm", lib.SizedKindFor("fish.test_fish", 0.9f, 12f));
            }
            finally { Object.DestroyImmediate(lib); }
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
                             ("fish.atlantic_cod", 12f, "cod_lg"),
                             // The small mackerel is drawn 10 px long, under the 12 px readability floor,
                             // so a 0.3 kg mackerel is held as the middle-rung drawing (13 px).
                             ("fish.mackerel", 0.3f, "mackerel"), ("fish.mackerel", 1.4f, "mackerel_lg"),
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

        // ---- small fish read in the hand: the face-on flatfish and the contrast line -----------------

        /// <summary>The flatfish is flagged on its def, the round fish are not, and the flag reaches
        /// every rung of the kind it draws as (and nothing else).</summary>
        [Test]
        public void TheFlounder_IsHeldFaceOn_AtEveryRung_AndNoRoundFishIs()
        {
            CatchItemLibrary art = CatchArt();
            FishArtLibrary lib = FishArt();
            List<FishSpeciesDef> rig = RigFinfish().ToList();
            Assert.IsTrue(rig.Single(d => d.Id == "fish.winter_flounder").HeldBroadSideOn,
                "the winter flounder is a flatfish: held, it shows its broad side");
            int mapped = 0;
            foreach (FishSpeciesDef d in rig)
                foreach (FishRungVisual r in lib.Find(d.Id).Rungs)
                {
                    string kind = RigKey(d.Id) + r.Suffix;
                    int[] map = art.HeldFacingMapFor(kind);
                    if (d.HeldBroadSideOn)
                    {
                        Assert.IsNotNull(map, $"{kind}: a flatfish rung with no face-on map (re-run Build Catch Item Library)");
                        Assert.AreEqual(art.HeldFacingsFor(kind), map.Length, $"{kind}: one entry per facing");
                        mapped++;
                    }
                    else Assert.IsNull(map, $"{kind}: a round fish turns with him, it is never re-faced");
                }
            Assert.AreEqual(3, mapped, "the flounder's three rungs");
        }

        /// <summary>
        /// Each face-on map is the one measured off its own held sheet, no facing draws less of the fish
        /// than it did, and the narrowest view the hands can show is wider than the edge-on sliver was.
        /// </summary>
        [Test]
        public void EveryFaceOnMap_IsMeasuredOffItsHeldSheet_AndShowsMoreOfTheFish()
        {
            CatchItemLibrary art = CatchArt();
            foreach (FishSpeciesDef d in RigFinfish().Where(x => x.HeldBroadSideOn))
                foreach (FishRungVisual r in FishArt().Find(d.Id).Rungs)
                {
                    string kind = RigKey(d.Id) + r.Suffix;
                    string sheet = $"{FishIso}Fish_{kind}_tail.png";
                    Sprite[] held = Enumerable.Range(0, 8).Select(f => art.HeldSprite(kind, f, 0)).ToArray();
                    int[] map = art.HeldFacingMapFor(kind);
                    Assert.IsNotNull(map, kind);
                    // Each facing's own still cell, read off the sheet by name (HeldSprite applies the map).
                    Sprite[] cells = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().ToArray();
                    var own = new Sprite[8];
                    for (int f = 0; f < 8; f++)
                        own[f] = cells.Single(s => s.name == $"Fish_{kind}_tail_d{f}_f0");

                    int[] areas = FishSheetPixels.HeldFacingAreas(sheet, own, 8);
                    Assert.IsNotNull(areas, $"{sheet}: could not be measured");
                    CollectionAssert.AreEqual(FishSheetPixels.BroadSideFacingMap(areas), map,
                        $"{kind}: the face-on map is stale (re-run Build Catch Item Library)");
                    for (int f = 0; f < 8; f++)
                    {
                        Assert.AreSame(own[map[f]], held[f], $"{kind} facing {f}: the hands draw the mapped cell");
                        Assert.GreaterOrEqual(areas[map[f]], areas[f], $"{kind} facing {f}: never shows less of the fish");
                    }
                    Assert.Greater(Enumerable.Range(0, 8).Min(f => areas[map[f]]), areas.Min(),
                        $"{kind}: the narrowest view in the hands must be wider than the edge-on sliver");
                }
        }

        [Test]
        public void BroadSideFacingMap_TakesTheBroadestNeighbour_AndKeepsATie()
        {
            //                    d0   d1  d2  d3   d4   d5  d6  d7
            int[] areas = { 100, 100, 20, 25, 100, 104, 22, 18 };
            CollectionAssert.AreEqual(new[] { 0, 1, 1, 4, 5, 5, 5, 0 }, FishSheetPixels.BroadSideFacingMap(areas));
            Assert.IsNull(FishSheetPixels.BroadSideFacingMap(null));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, FishSheetPixels.BroadSideFacingMap(new[] { 5, 5, 5 }),
                "all equal: every facing keeps itself");
        }

        [Test]
        public void HeldSprite_DrawsTheMappedFacing_AndIgnoresABadMap()
        {
            var lib = ScriptableObject.CreateInstance<CatchItemLibrary>();
            var sprites = new List<Sprite>();
            var tex = new Texture2D(8, 1);
            try
            {
                for (int i = 0; i < 4; i++) sprites.Add(Sprite.Create(tex, new Rect(i * 2, 0, 2, 1), Vector2.zero));
                var flat = new CatchItemLibrary.KindEntry
                {
                    Kind = "flat", Held = sprites.ToArray(), HeldFacings = 4, HeldFramesPerFacing = 1,
                    HeldFacingMap = new[] { 0, 0, 3, 3 },
                };
                var bad = new CatchItemLibrary.KindEntry
                {
                    Kind = "bad", Held = sprites.ToArray(), HeldFacings = 4, HeldFramesPerFacing = 1,
                    HeldFacingMap = new[] { 1, 2 },
                };
                lib.Configure(new[] { flat, bad }, null);
                Assert.AreSame(sprites[0], lib.HeldSprite("flat", 1, 0));
                Assert.AreSame(sprites[3], lib.HeldSprite("flat", 2, 0));
                Assert.AreSame(sprites[3], lib.HeldSprite("flat", 6, 0), "the facing wraps before the map applies");
                Assert.AreSame(sprites[1], lib.HeldSprite("bad", 1, 0), "a map the wrong length is ignored");
                Assert.IsNull(lib.HeldFacingMapFor("unknown"));
            }
            finally
            {
                foreach (Sprite s in sprites) Object.DestroyImmediate(s);
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(lib);
            }
        }

        /// <summary>
        /// A fish drawn short enough is held with the contrast line: darker copies of its own sprite behind
        /// it, a sprite pixel to the side, that turn with it and sort under it inside one sorting group. A
        /// fish long enough to read, and a handful of clams, get none.
        /// </summary>
        [Test]
        public void ASmallHeldFish_GetsTheContrastLine_AndALargeOneDoesNot()
        {
            CatchItemLibrary art = CatchArt();
            Vector2[] offsets = GameServices.HeldFishKeylineOffsetsPx;
            float maxPx = GameServices.HeldFishKeylineMaxLengthPx;
            float shade = GameServices.HeldFishKeylineShade;
            Assert.Greater(maxPx, 0f, "premise: the shipped line is on");
            Assert.Greater(offsets.Length, 0, "premise: the shipped line draws at least one copy");
            var made = new List<GameObject>();
            try
            {
                var mackerel = new CatchItem("fish.mackerel", "Mackerel", FishCategory.Pelagic, 0.9f, 10, 0.1f);
                CarriableCatch small = CarriableCatch.Create(mackerel, null, art);
                made.Add(small.gameObject);
                Assert.LessOrEqual(art.HeldLengthPxFor(small.ArtKind), maxPx, $"premise: {small.ArtKind} is a small drawing");
                Assert.AreEqual(offsets.Length, small.KeylineCount, $"{small.ArtKind}: one copy per offset");
                SpriteRenderer fish = small.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(small.GetComponent<UnityEngine.Rendering.SortingGroup>(), "the fish and its line sort as one");

                small.RideSortingBand(0, 7);
                small.ShowFacing(3);
                for (int i = 0; i < small.KeylineCount; i++)
                {
                    SpriteRenderer line = small.Keyline(i);
                    Assert.AreSame(fish.sprite, line.sprite, "the line is the fish's own drawing, turned with it");
                    Assert.AreEqual(new Color(shade, shade, shade, 1f), line.color, "a step down the fish's own ramp");
                    Assert.AreEqual((Vector3)(offsets[i] / fish.sprite.pixelsPerUnit), line.transform.localPosition,
                        "moved whole sprite pixels");
                    Assert.Less(line.sortingOrder, fish.sortingOrder, "the line draws under the fish");
                }
                Assert.AreEqual(7, small.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder,
                    "the group rides the body's band, so the line can never sort under the body");

                var cod = new CatchItem("fish.atlantic_cod", "Atlantic Cod", FishCategory.InshoreGroundfish, 12f, 10, 0.1f);
                CarriableCatch big = CarriableCatch.Create(cod, null, art);
                made.Add(big.gameObject);
                Assert.Greater(art.HeldLengthPxFor(big.ArtKind), maxPx, "premise: the large cod reads on its own");
                Assert.AreEqual(0, big.KeylineCount, "a fish long enough to read gets no line");
                Assert.IsNull(big.GetComponent<UnityEngine.Rendering.SortingGroup>());

                var clam = new CatchItem("fish.soft_shell_clam", "clam", FishCategory.Shellfish, 0.1f, 2, 0.45f);
                CarriableCatch handful = CarriableCatch.Create(clam, null, art);
                made.Add(handful.gameObject);
                Assert.AreEqual(0, handful.KeylineCount, "a handful of shellfish is not a fish");
            }
            finally
            {
                foreach (GameObject go in made) Object.DestroyImmediate(go);
            }
        }

        /// <summary>With the shipped default, the line falls on exactly the drawings no longer than it: the
        /// small rungs and the tiny species, never a cod.</summary>
        [Test]
        public void TheContrastLine_FallsOnTheSmallDrawings_NeverACod()
        {
            CatchItemLibrary art = CatchArt();
            float maxPx = GameConfig.DefaultHeldFishKeylineMaxLengthPx;
            int lined = 0;
            foreach (FishSpeciesDef d in RigFinfish())
                foreach (FishRungVisual r in FishArt().Find(d.Id).Rungs)
                {
                    string kind = RigKey(d.Id) + r.Suffix;
                    bool line = art.HeldLengthPxFor(kind) <= maxPx;
                    if (RigKey(d.Id) == "cod") Assert.IsFalse(line, $"{kind}: a cod reads on its own");
                    if (r.Suffix == "_lg" && RigKey(d.Id) != "herring" && RigKey(d.Id) != "mackerel")
                        Assert.IsFalse(line, $"{kind}: a large rung reads on its own");
                    if (line) lined++;
                }
            Assert.GreaterOrEqual(lined, 5, "the small drawings are lined");
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
        /// The old flat fish sprites are retired: no shipped asset, prefab or scene points at any of them,
        /// except the shellfish icons kept on purpose (<see cref="KeptShellfishIcons"/>). (The files
        /// themselves are left for a deliberate delete.)
        /// </summary>
        [Test]
        public void NothingShipped_ReferencesTheOldFishSprites()
        {
            // Read the guids off the .meta files themselves, so the check does not depend on the PNGs
            // having imported.
            string[] oldGuids = Directory.GetFiles(OldFishFolder, "*.png.meta")
                .Where(m => !KeptShellfishIcons.Contains(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(m))))
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
