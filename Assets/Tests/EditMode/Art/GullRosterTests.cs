using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A gull belongs to a place, not to the camera.</b> <see cref="GullRoster"/> places every bird
    /// from the schedule alone; the view only chooses which of them are drawn. These pan the view away
    /// and back, run the clock, and follow one bird from its roost to its haunt — all on St Peters'
    /// places as the repo holds them (read by <see cref="GullPlaceFiles"/>, in GullScheduleTests.cs).
    /// </summary>
    public class GullRosterTests
    {
        private const int HighSummer = (int)Season.HighSummer;
        private const string ArtFolder = "Assets/_Project/Code/Art/";

        /// <summary>St Peters, its tuning and a weather, and rosters sized as the flock sizes them.</summary>
        private sealed class Coast
        {
            public readonly GullRegionPlaces Region = GullPlaceFiles.Region(GullPlaceFiles.StPeters);
            public readonly GullTuningSpec Tuning = GullPlaceFiles.Tuning();
            public readonly GullHauntSpec[] Places;
            public IGullWeather Weather = GullTestWeather.Steady(GullSky.Calm.Storm, GullPlaceFiles.LowWater);

            public Coast()
            {
                Places = Region.Places;
            }

            public GullRoster NewRoster() => new GullRoster(Places.Length, GullFlock.MaxNearBirds, GullConfig.Default.Count);

            public GullRoster Refresh(GullRoster roster, GullCalendar calendar, GullRect view)
            {
                roster.Refresh(Places, Places.Length, Places.Length, Tuning, GullPlaceFiles.Settings(Region.Bounds),
                               calendar, Weather, view);
                return roster;
            }

            public GullRoster At(GullCalendar calendar, GullRect view) => Refresh(NewRoster(), calendar, view);
        }

        private static HashSet<int> NearKeys(GullRoster roster)
        {
            var keys = new HashSet<int>();
            for (int i = 0; i < roster.StoredCount; i++) keys.Add(roster.Near[i].Key);
            return keys;
        }

        private static bool Present(GullRoster roster, int place, int slot)
            => roster.StateOf(place, slot, out GullRosterBird bird)
               && (bird.Where == GullWhere.AtHaunt || (bird.Where == GullWhere.Commuting && bird.At == place));

        [Test]
        public void MovingTheCameraDoesNotMoveAHauntsBirds()
        {
            var coast = new Coast();
            GullCalendar dawn = GullPlaceFiles.Calendar(3, 6.5, HighSummer);
            GullRoster here = coast.At(dawn, GullPlaceFiles.CanneryView);
            GullRoster there = coast.At(dawn, GullPlaceFiles.AwayView);

            var failures = new List<string>();
            var states = new Dictionary<(int, int), GullRosterBird>();
            int placed = 0;
            foreach ((int p, int slot) in GullPlaceFiles.Slots(here, coast.Region.Count))
            {
                bool a = here.StateOf(p, slot, out GullRosterBird x);
                bool b = there.StateOf(p, slot, out GullRosterBird y);
                if (a != b || !x.Equals(y))
                    failures.Add(coast.Region.IdOf(p) + " slot " + slot + ": " + GullPlaceFiles.Describe(x) + " with the view on the " +
                                 "cannery, " + GullPlaceFiles.Describe(y) + " with it away");
                states[(p, slot)] = x;
                if (a) placed++;
            }

            HashSet<int> nearHere = NearKeys(here), nearThere = NearKeys(there);
            Assert.That(nearHere.Count, Is.GreaterThan(0), "no bird near the cannery at dawn");
            Assert.IsFalse(nearHere.SetEquals(nearThere), "the two views drew the same birds: the test proves nothing");

            // Swap the views on the same rosters: still nobody moves.
            coast.Refresh(here, dawn, GullPlaceFiles.AwayView);
            coast.Refresh(there, dawn, GullPlaceFiles.CanneryView);
            foreach ((int p, int slot) in GullPlaceFiles.Slots(here, coast.Region.Count))
            {
                here.StateOf(p, slot, out GullRosterBird x);
                there.StateOf(p, slot, out GullRosterBird y);
                GullRosterBird was = states[(p, slot)];
                if (!x.Equals(was) || !y.Equals(was))
                    failures.Add(coast.Region.IdOf(p) + " slot " + slot + " moved when the view moved: " +
                                 GullPlaceFiles.Describe(was) + " -> " + GullPlaceFiles.Describe(x) + " / " + GullPlaceFiles.Describe(y));
            }

            Assert.That(placed, Is.GreaterThan(0), "no bird placed at dawn");
            GullAssert.None(failures, "a bird moved with the camera");
        }

        [Test]
        public void ABirdLeftBehindIsWhereItWasWhenYouComeBack()
        {
            var coast = new Coast();
            GullCalendar eight = GullPlaceFiles.Calendar(3, 8.0, HighSummer);
            GullCalendar nine = GullPlaceFiles.Calendar(3, 9.0, HighSummer);

            // A: watch the cannery at eight, look away at nine, look back.
            GullRoster a = coast.At(eight, GullPlaceFiles.CanneryView);
            coast.Refresh(a, nine, GullPlaceFiles.AwayView);
            coast.Refresh(a, nine, GullPlaceFiles.CanneryView);
            // B: arrive at the cannery at nine.
            GullRoster b = coast.At(nine, GullPlaceFiles.CanneryView);

            var failures = new List<string>();
            foreach ((int p, int slot) in GullPlaceFiles.Slots(a, coast.Region.Count))
            {
                bool inA = a.StateOf(p, slot, out GullRosterBird x);
                bool inB = b.StateOf(p, slot, out GullRosterBird y);
                if (inA != inB || !x.Equals(y))
                    failures.Add(coast.Region.IdOf(p) + " slot " + slot + ": " + GullPlaceFiles.Describe(x) + " vs " +
                                 GullPlaceFiles.Describe(y));
            }
            if (a.NearCount != b.NearCount) failures.Add("near " + a.NearCount + " vs " + b.NearCount);
            if (a.StoredCount != b.StoredCount) failures.Add("stored " + a.StoredCount + " vs " + b.StoredCount);
            for (int i = 0; i < Math.Min(a.StoredCount, b.StoredCount); i++)
            {
                GullRosterBird x = a.Near[i], y = b.Near[i];
                if (x.Key != y.Key || x.X != y.X || x.Y != y.Y)
                    failures.Add("near bird " + i + ": key " + x.Key + " at " + GullPlaceFiles.Describe(x) + " vs key " + y.Key +
                                 " at " + GullPlaceFiles.Describe(y));
                if (a.NearPool[i] != b.NearPool[i]) failures.Add("near bird " + i + " drawn by " + a.NearPool[i] + " vs " + b.NearPool[i]);
            }
            for (int i = 0; i < a.PoolKey.Length; i++)
                if (a.PoolKey[i] != b.PoolKey[i]) failures.Add("pool bird " + i + " draws " + a.PoolKey[i] + " vs " + b.PoolKey[i]);

            Assert.That(b.NearCount, Is.GreaterThan(0), "no bird near the cannery at nine");
            GullAssert.None(failures, "the birds were not where they would have been");
        }

        [Test]
        public void TheClockMovesThemOn()
        {
            var coast = new Coast();
            double period = 12.42 * GullPlaceFiles.SecondsPerHour;
            coast.Weather = new GullTestWeather(s => GullSky.Calm.Storm,
                                                s => GullPlaceFiles.HighWater * Math.Cos(2.0 * Math.PI * s / period));
            GullRoster nine = coast.At(GullPlaceFiles.Calendar(3, 9.0, HighSummer), GullPlaceFiles.CanneryView);
            GullRoster eleven = coast.At(GullPlaceFiles.Calendar(3, 11.0, HighSummer), GullPlaceFiles.CanneryView);

            var failures = new List<string>();
            int leavers = 0, present = 0;
            for (int p = 0; p < coast.Region.Count; p++)
            {
                GullHauntSpec place = coast.Places[p];
                if (place.IsRoost) continue;
                nine.SlotRange(p, out int first, out int end);
                for (int slot = first; slot < end; slot++)
                {
                    double threshold = GullSchedule.SlotThreshold(GullPlaceFiles.Seed, place.IdHash, slot, 3);
                    foreach ((string when, GullRoster roster) in new[] { ("09:00", nine), ("11:00", eleven) })
                    {
                        bool here = Present(roster, p, slot);
                        bool due = roster.NowExpected(p) > threshold;
                        if (here) present++;
                        if (here != due)
                            failures.Add(coast.Region.IdOf(p) + " slot " + slot + " at " + when + ": " + (here ? "there" : "gone") +
                                         " with " + roster.NowExpected(p) + " expected against " + threshold);
                    }
                    if (Present(nine, p, slot) && !Present(eleven, p, slot)) leavers++;
                }
            }

            Assert.That(present, Is.GreaterThan(0), "no bird at any haunt");
            Assert.That(leavers, Is.GreaterThan(0), "nobody left between nine and eleven: the clock moved no one");
            GullAssert.None(failures, "the birds did not follow the clock");
        }

        [Test]
        public void CommutersFlyBetweenRoostAndHaunt()
        {
            var coast = new Coast();
            GullTuningSpec t = coast.Tuning;
            int cannery = coast.Region.Index("birdhaunt.stp_cannery");
            int whelps = coast.Region.Index("birdroost.stp_whelps");
            GullHauntSpec place = coast.Places[cannery];
            Assert.That(place.RoostIndex, Is.EqualTo(whelps), "the cannery's birds sleep on the Whelps");

            GullCalendar dawn = GullPlaceFiles.Calendar(3, 5.0, HighSummer);
            double Expected(double seconds)
                => GullSchedule.Expected(t, place, GullSchedule.InputsAt(t, dawn, coast.Weather, seconds));

            // A bird that sleeps at the roost (its home number is under every daytime roost share) and
            // whose count is crossed inside the dawn ramp, clear of the shift's jump at seven.
            double from = GullPlaceFiles.At(3, 5.5), to = GullPlaceFiles.At(3, 6.99);
            double before = Expected(from), ramp = Expected(to);
            int slot = -1;
            double threshold = 0.0;
            for (int k = 0; k < place.Capacity && slot < 0; k++)
            {
                double home = GullHash.Unit(GullPlaceFiles.Seed, place.IdHash, k, 0, GullHash.TagHome);
                double th = GullSchedule.SlotThreshold(GullPlaceFiles.Seed, place.IdHash, k, 3);
                if (home < 0.2 && th > before && th < ramp - 0.05)
                {
                    slot = k;
                    threshold = th;
                }
            }
            Assert.That(slot, Is.GreaterThanOrEqualTo(0), "no cannery bird sleeps at the roost and arrives in the dawn ramp");

            // The moment its count is crossed.
            double lo = from, hi = to;
            while (hi - lo > 1e-9)
            {
                double mid = 0.5 * (lo + hi);
                if (Expected(mid) > threshold) hi = mid;
                else lo = mid;
            }
            double crossing = hi;

            // A second before: asleep on its own spot on the rock.
            GullRoster asleep = coast.At(GullPlaceFiles.CalendarAt(crossing - 1.0, HighSummer), GullPlaceFiles.CanneryView);
            Assert.IsTrue(asleep.StateOf(cannery, slot, out GullRosterBird sleeper), "a second before, the bird is away");
            Assert.That(sleeper.Where, Is.EqualTo(GullWhere.AtRoost), "a second before: " + GullPlaceFiles.Describe(sleeper));
            asleep.RoostVisitorSpot(whelps, cannery, slot, out double fx, out double fy);
            Assert.That(sleeper.At, Is.EqualTo(whelps));
            Assert.That(sleeper.X, Is.EqualTo(fx));
            Assert.That(sleeper.Y, Is.EqualTo(fy));

            // Just after: in the air on the line from the rock to its spot at the haunt.
            double after = crossing + 0.05;
            GullRoster flying = coast.At(GullPlaceFiles.CalendarAt(after, HighSummer), GullPlaceFiles.CanneryView);
            Assert.IsTrue(flying.StateOf(cannery, slot, out GullRosterBird bird), "just after, the bird is away");
            Assert.That(bird.Where, Is.EqualTo(GullWhere.Commuting), "just after: " + GullPlaceFiles.Describe(bird));
            Assert.That(bird.From, Is.EqualTo(whelps), "it leaves the Whelps");
            Assert.That(bird.At, Is.EqualTo(cannery), "it is bound for the cannery");
            Assert.That(bird.DepartSeconds, Is.EqualTo(crossing).Within(GullRoster.SwitchToleranceSeconds + 1e-6),
                        "it leaves when its count is crossed");
            flying.HauntSpot(cannery, slot, 3, out double tx, out double ty);
            Assert.That(bird.FromX, Is.EqualTo(fx));
            Assert.That(bird.FromY, Is.EqualTo(fy));
            Assert.That(bird.ToX, Is.EqualTo(tx));
            Assert.That(bird.ToY, Is.EqualTo(ty));
            double length = Math.Sqrt((tx - fx) * (tx - fx) + (ty - fy) * (ty - fy));
            Assert.That(bird.FlightSeconds, Is.EqualTo(length / GullConfig.Default.CommuteSpeedMetresPerSecond).Within(1e-9),
                        "it flies at the commute speed");
            double u = bird.ProgressAt(after);
            Assert.That(u, Is.GreaterThan(0.0).And.LessThan(0.05), "it has only just left");
            Assert.That(bird.X, Is.EqualTo(fx + (tx - fx) * u).Within(1e-9));
            Assert.That(bird.Y, Is.EqualTo(fy + (ty - fy) * u).Within(1e-9));

            // Half a second after it lands: down on its spot.
            double landed = bird.DepartSeconds + bird.FlightSeconds + 0.5;
            GullRoster down = coast.At(GullPlaceFiles.CalendarAt(landed, HighSummer), GullPlaceFiles.CanneryView);
            Assert.IsTrue(down.StateOf(cannery, slot, out GullRosterBird there), "after landing, the bird is away");
            Assert.That(there.Where, Is.EqualTo(GullWhere.AtHaunt), "after landing: " + GullPlaceFiles.Describe(there));
            Assert.That(there.FlightSeconds, Is.EqualTo(0.0));
            Assert.That(there.X, Is.EqualTo(tx));
            Assert.That(there.Y, Is.EqualTo(ty));
        }

        /// <summary>
        /// The rule in code: nothing that places a bird reads the camera. The roster, the schedule and
        /// the places never name it; the flock names it in one place only, where it reads the view.
        /// </summary>
        [Test]
        public void NoBirdIsPlacedRelativeToTheCamera()
        {
            foreach (string file in new[] { "GullRoster.cs", "GullSchedule.cs", "BirdHauntDef.cs", "BirdHauntLibrary.cs" })
            {
                string code = CodeOnly(File.ReadAllText(ArtFolder + file));
                Match named = Regex.Match(code, @"\bCamera\b");
                Assert.IsFalse(named.Success, file + " names the camera at line " + LineOf(code, named.Index));
            }

            string flock = CodeOnly(File.ReadAllText(ArtFolder + "GullFlock.cs"));
            Match view = Regex.Match(flock, @"\bstatic\s+bool\s+ViewRectWorld\s*\(");
            Assert.IsTrue(view.Success, "GullFlock.cs has no ViewRectWorld: the one place the view is read");
            int open = flock.IndexOf('{', view.Index);
            int close = MatchingBrace(flock, open);
            Assert.That(close, Is.GreaterThan(open), "ViewRectWorld's body does not close");

            MatchCollection uses = Regex.Matches(flock, @"\bCamera\b|ResolveCamera");
            Assert.That(uses.Count, Is.GreaterThan(0), "the flock never reads the view: the test lost its case");
            var failures = new List<string>();
            foreach (Match use in uses)
                if (use.Index < open || use.Index > close)
                    failures.Add("GullFlock.cs line " + LineOf(flock, use.Index) + " reads the camera outside ViewRectWorld");
            GullAssert.None(failures, "the camera is read where birds are placed");
        }

        [Test]
        public void TheNearestFillThePoolFirst_TheRestAreCounts()
        {
            var places = new[]
            {
                new GullHauntSpec
                {
                    IdHash = GullHash.Id("birdhaunt.test_always_on"),
                    Kind = BirdHauntKind.Loafing,
                    AlwaysOn = true,
                    HalfX = 10.0,
                    HalfY = 10.0,
                    RoostIndex = -1,
                    Capacity = 60,
                },
            };
            var roster = new GullRoster(1, 64, 40);
            roster.Refresh(places, 1, 1, GullPlaceFiles.Tuning(), GullPlaceFiles.Settings(default),
                           GullPlaceFiles.Calendar(3, 12.0, HighSummer),
                           GullTestWeather.Steady(GullSky.Calm.Storm, GullPlaceFiles.LowWater), GullRect.Box(0.0, 0.0, 14.2, 8.0));

            Assert.That(roster.NearCount, Is.EqualTo(60), "every bird is near");
            Assert.That(roster.StoredCount, Is.EqualTo(60), "every bird is listed");
            Assert.That(roster.BoundCount, Is.EqualTo(40), "the pool is full");

            var failures = new List<string>();
            for (int i = 1; i < roster.StoredCount; i++)
            {
                GullRosterBird a = roster.Near[i - 1], b = roster.Near[i];
                bool ordered = a.DistanceSq < b.DistanceSq || (a.DistanceSq == b.DistanceSq && a.Key < b.Key);
                if (!ordered) failures.Add("near bird " + i + " is nearer than the one before it");
            }
            for (int i = 0; i < roster.StoredCount; i++)
            {
                if (roster.Near[i].Where != GullWhere.AtHaunt) failures.Add("near bird " + i + " is " + roster.Near[i].Where);
                int want = i < 40 ? i : -1;
                if (roster.NearPool[i] != want) failures.Add("near bird " + i + " is drawn by " + roster.NearPool[i] + ", not " + want);
            }
            for (int i = 0; i < roster.PoolKey.Length; i++)
                if (roster.PoolKey[i] != roster.Near[i].Key)
                    failures.Add("pool bird " + i + " draws key " + roster.PoolKey[i] + ", not the " + i + "th nearest");

            GullAssert.None(failures, "the pool did not take the nearest birds");
        }

        // ── reading code ────────────────────────────────────────────────────────────────────────

        /// <summary>The source with its comments and the insides of its string and char literals
        /// blanked; lines and offsets are kept.</summary>
        private static string CodeOnly(string source)
        {
            var sb = new StringBuilder(source.Length);
            int i = 0, n = source.Length;
            while (i < n)
            {
                char c = source[i];
                char next = i + 1 < n ? source[i + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    while (i < n && source[i] != '\n')
                    {
                        sb.Append(source[i] == '\r' ? '\r' : ' ');
                        i++;
                    }
                }
                else if (c == '/' && next == '*')
                {
                    sb.Append("  ");
                    i += 2;
                    while (i < n && !(source[i] == '*' && i + 1 < n && source[i + 1] == '/'))
                    {
                        sb.Append(source[i] == '\n' || source[i] == '\r' ? source[i] : ' ');
                        i++;
                    }
                    if (i < n)
                    {
                        sb.Append("  ");
                        i += 2;
                    }
                }
                else if (c == '@' && next == '"')
                {
                    sb.Append("@\"");
                    i += 2;
                    while (i < n)
                    {
                        if (source[i] == '"')
                        {
                            if (i + 1 < n && source[i + 1] == '"')
                            {
                                sb.Append("  ");
                                i += 2;
                                continue;
                            }
                            break;
                        }
                        sb.Append(source[i] == '\n' || source[i] == '\r' ? source[i] : ' ');
                        i++;
                    }
                    if (i < n)
                    {
                        sb.Append('"');
                        i++;
                    }
                }
                else if (c == '"' || c == '\'')
                {
                    sb.Append(c);
                    i++;
                    while (i < n && source[i] != c && source[i] != '\n')
                    {
                        if (source[i] == '\\' && i + 1 < n)
                        {
                            sb.Append("  ");
                            i += 2;
                            continue;
                        }
                        sb.Append(' ');
                        i++;
                    }
                    if (i < n && source[i] == c)
                    {
                        sb.Append(c);
                        i++;
                    }
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            return sb.ToString();
        }

        private static int MatchingBrace(string code, int open)
        {
            if (open < 0) return -1;
            int depth = 0;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == '{') depth++;
                else if (code[i] == '}' && --depth == 0) return i;
            }
            return -1;
        }

        private static int LineOf(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
                if (text[i] == '\n') line++;
            return line;
        }
    }
}
