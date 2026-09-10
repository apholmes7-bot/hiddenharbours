using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HiddenHarbours.Art;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The C# flock is the rig's flock — asked of V8, not assumed.</b>
    ///
    /// <para><see cref="SeagullFlockMath.Flock"/> is a hand transcription of
    /// <c>flock(n, t, {seed, radius})</c> in <c>docs/art/rigs/seagullIsoRig.js</c>. A transcription
    /// that is nearly right is the worst outcome available: nine random draws per bird, and a port
    /// that takes them in a different order produces a flock that wheels, breathes and swoops
    /// perfectly plausibly while being a DIFFERENT flock from bird 1 onward. Nothing on screen would
    /// ever tell you. So this fixture runs the art director's own file in V8 and compares.</para>
    ///
    /// <para><b>Numbers cross the language boundary as BITS, never as text.</b> The packer writes each
    /// double through a <c>DataView</c> as two hex words, so a difference this fixture reports is a
    /// difference in the arithmetic — never in how V8 formatted a decimal or how <c>double.Parse</c>
    /// read it back. That is what makes the exact claims below honest.</para>
    ///
    /// <para><b>What is exact and what is not.</b> <c>scale</c>, <c>frame</c>, <c>dir</c> and
    /// <c>anim</c> come from the RNG stream and integer arithmetic alone — no transcendentals — so
    /// they are compared BIT FOR BIT and any drift is a port bug. <c>x</c>, <c>y</c>, <c>z</c> and
    /// <c>heading</c> pass through <c>sin</c>/<c>cos</c>/<c>atan2</c>, where V8's libm and .NET's are
    /// entitled to disagree in the last place; those carry a <see cref="UlpBudget"/>-ULP allowance,
    /// and the fixture also counts how many landed exactly so a sudden fleet-wide drift cannot hide
    /// inside the budget.</para>
    ///
    /// <para>🔴 The two that pay for the fixture: <see cref="SeedZeroIsSeedSevenInBothLanguages"/> —
    /// the rig writes <c>opts.seed || 7</c>, so seed 0 is NOT its own stream and a port that treats it
    /// as one is silently wrong for exactly one caller; and
    /// <see cref="TheDefaultRadiusIsTheSidecarsOwnInBothLanguages"/> — the rig writes
    /// <c>opts.radius != null</c> where the port writes <c>radiusMetres &gt; 0.0</c>, so the two agree
    /// on "absent" and disagree on "zero". <b>Never pass <c>radius: 0</c> to the JS side</b>: JS would
    /// fly a wheel of radius zero and C# would fly the sidecar's six metres, and the divergence is the
    /// test's own doing, not the port's.</para>
    /// </summary>
    [TestFixture]
    public sealed class SeagullFlockParityTests
    {
        const string RigPath = "docs/art/rigs/seagullIsoRig.js";
        const string SidecarPath = "docs/art/rigs/gameplay/seagullIsoRig.gameplay.json";

        /// <summary>Last-place allowance on the values that pass through a transcendental. Four is
        /// generous for one <c>sin</c>/<c>cos</c> and a multiply; a port bug misses by whole
        /// metres, never by four ULPs.</summary>
        const long UlpBudget = 4;

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);
        static byte[] RigBytes() => File.ReadAllBytes(Full(RigPath));
        static string SidecarText() => File.ReadAllText(Full(SidecarPath));

        static SeagullSidecarRead ReadKit() =>
            SeagullSidecarReader.Read(SidecarText(), SidecarPath, RigBytes());

        static SeagullVisualDef Def()
        {
            var def = Resources.Load<SeagullVisualDef>(SeagullVisualDef.ResourcesPath);
            Assert.IsNotNull(def, "No SeagullVisualDef in Resources. The gull's numbers reach the " +
                                  "game through this asset; without it there is nothing to compare.");
            Assert.IsTrue(def.TryValidate(out string error), error);
            return def;
        }

        static SeagullFlockMath.SeagullFlockTuning Tuning() => Def().Behaviour.FlockTuning;

        // ── bits ────────────────────────────────────────────────────────────────────────────────

        static long BitsOf(double d) => BitConverter.DoubleToInt64Bits(d);

        /// <summary>The IEEE754 bit pattern re-ordered so that adjacent doubles are adjacent longs,
        /// negatives included. Subtracting two of these is the ULP distance.</summary>
        static long Ordered(double d)
        {
            long b = BitConverter.DoubleToInt64Bits(d);
            return b >= 0 ? b : long.MinValue - b;
        }

        /// <summary>How many representable doubles lie between two values. Zero when they are equal,
        /// which deliberately makes +0 and -0 the same value rather than 2^63 apart.</summary>
        static long Ulps(double a, double b)
        {
            if (a == b) return 0;
            if (double.IsNaN(a) || double.IsNaN(b)) return long.MaxValue;
            long x = Ordered(a), y = Ordered(b);
            if (x < y) { long t = x; x = y; y = t; }
            ulong d = unchecked((ulong)x - (ulong)y);
            return d > long.MaxValue ? long.MaxValue : (long)d;
        }

        static string Text(double d) => d.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>Running tally over one comparison, so the fixture can report the worst drift and
        /// prove that most of the fleet landed exactly rather than merely inside the budget.</summary>
        sealed class Drift
        {
            public long Worst;
            public string Where = "nothing";
            public int Exact, Total;
            public override string ToString() =>
                $"{Exact}/{Total} exact, worst {Worst} ULP at {Where}";
        }

        // ── the V8 side ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Packs <c>flock()</c>'s output as text without ever formatting a number: each double goes
        /// through a little-endian <c>DataView</c> and comes out as "high:low" hex words. Endianness
        /// is stated explicitly so the fixture does not inherit the host machine's.
        /// </summary>
        const string PackerJs = @"
globalThis.__gullPack = function (n, t, opts) {
  var dv = new DataView(new ArrayBuffer(8));
  function bits(v) {
    dv.setFloat64(0, v, true);
    return dv.getUint32(4, true).toString(16) + ':' + dv.getUint32(0, true).toString(16);
  }
  var a = SeagullIso.flock(n, t, opts), out = [];
  for (var i = 0; i < a.length; i++) {
    var b = a[i];
    out.push([bits(b.x), bits(b.y), bits(b.z), bits(b.heading),
              b.dir, b.anim, b.frame, bits(b.scale)].join(','));
  }
  return out.join(';');
};";

        static IRigScriptHost OpenRig()
        {
            IRigScriptHost host = RigScriptHostFactory.Create();
            try
            {
                RigCatalog.InstallModule(host, RigCatalog.Get(SeagullBaker.RigKey));
                Assert.IsTrue(host.EvaluateBool("typeof SeagullIso.flock === 'function'"),
                              "The seagull rig ran but exposes no flock(). The port has nothing to " +
                              "be a port OF, and every parity claim below would be vacuous.");
                host.Execute(PackerJs);
            }
            catch
            {
                host.Dispose();
                throw;
            }
            return host;
        }

        readonly struct JsBird
        {
            public readonly double X, Y, Z, Heading, Scale;
            public readonly int Dir, Frame;
            public readonly string Anim;

            public JsBird(double x, double y, double z, double heading,
                          int dir, string anim, int frame, double scale)
            { X = x; Y = y; Z = z; Heading = heading; Dir = dir; Anim = anim; Frame = frame; Scale = scale; }
        }

        static double FromJsBits(string word)
        {
            int colon = word.IndexOf(':');
            Assert.Greater(colon, 0, $"Malformed packed double '{word}'.");
            ulong hi = ulong.Parse(word.Substring(0, colon), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            ulong lo = ulong.Parse(word.Substring(colon + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return BitConverter.Int64BitsToDouble(unchecked((long)((hi << 32) | lo)));
        }

        /// <summary>Runs the art director's own <c>flock()</c>. <paramref name="optsJs"/> is a JS object
        /// literal, written out verbatim, so a test can express "no radius key at all" — which is a
        /// different thing from radius zero and the whole point of
        /// <see cref="TheDefaultRadiusIsTheSidecarsOwnInBothLanguages"/>.</summary>
        static JsBird[] JsFlock(IRigScriptHost host, int count, double timeMilliseconds, string optsJs)
        {
            string call = $"__gullPack({count.ToString(CultureInfo.InvariantCulture)}, " +
                          $"{Text(timeMilliseconds)}, {optsJs})";
            string packed = host.EvaluateString(call);
            if (string.IsNullOrEmpty(packed)) return Array.Empty<JsBird>();

            string[] rows = packed.Split(';');
            var birds = new JsBird[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                string[] f = rows[i].Split(',');
                Assert.AreEqual(8, f.Length, $"flock() bird {i} packed {f.Length} fields, not 8. " +
                                             "The rig changed shape and the port has not followed.");
                birds[i] = new JsBird(FromJsBits(f[0]), FromJsBits(f[1]), FromJsBits(f[2]),
                                      FromJsBits(f[3]),
                                      int.Parse(f[4], CultureInfo.InvariantCulture), f[5],
                                      int.Parse(f[6], CultureInfo.InvariantCulture),
                                      FromJsBits(f[7]));
            }
            return birds;
        }

        static SeagullFlockMath.SeagullFlockBird[] CsFlock(int count, double timeMilliseconds,
                                                           int seed, double radiusMetres)
        {
            var into = new SeagullFlockMath.SeagullFlockBird[count];
            int wrote = SeagullFlockMath.Flock(count, timeMilliseconds, seed, radiusMetres,
                                               Tuning(), into);
            Assert.AreEqual(count, wrote, "The port short-filled the caller's buffer.");
            return into;
        }

        static void Near(Drift d, string field, int bird, double js, double cs)
        {
            long u = Ulps(js, cs);
            d.Total++;
            if (u == 0) d.Exact++;
            if (u > d.Worst) { d.Worst = u; d.Where = $"{field} of bird {bird}"; }
            Assert.LessOrEqual(u, UlpBudget,
                $"bird {bird} {field}: V8 {Text(js)}, port {Text(cs)} — {u} ULPs apart, budget " +
                $"{UlpBudget}. A libm disagreeing about sin costs one place; a draw taken out of " +
                "order costs metres.");
        }

        /// <summary>The whole comparison, one call. Exact on everything that can be exact, ULP-bounded
        /// on everything that passes through a transcendental, and non-vacuous: it insists that some
        /// of the reals landed EXACTLY, so a fleet-wide drift cannot sit quietly inside the budget.
        /// </summary>
        static void AssertSameFlock(JsBird[] js, SeagullFlockMath.SeagullFlockBird[] cs, string what)
        {
            Assert.AreEqual(js.Length, cs.Length, $"{what}: the two flocks are not even the same size.");
            Assert.Greater(js.Length, 0, $"{what}: nothing was compared.");

            var d = new Drift();
            for (int i = 0; i < js.Length; i++)
            {
                Assert.AreEqual(js[i].Anim, SeagullFlockMath.AnimId(cs[i].Anim),
                    $"{what}: bird {i} is flying a different strip. swoop is bird 0's alone and glide " +
                    "is an oscillator — either branch taken differently is a port bug, not a rounding.");
                Assert.AreEqual(js[i].Frame, cs[i].Frame,
                    $"{what}: bird {i} is on a different frame of {js[i].Anim}. The frame index reads " +
                    "t in MILLISECONDS, not seconds; a port that divides first is exactly this wrong.");
                Assert.AreEqual(js[i].Dir, cs[i].Dir,
                    $"{what}: bird {i} is facing a different row of the sheet.");
                Assert.AreEqual(BitsOf(js[i].Scale), BitsOf(cs[i].Scale),
                    $"{what}: bird {i} drew a different size jitter ({Text(js[i].Scale)} vs " +
                    $"{Text(cs[i].Scale)}). It is the NINTH draw of the bird's block and nothing but " +
                    "add and multiply touches it, so this can only mean the RNG stream diverged.");

                Near(d, "x", i, js[i].X, cs[i].X);
                Near(d, "y", i, js[i].Y, cs[i].Y);
                Near(d, "z", i, js[i].Z, cs[i].Z);
                Near(d, "heading", i, js[i].Heading, cs[i].Heading);
            }

            Assert.Greater(d.Exact, 0,
                $"{what}: not one of {d.Total} values matched V8 exactly ({d}). Everything sat inside " +
                "the ULP budget, which is what a systematically shifted transcription looks like.");
            TestContext.WriteLine($"[gull-parity] {what}: {d}");
        }

        // ── 1. the port is the rig ──────────────────────────────────────────────────────────────

        /// <summary>
        /// The charter's bullet, literally: <i>port flock(n, t, {seed, radius}) to C#
        /// deterministically, bit-for-bit vs V8 on one seed</i>. Seven is the seed the rig defaults to
        /// and the seed <c>GullConfig.Default</c> ships, so this is the flock the game actually flies.
        /// </summary>
        [Test]
        public void TheWheelIsTheRigsWheelOnTheSeedTheGameShips()
        {
            using IRigScriptHost host = OpenRig();
            const double t = 12345.0;
            JsBird[] js = JsFlock(host, 9, t, "{seed:7}");
            var cs = CsFlock(9, t, 7, 0.0);
            AssertSameFlock(js, cs, "seed 7, nine birds, t=12345 ms");
        }

        /// <summary>
        /// 🔴 <c>const rng = mulberry((opts.seed || 7) * 2654435761)</c>. Zero is falsy, so the rig has
        /// no seed 0 — it has a second way of spelling seed 7. A port that writes
        /// <c>seed * 2654435761</c> straight through gets a whitened zero, which is a perfectly
        /// well-behaved RNG stream that simply is not the rig's.
        /// </summary>
        [Test]
        public void SeedZeroIsSeedSevenInBothLanguages()
        {
            using IRigScriptHost host = OpenRig();
            const double t = 6000.0;
            AssertSameFlock(JsFlock(host, 7, t, "{seed:0}"), CsFlock(7, t, 0, 0.0), "seed 0");
            AssertSameFlock(JsFlock(host, 7, t, "{seed:7}"), CsFlock(7, t, 0, 0.0),
                            "V8 seed 7 against the port's seed 0");

            // The control: some other seed is genuinely a different flock, so the agreement above is
            // the || 7 rule and not the port ignoring its seed argument.
            var one = CsFlock(7, t, 1, 0.0);
            var zero = CsFlock(7, t, 0, 0.0);
            Assert.AreNotEqual(BitsOf(zero[0].Scale), BitsOf(one[0].Scale),
                "Seed 1 drew the same size jitter as seed 0 — the port is not reading its seed at all.");
        }

        /// <summary>
        /// 🔴 The rig writes <c>opts.radius != null ? opts.radius : FLOCK.radius_m</c>; the port writes
        /// <c>radiusMetres &gt; 0.0 ? radiusMetres : tuning.RadiusMetres</c>. The two agree on ABSENT
        /// and disagree on ZERO — so the fixture asks JS for the absent case only, and asks the port
        /// for both spellings of "take the sidecar's".
        /// <b>Never pass <c>radius: 0</c> to the JS side.</b> V8 would fly a wheel of radius zero, the
        /// port would fly six metres, and the divergence would be this test's own doing.
        /// </summary>
        [Test]
        public void TheDefaultRadiusIsTheSidecarsOwnInBothLanguages()
        {
            using IRigScriptHost host = OpenRig();
            const double t = 3200.0;
            JsBird[] js = JsFlock(host, 6, t, "{seed:5}");            // no radius key at all
            AssertSameFlock(js, CsFlock(6, t, 5, 0.0), "absent radius against the port's 0.0");
            AssertSameFlock(js, CsFlock(6, t, 5, -1.0), "absent radius against the port's -1.0");
            AssertSameFlock(js, CsFlock(6, t, 5, Tuning().RadiusMetres),
                            "absent radius against the sidecar's radius spelled out");
        }

        /// <summary>An explicit radius reaches both wheels, and moves them.</summary>
        [Test]
        public void AnExplicitRadiusFliesTheSameWheelInBothLanguages()
        {
            using IRigScriptHost host = OpenRig();
            const double t = 9100.0;
            const double radius = 11.5;
            Assert.AreNotEqual(radius, Tuning().RadiusMetres,
                "Pick a radius the sidecar does not already ship, or the test cannot tell the two paths apart.");

            var cs = CsFlock(6, t, 3, radius);
            AssertSameFlock(JsFlock(host, 6, t, "{seed:3, radius:11.5}"), cs, "seed 3, radius 11.5 m");

            var wide = CsFlock(6, t, 3, 0.0);
            Assert.AreNotEqual(BitsOf(wide[0].X), BitsOf(cs[0].X),
                "The explicit radius changed nothing — the port is dropping the argument, and the " +
                "agreement above would hold for any value at all.");
        }

        /// <summary>
        /// One seed, five moments, and all three airborne branches taken. <c>swoop</c> is bird 0's
        /// alone and only inside its duty window, <c>glide</c> is a sine oscillator against the duty
        /// cycle, and <c>fly</c> is what is left — this sweep visits all three, so the agreement is
        /// about the branch logic and not about one lucky sample of it.
        /// </summary>
        [Test]
        public void TheWheelTurnsThroughTimeTogether()
        {
            using IRigScriptHost host = OpenRig();
            double[] moments = { 0.0, 250.0, 1000.5, 7777.25, 60000.0 };
            var anims = new HashSet<string>(StringComparer.Ordinal);
            var frames = new HashSet<string>(StringComparer.Ordinal);

            foreach (double t in moments)
            {
                JsBird[] js = JsFlock(host, 5, t, "{seed:21}");
                AssertSameFlock(js, CsFlock(5, t, 21, 0.0), $"seed 21 at t={Text(t)} ms");
                foreach (JsBird b in js) { anims.Add(b.Anim); frames.Add(b.Anim + b.Frame); }
            }

            CollectionAssert.AreEquivalent(new[] { "fly", "glide", "swoop" }, anims,
                "The sweep did not visit all three airborne branches, so it did not test the branch " +
                "that picks between them. Choose different moments rather than weakening this.");
            Assert.Greater(frames.Count, 1,
                "Every bird sat on the same frame all sweep — the frame index is not moving, and the " +
                "agreement above says nothing about how t reaches it.");
        }

        /// <summary>
        /// The control for everything above: two seeds are two flocks, and
        /// <see cref="AssertSameFlock"/> is capable of saying so. Without this, a comparison that
        /// silently passed on any pair of inputs would look exactly like a correct port.
        /// </summary>
        [Test]
        public void TheComparisonCanTellTwoFlocksApart()
        {
            using IRigScriptHost host = OpenRig();
            const double t = 4000.0;
            JsBird[] eight = JsFlock(host, 6, t, "{seed:8}");
            var cs8 = CsFlock(6, t, 8, 0.0);
            AssertSameFlock(eight, cs8, "seed 8");

            JsBird[] seven = JsFlock(host, 6, t, "{seed:7}");
            Assert.Throws<AssertionException>(() => AssertSameFlock(seven, cs8, "seed 7 against port seed 8"),
                "V8's seed-7 flock compared EQUAL to the port's seed-8 flock. The comparison is " +
                "vacuous and every parity claim in this fixture is worthless.");
        }

        // ── 2. the numbers the port flies are the drop's own ────────────────────────────────────

        /// <summary>
        /// The premise every "absent radius" claim above rests on. The JS reads its own <c>FLOCK</c>
        /// and <c>ANIMS</c> tables straight out of the rig file; the port reads the sidecar by way of
        /// <see cref="SeagullVisualDef"/>. They are two copies of one drop, and if a re-export ever
        /// moves one without the other, the parity tests would fail somewhere confusing — so the
        /// divergence is caught here, by name.
        ///
        /// <para><b>⚠ The two sides are not the same precision, and the comparison must say so.</b>
        /// A JS number is a <c>double</c>; every non-integer FLOCK field of
        /// <see cref="SeagullVisualDef"/> is a serialized <c>float</c>. Of the rig's own numbers only
        /// <c>glide_duty</c> 0.45 and <c>spacing_m</c> 1.6 fail to survive that round trip: 0.45 comes
        /// back as 0.44999998807907104, which is simply the nearest float to it. So the answerable question
        /// is not "are these two doubles equal" (they cannot be) but <b>"does the def carry the
        /// NEAREST FLOAT to the number the rig states"</b>, and that is asked exactly, with no
        /// tolerance to drift. What this guard exists to catch — a re-export that moved a tunable,
        /// or a port that typed its own copy — misses by orders of magnitude more than one ULP, and
        /// the negative control at the end of the method proves the cast did not blunt it.</para>
        ///
        /// <para>That the remaining 1.2e-8 does not change the FLIGHT is not assumed here, it is
        /// measured by the siblings above: they drive the port from this same float-widened tuning
        /// and V8 from the rig's own doubles, and agree on every anim, frame and dir.</para>
        /// </summary>
        [Test]
        public void TheTuningThePortFliesIsTheRigsOwnFlockBlock()
        {
            using IRigScriptHost host = OpenRig();
            SeagullFlockMath.SeagullFlockTuning t = Tuning();
            Assert.IsTrue(t.IsUsable, "The committed def does not make a usable tuning; the port would " +
                                      "be flying a flock of zeroes and V8 would not.");

            void Same(string expression, double mine)
            {
                double theirs = host.EvaluateNumber(expression);

                // Exact, at FLOAT precision — see the remark. A double-precision tolerance here is a
                // number about the SIDECAR being asked of a FLOAT FIELD, and 1e-9 duly reddened CI on
                // glide_duty 0.45 (#831) for no fault of the def's.
                Assert.AreEqual((float)theirs, (float)mine, 0f,
                    $"{expression}: the rig says {Text(theirs)}, the def carries {Text(mine)} — and " +
                    $"they differ once both are narrowed to the float the def stores ({(float)theirs} " +
                    $"vs {(float)mine}). Re-export the sidecar and rebuild SeagullVisualDef.asset — " +
                    "the port must not carry its own copy of a tunable (rule 6).");
            }

            Same("SeagullIso.FLOCK.radius_m", t.RadiusMetres);
            Same("SeagullIso.FLOCK.period_s[0]", t.PeriodMinSeconds);
            Same("SeagullIso.FLOCK.period_s[1]", t.PeriodMaxSeconds);
            Same("SeagullIso.FLOCK.alt_m[0]", t.AltitudeMinMetres);
            Same("SeagullIso.FLOCK.alt_m[1]", t.AltitudeMaxMetres);
            Same("SeagullIso.FLOCK.swoop_every_s[0]", t.SwoopEveryMinSeconds);
            Same("SeagullIso.FLOCK.swoop_every_s[1]", t.SwoopEveryMaxSeconds);
            Same("SeagullIso.FLOCK.glide_duty", t.GlideDuty);
            Same("SeagullIso.ANIMS.fly.n", t.FlyFrames);
            Same("SeagullIso.ANIMS.fly.ms", t.FlyMilliseconds);
            Same("SeagullIso.ANIMS.glide.n", t.GlideFrames);
            Same("SeagullIso.ANIMS.glide.ms", t.GlideMilliseconds);
            Same("SeagullIso.ANIMS.swoop.n", t.SwoopFrames);
            Same("SeagullIso.ANIMS.swoop.ms", t.SwoopMilliseconds);

            // ⚠ The control on the narrowing. Everything above compares floats, so it is worth one
            // line to prove that a tunable which REALLY moved is still caught. The offset is derived
            // from the rig's own value rather than typed, so it survives the drop re-tuning the duty;
            // 1e-4 is some three thousand times a float ULP at 0.45 (2^-25) and still a change
            // no one would make by accident.
            Assert.Throws<AssertionException>(
                () => Same("SeagullIso.FLOCK.glide_duty", t.GlideDuty + 1e-4),
                "A glide duty a ten-thousandth away from the rig's compared EQUAL. Narrowing to float " +
                "has made this guard vacuous: it can no longer see a re-export that moved a number, " +
                "and every 'the port flies the drop's own tuning' claim in this fixture is worthless.");
        }

        /// <summary>The sidecar on disk is still the one this rig produced, so everything below
        /// compares the def against a MATCHED pair rather than against whatever happens to be there.
        /// </summary>
        [Test]
        public void TheDropOnDiskIsStillOneKit()
        {
            SeagullSidecarRead read = ReadKit();
            Assert.That(read.Errors, Is.Empty,
                "The sidecar no longer reads cleanly against the rig: " + string.Join("; ", read.Errors));
            Assert.AreNotEqual(RigHashMatch.None, read.HashMatch,
                "The sidecar's derivedFromRigSha256 no longer names the rig beside it. One of the two " +
                "was edited without the other, and the def was baked from a kit that no longer exists.");
        }

        /// <summary>
        /// Thirteen rows, number for number. <see cref="SeagullVisualDef"/> is a BAKE of the sidecar —
        /// a derived file — and a derived file is not where a fix lives. If the art director retunes
        /// <c>land</c>'s descent, this test is what says the asset was never rebuilt.
        /// </summary>
        [Test]
        public void TheDefAndTheDropAgreeAboutEveryState()
        {
            SeagullGameplay drop = ReadKit().Gameplay;
            SeagullVisualDef def = Def();

            Assert.AreEqual(drop.States.Count, def.States.Count,
                "The def carries a different number of states than the drop declares.");
            Assert.AreEqual(SeagullStates.Order.Length, def.States.Count,
                "The def does not carry all thirteen states.");

            foreach (SeagullVisualDef.StateEntry mine in def.States)
            {
                SeagullState theirs = drop.State(mine.State);
                Assert.IsNotNull(theirs, $"The def carries a state '{mine.State}' the drop does not declare.");

                Assert.AreEqual(theirs.Frames, mine.Frames, $"{mine.State}: frames");
                Assert.AreEqual(theirs.Milliseconds, mine.FrameMilliseconds, 1e-4f, $"{mine.State}: ms per frame");
                Assert.AreEqual(theirs.SpeedMetresPerSecond, mine.SpeedMetresPerSecond, 1e-4f, $"{mine.State}: v");
                Assert.AreEqual(theirs.ClimbMetresPerSecond, mine.ClimbMetresPerSecond, 1e-4f, $"{mine.State}: vz");
                Assert.AreEqual(theirs.AltitudeA, mine.AltitudeA, 1e-4f, $"{mine.State}: alt[0]");
                Assert.AreEqual(theirs.AltitudeB, mine.AltitudeB, 1e-4f, $"{mine.State}: alt[1]");
                Assert.AreEqual(theirs.Loop, mine.Loop, $"{mine.State}: loop");
                Assert.AreEqual(theirs.Next ?? "", mine.Next ?? "", $"{mine.State}: next");
                Assert.AreEqual(theirs.HasTravel ? theirs.TravelMetres : 0f, mine.TravelMetres, 1e-4f,
                                $"{mine.State}: travel");
                Assert.AreEqual(theirs.HasCycles ? theirs.CyclesMin : 0, mine.CyclesMin, $"{mine.State}: cycles[0]");
                Assert.AreEqual(theirs.HasCycles ? theirs.CyclesMax : 0, mine.CyclesMax, $"{mine.State}: cycles[1]");
            }
        }

        /// <summary>The state graph, edge for edge and in both directions — a def that dropped an edge
        /// and a def that invented one are equally wrong, and only a two-way comparison catches both.
        /// </summary>
        [Test]
        public void TheDefAndTheDropAgreeAboutEveryEdge()
        {
            SeagullGameplay drop = ReadKit().Gameplay;
            SeagullVisualDef def = Def();

            var mine = new HashSet<string>(StringComparer.Ordinal);
            foreach (SeagullVisualDef.EdgeEntry e in def.Transitions)
                Assert.IsTrue(mine.Add(e.From + "->" + e.To), $"The def declares {e.From}->{e.To} twice.");

            var theirs = new HashSet<string>(StringComparer.Ordinal);
            foreach (SeagullTransition e in drop.Transitions)
                theirs.Add(e.From + "->" + e.To);

            CollectionAssert.AreEquivalent(theirs, mine,
                "The def's edge set is not the drop's. A bird cannot take an edge the def does not " +
                "carry, and it must not take one the drop never declared.");
            Assert.AreEqual(26, mine.Count,
                "The drop shipped twenty-six edges. If a re-export changed that, say so deliberately " +
                "here — the structural claims in SeagullBehaviourTests are counted against this number.");
        }

        /// <summary>
        /// The bird herself, and the box she needs to come down in. The wingspan is the number the
        /// owner ruled on — 1.40 m at EVERY altitude — and it is also the width of the clear box a
        /// landing wants, because a gull lands with its wings open.
        /// </summary>
        [Test]
        public void TheDefAndTheDropAgreeAboutTheBirdAndWhereSheMayLand()
        {
            SeagullGameplay drop = ReadKit().Gameplay;
            SeagullVisualDef def = Def();

            Assert.AreEqual(drop.LengthMetres, def.LengthMetres, 1e-4f, "length_m");
            Assert.AreEqual(drop.WingspanMetres, def.WingspanMetres, 1e-4f, "wingspan_m");
            Assert.AreEqual(drop.MassKg, def.MassKg, 1e-4f, "mass_kg");
            Assert.AreEqual(drop.DraftMetres, def.DraftMetres, 1e-4f, "draft_m");
            Assert.AreEqual(drop.BodyCentreZStand, def.BodyCentreZStand, 1e-4f, "body_centre_z.stand");
            Assert.AreEqual(drop.BodyCentreZPerch, def.BodyCentreZPerch, 1e-4f, "body_centre_z.perch");
            Assert.AreEqual(drop.BodyCentreZFloat, def.BodyCentreZFloat, 1e-4f, "body_centre_z.float");
            Assert.AreEqual(drop.FootprintStandX, def.FootprintStand.x, 1e-4f, "footprint.stand.x");
            Assert.AreEqual(drop.FootprintStandY, def.FootprintStand.y, 1e-4f, "footprint.stand.y");
            Assert.AreEqual(drop.FootprintWingsOpenX, def.FootprintWingsOpen.x, 1e-4f, "footprint.wings_open.x");
            Assert.AreEqual(drop.FootprintWingsOpenY, def.FootprintWingsOpen.y, 1e-4f, "footprint.wings_open.y");

            SeagullLandRules land = def.Behaviour.Land;
            Assert.AreEqual(drop.LandNeedsClearX, land.ClearX, 1e-4, "LAND.needs_clear_m[0]");
            Assert.AreEqual(drop.LandNeedsClearY, land.ClearY, 1e-4, "LAND.needs_clear_m[1]");
            Assert.AreEqual(drop.LandApproachIntoWind, land.ApproachIntoWind, "LAND.approach_into_wind");
            Assert.AreEqual(drop.LandMinFlatMetres, land.MinFlatMetres, 1e-4, "LAND.min_flat_m");
            Assert.AreEqual(def.WingspanMetres, land.ClearX, 1e-4,
                "The clear box is no longer as wide as the bird. A gull lands with her wings open — " +
                "if the drop separated these two numbers, say so deliberately rather than here.");
            Assert.IsTrue(land.ApproachIntoWind,
                "The drop stopped asking for an into-wind approach, and SeagullStateMachine's run-in " +
                "leg is built on it.");

            SeagullWaterRules water = def.Behaviour.Water;
            Assert.AreEqual(drop.FloatBodyZMetres, water.FloatBodyZMetres, 1e-4, "WATER.float_body_z");
            Assert.AreEqual(drop.SplashBurstFrame, water.SplashBurstFrame, "WATER.splash_burst_frame");
            Assert.AreEqual(drop.DriftWithCurrent, water.DriftWithCurrent, "WATER.drift_with_current");

            // The ROCK caps are not their own JSON fields — the art director states them in the WATER
            // note, and the bake lifts them out of that prose. If the note is reworded, the bake stops
            // finding them and silently ships zeroes, so both ends are asserted.
            StringAssert.Contains("roll_deg", drop.WaterNote,
                "The WATER note no longer states the ROCK roll cap the bake reads out of it.");
            StringAssert.Contains("heave_px", drop.WaterNote,
                "The WATER note no longer states the ROCK heave cap the bake reads out of it.");
            Assert.AreEqual(2.4, water.RockRollDegreesMax, 1e-4,
                "The drop caps a floating gull at 2.4 degrees of roll.");
            Assert.AreEqual(1.0, water.RockHeavePixelsMax, 1e-4,
                "The drop caps a floating gull at one pixel of heave.");
        }
    }
}
