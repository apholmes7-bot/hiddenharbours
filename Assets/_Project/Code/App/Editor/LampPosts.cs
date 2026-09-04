#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Art;                 // PreconfiguredLight, SceneLight, SpriteShadow, YSortSprite
using HiddenHarbours.Core;                // ITidalTerrain
using HiddenHarbours.Tools.RigBaking;     // IsoPackSprites / IsoPackContract — the read side of the ISO pack

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>LAMPS ON THE LAND</b> — the one place a lamp post becomes a GameObject.
    ///
    /// <para>The owner, 2026-09-03: <i>"a latern and a spotlight, and yes i want lights on land"</i>. Before
    /// this, the whole game had exactly ONE placed light — Aunt Ginny's window glow — and the only thing that
    /// lit anything outdoors was the boat's 9 m searchlight, which never reaches the shore. The wharves, the
    /// yard and the forecourt were pitch dark at 02:00.</para>
    ///
    /// <para><b>Nothing here is new machinery.</b> <see cref="PreconfiguredLight"/> + the
    /// <see cref="LightPresets"/> library are the owner's ratified lighting principle (2026-07-05) already
    /// built: <i>lighting is AUTOMATIC; the exception is a light SOURCE, and some objects come PRECONFIGURED
    /// with one</i>. The post carries its own night-gated glow with zero wiring. The ART is already baked too
    /// — <c>utilityIso</c> ships <c>streetLamp</c> / <c>yardLight</c> / <c>floodMast</c> and
    /// <c>wharfDecor</c> ships <c>lanternPost</c>, eight facings each. This file is the missing third thing:
    /// somewhere for a builder to say <i>a lamp post stands here</i>.</para>
    ///
    /// <para><b>Why a builder and not the scene.</b> A hand-placed light is undone by the next Build — the
    /// regions are rebuilt from these scripts, so a lamp that is not in a builder is a lamp that exists until
    /// somebody presses the button. Positions are DATA beside the bollards, in the region files that own the
    /// geometry they are derived from; this file owns only what a lamp post IS.</para>
    ///
    /// <para><b>Three things a lamp post gets that a decor prop does not:</b>
    /// <list type="number">
    ///   <item>a <see cref="PreconfiguredLight"/> — the night-gated pool, stamped from the preset;</item>
    ///   <item>a <see cref="SceneLight.LampHeightMeters"/> taken from the KIT'S OWN published drawn height
    ///         (<see cref="HeadHeightMetres"/>) rather than left at the 2.5 m default — a lamp's height is
    ///         what sets its cast shadows' length, so a 7.8 m flood mast left at the default throws a
    ///         7.8 m mast's light with a 2.5 m mast's shadows;</item>
    ///   <item>a <see cref="SpriteShadow"/> — a post is a tall thing standing in the sun, and it casts by
    ///         day like every other stander (the wharf's bollards and pileheads already do).</item>
    /// </list></para>
    ///
    /// <para><b>⭐ A lamp post is a caster standing in its OWN light</b>, which is new: every other caster in
    /// the game is somewhere else from every lamp. <see cref="SceneLight"/> registers with
    /// <see cref="LampShadowSystem"/> on enable and <see cref="SpriteShadow"/> registers as a caster, so
    /// without the carrier rule below a post pairs with its own lamp at ~0.2 m — the SMALLEST lamp-to-feet
    /// distance in the scene, so it sorts to the front of the nearest-N pool and every post spends one of the
    /// 24 slots throwing its own foot-blob instead of lighting the bollards. See
    /// <see cref="LampShadowSystem"/>'s carrier rule, which this PR is what forced.</para>
    ///
    /// <para><b>Off the water bridge, by construction.</b> <see cref="WaterLightBridge"/> takes the four
    /// nearest <see cref="IWaterLightEmitter"/>s, and only <see cref="BoatSpotlight"/> implements it. A lamp
    /// post is a <see cref="SceneLight"/> and nothing more, so a wharf's posts can never evict the
    /// searchlight from the water's four slots — there is no opt-out to remember because there is no opt-in.
    /// </para>
    /// </summary>
    public static class LampPosts
    {
        /// <summary>The ISO pack family the tall utility lamps come from.</summary>
        public const string UtilityFamily = "utilityIso";

        /// <summary>The ISO pack family the wharf's own small lantern post comes from.</summary>
        public const string DecorFamily = "wharfDecor";

        // --- the kit keys this file knows how to light -------------------------------------------------
        // Named constants rather than loose strings so a typo is a compile error and the preset table below
        // and the region tables cannot drift apart on a spelling.

        /// <summary>A short quay/garden lantern on a post — <c>wharfDecor</c>, 2.46 m.</summary>
        public const string LanternPost = "lanternPost";

        /// <summary>A road-side lamp on a swan neck — <c>utilityIso</c>, 4.48 m.</summary>
        public const string StreetLamp = "streetLamp";

        /// <summary>A yard light on a tall pole — <c>utilityIso</c>, 7.26 m.</summary>
        public const string YardLight = "yardLight";

        /// <summary>A flood mast over a working yard — <c>utilityIso</c>, 7.8 m.</summary>
        public const string FloodMast = "floodMast";

        /// <summary>
        /// Which <see cref="LightPresets.Kind"/> a kit key carries. The mapping lives HERE and only here, so
        /// every <c>streetLamp</c> in the game reads as the same lamp — the whole point of a preset library.
        ///
        /// <para>The split is by HEAD HEIGHT, because that is what a lamp's job follows: a lamp lights a
        /// circle of roughly twice its height. The two low POSTS (<c>lanternPost</c> 2.46 m, <c>streetLamp</c>
        /// 4.48 m) are warm domestic <see cref="LightPresets.Kind.Lightpost"/> pools at 4.6 m; the two TALL
        /// poles (<c>yardLight</c> 7.26 m, <c>floodMast</c> 7.8 m) stand over open working ground and take
        /// <see cref="LightPresets.Kind.Floodlight"/> at 9.5 m. <see cref="LightPresets.Kind.Worklight"/>
        /// (5.2 m, a lamp on a wall) fits none of these four and is left exactly as shipped.</para>
        /// </summary>
        public static LightPresets.Kind PresetFor(string key)
        {
            switch (key)
            {
                case LanternPost: return LightPresets.Kind.Lightpost;
                case StreetLamp:  return LightPresets.Kind.Lightpost;
                case YardLight:   return LightPresets.Kind.Floodlight;
                case FloodMast:   return LightPresets.Kind.Floodlight;
                default:          return LightPresets.Kind.Lightpost;
            }
        }

        /// <summary>
        /// How high this piece's lamp head sits, in metres — <b>read off the PACK'S OWN CONTRACT</b>
        /// (<c>heightM</c>, the honest drawn height the rig publishes for every <c>utilityIso</c> /
        /// <c>wharfDecor</c> piece), never a number chosen here.
        ///
        /// <para>It matters because <see cref="LampShadowSystem"/> takes the lamp's elevation as
        /// <c>h/√(h²+d²)</c>: the height is what decides whether a caster's shadow is a short pool at its
        /// feet or a long rake across the yard. Left at <see cref="SceneLight.DefaultLampHeightMeters"/>
        /// (2.5 m) a 7.8 m flood mast would light like a mast and shadow like a bollard.</para>
        ///
        /// <para>The head is at the TOP of every one of these pieces (a lantern on its post, a lamp on its
        /// swan neck, a floodlight on its mast), so the drawn height IS the head height to within the
        /// fitting's own depth — which is a good deal more honest than 2.5 m for all four.</para>
        ///
        /// <para>Falls back to the <see cref="SceneLight"/> default with a warning if the pack has no entry:
        /// a guessed lamp height is a lamp whose shadows lie, and it should say so out loud.</para>
        /// </summary>
        public static float HeadHeightMetres(string family, string key)
        {
            var contract = IsoPackSprites.ContractOf(family);
            if (contract != null && contract.TryGet(key, out IsoPackContract.Cell cell) && cell.heightM > 0f)
                return cell.heightM;

            Debug.LogWarning(
                $"[LampPosts] '{family}/{key}' publishes no heightM in its pack contract, so its lamp head " +
                $"falls back to SceneLight's {SceneLight.DefaultLampHeightMeters:0.0} m default. Its cast " +
                "shadows will be a 2.5 m lamp's, whatever the post actually is.");
            return SceneLight.DefaultLampHeightMeters;
        }

        /// <summary>
        /// One placed lamp post: which kit piece, where it stands, which way it looks, and one line on why
        /// it is there. The <c>Reason</c> is the same discipline <c>NineMileCreekDressing.Prop</c> keeps —
        /// a light that cannot say what it is for is the one to cut.
        /// </summary>
        public readonly struct Site
        {
            /// <summary>The ISO pack family the sprite comes from.</summary>
            public readonly string Family;

            /// <summary>The kit key — one of the four constants above.</summary>
            public readonly string Key;

            /// <summary>Where the post's FOOT stands (both packs pivot at the ground centre).</summary>
            public readonly Vector2 Position;

            /// <summary>Compass heading (N = 0, clockwise) the piece's front looks along, turned into a
            /// facing cell by the pack's own declared convention and never by a guess here.</summary>
            public readonly float Heading;

            /// <summary>
            /// The deck this post stands ON, when it stands on a structure rather than on the ground.
            /// Zero-width means "on the ground" and the placer checks the post is DRY instead.
            ///
            /// <para>⚠ The distinction is load-bearing, not bookkeeping. A post on the St Peters pier
            /// stands on planks over a slip dredged to <b>−1.0 m</b>: check it against the terrain and a
            /// perfectly good lamp is rejected for standing in water it is six metres above.</para>
            /// </summary>
            public readonly Rect Deck;

            /// <summary>Why this lamp is here.</summary>
            public readonly string Reason;

            public Site(string family, string key, Vector2 position, float heading, string reason,
                        Rect deck = default)
            {
                Family = family; Key = key; Position = position; Heading = heading;
                Reason = reason; Deck = deck;
            }

            /// <summary>True when this post stands on a registered deck rather than on the ground.</summary>
            public bool StandsOnDeck => Deck.width > 0f && Deck.height > 0f;
        }

        /// <summary>A lamp post on the ground.</summary>
        public static Site OnGround(string family, string key, Vector2 position, float heading, string reason)
            => new Site(family, key, position, heading, reason);

        /// <summary>A lamp post standing on a wharf deck (checked against the planks, not the seabed).</summary>
        public static Site OnDeck(string family, string key, Vector2 position, float heading, Rect deck,
                                  string reason)
            => new Site(family, key, position, heading, reason, deck);

        /// <summary>
        /// Place a region's lamp posts under <paramref name="parent"/>, returning how many stood up.
        ///
        /// <para>A site that fails its standing check is reported as an ERROR and skipped — the
        /// <c>NineMileCreekDressing</c> rule: a prop standing in water is an authoring bug to fix loudly,
        /// not a prop to quietly drop. A site whose SPRITE is missing is a warning and a skip, because
        /// un-imported art is a pipeline state and not an authoring mistake.</para>
        /// </summary>
        /// <param name="terrain">The region's authored terrain, for the ground sites' dryness check. A null
        /// terrain skips the check (a caller with no terrain to check against, e.g. a bare test scene).</param>
        /// <param name="minDryElevationMetres">The water the ground must stand clear of — the region's own
        /// spring high water, passed in because the two regions do not share one.</param>
        public static int Place(Transform parent, IReadOnlyList<Site> sites, ITidalTerrain terrain,
                                float minDryElevationMetres, string logPrefix)
        {
            if (sites == null || sites.Count == 0) return 0;

            int placed = 0;
            foreach (var site in sites)
            {
                if (!StandingIsSound(site, terrain, minDryElevationMetres, logPrefix)) continue;

                int facing = IsoPackSprites.FacingForHeading(site.Family, site.Heading);
                Sprite sprite = IsoPackSprites.Facing(site.Family, site.Key, facing);
                if (sprite == null)
                {
                    Debug.LogWarning(
                        $"{logPrefix} '{site.Key}' facing {facing} has no sprite at " +
                        $"{IsoPackSprites.SheetPath(site.Family, site.Key)} — skipping it rather than " +
                        $"placing a blank post. It would have been: {site.Reason}.");
                    continue;
                }

                var go = new GameObject($"{site.Key}_{placed}");
                go.transform.SetParent(parent, worldPositionStays: false);
                // The pivot IS the ground centre of the footprint (both packs' contracts say so), so the
                // site position is the position and nothing here offsets it.
                go.transform.position = new Vector3(site.Position.x, site.Position.y, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                // The band, never a hand-picked order (ADR 0032). A lamp post is a tall thing you walk in
                // front of and behind, which a fixed order cannot express.
                go.AddComponent<YSortSprite>();

                Light(go, site.Family, site.Key);

                // A post is a stander in the sun like the wharf's own bollards and pileheads, and its pivot
                // is its base — which is where a shadow anchors (ADR 0026).
                //
                // ⚠ But NOT the ground-contact pool. That pool is gated on the caster's drawn HEIGHT (3 m),
                // which stands in for "has mass overhead" — true of the trees it was measured on, false of
                // a lamp post, which is a thin pole that clears the gate with nothing to throw straight
                // down. Three of these four pieces clear it (streetLamp 4.48 m, yardLight 7.26 m, floodMast
                // 7.8 m; only lanternPost at 2.46 m is under), so leaving it on would put a dark ellipse
                // under every tall lamp — on the one patch of ground the lamp is there to light.
                go.AddComponent<SpriteShadow>().CastsGroundContact = false;

                placed++;
            }
            return placed;
        }

        /// <summary>
        /// Give an already-placed object the preconfigured lamp for a kit key: the night-gated glow, the
        /// preset's look, and the lamp head height the PACK publishes.
        ///
        /// <para>Public and separate from <see cref="Place"/> so a builder that already draws the piece for
        /// its own reasons can light it without the sprite being placed twice — which is exactly Nine Mile
        /// Creek's case, where the yard light at the wharf entrance has stood there since #462 as a decor
        /// prop described as <i>"the only lit thing out here at night"</i> and has never emitted a photon.
        /// </para>
        ///
        /// <para>⚠ The preset is stamped onto the <see cref="SceneLight"/> HERE as well as being set on the
        /// <see cref="PreconfiguredLight"/>. <see cref="PreconfiguredLight"/> stamps in <c>Awake</c>, which
        /// does not run at build time, so without this the SCENE would be saved carrying
        /// <see cref="SceneLight"/>'s bare defaults (a 6 m white CONE) and only look right once the game
        /// ran. Both paths call <see cref="LightPresets.Apply"/>, so they cannot disagree.</para>
        /// </summary>
        public static SceneLight Light(GameObject go, string family, string key)
        {
            if (go == null) return null;

            LightPresets.Kind preset = PresetFor(key);

            // SceneLight FIRST, so the height below lands on the instance PreconfiguredLight will adopt
            // rather than on a second one it adds for itself.
            var light = go.GetComponent<SceneLight>();
            if (light == null) light = go.AddComponent<SceneLight>();
            LightPresets.Apply(light, preset);
            light.LampHeightMeters = HeadHeightMetres(family, key);
            light.CastsShadows = true;      // the wharf's gear throws from the post (lights PR B)

            var carried = go.GetComponent<PreconfiguredLight>();
            if (carried == null) carried = go.AddComponent<PreconfiguredLight>();
            carried.Preset = preset;

            return light;
        }

        /// <summary>
        /// Is this post standing on something? A deck post must be ON its deck; a ground post must be
        /// clear of spring high water. Reports the failure loudly and returns false.
        /// </summary>
        static bool StandingIsSound(in Site site, ITidalTerrain terrain, float minDryElevationMetres,
                                    string logPrefix)
        {
            if (site.StandsOnDeck)
            {
                if (site.Deck.Contains(site.Position)) return true;
                Debug.LogError(
                    $"{logPrefix} '{site.Key}' is sited at {site.Position}, which is OFF the deck it is " +
                    $"declared to stand on ({site.Deck}). {site.Reason}. Move the site; a post beside a " +
                    "pier is a post in the water.");
                return false;
            }

            if (terrain == null) return true;

            float ground = terrain.ElevationAt(site.Position);
            if (ground > minDryElevationMetres) return true;

            Debug.LogError(
                $"{logPrefix} '{site.Key}' is sited at {site.Position} where the ground is {ground:0.00} m " +
                $"— at or below spring high water ({minDryElevationMetres:0.0} m). {site.Reason}. Move the " +
                "site; do not lower the tide.");
            return false;
        }
    }
}
#endif
