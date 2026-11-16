namespace HiddenHarbours.Core
{
    /// <summary>
    /// What a wash station does to dirt, resolved from its <see cref="WashStationDef"/>: a rate toward a
    /// target dirt (1 - its ceiling), and whether it also dirties a cleaner player (the sea). The default
    /// (rate 0) does nothing: a toilet, or no station.
    /// </summary>
    public readonly struct WashPull
    {
        public readonly float RatePerGameHour;
        public readonly float TargetDirt;
        public readonly bool BothWays;

        public WashPull(float ratePerGameHour, float targetDirt, bool bothWays)
        {
            RatePerGameHour = ratePerGameHour;
            TargetDirt = targetDirt;
            BothWays = bothWays;
        }

        public bool Acts => RatePerGameHour > 0f;
    }

    /// <summary>
    /// <b>The pure maths of cleanliness</b> (cleanliness slice 1). No Unity state, no service: a saved
    /// <see cref="PlayerCleanlinessDto"/>, the clock, the owner's <see cref="CleanlinessSettings"/> and the
    /// station being washed at in; a dirt level out.
    ///
    /// <para><b>Deterministic, and stepping equals catch-up.</b> Every regime is a straight line clamped at
    /// a bound (rising to 1, or moving toward a wash target), and the guard window counts from the stretch's
    /// own start, so re-anchoring at any instant (<see cref="Settle"/>) and reading on gives exactly the
    /// level a single read over the whole span gives. No roll, no frame step (rule 5).</para>
    ///
    /// <para><b>The ceilings.</b> A wash only moves dirt DOWN toward its target and never past it, so a sink
    /// can never get you cleaner than its ceiling however long you stand there. The sea
    /// (<see cref="WashPull.BothWays"/>) also moves a cleaner player UP to its base: it pulls toward the base
    /// from either side.</para>
    /// </summary>
    public static class CleanlinessMath
    {
        /// <summary>The pull of <paramref name="station"/>; the do-nothing pull for null, a toilet, or a
        /// zero rate.</summary>
        public static WashPull PullOf(WashStationDef station)
        {
            if (station == null || !station.Washes) return default;
            return new WashPull(station.CleanRatePerGameHour, 1f - Clamp01(station.Ceiling), station.PullsBothWays);
        }

        /// <summary><paramref name="dirt"/> raised by <paramref name="amount"/>, clamped to 0..1.</summary>
        public static float Rise(float dirt, double amount)
        {
            if (!(amount > 0d)) return Clamp01(dirt);
            double v = dirt + amount;
            return v >= 1d ? 1f : (float)v;
        }

        /// <summary><paramref name="dirt"/> after <paramref name="hours"/> of <paramref name="pull"/>: down
        /// toward the target, never past it; up toward it as well only when the pull is both ways.</summary>
        public static float Approach(float dirt, in WashPull pull, double hours)
        {
            float d = Clamp01(dirt);
            if (!pull.Acts || !(hours > 0d)) return d;
            float target = Clamp01(pull.TargetDirt);
            double step = pull.RatePerGameHour * hours;
            if (d > target) { double v = d - step; return v <= target ? target : (float)v; }
            if (d < target && pull.BothWays) { double v = d + step; return v >= target ? target : (float)v; }
            return d;
        }

        /// <summary>The pure "wash at a station for a duration": <paramref name="dirt"/> after
        /// <paramref name="gameHours"/> at <paramref name="station"/>.</summary>
        public static float AfterWash(float dirt, WashStationDef station, double gameHours)
            => Approach(dirt, PullOf(station), gameHours);

        /// <summary>The extra dirt per game hour a stretch of <paramref name="kind"/> adds (first matching
        /// row; 0 for none or no row).</summary>
        public static float RateOf(DirtActivity kind, in CleanlinessSettings settings)
        {
            if (kind == DirtActivity.None || settings.Sources == null) return 0f;
            for (int i = 0; i < settings.Sources.Length; i++)
                if (settings.Sources[i].Kind == kind)
                    return settings.Sources[i].RisePerGameHour > 0f ? settings.Sources[i].RisePerGameHour : 0f;
            return 0f;
        }

        /// <summary>The dirt one reported action of <paramref name="kind"/> adds (first matching row).</summary>
        public static float BumpOf(DirtActivity kind, in CleanlinessSettings settings)
        {
            if (kind == DirtActivity.None || settings.Sources == null) return 0f;
            for (int i = 0; i < settings.Sources.Length; i++)
                if (settings.Sources[i].Kind == kind)
                    return settings.Sources[i].PerAction > 0f ? settings.Sources[i].PerAction : 0f;
            return 0f;
        }

        /// <summary>The guard window in clock seconds.</summary>
        public static double StretchSeconds(in CleanlinessSettings settings, double secondsPerGameHour)
        {
            double minutes = settings.MaxStretchGameMinutes >= 1f ? settings.MaxStretchGameMinutes : 1d;
            return minutes * secondsPerGameHour / 60d;
        }

        /// <summary>
        /// The dirt of <paramref name="state"/> at <paramref name="nowGameSeconds"/>. While washing
        /// (<paramref name="pull"/> acts and the state names a station) the wash replaces the passive rate;
        /// during a stretch of work its rate adds to the passive rate; either lasts at most the guard window
        /// from the stretch's start, after which only the passive rate runs. An unstamped state, a clock
        /// behind the stamp, or no sensible hour length holds the saved dirt.
        /// </summary>
        public static float DirtAt(PlayerCleanlinessDto state, in CleanlinessSettings settings, in WashPull pull,
                                   double nowGameSeconds, double secondsPerGameHour)
        {
            if (state == null) return 0f;
            float d = Clamp01(state.Dirt);
            if (!state.IsStamped || !(secondsPerGameHour > 0d)) return d;
            double from = state.SinceGameSeconds;
            if (!(nowGameSeconds > from)) return d;

            float passive = settings.Curve.RisePerGameHour > 0f ? settings.Curve.RisePerGameHour : 0f;
            bool washing = state.IsWashing && pull.Acts;
            float activityRate = washing ? 0f : RateOf(state.ActivityKind, settings);
            bool stretch = washing || activityRate > 0f;

            if (stretch)
            {
                double start = state.StretchSinceGameSeconds >= 0d ? state.StretchSinceGameSeconds : from;
                double end = start + StretchSeconds(settings, secondsPerGameHour);
                double activeEnd = nowGameSeconds < end ? nowGameSeconds : end;
                if (activeEnd > from)
                {
                    double hours = (activeEnd - from) / secondsPerGameHour;
                    d = washing ? Approach(d, pull, hours) : Rise(d, (passive + activityRate) * hours);
                    from = activeEnd;
                }
            }

            return Rise(d, passive * ((nowGameSeconds - from) / secondsPerGameHour));
        }

        /// <summary>True when the state's stretch (work or wash) has run past its guard window by
        /// <paramref name="nowGameSeconds"/>, so it can be closed without changing any level.</summary>
        public static bool StretchExpired(PlayerCleanlinessDto state, in CleanlinessSettings settings,
                                          double nowGameSeconds, double secondsPerGameHour)
        {
            if (state == null || state.StretchSinceGameSeconds < 0d) return false;
            if (!state.IsWashing && state.Activity == 0) return false;
            return nowGameSeconds >= state.StretchSinceGameSeconds + StretchSeconds(settings, secondsPerGameHour);
        }

        /// <summary>
        /// <b>At a save write, or before a change</b>: re-anchor a stamped state at its dirt NOW, keeping the
        /// current stretch and its start, so the line goes on unchanged. Null-safe.
        /// </summary>
        public static void Settle(PlayerCleanlinessDto state, in CleanlinessSettings settings, in WashPull pull,
                                  double nowGameSeconds, double secondsPerGameHour)
        {
            if (state == null || !state.IsStamped || nowGameSeconds < 0d) return;
            state.Dirt = DirtAt(state, settings, pull, nowGameSeconds, secondsPerGameHour);
            state.SinceGameSeconds = nowGameSeconds;
        }

        /// <summary>
        /// <b>At a clock seek</b> (a restore, never time passing): keep the SAVED dirt, anchor it at
        /// <paramref name="nowGameSeconds"/>, and end any stretch (after a load the player is not standing
        /// in a shower). Null-safe; an unstamped state stays unstamped.
        /// </summary>
        public static void Rebase(PlayerCleanlinessDto state, double nowGameSeconds)
        {
            if (state == null || nowGameSeconds < 0d) return;
            if (state.IsStamped) state.SinceGameSeconds = nowGameSeconds;
            state.Activity = 0;
            state.WashStationId = "";
            state.StretchSinceGameSeconds = NeedStateDto.Unstamped;
        }

        internal static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : (float.IsNaN(v) ? 0f : v);
    }
}
