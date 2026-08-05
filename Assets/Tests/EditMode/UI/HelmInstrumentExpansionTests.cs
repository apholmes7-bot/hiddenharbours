using NUnit.Framework;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The S4.5 expansion state machine (<see cref="HelmInstrumentExpansion"/>): instruments mount
    /// flush by default; the big card is opt-in, ONE at a time, and the state is transient UI —
    /// never a preference, never saved (rule 5; the finder's Ruling A precedent).
    /// </summary>
    public class HelmInstrumentExpansionTests
    {
        [TearDown]
        public void TearDown() => HelmInstrumentExpansion.Collapse();

        // ---- the pure toggle rule ------------------------------------------------------------------

        [Test]
        public void Toggled_ClickExpands_ClickAgainCollapses()
        {
            Assert.That(HelmInstrumentExpansion.Toggled(DashInstrument.None, DashInstrument.Sounder),
                        Is.EqualTo(DashInstrument.Sounder), "clicking a flush mount expands it");
            Assert.That(HelmInstrumentExpansion.Toggled(DashInstrument.Sounder, DashInstrument.Sounder),
                        Is.EqualTo(DashInstrument.None), "clicking the expanded mount collapses it");
        }

        [Test]
        public void Toggled_ClickingNothing_ChangesNothing()
        {
            Assert.That(HelmInstrumentExpansion.Toggled(DashInstrument.Sounder, DashInstrument.None),
                        Is.EqualTo(DashInstrument.Sounder));
            Assert.That(HelmInstrumentExpansion.Toggled(DashInstrument.None, DashInstrument.None),
                        Is.EqualTo(DashInstrument.None));
        }

        [Test]
        public void OneExpandedAtATime_IsTypeLevel()
        {
            // The invariant is the SHAPE of the state — a single value — so "two expanded at once"
            // is not a case anyone can code their way into. This pin documents (and freezes) that
            // choice: the state is one enum, not a set of flags.
            HelmInstrumentExpansion.Toggle(DashInstrument.Sounder);
            Assert.That(HelmInstrumentExpansion.Current, Is.EqualTo(DashInstrument.Sounder));
            HelmInstrumentExpansion.Collapse();
            Assert.That(HelmInstrumentExpansion.Current, Is.EqualTo(DashInstrument.None));
        }

        // ---- never persisted -----------------------------------------------------------------------

        [Test]
        public void ExpansionNeverTouchesTheSave_NoPrefsMove()
        {
            // Expanding/collapsing is where the player's EYES are, not a preference. Drive the state
            // through its whole cycle against a live save row and pin that no instrument pref moved
            // and no dirty flag was raised — InstrumentLocker.SetPrefs untouched by expansion.
            SaveData save = SaveMigration.NewGame();
            SounderPrefs before = InstrumentLocker.PrefsFor(save, "boat.test_expand",
                                                            SounderPrefs.FromDefaults(
                                                                DepthSounderSettings.Default,
                                                                FishFinderSettings.Default));
            int rowsBefore = save.HullInstruments != null ? save.HullInstruments.Count : 0;

            HelmInstrumentExpansion.Toggle(DashInstrument.Sounder);
            HelmInstrumentExpansion.Toggle(DashInstrument.Sounder);
            HelmInstrumentExpansion.Toggle(DashInstrument.Sounder);
            HelmInstrumentExpansion.Collapse();

            SounderPrefs after = InstrumentLocker.PrefsFor(save, "boat.test_expand",
                                                           SounderPrefs.FromDefaults(
                                                               DepthSounderSettings.Default,
                                                               FishFinderSettings.Default));
            int rowsAfter = save.HullInstruments != null ? save.HullInstruments.Count : 0;
            Assert.That(after.AlarmMetres, Is.EqualTo(before.AlarmMetres));
            Assert.That(after.Feet, Is.EqualTo(before.Feet));
            Assert.That(after.Night, Is.EqualTo(before.Night));
            Assert.That(after.Armed, Is.EqualTo(before.Armed));
            Assert.That(after.RangeMetres, Is.EqualTo(before.RangeMetres));
            Assert.That(rowsAfter, Is.EqualTo(rowsBefore),
                        "expansion wrote no instrument row into the save");
        }

        // ---- the shared dash predicate -------------------------------------------------------------

        [Test]
        public void DashCarriesBrow_IsTheDashHostsOwnRule()
        {
            // The one predicate the dash host and both instrument hosts share: a LEVER helm with a
            // console rig mounts its brow on the dash; a tiller (no console) or a rig-less lever
            // keeps the standalone cards.
            Assert.That(HelmInstrumentExpansion.DashCarriesBrow(HelmControlStyle.Lever,
                                                                ConsoleRigKind.Console), Is.True);
            Assert.That(HelmInstrumentExpansion.DashCarriesBrow(HelmControlStyle.Lever,
                                                                ConsoleRigKind.Cape), Is.True);
            Assert.That(HelmInstrumentExpansion.DashCarriesBrow(HelmControlStyle.Lever,
                                                                ConsoleRigKind.None), Is.False,
                        "a lever with no console rig falls back to the lone card");
            Assert.That(HelmInstrumentExpansion.DashCarriesBrow(HelmControlStyle.Tiller,
                                                                ConsoleRigKind.None), Is.False,
                        "a tiller boat keeps the S1/S2 standalone behaviour");
            Assert.That(HelmInstrumentExpansion.DashCarriesBrow(HelmControlStyle.None,
                                                                ConsoleRigKind.None), Is.False);
        }
    }
}
