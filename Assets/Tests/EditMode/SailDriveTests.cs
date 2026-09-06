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
                // Skip the two ends of the range: dead upwind and dead downwind are their own mirrors,
                // so a reversed wind legitimately reproduces there and would blunt the arm.
                if (r.Twa <= 40f || r.Twa >= 150f) continue;
                checkedRows++;
                SailWind felt = FeelOf(180f - r.Twa, r.Tws, r.Kn, headingDeg: 47f);
                if (Mathf.Abs(Mathf.Abs(felt.ApparentAngleDeg) - r.Awa) <= 0.05f) reproduced++;
            }
            Assert.Greater(checkedRows, 40, "the sabotage arm must actually sweep the grid.");
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
        /// ⭐ <b>The thrust law is the hull's own terminal-speed identity, not a tuned number.</b>
        /// <c>BoatController</c> scales thrust and drag by the same <c>ForceFeelScale</c>, so terminal
        /// speed is exactly <c>F / ForwardDrag</c> m/s. Asking for <c>target × ForwardDrag</c> therefore
        /// settles at the target — with no ramp, which is the whole point (a ramp would be a second lag
        /// in series with the hull's own ~20 s time constant).
        /// </summary>
        [Test]
        public void TheThrustAskedForIsTheThrustThatSettlesAtTheTarget()
        {
            foreach (float drag in new[] { 40f, 240f, 300f })
                foreach (float targetKn in new[] { 1.5f, 4.97f, 9.15f, 12.6f })
                {
                    float targetMs = SailDrive.ToMetresPerSecond(targetKn);
                    float thrust = SailDrive.ThrustFor(targetMs, drag);
                    Assert.AreEqual(targetMs, thrust / drag, 1e-4f,
                        $"terminal speed is thrust/ForwardDrag; at drag {drag} the ask must settle at " +
                        $"{targetKn} kn.");
                }

            Assert.AreEqual(0f, SailDrive.ThrustFor(0f, 300f), 1e-6f, "no target, no push.");
            Assert.AreEqual(0f, SailDrive.ThrustFor(5f, 0f), 1e-6f,
                "a hull with no forward drag has no terminal speed to solve for — refuse rather than " +
                "divide by zero downstream.");
            Assert.AreEqual(0f, SailDrive.ThrustFor(-5f, 300f), 1e-6f,
                "a sail cannot push a boat backwards; astern is the auxiliary's job.");
        }

        /// <summary>AUTO_TRIM reproduces the builder pages' law at the seven points of sail — the sheet
        /// numbers the sidecar's own POINTS_OF_SAIL presets carry.</summary>
        [Test]
        public void AutoTrimReproducesTheBuilderPagesLaw()
        {
            // (awa, main, jib) — the presets in POINTS_OF_SAIL.builder_preset, both hulls share them.
            foreach ((float awa, float main, float jib) in new[]
            {
                (8f, 1f, 1f), (38f, 0.80f, 0.83f), (60f, 0.55f, 0.58f),
                (90f, 0.18f, 0.20f), (135f, 0f, 0f), (172f, 0f, 0f),
            })
            {
                SailDrive.AutoTrim(awa, out float m, out float j);
                Assert.AreEqual(main, m, 0.005f, $"main sheet at awa {awa}");
                Assert.AreEqual(jib, j, 0.005f, $"headsail sheet at awa {awa}");
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
                Assert.AreEqual(SailDrive.TargetSpeedKn(polar, 180f, 12f, 45f),
                                SailDrive.TargetSpeedKn(polar, 200f, 12f, 45f), 1e-4f,
                    $"{path}: past dead downwind the angle wraps back, it does not run off the end.");
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
    }
}
