using System.IO;
using NUnit.Framework;
using UnityEditor;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The two sloops' pictures turn the way their hulls turn.
    ///
    /// <para>Sloop 30's mesh carried <c>AzimuthCounterClockwise: 0</c>, the only one of the fleet's hull
    /// meshes to do so, and her picture pointed the wrong way at 90 and 270 degrees: heading west, she was
    /// drawn bow east. The bake had no analytic pair on either sloop rig, so it fell back to guessing
    /// the bow from the silhouette's taper, and on a sloop the mast and rigging stand over the bow
    /// half and make it the WIDE end (taper 2.45, read backwards). Both rigs now publish a centreline
    /// bow and stern eye (<c>painter</c>, <c>sternEye</c>), which the bake reads first.</para>
    /// </summary>
    public class SloopPictureHeadingTests
    {
        static readonly (string catalogKey, string mesh)[] Sloops =
        {
            ("sloop30", "Assets/_Project/Data/Boats/HullMeshes/SloopIsoHullMesh.asset"),
            ("sloop88", "Assets/_Project/Data/Boats/HullMeshes/Sloop88IsoHullMesh.asset"),
        };

        [Test]
        public void BothSloopMeshes_TurnTheWayTheirRigsAreDeclaredToTurn()
        {
            foreach (var (key, path) in Sloops)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(path);
                Assert.IsNotNull(def, path);
                bool declaredCcw = RigCatalog.Get(key).DeclaredConvention == AzimuthConvention.CounterClockwise;
                Assert.AreEqual(declaredCcw, def.AzimuthCounterClockwise,
                    $"{key}: the mesh's measured convention must match the catalog's, which was measured " +
                    "from the rig's own anchors. A mismatch draws her picture mirrored at 90 and 270 degrees.");
            }
        }

        [Test]
        public void BothSloopRigs_PublishACentrelineBowAndSternEye_WithTheBowWestAtAQuarterTurn()
        {
            foreach (var (key, _) in Sloops)
            {
                RigEntry rig = RigCatalog.Get(key);
                using var host = new V8RigScriptHost();
                host.Execute(File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, rig.ScriptPath)));
                string g = rig.GlobalName, view = $"{{elev:{g}.defaultElev}}";

                Assert.IsTrue(host.EvaluateBool($"typeof {g}.painter === 'function' && typeof {g}.sternEye === 'function'"),
                    $"{g} must publish painter(dir, opts) and sternEye(dir, opts): they are what the mesh bake " +
                    "reads her azimuth convention from instead of the silhouette guess.");
                Assert.AreEqual(0.0, host.EvaluateNumber($"{g}.painter(0,{view}).x - {g}.sternEye(0,{view}).x"), 1e-6,
                    $"{g}: both eyes must be on the centreline (same screen x at heading 0), or the bake rejects the pair.");
                Assert.Less(host.EvaluateNumber($"{g}.painter(0,{view}).y"), host.EvaluateNumber($"{g}.sternEye(0,{view}).y"),
                    $"{g}: at dir 0 the bow eye must be north (up the screen) of the stern eye.");
                Assert.Less(host.EvaluateNumber($"{g}.painter(2,{view}).x - {g}.sternEye(2,{view}).x"), -100.0,
                    $"{g}: at cell 2 the rig draws her bow WEST, which is a counter-clockwise turntable.");
            }
        }

        [Test]
        public void HeadingWest_DrawsEachSloopWithHerBowOnTheWest()
        {
            foreach (var (key, path) in Sloops)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(path);
                RigEntry rig = RigCatalog.Get(key);
                using var host = new V8RigScriptHost();
                host.Execute(File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, rig.ScriptPath)));
                string g = rig.GlobalName, view = $"{{elev:{g}.defaultElev}}";

                foreach (float compass in new[] { 270f, 90f })
                {
                    float dir = HullMeshMath.HeadingToDirUnits(compass, 0f, def.AzimuthCounterClockwise);
                    string d = dir.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                    double bowMinusStern = host.EvaluateNumber($"{g}.painter({d},{view}).x - {g}.sternEye({d},{view}).x");
                    if (compass == 270f)
                        Assert.Less(bowMinusStern, -100.0, $"{key} heading west must be drawn with her bow on the west (left).");
                    else
                        Assert.Greater(bowMinusStern, 100.0, $"{key} heading east must be drawn with her bow on the east (right).");
                }
            }
        }
    }
}
