#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Vehicles;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>THE LAYDOWN UNDER A RUNNING FRAME PUMP</b> — the two things about the yard that only a
    /// real play session can settle, and that the EditMode fixture is structurally unable to ask.
    ///
    /// <list type="number">
    /// <item><b>The machines actually SKIN.</b> EditMode registers no presentation service, so every
    /// <c>ParkedVehicle</c> and <c>ParkedTrailer</c> there comes back unskinned by design — which means
    /// the EditMode suite cannot see that a towed body now has a picture at all. That is the whole gap
    /// this PR closed, so it wants a test where the skinner really runs.</item>
    /// <item><b>The hitch is installed BY the skinner, not by the fixture.</b> The EditMode couple test
    /// wires a <see cref="VehicleHitch"/> by hand. Here nothing does: if the aero semi's plate does not
    /// come through <c>VehicleSkinner</c>, there is no hitch and the couple is not offered — which is
    /// exactly the shape of the failure the owner would meet walking up to her.</item>
    /// </list>
    ///
    /// <para><b>No key is ever pressed</b> — a headless PlayMode fixture cannot drive the keyboard
    /// (memory <c>playmode-virtual-keypress-is-undeliverable</c>), so the production path is reached
    /// through <see cref="InteractVerb.TryPerform"/> at the door's own published point, which is what
    /// the E press does and is the same route <c>DriveModePlayTests</c> takes.</para>
    ///
    /// <para>⚠️ Driving is stepped on <see cref="WaitForFixedUpdate"/>, never on frames: a frame in
    /// batchmode buys hardware rather than time, so a frame-counted drive covers a distance that
    /// depends on the box it ran on (memory <c>playmode-frames-buy-hardware-not-time</c>). The physics
    /// step is fixed, so a fixed-step count is a real duration.</para>
    /// </summary>
    public class NineMileCreekLaydownPlayTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
            GameServices.Reset();
        }

        /// <summary>Stand the yard up and let a frame run, so every machine has taken the mesh path.</summary>
        private IEnumerator StandTheYard(List<GameObject> into)
        {
            List<GameObject> made = NineMileCreekLaydown.Place();
            into.AddRange(made);
            for (int i = 0; i < made.Count; i++) _spawned.Add(made[i]);

            Assert.That(made.Count, Is.EqualTo(9),
                $"the yard stood up {made.Count} of 9 machines — a def is missing from the bake.");

            yield return null;
            yield return null;
        }

        /// <summary>
        /// ⭐⭐ <b>Every machine in the yard takes the mesh path — the towed bodies included.</b>
        ///
        /// <para>This is the regression that motivated the whole placement seam. <c>VehicleSkinner.Apply</c>
        /// takes a <c>VehicleDef</c>, a towed body deliberately has none, and nothing else installed a
        /// picture — so a trailer placed in a scene was an INVISIBLE object that every existing test
        /// agreed was fine, because no test had ever placed one. A trailer that reports unskinned here
        /// is a trailer the owner walks up to and does not see.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator EveryMachineInTheYardIsActuallyDrawn()
        {
            var made = new List<GameObject>();
            yield return StandTheYard(made);

            int driven = 0, towed = 0;

            foreach (GameObject go in made)
            {
                var trailer = go.GetComponent<ParkedTrailer>();
                if (trailer != null)
                {
                    towed++;
                    Assert.That(trailer.IsSkinned, Is.True,
                        $"{go.name} is a towed body with NO PICTURE — she stands in the yard as an " +
                        "invisible object. This is the defect ParkedTrailer/ApplyTowed exist to fix.");
                    Assert.That(trailer.Trailer, Is.Not.Null,
                        $"{go.name} grew no TowedBody, so no tractor could ever couple to her.");
                    continue;
                }

                var parked = go.GetComponent<ParkedVehicle>();
                Assert.That(parked, Is.Not.Null, $"{go.name} is neither a parked vehicle nor a trailer.");
                driven++;
                Assert.That(parked.IsSkinned, Is.True,
                    $"{go.name} did not skin at play — she stands in the yard undrawn.");
            }

            Assert.That(driven, Is.EqualTo(5), "wrong number of driven machines skinned.");
            Assert.That(towed, Is.EqualTo(4), "wrong number of towed bodies skinned.");
        }

        /// <summary>
        /// ⭐⭐ <b>The pair offers the couple with nothing but the builder and the skinner running.</b>
        ///
        /// <para>Nothing in this test wires a hitch: the aero semi grows one because her own baked def
        /// publishes a fifth wheel and <c>VehicleSkinner</c> reads it. All four trailers stand in the
        /// world, so capturing the wrong one fails here too.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ThePairOffersTheCoupleWithOnlyTheBuilderAndTheSkinnerRunning()
        {
            var made = new List<GameObject>();
            yield return StandTheYard(made);

            GameObject tractorGo = made.Find(g => g != null && g.name == "AeroSemiAtTheLaydown");
            GameObject trailerGo = made.Find(g => g != null && g.name == "Flatbed53AtTheLaydown");
            Assert.That(tractorGo, Is.Not.Null, "the pair's tractor was never placed.");
            Assert.That(trailerGo, Is.Not.Null, "the pair's trailer was never placed.");

            var hitch = tractorGo.GetComponent<VehicleHitch>();
            Assert.That(hitch, Is.Not.Null,
                "the semi grew no VehicleHitch at play — the skinner installs one for any def that " +
                "publishes a plate, so either her bake lost the fifth wheel or she never skinned.");

            TowedBody offered = hitch.CapturedTrailer();
            Assert.That(offered, Is.Not.Null,
                "the semi standing in bay 0 is offered NO trailer, so the hitch affordance the owner " +
                "asked for is not in the world. She is at " + (Vector2)tractorGo.transform.position +
                " on " + hitch.HeadingDegrees.ToString("0.##") + "°.");

            Assert.That(offered.gameObject, Is.EqualTo(trailerGo),
                $"the semi is offered {offered.gameObject.name} rather than the trailer on her own " +
                "plate — a trailer parked elsewhere is being captured across the apron.");

            // And the couple actually takes: the pin drops and the legs are sent up.
            Assert.That(offered.LegsAreDown, Is.True,
                "the parked trailer's landing gear is already up — she is standing on her kingpin.");

            Assert.That(hitch.Couple(offered), Is.True, "the offered couple refused.");
            Assert.That(hitch.IsCoupled, Is.True, "the hitch reports nothing coupled after coupling.");
            Assert.That(offered.CoupledTo, Is.EqualTo(hitch), "the trailer does not know her tractor.");
        }

        /// <summary>
        /// ⭐ <b>E at a placed machine's door takes the wheel.</b> The acceptance criterion, run against
        /// the yard the builder actually stands up rather than a machine built at the origin.
        ///
        /// <para>The conventional box is the one used because she is the longest single road vehicle
        /// (9.6 m) — if any of the five is going to be awkward in her own bay, it is her.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator EAtHerDoorTakesTheWheel()
        {
            var made = new List<GameObject>();
            yield return StandTheYard(made);

            GameObject truckGo = made.Find(g => g != null && g.name == "ConvBoxAtTheLaydown");
            Assert.That(truckGo, Is.Not.Null, "the conventional box was never placed.");

            var door = truckGo.GetComponent<VehicleDoor>();
            Assert.That(door, Is.Not.Null, "the placed truck grew no door at play.");
            Assert.That(door.IsDrivable, Is.True,
                "her door reports NOT drivable with the shipped assets — E would be dead in the yard.");

            var playerGo = new GameObject("Player", typeof(SpriteRenderer), typeof(Rigidbody2D));
            _spawned.Add(playerGo);
            playerGo.transform.position = door.DoorWorldPosition;
            var walk = playerGo.AddComponent<PlayerWalkController>();

            var switcherGo = new GameObject("Switcher");
            _spawned.Add(switcherGo);
            var switcher = switcherGo.AddComponent<ControlSwitcher>();
            switcher.Configure(walk, null, null, null, 0f, null);
            yield return null;

            // The E press, without a keyboard: the verb resolved at the door's own published point.
            var actor = new InteractActor(playerGo.transform.position, Vector2.zero,
                                          InteractContext.OnFoot);
            Assert.That(InteractVerb.TryPerform(actor, 180f), Is.True,
                "standing ON her door point in the yard, the verb resolved nothing — the owner's dead E.");
            yield return null;

            Assert.That(switcher.Mode, Is.EqualTo(ControlMode.Driving),
                "the verb acted but nobody took the wheel.");
        }

        /// <summary>
        /// ⭐⭐ <b>ALL FIVE driven machines pull out of their bays onto the lane</b> — the acceptance
        /// criterion in the owner's own count, not a sample of it. The yard's own claim, and it is
        /// about GROUND rather than input: is a machine parked nose-to-the-lane actually able to
        /// leave, or has her bay been cut so tight she is walled in?
        ///
        /// <para>The yard is stood up FRESH for each machine, so every one of them drives out of a
        /// full yard rather than out of one the previous machines have already vacated. Driving them
        /// in turn through a single placement would make each test easier than the last, which is the
        /// sort of coverage that reads as five and proves one.</para>
        ///
        /// <para>⚠️ <b>Driven through <c>VehicleController.Throttle</c> and deliberately NOT through
        /// <c>ControlSwitcher.DriveInput</c>.</b> That method's own doc says it is "public and explicit
        /// so a headless test can drive it", but while the mode is Driving the switcher's Update calls
        /// <c>ReadDriveInput()</c> every frame, and with no key held that lands
        /// <c>DriveInput(0, 0, false)</c> — so an explicit demand is zeroed before the next physics
        /// step and the machine never moves. Measured: 0.00 m under "full throttle" for 30 s of
        /// physics. Taking the wheel is proved by the test above; what THIS one is about is the room
        /// she has, so it drives the machine directly rather than fighting a keyboard read.</para>
        ///
        /// <para>⚠️ Stepped on <see cref="WaitForFixedUpdate"/>, never on frames — the physics step is
        /// a real duration and a frame is not.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator EveryDrivenMachinePullsOutOfHerBayOntoTheLane()
        {
            var driven = new List<string>();
            foreach (NineMileCreekLaydown.Placement p in NineMileCreekLaydown.Solve())
                if (!p.Unit.IsTowed) driven.Add(p.Unit.Name);

            Assert.That(driven.Count, Is.EqualTo(5),
                $"the yard solved {driven.Count} driven machines, not the five the owner asked for.");

            foreach (string name in driven)
            {
                var made = new List<GameObject>();
                yield return StandTheYard(made);

                GameObject truckGo = made.Find(g => g != null && g.name == name);
                Assert.That(truckGo, Is.Not.Null, $"{name} was never placed.");

                var controller = truckGo.GetComponent<VehicleController>();
                Assert.That(controller, Is.Not.Null,
                    $"{name} grew no VehicleController — she is scenery, not a machine.");

                int bayIndex = -1;
                foreach (NineMileCreekLaydown.Placement p in NineMileCreekLaydown.Solve())
                    if (p.Unit.Name == name) bayIndex = p.Unit.Bay;
                Rect bay = NineMileCreekLaydown.BayArea(bayIndex);

                Vector2 startedAt = truckGo.transform.position;
                Assert.That(bay.Contains(startedAt), Is.True,
                    $"{name} does not start inside her own bay, so leaving it would prove nothing.");

                float wanted = NineMileCreekLaydown.LongestUnitMetres;
                float travelled = 0f;

                for (int step = 0; step < 1500 && travelled < wanted; step++)
                {
                    controller.Throttle = 1f;
                    yield return new WaitForFixedUpdate();
                    travelled = Vector2.Distance((Vector2)truckGo.transform.position, startedAt);
                }
                controller.Throttle = 0f;

                Assert.That(travelled, Is.GreaterThanOrEqualTo(wanted),
                    $"{name} moved {travelled:0.##} m under full throttle in 30 s of physics — she is " +
                    "not driving off her slot.");

                Assert.That(bay.Contains((Vector2)truckGo.transform.position), Is.False,
                    $"{name} has travelled {travelled:0.##} m and is still inside her own bay.");

                // And she left the way she was pointed: SOUTH, onto the lane, not sideways through a
                // neighbour. Her heading never changed, so this is a pure statement about the yard.
                Assert.That(truckGo.transform.position.y, Is.LessThan(startedAt.y),
                    $"{name} pulled out NORTHWARD, away from the lane — her bay faces the wrong way.");

                // Clear the yard before the next machine, so she too leaves a FULL one.
                for (int i = 0; i < made.Count; i++)
                    if (made[i] != null) Object.DestroyImmediate(made[i]);
                yield return null;
            }
        }
    }
}
#endif
