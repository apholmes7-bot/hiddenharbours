using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The INTAKE contract for the pixel-language cliff kit (<c>docs/art/rigs/px-cliff-face-kit/</c>),
    /// landed beside — not over — the v10 photoreal kit.
    ///
    /// <para><b>This PR wires nothing.</b> <c>CliffBaker</c> still drives the v10 <c>cliffRig.js</c>;
    /// the flip is a later PR. So this suite is the whole of the kit's guard: it proves the landed
    /// bytes are the ones the sidecar was stamped over, that the rig runs headless under V8, that its
    /// geometry really is v10's (the claim the whole arc rests on), and that the landed sample PNGs
    /// are what the landed rig actually bakes.</para>
    ///
    /// <para>Everything here is CPU-side — file reads, the JsEngine, and PNG decodes through
    /// <see cref="ImageConversion.LoadImage"/> — so it passes on CI's GPU-less agent, exactly like
    /// <c>CliffRigBakeTests</c> beside it.</para>
    /// </summary>
    public class PxCliffFaceKitIntakeTests
    {
        const string KitDir = "docs/art/rigs/px-cliff-face-kit";
        const string RigJs = KitDir + "/bake/pxCliffFaceRig.js";
        const string LangJs = KitDir + "/bake/pixelLanguage.js";
        const string Sidecar = KitDir + "/pxCliffFaceRig.gameplay.json";
        const string PalettePng = KitDir + "/CliffPx_palette.png";
        const string V10RigJs = "docs/art/rigs/cliff-face-kit/bake/cliffRig.js";

        const string PX = "PxCliffFace";     // the new kit's global
        const string V10 = "CliffRig";       // the kit it claims to be a drop-in for

        const int FaceW = 384, FaceH = 288, DecalH = 128;
        const int PaletteRows = 25, PaletteBands = 5;

        /// <summary>The sample set the drop ships: sandstone, every aspect, at wear step 1 on a
        /// vertical wall. Those parameters are not documented anywhere machine-readable — they are
        /// recovered by the bake in gate 4 matching bit for bit, which is also what would catch a
        /// re-bake at some other step slipping in unannounced.</summary>
        static readonly string[] SampleAspects = { "W", "SW", "S", "SE", "E" };

        /// <summary>Filename suffixes, which are also the keys <c>channels()</c> returns — the albedo
        /// is the empty string in both.</summary>
        static readonly string[] Channels = { "", "_unlit", "_mask", "_normal", "_index" };

        static IRigScriptHost _px, _v10;

        [OneTimeSetUp]
        public void InstallBothRigsOnce()
        {
            // Two engines, not one. The parity gate below compares two ports of the same field, and a
            // shared global scope is exactly how a comparison quietly becomes self-referential.
            _px = RigScriptHostFactory.Create();
            _px.Execute(File.ReadAllText(Abs(LangJs)));   // the rig throws without the language first
            _px.Execute(File.ReadAllText(Abs(RigJs)));

            _v10 = RigScriptHostFactory.Create();
            _v10.Execute(File.ReadAllText(Abs(V10RigJs)));
        }

        [OneTimeTearDown]
        public void DisposeHosts()
        {
            _px?.Dispose(); _px = null;
            _v10?.Dispose(); _v10 = null;
        }

        // =====================================================================================
        //  gate 1 — the two digest pins, and the census
        // =====================================================================================

        /// <summary>
        /// 🔴 <b>TWO hashes, because two files own the contract.</b> The sidecar stamps
        /// <c>derivedFromRigSha256</c> over the rig and <c>languageDerivedFromRigSha256</c> over
        /// <c>pixelLanguage.js</c> — the language owns the key light every albedo was baked under and
        /// the <c>bands()</c> ramp the palette LUT is cut from, so a language edit moves every number
        /// in LIGHTING and every row in PALETTES while the rig's own hash sits still. Stamp both,
        /// never one.
        ///
        /// <para>The expected hex is READ FROM THE LANDED SIDECAR, never restated here. A test
        /// carrying its own copy of the number proves the two copies agree, not that the file does.</para>
        ///
        /// <para>Hashed over LF-NORMALISED bytes. The stamps were taken over the files as written,
        /// which are LF; a Windows checkout with <c>core.autocrlf</c> on would otherwise miss both
        /// pins for line endings alone. Normalising here is what lets the kit land without a
        /// <c>.gitattributes</c> pin of its own — the sail and seagull kits took the other route,
        /// and either is sound so long as exactly one of them is in force.</para>
        /// </summary>
        [Test]
        public void TheLandedRigAndLanguageAreTheBytesTheSidecarWasStampedOver()
        {
            object root = DeckSidecarJson.Parse(File.ReadAllText(Abs(Sidecar)));

            string rigStamp = DeckSidecarJson.String(DeckSidecarJson.Member(root, "derivedFromRigSha256"));
            string langStamp = DeckSidecarJson.String(DeckSidecarJson.Member(root, "languageDerivedFromRigSha256"));

            Assert.IsFalse(string.IsNullOrEmpty(rigStamp), "The sidecar carries no derivedFromRigSha256.");
            Assert.IsFalse(string.IsNullOrEmpty(langStamp), "The sidecar carries no " +
                "languageDerivedFromRigSha256 — the language owns half the lighting contract and must " +
                "be stamped in its own right.");
            Assert.AreNotEqual(rigStamp, langStamp,
                "Both stamps hold the SAME hex. Two different files cannot hash alike; one stamp was " +
                "copied over the other and half the contract is unguarded.");

            Assert.AreEqual(rigStamp.ToLowerInvariant(), Sha256Lf(Abs(RigJs)),
                RigJs + " is not the file the sidecar was stamped over. Every gameplay number in that " +
                "sidecar — the collision profile, the batter table, the palette rows — describes a rig " +
                "that is no longer on disk. STOP: neither the file nor the stamp may be rewritten to " +
                "make the other fit.");

            Assert.AreEqual(langStamp.ToLowerInvariant(), Sha256Lf(Abs(LangJs)),
                LangJs + " is not the file the sidecar was stamped over. The key vector and the band " +
                "ramps live here, so the landed PALETTES table and every LIGHTING number describe a " +
                "language that is no longer on disk.");
        }

        /// <summary>
        /// The kit is 34 files and the gates below exercise nine of them. A half-copied drop — rig
        /// present, a shader or half the samples missing — would otherwise sail through every gate
        /// and land a kit that cannot be baked from.
        /// </summary>
        [Test]
        public void TheKitLandedWhole()
        {
            foreach (string rel in new[] { KitDir + "/README.md", Sidecar, PalettePng, RigJs, LangJs,
                                           KitDir + "/bake/_pxCliffBake.js",
                                           KitDir + "/bake/_pxCliffGameplay.js",
                                           KitDir + "/shaders/relight_palette.glsl",
                                           KitDir + "/shaders/relight_continuous.glsl" })
                Assert.IsTrue(File.Exists(Abs(rel)), "missing from the drop: " + rel);

            foreach (string aspect in SampleAspects)
                foreach (string channel in Channels)
                    Assert.IsTrue(File.Exists(Abs(SamplePath(aspect, channel))),
                        "missing sample: " + SamplePath(aspect, channel));

            Assert.AreEqual(34, Directory.GetFiles(Abs(KitDir), "*", SearchOption.AllDirectories).Length,
                "The kit is 34 files. A different count means the drop was edited on its way in — this " +
                "PR lands VERBATIM copies, and the two hashes above only cover two of them.");
        }

        // =====================================================================================
        //  gate 2 — the rig runs on CI, and says what the sidecar says
        // =====================================================================================

        /// <summary>
        /// The public API the later flip PR will call, named one by one rather than inferred: a
        /// renamed export must fail HERE, where the message can say so, and not inside a bake.
        /// </summary>
        [Test]
        public void TheRigInstallsItsApiUnderV8()
        {
            Assert.IsTrue(_px.EvaluateBool("typeof globalThis.PxLang === 'object' && PxLang !== null"),
                "pixelLanguage.js did not install globalThis.PxLang. The rig's first line throws " +
                "without it — the two files load in order, language first.");

            Assert.IsTrue(_px.EvaluateBool("typeof globalThis." + PX + " === 'object' && " + PX + " !== null"),
                "pxCliffFaceRig.js ran but did not install globalThis." + PX + ".");

            foreach (string fn in new[] { "face", "channels", "profile", "brow", "toe", "paletteLUT" })
                Assert.IsTrue(_px.EvaluateBool("typeof " + PX + "." + fn + " === 'function'"),
                    PX + "." + fn + "() is missing. The baker calls the rig's public API directly (no " +
                    "shim), so a renamed export is a bake that throws mid-run.");

            // The vocabulary both kits name. This is what makes the geometry gate below a comparison
            // of like with like rather than of two different walls that happen to be the same size.
            Assert.AreEqual(_v10.EvaluateString(V10 + ".ROCKS.join(',')"),
                            _px.EvaluateString(PX + ".ROCKS.join(',')"),
                "The two kits name different rocks. The seed is derived from the rock's INDEX, so a " +
                "reordered array silently bakes sandstone as another rock's wall.");
            Assert.AreEqual(_v10.EvaluateString("Object.keys(" + V10 + ".SLOPES).join(',')"),
                            _px.EvaluateString("Object.keys(" + PX + ".SLOPES).join(',')"),
                "The two kits name different batters.");
        }

        /// <summary>
        /// One probe set through the whole channel path, at the size the import settings and the
        /// shader both assume. <c>_index</c> is OPT-IN (<c>channels(b, {index:true})</c>) and is the
        /// channel the palette-shift relight path cannot work without — a bake that forgets the flag
        /// produces four channels and a shader that samples nothing.
        /// </summary>
        [Test]
        public void TheProbeSetReturnsFiveCoRegisteredChannels()
        {
            _px.Execute("globalThis.__b = " + PX + ".face('sandstone', 'S', 1, {slope: 'wall'});");
            _px.Execute("globalThis.__c = " + PX + ".channels(__b, {index: true});");

            Assert.AreEqual(FaceW, (int)_px.EvaluateNumber("__b.W"));
            Assert.AreEqual(FaceH, (int)_px.EvaluateNumber("__b.H"));

            foreach (string channel in Channels)
                Assert.AreEqual(FaceW * FaceH * 4, (int)_px.EvaluateNumber("__c[" + Js(channel) + "].length"),
                    "channel '" + (channel == "" ? "(albedo)" : channel) + "' is not " + FaceW + "x" +
                    FaceH + " RGBA. The five channels are co-registered — the shader samples one texel " +
                    "position across all of them, so a channel of another size is a face lit by " +
                    "another face.");

            Assert.IsTrue(_px.EvaluateBool(PX + ".channels(__b)._index === undefined"),
                "_index came back WITHOUT the {index:true} flag. It is opt-in on purpose (the standing " +
                "bake's file count), and a channel that appears unasked would silently grow every bake " +
                "by a fifth — check the flag before relaxing this.");

            _px.Execute("globalThis.__br = " + PX + ".brow('S', 1, {slope: 'wall'});");
            _px.Execute("globalThis.__to = " + PX + ".toe('S', 1, {});");
            foreach (string v in new[] { "__br", "__to" })
            {
                Assert.AreEqual(FaceW, (int)_px.EvaluateNumber(v + ".W"),
                    "The decals tile along s with the face, so they share its width.");
                Assert.AreEqual(DecalH, (int)_px.EvaluateNumber(v + ".H"));
                Assert.AreEqual(FaceW * DecalH * 4, (int)_px.EvaluateNumber(v + ".data.length"));
            }
        }

        /// <summary>
        /// ⭐ <b>The palette LUT has to agree in three places at once</b> — the rig computes it, the
        /// sidecar publishes it to the gameplay side, and <c>CliffPx_palette.png</c> is what the
        /// shader actually taps. The relight path reads <c>_index</c> R as a row and G as a band and
        /// looks the colour up in that image; if the image and the rig ever disagreed, every relit
        /// texel would come back the wrong colour with nothing in the frame to say why.
        ///
        /// <para>The PNG is compared as DECODED PIXELS, never as file bytes — encoder output is not
        /// reproducible, which is the whole reason this kit ships a rig instead of sheets.</para>
        /// </summary>
        [Test]
        public void ThePaletteLutIsTheSidecarsTableAndThePngsPixels()
        {
            _px.Execute("globalThis.__lut = " + PX + ".paletteLUT();");
            Assert.AreEqual(PaletteRows, (int)_px.EvaluateNumber("__lut.length"),
                "The LUT is 25 rows: three rocks x six tiers, then the seven accessory palettes. The " +
                "PNG's row index IS this row number, so a row inserted anywhere but the end renumbers " +
                "every accessory under the shader's feet.");

            var rigRows = new List<string[]>();
            for (int r = 0; r < PaletteRows; r++)
            {
                Assert.AreEqual(PaletteBands, (int)_px.EvaluateNumber("__lut[" + r + "].bands.length"),
                    "LUT row " + r + " is not " + PaletteBands + " bands — bands() cuts a five-step " +
                    "ramp and the PNG is five columns wide.");
                var bands = new string[PaletteBands];
                for (int b = 0; b < PaletteBands; b++)
                    bands[b] = _px.EvaluateString("__lut[" + r + "].bands[" + b + "]").ToLowerInvariant();
                rigRows.Add(bands);
            }

            // ...against the sidecar's published copy
            var palettes = DeckSidecarJson.AsArray(
                DeckSidecarJson.Member(DeckSidecarJson.Parse(File.ReadAllText(Abs(Sidecar))), "PALETTES"));
            Assert.IsNotNull(palettes, "The sidecar has no PALETTES table.");
            Assert.AreEqual(PaletteRows, palettes.Count, "The sidecar publishes a different row count.");

            for (int r = 0; r < PaletteRows; r++)
            {
                var bands = DeckSidecarJson.AsArray(DeckSidecarJson.Member(palettes[r], "bands"));
                Assert.IsNotNull(bands, "sidecar PALETTES[" + r + "] has no bands array.");
                Assert.AreEqual(PaletteBands, bands.Count, "sidecar PALETTES[" + r + "] is not 5 bands.");
                for (int b = 0; b < PaletteBands; b++)
                    Assert.AreEqual(rigRows[r][b], DeckSidecarJson.String(bands[b]).ToLowerInvariant(),
                        "PALETTES[" + r + "].bands[" + b + "] in the sidecar is not what the landed rig " +
                        "computes. The gameplay side reads the sidecar and the bake reads the rig — a " +
                        "drift here is two different palettes shipping under one hash.");
            }

            // ...and against the image the shader taps
            Texture2D png = LoadPng(PalettePng);
            Assert.AreEqual(8, png.width, "CliffPx_palette.png is 8 x 32.");
            Assert.AreEqual(32, png.height, "CliffPx_palette.png is 8 x 32.");

            Color32[] texels = TopDownRgba(png);
            for (int r = 0; r < PaletteRows; r++)
                for (int b = 0; b < PaletteBands; b++)
                {
                    Color32 got = texels[r * png.width + b];
                    Color32 want = Hex(rigRows[r][b]);
                    Assert.IsTrue(got.r == want.r && got.g == want.g && got.b == want.b && got.a == 255,
                        "CliffPx_palette.png row " + r + " band " + b + " decodes to #" +
                        got.r.ToString("x2") + got.g.ToString("x2") + got.b.ToString("x2") + " (a=" +
                        got.a + ") but the rig cuts " + rigRows[r][b] + ". The shader looks every relit " +
                        "colour up in this image, so a row that disagrees with the rig recolours the " +
                        "whole wall.");
                }
        }

        // =====================================================================================
        //  gate 3 — THE LOAD-BEARING ONE: the geometry is still v10's
        // =====================================================================================

        /// <summary>
        /// 🔴 <b>The claim the whole arc rests on.</b> This kit re-authors the cliff's LOOK in the
        /// pixel language while promising its SHAPE is untouched — same field, same seed, same metres.
        /// CoastPlan, the collision contract and the chunk-cut fix all stand on that, so the later
        /// flip PR is a texture swap only while it is true.
        ///
        /// <para><b>It was false once.</b> The first drop failed exactly this gate, and the cause was
        /// not a coefficient: the port ran the form field on the pixel language's own integer hash
        /// instead of cliffRig's, so a parameter-perfect port still put every rib, cleft and bench
        /// somewhere else. A gate that accepted "close enough" would have shipped a different
        /// coastline. Hence exact equality, bit for bit on the float, and NO tolerance to widen: a
        /// near miss here is a finding to report, not a bar to move.</para>
        /// </summary>
        [Test]
        public void TheProfileIsBitIdenticalToTheV10Rig()
        {
            string[] rocks = _v10.EvaluateString(V10 + ".ROCKS.join(',')").Split(',');
            string[] batters = _v10.EvaluateString("Object.keys(" + V10 + ".SLOPES).join(',')").Split(',');

            // The comparator lives in C#: each side computes its own field in its own engine, and the
            // two arrays meet only here. Comparing inside one engine would let a shared closure answer
            // for both halves, which is how a parity check turns into a mirror.
            long texels = 0, differing = 0;
            double worst = 0;
            string worstAt = "-";

            foreach (string rock in rocks)
                foreach (string batter in batters)
                {
                    float[] a = Displacement(_v10, V10, rock, batter);
                    float[] b = Displacement(_px, PX, rock, batter);
                    Assert.AreEqual(a.Length, b.Length,
                        rock + "/" + batter + ": the two profiles are not even the same size.");

                    // ⚠ THE BLANK CONTROL. Two flat fields compare equal and read as perfect parity.
                    // A cliff profile is never flat — measured peak relief here is ~0.7 m.
                    Assert.Greater(Peak(a), 0.05f, "v10 " + rock + "/" + batter + " came back " +
                        "essentially FLAT. The reference side of this comparison is not rendering, so " +
                        "a match below would mean nothing.");
                    Assert.Greater(Peak(b), 0.05f, "px " + rock + "/" + batter + " came back " +
                        "essentially FLAT.");

                    for (int i = 0; i < a.Length; i++)
                        if (BitConverter.SingleToInt32Bits(a[i]) != BitConverter.SingleToInt32Bits(b[i]))
                        {
                            differing++;
                            double d = Math.Abs((double)a[i] - (double)b[i]);
                            if (d > worst) { worst = d; worstAt = rock + "/" + batter; }
                        }
                    texels += a.Length;
                }

            Assert.AreEqual(0, differing,
                differing + " of " + texels + " displacement texels differ from the v10 rig (worst " +
                "|delta| " + worst.ToString("G6") + " at " + worstAt + "). This kit is NOT a drop-in: " +
                "CoastPlan, the collision contract and the chunk-cut fix were all derived from v10's " +
                "field. Do not widen this into a tolerance — a near miss is a different coastline, not " +
                "a rounding error. STOP and report the delta.");

            // ⚠ THE POSITIVE CONTROL. Exact equality is the easiest assertion in the world to pass by
            // accident — two empty arrays, a comparator that never runs, a seed that reaches nothing.
            // Two DIFFERENT rocks must come back different, or the zero above is worth nothing.
            float[] s = Displacement(_v10, V10, rocks[0], batters[0]);
            float[] t = Displacement(_v10, V10, rocks[1], batters[0]);
            int moved = 0;
            for (int i = 0; i < s.Length; i++)
                if (BitConverter.SingleToInt32Bits(s[i]) != BitConverter.SingleToInt32Bits(t[i])) moved++;
            Assert.Greater(moved, s.Length / 2,
                "Two DIFFERENT rocks gave the same profile at " + moved + " of " + s.Length + " texels. " +
                "The comparator above is blind, and its zero proves nothing.");
        }

        // =====================================================================================
        //  gate 4 — the landed rig bakes the landed samples
        // =====================================================================================

        /// <summary>
        /// <b>Bake-is-the-oracle, the intake's version.</b> The kit ships a rig and 25 sample PNGs;
        /// this drives the landed rig and compares its pixels against those files. It is what makes
        /// <c>tex-sample/</c> trustworthy enough to wire a shader against before the flip PR exists.
        ///
        /// <para><b>The <c>_normal</c> law.</b> Four channels match bit for bit. <c>_normal</c> does
        /// not, and the reason is understood and measured: its alpha carries cavity occlusion rather
        /// than coverage, and the shipped PNG has been through a premultiplied-alpha round trip, which
        /// loses precision wherever that alpha is below 255. Measured across all five aspects: every
        /// differing subpixel sits within 1 of the round trip of the rig's own value, none differ at
        /// alpha = 255, and not one alpha byte differs anywhere. So the assertion below is the
        /// MECHANISM, not a widened bar — a genuinely different wall goes straight through it.</para>
        /// </summary>
        [Test]
        public void TheLandedSamplesAreWhatTheLandedRigBakes()
        {
            foreach (string aspect in SampleAspects)
            {
                _px.Execute("globalThis.__bb = " + PX + ".face('sandstone', " + Js(aspect) +
                            ", 1, {slope: 'wall'});");
                _px.Execute("globalThis.__cc = " + PX + ".channels(__bb, {index: true});");

                foreach (string channel in Channels)
                {
                    byte[] rig = _px.EvaluateBytes("__cc[" + Js(channel) + "]");
                    string rel = SamplePath(aspect, channel);
                    Texture2D tex = LoadPng(rel);
                    Assert.AreEqual(FaceW, tex.width, rel + " is not " + FaceW + " wide.");
                    Assert.AreEqual(FaceH, tex.height, rel + " is not " + FaceH + " tall.");

                    Color32[] file = TopDownRgba(tex);
                    Assert.AreEqual(rig.Length / 4, file.Length, rel + ": texel count disagrees.");

                    int alphaMoved = 0, rgbMovedAtOpaque = 0, rgbMoved = 0, unexplained = 0;
                    for (int i = 0; i < file.Length; i++)
                    {
                        byte alpha = rig[i * 4 + 3];
                        if (file[i].a != alpha) alphaMoved++;

                        byte[] got = { file[i].r, file[i].g, file[i].b };
                        for (int k = 0; k < 3; k++)
                        {
                            if (got[k] == rig[i * 4 + k]) continue;
                            rgbMoved++;
                            if (alpha == 255) rgbMovedAtOpaque++;
                            if (Math.Abs(got[k] - PremultiplyRoundTrip(rig[i * 4 + k], alpha)) > 1)
                                unexplained++;
                        }
                    }

                    // Both orientations are reported, because the two ways this can fail need
                    // opposite fixes and look identical from a single number: the rigs emit
                    // TOP-LEFT-origin rows and Unity decodes bottom-origin, so if the SECOND count is
                    // the low one the convention moved, not the rock.
                    Assert.AreEqual(0, alphaMoved, rel + ": " + alphaMoved + " alpha bytes differ from " +
                        "the rig's bake (" + AlphaMismatches(tex.GetPixels32(), rig) + " differ if the " +
                        "rows are read the other way up). Alpha survives a premultiply round trip " +
                        "untouched, so a moved alpha is a moved bake.");

                    Assert.AreEqual(0, rgbMovedAtOpaque, rel + ": " + rgbMovedAtOpaque + " RGB " +
                        "subpixels differ at ALPHA = 255, where the premultiply round trip is exactly " +
                        "lossless. That is a different wall, not an encoding artefact.");

                    if (channel != "_normal")
                        Assert.AreEqual(0, rgbMoved, rel + ": " + rgbMoved + " RGB subpixels differ. " +
                            "Only _normal carries occlusion in its alpha; the other four are " +
                            "coverage-alpha and must be bit-exact.");
                    else
                        Assert.AreEqual(0, unexplained, rel + ": " + unexplained + " of " + rgbMoved +
                            " differing subpixels are NOT within 1 of the premultiplied-alpha round " +
                            "trip of the rig's own value. The known loss in this channel is that round " +
                            "trip and nothing else — anything outside it means the sample and the rig " +
                            "disagree about the rock.");
                }
            }
        }

        /// <summary>
        /// The source-level half of "ship the rig, not the sheets". Both files, because the language
        /// is half the bake: one unseeded call anywhere in either makes the coast unreproducible, and
        /// no pixel comparison can see it in a path this suite never renders.
        /// </summary>
        [Test]
        public void NeitherTheRigNorTheLanguageCarriesHiddenRandomness()
        {
            foreach (string rel in new[] { RigJs, LangJs })
            {
                string source = File.ReadAllText(Abs(rel));
                foreach (string forbidden in new[] { "Math.random", "Date.now", "new Date", "performance.now" })
                    Assert.IsFalse(source.Contains(forbidden),
                        rel + " contains '" + forbidden + "'. This kit ships as a rig rather than as " +
                        "sheets, and that trade is only honest while the bake is a pure function of " +
                        "(rig, config).");
            }
        }

        // =====================================================================================
        //  helpers
        // =====================================================================================

        static string Abs(string repoRelative) =>
            Path.Combine(RigCatalog.RepoRoot, repoRelative.Replace('/', Path.DirectorySeparatorChar));

        static string SamplePath(string aspect, string channel) =>
            KitDir + "/tex-sample/Sandstone_" + aspect + channel + ".png";

        /// <summary>SHA-256 over LF-normalised bytes — see the note on gate 1.</summary>
        static string Sha256Lf(string path)
        {
            byte[] raw = File.ReadAllBytes(path);
            var lf = new List<byte>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == 0x0D && i + 1 < raw.Length && raw[i + 1] == 0x0A) continue;
                lf.Add(raw[i]);
            }
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(lf.ToArray()))
                                   .Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>
        /// The plan-displacement field in metres, read out of the engine as raw bytes. Both rigs
        /// expose the same <c>profile(rock, aspect, opts)</c> and both return a Float32Array, so this
        /// one reader serves both sides of the parity gate — deliberately, since a per-rig reader is
        /// a place for the comparison to acquire a difference of its own.
        /// </summary>
        static float[] Displacement(IRigScriptHost host, string global, string rock, string batter)
        {
            host.Execute("globalThis.__p = " + global + ".profile(" + Js(rock) + ", 'S', {slope: " +
                         Js(batter) + "});");
            byte[] bytes = host.EvaluateBytes(
                "new Uint8Array(__p.disp.buffer, __p.disp.byteOffset, __p.disp.length * 4)");
            var f = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, f, 0, bytes.Length);
            return f;
        }

        static float Peak(float[] v)
        {
            float m = 0;
            foreach (float x in v) { float a = Math.Abs(x); if (a > m) m = a; }
            return m;
        }

        /// <summary>
        /// What a premultiplying encoder does to a straight-alpha subpixel and back: scale into
        /// 8-bit premultiplied, then divide out again. Lossless at alpha 255, total at alpha 0, and
        /// worth about a unit in between — which is exactly the residue measured in the shipped
        /// <c>_normal</c> samples.
        /// </summary>
        static int PremultiplyRoundTrip(byte value, byte alpha)
        {
            if (alpha == 0) return 0;
            int premultiplied = (int)Math.Round(value * alpha / 255.0, MidpointRounding.AwayFromZero);
            return Math.Min(255, (int)Math.Round(premultiplied * 255.0 / alpha, MidpointRounding.AwayFromZero));
        }

        static Texture2D LoadPng(string repoRelative)
        {
            string full = Abs(repoRelative);
            Assert.IsTrue(File.Exists(full), "art present on disk: " + repoRelative);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(ImageConversion.LoadImage(tex, File.ReadAllBytes(full)),
                "PNG decodes: " + repoRelative + ". An LFS pointer that was never smudged decodes as " +
                "nothing — check `git lfs ls-files` before reading anything else into this failure.");
            return tex;
        }

        /// <summary>
        /// The rigs hand back TOP-LEFT-origin rows; Unity textures are bottom-origin, so a decoded
        /// PNG arrives upside down relative to the buffer it is compared with. Same flip every
        /// sibling baker performs, duplicated here rather than reached for, because a test may not
        /// borrow the production path it is supposed to be checking.
        /// </summary>
        static Color32[] TopDownRgba(Texture2D tex)
        {
            Color32[] bottomUp = tex.GetPixels32();
            var topDown = new Color32[bottomUp.Length];
            int w = tex.width, h = tex.height;
            for (int y = 0; y < h; y++)
                Array.Copy(bottomUp, (h - 1 - y) * w, topDown, y * w, w);
            return topDown;
        }

        /// <summary>How many alpha bytes disagree — the cheapest orientation probe there is, since
        /// alpha survives the encoder untouched.</summary>
        static int AlphaMismatches(Color32[] texels, byte[] rig)
        {
            int n = 0;
            for (int i = 0; i < texels.Length; i++) if (texels[i].a != rig[i * 4 + 3]) n++;
            return n;
        }

        static Color32 Hex(string hex)
        {
            string s = hex.StartsWith("#") ? hex.Substring(1) : hex;
            return new Color32(Convert.ToByte(s.Substring(0, 2), 16),
                               Convert.ToByte(s.Substring(2, 2), 16),
                               Convert.ToByte(s.Substring(4, 2), 16), 255);
        }

        static string Js(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
    }
}
