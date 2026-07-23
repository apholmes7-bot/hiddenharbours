using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The runtime shape of <c>CatchStorageAnchors.json</c> — the container-fill geometry the
    /// storage bake exports from the rigs (ADR 0021 §4: geometry comes from the rig as DATA, never
    /// restated in a README or hand-typed into a component).
    ///
    /// <para>Written by <c>CatchStorageBaker</c> with <c>JsonUtility.ToJson</c> on THIS class and
    /// parsed back with <see cref="Parse"/>, so serializer and parser cannot drift. All pixel
    /// offsets are screen-space cell px FROM THE CONTAINER PIVOT, screen-down-positive (the rigs'
    /// convention); consumers negate dy for Unity's y-up. Direction index d matches the baked
    /// sheet rows: the baker applies the rig's MEASURED azimuth convention before export, so
    /// <c>byDir[d]</c> genuinely belongs to the sprite in row d (heading 45°·d).</para>
    /// </summary>
    [Serializable]
    public sealed class CatchStorageAnchors
    {
        /// <summary>Rig source path, for provenance (e.g. "docs/art/rigs/fishToteRig.js").</summary>
        public string rig;

        /// <summary>The measured azimuth convention the export already applied ("Clockwise" /
        /// "CounterClockwise") — informational; rows are corrected, nothing should re-correct.</summary>
        public string convention;

        /// <summary>Human note written by the baker.</summary>
        public string note;

        /// <summary>The insulated fish tote (fishToteRig.js) — the one container whose fill is
        /// DRAWN from real items on projected slot points.</summary>
        public CatchContainerAnchors tote;

        public static CatchStorageAnchors Parse(string json) =>
            string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<CatchStorageAnchors>(json);
    }

    /// <summary>One container's per-direction fill geometry.</summary>
    [Serializable]
    public sealed class CatchContainerAnchors
    {
        public int cellW;
        public int cellH;

        /// <summary>Container pivot in TOP-LEFT cell px (the rigs' screen origin).</summary>
        public float pivotX;
        public float pivotY;

        /// <summary>Index d = baked sheet row d (heading 45°·d, convention already applied).</summary>
        public CatchDirAnchors[] byDir;

        /// <summary>Slot capacity — how many items a brim-full container draws.</summary>
        public int Capacity =>
            byDir != null && byDir.Length > 0 && byDir[0].slots != null ? byDir[0].slots.Length : 0;
    }

    /// <summary>One direction row: the fill-surface slot points (floor layer first, back-to-front
    /// within each layer — drawing the first N always reads as layers stacking) and the projected
    /// rim opening the items clip to (the front wall occludes the low layers).</summary>
    [Serializable]
    public sealed class CatchDirAnchors
    {
        public CatchAnchorPoint[] slots;
        public CatchAnchorPoint[] opening;
    }

    /// <summary>A screen-space cell-px offset from the container pivot (screen-down-positive dy).</summary>
    [Serializable]
    public sealed class CatchAnchorPoint
    {
        public int dx;
        public int dy;
    }
}
