using System;
using System.Globalization;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The figure's held-prop sockets by their rig ids (ADR 0053): the mount bones the character rigs
    /// pose a tool on and that own no vertex. Rig contract names, like <see cref="CarryPropKeys"/>; a
    /// holder names the one it mounts on as data (<c>ToolDef.HeldMeshMountBone</c>).
    /// </summary>
    public static class HeldPropSockets
    {
        /// <summary>The right hand's tool mount: the palm point, turned to the tool's aim on a frame
        /// whose tool track holds a tool. The rod and the spade grip here.</summary>
        public const string RightHandTool = "tool_R";

        /// <summary>The left hand's tool mount.</summary>
        public const string LeftHandTool = "tool_L";
    }

    /// <summary>
    /// <b>How a baked held prop sits on a figure's socket</b> (ADR 0053), as pure functions a test can
    /// prove without a scene.
    ///
    /// <para><b>The frame.</b> A held mesh is the rig's own faces in its model frame, grip at the origin:
    /// +y the figure's forward, +z up. The character rig aims a held tool with the same function the
    /// rod rig builds its faces with, <c>aimM(pitch, yaw) = Rz(−yaw)·Rx(pitch)</c>, which takes +y onto
    /// <c>(sin yaw·cos pitch, cos yaw·cos pitch, sin pitch)</c>. A def baked at (p0, y0) therefore sits at
    /// the clip's (p, y) under <c>aimM(p, y)·aimM(p0, y0)⁻¹</c>, and at its own baked pose under the
    /// identity (D1: the rig's held pose, relative to the body).</para>
    /// </summary>
    public static class HeldPropPose
    {
        /// <summary>The rig's <c>aimM(pitch, yaw)</c>, degrees: +y onto the tool's direction.</summary>
        public static Quaternion ToolRotation(float pitchDeg, float yawDeg) =>
            Quaternion.AngleAxis(-yawDeg, Vector3.forward) * Quaternion.AngleAxis(pitchDeg, Vector3.right);

        /// <summary>The rig's tool direction, restated from its formula (<c>rodIsoRig.js</c> atFn), so a
        /// test can hold <see cref="ToolRotation"/> to it.</summary>
        public static Vector3 ToolDirection(float pitchDeg, float yawDeg)
        {
            float p = pitchDeg * Mathf.Deg2Rad, y = yawDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(y) * Mathf.Cos(p), Mathf.Cos(y) * Mathf.Cos(p), Mathf.Sin(p));
        }

        /// <summary>The rotation that takes a mesh baked at (<paramref name="bakedPitchDeg"/>,
        /// <paramref name="bakedYawDeg"/>) to the aim (<paramref name="pitchDeg"/>, <paramref name="yawDeg"/>),
        /// in the figure's frame. The identity when the two agree.</summary>
        public static Quaternion FromBakedPose(float bakedPitchDeg, float bakedYawDeg, float pitchDeg, float yawDeg) =>
            ToolRotation(pitchDeg, yawDeg) * Quaternion.Inverse(ToolRotation(bakedPitchDeg, bakedYawDeg));

        /// <summary>
        /// Read the pitch and yaw a def was baked at off its provenance line
        /// (<see cref="HeldPropMeshDef.SourcePose"/>, e.g. <c>held: pitch 56.0000 deg, yaw 16.0000 deg,
        /// bend 0, zOff 0</c>). False when either is missing: a prop whose baked aim is unknown is refused,
        /// never guessed. Allocates; call once per def.
        /// </summary>
        public static bool TryParseSourcePose(string sourcePose, out float pitchDeg, out float yawDeg)
        {
            pitchDeg = yawDeg = 0f;
            return TryReadNumberAfter(sourcePose, "pitch ", out pitchDeg)
                && TryReadNumberAfter(sourcePose, "yaw ", out yawDeg);
        }

        private static bool TryReadNumberAfter(string text, string label, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(text)) return false;
            int at = text.IndexOf(label, StringComparison.Ordinal);
            if (at < 0) return false;
            int start = at + label.Length, end = start;
            while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '.' || text[end] == '-' || text[end] == '+'))
                end++;
            return end > start && float.TryParse(text.Substring(start, end - start), NumberStyles.Float,
                                                 CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// The tool key that aims a held <paramref name="kind"/> on <paramref name="frame"/>: that frame's
        /// own when it holds one, else the nearest frame's that does (earlier first at equal distance).
        /// False when the track holds no such tool on any frame. Allocation-free.
        /// </summary>
        public static bool TryNearestHeldKey(CharacterSkinDef.ToolKey[] track, int frame, string kind,
                                             out CharacterSkinDef.ToolKey key)
        {
            key = default;
            if (track == null || track.Length == 0 || string.IsNullOrEmpty(kind)) return false;
            int f = Mathf.Clamp(frame, 0, track.Length - 1);
            for (int d = 0; d < track.Length; d++)
            {
                int before = f - d, after = f + d;
                if (before >= 0 && Holds(track[before], kind)) { key = track[before]; return true; }
                if (d > 0 && after < track.Length && Holds(track[after], kind)) { key = track[after]; return true; }
                if (before < 0 && after >= track.Length) break;
            }
            return false;
        }

        private static bool Holds(in CharacterSkinDef.ToolKey k, string kind) =>
            k.Held && string.Equals(k.Kind, kind, StringComparison.Ordinal);

        /// <summary>
        /// The prop's tip in its own metres: the plotted-line sample farthest from the grip (the rod
        /// blank's last sample). Eye dots do not count. False when the def plots no line.
        /// </summary>
        public static bool TryTipLocal(HeldPropMeshDef def, out Vector3 tip)
        {
            tip = Vector3.zero;
            if (def == null || def.Lines == null) return false;
            float best = -1f;
            foreach (HeldPropMeshDef.Line line in def.Lines)
            {
                if (line.Dots || line.Points == null) continue;
                foreach (HeldPropMeshDef.LinePoint p in line.Points)
                {
                    float d = (p.Position - def.GripLocalMeters).sqrMagnitude;
                    if (d > best) { best = d; tip = p.Position; }
                }
            }
            return best >= 0f;
        }
    }
}
