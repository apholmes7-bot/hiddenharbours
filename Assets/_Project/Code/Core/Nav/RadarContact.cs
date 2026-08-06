using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// What KIND of thing a radar echo is — the five shapes the rig itself authors
    /// (<c>docs/art/rigs/ui/radar/Art/radarRig.js:130</c> <c>KINDS</c>), in the source's own order so
    /// the transcription is a cast rather than a lookup table that can drift.
    ///
    /// <para><b>The kind is the PAINTER, not a classification.</b> The rig draws each one differently —
    /// a vessel is a solid return with a motion trail, land is a lumpy mass, a buoy is a small ringed
    /// dot, rain and flocks are stipple — so a producer picks the kind that says how the return should
    /// LOOK on a scope, not what the object is in the fiction. That is why a rain cell and a bird flock
    /// are peers of a boat here: to a radar they are all just returns of different texture.</para>
    ///
    /// <para><see cref="Rain"/> and <see cref="Flock"/> have no producer in this slice and are carried
    /// because the rig paints them: weather returns are the Smother's payoff (canon M4) and nothing in
    /// this PR builds weather. They cost nothing to carry and inventing the enum a second time later
    /// would break the append-only rule the def ids follow.</para>
    /// </summary>
    public enum RadarEchoKind
    {
        Vessel = 0,   // solid strong return + afterglow trail when making way (radarRig.js:253-262)
        Land   = 1,   // lumpy coastal mass (radarRig.js:236-241)
        Buoy   = 2,   // small dot in a faint ring (radarRig.js:242-244)
        Rain   = 3,   // stipple cloud — no producer yet (M4)
        Flock  = 4,   // tighter stipple — no producer yet
    }

    /// <summary>
    /// ONE radar return, in WORLD terms — the unit of <see cref="IRadarContacts"/>.
    ///
    /// <para><b>World metres, never bearing-and-range.</b> The rig's own target shape is
    /// <c>{brgTrue, rngNM, size, kind, crs, spd}</c> (radarRig.js:137-145) and it would have been
    /// shorter to publish that. It is deliberately not what crosses this seam. A bearing is only
    /// meaningful relative to a specific own-ship position, so a producer that computed one would have
    /// to know where the player is and would bake a second bearing convention beside
    /// <see cref="BoatKinematics.BearingDegrees"/> — exactly the trap <see cref="NavMath"/> was created
    /// to close. Producers publish where things ARE; the instrument converts once, at paint time,
    /// through the one convention (0 = N, clockwise). The seam therefore also serves a second radar on
    /// a second boat without either producer knowing.</para>
    ///
    /// <para><b>Nothing here is saved</b> (rule 5). Every contact is recomputed from
    /// <c>(worldSeed, gameTime)</c> plus live positions, the same contract the tide, the wind and the
    /// fish schools keep.</para>
    /// </summary>
    public readonly struct RadarContact
    {
        /// <summary>Where the return is, in world metres — the same frame
        /// <see cref="IHelmInstruments.TryReadPosition"/> reports the transducer in.</summary>
        public readonly Vector2 WorldPos;

        /// <summary>Which painter draws it.</summary>
        public readonly RadarEchoKind Kind;

        /// <summary>The rig's echo SIZE multiplier (radarRig.js:153 — the blip's half-extent is
        /// <c>max(3, r*0.05) * size</c>, and <c>strengthIdx</c> bands a vessel's colour off it at
        /// 0.8 / 1.3 / 1.9). Roughly "how big a target is this": a punt is under one, a coastal
        /// headland is over two. Clamped by the renderer, so a wild value cannot blow up the scope.</summary>
        public readonly float Size;

        /// <summary>The contact's COURSE in the one convention (0 = N, clockwise), or
        /// <see cref="float.NaN"/> for something that is not making way — a buoy, a rock, a hove-to
        /// boat. The rig draws a motion trail only for a vessel with speed, so an honest NaN here is
        /// what stops a moored punt growing a wake on the glass.</summary>
        public readonly float CourseDegrees;

        /// <summary>Speed over ground in METRES PER SECOND (the sim's unit — the instrument converts
        /// to whatever it prints). Zero for anything at rest.</summary>
        public readonly float SpeedMps;

        public RadarContact(Vector2 worldPos, RadarEchoKind kind, float size,
                            float courseDegrees, float speedMps)
        {
            WorldPos = worldPos;
            Kind = kind;
            Size = size;
            CourseDegrees = courseDegrees;
            SpeedMps = speedMps;
        }

        /// <summary>A contact that is not going anywhere — a buoy, a mark, a rock.</summary>
        public static RadarContact Still(Vector2 worldPos, RadarEchoKind kind, float size)
            => new RadarContact(worldPos, kind, size, float.NaN, 0f);

        /// <summary>True when this contact has a defined course to draw a trail along.</summary>
        public bool IsMakingWay => !float.IsNaN(CourseDegrees) && SpeedMps > 0f;
    }
}
