#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;          // RodKitAnchorMath — the kit's one px→metre conversion
using HiddenHarbours.Art.Editor;      // MiniJson — the shared editor JSON reader

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The character rig's HAND-PROP table, imported once into an asset</b> — the consumer side of the
    /// rev-6.4 <c>handProps</c> block that <c>CharacterRigBaker</c> writes into
    /// <c>FisherFightAnchors.json</c>.
    ///
    /// <para><b>Why edit time, and why a committed asset.</b> Both halves of the source are committed and
    /// change only when the character is re-baked: the sidecar's numbers, and the sheets' import PPU. So
    /// there is nothing to decide at run time and nothing to parse there either — the same reason
    /// <see cref="CharacterVisualLibraryBuilder"/> writes a committed <c>CharacterVisualDef</c> rather
    /// than reading PNGs at boot, and the same reason <see cref="RodKitImporter"/> converts every pin to
    /// metres in the builder. Runtime never sees JSON (ADR 0021 §4). Non-destructive: it refreshes the
    /// asset in place, keeping its guid, so nothing pointing at it breaks.</para>
    ///
    /// <para><b>What is converted here, once.</b> Cell pixels → world metres, through the sheets' OWN
    /// import PPU and the sidecar's own declared pivot — never a typed-in 32. Absolute points (the wrists
    /// and the rest pins) go through <see cref="RodKitAnchorMath.CellPxToPivotWorld"/>; the grip is an
    /// OFFSET and goes through <see cref="RodKitAnchorMath.OffsetPxToWorld"/>, which is not the same
    /// function and would be a silent 2 m error if confused (one subtracts the pivot, the other does
    /// not).</para>
    ///
    /// <para><b>Everything degrades per element</b>, the house rule for importers: a missing sidecar
    /// leaves the asset untouched and says so; a prop whose rows are malformed is skipped with its own
    /// error and the rest still import; a gait whose state the rig never baked simply contributes no
    /// wrist grid, and the carrier falls back to that row's rest pin. Silent half-wiring is what cost the
    /// owner a playtest, so every skip is loud.</para>
    /// </summary>
    public static class CarryAnchorTableBuilder
    {
        const string MenuPath = "Hidden Harbours/Art/Import (after a new drop)/Build Carry Anchor Table";
        const string OutputFolder = "Assets/_Project/Data/Characters";
        const string AssetName = "FisherCarryAnchors";
        const string ArtIso = "Assets/_Project/Art/Characters/Iso";
        const string SidecarPath = ArtIso + "/FisherFightAnchors.json";

        /// <summary>Where the built asset lands — what the start builder loads.</summary>
        public const string TablePath = OutputFolder + "/" + AssetName + ".asset";

        /// <summary>The def id, append-only.</summary>
        public const string TableId = "carryanchors.fisher_iso";

        /// <summary>
        /// Which sidecar state each drawable GAIT's wrists come from.
        ///
        /// <para><b>Only the free body, deliberately.</b> These three are the states
        /// <see cref="IsoCharacterSprite"/> draws with the arms free; the rig's other thirty-four are
        /// stances and clips whose hands are already committed to something (a helm, a warp, a rail) or
        /// which are driven by another presenter entirely. A carried thing during one of those falls back
        /// to its rest pin, which is still the rig's own per-facing answer — just not one that tracks the
        /// swing. Adding a stance later is one line here plus the gait/stance key the runtime asks with;
        /// it is not a code change anywhere else.</para>
        /// </summary>
        static readonly (CharacterGait gait, string state)[] GaitStates =
        {
            (CharacterGait.Idle, "idle"),
            (CharacterGait.Walk, "walk"),
            (CharacterGait.Run, "run"),
        };

        const int Directions = 8;

        [MenuItem(MenuPath, priority = 232)]
        public static void Build()
        {
            if (BuildTable(out string summary)) Debug.Log($"[CarryAnchorTableBuilder] {summary}");
            else Debug.LogError($"[CarryAnchorTableBuilder] {summary}");
        }

        /// <summary>
        /// Import the sidecar into <see cref="TablePath"/>. False (with a reason) when the sidecar could
        /// not be read at all — the asset is then left exactly as it was, because a half-written table is
        /// worse than an old one.
        /// </summary>
        public static bool BuildTable(out string summary)
        {
            summary = null;

            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(SidecarPath);
            if (text == null)
            {
                summary = $"No anchor sidecar at '{SidecarPath}' — nothing imported, the existing table " +
                          "is untouched. Re-run the character bake first.";
                return false;
            }

            object root;
            try { root = MiniJson.Parse(text.text); }
            catch (System.FormatException e)
            {
                summary = $"'{SidecarPath}' failed to parse ({e.Message}) — nothing imported. Re-bake " +
                          "the character kit.";
                return false;
            }

            var handProps = MiniJson.Dict(root, "handProps");
            if (handProps == null)
            {
                // The absent-layer case is LEGAL — an older rig kit bakes no hand props — and it must not
                // clobber a good table with an empty one.
                summary = $"'{SidecarPath}' carries no 'handProps' block (an older rig kit, or the layer " +
                          "failed to install) — nothing imported, the existing table is untouched.";
                return false;
            }

            var pivot = MiniJson.Dict(root, "pivotTopLeft");
            if (pivot == null)
            {
                summary = $"'{SidecarPath}' has no 'pivotTopLeft' — every pin would be measured from the " +
                          "wrong origin, so nothing is imported.";
                return false;
            }
            float pivotX = MiniJson.Float(pivot, "x"), pivotY = MiniJson.Float(pivot, "y");
            float ppu = CharacterSheetPpu();

            var table = AssetDatabase.LoadAssetAtPath<CarryAnchorTableDef>(TablePath);
            bool created = table == null;
            if (created) table = ScriptableObject.CreateInstance<CarryAnchorTableDef>();

            table.Id = TableId;
            table.SourceSidecar = SidecarPath;
            table.Pass = MiniJson.Float(handProps, "pass");
            table.TorsoHalfMetres = MiniJson.Float(handProps, "torsoHalfMetres");
            table.FacingCount = MiniJson.Int(root, "dirs", Directions);

            var restPose = MiniJson.Dict(handProps, "restPose");
            table.RestAnim = restPose != null ? MiniJson.String(restPose, "anim") : null;
            table.RestFrame = restPose != null ? MiniJson.Int(restPose, "frame") : 0;

            table.Props = ReadProps(handProps, pivotX, pivotY, ppu, table.FacingCount, out int skipped);
            table.Wrists = ReadWrists(MiniJson.Dict(root, "states"), pivotX, pivotY, ppu,
                                      table.FacingCount, out int gaitsMissing);

            if (created) AssetDatabase.CreateAsset(table, TablePath);
            else EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            summary = $"{(created ? "Created" : "Refreshed")} '{TablePath}' from rig pass {table.Pass:0.0} " +
                      $"at {ppu:0} px/m: {table.Props.Length} prop(s) × {table.FacingCount} facings, " +
                      $"{table.Wrists.Length}/{GaitStates.Length} gait wrist grid(s)" +
                      (skipped > 0 ? $", {skipped} prop(s) SKIPPED (see the errors above)" : "") +
                      (gaitsMissing > 0 ? $", {gaitsMissing} gait(s) had no baked state (those fall back " +
                                          "to the rest pin)" : "") +
                      ". Commit it; re-run only after a character re-bake.";
            return true;
        }

        // ---- the prop rows --------------------------------------------------------------------------

        static CarryPropAnchors[] ReadProps(Dictionary<string, object> handProps,
                                            float pivotX, float pivotY, float ppu, int facings,
                                            out int skipped)
        {
            skipped = 0;
            var props = MiniJson.Dict(handProps, "props");
            if (props == null) return System.Array.Empty<CarryPropAnchors>();

            var built = new List<CarryPropAnchors>(props.Count);
            foreach (KeyValuePair<string, object> kv in props)
            {
                var node = kv.Value as Dictionary<string, object>;
                var rowsJson = MiniJson.List(node, "rows");
                if (rowsJson == null || rowsJson.Count != facings)
                {
                    Debug.LogError($"[CarryAnchorTableBuilder] Prop '{kv.Key}' has " +
                                   $"{(rowsJson == null ? "no" : rowsJson.Count.ToString())} facing rows, " +
                                   $"expected {facings} — SKIPPED, so anything carrying it keeps the " +
                                   "carrier's fallback offset. Re-bake the character kit.");
                    skipped++;
                    continue;
                }

                var rows = new CarryAnchorRow[facings];
                bool ok = true;
                for (int i = 0; i < rowsJson.Count && ok; i++)
                {
                    if (rowsJson[i] is not Dictionary<string, object> r) { ok = false; break; }

                    // ⚠️ The row is indexed by its OWN declared dir, not by its position in the array.
                    // They are the same in every baked sidecar, and checking rather than assuming is the
                    // whole lesson of the counter-clockwise rigs: a table silently rotated by one facing
                    // draws right at N and S and wrong everywhere else.
                    int dir = MiniJson.Int(r, "dir", -1);
                    if (dir != i)
                    {
                        Debug.LogError($"[CarryAnchorTableBuilder] Prop '{kv.Key}' row {i} declares " +
                                       $"dir {dir} — the rows are out of order, which would rotate the " +
                                       "whole prop's anchoring. SKIPPED.");
                        ok = false;
                        break;
                    }

                    // ⚠️ ABSENT is not FALSE here. 'behind' decides whether a held thing draws in front
                    // of the fisher or is hidden by her, and defaulting a missing flag to false would
                    // import a plausible table from a bake that had stopped writing it — wrong at exactly
                    // the five rows that are true, and invisible everywhere else.
                    if (!MiniJson.Has(r, "behind"))
                    {
                        Debug.LogError($"[CarryAnchorTableBuilder] Prop '{kv.Key}' row {i} has no " +
                                       "'behind' flag — the draw order would be guessed. SKIPPED; " +
                                       "re-bake the character kit.");
                        ok = false;
                        break;
                    }

                    rows[i] = new CarryAnchorRow
                    {
                        Hand = HandSide(MiniJson.String(r, "hand"), MiniJson.Int(r, "hands", 1),
                                        MiniJson.String(r, "mode")),
                        Mount = string.Equals(MiniJson.String(r, "mode"), "back",
                                              System.StringComparison.Ordinal)
                                ? CarryMountKind.Back : CarryMountKind.Hand,
                        // gripD is an OFFSET from the anchor — no pivot subtraction, y flipped.
                        GripOffsetMeters = RodKitAnchorMath.OffsetPxToWorld(
                            MiniJson.Float(r, "gripDx"), MiniJson.Float(r, "gripDy"), ppu),
                        // rest is an ABSOLUTE cell point — measured from the pivot.
                        RestOffsetMeters = RodKitAnchorMath.CellPxToPivotWorld(
                            MiniJson.Float(r, "restX"), MiniJson.Float(r, "restY"), pivotX, pivotY, ppu),
                        ItemFacing = MiniJson.Int(r, "itemDir"),
                        Behind = MiniJson.Bool(r, "behind"),
                    };
                }
                if (!ok) { skipped++; continue; }

                built.Add(new CarryPropAnchors
                {
                    PropKey = kv.Key,
                    Rig = MiniJson.String(node, "rig"),
                    ItemScale = MiniJson.Float(node, "itemScale", 1f),
                    Rows = rows,
                });
            }
            return built.ToArray();
        }

        /// <summary>
        /// Which hand the rig resolved for this row. <c>hand</c> is already swapped for the heading, so
        /// nothing here re-derives it — this only turns the rig's letter into the enum, and reads a
        /// two-handed <c>cradle</c> as <see cref="CarryHandSide.Both"/> (the midpoint of the wrists).
        ///
        /// <para><b>⚠️ The shipped table has no cradle row.</b> The fish declares <c>hands:'auto'</c> and
        /// the bake resolves it by asking the fish rig about its DEFAULT species (a mackerel, which
        /// hangs from one hand); a cod would cradle. So a heavy fish is currently held one-handed. That
        /// is a limit of the baked table, not of this reader — the case is handled here so that a re-bake
        /// carrying a cradle row needs no code change.</para>
        /// </summary>
        static CarryHandSide HandSide(string hand, int hands, string mode)
        {
            if (hands >= 2 || string.Equals(mode, "cradle", System.StringComparison.Ordinal))
                return CarryHandSide.Both;
            if (string.Equals(hand, "L", System.StringComparison.Ordinal)) return CarryHandSide.Left;
            if (string.Equals(hand, "R", System.StringComparison.Ordinal)) return CarryHandSide.Right;
            return CarryHandSide.None;      // a back mount — the rig writes no hand at all
        }

        // ---- the live wrist grid --------------------------------------------------------------------

        static CarryWristFrames[] ReadWrists(Dictionary<string, object> states,
                                             float pivotX, float pivotY, float ppu, int facings,
                                             out int missing)
        {
            missing = 0;
            var built = new List<CarryWristFrames>(GaitStates.Length);
            foreach ((CharacterGait gait, string state) in GaitStates)
            {
                var node = MiniJson.Dict(states, state);
                var dirs = MiniJson.List(node, "anchors");
                int frames = MiniJson.Int(node, "frames");
                if (node == null || dirs == null || dirs.Count != facings || frames <= 0)
                {
                    Debug.LogWarning($"[CarryAnchorTableBuilder] No usable '{state}' anchors for the " +
                                     $"{gait} gait — a prop carried at that gait falls back to its REST " +
                                     "pin, which is per-facing but does not track the hand's swing.");
                    missing++;
                    continue;
                }

                var left = new Vector2[facings * frames];
                var right = new Vector2[facings * frames];
                bool ok = true;
                for (int d = 0; d < facings && ok; d++)
                {
                    if (dirs[d] is not List<object> row || row.Count != frames) { ok = false; break; }
                    for (int f = 0; f < frames; f++)
                    {
                        var l = MiniJson.Dict(row[f], "handL");
                        var r = MiniJson.Dict(row[f], "handR");
                        if (l == null || r == null) { ok = false; break; }
                        int i = d * frames + f;
                        left[i] = RodKitAnchorMath.CellPxToPivotWorld(
                            MiniJson.Float(l, "x"), MiniJson.Float(l, "y"), pivotX, pivotY, ppu);
                        right[i] = RodKitAnchorMath.CellPxToPivotWorld(
                            MiniJson.Float(r, "x"), MiniJson.Float(r, "y"), pivotX, pivotY, ppu);
                    }
                }
                if (!ok)
                {
                    Debug.LogError($"[CarryAnchorTableBuilder] The '{state}' anchor grid is the wrong " +
                                   $"shape (want [{facings}][{frames}] with handL/handR) — the {gait} " +
                                   "gait is SKIPPED and falls back to the rest pin. Re-bake.");
                    missing++;
                    continue;
                }

                built.Add(new CarryWristFrames
                {
                    Gait = gait,
                    FramesPerDir = frames,
                    Left = left,
                    Right = right,
                });
            }
            return built.ToArray();
        }

        // ---- the one number that is NOT in the sidecar -----------------------------------------------

        /// <summary>
        /// The character sheets' import PPU, read off a real imported Fisher sprite.
        ///
        /// <para>It is not in the sidecar because it is not a fact about the rig — it is a fact about how
        /// the sheet was IMPORTED, and the two can disagree (the >2048 px size cap has silently halved a
        /// sheet before). Reading the sprite is therefore the only honest source. The fallback matches
        /// the kit's shipped 32 and is only reached before the sheets have imported at all, in which case
        /// the whole table is about to be rebuilt anyway.</para>
        /// </summary>
        static float CharacterSheetPpu()
        {
            Sprite[] idle = PersistentCoreBuilder.LoadIsoDirFrames($"{ArtIso}/Fisher_idle.png");
            if (idle.Length > 0 && idle[0] != null) return idle[0].pixelsPerUnit;

            Debug.LogWarning("[CarryAnchorTableBuilder] No imported Fisher_idle sprite to read the PPU " +
                             "from — falling back to 32. Re-run this after the sheets import.");
            return 32f;
        }
    }
}
#endif
