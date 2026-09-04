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
            /// <summary>How far the glow reaches (world metres) — a window pool is small, a lamp pool larger.</summary>
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

                // LAMPPOST — a warm lamp pool cast on the ground beneath a post. Bigger + a touch brighter than a
                // window spill, steadier (an electric/gas street lamp barely flickers). Offset so the glow sits
                // at the ground under the lamp HEAD (the post decor mounts the head ~2.2 m up; the pool falls
                // just below it).
                //
                // ⭐ RETUNED 2026-09-04 from 1.15 / 4.6 m / 0.78, off 02:00 plates of the St Peters pier at
                // the game's own on-foot framing (9 m of world height). At 4.6 m the pool is HALF THE
                // SCREEN and, added on top of ADR 0013's night multiply, saturates to a flat cream disc
                // that hides the planks it is meant to light — the deck, the bollards and the post itself
                // all vanish inside it. A sweep at 4.6 / 3.5 / 2.5 m put the read squarely at the short
                // end. These are the FIRST measured values this preset has ever carried; the shipped
                // numbers were a stub, placed nowhere in the game.
                //
                // ⚠ The physical answer and the rendered one disagree, and it is worth saying why. A lamp
                // lights a circle of roughly twice its head height, so a 4.48 m streetLamp really should
                // pool ~4.5 m. It cannot here, because ADR 0016's additive quad is the SOURCE'S OWN BLOOM
                // and not illumination — it adds light to the frame rather than modulating what the ground
                // returns. Making a lamp light the ground the way the sun lights a tree (the lit-decor
                // path, #715) is the real fix and is its own PR; until then the pool is deliberately
                // smaller than physics so that it reads.
                //
                // ⚠ The RANGE did the work, not the intensity. Area goes as r², so 4.6 -> 3.6 m is a 39 %
                // smaller pool on its own. Intensity lands at 1.0 rather than the 0.9 the sweep was shot at
                // because a lamp pool must stay brighter than a WINDOW SPILL (WindowGlow 0.95) -- an
                // ordering the preset library has asserted since it was written, and one that is obviously
                // right: you can see a street lamp from further away than you can see somebody's window.
                case Kind.Lightpost:
                    return new Config(
                        SceneLight.LightShape.Radial,
                        new Color(1f, 0.88f, 0.62f, 1f),   // warm sodium-ish lamp
                        intensity: 1f,                     // see the note: just ABOVE WindowGlow's 0.95
                        range: 3.6f,
                        edgeSoftness: 0.88f,               // softer: the hard edge is what read as a disc
                        flickerAmount: 0.02f,              // a barely-there electric hum
                        originOffset: new Vector2(0f, -0.2f));

                // WORKLIGHT — a brighter, COOLER, steady work lamp (a wharf floodlight). Bigger reach, near-white,
                // rock-steady (no flicker — it's electric work light, not a flame). Centred on the object.
                case Kind.Worklight:
                    return new Config(
                        SceneLight.LightShape.Radial,
                        new Color(1f, 0.97f, 0.9f, 1f),    // near-white cool work light
                        intensity: 1.35f,
                        range: 5.2f,
                        edgeSoftness: 0.7f,
                        flickerAmount: 0f,                 // steady electric work light
                        originOffset: Vector2.zero);

                // FLOODLIGHT — what a 7 m pole is FOR. The two tall utility pieces (`yardLight` 7.26 m,
                // `floodMast` 7.8 m) exist to flood an open working area — a laydown yard, a forecourt —
                // and the Worklight above is sized for a lamp on a wall: 5.2 m of reach under a pole
                // taller than its own pool is wide reads as a torch on a mast. A lamp lights a circle of
                // roughly twice its head height, which is the rule the other three already satisfy
                // (lanternPost 2.46 m and streetLamp 4.48 m both sit under Lightpost's 4.6 m), so the two
                // tall pieces get their own preset at 9.5 m rather than a per-placement multiplier — which
                // is the magic number rule 6 forbids. Cooler and steadier than a lamp post: this is
                // electric light over a place where work gets done in the dark.
                case Kind.Floodlight:
                    return new Config(
                        SceneLight.LightShape.Radial,
                        new Color(0.96f, 0.97f, 1f, 1f),   // cool mercury/LED flood
                        intensity: 1.1f,
                        range: 7f,                         // see Lightpost's note: sized to READ, not to physics
                        edgeSoftness: 0.72f,               // harder-edged than a lamp pool: it is higher up
                        flickerAmount: 0f,                 // steady electric flood
                        originOffset: Vector2.zero);

                default:
                    goto case Kind.WindowGlow;
            }
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
