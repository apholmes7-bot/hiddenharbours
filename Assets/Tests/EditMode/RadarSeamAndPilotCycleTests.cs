using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The radar's CORE seam and the pilot-deck dev cycle (ADR 0025 S5).
    ///
    /// <para>Three separate claims, each of which could quietly be false:
    /// the seam answers honestly with no producer registered (the null object); the preferences really
    /// persist, EVERY field of them (the trap that made the finder's RANGE pushers move the glass and
    /// never the save); and SHIFT+K states the pilot deck without ever touching the player's
    /// ownership.</para>
    ///
    /// <para>Pure functions and a fresh save — no scene, so this runs headless.</para>
    /// </summary>
    public class RadarSeamAndPilotCycleTests
    {
        private const string Hull = "boat.test_novi";

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        private static List<string> Owned(params string[] ids) => new List<string>(ids);

        private HelmConsoleDef Console(bool radar, bool gps, bool fishSlot = true,
                                       bool defaultRadar = false, bool defaultGps = false,
                                       ConsoleRigKind rig = ConsoleRigKind.Novi)
        {
            var c = ScriptableObject.CreateInstance<HelmConsoleDef>();
            c.Id = "helm.test";
            c.Rig = rig;
            c.DefaultSounder = SounderKind.Depth;
            c.DefaultCompass = CompassMount.None;
            c.DefaultRadar = defaultRadar;
            c.DefaultGps = defaultGps;
            c.SupportsFishFinder = fishSlot;
            c.SupportsDomeCompass = true;
            c.SupportsFlushCompass = false;
            c.SupportsRadar = radar;
            c.SupportsGps = gps;
            _spawned.Add(c);
            return c;
        }

        // ---- the null object ---------------------------------------------------------------------

        [Test]
        public void QuietSea_AnswersZero_AndClearsTheCallersList()
        {
            var into = new List<RadarContact> { RadarContact.Still(Vector2.zero, RadarEchoKind.Buoy, 1f) };
            Assert.AreEqual(0, EmptyRadarSea.Instance.ContactsAt(Vector2.zero, 500f, 0.0, into));
            Assert.IsEmpty(into, "the seam's contract is cleared-first, then refilled");
            Assert.DoesNotThrow(() => EmptyRadarSea.Instance.ContactsAt(Vector2.zero, 500f, 0.0, null),
                                "a null list is tolerated — the IFishSchools contract, unchanged");
        }

        [Test]
        public void GameServices_RadarContacts_IsNeverNull_AndFallsBackWhenAProducerGoesAway()
        {
            GameServices.Reset();
            Assert.IsNotNull(GameServices.RadarContacts,
                             "read without a null check — the FishSchools discipline");
            Assert.AreSame(EmptyRadarSea.Instance, GameServices.RadarContacts);

            GameServices.RadarContacts = null;
            Assert.AreSame(EmptyRadarSea.Instance, GameServices.RadarContacts,
                           "clearing the producer must return the quiet sea, not null");
            GameServices.Reset();
        }

        [Test]
        public void Contact_KindsMatchTheRigsOwnOrder()
        {
            // radarRig.js:130 KINDS = ['vessel','land','buoy','rain','flock'] — the enum is a cast, not a
            // lookup table, so a drift here would silently repaint every echo as the wrong shape.
            Assert.AreEqual(0, (int)RadarEchoKind.Vessel);
            Assert.AreEqual(1, (int)RadarEchoKind.Land);
            Assert.AreEqual(2, (int)RadarEchoKind.Buoy);
            Assert.AreEqual(3, (int)RadarEchoKind.Rain);
            Assert.AreEqual(4, (int)RadarEchoKind.Flock);
        }

        [Test]
        public void Contact_Still_HasNoCourse_SoNothingMoored_GrowsAWake()
        {
            RadarContact buoy = RadarContact.Still(new Vector2(10f, 20f), RadarEchoKind.Buoy, 0.7f);
            Assert.IsFalse(buoy.IsMakingWay);
            Assert.IsTrue(float.IsNaN(buoy.CourseDegrees));

            var underway = new RadarContact(new Vector2(0f, 0f), RadarEchoKind.Vessel, 1.05f, 200f, 4f);
            Assert.IsTrue(underway.IsMakingWay);

            var hoveTo = new RadarContact(new Vector2(0f, 0f), RadarEchoKind.Vessel, 1.05f, 200f, 0f);
            Assert.IsFalse(hoveTo.IsMakingWay, "a course with no way on is not making way");
        }

        // ---- preferences --------------------------------------------------------------------------

        [Test]
        public void Prefs_RoundTripThroughTheLocker_EveryFieldOfThem()
        {
            // ⚠ The trap this pins: an upsert that compares only SOME fields silently stops persisting
            // the others. Each field is moved on its own, from the same baseline.
            var cfg = RadarSettings.Default;
            RadarPrefs baseline = RadarPrefs.FromDefaults(in cfg);

            foreach (RadarPrefs moved in new[]
                     {
                         baseline.WithNight(true),
                         baseline.WithMode(RadarMode.NorthUp),
                         baseline.SteppedRange(+1, in cfg),
                     })
            {
                SaveData save = SaveMigration.NewGame();
                Assert.IsTrue(InstrumentLocker.SetRadarPrefs(save, Hull, in moved));
                RadarPrefs back = InstrumentLocker.RadarPrefsFor(save, Hull, in baseline);
                Assert.AreEqual(moved.Night, back.Night);
                Assert.AreEqual(moved.Mode, back.Mode);
                Assert.AreEqual(moved.RangeStep, back.RangeStep);

                Assert.IsFalse(InstrumentLocker.SetRadarPrefs(save, Hull, in moved),
                               "writing the same value twice must not dirty the save");
            }
        }

        [Test]
        public void Prefs_AbsentRow_IsTheOwnersDefaults_NotAZeroedStruct()
        {
            var cfg = RadarSettings.Default;
            SaveData save = SaveMigration.NewGame();
            RadarPrefs fresh = InstrumentLocker.RadarPrefsFor(save, "boat.never_touched",
                                                              RadarPrefs.FromDefaults(in cfg));
            Assert.AreEqual(cfg.SafeDefaultRangeStep, fresh.RangeStep);
            Assert.AreEqual(RadarMode.HeadUp, fresh.Mode,
                            "a freshly fitted set wakes TRANSMITTING — a standby one looks broken");
            Assert.IsTrue(fresh.Transmitting);
            Assert.IsFalse(fresh.Night);
        }

        [Test]
        public void Prefs_ModeRing_GoesStandbyHeadUpNorthUpAndBack()
        {
            var p = new RadarPrefs(false, RadarMode.Standby, 2);
            p = p.SteppedMode();
            Assert.AreEqual(RadarMode.HeadUp, p.Mode, "the first press on a standby set puts it on air");
            Assert.IsTrue(p.Transmitting);
            Assert.IsTrue(p.HeadUp);

            p = p.SteppedMode();
            Assert.AreEqual(RadarMode.NorthUp, p.Mode);
            Assert.IsFalse(p.HeadUp);

            p = p.SteppedMode();
            Assert.AreEqual(RadarMode.Standby, p.Mode);
            Assert.IsFalse(p.Transmitting);
        }

        [Test]
        public void Prefs_RangeIsClampedNeverWrapped()
        {
            var cfg = RadarSettings.Default;
            var p = new RadarPrefs(false, RadarMode.HeadUp, 0);
            Assert.AreEqual(0, p.SteppedRange(+1, in cfg).RangeStep,
                            "a set at its closest range, pressed IN again, stays put");

            var wide = new RadarPrefs(false, RadarMode.HeadUp, cfg.SafeRangeStepCount - 1);
            Assert.AreEqual(cfg.SafeRangeStepCount - 1, wide.SteppedRange(-1, in cfg).RangeStep);
        }

        [Test]
        public void PrefsDto_HealsAnUnknownMode_ToStandby()
        {
            var dto = new RadarPrefsDto { HullId = Hull, Mode = 99, RangeStep = 1 };
            Assert.AreEqual(RadarMode.Standby, dto.Prefs.Mode,
                            "an out-of-range mode heals to the safe answer — a set that is not " +
                            "radiating never lies about what is out there");
        }

        [Test]
        public void Prefs_LandOnTheSameSaveVersion_NoBumpNoMigration()
        {
            // ADR 0030: additive v10 member, the plotter's own precedent.
            SaveData save = SaveMigration.NewGame();
            Assert.IsNotNull(save.HullRadarPrefs, "a fresh save carries the (empty) list");
            Assert.IsEmpty(save.HullRadarPrefs);
            Assert.AreEqual(10, SaveMigration.CurrentVersion,
                            "the radar's row is ADDITIVE on v10 — if this ever needs a bump, it needs an " +
                            "ADR and a migration step, not a quiet edit here");
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
        }

        // ---- the pilot-deck cycle (SHIFT+K) --------------------------------------------------------

        [Test]
        public void PilotRing_WalksAllFourStatesAndComesBack()
        {
            var seen = new List<DevInstrumentCycle.DevPilotFit>();
            var at = DevInstrumentCycle.DevPilotFit.Owned;
            for (int i = 0; i < 4; i++)
            {
                at = DevInstrumentCycle.NextPilot(at);
                seen.Add(at);
            }
            CollectionAssert.AreEqual(new[]
            {
                DevInstrumentCycle.DevPilotFit.Gps,
                DevInstrumentCycle.DevPilotFit.GpsRadar,
                DevInstrumentCycle.DevPilotFit.Radar,
                DevInstrumentCycle.DevPilotFit.Owned,
            }, seen, "as-owned → gps → gps+radar → radar → as-owned");
        }

        [Test]
        public void PilotRing_ReachesRadarALONE_WhichIsTheWholePointOfACycle()
        {
            // With both fitted the two instruments compete for the eye; "show me just the scope" is
            // unreachable by owning MORE, which is the same argument that made the brow a cycle.
            var at = DevInstrumentCycle.DevPilotFit.Owned;
            bool sawRadarAlone = false;
            for (int i = 0; i < 4; i++)
            {
                at = DevInstrumentCycle.NextPilot(at);
                if (at == DevInstrumentCycle.DevPilotFit.Radar) sawRadarAlone = true;
            }
            Assert.IsTrue(sawRadarAlone);
        }

        [Test]
        public void ReachablePilot_DropsAStationTheConsoleHasNoSlotFor_AndKeepsTheOther()
        {
            HelmConsoleDef gpsOnly = Console(radar: false, gps: true);
            Assert.AreEqual(DevInstrumentCycle.DevPilotFit.Gps,
                            DevInstrumentCycle.ReachablePilot(DevInstrumentCycle.DevPilotFit.GpsRadar, gpsOnly),
                            "no radar slot: show the plotter rather than nothing");
            Assert.AreEqual(DevInstrumentCycle.DevPilotFit.Owned,
                            DevInstrumentCycle.ReachablePilot(DevInstrumentCycle.DevPilotFit.Radar, gpsOnly));

            HelmConsoleDef neither = Console(radar: false, gps: false, rig: ConsoleRigKind.Console);
            foreach (DevInstrumentCycle.DevPilotFit step in
                     new[] { DevInstrumentCycle.DevPilotFit.Gps, DevInstrumentCycle.DevPilotFit.GpsRadar,
                             DevInstrumentCycle.DevPilotFit.Radar })
                Assert.AreEqual(DevInstrumentCycle.DevPilotFit.Owned,
                                DevInstrumentCycle.ReachablePilot(step, neither),
                                "a skiff has neither station");

            Assert.AreEqual(DevInstrumentCycle.DevPilotFit.Owned,
                            DevInstrumentCycle.ReachablePilot(DevInstrumentCycle.DevPilotFit.GpsRadar, null),
                            "no dash, nothing to mount in");
        }

        [Test]
        public void DevFit_WithAPilotStep_FitsTheDeck_AndOnlyOnTheCallersScratchList()
        {
            HelmConsoleDef console = Console(radar: true, gps: true);
            var scratch = Owned();

            HelmFit fit = DevInstrumentCycle.DevFit(console, scratch, SounderKind.Depth,
                                                    DevInstrumentCycle.DevPilotFit.GpsRadar);
            Assert.IsTrue(fit.Radar);
            Assert.IsTrue(fit.Gps);
            Assert.Contains(BoatEquipment.RadarId, scratch, "widened on the SCRATCH list, the S2 mechanism");
            Assert.Contains(BoatEquipment.GpsId, scratch);

            // …and the very next real read wipes it: nothing survives into ownership.
            SaveData save = SaveMigration.NewGame();
            InstrumentLocker.OwnedFor(save, Hull, scratch);
            Assert.IsEmpty(scratch);
            Assert.IsFalse(InstrumentLocker.Owns(save, Hull, BoatEquipment.RadarId),
                           "no dev convenience may ever reach the player's save");
        }

        [Test]
        public void DevFit_NEVERHidesABoughtPilotInstrument_UnlikeTheBrowRing()
        {
            // ⚠ The asymmetry is deliberate and is what a live chartplotter test caught. The BROW ring
            // narrows because the Novi and Cape ship a depth sounder in their AUTHORED default, so
            // "show me a bare brow" is unreachable by owning less. No authored helm ships a radar or a
            // plotter, so a bare deck is ALREADY what owning nothing gives — narrowing here would buy
            // nothing and would HIDE a bought instrument the moment dev gating was on, which is the
            // editor's default.
            HelmConsoleDef console = Console(radar: true, gps: true);
            var bought = Owned(BoatEquipment.GpsId);

            HelmFit asOwned = DevInstrumentCycle.DevFit(console, bought, SounderKind.Depth,
                                                        DevInstrumentCycle.DevPilotFit.Owned);
            Assert.IsTrue(asOwned.Gps, "a plotter the player BOUGHT stays on the brow");
            Assert.IsFalse(asOwned.Radar, "…and one they did not is still absent");

            // A console that ships one fitted keeps it in every state of the ring: this ring only widens.
            HelmConsoleDef shipsBoth = Console(radar: true, gps: true, defaultRadar: true, defaultGps: true);
            foreach (DevInstrumentCycle.DevPilotFit step in
                     new[] { DevInstrumentCycle.DevPilotFit.Owned, DevInstrumentCycle.DevPilotFit.Gps,
                             DevInstrumentCycle.DevPilotFit.GpsRadar, DevInstrumentCycle.DevPilotFit.Radar })
            {
                HelmFit fit = DevInstrumentCycle.DevFit(shipsBoth, Owned(), SounderKind.Depth, step);
                Assert.IsTrue(fit.Radar, $"{step}: a hull that SHIPS a radar keeps it");
                Assert.IsTrue(fit.Gps, $"{step}: …and its plotter");
            }
        }

        [Test]
        public void DevFit_TheBrowOnlyOverload_STILLLeavesThePilotDeckAlone()
        {
            // The must-not-regress pin: S5 must not change what the three-argument form means, because
            // it is what "this is a BROW cycle" is written in.
            HelmConsoleDef shipsBoth = Console(radar: true, gps: true, defaultRadar: true, defaultGps: true);
            foreach (SounderKind step in new[] { SounderKind.None, SounderKind.Depth, SounderKind.Fish })
            {
                HelmFit fit = DevInstrumentCycle.DevFit(shipsBoth, Owned(), step);
                Assert.IsTrue(fit.Radar, "the brow-only form reads the pilot deck through untouched");
                Assert.IsTrue(fit.Gps);
            }
        }

        [Test]
        public void DevFit_OnEverySHIPPEDConsole_ThePilotStepNoneIsExactlyTodaysPicture()
        {
            // None of the four authored helms sets DefaultRadar/DefaultGps, so stating the deck as
            // "none" must be indistinguishable from not stating it — the regression bar S5 is held to.
            foreach (bool radarSlot in new[] { true, false })
                foreach (bool gpsSlot in new[] { true, false })
                {
                    HelmConsoleDef console = Console(radarSlot, gpsSlot);
                    HelmFit stated = DevInstrumentCycle.DevFit(console, Owned(), SounderKind.Depth,
                                                               DevInstrumentCycle.DevPilotFit.Owned);
                    HelmFit unstated = DevInstrumentCycle.DevFit(console, Owned(), SounderKind.Depth);
                    Assert.AreEqual(unstated.Radar, stated.Radar);
                    Assert.AreEqual(unstated.Gps, stated.Gps);
                    Assert.AreEqual(unstated.Sounder, stated.Sounder);
                    Assert.AreEqual(unstated.Compass, stated.Compass);
                }
        }

        [Test]
        public void DevFit_PilotStepDoesNotDisturbTheBrowCycle()
        {
            HelmConsoleDef console = Console(radar: true, gps: true);
            foreach (DevInstrumentCycle.DevPilotFit deck in
                     new[] { DevInstrumentCycle.DevPilotFit.Owned, DevInstrumentCycle.DevPilotFit.Gps,
                             DevInstrumentCycle.DevPilotFit.GpsRadar, DevInstrumentCycle.DevPilotFit.Radar })
                foreach (SounderKind brow in new[] { SounderKind.None, SounderKind.Depth, SounderKind.Fish })
                    Assert.AreEqual(brow,
                                    DevInstrumentCycle.DevFit(console, Owned(), brow, deck).Sounder,
                                    "the two rings are independent axes, by construction");
        }
    }
}
