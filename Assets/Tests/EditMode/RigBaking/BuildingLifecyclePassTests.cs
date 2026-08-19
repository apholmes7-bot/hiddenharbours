using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.Art.Editor;   // BuildingLifecycleStates

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// Guards the BUILDING LIFECYCLE PASS — <c>docs/art/rigs/building-lifecycle-kit/</c>, the drop that
    /// gives every iso building seven construction phases and four states of dereliction.
    ///
    /// <para><b>⭐ WHY THIS FIXTURE IS THE WHOLE POINT OF THE ADOPTION.</b> The pass is not a rig; it is
    /// a PASS, wired into three EXISTING host rigs by a two-line hook after <c>build(b)</c>. That makes
    /// it the first change in this repo that edits rigs whose art is already shipped and placed — so the
    /// question that has to be answered in pixels, not prose, is <i>"did adopting it change a single
    /// finished building?"</i> <see cref="TheHookIsAByteExactNoOp_OnEveryHostAndEveryFinishedBuild"/>
    /// answers it by rendering the SAME file with and without the hook in the SAME V8 and comparing
    /// sha256. Byte equality is attainable here and is therefore required — this is one file against
    /// itself, not two transcriptions of one algorithm.</para>
    ///
    /// <para><b>⚠️ AND THE PASS'S OWN FAILURE MODE IS SILENCE, MEASURED TWICE OVER.</b>
    /// <list type="number">
    ///   <item>The hook reads <c>root.BuildingLifecycle</c> and no-ops when it is absent, so a bake
    ///     that forgets to load the pass renders the FINISHED building and writes it to a sheet named
    ///     <c>collapsing</c>. <see cref="TheHostsReachThePass_ThroughTheCatalogsOwnPrerequisite"/> is
    ///     what proves the catalog declaration closes that hole.</item>
    ///   <item><c>normPhase</c>/<c>normDecay</c> fall back to <c>finished</c>/<c>sound</c> for an
    ///     unknown key while <c>active()</c> still returns true — so a misspelled state is a no-op that
    ///     nothing reports. <see cref="AMisspelledStateIsASilentNoOp_WhichIsWhyTheBakeRefusesOne"/>
    ///     measures it, and <c>BuildingLifecycleStates.AssertKnown</c> is what refuses it.</item>
    /// </list></para>
    /// </summary>
    public class BuildingLifecyclePassTests
    {
        // =====================================================================================
        //  the hosts, and the hook they carry
        // =====================================================================================

        /// <summary>The two lines the kit's README calls "the whole patch", byte-exact.</summary>
        const string HookLine1 =
            "const LC=root.BuildingLifecycle;                       // construction phase / dereliction pass";

        const string HookLine2 =
            "if(LC && LC.active(opts)){ const r=LC.apply(faces, MATS, b, opts); faces=r.faces; b=r.b; }";

        /// <summary>
        /// sha256 of <c>buildingLifecycleRig.js</c> with line endings normalised to LF — the drop's own
        /// bytes, as received on <c>feat/building-lifecycle-drop</c>.
        ///
        /// <para>Pinned so a silent RE-EXPORT of the pass shows up here as one failing assert with a
        /// number in it, rather than as a mystery two kits downstream. The rule for this repo's rigs is
        /// that a re-export is REJECTED, not merged (the character-kit lesson) — if this fails, find out
        /// what changed before you update the constant.</para>
        /// </summary>
        const string PassLfSha256 =
            "90b2828289d3868fb7a41df4ae34cf30963bacce15e3f246d91f3848e685a4fc";

        /// <summary>The three host rigs the pass is bound into, and a render the owner has already
        /// shipped art for. Shop Building and Shipyard take a different signature and are explicitly
        /// out of scope for this pass (the kit README's own "coverage and limits").</summary>
        static IEnumerable<TestCaseData> Hosts()
        {
            yield return new TestCaseData("wharfBuilding", "Object.assign({},WharfBuilding.PRESETS['netShed'])")
                .SetName("wharfBuilding_netShed");
            yield return new TestCaseData("wharfBuilding", "Object.assign({},WharfBuilding.PRESETS['cannery'])")
                .SetName("wharfBuilding_cannery");
            yield return new TestCaseData("wharfBuilding", "{}").SetName("wharfBuilding_default");
            yield return new TestCaseData("house", "Object.assign({},HouseIso.PRESETS['redSaltbox'])")
                .SetName("house_redSaltbox");
            yield return new TestCaseData("house", "{}").SetName("house_default");
            yield return new TestCaseData("shopfront", "{}").SetName("shopfront_default");
        }

        // =====================================================================================
        //  1. the hook changes nothing
        // =====================================================================================

        /// <summary>
        /// 🔴 <b>THE LOAD-BEARING ASSERT OF THE WHOLE ADOPTION.</b> With the pass loaded and no
        /// lifecycle keys in the options, each hooked host renders byte-identical to the same source
        /// with the hook removed.
        ///
        /// <para>Both halves run in ONE host, so the engine, the ICU data and the load order are
        /// controls rather than variables — the only difference between the two renders is the two
        /// lines. <c>LC.active(opts)</c> is false for every finished build, so the pass must not be
        /// entered at all; anything less than byte equality means it was.</para>
        /// </summary>
        [Test, TestCaseSource(nameof(Hosts))]
        public void TheHookIsAByteExactNoOp_OnEveryHostAndEveryFinishedBuild(string rigKey, string optsJs)
        {
            var entry = RigCatalog.Get(rigKey);
            string hooked = RigCatalog.ReadSource(entry);
            string plain = WithoutTheHook(hooked, rigKey);

            Assert.AreNotEqual(hooked, plain,
                $"'{rigKey}' does not carry the hook, so this test is comparing a file with itself and " +
                "proves nothing. Adopt the hook or delete this case — do not let it pass vacuously.");

            using var host = RigScriptHostFactory.Create();
            InstallDependencies(host, entry);

            // The pass IS loaded for both halves. That is deliberate: the question is whether the HOOK
            // fires, not whether the pass is present.
            host.Execute(RigCatalog.ReadSource(RigCatalog.Get("buildingLifecycle")));

            host.Execute(plain);
            byte[] before = host.EvaluateBytes($"{entry.GlobalName}.render(0,{optsJs})");

            host.Execute(hooked);
            byte[] after = host.EvaluateBytes($"{entry.GlobalName}.render(0,{optsJs})");

            Assert.Greater(CountOpaque(before), 1000,
                $"{rigKey} drew nothing — the comparison below would be two empty buffers agreeing.");

            Assert.AreEqual(Sha(before), Sha(after),
                $"THE HOOK IS NOT A NO-OP on {rigKey} {optsJs}. Adopting the lifecycle pass has changed " +
                "a finished building, which means every sheet already baked from this rig is now stale. " +
                "Do not re-bake around it — find out why LC.active() fired on a build with no lifecycle " +
                "keys.");

            Debug.Log($"[lifecycle] {rigKey} {optsJs}: no-op parity holds, sha {Sha(after)[..16]}, " +
                      $"{CountOpaque(after)} opaque px");
        }

        /// <summary>
        /// The hook is present, in both lines, in all three hosts — and nowhere else.
        ///
        /// <para>Cheap text check, no engine: the parity test above proves the hook is harmless, but a
        /// hook that was never applied would pass it too (by failing its own vacuity guard, which is why
        /// that guard is there). This is the positive half.</para>
        /// </summary>
        [Test]
        public void AllThreeHostRigs_CarryTheHook_Verbatim(
            [Values("wharfBuilding", "house", "shopfront")] string rigKey)
        {
            string src = RigCatalog.ReadSource(RigCatalog.Get(rigKey));

            StringAssert.Contains(HookLine1, src,
                $"'{rigKey}' is missing the lifecycle hook's first line. Without it the pass is never " +
                "consulted and a phase/decay bake silently renders the finished building.");
            StringAssert.Contains(HookLine2, src,
                $"'{rigKey}' is missing the lifecycle hook's second line.");

            Assert.AreEqual(1, Occurrences(src, HookLine2),
                $"'{rigKey}' carries the hook {Occurrences(src, HookLine2)} times. Two hooks means the " +
                "pass runs twice and the second run surveys a building that has already been ruined.");
        }

        /// <summary>
        /// The pass file is the drop's own bytes. See <see cref="PassLfSha256"/> for why a re-export is
        /// a failure and not an update.
        /// </summary>
        [Test]
        public void ThePassFile_IsTheDropsOwnBytes()
        {
            var entry = RigCatalog.Get("buildingLifecycle");
            string abs = Path.Combine(RigCatalog.RepoRoot, entry.ScriptPath);
            Assert.IsTrue(File.Exists(abs), $"the pass is missing at {entry.ScriptPath}");

            byte[] raw = File.ReadAllBytes(abs);
            byte[] lf = NormaliseToLf(raw);

            Assert.AreEqual(PassLfSha256, Sha(lf),
                "buildingLifecycleRig.js is not the file that was dropped. A rig re-export is REJECTED " +
                "in this repo, not merged — find out what changed and why before touching this pin.");

            Debug.Log($"[lifecycle] pass: {raw.Length} bytes, LF-sha {Sha(lf)[..16]}");
        }

        // =====================================================================================
        //  2. the catalog wiring
        // =====================================================================================

        /// <summary>
        /// Installing a host through the catalog brings the pass with it.
        ///
        /// <para>This is the assert that makes the prerequisite declaration real. The hook reads
        /// <c>root.BuildingLifecycle</c> and no-ops when it is absent, so the whole cost of getting this
        /// wrong is a sheet named <c>collapsing</c> holding a pristine building — no throw, no warning,
        /// correct sprite count.</para>
        /// </summary>
        [Test]
        public void TheHostsReachThePass_ThroughTheCatalogsOwnPrerequisite(
            [Values("wharfBuilding", "house", "shopfront")] string rigKey)
        {
            var entry = RigCatalog.Get(rigKey);
            CollectionAssert.Contains(entry.Prerequisites, "buildingLifecycle",
                $"'{rigKey}' does not declare the pass as a prerequisite, so a bake reaches it only by " +
                "luck of what else the host happens to have loaded.");

            using var host = RigScriptHostFactory.Create();
            RigCatalog.Install(host, entry);

            Assert.IsTrue(host.EvaluateBool("typeof BuildingLifecycle === 'object' && BuildingLifecycle !== null"),
                $"installing '{rigKey}' did not bring the pass with it.");
            Assert.IsTrue(host.EvaluateBool("typeof BuildingLifecycle.apply === 'function'"),
                "the global installed but has no apply() — the pass changed shape.");
        }

        // =====================================================================================
        //  3. the tables — ids a save file will one day name
        // =====================================================================================

        /// <summary>
        /// The state tables are exactly what the kit README documents, IN ORDER.
        ///
        /// <para>These are ids, not labels: a building's state is what a save file will name once repair
        /// and construction become gameplay (backlog M3). Treating them as append-only from day one is
        /// CLAUDE.md §5, and pinning the order here is what makes a silent re-ordering — which would
        /// remap every saved building to a different state — a failing test instead of a bug report.</para>
        /// </summary>
        [Test]
        public void ThePhaseAndDecayTables_AreTheDocumentedIds_InOrder()
        {
            using var host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, RigCatalog.Get("buildingLifecycle"));

            Assert.AreEqual("site,foundation,frame,rafters,sheathed,cladding,finished",
                            host.EvaluateString("BuildingLifecycle.PHASES.join(',')"),
                            "the seven construction phases, in build order");

            Assert.AreEqual("sound,neglected,abandoned,collapsing,ruin",
                            host.EvaluateString("BuildingLifecycle.DECAY.join(',')"),
                            "the decay ladder, kept-up first");

            // Every id carries a label and a note — the strings a repair/construction UI would show.
            foreach (string table in new[] { "PHASES", "DECAY" })
            {
                string kind = table == "PHASES" ? "PHASE" : "DECAY";
                Assert.IsTrue(host.EvaluateBool(
                    $"BuildingLifecycle.{table}.every(function(k){{ return " +
                    $"typeof BuildingLifecycle.{kind}_LABEL[k]==='string' && " +
                    $"typeof BuildingLifecycle.{kind}_NOTE[k]==='string'; }})"),
                    $"an id in {table} has no {kind}_LABEL or {kind}_NOTE entry.");
            }
        }

        /// <summary>
        /// <c>active()</c> is the gate the hook asks, and it must be false for exactly the finished,
        /// sound, unburnt building — that is what makes the no-op above structural rather than lucky.
        /// </summary>
        [Test]
        public void Active_IsFalseForAFinishedBuilding_AndTrueForEveryRealState()
        {
            using var host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, RigCatalog.Get("buildingLifecycle"));

            foreach (string opts in new[] { "{}", "{phase:'finished'}", "{decay:'sound'}",
                                            "{phase:'finished',decay:'sound'}", "{burnt:false}" })
                Assert.IsFalse(host.EvaluateBool($"!!BuildingLifecycle.active({opts})"),
                               $"active({opts}) must be false — the pass has nothing to do.");

            foreach (string opts in new[] { "{phase:'site'}", "{phase:'frame'}", "{decay:'neglected'}",
                                            "{decay:'ruin'}", "{burnt:true}" })
                Assert.IsTrue(host.EvaluateBool($"!!BuildingLifecycle.active({opts})"),
                              $"active({opts}) must be true or the state never renders.");
        }

        // =====================================================================================
        //  4. the ladder — determinism, and that the states are actually different
        // =====================================================================================

        /// <summary>
        /// Every rung of the ladder draws a DIFFERENT building, and the same rung twice draws the same
        /// one.
        ///
        /// <para>Determinism is a rule (CLAUDE.md §5) and here it is also a bake requirement: the pass
        /// seeds its PRNG from the building's own span, eave height, phase and decay, so a re-bake of a
        /// shipped sheet has to come back byte-identical or the committed art churns on every run.</para>
        ///
        /// <para>Distinctness is the other half. <c>normPhase</c>/<c>normDecay</c> silently fall back to
        /// finished/sound, so "12 distinct renders" is the cheapest proof that all twelve keys were
        /// understood — one that fell back would show up as a duplicate of <c>finished</c>.</para>
        /// </summary>
        [Test]
        public void TheLadderIsTwelveDistinctStates_AndEachOneIsDeterministic()
        {
            var entry = RigCatalog.Get("wharfBuilding");
            using var host = RigScriptHostFactory.Create();
            RigCatalog.Install(host, entry);

            const string bas = "Object.assign({},WharfBuilding.PRESETS['netShed'])";
            var byState = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string phase in host.EvaluateString("BuildingLifecycle.PHASES.join(',')").Split(','))
                byState["phase:" + phase] = Sha(host.EvaluateBytes(
                    $"WharfBuilding.render(0,Object.assign({bas},{{phase:'{phase}'}}))"));

            foreach (string decay in host.EvaluateString("BuildingLifecycle.DECAY.join(',')")
                                         .Split(',').Skip(1))
                byState["decay:" + decay] = Sha(host.EvaluateBytes(
                    $"WharfBuilding.render(0,Object.assign({bas},{{decay:'{decay}'}}))"));

            byState["burnt:ruin"] = Sha(host.EvaluateBytes(
                $"WharfBuilding.render(0,Object.assign({bas},{{decay:'ruin',burnt:true}}))"));

            var duplicates = byState.GroupBy(kv => kv.Value)
                                    .Where(g => g.Count() > 1)
                                    .Select(g => string.Join(" == ", g.Select(kv => kv.Key)))
                                    .ToArray();

            CollectionAssert.IsEmpty(duplicates,
                "two states of the netShed drew the SAME pixels: " + string.Join("; ", duplicates) +
                ". A state that matches 'phase:finished' was not understood and fell back silently.");

            Assert.AreEqual(12, byState.Count,
                "7 phases + 4 decay + burnt is the ladder the README documents");

            // …and the same state twice is the same bytes. Rendered at a different facing from the ones
            // above so this is a fresh call rather than a cache read.
            string once = Sha(host.EvaluateBytes(
                $"WharfBuilding.render(3,Object.assign({bas},{{decay:'collapsing'}}))"));
            string twice = Sha(host.EvaluateBytes(
                $"WharfBuilding.render(3,Object.assign({bas},{{decay:'collapsing'}}))"));

            Assert.AreEqual(once, twice,
                "a collapsing net shed rendered twice came back different. The pass seeds from the " +
                "building's own dimensions and state — if that is no longer true, every committed " +
                "lifecycle sheet churns on re-bake.");

            Debug.Log($"[lifecycle] netShed ladder: {byState.Count} distinct states, collapsing " +
                      $"deterministic at sha {once[..16]}");
        }

        /// <summary>
        /// 🔴 <b>A MISSPELLED STATE RENDERS THE FINISHED BUILDING AND SAYS NOTHING.</b> Measured, not
        /// feared: <c>decay:'collapsed'</c> — the natural misspelling of <c>collapsing</c> — makes
        /// <c>active()</c> return true (it is not <c>'sound'</c>), then <c>normDecay</c> falls back to
        /// <c>sound</c> and the pass returns the faces untouched.
        ///
        /// <para>This test PINS the trap rather than the fix, so it stays true when the fix moves.
        /// <see cref="BuildingLifecycleStates.AssertKnown"/> is what refuses it, and
        /// <c>BuildingLifecycleSetTests</c> is what proves the refusal reaches a bake.</para>
        /// </summary>
        [Test]
        public void AMisspelledStateIsASilentNoOp_WhichIsWhyTheBakeRefusesOne()
        {
            var entry = RigCatalog.Get("wharfBuilding");
            using var host = RigScriptHostFactory.Create();
            RigCatalog.Install(host, entry);

            const string bas = "Object.assign({},WharfBuilding.PRESETS['netShed'])";
            string finished = Sha(host.EvaluateBytes($"WharfBuilding.render(0,{bas})"));
            string typo = Sha(host.EvaluateBytes(
                $"WharfBuilding.render(0,Object.assign({bas},{{decay:'collapsed'}}))"));

            Assert.AreEqual(finished, typo,
                "the silent-typo trap has changed shape. It used to be that an unknown decay key " +
                "rendered the finished building; if it now renders something else, re-read the pass's " +
                "normDecay and re-word BuildingLifecycleStates.AssertKnown's message to match.");

            // And the guard refuses exactly that, on both axes, before it can reach a sheet name.
            Assert.Throws<ArgumentException>(() => BuildingLifecycleStates.AssertKnown("finished", "collapsed"));
            Assert.Throws<ArgumentException>(() => BuildingLifecycleStates.AssertKnown("framed", "sound"));
            Assert.DoesNotThrow(() => BuildingLifecycleStates.AssertKnown("frame", "collapsing"));
            Assert.DoesNotThrow(() => BuildingLifecycleStates.AssertKnown(null, "ruin"));
        }

        /// <summary>
        /// The C# state table and the pass's own tables are the same lists. <see cref="BuildingLifecycleStates"/>
        /// exists so a bake can refuse a typo without starting a JS engine; this is what stops the two
        /// copies drifting.
        /// </summary>
        [Test]
        public void TheCSharpStateTable_IsThePassesOwnTable()
        {
            using var host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, RigCatalog.Get("buildingLifecycle"));

            CollectionAssert.AreEqual(
                host.EvaluateString("BuildingLifecycle.PHASES.join(',')").Split(','),
                BuildingLifecycleStates.Phases,
                "BuildingLifecycleStates.Phases has drifted from the pass's PHASES table");

            CollectionAssert.AreEqual(
                host.EvaluateString("BuildingLifecycle.DECAY.join(',')").Split(','),
                BuildingLifecycleStates.Decays,
                "BuildingLifecycleStates.Decays has drifted from the pass's DECAY table");
        }

        // =====================================================================================
        //  helpers
        // =====================================================================================

        /// <summary>
        /// Install whatever the host needs EXCEPT the pass — the prerequisites minus
        /// <c>buildingLifecycle</c>, so the parity test controls that variable itself.
        /// </summary>
        static void InstallDependencies(IRigScriptHost host, RigEntry entry)
        {
            foreach (string key in entry.Prerequisites)
            {
                if (key == "buildingLifecycle") continue;
                RigCatalog.InstallModule(host, RigCatalog.Get(key));
            }
        }

        /// <summary>
        /// The rig source with the two hook lines removed and the <c>let</c> they needed put back to
        /// <c>const</c> — i.e. the file exactly as it stood before the adoption.
        ///
        /// <para>Done on the decoded string in memory, never via a file: these rigs carry multi-byte
        /// UTF-8 and a PowerShell round-trip mangles it.</para>
        /// </summary>
        static string WithoutTheHook(string src, string rigKey)
        {
            var kept = src.Split('\n')
                          .Where(line => !line.Contains(HookLine1) && !line.Contains(HookLine2));
            string stripped = string.Join("\n", kept);

            // The hook needs `b` and `faces` to be reassignable; the pre-hook file declared them const.
            // ⚠️ The one-line (shopfront) shape goes FIRST: its text begins with the two-line shape's
            // `    let b=resolve(opts);`, so putting the two-line replacements ahead of it would eat
            // that prefix and leave the third pattern unmatched — silently, and still rendering.
            stripped = stripped
                .Replace("    let b=resolve(opts); const MATS=makeMats(b); let faces=build(b);",
                         "    const b=resolve(opts), MATS=makeMats(b), faces=build(b);")
                .Replace("    let b=resolve(opts);", "    const b=resolve(opts);")
                .Replace("    let faces=build(b);", "    const faces=build(b);");

            Assert.IsFalse(stripped.Contains("    let b=resolve(opts)"),
                $"'{rigKey}' still declares b with let after the strip, so the pre-hook shape was not " +
                "restored and this comparison is not against the file that shipped.");

            Assert.IsFalse(stripped.Contains("BuildingLifecycle"),
                $"stripping the hook out of '{rigKey}' left a reference to the pass behind, so the " +
                "'before' half of the comparison is not actually hook-free.");
            return stripped;
        }

        static int Occurrences(string haystack, string needle)
        {
            int n = 0;
            for (int i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
                 i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)) n++;
            return n;
        }

        static byte[] NormaliseToLf(byte[] raw)
        {
            var outp = new List<byte>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == (byte)'\r' && i + 1 < raw.Length && raw[i + 1] == (byte)'\n') continue;
                outp.Add(raw[i]);
            }
            return outp.ToArray();
        }

        static string Sha(byte[] b)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(b)).Replace("-", "").ToLowerInvariant();
        }

        static int CountOpaque(byte[] rgba)
        {
            int n = 0;
            for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] > 0) n++;
            return n;
        }
    }
}
