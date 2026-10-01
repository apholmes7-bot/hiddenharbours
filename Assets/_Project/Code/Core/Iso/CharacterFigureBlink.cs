namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The rig's blink (<c>BLINK</c>, rig 9 README §4), played on one figure's own clock.</b>
    ///
    /// <para>The steps (half 40 ms, shut 80 ms, half 40 ms), the wait between blinks (uniform in
    /// 2.4–6.0 s), the first blink (a random point in a first interval, so a crowd never blinks
    /// together), the double (15 % of blinks, a second one 120 ms after the first ends) and the skip
    /// (a frame whose own eyes are shut keeps them) are the def's, baked from the rig; nothing here
    /// is a number of its own.</para>
    ///
    /// <para><b>Seeded, never global.</b> The draws come from a PRNG seeded by
    /// <see cref="SeedFor"/> — the def id and the ONE hash of the figure's key — so the same figure blinks
    /// the same way on every run, and two skippers of one def still blink apart. It never touches
    /// <c>UnityEngine.Random</c>, and no simulation system reads it: it is presentation only
    /// (rule 5).</para>
    ///
    /// <para><b>The clock.</b> The caller passes the time the clips play on
    /// (<see cref="IGameClock.TotalSeconds"/>). A reversal (a load that seeks back) or a jump longer
    /// than any wait the def allows (a load, a long fast-forward) restarts the schedule from the seed
    /// at the new time, so a jump never costs a loop over the hours it skipped, and a replay from the
    /// same time blinks the same.</para>
    ///
    /// <para>No allocation after <see cref="Reset"/>: a presenter asks every frame.</para>
    /// </summary>
    public sealed class CharacterFigureBlink
    {
        const uint FnvOffset = 2166136261u;
        const uint FnvPrime = 16777619u;

        CharacterSkinDef _def;
        uint _state;
        double _start;
        double _last;
        double _length;
        bool _second;
        bool _live;

        /// <summary>The seed this schedule restarts from.</summary>
        public uint Seed { get; private set; }

        /// <summary>
        /// The seed for one figure: FNV-1a over the def id, a separator and the four bytes of
        /// <paramref name="keyHash"/>, low byte first. <paramref name="keyHash"/> is the ONE hash the
        /// figure's presenter took of its key (<see cref="ICharacterFigureIdentity.FigureKey"/>; ADR 0044
        /// §9), the one that also moves a villager's idle phase, and 0 for a figure with none. Hashed
        /// character by character and byte by byte, so no string is built.
        /// </summary>
        public static uint SeedFor(string defId, uint keyHash)
        {
            uint h = Mix(FnvOffset, defId);
            unchecked
            {
                h = (h ^ '|') * FnvPrime;
                for (int shift = 0; shift < 32; shift += 8) h = (h ^ ((keyHash >> shift) & 0xFFu)) * FnvPrime;
            }
            return h;
        }

        static uint Mix(uint h, string s)
        {
            if (s == null) return h;
            unchecked
            {
                for (int i = 0; i < s.Length; i++) h = (h ^ s[i]) * FnvPrime;
            }
            return h;
        }

        /// <summary>Start (or restart) this schedule for a def and a seed. The first blink is scheduled
        /// at the first <see cref="EyesAt"/>.</summary>
        public void Reset(CharacterSkinDef def, uint seed)
        {
            _def = def;
            Seed = seed;
            _state = seed;
            _live = false;
            _length = 0d;
            if (def != null && def.BlinkSteps != null)
                foreach (CharacterSkinDef.BlinkStep step in def.BlinkSteps) _length += step.Seconds;
        }

        /// <summary>The blink's whole length, seconds: the sum of its steps.</summary>
        public double Length => _length;

        /// <summary>
        /// The eyes group the blink shows at <paramref name="now"/>, or
        /// <see cref="CharacterSkinDef.NoFaceGroup"/> between blinks and for a def with no blink. The
        /// caller applies the skip (a frame whose own eyes are shut): the schedule runs on regardless,
        /// so a skipped blink is simply not shown.
        /// </summary>
        public int EyesAt(double now)
        {
            if (_def == null || !_def.HasBlink || !(_length > 0d)) return CharacterSkinDef.NoFaceGroup;
            if (!_live || now < _last || now - _last > _def.BlinkIntervalSeconds.y + _length) Restart(now);
            _last = now;
            while (now >= _start + _length) Advance();
            if (now < _start) return CharacterSkinDef.NoFaceGroup;

            double t = now - _start;
            CharacterSkinDef.BlinkStep[] steps = _def.BlinkSteps;
            for (int i = 0; i < steps.Length; i++)
            {
                if (t < steps[i].Seconds) return steps[i].Group;
                t -= steps[i].Seconds;
            }
            return CharacterSkinDef.NoFaceGroup;
        }

        /// <summary>When the scheduled (or showing) blink starts — for a test.</summary>
        public double NextStart => _start;

        void Restart(double now)
        {
            _state = Seed;
            _live = true;
            _second = false;
            double first = Interval();
            _start = now + Next01() * first;
        }

        void Advance()
        {
            double end = _start + _length;
            if (!_second && Next01() < _def.BlinkDoubleChance)
            {
                _start = end + _def.BlinkDoubleGapSeconds;
                _second = true;
            }
            else
            {
                _start = end + Interval();
                _second = false;
            }
        }

        double Interval()
        {
            double lo = _def.BlinkIntervalSeconds.x, hi = _def.BlinkIntervalSeconds.y;
            return lo + (hi - lo) * Next01();
        }

        // splitmix32: one add and two multiplies per draw, uniform on [0, 1).
        double Next01()
        {
            unchecked
            {
                _state += 0x9E3779B9u;
                uint z = _state;
                z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                z ^= z >> 16;
                return z / 4294967296.0;
            }
        }
    }
}
