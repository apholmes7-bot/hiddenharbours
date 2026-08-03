using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// THE FOUR THINGS THE SCHOOL SIM ASKS THE WORLD (ADR 0025 S3) — the world seed and region it is
    /// fishing, how deep the water is somewhere, what the weather was doing at a given moment, and what
    /// season it was. Nothing else: everything else about a school is hashed out of those
    /// (<see cref="FishSchoolMath"/>).
    ///
    /// <para><b>Why an interface rather than reading <see cref="GameServices"/> directly.</b> Every one of
    /// these is a live service read, and three of the four are <i>the</i> determinism inputs — so this is
    /// the seam that lets the whole sim be exercised in EditMode with no scene, no clock, no terrain and
    /// no environment service, by handing it a fake world and asserting the schools come out identical.
    /// The production implementation is <see cref="LiveFishSchoolWorld"/>, which is the only place in the
    /// sim that touches a service at all.</para>
    ///
    /// <para><b>Everything is asked AT A TIME, never "now".</b> The sim decides a school from the state of
    /// the world at the moment that school formed, and the school then stands for its whole window. If
    /// these read "now" instead, a school would blink in and out as the tide and the wind wandered across
    /// a threshold mid-window — and the fish finder would faithfully draw the blinking. Recomputed from
    /// <c>(worldSeed, gameTime)</c> is only meaningful if the time is the one being reasoned about
    /// (rule 5).</para>
    /// </summary>
    public interface IFishSchoolWorld
    {
        /// <summary>The world seed — the only entropy in the whole sim (rule 5: no hidden randomness).</summary>
        int WorldSeed { get; }

        /// <summary>The stable id of the region being fished. Folded into every school's hash so two
        /// regions never mirror each other's shoals, and read as the species pool's region gate.</summary>
        string RegionId { get; }

        /// <summary>How deep the water is (m) at a world position at a given game time — the LOCATION gate
        /// (fish are not on the beach) and the scale the school's depth is a fraction of. Where no
        /// bathymetry is authored the implementation reports the open-water fallback rather than 0, the
        /// same "no height map means open water" posture the rest of the module takes.</summary>
        float WaterColumnAt(Vector2 worldPos, double gameSeconds);

        /// <summary>The continuous sea state (0 glass .. 1 storm) at a given game time — the WEATHER
        /// gate, and the term that pushes a school deeper in a blow.</summary>
        float SeaState01At(double gameSeconds);

        /// <summary>The season at a given game time — the DATE gate (its appearance multiplier) and the
        /// species filter (each fish's own authored season window).</summary>
        Season SeasonAt(double gameSeconds);
    }

    /// <summary>
    /// The production <see cref="IFishSchoolWorld"/>: the school sim's four questions answered off the
    /// live Core services (<see cref="GameServices"/>) and the owner's <see cref="GameConfig"/>. The ONLY
    /// part of the school sim that touches a service — everything else is pure maths over what this
    /// returns, which is what keeps the sim testable and deterministic.
    ///
    /// <para><b>Absent services degrade, they never throw.</b> No environment service → a flat calm and
    /// the tide at datum; no tidal terrain → open water at
    /// <see cref="FishSchoolSettings.OpenWaterColumnMetres"/>; no config → the shipped defaults. The same
    /// gate-off posture <c>FishingController</c> already takes for bathymetry and weather: a missing
    /// subsystem means "as if it said nothing", never a refusal to run.</para>
    /// </summary>
    public sealed class LiveFishSchoolWorld : IFishSchoolWorld
    {
        private readonly GameConfig _config;
        private readonly string _fallbackRegionId;

        /// <param name="config">The owner's tuning (may be null — defaults are used).</param>
        /// <param name="fallbackRegionId">The region to fish when no region has reported itself into
        /// <see cref="GameServices.CurrentRegionId"/> (EditMode, pre-boot, a bare rig) — the authored
        /// fallback, exactly as <c>FishingController.EffectiveRegionId</c> resolves it.</param>
        public LiveFishSchoolWorld(GameConfig config, string fallbackRegionId)
        {
            _config = config;
            _fallbackRegionId = fallbackRegionId;
        }

        /// <inheritdoc/>
        public int WorldSeed
        {
            get
            {
                IEnvironmentService env = Live(GameServices.Environment);
                return env != null ? env.WorldSeed : 0;
            }
        }

        /// <inheritdoc/>
        public string RegionId
            => string.IsNullOrEmpty(GameServices.CurrentRegionId)
                ? _fallbackRegionId
                : GameServices.CurrentRegionId;

        /// <inheritdoc/>
        public float WaterColumnAt(Vector2 worldPos, double gameSeconds)
        {
            IEnvironmentService env = Live(GameServices.Environment);
            ITidalTerrain terrain = Live(GameServices.TidalTerrain);
            if (env == null || terrain == null) return Mathf.Max(0.0001f, Settings.OpenWaterColumnMetres);
            return TidalExposure.WaterDepth(env.WaterLevelAt(gameSeconds), terrain.ElevationAt(worldPos));
        }

        /// <inheritdoc/>
        public float SeaState01At(double gameSeconds)
        {
            IEnvironmentService env = Live(GameServices.Environment);
            return env != null ? env.SeaState01At(gameSeconds) : 0f;   // no service → a flat calm
        }

        /// <inheritdoc/>
        public Season SeasonAt(double gameSeconds)
        {
            // Derived from the clock's own arithmetic (GameClock.Season) rather than read off the live
            // clock, because the sim asks about the moment a school FORMED, not about now — and IGameClock
            // exposes only "now". Both roads are the same expression over (SecondsPerDay, DaysPerSeason).
            float secondsPerDay = _config != null ? _config.SecondsPerDay : GameConfig.DefaultSecondsPerDay;
            int daysPerSeason = _config != null ? Mathf.Max(1, _config.DaysPerSeason) : 28;
            if (secondsPerDay <= 0f) return Season.EarlySpring;

            long totalDays = (long)System.Math.Floor(gameSeconds / secondsPerDay);
            if (totalDays < 0) totalDays = 0;
            return (Season)(int)(totalDays / daysPerSeason % 4);
        }

        /// <summary>The owner's school tuning, resolved on every read so dragging a slider in the
        /// GameConfig inspector during play moves the fish live — the <see cref="GameServices.WaveField"/>
        /// discipline, including the <c>Config != null</c> rule (never <c>?.</c>/<c>??</c> on a
        /// <c>UnityEngine.Object</c>).</summary>
        public FishSchoolSettings Settings
            => _config != null ? _config.FishSchools : FishSchoolSettings.Default;

        /// <summary>The owner's depth-band thresholds — the school sim shares the depth drop's bands
        /// rather than owning a second set (see <see cref="FishSchoolMath.DepthMatch01"/>).</summary>
        public DepthDropSettings DepthSettings
            => _config != null ? _config.DepthDrop : DepthDropSettings.Default;

        /// <summary>In-game seconds per in-game hour — the frame the school windows are authored in.</summary>
        public double SecondsPerHour
            => _config != null ? _config.SecondsPerHour : GameConfig.DefaultSecondsPerDay / 24.0;

        /// <summary>
        /// A service reference that is safe to use, or null. ⚠️ NEVER <c>service ?? fallback</c> or
        /// <c>service?.X</c> here: these are interface-typed references to MONOBEHAVIOURS, and both
        /// null-propagating operators bypass <c>UnityEngine.Object</c>'s overloaded <c>==</c>. A destroyed
        /// producer would read as live and every call through it would throw
        /// <c>MissingReferenceException</c> — compile-clean, runtime-red. Ask the Unity way first
        /// (<see cref="GameServices.FishSchools"/> makes the same check for the same reason).
        /// </summary>
        private static T Live<T>(T service) where T : class
            => service is UnityEngine.Object o && o == null ? null : service;
    }
}
