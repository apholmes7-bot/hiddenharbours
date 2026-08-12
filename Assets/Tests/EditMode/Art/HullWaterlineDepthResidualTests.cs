using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>ONE DEPTH UNIT: THE HULL SITS IN THE SEA AT EVERY HEADING</b> (ADR 0033, implementing the
    /// diagnosis in #491 — owner report 2026-08-11: "the water still doesn't seem to interact with
    /// the stern correctly — it always feels like the boat is visually in front of the water when
    /// the boat is sailing north and you look at the stern").
    ///
    /// <para>ADR 0023 phase 3 makes the waterline free by putting hull and water in ONE calibrated
    /// iso-depth frame and letting the shared private z-buffer decide: a hull fragment below the
    /// lifted surface loses the z-test and the sea shows through. That was exact on ONE LINE only.
    /// The hull was translated onto the frame by a per-hull CONSTANT
    /// (<see cref="DisplacedWaterMath.HullDepthBias"/> of its ROOT world y) while its own vertices
    /// carried the rig's depth convention <c>ry·cos − rz·sin</c>, and the two disagreed about how
    /// fast depth advances with screen y because they measured ground y in different units:</para>
    ///
    /// <list type="bullet">
    /// <item>the HULL's ground y was RIG ground metres — one metre aft is <c>sin(elev)</c> of screen
    /// travel and <c>cos(elev)</c> of depth;</item>
    /// <item>the WATER's ground y is WORLD y, which for a flat water quad IS the screen y — one
    /// unit of screen travel is <c>cos(elev)</c> of depth.</item>
    /// </list>
    ///
    /// <para>So the hull's depth ramp ran <c>1/sin(elev)</c> = 1.556× too steep along its own
    /// fore-aft axis, a constant cancelled it at the root line alone, and everywhere else the two
    /// drifted apart by <c>Δy_rigGround·cos(elev)·(1 − sin(elev))</c> — 0.62 m of false depth at a
    /// dory's stern, 1.64 m at a lobster boat's, 3.42 m at a dragger's, NEGATIVE sailing north (the
    /// hull reads nearer than the sea that should lap it, so no wave can ever climb that planking),
    /// positive sailing south (the sea draws over a dry stern — verbatim the owner's 2026-07-25
    /// report), and exactly zero east/west. One mechanism, two reports ten months apart.</para>
    ///
    /// <para><b>What ADR 0033 ships, and what this suite now asserts.</b> A y→z shear of the hull
    /// frame, <c>z −= (worldY − referenceY)·g</c> with
    /// <c>g = cos(elev)(1 − sin(elev))/sin(elev)</c> = 0.42571 at the fleet's 40° bake
    /// (<see cref="DisplacedWaterMath.HullDepthShear"/>, applied by the facet shader's vertex stage
    /// and compensated at the root by <see cref="DisplacedWaterMath.HullShearCompensation"/>). It is
    /// EXACT: the ground term goes to zero at every facing and the height term lands on the true iso
    /// relation <c>−h/sin(elev)</c>, so the z-test asks only the question the composite exists to
    /// ask — <i>is this bit of hull above or below the water?</i></para>
    ///
    /// <para><b>These tests were GREEN while they stated the DEFECT</b> (#491) and are green now
    /// that they state the LAW; the numbers they drive come from the production functions, so
    /// neither reading can drift unnoticed. The sabotage structure is the diagnosis itself: remove
    /// the shear and <see cref="EveryFacing_TheGroundTermIsZero"/> goes red at N/S/NE/NW/SE/SW and
    /// stays GREEN at E/W, because the residual was always a pure function of how much of the hull's
    /// fore-aft offset lay along world y.</para>
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
        /// One hull vertex through the EXACT production path at the fleet's bake: the rig→world map
        /// (<see cref="IsoFacetMath.RigToWorld"/>, which is what <c>IsoFacetHullRenderer.ApplyPose</c>
        /// assigns as rotation × mirror scale), the per-hull constant z the same method applies from
        /// <see cref="DisplacedWaterMath.HullDepthBias"/> and
        /// <see cref="DisplacedWaterMath.HullShearCompensation"/>, and then the per-vertex shear the
        /// facet shader applies (<see cref="DisplacedWaterMath.ShearedDepth"/>, of which the HLSL
        /// line is a transcription).
        /// </summary>
        private static void HullVertex(Vector3 rigPointMeters, float dirUnits, float heaveMeters,
                                       out float screenY, out float depthZ) =>
            HullVertexAt(ElevationDeg, rigPointMeters, dirUnits, heaveMeters, out screenY, out depthZ);

        /// <summary>The same, at an arbitrary bake elevation — the rule-6 arm: nothing here may
        /// depend on the fleet's 40°.</summary>
        private static void HullVertexAt(float elevationDeg, Vector3 rigPointMeters, float dirUnits,
                                         float heaveMeters, out float screenY, out float depthZ)
        {
            float sin = Mathf.Sin(elevationDeg * Mathf.Deg2Rad);
            float cos = Mathf.Cos(elevationDeg * Mathf.Deg2Rad);
            var frame = new WaterIsoDepthFrame(ReferenceY, cos, sin, BaseZ);

            Matrix4x4 rigToWorld = IsoFacetMath.RigToWorld(dirUnits, elevationDeg);
            Vector3 offset = rigToWorld.MultiplyPoint3x4(rigPointMeters);
            screenY = RootWorldY + heaveMeters + offset.y;

            float shear = DisplacedWaterMath.HullDepthShear(elevationDeg);
            // Where ApplyPose puts this hull's mesh child, in world z.
            float childZ = DisplacedWaterMath.HullDepthBias(RootWorldY, heaveMeters, in frame)
                           + DisplacedWaterMath.HullShearCompensation(
                                 RootWorldY, heaveMeters, shear, in frame);
            depthZ = DisplacedWaterMath.ShearedDepth(screenY, childZ + offset.z, ReferenceY, shear);
        }

        /// <summary>
        /// The depth of the displaced sea sharing a screen row. The water's vertex stage is
        /// <c>ws.y += lift; ws.z += (ground.y − _HeightWorldMin.y)·cosElev − lift·sinElev</c>, so a
        /// water vertex DRAWN at <paramref name="screenY"/> had ground y of
        /// <c>screenY − lift</c> — and <see cref="DisplacedWaterMath.HullDepthBias"/> is documented
        /// as the C# reference of exactly that expression. Driving the SAME production function both
        /// sides is what makes the comparison a statement about shipped code rather than about a
        /// transcription of it. ⚠️ The water is NOT sheared: the shear exists to bring the hull into
        /// the water's unit, not to move the sea.
        /// </summary>
        private static float WaterDepthAtScreenY(float screenY, float lift) =>
            DisplacedWaterMath.HullDepthBias(screenY - lift, lift, Frame());

        private static float StillWaterDepthAtScreenY(float screenY) =>
            WaterDepthAtScreenY(screenY, 0f);

        /// <summary>The residual a hull point leaves against the sea sharing its pixel: negative =
        /// the hull reads NEARER than that water (it wins the z-test and stays dry), positive = the
        /// sea covers it. Zero is a point exactly at the waterline.</summary>
        private static float Residual(Vector3 rigPointMeters, float dirUnits,
                                      float heaveMeters = 0f, float lift = 0f)
        {
            HullVertex(rigPointMeters, dirUnits, heaveMeters, out float screenY, out float depthZ);
            return depthZ - WaterDepthAtScreenY(screenY, lift);
        }

        // Rig dir units: 1 = 45° CCW, and dir 0 draws the bow up-screen (rig +Y → world +Y).
        private const float DirNorth = 0f;
        private const float DirWest = 2f;
        private const float DirSouth = 4f;
        private const float DirEast = 6f;

        /// <summary>The residual the UNSHEARED frame left — <c>Δ·cos·(1−sin)</c> projected onto
        /// world y by the heading. Kept because it is the shape of the defect, and because the
        /// facings where it is nonzero are exactly the facings a removed shear must break.</summary>
        private static float LegacyResidual(float alongMeters, float dirUnits)
        {
            float alongWorldY = alongMeters * Mathf.Cos(dirUnits * 45f * Mathf.Deg2Rad);
            return alongWorldY * CosElev * (1f - SinElev);
        }

        [Test]
        public void TheShear_IsTheAdrsClosedForm()
        {
            // g = cos(elev)·(1 − sin(elev))/sin(elev), and the identity the whole re-derivation
            // turns on: g + cos = cot(elev). That is what collapses the ground term to nothing and
            // the height term to 1/sin — in the residual below, in the watertight clamp's per-point
            // law, and in HullSettleMath's flotation gain.
            float g = DisplacedWaterMath.HullDepthShear(ElevationDeg);
            Assert.AreEqual(CosElev * (1f - SinElev) / SinElev, g, 1e-6f);
            Assert.AreEqual(0.42571f, g, 1e-5f, "the ADR's g at the fleet's 40° bake");
            Assert.AreEqual(CosElev / SinElev, g + CosElev, 1e-6f,
                "g + cos must be cot(elev) — the identity every re-derivation in ADR 0033 rests on");

            // A degenerate bake earns no shear rather than a division by a vanishing sine.
            Assert.AreEqual(0f, DisplacedWaterMath.HullDepthShear(0f));
            Assert.AreEqual(0f, DisplacedWaterMath.HullDepthShear(-12f));
            Assert.AreEqual(0f, DisplacedWaterMath.HullDepthShear(91f));
            Assert.AreEqual(0f, DisplacedWaterMath.HullDepthShear(90f), 1e-6f,
                "a plan bake needs no shear: screen y and ground y already agree");
        }

        [Test]
        public void NoDisplacedSea_LeavesEveryDepthUntouched()
        {
            // The A/B contract. With no frame published ApplyPose applies neither bias nor shear,
            // and shear 0 makes ShearedDepth the identity — byte-identical, at any world y.
            foreach (float y in new[] { -400f, 0f, 12.5f, 1000f })
                Assert.AreEqual(37.25f, DisplacedWaterMath.ShearedDepth(y, 37.25f, ReferenceY, 0f),
                    "shear 0 must be bit-exactly the identity — no sea, no shear, no change");
        }

        [Test]
        public void AmidshipsLine_IsExact()
        {
            // The one line the constant calibration always landed, and it must survive the fix: a
            // point on her waterline plane amidships shares its pixel with sea at exactly its own
            // depth, at every heading.
            for (float dir = 0f; dir < 8f; dir += 1f)
                Assert.AreEqual(0f, Residual(Vector3.zero, dir), 1e-4f,
                    $"the hull's ROOT waterline point must sit exactly in the sea at dir {dir}");
        }

        [TestCase("Dory", 4.5f, TestName = "Stern sits in the sea: Dory (L 4.5 m)")]
        [TestCase("Punt", 5.2f, TestName = "Stern sits in the sea: Punt (L 5.2 m)")]
        [TestCase("SportSkiff", 7.0f, TestName = "Stern sits in the sea: Sport skiff (L 7.0 m)")]
        [TestCase("LobsterBoat", 12.0f, TestName = "Stern sits in the sea: Lobster boat (L 12.0 m)")]
        [TestCase("SideDragger", 25.0f, TestName = "Stern sits in the sea: Side dragger (L 25.0 m)")]
        public void SailingNorth_TheSternSitsExactlyInTheSea(string hull, float lengthMeters)
        {
            float halfLength = 0.5f * lengthMeters;
            // The stern's waterline point: rig −Y, on the hull's waterline plane (rig z = 0).
            float residual = Residual(new Vector3(0f, -halfLength, 0f), DirNorth);

            Assert.AreEqual(0f, residual, 1e-4f,
                $"{hull}: sailing north her stern must share its pixel with sea at exactly its own " +
                "depth — this is the owner's 2026-08-11 report, and it is the whole of ADR 0033");

            // And the defect it replaces, in the currency the owner sees: how far the stern used to
            // stand out of the water beyond where she floats (a depth advantage converts to height
            // at 1/sin(elev), the relation the shear now makes true).
            float wasApparentFreeboard = -LegacyResidual(-halfLength, DirNorth) * SinElev;
            Assert.Greater(wasApparentFreeboard, 0.3f,
                $"{hull}: the unsheared frame read her stern {wasApparentFreeboard:0.000} m higher " +
                "out of the sea than she floats — comparable to (or well past) the freeboard of " +
                "the whole fleet, which is why no wave could reach that planking");
        }

        [Test]
        public void SailingSouth_TheSeaNoLongerCoversTheDryStern()
        {
            // The owner's 2026-07-25 report ("when the bow faces south you see water at the stern"),
            // attributed then to the watertight clamp's half-BEAM reach and cured by the per-face
            // interior mask. The mask stops the sea drawing INSIDE a hull; it could not make a hull
            // that read too near sit back down in the water, which is why the north-facing half of
            // the same defect survived it. Both halves close here, with one sign.
            const float halfLength = 6.0f;   // lobster boat
            Assert.AreEqual(0f, Residual(new Vector3(0f, -halfLength, 0f), DirSouth), 1e-4f,
                "bow-south, the stern is up-screen and used to read FARTHER than the sea sharing " +
                "its pixel, so the sea drew over planking that is out of the water");

            Assert.AreEqual(-LegacyResidual(-halfLength, DirNorth),
                            LegacyResidual(-halfLength, DirSouth), 1e-4f,
                "north and south were always the same mechanism with the sign flipped — which is " +
                "why one shear closes both");
        }

        [Test]
        public void BeamOn_WasAlreadyRight_AndStaysRight()
        {
            // Why the owner reported this sailing NORTH and not east or west: at dir 2/6 the hull's
            // fore-aft axis lies along screen x, so her half-length bought no world-y offset and the
            // residual was already exactly 0. These two facings are the negative control of the
            // whole diagnosis — a shear that broke them would be curing the wrong thing.
            const float halfLength = 6.0f;
            foreach (float dir in new[] { DirWest, DirEast })
            {
                Assert.AreEqual(0f, LegacyResidual(-halfLength, dir), 1e-4f,
                    $"beam-on (dir {dir}) the stern shared the root's ground line and was exact");
                Assert.AreEqual(0f, Residual(new Vector3(0f, -halfLength, 0f), dir), 1e-4f,
                    $"beam-on (dir {dir}) it must STILL be exact");
            }
        }

        [Test]
        public void EveryFacing_TheGroundTermIsZero()
        {
            // THE 8-FACING SWEEP, and the suite's sabotage arm. Every point on the hull's waterline
            // PLANE — anywhere along the keel, anywhere across the beam — sits exactly in the sea,
            // at every heading. Remove the shear and this goes red at the six facings whose
            // fore-aft axis has a world-y component and stays GREEN at E/W: that sign structure IS
            // the diagnosis, so the test encodes it rather than merely asserting a number.
            foreach (float along in new[] { -12.5f, -6f, -1f, 0f, 1f, 6f, 12.5f })
            foreach (float across in new[] { -3.5f, 0f, 3.5f })
            for (float dir = 0f; dir < 8f; dir += 1f)
                Assert.AreEqual(0f, Residual(new Vector3(across, along, 0f), dir), 1e-4f,
                    $"dir {dir}, {along:+0.0;-0.0} m along the keel, {across:+0.0;-0.0} m abeam");
        }

        [Test]
        public void TheHeightAxis_ReadsAtOneOverSinElev()
        {
            // WHAT THE CORRECTED SEAM OWES, now shipped rather than proposed.
            //
            // In a true iso view the camera ray through a hull point h metres above the water meets
            // the sea h/tan(elev) FARTHER away, so the sea sharing that pixel is deeper by
            // h·(sin + cos²/sin) = h/sin(elev). Before the shear the ground axis contributed
            // cos·(1−sin) per metre where it should contribute NOTHING, and the height axis
            // contributed (sin + cos²) = 1.2296 where it should contribute 1/sin = 1.5557.
            const float heightMeters = 1f;
            float target = -heightMeters / SinElev;

            for (float dir = 0f; dir < 8f; dir += 1f)
                Assert.AreEqual(target, Residual(new Vector3(0f, 0f, heightMeters), dir), 1e-4f,
                    $"dir {dir}: a metre of freeboard must read exactly 1/sin(elev) nearer than the " +
                    "sea sharing its pixel — no heading term left in the z-test at all");

            // Linear in height, so the whole planking reads one law rather than one calibrated line.
            foreach (float h in new[] { 0.25f, 1f, 2.5f, 6f })
                Assert.AreEqual(-h / SinElev, Residual(new Vector3(0f, 0f, h), DirNorth), 1e-4f);

            Assert.AreNotEqual(target, -(SinElev + CosElev * CosElev) * heightMeters,
                "the pre-shear height reading and the true iso relation are genuinely different " +
                "numbers — if this ever passes, the projection has changed under us");
        }

        [Test]
        public void RidingTheSea_TheHeaveAndTheLiftCancelExactly()
        {
            // ⚠️ THE INVARIANT THAT DECIDES WHERE THE SHEAR IS REFERENCED FROM, and it is not
            // decorative. The heave/lift channel was ALREADY exact before ADR 0033: the hull's
            // −heave·sin and the water's −lift·sin cancel term for term when she floats on the sea
            // she is riding. So the shear must be referenced to the hull's DRAWN (heaved) root, as
            // HullShearCompensation is — reference the unheaved root instead and a residual of
            // −heave·g survives, which on a 1.4 m crest is 0.6 m of false depth: the very defect
            // ADR 0033 closes, rebuilt, this time modulated by the wave instead of the heading.
            //
            // A hull riding a crest of L metres with heave H = L must read her waterline point
            // exactly in the sea, at every heading and every crest height.
            foreach (float lift in new[] { -1.6f, -0.4f, 0f, 0.9f, 1.4f, 3.2f })
            for (float dir = 0f; dir < 8f; dir += 1f)
            {
                Assert.AreEqual(0f, Residual(Vector3.zero, dir, heaveMeters: lift, lift: lift), 1e-4f,
                    $"riding lift {lift} m at dir {dir}, her waterline must sit IN the sea");
                Assert.AreEqual(0f, Residual(new Vector3(0f, -6f, 0f), dir, lift, lift), 1e-4f,
                    $"riding lift {lift} m at dir {dir}, her STERN waterline too");
                Assert.AreEqual(-1f / SinElev,
                                Residual(new Vector3(0f, 0f, 1f), dir, lift, lift), 1e-4f,
                    $"riding lift {lift} m at dir {dir}, a metre of freeboard still reads 1/sin");
            }
        }

        [Test]
        public void SunkByHerDraft_TheSeaDrawsTheWaterlineTheDatumAsks()
        {
            // The flotation half of ADR 0033, from the depth side: sink the hull by S metres below
            // the sea she rides (H = L − S) and the sea covers her planking to exactly
            // S·sin·(cos+sin) — which is HullSettleMath's re-derived gain, solved here out of the
            // shipped depth functions rather than restated. (HullSettleMath inverts it, so what an
            // owner types into RestingDraftMeters is what the sea draws.)
            float gain = SinElev * (CosElev + SinElev);
            foreach (float sink in new[] { 0.11f, 0.5f, 1.1f, 2.47f })
            foreach (float lift in new[] { 0f, 1.2f })
            for (float dir = 0f; dir < 8f; dir += 1f)
            {
                float drawn = sink * gain;
                // Just BELOW the drawn waterline the sea covers her; just above, it cannot.
                Assert.Greater(Residual(new Vector3(0f, 0f, drawn - 0.01f), dir, lift - sink, lift), 0f,
                    $"sunk {sink} m at dir {dir}: planking below her drawn waterline must be covered");
                Assert.Less(Residual(new Vector3(0f, 0f, drawn + 0.01f), dir, lift - sink, lift), 0f,
                    $"sunk {sink} m at dir {dir}: planking above her drawn waterline must stay dry");
            }
        }

        [Test]
        public void TheLaw_ComesFromTheHullsOwnBakeElevation_NotTheFleets40()
        {
            // Rule 6: a hull re-baked at another elevation must be right for free. Nothing in the
            // shear, the compensation or the residual may know about 40°.
            foreach (float elev in new[] { 25f, 33f, 40f, 55f, 70f })
            {
                float sin = Mathf.Sin(elev * Mathf.Deg2Rad);
                var frame = new WaterIsoDepthFrame(ReferenceY, Mathf.Cos(elev * Mathf.Deg2Rad),
                                                   sin, BaseZ);
                for (float dir = 0f; dir < 8f; dir += 1f)
                {
                    HullVertexAt(elev, new Vector3(0f, -6f, 0f), dir, 0f,
                                 out float groundScreenY, out float groundZ);
                    Assert.AreEqual(0f,
                        groundZ - DisplacedWaterMath.HullDepthBias(groundScreenY, 0f, in frame),
                        1e-4f,
                        $"a {elev}° bake must put her stern waterline exactly in the sea at dir {dir}");

                    HullVertexAt(elev, new Vector3(0f, 0f, 1f), dir, 0f,
                                 out float highScreenY, out float highZ);
                    Assert.AreEqual(-1f / sin,
                        highZ - DisplacedWaterMath.HullDepthBias(highScreenY, 0f, in frame), 1e-4f,
                        $"a {elev}° bake must read a metre of freeboard at 1/sin({elev}°) at dir {dir}");
                }
            }
        }
    }
}
