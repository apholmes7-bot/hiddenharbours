using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The chart's SURVEY: a depth field sampled from the real painted seabed, banded into the rig's
    /// shading and baked once per (region × palette) — the C# answer to <c>navRig.js:211 chartBase</c>.
    ///
    /// <para><b>What replaced the rig's world.</b> <c>navRig.js:126-175</c> ships a hand-authored
    /// fictional coastline (Ironbound, the Sunkers, the Drownded flats) with its own distance-to-land
    /// depth model. None of that is portable content: this chart must show the water the player is
    /// actually sailing, or it is decoration. So the polygons are gone and the field is sampled from
    /// <see cref="ITidalTerrain.ElevationAt"/> — the SAME height map the water shader renders, the tide
    /// exposes and the depth sounder reads. One height map, one truth; the chart cannot disagree with
    /// the sounder because they are reading the same number.</para>
    ///
    /// <para><b>DATUM depth, not tide-of-the-moment</b> (the §4 call). <see cref="ITidalTerrain"/>
    /// returns metres above CHART DATUM, so depth below datum is simply the negated elevation and the
    /// survey is independent of time. Three reasons that is the right choice and not the lazy one:
    /// a real chart is a stable reference you plan a passage against, and one whose shading breathed
    /// with the tide would be unreadable; the tide is already reported separately, as a height and a
    /// set, exactly as it is on a real plotter; and a time-varying base would re-bake the whole survey
    /// continuously, which is a rule-7 disaster for a picture nobody asked to change. Live
    /// depth-under-the-hull is the SOUNDER's job and stays live there.</para>
    ///
    /// <para><b>Never re-baked while sailing.</b> The bake is a pure function of the region rect, the
    /// palette and the terrain, so panning, ranging, turning and the tide all leave it alone. It is
    /// rebuilt only when the region changes, the panel lights change, or the terrain service is
    /// swapped.</para>
    /// </summary>
    public sealed class NavChartSource
    {
        /// <summary>Metres of world per baked chart texel. The painted seabed is authored at
        /// <c>RegionDef.SeabedPixelsPerMetre = 2</c> (0.5 m), so a metre-per-texel survey is already
        /// coarser than its source and finer than any range the plotter shows — at the closest range
        /// (0.05 NM ≈ 93 m across the glass) one texel is still under half a screen pixel.</summary>
        public const float MetresPerTexel = 1f;

        /// <summary>Metres between terrain SAMPLES. Two texels per sample: the seabed is painted in
        /// broad strokes and a chart bands it anyway, so sampling every metre would quadruple the
        /// bake's cost to draw the same bands.</summary>
        public const float MetresPerSample = 2f;

        // The rig's shading ladder (navRig.js:208 bandOf + :217): five water bands from shoal to deep,
        // with anything shallower than half a metre drawn as drying ground rather than water. The
        // thresholds are METRES and transfer to this world unchanged — they already match the game's
        // own depth ladder (0.6 / 1.6 / 6 m).
        private static readonly float[] BandMetres = { 2f, 5f, 10f, 18f };

        /// <summary>Depth (metres below chart datum) at which ground is drawn as drying flat rather
        /// than as water (navRig.js:217 <c>d &lt; 0.5</c>).</summary>
        public const float DryingDepthMetres = 0.5f;

        private Rect _bounds;
        private bool _night;
        private object _terrainKey;          // identity of the service the bake was taken from
        private int _w, _h;
        private Color32[] _pixels;
        private float[] _depth;              // datum depth per texel, metres; negative = above datum

        /// <summary>Baked width in texels (0 until a successful bake).</summary>
        public int Width => _w;

        /// <summary>Baked height in texels.</summary>
        public int Height => _h;

        /// <summary>The world rect the bake covers, in metres.</summary>
        public Rect Bounds => _bounds;

        /// <summary>True once there is a usable survey to draw.</summary>
        public bool HasChart => _pixels != null && _w > 0 && _h > 0;

        /// <summary>The banded chart colours, row-major from the rect's BOTTOM-LEFT (world +Y up).</summary>
        public Color32[] Pixels => _pixels;

        /// <summary>
        /// Rebuild the survey if anything it depends on has changed, and report whether a bake actually
        /// happened (the seam the repaint-cost test and the "never re-bakes while sailing" guard both
        /// assert on). Cheap and safe to call every repaint: the common case is four comparisons.
        /// </summary>
        public bool EnsureBaked(Rect bounds, bool night, ITidalTerrain terrain)
        {
            if (terrain == null || bounds.width <= 0f || bounds.height <= 0f)
            {
                // No survey to be had. Drop what we have rather than draw another region's seabed
                // under this one's boat — an honestly blank chart beats a confidently wrong one.
                if (_pixels != null) { _pixels = null; _depth = null; _w = _h = 0; }
                return false;
            }
            if (HasChart && _night == night && _bounds == bounds && ReferenceEquals(_terrainKey, terrain))
                return false;

            Bake(bounds, night, terrain);
            return true;
        }

        private void Bake(Rect bounds, bool night, ITidalTerrain terrain)
        {
            _bounds = bounds;
            _night = night;
            _terrainKey = terrain;
            _w = Mathf.Max(1, Mathf.RoundToInt(bounds.width / MetresPerTexel));
            _h = Mathf.Max(1, Mathf.RoundToInt(bounds.height / MetresPerTexel));

            if (_pixels == null || _pixels.Length != _w * _h)
            {
                _pixels = new Color32[_w * _h];
                _depth = new float[_w * _h];
            }

            int step = Mathf.Max(1, Mathf.RoundToInt(MetresPerSample / MetresPerTexel));
            NavPalette p = NavPalette.For(night);

            for (int y = 0; y < _h; y += step)
            {
                for (int x = 0; x < _w; x += step)
                {
                    // Sample at the CENTRE of the block this sample fills, so a coarse survey is not
                    // biased half a block toward the rect's origin.
                    float wx = bounds.xMin + (x + step * 0.5f) * MetresPerTexel;
                    float wy = bounds.yMin + (y + step * 0.5f) * MetresPerTexel;
                    float elevation = terrain.ElevationAt(new Vector2(wx, wy));
                    // ITidalTerrain measures metres ABOVE chart datum; a chart measures depth BELOW it.
                    float depth = -elevation;
                    Color32 c = ColourFor(depth, in p);

                    int xEnd = Mathf.Min(x + step, _w), yEnd = Mathf.Min(y + step, _h);
                    for (int by = y; by < yEnd; by++)
                    {
                        int row = by * _w;
                        for (int bx = x; bx < xEnd; bx++)
                        {
                            _pixels[row + bx] = c;
                            _depth[row + bx] = depth;
                        }
                    }
                }
            }
        }

        private static Color32 ColourFor(float depthMetres, in NavPalette p)
        {
            if (depthMetres <= 0f) return p.Land;                       // at or above datum: ground
            if (depthMetres < DryingDepthMetres) return p.Dry;          // covers and uncovers
            return p.Water[BandOf(depthMetres)];
        }

        /// <summary>Which shading band a depth falls in (navRig.js:208). 0 = shoalest, 4 = deepest.</summary>
        public static int BandOf(float depthMetres)
        {
            for (int i = 0; i < BandMetres.Length; i++)
                if (depthMetres < BandMetres[i]) return i;
            return BandMetres.Length;
        }

        /// <summary>
        /// Datum depth in metres at a world position, or <see cref="float.NaN"/> outside the survey.
        /// The chart's own read — used by the depth-ahead profile strip, which must agree with the
        /// shading under it because both come from this one array.
        /// </summary>
        public float DepthAt(Vector2 world)
        {
            if (!HasChart) return float.NaN;
            int x = Mathf.FloorToInt((world.x - _bounds.xMin) / MetresPerTexel);
            int y = Mathf.FloorToInt((world.y - _bounds.yMin) / MetresPerTexel);
            if (x < 0 || y < 0 || x >= _w || y >= _h) return float.NaN;
            return _depth[y * _w + x];
        }

        /// <summary>
        /// The banded colour at a world position, or <paramref name="offChart"/> beyond the survey's
        /// edge. Point-sampled on purpose — the rig draws its base with
        /// <c>imageSmoothingEnabled=false</c>, and a chart's depth areas are hard-edged by convention.
        /// </summary>
        public Color32 ColourAt(Vector2 world, Color32 offChart)
        {
            if (!HasChart) return offChart;
            int x = Mathf.FloorToInt((world.x - _bounds.xMin) / MetresPerTexel);
            int y = Mathf.FloorToInt((world.y - _bounds.yMin) / MetresPerTexel);
            if (x < 0 || y < 0 || x >= _w || y >= _h) return offChart;
            return _pixels[y * _w + x];
        }

        /// <summary>Is this world position dry ground on the chart (at or above datum)?</summary>
        public bool IsLand(Vector2 world)
        {
            float d = DepthAt(world);
            return !float.IsNaN(d) && d <= 0f;
        }
    }
}
