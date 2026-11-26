using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One idle fidget</b>: a short thing a person standing idle does on their own (looks along the
    /// wharf, glances at the sky, checks the water at their feet). One asset per fidget, a stable id
    /// (<c>idle.look_around</c>), the whole set listed by <see cref="IdleFidgetLibraryDef"/>.
    ///
    /// <para><b>Two channels.</b> A <see cref="Channel.Look"/> fidget is a gaze track: where the eyes
    /// point, as yaw and elevation against the figure's own forward, keyed in seconds from the fidget's
    /// start. It is drawn through the rig's own look (the neck and head turn and the eyes,
    /// <see cref="CharacterFigureLook"/>), so the current rig plays it on every facing without a new
    /// export. A <see cref="Channel.Clip"/> fidget names a rig clip (<see cref="ClipKey"/>) and plays it
    /// once; a skin that carries no such clip simply never picks it (<see cref="AvailableFor"/>), so the
    /// asset can ship before the rig export that makes it drawable.</para>
    ///
    /// <para>Presentation only (rule 5): nothing here is saved, and nothing here draws a random number.
    /// Which fidget plays and when is <see cref="IdleFidgetSchedule"/>'s pure function.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Idle Fidget", fileName = "IdleFidget")]
    public sealed class IdleFidgetDef : ScriptableObject
    {
        /// <summary>How a fidget is drawn.</summary>
        public enum Channel
        {
            /// <summary>A gaze track through the rig's look (neck, head, eyes).</summary>
            Look = 0,

            /// <summary>A rig clip, played once from its first frame.</summary>
            Clip = 1,
        }

        /// <summary>One key of a gaze track.</summary>
        [Serializable]
        public struct GazeKey
        {
            [Tooltip("Seconds from the fidget's start. Keys are read in order; between two keys the gaze " +
                     "moves in a straight line, and it holds at the first and last key.")]
            [Min(0f)] public float AtSeconds;

            [Tooltip("Where the eyes point across, degrees from the figure's own forward: + is to the " +
                     "figure's RIGHT. The rig's head share and limits decide how much of it the head takes.")]
            [Range(-180f, 180f)] public float YawDegrees;

            [Tooltip("Where the eyes point up and down, degrees above the level: + looks UP (the sky), " +
                     "- looks DOWN (the water at his feet). The rig's pitch limits clamp it.")]
            [Range(-80f, 80f)] public float UpDegrees;
        }

        [Header("Identity")]
        [Tooltip("Stable id, append-only: idle.<snake_case>.")]
        public string Id = "idle.unnamed";

        [Header("Choice")]
        [Tooltip("How often this fidget is chosen against the others: its share of the summed weights of " +
                 "the fidgets the figure can draw. 0 = never.")]
        [Min(0f)] public float Weight = 1f;

        [Tooltip("How long the fidget lasts, seconds. A Look fidget's last key should sit at or before " +
                 "this; a Clip fidget holds its last frame to the end.")]
        [Min(0f)] public float DurationSeconds = 3f;

        [Header("Drawing")]
        [Tooltip("Look: a gaze track through the rig's own look (every rig with a look draws it). Clip: a " +
                 "rig clip named below, drawn only by a skin that carries it.")]
        public Channel Kind = Channel.Look;

        [Tooltip("Look only: the gaze track. The first and last keys should be level and straight ahead " +
                 "(0, 0) so the fidget starts and ends on the figure's own look.")]
        public GazeKey[] Gaze = Array.Empty<GazeKey>();

        [Tooltip("Clip only: the skin clip's state key (e.g. 'idle_scratch'), as the rig export names it.")]
        public string ClipKey = "";

        /// <summary>
        /// True when <paramref name="skin"/> can draw this fidget: a Look fidget on a skin with a look (or
        /// no skin, for a pure schedule), a Clip fidget on a skin that carries the clip. A fidget with no
        /// weight or no length is never available.
        /// </summary>
        public bool AvailableFor(CharacterSkinDef skin)
        {
            if (!(Weight > 0f) || !(DurationSeconds > 0f)) return false;
            if (Kind == Channel.Clip)
                return skin != null && !string.IsNullOrEmpty(ClipKey) && skin.TryGetClip(ClipKey, out _);
            return Gaze != null && Gaze.Length > 0 && (skin == null || skin.HasLook);
        }

        /// <summary>
        /// The gaze at <paramref name="localSeconds"/> into the fidget: linear between keys, held before the
        /// first and after the last. False for a fidget with no gaze track. Allocation-free.
        /// </summary>
        public bool TrySampleGaze(double localSeconds, out float yawDegrees, out float upDegrees)
        {
            yawDegrees = upDegrees = 0f;
            if (Kind != Channel.Look || Gaze == null || Gaze.Length == 0) return false;
            GazeKey first = Gaze[0];
            if (localSeconds <= first.AtSeconds)
            {
                yawDegrees = first.YawDegrees;
                upDegrees = first.UpDegrees;
                return true;
            }
            for (int i = 1; i < Gaze.Length; i++)
            {
                GazeKey b = Gaze[i];
                if (localSeconds >= b.AtSeconds) continue;
                GazeKey a = Gaze[i - 1];
                double span = b.AtSeconds - a.AtSeconds;
                float w = span > 0d ? (float)((localSeconds - a.AtSeconds) / span) : 1f;
                yawDegrees = Mathf.Lerp(a.YawDegrees, b.YawDegrees, w);
                upDegrees = Mathf.Lerp(a.UpDegrees, b.UpDegrees, w);
                return true;
            }
            GazeKey last = Gaze[Gaze.Length - 1];
            yawDegrees = last.YawDegrees;
            upDegrees = last.UpDegrees;
            return true;
        }

        /// <summary>
        /// The point the eyes look at, in the figure's own frame (rig metres, +x right, +y forward, +z up):
        /// <paramref name="eye"/> plus <paramref name="distanceMetres"/> along the gaze. This is what the
        /// rig's look is handed (<c>IsoCharacterFigureRenderer.Life.Target</c>). Pure.
        /// </summary>
        public static Vector3 GazeTarget(Vector3 eye, float yawDegrees, float upDegrees, float distanceMetres)
        {
            float yaw = yawDegrees * Mathf.Deg2Rad, up = upDegrees * Mathf.Deg2Rad;
            float level = Mathf.Cos(up);
            var dir = new Vector3(Mathf.Sin(yaw) * level, Mathf.Cos(yaw) * level, Mathf.Sin(up));
            return eye + dir * distanceMetres;
        }

        /// <summary>The frame of a Clip fidget's clip at <paramref name="localSeconds"/>: played once from
        /// frame 0 at the clip's own rate, holding its last frame. Pure.</summary>
        public static int ClipFrame(in CharacterSkinDef.SkinClip clip, double localSeconds)
        {
            int n = clip.FrameCount;
            if (n <= 1 || !(localSeconds > 0d)) return 0;
            float fps = clip.FramesPerSecond > 0f ? clip.FramesPerSecond : CharacterSkinPose.FallbackFramesPerSecond;
            double ticks = Math.Floor(localSeconds * fps);
            return ticks >= n - 1 ? n - 1 : (int)ticks;
        }
    }
}
