using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The look of every lamp a boat carries</b> (ADR 0016) — one fixed preset per
    /// <see cref="HullLampKind"/>, in the same shape as the placed-decor library
    /// (<see cref="LightPresets"/>) and stamped onto a <see cref="SceneLight"/> through the same
    /// <see cref="LightPresets.Stamp"/>. Pure data: no scene, no GPU, no RNG, so it is unit-tested
    /// headless, and there is exactly ONE place a sidelight's red lives (rule 6).
    ///
    /// <para><b>Why the fleet's lamps are a SEPARATE library from the placed glows.</b> A cottage
    /// window and a lamp post are decor: you drop them, they pool light on the ground, and their
    /// menu should not offer "port sidelight". A hull's lamps are the other way round — the KIND is
    /// fixed vocabulary from Core and the POSITION is per-hull data — so they are keyed by
    /// <see cref="HullLampKind"/> directly and there is no picker to pollute. Both libraries yield
    /// the same <see cref="LightPresets.Config"/>, so a light is a light however it was chosen.</para>
    ///
    /// <para><b>All RADIAL, deliberately — and that is a simplification worth stating.</b> A real
    /// sidelight is a SECTOR light: red shows from dead ahead round to 112.5 degrees on the port bow
    /// and nowhere else, which is how a lookout reads your aspect. <see cref="SceneLight"/> can cut a
    /// cone and could carry that. It does not, because at this camera a sidelight is a handful of
    /// pixels: a sector and a round glow are the same picture, and the sector would cost a per-lamp
    /// orientation tracking the drawn heading for no visible return. THE COLOUR is the signal at this
    /// scale. If the camera ever comes close enough for aspect to read (the close-up tier), the cone
    /// is the change, and it is a change to this file only.</para>
    ///
    /// <para><b>Steady, not flickering.</b> Every one of these runs off the boat's own batteries, so
    /// the deterministic flicker <see cref="SceneLight"/> offers is left at zero for the navigation
    /// lights — a wobbling sidelight reads as a fire, not a lamp. The cabin glow takes a whisper of
    /// it, because a lit room with somebody moving about in it is not perfectly still.</para>
    /// </summary>
    public static class BoatLampPresets
    {
        // ---- the colours, named once ---------------------------------------------------------------
        // Rule of the road, not taste: red to port, green to starboard, white astern and at the
        // masthead. They are deliberately saturated — this is an ADDITIVE glow over a crushed-dark
        // frame (ADR 0016), so a washed-out pink ADDS UP to white and stops being a signal at all.
        static readonly Color PortRed      = new Color(1f, 0.10f, 0.09f, 1f);
        static readonly Color StarboardGrn = new Color(0.10f, 1f, 0.34f, 1f);
        static readonly Color LampWhite    = new Color(1f, 0.96f, 0.88f, 1f);
        static readonly Color CabinAmber   = new Color(1f, 0.80f, 0.46f, 1f);

        /// <summary>
        /// The fixed look of one lamp kind. Pure: same input, same config, always — the tests pin
        /// every value here, so a change to a sidelight's red is a change somebody has to mean.
        /// </summary>
        public static LightPresets.Config For(HullLampKind kind)
        {
            switch (kind)
            {
                // THE SIDELIGHTS. Small, tight and BRIGHT: a lamp in a box on the bow, not a
                // floodlight.
                //
                // ⚠️ THE REACH IS BOUNDED BY THE BOAT, not by taste. The cape wears her pair 0.6048 m
                // apart (±0.3024 off the centreline), which is as close as two lamps ever sit — they
                // are one fitting either side of a stem. Two radial glows overlap wherever their radii
                // sum exceeds that gap, and where red and green overlap additively the answer is
                // YELLOW: the two lamps whose whole job is to be told apart merge into one colour that
                // says nothing about which way she is heading. So the radius is held BELOW HALF the
                // separation and the glows never meet at all. A first pass here used 0.85 m — bigger
                // than the whole gap — and the preset test caught it before any of it was drawn.
                //
                // The intensity carries what the size gave up: a small hot dot reads as a lamp at this
                // camera far better than a broad haze would, and a haze is not what a sidelight looks
                // like anyway.
                case HullLampKind.PortSidelight:
                    return new LightPresets.Config(
                        SceneLight.LightShape.Radial, PortRed,
                        intensity: 1.4f, range: 0.28f,
                        edgeSoftness: 0.4f, flickerAmount: 0f, originOffset: Vector2.zero);

                case HullLampKind.StarboardSidelight:
                    return new LightPresets.Config(
                        SceneLight.LightShape.Radial, StarboardGrn,
                        intensity: 1.4f, range: 0.28f,
                        edgeSoftness: 0.4f, flickerAmount: 0f, originOffset: Vector2.zero);

                // THE STERN LIGHT. White, a touch softer and wider than a sidelight — it is the one
                // seen from astern across open water, and the one the player looks AT most, since the
                // boat ahead is the boat you are following in.
                case HullLampKind.SternLight:
                    return new LightPresets.Config(
                        SceneLight.LightShape.Radial, LampWhite,
                        intensity: 1.1f, range: 1.0f,
                        edgeSoftness: 0.55f, flickerAmount: 0f, originOffset: Vector2.zero);

                // THE MASTHEAD. The brightest and furthest of the four — mounted highest, and the one
                // that says "under power" — but still a lamp, not a beam: the SPOTLIGHT is what
                // lights the water, and a masthead competing with it would flatten the very dark the
                // beam exists to cut.
                case HullLampKind.Masthead:
                    return new LightPresets.Config(
                        SceneLight.LightShape.Radial, LampWhite,
                        intensity: 1.25f, range: 1.35f,
                        edgeSoftness: 0.6f, flickerAmount: 0f, originOffset: Vector2.zero);

                // THE CABIN GLOW. The warm spill out of a lit wheelhouse: far bigger and far softer
                // than any lamp above, because it is not a source you look at but a room you can tell
                // is lit. Cousin to LightPresets.WindowGlow — same warmth, same very soft edge — with
                // a longer reach, a wheelhouse being a bigger lantern than a cottage window, and a
                // whisper of flicker for the man moving about inside it.
                //
                // ⚠️ THE REACH IS BOUNDED BY THE WHEELHOUSE, and the first pass was not. At 2.6 m the
                // glow was WIDER THAN THE BOAT (her beam is 4.8 m, the pool was 5.2 m across) and the
                // pre-dawn screenshot showed a cape that looked on fire rather than lit — the hull, the
                // mast and both sidelights swallowed by one amber blob. Her house is 2.04 m long and
                // 2.64 m across, so 1.5 m reaches the deck around it and stops: a lit room, seen from
                // outside, which is the whole brief.
                case HullLampKind.CabinGlow:
                    return new LightPresets.Config(
                        SceneLight.LightShape.Radial, CabinAmber,
                        intensity: 0.55f, range: 1.5f,
                        edgeSoftness: 0.92f, flickerAmount: 0.03f, originOffset: Vector2.zero);

                default:
                    goto case HullLampKind.SternLight;
            }
        }

        /// <summary>
        /// Stamp a lamp kind's look onto a <see cref="SceneLight"/>, with this placement's own
        /// intensity trim layered over the preset's base (the <see cref="PreconfiguredLight"/>
        /// pattern — one boat's masthead can be dimmed without editing the preset every other hull
        /// reads). Null-safe. Never touches the night gate: every additive light in the project gates
        /// on the same published _DayNightTint in-shader, and a preset that could switch that off
        /// would be a light that burns at noon.
        /// </summary>
        public static void Apply(SceneLight light, HullLampKind kind, float intensityScale = 1f)
        {
            if (light == null) return;
            LightPresets.Config c = For(kind);
            LightPresets.Stamp(light, c);
            light.Intensity = c.Intensity * Mathf.Max(0f, intensityScale);
        }
    }
}
