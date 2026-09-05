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
    /// <para><b>The NAVIGATION lamps are all radial, deliberately — and that is a simplification
    /// worth stating.</b> A real sidelight is a SECTOR light: red shows from dead ahead round to
    /// 112.5 degrees on the port bow and nowhere else, which is how a lookout reads your aspect.
    /// <see cref="SceneLight"/> can cut a cone and could carry that. It does not, because at this
    /// camera a sidelight is a handful of pixels: a sector and a round glow are the same picture, and
    /// the sector would cost a per-lamp orientation tracking the drawn heading for no visible return.
    /// THE COLOUR is the signal at this scale. If the camera ever comes close enough for aspect to
    /// read (the close-up tier), the cone is the change, and it is a change to this file only.</para>
    ///
    /// <para><b>The CABIN GLOW is the exception, and it is a cone because the owner ruled it should
    /// be</b> (2026-09-03): a glow is confined to its space, and an interior's reaches the outside
    /// only through the windows. So what used to be a disc over the wheelhouse is now the wash that
    /// leaves each glazed WALL — which is directional by construction, the wall behind it being what
    /// makes it so — while the windows themselves are drawn as their own rectangles by
    /// <see cref="BoatWindowGlow"/>. It pays exactly the per-lamp orientation the sidelights declined,
    /// and it pays it because at this camera a wheelhouse is not a handful of pixels: it is the
    /// biggest thing on the boat, and which way its light goes is plainly visible.</para>
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

                // THE STERN LIGHT. White, and the one seen from astern across open water — the one
                // the player looks AT most, since the boat ahead is the boat you are following in.
                //
                // ⚠️ IT IS A LAMP, NOT A POOL (owner's ruling, 2026-09-03: "the glows should be
                // constrained to their space"). It shipped at 1.0 m — a two-metre pool of white
                // hanging off a transom, wider than the punt is long — and at the zoom the owner
                // plays at that reads as a blob rather than as a fitting. The reach is now the size
                // of the lamp itself, and the INTENSITY carries what the radius gave up: a small hot
                // point reads as a stern light far better than a broad haze does, and a haze is not
                // what a stern light looks like anyway. That is the trade the SIDELIGHTS already made
                // (see above), for the same reason, now applied to the three lamps that had not.
                case HullLampKind.SternLight:
                    return new LightPresets.Config(
                        SceneLight.LightShape.Radial, LampWhite,
                        intensity: 1.35f, range: 0.40f,
                        edgeSoftness: 0.45f, flickerAmount: 0f, originOffset: Vector2.zero);

                // THE MASTHEAD. The brightest of the four — mounted highest, and the one that says
                // "under power" — but still a lamp, not a beam: the SPOTLIGHT is what lights the
                // water, and a masthead competing with it would flatten the very dark the beam exists
                // to cut. Shrunk to its fitting alongside the stern light and under the same ruling;
                // it stays the BIGGEST and BRIGHTEST of the round lamps, which is the only thing
                // about its size that ever had to be true.
                case HullLampKind.Masthead:
                    return new LightPresets.Config(
                        SceneLight.LightShape.Radial, LampWhite,
                        intensity: 1.6f, range: 0.50f,
                        edgeSoftness: 0.45f, flickerAmount: 0f, originOffset: Vector2.zero);

                // THE CABIN GLOW — and this is the one the 2026-09-03 ruling is ABOUT.
                //
                // ⭐ IT IS NO LONGER A DISC, AND THE TWO PREVIOUS PASSES EXPLAIN WHY IT COULD NOT BE.
                // Pass one made it 2.6 m and the pre-dawn screenshot showed a cape that looked on
                // fire: the hull, the mast and both sidelights swallowed by one amber blob wider than
                // her beam. Pass two brought it to 1.5 m — bounded by the wheelhouse, which was the
                // right instinct — and the owner, looking at the fleet at that zoom, still called it
                // "large and blobby" and ruled: <i>"The glows should be constrained to their space,
                // if its interior it should be confined to the cabin with the glow only coming
                // through the windows."</i>
                //
                // A round pool centred on a room is not a lit room seen from outside. It is a lamp
                // parked on a roof, and no radius makes it anything else — shrinking it only makes a
                // smaller blob. What a lit room actually looks like from outside is its WINDOWS: a
                // few bright rectangles in a dark box, with a wash of light on the deck under each.
                // So the disc is retired and the glow is drawn as the glass itself — see
                // <see cref="BoatWindowGlow"/>, which reads the panes the rigs already publish — and
                // what survives here is the SPILL: the wash that leaves each glazed wall.
                //
                // The config below is the spill's LOOK, not its shape: the same amber the disc wore
                // and the same whisper of flicker for the man moving about inside, over a CONE whose
                // throw and aim are per-wall and therefore arguments rather than constants (see
                // <see cref="ApplyWallSpill"/>). Range here is the floor a wall with almost no
                // glazing still throws; a real wall overrides it with its own glazed width.
                //
                // The disc is not deleted — <see cref="GameServices.BoatLegacyCabinGlow"/> restores
                // it exactly, as the honest arm of the owner's A/B (see <see cref="Legacy"/>).
                case HullLampKind.CabinGlow:
                    return new LightPresets.Config(
                        SceneLight.LightShape.Cone, CabinAmber,
                        intensity: 0.5f, range: MinWallSpillMetres,
                        edgeSoftness: 0.9f, flickerAmount: 0.03f, originOffset: Vector2.zero);

                // THE RANGE LIGHT. The second masthead a big ship carries, and it is the SAME LAMP:
                // the rule of the road distinguishes the two by where they are hung and how high, not
                // by what they look like. So the look is the masthead's verbatim — one lamp, two
                // stations — and the kinds are separate only so that one hull's two mastheads cannot
                // collapse into a single duplicated row.
                case HullLampKind.RangeLight:
                    goto case HullLampKind.Masthead;

                // THE ANCHOR LIGHT. One all-round white, and the ONLY navigation light a hull lying
                // still is allowed to show. Deliberately DIMMER and SMALLER than the masthead it hangs
                // in place of: a masthead says "under power, coming through" and is the brightest lamp
                // on the boat, while this one says only "something is here" — and a whole wharf of them
                // at two in the morning, each as bright as a steaming light, would read as a fleet
                // getting under way rather than a fleet asleep.
                //
                // Reach sits between a sidelight and the stern light, and it stays there after the
                // 2026-09-03 shrink — the ORDER of these lamps is the part that carries meaning
                // (masthead brightest and biggest, anchor light dimmest and smallest of the whites),
                // and all three moved together so that order is exactly preserved. A whole wharf of
                // these at two in the morning must read as a fleet asleep, which is a job for a
                // small steady point and never was a job for a pool.
                case HullLampKind.AnchorLight:
                    return new LightPresets.Config(
                        SceneLight.LightShape.Radial, LampWhite,
                        intensity: 1.0f, range: 0.34f,
                        edgeSoftness: 0.45f, flickerAmount: 0f, originOffset: Vector2.zero);

                default:
                    goto case HullLampKind.SternLight;
            }
        }

        // -------------------------------------------------------------------------------------------
        //  the WALL SPILL — what leaves a glazed wall (owner's ruling, 2026-09-03)
        // -------------------------------------------------------------------------------------------

        /// <summary>
        /// The shortest throw a glazed wall gets, metres — the floor for a wall with one small light
        /// in it, so a single porthole still washes a little of the plating under it rather than
        /// nothing at all.
        /// </summary>
        public const float MinWallSpillMetres = 0.50f;

        /// <summary>The longest, metres — a backstop rather than a working number, since scaling off a
        /// WINDOW rather than off a wall already keeps every hull in the fleet well inside it. Two
        /// metres is a lit room throwing onto her own deck and no further.</summary>
        public const float MaxWallSpillMetres = 1.60f;

        /// <summary>
        /// <b>How far a wall washes, as a multiple of ONE of its windows.</b>
        ///
        /// <para>⭐ THE MULTIPLE IS OF A WINDOW, NOT OF THE WALL, and that is the ruling in one number.
        /// Scaled off the wall's glazed SPAN instead, the tanker's five portholes strung over 6.8 m of
        /// accommodation would throw a seven-metre wash and the cape's three-pane screen a two-metre
        /// one — both of them pools on the deck, which is exactly the picture the owner refused. A
        /// wash scaled off a WINDOW cannot grow just because a wall has more windows in it: light
        /// through a 0.6 m pane reaches about a metre whether it has two neighbours or none.</para>
        ///
        /// <para>Half again the window, because a window is a diffuse source and its light does carry
        /// past its own width — but not much past it before the night takes it.</para>
        ///
        /// <para><b>⚠️ 1.4 AND NOT 2, AND THE PLATE IS WHY.</b> This shipped at 2 first, and the
        /// four-heading average looked comfortable — 0.72 of the area the disc covered — while the one
        /// heading where TWO of her walls face the viewer at once covered <b>1.23×</b>: MORE deck than
        /// the blob the ruling retired. A mean is the wrong statistic for "constrained to its space".
        /// At 1.4 (with the cone at 45° rather than 55°) the worst heading is 0.43 and the average
        /// 0.26, and the fixture now asserts the WORST rather than the mean.</para>
        /// </summary>
        public const float WallSpillWindowMultiple = 1.4f;

        /// <summary>
        /// The cone's half-angle, degrees. Wide, because light out of a window does not come out as a
        /// beam — it comes out as most of a hemisphere and only READS as directional because the wall
        /// behind it blocks the other half. Narrow enough that the four walls of a wheelhouse stay
        /// four separate washes rather than merging back into the disc this replaced.
        /// </summary>
        public const float WallSpillHalfAngleDeg = 45f;

        /// <summary>How soft the cone's edge is, as a fraction of the half-angle. Nearly all of it:
        /// a hard-edged wedge of light off a boat would read as a searchlight, and she already has
        /// one of those.</summary>
        public const float WallSpillAngularSoftness = 0.85f;

        /// <summary>
        /// <b>Stamp the WALL SPILL onto a light</b> — the cabin glow's colour and flicker over a cone
        /// aimed out of one glazed wall, with the throw that wall's own glazed width (clamped into
        /// <see cref="MinWallSpillMetres"/>..<see cref="MaxWallSpillMetres"/>).
        ///
        /// <para><b>The core boost is driven to ZERO here, deliberately.</b> Every other lamp in this
        /// file wants a hot point at its origin, because every other lamp IS a point. A spill is not:
        /// its origin is a wall, the bright thing at that wall is the glass itself (drawn by
        /// <see cref="BoatWindowGlow"/> as a real rectangle), and a round core added on top of it
        /// would put back, at the wall, precisely the blob the ruling took off the roof.</para>
        ///
        /// <para>Aim is the CALLER's: a wall's outward direction is a property of the hull's pose and
        /// changes every frame she turns, so it cannot live in a preset. This sets everything that
        /// does not change.</para>
        /// </summary>
        public static void ApplyWallSpill(SceneLight light, float windowWidthMetres,
                                          float intensityScale = 1f)
        {
            if (light == null) return;
            LightPresets.Config c = For(HullLampKind.CabinGlow);
            LightPresets.Stamp(light, c);
            light.Intensity = c.Intensity * Mathf.Max(0f, intensityScale);
            light.Shape = SceneLight.LightShape.Cone;
            light.ConeHalfAngle = WallSpillHalfAngleDeg;
            light.AngularSoftness = WallSpillAngularSoftness;
            light.CoreBoost = 0f;
            light.Range = WallSpillThrow(windowWidthMetres);
        }

        /// <summary>How far a wall with windows this wide washes, metres — the one place the rule
        /// lives, so a test can pin it without building a light and the report can print it.</summary>
        public static float WallSpillThrow(float windowWidthMetres) =>
            Mathf.Clamp(windowWidthMetres * WallSpillWindowMultiple,
                        MinWallSpillMetres, MaxWallSpillMetres);

        // -------------------------------------------------------------------------------------------
        //  the passthrough — yesterday's picture, as the honest arm of the owner's A/B
        // -------------------------------------------------------------------------------------------

        /// <summary>
        /// <b>The look every one of these lamps had before the 2026-09-03 ruling</b>, verbatim — the
        /// 1.5 m amber disc over the house and the three round lamps at their old pool radii.
        ///
        /// <para><b>Why the old numbers live on in code rather than in git.</b> An A/B whose other arm
        /// is "check out the parent commit" is an A/B nobody runs, and a plate pair shot from two
        /// working trees is a plate pair with two builds in it. This is one build, one dial
        /// (<see cref="GameServices.BoatLegacyCabinGlow"/>), and the arms differ by exactly the thing
        /// under review. It is also the way back: if the owner refuses the confined look, flipping
        /// the dial is the whole rollback.</para>
        ///
        /// <para>Pure, and pinned value-for-value by the preset tests against the numbers that
        /// actually shipped — a passthrough that has drifted is not a passthrough.</para>
        /// </summary>
        public static LightPresets.Config Legacy(HullLampKind kind)
        {
            switch (kind)
            {
                case HullLampKind.SternLight:
                    return new LightPresets.Config(
                        SceneLight.LightShape.Radial, LampWhite,
                        intensity: 1.1f, range: 1.0f,
                        edgeSoftness: 0.55f, flickerAmount: 0f, originOffset: Vector2.zero);

                case HullLampKind.Masthead:
                case HullLampKind.RangeLight:
                    return new LightPresets.Config(
                        SceneLight.LightShape.Radial, LampWhite,
                        intensity: 1.25f, range: 1.35f,
                        edgeSoftness: 0.6f, flickerAmount: 0f, originOffset: Vector2.zero);

                case HullLampKind.CabinGlow:
                    return new LightPresets.Config(
                        SceneLight.LightShape.Radial, CabinAmber,
                        intensity: 0.55f, range: 1.5f,
                        edgeSoftness: 0.92f, flickerAmount: 0.03f, originOffset: Vector2.zero);

                case HullLampKind.AnchorLight:
                    return new LightPresets.Config(
                        SceneLight.LightShape.Radial, LampWhite,
                        intensity: 0.8f, range: 0.75f,
                        edgeSoftness: 0.5f, flickerAmount: 0f, originOffset: Vector2.zero);

                // The two SIDELIGHTS did not move: they were already bounded by the gap between
                // them (see the note on PortSidelight), which is a harder constraint than the
                // ruling's and was already being met. Same object both arms.
                default:
                    return For(kind);
            }
        }

        /// <summary>Stamp YESTERDAY's look for this kind — <see cref="Apply"/>'s twin, over
        /// <see cref="Legacy"/>. The caller reads the dial; this library stays pure.</summary>
        public static void ApplyLegacy(SceneLight light, HullLampKind kind, float intensityScale = 1f)
        {
            if (light == null) return;
            LightPresets.Config c = Legacy(kind);
            LightPresets.Stamp(light, c);
            light.Intensity = c.Intensity * Mathf.Max(0f, intensityScale);
            // The disc had no cone and the shipped core boost; a light re-stamped from the spill
            // back to the disc has to have BOTH put back, or the A/B's other arm is not yesterday.
            light.ConeHalfAngle = 180f;
            light.CoreBoost = 1f;
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
