using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The dev boat picker: cycle the piloted hull in place at the helm.
    ///
    /// <para><b>What actually needs guarding here is the LOCKSTEP.</b> A hull is four things at once — feel
    /// (<see cref="BoatController"/>), hold (<see cref="ShipHold"/>), camera (the Core
    /// <see cref="ActiveBoatChanged"/> signal) and picture (<see cref="BoatHullSkinner"/>) — and they CAN
    /// silently come apart. They did: #208's bug was a hull swap that moved feel, hold and camera while the
    /// picture stayed the dory, because it wrote a sprite onto a renderer the skin had disabled. Nothing
    /// threw; the boat just lied. So the tests below assert all four MOVE TOGETHER on every swap, rather
    /// than assuming that a picker which sets the hull has done its job.</para>
    ///
    /// <para>Defs and sprites are built in-code — no assets, no art on disk — so this stays a pure logic
    /// guard that can't go stale when the owner re-slices a sheet. The committed hull assets are checked
    /// separately by <c>PilotableFleetContentTests</c>.</para>
    /// </summary>
    public class DevBoatPickerTests
    {
        private readonly List<Object> _spawned = new();
        private readonly List<ActiveBoatChanged> _cameraSignals = new();

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Clear<DevNotice>();
            _cameraSignals.Clear();
            EventBus.Subscribe<ActiveBoatChanged>(OnCamera);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<ActiveBoatChanged>(OnCamera);
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Clear<DevNotice>();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private void OnCamera(ActiveBoatChanged e) => _cameraSignals.Add(e);

        // ---- helpers ------------------------------------------------------------------------

        private Sprite MakeSprite(string name)
        {
            var tex = new Texture2D(4, 4);
            _spawned.Add(tex);
            var spr = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            spr.name = name;
            _spawned.Add(spr);
            return spr;
        }

        private Sprite[] MakeSprites(string stem, int count)
        {
            var set = new Sprite[count];
            for (int i = 0; i < count; i++) set[i] = MakeSprite($"{stem}_{i}");
            return set;
        }

        private BoatVisualDef MakeVisual(string id)
        {
            var v = ScriptableObject.CreateInstance<BoatVisualDef>();
            v.Id = id;
            v.Facings = MakeSprites(id + "Hull", 8);
            _spawned.Add(v);
            return v;
        }

        /// <summary>A hull with a distinct hold/camera/mass, so a swap that half-lands is VISIBLE as a
        /// mismatch rather than passing by coincidence.</summary>
        private BoatHullDef MakeHull(string id, BoatVisualDef visual, int hold, float camera, float massKg)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.DisplayName = id;
            h.Visual = visual;
            h.Sprite = MakeSprite(id + "Plain");
            h.HoldUnits = hold;
            h.CameraWorldHeightMeters = camera;
            h.MassKg = massKg;
            _spawned.Add(h);
            return h;
        }

        private (DevBoatPicker picker, BoatController boat, ShipHold hold, SpriteRenderer sr, GameObject go)
            MakeRig(BoatHullDef[] roster, BoatHullDef startHull)
        {
            var go = new GameObject("Boat");
            _spawned.Add(go);
            go.AddComponent<Rigidbody2D>();
            var boat = go.AddComponent<BoatController>();
            var sr = go.AddComponent<SpriteRenderer>();
            var hold = go.AddComponent<ShipHold>();
            var picker = go.AddComponent<DevBoatPicker>();

            boat.SetHull(startHull);
            hold.SetHull(startHull);
            sr.sprite = startHull != null ? startHull.Sprite : null;
            picker.Configure(roster, boat, hold, sr);
            return (picker, boat, hold, sr, go);
        }

        /// <summary>THE assertion this whole file exists for: the boat's feel, hold, camera and picture all
        /// agree about which hull is being piloted.</summary>
        private void AssertShowing(BoatHullDef expected, BoatController boat, ShipHold hold,
                                   SpriteRenderer sr, GameObject go)
        {
            Assert.AreSame(expected, boat.Hull, $"FEEL: the controller should be on {expected.Id}");
            Assert.AreEqual(expected.HoldUnits, hold.CapacityUnits,
                $"HOLD: capacity must follow the hull to {expected.Id} — a boat whose hold lags is lying " +
                "about what it can carry");
            Assert.AreEqual(expected.Id, _cameraSignals[_cameraSignals.Count - 1].BoatId,
                "CAMERA: the last ActiveBoatChanged must name this hull");
            Assert.AreEqual(expected.CameraWorldHeightMeters,
                _cameraSignals[_cameraSignals.Count - 1].CameraWorldHeightMeters, 0.001f,
                "CAMERA: …and carry its framing");

            // PICTURE: the half that #208 dropped. A skinned hull draws on the compass child (base renderer
            // hidden); a plain hull draws on the base renderer (no compass child).
            var child = go.transform.Find(BoatHullSkinner.VisualChildName);
            if (expected.Visual != null && expected.Visual.HasFullCompass())
            {
                Assert.IsNotNull(child, $"PICTURE: {expected.Id} has facings — it must wear the compass");
                Assert.AreSame(expected.Visual.Facings[0], child.GetComponent<SpriteRenderer>().sprite,
                    $"PICTURE: the compass must show {expected.Id}'s art, not the previous hull's");
                Assert.IsFalse(sr.enabled, "PICTURE: the plain renderer is hidden under a skin");
            }
            else
            {
                Assert.IsNull(child, $"PICTURE: {expected.Id} has no facings — no compass should be left over");
                Assert.IsTrue(sr.enabled, "PICTURE: the plain rotating picture comes back");
                Assert.AreSame(expected.Sprite, sr.sprite);
            }
        }

        // ---- the cycle ----------------------------------------------------------------------

        [Test]
        public void Next_WalksTheRosterInOrder_AndWraps()
        {
            var a = MakeHull("boat.a", MakeVisual("visual.a"), 6, 14f, 400f);
            var b = MakeHull("boat.b", MakeVisual("visual.b"), 20, 18.5f, 1200f);
            var c = MakeHull("boat.c", MakeVisual("visual.c"), 8, 19f, 950f);
            var (picker, boat, _, _, _) = MakeRig(new[] { a, b, c }, a);

            Assert.AreSame(a, picker.Current, "the cycle starts on the hull the boat is already wearing…");

            picker.Next();
            Assert.AreSame(b, picker.Current, "…so the FIRST press moves on, rather than re-applying it");
            picker.Next();
            Assert.AreSame(c, picker.Current);
            picker.Next();
            Assert.AreSame(a, picker.Current, "and the end of the roster wraps back to the start");
            Assert.AreSame(a, boat.Hull);
        }

        [Test]
        public void TheStPetersCycle_IsSevenRungs_AndWrapsOnTheSeventh()
        {
            // St Peters' real roster shape, in its real order: dory → fishing boat → punt → punt (upgraded)
            // → console → sport → sport twin → wrap. Mirrored in memory rather than loaded off disk, because
            // Data/Boats/Punt.asset is builder-generated and has never been committed (a clean clone has no
            // such file) — so this asserts the CYCLE, which is the picker's job, and leaves the assets to the
            // content tests. The COUNT is the point: the two punts took it from 5 to 7, and an off-by-one
            // here is a rung the owner cycles into and finds empty.
            var roster = new[]
            {
                MakeHull("boat.dory", MakeVisual("visual.dory_iso"), 6, 14f, 400f),
                MakeHull("boat.fishing_skiff", MakeVisual("visual.fishing_boat"), 6, 13.5f, 450f),
                MakeHull("boat.punt", MakeVisual("visual.punt_iso_basic"), 14, 17f, 700f),
                MakeHull("boat.punt_upgraded", MakeVisual("visual.punt_iso_upgraded"), 14, 17f, 725f),
                MakeHull("boat.console_skiff", MakeVisual("visual.console_skiff"), 20, 18.5f, 1200f),
                MakeHull("boat.sport_skiff", MakeVisual("visual.sport_skiff_single"), 8, 19f, 950f),
                MakeHull("boat.sport_skiff_twin", MakeVisual("visual.sport_skiff_twin"), 8, 19.5f, 1000f),
            };
            var (picker, boat, hold, sr, go) = MakeRig(roster, roster[0]);

            Assert.AreEqual(7, picker.RosterCount, "the punt and her upgrade take the cycle from 5 to 7");
            Assert.AreSame(roster[0], picker.Current, "…starting on the dory the boat is already wearing");

            // A full lap: every rung lands on the RIGHT boat, and all four things move together at each one.
            for (int step = 1; step <= roster.Length; step++)
            {
                picker.Next();
                var expected = roster[step % roster.Length];
                Assert.AreSame(expected, picker.Current, $"step {step} of the cycle");
                AssertShowing(expected, boat, hold, sr, go);
            }

            Assert.AreSame(roster[0], boat.Hull, "the seventh press wraps back to the dory");

            // …and the two punts really are distinct rungs, not one hull shown twice — the mistake this
            // shape invites, since they share a hull, a length and a hold and differ only in the engine.
            Assert.AreNotSame(roster[2], roster[3]);
            Assert.AreNotEqual(roster[2].Id, roster[3].Id,
                "boat.punt and boat.punt_upgraded are separate ids — the cycle must stop at both");
        }

        [Test]
        public void EveryStepOfTheCycle_MovesFeelHoldCameraAndPictureTogether()
        {
            // THE #208 GUARD. Each of these hulls differs in hold, camera AND picture, and one of them is
            // UNSKINNED — so the swap has to tear a compass down and bring the plain renderer back, which is
            // the exact transition that used to leave the old boat's picture on screen.
            var dory = MakeHull("boat.dory", MakeVisual("visual.dory"), 6, 14f, 400f);
            var plain = MakeHull("boat.plain", null, 14, 17f, 700f);          // no facings — the fallback path
            var skiff = MakeHull("boat.console_skiff", MakeVisual("visual.console"), 20, 18.5f, 1200f);
            var roster = new[] { dory, plain, skiff };
            var (picker, boat, hold, sr, go) = MakeRig(roster, dory);
            BoatHullSkinner.ApplyHull(go, sr, dory, boat);   // as the builder leaves it

            // A full lap, plus one more step, so the wrap is covered by the same assertion.
            for (int step = 1; step <= roster.Length + 1; step++)
            {
                picker.Next();
                AssertShowing(roster[step % roster.Length], boat, hold, sr, go);
            }
        }

        [Test]
        public void Show_ReSkinsInPlace_WithoutMovingTheBoat()
        {
            // The whole point of the affordance: same spot, same heading, same water — so two hulls can be
            // compared in the SAME wave. If the swap teleported or re-oriented the boat, the A/B would be
            // worthless.
            var a = MakeHull("boat.a", MakeVisual("visual.a"), 6, 14f, 400f);
            var b = MakeHull("boat.b", MakeVisual("visual.b"), 20, 18.5f, 1200f);
            var (picker, _, _, _, go) = MakeRig(new[] { a, b }, a);

            go.transform.position = new Vector3(12.5f, -7.25f, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, 137f);

            picker.Next();

            Assert.AreEqual(new Vector3(12.5f, -7.25f, 0f), go.transform.position,
                "the re-skin must happen UNDER the player — the boat does not move");
            Assert.AreEqual(137f, go.transform.rotation.eulerAngles.z, 0.01f, "…and does not re-orient");
        }

        [Test]
        public void Show_TracksMass_SoTheNewHullActuallyFeelsDifferent()
        {
            // BoatController.SetHull re-derives the rigidbody mass. Without it, the console would swap its
            // picture and its hold but still shove around like a 400 kg dory — the swap would be cosmetic.
            var dory = MakeHull("boat.dory", MakeVisual("visual.dory"), 6, 14f, 400f);
            var console = MakeHull("boat.console_skiff", MakeVisual("visual.console"), 20, 18.5f, 1200f);
            var (picker, _, _, _, go) = MakeRig(new[] { dory, console }, dory);
            var rb = go.GetComponent<Rigidbody2D>();

            Assert.AreEqual(4f, rb.mass, 0.001f, "precondition: the dory's 400 kg → mass 4");
            picker.Next();
            Assert.AreEqual(12f, rb.mass, 0.001f, "the console's 1200 kg → mass 12: it really is heavier");
        }

        // ---- gating + robustness -------------------------------------------------------------

        [Test]
        public void IsAtHelm_IsFalse_WhenTheBoatIsNotBeingDriven()
        {
            // The gate that keeps F inert ashore. A moored boat's controller is DISABLED (that is how the
            // builder leaves it and how the ControlSwitcher tracks boarding), so the picker must read that
            // rather than re-skinning a boat the player is only standing next to.
            var a = MakeHull("boat.a", MakeVisual("visual.a"), 6, 14f, 400f);
            var b = MakeHull("boat.b", MakeVisual("visual.b"), 20, 18.5f, 1200f);
            var (picker, boat, _, _, _) = MakeRig(new[] { a, b }, a);

            Assert.IsTrue(picker.IsAtHelm, "an enabled controller = someone is driving");
            boat.enabled = false;
            Assert.IsFalse(picker.IsAtHelm, "moored/on-foot → F does nothing");
        }

        [Test]
        public void EmptyRoster_IsInert()
        {
            var a = MakeHull("boat.a", MakeVisual("visual.a"), 6, 14f, 400f);
            var (picker, boat, _, _, _) = MakeRig(System.Array.Empty<BoatHullDef>(), a);

            Assert.DoesNotThrow(() => picker.Next(), "an unwired picker must not throw at the helm");
            Assert.AreSame(a, boat.Hull, "…and must not swap the player out of the boat they're in");
            Assert.IsNull(picker.Current);
            Assert.AreEqual(0, picker.RosterCount);
        }

        [Test]
        public void NullsInTheRoster_AreSkipped_NotSwappedInto()
        {
            // A half-filled array is the likely state after a bad pull (the hull assets are builder-
            // generated). Cycling into a null would null-swap the player into a dead boat.
            var a = MakeHull("boat.a", MakeVisual("visual.a"), 6, 14f, 400f);
            var c = MakeHull("boat.c", MakeVisual("visual.c"), 8, 19f, 950f);
            var (picker, boat, _, _, _) = MakeRig(new BoatHullDef[] { a, null, c }, a);

            picker.Next();
            Assert.AreSame(c, picker.Current, "the null rung is stepped over, not landed on");
            Assert.IsNotNull(boat.Hull);
            picker.Next();
            Assert.AreSame(a, picker.Current, "and the wrap still works around the hole");
        }

        [Test]
        public void AnAllNullRoster_Terminates_RatherThanSpinning()
        {
            var a = MakeHull("boat.a", MakeVisual("visual.a"), 6, 14f, 400f);
            var (picker, boat, _, _, _) = MakeRig(new BoatHullDef[] { null, null, null }, a);

            Assert.DoesNotThrow(() => picker.Next(), "the search must walk at most one lap and give up");
            Assert.AreSame(a, boat.Hull);
        }

        [Test]
        public void AHullOutsideTheRoster_StartsTheCycleAtTheBeginning()
        {
            // If the boat is wearing something the roster doesn't list, the first press should land on entry
            // 0 rather than silently skipping it.
            var stranger = MakeHull("boat.stranger", MakeVisual("visual.stranger"), 6, 14f, 400f);
            var a = MakeHull("boat.a", MakeVisual("visual.a"), 6, 14f, 400f);
            var b = MakeHull("boat.b", MakeVisual("visual.b"), 20, 18.5f, 1200f);
            var (picker, _, _, _, _) = MakeRig(new[] { a, b }, stranger);

            picker.Next();
            Assert.AreSame(a, picker.Current, "the first press lands on the first hull in the roster");
        }

        [Test]
        public void Show_Null_IsANoOp()
        {
            var a = MakeHull("boat.a", MakeVisual("visual.a"), 6, 14f, 400f);
            var (picker, boat, _, _, _) = MakeRig(new[] { a }, a);
            int before = _cameraSignals.Count;

            Assert.DoesNotThrow(() => picker.Show(null));
            Assert.AreSame(a, boat.Hull);
            Assert.AreEqual(before, _cameraSignals.Count, "a no-op swap must not reframe the camera");
        }
    }
}
