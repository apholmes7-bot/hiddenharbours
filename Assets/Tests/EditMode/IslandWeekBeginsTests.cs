using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The island week begins when a new game is started, and at no other time.</b> The key the
    /// island's weather and the word at the end of the week stand on is written once, by the shell's
    /// New Game. Continue never writes it; an older save, however empty, never gets it; and nothing
    /// else a new game sets up, before or after, changes that. Only the host writes it.
    /// </summary>
    public class IslandWeekBeginsTests
    {
        private const string DoryOnSlip = "st_peters.dory_on_slip";

        private sealed class FakeSave : ISaveService
        {
            public bool Existing;
            public int NewGames;
            public string SetOnNewGame;
            public readonly Dictionary<string, int> Writes = new Dictionary<string, int>();
            public SaveData Current { get; set; } = new SaveData();
            public bool LoadedExistingSave => Existing;

            public void BeginNewGame()
            {
                NewGames++;
                Existing = false;
                Current = SaveMigration.NewGame();
                if (SetOnNewGame != null) SetFlag(SetOnNewGame, true);
            }

            public bool GetFlag(string key)
            {
                if (Current?.OnboardingFlags == null) return false;
                foreach (SaveFlag flag in Current.OnboardingFlags)
                    if (flag.Key == key) return flag.Value;
                return false;
            }

            public void SetFlag(string key, bool value)
            {
                Current.OnboardingFlags ??= new List<SaveFlag>();
                Writes.TryGetValue(key, out int n);
                Writes[key] = n + 1;
                for (int i = 0; i < Current.OnboardingFlags.Count; i++)
                    if (Current.OnboardingFlags[i].Key == key)
                    {
                        Current.OnboardingFlags[i] = new SaveFlag(key, value);
                        return;
                    }
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
            }

            public void Save() { }
        }

        private FakeSave _save;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            ShellFlow.Reset();
            IslandWeekStart.IsHost = null;
            _save = new FakeSave();
            GameServices.Save = _save;
        }

        [TearDown]
        public void TearDown()
        {
            IslandWeekStart.IsHost = null;
            ShellFlow.Reset();
            GameServices.Reset();
        }

        [Test]
        public void ANewGameFromTheShell_BeginsTheIslandWeek_Once()
        {
            ShellFlow.StartNewGame();

            Assert.AreEqual(1, _save.NewGames);
            Assert.IsTrue(_save.GetFlag(IslandWeekStart.FirstWeekKey));
            Assert.IsTrue(IslandWeekStart.Begun(_save));
            Assert.AreEqual(1, _save.Writes[IslandWeekStart.FirstWeekKey], "written once");

            Assert.IsFalse(IslandWeekStart.BeginForNewGame(_save), "asked again, nothing more");
            Assert.AreEqual(1, _save.Writes[IslandWeekStart.FirstWeekKey]);
        }

        [Test]
        public void Continue_NeverBeginsIt()
        {
            _save.Existing = true;
            ShellFlow.ContinueGame();

            Assert.IsFalse(_save.GetFlag(IslandWeekStart.FirstWeekKey));
            Assert.IsEmpty(_save.Writes, "a game carried on is not written to");
        }

        [Test]
        public void AnOlderSave_HoweverEmpty_NeverGetsIt()
        {
            // A save from before the week existed, with nothing in it at all, carried on with.
            _save.Existing = true;
            _save.Current = new SaveData();
            ShellFlow.ContinueGame();
            Assert.IsFalse(IslandWeekStart.Begun(_save), "empty is not new");

            // And one with a history.
            _save.Current.OnboardingFlags.Add(new SaveFlag(OnboardingFlags.LandedCatchKey, true));
            _save.Current.OwnedLicenses.Add("license.cod");
            ShellFlow.ContinueGame();
            Assert.IsFalse(IslandWeekStart.Begun(_save));
            Assert.IsEmpty(_save.Writes);
        }

        [Test]
        public void WhatElseANewGameSetsUp_BeforeOrAfter_DoesNotStopTheWeek()
        {
            // Set by the new game itself, before the week begins.
            _save.SetOnNewGame = DoryOnSlip;
            ShellFlow.StartNewGame();
            Assert.IsTrue(_save.GetFlag(DoryOnSlip));
            Assert.IsTrue(_save.GetFlag(IslandWeekStart.FirstWeekKey), "another flag set first changes nothing");

            // Set by hand before the act, on a save with a licence and a mended boat for good measure.
            var other = new FakeSave();
            other.SetFlag(DoryOnSlip, true);
            other.Current.OwnedLicenses.Add("license.clam");
            other.Current.RepairedBoats.Add("boat.dory");
            Assert.IsTrue(IslandWeekStart.BeginForNewGame(other));
            Assert.IsTrue(other.GetFlag(IslandWeekStart.FirstWeekKey));

            // And the other order: the week first, the other flag after.
            var third = new FakeSave();
            Assert.IsTrue(IslandWeekStart.BeginForNewGame(third));
            third.SetFlag(DoryOnSlip, true);
            Assert.IsTrue(third.GetFlag(IslandWeekStart.FirstWeekKey));
            Assert.IsTrue(third.GetFlag(DoryOnSlip));
            Assert.AreEqual(1, third.Writes[IslandWeekStart.FirstWeekKey]);
        }

        [Test]
        public void ASecondNewGame_BeginsItsOwnWeek()
        {
            ShellFlow.StartNewGame();
            ShellFlow.StartNewGame();
            Assert.AreEqual(2, _save.NewGames);
            Assert.IsTrue(_save.GetFlag(IslandWeekStart.FirstWeekKey));
            Assert.AreEqual(2, _save.Writes[IslandWeekStart.FirstWeekKey], "once for each game started");
        }

        [Test]
        public void OnlyTheHostWrites_AndAGuestJoiningBeginsNothing()
        {
            // A guest's machine: nothing is written, whatever is asked of it.
            IslandWeekStart.IsHost = () => false;
            Assert.IsFalse(IslandWeekStart.BeginForNewGame(_save));
            Assert.IsEmpty(_save.Writes, "a guest writes nothing");

            // The host begins the week.
            IslandWeekStart.IsHost = () => true;
            Assert.IsTrue(IslandWeekStart.BeginForNewGame(_save));
            int flags = _save.Current.OnboardingFlags.Count;

            // A guest joins the game under way: the host's week is neither begun again nor restarted.
            IslandWeekStart.IsHost = () => false;
            Assert.IsFalse(IslandWeekStart.BeginForNewGame(_save));
            IslandWeekStart.IsHost = () => true;
            Assert.IsFalse(IslandWeekStart.BeginForNewGame(_save), "the host's week is already under way");
            Assert.AreEqual(1, _save.Writes[IslandWeekStart.FirstWeekKey]);
            Assert.AreEqual(flags, _save.Current.OnboardingFlags.Count);
            Assert.IsTrue(IslandWeekStart.Begun(_save));
        }

        [Test]
        public void WithNoSave_NothingHappens()
        {
            Assert.IsFalse(IslandWeekStart.BeginForNewGame(null));
            Assert.IsFalse(IslandWeekStart.Begun(null));
        }

        [Test]
        public void TheKeyIsAFlagInTheListTheSaveAlreadyHas()
        {
            ShellFlow.StartNewGame();
            Assert.AreEqual(SaveMigration.CurrentVersion, _save.Current.SchemaVersion);
            Assert.AreEqual(OnboardingFlags.FirstWeekKey, IslandWeekStart.FirstWeekKey, "one key, one definition");
        }

        [Test]
        public void TheKeyIsOneTheFlagRegistryKnows_AndTheShippedWallsStandOnIt()
        {
            CollectionAssert.Contains(QuestContentValidationTests.SettableFlags(), OnboardingFlags.FirstWeekKey);
            CampaignBarrierDef[] walls = Resources.LoadAll<CampaignBarrierDef>(CampaignBarrierDirector.ResourcesFolder);
            int standing = 0;
            foreach (CampaignBarrierDef wall in walls)
                if (wall.StandsWhileFlag == OnboardingFlags.FirstWeekKey) standing++;
            Assert.AreEqual(4, standing, "the island's four weather walls stand on the key a new game is given");

            // What a wall stands on is built. What lifts them is the morning after the word, which is not.
            CollectionAssert.DoesNotContain(CampaignBarrierContentValidationTests.NotYetSettable.Keys,
                OnboardingFlags.FirstWeekKey, "the key a wall stands on is set by a new game");
            CollectionAssert.AreEquivalent(new[] { "st_peters.weather_turned" },
                CampaignBarrierContentValidationTests.NotYetSettable.Keys);
        }
    }
}
