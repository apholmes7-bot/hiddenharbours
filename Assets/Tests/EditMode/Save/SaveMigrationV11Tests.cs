using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// SaveData v10 → v11 — the player's own notebook lines (the My Notes tab).
    ///
    /// <para><b>This step ADDS and must never HEAL</b>, which is what makes it different from every
    /// sibling above it. v9's bump repaired a zero range (a divide-by-zero); v10's dropped
    /// undrawable nav rows. This one adds a list and stops, because
    /// <see cref="PlayerNoteDto.Text"/> is the player's own prose — the only such field in the save
    /// file — and the only correct transformation of it is none. Half the tests here exist to stop a
    /// later lane adding a "tidy" pass in good faith.</para>
    ///
    /// <para><b>Asserted off <c>JsonUtility.FromJson&lt;SaveData&gt;</c> DIRECTLY</b>, following
    /// <c>SaveMigrationV10Tests</c>: <c>SaveSerialization.FromJson</c> already migrates, so a
    /// migration defect is invisible through the public path. Every blob below is deserialized raw
    /// and migrated by hand, and the v10-shaped JSON is proved genuinely v10 before it is trusted.</para>
    ///
    /// <para><b>Version-agnostic on the counter</b>, also following that suite: these own the v11
    /// CONTRACT, not the value of a number every later lane bumps.</para>
    /// </summary>
    public class SaveMigrationV11Tests
    {
        /// <summary>A v10 save as it exists on disk today: real play state, nav present, and NO
        /// notebook key at all.</summary>
        private const string V10Json =
            "{\"SchemaVersion\":10,\"WorldSeed\":4242,\"GameTimeSeconds\":9001.5,\"Money\":175," +
            "\"ActiveHullId\":\"boat.novi\",\"OwnedBoats\":[\"boat.dory\",\"boat.novi\"]," +
            "\"NavWaypoints\":[],\"NavRoute\":[],\"NavTrack\":[]}";

        // ---- guard the guard -----------------------------------------------------------------------

        [Test]
        public void TheV10Blob_IsGenuinelyV10_BeforeAnythingElseTrustsIt()
        {
            // If this JSON ever gained the notebook key, every assertion below would pass for the
            // wrong reason — the same trap SaveMigrationV10Tests guards on its own fixture.
            StringAssert.DoesNotContain("PlayerNotes", V10Json);

            SaveData raw = JsonUtility.FromJson<SaveData>(V10Json);
            Assert.AreEqual(10, raw.SchemaVersion, "the blob must arrive as v10, un-migrated");
        }

        // ---- condition 2 + 3(a): the ABSENT case, and a clean load with zero notes ------------------

        [Test]
        public void AV10Save_MigratesToCurrent_WithAnEmptyNotebook_AndInventsNothing()
        {
            SaveData d = SaveMigration.Migrate(JsonUtility.FromJson<SaveData>(V10Json));

            Assert.AreEqual(SaveMigration.CurrentVersion, d.SchemaVersion);
            Assert.IsNotNull(d.PlayerNotes, "an absent key must become an empty list, never null");
            Assert.AreEqual(0, d.PlayerNotes.Count,
                "a v10 player had no notebook; they must get a blank page, not invented lines");

            // ...and nothing else was disturbed on the way past.
            Assert.AreEqual(4242, d.WorldSeed);
            Assert.AreEqual(175, d.Money);
            Assert.AreEqual("boat.novi", d.ActiveHullId);
        }

        /// <summary>
        /// The ABSENT case again, but reached the OTHER way: a blob that already claims v11 and yet
        /// has no notebook key (hand-edited, or written by a build between the field landing and this
        /// step). The version guard cannot help here — <c>SchemaVersion &lt; 11</c> is false — so this
        /// proves the unconditional null-repair carries it independently of the counter. Without that
        /// second line in the migration, this is a NullReferenceException the first time the notebook
        /// opens.
        /// </summary>
        [Test]
        public void AV11BlobMissingTheKeyEntirely_IsRepairedByTheUnconditionalPass()
        {
            const string json = "{\"SchemaVersion\":11,\"WorldSeed\":7,\"Money\":0}";
            StringAssert.DoesNotContain("PlayerNotes", json);

            SaveData d = SaveMigration.Migrate(JsonUtility.FromJson<SaveData>(json));

            Assert.IsNotNull(d.PlayerNotes,
                "the version step is skipped at v11, so only the unconditional repair can save this");
            Assert.AreEqual(0, d.PlayerNotes.Count);
        }

        // ---- condition 2: the EMPTY case --------------------------------------------------------------

        [Test]
        public void AnExplicitlyEmptyNotebookLoadsAsEmpty_NotAsMissing()
        {
            const string json = "{\"SchemaVersion\":11,\"WorldSeed\":7,\"PlayerNotes\":[]}";

            SaveData d = SaveMigration.Migrate(JsonUtility.FromJson<SaveData>(json));

            Assert.IsNotNull(d.PlayerNotes);
            Assert.AreEqual(0, d.PlayerNotes.Count);
        }

        // ---- condition 3(b): the round trip ------------------------------------------------------------

        [Test]
        public void NotesSurviveLoadResaveReload_Identically()
        {
            SaveData first = SaveMigration.Migrate(JsonUtility.FromJson<SaveData>(V10Json));
            first.PlayerNotes.Add(new PlayerNoteDto { Text = "twine from the store", IsTask = true, Done = true });
            first.PlayerNotes.Add(new PlayerNoteDto { Text = "four traps by dusk", IsTask = true, Done = false });
            first.PlayerNotes.Add(new PlayerNoteDto { Text = "the cove fishes better on the ebb", IsTask = false });

            string json = JsonUtility.ToJson(first);
            SaveData second = SaveMigration.Migrate(JsonUtility.FromJson<SaveData>(json));

            Assert.AreEqual(SaveMigration.CurrentVersion, second.SchemaVersion);
            Assert.AreEqual(3, second.PlayerNotes.Count);

            Assert.AreEqual("twine from the store", second.PlayerNotes[0].Text);
            Assert.IsTrue(second.PlayerNotes[0].IsTask);
            Assert.IsTrue(second.PlayerNotes[0].Done);

            Assert.AreEqual("four traps by dusk", second.PlayerNotes[1].Text);
            Assert.IsTrue(second.PlayerNotes[1].IsTask);
            Assert.IsFalse(second.PlayerNotes[1].Done);

            Assert.AreEqual("the cove fishes better on the ebb", second.PlayerNotes[2].Text);
            Assert.IsFalse(second.PlayerNotes[2].IsTask, "a prose line carries no checkbox");

            Assert.AreEqual(JsonUtility.ToJson(second), json, "a second trip through must change nothing");
        }

        [Test]
        public void HerOrderIsPreserved_BecauseOrderIsTheOnlyHandleANoteHas()
        {
            SaveData d = SaveMigration.Migrate(JsonUtility.FromJson<SaveData>(V10Json));
            string[] written = { "first", "second", "third", "fourth" };
            foreach (string t in written) d.PlayerNotes.Add(new PlayerNoteDto { Text = t });

            SaveData reloaded = SaveMigration.Migrate(
                JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(d)));

            CollectionAssert.AreEqual(written, reloaded.PlayerNotes.Select(n => n.Text).ToArray(),
                "notes have no id — position IS the identity, so a reorder on load loses her page");
        }

        // ---- condition 4: the migration must NEVER normalise her prose ----------------------------------

        /// <summary>
        /// Deliberately awkward prose: leading and trailing spaces, a double space, a tab, an em dash,
        /// curly quotes, an accented letter, a surrogate pair, and a line far longer than any sane
        /// cap. Every one of these is something a well-meaning "tidy" or "cap" pass would change, and
        /// every one must come back byte-identical.
        /// </summary>
        static readonly string[] AwkwardButHers =
        {
            "  leading and trailing spaces  ",
            "a  double  space",
            "a\ttab",
            "cod off the point — 2 totes",
            "she said “on the ebb”",
            "réal, with an accent",
            "a fish emoji \U0001F41F and text after it",
            "", // an empty line she left blank on purpose
            new string('x', 500),
        };

        [Test]
        public void TheMigrationNeverTrimsRecomputesOrReEncodesHerText()
        {
            SaveData d = SaveMigration.Migrate(JsonUtility.FromJson<SaveData>(V10Json));
            foreach (string t in AwkwardButHers) d.PlayerNotes.Add(new PlayerNoteDto { Text = t });

            // Through the file and back, then migrated a second time for good measure.
            SaveData reloaded = SaveMigration.Migrate(
                JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(d)));
            SaveData twice = SaveMigration.Migrate(reloaded);

            Assert.AreEqual(AwkwardButHers.Length, twice.PlayerNotes.Count,
                "not one line may be dropped — there is no such thing as an invalid note");

            for (int i = 0; i < AwkwardButHers.Length; i++)
                Assert.AreEqual(AwkwardButHers[i], twice.PlayerNotes[i].Text,
                    $"note {i} was altered in transit. Nothing may normalise the player's own prose — " +
                    "no trim, no truncation, no re-encoding, no whitespace collapsing.");
        }

        [Test]
        public void ALongNotebookIsNotCapped()
        {
            // Every other list on SaveData has a ceiling because the GAME fills it. This one is typed
            // by a person, so a cap at rest could only ever delete something she wrote.
            SaveData d = SaveMigration.Migrate(JsonUtility.FromJson<SaveData>(V10Json));
            for (int i = 0; i < 500; i++) d.PlayerNotes.Add(new PlayerNoteDto { Text = "note " + i });

            SaveData reloaded = SaveMigration.Migrate(
                JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(d)));

            Assert.AreEqual(500, reloaded.PlayerNotes.Count,
                "if this ever trims, a cap has been added at rest instead of at the point of entry");
            Assert.AreEqual("note 499", reloaded.PlayerNotes[499].Text);
        }

        // ---- idempotence -------------------------------------------------------------------------------

        [Test]
        public void MigratingTwiceChangesNothing()
        {
            SaveData once = SaveMigration.Migrate(JsonUtility.FromJson<SaveData>(V10Json));
            once.PlayerNotes.Add(new PlayerNoteDto { Text = "  keep me exactly  ", IsTask = true });

            string after = JsonUtility.ToJson(once);
            string afterAgain = JsonUtility.ToJson(SaveMigration.Migrate(once));

            Assert.AreEqual(after, afterAgain);
        }

        // ---- condition 1: the DTO's fields are append-only ------------------------------------------------

        /// <summary>
        /// The shipped field set, pinned. Renaming or removing one silently drops that data from
        /// every existing save — <c>JsonUtility</c> leaves an unmatched field at its default and says
        /// nothing — so this is the tripwire for a refactor that looks harmless.
        ///
        /// <para>ADDING a field is fine and is not caught here, deliberately: append-only means the
        /// existing ones are load-bearing, not that the shape is frozen.</para>
        /// </summary>
        [Test]
        public void PlayerNoteDtoFieldsAreAppendOnly()
        {
            var fields = typeof(PlayerNoteDto)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(f => f.Name, f => f.FieldType);

            var required = new Dictionary<string, System.Type>
            {
                { "Text", typeof(string) },
                { "IsTask", typeof(bool) },
                { "Done", typeof(bool) },
            };

            var missing = required
                .Where(r => !fields.TryGetValue(r.Key, out var t) || t != r.Value)
                .Select(r => $"{r.Value.Name} {r.Key}")
                .ToList();

            Assert.That(missing, Is.Empty,
                "these fields have shipped and are append-only. Renaming or removing one drops that " +
                "data from every existing save without an error: " + string.Join(", ", missing));
        }

        [Test]
        public void PlayerNotesIsOnSaveDataItself_NotInASecondStore()
        {
            // Condition 5, made mechanical: the notebook rides the existing save surface beside the
            // nav rows, rather than a new service or a keyed side-store.
            var field = typeof(SaveData).GetField("PlayerNotes", BindingFlags.Public | BindingFlags.Instance);

            Assert.IsNotNull(field, "PlayerNotes must live on SaveData");
            Assert.AreEqual(typeof(List<PlayerNoteDto>), field.FieldType);
            Assert.AreEqual(typeof(SaveData).Assembly, typeof(PlayerNoteDto).Assembly,
                "the DTO belongs beside NavWaypointDto in the Core save surface");
        }
    }
}
