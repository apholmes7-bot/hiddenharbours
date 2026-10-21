using System;
using System.IO;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// No test touches the player's save. Drives the REAL self-installed <see cref="SaveService"/>, the one
    /// its bootstrap made for this play session, through every path that writes: a flag, an active boat, the
    /// app being paused, the quit, and a New Game. Each case proves the write landed in the run's own save
    /// (<see cref="TestRunSave"/>) and that the player's file stayed exactly as it was: the same bytes and
    /// mtime where it exists (a GPU box), still absent where it does not (CI).
    ///
    /// <para>The player's path is computed here from <see cref="Application.persistentDataPath"/> and the
    /// literal file name, never through <see cref="SaveStore"/> or the service, which are what is under
    /// test. Its bytes are only ever read, with sharing that never blocks the game's own writer. Not
    /// GPU-gated: CI is where it must hold first.</para>
    ///
    /// <para>Nothing is driven until <see cref="SetUp"/> has seen the live service on the run's save: a run
    /// started in an editor already in play mode opened the player's save before the runner's setup, and
    /// there every case fails without writing.</para>
    ///
    /// <para>Each case drives a copy of the service's <see cref="SaveService.Current"/> and puts the
    /// original back afterwards, with the service's other state and the shell's phase, so the rest of the
    /// run sees the service as it was. The boat is driven through the service's own handler (a message to
    /// it) rather than the bus, so the camera and the mix do not react to a boat no test is sailing.</para>
    /// </summary>
    public class SaveNeverClobberedPlayTests
    {
        private const string ProbeHull = "boat.dory";

        private SaveService _service;
        private string _runsSave;

        private string _playersSave;
        private bool _playersSaveExisted;
        private byte[] _playersSaveBytes;
        private DateTime _playersSaveMtime;

        private bool _driven;
        private SaveData _current;
        private bool _loadedExistingSave;
        private ShellPhase _phase;

        [SetUp]
        public void SetUp()
        {
            _driven = false;
            _playersSave = Path.Combine(Application.persistentDataPath, "savegame.json");

            _runsSave = System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable);
            Assert.That(_runsSave, Is.Not.Null.And.Not.Empty,
                "The runner's setup gave this run no save of its own (TestRunSave did not run).");
            Assert.That(Path.GetFullPath(_runsSave), Is.Not.EqualTo(Path.GetFullPath(_playersSave)),
                "This run's save IS the player's.");

            _service = Object.FindAnyObjectByType<SaveService>();
            Assert.That(_service, Is.Not.Null, "No live SaveService: its bootstrap did not run in this session.");
            Assert.That(_service.SavePath, Is.EqualTo(_runsSave),
                "The live service is not on this run's save, so it opened before the runner's setup (a run " +
                "started in play mode?). Refusing to drive a write that would land in " + _service.SavePath);

            _playersSaveExisted = File.Exists(_playersSave);
            if (_playersSaveExisted)
            {
                _playersSaveBytes = ReadShared(_playersSave);
                _playersSaveMtime = File.GetLastWriteTimeUtc(_playersSave);
            }

            _current = _service.Current;
            _loadedExistingSave = _service.LoadedExistingSave;
            _phase = ShellFlow.Phase;
            _driven = true;

            SetCurrent(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(_current)));
            if (ShellFlow.AtTitle) SetPhase(ShellPhase.Playing);   // a write at the title is refused by design
            if (File.Exists(_runsSave)) File.Delete(_runsSave);   // so each drive must write it again
        }

        [TearDown]
        public void TearDown()
        {
            if (!_driven) return;
            SetCurrent(_current);
            SetLoadedExistingSave(_loadedExistingSave);
            SetPhase(_phase);
        }

        [Test]
        public void TheLiveService_OpenedThisRunsSave_InTheProjectsTemp()
        {
            string run = Path.GetDirectoryName(Path.GetFullPath(_runsSave));
            Assert.That(Path.GetDirectoryName(run), Is.EqualTo(TestRunSave.RunsRoot),
                "The run's save is not in <project>/Temp/TestRunSaves/<run>/.");
            Assert.That(Path.GetFileName(_runsSave), Is.EqualTo("savegame.json"));
            Assert.That(SaveStore.ActivePath, Is.EqualTo(_runsSave),
                "SaveStore.ActivePath does not name the run's save while the run is going.");
        }

        [Test]
        public void AFlag_WritesTheRunsSave_NotThePlayers()
        {
            _service.SetFlag("test.save_never_clobbered." + Guid.NewGuid().ToString("N"), true);
            AssertWroteTheRunsSave("A flag");
            AssertThePlayersSaveUntouched("A flag");
        }

        [Test]
        public void AnActiveBoat_WritesTheRunsSave_NotThePlayers()
        {
            _service.SendMessage("OnActiveBoatChanged", new ActiveBoatChanged(ProbeHull, 0f));
            Assert.That(_service.Current.ActiveHullId, Is.EqualTo(ProbeHull), "The boat handler did not run.");
            AssertWroteTheRunsSave("An active boat");
            AssertThePlayersSaveUntouched("An active boat");
        }

        [Test]
        public void ThePause_WritesTheRunsSave_NotThePlayers()
        {
            _service.SendMessage("OnApplicationPause", true);
            AssertWroteTheRunsSave("OnApplicationPause(true)");
            AssertThePlayersSaveUntouched("OnApplicationPause(true)");
        }

        [Test]
        public void TheQuit_WritesTheRunsSave_NotThePlayers()
        {
            // The same handler the runner's exit from play mode calls after the last test, on the same
            // service and so the same file: the path is fixed when the service opens, not when it writes.
            _service.SendMessage("OnApplicationQuit");
            AssertWroteTheRunsSave("OnApplicationQuit");
            AssertThePlayersSaveUntouched("OnApplicationQuit");
        }

        [Test]
        public void ANewGame_KeepsTheRunsGame_NotThePlayers()
        {
            string mark = "test.save_never_clobbered." + Guid.NewGuid().ToString("N");
            _service.SetFlag(mark, true);                       // a game on the run's save that a new one is not
            byte[] game = File.ReadAllBytes(_runsSave);

            _service.BeginNewGame();

            string kept = SaveStore.KeptPath(_runsSave, 1);
            Assert.That(File.Exists(kept), Is.True, "The New Game kept nothing beside the run's save.");
            Assert.That(File.ReadAllBytes(kept), Is.EqualTo(game), "The kept copy is not the game it replaced, whole.");
            AssertWroteTheRunsSave("A New Game");
            StringAssert.DoesNotContain(mark, File.ReadAllText(_runsSave), "The New Game did not replace the game.");
            AssertThePlayersSaveUntouched("A New Game");
        }

        // ---- helpers ---------------------------------------------------------------------------

        private void AssertWroteTheRunsSave(string path)
        {
            Assert.That(File.Exists(_runsSave), Is.True,
                $"{path} did not write the run's save, so the case proves nothing about where writes go.");
        }

        private void AssertThePlayersSaveUntouched(string path)
        {
            if (!_playersSaveExisted)
            {
                Assert.That(File.Exists(_playersSave), Is.False, $"{path} created the player's save at {_playersSave}.");
                return;
            }

            Assert.That(File.Exists(_playersSave), Is.True, $"{path} removed the player's save at {_playersSave}.");
            Assert.That(File.GetLastWriteTimeUtc(_playersSave), Is.EqualTo(_playersSaveMtime),
                $"{path} wrote the player's save at {_playersSave}.");
            Assert.That(ReadShared(_playersSave), Is.EqualTo(_playersSaveBytes),
                $"{path} changed the player's save at {_playersSave}.");
        }

        private static byte[] ReadShared(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            return bytes.ToArray();
        }

        private void SetCurrent(SaveData data) =>
            typeof(SaveService).GetProperty(nameof(SaveService.Current)).SetValue(_service, data);

        private void SetLoadedExistingSave(bool value) =>
            typeof(SaveService).GetProperty(nameof(SaveService.LoadedExistingSave)).SetValue(_service, value);

        private static void SetPhase(ShellPhase phase) =>
            typeof(ShellFlow).GetProperty(nameof(ShellFlow.Phase)).SetValue(null, phase);
    }
}
