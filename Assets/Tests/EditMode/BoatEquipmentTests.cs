using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The per-boat helm equipment reader (<see cref="BoatEquipment.EffectiveFit"/>): a hull's default
    /// console fit with the player's owned upgrades layered on, applied ONLY where the console supports the
    /// slot. Verifies the dory (no console) resolves to nothing, the default fit reads through, a bought
    /// fish-finder / radar / gps / compass upgrades the fit, an unsupported slot ignores the owned id, and
    /// flush beats dome. Pure over an owned-id set (the future save deviations); the HelmConsoleDef is a
    /// throwaway ScriptableObject built in the test.
    /// </summary>
    public class BoatEquipmentTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private HelmConsoleDef Console(ConsoleRigKind rig, bool fishSlot, bool dome, bool flush, bool radar,
                                      bool gps, SounderKind defaultSounder = SounderKind.Depth)
        {
            var c = ScriptableObject.CreateInstance<HelmConsoleDef>();
            c.Rig = rig;
            c.DefaultSounder = defaultSounder;
            c.DefaultCompass = CompassMount.None;
            c.DefaultRadar = false; c.DefaultGps = false;
            c.SupportsFishFinder = fishSlot;
            c.SupportsDomeCompass = dome;
            c.SupportsFlushCompass = flush;
            c.SupportsRadar = radar;
            c.SupportsGps = gps;
            _spawned.Add(c);
            return c;
        }

        private static HashSet<string> Owned(params string[] ids) => new HashSet<string>(ids);

        [Test]
        public void NullConsole_IsNone_TheDory()
        {
            var fit = BoatEquipment.EffectiveFit((HelmConsoleDef)null, Owned());
            Assert.AreEqual(HelmFit.None.Rig, fit.Rig);
            Assert.IsFalse(fit.HasConsole);
            Assert.AreEqual(SounderKind.None, fit.Sounder);
        }

        [Test]
        public void DefaultFit_ReadsThrough_WhenNothingOwned()
        {
            var console = Console(ConsoleRigKind.Console, fishSlot: true, dome: true, flush: false, radar: false, gps: false);
            var fit = BoatEquipment.EffectiveFit(console, Owned());
            Assert.IsTrue(fit.HasConsole);
            Assert.AreEqual(ConsoleRigKind.Console, fit.Rig);
            Assert.AreEqual(SounderKind.Depth, fit.Sounder, "ships with the basic depth sounder");
            Assert.AreEqual(CompassMount.None, fit.Compass);
            Assert.IsFalse(fit.Radar);
            Assert.IsFalse(fit.Gps);
        }

        [Test]
        public void FishFinder_Upgrades_TheSounder_OnlyWhenSupported()
        {
            var canFit = Console(ConsoleRigKind.Console, fishSlot: true, dome: true, flush: false, radar: false, gps: false);
            Assert.AreEqual(SounderKind.Fish,
                BoatEquipment.EffectiveFit(canFit, Owned(BoatEquipment.FishFinderId)).Sounder,
                "owning the fish-finder swaps the depth read for sonar");

            var cannotFit = Console(ConsoleRigKind.Console, fishSlot: false, dome: true, flush: false, radar: false, gps: false);
            Assert.AreEqual(SounderKind.Depth,
                BoatEquipment.EffectiveFit(cannotFit, Owned(BoatEquipment.FishFinderId)).Sounder,
                "an unsupported slot ignores the owned upgrade");
        }

        // ---- the purchasable DEPTH SOUNDER (ADR 0025 S2) -------------------------------------------

        [Test]
        public void DepthSounder_LightsABareBrow_WhenBought()
        {
            var bare = Console(ConsoleRigKind.Console, fishSlot: true, dome: true, flush: false,
                               radar: false, gps: false, defaultSounder: SounderKind.None);

            Assert.AreEqual(SounderKind.None, BoatEquipment.EffectiveFit(bare, Owned()).Sounder,
                "a helm that ships without one shows a blank brow until the player buys it");
            Assert.AreEqual(SounderKind.Depth,
                BoatEquipment.EffectiveFit(bare, Owned(BoatEquipment.DepthSounderId)).Sounder,
                "…and buying the sounder fits it");
        }

        [Test]
        public void DepthSounder_OnAHullThatAlreadyHasOne_ChangesNothing()
        {
            var shipsWithOne = Console(ConsoleRigKind.Novi, fishSlot: true, dome: true, flush: true,
                                       radar: true, gps: true);
            Assert.AreEqual(SounderKind.Depth,
                BoatEquipment.EffectiveFit(shipsWithOne, Owned(BoatEquipment.DepthSounderId)).Sounder,
                "a working boat already carries one — the purchase is a no-op, not a downgrade");
        }

        [Test]
        public void FishFinder_Wins_OverAnOwnedDepthSounder()
        {
            // The two share ONE flush cutout (HelmInstruments' own doc). The finder does everything the
            // sounder does, so owning both must show the finder — never the cheaper unit.
            var bare = Console(ConsoleRigKind.Console, fishSlot: true, dome: true, flush: false,
                               radar: false, gps: false, defaultSounder: SounderKind.None);
            Assert.AreEqual(SounderKind.Fish,
                BoatEquipment.EffectiveFit(bare,
                    Owned(BoatEquipment.DepthSounderId, BoatEquipment.FishFinderId)).Sounder,
                "the higher-tier unit owns the cutout when both are owned");

            // …and on a brow that cannot take the finder, the basic sounder still fits.
            var noFishSlot = Console(ConsoleRigKind.Console, fishSlot: false, dome: true, flush: false,
                                     radar: false, gps: false, defaultSounder: SounderKind.None);
            Assert.AreEqual(SounderKind.Depth,
                BoatEquipment.EffectiveFit(noFishSlot,
                    Owned(BoatEquipment.DepthSounderId, BoatEquipment.FishFinderId)).Sounder,
                "an unsupported finder falls back to the sounder that does fit");
        }

        [Test]
        public void DepthSounder_DoesNothingWithoutAConsole_TheDory()
        {
            Assert.AreEqual(SounderKind.None,
                BoatEquipment.EffectiveFit((HelmConsoleDef)null, Owned(BoatEquipment.DepthSounderId)).Sounder,
                "there is no dash to bolt it into on a rowed dory");
        }

        [Test]
        public void TheShippedOffer_SellsExactlyTheIdTheFitResolverLooksFor()
        {
            // The one seam a rename would break silently: the chandlery would take the money, write an id
            // nothing reads, and the dash would stay blank. Assert the two ends against the SHIPPED asset.
            const string path = "Assets/_Project/Data/Instruments/DepthSounderOffer.asset";
            var offer = UnityEditor.AssetDatabase.LoadAssetAtPath<HiddenHarbours.Economy.InstrumentOffer>(path);
            Assert.IsNotNull(offer, $"the depth-sounder offer must ship at {path}");
            Assert.AreEqual(BoatEquipment.DepthSounderId, offer.Id,
                "the offer sells the id EffectiveFit resolves — a rename on either side is a dead purchase");
            Assert.GreaterOrEqual(offer.Price, 0, "a price is data, but never negative");
            Assert.IsNotEmpty(offer.DisplayName);
        }

        [Test]
        public void TheShippedFinderOffer_SellsExactlyTheIdTheFitResolverLooksFor()
        {
            // The same seam as the sounder's, for the ADR 0025 S3 upgrade. ⚠ Price is a PROPOSAL pending
            // the owner's veto (Ruling F), so this asserts it is a sane positive number ABOVE the basic
            // unit's — not a specific figure the owner would then have to fight a test to change.
            const string path = "Assets/_Project/Data/Instruments/FishFinderOffer.asset";
            var offer = UnityEditor.AssetDatabase.LoadAssetAtPath<HiddenHarbours.Economy.InstrumentOffer>(path);
            Assert.IsNotNull(offer, $"the fish-finder offer must ship at {path}");
            Assert.AreEqual(BoatEquipment.FishFinderId, offer.Id,
                "the offer sells the id EffectiveFit resolves — a rename on either side is a dead purchase");
            Assert.IsNotEmpty(offer.DisplayName);

            const string basicPath = "Assets/_Project/Data/Instruments/DepthSounderOffer.asset";
            var basic = UnityEditor.AssetDatabase.LoadAssetAtPath<HiddenHarbours.Economy.InstrumentOffer>(basicPath);
            Assert.Greater(offer.Price, basic.Price,
                "the finder supersedes the sounder in the same cutout — it cannot be the cheaper buy");
        }

        [Test]
        public void TheInstrumentIds_AreDistinct_AndNamespaced()
        {
            // Append-only ids (rule 2): a collision or a rename would silently re-point saved fitments.
            var ids = new HashSet<string>
            {
                BoatEquipment.DepthSounderId, BoatEquipment.FishFinderId, BoatEquipment.RadarId,
                BoatEquipment.GpsId, BoatEquipment.CompassDomeId, BoatEquipment.CompassFlushId,
            };
            Assert.AreEqual(6, ids.Count, "every instrument id is distinct");
            foreach (string id in ids)
                StringAssert.StartsWith("instrument.", id, "ids are type.snake_case (rule 2)");
            Assert.AreEqual("instrument.depth_sounder", BoatEquipment.DepthSounderId,
                "the shipped id is a contract — saved fitments name it");
        }

        [Test]
        public void Compass_Flush_Beats_Dome_AndSupportGates()
        {
            var novi = Console(ConsoleRigKind.Novi, fishSlot: true, dome: true, flush: true, radar: true, gps: true);
            Assert.AreEqual(CompassMount.Flush,
                BoatEquipment.EffectiveFit(novi, Owned(BoatEquipment.CompassDomeId, BoatEquipment.CompassFlushId)).Compass,
                "flush (higher tier) wins when both are owned");

            var domeOnly = Console(ConsoleRigKind.Console, fishSlot: true, dome: true, flush: false, radar: false, gps: false);
            Assert.AreEqual(CompassMount.Dome,
                BoatEquipment.EffectiveFit(domeOnly, Owned(BoatEquipment.CompassFlushId, BoatEquipment.CompassDomeId)).Compass,
                "a flush-incapable helm falls back to the dome it can take");
        }

        [Test]
        public void RadarGps_FitWhenOwnedAndSupported()
        {
            var novi = Console(ConsoleRigKind.Novi, fishSlot: true, dome: true, flush: true, radar: true, gps: true);
            var fit = BoatEquipment.EffectiveFit(novi, Owned(BoatEquipment.RadarId, BoatEquipment.GpsId));
            Assert.IsTrue(fit.Radar);
            Assert.IsTrue(fit.Gps);

            var console = Console(ConsoleRigKind.Console, fishSlot: true, dome: true, flush: false, radar: false, gps: false);
            var fit2 = BoatEquipment.EffectiveFit(console, Owned(BoatEquipment.RadarId, BoatEquipment.GpsId));
            Assert.IsFalse(fit2.Radar, "console skiff has no radar brow slot");
            Assert.IsFalse(fit2.Gps);
        }
    }
}
