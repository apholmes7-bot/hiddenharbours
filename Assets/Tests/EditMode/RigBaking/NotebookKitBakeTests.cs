using System.Linq;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.RigBaking.EditMode
{
    /// <summary>
    /// The notebook kit through the real V8 host: the shim rasterises, the kit's own four probes
    /// pass, every piece renders at the size the contract declares, and the render is deterministic.
    ///
    /// <para><b>These are the tests the cloud lane could not run.</b> They were written against a
    /// Node execution of the same rigs with a shim identical in shape — same load order, same
    /// expressions, same canvas members — so the JS half is evidenced; what only the editor can
    /// adjudicate is whether ClearScript's V8 agrees, which is what running these answers.</para>
    /// </summary>
    public class NotebookKitBakeTests
    {
        static IRigScriptHost Host()
        {
            var host = RigScriptHostFactory.Create();
            host.Execute(NotebookKitBaker.CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get(NotebookKitBaker.RigKey));
            return host;
        }

        [Test]
        public void TheShimActuallyRasterises()
        {
            // Not "the shim accepts calls" — that would pass against a mailbox that drops every
            // pixel. This paints a known rect and counts what landed.
            using var host = Host();
            host.Execute(
                "globalThis.__t = (function(){var cv=new OffscreenCanvas(4,4);var c=cv.getContext('2d');" +
                "c.fillStyle='#ff0000';c.fillRect(1,1,2,2);return cv;})();");

            Assert.That((int)host.EvaluateNumber("__t.width"), Is.EqualTo(4));
            Assert.That((int)host.EvaluateNumber(
                    "(function(){var n=0,d=__t.data;for(var i=3;i<d.length;i+=4)if(d[i]>0)n++;return n;})()"),
                Is.EqualTo(4), "a 2×2 fill should leave exactly 4 opaque pixels");
            Assert.That((int)host.EvaluateNumber("__t.data[(1*4+1)*4]"), Is.EqualTo(255),
                "the painted pixel should carry the red channel it was given");
        }

        [Test]
        public void TheShimKeepsFillStyleAcrossSaveAndRestore()
        {
            // save/restore is the member the bubble kit's shim does NOT have, and the notebook rig
            // uses it. A no-op restore would silently leak a colour into whatever drew next.
            using var host = Host();
            host.Execute(
                "globalThis.__s = (function(){var cv=new OffscreenCanvas(2,1);var c=cv.getContext('2d');" +
                "c.fillStyle='#112233';c.save();c.fillStyle='#445566';c.restore();" +
                "c.fillRect(0,0,1,1);return cv;})();");

            Assert.That((int)host.EvaluateNumber("__s.data[0]"), Is.EqualTo(0x11),
                "restore() must put the earlier fillStyle back");
            Assert.That((int)host.EvaluateNumber("__s.data[1]"), Is.EqualTo(0x22));
        }

        [Test]
        public void TheKitsOwnFourProbesPass()
        {
            // The kit's README: "run these in the import PR and fail on them."
            using var host = Host();
            foreach (string probe in NotebookKitBakeMenu.NotebookProbes)
            {
                host.Execute($"globalThis.__p = NotebookKit.{probe}();");
                bool pass = host.EvaluateBool("!!__p.pass");
                string fails = host.EvaluateString("JSON.stringify(__p.fails || null)");
                Assert.That(pass, Is.True, $"NotebookKit.{probe}() failed: {fails}");
            }
        }

        [Test]
        public void TheFaceIsLiterallyTheBubbleKitsObject()
        {
            // probeType reports sameFontObject — object identity, not matching numbers. This is what
            // makes "one voice across the talking UI and the book UI" enforced rather than promised.
            using var host = Host();
            Assert.That(host.EvaluateBool("NotebookKit.FONT === BubbleKit.FONT"), Is.True,
                "The notebook must not carry a face of its own.");
            Assert.That(host.EvaluateBool("NotebookKit.CELL.adv === BubbleKit.CELL.adv && " +
                                          "NotebookKit.CELL.line === BubbleKit.CELL.line"), Is.True);
        }

        [Test]
        public void EveryPieceRendersAtItsContractSizeAndIsNotBlank()
        {
            using var host = Host();
            var contract = NotebookKitContract.Load();

            foreach (var (name, sheetKey, expr) in NotebookKitBaker.PieceExpressions())
            {
                host.Execute($"globalThis.__q = {expr};");
                int w = (int)host.EvaluateNumber("__q.width");
                int h = (int)host.EvaluateNumber("__q.height");
                int ink = (int)host.EvaluateNumber(
                    "(function(){var n=0,d=__q.data;for(var i=3;i<d.length;i+=4)if(d[i]>0)n++;return n;})()");

                var declared = contract.SheetPiece(sheetKey);
                Assert.That(declared, Is.Not.Null, $"{name}: no '{sheetKey}' row in the contract");
                Assert.That(new[] { w, h }, Is.EqualTo(new[] { declared.px[0], declared.px[1] }),
                    $"{name} rendered {w}×{h}, contract says {declared.px[0]}×{declared.px[1]}");
                Assert.That(ink, Is.GreaterThan(0),
                    $"{name} rendered {w}×{h} but is entirely transparent — a blank PNG imports " +
                    "cleanly and draws nothing.");
            }
        }

        [Test]
        public void RenderingIsDeterministic()
        {
            // What makes the bake's skip-if-unchanged a converging no-op rather than a coin flip.
            using var host = Host();
            foreach (var (name, _, expr) in NotebookKitBaker.PieceExpressions())
            {
                host.Execute($"globalThis.__a = {expr}; globalThis.__b = {expr};");
                bool same = host.EvaluateBool(
                    "(function(){if(__a.data.length!==__b.data.length)return false;" +
                    "for(var i=0;i<__a.data.length;i++)if(__a.data[i]!==__b.data[i])return false;" +
                    "return true;})()");
                Assert.That(same, Is.True, $"{name} rendered differently on a second call");
            }
        }

        /// <summary>
        /// <b>⚠️ THE CUFF TRAP, pinned.</b> The kit declares
        /// <c>CURSOR_ORDER = ['hand','cuff','tack']</c>, but the bubble kit ships
        /// <c>['hand','glove','tack','hook']</c> — there is no <c>cuff</c>. The rig resolves cursors
        /// as <c>BK.CURSORS[kind] || BK.CURSORS.hand</c>, so <c>cuff</c> falls back to HAND's mask,
        /// and the notebook's own <c>CURSORS.cuff.ramp</c> is identical to <c>CURSORS.hand.ramp</c> —
        /// so it renders byte-for-byte identical to hand. Baking it would ship a duplicate PNG under
        /// a name implying a second cursor, so <see cref="NotebookKit.Pieces"/> leaves it out.
        ///
        /// <para>The day the kit gives cuff a real mask or its own ramp, this goes red — and cuff
        /// earns its own bake. The likely intent is the bubble kit's <c>glove</c> (10 × 10).</para>
        /// </summary>
        [Test]
        public void CuffIsStillJustHandAndIsStillNotBaked()
        {
            using var host = Host();

            Assert.That(host.EvaluateBool("!BubbleKit.CURSORS.cuff"), Is.True,
                "The bubble kit has grown a real 'cuff' — the fallback no longer applies, so " +
                "re-read this trap and decide whether cuff now deserves its own baked piece.");

            host.Execute(
                "globalThis.__cuffSame = (function(){" +
                "function draw(k){var s=BubbleKit.CURSORS[k]||BubbleKit.CURSORS.hand;" +
                "var cv=new OffscreenCanvas(s.w,s.h);NotebookKit.cursor(cv.getContext('2d'),0,0,k,{});return cv;}" +
                "var a=draw('hand'),b=draw('cuff');" +
                "if(a.data.length!==b.data.length)return false;" +
                "for(var i=0;i<a.data.length;i++)if(a.data[i]!==b.data[i])return false;return true;})();");

            Assert.That(host.EvaluateBool("__cuffSame"), Is.True,
                "cursor.cuff no longer renders identically to cursor.hand — it has become a real " +
                "alternate and should now be baked as its own piece.");

            Assert.That(NotebookKit.Pieces, Does.Not.Contain("Notebook_Cursor_Cuff"),
                "While cuff is byte-identical to hand it must not be baked as a separate asset.");
        }

        [Test]
        public void WithoutIsoSolidTheClosedObjectSilentlyVanishes()
        {
            // Documents why deckIsoSolid is a declared prerequisite even though no baked piece needs
            // it: the failure mode is a MISSING GLOBAL, not an error. Load order here is deliberate —
            // the bubble face only, then the notebook.
            using var host = RigScriptHostFactory.Create();
            host.Execute(NotebookKitBaker.CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get("dialogueBubble"));
            host.Execute(RigCatalog.ReadSource(RigCatalog.Get(NotebookKitBaker.RigKey)));

            Assert.That(host.EvaluateBool("typeof NotebookKit !== 'undefined'"), Is.True,
                "the furniture half loads happily without isoSolid — which is the trap");
            Assert.That(host.EvaluateBool("typeof NotebookIsoKit === 'undefined'"), Is.True,
                "If NotebookIsoKit now registers without isoSolid, the prerequisite note in " +
                "RigCatalog.NotebookKit.cs is stale and should be corrected.");
        }

        [Test]
        public void OverflowIsMorePagesAndNeverALostBlock()
        {
            // flow()'s conservation law: placed + overflow = given. The pencil "n more" is only a
            // fact if nothing is quietly dropped, and a page turn is the only answer to a long list.
            using var host = Host();
            host.Execute(
                "globalThis.__flow = (function(){" +
                "var L=NotebookKit.layout({cols:24,lines:12,scale:1});var out=[];" +
                "[1,3,7,20].forEach(function(n){" +
                "  var blocks=[];for(var i=0;i<n;i++)blocks.push({kind:'quest',title:'q'+i,context:'x'," +
                "    steps:[{text:'a',state:'todo'},{text:'b',state:'todo'},{text:'c',state:'todo'}]});" +
                "  var f=NotebookKit.flow(blocks,L);" +
                "  out.push(f.placed.length + f.overflow === n);});" +
                "return out.every(Boolean);})();");

            Assert.That(host.EvaluateBool("__flow"), Is.True,
                "flow() lost or duplicated a block at one of n = 1, 3, 7, 20.");
        }

        [Test]
        public void AnOverlongQuestIsSplitRatherThanNotDrawn()
        {
            // The kit's rule: a block that fits neither leaf is split, "because the alternative is
            // not drawing it".
            using var host = Host();
            host.Execute(
                "globalThis.__huge = (function(){" +
                "var L=NotebookKit.layout({cols:24,lines:12,scale:1});var steps=[];" +
                "for(var i=0;i<60;i++)steps.push({text:'step '+i,state:'todo'});" +
                "return NotebookKit.flow([{kind:'quest',title:'long',steps:steps}],L).placed.length;})();");

            Assert.That((int)host.EvaluateNumber("__huge"), Is.GreaterThan(0),
                "a 60-step quest must still put something on the page");
        }

        [Test]
        public void FitAlwaysReturnsAnIntegerScaleAndReportsClamping()
        {
            // "INTEGER scale only — a fractional scale puts the hand on a half cell and the wobble
            // stops reading as a hand and starts reading as a bug." And: never clamp a returned rect
            // yourself — pass the room in and read `clamped`.
            using var host = Host();
            foreach (var (w, h) in new[] { (480, 270), (640, 360), (960, 540), (1920, 1080), (200, 120) })
            {
                host.Execute($"globalThis.__f = NotebookKit.fit({w},{h});");
                double s = host.EvaluateNumber("__f.s");
                Assert.That(s, Is.EqualTo(System.Math.Floor(s)), $"fit({w},{h}) returned scale {s}");

                bool fits = host.EvaluateBool($"__f.w <= {w} && __f.h <= {h}");
                if (!fits)
                    Assert.That(host.EvaluateBool("!!__f.clamped"), Is.True,
                        $"fit({w},{h}) overflowed the room without reporting clamped");
            }
        }
    }
}
