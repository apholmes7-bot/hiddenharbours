using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ADR 0059 step 2: the free effects that were parked at fixed orders just above the decor floor (spray at
    /// 5, splashes at 4 and 6, buoys and emergent shore plants at 3, the NPC catch at 30, the rod line and
    /// splash at 50 and 51) are re-anchored before hulls move into the band, so nothing vanishes under a hull
    /// in step 3. These pin the table in <see cref="EffectSorting"/>, that every effect resolves to the band's
    /// own Y-sort at its anchor (or its carrier's order) plus its rungs, that each call site asks the table,
    /// and the ADR's guard: no new sorting-order literal between <see cref="SortingBands.DecorFloor"/> and
    /// <c>DecorFloor + 100</c>.
    /// </summary>
    public sealed class EffectSortingTests
    {
        /// <summary>The order a rock (a static <c>YSortSprite</c>) standing at this Y sorts at.</summary>
        static int RockAt(float y) => YSortSprite.OrderFor(y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                                                           SortingBands.DecorFloor, SortingBands.DecorCeiling);

        static readonly float[] Ys = { -250f, -55.88f, -0.13f, 0f, 0.125f, 3.3f, 120f, 250f };

        static IEnumerable<SortedEffect> AllEffects() =>
            System.Enum.GetValues(typeof(SortedEffect)).Cast<SortedEffect>();

        // ---- the table ------------------------------------------------------------------------------

        [Test] public void TheTable_IsPinned()
        {
            Assert.AreEqual(0, EffectSorting.SprayRungs, "spray sorts at its own spawn point");
            Assert.AreEqual(-1, EffectSorting.FootSplashRungs, "a splash yields one rung to the feet that threw it");
            Assert.AreEqual(1, EffectSorting.TrapSplashRungs, "a trap splash breaks round its buoy");
            Assert.AreEqual(0, EffectSorting.BuoyRungs, "a buoy sorts where it floats");
            Assert.AreEqual(0, EffectSorting.EmergentShorePlantRungs, "a plant sorts as a YSortSprite would");
            Assert.AreEqual(0, EffectSorting.NpcCatchRungs, "the NPC catch sorts at the boat's pivot");
            Assert.AreEqual(1, EffectSorting.RodOverFisherRungs, "the rod splash, one rung over the fisher");
        }

        [Test] public void EveryEffect_ReadsItsRungsFromTheTable()
        {
            var expected = new Dictionary<SortedEffect, int>
            {
                { SortedEffect.WindSpray, EffectSorting.SprayRungs },
                { SortedEffect.SurfSpray, EffectSorting.SprayRungs },
                { SortedEffect.WadeSplash, EffectSorting.FootSplashRungs },
                { SortedEffect.MomentBurst, EffectSorting.FootSplashRungs },
                { SortedEffect.TrapSplash, EffectSorting.TrapSplashRungs },
                { SortedEffect.Buoy, EffectSorting.BuoyRungs },
                { SortedEffect.EmergentShorePlant, EffectSorting.EmergentShorePlantRungs },
                { SortedEffect.NpcCatch, EffectSorting.NpcCatchRungs },
                { SortedEffect.RodLine, EffectSorting.RodLineRungs },
                { SortedEffect.RodSplash, EffectSorting.RodOverFisherRungs },
                { SortedEffect.NpcGearAtRail, EffectSorting.NpcGearOverHullRungs },
            };
            CollectionAssert.AreEquivalent(AllEffects().ToArray(), expected.Keys.ToArray(),
                "a new SortedEffect needs its row here and in EffectSorting.RungsFor");
            foreach (var kv in expected)
                Assert.AreEqual(kv.Value, EffectSorting.RungsFor(kv.Key), kv.Key.ToString());
        }

        [Test] public void OnlyTheRodAndTheNpcRailGear_RideTheirCarrier()
        {
            foreach (SortedEffect e in AllEffects())
                Assert.AreEqual(e == SortedEffect.RodLine || e == SortedEffect.RodSplash || e == SortedEffect.NpcGearAtRail,
                                EffectSorting.IsCarried(e), e.ToString());
        }

        // ---- the rule -------------------------------------------------------------------------------

        [Test] public void TheBandOrder_IsExactlyTheYSortSpritesRule()
        {
            foreach (float y in Ys) Assert.AreEqual(RockAt(y), EffectSorting.BandOrderAt(y), $"y = {y}");
            Assert.AreEqual(SortingBands.DecorFloor, EffectSorting.BandOrderAt(10_000f), "clamped at the floor");
            Assert.AreEqual(SortingBands.DecorCeiling, EffectSorting.BandOrderAt(-10_000f), "clamped at the ceiling");
            Assert.AreEqual(SortingBands.DecorBase, EffectSorting.BandOrderAt(float.NaN), "unplaced: the middle");
        }

        [Test] public void EveryAnchoredEffect_SortsAtTheBandsYSortAtItsAnchor_PlusItsRungs()
        {
            foreach (SortedEffect e in AllEffects())
            {
                if (EffectSorting.IsCarried(e)) continue;
                foreach (float y in Ys)
                    Assert.AreEqual(RockAt(y) + EffectSorting.RungsFor(e), EffectSorting.OrderAt(e, y), $"{e} at y = {y}");
            }
        }

        [Test] public void AnAnchoredEffect_NeverLeavesTheBand()
        {
            foreach (SortedEffect e in AllEffects())
            {
                if (EffectSorting.IsCarried(e)) continue;
                foreach (float y in new[] { 10_000f, SortingBands.DecorHalfExtentMetres, -SortingBands.DecorHalfExtentMetres, -10_000f })
                {
                    int order = EffectSorting.OrderAt(e, y);
                    Assert.GreaterOrEqual(order, SortingBands.DecorFloor, $"{e} at y = {y}");
                    Assert.LessOrEqual(order, SortingBands.DecorCeiling, $"{e} at y = {y}");
                }
            }
        }

        [Test] public void ACarriedEffect_SortsAtItsCarriersOrder_PlusItsRungs()
        {
            foreach (int carrier in new[] { SortingBands.DecorFloor, 1003, SortingBands.DecorBase, SortingBands.DecorCeiling })
            {
                Assert.AreEqual(carrier - 2, EffectSorting.OrderOver(SortedEffect.RodLine, carrier));
                Assert.AreEqual(carrier + 1, EffectSorting.OrderOver(SortedEffect.RodSplash, carrier));
            }
        }

        [Test] public void InterleavesWithDecorByPosition_TheVisibleChange()
        {
            // A buoy off the shoal: a rock north of it draws behind it, one south of it in front. When both sat
            // at fixed orders (the buoy at 3), every rock drew over every buoy.
            const float buoyY = -55.88f;
            int buoy = EffectSorting.OrderAt(SortedEffect.Buoy, buoyY);
            Assert.Less(RockAt(buoyY + 1f), buoy, "a rock a metre north draws behind the buoy");
            Assert.Greater(RockAt(buoyY - 1f), buoy, "a rock a metre south draws in front of it");

            // A wade splash at the player's feet sits under him, and over a tuft a metre behind him.
            const float feetY = 12.4f;
            int splash = EffectSorting.OrderAt(SortedEffect.WadeSplash, feetY);
            Assert.Less(splash, RockAt(feetY), "the wader standing on the splash draws over it");
            Assert.Greater(splash, RockAt(feetY + 1f), "a tuft a metre north is behind the splash");

            // A trap splash breaks round the buoy it bursts from.
            Assert.Greater(EffectSorting.OrderAt(SortedEffect.TrapSplash, buoyY), buoy);
        }

        [Test] public void EveryReAnchoredEffect_ClearsAHullStillAtOrderOne()
        {
            // Step 2 runs while every hull still sorts at BoatVisualDef's 1: nothing re-anchored may fall to or
            // under her, or it would vanish now instead of in step 3.
            const int hullToday = 1;
            foreach (SortedEffect e in AllEffects())
            {
                if (EffectSorting.IsCarried(e)) continue;
                foreach (float y in new[] { 10_000f, 0f, -10_000f })
                    Assert.Greater(EffectSorting.OrderAt(e, y), hullToday, $"{e} at y = {y}");
            }
        }

        // ---- the effects ask the table --------------------------------------------------------------

        [Test] public void TheMomentBurst_SortsEachMoteWhereTheCueFell_OneRungBehind()
        {
            var go = new GameObject("bursts");
            try
            {
                var em = go.AddComponent<MomentBurstEmitter>();
                em.OverrideSettings(JuiceSettings.Default);
                var at = new Vector2(3f, -41.6f);
                em.OnMoment(new JuiceMomentCue(JuiceMoment.DigStrike, at, 0.1f));

                Assert.IsNull(go.GetComponent<SortingGroup>(), "no host group: one order for every mote is the parked order");
                int expected = EffectSorting.OrderAt(SortedEffect.MomentBurst, at.y);
                Assert.AreEqual(RockAt(at.y) - 1, expected);
                int sorted = go.GetComponentsInChildren<SortingGroup>(true).Count(g => g.sortingOrder == expected);
                Assert.AreEqual(JuiceSettings.Default.SandChunkCount, sorted, "every chunk thrown, sorted where it fell");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>Each re-anchored call site, and the table row it must ask.</summary>
        static readonly (string Path, string Call)[] CallSites =
        {
            ("Assets/_Project/Code/Art/SprayEmitter.cs", "EffectSorting.OrderAt(SortedEffect.WindSpray,pos.y)"),
            ("Assets/_Project/Code/Art/SurfSprayEmitter.cs", "EffectSorting.OrderAt(SortedEffect.SurfSpray,pos.y)"),
            ("Assets/_Project/Code/Art/WadeSplashEmitter.cs", "EffectSorting.OrderAt(SortedEffect.WadeSplash,pos.y)"),
            ("Assets/_Project/Code/Art/MomentBurstEmitter.cs", "EffectSorting.OrderAt(SortedEffect.MomentBurst,pos.y)"),
            ("Assets/_Project/Code/Fishing/TrapSplashFx.cs", "EffectSorting.OrderAt(SortedEffect.TrapSplash,at.y)"),
            ("Assets/_Project/Code/Boats/TrapBuoyPresenter.cs", "EffectSorting.OrderAt(SortedEffect.Buoy,e.PosY)"),
            ("Assets/_Project/Code/Boats/AmbientFleetPresenter.cs", "EffectSorting.OrderAt(SortedEffect.Buoy,fisher.Spots[j].y)"),
            ("Assets/_Project/Code/App/NpcPotBoats.cs", "EffectSorting.OrderAt(SortedEffect.Buoy,at.y)"),
            ("Assets/_Project/Code/Art/ShorePlantTideView.cs", "EffectSorting.OrderAt(SortedEffect.EmergentShorePlant,transform.position.y)"),
            ("Assets/_Project/Code/App/NpcPotDeck.cs", "EffectSorting.OrderAt(SortedEffect.NpcCatch,hull.transform.position.y)"),
            ("Assets/_Project/Code/Player/RodFightPresenter.cs", "EffectSorting.OrderOver(SortedEffect.RodLine,_playerSr.sortingOrder)"),
            ("Assets/_Project/Code/Player/RodFightPresenter.cs", "EffectSorting.OrderOver(SortedEffect.RodSplash,_playerSr.sortingOrder)"),
        };

        [Test] public void EachReAnchoredEffect_AsksTheTable()
        {
            foreach (var (path, call) in CallSites)
                StringAssert.Contains(call, Squash(File.ReadAllText(path)), path);
        }

        [Test] public void NoPooledEffect_KeepsOneGroupForTheWholePool()
        {
            // A single SortingGroup on the host sorts every particle at one order: the parked order this step
            // retires. Each pooled particle carries its own group instead (the #134 "sort as 2D" quirk kept).
            foreach (string path in new[]
                     {
                         "Assets/_Project/Code/Art/SprayEmitter.cs", "Assets/_Project/Code/Art/SurfSprayEmitter.cs",
                         "Assets/_Project/Code/Art/WadeSplashEmitter.cs", "Assets/_Project/Code/Art/MomentBurstEmitter.cs",
                         "Assets/_Project/Code/Fishing/TrapSplashFx.cs",
                     })
            {
                string code = Squash(File.ReadAllText(path));
                StringAssert.DoesNotContain("gameObject.AddComponent<SortingGroup>()", code, path);
                StringAssert.Contains("=go.AddComponent<SortingGroup>();", code, path);
            }
        }

        // ---- the low-floor guard (ADR 0059 decision 3) ----------------------------------------------

        /// <summary>
        /// A sorting-order literal: a name holding "sort…order" assigned a whole number. Matches a field
        /// default, a const and a renderer assignment alike.
        /// </summary>
        static readonly Regex Literal = new Regex(@"\b(\w*sort\w*order\w*)\s*=\s*(\d+)\s*[;,)]",
                                                  RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Every literal in the low floor that is still on main, by file, name and value. Each row is a known
        /// debt with its reason; the guard fails on anything not listed (a new parked order) and on any row no
        /// longer found (a debt paid: delete its row). Grouped by why it is still here.
        /// </summary>
        static readonly string[] KnownLowFloorLiterals =
        {
            // Rides with the boat: a local order inside the boat's SortingGroup since step 3 (BoatSortingGroupTests).
            "Assets/_Project/Code/Boats/BoatInteriorInstaller.cs|_sortingOrderAboveHull|2",
            "Assets/_Project/Code/Boats/DeckContainerPresenter.cs|ContainerSortingOrder|50",
            "Assets/_Project/Code/Fishing/PotDeckWorkController.cs|PotSortingOrder|51",
            "Assets/_Project/Code/Fishing/PotDeckWorkController.cs|AnimalSortingOrder|52",

            // Serialized fields kept so existing assets load unchanged; no longer read since step 2.
            "Assets/_Project/Code/Art/ShorePlantDef.cs|EmergentSortingOrder|3",
            "Assets/_Project/Art/Editor/ShorePlantDefBuilder.cs|EmergentSortingOrder|3",
            "Assets/_Project/Code/Boats/AmbientFleetDef.cs|BuoySortingOrder|3",
            "Assets/_Project/Code/Fishing/NpcPotDayDef.cs|CatchSortingOrder|30",
            "Assets/_Project/Code/Fishing/NpcPotDayDef.cs|BuoySortingOrder|3",

            // Open points of step 2, not named in its list: each needs a call (or a re-bake) before it moves.
            "Assets/_Project/Code/Art/DustMotes.cs|_sortingOrder|8",
            "Assets/_Project/Code/Fishing/NpcPotDayDef.cs|RopeSortingOrder|2",
            "Assets/_Project/Code/Player/RodFightPresenter.cs|_shadowSortingOrder|20",
            "Assets/_Project/Code/Player/RodFightPresenter.cs|_rippleSortingOrder|15",
            "Assets/_Project/Code/Tools/Editor/RigBaking/NavBuoyDefBuilder.cs|sortingOrder|3",

            // Editor-built decor: a pre-Play seed that a YSortSprite overwrites, or a catalog default for one.
            "Assets/_Project/Art/Editor/AcadianTreeCatalog.cs|SortingOrder|5",
            "Assets/_Project/Art/Editor/DecorPrefabBuilder.cs|TreeSortingOrder|5",
            "Assets/_Project/Art/Editor/DecorPrefabBuilder.cs|BuildingSortingOrder|4",
            "Assets/_Project/Art/Editor/DecorPrefabBuilder.cs|PropSortingOrder|3",
            "Assets/_Project/Art/Editor/DecorPrefabBuilder.cs|GrassSortingOrder|2",
            "Assets/_Project/Art/Editor/DecorPrefabBuilder.cs|FlowerSortingOrder|3",
            "Assets/_Project/Art/Editor/FlowerPaintTool.cs|_sortingOrder|3",
            "Assets/_Project/Art/Editor/GrassPaintTool.cs|_sortingOrder|2",
            "Assets/_Project/Art/Editor/InteriorCatalog.cs|PropSortingOrder|4",
            "Assets/_Project/Art/Editor/LightMenu.cs|sortingOrder|10",
            "Assets/_Project/Art/Editor/ShopCatalog.cs|SortingOrder|4",
            "Assets/_Project/Art/Editor/ShopFixtureCatalog.cs|SortingOrder|3",
            "Assets/_Project/Art/Editor/VillageBuildingCatalog.cs|SortingOrder|4",
            "Assets/_Project/Art/Editor/YardCatalog.cs|SortingOrder|3",
            "Assets/_Project/Code/App/Editor/NineMileCreekBuilder.cs|sortingOrder|10",
            "Assets/_Project/Code/App/Editor/NineMileCreekBuilder.cs|sortingOrder|2",
            "Assets/_Project/Code/App/Editor/NineMileCreekPeople.cs|sortingOrder|9",
            "Assets/_Project/Code/App/Editor/PersistentCoreBuilder.cs|sortingOrder|10",
            "Assets/_Project/Code/App/Editor/StPetersBuilder.cs|sortingOrder|3",
            "Assets/_Project/Code/App/Editor/StPetersBuilder.cs|sortingOrder|9",
            "Assets/_Project/Code/App/Editor/StPetersCamperLot.cs|sortingOrder|4",
            "Assets/_Project/Code/App/Editor/StPetersCannery.cs|sortingOrder|2",
            "Assets/_Project/Code/App/Editor/StPetersGinnyPlot.cs|sortingOrder|3",
            "Assets/_Project/Code/App/Editor/StPetersGinnyPlot.cs|sortingOrder|2",
            "Assets/_Project/Code/App/Editor/StPetersInhabitants.cs|sortingOrder|9",
            "Assets/_Project/Code/App/Editor/StPetersWoodsPlanter.cs|GrassSortingOrder|2",
            "Assets/_Project/Code/App/Editor/StPetersWoodsPlanter.cs|ShrubSortingOrder|4",
            "Assets/_Project/Code/App/Editor/StPetersWoodsPlanter.cs|FlowerSortingOrder|3",
            "Assets/_Project/Code/App/Editor/CarryProof.cs|sortingOrder|100",

            // A Canvas order: screen space, a separate ladder from the world's.
            "Assets/_Project/Code/Player/DevToast.cs|sortingOrder|96",
            "Assets/_Project/Code/UI/ChartplotterOverlayHost.cs|_sortingOrder|63",
            "Assets/_Project/Code/UI/FishFinderOverlayHost.cs|_sortingOrder|62",
            "Assets/_Project/Code/UI/HelmOverlayHost.cs|_sortingOrder|60",
            "Assets/_Project/Code/UI/HudController.cs|sortingOrder|100",
            "Assets/_Project/Code/UI/RadarOverlayHost.cs|_sortingOrder|64",
            "Assets/_Project/Code/UI/SounderOverlayHost.cs|_sortingOrder|62",
            "Assets/_Project/Code/World/QuestPanelPresenter.cs|SortingOrder|94",
        };

        [Test] public void NoNewSortingOrderLiteral_InTheBandsLowFloor()
        {
            int lo = SortingBands.DecorFloor, hi = SortingBands.DecorFloor + 100;
            var found = new List<string>();
            foreach (string file in Directory.GetFiles("Assets/_Project", "*.cs", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                if (path.EndsWith("/SortingBands.cs")) continue;
                foreach (string line in File.ReadLines(file))
                    foreach (Match m in Literal.Matches(line))
                    {
                        if (!int.TryParse(m.Groups[2].Value, out int n) || n < lo || n > hi) continue;
                        found.Add($"{path}|{m.Groups[1].Value}|{n}");
                    }
            }

            var known = KnownLowFloorLiterals.ToList();
            var unexpected = new List<string>();
            foreach (string f in found)
                if (!known.Remove(f)) unexpected.Add(f);

            Assert.IsEmpty(unexpected,
                "A sorting order parked in the decor band's low floor (ADR 0059 decision 3). Y-sort the effect at " +
                "its emission point or derive it from its carrier (Core EffectSorting), or ride the boat's group:\n" +
                string.Join("\n", unexpected));
            Assert.IsEmpty(known, "Debts paid: delete these rows from KnownLowFloorLiterals:\n" + string.Join("\n", known));
        }

        static string Squash(string s) => Regex.Replace(s, @"\s+", "");
    }
}
