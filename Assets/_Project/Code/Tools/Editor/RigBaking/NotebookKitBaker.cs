using System;
using System.Collections.Generic;
using System.IO;
using HiddenHarbours.World;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One baked notebook piece, as reported back to the menu and the tests.</summary>
    public sealed class NotebookKitPieceResult
    {
        public string Name, AssetPath, Expression, SheetKey;
        public int Width, Height, OpaquePixels, PngBytes;
        public bool Rewritten;

        public override string ToString() =>
            $"{Name,-26} {Width,3}×{Height,-3} {OpaquePixels,5} px ink  " +
            $"{PngBytes,6:N0} B  {(Rewritten ? "written" : "unchanged")}";
    }

    public sealed class NotebookKitBakeReport
    {
        public string EngineName;
        public readonly List<NotebookKitPieceResult> Pieces = new List<NotebookKitPieceResult>();
        public double RenderMilliseconds, TotalMilliseconds;
        public int RewrittenCount;
    }

    /// <summary>
    /// <b>THE NOTEBOOK KIT, BAKED.</b> Runs <c>notebookRig3.js</c> through the repo's V8 host and
    /// writes one PNG per piece of furniture into <see cref="NotebookKit.ArtFolder"/>.
    ///
    /// <para><b>Furniture only — never a page.</b> The kit's reference sheet has 37 entries, and this
    /// bakes 26 of them. The three <c>type.*</c> specimens are superseded by the font asset (the face
    /// is type, not a sprite), and the seven <c>page.*</c> / <c>turn.*</c> entries are composites the
    /// sheet draws from <c>NotebookKit.SAMPLE</c> — baking those would bake sample GAME TEXT into a
    /// shipped asset, which the kit forbids in as many words: "NOTHING in this kit contains a word of
    /// game text… what ships is furniture with measured rects."</para>
    ///
    /// <para><b>Why hand-written expressions and not the sheet's own draw thunks.</b>
    /// <c>sheetSpec()</c> does publish a <c>draw</c> thunk per piece, which is tempting. But every
    /// thunk paints an opaque paper ground first (<c>paper(c, X-1, Y-1, w+2, h+2)</c>) so the piece
    /// reads on the sheet's dark backdrop — and at a canvas of exactly the piece's size that fill
    /// covers everything. Baking through the thunks would put an opaque page-coloured rectangle
    /// behind every checkbox and cursor, which is wrong the moment one is drawn on her own stock
    /// rather than the printed leaf. So each piece is drawn directly onto a TRANSPARENT canvas, the
    /// same shape <see cref="BubbleKitBaker.PieceExpressions"/> uses, and every size is cross-checked
    /// against the contract so a wrong argument is loud rather than subtle.</para>
    ///
    /// <para><b>The canvas shim is a SUPERSET of the bubble kit's.</b> Both rigs load into one host
    /// here (the notebook's prerequisite IS the bubble rig — it has no face of its own), so this shim
    /// must satisfy everything either touches. Measured across both files: the bubble rig needs
    /// <c>fillStyle</c> + <c>fillRect</c>; the notebook adds <c>save</c>/<c>restore</c>,
    /// <c>imageSmoothingEnabled</c> and <c>getImageData</c> (the last for its own probes). Nothing
    /// wider is provided, and an unsupported colour or element throws rather than drawing nothing.</para>
    ///
    /// <para><b>Idempotent.</b> A piece whose bytes match disk is not rewritten, so a re-run converges
    /// and leaves the working tree clean — no <c>.meta</c> churn (the #553 lesson).</para>
    ///
    /// <para>Stops at "PNGs on disk". Import settings and 9-slice borders are
    /// <see cref="NotebookKitImporter"/>'s job.</para>
    /// </summary>
    public static class NotebookKitBaker
    {
        /// <summary>The catalog key for the notebook rig.</summary>
        public const string RigKey = "notebook";

        /// <summary>
        /// Host-side environment shim (see class remarks). A superset of
        /// <see cref="BubbleKitBaker.CanvasShimJs"/>, behind its own marker so the two can never
        /// half-install over each other.
        ///
        /// <para>⚠️ <c>save</c>/<c>restore</c> preserve <c>fillStyle</c> ONLY, because that plus
        /// <c>imageSmoothingEnabled</c> is the entire mutable state either rig sets. If a rig ever
        /// starts using transforms or clipping, this must grow with it — and it will fail loudly
        /// (missing method) rather than silently drawing in the wrong place.</para>
        /// </summary>
        public const string CanvasShimJs = @"
(function (g) {
  if (g.__hhNotebookCanvasShim) return;
  g.__hhNotebookCanvasShim = 1;

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

  function Ctx(cv) {
    this._cv = cv; this._stack = [];
    this.fillStyle = '#000000';
    this.imageSmoothingEnabled = false;
  }
  Ctx.prototype.save = function () { this._stack.push(this.fillStyle); };
  Ctx.prototype.restore = function () {
    if (this._stack.length) this.fillStyle = this._stack.pop();
  };
  Ctx.prototype.fillRect = function (x, y, w, h) {
    var cv = this._cv;
    var x0 = Math.round(x), y0 = Math.round(y), x1 = Math.round(x + w), y1 = Math.round(y + h), t;
    if (x1 < x0) { t = x0; x0 = x1; x1 = t; }
    if (y1 < y0) { t = y0; y0 = y1; y1 = t; }
    var col = parse(this.fillStyle);
    if (!col) throw new Error('notebook canvas shim: unsupported fillStyle ' + this.fillStyle);
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
  Ctx.prototype.getImageData = function (x, y, w, h) {
    var cv = this._cv;
    x = Math.round(x); y = Math.round(y); w = Math.round(w); h = Math.round(h);
    var out = new Uint8ClampedArray(Math.max(0, w * h * 4));
    for (var py = 0; py < h; py++) {
      for (var px = 0; px < w; px++) {
        var sx = x + px, sy = y + py;
        if (sx < 0 || sy < 0 || sx >= cv.width || sy >= cv.height) continue;
        var si = (sy * cv.width + sx) * 4, di = (py * w + px) * 4;
        out[di] = cv.data[si]; out[di+1] = cv.data[si+1];
        out[di+2] = cv.data[si+2]; out[di+3] = cv.data[si+3];
      }
    }
    return { data: out, width: w, height: h };
  };

  function alloc(cv) {
    cv.data = new Uint8ClampedArray(Math.max(0, cv.width * cv.height * 4));
    cv._ctx = null;
  }
  function Canvas(w, h) { this.width = w | 0; this.height = h | 0; alloc(this); }
  Canvas.prototype.getContext = function (kind) {
    if (kind !== '2d') throw new Error('notebook canvas shim: only 2d, got ' + kind);
    // The rig sets width/height AFTER construction, so size the buffer on first use.
    if (!this.data || this.data.length !== this.width * this.height * 4) alloc(this);
    if (!this._ctx) this._ctx = new Ctx(this);
    return this._ctx;
  };

  g.OffscreenCanvas = Canvas;
  g.document = {
    createElement: function (tag) {
      if (String(tag).toLowerCase() !== 'canvas')
        throw new Error('notebook canvas shim provides only <canvas>, got <' + tag + '>');
      return new Canvas(0, 0);
    }
  };
})(globalThis);
";

        /// <summary>The global each rendered piece is parked on, so width/height/data are three cheap
        /// evaluations against one render rather than three renders.</summary>
        const string Slot = "globalThis.__hhNotebookPiece";

        /// <summary>A canvas of exactly (w, h) with the piece drawn at the origin and nothing else.</summary>
        static string Cv(string w, string h, string body) =>
            $"(function(){{var cv=new OffscreenCanvas({w},{h});var c=cv.getContext('2d');{body};return cv;}})()";

        /// <summary>
        /// Every piece, as (name, sheet key, the JS that produces its canvas). The sheet key is what
        /// <see cref="AssertSizeAgainstContract"/> looks the declared size up by, so a piece cannot be
        /// added here without also being measurable.
        /// </summary>
        public static IReadOnlyList<(string Name, string SheetKey, string Expr)> PieceExpressions()
        {
            const string G = "NotebookKit.GEO", C = "NotebookKit.CELL";
            var list = new List<(string, string, string)>();

            // --- paper stocks: 9-slice tiles. The stock IS the ground, so these keep theirs. -------
            foreach (var (piece, key) in new[]
            {
                (NotebookKit.PieceStockRuled, "ruled"),
                (NotebookKit.PieceStockSquared, "squared"),
                (NotebookKit.PieceStockMine, "mine"),
            })
                list.Add((piece, "stock." + key, Cv("70", "34",
                    "c.fillStyle=NotebookKit.DAY.page;c.fillRect(0,0,70,34);" +
                    $"NotebookKit.drawStock(c,{{x:0,y:0,w:70,h:34,i:0,marginX:{G}.ruleInset," +
                    $"rows:[0,1,2].map(function(i){{return {{i:i,rule:2+{C}.rule+i*{C}.pitch}};}})}}," +
                    $"'{key}',{{s:1}})")));

            // --- the checkbox family, drawn once and reused by her own lists -----------------------
            foreach (var (piece, key) in new[]
            {
                (NotebookKit.PieceBoxPencil, "pencil"),
                (NotebookKit.PieceBoxPrinted, "printed"),
                (NotebookKit.PieceBoxRound, "round"),
            })
                list.Add((piece, "box." + key, Cv("6", "6", $"NotebookKit.tickBox(c,0,0,1,{{style:'{key}'}})")));

            list.Add((NotebookKit.PieceBoxTicked, "box.ticked", Cv("7", "7",
                "NotebookKit.tickBox(c,0,0,1,{});NotebookKit.tickMark(c,0,0,1,{})")));

            // --- the current-step marker family ----------------------------------------------------
            foreach (var (piece, key) in new[]
            {
                (NotebookKit.PieceCurrentArrow, "arrow"),
                (NotebookKit.PieceCurrentPill, "pill"),
                (NotebookKit.PieceCurrentDot, "dot"),
            })
                list.Add((piece, "current." + key, Cv("8", "8",
                    $"NotebookKit.currentMark(c,{{x:0,y:1,w:{G}.box,h:{G}.box}},1,{{style:'{key}'}})")));

            list.Add((NotebookKit.PieceCaret, "caret", Cv("1", "6", "NotebookKit.caret(c,0,0,1,{})")));
            list.Add((NotebookKit.PieceStampDone, "stamp.done", Cv("25", "10",
                "NotebookKit.doneStamp(c,0,0,1,{})")));

            // --- cursors: the bubble kit's masks, RE-STAMPED in the notebook's own ramp -------------
            // Sized the way the RIG resolves them (BK.CURSORS[k] || BK.CURSORS.hand), not by a direct
            // lookup — see NotebookKit.Pieces on why 'cuff' is not among these.
            foreach (var (piece, key) in new[]
            {
                (NotebookKit.PieceCursorHand, "hand"),
                (NotebookKit.PieceCursorTack, "tack"),
            })
                list.Add((piece, "cursor." + key, Cv(
                    $"(BubbleKit.CURSORS['{key}']||BubbleKit.CURSORS.hand).w",
                    $"(BubbleKit.CURSORS['{key}']||BubbleKit.CURSORS.hand).h",
                    $"NotebookKit.cursor(c,0,0,'{key}',{{}})")));

            // --- tab stubs: 9-slice chips ------------------------------------------------------------
            // No label: the kit ships the chip and the label rect, never a label. `on` for the shipped
            // card so its pulled-out, inked edge is what the tile carries.
            foreach (var (piece, key) in new[]
            {
                (NotebookKit.PieceTabCard, "card"),
                (NotebookKit.PieceTabTape, "tape"),
                (NotebookKit.PieceTabCloth, "cloth"),
            })
                list.Add((piece, "tab." + key, Cv("37", "12",
                    $"NotebookKit.tabStub(c,{{i:0,x:9,y:0,w:{G}.tabCol,h:{G}.tabH,cols:2," +
                    $"label:{{x:9+{G}.tabPad,y:2}}}},{{label:'',style:'{key}'}}," +
                    $"'{(key == "card" ? "on" : "off")}',{{s:1}})")));

            list.Add((NotebookKit.PieceMarkNew, "mark.new", Cv("22", "10",
                $"NotebookKit.newNoteMark(c,{{i:0,band:{{x:0,y:0,w:22,h:{G}.pitch}}," +
                "box:{x:13,y:1},cursor:{x:1,y:1}},1,{})")));
            list.Add((NotebookKit.PieceMarkNext, "mark.next", Cv("5", "7",
                "NotebookKit.cornerMark(c,{x:0,y:0,w:5,h:6},'next',1,{})")));
            list.Add((NotebookKit.PieceMarkPrev, "mark.prev", Cv("5", "7",
                "NotebookKit.cornerMark(c,{x:0,y:0,w:5,h:6},'prev',1,{})")));
            list.Add((NotebookKit.PieceDogear, "dogear", Cv("9", "9",
                "NotebookKit.dogEar(c,{s:1,dogEar:{x:0,y:0,w:9,h:9}},{})")));
            list.Add((NotebookKit.PieceRibbon, "ribbon", Cv("5", "26",
                $"NotebookKit.ribbon(c,{{s:1,ribbon:{{x:1,y:0,w:{G}.ribbonW,h:22}}}},{{}})")));

            // --- the selection family. No text: furniture only. ----------------------------------------
            foreach (var (piece, key) in new[]
            {
                (NotebookKit.PieceSelectPill, "pill"),
                (NotebookKit.PieceSelectBracket, "bracket"),
                (NotebookKit.PieceSelectRule, "rule"),
                (NotebookKit.PieceSelectPress, "press"),
            })
                list.Add((piece, "select." + key, Cv("40", "10",
                    $"NotebookKit.highlight(c,{{x:0,y:0,w:40,h:{G}.pitch}},'{key}',{{s:1}})")));

            return list;
        }

        /// <summary>Bake every piece into <paramref name="outputFolder"/>.</summary>
        public static NotebookKitBakeReport BakeAll(string outputFolder = null,
                                                    Action<string, float> progress = null)
        {
            outputFolder ??= NotebookKit.ArtFolder;

            var total = Stopwatch.StartNew();
            var renderClock = new Stopwatch();
            var contract = NotebookKitContract.Load();

            // THE ORACLE, before a pixel is written: if the runtime's copy of the kit's numbers has
            // drifted from the contract, every downstream measurement is against the wrong ruler.
            var drift = contract.Disagreements();
            if (drift.Count > 0)
                throw new InvalidOperationException(
                    "Refusing to bake: NotebookKit disagrees with the kit's contract on " +
                    $"{drift.Count} value(s).\n  {string.Join("\n  ", drift)}\n" +
                    $"The contract ({contract.SourcePath}) is generated from the rig and is the " +
                    "oracle — fix the C# constants, not the JSON.");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(CanvasShimJs);
            // Installs the bubble rig first, through the notebook's declared prerequisite — the
            // notebook has no face of its own and will not resolve without it.
            RigCatalog.InstallModule(host, RigCatalog.Get(RigKey));

            var report = new NotebookKitBakeReport { EngineName = host.EngineName };

            string outAbs = Path.Combine(RigCatalog.RepoRoot, outputFolder);
            Directory.CreateDirectory(outAbs);

            var expressions = PieceExpressions();
            if (expressions.Count != NotebookKit.Pieces.Length)
                throw new InvalidOperationException(
                    $"The expression table has {expressions.Count} pieces but NotebookKit.Pieces has " +
                    $"{NotebookKit.Pieces.Length}. They are one list in two places on purpose — a " +
                    "piece in one and not the other bakes and never imports, or imports and never bakes.");

            for (int i = 0; i < expressions.Count; i++)
            {
                var (name, sheetKey, expr) = expressions[i];
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

                AssertSizeAgainstContract(name, sheetKey, w, h, contract);

                var result = new NotebookKitPieceResult
                {
                    Name = name,
                    SheetKey = sheetKey,
                    Expression = expr,
                    Width = w,
                    Height = h,
                    OpaquePixels = CountOpaque(rgba),
                    AssetPath = $"{outputFolder}/{name}.png",
                };

                if (result.OpaquePixels == 0)
                    throw new InvalidOperationException(
                        $"Piece '{name}' rendered {w}×{h} but is entirely transparent. A blank PNG " +
                        "imports cleanly and draws nothing, which is the failure the presenter's " +
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
        /// Cross-check a rendered piece against the size the kit's own reference sheet declares for
        /// it. Every baked piece has a sheet row, which is what makes this total rather than partial.
        /// </summary>
        static void AssertSizeAgainstContract(string name, string sheetKey, int w, int h,
                                              NotebookKitContract contract)
        {
            var declared = contract.SheetPiece(sheetKey);
            if (declared == null)
                throw new ArgumentException(
                    $"Piece '{name}' claims sheet key '{sheetKey}', which the contract does not list. " +
                    "Every baked piece must be measurable against the kit — add its row, or fix the key.");

            if (w == declared.px[0] && h == declared.px[1]) return;

            throw new InvalidOperationException(
                $"Piece '{name}' rendered {w}×{h} but the contract's '{sheetKey}' row says " +
                $"{declared.px[0]}×{declared.px[1]}. The expression and the kit disagree — the " +
                "expression is almost certainly passing a wrong rect, since the contract is generated " +
                "from the rig.");
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
        static void WritePng(byte[] rgba, int w, int h, NotebookKitPieceResult result)
        {
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                // The rigs hand back TOP-LEFT-origin rows; Unity textures are bottom-origin — the same
                // flip every sibling baker performs.
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
    }
}
