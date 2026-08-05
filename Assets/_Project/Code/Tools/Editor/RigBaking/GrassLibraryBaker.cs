using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    public sealed class GrassVariantBake
    {
        public string Name, AssetPath, HeightClass;
        public int Width, Height, Climb;
        public long PngBytes;

        public override string ToString() =>
            $"{Name}  {Width}×{Height}  climb {Climb} ({HeightClass})  {PngBytes / 1024.0:F1} KiB";
    }

    public sealed class GrassBakeResult
    {
        public string EngineName;
        public readonly List<GrassVariantBake> Variants = new List<GrassVariantBake>();
        public string ManifestPath;
        public int ManifestEntries;
        public double RenderMilliseconds, TotalMilliseconds;
        public long TotalPngBytes;
    }

    /// <summary>
    /// Bakes the <b>grass library</b> — the habitat set from <c>docs/art/rigs/grassRig.js</c> plus
    /// the art-director species drop it composes on (<c>grassSpeciesRig.js</c>) — into
    /// <see cref="GrassLibraryCatalog.LibraryRoot"/>, and writes the manifest beside it.
    ///
    /// <para><b>⭐ ONE PNG PER VARIANT, NOT A SHEET, AND THAT IS DELIBERATE.</b> Every sibling kit in
    /// this repo bakes atlas sheets and then slices them, and every one of them has been bitten by
    /// the same trap: a fresh Multiple-mode import has EMPTY rects, so a sheet committed before its
    /// slicer ran looks imported and yields no sprites. Grass has no axis to put on a sheet — these
    /// are STATIC single sprites with no facings, no sway frames and no tide states — so there is
    /// nothing to buy with an atlas here except that trap. Single-mode PNGs also match what the
    /// three shipped #102 tufts already are, which keeps the whole library one kind of thing.
    /// Batching is not lost: every tuft draws on the ONE shared <c>Grass.mat</c> (rule 7), and a
    /// Sprite Atlas can be laid over the folder later without touching a line of this.</para>
    ///
    /// <para><b>⚠ THE MANIFEST IS THE PRODUCT, NOT A SIDECAR.</b> Until this kit,
    /// <c>GrassPaintTool</c> and <c>StPetersWoodsPlanter</c> each carried the same three literal
    /// sprite paths, index-aligned to three height toggles. The manifest is what lets both read the
    /// library as data instead — so it is emitted by the SAME rig call that renders the pixels
    /// (<c>GrassRig.manifest()</c>), and a variant's size, climb and height class therefore cannot
    /// drift from its own art. Height class is MEASURED off the bake: climb is not decoration, it is
    /// the sway dial, because the grass shader bends by sprite <c>UV.y²</c>.</para>
    ///
    /// <para><b>⚠ TWO RIGS, IN ORDER.</b> <c>grassRig.js</c> is ours (authored in-repo, by PR — the
    /// tree/shrub/flower precedent) and it COMPOSES on the art-director drop rather than copying its
    /// Bezier-blade renderer. It throws if the drop is not loaded first, so the load order below is
    /// not a convention, it is enforced at the far end.</para>
    ///
    /// <para>Like every sibling baker this stops at "PNGs on disk"; import settings are the
    /// importer's (bottom-centre pivot, PPU 32, point filter — <see cref="ApplyImportSettings"/>).
    /// The three shipped tufts are NOT re-baked and NOT overwritten: they are in the manifest, and
    /// that is all this baker does with them.</para>
    /// </summary>
    public static class GrassLibraryBaker
    {
        public static string DefaultOutputFolder => GrassLibraryCatalog.LibraryRoot.TrimEnd('/');

        /// <summary>The JS global one bake is parked on, so its pixels and its report are read
        /// without rendering twice. Underscored to stay clear of the rig's own namespace.</summary>
        const string ResultGlobal = "__hhGrassBake";

        // =====================================================================================
        // the bake
        // =====================================================================================

        public static GrassBakeResult BakeAll(string outputFolder = null,
                                              Action<string, float> progress = null)
        {
            outputFolder ??= DefaultOutputFolder;
            var total = Stopwatch.StartNew();

            using IRigScriptHost host = RigScriptHostFactory.Create();
            InstallRigs(host);

            var result = new GrassBakeResult { EngineName = host.EngineName };
            string[] names = ReadBakeNames(host);

            string full = Path.Combine(RigCatalog.RepoRoot, outputFolder);
            Directory.CreateDirectory(full);

            var renderClock = new Stopwatch();
            var written = new List<string>();

            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                progress?.Invoke(name, (float)i / names.Length);

                renderClock.Start();
                host.Execute($"globalThis.{ResultGlobal} = {G}.bake({Js(name)});");
                int w = (int)host.EvaluateNumber($"{ResultGlobal}.w");
                int h = (int)host.EvaluateNumber($"{ResultGlobal}.h");
                byte[] rgba = host.EvaluateBytes($"{ResultGlobal}.rgba");
                int climb = (int)host.EvaluateNumber($"{ResultGlobal}.report.climb");
                int detached = (int)host.EvaluateNumber($"{ResultGlobal}.report.detached");
                int rooted = (int)host.EvaluateNumber($"{ResultGlobal}.report.rooted");
                renderClock.Stop();

                AssertShaderContract(name, w, h, rgba, rooted, detached);

                string assetPath = $"{outputFolder}/{name}.png";
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
                try
                {
                    tex.LoadRawTextureData(FlipRows(rgba, w, h));
                    tex.Apply(false, false);
                    byte[] png = tex.EncodeToPNG();
                    File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, assetPath), png);
                    result.TotalPngBytes += png.Length;

                    result.Variants.Add(new GrassVariantBake
                    {
                        Name = name, AssetPath = assetPath, Width = w, Height = h,
                        Climb = climb, PngBytes = png.Length,
                        HeightClass = host.EvaluateString($"{G}.classOf({climb})"),
                    });
                    written.Add(assetPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }

            // The manifest comes from the SAME rig that just rendered the pixels, so the two cannot
            // disagree — it is written last so a bake that threw leaves no manifest describing
            // sprites that were never produced.
            string manifestJson = host.EvaluateString(
                $"JSON.stringify({G}.manifest({Js(outputFolder)}), null, 2)");
            string manifestPath = $"{outputFolder}/{GrassLibraryCatalog.ManifestFileName}";
            File.WriteAllText(Path.Combine(RigCatalog.RepoRoot, manifestPath), manifestJson + "\n");
            result.ManifestPath = manifestPath;
            result.ManifestEntries =
                (int)host.EvaluateNumber($"Object.keys({G}.manifest({Js(outputFolder)}).variants).length");

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        // =====================================================================================
        // the rigs
        // =====================================================================================

        const string G = GrassLibraryCatalog.RigGlobalName;

        /// <summary>
        /// Loads the art-director drop, then our habitat rig on top of it — <b>in that order</b>.
        /// <c>grassRig.js</c> throws on load if the drop is missing, so the order is enforced at the
        /// far end rather than trusted here; this method's job is to give that throw a message the
        /// owner can act on.
        /// </summary>
        public static void InstallRigs(IRigScriptHost host)
        {
            foreach (string rel in new[] { GrassLibraryCatalog.SpeciesRigPath,
                                           GrassLibraryCatalog.RigScriptPath })
            {
                string path = Path.Combine(RigCatalog.RepoRoot, rel);
                if (!File.Exists(path))
                    throw new FileNotFoundException(
                        $"Grass rig source missing at {path}. Both rigs are committed under " +
                        "docs/art/rigs/ — if this fired, the branch predates that import.", path);
                host.Execute(File.ReadAllText(path));
            }

            if (!host.EvaluateBool($"typeof {G} === 'object' && {G} !== null"))
                throw new InvalidOperationException(
                    $"'{GrassLibraryCatalog.RigScriptPath}' ran but did not install globalThis.{G} " +
                    "— the rig changed shape.");

            // Asserted up front rather than discovered mid-bake, where a renamed export would
            // surface as a confusing type error on variant 20.
            foreach (string fn in new[] { "render", "toRGBA", "validate", "bake", "bakeSpecies",
                                          "bakeAll", "manifest", "classOf" })
                if (!host.EvaluateBool($"typeof {G}.{fn} === 'function'"))
                    throw new InvalidOperationException(
                        $"{G}.{fn}() is missing — this baker calls the rig's public API directly " +
                        "(no shim), so a renamed export must fail loudly here.");

            foreach (string arr in new[] { "VARIANTS", "SHIPPED" })
                if (!host.EvaluateBool($"Array.isArray({G}.{arr})"))
                    throw new InvalidOperationException($"{G}.{arr} is missing — the rig changed shape.");
        }

        /// <summary>Every variant the rig will WRITE, in its own order — the habitat set plus the
        /// species drop. The three shipped tufts are deliberately absent: they are described by the
        /// manifest and never re-rendered.</summary>
        public static string[] ReadBakeNames(IRigScriptHost host) =>
            ShorePlantBaker.ReadStringArray(host, $"{G}.bakeAll().map(b => b.name)");

        // =====================================================================================
        // the contract
        // =====================================================================================

        /// <summary>
        /// 🔴 The four things <c>HiddenHarboursGrass.shader</c> cannot survive being wrong, checked
        /// on the actual pixels before anything reaches disk. The shader bends tips in the VERTEX
        /// stage weighted by sprite <c>UV.y²</c> — 0 at the bottom edge, 1 at the top — so:
        /// a blade not rooted on the bottom row SHEARS OFF in wind; a detached pixel flies away on
        /// its own; soft alpha fringes at point filtering; and a width off the 32 grid breaks the
        /// PPU-32 pivot. A bake that fails here writes NOTHING, rather than a field of grass that
        /// only comes apart once the wind picks up.
        /// </summary>
        public static void AssertShaderContract(string name, int w, int h, byte[] rgba,
                                                int rooted, int detached)
        {
            int expected = w * h * 4;
            if (rgba.Length != expected)
                throw new InvalidOperationException(
                    $"[grass-baker] {name}: rgba came back {rgba.Length} bytes, expected {expected} " +
                    $"for {w}×{h} RGBA. The canvas the rig rendered is not the one it declared.");

            if (w % GrassLibraryCatalog.Ppu != 0)
                throw new InvalidOperationException(
                    $"[grass-baker] {name}: width {w} is not a multiple of {GrassLibraryCatalog.Ppu}. " +
                    "Canvas widths are whole metres so the bottom-centre pivot lands on the grid.");

            if (rooted <= 0)
                throw new InvalidOperationException(
                    $"[grass-baker] {name}: NOTHING on the bottom row. The shader's bend weight is " +
                    "0 at the bottom edge and 1 at the top, so a tuft that does not touch its own " +
                    "bottom row is a tuft that shears off the ground the first time the wind blows.");

            if (detached != 0)
                throw new InvalidOperationException(
                    $"[grass-baker] {name}: {detached} pixel(s) not 8-connected to the bottom edge. " +
                    "Under the vertex bend those fly away on their own.");

            for (int i = 3; i < rgba.Length; i += 4)
                if (rgba[i] != 0 && rgba[i] != 255)
                    throw new InvalidOperationException(
                        $"[grass-baker] {name}: soft alpha {rgba[i]} at byte {i}. Pixel art imports " +
                        "at point filtering; a partial alpha is a grey fringe on every blade.");
        }

        /// <summary>
        /// A rig renders bottom-up (row 0 at the BOTTOM, the natural frame for a plant rooted on its
        /// bottom edge) and <see cref="Texture2D.LoadRawTextureData(byte[])"/> reads bottom-up too,
        /// but <c>render()</c> hands back a buffer whose row 0 is the TOP — the canvas convention.
        /// One flip, in one place, so the answer is not re-derived per baker.
        /// </summary>
        static byte[] FlipRows(byte[] rgba, int w, int h)
        {
            var flipped = new byte[rgba.Length];
            int stride = w * 4;
            for (int y = 0; y < h; y++)
                Buffer.BlockCopy(rgba, y * stride, flipped, (h - 1 - y) * stride, stride);
            return flipped;
        }

        // =====================================================================================
        // import settings
        // =====================================================================================

        /// <summary>
        /// The import contract for every grass sprite: <b>bottom-centre pivot</b> (the tuft's root
        /// is where it stands), PPU 32, point filter, no compression on the default platform, hard
        /// alpha preserved. Applied by path, so the shipped three and the baked twenty-six are the
        /// same kind of asset.
        ///
        /// <para><b>⚠ Alignment 7 is not cosmetic.</b> A centre pivot would sink every tuft half its
        /// own height into the ground, and the error scales with the sprite — a 48 px marram would
        /// bury 24 px, a 32 px sward 16, so the field would look mis-scattered rather than
        /// mis-pivoted. It is also what makes the shader's UV.y² bend read as "blades lean, root
        /// stays put".</para>
        /// </summary>
        public static bool ApplyImportSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return false;

            bool changed = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            if (importer.textureType != TextureImporterType.Sprite)
            { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, GrassLibraryCatalog.Ppu))
            { importer.spritePixelsPerUnit = GrassLibraryCatalog.Ppu; changed = true; }
            if (importer.filterMode != FilterMode.Point)
            { importer.filterMode = FilterMode.Point; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp)
            { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }

            int wantAlign = (int)SpriteAlignment.BottomCenter;
            if (settings.spriteAlignment != wantAlign ||
                settings.spritePivot != new Vector2(0.5f, 0f))
            {
                settings.spriteAlignment = wantAlign;
                settings.spritePivot = new Vector2(0.5f, 0f);
                importer.SetTextureSettings(settings);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
            return changed;
        }

        static string Js(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
    }
}
