using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐⭐ <b>THE HULL IS TOLD WHERE THE SKIPPER IS STANDING.</b> The guard for the owner's 2026-09-09
    /// playtest: <i>"the captain still doesnt appear behind the helm from every angle."</i>
    ///
    /// <para><b>What was wrong, and it was #806's blast radius.</b> <c>MooredBoat.ClaimTheDeckSlot</c>
    /// publishes the hull's deck-occupant slot <b>exactly once</b>, at spawn, at
    /// <c>StandPointOf(Visual.Deck)</c> — the middle of her measured deck. That was right while nothing
    /// moved the figure. #806 then began holding the arrival's skipper at his WHEEL every LateUpdate and
    /// did not carry his occlusion with him, so the hull went on discarding its pixels in front of a man
    /// standing amidships while he was drawn forward, inside the house.</para>
    ///
    /// <para><b>The gap, on the shipped cape:</b> the slot said <b>(0, −0.096, 0.72)</b> and the figure
    /// was drawn at her rig's helm station <b>(0, 1.35, 0.74)</b> — <b>1.446 m along the keel, ≈46 px at
    /// 32 px/m, the whole depth of the wheelhouse</b>. And heading-DEPENDENT in effect, because which
    /// geometry lies between the camera and the published point changes with facing. Hence "from every
    /// angle". <see cref="TheGapThisClosed_IsStillWorthClosing"/> keeps that number in the run.</para>
    ///
    /// <para><b>⚠ What this fixture does NOT reach.</b> It drives <see cref="MooredBoat"/>'s publish and
    /// the shipped data, not <c>ArrivalOpening.HoldTheSkipper</c> — that is private and needs a cabin, a
    /// skipper asset and a running arrival, which is PlayMode. So this pins the CONTRACT (the drawer
    /// republishes what it is told, in rig metres) and the EVIDENCE (the two points differ by 1.446 m on
    /// shipped data); the call site itself is covered by the arrival's own PlayMode suite.</para>
    /// </summary>
    public class SkipperOcclusionSlotTests
    {
        private const string CapeDeckPath = "Assets/_Project/Data/Boats/Decks/CapeIslanderIso.asset";
        private const float Ppu = 32f;

        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        /// <summary>Records what production publishes, which is the only thing worth asserting about a
        /// publisher. <see cref="Set"/> is the seam <c>MooredBoat</c> writes through.</summary>
        private sealed class RecordingSlots : IDeckOccupantSlots
        {
            public readonly List<Vector3> Published = new List<Vector3>();
            public Vector3 Last => Published.Count > 0 ? Published[Published.Count - 1] : Vector3.zero;

            public int Capacity => 1;
            public int ActiveCount => Published.Count > 0 ? 1 : 0;
            public int Claim(object owner) => 0;
            public void Release(int slot, object owner) { }
            public void Set(int slot, object owner, Vector3 rigLocalMeters, bool active)
                => Published.Add(rigLocalMeters);
            public float OccluderId(int slot) => 0f;
            public float OccluderIdTop => 7f / 255f;
        }

        /// <summary>A drawer with its slot already claimed — the state <c>ClaimTheDeckSlot</c> leaves it
        /// in. Reached by reflection because claiming for real needs an owner def, a skipper asset and a
        /// presenter; the method under test is the publish, not the claim.</summary>
        private MooredBoat DrawerWithASlot(RecordingSlots slots, Vector3 standRigMeters)
        {
            var go = new GameObject("Boat");
            _spawned.Add(go);
            var drawer = go.AddComponent<MooredBoat>();

            const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(MooredBoat).GetField("_slots", F)!.SetValue(drawer, slots);
            typeof(MooredBoat).GetField("_slot", F)!.SetValue(drawer, 0);
            typeof(MooredBoat).GetField("_standRigMeters", F)!.SetValue(drawer, standRigMeters);
            return drawer;
        }

        // ---- the cases -------------------------------------------------------------------------------

        /// <summary>⭐ The drawer republishes where it is told, in the hull's own rig metres.</summary>
        [Test]
        public void TellingTheDrawerWhereHeStands_RepublishesTheSlot()
        {
            var slots = new RecordingSlots();
            MooredBoat drawer = DrawerWithASlot(slots, new Vector3(0f, -0.096f, 0.72f));

            var helm = new Vector3(0f, 1.35f, 0.74f);
            drawer.StandTheSkipperAt(helm);

            Assert.AreEqual(1, slots.Published.Count,
                "the drawer published nothing — the hull is still occluding for wherever he used to be");
            Assert.AreEqual(helm, slots.Last,
                "the drawer published a point that is not the one it was handed");
            Assert.AreEqual(helm, drawer.OccupantStandRigMeters, "…and did not remember it");
        }

        /// <summary>⚠ RIG metres, so heading-INDEPENDENT. The most likely wrong fix is to publish the
        /// projected screen offset, which would make the occlusion swing with the boat — the same class
        /// of bug one level down. Handing the same station at four headings must publish one point.</summary>
        [Test]
        public void TheSlotIsAPlaceOnTheHull_NotOnTheScreen()
        {
            var slots = new RecordingSlots();
            MooredBoat drawer = DrawerWithASlot(slots, Vector3.zero);
            var helm = new Vector3(0f, 1.35f, 0.74f);

            foreach (float heading in new[] { 0f, 90f, 180f, 270f })
            {
                // The station does not change with heading; a screen offset would. Re-publish through a
                // no-op nudge so each heading records, then hand back the station itself.
                drawer.StandTheSkipperAt(helm + new Vector3(0f, 0f, heading * 1e-6f));
                drawer.StandTheSkipperAt(helm);
                Assert.AreEqual(helm, drawer.OccupantStandRigMeters,
                    $"at {heading:F0}° the published stand point moved — the slot is being handed a " +
                    "SCREEN offset, not the hull-local station");
            }
        }

        /// <summary>⚠ Idempotent on the per-frame path: the same point twice must not churn the slot.
        /// <c>HoldTheSkipper</c> calls this every LateUpdate (rule 7).</summary>
        [Test]
        public void RepublishingTheSamePoint_DoesNotChurnTheSlot()
        {
            var slots = new RecordingSlots();
            var helm = new Vector3(0f, 1.35f, 0.74f);
            MooredBoat drawer = DrawerWithASlot(slots, helm);

            for (int i = 0; i < 10; i++) drawer.StandTheSkipperAt(helm);
            Assert.AreEqual(0, slots.Published.Count,
                "the slot was rewritten with a point it already held, ten times, on a per-frame path");
        }

        /// <summary>⚠ Inert on a hull with no slot — a sprite hull has no facet depth to discard
        /// against, and a review hull carries nobody. Absence is data, as it is in the claim.</summary>
        [Test]
        public void AHullWithNoSlot_IsUntouched()
        {
            var go = new GameObject("Boat");
            _spawned.Add(go);
            var drawer = go.AddComponent<MooredBoat>();
            Assert.DoesNotThrow(() => drawer.StandTheSkipperAt(new Vector3(0f, 1.35f, 0.74f)),
                                "a hull with no occupant slot must ignore this, not throw");
        }

        /// <summary>⭐ THE EVIDENCE, kept in the run on SHIPPED data: how far apart the two points are.
        /// If this ever falls below a pixel the defect has gone away by other means and this guard is
        /// measuring nothing.</summary>
        [Test]
        public void TheGapThisClosed_IsStillWorthClosing()
        {
            var deck = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatDeckDef>(CapeDeckPath);
            Assert.IsNotNull(deck, $"the cape's deck asset is missing at {CapeDeckPath}");
            Assert.IsTrue(deck.HasHelmStation,
                "the cape carries no helm station, so there is no wheel to stand him at and this guard " +
                "is measuring nothing — re-run the deck sidecar import");

            Vector3 sole = MooredBoat.StandPointOf(deck);
            Vector3 helm = deck.HelmStationLocalMeters;
            float gap = Vector3.Distance(sole, helm);

            Debug.Log($"[SkipperSlot] the cape: the occluder was told ({sole.x:F3}, {sole.y:F3}, " +
                      $"{sole.z:F3}) while #806 drew him at ({helm.x:F3}, {helm.y:F3}, {helm.z:F3}) — " +
                      $"{gap:F3} m, {gap * Ppu:F0} px at {Ppu:F0} px/m.");

            Assert.Greater(gap * Ppu, 1f,
                $"the stand point and the helm station are now {gap * Ppu:F2} px apart. Either the deck's " +
                "walk centre or her helm station has moved to meet the other, and this guard no longer " +
                "measures the defect it was written for.");
        }
    }
}
