using System.IO;
using System.Linq;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ⭐⭐ <b>THE WAY IN — a cab has a DOOR, a saddle has a LEG OVER IT, and the reader has to know
    /// both without learning about either machine.</b>
    ///
    /// <para><b>What this exists for.</b> Until 2026-09-06 <c>VehicleSidecarFacts</c> looked for one
    /// thing: an <c>INTERACT</c> entry with <c>id: "drive"</c>, at the sidecar's ROOT. The ATV pack
    /// publishes neither — her interactions are <c>ride</c> and <c>stand</c>, and they live per
    /// BODY, because a dirtbike's way on is not a quad's. Both misses were <b>silent</b>: an absent
    /// <c>drive</c> was recorded as an ABSENCE and the reader returned, so the bake would have gone
    /// on to produce a machine with no door, no seat, <c>ShowsDriver</c> false, and nobody able ever
    /// to get on. Not a refusal — a bake that succeeds and ships a bike nobody can ride.</para>
    ///
    /// <para>So this fixture asserts in BOTH directions, on the committed bytes: the trucks and the
    /// Otter read exactly as they always did, and the three saddles now read too.</para>
    /// </summary>
    public class VehicleSidecarSaddleTests
    {
        const string AtvSidecar = "docs/art/rigs/gameplay/vehicles/atvIsoRig.atvPack.gameplay.json";
        const string DuallySidecar =
            "docs/art/rigs/gameplay/vehicles/vehicleIsoRig.dually3500.gameplay.json";
        const string OtterSidecar =
            "docs/art/rigs/gameplay/vehicles/amphibIsoRig.otter8x8.gameplay.json";
        const string TrailerSidecar =
            "docs/art/rigs/gameplay/vehicles/trailerIsoRig.trailers.gameplay.json";

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        static VehicleSidecarFacts Read(string path, string bodyScope = null) =>
            VehicleSidecarFacts.Read(File.ReadAllText(Full(path)), path, bodyScope);

        // =============================================================================================
        //  1. THE SADDLE — what the new arm reads
        // =============================================================================================

        [Test]
        public void EverySaddleBodyPublishesAWayOnAndARiderYouCanSee(
            [Values("dirtbike", "trike", "quad")] string body)
        {
            VehicleSidecarFacts facts = Read(AtvSidecar, body);

            Assert.That(facts.Errors, Is.Empty,
                $"bodies.{body} did not read: {string.Join("; ", facts.Errors)}");

            Assert.That(facts.HasDriveDoor, Is.True,
                $"bodies.{body} publishes no numeric reach point for her `ride` interaction, so " +
                "nobody could walk to her to mount.");
            Assert.That(facts.DriveDoorLocal, Is.Not.EqualTo(Vector2.zero),
                "(0,0) is the 'not published' sentinel and it is inside the machine.");

            Assert.That(facts.HasAltDriveDoor, Is.True,
                $"bodies.{body} publishes only one side. Every machine in this pack can be mounted " +
                "from either side — the bike prefers the street side because that is where her " +
                "stand is, and the quad says 'either' — so a single side is a lost half.");
            Assert.That(facts.AltDriveDoorLocal.x, Is.EqualTo(-facts.DriveDoorLocal.x).Within(1e-4f),
                "the two reach points are mirrored about the centreline (width/2 + 0.55 m outboard " +
                "at the seat reference, on both sides).");
            Assert.That(facts.AltDriveDoorLocal.y, Is.EqualTo(facts.DriveDoorLocal.y).Within(1e-4f),
                "…and at the same station: you mount at the saddle, not at the nose.");

            Assert.That(facts.HasDriverSeat, Is.True,
                $"bodies.{body} publishes no seat, so VehicleMeshDef.ShowsDriver would be FALSE and " +
                "she would be drawn with nobody on her. There is nothing here to be INSIDE.");
            Assert.That(facts.DriverSeatLocal.z, Is.GreaterThan(0.5f),
                "a saddle sits at hip height; a seat reference near the road is a misread pointer.");

            Assert.That(facts.InteractIds, Contains.Item("ride"));
        }

        /// <summary>
        /// ⭐ The seat comes from FOLLOWING <c>at</c> as a path, not from matching a literal. The
        /// pack writes <c>"at": "SADDLE.seat_ref"</c>; this asserts the reader landed on the point
        /// that path names, by reading the same JSON a second way.
        /// </summary>
        [Test]
        public void TheSeatIsTheOneTheRideInteractionPointsAt(
            [Values("dirtbike", "trike", "quad")] string body)
        {
            string json = File.ReadAllText(Full(AtvSidecar));
            VehicleSidecarFacts facts = Read(AtvSidecar, body);

            object root = DeckSidecarJson.Parse(json);
            object saddle = DeckSidecarJson.Member(
                DeckSidecarJson.Member(DeckSidecarJson.Member(root, "bodies"), body), "SADDLE");
            var reference = DeckSidecarJson.AsArray(DeckSidecarJson.Member(saddle, "seat_ref"));

            Assert.That(reference, Is.Not.Null.And.Count.GreaterThanOrEqualTo(3),
                $"bodies.{body}.SADDLE.seat_ref is not a three-number point any more — the `at` " +
                "pointer in her `ride` interaction names it, so the two moved apart.");

            DeckSidecarJson.TryDouble(reference[0], out double x);
            DeckSidecarJson.TryDouble(reference[1], out double y);
            DeckSidecarJson.TryDouble(reference[2], out double z);

            Assert.That(facts.DriverSeatLocal,
                Is.EqualTo(new Vector3((float)x, (float)y, (float)z)),
                "the reader's seat is not SADDLE.seat_ref.");
        }

        /// <summary>
        /// ⭐ The blocks are read PER BODY. One sidecar, three machines, and the bike's collider is
        /// not the quad's — reading the root would have handed all three the same box, which is the
        /// (file, pick) trap in the place it is least visible.
        /// </summary>
        [Test]
        public void EachSaddleBodyGetsHerOwnColliderAndHerOwnReachPoints()
        {
            VehicleSidecarFacts bike = Read(AtvSidecar, "dirtbike");
            VehicleSidecarFacts trike = Read(AtvSidecar, "trike");
            VehicleSidecarFacts quad = Read(AtvSidecar, "quad");

            foreach (VehicleSidecarFacts f in new[] { bike, trike, quad })
                Assert.That(f.HasCollider, Is.True, $"bodies.{f.BodyScope} declares nothing solid.");

            Assert.That(bike.ColliderMax.x, Is.LessThan(quad.ColliderMax.x),
                "the bike came back as wide as the quad, which means both read the same block. Her " +
                "collider is over the GRIPS (0.86 m); the quad's is over her fender aprons (1.28).");
            Assert.That(bike.DriveDoorLocal, Is.Not.EqualTo(quad.DriveDoorLocal),
                "the two machines' reach points are the same number, so one of them is the other's.");

            // The quad's own handles, which only she has.
            Assert.That(quad.InteractIds, Is.SupersetOf(new[] { "ride", "rack_front", "rack_rear",
                                                                "hitch", "winch" }));
            Assert.That(bike.InteractIds, Is.EquivalentTo(new[] { "ride", "stand" }),
                "the bike publishes exactly two: mount, and kick the stand. A rack id appearing on " +
                "her means the root's interactions leaked back in.");
        }

        /// <summary>
        /// An asked-for body the sidecar does not carry is a REFUSAL, not the first body's numbers —
        /// the trailers' law, on the second container sidecar in the repo.
        /// </summary>
        [Test]
        public void AnUnknownBodyScopeIsRefused()
        {
            VehicleSidecarFacts facts = Read(AtvSidecar, "NONSENSE");
            Assert.That(facts.Errors, Is.Not.Empty,
                "an unknown pick read clean. It must not: the rig itself falls back to the quad for " +
                "one, so the sidecar is the only place a typo can still be caught.");
            Assert.That(facts.HasDriverSeat, Is.False);
            Assert.That(facts.HasCollider, Is.False);
        }

        // =============================================================================================
        //  2. THE OTHER DIRECTION — the cabs read exactly as they did
        // =============================================================================================

        /// <summary>
        /// ⚠️⚠️ <b>The trucks' path is unchanged, and this is what says so.</b> The Dually's seats
        /// live inside a CAB — a room with a liner, a roof panel and glass opaque at 32 px/m — so she
        /// publishes a door and NO open seat, and a figure drawn in her would be standing on the
        /// roofline. The widening for the saddles must not have given her one.
        /// </summary>
        [Test]
        public void TheDuallyStillPublishesADoorAndNoOpenSeat()
        {
            VehicleSidecarFacts facts = Read(DuallySidecar);

            Assert.That(facts.Errors, Is.Empty);
            Assert.That(facts.HasDriveDoor, Is.True, "she publishes a driver's door.");
            Assert.That(facts.HasDriverSeat, Is.False,
                "the Dually has grown a visible driver. Her seats are in a CAB; drawing a fisher " +
                "there puts her on the roofline.");
            Assert.That(facts.HasAltDriveDoor, Is.False,
                "a cab has ONE driver's door — the alt reach point is a saddle's second side, and " +
                "nothing about a truck should have acquired one.");
            Assert.That(facts.InteractIds, Contains.Item("drive"));
        }

        /// <summary>The Otter's cockpit is an open tub, so she publishes a seat AND a door — the
        /// original of the rule the saddle arm now shares.</summary>
        [Test]
        public void TheOtterStillPublishesBothHerDoorAndHerOpenBench()
        {
            VehicleSidecarFacts facts = Read(OtterSidecar);

            Assert.That(facts.Errors, Is.Empty);
            Assert.That(facts.HasDriveDoor, Is.True);
            Assert.That(facts.HasDriverSeat, Is.True,
                "the Otter's front bench is listed in the open — her sidecar calls the cockpit 'an " +
                "open tub with two benches, not a room' — and a driver in one is genuinely on screen.");
            Assert.That(facts.DriverSeatLocal, Is.Not.EqualTo(Vector3.zero));
            Assert.That(facts.HasAltDriveDoor, Is.False);
        }

        /// <summary>
        /// A TOWED body publishes neither <c>drive</c> nor <c>ride</c>, and still gets the absence.
        /// That is the right answer for something that is dragged, and it is why the reader keys on
        /// the art's own ids rather than on "is there any INTERACT at all".
        /// </summary>
        [Test]
        public void ATowedBodyStillHasNoWayIn(
            [Values("flatbed28", "flatbed53", "reefer28", "reefer53")] string body)
        {
            VehicleSidecarFacts facts = Read(TrailerSidecar, body);

            Assert.That(facts.Errors, Is.Empty);
            Assert.That(facts.HasDriveDoor, Is.False,
                $"trailer '{body}' acquired a way in. She has no engine, no steering axle and no " +
                "seat; she goes where her tractor's fifth wheel takes her.");
            Assert.That(facts.HasDriverSeat, Is.False);
            Assert.That(facts.Absences.Any(a => a.Contains("'drive' or 'ride'")), Is.True,
                "the absence is no longer recorded. A deliberate zero has to read as one in the " +
                "bake report, or it reads as silence.");

            // ⚠️ And her ROOT-level blocks still reach her: the trailer set publishes ONE INTERACT,
            // ONE KINGPIN and ONE GEAR for all four bodies. The body-first lookup added for the ATVs
            // must fall through to the root here, or every trailer loses her coupling.
            Assert.That(facts.InteractIds, Is.Not.Empty,
                "the trailers' root INTERACT block stopped being found. Body-first, root-second: " +
                "their body blocks carry only BODY, CARGO and THRESHOLD.");
            Assert.That(facts.HasKingpin, Is.True,
                "her KINGPIN is at the ROOT and is keyed by body. Losing it makes her track like " +
                "whichever trailer was read instead.");
        }

        /// <summary>The two semis still find their fifth wheel, which is a ROOT block under
        /// <c>TOW</c> — the scoped lookup must not have hidden it.</summary>
        [Test]
        public void TheSemisStillFindTheirFifthWheel(
            [Values("aeroSemiIsoRig.aeroSemi", "classicSemiIsoRig.classicSemi")] string stem)
        {
            VehicleSidecarFacts facts =
                Read($"docs/art/rigs/gameplay/vehicles/{stem}.gameplay.json");

            Assert.That(facts.Errors, Is.Empty);
            Assert.That(facts.HasFifthWheel, Is.True,
                "a tractor lost her plate. TOW.fifth_wheel is a root block; the body-first lookup " +
                "falls through to the root for every single-body sidecar.");
        }

        /// <summary>
        /// Nothing in the vehicle folder reads with an ERROR. The absences are answers; an error is
        /// a refusal, and a refusal nobody looked at is a bake that should not have happened.
        /// </summary>
        [Test]
        public void EverySidecarInTheFolderReadsWithoutAnError()
        {
            foreach (VehicleRigFleet.Vehicle v in VehicleRigFleet.Vehicles)
            {
                VehicleSidecarFacts facts = VehicleSidecarFacts.Read(
                    File.ReadAllText(Full(v.SidecarPath)), v.SidecarPath,
                    string.IsNullOrEmpty(v.SidecarBodyScope) ? null : v.SidecarBodyScope);

                Assert.That(facts.Errors, Is.Empty,
                    $"'{v.Key}' ({v.SidecarPath}" +
                    (string.IsNullOrEmpty(v.SidecarBodyScope) ? "" : $" → bodies.{v.SidecarBodyScope}") +
                    $"): {string.Join("; ", facts.Errors)}");
            }
        }
    }
}
