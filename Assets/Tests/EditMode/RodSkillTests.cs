using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The rod-fishing skill (owner ruling 2026-11-25): rods carry their own numbers, the player's skill
    /// grows with fish won on a rod, and the effective numbers are the rod's PLUS the skill's, so a
    /// skilled player still gains from a better rod and a novice still gains from one too. Pins the
    /// shipped curve, the additive rule at every level and tier, determinism, per-player books, and that
    /// a save made before the skill existed loads at zero.
    /// </summary>
    public class RodSkillTests
    {
        private const string SkillPath = "Assets/_Project/Data/Resources/RodSkill/RodSkill.asset";
        private static readonly string[] RodsByTier = { "Rod", "RodCoast", "RodDeep" };

        private readonly List<int> _levelsReached = new();
        private void OnLevel(RodSkillLevelReached e) => _levelsReached.Add(e.Level);

        [SetUp]
        public void SetUp()
        {
            _levelsReached.Clear();
            EventBus.Clear<RodSkillLevelReached>();
            EventBus.Subscribe<RodSkillLevelReached>(OnLevel);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<RodSkillLevelReached>(OnLevel);
            EventBus.Clear<RodSkillLevelReached>();
            GameServices.RodSkills = null;
        }

        private static RodSkillDef Shipped()
        {
            var def = AssetDatabase.LoadAssetAtPath<RodSkillDef>(SkillPath);
            Assert.IsNotNull(def, SkillPath);
            return def;
        }

        private static RodFishingProfile Rod(string name)
        {
            var def = AssetDatabase.LoadAssetAtPath<ToolDef>($"Assets/_Project/Data/Tools/{name}.asset");
            Assert.IsNotNull(def, name);
            Assert.IsTrue(def.RodFishing.IsValid, name);
            return def.RodFishing;
        }

        private static RodSkillProgress AtLevel(RodSkillDef def, int level)
            => new RodSkillProgress(def, "player.test", level == 0 ? 0 : def.Levels[level - 1].ExperienceRequired);

        // ---- content ----------------------------------------------------------------------------

        [Test]
        public void ShippedSkill_IsSound_AndTheOnlyOneUnderResources()
        {
            RodSkillDef def = Shipped();
            Assert.IsNull(def.Problem());
            Assert.AreEqual("skill.rod_fishing", def.Id);
            Assert.Greater(def.MaxLevel, 0);
            Assert.AreEqual(1, Resources.LoadAll<RodSkillDef>(RodSkillBook.ResourcesFolder).Length);
        }

        [Test]
        public void Problem_RefusesALadderThatDoesNotRise()
        {
            var def = ScriptableObject.CreateInstance<RodSkillDef>();
            try
            {
                def.Levels = new[]
                {
                    new RodSkillLevel { ExperienceRequired = 10, CastMetres = 1f, LineStrength = 0.1f, ReelUpMetresPerSecond = 0.1f, FightHelp = 0.1f },
                    new RodSkillLevel { ExperienceRequired = 20, CastMetres = 1f, LineStrength = 0.2f, ReelUpMetresPerSecond = 0.2f, FightHelp = 0.2f },
                };
                StringAssert.Contains("cast reach", def.Problem());
                def.Levels[1].CastMetres = 2f;
                def.Levels[1].ExperienceRequired = 10;
                StringAssert.Contains("experience", def.Problem());
                def.Levels[1].ExperienceRequired = 20;
                Assert.IsNull(def.Problem());
            }
            finally { Object.DestroyImmediate(def); }
        }

        // ---- the curve --------------------------------------------------------------------------

        [Test]
        public void Curve_EachThresholdReachesItsLevel_AndTheTopHolds()
        {
            RodSkillDef def = Shipped();
            Assert.AreEqual(0, RodSkillMath.LevelFor(0, def));
            for (int i = 0; i < def.MaxLevel; i++)
            {
                int need = def.Levels[i].ExperienceRequired;
                Assert.AreEqual(i, RodSkillMath.LevelFor(need - 1, def), $"one short of level {i + 1}");
                Assert.AreEqual(i + 1, RodSkillMath.LevelFor(need, def), $"exactly level {i + 1}");
            }
            Assert.AreEqual(def.MaxLevel, RodSkillMath.LevelFor(int.MaxValue, def));
            Assert.AreEqual(RodSkillMath.BonusAt(def.MaxLevel, def), RodSkillMath.BonusAt(def.MaxLevel + 5, def));
            Assert.IsTrue(RodSkillMath.BonusAt(0, def).IsNone);
            Assert.AreEqual(0, RodSkillMath.LevelFor(1000, null), "no Def, no level");
        }

        [Test]
        public void Experience_HarderFishGiveMore_AndEveryWonFishGivesSome()
        {
            RodSkillDef def = Shipped();
            int easiest = RodSkillMath.ExperienceFor(0f, def);
            int hardest = RodSkillMath.ExperienceFor(1f, def);
            Assert.AreEqual(def.ExperiencePerFish, easiest);
            Assert.AreEqual(def.ExperiencePerFish + def.ExperienceForHardestFish, hardest);
            Assert.Greater(easiest, 0);
            int previous = easiest;
            for (int step = 1; step <= 20; step++)
            {
                int xp = RodSkillMath.ExperienceFor(step / 20f, def);
                Assert.GreaterOrEqual(xp, previous);
                previous = xp;
            }
            Assert.AreEqual(easiest, RodSkillMath.ExperienceFor(float.NaN, def));
            Assert.AreEqual(easiest, RodSkillMath.ExperienceFor(-3f, def));
            Assert.AreEqual(hardest, RodSkillMath.ExperienceFor(9f, def));
        }

        [Test]
        public void Progress_LevelsUpOnLandings_AndAnnouncesEachLevelOnce()
        {
            RodSkillDef def = Shipped();
            var skill = new RodSkillProgress(def, "player.test");
            int landings = 0;
            while (skill.Level < skill.MaxLevel && landings < 100000)
            {
                skill.RecordLanded(0.5f);
                landings++;
            }
            Assert.AreEqual(def.MaxLevel, skill.Level);
            CollectionAssert.AreEqual(Ladder(def.MaxLevel), _levelsReached);
            Assert.Greater(landings, def.MaxLevel, "the top takes many fish, never a handful");
        }

        private static List<int> Ladder(int top)
        {
            var l = new List<int>();
            for (int i = 1; i <= top; i++) l.Add(i);
            return l;
        }

        // ---- the additive rule --------------------------------------------------------------------

        [Test]
        public void Effective_IsExactlyRodPlusSkill_InTheFourNumbers_AndLeavesTheRest()
        {
            RodSkillDef def = Shipped();
            foreach (string name in RodsByTier)
            {
                RodFishingProfile rod = Rod(name);
                for (int level = 0; level <= def.MaxLevel; level++)
                {
                    RodSkillBonus b = RodSkillMath.BonusAt(level, def);
                    RodFishingProfile e = AtLevel(def, level).Apply(in rod);
                    Assert.AreEqual(rod.CastMetres + b.CastMetres, e.CastMetres);
                    Assert.AreEqual(rod.LineStrengthMultiplier + b.LineStrength, e.LineStrengthMultiplier);
                    Assert.AreEqual(rod.ReelUpMetresPerSecond + b.ReelUpMetresPerSecond, e.ReelUpMetresPerSecond);
                    Assert.AreEqual(rod.LandingMultiplier + b.FightHelp, e.LandingMultiplier);

                    RodFishingProfile rest = e;
                    rest.CastMetres = rod.CastMetres;
                    rest.LineStrengthMultiplier = rod.LineStrengthMultiplier;
                    rest.ReelUpMetresPerSecond = rod.ReelUpMetresPerSecond;
                    rest.LandingMultiplier = rod.LandingMultiplier;
                    Assert.AreEqual(rod, rest, $"{name} L{level}: nothing else moves");
                    Assert.IsTrue(e.IsValid);
                }
            }
        }

        [Test]
        public void ZeroSkill_LeavesEveryRodBitForBit()
        {
            RodSkillDef def = Shipped();
            foreach (string name in RodsByTier)
            {
                RodFishingProfile rod = Rod(name);
                Assert.AreEqual(rod, AtLevel(def, 0).Apply(in rod), name);
                Assert.AreEqual(rod, RodSkillMath.Apply(in rod, RodSkillBonus.None), name);
            }
        }

        /// <summary>The ruling's heart: at EVERY level, a better rod is strictly better in all four
        /// numbers, and at EVERY rod, a higher level is strictly better in all four. So a high skill plus a
        /// better rod beats the high skill on the worse rod AND the better rod in a weaker hand.</summary>
        [Test]
        public void SkillAndRod_EachStrictlyHelps_AtEveryLevelAndEveryTier()
        {
            RodSkillDef def = Shipped();
            for (int level = 0; level <= def.MaxLevel; level++)
            {
                for (int tier = 0; tier < RodsByTier.Length; tier++)
                {
                    RodFishingProfile here = AtLevel(def, level).Apply(Rod(RodsByTier[tier]));
                    if (tier > 0)
                        StrictlyBetter(here, AtLevel(def, level).Apply(Rod(RodsByTier[tier - 1])),
                                       $"L{level}: {RodsByTier[tier]} over {RodsByTier[tier - 1]}");
                    if (level > 0)
                        StrictlyBetter(here, AtLevel(def, level - 1).Apply(Rod(RodsByTier[tier])),
                                       $"{RodsByTier[tier]}: L{level} over L{level - 1}");
                    if (level > 0 && tier > 0)
                    {
                        RodFishingProfile skillAlone = AtLevel(def, level).Apply(Rod(RodsByTier[0]));
                        RodFishingProfile rodAlone = AtLevel(def, 0).Apply(Rod(RodsByTier[tier]));
                        StrictlyBetter(here, skillAlone, $"L{level}+{RodsByTier[tier]} over the skill alone");
                        StrictlyBetter(here, rodAlone, $"L{level}+{RodsByTier[tier]} over the rod alone");
                    }
                }
            }
        }

        private static void StrictlyBetter(in RodFishingProfile a, in RodFishingProfile b, string why)
        {
            Assert.Greater(a.CastMetres, b.CastMetres, why + " (reach)");
            Assert.Greater(a.LineStrengthMultiplier, b.LineStrengthMultiplier, why + " (line)");
            Assert.Greater(a.ReelUpMetresPerSecond, b.ReelUpMetresPerSecond, why + " (reel)");
            Assert.Greater(a.LandingMultiplier, b.LandingMultiplier, why + " (fight help)");
        }

        // ---- determinism --------------------------------------------------------------------------

        [Test]
        public void SameLandings_GiveTheSameSkill_AndOrderDoesNotMatter()
        {
            RodSkillDef def = Shipped();
            var difficulties = new List<float>();
            var rng = new System.Random(20261125);
            for (int i = 0; i < 400; i++) difficulties.Add((float)rng.NextDouble());

            var a = new RodSkillProgress(def);
            var b = new RodSkillProgress(def);
            var reversed = new RodSkillProgress(def);
            foreach (float d in difficulties) { a.RecordLanded(d); b.RecordLanded(d); }
            for (int i = difficulties.Count - 1; i >= 0; i--) reversed.RecordLanded(difficulties[i]);

            Assert.AreEqual(a.Experience, b.Experience);
            Assert.AreEqual(a.Level, b.Level);
            Assert.AreEqual(a.Bonus, b.Bonus);
            Assert.AreEqual(a.Experience, reversed.Experience);
            Assert.AreEqual(a.Bonus, reversed.Bonus);
            Assert.AreEqual(a.Bonus, new RodSkillProgress(def, "", a.Experience).Bonus,
                            "the level is a pure function of the experience total");
        }

        [Test]
        public void ReadingTheSkill_AllocatesNothing()
        {
            var session = new object();
            RodSkillDef def = Shipped();
            var book = new RodSkillBook(() => session, () => def);
            book.For(RodSkillBook.LocalPlayerKey).RecordLanded(1f);
            RodFishingProfile rod = Rod("RodDeep");
            RodFishingProfile sink = default;
            TestDelegate read = () =>
            {
                for (int i = 0; i < 200; i++) sink = book.For(RodSkillBook.LocalPlayerKey).Apply(in rod);
            };
            read();   // warm up: the first call compiles the path; the steady read is what is measured
            Assert.That(read, Is.Not.AllocatingGCMemory());
            Assert.IsTrue(sink.IsValid);
        }

        // ---- per player, per game, and the save -----------------------------------------------------

        [Test]
        public void Book_KeepsEachPlayersOwnSkill()
        {
            var session = new object();
            RodSkillDef def = Shipped();
            var book = new RodSkillBook(() => session, () => def);
            IRodSkill host = book.For(RodSkillBook.LocalPlayerKey);
            IRodSkill peer = book.For("player.peer_2");
            for (int i = 0; i < 50; i++) host.RecordLanded(1f);

            Assert.Greater(host.Level, 0);
            Assert.AreEqual(0, peer.Experience, "one player's fish never train another");
            Assert.AreSame(host, book.For(RodSkillBook.LocalPlayerKey));
            Assert.AreSame(host, book.For(""), "an unnamed angler is the local player");
            Assert.AreNotSame(host, peer);
        }

        [Test]
        public void Book_StartsEachGameAtZero_AndEarnsNothingWithNoGame()
        {
            object session = new object();
            RodSkillDef def = Shipped();
            var book = new RodSkillBook(() => session, () => def);
            for (int i = 0; i < 50; i++) book.For(RodSkillBook.LocalPlayerKey).RecordLanded(1f);
            Assert.Greater(book.For(RodSkillBook.LocalPlayerKey).Experience, 0);

            session = new object();   // a load or a new game
            Assert.AreEqual(0, book.For(RodSkillBook.LocalPlayerKey).Experience);

            session = null;           // no game at all (a bare rig)
            IRodSkill none = book.For(RodSkillBook.LocalPlayerKey);
            Assert.AreEqual(0, none.RecordLanded(1f));
            Assert.AreEqual(0, none.Experience);
            Assert.IsTrue(none.Bonus.IsNone);
        }

        [Test]
        public void OldSave_LoadsWithSkillZero_AndTheSaveFormatIsUntouched()
        {
            var old = new SaveData { SchemaVersion = 23, WorldSeed = 7 };
            SaveData migrated = SaveMigration.Migrate(old);
            Assert.AreEqual(SaveMigration.CurrentVersion, migrated.SchemaVersion);

            // A save with a long catch record still starts at zero: the skill is never read from it.
            migrated.CaughtSpecies.Add(new CaughtSpeciesDto { SpeciesId = "fish.atlantic_cod", Count = 400, RecordWeightKg = 9f });
            SaveData reloaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(migrated));

            RodSkillDef def = Shipped();
            var book = new RodSkillBook(() => reloaded, () => def);
            IRodSkill skill = book.For(RodSkillBook.LocalPlayerKey);
            Assert.AreEqual(0, skill.Experience);
            Assert.AreEqual(0, skill.Level);
            Assert.IsTrue(skill.Bonus.IsNone);

            string before = JsonUtility.ToJson(reloaded);
            skill.RecordLanded(1f);
            Assert.AreEqual(before, JsonUtility.ToJson(reloaded), "earning skill writes nothing to the save");
        }

        [Test]
        public void GameServices_RodSkills_IsNeverNull_AndResetsToZero()
        {
            Assert.IsNotNull(GameServices.RodSkills);
            var session = new object();
            RodSkillDef def = Shipped();
            GameServices.RodSkills = new RodSkillBook(() => session, () => def);
            GameServices.RodSkills.For(RodSkillBook.LocalPlayerKey).RecordLanded(1f);
            GameServices.Reset();
            Assert.IsNotNull(GameServices.RodSkills);
            Assert.AreEqual(0, GameServices.RodSkills.For(RodSkillBook.LocalPlayerKey).Experience);
        }
    }
}
