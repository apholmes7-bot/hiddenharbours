using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE DRIVER IS TOLD</b> — the half of coupling that was missing, and the reason the owner
    /// could not do it.
    ///
    /// <para>Coupling is an act of BACKING; the capture test has always been right. But the only
    /// thing that ever announced a capture was the verb on the release handle, which is an ON-FOOT
    /// interactable — so from the seat, a correct capture and a missed one looked identical. The
    /// loop the owner actually played was: reverse, get out, walk round, read nothing, get back in.
    /// <see cref="TrailerCaptureChanged"/> is what closes that, and this pins its three properties:
    /// it fires on a DISTANCE rather than a frame, it fires only on CHANGE, and it stops being said
    /// the moment it stops being true.</para>
    /// </summary>
    public sealed class TrailerCaptureNoticeTests
    {
        const string AeroMesh = "Assets/_Project/Data/Vehicles/Meshes/AeroSemiVehicleMesh.asset";
        const string Pup = "Assets/_Project/Data/Vehicles/Meshes/TrailerReefer28VehicleMesh.asset";

        readonly List<TrailerCaptureChanged> _heard = new List<TrailerCaptureChanged>();
        GameObject _yard;

        [SetUp]
        public void SetUp()
        {
            _heard.Clear();
            EventBus.Subscribe<TrailerCaptureChanged>(Hear);
        }

        /// <summary>⚠️ Unsubscribe by hand rather than <c>EventBus.Clear</c>: this suite shares the bus
        /// with every other fixture in the run, and clearing a channel takes every OTHER listener off it
        /// too — a component that subscribed in OnEnable is then deaf for the rest of the session.</summary>
        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<TrailerCaptureChanged>(Hear);
            if (_yard != null) Object.DestroyImmediate(_yard);
            _yard = null;
        }

        void Hear(TrailerCaptureChanged e) => _heard.Add(e);

        static VehicleMeshDef Load(string path)
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(path);
            Assert.That(def, Is.Not.Null, $"{path} did not load — re-run the vehicle bake.");
            return def;
        }

        /// <summary>
        /// A tractor with a pin squarely in her slot, and nothing else in the yard.
        ///
        /// <para>⚠️ <c>Configure</c> is what puts the body in <see cref="TowedBody.All"/> — an editor
        /// <c>AddComponent</c> fires no <c>OnEnable</c>, which is exactly why the registration lives
        /// there as well. Without it the registry is empty and every capture test passes by finding
        /// nothing.</para>
        /// </summary>
        (VehicleHitch hitch, TowedBody body) Pair()
        {
            VehicleMeshDef tractorMesh = Load(AeroMesh);
            VehicleMeshDef trailerMesh = Load(Pup);

            _yard = new GameObject("yard");

            var tractorGo = new GameObject("tractor");
            tractorGo.transform.SetParent(_yard.transform, false);
            var hitch = tractorGo.AddComponent<VehicleHitch>();
            hitch.Configure(tractorMesh, null, "vehicle.aero_semi");

            var trailerGo = new GameObject("trailer");
            trailerGo.transform.SetParent(_yard.transform, false);
            var doors = trailerGo.AddComponent<VehicleDoors>();
            doors.Configure(trailerMesh);
            doors.SnapAllShut();
            var body = trailerGo.AddComponent<TowedBody>();
            body.Configure(trailerMesh);

            // Stand her so the pin the PICTURE draws sits on his plate, both pointing the same way.
            Vector3 pinLocal = trailerMesh.Kingpin.CouplingPointLocal;
            Vector2 drawnPinFromOrigin = trailerGo.transform.rotation * pinLocal;
            Vector2 origin = hitch.CouplingPointWorld - drawnPinFromOrigin;
            trailerGo.transform.position = new Vector3(origin.x, origin.y, 0f);

            Assert.That(hitch.CapturedTrailer(), Is.EqualTo(body),
                "the fixture did not build a captured pair — nothing below tests what it says.");
            return (hitch, body);
        }

        // =============================================================================================
        //  THE TICK IS A DISTANCE, AND THE DISTANCE IS THE ART'S
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>The poll is gated on how far she has DRIVEN, and the gate is the jaw's own
        /// half-width</b> — not a frame count, not a timer, and not a number anybody picked.
        ///
        /// <para><b>Why distance is the honest clock.</b> The only thing that can change whether a pin
        /// is in a slot is the tractor moving under it, so a truck stopped at the wrong angle should
        /// cost nothing however long she sits there — which a timer cannot express and a per-frame poll
        /// refuses to (rule 7). And the step is read off the plate rather than chosen: the narrowest
        /// feature the art drew is the slot's half-width, so a truck that has moved less than that
        /// cannot have changed the answer in any way a driver could see.</para>
        ///
        /// <para>⚠️ Walked from BOTH sides of the gate, a millimetre either way. Standing exactly on it
        /// would be an exact boundary decided by the last bit of a float.</para>
        ///
        /// <para>⭐⭐ <b>The pin is driven OUT of the slot before the gate is walked, and that is the
        /// whole design of this test.</b> The gate governs whether the poll RUNS; whether it SPEAKS is
        /// a second question, answered by change-detection
        /// (<see cref="TheSameAnswerIsAnnouncedOnceHoweverFarSheDrives"/>). Walking the gate with an
        /// unchanged pin asks neither cleanly: silence below the gate is then equally explained by
        /// "the poll did not run" and by "the poll ran and had nothing new to say", and the first
        /// draft of this test read the second as the first and went red on correct code.</para>
        ///
        /// <para>With a real change waiting behind the gate the two come apart: below it the game must
        /// stay silent <b>even though the answer has changed</b> — which only the distance gate can
        /// explain — and above it, it must speak. That is a strictly stronger claim than the one that
        /// was red, and it is the claim the gate actually makes.</para>
        /// </summary>
        [Test]
        public void ThePollIsGatedOnTheJawsOwnHalfWidthOfTravel()
        {
            (VehicleHitch hitch, TowedBody body) = Pair();
            float step = hitch.FifthWheel.SlotHalfWidthMeters;

            Assert.That(step, Is.GreaterThan(0f),
                "the plate baked no jaw, so the poll's gate is 'any movement at all' — which is a "
                + "per-frame poll wearing a distance's clothes.");

            // The first look is ungated and establishes the state — bay 0 ships a pair already
            // captured, so a plate that said nothing until the driver had moved would keep the one
            // pair he is told to go and try silent.
            hitch.PollCapture(0f);
            Assert.That(_heard.Count, Is.EqualTo(1),
                "the very first look said nothing — a tractor that starts the scene already on a pin "
                + "never tells her driver so.");
            Assert.That(_heard[0].Captured, Is.True);
            Assert.That(_heard[0].VehicleId, Is.EqualTo("vehicle.aero_semi"),
                "the announcement does not name the machine it is about, so a HUD cannot tell it "
                + "from the truck two rows away.");
            Assert.That(_heard[0].TrailerMeshId, Is.EqualTo(body.Mesh.Id));
            _heard.Clear();

            // ⭐ Put a REAL CHANGE behind the gate: walk her clean out of the window, further than
            // the whole reach so no boundary decides it. From here on, silence can only be the
            // gate's doing — there is something new to say and the game is not saying it.
            float reach = Mathf.Abs(hitch.FifthWheel.SlotSeatY - hitch.FifthWheel.RampMouthY);
            body.transform.position += new Vector3(0f, 2f * reach, 0f);
            Assert.That(hitch.CapturedTrailer(), Is.Null,
                "the fixture failed to get her out of the slot, so the step below has nothing new "
                + "to announce and would pass without the gate existing at all.");

            hitch.PollCapture(step - 0.001f);
            Assert.That(_heard, Is.Empty,
                $"she moved {step - 0.001f:0.###} m — less than the jaw is wide — and the game went "
                + "looking anyway. The gate is not the plate's.");

            hitch.PollCapture(step + 0.001f);
            Assert.That(_heard.Count, Is.EqualTo(1),
                "she moved further than the jaw is wide, the pin had left the slot, and nothing was "
                + "announced — the gate is holding shut past its own step.");
            Assert.That(_heard[0].Captured, Is.False,
                "the gate opened and the game announced the pin it had already reported.");
        }

        /// <summary>⭐ <b>Only on CHANGE.</b> A truck reversing through a 0.71 m window crosses the poll's
        /// gate about eleven times; eleven identical sentences on the bus is a per-frame publish with a
        /// gate in front of it.</summary>
        [Test]
        public void TheSameAnswerIsAnnouncedOnceHoweverFarSheDrives()
        {
            (VehicleHitch hitch, TowedBody _) = Pair();
            float step = hitch.FifthWheel.SlotHalfWidthMeters;

            for (int i = 1; i <= 12; i++) hitch.PollCapture(i * step * 1.5f);

            Assert.That(_heard.Count, Is.EqualTo(1),
                $"twelve polls of an unchanged pin produced {_heard.Count} announcements.");
            Assert.That(_heard[0].Captured, Is.True);
        }

        /// <summary>⭐ <b>And it stops being said when it stops being true.</b> A driver who backs clean
        /// past the seat has lost the pin, and a line still telling him to get out would be the popup's
        /// own failure mode — a surface outliving its own actionability.</summary>
        [Test]
        public void DrivingThePinOutOfTheSlotWithdrawsTheOffer()
        {
            (VehicleHitch hitch, TowedBody body) = Pair();
            float step = hitch.FifthWheel.SlotHalfWidthMeters;

            hitch.PollCapture(step * 2f);
            Assert.That(_heard.Count, Is.EqualTo(1), "the pair never announced a capture to lose.");

            // Walk her clean out of the window — further than the whole reach, so no boundary decides it.
            float reach = Mathf.Abs(hitch.FifthWheel.SlotSeatY - hitch.FifthWheel.RampMouthY);
            body.transform.position += new Vector3(0f, 2f * reach, 0f);

            hitch.PollCapture(step * 4f);
            Assert.That(_heard.Count, Is.EqualTo(2), "the pin left the slot and nobody was told.");
            Assert.That(_heard[1].Captured, Is.False);
            Assert.That(_heard[1].TrailerMeshId, Is.Null,
                "a 'lost it' still named a trailer — a listener reading the id would show a line for "
                + "a pin that is no longer there.");
        }

        /// <summary>⚠️ <b>The offer is SPENT once it is taken.</b> The poll returns early on a coupled
        /// plate, so without an explicit withdrawal the last "pin's in the slot" would stand for as long
        /// as the pair was together — telling a driver to go and do a thing he has done.</summary>
        [Test]
        public void CouplingHerWithdrawsTheOffer()
        {
            (VehicleHitch hitch, TowedBody body) = Pair();
            hitch.PollCapture(hitch.FifthWheel.SlotHalfWidthMeters * 2f);
            _heard.Clear();

            Assert.That(hitch.Couple(body), Is.True, "the shipped hitch refused a captured pair.");

            Assert.That(_heard.Count, Is.EqualTo(1),
                "she went on the pin and the standing offer was never withdrawn.");
            Assert.That(_heard[0].Captured, Is.False);
        }

        /// <summary>A machine with no fifth wheel announces nothing, however far she is driven —
        /// every box truck in the yard would otherwise be polling the trailer registry.</summary>
        [Test]
        public void AMachineWithNoPlateNeverAnnounces()
        {
            _yard = new GameObject("yard");
            var go = new GameObject("box");
            go.transform.SetParent(_yard.transform, false);
            var hitch = go.AddComponent<VehicleHitch>();
            hitch.Configure(Load("Assets/_Project/Data/Vehicles/Meshes/CaboverBoxVehicleMesh.asset"),
                            null, "vehicle.cabover_box");

            for (int i = 1; i <= 20; i++) hitch.PollCapture(i);

            Assert.That(_heard, Is.Empty, "a box truck announced a coupling.");
        }

        // =============================================================================================
        //  THE WORDS AGREE WITH THE VERB
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>The line at the wheel names the verb the handle will offer</b> — and this exists
        /// because the first draft of it did not.
        ///
        /// <para><see cref="VehicleHitch.VerbLabel"/> reads <c>"Couple the trailer"</c> while a pin is
        /// captured and <c>"Pull the release"</c> only once she is already ON. A notice that said "pull
        /// the release" would send the driver out of the cab to look for a verb that does not appear
        /// until after he has done the thing it was telling him to do. The two strings live in different
        /// modules and nothing but this stops them drifting apart again.</para>
        /// </summary>
        [Test]
        public void TheNoticeNamesTheVerbTheHandleWillOffer()
        {
            (VehicleHitch hitch, TowedBody body) = Pair();

            Assert.That(hitch.IsCoupled, Is.False);
            Assert.That(hitch.VerbLabel, Is.EqualTo("Couple the trailer"),
                "the handle's verb moved; the notice below quotes it and must move with it.");

            StringAssert.Contains("couple", HudStrings.TrailerCaptured.ToLowerInvariant(),
                $"the line at the wheel (\"{HudStrings.TrailerCaptured}\") does not name the verb the "
                + $"handle offers (\"{hitch.VerbLabel}\") — it sends him out to hunt for the wrong one.");

            StringAssert.DoesNotContain("release", HudStrings.TrailerCaptured.ToLowerInvariant(),
                "the line names the RELEASE, which is the verb for letting her go — it is what the "
                + "handle offers only AFTER she is coupled.");

            // And the other half of the pair, so the two labels cannot be swapped without a red.
            hitch.Couple(body);
            Assert.That(hitch.VerbLabel, Is.EqualTo("Pull the release"));
        }

        // =============================================================================================
        //  WHOSE WHEEL IS HE HOLDING
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>A capture on somebody else's truck is not the player's news.</b>
        ///
        /// <para>The hitch publishes a fact about ITSELF and names no surface (rule 4) — so every
        /// tractor in the yard may announce, an NPC driver's included, and the laydown routinely has
        /// two: bay 0 holds a coupled pair while the player backs another onto bay 5. Without the id
        /// compare, a truck two rows away taking a pin puts "get out and couple her" on the player's
        /// screen while he is nowhere near a trailer.</para>
        ///
        /// <para>Pinned on the pure rule rather than on the HUD, because the live seam is three fields
        /// on a MonoBehaviour no EditMode test can assemble — the same split
        /// <see cref="HudVisibilityPolicy.MayShow(HudReadout, HudInstruments, bool)"/> already keeps.</para>
        /// </summary>
        [Test]
        public void TheNoticeIsOnlyForTheWheelHeIsHolding()
        {
            const string His = "vehicle.aero_semi";
            const string Hers = "vehicle.classic_semi";

            Assert.That(HudVisibilityPolicy.ShowTrailerCaptureNotice(true, true, His, His), Is.True,
                "his own truck took a pin and he was not told.");

            Assert.That(HudVisibilityPolicy.ShowTrailerCaptureNotice(true, true, Hers, His), Is.False,
                "an NPC's tractor two rows away put a line on the player's screen.");

            Assert.That(HudVisibilityPolicy.ShowTrailerCaptureNotice(true, false, His, His), Is.False,
                "the line stayed up after he got out — which is precisely when he is going to go and "
                + "work the handle, and the popup takes over there.");

            Assert.That(HudVisibilityPolicy.ShowTrailerCaptureNotice(false, true, His, His), Is.False,
                "the pin is not in the slot and the line is up anyway.");

            Assert.That(HudVisibilityPolicy.ShowTrailerCaptureNotice(true, true, null, His), Is.False);
            Assert.That(HudVisibilityPolicy.ShowTrailerCaptureNotice(true, true, His, null), Is.False,
                "nobody is at a wheel yet and a capture was matched against an empty id.");
        }
    }
}
