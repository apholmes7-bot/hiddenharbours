using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// What an NPC boat's pots hold and what becomes of each animal on deck, on the real committed Def, trap,
    /// deck-work rules and species: the same catch a player's pot would hold, the same every time it is asked,
    /// and every animal either banded, graded and stowed or put back.
    /// </summary>
    public class NpcPotDayCatchTests
    {
        private const string DefPath = "Assets/_Project/Data/Traps/NmcDoironYvettePotDay.asset";
        private const string FishFolder = "Assets/_Project/Data/Fish";
        private const double Day = GameConfig.DefaultSecondsPerDay;
        private const int Seed = 4242;
        private const int DaysProbed = 30;

        private NpcPotDayDef _def;

        private static float Tide(double t) => 1.1f * Mathf.Sin((float)(t / Day * 4.0 * Mathf.PI));

        [SetUp]
        public void SetUp()
        {
            FishSpeciesRegistry.Reset();
            _def = AssetDatabase.LoadAssetAtPath<NpcPotDayDef>(DefPath);
            Assert.IsNotNull(_def, DefPath);
            var allowed = new HashSet<string>(_def.Trap.AllowedCatchFishIds);
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(FishSpeciesDef)}", new[] { FishFolder }))
            {
                var fish = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (fish != null && allowed.Contains(fish.Id)) FishSpeciesRegistry.Register(fish);
            }
            Assert.IsNotEmpty(FishSpeciesRegistry.Resolve(_def.Trap.AllowedCatchFishIds), "the pot's species are in Data/Fish");
        }

        [TearDown]
        public void TearDown() => FishSpeciesRegistry.Reset();

        private NpcPotDayCatch Build(int day, out NpcPotDay today, int seed = Seed)
        {
            today = NpcPotDay.Build(_def, seed, day, Day);
            NpcPotDay yesterday = NpcPotDay.Build(_def, seed, day - 1, Day);
            return NpcPotDayCatch.Build(_def, seed, today, yesterday, Season.EarlySpring, Tide, Day);
        }

        [Test]
        public void TheDefIsSetUpForTheDeck()
        {
            Assert.That(_def.CatchRegionId, Does.StartWith("region.").And.Length.GreaterThan("region.".Length));
            foreach (FishSpeciesDef fish in FishSpeciesRegistry.Resolve(_def.Trap.AllowedCatchFishIds))
                Assert.IsTrue(fish.RegionAllowed(_def.CatchRegionId), $"{fish.Id} is not on {_def.CatchRegionId}'s table: her pots would come up empty");
            Assert.IsNotNull(_def.Bait, "she fishes with bait");
            Assert.IsNotNull(_def.Trap.DeckWork, "her pot has deck-work rules: they are what makes a keeper");
            Assert.That(NpcPotDayCatch.WorstCaseDeckSeconds(_def), Is.LessThanOrEqualTo(_def.DeckWorkSeconds),
                        "a full pot of keepers, banded and stowed, then the bait, fits the time the pot is aboard");
            Assert.That(_def.GradeBreaksMm, Is.Ordered.Ascending);
        }

        [Test]
        public void SameSeedAndDay_GiveTheSameCatch_AnimalForAnimal()
        {
            for (int day = 0; day < 5; day++)
            {
                NpcPotDayCatch a = Build(day, out _), b = Build(day, out _);
                Assert.That(b.KeptCount, Is.EqualTo(a.KeptCount));
                Assert.That(b.KeptKg, Is.EqualTo(a.KeptKg));
                for (int p = 0; p < a.PotCount; p++)
                {
                    Assert.That(b.Animals(p).Count, Is.EqualTo(a.Animals(p).Count), $"day {day} pot {p}");
                    for (int k = 0; k < a.Animals(p).Count; k++)
                    {
                        NpcPotAnimal x = a.Animals(p)[k], y = b.Animals(p)[k];
                        Assert.That(y.Item.SpeciesId, Is.EqualTo(x.Item.SpeciesId));
                        Assert.That(y.Item.WeightKg, Is.EqualTo(x.Item.WeightKg));
                        Assert.That(y.SizeMm, Is.EqualTo(x.SizeMm));
                        Assert.That(y.Keeper, Is.EqualTo(x.Keeper));
                        Assert.That(y.Grade, Is.EqualTo(x.Grade));
                        Assert.That(y.DoneAt, Is.EqualTo(x.DoneAt));
                    }
                }
            }
            Assert.That(Build(3, out _, Seed + 1).KeptKg, Is.Not.EqualTo(Build(3, out _).KeptKg), "another world, another catch");
        }

        [Test]
        public void HerPot_HoldsExactlyWhatAPlayersPotWouldHold()
        {
            var expected = new List<CatchItem>();
            var pool = FishSpeciesRegistry.Resolve(_def.Trap.AllowedCatchFishIds);
            for (int day = 0; day < 5; day++)
            {
                NpcPotDayCatch c = Build(day, out NpcPotDay today);
                NpcPotDay yesterday = NpcPotDay.Build(_def, Seed, day - 1, Day);
                for (int p = 0; p < c.PotCount; p++)
                {
                    double setAt = yesterday.SetEnd[p], hauledAt = today.HaulStart[p];
                    string id = NpcPotDay.PotInstanceId(_def, p);
                    float hour = (float)((hauledAt - today.DayStartSeconds) / Day * 24.0);
                    var ctx = new CatchContext(_def.CatchRegionId, Tide(hauledAt), hour, Season.EarlySpring, Gear.Trap);
                    float fill = TrapFill.Fill01(setAt, hauledAt, _def.Trap.SoakHours, _def.Trap.HoursToFullPot);
                    int count = TrapFill.ResolveCount(fill, _def.Trap.CapacityUnits, Seed, id, setAt);
                    PlacedTrapCatch.ResolveMany(pool, in ctx, _def.Bait.FavorsSpeciesIds, PlacedTrapCatch.BaitFavourMultiplier,
                                                count, Seed, id, setAt, expected);
                    var deck = new DeckPot(_def.Trap, _def.Trap.DeckWork, id, setAt, Seed, expected);

                    Assert.That(c.Animals(p).Count, Is.EqualTo(expected.Count), $"day {day} pot {p}");
                    for (int k = 0; k < expected.Count; k++)
                    {
                        Assert.That(c.Animals(p)[k].Item.SpeciesId, Is.EqualTo(expected[k].SpeciesId));
                        Assert.That(c.Animals(p)[k].Item.WeightKg, Is.EqualTo(expected[k].WeightKg));
                        Assert.That(c.Animals(p)[k].Keeper, Is.EqualTo(deck.Animals[k].Keeper));
                        Assert.That(c.Animals(p)[k].SizeMm, Is.EqualTo(deck.Animals[k].SizeMm));
                    }
                }
            }
        }

        [Test]
        public void AfterANightsSoak_EveryPotComesUpWithSomething_AndNeverMoreThanItHolds()
        {
            int kept = 0, returned = 0;
            for (int day = 0; day < DaysProbed; day++)
            {
                NpcPotDayCatch c = Build(day, out _);
                for (int p = 0; p < c.PotCount; p++)
                    Assert.That(c.Animals(p).Count, Is.InRange(1, _def.Trap.CapacityUnits), $"day {day} pot {p}");
                kept += c.KeptCount;
                returned += c.ReturnedCount;
            }
            Assert.That(kept, Is.GreaterThan(0), "she lands something in a month");
            Assert.That(returned, Is.GreaterThan(0), "and puts shorts or berried ones back");
        }

        [Test]
        public void EveryAnimal_IsPickedThenBandedAndStowed_OrPutBack_ThenThePotIsBaited()
        {
            const double eps = 0.01;
            for (int day = 0; day < 6; day++)
            {
                NpcPotDayCatch c = Build(day, out NpcPotDay today);
                for (int p = 0; p < c.PotCount; p++)
                {
                    Assert.That(c.MomentAt(p, today.HaulEnd[p] - eps).Step, Is.EqualTo(NpcDeckStep.Idle), "nothing before the pot is aboard");
                    var animals = c.Animals(p);
                    double t = today.HaulEnd[p];
                    for (int k = 0; k < animals.Count; k++)
                    {
                        NpcPotAnimal a = animals[k];
                        Assert.That(a.PickedAt, Is.EqualTo(t).Within(1e-6), "one animal at a time, in order");
                        Assert.That(c.MomentAt(p, a.PickedAt + eps).Step, Is.EqualTo(NpcDeckStep.Picking));
                        Assert.That(c.MomentAt(p, a.PickedAt + eps).Animal, Is.EqualTo(k));
                        if (a.Keeper)
                        {
                            Assert.That(a.Grade, Is.EqualTo(NpcPotDayCatch.GradeOf(a.SizeMm, _def.GradeBreaksMm)));
                            Assert.That(c.MomentAt(p, a.PickedAt + _def.PickSeconds + eps).Step, Is.EqualTo(NpcDeckStep.Banding));
                            Assert.That(c.MomentAt(p, a.PickedAt + _def.PickSeconds + _def.BandSeconds + eps).Step, Is.EqualTo(NpcDeckStep.Stowing));
                            Assert.That(c.StowedAt(a.DoneAt - eps, a.Grade) + 1, Is.EqualTo(c.StowedAt(a.DoneAt, a.Grade)),
                                        "it is in its crate when the carry ends, not before");
                        }
                        else
                        {
                            Assert.That(a.Grade, Is.EqualTo(-1));
                            Assert.That(c.MomentAt(p, a.PickedAt + _def.PickSeconds + eps).Step, Is.EqualTo(NpcDeckStep.Returning));
                            Assert.That(c.StowedAt(a.DoneAt), Is.EqualTo(c.StowedAt(a.PickedAt)), "a returned animal is never stowed");
                        }
                        t = a.DoneAt;
                    }
                    Assert.That(c.MomentAt(p, t + eps).Step, Is.EqualTo(NpcDeckStep.Baiting));
                    Assert.That(c.ReadyToSetAt(p), Is.EqualTo(t + _def.BaitSeconds).Within(1e-6));
                    Assert.That(c.ReadyToSetAt(p), Is.LessThanOrEqualTo(today.SetStart[p] + 1e-6), "baited before it goes back over");
                    Assert.That(c.MomentAt(p, c.ReadyToSetAt(p) + eps).Step, Is.EqualTo(NpcDeckStep.Idle));
                }
            }
        }

        [Test]
        public void TheCrates_FillThroughTheDay_AndHoldTheDaysKeepersWhenSheIsHome()
        {
            NpcPotDayCatch c = Build(2, out NpcPotDay today);
            Assert.That(c.StowedAt(today.DayStartSeconds), Is.EqualTo(0), "empty at dawn");
            Assert.That(c.StowedAt(today.GaffStart[0]), Is.EqualTo(0), "empty until the first pot is worked");

            int last = 0;
            for (double t = today.DepartSeconds; t <= today.ReturnSeconds; t += 1.0)
            {
                int now = c.StowedAt(t);
                Assert.That(now, Is.GreaterThanOrEqualTo(last), $"nothing leaves a crate at sea ({t})");
                last = now;
            }

            int grades = _def.GradeBreaksMm.Length + 1, byGrade = 0;
            for (int g = 0; g < grades; g++) byGrade += c.StowedAt(today.ReturnSeconds, g);
            Assert.That(c.StowedAt(today.ReturnSeconds), Is.EqualTo(c.KeptCount));
            Assert.That(byGrade, Is.EqualTo(c.KeptCount), "every keeper is in exactly one grade's crate");
            Assert.That(c.StowedKgAt(today.ReturnSeconds), Is.EqualTo(c.KeptKg).Within(1e-3f));
        }

        [Test]
        public void Grades_StepUpAtEachBreak()
        {
            float[] breaks = { 95f, 110f };
            Assert.That(NpcPotDayCatch.GradeOf(83f, breaks), Is.EqualTo(0));
            Assert.That(NpcPotDayCatch.GradeOf(94.9f, breaks), Is.EqualTo(0));
            Assert.That(NpcPotDayCatch.GradeOf(95f, breaks), Is.EqualTo(1));
            Assert.That(NpcPotDayCatch.GradeOf(110f, breaks), Is.EqualTo(2));
            Assert.That(NpcPotDayCatch.GradeOf(200f, null), Is.EqualTo(0), "no breaks: one crate");
        }

        [Test]
        public void WithNoSpeciesToCatch_ThePotsComeUpEmpty_AndTheJobIsOnlyTheBait()
        {
            FishSpeciesRegistry.Reset();
            NpcPotDayCatch c = Build(1, out NpcPotDay today);
            Assert.That(c.KeptCount + c.ReturnedCount, Is.EqualTo(0));
            Assert.That(c.MomentAt(0, today.HaulEnd[0] + 0.01).Step, Is.EqualTo(NpcDeckStep.Baiting));
        }
    }
}
