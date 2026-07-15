using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for the trap-kit art WIRING: now that the owner's painted trap art is delivered,
    /// every art slot on the shipped trap-arc Defs must RESOLVE. Before the kit these slots were allowed
    /// empty (the greybox rule); with real art assigned, an empty slot means a broken reference — a sheet
    /// re-slice, a renamed sub-sprite, or a meta regeneration — and must fail RED here, not read as a
    /// silent greybox regression on the owner's next haul.
    /// </summary>
    public class TrapKitContentValidationTests
    {
        private const string IconLibraryPath = "Assets/_Project/Resources/IconLibrary.asset";

        private static List<T> LoadAll<T>() where T : ScriptableObject
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        // ---- TrapDefs: pot (dry + wet), buoy, splash ----------------------------------------------

        [Test]
        public void EveryTrapDef_HasItsPaintedPotAndBuoy()
        {
            var traps = LoadAll<TrapDef>();
            Assert.IsTrue(traps.Count >= 2, "the lobster and crab pots exist");
            foreach (TrapDef trap in traps)
            {
                Assert.IsNotNull(trap.TrapSprite, $"{trap.Id}: TrapSprite (the dry pot) must resolve");
                Assert.IsNotNull(trap.TrapSpriteWet, $"{trap.Id}: TrapSpriteWet (the hauled-up pot) must resolve");
                Assert.IsNotNull(trap.BuoySprite, $"{trap.Id}: BuoySprite must resolve");
            }
        }

        [Test]
        public void LobsterAndCrabPots_ReadApart_WoodVsWire()
        {
            var traps = LoadAll<TrapDef>();
            TrapDef lobster = traps.Find(t => t.Id == "trap.lobster");
            TrapDef crab = traps.Find(t => t.Id == "trap.crab");
            Assert.IsNotNull(lobster, "trap.lobster exists");
            Assert.IsNotNull(crab, "trap.crab exists");
            Assert.AreNotEqual(lobster.TrapSprite, crab.TrapSprite,
                "the wooden bow-top and the wire pot are different sprites — the kinds must read apart");
            Assert.AreNotEqual(lobster.TrapSpriteWet, crab.TrapSpriteWet,
                "wet states too — a hauled crab pot must not read as a lobster pot");
        }

        [Test]
        public void EveryTrapDef_HasAPlayableSplashBurst()
        {
            foreach (TrapDef trap in LoadAll<TrapDef>())
            {
                Assert.IsNotNull(trap.SplashBurstFrames, $"{trap.Id}: SplashBurstFrames array exists");
                Assert.IsTrue(trap.SplashBurstFrames.Length > 0,
                    $"{trap.Id}: the haul-break burst is authored (the kit's SplashBurst sheet)");
                for (int i = 0; i < trap.SplashBurstFrames.Length; i++)
                    Assert.IsNotNull(trap.SplashBurstFrames[i],
                        $"{trap.Id}: splash frame {i} must resolve — a re-slice broke a sub-sprite ref");
                Assert.GreaterOrEqual(trap.SplashBurstFps, 1f,
                    $"{trap.Id}: SplashBurstFps must be playable (the artist's brief: ~14-18)");
            }
        }

        // ---- DeckWorkDef species rules: the animated deck animals --------------------------------

        [Test]
        public void EveryDeckWorkSpeciesRule_HasItsAnimatedAnimal()
        {
            foreach (DeckWorkDef def in LoadAll<DeckWorkDef>())
            {
                Assert.IsNotNull(def.SpeciesRules, $"{def.Id}: SpeciesRules exist");
                foreach (SpeciesDeckRule rule in def.SpeciesRules)
                {
                    string who = $"{def.Id}/{rule.SpeciesId}";
                    Assert.IsNotNull(rule.AnimalSprite, $"{who}: AnimalSprite (the still) must resolve");
                    Assert.IsNotNull(rule.CrawlFrames, $"{who}: CrawlFrames array exists");
                    Assert.IsTrue(rule.CrawlFrames.Length > 0,
                        $"{who}: the crawl loop is authored (the deck sheet's frames 0-5)");
                    for (int i = 0; i < rule.CrawlFrames.Length; i++)
                        Assert.IsNotNull(rule.CrawlFrames[i],
                            $"{who}: crawl frame {i} must resolve — a re-slice broke a sub-sprite ref");
                    Assert.Greater(rule.CrawlFps, 0f, $"{who}: CrawlFps must animate");
                    Assert.IsNotNull(rule.RearSprite, $"{who}: RearSprite (the picked/held pose) must resolve");
                    Assert.IsNotNull(rule.DefendSprite, $"{who}: DefendSprite (the nip pose) must resolve");
                }
            }
        }

        // ---- Catch icons: the fish Defs + the Core icon table ------------------------------------

        [Test]
        public void TrapCatchSpecies_CarryTheirCatchSprites()
        {
            var species = LoadAll<FishSpeciesDef>();
            FishSpeciesDef lobster = species.Find(s => s.Id == "fish.lobster");
            FishSpeciesDef crab = species.Find(s => s.Id == "fish.rock_crab");
            Assert.IsNotNull(lobster, "fish.lobster exists");
            Assert.IsNotNull(crab, "fish.rock_crab exists");
            Assert.IsNotNull(lobster.Sprite, "fish.lobster: the Def's catch sprite must resolve");
            Assert.IsNotNull(crab.Sprite,
                "fish.rock_crab: the Def's catch sprite must resolve — this slot was empty project-wide " +
                "until the trap kit landed; it must never go empty again");
        }

        [Test]
        public void IconLibrary_MapsTheTrapArcIds_ToRealIcons()
        {
            var lib = AssetDatabase.LoadAssetAtPath<IconLibrary>(IconLibraryPath);
            Assert.IsNotNull(lib, $"IconLibrary at {IconLibraryPath}");

            // The ids the trap arc renders by string: the two catch species (sell screen, HUD catch
            // card) and the player buoy (the Boats presenter's authored key — the code-side const and
            // the data-side entry must agree, which is exactly what this pins).
            string[] required = { "fish.lobster", "fish.rock_crab", TrapBuoyPresenter.PlayerBuoyIconId };
            foreach (string id in required)
            {
                bool found = false;
                foreach (IconLibrary.Entry e in lib.Entries)
                {
                    if (!string.Equals(e.Id, id, System.StringComparison.OrdinalIgnoreCase)) continue;
                    Assert.IsNotNull(e.Icon, $"IconLibrary['{id}']: icon must resolve (broken sprite ref?)");
                    found = true;
                }
                Assert.IsTrue(found, $"IconLibrary must carry an entry for '{id}'");
            }
        }
    }
}
