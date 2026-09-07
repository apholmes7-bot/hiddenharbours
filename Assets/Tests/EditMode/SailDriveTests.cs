using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE SAIL DRIVE — the first hull whose speed IS the wind (P1).</b>
    ///
    /// <para>The guard that matters most here is not "does the arithmetic run" — it is
    /// <see cref="ThePolarLoopCloses_ThroughTheShippedCode"/>: the art side shipped a polar whose every
    /// row carries the apparent wind it implies, so feeding the true half of a row through the GAME's
    /// own wind frame and getting the art's apparent half back proves the two describe one boat, sign
    /// and all. A sign error in a wind frame is invisible in a symmetric test and puts the boat on the
    /// opposite tack, so it is checked against a file we did not write.</para>
    /// </summary>
    public class SailDriveTests
    {
        const string Sloop30Polar = "Assets/_Project/Data/Boats/SailPolars/SloopIsoRig.asset";
        const string Sloop88Polar = "Assets/_Project/Data/Boats/SailPolars/Sloop88IsoRig.asset";
        const string Sidecar30 = "docs/art/rigs/gameplay/sail/sloopIsoRig.sailing.json";
        const string Sidecar88 = "docs/art/rigs/gameplay/sail/sloop88IsoRig.sailing.json";

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        static SailPolarDef Polar(string path)
        {
            var p = AssetDatabase.LoadAssetAtPath<SailPolarDef>(path);
            Assert.IsNotNull(p, $"{path} is missing — run Hidden Harbours ▸ Dev ▸ Import Sail Polars.");
            Assert.IsTrue(p.IsUsable(), $"{path} is malformed: the axes and the columns disagree.");
            return p;
        }

        /// <summary>One shipped polar row, straight out of the sidecar — the art side's own numbers.</summary>
        readonly struct Row
        {
            public readonly float Twa, Tws, Kn, Awa, Aws;
            public readonly string Mode;
            public Row(float twa, float tws, float kn, float awa, float aws, string mode)
            { Twa = twa; Tws = tws; Kn = kn; Awa = awa; Aws = aws; Mode = mode; }
        }

        /// <summary>
        /// Reads POLAR_REFERENCE.rows out of a sailing sidecar without a JSON dependency — flat objects
        /// of scalars, which is all these rows are. Deliberately NOT read through
        /// <c>SailPolarImporter</c>: this fixture's whole job is to check the game against the file, so
        /// it must not check the file against itself through the same reader.
        /// </summary>
        static List<Row> ShippedRows(string sidecarRepoPath)
        {
            string json = File.ReadAllText(Path.Combine(RepoRoot, sidecarRepoPath));
            int at = json.IndexOf("\"POLAR_REFERENCE\"", StringComparison.Ordinal);
            Assert.Greater(at, 0, $"{sidecarRepoPath}: no POLAR_REFERENCE section.");
            int rowsAt = json.IndexOf("\"rows\"", at, StringComparison.Ordinal);
            Assert.Greater(rowsAt, 0, $"{sidecarRepoPath}: POLAR_REFERENCE has no rows.");

            var rows = new List<Row>();
            int i = json.IndexOf('[', rowsAt);
            int depth = 0;
            for (; i < json.Length; i++)
            {
                if (json[i] == '[') { depth++; continue; }
                if (json[i] == ']') { if (--depth == 0) break; continue; }
                if (json[i] != '{') continue;

                int end = json.IndexOf('}', i);
                string obj = json.Substring(i, end - i + 1);
                rows.Add(new Row(Num(obj, "twa"), Num(obj, "tws"), Num(obj, "v_kn"),
                                 Num(obj, "awa"), Num(obj, "aws"), Str(obj, "mode")));
                i = end;
            }
            Assert.AreEqual(150, rows.Count, $"{sidecarRepoPath}: expected a 15×10 grid.");
            return rows;
        }

        /// <summary>
        /// The trimmed presets out of POINTS_OF_SAIL — (awa, main, jib) — straight from the sidecar.
        /// Presets carrying no sheets (becalmed) are skipped: they state a mode, not a trim.
        /// </summary>
        static List<(float Awa, float Main, float Jib)> BuilderPresets(string sidecarRepoPath)
        {
            string json = File.ReadAllText(Path.Combine(RepoRoot, sidecarRepoPath));
            int at = json.IndexOf("\"POINTS_OF_SAIL\"", StringComparison.Ordinal);
            Assert.Greater(at, 0, $"{sidecarRepoPath}: no POINTS_OF_SAIL section.");

            var found = new List<(float, float, float)>();
            int i = at;
            while (true)
            {
                int k = json.IndexOf("\"builder_preset\"", i, StringComparison.Ordinal);
                if (k < 0) break;
                int open = json.IndexOf('{', k);
                int close = json.IndexOf('}', open);
                string obj = json.Substring(open, close - open + 1);
                i = close;

                float awa = Num(obj, "awa"), main = Num(obj, "main"), jib = Num(obj, "jib");
                if (float.IsNaN(awa) || float.IsNaN(main) || float.IsNaN(jib)) continue;
                found.Add((awa, main, jib));
            }
            return found;
        }

        static float Num(string obj, string key)
        {
            int k = obj.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (k < 0) return float.NaN;
            int c = obj.IndexOf(':', k) + 1, e = c;
            while (e < obj.Length && (char.IsDigit(obj[e]) || obj[e] is '-' or '+' or '.' or 'e' or 'E' or ' ')) e++;
            return float.Parse(obj.Substring(c, e - c).Trim(), CultureInfo.InvariantCulture);
        }

        static string Str(string obj, string key)
        {
            int k = obj.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (k < 0) return "";
            int q = obj.IndexOf('"', obj.IndexOf(':', k) + 1);
            int e = obj.IndexOf('"', q + 1);
            return obj.Substring(q + 1, e - q - 1);
        }

        /// <summary>
        /// Put a boat in a world where the wind is <paramref name="twaDeg"/> off her bow at
        /// <paramref name="twsKn"/> and she is making <paramref name="boatKn"/> through it — using the
        /// SIM's conventions throughout (WindVector points DOWNWIND) — and ask the drive what she feels.
        /// </summary>
        static SailWind FeelOf(float twaDeg, float twsKn, float boatKn, float headingDeg = 0f)
        {
            Vector2 bow = new Vector2(Mathf.Sin(headingDeg * Mathf.Deg2Rad),
                                      Mathf.Cos(headingDeg * Mathf.Deg2Rad));
            // The wind comes FROM twa off the bow, so it BLOWS toward the opposite of that.
            float fromDeg = headingDeg + twaDeg;
            Vector2 windFrom = new Vector2(Mathf.Sin(fromDeg * Mathf.Deg2Rad),
                                           Mathf.Cos(fromDeg * Mathf.Deg2Rad));
            Vector2 windVector = -windFrom * SailDrive.ToMetresPerSecond(twsKn);
            Vector2 velocity = bow * SailDrive.ToMetresPerSecond(boatKn);
            return SailDrive.ResolveWind(windVector, bow, velocity);
        }

        // =========================================================================================
        //  1. THE LOOP CLOSES — against a file we did not write
        // =========================================================================================

        /// <summary>
        /// ⭐⭐ <b>Every one of the 300 shipped rows, through the game's own wind frame.</b> Each row
        /// states a true wind, the speed the model gives at it, AND the apparent wind that implies.
        /// Hand the true half plus the speed to <see cref="SailDrive.ResolveWind"/> and the apparent
        /// half must come back — magnitude and SIGN.
        ///
        /// <para>Tolerance is 0.05° / 0.05 kn, which is the rounding the sidecar itself carries (its
        /// columns are stated to one decimal). A looser bar would let a real frame error hide inside
        /// it; a tighter one would fail on the file's own rounding.</para>
        ///
        /// <para><b>⚠️ Run at a NON-ZERO heading.</b> At heading 0 a sign error in the bearing maths
        /// cancels against a sign error in the relative-bearing maths and the test passes on a boat
        /// that sails on the wrong tack everywhere else. 47° is arbitrary and not a multiple of
        /// anything.</para>
        /// </summary>
        [Test]
        public void ThePolarLoopCloses_ThroughTheShippedCode()
        {
            foreach (string sidecar in new[] { Sidecar30, Sidecar88 })
            {
                var bad = new List<string>();
                foreach (Row r in ShippedRows(sidecar))
                {
                    SailWind felt = FeelOf(r.Twa, r.Tws, r.Kn, headingDeg: 47f);
                    if (Mathf.Abs(Mathf.Abs(felt.ApparentAngleDeg) - r.Awa) > 0.05f ||
                        Mathf.Abs(felt.ApparentKn - r.Aws) > 0.05f)
                        bad.Add($"twa {r.Twa} tws {r.Tws}: sidecar awa {r.Awa} aws {r.Aws}, " +
                                $"drive {Mathf.Abs(felt.ApparentAngleDeg):0.00} {felt.ApparentKn:0.00}");

                    // the wind is over the STARBOARD bow in this fixture, so the sails are to port
                    if (r.Twa > 0f && r.Twa < 180f && felt.ApparentAngleDeg <= 0f)
                        bad.Add($"twa {r.Twa} tws {r.Tws}: apparent angle came back to PORT " +
                                "for a wind over the starboard bow — the frame is mirrored.");
                }
                Assert.IsEmpty(bad, $"{sidecar}: the game's wind frame and the art's disagree:\n" +
                                    string.Join("\n", bad.Take(8)));
            }
        }

        /// <summary>
        /// ⭐ <b>The sabotage arm.</b> Rotate the wind 180° and the same row must NOT reproduce. Without
        /// this, the test above proves only that some arithmetic is stable — a boat that reads the wind
        /// backwards sails on the opposite tack and would pass every symmetric check in this file.
        /// </summary>
        [Test]
        public void TheLoopDoesNotClose_WhenTheWindIsReversed()
        {
            var rows = ShippedRows(Sidecar30);
            int reproduced = 0, checkedRows = 0;
            foreach (Row r in rows)
            {
                // ⚠️⚠️ THE BEAM IS THE FIXED POINT OF THIS REFLECTION, so it must be excluded BY
                // CONSTRUCTION rather than tolerated. Reversing the wind maps twa -> 180 - twa, and
                // 90° is the one angle that maps to ITSELF: at the beam a reversed wind is the SAME
                // wind, the row reproduces exactly, and it says nothing whatever about whether the
                // closure test measures direction. That is all ten of the beam's wind rows — exactly
                // the "10 of 100" this arm reported before the exclusion. The two ends go for a
                // weaker reason: near dead-upwind and dead-downwind the fold barely moves the
                // apparent angle, so they blunt the arm without sharpening it.
                if (r.Twa <= 40f || r.Twa >= 150f) continue;
                if (Mathf.Abs(r.Twa - 90f) < 0.01f) continue;
                checkedRows++;
                SailWind felt = FeelOf(180f - r.Twa, r.Tws, r.Kn, headingDeg: 47f);
                if (Mathf.Abs(Mathf.Abs(felt.ApparentAngleDeg) - r.Awa) <= 0.05f) reproduced++;
            }
            Assert.Greater(checkedRows, 40, "the sabotage arm must actually sweep the grid.");

            // ...and the exclusion must be EARNED, not asserted in a comment. If the beam ever stops
            // reproducing under a reversed wind then the reflection is no longer twa -> 180 - twa and
            // this arm's whole construction needs re-deriving.
            Row beam = rows.First(x => Mathf.Abs(x.Twa - 90f) < 0.01f);
            SailWind beamReversed = FeelOf(180f - beam.Twa, beam.Tws, beam.Kn, headingDeg: 47f);
            Assert.AreEqual(beam.Awa, Mathf.Abs(beamReversed.ApparentAngleDeg), 0.05f,
                "the beam MUST reproduce under a reversed wind — it is the fold's fixed point. If it " +
                "no longer does, delete the exclusion above: it has stopped being a fact.");
            Assert.AreEqual(0, reproduced,
                $"{reproduced} of {checkedRows} rows reproduced with the wind rotated 180°. The " +
                "closure test above is therefore not measuring the wind's direction at all.");
        }

        // =========================================================================================
        //  2. THE NO-GO — and why it is on the TRUE angle
        // =========================================================================================

        [Test]
        public void SheMakesNoWayInTheNoGo_AndNoneBecalmed()
        {
            SailPolarDef polar = Polar(Sloop30Polar);

            for (float twa = -44f; twa <= 44f; twa += 4f)
                Assert.AreEqual(0f, SailDrive.TargetSpeedKn(polar, twa, 12f, 45f), 1e-6f,
                    $"twa {twa}° is inside a 45° no-go: she must make no way, on either tack.");

            Assert.Greater(SailDrive.TargetSpeedKn(polar, 45f, 12f, 45f), 4f,
                "at the no-go's own edge she must sail — a gate that never opens is a becalmed boat.");
            Assert.Greater(SailDrive.TargetSpeedKn(polar, -90f, 12f, 45f), 4f,
                "and on the port tack too.");

            Assert.AreEqual(0f, SailDrive.TargetSpeedKn(polar, 90f, 0f, 45f), 1e-6f,
                "becalmed: no wind, no way, at any angle.");
            Assert.AreEqual(0f, SailDrive.TargetSpeedKn(null, 90f, 12f, 45f), 1e-6f,
                "no polar is not a fast boat — a hull with no table makes no way through this drive.");
        }

        /// <summary>
        /// ⭐⭐ <b>Why the no-go defaults to 45° and not the reference polar's own 35°.</b>
        ///
        /// <para>The polar and the sprite are in different frames: the model returns zero below twa 35
        /// TRUE, the rig draws flogging sails below awa 25 APPARENT. Between them sit cells where the
        /// boat is handed a speed while her own picture says she is in irons. This counts them, and
        /// pins that a 45° gate leaves NONE — on both hulls, over the whole shipped grid.</para>
        ///
        /// <para><b>⚠️ The negative control is the point.</b> If 35° also left zero, the default would
        /// be arbitrary and this test would be decoration. It does not: 4 cells on the 30 and 14 on the
        /// 88. Both halves are asserted.</para>
        /// </summary>
        [Test]
        public void AFortyFiveDegreeNoGo_LeavesNoCellWhereThePolarSailsAndTheSpriteFlogs()
        {
            foreach ((string sidecar, int at35) in new[] { (Sidecar30, 4), (Sidecar88, 14) })
            {
                var rows = ShippedRows(sidecar);
                const float ironsApparent = 25f;

                int disagreeAt45 = rows.Count(r => r.Twa >= 45f && r.Awa < ironsApparent);
                int disagreeAt35 = rows.Count(r => r.Twa >= 35f && r.Awa < ironsApparent);

                Assert.AreEqual(0, disagreeAt45,
                    $"{sidecar}: a 45° no-go still leaves {disagreeAt45} cell(s) where the polar hands " +
                    "out a speed and the rig draws flogging sails. The default on BoatHullDef is no " +
                    "longer the angle that makes the two agree — re-measure before changing it.");

                Assert.AreEqual(at35, disagreeAt35,
                    $"{sidecar}: the NEGATIVE CONTROL moved. At the reference polar's own 35° there " +
                    $"were {at35} disagreeing cells; there are now {disagreeAt35}. If this is zero, " +
                    "the coupling defect is gone and the 45° default has lost its reason.");
            }
        }

        /// <summary>
        /// ⭐⭐ <b>Why the gate cannot be on the APPARENT angle — the oscillation, measured.</b>
        ///
        /// <para>Apparent angle is a function of boat speed. At twa 40 in 12 kn the sloop 88 sits at
        /// ~40° apparent when stopped and ~23.6° when making her polar speed — one side of the rig's
        /// 25° irons band when still and the other side when moving. A gate on that angle would stop
        /// her, which reopens the angle, which starts her: a limit cycle at exactly the angle a player
        /// pinching to windward sails.</para>
        ///
        /// <para>This test does not check any production code — it checks the FACT that made the
        /// production code choose the true angle, so that if the fact ever stops being true the reason
        /// is re-read rather than inherited.</para>
        /// </summary>
        [Test]
        public void AnApparentAngleGateWouldOscillate_WhichIsWhyTheGateIsOnTheTrueAngle()
        {
            const float twa = 40f, tws = 12f, ironsApparent = 25f;
            float sailingKn = SailDrive.TargetSpeedKn(Polar(Sloop88Polar), twa, tws, 35f);
            Assert.Greater(sailingKn, 6f, "she should be moving well at twa 40 in 12 kn true.");

            float stopped = Mathf.Abs(FeelOf(twa, tws, 0f).ApparentAngleDeg);
            float moving = Mathf.Abs(FeelOf(twa, tws, sailingKn).ApparentAngleDeg);

            Assert.Greater(stopped, ironsApparent,
                $"stopped she reads {stopped:0.0}° apparent — outside the irons band, so an " +
                "apparent-angle gate would let her start.");
            Assert.Less(moving, ironsApparent,
                $"moving at {sailingKn:0.00} kn she reads {moving:0.0}° apparent — inside the band, so " +
                "the same gate would stop her. Start, stop, start: that is the cycle the TRUE-angle " +
                "gate exists to avoid. If this assertion now fails the coupling has changed and " +
                "BoatHullDef.NoGoTrueWindDeg's reasoning should be re-read.");
        }

        // =========================================================================================
        //  3. THRUST, TRIM AND THE TACK
        // =========================================================================================

        /// <summary>
        /// ⭐ <b>The thrust law is the hull's own terminal-speed identity, not a tuned number.</b> Ask
        /// for <c>target × (everything that resists her)</c> and she settles at the target — with no
        /// ramp, which is the whole point (a ramp would be a second lag in series with the hull's own
        /// time constant).
        ///
        /// <para><b>⚠️ This test used to be a tautology and it hid a real bug.</b> It asserted
        /// <c>ThrustFor(t, d) / d == t</c>, which only inverts the function's own multiply: it passes
        /// for ANY resistance you hand in, including the wrong one. What it could not see is that the
        /// resistance was wrong — <c>ForwardDrag</c> alone, while the body also carries
        /// <c>linearDamping</c>. The identity below is therefore stated against the CONTROLLER's real
        /// constants, and <see cref="TheDampingTermIsNotOptional_AndIsTheLargerOneOnASloop"/> is the arm
        /// that fails if the second term is dropped again.</para>
        /// </summary>
        [Test]
        public void TheThrustAskedForIsTheThrustThatSettlesAtTheTarget()
        {
            // (ForwardDrag, MassKg) of hulls that actually ship — a punt, a working boat, both sloops.
            foreach (var hull in new[] { (140f, 700f), (300f, 6000f), (200f, 4875f), (900f, 89190f) })
                foreach (float targetKn in new[] { 1.5f, 4.97f, 9.15f, 12.6f })
                {
                    float targetMs = SailDrive.ToMetresPerSecond(targetKn);
                    float resistance = SailDrive.LinearResistance(
                        hull.Item1, hull.Item2 / 100f, BoatController.HullLinearDamping, BoatController.ForceFeelScale);
                    float thrust = SailDrive.ThrustFor(targetMs, resistance);

                    // The controller's own equilibrium, written out: thrust × scale balances
                    // (ForwardDrag × scale)·v  +  (damping × mass)·v.
                    float mass = hull.Item2 / 100f;
                    float settled = thrust * BoatController.ForceFeelScale /
                                    (hull.Item1 * BoatController.ForceFeelScale + BoatController.HullLinearDamping * mass);
                    Assert.AreEqual(targetMs, settled, 1e-3f,
                        $"a hull of {hull.Item2} kg at drag {hull.Item1} must SETTLE at {targetKn} kn.");
                }

            Assert.AreEqual(0f, SailDrive.ThrustFor(0f, 300f), 1e-6f, "no target, no push.");
            Assert.AreEqual(0f, SailDrive.ThrustFor(5f, 0f), 1e-6f,
                "a hull that nothing resists has no terminal speed to solve for — refuse rather than " +
                "divide by zero downstream.");
            Assert.AreEqual(0f, SailDrive.ThrustFor(-5f, 300f), 1e-6f,
                "a sail cannot push a boat backwards; astern is the auxiliary's job.");
        }

        /// <summary>
        /// ⭐⭐ <b>The term that gets forgotten is the BIGGER one, and forgetting it does not cost a few
        /// per cent.</b>
        ///
        /// <para><c>ForwardDrag</c> is a hull field, so it is the resistance anyone writing a drive
        /// reaches for. <c>Rigidbody2D.linearDamping</c> is set once in <c>BoatController.Awake</c> and
        /// is invisible from the def — but it acts on a mass of <c>MassKg/100</c>, and a sailing hull is
        /// heavy, so on the sloop 30 it is roughly FIVE TIMES the hull drag. A drive that balances only
        /// the hull drag sails her at <c>ForwardDrag / (ForwardDrag + damping·MassKg)</c> of her polar —
        /// 17 %, which is 0.8 kn on a broad reach her own polar puts at 4.7, and reads in play as a boat
        /// that is becalmed in a working breeze rather than as an arithmetic slip.</para>
        ///
        /// <para>This is the arm that fails if the damping is dropped again, and it is stated as a RATIO
        /// so it survives every retune of either number.</para>
        /// </summary>
        [Test]
        public void TheDampingTermIsNotOptional_AndIsTheLargerOneOnASloop()
        {
            const float sloop30Drag = 200f, sloop30MassKg = 4875f;
            float mass = sloop30MassKg / 100f;

            float total = SailDrive.LinearResistance(sloop30Drag, mass, BoatController.HullLinearDamping,
                                                     BoatController.ForceFeelScale);
            float damping = total - sloop30Drag;

            Assert.Greater(damping, sloop30Drag * 2f,
                $"the body's damping ({damping:0.#}) must dominate the hull drag ({sloop30Drag}) on a " +
                "hull this heavy — if it no longer does, the mass rule or the damping constant changed " +
                "and the numbers quoted throughout this drive need re-measuring.");

            float naiveFraction = sloop30Drag / total;
            Assert.Less(naiveFraction, 0.25f,
                $"balancing ForwardDrag alone would sail her at {naiveFraction:P0} of her polar. The " +
                "guard exists because that is a silent, plausible-looking slowness, not a crash.");

            // And the composition itself, against the controller's constants rather than a transcription.
            Assert.AreEqual(sloop30Drag + BoatController.HullLinearDamping * mass / BoatController.ForceFeelScale, total, 1e-3f,
                "LinearResistance must put the body's newtons and the hull's design units on one footing.");

            Assert.AreEqual(sloop30Drag, SailDrive.LinearResistance(sloop30Drag, mass, 0f,
                            BoatController.ForceFeelScale), 1e-3f,
                "a body with no damping is resisted by the hull alone.");
        }

        /// <summary>AUTO_TRIM reproduces the builder pages' law at the seven points of sail — the sheet
        /// numbers the sidecar's own POINTS_OF_SAIL presets carry.</summary>
        [Test]
        public void AutoTrimReproducesTheBuilderPagesLaw()
        {
            // ⚠️ READ FROM THE SIDECAR, never transcribed. This table used to be six hand-copied
            // triples and TWO OF THEM WERE WRONG (awa 60 was written 0.55/0.58 against the file's
            // 0.54/0.54; awa 90 was written 0.18/0.20 against 0.17/0.14), so CI failed the SHIPPED
            // law for disagreeing with a typo. The file is the oracle; copying it defeats the point.
            var presets = BuilderPresets(Sidecar30);
            Assert.GreaterOrEqual(presets.Count, 6,
                "POINTS_OF_SAIL must still carry its trimmed presets — if this drops, the fixture is " +
                "asserting against nothing.");

            foreach ((float awa, float main, float jib) in presets)
            {
                SailDrive.AutoTrim(awa, out float m, out float j);
                // The presets are stated to TWO DECIMALS, so the honest comparison is the law rounded
                // as the file rounds it — an exact identity, not a fuzzy band wide enough to hide a
                // real drift of up to half a hundredth.
                Assert.AreEqual(main, Mathf.Round(m * 100f) / 100f, 1e-4f,
                    $"main sheet at awa {awa}: the law gives {m:0.0000}, the builder page says {main}");
                Assert.AreEqual(jib, Mathf.Round(j * 100f) / 100f, 1e-4f,
                    $"headsail sheet at awa {awa}: the law gives {j:0.0000}, the builder page says {jib}");
            }

            // Symmetric: the same trim on either tack. The SIDE the sails go is the pose's business.
            SailDrive.AutoTrim(38f, out float stbdMain, out _);
            SailDrive.AutoTrim(-38f, out float portMain, out _);
            Assert.AreEqual(stbdMain, portMain, 1e-6f, "a sheet is pulled the same on either tack.");
        }

        /// <summary>
        /// ⭐ <b>A tack passes the wind, and the sails change side.</b> The rig's law is
        /// <c>side = awa &gt;= 0 ? -1 : 1</c> — wind over starboard puts the sails to port — so the
        /// SIGN of the apparent angle is what a presenter reads to know which side the boom is on.
        /// Swing the boat through the wind and that sign must flip exactly once.
        /// </summary>
        [Test]
        public void ATackFlipsTheSideTheSailsAreOn_ExactlyOnce()
        {
            // From 40° on the starboard tack, through the wind, to 40° on the port tack — the
            // MANOEUVRES block's own tack (from_awa 40 → to_awa −40).
            var signs = new List<int>();
            for (float twa = 40f; twa >= -40f; twa -= 2f)
            {
                SailWind w = FeelOf(twa, 14f, 3f, headingDeg: 47f);
                signs.Add(w.ApparentAngleDeg >= 0f ? 1 : -1);
            }

            int flips = 0;
            for (int i = 1; i < signs.Count; i++) if (signs[i] != signs[i - 1]) flips++;

            Assert.AreEqual(1, flips,
                "a tack crosses the wind once, so the side the sails set changes once. More flips " +
                "means the angle is wrapping through ±180 somewhere it should not.");
            Assert.AreEqual(1, signs.First(), "she starts with the wind over the starboard bow.");
            Assert.AreEqual(-1, signs.Last(), "and finishes with it over the port bow.");
        }

        /// <summary>Pure and deterministic: the same inputs give the same answer, and nothing here reads
        /// a clock or an RNG. Cheap to assert, and it is the property the whole sim rests on (rule 5).</summary>
        [Test]
        public void TheDriveIsPure()
        {
            SailPolarDef polar = Polar(Sloop30Polar);
            for (int i = 0; i < 3; i++)
            {
                SailWind w = FeelOf(63f, 11.3f, 4.1f, headingDeg: 211f);
                Assert.AreEqual(FeelOf(63f, 11.3f, 4.1f, headingDeg: 211f).ApparentAngleDeg,
                                w.ApparentAngleDeg, 0f, "same wind, same answer, every time.");
                Assert.AreEqual(SailDrive.TargetSpeedKn(polar, 63f, 11.3f, 45f),
                                SailDrive.TargetSpeedKn(polar, 63f, 11.3f, 45f), 0f);
            }
        }

        // =========================================================================================
        //  4. THE POLAR ASSETS THEMSELVES
        // =========================================================================================

        /// <summary>
        /// The interpolated table agrees with the grid it was built from AT the nodes, and is clamped
        /// off the ends rather than extrapolated — a linear run-out past 25 kn of breeze would invent
        /// the planing the model explicitly says it does not have.
        /// </summary>
        [Test]
        public void ThePolarSamplesItsOwnNodes_AndClampsOffTheEnds()
        {
            foreach ((string path, string sidecar) in new[] { (Sloop30Polar, Sidecar30), (Sloop88Polar, Sidecar88) })
            {
                SailPolarDef polar = Polar(path);
                foreach (Row r in ShippedRows(sidecar))
                {
                    if (r.Twa < 45f) continue;          // inside the shipped default's no-go
                    Assert.AreEqual(r.Kn, SailDrive.TargetSpeedKn(polar, r.Twa, r.Tws, 45f), 0.005f,
                        $"{path}: the table must return its own cell at twa {r.Twa} tws {r.Tws}.");
                }

                float topWind = polar.TrueWindKn.Last();
                Assert.AreEqual(SailDrive.TargetSpeedKn(polar, 90f, topWind, 45f),
                                SailDrive.TargetSpeedKn(polar, 90f, topWind * 3f, 45f), 1e-4f,
                    $"{path}: past the grid's last wind the speed is CLAMPED. Extrapolating would " +
                    "invent the surfing the polar's own note says is not modelled.");
                // ⚠️ THE ANGLE AXIS MIRRORS; IT DOES NOT CLAMP, and that is correct rather than a
                // gap. This used to assert that 200° gives the same speed as 180° — but 200° off the
                // bow IS -160°, i.e. 160° off the OTHER bow, and the sampler folds it there (5.39 kn
                // at 12 kn, the twa-160 interpolation). Clamping it to 180 would tell a boat with the
                // wind 160° off her port quarter that she is running dead downwind. The old
                // expectation was unreachable as well as wrong: ResolveWind answers through
                // BoatKinematics.RelativeBearingDegrees, which is ±180 by construction, so nothing in
                // the game can hand this function 200° in the first place
                // (a-test-that-passes-an-unreachable-input-guarantees-nothing).
                Assert.AreEqual(SailDrive.TargetSpeedKn(polar, 160f, 12f, 45f),
                                SailDrive.TargetSpeedKn(polar, 200f, 12f, 45f), 1e-4f,
                    $"{path}: 200° off the bow is 160° off the other bow — the fold is a MIRROR.");
                Assert.AreEqual(SailDrive.TargetSpeedKn(polar, 120f, 12f, 45f),
                                SailDrive.TargetSpeedKn(polar, -120f, 12f, 45f), 1e-4f,
                    $"{path}: port and starboard are the same boat — the reachable half of the same " +
                    "law, and the half a tack actually exercises.");

                // The angle axis DOES clamp at its low end, which is reachable whenever a hull's
                // no-go sits below the grid's first angle: ask below it and you get that first row,
                // never an extrapolation off the front of the table.
                float firstAngle = polar.TrueWindAngleDeg.First();
                Assert.AreEqual(SailDrive.TargetSpeedKn(polar, firstAngle, 12f, 20f),
                                SailDrive.TargetSpeedKn(polar, firstAngle - 5f, 12f, 20f), 1e-4f,
                    $"{path}: below the grid's first angle the speed is CLAMPED to it.");
            }
        }

        /// <summary>The polars carry their provenance, and it says REFERENCE. A table of speeds with
        /// its provenance stripped is indistinguishable from a measurement.</summary>
        [Test]
        public void ThePolarsStillSayTheyAreAReference()
        {
            foreach (string path in new[] { Sloop30Polar, Sloop88Polar })
            {
                SailPolarDef polar = Polar(path);
                Assert.AreEqual(SailPolarStatus.Reference, polar.Status,
                    $"{path}: if this is now Authored, a VPP replaced the model and the drive should " +
                    "be re-tuned against it rather than inheriting the reference's feel.");
                Assert.IsNotEmpty(polar.StatusNote);
                Assert.IsNotEmpty(polar.Model);
                Assert.AreEqual(64, polar.DerivedFromRigSha256.Length,
                    $"{path}: the full digest, never a prefix.");
                Assert.Greater(polar.IronsRowCount, 0,
                    $"{path}: the polar/sprite disagreement is counted onto the asset so the owner's " +
                    "ruling has a size. Zero would mean the coupling defect is gone.");
            }
        }

        // =========================================================================================
        //  5. THE OWNER'S NO-GO RULING (2026-09-06) — the two frames must agree
        // =========================================================================================

        /// <summary>
        /// ⭐⭐ <b>The picture and the physics never disagree about whether she is sailing</b> — owner
        /// ruling 2026-09-06 ("whatever no-go angle is realistic"), applied as: the no-go is a TRUE
        /// angle at a realistic cruising figure (45° on the fractional 30), the polar's 35° stays
        /// REFERENCE data (a VMG limit, not a sailing limit), and the sprite's 25° APPARENT is not a
        /// second threshold — <see cref="SailDrive.IsInIrons"/> is the one publisher.
        ///
        /// <para><b>⚠️ The union is not decoration, and this is the arm that proves it.</b> Inside the
        /// no-go the drive takes her speed to ZERO, and at zero speed the apparent wind IS the true
        /// wind — so <c>awa == twa</c>. An apparent-only test therefore draws a boat pinching at
        /// twa 30° with her sails DRAWING while she sits dead in the water, because 30° is outside the
        /// rig's 25° flogging band. The whole band 25° &lt; twa &lt; 45° has that defect. Only taking
        /// the no-go as well closes it.</para>
        /// </summary>
        [Test]
        public void TheDrawnPictureAndTheDriveAgreeAboutWhetherSheIsSailing()
        {
            SailPolarDef polar = Polar(Sloop30Polar);
            const float noGo = 45f, spriteIrons = 25f;

            // --- JUST sailing: at the no-go boundary she must be drawn SAILING. ---
            // At twa 45 in 12 kn the polar gives 4.97 kn, and her own motion pulls the apparent angle
            // forward to 32.2° — outside the 25° band, so the boat that is only just sailing is drawn
            // as sailing. (The kit README's "28-30°" is not this hull's number; the sidecar says 32.2.)
            float target = SailDrive.TargetSpeedKn(polar, noGo, 12f, noGo);
            Assert.Greater(target, 0f, "at the no-go boundary itself the polar must still hand out speed.");
            SailWind sailing = FeelOf(noGo, 12f, target, headingDeg: 47f);
            Assert.AreEqual(32.2f, Mathf.Abs(sailing.ApparentAngleDeg), 0.15f,
                "the apparent angle at the boundary is the sidecar's own 32.2°.");
            Assert.IsFalse(SailDrive.IsInIrons(noGo, sailing.ApparentAngleDeg, noGo, spriteIrons),
                "a boat the polar is sailing must never be drawn with her sails flogging.");

            // --- Dead in the no-go: she must be drawn IN IRONS, at every angle inside it. ---
            // She is stopped, so awa == twa; for 25 < twa < 45 the sprite's band alone says "drawing".
            for (float twa = 5f; twa < noGo; twa += 5f)
            {
                Assert.AreEqual(0f, SailDrive.TargetSpeedKn(polar, twa, 12f, noGo), 1e-4f,
                    $"twa {twa}° is inside the no-go: the drive gives her nothing.");
                SailWind dead = FeelOf(twa, 12f, 0f, headingDeg: 47f);
                Assert.AreEqual(twa, Mathf.Abs(dead.ApparentAngleDeg), 0.05f,
                    "stopped, the apparent wind IS the true wind — this is why an apparent-only " +
                    "threshold cannot see a boat that the no-go has already killed.");
                Assert.IsTrue(SailDrive.IsInIrons(twa, dead.ApparentAngleDeg, noGo, spriteIrons),
                    $"at twa {twa}° she makes no way, so she must be DRAWN making no way.");
            }
        }

        /// <summary>
        /// ⭐ <b>Two sabotage arms for the ruling above.</b> A guard that only ever sees the shipped
        /// numbers cannot tell you it is load-bearing.
        /// </summary>
        [Test]
        public void TheInIronsPublisherRefusesBothWaysOfGettingItWrong()
        {
            const float noGo = 45f, spriteIrons = 25f;

            // ARM 1 — the sprite's band used ALONE (a presenter that kept its own threshold and asked
            // only about apparent wind). At twa 30, stopped, awa = 30: it says "drawing" for a boat the
            // drive has killed. This is the defect the union exists to close, so it MUST still be
            // visible here — if it is not, awa no longer collapses onto twa and the union's whole
            // justification needs re-measuring.
            SailWind dead = FeelOf(30f, 12f, 0f, headingDeg: 47f);
            Assert.IsFalse(Mathf.Abs(dead.ApparentAngleDeg) < spriteIrons,
                "the apparent-only test must still be WRONG at twa 30 — that is the premise of the union.");
            Assert.IsTrue(SailDrive.IsInIrons(30f, dead.ApparentAngleDeg, noGo, spriteIrons),
                "...and the publisher must nonetheless call her in irons.");

            // ARM 2 — the sprite's 25° fed in as a TRUE-angle threshold (the frame confusion the ruling
            // names). It would declare a boat sailing at twa 30 — dead water, drawn drawing.
            Assert.IsFalse(SailDrive.IsInIrons(30f, dead.ApparentAngleDeg, spriteIrons, spriteIrons),
                "25° is an APPARENT threshold. Used as the true-angle no-go it says a boat pinching at " +
                "twa 30 is sailing, which is the exact frame error the owner's ruling forbids. If this " +
                "ever passes, the two thresholds have been transposed.");
        }
    }
}
