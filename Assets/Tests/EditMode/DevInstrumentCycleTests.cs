using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The DEV BROW CYCLE's pure rules (ADR 0025 S3d) — the ring
    /// (<see cref="DevInstrumentCycle.NextBrow"/>), the per-console reachability clamp
    /// (<see cref="DevInstrumentCycle.Reachable"/>) and the resolved fit
    /// (<see cref="DevInstrumentCycle.DevFit"/>).
    ///
    /// <para>The load-bearing property is the one a blanket "dev owns everything" grant cannot have:
    /// <b>the cycle must be able to express FEWER instruments than the hull ships</b>. Two of the four
    /// shipped consoles (Novi, Cape) carry <see cref="SounderKind.Depth"/> in their authored default and a
    /// real purchase can never be un-bought, so owning less can never show less — which is exactly why
    /// <see cref="DevInstrumentCycle.DevFit"/> narrows the RESOLVED value as well as widening the owned
    /// set. Without that, "show me a bare brow" is unreachable and the owner can never look at an empty
    /// cutout.</para>
    ///
    /// <para>Pure over a throwaway <see cref="HelmConsoleDef"/> — no scene, no boat, no save.</para>
    /// </summary>
    public class DevInstrumentCycleTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private HelmConsoleDef Console(SounderKind defaultSounder, bool fishSlot,
                                       ConsoleRigKind rig = ConsoleRigKind.Console,
                                       CompassMount defaultCompass = CompassMount.None,
                                       bool radar = false, bool gps = false)
        {
            var c = ScriptableObject.CreateInstance<HelmConsoleDef>();
            c.Id = "helm.test";
            c.Rig = rig;
            c.DefaultSounder = defaultSounder;
            c.DefaultCompass = defaultCompass;
            c.DefaultRadar = radar;
            c.DefaultGps = gps;
            c.SupportsFishFinder = fishSlot;
            c.SupportsDomeCompass = true;
            c.SupportsFlushCompass = false;
            c.SupportsRadar = radar;
            c.SupportsGps = gps;
            _spawned.Add(c);
            return c;
        }

        private static List<string> Owned(params string[] ids) => new List<string>(ids);

        // ---- the ring ---------------------------------------------------------------------------------

        [Test]
        public void TheRing_WalksBareThenDepthThenFish_AndCloses()
        {
            Assert.AreEqual(SounderKind.Depth, DevInstrumentCycle.NextBrow(SounderKind.None, true));
            Assert.AreEqual(SounderKind.Fish, DevInstrumentCycle.NextBrow(SounderKind.Depth, true));
            Assert.AreEqual(SounderKind.None, DevInstrumentCycle.NextBrow(SounderKind.Fish, true));
        }

        [Test]
        public void TheRing_SkipsTheFinder_OnAConsoleThatCannotTakeIt()
        {
            // Skipping matters because EffectiveFit would SWALLOW the unsupported id and leave the brow
            // where it was — the key would look like a dropped keypress rather than a refused slot.
            Assert.AreEqual(SounderKind.None, DevInstrumentCycle.NextBrow(SounderKind.Depth, false),
                            "a brow that cannot take the finder steps straight back to bare");
            Assert.AreEqual(SounderKind.Depth, DevInstrumentCycle.NextBrow(SounderKind.None, false),
                            "…but the basic sounder always fits: a console is by definition a dash with a brow");
        }

        [Test]
        public void TheRing_ReturnsToItsStart_AndNeverLeavesTheSet()
        {
            foreach (bool fishSlot in new[] { true, false })
            foreach (SounderKind start in new[] { SounderKind.None, SounderKind.Depth, SounderKind.Fish })
            {
                SounderKind at = start;
                var seen = new List<SounderKind>();
                for (int i = 0; i < 3; i++)
                {
                    at = DevInstrumentCycle.NextBrow(at, fishSlot);
                    seen.Add(at);
                    Assert.Contains(at, new[] { SounderKind.None, SounderKind.Depth, SounderKind.Fish },
                                    "the ring never leaves the three shipped brow states");
                    if (!fishSlot)
                        Assert.AreNotEqual(SounderKind.Fish, at,
                                           "…and never proposes an instrument this brow cannot take");
                }
                Assert.Contains(SounderKind.None, seen, "bare is always reachable within one lap");
                Assert.Contains(SounderKind.Depth, seen, "the sounder is always reachable within one lap");
            }
        }

        [Test]
        public void TheRing_ReachesEveryShippedInstrument_WithinOneLap()
        {
            // The owner's whole ask: three presses show him all three states of the cutout.
            SounderKind at = SounderKind.None;
            var seen = new HashSet<SounderKind> { at };
            for (int i = 0; i < 3; i++) { at = DevInstrumentCycle.NextBrow(at, true); seen.Add(at); }
            Assert.AreEqual(3, seen.Count, "bare, depth sounder and fish finder are all reachable");
            Assert.AreEqual(SounderKind.None, at, "…and the lap closes where it started");
        }

        // ---- reachability -----------------------------------------------------------------------------

        [Test]
        public void Reachable_ClampsTheFinder_ToTheSounder_OnAnIncapableBrow()
        {
            var canFit = Console(SounderKind.None, fishSlot: true);
            var cannotFit = Console(SounderKind.None, fishSlot: false);

            Assert.AreEqual(SounderKind.Fish, DevInstrumentCycle.Reachable(SounderKind.Fish, canFit));
            Assert.AreEqual(SounderKind.Depth, DevInstrumentCycle.Reachable(SounderKind.Fish, cannotFit),
                            "the carried tier falls back to the nearest unit the hull CAN carry");
            Assert.AreEqual(SounderKind.None, DevInstrumentCycle.Reachable(SounderKind.None, cannotFit));
            Assert.AreEqual(SounderKind.Depth, DevInstrumentCycle.Reachable(SounderKind.Depth, cannotFit));
        }

        [Test]
        public void Reachable_OnNoConsole_IsNothing_TheDory()
        {
            foreach (SounderKind step in new[] { SounderKind.None, SounderKind.Depth, SounderKind.Fish })
                Assert.AreEqual(SounderKind.None, DevInstrumentCycle.Reachable(step, null),
                                "there is no dash on a rowed dory to bolt anything into");
        }

        // ---- the resolved fit -------------------------------------------------------------------------

        [Test]
        public void DevFit_ShowsFEWER_ThanTheHullShips_TheWholePointOfACycle()
        {
            // ⚠ THE test. The Novi/Cape consoles ship a depth sounder in their AUTHORED default, so a dev
            // override built only from "add owned ids" can never take the brow back to bare — the owner
            // would lose the ability to look at an empty cutout the moment the cycle existed.
            var shipsWithOne = Console(SounderKind.Depth, fishSlot: true, rig: ConsoleRigKind.Novi);

            Assert.AreEqual(SounderKind.Depth,
                BoatEquipment.EffectiveFit(shipsWithOne, Owned()).Sounder,
                "precondition: owning nothing still shows the console's authored sounder");

            Assert.AreEqual(SounderKind.None,
                DevInstrumentCycle.DevFit(shipsWithOne, Owned(), SounderKind.None).Sounder,
                "…and the cycle can still take it away, which no grant could");
        }

        [Test]
        public void DevFit_ShowsMORE_ThanTheHullShips_OnABareBrow()
        {
            var bare = Console(SounderKind.None, fishSlot: true);
            Assert.AreEqual(SounderKind.None, DevInstrumentCycle.DevFit(bare, Owned(), SounderKind.None).Sounder);
            Assert.AreEqual(SounderKind.Depth, DevInstrumentCycle.DevFit(bare, Owned(), SounderKind.Depth).Sounder);
            Assert.AreEqual(SounderKind.Fish, DevInstrumentCycle.DevFit(bare, Owned(), SounderKind.Fish).Sounder);
        }

        [Test]
        public void DevFit_NeverProducesAFitTheHullCannotCarry()
        {
            var cannotFit = Console(SounderKind.Depth, fishSlot: false, rig: ConsoleRigKind.Cape);
            foreach (SounderKind step in new[] { SounderKind.None, SounderKind.Depth, SounderKind.Fish })
                Assert.AreNotEqual(SounderKind.Fish,
                    DevInstrumentCycle.DevFit(cannotFit, Owned(), step).Sounder,
                    $"step {step} must never light a finder in a brow that cannot take one");

            Assert.AreEqual(HelmFit.None.Sounder,
                DevInstrumentCycle.DevFit(null, Owned(), SounderKind.Fish).Sounder,
                "and a hull with no console resolves to nothing at all");
            Assert.IsFalse(DevInstrumentCycle.DevFit(null, Owned(), SounderKind.Fish).HasConsole);
        }

        [Test]
        public void DevFit_OverridesEvenARealOwnedFinder_SoTheOwnerCanStillSeeTheBasicUnit()
        {
            // The dev cycle is a view of the brow, not a purchase ledger: it must win over what the save
            // says or the owner could never step back down off an instrument he has actually bought.
            var console = Console(SounderKind.None, fishSlot: true);
            var reallyOwned = Owned(BoatEquipment.FishFinderId);

            Assert.AreEqual(SounderKind.Fish, BoatEquipment.EffectiveFit(console, Owned(BoatEquipment.FishFinderId)).Sounder,
                            "precondition: the bought finder owns the cutout");
            Assert.AreEqual(SounderKind.Depth,
                DevInstrumentCycle.DevFit(console, reallyOwned, SounderKind.Depth).Sounder,
                "the cycle steps down past a bought finder…");
            Assert.AreEqual(SounderKind.None,
                DevInstrumentCycle.DevFit(console, Owned(BoatEquipment.FishFinderId), SounderKind.None).Sounder,
                "…and all the way back to bare");
        }

        [Test]
        public void DevFit_TouchesOnlyTheBrow_NotTheRestOfTheDash()
        {
            // It is a BROW cycle, not a dev mode that re-fits the whole console.
            var console = Console(SounderKind.Depth, fishSlot: true, rig: ConsoleRigKind.Sport,
                                  defaultCompass: CompassMount.Dome, radar: true, gps: true);
            foreach (SounderKind step in new[] { SounderKind.None, SounderKind.Depth, SounderKind.Fish })
            {
                HelmFit fit = DevInstrumentCycle.DevFit(console, Owned(), step);
                Assert.AreEqual(ConsoleRigKind.Sport, fit.Rig, "the renderer is the console's, always");
                Assert.AreEqual(CompassMount.Dome, fit.Compass, "the compass is untouched by a brow step");
                Assert.IsTrue(fit.Radar);
                Assert.IsTrue(fit.Gps);
            }
        }

        [Test]
        public void DevFit_WidensOnlyTheCallersScratchList_NeverAnOwnershipRecord()
        {
            // The save-pollution guard at unit level (its live twin is DevInstrumentCyclePlayTests):
            // the ONLY mutation this function is allowed is on the caller's per-read scratch list, which
            // InstrumentLocker.OwnedFor clears and refills on every single read.
            var console = Console(SounderKind.None, fishSlot: true);
            var scratch = Owned();

            DevInstrumentCycle.DevFit(console, scratch, SounderKind.Fish);
            Assert.Contains(BoatEquipment.FishFinderId, scratch,
                            "the step is applied by widening the scratch set — the S2 bypass's own mechanism");

            var save = SaveMigration.NewGame();
            InstrumentLocker.OwnedFor(save, "boat.test", scratch);
            Assert.IsEmpty(scratch, "…and the very next read wipes it: nothing survives into ownership");
            Assert.IsEmpty(save.HullInstruments, "the save never learned about a dev grant");
        }

        // ---- the shipped consoles ----------------------------------------------------------------------

        [Test]
        public void EveryShippedConsole_ReachesAllThreeBrowStates()
        {
            // The acceptance criterion, asserted against the SHIPPED assets rather than a fixture: F to any
            // consoled hull and the cycle key shows bare, sounder and finder in turn.
            string[] paths =
            {
                "Assets/_Project/Data/Boats/Helms/ConsoleSkiffHelm.asset",
                "Assets/_Project/Data/Boats/Helms/SportSkiffHelm.asset",
                "Assets/_Project/Data/Boats/Helms/NoviHelm.asset",
                "Assets/_Project/Data/Boats/Helms/CapeIslanderHelm.asset",
            };

            foreach (string path in paths)
            {
                var console = UnityEditor.AssetDatabase.LoadAssetAtPath<HelmConsoleDef>(path);
                Assert.IsNotNull(console, $"the shipped console must exist at {path}");

                var reached = new HashSet<SounderKind>();
                SounderKind at = SounderKind.None;
                for (int i = 0; i < 3; i++)
                {
                    at = DevInstrumentCycle.NextBrow(at, console.SupportsFishFinder);
                    reached.Add(DevInstrumentCycle.DevFit(console, Owned(), at).Sounder);
                }

                Assert.IsTrue(reached.Contains(SounderKind.None), $"{console.Id}: a bare brow is reachable");
                Assert.IsTrue(reached.Contains(SounderKind.Depth), $"{console.Id}: the sounder is reachable");
                Assert.IsTrue(reached.Contains(SounderKind.Fish),
                              $"{console.Id}: every shipped helm takes the finder today — if one stops, the " +
                              "ring must SKIP it rather than this test being relaxed");
            }
        }
    }
}
