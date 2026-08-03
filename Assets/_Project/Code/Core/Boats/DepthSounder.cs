namespace HiddenHarbours.Core
{
    /// <summary>
    /// One hull's <b>sounder preferences</b> — the shallow set-point, whether the alarm is armed, the two
    /// display toggles (feet vs metres, night backlight), and the fish finder's vertical RANGE. A fisherman
    /// sets his shallow alarm once and expects to find it there tomorrow, so these DO persist (per hull,
    /// beside the owned instrument ids — <c>InstrumentLocker</c>).
    ///
    /// <para><b>One record for both instruments.</b> The fish finder supersedes the depth sounder in the
    /// same cutout (<c>BoatEquipment.EffectiveFit</c>) and keeps its shallow alarm unchanged, so it shares
    /// this record rather than growing a parallel one that could be tuned apart.
    /// <see cref="RangeMetres"/> is the only field the finder adds (ADR 0025 S3, schema v9).</para>
    ///
    /// <para><b>Preferences, never sim inputs.</b> Nothing here feeds the simulation: the alarm decides
    /// whether the glass flashes, not whether the boat grounds; the range decides how tall the picture is,
    /// not where the fish are. The <em>depth</em> itself is never in this struct and never in the save — it
    /// is recomputed from the one height map every tick (<see cref="DepthSounder.DisplayDepth"/>, rule 5).
    /// A saved depth is the exact bug that rule exists to prevent, and the same goes for a saved school.</para>
    /// </summary>
    [System.Serializable]
    public struct SounderPrefs
    {
        /// <summary>The shallow set-point in METRES above which the sounder is quiet. Stored in metres
        /// whatever the display units, so toggling feet never re-tunes the alarm.</summary>
        public float AlarmMetres;

        /// <summary>Is the shallow alarm armed at all? (The rig's <c>armed</c> signal.)</summary>
        public bool Armed;

        /// <summary>Display in FEET rather than metres (the rig's <c>ft</c> signal).</summary>
        public bool Feet;

        /// <summary>Amber night backlight rather than the day panel (the rig's <c>night</c> signal).</summary>
        public bool Night;

        /// <summary>
        /// The fish finder's vertical scale in METRES — how deep the bottom of the glass reaches (the rig's
        /// <c>range</c> signal; its own steps are 10/20/40/60 m). Stored in metres whatever the display
        /// units, exactly like <see cref="AlarmMetres"/>, so toggling feet never re-scales the picture.
        ///
        /// <para><b>⚠ Must be positive.</b> Every mark and the bottom contour are drawn at
        /// <c>depth / range</c>, so a zero here is Inf/NaN — a garbage picture, not a small one. No
        /// constructor on this type can produce a zero, and <c>SaveMigration</c> heals any stored row that
        /// somehow carries one.</para>
        /// </summary>
        public float RangeMetres;

        /// <summary>The four-field constructor as v8 had it. Kept so no S2 call site churns; the finder's
        /// range takes the shipped default rather than a zero, because a zero range is never a legal
        /// value (see <see cref="RangeMetres"/>).</summary>
        public SounderPrefs(float alarmMetres, bool armed, bool feet, bool night)
            : this(alarmMetres, armed, feet, night, FishFinderSettings.Default.DefaultRangeMetres) { }

        public SounderPrefs(float alarmMetres, bool armed, bool feet, bool night, float rangeMetres)
        {
            AlarmMetres = alarmMetres;
            Armed = armed;
            Feet = feet;
            Night = night;
            RangeMetres = rangeMetres;
        }

        /// <summary>The out-of-the-box preferences for a freshly fitted sounder, from the owner's tuning
        /// (rule 6 — the numbers are data, not literals here). The finder's range takes
        /// <see cref="FishFinderSettings.Default"/>; use the two-settings overload to honour the owner's
        /// tuned value.</summary>
        public static SounderPrefs FromDefaults(in DepthSounderSettings settings)
            => FromDefaults(in settings, FishFinderSettings.Default);

        /// <summary>The out-of-the-box preferences for a freshly fitted sounder OR finder, both blocks of
        /// the owner's tuning honoured.</summary>
        public static SounderPrefs FromDefaults(in DepthSounderSettings settings,
                                                in FishFinderSettings finder)
            => new SounderPrefs(settings.DefaultAlarmMetres, settings.DefaultArmed, settings.DefaultFeet,
                                false, finder.DefaultRangeMetres);
    }

    /// <summary>
    /// The depth sounder's pure rules (ADR 0025 S2) — what the glass reads and when it flashes. Static,
    /// engine-light and deterministic, so the whole instrument's behaviour is EditMode-testable without a
    /// scene, a canvas or a boat.
    ///
    /// <para><b>"Is it deep" IS "is it wet".</b> The depth is
    /// <see cref="TidalExposure.WaterDepth"/> over the SAME height map the water render, the walkability
    /// sim and boat grounding read (<see cref="ITidalTerrain"/> + <see cref="IEnvironmentService.WaterLevelAt"/>),
    /// so the sounder can never disagree with the sea the player is looking at. No second copy of the
    /// height map, no cached depth anywhere — recompute, never store (rule 5).</para>
    /// </summary>
    public static class DepthSounder
    {
        /// <summary>
        /// The depth the LCD shows, in metres: the water over the seabed at the transducer, <b>clamped at
        /// 0</b> when aground (a negative depth is dry ground — the sounder reads 0.0, it does not report
        /// a negative sounding). <paramref name="waterLevel"/> and <paramref name="terrainElevation"/>
        /// are both metres above chart datum, the one frame the tide model uses.
        /// </summary>
        public static float DisplayDepth(float waterLevel, float terrainElevation)
        {
            float depth = TidalExposure.WaterDepth(waterLevel, terrainElevation);
            return depth > 0f ? depth : 0f;
        }

        /// <summary>The rig's own trigger (<c>depthRig.js:160</c> — <c>armed &amp;&amp; depth &lt;= alarm</c>):
        /// the SHALLOW banner flashes and the reading goes red. Inclusive at the set-point, exactly as
        /// the rig has it.</summary>
        public static bool ShallowAlarm(float displayDepth, in SounderPrefs prefs)
            => prefs.Armed && displayDepth <= prefs.AlarmMetres;

        /// <summary>
        /// Move the shallow set-point by <paramref name="steps"/> of the owner's step size (the rig's two
        /// ALARM pushers are ±1 step — <c>depth-finder/README.md</c>), clamped into the tunable range.
        /// Pure; the caller persists the result as a preference.
        /// </summary>
        public static float StepAlarm(float alarmMetres, int steps, in DepthSounderSettings settings)
        {
            float stepped = alarmMetres + steps * settings.AlarmStepMetres;
            if (stepped < settings.MinAlarmMetres) stepped = settings.MinAlarmMetres;
            if (stepped > settings.MaxAlarmMetres) stepped = settings.MaxAlarmMetres;
            return stepped;
        }

        /// <summary>
        /// The alarm's blink phase at a moment: true for the first half of each cycle, false for the
        /// second, at <paramref name="hz"/> full cycles per second (data — rule 6). Pure function of the
        /// clock, so the phase is the same wherever it is asked and the host can repaint on the FLIP
        /// rather than every frame (rule 7).
        /// </summary>
        public static bool BlinkPhase(double timeSeconds, float hz)
        {
            if (hz <= 0f) return true;                       // a zero rate means "lit, not flashing"
            long half = (long)System.Math.Floor(timeSeconds * hz * 2.0);
            return (half & 1L) == 0L;
        }
    }
}
