using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The shipped blue mussel, American oyster and sea scallop, read from their own assets.
    ///
    /// <para>The rule these pin is the clam's: a shellfish is gathered by its own method and can never
    /// come up on a line or in a pot. The mussel and the oyster are hand-gathered at St Peters on the
    /// <see cref="Gear.ClamFork"/> tag; the scallop is <see cref="Gear.Dredge"/> only and waits for the
    /// trawl arc, so it lists no St Peters region.</para>
    ///
    /// <para>Every expectation is read from the Defs or from the enums; no balance number is repeated
    /// here, so a retune of a value or a tide window does not need this file edited.</para>
    /// </summary>
    public class ShellfishSpeciesContentTests
    {
        private const string FishDir = "Assets/_Project/Data/Fish/";
        private const string DataRoot = "Assets/_Project/Data";
        private const string StPeters = "region.st_peters";

        private const string MusselId = "fish.blue_mussel";
        private const string OysterId = "fish.american_oyster";
        private const string ScallopId = "fish.sea_scallop";

        /// <summary>Every way a fish comes aboard that is not a hand on the shore or a dredge: the rod
        /// (handline, jig), the longline, the net and the pot.</summary>
        private static readonly Gear[] LineNetAndPot =
        {
            Gear.Handline, Gear.Jig, Gear.Longline, Gear.Net, Gear.Trap,
        };

        // A sweep wide enough to cover any tide a region can author and every hour of the day.
        private const float SweepLowestTide = -6f;
        private const float SweepHighestTide = 6f;
        private const float SweepTideStep = 0.25f;
        private const float SweepHourStep = 0.5f;

        private static FishSpeciesDef Load(string assetName)
        {
            var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(FishDir + assetName + ".asset");
            Assert.IsNotNull(def, assetName + " must exist in Data/Fish");
            return def;
        }

        private static FishSpeciesDef[] NewShellfish() =>
            new[] { Load("BlueMussel"), Load("AmericanOyster"), Load("SeaScallop") };

        private static List<T> LoadAll<T>() where T : UnityEngine.Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { DataRoot }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        private static bool MatchesAnywhere(FishSpeciesDef fish, string regionId, Gear gear)
        {
            foreach (Season season in Enum.GetValues(typeof(Season)))
                for (float hour = 0f; hour < 24f; hour += SweepHourStep)
                    for (float tide = SweepLowestTide; tide <= SweepHighestTide; tide += SweepTideStep)
                    {
                        var ctx = new CatchContext(regionId, tide, hour, season, gear);
                        if (CatchResolver.Matches(fish, in ctx)) return true;
                    }
            return false;
        }

        [Test]
        public void TheThreeShellfish_CarryTheirStableIds_AndAreShellfish()
        {
            Assert.AreEqual(MusselId, Load("BlueMussel").Id);
            Assert.AreEqual(OysterId, Load("AmericanOyster").Id);
            Assert.AreEqual(ScallopId, Load("SeaScallop").Id);
            foreach (FishSpeciesDef fish in NewShellfish())
                Assert.AreEqual(FishCategory.Shellfish, fish.Category, fish.Id);
        }

        [Test]
        public void MusselAndOyster_AreHandGatheredOnly_LikeTheClam()
        {
            Gear clamGear = Load("SoftShellClam").AllowedGear;
            Assert.AreEqual(Gear.ClamFork, clamGear, "the clam is the pattern: hand-gather only");
            Assert.AreEqual(clamGear, Load("BlueMussel").AllowedGear);
            Assert.AreEqual(clamGear, Load("AmericanOyster").AllowedGear);
        }

        [Test]
        public void TheScallop_IsDredgedOnly_AndIsNotAStPetersSpecies()
        {
            FishSpeciesDef scallop = Load("SeaScallop");
            Assert.AreEqual(Gear.Dredge, scallop.AllowedGear);
            Assert.IsFalse(scallop.RegionAllowed(StPeters), "the scallop rides the trawl arc, not the village");
        }

        [Test]
        public void MusselAndOyster_AreStPetersSpecies_AndAHandCanTakeThemAtSomeLowWater()
        {
            foreach (FishSpeciesDef fish in new[] { Load("BlueMussel"), Load("AmericanOyster") })
            {
                Assert.IsTrue(fish.RegionAllowed(StPeters), fish.Id);
                Assert.IsTrue(MatchesAnywhere(fish, StPeters, Gear.ClamFork),
                    fish.Id + ": no tide, hour or season lets a hand take it at St Peters");
            }
        }

        [Test]
        public void TheOysterBares_OnlyOnALowerTideThanTheMussel()
        {
            Assert.Less(Load("AmericanOyster").MaxTide, Load("BlueMussel").MaxTide,
                "the oyster reef is painted lower than the mussel bed, so it needs the bigger low tide");
        }

        [Test]
        public void NoneOfTheThree_CanComeOutOfTheRodsCatchPool_OrOffALineOrANet()
        {
            var regions = LoadAll<RegionDef>();
            Assert.IsNotEmpty(regions, "the sweep needs the authored regions");
            FishSpeciesDef[] shellfish = NewShellfish();

            foreach (FishSpeciesDef fish in shellfish)
                foreach (Gear gear in LineNetAndPot)
                {
                    Assert.IsFalse(fish.GearAllowed(gear), $"{fish.Id} must not be catchable by {gear}");
                    foreach (RegionDef region in regions)
                        Assert.IsFalse(MatchesAnywhere(fish, region.Id, gear),
                            $"{fish.Id} matched {gear} in {region.Id}");
                }

            // The roll itself, with a pool of nothing but these three: the rod comes up empty.
            var rng = new System.Random(20261113);
            foreach (RegionDef region in regions)
                foreach (Gear gear in LineNetAndPot)
                    foreach (Season season in Enum.GetValues(typeof(Season)))
                        for (float hour = 0f; hour < 24f; hour += 1f)
                        {
                            var ctx = new CatchContext(region.Id, SweepLowestTide, hour, season, gear);
                            Assert.IsNull(CatchResolver.Resolve(shellfish, in ctx, rng),
                                $"{gear} in {region.Id} landed a shellfish");
                        }
        }

        [Test]
        public void NoneOfTheThree_CanComeOutOfATrap()
        {
            var traps = LoadAll<TrapDef>();
            Assert.IsNotEmpty(traps, "the check needs the authored traps");
            foreach (TrapDef trap in traps)
                foreach (FishSpeciesDef fish in NewShellfish())
                    CollectionAssert.DoesNotContain(trap.AllowedCatchFishIds, fish.Id,
                        $"{AssetDatabase.GetAssetPath(trap)} lists {fish.Id}");
        }
    }
}
