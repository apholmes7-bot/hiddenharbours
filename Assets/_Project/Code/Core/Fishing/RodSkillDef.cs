using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// One rung of the rod-fishing skill: the total experience that reaches it, and what the player's
    /// own hand adds to whatever rod he holds once he is there. The amounts are CUMULATIVE (the whole
    /// bonus at this level, not the step from the one below), so the owner reads one row and knows
    /// what a level-6 angler has.
    /// </summary>
    [Serializable]
    public struct RodSkillLevel
    {
        [Tooltip("Total rod-fishing experience needed to reach this level. Strictly rising level to level.")]
        public int ExperienceRequired;

        [Tooltip("Metres added to the held rod's cast reach at this level.")]
        public float CastMetres;

        [Tooltip("Added to the held rod's line-strength multiplier at this level (rising strain is divided by it).")]
        public float LineStrength;

        [Tooltip("Metres per second added to the held rod's reel-up speed at this level.")]
        public float ReelUpMetresPerSecond;

        [Tooltip("Added to the held rod's landing multiplier at this level: the fight help, how fast a won " +
                 "pull brings the fish in.")]
        public float FightHelp;
    }

    /// <summary>
    /// <b>The rod-fishing skill's numbers</b> (owner ruling 2026-11-25): how much experience a landed fish
    /// gives, the level ladder, and what each level adds to the held rod. One asset under
    /// <c>Data/Resources/RodSkill/</c> (rule 2); every number is the owner's to tune and no code holds a
    /// copy of them (rule 6).
    ///
    /// <para><b>The ruling, in data.</b> The player's skill is ADDED to the rod's own stats, never instead
    /// of them: effective = rod + skill bonus at his level. A skilled player therefore still gains from a
    /// better rod, and a better rod still helps a novice. Experience comes only from fish won on a rod
    /// (a harder fish gives more); waiting, walking or idling gives none.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fishing/Rod Skill", fileName = "RodSkill")]
    public sealed class RodSkillDef : ScriptableObject
    {
        [Tooltip("Stable id, `skill.snake_case`. Append-only.")]
        public string Id = "skill.rod_fishing";

        [Tooltip("The name the player reads (the Notebook line).")]
        public string DisplayName = "Rod fishing";

        [Tooltip("Experience every fish won on a rod gives, however easy.")]
        [Min(0)] public int ExperiencePerFish = 3;

        [Tooltip("Extra experience for the hardest fish (difficulty 1). A fish of difficulty d gives " +
                 "ExperiencePerFish + round(d x this).")]
        [Min(0)] public int ExperienceForHardestFish = 12;

        [Tooltip("Levels 1..N in order. Level 0 is implicit: no experience, nothing added. The last row " +
                 "is the top level; experience past it is still counted but adds nothing more.")]
        public RodSkillLevel[] Levels = Array.Empty<RodSkillLevel>();

        /// <summary>The top level (the number of rows).</summary>
        public int MaxLevel => Levels != null ? Levels.Length : 0;

        /// <summary>Why this asset is malformed, or null when it is sound: experience must rise strictly
        /// level to level and every bonus must rise strictly too, so each level always helps.</summary>
        public string Problem()
        {
            if (string.IsNullOrEmpty(Id)) return "no id";
            if (ExperiencePerFish < 0 || ExperienceForHardestFish < 0) return "negative experience";
            if (ExperiencePerFish + ExperienceForHardestFish <= 0) return "a fish must give some experience";
            if (Levels == null || Levels.Length == 0) return "no levels";
            var below = default(RodSkillLevel);
            for (int i = 0; i < Levels.Length; i++)
            {
                RodSkillLevel l = Levels[i];
                if (l.ExperienceRequired <= below.ExperienceRequired) return $"level {i + 1}: experience must rise";
                if (!Rises(l.CastMetres, below.CastMetres)) return $"level {i + 1}: cast reach must rise";
                if (!Rises(l.LineStrength, below.LineStrength)) return $"level {i + 1}: line strength must rise";
                if (!Rises(l.ReelUpMetresPerSecond, below.ReelUpMetresPerSecond)) return $"level {i + 1}: reel speed must rise";
                if (!Rises(l.FightHelp, below.FightHelp)) return $"level {i + 1}: fight help must rise";
                below = l;
            }
            return null;
        }

        private static bool Rises(float value, float below) => value > below && !float.IsInfinity(value) && !float.IsNaN(value);
    }
}
