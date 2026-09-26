using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE RIG 9 BAKE (characterIsoRig9.js rev 9.2), HELD TO THE SAME BAR AS RIG 7'S.</b>
    ///
    /// <para><see cref="CharacterSkinAssetBaker.ComposeV9"/> bakes every cast preset from the rig
    /// under <see cref="ToneRule.V9"/>: one skeleton, one bind mesh of the preset's rest face, and
    /// the rig's 53 clips as bone keys. These guards replay each def the way a
    /// <see cref="SkinnedMeshRenderer"/> would and hold it to the rig's own posed geometry, count what
    /// it paints against <see cref="CharacterSkinDef.MaxMaterials"/> for v9, and check that it
    /// carries every clip of the committed export (<c>builds/&lt;preset&gt;.v9.json</c>) with the
    /// export's timing.</para>
    ///
    /// <para>The ten bakes are composed once, on first use, in a host of their own; rig 7's
    /// <see cref="ComposeOnce"/> host never has rig 9 installed.</para>
    /// </summary>
    public partial class CharacterSkinBakeGuardTests
    {
        /// <summary>
        /// The replay bar for a v9 def: its float skinning against the rig's double-precision
        /// <c>posed()</c>. Measured headless on the 9.2 drop over all ten presets and every frame
        /// (7.47M corner-frames), the worst is 4.92e-7 m (deckboss <c>astrideStand</c> frame 3) and no
        /// corner reaches 1e-6 m, so 1e-5 m leaves twenty times headroom for float rounding and still
        /// sits ten times tighter than rig 7's 1e-4 m.
        /// </summary>
        const double V9ReplayTolerance = 1e-5;

        IRigScriptHost _v9Host;
        readonly Dictionary<string, CharacterSkinAssetBaker.SkinBake> _v9Bakes =
            new Dictionary<string, CharacterSkinAssetBaker.SkinBake>(StringComparer.Ordinal);

        IRigScriptHost V9Host
        {
            get
            {
                if (_v9Host == null)
                {
                    _v9Host = RigScriptHostFactory.Create();
                    CharacterSkinExtractor.Load9(_v9Host);
                }
                return _v9Host;
            }
        }

        CharacterSkinAssetBaker.SkinBake V9Bake(string preset)
        {
            if (!_v9Bakes.TryGetValue(preset, out CharacterSkinAssetBaker.SkinBake bake))
                _v9Bakes[preset] = bake = CharacterSkinAssetBaker.ComposeV9(V9Host, preset);
            return bake;
        }

        [OneTimeTearDown]
        public void DisposeV9()
        {
            foreach (CharacterSkinAssetBaker.SkinBake bake in _v9Bakes.Values) DestroyDef(bake.Def);
            _v9Bakes.Clear();
            _v9Host?.Dispose();
            _v9Host = null;
        }

        /// <summary>One value of the committed export of <paramref name="preset"/>, as the rig's
        /// host reads it (<c>B</c> is the export).</summary>
        string V9Export(string preset, string expression) =>
            V9Host.EvaluateString("(function(B){return String(" + expression + ");})(" +
                CharacterSkinExtractor.ReadKitText9(CharacterSkinExtractor.V9KitRoot,
                                                    $"builds/{preset}.v9.json") + ")");

        /// <summary>The materials the bind mesh actually paints: the distinct material indices its
        /// faces carry in the attribute channel, read from the mesh rather than from the reader
        /// that built it.</summary>
        static SortedSet<int> PaintedMaterials(CharacterSkinDef def)
        {
            var attrs = new List<Vector4>();
            def.BindMesh.GetUVs(RigMeshBuilder.AttrUvChannel, attrs);
            var painted = new SortedSet<int>();
            foreach (Vector4 a in attrs) painted.Add(Mathf.RoundToInt(a.x));
            return painted;
        }

        // =======================================================================================
        // v9 1. the player bakes under the v9 tone rule, from the rig the export came from
        // =======================================================================================

        /// <summary>
        /// The player's def is a v9 def: the v9 tone rule, the rig and poses paths, and the rig's
        /// revision. Its two source hashes are the ones the committed export was derived from, so
        /// a def baked from a rig that has moved since the export cannot pass. Every material it
        /// carries is one its bind mesh paints (the 9.2 contract declares 32 for the fisher and
        /// the rest face paints 19; a def padded to the declaration would hide the real count).
        /// </summary>
        [Test]
        public void V9_ComposesThePlayerUnderTheV9ToneRule()
        {
            CharacterSkinDef def = V9Bake(Player).Def;
            int limit = CharacterSkinDef.MaxMaterials(ToneRule.V9);

            Assert.AreEqual(ToneRule.V9, def.ToneRule,
                "A rig 9 def must carry the v9 tone rule, or the presenter shades it with rig 7's " +
                "sixteen-slot ramp.");
            Assert.AreEqual(CharacterSkinExtractor.V9ScriptPath, def.SourceRigPath);
            Assert.AreEqual(CharacterSkinExtractor.V9Revision, def.SourceRigRevision);
            Assert.AreEqual(CharacterSkinExtractor.V9PosesPath, def.BaseRigPath);
            Assert.AreEqual(V9Export(Player, "B.derivedFromRigSha256"), def.SourceRigSha256,
                "The def was baked from a rig that is not the one the committed export was derived from.");
            Assert.AreEqual(V9Export(Player, "B.posesDerivedFromRigSha256"), def.BaseRigSha256,
                "The def was baked with a poses file that is not the one the committed export was derived from.");

            SortedSet<int> painted = PaintedMaterials(def);
            Assert.AreEqual(def.Materials.Length, painted.Count,
                $"The def carries {def.Materials.Length} materials and its bind mesh paints " +
                $"{painted.Count} ([{string.Join(",", painted)}]).");
            Assert.AreEqual(def.Materials.Length - 1, painted.Max,
                "The bind mesh paints a material index the def does not carry.");
            Assert.LessOrEqual(def.Materials.Length, limit,
                $"The player paints {def.Materials.Length} materials, over the {limit} a v9 skin carries.");
            Assert.IsTrue(def.IsUsable(), "The composed player def is not usable.");
        }

        // =======================================================================================
        // v9 2. every cast preset fits the v9 material limit
        // =======================================================================================

        /// <summary>
        /// Ruling 1 moves the cast onto rig 9 with the player, so every preset the rig declares is
        /// baked and counted, the way the baker counts: the materials its def carries, each one
        /// painted by the bind mesh.
        /// </summary>
        [Test]
        public void V9_EveryPresetPaintsWithinTheV9MaterialLimit()
        {
            int limit = CharacterSkinDef.MaxMaterials(ToneRule.V9);
            var counts = new StringBuilder();
            var over = new List<string>();

            foreach (string preset in CharacterSkinExtractor.Presets9(V9Host))
            {
                CharacterSkinDef def = V9Bake(preset).Def;
                int painted = PaintedMaterials(def).Count;
                counts.Append($" {preset} {def.Materials.Length}");

                Assert.IsTrue(def.IsUsable(), $"{preset}: the composed def is not usable.");
                Assert.AreEqual(def.Materials.Length, painted,
                    $"{preset}: the def carries {def.Materials.Length} materials and its bind mesh paints {painted}.");
                if (def.Materials.Length > limit) over.Add($"{preset} ({def.Materials.Length})");
            }

            Debug.Log($"[CharacterSkinBakeGuardTests] v9 painted materials, limit {limit}:{counts}");
            Assert.IsEmpty(over, $"Presets that paint more than the {limit} materials a v9 skin carries: " +
                                 string.Join(", ", over));
        }

        // =======================================================================================
        // v9 3. the load-bearing one: every def poses the rig's own geometry
        // =======================================================================================

        /// <summary>
        /// Every preset, every clip, every frame: the def's bind mesh skinned with its own bones,
        /// weights, bindposes and bone keys lands within <see cref="V9ReplayTolerance"/> of the rig's
        /// own <c>posed()</c> geometry for that clip and frame, corner for corner.
        ///
        /// <para>A corner whose face group the frame does not show is skipped and counted: the def
        /// binds the rest face, and where a clip blinks or looks the rig draws another face group
        /// over those corners. Ruling 8 has the engine play blink and gaze, so the count is reported
        /// rather than hidden.</para>
        /// </summary>
        [Test]
        public void V9_TheDefReplaysTheRigsOwnPoseOnEveryClipAndFrame()
        {
            IRigScriptHost host = V9Host;
            string[] names = CharacterSkinExtractor.ClipNames9(host);
            var report = new StringBuilder();
            var sw = Stopwatch.StartNew();
            double worst = 0; string worstAt = "none";
            long compared = 0, hidden = 0; int frames = 0;

            foreach (string preset in CharacterSkinExtractor.Presets9(host))
            {
                CharacterSkinDef def = V9Bake(preset).Def;
                Assert.AreEqual(names.Length, def.Clips.Length,
                    $"{preset}: the rig names {names.Length} clips and the def carries {def.Clips.Length}.");
                string[] rest = CharacterSkinExtractor.DefaultFaceGroups9(host, preset);
                Vector3[] bindVerts = def.BindMesh.vertices;
                BoneWeight[] weights = def.BindMesh.boneWeights;
                double presetWorst = 0; long presetHidden = 0;

                for (int i = 0; i < names.Length; i++)
                {
                    CharacterSkinDef.SkinClip clip = def.Clips[i];
                    for (int k = 0; k < clip.FrameCount; k++)
                    {
                        double[] truth = CharacterSkinExtractor.PosedCorners9(host, preset, rest, names[i], k);
                        Assert.AreEqual(bindVerts.Length * 3, truth.Length,
                            $"{preset} {names[i]}[{k}]: the rig posed {truth.Length / 3} corners and the bind " +
                            $"mesh has {bindVerts.Length}, so the two are not the same corner list.");
                        Matrix4x4[] skin = SkinMatrices(def, clip, k);
                        frames++;

                        for (int c = 0; c < bindVerts.Length; c++)
                        {
                            double tx = truth[3 * c], ty = truth[3 * c + 1], tz = truth[3 * c + 2];
                            if (double.IsNaN(tx)) { hidden++; presetHidden++; continue; }
                            Vector3 p = SkinVertex(bindVerts[c], weights[c], skin, def.MaxInfluences);
                            double dx = p.x - tx, dy = p.y - ty, dz = p.z - tz;
                            double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                            if (d > presetWorst) presetWorst = d;
                            if (d > worst) { worst = d; worstAt = $"{preset} {names[i]}[{k}] corner {c}"; }
                            compared++;
                        }
                    }
                }
                report.Append($"\n  {preset}: worst {presetWorst.ToString("0.00E+0", CultureInfo.InvariantCulture)} m, " +
                              $"{presetHidden:N0} corner-frames on a face group the frame does not show");
            }

            Debug.Log($"[CharacterSkinBakeGuardTests] v9 replay: {frames:N0} frames, {compared:N0} corners " +
                      $"compared, {hidden:N0} hidden; worst {worst.ToString("0.00E+0", CultureInfo.InvariantCulture)} m " +
                      $"at {worstAt}; bar {V9ReplayTolerance} m ({sw.ElapsedMilliseconds} ms){report}");
            Assert.Greater(compared, 0, "The replay compared no corner at all.");
            Assert.LessOrEqual(worst, V9ReplayTolerance,
                $"A v9 def poses a corner {worst:E2} m from where rig 9 draws it ({worstAt}); the bar is " +
                $"{V9ReplayTolerance} m.{report}");
        }

        // =======================================================================================
        // v9 4. every def carries every clip of the committed export, with its timing
        // =======================================================================================

        /// <summary>
        /// The committed export is what the game was sized against, so each def must carry each of
        /// its clips under the key the game looks a state up by (the animation, or the animation
        /// and its carry), with the export's frames, rate, loop, settle, carry and mount. Matched by
        /// key, not by position, so the check does not lean on the order the baker happens to use.
        /// </summary>
        [Test]
        public void V9_TheDefCarriesEveryClipOfTheCommittedExport()
        {
            var problems = new List<string>();
            int rowsTotal = 0;

            foreach (string preset in CharacterSkinExtractor.Presets9(V9Host))
            {
                CharacterSkinAssetBaker.SkinBake bake = V9Bake(preset);
                CharacterSkinDef def = bake.Def;
                string[] rows = V9Export(preset,
                    "B.clips.map(function(c){return [c.name,c.anim,c.carry||'',c.mount||''," +
                    "c.frames,c.ms,!!c.loop,!!c.settle].join(':');}).join('|')").Split('|');

                var byKey = new Dictionary<string, CharacterSkinDef.SkinClip>(StringComparer.Ordinal);
                foreach (CharacterSkinDef.SkinClip clip in def.Clips)
                {
                    Assert.IsFalse(byKey.ContainsKey(clip.StateKey),
                        $"{preset}: two clips answer the state '{clip.StateKey}'.");
                    byKey.Add(clip.StateKey, clip);
                }

                int frames = 0;
                foreach (string row in rows)
                {
                    // name : anim : carry : mount : frames : ms : loop : settle
                    string[] f = row.Split(':');
                    string at = $"{preset} '{f[0]}'";
                    string key = f[2].Length == 0 ? f[1] : new CharacterState(f[1], null, f[2]).Key;
                    if (!byKey.TryGetValue(key, out CharacterSkinDef.SkinClip clip))
                    {
                        problems.Add($"{at}: the def has no clip for the state '{key}'.");
                        continue;
                    }

                    int n = int.Parse(f[4], CultureInfo.InvariantCulture);
                    double ms = double.Parse(f[5], CultureInfo.InvariantCulture);
                    frames += n;
                    if (clip.Anim != f[1]) problems.Add($"{at}: anim '{clip.Anim}', export '{f[1]}'.");
                    if ((clip.Carry ?? "") != f[2]) problems.Add($"{at}: carry '{clip.Carry}', export '{f[2]}'.");
                    if ((clip.Mount ?? "") != f[3]) problems.Add($"{at}: mount '{clip.Mount}', export '{f[3]}'.");
                    if (clip.FrameCount != n) problems.Add($"{at}: {clip.FrameCount} frames, export {n}.");
                    if (Math.Abs(clip.FramesPerSecond - 1000.0 / ms) > 1e-3)
                        problems.Add($"{at}: {clip.FramesPerSecond} fps, export {ms} ms a frame.");
                    if (clip.Loop != (f[6] == "true")) problems.Add($"{at}: loop {clip.Loop}, export {f[6]}.");
                    if (clip.Settle != (f[7] == "true")) problems.Add($"{at}: settle {clip.Settle}, export {f[7]}.");
                }

                if (rows.Length != def.Clips.Length)
                    problems.Add($"{preset}: the export has {rows.Length} clips and the def {def.Clips.Length}.");
                if (frames != bake.TotalFrames)
                    problems.Add($"{preset}: the export has {frames} frames and the bake {bake.TotalFrames}.");
                rowsTotal += rows.Length;
            }

            Assert.Greater(rowsTotal, 0, "No committed export clip was read.");
            Assert.IsEmpty(problems, "Clips the defs do not carry as exported:\n  " + string.Join("\n  ", problems));
        }
    }
}
