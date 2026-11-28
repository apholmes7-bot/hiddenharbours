using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>A clifftop's turf lip, baked from its recipe.</b> Renders a <see cref="CliffTurfLipDef"/>'s strip in a
    /// fresh rig host: the px kit's pixel language, then the Def's painter
    /// (<see cref="CliffTurfLipDef.PainterScriptPath"/>) with the Def's tunables.
    ///
    /// <para><b>Committed, like the kit.</b> The strip lands at <see cref="CliffTurfLipDef.OutputPath"/> as a PNG
    /// (LFS) whose meta GUID is a hash of the file's place (<see cref="StableGuid"/>), written before Unity first
    /// sees the file. It is written only when its pixels differ. Nothing bakes at load;
    /// <c>CliffTurfLipBakeTests</c> holds the committed strip equal to a fresh bake of its Def.</para>
    /// </summary>
    public static class CliffTurfLipBaker
    {
        const string ResultGlobal = "__hhCliffTurfLip";

        /// <summary>What every lip GUID's hash key starts with.</summary>
        public const string GuidNamespace = "hh.cliff-turf-lip/";

        /// <summary>A lip file's GUID: MD5 of the namespace plus its asset path.</summary>
        public static string StableGuid(string assetPath)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(GuidNamespace + assetPath.Replace('\\', '/')));
                var hex = new StringBuilder(32);
                foreach (byte b in hash) hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        /// <summary>A rig host with the pixel language and the lip's painter installed.</summary>
        public static IRigScriptHost CreateHost(CliffTurfLipDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            string lang = Path.Combine(RigCatalog.RepoRoot, CliffCatalog.PxLanguagePath);
            string painter = Path.Combine(RigCatalog.RepoRoot, def.PainterScriptPath);
            foreach (string path in new[] { lang, painter })
                if (!File.Exists(path))
                    throw new FileNotFoundException($"[cliff-lip] {def.Id}: script missing at {path}.", path);
            IRigScriptHost host = RigScriptHostFactory.Create();
            try
            {
                host.Execute(File.ReadAllText(lang) + "\n;\n" + File.ReadAllText(painter));
                if (!host.EvaluateBool("typeof PxCliffTurfLip === 'object' && typeof PxCliffTurfLip.strip === 'function'"))
                    throw new InvalidOperationException($"[cliff-lip] {def.Id}: the painter did not install PxCliffTurfLip.strip().");
                return host;
            }
            catch
            {
                host.Dispose();
                throw;
            }
        }

        /// <summary>Renders the strip: RGBA, top row first.</summary>
        public static byte[] Render(IRigScriptHost host, CliffTurfLipDef def, out int w, out int h)
        {
            host.Execute($"globalThis.{ResultGlobal} = PxCliffTurfLip.strip({def.OptionsJson()});");
            w = (int)host.EvaluateNumber($"{ResultGlobal}.W");
            h = (int)host.EvaluateNumber($"{ResultGlobal}.H");
            if (w != def.StripTexels.x || h != def.StripTexels.y)
                throw new InvalidOperationException($"[cliff-lip] {def.Id}: painted {w}x{h}, the Def asks {def.StripTexels.x}x{def.StripTexels.y}.");
            byte[] rgba = host.EvaluateBytes($"{ResultGlobal}.rgba");
            if (rgba == null || rgba.Length != w * h * 4)
                throw new InvalidOperationException($"[cliff-lip] {def.Id}: the strip is {(rgba == null ? 0 : rgba.Length)} bytes, not {w * h * 4}.");
            return rgba;
        }

        /// <summary>Whether the committed strip decodes to exactly these top-row-first pixels.</summary>
        public static bool MatchesCommitted(CliffTurfLipDef def, byte[] topFirstRgba, int w, int h)
        {
            string abs = Path.Combine(RigCatalog.RepoRoot, def.OutputPath);
            return File.Exists(abs) && CliffBaker.SamePixels(File.ReadAllBytes(abs), topFirstRgba, w, h);
        }

        /// <summary>Bakes a lip, writes it if its pixels changed, imports it as a strip and fills the Def. Returns
        /// whether it wrote the PNG.</summary>
        public static bool Bake(CliffTurfLipDef def)
        {
            byte[] rgba;
            int w, h;
            using (IRigScriptHost host = CreateHost(def)) rgba = Render(host, def, out w, out h);

            string path = def.OutputPath.Replace('\\', '/');
            string abs = Path.Combine(RigCatalog.RepoRoot, path);
            Directory.CreateDirectory(Path.GetDirectoryName(abs));
            WriteFolderMetas(path.Substring(0, path.LastIndexOf('/')));
            string meta = abs + ".meta";
            if (!File.Exists(meta))
                File.WriteAllText(meta, $"fileFormatVersion: 2\nguid: {StableGuid(path)}\n", new UTF8Encoding(false));
            bool wrote = !File.Exists(abs) || !CliffBaker.SamePixels(File.ReadAllBytes(abs), rgba, w, h);
            if (wrote) File.WriteAllBytes(abs, CliffBaker.EncodePng(rgba, w, h));

            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                bool changed = false;
                if (ti.textureShape != TextureImporterShape.Texture2D) { ti.textureShape = TextureImporterShape.Texture2D; changed = true; }
                if (ti.maxTextureSize < Mathf.Max(w, h)) { ti.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(w, h)); changed = true; }
                if (ti.npotScale != TextureImporterNPOTScale.None) { ti.npotScale = TextureImporterNPOTScale.None; changed = true; }
                if (changed) ti.SaveAndReimport();
            }
            CliffBaker.ApplyImportSettings(path, CliffAssetKind.Strip, "");

            var strip = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (strip == null) throw new InvalidOperationException($"[cliff-lip] {def.Id}: the strip did not load at {path}.");
            if (strip.width != w || strip.height != h)
                throw new InvalidOperationException($"[cliff-lip] {def.Id}: the strip imported {strip.width}x{strip.height}, not {w}x{h}: it would be drawn resampled.");
            if (def.Strip != strip)
            {
                def.Strip = strip;
                EditorUtility.SetDirty(def);
            }
            return wrote;
        }

        static void WriteFolderMetas(string folder)
        {
            string f = folder.Replace('\\', '/').TrimEnd('/');
            while (f.Contains("/") && f != "Assets")
            {
                string meta = Path.Combine(RigCatalog.RepoRoot, f + ".meta");
                if (!File.Exists(meta))
                    File.WriteAllText(meta, $"fileFormatVersion: 2\nguid: {StableGuid(f)}\nfolderAsset: yes\n" +
                                            "DefaultImporter:\n  externalObjects: {}\n  userData: \n" +
                                            "  assetBundleName: \n  assetBundleVariant: \n", new UTF8Encoding(false));
                f = f.Substring(0, f.LastIndexOf('/'));
            }
        }

        /// <summary>Bakes every turf lip in the project.</summary>
        [MenuItem("Hidden Harbours/Dev/Bake Cliff Turf Lips")]
        public static void BakeAll()
        {
            int lips = 0, wrote = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(CliffTurfLipDef)))
            {
                var def = AssetDatabase.LoadAssetAtPath<CliffTurfLipDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null) continue;
                lips++;
                if (Bake(def)) wrote++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[cliff-lip] baked {lips} lip(s), wrote {wrote} PNG(s).");
        }

        /// <summary>The batchmode entry: the cliff face styles, then the lips.
        /// <c>-executeMethod HiddenHarbours.Tools.RigBaking.CliffTurfLipBaker.BakeAllBatch</c>.</summary>
        public static void BakeAllBatch()
        {
            try
            {
                CliffSkinPaletteBaker.BakeAll();
                CliffFaceStyleBaker.BakeAll();
                BakeAll();
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
