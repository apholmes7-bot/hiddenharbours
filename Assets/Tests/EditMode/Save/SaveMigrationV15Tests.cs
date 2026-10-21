using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// SAVE v15 — the player's own build (character PR 2b, ADR 0051): one <see cref="CharacterRecipe"/>
    /// of rig keys, empty for the body as baked.
    ///
    /// <para><b>⭐ The empty recipe IS the migration.</b> A v14 player was drawn from the baked body,
    /// because there was no recipe to draw from, and an empty recipe draws exactly that. The wardrobe's
    /// id is RESOLVED, not moved: it recolours the sprite layer, which the mesh never drew, so copying it
    /// into the recipe would change how an old save looks rather than keep it.</para>
    ///
    /// <para>Every file here is the test's own temp file, opened by a real <see cref="SaveService"/>
    /// (which does not awake in edit mode, so it never opens the player's save).</para>
    /// </summary>
    public class SaveMigrationV15Tests
    {
        private string _dir;
        private string _save;
        private readonly List<GameObject> _made = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hh_save_v15_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _save = Path.Combine(_dir, SaveStore.DefaultFileName);
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
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        // ---- the migration itself ---------------------------------------------------------------

        [Test]
        public void CurrentVersion_IsAtLeastFifteen_BecauseTheRecipeShipped()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 15,
                "the player's recipe is saved state (it is what they chose), so the schema carries it and " +
                "the version says so");
        }

        [Test]
        public void AV14Save_MigratesToCurrent_AndGainsAnEmptyRecipe()
        {
            var data = SaveMigration.Migrate(new SaveData { SchemaVersion = 14, PlayerRecipe = null });

            Assert.AreEqual(SaveMigration.CurrentVersion, data.SchemaVersion);
            Assert.IsNotNull(data.PlayerRecipe);
            Assert.IsTrue(data.PlayerRecipe.IsEmpty,
                "⭐ the empty recipe IS the migration: it draws the body as baked, which is what a v14 " +
                "player looked like");
        }

        [Test]
        public void TheWornOutfit_IsResolvedWhereItIs_NotMovedIntoTheRecipe()
        {
            var data = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = 14,
                WornOutfitId = "outfit.harbour_teal",
                PlayerRecipe = null,
            });

            Assert.AreEqual("outfit.harbour_teal", data.WornOutfitId,
                "the wardrobe's id still recolours the sprite layer; v15 leaves it exactly where it was");
            Assert.IsTrue(data.PlayerRecipe.IsEmpty,
                "copying the outfit into the recipe would recolour a mesh that never wore it: the old save " +
                "would change its look on load");
            Assert.AreEqual("", data.PlayerRecipe.Outfit);
            Assert.AreEqual("", data.PlayerRecipe.Shirt);
        }

        [Test]
        public void TheMigration_ReinterpretsNoExistingValue()
        {
            var data = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = 14,
                Money = 417,
                WornOutfitId = "outfit.oilskins",
                RestRegion = "region.st_peters",
                HullFuel = new List<HullFuelDto> { new HullFuelDto("boat.dory_outboard", 3.5f) },
            });

            Assert.AreEqual(417, data.Money);
            Assert.AreEqual("outfit.oilskins", data.WornOutfitId);
            Assert.AreEqual("region.st_peters", data.RestRegion);
            Assert.AreEqual(1, data.HullFuel.Count);
            Assert.AreEqual(3.5f, data.HullFuel[0].Litres, 1e-4f);
        }

        [Test]
        public void MigratingTwice_ChangesNothingTheSecondTime()
        {
            var first = SaveMigration.Migrate(new SaveData { SchemaVersion = 14 });
            first.PlayerRecipe = Ginny();

            var second = SaveMigration.Migrate(first);

            Assert.AreEqual(SaveMigration.CurrentVersion, second.SchemaVersion);
            Assert.IsTrue(second.PlayerRecipe.SameAs(Ginny()),
                $"idempotent: a re-run must not wipe or rewrite the recipe (got {second.PlayerRecipe})");
        }

        [Test]
        public void ANullRecipe_AndItsNullFields_AreRepaired_NotThrownOn()
        {
            // A hand-edited or partial JSON can omit the field, or hold a null inside it.
            var missing = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = SaveMigration.CurrentVersion,
                PlayerRecipe = null,
            });
            Assert.IsNotNull(missing.PlayerRecipe);
            Assert.IsTrue(missing.PlayerRecipe.IsEmpty);

            var holed = Ginny();
            holed.Shirt = null;
            holed.FitId = null;
            var data = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = SaveMigration.CurrentVersion,
                PlayerRecipe = holed,
            });
            Assert.AreEqual("", data.PlayerRecipe.Shirt, "a null field reads as 'the preset's own'");
            Assert.AreEqual("", data.PlayerRecipe.FitId);
            Assert.AreEqual("tan", data.PlayerRecipe.Skin, "and a set field is never touched");
            Assert.AreEqual("char_build.ginny", data.PlayerRecipe.BuildId);
        }

        [Test]
        public void ANewGame_StartsWithAnEmptyRecipe()
        {
            SaveData game = SaveMigration.NewGame();

            Assert.IsNotNull(game.PlayerRecipe);
            Assert.IsTrue(game.PlayerRecipe.IsEmpty, "a new game draws the body as baked");
            Assert.AreEqual(CharacterRecipe.Version, game.PlayerRecipe.SchemaVersion);
        }

        // ---- against a real v14 file -------------------------------------------------------------

        [Test]
        public void ARealV14File_OpensAtCurrent_KeepsItsLook_AndWritesTheRecipeBack()
        {
            var v14 = SaveMigration.NewGame();
            v14.Money = 417;
            v14.WorldSeed = 12345;
            v14.WornOutfitId = "outfit.harbour_teal";
            v14.RestRegion = "region.st_peters";
            File.WriteAllText(_save, V14Json(v14));

            SaveService service = OpenService(_save);

            Assert.IsTrue(service.LoadedExistingSave, "harness: the v14 file did not read as a save");
            Assert.AreEqual(SaveMigration.CurrentVersion, service.Current.SchemaVersion);
            Assert.AreEqual(417, service.Current.Money);
            Assert.AreEqual(12345, service.Current.WorldSeed);
            Assert.AreEqual("outfit.harbour_teal", service.Current.WornOutfitId);
            Assert.AreEqual("region.st_peters", service.Current.RestRegion);
            Assert.IsNotNull(service.Current.PlayerRecipe);
            Assert.IsTrue(service.Current.PlayerRecipe.IsEmpty, "the old save keeps its look: the body as baked");

            service.Save();

            StringAssert.Contains("\"PlayerRecipe\"", File.ReadAllText(_save), "the first write did not add the recipe");
            SaveData reread = SaveStore.Read(_save);
            Assert.AreEqual(SaveMigration.CurrentVersion, reread.SchemaVersion);
            Assert.AreEqual("outfit.harbour_teal", reread.WornOutfitId);
            Assert.IsTrue(reread.PlayerRecipe.IsEmpty);
        }

        [Test]
        public void ARecipe_RoundTripsThroughTheFile()
        {
            SaveService service = OpenService(_save);
            service.Current.PlayerRecipe = Ginny();
            service.Save();

            SaveData reread = SaveStore.Read(_save);

            Assert.IsTrue(reread.PlayerRecipe.SameAs(Ginny()), $"got {reread.PlayerRecipe}");
            Assert.AreEqual(CharacterRecipe.Version, reread.PlayerRecipe.SchemaVersion);
        }

        // ---- helpers -----------------------------------------------------------------------------

        private static CharacterRecipe Ginny() => new CharacterRecipe
        {
            BuildId = "char_build.ginny",
            FitId = "fit.character_v10",
            Skin = "tan",
            Outfit = "navy",
            Shirt = "white",
        };

        /// <summary>
        /// The file a v14 build wrote for <paramref name="data"/>: the current blob, stamped 14, less the
        /// one field v15 appends. It is the last field <see cref="SaveData"/> declares, so it is the
        /// last one JsonUtility writes, and cutting it leaves exactly the v14 layout.
        /// </summary>
        private static string V14Json(SaveData data)
        {
            data.SchemaVersion = 14;
            string json = SaveSerialization.ToJson(data);
            int at = json.IndexOf("\"PlayerRecipe\"", StringComparison.Ordinal);
            Assert.Greater(at, 0, "harness: the blob carries no PlayerRecipe to take away");
            int comma = json.LastIndexOf(',', at);
            Assert.Greater(comma, 0, "harness: PlayerRecipe is not after another field");
            string v14 = json.Substring(0, comma) + "\n}\n";
            StringAssert.DoesNotContain("PlayerRecipe", v14, "harness: PlayerRecipe is not the last field");
            StringAssert.Contains("\"SchemaVersion\": 14", v14);
            return v14;
        }

        private SaveService OpenService(string path)
        {
            var go = new GameObject(nameof(SaveMigrationV15Tests));
            _made.Add(go);
            var service = go.AddComponent<SaveService>();
            service.Open(path);
            return service;
        }
    }
}
