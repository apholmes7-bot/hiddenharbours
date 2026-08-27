#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The <b>grass library</b> — the schema of
    /// <c>Assets/_Project/Art/Sprites/Grass/GrassLibrary.json</c> and the ONE place anything
    /// downstream learns which grass sprites exist, how tall they stand, and what ground they
    /// belong on.
    ///
    /// <para><b>⭐ THIS EXISTS TO KILL A HARD-CODED LIST.</b> Until now
    /// <see cref="GrassPaintTool"/> carried three literal sprite paths, index-aligned to its three
    /// height toggles, and <c>StPetersWoodsPlanter</c> carried the same three again. Adding a
    /// variant meant editing both, in lockstep, and getting the index right — and a field that has
    /// to cover most of an island needs a great deal more than three silhouettes before it stops
    /// reading as wallpaper. So the library is DATA: the rigs bake it, the manifest describes it,
    /// and both consumers read this. Adding a variant is now a bake, not a code change.</para>
    ///
    /// <para><b>The manifest is generated, never hand-edited.</b> <c>docs/art/rigs/grassRig.js</c>
    /// emits it from the same call that renders the pixels, so the two cannot disagree about a
    /// variant's size, climb or height class. <see cref="GrassLibraryBaker"/> writes both in one
    /// pass. If you want a variant retagged, retag it in the rig and re-bake.</para>
    ///
    /// <para><b>Height class is MEASURED, not asserted.</b> A variant's <see cref="Entry.Climb"/> is
    /// how far its blades rise off the bottom edge in the baked pixels, and the rig files it into
    /// short / medium / tall on that number. This matters because climb is not decoration: the
    /// grass shader bends by sprite <c>UV.y²</c>, so <b>how far a variant climbs its canvas IS how
    /// much wind it takes</b>. A "tall" sprite that only climbs 12 px would sit dead still in a
    /// gale, and filing it by eye is how that ships.</para>
    ///
    /// <para><b>Lane &amp; rules.</b> art-pipeline authoring data (Art.Editor); editor-only, nothing
    /// saved at runtime (rule 5); no tunable restated here (rule 6) — every number comes from the
    /// manifest. The three shipped #102 tufts appear in the library alongside the baked ones and
    /// carry their own paths, because they live one directory up and are NOT moving.</para>
    /// </summary>
    public static class GrassLibraryCatalog
    {
        /// <summary>Where the baked library lands, and where the manifest lives beside it.</summary>
        public const string LibraryRoot = "Assets/_Project/Art/Sprites/Grass/";

        public const string ManifestFileName = "GrassLibrary.json";

        public static string ManifestPath => LibraryRoot + ManifestFileName;

        /// <summary>The rigs this library bakes from. The second is an art-director drop and is
        /// read-only from here; the first is ours, and composes on it.</summary>
        public const string SpeciesRigPath = "docs/art/rigs/grassSpeciesRig.js";

        public const string RigScriptPath = "docs/art/rigs/grassRig.js";

        public const string RigGlobalName = "GrassRig";

        /// <summary>What the manifest calls itself, pinned so a different rig cannot land here
        /// unnoticed.</summary>
        public const string RigName = "grassRig";

        /// <summary>32 px = 1 m, the locked scale (ADR 0006/0022).</summary>
        public const int Ppu = 32;

        /// <summary>The one material every tuft shares, so hundreds of them batch (rule 7).</summary>
        public const string GrassMaterialPath = "Assets/_Project/Art/Materials/Grass.mat";

        /// <summary>The three height classes the rig files a variant into off its measured
        /// <see cref="Entry.Climb"/>. Named rather than left as literals because the meadow's edge band
        /// speaks about them BY NAME (its step-down goes interior → mid → short), and a typo in one of
        /// those narrows a pool to nothing without failing.</summary>
        public const string HeightShort = "short";

        /// <inheritdoc cref="HeightShort"/>
        public const string HeightMedium = "medium";

        /// <inheritdoc cref="HeightShort"/>
        public const string HeightTall = "tall";

        /// <summary>The height classes, in the order the paint tool's toggles read — and, not by
        /// accident, shortest first.</summary>
        public static readonly string[] HeightClasses = { HeightShort, HeightMedium, HeightTall };

        /// <summary>
        /// The blade ramp, dark roots → light tips. Restated here for ONE purpose: so
        /// <c>GrassLibraryContractTests</c> can assert the committed manifest still declares it.
        /// Nothing renders from this — the pixels come from the rig.
        /// </summary>
        public static readonly string[] BaseRamp =
            { "#283a22", "#3a542a", "#567834", "#7ca248", "#aac660" };

        // =====================================================================================
        // the schema
        // =====================================================================================

        /// <summary>One grass sprite in the library.</summary>
        public sealed class Entry
        {
            /// <summary>Sprite/file stem — <c>GrassTuft_MeadowMidA</c>. Stable; the manifest key.</summary>
            public string Name;

            /// <summary>Asset path of the PNG. Carried per entry rather than composed from
            /// <see cref="LibraryRoot"/> because the three shipped tufts live elsewhere.</summary>
            public string Path;

            public int Width, Height;

            /// <summary>The species ramp this variant is painted on — <c>grass</c> for everything
            /// this project bakes itself, plus the drop's five marsh/meadow species.</summary>
            public string Species;

            /// <summary>Where it came from: <c>shipped</c> (the #102 three) · <c>habitat</c>
            /// (grassRig.js) · <c>species</c> (the art-director drop).</summary>
            public string Source;

            /// <summary>short · medium · tall — measured off <see cref="Climb"/> by the rig.</summary>
            public string HeightClass;

            /// <summary>What ground this belongs on: meadow, sward, fringe, dune, headland, verge,
            /// marsh, wet, saltmarsh, hay. A scatter pass matches on these and never on a name.</summary>
            public readonly List<string> Habitat = new List<string>();

            /// <summary>How far the blades rise off the bottom edge, in pixels. ALSO the sway
            /// dial — see the class remarks.</summary>
            public int Climb;

            /// <summary>How much of the shader's bend this sprite should take (1 = a normal tuft).
            /// Carried from the rig; no consumer reads it yet — a later per-variant bend multiplier
            /// is what it is for.</summary>
            public float Stiffness = 1f;

            public string Note;

            public bool HasHabitat(string tag) =>
                Habitat.Any(h => string.Equals(h, tag, StringComparison.OrdinalIgnoreCase));

            public override string ToString() =>
                $"{Name} {Width}×{Height} {HeightClass} climb {Climb} [{string.Join(",", Habitat)}]";
        }

        public sealed class Library
        {
            public string Rig, Composes, OutputFolder, Note;
            public int Ppu;
            public readonly List<string> BaseRamp = new List<string>();
            public readonly List<Entry> Entries = new List<Entry>();

            /// <summary>habitat tag → the prose the rig documents it with. Shown in the tool.</summary>
            public readonly Dictionary<string, string> Habitats =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// species key → its five colours, dark roots to light tips. Every species ramp is the
            /// BASE ramp with the hue rotated and the saturation scaled at <b>identical
            /// lightness</b>, which is what lets the runtime lush→straw knob multiply over all of
            /// them coherently: sedge stays yellower than rush at every point on the knob. A variant
            /// painted off its own species' ramp becomes a colour island the moment a field is
            /// tinted, so <c>GrassLibraryContractTests</c> checks every committed pixel against this.
            /// </summary>
            public readonly Dictionary<string, List<string>> SpeciesRamps =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            /// <summary>The off-ramp accent the cattail spike is allowed (and nothing else is).</summary>
            public readonly List<string> SpikeRamp = new List<string>();

            /// <summary>Every colour an entry of this species may legally use.</summary>
            public IEnumerable<string> LegalColours(string species)
            {
                if (SpeciesRamps.TryGetValue(species ?? "", out var r))
                    foreach (var c in r) yield return c;
                foreach (var c in SpikeRamp) yield return c;
            }

            public Entry Find(string name) =>
                Entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

            /// <summary>Every entry of a height class, in manifest order.</summary>
            public IEnumerable<Entry> OfClass(string heightClass) =>
                Entries.Where(e => string.Equals(e.HeightClass, heightClass,
                                                 StringComparison.OrdinalIgnoreCase));

            /// <summary>Every entry carrying a habitat tag, in manifest order.</summary>
            public IEnumerable<Entry> OfHabitat(string tag) => Entries.Where(e => e.HasHabitat(tag));

            /// <summary>
            /// The entries a scatter or a brush should actually choose between: those matching ANY
            /// of <paramref name="habitats"/> AND any of <paramref name="heightClasses"/>. Both
            /// filters are skipped when their list is null/empty, so "all grass" is the natural
            /// no-argument answer. Falls back to the whole library rather than returning nothing —
            /// an over-narrow filter must not paint an empty field silently.
            /// </summary>
            public List<Entry> Choose(IReadOnlyList<string> habitats,
                                      IReadOnlyList<string> heightClasses)
            {
                bool AnyHabitat(Entry e) =>
                    habitats == null || habitats.Count == 0 || habitats.Any(e.HasHabitat);
                bool AnyClass(Entry e) =>
                    heightClasses == null || heightClasses.Count == 0 ||
                    heightClasses.Any(c => string.Equals(c, e.HeightClass,
                                                         StringComparison.OrdinalIgnoreCase));

                var hit = Entries.Where(e => AnyHabitat(e) && AnyClass(e)).ToList();
                return hit.Count > 0 ? hit : new List<Entry>(Entries);
            }
        }

        // =====================================================================================
        // loading
        // =====================================================================================

        /// <summary>
        /// Reads the committed manifest. Returns null (with a console error) rather than throwing,
        /// so a tool window can show the owner a "re-bake the grass library" message instead of
        /// dying — the same shape <see cref="ShorePlantCatalog.Load"/> takes.
        ///
        /// <para>Parsed with <see cref="MiniJson"/>, not <c>JsonUtility</c>: <c>variants</c> is a map
        /// keyed by variant name, which <c>JsonUtility</c> cannot read.</para>
        /// </summary>
        public static Library Load()
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError(
                    $"[GrassLibraryCatalog] No manifest at '{ManifestPath}'. It is baked from " +
                    $"'{RigScriptPath}' — run Hidden Harbours ▸ Dev ▸ Bake Grass Library.");
                return null;
            }

            object root;
            try { root = MiniJson.Parse(File.ReadAllText(ManifestPath)); }
            catch (FormatException e)
            {
                Debug.LogError($"[GrassLibraryCatalog] '{ManifestPath}' is not valid JSON: {e.Message}");
                return null;
            }

            if (!(root is Dictionary<string, object> d))
            {
                Debug.LogError($"[GrassLibraryCatalog] '{ManifestPath}' did not parse to an object.");
                return null;
            }

            var lib = new Library
            {
                Rig = Str(d, "rig"),
                Composes = Str(d, "composes"),
                OutputFolder = Str(d, "outputFolder"),
                Note = Str(d, "note"),
                Ppu = MiniJson.Int(d, "ppu", Ppu),
            };

            if (lib.Rig != RigName)
            {
                Debug.LogError(
                    $"[GrassLibraryCatalog] '{ManifestPath}' calls itself '{lib.Rig}', expected " +
                    $"'{RigName}'. A different rig's manifest has landed here.");
                return null;
            }

            foreach (var s in MiniJson.List(d, "baseRamp") ?? new List<object>())
                lib.BaseRamp.Add(Convert.ToString(s));
            foreach (var s in MiniJson.List(d, "spikeRamp") ?? new List<object>())
                lib.SpikeRamp.Add(Convert.ToString(s));

            var species = MiniJson.Dict(d, "species");
            if (species != null)
                foreach (var kv in species)
                {
                    if (!(kv.Value is Dictionary<string, object> sv)) continue;
                    var ramp = new List<string>();
                    foreach (var c in MiniJson.List(sv, "ramp") ?? new List<object>())
                        ramp.Add(Convert.ToString(c));
                    lib.SpeciesRamps[kv.Key] = ramp;
                }

            var habitats = MiniJson.Dict(d, "habitats");
            if (habitats != null)
                foreach (var kv in habitats) lib.Habitats[kv.Key] = Convert.ToString(kv.Value);

            var variants = MiniJson.Dict(d, "variants");
            if (variants == null || variants.Count == 0)
            {
                Debug.LogError($"[GrassLibraryCatalog] '{ManifestPath}' declares no variants.");
                return null;
            }

            foreach (var kv in variants)
            {
                if (!(kv.Value is Dictionary<string, object> v)) continue;
                var e = new Entry
                {
                    Name = kv.Key,
                    Path = Str(v, "path"),
                    Width = MiniJson.Int(v, "w"),
                    Height = MiniJson.Int(v, "h"),
                    Species = Str(v, "species"),
                    Source = Str(v, "source"),
                    HeightClass = Str(v, "heightClass"),
                    Climb = MiniJson.Int(v, "climb"),
                    Stiffness = MiniJson.Float(v, "stiffness", 1f),
                    Note = Str(v, "note"),
                };
                foreach (var h in MiniJson.List(v, "habitat") ?? new List<object>())
                    e.Habitat.Add(Convert.ToString(h));
                lib.Entries.Add(e);
            }

            return lib;
        }

        /// <summary>
        /// Loads the sprite for an entry. <b>Name-matched, never "first"</b> — on a Multiple-mode
        /// re-import sub-sprite enumeration order is unstable (the sprite-refs trap), and "first"
        /// would quietly change which blade a whole field is drawn with. Falls back to the single
        /// asset only when the name misses.
        /// </summary>
        public static Sprite LoadSprite(Entry e)
        {
            if (e == null || string.IsNullOrEmpty(e.Path)) return null;
            var all = AssetDatabase.LoadAllAssetsAtPath(e.Path).OfType<Sprite>().ToArray();
            if (all.Length == 0) return null;
            // == / != on purpose, never ?? — coalescing skips UnityEngine.Object's overload and
            // resurrects fake-null.
            Sprite named = all.FirstOrDefault(s => s.name == e.Name);
            return named != null ? named : all[0];
        }

        /// <summary>
        /// The library with every entry whose PNG is actually imported, and nothing else. An entry
        /// the owner has not pulled through LFS (or a bake that has not run) drops out here rather
        /// than surfacing later as a null sprite on a painted tuft.
        /// </summary>
        public static List<Entry> Imported(Library lib) =>
            lib == null ? new List<Entry>()
                        : lib.Entries.Where(e => LoadSprite(e) != null).ToList();

        static string Str(Dictionary<string, object> d, string key) =>
            d != null && d.TryGetValue(key, out var v) ? Convert.ToString(v) : null;
    }
}
#endif
