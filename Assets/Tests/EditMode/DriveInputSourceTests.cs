using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE DRIVE-INPUT SEAM</b> (driveable charter, PR 0) — <see cref="IDriveInputSource"/>, the
    /// keyboard behind it, and the held source a scripted driver uses.
    ///
    /// <para><b>What is pinned here is the SENSE of the shipped keyboard read</b>, because the seam's
    /// whole promise is that moving the read behind an interface changed nothing a player can feel:
    /// W/S is the throttle, A/D the wheel, Space the brake, opposing keys cancel, and LEFT is +1 — the
    /// rig's own steering sense, so the A key, the drawn wheels and the yaw agree with no sign flip.
    /// A flipped sign here would turn the drawn wheels against the machine's yaw, which is precisely
    /// the disagreement ADR 0035 §5 exists to prevent.</para>
    ///
    /// <para>What is NOT here: that the switcher reads the source every frame and hands the demand to
    /// the seat. That is a claim about <c>Update</c>, and <c>RoadFleetJourneyPlayTests</c> makes it
    /// under a running frame pump — where the trap this seam retires was measured.</para>
    /// </summary>
    public class DriveInputSourceTests
    {
        [Test]
        public void TheKeyboardMapIsTheRigsOwnSense()
        {
            DriveDemand ahead = KeyboardDriveInputSource.Map(true, false, false, false, false);
            Assert.That(ahead.Throttle, Is.EqualTo(1f), "W is full ahead.");
            Assert.That(ahead.Steer, Is.EqualTo(0f));
            Assert.That(ahead.Brake, Is.False);

            DriveDemand astern = KeyboardDriveInputSource.Map(false, true, false, false, false);
            Assert.That(astern.Throttle, Is.EqualTo(-1f), "S is full astern — reverse, not a brake.");

            DriveDemand left = KeyboardDriveInputSource.Map(false, false, true, false, false);
            Assert.That(left.Steer, Is.EqualTo(1f),
                "LEFT is +1: the rig's +1 is full LEFT lock, and the drawn wheels, the yaw and the A " +
                "key must all agree on that sign.");

            DriveDemand right = KeyboardDriveInputSource.Map(false, false, false, true, false);
            Assert.That(right.Steer, Is.EqualTo(-1f), "D is full right.");

            DriveDemand brake = KeyboardDriveInputSource.Map(false, false, false, false, true);
            Assert.That(brake.Brake, Is.True, "Space is the brake.");
            Assert.That(brake.Throttle, Is.EqualTo(0f), "and the brake alone asks no throttle.");
        }

        [Test]
        public void OpposingKeysCancelAsTheyAlwaysDid()
        {
            DriveDemand both = KeyboardDriveInputSource.Map(true, true, true, true, false);
            Assert.That(both.Throttle, Is.EqualTo(0f), "W and S together are no throttle.");
            Assert.That(both.Steer, Is.EqualTo(0f), "A and D together are a centred wheel.");

            DriveDemand all = KeyboardDriveInputSource.Map(true, true, true, true, true);
            Assert.That(all.Brake, Is.True, "and the brake still reads through a cancelled pair.");
        }

        [Test]
        public void NoKeyHeldIsNoDemand()
        {
            DriveDemand none = KeyboardDriveInputSource.Map(false, false, false, false, false);
            Assert.That(none.Throttle, Is.EqualTo(0f));
            Assert.That(none.Steer, Is.EqualTo(0f));
            Assert.That(none.Brake, Is.False);

            // The live read on this box: nobody is holding a key in a test run, and a box with no
            // keyboard device answers the same — which is the whole of "no device is no key held".
            DriveDemand live = new KeyboardDriveInputSource().Read();
            Assert.That(live.Throttle, Is.EqualTo(0f), "the keyboard read a throttle with no key held.");
            Assert.That(live.Steer, Is.EqualTo(0f), "the keyboard read a wheel with no key held.");
            Assert.That(live.Brake, Is.False, "the keyboard read a brake with no key held.");
        }

        [Test]
        public void AHeldDemandOutlivesTheFrameItWasSetIn()
        {
            var held = new HeldDriveInput();
            Assert.That(held.Reads, Is.EqualTo(0));

            held.Set(0.75f, -0.5f, false);
            for (int frame = 0; frame < 3; frame++)
            {
                DriveDemand d = held.Read();
                Assert.That(d.Throttle, Is.EqualTo(0.75f), $"frame {frame}: the throttle did not hold.");
                Assert.That(d.Steer, Is.EqualTo(-0.5f), $"frame {frame}: the wheel did not hold.");
                Assert.That(d.Brake, Is.False);
            }
            Assert.That(held.Reads, Is.EqualTo(3), "the source did not count the frames it answered.");

            held.Release();
            DriveDemand released = held.Read();
            Assert.That(released.Throttle, Is.EqualTo(0f), "released, and still asking for throttle.");
            Assert.That(released.Steer, Is.EqualTo(0f));
            Assert.That(released.Brake, Is.False);
            Assert.That(held.Reads, Is.EqualTo(4));
        }

        [Test]
        public void TheSwitcherReadsTheKeyboardUntilHandedAnotherSource()
        {
            var go = new GameObject("switcher");
            try
            {
                var switcher = go.AddComponent<ControlSwitcher>();

                Assert.That(switcher.DriveInputSource, Is.InstanceOf<KeyboardDriveInputSource>(),
                    "with nothing configured the wheel must be the greybox keyboard — the read the " +
                    "seam replaced, not silence.");

                var held = new HeldDriveInput();
                switcher.ConfigureDriveInput(held);
                Assert.That(switcher.DriveInputSource, Is.SameAs(held),
                    "a configured source is not the one the switcher exposes.");

                switcher.ConfigureDriveInput(null);
                Assert.That(switcher.DriveInputSource, Is.InstanceOf<KeyboardDriveInputSource>(),
                    "null did not restore the keyboard.");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
