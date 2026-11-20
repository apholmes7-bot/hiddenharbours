using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>No sea inside a hull (owner, 2026-11-19).</b> The dory's guard, carried to every hull with an
    /// inside lining. The sea guard (ADR 0023) draws the displaced water on a side whose interior code
    /// says "exterior", so a lining face baked with side code 0 shows the sea inside the boat wherever
    /// it sits below the waterline.
    ///
    /// <para><b>The causes, proved per hull against the classifier's own sampling:</b></para>
    /// <list type="bullet">
    /// <item><b>Punt:</b> the inner garboard stood ON the bottom's plane and the inner skin's aft edge ON
    /// the transom's plane (both inside the classifier's 4 mm coplanar band, so seen from under the hull);
    /// the floorboards stopped 6 % short of the inner skin; the forward thwart's ends came out through the
    /// planking; the bow was open between the two skins; and the stern board, one face, had a bare inside.
    /// The rig now stands the inner skin 48 mm over the keel and 6 mm off the transom, runs the floor to the
    /// skin, keeps the thwarts inside it, closes the stem and adds an inner transom board.</item>
    /// <item><b>Console skiff and sport skiff:</b> the sole stopped 4 % short of the liner, and the slot
    /// beside it showed the inside of the outer skin under the waterline. The sole now runs 3 mm into the
    /// liner. The console's liner also stopped on the transom plane, and its transom had a bare inside: the
    /// liner now stops 6 mm short and an inner transom panel covers it.</item>
    /// <item><b>Rooms</b> (ADR 0041) are appended after the classifier runs, with code 0 by construction;
    /// the guard pass now reads their interior flag the way the caustic path always has, so they are not
    /// policed here.</item>
    /// </list>
    ///
    /// <para>The dory has its own fixture (<c>DoryLiningSeaGuardTests</c>) and is left out here.</para>
    ///
    /// <para>CPU only, so CI adjudicates it: it reads the committed meshes' vertex attributes.</para>
    /// </summary>
    public class HullLiningSeaGuardTests
    {
        const string Meshes = "Assets/_Project/Data/Boats/HullMeshes/";
        const string DoryMesh = Meshes + "DoryIsoHullMesh.asset";
        const int LiningRole = 3;              // RO.lining in the role-tagged rigs: UV1.z carries role + 1
        const float FootMinimum = 0.04f;       // m over the keel bottom: clear of the 4 mm coplanar band with margin
        const float RockMargin = 0.05f;        // m over the resting draught: the water's reach as she rocks

        // The hulls without a role channel name their inside surfaces by colour slot (UV0.x, the index of
        // the material in the rig's own list). Every face in these slots is inside the boat.
        static readonly (string Mesh, int[] Slots, string Names)[] SlotHulls =
        {
            ("PuntIsoHullMesh.asset", new[] { 4 }, "wood: inner skin, floorboards, thwarts, battens, inner transom"),
            ("ConsoleIsoHullMesh.asset", new[] { 4, 5 }, "liner, sole"),
            ("SportSkiffIsoHullMesh.asset", new[] { 2, 4 }, "gel liner, non-skid sole"),
        };

        sealed class Hull
        {
            public HullMeshDef Def;
            public List<Vector3> Verts = new List<Vector3>();
            public List<Vector4> Attrs = new List<Vector4>();
            public List<Vector3> Tags = new List<Vector3>();
        }

        static Hull Load(string path)
        {
            var h = new Hull { Def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(path) };
            Assert.IsNotNull(h.Def, path);
            Assert.IsNotNull(h.Def.Mesh, $"{path}: no mesh sub-asset");
            h.Def.Mesh.GetVertices(h.Verts);
            h.Def.Mesh.GetUVs(RigMeshBuilder.AttrUvChannel, h.Attrs);
            h.Def.Mesh.GetUVs(RigMeshBuilder.LevelUvChannel, h.Tags);
            Assert.AreEqual(h.Verts.Count, h.Attrs.Count, $"{path}: the interior mask channel is missing");
            return h;
        }

        // A vertex the sea could reach: under the resting waterline plus the rock margin.
        static bool Wettable(Hull h, int i) => h.Verts[i].z < h.Def.RestingDraftMeters + RockMargin;

        static void Police(string who, Hull h, System.Func<int, bool> inside, List<string> faults)
        {
            float keel = h.Def.Mesh.bounds.min.z;
            int count = 0, wet = 0;
            float lowest = float.MaxValue, lowestWet = float.MaxValue;
            for (int i = 0; i < h.Verts.Count; i++)
            {
                if (!inside(i)) continue;
                count++;
                lowest = Mathf.Min(lowest, h.Verts[i].z);
                if (Mathf.RoundToInt(h.Attrs[i].w) == 0 && Wettable(h, i))
                {
                    wet++;
                    lowestWet = Mathf.Min(lowestWet, h.Verts[i].z);
                }
            }
            if (count == 0)
            {
                faults.Add($"{who}: no inside-lining vertices found (did the slot or role move?)");
                return;
            }
            if (wet > 0)
                faults.Add($"{who}: {wet} inside-lining vertices under the waterline carry side code 0 (lowest at " +
                           $"{lowestWet:0.###} m), so the sea draws on them inside the boat");
            if (lowest - keel < FootMinimum && lowest < h.Def.RestingDraftMeters + RockMargin)
                faults.Add($"{who}: the inside lining reaches {lowest - keel:0.###} m over the keel bottom. Within " +
                           "the classifier's 4 mm coplanar band of the bottom it is seen from under the hull and baked exterior.");
        }

        [Test]
        public void TheHullsWithoutRoles_KeepTheSeaOffTheirInsideSurfaces()
        {
            var faults = new List<string>();
            foreach (var (mesh, slots, names) in SlotHulls)
            {
                var h = Load(Meshes + mesh);
                var set = new HashSet<int>(slots);
                Police($"{mesh} ({names})", h, i => set.Contains(Mathf.RoundToInt(h.Attrs[i].x)), faults);
            }
            Assert.IsEmpty(faults, string.Join("\n", faults));
        }

        [Test]
        public void EveryRoleTaggedHull_KeepsTheSeaOffItsLining()
        {
            var faults = new List<string>();
            int policed = 0;
            foreach (var hull in HullMeshFleet.Hulls)
            {
                if (hull.MeshAssetPath == DoryMesh) continue;   // DoryLiningSeaGuardTests
                var h = Load(hull.MeshAssetPath);
                if (h.Tags.Count != h.Verts.Count) continue;     // no role channel
                bool any = false;
                for (int i = 0; i < h.Verts.Count && !any; i++)
                    any = Mathf.RoundToInt(h.Tags[i].z) - 1 == LiningRole && h.Tags[i].y < 0.5f;
                if (!any) continue;                              // no lining of its own (a room's is not policed)
                policed++;
                Police(hull.Key, h, i => Mathf.RoundToInt(h.Tags[i].z) - 1 == LiningRole && h.Tags[i].y < 0.5f, faults);
            }
            Assert.Greater(policed, 0, "no role-tagged hull with a lining was found: the role channel moved");
            Assert.IsEmpty(faults, string.Join("\n", faults));
        }
    }
}
