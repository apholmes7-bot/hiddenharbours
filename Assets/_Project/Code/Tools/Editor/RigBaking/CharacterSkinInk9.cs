using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>The ink comparison (character PR 2a).</b> A v9 def's figure, painted by rig 9's own rules
    /// (<see cref="RigPaint9"/>) and compared pixel for pixel with rig 9's own render of the same clip,
    /// frame, view, face and look (<see cref="CharacterSkinExtractor.RenderTruth9"/>, head snap on). The
    /// figure is the def's in every part:
    /// <list type="bullet">
    /// <item>the bind mesh, posed through the engine's own path (<see cref="CharacterSkinPose"/>: the
    /// clip's bone keys, the look's turn, the two-influence skin);</item>
    /// <item>its faces chosen by the def's face groups and culled at the def's role thresholds;</item>
    /// <item>shaded by the def's materials and key, then snapped, edged and keylined by the def's own
    /// numbers.</item>
    /// </list>
    ///
    /// <para><b>What it measures is what the def CARRIES:</b> floats where the rig has doubles, bone
    /// keys as quaternions and a two-influence skin, drawn under the rig's paint. The GPU's own
    /// arithmetic is the plate's to measure. The one place the two would part is the keyline mix. There
    /// the port computes as the resolve does (<see cref="RigPaint9.Ink.SinglePrecisionMix"/>), once
    /// <see cref="KeylineMixAgrees"/> has shown that this equals the rig's mix on every byte pair.</para>
    ///
    /// <para><b>The bar is <see cref="ClusterBar"/>:</b> the largest 4-connected cluster of differing
    /// pixels in any shot, never a percentage (the house rule, <see cref="RigPixelDiff"/>). A def's
    /// floats flip single pixels where the rig's doubles sit on a boundary. A keyline pixel takes its
    /// colour from the pixel beside it, so one flip can carry one neighbour with it. A face in the
    /// wrong place, a missing group or a wrong threshold makes a run of pixels, and a run is what the
    /// bar refuses.</para>
    /// </summary>
    public static class CharacterSkinInk9
    {
        /// <summary>The largest cluster of differing pixels a shot may hold: one flipped pixel and the
        /// one keyline pixel that takes its colour from it.</summary>
        public const int ClusterBar = 2;

        /// <summary>The headings every face group is shot at: a three-quarter view (near and far
        /// marks), the front and a profile (the side marks). Headings 0, 1 and 7 show no face at all
        /// (rig 9 turns the figure away from the camera there).</summary>
        public static readonly int[] FaceHeadings = { 3, 4, 6 };

        /// <summary>The headings the look and the carry clips are shot at: both three-quarter views.</summary>
        public static readonly int[] TurnHeadings = { 3, 5 };

        /// <summary>The look turns shot, degrees: both ways, a tip down and a tip up with a turn. Each
        /// lies inside the rig's limits, so the rig's clamp and the def's are not what is compared.</summary>
        public static readonly Vector2[] Looks =
        {
            new Vector2(30f, 0f), new Vector2(-30f, 0f), new Vector2(0f, 15f), new Vector2(20f, -10f),
        };

        /// <summary>One shot: a clip frame, a heading, and optionally a face and a look.</summary>
        public struct Shot
        {
            /// <summary>Rig 9's clip name: what its <c>render</c> takes (<c>helm_idle</c>).</summary>
            public string Clip;
            /// <summary>The def's key of that clip (<see cref="CharacterSkinDef.SkinClip.StateKey"/>).</summary>
            public string State;
            public int Frame;
            public int Dir;
            /// <summary>A look turn, x yaw and y pitch in degrees, or null.</summary>
            public Vector2? Look;
            /// <summary>One state per face slot, or null for the clip frame's own face.</summary>
            public string[] Face;

            public string Label
            {
                get
                {
                    var c = CultureInfo.InvariantCulture;
                    var sb = new StringBuilder(Clip).Append(" f").Append(Frame.ToString(c))
                        .Append(" dir ").Append(Dir.ToString(c));
                    if (Face != null) sb.Append(" face ").Append(string.Join("/", Face));
                    if (Look.HasValue)
                        sb.Append(" look ").Append(Look.Value.x.ToString("0.#", c)).Append('/')
                          .Append(Look.Value.y.ToString("0.#", c));
                    return sb.ToString();
                }
            }
        }

        /// <summary>One shot's comparison.</summary>
        public sealed class Reading
        {
            public Shot Shot;
            public RigPixelDiff Diff;
            /// <summary>The head snap rig 9 applied and the one the def path applied, pixels, or null
            /// with the snap off.</summary>
            public double[] RigSnap, DefSnap;

            public override string ToString() => $"{Shot.Label}: {Diff}";
        }

        /// <summary>
        /// The shot plan for <paramref name="def"/>. <paramref name="clipOfState"/> maps each of the
        /// def's clip keys to the rig clip it was baked from, as the bake built it. The shots are:
        /// <list type="bullet">
        /// <item>the idle's first frame at all eight headings, with the clip's own face;</item>
        /// <item>every face group the rest face does not show, at <see cref="FaceHeadings"/>;</item>
        /// <item>every <see cref="Looks"/> turn at <see cref="TurnHeadings"/>;</item>
        /// <item>every helm and oars clip at its first and middle frames, at <see cref="TurnHeadings"/>.</item>
        /// </list>
        /// </summary>
        public static List<Shot> Plan(CharacterSkinDef def, IReadOnlyDictionary<string, string> clipOfState)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (clipOfState == null) throw new ArgumentNullException(nameof(clipOfState));
            string idleState = CharacterSkinStateMap.Idle;
            if (!clipOfState.TryGetValue(idleState, out string idleClip))
                throw new InvalidOperationException($"'{def.Preset}' has no '{idleState}' clip to shoot the ink on.");

            var shots = new List<Shot>();
            for (int dir = 0; dir < 8; dir++)
                shots.Add(new Shot { Clip = idleClip, State = idleState, Frame = 0, Dir = dir });

            string[] rest = RestStates(def);
            for (int g = 1; g <= def.FaceGroups.Length; g++)
            {
                if (Array.IndexOf(def.RestFace, g) >= 0) continue;
                int slot = def.FaceSlotOf(g);
                if (slot < 0)
                    throw new InvalidOperationException($"'{def.Preset}' face group '{def.FaceGroups[g - 1]}' is in no slot.");
                var face = (string[])rest.Clone();
                face[slot] = StateOf(def.FaceGroups[g - 1]);
                foreach (int dir in FaceHeadings)
                    shots.Add(new Shot { Clip = idleClip, State = idleState, Frame = 0, Dir = dir, Face = face });
            }

            foreach (Vector2 look in Looks)
                foreach (int dir in TurnHeadings)
                    shots.Add(new Shot { Clip = idleClip, State = idleState, Frame = 0, Dir = dir, Look = look });

            var carried = new List<string>(clipOfState.Keys);
            carried.Sort(StringComparer.Ordinal);
            foreach (string state in carried)
            {
                int ci = ClipIndex(def, state);
                if (ci < 0) continue;
                CharacterSkinDef.SkinClip clip = def.Clips[ci];
                if (clip.Carry != CharacterSkinStateMap.HelmCarry && clip.Carry != CharacterSkinStateMap.OarsCarry)
                    continue;
                foreach (int frame in new[] { 0, clip.FrameCount / 2 })
                    foreach (int dir in TurnHeadings)
                        shots.Add(new Shot { Clip = clipOfState[state], State = state, Frame = frame, Dir = dir });
            }
            return shots;
        }

        /// <summary>
        /// Every <see cref="Plan"/> shot of <paramref name="def"/>, compared. First it proves the keyline
        /// mix (<see cref="KeylineMixAgrees"/>) against the rig's own <c>SHADING.keylineMix</c>, and
        /// refuses a def whose float mix the resolve could not mix as the rig does.
        /// </summary>
        public static Reading[] MeasureAll(IRigScriptHost host, CharacterSkinDef def,
                                           IReadOnlyDictionary<string, string> clipOfState)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            double rigMix = CharacterSkinExtractor.V9ShadingNumber(host, "keylineMix");
            if (!KeylineMixAgrees(rigMix, def.KeylineMix, out string miss))
                throw new InvalidOperationException(
                    $"'{def.Preset}': the resolve mixes the keyline in single precision and the rig in doubles, " +
                    $"and at SHADING.keylineMix = {rigMix.ToString("R", CultureInfo.InvariantCulture)} they part: {miss}. " +
                    "The def's keyline would be a different colour from the rig's wherever that pair meets.");
            List<Shot> plan = Plan(def, clipOfState);
            var readings = new Reading[plan.Count];
            var poser = new Poser(def);
            for (int i = 0; i < plan.Count; i++) readings[i] = Measure(host, def, plan[i], poser);
            return readings;
        }

        /// <summary>One shot, compared.</summary>
        public static Reading Measure(IRigScriptHost host, CharacterSkinDef def, Shot shot) =>
            Measure(host, def, shot, new Poser(def));

        static Reading Measure(IRigScriptHost host, CharacterSkinDef def, Shot shot, Poser poser)
        {
            byte[] truth = CharacterSkinExtractor.RenderTruth9(
                host, def.Preset, shot.Clip, shot.Frame, shot.Dir,
                new CharacterSkinExtractor.TruthOptions9 { SnapHead = def.HeadSnap, Look = shot.Look, Face = shot.Face },
                out RigMeshData posed, out double[] rigSnap);
            RigPaint9.Camera cam = CameraOf(host, shot.Dir, def.ElevationDeg);
            RigPaint9.Result mine = poser.Paint(shot, cam, out double[] defSnap);
            if (mine.W != posed.W || mine.H != posed.H)
                throw new InvalidOperationException(
                    $"'{def.Preset}' {shot.Label}: the def paints a {mine.W}x{mine.H} cell and rig 9 a {posed.W}x{posed.H} one.");
            return new Reading
            {
                Shot = shot,
                Diff = RigMeshReferenceRasterizer.Compare(truth, mine.Rgba, posed.W, posed.H),
                RigSnap = rigSnap,
                DefSnap = defSnap,
            };
        }

        /// <summary>
        /// The report the bake logs: the worst cluster against the bar and the shot that holds it, the
        /// totals, how closely the head snaps agree, and every shot that differs at all.
        /// </summary>
        public static string Report(CharacterSkinDef def, IList<Reading> readings, out int worstCluster)
        {
            var c = CultureInfo.InvariantCulture;
            worstCluster = 0;
            string worstAt = "none";
            long inked = 0, differing = 0;
            double snapDelta = 0;
            var lines = new StringBuilder();
            foreach (Reading r in readings)
            {
                inked += r.Diff.InkedPixels;
                differing += r.Diff.DifferingPixels;
                if (r.Diff.LargestDifferingCluster > worstCluster)
                {
                    worstCluster = r.Diff.LargestDifferingCluster;
                    worstAt = r.Shot.Label;
                }
                if (r.RigSnap != null && r.DefSnap != null)
                    for (int k = 0; k < 2; k++) snapDelta = Math.Max(snapDelta, Math.Abs(r.RigSnap[k] - r.DefSnap[k]));
                else if ((r.RigSnap == null) != (r.DefSnap == null))
                    snapDelta = double.PositiveInfinity;
                if (r.Diff.DifferingPixels > 0) lines.Append("  ").Append(r).Append('\n');
            }
            return string.Format(c,
                       "{0} shots of '{1}' against rig 9's own render (head snap {2}, edges and keyline on): worst " +
                       "cluster {3} px (bar {4}) at {5}; {6} of {7} inked px differ in all ({8:F4}%); the head snaps " +
                       "agree to {9:0.######} px; the keyline mix {10} is mixed in single precision, which equals " +
                       "the rig's mixHex on all 65,536 byte pairs.\n",
                       readings.Count, def.Preset, def.HeadSnap ? "on" : "off", worstCluster, ClusterBar, worstAt,
                       differing, inked, inked == 0 ? 0 : 100.0 * differing / inked, snapDelta,
                       def.KeylineMix.ToString("R", c)) +
                   (lines.Length > 0 ? "shots that differ:\n" + lines : "no shot differs by a pixel.\n");
        }

        /// <summary>
        /// True when the resolve's single-precision keyline mix equals the rig's on every byte pair.
        /// The rig computes <c>Math.round(A + (B − A)·mix)</c> in doubles. The resolve computes
        /// <c>floor(A + (B − A)·mixF + 0.5)</c> in floats, with the product either rounded before the add
        /// or fused into it. All three are compared over the 65,536 pairs, and the first pair where any
        /// two differ is named in <paramref name="firstMiss"/>.
        /// </summary>
        public static bool KeylineMixAgrees(double rigMix, float defMix, out string firstMiss)
        {
            for (int a = 0; a < 256; a++)
                for (int b = 0; b < 256; b++)
                {
                    int rig = (int)RigPaint9.JsRound(a + (b - a) * rigMix);
                    int single = RigPaint9.MixByteSingle(a, b, defMix);
                    // A fused multiply-add rounds once: the exact a + (b − a)·mixF is a 33-bit product plus
                    // a byte, which a double holds exactly, so this is its one rounding to float.
                    var fused = (float)(a + (double)(b - a) * defMix);
                    int fusedByte = (int)Math.Floor(fused + 0.5);
                    if (rig != single || rig != fusedByte)
                    {
                        firstMiss = string.Format(CultureInfo.InvariantCulture,
                            "keyline byte {0} over {1}: the rig mixes {2}, the resolve {3} (fused {4})",
                            a, b, rig, single, fusedByte);
                        return false;
                    }
                }
            firstMiss = null;
            return true;
        }

        /// <summary>The rig's <c>camOf({dir, elev})</c>, read off the rig so its sines and cosines are V8's.</summary>
        public static RigPaint9.Camera CameraOf(IRigScriptHost host, int dir, double elevationDeg)
        {
            var c = CultureInfo.InvariantCulture;
            string g = CharacterSkinExtractor.V9GlobalName;
            host.Execute($"globalThis.__hhCam9={g}.camOf({{dir:{dir.ToString(c)},elev:{elevationDeg.ToString("R", c)}}});");
            try
            {
                double N(string f) => host.EvaluateNumber("globalThis.__hhCam9." + f);
                return new RigPaint9.Camera
                {
                    Ct = N("ct"), St = N("st"), Se = N("se"), Ce = N("ce"), Cr = N("cr"), Sr = N("sr"),
                    Cq = N("cq"), Sq = N("sq"), S = N("S"), Heave = N("heave"),
                    W = (int)N("W"), H = (int)N("H"), Cx = (int)N("cx"), Cy = (int)N("cy"),
                };
            }
            finally { host.Execute("delete globalThis.__hhCam9;"); }
        }

        // ------------------------------------------------------------------------------------------

        /// <summary>The def's figure, set up once per def: its bind mesh's faces, weights and face
        /// attributes, the materials and ink as <see cref="RigPaint9"/> reads them, and the buffers a
        /// pose writes.</summary>
        sealed class Poser
        {
            readonly CharacterSkinDef _def;
            readonly Vector3[] _srcVerts, _srcNorms, _outVerts, _outNorms;
            readonly BoneWeight[] _weights;
            readonly Matrix4x4[] _bindposes, _world, _skin;
            readonly int[] _starts, _counts;
            readonly Vector4[] _attr, _face;
            readonly RigPaint9.Material[] _mats;
            readonly RigPaint9.Ink _ink;

            public Poser(CharacterSkinDef def)
            {
                _def = def ?? throw new ArgumentNullException(nameof(def));
                if (def.ToneRule != ToneRule.V9 || !def.HasFace)
                    throw new InvalidOperationException($"'{def.Preset}' is not a v9 def with a face; the ink comparison is rig 9's.");
                Mesh mesh = def.BindMesh;
                if (mesh == null) throw new InvalidOperationException($"'{def.Preset}' has no bind mesh.");
                _srcVerts = mesh.vertices;
                _srcNorms = mesh.normals;
                _weights = mesh.boneWeights;
                _bindposes = mesh.bindposes;
                int n = _srcVerts.Length;
                _outVerts = new Vector3[n];
                _outNorms = new Vector3[n];
                _world = new Matrix4x4[def.BoneCount];
                _skin = new Matrix4x4[def.BoneCount];
                if (!CharacterSkinPose.TryFaceRuns(mesh.triangles, n, out _starts, out _counts))
                    throw new InvalidOperationException($"'{def.Preset}''s bind mesh is not one fan per face.");
                var uv = new List<Vector4>(n);
                mesh.GetUVs(RigMeshBuilder.AttrUvChannel, uv);
                _attr = uv.ToArray();
                uv.Clear();
                mesh.GetUVs(RigMeshBuilder.FaceUvChannel, uv);
                _face = uv.ToArray();
                if (_attr.Length != n || _face.Length != n)
                    throw new InvalidOperationException(
                        $"'{def.Preset}''s bind mesh carries {_attr.Length} facet and {_face.Length} face attributes for {n} vertices.");

                _mats = new RigPaint9.Material[def.Materials.Length];
                for (int m = 0; m < _mats.Length; m++)
                {
                    CharacterSkinDef.Material src = def.Materials[m];
                    var ramp = new int[src.Colors.Length];
                    for (int k = 0; k < ramp.Length; k++) ramp[k] = Rgb(src.Colors[k]);
                    // A v9 fixed material is a one-colour ramp with the window 0..0: its step is 0 and
                    // it never drops, which is what the rig's `fixed` does (CharacterSkinDef.Material).
                    _mats[m] = new RigPaint9.Material
                    {
                        Ramp = ramp, Gain = def.Gain * src.Gain, Bias = src.BiasOr(def.Bias),
                        Lo = src.ToneLo, Hi = src.ToneHi, Off = src.Offset, Fixed = false,
                    };
                }
                _ink = new RigPaint9.Ink
                {
                    KeyX = def.KeyScreen.x, KeyY = def.KeyScreen.y, KeyZ = def.KeyScreen.z,
                    Form = def.Form, FormMid = def.FormMid, Edge = def.Edge,
                    Keyline = Rgb(def.Keyline), KeylineMix = def.KeylineMix,
                    CullFloor = def.FaceCullFloor, SinglePrecisionMix = true,
                };
            }

            public RigPaint9.Result Paint(Shot shot, in RigPaint9.Camera cam, out double[] snap)
            {
                CharacterSkinDef def = _def;
                int ci = ClipIndex(def, shot.State);
                if (ci < 0) throw new InvalidOperationException($"'{def.Preset}' has no clip '{shot.State}'.");
                CharacterSkinDef.SkinClip clip = def.Clips[ci];
                int frame = ((shot.Frame % clip.FrameCount) + clip.FrameCount) % clip.FrameCount;

                CharacterSkinPose.ComposeWorld(clip, frame, def.Bones, _world);
                if (shot.Look.HasValue && def.HasLook)
                {
                    var limits = CharacterFigureLook.Limits.Of(def);
                    Vector2 l = shot.Look.Value;
                    CharacterSkinPose.ApplyTurn(clip, frame, def.Bones, _world,
                        def.LookNeckBone, CharacterFigureLook.TurnOf(l.x, l.y, def.LookSplitNeck, limits),
                        def.LookHeadBone, CharacterFigureLook.TurnOf(l.x, l.y, def.LookSplitHead, limits));
                }
                snap = null;
                if (def.HeadSnap)
                {
                    Vector3 hp = CharacterSkinPose.HeadPoint(def, _world);
                    snap = RigPaint9.Snap(hp.x, hp.y, hp.z, cam);
                }
                CharacterSkinPose.FinishSkin(_world, _bindposes, _skin);
                CharacterSkinPose.Skin(_skin, _weights, _srcVerts, _srcNorms, _outVerts, _outNorms);

                var active = new bool[def.FaceGroups.Length + 1];
                for (int s = 0; s < CharacterSkinDef.FaceSlots; s++)
                {
                    int g = shot.Face != null
                        ? def.FaceGroupId(CharacterSkinDef.FaceSlotNames[s] + "." + shot.Face[s])
                        : clip.HasFaceTrack ? clip.FaceGroupOf(frame, s) : def.RestFace[s];
                    if (g < 1 || g > def.FaceGroups.Length)
                        throw new InvalidOperationException($"'{def.Preset}' {shot.Label} shows no {CharacterSkinDef.FaceSlotNames[s]} group.");
                    active[g] = true;
                }

                var faces = new List<RigPaint9.Face>(_starts.Length);
                for (int f = 0; f < _starts.Length; f++)
                {
                    int s0 = _starts[f], nv = _counts[f];
                    Vector4 fa = _face[s0], at = _attr[s0];
                    int group = Mathf.RoundToInt(fa.x);
                    if (group != CharacterSkinDef.NoFaceGroup && !active[group]) continue;
                    var v = new double[nv * 3];
                    for (int k = 0; k < nv; k++)
                    {
                        Vector3 p = _outVerts[s0 + k];
                        v[3 * k] = p.x; v[3 * k + 1] = p.y; v[3 * k + 2] = p.z;
                    }
                    faces.Add(new RigPaint9.Face
                    {
                        V = v, Mat = Mathf.RoundToInt(at.x), B = at.y, Db = at.z,
                        MinT = MinTowardOf(def, Mathf.RoundToInt(fa.y)), Head = fa.z > 0.5f,
                    });
                }
                return RigPaint9.Paint(faces.ToArray(), cam, _mats, _ink, snap, keyline: true, edges: true);
            }
        }

        /// <summary>A face role's threshold: 0 for a body face (it culls at the floor alone), else the
        /// def's <see cref="CharacterSkinDef.FaceMinToward"/> component.</summary>
        static double MinTowardOf(CharacterSkinDef def, int role)
        {
            switch ((CharacterSkinDef.FaceRole)role)
            {
                case CharacterSkinDef.FaceRole.Body: return 0;
                case CharacterSkinDef.FaceRole.Near: return def.FaceMinToward.x;
                case CharacterSkinDef.FaceRole.Far: return def.FaceMinToward.y;
                case CharacterSkinDef.FaceRole.Side: return def.FaceMinToward.z;
                case CharacterSkinDef.FaceRole.Mouth: return def.FaceMinToward.w;
                default: throw new InvalidOperationException($"'{def.Preset}' has a face of role {role}, which is no role.");
            }
        }

        static int ClipIndex(CharacterSkinDef def, string state)
        {
            for (int i = 0; i < def.Clips.Length; i++)
                if (string.Equals(def.Clips[i].StateKey, state, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>The rest face as one state per slot (<c>open, flat, flat</c>).</summary>
        static string[] RestStates(CharacterSkinDef def)
        {
            var rest = new string[CharacterSkinDef.FaceSlots];
            for (int s = 0; s < rest.Length; s++) rest[s] = StateOf(def.FaceGroups[def.RestFace[s] - 1]);
            return rest;
        }

        static string StateOf(string group) => group.Substring(group.IndexOf('.') + 1);

        static int Rgb(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;
    }
}
