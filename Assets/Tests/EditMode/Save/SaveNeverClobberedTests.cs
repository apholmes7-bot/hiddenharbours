using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The save is never clobbered. Under the test runner the game opens the run's own save instead of the
    /// player's (<see cref="SaveStore.ActivePath"/>). A New Game keeps the game it erases, and the first
    /// write over a save that could not be read keeps that file (<see cref="SaveStore.KeepReplaced"/>), each
    /// whole, beside the save. Every other write behaves as it always has.
    ///
    /// <para>All on temp files: the store directly, and a real <see cref="SaveService"/> opened on a temp
    /// save (it does not awake in edit mode, so it never opens the player's). The PlayMode half,
    /// <c>SaveNeverClobberedPlayTests</c>, drives the live service of a real run.</para>
    /// </summary>
    public class SaveNeverClobberedTests
    {
        private static readonly byte[] Unreadable =
            Encoding.UTF8.GetBytes("not a save {\"SchemaVersion\": ").Concat(new byte[] { 0xFF, 0x00, 0x13 }).ToArray();

        private string _dir;
        private string _save;
        private string _variableBefore;
        private bool? _demoBefore;
        private readonly List<GameObject> _made = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hh_save_never_clobbered_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _save = Path.Combine(_dir, SaveStore.DefaultFileName);
            _variableBefore = System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable);
            _demoBefore = DemoBuild.Override;
            DemoBuild.Override = false;
            GameServices.Reset();
            ShellFlow.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
            GameServices.Reset();
            ShellFlow.Reset();
            System.Environment.SetEnvironmentVariable(SaveStore.TestRunSaveVariable, _variableBefore);
            DemoBuild.Override = _demoBefore;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        // ---- which save the game opens -----------------------------------------------------------

        [Test]
        public void ActivePath_IsThePlayersSave_WhenNoRunHasSetOne()
        {
            System.Environment.SetEnvironmentVariable(SaveStore.TestRunSaveVariable, null);
            Assert.That(SaveStore.ActivePath, Is.EqualTo(SaveStore.DefaultPath));
        }

        [Test]
        public void ActivePath_IsTheRunsSave_WhileARunHasSetOne()
        {
            System.Environment.SetEnvironmentVariable(SaveStore.TestRunSaveVariable, _save);
            Assert.That(SaveStore.ActivePath, Is.EqualTo(_save));
            Assert.That(SaveStore.ActivePath, Is.Not.EqualTo(SaveStore.DefaultPath));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DefaultPath_SelectsOnlyTheCurrentMode(bool demo)
        {
            DemoBuild.Override = demo;
            System.Environment.SetEnvironmentVariable(SaveStore.TestRunSaveVariable, null);
            string expected = demo
                ? Path.Combine(Application.persistentDataPath, "demo", SaveStore.DefaultFileName)
                : Path.Combine(Application.persistentDataPath, SaveStore.DefaultFileName);
            string other = demo
                ? Path.Combine(Application.persistentDataPath, SaveStore.DefaultFileName)
                : Path.Combine(Application.persistentDataPath, "demo", SaveStore.DefaultFileName);
            Assert.That(Path.GetFullPath(SaveStore.DefaultPath), Is.EqualTo(Path.GetFullPath(expected)));
            Assert.That(Path.GetFullPath(SaveStore.ActivePath), Is.EqualTo(Path.GetFullPath(expected)));
            Assert.That(Path.GetFullPath(SaveStore.DefaultPath), Is.Not.EqualTo(Path.GetFullPath(other)),
                "The demo and the full game must never select the same file.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Modes_NeverContinueOrReplaceTheOtherGame(bool demo)
        {
            DemoBuild.Override = demo;
            System.Environment.SetEnvironmentVariable(SaveStore.TestRunSaveVariable, _save);
            string selected = demo ? Path.Combine(_dir, "demo", SaveStore.DefaultFileName) : _save;
            string other = demo ? _save : Path.Combine(_dir, "demo", SaveStore.DefaultFileName);
            Assert.That(SaveStore.ActivePath, Is.EqualTo(selected));
            WriteGame(other, money: 4242);
            var protectedFiles = new List<string> { other, other + ".tmp" };
            for (int n = 1; n <= SaveStore.KeptGameCount; n++)
                protectedFiles.Add(SaveStore.KeptPath(other, n));
            foreach (string path in protectedFiles.Skip(1)) File.WriteAllText(path, "other game: " + path);
            var original = protectedFiles.ToDictionary(path => path, File.ReadAllBytes);
            var locks = new List<FileStream>();
            try
            {
                foreach (string path in protectedFiles)
                    locks.Add(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None));
                var service = OpenService(SaveStore.ActivePath);
                Assert.That(service.SavePath, Is.EqualTo(selected));
                Assert.That(service.LoadedExistingSave, Is.False,
                    "The title must not offer the other mode's game as Continue.");
                Assert.That(service.Current.Money, Is.EqualTo(SaveMigration.NewGame().Money));
                service.BeginNewGame();
                service.Current.Money = 77;
                service.Save();
                Assert.That(SaveStore.Read(selected).Money, Is.EqualTo(77));
                var replaced = new List<byte[]>();
                for (int n = 0; n <= SaveStore.KeptGameCount; n++)
                {
                    service.Current.Money = 100 + n;
                    service.Save();
                    replaced.Add(File.ReadAllBytes(selected));
                    service.BeginNewGame();
                }
                for (int n = 1; n <= SaveStore.KeptGameCount; n++)
                    Assert.That(File.ReadAllBytes(SaveStore.KeptPath(selected, n)),
                        Is.EqualTo(replaced[replaced.Count - n]));
                service.Current.Money = 88;
                service.Save();
                service.Open(SaveStore.ActivePath);
                Assert.That(service.LoadedExistingSave, Is.True);
                Assert.That(service.Current.Money, Is.EqualTo(88));
                var expectedFiles = new List<string> { selected };
                for (int n = 1; n <= SaveStore.KeptGameCount; n++)
                    expectedFiles.Add(SaveStore.KeptPath(selected, n));
                CollectionAssert.AreEquivalent(expectedFiles,
                    Directory.GetFiles(Path.GetDirectoryName(selected)),
                    "Only the selected save and its kept copies belong to this slot.");
            }
            finally { foreach (var stream in locks) stream.Dispose(); }
            foreach (var pair in original)
                Assert.That(File.ReadAllBytes(pair.Key), Is.EqualTo(pair.Value));
        }

        // ---- keeping a replaced file ---------------------------------------------------------------

        [Test]
        public void KeepReplaced_KeepsTheFileWhole_BesideIt()
        {
            File.WriteAllBytes(_save, Unreadable);

            string kept = SaveStore.KeepReplaced(_save);

            Assert.That(kept, Is.EqualTo(Path.Combine(_dir, "savegame.kept-1.json")));
            Assert.That(File.ReadAllBytes(kept), Is.EqualTo(Unreadable), "The kept copy is not the file, byte for byte.");
            Assert.That(File.ReadAllBytes(_save), Is.EqualTo(Unreadable), "Keeping a copy changed the save itself.");
        }

        [Test]
        public void KeepReplaced_WithNothingOnDisk_KeepsNothing()
        {
            Assert.That(SaveStore.KeepReplaced(_save), Is.Null);
            Assert.That(Directory.GetFiles(_dir), Is.Empty);
        }

        [Test]
        public void KeepReplaced_ShiftsOlderCopies_AndDropsThoseOverTheCount()
        {
            int replaced = SaveStore.KeptGameCount + 1;
            for (int game = 1; game <= replaced; game++)
            {
                File.WriteAllText(_save, $"game {game}");
                SaveStore.KeepReplaced(_save);
            }

            for (int n = 1; n <= SaveStore.KeptGameCount; n++)
                Assert.That(File.ReadAllText(SaveStore.KeptPath(_save, n)), Is.EqualTo($"game {replaced + 1 - n}"),
                    $"Kept copy {n} is not the {n}th most recently replaced game.");
            Assert.That(File.Exists(SaveStore.KeptPath(_save, SaveStore.KeptGameCount + 1)), Is.False,
                "A copy past the count was kept.");
            Assert.That(Directory.GetFiles(_dir), Has.Length.EqualTo(SaveStore.KeptGameCount + 1),
                "Besides the save and its kept copies, something else was left in the folder.");
        }

        [Test]
        public void KeepReplaced_LeavesEveryOtherFileBesideTheSaveAlone()
        {
            string[] others =
            {
                Path.Combine(_dir, "settings.json"),
                Path.Combine(_dir, "savegame.backup.json"),
                Path.Combine(_dir, "savegame.json.tmp"),
            };
            foreach (string other in others) File.WriteAllText(other, Path.GetFileName(other));
            File.WriteAllText(_save, "a game");

            for (int i = 0; i <= SaveStore.KeptGameCount; i++) SaveStore.KeepReplaced(_save);

            foreach (string other in others)
                Assert.That(File.ReadAllText(other), Is.EqualTo(Path.GetFileName(other)), $"{other} was touched.");
        }

        // ---- the service: what a New Game and the first write keep ------------------------------

        [Test]
        public void Open_ReadsTheGameThere_AndWritesThereFromThen()
        {
            WriteGame(_save, money: 4242);
            var service = OpenService(_save);

            Assert.That(service.SavePath, Is.EqualTo(_save));
            Assert.That(service.LoadedExistingSave, Is.True);
            Assert.That(service.Current.Money, Is.EqualTo(4242));

            service.Current.Money = 77;
            service.Save();
            Assert.That(SaveStore.Read(_save).Money, Is.EqualTo(77));
        }

        [Test]
        public void NewGame_OverAGame_KeepsTheReplacedGameWhole()
        {
            WriteGame(_save, money: 4242);
            byte[] game = File.ReadAllBytes(_save);
            var service = OpenService(_save);

            service.BeginNewGame();

            Assert.That(File.ReadAllBytes(SaveStore.KeptPath(_save, 1)), Is.EqualTo(game),
                "The New Game did not keep the game it erased, whole.");
            Assert.That(SaveStore.Read(_save).Money, Is.EqualTo(SaveMigration.NewGame().Money),
                "The save is not the new game.");
            Assert.That(service.LoadedExistingSave, Is.False);
        }

        [Test]
        public void NewGame_OverAnUnreadableSave_KeepsItWhole()
        {
            File.WriteAllBytes(_save, Unreadable);
            var service = OpenService(_save);
            Assert.That(service.LoadedExistingSave, Is.False, "The unreadable file was read as a game.");

            service.BeginNewGame();

            Assert.That(File.ReadAllBytes(SaveStore.KeptPath(_save, 1)), Is.EqualTo(Unreadable),
                "The New Game did not keep the unreadable save, whole.");
            Assert.That(SaveStore.Read(_save), Is.Not.Null, "The New Game was not written.");
            Assert.That(File.Exists(SaveStore.KeptPath(_save, 2)), Is.False, "The unreadable save was kept twice.");
        }

        [Test]
        public void AnUnreadableSave_IsKept_BeforeTheFirstWriteReplacesIt()
        {
            File.WriteAllBytes(_save, Unreadable);
            var service = OpenService(_save);

            service.Save();

            Assert.That(File.ReadAllBytes(SaveStore.KeptPath(_save, 1)), Is.EqualTo(Unreadable),
                "The first write replaced an unreadable save without keeping it.");
            Assert.That(SaveStore.Read(_save), Is.Not.Null, "The first write did not land.");

            service.Save();

            Assert.That(File.Exists(SaveStore.KeptPath(_save, 2)), Is.False,
                "A later write kept again: only the first write over the unreadable file should.");
            Assert.That(File.ReadAllBytes(SaveStore.KeptPath(_save, 1)), Is.EqualTo(Unreadable));
        }

        [Test]
        public void AnOrdinarySave_OverAGame_KeepsNothing()
        {
            WriteGame(_save, money: 4242);
            var service = OpenService(_save);

            service.Save();
            service.SetFlag("test.save_never_clobbered", true);

            Assert.That(Directory.GetFiles(_dir), Is.EqualTo(new[] { _save }),
                "An ordinary save kept a copy: only a New Game or an unreadable save should.");
        }

        [Test]
        public void NewGame_WithNothingOnDisk_KeepsNothing()
        {
            var service = OpenService(_save);

            service.BeginNewGame();

            Assert.That(Directory.GetFiles(_dir), Is.EqualTo(new[] { _save }),
                "A first New Game, with no game on disk, kept something.");
        }

        // ---- helpers -------------------------------------------------------------------------------

        private SaveService OpenService(string path)
        {
            var go = new GameObject(nameof(SaveNeverClobberedTests));
            _made.Add(go);
            var service = go.AddComponent<SaveService>();
            service.Open(path);
            return service;
        }

        private static void WriteGame(string path, int money)
        {
            var game = SaveMigration.NewGame();
            game.Money = money;
            game.WorldSeed = 12345;
            SaveStore.Write(game, path);
        }
    }
}
