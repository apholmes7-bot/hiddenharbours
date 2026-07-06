namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The pure soak math for a placed trap (trap-fishing arc Build 3). A trap sitting on the seabed must
    /// <b>soak</b> for its Def's <c>SoakHours</c> before it's worth hauling; how far along it is, and
    /// whether it's ready, are a pure function of the clock — <c>now − placedAt</c> against the soak span.
    ///
    /// <para><b>No stored progress, no per-tick counter (rule 5).</b> Nothing here accumulates or is saved.
    /// Progress is recomputed on demand from the two game-time anchors (placement time + now) and the Def's
    /// soak span, exactly the way tide is recomputed from <c>(seed, gameTime)</c> rather than integrated.
    /// A save records only the placement instant (<see cref="Core.PlacedTrapDto.PlacementGameTimeSeconds"/>);
    /// the soak reconstructs identically on load. Static + parameterised so it's fully EditMode-testable with
    /// no engine, no clock, no scene.</para>
    /// </summary>
    public static class TrapSoak
    {
        /// <summary>In-game seconds in one in-game hour. The soak Def is authored in HOURS
        /// (<see cref="TrapDef.SoakHours"/>); the clock runs in seconds — this is the one conversion, not a
        /// tunable (an hour is 3600 seconds by definition, so it's a constant, not a magic number).</summary>
        public const double SecondsPerHour = 3600.0;

        /// <summary>The soak span in in-game seconds for a trap whose Def asks for <paramref name="soakHours"/>.
        /// A non-positive soak (or NaN) reads as 0 — an "instant" trap that's ready the moment it's down.</summary>
        public static double SoakSeconds(float soakHours)
            => soakHours > 0f ? soakHours * SecondsPerHour : 0.0;

        /// <summary>How long the trap has been soaking (in-game seconds): <c>now − placedAt</c>, floored at 0
        /// (a now earlier than placement — a clock oddity — reads as "just placed", never negative).</summary>
        public static double ElapsedSeconds(double placedAtSeconds, double nowSeconds)
        {
            double elapsed = nowSeconds - placedAtSeconds;
            return elapsed > 0.0 ? elapsed : 0.0;
        }

        /// <summary>Soak progress in [0,1]: <c>clamp01((now − placed) / soakSpan)</c>. A zero/negative soak
        /// span is treated as already complete (returns 1). Pure; no side effects, nothing stored.</summary>
        public static float Progress01(double placedAtSeconds, double nowSeconds, float soakHours)
        {
            double span = SoakSeconds(soakHours);
            if (span <= 0.0) return 1f;
            double p = ElapsedSeconds(placedAtSeconds, nowSeconds) / span;
            if (p < 0.0) p = 0.0;
            if (p > 1.0) p = 1.0;
            return (float)p;
        }

        /// <summary>True once the trap has soaked its full span: <c>(now − placed) ≥ soakHours·3600</c>. A
        /// zero/negative soak span is ready immediately. This is the readiness gate the haul/dev-check reads
        /// (a not-ready trap yields nothing — the "wait for it to soak" beat, P4/P5).</summary>
        public static bool IsReady(double placedAtSeconds, double nowSeconds, float soakHours)
        {
            double span = SoakSeconds(soakHours);
            if (span <= 0.0) return true;
            return ElapsedSeconds(placedAtSeconds, nowSeconds) >= span;
        }
    }
}
