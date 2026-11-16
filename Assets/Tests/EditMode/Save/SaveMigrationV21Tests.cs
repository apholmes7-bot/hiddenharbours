using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Save schema v21: the body and the messes (body slice 1). An older save loads as the default
    /// body at its lean mass (today's player exactly) with no messes.</summary>
    public class SaveMigrationV21Tests
    {
        [Test]
        public void CurrentVersion_IsAtLeastTwentyOne_BecauseTheBodyShipped()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 21);
        }

        [Test]
        public void Version20Json_WithoutABody_LoadsTheDefaultBody_AndKeepsTheRest()
        {
            SaveData save = SaveSerialization.FromJson(
                "{\"SchemaVersion\":20,\"GameTimeSeconds\":5000.0,\"PlayerName\":\"Skipper\"," +
                "\"PlayerNeeds\":{\"Hunger\":{\"Level\":0.25,\"SinceGameSeconds\":4000.0}}}");
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsNotNull(save.PlayerBody);
            Assert.AreEqual(PlayerBodyDto.Unset, save.PlayerBody.Frame, "unset frame = the config default");
            Assert.AreEqual(PlayerBodyDto.Unset, save.PlayerBody.HeightScale);
            Assert.AreEqual((double)PlayerBodyDto.Unset, save.PlayerBody.MassKg, 0d, "unset mass = lean");
            Assert.IsFalse(save.PlayerBody.ShapeChosen);
            Assert.IsNotNull(save.Messes);
            Assert.AreEqual(0, save.Messes.Count);
            Assert.AreEqual(0, save.NextMessSerial);
            Assert.AreEqual("Skipper", save.PlayerName, "nothing else is touched");
            Assert.AreEqual(0.25f, save.PlayerNeeds.Hunger.Level);
            Assert.AreEqual(4000d, save.PlayerNeeds.Hunger.SinceGameSeconds);
        }

        [Test]
        public void Version20Object_WithNullBody_IsMigrated()
        {
            var old = new SaveData { SchemaVersion = 20, PlayerBody = null, Messes = null, PlayerName = "Skipper" };
            SaveData save = SaveMigration.Migrate(old);
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsNotNull(save.PlayerBody);
            Assert.IsNotNull(save.Messes);
            Assert.AreEqual(1f, BodyMath.HungerRateScale(save.PlayerBody, BodySettings.Default));
            Assert.AreEqual(1f, BodyMath.WalkFactor(BodyMath.AnchorMassKg(save.PlayerBody, BodySettings.Default),
                                                    BodyMath.FrameOf(save.PlayerBody, BodySettings.Default),
                                                    BodyMath.HeightScaleOf(save.PlayerBody, BodySettings.Default),
                                                    BodySettings.Default));
        }

        [Test]
        public void Version21_BodyAndMessesSurviveRoundTrip_AndMigrationKeepsThem()
        {
            SaveData save = SaveMigration.NewGame();
            save.PlayerBody = new PlayerBodyDto
            {
                Frame = 0.75f, HeightScale = 1.04f, ShapeChosen = true, MassKg = 81.5d,
                MassSinceGameSeconds = 9000.5d, StomachMeals = 0.5f, StomachSinceGameSeconds = 8999d,
            };
            save.Messes.Add(new MessRecordDto("mess.3", "region.nmc", new UnityEngine.Vector2(1.5f, -2f), 777d));
            save.NextMessSerial = 3;

            SaveData back = SaveMigration.Migrate(SaveSerialization.FromJson(SaveSerialization.ToJson(save)));
            Assert.AreEqual(0.75f, back.PlayerBody.Frame);
            Assert.AreEqual(1.04f, back.PlayerBody.HeightScale);
            Assert.IsTrue(back.PlayerBody.ShapeChosen);
            Assert.AreEqual(81.5d, back.PlayerBody.MassKg, "idempotent: a current body is not reset");
            Assert.AreEqual(9000.5d, back.PlayerBody.MassSinceGameSeconds);
            Assert.AreEqual(0.5f, back.PlayerBody.StomachMeals);
            Assert.AreEqual(1, back.Messes.Count);
            Assert.AreEqual("mess.3", back.Messes[0].Id);
            Assert.AreEqual("region.nmc", back.Messes[0].RegionId);
            Assert.AreEqual(777d, back.Messes[0].GameSeconds);
            Assert.AreEqual(3, back.NextMessSerial);
        }

        [Test]
        public void Version20Json_LoadsUntrained_AndFitnessSurvivesRoundTrip()
        {
            SaveData old = SaveSerialization.FromJson("{\"SchemaVersion\":20,\"GameTimeSeconds\":5000.0}");
            foreach (FitnessKind kind in StrengthMath.Kinds)
            {
                FitnessStateDto f = old.PlayerBody.Get(kind);
                Assert.AreEqual(0f, f.Level, kind.ToString());
                Assert.AreEqual(0f, f.Peak, kind.ToString());
                Assert.Less(f.SinceGameSeconds, 0d, "body slice 2 rides the same v21 migration: untrained");
            }

            SaveData save = SaveMigration.NewGame();
            save.PlayerBody.Grip = new FitnessStateDto
            {
                Level = 0.25f, Peak = 0.5f, SinceGameSeconds = 1200.5d, MaintainedAtGameSeconds = 1000d, PendingDose = 3.5f,
            };
            save.PlayerBody.Cardio = new FitnessStateDto
            {
                Level = 0.75f, Peak = 0.75f, SinceGameSeconds = 99d, MaintainedAtGameSeconds = -1d, PendingDose = 0f,
            };
            SaveData back = SaveMigration.Migrate(SaveSerialization.FromJson(SaveSerialization.ToJson(save)));
            Assert.AreEqual(0.25f, back.PlayerBody.Grip.Level);
            Assert.AreEqual(0.5f, back.PlayerBody.Grip.Peak);
            Assert.AreEqual(1200.5d, back.PlayerBody.Grip.SinceGameSeconds);
            Assert.AreEqual(1000d, back.PlayerBody.Grip.MaintainedAtGameSeconds);
            Assert.AreEqual(3.5f, back.PlayerBody.Grip.PendingDose);
            Assert.AreEqual(0.75f, back.PlayerBody.Cardio.Level);
            Assert.AreEqual(-1d, back.PlayerBody.Cardio.MaintainedAtGameSeconds);
            Assert.AreEqual(0f, back.PlayerBody.Back.Level, "untouched kinds stay untrained");
        }
    }
}
