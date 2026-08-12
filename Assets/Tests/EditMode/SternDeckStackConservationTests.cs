using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>CONSERVATION: nothing on the stack is created, lost, or counted twice</b> — walked over whole
    /// gear cycles rather than single calls, because that is the only place a decrement can go missing.
    ///
    /// <para><b>What this suite is for.</b> The stern-deck arc closed with "unconsumed stack maths"
    /// logged as an open defect: the suspicion that some step of the toss-to-re-set or restack path fails
    /// to consume or decrement what it should. A single-call test cannot see that — a leak only shows up
    /// as a DRIFT across cycles, which is why every test here stacks, sets, restacks, drains and
    /// interleaves, and asserts the books balance at each step rather than at the end.</para>
    ///
    /// <para><b>The three quantities that must balance</b>, and the identity each is checked against:</para>
    /// <list type="number">
    ///   <item><b>Pots.</b> <c>drawn + refused == asked for</c>, at every count, on the way up and on the
    ///   way down. A pot may be refused — a hull's <c>CAPS</c> are real — but it may never simply
    ///   evaporate, and a refusal must be SAID (<c>SternDeckLoop.ChooseStackSurface</c> returning
    ///   <see cref="DeckStackSurface.None"/>).</item>
    ///   <item><b>Occupant slots (the #481 twelve).</b> A STACK is one occupant however many pots stand
    ///   on it — every tier shares one ground point, one view depth, one band. So claims must track
    ///   SURFACES IN USE, not pots, and must come back to zero when the deck empties.</item>
    ///   <item><b>Tier geometry.</b> Tier <c>t</c> stands exactly <c>t × step</c> up, always; and the
    ///   tiers below the top do not move when the stack grows or shrinks under them, which is what makes
    ///   a restack a restack rather than a rebuild.</item>
    /// </list>
    ///
    /// <para><b>Why a model and not the presenter.</b> <c>SternDeckGearPresenter</c> is a MonoBehaviour
    /// that needs a hull, sprites, materials and a shader to draw anything. <see cref="DeckModel"/> below
    /// is a faithful, scene-free transcription of the two methods that do the accounting —
    /// <c>DrawStacks</c> and <c>DrawStackOn</c> — including the part that actually matters: that pieces
    /// are POSITIONAL and reused, so a piece that was tier 0 on one frame can be a sharing tier on the
    /// next and must give its slot back when it is. If the presenter's rule ever changes, this model must
    /// change with it, and the doc comments on both say so.</para>
    ///
    /// <para>Every capacity, step and slew fed in is rig-measured DATA passed in, as production must
    /// (CLAUDE.md rule 2). Nothing here is random: rule 5 applies to tests too, so the interleave walk is
    /// a written-down script, not a seeded shuffle.</para>
    /// </summary>
    public class SternDeckStackConservationTests
    {
        // ---- rig-measured fixtures. DATA, passed in — never constants inside the classes under test ----

        /// <summary>TrapIso.CAPS['lobster_boat'] — 5 on the working deck, 2 on the washboard.</summary>
        private const int DeckCapacity = 5;
        private const int WashboardCapacity = 2;
        private const int TotalCapacity = DeckCapacity + WashboardCapacity;

        /// <summary>IsoFacetHullRenderer.DeckOccupantSlots — the twelve #481 measured.</summary>
        private const int HullSlots = 12;

        /// <summary>TrapIso.STACK['wire'] — tier step and the lateral jog pattern a stack leans by.</summary>
        private const float StackStep = 0.38f;
        private const float SlewMeters = 0.04f;
        private static readonly int[] Slew = { 0, 1, 0, -1, 1, 0 };

        /// <summary>The lobster boat's deck stack base, hull-local metres.</summary>
        private static readonly Vector3 DeckBase = new Vector3(-0.30f, -5.30f, 0.50f);

        /// <summary>Four pieces of furniture and two hands — the rest of the worst beat's occupants.</summary>
        private const int GearPieces = 4;
        private const int Hands = 2;

        private const float Tol = 1e-4f;

        // ---- the model -------------------------------------------------------------------------------

        /// <summary>
        /// A fixed-capacity slot pool with the protocol <see cref="IDeckOccupantSlots"/> documents: a
        /// claim is HELD across frames and re-asserting it is idempotent (the same owner gets the same
        /// slot back rather than a second one — the presenter re-claims every frame and would otherwise
        /// drain the hull in twelve frames), and a release is IGNORED unless the caller is the owner the
        /// slot was claimed with, so a stale holder cannot evict a live one.
        /// </summary>
        private sealed class FakeSlots : IDeckOccupantSlots
        {
            private readonly object[] _owners;
            public FakeSlots(int capacity) { _owners = new object[Mathf.Max(0, capacity)]; }

            public int Capacity => _owners.Length;

            public int ActiveCount
            {
                get
                {
                    int n = 0;
                    for (int i = 0; i < _owners.Length; i++) if (_owners[i] != null) n++;
                    return n;
                }
            }

            /// <summary>Peak simultaneous claims over this pool's whole life — the number that catches a
            /// leak that a later release would otherwise tidy away before anybody looked.</summary>
            public int PeakActive { get; private set; }

            public int Claim(object owner)
            {
                for (int i = 0; i < _owners.Length; i++)
                    if (ReferenceEquals(_owners[i], owner)) return i;      // idempotent re-assert
                for (int i = 0; i < _owners.Length; i++)
                    if (_owners[i] == null)
                    {
                        _owners[i] = owner;
                        if (ActiveCount > PeakActive) PeakActive = ActiveCount;
                        return i;
                    }
                return -1;
            }

            public void Release(int slot, object owner)
            {
                if (slot < 0 || slot >= _owners.Length) return;
                if (!ReferenceEquals(_owners[slot], owner)) return;        // only the owner may let go
                _owners[slot] = null;
            }

            public void Set(int slot, object owner, Vector3 rigLocalMeters, bool active) { }
            public float OccluderId(int slot) => 0f;
            public float OccluderIdTop => 0f;
        }

        /// <summary>One drawn thing. Pieces are POSITIONAL and reused frame to frame, exactly as
        /// <c>SternDeckGearPresenter._pieces</c> is — which is the whole reason a slot can leak.</summary>
        private sealed class Piece
        {
            public int Slot = -1;
            public bool Live;
            public Vector3 Local;
        }

        /// <summary>
        /// A scene-free transcription of <c>SternDeckGearPresenter</c>'s accounting: the furniture, then
        /// the stacks (deck first, then washboard, then refused out loud), then the pot in play, then the
        /// release tail that takes back every slot held by a piece that did not draw this frame.
        /// </summary>
        private sealed class DeckModel
        {
            private readonly List<Piece> _pieces = new();
            public readonly FakeSlots Slots;

            public DeckModel(int hullSlots) { Slots = new FakeSlots(hullSlots); }

            /// <summary>Pots asked for that this hull's surfaces would not take — the presenter's
            /// RefusedClaims, per frame.</summary>
            public int RefusedPots { get; private set; }

            /// <summary>Pieces drawn this frame (furniture + every stack tier + the pot in play).</summary>
            public int Drawn { get; private set; }

            /// <summary>Stack tiers drawn this frame, across both surfaces.</summary>
            public int TiersDrawn { get; private set; }

            /// <summary>The hull-local point each stack tier was drawn at this frame, in draw order.</summary>
            public readonly List<Vector3> TierPoints = new();

            /// <summary>One frame of <c>SternDeckGearPresenter.Draw()</c>.</summary>
            public void Draw(int gearPieces, int stackedPots, bool potInPlay)
            {
                RefusedPots = 0;
                TiersDrawn = 0;
                TierPoints.Clear();
                for (int i = 0; i < _pieces.Count; i++) _pieces[i].Live = false;
                int next = 0;

                for (int i = 0; i < gearPieces; i++)
                    DrawAt(ref next, new Vector3(i, 0f, 0f));

                DrawStacks(ref next, stackedPots);

                if (potInPlay) DrawAt(ref next, new Vector3(0f, -1f, 0f));

                // The release tail: anything that drew last frame and not this one gives its slot back NOW.
                for (int i = 0; i < _pieces.Count; i++)
                    if (!_pieces[i].Live) ReleaseSlot(_pieces[i]);

                Drawn = next;
            }

            private void DrawStacks(ref int next, int stackedPots)
            {
                int remaining = stackedPots;
                if (remaining <= 0) return;
                remaining = DrawStackOn(DeckCapacity, remaining, ref next);
                remaining = DrawStackOn(WashboardCapacity, remaining, ref next);
                RefusedPots += remaining;      // said out loud, never eaten
            }

            /// <summary>Tier 0 CLAIMS; every tier above it draws with the id tier 0 was given and needs no
            /// slot of its own. Hands back what would not fit here.</summary>
            private int DrawStackOn(int capacity, int wanted, ref int next)
            {
                if (wanted <= 0) return wanted;
                int here = Mathf.Min(wanted, Mathf.Max(0, capacity));
                for (int tier = 0; tier < here; tier++)
                {
                    Vector3 local = DeckGearPlacement.StackSlot(DeckBase, tier, StackStep,
                                                                Slew, SlewMeters, dir: 0);
                    TierPoints.Add(local);
                    TiersDrawn++;
                    if (tier == 0) DrawAt(ref next, local);
                    else DrawSharing(ref next, local);
                }
                return wanted - here;
            }

            private void DrawAt(ref int next, Vector3 local)
            {
                Piece p = PieceAt(next++);
                p.Local = local;
                p.Slot = Slots.Claim(p);
            }

            /// <summary>A sharing tier holds NO slot — and must give back one it happened to be holding
            /// from an earlier frame, when this same positional piece was somebody else's tier 0.</summary>
            private void DrawSharing(ref int next, Vector3 local)
            {
                Piece p = PieceAt(next++);
                p.Local = local;
                ReleaseSlot(p);
            }

            private void ReleaseSlot(Piece p)
            {
                if (p.Slot < 0) return;
                Slots.Release(p.Slot, p);
                p.Slot = -1;
            }

            private Piece PieceAt(int index)
            {
                while (_pieces.Count <= index) _pieces.Add(new Piece());
                Piece p = _pieces[index];
                p.Live = true;
                return p;
            }
        }

        /// <summary>How many SURFACES a given number of pots occupies — which is how many slots the
        /// stacks claim, because a stack is one occupant however tall it is.</summary>
        private static int SurfacesUsed(int pots)
        {
            if (pots <= 0) return 0;
            return pots <= DeckCapacity ? 1 : 2;
        }

        /// <summary>Slots a frame must be holding: one per piece of furniture, one per stack SURFACE, one
        /// for the pot in play. Never one per pot.</summary>
        private static int ExpectedSlots(int gearPieces, int stackedPots, bool potInPlay)
            => gearPieces + SurfacesUsed(Mathf.Min(stackedPots, TotalCapacity)) + (potInPlay ? 1 : 0);

        /// <summary>Assert the whole ledger for one frame: pots in equal pots drawn plus pots refused,
        /// and the slots held are the surfaces in use rather than the pots standing on them.</summary>
        private static void AssertBalanced(DeckModel deck, int stackedPots, bool potInPlay, string when)
        {
            int fitted = Mathf.Min(stackedPots, TotalCapacity);

            Assert.AreEqual(fitted, deck.TiersDrawn,
                $"{when}: {stackedPots} pot(s) asked for, {fitted} should stand on this hull's surfaces");
            Assert.AreEqual(Mathf.Max(0, stackedPots - TotalCapacity), deck.RefusedPots,
                $"{when}: everything over capacity must be REFUSED out loud, not quietly dropped");
            Assert.AreEqual(stackedPots, deck.TiersDrawn + deck.RefusedPots,
                $"{when}: drawn + refused must equal asked-for — this is the conservation identity, and " +
                "a decrement that goes missing anywhere in the stack path breaks it here first");

            Assert.AreEqual(ExpectedSlots(GearPieces, stackedPots, potInPlay), deck.Slots.ActiveCount,
                $"{when}: a STACK is one occupant however many pots are on it (#481) — slots must track " +
                "surfaces in use, plus the furniture, plus the pot in play");
            Assert.LessOrEqual(deck.Slots.ActiveCount + Hands, HullSlots,
                $"{when}: the frame plus the hands must still fit the hull's twelve");
        }

        // ---- (1) the full cycle: stack up, set one, restack, drain ------------------------------------

        /// <summary>
        /// <b>Stack N, set one, restack, set all — and the books balance at every single step.</b>
        ///
        /// <para>This is the walk the close-out's suspicion was about. It climbs past the deck's own
        /// capacity onto the washboard and past the washboard into refusal, then comes all the way back
        /// down to nothing, asserting after each frame that the pots asked for are exactly the pots drawn
        /// plus the pots refused, and that the slots held are the surfaces in use — never the pots.</para>
        ///
        /// <para>The way down is not the way up run backwards: the pieces are positional and REUSED, so a
        /// piece that claimed as tier 0 of the washboard on the way up becomes a sharing tier of the deck
        /// stack on the way down. If that transition failed to release, the count would drift upward here
        /// and nowhere else.</para>
        /// </summary>
        [Test]
        public void StackingUpAndSettingBackDown_ConservesEveryPot_AndEverySlot()
        {
            var deck = new DeckModel(HullSlots);

            for (int n = 0; n <= TotalCapacity + 2; n++)
            {
                deck.Draw(GearPieces, n, potInPlay: false);
                AssertBalanced(deck, n, false, $"stacking up to {n}");
            }

            for (int n = TotalCapacity + 2; n >= 0; n--)
            {
                deck.Draw(GearPieces, n, potInPlay: false);
                AssertBalanced(deck, n, false, $"setting back down to {n}");
            }

            deck.Draw(0, 0, potInPlay: false);
            Assert.AreEqual(0, deck.Slots.ActiveCount,
                "an empty deck holds NO slots — every claim taken over the cycle came back");
        }

        /// <summary>
        /// <b>A stack is ONE occupant, however many pots stand on it.</b> The #481 budget was measured
        /// that way — it counted the lobster boat's two trap stacks as two occupants, not as the seven
        /// pots on them — so claiming per pot would overrun a deck the seam had sized correctly.
        /// </summary>
        [Test]
        public void AFullStackClaimsOneSlotPerSurface_NotOnePerPot()
        {
            var deck = new DeckModel(HullSlots);

            deck.Draw(gearPieces: 0, stackedPots: DeckCapacity, potInPlay: false);
            Assert.AreEqual(DeckCapacity, deck.TiersDrawn, "five pots stand on the deck");
            Assert.AreEqual(1, deck.Slots.ActiveCount,
                "…and they are ONE occupant: same ground point, same view depth, one band hides them all");

            deck.Draw(gearPieces: 0, stackedPots: TotalCapacity, potInPlay: false);
            Assert.AreEqual(TotalCapacity, deck.TiersDrawn, "seven pots across both surfaces");
            Assert.AreEqual(2, deck.Slots.ActiveCount, "two surfaces in use — two occupants, not seven");
        }

        /// <summary>
        /// <b>The worst beat of the loop still fits the twelve when it is actually DRAWN.</b>
        /// <c>SternDeckLoop.OccupantsNeeded</c> says the arithmetic works; this says the draw does, which
        /// is a different claim — it is the one that fails if the drawing path ever claims per pot.
        /// </summary>
        [Test]
        public void TheWorstBeatDrawsInsideTheHullsTwelveSlots()
        {
            var deck = new DeckModel(HullSlots);
            deck.Draw(GearPieces, stackedPots: TotalCapacity, potInPlay: true);

            int needed = SternDeckLoop.OccupantsNeeded(GearPieces, stackedPots: 2, potInPlay: true, hands: Hands);
            Assert.AreEqual(needed - Hands, deck.Slots.ActiveCount,
                "the drawn frame and the arithmetic must agree — two stacks, four gear, one pot in play");
            Assert.LessOrEqual(deck.Slots.PeakActive + Hands, HullSlots,
                "and the peak over the whole frame, plus the hands, fits the twelve");
        }

        // ---- (2) interleaving work with stacking ------------------------------------------------------

        /// <summary>
        /// <b>Interleaved: work a pot, square her away, toss her, haul the next — and nothing drifts.</b>
        ///
        /// <para>The pot in play and the pot on the stack are EXCLUSIVE, and the cycle crosses between
        /// them twice per pot (squared away → stacked; tossed → gone). A step that failed to consume on
        /// that crossing would show as a slot that is never given back, so this walks several whole pots
        /// through it and checks the ledger after every frame — including the frames where the stack
        /// grows under a pot that is still in the hands.</para>
        ///
        /// <para>The script is written down rather than generated: determinism (rule 5) applies to what a
        /// test does as much as to what the sim does.</para>
        /// </summary>
        [Test]
        public void InterleavingTheWorkedPotWithTheStack_NeverLeaksASlot()
        {
            var deck = new DeckModel(HullSlots);

            // (stacked, potInPlay) — four pots through the full cycle, the stack growing under them.
            var script = new List<(int Stacked, bool InPlay)>
            {
                (0, false),                                   // idle deck
                (0, true),  (0, true),                        // pot 1 aboard, being emptied then baited
                (1, false),                                   // squared away — she IS the stack now
                (0, false),                                   // tossed: over the side, deck bare
                (0, true),  (1, true),                        // pot 2 in the hands while pot 1's back aboard
                (2, false),                                   // both squared away
                (2, true),                                    // pot 3 in the hands, two on the stack
                (3, false),
                (3, true),                                    // pot 4 in the hands
                (4, false),
                (1, false),                                   // three set in a row
                (0, false),                                   // and the last one — bare deck again
            };

            for (int i = 0; i < script.Count; i++)
            {
                (int stacked, bool inPlay) = script[i];
                deck.Draw(GearPieces, stacked, inPlay);
                AssertBalanced(deck, stacked, inPlay, $"step {i} ({stacked} stacked, in-play {inPlay})");
            }

            deck.Draw(0, 0, potInPlay: false);
            Assert.AreEqual(0, deck.Slots.ActiveCount,
                "after four whole pots the deck gives every slot back — no drift across cycles");
        }

        /// <summary>
        /// <b>A refused pot costs nothing.</b> Asking for more than the hull will take must not consume a
        /// slot, must not draw a tier in mid-air, and must not poison the next frame: dropping back to a
        /// legal count has to leave exactly the ledger a legal count would have had on its own.
        /// </summary>
        [Test]
        public void OverflowIsRefusedWithoutSpendingAnything_AndLeavesNoResidue()
        {
            var over = new DeckModel(HullSlots);
            over.Draw(GearPieces, stackedPots: TotalCapacity + 5, potInPlay: true);
            Assert.AreEqual(5, over.RefusedPots, "five pots over this hull's CAPS are refused");
            Assert.AreEqual(TotalCapacity, over.TiersDrawn, "and exactly capacity is drawn — none in mid-air");

            over.Draw(GearPieces, stackedPots: 3, potInPlay: false);

            var clean = new DeckModel(HullSlots);
            clean.Draw(GearPieces, stackedPots: 3, potInPlay: false);

            Assert.AreEqual(clean.Slots.ActiveCount, over.Slots.ActiveCount,
                "a deck that overflowed and came back must hold exactly what a deck that never did holds");
            Assert.AreEqual(clean.TiersDrawn, over.TiersDrawn);
            Assert.AreEqual(0, over.RefusedPots, "and the refusal does not linger into the next frame");
        }

        // ---- (3) the tier geometry --------------------------------------------------------------------

        /// <summary>
        /// <b>Restacking does not move the pots underneath.</b> Tier <c>t</c> is a function of <c>t</c>
        /// alone, so growing or shrinking a stack leaves every tier below the top exactly where it was.
        /// If height or slew were ever derived from the stack's CURRENT size instead, a pot would slide
        /// every time another landed on it — and no single-height assertion would catch that.
        /// </summary>
        [Test]
        public void TiersBelowTheTopDoNotMoveWhenTheStackGrowsOrShrinks()
        {
            var deck = new DeckModel(HullSlots);
            var byHeight = new List<Vector3>();

            for (int n = 1; n <= DeckCapacity; n++)
            {
                deck.Draw(gearPieces: 0, stackedPots: n, potInPlay: false);
                Assert.AreEqual(n, deck.TierPoints.Count, $"{n} pots draw {n} tiers");

                for (int t = 0; t < byHeight.Count; t++)
                    Assert.AreEqual(byHeight[t], deck.TierPoints[t],
                        $"tier {t} moved when the stack grew to {n} — a restack must not disturb what is " +
                        "already under it");

                byHeight.Clear();
                byHeight.AddRange(deck.TierPoints);
            }

            // …and back down: the same points, in the same order, as the stack is set away.
            for (int n = DeckCapacity - 1; n >= 1; n--)
            {
                deck.Draw(gearPieces: 0, stackedPots: n, potInPlay: false);
                for (int t = 0; t < n; t++)
                    Assert.AreEqual(byHeight[t], deck.TierPoints[t],
                        $"tier {t} moved when the stack shrank to {n}");
            }
        }

        /// <summary>
        /// <b>Every tier is its own height and its own place</b> — <c>t × step</c> up, never two pots in
        /// one spot. A step that silently resolved to 0 would stack the whole load inside one pot, which
        /// reads as "one pot" on screen and as "five" in the ledger: the exact shape of a miscount.
        /// </summary>
        [Test]
        public void EveryTierStandsAtItsOwnHeight_AndNoTwoTiersCollide()
        {
            var deck = new DeckModel(HullSlots);
            deck.Draw(gearPieces: 0, stackedPots: DeckCapacity, potInPlay: false);

            for (int t = 0; t < DeckCapacity; t++)
                Assert.AreEqual(DeckBase.z + (t * StackStep), deck.TierPoints[t].z, Tol,
                    $"tier {t} stands exactly {t} steps up from the base — the pot's own step, not the hull's");

            for (int a = 0; a < DeckCapacity; a++)
                for (int b = a + 1; b < DeckCapacity; b++)
                    Assert.AreNotEqual(deck.TierPoints[a], deck.TierPoints[b],
                        $"tiers {a} and {b} landed in the same place — a stack of {DeckCapacity} would draw " +
                        "as fewer pots than the deck was told it had");
        }

        // ---- (4) the surface walk ---------------------------------------------------------------------

        /// <summary>
        /// <b>The deck fills, then the washboard, then the answer is honestly "nowhere".</b> Walked as a
        /// full fill rather than probed once, so the counts are checked as they ACCUMULATE — a chooser
        /// that forgot to count what it had already placed would keep answering
        /// <see cref="DeckStackSurface.Deck"/> for ever and the total would run past the hull.
        /// </summary>
        [Test]
        public void FillingBothSurfacesPlacesExactlyCapacity_ThenRefuses()
        {
            int onDeck = 0, onWashboard = 0, refused = 0;

            for (int pot = 0; pot < TotalCapacity + 3; pot++)
            {
                switch (SternDeckLoop.ChooseStackSurface(onDeck, DeckCapacity, onWashboard, WashboardCapacity))
                {
                    case DeckStackSurface.Deck: onDeck++; break;
                    case DeckStackSurface.Washboard: onWashboard++; break;
                    default: refused++; break;
                }

                Assert.LessOrEqual(onDeck, DeckCapacity, "the deck never takes more than its CAPS");
                Assert.LessOrEqual(onWashboard, WashboardCapacity, "nor does the washboard");
                Assert.AreEqual(pot + 1, onDeck + onWashboard + refused,
                    "every pot offered is placed somewhere or refused — never both, never neither");
            }

            Assert.AreEqual(DeckCapacity, onDeck, "the deck filled first — that is where the hands are");
            Assert.AreEqual(WashboardCapacity, onWashboard, "then the washboard took the overflow");
            Assert.AreEqual(3, refused, "and the last three had nowhere to go, which is a real answer");
            Assert.AreEqual(TotalCapacity,
                SternDeckLoop.TotalStackCapacity(DeckCapacity, WashboardCapacity),
                "the total the caller promises against is the two surfaces' own numbers");
        }

        /// <summary>Emptying is the fill run backwards, and it must free the surfaces in the reverse
        /// order it filled them — otherwise the next pot is offered a washboard slot while the deck
        /// stands empty, and the stack the player sees stops matching the stack she has.</summary>
        [Test]
        public void EmptyingFreesTheWashboardFirst_AndTheDeckLast()
        {
            int onDeck = DeckCapacity, onWashboard = WashboardCapacity;

            for (int set = 0; set < TotalCapacity; set++)
            {
                if (onWashboard > 0) onWashboard--;
                else onDeck--;

                Assert.AreEqual(TotalCapacity - (set + 1), onDeck + onWashboard,
                    "one pot set is exactly one pot fewer aboard");
                if (onDeck < DeckCapacity)
                    Assert.AreEqual(DeckStackSurface.Deck,
                        SternDeckLoop.ChooseStackSurface(onDeck, DeckCapacity, onWashboard, WashboardCapacity),
                        "with room on the deck, the next pot goes to the deck — not to a stale washboard");
            }

            Assert.AreEqual(0, onDeck + onWashboard, "the deck is bare");
            Assert.AreEqual(DeckStackSurface.Deck,
                SternDeckLoop.ChooseStackSurface(0, DeckCapacity, 0, WashboardCapacity),
                "and a bare deck takes the next pot on the deck");
        }
    }
}
