using System;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// The runtime shape of the ROD FISHING KIT's anchor sidecars — plain serializable data the start
    /// builder fills by parsing the baked JSONs (<c>RodIsoAnchors.json</c> / <c>BobberAnchors.json</c> /
    /// <c>FishIsoAnchors.json</c> — ADR 0021 §4: geometry comes from the rig as DATA, never hand-typed)
    /// and converting every pixel anchor to WORLD METRES through <see cref="RodKitAnchorMath"/> with the
    /// sheets' own import PPU. Nothing here is authored by hand: re-running the builder after a re-bake
    /// refreshes it all, and a missing sidecar simply leaves the arrays empty (the consuming presenter
    /// element stays inert — the null-safe greybox rule).
    /// </summary>
    [Serializable]
    public sealed class RodStateVisual
    {
        [Tooltip("The RIG STATE this entry is, spelled exactly as the rig spells it — one of " +
                 "RodPresenterMath.RodStates. This is the key, not a label: RodSheetFor resolves " +
                 "through it by name, so a sheet can never quietly stand in for a state it is not.")]
        public string State;

        // ---- what the rod IS, from the rig, so a swap can prove it is swapping the same rod -------
        // Every state's sheet is one cell of ONE rod. These four say which rod, and RodPresenterMath
        // .SameRod holds every wired state to the first one's answer. They are baked, never typed:
        // the anchors sidecar carries them because the rig does.

        [Tooltip("The rod cell's pixel size — the rig's own cell, identical in every state.")]
        public int CellW, CellH;

        [Tooltip("The GRIP CENTRE in that cell, top-left px. The sprite pins this to the fisher's " +
                 "hand, so it means the same thing in every state or the rod teleports between them.")]
        public float PivotX, PivotY;

        [Tooltip("Which rod this is ('cane' / 'coast' / 'deep'). A tier seam is a different rod and " +
                 "must be re-wired whole, never state by state.")]
        public string Tier;

        [Tooltip("The blank's length in metres, from the rig's tier table. A rod does not get longer " +
                 "because it was put down.")]
        public float BlankLenM;

        [Tooltip("For a REST state: how far above its resting surface (ground, rack) the settled rod " +
                 "holds its grip, in metres. 0 for a held state. This is the ground datum that used " +
                 "to live inside the render as a pivot offset — as data it places the rod; as pixels " +
                 "it moved the rod.")]
        public float RestLiftM;

        [Tooltip("For a REST state: how many of the state's frames still have the rod IN HAND. The " +
                 "release happens at that frame — mid-animation, watchable — and never at the seam.")]
        public int HeldFramesPerDir;

        [Tooltip("The rod sheet for this state: 8 directions × FramesPerDir, d/f order.")]
        public Sprite[] Frames;

        public int FramesPerDir;

        [Tooltip("Where the rod's GRIP sits, world metres from the ANGLER'S PIVOT, per [dir·FramesPerDir " +
                 "+ frame] — from the rig's grips table (the rod sprite's pivot IS the grip centre).")]
        public Vector2[] GripOffsets;

        [Tooltip("Where the rod TIP sits, world metres from the GRIP, per [dir·FramesPerDir + frame] — " +
                 "the fishing line starts here.")]
        public Vector2[] TipOffsets;
    }

    /// <summary>One bobber state ('float', 'nibble', 'strike', 'fly') — single-direction frames plus the
    /// stem-top line-attach point per frame (world m from the bobber's WATERLINE pivot).</summary>
    [Serializable]
    public sealed class BobberStateVisual
    {
        public string State;
        public Sprite[] Frames;
        [Tooltip("Seconds per frame, from the rig's per-state ms.")]
        public float SecondsPerFrame;
        public Vector2[] LineAttachOffsets;
    }

    /// <summary>One species' fight sheets + mouth anchors, keyed by the FishSpeciesDef id the catch
    /// publishes (<c>FishingState.FishId</c>). Which held sheet ships (gill vs tail) was decided at
    /// build time from the rig's hold.hands — data, not code.</summary>
    [Serializable]
    public sealed class FishSpeciesVisual
    {
        [Tooltip("The species id FishingState.FishId publishes (e.g. 'fish.atlantic_cod').")]
        public string FishId;

        public Sprite[] ShadowFrames;   // 8 dirs × n, d/f — the deep, unseen shape
        public int ShadowFramesPerDir;

        public Sprite[] DartFrames;     // 8 dirs × n — the surfaced run
        public int DartFramesPerDir;
        [Tooltip("Mouth (line-attach) offset, world m from the fish pivot, per [dir·FramesPerDir+frame].")]
        public Vector2[] DartMouthOffsets;

        public Sprite[] ThrashFrames;   // 8 dirs × n — the station-holding head-shake
        public int ThrashFramesPerDir;
        public Vector2[] ThrashMouthOffsets;

        public Sprite[] HeldFrames;     // 8 dirs × n — gill (two-handed) or tail (one-handed) hold
        public int HeldFramesPerDir;
        [Tooltip("From the rig's hold.hands: true = both hands (held at the hands' midpoint).")]
        public bool TwoHanded;
    }

    /// <summary>
    /// The pixel→world conversions for the kit's anchor tables — pure and pinned by EditMode tests,
    /// because eyeballed anchor offsets are exactly how overlay rigs go subtly wrong (the
    /// overlay-pose-rotates-about-the-origin lesson). Cell pixel coords are TOP-LEFT origin,
    /// screen-down-positive (the rigs' convention); world is metres, y-up, PPU from the sheet's import.
    /// </summary>
    public static class RodKitAnchorMath
    {
        /// <summary>A cell-space point relative to the cell's PIVOT, as a world-metre offset
        /// (x right, y up — the y axis flips).</summary>
        public static Vector2 CellPxToPivotWorld(float px, float py, float pivotX, float pivotY, float ppu)
        {
            float u = Mathf.Max(1e-3f, ppu);
            return new Vector2((px - pivotX) / u, (pivotY - py) / u);
        }

        /// <summary>A pivot-relative pixel OFFSET (screen-down dy) as a world-metre offset.</summary>
        public static Vector2 OffsetPxToWorld(float dx, float dy, float ppu)
        {
            float u = Mathf.Max(1e-3f, ppu);
            return new Vector2(dx / u, -dy / u);
        }
    }
}
