using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The PURE time maths of a routine: <b>which block of the day is running, how long it has been
    /// running, and how long a walk takes in game hours</b>. The direct sibling of
    /// <c>AmbientFleetSchedule</c>, written for the same reason and to the same rules — a function of the
    /// clock, never a saved state machine (CLAUDE.md rule 5), so joining a session at any moment puts
    /// everybody exactly where the clock says they should be.
    ///
    /// <para><b>The hour-of-day domain.</b> Everything here is in game hours off
    /// <c>IGameClock.HourOfDay</c>, so the schedule follows the owner's day length with no unit
    /// conversion — and the one place a real-world quantity enters (walking speed, in metres per second)
    /// is converted through <see cref="TravelHours"/> with the clock's own seconds-per-game-hour passed
    /// in. Nothing here reads <c>Time.time</c>, <c>Time.deltaTime</c> or a wall clock.</para>
    ///
    /// <para><b>Seeding.</b> FNV-1a + the avalanche finalizer over the raw input facts — the same
    /// process-stable constants as the Fishing lane's <c>StableHash</c>, the clam scatter and
    /// <c>AmbientFleetPlan</c>. Re-derived here because World may not reference Boats (rule 4) and the
    /// helper is a dozen lines. No <c>UnityEngine.Random</c>, no <c>string.GetHashCode</c> (which is
    /// process-randomized and would make "the same seed" mean nothing across runs).</para>
    /// </summary>
    public static class RoutineSchedule
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        /// <summary>Game hours in a day. Not a tunable: it is what "hour of day" means.</summary>
        public const float HoursPerDay = 24f;

        // ---- deterministic hashing (process-stable — mirrors AmbientFleetPlan) --------------------

        /// <summary>Fold a string into the running FNV-1a hash (UTF-16 code units, low byte then high).</summary>
        public static uint Fold(uint hash, string s)
        {
            unchecked
            {
                if (s != null)
                {
                    for (int i = 0; i < s.Length; i++)
                    {
                        char c = s[i];
                        hash = (hash ^ (byte)(c & 0xFF)) * FnvPrime;
                        hash = (hash ^ (byte)(c >> 8)) * FnvPrime;
                    }
                }
                return hash;
            }
        }

        /// <summary>Fold an int into the running FNV-1a hash (four bytes, little-endian order).</summary>
        public static uint Fold(uint hash, int value)
        {
            unchecked
            {
                uint u = (uint)value;
                hash = (hash ^ (u & 0xFF)) * FnvPrime;
                hash = (hash ^ ((u >> 8) & 0xFF)) * FnvPrime;
                hash = (hash ^ ((u >> 16) & 0xFF)) * FnvPrime;
                hash = (hash ^ ((u >> 24) & 0xFF)) * FnvPrime;
                return hash;
            }
        }

        /// <summary>The avalanche finalizer, so close inputs diverge well.</summary>
        public static uint Finalize(uint hash)
        {
            unchecked
            {
                hash ^= hash >> 15;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                return hash;
            }
        }

        /// <summary>
        /// A routine's stable identity seed: <c>(worldSeed, routineId)</c>.
        ///
        /// <para><b>The day index is deliberately NOT folded in.</b> A villager's departure jitter is who
        /// they are, not weather: the postmistress opens a few minutes past eight EVERY morning, in the
        /// same way, and a player can learn it — which is the point (<c>npcs-and-routines.md</c> §2.1:
        /// "predictability is a feature"). Folding the day would also put a seam at midnight, where the
        /// block that wraps the day would carry one jitter before the rollover and a different one after,
        /// and a villager would visibly jump at 00:00.</para>
        /// </summary>
        public static uint RoutineSeed(int worldSeed, string routineId)
        {
            uint h = FnvOffsetBasis;
            h = Fold(h, worldSeed);
            h = Fold(h, routineId);
            return h;
        }

        /// <summary>Deterministic uniform value in [0, 1) from a seed and a stream/index pair — the only
        /// "random" primitive in the routine engine. Same inputs ⇒ same value, forever.</summary>
        public static float Hash01(uint seed, int stream, int index)
        {
            uint h = Fold(seed, stream);
            h = Fold(h, index);
            h = Finalize(h);
            return (h & 0x00FFFFFF) / (float)0x01000000;   // 24 mantissa bits → [0, 1)
        }

        /// <summary>Deterministic value in [−1, 1) — <see cref="Hash01"/> centred.</summary>
        public static float HashSigned(uint seed, int stream, int index) =>
            Hash01(seed, stream, index) * 2f - 1f;

        // ---- the jittered day plan ---------------------------------------------------------------

        /// <summary>Stream id for the departure jitter, so a later use of the same seed cannot re-roll it.</summary>
        public const int JitterStream = 11;

        /// <summary>
        /// The hour block <paramref name="entryIndex"/> actually departs at: its authored hour shifted by
        /// this person's seeded jitter, wrapped into [0, 24).
        ///
        /// <para><b>Clamped so jitter can never REORDER the day.</b> A shift big enough to push one
        /// departure past the next would turn a day into a different day, silently. The magnitude is
        /// therefore capped at just under half the smaller of the two gaps this block sits between, which
        /// is the largest shift that provably preserves the order for every block at once.
        /// <c>RoutineContentTests</c> also checks the authored magnitude against the tightest gap, so the
        /// clamp is a guarantee rather than the mechanism.</para>
        /// </summary>
        public static float DepartureHour(float[] authoredStartHours, int entryIndex,
                                          uint seed, float jitterMinutes)
        {
            if (authoredStartHours == null || authoredStartHours.Length == 0) return 0f;
            int n = authoredStartHours.Length;
            int i = ((entryIndex % n) + n) % n;
            float authored = authoredStartHours[i];
            if (n == 1 || jitterMinutes <= 0f) return Wrap24(authored);

            float before = GapHours(authoredStartHours, (i - 1 + n) % n);   // gap ENDING at i
            float after = GapHours(authoredStartHours, i);                  // gap STARTING at i
            float roomHours = Mathf.Max(0f, Mathf.Min(before, after) * 0.5f - MinBlockMarginHours);
            float wantHours = Mathf.Abs(jitterMinutes) / 60f;
            float amplitude = Mathf.Min(wantHours, roomHours);

            return Wrap24(authored + HashSigned(seed, JitterStream, i) * amplitude);
        }

        /// <summary>A hair of daylight kept either side of a jittered departure, so two blocks can never be
        /// shifted onto the same instant even at the clamp's limit. One game second at the pinned 1800 s
        /// day is 0.0133 game hours; this is under a game minute and unnoticeable.</summary>
        public const float MinBlockMarginHours = 0.01f;

        /// <summary>The length of the block starting at <paramref name="entryIndex"/>, in hours — to the
        /// NEXT authored start, wrapping midnight for the last one. Always positive for a sane table; a
        /// duplicated hour yields a full day rather than 0, which is what a one-block day is.</summary>
        public static float GapHours(float[] authoredStartHours, int entryIndex)
        {
            if (authoredStartHours == null || authoredStartHours.Length == 0) return HoursPerDay;
            int n = authoredStartHours.Length;
            if (n == 1) return HoursPerDay;
            int i = ((entryIndex % n) + n) % n;
            int j = (i + 1) % n;
            float gap = authoredStartHours[j] - authoredStartHours[i];
            if (gap <= 0f) gap += HoursPerDay;
            return gap;
        }

        /// <summary>The smallest gap between two consecutive blocks, in hours — what a routine's jitter has
        /// to stay under half of, and what the content test reports when it does not.</summary>
        public static float SmallestGapHours(float[] authoredStartHours)
        {
            if (authoredStartHours == null || authoredStartHours.Length < 2) return HoursPerDay;
            float smallest = HoursPerDay;
            for (int i = 0; i < authoredStartHours.Length; i++)
            {
                float gap = GapHours(authoredStartHours, i);
                if (gap < smallest) smallest = gap;
            }
            return smallest;
        }

        // ---- reading the clock -------------------------------------------------------------------

        /// <summary>An hour value folded into [0, 24). Negative-safe.</summary>
        public static float Wrap24(float hour)
        {
            float h = hour % HoursPerDay;
            return h < 0f ? h + HoursPerDay : h;
        }

        /// <summary>
        /// Which block is running at <paramref name="hourOfDay"/>, given the DEPARTURE hours (already
        /// jittered). The block whose departure is the most recent one at or before now — which for an
        /// hour before the first departure of the day is the LAST block, still running through from
        /// yesterday. That wrap is the whole reason this is a function and not an array index.
        ///
        /// <para>Total: any hour, any table with at least one entry, no allocation. Departure hours need
        /// not be sorted — the most recent one is found by measuring backwards from now, so a table the
        /// jitter has nudged out of exact order still resolves sanely.</para>
        /// </summary>
        public static int BlockIndexAt(float hourOfDay, float[] departureHours)
        {
            if (departureHours == null || departureHours.Length == 0) return -1;
            float h = Wrap24(hourOfDay);

            int best = 0;
            float bestElapsed = float.MaxValue;
            for (int i = 0; i < departureHours.Length; i++)
            {
                float elapsed = Wrap24(h - Wrap24(departureHours[i]));
                if (elapsed < bestElapsed) { bestElapsed = elapsed; best = i; }
            }
            return best;
        }

        /// <summary>How long the block departing at <paramref name="departureHour"/> has been running at
        /// <paramref name="hourOfDay"/>, in game hours — wrapped, so the block that spans midnight
        /// measures straight through it.</summary>
        public static float ElapsedHours(float hourOfDay, float departureHour) =>
            Wrap24(Wrap24(hourOfDay) - Wrap24(departureHour));

        /// <summary>
        /// How many GAME hours a walk of <paramref name="routeLengthMetres"/> takes at
        /// <paramref name="speedMetresPerSecond"/> — the one conversion between the world's metres and the
        /// clock's hours, and the reason <paramref name="secondsPerGameHour"/> is passed in rather than
        /// read: it is <c>GameConfig.SecondsPerHour</c>, the owner's day-length knob, and this file may not
        /// reach for a service.
        ///
        /// <para>Zero for a zero-length route (two blocks at one station: nobody walks), and zero rather
        /// than infinity for a nonsensical speed or day length, so a half-authored def leaves a villager
        /// standing rather than teleporting or NaN-ing.</para>
        /// </summary>
        public static float TravelHours(float routeLengthMetres, float speedMetresPerSecond,
                                        float secondsPerGameHour)
        {
            if (routeLengthMetres <= 0f || speedMetresPerSecond <= 0f || secondsPerGameHour <= 0f)
                return 0f;
            return routeLengthMetres / speedMetresPerSecond / secondsPerGameHour;
        }

        /// <summary>How far along their walk someone is, in metres, after <paramref name="elapsedHours"/>
        /// of a block — the inverse of <see cref="TravelHours"/>, and what the position is sampled at.</summary>
        public static float DistanceWalked(float elapsedHours, float speedMetresPerSecond,
                                           float secondsPerGameHour)
        {
            if (elapsedHours <= 0f || speedMetresPerSecond <= 0f || secondsPerGameHour <= 0f) return 0f;
            return elapsedHours * secondsPerGameHour * speedMetresPerSecond;
        }
    }
}
