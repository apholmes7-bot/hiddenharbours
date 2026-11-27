using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// A rod object made from parts: the rod part it is, and the line and reel it comes with. A held rod
    /// that answers this is fished with its player's own fit (<see cref="IRodFitting"/>); one that does
    /// not is fished with its whole-rod profile, exactly as before parts existed.
    /// </summary>
    public interface IRodPartsEquipment
    {
        /// <summary>The key the player's fit is held under: the rod's tool id (<c>tool.rod_deep</c>).</summary>
        string RodKey { get; }
        RodBlankDef RodBlank { get; }
        FishingLineDef DefaultLine { get; }
        FishingReelDef DefaultReel { get; }
    }

    /// <summary>Raised when a player changes what is on one of his rods.</summary>
    public readonly struct RodFitChanged
    {
        public readonly string PlayerKey;
        public readonly string RodKey;

        public RodFitChanged(string playerKey, string rodKey)
        {
            PlayerKey = playerKey;
            RodKey = rodKey;
        }
    }

    /// <summary>
    /// <b>One player's fit</b>: which line and reel are on which of his rods. A rod with nothing fitted
    /// fishes with its own default parts. Session-only (see <see cref="RodFittingBook"/>).
    /// </summary>
    public interface IRodFitting
    {
        string PlayerKey { get; }

        /// <summary>The parts fitted to this rod. Either may be null: that part is the rod's default.
        /// False when nothing is fitted.</summary>
        bool TryGetFit(string rodKey, out FishingLineDef line, out FishingReelDef reel);

        /// <summary>Put this line and reel on the rod (null keeps that part at the rod's default; both null
        /// clears the fit). False, and nothing changes, for a part that is not sound or with no game.</summary>
        bool Fit(string rodKey, FishingLineDef line, FishingReelDef reel);

        /// <summary>Back to the rod's default parts. False when nothing was fitted.</summary>
        bool ClearFit(string rodKey);
    }

    /// <summary>Each player's own fit, by player key (co-op: one per player).</summary>
    public interface IRodFittingBook
    {
        IRodFitting For(string playerKey);
    }

    /// <summary>
    /// <b>Rods from parts</b> (owner, 2026-11-25): put a rod part, a line and a reel together into the one
    /// <see cref="RodFishingProfile"/> the fishing code reads. Every number comes from exactly one part, so
    /// a shipped whole rod and its pre-fitted parts give the same profile bit for bit:
    /// <list type="bullet">
    /// <item>rod: cast reach, bite timing, take window, its lift, the old fight's ends;</item>
    /// <item>line: length, strength, which small fish it puts off;</item>
    /// <item>reel: reel-up speed;</item>
    /// <item>fight help = the rod's lift + the line's ease + the reel's ease (the skill adds its own after).</item>
    /// </list>
    /// Pure: no state, no allocation, nothing rolls.
    /// </summary>
    public static class RodFit
    {
        public const string RodIdPrefix = "rod.";
        public const string LineIdPrefix = "fishing_line.";
        public const string ReelIdPrefix = "reel.";

        /// <summary>The three parts as one profile. Callers check <see cref="RodFishingProfile.IsValid"/>.</summary>
        public static RodFishingProfile Compose(RodBlankDef rod, FishingLineDef line, FishingReelDef reel)
        {
            if (rod == null || line == null || reel == null)
                throw new ArgumentNullException(rod == null ? nameof(rod) : line == null ? nameof(line) : nameof(reel));

            return new RodFishingProfile
            {
                CastMetres = rod.CastMetres,
                LineMetres = line.LineMetres,
                ReelUpMetresPerSecond = reel.ReelUpMetresPerSecond,
                MinBiteDelaySeconds = rod.MinBiteDelaySeconds,
                MaxBiteDelaySeconds = rod.MaxBiteDelaySeconds,
                AutoHookSeconds = rod.AutoHookSeconds,
                TakeWindowMultiplier = rod.TakeWindowMultiplier,
                LineStrengthMultiplier = line.Strength,
                LandingMultiplier = rod.LandingMultiplier + line.FightEase + reel.FightEase,
                TensionRiseEasy = rod.TensionRiseEasy,
                TensionRiseHard = rod.TensionRiseHard,
                TensionFallEasy = rod.TensionFallEasy,
                TensionFallHard = rod.TensionFallHard,
                LandingEasy = rod.LandingEasy,
                LandingHard = rod.LandingHard,
                SurgeIntervalEasy = rod.SurgeIntervalEasy,
                SurgeIntervalHard = rod.SurgeIntervalHard,
                SurgeDurationSeconds = rod.SurgeDurationSeconds,
                SurgePressureEasy = rod.SurgePressureEasy,
                SurgePressureHard = rod.SurgePressureHard,
                // A line that puts nothing off reads (0, 0), the same as a whole rod authored before parts.
                SmallFishShyBelowKg = PutsOff(line) ? line.ShyBelowKg : 0f,
                SmallFishShyDamp01 = PutsOff(line) ? line.ShyDamp01 : 0f,
            };
        }

        private static bool PutsOff(FishingLineDef line) => line.ShyBelowKg > 0f && line.ShyDamp01 < 1f;

        /// <summary>
        /// How much a species still bites on a line that puts small fish off: 1 for a fish whose top weight
        /// is at or over the line's mark (or a line with no mark), down to <paramref name="damp01"/> for the
        /// very smallest, in a straight line between. From the species' own data only.
        /// </summary>
        public static float ShyFactor(float shyBelowKg, float damp01, float speciesMaxWeightKg)
        {
            if (!(shyBelowKg > 0f) || !(damp01 < 1f)) return 1f;
            if (speciesMaxWeightKg >= shyBelowKg) return 1f;
            float t = Mathf.Clamp01(Mathf.Max(0f, speciesMaxWeightKg) / shyBelowKg);
            float d = Mathf.Clamp01(damp01);
            return d + (1f - d) * t;
        }

        /// <summary><see cref="ShyFactor(float,float,float)"/> for the line in a captured profile.</summary>
        public static float ShyFactor(in RodFishingProfile rod, float speciesMaxWeightKg)
            => ShyFactor(rod.SmallFishShyBelowKg, rod.SmallFishShyDamp01, speciesMaxWeightKg);

        /// <summary>This player's rod: the rod part with his fitted line and reel, or its defaults where he
        /// has fitted nothing. False when a part is missing or the result is not sound.</summary>
        public static bool TryFitted(IRodPartsEquipment rod, IRodFitting fitting, out RodFishingProfile profile)
        {
            profile = RodFishingProfile.Default;
            if (rod == null || rod.RodBlank == null) return false;

            FishingLineDef line = rod.DefaultLine;
            FishingReelDef reel = rod.DefaultReel;
            if (fitting != null && fitting.TryGetFit(rod.RodKey, out FishingLineDef fittedLine, out FishingReelDef fittedReel))
            {
                if (fittedLine != null) line = fittedLine;
                if (fittedReel != null) reel = fittedReel;
            }
            if (line == null || reel == null) return false;

            RodFishingProfile composed = Compose(rod.RodBlank, line, reel);
            if (!composed.IsValid) return false;
            profile = composed;
            return true;
        }

        /// <summary>
        /// The rod in these hands as this player has it: a rod made from parts with his fit, otherwise the
        /// whole rod's own profile (<see cref="CarriedRod.TryRead(ICarrier, out RodFishingProfile)"/>'s
        /// rule). False with no rod in hand.
        /// </summary>
        public static bool TryReadHeld(ICarrier hands, IRodFitting fitting, out RodFishingProfile profile)
        {
            CarriedItem.Both(hands, out ICarriable first, out ICarriable second);
            if (TryReadOne(first, fitting, out profile)) return true;
            return TryReadOne(second, fitting, out profile);
        }

        private static bool TryReadOne(ICarriable item, IRodFitting fitting, out RodFishingProfile profile)
        {
            if (item is IRodFishingEquipment rod && rod.IsFishingRod)
            {
                if (item is IRodPartsEquipment parts && TryFitted(parts, fitting, out profile)) return true;
                profile = rod.RodFishing;
                if (profile.IsValid) return true;
            }
            profile = RodFishingProfile.Default;
            return false;
        }
    }

    /// <summary>One player's fit, held for the game being played.</summary>
    public sealed class RodFitting : IRodFitting
    {
        private readonly Dictionary<string, (FishingLineDef line, FishingReelDef reel)> _rods =
            new Dictionary<string, (FishingLineDef, FishingReelDef)>(StringComparer.Ordinal);
        private readonly bool _accepts;

        public string PlayerKey { get; }

        /// <param name="accepts">False for the no-game fitting, which holds nothing.</param>
        public RodFitting(string playerKey, bool accepts = true)
        {
            PlayerKey = playerKey ?? "";
            _accepts = accepts;
        }

        public bool TryGetFit(string rodKey, out FishingLineDef line, out FishingReelDef reel)
        {
            line = null;
            reel = null;
            if (string.IsNullOrEmpty(rodKey) || !_rods.TryGetValue(rodKey, out var fit)) return false;
            line = fit.line;
            reel = fit.reel;
            return true;
        }

        public bool Fit(string rodKey, FishingLineDef line, FishingReelDef reel)
        {
            if (!_accepts || string.IsNullOrEmpty(rodKey)) return false;
            if (line != null && line.Problem() != null) return false;
            if (reel != null && reel.Problem() != null) return false;
            if (line == null && reel == null) return ClearFit(rodKey);

            _rods[rodKey] = (line, reel);
            EventBus.Publish(new RodFitChanged(PlayerKey, rodKey));
            return true;
        }

        public bool ClearFit(string rodKey)
        {
            if (string.IsNullOrEmpty(rodKey) || !_rods.Remove(rodKey)) return false;
            EventBus.Publish(new RodFitChanged(PlayerKey, rodKey));
            return true;
        }
    }

    /// <summary>
    /// <b>The rod fittings of the game being played</b>, one <see cref="RodFitting"/> per player key.
    ///
    /// <para><b>Session-only, on purpose,</b> like <see cref="RodSkillBook"/>: nothing is written to the
    /// save (the saved rows are in the design, "Rods from parts"). Bound to the live save object, so a load
    /// or a new game starts with every rod on its default parts. With no game, nothing can be fitted.</para>
    /// FLAG lead-architect: new Core contract (rod fitting).
    /// </summary>
    public sealed class RodFittingBook : IRodFittingBook
    {
        private readonly Func<object> _session;
        private readonly Dictionary<string, RodFitting> _players = new Dictionary<string, RodFitting>(StringComparer.Ordinal);
        private readonly RodFitting _noGame = new RodFitting(null, accepts: false);
        private object _bound;

        /// <param name="session">The current game's identity (default: the live save object).</param>
        public RodFittingBook(Func<object> session = null)
        {
            _session = session ?? (() => GameServices.Save?.Current);
        }

        public IRodFitting For(string playerKey)
        {
            object session = _session();
            if (session == null) return _noGame;
            if (!ReferenceEquals(session, _bound))
            {
                _players.Clear();
                _bound = session;
            }

            string key = string.IsNullOrEmpty(playerKey) ? RodSkillBook.LocalPlayerKey : playerKey;
            if (!_players.TryGetValue(key, out RodFitting fitting))
            {
                fitting = new RodFitting(key);
                _players.Add(key, fitting);
            }
            return fitting;
        }
    }
}
