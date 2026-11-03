#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.Player;
using HiddenHarbours.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public class CharacterCreatorOpeningPlayTests
    {
        string _path, _previousPath;
        SaveService _saveService;
        int _lines;
        bool _lineWhileOpen;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "hh_creator_opening_" + Guid.NewGuid().ToString("N"), "fixture.json");
            _previousPath = System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable);
            Assert.IsNotEmpty(_previousPath, "the test runner must redirect saves before the fixture starts");
            System.Environment.SetEnvironmentVariable(SaveStore.TestRunSaveVariable, _path);
            _saveService = Object.FindAnyObjectByType<SaveService>();
            Assert.IsNotNull(_saveService, "the running game's save service must be available");
            _saveService.Open(_path);
            GameServices.Save = _saveService;
            _lines = 0;
            _lineWhileOpen = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EventBus.Unsubscribe<ArrivalCompleted>(OnLine);
            ShellPause.Resume();
            ShellRestart.DestroyPersistentCore();
            yield return null;
            var empty = SceneManager.GetSceneByName("AfterCreatorOpening");
            if (!empty.IsValid() || !empty.isLoaded) empty = SceneManager.CreateScene("AfterCreatorOpening");
            SceneManager.SetActiveScene(empty);
            foreach (string name in new[] { "EastWater", "StPeters" })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            GameServices.Reset();
            ShellFlow.Reset();
            System.Environment.SetEnvironmentVariable(SaveStore.TestRunSaveVariable, _previousPath);
            if (_saveService != null && !string.IsNullOrEmpty(_previousPath))
            {
                _saveService.Open(_previousPath);
                GameServices.Save = _saveService;
            }
        }

        IEnumerator Boot(bool newGame)
        {
            if (!newGame) _saveService.Open(_path);
            ShellRestart.DestroyPersistentCore();
            yield return null;
            yield return SceneManager.LoadSceneAsync("StPeters", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Assert.IsTrue(ShellFlow.AtTitle);
            if (newGame) ShellFlow.StartNewGame();
            else ShellFlow.ContinueGame();
            double end = Time.realtimeSinceStartupAsDouble + 60d;
            while (CharacterCreatorOpener.Model == null || !CharacterCreatorOpener.Model.IsOpen)
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble, end, "the opening did not show its creator");
                yield return null;
            }
            EventBus.Subscribe<ArrivalCompleted>(OnLine);
        }

        void OnLine(ArrivalCompleted line)
        {
            _lines++;
            _lineWhileOpen |= CharacterCreatorOpener.Model != null && CharacterCreatorOpener.Model.IsOpen;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator NewGameCommit_ChangesTheLivePlayer_PersistsAndResumesBeforeTheCaptain()
        {
            yield return Boot(true);
            var opening = ArrivalOpening.SeaJourney;
            var creator = CharacterCreatorOpener.Model;
            var clock = (GameClock)GameServices.Clock;
            Assert.IsTrue(opening.IsAtSea);
            Assert.IsTrue(clock.IsHeld);
            Assert.AreEqual(0, _lines);
            Assert.IsTrue(InteractionGate.IsBlocked);
            Assert.IsTrue(MoveActionClaim.IsClaimed);
            var held = new HeldDeckIntents();
            opening.ConfigureWalkInput(held);
            Vector2 deck = opening.DeckLocalPosition;
            Vector2 sole = opening.CabinLocalPosition;
            bool below = opening.IsBelowDecks;
            held.Walk(Vector2.right);
            for (int i = 0; i < 12; i++) yield return null;
            held.Release();
            Assert.AreEqual(deck, opening.DeckLocalPosition, "name navigation must not walk the passenger");
            Assert.AreEqual(sole, opening.CabinLocalPosition);
            Assert.AreEqual(below, opening.IsBelowDecks);

            var body = opening.Boat.GetComponent<Rigidbody2D>();
            Vector2 position = body.position, velocity = body.linearVelocity;
            float rotation = body.rotation, angular = body.angularVelocity;
            Assert.IsTrue(creator.SelectBody("char_build.ginny"), creator.Refusal);
            Assert.IsTrue(creator.SelectColour("skin", "tan"), creator.Refusal);
            Assert.IsTrue(creator.SelectColour("hair", "black"), creator.Refusal);
            Assert.IsTrue(creator.SelectColour("outfit", "teal"), creator.Refusal);
            Assert.IsTrue(creator.SelectOutfit("outfit.harbour_teal"), creator.Refusal);
            Assert.IsTrue(creator.SetName("  Skipper  "), creator.Refusal);
            Assert.AreEqual(position, body.position);
            Assert.AreEqual(velocity, body.linearVelocity);
            Assert.AreEqual(rotation, body.rotation);
            Assert.AreEqual(angular, body.angularVelocity);
            Assert.AreEqual(deck, opening.DeckLocalPosition);
            Assert.IsTrue(GameServices.Appearance.IsPreviewing);
            Assert.IsTrue(GameServices.Save.Current.PlayerRecipe.IsEmpty);
            var presenter = GameServices.PlayerTransform.GetComponentInChildren<DeckRiderMeshPresenter>();
            Assert.IsNotNull(presenter);
            Assert.IsTrue(presenter.enabled);
            yield return null;
            yield return null;
            Assert.IsTrue(presenter.DrawsInsteadOfSprite, presenter.NotDrawingReason);
            Assert.IsNotNull(presenter.Skin);
            Assert.AreEqual("charskin.ginny", presenter.Skin.Id);

            double productHour = clock.TotalSeconds;
            // Fixture clock only: the opening's authored hour and the forthcoming sun model are untouched.
            clock.SeekTo(clock.Config.SecondsPerDay * (7.5d / 24d));
            yield return new WaitForSecondsRealtime(1.1f); // allow the existing throttled tint to refresh
            yield return Plate("creator-0730-current-sun.png");
            clock.SeekTo(clock.Config.SecondsPerDay * (12d / 24d));
            yield return new WaitForSecondsRealtime(1.1f);
            yield return Plate("creator-daylight-on-deck.png");
            clock.SeekTo(clock.Config.SecondsPerDay * (7.5d / 24d));
            yield return new WaitForSecondsRealtime(1.1f);
            yield return Plate("creator-before-close.png");
            var camera = Camera.main;
            Vector3 lens = camera.transform.position;
            float size = camera.orthographicSize;
            Color ambient = RenderSettings.ambientLight;
            Assert.IsTrue(creator.Commit(), creator.Refusal);
            Assert.AreEqual(lens, camera.transform.position);
            Assert.AreEqual(size, camera.orthographicSize);
            Assert.AreEqual(ambient, RenderSettings.ambientLight);
            Assert.IsTrue(clock.IsHeld, "closing the creator must not release the opening's hour hold");
            Assert.IsFalse(InteractionGate.IsBlocked);
            Assert.IsFalse(MoveActionClaim.IsClaimed);
            yield return null;
            yield return Plate("creator-after-close.png");
            clock.SeekTo(productHour);
            SaveData persisted = SaveStore.Read(_path);
            Assert.AreEqual("char_build.ginny", persisted.PlayerRecipe.BuildId);
            Assert.AreEqual("tan", persisted.PlayerRecipe.Skin);
            Assert.AreEqual("outfit.harbour_teal", persisted.WornOutfitId);
            Assert.AreEqual("Skipper", persisted.PlayerName);
            Assert.AreEqual(0, _lines);

            // Continue an unfinished introduction through the real shell and the same owned fixture file.
            EventBus.Unsubscribe<ArrivalCompleted>(OnLine);
            yield return Boot(false);
            creator = CharacterCreatorOpener.Model;
            Assert.AreEqual("char_build.ginny", creator.Recipe.BuildId);
            Assert.AreEqual("outfit.harbour_teal", creator.OutfitId);
            Assert.AreEqual("Skipper", creator.Name);
            creator.Cancel();
            Assert.AreEqual("Skipper", GameServices.Save.Current.PlayerName);
            yield return FinishArrival();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator NewGameCancel_RestoresBakedLookWornOutfitAndNoName_ThenTheCaptain()
        {
            yield return Boot(true);
            var creator = CharacterCreatorOpener.Model;
            string worn = OutfitLocker.WornOutfitId();
            Assert.IsTrue(creator.SelectBody("char_build.ginny"));
            Assert.IsTrue(creator.SelectColour("hair", "black"));
            Assert.IsTrue(creator.SelectOutfit("outfit.spruce"));
            Assert.IsTrue(creator.SetName("Skipper"));
            Assert.AreEqual(0, _lines);
            creator.Cancel();
            Assert.IsTrue(GameServices.Appearance.Committed.IsEmpty);
            Assert.IsTrue(GameServices.Appearance.Showing.IsEmpty);
            Assert.AreEqual(worn, OutfitLocker.WornOutfitId());
            Assert.AreEqual("", PlayerName.Of(GameServices.Save.Current));
            Assert.IsTrue(GameServices.Clock.IsHeld);
            SaveData persisted = SaveStore.Read(_path);
            Assert.IsTrue(persisted.PlayerRecipe.IsEmpty);
            Assert.AreEqual(worn, persisted.WornOutfitId);
            Assert.AreEqual("", persisted.PlayerName);
            yield return FinishArrival();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SaveFromBeforeTheCreator_ContinuesWithoutAnOpeningOrAppearanceWrite()
        {
            ShellRestart.DestroyPersistentCore();
            yield return null;
            yield return SceneManager.LoadSceneAsync("StPeters", LoadSceneMode.Single);
            yield return null;
            yield return null;
            // A prior save has already received the base's four one-time grants. Keep this fixture's
            // kit explicit so Continue must preserve the entire snapshot without another grant or write.
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(GameServices.Save.Current));
            loaded.OwnedGear = new List<string> { "gear.shovel", "gear.bucket" };
            loaded.BaitStock = new List<BaitStock> { new BaitStock("bait.herring", 6) };
            loaded.PotStock = new List<PotStock> { new PotStock("trap.lobster", 2), new PotStock("trap.crab", 1) };
            loaded.Money = 20;
            loaded.OnboardingFlags.Add(new SaveFlag("st_peters_starting_gear_granted", true));
            loaded.OnboardingFlags.Add(new SaveFlag("trap_starting_bait_granted", true));
            loaded.OnboardingFlags.Add(new SaveFlag("pot_starter_kit_granted", true));
            loaded.OnboardingFlags.Add(new SaveFlag("ginny_fronted_clam_fee", true));
            loaded.SchemaVersion = 16;
            loaded.WornOutfitId = "outfit.spruce";
            loaded.OnboardingFlags.RemoveAll(flag => flag.Key == ArrivalOpening.ArrivedFlagKey);
            loaded.OnboardingFlags.Add(new SaveFlag(ArrivalOpening.ArrivedFlagKey, true));
            SaveMigration.Migrate(loaded);
            var save = new PriorSave(loaded);
            GameServices.Save = save;
            string before = SaveSerialization.ToJson(loaded);
            ShellFlow.ContinueGame();
            for (int i = 0; i < 12; i++) yield return null;
            Assert.IsTrue(CharacterCreatorOpener.Model == null || !CharacterCreatorOpener.Model.IsOpen);
            Assert.AreEqual(before, SaveSerialization.ToJson(save.Current));
            Assert.AreEqual(0, save.Writes);
            Assert.IsTrue(save.Current.PlayerRecipe.IsEmpty);
            Assert.AreEqual("", save.Current.PlayerName);
        }

        IEnumerator FinishArrival()
        {
            double end = Time.realtimeSinceStartupAsDouble + 240d;
            while (ArrivalOpening.SeaJourney == null || !ArrivalOpening.SeaJourney.CanStepAshore)
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble, end, "the existing arrival did not offer its step ashore");
                Assert.IsTrue(GameServices.Clock.IsHeld);
                yield return null;
            }
            Assert.AreEqual(0, _lines);
            var opening = ArrivalOpening.SeaJourney;
            Assert.IsTrue(opening.StepAshore());
            while (opening.Current != ArrivalOpening.Phase.HandedOver)
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble, end);
                yield return null;
            }
            Assert.AreEqual(1, _lines);
            Assert.IsFalse(_lineWhileOpen);
            Assert.IsFalse(GameServices.Clock.IsHeld);
        }

        IEnumerator Plate(string file)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-creatorPlates");
            if (index < 0 || index + 1 >= args.Length) yield break;
            string path = Path.Combine(args[index + 1], file);
            Directory.CreateDirectory(args[index + 1]);
            Camera camera = Camera.main;
            Assert.IsNotNull(camera);
            var previous = camera.targetTexture;
            int width = camera.pixelWidth, height = camera.pixelHeight;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            bool done = false;
            Exception error = null;
            void Rendered(ScriptableRenderContext context, Camera rendered)
            {
                if (rendered != camera || done) return;
                done = true;
                RenderTexture active = RenderTexture.active;
                var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                    var opening = ArrivalOpening.SeaJourney;
                    var read = new PlateRead { Hour = GameServices.Clock.HourOfDay, Held = GameServices.Clock.IsHeld,
                        Player = GameServices.PlayerTransform.position, Boat = opening.Boat.transform.position,
                        Lens = camera.transform.position, Size = camera.orthographicSize,
                        Recipe = GameServices.Appearance.Showing, Outfit = CharacterCreatorOpener.Model.OutfitId };
                    File.WriteAllText(path + ".json", JsonUtility.ToJson(read, true));
                }
                catch (Exception exception) { error = exception; }
                finally { RenderTexture.active = active; Object.Destroy(pixels); }
            }
            try
            {
                camera.targetTexture = target;
                RenderPipelineManager.endCameraRendering += Rendered;
                double end = Time.realtimeSinceStartupAsDouble + 10d;
                while (!done) { Assert.Less(Time.realtimeSinceStartupAsDouble, end); yield return null; }
                Assert.IsNull(error, error?.ToString());
            }
            finally
            {
                RenderPipelineManager.endCameraRendering -= Rendered;
                if (camera != null) camera.targetTexture = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        [Serializable]
        sealed class PlateRead
        {
            public float Hour, Size;
            public bool Held;
            public Vector3 Player, Boat, Lens;
            public CharacterRecipe Recipe;
            public string Outfit;
        }

        sealed class PriorSave : ISaveService
        {
            public SaveData Current { get; }
            public bool LoadedExistingSave => true;
            public int Writes;
            public PriorSave(SaveData current) { Current = current; }
            public bool GetFlag(string key)
            {
                foreach (SaveFlag flag in Current.OnboardingFlags) if (flag.Key == key) return flag.Value;
                return false;
            }
            public void SetFlag(string key, bool value) { Writes++; }
            public void Save() => Writes++;
        }
    }
}
#endif
