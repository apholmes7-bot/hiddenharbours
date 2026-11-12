using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// An NPC pot boat's day is a pure function of <c>(def, worldSeed, dayIndex, secondsPerDay)</c> (rule 5):
    /// the same inputs give the same boat, pots and buoys; a game loaded mid-day finds them where the clock says;
    /// and a pot lies where the latest set put it, across midnight, with nothing saved.
    /// </summary>
    public class NpcPotDayTests
    {
        private const double Day = 1800.0;
        private const int Seed = 4242;

        private NpcPotDayDef _def;
        private TrapDef _trap;

        [SetUp]
        public void SetUp()
        {
            _trap = ScriptableObject.CreateInstance<TrapDef>();
            _def = ScriptableObject.CreateInstance<NpcPotDayDef>();
            _def.Id = "potday.test";
            _def.OwnerId = "owner.test";
            _def.Trap = _trap;
            _def.BerthPosition = new Vector2(0f, 0f);
            _def.BerthHeadingDegrees = 90f;
            _def.PassageOut = new[] { new Vector2(0f, -20f), new Vector2(60f, -20f), new Vector2(96.8f, 0f) };
            _def.PassageHome = new[] { new Vector2(80f, 60f), new Vector2(60f, -20f), new Vector2(0f, -20f) };
            _def.HarbourLegCount = 1;
            _def.StringStart = new Vector2(100f, 10f);
            _def.StringBearingDegrees = 0f;
            _def.PotCount = 4;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_def);
            Object.DestroyImmediate(_trap);
        }

        private NpcPotDay Build(int day, int seed = Seed) => NpcPotDay.Build(_def, seed, day, Day);

        [Test]
        public void SameSeedAndDay_GiveTheSameBoatAndPots()
        {
            NpcPotDay a = Build(3), b = Build(3);
            NpcPotDay ya = Build(2), yb = Build(2);

            Assert.That(b.DepartSeconds, Is.EqualTo(a.DepartSeconds));
            Assert.That(b.ReturnSeconds, Is.EqualTo(a.ReturnSeconds));
            for (double t = a.DayStartSeconds; t < a.DayStartSeconds + Day; t += 7.3)
            {
                NpcPotBoatPose pa = a.PoseAt(t), pb = b.PoseAt(t);
                Assert.That(pb.Position, Is.EqualTo(pa.Position), $"boat at {t}");
                Assert.That(pb.HeadingDegrees, Is.EqualTo(pa.HeadingDegrees), $"heading at {t}");
                Assert.That(pb.Stage, Is.EqualTo(pa.Stage), $"stage at {t}");
                for (int i = 0; i < a.PotCount; i++)
                {
                    NpcPotState sa = NpcPotDay.PotAt(a, ya, i, t), sb = NpcPotDay.PotAt(b, yb, i, t);
                    Assert.That(sb.Phase, Is.EqualTo(sa.Phase), $"pot {i} at {t}");
                    Assert.That(sb.Seabed, Is.EqualTo(sa.Seabed), $"pot {i} at {t}");
                    Assert.That(sb.Phase01, Is.EqualTo(sa.Phase01), $"pot {i} at {t}");
                }
            }
        }

        [Test]
        public void AnotherSeed_OrAnotherDay_MovesTheGearAndTheCastOff()
        {
            NpcPotDay a = Build(3), otherSeed = Build(3, Seed + 1), otherDay = Build(4);

            Assert.That(otherSeed.SetPositions[0], Is.Not.EqualTo(a.SetPositions[0]), "another world sets elsewhere");
            Assert.That(otherDay.SetPositions[0], Is.Not.EqualTo(a.SetPositions[0]), "tomorrow's set is not today's");
            Assert.That(otherSeed.DepartSeconds - otherSeed.DayStartSeconds,
                        Is.Not.EqualTo(a.DepartSeconds - a.DayStartSeconds), "the cast-off is seeded");
        }

        [Test]
        public void TheCastOff_StaysWithinItsJitter()
        {
            double minute = Day / 1440.0;
            for (int day = -3; day < 40; day++)
            {
                NpcPotDay d = Build(day);
                double planned = d.DayStartSeconds + _def.DepartHour / 24.0 * Day;
                Assert.That(d.DepartSeconds, Is.InRange(planned - _def.DepartJitterMinutes * minute,
                                                         planned + _def.DepartJitterMinutes * minute), $"day {day}");
                Assert.That(d.FitsTheDay(Day), Is.True, $"day {day} is home before midnight");
            }
        }

        [Test]
        public void OutsideTheWorkingDay_SheLiesAtHerBerth()
        {
            NpcPotDay d = Build(5);
            foreach (double t in new[] { d.DayStartSeconds, d.DepartSeconds - 0.01, d.ReturnSeconds, d.DayStartSeconds + Day - 0.01 })
            {
                NpcPotBoatPose p = d.PoseAt(t);
                Assert.That(p.Stage, Is.EqualTo(NpcPotBoatStage.AtBerth), $"at {t}");
                Assert.That(p.Position, Is.EqualTo(_def.BerthPosition));
                Assert.That(p.HeadingDegrees, Is.EqualTo(_def.BerthHeadingDegrees));
                Assert.That(p.UnderWay, Is.False);
            }
        }

        [Test]
        public void TheTrack_NeverJumps_AndNeverOutrunsHerSpeed()
        {
            NpcPotDay d = Build(2);
            const double step = 0.1;
            float fastest = Mathf.Max(_def.HarbourSpeed, _def.CruiseSpeed, _def.WorkSpeed);
            NpcPotBoatPose was = d.PoseAt(d.DepartSeconds - step);
            for (double t = d.DepartSeconds; t <= d.ReturnSeconds + step; t += step)
            {
                NpcPotBoatPose now = d.PoseAt(t);
                Assert.That((now.Position - was.Position).magnitude, Is.LessThanOrEqualTo(fastest * (float)step + 0.002f),
                            $"position jumped at {t} ({was.Stage} -> {now.Stage})");
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(was.HeadingDegrees, now.HeadingDegrees)), Is.LessThan(20f),
                            $"heading snapped at {t} ({was.Stage} -> {now.Stage})");
                was = now;
            }
        }

        [Test]
        public void SheWorksEveryPot_InOrder_GaffHaulDeckSet()
        {
            NpcPotDay d = Build(2);
            for (int i = 0; i < d.PotCount; i++)
            {
                Assert.That(d.GaffStart[i], Is.GreaterThan(d.DepartSeconds));
                Assert.That(d.HaulStart[i] - d.GaffStart[i], Is.EqualTo(_def.GaffSeconds).Within(1e-6));
                Assert.That(d.HaulEnd[i] - d.HaulStart[i], Is.EqualTo(_def.HaulSeconds).Within(1e-6));
                Assert.That(d.SetStart[i] - d.HaulEnd[i], Is.GreaterThanOrEqualTo(_def.DeckWorkSeconds - 1e-6),
                            "the pot is worked on deck before it goes back");
                Assert.That(d.SetEnd[i] - d.SetStart[i], Is.EqualTo(_def.SetSeconds).Within(1e-6));
                if (i > 0) Assert.That(d.GaffStart[i], Is.GreaterThan(d.SetEnd[i - 1]), "one pot at a time");

                Assert.That(d.PoseAt(d.GaffStart[i] + 0.01).Stage, Is.EqualTo(NpcPotBoatStage.Gaffing));
                Assert.That(d.PoseAt(d.HaulStart[i] + 0.01).Stage, Is.EqualTo(NpcPotBoatStage.Hauling));
                Assert.That(d.PoseAt(d.HaulEnd[i] + 0.01).Stage, Is.EqualTo(NpcPotBoatStage.DeckWork));
                Assert.That(d.PoseAt(d.SetStart[i] + 0.01).Stage, Is.EqualTo(NpcPotBoatStage.Setting));
                Assert.That(d.PoseAt(d.HaulStart[i] + 0.01).PotIndex, Is.EqualTo(i));
            }
            Assert.That(d.ReturnSeconds, Is.GreaterThan(d.SetEnd[d.PotCount - 1]));
        }

        [Test]
        public void AtTheHaulAndAtTheSet_ThePotIsToStarboard_AtTheHauler()
        {
            NpcPotDay today = Build(2), yesterday = Build(1);
            for (int i = 0; i < today.PotCount; i++)
            {
                NpcPotBoatPose hauling = today.PoseAt(today.HaulStart[i] + 0.5 * _def.HaulSeconds);
                Vector2 toPot = yesterday.SetPositions[i] - hauling.Position;
                Assert.That(hauling.HeadingDegrees, Is.EqualTo(_def.StringBearingDegrees).Within(0.01f), "bow along the string");
                Assert.That(Vector2.Dot(toPot, NpcPotDay.Starboard(hauling.HeadingDegrees)),
                            Is.EqualTo(_def.LayAlongsideMeters).Within(0.01f), $"pot {i} lies off the starboard rail at the haul");
                Assert.That(Vector2.Dot(toPot, NpcPotDay.Along(hauling.HeadingDegrees)), Is.EqualTo(0f).Within(0.01f));

                NpcPotBoatPose setting = today.PoseAt(today.SetStart[i] + 0.5 * _def.SetSeconds);
                Vector2 toSet = today.SetPositions[i] - setting.Position;
                Assert.That(Vector2.Dot(toSet, NpcPotDay.Starboard(setting.HeadingDegrees)),
                            Is.EqualTo(_def.LayAlongsideMeters).Within(0.01f), $"pot {i} goes over the starboard rail");
            }
        }

        [Test]
        public void APot_LiesWhereTheLatestSetPutIt()
        {
            NpcPotDay today = Build(2), yesterday = Build(1);
            for (int i = 0; i < today.PotCount; i++)
            {
                NpcPotState dawn = NpcPotDay.PotAt(today, yesterday, i, today.DayStartSeconds);
                Assert.That(dawn.Phase, Is.EqualTo(NpcPotPhase.Soaking));
                Assert.That(dawn.Seabed, Is.EqualTo(yesterday.SetPositions[i]), "until today's haul it lies where yesterday set it");
                Assert.That(dawn.SetAtSeconds, Is.EqualTo(yesterday.SetEnd[i]), "and has soaked since yesterday's set");

                Assert.That(NpcPotDay.PotAt(today, yesterday, i, today.GaffStart[i] + 0.01).Phase, Is.EqualTo(NpcPotPhase.Gaffed));
                Assert.That(NpcPotDay.PotAt(today, yesterday, i, today.HaulStart[i] + 0.01).Phase, Is.EqualTo(NpcPotPhase.Hauling));
                Assert.That(NpcPotDay.PotAt(today, yesterday, i, today.HaulEnd[i] + 0.01).Phase, Is.EqualTo(NpcPotPhase.Aboard));
                Assert.That(NpcPotDay.PotAt(today, yesterday, i, today.SetStart[i] + 0.01).Phase, Is.EqualTo(NpcPotPhase.Setting));

                NpcPotState dusk = NpcPotDay.PotAt(today, yesterday, i, today.DayStartSeconds + Day - 0.01);
                Assert.That(dusk.Phase, Is.EqualTo(NpcPotPhase.Soaking));
                Assert.That(dusk.Seabed, Is.EqualTo(today.SetPositions[i]));
                Assert.That(dusk.SetAtSeconds, Is.EqualTo(today.SetEnd[i]));
            }
        }

        [Test]
        public void AcrossMidnight_NoPotMoves()
        {
            NpcPotDay d1 = Build(1), d2 = Build(2), d3 = Build(3);
            double midnight = d3.DayStartSeconds;
            for (int i = 0; i < d2.PotCount; i++)
            {
                NpcPotState before = NpcPotDay.PotAt(d2, d1, i, midnight - 0.001);
                NpcPotState after = NpcPotDay.PotAt(d3, d2, i, midnight);
                Assert.That(after.Seabed, Is.EqualTo(before.Seabed), $"pot {i}");
                Assert.That(after.SetAtSeconds, Is.EqualTo(before.SetAtSeconds), $"pot {i}'s soak carries over");
                Assert.That(after.Phase, Is.EqualTo(NpcPotPhase.Soaking));
            }
        }

        [Test]
        public void TheFisheryDidNotStartWithThePlayer_DayZeroHaulsWhatTheDayBeforeSet()
        {
            NpcPotDay zero = Build(0), before = Build(-1);
            Assert.That(before.DayStartSeconds, Is.EqualTo(-Day));
            NpcPotState atStart = NpcPotDay.PotAt(zero, before, 0, 0.0);
            Assert.That(atStart.Phase, Is.EqualTo(NpcPotPhase.Soaking));
            Assert.That(atStart.SetAtSeconds, Is.LessThan(0.0), "the gear was in the water before the game began");
        }

        [Test]
        public void TodaysSet_IsWithinTheDailyShiftOfItsPlaceOnTheString()
        {
            for (int day = 0; day < 30; day++)
            {
                for (int i = 0; i < _def.PotCount; i++)
                {
                    Vector2 p = NpcPotDay.PotPosition(_def, Seed, day, i);
                    Vector2 nominal = _def.StringStart + NpcPotDay.Along(_def.StringBearingDegrees) * (i * _def.PotSpacingMeters);
                    float along = Vector2.Dot(p - nominal, NpcPotDay.Along(_def.StringBearingDegrees));
                    Assert.That(along, Is.InRange(0f, _def.DailyShiftMeters), $"day {day} pot {i}");
                    Assert.That((p - nominal).magnitude, Is.EqualTo(along).Within(1e-3f), "it stays on the string's line");
                }
            }
        }

        [Test]
        public void Hash01_IsStable_AndInRange()
        {
            for (int i = 0; i < 200; i++)
            {
                float h = NpcPotDay.Hash01(Seed, "potday.test", i, i % 5, 2);
                Assert.That(h, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                Assert.That(NpcPotDay.Hash01(Seed, "potday.test", i, i % 5, 2), Is.EqualTo(h));
            }
        }

        [Test]
        public void CompassHelpers_AgreeWithTheHouseConvention()
        {
            Assert.That(NpcPotDay.Along(0f), Is.EqualTo(Vector2.up), "0 is North");
            Assert.That((NpcPotDay.Along(90f) - Vector2.right).magnitude, Is.LessThan(1e-5f), "90 is East");
            Assert.That((NpcPotDay.Starboard(0f) - Vector2.right).magnitude, Is.LessThan(1e-5f), "starboard of North is East");
            Assert.That((NpcPotDay.Starboard(90f) - Vector2.down).magnitude, Is.LessThan(1e-5f), "starboard of East is South");
            Assert.That(NpcPotDay.Bearing(Vector2.zero, new Vector2(-1f, 0f), 0f), Is.EqualTo(270f).Within(1e-3f));
            Assert.That(NpcPotDay.Bearing(Vector2.one, Vector2.one, 33f), Is.EqualTo(33f), "no direction: the fallback");
        }

        [Test]
        public void NpcPotIds_AreNeverAPlayerPotsId()
        {
            Assert.That(NpcPotDay.PotInstanceId(_def, 2), Is.EqualTo("npc.potday.test.2"));
        }

        [Test]
        public void ADefThatCannotMakeADay_SaysWhy_AndKeepsHerAtTheBerth()
        {
            Assert.That(NpcPotDay.Problem(_def), Is.Null);
            Assert.That(NpcPotDay.Problem(null), Is.Not.Null);

            _def.DailyShiftMeters = _def.PotSpacingMeters;
            Assert.That(NpcPotDay.Problem(_def), Does.Contain("DailyShiftMeters"));
            _def.DailyShiftMeters = 3f;

            _def.Trap = null;
            Assert.That(NpcPotDay.Problem(_def), Does.Contain("Trap"));
            NpcPotDay day = Build(1);
            Assert.That(day.PotCount, Is.EqualTo(0));
            Assert.That(day.PoseAt(day.DayStartSeconds + 0.5 * Day).Stage, Is.EqualTo(NpcPotBoatStage.AtBerth));
            Assert.That(NpcPotDay.PotAt(day, day, 0, day.DayStartSeconds).Phase, Is.EqualTo(NpcPotPhase.Soaking), "default state, no throw");
        }

        [Test]
        public void DayIndexOf_FloorsNegativeTime()
        {
            Assert.That(NpcPotDay.DayIndexOf(0.0, Day), Is.EqualTo(0));
            Assert.That(NpcPotDay.DayIndexOf(Day - 0.001, Day), Is.EqualTo(0));
            Assert.That(NpcPotDay.DayIndexOf(Day, Day), Is.EqualTo(1));
            Assert.That(NpcPotDay.DayIndexOf(-0.001, Day), Is.EqualTo(-1));
        }
    }
}
