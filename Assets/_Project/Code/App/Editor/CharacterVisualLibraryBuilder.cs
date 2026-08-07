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
        const string MenuPath = "Hidden Harbours/Art/Import (after a new drop)/Build Character Visual Defs";
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

        // ---- the STANCE sheets (the body doing something besides travelling) -----------------------
        // Frame counts and rates come from the rig's OWN ANIMS table (characterIsoRig6.js) rather than
        // from a README — ADR 0021 §4, the same rule CharacterRigBaker follows when it reads
        // ANIMS[anim].frames/.ms to decide what to bake. The three constants below ARE that table:
        //     idle:{frames:6, ms:170}   walk:{frames:8, ms:110}   balance:{frames:8, ms:150}
        // The carry stances ride the idle and walk anims (the rig's CARRIES declares
        // anims:['idle','walk'] for both helm and oars), so they inherit those counts exactly.
        //
        // ⚠️ These do NOT match the base sheets' 6 / 10 fps above, and must not be "unified" with them.
        // Those two are a deliberate owner-facing rounding that predates this; these are the rig's
        // declared timing for clips the owner has never tuned. Rounding a 150 ms clip to the 170 ms
        // one's rate would slow the deck brace by 13% for tidiness alone.
        const int BalanceFrames = 8;
        const float BalanceFps = 1000f / 150f;    // 6.67
        const int CarryIdleFrames = 6;
        const float CarryIdleFps = 1000f / 170f;  // 5.88
        const int CarryWalkFrames = 8;
        const float CarryWalkFps = 1000f / 110f;  // 9.09

        // ---- the CLIP sheets (whole-body actions played as events) ---------------------------------
        // Same rule as the stances: the counts and rates ARE the rig's own ANIMS table, rev 6.2 —
        //     board:{frames:10, ms:90, oneShot}   boardDown:{frames:6, ms:95, oneShot}
        //     haul:{frames:8, ms:120}             ladderDown:{frames:10, ms:110}
        // and `oneShot` there is Loops=false here. The two boarding clips play once and hold their last
        // frame (a board that wrapped would drop the fisher back on the wharf at the top of her own
        // vault); haul and ladderDown loop, because a heave and a climb are cycles that run until the
        // thing driving them stops.
        //
        // FACING COUNT is left at the def's eight for all four — the pass-6.2 kit bakes every clip at
        // the full eight rows (verified against the rendered pixels, not the labels: all eight facings
        // come back non-empty for all four clips). A later kit that ships a reduced-facing ladder climb
        // sets CharacterClipSheets.FacingCount and the row snap re-solves with no code change here.
        const int BoardFrames = 10;
        const float BoardFps = 1000f / 90f;        // 11.11
        const int BoardDownFrames = 6;
        const float BoardDownFps = 1000f / 95f;    // 10.53
        const int HaulFrames = 8;
        const float HaulFps = 1000f / 120f;        // 8.33
        const int LadderDownFrames = 10;
        const float LadderDownFps = 1000f / 110f;  // 9.09

        /// <summary>One character's worth of sheets on disk → one <see cref="CharacterVisualDef"/> asset.</summary>
        struct Kit
        {
            public string AssetName;    // file written under VisualsFolder
            public string Id;           // stable def id (append-only)
            public string Stem;         // "Fisher" → Fisher_idle.png / Fisher_walk.png / Fisher_run.png
            public string Folder;       // null = the Iso root (the player); the cast get one each
            public int IdleFrames;
            public int WalkFrames;
            public int RunFrames;       // 0 = this build has no run sheet, and that is legal
        }

        /// <summary>
        /// The cast presets and their sheet stems — the nine baked by
        /// <c>Art ▸ Bake Character Sheets — the cast</c>, one subfolder each. Mirrors
        /// <c>CharacterRigBakeMenu.Cast</c>; restated here rather than referenced because the two
        /// assemblies do not meet, and a mismatch surfaces immediately as an EMPTY skin in the log.
        /// </summary>
        static readonly (string preset, string stem)[] Cast =
        {
            ("ginny", "Ginny"), ("skipper", "Skipper"), ("nan", "Nan"),
            ("deckboss", "DeckBoss"), ("packer", "Packer"), ("cutter", "Cutter"),
            ("hand", "Hand"), ("boy", "Boy"), ("girl", "Girl"),
        };

        /// <summary>The def asset name for a cast preset: <c>GinnyIso</c>.</summary>
        public static string CastAssetName(string stem) => $"{stem}Iso";

        /// <summary>The def id for a cast preset: <c>visual.ginny_iso</c>. Append-only.</summary>
        public static string CastVisualId(string preset) => $"visual.{preset}_iso";

        static readonly Kit[] Kits =
        {
            // THE PLAYER'S FISHER. 8 direction rows per sheet; idle 6 frames, walk 8, run 6 — the counts the
            // art actually ships (384/512/384 px wide at a 64 px cell). Cell 64×92 at PPU 32 as of the
            // pass-6 kit (it was 64×88), pivot on GROUND CONTACT — which is why the cell growing two rows
            // moved nothing the player can see: the feet stay planted and only the empty headroom changed.
            new Kit
            {
                AssetName = "FisherIso", Id = "visual.fisher_iso", Stem = "Fisher", Folder = null,
                IdleFrames = 6, WalkFrames = 8, RunFrames = 6,
            },

            // THE CAST is appended below, one def per baked preset — the comment that used to sit here
            // saying "nothing wears them yet" is retired: the region builders wear them now.
        };

        /// <summary>
        /// The player's kit plus one per cast preset. The cast bake idle + walk only (an M1 standee
        /// idles; a routine that walks is M2), so their run count is <b>0</b> — which is not a gap to
        /// paper over: <c>CharacterVisualDef.PlayableGait</c> ladders run → walk → idle, so a preset
        /// with no run sheet simply never shows one.
        /// </summary>
        static IEnumerable<Kit> AllKits()
        {
            foreach (var kit in Kits) yield return kit;

            foreach (var (preset, stem) in Cast)
                yield return new Kit
                {
                    AssetName = CastAssetName(stem),
                    Id = CastVisualId(preset),
                    Stem = stem,
                    Folder = preset,
                    IdleFrames = 6, WalkFrames = 8, RunFrames = 0,
                };
        }

        [MenuItem(MenuPath, priority = 231)]
        public static void Build()
        {
            EnsureFolder(VisualsFolder);
            int ok = 0, total = 0;
            foreach (var kit in AllKits())
            {
                total++;
                if (BuildOne(kit)) ok++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CharacterVisualLibraryBuilder] Refreshed {ok}/{total} character visual def(s) " +
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
            string folder = string.IsNullOrEmpty(kit.Folder) ? ArtIso : $"{ArtIso}/{kit.Folder}";
            def.IdleSheet = TakeExactly($"{folder}/{kit.Stem}_idle.png", Directions * kit.IdleFrames);
            def.WalkSheet = TakeExactly($"{folder}/{kit.Stem}_walk.png", Directions * kit.WalkFrames);
            def.RunSheet = TakeExactly($"{folder}/{kit.Stem}_run.png", Directions * kit.RunFrames);

            // The STANCES are attempted for EVERY kit and simply come back empty where the art doesn't
            // exist — which is the whole cast today (they bake idle + walk only). No flag says who has
            // them: an absent file yields an empty set, the def's all-or-nothing gate drops it whole, and
            // the presenter falls back to the free body. So the day a cast preset gains a deck bake, it
            // wires itself with no edit here.
            def.BalanceStance = TakeStance(folder, kit.Stem,
                                           idleSuffix: "_balance", idleFrames: BalanceFrames, idleFps: BalanceFps,
                                           walkSuffix: null, walkFrames: 0, walkFps: 0f);
            def.HelmStance = TakeStance(folder, kit.Stem,
                                        idleSuffix: "_idle_helm", idleFrames: CarryIdleFrames, idleFps: CarryIdleFps,
                                        walkSuffix: "_walk_helm", walkFrames: CarryWalkFrames, walkFps: CarryWalkFps);
            def.OarsStance = TakeStance(folder, kit.Stem,
                                        idleSuffix: "_idle_oars", idleFrames: CarryIdleFrames, idleFps: CarryIdleFps,
                                        walkSuffix: "_walk_oars", walkFrames: CarryWalkFrames, walkFps: CarryWalkFps);

            // The CLIPS, attempted for every kit on exactly the same terms as the stances: an absent
            // sheet yields an empty block, the def's all-or-nothing gate drops it whole, and the clip
            // simply never plays. So the cast — which bakes idle + walk only — wires no clips today and
            // wires them the day their bake grows, with no edit here.
            def.BoardClip = TakeClip(folder, kit.Stem, "_board", BoardFrames, BoardFps, loops: false);
            def.BoardDownClip = TakeClip(folder, kit.Stem, "_boardDown", BoardDownFrames, BoardDownFps,
                                         loops: false);
            def.HaulClip = TakeClip(folder, kit.Stem, "_haul", HaulFrames, HaulFps, loops: true);
            def.LadderDownClip = TakeClip(folder, kit.Stem, "_ladderDown", LadderDownFrames,
                                          LadderDownFps, loops: true);

            if (created) AssetDatabase.CreateAsset(def, path);
            else EditorUtility.SetDirty(def);

            if (!def.HasAnyArt())
            {
                Debug.LogWarning($"[CharacterVisualLibraryBuilder] {kit.AssetName}: " +
                                 $"'{folder}/{kit.Stem}_idle.png' gave {def.IdleSheet.Length}/" +
                                 $"{Directions * kit.IdleFrames} ordered slices — the skin is EMPTY, so the " +
                                 "character keeps whatever sprite drew it before. Slice the sheet (Hidden " +
                                 "Harbours ▸ Art ▸ Import (after a new drop) ▸ Slice Iso Character " +
                                 "Sheets) and re-run.");
                return false;
            }

            Debug.Log($"[CharacterVisualLibraryBuilder] {kit.AssetName}: {def.FacingCount} directions, idle " +
                      $"{(def.HasGait(CharacterGait.Idle) ? "WIRED" : "none")}, walk " +
                      $"{(def.HasGait(CharacterGait.Walk) ? "WIRED" : "none")}, run " +
                      $"{(def.HasGait(CharacterGait.Run) ? "WIRED" : "none")}" +
                      $"; stances balance {StanceState(def, CharacterStance.Balance)}, " +
                      $"helm {StanceState(def, CharacterStance.Helm)}, " +
                      $"oars {StanceState(def, CharacterStance.Oars)}" +
                      $"; clips board {ClipState(def, CharacterClip.Board)}, " +
                      $"boardDown {ClipState(def, CharacterClip.BoardDown)}, " +
                      $"haul {ClipState(def, CharacterClip.Haul)}, " +
                      $"ladderDown {ClipState(def, CharacterClip.LadderDown)}" +
                      $"{(def.FacingsAreCounterClockwise ? ", rows UN-MIRRORED (art bakes CCW)" : ", rows as labelled (art bakes CW)")}.");
            return true;
        }

        /// <summary>One stance's sheets, or an EMPTY (never null) block where the art isn't baked. Each
        /// sheet goes through the same all-or-nothing <see cref="TakeExactly"/> gate as the free body, so a
        /// half-sliced stance is dropped whole rather than half-bound. A null suffix means the stance has
        /// no such clip at all (the deck brace bakes no walk — you cross a deck at an ordinary walk).</summary>
        static CharacterStanceSheets TakeStance(string folder, string stem,
                                                string idleSuffix, int idleFrames, float idleFps,
                                                string walkSuffix, int walkFrames, float walkFps)
        {
            var s = new CharacterStanceSheets();
            if (!string.IsNullOrEmpty(idleSuffix) && idleFrames > 0)
            {
                s.IdleSheet = TakeExactly($"{folder}/{stem}{idleSuffix}.png", Directions * idleFrames);
                s.IdleFrameCount = idleFrames;
                s.IdleFramesPerSecond = idleFps;
            }
            if (!string.IsNullOrEmpty(walkSuffix) && walkFrames > 0)
            {
                s.WalkSheet = TakeExactly($"{folder}/{stem}{walkSuffix}.png", Directions * walkFrames);
                s.WalkFrameCount = walkFrames;
                s.WalkFramesPerSecond = walkFps;
            }
            return s;
        }

        /// <summary>One clip's sheet, or an EMPTY (never null) block where the art isn't baked — the same
        /// all-or-nothing <see cref="TakeExactly"/> gate the free body and the stances go through, for the
        /// same reason. <paramref name="loops"/> is the rig's own <c>oneShot</c> flag, inverted.</summary>
        static CharacterClipSheets TakeClip(string folder, string stem, string suffix,
                                            int frames, float fps, bool loops)
        {
            var c = new CharacterClipSheets
            {
                FrameCount = Mathf.Max(1, frames),
                FramesPerSecond = fps,
                Loops = loops,
                // 0 = inherit the def's FacingCount. The pass-6.2 kit bakes all eight rows for every
                // clip; this is the seam a reduced-facing kit would use instead.
                FacingCount = 0,
            };
            if (frames > 0)
                c.Sheet = TakeExactly($"{folder}/{stem}{suffix}.png", Directions * frames);
            return c;
        }

        /// <summary>A clip's wiring state for the build log.</summary>
        static string ClipState(CharacterVisualDef def, CharacterClip clip) =>
            def.HasClip(clip) ? "WIRED" : "none";

        /// <summary>A stance's wiring state for the build log — which gaits of it actually came through.</summary>
        static string StanceState(CharacterVisualDef def, CharacterStance stance)
        {
            bool idle = def.HasStanceGait(stance, CharacterGait.Idle);
            bool walk = def.HasStanceGait(stance, CharacterGait.Walk);
            if (idle && walk) return "idle+walk";
            if (idle) return "idle";
            if (walk) return "walk";
            return "none";
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
