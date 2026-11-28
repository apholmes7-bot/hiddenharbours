using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The idle fidgets the game ships, and the pace they come at</b>: one asset under
    /// <c>Assets/_Project/Resources</c> listing every <see cref="IdleFidgetDef"/> and holding the tunables
    /// the schedule reads (<see cref="IdleFidgetSchedule"/>): the gap between fidgets, how long a person
    /// must have stood idle before the first one, and how far off the gaze target is put.
    ///
    /// <para>Read by the player's figure and every villager's and skipper's figure through one shared
    /// piece (<c>CharacterFigureLife</c>); nothing here is saved.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Idle Fidget Library", fileName = "IdleFidgetLibrary")]
    public sealed class IdleFidgetLibraryDef : ScriptableObject
    {
        /// <summary>Where the shipped library lives for a runtime load (the library convention, as
        /// <see cref="CharacterCarryPoseDef.ResourcesPath"/>).</summary>
        public const string ResourcesPath = "IdleFidgetLibrary";

        [Header("Identity")]
        [Tooltip("Stable id, append-only.")]
        public string Id = "idlefidgets.library";

        [Header("Switch")]
        [Tooltip("Off: nobody fidgets, and every figure draws exactly what it drew before fidgets existed.")]
        public bool Enabled = true;

        [Header("Pace")]
        [Tooltip("The least time between the end of one fidget and the start of the next, seconds.")]
        [Min(0f)] public float MinGapSeconds = 6f;

        [Tooltip("The most time between the end of one fidget and the start of the next, seconds (plus the " +
                 "difference between the longest and the shortest fidget, which the schedule's even grid " +
                 "adds when a short fidget follows a long one's slot).")]
        [Min(0f)] public float MaxGapSeconds = 14f;

        [Tooltip("How long a person must have stood idle before a fidget may START, seconds. Any move, any " +
                 "action, any input restarts this wait, so a fidget never begins just as he stops.")]
        [Min(0f)] public float SettleSeconds = 2.5f;

        [Header("Look")]
        [Tooltip("How far along the gaze the look target is put, metres. Far enough that the head turns by " +
                 "the gaze's angle rather than by parallax.")]
        [Min(0.5f)] public float LookDistanceMetres = 6f;

        [Header("The fidgets")]
        [Tooltip("Every fidget a standing person may do. Order does not matter to the draw beyond being " +
                 "stable: the schedule walks this list as authored.")]
        public IdleFidgetDef[] Fidgets = Array.Empty<IdleFidgetDef>();

        private static IdleFidgetLibraryDef s_shipped;
        private static bool s_looked;

        /// <summary>The shipped library, loaded once from Resources; null when none ships.</summary>
        public static IdleFidgetLibraryDef Shipped
        {
            get
            {
                if (!s_looked)
                {
                    s_looked = true;
                    s_shipped = Resources.Load<IdleFidgetLibraryDef>(ResourcesPath);
                }
                return s_shipped;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_shipped = null;
            s_looked = false;
        }
    }
}
