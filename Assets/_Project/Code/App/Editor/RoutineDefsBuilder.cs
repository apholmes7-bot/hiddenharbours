#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.World;               // NpcDef, RoutineDef, RoutineEntry, RoutineActivity

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>WHO DOES WHAT, AND WHEN — THE OWNER'S TO VETO.</b> Authors one <see cref="RoutineDef"/> asset per
    /// St Peters villager from the table below and wires each to its <see cref="NpcDef"/>, the same shape
    /// (and for the same reason) as <see cref="CharacterBuildsBuilder.CastMapping"/>: the mapping lives in
    /// code so it can be reviewed as an argument, and the ASSETS are what the game reads and what the owner
    /// edits. Changing who keeps the post office is then a one-line edit in an inspector, and this file is
    /// the record of what it was before.
    ///
    /// <para><b>⚠️ IT DOES NOT OVERWRITE BY DEFAULT, AND IT SAYS WHICH.</b> <see cref="Build"/> creates
    /// only the assets that are ABSENT and reports the ones it left alone, because the moment the owner
    /// retimes somebody's day the asset is the truth and this table is history. A builder that
    /// silently reasserted this table would undo his tuning and report success —
    /// <c>LoadOrCreate</c>'s standing trap. <see cref="Rewrite"/> is the deliberate, separate,
    /// confirmed way to throw that away.</para>
    ///
    /// <para><b>The village is designed to OVERLAP.</b> The times below are chosen so the island has
    /// moments rather than five independent clocks: THREE customers at the store counter just after noon
    /// while the storekeeper is behind it, two men out of one cottage door an hour apart at dawn, four
    /// neighbours on the green in the evening, and the schoolteacher out at the head of the flats at sunset. Each is a consequence of
    /// two rows in this table and nothing else — no scripting, no events.</para>
    /// </summary>
    public static class RoutineDefsBuilder
    {
        const string MenuBuild = "Hidden Harbours/World/Build St Peters Routines (create missing)";
        const string MenuRewrite = "Hidden Harbours/World/Rewrite St Peters Routines (discards owner edits)";
        const string RoutinesFolder = "Assets/_Project/Data/Routines";
        const string DataNpcs = "Assets/_Project/Data/NPCs";

        // Shorthand so a day reads as a table rather than as constructor noise.
        static RoutineEntry At(float hour, string station, RoutineActivity activity, string why) =>
            new RoutineEntry { StartHour = hour, StationId = station, Activity = activity, Why = why };

        /// <summary>One villager's authored day: which NpcDef, the id, how fast they walk, and the blocks.</summary>
        public readonly struct Day
        {
            public readonly string NpcAsset;
            public readonly string RoutineId;
            public readonly float WalkSpeed;
            public readonly float JitterMinutes;
            public readonly string Why;
            public readonly RoutineEntry[] Blocks;

            public Day(string npcAsset, string routineId, float walkSpeed, float jitterMinutes,
                       string why, RoutineEntry[] blocks)
            {
                NpcAsset = npcAsset; RoutineId = routineId; WalkSpeed = walkSpeed;
                JitterMinutes = jitterMinutes; Why = why; Blocks = blocks;
            }
        }

        /// <summary>
        /// <b>THE ROSTER.</b> Six days: the island's five named inhabitants (§7.1's cast, placed since
        /// #354) and Aunt Ginny, whose day is deliberately the smallest one on the island — see her entry.
        ///
        /// <para>Walking speeds differ per person because they should: an old skipper and a young clam
        /// digger do not cross the green at the same pace, and the number is what decides how much of the
        /// day is spent walking. At the pinned 1800 s day one game hour is 75 real seconds, so 1.15 m/s
        /// covers ~86 m of island per game hour.</para>
        /// </summary>
        public static IReadOnlyList<Day> Roster => new[]
        {
            // -----------------------------------------------------------------------------------------
            new Day("RoseMacIsaac", "routine.rose_macisaac", walkSpeed: 1.20f, jitterMinutes: 6f,
                    why: "the neighbour on the green keeps the POST OFFICE — the one public building on " +
                         "this island with a door and nobody behind it. ⚠️ OWNER: this is the assignment " +
                         "most worth vetoing (she wears a fish-plant cutter's apron, not a postmistress's); " +
                         "the alternative is that she works the wharf and the post office stays dark",
                    new[]
                    {
                        At(7.00f, StPetersRoutines.StationSaltboxDooryard, RoutineActivity.Home,
                           "out on her own step with the tea — the first face you meet crossing the green"),
                        At(8.50f, StPetersRoutines.StationPostOfficeCounter, RoutineActivity.Work,
                           "up the lane and INSIDE the post office: you watch her go in the door you can " +
                           "see, and she is behind the counter if you follow her"),
                        At(12.50f, StPetersRoutines.StationStoreCustomerA, RoutineActivity.Errand,
                           "her dinner from the store — and the reason the counter has a queue at noon"),
                        At(14.00f, StPetersRoutines.StationPostOfficeCounter, RoutineActivity.Work,
                           "back behind the counter for the afternoon"),
                        At(18.00f, StPetersRoutines.StationGreenA, RoutineActivity.Recreation,
                           "the green in the evening — the half of the day the village is a place to be IN"),
                        At(21.50f, StPetersRoutines.StationHomeSaltbox, RoutineActivity.Home,
                           "in through her own door for the night"),
                    }),

            // -----------------------------------------------------------------------------------------
            new Day("MargueriteLeBlanc", "routine.marguerite_leblanc", walkSpeed: 1.10f, jitterMinutes: 5f,
                    why: "the storekeeper LIVES OVER HER SHOP, which is why the shop is dark at night by " +
                         "the same data that draws her asleep (§2.5's own promise, made literal) and why " +
                         "the island's four houses stretch to five people",
                    new[]
                    {
                        At(7.00f, StPetersRoutines.StationStoreCounter, RoutineActivity.Work,
                           "out of her own door and behind the counter — she opens before anyone is up"),
                        At(13.00f, StPetersRoutines.StationPostOfficeDoor, RoutineActivity.Errand,
                           "up the lane with the day's mail. She crosses Rose coming the other way, which " +
                           "is a moment neither of them was scripted into"),
                        At(14.50f, StPetersRoutines.StationStoreCounter, RoutineActivity.Work,
                           "back behind the counter"),
                        At(19.00f, StPetersRoutines.StationGreenC, RoutineActivity.Recreation,
                           "shuts up and comes down to the green"),
                        At(21.00f, StPetersRoutines.StationHomeStore, RoutineActivity.Home,
                           "in, upstairs, and the storefront goes dark"),
                    }),

            // -----------------------------------------------------------------------------------------
            new Day("EileenDoiron", "routine.eileen_doiron", walkSpeed: 1.05f, jitterMinutes: 4f,
                    why: "the schoolteacher BOARDS at the white farmhouse — the biggest house on the " +
                         "island and the only floor that seats four — and teaches inside the schoolhouse, " +
                         "which is what makes a one-room school read as one",
                    new[]
                    {
                        At(7.50f, StPetersRoutines.StationFarmhouseDooryard, RoutineActivity.Home,
                           "out on the farmhouse step before school"),
                        At(8.25f, StPetersRoutines.StationSchoolDesk, RoutineActivity.Work,
                           "down the lane and in — the schoolhouse door faces the green, so you see it open"),
                        At(12.00f, StPetersRoutines.StationStoreCustomerC, RoutineActivity.Errand,
                           "her dinner at the store, the first of the noon queue"),
                        At(13.25f, StPetersRoutines.StationSchoolDesk, RoutineActivity.Work,
                           "afternoon lessons"),
                        At(17.50f, StPetersRoutines.StationFlatsLookout, RoutineActivity.Recreation,
                           "out the painted dirt path west to the head of the flats: the sun goes down " +
                           "over the bar, and this is the only place on the island you can watch it"),
                        At(20.50f, StPetersRoutines.StationHomeFarmhouse, RoutineActivity.Home,
                           "the long walk back up the island in the dusk, and in"),
                    }),

            // -----------------------------------------------------------------------------------------
            new Day("JuniorPoirier", "routine.junior_poirier", walkSpeed: 1.35f, jitterMinutes: 7f,
                    why: "the clam digger works the FLATS — the same spot #354 anchored him on — and lives " +
                         "in the sage cottage, the last house before the shore, which is where a man whose " +
                         "work is the beach would live. The youngest legs on the island, so the fastest",
                    new[]
                    {
                        At(6.00f, StPetersRoutines.StationSageDooryard, RoutineActivity.Home,
                           "out early — through the same door Basil left by an hour before him, and Basil " +
                           "is still a speck away up the island when he does it"),
                        At(6.50f, StPetersRoutines.StationFlatsHead, RoutineActivity.Work,
                           "west on the painted dirt to the head of the flats, and digs"),
                        At(12.00f, StPetersRoutines.StationStoreCustomerB, RoutineActivity.Errand,
                           "the long walk in for his dinner — the middle slot at the counter"),
                        At(14.50f, StPetersRoutines.StationFlatsHead, RoutineActivity.Work,
                           "back out west for the afternoon tide"),
                        At(19.00f, StPetersRoutines.StationGreenD, RoutineActivity.Recreation,
                           "in off the flats to the green"),
                        At(21.00f, StPetersRoutines.StationHomeSageA, RoutineActivity.Home,
                           "in, one side of the doorway lane"),
                    }),

            // -----------------------------------------------------------------------------------------
            new Day("BasilSamson", "routine.basil_samson", walkSpeed: 0.95f, jitterMinutes: 8f,
                    why: "the old skipper WALKS THE LENGTH OF THE ISLAND TWICE A DAY, on the painted dirt " +
                         "path east — which is the same path the player walks out to the dory, so you meet " +
                         "him on it. The slowest walk on the island and the longest: ~234 m each way, " +
                         "about 3.3 game hours, and it is most of what his day is. ⚠️ OWNER: the " +
                         "alternative is a wharf-local day, which needs a building at the wharf that does " +
                         "not exist",
                    new[]
                    {
                        At(5.00f, StPetersRoutines.StationWharfHead, RoutineActivity.Work,
                           "away before anyone, up the wharf from the slip head, watching the water"),
                        At(12.00f, StPetersRoutines.StationSlipHead, RoutineActivity.Work,
                           "down to the shoreward end of the planks — where a boat's line gets taken"),
                        At(15.00f, StPetersRoutines.StationWharfHead, RoutineActivity.Recreation,
                           "back out to the pier head. He is who is there when you come home under power"),
                        At(18.00f, StPetersRoutines.StationHomeSageB, RoutineActivity.Home,
                           "the whole island again, in the dusk, and in"),
                    }),

            // -----------------------------------------------------------------------------------------
            new Day("AuntGinny", "routine.aunt_ginny", walkSpeed: 1.00f, jitterMinutes: 3f,
                    why: "⭐ THE SMALLEST DAY ON THE ISLAND, ON PURPOSE. Ginny is the onboarding gate — " +
                         "the first conversation, the met_ginny flag, and the FrontedFeeGrant that stops " +
                         "the clam licence being a soft-lock — so the opening cannot afford her to be " +
                         "somewhere else on the first morning. Her whole day stays inside her own dooryard: " +
                         "she is never more than ~9 m from the spot she has stood on since the opening " +
                         "shipped, and at the 06:00 start hour she is standing exactly on it. §3.4's own " +
                         "words are 'garden and kitchen at dawn … on the step at dusk … rarely leaves'. " +
                         "⚠️ Her cottage is a legacy single sprite with no baked room, so she has no " +
                         "inside to go into — she keeps her step overnight, which is what she did before " +
                         "routines existed",
                    new[]
                    {
                        At(6.50f, StPetersRoutines.StationCottageGarden, RoutineActivity.Work,
                           "the garden round the cottage's west side, at dawn"),
                        At(10.00f, StPetersRoutines.StationCottageStep, RoutineActivity.Home,
                           "back to her step — the mark the opening's first conversation happens on, and " +
                           "where she is for most of the day"),
                        At(19.00f, StPetersRoutines.StationGreenB, RoutineActivity.Recreation,
                           "down to the green at dusk, four metres from her own door"),
                        At(21.00f, StPetersRoutines.StationCottageStep, RoutineActivity.Home,
                           "back on the step for the night"),
                    }),
        };

        [MenuItem(MenuBuild, priority = 240)]
        public static void Build() => Author(overwrite: false);

        [MenuItem(MenuRewrite, priority = 241)]
        public static void Rewrite()
        {
            bool ok = !InternalEditorBridge.IsInteractive || EditorUtility.DisplayDialog(
                "Rewrite St Peters routines?",
                "This replaces every routine asset under Data/Routines with the table in " +
                "RoutineDefsBuilder.cs, discarding any retiming done in the inspector.\n\n" +
                "Use 'Build St Peters Routines (create missing)' if you only want the absent ones.",
                "Rewrite them", "Cancel");
            if (ok) Author(overwrite: true);
        }

        static void Author(bool overwrite)
        {
            EnsureFolder(RoutinesFolder);

            var created = new List<string>();
            var rewritten = new List<string>();
            var kept = new List<string>();
            var missingNpcs = new List<string>();

            foreach (Day day in Roster)
            {
                var npc = AssetDatabase.LoadAssetAtPath<NpcDef>($"{DataNpcs}/{day.NpcAsset}.asset");
                if (npc == null)
                {
                    missingNpcs.Add(day.NpcAsset);
                    continue;
                }

                string path = $"{RoutinesFolder}/{day.NpcAsset}Routine.asset";
                var existing = AssetDatabase.LoadAssetAtPath<RoutineDef>(path);

                if (existing != null && !overwrite)
                {
                    kept.Add($"{npc.DisplayName} ({existing.Entries.Length} block(s))");
                    // Re-point the NPC reference even when keeping the times: a routine whose Npc came
                    // loose stops being wired to anybody, and that is bookkeeping rather than tuning.
                    if (existing.Npc != npc)
                    {
                        existing.Npc = npc;
                        EditorUtility.SetDirty(existing);
                    }
                    continue;
                }

                RoutineDef def = existing != null ? existing : ScriptableObject.CreateInstance<RoutineDef>();
                def.Id = day.RoutineId;
                def.Npc = npc;
                def.WalkSpeedMetresPerSecond = day.WalkSpeed;
                def.ScheduleJitterMinutes = day.JitterMinutes;
                def.Entries = day.Blocks;

                if (existing != null)
                {
                    EditorUtility.SetDirty(def);
                    rewritten.Add($"{npc.DisplayName} ({day.Blocks.Length} block(s))");
                }
                else
                {
                    AssetDatabase.CreateAsset(def, path);
                    created.Add($"{npc.DisplayName} ({day.Blocks.Length} block(s))");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[RoutineDefsBuilder] {RoutinesFolder}: created {created.Count}" +
                (created.Count > 0 ? $" — {string.Join(" · ", created)}" : "") +
                $"; rewritten {rewritten.Count}" +
                (rewritten.Count > 0 ? $" — {string.Join(" · ", rewritten)}" : "") +
                $"; LEFT ALONE {kept.Count}" +
                (kept.Count > 0 ? $" — {string.Join(" · ", kept)}" : "") +
                ". Commit the routine assets. Re-run Build St Peters Scene to wire them into the scene.");

            if (kept.Count > 0 && !overwrite)
                Debug.LogWarning(
                    $"[RoutineDefsBuilder] {kept.Count} routine(s) already existed and were NOT changed — " +
                    "their authored times stand, which is the point: once the owner has retimed a day, the " +
                    "asset is the truth and the table in this file is history. Use " +
                    $"'{MenuRewrite}' to deliberately throw that away.");

            if (missingNpcs.Count > 0)
                Debug.LogWarning(
                    $"[RoutineDefsBuilder] no NpcDef for {string.Join(", ", missingNpcs)} under " +
                    $"{DataNpcs} — no routine was authored for them, so they stay anchored. Re-run after " +
                    "the Data/NPCs assets import rather than authoring a routine with nobody in it.");
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Whether a human is watching. A headless <c>-executeMethod</c> run must not stop on a
        /// modal dialog — it would hang the run and time out with no log line saying why.</summary>
        static class InternalEditorBridge
        {
            public static bool IsInteractive => !Application.isBatchMode;
        }
    }
}
#endif
