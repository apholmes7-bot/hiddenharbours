using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>WHY A NORTH-SAILING HULL'S STERN SITS ON TOP OF THE SEA INSTEAD OF IN IT</b>
    /// (owner report 2026-08-11: "the water still doesn't seem to interact with the stern correctly
    /// — it always feels like the boat is visually in front of the water when the boat is sailing
    /// north and you look at the stern").
    ///
    /// <para>ADR 0023 phase 3 makes the waterline free by putting hull and water in ONE calibrated
    /// iso-depth frame and letting the shared private z-buffer decide: a hull fragment below the
    /// lifted surface loses the z-test and the sea shows through. That is exact — but only on ONE
    /// LINE. The hull is translated onto the frame by a per-hull CONSTANT
    /// (<see cref="DisplacedWaterMath.HullDepthBias"/> of its ROOT world y), while its own vertices
    /// carry the rig's depth convention <c>ry·cos − rz·sin</c>. Those two disagree about how fast
    /// depth advances with screen y, because they measure ground y in different units:</para>
    ///
    /// <list type="bullet">
    /// <item>the HULL's ground y is RIG ground metres — one metre aft is <c>sin(elev)</c> of screen
    /// travel and <c>cos(elev)</c> of depth;</item>
    /// <item>the WATER's ground y is WORLD y, which for a flat water quad IS the screen y — one
    /// unit of screen travel is <c>cos(elev)</c> of depth.</item>
    /// </list>
    ///
    /// <para>So the hull's depth ramp is <c>1/sin(elev)</c> = 1.556× too steep along its own
    /// fore-aft axis, and one constant can only cancel that at the hull's root line. Everywhere else
    /// the two drift apart by exactly</para>
    ///
    /// <code>residual = Δy_rigGround · cos(elev) · (1 − sin(elev))</code>
    ///
    /// <para>which is the SAME term the watertight clamp already carries per line as "§24's beam
    /// residual" (<c>ry·cos·(1−sin)</c> in <see cref="DisplacedWaterMath.WatertightZHeaveMeters"/>'s
    /// per-point law). The clamp only ever met it across the BEAM — a metre or so. Bow-on it is the
    /// half-LENGTH, and that is the whole defect: 0.62 m of false depth at a dory's stern, 1.64 m at
    /// a lobster boat's, 3.42 m at a dragger's.</para>
    ///
    /// <para><b>The sign is the owner's sentence.</b> Rig +Y is the bow. Sailing north the bow is
    /// up-screen, so the stern sits at <c>Δy_rigGround = −halfLength</c> and the residual is
    /// NEGATIVE — smaller z, and the camera looks along +Z, so the stern is NEARER than the water
    /// that should be lapping it. The hull wins the z-test at its own stern at every tide and every
    /// wave phase: the waterline can never climb that planking. Sailing SOUTH the sign flips and the
    /// sea covers the stern instead — which is verbatim the owner's 2026-07-25 defect ("when the bow
    /// faces south you see water at the stern"), blamed then on the watertight clamp's half-beam
    /// reach. One mechanism, two reports.</para>
    ///
    /// <para><b>These tests are GREEN and they state a DEFECT.</b> They measure the residual through
    /// the production maths so the number cannot drift unnoticed and so the fix has an oracle to
    /// flip. <see cref="AmidshipsLine_IsExact"/> pins the part that genuinely works (the root line),
    /// and <see cref="TargetLaw_WouldBeHeightOverSinElev"/> states what the corrected seam owes.
    /// Fixing it is an ADR-level change to the depth contract — the residual is woven into the
    /// clamp's per-point law and into <c>HullSettleMath</c>'s 1.1457 flotation gain — so the fix is
    /// proposed, not shipped, in the PR that added this file.</para>
    /// </summary>
    public class HullWaterlineDepthResidualTests
    {
        // The fleet's bake elevation — every iso boat rig's DEFAULT_ELEV, and the value every
        // shipped HullMeshDef carries in ElevationDeg.
        private const float ElevationDeg = 40f;
        private const float RootWorldY = 100f;   // arbitrary; the frame's terms cancel
        private const float ReferenceY = -20f;
        private const float BaseZ = 3f;

        private static readonly float SinElev = Mathf.Sin(ElevationDeg * Mathf.Deg2Rad);
        private static readonly float CosElev = Mathf.Cos(ElevationDeg * Mathf.Deg2Rad);

        private static WaterIsoDepthFrame Frame() =>
            new WaterIsoDepthFrame(ReferenceY, CosElev, SinElev, BaseZ);

        /// <summary>
        /// One hull vertex through the EXACT production path: the rig→world map
        /// (<see cref="IsoFacetMath.RigToWorld"/>, which is what
        /// <c>IsoFacetHullRenderer.ApplyPose</c> assigns as rotation × mirror scale) plus the
        /// per-hull constant z the same method applies from
        /// <see cref="DisplacedWaterMath.HullDepthBias"/>.
        /// </summary>
        private static void HullVertex(Vector3 rigPointMeters, float dirUnits, float heaveMeters,
                                       out float screenY, out float depthZ)
        {
            Matrix4x4 rigToWorld = IsoFacetMath.RigToWorld(dirUnits, ElevationDeg);
            Vector3 offset = rigToWorld.MultiplyPoint3x4(rigPointMeters);
            screenY = RootWorldY + heaveMeters + offset.y;
            depthZ = DisplacedWaterMath.HullDepthBias(RootWorldY, heaveMeters, Frame()) + offset.z;
        }

        /// <summary>
        /// The depth of the still sea sharing a screen row. The water's vertex stage is
        /// <c>ws.z += (ground.y − _HeightWorldMin.y)·cosElev − lift·sinElev</c>, and
        /// <see cref="DisplacedWaterMath.HullDepthBias"/> is documented as the C# reference of
        /// exactly that expression — so this drives the SAME production function the hull side does,
        /// which is what makes the comparison a statement about shipped code and not about a
        /// transcription of it. Still water (lift 0) puts a vertex's ground y at its screen y.
        /// </summary>
        private static float StillWaterDepthAtScreenY(float screenY) =>
            DisplacedWaterMath.HullDepthBias(screenY, 0f, Frame());

        // Rig dir units: 1 = 45° CCW, and dir 0 draws the bow up-screen (rig +Y → world +Y).
        private const float DirNorth = 0f;
        private const float DirWest = 2f;
        private const float DirSouth = 4f;
        private const float DirEast = 6f;

        /// <summary>
        /// The closed form: a point on the hull's waterline plane, <paramref name="alongMeters"/>
        /// forward of the root along the rig's fore-aft axis, drifts from the sea by
        /// <c>Δ·cos·(1−sin)</c> projected onto the world-y axis by the heading.
        /// </summary>
        private static float ExpectedResidual(float alongMeters, float dirUnits)
        {
            // The component of the rig fore-aft offset that lies along WORLD y at this heading.
            float alongWorldY = alongMeters * Mathf.Cos(dirUnits * 45f * Mathf.Deg2Rad);
            return alongWorldY * CosElev * (1f - SinElev);
        }

        [Test]
        public void AmidshipsLine_IsExact()
        {
            // The one line the constant calibration genuinely lands: the hull's own root. A point on
            // her waterline plane amidships shares its pixel with sea at exactly its own depth, at
            // every heading. This is the half of ADR 0023 phase 3 that works, and it must keep
            // working through any fix.
            for (float dir = 0f; dir < 8f; dir += 1f)
            {
                HullVertex(Vector3.zero, dir, 0f, out float screenY, out float depthZ);
                Assert.AreEqual(StillWaterDepthAtScreenY(screenY), depthZ, 1e-4f,
                    $"the hull's ROOT waterline point must sit exactly in the sea at dir {dir}");
            }
        }

        [TestCase("Dory", 4.5f, TestName = "Stern residual: Dory (L 4.5 m)")]
        [TestCase("Punt", 5.2f, TestName = "Stern residual: Punt (L 5.2 m)")]
        [TestCase("SportSkiff", 7.0f, TestName = "Stern residual: Sport skiff (L 7.0 m)")]
        [TestCase("LobsterBoat", 12.0f, TestName = "Stern residual: Lobster boat (L 12.0 m)")]
        [TestCase("SideDragger", 25.0f, TestName = "Stern residual: Side dragger (L 25.0 m)")]
        public void SailingNorth_TheSternReadsNearerThanTheSeaBesideIt(string hull, float lengthMeters)
        {
            float halfLength = 0.5f * lengthMeters;
            // The stern's waterline point: rig −Y, on the hull's waterline plane (rig z = 0).
            HullVertex(new Vector3(0f, -halfLength, 0f), DirNorth, 0f,
                       out float screenY, out float depthZ);
            float residual = depthZ - StillWaterDepthAtScreenY(screenY);

            Assert.AreEqual(ExpectedResidual(-halfLength, DirNorth), residual, 1e-4f,
                $"{hull}: the stern residual must follow Δ·cos·(1−sin)");
            Assert.Less(residual, 0f,
                $"{hull}: sailing north the stern reads NEARER than the sea sharing its pixel — the " +
                "hull wins the shared z-test at its own waterline, so no wave can ever climb that " +
                "planking (the owner's 2026-08-11 report)");

            // What the drift is worth in the currency the owner sees: the apparent extra freeboard.
            // A hull point's depth advantage converts to height at 1/sin(elev) (see the target law),
            // so this is how far the stern appears to stand out of the water beyond where it floats.
            float apparentFreeboardMeters = -residual * SinElev;
            Assert.Greater(apparentFreeboardMeters, 0.3f,
                $"{hull}: the stern reads {apparentFreeboardMeters:0.000} m higher out of the sea " +
                "than she floats — comparable to (or well past) the freeboard of the whole fleet");
        }

        [Test]
        public void SailingSouth_TheSignFlipsAndTheSeaCoversTheStern()
        {
            // The owner's 2026-07-25 report ("when the bow faces south you see water at the stern"),
            // which was attributed to the watertight clamp's half-BEAM reach and cured by the
            // per-face interior mask. The mask stops the sea drawing INSIDE a hull; it cannot make a
            // hull that reads too near sit back down in the water, which is why the north-facing
            // half of the same defect survived it.
            const float halfLength = 6.0f;   // lobster boat
            HullVertex(new Vector3(0f, -halfLength, 0f), DirSouth, 0f,
                       out float screenY, out float depthZ);
            float residual = depthZ - StillWaterDepthAtScreenY(screenY);

            Assert.Greater(residual, 0f,
                "bow-south, the stern is up-screen and reads FARTHER than the sea sharing its pixel " +
                "— the sea draws over planking that is out of the water");
            HullVertex(new Vector3(0f, -halfLength, 0f), DirNorth, 0f,
                       out float northScreenY, out float northDepthZ);
            float northResidual = northDepthZ - StillWaterDepthAtScreenY(northScreenY);
            Assert.AreEqual(-northResidual, residual, 1e-4f,
                "north and south are the same mechanism with the sign flipped");
        }

        [Test]
        public void BeamOn_TheForeAftDriftVanishes()
        {
            // Why the owner reports this sailing NORTH and not sailing east or west: at dir 2/6 the
            // hull's fore-aft axis lies along screen x, so her half-length buys no world-y offset and
            // the fore-aft residual is exactly 0. What is left abeam is the half-BEAM term the
            // watertight clamp already models — a quarter of a metre, not two and a half.
            const float halfLength = 6.0f;
            foreach (float dir in new[] { DirWest, DirEast })
            {
                HullVertex(new Vector3(0f, -halfLength, 0f), dir, 0f,
                           out float screenY, out float depthZ);
                float residual = depthZ - StillWaterDepthAtScreenY(screenY);
                Assert.AreEqual(0f, residual, 1e-4f,
                    $"beam-on (dir {dir}) the stern shares the root's ground line and must be exact");
            }
        }

        [Test]
        public void EveryFacing_FollowsOneClosedForm()
        {
            // The whole 8-facing sweep in one statement: the residual is a pure function of how much
            // of the hull's fore-aft offset lies along world y. Sabotage arm — change the rig
            // projection rows, the hull bias, or the water's depth expression and this fails.
            const float halfLength = 6.0f;
            for (float dir = 0f; dir < 8f; dir += 1f)
            {
                foreach (float along in new[] { -halfLength, halfLength })
                {
                    HullVertex(new Vector3(0f, along, 0f), dir, 0f,
                               out float screenY, out float depthZ);
                    float residual = depthZ - StillWaterDepthAtScreenY(screenY);
                    Assert.AreEqual(ExpectedResidual(along, dir), residual, 1e-4f,
                        $"dir {dir}, {along:+0.0;-0.0} m along the keel");
                }
            }
        }

        [Test]
        public void TargetLaw_WouldBeHeightOverSinElev()
        {
            // WHAT THE CORRECTED SEAM OWES, stated so the fix has an oracle.
            //
            // In a true iso view the camera ray through a hull point h metres above the water meets
            // the sea h/tan(elev) FARTHER away, so the sea sharing that pixel is deeper by
            // h·(sin + cos²/sin) = h/sin(elev). The z-test then reduces to the only question worth
            // asking — "is this bit of hull above or below the water?" — with no heading term at all.
            //
            // Today the ground axis contributes cos·(1−sin) per metre where it should contribute
            // NOTHING, and the height axis contributes (sin + cos²) = 1.2296 where it should
            // contribute 1/sin = 1.5557. Both are cured by one y→z shear of the hull frame,
            // z −= (worldY − rootY)·cos·(1−sin)/sin, which is invariant on any single pixel (two
            // fragments sharing a pixel share a world y and take the same shift) — so hull
            // self-occlusion, the deck-occupant bands and the golden master are untouched by it.
            const float heightMeters = 1f;
            HullVertex(new Vector3(0f, 0f, heightMeters), DirNorth, 0f,
                       out float screenY, out float depthZ);
            float residual = depthZ - StillWaterDepthAtScreenY(screenY);

            float today = -(SinElev + CosElev * CosElev) * heightMeters;
            float target = -heightMeters / SinElev;
            Assert.AreEqual(today, residual, 1e-4f,
                "the height axis reads at (sin + cos²) = 1.2296 per metre today");
            Assert.AreNotEqual(target, residual,
                "…where the true iso relation is 1/sin(elev) = 1.5557 per metre");

            // The shear that lands it, as the fix would apply it.
            float shear = CosElev * (1f - SinElev) / SinElev;
            float shearedGround = ResidualUnderShear(0f, 0f, shear);
            float shearedHeight = ResidualUnderShear(0f, heightMeters, shear);
            Assert.AreEqual(0f, ResidualUnderShear(-6.0f, 0f, shear), 1e-4f,
                "sheared, a waterline point anywhere along the keel sits exactly in the sea");
            Assert.AreEqual(0f, shearedGround, 1e-4f);
            Assert.AreEqual(target, shearedHeight, 1e-4f,
                "sheared, the height axis reads at exactly 1/sin(elev)");
        }

        /// <summary>The residual a proposed y→z shear of the hull frame would leave, sailing north.
        /// Lives here rather than in production code because the shear is a PROPOSAL — the PR that
        /// added this file does not ship it.</summary>
        private static float ResidualUnderShear(float alongMeters, float heightMeters, float shear)
        {
            HullVertex(new Vector3(0f, alongMeters, heightMeters), DirNorth, 0f,
                       out float screenY, out float depthZ);
            depthZ -= (screenY - RootWorldY) * shear;
            return depthZ - StillWaterDepthAtScreenY(screenY);
        }
    }
}
