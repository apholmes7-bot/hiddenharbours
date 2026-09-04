using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The look of a lit navigation mark's lantern</b> — one fixed preset per
    /// <see cref="NavLightColour"/>, in the same shape as <see cref="BoatLampPresets"/> and stamped
    /// onto a <see cref="SceneLight"/> through the same <see cref="LightPresets.Stamp"/>. Pure data:
    /// no scene, no GPU, no RNG, so it is unit-tested headless, and there is exactly ONE place a
    /// port-hand green lives (rule 6).
    ///
    /// <para><b>Why a separate library from the fleet's lamps, when green is green.</b> They are
    /// different fittings saying different things. A sidelight is a small box on a bow whose reach is
    /// bounded by how close its opposite number sits — 0.28 m, or red and green add up to yellow and
    /// the boat stops having an aspect. A lantern on a channel mark has no opposite number within a
    /// hundred metres and the OPPOSITE job: it is meant to be picked up from as far off as possible,
    /// so it is the brighter, longer-reaching fitting of the two. Keying them separately is what lets
    /// each be right; the hues are deliberately the same numbers, because the SIGNAL is the same
    /// signal, and a test pins them equal so a retune of one is a deliberate divergence rather than
    /// an accident.</para>
    ///
    /// <para><b>Region B, and not a tunable.</b> Port hand green, starboard hand red — the Canadian
    /// Coast Guard's convention and the one the kit is baked to. Cardinals and isolated-danger marks
    /// are white. Getting this backwards puts a skipper on the rocks, so it lives in code beside the
    /// rule that says so rather than in an asset somebody can drag.</para>
    ///
    /// <para><b>Dead steady, unlike a cabin.</b> The deterministic flicker <see cref="SceneLight"/>
    /// offers is left at ZERO. A modern buoy lantern is an LED on a battery and a solar panel; it
    /// does not gutter. And the character is the whole message — a mark that wobbled between flashes
    /// would blur the very thing a skipper is counting.</para>
    /// </summary>
    public static class NavLightPresets
    {
        // ---- the colours, named once -----------------------------------------------------------
        // The same numbers BoatLampPresets uses for the rule-of-the-road pair, on purpose: an
        // additive glow over a crushed-dark frame washes out fast, so these are deliberately
        // saturated. A pastel green ADDS UP to white and stops being a signal at all.
        static readonly Color MarkGreen = new Color(0.10f, 1f, 0.34f, 1f);
        static readonly Color MarkRed   = new Color(1f, 0.10f, 0.09f, 1f);
        static readonly Color MarkWhite = new Color(1f, 0.96f, 0.88f, 1f);
        static readonly Color MarkAmber = new Color(1f, 0.82f, 0.20f, 1f);

        /// <summary>
        /// How far a lantern throws, metres.
        ///
        /// <para>Sized against the mark, not by eye: the working default is a 1.75 m can whose
        /// painted height is about 2.8 m, so a 1.6 m halo reads as a lantern lighting its own
        /// structure and a little of the water round it — bigger than the buoy's girth, smaller
        /// than the gap to her neighbour. The nearest two marks anywhere in the two harbours are a
        /// channel's port and starboard pair, and a test holds this radius below half that gap for
        /// exactly the reason the sidelights have one: where red and green overlap additively the
        /// answer is yellow, and a channel whose two sides merge into one colour marks nothing.</para>
        /// </summary>
        public const float LanternRangeMetres = 1.6f;

        /// <summary>
        /// The lantern's brightness. Brighter than a sidelight (1.4) because it is the thing you are
        /// meant to pick up first and from furthest off, and because it burns for an eighth of its
        /// period — a wink you miss is a mark you did not see.
        /// </summary>
        public const float LanternIntensity = 1.7f;

        /// <summary>
        /// The fixed look of one lantern colour. Pure: same input, same config, always — the tests
        /// pin every value here, so a change to a port-hand green is a change somebody has to mean.
        /// </summary>
        public static LightPresets.Config For(NavLightColour colour)
        {
            Color c;
            switch (colour)
            {
                case NavLightColour.Green:  c = MarkGreen; break;
                case NavLightColour.Red:    c = MarkRed;   break;
                case NavLightColour.Yellow: c = MarkAmber; break;
                default:                    c = MarkWhite; break;
            }

            return new LightPresets.Config(
                SceneLight.LightShape.Radial, c,
                intensity: LanternIntensity, range: LanternRangeMetres,
                // A touch softer than a sidelight: a lantern in a cage on top of a steel can throws a
                // haze through the fog and the spray, and a hard disc on open water reads as a decal.
                edgeSoftness: 0.55f, flickerAmount: 0f, originOffset: Vector2.zero);
        }

        /// <summary>
        /// Stamp a lantern onto a light, with the two settings a mark needs that the shared
        /// <see cref="LightPresets.Config"/> does not carry.
        ///
        /// <para><b>⭐ <c>CastsShadows</c> is OFF, and that is a measurement, not a preference.</b>
        /// The shadow system rescans every lamp against every caster on a 10 Hz tick; the fleet's own
        /// lamps already cost it 25×592 pairs a scan at the Nine Mile Creek wharf. A buoy lantern
        /// stands in open water with nothing inside its 1.6 m to cast anything, so every pair it adds
        /// is work that provably yields no shadow — and 23 marks flashing would add them and take
        /// them away twice a second. Off, deliberately, with the reason written down.</para>
        /// </summary>
        public static void Apply(SceneLight light, NavLightColour colour, float lanternHeightMetres)
        {
            if (light == null) return;
            LightPresets.Stamp(light, For(colour));
            light.CastsShadows = false;
            light.LampHeightMeters = Mathf.Max(0f, lanternHeightMetres);
        }
    }
}
