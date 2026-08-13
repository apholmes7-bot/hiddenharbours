using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A second walker adds a second trail — it no longer erases the first.</b>
    ///
    /// <para><see cref="GrassFootstep"/> published its 24-point ring buffer into ONE global shader array from
    /// every instance's <c>LateUpdate</c>, so the mechanism supported exactly one walker: put it on six
    /// villagers and each frame's last writer wiped the other five and the player. That is why the village
    /// routines deliberately left the component OFF the villagers. The array is now a POOL of
    /// <see cref="GrassFootstep.MaxWalkers"/> fixed-stride segments and each enabled walker owns one.</para>
    ///
    /// <para>PlayMode on purpose: the slot is claimed in <c>OnEnable</c> and published in <c>LateUpdate</c>, and
    /// EditMode runs neither on <c>AddComponent</c>. The size half of the contract — that both shaders declare
    /// the same segment count and stride the component writes — is pinned headless in
    /// <c>Assets/Tests/EditMode/Art/GrassTrailPoolTests.cs</c>.</para>
    /// </summary>
    public class GrassTrailMultiWalkerPlayTests
    {
        const float Eps = 1e-3f;

        readonly List<GameObject> _spawned = new List<GameObject>();

        static readonly FieldInfo ResetOnDisable =
            typeof(GrassFootstep).GetField("_resetOnDisable", BindingFlags.Instance | BindingFlags.NonPublic);

        [TearDown]
        public void TearDown()
        {
            // SetActive(false) runs OnDisable SYNCHRONOUSLY, which releases the pool slot now. Object.Destroy is
            // deferred to the end of the frame, so relying on it alone would leak slots into the next test and
            // make this fixture order-dependent. Force _resetOnDisable back on first: the hold-the-parting test
            // turns it OFF, and a walker that keeps its slot through teardown would starve the next fixture.
            foreach (var go in _spawned)
            {
                if (go == null) continue;
                var step = go.GetComponent<GrassFootstep>();
                if (step != null) ResetOnDisable.SetValue(step, true);
                go.SetActive(false);
                Object.Destroy(go);
            }
            _spawned.Clear();
        }

        /// <summary>A walker at a position, built INACTIVE so the priority is set before the slot is claimed.</summary>
        GrassFootstep Walker(string name, Vector2 at, int priority = 0)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var step = go.AddComponent<GrassFootstep>();
            step.Priority = priority;
            go.SetActive(true);
            _spawned.Add(go);
            return step;
        }

        static Vector4[] Trail() => Shader.GetGlobalVectorArray("_GrassTrail");
        static Vector4[] Walkers() => Shader.GetGlobalVectorArray("_GrassWalkers");

        /// <summary>Does this slot's OWN segment carry a still-bending (recency &gt; 0) footprint at a place?</summary>
        static bool SegmentHasLivePointAt(Vector4[] trail, int slot, Vector2 expected, float tolerance)
        {
            int b = GrassFootstep.SlotBase(slot);
            for (int i = 0; i < GrassFootstep.PointsPerWalker; i++)
            {
                Vector4 p = trail[b + i];
                if (p.z <= 0f) continue;
                if (Vector2.Distance(new Vector2(p.x, p.y), expected) <= tolerance) return true;
            }
            return false;
        }

        [UnityTest]
        public IEnumerator TwoWalkers_EachKeepTheirOwnTrail()
        {
            // Far enough apart that neither could be mistaken for the other's footprints.
            GrassFootstep a = Walker("WalkerA", new Vector2(0f, 0f));
            GrassFootstep b = Walker("WalkerB", new Vector2(50f, 0f));
            yield return null;

            Assert.GreaterOrEqual(a.Slot, 0, "The first walker must get a pool slot.");
            Assert.GreaterOrEqual(b.Slot, 0, "The second walker must get a pool slot.");
            Assert.AreNotEqual(a.Slot, b.Slot, "Two walkers sharing one slot is the overwrite bug this replaces.");

            Vector4[] trail = Trail();
            Assert.IsNotNull(trail, "Nothing was published to _GrassTrail.");
            Assert.AreEqual(GrassFootstep.TrailPointCount, trail.Length, "The published array is the wrong size.");

            Assert.IsTrue(
                SegmentHasLivePointAt(trail, a.Slot, new Vector2(0f, 0f), Eps),
                "The FIRST walker's trail was erased — this is exactly the last-writer-wins bug: with one shared " +
                "array, six villagers wiped the player's trodden path every frame.");
            Assert.IsTrue(
                SegmentHasLivePointAt(trail, b.Slot, new Vector2(50f, 0f), Eps),
                "The SECOND walker left no trail of its own.");

            Vector4[] walkers = Walkers();
            Assert.AreEqual(GrassFootstep.MaxWalkers, walkers.Length, "One record per pool slot.");
            Assert.GreaterOrEqual(walkers[a.Slot].z, 0f, "A claimed slot must publish a non-negative cull radius.");
            Assert.GreaterOrEqual(walkers[b.Slot].z, 0f, "A claimed slot must publish a non-negative cull radius.");
            Assert.AreEqual(0f, Vector2.Distance(new Vector2(walkers[b.Slot].x, walkers[b.Slot].y),
                                                 new Vector2(50f, 0f)), Eps,
                "The cull circle must be centred on that walker, not on whoever wrote last.");
        }

        [UnityTest]
        public IEnumerator AWalkerLeaving_ClearsOnlyItsOwnSegmentAndFreesTheSlot()
        {
            GrassFootstep a = Walker("Leaver", new Vector2(0f, 0f));
            GrassFootstep b = Walker("Stayer", new Vector2(50f, 0f));
            yield return null;

            int freed = a.Slot;
            int kept = b.Slot;
            a.gameObject.SetActive(false);          // OnDisable: release + republish

            Vector4[] trail = Trail();
            for (int i = 0; i < GrassFootstep.PointsPerWalker; i++)
                Assert.AreEqual(
                    0f, trail[GrassFootstep.SlotBase(freed) + i].z, Eps,
                    "A departed walker's footprints must stop bending the grass immediately.");
            Assert.Less(
                Walkers()[freed].z, 0f,
                "A released slot must publish a NEGATIVE cull radius so the shaders skip it outright.");
            Assert.IsTrue(
                SegmentHasLivePointAt(trail, kept, new Vector2(50f, 0f), Eps),
                "Releasing one walker must not disturb another's trail.");
            Assert.AreEqual(-1, a.Slot, "A disabled walker holds no slot.");

            GrassFootstep c = Walker("Newcomer", new Vector2(-20f, 7f));
            yield return null;
            Assert.AreEqual(freed, c.Slot, "The freed slot must be reusable — otherwise the pool leaks.");
            Assert.IsTrue(SegmentHasLivePointAt(Trail(), c.Slot, new Vector2(-20f, 7f), Eps));
        }

        /// <summary>
        /// Spawn ambient (priority 0) walkers until one is turned away, and return the ones that DID get a slot.
        /// Deliberately not "spawn exactly MaxWalkers": another PlayMode fixture's persistent-core player is a
        /// DontDestroyOnLoad walker that may still hold a slot, and this must not depend on whether it does.
        /// </summary>
        List<GrassFootstep> FillPool(out GrassFootstep denied)
        {
            var held = new List<GrassFootstep>();
            denied = null;
            for (int i = 0; i <= GrassFootstep.MaxWalkers && denied == null; i++)
            {
                GrassFootstep w = Walker($"Villager{i}", new Vector2(i * 4f, 0f));
                if (w.Slot < 0) denied = w; else held.Add(w);
            }
            Assert.IsNotNull(
                denied,
                $"Spawning {GrassFootstep.MaxWalkers + 1} walkers into a {GrassFootstep.MaxWalkers}-slot pool " +
                "must turn one away — otherwise slots are being handed out twice.");
            return held;
        }

        [UnityTest]
        public IEnumerator AFullPool_DropsTheNewcomersTrail_WithoutDisturbingEqualRankedWalkers()
        {
            GrassFootstep denied;
            List<GrassFootstep> held = FillPool(out denied);
            denied.transform.position = new Vector3(200f, 0f, 0f);
            yield return null;

            Assert.AreEqual(
                -1, denied.Slot,
                "Equal-ranked walkers must never evict each other — a full pool simply leaves the newest one " +
                "without a trail (cosmetic), rather than churning somebody else's path away.");
            foreach (GrassFootstep h in held)
                Assert.GreaterOrEqual(h.Slot, 0, "An equal-ranked holder must keep its slot.");

            // And the one with no slot writes nothing at all — no stray footprint in somebody else's segment.
            Vector4[] trail = Trail();
            for (int slot = 0; slot < GrassFootstep.MaxWalkers; slot++)
                Assert.IsFalse(
                    SegmentHasLivePointAt(trail, slot, new Vector2(200f, 0f), Eps),
                    "A walker without a slot must not write into the pool at all.");
        }

        [UnityTest]
        public IEnumerator AWalkerThatHoldsItsPartingAcrossADisable_DoesNotConsumeASecondSlot()
        {
            // _resetOnDisable = false means "keep the last path so the grass holds its parting", which in a
            // POOL also means keeping the slot. Re-enabling must reuse that slot: claiming a second one would
            // leak the first with its footprints frozen at full strength forever.
            GrassFootstep holder = Walker("Holder", new Vector2(0f, 0f));
            ResetOnDisable.SetValue(holder, false);   // TearDown puts it back, however this test ends
            yield return null;

            int slot = holder.Slot;
            Assert.GreaterOrEqual(slot, 0);

            holder.gameObject.SetActive(false);
            Assert.AreEqual(slot, holder.Slot, "Holding the parting means holding the slot it lives in.");
            Assert.IsTrue(
                SegmentHasLivePointAt(Trail(), slot, new Vector2(0f, 0f), Eps),
                "With _resetOnDisable off the footprints must survive the disable.");
            Assert.AreEqual(0f, Walkers()[slot].w, Eps,
                "A departed walker must not leave the directional gate engaged on a stale heading.");

            holder.gameObject.SetActive(true);
            yield return null;
            Assert.AreEqual(slot, holder.Slot, "Re-enabling must REUSE the held slot, never claim a second.");

            // And nobody else is shadow-holding the one it started with: a fresh walker gets a different slot.
            GrassFootstep other = Walker("Other", new Vector2(50f, 0f));
            yield return null;
            Assert.AreNotEqual(slot, other.Slot, "The held slot must still be the holder's.");
        }

        [UnityTest]
        public IEnumerator ThePlayer_OutranksAVillageAndTakesASlot()
        {
            // The failure this guards: the persistent-core root toggles on a region hop, so the PLAYER's
            // GrassFootstep re-claims AFTER the arriving region's villagers have filled the pool.
            GrassFootstep unused;
            List<GrassFootstep> villagers = FillPool(out unused);
            yield return null;

            GrassFootstep player = Walker("Player", new Vector2(-99f, 3f), GrassFootstep.PlayerPriority);
            yield return null;

            Assert.GreaterOrEqual(
                player.Slot, 0,
                "The player must never be the one left without a trodden path — it claims at PlayerPriority, " +
                "which evicts the lowest-ranked holder.");
            Assert.IsTrue(SegmentHasLivePointAt(Trail(), player.Slot, new Vector2(-99f, 3f), Eps));

            int evicted = 0;
            foreach (GrassFootstep v in villagers) if (v.Slot < 0) evicted++;
            Assert.AreEqual(1, evicted, "Exactly one villager yields its slot — not none, and not several.");
        }
    }
}
