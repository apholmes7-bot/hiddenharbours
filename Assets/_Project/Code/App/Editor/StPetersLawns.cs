#if UNITY_EDITOR
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The mown lawns</b> — the ground inside a property's yard, painted as the Lawn terrain
    /// material rather than left as the wild grass band.
    ///
    /// <para><b>The owner's ask (2026-08-26):</b> <i>"i want peoples residences to have trimmed nice
    /// looking lawns, there should be a noticable difference between trimmed grass and wild grass"</i>,
    /// ruled as <b>kept everywhere, striped only on the properties that would bother</b>.</para>
    ///
    /// <para><b>⚠⚠ A LAWN IS NOT GRASS PAINTED LOW, AND THAT IS THE WHOLE REASON IT COSTS A SLOT.</b>
    /// The kit's Grass ladder reads <i>"grazed, trodden thin turf"</i> at its <c>_Lo</c> step, which
    /// sounds exactly like a lawn. It is not reachable. A painted channel's value is BOTH the blend
    /// weight against the height-derived bands AND the position on that material's ladder
    /// (<c>HiddenHarboursTerrainSplat.shader</c>, the painted-overrides block), so asking for Grass's
    /// low step is the same act as asking for LESS grass: you get sparse rank meadow with the wild
    /// band showing through, and painting it high instead lands on <c>_Hi</c>, which is rank meadow
    /// WITH SEED HEADS. There is no way to say "a lot of very short grass". Hence a Lawn material,
    /// with its ladder deliberately running the other way round — see
    /// <c>docs/art/rigs/terrain/terrainBake5.js</c>.</para>
    ///
    /// <para><b>⭐ AND THAT COUPLING IS WHY THERE ARE THREE TIERS OF CARE AND ONLY ONE NUMBER.</b>
    /// Because more material means a better lawn, <see cref="IntensityFor"/> is simultaneously "how
    /// well cut" and "how much wild meadow shows through". A yard at 1.0 is full-weight crisp turf; a
    /// yard at 0.55 is half-weight ordinary turf with the meadow coming through the other half, which
    /// is precisely what a let-go dooryard looks like. No second channel, no second material.</para>
    ///
    /// <para><b>The extent is the YARD POLYGON</b> (<see cref="StPetersYards"/>), which is the lawn
    /// ruling's whole point: one authored fact per property, and the lawn, the wild-grass suppression
    /// and the fence line all read it. There is no second lawn boundary to keep in step.</para>
    ///
    /// <para><b>⭐ The edge into the meadow comes FREE.</b> A yard boundary is already a CUT edge to
    /// the grass layer's edge band (<see cref="StPetersGrass.ClearanceDistanceMetres"/> walks
    /// <c>StPetersYards</c>), so the wild tufts outside a lawn already thin and step down to short
    /// blades as they approach the mow line. Nothing here re-derives that, and
    /// <c>StPetersLawnTests</c> asserts it rather than restating it.</para>
    ///
    /// <para>Pure and deterministic (rule 5): every value is a function of the authored yard table
    /// and world position, so a rebuild reproduces the same lawns and a second run of the paint pass
    /// writes the same bytes.</para>
    /// </summary>
    public static class StPetersLawns
    {
        /// <summary>The Lawn material's index in the canonical order — <c>SplatE.b</c>. Kept as a
        /// named constant beside the other family ids in <see cref="StPetersStarterSplat"/> rather
        /// than as a literal, because the index IS the channel and a wrong one paints a reef bed on
        /// somebody's front garden.</summary>
        public const int Lawn = 18;

        /// <summary>
        /// The painted channel value for a style — <b>how well kept, and how much lawn there is,
        /// which here are the same number.</b>
        ///
        /// <para>Striped and Kept sit near the top of the ladder because that is where the material
        /// is worth looking at and where the wild band is fully displaced. Rough sits near the middle
        /// ON PURPOSE: at 0.55 the shader keeps 45% of the height-derived grass band, so the meadow
        /// grows up through it. Reading that as "a bug in the weighting" and pushing it to 1.0 would
        /// give Ginny the tidiest lawn on the island.</para>
        /// </summary>
        public static float IntensityFor(MownStyle style) => style switch
        {
            MownStyle.Striped => 1.00f,
            MownStyle.Kept => 0.88f,
            _ => 0.55f,
        };

        /// <summary>How far (m) a lawn fades out at its own boundary.
        ///
        /// <para><b>Not zero, and not wide.</b> A mow line is a CUT — the crispest edge on the
        /// island — so this is barely over one splat texel (the maps are 2 px/m). It exists only to
        /// stop the boundary stair-stepping along the polygon's diagonal edges; any wider and the
        /// thing that makes a lawn read as mown, its hard edge, turns into a gradient.</para></summary>
        public const float MowLineFeatherMetres = 0.75f;

        /// <summary>
        /// The coverage map for one mown style: 1 inside those yards, easing to 0 across
        /// <see cref="MowLineFeatherMetres"/> at their boundary, 0 everywhere else.
        ///
        /// <para>One map per style rather than one map carrying values, because
        /// <c>TerrainSplatBrush.PaintField</c> multiplies its target by the coverage — so a single
        /// map would make the feather change the LADDER as well as the weight, and every lawn would
        /// grow a ring of neglect around its own edge.</para>
        /// </summary>
        public static float[] CoverageFor(MownStyle style, int width, int height,
                                          Vector2 worldMin, Vector2 worldSize)
        {
            var map = new float[Mathf.Max(0, width) * Mathf.Max(0, height)];
            if (width <= 0 || height <= 0) return map;

            var yards = StPetersYards.Yards;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2(worldMin.x + (x + 0.5f) / width * worldSize.x,
                                    worldMin.y + (y + 0.5f) / height * worldSize.y);

                float best = 0f;
                for (int i = 0; i < yards.Count; i++)
                {
                    var yard = yards[i];
                    if (yard.Mown != style || !yard.Polygon.IsValid) continue;

                    // The bounding box first — a texel out on the meadow costs four compares per row
                    // and never walks a polygon's edges. Same move IsInsideAYard makes.
                    var box = yard.Polygon.Bounds;
                    if (p.x < box.xMin - MowLineFeatherMetres || p.x > box.xMax + MowLineFeatherMetres ||
                        p.y < box.yMin - MowLineFeatherMetres || p.y > box.yMax + MowLineFeatherMetres)
                        continue;

                    if (!yard.Polygon.Contains(p)) continue;
                    float inset = yard.Polygon.DistanceToEdge(p);
                    float k = MowLineFeatherMetres <= 0f
                        ? 1f
                        : Mathf.Clamp01(inset / MowLineFeatherMetres);
                    if (k > best) best = k;
                }
                map[y * width + x] = best;
            }
            return map;
        }

        /// <summary>Every style that actually appears in the yard table, so the paint pass makes one
        /// call per style that exists rather than one per enum value.</summary>
        public static MownStyle[] StylesInUse()
        {
            var seen = new System.Collections.Generic.List<MownStyle>();
            // Ordered worst-kept first, so an overlapping pair (which the yard tests forbid, but a
            // future author might create) resolves to the BETTER lawn rather than to whichever row
            // happened to be last.
            foreach (MownStyle style in new[] { MownStyle.Rough, MownStyle.Kept, MownStyle.Striped })
                foreach (var yard in StPetersYards.Yards)
                    if (yard.Mown == style) { seen.Add(style); break; }
            return seen.ToArray();
        }

        /// <summary>
        /// Paint every lawn into the splat layers.
        ///
        /// <para><b>⚠ Called BEFORE the walked paths, deliberately</b>, for the reason the shore
        /// bands are: an exclusive stroke lerps its own channel from whatever is beneath, so a track
        /// crossing a corner of somebody's yard has to land on top of the lawn and still read as a
        /// track. Painting the lawn last would rub the path out.</para>
        ///
        /// <para><b>⚠ IDEMPOTENT, like the rest of the pass.</b> The coverage maps are re-derived
        /// from the yard table every run, so a second run writes the same bytes. That is not free —
        /// see the generated-pass note on <see cref="StPetersStarterSplat.PaintInto"/> — it holds
        /// because the whole pass clears the layers first.</para>
        /// </summary>
        public static void PaintInto(Color[][] layers, int width, int height,
                                     Vector2 worldMin, Vector2 worldSize)
        {
            foreach (MownStyle style in StylesInUse())
            {
                float[] coverage = CoverageFor(style, width, height, worldMin, worldSize);
                TerrainSplatBrush.PaintField(layers, width, height,
                    Lawn, IntensityFor(style), coverage, exclusive: true);
            }
        }
    }
}
#endif
