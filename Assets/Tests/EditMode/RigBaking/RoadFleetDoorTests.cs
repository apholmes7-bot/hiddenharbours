using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE ROAD FLEET's doors — every one measured before it was declared (PR 3a).</b>
    ///
    /// <para>The sibling of <see cref="RoadFleetBakeTests"/>, for the parts the player works rather
    /// than the parts the road turns. Its whole subject is the landing gear's law, applied to doors:
    /// <b>a part is a fitting only if the WHOLE vertex set says so.</b> A door is a rigid leaf only
    /// when turning every one of its vertices through one angle about its published pin leaves them
    /// where the rig actually puts them; a slide is a slide only when every sample is an exact rigid
    /// translation; and a part that is neither gets baked at each end rather than posed.</para>
    ///
    /// <para>Everything here re-measures against the RIG. A test that restated the numbers would be
    /// a second transcription agreeing with the first.</para>
    /// </summary>
    public class RoadFleetDoorTests
    {
        /// <summary>
        /// How far a leaf's worst vertex may sit from where one rotation about its pin puts it —
        /// the baker's own threshold, restated here so the two cannot drift apart.
        ///
        /// <para>0.1 mm: above float32's resolution at a 53 ft trailer's coordinates (~1e-6 m, which
        /// is what her barn actually measures) and 300× below one 31 mm pixel. Nothing in the drop
        /// is near it in either direction — every shipped door is ~1e-6 or less, and every part that
        /// is not a leaf misses by metres.</para>
        /// </summary>
        const double HingeEpsilon = 1e-4;

        /// <summary>
        /// The barn leaves' own bar, in METRES — <b>1e-6</b>, the bar the kit's
        /// <c>check-barn-rigid.cjs</c> holds on node, written here as a literal and read from nowhere.
        ///
        /// <para>⚠️ Not <see cref="HingeEpsilon"/>, and never to be read from the baker: a guard that
        /// asks the code under test for its bar is a mirror. The baker's 1e-4 is for a FITTING, posed
        /// about a float32 pin that at |y| ≈ 8 m cannot sit closer than ~2e-7 m to the rig's (the
        /// 53 ft leaf, swung 255° about it, measures 1.066e-6 m); this bar is for the ART, fitted about
        /// the published pin in double. The re-cut measures ≤ 1e-15 m at every pose; the rig before
        /// it, 8.125e-3 m. Nothing lives near it on either side.</para>
        /// </summary>
        const double BarnLeafBar = 1e-6;

        public sealed class Door
        {
            public string Vehicle, Slot, Probe;
            public int Faces;
            public VehicleHingeAxis Axis;
            public float PinA, PinB;      // (x, y) for a vertical pin; (y, z) for a lateral one
            public float Sweep;
            public override string ToString() => $"{Vehicle}.{Slot}";
        }

        // Every hinged leaf in the fleet, with the pin and sweep its own sidecar publishes (the van's
        // hood comes off her RIG — `rotX(p, 1.74, G.hoodZc, …)` at 42° — because her sidecar carries
        // no HOOD block, and the rig is the authority on geometry).
        static readonly Door[] Hinges =
        {
            new Door { Vehicle = "hightopVan", Slot = "DoorFL", Probe = "{dFL:1}", Faces = 137,
                       Axis = VehicleHingeAxis.Vertical, PinA = -0.98f, PinB = 1.66f, Sweep = -62f },
            new Door { Vehicle = "hightopVan", Slot = "DoorFR", Probe = "{dFR:1}", Faces = 137,
                       Axis = VehicleHingeAxis.Vertical, PinA = 0.98f, PinB = 1.66f, Sweep = 62f },
            new Door { Vehicle = "hightopVan", Slot = "BarnL", Probe = "{barnL:1}", Faces = 62,
                       Axis = VehicleHingeAxis.Vertical, PinA = -0.98f, PinB = -2.92f, Sweep = -96f },
            new Door { Vehicle = "hightopVan", Slot = "BarnR", Probe = "{barnR:1}", Faces = 62,
                       Axis = VehicleHingeAxis.Vertical, PinA = 0.98f, PinB = -2.92f, Sweep = 96f },
            new Door { Vehicle = "hightopVan", Slot = "Hood", Probe = "{hood:1}", Faces = 336,
                       Axis = VehicleHingeAxis.Lateral, PinA = 1.74f, PinB = 1.28f, Sweep = 42f },

            new Door { Vehicle = "caboverBox", Slot = "DoorL", Probe = "{dL:1}", Faces = 137,
                       Axis = VehicleHingeAxis.Vertical, PinA = -0.94f, PinB = 2.96f, Sweep = -65f },
            new Door { Vehicle = "caboverBox", Slot = "DoorR", Probe = "{dR:1}", Faces = 137,
                       Axis = VehicleHingeAxis.Vertical, PinA = 0.94f, PinB = 2.96f, Sweep = 65f },
            // ⚠️ 1014 is what the PROBE moves — the whole cab, her two leaves included. The baked
            // fitting keeps 740, because the doors are claimed first and ride it back as children;
            // TheCaboversDoorsAreCutOutOfHerCabAndRideIt asserts that half.
            new Door { Vehicle = "caboverBox", Slot = "CabTilt", Probe = "{tilt:1}", Faces = 1014,
                       Axis = VehicleHingeAxis.Lateral, PinA = 3.20f, PinB = 0.50f, Sweep = -38f },

            new Door { Vehicle = "convBox", Slot = "DoorL", Probe = "{dL:1}", Faces = 137,
                       Axis = VehicleHingeAxis.Vertical, PinA = -0.98f, PinB = 2.38f, Sweep = -65f },
            new Door { Vehicle = "convBox", Slot = "DoorR", Probe = "{dR:1}", Faces = 137,
                       Axis = VehicleHingeAxis.Vertical, PinA = 0.98f, PinB = 2.38f, Sweep = 65f },
            new Door { Vehicle = "convBox", Slot = "Hood", Probe = "{hood:1}", Faces = 1096,
                       Axis = VehicleHingeAxis.Lateral, PinA = 4.08f, PinB = 0.60f, Sweep = -70f },

            new Door { Vehicle = "aeroSemi", Slot = "DoorL", Probe = "{dL:1}", Faces = 137,
                       Axis = VehicleHingeAxis.Vertical, PinA = -1.18f, PinB = 1.77f, Sweep = -65f },
            new Door { Vehicle = "aeroSemi", Slot = "DoorR", Probe = "{dR:1}", Faces = 137,
                       Axis = VehicleHingeAxis.Vertical, PinA = 1.18f, PinB = 1.77f, Sweep = 65f },
            new Door { Vehicle = "aeroSemi", Slot = "Hood", Probe = "{hood:1}", Faces = 1086,
                       Axis = VehicleHingeAxis.Lateral, PinA = 4.02f, PinB = 0.55f, Sweep = -72f },

            new Door { Vehicle = "classicSemi", Slot = "DoorL", Probe = "{dL:1}", Faces = 143,
                       Axis = VehicleHingeAxis.Vertical, PinA = -1.18f, PinB = 1.67f, Sweep = -65f },
            new Door { Vehicle = "classicSemi", Slot = "DoorR", Probe = "{dR:1}", Faces = 143,
                       Axis = VehicleHingeAxis.Vertical, PinA = 1.18f, PinB = 1.67f, Sweep = 65f },
            new Door { Vehicle = "classicSemi", Slot = "Hood", Probe = "{hood:1}", Faces = 902,
                       Axis = VehicleHingeAxis.Lateral, PinA = 4.42f, PinB = 0.55f, Sweep = -70f },

            // The re-cut (rig 2822a7fa…) builds each barn leaf of 72 faces; the rig before it, 27.
            new Door { Vehicle = "trailerReefer28", Slot = "BarnL", Probe = "{barnL:1}", Faces = 72,
                       Axis = VehicleHingeAxis.Vertical, PinA = -1.19f, PinB = -4.245f, Sweep = -255f },
            new Door { Vehicle = "trailerReefer28", Slot = "BarnR", Probe = "{barnR:1}", Faces = 72,
                       Axis = VehicleHingeAxis.Vertical, PinA = 1.19f, PinB = -4.245f, Sweep = 255f },
            new Door { Vehicle = "trailerReefer53", Slot = "BarnL", Probe = "{barnL:1}", Faces = 72,
                       Axis = VehicleHingeAxis.Vertical, PinA = -1.19f, PinB = -8.055f, Sweep = -255f },
            new Door { Vehicle = "trailerReefer53", Slot = "BarnR", Probe = "{barnR:1}", Faces = 72,
                       Axis = VehicleHingeAxis.Vertical, PinA = 1.19f, PinB = -8.055f, Sweep = 255f },
        };

        /// <summary>One barn leaf on the pin her sidecar publishes, in DOUBLE — the rig's own
        /// <c>-S.L/2 + 0.02</c> lands on −4.245 and −8.055 to the last bit, so the fit measures the
        /// art and not a float32 rounding of the pin.</summary>
        public sealed class Barn
        {
            public string Vehicle, Probe;
            public int Side;              // −1 the left leaf (sweeps negative), +1 the right
            public double PinX, PinY;
            public override string ToString() => $"{Vehicle}.{Probe}";
        }

        static readonly Barn[] Barns =
        {
            new Barn { Vehicle = "trailerReefer28", Probe = "barnL", Side = -1, PinX = -1.19, PinY = -4.245 },
            new Barn { Vehicle = "trailerReefer28", Probe = "barnR", Side = 1, PinX = 1.19, PinY = -4.245 },
            new Barn { Vehicle = "trailerReefer53", Probe = "barnL", Side = -1, PinX = -1.19, PinY = -8.055 },
            new Barn { Vehicle = "trailerReefer53", Probe = "barnR", Side = 1, PinX = 1.19, PinY = -8.055 },
        };

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        static VehicleMeshDef LoadMesh(string key)
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(key);
            var def = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(v.MeshAssetPath);
            Assert.That(def, Is.Not.Null, $"{v.MeshAssetPath} did not load — re-run the vehicle bake.");
            return def;
        }

        static VehicleFitment Fitment(string vehicle, string slot)
        {
            VehicleMeshDef def = LoadMesh(vehicle);
            foreach (VehicleFitment f in def.Wheels)
                if (f.Slot == slot) return f;
            Assert.Fail($"'{vehicle}' baked no fitting called '{slot}'. She has: " +
                        string.Join(", ", def.Wheels.Select(w => w.Slot)));
            return default;
        }

        // =============================================================================================
        //  1. EVERY LEAF IS ONE RIGID BODY ON ITS PUBLISHED PIN
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>The measurement the whole PR rests on, re-run against the rig.</b>
        ///
        /// <para>Turn every vertex of the leaf's faces through ONE least-squares best-fit angle about
        /// the published pin, and see how far they land from where the rig puts them. A rigid leaf
        /// leaves a residual at the floating-point floor; anything else leaves metres.</para>
        ///
        /// <para>⚠️ <b>A residual, not an angle spread</b>, and that distinction cost a bake. Reading
        /// each vertex's own turned angle and comparing the spread is ill-conditioned exactly where a
        /// door keeps most of its vertices — ON the hinge edge, radius ~0, where atan2 turns a
        /// last-bit position error into a large angular one. Measured that way these very doors
        /// reported spreads of 6e-5 to 1.6e-3 degrees while their radius error was EXACTLY 0: noise
        /// wearing the shape of non-rigidity, and the obvious repair would have been to loosen a
        /// tolerance until it stopped meaning anything.</para>
        /// </summary>
        [Test]
        public void EveryLeafIsOneRigidBodyOnItsPublishedPin([ValueSource(nameof(Hinges))] Door d)
        {
            using IRigScriptHost host = Host(d.Vehicle);

            double[] r = Hinge(host, d);
            double measured = r[0], residual = r[1], faces = r[2];

            // ⚠️ What the PROBE moves in the rig, which is not always what the FITTING keeps: the
            // cabover's tilt moves 1014 faces and her CabTilt fitting is 740, because her two door
            // leaves are claimed out of the cab before it is asked what is left.
            Assert.That(faces, Is.EqualTo((double)d.Faces),
                $"'{d.Vehicle}.{d.Slot}' moves a different number of faces than the drop measured.");

            Assert.That(residual, Is.LessThanOrEqualTo(HingeEpsilon),
                $"'{d.Vehicle}.{d.Slot}' is not one rigid leaf about ({d.PinA}, {d.PinB}): its worst " +
                $"vertex lands {residual:0.#########} m from where one rotation puts it. ⚠️ Do NOT " +
                "loosen this to make a door fit — the residual is in metres on a 32 px/m grid, so " +
                "anything it can see is a change of kind, not of precision.");

            // …and the leaf really does swing the angle that was declared, modulo the wrap.
            Assert.That(Mathf.DeltaAngle((float)measured, d.Sweep), Is.EqualTo(0f).Within(0.01f),
                $"'{d.Vehicle}.{d.Slot}' swings {measured:0.###}°, which is not the declared " +
                $"{d.Sweep}° even allowing for the 360° wrap.");
        }

        /// <summary>
        /// ⚠️⚠️ <b>THE BARN DOORS SWEEP 255°, NOT −105°, AND THE DIFFERENCE IS A REAL VOLUME.</b>
        ///
        /// <para>The two reach the identical pose — 255 wraps to −105 — so a measurement taken
        /// through <c>atan2</c> reports the short one and a declaration that copied it would look
        /// perfect. It would also be wrong in a way nothing downstream could catch: the published
        /// <c>keep_clear</c> fan runs the leaf out to FULL OUTBOARD at 180° (|x| 2.37 m, wider than
        /// the trailer herself) before folding it back along the side. A leaf animated the short way
        /// arrives looking correct having swept through whatever is parked alongside.</para>
        ///
        /// <para>So the declared sweep must be the LONG way round, and this asserts exactly that:
        /// same final pose, opposite sign, magnitude 255.</para>
        /// </summary>
        [Test]
        public void TheBarnDoorsSweepTheLongWayRound(
            [Values("trailerReefer28", "trailerReefer53")] string key)
        {
            foreach (string slot in new[] { "BarnL", "BarnR" })
            {
                VehicleFitment f = Fitment(key, slot);

                Assert.That(Mathf.Abs(f.SweepDegrees), Is.EqualTo(255f).Within(0.01f),
                    $"'{key}.{slot}' declares a {f.SweepDegrees}° sweep. Her sidecar's DOORS block " +
                    "says 0..255 and her keep_clear fan is drawn for 255 — the short way through " +
                    "the same pose is −105 and sweeps a different volume entirely.");

                Assert.That(Mathf.Abs(f.SweepDegrees), Is.GreaterThan(180f),
                    $"'{key}.{slot}' takes the SHORT way round. Both reach the same pose; only one " +
                    "passes through full outboard at 180° the way the art published it.");
            }
        }

        /// <summary>
        /// ⭐⭐ <b>A barn leaf is ONE rigid body at every pose it passes through, the still one
        /// included — the fault #855 held the trailers for, re-measured on the rig.</b>
        ///
        /// <para>The leaf test above asks one question at one pose, t = 1, against the baker's
        /// 1e-4 m: the right question for a fitting, the wrong one for the art. At <c>280f0538</c>
        /// the rig's <c>artFinish</c> ran over the POSED leaf in world space and lifted 12 of its
        /// vertices in pure z — 2.19e-3 m at t = 0.25, 8.125e-3 m from t = 0.5 on — so the baker
        /// refused both reefers, and because one rig draws all four trailers, #855 landed the
        /// powered trucks and held every trailer back. The re-cut (<c>2822a7fa…</c>) rolls a posed
        /// part in its build frame and <c>artFinish</c> skips it.</para>
        ///
        /// <para>So this walks t through 0, ¼, ½, ¾ and 1, and at each fits EVERY vertex of the
        /// faces the FULL probe claims — the ones that did not move as well, and at t = 0 all of
        /// them. A deviation taken over only the vertices that moved can never see a part that
        /// stays behind (the landing gear's law), and one that never visits the still pose never
        /// proves its claim is the whole leaf and nothing else. The bar is
        /// <see cref="BarnLeafBar"/>, the node checker's, owned here.</para>
        /// </summary>
        [Test]
        public void EveryBarnLeafIsRigidAtEveryPoseItPassesThrough_StillPoseIncluded(
            [ValueSource(nameof(Barns))] Barn b)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            double[] ts = { 0, 0.25, 0.5, 0.75, 1 };
            var rows = new double[ts.Length][];

            using (IRigScriptHost host = Host(b.Vehicle))
                for (int i = 0; i < ts.Length; i++)
                    rows[i] = host.EvaluateString(
                            $"__hingeAt({{{b.Probe}:1}},{{{b.Probe}:{ts[i].ToString("R", inv)}}},'z'," +
                            $"{b.PinX.ToString("R", inv)},{b.PinY.ToString("R", inv)})")
                        .Split(',').Select(x => double.Parse(x, inv)).ToArray();

            string walk = string.Join(" · ", ts.Select((t, i) =>
                $"t {t.ToString(inv)}: {rows[i][1].ToString("0.###e+0", inv)} m"));

            for (int i = 0; i < ts.Length; i++)
            {
                double t = ts[i], deg = rows[i][0], worst = rows[i][1];
                int claimed = (int)rows[i][2], movedClaimed = (int)rows[i][3],
                    movedOutside = (int)rows[i][4], verts = (int)rows[i][5], still = (int)rows[i][6];
                string at = $"'{b}' at t = {t.ToString(inv)}";

                Assert.That(claimed, Is.EqualTo(72),
                    $"{at}: the full probe claims {claimed} faces; the re-cut leaf is 72.");
                Assert.That(verts, Is.EqualTo(312),
                    $"{at}: {verts} vertices were fitted; the leaf's 72 faces carry 312 and every one " +
                    "is fitted, moved or not.");
                Assert.That(movedOutside, Is.EqualTo(0),
                    $"{at}: {movedOutside} faces OUTSIDE the leaf moved — the pose reaches past its door.");

                Assert.That(worst, Is.LessThanOrEqualTo(BarnLeafBar),
                    $"{at}: NOT one rigid leaf about ({b.PinX.ToString(inv)}, {b.PinY.ToString(inv)}) — " +
                    $"its worst vertex lands {worst.ToString("0.###e+0", inv)} m from where one rotation " +
                    $"puts it. The walk: {walk}. ⚠️ Do NOT loosen the bar: 8.125e-3 m is the fault that " +
                    "held the trailers, and it is a quarter of a pixel.");

                Assert.That(Math.Abs(Math.IEEERemainder(deg - b.Side * 255.0 * t, 360.0)),
                    Is.LessThanOrEqualTo(1e-6),
                    $"{at}: the leaf stands at {deg.ToString("0.####", inv)}°, not {(b.Side * 255.0 * t).ToString(inv)}° " +
                    "(mod 360) — the rig turns a barn leaf 255° linearly in t.");

                if (t == 0)
                {
                    Assert.That(movedClaimed, Is.EqualTo(0),
                        $"{at}: the closed leaf is not closed — {movedClaimed} of its faces moved.");
                    Assert.That(still, Is.EqualTo(verts),
                        $"{at}: the still pose fitted {still} still vertices of {verts}; every one is " +
                        "still, and every one must be in the fit.");
                }
                else
                    Assert.That(movedClaimed, Is.EqualTo(claimed),
                        $"{at}: only {movedClaimed} of the leaf's {claimed} faces moved — part of it " +
                        "stays behind.");
            }
        }

        // =============================================================================================
        //  2. THE SLIDES
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>A sliding part is an exact rigid translation at every pose — and the sampled path
        /// reproduces the rig BETWEEN the samples too.</b>
        ///
        /// <para>That second half is the assertion that the sample set is sufficient rather than
        /// merely present. The van's slide has two corners — her outboard pop ramps over t ∈ [0, 1/6]
        /// and her run aft over t ∈ [0.1, 1], which OVERLAP — so a path sampled only at its ends
        /// would cut the corner and slide the door diagonally through her own flank.</para>
        /// </summary>
        [Test]
        public void ASlidingPartReproducesTheRigBetweenItsSamples(
            [Values("hightopVan", "trailerFlatbed28", "trailerReefer53")] string key)
        {
            string slot = key == "hightopVan" ? "SlideDoor" : "LandingGearShoes";
            string axis = key == "hightopVan" ? "slide" : "gear";
            VehicleFitment f = Fitment(key, slot);

            Assert.That(f.Motion, Is.EqualTo(VehicleFitmentMotion.Slide));
            Assert.That(f.SlidePath, Is.Not.Null.And.Length.GreaterThanOrEqualTo(2),
                $"'{key}.{slot}' baked no slide path.");

            using IRigScriptHost host = Host(key);

            foreach (float t in new[] { 0.05f, 0.2f, 0.5f, 0.8f })
            {
                // ⚠️ The gear runs the other way from its pose axis: the rig bakes at gear:1
                // (parked), so the fitting's t = 0 IS gear 1.
                string pose = axis == "slide" ? $"{{slide:{t}}}" : $"{{gear:{1f - t}}}";
                string[] parts = host.EvaluateString(
                    $"__offset({pose},'{MaterialsOf(f, key)}')").Split(',');

                var measured = new Vector3(
                    float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
                double deviation = double.Parse(parts[3],
                    System.Globalization.CultureInfo.InvariantCulture);

                Assert.That(deviation, Is.LessThanOrEqualTo(HingeEpsilon),
                    $"'{key}.{slot}' is not a rigid translation at t {t} — its vertices moved by " +
                    $"different amounts (worst {deviation:0.#########} m apart).");

                Vector3 fromPath = f.SlideOffsetAt(t);
                Assert.That(Vector3.Distance(fromPath, measured), Is.LessThanOrEqualTo(1e-3f),
                    $"'{key}.{slot}': the baked path puts her at {fromPath} at t {t}, the rig at " +
                    $"{measured}. The samples do not describe the path between them — add one at " +
                    "the corner that was missed.");
            }
        }

        // =============================================================================================
        //  3. THE LANDING GEAR — PR 2's deferral, LIFTED
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>The gear ships split, and the split is the measurement.</b>
        ///
        /// <para>PR 2 measured the gear as 24 faces of which 16 translate rigidly by exactly
        /// [0, 0, 0.78] and 8 have their top vertices pinned while their bottoms rise — a telescope —
        /// and refused to fake it: the whole assembly baked into the body at parked and the raise was
        /// deferred with <c>TheLandingGearTelescopes_SoItIsBakedIntoTheBodyRatherThanLiftedOut</c>
        /// pinning it in both directions.</para>
        ///
        /// <para>This is that deferral discharged. The split falls out of the ART — the rigid 16 are
        /// the <c>iron</c> sand shoes and the telescoping 8 are the <c>galv</c> leg tubes, and no side
        /// filter or station window separates them because they are stacked on one another. The shoes
        /// are a Slide; the legs are baked at each end and swapped. <b>The pinned-vertex count is
        /// asserted still, so the day the art makes the legs rigid this goes red and the swap can be
        /// retired for a second Slide.</b></para>
        /// </summary>
        [Test]
        public void TheGearShipsSplit_ShoesSlideAndLegsSwap(
            [Values("trailerFlatbed28", "trailerFlatbed53",
                    "trailerReefer28", "trailerReefer53")] string key)
        {
            VehicleFitment shoes = Fitment(key, "LandingGearShoes");
            VehicleFitment legs = Fitment(key, "LandingGearLegs");

            Assert.That(shoes.Motion, Is.EqualTo(VehicleFitmentMotion.Slide));
            Assert.That(legs.Motion, Is.EqualTo(VehicleFitmentMotion.DiscreteStates),
                "the legs are posed rather than swapped. They TELESCOPE — 16 of their 32 vertices " +
                "do not move at all — so nothing poses them; see the note on this test.");

            Assert.That(shoes.SlidePath[shoes.SlidePath.Length - 1].OffsetMeters.z,
                Is.EqualTo(0.78f).Within(1e-3f),
                "the shoes' lift is no longer the published 0.78 m drop.");

            Assert.That(legs.StateNames, Is.EquivalentTo(new[] { "down", "up" }));
            Assert.That(legs.StateProps, Is.Not.Null.And.Length.EqualTo(2));
            foreach (HullPropMeshDef state in legs.StateProps)
            {
                Assert.That(state, Is.Not.Null, "a leg state baked no mesh.");
                Assert.That(state.IsUsable(), Is.True);
            }

            // The measurement that keeps the swap honest — and that retires it when the art moves.
            using IRigScriptHost host = Host(key);
            Assert.That(host.EvaluateNumber("__pinned({gear:0},'galv')"), Is.EqualTo(16d),
                "the leg tubes no longer have vertices pinned while others move. If the art made " +
                "the whole assembly rigid, the legs CAN become a second Slide fitting — take that " +
                "deliberately: give them a SlidePath, delete the two states, and delete this " +
                "assertion's reason for existing.");
        }

        // =============================================================================================
        //  4. THE HANDLES
        // =============================================================================================

        /// <summary>Every handle a machine carries is an interaction her own sidecar publishes, works
        /// fittings she actually baked, and (bar the ones whose point the art wrote as prose) knows
        /// where the player stands.</summary>
        [Test]
        public void EveryHandleIsOneTheArtPublished(
            [ValueSource(nameof(BakedVehicleKeys))] string key)
        {
            VehicleMeshDef def = LoadMesh(key);
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(key);

            string json = File.ReadAllText(Full(v.SidecarPath));
            var slots = new HashSet<string>(def.Wheels.Select(w => w.Slot), StringComparer.Ordinal);

            foreach (VehicleDoorGroup g in def.DoorGroups)
            {
                StringAssert.Contains($"\"{g.Id}\"", json,
                    $"'{key}' carries a handle '{g.Id}' that {v.SidecarPath} never mentions.");

                foreach (string slot in g.Slots)
                    Assert.That(slots.Contains(slot), Is.True,
                        $"'{key}' handle '{g.Id}' works '{slot}', which she did not bake.");

                if (g.HasReachPoint)
                    Assert.That(g.ReachPointLocal, Is.Not.EqualTo(Vector2.zero),
                        $"'{key}' handle '{g.Id}' claims a reach point at the machine's own origin, " +
                        "which is inside her. (0,0) is the 'not published' sentinel.");
            }
        }

        /// <summary>
        /// ⚠️ <b>The trailers' reach points are per-body FORMULAS, and this recomputes them.</b>
        ///
        /// <para>One sidecar serves four towed bodies of different lengths, so it cannot write a
        /// literal: the gear crank is published as <c>[-1.7, gearY, 0]</c> and the rear doors as
        /// <c>[0, -L/2-1.6, 0]</c>. The reader refuses those as numbers — correctly, because reading
        /// a formula as 0 would hang both handles on the centreline — and the fleet resolves them per
        /// body. Resolving is arithmetic, and arithmetic drifts, so it is checked here against the
        /// sidecar's OWN published length and kingpin rather than against the numbers that produced
        /// it.</para>
        /// </summary>
        [Test]
        public void TheTrailerHandlesSitWhereHerOwnFormulaPutsThem(
            [Values("trailerFlatbed28", "trailerFlatbed53",
                    "trailerReefer28", "trailerReefer53")] string key)
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(key);
            VehicleMeshDef def = LoadMesh(key);

            object root = DeckSidecarJson.Parse(File.ReadAllText(Full(v.SidecarPath)));
            object body = DeckSidecarJson.Member(
                DeckSidecarJson.Member(root, "bodies"), v.SidecarBodyScope);
            object bodyBlock = DeckSidecarJson.Member(body, "BODY");

            float kingpinY = DeckSidecarJson.Float(DeckSidecarJson.Member(bodyBlock, "kingpin_y"));
            float length = DeckSidecarJson.Float(
                DeckSidecarJson.Member(bodyBlock, "body_length_m"),
                DeckSidecarJson.Float(DeckSidecarJson.Member(bodyBlock, "loa_m")));

            Assert.That(def.TryGetDoorGroup("gear", out VehicleDoorGroup gear), Is.True);
            Assert.That(gear.ReachPointLocal.x, Is.EqualTo(-1.7f).Within(1e-3f));
            Assert.That(gear.ReachPointLocal.y, Is.EqualTo(kingpinY - 2.00f).Within(1e-3f),
                "the gear crank is published at [-1.7, gearY, 0] and gearY is her kingpin less the " +
                "2.00 m leg setback — both of which her own BODY block publishes.");

            if (def.TryGetDoorGroup("doors", out VehicleDoorGroup doors))
                Assert.That(doors.ReachPointLocal.y, Is.EqualTo(-length / 2f - 1.6f).Within(1e-3f),
                    "her rear-door handle is published at [0, -L/2-1.6, 0], well aft of the leaves' " +
                    "own 1.175 m keep-clear fan — which is the point of standing there.");
        }

        // =============================================================================================
        //  5. THE CAB THAT CARRIES ITS OWN DOORS
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The cabover's doors are cut OUT of her tilting cab, and ride it.</b>
        ///
        /// <para>Her <c>tilt</c> moves 1014 faces — the whole cab, her two leaves included. The doors
        /// are claimed first, so the cab's own mesh is 740 and each leaf is its own fitting; without
        /// the parent link they would then hang in the air the moment the cab went over. Her sidecar
        /// says it in as many words: the door keep-clear arc <i>"RIDES THE TILT — a tilted cab
        /// carries its door arcs with it"</i>.</para>
        /// </summary>
        [Test]
        public void TheCaboversDoorsAreCutOutOfHerCabAndRideIt()
        {
            VehicleMeshDef def = LoadMesh("caboverBox");

            VehicleFitment tilt = Fitment("caboverBox", "CabTilt");
            Assert.That(tilt.ParentSlot, Is.Null.Or.Empty, "the cab itself hangs off nothing.");

            foreach (string slot in new[] { "DoorL", "DoorR" })
                Assert.That(Fitment("caboverBox", slot).ParentSlot, Is.EqualTo("CabTilt"),
                    $"'{slot}' does not ride the cab. Her leaves are cut out of it, so a tilt " +
                    "without them leaves two doors hanging in the air.");

            // And the cab's mesh really is short of the leaves it gave up.
            using IRigScriptHost host = Host("caboverBox");
            Assert.That(def.Wheels.First(f => f.Slot == "CabTilt").Prop.Mesh.vertexCount,
                Is.GreaterThan(0), "the cab baked an empty mesh.");
            Assert.That(host.EvaluateNumber("__moved({tilt:1})"), Is.EqualTo(1014d),
                "the tilt moves a different amount. It is the whole cab INCLUDING her doors — 1014 " +
                "faces, of which the two leaves are 274 and the CabTilt fitting keeps 740.");
        }

        // =============================================================================================
        //  6. WORKING THEM
        // =============================================================================================

        /// <summary>A handle sends every leaf in its group to the same target, and they walk there at
        /// the configured pace rather than snapping.</summary>
        [Test]
        public void AHandleWorksEveryLeafInItsGroup()
        {
            VehicleMeshDef def = LoadMesh("trailerReefer53");
            var go = new GameObject("doors-test");
            try
            {
                var doors = go.AddComponent<HiddenHarbours.Vehicles.VehicleDoors>();
                doors.Configure(def);
                doors.SnapAllShut();

                Assert.That(doors.ToggleGroup("doors"), Is.True);
                Assert.That(doors.IsMoving, Is.True, "the leaves snapped instead of travelling.");

                // Half a sweep in: both leaves together, neither there yet.
                doors.Advance(GameServices.VehicleDoorSweepSeconds * 0.5f);
                float left = doors.Openness("BarnL"), right = doors.Openness("BarnR");
                Assert.That(left, Is.EqualTo(0.5f).Within(0.02f));
                Assert.That(right, Is.EqualTo(left).Within(1e-4f),
                    "the two leaves are out of step — a pair worked by one handle must travel " +
                    "together rather than scissoring.");

                doors.Advance(GameServices.VehicleDoorSweepSeconds);
                Assert.That(doors.Openness("BarnL"), Is.EqualTo(1f).Within(1e-4f));
                Assert.That(doors.IsMoving, Is.False);

                // ⚠️ The gear is paced by its OWN tunable, because a hand crank is not a door.
                Assert.That(doors.ToggleGroup("gear"), Is.True);
                doors.Advance(GameServices.VehicleDoorSweepSeconds);
                Assert.That(doors.Openness("LandingGearShoes"), Is.LessThan(1f),
                    "the gear wound up in a door's time. It is a hand crank and takes " +
                    $"{GameServices.VehicleGearCrankSeconds}s — the discipline the kit asks for " +
                    "(couple, THEN wind up before rolling) is free if the crank is instant.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        /// <summary>An unknown handle does nothing and says so — never silently works leaf zero.</summary>
        [Test]
        public void AnUnknownHandleWorksNothing()
        {
            VehicleMeshDef def = LoadMesh("aeroSemi");
            var go = new GameObject("doors-test");
            try
            {
                var doors = go.AddComponent<HiddenHarbours.Vehicles.VehicleDoors>();
                doors.Configure(def);

                Assert.That(doors.ToggleGroup("rollup"), Is.False,
                    "a semi answered to a box truck's handle.");
                Assert.That(doors.SetGroupTarget("nonesuch", 1f), Is.False);
                Assert.That(doors.IsMoving, Is.False, "an unknown handle moved something anyway.");
                Assert.That(doors.IndexOfSlot("NotAFitting"), Is.EqualTo(-1),
                    "⚠️ an unknown slot must read −1, never 0 — 0 is a real fitting and working it " +
                    "would open the wrong thing.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // =============================================================================================
        //  helpers
        // =============================================================================================

        static IEnumerable<string> BakedVehicleKeys =>
            VehicleRigFleet.Baked.Where(k => k != "dually3500" && k != "otter8x8");

        /// <summary>The materials a fitting's faces were claimed by, as a JS-safe csv — the gear needs
        /// it (its shoes and legs share a probe) and nothing else does.</summary>
        static string MaterialsOf(VehicleFitment f, string key) =>
            f.Slot == "LandingGearShoes" ? "iron" : "";

        static double[] Hinge(IRigScriptHost host, Door d)
        {
            string kind = d.Axis == VehicleHingeAxis.Vertical ? "z" : "x";
            string csv = host.EvaluateString(
                $"__hinge({d.Probe},'{kind}',{Js(d.PinA)},{Js(d.PinB)})");
            return csv.Split(',')
                      .Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture))
                      .ToArray();
        }

        static string Js(float f) =>
            f.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>A host with this body's rig widened and its rest pose installed — the same base
        /// the baker measures from, so a container rig answers for the RIGHT body.</summary>
        static IRigScriptHost Host(string key)
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(key);
            IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Full(v.ScriptPath)), v.GlobalName,
                new[] { "build", "makeMats" }, v.ScriptPath));

            host.Execute($@"
                var __R = {v.GlobalName};
                function __base(){{ return {v.RestPose}; }}
                function __pose(o){{ var s = __base(); for (var k in o) s[k] = o[k]; return s; }}
                function __faces(o){{ return __R.build(__R.resolve(__pose(o))); }}

                function __set(pose, mat){{
                  var A = __faces({{}}), B = __faces(pose), out = [];
                  for (var i = 0; i < A.length; i++) {{
                    if (mat && A[i].mat !== mat) continue;
                    var p = A[i].v, q = B[i].v, d = false;
                    for (var k = 0; k < p.length && !d; k++)
                      for (var c = 0; c < 3; c++) if (p[k][c] !== q[k][c]) {{ d = true; break; }}
                    if (d) out.push(i);
                  }}
                  return out;
                }}
                function __moved(pose){{ return __set(pose, null).length; }}

                // Vertices of the moved faces that did NOT themselves move — what separates a rigid
                // body from a telescope, and what the moved-subset helpers cannot see.
                function __pinned(pose, mat){{
                  var A = __faces({{}}), B = __faces(pose), s = __set(pose, mat), n = 0;
                  for (var g = 0; g < s.length; g++) {{
                    var p = A[s[g]].v, q = B[s[g]].v;
                    for (var k = 0; k < p.length; k++)
                      if (Math.abs(q[k][0]-p[k][0]) < 1e-12 &&
                          Math.abs(q[k][1]-p[k][1]) < 1e-12 &&
                          Math.abs(q[k][2]-p[k][2]) < 1e-12) n++;
                  }}
                  return n;
                }}

                // One offset over every vertex, and the worst departure from it. dev 0 = rigid.
                function __offset(pose, mat){{
                  var A = __faces({{}}), B = __faces(pose), s = __set(pose, mat || null);
                  var dx = null, worst = 0;
                  for (var g = 0; g < s.length; g++) {{
                    var p = A[s[g]].v, q = B[s[g]].v;
                    for (var k = 0; k < p.length; k++) {{
                      var d = [q[k][0]-p[k][0], q[k][1]-p[k][1], q[k][2]-p[k][2]];
                      if (dx === null) dx = d;
                      for (var c = 0; c < 3; c++) worst = Math.max(worst, Math.abs(d[c]-dx[c]));
                    }}
                  }}
                  if (dx === null) return '0,0,0,-1';
                  return dx[0] + ',' + dx[1] + ',' + dx[2] + ',' + worst;
                }}

                // The least-squares best-fit rotation about a pin, and how far every vertex lands
                // from it. Returns angleDeg, maxResidualMetres, faceCount.
                function __hinge(pose, kind, a, b){{
                  var A = __faces({{}}), B = __faces(pose), s = __set(pose, null);
                  var iu = kind === 'z' ? 0 : 1, iv = kind === 'z' ? 1 : 2, iw = kind === 'z' ? 2 : 0;
                  var sn = 0, cs = 0;
                  for (var g = 0; g < s.length; g++) {{
                    var p = A[s[g]].v, q = B[s[g]].v;
                    for (var k = 0; k < p.length; k++) {{
                      var pu = p[k][iu]-a, pv = p[k][iv]-b, qu = q[k][iu]-a, qv = q[k][iv]-b;
                      sn += pu*qv - pv*qu; cs += pu*qu + pv*qv;
                    }}
                  }}
                  var ang = Math.atan2(sn, cs), c = Math.cos(ang), si = Math.sin(ang), worst = 0;
                  for (var g2 = 0; g2 < s.length; g2++) {{
                    var p2 = A[s[g2]].v, q2 = B[s[g2]].v;
                    for (var k2 = 0; k2 < p2.length; k2++) {{
                      var du = p2[k2][iu]-a, dv = p2[k2][iv]-b;
                      var eu = (a + du*c - dv*si) - q2[k2][iu];
                      var ev = (b + du*si + dv*c) - q2[k2][iv];
                      var ew = p2[k2][iw] - q2[k2][iw];
                      worst = Math.max(worst, Math.sqrt(eu*eu + ev*ev + ew*ew));
                    }}
                  }}
                  var deg = ang * 180 / Math.PI;
                  while (deg > 180) deg -= 360;
                  while (deg < -180) deg += 360;
                  return deg + ',' + worst + ',' + s.length;
                }}");

            // The same fit over a CLAIM instead of over what this pose moved: the faces the FULL
            // probe moves, every vertex of them, at any pose — t = 0 included, where all are still.
            // Returns angleDeg, maxResidualMetres, claimedFaces, claimedMoved, outsideMoved,
            // vertices, stillVertices.
            host.Execute(@"
                function __hingeAt(claim, pose, kind, a, b){
                  var A = __faces({}), B = __faces(pose), C = __set(claim, null);
                  var iu = kind === 'z' ? 0 : 1, iv = kind === 'z' ? 1 : 2, iw = kind === 'z' ? 2 : 0;
                  var inC = {}, movedClaimed = 0, movedOutside = 0;
                  for (var i = 0; i < C.length; i++) inC[C[i]] = 1;
                  for (var f = 0; f < A.length; f++) {
                    var p0 = A[f].v, q0 = B[f].v, d = false;
                    for (var k0 = 0; k0 < p0.length && !d; k0++)
                      for (var c0 = 0; c0 < 3; c0++) if (p0[k0][c0] !== q0[k0][c0]) { d = true; break; }
                    if (d) { if (inC[f]) movedClaimed++; else movedOutside++; }
                  }
                  var sn = 0, cs = 0, verts = 0, still = 0;
                  for (var g = 0; g < C.length; g++) {
                    var p = A[C[g]].v, q = B[C[g]].v;
                    for (var k = 0; k < p.length; k++) {
                      var pu = p[k][iu]-a, pv = p[k][iv]-b, qu = q[k][iu]-a, qv = q[k][iv]-b;
                      sn += pu*qv - pv*qu; cs += pu*qu + pv*qv; verts++;
                      if (Math.abs(q[k][0]-p[k][0]) < 1e-12 && Math.abs(q[k][1]-p[k][1]) < 1e-12 &&
                          Math.abs(q[k][2]-p[k][2]) < 1e-12) still++;
                    }
                  }
                  var ang = Math.atan2(sn, cs), co = Math.cos(ang), si = Math.sin(ang), worst = 0;
                  for (var g2 = 0; g2 < C.length; g2++) {
                    var p2 = A[C[g2]].v, q2 = B[C[g2]].v;
                    for (var k2 = 0; k2 < p2.length; k2++) {
                      var du = p2[k2][iu]-a, dv = p2[k2][iv]-b;
                      var eu = (a + du*co - dv*si) - q2[k2][iu];
                      var ev = (b + du*si + dv*co) - q2[k2][iv];
                      var ew = p2[k2][iw] - q2[k2][iw];
                      worst = Math.max(worst, Math.sqrt(eu*eu + ev*ev + ew*ew));
                    }
                  }
                  return [ang * 180 / Math.PI, worst, C.length, movedClaimed, movedOutside,
                          verts, still].join(',');
                }");
            return host;
        }
    }
}
