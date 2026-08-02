using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points for the pass-6 character sheets. Two clicks in all: one for the
    /// PLAYER (every state the rig declares) and one for the CAST (the nine other presets, at the
    /// gaits an M1 standee needs). Each bakes, force-reimports and slices; nothing further to run.
    ///
    /// <para>Fixed recipes, same philosophy as <see cref="RigBakeMenu"/>: which states to bake is a
    /// decision already made, and the geometry (cell, frame counts, pivot, which layer an anim
    /// drives, which carry stances ride it) comes from the rig at bake time — never restated here.
    /// The only tables below are the two RECIPES and the cast's display names.</para>
    /// </summary>
    public static class CharacterRigBakeMenu
    {
        public const string CharacterArtFolder = "Assets/_Project/Art/Characters/Iso";

        /// <summary>The player's sheets sit at the root of that folder; the cast get a subfolder each.</summary>
        public const string SheetPrefix = "Fisher_";

        public const string AnchorFileName = "FisherFightAnchors.json";

        /// <summary>
        /// The player is the cast's <c>fisher</c> preset, named explicitly rather than left to the
        /// rig's DEFAULT_BUILD. The two differ (the preset wears stubble), and naming it means the
        /// player travels the same one code path every NPC does — which is what the creator slice
        /// will need.
        /// </summary>
        public const string PlayerPreset = "fisher";

        /// <summary>
        /// <b>Every state the pass-6 rig declares</b>, plus the two <c>cast</c> power variants that
        /// bake to their own sheets. Frame counts and playback ms are READ from the rig's ANIMS table;
        /// this list is only WHICH states, in a stable order.
        ///
        /// <para><b>The CARRY sheets are not listed here</b> — the bake grows them from the rig's own
        /// CARRIES table (<c>withCarryStances: true</c>), because which stance rides which anim is the
        /// rig's statement about its art. Ten sheets today: pails and tray on idle/walk/run, helm and
        /// oars on idle/walk. They are separate sheets rather than pins on the free ones because
        /// <b>a stance changes the POSE</b>, not only the hands.</para>
        ///
        /// <para>Grown from the eight of the v2 wave-1 spec: locomotion (idle/walk/run) and
        /// <c>dig</c> join the fight/balance set, and <c>hold</c>/<c>cast</c> are re-baked here rather
        /// than left behind at pass 1 — at a new cell size, a sheet left un-rebaked is a sheet at the
        /// wrong cell.</para>
        /// </summary>
        public static readonly CharacterState[] PlayerStates =
        {
            // base
            new CharacterState("idle"), new CharacterState("walk"), new CharacterState("run"),
            // deck
            new CharacterState("balance"), new CharacterState("stagger"),
            // rod
            new CharacterState("hold"),
            new CharacterState("cast", "short"), new CharacterState("cast", "long"),
            new CharacterState("castBack"), new CharacterState("castRelease"),
            new CharacterState("bite"), new CharacterState("strike"),
            new CharacterState("reel"), new CharacterState("land"),
            // shovel
            new CharacterState("dig"),
        };

        /// <summary>
        /// What a cast standee needs for M1: <c>idle</c> is what the world builders render, and
        /// <c>walk</c> bakes with it so the day a routine moves someone there is no second art pass.
        /// <c>run</c> and the rod states are deliberately NOT baked for the cast — nine presets ×
        /// fifteen states is tens of MiB of texture for animations nobody plays (CLAUDE.md rule 7;
        /// the budget lands in the log either way).
        /// </summary>
        public static readonly CharacterState[] CastStates =
        {
            new CharacterState("idle"), new CharacterState("walk"),
        };

        /// <summary>
        /// The nine NPC presets and their sheet stems. <c>fisher</c> is absent ON PURPOSE — he is the
        /// player, baked at the folder root with the full state set by the menu above; baking him
        /// again under <c>Iso/fisher/</c> would duplicate art the game already has.
        ///
        /// <para>The KEYS are the rig's own (<c>C.CAST</c> order) and are validated against its BUILDS
        /// table before a file lands. The stems are display capitalisation only.</para>
        /// </summary>
        public static readonly (string preset, string stem)[] Cast =
        {
            ("ginny", "Ginny"), ("skipper", "Skipper"), ("nan", "Nan"),
            ("deckboss", "DeckBoss"), ("packer", "Packer"), ("cutter", "Cutter"),
            ("hand", "Hand"), ("boy", "Boy"), ("girl", "Girl"),
        };

        /// <summary>Where one preset's sheets land: <c>…/Iso/ginny/Ginny_idle.png</c>.</summary>
        public static string FolderFor(string preset) => $"{CharacterArtFolder}/{preset}";

        /// <summary>The combined sidecar beside a preset's sheets.</summary>
        public static string AnchorFileFor(string stem) => $"{stem}Anchors.json";

        // ---- the player ------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Bake Character Sheets — the player (every state × 8 dir)",
                  priority = 42)]
        public static void BakePlayerSheets()
        {
            var results = new List<CharacterBakeResult>();
            try
            {
                results.Add(CharacterRigBaker.Bake(
                    "character", PlayerStates, CharacterArtFolder, SheetPrefix, AnchorFileName,
                    buildPreset: PlayerPreset, withCarryStances: true,
                    progress: (label, t) =>
                        EditorUtility.DisplayProgressBar("Baking the player's sheets", label, t)));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] player character sheets FAILED: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            FinishBake(results, "the player");
        }

        // ---- the cast --------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Bake Character Sheets — the cast (9 presets × idle + walk)",
                  priority = 43)]
        public static void BakeCastSheets()
        {
            var results = new List<CharacterBakeResult>();
            try
            {
                for (int i = 0; i < Cast.Length; i++)
                {
                    var (preset, stem) = Cast[i];
                    float t0 = (float)i / Cast.Length;
                    results.Add(CharacterRigBaker.Bake(
                        "character", CastStates, FolderFor(preset), $"{stem}_",
                        AnchorFileFor(stem), buildPreset: preset,
                        progress: (label, t) => EditorUtility.DisplayProgressBar(
                            "Baking the cast", $"{preset} · {label}", t0 + t / Cast.Length)));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] cast sheets FAILED: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            FinishBake(results, $"the cast ({Cast.Length} presets)");
        }

        // ---- shared tail -----------------------------------------------------------------------

        /// <summary>
        /// Reload, slice, and say what must be committed. Sheets are written by
        /// <c>File.WriteAllBytes</c> behind the AssetDatabase's back, so they are force-reimported
        /// before anything wires a serialized reference to them: a mid-build import invalidates
        /// in-memory refs.
        /// </summary>
        static void FinishBake(List<CharacterBakeResult> results, string what)
        {
            AssetDatabase.Refresh();

            foreach (var r in results)
            foreach (var sheet in r.Sheets)
                AssetDatabase.ImportAsset(sheet.AssetPath, ImportAssetOptions.ForceUpdate);

            // Slice in the same operation rather than leaving it as a step the owner has to
            // remember. ArtImportPipeline stamps the pixel-art import lock (PPU 32, Point,
            // Uncompressed) on first import; CharacterSheetSlicer adds the Multiple-mode grid and
            // the ground-contact pivot, recursively through the cast's subfolders. Every sheet is
            // ≤ 768 × 736 — no cap lift needed (and CharacterRigBaker refuses anything over the
            // 2048 default cap at bake time).
            Art.Editor.CharacterSheetSlicer.SliceAllMenu();

            foreach (var r in results) Debug.Log(Summarise(r));
            Debug.Log($"[rig-baker] Baked and sliced {what}. {Budget(results)}\n" +
                      "Nothing further to run — but the results must be COMMITTED: every .png AND " +
                      "its .meta (LFS covers *.png), plus each *Anchors.json and its .meta, under " +
                      $"{CharacterArtFolder}/.");
        }

        /// <summary>What a bake costs on disk and in texture memory — the number a sheet-budget
        /// decision needs, and which the PNG size alone does not give.</summary>
        public static string Budget(IReadOnlyList<CharacterBakeResult> results)
        {
            long png = 0, rgba = 0;
            int sheets = 0;
            foreach (var r in results)
            {
                png += r.TotalPngBytes;
                rgba += r.TotalRgba32Bytes;
                sheets += r.Sheets.Count;
            }
            return $"{sheets} sheets · {png / (1024.0 * 1024.0):F2} MiB PNG · " +
                   $"{rgba / (1024.0 * 1024.0):F1} MiB RGBA32 once imported.";
        }

        public static string Summarise(CharacterBakeResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[rig-baker] character sheets — build '{r.BuildPreset ?? "default"}'");
            sb.AppendLine($"  engine      : {r.EngineName}");
            sb.AppendLine($"  rig         : {r.RigKey}  {r.Geometry}");
            sb.AppendLine($"  convention  : MEASURED {r.MeasuredConvention} -> emitted unchanged " +
                          "(FacingsAreCounterClockwise = false)");
            sb.AppendLine($"  cells       : {r.CellsRendered} across {r.Sheets.Count} state sheets");
            foreach (var s in r.Sheets) sb.AppendLine($"  sheet       : {s}");
            sb.AppendLine($"  anchors     : {r.AnchorJsonPath}");
            sb.AppendLine($"  render time : {r.RenderMilliseconds:F0} ms " +
                          $"({r.RenderMilliseconds / Mathf.Max(1, r.CellsRendered):F2} ms/cell)");
            sb.AppendLine($"  total time  : {r.TotalMilliseconds / 1000.0:F2} s");
            sb.Append    ($"  on disk     : {r.TotalPngBytes / 1024.0:F0} KB PNG · " +
                          $"{r.TotalRgba32Bytes / (1024.0 * 1024.0):F1} MiB RGBA32");
            return sb.ToString();
        }

        /// <summary>
        /// Headless entry point for CI / -executeMethod: player then cast, in one editor session.
        ///
        /// ⚠️ Never invoke this with -quit alongside -runTests. The two race: Unity exits 0 having
        /// written total=0, which reads as a pass. That trap is recorded in ADR 0021 and it would
        /// quietly poison any job written that way.
        /// </summary>
        public static void BakeAllCharacterSheetsFromCommandLine()
        {
            try
            {
                BakePlayerSheets();
                BakeCastSheets();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] headless character bake failed: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
