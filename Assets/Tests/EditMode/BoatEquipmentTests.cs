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

        private HelmConsoleDef Console(ConsoleRigKind rig, bool fishSlot, bool dome, bool flush, bool radar, bool gps)
        {
            var c = ScriptableObject.CreateInstance<HelmConsoleDef>();
            c.Rig = rig;
            c.DefaultSounder = SounderKind.Depth;
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
