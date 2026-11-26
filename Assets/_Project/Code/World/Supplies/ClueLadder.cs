using System;
using System.Collections.Generic;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The clue ladder rule</b>, pure: no scene, no save, no Unity lifecycle.
    ///
    /// <para>For each ladder still wanted, in order, the speaker gives the LOWEST rung they hold that has
    /// not been heard and whose rung below has been heard (the first rung always qualifies). At most one
    /// line per ladder per ask, so nobody walks the player from "somebody has it" to the door in one breath.
    /// Every rung spoken is reported so the caller sets its flag. When nothing qualifies the speaker gives
    /// their fallback.</para>
    /// </summary>
    public static class ClueLadder
    {
        /// <summary>
        /// Answer one ask. Appends the spoken lines to <paramref name="lines"/> and the flags of the rungs
        /// spoken to <paramref name="spokenFlags"/>; changes nothing else. Returns true when at least one
        /// rung was spoken (false: <paramref name="lines"/> got the fallback, if the speaker has one).
        /// </summary>
        public static bool Ask(string speakerId, IReadOnlyList<ClueLadderDef> ladders,
                               Func<ClueLadderDef, bool> stillWanted, Func<string, bool> heard,
                               string fallback, List<string> lines, List<string> spokenFlags)
        {
            bool spoke = false;
            if (!string.IsNullOrEmpty(speakerId) && ladders != null)
            {
                for (int l = 0; l < ladders.Count; l++)
                {
                    ClueLadderDef ladder = ladders[l];
                    if (ladder == null || ladder.Rungs == null) continue;
                    if (stillWanted != null && !stillWanted(ladder)) continue;

                    for (int r = 0; r < ladder.Rungs.Length; r++)
                    {
                        ClueRung rung = ladder.Rungs[r];
                        if (Heard(heard, rung.Flag)) continue;
                        if (r > 0 && !Heard(heard, ladder.Rungs[r - 1].Flag)) break;
                        string line = LineOf(rung, speakerId);
                        if (line == null) continue;      // not theirs to say; a higher rung needs this one heard
                        lines.Add(line);
                        if (!string.IsNullOrEmpty(rung.Flag)) spokenFlags.Add(rung.Flag);
                        spoke = true;
                        break;
                    }
                }
            }
            if (!spoke && !string.IsNullOrEmpty(fallback)) lines.Add(fallback);
            return spoke;
        }

        private static bool Heard(Func<string, bool> heard, string flag)
            => !string.IsNullOrEmpty(flag) && heard != null && heard(flag);

        /// <summary>The speaker's words for a rung, or null when they do not hold it.</summary>
        public static string LineOf(in ClueRung rung, string speakerId)
        {
            if (rung.Hints == null) return null;
            for (int i = 0; i < rung.Hints.Length; i++)
                if (rung.Hints[i].NpcId == speakerId && !string.IsNullOrEmpty(rung.Hints[i].Line))
                    return rung.Hints[i].Line;
            return null;
        }
    }
}
