using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The one place a player's saved skill experience is read and written</b> (save v27,
    /// <see cref="SaveData.PlayerSkills"/>). The skill turns experience into a level and a bonus with its own
    /// Def; only the experience is saved. Keeps no state of its own; static and null-safe (a null save reads
    /// 0 and a write to it does nothing).
    /// </summary>
    public static class PlayerSkillRows
    {
        /// <summary>The experience <paramref name="playerKey"/> has in <paramref name="skillId"/> (0 when the
        /// save holds no row: an older save, or a skill never used).</summary>
        public static int Experience(SaveData save, string playerKey, string skillId)
        {
            PlayerSkillDto row = Find(save, playerKey, skillId);
            return row != null && row.Experience > 0 ? row.Experience : 0;
        }

        /// <summary>
        /// Set the experience <paramref name="playerKey"/> has in <paramref name="skillId"/> (a negative value
        /// reads 0). Returns false, changing nothing, for a null save or an empty skill id.
        /// </summary>
        public static bool SetExperience(SaveData save, string playerKey, string skillId, int experience)
        {
            if (save == null || string.IsNullOrEmpty(skillId)) return false;
            save.PlayerSkills ??= new List<PlayerSkillDto>();
            PlayerSkillDto row = Find(save, playerKey, skillId);
            if (row == null)
            {
                row = new PlayerSkillDto { PlayerKey = SavePlayerKeys.Normalise(playerKey), SkillId = skillId };
                save.PlayerSkills.Add(row);
            }
            row.Experience = experience > 0 ? experience : 0;
            return true;
        }

        /// <summary>The row for this player and skill, or null (an empty key means the local player).</summary>
        public static PlayerSkillDto Find(SaveData save, string playerKey, string skillId)
        {
            if (save?.PlayerSkills == null || string.IsNullOrEmpty(skillId)) return null;
            string key = SavePlayerKeys.Normalise(playerKey);
            for (int i = 0; i < save.PlayerSkills.Count; i++)
            {
                PlayerSkillDto row = save.PlayerSkills[i];
                if (row != null
                    && string.Equals(row.SkillId, skillId, StringComparison.Ordinal)
                    && string.Equals(SavePlayerKeys.Normalise(row.PlayerKey), key, StringComparison.Ordinal))
                    return row;
            }
            return null;
        }
    }
}
