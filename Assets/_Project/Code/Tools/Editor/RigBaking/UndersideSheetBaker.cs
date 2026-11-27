using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>The underside sheet: what the calm water shows of a deck seen from below.</b> The underside rig
    /// (<c>docs/art/rigs/underside-kit/undersideRig.js</c>) paints every piece as a flat pixel tile at
    /// 32 px/m and stacks them, top-down in its <c>PIECES</c> order, on one sheet, so the calm mirror binds a
    /// single texture whatever is in view. Each <c>UndersideDef</c> names its cell in Unity's bottom-up rows
    /// (<see cref="UnityCell"/>).
    ///
    /// <para><b>Host code, not a catalog entry.</b> The rig needs no turntable, so this runs its source
    /// unmodified in the shared V8 host. <c>UndersideArtTests</c> re-renders the sheet this way and holds the
    /// committed PNG to it pixel for pixel, and each Def's cell to the rig's.</para>
    /// </summary>
    public static class UndersideSheetBaker
    {
        public const string RigPath = "docs/art/rigs/underside-kit/undersideRig.js";
        public const string GlobalName = "Underside";
        public const string SheetPath = "Assets/_Project/Art/Textures/Water/Undersides.png";

        /// <summary>Install the rig into <paramref name="host"/>.</summary>
        public static void Install(IRigScriptHost host)
        {
            string full = Path.Combine(RigCatalog.RepoRoot, RigPath);
            if (!File.Exists(full)) throw new FileNotFoundException("The underside rig is missing.", full);
            host.Execute(File.ReadAllText(full));
            if (!host.EvaluateBool($"typeof {GlobalName} === 'object' && {GlobalName} !== null"))
                throw new InvalidOperationException($"{RigPath} ran but did not install globalThis.{GlobalName}.");
        }

        /// <summary>How many pieces the rig paints.</summary>
        public static int PieceCount(IRigScriptHost host) => (int)host.EvaluateNumber($"{GlobalName}.PIECES.length");

        /// <summary>Piece <paramref name="i"/>'s id, as its UndersideDef must carry it.</summary>
        public static string PieceId(IRigScriptHost host, int i) => host.EvaluateString($"{GlobalName}.PIECES[{i}].id");

        /// <summary>Piece <paramref name="i"/>'s fascia rows.</summary>
        public static int PieceFascia(IRigScriptHost host, int i) => (int)host.EvaluateNumber($"{GlobalName}.PIECES[{i}].fascia");

        /// <summary>Piece <paramref name="i"/>'s cell in Unity's bottom-up rows (the rig stacks top-down).</summary>
        public static RectInt UnityCell(IRigScriptHost host, int i)
        {
            int sheetH = (int)host.EvaluateNumber($"{GlobalName}.H");
            int x = (int)host.EvaluateNumber($"{GlobalName}.PIECES[{i}].x");
            int y = (int)host.EvaluateNumber($"{GlobalName}.PIECES[{i}].y");
            int w = (int)host.EvaluateNumber($"{GlobalName}.PIECES[{i}].w");
            int h = (int)host.EvaluateNumber($"{GlobalName}.PIECES[{i}].h");
            return new RectInt(x, sheetH - (y + h), w, h);
        }

        /// <summary>
        /// Render the whole sheet as Unity pixels (rows BOTTOM-up, as <see cref="Texture2D"/> stores them; the
        /// rig renders top-down). The host must already hold the rig (<see cref="Install"/>).
        /// </summary>
        public static Color32[] RenderSheet(IRigScriptHost host, out int width, out int height)
        {
            width = (int)host.EvaluateNumber($"{GlobalName}.W");
            height = (int)host.EvaluateNumber($"{GlobalName}.H");
            byte[] rgba = host.EvaluateBytes($"{GlobalName}.sheet()");
            if (rgba.Length != width * height * 4)
                throw new InvalidOperationException($"The underside sheet came back {rgba.Length} bytes for {width}x{height}.");

            var px = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int dst = (height - 1 - y) * width;
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    px[dst + x] = new Color32(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]);
                }
            }
            return px;
        }

        [MenuItem("Hidden Harbours/Art/Bake Undersides (piers, floats, gangways seen from below)", priority = 70)]
        public static void BakeMenu()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            Install(host);
            Color32[] px = RenderSheet(host, out int w, out int h);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                tex.SetPixels32(px);
                tex.Apply(false, false);
                File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, SheetPath), tex.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[UndersideSheetBaker] baked {SheetPath} ({w}x{h}, {PieceCount(host)} pieces) on {host.EngineName}");
        }
    }
}
