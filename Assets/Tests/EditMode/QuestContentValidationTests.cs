using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Content validation for quests and knowledge pages.</b> Ids, shape, and — the one that earns
    /// its keep — that every quest step names a flag <i>something in the shipped content can actually
    /// set</i>.
    ///
    /// <para><b>⭐ WHY THE FLAG REGISTRY EXISTS.</b> A quest is a view over flags, so a step whose
    /// flag id is misspelled is not a compile error, not a runtime error, and not visibly wrong: it
    /// is a step that can never complete, on a quest that can never leave the Active tab. Nothing in
    /// the game would ever say so. <see cref="SettableFlags"/> gathers every id the shipped content
    /// can set and <see cref="Problems"/> rejects any step outside it, which turns a silent
    /// content bug into a red test.</para>
    ///
    /// <para><b>The registry is deliberately extensible, and its incompleteness is the failure mode to
    /// watch.</b> It reads three sources today: <see cref="OnboardingFlags"/>' declared constants (by
    /// reflection, so adding one cannot leave this stale), every <see cref="NpcDef.CompletionFlag"/>,
    /// and a deed key for every <see cref="HomeDef"/>. A flag set from somewhere else — an
    /// <c>Interactable</c>'s inline <c>_completionFlag</c> on a prefab, say — is NOT yet seen here, so
    /// a legitimate quest step could be rejected. That is the right way round: the failure asks a
    /// human to add the source, rather than a typo shipping quietly. The message says so.</para>
    ///
    /// <para>Lives in the TOP-LEVEL EditMode folder, not a per-module one, because content validation
    /// loads real <c>Data/</c> assets across World and Economy — the placement
    /// <c>ContentValidationTests</c> and <c>NpcContentValidationTests</c> already use.</para>
    /// </summary>
    public class QuestContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";

        private static readonly Regex QuestId = new Regex(@"^quest\.[a-z0-9]+(_[a-z0-9]+)*$");
        private static readonly Regex KnowledgeId = new Regex(@"^knowledge\.[a-z0-9]+(_[a-z0-9]+)*$");
        private static readonly Regex TabId = new Regex(@"^tab\.[a-z0-9]+(_[a-z0-9]+)*$");

        /// <summary>
        /// The kit's copy budget at the closest zoom tier. A longer line is not clipped — it wraps
        /// and eats a ruled line — so these are failures for the WRITING lane rather than hard
        /// rendering limits.
        ///
        /// <para>These were literals while <c>NotebookKit</c> sat on the parked art-import branch,
        /// with a note that they must become the kit's own constants the day it landed — a second
        /// definition of a shared number is exactly how content and layout drift apart. #563 landed
        /// the kit, so that reconciliation is DONE: these are now the kit's published numbers read
        /// from the kit, and there is nothing left to keep in step by hand.</para>
        /// </summary>
        private const int StepCols = NotebookKit.StepCharsAtClosestTier,
                          TabLabelCols = NotebookKit.ChipChars;

        /// <inheritdoc cref="StepCols"/>
        private static readonly int TitleCols = NotebookKit.TitleColsFor(NotebookKit.ClosestTierCols);

        /// <summary>The kit reserves at least this many ruled lines for an illustration frame and its
        /// caption. Same provenance as the copy budget above, reconciled the same day.</summary>
        private const int SlotMinLines = NotebookKit.PlateMinLines;

        private static List<T> LoadAll<T>() where T : Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { DataRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        // ---- the registry -------------------------------------------------------------------------

        /// <summary>
        /// Every flag id the shipped content can set. See the class remarks for why this is the
        /// load-bearing part, and for what it does not yet see.
        /// </summary>
        public static HashSet<string> SettableFlags()
        {
            var flags = new HashSet<string>();

            // 1. The onboarding beats, by REFLECTION over the declared constants — so a new beat
            //    cannot leave this list stale, which a hand-copied array would.
            foreach (var f in typeof(OnboardingFlags).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (f.IsLiteral && f.FieldType == typeof(string))
                    flags.Add((string)f.GetRawConstantValue());

            // 2. Every conversation that banks a flag when it completes.
            foreach (var npc in LoadAll<NpcDef>())
                if (!string.IsNullOrEmpty(npc.CompletionFlag)) flags.Add(npc.CompletionFlag);

            // 3. Every home's deed. Built through HomeDeeds.KeyFor rather than by pasting "deed." so
            //    the prefix has exactly one definition.
            foreach (var home in LoadAll<HomeDef>())
                if (!string.IsNullOrEmpty(home.Id)) flags.Add(HomeDeeds.KeyFor(home.Id));

            return flags;
        }

        // ---- the rules, callable on ANY def so they can be sabotaged ------------------------------

        /// <summary>
        /// Everything wrong with one quest, as human-readable lines. Empty means valid. Taking the
        /// def as an argument (rather than sweeping assets inline) is what lets the sabotage tests
        /// below prove these rules actually reject bad content — a validator nobody has seen fail is
        /// a validator nobody has tested.
        /// </summary>
        public static List<string> Problems(QuestDef q, ICollection<string> knownFlags)
        {
            var bad = new List<string>();
            if (q == null) { bad.Add("null quest"); return bad; }

            if (string.IsNullOrWhiteSpace(q.Id)) bad.Add("empty Id");
            else if (!QuestId.IsMatch(q.Id)) bad.Add($"Id '{q.Id}' is not quest.snake_case");

            if (string.IsNullOrWhiteSpace(q.Title)) bad.Add("empty Title");
            else if (q.Title.Length > TitleCols)
                bad.Add($"Title is {q.Title.Length} chars, over the {TitleCols} a title line holds");

            if (q.Context != null && q.Context.Length > TitleCols)
                bad.Add($"Context is {q.Context.Length} chars, over the {TitleCols} a title line holds");

            if (q.Steps == null || q.Steps.Count == 0)
            {
                bad.Add("no steps — a quest with nothing to do reads as broken on the Active tab");
                return bad;
            }

            var seenFlags = new HashSet<string>();
            for (int i = 0; i < q.Steps.Count; i++)
            {
                var s = q.Steps[i];
                string at = $"step {i}";

                if (s == null) { bad.Add($"{at} is null"); continue; }

                if (string.IsNullOrWhiteSpace(s.Label)) bad.Add($"{at} has no Label");
                else if (s.Label.Length > StepCols)
                    bad.Add($"{at} Label is {s.Label.Length} chars, over the {StepCols} a step line holds");

                if (string.IsNullOrWhiteSpace(s.Flag))
                {
                    // An empty flag is never done, so the quest could never be archived.
                    bad.Add($"{at} has no Flag — it could never complete");
                }
                else if (knownFlags != null && !knownFlags.Contains(s.Flag))
                {
                    bad.Add($"{at} names flag '{s.Flag}', which nothing in the shipped content sets. " +
                            "Either it is a typo, or it comes from a source QuestContentValidationTests." +
                            "SettableFlags() does not read yet — add the source there rather than " +
                            "deleting this check.");
                }
                else if (!seenFlags.Add(s.Flag))
                {
                    // Two steps on one flag both flip at once, so one of them can never be the
                    // current step. That is a content mistake, not a feature.
                    bad.Add($"{at} repeats flag '{s.Flag}' from an earlier step");
                }
            }

            return bad;
        }

        /// <summary>Everything wrong with one knowledge page. Empty means valid.</summary>
        public static List<string> Problems(KnowledgePageDef k)
        {
            var bad = new List<string>();
            if (k == null) { bad.Add("null page"); return bad; }

            if (string.IsNullOrWhiteSpace(k.Id)) bad.Add("empty Id");
            else if (!KnowledgeId.IsMatch(k.Id)) bad.Add($"Id '{k.Id}' is not knowledge.snake_case");

            if (string.IsNullOrWhiteSpace(k.TabId)) bad.Add("empty TabId");
            else if (!TabId.IsMatch(k.TabId)) bad.Add($"TabId '{k.TabId}' is not tab.snake_case");

            if (string.IsNullOrWhiteSpace(k.TabLabel)) bad.Add("empty TabLabel");
            else if (k.TabLabel.Length > TabLabelCols)
                bad.Add($"TabLabel '{k.TabLabel}' is {k.TabLabel.Length} chars; the chip clips at " +
                        $"{TabLabelCols} and does not shrink");

            if (string.IsNullOrWhiteSpace(k.Head)) bad.Add("empty Head");

            if (k.Body == null || k.Body.Count == 0) bad.Add("no Body — an empty page");
            else if (k.Body.Any(string.IsNullOrWhiteSpace)) bad.Add("a blank Body paragraph");

            if (!string.IsNullOrEmpty(k.SlotKey) && k.SlotLines < SlotMinLines)
                bad.Add($"SlotKey '{k.SlotKey}' reserves {k.SlotLines} lines, under the kit's minimum " +
                        $"of {SlotMinLines} — the frame and its caption have nowhere to sit");

            return bad;
        }

        // ---- the sweeps over real Data/ -----------------------------------------------------------

        [Test]
        public void EveryQuestIsValid()
        {
            var known = SettableFlags();
            var quests = LoadAll<QuestDef>();

            Assert.That(quests, Is.Not.Empty,
                "no QuestDef assets found — the starter content should be under Data/Resources/Quests");

            var report = quests
                .Select(q => (q, problems: Problems(q, known)))
                .Where(t => t.problems.Count > 0)
                .Select(t => $"{AssetDatabase.GetAssetPath(t.q)}:\n    " + string.Join("\n    ", t.problems))
                .ToList();

            Assert.That(report, Is.Empty, "\n  " + string.Join("\n  ", report));
        }

        [Test]
        public void EveryKnowledgePageIsValid()
        {
            var pages = LoadAll<KnowledgePageDef>();

            Assert.That(pages, Is.Not.Empty,
                "no KnowledgePageDef assets found — the starter content should be under Data/Resources/Knowledge");

            var report = pages
                .Select(k => (k, problems: Problems(k)))
                .Where(t => t.problems.Count > 0)
                .Select(t => $"{AssetDatabase.GetAssetPath(t.k)}:\n    " + string.Join("\n    ", t.problems))
                .ToList();

            Assert.That(report, Is.Empty, "\n  " + string.Join("\n  ", report));
        }

        [Test]
        public void QuestIdsAreUnique()
        {
            var dupes = LoadAll<QuestDef>().GroupBy(q => q.Id)
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key} × {g.Count()}").ToList();

            Assert.That(dupes, Is.Empty, "duplicate quest ids: " + string.Join(", ", dupes));
        }

        [Test]
        public void KnowledgeIdsAreUnique()
        {
            var dupes = LoadAll<KnowledgePageDef>().GroupBy(k => k.Id)
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key} × {g.Count()}").ToList();

            Assert.That(dupes, Is.Empty, "duplicate knowledge ids: " + string.Join(", ", dupes));
        }

        // ---- ⭐ the book can only see what is inside its Resources root ---------------------------

        /// <summary>
        /// <b>An asset authored outside the Resources root is INVISIBLE to the notebook, and nothing
        /// else says so.</b>
        ///
        /// <para>The book installs itself and has no builder to hand it anything, so it loads its Defs
        /// with <c>Resources.LoadAll</c> — which reaches only inside a folder literally named
        /// <c>Resources</c>, and which reports a miss as an empty list rather than an error. Every other
        /// check in this file sweeps the whole of <c>Data/</c>, so a quest dropped at the old
        /// <c>Data/Quests/</c> path validates perfectly and then simply never appears on the page. That
        /// is the worst shape a content bug can take: the author did nothing wrong that anything told
        /// them about, and the symptom is an absence.</para>
        ///
        /// <para>So this compares the two sets directly — what the AssetDatabase sweep finds under
        /// <c>Data/</c> against what the runtime loader actually returns — and names the offending
        /// paths. Both directions matter: an asset the sweep sees and the loader does not is misplaced,
        /// and one the loader sees and the sweep does not would mean the roots have come apart.</para>
        /// </summary>
        [Test]
        public void EveryQuestIsSomewhereTheBookCanActuallyLoadIt()
        {
            AssertTheLoaderSeesEverything<QuestDef>(
                NotebookContentSource.QuestsFolderPath,
                NotebookContentSource.Quests().Select(q => q.Id),
                q => q.Id);
        }

        /// <inheritdoc cref="EveryQuestIsSomewhereTheBookCanActuallyLoadIt"/>
        [Test]
        public void EveryKnowledgePageIsSomewhereTheBookCanActuallyLoadIt()
        {
            AssertTheLoaderSeesEverything<KnowledgePageDef>(
                NotebookContentSource.KnowledgeFolderPath,
                NotebookContentSource.Knowledge().Select(k => k.Id),
                k => k.Id);
        }

        /// <summary>
        /// The shared arm of the two tests above. Reported as PATHS rather than ids, because "move this
        /// file" is the fix and the id is not what is wrong with it.
        /// </summary>
        private static void AssertTheLoaderSeesEverything<T>(string expectedFolder,
                                                             IEnumerable<string> loadedIds,
                                                             System.Func<T, string> idOf)
            where T : Object
        {
            var onDisk = LoadAll<T>();
            Assert.That(onDisk, Is.Not.Empty,
                $"no {typeof(T).Name} assets found at all — the starter content should be under " +
                expectedFolder);

            var misplaced = onDisk
                .Select(a => AssetDatabase.GetAssetPath(a))
                .Where(path => !path.StartsWith(expectedFolder + "/", System.StringComparison.Ordinal))
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToList();

            Assert.That(misplaced, Is.Empty,
                $"⭐ These {typeof(T).Name} assets are NOT under {expectedFolder}, so the notebook " +
                "loads them with Resources.LoadAll and finds nothing — they validate cleanly here and " +
                "then never appear on the page. Move them:\n  " + string.Join("\n  ", misplaced));

            // The reverse arm, and the one that would catch the roots coming apart rather than one
            // asset being in the wrong place.
            var sweptIds = onDisk.Select(idOf).OrderBy(i => i, System.StringComparer.Ordinal).ToArray();
            var loaded = loadedIds.OrderBy(i => i, System.StringComparer.Ordinal).ToArray();

            Assert.That(loaded, Is.EqualTo(sweptIds),
                $"the {typeof(T).Name}s on disk and the ones the book's own loader returns disagree — " +
                $"is {expectedFolder} still inside a folder named 'Resources'?");
        }

        [Test]
        public void PagesSharingATabAgreeOnItsLabel()
        {
            // The tab chip is drawn once per tab, so two pages disagreeing about its label means one
            // of them silently loses.
            var conflicts = LoadAll<KnowledgePageDef>()
                .Where(k => !string.IsNullOrEmpty(k.TabId))
                .GroupBy(k => k.TabId)
                .Where(g => g.Select(k => k.TabLabel).Distinct().Count() > 1)
                .Select(g => $"{g.Key}: " + string.Join(" / ", g.Select(k => $"'{k.TabLabel}'").Distinct()))
                .ToList();

            Assert.That(conflicts, Is.Empty, "tabs with more than one label: " + string.Join(", ", conflicts));
        }

        // ---- the registry itself ---------------------------------------------------------------------

        [Test]
        public void TheFlagRegistryIsNotEmpty_AndCarriesTheOpeningBeats()
        {
            // Guard the guard: an empty or partial registry would make EveryQuestIsValid pass for the
            // wrong reason — every unknown flag would be "known" if the set were never populated.
            var known = SettableFlags();

            Assert.That(known, Is.Not.Empty);
            Assert.That(known, Does.Contain(OnboardingFlags.MetGinnyKey));
            Assert.That(known, Does.Contain(OnboardingFlags.ReadLogbookKey));
            Assert.That(known, Does.Contain(OnboardingFlags.OnboardedKey));
            Assert.That(known.Any(f => f.StartsWith(HomeDeeds.KeyPrefix)), Is.True,
                "no deed keys in the registry — did HomeDef assets move out of Data/?");
        }

        // ---- sabotage: prove the rules actually reject ------------------------------------------------

        static QuestDef Sabotage(string id, string title, params (string label, string flag)[] steps)
        {
            var q = ScriptableObject.CreateInstance<QuestDef>();
            q.Id = id; q.Title = title;
            q.Steps = steps.Select(s => new QuestStep { Label = s.label, Flag = s.flag }).ToList();
            return q;
        }

        [Test]
        public void Sabotage_AnUnknownFlagIsRejected()
        {
            var known = SettableFlags();

            // One character off a real flag — the exact bug this whole registry exists to catch.
            var typo = Sabotage("quest.typo", "Typo", ("find Ginny", "met_giny"));

            Assert.That(Problems(typo, known).Any(p => p.Contains("met_giny")), Is.True,
                "a misspelled flag must fail validation, or it ships as a step that never completes");

            // ...and the same quest with the flag spelled right is clean, so the rule is discriminating
            // rather than just always failing.
            var fixed_ = Sabotage("quest.typo", "Typo", ("find Ginny", OnboardingFlags.MetGinnyKey));
            Assert.That(Problems(fixed_, known), Is.Empty);
        }

        [Test]
        public void Sabotage_AnEmptyFlagIsRejected()
        {
            var q = Sabotage("quest.empty_flag", "No flag", ("do the thing", ""));
            Assert.That(Problems(q, SettableFlags()).Any(p => p.Contains("no Flag")), Is.True);
        }

        [Test]
        public void Sabotage_AStepListWithNoStepsIsRejected()
        {
            var q = Sabotage("quest.no_steps", "Nothing to do");
            Assert.That(Problems(q, SettableFlags()).Any(p => p.Contains("no steps")), Is.True);
        }

        [Test]
        public void Sabotage_AMalformedIdIsRejected()
        {
            foreach (string id in new[] { "the_letter", "Quest.The_Letter", "quest.The_Letter", "quest." })
            {
                var q = Sabotage(id, "Title", ("step", OnboardingFlags.MetGinnyKey));
                Assert.That(Problems(q, SettableFlags()).Any(p => p.Contains("snake_case")), Is.True,
                    $"'{id}' should have been rejected as malformed");
            }
        }

        [Test]
        public void Sabotage_ARepeatedFlagIsRejected()
        {
            var q = Sabotage("quest.repeat", "Repeat",
                ("meet her", OnboardingFlags.MetGinnyKey),
                ("meet her again", OnboardingFlags.MetGinnyKey));

            Assert.That(Problems(q, SettableFlags()).Any(p => p.Contains("repeats flag")), Is.True,
                "two steps on one flag both flip at once, so one could never be the current step");
        }

        [Test]
        public void Sabotage_AnOverlongStepIsRejected()
        {
            var q = Sabotage("quest.long", "Long",
                (new string('x', StepCols + 1), OnboardingFlags.MetGinnyKey));

            Assert.That(Problems(q, SettableFlags()).Any(p => p.Contains("over the")), Is.True);
        }

        [Test]
        public void Sabotage_AKnowledgePageWithATooShortSlotIsRejected()
        {
            var k = ScriptableObject.CreateInstance<KnowledgePageDef>();
            k.Id = "knowledge.short_slot"; k.TabId = "tab.gear"; k.TabLabel = "GEAR";
            k.Head = "Head"; k.Body = new List<string> { "body" };
            k.SlotKey = "slot.something"; k.SlotLines = SlotMinLines - 1;

            Assert.That(Problems(k).Any(p => p.Contains("under the kit's minimum")), Is.True);

            // A page with no slot at all is unaffected by SlotLines.
            k.SlotKey = "";
            Assert.That(Problems(k), Is.Empty);
        }
    }
}
