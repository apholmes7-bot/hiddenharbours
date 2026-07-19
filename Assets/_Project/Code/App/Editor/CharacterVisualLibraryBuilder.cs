#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The one place that knows where the 8-direction iso CHARACTER sheets live on disk</b> — the
    /// character-side twin of <see cref="BoatVisualLibraryBuilder"/>. It imports the sliced sheets into a
    /// <see cref="CharacterVisualDef"/> asset, after which everything downstream (the start builder, the
    /// presenter, the tests) reads DATA and never a path (rule 2).
    ///
    /// <para><b>The generated asset is committed</b> — the owner does NOT need to run this after every
    /// pull. Run it only when the art changes: a RE-SLICE changes the sprite sub-asset ids and the def's
    /// refs go stale (the sheets turn to None), which is what re-running repairs. It is non-destructive —
    /// it refreshes an existing asset in place, keeping its guid — so nothing pointing at the def breaks.</para>
    ///
    /// <para><b>Adding a character:</b> add a <see cref="Kit"/> entry below. Nothing else in the codebase
    /// needs to learn the new character exists. (Only the PLAYER's fisher is wired for now — Ginny and the
    /// skipper are deliberately left un-skinned; re-skinning the NPCs is its own change.)</para>
    /// </summary>
    public static class CharacterVisualLibraryBuilder
    {
        const string MenuPath = "Hidden Harbours/Art/Build Character Visual Defs";
        const string VisualsFolder = "Assets/_Project/Data/Characters";
        const string ArtIso = "Assets/_Project/Art/Characters/Iso";

        // ✅ THE CHARACTER SHEETS ARE NOW BAKED CLOCKWISE — the rig itself was fixed, so this is false.
        //
        // They USED to be counter-clockwise: the same defect, from the same rig recipe, that mirrored every
        // iso BOAT kit (PR #212). The rig rotated the model CCW and then labelled the rows clockwise, so row
        // i actually DEPICTED heading −45°·i — the row called 'E' was a fisher facing WEST. Drawn-minus-true
        // = −2·heading: 0° at N/S (which is why it hid), 90° at the diagonals, a full 180° at E/W. This
        // constant was the un-mirror.
        //
        // The art director has since corrected the RIG (th = −dir·45°) and re-baked all twelve body sheets,
        // so row i now depicts +45°·i exactly as labelled and no un-mirror is wanted. Applying both the
        // corrected bake AND the un-mirror would cancel into a fresh 180° error at E/W — which is precisely
        // why the flag flip and the new art must land in ONE commit.
        //
        // Verified against the ART, not the rig's labels: face-skin centroids measured per row of the
        // re-baked Fisher_idle.png put rows 1–3 on the screen RIGHT and rows 5–7 on the screen LEFT (the
        // exact row-order reversal of the old art). Rows 0/4 (N/S) are their own mirrors and cannot
        // discriminate. CharacterIsoFacingTests re-measures exactly that at test time, so this constant is
        // checked against the pixels rather than believed — it went RED the moment the new art landed while
        // this still said true.
        //
        // ⚠️ THE BOAT RIGS WERE NOT FIXED. BoatVisualLibraryBuilder.IsoSheetsAreCounterClockwise STAYS true.
        // This flag is per-artwork DATA precisely so the two art lineages can disagree; that design is now
        // load-bearing. Do not "unify" them.
        const bool IsoCharacterSheetsAreCounterClockwise = false;   // an art fact, not a feel knob

        // The bake's shape, stated once. Frame counts are per sheet (below) because they genuinely differ.
        const int Directions = 8;

        // Gait thresholds, in m/s. The walk threshold is a NOISE dead-band — a collider nudge or a wave
        // shove on deck must not twitch the idle into a step. The run threshold sits deliberately ABOVE the
        // on-foot walk speed (PlayerWalkController._moveSpeed, 3 m/s today) so the ordinary walk never trips
        // it: there is NO run/sprint input on the controller yet, so the run sheet is wired but dormant.
        // That is on purpose — inventing a sprint input is a gameplay change, and this is a visual swap. The
        // day a sprint speed lands, the run cycle plays with no code edit, and until then the owner can try
        // it by dropping this number in the asset.
        const float WalkThreshold = 0.35f;
        const float RunThreshold = 4.5f;

        // Playback rates (fps) — how fast each cycle reads, per sheet.
        const float IdleFps = 6f;
        const float WalkFps = 10f;
        const float RunFps = 12f;

        /// <summary>One character's worth of sheets on disk → one <see cref="CharacterVisualDef"/> asset.</summary>
        struct Kit
        {
            public string AssetName;    // file written under VisualsFolder
            public string Id;           // stable def id (append-only)
            public string Stem;         // "Fisher" → Fisher_idle.png / Fisher_walk.png / Fisher_run.png
            public int IdleFrames;
            public int WalkFrames;
            public int RunFrames;
        }

        static readonly Kit[] Kits =
        {
            // THE PLAYER'S FISHER. 8 direction rows per sheet; idle 6 frames, walk 8, run 6 — the counts the
            // art actually ships (384/512/384 px wide at a 64 px cell). Cell 64×88 at PPU 32, pivot on
            // GROUND CONTACT, so the frames swap without the feet moving.
            new Kit
            {
                AssetName = "FisherIso", Id = "visual.fisher_iso", Stem = "Fisher",
                IdleFrames = 6, WalkFrames = 8, RunFrames = 6,
            },

            // Ginny and the Skipper ship the same kit shape and are IMPORTED, but nothing wears them yet —
            // re-skinning the NPCs is its own change, with its own playtest. Adding them here would write
            // assets no one reads; they are named in this comment so the next hand knows where they go.
        };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            EnsureFolder(VisualsFolder);
            int ok = 0;
            foreach (var kit in Kits)
                if (BuildOne(kit)) ok++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CharacterVisualLibraryBuilder] Refreshed {ok}/{Kits.Length} character visual def(s) " +
                      $"in {VisualsFolder}. Commit them; re-run only when the sheets are re-sliced.");
        }

        static bool BuildOne(Kit kit)
        {
            string path = $"{VisualsFolder}/{kit.AssetName}.asset";
            var def = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(path);
            bool created = def == null;
            if (created) def = ScriptableObject.CreateInstance<CharacterVisualDef>();

            def.Id = kit.Id;
            def.FacingCount = Directions;
            def.ZeroHeadingDegrees = 0f;   // row 0 is the North-facing picture
            // Which way this artwork's rows actually run. Stated per kit, never assumed (see the note above).
            def.FacingsAreCounterClockwise = IsoCharacterSheetsAreCounterClockwise;

            def.IdleFrameCount = kit.IdleFrames;
            def.WalkFrameCount = kit.WalkFrames;
            def.RunFrameCount = kit.RunFrames;
            def.IdleFramesPerSecond = IdleFps;
            def.WalkFramesPerSecond = WalkFps;
            def.RunFramesPerSecond = RunFps;
            def.WalkSpeedThreshold = WalkThreshold;
            def.RunSpeedThreshold = RunThreshold;

            // All-or-nothing per sheet, mirroring CharacterVisualDef's own gate: a short sheet is dropped
            // whole rather than half-bound, because one missing slice would index a stale cell mid-stride.
            def.IdleSheet = TakeExactly($"{ArtIso}/{kit.Stem}_idle.png", Directions * kit.IdleFrames);
            def.WalkSheet = TakeExactly($"{ArtIso}/{kit.Stem}_walk.png", Directions * kit.WalkFrames);
            def.RunSheet = TakeExactly($"{ArtIso}/{kit.Stem}_run.png", Directions * kit.RunFrames);

            if (created) AssetDatabase.CreateAsset(def, path);
            else EditorUtility.SetDirty(def);

            if (!def.HasAnyArt())
            {
                Debug.LogWarning($"[CharacterVisualLibraryBuilder] {kit.AssetName}: " +
                                 $"'{ArtIso}/{kit.Stem}_idle.png' gave {def.IdleSheet.Length}/" +
                                 $"{Directions * kit.IdleFrames} ordered slices — the skin is EMPTY, so the " +
                                 "character keeps whatever sprite drew it before. Slice the sheet (Hidden " +
                                 "Harbours ▸ Art ▸ Slice Iso Character Sheets) and re-run.");
                return false;
            }

            Debug.Log($"[CharacterVisualLibraryBuilder] {kit.AssetName}: {def.FacingCount} directions, idle " +
                      $"{(def.HasGait(CharacterGait.Idle) ? "WIRED" : "none")}, walk " +
                      $"{(def.HasGait(CharacterGait.Walk) ? "WIRED" : "none")}, run " +
                      $"{(def.HasGait(CharacterGait.Run) ? "WIRED" : "none")}" +
                      $"{(def.FacingsAreCounterClockwise ? ", rows UN-MIRRORED (art bakes CCW)" : ", rows as labelled (art bakes CW)")}.");
            return true;
        }

        /// <summary>
        /// A sheet's sprites in <c>direction·frames + frame</c> order, but ONLY if it gives exactly the
        /// expected count — otherwise an empty set (the all-or-nothing gate).
        ///
        /// <para>⚠️ Sliced sheets import as spriteMode Multiple, so <c>LoadAssetAtPath&lt;Sprite&gt;</c>
        /// returns NULL and the sub-assets must be loaded with <c>LoadAllAssetsAtPath</c>. Unlike the boat
        /// sheets (named <c>&lt;Stem&gt;_&lt;index&gt;</c>), these are named
        /// <c>&lt;Stem&gt;_d&lt;dir&gt;_f&lt;frame&gt;</c> — so the order is recovered from the d/f pair,
        /// NOT from a trailing number, which would sort every direction's frame 0 together.</para>
        /// </summary>
        static Sprite[] TakeExactly(string path, int expected)
        {
            if (string.IsNullOrEmpty(path) || expected <= 0) return System.Array.Empty<Sprite>();

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            if (sprites.Length != expected) return System.Array.Empty<Sprite>();

            var ordered = new Sprite[expected];
            int framesPerDirection = expected / Directions;
            var seen = new HashSet<int>();
            foreach (var s in sprites)
            {
                if (s == null || !TryParseCell(s.name, out int dir, out int frame))
                    return System.Array.Empty<Sprite>();
                if (dir < 0 || dir >= Directions || frame < 0 || frame >= framesPerDirection)
                    return System.Array.Empty<Sprite>();

                int idx = dir * framesPerDirection + frame;
                if (!seen.Add(idx)) return System.Array.Empty<Sprite>();   // a duplicate cell = a bad slice
                ordered[idx] = s;
            }
            return seen.Count == expected ? ordered : System.Array.Empty<Sprite>();
        }

        /// <summary>Pull the direction + frame out of a <c>..._d3_f5</c> sub-sprite name. False on anything
        /// that doesn't match, which drops the whole sheet rather than guessing an order.</summary>
        static bool TryParseCell(string spriteName, out int dir, out int frame)
        {
            dir = frame = -1;
            if (string.IsNullOrEmpty(spriteName)) return false;

            int f = spriteName.LastIndexOf("_f", System.StringComparison.Ordinal);
            if (f < 0) return false;
            int d = spriteName.LastIndexOf("_d", f, System.StringComparison.Ordinal);
            if (d < 0) return false;

            return int.TryParse(spriteName.Substring(d + 2, f - d - 2), out dir)
                && int.TryParse(spriteName.Substring(f + 2), out frame);
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
#endif
