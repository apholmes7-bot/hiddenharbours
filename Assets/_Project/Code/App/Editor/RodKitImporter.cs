#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Player;
using HiddenHarbours.Fishing;
using HiddenHarbours.Art.Editor;   // MiniJson — the shared editor JSON reader

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// EDITOR-time importer for the ROD FISHING KIT: parses the baked anchor sidecars
    /// (<c>RodIsoAnchors.json</c> / <c>BobberAnchors.json</c> / <c>FishIsoAnchors.json</c> /
    /// <c>FisherFightAnchors.json</c> — the rigs' exported geometry, ADR 0021 §4) plus the sliced
    /// sheets, converts every pixel anchor to WORLD METRES with each sheet's own import PPU
    /// (<see cref="RodKitAnchorMath"/> — no eyeballed offsets, rule 6), and returns the plain
    /// serializable tables <see cref="RodFightPresenter"/> runs on. Runs ONLY inside the start
    /// builders; runtime never parses JSON.
    ///
    /// <para>Everything degrades per element: a missing sidecar/sheet returns null (that presenter
    /// element stays inert), and a JSON-vs-sheet FRAME-COUNT drift skips that state loudly — silent
    /// half-wiring is how the owner lost a playtest (the frozen-statue bug).</para>
    /// </summary>
    public static class RodKitImporter
    {
        public const string FishingIsoFolder = "Assets/_Project/Art/Fishing/Iso";
        public const string CharacterIsoFolder = "Assets/_Project/Art/Characters/Iso";
        public const string RodAnchorsPath = FishingIsoFolder + "/RodIsoAnchors.json";
        public const string BobberAnchorsPath = FishingIsoFolder + "/BobberAnchors.json";
        public const string FishAnchorsPath = FishingIsoFolder + "/FishIsoAnchors.json";
        public const string FisherAnchorsPath = CharacterIsoFolder + "/FisherFightAnchors.json";

        /// <summary>The rod-state order the presenter indexes by. It IS
        /// <see cref="RodPresenterMath.RodStates"/> — this used to be a second copy of the same list,
        /// and a wiring order that can disagree with the order the consumer reads it in is a rod swap
        /// waiting to hand back the wrong sheet.</summary>
        public static string[] RodStateOrder => RodPresenterMath.RodStates;

        /// <summary>The bobber-state order the presenter indexes by (float, nibble, strike, fly).</summary>
        public static readonly string[] BobberStateOrder = { "float", "nibble", "strike", "fly" };

        private const int Directions = 8;

        // ---- the rod ------------------------------------------------------------------------------

        /// <summary>
        /// The rod overlay's per-state sheets + grip/tip anchors for one tier ('cane' today — the tier
        /// seam is this parameter). Null when the sidecars/sheets aren't importable; individual states
        /// that drifted from their sheet are left null inside the array (that pose's rod stays inert).
        /// </summary>
        public static RodStateVisual[] BuildRodStates(string tier, out int[] behindDirs)
        {
            behindDirs = null;
            object rod = ParseJson(RodAnchorsPath);
            object fisher = ParseJson(FisherAnchorsPath);
            if (rod == null || fisher == null) return null;

            // The grip lives in the CHARACTER's body cell; the tip in the ROD cell. Each converts with
            // its OWN pivot + its own sheet's PPU.
            var rodPivot = MiniJson.Dict(rod, "pivotTopLeft");
            var charPivot = MiniJson.Dict(fisher, "pivotTopLeft");
            if (rodPivot == null || charPivot == null) return null;
            float rodPx = MiniJson.Float(rodPivot, "x"), rodPy = MiniJson.Float(rodPivot, "y");
            float chPx = MiniJson.Float(charPivot, "x"), chPy = MiniJson.Float(charPivot, "y");

            behindDirs = MiniJson.List(rod, "behindDirs")?.OfType<double>()
                .Select(d => (int)System.Math.Round(d)).Where(d => d >= 0 && d < Directions).ToArray();

            var grips = MiniJson.Dict(rod, "grips");
            var tierNode = MiniJson.Dict(MiniJson.Dict(rod, "tiers"), tier);
            var tierStates = MiniJson.Dict(tierNode, "states");
            if (grips == null || tierStates == null)
            {
                Debug.LogWarning($"[RodKitImporter] '{RodAnchorsPath}' has no grips/tiers.{tier}.states " +
                                 "— the rod overlay stays inert. Re-bake the fishing kit.");
                return null;
            }

            // What the rod IS — read ONCE, stamped on every state, so RodPresenterMath.SameRod can
            // hold the whole wired set to one answer. Cell/pivot/length are properties of the rod,
            // not of the pose it happens to be in, and the anchors sidecar says so in one place.
            var cell = MiniJson.Dict(rod, "cell");
            int cellW = MiniJson.Int(cell, "w"), cellH = MiniJson.Int(cell, "h");
            float blankLenM = MiniJson.Float(tierNode, "lenM");
            if (blankLenM <= 0f)
                Debug.LogWarning($"[RodKitImporter] '{RodAnchorsPath}' carries no tiers.{tier}.lenM — " +
                                 "the rod's blank length is unknown, so the same-rod check across " +
                                 "states cannot use it. Re-bake the fishing kit.");

            var result = new RodStateVisual[RodStateOrder.Length];
            for (int s = 0; s < RodStateOrder.Length; s++)
            {
                string state = RodStateOrder[s];
                Sprite[] frames = PersistentCoreBuilder.LoadIsoDirFrames(
                    $"{FishingIsoFolder}/Rod_{tier}_{state}.png");
                var grip = MiniJson.Dict(grips, state);
                var tip = MiniJson.Dict(tierStates, state);
                if (frames.Length == 0 || grip == null || tip == null)
                {
                    Debug.LogWarning($"[RodKitImporter] Rod state '{state}' ({tier}): sheet or anchors " +
                                     "missing — that pose draws no rod.");
                    continue;
                }

                int framesPerDir = frames.Length / Directions;
                if (MiniJson.Int(grip, "frames") != framesPerDir || MiniJson.Int(tip, "frames") != framesPerDir)
                {
                    Debug.LogError($"[RodKitImporter] Rod state '{state}' ({tier}): the sheet has " +
                                   $"{framesPerDir} frames/dir but the anchors say " +
                                   $"{MiniJson.Int(grip, "frames")}/{MiniJson.Int(tip, "frames")} — the " +
                                   "bake drifted. Re-run the fishing-kit bake; skipping this state.");
                    continue;
                }

                float ppuRod = frames[0].pixelsPerUnit;
                float ppuChar = CharacterSheetPpu(ppuRod);
                Vector2[] gripOffsets = ReadDirFramePoints(MiniJson.List(grip, "px"), framesPerDir,
                    (x, y) => RodKitAnchorMath.CellPxToPivotWorld(x, y, chPx, chPy, ppuChar));
                Vector2[] tipOffsets = ReadDirFramePoints(MiniJson.List(tip, "tip"), framesPerDir,
                    (x, y) => RodKitAnchorMath.CellPxToPivotWorld(x, y, rodPx, rodPy, ppuRod));
                if (gripOffsets == null || tipOffsets == null)
                {
                    Debug.LogError($"[RodKitImporter] Rod state '{state}' ({tier}): anchor table shape " +
                                   "is wrong (want [8][frames]) — skipping this state.");
                    continue;
                }

                result[s] = new RodStateVisual
                {
                    State = state,
                    CellW = cellW,
                    CellH = cellH,
                    PivotX = rodPx,
                    PivotY = rodPy,
                    Tier = tier,
                    BlankLenM = blankLenM,
                    RestLiftM = MiniJson.Float(tip, "liftM", 0f),
                    HeldFramesPerDir = MiniJson.Int(grip, "heldFrames", framesPerDir),
                    Frames = frames,
                    FramesPerDir = framesPerDir,
                    GripOffsets = gripOffsets,
                    TipOffsets = tipOffsets,
                };
            }

            // The swap's own precondition, checked here where the wiring is still in one hand: every
            // state must describe the SAME rod. A state that does not is dropped rather than wired,
            // because a rod that changes size or pivot when the pose changes is the exact defect this
            // kit was rebuilt to end, and it is invisible until someone watches it happen.
            RodStateVisual reference = null;
            for (int s = 0; s < result.Length; s++)
            {
                if (result[s] == null) continue;
                if (reference == null) { reference = result[s]; continue; }
                if (!RodPresenterMath.SameRod(reference, result[s], out string why))
                {
                    Debug.LogError($"[RodKitImporter] Rod state '{result[s].State}' ({tier}) is not the " +
                                   $"same rod as '{reference.State}': {why}. One rod serves every " +
                                   "state — dropping this one rather than letting it swap in. Re-bake " +
                                   "the fishing kit from the current rig.");
                    result[s] = null;
                }
            }
            return result;
        }

        // ---- the bobber ---------------------------------------------------------------------------

        /// <summary>The four bobber states in <see cref="BobberStateOrder"/>. Null when the sidecar is
        /// missing; a state whose sheet drifted is left null (that state simply doesn't draw).</summary>
        public static BobberStateVisual[] BuildBobberStates()
        {
            object bob = ParseJson(BobberAnchorsPath);
            var states = MiniJson.Dict(bob, "states");
            if (states == null) return null;

            var result = new BobberStateVisual[BobberStateOrder.Length];
            for (int s = 0; s < BobberStateOrder.Length; s++)
            {
                string state = BobberStateOrder[s];
                Sprite[] frames = LoadSingleDirFrames($"{FishingIsoFolder}/Bobber_{state}.png");
                var node = MiniJson.Dict(states, state);
                if (frames.Length == 0 || node == null) continue;
                if (MiniJson.Int(node, "frames") != frames.Length)
                {
                    Debug.LogError($"[RodKitImporter] Bobber state '{state}': sheet has {frames.Length} " +
                                   $"frames, anchors say {MiniJson.Int(node, "frames")} — re-bake; skipped.");
                    continue;
                }

                float ppu = frames[0].pixelsPerUnit;
                var attach = MiniJson.List(node, "lineAttach");
                var offsets = new Vector2[frames.Length];
                for (int f = 0; f < frames.Length; f++)
                {
                    var p = attach != null && f < attach.Count ? attach[f] as Dictionary<string, object> : null;
                    offsets[f] = p != null
                        ? RodKitAnchorMath.OffsetPxToWorld(MiniJson.Float(p, "dx"), MiniJson.Float(p, "dy"), ppu)
                        : Vector2.zero;
                }

                result[s] = new BobberStateVisual
                {
                    State = state,
                    Frames = frames,
                    SecondsPerFrame = Mathf.Max(0.01f, MiniJson.Float(node, "ms", 120f) / 1000f),
                    LineAttachOffsets = offsets,
                };
            }
            return result;
        }

        // ---- the fish -----------------------------------------------------------------------------

        /// <summary>
        /// One entry per baked species whose id can be found among <paramref name="regionFish"/> (the
        /// sheet key must appear in the def id, 'cod' in 'fish.atlantic_cod'; the ids are the defs',
        /// never invented here), each carrying its whole SIZE LADDER.
        ///
        /// <para><b>The ladder is read, not derived.</b> Which weights the three rungs draw, and how
        /// many hands each takes, are facts about the rig that the bake measured and published; this
        /// only converts them. The held sheet follows the rung's own hand count — gill for the
        /// two-arm cradle, tail for one hand — which is why a big cod and a small one can hold
        /// differently.</para>
        ///
        /// <para><b>An older sidecar still imports</b>, loudly. A pre-rung-major bake published one
        /// hold and one mouth table for the whole species; that becomes a single middle rung and says
        /// so, rather than half-wiring the ladder in silence. Run
        /// <c>Hidden Harbours ▸ Art ▸ Rewrite Catch Pass 2 Fish Anchors</c> to close it.</para>
        /// </summary>
        public static FishSpeciesVisual[] BuildFishSpecies(FishSpeciesDef[] regionFish)
        {
            object fish = ParseJson(FishAnchorsPath);
            var species = MiniJson.Dict(fish, "species");
            if (species == null || regionFish == null || regionFish.Length == 0)
                return System.Array.Empty<FishSpeciesVisual>();

            var entries = new List<FishSpeciesVisual>();
            foreach (KeyValuePair<string, object> kv in species)
            {
                string key = kv.Key;
                FishSpeciesDef def = regionFish.FirstOrDefault(
                    d => d != null && !string.IsNullOrEmpty(d.Id) && d.Id.Contains(key));
                if (def == null)
                {
                    Debug.Log($"[RodKitImporter] Baked fish '{key}' has no matching FishSpeciesDef in " +
                              "this region's roster — its sheets stay unwired until the species lands.");
                    continue;
                }

                FishRungVisual[] rungs = ReadRungs(key, kv.Value);
                if (rungs.Length == 0)
                {
                    Debug.LogWarning($"[RodKitImporter] Species '{key}' wired NO size rungs — the fight " +
                                     "will draw nothing for it. Re-bake catch pass 2's fish sheets.");
                    continue;
                }

                entries.Add(new FishSpeciesVisual { FishId = def.Id, Rungs = rungs });
            }
            return entries.ToArray();
        }

        /// <summary>
        /// One species' rungs, ascending, from the sidecar's own ladder — or a single middle rung
        /// reconstructed from a pre-rung-major sidecar.
        /// </summary>
        private static FishRungVisual[] ReadRungs(string key, object speciesNode)
        {
            var ladder = MiniJson.List(speciesNode, "rungs");
            var speciesStates = MiniJson.Dict(speciesNode, "states");

            // ---- the legacy shape: one hold, one mouth table, no ladder ------------------------
            if (ladder == null || ladder.Count == 0)
            {
                Debug.LogWarning(
                    $"[RodKitImporter] '{FishAnchorsPath}' publishes no size ladder for '{key}' — this " +
                    "is a pre-pass-2 sidecar. Wiring the unsuffixed sheets as a single middle rung, so " +
                    "every catch of this species draws one size. Run Hidden Harbours ▸ Art ▸ " +
                    "Rewrite Catch Pass 2 Fish Anchors to publish the ladder.");
                var holdNode = MiniJson.Dict(speciesNode, "hold");
                var only = BuildRung(key, suffix: "", kg: 0f,
                                     twoHanded: MiniJson.Int(holdNode, "hands", 1) >= 2,
                                     statesNode: speciesStates);
                return only == null ? System.Array.Empty<FishRungVisual>() : new[] { only };
            }

            var built = new List<FishRungVisual>(ladder.Count);
            foreach (object node in ladder)
            {
                if (node is not Dictionary<string, object> r) continue;

                // ⚠️ 'hands' and the per-rung 'states' block arrived together, when the sidecar went
                // rung-major. Their ABSENCE is the tell that the JSON predates the ladder's consumer:
                // falling back to the species-level table would silently give every rung the MIDDLE
                // rung's mouth offsets, which are wrong by a pixel or two at the other two sizes and
                // wrong in a way nobody would ever see in a log.
                bool perRung = MiniJson.Has(r, "hands") && MiniJson.Dict(r, "states") != null;
                if (!perRung)
                    Debug.LogWarning(
                        $"[RodKitImporter] Rung '{MiniJson.String(r, "suffix")}' of '{key}' carries no " +
                        "per-rung hands/states — falling back to the species-level table, whose mouth " +
                        "offsets were measured at the MIDDLE rung and do not fit this one. Run Hidden " +
                        "Harbours ▸ Art ▸ Rewrite Catch Pass 2 Fish Anchors.");

                object statesNode = perRung ? MiniJson.Dict(r, "states") : speciesStates;
                bool twoHanded = perRung
                    ? MiniJson.Int(r, "hands", 1) >= 2
                    : MiniJson.Int(MiniJson.Dict(speciesNode, "hold"), "hands", 1) >= 2;

                FishRungVisual rung = BuildRung(key, MiniJson.String(r, "suffix") ?? "",
                                                (float)MiniJson.Float(r, "kg"), twoHanded, statesNode);
                if (rung != null) built.Add(rung);
            }

            // Ascending by weight, so the ladder reads as a ladder in the inspector and a picker can
            // rely on the order. The bake writes them in order today; sorting is what makes that a
            // property of the TABLE rather than a habit of whoever wrote it.
            built.Sort((a, b) => a.Kg.CompareTo(b.Kg));
            return built.ToArray();
        }

        /// <summary>One rung's sheets and mouth tables, or null when the rung has no art at all.</summary>
        private static FishRungVisual BuildRung(string key, string suffix, float kg, bool twoHanded,
                                                object statesNode)
        {
            string stem = $"{FishingIsoFolder}/Fish_{key}{suffix}";

            Sprite[] shadow = PersistentCoreBuilder.LoadIsoDirFrames($"{stem}_shadow.png");
            Sprite[] dart = PersistentCoreBuilder.LoadIsoDirFrames($"{stem}_dart.png");
            Sprite[] thrash = PersistentCoreBuilder.LoadIsoDirFrames($"{stem}_thrash.png");
            Sprite[] held = PersistentCoreBuilder.LoadIsoDirFrames(
                $"{stem}_{(twoHanded ? "gill" : "tail")}.png");

            // Pass 2's two new surface beats. Absent sheets are NOT an error — the loader hands back
            // an empty array, the fields stay empty, and a presenter that checks Length simply never
            // plays them. That keeps this importer working against a pass-1 bake.
            Sprite[] roll = PersistentCoreBuilder.LoadIsoDirFrames($"{stem}_roll.png");
            Sprite[] jump = PersistentCoreBuilder.LoadIsoDirFrames($"{stem}_jump.png");

            if (shadow.Length == 0 && dart.Length == 0 && thrash.Length == 0 && held.Length == 0)
            {
                Debug.LogWarning($"[RodKitImporter] No sliced sheets at '{stem}_*.png' — rung " +
                                 $"'{suffix}' of '{key}' is SKIPPED. Run the catch pass 2 bake, then " +
                                 "the two sheet slicers (the bake does not slice).");
                return null;
            }

            float ppu = FirstPpu(dart) ?? FirstPpu(shadow) ?? FirstPpu(thrash) ?? 32f;
            return new FishRungVisual
            {
                Suffix = suffix,
                Kg = kg,
                TwoHanded = twoHanded,
                ShadowFrames = shadow,
                ShadowFramesPerDir = shadow.Length / Directions,
                DartFrames = dart,
                DartFramesPerDir = dart.Length / Directions,
                DartMouthOffsets = ReadMouths(statesNode, "dart", dart.Length / Directions, ppu),
                ThrashFrames = thrash,
                ThrashFramesPerDir = thrash.Length / Directions,
                ThrashMouthOffsets = ReadMouths(statesNode, "thrash", thrash.Length / Directions, ppu),
                HeldFrames = held,
                HeldFramesPerDir = held.Length / Directions,
                RollFrames = roll,
                RollFramesPerDir = roll.Length / Directions,
                RollMouthOffsets = ReadMouths(statesNode, "roll", roll.Length / Directions, ppu),
                JumpFrames = jump,
                JumpFramesPerDir = jump.Length / Directions,
                JumpMouthOffsets = ReadMouths(statesNode, "jump", jump.Length / Directions, ppu),
            };
        }

        // ---- the fisher's hands (the land beat's held-fish pin) ------------------------------------

        /// <summary>Hand anchors of the LAND state, world m from the angler pivot, [dir·frames+f].
        /// False (and null outs) when the sidecar/sheet is missing.</summary>
        public static bool BuildLandHands(out Vector2[] mid, out Vector2[] right, out int framesPerDir)
        {
            mid = null;
            right = null;
            framesPerDir = 0;

            object fisher = ParseJson(FisherAnchorsPath);
            var land = MiniJson.Dict(MiniJson.Dict(fisher, "states"), "land");
            var pivot = MiniJson.Dict(fisher, "pivotTopLeft");
            var dirs = MiniJson.List(land, "anchors");
            if (land == null || pivot == null || dirs == null || dirs.Count != Directions) return false;

            float px = MiniJson.Float(pivot, "x"), py = MiniJson.Float(pivot, "y");
            float ppu = CharacterSheetPpu(32f);
            framesPerDir = MiniJson.Int(land, "frames");
            if (framesPerDir <= 0) return false;

            mid = new Vector2[Directions * framesPerDir];
            right = new Vector2[Directions * framesPerDir];
            for (int d = 0; d < Directions; d++)
            {
                if (!(dirs[d] is List<object> row) || row.Count != framesPerDir) { mid = right = null; return false; }
                for (int f = 0; f < framesPerDir; f++)
                {
                    var l = MiniJson.Dict(row[f], "handL");
                    var r = MiniJson.Dict(row[f], "handR");
                    if (l == null || r == null) { mid = right = null; return false; }
                    Vector2 wl = RodKitAnchorMath.CellPxToPivotWorld(
                        MiniJson.Float(l, "x"), MiniJson.Float(l, "y"), px, py, ppu);
                    Vector2 wr = RodKitAnchorMath.CellPxToPivotWorld(
                        MiniJson.Float(r, "x"), MiniJson.Float(r, "y"), px, py, ppu);
                    mid[d * framesPerDir + f] = (wl + wr) * 0.5f;
                    right[d * framesPerDir + f] = wr;
                }
            }
            return true;
        }

        // ---- shared loaders -------------------------------------------------------------------------

        /// <summary>A single-direction sheet's frames ordered by their <c>_f&lt;n&gt;</c> suffix (the
        /// bobber sheets — 'directional: false', sliced as d0 only). Empty when unsliced/missing.</summary>
        public static Sprite[] LoadSingleDirFrames(string path)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            if (sprites.Length == 0) return System.Array.Empty<Sprite>();
            var ordered = sprites.OrderBy(s => FrameSuffix(s.name)).ToArray();
            for (int i = 0; i < ordered.Length; i++)
                if (FrameSuffix(ordered[i].name) != i) return System.Array.Empty<Sprite>();
            return ordered;
        }

        private static int FrameSuffix(string spriteName)
        {
            int f = spriteName.LastIndexOf("_f", System.StringComparison.Ordinal);
            return f >= 0 && int.TryParse(spriteName.Substring(f + 2), out int n) ? n : -1;
        }

        // ---- internals ------------------------------------------------------------------------------

        private static object ParseJson(string assetPath)
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (text == null)
            {
                Debug.LogWarning($"[RodKitImporter] No anchor sidecar at '{assetPath}' — the elements " +
                                 "it pins stay inert. Re-run the fishing-kit bake, then this builder.");
                return null;
            }
            try
            {
                return MiniJson.Parse(text.text);
            }
            catch (System.FormatException e)
            {
                Debug.LogError($"[RodKitImporter] '{assetPath}' failed to parse ({e.Message}) — the " +
                               "elements it pins stay inert. Re-bake the fishing kit.");
                return null;
            }
        }

        /// <summary>An [8][frames] table of {x,y} points → a flattened world-metre array via
        /// <paramref name="convert"/>; null when the shape is wrong (the caller warns + skips).</summary>
        private static Vector2[] ReadDirFramePoints(List<object> dirs, int framesPerDir,
                                                    System.Func<float, float, Vector2> convert)
        {
            if (dirs == null || dirs.Count != Directions) return null;
            var result = new Vector2[Directions * framesPerDir];
            for (int d = 0; d < Directions; d++)
            {
                if (!(dirs[d] is List<object> row) || row.Count != framesPerDir) return null;
                for (int f = 0; f < framesPerDir; f++)
                {
                    if (!(row[f] is Dictionary<string, object> p)) return null;
                    result[d * framesPerDir + f] = convert(MiniJson.Float(p, "x"), MiniJson.Float(p, "y"));
                }
            }
            return result;
        }

        /// <summary>A species state's mouth table ([8][frames]{dx,dy} offsets from the fish pivot) as
        /// flattened world metres; null-safe (missing table → null → the line pins to the fish pivot).</summary>
        private static Vector2[] ReadMouths(object statesNode, string state, int framesPerDir, float ppu)
        {
            var mouths = MiniJson.List(MiniJson.Dict(statesNode, state), "mouth");
            if (mouths == null || mouths.Count != Directions || framesPerDir <= 0) return null;
            var result = new Vector2[Directions * framesPerDir];
            for (int d = 0; d < Directions; d++)
            {
                if (!(mouths[d] is List<object> row) || row.Count != framesPerDir) return null;
                for (int f = 0; f < framesPerDir; f++)
                {
                    var p = row[f] as Dictionary<string, object>;
                    result[d * framesPerDir + f] = p != null
                        ? RodKitAnchorMath.OffsetPxToWorld(MiniJson.Float(p, "dx"), MiniJson.Float(p, "dy"), ppu)
                        : Vector2.zero;
                }
            }
            return result;
        }

        /// <summary>The character sheets' import PPU, read off a real Fisher sprite (falling back to
        /// <paramref name="fallback"/> when none has imported yet — the same PPU the kit shares).</summary>
        private static float CharacterSheetPpu(float fallback)
        {
            Sprite[] any = PersistentCoreBuilder.LoadIsoDirFrames($"{CharacterIsoFolder}/Fisher_hold.png");
            return any.Length > 0 && any[0] != null ? any[0].pixelsPerUnit : fallback;
        }

        private static float? FirstPpu(Sprite[] frames)
            => frames != null && frames.Length > 0 && frames[0] != null ? frames[0].pixelsPerUnit : null;
    }
}
#endif
