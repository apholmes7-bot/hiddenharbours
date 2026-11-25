using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Produces the sea's mood (tide, wind, sea state, fog, current) deterministically from
    /// the world seed and the clock. Because it is deterministic, the environment is
    /// recomputed rather than saved (docs/architecture/tech-architecture.md §1, §6).
    /// </summary>
    public interface IEnvironmentService
    {
        int WorldSeed { get; }

        /// <summary>The tide profile of the active region. Set when a region is entered.</summary>
        TideProfile ActiveTideProfile { get; set; }

        /// <summary>Full environment snapshot for the active region at the current time.</summary>
        EnvironmentSample Sample();

        /// <summary>Tide height (m rel. datum) at an arbitrary time, for the active region —
        /// e.g. to draw a tide table / forecast.</summary>
        float TideHeightAt(double totalSeconds);

        /// <summary>
        /// The deterministic <b>water-surface level</b> for the active region at the given time, in
        /// <b>metres above chart datum</b> — the accessor the tidal-exposure query reads
        /// (<see cref="TidalExposure"/>). It is the height the on-foot walkability sim and terrain
        /// authoring both compare against authored ground elevation to answer "submerged or exposed
        /// here, now?" for the St Peters sandbar and the Drownded Lands flats (design/world-and-regions.md
        /// §7, design/time-tides-weather.md §3.5).
        ///
        /// <para><b>Additive &amp; non-breaking.</b> Provided as a <i>default interface method</i> that
        /// returns <see cref="TideHeightAt"/> — the tide <em>is</em> the water level for the
        /// inshore/intertidal regions — so existing implementers (and test fakes) compile unchanged.
        /// It is named separately so consumers express intent ("the water level I walk under") and so a
        /// region that later offsets its local water plane from raw tide height can <b>override</b> this
        /// one accessor without touching every call site. Like all of this service it is
        /// <b>deterministic</b> — recomputed from <c>(worldSeed, gameTime)</c>, never saved
        /// (CLAUDE.md rule 5).</para>
        /// </summary>
        float WaterLevelAt(double totalSeconds) => TideHeightAt(totalSeconds);

        /// <summary>
        /// The deterministic <b>continuous sea state</b> (0 glass .. 1 storm) at an arbitrary time — the
        /// weather twin of <see cref="TideHeightAt"/>, and read for exactly the same reason: a consumer
        /// that reasons about a SPAN of time rather than this instant needs the weather at the instant it
        /// is reasoning about, not at the instant it happens to be asking.
        ///
        /// <para><b>Who needed it.</b> The fish-school sim (ADR 0025 S3) decides whether a patch of water
        /// holds fish from the weather at the moment that school formed, and then that school stands for
        /// its whole window. Reading <see cref="Sample"/> instead would re-decide the question every
        /// frame against a drifting wind, and a school would blink in and out as the sea state wandered
        /// across a threshold — which the fish finder would faithfully draw. Schools are recomputed from
        /// <c>(worldSeed, gameTime)</c> like the tide (rule 5), and that is only meaningful if the weather
        /// term is evaluated at a FIXED time.</para>
        ///
        /// <para><b>Additive &amp; non-breaking</b>, exactly as <see cref="WaterLevelAt"/> is: a
        /// <i>default interface method</i> returning <see cref="Sample"/>'s value, so every existing
        /// implementer and test fake compiles unchanged and degrades to "the weather I can see". The real
        /// <c>EnvironmentService</c> overrides it with the pure <c>WeatherModel</c> evaluation at the
        /// requested time.</para>
        /// FLAG lead-architect: additive Core contract (the weather-at-a-time read the S3 school sim needs).
        /// </summary>
        float SeaState01At(double totalSeconds) => Sample().SeaState01;

        /// <summary>
        /// Which <b>tide</b> the given moment belongs to, for the active region: an index that steps by
        /// exactly one at each high water and is constant between two of them. Two moments with the same
        /// index have had no high water between them; two with different indices have had the flood come
        /// in and cover the flats at least once.
        ///
        /// <para><b>Who needed it.</b> A dug clam hole stays dug "until the tide has covered the flat
        /// again". The hole keeps the one fact that cannot be recomputed — WHEN it was dug — and asks this
        /// whether that moment and now are the same tide. Nothing about the tide is stored: like
        /// <see cref="TideHeightAt"/> this is a pure function of the clock and the region's tide profile
        /// (rule 5), so it reads the same after a save and load as it did before.</para>
        ///
        /// <para><b>Additive &amp; non-breaking</b>, as <see cref="WaterLevelAt"/> is: a <i>default
        /// interface method</i> returning <c>0</c>, which reads as "one endless tide" — every existing
        /// implementer and test fake compiles unchanged and keeps the behaviour it had (a spent thing
        /// stays spent). The real <c>EnvironmentService</c> overrides it with the pure tide model.</para>
        /// FLAG lead-architect: additive Core contract (the tide-cycle read the clam flats need).
        /// </summary>
        long TideCycleIndexAt(double totalSeconds) => 0L;
    }
}
