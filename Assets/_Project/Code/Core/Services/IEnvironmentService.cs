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
    }
}
