using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The library of PRECONFIGURED light-source PRESETS (ADR 0016): the fixed, tunable look of each kind of
    /// "object that comes with a light" — a house WINDOW GLOW, a street LAMPPOST, a work WORKLIGHT. A preset is
    /// pure DATA (shape + colour + size + softness + flicker) with NO scene / GPU / RNG — so it is unit-tested
    /// headless (the determinism guard, CLAUDE.md rule 5) and there are NO magic numbers scattered through the
    /// components / the editor menu (rule 6): the ONE place a preset's feel lives.
    ///
    /// <para><b>Why a preset library, not fields-on-each-component.</b> The owner's ratified lighting principle
    /// (2026-07-05): <em>lighting is AUTOMATIC (the day/night multiply darkens everything for free); the
    /// EXCEPTION is a light SOURCE, and some objects come PRECONFIGURED with one</em> (houses glow, lamp posts
    /// glow, vehicles have headlights…). Each such object type wants the SAME feel every time it is placed — a
    /// window glow should look like a window glow on every cottage — so its look is a named preset here, applied
    /// by <see cref="PreconfiguredLight"/> when the prefab wakes. A future object (a vehicle headlight, a
    /// lantern) adds a case here and reuses the whole night-gated additive-light machinery unchanged.</para>
    ///
    /// <para><b>All radial by design.</b> Every preconfigured PLACED light here is a round <c>Radial</c> glow (a
    /// pool of light spilling from a window / under a post / around a worklamp). The directional CONE beam is the
    /// boat spotlight's job (a searchlight you aim), which stays its own bespoke <see cref="BoatSpotlight"/>; a
    /// vehicle HEADLIGHT, when vehicles arrive (M2), would be the first placed CONE preset — the same pattern,
    /// just <see cref="SceneLight.LightShape.Cone"/> with a headlight colour/throw.</para>
    ///
    /// <para><b>⭐⭐ A PRESET NOW CARRIES TWO SIZES, AND THEY ARE DIFFERENT THINGS.</b> Until 2026-09-04 it
    /// carried one — <see cref="Config.Range"/> — and that number was doing two incompatible jobs at once.
    /// <list type="bullet">
    ///   <item><b>The BLOOM</b> (<see cref="Config.Range"/>) is how far the additive quad reaches: the
    ///   SOURCE'S OWN GLOW, the halo you see around a lit fitting. ADR 0016 says so in as many words, and
    ///   it is all the quad can ever be — it is ADDED to the frame, so it cannot darken, cannot be
    ///   occluded, and cannot tell a plank from the sea.</item>
    ///   <item><b>The REACH</b> (<see cref="ReachMetres"/>) is how far the lamp LIGHTS: the pool on the
    ///   ground, the thing a lamp is actually for. Nothing draws it yet. It is what the builders site
    ///   their lamps by, and it is what the lit-decor path will illuminate with when a lamp lights the
    ///   ground the way the sun lights a tree.</item>
    /// </list></para>
    ///
    /// <para><b>Why they had to be split (the owner's ruling, 2026-09-04).</b> Playing the St Peters arrival
    /// he said of the pier lanterns: <i>"dock lights are just a round glow, it should glow from within the
    /// lamp reasilitcally."</i> (his spelling) He is describing a 3.6 m bloom — a bloom drawn at the REACH. A lamp does
    /// not have a 3.6 m glowing part; it has a 0.4 m lantern, and a 3.6 m patch of ground that the lantern
    /// makes brighter. Drawing the second as though it were the first is what produces a flat cream disc
    /// with the planks, the bollards and the post itself hidden inside it. So the bloom comes down to the
    /// FITTING — the same ruling <see cref="BoatLampPresets"/> already applied to every lamp the fleet
    /// carries — and the reach keeps the plate-tuned number it always had, under its own name, for the
    /// siting that derives from it and for the illumination PR that will finally draw it.</para>
    ///
    /// <para><b>⚠ Until then a lamp post has no pool at all, and that is deliberate.</b> The post glows and
    /// the planks under it stay dark. It is the honest half-picture: the owner has already ruled the disc
    /// worse than the dark.</para>
    /// </summary>
    public static class LightPresets
    {
        /// <summary>
        /// The kinds of preconfigured light a placed object can carry. Each maps to one <see cref="Config"/>
        /// below. (The boat SPOTLIGHT is deliberately NOT here — it is the aimed directional beam driven by the
        /// bespoke <see cref="BoatSpotlight"/>; these are the STATIC placed glows.)
        /// </summary>
        public enum Kind
        {
            /// <summary>Warm interior light spilling out of a lit house window at night (the cottage).</summary>
            WindowGlow,
            /// <summary>A warm lamp pool on the ground beneath a street/quay lamp post.</summary>
            Lightpost,
            /// <summary>A brighter, cooler, steady work lamp (a wharf worklight / a floodlit workspace).</summary>
            Worklight,
            /// <summary>A TALL mast's wide, cool pool over a working yard — a yard light or a flood mast.</summary>
            Floodlight,
        }

        /// <summary>
        /// The pure, serialization-free config of ONE preset: the tunables <see cref="PreconfiguredLight"/>
        /// stamps onto a <see cref="SceneLight"/>. A plain value struct so the tests can pin every field with no
        /// scene. Distances are world metres; colours are linear-ish RGB; softness/flicker are 0..1.
        /// </summary>
        public readonly struct Config
        {
            /// <summary>Cone beam vs round halo. Every placed preset here is <see cref="SceneLight.LightShape.Radial"/>.</summary>
            public readonly SceneLight.LightShape Shape;
            /// <summary>The glow colour (a warm amber reads as a cosy interior / sodium lamp).</summary>
            public readonly Color Color;
            /// <summary>Master intensity (pre night-gate / pre flicker).</summary>
            public readonly float Intensity;
            /// <summary>
            /// <b>The BLOOM radius</b>, world metres — how far the additive quad's halo reaches from the
            /// lamp, i.e. how big the SOURCE looks. Since 2026-09-04 this is the size of the lit FITTING
            /// and not of the pool it lights; the pool is <see cref="ReachMetres"/>. See the class note.
            /// </summary>
            public readonly float Range;
            /// <summary>Radial edge softness (0 hard disc .. 1 soft halo). Placed glows are soft.</summary>
            public readonly float EdgeSoftness;
            /// <summary>Deterministic flicker amount (0 steady .. 1 strong). A tiny amount reads as a living flame.</summary>
            public readonly float FlickerAmount;
            /// <summary>Local offset (m) of the glow ORIGIN from the object's transform — e.g. the lamp head atop a post.</summary>
            public readonly Vector2 OriginOffset;

            public Config(SceneLight.LightShape shape, Color color, float intensity, float range,
                          float edgeSoftness, float flickerAmount, Vector2 originOffset)
            {
                Shape = shape;
                Color = color;
                Intensity = intensity;
                Range = range;
                EdgeSoftness = edgeSoftness;
                FlickerAmount = flickerAmount;
                OriginOffset = originOffset;
            }
        }

        /// <summary>
        /// The config for a preset <see cref="Kind"/> — the single source of truth for how each preconfigured
        /// light looks. Pure: same input ⇒ same config, always (the tests pin these values). All three are
        /// RADIAL warm-to-cool pools, night-gated by the SAME machinery as every additive light (in-shader off
        /// <c>_DayNightTint</c>, ADR 0016) — a preset changes only the shape/colour/size/flicker, never the
        /// gate.
        /// </summary>
        public static Config For(Kind kind)
        {
            switch (kind)
            {
                // WINDOW GLOW — a soft warm pool of interior light spilling out of a lit window. Small + very
                // soft (it's a spill, not a spotlight), gently flickering (hearth/lamp within). Nudged a touch
                // DOWN from the sprite centre so the pool reads as pooling at the sill/ground below the window,
                // complementing CottageDayNight's lit-window sprite swap rather than haloing the whole roof.
                case Kind.WindowGlow:
                    return new Config(
                        SceneLight.LightShape.Radial,
                        new Color(1f, 0.82f, 0.48f, 1f),   // warm amber interior
                        intensity: 0.95f,
                        range: 3.4f,
                        edgeSoftness: 0.88f,
                        flickerAmount: 0.05f,              // a living hearth/lamp within
                        originOffset: new Vector2(0f, -0.35f));

                // LAMPPOST — the warm glow of a LIT LANTERN on a post. Offset so the glow sits at the lamp
                // HEAD rather than at the post's feet, steadier than a hearth (an electric/gas street lamp
                // barely flickers), and warm sodium in colour.
                //
                // ⭐⭐ THE BLOOM IS THE LANTERN, NOT THE POOL — the owner's ruling of 2026-09-04, and the
                // reason the two numbers below no longer describe the same thing. It shipped at 3.6 m, and
                // 3.6 m is the POOL: the patch of ground a street lamp lights. Drawn as an additive quad
                // that is a 7.2 m disc of cream laid over the frame, and the owner, looking at the pier
                // through it, said the lamps were "just a round glow" and should "glow from within the
                // lamp". He is right, and no radius fixes it — a smaller disc is a smaller disc. What a
                // lit lamp looks like is a BRIGHT FITTING with a short halo round it.
                //
                // So the bloom is the size of the lantern: 0.40 m, the width of `streetLamp`'s own lens
                // (utilityIsoRig.js:361, prismT(-0.8, 0, r=0.2, ..., 'glow')), which is this preset's
                // archetype. Each placed piece then overrides it with ITS OWN measured fitting via
                // <see cref="ApplyFitting"/> — a wharf lantern is a smaller lamp than a road lamp and
                // should look like one — so this value is the floor for a lamp post nobody measured.
                //
                // ⚠ THE POOL IS NOT DELETED, IT IS RENAMED. <see cref="ReachMetres"/> still says 3.6 m,
                // still carries the plate tuning of 2026-09-04, and is still what the builders site their
                // lamps by — so not one lamp post moves on this change. What moved is what gets DRAWN.
                //
                // ⚠ Intensity rises 1.0 -> 1.3 because the radius gave something up and something has to
                // carry it: a small hot point reads as a lamp far better than a broad haze, which is the
                // trade every lamp in <see cref="BoatLampPresets"/> made under the same ruling on
                // 2026-09-03. It stays above a WINDOW SPILL (WindowGlow 0.95) — an ordering this library
                // has asserted since it was written, and an obviously right one: you can see a street lamp
                // from further away than you can see somebody's window.
                case Kind.Lightpost:
                    return new Config(
                        SceneLight.LightShape.Radial,
                        new Color(1f, 0.88f, 0.62f, 1f),   // warm sodium-ish lamp
                        intensity: 1.3f,                   // the core carries what the radius gave up
                        range: 0.40f,                      // the BLOOM: streetLamp's own lantern lens
                        edgeSoftness: 0.88f,               // softer: the hard edge is what read as a disc
                        flickerAmount: 0.02f,              // a barely-there electric hum
                        originOffset: new Vector2(0f, -0.2f));

                // WORKLIGHT — a brighter, COOLER, steady work lamp on a wall. Near-white, rock-steady (no
                // flicker — it's electric work light, not a flame), centred on the object.
                //
                // ⚠ THE ONE FITTING HERE WITH NO ART TO MEASURE, and it says so rather than pretending.
                // This preset is placed NOWHERE (LampPosts' four kit pieces take Lightpost or Floodlight),
                // so there is no rig part to read a lens off. 0.50 m is reasoned, not measured: a bulkhead
                // work lamp is a bigger fitting than a street lantern (0.40 m) and a smaller one than a
                // cobra head (0.58 m), and it sits between them. The moment something places a Worklight,
                // that thing should hand its own fitting to <see cref="ApplyFitting"/> and this number
                // stops mattering.
                //
                // Its POOL is unchanged at 5.2 m — see <see cref="ReachMetres"/>.
                case Kind.Worklight:
                    return new Config(
                        SceneLight.LightShape.Radial,
                        new Color(1f, 0.97f, 0.9f, 1f),    // near-white cool work light
                        intensity: 1.7f,
                        range: 0.50f,                      // the BLOOM: reasoned, not measured — see the note
                        edgeSoftness: 0.7f,
                        flickerAmount: 0f,                 // steady electric work light
                        originOffset: Vector2.zero);

                // FLOODLIGHT — what a 7 m pole is FOR. The two tall utility pieces (`yardLight` 7.26 m,
                // `floodMast` 7.8 m) exist to flood an open working area — a laydown yard, a forecourt —
                // and the Worklight above is sized for a lamp on a wall: a 5.2 m pool under a pole taller
                // than its own pool is wide reads as a torch on a mast. So the two tall pieces get their
                // own preset rather than a per-placement multiplier, which is the magic number rule 6
                // forbids. Cooler and steadier than a lamp post: this is electric light over a place where
                // work gets done in the dark.
                //
                // The BLOOM is the `yardLight` cobra head's own lens — 0.58 m, the glow slab spanning
                // x -1.82..-1.24 at utilityIsoRig.js:339 — this preset's archetypal single head. The
                // `floodMast` is a THREE-lamp array 1.49 m across and overrides upward with its own
                // measured fitting through <see cref="ApplyFitting"/>, which is why the two tall pieces can
                // share a preset and still not glow the same size.
                //
                // Its POOL is unchanged at 7 m — see <see cref="ReachMetres"/>, where the ordering that
                // matters (a flood reaches comfortably further than a lamp post's 3.6 m) still lives.
                case Kind.Floodlight:
                    return new Config(
                        SceneLight.LightShape.Radial,
                        new Color(0.96f, 0.97f, 1f, 1f),   // cool mercury/LED flood
                        intensity: 1.45f,
                        range: 0.58f,                      // the BLOOM: yardLight's own cobra-head lens
                        edgeSoftness: 0.72f,               // harder-edged than a lamp pool: it is higher up
                        flickerAmount: 0f,                 // steady electric flood
                        originOffset: Vector2.zero);

                default:
                    goto case Kind.WindowGlow;
            }
        }

        // -------------------------------------------------------------------------------------------
        //  the REACH — the pool the lamp lights, which is not the bloom it wears
        // -------------------------------------------------------------------------------------------

        /// <summary>
        /// <b>How far this kind of lamp LIGHTS, world metres</b> — the pool on the ground, as distinct from
        /// the <see cref="Config.Range"/> bloom the fitting itself wears. See the class note for why the two
        /// had to become separate numbers.
        ///
        /// <para><b>⭐ These are exactly the numbers that shipped as <c>Range</c>, to the decimal.</b> They
        /// carry the 2026-09-04 plate tuning of the land lamp posts and nothing about them was re-derived
        /// here: the reach is not a new decision, it is the old number given the name it always deserved.
        /// That is what lets the bloom shrink without a single lamp post moving — the builders site their
        /// lamps by the reach (<c>StPetersWharf.LampRowY</c>), and the reach did not change.</para>
        ///
        /// <para><b>⚠ NOTHING DRAWS THIS YET, and that is the honest state of the feature.</b> ADR 0016's
        /// additive quad is the source's own bloom; it adds to the frame instead of modulating what the
        /// ground returns, so it cannot draw a pool that a bollard's shadow could fall across. Making a lamp
        /// light the ground the way the sun lights a tree — the lit-decor path, <c>SpriteLitDecor.hlsl</c> —
        /// is the illumination PR, and THIS is the number it will illuminate with. Until it lands, a lamp
        /// post glows and the planks under it stay dark, which the owner has already ruled the better of
        /// the two honest pictures.</para>
        ///
        /// <para>Pure, and pinned value-for-value by the preset tests against the numbers that shipped —
        /// a reach that has drifted from its plates is a reach nobody measured.</para>
        /// </summary>
        public static float ReachMetres(Kind kind)
        {
            switch (kind)
            {
                // A window spill: small, because it is light that has already been through glass and a room.
                case Kind.WindowGlow: return 3.4f;
                // A street/quay lamp: retuned 4.6 -> 3.6 m off the 02:00 plates of the St Peters pier
                // (docs/art/spikes/land-lamp-posts/). Area goes as r², so that alone was a 39 % smaller pool.
                case Kind.Lightpost:  return 3.6f;
                // A lamp on a wall, placed nowhere — untouched since the library was written.
                case Kind.Worklight:  return 5.2f;
                // A tall pole over open working ground: 7 m, not the 9.5 m it shipped at for one commit.
                case Kind.Floodlight: return 7f;
                default:              goto case Kind.WindowGlow;
            }
        }

        // -------------------------------------------------------------------------------------------
        //  the BLOOM — sized to the lit fitting, whichever piece is carrying it
        // -------------------------------------------------------------------------------------------

        /// <summary>The smallest bloom a fitting gets, metres. A pilot lamp in a fitting narrower than a
        /// hand still has to be visible at the game's framing; below this it stops being a light and
        /// becomes a stray bright pixel.</summary>
        public const float MinBloomRadiusMetres = 0.10f;

        /// <summary>The largest, metres — a backstop rather than a working number. The widest lit fitting
        /// in the kit is `floodMast`'s three-lamp array at 1.49 m, so nothing placed today comes near it;
        /// it exists so that a caller handing in a whole BUILDING as a "fitting" gets a lamp rather than
        /// the disc this ruling retired.</summary>
        public const float MaxBloomRadiusMetres = 1.60f;

        /// <summary>
        /// <b>The bloom radius a lit fitting this wide wears</b>, metres — the one place the rule lives, so
        /// a test can pin it without building a light and a report can print it.
        ///
        /// <para><b>The radius is the fitting's own WIDTH, so the glow's DIAMETER is twice the fitting.</b>
        /// A lit lantern is not a disc the size of its glass and nothing more — glass that bright bleeds a
        /// little into the dark around it. Half a fitting-width of halo all round is what that looks like,
        /// and it is the same ratio the fleet's lamps landed on under the same ruling a day earlier
        /// (<see cref="BoatLampPresets"/>: a ~0.15 m sidelight box blooms at 0.28 m, a ~0.25 m masthead at
        /// 0.50 m).</para>
        /// </summary>
        public static float BloomForFitting(float fittingWidthMetres) =>
            Mathf.Clamp(fittingWidthMetres, MinBloomRadiusMetres, MaxBloomRadiusMetres);

        /// <summary>
        /// <b>Stamp a preset, then size its bloom to THIS piece's own lit fitting</b> — the twin of
        /// <see cref="BoatLampPresets.ApplyWallSpill"/>, and for the same reason: the preset owns the LOOK
        /// (colour, softness, flicker, how bright), the art owns the SIZE. A wharf lantern and a road lamp
        /// are both <see cref="Kind.Lightpost"/> and should not glow the same size, because they are not
        /// the same lamp.
        ///
        /// <para>Everything except <see cref="Config.Range"/> is the preset's verbatim. Null-safe; a
        /// non-positive width falls back to the preset's own bloom, so a caller that cannot measure its
        /// fitting gets the archetype rather than nothing.</para>
        /// </summary>
        public static void ApplyFitting(SceneLight light, Kind kind, float fittingWidthMetres)
        {
            if (light == null) return;
            Apply(light, kind);
            if (fittingWidthMetres > 0f) light.Range = BloomForFitting(fittingWidthMetres);
        }

        /// <summary>
        /// Stamp a preset <see cref="Config"/> onto a <see cref="SceneLight"/> — the ONE place the preset→light
        /// mapping lives, shared by the runtime <see cref="PreconfiguredLight"/> component and the editor
        /// "Add Light" menu, so both configure a placed glow identically. Null-safe (a null light is a no-op).
        /// Sets only the shape/colour/size/softness/flicker/origin; the night-gate is the shader's job (every
        /// light gates off the same published <c>_DayNightTint</c>, ADR 0016), so a preset never touches it.
        /// </summary>
        public static void Apply(SceneLight light, Kind kind) => Stamp(light, For(kind));

        /// <summary>
        /// Write ONE <see cref="Config"/> onto a <see cref="SceneLight"/> — the single place a preset
        /// becomes a light, whichever library the preset came from. <see cref="Apply"/> uses it for the
        /// placed decor glows and <c>BoatLampPresets</c> for the fleet's lamps, so a field added to
        /// <see cref="Config"/> can never reach one of them and not the other. Null-safe.
        /// </summary>
        public static void Stamp(SceneLight light, in Config c)
        {
            if (light == null) return;
            light.Shape = c.Shape;
            light.Color = c.Color;
            light.Intensity = c.Intensity;
            light.Range = c.Range;
            light.EdgeSoftness = c.EdgeSoftness;
            light.FlickerAmount = c.FlickerAmount;
            light.OriginOffset = c.OriginOffset;
        }
    }
}
