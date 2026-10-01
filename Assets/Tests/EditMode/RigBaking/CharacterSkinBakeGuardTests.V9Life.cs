using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>RIG 9 PLAYS IN FULL (character PR 2a): the face, the tracks, the blink, the look and the
    /// ink of every preset, each held to the rig itself.</b>
    ///
    /// <para>A guard asks the rig for its bar, never the code it tests. Every expected value below is
    /// read here, in V8, off the rig's own tables and functions (<c>GROUP_ORDER</c>,
    /// <c>FACE_SLOTS</c>, <c>bindMesh</c>, <c>clip</c>, <c>BLINK</c>, <c>blinkClip</c>, <c>LOOK</c>,
    /// <c>lookContract</c>, <c>lookAt</c>, <c>evalClip</c>, <c>ROLE</c>, <c>SHADING</c>) or off its
    /// committed golden report, by JS written for these tests, and none of it through the extractor
    /// readers that baked the defs. The defs are the fresh bakes <see cref="V9Bake"/> composes, so
    /// these guards hold before the committed skins are re-baked.</para>
    /// </summary>
    public partial class CharacterSkinBakeGuardTests
    {
        /// <summary>
        /// The look port's bar against the rig's <c>lookAt</c>, degrees, on every field it returns.
        /// Both sides round to three decimals (the rig's <c>toFixed(3)</c>), so a value that lands on
        /// a rounding edge can come out one step (0.001°) apart when the port measures on the def's
        /// float keys and float head share; the bar allows that step and as much again.
        /// </summary>
        const double LookPortToleranceDeg = 0.002;

        /// <summary>The turn port's bar against the rig's <c>evalClip(…, look)</c> world matrices, on
        /// rotation entries and metres alike: the replay bar.</summary>
        const double TurnPortTolerance = V9ReplayTolerance;

        /// <summary>The golden aim's bar, target by target: the port's aim against the rig's own,
        /// degrees. A one-step difference in a rounded turn moves an aim by well under 0.001°.</summary>
        const double GoldenAimToleranceDeg = 0.005;

        /// <summary>The clip frames the look grid is measured on: the free body, the carries the
        /// player plays at the wheel and the oars, and two upright work clips.</summary>
        static readonly (string Clip, int Frame)[] LookGridFrames =
        {
            ("idle", 0), ("idle", 3), ("walk", 2), ("run", 1), ("haul", 3), ("balance", 5),
            ("helm_idle", 0), ("oars_row", 4),
        };

        /// <summary>The clip frames the turn is composed on.</summary>
        static readonly (string Clip, int Frame)[] TurnFrames = { ("idle", 0), ("walk", 2), ("oars_row", 4) };

        readonly Dictionary<string, Dictionary<string, RigClipRow9>> _rigClipRows9 =
            new Dictionary<string, Dictionary<string, RigClipRow9>>(StringComparer.Ordinal);

        [OneTimeTearDown]
        public void ForgetRigClipRows9() => _rigClipRows9.Clear();

        // =======================================================================================
        // v9 life 1. every preset binds every face group of the rig, each face culled by its role
        // =======================================================================================

        /// <summary>
        /// Every def binds the rig's whole face: its <see cref="CharacterSkinDef.FaceGroups"/> are the
        /// rig's <c>GROUP_ORDER</c> (thirteen at 9.2), its slots the rig's <c>FACE_SLOTS</c>, its rest
        /// face the rig's <c>baseIntent</c> face, and every face of the rig's <c>bindMesh</c> sits in the
        /// one bind mesh, in order, with its group, its cull role and its head flag on each of its
        /// corners (UV1). The role is judged here from the rig's <c>ROLE</c> and the face's own
        /// <c>minT</c>; the head flag by the rig's <c>posed()</c> rule (the face's first bone is the
        /// head). One submesh: the face draws in the figure's one draw call.
        /// </summary>
        [Test]
        public void V9_EveryPresetBindsEveryFaceGroupOfTheRig()
        {
            IRigScriptHost host = V9Host;
            string g = CharacterSkinExtractor.V9GlobalName;
            string[] order = host.EvaluateString(g + ".GROUP_ORDER.join(',')").Split(',');
            string[] slots = host.EvaluateString("Object.keys(" + g + ".FACE_SLOTS).join(',')").Split(',');
            CollectionAssert.AreEqual(slots, CharacterSkinDef.FaceSlotNames,
                "The def's face slots are not rig 9's FACE_SLOTS, in order.");
            string strays = host.EvaluateString(
                "(function(){var G=" + g + ",o=[];G.GROUP_ORDER.forEach(function(n){var d=n.indexOf('.')," +
                "s=d>0?G.FACE_SLOTS[n.slice(0,d)]:null;if(!s||s.indexOf(n.slice(d+1))<0)o.push(n);});" +
                "return o.join(',');})()");
            Assert.IsEmpty(strays, $"Rig 9's GROUP_ORDER names groups its FACE_SLOTS do not: {strays}.");

            string mouth = CharacterSkinDef.FaceSlotNames[CharacterSkinDef.MouthSlot] + ".";
            var report = new StringBuilder();
            foreach (string preset in CharacterSkinExtractor.Presets9(host))
            {
                CharacterSkinDef def = V9Bake(preset).Def;
                CollectionAssert.AreEqual(order, def.FaceGroups,
                    $"{preset}: the def's face groups are not rig 9's GROUP_ORDER.");
                Assert.AreEqual(1, def.BindMesh.subMeshCount,
                    $"{preset}: the bind mesh has {def.BindMesh.subMeshCount} submeshes; the face draws in " +
                    "the figure's one draw call.");

                string[] rest = host.EvaluateString(
                    "(function(){var G=" + g + ",f=G.baseIntent(G.buildOf(" + JsQuote9(preset) + ").D).face;" +
                    "return Object.keys(G.FACE_SLOTS).map(function(s){return s+'.'+f[s];}).join(',');})()")
                    .Split(',');
                Assert.AreEqual(rest.Length, def.RestFace.Length,
                    $"{preset}: the def rests {def.RestFace.Length} face slots and the rig {rest.Length}.");
                for (int s = 0; s < rest.Length; s++)
                    Assert.AreEqual(rest[s], GroupName9(def, def.RestFace[s]),
                        $"{preset}: the rest face's {slots[s]} is not the rig's baseIntent face.");

                string[] faces = host.EvaluateString(
                    "(function(){var G=" + g + ",P=" + JsQuote9(preset) + ",B=G.buildOf(P),F=G.bindMesh(P)," +
                    "GO=G.GROUP_ORDER,R=G.ROLE,hx=B.sk.ix.head,o=[];" +
                    "for(var i=0;i<F.length;i++){var f=F[i],m=f.minT||0,gi=f.group?GO.indexOf(f.group)+1:0,r;" +
                    "if(!f.group)r=m===0?" + Role9(CharacterSkinDef.FaceRole.Body) + ":-1;" +
                    "else if(gi===0)r=-1;" +
                    "else if(f.group.indexOf(" + JsQuote9(mouth) + ")===0)r=" + Role9(CharacterSkinDef.FaceRole.Mouth) + ";" +
                    "else r=m===R.near.minT?" + Role9(CharacterSkinDef.FaceRole.Near) +
                    ":m===R.far.minT?" + Role9(CharacterSkinDef.FaceRole.Far) +
                    ":m===R.side.minT?" + Role9(CharacterSkinDef.FaceRole.Side) + ":-1;" +
                    "o.push(f.v.length+':'+gi+':'+r+':'+(f.bone[0][0][0]===hx?1:0));}" +
                    "return o.join(',');})()").Split(',');

                var uv = new List<Vector4>();
                def.BindMesh.GetUVs(RigMeshBuilder.FaceUvChannel, uv);
                Assert.AreEqual(def.BindMesh.vertexCount, uv.Count,
                    $"{preset}: UV1 holds {uv.Count} values for {def.BindMesh.vertexCount} vertices.");
                int corner = 0, headFaces = 0, grouped = 0, misses = 0;
                var perGroup = new int[order.Length + 1];
                string firstMiss = null;
                for (int i = 0; i < faces.Length; i++)
                {
                    string[] p = faces[i].Split(':');
                    int n = I9(p[0]), grp = I9(p[1]), role = I9(p[2]), head = I9(p[3]);
                    Assert.GreaterOrEqual(role, 0,
                        $"{preset}: rig face {i} (group {grp}) culls at a minT no ROLE carries, so the def has " +
                        "no role for it.");
                    Assert.LessOrEqual(corner + n, uv.Count,
                        $"{preset}: the rig's faces run past the bind mesh's {uv.Count} corners at face {i}.");
                    for (int k = 0; k < n; k++)
                    {
                        Vector4 a = uv[corner + k];
                        if (a.x == grp && a.y == role && a.z == head && a.w == 0f) continue;
                        misses++;
                        firstMiss ??= $"face {i} corner {k} carries {a} and the rig gives it " +
                                      $"(group {grp}, role {role}, head {head}, 0)";
                    }
                    corner += n;
                    perGroup[grp]++;
                    headFaces += head;
                    if (grp > 0) grouped++;
                }
                Assert.AreEqual(uv.Count, corner,
                    $"{preset}: the rig's {faces.Length} faces hold {corner} corners and the bind mesh {uv.Count}.");
                Assert.AreEqual(0, misses,
                    $"{preset}: {misses} bind-mesh corners carry face attributes the rig does not give them; " +
                    $"the first: {firstMiss}.");
                for (int gi = 1; gi <= order.Length; gi++)
                    Assert.Greater(perGroup[gi], 0, $"{preset}: no face of the bind mesh is in '{order[gi - 1]}'.");
                report.Append($"\n  {preset}: {faces.Length} faces, {grouped} in the {order.Length} groups, " +
                              $"{headFaces} on the head; {uv.Count} corners");
            }
            Debug.Log($"[CharacterSkinBakeGuardTests] v9 face: every preset binds rig 9's {order.Length} face " +
                      $"groups, each face with the rig's group, role and head flag:{report}");
        }

        // =======================================================================================
        // v9 life 2. every clip carries the rig's face track and, as data, its tool track
        // =======================================================================================

        /// <summary>
        /// Each clip of each def carries the rig's <c>clip()</c> tracks: one face group per slot per
        /// frame, the one the rig's <c>tracks[k].face</c> names (matched by group NAME through the
        /// def's own groups, so a reordered group table cannot pass), and the rig's
        /// <c>tracks[k].tool</c> key for key — or no tool track when every frame is the rig's bare
        /// <c>{held:false}</c>. Clips are matched to the rig's by anim and carry, not by position.
        /// </summary>
        [Test]
        public void V9_EveryClipCarriesTheRigsFaceAndToolTracks()
        {
            IRigScriptHost host = V9Host;
            var report = new StringBuilder();
            long faceStates = 0;
            foreach (string preset in CharacterSkinExtractor.Presets9(host))
            {
                CharacterSkinDef def = V9Bake(preset).Def;
                Dictionary<string, RigClipRow9> rows = RigClipRows9(preset);
                Assert.AreEqual(rows.Count, def.Clips.Length,
                    $"{preset}: the rig plays {rows.Count} clips and the def carries {def.Clips.Length}.");
                int tools = 0;
                foreach (RigClipRow9 row in rows.Values)
                {
                    int ci = SkinClipIndex9(def, row.Anim, row.Carry);
                    Assert.GreaterOrEqual(ci, 0,
                        $"{preset}: no clip of the def is rig 9's '{row.Name}' ({row.Anim}, carry '{row.Carry}').");
                    CharacterSkinDef.SkinClip clip = def.Clips[ci];
                    string at = $"{preset} '{row.Name}'";
                    Assert.AreEqual(row.Frames, clip.FrameCount, $"{at}: frame count.");
                    int faceLength = clip.Face?.Length ?? 0;
                    Assert.AreEqual(row.Frames * CharacterSkinDef.FaceSlots, faceLength,
                        $"{at}: the face track holds {faceLength} states for {row.Frames} frames of " +
                        $"{CharacterSkinDef.FaceSlots} slots.");
                    for (int k = 0; k < row.Frames; k++)
                        for (int s = 0; s < CharacterSkinDef.FaceSlots; s++)
                        {
                            string shown = GroupName9(def, clip.FaceGroupOf(k, s));
                            if (shown != row.Face[k][s])
                                Assert.Fail($"{at} frame {k}: the def shows '{shown}' in its " +
                                            $"{CharacterSkinDef.FaceSlotNames[s]} slot and the rig '{row.Face[k][s]}'.");
                            faceStates++;
                        }

                    int toolLength = clip.Tool?.Length ?? 0;
                    if (row.Tool == null)
                    {
                        Assert.AreEqual(0, toolLength,
                            $"{at}: the rig holds no tool on any frame and the def carries {toolLength} tool keys.");
                        continue;
                    }
                    tools++;
                    Assert.AreEqual(row.Frames, toolLength, $"{at}: the tool track holds {toolLength} keys.");
                    for (int k = 0; k < row.Frames; k++)
                    {
                        string[] t = row.Tool[k];
                        CharacterSkinDef.ToolKey key = clip.Tool[k];
                        string kat = $"{at} tool key {k}";
                        Assert.AreEqual(t[0] == "1", key.Held, kat + ": held.");
                        Assert.AreEqual(t[1], key.Kind ?? "", kat + ": kind.");
                        Assert.AreEqual((float)D9(t[2]), key.Pitch, kat + ": pitch.");
                        Assert.AreEqual((float)D9(t[3]), key.Yaw, kat + ": yaw.");
                        Assert.AreEqual((float)D9(t[4]), key.Bend, kat + ": bend.");
                        Assert.AreEqual((float)D9(t[5]), key.Length, kat + ": length.");
                        Assert.AreEqual(t[6] == "1", key.Advisory, kat + ": advisory.");
                    }
                }
                report.Append($"\n  {preset}: {rows.Count} clips carry the rig's face track, {tools} its tool track");
            }
            Debug.Log($"[CharacterSkinBakeGuardTests] v9 tracks: {faceStates:N0} face states equal the rig's " +
                      $"clip() tracks:{report}");
        }

        // =======================================================================================
        // v9 life 3. the blink is the rig's BLINK, and plays as the rig's exported blink clip
        // =======================================================================================

        /// <summary>
        /// Each def carries the rig's <c>BLINK</c> in its own units — the steps (group and seconds), the
        /// wait, the double's chance and gap, and the eyes it skips — and the figure's schedule
        /// (<see cref="CharacterFigureBlink"/>) plays the rig's exported blink clip (<c>blinkClip()</c>,
        /// four 40 ms frames) frame for frame, its first blink inside its first wait, the eyes back to
        /// the frame's own after. Then the skip on the rig's own clips: on every frame of every clip,
        /// a blink over eyes the rig's <c>skipIf</c> names leaves them (sleep keeps its closed eyes),
        /// and takes any other eyes.
        /// </summary>
        [Test]
        public void V9_TheBlinkIsTheRigsBlink()
        {
            IRigScriptHost host = V9Host;
            string g = CharacterSkinExtractor.V9GlobalName;
            string[] b = host.EvaluateString(
                "(function(){var K=" + g + ".BLINK,C=" + g + ".blinkClip(),e=K.slot;" +
                "return [K.steps.map(function(s){return e+'.'+s[e]+':'+s.ms;}).join(','),K.interval_ms.join(',')," +
                "String(K.doubleChance),String(K.doubleGap_ms)," +
                "((K.skipIf||{})[e]||[]).map(function(x){return e+'.'+x;}).join(',')," +
                "C.tracks.map(function(t){return e+'.'+t.face[e];}).join(','),String(C.ms)].join('|');})()")
                .Split('|');
            Assert.AreEqual(7, b.Length, "Rig 9's BLINK read as the wrong number of fields.");
            string[] steps = b[0].Split(',');
            string[] interval = b[1].Split(',');
            double chance = D9(b[2]), gapMs = D9(b[3]);
            string[] skips = b[4].Length == 0 ? Array.Empty<string>() : b[4].Split(',');
            string[] clipFrames = b[5].Split(',');
            double clipMs = D9(b[6]);
            Assert.AreEqual(2, interval.Length, "Rig 9's BLINK.interval_ms is not a pair.");

            long skipped = 0, blinked = 0;
            foreach (string preset in CharacterSkinExtractor.Presets9(host))
            {
                CharacterSkinDef def = V9Bake(preset).Def;
                Assert.IsTrue(def.HasBlink, $"{preset}: the def carries no blink.");
                Assert.AreEqual(steps.Length, def.BlinkSteps.Length, $"{preset}: blink step count.");
                for (int i = 0; i < steps.Length; i++)
                {
                    string[] s = steps[i].Split(':');
                    Assert.AreEqual(s[0], GroupName9(def, def.BlinkSteps[i].Group), $"{preset}: blink step {i}'s eyes.");
                    Assert.AreEqual(D9(s[1]) / 1000.0, def.BlinkSteps[i].Seconds, 1e-6, $"{preset}: blink step {i}'s time.");
                }
                Assert.AreEqual(D9(interval[0]) / 1000.0, def.BlinkIntervalSeconds.x, 1e-6, $"{preset}: the shortest wait.");
                Assert.AreEqual(D9(interval[1]) / 1000.0, def.BlinkIntervalSeconds.y, 1e-6, $"{preset}: the longest wait.");
                Assert.AreEqual(chance, def.BlinkDoubleChance, 1e-7, $"{preset}: the double's chance.");
                Assert.AreEqual(gapMs / 1000.0, def.BlinkDoubleGapSeconds, 1e-6, $"{preset}: the double's gap.");
                var skipNames = new List<string>();
                foreach (int id in def.BlinkSkipGroups) skipNames.Add(GroupName9(def, id));
                CollectionAssert.AreEquivalent(skips, skipNames, $"{preset}: the eyes a blink leaves alone.");

                // The schedule plays the rig's own four-frame blink clip, sampled at each frame's middle.
                var blink = new CharacterFigureBlink();
                blink.Reset(def, CharacterFigureBlink.SeedFor(def.Id, 0u));   // a figure with no key of its own
                blink.EyesAt(0d);
                double start = blink.NextStart;
                Assert.That(start, Is.InRange(0d, (double)def.BlinkIntervalSeconds.y),
                    $"{preset}: the first blink falls at {start} s, outside a first wait.");
                for (int j = 0; j < clipFrames.Length; j++)
                    Assert.AreEqual(clipFrames[j], GroupName9(def, blink.EyesAt(start + (j + 0.5) * clipMs / 1000.0)),
                        $"{preset}: blink frame {j} is not the rig's blinkClip frame.");
                Assert.AreEqual(CharacterSkinDef.NoFaceGroup, blink.EyesAt(start + clipFrames.Length * clipMs / 1000.0 + 1e-3),
                    $"{preset}: the eyes are not the frame's own again after the blink.");

                // The skip, judged on the frame's own eyes, on every frame the rig plays.
                int blinkEyes = def.FaceGroupId(steps[0].Split(':')[0]);
                string blinkName = GroupName9(def, blinkEyes);
                foreach (RigClipRow9 row in RigClipRows9(preset).Values)
                {
                    int ci = SkinClipIndex9(def, row.Anim, row.Carry);
                    Assert.GreaterOrEqual(ci, 0, $"{preset}: no clip of the def is rig 9's '{row.Name}'.");
                    CharacterSkinDef.SkinClip clip = def.Clips[ci];
                    for (int k = 0; k < row.Frames; k++)
                    {
                        string own = row.Face[k][CharacterSkinDef.EyesSlot];
                        bool skip = Array.IndexOf(skips, own) >= 0;
                        CharacterFigureFace.Compose(def, clip, k, CharacterFigureLook.GazeOpen, blinkEyes,
                                                    out int eyes, out _, out _);
                        Assert.AreEqual(skip ? own : blinkName, GroupName9(def, eyes),
                            $"{preset} '{row.Name}' frame {k}: a blink over '{own}'.");
                        if (skip) skipped++; else blinked++;
                    }
                }
            }
            Assert.Greater(skipped, 0,
                "No frame of any clip shows eyes the rig's blink skips, so the skip was never tried.");
            Debug.Log($"[CharacterSkinBakeGuardTests] v9 blink: steps [{b[0]}], every {b[1]} ms, double " +
                      $"{b[2]} after {b[3]} ms, skips [{b[4]}]; the schedule plays blinkClip [{b[5]}]; a blink " +
                      $"showed on {blinked:N0} clip frames and left {skipped:N0} alone.");
        }

        // =======================================================================================
        // v9 life 4. the look is the rig's LOOK
        // =======================================================================================

        /// <summary>
        /// Each def carries the rig's <c>LOOK</c> and <c>lookContract()</c>: the neck and head it
        /// splits the turn onto (by the rig's own bone indices), the split, the limits, the head's
        /// share, the eyes' threshold and gaze groups, the head mid point <c>lookAt</c> aims from, and
        /// the chest it measures in (<c>ix.chest</c>, which the rig names in lookAt's code and in the
        /// contract's "both in the chest frame"; the grid test below fails if it moves).
        /// </summary>
        [Test]
        public void V9_TheLookIsTheRigsLook()
        {
            IRigScriptHost host = V9Host;
            string g = CharacterSkinExtractor.V9GlobalName;
            string[] l = host.EvaluateString(
                "(function(){var L=" + g + ".LOOK,E=" + g + ".lookContract().eyes;" +
                "return [L.bones.join(','),String(L.split[L.bones[0]]),String(L.split[L.bones[1]]),L.yaw.join(',')," +
                "L.pitch.join(','),String(L.headShare),String(L.eyesBeyond_deg),E.centre,E.left,E.right].join('|');})()")
                .Split('|');
            Assert.AreEqual(10, l.Length, "Rig 9's LOOK read as the wrong number of fields.");
            string[] bones = l[0].Split(',');
            Assert.AreEqual(2, bones.Length, $"Rig 9's LOOK turns [{l[0]}]; the def splits a turn onto two bones.");
            string[] yaw = l[3].Split(','), pitch = l[4].Split(',');

            foreach (string preset in CharacterSkinExtractor.Presets9(host))
            {
                CharacterSkinDef def = V9Bake(preset).Def;
                Assert.IsTrue(def.HasLook, $"{preset}: the def carries no look.");
                string[] ix = host.EvaluateString(
                    "(function(){var B=" + g + ".buildOf(" + JsQuote9(preset) + "),x=B.sk.ix;" +
                    "return [x[" + JsQuote9(bones[0]) + "],x[" + JsQuote9(bones[1]) + "],x.chest,B.D.headMid.join(',')].join('|');})()")
                    .Split('|');
                Assert.AreEqual(I9(ix[0]), def.LookNeckBone, $"{preset}: the look's first bone.");
                Assert.AreEqual(bones[0], def.Bones[def.LookNeckBone].Id, $"{preset}: the look's first bone id.");
                Assert.AreEqual(I9(ix[1]), def.LookHeadBone, $"{preset}: the look's second bone.");
                Assert.AreEqual(bones[1], def.Bones[def.LookHeadBone].Id, $"{preset}: the look's second bone id.");
                Assert.AreEqual(I9(ix[2]), def.LookChestBone, $"{preset}: the bone lookAt measures in (ix.chest).");
                Assert.AreEqual((float)D9(l[1]), def.LookSplitNeck, $"{preset}: the first bone's split.");
                Assert.AreEqual((float)D9(l[2]), def.LookSplitHead, $"{preset}: the second bone's split.");
                Assert.AreEqual((float)D9(yaw[0]), def.LookYawLimits.x, $"{preset}: the yaw limit (left).");
                Assert.AreEqual((float)D9(yaw[1]), def.LookYawLimits.y, $"{preset}: the yaw limit (right).");
                Assert.AreEqual((float)D9(pitch[0]), def.LookPitchLimits.x, $"{preset}: the pitch limit (up).");
                Assert.AreEqual((float)D9(pitch[1]), def.LookPitchLimits.y, $"{preset}: the pitch limit (down).");
                Assert.AreEqual((float)D9(l[5]), def.LookHeadShare, $"{preset}: the head's share.");
                Assert.AreEqual((float)D9(l[6]), def.LookEyesBeyondDeg, $"{preset}: the eyes' threshold.");
                Assert.AreEqual(l[7], GroupName9(def, def.LookEyes.x), $"{preset}: the centre gaze.");
                Assert.AreEqual(l[8], GroupName9(def, def.LookEyes.y), $"{preset}: the left gaze.");
                Assert.AreEqual(l[9], GroupName9(def, def.LookEyes.z), $"{preset}: the right gaze.");
                string[] mid = ix[3].Split(',');
                Assert.AreEqual(3, mid.Length, $"{preset}: D.headMid is not three numbers.");
                Assert.AreEqual((float)D9(mid[0]), def.HeadMid.x, $"{preset}: headMid x.");
                Assert.AreEqual((float)D9(mid[1]), def.HeadMid.y, $"{preset}: headMid y.");
                Assert.AreEqual((float)D9(mid[2]), def.HeadMid.z, $"{preset}: headMid z.");
            }
        }

        // =======================================================================================
        // v9 life 5. the look port is the rig's lookAt, over a grid of targets
        // =======================================================================================

        /// <summary>
        /// <see cref="CharacterSkinPose.LookAtFrame"/> on the def's composed frame against the rig's own
        /// <c>lookAt(evalClip(…), target, share)</c> in V8, on eight clip frames of every preset, 78
        /// targets (13 bearings all the way round, two ranges, three heights) and four shares (the
        /// rig's default, 1, 0.5 and 0): every field within <see cref="LookPortToleranceDeg"/>, the
        /// gaze equal wherever the residual is not within the bar of the eyes' threshold, every turn
        /// inside the limits, and each of the four limits reached somewhere, so the clamp is tried on
        /// both axes both ways.
        /// </summary>
        [Test]
        public void V9_TheLookPortMatchesTheRigsLookAtOverAGrid()
        {
            IRigScriptHost host = V9Host;
            string g = CharacterSkinExtractor.V9GlobalName;
            List<Vector3> targets = LookGridTargets9();
            var tj = new StringBuilder("[");
            foreach (Vector3 t in targets)
                tj.Append(tj.Length == 1 ? "" : ",").Append('[').Append(R9(t.x)).Append(',').Append(R9(t.y))
                  .Append(',').Append(R9(t.z)).Append(']');
            tj.Append(']');
            double?[] shares = { null, 1d, 0.5d, 0d };
            const string sharesJs = "[null,1,0.5,0]";

            double worst = 0; string worstAt = "none";
            long compared = 0; int nearThreshold = 0, yawLo = 0, yawHi = 0, pitchLo = 0, pitchHi = 0;
            var gazes = new int[3];
            foreach (string preset in CharacterSkinExtractor.Presets9(host))
            {
                CharacterSkinDef def = V9Bake(preset).Def;
                var world = new Matrix4x4[def.Bones.Length];
                CharacterFigureLook.Limits lim = CharacterFigureLook.Limits.Of(def);
                foreach ((string name, int frame) in LookGridFrames)
                {
                    CharacterSkinDef.SkinClip clip = def.Clips[RigClipIndex9(def, name)];
                    Assert.Less(frame, clip.FrameCount, $"{preset} '{name}' has no frame {frame}.");
                    CharacterSkinPose.ComposeWorld(clip, frame, def.Bones, world);
                    string[] rig = host.EvaluateString(
                        "(function(){var G=" + g + ",N=" + JsQuote9(name) + ",cd=G.clipDef(N)," +
                        "S=G.evalClip(N,G.uOf(cd.anim," + frame + ")," + JsQuote9(preset) + "),T=" + tj + ",H=" + sharesJs + ",o=[];" +
                        "for(var i=0;i<T.length;i++)for(var j=0;j<H.length;j++){var L=G.lookAt(S,T[i],H[j]);" +
                        "o.push([L.yaw,L.pitch,L.need.yaw,L.need.pitch,L.residualYaw,L.eyes].join(':'));}" +
                        "return o.join(',');})()").Split(',');
                    Assert.AreEqual(targets.Count * shares.Length, rig.Length, $"{preset} '{name}': rig rows.");

                    for (int t = 0; t < targets.Count; t++)
                        for (int j = 0; j < shares.Length; j++)
                        {
                            string[] r = rig[t * shares.Length + j].Split(':');
                            CharacterFigureLook.Result c =
                                CharacterSkinPose.LookAtFrame(def, world, targets[t], shares[j] ?? def.LookHeadShare);
                            double d = Math.Max(Math.Max(Math.Abs(c.Yaw - D9(r[0])), Math.Abs(c.Pitch - D9(r[1]))),
                                                Math.Max(Math.Max(Math.Abs(c.NeedYaw - D9(r[2])), Math.Abs(c.NeedPitch - D9(r[3]))),
                                                         Math.Abs(c.ResidualYaw - D9(r[4]))));
                            compared++;
                            string at = $"{preset} {name}[{frame}] target {targets[t]} share {(shares[j].HasValue ? R9(shares[j].Value) : "default")}";
                            if (d > worst) { worst = d; worstAt = at; }

                            int rigGaze = r[5] == "right" ? CharacterFigureLook.GazeRight
                                        : r[5] == "left" ? CharacterFigureLook.GazeLeft : CharacterFigureLook.GazeOpen;
                            if (Math.Abs(Math.Abs(D9(r[4])) - def.LookEyesBeyondDeg) <= LookPortToleranceDeg) nearThreshold++;
                            else if (c.Gaze != rigGaze)
                                Assert.Fail($"{at}: the port's gaze is {c.Gaze} and the rig's '{r[5]}' (residual {r[4]}°).");
                            gazes[c.Gaze + 1]++;

                            Assert.That(c.Yaw >= lim.YawMin && c.Yaw <= lim.YawMax &&
                                        c.Pitch >= lim.PitchMin && c.Pitch <= lim.PitchMax,
                                $"{at}: the turn {c.Yaw}/{c.Pitch} lies outside the limits.");
                            if (c.Yaw == lim.YawMin) yawLo++;
                            if (c.Yaw == lim.YawMax) yawHi++;
                            if (c.Pitch == lim.PitchMin) pitchLo++;
                            if (c.Pitch == lim.PitchMax) pitchHi++;
                        }
                }
            }
            Debug.Log($"[CharacterSkinBakeGuardTests] v9 look: {compared:N0} lookAt results against the rig's; " +
                      $"worst {worst.ToString("0.#####", CultureInfo.InvariantCulture)}° at {worstAt}; bar " +
                      $"{LookPortToleranceDeg}°; gaze left/open/right {gazes[0]}/{gazes[1]}/{gazes[2]}, {nearThreshold} " +
                      $"within the bar of the threshold; clamps yaw {yawLo}/{yawHi}, pitch {pitchLo}/{pitchHi}.");
            Assert.LessOrEqual(worst, LookPortToleranceDeg,
                $"The look port lands {worst}° from the rig's lookAt at {worstAt}; the bar is {LookPortToleranceDeg}°.");
            Assert.That(yawLo > 0 && yawHi > 0 && pitchLo > 0 && pitchHi > 0,
                $"The grid never reached every limit (yaw {yawLo}/{yawHi}, pitch {pitchLo}/{pitchHi}), so the clamp " +
                "was not tried both ways on both axes.");
            Assert.That(gazes[0] > 0 && gazes[2] > 0, "The grid never turned the eyes both ways.");
        }

        // =======================================================================================
        // v9 life 6. the turn composes as the rig's look does
        // =======================================================================================

        /// <summary>
        /// <see cref="CharacterSkinPose.ApplyTurn"/> with <see cref="CharacterFigureLook.TurnOf"/> on the
        /// def's clip frame against the rig's <c>evalClip(…, {yaw, pitch}).W</c>: every bone the neck
        /// carries lands where the rig puts it (rotation and position within
        /// <see cref="TurnPortTolerance"/>), over turns inside and past the limits (the rig clamps, so
        /// the port must), and every other bone keeps the exact matrix it had.
        /// </summary>
        [Test]
        public void V9_TheTurnComposesAsTheRigsLookDoes()
        {
            IRigScriptHost host = V9Host;
            string g = CharacterSkinExtractor.V9GlobalName;
            double[] yaws = { -75, -60, -25, 0, 35, 60, 80 };
            double[] pitches = { -25, -15, 0, 12, 20, 30 };
            var turns = new List<(double Yaw, double Pitch)>();
            var tj = new StringBuilder("[");
            foreach (double y in yaws)
                foreach (double p in pitches)
                {
                    turns.Add((y, p));
                    tj.Append(tj.Length == 1 ? "" : ",").Append('[').Append(R9(y)).Append(',').Append(R9(p)).Append(']');
                }
            tj.Append(']');

            double worst = 0; string worstAt = "none"; long compared = 0;
            foreach (string preset in CharacterSkinExtractor.Presets9(host))
            {
                CharacterSkinDef def = V9Bake(preset).Def;
                int n = def.Bones.Length, neck = def.LookNeckBone, head = def.LookHeadBone;
                CharacterFigureLook.Limits lim = CharacterFigureLook.Limits.Of(def);
                var moved = new List<int>();
                var kept = new List<int>();
                for (int bone = neck; bone < n; bone++) (Descends9(def.Bones, bone, neck) ? moved : kept).Add(bone);
                Assert.Contains(head, moved, $"{preset}: the head is not carried by the neck.");
                string bonesJs = "[" + string.Join(",", moved) + "]";
                var before = new Matrix4x4[n];
                var world = new Matrix4x4[n];

                foreach ((string name, int frame) in TurnFrames)
                {
                    CharacterSkinDef.SkinClip clip = def.Clips[RigClipIndex9(def, name)];
                    CharacterSkinPose.ComposeWorld(clip, frame, def.Bones, before);
                    string[] rows = host.EvaluateString(
                        "(function(){var G=" + g + ",N=" + JsQuote9(name) + ",cd=G.clipDef(N),u=G.uOf(cd.anim," + frame + ")," +
                        "BI=" + bonesJs + ",TU=" + tj + ",o=[];" +
                        "for(var t=0;t<TU.length;t++){var W=G.evalClip(N,u," + JsQuote9(preset) +
                        ",null,null,{yaw:TU[t][0],pitch:TU[t][1]}).W,r=[];" +
                        "for(var i=0;i<BI.length;i++){var w=W[BI[i]];r.push(w.R.join(','),w.p.join(','));}o.push(r.join(','));}" +
                        "return o.join(';');})()").Split(';');
                    Assert.AreEqual(turns.Count, rows.Length, $"{preset} '{name}': rig rows.");

                    for (int t = 0; t < turns.Count; t++)
                    {
                        Array.Copy(before, world, n);
                        (double y, double p) = turns[t];
                        CharacterSkinPose.ApplyTurn(clip, frame, def.Bones, world,
                            neck, CharacterFigureLook.TurnOf(y, p, def.LookSplitNeck, lim),
                            head, CharacterFigureLook.TurnOf(y, p, def.LookSplitHead, lim));
                        string[] v = rows[t].Split(',');
                        Assert.AreEqual(moved.Count * 12, v.Length, $"{preset} '{name}' turn {y}/{p}: rig values.");
                        for (int m = 0; m < moved.Count; m++)
                        {
                            Matrix4x4 w = world[moved[m]];
                            int o = m * 12;
                            for (int row = 0; row < 3; row++)
                            {
                                for (int col = 0; col < 3; col++)
                                {
                                    double d = Math.Abs(w[row, col] - D9(v[o + col * 3 + row]));
                                    if (d > worst) { worst = d; worstAt = $"{preset} {name}[{frame}] turn {y}/{p} bone {def.Bones[moved[m]].Id} R[{row},{col}]"; }
                                }
                                double dp = Math.Abs(w[row, 3] - D9(v[o + 9 + row]));
                                if (dp > worst) { worst = dp; worstAt = $"{preset} {name}[{frame}] turn {y}/{p} bone {def.Bones[moved[m]].Id} p[{row}]"; }
                            }
                            compared++;
                        }
                        foreach (int bone in kept)
                            if (!SameMatrix9(before[bone], world[bone]))
                                Assert.Fail($"{preset} '{name}' turn {y}/{p}: the turn moved '{def.Bones[bone].Id}', which the neck does not carry.");
                    }
                }
            }
            Debug.Log($"[CharacterSkinBakeGuardTests] v9 turn: {compared:N0} bone-turns against the rig's " +
                      $"evalClip(…, look); worst {worst.ToString("0.00E+0", CultureInfo.InvariantCulture)} at {worstAt}; " +
                      $"bar {TurnPortTolerance}.");
            Assert.LessOrEqual(worst, TurnPortTolerance,
                $"The turn port lands {worst:E2} from the rig's turned frame at {worstAt}; the bar is {TurnPortTolerance}.");
        }

        // =======================================================================================
        // v9 life 7. the look port lands where the rig's golden check does
        // =======================================================================================

        /// <summary>
        /// The rig's golden <c>look</c> check, ported: on idle frame 0, targets 2 m from the head point
        /// at seven bearings and three heights; <c>lookAt</c> with share 1; the targets whose whole
        /// turn lies inside the limits kept; the turn applied; the angle from the head's +y to the
        /// target measured. The port keeps the same targets as the rig, lands on each within
        /// <see cref="GoldenAimToleranceDeg"/> of the rig's own aim, keeps as many as the committed
        /// golden report counts, and its worst aim stays inside the report's "within X°" — the bar
        /// the rig sets itself, per preset (1.70° for eight presets and 2.40° for the skipper and
        /// the nan at 9.2), which the rig's aim today must still round to.
        /// </summary>
        [Test]
        public void V9_TheLookPortLandsWhereTheRigsGoldenCheckDoes()
        {
            IRigScriptHost host = V9Host;
            string g = CharacterSkinExtractor.V9GlobalName;
            Dictionary<string, (double Bar, int Inside)> golden = GoldenLookBars9();
            double[] bearings = { -50, -30, -15, 0, 15, 30, 50 };
            double[] rises = { -0.35, 0, 0.25 };
            var report = new StringBuilder();

            foreach (string preset in CharacterSkinExtractor.Presets9(host))
            {
                Assert.IsTrue(golden.TryGetValue(preset, out (double Bar, int Inside) bar),
                    $"The golden report holds no look check for '{preset}'.");
                CharacterSkinDef def = V9Bake(preset).Def;
                int n = def.Bones.Length;
                CharacterSkinDef.SkinClip idle = def.Clips[RigClipIndex9(def, "idle")];
                CharacterFigureLook.Limits lim = CharacterFigureLook.Limits.Of(def);
                var w0 = new Matrix4x4[n];
                var w2 = new Matrix4x4[n];
                CharacterSkinPose.ComposeWorld(idle, 0, def.Bones, w0);
                Vector3 e0 = CharacterSkinPose.HeadPoint(def, w0);

                string[] rig = host.EvaluateString(
                    "(function(){var G=" + g + ",B=G.buildOf(" + JsQuote9(preset) + "),ix=B.sk.ix,L=G.LOOK,S=G.evalClip('idle',0,B)," +
                    "mV=function(R,v){return [R[0]*v[0]+R[3]*v[1]+R[6]*v[2],R[1]*v[0]+R[4]*v[1]+R[7]*v[2],R[2]*v[0]+R[5]*v[1]+R[8]*v[2]];}," +
                    "add=function(a,b){return [a[0]+b[0],a[1]+b[1],a[2]+b[2]];},W0=S.W,e0=add(W0[ix.head].p,mV(W0[ix.head].R,B.D.headMid)),o=[];" +
                    "[" + JoinR9(bearings) + "].forEach(function(bear){[" + JoinR9(rises) + "].forEach(function(dz){" +
                    "var T=[e0[0]+2*Math.sin(bear*Math.PI/180),e0[1]+2*Math.cos(bear*Math.PI/180),e0[2]+dz],A=G.lookAt(S,T,1);" +
                    "if(Math.abs(A.need.yaw)>L.yaw[1]||A.need.pitch<L.pitch[0]||A.need.pitch>L.pitch[1]){o.push('0:0');return;}" +
                    "var Wh=G.evalClip('idle',0,B,null,null,{yaw:A.yaw,pitch:A.pitch}).W[ix.head],f=mV(Wh.R,[0,1,0])," +
                    "ep=add(Wh.p,mV(Wh.R,B.D.headMid)),d=[T[0]-ep[0],T[1]-ep[1],T[2]-ep[2]],dl=Math.hypot(d[0],d[1],d[2]);" +
                    "o.push('1:'+Math.acos(Math.max(-1,Math.min(1,(f[0]*d[0]+f[1]*d[1]+f[2]*d[2])/dl)))*180/Math.PI);});});" +
                    "return o.join(',');})()").Split(',');
                Assert.AreEqual(bearings.Length * rises.Length, rig.Length, $"{preset}: rig rows.");

                int inside = 0;
                double worst = 0, rigWorst = 0, worstGap = 0;
                string worstAt = "none";
                for (int bi = 0; bi < bearings.Length; bi++)
                    for (int di = 0; di < rises.Length; di++)
                    {
                        string[] r = rig[bi * rises.Length + di].Split(':');
                        bool rigKeeps = r[0] == "1";
                        double rad = bearings[bi] * Math.PI / 180.0;
                        var target = new Vector3((float)(e0.x + 2 * Math.Sin(rad)), (float)(e0.y + 2 * Math.Cos(rad)),
                                                 (float)(e0.z + rises[di]));
                        CharacterFigureLook.Result look = CharacterSkinPose.LookAtFrame(def, w0, target, 1d);
                        bool keeps = !(Math.Abs(look.NeedYaw) > lim.YawMax ||
                                       look.NeedPitch < lim.PitchMin || look.NeedPitch > lim.PitchMax);
                        string at = $"bearing {R9(bearings[bi])}°, {R9(rises[di])} m";
                        Assert.AreEqual(rigKeeps, keeps,
                            $"{preset} {at}: the rig {(rigKeeps ? "keeps" : "skips")} the target and the port " +
                            $"{(keeps ? "keeps" : "skips")} it.");
                        if (!keeps) continue;
                        inside++;

                        Array.Copy(w0, w2, n);
                        CharacterSkinPose.ApplyTurn(idle, 0, def.Bones, w2,
                            def.LookNeckBone, CharacterFigureLook.TurnOf(look.Yaw, look.Pitch, def.LookSplitNeck, lim),
                            def.LookHeadBone, CharacterFigureLook.TurnOf(look.Yaw, look.Pitch, def.LookSplitHead, lim));
                        Matrix4x4 h = w2[def.LookHeadBone];
                        Vector3 ep = CharacterSkinPose.HeadPoint(def, w2);
                        double dx = (double)target.x - ep.x, dy = (double)target.y - ep.y, dz = (double)target.z - ep.z;
                        double dl = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        double cos = ((double)h.m01 * dx + (double)h.m11 * dy + (double)h.m21 * dz) / dl;
                        double aim = Math.Acos(Math.Max(-1d, Math.Min(1d, cos))) * 180.0 / Math.PI;
                        double rigAim = D9(r[1]);
                        worstGap = Math.Max(worstGap, Math.Abs(aim - rigAim));
                        rigWorst = Math.Max(rigWorst, rigAim);
                        if (aim > worst) { worst = aim; worstAt = at; }
                    }

                string line = string.Format(CultureInfo.InvariantCulture,
                    "{0}: {1} targets inside; the port's worst aim {2:0.000}° ({3}), the rig's {4:0.000}°, the report's " +
                    "bar {5:0.00}°; port against rig at most {6:0.0000}°", preset, inside, worst, worstAt, rigWorst,
                    bar.Bar, worstGap);
                report.Append("\n  ").Append(line);
                Assert.AreEqual(bar.Inside, inside, $"{preset}: the golden report counts {bar.Inside} targets inside and the port {inside}.");
                Assert.LessOrEqual(worstGap, GoldenAimToleranceDeg, $"{line}: the port's aim leaves the rig's.");
                Assert.AreEqual(bar.Bar, rigWorst, 0.005 + 1e-9,
                    $"{line}: the committed golden report's bar is not the rig's own aim today.");
                Assert.LessOrEqual(worst, bar.Bar + 0.005 + GoldenAimToleranceDeg, $"{line}: past the rig's bar.");
            }
            Debug.Log($"[CharacterSkinBakeGuardTests] v9 golden look (share 1, targets 2 m away, inside the limits):{report}");
        }

        // =======================================================================================
        // v9 life 8. the ink is the rig's SHADING and ROLE
        // =======================================================================================

        /// <summary>
        /// Each def's ink fields are the rig's: <c>SHADING.edge</c>, <c>keylineMix</c> and
        /// <c>keyline</c>, the head snap <c>SHADING.headSnap</c> declares, the face cull by role
        /// (<c>ROLE.near/far/side.minT</c>, and the one threshold the rig's mouth faces carry), and the
        /// floor every face culls at, read here off the rig's paint, which must hold that cull once.
        /// </summary>
        [Test]
        public void V9_TheInkIsTheRigsShadingAndRole()
        {
            IRigScriptHost host = V9Host;
            string g = CharacterSkinExtractor.V9GlobalName;
            string[] s = host.EvaluateString(
                "(function(){var S=" + g + ".SHADING,R=" + g + ".ROLE;return [String(S.edge),String(S.keylineMix)," +
                "String(S.keyline),(typeof S.headSnap==='string'&&S.headSnap.length>0)?'1':'0'," +
                "String(R.near.minT),String(R.far.minT),String(R.side.minT)].join('|');})()").Split('|');
            Assert.AreEqual(7, s.Length, "Rig 9's SHADING and ROLE read as the wrong number of fields.");
            string hex = s[2];
            Assert.IsTrue(Regex.IsMatch(hex, "^#[0-9a-fA-F]{6}$"), $"Rig 9's SHADING.keyline is '{hex}', not #rrggbb.");
            byte Hex(int at) => byte.Parse(hex.Substring(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            string source = File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, CharacterSkinExtractor.V9ScriptPath));
            MatchCollection floors = Regex.Matches(source,
                @"toward\s*<=\s*Math\.max\(\s*([0-9.eE+-]+)\s*,\s*f\.minT\s*\|\|\s*0\s*\)");
            Assert.AreEqual(1, floors.Count,
                $"Rig 9's paint holds its face cull (toward <= Math.max(floor, f.minT||0)) {floors.Count} times, not once.");
            double floor = D9(floors[0].Groups[1].Value);

            string mouth = CharacterSkinDef.FaceSlotNames[CharacterSkinDef.MouthSlot] + ".";
            foreach (string preset in CharacterSkinExtractor.Presets9(host))
            {
                CharacterSkinDef def = V9Bake(preset).Def;
                string[] mouths = host.EvaluateString(
                    "(function(){var s={};" + g + ".bindMesh(" + JsQuote9(preset) + ").forEach(function(f){" +
                    "if(f.group&&f.group.indexOf(" + JsQuote9(mouth) + ")===0)s[String(f.minT||0)]=1;});" +
                    "return Object.keys(s).join(',');})()").Split(',');
                Assert.AreEqual(1, mouths.Length, $"{preset}: the rig's mouth faces carry the thresholds [{string.Join(",", mouths)}].");

                Assert.AreEqual(ToneRule.V9, def.ToneRule, $"{preset}: tone rule.");
                Assert.IsTrue(def.HasInk, $"{preset}: the def carries no ink.");
                Assert.AreEqual((float)D9(s[0]), def.Edge, $"{preset}: SHADING.edge.");
                Assert.AreEqual((float)D9(s[1]), def.KeylineMix, $"{preset}: SHADING.keylineMix.");
                Assert.AreEqual(Hex(1), def.Keyline.r, $"{preset}: the keyline's red.");
                Assert.AreEqual(Hex(3), def.Keyline.g, $"{preset}: the keyline's green.");
                Assert.AreEqual(Hex(5), def.Keyline.b, $"{preset}: the keyline's blue.");
                Assert.AreEqual(255, def.Keyline.a, $"{preset}: the keyline's alpha.");
                Assert.AreEqual(s[3] == "1", def.HeadSnap, $"{preset}: SHADING.headSnap.");
                Assert.AreEqual((float)D9(s[4]), def.FaceMinToward.x, $"{preset}: ROLE.near.minT.");
                Assert.AreEqual((float)D9(s[5]), def.FaceMinToward.y, $"{preset}: ROLE.far.minT.");
                Assert.AreEqual((float)D9(s[6]), def.FaceMinToward.z, $"{preset}: ROLE.side.minT.");
                Assert.AreEqual((float)D9(mouths[0]), def.FaceMinToward.w, $"{preset}: the mouth faces' minT.");
                Assert.AreEqual((float)floor, def.FaceCullFloor, $"{preset}: the paint's cull floor.");
            }
        }

        // =======================================================================================
        // v9 life 9. every preset's ink is the rig's own render (RenderTruth9, head snap on)
        // =======================================================================================

        /// <summary>
        /// The bake's ink comparison (<see cref="CharacterSkinInk9"/>: the def's paint against the rig's
        /// own render, edges, keyline and head snap on) holds for every preset: no differing cluster
        /// past <see cref="CharacterSkinInk9.ClusterBar"/>, over shots that take in every face group the
        /// rest face does not show, a look both ways, and every wheel and oars clip; every shot snapped
        /// its head on both sides; and the def's float keyline mix mixes every byte pair as the rig's
        /// <c>SHADING.keylineMix</c> does.
        /// </summary>
        [Test]
        public void V9_EveryPresetsInkMatchesTheRigsOwnRender()
        {
            IRigScriptHost host = V9Host;
            double mix = D9(host.EvaluateString("String(" + CharacterSkinExtractor.V9GlobalName + ".SHADING.keylineMix)"));
            var report = new StringBuilder();
            foreach (string preset in CharacterSkinExtractor.Presets9(host))
            {
                CharacterSkinAssetBaker.SkinBake bake = V9Bake(preset);
                CharacterSkinDef def = bake.Def;
                CharacterSkinInk9.Reading[] readings = bake.InkReadings;
                Assert.IsNotEmpty(readings, $"{preset}: the bake shot no ink.");

                bool lookLeft = false, lookRight = false;
                var groupsShot = new HashSet<string>(StringComparer.Ordinal);
                var statesShot = new HashSet<string>(StringComparer.Ordinal);
                foreach (CharacterSkinInk9.Reading r in readings)
                {
                    Assert.IsNotNull(r.RigSnap, $"{preset} {r.Shot.Label}: the rig's render did not snap the head.");
                    Assert.IsNotNull(r.DefSnap, $"{preset} {r.Shot.Label}: the def's paint did not snap the head.");
                    if (r.Shot.Look.HasValue)
                    {
                        lookLeft |= r.Shot.Look.Value.x < 0f;
                        lookRight |= r.Shot.Look.Value.x > 0f;
                    }
                    if (r.Shot.Face != null)
                        for (int slot = 0; slot < r.Shot.Face.Length; slot++)
                            groupsShot.Add(CharacterSkinDef.FaceSlotNames[slot] + "." + r.Shot.Face[slot]);
                    if (!string.IsNullOrEmpty(r.Shot.State)) statesShot.Add(r.Shot.State);
                }
                for (int id = 1; id <= def.FaceGroups.Length; id++)
                    if (Array.IndexOf(def.RestFace, id) < 0)
                        Assert.IsTrue(groupsShot.Contains(def.FaceGroups[id - 1]),
                            $"{preset}: no ink shot shows '{def.FaceGroups[id - 1]}'.");
                Assert.IsTrue(lookLeft && lookRight, $"{preset}: the ink shots do not look both ways.");
                foreach (CharacterSkinDef.SkinClip clip in def.Clips)
                    if (clip.Carry == CharacterSkinStateMap.HelmCarry || clip.Carry == CharacterSkinStateMap.OarsCarry)
                        Assert.IsTrue(statesShot.Contains(clip.StateKey), $"{preset}: no ink shot plays '{clip.StateKey}'.");

                Assert.LessOrEqual(bake.InkWorstCluster, CharacterSkinInk9.ClusterBar, $"{preset}: {bake.InkReport}");
                Assert.IsTrue(CharacterSkinInk9.KeylineMixAgrees(mix, def.KeylineMix, out string miss),
                    $"{preset}: the def's keyline mix {def.KeylineMix} does not mix as the rig's {mix}: {miss}.");
                report.Append($"\n  {preset}: {readings.Length} shots, worst cluster {bake.InkWorstCluster} px");
            }
            Debug.Log($"[CharacterSkinBakeGuardTests] v9 ink against rig 9's own render (bar " +
                      $"{CharacterSkinInk9.ClusterBar} px):{report}");
        }

        // =======================================================================================
        // helpers
        // =======================================================================================

        /// <summary>One rig clip as the rig's <c>clip()</c> plays it: its tracks by frame.</summary>
        sealed class RigClipRow9
        {
            public string Name, Anim, Carry;
            public int Frames;

            /// <summary>[frame][slot]: the group name the frame shows in each slot.</summary>
            public string[][] Face;

            /// <summary>[frame]: held, kind, pitch, yaw, bend, len, advisory; null when every frame is
            /// the rig's bare <c>{held:false}</c>.</summary>
            public string[][] Tool;
        }

        /// <summary>Every clip the rig plays for <paramref name="preset"/>, read once, by name.</summary>
        Dictionary<string, RigClipRow9> RigClipRows9(string preset)
        {
            if (_rigClipRows9.TryGetValue(preset, out Dictionary<string, RigClipRow9> rows)) return rows;
            var slots = new StringBuilder();
            foreach (string s in CharacterSkinDef.FaceSlotNames) slots.Append(slots.Length == 0 ? "" : ",").Append(JsQuote9(s));
            string text = V9Host.EvaluateString(
                "(function(){var G=" + CharacterSkinExtractor.V9GlobalName + ",P=" + JsQuote9(preset) + ",S=[" + slots + "]," +
                "N=G.clipNames(),o=[];" +
                "for(var i=0;i<N.length;i++){var c=G.clip(N[i],P),fs=[],ts=[],any=false;" +
                "for(var t=0;t<c.tracks.length;t++){var fc=c.tracks[t].face||{},k=c.tracks[t].tool||{};" +
                "fs.push(S.map(function(s){return s+'.'+fc[s];}).join('/'));" +
                "if(Object.keys(k).length!==1||k.held!==false)any=true;" +
                "ts.push([k.held?1:0,k.kind==null?'':k.kind,+k.pitch||0,+k.yaw||0,+k.bend||0,+k.len||0,k.advisory?1:0].join(':'));}" +
                "o.push([N[i],c.anim,c.carry||'',c.frames,fs.join(' '),any?ts.join(' '):''].join('|'));}" +
                "return o.join('\\n');})()");
            rows = new Dictionary<string, RigClipRow9>(StringComparer.Ordinal);
            foreach (string line in text.Split('\n'))
            {
                string[] p = line.Split('|');
                Assert.AreEqual(6, p.Length, $"{preset}: a rig clip row read as {p.Length} fields: {line}");
                int frames = I9(p[3]);
                string[] fs = p[4].Split(' ');
                Assert.AreEqual(frames, fs.Length, $"{preset} '{p[0]}': {fs.Length} face rows for {frames} frames.");
                var row = new RigClipRow9 { Name = p[0], Anim = p[1], Carry = p[2], Frames = frames, Face = new string[frames][] };
                for (int k = 0; k < frames; k++) row.Face[k] = fs[k].Split('/');
                if (p[5].Length > 0)
                {
                    string[] ts = p[5].Split(' ');
                    Assert.AreEqual(frames, ts.Length, $"{preset} '{p[0]}': {ts.Length} tool rows for {frames} frames.");
                    row.Tool = new string[frames][];
                    for (int k = 0; k < frames; k++) row.Tool[k] = ts[k].Split(':');
                }
                rows.Add(row.Name, row);
            }
            _rigClipRows9[preset] = rows;
            return rows;
        }

        /// <summary>The def clip the rig's <paramref name="rigClip"/> was baked into, found by the anim
        /// and carry the rig's <c>clipDef</c> gives it.</summary>
        int RigClipIndex9(CharacterSkinDef def, string rigClip)
        {
            string[] ac = V9Host.EvaluateString(
                "(function(){var d=" + CharacterSkinExtractor.V9GlobalName + ".clipDef(" + JsQuote9(rigClip) + ");" +
                "return d?d.anim+'|'+(d.carry||''):'';})()").Split('|');
            Assert.AreEqual(2, ac.Length, $"Rig 9 has no clip '{rigClip}'.");
            int i = SkinClipIndex9(def, ac[0], ac[1]);
            Assert.GreaterOrEqual(i, 0, $"{def.Preset}: no clip of the def is rig 9's '{rigClip}'.");
            return i;
        }

        static int SkinClipIndex9(CharacterSkinDef def, string anim, string carry)
        {
            for (int i = 0; i < def.Clips.Length; i++)
                if (string.Equals(def.Clips[i].Anim, anim, StringComparison.Ordinal) &&
                    string.Equals(def.Clips[i].Carry ?? "", carry ?? "", StringComparison.Ordinal))
                    return i;
            return -1;
        }

        /// <summary>The look grid's targets, in the figure's frame, as floats (the port's type); the
        /// rig is handed the same floats.</summary>
        static List<Vector3> LookGridTargets9()
        {
            double[] bearings = { -150, -110, -75, -50, -25, -8, 0, 8, 25, 50, 75, 110, 150 };
            double[] ranges = { 0.9, 2.5 };
            double[] heights = { 0.2, 1.2, 2.3 };
            var targets = new List<Vector3>();
            foreach (double b in bearings)
                foreach (double r in ranges)
                    foreach (double z in heights)
                        targets.Add(new Vector3((float)(r * Math.Sin(b * Math.PI / 180.0)),
                                                (float)(r * Math.Cos(b * Math.PI / 180.0)), (float)z));
            return targets;
        }

        /// <summary>The committed golden report's look check per preset: the "within X°" bar and the
        /// number of targets inside, read in V8 off the report's own words.</summary>
        Dictionary<string, (double Bar, int Inside)> GoldenLookBars9()
        {
            string json = CharacterSkinExtractor.ReadKitText9(CharacterSkinExtractor.V9KitRoot, "golden-report.json");
            string text = V9Host.EvaluateString(
                "(function(R){var o=[];Object.keys(R.builds||{}).forEach(function(p){" +
                "var c=(R.builds[p].checks||[]).filter(function(x){return x.id==='look';})[0],d=c?String(c.detail):''," +
                "w=/within ([0-9.]+)\\u00b0/.exec(d),n=/for ([0-9]+) targets/.exec(d);" +
                "o.push(p+'|'+(w?w[1]:'')+'|'+(n?n[1]:''));});return o.join(',');})(" + json + ")");
            var bars = new Dictionary<string, (double, int)>(StringComparer.Ordinal);
            foreach (string row in text.Split(','))
            {
                string[] p = row.Split('|');
                Assert.AreEqual(3, p.Length, $"A golden look row read as {p.Length} fields: {row}");
                Assert.IsNotEmpty(p[1], $"The golden report's look check for '{p[0]}' names no 'within X°'.");
                Assert.IsNotEmpty(p[2], $"The golden report's look check for '{p[0]}' counts no targets.");
                bars[p[0]] = (D9(p[1]), I9(p[2]));
            }
            return bars;
        }

        static bool Descends9(CharacterSkinDef.Bone[] bones, int bone, int ancestor)
        {
            for (int i = bone, hops = 0; i >= 0 && i < bones.Length && hops <= bones.Length; i = bones[i].Parent, hops++)
                if (i == ancestor) return true;
            return false;
        }

        static bool SameMatrix9(Matrix4x4 a, Matrix4x4 b)
        {
            for (int i = 0; i < 16; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        static string GroupName9(CharacterSkinDef def, int id) =>
            id >= 1 && id <= def.FaceGroups.Length ? def.FaceGroups[id - 1] : "(no group " + id + ")";

        static string Role9(CharacterSkinDef.FaceRole role) => ((int)role).ToString(CultureInfo.InvariantCulture);

        static string JsQuote9(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        static string R9(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        static string JoinR9(double[] values)
        {
            var sb = new StringBuilder();
            foreach (double v in values) sb.Append(sb.Length == 0 ? "" : ",").Append(R9(v));
            return sb.ToString();
        }

        static double D9(string s)
        {
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                Assert.Fail($"'{s}' is not a number.");
            return v;
        }

        static int I9(string s)
        {
            if (!int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v))
                Assert.Fail($"'{s}' is not a whole number.");
            return v;
        }
    }
}
