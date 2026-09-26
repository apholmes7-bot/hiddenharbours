using System;
using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>RIG 9'S APPEARANCE CONTRACT (<c>data/options.v9.json</c>, rev 9.2), HELD TO THE RIG.</b>
    ///
    /// <para>The character creator offers what the options file lists, and the game bakes what the
    /// rig builds. The two are only the same thing if every option the file offers is one the rig
    /// fits, and if no build the creator can make paints more materials than a v9 skin can carry.
    /// Both are measured here against the rig itself, in its own script host, so a new drop that
    /// moves either one reds here before it reaches a bake (APPEARANCE-MAP.md §9).</para>
    ///
    /// <para>The audit logic is <see cref="CharacterSkinExtractor.AuditOptions9"/> and
    /// <see cref="CharacterSkinExtractor.WorstCreatorBuild9"/>; the counts below are recounted from
    /// the file independently, so a reader that skipped an axis cannot pass by agreeing with itself.</para>
    /// </summary>
    public class CharacterRig9OptionGuardTests
    {
        /// <summary>
        /// The axes whose options only change the figure when it wears something first, from the
        /// file's <c>rules.sameFigure</c>: an outfit brings its own bottom (so a bottom is judged on a
        /// tee), no hat paints no hat colour (so a hat colour is judged on the watch cap), and only an
        /// apron paints the apron colour. Every other axis is judged on the file's defaults.
        /// </summary>
        static readonly Dictionary<string, (string axis, string value)> Contexts =
            new Dictionary<string, (string axis, string value)>(StringComparer.Ordinal)
            {
                ["bottom"] = ("garment", "tee"),
                ["hatCol"] = ("hat", "watchcap"),
                ["apronCol"] = ("garment", "apron"),
            };

        /// <summary>Random creator builds the worst-case count is checked against, and their seed.
        /// Fixed, so a red names the same build on every run.</summary>
        const int Samples = 400;
        const uint Seed = 20260925u;

        IRigScriptHost _host;
        string _options;
        OptionAudit9 _audit;
        CreatorWorst9 _worst;

        OptionAudit9 Audit => _audit ??= CharacterSkinExtractor.AuditOptions9(_host, _options, Contexts);

        CreatorWorst9 Worst => _worst ??= CharacterSkinExtractor.WorstCreatorBuild9(_host, _options, Samples, Seed);

        [OneTimeSetUp]
        public void LoadOnce()
        {
            _host = RigScriptHostFactory.Create();
            CharacterSkinExtractor.Load9(_host);
            _options = CharacterSkinExtractor.ReadKitText9(CharacterSkinExtractor.V9KitRoot,
                                                           CharacterSkinExtractor.V9OptionsFile);
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            _host?.Dispose();
            _host = null; _options = null; _audit = null; _worst = null;
        }

        int CountInFile(string body) =>
            (int)_host.EvaluateNumber("(function(O){" + body + "})(" + _options + ")");

        // =======================================================================================
        // 1. every option the creator offers is one the rig fits
        // =======================================================================================

        /// <summary>
        /// Each offered option, set on its axis's context build, must be an option the rig accepts
        /// (it round-trips through <c>buildKey</c>), draw a figure different from the axis default,
        /// and paint only materials the contract gives a ramp. The count is recounted from the file,
        /// and pinned: 136 is what the 9.2 drop offers, so a drop that moves it is a new intake and
        /// has to say so.
        /// </summary>
        [Test]
        public void EveryOfferedOptionResolvesOnTheRig()
        {
            OptionAudit9 a = Audit;
            Assert.IsEmpty(a.Problems,
                "Options the creator offers that the rig does not fit:\n  " + string.Join("\n  ", a.Problems));

            int inFile = CountInFile(
                "var n=0;O.axes.forEach(function(a){if(a.creator&&a.creator.offered)n+=a.options.length;});return n;");
            Assert.AreEqual(inFile, a.Offered,
                $"The file offers {inFile} options on its offered axes and the audit checked {a.Offered}: " +
                "the audit skipped options, so the empty problem list above proves nothing about them.");
            Assert.AreEqual(136, a.Offered,
                "The 9.2 drop offers 136 options. A drop that moves the count is a new intake: land it " +
                "as one, re-run the fit and the material audit on it, and move this pin in that PR.");
        }

        /// <summary>
        /// Ruling 2 (2026-09-24): youth, adult and elder bodies are the player's, children are the
        /// cast's. The file makes age a start-preset choice rather than a control, so the creator
        /// reaches exactly the ages of the cast members it may start from: every cast age except the
        /// ones the file's <c>rules.notOffered</c> keeps back, and that must be the children alone.
        /// </summary>
        [Test]
        public void TheCreatorReachesYouthAdultAndElderButNeverAChild()
        {
            OptionAudit9 a = Audit;
            CollectionAssert.AreEqual(new[] { "age.child" }, a.NotOffered,
                "Ruling 2 keeps children out of the creator and nothing else; the file's notOffered is " +
                $"[{string.Join(", ", a.NotOffered)}].");

            string g = CharacterSkinExtractor.V9GlobalName;
            var starts = new SortedSet<string>(StringComparer.Ordinal);
            var kept = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string preset in CharacterSkinExtractor.Presets9(_host))
            {
                string age = _host.EvaluateString($"String({g}.normBuild('{preset}').age)");
                if (a.NotOffered.Contains("age." + age)) kept.Add(age);
                else starts.Add(age);
            }
            Assert.AreEqual("adult,elder,youth", string.Join(",", starts),
                "The creator keeps the age of the preset it starts from, so the ages it can reach are " +
                "those of the cast it may start from. Ruling 2 wants youth, adult and elder.");
            Assert.AreEqual("child", string.Join(",", kept),
                "The cast should still hold the children the creator does not offer (the Wharf boy and girl).");
        }

        // =======================================================================================
        // 2. no creator build paints more than a v9 skin can carry
        // =======================================================================================

        /// <summary>
        /// The worst build over every combination of the offered axes, counted the way the baker
        /// counts a def's materials (each painted material once, on the rest face and on the whole
        /// face), must fit <see cref="CharacterSkinDef.MaxMaterials"/> for <see cref="ToneRule.V9"/>.
        /// The worst case is worked out per axis, so it is also checked by painting sampled builds
        /// on the rig and every cast preset: a count that disagrees with the rig anywhere means the
        /// per-axis sum is wrong and the worst case with it.
        /// </summary>
        [Test]
        public void NoCreatorBuildPaintsMoreThanTheV9MaterialLimit()
        {
            CreatorWorst9 w = Worst;
            int limit = CharacterSkinDef.MaxMaterials(ToneRule.V9);

            Assert.Greater(w.Combinations, 0, "The audit met no creator build at all.");
            Assert.AreEqual(Samples, w.Sampled, "The audit painted fewer sampled builds than it was asked to.");
            Assert.AreEqual(0, w.Mismatches,
                $"The per-axis count disagrees with what the rig paints on {w.Mismatches} sampled builds, " +
                $"so the worst case is not the worst case. First: {w.FirstMismatch}");
            Assert.IsEmpty(w.Strays ?? "",
                "The rig paints materials the options contract does not list, so no count of the contract " +
                $"can bound the bake: {w.Strays}");
            Assert.IsEmpty(w.PresetDisagreements,
                "Cast presets whose painted materials the per-axis count gets wrong:\n  " +
                string.Join("\n  ", w.PresetDisagreements));

            Assert.LessOrEqual(w.DefaultFace, limit,
                $"The worst creator build paints {w.DefaultFace} materials on the rest face ({w.DefaultFaceWitness}), " +
                $"over the {limit} a v9 skin can carry.");
            Assert.LessOrEqual(w.AllGroups, limit,
                $"The worst creator build paints {w.AllGroups} materials over every face group " +
                $"({w.AllGroupsWitness}), over the {limit} a v9 skin can carry; ruling 8 has the engine " +
                "play blink and gaze, so the whole face has to fit, not only the rest face.");
        }

        // =======================================================================================
        // 3. the fixed colours the file documents as a mix hold for every option
        // =======================================================================================

        /// <summary>
        /// The hair axis fixes each option's brow colour and documents it as
        /// <c>mix(hair ramp [0], ink, 0.34)</c>. The rig does not export its ink, so the audit finds
        /// the inks that give every hair option its fixed brow and passes only if one ink does. This
        /// is the audit saying it met the rule, on every hair option, rather than skipping it.
        /// </summary>
        [Test]
        public void TheDocumentedBrowMixHoldsForEveryHairColour()
        {
            OptionAudit9 a = Audit;
            int hairs = CountInFile(
                "for(var i=0;i<O.axes.length;i++)if(O.axes[i].id==='hair')return O.axes[i].options.length;return -1;");

            Assert.AreEqual(1, a.Derived.Count,
                "The file documents one mixed colour (the brow); the audit derived " +
                $"[{string.Join("; ", a.Derived)}].");
            StringAssert.StartsWith($"hair.brow = mix(ramp [0], ink, 0.34) on {hairs} options, one ink in #",
                a.Derived[0], "The brow mix was not checked on every hair option.");
        }
    }
}
