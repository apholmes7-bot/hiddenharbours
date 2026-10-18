using System;
using System.Collections.Generic;
using System.Globalization;
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

    /// <summary>
    /// <b>A gull asked down comes down, and a paused flock waits.</b> The flock asks a bird in the air to
    /// put down at its place whenever her settle window comes due — and the departures that come before
    /// the window (getting up to commute, off ground she has wandered from, or with a catch) leave it
    /// where it was, so it can come due while she is still getting up.
    /// These ask a bird down from every airborne state the flock can find her in, and run a flock against
    /// a paused clock, on the state table built from the committed def as the repo holds it (read by
    /// <see cref="GullYaml"/>, in GullScheduleTests.cs).
    /// </summary>
    public class GullFlockDecisionTests
    {
        private const string DefFile = "Assets/_Project/Resources/" + SeagullVisualDef.ResourcesPath + ".asset";
        private const int TicksPerSecond = 30;

        [Test]
        public void AGullAskedDownNeverHangsInTheAir()
        {
            SeagullBehaviour table = Table();
            int land = table.IndexOf(SeagullStates.Land);
            var slot = WheelSlot(table);
            var refused = new HashSet<string>();
            var failures = new List<string>();

            foreach ((string label, SeagullSimBird found) in BirdsInTheAir(table, slot))
            {
                SeagullSimBird bird = found;
                if (!GullFlock.CanBeAskedDown(table, bird)) { refused.Add(table.IdOf(bird.State)); continue; }
                if (!SeagullStateMachine.CommandAlight(ref bird, land, 6.0, 0.0, 0.0, table))
                {
                    failures.Add(label + ": the flock may ask her down but the table refuses the descent");
                    continue;
                }
                string hang = FirstSecondWithoutDescent(table, ref bird, slot);
                if (hang != null) failures.Add(label + ": " + hang);
            }

            foreach (string wheel in new[] { SeagullStates.Fly, SeagullStates.Glide, SeagullStates.Swoop })
                if (refused.Contains(wheel))
                    failures.Add(wheel + " is never asked down: the wheel's own birds could not settle");
            GullAssert.None(failures, "a gull asked down hangs in the air");
        }

        /// <summary>
        /// A gull back in the air rejoins her wheel over the regroup time, and is not asked down until she
        /// has. Asked down on the way, she can start down from about a metre up, and her path to the spot
        /// is paced by the height she loses: she covers it in a fraction of a second and zips there. Takes a
        /// gull off the ground and asks, at every tick until she is back on her wheel, whether the flock may
        /// ask her down: never, and then at once.
        /// </summary>
        [Test]
        public void AGullAskedDownWhileSheRejoinsIsTurnedAway()
        {
            SeagullBehaviour table = Table();
            var slot = WheelSlot(table);
            var bird = new SeagullSimBird { IntentState = -1 };
            SeagullStateMachine.Enter(ref bird, table.IndexOf(SeagullStates.Stand), table);
            Assert.IsTrue(SeagullStateMachine.CommandDepart(ref bird, table), "a standing gull cannot take off");

            var failures = new List<string>();
            int limit = (int)Math.Ceiling(table.Flock.RegroupSeconds * TicksPerSecond) + TicksPerSecond;
            for (int t = 1; t <= limit && !bird.FlockBound; t++)
            {
                Tick(table, ref bird, slot);
                if (bird.FlockBound || !GullFlock.CanBeAskedDown(table, bird)) continue;
                int ms = (int)Math.Round(t * 1000.0 / TicksPerSecond);
                failures.Add(ms + " ms after she got up, still rejoining (" + table.IdOf(bird.State) + " at " +
                             Metres(bird.AltitudeMetres) + ")");
            }

            Assert.IsTrue(bird.FlockBound, "she never rejoined her wheel");
            Assert.IsTrue(GullFlock.CanBeAskedDown(table, bird), "back on her wheel, she is never asked down");
            GullAssert.None(failures, "a gull still rejoining her wheel is asked down");
        }

        /// <summary>
        /// A gull asked down is handed back to the wheel if she has not arrived by her deadline. A glider
        /// stays in <c>glide</c> all the way down and sinks at 0.4 m/s: from high on the wheel her glide-in
        /// alone outlasts a settle window, and a deadline of one window pops her back onto the wheel while
        /// she is still coming down. Asks down every bird the flock would ask, at the bottom and the top of
        /// the wheel's band, onto the ground and onto the water, and flies each of them down: every one
        /// arrives before her deadline.
        /// </summary>
        [Test]
        public void AGliderAskedDownFromHighIsNeverPoppedBack()
        {
            SeagullBehaviour table = Table();
            var slot = WheelSlot(table);
            var failures = new List<string>();
            int descents = 0;

            foreach ((string label, SeagullSimBird found) in BirdsInTheAir(table, slot))
            {
                if (!GullFlock.CanBeAskedDown(table, found)) continue;
                foreach (string arrival in new[] { SeagullStates.Land, SeagullStates.Splash })
                {
                    SeagullSimBird bird = found;
                    if (!SeagullStateMachine.CommandAlight(ref bird, table.IndexOf(arrival), 6.0, 0.0, 0.0, table))
                    {
                        failures.Add(label + ", down to " + arrival + ": the table refuses the descent");
                        continue;
                    }
                    descents++;
                    string popped = HandedBackBeforeSheArrives(table, ref bird, slot,
                                                               GullFlock.AlightAllowanceMilliseconds(table, bird));
                    if (popped != null) failures.Add(label + ", down to " + arrival + ": " + popped);
                }
            }

            Assert.That(descents, Is.GreaterThan(0), "the flock would ask no bird down");
            GullAssert.None(failures, "a gull asked down is handed back to the wheel before she arrives");
        }

        /// <summary>
        /// The game's pause holds the whole flock. A commuter rides the game's clock, which a pause stops;
        /// the wheel and every bird's flight run on the flock's own unscaled clock, which a pause alone does
        /// not. Lays a flock out in every kind of flight — on her wheel, on a commute, rejoining her wheel
        /// after a takeoff, gliding down from high, afloat — then pauses the game's clock and steps the
        /// flock as <see cref="GullFlock.Tick"/> does for three seconds: no bird moves. Then lets the clock
        /// run again, so the test cannot pass on birds that would not have moved anyway.
        /// </summary>
        [Test]
        public void APausedFlockMovesNoBird()
        {
            SeagullBehaviour table = Table();
            var clock = new PausableClock { TotalSeconds = 9.0 * 3600.0 };
            var line = new GullRosterBird
            {
                Where = GullWhere.Commuting,
                FromX = -40.0, FromY = 0.0, ToX = 40.0, ToY = 0.0,
                DepartSeconds = clock.TotalSeconds,
                FlightSeconds = 80.0 / GullConfig.Default.CommuteSpeedMetresPerSecond,
            };

            string[] labels =
            {
                "on her wheel", "on a commute", "rejoining her wheel after a takeoff", "gliding down from high",
                "afloat",
            };
            const int commuter = 1;
            var birds = new SeagullSimBird[labels.Length];
            for (int i = 0; i < 2; i++)
            {
                birds[i] = new SeagullSimBird { IntentState = -1, FlockBound = true };
                SeagullStateMachine.Enter(ref birds[i], table.IndexOf(SeagullStates.Fly), table);
            }
            birds[2] = new SeagullSimBird { IntentState = -1 };
            SeagullStateMachine.Enter(ref birds[2], table.IndexOf(SeagullStates.Stand), table);
            Assert.IsTrue(SeagullStateMachine.CommandDepart(ref birds[2], table), "a standing gull cannot take off");
            birds[3] = new SeagullSimBird { IntentState = -1, FlockBound = true };
            SeagullStateMachine.Enter(ref birds[3], table.IndexOf(SeagullStates.Glide), table);
            birds[3].AltitudeMetres = table.Flock.AltitudeMaxMetres;
            Assert.IsTrue(SeagullStateMachine.CommandAlight(ref birds[3], table.IndexOf(SeagullStates.Land),
                                                            6.0, 0.0, 0.0, table), "a glider cannot be asked down");
            birds[4] = new SeagullSimBird { IntentState = -1 };
            SeagullStateMachine.Enter(ref birds[4], table.IndexOf(SeagullStates.Float), table);

            var wheel = new SeagullFlockMath.SeagullFlockBird[birds.Length];
            double flockMilliseconds = 0.0;
            for (int t = 0; t < TicksPerSecond; t++)
                FlockTick(table, clock, ref flockMilliseconds, birds, wheel, commuter, line);
            var laidOut = (SeagullSimBird[])birds.Clone();

            clock.IsPaused = true;
            var failures = new List<string>();
            var moved = new bool[birds.Length];
            for (int t = 1; t <= 3 * TicksPerSecond; t++)
            {
                FlockTick(table, clock, ref flockMilliseconds, birds, wheel, commuter, line);
                for (int i = 0; i < birds.Length; i++)
                {
                    if (moved[i] || SamePose(birds[i], laidOut[i])) continue;
                    moved[i] = true;
                    failures.Add(labels[i] + ": moved " + (int)Math.Round(t * 1000.0 / TicksPerSecond) +
                                 " ms into the pause, " + Pose(table, laidOut[i]) + " to " + Pose(table, birds[i]));
                }
            }
            GullAssert.None(failures, "a paused flock moves its birds");

            clock.IsPaused = false;
            for (int t = 0; t < TicksPerSecond; t++)
                FlockTick(table, clock, ref flockMilliseconds, birds, wheel, commuter, line);
            var still = new List<string>();
            for (int i = 0; i < birds.Length; i++)
                if (SamePose(birds[i], laidOut[i])) still.Add(labels[i]);
            Assert.That(still, Is.Empty, "with the clock running again these never moved: the pause proves nothing for them");
            Assert.IsFalse(GullFlock.HeldByThePause(new PausableClock { IsPaused = true }, pinned: true),
                           "a pinned fixture keeps no game time, so no pause holds it");
        }

        /// <summary>
        /// Every way the flock can find a bird in the air when her window comes due: on the wheel in each
        /// of its three states, at the bottom and the top of its band; at every tick of a takeoff off the
        /// ground, as a departure leaves her; and in each other airborne state at the height it is
        /// entered at.
        /// </summary>
        private static List<(string Label, SeagullSimBird Bird)> BirdsInTheAir(
            SeagullBehaviour table, in SeagullFlockMath.SeagullFlockBird slot)
        {
            var birds = new List<(string, SeagullSimBird)>();
            int takeoff = table.IndexOf(SeagullStates.Takeoff);
            foreach (string id in SeagullStates.Order)
            {
                int state = table.IndexOf(id);
                if (table.SurfaceOf(state) != SeagullSurface.None) continue;

                if (table.IsFlockDriven(state))
                {
                    foreach (double band in new[] { table.Flock.AltitudeMinMetres, table.Flock.AltitudeMaxMetres })
                    {
                        var bird = new SeagullSimBird { IntentState = -1, FlockBound = true };
                        SeagullStateMachine.Enter(ref bird, state, table);
                        bird.AltitudeMetres = Math.Min(band, table.AltitudeMax(state));
                        birds.Add((id + " on the wheel at " + Metres(bird.AltitudeMetres), bird));
                    }
                }
                else if (state == takeoff)
                {
                    var bird = new SeagullSimBird { IntentState = -1 };
                    SeagullStateMachine.Enter(ref bird, table.IndexOf(SeagullStates.Stand), table);
                    Assert.IsTrue(SeagullStateMachine.CommandDepart(ref bird, table), "a standing gull cannot take off");
                    for (int t = 1; t <= TicksPerSecond * 10; t++)
                    {
                        Tick(table, ref bird, slot);
                        if (bird.State != takeoff) break;
                        int ms = (int)Math.Round(t * 1000.0 / TicksPerSecond);
                        birds.Add(("takeoff " + ms + " ms up, at " + Metres(bird.AltitudeMetres), bird));
                    }
                }
                else
                {
                    var bird = new SeagullSimBird { IntentState = -1 };
                    SeagullStateMachine.Enter(ref bird, state, table);
                    bird.AltitudeMetres = table.EntryAltitude(state);
                    birds.Add((id + " at " + Metres(bird.AltitudeMetres), bird));
                }
            }
            return birds;
        }

        /// <summary>Runs one settle window — as long as the flock gives a descent before it hands her
        /// back to the wheel — and names the first whole second in which she lost no height and reached
        /// no surface; null when there is none.</summary>
        private static string FirstSecondWithoutDescent(SeagullBehaviour table, ref SeagullSimBird bird,
                                                        in SeagullFlockMath.SeagullFlockBird slot)
        {
            double atLastSecond = bird.AltitudeMetres;
            for (int s = 1; s <= table.Flock.SettleAfterSeconds; s++)
            {
                for (int t = 0; t < TicksPerSecond; t++)
                {
                    Tick(table, ref bird, slot);
                    if (table.SurfaceOf(bird.State) != SeagullSurface.None) return null;
                }
                if (bird.AltitudeMetres > atLastSecond - 1e-6)
                    return "in second " + s + " she lost no height (" + table.IdOf(bird.State) + " at " +
                           Metres(bird.AltitudeMetres) + ")";
                atLastSecond = bird.AltitudeMetres;
            }
            return null;
        }

        /// <summary>One tick of the flock's step for a single bird: the step, then the arrival.</summary>
        private static void Tick(SeagullBehaviour table, ref SeagullSimBird bird,
                                 in SeagullFlockMath.SeagullFlockBird slot)
        {
            SeagullStateMachine.Step(ref bird, 1000.0 / TicksPerSecond, table, in slot, 0.0, 0.0);
            SeagullStateMachine.TryAlight(ref bird, table);
        }

        /// <summary>Flies a bird asked down until she arrives, and names where she is when her deadline
        /// hands her back to the wheel first, as the flock's decision does; null when she arrives in time.</summary>
        private static string HandedBackBeforeSheArrives(SeagullBehaviour table, ref SeagullSimBird bird,
                                                         in SeagullFlockMath.SeagullFlockBird slot,
                                                         double deadlineMilliseconds)
        {
            for (int t = 0; bird.IntentState >= 0; t++)
            {
                double elapsed = t * 1000.0 / TicksPerSecond;
                if (!(elapsed < deadlineMilliseconds))
                    return "handed back after " + Seconds(elapsed) + ", still coming down (" +
                           table.IdOf(bird.State) + " at " + Metres(bird.AltitudeMetres) + ")";
                Tick(table, ref bird, slot);
            }
            return null;
        }

        /// <summary>
        /// One tick of a laid-out flock's flight, stepped as <see cref="GullFlock.Tick"/> steps it (less its
        /// decisions, its breeze and its drawing): the game's clock runs on unless it is paused, and unless
        /// the pause holds the flock its own clock runs on too, the wheel turns, the commuter's centre is
        /// her point on her line at the game's time, and every bird takes her step and arrives.
        /// </summary>
        private static void FlockTick(SeagullBehaviour table, PausableClock clock, ref double flockMilliseconds,
                                      SeagullSimBird[] birds, SeagullFlockMath.SeagullFlockBird[] wheel,
                                      int commuter, GullRosterBird line)
        {
            const double tick = 1000.0 / TicksPerSecond;
            clock.Run(tick / 1000.0);
            if (GullFlock.HeldByThePause(clock, pinned: false)) return;

            flockMilliseconds += tick;
            GullConfig config = GullConfig.Default;
            SeagullFlockMath.Flock(birds.Length, flockMilliseconds, config.Seed, config.WheelRadiusMetres,
                                   table.FlockTuning, wheel);
            for (int i = 0; i < birds.Length; i++)
            {
                double centreX = 0.0, centreY = 0.0;
                if (i == commuter) line.PointAt(clock.TotalSeconds, out centreX, out centreY);
                SeagullStateMachine.Step(ref birds[i], tick, table, in wheel[i], centreX, centreY);
                SeagullStateMachine.TryAlight(ref birds[i], table);
            }
        }

        /// <summary>Is she drawn as she was: the same spot, height, state and frame?</summary>
        private static bool SamePose(in SeagullSimBird a, in SeagullSimBird b)
            => a.X == b.X && a.Y == b.Y && a.AltitudeMetres == b.AltitudeMetres && a.State == b.State &&
               a.Frame == b.Frame;

        private static string Pose(SeagullBehaviour table, in SeagullSimBird bird)
            => table.IdOf(bird.State) + " frame " + bird.Frame + " at (" +
               bird.X.ToString("0.00", CultureInfo.InvariantCulture) + ", " +
               bird.Y.ToString("0.00", CultureInfo.InvariantCulture) + "), " + Metres(bird.AltitudeMetres) + " up";

        /// <summary>The game's clock as the flock reads it: its seconds, which stand still while it is
        /// paused.</summary>
        private sealed class PausableClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public void Run(double seconds) { if (!IsPaused) TotalSeconds += seconds * TimeScale; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 9f;
            public float DayFraction => 9f / 24f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        /// <summary>Her place on the wheel, at the bottom of its band: where a rising bird rejoins.</summary>
        private static SeagullFlockMath.SeagullFlockBird WheelSlot(SeagullBehaviour table)
            => new SeagullFlockMath.SeagullFlockBird(0.0, 0.0, table.Flock.AltitudeMinMetres, 0.0, 0,
                                                     SeagullFlockMath.SeagullFlockAnim.Fly, 0, 1.0);

        private static string Metres(double metres) => metres.ToString("0.00", CultureInfo.InvariantCulture) + " m";

        private static string Seconds(double milliseconds)
            => (milliseconds / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s";

        /// <summary>The state table, field by field as <see cref="SeagullVisualDef.Behaviour"/> builds it,
        /// from the committed asset and without the asset database.</summary>
        private static SeagullBehaviour Table()
        {
            GullYaml def = GullYaml.ReadAsset(DefFile);
            var rows = new List<SeagullStateRow>();
            foreach (GullYaml s in def["_states"].Items)
                rows.Add(new SeagullStateRow(
                    s["State"].Scalar, s["Frames"].AsInt, s["FrameMilliseconds"].AsFloat,
                    s["SpeedMetresPerSecond"].AsFloat, s["ClimbMetresPerSecond"].AsFloat,
                    s["AltitudeA"].AsFloat, s["AltitudeB"].AsFloat, s["Loop"].AsBool,
                    string.IsNullOrEmpty(s["Next"].Scalar) ? null : s["Next"].Scalar,
                    s["TravelMetres"].AsFloat, s["CyclesMin"].AsInt, s["CyclesMax"].AsInt));
            var edges = def["_transitions"].Items.Select(e => new SeagullEdge(e["From"].Scalar, e["To"].Scalar)).ToList();

            var clear = def["_landNeedsClear"].AsVector2;
            return new SeagullBehaviour(
                rows, edges,
                new SeagullLandRules(clear.x, clear.y, def["_landApproachIntoWind"].AsBool,
                                     def["_landMinFlatMetres"].AsFloat),
                new SeagullWaterRules(def["_floatBodyZMetres"].AsFloat, def["_splashBurstFrame"].AsInt,
                                      def["_driftWithCurrent"].AsBool, def["_rockRollDegreesMax"].AsFloat,
                                      def["_rockHeavePixelsMax"].AsFloat),
                new SeagullFlockRules(def["_flockSizeMin"].AsInt, def["_flockSizeMax"].AsInt,
                                      def["_flockRadiusMetres"].AsFloat,
                                      def["_flockAltitudeMinMetres"].AsFloat, def["_flockAltitudeMaxMetres"].AsFloat,
                                      def["_flockPeriodMinSeconds"].AsFloat, def["_flockPeriodMaxSeconds"].AsFloat,
                                      def["_flockGlideDuty"].AsFloat,
                                      def["_flockSwoopEveryMinSeconds"].AsFloat, def["_flockSwoopEveryMaxSeconds"].AsFloat,
                                      def["_flockSpacingMetres"].AsFloat, def["_flockAttractRadiusMetres"].AsFloat,
                                      def["_flockFleeRadiusMetres"].AsFloat, def["_flockSettleAfterSeconds"].AsFloat,
                                      def["_flockRegroupSeconds"].AsFloat));
        }
    }
}
