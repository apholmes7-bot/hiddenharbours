using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE WHEEL, THE OARS AND THE CARRIES PLAY THE RIG'S OWN CLIPS</b> (character PR 2a, A3).
    ///
    /// <para>The runtime spells a carry clip's key in Core (<see cref="CharacterSkinStateMap.CarryKey"/>),
    /// because the presenters cannot see the sheet baker's <see cref="CharacterState"/>. The two spellings
    /// must never drift, so the twin is held to <c>CharacterState(anim, null, carry).Key</c> over every
    /// carry clip the rig declares and every carry clip the ten committed defs carry.</para>
    ///
    /// <para>Every name these guards expect comes from the rig: its clip table (<c>clipNames()</c> and
    /// <c>clipDef()</c>) says which carries exist and which anims each rides, so a clip the rig adds or
    /// drops moves the bar with it. The state map's <see cref="CharacterSkinStateMap.HelmCarry"/> and
    /// <see cref="CharacterSkinStateMap.OarsCarry"/> are the code under test, never the bar.</para>
    ///
    /// <para><see cref="CarryAnchorTableDef"/> is a different table (rig 6's hand anchors for the sprite
    /// overlay) and is not read here; the carriable-to-carry rows are
    /// <see cref="CharacterCarryPoseDef"/>.</para>
    /// </summary>
    public class CharacterSkinCarryStateTests
    {
        const string CarryPosesPath = "Assets/_Project/Resources/" + CharacterCarryPoseDef.ResourcesPath + ".asset";

        IRigScriptHost _host;
        List<(string name, string anim, string carry)> _clips;

        IRigScriptHost Host
        {
            get
            {
                if (_host == null)
                {
                    _host = RigScriptHostFactory.Create();
                    CharacterSkinExtractor.Load9(_host);
                }
                return _host;
            }
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            _host?.Dispose();
            _host = null;
        }

        /// <summary>The rig's clip table: every clip name with its anim and carry (empty for none), as
        /// <c>clipDef</c> states them.</summary>
        List<(string name, string anim, string carry)> RigClips()
        {
            if (_clips != null) return _clips;
            string g = CharacterSkinExtractor.V9GlobalName;
            string rows = Host.EvaluateString(
                "(function(){var G=" + g + ";return G.clipNames().map(function(n){var d=G.clipDef(n);" +
                "return n+'|'+d.anim+'|'+(d.carry||'');}).join('\\n');})()");
            _clips = new List<(string, string, string)>();
            foreach (string row in rows.Split('\n'))
            {
                string[] p = row.Split('|');
                Assert.AreEqual(3, p.Length, $"harness: the rig's clip row '{row}' did not read as name|anim|carry");
                _clips.Add((p[0], p[1], p[2]));
            }
            Assert.IsNotEmpty(_clips, "harness: the rig lists no clips");
            return _clips;
        }

        IEnumerable<(string name, string anim, string carry)> RigCarryClips() =>
            RigClips().Where(c => c.carry.Length > 0);

        string[] Cast() => CharacterSkinExtractor.Presets9(Host);

        static CharacterSkinDef Committed(string preset)
        {
            string path = CharacterSkinAssetBaker.AssetPathFor(preset);
            var def = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(path);
            Assert.IsNotNull(def, $"harness: no committed skin def at {path}");
            return def;
        }

        static string AnimOf(CharacterGait gait) => CharacterSkinStateMap.GaitKey(gait);

        // =======================================================================================
        // the key twin
        // =======================================================================================

        /// <summary>
        /// The Core twin spells every carry clip of the rig exactly as the sheet baker keys it, and
        /// every carry clip a committed def carries is keyed by it: the runtime asks for the name the
        /// bake wrote.
        /// </summary>
        [Test]
        public void TheCoreCarryKeyIsTheBakersKeyForEveryCarryClip()
        {
            int rig = 0;
            foreach ((string name, string anim, string carry) in RigCarryClips())
            {
                Assert.AreEqual(new CharacterState(anim, null, carry).Key, CharacterSkinStateMap.CarryKey(anim, carry),
                    $"rig clip '{name}' ({anim} carrying {carry})");
                rig++;
            }
            Assert.Greater(rig, 0, "the rig declares no carry clip, so the twin was held to nothing");

            int baked = 0;
            foreach (string preset in Cast())
            {
                CharacterSkinDef def = Committed(preset);
                foreach (CharacterSkinDef.SkinClip clip in def.Clips)
                {
                    if (string.IsNullOrEmpty(clip.Carry)) continue;
                    Assert.IsTrue(string.IsNullOrEmpty(clip.Power),
                        $"'{preset}' clip '{clip.StateKey}' carries a power as well as a carry; the twin spells no power");
                    string key = new CharacterState(clip.Anim, null, clip.Carry).Key;
                    Assert.AreEqual(key, CharacterSkinStateMap.CarryKey(clip.Anim, clip.Carry),
                        $"'{preset}' clip '{clip.StateKey}' ({clip.Anim} carrying {clip.Carry})");
                    Assert.AreEqual(key, clip.StateKey,
                        $"'{preset}' keys its {clip.Anim}/{clip.Carry} clip '{clip.StateKey}', not the baker's '{key}'");
                    baked++;
                }
            }
            Assert.Greater(baked, 0, "no committed def carries a carry clip, so the twin was held to no bake");
            TestContext.WriteLine($"[carry] twin held over {rig} rig carry clips and {baked} committed carry clips");
        }

        // =======================================================================================
        // the wheel and the oars
        // =======================================================================================

        /// <summary>
        /// The map's two stance carries are carries the rig has, and each rides the rig's idle and walk:
        /// the spelling is the code under test, and the rig's clip table is the bar.
        /// </summary>
        [Test]
        public void TheWheelAndTheOarsAreCarriesTheRigRidesOnIdleAndWalk()
        {
            foreach (string carry in new[] { CharacterSkinStateMap.HelmCarry, CharacterSkinStateMap.OarsCarry })
                foreach (CharacterGait gait in new[] { CharacterGait.Idle, CharacterGait.Walk })
                {
                    string anim = AnimOf(gait);
                    Assert.IsTrue(RigCarryClips().Any(c => c.carry == carry && c.anim == anim),
                        $"the rig has no clip of '{anim}' carrying '{carry}'; its carry clips are " +
                        string.Join(", ", RigCarryClips().Select(c => $"{c.name}={c.anim}/{c.carry}")));
                }
            foreach (string carry in new[] { CharacterSkinStateMap.HelmCarry, CharacterSkinStateMap.OarsCarry })
                Assert.IsFalse(RigCarryClips().Any(c => c.carry == carry && c.anim == CharacterSkinStateMap.Run),
                    $"the rig now bakes a run carrying '{carry}': the map's 'a stance's run is the free run' rule " +
                    "is out of date");
        }

        /// <summary>
        /// Every committed def resolves the wheel and the oars, idle and walk, to the rig's own clip for
        /// them — no fallback, switched on as a mesh — and a run at either is the free run the def
        /// carries. Any def that fell back is listed, never skipped.
        /// </summary>
        [Test]
        public void EveryCommittedDefPlaysTheRigsClipsAtTheWheelAndTheOars()
        {
            var stances = new[]
            {
                (CharacterStance.Helm, CharacterSkinStateMap.HelmCarry),
                (CharacterStance.Oars, CharacterSkinStateMap.OarsCarry),
            };
            var fellBack = new List<string>();
            int resolved = 0;
            foreach (string preset in Cast())
            {
                CharacterSkinDef def = Committed(preset);
                foreach ((CharacterStance stance, string carry) in stances)
                {
                    foreach (CharacterGait gait in new[] { CharacterGait.Idle, CharacterGait.Walk })
                    {
                        string anim = AnimOf(gait);
                        (string name, string anim, string carry) rigClip =
                            RigCarryClips().First(c => c.carry == carry && c.anim == anim);
                        string want = new CharacterState(anim, null, carry).Key;

                        Assert.IsTrue(CharacterSkinStateMap.Resolve(def, stance, gait, out string key, out bool fell),
                            $"'{preset}' resolves nothing at {stance}/{gait}");
                        if (fell) { fellBack.Add($"{preset} {stance}/{gait} -> {key}"); continue; }
                        Assert.AreEqual(want, key, $"'{preset}' at {stance}/{gait} (the rig's '{rigClip.name}')");
                        Assert.IsTrue(def.TryGetClip(key, out CharacterSkinDef.SkinClip clip));
                        Assert.AreEqual(anim, clip.Anim, $"'{preset}' clip '{key}'");
                        Assert.AreEqual(carry, clip.Carry, $"'{preset}' clip '{key}'");
                        Assert.IsTrue(def.DrawsAsMesh(key), $"'{preset}' does not switch '{key}' on as a mesh");
                        resolved++;
                    }

                    Assert.IsTrue(CharacterSkinStateMap.Resolve(def, stance, CharacterGait.Run, out string run, out bool runFell));
                    Assert.AreEqual(CharacterSkinStateMap.Run, run, $"'{preset}' at {stance}/Run");
                    Assert.IsFalse(runFell, $"'{preset}' at {stance}/Run: the free run is what is asked for");
                }
            }
            Assert.IsEmpty(fellBack,
                "A committed def fell back to the free gait at the wheel or the oars:\n  " + string.Join("\n  ", fellBack));
            TestContext.WriteLine($"[carry] {resolved} wheel/oars states resolved to the rig's clips, no fallback");
        }

        // =======================================================================================
        // the carriables
        // =======================================================================================

        /// <summary>
        /// Every row of the shipped carry-pose table names a carry the rig has (and not a stance's), and
        /// every committed def plays that carry's clip for each gait the rig rides it on, switched on as a
        /// mesh. The rig's carries no row maps are named in the output: they have no carriable yet.
        /// </summary>
        [Test]
        public void EveryMappedCarriableAsksForACarryTheRigHasAndEveryDefPlaysIt()
        {
            var table = AssetDatabase.LoadAssetAtPath<CharacterCarryPoseDef>(CarryPosesPath);
            Assert.IsNotNull(table, $"harness: no carry-pose table at {CarryPosesPath}");
            Assert.IsNotEmpty(table.Rows, "the carry-pose table maps nothing");

            var stanceCarries = new HashSet<string>(StringComparer.Ordinal)
                { CharacterSkinStateMap.HelmCarry, CharacterSkinStateMap.OarsCarry };
            var mapped = new HashSet<string>(StringComparer.Ordinal);
            var fellBack = new List<string>();
            foreach (CharacterCarryPoseDef.Row row in table.Rows)
            {
                Assert.IsFalse(string.IsNullOrEmpty(row.CarriableDefId), "a carry-pose row names no carriable");
                Assert.IsFalse(stanceCarries.Contains(row.Carry),
                    $"'{row.CarriableDefId}' asks for '{row.Carry}', which is a stance the state map reaches, not a carry");
                string[] anims = RigCarryClips().Where(c => c.carry == row.Carry).Select(c => c.anim).ToArray();
                Assert.IsNotEmpty(anims, $"'{row.CarriableDefId}' asks for '{row.Carry}', which the rig does not carry");
                Assert.AreEqual(row.Carry, table.CarryFor(row.CarriableDefId), $"the table's own read of '{row.CarriableDefId}'");
                mapped.Add(row.Carry);

                foreach (string preset in Cast())
                {
                    CharacterSkinDef def = Committed(preset);
                    foreach (CharacterGait gait in new[] { CharacterGait.Idle, CharacterGait.Walk, CharacterGait.Run })
                    {
                        string anim = AnimOf(gait);
                        if (!anims.Contains(anim)) continue;
                        string want = new CharacterState(anim, null, row.Carry).Key;
                        Assert.IsTrue(CharacterSkinStateMap.Resolve(def, CharacterStance.Free, gait, row.Carry,
                                                                    out string key, out bool fell));
                        if (fell) { fellBack.Add($"{preset} {row.CarriableDefId} {gait} -> {key}"); continue; }
                        Assert.AreEqual(want, key, $"'{preset}' carrying '{row.CarriableDefId}' at {gait}");
                        Assert.IsTrue(def.DrawsAsMesh(key), $"'{preset}' does not switch '{key}' on as a mesh");
                    }
                }
            }
            Assert.IsEmpty(fellBack, "A committed def lacks a mapped carry's clip:\n  " + string.Join("\n  ", fellBack));

            string[] unmapped = RigCarryClips().Select(c => c.carry).Distinct()
                .Where(c => !stanceCarries.Contains(c) && !mapped.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToArray();
            TestContext.WriteLine($"[carry] mapped: {string.Join(", ", mapped)}; the rig's carries with no carriable: " +
                                  (unmapped.Length == 0 ? "none" : string.Join(", ", unmapped)));
        }
    }
}
