using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Builds <c>Assets/_Project/Resources/SeagullVisualDef.asset</c> — the one thing the runtime
    /// loads to draw a gull. It is REGENERATED, never hand-edited: three committed files are read in
    /// one pass and everything the def carries comes from them.
    ///
    /// <list type="bullet">
    /// <item><b>The baked sheet</b> (<c>Seagull.png</c>) supplies the 384 sliced cells, looked up by
    /// the contract's own <c>gull_&lt;anim&gt;_&lt;frame&gt;_d&lt;row&gt;</c> names.</item>
    /// <item><b>The contract</b> (<c>Seagull.contract.json</c>) supplies the page: 8 rows × 48
    /// columns, the 64×64 cell, the (32,46) pivot, 32 px/m, the measured CLOCKWISE facing
    /// convention, and the <c>order</c> array that says which column each state's strip starts at.
    /// The bake wrote it; nothing here re-derives it.</item>
    /// <item><b>The gameplay sidecar</b> (<c>seagullIsoRig.gameplay.json</c>) supplies every NUMBER a
    /// bird behaves by — frame times, speeds, climb rates, altitude bands, the oneshot chains, the
    /// 26 transitions, the LAND clear box, the WATER float/rock caps and the FLOCK block. Read
    /// through <see cref="SeagullSidecarReader"/> with the rig-hash pin ENFORCED, so a rig that has
    /// moved under the sidecar stops the build instead of quietly baking stale behaviour.</item>
    /// </list>
    ///
    /// <para><b>Why a builder and not a hand-authored asset (rule 2 / rule 6).</b> The def is derived
    /// data — the art director owns the rig and the sidecar, and this turns their bytes into the
    /// asset the game reads. Editing the <c>.asset</c> by hand puts a number in the game that no
    /// source file agrees with, which is precisely the failure the <c>derivedFromRigSha256</c> pin
    /// exists to make impossible.</para>
    ///
    /// <para><b>It fails loudly.</b> A missing cell, a strip that does not match the sidecar's frame
    /// count, a contract that names a state the sheet is not baked for, a sidecar whose hash has
    /// moved — each throws with the fix in the message. The half-built alternative is a def that
    /// loads, draws, and behaves wrongly.</para>
    ///
    /// <para>Editor-only, and nothing at runtime binds it: the game loads the finished
    /// <c>.asset</c> from <c>Resources</c> and never sees this class.</para>
    /// </summary>
    public static class SeagullVisualDefBuilder
    {
        /// <summary>Where the runtime looks for it — <see cref="SeagullVisualDef.ResourcesPath"/>
        /// under a <c>Resources</c> folder.</summary>
        public const string DefAssetPath =
            "Assets/_Project/Resources/" + SeagullVisualDef.ResourcesPath + ".asset";

        public const string SheetPath =
            SeagullBaker.DefaultOutputFolder + "/" + SeagullBaker.SheetName + ".png";

        public const string ContractPath =
            SeagullBaker.DefaultOutputFolder + "/" + SeagullBaker.ContractFileName;

        /// <summary>The rig itself — its bytes are what the sidecar's hash is checked against.</summary>
        public const string RigPath = "docs/art/rigs/seagullIsoRig.js";

        [MenuItem("Hidden Harbours/Art/Build Seagull Visual Def (sheet + sidecar → Resources)",
                  priority = 52)]
        public static void BuildMenu()
        {
            try
            {
                Debug.Log(Build());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] the seagull visual def did NOT build: {ex.Message}\n{ex}");
                throw;
            }
        }

        /// <summary>
        /// Headless entry point for CI / <c>-executeMethod</c>.
        ///
        /// ⚠️ Never invoke this with <c>-quit</c> alongside <c>-runTests</c>; the two race and Unity
        /// exits 0 having run nothing (ADR 0021), which reads as a pass.
        /// </summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                Debug.Log(Build());
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] headless seagull visual def build failed: {ex}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Reads the three sources, writes the asset, validates it, and returns a one-paragraph
        /// summary of what landed. Throws on the first thing that does not add up.
        /// </summary>
        public static string Build()
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName
                          ?? Directory.GetCurrentDirectory();

            // ── the sidecar, with the rig-hash pin enforced ──────────────────────────────────────
            string rigFull = Path.Combine(root, RigPath.Replace('/', Path.DirectorySeparatorChar));
            string sidecarFull = Path.Combine(root,
                SeagullBaker.SidecarPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(rigFull))
                throw new FileNotFoundException($"the seagull rig is missing: {RigPath}", rigFull);
            if (!File.Exists(sidecarFull))
                throw new FileNotFoundException(
                    $"the seagull gameplay sidecar is missing: {SeagullBaker.SidecarPath}", sidecarFull);

            var read = SeagullSidecarReader.Read(File.ReadAllText(sidecarFull),
                                                 SeagullBaker.SidecarPath,
                                                 File.ReadAllBytes(rigFull));
            if (!read.Ok)
                throw new InvalidOperationException(
                    $"{SeagullBaker.SidecarPath} did not read: {string.Join("; ", read.Errors)}");
            var g = read.Gameplay;

            // ── the contract: the page, and which column each strip starts at ────────────────────
            string contractFull = Path.Combine(root, ContractPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(contractFull))
                throw new FileNotFoundException(
                    $"the seagull contract is missing — bake the sheet first: {ContractPath}", contractFull);

            object contract = DeckSidecarJson.Parse(File.ReadAllText(contractFull));
            int rows = IntOf(contract, "rows");
            int columns = IntOf(contract, "columns");
            float pixelsPerUnit = DeckSidecarJson.Float(DeckSidecarJson.Member(contract, "pixelsPerUnit"));
            bool ccw = FlagOf(contract, "facingsAreCounterClockwise");
            object cell = DeckSidecarJson.Member(contract, "cell");
            object pivot = DeckSidecarJson.Member(contract, "pivotTopLeft");
            int cellWidth = IntOf(cell, "w"), cellHeight = IntOf(cell, "h");
            int pivotX = IntOf(pivot, "x"), pivotY = IntOf(pivot, "y");
            string contractSha = DeckSidecarJson.String(
                DeckSidecarJson.Member(contract, "derivedFromRigSha256")) ?? "";

            if (rows <= 0 || columns <= 0 || cellWidth <= 0 || cellHeight <= 0)
                throw new InvalidOperationException(
                    $"{ContractPath} does not describe a page (rows {rows}, columns {columns}, " +
                    $"cell {cellWidth}×{cellHeight}).");

            // The three files must all be talking about the SAME rig. The reader already refused a
            // moved rig; this catches a contract baked from a different one, which nothing else would.
            if (!string.Equals(contractSha, read.ActualRigSha, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"{ContractPath} was baked from rig {Short(contractSha)} but {RigPath} is now " +
                    $"{Short(read.ActualRigSha)}. Re-bake the sheet before building the def — the " +
                    "cells and the behaviour would be describing two different birds.");

            // ── the strips: contiguous runs of one anim across the contract's own column order ───
            var strips = StripsFromOrder(contract, columns, g);

            // ── the cells: every one of rows × columns, by the contract's sprite-name template ────
            var cells = CellsFromSheet(rows, columns, strips, out int missing);
            if (missing > 0)
                throw new InvalidOperationException(
                    $"{missing} of {rows * columns} cells were not found in {SheetPath}. The sheet is " +
                    "unsliced or sliced with different names — re-run the seagull bake (it slices in " +
                    "the same operation) and check the .meta carries a grid.");

            // ── the state table and the edges, in the canonical order ────────────────────────────
            var states = StatesFromSidecar(g);
            var edges = EdgesFromSidecar(g);

            // The sea-state rock caps are stated in the WATER note's prose rather than as fields.
            RockCapsFromNote(g.WaterNote, out float rockRoll, out float rockHeave);

            // ── write it ─────────────────────────────────────────────────────────────────────────
            var def = AssetDatabase.LoadAssetAtPath<SeagullVisualDef>(DefAssetPath);
            bool created = def == null;
            if (created)
            {
                string folder = Path.GetDirectoryName(DefAssetPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
                    throw new InvalidOperationException(
                        $"{folder} does not exist — the def has to sit under a Resources folder for " +
                        "the runtime to load it.");
                def = ScriptableObject.CreateInstance<SeagullVisualDef>();
                AssetDatabase.CreateAsset(def, DefAssetPath);
            }

            def.EditorPopulate(rows, columns, cellWidth, cellHeight, pivotX, pivotY, pixelsPerUnit,
                               ccw, read.ActualRigSha, strips, cells, states, edges);
            def.EditorPopulateRules(
                g.LengthMetres, g.WingspanMetres, g.MassKg, g.DraftMetres,
                g.BodyCentreZStand, g.BodyCentreZPerch, g.BodyCentreZFloat,
                new Vector2(g.FootprintStandX, g.FootprintStandY),
                new Vector2(g.FootprintWingsOpenX, g.FootprintWingsOpenY),
                new Vector2(g.LandNeedsClearX, g.LandNeedsClearY),
                g.LandApproachIntoWind, g.LandMinFlatMetres,
                g.FloatBodyZMetres, g.SplashBurstFrame, g.DriftWithCurrent,
                rockRoll, rockHeave,
                g.FlockSizeMin, g.FlockSizeMax, g.FlockRadiusMetres,
                g.FlockAltitudeMinMetres, g.FlockAltitudeMaxMetres,
                g.FlockPeriodMinSeconds, g.FlockPeriodMaxSeconds, g.FlockGlideDuty,
                g.FlockSwoopEveryMinSeconds, g.FlockSwoopEveryMaxSeconds,
                g.FlockSpacingMetres, g.FlockAttractRadiusMetres, g.FlockFleeRadiusMetres,
                g.FlockSettleAfterSeconds, g.FlockRegroupSeconds);

            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();

            // Validate the thing that is now on disk, not the thing in memory.
            AssetDatabase.ImportAsset(DefAssetPath, ImportAssetOptions.ForceUpdate);
            var written = AssetDatabase.LoadAssetAtPath<SeagullVisualDef>(DefAssetPath);
            if (written == null)
                throw new InvalidOperationException(
                    $"{DefAssetPath} was written but did not import as a SeagullVisualDef.");
            if (!written.TryValidate(out string error))
                throw new InvalidOperationException(
                    $"{DefAssetPath} was written but does not validate: {error}");

            var sb = new StringBuilder();
            sb.Append($"[rig-baker] {(created ? "created" : "regenerated")} {DefAssetPath} — ");
            sb.Append($"{rows}×{columns} page, {cells.Count} cells, {strips.Count} strips, ");
            sb.Append($"{states.Count} states, {edges.Count} transitions, rig {Short(read.ActualRigSha)}. ");
            sb.Append("COMMIT the .asset and its .meta BY NAME; a Unity run also rewrites boat assets " +
                      "and ProjectSettings, and none of that belongs in this PR.");
            if (read.Notes.Count > 0) sb.Append("\nsidecar notes: " + string.Join("; ", read.Notes));
            return sb.ToString();
        }

        // ── sources ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The contract's <c>order</c> array is 48 records of <c>{col, anim, frame}</c>. A strip is a
        /// contiguous run of one anim; its column is where the run starts and its frame count is the
        /// run's length. Cross-checked against the sidecar so a sheet baked with a different frame
        /// count than the behaviour declares cannot get through.
        /// </summary>
        static List<SeagullVisualDef.StripEntry> StripsFromOrder(object contract, int columns,
                                                                 SeagullGameplay g)
        {
            var order = DeckSidecarJson.AsArray(DeckSidecarJson.Member(contract, "order"));
            if (order == null || order.Count != columns)
                throw new InvalidOperationException(
                    $"{ContractPath} lists {(order == null ? "no" : order.Count.ToString())} columns " +
                    $"in 'order' but declares {columns}.");

            var strips = new List<SeagullVisualDef.StripEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            SeagullVisualDef.StripEntry current = null;

            for (int col = 0; col < order.Count; col++)
            {
                string anim = DeckSidecarJson.String(DeckSidecarJson.Member(order[col], "anim")) ?? "";
                int declaredCol = IntOf(order[col], "col");
                if (declaredCol != col)
                    throw new InvalidOperationException(
                        $"{ContractPath} 'order' is out of sequence: entry {col} says col {declaredCol}.");
                if (SeagullStates.IndexOf(anim) < 0)
                    throw new InvalidOperationException(
                        $"{ContractPath} column {col} names '{anim}', which is not one of the " +
                        $"{SeagullStates.Order.Length} states the sheet is baked for.");

                if (current != null && current.State == anim) { current.Frames++; continue; }

                // A state whose columns are not contiguous cannot be addressed as one strip.
                if (!seen.Add(anim))
                    throw new InvalidOperationException(
                        $"{ContractPath} splits '{anim}' across non-adjacent columns; a strip has to " +
                        "be one run.");
                current = new SeagullVisualDef.StripEntry { State = anim, Column = col, Frames = 1 };
                strips.Add(current);
            }

            foreach (var s in strips)
            {
                var declared = g.State(s.State);
                if (declared == null)
                    throw new InvalidOperationException(
                        $"the sidecar declares no state '{s.State}', but the sheet bakes {s.Frames} " +
                        "columns of it.");
                if (declared.Frames != s.Frames)
                    throw new InvalidOperationException(
                        $"'{s.State}': the sheet bakes {s.Frames} frames, the sidecar declares " +
                        $"{declared.Frames}. Re-bake, or fix the sidecar — they cannot both be right.");
            }
            return strips;
        }

        /// <summary>
        /// Every cell of the page in row-major order, which is the layout
        /// <see cref="SeagullVisualDef.CellAt"/> indexes. Names follow the contract's
        /// <c>gull_&lt;anim&gt;_&lt;frame&gt;_d&lt;row&gt;</c> template; row r IS facing r (the rig
        /// is clockwise and the bake does not remap).
        /// </summary>
        static List<Sprite> CellsFromSheet(int rows, int columns,
                                           List<SeagullVisualDef.StripEntry> strips, out int missing)
        {
            var byName = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(SheetPath))
                if (o is Sprite sprite) byName[sprite.name] = sprite;
            if (byName.Count == 0)
                throw new InvalidOperationException(
                    $"{SheetPath} carries no sprites. It is unsliced (or missing) — run the seagull " +
                    "bake, which slices in the same operation.");

            // column → (anim, frame), straight off the strips.
            var animOf = new string[columns];
            var frameOf = new int[columns];
            foreach (var s in strips)
                for (int f = 0; f < s.Frames; f++) { animOf[s.Column + f] = s.State; frameOf[s.Column + f] = f; }

            missing = 0;
            var cells = new List<Sprite>(rows * columns);
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < columns; col++)
                {
                    string name = $"gull_{animOf[col]}_{frameOf[col]}_d{row}";
                    cells.Add(byName.TryGetValue(name, out var sprite) ? sprite : null);
                    if (sprite == null) missing++;
                }
            return cells;
        }

        /// <summary>
        /// The state rows, emitted in <see cref="SeagullStates.Order"/> so the generated asset is
        /// stable whatever order the sidecar happens to list them in. Every declared state must be
        /// one the sheet knows, and every state the sheet knows must be declared.
        /// </summary>
        static List<SeagullVisualDef.StateEntry> StatesFromSidecar(SeagullGameplay g)
        {
            foreach (var s in g.States)
                if (SeagullStates.IndexOf(s.Id) < 0)
                    throw new InvalidOperationException(
                        $"the sidecar declares state '{s.Id}', which the sheet is not baked for.");

            var states = new List<SeagullVisualDef.StateEntry>(SeagullStates.Order.Length);
            foreach (string id in SeagullStates.Order)
            {
                var s = g.State(id);
                if (s == null)
                    throw new InvalidOperationException(
                        $"the sidecar declares no state '{id}'. All {SeagullStates.Order.Length} are " +
                        "needed — a bird with a hole in its table stops mid-chain.");
                states.Add(new SeagullVisualDef.StateEntry
                {
                    State = s.Id,
                    Frames = s.Frames,
                    FrameMilliseconds = s.Milliseconds,
                    SpeedMetresPerSecond = s.SpeedMetresPerSecond,
                    ClimbMetresPerSecond = s.ClimbMetresPerSecond,
                    AltitudeA = s.AltitudeA,
                    AltitudeB = s.AltitudeB,
                    Loop = s.Loop,
                    Next = s.Next ?? "",
                    TravelMetres = s.HasTravel ? s.TravelMetres : 0f,
                    CyclesMin = s.HasCycles ? s.CyclesMin : 0,
                    CyclesMax = s.HasCycles ? s.CyclesMax : 0,
                });
            }
            return states;
        }

        /// <summary>The declared edges, verbatim and in the sidecar's own order — the graph is the
        /// art director's, and reordering it would make a re-export look like a change.</summary>
        static List<SeagullVisualDef.EdgeEntry> EdgesFromSidecar(SeagullGameplay g)
        {
            var edges = new List<SeagullVisualDef.EdgeEntry>(g.Transitions.Count);
            foreach (var t in g.Transitions)
            {
                if (SeagullStates.IndexOf(t.From) < 0 || SeagullStates.IndexOf(t.To) < 0)
                    throw new InvalidOperationException(
                        $"the sidecar declares the edge {t.From}->{t.To}, which names a state the " +
                        "sheet is not baked for.");
                edges.Add(new SeagullVisualDef.EdgeEntry { From = t.From, To = t.To });
            }
            if (edges.Count == 0)
                throw new InvalidOperationException(
                    "the sidecar declares no transitions; the birds would have nowhere to go.");
            return edges;
        }

        /// <summary>
        /// The sea-state ROCK caps. ⚠️ The art director states them in the WATER block's <c>note</c>
        /// ("Sea state adds the fleet ROCK pose: roll_deg 2.4 / heave_px 1 max — a gull rides higher
        /// than a hull") rather than as fields, so this reads them out of that sentence and refuses
        /// to guess if they are not there. Two numbers in prose are still the art director's numbers;
        /// a default baked into C# would not be, and rule 6 says the number lives in the data. When
        /// the sidecar schema grows real fields for these, delete this and read them.
        /// </summary>
        static void RockCapsFromNote(string note, out float rollDegrees, out float heavePixels)
        {
            rollDegrees = ReadLabelled(note, "roll_deg");
            heavePixels = ReadLabelled(note, "heave_px");
            if (rollDegrees <= 0f || heavePixels <= 0f)
                throw new InvalidOperationException(
                    "the sidecar's WATER note no longer states the ROCK caps as 'roll_deg <n>' and " +
                    "'heave_px <n>'. They are the only statement of how hard the sea may rock a " +
                    "floating gull; ask the art director to restore them (better: as fields) rather " +
                    $"than letting the def default to nothing. Note read: \"{note}\"");
        }

        /// <summary>The number that follows a label in a sentence, or 0 if it is not there.</summary>
        static float ReadLabelled(string text, string label)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            int at = text.IndexOf(label, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return 0f;
            int i = at + label.Length;
            while (i < text.Length && (text[i] == ' ' || text[i] == ':' || text[i] == '=')) i++;
            int start = i;
            while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.')) i++;
            return i > start && float.TryParse(text.Substring(start, i - start),
                                               System.Globalization.NumberStyles.Float,
                                               System.Globalization.CultureInfo.InvariantCulture,
                                               out float v)
                ? v : 0f;
        }

        // ── small helpers ───────────────────────────────────────────────────────────────────────

        static int IntOf(object owner, string key) =>
            Mathf.RoundToInt(DeckSidecarJson.Float(DeckSidecarJson.Member(owner, key)));

        static bool FlagOf(object owner, string key)
        {
            object v = DeckSidecarJson.Member(owner, key);
            if (v is bool b) return b;
            return DeckSidecarJson.TryDouble(v, out double d) && d != 0.0;
        }

        static string Short(string sha) =>
            string.IsNullOrEmpty(sha) ? "(none)" : sha.Substring(0, Math.Min(8, sha.Length));
    }
}
