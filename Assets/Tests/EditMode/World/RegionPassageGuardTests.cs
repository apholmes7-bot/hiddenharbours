using NUnit.Framework;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// The <see cref="RegionPassage"/> re-fire guard (the helm-drop fix). A passage must take a crossing
    /// exactly ONCE per genuine entry and NEVER re-fire on the boat that just arrived — because every fire
    /// re-runs travel, which teleports + Stop()s the boat and re-binds control, dropping the helm for a beat
    /// (the owner's "controls cut out crossing the boundary"). These guard the pure decision the trigger
    /// callbacks lean on (<see cref="RegionPassage.ShouldFire"/>): the leave-then-enter latch + the cooldown
    /// debounce. The collider plumbing / scene-toggle is play-mode glue; this is the invariant.
    /// </summary>
    public class RegionPassageGuardTests
    {
        const float Cooldown = 1.5f;

        [Test]
        public void ShouldFire_GenuineEntry_Primed_AndPastCooldown_Fires()
        {
            // Primed (not consumed), and the entry is well past any prior fire → a real crossing fires.
            Assert.IsTrue(RegionPassage.ShouldFire(consumed: false, now: 100f, lastActivateTime: 0f, Cooldown),
                "a primed passage entered long after the last fire takes the crossing");
        }

        [Test]
        public void ShouldFire_WhileConsumed_DoesNotFire_EvenPastCooldown()
        {
            // The latch is the primary guard: once fired it stays consumed until the body LEAVES (resets it),
            // so the just-arrived boat sitting in the band can't re-fire — regardless of elapsed time.
            Assert.IsFalse(RegionPassage.ShouldFire(consumed: true, now: 100f, lastActivateTime: 0f, Cooldown),
                "a consumed passage ignores entries until the boat leaves and re-arms it");
        }

        [Test]
        public void ShouldFire_Primed_ButWithinCooldown_DoesNotFire()
        {
            // The debounce backstop: even if something re-arms the latch immediately, an entry inside the
            // cooldown window after the last fire is swallowed (the scene-toggle bounce lands here in time).
            Assert.IsFalse(RegionPassage.ShouldFire(consumed: false, now: 0.5f, lastActivateTime: 0f, Cooldown),
                "a re-entry within the cooldown window after a fire is debounced");
        }

        [Test]
        public void ShouldFire_Primed_ExactlyAtCooldownBoundary_Fires()
        {
            // At exactly the cooldown boundary the debounce releases (>= window) — no off-by-one dead zone.
            Assert.IsTrue(RegionPassage.ShouldFire(consumed: false, now: Cooldown, lastActivateTime: 0f, Cooldown),
                "the cooldown releases at exactly the window length");
        }

        [Test]
        public void ShouldFire_SameRegionLingerBounce_IsSwallowed_ByTheLatch()
        {
            // The source passage's same-region case: the boat crosses → fires (latched consumed), then
            // lingers in / nudges back into the wide band. The latch (consumed) swallows every re-entry until
            // a real exit re-arms it — even once well past the cooldown.
            float fireTime = 10f;
            Assert.IsTrue(RegionPassage.ShouldFire(consumed: false, now: fireTime, lastActivateTime: 0f, Cooldown),
                "the crossing itself fires");
            Assert.IsFalse(RegionPassage.ShouldFire(consumed: true, now: fireTime + 100f, fireTime, Cooldown),
                "while latched (no exit yet) a re-entry never re-fires the passage — no second travel, no helm drop");
            // Boat genuinely leaves (OnTriggerExit2D → consumed=false) and crosses again later:
            Assert.IsTrue(RegionPassage.ShouldFire(consumed: false, now: fireTime + 200f, fireTime, Cooldown),
                "a genuine later crossing still works");
        }

        [Test]
        public void ShouldFire_SceneToggleBounce_IsSwallowed_ByTheFreshEnableCooldown()
        {
            // The re-activated region's case: its passage's OnEnable runs and stamps lastActivateTime = enable
            // time while PRIMED (consumed=false). Unity then re-raises trigger-enter on the still-overlapping
            // just-arrived boat on the very next physics step — landing inside the fresh cooldown window, so
            // it's debounced even though the passage is primed. A genuine approach seconds later still fires.
            float enableTime = 50f;
            Assert.IsFalse(RegionPassage.ShouldFire(consumed: false, now: enableTime + 0.02f, enableTime, Cooldown),
                "the immediate post-activation re-enter on the just-arrived boat is debounced (no helm drop)");
            Assert.IsTrue(RegionPassage.ShouldFire(consumed: false, now: enableTime + 100f, enableTime, Cooldown),
                "a genuine crossing well after activation still fires");
        }
    }
}
