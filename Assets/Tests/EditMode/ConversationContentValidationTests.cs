using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for the NPC↔NPC exchanges (owner ruling 2026-09-06). The same rules the other
    /// validators apply — unique namespaced ids, no dangling references, everything authored is
    /// reachable — plus two that are specific to a conversation and are the ones that will actually
    /// catch something:
    ///
    /// <list type="number">
    ///   <item><b>The meeting station must exist in the region's own table.</b> A typo'd station id is a
    ///   conversation that compiles, validates, ships, and never once happens.</item>
    ///   <item><b>Both participants must still be in ONE routine block across the whole window.</b> An
    ///   exchange is authored against two people's days; the day is the other lane's to re-time, and when
    ///   somebody moves a departure this is what says so. A window that straddles a departure means one
    ///   of them walks off mid-sentence — or, more likely, is still walking when the chat is due and it
    ///   silently never fires.</item>
    /// </list>
    ///
    /// <para>Lives in the TOP-LEVEL EditMode folder because it loads real <c>Data/</c> assets through
    /// <c>AssetDatabase</c> and reads the region's station table from <c>App.Editor</c> — the same
    /// placement as the other content validators.</para>
    /// </summary>
    public class ConversationContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string StPeters = "region.st_peters";

        private static List<T> LoadAll<T>() where T : Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { DataRoot }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        private static ConversationLibrary Library()
        {
            var lib = Resources.Load<ConversationLibrary>(ConversationLibrary.ResourcesPath);
            Assert.IsNotNull(lib,
                $"Resources/{ConversationLibrary.ResourcesPath}.asset is missing — the self-installing " +
                "NpcConversationDirector loads its exchanges from there and would have none");
            return lib;
        }

        // ---- the defs -----------------------------------------------------------------------------

        [Test]
        public void Conversations_Exist_AndHaveUniqueNamespacedIds()
        {
            var all = LoadAll<ConversationDef>();
            Assert.IsNotEmpty(all, "the coast must ship at least one authored exchange");

            var seen = new Dictionary<string, string>();
            foreach (ConversationDef d in all)
            {
                string path = AssetDatabase.GetAssetPath(d);
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.Id), $"{path}: ConversationDef has an empty id");
                Assert.IsTrue(d.Id.StartsWith("conversation."),
                    $"{path}: id '{d.Id}' must be namespaced 'conversation.snake_case' — it also seeds " +
                    "the minute the exchange starts at, so it is append-only and stable");
                Assert.IsFalse(seen.ContainsKey(d.Id),
                    $"duplicate ConversationDef id '{d.Id}' in '{path}' and " +
                    $"'{(seen.TryGetValue(d.Id, out string o) ? o : "?")}'");
                seen[d.Id] = path;
            }
        }

        [Test]
        public void EveryConversationIsRunnable()
        {
            foreach (ConversationDef d in LoadAll<ConversationDef>())
                Assert.IsTrue(d.IsRunnable,
                    $"{AssetDatabase.GetAssetPath(d)}: '{d.Id}' is not runnable — it needs exactly two " +
                    "distinct participants, at least one line, a region and a station. The director " +
                    "skips a def that fails this SILENTLY, which is why it is checked here.");
        }

        [Test]
        public void EveryLineNamesAParticipantAndHasWords()
        {
            foreach (ConversationDef d in LoadAll<ConversationDef>())
            {
                string path = AssetDatabase.GetAssetPath(d);
                for (int i = 0; i < d.Lines.Length; i++)
                {
                    Assert.IsNotNull(d.SpeakerOf(i),
                        $"{path}: '{d.Id}' line {i} has SpeakerIndex " +
                        $"{d.Lines[i].SpeakerIndex}, which is not one of its two participants — the " +
                        "director skips such a line rather than putting it in the wrong mouth, so it " +
                        "would just quietly go missing");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(d.Lines[i].Text),
                        $"{path}: '{d.Id}' line {i} has no words");
                }
            }
        }

        [Test]
        public void EveryWindowIsSane()
        {
            foreach (ConversationDef d in LoadAll<ConversationDef>())
            {
                string path = AssetDatabase.GetAssetPath(d);
                Assert.That(d.LatestHour, Is.GreaterThan(d.EarliestHour),
                    $"{path}: '{d.Id}' has an empty or inverted window — it would collapse to a single " +
                    "instant, which is not what an author who typed two numbers meant");
                Assert.That(d.RadiusMetres, Is.GreaterThan(0f), $"{path}: '{d.Id}' has a zero radius");
            }
        }

        // ---- reachability --------------------------------------------------------------------------

        [Test]
        public void EveryAuthoredConversation_IsInTheLibraryTheDirectorLoads()
        {
            var listed = new HashSet<string>();
            foreach (ConversationDef d in Library().Conversations)
                if (d != null) listed.Add(d.Id);

            foreach (ConversationDef d in LoadAll<ConversationDef>())
                Assert.IsTrue(listed.Contains(d.Id),
                    $"{AssetDatabase.GetAssetPath(d)}: '{d.Id}' is authored but is not in " +
                    $"Resources/{ConversationLibrary.ResourcesPath} — the director will never load it, " +
                    "and nothing else in the project would ever say so");
        }

        [Test]
        public void TheLibraryHasNoHoles()
        {
            ConversationLibrary lib = Library();
            Assert.IsNotEmpty(lib.Conversations, "the library lists no exchanges");
            for (int i = 0; i < lib.Conversations.Length; i++)
                Assert.IsNotNull(lib.Conversations[i],
                    $"Resources/{ConversationLibrary.ResourcesPath}: entry {i} is a missing reference");
        }

        // ---- the place ------------------------------------------------------------------------------

        /// <summary>
        /// ⭐ A typo'd station id is an exchange that compiles, validates, ships and never happens. The
        /// region's own table is the oracle.
        /// </summary>
        [Test]
        public void EveryMeetingStation_IsOneTheRegionActuallyDeclares()
        {
            foreach (ConversationDef d in LoadAll<ConversationDef>())
            {
                if (d.RegionId != StPeters) continue;   // the only region with a declared table today
                Assert.IsTrue(StPetersRoutines.AllStationIds.Contains(d.StationId),
                    $"{AssetDatabase.GetAssetPath(d)}: '{d.Id}' meets at '{d.StationId}', which is not " +
                    "in StPetersRoutines.AllStationIds — nothing would ever resolve it and the exchange " +
                    "would never fire");
            }
        }

        // ---- the two days it depends on ----------------------------------------------------------------

        /// <summary>
        /// ⭐⭐ <b>The guard with the longest reach.</b> An exchange is authored against two people's days,
        /// and their days belong to another lane. If somebody re-times a departure so that one of them is
        /// walking, or somewhere else entirely, during the window, this exchange stops happening — with no
        /// error, no warning and no failing test anywhere else in the project.
        ///
        /// <para>So: for each participant, the routine BLOCK covering the start of the window must be the
        /// same one covering its end. A window that straddles a departure means somebody sets off
        /// mid-sentence, and — far more likely — that they are still walking when the chat is due, since a
        /// walk here takes game HOURS at the shipped day length.</para>
        /// </summary>
        [Test]
        public void BothParticipants_StayInOneRoutineBlockAcrossTheWholeWindow()
        {
            var routines = LoadAll<RoutineDef>();

            foreach (ConversationDef d in LoadAll<ConversationDef>())
            {
                string path = AssetDatabase.GetAssetPath(d);

                for (int p = 0; p < d.Participants.Length; p++)
                {
                    NpcDef npc = d.Participants[p];
                    RoutineDef routine = routines.FirstOrDefault(r => r != null && r.Npc == npc);

                    Assert.IsNotNull(routine,
                        $"{path}: '{d.Id}' casts {npc.DisplayName} ({npc.Id}), who has no RoutineDef — " +
                        "somebody with no day is never anywhere, so the exchange could never fire");

                    float[] hours = routine.Entries.Select(e => e.StartHour).ToArray();
                    int atStart = RoutineSchedule.BlockIndexAt(d.EarliestHour, hours);
                    int atEnd = RoutineSchedule.BlockIndexAt(d.LatestHour, hours);

                    Assert.That(atEnd, Is.EqualTo(atStart),
                        $"{path}: '{d.Id}' runs {d.EarliestHour:0.##}–{d.LatestHour:0.##}, but " +
                        $"{npc.DisplayName} changes station inside it — she is at " +
                        $"'{routine.Entries[atStart].StationId}' at the start and heading for " +
                        $"'{routine.Entries[atEnd].StationId}' by the end. Narrow the window, or re-aim " +
                        "the exchange at the block she is actually standing in.");
                }
            }
        }

        /// <summary>
        /// The other half of the same worry, stated as a fact a reader can check: which block each
        /// participant is in, and where. This never fails — it REPORTS, so a run's output carries the
        /// evidence for "yes, these two really are both at the counter at noon" without anyone having to
        /// open six assets.
        /// </summary>
        [Test]
        public void Report_WhereEachPairIsStandingDuringItsWindow()
        {
            var routines = LoadAll<RoutineDef>();
            foreach (ConversationDef d in LoadAll<ConversationDef>())
            {
                var where = new List<string>();
                foreach (NpcDef npc in d.Participants)
                {
                    RoutineDef r = routines.FirstOrDefault(x => x != null && x.Npc == npc);
                    if (r == null) { where.Add($"{npc.Id}: NO ROUTINE"); continue; }
                    float[] hours = r.Entries.Select(e => e.StartHour).ToArray();
                    int block = RoutineSchedule.BlockIndexAt(d.EarliestHour, hours);
                    where.Add($"{npc.DisplayName} at '{r.Entries[block].StationId}' " +
                              $"(from {r.Entries[block].StartHour:0.##})");
                }
                Debug.Log($"[conversations] {d.Id} @ {d.StationId} " +
                          $"{d.EarliestHour:0.##}–{d.LatestHour:0.##}: {string.Join(" · ", where)}");
            }
            Assert.Pass();
        }
    }
}
