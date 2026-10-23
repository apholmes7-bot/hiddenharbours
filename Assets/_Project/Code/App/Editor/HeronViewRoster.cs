using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>Proposed visitors. No assets are written by this table.</summary>
    public static class HeronViewRoster
    {
        public const float WalkSpeed = 1.5f;
        public const float FileGapMinutes = 4f;
        public const float DwellMinutes = 4f;
        public const float FirstTeeDwellMinutes = 16f;
        public const float BackGreenDwellMinutes = 12f;
        public const float FrontStartHour = 6.5f;
        public const float FirstBackHour = 14f;
        public const float SecondBackHour = 16.5f;
        public const float CatchUpMultiplier = 1.6f;

        public sealed class Visitor
        {
            public string Key, Name, Look, Line;
            public int Party, Slot;
            public string NpcId => "npc." + Key;
            public string RoutineId => "routine." + Key;
            public string DialogueId => "dialogue." + Key.Split('_')[0] + "_first";
            public string BuildId => "char_build." + Key;
            public string AssetName => string.Concat(Key.Split('_').Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1)));
        }

        public sealed class Day
        {
            public Visitor Person;
            public float Speed = WalkSpeed, JitterMinutes = 0f, CatchUp = CatchUpMultiplier;
            public RoutineEntry[] Blocks;
            public float[] DwellFloorMinutes;
        }

        // Names, looks and ids await the Phase B word.
        public static Visitor[] Visitors => new[] {
            Person("mara_fenwick", "Mara Fenwick", 0, 0, "Short silver hair, moss pullover, cream slacks.", "A quiet start leaves room for a better finish."),
            Person("tobin_rell", "Tobin Rell", 0, 1, "Dark curls, rust polo, slate slacks.", "That last putt will keep me thinking all day."),
            Person("elsi_brindle", "Elsi Brindle", 0, 2, "Copper braid, blue cardigan, dark slacks.", "There is always time to take the long view."),
            Person("orrin_vale", "Orrin Vale", 0, 3, "Grey beard, ochre cap, navy pullover.", "The walk is half the reason I come."),
            Person("nella_tarn", "Nella Tarn", 1, 0, "Dark bob, plum windbreaker, pale slacks.", "One good shot can carry an afternoon."),
            Person("vey_corren", "Vey Corren", 1, 1, "Close cropped hair, teal polo, charcoal slacks.", "I count the pleasant stretches before the strokes."),
            Person("ione_merrow", "Ione Merrow", 2, 0, "White curls, amber cardigan, brown slacks.", "The last green still has a surprise in it."),
            Person("pell_wickett", "Pell Wickett", 2, 1, "Sandy hair, blue cap, cream pullover.", "A steady pace gets me round."),
        };

        static Visitor Person(string key, string name, int party, int slot, string look, string line) =>
            new Visitor { Key = key, Name = name, Party = party, Slot = slot, Look = look, Line = line };

        public static string[] FrontStations(int slot) => new[] {
            CumberlandCourse.Clubhouse,
            CumberlandCourse.SlotId(true, 1, slot), CumberlandCourse.SlotId(false, 1, slot),
            CumberlandCourse.SlotId(true, 2, slot), CumberlandCourse.SlotId(false, 2, slot),
            CumberlandCourse.SlotId(true, 3, slot), CumberlandCourse.SlotId(false, 3, slot),
            CumberlandCourse.SlotId(true, 4, slot), CumberlandCourse.SlotId(false, 4, slot),
            CumberlandCourse.SlotId(true, 5, slot), CumberlandCourse.EdgeOut, CumberlandCourse.Clubhouse
        };

        public static Day[] Build(CumberlandCourse course, float secondsPerGameHour)
        {
            var frontHours = new float[12];
            frontHours[1] = FrontStartHour;
            for (int leg = 2; leg <= 10; leg++)
            {
                int arriving = leg - 1;
                float longest = Enumerable.Range(0, 4).Max(s => {
                    string[] ids = FrontStations(s);
                    return course.Distance(ids[arriving - 1], ids[arriving]);
                });
                float dwell = arriving == 1 ? FirstTeeDwellMinutes : DwellMinutes;
                frontHours[leg] = frontHours[leg - 1] +
                    RoutineSchedule.TravelHours(longest, WalkSpeed, secondsPerGameHour) + dwell / 60f;
            }
            float edgeWalk = Enumerable.Range(0, 4).Max(s => course.Distance(FrontStations(s)[9], CumberlandCourse.EdgeOut));
            frontHours[11] = frontHours[10] + RoutineSchedule.TravelHours(edgeWalk, WalkSpeed, secondsPerGameHour) + DwellMinutes / 60f;
            return Visitors.Select(person => {
                float delay = person.Slot * FileGapMinutes / 60f;
                if (person.Party == 0)
                {
                    string[] ids = FrontStations(person.Slot);
                    return new Day { Person = person,
                        Blocks = ids.Select((id, i) => Entry(i == 0 ? 0f : frontHours[i] + delay, id)).ToArray(),
                        DwellFloorMinutes = ids.Select((_, i) => i == 1 ? FirstTeeDwellMinutes : DwellMinutes).ToArray() };
                }
                float hour = (person.Party == 1 ? FirstBackHour : SecondBackHour) + delay;
                string green = CumberlandCourse.SlotId(false, 18, 3 - person.Slot);
                float longestBack = Enumerable.Range(2, 2).Max(s => course.Distance(CumberlandCourse.EdgeIn,
                    CumberlandCourse.SlotId(false, 18, s)));
                float home = hour + RoutineSchedule.TravelHours(longestBack, WalkSpeed, secondsPerGameHour)
                    + BackGreenDwellMinutes / 60f;
                return new Day { Person = person, Blocks = new[] {
                    Entry(0f, CumberlandCourse.Clubhouse), Entry(11f, CumberlandCourse.EdgeIn),
                    Entry(hour, green), Entry(home, CumberlandCourse.Clubhouse) },
                    DwellFloorMinutes = new[] { DwellMinutes, DwellMinutes, BackGreenDwellMinutes, DwellMinutes } };
            }).ToArray();
        }

        static RoutineEntry Entry(float hour, string station) => new RoutineEntry {
            StartHour = hour, StationId = station, Activity = RoutineActivity.Recreation,
            Why = "One round; the remaining holes are past the east edge." };

        public static float SecondsPerGameHour => GameConfig.DefaultSecondsPerDay / RoutineSchedule.HoursPerDay;
    }
}
