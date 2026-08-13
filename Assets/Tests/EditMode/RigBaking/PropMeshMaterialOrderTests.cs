using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The material INDEX is the thing a fitting bakes, and a rig can move it without moving a
    /// pixel.</b>
    ///
    /// <para>A fitting's mesh stores <c>UV0 = (materialId, …)</c> — an INDEX — and its
    /// <see cref="HullPropMeshDef.Ramps"/> is that index's lookup table, held (per its own tooltip)
    /// "in the rig's MATS order". <see cref="RigMeshExtractor"/> builds that table by enumerating
    /// <c>MATS</c> with <c>for..in</c>, so <b>the rig's key order IS the baked index</b>.</para>
    ///
    /// <para>That makes a MATS reorder a silent, invisible break. Inserting one entry in the middle
    /// shifts every index after it; the rig's own render is unaffected (it resolves by NAME), the
    /// mesh geometry is unaffected, and only the committed fitting — which resolved its index
    /// against the OLD table — comes out wearing the wrong colours.</para>
    ///
    /// <para><b>Measured, not hypothesised.</b> The fleet rig pack's <c>skiffMotorRig.js</c> added a
    /// <c>guard</c> livery whose <c>orng</c> entry sits between <c>steel</c> and <c>moto</c>:</para>
    /// <code>
    ///   before:  0 paint  1 trim  2 steel  3 moto  4 blk
    ///   after :  0 paint  1 trim  2 steel  3 orng  4 moto  5 blk
    /// </code>
    /// <para>Landed against the pre-drop bake, that turned every <c>moto</c> face of the outboard
    /// cowl rescue-orange. <see cref="OutboardPropMeshAcceptanceTests"/> caught it — 4 of 5 red,
    /// with the tell-tale signature <b>0 silhouette differences and 65% of inked pixels wrong</b>
    /// (colour moved, geometry did not). This fixture is the cheap, explicitly-named guard for the
    /// same fault: it compares the table directly instead of rasterising 104 poses to find it, and
    /// it says in one line what to do about it.</para>
    /// </summary>
    public class PropMeshMaterialOrderTests
    {
        /// <summary>
        /// Every committed fitting's ramp table still matches, entry for entry and IN ORDER, the
        /// <c>MATS</c> of the rig it was baked from. Fails the moment a rig reorders, inserts or
        /// renames a material without a re-bake.
        /// </summary>
        [Test]
        public void EveryCommittedFitting_RampTableStillMatchesItsRigsMatsOrder()
        {
            var report = new StringBuilder();
            bool bad = false;

            foreach (FleetProp p in HullPropFleet.All)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullPropMeshDef>(p.AssetPath);
                Assert.IsNotNull(def, $"{p.AssetPath} did not load — {p.Label} is not committed.");

                using IRigScriptHost host = RigScriptHostFactory.Create();
                RigMeshData rig = RigMeshExtractor.ExtractFrom(host, p.ScriptPath, p.GlobalName,
                                                               p.Extraction);

                string names = string.Join(" ", rig.Materials.Select((m, i) => $"{i}:{m.Name}"));
                report.AppendLine($"{p.Key,-20} rig MATS [{names}]");

                if (def.Ramps.Length != rig.Materials.Count)
                {
                    bad = true;
                    report.AppendLine($"    ✗ committed table has {def.Ramps.Length} ramp(s), the rig " +
                                      $"now has {rig.Materials.Count}.");
                    continue;
                }

                for (int i = 0; i < rig.Materials.Count; i++)
                {
                    RigMaterial m = rig.Materials[i];
                    HullMeshDef.Ramp baked = def.Ramps[i];

                    if (baked.Offset != m.Off)
                    {
                        bad = true;
                        report.AppendLine($"    ✗ index {i} ({m.Name}): offset {baked.Offset} baked, " +
                                          $"{m.Off} in the rig.");
                    }

                    if (baked.Colors == null || baked.Colors.Length != m.Ramp.Length ||
                        baked.Colors.Where((c, k) => !Same(c, m.Ramp[k])).Any())
                    {
                        bad = true;
                        report.AppendLine($"    ✗ index {i} ({m.Name}): the baked ramp is not this " +
                                          "rig material's ramp.");
                    }
                }
            }

            Assert.IsFalse(bad,
                "A committed fitting's ramp table no longer lines up with its rig's MATS.\n\n" +
                "The mesh stores a material INDEX in UV0, so a rig that reorders, inserts or renames a " +
                "MATS entry re-points every index after it. The rig's own render is unaffected (it " +
                "resolves by NAME) — only the committed bake is wrong, and it is wrong in COLOUR while " +
                "the silhouette stays perfect, which is exactly the fault that looks like nothing.\n\n" +
                "Fix: re-bake the fittings — Hidden Harbours ▸ Dev ▸ Rig meshes ▸ Bake ALL hull " +
                "fittings (oars, outboards) — in the SAME commit as the rig.\n\n" + report);
        }

        /// <summary>
        /// The skiff outboard's MATS order, pinned as a literal.
        ///
        /// <para>Belt and braces over the fixture above, and it earns its place: it names the
        /// insertion point, so a reviewer reading a future diff can see at a glance that
        /// <c>orng</c> arrived in the MIDDLE rather than at the end — the detail that made the
        /// re-bake mandatory rather than optional. Change this literal only together with a
        /// re-bake.</para>
        /// </summary>
        [Test]
        public void TheSkiffOutboardsMatsOrder_IsPinned()
        {
            FleetProp p = HullPropFleet.All.First(f => f.Key == "skiffMotorWork");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData rig = RigMeshExtractor.ExtractFrom(host, p.ScriptPath, p.GlobalName,
                                                           p.Extraction);

            CollectionAssert.AreEqual(
                new[] { "paint", "trim", "steel", "orng", "moto", "blk" },
                rig.Materials.Select(m => m.Name).ToArray(),
                "skiffMotorRig.js's MATS order changed. It is the baked material index of every " +
                "committed skiff outboard — re-bake the fittings in this same commit, then update " +
                "this literal. ('orng' is the guard livery that arrived with the fleet rig pack; it " +
                "sits between 'steel' and 'moto', which is why landing it shifted moto 3→4 and " +
                "blk 4→5.)");
        }

        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}
