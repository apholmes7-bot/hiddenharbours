using System;

namespace HiddenHarbours.Core
{
    /// <summary>The colour a mark shows. IALA Region B as the Canadian Coast Guard flies it.</summary>
    public enum NavLightColour
    {
        /// <summary>White — cardinals, isolated danger, safe water. The default when a character
        /// names no colour at all, which is why it is zero.</summary>
        White = 0,
        /// <summary>Green — port hand, Region B.</summary>
        Green = 1,
        /// <summary>Red — starboard hand, Region B.</summary>
        Red = 2,
        /// <summary>Yellow — special marks. Nothing in the kit shows one yet; parsed so a future
        /// def cannot fail silently.</summary>
        Yellow = 3,
    }

    /// <summary>
    /// One rhythm token out of a light character. The <b>duration</b> of a flash and the
    /// <b>length of its cycle</b> are what separate them, and both live in
    /// <see cref="NavLightCharacter"/> as named constants.
    /// </summary>
    public enum NavLightRhythm
    {
        /// <summary><c>F</c> — a fixed light, burning steadily. On for its whole period.</summary>
        Fixed = 0,
        /// <summary><c>Fl</c> — an ordinary flash: light shorter than dark.</summary>
        Flash = 1,
        /// <summary><c>LFl</c> — a long flash, two seconds or more. The south cardinal's tail.</summary>
        LongFlash = 2,
        /// <summary><c>Q</c> — quick, 60 a minute.</summary>
        Quick = 3,
        /// <summary><c>VQ</c> — very quick, 120 a minute. Unused by the kit; parsed anyway.</summary>
        VeryQuick = 4,
    }

    /// <summary>
    /// <b>What a lit mark actually does with its lamp</b> — parsed once out of the chart
    /// abbreviation a <c>NavBuoyDef</c> carries, and thereafter answered as a pure function of the
    /// master clock: <see cref="IsOn(double,double)"/>.
    ///
    /// <para><b>Pure, and that is the whole point (rule 5).</b> There is no accumulator, no
    /// <c>Time.time</c>, no saved state and no RNG anywhere in here. A mark's light at an instant is
    /// <c>f(totalSeconds, phase)</c> and nothing else, so two marks 30 m apart cannot drift out of
    /// step, a reload cannot land mid-flash, and a test can ask what the south cardinal is doing at
    /// t = 1 000 000 s without running a frame. It is Core because the DATA is a Boats type and the
    /// LIGHT is drawn by Art, and those two may not reference each other (rule 4).</para>
    ///
    /// <para><b>⚠️ IT PARSES <c>LightText</c>, NOT <c>LightCharacter</c> — and it has to.</b> The
    /// charter for this slice said to parse the def's <c>LightCharacter</c> id. That id cannot be
    /// parsed: <c>Q3</c> is the east cardinal and its period is <b>10 s</b>, <c>Q9</c> is the west
    /// and its period is <b>15 s</b>, and neither number appears in the id at all. The id is also
    /// ambiguous where it does carry two numbers — in <c>Fl2W5</c> the 2 is a group count and the 5
    /// a period, and only the colour letter between them says which is which. Recovering the missing
    /// periods would mean hard-coding "an east cardinal flashes on ten seconds" into C#, which is
    /// exactly the content-as-code the project forbids (rule 2). <c>LightText</c> — <c>Q(3) 10s</c>,
    /// <c>Fl(2) W 5s</c>, <c>Q(6) + LFl 15s</c> — is the international chart abbreviation, it is
    /// already authored on all ten defs, and it is complete. So the id stays an id and the text is
    /// the source of truth. A test pins both halves of every shipped mark against each other so the
    /// two cannot drift.</para>
    ///
    /// <para><b>The grammar accepted.</b> One or more segments joined by <c>+</c>, then an optional
    /// period:
    /// <code>
    ///   character := segment ( '+' segment )* period?
    ///   segment   := rhythm group? colour?
    ///   rhythm    := 'LFl' | 'VQ' | 'Fl' | 'Q' | 'F'      (longest token first — 'LFl' is not 'F')
    ///   group     := '(' N ')'
    ///   colour    := 'W' | 'G' | 'R' | 'Y'
    ///   period    := N 's'
    /// </code>
    /// Whitespace is insignificant. An unknown token FAILS the parse rather than yielding a dark
    /// mark — a light that silently never lights is indistinguishable from a broken one, and this is
    /// channel furniture.</para>
    ///
    /// <para><b>The schedule model: every rhythm is a flash inside a cycle.</b> A group of N is N
    /// cycles laid end to end from the top of the period; whatever is left of the period after the
    /// last cycle is dark. That one rule reproduces all six of the kit's characters, the two
    /// cardinals whose whole identity is their count included, and it is why a composite like
    /// <c>Q(6) + LFl</c> needs no special case: six quick cycles, then one long-flash cycle.</para>
    ///
    /// <para><b>Never saved (rule 5).</b> Where a mark is in its period at this instant is recomputed
    /// from the clock, exactly as tide and weather are.</para>
    /// </summary>
    public readonly struct NavLightCharacter
    {
        // ---- the rhythms, in seconds, named once (rule 6) -------------------------------------
        // IALA states quick as 50-79 flashes a minute and very quick as 80-159; the kit's marks are
        // read at 60 and 120, the round numbers at the centre of each band. A flash is "of duration
        // short in relation to the darkness"; a LONG flash is two seconds or more BY DEFINITION,
        // which is what makes the south cardinal's tail tell itself apart from the six quicks in
        // front of it. These are the numbers the picture is made of, so they live here and nowhere
        // else.

        /// <summary>Quick: 0.5 s of light in a 1.0 s cycle — 60 a minute.</summary>
        public const float QuickFlashSeconds = 0.5f;
        /// <summary>The cycle a quick flash repeats on.</summary>
        public const float QuickCycleSeconds = 1.0f;

        /// <summary>Very quick: 0.25 s of light in a 0.5 s cycle — 120 a minute.</summary>
        public const float VeryQuickFlashSeconds = 0.25f;
        /// <summary>The cycle a very quick flash repeats on.</summary>
        public const float VeryQuickCycleSeconds = 0.5f;

        /// <summary>An ordinary flash: 1.0 s of light, and at least as long dark after it.</summary>
        public const float FlashSeconds = 1.0f;
        /// <summary>The cycle an ordinary flash repeats on inside a group.</summary>
        public const float FlashCycleSeconds = 2.0f;

        /// <summary>A long flash: 2.0 s — the IALA floor, and the south cardinal's signature.</summary>
        public const float LongFlashSeconds = 2.0f;
        /// <summary>The cycle a long flash repeats on inside a group.</summary>
        public const float LongFlashCycleSeconds = 3.0f;

        // ---- the parsed schedule --------------------------------------------------------------
        // Two parallel arrays rather than a struct array: IsOn is called once per lit mark per
        // frame and walks them linearly, and this way it touches two tight blocks and allocates
        // nothing (rule 7). Allocated ONCE, at parse.
        private readonly float[] _onsets;
        private readonly float[] _durations;

        /// <summary>The full period in seconds — the time from the top of one group to the next.</summary>
        public readonly float PeriodSeconds;

        /// <summary>The colour shown. White when the character named none.</summary>
        public readonly NavLightColour Colour;

        /// <summary>Did the character actually SAY a colour? <c>Fl G 4s</c> did, <c>Q(3) 10s</c> did
        /// not (a cardinal is white by being a cardinal). Kept so the naming guard can hold the id
        /// and the text together without a second parser.</summary>
        public readonly bool ColourStated;

        /// <summary>How many flashes in the group — 3 for an east cardinal, 1 for a plain flash. For
        /// a composite it is the count of the FIRST segment, which is the one a skipper counts.</summary>
        public readonly int GroupCount;

        /// <summary>The rhythm of the first segment: what kind of light this is at a glance.</summary>
        public readonly NavLightRhythm Rhythm;

        private NavLightCharacter(float[] onsets, float[] durations, float periodSeconds,
                                  NavLightColour colour, bool colourStated, int groupCount,
                                  NavLightRhythm rhythm)
        {
            _onsets = onsets;
            _durations = durations;
            PeriodSeconds = periodSeconds;
            Colour = colour;
            ColourStated = colourStated;
            GroupCount = groupCount;
            Rhythm = rhythm;
        }

        /// <summary>
        /// Is this a real, lit character? A default <see cref="NavLightCharacter"/> — what an unlit
        /// mark like the mooring buoy yields — is not, and <see cref="IsOn"/> answers false forever.
        /// </summary>
        public bool IsLit => _onsets != null && _onsets.Length > 0 && PeriodSeconds > 0f;

        /// <summary>How many separate flashes there are in one period, composites included.</summary>
        public int FlashCount => _onsets?.Length ?? 0;

        /// <summary>
        /// Total seconds of light in one period. The fraction of the period a mark is actually
        /// burning is <c>OnSeconds / PeriodSeconds</c> — a hair over an eighth for a port hand, and
        /// the number that says a flashing light costs almost nothing to look at.
        /// </summary>
        public float OnSeconds
        {
            get
            {
                if (_durations == null) return 0f;
                float sum = 0f;
                for (int i = 0; i < _durations.Length; i++) sum += _durations[i];
                return sum;
            }
        }

        /// <summary>
        /// <b>Is the lamp burning at this instant?</b> The whole runtime surface of this type.
        ///
        /// <para>Pure in <c>(totalSeconds, phaseSeconds)</c>: same pair in, same answer out, always
        /// and everywhere. <paramref name="totalSeconds"/> is <see cref="IGameClock.TotalSeconds"/>,
        /// the master value; <paramref name="phaseSeconds"/> is this mark's own offset into the
        /// period (see <see cref="PhaseFromSeed"/>), which is what stops two port-hand cans a
        /// hundred metres apart winking in unison like a Christmas tree.</para>
        ///
        /// <para><b>Why <c>double</c> all the way down.</b> A game running a season reaches tens of
        /// millions of in-game seconds; a <c>float</c> there has a resolution of a couple of seconds
        /// and a 0.5 s flash simply stops existing. The fold is done in double and only the compare
        /// meets the float schedule.</para>
        /// </summary>
        public bool IsOn(double totalSeconds, double phaseSeconds)
        {
            if (_onsets == null || _onsets.Length == 0) return false;
            double period = PeriodSeconds;
            if (period <= 0d) return false;

            // Math.Floor rather than the % operator: % keeps the sign of the dividend, so a negative
            // time (a fixture winding back before the start of the game) would land outside every
            // window and read as dark. Floor folds negatives correctly.
            double t = totalSeconds + phaseSeconds;
            t -= Math.Floor(t / period) * period;

            for (int i = 0; i < _onsets.Length; i++)
            {
                double on = _onsets[i];
                if (t >= on && t < on + _durations[i]) return true;
            }
            return false;
        }

        /// <summary>
        /// This mark's own offset into the period, from a seed that is a property of the mark
        /// itself (its chart id) rather than of the order it happened to be spawned in.
        ///
        /// <para><b>⭐ The seed must not be a sibling index, and this is not a style preference.</b>
        /// The lamp lane has already been bitten once by exactly that: a scene light seeded its
        /// flicker off <c>transform.GetSiblingIndex()</c>, so adding one new child RE-SEEDED every
        /// light beside it and a fixture that had been green for a fortnight went red for a reason
        /// nobody could see. A mark's phase is derived here from a hash of its planned id, which is
        /// a fact about the chart — place the marks in any order, place one more, place them in
        /// another region, and every existing mark keeps the phase it had.</para>
        ///
        /// <para>The spread is over a whole period, quantised finely enough (1/4096) that no two ids
        /// of different text collide in practice while staying exactly reproducible.</para>
        /// </summary>
        public float PhaseFromSeed(int seed)
        {
            if (PeriodSeconds <= 0f) return 0f;
            // Mask to positive before the modulo: int.MinValue % n is negative, and a negative phase
            // is harmless to IsOn (it folds) but makes the value hard to read in a log or a test.
            int bucket = (seed & 0x7FFFFFFF) % PhaseBuckets;
            return PeriodSeconds * (bucket / (float)PhaseBuckets);
        }

        /// <summary>How finely <see cref="PhaseFromSeed"/> quantises the period.</summary>
        public const int PhaseBuckets = 4096;

        /// <summary>
        /// A stable hash of a mark's id, for <see cref="PhaseFromSeed"/>.
        ///
        /// <para><b>⚠️ Deliberately NOT <c>string.GetHashCode()</c>.</b> That is documented as
        /// unstable across runtimes and versions and is randomised per process on some of them — so a
        /// test that pins a phase would pass here and fail on another machine, and the marks would
        /// re-phase themselves on an engine upgrade. This is FNV-1a: eight lines, no allocation, and
        /// the same answer on every platform forever.</para>
        /// </summary>
        public static int SeedFromId(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            unchecked
            {
                const uint offset = 2166136261u;
                const uint prime = 16777619u;
                uint h = offset;
                for (int i = 0; i < id.Length; i++)
                {
                    h ^= id[i];
                    h *= prime;
                }
                return (int)h;
            }
        }

        // ---- parsing --------------------------------------------------------------------------

        /// <summary>
        /// Parse a chart abbreviation. Returns false — with <paramref name="error"/> saying why —
        /// for anything it does not fully understand, so an unparseable def is loud at validation
        /// rather than quietly dark on the water at two in the morning.
        ///
        /// <para>An empty or whitespace string is an UNLIT mark, which is not an error: it returns
        /// false with an empty <paramref name="error"/>, and the caller checks
        /// <c>string.IsNullOrWhiteSpace</c> if it wants to tell the two apart.</para>
        /// </summary>
        public static bool TryParse(string text, out NavLightCharacter character, out string error)
        {
            character = default;
            error = "";
            if (string.IsNullOrWhiteSpace(text)) return false;   // unlit, not malformed

            var segments = new Segment[MaxSegments];
            int segmentCount = 0;
            float declaredPeriod = 0f;

            int i = 0;
            int n = text.Length;
            while (i < n)
            {
                if (char.IsWhiteSpace(text[i]) || text[i] == '+') { i++; continue; }

                // A period is the only token that starts with a digit at segment level: "15s".
                if (char.IsDigit(text[i]))
                {
                    int start = i;
                    while (i < n && (char.IsDigit(text[i]) || text[i] == '.')) i++;
                    string number = text.Substring(start, i - start);
                    if (i < n && (text[i] == 's' || text[i] == 'S')) i++;
                    if (!float.TryParse(number, System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        out float seconds) || seconds <= 0f)
                    {
                        error = $"'{number}' is not a period in seconds";
                        return false;
                    }
                    if (declaredPeriod > 0f)
                    {
                        error = $"two periods declared ({declaredPeriod}s and {seconds}s)";
                        return false;
                    }
                    declaredPeriod = seconds;
                    continue;
                }

                if (!TryReadSegment(text, ref i, out Segment segment, out error)) return false;
                if (segmentCount >= MaxSegments)
                {
                    error = $"more than {MaxSegments} segments";
                    return false;
                }
                segments[segmentCount++] = segment;
            }

            if (segmentCount == 0) { error = "no rhythm found"; return false; }

            // Lay the cycles end to end from the top of the period.
            int flashes = 0;
            for (int s = 0; s < segmentCount; s++) flashes += segments[s].Count;

            var onsets = new float[flashes];
            var durations = new float[flashes];
            float cursor = 0f;
            int f = 0;
            for (int s = 0; s < segmentCount; s++)
            {
                CycleOf(segments[s].Rhythm, out float flash, out float cycle);
                for (int c = 0; c < segments[s].Count; c++)
                {
                    onsets[f] = cursor;
                    durations[f] = flash;
                    cursor += cycle;
                    f++;
                }
            }

            // A fixed light is the one rhythm with no dark part, so it has no cycle to measure and
            // 'cursor' is still zero here. It fills whatever period it is given, and a nominal one
            // if it was given none — for a light that never goes out the period is bookkeeping.
            bool fixedLight = segments[0].Rhythm == NavLightRhythm.Fixed && segmentCount == 1;

            float period = declaredPeriod > 0f
                ? declaredPeriod
                : (fixedLight ? FixedNominalPeriodSeconds : cursor);
            if (period <= 0f) { error = "the character has no period"; return false; }

            if (fixedLight)
            {
                onsets = new float[] { 0f };
                durations = new float[] { period };
            }
            else if (cursor > period + PeriodSlackSeconds)
            {
                // The flashes do not fit in the period they claim. That is a data error and a
                // visible one — the mark would be lit more than it is dark and stop reading as its
                // own character — so it is refused rather than clamped.
                error = $"the flashes need {cursor:0.##}s but the period is {period:0.##}s";
                return false;
            }

            // The colour is a property of the whole light, not of one flash: a mark that named one
            // anywhere shows it throughout.
            NavLightColour colour = NavLightColour.White;
            bool stated = false;
            for (int s = 0; s < segmentCount; s++)
            {
                if (!segments[s].ColourStated) continue;
                colour = segments[s].Colour;
                stated = true;
                break;
            }

            character = new NavLightCharacter(onsets, durations, period, colour, stated,
                                              segments[0].Count, segments[0].Rhythm);
            return true;
        }

        /// <summary>Parse, or a default (unlit) character. For callers that have already validated.</summary>
        public static NavLightCharacter Parse(string text) =>
            TryParse(text, out NavLightCharacter c, out _) ? c : default;

        /// <summary>
        /// How much longer than its declared period a group may run before the parse refuses it.
        /// A hair, for float text like "4.5s" — not enough to hide a real mistake.
        /// </summary>
        private const float PeriodSlackSeconds = 0.001f;

        /// <summary>The most segments a composite may have. The kit's longest is two.</summary>
        private const int MaxSegments = 4;

        /// <summary>The period a fixed light gets when it declares none. Arbitrary and harmless —
        /// the lamp is on for all of it either way.</summary>
        private const float FixedNominalPeriodSeconds = 1f;

        private struct Segment
        {
            public NavLightRhythm Rhythm;
            public int Count;
            public NavLightColour Colour;
            public bool ColourStated;
        }

        private static void CycleOf(NavLightRhythm rhythm, out float flash, out float cycle)
        {
            switch (rhythm)
            {
                case NavLightRhythm.Quick:      flash = QuickFlashSeconds;     cycle = QuickCycleSeconds;     return;
                case NavLightRhythm.VeryQuick:  flash = VeryQuickFlashSeconds; cycle = VeryQuickCycleSeconds; return;
                case NavLightRhythm.LongFlash:  flash = LongFlashSeconds;      cycle = LongFlashCycleSeconds; return;
                case NavLightRhythm.Fixed:      flash = 0f;                    cycle = 0f;                    return;
                default:                        flash = FlashSeconds;          cycle = FlashCycleSeconds;     return;
            }
        }

        private static bool TryReadSegment(string text, ref int i, out Segment segment, out string error)
        {
            segment = new Segment { Count = 1, Colour = NavLightColour.White };
            error = "";

            // ⚠️ LONGEST TOKEN FIRST. "LFl" starts with an L and "Fl" is inside it; "VQ" contains a
            // Q. Reading them shortest-first turns a long flash into a fixed light plus a flash and
            // the south cardinal loses the one feature that identifies her.
            if (Match(text, ref i, "LFl")) segment.Rhythm = NavLightRhythm.LongFlash;
            else if (Match(text, ref i, "VQ")) segment.Rhythm = NavLightRhythm.VeryQuick;
            else if (Match(text, ref i, "Fl")) segment.Rhythm = NavLightRhythm.Flash;
            else if (Match(text, ref i, "Q")) segment.Rhythm = NavLightRhythm.Quick;
            else if (Match(text, ref i, "F")) segment.Rhythm = NavLightRhythm.Fixed;
            else
            {
                error = $"unknown rhythm at '{text.Substring(i)}' " +
                        "(expected F, Fl, LFl, Q or VQ)";
                return false;
            }

            SkipSpace(text, ref i);

            // The group count, bracketed "(3)" or bare "3".
            if (i < text.Length && text[i] == '(')
            {
                i++;
                if (!TryReadInt(text, ref i, out int count)) { error = "empty group count"; return false; }
                if (i >= text.Length || text[i] != ')') { error = "unclosed group count"; return false; }
                i++;
                if (count < 1) { error = $"a group of {count}"; return false; }
                segment.Count = count;
                SkipSpace(text, ref i);
            }

            // The colour.
            if (i < text.Length)
            {
                switch (text[i])
                {
                    case 'W': segment.Colour = NavLightColour.White;  segment.ColourStated = true; i++; break;
                    case 'G': segment.Colour = NavLightColour.Green;  segment.ColourStated = true; i++; break;
                    case 'R': segment.Colour = NavLightColour.Red;    segment.ColourStated = true; i++; break;
                    case 'Y': segment.Colour = NavLightColour.Yellow; segment.ColourStated = true; i++; break;
                }
            }

            SkipSpace(text, ref i);
            return true;
        }

        private static bool Match(string text, ref int i, string token)
        {
            if (i + token.Length > text.Length) return false;
            for (int k = 0; k < token.Length; k++)
                if (text[i + k] != token[k]) return false;
            i += token.Length;
            return true;
        }

        private static void SkipSpace(string text, ref int i)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        }

        private static bool TryReadInt(string text, ref int i, out int value)
        {
            int start = i;
            while (i < text.Length && char.IsDigit(text[i])) i++;
            if (i == start) { value = 0; return false; }
            return int.TryParse(text.Substring(start, i - start),
                                System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        /// <summary>The onset (seconds into the period) of flash <paramref name="index"/>. Tests.</summary>
        public float OnsetOf(int index) =>
            _onsets != null && index >= 0 && index < _onsets.Length ? _onsets[index] : -1f;

        /// <summary>The duration of flash <paramref name="index"/>. Tests.</summary>
        public float DurationOf(int index) =>
            _durations != null && index >= 0 && index < _durations.Length ? _durations[index] : -1f;
    }
}
