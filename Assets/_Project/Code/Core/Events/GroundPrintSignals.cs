using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Something left a mark on the ground</b>: one boot, or one length of a tyre pair. Any mover
    /// (the player on foot, a villager, a land vehicle) publishes it; ONE renderer listens, decides
    /// whether the ground there takes a print (grass, sand, mud, dirt; not rock, planks or water) and
    /// draws it fading over <see cref="GroundPrintSettings.LifetimeGameHours"/>. The mover never
    /// knows the renderer and the renderer never knows the mover (rule 4).
    ///
    /// <para><b>The mover reports geometry, not ground.</b> It does not say what surface it stands on:
    /// the renderer resolves that once, through <see cref="GroundZoneSources"/>, so every mover agrees
    /// with every other about where sand ends. Build these with <see cref="FootPrintTrail"/> and
    /// <see cref="TirePrintTrail"/>, which already space them by the data.</para>
    ///
    /// <para>Its own Core/Events file beside the others, because <c>GameSignals.cs</c> is
    /// lead-architect's and new signals land additively beside it.</para>
    /// </summary>
    public readonly struct GroundPrintMade
    {
        /// <summary>Centre of the mark (world metres). For a tyre pair, the midpoint between the two
        /// tyres, halfway along the length.</summary>
        public readonly Vector2 Position;

        /// <summary>Unit direction of travel (the toe of a boot, the way the tyre rolled).</summary>
        public readonly Vector2 Heading;

        public readonly GroundPrintKind Kind;

        /// <summary>Length of the mark along <see cref="Heading"/>, metres.</summary>
        public readonly float LengthMetres;

        /// <summary>Width of ONE mark across the heading, metres (a boot sole, one tyre).</summary>
        public readonly float WidthMetres;

        /// <summary>Tyre pair only: centre-to-centre distance between the two tyres (the vehicle's
        /// track gauge), metres. Zero for a boot.</summary>
        public readonly float GaugeMetres;

        /// <summary>The game time the mark was made (<see cref="IGameClock.TotalSeconds"/>). The print's
        /// age, and so its fade, is a function of this and the clock only.</summary>
        public readonly double GameTime;

        public GroundPrintMade(Vector2 position, Vector2 heading, GroundPrintKind kind,
                               float lengthMetres, float widthMetres, float gaugeMetres, double gameTime)
        {
            Position = position; Heading = heading; Kind = kind;
            LengthMetres = lengthMetres; WidthMetres = widthMetres; GaugeMetres = gaugeMetres;
            GameTime = gameTime;
        }
    }
}
