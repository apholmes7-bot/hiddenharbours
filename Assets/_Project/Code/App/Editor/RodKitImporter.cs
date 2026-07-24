#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Player;
using HiddenHarbours.Fishing;

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

        /// <summary>The rod-state order the presenter indexes by (<c>RodPresenterMath.RodSheetFor</c>).</summary>
        public static readonly string[] RodStateOrder =
            { "hold", "bite", "strike", "reel", "land", "castBack", "castRelease" };

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
            var tierStates = MiniJson.Dict(MiniJson.Dict(MiniJson.Dict(rod, "tiers"), tier), "states");
            if (grips == null || tierStates == null)
            {
                Debug.LogWarning($"[RodKitImporter] '{RodAnchorsPath}' has no grips/tiers.{tier}.states " +
                                 "— the rod overlay stays inert. Re-bake the fishing kit.");
                return null;
            }

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
                    Frames = frames,
                    FramesPerDir = framesPerDir,
                    GripOffsets = gripOffsets,
                    TipOffsets = tipOffsets,
                };
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
        /// sheet key must appear in the def id — 'cod' in 'fish.atlantic_cod'; the ids are the defs',
        /// never invented here). The held sheet is gill (two hands) or tail per the rig's hold.hands.
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

                var statesNode = MiniJson.Dict(kv.Value, "states");
                var holdNode = MiniJson.Dict(kv.Value, "hold");
                bool twoHanded = MiniJson.Int(holdNode, "hands", 1) >= 2;

                Sprite[] shadow = PersistentCoreBuilder.LoadIsoDirFrames($"{FishingIsoFolder}/Fish_{key}_shadow.png");
                Sprite[] dart = PersistentCoreBuilder.LoadIsoDirFrames($"{FishingIsoFolder}/Fish_{key}_dart.png");
                Sprite[] thrash = PersistentCoreBuilder.LoadIsoDirFrames($"{FishingIsoFolder}/Fish_{key}_thrash.png");
                Sprite[] held = PersistentCoreBuilder.LoadIsoDirFrames(
                    $"{FishingIsoFolder}/Fish_{key}_{(twoHanded ? "gill" : "tail")}.png");

                float ppu = FirstPpu(dart) ?? FirstPpu(shadow) ?? FirstPpu(thrash) ?? 32f;
                entries.Add(new FishSpeciesVisual
                {
                    FishId = def.Id,
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
                    TwoHanded = twoHanded,
                });
            }
            return entries.ToArray();
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
