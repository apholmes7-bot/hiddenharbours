using System;

namespace HiddenHarbours.Core
{
    /// <summary>One fidget playing at one moment: which, since when, and how far in.</summary>
    public struct IdleFidgetMoment
    {
        /// <summary>The fidget, or null when none plays.</summary>
        public IdleFidgetDef Def;

        /// <summary>The schedule slot it was drawn for.</summary>
        public long Slot;

        /// <summary>When it started, on the figure clock (seconds).</summary>
        public double StartSeconds;

        /// <summary>How far into it this moment is, seconds.</summary>
        public double LocalSeconds;

        /// <summary>True while a fidget plays.</summary>
        public bool Active => Def != null;
    }

    /// <summary>
    /// <b>Which idle fidget plays, and when: a pure function of (world seed, game time, who).</b>
    ///
    /// <para><b>The grid.</b> With <c>J = (MaxGap − MinGap)/2</c>, each figure's time is cut into equal
    /// slots of <c>P = MinGap + J + longest fidget + stretch</c>, the stretch the figure's own in
    /// <c>[0, J/2]</c>. Slot <c>n</c> holds one fidget, chosen by weight, starting
    /// <c>n·P + phase + jitter(n)</c> with the jitter uniform in <c>[0, J/2]</c>. So the gap between one
    /// fidget's end and the next one's start is never under <see cref="IdleFidgetLibraryDef.MinGapSeconds"/>
    /// and never over <see cref="IdleFidgetLibraryDef.MaxGapSeconds"/> plus the spread of the fidgets'
    /// lengths, and only slot <c>floor((t − phase)/P)</c> can be playing at <c>t</c>: O(1), no loop over
    /// the hours. The per-figure stretch is what keeps two neighbours apart: with one shared P two figures
    /// whose phases happened to land close would stay close all day; with different Ps they drift through
    /// each other and part again.</para>
    ///
    /// <para><b>Who.</b> The phase, the jitter and the choice are hashed from <see cref="KeyFor"/>: the
    /// world seed, the skin's id and the figure's own key hash (the one its presenter took of its
    /// <see cref="ICharacterFigureIdentity.FigureKey"/>; 0 for the player). Two people side by side have
    /// different keys, so they fidget at different times and pick different fidgets; the same world
    /// replays the same fidgets. No <c>UnityEngine.Random</c>, no <c>System.Random</c>, no
    /// <c>string.GetHashCode</c>.</para>
    ///
    /// <para><b>Settle.</b> A fidget plays only if the person had been standing idle since at least
    /// <see cref="IdleFidgetLibraryDef.SettleSeconds"/> before it STARTS (<paramref name="idleSinceSeconds"/>
    /// in <see cref="TryActive"/>). Any move or action ends the idle, so it cancels the fidget that frame,
    /// and a person returning to idle mid-slot waits for the next slot instead of popping into the middle
    /// of a head turn.</para>
    ///
    /// <para>Allocation-free: a presenter asks every frame.</para>
    /// </summary>
    public static class IdleFidgetSchedule
    {
        const uint SaltPhase = 0x51ED270Bu;
        const uint SaltJitter = 0x2545F491u;
        const uint SaltPick = 0x9E3779B9u;
        const uint SaltRepick = 0x85EBCA6Bu;
        const uint SaltWorld = 0x7F4A7C15u;
        const uint SaltStretch = 0x632BE5ABu;

        /// <summary>
        /// The schedule key for one figure: the blink's seed of (skin id, figure key hash)
        /// (<see cref="CharacterFigureBlink.SeedFor"/>) folded with the world seed. Pure.
        /// </summary>
        public static uint KeyFor(int worldSeed, string skinId, uint figureKeyHash)
        {
            uint h = CharacterFigureBlink.SeedFor(skinId ?? string.Empty, figureKeyHash);
            unchecked
            {
                ulong z = ((ulong)h << 32) ^ (uint)worldSeed ^ SaltWorld;
                return (uint)(Mix(z) >> 32);
            }
        }

        /// <summary>The slot length P, seconds, for the figure keyed <paramref name="key"/> over the fidgets
        /// <paramref name="skin"/> can draw; 0 when none can (or the library is off).</summary>
        public static double SlotSeconds(IdleFidgetLibraryDef library, CharacterSkinDef skin, uint key)
        {
            if (!Scan(library, skin, out _, out double longest, out _)) return 0d;
            return Slot(library, longest, key);
        }

        /// <summary>
        /// The fidget playing at <paramref name="nowSeconds"/> for the figure keyed <paramref name="key"/>
        /// (<see cref="KeyFor"/>), standing idle since <paramref name="idleSinceSeconds"/>. False, with a
        /// default moment, when the library is off or empty for this skin, when the slot's fidget is not
        /// playing at this time, or when the person had not settled before it started.
        /// </summary>
        public static bool TryActive(IdleFidgetLibraryDef library, CharacterSkinDef skin, uint key,
                                     double nowSeconds, double idleSinceSeconds, out IdleFidgetMoment moment)
        {
            moment = default;
            if (!SlotStart(library, skin, key, nowSeconds, out long slot, out double start, out double totalWeight,
                           out int available))
                return false;

            IdleFidgetDef def = Pick(library, skin, key, slot, totalWeight, available);
            if (def == null) return false;
            if (nowSeconds < start || nowSeconds >= start + def.DurationSeconds) return false;
            if (start < idleSinceSeconds + library.SettleSeconds) return false;

            moment = new IdleFidgetMoment
            {
                Def = def,
                Slot = slot,
                StartSeconds = start,
                LocalSeconds = nowSeconds - start,
            };
            return true;
        }

        /// <summary>
        /// The fidget slot <paramref name="slot"/> holds and when it starts, ignoring idleness: for tests and
        /// plates that need to know what WOULD play. False when the library is off or empty for this skin.
        /// </summary>
        public static bool TrySlot(IdleFidgetLibraryDef library, CharacterSkinDef skin, uint key, long slot,
                                   out IdleFidgetDef def, out double startSeconds)
        {
            def = null;
            startSeconds = 0d;
            if (!Scan(library, skin, out double totalWeight, out double longest, out int available)) return false;
            double p = Slot(library, longest, key);
            if (!(p > 0d)) return false;
            startSeconds = StartOf(library, key, slot, p);
            def = Pick(library, skin, key, slot, totalWeight, available);
            return def != null;
        }

        /// <summary>The slot that holds <paramref name="nowSeconds"/> for this key. False as
        /// <see cref="TrySlot"/>.</summary>
        public static bool TrySlotAt(IdleFidgetLibraryDef library, CharacterSkinDef skin, uint key, double nowSeconds,
                                     out long slot)
        {
            slot = 0;
            if (!Scan(library, skin, out _, out double longest, out _)) return false;
            double p = Slot(library, longest, key);
            if (!(p > 0d)) return false;
            slot = (long)Math.Floor((nowSeconds - Phase(key, p)) / p);
            return true;
        }

        /// <summary>True for the one clip a fidget may play over: the plain standing <c>idle</c>
        /// (<see cref="CharacterSkinStateMap.Idle"/>). Walking, carrying, the helm, the oars, a seat, work and
        /// every performance clip have keys of their own, so each suppresses fidgets by being itself.</summary>
        public static bool IsStandingIdle(string stateKey) =>
            string.Equals(stateKey, CharacterSkinStateMap.Idle, StringComparison.Ordinal);

        // ------------------------------------------------------------------ the grid

        static bool SlotStart(IdleFidgetLibraryDef library, CharacterSkinDef skin, uint key, double now,
                              out long slot, out double start, out double totalWeight, out int available)
        {
            slot = 0;
            start = 0d;
            if (!Scan(library, skin, out totalWeight, out double longest, out available)) return false;
            double p = Slot(library, longest, key);
            if (!(p > 0d)) return false;
            slot = (long)Math.Floor((now - Phase(key, p)) / p);
            start = StartOf(library, key, slot, p);
            return true;
        }

        // P = MinGap + J + longest + stretch(key), stretch in [0, J/2]; the per-slot jitter is in [0, J/2].
        static double Slot(IdleFidgetLibraryDef library, double longest, uint key) =>
            library.MinGapSeconds + Spread(library) + longest + U01(key, 0L, SaltStretch) * Spread(library) * 0.5;

        // J: half the span between the least and the most gap.
        static double Spread(IdleFidgetLibraryDef library) =>
            Math.Max(0d, (double)library.MaxGapSeconds - library.MinGapSeconds) * 0.5;

        static double Phase(uint key, double p) => U01(key, 0L, SaltPhase) * p;

        static double StartOf(IdleFidgetLibraryDef library, uint key, long slot, double p) =>
            slot * p + Phase(key, p) + U01(key, slot, SaltJitter) * Spread(library) * 0.5;

        // The library walked once: the summed weight, the longest fidget and how many this skin can draw.
        static bool Scan(IdleFidgetLibraryDef library, CharacterSkinDef skin, out double totalWeight,
                         out double longest, out int available)
        {
            totalWeight = 0d;
            longest = 0d;
            available = 0;
            if (library == null || !library.Enabled || library.Fidgets == null) return false;
            IdleFidgetDef[] all = library.Fidgets;
            for (int i = 0; i < all.Length; i++)
            {
                IdleFidgetDef d = all[i];
                if (d == null || !d.AvailableFor(skin)) continue;
                totalWeight += d.Weight;
                if (d.DurationSeconds > longest) longest = d.DurationSeconds;
                available++;
            }
            return available > 0 && totalWeight > 0d;
        }

        // ------------------------------------------------------------------ the choice

        /// <summary>How many slots back the no-repeat rule looks. Not a tunable: the depth of a bounded
        /// recursion. Comparing against the previous slot's RAW draw (depth 1) repeats about one slot in six
        /// with three fidgets, because a re-drawn slot is then never seen as re-drawn; four slots back
        /// brings that to about one in twenty with three fidgets (weights 3:2:2) and under one in a hundred
        /// with six. With exactly two drawable fidgets no bounded look-back helps (the re-draw alternates),
        /// and repeats stay common; the shipped library never has fewer than three.</summary>
        const int RepeatLookBack = 4;

        // The slot's fidget: a weighted draw, re-drawn among the others when it repeats the previous slot's
        // own pick (that one found the same way, RepeatLookBack deep), so the same fidget seldom plays twice
        // running. Every draw is pure in (key, slot); no allocation.
        static IdleFidgetDef Pick(IdleFidgetLibraryDef library, CharacterSkinDef skin, uint key, long slot,
                                  double totalWeight, int available) =>
            PickAt(library, skin, key, slot, totalWeight, available, RepeatLookBack);

        static IdleFidgetDef PickAt(IdleFidgetLibraryDef library, CharacterSkinDef skin, uint key, long slot,
                                    double totalWeight, int available, int depth)
        {
            IdleFidgetDef raw = Draw(library, skin, U01(key, slot, SaltPick) * totalWeight, null);
            if (depth <= 0 || available < 2 || raw == null) return raw;
            IdleFidgetDef before = PickAt(library, skin, key, slot - 1, totalWeight, available, depth - 1);
            if (!ReferenceEquals(raw, before)) return raw;
            double rest = totalWeight - raw.Weight;
            if (!(rest > 0d)) return raw;
            return Draw(library, skin, U01(key, slot, SaltRepick) * rest, raw) ?? raw;
        }

        static IdleFidgetDef Draw(IdleFidgetLibraryDef library, CharacterSkinDef skin, double at, IdleFidgetDef skip)
        {
            IdleFidgetDef[] all = library.Fidgets;
            IdleFidgetDef last = null;
            for (int i = 0; i < all.Length; i++)
            {
                IdleFidgetDef d = all[i];
                if (d == null || ReferenceEquals(d, skip) || !d.AvailableFor(skin)) continue;
                last = d;
                if (at < d.Weight) return d;
                at -= d.Weight;
            }
            return last;   // rounding at the top end lands on the last one drawable
        }

        // ------------------------------------------------------------------ the hash

        /// <summary>Uniform on [0, 1) from (key, slot, salt): splitmix64's finaliser. Pure.</summary>
        public static double U01(uint key, long slot, uint salt)
        {
            unchecked
            {
                ulong z = ((ulong)key << 32 | salt) ^ ((ulong)slot * 0x9E3779B97F4A7C15UL);
                return (Mix(z) >> 11) * (1.0 / 9007199254740992.0);
            }
        }

        static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }

    /// <summary>
    /// <b>One figure's idle, for its fidgets</b>: when it last started standing idle, and the fidget
    /// playing now. One per figure, owned by whoever poses it (<c>CharacterFigureLife</c>). The only state
    /// is the idle-since time, which is presentation state, never saved, and never a random draw.
    /// </summary>
    public sealed class IdleFidgetClock
    {
        bool _idle;
        double _idleSince;
        double _last = double.NaN;

        /// <summary>The fidget this figure is playing as of the last <see cref="Step"/>.</summary>
        public IdleFidgetMoment Current { get; private set; }

        /// <summary>When the current idle began, or NaN while not idle.</summary>
        public double IdleSinceSeconds => _idle ? _idleSince : double.NaN;

        /// <summary>
        /// Advance to <paramref name="nowSeconds"/>. <paramref name="eligible"/> false (moving, working,
        /// rowing, sitting, an action or any input this frame) ends the idle and the fidget at once. A clock
        /// that runs backwards (a load that seeks back) restarts the idle from now. Returns
        /// <see cref="Current"/>'s activity.
        /// </summary>
        public bool Step(bool eligible, double nowSeconds, IdleFidgetLibraryDef library, CharacterSkinDef skin, uint key)
        {
            if (!eligible || library == null || !library.Enabled)
            {
                _idle = false;
                _last = nowSeconds;
                Current = default;
                return false;
            }
            if (!_idle || !(nowSeconds >= _last))
            {
                _idle = true;
                _idleSince = nowSeconds;
            }
            _last = nowSeconds;
            if (IdleFidgetPreview.TryGet(out IdleFidgetDef forced, out double forcedLocal))
            {
                Current = new IdleFidgetMoment { Def = forced, StartSeconds = nowSeconds - forcedLocal, LocalSeconds = forcedLocal };
                return true;
            }
            IdleFidgetSchedule.TryActive(library, skin, key, nowSeconds, _idleSince, out IdleFidgetMoment m);
            Current = m;
            return m.Active;
        }

        /// <summary>Forget the idle (a figure re-configured or re-shown).</summary>
        public void Reset()
        {
            _idle = false;
            _last = double.NaN;
            Current = default;
        }
    }

    /// <summary>
    /// <b>Plates only:</b> hold every eligible figure on one fidget at one moment, so a picture can be
    /// taken of each key frame while the world is frozen. Never set by the game. Cleared by
    /// <see cref="Clear"/> and on every play-mode start.
    /// </summary>
    public static class IdleFidgetPreview
    {
        static IdleFidgetDef s_def;
        static double s_local;

        /// <summary>Hold eligible figures on <paramref name="def"/> at <paramref name="localSeconds"/> in.</summary>
        public static void Hold(IdleFidgetDef def, double localSeconds)
        {
            s_def = def;
            s_local = localSeconds;
        }

        /// <summary>Let the schedule decide again.</summary>
        public static void Clear() => s_def = null;

        /// <summary>The held fidget and moment, if any.</summary>
        public static bool TryGet(out IdleFidgetDef def, out double localSeconds)
        {
            def = s_def;
            localSeconds = s_local;
            return def != null;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_def = null;
    }

    /// <summary>
    /// <b>The player touched a control</b>: the on-foot and deck intent sources note any move or press
    /// here, and the player's figure treats a note this frame or the one before as "not idle", so input
    /// cancels a fidget the frame it arrives, before the move has even changed his gait. Presentation
    /// only: a frame number, never saved, read by no simulation system.
    /// </summary>
    public static class IdleFidgetInput
    {
        static int s_frame = int.MinValue;

        /// <summary>Note a control actuated this frame.</summary>
        public static void Note() => s_frame = UnityEngine.Time.frameCount;

        /// <summary>True when a control was actuated this frame or the frame before.</summary>
        public static bool ActedRecently
        {
            get
            {
                if (s_frame == int.MinValue) return false;
                int now = UnityEngine.Time.frameCount;
                return now >= s_frame && now - s_frame <= 1;
            }
        }

        /// <summary>Forget every note (tests).</summary>
        public static void Reset() => s_frame = int.MinValue;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_frame = int.MinValue;
    }
}
