using UnityEngine;              // Mathf — the helm-framing ladder below
using HiddenHarbours.Core;

namespace HiddenHarbours.App
{
    /// <summary>
    /// Which of the follow-cam's discrete, pixel-perfect framings should be on screen. The camera never
    /// zooms to an arbitrary orthographic size — each framing maps to a PPU-integer step (the ratified
    /// per-context discrete-zoom vision), so the picture stays crisp at every stop.
    /// </summary>
    public enum CameraFraming
    {
        /// <summary>At the helm — the active hull's data-driven framing
        /// (<c>BoatHullDef.CameraWorldHeightMeters</c>; bigger boat = more water).</summary>
        Boat,

        /// <summary>Walking the coast — the tighter on-foot framing (the fisher reads large).</summary>
        OnFoot,

        /// <summary>Standing ON DECK (boarded, not at the helm) — a step closer again so the boat fills
        /// the screen and deck work (pots, bait, the rail) reads in detail. Owner playtest 2026-07-08.</summary>
        Deck,

        /// <summary>On deck with a trap haul LIVE — one more step so the rope-and-buoy action is the
        /// star. Released the moment the pot surfaces or the haul goes idle. Optional (owner-tunable).</summary>
        DeckHaul,

        /// <summary>Behind the wheel of a road vehicle — the machine's own data-driven framing
        /// (<c>VehicleDef.CameraWorldHeightMeters</c>, arriving on <see cref="ActiveVehicleChanged"/>).
        /// Wider than any of the above, and it has to be: a truck at 11 m/s covers ground faster than
        /// anything else the player controls, and a view framed for a walking fisher would be a wall of
        /// scenery arriving with no time to read it.</summary>
        Vehicle,
    }

    /// <summary>
    /// The zoom brain of <see cref="CameraFollow"/> as a plain engine-light POCO (CLAUDE.md §5): it maps
    /// (control mode, live-haul flag) to the <see cref="CameraFraming"/> that should be on screen, and
    /// owns the commit HOLD (hysteresis) so rapid helm⇄deck hops collapse into a single re-zoom instead
    /// of thrashing the discrete pixel-perfect steps. Deterministic — a pure function of the inputs and
    /// the time it is fed (no hidden clock, no randomness), so EditMode tests drive it with plain numbers.
    /// </summary>
    public sealed class CameraZoomPolicy
    {
        private bool _hasCommitted;
        private CameraFraming _committed;
        private double _lastCommitTime;

        /// <summary>False until the first <see cref="TryCommit"/> succeeds.</summary>
        public bool HasCommitted => _hasCommitted;

        /// <summary>The framing last committed (undefined until <see cref="HasCommitted"/>).</summary>
        public CameraFraming Committed => _committed;

        /// <summary>
        /// The framing the current control state WANTS. Pure mapping: the helm gets the boat's framing,
        /// on foot gets the on-foot framing, the deck gets the closer deck step — tightened one more step
        /// while a trap haul is live (if <paramref name="haulTightensZoom"/>; the haul is deck work, so
        /// the flag can never tighten any other mode).
        /// </summary>
        public static CameraFraming DesiredFraming(ControlMode mode, bool haulLive, bool haulTightensZoom)
            => DesiredFraming(mode, haulLive, haulTightensZoom, carriedAboard: false);

        /// <summary>
        /// The same mapping, told whether she is a PASSENGER on the deck she is standing on
        /// (<see cref="CarriedAboardChanged"/>) — in which case the deck is a vessel crossing water and
        /// not a workbench, and the framing is the vessel's.
        ///
        /// <para><b>Being carried beats the haul</b>, and not merely by precedence: the two cannot both
        /// be true. A haul is something you do with your own hands on your own boat, and a passenger has
        /// neither. Ordering it first states that rather than leaving the reader to work out which flag
        /// would win.</para>
        /// </summary>
        public static CameraFraming DesiredFraming(ControlMode mode, bool haulLive,
                                                   bool haulTightensZoom, bool carriedAboard)
        {
            if (mode == ControlMode.Aboard) return CameraFraming.Boat;
            if (mode == ControlMode.Driving) return CameraFraming.Vehicle;
            if (mode == ControlMode.OnDeck)
            {
                if (carriedAboard) return CameraFraming.Boat;
                return (haulLive && haulTightensZoom) ? CameraFraming.DeckHaul : CameraFraming.Deck;
            }
            return CameraFraming.OnFoot;
        }

        /// <summary>
        /// Feed the desired framing every tick; returns true exactly when the camera should re-frame NOW.
        /// The first-ever desire commits immediately (a single clean switch feels instant). A change that
        /// lands within <paramref name="minHoldSeconds"/> of the previous commit is HELD: keep feeding it
        /// and it commits the moment the hold expires — unless the desire meanwhile returns to the
        /// committed framing, in which case the hop dissolves and the camera never moved (a rapid
        /// helm⇄deck there-and-back re-zooms ZERO times).
        /// </summary>
        public bool TryCommit(CameraFraming desired, double nowSeconds, double minHoldSeconds)
        {
            if (_hasCommitted && desired == _committed) return false;
            if (_hasCommitted && nowSeconds - _lastCommitTime < minHoldSeconds) return false;
            _committed = desired;
            _hasCommitted = true;
            _lastCommitTime = nowSeconds;
            return true;
        }

        // ================= HELM FRAMING: the whole vessel visible (owner ruling 2026-07-29, §9.8) ==========
        //
        // *"cameras should zoom out on larger vessels so the whole vessels are visible; they seem fine
        // up till lobster boat and then you're too zoomed in on larger vessels."*
        //
        // ⚠️ THE DEFECT WAS A SILENT CAP, not a missing derivation. The step search ran integer
        // UPSCALE only (zoom x1..x8), and PixelPerfectCamera's own zoom clamps at >= 1 — so the widest
        // framing expressible AT ALL was screenH / ppu = 33.75 m at 1080p. The defs already ask for
        // 40 / 60 / 90 / 160 m for dragger -> tanker; every one of them was quietly capped to 33.75
        // and rendered at the SAME framing. That is exactly what he saw.
        //
        // ⚠️ AND THE RULING'S TWO CONSTRAINTS CANNOT BOTH HOLD ABOVE ~37.5 m OF HULL. "Whole vessel
        // visible" and "an integer pixel-perfect step" are unsatisfiable past that at the locked
        // PPU 32 (VS-23): a 110 m tanker is 3,520 asset pixels of hull on a 1,920 px screen. The ladder
        // therefore continues OUTWARD by integer DOWNSCALE (2:1, 3:1 …) — still a clean pixel ratio,
        // 2x2 asset pixels to one screen pixel, with no blur and no shimmer, unlike an arbitrary ortho
        // size. Coordinator's call, 2026-07-30: big hulls lose pixel DETAIL rather than being cropped.

        /// <summary>Ladder steps are a signed integer. <c>step &gt;= 1</c> is integer UPSCALE (zoom
        /// in): <c>screenH / (step*ppu)</c>. <c>step &lt;= -2</c> is integer DOWNSCALE (zoom out):
        /// <c>screenH * (-step) / ppu</c>. Step 1 is the 1:1 pivot, so -1 is deliberately not a step —
        /// it would duplicate it.</summary>
        public const int MinStep = -8;   // 8:1 downscale — 270 m of world height at 1080p
        public const int MaxStep = 8;    // x8 upscale — the tightest haul framing

        /// <summary>World height (m) a ladder step shows. The ONE place the ladder is defined.</summary>
        public static float WorldHeightForStep(int step, int ppu, int screenHeightPx)
        {
            int p = Mathf.Max(1, ppu);
            float h = Mathf.Max(1, screenHeightPx);
            if (step >= 1) return h / (step * p);            // integer upscale (crisp, magnified)
            return h * Mathf.Max(2, -step) / p;              // integer downscale (clean ratio, shrunk)
        }

        /// <summary>
        /// A step's position in the ladder's own ORDER, widest to closest, as a contiguous integer.
        ///
        /// <para><b>Why an index exists at all.</b> Steps are not contiguous: 0 and -1 are not steps
        /// (see the const doc), so the sequence runs … -4, -3, -2, <b>1</b>, 2, 3 … and plain
        /// <c>step + 1</c> walks straight into the two-step hole at the 1:1 pivot. On foot that never
        /// mattered because the walker's range lives entirely inside the upscale half. The aboard band
        /// below is centred on a HULL's framing, and a big enough hull sits at or past the pivot — so
        /// "one stop wider" there has to mean the next real stop, not an arithmetic neighbour that
        /// does not exist.</para>
        /// </summary>
        public static int LadderIndex(int step) => step <= -2 ? step + 2 : step;

        /// <summary>The step at a ladder index — the inverse of <see cref="LadderIndex"/>.</summary>
        public static int StepAtIndex(int index) => index <= 0 ? index - 2 : index;

        /// <summary>
        /// Walk <paramref name="stops"/> whole stops along the ladder from <paramref name="step"/>,
        /// <b>positive = closer</b>, saturating at the ladder's own ends rather than wrapping. Skips
        /// the 0 / -1 hole by construction (it walks indices, not step numbers).
        /// </summary>
        public static int StepClosestBy(int step, int stops)
            => StepAtIndex(Mathf.Clamp(LadderIndex(step) + stops,
                                       LadderIndex(MinStep), LadderIndex(MaxStep)));

        /// <summary>True when a step is an integer UPSCALE — the only kind <c>PixelPerfectCamera</c>
        /// can express (its zoom clamps at &gt;= 1). A downscale step must bypass it and drive the
        /// orthographic size directly; see <c>CameraFollow.ApplyFramingHard</c>.</summary>
        public static bool StepIsPixelPerfectUpscale(int step) => step >= 1;

        /// <summary>
        /// <b>How much world ONE RENDERED SCREEN PIXEL covers at a ladder step</b> — the grid a
        /// rendered position must land on to stay crisp (<c>Core.PixelGrid</c>).
        ///
        /// <para>It is just the step's own world height divided by the screen, which is the point:
        /// the ladder is already the ONE place the framing is defined, so the pixel size falls out of
        /// it rather than being re-derived beside it and drifting. That resolves to
        /// <c>1/(step·ppu)</c> on an integer UPSCALE and <c>(−step)/ppu</c> on an integer DOWNSCALE.</para>
        ///
        /// <para><b>Why not just snap to the asset grid (1/ppu).</b> It is wrong at both ends of the
        /// ladder. Magnified (step 3), a screen pixel is a THIRD of an asset pixel, so 1/ppu would
        /// throw away two thirds of the camera's available smoothness for nothing — the texels were
        /// already aligned. Shrunk (step −2), one screen pixel is TWO asset pixels, so 1/ppu does not
        /// reach a screen pixel boundary at all and the snap would simply fail to bite.</para>
        /// </summary>
        public static float WorldUnitsPerRenderedPixel(int step, int ppu, int screenHeightPx)
        {
            int h = Mathf.Max(1, screenHeightPx);
            return WorldHeightForStep(step, ppu, h) / h;
        }

        /// <summary>The ladder step nearest a requested world height — now searching OUTWARD as well
        /// as in, so a def asking for 60 m is no longer silently served 33.75.</summary>
        public static int StepForWorldHeight(float worldHeightMeters, int ppu, int screenHeightPx)
        {
            float wanted = Mathf.Max(0.5f, worldHeightMeters);
            int best = 1;
            float bestErr = float.MaxValue;
            for (int s = MinStep; s <= MaxStep; s++)
            {
                if (s == -1 || s == 0) continue;             // not steps (see the const doc)
                float err = Mathf.Abs(WorldHeightForStep(s, ppu, screenHeightPx) - wanted);
                if (err < bestErr) { bestErr = err; best = s; }
            }
            return best;
        }

        /// <summary>
        /// The helm framing (m of world height) for a hull: its authored framing, FLOORED by what it
        /// takes to actually show the vessel.
        ///
        /// <para><b>A floor, not a replacement</b> — and that is why the small end is untouched. A
        /// dory is framed at 14 m for intimacy (P2's scale fantasy), not because 4.5 m of boat needs
        /// it; replacing the authored value with a fit-derived one would zoom every small boat IN and
        /// change the game the owner is happy with. The ruling says only that big vessels must not be
        /// cropped, so this only ever pushes OUT.</para>
        ///
        /// <para><b>The worst-case footprint is not the hull's length.</b> The view is iso: bow-on, a
        /// hull is foreshortened into <c>sin(elevation)</c> of the screen's SHORT axis; beam-on it
        /// lies across the LONG axis and costs <c>1/aspect</c> of the height. The binding case is
        /// whichever is larger — at the fleet's 40° elevation on 16:9 that is the bow-on 0.643, and
        /// measuring against raw length instead would zoom out ~55 % further than any heading needs.</para>
        /// </summary>
        public static float HelmWorldHeightMeters(float authoredWorldHeightMeters, float hullLengthMeters,
                                                  float marginFactor, float isoElevationDegrees, float aspect)
        {
            float footprint = Mathf.Max(Mathf.Sin(Mathf.Max(1f, isoElevationDegrees) * Mathf.Deg2Rad),
                                        1f / Mathf.Max(0.1f, aspect));
            float needed = Mathf.Max(0f, hullLengthMeters) * Mathf.Max(1f, marginFactor) * footprint;
            return Mathf.Max(authoredWorldHeightMeters, needed);
        }

        // ================= PLAYER ZOOM: the wheel is the player's eye (owner ruling 2026-08-19) =====
        //
        // *"Mouse wheel modifies player zoom — closer to look at interiors, out when outside."*
        //
        // ⚠️ THIS IS A SECOND HAND ON THE SAME LADDER, NOT A SECOND LADDER. The failure mode the
        // ruling could have produced is a free player zoom fighting the RULED framings above: the
        // helm's per-hull step (§9.8's "whole vessel visible"), the deck step, the haul tighten. Every
        // stop the wheel can reach anywhere is a step on the same integer ladder
        // <see cref="WorldHeightForStep"/> defines, so there is no orthographic size in this feature
        // that the ladder did not produce.
        //
        // ⚠️ TWO KINDS OF PLAYER STATE, AND THE DIFFERENCE IS THE WHOLE DESIGN.
        //   • ON FOOT she owns a RUNG — an absolute stop she keeps for the whole voyage. Disembarking
        //     restores it because that rung IS the on-foot framing; nothing saves or reinstates it.
        //   • ABOARD AND ON DECK (owner ruling 2026-08-22) she owns an OFFSET from whatever the
        //     context ruled, released the moment that ruling changes. That is what lets the band exist
        //     without taking the framing away from the hull: arriving at a helm, a deck or a new boat
        //     always hands her the ruled framing, and the wheel is a look around from there.
        //
        // ⚠️ STEPS ASCEND AS YOU ZOOM IN (step 4 = 8.44 m, step 6 = 5.63 m). No step INDEX is ever
        // owner-facing for that reason — the walker's clamps are METRES on the GameConfig asset, the
        // same units every other camera dial uses, and they quantise through StepForWorldHeight. The
        // aboard band's two allowances are the one exception and they are not indices: they are COUNTS
        // of stops either way, which cannot be metres because the framing they are measured from is
        // different for every hull the player will ever own.

        /// <summary>
        /// Whether the PLAYER's wheel may move <paramref name="framing"/> at all.
        ///
        /// <para><b>Three framings, since the owner ruling of 2026-08-22:</b> the walker's, the helm's
        /// and the deck's. On foot the wheel picks the rung outright; aboard and on deck it may only
        /// step within a band around the framing that context was given (see
        /// <see cref="ClampBandOffset"/>), so the hull's "whole vessel visible" derivation and the
        /// deck step are still what you are handed every time you arrive at them.</para>
        ///
        /// <para><b>A live haul and a road vehicle stay ruled outright.</b> The haul tighten exists for
        /// the seconds a pot is coming up and releases itself; a wheel fighting it would be fighting
        /// something that is already leaving. The vehicle framing is the machine's own def and the
        /// ruling did not reach it — a truck at 11 m/s is the one view where a player zooming in is
        /// taking away the room they need to read what is arriving.</para>
        /// </summary>
        public static bool PlayerOwnsFraming(CameraFraming framing)
            => framing == CameraFraming.OnFoot
            || framing == CameraFraming.Boat
            || framing == CameraFraming.Deck;

        /// <summary>
        /// What the player is actually LOOKING AT — the committed framing, or the walker's if control
        /// has not declared itself yet.
        ///
        /// <para>🔴 <b>THE ON-FOOT DEAD PATH (owner playtest 2026-08-22), and it lived right here.</b>
        /// The wheel used to require a committed framing, on the reasoning that "before the first
        /// commit the builder-authored framing rules and there is no tier to step from". But the camera
        /// only ever commits from <c>TickZoom</c>, <c>TickZoom</c> refuses to run until a
        /// <c>ControlModeChanged</c> has arrived, and <b>nothing publishes one at boot</b>: the
        /// switcher announces a mode on a TRANSITION (board, disembark, take a wheel) and on the
        /// region-arrival re-assert, and the arrival opening — which does publish — is skipped for any
        /// save that has already arrived. So a returning player who loaded their game and walked down
        /// the wharf had a camera that had committed nothing, and a wheel that was switched off with no
        /// error anywhere. Boarding once and stepping back ashore "fixed" it, which is exactly how it
        /// survived a build.</para>
        ///
        /// <para>The repair is to answer the question the gate was really asking. Un-committed does not
        /// mean "no framing is on screen"; it means the builder-authored one is, and the builders
        /// author the walker's (<c>PersistentCoreBuilder</c> writes <c>OnFootWorldHeightMeters</c> into
        /// the camera). She is a walker until the game says otherwise — you cannot be at a helm without
        /// having taken one, and taking one publishes. So the un-committed camera is a walking camera,
        /// and its wheel is the walker's.</para>
        /// </summary>
        public static CameraFraming FramingOnScreen(CameraFraming committed, bool hasCommitted)
            => hasCommitted ? committed : CameraFraming.OnFoot;

        /// <summary>
        /// Does a wheel notch do anything at all this frame? The framing on screen must be one the
        /// player may move (<see cref="PlayerOwnsFraming"/> of <see cref="FramingOnScreen"/>), and no
        /// modal may be holding the interaction gate.
        ///
        /// <para><b>Why the modal gate.</b> A wheel turned over an open notebook or a dialogue must not
        /// do two things at once — and the moment any of those UIs grows a scrollable list, the same
        /// wheel would scroll the list AND zoom the world. Refusing here is the cheap half of that
        /// problem; the UI keeping the gate raised is the other half, and it already does.</para>
        /// </summary>
        public static bool WheelIsLive(CameraFraming committed, bool hasCommitted, bool modalBlocked)
            => !modalBlocked && PlayerOwnsFraming(FramingOnScreen(committed, hasCommitted));

        /// <summary>
        /// Clamp a ladder step into the player's range. The bounds arrive as the steps the owner's
        /// METRE clamps quantised to, so they may land either way round (closest is the LARGER step
        /// number); this normalises rather than trusting the caller, because a config asset with the
        /// two heights typed the wrong way round must still produce a usable range, not an empty one.
        /// Both ends are held inside the ladder's own <see cref="MinStep"/>/<see cref="MaxStep"/>
        /// FIRST and independently — clamping only one end (or clamping after taking min/max) lets a
        /// nonsense config carry a bound straight past the ladder and hand back a step that does not
        /// exist. Because the two clamps are monotonic, lo ≤ hi always holds afterwards, so a range
        /// typed as a single repeated value pins the wheel to that one tier rather than to none.
        /// Steps 0 and -1 (which are not steps — see the const doc) stay unreachable because the
        /// player's range never crosses the 1:1 pivot.
        /// </summary>
        public static int ClampPlayerStep(int step, int closestStep, int farthestStep)
        {
            int lo = Mathf.Clamp(Mathf.Min(closestStep, farthestStep), MinStep, MaxStep);
            int hi = Mathf.Clamp(Mathf.Max(closestStep, farthestStep), MinStep, MaxStep);
            return Mathf.Clamp(step, lo, hi);
        }

        /// <summary>
        /// Step the player's tier by whole wheel notches. <b>+1 notch = one step CLOSER</b> (scroll up
        /// / push away = zoom in, the near-universal convention), which is +1 on the ladder because
        /// steps ascend inward. Saturates at the clamps rather than wrapping: a wheel spun hard at the
        /// closest tier simply stays there.
        /// </summary>
        public static int StepPlayerZoom(int currentStep, int notches, int closestStep, int farthestStep)
            => ClampPlayerStep(StepClosestBy(currentStep, notches), closestStep, farthestStep);

        // ================= THE ABOARD BAND: the wheel at the helm (owner ruling 2026-08-22) =========
        //
        // The design doc left this open in as many words — *"Should the wheel work at the helm at all —
        // a per-hull ± of one or two stops around the ruled framing, rather than being inert? Inert is
        // what the ruling asked for and what is built; the alternative is one more clamp pair and the
        // same policy."* The owner has now answered it: the wheel works aboard and on deck.
        //
        // ⚠️ AN OFFSET, NOT A RUNG. The walker owns a rung — an absolute stop on the ladder that she
        // keeps across a whole voyage and gets back when she steps ashore. Aboard she owns an OFFSET
        // from whatever the context ruled, and the offset is released the moment that ruling changes.
        // The distinction is the whole reason a band can exist without taking the framing away from the
        // hull: §9.8's "whole vessel visible" derivation, the deck step and the haul tighten each stay
        // the thing you are HANDED, and the wheel is a look around from there. Store a rung instead and
        // the first hull upgrade would frame the new boat at the old boat's zoom — the exact bug §9.8
        // was written to kill.
        //
        // ⚠️ AND IT IS THE SAME LADDER AGAIN. Every stop the band can reach is StepClosestBy of the
        // ruled framing's own step, so a banded view is as pixel-perfect as an unbanded one. There is
        // no arbitrary orthographic size anywhere in this feature, at any framing, at any offset.

        /// <summary>
        /// Hold a band offset inside the allowance the owner's config gives (<b>positive = closer</b>).
        /// Both allowances are counts of stops and are read as magnitudes, so a negative typed into
        /// either simply means "no stops that way" rather than inverting the band. An allowance pair of
        /// 0 / 0 pins the offset to zero, which is how an owner turns the aboard band off without
        /// touching the walker's wheel.
        /// </summary>
        public static int ClampBandOffset(int offset, int stopsCloser, int stopsWider)
            => Mathf.Clamp(offset, -Mathf.Abs(stopsWider), Mathf.Abs(stopsCloser));

        /// <summary>
        /// The ladder step a RULED framing is showing once the player's band offset is applied: the
        /// framing's own nearest step, walked <paramref name="offset"/> stops (clamped to the
        /// allowance, then held inside the ladder's ends by <see cref="StepClosestBy"/>).
        /// </summary>
        public static int BandStep(float ruledWorldHeightMeters, int offset,
                                   int stopsCloser, int stopsWider, int ppu, int screenHeightPx)
            => StepClosestBy(StepForWorldHeight(ruledWorldHeightMeters, ppu, screenHeightPx),
                             ClampBandOffset(offset, stopsCloser, stopsWider));

        /// <summary>
        /// World height (m) a ruled framing shows at a band offset.
        ///
        /// <para>⚠️ <b>At offset zero it answers the RULED height, not that height's step</b> — the
        /// same rule, for the same reason, as the walker's home rung in
        /// <c>CameraFollow.PlayerZoomWorldHeightMeters</c>. <c>ApplyFramingHard</c> deliberately drives
        /// the raw orthographic size from the REQUEST on the upscale half of the ladder, so answering
        /// 16.875 where the hull has always asked for 17 would move the helm framing for every player
        /// who never touches the wheel. An untouched wheel therefore leaves every ruled framing in the
        /// game byte-for-byte where it was, and only a player who actually scrolls meets the ladder.</para>
        /// </summary>
        public static float BandWorldHeightMeters(float ruledWorldHeightMeters, int offset,
                                                  int stopsCloser, int stopsWider,
                                                  int ppu, int screenHeightPx)
        {
            int clamped = ClampBandOffset(offset, stopsCloser, stopsWider);
            if (clamped == 0) return ruledWorldHeightMeters;
            return WorldHeightForStep(
                BandStep(ruledWorldHeightMeters, clamped, stopsCloser, stopsWider, ppu, screenHeightPx),
                ppu, screenHeightPx);
        }

        /// <summary>
        /// Step a band offset by whole wheel notches and report whether the view would actually MOVE.
        /// Returns the new offset; <paramref name="moved"/> is false when the notch changed no step —
        /// either the allowance saturated, or the ladder itself ran out under a very wide hull. The
        /// caller refuses the nudge in that case, so a blocked notch never re-frames and the camera
        /// never twitches at a clamp.
        /// </summary>
        public static int StepBandOffset(int currentOffset, int notches, float ruledWorldHeightMeters,
                                         int stopsCloser, int stopsWider, int ppu, int screenHeightPx,
                                         out bool moved)
        {
            int from = ClampBandOffset(currentOffset, stopsCloser, stopsWider);
            int to = ClampBandOffset(from + notches, stopsCloser, stopsWider);
            moved = to != from
                    && BandStep(ruledWorldHeightMeters, to, stopsCloser, stopsWider, ppu, screenHeightPx)
                       != BandStep(ruledWorldHeightMeters, from, stopsCloser, stopsWider, ppu, screenHeightPx);
            return moved ? to : from;
        }

        /// <summary>
        /// Whole notches out of a raw scroll reading, carrying the remainder in
        /// <paramref name="carry"/> so a fine-grained device is accumulated instead of quantised away.
        ///
        /// <para><b>Why an accumulator and not <c>Mathf.Sign</c>.</b> A mouse wheel reports ±120 per
        /// detent, but a trackpad's two-finger scroll reports a stream of small values — sign alone
        /// would fire a whole tier per FRAME there, blowing through the range in a flick. Threshold and
        /// carry gives both devices the same feel: one tier per detent, and a trackpad has to travel
        /// the same distance to earn one.</para>
        ///
        /// <para>Deterministic and frame-rate independent by construction — it reads no clock and the
        /// carry is the only state. A non-positive <paramref name="unitsPerNotch"/> is treated as
        /// "every non-zero reading is one notch" rather than dividing by zero.</para>
        /// </summary>
        public static int NotchesFromScroll(ref float carry, float scrollY, float unitsPerNotch)
        {
            if (unitsPerNotch <= 0f)
            {
                carry = 0f;
                return scrollY > 0f ? 1 : (scrollY < 0f ? -1 : 0);
            }

            // A reversal must not be paid for out of the other direction's banked carry: flicking up
            // then down should step down immediately, not spend three notches undoing the bank.
            if (carry != 0f && Mathf.Sign(scrollY) != Mathf.Sign(carry) && scrollY != 0f) carry = 0f;

            carry += scrollY;
            int notches = (int)(carry / unitsPerNotch);   // truncates toward zero — the sign is preserved
            carry -= notches * unitsPerNotch;
            return notches;
        }
    }
}
