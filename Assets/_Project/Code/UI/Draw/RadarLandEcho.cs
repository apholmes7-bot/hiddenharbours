using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// THE COAST, AS A RADAR SEES IT — a polar scan of the terrain height map that turns the shoreline
    /// into echoes (ADR 0025 S5). The C# answer to <c>radarRig.js:137-140</c>, whose four hand-authored
    /// <c>kind:'land'</c> targets are a fictional headland: none of that is portable content, so the
    /// coast is sampled from <see cref="ITidalTerrain.ElevationAt"/> — the SAME height map the water
    /// shader renders, the tide exposes, the sounder reads and the chart surveys. One height map, one
    /// truth.
    ///
    /// <para><b>⚠ THE WATERLINE IS THE TIDE OF THE MOMENT, NOT CHART DATUM — and that is the opposite
    /// of what <see cref="NavChartSource"/> chose, on purpose.</b> The chart is a SURVEY: a stable
    /// reference you plan a passage against, so its depths are datum and never breathe. The radar is a
    /// SENSOR: it reports the water that is there right now. So a bar that dries at low water paints as
    /// a solid coastal return, and the same bar an hour after the flood makes is simply gone from the
    /// scope — because the sea really has covered it and a radar really would stop seeing it. The two
    /// instruments disagreeing about where the land is, in exactly this way, is the correct behaviour
    /// for both; a skipper reading the chart against the scope is reading a plan against a moment, which
    /// is the whole skill the tide pillar (P1) is about.</para>
    ///
    /// <para><b>Radar shadow is free and honest.</b> Each ray marches outward and STOPS at its first
    /// land crossing, because that is what a radar does: energy that hits a headland does not carry on
    /// to paint the harbour behind it. This bounds the scan (one echo per azimuth, at most) and gets the
    /// physics right for nothing — a cove behind a point genuinely goes dark until you open it.</para>
    ///
    /// <para><b>Never re-scanned per frame</b> (rule 7). The scan is a pure function of (own ship ×
    /// range × water level × terrain), so it is rebuilt only when the boat has moved a meaningful
    /// fraction of the range, the rung changed, the tide has moved past
    /// <see cref="RadarSettings.LandRescanTideMetres"/>, or the terrain service was swapped. Everything
    /// is written into one reused list; a steady frame costs four comparisons.</para>
    ///
    /// <para><b>Deterministic.</b> Nothing here is saved and nothing is random: the same seed at the same
    /// instant gives the same water level over the same height map, hence the same coast.</para>
    /// </summary>
    public sealed class RadarLandEcho
    {
        /// <summary>How many samples each ray takes between own ship and the rim. Fixed rather than
        /// tunable: it trades against <see cref="RadarSettings.LandScanAzimuths"/> for the same budget,
        /// and one dial for a two-dial budget is the one an owner can actually reason about.</summary>
        public const int RangeBins = 64;

        /// <summary>How far the boat may move, as a FRACTION of the current range, before the scan is
        /// stale. A tenth of the range is about a scope pixel's worth of parallax on the coast.</summary>
        public const float RescanMoveFraction = 0.1f;

        /// <summary>Echo size of a coast that is barely awash (<c>radarRig.js:140</c>'s smallest land
        /// lump) and of a full cliff (its largest). Sized by how much of the land stands above the water,
        /// because a bluff really does throw a bigger return than a drying flat.</summary>
        public const float MinLandSize = 1.2f, MaxLandSize = 2.5f;

        /// <summary>Metres of freeboard at which a coast returns <see cref="MaxLandSize"/>.</summary>
        public const float FullReturnHeightMetres = 4f;

        private readonly List<RadarContact> _echoes = new();

        private bool _has;
        private Vector2 _at;
        private float _range;
        private float _water;
        private object _terrainKey;

        /// <summary>The coast as it stands — one contact per azimuth that found land, ordered by
        /// azimuth. Read-only to callers: the list is reused, so a caller that kept it across a bake
        /// would be looking at the next scan.</summary>
        public IReadOnlyList<RadarContact> Echoes => _echoes;

        /// <summary>How many scans have run. The rule-7 guard's evidence: this must stay far below the
        /// host's repaint count while sailing, and must not move at all for a repaint that only turned
        /// the sweep.</summary>
        public int ScanCount { get; private set; }

        /// <summary>True once there is a scan to draw (even an empty one — open water is a result).</summary>
        public bool HasScan => _has;

        /// <summary>
        /// Rescan if anything the coast depends on has changed, and report whether a scan actually ran
        /// (the seam the repaint-cost test asserts on). Cheap and safe to call every repaint: the common
        /// case is four comparisons.
        /// </summary>
        public bool EnsureScanned(Vector2 boat, float rangeMetres, float waterLevel,
                                  ITidalTerrain terrain, in RadarSettings cfg)
        {
            if (terrain == null || rangeMetres <= 0f)
            {
                // No height map — open water everywhere, which is the honest picture rather than the
                // last region's coastline left under this one's boat.
                if (_has || _echoes.Count > 0) { _echoes.Clear(); _has = false; }
                return false;
            }

            if (_has && ReferenceEquals(_terrainKey, terrain)
                && Mathf.Approximately(_range, rangeMetres)
                && Mathf.Abs(waterLevel - _water) < cfg.SafeLandRescanTideMetres
                && (boat - _at).sqrMagnitude
                   < rangeMetres * RescanMoveFraction * (rangeMetres * RescanMoveFraction))
                return false;

            Scan(boat, rangeMetres, waterLevel, terrain, in cfg);
            return true;
        }

        private void Scan(Vector2 boat, float rangeMetres, float waterLevel, ITidalTerrain terrain,
                          in RadarSettings cfg)
        {
            _at = boat;
            _range = rangeMetres;
            _water = waterLevel;
            _terrainKey = terrain;
            _has = true;
            ScanCount++;
            _echoes.Clear();

            int azimuths = cfg.SafeLandScanAzimuths;
            int cap = cfg.SafeMaxLandEchoes;
            float step = rangeMetres / RangeBins;

            for (int a = 0; a < azimuths && _echoes.Count < cap; a++)
            {
                float bearing = 360f * a / azimuths;
                Vector2 dir = NavMath.DirectionFromBearing(bearing);

                for (int i = 1; i <= RangeBins; i++)
                {
                    Vector2 p = boat + dir * (step * i);
                    float elevation = terrain.ElevationAt(p);
                    // ⚠ The waterline of the MOMENT — see the class remarks for why this is deliberately
                    // not the chart's datum.
                    if (elevation < waterLevel) continue;

                    float above = elevation - waterLevel;
                    float size = Mathf.Lerp(MinLandSize, MaxLandSize,
                                            Mathf.Clamp01(above / FullReturnHeightMetres));
                    _echoes.Add(RadarContact.Still(p, RadarEchoKind.Land, size));
                    break;                  // radar shadow: this ray is spent
                }
            }
        }

        /// <summary>Drop the scan (region change / teardown / tests) so the next call rebuilds.</summary>
        public void Forget()
        {
            _echoes.Clear();
            _has = false;
            _terrainKey = null;
        }
    }
}
