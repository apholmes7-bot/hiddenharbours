using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The shipped lamp positions still are the ones the rig draws</b> (ADR 0016).
    ///
    /// <para><b>What this exists to catch.</b> <see cref="HullMeshDef.Lamps"/> is hand-authored data:
    /// four boat-local triples measured out of the hull's own rig. Nothing in the mesh bake writes it
    /// and nothing at run time checks it, so a rig revision that moves a sidelight — or a typo in the
    /// def — would leave the lamps burning confidently in the wrong place, at every heading, with no
    /// error anywhere. This test is the join: it takes the numbers the game actually ships, pushes
    /// them through the RUNTIME's own projection, and demands they land on the pixels the RIG's own
    /// <c>navMounts(dir)</c> reports, at all eight facings.</para>
    ///
    /// <para><b>Why that is a real oracle and not two transcriptions agreeing.</b> The two sides share
    /// no code and no constants. The rig computes its answer in JavaScript from its own stations and
    /// its own <c>camBasis</c>/<c>projVert</c>; the game computes its answer in C# from
    /// <see cref="IsoFacetMath.RigToWorld"/>, the def's pivot and the def's pixels-per-metre. Three
    /// separate things therefore go red here: the def drifting, the rig moving its lamps, and the
    /// runtime's handedness or elevation convention changing under the data. A test that only pinned
    /// the def's numbers against a copy of themselves would catch the first and neither of the
    /// others.</para>
    /// </summary>
    public class BoatLampAnchorTests
    {
        const string CapeDefPath =
            "Assets/_Project/Data/Boats/HullMeshes/CapeIslanderIsoHullMesh.asset";

        /// <summary>Her key in the mesh fleet — the registry hull rigs actually live in.</summary>
        const string RigKey = "capeIslander";

        // The rig's own name for each nav lamp, beside the kind the game files it under. The cabin
        // glow and the searchlight are deliberately absent: the rig publishes no anchor for either
        // (they are placed against her published HOUSE box instead, which the wheelhouse test below
        // checks), so there is nothing here to agree with.
        static readonly (HullLampKind Kind, string RigName)[] NavPairs =
        {
            (HullLampKind.PortSidelight,      "port"),
            (HullLampKind.StarboardSidelight, "star"),
            (HullLampKind.SternLight,         "stern"),
            (HullLampKind.Masthead,           "mast"),
        };

        // Sub-pixel. The two paths are different languages doing the same double-precision arithmetic,
        // so they agree to floating noise; a tenth of a pixel is far tighter than any real drift and
        // far looser than the noise.
        const double TolerancePx = 0.1;

        /// <summary>
        /// Load the cape's rig into a fresh host and return the global it installs.
        ///
        /// <para><b>Off the MESH FLEET, not off RigCatalog.</b> The hull rigs are registered in
        /// <see cref="HullMeshFleet"/> — the catalog holds the prop and character kits and knows
        /// nothing about "capeIslander". Asking the wrong registry throws a very helpful exception
        /// listing forty rigs that are not hers, which reads like the rig is missing rather than like
        /// the question was addressed to the wrong desk.</para>
        /// </summary>
        static string LoadCapeRig(IRigScriptHost host)
        {
            FleetHull hull = HullMeshFleet.Get(RigKey);
            string full = Path.Combine(RigCatalog.RepoRoot, hull.ScriptPath);
            Assert.IsTrue(File.Exists(full), $"the cape's rig must be on disk at {hull.ScriptPath}");

            host.Execute(File.ReadAllText(full));

            string g = hull.GlobalName;
            Assert.IsTrue(host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"),
                          $"the cape's rig must install globalThis.{g}");
            return g;
        }

        static HullMeshDef Cape()
        {
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(CapeDefPath);
            Assert.IsNotNull(def, $"the cape's hull-mesh def must load from {CapeDefPath}");
            return def;
        }

        static HullLamp LampOf(HullMeshDef def, HullLampKind kind)
        {
            foreach (HullLamp l in def.Lamps)
                if (l.Kind == kind) return l;
            Assert.Fail($"the cape declares no {kind} lamp");
            return default;
        }

        // ---- the lamps she declares ------------------------------------------------------------------

        [Test]
        public void TheCapeDeclaresHerFourNavLampsHerCabinAndHerSearchlight()
        {
            HullMeshDef def = Cape();
            var kinds = new List<HullLampKind>();
            foreach (HullLamp l in def.Lamps) kinds.Add(l.Kind);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    HullLampKind.PortSidelight, HullLampKind.StarboardSidelight,
                    HullLampKind.SternLight, HullLampKind.Masthead,
                    HullLampKind.CabinGlow, HullLampKind.Spotlight,
                },
                kinds,
                "the cape is the hull the intro's arrival is run on, and the owner's ruling names all " +
                "three of cabin light, navigation lights and spotlight — so all six declarations have " +
                "to be on her def or the demo is short one of the things it promises");

            // A duplicate would build two lights at one point and read as one brighter lamp — quiet,
            // and exactly the kind of thing a table edited by hand grows.
            CollectionAssert.AllItemsAreUnique(kinds, "one lamp of each kind, not two");
        }

        [Test]
        public void HerSidelightsAreOnOppositeSidesAndAreNotTheSameLamp()
        {
            HullMeshDef def = Cape();
            HullLamp port = LampOf(def, HullLampKind.PortSidelight);
            HullLamp star = LampOf(def, HullLampKind.StarboardSidelight);

            // +x is starboard in this frame, so the signs are the whole claim: get them the wrong way
            // round and the boat shows red to starboard, which is the one mistake in this feature that
            // could actually mislead somebody about which way she is heading.
            Assert.Less(port.RigLocalMetres.x, 0f, "the PORT sidelight sits to port (negative x)");
            Assert.Greater(star.RigLocalMetres.x, 0f, "the STARBOARD sidelight sits to starboard");
            Assert.AreEqual(port.RigLocalMetres.y, star.RigLocalMetres.y, 1e-4f,
                            "the pair sits at the same station — they are one fitting on two sides");
        }

        // ---- THE PROOF ---------------------------------------------------------------------------------

        [Test]
        public void HerNavLampsLandExactlyWhereHerRigDrawsThem_AtEveryFacing()
        {
            HullMeshDef def = Cape();

            using var host = new V8RigScriptHost();
            string g = LoadCapeRig(host);

            Assert.IsTrue(host.EvaluateBool($"typeof {g}.navMounts === 'function'"),
                          $"{g} must still publish navMounts(dir) — it is the only statement the rig " +
                          "makes about where her lamps are, and this whole test is the join to it. If " +
                          "a revision drops it, the def's numbers are unmoored and somebody has to " +
                          "re-measure them rather than delete this test.");

            // The rig and the game must also still agree about the CELL these pixels are in. Read from
            // the def, because the def is what the runtime actually draws with; a pivot that had drifted
            // would move every lamp together and could otherwise hide inside the comparison below.
            Assert.AreEqual(host.EvaluateNumber($"{g}.PX"), def.PxPerMetre, 1e-9,
                            "the def's pixels-per-metre is the rig's own");
            Assert.AreEqual(host.EvaluateNumber($"{g}.pivot.x"), def.PivotPx.x, 1e-6,
                            "the def's pivot x is the rig's own");
            Assert.AreEqual(host.EvaluateNumber($"{g}.pivot.y"), def.PivotPx.y, 1e-6,
                            "the def's pivot y is the rig's own");

            int facings = (int)host.EvaluateNumber($"{g}.DIRS");
            Assert.AreEqual(8, facings, "the cape is an eight-facing rig");

            double worst = 0;
            string worstWhere = "";

            for (int d = 0; d < facings; d++)
            {
                // The runtime's rig-to-world map for this facing. dirUnits is the rig's own dir
                // argument (1 unit = 45 degrees), which is exactly what navMounts is handed below, so
                // the two sides are asked about the same heading rather than about two conventions
                // that happen to line up at north.
                Matrix4x4 m = IsoFacetMath.RigToWorld(d, def.ElevationDeg);

                foreach ((HullLampKind kind, string rigName) in NavPairs)
                {
                    HullLamp lamp = LampOf(def, kind);

                    // Rig metres -> world (screen x/y up, z depth) -> the rig's own cell pixels, whose
                    // origin is the cell's TOP-LEFT and whose y runs DOWN. That flip is the only
                    // convention this test asserts on its own behalf, and getting it wrong would show
                    // up as a mirrored error at every facing rather than as agreement.
                    Vector3 w = m.MultiplyPoint3x4(lamp.RigLocalMetres);
                    double px = def.PivotPx.x + w.x * def.PxPerMetre;
                    double py = def.PivotPx.y - w.y * def.PxPerMetre;

                    double rx = host.EvaluateNumber($"{g}.navMounts({d}).{rigName}.x");
                    double ry = host.EvaluateNumber($"{g}.navMounts({d}).{rigName}.y");

                    double err = Mathf.Max(Mathf.Abs((float)(px - rx)), Mathf.Abs((float)(py - ry)));
                    if (err > worst) { worst = err; worstWhere = $"{kind} at facing {d}"; }

                    Assert.AreEqual(rx, px, TolerancePx,
                        $"{kind} at facing {d}: the def puts her at cell x {px:F4}, her rig draws " +
                        $"her at {rx:F4}. Either the def's boat-local triple has drifted or the rig " +
                        "has moved the lamp — re-measure from the rig, do not nudge the def until " +
                        "the numbers meet.");
                    Assert.AreEqual(ry, py, TolerancePx,
                        $"{kind} at facing {d}: the def puts her at cell y {py:F4}, her rig draws " +
                        $"her at {ry:F4}. See the x message.");
                }
            }

            Debug.Log($"[boat-lamps] the cape's four nav lamps agree with her rig at all {facings} " +
                      $"facings; worst disagreement {worst:E3} px ({worstWhere}).");
        }

        // ---- the two lamps the rig publishes no anchor for ---------------------------------------------

        [Test]
        public void HerCabinGlowSitsInsideHerWheelhouse()
        {
            HullMeshDef def = Cape();
            HullLamp cabin = LampOf(def, HullLampKind.CabinGlow);

            using var host = new V8RigScriptHost();
            string g = LoadCapeRig(host);

            // Her rig publishes the house as a box, which is the only thing a cabin glow has to
            // respect: a lamp outside it is a lamp glowing through a deck, and no measurement of the
            // room's centre can be checked any other way.
            double yAft = host.EvaluateNumber($"{g}.HOUSE.yAft");
            double yFwd = host.EvaluateNumber($"{g}.HOUSE.yFwd");
            double soleZ = host.EvaluateNumber($"{g}.HOUSE.soleZ");
            double roofZ = host.EvaluateNumber($"{g}.HOUSE.roofZ");
            double halfBeam = host.EvaluateNumber($"{g}.HOUSE.hxAft");

            Vector3 p = cabin.RigLocalMetres;
            Assert.GreaterOrEqual(p.y, yAft, "the cabin glow is not aft of her wheelhouse");
            Assert.LessOrEqual(p.y, yFwd, "the cabin glow is not forward of her wheelhouse");
            Assert.GreaterOrEqual(p.z, soleZ, "the cabin glow is not below her wheelhouse sole");
            Assert.LessOrEqual(p.z, roofZ, "the cabin glow is not above her wheelhouse roof");
            Assert.LessOrEqual(Mathf.Abs(p.x), (float)halfBeam,
                               "the cabin glow is inside her wheelhouse walls");
        }

        [Test]
        public void HerSearchlightIsMountedForwardOfHerOriginAndOnTheCentreline()
        {
            HullLamp beam = LampOf(Cape(), HullLampKind.Spotlight);

            // The beam is thrown along the bow, from this point. Behind the origin it would light the
            // water she has already passed over, which is the one placement that makes the feature
            // useless rather than merely wrong.
            Assert.Greater(beam.RigLocalMetres.y, 0f,
                           "her searchlight is mounted forward of amidships, so the cone throws ahead");
            Assert.AreEqual(0f, beam.RigLocalMetres.x, 1e-4f,
                            "and on the centreline, like the lamp on her wheelhouse roof");
        }
    }
}
