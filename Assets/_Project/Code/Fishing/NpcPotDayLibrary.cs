using System;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// Every NPC pot boat's working day, one Def per boat (Data/Traps). Lives in Resources so the run-time
    /// presenter finds the days without a scene reference: adding a boat is a new Def and a line here, never a
    /// scene edit (rule 2, ADR 0003).
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/NPC Pot Day Library", fileName = "NpcPotDayLibrary")]
    public sealed class NpcPotDayLibrary : ScriptableObject
    {
        public const string ResourcesPath = "NpcPotDayLibrary";

        [Tooltip("Every NPC pot day in the game. Null entries are skipped.")]
        public NpcPotDayDef[] Days = Array.Empty<NpcPotDayDef>();
    }
}
