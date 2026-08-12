using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>Does a scheme actually reach the renderer — and does NOT having one still draw today's boat?</b>
    ///
    /// <para>These are the two halves of the same contract and they fail in opposite directions. A
    /// repaint that does not apply is a dead feature; a repaint that applies when nobody asked for one
    /// silently recolours every lobster boat already in the game. So both are asserted, and the
    /// no-scheme case is asserted as EQUALITY against the def's own table rather than as "looks
    /// right".</para>
    ///
    /// <para><b>Where the seam is.</b> <c>IsoFacetHullPresentationService.ToSetup</c> is the one place
    /// <c>HullMeshDef.Ramps</c> becomes the renderer's ramp table, and the renderer builds a
    /// per-instance ramp texture and a per-instance material from it — which is why two boats off one
    /// mesh can wear two paints at one wharf without a second mesh, a second sheet or a second draw
    /// call. Testing at <c>ToSetup</c> tests the substitution itself, with no GPU in the way.</para>
    /// </summary>
    public class HullPaintSchemeApplicationTests
    {
        const string HullMeshAssetPath =
            "Assets/_Project/Data/Boats/HullMeshes/LobsterBoatIsoHullMesh.asset";

        HullMeshDef _hull;

        [SetUp]
        public void SetUp()
        {
            _hull = AssetDatabase.LoadAssetAtPath<HullMeshDef>(HullMeshAssetPath);
            Assert.IsNotNull(_hull, $"No hull mesh def at {HullMeshAssetPath}.");
        }

        static HullPaintSchemeDef[] Schemes() => AssetDatabase
            .FindAssets($"t:{nameof(HullPaintSchemeDef)}", new[] { HullPaintSchemeBaker.SchemeFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<HullPaintSchemeDef>)
            .Where(s => s != null)
            .OrderBy(s => s.Id)
            .ToArray();

        static HullPaintSchemeDef Scheme(string rigPaintId)
        {
            var s = Schemes().FirstOrDefault(x => x.RigPaintId == rigPaintId);
            Assert.IsNotNull(s, $"No baked scheme for the rig's '{rigPaintId}'. Bake the paint schemes.");
            return s;
        }

        static void AssertRampsEqual(IsoFacetHullSetup a, IsoFacetHullSetup b, string because)
        {
            Assert.AreEqual(a.Ramps.Length, b.Ramps.Length, $"{because} (material count)");
            CollectionAssert.AreEqual(a.RampOffsets, b.RampOffsets, $"{because} (offsets)");
            for (int m = 0; m < a.Ramps.Length; m++)
                CollectionAssert.AreEqual(a.Ramps[m], b.Ramps[m], $"{because} (material {m})");
        }

        // ---- "unset scheme = today's boat", the A/B contract -------------------------------------

        [Test]
        public void NoSchemeIsByteIdenticalToTheOneArgumentCall()
        {
            AssertRampsEqual(IsoFacetHullPresentationService.ToSetup(_hull),
                             IsoFacetHullPresentationService.ToSetup(_hull, null),
                             "passing a null scheme changed the ramp table");
        }

        [Test]
        public void NoSchemeDrawsTheDefsOwnRamps()
        {
            var setup = IsoFacetHullPresentationService.ToSetup(_hull, null);
            Assert.AreEqual(_hull.Ramps.Length, setup.Ramps.Length);
            for (int m = 0; m < _hull.Ramps.Length; m++)
            {
                Assert.AreEqual(_hull.Ramps[m].Offset, setup.RampOffsets[m], $"material {m}: offset");
                CollectionAssert.AreEqual(_hull.Ramps[m].Colors, setup.Ramps[m], $"material {m}: colours");
            }
        }

        /// <summary>The default scheme is a repaint that changes nothing — the control that proves the
        /// substitution machinery is not what makes a painted boat look different.</summary>
        [Test]
        public void TheDefaultSchemeRepaintsHerInHerOwnColours()
        {
            AssertRampsEqual(IsoFacetHullPresentationService.ToSetup(_hull, null),
                             IsoFacetHullPresentationService.ToSetup(_hull, Scheme("gelcoat")),
                             "the DEFAULT scheme changed her colours; it must be a no-op");
        }

        // ---- a scheme actually applies -----------------------------------------------------------

        /// <summary>⚠️ THE SABOTAGE TARGET. Break the substitution in <c>ToSetup</c> (make it always
        /// read <c>def.Ramps</c>) and this is the test that must go red.</summary>
        [Test]
        public void ASchemeReplacesTheRampTable()
        {
            var scheme = Scheme("harbour");
            var painted = IsoFacetHullPresentationService.ToSetup(_hull, scheme);

            Assert.AreEqual(scheme.Ramps.Length, painted.Ramps.Length);
            for (int m = 0; m < scheme.Ramps.Length; m++)
            {
                Assert.AreEqual(scheme.Ramps[m].Offset, painted.RampOffsets[m], $"material {m}: offset");
                CollectionAssert.AreEqual(scheme.Ramps[m].Colors, painted.Ramps[m],
                    $"material {m}: the scheme's colours did not reach the renderer setup.");
            }

            // …and it is genuinely a different boat, not the same table under another name.
            var plain = IsoFacetHullPresentationService.ToSetup(_hull, null);
            bool anyDifferent = Enumerable.Range(0, plain.Ramps.Length)
                .Any(m => !plain.Ramps[m].SequenceEqual(painted.Ramps[m]));
            Assert.IsTrue(anyDifferent,
                "HARBOUR NAVY produced the same ramp table as the unpainted hull. Either the " +
                "substitution is not happening or the scheme was baked from the default paint.");
        }

        [Test]
        public void EveryBakedSchemeExceptTheDefaultChangesHer()
        {
            var plain = IsoFacetHullPresentationService.ToSetup(_hull, null);
            foreach (var s in Schemes().Where(x => x.RigPaintId != "gelcoat"))
            {
                var painted = IsoFacetHullPresentationService.ToSetup(_hull, s);
                bool differs = Enumerable.Range(0, plain.Ramps.Length)
                    .Any(m => !plain.Ramps[m].SequenceEqual(painted.Ramps[m]));
                Assert.IsTrue(differs, $"'{s.Id}' draws exactly the unpainted hull.");
            }
        }

        /// <summary>Two schemes off one hull are two different tables — the property the moored fleet
        /// needs, stated directly rather than inferred from seven boats looking different.</summary>
        [Test]
        public void TwoSchemesOffOneHullAreTwoDifferentTables()
        {
            var a = IsoFacetHullPresentationService.ToSetup(_hull, Scheme("harbour"));
            var b = IsoFacetHullPresentationService.ToSetup(_hull, Scheme("ochre"));
            bool differs = Enumerable.Range(0, a.Ramps.Length)
                .Any(m => !a.Ramps[m].SequenceEqual(b.Ramps[m]));
            Assert.IsTrue(differs, "HARBOUR NAVY and OCHRE resolved to the same table.");

            // The mesh is shared — that is the whole economy of the design.
            Assert.AreSame(a.Mesh, b.Mesh, "Two schemes must share one mesh.");
        }

        /// <summary>Everything that is not colour comes from the def either way.</summary>
        [Test]
        public void ASchemeChangesNothingButColour()
        {
            var plain = IsoFacetHullPresentationService.ToSetup(_hull, null);
            var painted = IsoFacetHullPresentationService.ToSetup(_hull, Scheme("tarblack"));

            Assert.AreSame(plain.Mesh, painted.Mesh, "mesh");
            Assert.AreEqual(plain.LightN, painted.LightN, "light");
            Assert.AreEqual(plain.Gain, painted.Gain, "gain");
            Assert.AreEqual(plain.Bias, painted.Bias, "bias");
            CollectionAssert.AreEqual(plain.Bayer16, painted.Bayer16, "dither");
            Assert.AreEqual(plain.PivotPx, painted.PivotPx, "pivot");
            Assert.AreEqual(plain.PxPerMetre, painted.PxPerMetre, "ppu");
            Assert.AreEqual(plain.CellW, painted.CellW, "cell width");
            Assert.AreEqual(plain.CellH, painted.CellH, "cell height");
            Assert.AreEqual(plain.ElevationDeg, painted.ElevationDeg, "elevation");
            Assert.AreEqual(plain.WatertightDeckHeightMeters, painted.WatertightDeckHeightMeters, "deck");
            Assert.AreEqual(plain.WatertightHalfBeamMeters, painted.WatertightHalfBeamMeters, "half beam");
        }

        // ---- a bad scheme costs the paint, never the boat ---------------------------------------

        [Test]
        public void AMismatchedSchemeFallsBackToTheDefsOwnRamps()
        {
            var wrongArity = ScriptableObject.CreateInstance<HullPaintSchemeDef>();
            try
            {
                wrongArity.Id = "paint.test_short";
                wrongArity.HullMeshId = _hull.Id;
                wrongArity.Ramps = _hull.Ramps.Take(_hull.Ramps.Length - 1).ToArray();

                Assert.IsFalse(wrongArity.IsUsableFor(_hull), "a short table must be refused");
                StringAssert.Contains("match by index", wrongArity.ExplainUnusableFor(_hull));

                AssertRampsEqual(IsoFacetHullPresentationService.ToSetup(_hull, null),
                                 IsoFacetHullPresentationService.ToSetup(_hull, wrongArity),
                                 "a refused scheme must leave the hull in her own colours");
            }
            finally { Object.DestroyImmediate(wrongArity); }
        }

        [Test]
        public void ASchemeBakedForAnotherHullIsRefused()
        {
            var otherHull = ScriptableObject.CreateInstance<HullPaintSchemeDef>();
            try
            {
                otherHull.Id = "paint.test_foreign";
                otherHull.HullMeshId = "hullmesh.some_other_boat";
                otherHull.Ramps = Schemes().First(s => s.RigPaintId == "harbour").Ramps;

                Assert.IsFalse(otherHull.IsUsableFor(_hull),
                    "a table baked against another hull's material list must be refused even when " +
                    "the arity happens to match — the materials behind those indices are not the same.");

                AssertRampsEqual(IsoFacetHullPresentationService.ToSetup(_hull, null),
                                 IsoFacetHullPresentationService.ToSetup(_hull, otherHull),
                                 "a foreign scheme must leave the hull in her own colours");
            }
            finally { Object.DestroyImmediate(otherHull); }
        }
    }
}
