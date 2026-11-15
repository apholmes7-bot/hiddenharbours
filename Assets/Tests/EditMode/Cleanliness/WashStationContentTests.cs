using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Cleanliness slice 1, the shipped content: one wash station per kind, the three ceilings strictly
    /// ordered sea &lt; sink &lt; shower/bath (the owner's "the ocean will clean you but only to a base amount
    /// that does not match what a shower/sink can offer" and "sinks... not able to fully clean a player"),
    /// the toilet a seat with no function, the outhouse one toilet behind an opening door, and the stinky
    /// reaction's lines.
    /// </summary>
    public class WashStationContentTests
    {
        private static WashStationDef[] Stations() => Resources.LoadAll<WashStationDef>(CleanlinessService.ResourcesFolder);

        private static WashStationDef Only(WashStationKind kind)
        {
            WashStationDef[] all = Stations().Where(s => s.Kind == kind).ToArray();
            Assert.AreEqual(1, all.Length, $"exactly one shipped {kind} station");
            return all[0];
        }

        [Test]
        public void OneStationPerKind_WithUniqueWellFormedIds()
        {
            WashStationDef[] all = Stations();
            foreach (WashStationKind kind in System.Enum.GetValues(typeof(WashStationKind))) Only(kind);
            var ids = new HashSet<string>();
            foreach (WashStationDef s in all)
            {
                StringAssert.StartsWith("washstation.", s.Id);
                Assert.IsTrue(ids.Add(s.Id), "duplicate id " + s.Id);
            }
        }

        [Test]
        public void Ceilings_AreStrictlyOrdered_SeaBelowSinkBelowShower()
        {
            WashStationDef sea = Only(WashStationKind.Sea), sink = Only(WashStationKind.Sink);
            WashStationDef shower = Only(WashStationKind.Shower), bath = Only(WashStationKind.Bath);
            Assert.Less(sea.Ceiling, sink.Ceiling, "the sea only rinses to a base below the sink");
            Assert.Less(sink.Ceiling, shower.Ceiling, "a sink cannot fully clean");
            Assert.AreEqual(1f, shower.Ceiling, "a shower reaches fully clean");
            Assert.AreEqual(1f, bath.Ceiling, "so does a bath");
            Assert.Less(sink.CleanRatePerGameHour, shower.CleanRatePerGameHour, "a sink is the slower wash");
        }

        [Test]
        public void TheSea_PullsBothWays_AndIsTheConfiguredSeaStation()
        {
            WashStationDef sea = Only(WashStationKind.Sea);
            Assert.IsTrue(sea.PullsBothWays);
            Assert.AreEqual(CleanlinessSettings.Default.SeaStationId, sea.Id);
            foreach (WashStationDef s in Stations())
                if (s.Kind != WashStationKind.Sea) Assert.IsFalse(s.PullsBothWays, s.Id + " must only ever clean");
            // A rinsed player is not grubby, but not clean either.
            Assert.Less(1f - sea.Ceiling, CleanlinessSettings.Default.Curve.MildAt);
            Assert.Greater(1f - sea.Ceiling, 0f);
        }

        [Test]
        public void TheToilet_IsASeatWithNoFunction()
        {
            WashStationDef toilet = Only(WashStationKind.Toilet);
            Assert.IsTrue(toilet.SitOnly);
            Assert.IsFalse(toilet.Washes);
            Assert.AreEqual(0.4f, CleanlinessMath.AfterWash(0.4f, toilet, 100d));
        }

        [Test]
        public void TheOuthouse_HasOneToilet_BehindADoorThatOpens()
        {
            WashroomDef[] rooms = Resources.LoadAll<WashroomDef>("Washrooms");
            WashroomDef outhouse = rooms.Single(r => r.Id == "washroom.outhouse");
            Assert.AreEqual(WashroomKind.Outhouse, outhouse.Kind);
            Assert.IsTrue(outhouse.HasDoor && outhouse.DoorOpens);
            Assert.AreEqual(1, outhouse.Fixtures.Length);
            Assert.IsTrue(outhouse.Has(WashStationKind.Toilet));
        }

        [Test]
        public void TheStinkyReaction_HasLines_ForThePutOffTone()
        {
            NeedReactionDef stinky = Resources.LoadAll<NeedReactionDef>("NeedReactions").Single(r => r.Id == "reaction.stinky");
            Assert.AreEqual(NeedKind.Dirtiness, stinky.Need);
            Assert.AreEqual(NeedStage.Strong, stinky.AtStage);
            Assert.AreEqual(ReplyTone.PutOff, stinky.Tone);
            Assert.Greater(stinky.Lines.Length, 0);
            foreach (string line in stinky.Lines) Assert.IsFalse(string.IsNullOrWhiteSpace(line));
        }

        [Test]
        public void ShippedConfig_MatchesTheDefaults()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(config, "GameConfig.asset");
            CleanlinessSettings shipped = config.Cleanliness, d = CleanlinessSettings.Default;
            Assert.AreEqual(d.Curve.RisePerGameHour, shipped.Curve.RisePerGameHour, 1e-6f);
            Assert.AreEqual(d.Curve.StrongAt, shipped.Curve.StrongAt);
            Assert.AreEqual(d.Sources.Length, shipped.Sources.Length);
            for (int i = 0; i < d.Sources.Length; i++)
            {
                Assert.AreEqual(d.Sources[i].Kind, shipped.Sources[i].Kind);
                Assert.AreEqual(d.Sources[i].RisePerGameHour, shipped.Sources[i].RisePerGameHour, 1e-6f);
                Assert.AreEqual(d.Sources[i].PerAction, shipped.Sources[i].PerAction, 1e-6f);
            }
            Assert.AreEqual(d.SeaStationId, shipped.SeaStationId);
        }
    }
}
