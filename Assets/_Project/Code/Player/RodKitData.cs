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

    /// <summary>
    /// One species' fight sheets + mouth anchors <b>at one rung of its size ladder</b>.
    ///
    /// <para>Everything here is size-dependent, which is why it is a type of its own: the sheets are
    /// baked at the rung's scale, the mouth offsets are measured at that scale (the rig rounds them
    /// to whole pixels, so they cannot be scaled after the fact), and even the CARRY changes — the
    /// same species is a one-hander at the bottom of its band and a two-arm cradle at the top.</para>
    /// </summary>
    [Serializable]
    public sealed class FishRungVisual
    {
        [Tooltip("Sheet-stem suffix this rung was baked under: '_sm', '' (the middle rung keeps the " +
                 "legacy stem) or '_lg'. Carried for diagnostics — nothing resolves through it at " +
                 "run time, because the sprites are already wired.")]
        public string Suffix;

        [Tooltip("The catch weight this rung DRAWS, kg, from the rig's own hold() at the rung's " +
                 "scale. This is the ladder: the picker chooses the rung nearest the landed fish.")]
        public float Kg;

        [Tooltip("From the rig's hold().hands AT THIS RUNG: true = the two-arm cradle. It is a " +
                 "property of the size, not of the species — a small cod is carried by the gill and " +
                 "a large one is cradled.")]
        public bool TwoHanded;

        public Sprite[] ShadowFrames;   // 8 dirs × n, d/f — the deep, unseen shape
        public int ShadowFramesPerDir;

        public Sprite[] DartFrames;     // 8 dirs × n — the surfaced run
        public int DartFramesPerDir;
        [Tooltip("Mouth (line-attach) offset, world m from the fish pivot, per [dir·FramesPerDir+frame].")]
        public Vector2[] DartMouthOffsets;

        public Sprite[] ThrashFrames;   // 8 dirs × n — the station-holding head-shake
        public int ThrashFramesPerDir;
        public Vector2[] ThrashMouthOffsets;

        public Sprite[] HeldFrames;     // 8 dirs × n — gill or tail grip; hand count comes from weight
        public int HeldFramesPerDir;

        [Tooltip("8 dirs × n, n = length / 8 — the fish hanging head-up by the gill, as she hangs from " +
                 "the line once she is out of the water. Every rung bakes the gill rest whatever its hand " +
                 "count, so a one-hand rung (held by the tail) still hangs head-up. Empty = HeldFrames.")]
        public Sprite[] LineHangFrames;

        // ---- catch pass 2's two new water anims -------------------------------------------------
        //
        // Both are SURFACE beats and both were missing from pass 1 entirely, which is why the fight
        // has only ever had two things a hooked fish can do at the top: run (dart) or shake
        // (thrash). Wiring them to the sim's states is the visible-fish arc's job, not this one —
        // these carry the art so that lane has something to reach for.

        [Tooltip("8 dirs × n — the belly flash as she rolls at the surface. 4f in the rig.")]
        public Sprite[] RollFrames;
        public int RollFramesPerDir;
        public Vector2[] RollMouthOffsets;

        [Tooltip("8 dirs × n — the breach: a z arc and pitch baked into the frames. 6f in the rig, " +
                 "and the rig's MOTION.jump.travel says she covers 0.55 m over them, so a presenter " +
                 "must MOVE her across the anim rather than play it on the spot.")]
        public Sprite[] JumpFrames;
        public int JumpFramesPerDir;
        public Vector2[] JumpMouthOffsets;
    }

    /// <summary>
    /// One species' fight art across its whole size ladder, keyed by the FishSpeciesDef id the catch
    /// publishes (<c>FishingState.FishId</c>).
    ///
    /// <para><b>Why a ladder and not one sheet set.</b> The runtime never scales a fish sprite — a
    /// scaled pixel sprite is exactly the mush this project's whole art pipeline exists to avoid — so
    /// the only way a 2 kg cod and a 12 kg cod can look different is to have been drawn different
    /// sizes. Catch pass 2 bakes three rungs per species spanning the weight band the game actually
    /// rolls; this is the table that lets the fight pick one.</para>
    /// </summary>
    [Serializable]
    public sealed class FishSpeciesVisual
    {
        [Tooltip("The species id FishingState.FishId publishes (e.g. 'fish.atlantic_cod').")]
        public string FishId;

        [Tooltip("The size ladder, ascending by weight — the baked rungs of this species. The " +
                 "thresholds ARE these weights; there is no separate table to keep in step.")]
        public FishRungVisual[] Rungs;

        /// <summary>
        /// The rung to draw for a landed fish of <paramref name="weightKg"/>, or null when this
        /// species has no rungs wired at all.
        /// </summary>
        public FishRungVisual RungFor(float weightKg)
        {
            int i = FishSizeLadder.PickRung(Rungs, weightKg);
            return i < 0 ? null : Rungs[i];
        }
    }

    /// <summary>
    /// Which rung of a baked size ladder draws a given catch — pure, so an EditMode test can pin it
    /// without a scene.
    /// </summary>
    public static class FishSizeLadder
    {
        /// <summary>
        /// The index of the rung closest to <paramref name="weightKg"/> <b>in rendered LENGTH</b>, or
        /// −1 when there are no rungs.
        ///
        /// <para><b>Why length and not mass.</b> The rig's own law is
        /// <c>mass ∝ scale³</c>, so length goes as the cube root of weight, and the ladder is
        /// deliberately spaced evenly in rendered LENGTH — that is what makes its three rungs look
        /// equally far apart. Picking "nearest in kg" would therefore split the band off-centre and
        /// send most of a species' catches to the small rung: for cod (2 / 5.59 / 12 kg) the kg
        /// midpoints fall at 3.79 and 8.80 kg, while the length midpoints fall at 3.49 and 8.39 — the
        /// difference is a whole size of fish either side. The player sees the fish's LENGTH, so the
        /// metric that decides which drawing is "closest" has to be the one they are looking at.</para>
        ///
        /// <para>No threshold is stored anywhere: the boundaries are derived from the rung weights
        /// themselves, so a re-bake that moves the ladder moves the picker with it and there is no
        /// second table to fall out of step (rule 6 — the tunable is the ladder, in the asset).</para>
        /// </summary>
        public static int PickRung(FishRungVisual[] rungs, float weightKg)
        {
            if (rungs == null || rungs.Length == 0) return -1;

            int best = -1;
            float bestGap = float.MaxValue;
            for (int i = 0; i < rungs.Length; i++)
            {
                if (rungs[i] == null) continue;
                // The same distance the catch art table picks its sized kinds by, so the fish on the
                // line and the fish in the hands are always the same drawing.
                float gap = HiddenHarbours.Core.CatchSize.LengthGap(rungs[i].Kg, weightKg);
                // Strictly-less keeps the FIRST (smaller) rung on an exact tie, so a fish sitting
                // precisely on a boundary always resolves the same way rather than by array order.
                if (gap < bestGap) { bestGap = gap; best = i; }
            }
            return best;
        }
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

        /// <summary>
        /// <b>The drawn texel a published anchor belongs to.</b> The texel at cell pixel
        /// (<paramref name="x"/>, <paramref name="y"/>) when it is drawn, else the nearest drawn texel within
        /// <paramref name="radius"/> (Euclidean; a tie goes to the one farther from the pivot, the lip rather
        /// than the cheek). Cell coords are top-left origin, y down. False when nothing within the radius is
        /// drawn (the caller keeps the published point and says so).
        ///
        /// <para>Why it exists: the rig publishes its mouth anchors from the skeleton, before the outline and
        /// the pixel pass, so on most frames they land 1 to 6 px off the fish that was actually drawn, and a
        /// line pinned there ends in open water. The importer snaps every frame's anchor onto that frame's
        /// own art. Pure, so the rule is pinned without a texture.</para>
        /// </summary>
        public static bool TrySnapToDrawn(Func<int, int, bool> drawn, int cellW, int cellH, int x, int y,
                                          float pivotX, float pivotY, int radius, out int snappedX, out int snappedY)
        {
            snappedX = x;
            snappedY = y;
            if (drawn == null || cellW <= 0 || cellH <= 0) return false;
            int best = int.MaxValue;
            float bestFar = float.MinValue;
            bool found = false;
            for (int yy = Mathf.Max(0, y - radius); yy <= Mathf.Min(cellH - 1, y + radius); yy++)
            for (int xx = Mathf.Max(0, x - radius); xx <= Mathf.Min(cellW - 1, x + radius); xx++)
            {
                int d2 = (xx - x) * (xx - x) + (yy - y) * (yy - y);
                if (d2 > radius * radius || d2 > best || !drawn(xx, yy)) continue;
                float fx = xx + 0.5f - pivotX, fy = yy + 0.5f - pivotY;
                float far = fx * fx + fy * fy;
                if (d2 < best || far > bestFar)
                {
                    best = d2;
                    bestFar = far;
                    snappedX = xx;
                    snappedY = yy;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>The CENTRE of cell texel (<paramref name="x"/>, <paramref name="y"/>) as a world-metre offset
        /// from a pivot at cell pixel (<paramref name="pivotX"/>, <paramref name="pivotY"/>), y up.</summary>
        public static Vector2 TexelCentreToPivotWorld(int x, int y, float pivotX, float pivotY, float ppu)
            => CellPxToPivotWorld(x + 0.5f, y + 0.5f, pivotX, pivotY, ppu);
    }
}
