namespace HiddenHarbours.Core
{
    /// <summary>
    /// One hull's <b>chartplotter preferences</b> — how that boat's glass is set up: the amber night
    /// backlight, north-up vs head-up, and which rung of the range ladder it is on. The skipper who
    /// leaves his plotter head-up at 0.1 NM expects to find it that way tomorrow, so these persist
    /// (per hull, beside the owned instrument ids — <see cref="InstrumentLocker"/>).
    ///
    /// <para><b>Why per HULL, and why not folded into <see cref="SounderPrefs"/>.</b> The nav DATA is
    /// per SAVE and region-stamped, because a waypoint is the skipper's knowledge of the water
    /// (<see cref="NavLocker"/>). These are the opposite: they describe a piece of GLASS, and the glass
    /// is equipment owned per hull. They are their own record rather than more fields on
    /// <see cref="SounderPrefs"/> because that struct's "one record for two instruments" is earned by a
    /// specific structural fact — the sounder and the finder contend for the SAME brow cutout, so they
    /// can never be fitted together and cannot want different settings. The plotter sits in a different
    /// slot entirely (the brow's GPS station) and is fitted ALONGSIDE whichever sounder is in the
    /// cutout, so sharing a record would mean one tap on the chart silently re-lighting the sounder.
    /// </para>
    ///
    /// <para><b>Preferences, never sim inputs</b> — the <see cref="SounderPrefs"/> rule, unchanged.
    /// Nothing here reaches the simulation: the orientation decides which way the picture is turned,
    /// not which way the boat is pointing, and the range decides how much sea is on the glass, not how
    /// much sea exists. Nothing derivable is stored — the chart itself, the tide and the track are all
    /// recomputed or accrued elsewhere (rule 5).</para>
    ///
    /// <para>⚠ <b>Schema:</b> persisted as <c>ChartplotterPrefsDto</c> in an ADDITIVE v10 member
    /// (<c>SaveData.HullChartplotterPrefs</c>) — the same version the nav lists themselves landed at,
    /// so there is no migration and no version bump. A save written before this member existed simply
    /// has no rows, which reads as "never touched" and yields <see cref="FromDefaults"/>.</para>
    /// </summary>
    [System.Serializable]
    public struct ChartplotterPrefs
    {
        /// <summary>Amber night backlight rather than the day palette (the rig's <c>night</c> signal).
        /// This is the EARLY-ON override only: the glass draws night when this OR the hull's panel light
        /// says so, which is the standing rule every instrument follows.</summary>
        public bool Night;

        /// <summary>Chart turned so the boat's course is at the top, rather than north
        /// (navRig.js:230). False is north-up, which is the chart convention and the default.</summary>
        public bool HeadUp;

        /// <summary>Which rung of <see cref="ChartplotterSettings"/>'s range ladder the glass is on
        /// (0 = closest). Stored as the RUNG, not the resulting nautical miles, so that re-tuning
        /// <see cref="ChartplotterSettings.MinRangeNM"/> moves every saved plotter with it instead of
        /// stranding it on a range the ladder no longer has.</summary>
        public int RangeStep;

        public ChartplotterPrefs(bool night, bool headUp, int rangeStep)
        {
            Night = night;
            HeadUp = headUp;
            RangeStep = rangeStep;
        }

        /// <summary>The out-of-the-box settings for a freshly fitted plotter, from the owner's tuning
        /// (rule 6 — the numbers are data, not literals here). Day, north-up, and whichever rung the
        /// owner nominated as the one a new unit wakes on.</summary>
        public static ChartplotterPrefs FromDefaults(in ChartplotterSettings cfg)
            => new ChartplotterPrefs(false, false, cfg.SafeDefaultRangeStep);

        /// <summary>
        /// The range in NM this hull's glass is showing, healed against both a garbage stored rung and
        /// an unwritten config block. The ONE place the pref meets the ladder, so a stored step that no
        /// longer exists (the owner shortened the ladder) shows the nearest real range rather than
        /// nothing at all.
        /// </summary>
        public readonly float RangeNM(in ChartplotterSettings cfg) => cfg.RangeNMAt(RangeStep);

        /// <summary>
        /// Step the range one rung and return the moved preferences. <paramref name="delta"/> is +1 for
        /// the IN pusher (a CLOSER range — a smaller rung) and −1 for OUT, so the caller says what the
        /// key means and this owns the arithmetic and the clamp.
        ///
        /// <para>Clamped, never wrapped: a plotter at its closest range and pressed IN again stays
        /// where it is. Wrapping to the widest range would be a very unwelcome surprise at a wharf.</para>
        /// </summary>
        public readonly ChartplotterPrefs SteppedRange(int delta, in ChartplotterSettings cfg)
        {
            int max = cfg.SafeRangeStepCount - 1;
            int step = RangeStep - delta;               // IN (+1) means a lower rung
            if (step < 0) step = 0;
            else if (step > max) step = max;
            return new ChartplotterPrefs(Night, HeadUp, step);
        }

        /// <summary>The orientation as the renderer's own enum lives in the UI lane, so this stays a
        /// bool here and is mapped at the one call site that draws.</summary>
        public readonly ChartplotterPrefs WithHeadUp(bool headUp) => new ChartplotterPrefs(Night, headUp, RangeStep);

        /// <summary>Toggle the night backlight override.</summary>
        public readonly ChartplotterPrefs WithNight(bool night) => new ChartplotterPrefs(night, HeadUp, RangeStep);
    }
}
