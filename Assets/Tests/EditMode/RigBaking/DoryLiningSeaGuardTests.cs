using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The dory's inside planking keeps the sea out (owner, 2026-11-19).</b> The water showed
    /// inside her, under the floorboards and below the far planking.
    ///
    /// <para><b>The cause, proved against the committed mask:</b> the inside garboard ran down to
    /// the keel bottom, so its lower edge lay ON the outer bottom's plane. The interior classifier
    /// counts a side as seen when it is within 4 mm of the frontmost surface, so it saw that edge
    /// from below the hull and baked 25 lining faces "exterior on both sides" (side code 0). The
    /// guard then let the displaced sea draw over them between the bilge floor and the waterline.
    /// The rig now stands the garboard's inside on the bilge (<c>LINING_FOOT</c>, 12 mm under it),
    /// and every lining face bakes interior.</para>
    ///
    /// <para>CPU only, so CI adjudicates it: it reads the committed mesh's vertex attributes.</para>
    /// </summary>
    public class DoryLiningSeaGuardTests
    {
        const string DoryMesh = "Assets/_Project/Data/Boats/HullMeshes/DoryIsoHullMesh.asset";
        const int LiningRole = 3;              // RO.lining in the rig: UV1.z carries role + 1
        const float LiningFootMinimum = 0.04f; // m above the keel bottom; the rig stands it at 0.048

        static (List<Vector3> Verts, List<Vector4> Attrs, List<Vector3> Tags) Load()
        {
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(DoryMesh);
            Assert.IsNotNull(def, DoryMesh);
            Assert.IsNotNull(def.Mesh, "the dory has no mesh sub-asset");
            var verts = new List<Vector3>(); def.Mesh.GetVertices(verts);
            var attrs = new List<Vector4>(); def.Mesh.GetUVs(RigMeshBuilder.AttrUvChannel, attrs);
            var tags = new List<Vector3>(); def.Mesh.GetUVs(RigMeshBuilder.LevelUvChannel, tags);
            Assert.AreEqual(verts.Count, attrs.Count, "the interior mask channel is missing");
            Assert.AreEqual(verts.Count, tags.Count, "the role channel is missing");
            return (verts, attrs, tags);
        }

        [Test]
        public void NoLiningFaceOfTheDory_IsBakedExteriorOnBothSides()
        {
            var (verts, attrs, tags) = Load();
            int lining = 0, exterior = 0;
            float lowestExterior = float.MaxValue;
            for (int i = 0; i < verts.Count; i++)
            {
                if (Mathf.RoundToInt(tags[i].z) - 1 != LiningRole) continue;
                lining++;
                if (Mathf.RoundToInt(attrs[i].w) != 0) continue;
                exterior++;
                lowestExterior = Mathf.Min(lowestExterior, verts[i].z);
            }
            Assert.Greater(lining, 0, "no lining vertices found: the role channel moved");
            Assert.AreEqual(0, exterior,
                $"{exterior} lining vertices carry side code 0 (lowest at {lowestExterior:0.###} m), so the " +
                "sea draws over the dory's inside planking. Does a lining face reach the bottom's plane again?");
        }

        [Test]
        public void TheDorysInsidePlanking_StandsClearOfTheBottomPlane()
        {
            var (verts, _, tags) = Load();
            float lowest = float.MaxValue;
            for (int i = 0; i < verts.Count; i++)
                if (Mathf.RoundToInt(tags[i].z) - 1 == LiningRole) lowest = Mathf.Min(lowest, verts[i].z);
            Assert.GreaterOrEqual(lowest, LiningFootMinimum,
                $"the dory's lining reaches {lowest:0.###} m above the keel bottom. Within the classifier's " +
                "4 mm coplanar band of the bottom it is seen from under the hull and baked exterior.");
        }
    }
}
