using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The L2 light channels of the village houses (drop 14): <see cref="BuildingLightChannels"/>, the
    /// pure packer, and <see cref="BuildingLightFrame"/>, which reads the rig's G-buffer for it.
    ///
    /// <para>The packer's claim is that it IS the tree rig's mask law, fed a house's normals: the same
    /// key, rim, field, thickness and depth, rounded the same way, in the same channel order. At intake
    /// that was checked byte for byte against the tree rig's own JS on all five houses at all eight
    /// facings (40 of 40 identical). These tests keep the parts of that claim that can drift: the law's
    /// lines in <c>treeIsoRig2.js</c>, the V8 numbers the packer copies, and each channel's
    /// behaviour.</para>
    /// </summary>
    public class BuildingLightChannelsTests
    {
        static readonly double Se = Math.Sin(40 * Math.PI / 180), Ce = Math.Cos(40 * Math.PI / 180);

        static BuildingLightChannels.Camera Facing(double degrees, double scale = 32) =>
            new BuildingLightChannels.Camera(Math.Cos(degrees * Math.PI / 180), Math.Sin(degrees * Math.PI / 180),
                                             Se, Ce, scale);

        // ----------------------------------------------------------------------------------------------
        //  the law is still the tree rig's
        // ----------------------------------------------------------------------------------------------

        [Test]
        public void TheMaskLaw_IsStillTheTreeRigs_LineForLine()
        {
            string tree = File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, "docs/art/rigs/treeIsoRig2.js"));
            string[] law =
            {
                "const nrm = (v) => { const L = Math.hypot(v[0], v[1], v[2]) || 1; return [v[0] / L, v[1] / L, v[2] / L]; };",
                "key: nrm([-0.55, -0.66, 0.52]),",
                "rim: nrm([0.48, -0.28, -0.83]),",
                "const D = new Float32Array(w * h), BIG = 1e6;",
                "if (y > 0) { d = rd(i, D[i - w] + 3); if (x > 0) d = Math.min(d, D[i - w - 1] + 4); if (x < w - 1) d = Math.min(d, D[i - w + 1] + 4); }",
                "for (let i = 0; i < w * h; i++) D[i] /= 3;",
                "const zr = Math.max(1, zmax - zmin);",
                "const RSl = Math.hypot(R[0], R[1]) || 1, RS = [R[0] / RSl, R[1] / RSl];",
                "const TH = new Float32Array(n), tmp = new Float32Array(n), RAD = 6;",
                "const lam = Math.pow(Math.max(0, nx * K[0] + ny * K[1] + nz * K[2]), 1.35);",
                "mFront[i] = clamp(Math.round(lam * 255), 0, 255);",
                "mDepth[i] = clamp(Math.round(((v.z[i] - zmin) / zr) * 255), 0, 255);",
                "const nl = Math.hypot(nx, ny) || 1;",
                "const back = Math.max(0, (nx * RS[0] + ny * RS[1]) / nl);",
                "const fres = Math.pow(1 - clamp(nz, 0, 1), 1.7);",
                "const thick = smooth(4.0, 5.2, TH[i]);",
                "let rim = Math.pow(back, 1.15) * fres * thick * smooth(3.6, 0.8, d);",
                "mRim[i] = clamp(Math.round(rim * 255), 0, 255);",
                "out[i * 4] = res.masks.front[i]; out[i * 4 + 1] = res.masks.rim[i];",
                "out[i * 4 + 2] = res.masks.depth[i]; out[i * 4 + 3] = a;",
                "out[i * 4] = (res.nx[i] * 0.5 + 0.5) * 255; out[i * 4 + 1] = (-res.ny[i] * 0.5 + 0.5) * 255;",
                "out[i * 4 + 2] = (res.nz[i] * 0.5 + 0.5) * 255; out[i * 4 + 3] = 255;",
            };
            string[] gone = law.Where(l => !tree.Contains(l)).ToArray();
            Assert.That(gone, Is.Empty,
                "treeIsoRig2.js no longer states the mask law BuildingLightChannels copies. Re-derive the " +
                "packer from the rig (and re-run the byte-for-byte cross-check) before re-baking a house:\n  " +
                string.Join("\n  ", gone));
        }

        [Test]
        public void TheLights_AreV8sOwnNumbers_NotANaiveRoot()
        {
            // Printed by V8 (17 significant digits) from the tree rig's own nrm and RS. The rim's x is one
            // where √(a² + b²) and Math.hypot part company, so a naive root fails this test.
            Assert.That(BuildingLightChannels.KeyDirection.X, Is.EqualTo(-0.54767729678885491));
            Assert.That(BuildingLightChannels.KeyDirection.Y, Is.EqualTo(0.65721275614662578), "y up: the rig's -0.657…");
            Assert.That(BuildingLightChannels.KeyDirection.Z, Is.EqualTo(0.51780398969128094));
            Assert.That(BuildingLightChannels.RimScreenX, Is.EqualTo(0.86377890089843345));
            Assert.That(BuildingLightChannels.RimScreenY, Is.EqualTo(0.50387102552408625), "y up: the rig's -0.504…");

            Assert.That(BuildingLightChannels.JsHypot(2, 3), Is.EqualTo(3.6055512754639896));
            Assert.That(Math.Sqrt(2 * 2 + 3 * 3), Is.Not.EqualTo(3.6055512754639896), "the pin bites");
            Assert.That(BuildingLightChannels.JsHypot(0.2, 0.7), Is.EqualTo(0.72801098892805183));
            Assert.That(BuildingLightChannels.JsHypot(3, 4), Is.EqualTo(5.0));
            Assert.That(BuildingLightChannels.JsHypot(0, 0), Is.EqualTo(0.0));
            Assert.That(BuildingLightChannels.JsHypot(0, 0, 0), Is.EqualTo(0.0));
        }

        [Test]
        public void TheKey_IsTheLitSpritePathsKey_AtTheRigsPrecision()
        {
            Vector3 key = SpriteLightMath.RigKeyDirection;
            Assert.That(BuildingLightChannels.KeyDirection.X, Is.EqualTo(key.x).Within(1e-6));
            Assert.That(BuildingLightChannels.KeyDirection.Y, Is.EqualTo(key.y).Within(1e-6));
            Assert.That(BuildingLightChannels.KeyDirection.Z, Is.EqualTo(key.z).Within(1e-6));
            Assert.That((float)BuildingLightChannels.KeyExponent, Is.EqualTo(SpriteLightMath.KeyExponent),
                        "the bake's 1.35 is the shader's, in double");
            Assert.That(BuildingLightFrame.CameraElevationDeg, Is.EqualTo(40.0));
        }

        // ----------------------------------------------------------------------------------------------
        //  the view normal
        // ----------------------------------------------------------------------------------------------

        [Test]
        public void ARoof_FacesUpTheScreenAndTowardTheCamera_AtEveryFacing()
        {
            for (int deg = 0; deg < 360; deg += 45)
            {
                BuildingLightChannels.Vector3d v = BuildingLightChannels.ViewNormal(0, 0, 1, Facing(deg));
                Assert.That(v.X, Is.EqualTo(0).Within(1e-12), $"{deg}°");
                Assert.That(v.Y, Is.EqualTo(Ce).Within(1e-12), $"{deg}°");
                Assert.That(v.Z, Is.EqualTo(Se).Within(1e-12), $"{deg}°");
                Assert.That(BuildingLightChannels.MaskByte(BuildingLightChannels.Key(v)), Is.EqualTo(200),
                            $"a roof's key byte at {deg}°: round(((ce·0.657 + se·0.518)^1.35)·255)");
            }
        }

        [Test]
        public void TheViewNormal_IsARotation()
        {
            foreach (double deg in new[] { 0.0, 30.0, 135.0, 290.0 })
            {
                var c = Facing(deg);
                var x = BuildingLightChannels.ViewNormal(1, 0, 0, c);
                var y = BuildingLightChannels.ViewNormal(0, 1, 0, c);
                var z = BuildingLightChannels.ViewNormal(0, 0, 1, c);
                foreach (var v in new[] { x, y, z })
                    Assert.That(v.Length, Is.EqualTo(1).Within(1e-12), $"{deg}°: unit");
                Assert.That(Dot(x, y), Is.EqualTo(0).Within(1e-12), $"{deg}°: x ⟂ y");
                Assert.That(Dot(y, z), Is.EqualTo(0).Within(1e-12), $"{deg}°: y ⟂ z");
                Assert.That(Dot(z, x), Is.EqualTo(0).Within(1e-12), $"{deg}°: z ⟂ x");
                double det = x.X * (y.Y * z.Z - y.Z * z.Y) - x.Y * (y.X * z.Z - y.Z * z.X) + x.Z * (y.X * z.Y - y.Y * z.X);
                Assert.That(det, Is.EqualTo(1).Within(1e-12), $"{deg}°: no mirror");
            }

            // a wall square to the camera leans back by the elevation: down the screen by se, toward us by ce
            var wall = BuildingLightChannels.ViewNormal(0, -1, 0, Facing(0));
            Assert.That(wall.Y, Is.EqualTo(-Se).Within(1e-12));
            Assert.That(wall.Z, Is.EqualTo(Ce).Within(1e-12));
        }

        // ----------------------------------------------------------------------------------------------
        //  the rim, the field and the thickness
        // ----------------------------------------------------------------------------------------------

        [Test]
        public void TheRim_IsOnlyOnTheBackEdgeOfAThickMass()
        {
            var grazingBack = new BuildingLightChannels.Vector3d(BuildingLightChannels.RimScreenX,
                                                                 BuildingLightChannels.RimScreenY, 0);
            Assert.That(BuildingLightChannels.Rim(grazingBack, 0.5, 6), Is.EqualTo(1).Within(1e-12),
                        "grazing, toward the rim, on the edge of a thick mass: full rim");
            Assert.That(BuildingLightChannels.Rim(grazingBack, 3.6, 6), Is.EqualTo(0), "3.6 px in: past the band");
            Assert.That(BuildingLightChannels.Rim(grazingBack, 0.5, 4.0), Is.EqualTo(0), "a mass under 4 px thick carries none");
            Assert.That(BuildingLightChannels.Rim(new BuildingLightChannels.Vector3d(0, 0, 1), 0.5, 6), Is.EqualTo(0),
                        "facing the camera: no fresnel");
            var grazingFront = new BuildingLightChannels.Vector3d(-grazingBack.X, -grazingBack.Y, 0);
            Assert.That(BuildingLightChannels.Rim(grazingFront, 0.5, 6), Is.EqualTo(0), "turned from the rim light");
        }

        [Test]
        public void TheField_IsTheTreeRigsChamfer_InPixels()
        {
            // a 5×5 block in a 9×9 frame: its rings are 1, 2 and 3 px from the outside
            const int w = 9, h = 9;
            var cov = new byte[w * h];
            for (int y = 2; y <= 6; y++) for (int x = 2; x <= 6; x++) cov[y * w + x] = 1;
            float[] f = BuildingLightChannels.DistanceField(cov, w, h);
            Assert.That(f[1 * w + 1], Is.EqualTo(0f), "outside");
            Assert.That(f[2 * w + 2], Is.EqualTo(1f), "the corner");
            Assert.That(f[2 * w + 4], Is.EqualTo(1f), "the edge");
            Assert.That(f[3 * w + 3], Is.EqualTo(2f), "one ring in");
            Assert.That(f[4 * w + 4], Is.EqualTo(3f), "the centre");

            // a frame with nothing uncovered keeps the rig's BIG / 3, stored as a float
            float[] full = BuildingLightChannels.DistanceField(new byte[] { 1, 1, 1, 1 }, 2, 2);
            Assert.That(full, Is.All.EqualTo((float)(1e6 / 3)));
        }

        [Test]
        public void TheThickness_IsTheFieldsMaxOverAThirteenPixelSquare()
        {
            const int w = 25, h = 25;
            var field = new float[w * h];
            field[10 * w + 10] = 5f;
            float[] th = BuildingLightChannels.Thickness(field, w, h);
            Assert.That(th[16 * w + 16], Is.EqualTo(5f), "6 px away on both axes: inside the window");
            Assert.That(th[4 * w + 4], Is.EqualTo(5f));
            Assert.That(th[10 * w + 17], Is.EqualTo(0f), "7 px away: outside");
            Assert.That(th[3 * w + 10], Is.EqualTo(0f));
        }

        // ----------------------------------------------------------------------------------------------
        //  the rounding and the glow
        // ----------------------------------------------------------------------------------------------

        [Test]
        public void TheBytesRound_AsTheRigsArraysDo()
        {
            Assert.That(BuildingLightChannels.JsRound(2.5), Is.EqualTo(3.0), "Math.round: ties up");
            Assert.That(BuildingLightChannels.JsRound(-2.5), Is.EqualTo(-2.0));
            Assert.That(BuildingLightChannels.MaskByte(0.5 / 255), Is.EqualTo(1));
            Assert.That(BuildingLightChannels.MaskByte(2), Is.EqualTo(255));
            Assert.That(BuildingLightChannels.MaskByte(-1), Is.EqualTo(0));

            Assert.That(BuildingLightChannels.ClampedArrayByte(0.5), Is.EqualTo(0), "Uint8ClampedArray: ties to even");
            Assert.That(BuildingLightChannels.ClampedArrayByte(1.5), Is.EqualTo(2));
            Assert.That(BuildingLightChannels.ClampedArrayByte(2.5), Is.EqualTo(2));
            Assert.That(BuildingLightChannels.ClampedArrayByte(double.NaN), Is.EqualTo(0));
            Assert.That(BuildingLightChannels.ClampedArrayByte(300), Is.EqualTo(255));
            Assert.That(BuildingLightChannels.NormalByte(0), Is.EqualTo(128), "127.5 → 128");
            Assert.That(BuildingLightChannels.NormalByte(1), Is.EqualTo(255));
            Assert.That(BuildingLightChannels.NormalByte(-1), Is.EqualTo(0));
        }

        [Test]
        public void TheGlowLevel_IsLuminanceOverTheBrightestWarmStep()
        {
            Assert.That(BuildingLightChannels.LinearLuminance(new Color32(255, 255, 255, 255)), Is.EqualTo(1).Within(1e-12));
            Assert.That(BuildingLightChannels.GlowLevel(BuildingLightChannels.WarmTop), Is.EqualTo(1).Within(1e-12));
            Assert.That(BuildingLightChannels.GlowLevel(new Color32(0, 0, 0, 255)), Is.EqualTo(0));
            Assert.That(BuildingLightChannels.GlowStrength, Is.EqualTo(1.3910132260189516).Within(1e-12));
            Assert.That(BuildingLightChannels.GlowLevel(BuildingLightChannels.GlowColour) * BuildingLightChannels.GlowStrength,
                        Is.EqualTo(1).Within(1e-12),
                        "a level-1 texel lights, through _EmitColor × _EmitStrength, to the rig's brightest window");
        }

        // ----------------------------------------------------------------------------------------------
        //  the pack
        // ----------------------------------------------------------------------------------------------

        const int W = 12, H = 12;

        /// <summary>A 6×6 roof (x, y 3..8) sloping away up the screen, one texel half-transparent and
        /// one glowing at the parlour's full level.</summary>
        static (BuildingLightChannels.GBuffer gb, byte[] albedo) Roof()
        {
            int n = W * H;
            var gb = new BuildingLightChannels.GBuffer
            {
                Width = W, Height = H, Coverage = new byte[n],
                NormalX = new float[n], NormalY = new float[n], NormalZ = new float[n], Depth = new float[n],
                Glow = new byte[n * 4], Source = new byte[n], Camera = Facing(0),
            };
            var albedo = new byte[n * 4];
            for (int y = 3; y <= 8; y++)
                for (int x = 3; x <= 8; x++)
                {
                    int i = y * W + x;
                    gb.Coverage[i] = 1;
                    gb.NormalZ[i] = 1;
                    gb.Depth[i] = 0.1f * y;          // metres; the top row is NEAREST
                    albedo[i * 4] = 90; albedo[i * 4 + 3] = 255;
                }
            albedo[(4 * W + 7) * 4 + 3] = 128;
            int g = 5 * W + 5;
            Color32 top = BuildingLightChannels.WarmTop;
            gb.Glow[g * 4] = top.r; gb.Glow[g * 4 + 1] = top.g; gb.Glow[g * 4 + 2] = top.b; gb.Glow[g * 4 + 3] = 255;
            gb.Source[g] = SpriteLightMath.EmitSourceParlour;
            return (gb, albedo);
        }

        [Test]
        public void EverySheet_IsInTheTreesOrder_OnTheAlbedosPixelsOnly()
        {
            (BuildingLightChannels.GBuffer gb, byte[] albedo) = Roof();
            BuildingLightChannels.Channels ch = BuildingLightChannels.Pack(gb, albedo);

            for (int i = 0; i < W * H; i++)
            {
                int o = i * 4;
                if (gb.Coverage[i] == 0)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        Assert.That(ch.Mask[o + c], Is.EqualTo(0), $"mask texel {i}");
                        Assert.That(ch.Normal[o + c], Is.EqualTo(0), $"normal texel {i}");
                        Assert.That(ch.Emit[o + c], Is.EqualTo(0), $"emit texel {i}");
                    }
                    continue;
                }
                Assert.That(ch.Mask[o + 0], Is.EqualTo(200), "R: the roof's key");
                Assert.That(ch.Mask[o + 1], Is.EqualTo(0), "G: a 6 px block is too thin to rim (the tree's RULE 3)");
                Assert.That(ch.Mask[o + 3], Is.EqualTo(albedo[o + 3]), "A: the albedo's own alpha");
                Assert.That(ch.Normal[o + 0], Is.EqualTo(128));
                Assert.That(ch.Normal[o + 1], Is.EqualTo(BuildingLightChannels.NormalByte(Ce)));
                Assert.That(ch.Normal[o + 2], Is.EqualTo(BuildingLightChannels.NormalByte(Se)));
                Assert.That(ch.Normal[o + 3], Is.EqualTo(255));
            }
            Assert.That(SpriteLightMath.MaskKey, Is.EqualTo(0));
            Assert.That(SpriteLightMath.MaskRim, Is.EqualTo(1));
            Assert.That(SpriteLightMath.MaskDepth, Is.EqualTo(2));
            Assert.That(SpriteLightMath.MaskCoverage, Is.EqualTo(3));
            Assert.That(ch.Mask[(4 * W + 7) * 4 + 3], Is.EqualTo(128), "the half-transparent texel keeps its alpha");
        }

        [Test]
        public void TheDepthByte_IsNearAt255AndFarAt0()
        {
            (BuildingLightChannels.GBuffer gb, byte[] albedo) = Roof();
            BuildingLightChannels.Channels ch = BuildingLightChannels.Pack(gb, albedo);
            byte B(int x, int y) => ch.Mask[(y * W + x) * 4 + 2];
            Assert.That(B(4, 3), Is.EqualTo(255), "the nearest row");
            Assert.That(B(4, 8), Is.EqualTo(0), "the farthest row");
            Assert.That(B(4, 5), Is.EqualTo(153), "0.6 of the 16 px span");

            // a facing shallower than a pixel is not stretched across the byte: the span floors at 1 px
            for (int i = 0; i < W * H; i++) gb.Depth[i] = gb.Coverage[i] != 0 ? (i / W == 3 ? 0f : 0.01f) : 0f;
            ch = BuildingLightChannels.Pack(gb, albedo);
            Assert.That(B(4, 3), Is.EqualTo(82), "0.32 px of a 1 px floor");
            Assert.That(B(4, 5), Is.EqualTo(0));
        }

        [Test]
        public void TheEmitterByte_IsOnlyWhereTheRigGlows_ThroughTheOneEncoder()
        {
            (BuildingLightChannels.GBuffer gb, byte[] albedo) = Roof();
            BuildingLightChannels.Channels ch = BuildingLightChannels.Pack(gb, albedo);
            int g = (5 * W + 5) * 4;
            Assert.That(ch.Emit[g], Is.EqualTo(SpriteLightMath.EncodeEmit(1f, SpriteLightMath.EmitSourceParlour)));
            Assert.That(ch.Emit[g], Is.EqualTo(SpriteLightMath.EmitSourceParlour * 32 + 31));
            Assert.That(ch.Emit[g + 1] | ch.Emit[g + 2] | ch.Emit[g + 3], Is.EqualTo(0), "the byte lives in R alone");
            Assert.That(ch.Emit.Where((b, i) => b != 0 && i != g), Is.Empty, "nowhere else glows");
            Assert.That(ch.GlowTexels, Is.EqualTo(1));
            Assert.That(ch.MaxLevel, Is.EqualTo(SpriteLightMath.EmitLevelMax));
        }

        [Test]
        public void ALightFrameThatCoversOtherPixels_IsRefused()
        {
            (BuildingLightChannels.GBuffer gb, byte[] albedo) = Roof();
            albedo[(6 * W + 6) * 4 + 3] = 0;
            var ex = Assert.Throws<InvalidOperationException>(() => BuildingLightChannels.Pack(gb, albedo));
            StringAssert.Contains("covers different pixels", ex.Message);
            StringAssert.Contains("(6, 6)", ex.Message);
        }

        // ----------------------------------------------------------------------------------------------
        //  the frame, on the real rig
        // ----------------------------------------------------------------------------------------------

        [Test]
        public void TheHousesLightFrame_IsTheAlbedosPixels_AndItsGlowIsTheRigsOwnLaw()
        {
            VillageBuildingKit.Build cottage = VillageBuildingKit.FindBuild("sageCottage")
                ?? throw new AssertionException("VillageBuildingKit has no sageCottage.");
            RigEntry house = RigCatalog.Get(cottage.RigKey);
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigGeometry geo = RigCatalog.Install(host, house);
            BuildingLightFrame.Install(host);

            string opts = VillageBuildingBakeMenu.BaseOptionsLiteralFor(cottage, house.GlobalName);
            byte[] albedo = host.EvaluateBytes($"{house.GlobalName}.render(3,{opts})");
            int w = geo.Width, h = geo.Height;

            BuildingLightChannels.GBuffer gb = BuildingLightFrame.Capture(host, house.GlobalName, "3", opts, w, h, "sageCottage");
            BuildingLightChannels.Channels ch = BuildingLightChannels.Pack(gb, albedo);   // refuses on any coverage mismatch

            Assert.That(ch.GlowTexels, Is.GreaterThan(0), "the cottage's windows glow at night with its lights on");
            Assert.That(ch.MaxLevel, Is.InRange(1, SpriteLightMath.EmitLevelMax));
            int covered = gb.Coverage.Count(b => b != 0);
            int rimmed = Enumerable.Range(0, w * h).Count(i => ch.Mask[i * 4 + 1] != 0);
            Assert.That(rimmed, Is.InRange(1, covered / 4), "a back rim on the silhouette, not across the face");
        }

        // ----------------------------------------------------------------------------------------------
        //  the baker refuses a contract drift
        // ----------------------------------------------------------------------------------------------

        [Test]
        public void TheBaker_RefusesAContractDrift_AFrameOffTheAlbedosPixels()
        {
            using IRigScriptHost host = CottageHost(out string g, out string opts, out RigGeometry geo);
            var ex = Assert.Throws<InvalidOperationException>(
                () => BuildingLightFrame.Capture(host, g, "3", opts, geo.Width + 1, geo.Height, "sageCottage"));
            StringAssert.Contains("one for one", ex.Message);
        }

        [Test]
        public void TheBaker_RefusesAContractDrift_AGlowThatIsNotTheCopiedLaw()
        {
            // The emit sheet stores a LEVEL and a SOURCE, and the shader turns them back into the rig's warm
            // glow through SpriteLightMath's law. That is honest only while the rig's own relight still draws
            // that law, so a relight that draws anything else (here: every red byte nudged by one) must stop
            // the bake rather than bake a sheet the shader then glows wrongly.
            using IRigScriptHost host = CottageHost(out string g, out string opts, out RigGeometry geo);
            host.Execute(
                $"(function(){{const R={g}, orig=R.relight; R.relight=function(fr,sky,o){{" +
                "const out=orig.apply(this,arguments); for(let i=0;i<fr.W*fr.H;i++) if(fr.gb.a[i]) out[i*4]^=1; " +
                "return out;};})()");
            var ex = Assert.Throws<InvalidOperationException>(
                () => BuildingLightFrame.Capture(host, g, "3", opts, geo.Width, geo.Height, "sageCottage"));
            StringAssert.Contains("is not the emitter law this bake copies", ex.Message);
        }

        [Test]
        public void TheChannelSheets_AreNamedOnce_AndImportAsData()
        {
            // The baker writes the suffixes, the kit reads them and the importer keys its data settings off
            // them: three spellings of one fact, held together here.
            Assert.That(BuildingRigBaker.MaskSuffix,
                        Is.EqualTo(VillageBuildingKit.SuffixOf(VillageBuildingKit.Channel.Mask)));
            Assert.That(BuildingRigBaker.NormalSuffix,
                        Is.EqualTo(VillageBuildingKit.SuffixOf(VillageBuildingKit.Channel.Normal)));
            Assert.That(BuildingRigBaker.EmitSuffix,
                        Is.EqualTo(VillageBuildingKit.SuffixOf(VillageBuildingKit.Channel.Emit)));

            foreach (VillageBuildingKit.Channel c in VillageBuildingKit.LightChannels)
                Assert.IsTrue(ArtImportPipeline.IsDataChannel(VillageBuildingKit.SheetPath("school", c)),
                              $"the {c} sheet would import as colour: sRGB-decoded and alpha-bled");
            Assert.IsFalse(ArtImportPipeline.IsDataChannel(VillageBuildingKit.SheetPath("school")),
                           "the albedo is colour");
        }

        [Test]
        public void TheLitHouseMaterial_GlowsInTheRigsOwnColour_AtItsOwnStrength()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(VillageBuildingCatalog.LitMaterialPath);
            Assert.IsNotNull(mat, $"{VillageBuildingCatalog.LitMaterialPath} is not on disk");
            Assert.AreEqual("HiddenHarbours/LitSprite", mat.shader.name);

            Assert.AreEqual((float)BuildingLightChannels.GlowStrength, mat.GetFloat("_EmitStrength"), 1e-6f,
                            "the house's glow strength is the rig's own: its brightest warm step's luminance " +
                            "over #ffc673's");
            Color32 colour = mat.GetColor("_EmitColor");
            Assert.AreEqual(BuildingLightChannels.GlowColour, colour, "the house's glow colour is the rig's #ffc673");

            // Both flags ship OFF: only a renderer whose binder bound the sheets turns them on, so the
            // material on its own draws what the sprite path always drew.
            Assert.AreEqual(0f, mat.GetFloat(SpriteLightBinding.ChannelsProperty));
            Assert.AreEqual(0f, mat.GetFloat(SpriteLightBinding.EmitChannelsProperty));

            // One sun lights the coast: a house spends the dials every lit family spends, the same rule
            // TreeSunLightingTests holds the trees and shore plants to. A house-only value is a deliberate
            // look exception, to be argued rather than inherited.
            var shrub = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/LitShrub.mat");
            Assert.IsNotNull(shrub, "LitShrub.mat, the shared dials, is not on disk");
            foreach (string dial in new[] { "_LightResponse", "_SunKeyStrength", "_SunRimStrength", "_KeyRelight",
                                            "_RimSteerAmount", "_LightFrontBand", "_LightDepthBias" })
                Assert.AreEqual(shrub.GetFloat(dial), mat.GetFloat(dial), 1e-6f,
                                $"the lit house material's {dial} has drifted from LitShrub.mat's");
            Assert.AreEqual(shrub.GetColor("_SunKeyColor"), mat.GetColor("_SunKeyColor"),
                            "one sun, one colour: the house's sun colour has drifted from the shared one");
        }

        /// <summary>A host carrying the returned house and the light-frame helper, with the sage cottage's
        /// options: the build whose facing 3 has lit windows.</summary>
        static IRigScriptHost CottageHost(out string global, out string opts, out RigGeometry geo)
        {
            VillageBuildingKit.Build cottage = VillageBuildingKit.FindBuild("sageCottage")
                ?? throw new AssertionException("VillageBuildingKit has no sageCottage.");
            RigEntry house = RigCatalog.Get(cottage.RigKey);
            IRigScriptHost host = RigScriptHostFactory.Create();
            try
            {
                geo = RigCatalog.Install(host, house);
                BuildingLightFrame.Install(host);
                global = house.GlobalName;
                opts = VillageBuildingBakeMenu.BaseOptionsLiteralFor(cottage, global);
                return host;
            }
            catch
            {
                host.Dispose();
                throw;
            }
        }

        static double Dot(in BuildingLightChannels.Vector3d a, in BuildingLightChannels.Vector3d b) =>
            a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }
}
