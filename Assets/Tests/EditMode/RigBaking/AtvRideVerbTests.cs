using System.Collections.Generic;
using System.IO;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ⭐⭐ <b>YOU CLIMB INTO A CAB AND YOU GET ON A SADDLE — and which it is has to be the ART'S
    /// answer, not a rule about kinds.</b>
    ///
    /// <para>PR 1 gave the three ATVs a working way on: two reach points, the nearest-door dismount,
    /// a rider on the seat. What it could not give them was the right WORD. <c>VehicleDoor</c> said
    /// "Climb in" unconditionally, because until the ATV pack every machine in the game was a cab —
    /// so the popup beside a dirtbike offered the player something a dirtbike does not have.</para>
    ///
    /// <para><b>The fact was already in the sidecar and was being thrown away.</b>
    /// <see cref="VehicleSidecarFacts.ReadWayIn"/> has to choose between two arms to find the door at
    /// all — <c>INTERACT id: "drive"</c> for a cab, <c>id: "ride"</c> for a saddle — and it discarded
    /// which arm it took the moment it returned. <c>WayInId</c> records it, the bake writes it to
    /// <c>VehicleMeshDef.WayInInteractId</c>, and the verb reads it. Nothing here names a vehicle.</para>
    ///
    /// <para><b>And this fixture asserts in BOTH directions</b>, the way
    /// <see cref="VehicleSidecarSaddleTests"/> does: the three saddles say <c>ride</c> and the trucks
    /// and the Otter say <c>drive</c>. A one-directional guard on a default-valued field passes just
    /// as happily when the field is never written at all.</para>
    /// </summary>
    public class AtvRideVerbTests
    {
        const string AtvSidecar = "docs/art/rigs/gameplay/vehicles/atvIsoRig.atvPack.gameplay.json";
        const string DuallySidecar =
            "docs/art/rigs/gameplay/vehicles/vehicleIsoRig.dually3500.gameplay.json";
        const string OtterSidecar =
            "docs/art/rigs/gameplay/vehicles/amphibIsoRig.otter8x8.gameplay.json";

        const string MeshFolder = "Assets/_Project/Data/Vehicles/Meshes/";
        const string DefFolder = "Assets/_Project/Data/Vehicles/";

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        static VehicleSidecarFacts Read(string path, string bodyScope = null) =>
            VehicleSidecarFacts.Read(File.ReadAllText(Full(path)), path, bodyScope);

        /// <summary>Body scope → the baked asset basename, so a case names one machine once.</summary>
        static readonly Dictionary<string, string> Saddles = new Dictionary<string, string>
        {
            { "dirtbike", "Enduro250" },
            { "trike", "Trike200" },
            { "quad", "UtilityQuad" },
        };

        // =============================================================================================
        //  1. THE SIDECAR — which way in each machine actually publishes
        // =============================================================================================

        [Test]
        public void EverySaddleBodyPublishesTheRideWayIn(
            [Values("dirtbike", "trike", "quad")] string body)
        {
            VehicleSidecarFacts facts = Read(AtvSidecar, body);

            Assert.That(facts.Errors, Is.Empty,
                $"bodies.{body} did not read: {string.Join("; ", facts.Errors)}");
            Assert.That(facts.WayInId, Is.EqualTo("ride"),
                $"bodies.{body} is a machine you sit ASTRIDE — her INTERACT block publishes `ride`, " +
                "not `drive`, and that word is the whole reason the verb can be hers rather than a " +
                "rule here.");
        }

        /// <summary>⭐ The control. A cab reads <c>drive</c> and MUST go on reading it — this is the
        /// half of the guard that catches a field that is never written at all, which would look
        /// identical to a correct cab from the truck's side alone.</summary>
        [Test]
        public void EveryCabStillPublishesTheDriveWayIn(
            [Values(DuallySidecar, OtterSidecar)] string sidecar)
        {
            VehicleSidecarFacts facts = Read(sidecar);

            Assert.That(facts.Errors, Is.Empty,
                $"{sidecar} did not read: {string.Join("; ", facts.Errors)}");
            Assert.That(facts.WayInId, Is.EqualTo("drive"),
                $"{sidecar} is a cab. If this ever says `ride` the verb flips under every truck in " +
                "the fleet at once.");
        }

        /// <summary>A machine with no way on must not come out of the reader claiming one. The trailer
        /// set is the case that exists: her <c>couple</c> entry carries PROSE where a reach point would
        /// be, because the act belongs to the tractor.</summary>
        [Test]
        public void AMachineWithNoWayOnRecordsNoWayInId()
        {
            VehicleSidecarFacts facts = Read(
                "docs/art/rigs/gameplay/vehicles/trailerIsoRig.trailers.gameplay.json", "reefer53");

            Assert.That(facts.HasDriveDoor, Is.False, "a towed body is not got into");
            Assert.That(facts.WayInId, Is.Empty,
                "no door found means no way-in id — an id set on the SCAN rather than on the arm " +
                "that succeeds would let a machine with prose for a reach point call itself a saddle.");
        }

        // =============================================================================================
        //  2. THE BAKED ASSET — and asserted on what Unity LOADS, not on what the file says
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>Read through <see cref="AssetDatabase"/>, deliberately.</b> A key written into a
        /// <c>.asset</c>'s YAML is not the same claim as a field the loaded object carries: a name that
        /// does not match the serialized field, or a type Unity cannot deserialise, leaves the file
        /// looking perfect and the object holding its default — and a text assertion on the file would
        /// pass for the whole life of the bug (memory
        /// <c>a-field-in-the-file-is-not-a-field-the-object-carries</c>, #747).
        /// </summary>
        [Test]
        public void TheBakedSaddleMeshCarriesTheSidecarsWayInId(
            [Values("dirtbike", "trike", "quad")] string body)
        {
            string asset = MeshFolder + Saddles[body] + "VehicleMesh.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(asset);
            Assert.IsNotNull(mesh, $"{asset} did not load");

            VehicleSidecarFacts facts = Read(AtvSidecar, body);

            Assert.That(mesh.WayInInteractId, Is.EqualTo(facts.WayInId),
                $"{Saddles[body]}'s baked way-in id disagrees with her own sidecar. The bake writes " +
                "this; a hand edit that drifts from the source is exactly what this compares.");
            Assert.That(mesh.IsSaddle, Is.True,
                $"{Saddles[body]} is something you sit astride and must answer so — the verb, and " +
                "nothing else, hangs off this.");
        }

        /// <summary>The other direction, on the committed assets: a cab's mesh answers false, including
        /// the ones baked before the field existed. An absent key deserialises to empty and empty is
        /// not <c>ride</c>, so every truck in the fleet is byte-for-byte the machine she was.</summary>
        [Test]
        public void EveryCabMeshIsStillACab(
            [Values("Dually3500", "Otter8x8", "AeroSemi", "ClassicSemi", "ConvBox", "CaboverBox",
                    "HightopVan")] string name)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(
                MeshFolder + name + "VehicleMesh.asset");
            Assert.IsNotNull(mesh, $"{name}'s mesh did not load");

            Assert.That(mesh.IsSaddle, Is.False,
                $"{name} is a cab. This is the assertion that turns red if `ride` ever becomes the " +
                "default rather than a machine's own published word.");
        }

        // =============================================================================================
        //  3. THE VERB — what the player is actually offered, off a real def and a real component
        // =============================================================================================

        static VehicleDef Def(string name) =>
            AssetDatabase.LoadAssetAtPath<VehicleDef>(DefFolder + name + ".asset");

        static VehicleDoor DoorOn(VehicleDef def, out GameObject go)
        {
            go = new GameObject(def.name, typeof(Rigidbody2D));
            go.AddComponent<ParkedVehicle>().Configure(def, drivable: true);
            return go.GetComponent<VehicleDoor>();
        }

        [Test]
        public void ASaddleOffersGetOnAndACabOffersClimbIn(
            [Values("Enduro250", "Trike200", "UtilityQuad", "Dually3500", "Otter8x8")] string name)
        {
            VehicleDef def = Def(name);
            Assert.IsNotNull(def, $"{name}'s def did not load");

            VehicleDoor door = DoorOn(def, out GameObject go);
            try
            {
                bool saddle = def.Mesh != null && def.Mesh.IsSaddle;
                Assert.That(door.VerbLabel,
                    Is.EqualTo(saddle ? VehicleDoor.SaddleVerb : VehicleDoor.CabVerb),
                    $"{name} is a {(saddle ? "saddle" : "cab")} and the popup must say so.");

                // ⭐ The two sides of a saddle cannot say different things about the same machine —
                // the alternate candidate forwards the door's own answer rather than deciding again.
                Assert.That(door.AltSide.VerbLabel, Is.EqualTo(door.VerbLabel),
                    "the curb side and the street side are one machine.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // =============================================================================================
        //  4. THE ENVELOPE — the three defs stopped being the Dually
        // =============================================================================================

        /// <summary>
        /// ⚠️⚠️ <b>The defs existed and every number in them was the DUALLY'S.</b> PR 0 created the
        /// three at "the class's tuning defaults", which is the same thing as saying a 112 kg dirtbike
        /// weighed three and a half tonnes and pulled away exactly as hard as a one-tonne pickup. It
        /// looked finished from every direction: three assets, correct ids, correct meshes, all green.
        ///
        /// <para>This is the guard that would have said so. It compares each machine against the
        /// Dually FIELD BY FIELD and requires the pair to differ somewhere real — not a bar on any
        /// particular number, which the owner is free to tune, but a refusal to let a machine ship
        /// wearing another machine's whole envelope.</para>
        /// </summary>
        [Test]
        public void NoSaddleStillCarriesTheDuallysEnvelope(
            [Values("Enduro250", "Trike200", "UtilityQuad")] string name)
        {
            VehicleDef atv = Def(name);
            VehicleDef dually = Def("Dually3500");
            Assert.IsNotNull(atv, $"{name}'s def did not load");
            Assert.IsNotNull(dually, "the Dually's def did not load");

            var shared = new List<string>();
            if (Mathf.Approximately(atv.MassKg, dually.MassKg)) shared.Add("MassKg");
            if (Mathf.Approximately(atv.MaxSpeedMetersPerSecond, dually.MaxSpeedMetersPerSecond))
                shared.Add("MaxSpeed");
            if (Mathf.Approximately(atv.AccelerationMetersPerSecondSquared,
                                    dually.AccelerationMetersPerSecondSquared))
                shared.Add("Acceleration");
            if (Mathf.Approximately(atv.SteerRateFullLocksPerSecond,
                                    dually.SteerRateFullLocksPerSecond))
                shared.Add("SteerRate");
            if (Mathf.Approximately(atv.SteerFalloffHalfSpeedMetersPerSecond,
                                    dually.SteerFalloffHalfSpeedMetersPerSecond))
                shared.Add("SteerFalloff");

            Assert.That(shared, Is.Empty,
                $"{name} still shares {string.Join(", ", shared)} with the Dually 3500. Five fields " +
                "identical to a one-tonne pickup is a def that was created and never tuned, which is " +
                "how the ATV pack shipped its envelopes at intake.");
        }

        /// <summary>
        /// ⭐ <b>The one envelope number the ART publishes</b> — everything else on a
        /// <see cref="VehicleDef"/> is a gameplay knob and the owner's to move, but the sidecar states
        /// a mass per body and calls it <i>"a gameplay knob, quoted so a load model has a number to
        /// start from"</i>. Held to the sidecar so the two cannot drift apart silently; when the owner
        /// deliberately moves one, this names the number he moved away from.
        /// </summary>
        [Test]
        public void EachSaddlesMassIsTheOneHerSidecarPublishes(
            [Values("dirtbike", "trike", "quad")] string body)
        {
            string json = File.ReadAllText(Full(AtvSidecar));
            object root = DeckSidecarJson.Parse(json);
            object bodies = DeckSidecarJson.Member(root, "bodies");
            object mass = DeckSidecarJson.Member(
                DeckSidecarJson.Member(DeckSidecarJson.Member(bodies, body), "BODY"),
                "mass_kg_estimate");

            Assert.IsTrue(DeckSidecarJson.TryDouble(DeckSidecarJson.Member(mass, "value"),
                                                    out double published),
                $"bodies.{body}.BODY.mass_kg_estimate.value is not a number");

            VehicleDef def = Def(Saddles[body]);
            Assert.IsNotNull(def, $"{Saddles[body]}'s def did not load");

            Assert.That(def.MassKg, Is.EqualTo((float)published).Within(0.5f),
                $"{Saddles[body]} carries {def.MassKg} kg; her sidecar publishes {published} kg.");
        }

        // =============================================================================================
        //  5. THE RAKED STEER NEEDS NO NEW FIELD — the handoff asked; this is the answer
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>The rake is in the ART and it does not reach the kinematics.</b> The ATV PR 2
        /// charter asked for "the RAKED steer's own field if the generic controller needs one". It does
        /// not, and the reason is measurable rather than argued: the bike's and the trike's steering
        /// heads are raked 27° and 25°, which lives on the mesh as
        /// <c>VehicleFitment.SteerAxisLocal</c> and is spent POSING the fork; the yaw model is the
        /// kinematic bicycle and takes wheelbase, front track and lock angle and nothing else.
        ///
        /// <para>The proof is that the def's own full-lock radius — solved from those three fields
        /// with no rake term anywhere — reproduces the radius the rig publishes for the same machine.
        /// Two derivations, one answer, so there is nothing left for a fourth field to carry. Adding
        /// one would be a number nobody could measure and everybody would eventually tune.</para>
        /// </summary>
        [Test]
        public void TheRakedSteerNeedsNoControllerFieldBecauseTheRadiiAlreadyAgree(
            [Values("dirtbike", "trike", "quad")] string body)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(
                MeshFolder + Saddles[body] + "VehicleMesh.asset");
            Assert.IsNotNull(mesh, "the mesh did not load");

            // The rake is real and it is on the ART. A vertical axis here would mean the pack had
            // stopped being raked and this test would be guarding nothing.
            if (body != "quad")
            {
                bool raked = false;
                foreach (VehicleFitment f in mesh.Wheels)
                    if (f.SteerLockDegrees > 0f && Mathf.Abs(f.SteerAxisLocal.z) < 0.999f)
                        raked = true;

                Assert.IsTrue(raked,
                    $"{Saddles[body]}'s steering head is published as RAKED; a vertical axis would " +
                    "mean the fitment table changed under this claim.");
            }

            string json = File.ReadAllText(Full(AtvSidecar));
            object turning = DeckSidecarJson.Member(
                DeckSidecarJson.Member(
                    DeckSidecarJson.Member(DeckSidecarJson.Member(
                        DeckSidecarJson.Parse(json), "bodies"), body), "STEERING"),
                "turning");

            object radius = DeckSidecarJson.Member(turning, "radius_m")
                            ?? DeckSidecarJson.Member(turning, "centreline_radius_m");
            Assert.IsTrue(DeckSidecarJson.TryDouble(radius, out double published),
                $"bodies.{body}.STEERING.turning publishes no centreline radius");

            Assert.That(mesh.FullLockTurnRadiusMeters, Is.EqualTo((float)published).Within(0.01f),
                $"{Saddles[body]}: the def solves {mesh.FullLockTurnRadiusMeters:0.###} m at full " +
                $"lock from wheelbase and lock alone; the rig publishes {published:0.###} m. They " +
                "agree, which is what says the rake has no work left to do in the controller.");
        }

        // =============================================================================================
        //  6. PARKED — and the one machine whose parked picture does not exist
        // =============================================================================================

        /// <summary>Three wheels and four stand up by themselves. Neither the trike nor the quad
        /// publishes a <c>STAND</c> at all, so parked and ridden are the same picture for them and
        /// nothing is owed — pinned so that a rig which grows one is noticed here rather than in a
        /// screenshot.</summary>
        [Test]
        public void TheTrikeAndTheQuadHaveNoStandSoParkedIsRidden(
            [Values("trike", "quad")] string body)
        {
            object bodies = DeckSidecarJson.Member(
                DeckSidecarJson.Parse(File.ReadAllText(Full(AtvSidecar))), "bodies");

            Assert.IsNull(DeckSidecarJson.Member(DeckSidecarJson.Member(bodies, body), "STAND"),
                $"bodies.{body} has grown a STAND. Parked has stopped being the same picture as " +
                "ridden for her, and StPetersMachines is standing one in a scene.");
        }

        /// <summary>
        /// ⚠️⚠️ <b>THE ENDURO'S PARKED PICTURE IS NOT BAKED, AND SHE IS NOW PARKED IN A SCENE.</b>
        ///
        /// <para>Her rig publishes a side stand — <c>STAND</c>, <c>default: 1</c> — and says in its own
        /// words what it is for: <i>"she BAKES PARKED. A dirtbike does not stand upright without a
        /// rider … a game that shows her upright with nobody aboard is showing a bug the rig will not
        /// catch."</i> The mesh this repo holds is <c>stand:0</c>, the RIDDEN state. That was the right
        /// call at intake (the parked build is 502 faces against 497 and a 12° lean — a topology
        /// change, not a rotation, so it cannot be posed out of what is here) and it is a VISIBLE gap
        /// now that <c>StPetersMachines</c> stands her outside the shop.</para>
        ///
        /// <para><b>Pinned EXACTLY, not as an upper bound.</b> The day someone bakes the parked build
        /// this test turns red and somebody has to come and wire a parked drawer — which is the whole
        /// point of it. An assertion that merely tolerated the gap would go on passing quietly after
        /// the art landed and tell nobody
        /// (<c>a-guard-with-an-absolute-bar-rots-on-a-good-change</c>).</para>
        /// </summary>
        [Test]
        public void TheEndurosBakedMeshIsStillTheRiddenStateAndHerStandIsOwed()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(
                MeshFolder + "Enduro250VehicleMesh.asset");
            Assert.IsNotNull(mesh, "the enduro's mesh did not load");

            Assert.That(mesh.SourceFaceBuilder, Does.Contain("stand:0"),
                "the enduro's ONE baked build is the ridden state. If this now says stand:1 the " +
                "ridden picture has been replaced rather than joined, and every ride draws a bike " +
                "leaned over on her stand at 40 km/h.");

            object stand = DeckSidecarJson.Member(
                DeckSidecarJson.Member(
                    DeckSidecarJson.Member(
                        DeckSidecarJson.Parse(File.ReadAllText(Full(AtvSidecar))), "bodies"),
                    "dirtbike"),
                "STAND");

            Assert.IsNotNull(stand,
                "the enduro's STAND is the whole reason this gap exists; if the rig has dropped it, " +
                "she genuinely does stand upright and this test should be deleted, not relaxed.");
            Assert.IsTrue(DeckSidecarJson.TryDouble(DeckSidecarJson.Member(stand, "default"),
                                                    out double atRest));
            Assert.That(atRest, Is.EqualTo(1d).Within(1e-9),
                "at rest she is ON the stand — which is exactly the picture this repo cannot draw " +
                "yet. Second bake owed (art-pipeline); see the pack README.");
        }
    }
}
