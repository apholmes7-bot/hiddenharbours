using System;
using System.IO;
using System.Linq;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static class HeronViewRoutinesBuilder
    {
        public const string Folder = "Assets/_Project/Data/Routines";
        public const string NpcFolder = "Assets/_Project/Data/NPCs";

        [MenuItem("Hidden Harbours/Regions/Cumberland/Build Golf Routines (create missing)")]
        public static void Build() => Author(false);

        [MenuItem("Hidden Harbours/Regions/Cumberland/Rewrite Golf Routines (discards edits)")]
        public static void Rewrite()
        {
            if (Application.isBatchMode) throw new InvalidOperationException("Use RewriteConfirmedCli for an explicit rewrite.");
            if (EditorUtility.DisplayDialog("Rewrite golf routines?", "Discard inspector edits to the eight golf routines?",
                "Rewrite", "Cancel")) Author(true);
        }

        public static void BuildCli() => RunCli(() => Author(false));
        public static void RewriteConfirmedCli() => RunCli(() => Author(true));

        static void Author(bool overwrite)
        {
            var days = HeronViewRoster.Build(CumberlandCourse.Read(), HeronViewRoster.SecondsPerGameHour);
            // Resolve all eight first. An incomplete approved cast writes no partial roster.
            var npcs = days.Select(d => AssetDatabase.LoadAssetAtPath<NpcDef>(NpcFolder + "/" + d.Person.AssetName + ".asset")).ToArray();
            for (int i = 0; i < days.Length; i++)
                if (npcs[i] == null || npcs[i].Id != days[i].Person.NpcId)
                    throw new InvalidOperationException("Missing approved golfer: " + days[i].Person.NpcId);
            int created = 0, kept = 0, rewritten = 0;
            for (int i = 0; i < days.Length; i++)
            {
                HeronViewRoster.Day day = days[i];
                string path = Folder + "/" + day.Person.AssetName + "Routine.asset";
                RoutineDef existing = AssetDatabase.LoadAssetAtPath<RoutineDef>(path);
                RoutineDef result = Apply(day, npcs[i], existing, overwrite);
                if (existing == null) { AssetDatabase.CreateAsset(result, path); created++; }
                else if (overwrite) { EditorUtility.SetDirty(result); rewritten++; }
                else kept++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[HeronViewRoutines] created {created}; kept {kept}; rewritten {rewritten}.");
        }

        /// <summary>Keeping an existing asset preserves every field, including its NPC.</summary>
        public static RoutineDef Apply(HeronViewRoster.Day day, NpcDef npc, RoutineDef existing, bool overwrite)
        {
            if (existing != null && !overwrite) return existing;
            RoutineDef def = existing != null ? existing : ScriptableObject.CreateInstance<RoutineDef>();
            def.Id = day.Person.RoutineId; def.Npc = npc;
            def.WalkSpeedMetresPerSecond = day.Speed; def.ScheduleJitterMinutes = day.JitterMinutes;
            def.CatchUpSpeedMultiplier = day.CatchUp;
            def.Entries = (RoutineEntry[])day.Blocks.Clone();
            return def;
        }

        internal static void RunCli(Action action)
        {
            try { action(); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
