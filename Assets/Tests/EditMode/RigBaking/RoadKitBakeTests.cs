using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The ROAD KIT v3's bake, checked against the drop's own reference atlases by running the art
    /// director's rig unmodified.
    ///
    /// <para><b>⭐ THE ORACLE, AND WHY IT IS SHAPED THE WAY IT IS.</b> The drop ships eleven reference
    /// PNGs. They are NOT byte-comparable to the rig's output, and the reason was measured rather
    /// than assumed: on the dirt sheet every fully-opaque pixel matches the rig exactly, and
    /// <b>every single one of the 381 partial-alpha pixels differs</b> — each by 1–2 per channel, and
    /// each explained to the byte by a premultiply→unpremultiply round-trip. The reference sheets
    /// went out through a browser canvas, which stores premultiplied alpha and cannot round-trip a
    /// partial-alpha pixel losslessly. This kit's contact shadows ARE partial alpha, by design.</para>
    ///
    /// <para>So the comparison below is round-trip aware — and <b>tight, not tolerant</b>: an opaque
    /// pixel must be exactly equal, and a partial-alpha pixel must round-trip to exactly the
    /// reference byte. It is not a fuzz threshold. <see cref="Oracle_GoesRedForAPerturbedBake"/> is
    /// what stops it from decaying into one.</para>
    /// </summary>
    public class RoadKitBakeTests
    {
        const string G = RoadKitV3.RigGlobalName;

        static string RefDir =>
            Path.Combine(RigCatalog.RepoRoot, "docs/art/rigs/road-path-kit-v3/reference");

        static string RefPath(string surface) =>
            Path.Combine(RefDir, $"RoadIso3_{surface}_new_blob47.png");

        static IRigScriptHost NewHost()
        {
            var host = new V8RigScriptHost();
            RoadKitSheetBaker.LoadRig(host);
            return host;
        }

        // ---- the rig still is what the kit says it is -----------------------------------------------

        [Test]
        public void Rig_InstallsItsGlobalAndItsGeometryMatchesTheContract()
        {
            using var host = NewHost();
            Assert.DoesNotThrow(() => RoadKitSheetBaker.AssertRigGeometry(host));
            Assert.DoesNotThrow(() => RoadKitSheetBaker.AssertRigSurfaces(host));
        }

        /// <summary>
        /// <see cref="RoadKitContract.Canon"/> is the one piece of the rig's maths restated in C#.
        /// Pinned against the rig itself over ALL 256 inputs, so the two cannot drift — a drifted
        /// canon picks a neighbouring tile, which draws perfectly and tears at every junction.
        /// </summary>
        [Test]
        public void Canon_AgreesWithTheRig_ForAll256Neighbourhoods()
        {
            using var host = NewHost();
            for (int m = 0; m < 256; m++)
            {
                int rig = (int)host.EvaluateNumber($"{G}.canon({m})");
                Assert.AreEqual(rig, RoadKitContract.Canon(m), $"canon({m})");
            }
        }

        /// <summary>
        /// The contract's mask table is the rig's <c>BLOB47</c>, in order. Re-read from the rig so a
        /// re-bake that silently reordered would be caught by the committed contract, not by pixels.
        /// </summary>
        [Test]
        public void Contract_MatchesTheRigsBlob47_InOrder()
        {
            using var host = NewHost();
            var entries = RoadKitContract.Entries;
            Assert.AreEqual((int)host.EvaluateNumber($"{G}.BLOB47.length"), entries.Length);

            for (int i = 0; i < entries.Length; i++)
            {
                Assert.AreEqual((int)host.EvaluateNumber($"{G}.BLOB47[{i}].mask"), entries[i].Mask,
                                $"BLOB47[{i}].mask");
                Assert.AreEqual(host.EvaluateString($"{G}.BLOB47[{i}].label"), entries[i].Label,
                                $"BLOB47[{i}].label");
            }
        }

        /// <summary>
        /// ⚠️ <b>GO RED WHEN FIXED.</b> Measured, not read: the rig resolves its seed as
        /// <c>(opts.seed|0)||7</c>, so passing <b>0</b> renders identically to passing <b>7</b> and
        /// there is no way to ask for seed 0. Recorded rather than patched — the art director's file
        /// runs unmodified (ADR 0021 §5). If a future drop makes 0 mean 0 this goes red, and that red
        /// is the notification.
        /// </summary>
        [Test]
        public void RigQuirk_SeedZeroRendersIdenticallyToSeedSeven()
        {
            using var host = NewHost();
            byte[] zero = RenderWith(host, Perturbed("dirt", seed: 0));
            byte[] seven = RenderWith(host, Perturbed("dirt", seed: 7));
            byte[] eight = RenderWith(host, Perturbed("dirt", seed: 8));

            Assert.AreEqual(0, CountRawDiff(zero, seven),
                "seed 0 must still be silently seed 7 — see the remarks if this went red");
            Assert.AreNotEqual(0, CountRawDiff(seven, eight),
                "control: a seed that IS honoured must change pixels, or the test above proves nothing");
        }

        // ---- the reproduction proof ------------------------------------------------------------------

        [Test]
        public void EverySurface_ReproducesTheDropsReferenceAtlas()
        {
            using var host = NewHost();
            int checkedSheets = 0;

            foreach (string surface in RoadKitV3.Surfaces)
            {
                byte[] reference = LoadReference(surface);
                if (reference == null) continue;      // Ignore()d below if none resolved

                byte[] baked = RoadKitSheetBaker.RenderAtlas(host, surface);
                var (diff, opaque) = ComparePremultiplied(baked, reference);

                Assert.AreEqual(0, diff,
                    $"'{surface}' no longer reproduces its reference atlas: {diff} pixel(s) differ, " +
                    $"{opaque} of them fully opaque. Opaque differences are a REAL divergence " +
                    "(parameters or rig); partial-alpha differences beyond the round-trip mean the " +
                    "oracle's premise has changed. Do not relax the comparison to make this pass.");
                checkedSheets++;
            }

            if (checkedSheets == 0)
                Assert.Ignore("no reference atlases resolved — they are LFS-backed; `git lfs pull`. " +
                              "Ignored rather than passed, so a pointer-only checkout cannot look green.");
        }

        /// <summary>
        /// ⭐ THE SABOTAGE CHECK. An oracle that cannot fail proves nothing, and this one is unusual
        /// enough (a round-trip, not an equality) to deserve the proof. Each perturbation must BOTH
        /// move pixels against the control AND be caught — the second half alone would pass for a
        /// parameter the rig quietly ignores, which is exactly the quantised-parameter trap.
        /// </summary>
        [Test]
        public void Oracle_GoesRedForAPerturbedBake()
        {
            byte[] reference = LoadReference("dirt");
            if (reference == null)
                Assert.Ignore("dirt reference atlas unavailable (LFS) — cannot sabotage-prove.");

            using var host = NewHost();
            byte[] control = RoadKitSheetBaker.RenderAtlas(host, "dirt");
            Assert.AreEqual(0, ComparePremultiplied(control, reference).diff,
                            "the control must reproduce before a perturbation means anything");

            foreach (var (what, expr) in new (string, string)[]
            {
                ("seed 7 → 8", Perturbed("dirt", seed: 8)),
                ("pattern step 2 → 1", Perturbed("dirt", step: 1)),
                ("wear new → worn", Perturbed("dirt", wear: "worn")),
                ("axis derived instead of passed", Perturbed("dirt", passAxis: false)),
            })
            {
                byte[] got = RenderWith(host, expr);
                Assert.AreNotEqual(0, CountRawDiff(got, control),
                    $"perturbation '{what}' changed no pixels at all — it is not exercising " +
                    "anything, so it cannot prove the oracle discriminates.");
                Assert.AreNotEqual(0, ComparePremultiplied(got, reference).diff,
                    $"the oracle did NOT notice '{what}'. It has decayed into a fuzz threshold.");
            }
        }

        // ---- helpers -----------------------------------------------------------------------------------

        /// <summary>Render a whole atlas from an explicit per-cell expression builder.</summary>
        static byte[] RenderWith(IRigScriptHost host, string cellExpr)
        {
            var atlas = new byte[RoadKitV3.SheetWidth * RoadKitV3.SheetHeight * 4];
            int stride = RoadKitV3.Tile * 4;
            for (int i = 0; i < RoadKitV3.BlobCount; i++)
            {
                host.Execute($"globalThis.__hhRoadTest = {cellExpr.Replace("{I}", i.ToString())};");
                byte[] cell = host.EvaluateBytes("__hhRoadTest.data");
                int ox = RoadKitV3.ColOf(i) * RoadKitV3.Tile;
                int oy = RoadKitV3.RowOf(i) * RoadKitV3.CellH;
                for (int y = 0; y < RoadKitV3.CellH; y++)
                    System.Buffer.BlockCopy(cell, y * stride, atlas,
                                            ((oy + y) * RoadKitV3.SheetWidth + ox) * 4, stride);
            }
            return atlas;
        }

        static string Perturbed(string surface, int seed = RoadKitV3.BakedSeed, int step = RoadKitV3.PatternStep,
                                string wear = RoadKitV3.BakedWear, bool passAxis = true)
        {
            string axis = passAxis ? " o.axis = b.axis;" : "";
            return $@"(function(){{
                var i = {{I}}; var b = {G}.BLOB47[i];
                var o = {{ con:b.con, diag:b.diag, wear:'{wear}',
                          profile:'{RoadKitV3.BakedProfile}', piece:'{RoadKitV3.BakedPiece}',
                          markings:[], ground:'{RoadKitV3.BakedGround}',
                          gx:{step}*(i%{RoadKitV3.Cols}), gy:{step}*Math.floor(i/{RoadKitV3.Cols}),
                          seed:{seed} }};{axis}
                return {G}.render('{surface}', o);
            }})()";
        }

        static byte[] LoadReference(string surface)
        {
            string path = RefPath(surface);
            if (!File.Exists(path)) return null;

            byte[] bytes = File.ReadAllBytes(path);
            // An LFS pointer is a few hundred bytes of ASCII, not a PNG.
            if (bytes.Length < 1024 || bytes[0] != 0x89) return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                if (!tex.LoadImage(bytes)) return null;
                if (tex.width != RoadKitV3.SheetWidth || tex.height != RoadKitV3.SheetHeight)
                    Assert.Fail($"reference '{surface}' is {tex.width}×{tex.height}, kit atlas is " +
                                $"{RoadKitV3.SheetWidth}×{RoadKitV3.SheetHeight}");

                // Unity textures are bottom-up; the rig's buffer is top-down. Flip to compare.
                Color32[] px = tex.GetPixels32();
                var top = new byte[px.Length * 4];
                for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                {
                    Color32 c = px[(tex.height - 1 - y) * tex.width + x];
                    int i = (y * tex.width + x) * 4;
                    top[i] = c.r; top[i + 1] = c.g; top[i + 2] = c.b; top[i + 3] = c.a;
                }
                return top;
            }
            finally { Object.DestroyImmediate(tex); }
        }

        static int CountRawDiff(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return int.MaxValue;
            int n = 0;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) n++;
            return n;
        }

        /// <summary>Opaque pixels exactly; partial-alpha pixels through the canvas round-trip.</summary>
        static (int diff, int opaque) ComparePremultiplied(byte[] got, byte[] reference)
        {
            if (got.Length != reference.Length) return (int.MaxValue, int.MaxValue);
            int diff = 0, opaque = 0;

            for (int i = 0; i < got.Length; i += 4)
            {
                int a = got[i + 3];
                bool bad = a != reference[i + 3];
                if (!bad)
                    for (int c = 0; c < 3; c++)
                    {
                        int have = got[i + c], want = reference[i + c];
                        if (a == 255 ? have != want : RoundTrip(have, a) != want) { bad = true; break; }
                    }
                if (bad) { diff++; if (a == 255) opaque++; }
            }
            return (diff, opaque);
        }

        /// <summary>
        /// ⚠️ <b>Away-from-zero, NOT <see cref="Mathf.RoundToInt"/>.</b> Unity's helper rounds halves
        /// to even (banker's), and the round-trip that was measured to explain all 381 partial-alpha
        /// pixels is away-from-zero. Using the wrong one here would leave a handful of shadow pixels
        /// "differing" for a reason that has nothing to do with the art.
        /// </summary>
        static int RoundTrip(int c, int a)
        {
            if (a == 0) return 0;
            int pm = (int)System.Math.Round(c * a / 255.0, System.MidpointRounding.AwayFromZero);
            return System.Math.Min(255,
                (int)System.Math.Round(pm * 255.0 / a, System.MidpointRounding.AwayFromZero));
        }
    }
}
