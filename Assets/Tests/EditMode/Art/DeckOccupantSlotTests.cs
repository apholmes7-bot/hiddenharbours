using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE DECK-OCCUPANT SLOTS</b> — the seam that unblocked the stern-deck loop (#474 stopped on
    /// it deliberately). #461 shipped per-pixel hull occlusion for exactly ONE occupant: one depth,
    /// one split plane, two ids. A trap stack on the stern and a fisher amidships sit at different
    /// depths, and one plane cannot be right for both — the hull geometry BETWEEN them is "in front"
    /// of the far one and "behind" the near one, and a single id cannot say both things.
    ///
    /// <para>This file holds everything about the generalisation that does <b>not</b> need a GPU: the
    /// partition theorem itself, the claim/release protocol, and the two source-level guards that
    /// keep the C# and the HLSL from drifting. The pixels are proved in
    /// <c>IsoFacetUrpPassTests</c>, which needs a real graphics device and says so loudly when it
    /// has not got one.</para>
    /// </summary>
    public class DeckOccupantSlotTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private IsoFacetHullRenderer NewHull(string name = "SlotTestHull")
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go.AddComponent<IsoFacetHullRenderer>();
        }

        // ---- the partition theorem ------------------------------------------------------------------

        /// <summary>
        /// <b>THE THEOREM THE WHOLE ENCODING RESTS ON.</b> For any hull pixel at depth <c>z</c> and
        /// any occupant <c>k</c>: <c>Band(z) &gt;= RankOf(depth[k])</c> <i>if and only if</i>
        /// <c>z &lt; depth[k]</c>.
        ///
        /// <para>The left side is what the hardware actually does — the facet shader counts a band
        /// per pixel, the sprite shader tests a range from the occupant's rank upward. The right side
        /// is what "the boat is in front of me" MEANS. If they ever part company the picture is
        /// wrong, and it is the kind of wrong that looks like a shader bug: a pot showing through a
        /// wheelhouse, or a fisher with a boat-shaped hole punched in her over open water.</para>
        ///
        /// <para>Swept over pseudo-random configurations with DELIBERATE TIES (depths are quantised
        /// to quarter-metres so equal depths recur), because ties are where a rank rule written with
        /// the wrong comparison — <c>&gt;</c> instead of <c>&gt;=</c> — comes apart, and two things
        /// standing at one depth on a deck is not exotic: it is a stack and the hand working it.</para>
        /// </summary>
        [Test]
        public void ThePartition_AnswersExactlyWhetherTheHullIsInFrontOfThatOccupant()
        {
            var depths = new float[IsoFacetHullRenderer.DeckOccupantSlots];
            int rng = 12345;
            float Next()
            {
                rng = (rng * 1103515245 + 12345) & 0x7fffffff;
                return rng / (float)0x7fffffff;
            }

            int checks = 0;
            for (int trial = 0; trial < 4000; trial++)
            {
                int n = 1 + (int)(Next() * IsoFacetHullRenderer.DeckOccupantSlots);
                if (n > depths.Length) n = depths.Length;
                for (int i = 0; i < n; i++) depths[i] = Mathf.Round((Next() * 6f - 3f) * 4f) / 4f;

                for (int t = 0; t < 8; t++)
                {
                    float z = Mathf.Round((Next() * 8f - 4f) * 4f) / 4f;
                    int band = DeckOccupantPartition.Band(z, depths, n);

                    for (int k = 0; k < n; k++)
                    {
                        int rank = DeckOccupantPartition.RankOf(depths[k], depths, n);
                        Assert.GreaterOrEqual(rank, 1,
                            "an occupant who is standing there counts themselves — rank 0 would put " +
                            "their low id below the hull's whole fore block");
                        checks++;
                        Assert.AreEqual(z < depths[k], band >= rank,
                            $"the partition disagrees with the geometry: pixel z={z}, occupant " +
                            $"depth={depths[k]}, band={band}, rank={rank}. The hull is " +
                            $"{(z < depths[k] ? "" : "NOT ")}in front of them, and the shader would " +
                            $"{(band >= rank ? "" : "NOT ")}hide them.");
                    }
                }
            }
            Assert.Greater(checks, 50000, "harness: the sweep must actually have swept");
        }

        /// <summary>
        /// <b>THE ONE-SLOT A/B, in id space.</b> With exactly one occupant the generalised rule must
        /// reproduce the shipped #461 rule exactly — band 0 is the hull's own id, band 1 is the first
        /// fore id, and there is no third possibility. This is the arithmetic half of the pixel A/B
        /// in <c>IsoFacetUrpPassTests</c>; it fails faster and says why.
        /// </summary>
        [Test]
        public void OneOccupant_ReproducesTheShippedSinglePlaneSplit_Exactly()
        {
            var depths = new float[IsoFacetHullRenderer.DeckOccupantSlots];
            for (float occupant = -2f; occupant <= 2f; occupant += 0.25f)
            {
                depths[0] = occupant;
                int rank = DeckOccupantPartition.RankOf(occupant, depths, 1);
                Assert.AreEqual(1, rank, "the only occupant aboard is always rank 1");

                for (float z = -3f; z <= 3f; z += 0.125f)
                {
                    int band = DeckOccupantPartition.Band(z, depths, 1);
                    Assert.AreEqual(z < occupant ? 1 : 0, band,
                        $"with one occupant the band is the shipped boolean split: z={z}, " +
                        $"occupant={occupant}");
                    Assert.AreEqual(z < occupant, band >= rank,
                        "…and the sprite hides exactly where #461 hid it");
                }
            }
        }

        // ---- the claim protocol ---------------------------------------------------------------------

        [Test]
        public void AClaim_IsHeldByItsOwner_AndReClaimingReturnsTheSameSlot()
        {
            var hull = NewHull();
            var rider = new object();

            int slot = hull.DeckOccupants.Claim(rider);
            Assert.GreaterOrEqual(slot, 0, "the first claim on an empty hull must succeed");
            Assert.AreEqual(slot, hull.DeckOccupants.Claim(rider),
                "re-claiming must hand back the SAME slot. An owner that lost its index — a domain " +
                "reload, a re-wire — would otherwise leak a slot per recovery until the deck is full.");
            Assert.AreEqual(IsoFacetHullRenderer.DeckOccupantSlots, hull.DeckOccupants.Capacity);
        }

        [Test]
        public void AStaleOwner_CanNeitherWriteNorReleaseSomebodyElsesSlot()
        {
            var hull = NewHull();
            var mine = new object();
            var stranger = new object();

            int slot = hull.DeckOccupants.Claim(mine);
            hull.DeckOccupants.Set(slot, mine, new Vector3(0f, -1.6f, 1.35f), true);
            Assert.AreEqual(1, hull.DeckOccupants.ActiveCount);

            hull.DeckOccupants.Set(slot, stranger, Vector3.zero, false);
            Assert.AreEqual(1, hull.DeckOccupants.ActiveCount,
                "a stranger must not be able to stand somebody else down — the owner token is the " +
                "whole guard against a stale holder moving a live occupant's occlusion");

            hull.DeckOccupants.Release(slot, stranger);
            Assert.AreEqual(1, hull.DeckOccupants.ActiveCount,
                "…nor to evict them");

            hull.DeckOccupants.Release(slot, mine);
            Assert.AreEqual(0, hull.DeckOccupants.ActiveCount, "the owner may release their own");
            Assert.AreEqual(0f, hull.DeckOccupants.OccluderId(slot), 0f,
                "a released slot hides nobody — 0 is the shader's inert value");
        }

        /// <summary>
        /// <b>OVERFLOW IS REFUSED, AND LOUDLY.</b> First claim wins: the occupants already standing
        /// keep their slots and their ids, and the surplus one is told NO with a warning naming the
        /// hull. The refused claimant draws un-occluded — the picture that shipped before any of this
        /// existed — and never half-occluded, because -1 means it never writes an id at all.
        ///
        /// <para>A silent drop here would be the worst possible failure: a pot drawing through a
        /// wheelhouse, indistinguishable from the shader bug this whole seam exists to fix.</para>
        /// </summary>
        [Test]
        public void ClaimingBeyondCapacity_IsRefusedLoudly_AndTheEarlierClaimsAreUntouched()
        {
            var hull = NewHull("Overflowing");
            int capacity = IsoFacetHullRenderer.DeckOccupantSlots;

            var owners = new object[capacity];
            var slots = new int[capacity];
            for (int i = 0; i < capacity; i++)
            {
                owners[i] = new object();
                slots[i] = hull.DeckOccupants.Claim(owners[i]);
                Assert.GreaterOrEqual(slots[i], 0, $"claim {i} of {capacity} must fit");
                hull.DeckOccupants.Set(slots[i], owners[i], new Vector3(0f, i * 0.2f, 1f), true);
            }
            Assert.AreEqual(capacity, hull.DeckOccupants.ActiveCount);

            var ids = new float[capacity];
            for (int i = 0; i < capacity; i++) ids[i] = hull.DeckOccupants.OccluderId(slots[i]);

            LogAssert.Expect(LogType.Warning, new Regex("deck-occupant slots"));
            int refused = hull.DeckOccupants.Claim(new object());

            Assert.AreEqual(-1, refused,
                "the surplus claim must be REFUSED, not squeezed in. -1 is what keeps it from " +
                "writing an id, which is what keeps it from drawing half-occluded.");
            Assert.AreEqual(capacity, hull.DeckOccupants.ActiveCount,
                "…and nobody already aboard may be evicted to make room (first claim wins)");
            for (int i = 0; i < capacity; i++)
                Assert.AreEqual(ids[i], hull.DeckOccupants.OccluderId(slots[i]), 1e-6f,
                    $"occupant {i}'s id moved when a later claim was refused — a refusal must not " +
                    "be able to change what anyone else is drawing with");
        }

        [Test]
        public void ASlotReleased_IsHandedToTheNextClaimant()
        {
            var hull = NewHull();
            int capacity = IsoFacetHullRenderer.DeckOccupantSlots;
            var owners = new object[capacity];
            var slots = new int[capacity];
            for (int i = 0; i < capacity; i++)
            {
                owners[i] = new object();
                slots[i] = hull.DeckOccupants.Claim(owners[i]);
            }

            hull.DeckOccupants.Release(slots[3], owners[3]);
            int reused = hull.DeckOccupants.Claim(new object());
            Assert.AreEqual(slots[3], reused,
                "a released slot must come back into circulation, or twelve boardings would exhaust " +
                "a hull the player never left");
        }

        /// <summary>
        /// <b>A HULL GOING AWAY CARRIES HER OCCUPANTS WITH HER.</b> Disabling her hands her id block
        /// back to the pool, so nothing may keep discarding against ids another boat is about to be
        /// issued — and every slot must empty with it.
        ///
        /// <para>The other half of that is the claimant's: because the claim is re-asserted every
        /// frame and <see cref="IDeckOccupantSlots.Claim"/> is idempotent, a rider standing on a hull
        /// that is disabled and re-enabled simply gets a slot again. Holding an index across that
        /// gap is the stale-slot bug this pair of behaviours exists to make impossible.</para>
        /// </summary>
        [Test]
        public void AHullDisabled_EmptiesEverySlot_AndReclaimingWorksAfterwards()
        {
            var hull = NewHull();
            var rider = new object();
            int slot = hull.DeckOccupants.Claim(rider);
            hull.DeckOccupants.Set(slot, rider, new Vector3(0f, -1.6f, 1.35f), true);
            Assert.AreEqual(1, hull.DeckOccupants.ActiveCount);

            hull.enabled = false;
            Assert.AreEqual(0, hull.DeckOccupants.ActiveCount,
                "a hull that has gone hides nobody — a stranded occupant would keep her splitting an " +
                "image nobody is standing on");
            Assert.AreEqual(0f, hull.DeckOccupants.OccluderIdTop, 0f,
                "…and her id block went back to the pool with her");

            hull.enabled = true;
            int again = hull.DeckOccupants.Claim(rider);
            Assert.GreaterOrEqual(again, 0,
                "the claimant must be able to take a slot again — the protocol re-asserts the claim " +
                "every frame precisely so this heals itself");
            hull.DeckOccupants.Set(again, rider, new Vector3(0f, -1.6f, 1.35f), true);
            Assert.AreEqual(1, hull.DeckOccupants.ActiveCount);
            Assert.Greater(hull.DeckOccupants.OccluderIdTop, 0f,
                "and she holds a fresh id block, so there is something to hide behind again");
        }

        [Test]
        public void ASpriteHull_RefusesEveryClaimAndHidesNobody()
        {
            IDeckOccupantSlots none = NoDeckOccupantSlots.Instance;
            Assert.AreEqual(-1, none.Claim(new object()),
                "a flat sheet has no depth to split — there is nothing honest to answer with");
            Assert.AreEqual(0, none.Capacity);
            Assert.AreEqual(0f, none.OccluderId(0), 0f);
            Assert.AreEqual(0f, none.OccluderIdTop, 0f);
            Assert.DoesNotThrow(() => none.Set(0, new object(), Vector3.one, true),
                "and every call on it is inert, so a caller written against a real hull needs no branch");
        }

        // ---- the two source-level guards ------------------------------------------------------------

        private const string FacetShader = "Assets/_Project/Art/Shaders/HiddenHarboursIsoFacet.shader";
        private const string OverlayShader = "Assets/_Project/Art/Shaders/HiddenHarboursIsoFacetOverlay.shader";
        private const string SpriteShader =
            "Assets/_Project/Art/Shaders/HiddenHarboursDeckOccludedSprite.shader";

        private static string ReadShader(string assetPath)
        {
            string full = Path.Combine(Application.dataPath, "..", assetPath);
            Assert.IsTrue(File.Exists(full), $"'{assetPath}' is missing — the guard cannot pass by absence.");
            return File.ReadAllText(full);
        }

        /// <summary>
        /// <b>THE SLOT COUNT IS WRITTEN TWICE AND MUST AGREE.</b> HLSL needs a compile-time array
        /// bound, so the facet shader carries the number as a literal while C# carries it as a const.
        /// A mismatch COMPILES CLEANLY and fails silently in the worst direction: C# publishes twelve
        /// occupants, the shader counts the first four, and the other eight simply are not hidden —
        /// which reads as "the occlusion is flaky", not as "a constant is out of step".
        /// </summary>
        [Test]
        public void TheShaderAndTheCSharp_AgreeOnHowManySlotsThereAre()
        {
            var m = Regex.Match(ReadShader(FacetShader),
                                @"#define\s+HH_DECK_OCCUPANT_SLOTS\s+(\d+)");
            Assert.IsTrue(m.Success,
                "HH_DECK_OCCUPANT_SLOTS is gone from the facet shader. It is the array bound the " +
                "occupant loop runs over; without it there is no partition at all.");
            Assert.AreEqual(IsoFacetHullRenderer.DeckOccupantSlots, int.Parse(m.Groups[1].Value),
                "the facet shader and IsoFacetHullRenderer.DeckOccupantSlots disagree about the slot " +
                "count. Change BOTH — and remember the id budget moves with it: each hull reserves " +
                "1 + slots of the 255 ids the facet alpha can carry.");
        }

        /// <summary>
        /// <b>EVERY PROPERTY C# WRITES MUST EXIST IN THE SHADER THAT READS IT.</b> Unity discards a
        /// write to a property a shader does not declare — no error, no warning, no magenta. A typo
        /// or a rename on either side of this seam therefore produces exactly the defect the seam
        /// exists to fix, and produces it silently.
        /// </summary>
        [Test]
        public void EveryDeckOcclusionProperty_IsDeclaredByTheShaderThatReadsIt()
        {
            string facet = ReadShader(FacetShader);
            string overlay = ReadShader(OverlayShader);
            string sprite = ReadShader(SpriteShader);

            foreach (var (source, where, prop) in new[]
            {
                (facet, "the facet pass", "_HullId"),
                (facet, "the facet pass", "_HullIdFore"),
                (facet, "the facet pass", "_DeckOccupant"),
                (facet, "the facet pass", "_DeckOccupantCount"),
                (overlay, "the overlay quad", "_HullIdFore"),
                (overlay, "the overlay quad", "_HullIdForeSpan"),
                (sprite, "the occluded sprite", "_HHDeckOccluderId"),
                (sprite, "the occluded sprite", "_HHDeckOccluderIdTop"),
            })
            {
                Assert.IsTrue(Regex.IsMatch(source, $@"(^|[^\w]){Regex.Escape(prop)}([^\w]|$)",
                                            RegexOptions.Multiline),
                    $"{where} does not declare '{prop}', which C# writes every frame " +
                    "(IsoFacetShaderIds / DeckRiderVisual). Unity throws that write away in silence, " +
                    "so the only symptom is crew drawing through cabins again.");
            }

            // The overlay must accept the WHOLE block, not just its base — otherwise every band above
            // the first is dropped from the boat's own picture and she loses her wheelhouse the moment
            // a second thing stands on her deck.
            Assert.IsTrue(overlay.Contains("_HullIdForeSpan"),
                "the overlay composes a RANGE of fore ids now; accepting only _HullIdFore would " +
                "delete part of the hull whenever more than one occupant is aboard");
        }
    }
}
