using System.IO;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE COUPLING (PR 3b)</b> — capture, the fold limit, and how a towed body follows.
    ///
    /// <para><b>Its subject is that none of it is tuned.</b> The pack draws a slot with a throat and
    /// a depth, a trailer with a nose that swings a known distance, and a kingpin a known way ahead
    /// of an axle group — and every number the coupling uses falls out of those. So most of what is
    /// asserted here is not "the answer is 8.53°" but "the answer MOVES when the art moves", which
    /// is the property that stops this becoming a table of magic numbers a year from now.</para>
    /// </summary>
    public class VehicleCouplingTests
    {
        const string AeroMesh = "Assets/_Project/Data/Vehicles/Meshes/AeroSemiVehicleMesh.asset";
        const string ClassicMesh = "Assets/_Project/Data/Vehicles/Meshes/ClassicSemiVehicleMesh.asset";
        const string Pup = "Assets/_Project/Data/Vehicles/Meshes/TrailerReefer28VehicleMesh.asset";
        const string Long = "Assets/_Project/Data/Vehicles/Meshes/TrailerReefer53VehicleMesh.asset";
        const string Box = "Assets/_Project/Data/Vehicles/Meshes/CaboverBoxVehicleMesh.asset";

        static VehicleMeshDef Load(string path)
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(path);
            Assert.That(def, Is.Not.Null, $"{path} did not load — re-run the vehicle bake.");
            return def;
        }

        // =============================================================================================
        //  1. WHO COUPLES — read off the art, never off the kind
        // =============================================================================================

        [Test]
        public void OnlyTheSemisTow_AndOnlyTheTrailersAreTowed()
        {
            Assert.That(Load(AeroMesh).CanTow, Is.True);
            Assert.That(Load(ClassicMesh).CanTow, Is.True);
            Assert.That(Load(Pup).IsTowable, Is.True);
            Assert.That(Load(Long).IsTowable, Is.True);

            // ⚠️ Both directions on a machine that does neither. An unpublished plate must read as
            // "she does not tow" rather than as a plate at her own origin — which is what a zeroed
            // struct would be, and it would capture any trailer standing on top of her.
            VehicleMeshDef box = Load(Box);
            Assert.That(box.CanTow, Is.False, "a box truck grew a fifth wheel.");
            Assert.That(box.IsTowable, Is.False);
            Assert.That(VehicleCouplingMath.IsCaptured(box.FifthWheel, Load(Pup).Kingpin,
                                                       Vector2.zero, 0f), Is.False,
                "an unpublished plate captured a kingpin sitting exactly on it. Published must be " +
                "checked before the geometry is read.");
        }

        // =============================================================================================
        //  2. THE CAPTURE WINDOW IS THE SLOT
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The heading tolerance is the slot's own aspect</b> — <c>atan(halfWidth / reach)</c>,
        /// which is 8.53° on both semis because the art drew both slots 0.06 × 0.40.
        ///
        /// <para>Asserted as a DERIVATION rather than as a number: widen the throat and it opens,
        /// deepen the slot and it tightens. That is the difference between a measurement and a magic
        /// constant, and it is why there is no tolerance in config for anybody to nudge.</para>
        /// </summary>
        [Test]
        public void TheHeadingToleranceIsTheSlotsOwnAspect()
        {
            VehicleFifthWheel aero = Load(AeroMesh).FifthWheel;
            Assert.That(VehicleCouplingMath.CaptureHeadingToleranceDegrees(aero),
                Is.EqualTo(8.53f).Within(0.01f));
            Assert.That(VehicleCouplingMath.CaptureHeadingToleranceDegrees(Load(ClassicMesh).FifthWheel),
                Is.EqualTo(8.53f).Within(0.01f),
                "the two semis' slots are the same shape, so their tolerances must agree.");

            VehicleFifthWheel wider = aero; wider.SlotHalfWidthMeters *= 2f;
            Assert.That(VehicleCouplingMath.CaptureHeadingToleranceDegrees(wider),
                Is.GreaterThan(VehicleCouplingMath.CaptureHeadingToleranceDegrees(aero)),
                "a wider throat did not admit a wider approach — the tolerance is not derived from " +
                "the slot at all.");

            VehicleFifthWheel deeper = aero; deeper.SlotMouthY = aero.SlotSeatY - 0.80f;
            Assert.That(VehicleCouplingMath.CaptureHeadingToleranceDegrees(deeper),
                Is.LessThan(VehicleCouplingMath.CaptureHeadingToleranceDegrees(aero)),
                "a deeper slot did not tighten the approach.");
        }

        /// <summary>Capture begins at the RAMP, not at the slot — the ramps angle down aft and a pin
        /// backed onto them rides up into the throat, which is the manoeuvre the art drew.</summary>
        [Test]
        public void CaptureRunsFromTheRampMouthToTheSeat()
        {
            VehicleFifthWheel w = Load(AeroMesh).FifthWheel;
            VehicleKingpin k = Load(Pup).Kingpin;

            Assert.That(VehicleCouplingMath.IsCaptured(w, k, new Vector2(0f, w.SlotSeatY), 0f), Is.True,
                "a pin exactly on the seat is not captured.");
            Assert.That(VehicleCouplingMath.IsCaptured(w, k, new Vector2(0f, w.RampMouthY), 0f), Is.True,
                "a pin at the aft end of the ramps is not captured — that is where backing under " +
                "her begins.");

            Assert.That(VehicleCouplingMath.IsCaptured(w, k, new Vector2(0f, w.RampMouthY - 0.30f), 0f),
                Is.False, "a pin astern of the ramps was captured from thin air.");
            Assert.That(VehicleCouplingMath.IsCaptured(w, k, new Vector2(0f, w.SlotSeatY + 0.30f), 0f),
                Is.False, "a pin forward of the seat — inside the tractor — was captured.");
        }

        /// <summary>
        /// ⭐ <b>A SEATED PIN IS CAPTURED ON EVERY TRACTOR</b>, and stays captured after the round-trip
        /// through world space that the game actually makes.
        ///
        /// <para><b>This is the arm the assertions above could not make.</b> They ask the question in
        /// the tractor's frame, where <c>w.SlotSeatY</c> is the literal float the window was built
        /// from — so "on the seat" compares equal to itself and the test passes whatever the window's
        /// inclusivity is. The game asks it the other way round: a trailer's pin is a world point,
        /// pushed through <c>TransformPoint</c> and back, and it lands a few hundred nanometres off.
        /// On the aero it landed inside; on the classic — whose slot seats 100 mm further forward —
        /// it landed outside, and a squarely backed truck refused to couple.</para>
        ///
        /// <para>So this walks the seat by ±1 µm on BOTH published tractors: a difference no player
        /// could produce and no renderer could show, which must not decide whether the pin is in
        /// the slot.</para>
        /// </summary>
        [Test]
        public void ASeatedPinIsCapturedOnBothTractors_EvenAMicronOffTheSeat()
        {
            foreach (string tractor in new[] { AeroMesh, ClassicMesh })
            foreach (string body in new[] { Pup, Long })
            {
                VehicleFifthWheel w = Load(tractor).FifthWheel;
                VehicleKingpin k = Load(body).Kingpin;

                foreach (float nudge in new[] { -1e-6f, 0f, 1e-6f })
                    Assert.That(
                        VehicleCouplingMath.IsCaptured(w, k, new Vector2(0f, w.SlotSeatY + nudge), 0f),
                        Is.True,
                        $"{tractor} + {body}: a pin {nudge * 1e6f:0} µm off the seat was not " +
                        "captured — that is a coupling decided by float noise.");
            }
        }

        /// <summary>
        /// ⭐ <b>The margin is DERIVED, and it has clear air on both sides</b> — the discipline that
        /// separates "the pin has a size" from "I added an epsilon until it passed".
        ///
        /// <para>Below it: the round-trip error it has to absorb, ~1e-7 m at yard coordinates.
        /// Above it: the slot's own depth, which it must not swallow. Four orders of magnitude of
        /// clear air one way and better than an order the other. If a re-stamp ever shrank the slot
        /// or grew the pin until those closed, this goes red before the capture starts lying.</para>
        /// </summary>
        [Test]
        public void ThePinRadiusHasClearAirBothWays()
        {
            VehicleFifthWheel w = Load(AeroMesh).FifthWheel;
            VehicleKingpin k = Load(Pup).Kingpin;

            Assert.That(k.PinRadiusMeters, Is.GreaterThan(0f),
                "the kingpin baked no radius — the capture test has fallen back to a point.");

            Assert.That(k.PinRadiusMeters, Is.GreaterThan(1e-4f),
                "the pin's radius is down in the noise a world round-trip makes — it can no longer " +
                "keep a seated pin captured, which is the job it is here for.");

            float depth = Mathf.Abs(w.SlotSeatY - w.RampMouthY);
            Assert.That(k.PinRadiusMeters, Is.LessThan(0.25f * depth),
                $"the pin's radius ({k.PinRadiusMeters:0.###} m) is no longer small against the " +
                $"reach it widens ({depth:0.###} m) — the window has stopped being the art's.");
        }

        [Test]
        public void CaptureRefusesAPinOutsideTheThroatOrOffTheHeading()
        {
            VehicleFifthWheel w = Load(AeroMesh).FifthWheel;
            VehicleKingpin k = Load(Pup).Kingpin;
            float mid = 0.5f * (w.SlotMouthY + w.SlotSeatY);

            Assert.That(VehicleCouplingMath.IsCaptured(w, k, new Vector2(0f, mid), 0f), Is.True);

            // Wider than the throat AND wider than the pin that rides in it: metal on the plate,
            // not metal in the slot.
            float clear = w.SlotHalfWidthMeters + k.PinRadiusMeters + 0.02f;
            Assert.That(VehicleCouplingMath.IsCaptured(w, k, new Vector2(clear, mid), 0f), Is.False,
                "a pin wider than the throat was captured — it would be sitting on the plate, not " +
                "in the slot.");

            float tol = VehicleCouplingMath.CaptureHeadingToleranceDegrees(w);
            Assert.That(VehicleCouplingMath.IsCaptured(w, k, new Vector2(0f, mid), tol - 0.1f), Is.True);
            Assert.That(VehicleCouplingMath.IsCaptured(w, k, new Vector2(0f, mid), tol + 0.5f), Is.False,
                "a trailer sat across the yard was hooked by standing near her.");
            Assert.That(VehicleCouplingMath.IsCaptured(w, k, new Vector2(0f, mid), -(tol + 0.5f)), Is.False,
                "the heading test is one-sided — it must refuse a skew either way.");
        }

        // =============================================================================================
        //  3. THE FOLD LIMIT SOLVES ITSELF
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Full jackknife clears this pack — by 4 mm — so the cap is the kinematic 90°.</b> But
        /// it is 90 BECAUSE the geometry says so, and the same call tightens on a pair that does not
        /// clear. Her sidecar is blunt about the stakes: <i>"the 0.90 m set and the 2.44 m width are
        /// LOAD-BEARING numbers: widen either and the tractors' published margin is gone."</i>
        /// </summary>
        [Test]
        public void TheJackknifeCapIsSolvedFromTheSwingAndTheClearance()
        {
            VehicleFifthWheel w = Load(AeroMesh).FifthWheel;
            VehicleKingpin pin = Load(Long).Kingpin;

            // The two numbers the cap is solved from must reproduce the radius the sidecar states
            // separately - her own swing_basis, "sqrt(1.22^2 + 0.90^2)". If a re-stamp ever moved one
            // without the other this is where it shows, because everything below is built on them.
            float rebuilt = Mathf.Sqrt(pin.NoseHalfWidthMeters * pin.NoseHalfWidthMeters +
                                       pin.KingpinSetMeters * pin.KingpinSetMeters);
            Assert.That(rebuilt, Is.EqualTo(pin.NoseSwingRadiusMeters).Within(1e-3f),
                "half her width and her kingpin set no longer rebuild her published nose swing.");

            Assert.That(pin.NoseSwingRadiusMeters, Is.LessThan(w.CabClearanceMeters),
                "her nose now swings further than the cab clearance - the published 4 mm margin is " +
                "gone and the cap below is no longer the kinematic one.");
            Assert.That(VehicleCouplingMath.JackknifeCapDegrees(pin, w),
                Is.EqualTo(90f).Within(0.01f));

            // ⭐ THE DIRECTION, which is the whole reason this test exists. A trailer whose corner
            // does reach the cab folds LESS the bigger she gets - and the first version of this
            // arithmetic had it backwards, allowing a wider trailer to fold FURTHER. Widening her is
            // the sidecar's own stated failure mode ("widen either and the margin is gone"), so that
            // is the axis walked here.
            float atStock = VehicleCouplingMath.JackknifeCapDegrees(
                pin.NoseHalfWidthMeters, pin.KingpinSetMeters, w.CabClearanceMeters);
            float wider = VehicleCouplingMath.JackknifeCapDegrees(
                1.30f, pin.KingpinSetMeters, w.CabClearanceMeters);
            float widest = VehicleCouplingMath.JackknifeCapDegrees(
                1.45f, pin.KingpinSetMeters, w.CabClearanceMeters);

            Assert.That(atStock, Is.EqualTo(90f).Within(0.01f),
                "the stock body clears at every angle, so she is capped kinematically.");
            Assert.That(wider, Is.LessThan(90f).And.GreaterThan(0f),
                "a trailer whose corner genuinely reaches the cab was still allowed a full fold.");
            Assert.That(widest, Is.LessThan(wider),
                "a wider trailer did not tighten the cap further - the cap is running the wrong way.");

            // And it is the corner's real path, not a radius: contact begins where the corner ARRIVES
            // at the cab, at alpha - acos(d/r), which is strictly inside the corner's own bearing.
            float alpha = Mathf.Atan2(1.30f, pin.KingpinSetMeters) * Mathf.Rad2Deg;
            Assert.That(wider, Is.LessThan(alpha),
                "the cap reached past the bearing the corner sits at, which no fold can do.");
        }

        [Test]
        public void ArticulationIsHeldAtTheCapRatherThanRefused()
        {
            Assert.That(VehicleCouplingMath.ClampArticulation(120f, 90f), Is.EqualTo(90f).Within(1e-3f));
            Assert.That(VehicleCouplingMath.ClampArticulation(-120f, 90f), Is.EqualTo(-90f).Within(1e-3f),
                "the cap must hold a fold in either direction.");
            Assert.That(VehicleCouplingMath.ClampArticulation(30f, 90f), Is.EqualTo(30f).Within(1e-3f),
                "a fold inside the cap was clamped anyway.");
        }

        // =============================================================================================
        //  4. THE FOLLOW — where off-tracking comes from
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>A 53 off-tracks more than a pup, and it is one line of arithmetic rather than a
        /// special case.</b>
        ///
        /// <para>The trailer straightens at <c>v·sin(φ)/L</c>, so for the same metre travelled at the
        /// same fold, the 13.275 m body swings less than half as far as the 6.265 m one — she stays
        /// folded longer, cuts the corner further and sweeps wider. That ratio IS the difference the
        /// PlayMode journey is asked to show, and it comes out of the two lengths her own sidecar
        /// publishes.</para>
        /// </summary>
        [Test]
        public void ALongTrailerStraightensSlowerThanAPup_InProportionToHerLength()
        {
            float pupL = Load(Pup).Kingpin.KingpinToAxleCentreMeters;
            float longL = Load(Long).Kingpin.KingpinToAxleCentreMeters;
            Assert.That(pupL, Is.EqualTo(6.265f).Within(1e-3f));
            Assert.That(longL, Is.EqualTo(13.275f).Within(1e-3f));

            const float fold = 20f, travel = 1f;
            float pupYaw = VehicleCouplingMath.TrailerYawDeltaDegrees(fold, travel, pupL);
            float longYaw = VehicleCouplingMath.TrailerYawDeltaDegrees(fold, travel, longL);

            Assert.That(pupYaw, Is.GreaterThan(0f), "the pup did not straighten toward the tractor.");
            Assert.That(longYaw / pupYaw, Is.EqualTo(pupL / longL).Within(1e-3f),
                "the two no longer straighten in inverse proportion to their lengths — the follow " +
                "has stopped being v·sin(phi)/L and the off-tracking is now something invented.");
        }

        /// <summary>⚠️ <b>Backing folds her the other way</b>, and that is the whole difference between
        /// pulling a trailer and reversing one. The distance is signed because the odometer is.</summary>
        [Test]
        public void BackingFoldsHerTheOtherWay()
        {
            float L = Load(Pup).Kingpin.KingpinToAxleCentreMeters;
            float ahead = VehicleCouplingMath.TrailerYawDeltaDegrees(15f, +1f, L);
            float astern = VehicleCouplingMath.TrailerYawDeltaDegrees(15f, -1f, L);

            Assert.That(ahead, Is.GreaterThan(0f));
            Assert.That(astern, Is.EqualTo(-ahead).Within(1e-4f),
                "reversing did not reverse the fold. Driving forward straightens a trailer out; " +
                "backing bends her further, which is why backing one is hard.");
        }

        /// <summary>A straight pull does not bend her: zero fold, zero yaw, however far she goes.</summary>
        [Test]
        public void AStraightPullLeavesHerStraight()
        {
            float L = Load(Long).Kingpin.KingpinToAxleCentreMeters;
            Assert.That(VehicleCouplingMath.TrailerYawDeltaDegrees(0f, 25f, L),
                Is.EqualTo(0f).Within(1e-5f));
        }

        /// <summary>Her origin is placed from her PIN, which sits metres ahead of it — 3.365 on a pup,
        /// 7.175 on a 53. Drawing her at the pin would put a whole nose of trailer inside the
        /// tractor.</summary>
        [Test]
        public void HerOriginIsSetBackFromTheKingpin()
        {
            VehicleKingpin pin = Load(Long).Kingpin;
            var atPin = new Vector2(10f, 4f);

            Vector2 facingNorth = VehicleCouplingMath.BodyOriginFromKingpin(atPin, 0f, pin);
            Assert.That(Vector2.Distance(facingNorth, atPin),
                Is.EqualTo(pin.CouplingPointLocal.y).Within(1e-3f),
                "her origin is not her kingpin's own distance behind the pin.");

            // …and it swings with her, rather than being a fixed offset in world axes.
            Vector2 facingEast = VehicleCouplingMath.BodyOriginFromKingpin(atPin, 90f, pin);
            Assert.That(Vector2.Distance(facingEast, facingNorth), Is.GreaterThan(1f),
                "turning her did not move her origin — the set-back is being applied in world axes " +
                "rather than in hers.");
            Assert.That(Vector2.Distance(facingEast, atPin),
                Is.EqualTo(pin.CouplingPointLocal.y).Within(1e-3f));

            // ⭐ AND IN WHICH DIRECTION — a distance cannot see a mirror. Her origin is BEHIND her pin:
            // south of it facing north, WEST of it facing east. The counter-clockwise arithmetic this
            // replaced put a trailer facing east with her origin to the EAST of her pin, nose pointing
            // due west, and this test passed it, because 3.365 m the wrong way is still 3.365 m.
            Assert.That(facingNorth.y, Is.LessThan(atPin.y),
                "facing north her origin is not SOUTH of her pin.");
            Assert.That(facingEast.x, Is.LessThan(atPin.x),
                "facing east her origin is not WEST of her pin — the rotation runs the wrong way.");
            Vector2 facingSouth = VehicleCouplingMath.BodyOriginFromKingpin(atPin, 180f, pin);
            Assert.That(facingSouth.y, Is.GreaterThan(atPin.y),
                "facing south her origin is not NORTH of her pin.");
            Vector2 facingWest = VehicleCouplingMath.BodyOriginFromKingpin(atPin, 270f, pin);
            Assert.That(facingWest.x, Is.GreaterThan(atPin.x),
                "facing west her origin is not EAST of her pin.");
        }

        // =============================================================================================
        //  5. THE RITUAL
        // =============================================================================================

        /// <summary>
        /// ⚠️⚠️ <b>You may not drop a trailer on raised legs.</b> A coupled trailer is held off the
        /// ground by the pin alone; releasing there puts her nose in the yard.
        ///
        /// <para>A FACT, not a lock: the refusal names the legs and clears the moment they are wound
        /// down. That is the same shape as the exit-afloat gate — the game says what is wrong rather
        /// than disabling a verb.</para>
        /// </summary>
        [Test]
        public void SheWillNotBeDroppedOnRaisedLegs()
        {
            var yard = new GameObject("yard");
            try
            {
                (VehicleHitch hitch, TowedBody trailer, VehicleDoors trailerDoors) = Pair(yard);

                Assert.That(hitch.Couple(trailer), Is.True, "she did not couple.");
                Assert.That(trailer.IsCoupled, Is.True);

                // Coupling sends the legs UP — the kit's own discipline, and it takes the crank's time.
                trailerDoors.Advance(GameServices.VehicleGearCrankSeconds);
                Assert.That(trailer.LegsAreDown, Is.False, "her legs never came up on coupling.");

                Assert.That(hitch.TryUncouple(out string refusal), Is.False,
                    "she was dropped on her belly.");
                StringAssert.Contains("legs", refusal);
                Assert.That(hitch.IsCoupled, Is.True, "the refused release let her go anyway.");

                // Wind them down and the same press works.
                trailerDoors.SetGroupTarget("gear", 0f);
                trailerDoors.Advance(GameServices.VehicleGearCrankSeconds);
                Assert.That(trailer.LegsAreDown, Is.True);

                Assert.That(hitch.TryUncouple(out _), Is.True, "legs down and she still would not go.");
                Assert.That(hitch.IsCoupled, Is.False);
                Assert.That(trailer.IsCoupled, Is.False);
            }
            finally { Object.DestroyImmediate(yard); }
        }

        /// <summary>A trailer that leaves the world must not leave a tractor believing she is still
        /// on the plate — the despawn case, which is how a phantom coupling would survive a region
        /// change.</summary>
        [Test]
        public void ADespawnedTrailerLeavesNoPhantomCoupling()
        {
            var yard = new GameObject("yard");
            try
            {
                (VehicleHitch hitch, TowedBody trailer, _) = Pair(yard);
                Assert.That(hitch.Couple(trailer), Is.True);

                Object.DestroyImmediate(trailer.gameObject);
                Assert.That(hitch.IsCoupled, Is.False,
                    "the tractor still thinks she is towing something that no longer exists.");
            }
            finally { Object.DestroyImmediate(yard); }
        }

        // =============================================================================================
        //  6. ⭐⭐ THE FRAME — the transform is the compass, and the coupling lives in it
        // =============================================================================================
        //
        // Every reader of a heading in this game is BoatKinematics.BearingDegrees(transform.up): 0 north,
        // clockwise positive, inverse z = −bearing. The coupling's two rotations were written the OTHER
        // way — (x·c − y·s, x·s + y·c) at +heading, a counter-clockwise turn — and agreed with the drawn
        // trailer only where sin(heading) = 0. Every fixture above stood its tractor north, so the sweeps
        // below are the fixture that was missing: written first, watched fail at 90° and 270° by 6.73 m,
        // and then the arithmetic was fixed. Nothing in this section places a trailer with the code it
        // checks — she is stood by her TRANSFORM, which is the frame the picture, the door and the plate
        // all read (memory an-exact-boundary-passes-in-the-frame-that-built-it).

        static readonly float[] FourHeadings = { 0f, 90f, 180f, 270f };

        static void AssertNear(Vector2 actual, Vector2 expected, float tolerance, string why)
        {
            float apart = Vector2.Distance(actual, expected);
            Assert.That(apart, Is.LessThan(tolerance),
                $"{why} — {actual} against {expected}, {apart:0.####} m apart.");
        }

        static Vector3 PinLocal(VehicleMeshDef trailerMesh) =>
            new Vector3(trailerMesh.Kingpin.CouplingPointLocal.x,
                        trailerMesh.Kingpin.CouplingPointLocal.y, 0f);

        /// <summary>The two frames, spelled out at the cardinals and then against a real transform at
        /// all eight compass points: the nose is the compass direction and the curb side is a quarter
        /// turn CLOCKWISE of it — facing east, +x is SOUTH.</summary>
        [Test]
        public void ALocalOffsetLandsWhereTheTransformPutsIt()
        {
            var nose = new Vector2(0f, 1f);
            var curb = new Vector2(1f, 0f);

            AssertNear(VehicleCouplingMath.LocalOffsetToWorld(nose, 0f), new Vector2(0f, 1f), 1e-6f,
                "facing north her nose is not north");
            AssertNear(VehicleCouplingMath.LocalOffsetToWorld(nose, 90f), new Vector2(1f, 0f), 1e-6f,
                "facing east her nose is not east");
            AssertNear(VehicleCouplingMath.LocalOffsetToWorld(nose, 180f), new Vector2(0f, -1f), 1e-6f,
                "facing south her nose is not south");
            AssertNear(VehicleCouplingMath.LocalOffsetToWorld(curb, 0f), new Vector2(1f, 0f), 1e-6f,
                "facing north her curb side is not east");
            AssertNear(VehicleCouplingMath.LocalOffsetToWorld(curb, 90f), new Vector2(0f, -1f), 1e-6f,
                "facing east her curb side is not SOUTH — the rotation is counter-clockwise");
            AssertNear(VehicleCouplingMath.LocalOffsetToWorld(curb, 270f), new Vector2(0f, 1f), 1e-6f,
                "facing west her curb side is not north");

            var probe = new GameObject("probe");
            try
            {
                var offset = new Vector3(1.22f, 3.365f, 0f);   // a pup's nose corner: nobody can eyeball it
                for (int i = 0; i < 8; i++)
                {
                    float heading = 45f * i;
                    probe.transform.rotation = Quaternion.Euler(0f, 0f, -heading);   // z = −bearing
                    Vector2 drawn = probe.transform.TransformPoint(offset);
                    AssertNear(VehicleCouplingMath.LocalOffsetToWorld(offset, heading), drawn, 1e-5f,
                        $"on {heading}° the coupling and the transform put the same local point in " +
                        "different places");
                }
            }
            finally { Object.DestroyImmediate(probe); }
        }

        /// <summary>⭐⭐ THE 90° FIXTURE. Her kingpin is reported where she is DRAWN, at four headings.
        /// Before the fix this failed at 90° and 270° with the pin 6.73 m from the picture.</summary>
        [Test]
        public void HerKingpinIsReportedWhereSheIsDrawn_AtFourHeadings()
        {
            VehicleMeshDef mesh = Load(Pup);
            Vector3 pinLocal = PinLocal(mesh);

            foreach (float heading in FourHeadings)
            {
                var go = new GameObject("trailer");
                try
                {
                    go.AddComponent<VehicleDoors>().Configure(mesh);
                    var body = go.AddComponent<TowedBody>();
                    body.Configure(mesh);
                    body.HeadingDegrees = heading;
                    go.transform.position = new Vector3(10f, 4f, 0f);

                    Vector2 drawn = go.transform.TransformPoint(pinLocal);
                    AssertNear(body.KingpinWorld, drawn, 1e-4f,
                        $"on {heading}°: KingpinWorld and the picture disagree about where her pin is " +
                        "(facing east the old arithmetic put it due WEST of her)");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        /// <summary>
        /// ⭐⭐ A trailer drawn with her pin ON the plate is captured, coupled and released on every
        /// heading — the whole ritual, at the four cardinals. The trailer is stood by her transform
        /// (see <see cref="Pair"/>): a fixture that stood her with <c>BodyOriginFromKingpin</c> would
        /// agree with <c>KingpinWorld</c> at any heading whatever frame either was written in, which is
        /// exactly how the mirror went unmeasured.
        /// </summary>
        [Test]
        public void ASquarelyBackedTractorCapturesHer_AtFourHeadings()
        {
            foreach (float heading in FourHeadings)
            {
                var yard = new GameObject("yard");
                try
                {
                    (VehicleHitch hitch, TowedBody trailer, VehicleDoors doors) = Pair(yard, heading);
                    Assert.That(hitch.HeadingDegrees, Is.EqualTo(heading).Within(1e-3f),
                        "fixture: the tractor is not on the heading asked for.");
                    Assert.That(trailer.HeadingDegrees, Is.EqualTo(heading).Within(1e-3f),
                        "fixture: the trailer did not adopt her transform's heading.");

                    TowedBody captured = hitch.CapturedTrailer();
                    Assert.That(captured, Is.SameAs(trailer),
                        $"on {heading}°: a trailer drawn with her pin on the plate was not captured — " +
                        $"{Window(hitch, trailer)}. The coupling asks in one frame and the picture is " +
                        "drawn in another.");

                    Assert.That(hitch.Couple(trailer), Is.True, $"on {heading}°: the couple refused.");

                    // …and the release ritual holds at every heading too.
                    doors.Advance(GameServices.VehicleGearCrankSeconds);
                    Assert.That(hitch.TryUncouple(out string refusal), Is.False,
                        $"on {heading}°: dropped on raised legs.");
                    StringAssert.Contains("legs", refusal);
                    doors.SetGroupTarget("gear", 0f);
                    doors.Advance(GameServices.VehicleGearCrankSeconds);
                    Assert.That(hitch.TryUncouple(out _), Is.True,
                        $"on {heading}°: legs down and she still would not go.");
                }
                finally { Object.DestroyImmediate(yard); }
            }
        }

        /// <summary>
        /// ⭐ The follow, at four headings: one pull on a heading 20° clockwise of hers swings her the
        /// SAME amount whichever way is up, toward the tractor, and leaves her DRAWN pin on the plate.
        /// The old arithmetic agreed with itself (KingpinWorld and BodyOriginFromKingpin were the same
        /// wrong turn), so the picture is the oracle here, not the coupling's own report.
        /// </summary>
        [Test]
        public void TheFollowKeepsHerDrawnPinOnThePlate_AtFourHeadings()
        {
            const float turn = 20f, travel = 1f;
            float referenceSwing = float.NaN;

            foreach (float heading in FourHeadings)
            {
                var yard = new GameObject("yard");
                try
                {
                    (VehicleHitch hitch, TowedBody trailer, _) = Pair(yard, heading);
                    Assert.That(hitch.Couple(trailer), Is.True, "fixture: couple");

                    // The tractor moves a metre on a heading 20° clockwise of hers, plate and all.
                    float tractorHeading = heading + turn;
                    Vector2 plate = hitch.CouplingPointWorld
                                    + NavMath.DirectionFromBearing(tractorHeading) * travel;
                    trailer.FollowKingpin(plate, tractorHeading, travel, hitch.JackknifeCapDegrees);

                    float swing = Mathf.DeltaAngle(heading, trailer.HeadingDegrees);
                    Assert.That(swing, Is.GreaterThan(0f),
                        $"on {heading}°: the tractor turned clockwise and the trailer swung the other way.");
                    if (float.IsNaN(referenceSwing)) referenceSwing = swing;
                    Assert.That(swing, Is.EqualTo(referenceSwing).Within(1e-4f),
                        $"on {heading}°: the same pull swung her {swing:0.####}° here and " +
                        $"{referenceSwing:0.####}° facing north — the follow depends on which way is up.");

                    Vector2 drawn = trailer.transform.TransformPoint(PinLocal(trailer.Mesh));
                    AssertNear(drawn, plate, 1e-4f,
                        $"on {heading}°: after one follow step the picture draws her pin off the plate");
                    AssertNear(trailer.KingpinWorld, plate, 1e-4f,
                        $"on {heading}°: after one follow step KingpinWorld is off the plate");
                }
                finally { Object.DestroyImmediate(yard); }
            }
        }

        /// <summary>Why a capture did or did not happen, in the tractor's own frame — so a red reports
        /// the geometry rather than just "null".</summary>
        static string Window(VehicleHitch hitch, TowedBody trailer)
        {
            VehicleFifthWheel w = hitch.FifthWheel;
            Vector2 pinWorld = trailer.KingpinWorld;
            Vector3 local = hitch.transform.InverseTransformPoint(new Vector3(pinWorld.x, pinWorld.y, 0f));
            float aft = Mathf.Min(w.RampMouthY, w.SlotSeatY), fore = Mathf.Max(w.RampMouthY, w.SlotSeatY);
            return $"pin local ({local.x:0.#####}, {local.y:0.#####}); slot x {w.CouplingPointLocal.x:0.###}" +
                   $"±{w.SlotHalfWidthMeters:0.###}, y [{aft:0.#####} … {fore:0.#####}]; heading Δ " +
                   $"{Mathf.DeltaAngle(hitch.HeadingDegrees, trailer.HeadingDegrees):0.###}° of " +
                   $"{VehicleCouplingMath.CaptureHeadingToleranceDegrees(w):0.###}°";
        }

        // ---- fixture ---------------------------------------------------------------------------

        /// <summary>
        /// A semi with her plate and a pup with her pin, seated so the capture test passes — both on
        /// <paramref name="headingDegrees"/>. Built rather than loaded from a scene so the whole ritual
        /// is exercised in EditMode.
        ///
        /// <para>⭐ <b>The trailer is stood by her TRANSFORM, never by the coupling arithmetic.</b> Her
        /// pin is where the picture draws it — rotation × local — and she is placed so THAT lands on the
        /// plate. A fixture that placed her with <c>BodyOriginFromKingpin</c> would agree with
        /// <c>KingpinWorld</c> at every heading whatever frame either was written in, which is exactly
        /// how a counter-clockwise coupling shipped under a clockwise picture and nothing noticed.</para>
        /// </summary>
        static (VehicleHitch, TowedBody, VehicleDoors) Pair(GameObject yard, float headingDegrees = 0f)
        {
            VehicleMeshDef tractorMesh = Load(AeroMesh);
            VehicleMeshDef trailerMesh = Load(Pup);

            var tractorGo = new GameObject("tractor");
            tractorGo.transform.SetParent(yard.transform, false);
            tractorGo.transform.rotation = Quaternion.Euler(0f, 0f, -headingDegrees);   // z = −bearing
            var hitch = tractorGo.AddComponent<VehicleHitch>();
            hitch.Configure(tractorMesh, null, "vehicle.aero_semi");

            var trailerGo = new GameObject("trailer");
            trailerGo.transform.SetParent(yard.transform, false);
            trailerGo.transform.rotation = tractorGo.transform.rotation;
            var doors = trailerGo.AddComponent<VehicleDoors>();
            doors.Configure(trailerMesh);
            doors.SnapAllShut();
            var body = trailerGo.AddComponent<TowedBody>();
            body.Configure(trailerMesh);              // adopts the transform's heading

            // Stand her so the pin the PICTURE draws sits on his plate, both pointing the same way.
            Vector2 drawnPinFromOrigin = trailerGo.transform.rotation * PinLocal(trailerMesh);
            Vector2 origin = hitch.CouplingPointWorld - drawnPinFromOrigin;
            trailerGo.transform.position = new Vector3(origin.x, origin.y, 0f);

            return (hitch, body, doors);
        }
    }
}
