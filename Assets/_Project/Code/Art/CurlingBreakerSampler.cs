using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>Everything one frame of the curl reads: the sea the water draws, on its own clock.</summary>
    public readonly struct CurlingBreakerInputs
    {
        public readonly ITidalTerrain Terrain;
        public readonly float WaterLevelMeters;
        /// <summary>The dominant train's breaker contour (<see cref="BreakerMath.ContourFor"/>).</summary>
        public readonly BreakerContour Contour;
        /// <summary>The trains the surf reads — the PUBLISHED field, sampled at
        /// <see cref="TimeSeconds"/> 0 (see <see cref="BreakerMath.SurfAt"/>).</summary>
        public readonly WaveTrains Trains;
        /// <summary>The same trains as the shader receives them, for the surface lift.</summary>
        public readonly PackedWaveField Field;
        public readonly float Gravity;
        public readonly BreakerSettings Breakers;
        /// <summary>The scale the drawn sea runs its wavelengths at (the bore's phase reads it).</summary>
        public readonly float FreqScale;
        public readonly double TimeSeconds;
        /// <summary>The displaced sea the sheet sits on (exaggeration, shore fade, its own scale).</summary>
        public readonly DisplacedSeaState Sea;

        public CurlingBreakerInputs(ITidalTerrain terrain, float waterLevelMeters, in BreakerContour contour,
                                    in WaveTrains trains, in PackedWaveField field, float gravity,
                                    in BreakerSettings breakers, float freqScale, double timeSeconds,
                                    in DisplacedSeaState sea)
        {
            Terrain = terrain;
            WaterLevelMeters = waterLevelMeters;
            Contour = contour;
            Trains = trains;
            Field = field;
            Gravity = gravity;
            Breakers = breakers;
            FreqScale = freqScale;
            TimeSeconds = timeSeconds;
            Sea = sea;
        }
    }

    /// <summary>The water material's surf dials, read where the water reads them — never a second
    /// set of knobs. B2 draws only with the plunging anatomy and the bore beat both up.</summary>
    public readonly struct CurlingBreakerLook
    {
        public readonly float Surf;
        public readonly float Plunge;
        public readonly float Beat;
        public readonly float LipThrow;
        public readonly float LipWidth;

        public CurlingBreakerLook(float surf, float plunge, float beat, float lipThrow, float lipWidth)
        {
            Surf = surf;
            Plunge = Mathf.Clamp01(plunge);
            Beat = beat;
            LipThrow = Mathf.Max(0f, lipThrow);
            LipWidth = lipWidth;
        }

        /// <summary>The flat lip is thrown at the crest's arrival only with the beat fully up; below it
        /// the shader's lip stands still, and a travelling sheet would disagree with it.</summary>
        public bool Active => Surf > 0f && Plunge > 0f && Mathf.Clamp01(Beat) >= 1f;
    }

    /// <summary>One site's curl this frame, in fixed arrays (rule 7: nothing allocated per frame).</summary>
    public sealed class CurlingBreakerPose
    {
        /// <summary>The topology ceiling (<see cref="CurlingBreakerSettings.AlongColumns"/> ≤ 17).</summary>
        public const int MaxColumns = 17;

        /// <summary>Where the bore front crosses each column, metres.</summary>
        public readonly Vector2[] Front = new Vector2[MaxColumns];
        /// <summary>Each column's offset from the site centre along the shore, metres.</summary>
        public readonly float[] Along = new float[MaxColumns];
        public readonly float[] Taper = new float[MaxColumns];
        /// <summary>False where the front or the landing point is dry or not breaking: the column
        /// draws nothing, so the sheet shortens rather than bridging unearned water.</summary>
        public readonly bool[] Valid = new bool[MaxColumns];
        /// <summary>The drawn surface's lift under the front and under the full landing point.</summary>
        public readonly float[] LiftFront = new float[MaxColumns];
        public readonly float[] LiftLand = new float[MaxColumns];

        public int Columns;
        public bool Live;
        public Vector2 Centre;
        public Vector2 Shoreward;
        /// <summary>The signed fraction of a period from the crest (the published bore clock).</summary>
        public float Phase;
        public float Period;
        /// <summary>The event's full throw and this frame's: metres ahead of the front.</summary>
        public float FullThrow;
        public float Throw;
        /// <summary>This frame's curl height above the surface, metres.</summary>
        public float Peak;
        public float Cover;
        /// <summary>0..1: how far the landing lip has whitened toward the foam colour.</summary>
        public float Whiten;

        public void Clear()
        {
            Live = false;
            Columns = 0;
            Phase = Period = FullThrow = Throw = Peak = Cover = Whiten = 0f;
            Centre = Shoreward = Vector2.zero;
        }
    }

    /// <summary>
    /// Samples the picked stretch and lays out its sheet and handoff mask. Pure statics over value
    /// inputs: the same inputs give the same pose, in any call order (rule 5).
    /// </summary>
    public static class CurlingBreakerSampler
    {
        /// <summary>The cross-section ceiling (<see cref="CurlingBreakerSettings.CrossRows"/> ≤ 25).</summary>
        public const int MaxRows = 25;
        public const int MinColumns = 3;
        public const int MinRows = 5;

        public static int ColumnsFor(in CurlingBreakerSettings settings)
            => Mathf.Clamp(settings.AlongColumns, MinColumns, CurlingBreakerPose.MaxColumns);

        public static int RowsFor(in CurlingBreakerSettings settings)
            => Mathf.Clamp(settings.CrossRows, MinRows, MaxRows);

        /// <summary>The surf the water draws at <paramref name="worldPos"/> — the ONE read the curl
        /// takes of the sea.</summary>
        public static SurfState SurfAt(in CurlingBreakerInputs inputs, WaveFetchSample fetch, Vector2 worldPos)
            => BreakerMath.SurfAt(worldPos, inputs.WaterLevelMeters, inputs.Terrain, in inputs.Contour, fetch,
                in inputs.Trains, inputs.Gravity, in inputs.Breakers, inputs.FreqScale, inputs.TimeSeconds);

        /// <summary>A stretch anchor may carry the curl only on working, plunging surf with a bore and
        /// whitewater above the presentation floors.</summary>
        public static bool Eligible(in SurfState surf, in CurlingBreakerSettings settings)
            => surf.IsWorking
               && surf.PlungingWeight01 >= settings.MinPlungingWeight
               && surf.BirthEnergy01 >= settings.MinBore
               && surf.Whitewater01 >= settings.MinWhitewater;

        /// <summary>
        /// Samples one site into <paramref name="pose"/>. Live only when the look is up, the centre
        /// and both ends of the stretch carry eligible surf, the bore clock is inside the event and
        /// the event clears the readable bar; otherwise the pose is cleared and the site yields.
        /// </summary>
        public static void Sample(in CurlingBreakerInputs inputs, WaveFetchSample fetch, Vector2 centre,
                                  float lengthMeters, in CurlingBreakerSettings settings,
                                  in CurlingBreakerLook look, CurlingBreakerPose pose)
        {
            if (pose == null) return;
            pose.Clear();
            if (!settings.Enabled || !look.Active || inputs.Terrain == null || !inputs.Contour.Breaks) return;
            if (inputs.Trains.Count <= 0) return;
            WaveTrain dominant = inputs.Trains.Dominant;
            if (dominant.Amplitude <= 0f) return;

            SurfState mid = SurfAt(in inputs, fetch, centre);
            if (!Eligible(in mid, in settings)) return;
            Vector2 shoreward = mid.ShorewardDirection;
            var alongShore = new Vector2(-shoreward.y, shoreward.x);
            float half = Mathf.Max(0f, lengthMeters) * 0.5f;
            SurfState first = SurfAt(in inputs, fetch, centre - alongShore * half);
            if (!Eligible(in first, in settings)) return;
            SurfState last = SurfAt(in inputs, fetch, centre + alongShore * half);
            if (!Eligible(in last, in settings)) return;

            float period = BreakerMath.PeriodSeconds(in dominant);
            if (period <= 0f) return;
            float q = CurlingBreakerMath.PhaseFraction(mid.BorePhaseDegrees, period);
            CurlStage stage = CurlingBreakerMath.StageAt(q, in settings);
            if (stage.Cover01 <= 0f) return;

            float peak = settings.HeightFraction * mid.StandingHeightMeters;
            float fullThrow = CurlingBreakerMath.EventThrow(mid.StandingHeightMeters, mid.PlungingWeight01,
                look.Plunge, mid.BirthEnergy01, look.LipThrow);
            if (!CurlingBreakerMath.IsReadable(peak, fullThrow, period, in settings)) return;

            // The bore front runs shoreward at √(g·d): before the crest it is still seaward of the
            // stretch, at the crest it is on it, after the crest it has passed.
            float speed = Mathf.Sqrt(Mathf.Max(0f, inputs.Gravity) * Mathf.Max(mid.DepthMeters, BreakerMath.MinDepthMeters));
            float ahead = -q * period * speed;
            float fetchFactor = fetch.ForTrain(inputs.Trains.DominantIndex, dominant.Wavelength);
            int columns = ColumnsFor(in settings);
            for (int c = 0; c < columns; c++)
            {
                float along = Mathf.Lerp(-half, half, c / (float)(columns - 1));
                Vector2 front = centre + alongShore * along - shoreward * ahead;
                Vector2 land = front + shoreward * fullThrow;
                pose.Along[c] = along;
                pose.Front[c] = front;
                pose.Taper[c] = CurlingBreakerMath.Taper01(along, lengthMeters, settings.EndTaperMeters);
                pose.Valid[c] = Breaks(in inputs, front, fetchFactor) && Breaks(in inputs, land, fetchFactor);
                pose.LiftFront[c] = SurfaceLift(in inputs, fetch, front);
                pose.LiftLand[c] = SurfaceLift(in inputs, fetch, land);
            }

            pose.Columns = columns;
            pose.Centre = centre;
            pose.Shoreward = shoreward;
            pose.Phase = q;
            pose.Period = period;
            pose.FullThrow = fullThrow;
            pose.Throw = fullThrow * stage.Throw01;
            pose.Peak = peak * stage.Height01;
            pose.Cover = stage.Cover01;
            pose.Whiten = CurlingBreakerMath.Ramp(settings.Crest, settings.Gone, q);
            pose.Live = true;
        }

        /// <summary>Wet, and inside the breaking band — the water's own gate, on its pixel grid.</summary>
        public static bool Breaks(in CurlingBreakerInputs inputs, Vector2 worldPos, float fetchFactor)
        {
            float depth = inputs.WaterLevelMeters - inputs.Terrain.ElevationAt(WaveFetch.Pixelize(worldPos));
            return depth > 0f && BreakerMath.Breaking01FromContour(depth, in inputs.Contour, fetchFactor) > 0f;
        }

        /// <summary>The displaced surface's lift at a point: the shader twin's height through the ONE
        /// shared exaggeration and shore fade (<see cref="ShoreFadeMath.DisplacedHeight"/>).</summary>
        public static float SurfaceLift(in CurlingBreakerInputs inputs, WaveFetchSample fetch, Vector2 worldPos)
        {
            float height = WaveFieldBridge.ShaderTwinSample(worldPos, in inputs.Field, inputs.Sea.FreqScale, fetch).Height;
            float depth = inputs.WaterLevelMeters - inputs.Terrain.ElevationAt(worldPos);
            return ShoreFadeMath.DisplacedHeight(height, depth, inputs.Sea.ShoreFadeBandMeters, inputs.Sea.Exaggeration);
        }

        /// <summary>The surface lift under <paramref name="aheadMeters"/> ahead of a column's front,
        /// between the lift at the front and at the full landing point.</summary>
        public static float LiftAt(CurlingBreakerPose pose, int column, float aheadMeters)
            => Mathf.Lerp(pose.LiftFront[column], pose.LiftLand[column],
                pose.FullThrow > 0f ? Mathf.Clamp01(aheadMeters / pose.FullThrow) : 0f);

        /// <summary>The alpha a column draws with: present, tapered, and only on earned water.</summary>
        public static float ColumnAlpha(CurlingBreakerPose pose, int column)
            => pose.Valid[column] ? pose.Cover * pose.Taper[column] : 0f;

        /// <summary>
        /// Lays the sheet out in world space on the water's iso depth (the displaced vertex's
        /// <c>z = base + (y − ref)·cos − lift·sin</c>): column-major, <paramref name="rows"/> per column.
        /// uv = (section t, along 0..1, alpha, whiten).
        /// </summary>
        public static Bounds FillSheet(CurlingBreakerPose pose, in WaterIsoDepthFrame frame, int rows,
                                       Vector3[] vertices, Vector4[] uvs)
        {
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = -min;
            int columns = pose.Columns;
            for (int c = 0; c < columns; c++)
            {
                float alpha = ColumnAlpha(pose, c);
                float u = c / (float)(columns - 1);
                for (int r = 0; r < rows; r++)
                {
                    float t = r / (float)(rows - 1);
                    Vector2 section = CurlingBreakerMath.SectionPoint(t, pose.Throw, pose.Peak);
                    Vector2 ground = pose.Front[c] + pose.Shoreward * section.x;
                    float rise = LiftAt(pose, c, section.x) + section.y;
                    var v = new Vector3(ground.x, ground.y + rise,
                        DisplacedWaterMath.HullDepthBias(ground.y, rise, in frame));
                    int i = c * rows + r;
                    vertices[i] = v;
                    uvs[i] = new Vector4(t, u, alpha, pose.Whiten);
                    min = Vector3.Min(min, v);
                    max = Vector3.Max(max, v);
                }
            }
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        /// <summary>
        /// Lays the handoff mask out on the water surface over <see cref="CurlingBreakerMath.HandoffBand"/>:
        /// two rows per column (the band's near and far edge), with the sheet's own column alpha, so the
        /// flat lip and barrel yield exactly where the sheet is drawn.
        /// </summary>
        public static Bounds FillMask(CurlingBreakerPose pose, in WaterIsoDepthFrame frame, float lipWidthMeters,
                                      Vector3[] vertices, Vector4[] uvs)
        {
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = -min;
            Vector2 band = CurlingBreakerMath.HandoffBand(pose.FullThrow, lipWidthMeters);
            int columns = pose.Columns;
            for (int c = 0; c < columns; c++)
            {
                float alpha = ColumnAlpha(pose, c);
                float u = c / (float)(columns - 1);
                for (int r = 0; r < 2; r++)
                {
                    float ahead = r == 0 ? band.x : band.y;
                    Vector2 ground = pose.Front[c] + pose.Shoreward * ahead;
                    float lift = LiftAt(pose, c, ahead);
                    var v = new Vector3(ground.x, ground.y + lift,
                        DisplacedWaterMath.HullDepthBias(ground.y, lift, in frame));
                    int i = c * 2 + r;
                    vertices[i] = v;
                    uvs[i] = new Vector4(r, u, alpha, 0f);
                    min = Vector3.Min(min, v);
                    max = Vector3.Max(max, v);
                }
            }
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }
}
