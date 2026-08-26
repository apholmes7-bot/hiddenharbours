using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>THE ONE SCATTER.</b> Given a per-region grass FIELD (species + density where the pass painted it)
    /// and a seed, this derives every tuft — where it stands, which baked blade it wears, how big, mirrored
    /// or not, and its tint. It is a pure function: no <c>Random</c>, no <c>Time</c>, no scene, no
    /// <c>UnityEditor</c>. Feed it the same field and the same seed and it grows the same meadow on every
    /// machine, every load, for ever (rule 5) — which is exactly why the tufts do not need to be saved,
    /// serialized, or shipped as GameObjects.
    ///
    /// <para><b>⭐ WHY THIS EXISTS: 27,000 TUFTS WENT INTO A .unity FILE.</b> The grass pass used to emit a
    /// GameObject per tuft — GO + Transform + SpriteRenderer + YSortSprite, about 3.1 KB of YAML each. At the
    /// density the owner ratified that is ~27,000 tufts, and St Peters weighed <b>114 MB</b> against main's
    /// 15.8 MB; GitHub's 100 MB limit is what actually caught it, and rule 7 condemned it either way. The
    /// tufts were never authored data — every one of them was already a deterministic function of the
    /// ground. So the SCENE now stores the field (a byte per candidate site, run-length encoded) and the
    /// tufts are DERIVED here at load. The picture is the same; the file is four orders of magnitude
    /// smaller.</para>
    ///
    /// <para><b>⚠ THE BAKE CALLS THIS TOO — that is the point.</b> The editor pass that writes the field
    /// walks the very same <see cref="SlotPosition"/> to decide what stands where, so the position a site is
    /// GATED at and the position it is DRAWN at cannot drift apart. A twinned copy of this arithmetic on the
    /// editor side would be a second definition of the meadow, and the two would diverge the first time
    /// somebody re-tuned a jitter. There is one definition, and it lives in the runtime assembly because
    /// that is the assembly that has to be right in a build.</para>
    ///
    /// <para><b>The hash.</b> FNV-1a over (x, y, salt), the same mixer <c>StPetersShoreMap.Hash01</c> uses,
    /// restated here because the runtime assembly cannot reference an editor one (rule 4). The two are
    /// pinned equal by <c>GrassFieldScatterTests</c>, so the meadow this derives is the meadow the old
    /// object-per-tuft pass drew — the migration changes what is STORED, not what is SEEN.</para>
    ///
    /// <para><b>Seeding.</b> The seed is folded into every salt, and it is carried BY THE FIELD rather than
    /// taken from the player's save. That is deliberate: a meadow keyed on a per-player world seed would
    /// grow differently for every player, which is not what an authored region wants. The field's seed is
    /// the region's world seed, stamped once at bake, so "deterministic from (field, worldSeed)" and "every
    /// machine grows the same meadow" are the same sentence. Seed 0 reproduces the shipped scatter
    /// bit-for-bit.</para>
    /// </summary>
    public static class GrassFieldScatter
    {
        // =====================================================================================
        // the slot byte — one byte per candidate site, and the whole reason the file got small
        // =====================================================================================
        // A CELL is one square of the scatter grid; it holds up to Slots SITES (the old pass planted 1-2
        // tufts per cell). Each site is one byte:
        //
        //     bits 0..3   habitat id.  0 = NOTHING GROWS HERE. 1..15 index the field's habitat table.
        //     bit  4      broad — this site asked for the widest art its habitat carries.
        //     bits 5..6   edge tier (GrassTier) — how near the field's edge this site stands, and so
        //                 which height classes it may wear. 0 = interior, which is what every field
        //                 baked before 2026-08-26 stored in those bits, so an OLD field decodes as
        //                 all-interior and draws exactly what it always drew.
        //     bit  7      spare (kept zero, so a later flag costs no re-bake of the layout).
        //
        // Everything else a tuft needs — its jittered position, which variant it wears, its scale, whether
        // it is mirrored, its brightness jitter — is a HASH of (cell, slot, seed) and is therefore not
        // stored at all. Only what the pass LEARNED FROM THE GROUND is stored, because only that cannot be
        // recomputed without the editor's terrain.

        /// <summary>Habitat id meaning "no tuft on this site". A run of these is what empty ground
        /// costs, and run-length encoding turns the sea, the paths and the building plots into a
        /// handful of bytes.</summary>
        public const byte EmptySlot = 0;

        /// <summary>Mask of the slot byte's habitat id.</summary>
        public const byte HabitatMask = 0x0F;

        /// <summary>The most habitats a field may name. The habitat id is a nibble and 0 is spoken for,
        /// so fifteen — St Peters uses six.</summary>
        public const int MaxHabitats = 15;

        /// <summary>Slot-byte bit meaning "this site wants the broadest art its habitat has".</summary>
        public const byte BroadFlag = 0x10;

        /// <summary>Mask of the slot byte's <see cref="GrassTier"/>, once shifted down by
        /// <see cref="TierShift"/>. Two bits: three tiers and one spare value.</summary>
        public const byte TierMask = 0x03;

        /// <summary>Where the tier sits in the slot byte.</summary>
        public const int TierShift = 5;

        /// <summary>The tiers a field may name — <see cref="TierMask"/> + 1. A fourth would need the
        /// spare bit 7 and a re-bake of every field.</summary>
        public const int MaxTiers = TierMask + 1;

        public static byte PackSlot(int habitatId, bool broad, GrassTier tier = GrassTier.Interior)
        {
            if (habitatId <= 0) return EmptySlot;                 // empty wins: an empty site has no flags
            byte b = (byte)(habitatId & HabitatMask);
            if (broad) b |= BroadFlag;
            b |= (byte)((((int)tier) & TierMask) << TierShift);
            return b;
        }

        public static int HabitatOf(byte slot) => slot & HabitatMask;
        public static bool BroadOf(byte slot) => (slot & BroadFlag) != 0;
        public static bool IsEmpty(byte slot) => (slot & HabitatMask) == 0;

        /// <summary>This site's edge tier. A field baked before the tier existed stored zeros in these
        /// bits, which decodes to <see cref="GrassTier.Interior"/> — the full pool, exactly what such a
        /// field has always drawn.</summary>
        public static GrassTier TierOf(byte slot) => (GrassTier)((slot >> TierShift) & TierMask);

        // =====================================================================================
        // the salts
        // =====================================================================================
        // ⚠ These are the SHIPPED salts of the object-per-tuft pass, carried over verbatim so the derived
        // meadow is the meadow the owner already ratified — same tufts, same places, same blades. Changing
        // one re-rolls the whole island, which is a look change, not a refactor.

        const int SaltJitterX = 163;
        const int SaltJitterY = 167;
        const int SaltSpreadX = 181;
        const int SaltSpreadY = 191;
        const int SaltScale = 193;
        const int SaltRoll = 197;
        const int SaltMirror = 211;

        /// <summary>How far the seed moves a salt. A large odd stride so two seeds one apart do not share
        /// a single salt between them; seed 0 leaves every salt exactly where it shipped.</summary>
        const int SeedStride = 7919;

        // =====================================================================================
        // geometry — where a site stands
        // =====================================================================================

        /// <summary>
        /// The centre of a grid cell, jittered off the lattice.
        ///
        /// <para><b>⚠ The jitter is not decoration.</b> A grid of tufts reads as rows the moment the player
        /// walks along it; a jitter approaching the step turns the lattice into a Poisson field, which is
        /// the arrangement that stays even as it gets dense. Its RATIO to the step is what matters and is
        /// held by the field itself (<see cref="GrassFieldLayout.JitterMetres"/>), not restated here.</para>
        /// </summary>
        public static Vector2 CellCentre(in GrassFieldLayout layout, int ix, int iy)
        {
            float x = layout.OriginX + (ix + 0.5f) * layout.CellSize
                    + (Hash01(ix, iy, Salt(SaltJitterX, layout.Seed)) * 2f - 1f) * layout.JitterMetres;
            float y = layout.OriginY + (iy + 0.5f) * layout.CellSize
                    + (Hash01(ix, iy, Salt(SaltJitterY, layout.Seed)) * 2f - 1f) * layout.JitterMetres;
            return new Vector2(x, y);
        }

        /// <summary>
        /// Where one SITE stands — the cell centre plus a small per-slot offset, so a cell's two tufts read
        /// as one clump of growth rather than two stacked sprites.
        ///
        /// <para><b>This is the function the bake gates on.</b> The editor pass asks "is THIS point
        /// plantable, and what habitat is it?" at exactly the point this returns, and stores the answer in
        /// the slot byte. Runtime then draws at the same point. One definition, so the gate and the draw
        /// cannot disagree about where a tuft is — the failure that would put grass on a footpath the pass
        /// believed it had kept clear.</para>
        /// </summary>
        public static Vector2 SlotPosition(in GrassFieldLayout layout, int ix, int iy, int slot)
        {
            Vector2 c = CellCentre(layout, ix, iy);
            return new Vector2(
                c.x + (Hash01(ix * 3 + slot, iy, Salt(SaltSpreadX, layout.Seed)) * 2f - 1f) * layout.SpreadMetres,
                c.y + (Hash01(ix, iy * 3 + slot, Salt(SaltSpreadY, layout.Seed)) * 2f - 1f) * layout.SpreadMetres);
        }

        // =====================================================================================
        // the per-site rolls — everything the field does NOT have to store
        // =====================================================================================

        /// <summary>The site's own stable roll, 0..1023 — which variant of its habitat's pool it wears.
        /// Stable in (cell, slot, seed), so the same metre of ground always grows the same blade.</summary>
        public static int VariantRoll(in GrassFieldLayout layout, int ix, int iy, int slot) =>
            (int)(Hash01(ix, iy, Salt(SaltRoll + slot, layout.Seed)) * 1024f);

        /// <summary>The site's shared shape roll, 0..1. Drives BOTH the height scale and the tint's
        /// brightness jitter — one roll, exactly as the object pass did, so a tuft that draws a little
        /// taller also draws a little brighter and the field does not look like two noises laid over each
        /// other.</summary>
        public static float ShapeRoll(in GrassFieldLayout layout, int ix, int iy, int slot) =>
            Hash01(ix * 5 + slot, iy * 7 + slot, Salt(SaltScale, layout.Seed));

        /// <summary>Whether this site draws mirrored. Free variety: the tufts pivot bottom-CENTRE, the wind
        /// is world-space and the shader's bend reads sprite <c>uv.y</c>, so a mirrored tuft leans and bends
        /// exactly like an unmirrored one while doubling the silhouettes the eye has to tell apart.</summary>
        public static bool Mirrored(in GrassFieldLayout layout, int ix, int iy, int slot) =>
            Hash01(ix, iy * 13 + slot, Salt(SaltMirror, layout.Seed)) < 0.5f;

        /// <summary>
        /// The tuft's tint — the straw the field baked for this ground, with the site's own brightness
        /// jitter. Multiplied over the sprite's own dark-to-light gradient by the grass shader, so the drawn
        /// shading survives the tint (a multiply cannot flatten a gradient).
        /// </summary>
        public static Color TintFor(Color strawTint, float straw01, float shapeRoll)
        {
            Color baseTint = Color.Lerp(Color.white, strawTint, Mathf.Clamp01(straw01));
            float v = Mathf.Lerp(0.9f, 1.1f, shapeRoll);
            return new Color(Mathf.Clamp01(baseTint.r * v),
                             Mathf.Clamp01(baseTint.g * v),
                             Mathf.Clamp01(baseTint.b * v), 1f);
        }

        // =====================================================================================
        // the hash
        // =====================================================================================

        /// <summary>How the seed moves a salt. Public so the bake and the tests can speak about the same
        /// mapping instead of re-deriving it.</summary>
        public static int Salt(int salt, int seed) => unchecked(salt + seed * SeedStride);

        /// <summary>
        /// A stable 0..1 hash of (x, y, salt) — FNV-1a with a final avalanche. The same mixer
        /// <c>StPetersShoreMap.Hash01</c> uses; restated because a runtime assembly may not reference an
        /// editor one, and pinned equal to it by test so the restatement can never quietly drift.
        /// </summary>
        public static float Hash01(int x, int y, int salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)x) * 16777619u;
                h = (h ^ (uint)y) * 16777619u;
                h = (h ^ (uint)salt) * 16777619u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }

    /// <summary>
    /// <b>How near the edge of the field a site stands — and so how TALL it is allowed to be.</b>
    ///
    /// <para>A meadow that thins toward its boundary but stays knee-high to the last blade still ends in
    /// a line; it is just a dotted one. So the density ramp (the region pass's own edge band) carries a
    /// height step-down with it: the interior wears whatever its habitat has, the outer band steps off
    /// the tall classes, and the last metre is short blades and verge clumps. That is what makes a field
    /// lie DOWN into the flat ground it meets instead of stopping against it.</para>
    ///
    /// <para><b>⚠ The values are stored in the slot byte, so they are append-only like a Def id.</b>
    /// Interior is 0 on purpose: every field baked before 2026-08-26 has zeros in those bits and
    /// therefore decodes as all-interior, drawing exactly the meadow it always drew. There is room for
    /// one more tier before bit 7 has to be spent.</para>
    ///
    /// <para>Which HEIGHT CLASSES a tier actually wears is not decided here — that is the grass
    /// library's answer, resolved at bake by the region planter's art chooser. This names the GROUND,
    /// the same split of concerns the habitat tag makes.</para>
    /// </summary>
    public enum GrassTier
    {
        /// <summary>Inside the band: the full habitat pool, exactly as it shipped.</summary>
        Interior = 0,

        /// <summary>The outer band: the mid classes, stepping down off the tall blades.</summary>
        Band = 1,

        /// <summary>The last metre: the short and verge classes.</summary>
        Hem = 2,
    }

    /// <summary>
    /// The field's SHAPE — where its grid sits in the world, how fine it is, how far a site may wander, and
    /// the seed the whole meadow grows from. Passed by <c>in</c> everywhere so deriving 27,000 tufts copies
    /// no structs it does not have to.
    ///
    /// <para>A plain struct rather than a component reference on purpose: <see cref="GrassFieldScatter"/> is
    /// a pure function and stays testable headless with no scene at all.</para>
    /// </summary>
    public struct GrassFieldLayout
    {
        /// <summary>World X of the grid's minimum corner (the low edge of column 0).</summary>
        public float OriginX;

        /// <summary>World Y of the grid's minimum corner (the low edge of row 0).</summary>
        public float OriginY;

        /// <summary>Grid step in metres — the grain of the field. The site count goes as its INVERSE
        /// SQUARE, so this is the one number to turn if a region has to get cheaper.</summary>
        public float CellSize;

        /// <summary>How far a site may wander from its cell centre (m). Held as a RATIO of
        /// <see cref="CellSize"/> by whoever bakes the field — a jitter that does not scale with the step
        /// buys holes.</summary>
        public float JitterMetres;

        /// <summary>Radius (m) of the ring a cell's later sites are offset into.</summary>
        public float SpreadMetres;

        /// <summary>Columns and rows of the grid.</summary>
        public int CellsX, CellsY;

        /// <summary>Sites per cell — the density ceiling. Every site is one byte whether it grows or
        /// not, which is what keeps the codec a flat, run-length-friendly plane.</summary>
        public int Slots;

        /// <summary>The seed the meadow grows from, carried by the field so every machine grows the same
        /// one. 0 reproduces the shipped scatter exactly.</summary>
        public int Seed;
    }
}
