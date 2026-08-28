using System;
using System.Collections.Generic;
using System.IO;
using HiddenHarbours.World;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One baked piece, as reported back to the menu and the tests.</summary>
    public sealed class BubbleKitPieceResult
    {
        public string Name, AssetPath, Expression;
        public int Width, Height, OpaquePixels, PngBytes;
        public bool Rewritten;

        public override string ToString() =>
            $"{Name,-24} {Width,3}×{Height,-3} {OpaquePixels,5} px ink  " +
            $"{PngBytes,6:N0} B  {(Rewritten ? "written" : "unchanged")}";
    }

    public sealed class BubbleKitBakeReport
    {
        public string EngineName;
        public readonly List<BubbleKitPieceResult> Pieces = new List<BubbleKitPieceResult>();
        public double RenderMilliseconds, TotalMilliseconds;
        public int RewrittenCount;
    }

    /// <summary>
    /// <b>THE BUBBLE KIT, BAKED.</b> Runs <c>dialogueBubbleRig.js</c> through the repo's V8 host and
    /// writes one PNG per piece into <see cref="DialoguePresenter.ArtFolder"/>.
    ///
    /// <para><b>Why this is a BAKE and not an import.</b> The handoff expected to copy PNGs out of
    /// <c>docs/</c> and set importer settings on them. There are no PNGs in <c>docs/</c>: this kit
    /// ships <i>nothing but source</i> and says so — "every piece is live-drawn from the rig at
    /// native resolution, exactly like the wall calendar's face. The PNG exports on the harness exist
    /// for review and for slice cutting, not as the shipping art path." So the shipping art path is
    /// the one every other rig in this repo already uses (ADR 0021): run the art director's file,
    /// unmodified, and rasterise it here. Nothing is transcribed and nothing is hand-exported.</para>
    ///
    /// <para><b>The canvas shim, and how it differs from its predecessor.</b> Bare V8 has no DOM, and
    /// the rig must run UNMODIFIED (ADR 0021 §5), so <see cref="CanvasShimJs"/> — host-side code,
    /// executed before the rig — supplies what it touches. <see cref="CatchStorageBaker.CanvasShimJs"/>
    /// is a <i>mailbox</i>: <c>catchKit.js</c> composes its own pixels and only wants somewhere to
    /// leave them. This kit genuinely DRAWS, so this shim genuinely RASTERISES. It is the first shim
    /// in the repo that does. It stays cheap because the whole surface the rigs touch is two members
    /// — <c>fillStyle</c> and <c>fillRect</c> — measured across both rig files, not assumed.</para>
    ///
    /// <para><b>Two pieces are 9-SLICE TILES, not instances.</b> The rig draws the panel and the gold
    /// chip/pill at whatever size the content needs, so baking one fixed instance of either would
    /// pin the bubble to one sentence. <see cref="DialogueBubbleKit.PiecePanel"/> bakes the kit's own
    /// declared 18×18 slice tile; <see cref="DialogueBubbleKit.PieceGold"/> bakes the smallest square
    /// carrying a chamfer-1 panel's full vocabulary (cap · light · paper · shade · cap).</para>
    ///
    /// <para><b>Idempotent.</b> A piece whose bytes match what is already on disk is not rewritten,
    /// so a re-run converges and leaves the working tree clean — no <c>.meta</c> churn, no diff noise
    /// for the owner to wade through (the #553 lesson, and the same rule
    /// <see cref="HiddenHarbours.Art.Editor.SpriteSheetSlicer"/> follows for sprite IDs).</para>
    ///
    /// <para>Stops at "PNGs on disk". Import settings and 9-slice borders are
    /// <see cref="BubbleKitImporter"/>'s job.</para>
    /// </summary>
    public static class BubbleKitBaker
    {
        /// <summary>The catalog key for the bubble rig.</summary>
        public const string RigKey = "dialogueBubble";

        /// <summary>The catalog key for the talk-cue rig (the mouth probe's home).</summary>
        public const string TalkCueRigKey = "talkCue";

        /// <summary>
        /// Host-side environment shim (see class remarks). Provides <c>OffscreenCanvas</c> and
        /// <c>document.createElement('canvas')</c> — the rig's <c>canvasFor()</c> prefers
        /// <c>document</c> when it exists, so BOTH must rasterise or the choice of path would change
        /// the output. Defined behind its own marker so re-running it is a no-op, and throwing on any
        /// unsupported <c>fillStyle</c> or element so a future rig change is loud rather than blank.
        ///
        /// <para>⚠️ Never merge this with <see cref="CatchStorageBaker.CanvasShimJs"/>. That one
        /// installs a <c>document</c> whose context has no <c>fillRect</c>; if both ran in one host,
        /// whichever landed first would decide whether this kit drew or silently produced nothing.
        /// They are deliberately separate, in separate hosts.</para>
        /// </summary>
        public const string CanvasShimJs = @"
(function (g) {
  if (g.__hhBubbleCanvasShim) return;
  g.__hhBubbleCanvasShim = 1;

  function parse(css) {
    if (typeof css !== 'string') return null;
    var s = css.trim(), m = /^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/.exec(s);
    if (m) {
      var h = m[1];
      if (h.length === 3) h = h[0]+h[0]+h[1]+h[1]+h[2]+h[2];
      return [parseInt(h.slice(0,2),16), parseInt(h.slice(2,4),16), parseInt(h.slice(4,6),16), 255];
    }
    var r = /^rgba?\(([^)]+)\)$/.exec(s);
    if (r) {
      var p = r[1].split(',');
      return [parseInt(p[0],10)|0, parseInt(p[1],10)|0, parseInt(p[2],10)|0,
              p.length > 3 ? Math.round(parseFloat(p[3]) * 255) : 255];
    }
    return null;
  }

  function Ctx(cv) { this._cv = cv; this.fillStyle = '#000000'; }
  Ctx.prototype.fillRect = function (x, y, w, h) {
    var cv = this._cv;
    var x0 = Math.round(x), y0 = Math.round(y), x1 = Math.round(x + w), y1 = Math.round(y + h), t;
    if (x1 < x0) { t = x0; x0 = x1; x1 = t; }
    if (y1 < y0) { t = y0; y0 = y1; y1 = t; }
    var col = parse(this.fillStyle);
    if (!col) throw new Error('bubble canvas shim: unsupported fillStyle ' + this.fillStyle);
    var xa = Math.max(0, x0), xb = Math.min(cv.width, x1);
    var ya = Math.max(0, y0), yb = Math.min(cv.height, y1);
    for (var py = ya; py < yb; py++) {
      var i = (py * cv.width + xa) * 4;
      for (var px = xa; px < xb; px++) {
        cv.data[i] = col[0]; cv.data[i+1] = col[1]; cv.data[i+2] = col[2]; cv.data[i+3] = col[3];
        i += 4;
      }
    }
  };

  function alloc(cv) {
    cv.data = new Uint8ClampedArray(Math.max(0, cv.width * cv.height * 4));
    cv._ctx = null;
  }
  function Canvas(w, h) {
    this.width = w | 0; this.height = h | 0; alloc(this);
  }
  Canvas.prototype.getContext = function (kind) {
    if (kind !== '2d') throw new Error('bubble canvas shim: only 2d, got ' + kind);
    // The rig sets width/height AFTER construction, so size the buffer on first use.
    if (!this.data || this.data.length !== this.width * this.height * 4) alloc(this);
    if (!this._ctx) this._ctx = new Ctx(this);
    return this._ctx;
  };

  g.OffscreenCanvas = Canvas;
  g.document = {
    createElement: function (tag) {
      if (String(tag).toLowerCase() !== 'canvas')
        throw new Error('bubble canvas shim provides only <canvas>, got <' + tag + '>');
      return new Canvas(0, 0);
    }
  };
})(globalThis);
";

        /// <summary>The global the shim parks each rendered piece on, so width/height/data are three
        /// cheap evaluations against one render rather than three renders.</summary>
        const string Slot = "globalThis.__hhBubblePiece";

        /// <summary>
        /// Every piece, as (name, the JS that produces its canvas). Held as ONE table because the
        /// bake, the size cross-check and the importer all have to agree about what exists; a second
        /// list somewhere else is how a piece gets baked and never imported.
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, string>> PieceExpressions(string stock)
        {
            string s = Js(stock);
            var list = new List<KeyValuePair<string, string>>
            {
                // The kit's own declared 9-slice source tile.
                new KeyValuePair<string, string>(DialogueBubbleKit.PiecePanel,
                    $"BubbleKit.render('slice',{{stock:{s}}})"),

                // The gold tile — chip() IS panel() with the gold ramp at chamfer 1, and the
                // selected-row pill is the same call at row height, so one tile dresses both.
                new KeyValuePair<string, string>(DialogueBubbleKit.PieceGold,
                    $"(function(){{var n={DialogueBubbleKit.GoldTileSize};" +
                    "var cv=new OffscreenCanvas(n,n);" +
                    "BubbleKit.panel(cv.getContext('2d'),0,0,n,n,{chamfer:BubbleKit.M.opt.pillChamfer," +
                    "paper:BubbleKit.GOLD[2],hi:BubbleKit.GOLD[3],lo:BubbleKit.GOLD[1]," +
                    $"edge:BubbleKit.STOCKS[{s}].ink}});return cv;}})()"),
            };

            for (int i = 0; i < DialogueBubbleKit.TailPieces.Length; i++)
                list.Add(new KeyValuePair<string, string>(
                    DialogueBubbleKit.TailPieces[i],
                    $"BubbleKit.render('tail',{{tail:{Js(DialogueBubbleKit.TailKeys[i])},stock:{s}}})"));

            // Both bob frames come back in ONE cell (h + 1) so they register against each other —
            // see the note on DialogueBubbleKit.MarkerCellHeight.
            list.Add(new KeyValuePair<string, string>(DialogueBubbleKit.PieceMarker0,
                $"BubbleKit.render('marker',{{frame:0,stock:{s}}})"));
            list.Add(new KeyValuePair<string, string>(DialogueBubbleKit.PieceMarker1,
                $"BubbleKit.render('marker',{{frame:1,stock:{s}}})"));

            // caret() has no render() kind — it is one fillRect — so it is drawn onto a cell of its
            // own declared size rather than being cut out of something larger.
            list.Add(new KeyValuePair<string, string>(DialogueBubbleKit.PieceCaret,
                "(function(){var cv=new OffscreenCanvas(BubbleKit.M.caret.w,BubbleKit.M.caret.h);" +
                $"BubbleKit.caret(cv.getContext('2d'),0,0,{{stock:{s}}});return cv;}})()"));

            for (int i = 0; i < DialogueBubbleKit.CursorPieces.Length; i++)
                list.Add(new KeyValuePair<string, string>(
                    DialogueBubbleKit.CursorPieces[i],
                    $"BubbleKit.render('cursor',{{cursor:{Js(DialogueBubbleKit.CursorKeys[i])},stock:{s}}})"));

            return list;
        }

        /// <summary>Bake every piece into <paramref name="outputFolder"/>.</summary>
        public static BubbleKitBakeReport BakeAll(string outputFolder = null,
                                                  string stock = null,
                                                  Action<string, float> progress = null)
        {
            outputFolder ??= DialoguePresenter.ArtFolder;
            stock ??= DialogueBubbleKit.ShippedStock;

            var total = Stopwatch.StartNew();
            var renderClock = new Stopwatch();
            var contract = BubbleKitContract.Load();

            // THE ORACLE, before a pixel is written: if the runtime's copy of the kit's numbers has
            // drifted from the contract, every downstream measurement is against the wrong ruler.
            var drift = contract.Disagreements();
            if (drift.Count > 0)
                throw new InvalidOperationException(
                    "Refusing to bake: DialogueBubbleKit disagrees with the kit's contract on " +
                    $"{drift.Count} value(s).\n  {string.Join("\n  ", drift)}\n" +
                    $"The contract ({contract.SourcePath}) is generated from the rig and is the " +
                    "oracle — fix the C# constants, not the JSON.");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get(RigKey));

            var report = new BubbleKitBakeReport { EngineName = host.EngineName };

            string outAbs = Path.Combine(RigCatalog.RepoRoot, outputFolder);
            Directory.CreateDirectory(outAbs);

            var expressions = PieceExpressions(stock);
            for (int i = 0; i < expressions.Count; i++)
            {
                var (name, expr) = (expressions[i].Key, expressions[i].Value);
                progress?.Invoke(name, (float)i / expressions.Count);

                renderClock.Start();
                host.Execute($"{Slot} = {expr};");
                int w = (int)host.EvaluateNumber($"{Slot}.width");
                int h = (int)host.EvaluateNumber($"{Slot}.height");
                byte[] rgba = host.EvaluateBytes($"{Slot}.data");
                renderClock.Stop();

                if (w <= 0 || h <= 0)
                    throw new InvalidOperationException(
                        $"Piece '{name}' rendered {w}×{h}. The rig resolved but sized nothing — " +
                        "refusing rather than writing an empty PNG.");

                if (rgba.Length != w * h * 4)
                    throw new InvalidOperationException(
                        $"Piece '{name}' returned {rgba.Length} bytes for a {w}×{h} canvas " +
                        $"(expected {w * h * 4}). The shim and the rig disagree about the buffer.");

                AssertSizeAgainstContract(name, w, h, contract);

                var result = new BubbleKitPieceResult
                {
                    Name = name,
                    Expression = expr,
                    Width = w,
                    Height = h,
                    OpaquePixels = CountOpaque(rgba),
                    AssetPath = $"{outputFolder}/{name}.png",
                };

                if (result.OpaquePixels == 0)
                    throw new InvalidOperationException(
                        $"Piece '{name}' rendered {w}×{h} but is entirely transparent. A blank PNG " +
                        "imports cleanly and draws nothing, which is the failure this kit's " +
                        "tinted-rect fallback exists to be VISIBLY better than — refusing.");

                WritePng(rgba, w, h, result);
                if (result.Rewritten) report.RewrittenCount++;
                report.Pieces.Add(result);
            }

            report.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            report.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return report;
        }

        /// <summary>
        /// Cross-check a rendered piece against the contract's declared size. Pieces the contract
        /// does not size (the two 9-slice tiles are sized by this repo, not by the kit) are checked
        /// against <see cref="DialogueBubbleKit"/> instead, which the oracle has already validated.
        /// </summary>
        static void AssertSizeAgainstContract(string name, int w, int h, BubbleKitContract contract)
        {
            void Want(int ew, int eh)
            {
                if (w == ew && h == eh) return;
                throw new InvalidOperationException(
                    $"Piece '{name}' rendered {w}×{h} but the contract says {ew}×{eh}. The rig and " +
                    "its generated contract disagree — regenerate the contract from the rig rather " +
                    "than adjusting either by hand.");
            }

            if (name == DialogueBubbleKit.PiecePanel)
            {
                Want(DialogueBubbleKit.PanelTileSize, DialogueBubbleKit.PanelTileSize);
                return;
            }
            if (name == DialogueBubbleKit.PieceGold)
            {
                Want(DialogueBubbleKit.GoldTileSize, DialogueBubbleKit.GoldTileSize);
                return;
            }
            if (name == DialogueBubbleKit.PieceCaret)
            {
                Want(contract.Data.caret.w, contract.Data.caret.h);
                return;
            }
            if (name == DialogueBubbleKit.PieceMarker0 || name == DialogueBubbleKit.PieceMarker1)
            {
                // ⚠️ h + 1, deliberately: the two bob frames share one cell. See MarkerCellHeight.
                Want(contract.Data.marker.w, contract.Data.marker.h + 1);
                return;
            }

            for (int i = 0; i < DialogueBubbleKit.TailPieces.Length; i++)
                if (name == DialogueBubbleKit.TailPieces[i])
                {
                    var t = contract.Tail(DialogueBubbleKit.TailKeys[i]);
                    Want(t.w, t.h);
                    return;
                }

            for (int i = 0; i < DialogueBubbleKit.CursorPieces.Length; i++)
                if (name == DialogueBubbleKit.CursorPieces[i])
                {
                    var c = contract.Cursor(DialogueBubbleKit.CursorKeys[i]);
                    Want(c.w, c.h);
                    return;
                }

            throw new ArgumentException(
                $"Piece '{name}' has no size rule. Every baked piece must be checked against " +
                "something — add it here when you add it to DialogueBubbleKit.Pieces.");
        }

        static int CountOpaque(byte[] rgba)
        {
            int n = 0;
            for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] > 0) n++;
            return n;
        }

        /// <summary>
        /// Encode and write, top-left rows in and bottom-origin texture out.
        ///
        /// <para>Skips the write when the bytes already on disk are identical, which is what makes a
        /// re-bake a no-op instead of a churned <c>.meta</c> for every piece.</para>
        /// </summary>
        static void WritePng(byte[] rgba, int w, int h, BubbleKitPieceResult result)
        {
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                // The rigs hand back TOP-LEFT-origin rows; Unity textures are bottom-origin — the
                // same flip every sibling baker performs (BuildingRigBaker.BlitCropped).
                int dstRow = (h - 1 - y) * w;
                int srcRow = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int i = srcRow + x * 4;
                    pixels[dstRow + x] = new Color32(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]);
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            byte[] png;
            try
            {
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                png = tex.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }

            result.PngBytes = png.Length;

            string abs = Path.Combine(RigCatalog.RepoRoot, result.AssetPath);
            if (File.Exists(abs) && BytesEqual(File.ReadAllBytes(abs), png))
            {
                result.Rewritten = false;
                return;
            }

            File.WriteAllBytes(abs, png);
            result.Rewritten = true;
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>A JS string literal. Single-quoted to stay readable inside the expression table;
        /// the only inputs are catalog keys this file owns, but it escapes anyway.</summary>
        public static string Js(string s) =>
            s == null ? "null" : "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
    }
}
