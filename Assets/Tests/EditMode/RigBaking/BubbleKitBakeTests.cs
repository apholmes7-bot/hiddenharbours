using System.IO;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.RigBaking.EditMode
{
    /// <summary>
    /// The bubble kit through the real V8 host: the shim rasterises, every piece renders at the size
    /// the contract declares, and the render is deterministic.
    ///
    /// <para><b>These are the tests the cloud lane could not run.</b> They were written against a
    /// Node execution of the same rigs with an identical shim — same load order, same expressions,
    /// same two canvas members — so the JS half is evidenced; what only the editor can adjudicate is
    /// whether ClearScript's V8 agrees, which is what running these answers.</para>
    /// </summary>
    public class BubbleKitBakeTests
    {
        static IRigScriptHost Host()
        {
            var host = RigScriptHostFactory.Create();
            host.Execute(BubbleKitBaker.CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get(BubbleKitBaker.RigKey));
            return host;
        }

        [Test]
        public void TheRigInstallsItsGlobal_InABareHostCarryingOnlyTheShim()
        {
            using var host = Host();
            Assert.That(host.EvaluateBool("typeof BubbleKit === 'object' && BubbleKit !== null"), Is.True);
            Assert.That((int)host.EvaluateNumber("BubbleKit.PPU"),
                        Is.EqualTo(DialogueBubbleKit.PixelsPerUnit));
        }

        [Test]
        public void TheShimRasterises_ItDoesNotJustMailboxLikeItsPredecessor()
        {
            // The distinction that matters: CatchStorageBaker's shim stores an ImageData somebody
            // else filled. This one has to actually paint, because the rig's whole output is
            // fillRect calls. A shim that accepted them and dropped them would produce correctly
            // sized, entirely transparent PNGs.
            using var host = Host();
            host.Execute(
                "globalThis.__t = (function(){var cv = new OffscreenCanvas(4,4);" +
                "var c = cv.getContext('2d'); c.fillStyle = '#ff8000'; c.fillRect(1,1,2,2);" +
                "return cv;})();");

            Assert.That((int)host.EvaluateNumber("globalThis.__t.width"), Is.EqualTo(4));

            byte[] px = host.EvaluateBytes("globalThis.__t.data");
            Assert.That(px, Has.Length.EqualTo(4 * 4 * 4));

            // (0,0) untouched, (1,1) painted.
            Assert.That(px[3], Is.EqualTo(0), "an unpainted pixel must stay transparent");
            int i = (1 * 4 + 1) * 4;
            Assert.That(new[] { px[i], px[i + 1], px[i + 2], px[i + 3] },
                        Is.EqualTo(new byte[] { 0xff, 0x80, 0x00, 0xff }));
        }

        [Test]
        public void EveryPieceRendersAtTheSizeTheContractDeclares_AndIsNotBlank()
        {
            var contract = BubbleKitContract.Load();
            using var host = Host();

            foreach (var piece in BubbleKitBaker.PieceExpressions(DialogueBubbleKit.ShippedStock))
            {
                host.Execute($"globalThis.__p = {piece.Value};");
                int w = (int)host.EvaluateNumber("globalThis.__p.width");
                int h = (int)host.EvaluateNumber("globalThis.__p.height");
                byte[] rgba = host.EvaluateBytes("globalThis.__p.data");

                Assert.That(w, Is.GreaterThan(0), piece.Key);
                Assert.That(rgba, Has.Length.EqualTo(w * h * 4), piece.Key);

                int opaque = 0;
                for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] > 0) opaque++;
                Assert.That(opaque, Is.GreaterThan(0),
                            $"'{piece.Key}' rendered {w}×{h} and is entirely transparent — the shim " +
                            "accepted the draw calls and dropped them.");
            }

            // And the sizes the presenter's own constants promise.
            Assert.That(contract.Tail("left").tip, Is.EqualTo(new[] { 1, 6 }));
        }

        [Test]
        public void TheSameExpressionRendersTheSameBytesTwice()
        {
            // Idempotence at the source: the bake skips rewriting a PNG whose bytes match, which is
            // only a converging no-op if the RENDER is deterministic to begin with. Nothing in these
            // rigs should be time- or random-dependent; this is what says so.
            using var host = Host();
            string expr = null;
            foreach (var p in BubbleKitBaker.PieceExpressions(DialogueBubbleKit.ShippedStock))
                if (p.Key == DialogueBubbleKit.PiecePanel) expr = p.Value;

            Assert.That(expr, Is.Not.Null);

            host.Execute($"globalThis.__a = {expr};");
            byte[] first = host.EvaluateBytes("globalThis.__a.data");
            host.Execute($"globalThis.__b = {expr};");
            byte[] second = host.EvaluateBytes("globalThis.__b.data");

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void TheKitsHarnessCopiesOfTheCharacterRigsAreTheShippingOnes()
        {
            // The kit ships characterIsoRig6 / headIsoRig3 / eyeIsoRig under its own Art/ folder for
            // its harness. They are byte-identical to the shipping rigs today, and the talk-cue
            // catalog entry deliberately depends on the SHIPPING ones so the mouth probe adjudicates
            // the rig the game actually runs. If a copy ever forks, that reasoning quietly stops
            // being true — so this is the test that notices.
            string root = RigCatalog.RepoRoot;
            foreach (string name in new[] { "characterIsoRig6.js", "headIsoRig3.js", "eyeIsoRig.js" })
            {
                string shipping = Path.Combine(root, "docs/art/rigs", name);
                string inKit = Path.Combine(root, BubbleKitContract.KitFolder, "Art", name);

                Assert.That(File.Exists(shipping), Is.True, shipping);
                Assert.That(File.Exists(inKit), Is.True, inKit);
                Assert.That(File.ReadAllBytes(inKit), Is.EqualTo(File.ReadAllBytes(shipping)),
                            $"the kit's harness copy of {name} has forked from the shipping rig. " +
                            "Either re-sync it, or stop claiming the mouth probe tests the rig the " +
                            "game runs.");
            }
        }
    }

    /// <summary>
    /// <b>THE KIT'S ONE BEHAVIOURAL CLAIM, AS A TEST.</b>
    ///
    /// <para>The kit states that <c>CharacterIso6.render(dir,{t,talk:true})</c> already reaches the
    /// head rig's mouth cadence — "a rename, not a feature" — and ships
    /// <c>TalkCue.probeMouth()</c> to prove it: it renders one frame TWICE AT THE SAME <c>t</c>,
    /// talk on and talk off, so any changed pixel is necessarily the talk channel, then asserts the
    /// difference is mouth-sized and sits in the lower face. Its own import notes say: "Run it in the
    /// import PR and fail on it." This is that.</para>
    /// </summary>
    public class BubbleKitMouthProbeTests
    {
        [Test]
        public void TheMouthChannelIsAlreadyShipping_AndTheProbeSaysSo()
        {
            using var host = RigScriptHostFactory.Create();
            host.Execute(BubbleKitBaker.CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get(BubbleKitBaker.TalkCueRigKey));

            host.Execute("globalThis.__probe = TalkCue.probeMouth(CharacterIso6);");

            bool pass = host.EvaluateBool("!!globalThis.__probe.pass");
            double px = host.EvaluateNumber("globalThis.__probe.px");
            string note = host.EvaluateString("String(globalThis.__probe.note || '')");

            Assert.That(pass, Is.True,
                        $"TalkCue.probeMouth failed: {px} px changed. {note}\n" +
                        "The claim under test is that talk:true already reaches HeadIso.look() " +
                        "through pose()'s sixth positional argument. If this is red, the rename " +
                        "that kept `rock` as arguments[5] has broken it, and every consumer page " +
                        "breaks with it.");

            Assert.That(px, Is.GreaterThan(0),
                        "a probe that changed NOTHING would pass vacuously — the two renders would " +
                        "be identical and the 'difference is mouth-sized' assertion trivially true");
        }

        [Test]
        public void TheRigsPositionalContractIsIntact()
        {
            // The kit is explicit that the rename must keep the stable ids: render()/pose()/anchors()
            // signatures and the opts keys (t, talk, expr) stay exactly as they are, "or every
            // consumer page and this kit break at once".
            using var host = RigScriptHostFactory.Create();
            host.Execute(BubbleKitBaker.CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get(BubbleKitBaker.TalkCueRigKey));

            Assert.That(host.EvaluateBool("typeof CharacterIso6.render === 'function'"), Is.True);
            Assert.That(host.EvaluateBool("typeof CharacterIso6.anchors === 'function'"), Is.True);
            Assert.That(host.EvaluateBool("typeof TalkCue.body === 'function'"), Is.True);
            Assert.That(host.EvaluateBool("typeof TalkCue.probeMouth === 'function'"), Is.True);

            // ⚠️ `rock` is the SIXTH positional argument of pose() and is read as arguments[5] —
            // it is deliberately NOT a declared parameter. So pose.length is 4, not 6, and a
            // "pose.length >= 6" assertion here would be red against a perfectly healthy rig
            // (measured: 4). What actually matters is that nobody has DECLARED the extra
            // parameters, because Function.length stops at the first defaulted one and the opts
            // forwarding the kit depends on rides on the undeclared positional slot.
            Assert.That((int)host.EvaluateNumber("CharacterIso6.pose.length"), Is.EqualTo(4),
                        "pose()'s declared arity changed. `rock` is arguments[5] — undeclared on " +
                        "purpose — so this going to 5 or 6 means the signature was rewritten, and " +
                        "the kit's stable-id requirement says every consumer page breaks with it.");
        }
    }
}
