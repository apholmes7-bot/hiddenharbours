using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// THE RADAR-CONTACT SEAM (ADR 0025 S5) — where "what is out there" is asked and answered.
    ///
    /// <para><b>Shaped on <see cref="IFishSchools"/> on purpose.</b> Same caller-owned list, same
    /// cleared-and-filled contract, same null-object fallback, same never-saved discipline. That is not
    /// imitation for its own sake: the fish seam is the validated answer to this exact problem — a UI
    /// instrument that must draw live world state without the UI module referencing the module that
    /// owns it (rule 4) — and a second seam that solved it differently would be a second thing to keep
    /// in step.</para>
    ///
    /// <para><b>Reads, never state.</b> The ambient fleet is a closed-form function of
    /// <c>(worldSeed, gameTime)</c> and the player's gear lives in the save; this seam owns neither. It
    /// exposes what a producer already knows, so the same seed at the same time yields the same
    /// contacts, and nothing radar-related enters the save (rule 5). A radar is a SENSOR: everything it
    /// shows exists whether or not one is fitted.</para>
    ///
    /// <para><b>"Within range", not "at" — the one deliberate divergence from the fish seam.</b>
    /// <see cref="IFishSchools.SchoolsAt"/> asks a containment question because a school owns its own
    /// radius and the player drives INTO it. A radar is the opposite instrument: it sweeps a disc
    /// around the aerial and everything inside answers. So this query carries an explicit
    /// <paramref name="rangeMetres"/>, and there is exactly one radius in the system — the one the
    /// glass is showing — rather than a producer-side search radius that could drift out of step with
    /// it.</para>
    ///
    /// <para><b>Land is NOT on this seam.</b> The coast is not a contact somebody publishes; it is the
    /// terrain height map every lane already reads through <see cref="GameServices.TidalTerrain"/>, and
    /// the instrument samples it directly the way the chartplotter's survey does. Routing it through
    /// here would mean a producer re-deriving the coastline from the same height map and publishing
    /// thousands of point echoes per sweep — a rule-7 disaster for a shape that is already available as
    /// a function. See <c>RadarLandEcho</c> for what a radar's waterline is and why it is the tide of
    /// the moment rather than the chart's datum.</para>
    ///
    /// <para><b>No allocation on the read path</b> (rule 7). One caller-owned list, cleared first,
    /// refilled, count returned — the <see cref="IFishSchools"/>/<c>InstrumentLocker.OwnedFor</c> shape.
    /// The instrument repaints on its own change-detection and reuses one list forever.</para>
    /// FLAG lead-architect: new Core contract (the ADR 0025 S5 radar-contact seam).
    /// </summary>
    public interface IRadarContacts
    {
        /// <summary>
        /// Every contact within <paramref name="rangeMetres"/> of <paramref name="worldPos"/> as the
        /// world stands at <paramref name="gameSeconds"/> (the <c>IGameClock.TotalSeconds</c> frame).
        /// <paramref name="into"/> is cleared first and returned filled; the return value is how many
        /// were written. A null list is tolerated (nothing is written, the count is still correct).
        ///
        /// <para>Order is not specified and callers must not depend on it — the renderer paints every
        /// contact and the sweep decides brightness, so nothing on the glass is a function of index.</para>
        /// </summary>
        int ContactsAt(Vector2 worldPos, float rangeMetres, double gameSeconds, List<RadarContact> into);
    }

    /// <summary>
    /// THE QUIET SEA — the null-object <see cref="IRadarContacts"/>: no contacts, ever, anywhere.
    /// <see cref="GameServices.RadarContacts"/> is this until a real producer registers, and falls back
    /// to it the moment one goes away.
    ///
    /// <para><b>An empty scope is a reading, not a fault.</b> This is the correct SHIPPED behaviour
    /// everywhere a producer legitimately does not exist — a bare art scene, an EditMode rig, a region
    /// with no ambient fleet authored — and it is also what a real radar shows on an empty sea. The
    /// coast still paints, because land comes from the terrain rather than from here, so "no producer"
    /// looks like open water rather than like a dead instrument. A
    /// <c>NullReferenceException</c> would be neither.</para>
    ///
    /// <para>Stateless and immutable, so the one <see cref="Instance"/> is safe to share.</para>
    /// </summary>
    public sealed class EmptyRadarSea : IRadarContacts
    {
        /// <summary>The shared instance. There is no state to give a second one.</summary>
        public static readonly EmptyRadarSea Instance = new EmptyRadarSea();

        private EmptyRadarSea() { }

        /// <inheritdoc/>
        public int ContactsAt(Vector2 worldPos, float rangeMetres, double gameSeconds,
                              List<RadarContact> into)
        {
            into?.Clear();
            return 0;
        }
    }
}
