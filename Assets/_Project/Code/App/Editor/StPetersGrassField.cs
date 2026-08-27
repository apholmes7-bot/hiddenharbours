#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The grass pass, with its OUTPUT changed and its INPUT untouched.</b> Everything
    /// <see cref="StPetersGrass"/> knows about this island's ground — the meadow's clearings, the swathe
    /// field, the walked paths and their verges, the dune/fringe/headland bands, which sites want a broad
    /// clump — is read exactly as it was. What changes is what comes out the far end: instead of a
    /// GameObject per tuft, this bakes a <b>FIELD</b>, one byte per candidate site, which
    /// <see cref="GrassField"/> grows the meadow from at load.
    ///
    /// <para><b>⭐ WHY: 27,000 GameObjects went into a .unity file and it weighed 114 MB.</b> Four
    /// serialized objects per tuft (GameObject, Transform, SpriteRenderer, YSortSprite) at ~3.1 KB of YAML.
    /// GitHub's 100 MB limit is what caught it; rule 7 condemned it regardless. Not one of those objects was
    /// authored — every one was already a deterministic function of the painted ground, which is why the
    /// ground's ANSWER is the only thing worth storing.</para>
    ///
    /// <para><b>⚠ THE GATE AND THE DRAW READ ONE FUNCTION.</b> Where a site stands comes from
    /// <see cref="GrassFieldScatter.SlotPosition"/> — the runtime's own arithmetic, called from here. The
    /// bake therefore gates each site at exactly the point the renderer will later draw it. A twinned copy
    /// of the jitter on this side would be a second definition of the meadow, and the first re-tune would
    /// have grass standing on a footpath the pass believed it had kept clear.</para>
    ///
    /// <para><b>⚠ IDEMPOTENT, WHICH THE OLD BRUSH WAS NOT (ADR 0019).</b> The stroke that doubled the
    /// owner's scene APPENDED objects and rejected on min-spacing, so dragging over the same ground twice
    /// laid more grass. A field cannot append: there is one byte per site, and writing it a second time
    /// writes the same value. Re-running this pass is a no-op by construction, not by a clear-first
    /// convention that somebody has to remember.</para>
    ///
    /// <para><b>What the SEED is doing here.</b> The field carries the seed its meadow grows from, so every
    /// machine grows the same one and a save carries nothing. St Peters bakes <see cref="GrassFieldSeed"/> =
    /// 0, which reproduces the scatter the owner already ratified tuft for tuft — this change alters what is
    /// STORED, not what is SEEN.</para>
    ///
    /// <para><b>⭐ AND SINCE NINE MILE CREEK, THE ENCODER IS SHARED.</b> <see cref="Encode"/> is the
    /// region-agnostic half — layout in, byte planes out — and <see cref="BakeField"/> is St Peters' thin
    /// wrapper round it. The mainland's fields bake through the same call with their own layout, their
    /// own scatter and their own straw, because two copies of a byte-packing rule is how one region's
    /// meadow quietly stops matching the renderer that draws it.</para>
    /// </summary>
    public static class StPetersGrassField
    {
        /// <summary>
        /// The seed St Peters' meadow grows from, stamped into the field at bake.
        ///
        /// <para><b>0 on purpose.</b> Every salt in <see cref="GrassFieldScatter"/> is the shipped salt
        /// offset by <c>seed × stride</c>, so seed 0 leaves them exactly where the object-per-tuft pass had
        /// them and the derived meadow is the ratified meadow — same tufts, same places, same blades. Turn
        /// this and the whole island re-rolls, which is a look change and wants the owner's eye, not a
        /// refactor's.</para>
        /// </summary>
        public const int GrassFieldSeed = 0;

        /// <summary>
        /// Grain (m) of the STRAW plane — the dry-coast bleach, baked as its own coarse grid.
        ///
        /// <para>Tint is a smooth function of exposure, and a smooth signal is the one thing run-length
        /// encoding is bad at: stored per site it would cost a byte a tuft and compress to almost nothing.
        /// At 4 m the whole island's straw is a couple of thousand bytes, and the renderer samples it
        /// bilinearly — what it drives is a tint MULTIPLY over each sprite's own gradient, where a 4 m
        /// gradient step is invisible and a hard cell edge would not be.</para>
        /// </summary>
        public const float StrawCellMetres = 4f;

        /// <summary>
        /// The habitat vocabulary, in ID order — <b>append-only, like a Def id</b>. The field stores a
        /// habitat as a nibble index into this list (0 means "nothing grows here", so the first name is
        /// id 1). Re-ordering it would silently repaint every baked field with the wrong art, so new ground
        /// types go on the END.
        /// </summary>
        public static readonly string[] HabitatIds =
        {
            StPetersGrass.HabitatSward,
            StPetersGrass.HabitatMeadow,
            StPetersGrass.HabitatFringe,
            StPetersGrass.HabitatDune,
            StPetersGrass.HabitatHeadland,
            StPetersGrass.HabitatVerge,
        };

        /// <summary>The baked field: the two byte planes, the grid they describe, and what it measured.</summary>
        public sealed class Baked
        {
            public GrassFieldLayout Layout;
            public byte[] Sites;
            public byte[] Straw;
            public float StrawCellSize;
            public int StrawCellsX, StrawCellsY;

            /// <summary>Tufts the field describes — the meadow's size, for the log the owner reads.</summary>
            public int Tufts;

            /// <summary>Tufts per habitat tag. The number worth reading when the island stops looking
            /// right, because it says WHICH ground got which art.</summary>
            public readonly Dictionary<string, int> PerHabitat = new Dictionary<string, int>();

            /// <summary>Tufts per edge tier. ⭐ The number that says whether the edge band is DOING
            /// anything: a bake that reports zero band and zero hem tufts has a falloff that never
            /// fires, which looks exactly like the hard line it was meant to remove.</summary>
            public readonly Dictionary<GrassTier, int> PerTier = new Dictionary<GrassTier, int>();

            public string HabitatSummary()
            {
                var parts = new List<string>();
                foreach (var kv in PerHabitat) parts.Add($"{kv.Key} {kv.Value}");
                parts.Sort();
                return string.Join(", ", parts);
            }

            /// <inheritdoc cref="PerTier"/>
            public string TierSummary()
            {
                var parts = new List<string>();
                foreach (GrassTier tier in new[] { GrassTier.Interior, GrassTier.Band, GrassTier.Hem })
                    parts.Add($"{tier.ToString().ToLowerInvariant()} " +
                              $"{(PerTier.TryGetValue(tier, out int n) ? n : 0)}");
                return string.Join(", ", parts);
            }
        }

        /// <summary>
        /// Read the island and write the field. A thin wrapper now: it decides WHICH island, and
        /// <see cref="Encode"/> does the encoding.
        ///
        /// <para><b>⚠ It does not walk the island itself — <see cref="StPetersGrass.Scatter"/> does.</b>
        /// That is the whole point: the gates (the meadow's clearings, the swathe field, the per-cell chance
        /// that is sparser under the stands, and the per-SITE re-check that keeps a spilled offset off a
        /// footpath) stay in the one class that has always owned them, and this only ENCODES the answer.
        /// A second grid walk here would be a second definition of the meadow, and the two would part
        /// company the first time somebody re-tuned a threshold.</para>
        /// </summary>
        public static Baked BakeField(ITidalTerrain terrain, int seed = GrassFieldSeed)
        {
            if (terrain == null) return new Baked();
            return Encode(StPetersGrass.FieldLayout(seed), StPetersGrass.Scatter(terrain, seed),
                          StrawCellMetres, StPetersStraw);
        }

        /// <summary>The island's straw: the same <c>exposure × 0.8</c> the object pass tinted each tuft
        /// with.</summary>
        static float StPetersStraw(Vector2 p) => Mathf.Clamp01(StPetersWoods.ExposureAt(p) * 0.8f);

        /// <summary>
        /// <b>Encode a scatter into a field.</b> The region-agnostic half, extracted so a second region can
        /// bake its own meadow without a second copy of this arithmetic — which is the same "two
        /// definitions of one thing" failure this whole file exists to prevent, one level up.
        ///
        /// <para>Every site carries its own cell and slot index, so packing is a direct write — no search,
        /// no re-derivation, and no way for a site to land in another cell's byte. A site whose habitat
        /// this field's vocabulary cannot name is DROPPED rather than stored as 0, because 0 means
        /// "nothing grows here" and a silently-renamed habitat would empty a region with nothing
        /// failing.</para>
        ///
        /// <para><paramref name="strawAt"/> is the region's own bleach field, sampled at cell centres —
        /// the one thing about the straw plane that is not shared, because how dry a coast gets is a
        /// property of that coast.</para>
        /// </summary>
        public static Baked Encode(GrassFieldLayout layout,
                                   IEnumerable<StPetersGrass.GrassTuftSite> sites,
                                   float strawCellMetres,
                                   System.Func<Vector2, float> strawAt)
        {
            var baked = new Baked { Layout = layout };
            var plane = new byte[layout.CellsX * layout.CellsY * layout.Slots];

            if (sites != null)
            foreach (var site in sites)
            {
                int id = HabitatIdOf(site.Habitat);
                if (id <= 0) continue;                    // a habitat this field cannot name grows nothing

                int at = (site.CellY * layout.CellsX + site.CellX) * layout.Slots + site.Slot;
                if ((uint)at >= (uint)plane.Length) continue;

                plane[at] = GrassFieldScatter.PackSlot(id, site.Broad, site.Tier);
                baked.Tufts++;
                baked.PerHabitat[site.Habitat] =
                    baked.PerHabitat.TryGetValue(site.Habitat, out int n) ? n + 1 : 1;
                baked.PerTier[site.Tier] =
                    baked.PerTier.TryGetValue(site.Tier, out int t) ? t + 1 : 1;
            }

            baked.Sites = plane;
            BakeStraw(layout, baked, strawCellMetres, strawAt);
            return baked;
        }

        /// <summary>
        /// The straw plane: how far this ground has bleached toward dry, per
        /// <paramref name="cellMetres"/>. Sampled at cell centres and quantised to a byte — a 1/255 step
        /// on a tint multiply, which no eye resolves.
        /// </summary>
        static void BakeStraw(in GrassFieldLayout layout, Baked baked, float cellMetres,
                              System.Func<Vector2, float> strawAt)
        {
            float cell = Mathf.Max(0.5f, cellMetres);
            float widthM = layout.CellsX * layout.CellSize;
            float heightM = layout.CellsY * layout.CellSize;

            // +2 so the bilinear tap at the far edge still lands on real data rather than a clamp.
            int sx = Mathf.CeilToInt(widthM / cell) + 2;
            int sy = Mathf.CeilToInt(heightM / cell) + 2;

            var straw = new byte[sx * sy];
            for (int y = 0; y < sy; y++)
            for (int x = 0; x < sx; x++)
            {
                var p = new Vector2(layout.OriginX + (x + 0.5f) * cell,
                                    layout.OriginY + (y + 0.5f) * cell);
                float s = strawAt != null ? Mathf.Clamp01(strawAt(p)) : 0f;
                straw[y * sx + x] = (byte)Mathf.RoundToInt(s * 255f);
            }

            baked.Straw = straw;
            baked.StrawCellSize = cell;
            baked.StrawCellsX = sx;
            baked.StrawCellsY = sy;
        }

        /// <summary>This habitat's field id, or 0 if the field cannot name it. Ids are 1-based because 0
        /// means "nothing grows here" in the slot byte.</summary>
        public static int HabitatIdOf(string habitat)
        {
            for (int i = 0; i < HabitatIds.Length; i++)
                if (HabitatIds[i] == habitat) return i + 1;
            return 0;
        }
    }
}
#endif
