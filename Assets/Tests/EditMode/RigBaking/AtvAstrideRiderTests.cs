using System.IO;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ⭐⭐ <b>SHE SITS ON THE SADDLE, AND HER HANDS DO NOT REACH THE BARS</b> — the measurement that
    /// says exactly what the missing ASTRIDE stance costs, taken on the committed art.
    ///
    /// <para><b>What is already right.</b> <see cref="DriveSeatMath"/> aligns the SEATS — the one place
    /// a figure and a machine actually touch — by lifting the drive cell by the difference between the
    /// machine's own seat height and the height the clip was baked sitting on. That is the whole of the
    /// placement: <c>PlayerDrivePresenter.SeatDriver</c> writes ONE position and the pose is a baked
    /// sheet, so every other part of the rider lands wherever the clip drew it relative to her bum.</para>
    ///
    /// <para><b>Which is fine until the machine is not a boat.</b> The <c>drive</c> clip puts the
    /// rider's hands <b>0.315 m forward</b> of her seat and <b>0.255 m above</b> it — a helm you reach
    /// out and down to, sitting up. The Otter, the machine this arithmetic shipped for, puts her helm
    /// grips 0.300 m forward and 0.270 m above her bench: <b>0.68 px out in total</b>, which is the
    /// half-pixel <see cref="DriveSeatMath"/>'s own doc claims and this fixture reproduces from her
    /// sidecar rather than taking on trust.</para>
    ///
    /// <para>⚠️⚠️ <b>A saddle is not a helm, and the same arithmetic is 11.6–13.9 px out on all three
    /// ATVs.</b> You sit ON a bike and reach FORWARD and slightly UP to bars that are most of a metre
    /// ahead of you — 0.66 to 0.72 m, against the pose's 0.315. Nothing here is wrong with the maths and
    /// nothing is wrong with the bake: the seats land exactly, and the <b>trike's seat is 0.76 m, the
    /// Otter's own height to the millimetre</b>, so her lift is identical (+0.2758) while her hands are
    /// still 12.7 px short of her grips. The seat is right and the REACH is wrong, which is precisely
    /// the half the pack's README says is missing — a stance with the legs either side and the arms out
    /// to bars at 1.04 m.</para>
    ///
    /// <para><b>So this fixture PINS the gap rather than hiding it.</b> These numbers are expected to go
    /// red exactly once: the day the astride stance lands (upstream ask 1 on
    /// <c>atv-pack-arc</c>) or the day someone re-bakes the drive clip per machine through
    /// <c>OffDeck_mounts.json</c>'s own <c>opts.seatZ/wheelZ/wheelY</c>. Both are deliberate acts, and a
    /// deliberate act should have to come here and say so. An upper-bound assertion would instead go on
    /// passing quietly after the fix and tell nobody it had landed
    /// (<c>a-guard-with-an-absolute-bar-rots-on-a-good-change</c>).</para>
    ///
    /// <para>⚠️ <b>Half of that “once” arrived on 2026-09-09, and this fixture is still green — the
    /// two halves have to be read apart.</b> Rig <b>6.10</b> landed the CLIPS: <c>astride</c>,
    /// <c>astrideStand</c> and four mount transitions now exist in <c>characterIsoRig6.js</c>. Not one
    /// number below moved, because nothing consumes them yet — they are not baked
    /// (<c>CharacterRigBakeMenu.PlayerAnimsBakedElsewhere</c>, group 2: <c>saddleOf()</c> reads
    /// absolute machine metres out of <c>opts.saddle</c>, and the <c>AtvIso.saddleFor()</c> the rig’s
    /// own worked example names does not exist, so a bake today would freeze ONE generic machine into
    /// a sheet three real machines then disagree with), and <c>CharacterOffDeckMountsDef</c> still
    /// carries only <c>drive</c>. The residuals below are still exactly the cost of the missing
    /// STANCE. The day the companion contract ships and the six bake per machine, that is the
    /// deliberate act this fixture asks to come here and say so.</para>
    ///
    /// <para><b>Read from the art both sides, never typed here</b> (rule 6): the grips and the seats come
    /// out of the sidecars, the pose comes out of <see cref="CharacterOffDeckMountsDef"/>. Only the
    /// RESIDUALS are literals, because a residual is the finding.</para>
    /// </summary>
    public class AtvAstrideRiderTests
    {
        const string AtvSidecar = "docs/art/rigs/gameplay/vehicles/atvIsoRig.atvPack.gameplay.json";
        const string OtterSidecar =
            "docs/art/rigs/gameplay/vehicles/amphibIsoRig.otter8x8.gameplay.json";
        const string MountsAssetPath = "Assets/_Project/Data/Characters/CharacterOffDeckMounts.asset";

        /// <summary>Pixels per metre at the shared art scale — for stating a residual in the unit the
        /// defect is actually seen in. 32 px = 1 m is the fleet's convention (ADR 0031).</summary>
        const float PxPerMetre = 32f;

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        static object Sidecar(string repoRelative) =>
            DeckSidecarJson.Parse(File.ReadAllText(Full(repoRelative)));

        /// <summary>Walk a dotted path to a <c>[x, y, z]</c> triple. Fails the test at the point the
        /// path breaks rather than returning a zero — an absent grip point read as (0,0,0) would put the
        /// residual at the seat itself and quietly PASS the comparison it exists to make.</summary>
        static Vector3 Point(object root, params string[] path)
        {
            object node = root;
            for (int i = 0; i < path.Length; i++)
            {
                node = path[i][0] == '[' && path[i][^1] == ']'
                    ? Element(node, int.Parse(path[i][1..^1]), string.Join('.', path, 0, i + 1))
                    : DeckSidecarJson.Member(node, path[i]);

                Assert.That(node, Is.Not.Null,
                    $"the sidecar has no '{string.Join('.', path, 0, i + 1)}'. The art has moved a " +
                    "published mount point; this fixture measures where the rider's hands land against " +
                    "it, so re-read the sidecar before changing anything here.");
            }

            var list = DeckSidecarJson.AsArray(node);
            Assert.That(list, Is.Not.Null.And.Count.EqualTo(3),
                $"'{string.Join('.', path)}' is not an [x, y, z] triple.");
            return new Vector3(Num(list[0]), Num(list[1]), Num(list[2]));
        }

        static object Element(object node, int index, string where)
        {
            var list = DeckSidecarJson.AsArray(node);
            Assert.That(list, Is.Not.Null, $"'{where}' is not a list.");
            Assert.That(index, Is.LessThan(list.Count), $"'{where}' has no element {index}.");
            return list[index];
        }

        static float Num(object v) => System.Convert.ToSingle(v);

        static CharacterOffDeckMountsDef Mounts()
        {
            var def = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterOffDeckMountsDef>(
                MountsAssetPath);
            Assert.That(def, Is.Not.Null,
                $"{MountsAssetPath} did not load. It carries the seat the drive clip was baked on; " +
                "without it there is no pose to measure a machine against.");
            return def;
        }

        /// <summary>Where the <c>drive</c> pose puts the rider's hands relative to her own seat:
        /// (forward, up) in metres, straight off the mount contract.</summary>
        static Vector2 PoseHandsFromSeat(CharacterOffDeckMountsDef m) =>
            new(m.DriveWheelY, m.DriveWheelZ - m.DriveSeatZ);

        // =================================================================================================
        //  1. THE SEAT IS EXACT — and it is worth proving before measuring anything that hangs off it
        // =================================================================================================

        /// <summary>
        /// The lift is the difference between the two seat heights at the camera's height scale, and it
        /// is signed. All four machines here sit HIGHER than the 0.40 m the clip was baked on, so every
        /// one of them raises the fisher; left out she would be drawn sitting inside the machine.
        ///
        /// <para>⚠️ The trike and the Otter share a seat height to the millimetre (0.76 m), so they share
        /// a lift. That coincidence is doing real work in this fixture — it is what separates "the seat
        /// is placed wrong" from "the reach is wrong" in the test below.</para>
        /// </summary>
        [TestCase("dirtbike", 0.94f, TestName = "SeatLift_enduro250")]
        [TestCase("trike", 0.76f, TestName = "SeatLift_trike200")]
        [TestCase("quad", 0.90f, TestName = "SeatLift_utilityQuad")]
        public void TheRidersPivotIsLiftedOntoTheMachinesOwnSeat(string body, float expectedSeatZ)
        {
            Vector3 seat = Point(Sidecar(AtvSidecar), "bodies", body, "SADDLE", "seat_ref");
            Assert.That(seat.z, Is.EqualTo(expectedSeatZ).Within(1e-4f),
                $"'{body}' has changed seat height. Every number below is measured from it.");

            CharacterOffDeckMountsDef m = Mounts();
            float lift = DriveSeatMath.LiftMeters(seat.z, m.DriveSeatZ);

            Assert.That(lift, Is.EqualTo((seat.z - m.DriveSeatZ) * SpriteLightMath.HeightScale)
                                .Within(1e-6f));
            Assert.That(lift, Is.GreaterThan(0f),
                $"'{body}' now sits at or below the {m.DriveSeatZ:F2} m the clip was baked on. That is " +
                "allowed — the lift is signed and would simply drop her — but it has never happened, so " +
                "check it is the art and not a zeroed field.");

            // …and the pivot the presenter writes is that seat's ground point, raised by exactly the lift,
            // with the character's own z carried across untouched (the iso sort band lives there).
            var ground = new Vector2(12.5f, -3.25f);
            const float depthZ = 4.5f;
            Vector3 pivot = DriveSeatMath.SeatPivotWorld(ground, seat.z, m.DriveSeatZ, depthZ);

            Assert.That(pivot.x, Is.EqualTo(ground.x).Within(1e-6f));
            Assert.That(pivot.y, Is.EqualTo(ground.y + lift).Within(1e-6f));
            Assert.That(pivot.z, Is.EqualTo(depthZ).Within(1e-6f),
                "the seating wrote the character's depth. The iso sort band lives on z (ADR 0032) and " +
                "a seating that touches it would quietly re-layer the rider.");
        }

        // =================================================================================================
        //  2. THE CONTROL — the machine this arithmetic shipped for, off her own sidecar
        // =================================================================================================

        /// <summary>
        /// <b>Half a pixel, reproduced rather than believed.</b> <see cref="DriveSeatMath"/>'s doc states
        /// this residual in prose; this reads both halves out of the committed art and gets the same
        /// answer. It is the negative control for the measurement below — if the Otter ever drifts, the
        /// ATV numbers are not evidence about saddles, they are evidence about a broken pose.
        /// </summary>
        [Test]
        public void TheOttersHandsLandOnHerHelm_TheControlThisArithmeticShippedFor()
        {
            object otter = Sidecar(OtterSidecar);
            Vector3 seat = Point(otter, "SEATS", "[0]", "seat_ref");
            Vector3 grip = Point(otter, "COCKPIT", "helm", "grips", "[1]");

            var artHands = new Vector2(grip.y - seat.y, grip.z - seat.z);
            Vector2 residual = artHands - PoseHandsFromSeat(Mounts());

            Assert.That(residual.x, Is.EqualTo(-0.015f).Within(5e-4f),
                "the Otter's helm is 0.300 m forward of her bench against the pose's 0.315.");
            Assert.That(residual.y, Is.EqualTo(+0.015f).Within(5e-4f),
                "…and 0.270 m above it against the pose's 0.255.");
            Assert.That(residual.magnitude * PxPerMetre, Is.LessThan(1f),
                $"the machine the drive pose was fitted to is now {residual.magnitude * PxPerMetre:F2} px " +
                "out at the hands. Until this is under a pixel the saddle measurements below say nothing " +
                "about saddles.");
        }

        // =================================================================================================
        //  3. THE FINDING — a saddle's bars are most of a metre ahead of the seat
        // =================================================================================================

        /// <summary>
        /// ⚠️⚠️ <b>The pinned cost of the missing astride stance</b>, per machine, in the unit it is seen
        /// in. See the class doc for why these are exact rather than an upper bound.
        ///
        /// <para>The residual is stated in the machine's own frame — forward and up, both metric and
        /// frame-independent. On screen the "up" part is drawn at <see cref="SpriteLightMath.HeightScale"/>
        /// and the "forward" part is foreshortened only at the N/S facings, so <b>the side-on facings —
        /// where a bike's bars read most clearly — show the full figure.</b></para>
        /// </summary>
        [TestCase("dirtbike", "enduro250", +0.4050f, -0.1550f, 13.88f)]
        [TestCase("trike", "trike200", +0.3850f, -0.0950f, 12.69f)]
        [TestCase("quad", "utilityQuad", +0.3450f, -0.1150f, 11.64f)]
        public void ASaddlesGripsAreOutOfTheDrivePosesReach_AndThisIsHowFar(
            string body, string key, float expectForward, float expectUp, float expectPx)
        {
            object atv = Sidecar(AtvSidecar);
            Vector3 seat = Point(atv, "bodies", body, "SADDLE", "seat_ref");
            Vector3 grip = Point(atv, "bodies", body, "SADDLE", "grips", "R");

            var artHands = new Vector2(grip.y - seat.y, grip.z - seat.z);
            Vector2 residual = artHands - PoseHandsFromSeat(Mounts());

            string owed =
                $"'{key}' no longer misses her grips by the pinned amount. If the ASTRIDE stance has " +
                "landed (atv-pack-arc upstream ask 1), or the drive clip has been re-baked per machine " +
                "through OffDeck_mounts.json's opts.seatZ/wheelZ/wheelY, then this is the good news and " +
                "these numbers are what you update — say so in the PR, and discharge the ask. If nothing " +
                "was meant to change, the art moved under us.";

            Assert.That(residual.x, Is.EqualTo(expectForward).Within(5e-4f),
                $"{owed}\nHer bars are {artHands.x:F3} m ahead of her saddle; the pose reaches " +
                $"{PoseHandsFromSeat(Mounts()).x:F3} m.");
            Assert.That(residual.y, Is.EqualTo(expectUp).Within(5e-4f),
                $"{owed}\nHer bars are {artHands.y:F3} m above her saddle; the pose reaches " +
                $"{PoseHandsFromSeat(Mounts()).y:F3} m.");
            Assert.That(residual.magnitude * PxPerMetre, Is.EqualTo(expectPx).Within(0.02f), owed);
        }

        /// <summary>
        /// The comparison stated once, as a single fact: <b>every saddle is more than ten times the
        /// Otter's miss.</b> This is the sentence the upstream ask is made of, and it is here so that a
        /// change which improves one machine but not the others cannot quietly leave the ask half true.
        /// </summary>
        [Test]
        public void EverySaddleMissesHerGripsByAnOrderMoreThanTheHelmDoes()
        {
            CharacterOffDeckMountsDef m = Mounts();
            Vector2 pose = PoseHandsFromSeat(m);

            object otter = Sidecar(OtterSidecar);
            Vector3 bench = Point(otter, "SEATS", "[0]", "seat_ref");
            Vector3 helm = Point(otter, "COCKPIT", "helm", "grips", "[1]");
            float helmMiss =
                (new Vector2(helm.y - bench.y, helm.z - bench.z) - pose).magnitude;

            object atv = Sidecar(AtvSidecar);
            foreach (string body in new[] { "dirtbike", "trike", "quad" })
            {
                Vector3 seat = Point(atv, "bodies", body, "SADDLE", "seat_ref");
                Vector3 grip = Point(atv, "bodies", body, "SADDLE", "grips", "R");
                float miss = (new Vector2(grip.y - seat.y, grip.z - seat.z) - pose).magnitude;

                Assert.That(miss, Is.GreaterThan(helmMiss * 10f),
                    $"'{body}' now misses her grips by {miss * PxPerMetre:F2} px against the helm's " +
                    $"{helmMiss * PxPerMetre:F2} px. If a stance has landed that fits a saddle, the " +
                    "per-machine pins above are the ones to update and this sentence is the one to " +
                    "retire — the ask it stands for would be discharged.");
            }
        }

        // =================================================================================================
        //  4. WHAT IS DELIBERATELY NOT DRAWN — the art publishes a lean the bake does not carry
        // =================================================================================================

        /// <summary>
        /// ⚠️ <b>Nothing rolls the rider, and that is correct today.</b> The sidecar's
        /// <c>rider_frame</c> note asks the character rig to take <c>leanDeg</c> from <c>dims()</c> and
        /// roll the rider with the machine — but the bike's <b>bake carries no lean and no stand</b>
        /// (<c>atv-pack-arc</c>: the stand is a whole-machine 12° lean PLUS five extra faces, a topology
        /// change rather than a pose, deferred from the intake). Her mesh has three moving parts and they
        /// are the front wheel, the rear wheel and the fork.
        ///
        /// <para>So a rider rolled by <c>leanDeg</c> today would lean while the machine underneath her
        /// stayed bolt upright — inventing a fact the machine does not have, which is the one thing
        /// <c>PlayerDrivePresenter</c>'s own doc forbids: <i>"If a driver ever leans into a turn it reads
        /// the machine's PUBLISHED axes, never a guess."</i> This pins the absence at BOTH ends so the
        /// day a lean is baked, the rider is known to be owed one.</para>
        /// </summary>
        [Test]
        public void TheArtPublishesALeanAndAStandThatTheBakeDoesNotYetCarry()
        {
            object bike = DeckSidecarJson.Member(
                DeckSidecarJson.Member(Sidecar(AtvSidecar), "bodies"), "dirtbike");

            Assert.That(DeckSidecarJson.Member(bike, "LEAN"), Is.Not.Null,
                "the bike's sidecar has stopped publishing a LEAN block — then there is nothing owed " +
                "and this fixture is the thing to retire.");
            Assert.That(DeckSidecarJson.Member(bike, "STAND"), Is.Not.Null,
                "…and likewise her STAND.");

            var mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(
                "Assets/_Project/Data/Vehicles/Meshes/Enduro250VehicleMesh.asset");
            Assert.That(mesh, Is.Not.Null, "the enduro mesh did not load — re-run the vehicle bake.");

            // ⚠ `Or`, not `Is.AnyOf` — the latter is a newer NUnit than this project ships.
            foreach (VehicleFitment wheel in mesh.Wheels)
                Assert.That(wheel.Slot,
                    Is.EqualTo("WheelF").Or.EqualTo("WheelR").Or.EqualTo("ForkF"),
                    $"the enduro has grown a '{wheel.Slot}' part. If that is her LEAN or her STAND, the " +
                    "rider is now owed the roll her sidecar's rider_frame note asks for, and " +
                    "PlayerDrivePresenter is where it goes — read the axis off the machine, never a " +
                    "guess from velocity.");
        }
    }
}
