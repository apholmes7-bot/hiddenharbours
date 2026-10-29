using System.Text.RegularExpressions;
using HiddenHarbours.Boats;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEditor;

namespace HiddenHarbours.Tests.EditMode
{
    public class LonglineDefTests
    {
        [Test]
        public void EveryLonglineDef_HasStableIdAndValidTuning()
        {
            string[] guids = AssetDatabase.FindAssets("t:LonglineDef", new[] { "Assets/_Project/Data/Longlines" });
            Assert.Greater(guids.Length, 0);
            foreach (string guid in guids)
            {
                LonglineDef def = AssetDatabase.LoadAssetAtPath<LonglineDef>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotNull(def);
                Assert.IsTrue(Regex.IsMatch(def.Id, @"^longline\.[a-z0-9]+(?:_[a-z0-9]+)*$"), def.Id);
                Assert.IsTrue(def.HasValidTuning, def.Id);
                Assert.Greater(def.HooksPerTub, 0);
                Assert.Greater(def.MaxTubsPerSet, 0);
                Assert.Greater(def.MaxSetDepthMeters, def.MinSetDepthMeters);
                Assert.Greater(def.HooksPerBaitUnit, 0);
                Assert.GreaterOrEqual(def.BaitFavourMultiplier, 1);
            }
        }

        [Test]
        public void BothWorkingHulls_HaveTheRequiredHaulerStation()
        {
            foreach (string hull in new[] { "CapeIslander", "LobsterBoat" })
            {
                var gear = AssetDatabase.LoadAssetAtPath<BoatDeckGearDef>(
                    "Assets/_Project/Data/Boats/DeckGear/" + hull + "DeckGear.asset");
                Assert.IsNotNull(gear, hull);
                Assert.IsTrue(gear.HasHaulerStation(), hull);
            }
        }
    }
}
