using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>The coast species' own art</b>: rainbow smelt, Atlantic tomcod and cunner through the SAME
    /// pass-2 fish bake as the seven (every state, three size rungs, the anchors sidecar), plus the
    /// pieces the fish bake does not make — an icon-scale deck lay per new finfish, the green crab's
    /// pot-deck sheet, and the icon of the green crab, the northern quahog and the Atlantic surf clam.
    ///
    /// <para><b>Additive.</b> The rigs are <c>docs/art/rigs/coast-species-kit/Art/coastSpeciesRig.js</c>
    /// over the unmodified pass-2 rigs (whose sources and lists are pinned). The seven pass-2 species'
    /// sheets are not re-rendered here; the anchors sidecar is rewritten for every species so it keeps
    /// describing all of them.</para>
    ///
    /// <para>After a bake, re-run the slicer, Build Fish Icons, Build Catch Item Library and Build Fish
    /// Art Library, as after any fish bake.</para>
    /// </summary>
    public static class CoastSpeciesBaker
    {
        /// <summary>The icon-scale deck lay of each new finfish, which <c>FishIconBuilder</c> trims into
        /// the committed icon. Kept beside the rig (it is a bake input to the icon, not shipped art), so
        /// the fish sheet slicer, which slices every <c>Fish_</c> stem in Iso, never sees it.</summary>
        public const string IconCellFolder = "docs/art/rigs/coast-species-kit/icon-cells";

        /// <summary>The green crab's pot-deck sheet: 8 cells of 48x48 in the rock crab sheet's layout
        /// (still/crawl 0-5, rear, defend), so the pot-deck rule keeps its sprite slots.</summary>
        public const string CrabDeckPath = "Assets/_Project/Art/Fishing/GreenCrabDeck.png";

        /// <summary>Where the shellfish icons land, named by the Def id's full leaf because the last
        /// word (crab, clam) is shared with other species.</summary>
        public const string IconFolder = "Assets/_Project/Art/Fishing/Icons";

        public static string IconCellPath(string species) => $"{IconCellFolder}/FishIconCell_{species}.png";

        public static string ShellIconPath(string leaf) => $"{IconFolder}/ShellIcon_{leaf}.png";

        /// <summary>Def leaf → the rig's shell key.</summary>
        static readonly (string Leaf, string Shell)[] Shells =
        {
            ("northern_quahog", "quahog"),
            ("atlantic_surf_clam", "surfclam"),
        };

        [MenuItem("Hidden Harbours/Art/Bake Coast Species (smelt, tomcod, cunner, green crab, quahog, surf clam)",
                  priority = 51)]
        public static void BakeMenu()
        {
            try
            {
                Bake(out string summary);
                Debug.Log(summary);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh();
        }

        /// <summary>Headless: <c>-executeMethod HiddenHarbours.Tools.RigBaking.CoastSpeciesBaker.BakeCli</c>.</summary>
        public static void BakeCli()
        {
            try
            {
                Bake(out string summary);
                Debug.Log(summary);
                AssetDatabase.Refresh();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CoastSpeciesBaker] bake failed: {ex}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>The whole bake. Throws, with the sidecar untouched, if the fish recipe is wrong.</summary>
        public static void Bake(out string summary)
        {
            var bands = CatchPass2BakeMenu.ResolveWeightBands();
            IReadOnlyList<string> coast = CoastOrder();

            CatchPass2BakeResult fish = CatchPass2Baker.BakeFish(bands, species: coast,
                progress: (l, t) => EditorUtility.DisplayProgressBar("Baking coast species", l, t));
            // BakeFish wrote the sidecar for the three alone; put every species back in it.
            CatchPass2BakeResult anchors = CatchPass2Baker.RewriteFishAnchors(bands);

            var written = new List<string>();
            using (IRigScriptHost host = RigScriptHostFactory.Create())
            {
                RigCatalog.Install(host, RigCatalog.Get(CatchPass2Baker.RigKey));
                RigCatalog.InstallModule(host, RigCatalog.Get("crustacean2"));
                RigCatalog.InstallModule(host, RigCatalog.Get("shellfish2"));
                CatchPass2Baker.InstallCoast(host);

                Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, IconCellFolder));
                foreach (string sp in coast)
                    written.Add(WriteRgba(host, $"CoastSpecies.iconCell({FishingKitBaker.Js(sp)})",
                                          IconCellPath(sp), trim: false));

                written.Add(WriteRgba(host, "CoastSpecies.crabDeckSheet()", CrabDeckPath, trim: false));
                written.Add(WriteRgba(host, "CoastSpecies.greenCrabIcon()", ShellIconPath("green_crab"), trim: true));
                foreach (var (leaf, shell) in Shells)
                    written.Add(WriteRgba(host, $"CoastSpecies.shellIcon({FishingKitBaker.Js(shell)})",
                                          ShellIconPath(leaf), trim: true));
            }

            summary = $"[CoastSpeciesBaker] {fish.Sheets.Count} fish sheet(s) for {string.Join(", ", coast)} " +
                      $"({fish.CellsRendered} cells, {fish.TotalMilliseconds / 1000.0:F1}s); anchors " +
                      $"rewritten for {anchors.Ladders.Count} species; " + string.Join(", ", written) +
                      ". Next: slice, Build Fish Icons, Build Catch Item Library, Build Fish Art Library.";
        }

        /// <summary>The coast kit's own <c>ORDER</c>, read from the rig.</summary>
        public static IReadOnlyList<string> CoastOrder()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigCatalog.Install(host, RigCatalog.Get(CatchPass2Baker.RigKey));
            CatchPass2Baker.InstallCoast(host);
            return FishingKitBaker.ReadStringArray(host, "CoastSpecies.ORDER");
        }

        /// <summary>
        /// Writes one image the rig returns. <paramref name="expr"/> yields either a
        /// <c>Uint8ClampedArray</c> of a 64x80 fish cell or an object <c>{W, H, rgba}</c>; rows are
        /// top-down, as every rig draws. <paramref name="trim"/> cuts to the opaque bounds plus 1 px.
        /// </summary>
        static string WriteRgba(IRigScriptHost host, string expr, string path, bool trim)
        {
            host.Execute($"globalThis.__coastOut = {expr};");
            int w, h;
            byte[] rgba;
            if (host.EvaluateBool("globalThis.__coastOut instanceof Uint8ClampedArray"))
            {
                w = (int)host.EvaluateNumber("FishIso2.W");
                h = (int)host.EvaluateNumber("FishIso2.H");
                rgba = host.EvaluateBytes("globalThis.__coastOut");
            }
            else
            {
                w = (int)host.EvaluateNumber("globalThis.__coastOut.W");
                h = (int)host.EvaluateNumber("globalThis.__coastOut.H");
                rgba = host.EvaluateBytes("globalThis.__coastOut.rgba");
            }
            host.Execute("delete globalThis.__coastOut;");
            if (rgba.Length != w * h * 4)
                throw new InvalidOperationException($"`{expr}` returned {rgba.Length} bytes for {w}x{h}.");

            int x0 = 0, y0 = 0, x1 = w - 1, y1 = h - 1;
            if (trim)
            {
                x0 = w; y0 = h; x1 = -1; y1 = -1;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (rgba[(y * w + x) * 4 + 3] != 0)
                    {
                        x0 = Math.Min(x0, x); x1 = Math.Max(x1, x);
                        y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
                    }
                if (x1 < 0) throw new InvalidOperationException($"`{expr}` drew nothing.");
                x0 = Math.Max(0, x0 - 1); y0 = Math.Max(0, y0 - 1);
                x1 = Math.Min(w - 1, x1 + 1); y1 = Math.Min(h - 1, y1 + 1);
            }

            int ow = x1 - x0 + 1, oh = y1 - y0 + 1;
            var px = new Color32[ow * oh];
            for (int y = 0; y < oh; y++)
            for (int x = 0; x < ow; x++)
            {
                int s = ((y0 + y) * w + (x0 + x)) * 4;
                // Texture rows run bottom-up; the rig's run top-down.
                px[(oh - 1 - y) * ow + x] = new Color32(rgba[s], rgba[s + 1], rgba[s + 2], rgba[s + 3]);
            }

            var tex = new Texture2D(ow, oh, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                tex.SetPixels32(px);
                tex.Apply(false, false);
                File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, path), tex.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
            return $"{Path.GetFileName(path)} {ow}x{oh}";
        }
    }
}
