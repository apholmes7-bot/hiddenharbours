using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE ROD IS ONE ROD.</b> The owner's law for every tool in the game, in his words: <i>no
    /// teleport, no hand change without an animated hand-over, no size change, no orientation change
    /// across any transition.</i> This is that law, run against the rig itself, in the editor.
    ///
    /// <para>The defect it was written for: the held rod and the cast rod had been authored as two
    /// animations, and <c>rest:'ground'</c> / <c>rest:'stored'</c> were single cells that each carried
    /// their own yaw (0, against the held 16°) and their own idea of what the sprite's pivot meant (a
    /// <c>zOff</c> that slid the grip 2.3 px / 3.9 px off it). Measured across the five transitions ×
    /// 8 facings, the rod teleported up to 3.9 px, swung up to 151°, and changed apparent length by a
    /// third the instant it left the hand — and nothing anywhere failed.</para>
    ///
    /// <para><b>The node twin is <c>tools/rig-recipes/rod-continuity.mjs</c></b>, which prints the
    /// per-facing table and runs with no editor at all. This one exists so the law is inside
    /// <c>unity test</c> too — a rig change that breaks the rod should not be able to reach a bake.
    /// Both measure the same things off the same V8; neither restates a number the rigs own.</para>
    /// </summary>
    public class RodContinuityTests
    {
        const int Dirs = 8;
        static readonly string[] Facings = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        /// <summary>Every rod state a fisher pose can put the rod in — the character rig's tool anims.</summary>
        static readonly string[] HeldStates =
            { "hold", "bite", "strike", "reel", "land", "castBack", "castRelease" };

        /// <summary>The five transitions, each as the frame-pair the player actually sees: the LAST
        /// frame of the state being left, and the FIRST frame of the state being entered.</summary>
        static readonly (string name, string from, string to)[] Transitions =
        {
            ("hold→cast",   "hold",        "castBack"),
            ("cast→hold",   "castRelease", "hold"),
            ("hold→ground", "hold",        "rest:ground"),
            ("hold→stow-V", "hold",        "rest:stowV"),
            ("hold→stow-H", "hold",        "rest:stowH"),
        };

        // Tolerances. Pivot, blank length and yaw are EXACT — nothing legitimate moves them. The
        // rendered extent and the on-screen angle get one eased frame's worth, and are additionally
        // held to the entering state's own largest per-frame step, so a seam can never be wider than
        // the animation it joins.
        const double PivotPx = 0.01, LenM = 1e-9, YawDeg = 0.01, InkPx = 2.0, AngleDeg = 1.5;

        sealed class Side
        {
            public double GripX, GripY, LenM, AngleDeg, PitchDeg, YawDeg, Bend, LiftM, InkDiag;
            public string Hand;
        }

        static string Js(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        static string Num(double d) => d.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>The rod options for a state at parameter <paramref name="u"/>: a rest addresses the
        /// rig directly, a held state is posed by <c>CharacterIso.tool()</c> exactly as the baker poses
        /// it. One expression, so the test cannot pose the rod differently from the bake.</summary>
        static string OptsJs(string rod, string ch, string state, double u, double dir, string tier)
            => state.StartsWith("rest:", StringComparison.Ordinal)
                ? $"{{tier:{Js(tier)},rest:{Js(state.Substring(5))},u:{Num(u)}}}"
                : $"(function(){{var t={ch}.tool({Num(dir)},{{anim:{Js(state)},u:{Num(u)}}});" +
                  $"return {{tier:{Js(tier)},pitch:t.pitch,yaw:t.yaw,bend:t.bend}};}})()";

        static string PoseJs(string rod, string ch, string state, double u, double dir, string tier)
            => state.StartsWith("rest:", StringComparison.Ordinal)
                ? $"{rod}.poseOf({Js(state.Substring(5))},{Num(u)},{OptsJs(rod, ch, state, u, dir, tier)})"
                : $"{rod}.poseOf('held',null,{OptsJs(rod, ch, state, u, dir, tier)})";

        static Side Measure(IRigScriptHost host, in RigGeometry geo, string rod, string ch,
                            string state, double u, int d, string tier, bool withInk = true)
        {
            string opts = OptsJs(rod, ch, state, u, d, tier);
            // The grip centre, projected: RodIso.gripLocal() is the rod's origin in EVERY state, and
            // where that origin lands in the cell is the whole of "the rod did not teleport".
            string json = host.EvaluateString(
                $"JSON.stringify((function(){{var o={opts};var p={PoseJs(rod, ch, state, u, d, tier)};" +
                $"var g={rod}.project({d},{rod}.gripLocal(o),{rod}.defaultElev);" +
                $"var t={rod}.tip({d},o);" +
                $"var gx={rod}.pivot.x+g.dx, gy={rod}.pivot.y+g.dy;" +
                "return {gx:gx, gy:gy, len:" + rod + ".TIERS[" + Js(tier) + "].len," +
                " ang:Math.atan2(-(t.y-gy),t.x-gx)*180/Math.PI," +
                " pitch:p.pitch*180/Math.PI, yaw:p.yaw*180/Math.PI, bend:p.bend, lift:p.lift," +
                " hand:p.hand};})())");

            byte[] rgba = null;
            if (withInk)
            {
                rgba = host.EvaluateBytes($"{rod}.render({d},{opts})");
                Assert.AreEqual(geo.Width * geo.Height * 4, rgba.Length, $"{state}@{u} render size");
            }

            var s = new Side
            {
                GripX = J(json, "gx"), GripY = J(json, "gy"), LenM = J(json, "len"),
                AngleDeg = J(json, "ang"), PitchDeg = J(json, "pitch"), YawDeg = J(json, "yaw"),
                Bend = J(json, "bend"), LiftM = J(json, "lift"), Hand = JStr(json, "hand"),
                InkDiag = rgba == null ? 0.0 : InkDiagonal(rgba, geo.Width, geo.Height),
            };
            return s;
        }

        /// <summary>The diagonal of the tightest box holding any opaque pixel — the rendered rod's
        /// SIZE, measured off the pixels rather than inferred from the pose that made them.</summary>
        static double InkDiagonal(byte[] rgba, int w, int h)
        {
            int x0 = w, y0 = h, x1 = -1, y1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (rgba[(y * w + x) * 4 + 3] == 0) continue;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            return x1 < 0 ? 0.0 : Math.Sqrt(Math.Pow(x1 - x0 + 1, 2) + Math.Pow(y1 - y0 + 1, 2));
        }

        // A deliberately small JSON reader: these payloads are the flat objects built above, and
        // pulling in a parser for them would be more machinery than the values are worth.
        static double J(string json, string key)
        {
            int i = json.IndexOf($"\"{key}\":", StringComparison.Ordinal);
            Assert.GreaterOrEqual(i, 0, $"'{key}' missing from {json}");
            int start = i + key.Length + 3;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'
                   || json[end] == '.' || json[end] == 'e' || json[end] == 'E' || json[end] == '+')) end++;
            return double.Parse(json.Substring(start, end - start), CultureInfo.InvariantCulture);
        }

        static string JStr(string json, string key)
        {
            int i = json.IndexOf($"\"{key}\":\"", StringComparison.Ordinal);
            Assert.GreaterOrEqual(i, 0, $"'{key}' missing from {json}");
            int start = i + key.Length + 4;
            return json.Substring(start, json.IndexOf('"', start) - start);
        }

        /// <summary>A rig's own string array, read element by element — the rigs are the authority on
        /// their tiers and rests, and this test restates neither.</summary>
        static string[] RigStrings(IRigScriptHost host, string arrayExpr)
        {
            int n = (int)host.EvaluateNumber($"{arrayExpr}.length");
            var outp = new string[n];
            for (int i = 0; i < n; i++) outp[i] = host.EvaluateString($"{arrayExpr}[{i}]");
            return outp;
        }

        static double DeltaAngle(double a, double b)
        {
            double d = ((b - a + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
            return Math.Abs(d);
        }

        static double FrameU(IRigScriptHost host, string rod, string ch, string state, int f)
        {
            if (state.StartsWith("rest:", StringComparison.Ordinal))
                return (double)f / (host.EvaluateNumber($"{rod}.REST_FRAMES") - 1);
            return f / (double)CharacterRigBaker.FramesOf(host, ch, state);
        }

        static int FrameCount(IRigScriptHost host, string rod, string ch, string state)
            => state.StartsWith("rest:", StringComparison.Ordinal)
                ? (int)host.EvaluateNumber($"{rod}.REST_FRAMES")
                : CharacterRigBaker.FramesOf(host, ch, state);

        // =========================================================================================

        /// <summary>
        /// ⭐ The load-bearing one. Every transition, every facing, every tier: the rod does not
        /// teleport, does not resize, does not re-point, and does not change hands at the seam.
        /// </summary>
        [Test]
        public void EveryTransition_KeepsOneRod_AtEveryFacing()
        {
            using var host = RigScriptHostFactory.Create();
            var rodEntry = RigCatalog.Get("rod");
            var charEntry = RigCatalog.Get("character");
            var geo = RigCatalog.Install(host, rodEntry);
            RigCatalog.Install(host, charEntry);
            string rod = rodEntry.GlobalName, ch = charEntry.GlobalName;

            var failures = new List<string>();
            var stepCache = new Dictionary<string, (double ink, double angle)>();
            foreach (string tier in RigStrings(host, $"{rod}.order"))
                foreach (var t in Transitions)
                    for (int d = 0; d < Dirs; d++)
                    {
                        Side a = Measure(host, geo, rod, ch, t.from, 1.0, d, tier);
                        Side b = Measure(host, geo, rod, ch, t.to, 0.0, d, tier);
                        string at = $"{tier} {t.name} @{Facings[d]}";

                        double pivot = Math.Sqrt(Math.Pow(b.GripX - a.GripX, 2) + Math.Pow(b.GripY - a.GripY, 2));
                        if (pivot > PivotPx) failures.Add($"{at}: the grip TELEPORTS {pivot:F3} px.");
                        if (Math.Abs(b.LenM - a.LenM) > LenM)
                            failures.Add($"{at}: the blank changes length by {Math.Abs(b.LenM - a.LenM):F4} m.");
                        if (Math.Abs(b.YawDeg - a.YawDeg) > YawDeg)
                            failures.Add($"{at}: the rod is re-pointed {Math.Abs(b.YawDeg - a.YawDeg):F2}° in yaw.");
                        if (a.Hand != b.Hand)
                            failures.Add($"{at}: the hand changes {a.Hand}→{b.Hand} AT the seam — a " +
                                         "hand-over has to be animated, not cut.");

                        string stepKey = $"{tier}|{t.to}|{d}";
                        if (!stepCache.TryGetValue(stepKey, out var step))
                            stepCache[stepKey] = step = LargestOwnStep(host, geo, rod, ch, t.to, d, tier);
                        double dInk = Math.Abs(b.InkDiag - a.InkDiag);
                        if (dInk > Math.Max(InkPx, step.ink))
                            failures.Add($"{at}: the rod changes size by {dInk:F2} px, more than " +
                                         $"{t.to}'s own largest frame step ({step.ink:F2} px).");
                        double dAng = DeltaAngle(a.AngleDeg, b.AngleDeg);
                        if (dAng > Math.Max(AngleDeg, step.angle))
                            failures.Add($"{at}: the rod swings {dAng:F2}°, more than {t.to}'s own " +
                                         $"largest frame step ({step.angle:F2}°).");
                    }

            Assert.IsEmpty(failures, "the rod broke across " + failures.Count + " seam checks:\n  · " +
                           string.Join("\n  · ", failures));
        }

        static (double ink, double angle) LargestOwnStep(IRigScriptHost host, in RigGeometry geo,
                                                         string rod, string ch, string state, int d, string tier)
        {
            int n = FrameCount(host, rod, ch, state);
            double ink = 0, ang = 0;
            Side prev = null;
            for (int f = 0; f < n; f++)
            {
                Side m = Measure(host, geo, rod, ch, state, FrameU(host, rod, ch, state, f), d, tier);
                if (prev != null)
                {
                    ink = Math.Max(ink, Math.Abs(m.InkDiag - prev.InkDiag));
                    ang = Math.Max(ang, DeltaAngle(prev.AngleDeg, m.AngleDeg));
                }
                prev = m;
            }
            return (ink, ang);
        }

        /// <summary>The sprite's pivot is the GRIP CENTRE in every state. This is what the consumer
        /// pins to the fisher's hand, so if it means one thing while held and another while stowed,
        /// the rod moves without anything having animated it — which is exactly how it used to.</summary>
        [Test]
        public void TheGripIsTheCellPivot_InEveryState()
        {
            using var host = RigScriptHostFactory.Create();
            var rodEntry = RigCatalog.Get("rod");
            var charEntry = RigCatalog.Get("character");
            var geo = RigCatalog.Install(host, rodEntry);
            RigCatalog.Install(host, charEntry);
            string rod = rodEntry.GlobalName, ch = charEntry.GlobalName;

            var states = new List<string>(HeldStates);
            foreach (string r in RigStrings(host, $"{rod}.REST")) states.Add("rest:" + r);

            foreach (string tier in RigStrings(host, $"{rod}.order"))
                foreach (string state in states)
                {
                    int n = FrameCount(host, rod, ch, state);
                    for (int d = 0; d < Dirs; d++)
                        for (int f = 0; f < n; f++)
                        {
                            Side m = Measure(host, geo, rod, ch, state,
                                             FrameU(host, rod, ch, state, f), d, tier, withInk: false);
                            Assert.AreEqual(geo.PivotX, m.GripX, PivotPx,
                                $"{tier}/{state} dir{d} f{f}: the grip is not on the cell's pivot x");
                            Assert.AreEqual(geo.PivotY, m.GripY, PivotPx,
                                $"{tier}/{state} dir{d} f{f}: the grip is not on the cell's pivot y");
                        }
                }
        }

        /// <summary>The two cross-rig pins. The rod rig has to state the stance it is handed over from
        /// and the yaw it is held at, because it cannot see the character rig that drives them — so
        /// those two statements are checked against the rig that actually drives them. This is the
        /// exact drift that produced the defect: the rests were authored at yaw 0 while every frame the
        /// character rig drives is at 16°, and nobody was comparing the two.</summary>
        [Test]
        public void TheRodRigsStatedStanceAndYaw_MatchTheCharacterRigThatDrivesThem()
        {
            using var host = RigScriptHostFactory.Create();
            var rodEntry = RigCatalog.Get("rod");
            var charEntry = RigCatalog.Get("character");
            RigCatalog.Install(host, rodEntry);
            RigCatalog.Install(host, charEntry);
            string rod = rodEntry.GlobalName, ch = charEntry.GlobalName;

            double heldYaw = host.EvaluateNumber($"{rod}.HELD_YAW");
            for (int d = 0; d < Dirs; d++)
            {
                foreach (string k in new[] { "pitch", "yaw", "bend" })
                    Assert.AreEqual(
                        host.EvaluateNumber($"{ch}.tool({d},{{anim:'hold',u:1}}).{k}"),
                        host.EvaluateNumber($"{rod}.STANCE.{k}"), 1e-9,
                        $"RodIso.STANCE.{k} must be the hold stance the hand actually hands the rod " +
                        "over from — the rests all start from it, so a drift here re-opens the jump.");

                foreach (string anim in HeldStates)
                {
                    int n = CharacterRigBaker.FramesOf(host, ch, anim);
                    for (int f = 0; f < n; f++)
                        Assert.AreEqual(heldYaw,
                            host.EvaluateNumber($"{ch}.tool({d},{{anim:{Js(anim)},frame:{f}}}).yaw"), 1e-9,
                            $"{anim} dir{d} f{f} is driven at a different yaw from RodIso.HELD_YAW — " +
                            "one rod, one yaw, held or stowed.");
                }
            }
        }

        /// <summary>A rest is an animated hand-over, not a snapshot: more than one frame, its first
        /// frame IS the stance, and the hand lets go somewhere in the middle.</summary>
        [Test]
        public void EveryRest_IsAnAnimatedHandOver()
        {
            using var host = RigScriptHostFactory.Create();
            var rodEntry = RigCatalog.Get("rod");
            RigCatalog.Install(host, rodEntry);
            string rod = rodEntry.GlobalName;

            int frames = (int)host.EvaluateNumber($"{rod}.REST_FRAMES");
            double releaseAt = host.EvaluateNumber($"{rod}.RELEASE_AT");
            Assert.Greater(frames, 1, "a rest of one cell cannot be entered from the hand without a cut");
            Assert.That(releaseAt, Is.GreaterThan(0.0).And.LessThan(1.0),
                "the hand must let go DURING the animation — not at the seam, and not never");

            foreach (string rest in RigStrings(host, $"{rod}.REST"))
            {
                Assert.AreEqual("R", host.EvaluateString($"{rod}.poseOf({Js(rest)},0,{{}}).hand"),
                    $"'{rest}' must still be in the hand at frame 0 — that frame is the seam");
                Assert.AreEqual("none", host.EvaluateString($"{rod}.poseOf({Js(rest)},1,{{}}).hand"),
                    $"'{rest}' must be out of the hand once settled");
                Assert.Greater(host.EvaluateNumber($"{rod}.restLift({Js(rest)},{{tier:'coast'}})"), 0.0,
                    $"'{rest}' must say how high it holds the grip above what it rests on — that datum " +
                    "is what stopped being a pixel offset that moved the rod");
            }
        }
    }
}
