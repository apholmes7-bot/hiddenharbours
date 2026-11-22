using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The dory's stowed oars fit her (owner ruling 2026-11-22).</b> The stow on
    /// <c>visual.dory_iso</c> is measured against the dory's RIG, in the repo's own V8: each oar lies
    /// fore and aft inside her planking from handle to blade tip, its loom rests on her two rowing
    /// thwarts, clear of the thwart knees, and it ends forward of where he sits at the tiller, so it
    /// cannot clip him, the thwarts or the motor. A test that restated the stow's numbers would only
    /// agree with itself; these ask the hull.
    /// </summary>
    public class DoryOarStowFitTests
    {
        const string RigPath = "docs/art/rigs/doryIsoRig.js";
        const string RigGlobal = "DoryIso";
        const string VisualAsset = "Assets/_Project/Data/Boats/Visuals/DoryIso.asset";

        /// <summary>Frames stand this far proud of the planking, inboard (the rig's frame offset).</summary>
        const double FrameStandoff = 0.028;
        /// <summary>The thwart knees' inboard depth off the thwart end (the rig's knee box).</summary>
        const double KneeDepth = 0.06;
        /// <summary>A thwart's end stands this far short of the planking (the rig's thwart margin).</summary>
        const double ThwartEndMargin = 0.006;
        /// <summary>The rowing thwarts' thickness (the rig's thwart board).</summary>
        const double ThwartThickness = 0.045;
        /// <summary>How far above a thwart a loom counts as RESTING on it rather than floating.</summary>
        const double RestTolerance = 0.005;
        /// <summary>The sampling step along each oar, metres.</summary>
        const double Step = 0.01;

        static readonly string[] Symbols =
            { "halfAt", "station", "oarlockPt", "SEAT", "THWARTS", "O_LOUT", "O_LIN", "O_TS", "O_BW", "O_BLEN", "L" };

        IRigScriptHost _host;
        BoatVisualDef _visual;

        [OneTimeSetUp]
        public void Load()
        {
            _host = RigScriptHostFactory.Create();
            _host.Execute(RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, RigPath)), RigGlobal, Symbols, RigPath));
            _visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(VisualAsset);
            Assert.IsNotNull(_visual, VisualAsset);
        }

        [OneTimeTearDown]
        public void Unload() => _host?.Dispose();

        double N(string expr) => _host.EvaluateNumber($"{RigGlobal}.{expr}");

        double HalfBeamInside(double y, double z) =>
            _host.EvaluateNumber(
                $"{RigGlobal}.halfAt(({y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)} + {RigGlobal}.L / 2) / {RigGlobal}.L, " +
                $"{z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}, 1)");

        Vector3 Tholes(int side) => new Vector3(
            (float)N($"oarlockPt({side})[0]"), (float)N($"oarlockPt({side})[1]"), (float)N($"oarlockPt({side})[2]"));

        struct Thwart { public double Y, Top, InnerEnd; }

        Thwart RowingThwart(int index)
        {
            double y = N($"station({RigGlobal}.THWARTS[{index}]).y");
            double top = N($"station({RigGlobal}.THWARTS[{index}]).kz") + N("SEAT");
            double end = HalfBeamInside(y, top - ThwartThickness) - ThwartEndMargin;
            return new Thwart { Y = y, Top = top, InnerEnd = end - KneeDepth };
        }

        [Test]
        public void TheDorysStowIsOn_AndMeasuredFromHerRealTholes()
        {
            Assert.IsTrue(_visual.OarStow.Enabled, "the dory stows her oars under power.");
            Assert.IsTrue(_visual.HasOarMeshes(), "…and she carries mesh oars to stow.");
            Assert.That((_visual.OarPortMesh.PivotLocalMeters - Tholes(-1)).magnitude, Is.LessThan(1e-3f),
                "the port fitting turns about the rig's port tholes, so the stow's shift is measured from them.");
            Assert.That((_visual.OarStarMesh.PivotLocalMeters - Tholes(+1)).magnitude, Is.LessThan(1e-3f),
                "…and the starboard about the starboard tholes.");
        }

        /// <summary>The baked oar is pushed back by the rig's depth bias (UV0.z, subtracted from depth in
        /// the facet shader), which would bury a loom lying on a thwart in that thwart. The stow's lift must
        /// cancel at least the least-pushed face's bias (the loom's), read off the committed bake.</summary>
        [Test]
        public void TheStowsCameraLift_CancelsTheBakedOarsDepthBias([Values(-1, 1)] int side)
        {
            Mesh mesh = (side < 0 ? _visual.OarPortMesh : _visual.OarStarMesh).Mesh;
            Assert.IsNotNull(mesh, "the baked oar mesh");
            var uv = new System.Collections.Generic.List<Vector4>();
            mesh.GetUVs(0, uv);
            Assert.That(uv.Count, Is.GreaterThan(0), "the oar's per-face attributes");
            float leastPushedBack = float.MaxValue;
            foreach (Vector4 a in uv) leastPushedBack = Mathf.Min(leastPushedBack, -a.z);
            Assert.That(leastPushedBack, Is.GreaterThan(0f), "the rig pushes every oar face back (db < 0).");
            Assert.That(_visual.OarStow.CameraLiftMeters, Is.GreaterThanOrEqualTo(leastPushedBack - 1e-5f),
                "the stowed loom must be drawn at least at its true depth, or the thwart it rests on hides it.");
        }

        [Test]
        public void EachStowedOar_LiesInsideHerPlanking_HandleToBladeTip([Values(-1, 1)] int side)
        {
            OarStowConfig stow = _visual.OarStow;
            Vector3 lockPt = stow.LockLocalMeters(side);
            Vector3 dir = DoryOarMeshPose.Direction(side, stow.SweepDegrees, stow.DipDegrees);
            double inboard = N("O_LIN"), outboard = N("O_LOUT"), loom = N("O_TS"),
                   blade = N("O_BW"), bladeLen = N("O_BLEN");

            for (double s = -inboard; s <= outboard + 1e-9; s += Step)
            {
                Vector3 p = lockPt + dir * (float)s;
                double halfWidth = s >= outboard - bladeLen ? blade : loom;  // the blade's widest, everywhere on it
                double room = HalfBeamInside(p.y, p.z - loom) - FrameStandoff - (Mathf.Abs(p.x) + halfWidth);
                Assert.That(room, Is.GreaterThanOrEqualTo(0.0),
                    $"side {side}: at {s:F2} m along the oar ({p}) it stands {-room:F3} m through her frames.");
            }
        }

        [Test]
        public void EachStowedLoom_RestsOnBothRowingThwarts_ClearOfTheirKnees([Values(-1, 1)] int side)
        {
            OarStowConfig stow = _visual.OarStow;
            Vector3 lockPt = stow.LockLocalMeters(side);
            Vector3 dir = DoryOarMeshPose.Direction(side, stow.SweepDegrees, stow.DipDegrees);
            double loom = N("O_TS");
            double lowestGap = double.MaxValue;

            for (int i = 0; i < 2; i++)
            {
                Thwart t = RowingThwart(i);
                Vector3 p = lockPt + dir * (float)((t.Y - lockPt.y) / dir.y);
                double gap = p.z - loom - t.Top;
                Assert.That(gap, Is.GreaterThanOrEqualTo(-1e-4),
                    $"side {side}: the loom sinks {-gap:F4} m into rowing thwart {i}.");
                lowestGap = System.Math.Min(lowestGap, gap);
                Assert.That(t.InnerEnd - (Mathf.Abs(p.x) + loom), Is.GreaterThanOrEqualTo(0.0),
                    $"side {side}: the loom crosses thwart {i}'s knee.");
            }
            Assert.That(lowestGap, Is.LessThanOrEqualTo(RestTolerance),
                $"side {side}: the loom floats {lowestGap:F4} m over the thwarts instead of resting on them.");
        }

        [Test]
        public void EachStowedOar_EndsForwardOfTheTillerStation_SoItClearsHimAndTheMotor([Values(-1, 1)] int side)
        {
            OarStowConfig stow = _visual.OarStow;
            Vector3 dir = DoryOarMeshPose.Direction(side, stow.SweepDegrees, stow.DipDegrees);
            Assert.That(dir.y, Is.GreaterThan(0f), "blades forward, handles aft.");
            Vector3 handleEnd = stow.LockLocalMeters(side) - dir * (float)N("O_LIN");

            TillerStationConfig tiller = _visual.Tiller;
            Assert.IsTrue(tiller.Enabled, "the outboard dory seats him at the tiller.");
            float aftmost = Mathf.Max(tiller.SeatLocalMeters.y,
                                      Mathf.Max(tiller.PortFootBraceLocalMeters.y, tiller.StarboardFootBraceLocalMeters.y));
            Assert.That(handleEnd.y, Is.GreaterThan(aftmost),
                $"side {side}: the handle end ({handleEnd.y:F3}) reaches back over his feet or seat ({aftmost:F3}).");
        }
    }
}
