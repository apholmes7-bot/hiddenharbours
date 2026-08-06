namespace HiddenHarbours.Core
{
    /// <summary>
    /// What the MODE pusher steps through (radarRig.js:430 — the rig labels <c>keys[0]</c> MODE and
    /// draws a ring glyph on it). Three states on one key, because the rig authors exactly three side
    /// pushers and the other two are RANGE up and RANGE down.
    ///
    /// <para><b>Standby is a real state, not an off switch.</b> A marine radar warms up in STANDBY with
    /// the aerial stopped and the scope dark but alive, and that is what the rig's own status strip
    /// prints (<c>o.tx ? 'TX' : 'STBY'</c>, radarRig.js:218). The sweep does not turn, the phosphor
    /// does not refresh, and the echoes sit at the flat 0.5 brightness <c>sweepBright</c> gives an
    /// untransmitting set — the picture you are looking at is the last one it painted, which is the
    /// truth about a set that is not radiating.</para>
    ///
    /// <para><b>Why orientation rides the same key.</b> Head-up and north-up are the two ways to read a
    /// PPI and a skipper picks one and leaves it; folding them into the mode ring costs one extra press
    /// to reach north-up and saves inventing a fourth pusher the rig never drew. The status strip
    /// already prints which one is live (<c>N-UP</c>/<c>H-UP</c>), so the state is never a guess.</para>
    /// </summary>
    public enum RadarMode
    {
        /// <summary>Warmed up, aerial stopped, not radiating.</summary>
        Standby = 0,
        /// <summary>Transmitting, picture turned so the bow is at the top.</summary>
        HeadUp = 1,
        /// <summary>Transmitting, picture turned so north is at the top (the chart convention).</summary>
        NorthUp = 2,
    }

    /// <summary>
    /// One hull's <b>radar preferences</b> — how that boat's set is left: standing by or transmitting,
    /// head-up or north-up, which rung of the range ladder, and the amber night backlight. The skipper
    /// who leaves his radar transmitting at half a mile expects to find it that way tomorrow, so these
    /// persist per hull beside the owned instrument ids (<see cref="InstrumentLocker"/>).
    ///
    /// <para><b>Its own record, for the reason <see cref="ChartplotterPrefs"/> is its own.</b>
    /// <see cref="SounderPrefs"/> earns "one record for two instruments" from a structural fact — the
    /// sounder and the finder contend for the SAME brow cutout and can never be fitted together. The
    /// radar sits in the brow's CENTRE station and is fitted alongside both a sounder and a plotter, so
    /// sharing a record would mean one tap on the scope silently re-lighting another instrument.</para>
    ///
    /// <para><b>Preferences, never sim inputs.</b> Nothing here reaches the simulation. The mode decides
    /// whether the aerial is turning on the glass, not whether the fleet is out there; the range decides
    /// how much sea is on the scope, not how much sea exists. Everything the radar SHOWS is recomputed
    /// from the world (rule 5) — this struct is only how the glass is set up to show it.</para>
    ///
    /// <para><b>What is deliberately NOT here.</b> GAIN, SEA and RAIN are drawn by the rig and are not
    /// preferences: the rig authors three pushers and none of them is a clutter knob, so a stored dial
    /// nothing can turn would be a knob that never moves (rule 6). Gain is factory tuning in
    /// <see cref="RadarSettings"/>; sea clutter is read live from the sea state, which is the honest
    /// source and makes the instrument answer to the weather (P1). See <c>RadarOverlayHost</c>.</para>
    ///
    /// <para>⚠ <b>Schema:</b> persisted as <c>RadarPrefsDto</c> in an ADDITIVE v10 member
    /// (<c>SaveData.HullRadarPrefs</c>) — the same shape and the same reasoning as the plotter's row,
    /// so there is no migration and no version bump (ADR 0030). A save written before this member
    /// existed simply has no rows, which reads as "never touched" and yields
    /// <see cref="FromDefaults"/>.</para>
    /// </summary>
    [System.Serializable]
    public struct RadarPrefs
    {
        /// <summary>Amber night backlight rather than the green day phosphor (the rig's <c>night</c>
        /// signal, radarRig.js:37-48). This is the EARLY-ON override only: the glass draws night when
        /// this OR the hull's panel light says so, which is the standing rule every instrument follows.</summary>
        public bool Night;

        /// <summary>Standing by, or transmitting in one of the two orientations.</summary>
        public RadarMode Mode;

        /// <summary>Which rung of <see cref="RadarSettings"/>'s range ladder the scope is on
        /// (0 = closest). Stored as the RUNG, not the resulting nautical miles, so that re-tuning
        /// <see cref="RadarSettings.MinRangeNM"/> moves every saved set with it instead of stranding one
        /// on a range the ladder no longer has — the <see cref="ChartplotterPrefs.RangeStep"/> rule.</summary>
        public int RangeStep;

        public RadarPrefs(bool night, RadarMode mode, int rangeStep)
        {
            Night = night;
            Mode = mode;
            RangeStep = rangeStep;
        }

        /// <summary>The out-of-the-box settings for a freshly fitted set, from the owner's tuning
        /// (rule 6 — the numbers are data, not literals here).
        ///
        /// <para><b>It wakes TRANSMITTING, head-up.</b> A set that came up in standby would look
        /// identical to a broken one on the first fit, and the first thing anybody does with a new radar
        /// is switch it on. Head-up because that is how a small boat's radar is read — what is on the
        /// scope's top is what is over the bow.</para></summary>
        public static RadarPrefs FromDefaults(in RadarSettings cfg)
            => new RadarPrefs(false, RadarMode.HeadUp, cfg.SafeDefaultRangeStep);

        /// <summary>The range in NM this hull's scope is showing, healed against both a garbage stored
        /// rung and an unwritten config block — the ONE place the pref meets the ladder.</summary>
        public readonly float RangeNM(in RadarSettings cfg) => cfg.RangeNMAt(RangeStep);

        /// <summary>True while the set is radiating — the rig's <c>tx</c> signal.</summary>
        public readonly bool Transmitting => Mode != RadarMode.Standby;

        /// <summary>True while the picture is turned to the bow rather than to north. Meaningless in
        /// standby, where the last picture is frozen; the renderer still needs an answer, and the last
        /// orientation is the honest one.</summary>
        public readonly bool HeadUp => Mode != RadarMode.NorthUp;

        /// <summary>
        /// Step the MODE ring one press: <b>standby → head-up → north-up → standby</b>. A ring rather
        /// than a toggle pair because the rig draws one MODE key, and it is ordered so the first press
        /// on a set left in standby puts it back on air — the thing you actually want that key to do.
        /// </summary>
        public readonly RadarPrefs SteppedMode()
        {
            RadarMode next = Mode switch
            {
                RadarMode.Standby => RadarMode.HeadUp,
                RadarMode.HeadUp => RadarMode.NorthUp,
                _ => RadarMode.Standby,
            };
            return new RadarPrefs(Night, next, RangeStep);
        }

        /// <summary>
        /// Step the range one rung and return the moved preferences. <paramref name="delta"/> is +1 for
        /// the RANGE-IN pusher (a CLOSER range — a smaller rung) and −1 for OUT, so the caller says what
        /// the key means and this owns the arithmetic and the clamp.
        ///
        /// <para>Clamped, never wrapped — the <see cref="ChartplotterPrefs.SteppedRange"/> ruling: a set
        /// at its closest range and pressed IN again stays where it is. Jumping to twenty-four miles
        /// while you are picking your way into a harbour would be a very unwelcome surprise.</para>
        /// </summary>
        public readonly RadarPrefs SteppedRange(int delta, in RadarSettings cfg)
        {
            int max = cfg.SafeRangeStepCount - 1;
            int step = RangeStep - delta;               // IN (+1) means a lower rung
            if (step < 0) step = 0;
            else if (step > max) step = max;
            return new RadarPrefs(Night, Mode, step);
        }

        /// <summary>Toggle the night backlight override.</summary>
        public readonly RadarPrefs WithNight(bool night) => new RadarPrefs(night, Mode, RangeStep);

        /// <summary>Set the mode outright (tests, and a future "switch every instrument to standby").</summary>
        public readonly RadarPrefs WithMode(RadarMode mode) => new RadarPrefs(Night, mode, RangeStep);
    }
}
