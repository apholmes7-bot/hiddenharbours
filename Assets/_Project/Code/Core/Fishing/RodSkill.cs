using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>What the player's own hand adds to a rod at his level. All zero at level 0.</summary>
    public readonly struct RodSkillBonus : IEquatable<RodSkillBonus>
    {
        public readonly float CastMetres;
        public readonly float LineStrength;
        public readonly float ReelUpMetresPerSecond;
        public readonly float FightHelp;

        public RodSkillBonus(float castMetres, float lineStrength, float reelUpMetresPerSecond, float fightHelp)
        {
            CastMetres = castMetres;
            LineStrength = lineStrength;
            ReelUpMetresPerSecond = reelUpMetresPerSecond;
            FightHelp = fightHelp;
        }

        public static RodSkillBonus None => default;

        public bool IsNone => CastMetres == 0f && LineStrength == 0f && ReelUpMetresPerSecond == 0f && FightHelp == 0f;

        public bool Equals(RodSkillBonus o) => CastMetres.Equals(o.CastMetres) && LineStrength.Equals(o.LineStrength)
            && ReelUpMetresPerSecond.Equals(o.ReelUpMetresPerSecond) && FightHelp.Equals(o.FightHelp);

        public override bool Equals(object obj) => obj is RodSkillBonus o && Equals(o);

        public override int GetHashCode() => HashCode.Combine(CastMetres, LineStrength, ReelUpMetresPerSecond, FightHelp);
    }

    /// <summary>
    /// <b>The rod skill's arithmetic</b>, pure and engine-light so it is provable headlessly. Every number
    /// comes from a <see cref="RodSkillDef"/>; nothing here is tuning.
    ///
    /// <para><b>The one rule</b> (owner ruling 2026-11-25): effective = rod + skill. Each of the four
    /// numbers the skill touches (cast reach, line strength, reel speed, fight help) is the held rod's own
    /// value PLUS the bonus at the player's level. Additive, never a replacement and never a maximum of
    /// the two, so a better rod always adds on top of a skilled hand and the reverse.</para>
    /// </summary>
    public static class RodSkillMath
    {
        /// <summary>The level <paramref name="experience"/> reaches: the highest row whose requirement it
        /// meets, 0 below the first row or with no Def.</summary>
        public static int LevelFor(int experience, RodSkillDef def)
        {
            if (def == null || def.Levels == null) return 0;
            int level = 0;
            for (int i = 0; i < def.Levels.Length; i++)
            {
                if (experience < def.Levels[i].ExperienceRequired) break;
                level = i + 1;
            }
            return level;
        }

        /// <summary>The whole bonus at <paramref name="level"/> (clamped to the ladder). Level 0 or no Def
        /// adds nothing.</summary>
        public static RodSkillBonus BonusAt(int level, RodSkillDef def)
        {
            if (def == null || def.Levels == null || level <= 0 || def.Levels.Length == 0) return RodSkillBonus.None;
            RodSkillLevel l = def.Levels[Mathf.Min(level, def.Levels.Length) - 1];
            return new RodSkillBonus(l.CastMetres, l.LineStrength, l.ReelUpMetresPerSecond, l.FightHelp);
        }

        /// <summary>Experience for one fish won on a rod, of difficulty <paramref name="difficulty01"/>
        /// (clamped to 0..1; NaN reads as 0). The same fish always gives the same amount.</summary>
        public static int ExperienceFor(float difficulty01, RodSkillDef def)
        {
            if (def == null) return 0;
            float d = float.IsNaN(difficulty01) ? 0f : Mathf.Clamp01(difficulty01);
            return Mathf.Max(0, def.ExperiencePerFish) + Mathf.RoundToInt(Mathf.Max(0, def.ExperienceForHardestFish) * d);
        }

        /// <summary>The held rod's profile with the player's bonus added to cast reach, line strength, reel
        /// speed and landing (fight help). Every other field is the rod's own. No bonus returns the rod
        /// unchanged, bit for bit.</summary>
        public static RodFishingProfile Apply(in RodFishingProfile rod, in RodSkillBonus bonus)
        {
            if (bonus.IsNone) return rod;
            RodFishingProfile p = rod;
            p.CastMetres += bonus.CastMetres;
            p.LineStrengthMultiplier += bonus.LineStrength;
            p.ReelUpMetresPerSecond += bonus.ReelUpMetresPerSecond;
            p.LandingMultiplier += bonus.FightHelp;
            return p;
        }
    }

    /// <summary>
    /// <b>One player's rod-fishing skill.</b> Read and fed through this seam so the fishing code never
    /// learns where it is kept (rule 4). Co-op: there is one of these PER PLAYER, never one for the game.
    /// </summary>
    public interface IRodSkill
    {
        /// <summary>Total experience earned.</summary>
        int Experience { get; }

        /// <summary>The level that experience reaches (0 = a beginner).</summary>
        int Level { get; }

        /// <summary>The top level the ladder has.</summary>
        int MaxLevel { get; }

        /// <summary>What his hand adds to any rod right now.</summary>
        RodSkillBonus Bonus { get; }

        /// <summary>The held rod's numbers with his bonus added (effective = rod + skill).</summary>
        RodFishingProfile Apply(in RodFishingProfile rod);

        /// <summary>A fish was won on a rod. Adds its experience and returns how much was added.</summary>
        int RecordLanded(float difficulty01);
    }

    /// <summary>Every player's rod skill, by player key. Never returns null.</summary>
    public interface IRodSkillBook
    {
        /// <summary>The skill of the player with <paramref name="playerKey"/> (made at zero on first ask).</summary>
        IRodSkill For(string playerKey);
    }

    /// <summary>A player reached a new rod-fishing level. For a Notebook line or an inner-voice beat later;
    /// nothing listens yet.</summary>
    public readonly struct RodSkillLevelReached
    {
        public readonly string PlayerKey;
        public readonly int Level;

        public RodSkillLevelReached(string playerKey, int level)
        {
            PlayerKey = playerKey ?? "";
            Level = level;
        }
    }

    /// <summary>One player's skill: an experience total over a Def. Plain object, no engine state.</summary>
    public sealed class RodSkillProgress : IRodSkill
    {
        private readonly RodSkillDef _def;
        private readonly string _playerKey;

        public RodSkillProgress(RodSkillDef def, string playerKey = "", int experience = 0)
        {
            _def = def;
            _playerKey = playerKey ?? "";
            Experience = Mathf.Max(0, experience);
            Level = RodSkillMath.LevelFor(Experience, _def);
            Bonus = RodSkillMath.BonusAt(Level, _def);
        }

        public int Experience { get; private set; }
        public int Level { get; private set; }
        public int MaxLevel => _def != null ? _def.MaxLevel : 0;
        public RodSkillBonus Bonus { get; private set; }

        public RodFishingProfile Apply(in RodFishingProfile rod) => RodSkillMath.Apply(in rod, Bonus);

        public int RecordLanded(float difficulty01)
        {
            int gained = RodSkillMath.ExperienceFor(difficulty01, _def);
            if (gained <= 0) return 0;
            Experience = Experience > int.MaxValue - gained ? int.MaxValue : Experience + gained;
            int level = RodSkillMath.LevelFor(Experience, _def);
            if (level != Level)
            {
                Level = level;
                Bonus = RodSkillMath.BonusAt(Level, _def);
                EventBus.Publish(new RodSkillLevelReached(_playerKey, Level));
            }
            return gained;
        }
    }

    /// <summary>
    /// <b>The rod skills of the game being played</b>, one <see cref="RodSkillProgress"/> per player key.
    ///
    /// <para><b>Session-only, on purpose.</b> Nothing here is written to the save: adding to the save
    /// needs the owner's yes, which this change does not have. The book is bound to the live save object
    /// (a load or a new game replaces it), so every game starts at skill zero and one game's skill never
    /// leaks into another's. With NO game (no save object), nothing is earned and every skill reads
    /// zero, so a rig without a game behaves exactly as the rod alone.</para>
    /// FLAG lead-architect: new Core contract (rod skill).
    /// </summary>
    public sealed class RodSkillBook : IRodSkillBook
    {
        /// <summary>The key of the player at this machine. Co-op peers use their own keys.</summary>
        public const string LocalPlayerKey = "player.local";

        /// <summary>The Resources folder the skill's Def is loaded from.</summary>
        public const string ResourcesFolder = "RodSkill";

        private readonly Func<object> _session;
        private readonly Func<RodSkillDef> _defSource;
        private readonly Dictionary<string, RodSkillProgress> _players = new Dictionary<string, RodSkillProgress>(StringComparer.Ordinal);
        private readonly RodSkillProgress _noGame;
        private RodSkillDef _def;
        private bool _defLoaded;
        private object _bound;

        /// <param name="session">The current game's identity (default: the live save object).</param>
        /// <param name="def">The skill's numbers (default: the one under Resources/RodSkill).</param>
        public RodSkillBook(Func<object> session = null, Func<RodSkillDef> def = null)
        {
            _session = session ?? (() => GameServices.Save?.Current);
            _defSource = def ?? FromResources;
            _noGame = new RodSkillProgress(null);
        }

        public IRodSkill For(string playerKey)
        {
            object session = _session();
            if (session == null) return _noGame;
            if (!ReferenceEquals(session, _bound))
            {
                _players.Clear();
                _bound = session;
            }

            string key = string.IsNullOrEmpty(playerKey) ? LocalPlayerKey : playerKey;
            if (!_players.TryGetValue(key, out RodSkillProgress skill))
            {
                skill = new RodSkillProgress(Def, key);
                _players.Add(key, skill);
            }
            return skill;
        }

        private RodSkillDef Def
        {
            get
            {
                if (_defLoaded) return _def;
                _defLoaded = true;
                _def = _defSource();
                if (_def != null && _def.Problem() != null)
                {
                    Debug.LogWarning($"[RodSkill] {_def.name}: {_def.Problem()}; the skill adds nothing.");
                    _def = null;
                }
                return _def;
            }
        }

        private static RodSkillDef FromResources()
        {
            RodSkillDef[] found = Resources.LoadAll<RodSkillDef>(ResourcesFolder);
            if (found == null || found.Length == 0) return null;
            Array.Sort(found, (a, b) => string.CompareOrdinal(a.Id, b.Id));
            return found[0];
        }
    }
}
