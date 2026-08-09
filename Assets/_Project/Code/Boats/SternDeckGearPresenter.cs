using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>THE STERN DECK, DRAWN</b> — the working furniture bolted to one hull, the pots stacked on her,
    /// the pot in play, and the clip the hands are working them with. The presentation half of the owner's
    /// gear cycle (canon M2-33): come up on your buoy → gaff the line → work the hauler → trap aboard on
    /// the stern deck → empty → chop/re-bait → stack → toss to re-set.
    ///
    /// <para><b>It owns no state machine and no payout.</b> WHAT is caught, what it is worth and whether a
    /// pot may be set are decided in Fishing and stay there (rule 5) — this component reads the beat off a
    /// Core snapshot (<see cref="DeckLoopStateChanged"/>, plus <see cref="TrapHaulStateChanged"/> for the
    /// line coming in) and draws it. It never publishes a sim event, never writes a save, and a listener
    /// that did would be writing sim from a picture.</para>
    ///
    /// <para><b>Every position comes from DATA, and that is the whole point.</b> Mounts and capacities
    /// from this hull's <see cref="BoatDeckGearDef"/>, stations from the kit's
    /// <see cref="DeckGearKitDef"/>, tier steps and slews from the POT's own Def, the projection from the
    /// hull's own bake elevation. There is not one mount, capacity, tier step or footprint in this file.
    /// The alternative — one rectangle typed into a C# file — was right for exactly one boat, which is the
    /// <see cref="BoatDeckDef"/> lesson restated for the gear that stands on the deck.</para>
    ///
    /// <para><b>The facing is DERIVED, never inherited</b> (#457). Every operator heading is
    /// <see cref="DeckGearPlacement.OperatorHeading"/> of the hull's DRAWN heading, resolved fresh each
    /// frame. Nothing here caches a facing, because a cached facing is how a figure keeps her old heading
    /// while the hull turns under her.</para>
    ///
    /// <para><b>A quantity is never a clock</b> (the #469 rule). The hauler's hands are seeked from the
    /// LINE hauled and the bench's from the ANIMALS worked, through
    /// <see cref="CharacterClipPlayer.PlayDriven"/> + <see cref="CharacterClipPlayer.SeekFrame"/>. Only
    /// the one-shots — landing her, stacking her, tossing her — are played off the clock, because those
    /// genuinely have a duration.</para>
    ///
    /// <para><b>Everything drawn takes a deck SLOT, and a refusal is never eaten.</b> Each piece of gear,
    /// each STACK and the pot in play claims an occupant slot on the hull, publishes where it stands, and
    /// consults the id it must discard against — the publish-then-consult protocol
    /// <see cref="IDeckOccupantSlots"/> documents. A stack is ONE occupant however many pots are on it:
    /// every tier stands on the same ground point and differs only in height, so one band hides all of
    /// them, and that is the budget the seam's twelve slots were measured against. Slots are HELD while
    /// the thing is drawn and RELEASED when it goes, so a cycle cannot leak them; and a deck that has run
    /// out says so in the log rather than quietly drawing one pot fewer than it was asked for.</para>
    ///
    /// <para><b>Art may be absent, and this must keep working when it is.</b> The deck-gear bake lands on
    /// its own schedule. Until it does, every kind draws a code-built greybox stand-in at exactly the
    /// measured position, the clips refuse (<see cref="CharacterClipPlayer.CanPlay"/> is false on an
    /// unbaked clip) and the fisher keeps whatever animator drew her before. The geometry is the
    /// deliverable; the picture follows it.</para>
    ///
    /// <para><b>Rules 4 and 7.</b> Boats-lane component on the boat root; it reaches Fishing and Player
    /// through Core only (a value-struct snapshot, and a <see cref="CharacterClipPlayer"/> — a Core type —
    /// handed in as a plain reference, the same way the haul's rope anchor is). Renderers are pooled and
    /// built once, the property block is reused, the occluder id is written only when it changes, and the
    /// per-frame path allocates nothing.</para>
    /// </summary>
    [DisallowMultipleComponent]
    // A READER of the hull's drawn facing and her rock, exactly like DeckRiderVisual — and for the same
    // reason it runs at 100: the gear stands on the deck the hull has already finished drawing.
    [DefaultExecutionOrder(100)]
    public sealed class SternDeckGearPresenter : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("This hull's deck gear — where her furniture is bolted and how many pots she stacks. " +
                 "NULL = this boat carries no deck gear and the component is completely inert, which is " +
                 "the honest answer for an open boat.")]
        [SerializeField] private BoatDeckGearDef _gear;

        [Tooltip("The kit the furniture is drawn from — its stations and its sheets. NULL = no stations " +
                 "are known, so nothing is placed (the geometry, not the art, is what this needs it for).")]
        [SerializeField] private DeckGearKitDef _kit;

        [Tooltip("The WORKER's clip seam — the deck-walking fisher's own CharacterClipPlayer. Null = the " +
                 "deck draws its gear and its pots and drives no clip at all, and whatever animator was " +
                 "already drawing her keeps doing so.")]
        [SerializeField] private CharacterClipPlayer _worker;

        [Header("Clip pacing (tunables, rule 6 — the art's frame counts come from the art)")]
        [Tooltip("Times round the HAULER clip that one whole haul (line 0→1) turns the hands. The rate " +
                 "breathes with the take for free, because the position is seeked from the line rather " +
                 "than run off a timer.")]
        [SerializeField, Min(0f)] private float _haulerCyclesPerHaul = 6f;

        [Tooltip("Times round the BENCH clip that emptying a whole pot works the hands — so a four-animal " +
                 "pot is four times the work of a one-animal pot, which is what the player is doing.")]
        [SerializeField, Min(0f)] private float _benchCyclesPerPot = 4f;

        [Tooltip("Times round the CHOP clip that re-baiting one pot works the hands.")]
        [SerializeField, Min(0f)] private float _chopCyclesPerBait = 2f;

        [Tooltip("Seconds a one-shot beat (landing her, stacking her, tossing her) is stretched to. The " +
                 "clip fits the move rather than the move being retuned to fit the art.")]
        [SerializeField, Min(0.01f)] private float _oneShotSeconds = 0.6f;

        [Header("Greybox stand-ins (used only until the kit's sheets bake)")]
        [Tooltip("Pixels-per-unit of the code-built stand-in silhouettes — the project's 32, so a " +
                 "stand-in is the size the real cell will be.")]
        [SerializeField, Min(1f)] private float _greyboxPixelsPerUnit = 32f;

        /// <summary>The shader an occludable deck sprite wears, and the two ids it discards against —
        /// named here rather than reached for through Art, which Boats may not reference (rule 4). The
        /// same string contract <c>DeckRiderVisual</c> spells on the Player side of the seam.</summary>
        private const string OccludedSpriteShader = "HiddenHarbours/DeckOccludedSprite";
        private static readonly int DeckOccluderIdProperty = Shader.PropertyToID("_HHDeckOccluderId");
        private static readonly int DeckOccluderIdTopProperty = Shader.PropertyToID("_HHDeckOccluderIdTop");

        // ---- one drawn thing on the deck ----------------------------------------------------------

        /// <summary>A pooled renderer plus the deck slot it holds. Built once, reused for the life of the
        /// boat — a stack that grows and shrinks every cycle must not churn GameObjects (rule 7).</summary>
        private sealed class Piece
        {
            public SpriteRenderer Renderer;
            public int Slot = -1;
            public bool Live;              // drawn THIS frame — the release edge is read off this
            public float IdWritten = -1f;
            public float TopWritten = -1f;
            public bool BlockValid;
        }

        private readonly List<Piece> _pieces = new();
        private MaterialPropertyBlock _block;
        private Material _occluderMaterial;
        private IDeckOccupantSlots _heldSlots;   // the slot host every live claim belongs to
        private Transform _pool;

        // The live beat, as published. Nothing here is sim state and none of it is saved.
        private DeckLoopState _loop = DeckLoopState.Idle;
        private float _line01;
        private bool _hauling;

        // The one-shot in flight (Aboard / Stacking / Setting): started once on the stage EDGE, never
        // restarted per frame — a one-shot re-played every frame is a one-shot frozen on frame 0.
        private SternDeckStage _clipStage = SternDeckStage.None;
        private bool _clipOurs;

        // Overflow is loud ONCE per hull, not once per frame: a full deck would otherwise write a
        // warning every LateUpdate for as long as the player stood there (rule 7, and readability).
        private bool _warnedOverflow;

        /// <summary>How many claims this deck asked for and did not get on the last frame it drew. The
        /// number a test reads to prove a full deck refuses OUT LOUD instead of silently over-claiming.
        /// 0 whenever everything asked for got a slot.</summary>
        public int RefusedClaims { get; private set; }

        /// <summary>How many things this deck drew on the last frame — gear, stacked pots and the pot in
        /// play. For tests and tooling.</summary>
        public int DrawnCount { get; private set; }

        /// <summary>The beat last published to this deck. For tests and tooling.</summary>
        public DeckLoopState Loop => _loop;

        /// <summary>The stage whose clip this presenter currently owns on the worker's seam, or
        /// <see cref="SternDeckStage.None"/>. For tests and tooling.</summary>
        public SternDeckStage DrivenStage => _clipOurs ? _clipStage : SternDeckStage.None;

        /// <summary>Wire the deck in one call (the editor builder / tests).</summary>
        public void Configure(BoatDeckGearDef gear, DeckGearKitDef kit, CharacterClipPlayer worker)
        {
            _gear = gear;
            _kit = kit;
            _worker = worker;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DeckLoopStateChanged>(OnDeckLoopChanged);
            EventBus.Subscribe<TrapHaulStateChanged>(OnHaulStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DeckLoopStateChanged>(OnDeckLoopChanged);
            EventBus.Unsubscribe<TrapHaulStateChanged>(OnHaulStateChanged);
            // Tearing the deck down must not strand a slot on the hull: a held slot is one the next
            // thing to stand there cannot have, and twelve teardowns would exhaust her.
            ReleaseAll();
            EndClip();
        }

        /// <summary>Public so tests can drive the beat through the same path the bus uses (the
        /// <c>PlayerHaulAnimator</c> convention — no play-mode lifecycle needed).</summary>
        public void OnDeckLoopChanged(DeckLoopStateChanged e)
        {
            _loop = e.State;
            RememberPot(e.State.Pot);
        }

        /// <summary>
        /// Keep the last pot the deck was TOLD about, rather than only the one in play.
        ///
        /// <para>A stack outlives the pot that built it: the moment the cycle goes idle the beat carries
        /// <see cref="DeckPotLook.Unknown"/>, and a deck that took that literally would drop the art off
        /// every pot already stacked on it. A half-authored Def is treated the same way — a step of 0 is
        /// "unknown", not "collapse the stack into one pot".</para>
        /// </summary>
        private void RememberPot(in DeckPotLook look)
        {
            if (look.Cell != null) _potCell = look.Cell;
            if (look.StackStepMeters > 0f) _stackStepMeters = look.StackStepMeters;
            if (look.Slew != null) _stackSlew = look.Slew;
            if (look.SlewMeters >= 0f) _stackSlewMeters = look.SlewMeters;
        }

        /// <summary>Public for the same reason. The haul's own snapshot carries the LINE, which is what
        /// paces the hauler's hands — the deck never times them.</summary>
        public void OnHaulStateChanged(TrapHaulStateChanged e)
        {
            _hauling = e.State.Phase == TrapHaulPhase.Hauling;
            _line01 = e.State.Line01;
        }

        private void LateUpdate() => Draw();

        // ---- the one path --------------------------------------------------------------------------

        /// <summary>
        /// Place and draw everything on this deck for the frame, then hand the worker's clip the beat.
        /// Public and input-free so a test can step it without a play-mode loop.
        /// </summary>
        public void Draw()
        {
            RefusedClaims = 0;
            DrawnCount = 0;

            if (_gear == null || _kit == null) { ReleaseAll(); EndClip(); return; }

            IBoatHullPresenter hull = LiveHull();
            IDeckOccupantSlots slots = hull != null ? hull.DeckOccupants : NoDeckOccupantSlots.Instance;
            // A hull swapped under the gear (the dev picker does exactly that) hands us a different slot
            // host. Let the old one go first, or every piece keeps splitting an image nobody stands in.
            if (_heldSlots != null && !ReferenceEquals(_heldSlots, slots)) ReleaseAll();
            _heldSlots = slots;

            float heading = hull != null ? hull.DrawnHeadingDegrees() : 0f;
            float elevation = hull != null ? hull.BakeElevationDegrees : DeckAreaMath.PlanViewElevationDegrees;
            Vector3 boatPos = transform.position;

            for (int i = 0; i < _pieces.Count; i++) _pieces[i].Live = false;
            int next = 0;

            // (1) THE FURNITURE. Bolted where the hull's data says, facing where its own data says.
            if (_gear.Mounts != null)
            {
                for (int i = 0; i < _gear.Mounts.Length; i++)
                {
                    BoatDeckGearMount m = _gear.Mounts[i];
                    if (m == null) continue;
                    DeckGearKitEntry entry = _kit.EntryFor(m.Kind);
                    if (entry == null) continue;          // a kind this kit does not draw — absence is data
                    DrawAt(ref next, m.MountLocalMeters,
                           entry.CellFor(m.Dir, _kit.FacingCount), GreyboxFor(m.Kind),
                           boatPos, heading, elevation, slots);
                }
            }

            // (2) THE STACKS. Deck first, then the washboard — and the tier step is the POT's, not the
            //     hull's, so a cone nests where a bow-top does not.
            DrawStacks(ref next, boatPos, heading, elevation, slots);

            // (3) THE POT IN PLAY, standing on the surface the station's hands work at.
            if (_loop.PotInPlay) DrawPotInPlay(ref next, boatPos, heading, elevation, slots);

            // Anything that drew last frame and not this one gives its slot back NOW.
            for (int i = 0; i < _pieces.Count; i++)
            {
                Piece p = _pieces[i];
                if (p.Live) continue;
                if (p.Renderer != null && p.Renderer.enabled) p.Renderer.enabled = false;
                ReleaseSlot(p);
            }

            DrawnCount = next;
            DriveWorkerClip(heading);
        }

        // ---- placement -----------------------------------------------------------------------------

        private void DrawStacks(ref int next, Vector3 boatPos, float heading, float elevation,
                                IDeckOccupantSlots slots)
        {
            int remaining = _loop.StackedPots;
            if (remaining <= 0) return;

            remaining = DrawStackOn(DeckStackSurface.Deck, remaining, ref next,
                                    boatPos, heading, elevation, slots);
            remaining = DrawStackOn(DeckStackSurface.Washboard, remaining, ref next,
                                    boatPos, heading, elevation, slots);

            // More pots than this hull's own CAPS will take. That is a REFUSAL and it is said out loud —
            // the alternative is a deck that quietly draws fewer pots than it was told it had.
            if (remaining > 0)
            {
                RefusedClaims += remaining;
                WarnOnce($"[SternDeck] {name}: {remaining} pot(s) over this hull's stacking capacity " +
                         $"({_gear.CapacityOf(DeckStackSurface.Deck)} on deck + " +
                         $"{_gear.CapacityOf(DeckStackSurface.Washboard)} on the washboard). They are not " +
                         "drawn — nothing is stacked in mid-air.");
            }
        }

        /// <summary>
        /// Fill one surface up to its rig-measured capacity and hand back what would not fit.
        ///
        /// <para><b>A STACK is one occupant, not one per pot</b>, and that is not a saving — it is the
        /// truth. Every tier of a stack stands on the same ground point and differs only in HEIGHT, so
        /// they all sit at the same view depth and the same band hides all of them; handing each pot its
        /// own slot would spend five of the hull's twelve saying one thing five times. It is also the
        /// budget the twelve was measured against (#481 counted the two trap stacks as two occupants, not
        /// as seven pots), so claiming per pot would overrun a deck the seam had sized correctly.</para>
        ///
        /// <para>So tier 0 claims, and every tier above it draws with the id tier 0 was given.</para>
        /// </summary>
        private int DrawStackOn(DeckStackSurface surface, int wanted, ref int next,
                                Vector3 boatPos, float heading, float elevation, IDeckOccupantSlots slots)
        {
            BoatDeckStackBase b = _gear.StackBaseFor(surface);
            if (b == null || wanted <= 0) return wanted;

            int capacity = Mathf.Max(0, b.Capacity);
            int here = Mathf.Min(wanted, capacity);
            Piece anchor = null;
            for (int tier = 0; tier < here; tier++)
            {
                Vector3 local = DeckGearPlacement.StackSlot(b.BaseLocalMeters, tier,
                                                            _stackStepMeters, _stackSlew,
                                                            _stackSlewMeters, b.Dir);
                if (tier == 0)
                    anchor = DrawAt(ref next, local, _potCell, _potGreybox,
                                    boatPos, heading, elevation, slots);
                else
                    DrawSharing(ref next, local, _potCell, _potGreybox, boatPos, heading, elevation, anchor);
            }
            return wanted - here;
        }

        private void DrawPotInPlay(ref int next, Vector3 boatPos, float heading, float elevation,
                                   IDeckOccupantSlots slots)
        {
            // She stands at whichever station the beat is worked at, ON its work surface — the station's
            // workZ is a WORLD METRE above the hull frame, so a pot on a bench sits on the bench and a pot
            // on the deck sits on the deck, without either number appearing here.
            DeckGearKind kind = StationKindFor(_loop.Stage);
            BoatDeckGearMount mount = _gear.MountFor(kind);
            DeckGearKitEntry entry = _kit.EntryFor(kind);

            Vector3 local;
            if (mount != null && entry != null)
            {
                local = new Vector3(mount.MountLocalMeters.x, mount.MountLocalMeters.y, entry.WorkZMeters);
            }
            else
            {
                // No station for this beat on this hull — she lies on the bottom of the stack's own base,
                // which is measured deck, not a guess. Still data, still per hull.
                BoatDeckStackBase b = _gear.StackBaseFor(DeckStackSurface.Deck)
                                      ?? _gear.StackBaseFor(DeckStackSurface.Washboard);
                if (b == null) return;
                local = b.BaseLocalMeters;
            }
            DrawAt(ref next, local, _potCell, _potGreybox, boatPos, heading, elevation, slots);
        }

        /// <summary>
        /// Put one thing on the deck: project its hull-local metres through the hull's own drawn heading
        /// and bake elevation, claim its slot, publish where it stands, and discard against the id that
        /// comes back. The publish-then-consult order is the protocol's, not a preference — the id
        /// depends on this thing's depth RANK among everything else aboard and cannot be known before it
        /// has said where it is.
        /// </summary>
        private Piece DrawAt(ref int next, Vector3 local, Sprite cell, Sprite fallback,
                             Vector3 boatPos, float heading, float elevation, IDeckOccupantSlots slots)
        {
            Piece p = Place(ref next, local, cell, fallback, boatPos, heading, elevation);
            if (p == null) return null;

            // The claim is HELD, not retaken per frame — but re-asserting it is idempotent and is what
            // makes a piece survive the hull being disabled and re-enabled under it (which empties every
            // slot and takes a fresh id block).
            p.Slot = slots.Claim(p);
            if (p.Slot < 0)
            {
                // Refused. The piece draws UN-OCCLUDED, which is the picture that shipped before any of
                // this existed — never half-hidden, and never silently. NoDeckOccupantSlots refuses
                // silently by design (a sprite hull is most of the fleet), so only a real hull is counted.
                if (slots.Capacity > 0)
                {
                    RefusedClaims++;
                    WarnOnce($"[SternDeck] {name}: the hull is out of deck-occupant slots " +
                             $"({slots.ActiveCount}/{slots.Capacity} standing). The gear beyond them draws " +
                             "un-occluded rather than being hidden wrongly.");
                }
                WriteOccluder(p, 0f, 0f);
                return p;
            }
            slots.Set(p.Slot, p, local, true);
            WriteOccluder(p, slots.OccluderId(p.Slot), slots.OccluderIdTop);
            return p;
        }

        /// <summary>Draw one more tier of a stack that has already claimed: same ground point, same view
        /// depth, so the same band hides it and it needs no slot of its own.</summary>
        private void DrawSharing(ref int next, Vector3 local, Sprite cell, Sprite fallback,
                                 Vector3 boatPos, float heading, float elevation, Piece anchor)
        {
            Piece p = Place(ref next, local, cell, fallback, boatPos, heading, elevation);
            if (p == null) return;
            ReleaseSlot(p);   // it may have claimed on an earlier frame as somebody else's tier 0
            if (anchor != null) WriteOccluder(p, anchor.IdWritten, anchor.TopWritten);
            else WriteOccluder(p, 0f, 0f);
        }

        /// <summary>Project one thing's hull-local metres through the hull's own drawn heading and bake
        /// elevation, and put its renderer there. The half of the draw that has nothing to do with
        /// occlusion, shared by the claiming and the sharing paths.</summary>
        private Piece Place(ref int next, Vector3 local, Sprite cell, Sprite fallback,
                            Vector3 boatPos, float heading, float elevation)
        {
            Piece p = PieceAt(next++);
            SpriteRenderer sr = p.Renderer;
            if (sr == null) return null;

            Vector2 offset = DeckAreaMath.DeckToWorld(new Vector2(local.x, local.y), local.z,
                                                      heading, elevation);
            Vector3 world = boatPos + new Vector3(offset.x, offset.y, 0f);
            sr.transform.position = world;
            sr.sortingOrder = DecorOrderFor(world.y);

            Sprite s = cell != null ? cell : fallback;
            if (sr.sprite != s) sr.sprite = s;
            if (!sr.enabled) sr.enabled = true;
            p.Live = true;
            return p;
        }

        /// <summary>Which station's surface a beat is worked at. A beat with no station of its own —
        /// landing her, stacking her, tossing her — answers with the banding bench, which is where a pot
        /// physically sits while she is being handled.</summary>
        private static DeckGearKind StationKindFor(SternDeckStage stage) => stage switch
        {
            SternDeckStage.Hauling => DeckGearKind.HaulerStation,
            SternDeckStage.Baiting => DeckGearKind.ChopBoard,
            _ => DeckGearKind.BandingTable,
        };

        // ---- the worker's hands --------------------------------------------------------------------

        /// <summary>
        /// Hand the beat to the worker's clip seam: the driven loops seeked from their QUANTITY, the
        /// one-shots played once on the stage edge, and the facing derived from the hull's drawn heading
        /// every call.
        ///
        /// <para>Refuses gracefully at every step — no worker wired, no station on this hull, no baked
        /// clip — and a refusal simply leaves whatever animator was drawing the fisher drawing her. That
        /// is the state the deck-gear bake lands into, so it is the state that must be a legal picture.</para>
        /// </summary>
        private void DriveWorkerClip(float hullHeading)
        {
            SternDeckStage stage = _hauling && _gear.HasHaulerStation() ? SternDeckStage.Hauling : _loop.Stage;
            CharacterClip clip = SternDeckLoop.ClipFor(stage);

            if (_worker == null || clip == CharacterClip.None) { EndClip(); return; }

            // Another driver has the seam — a boarding vault is a whole-body move over a rail and wins
            // outright. Stand down and touch nothing; the next frame takes it back on its own, because
            // the re-take below tests what is actually running rather than a flag.
            if (_worker.IsPlaying && _clipOurs && _worker.Clip != clip) _clipOurs = false;
            if (_worker.IsPlaying && !_clipOurs && _worker.Clip != clip) return;

            if (!_worker.CanPlay(clip)) { EndClip(); return; }   // unbaked — the fallback IS the behaviour

            DeckGearKind kind = StationKindFor(stage);
            BoatDeckGearMount mount = _gear.MountFor(kind);
            DeckGearKitEntry entry = _kit.EntryFor(kind);
            if (mount == null || entry == null) { EndClip(); return; }

            // DERIVED, every call: the deck's drawn bearing + the gear's own + the station's turn. The
            // hauler is the one station whose operator faces OUTBOARD, and that is the station's data
            // saying so, not a branch here.
            float heading = DeckGearPlacement.OperatorHeading(hullHeading, mount.Dir, entry.Station());

            if (SternDeckLoop.IsQuantityPaced(stage))
            {
                var def = _worker.ResolvedVisual();
                int frames = def != null ? def.ClipFrameCount(clip) : 0;
                if (frames <= 0) { EndClip(); return; }

                float quantity = stage == SternDeckStage.Hauling ? _line01 : _loop.Worked01;
                float position = SternDeckLoop.DrivenFramePosition(quantity, CyclesFor(stage), frames);

                bool ours = _clipOurs && _worker.IsDriven && _worker.Clip == clip;
                if (!ours)
                {
                    if (!_worker.PlayDriven(clip, heading, position)) { EndClip(); return; }
                    _clipOurs = true;
                    _clipStage = stage;
                    return;
                }
                _worker.SetHeading(heading);
                _worker.SeekFrame(position);       // the LINE moves the hands; no clock touches them
                _clipStage = stage;
                return;
            }

            // A one-shot has a duration, so it is honestly played off the clock — but started ONCE, on
            // the stage EDGE. Re-playing it every frame would freeze it on frame 0; re-playing it after
            // it has played out would loop a move the cycle means to happen exactly once. Both are the
            // same guard: while this stage is still OURS the clip is never started again, whether it is
            // in flight or already finished and handed back.
            if (_clipOurs && _clipStage == stage)
            {
                if (_worker.IsPlaying && _worker.Clip == clip) _worker.SetHeading(heading);
                return;
            }
            // Fire-and-forget (no hold): a one-shot that has played out hands the renderer back and the
            // fisher returns to her walk sprite, which is what landing a pot and letting go looks like.
            if (!_worker.Play(clip, heading, _oneShotSeconds)) { EndClip(); return; }
            _clipOurs = true;
            _clipStage = stage;
        }

        private float CyclesFor(SternDeckStage stage) => stage switch
        {
            SternDeckStage.Hauling => _haulerCyclesPerHaul,
            SternDeckStage.Emptying => _benchCyclesPerPot,
            _ => _chopCyclesPerBait,
        };

        /// <summary>Hand the clip seam back, idempotently. Only ever stops a clip THIS component started
        /// — the fisher's own haul animator and the boarding move both use the same seam, and stopping
        /// theirs would leave them frozen mid-move.</summary>
        private void EndClip()
        {
            if (!_clipOurs) return;
            _clipOurs = false;
            _clipStage = SternDeckStage.None;
            if (_worker != null && _worker.IsPlaying) _worker.Stop();
        }

        // ---- the pot's own art + stacking data -----------------------------------------------------

        // The pot being drawn, and how she stacks — described across the Core seam by the lane that owns
        // trap content (Boats may not reference Fishing, rule 4) and REMEMBERED, because a stack outlives
        // the pot that built it. Until a pot has been described the greybox stand-in draws at the measured
        // positions, which is the state the pot bake lands into.
        //
        // The seeded step is the kit's own smallest tier (a wire pot's 0.38 m) so an undescribed stack is
        // drawn tight rather than floating; the first described pot replaces it.
        private Sprite _potCell;
        private float _stackStepMeters = 0.38f;
        private int[] _stackSlew;
        private float _stackSlewMeters;

        // ---- slots, renderers and the occluder id --------------------------------------------------

        private Piece PieceAt(int index)
        {
            while (_pieces.Count <= index)
            {
                if (_pool == null)
                {
                    var poolGo = new GameObject("SternDeckGear");
                    poolGo.transform.SetParent(transform, false);
                    _pool = poolGo.transform;
                }
                var go = new GameObject("DeckGear_" + _pieces.Count);
                go.transform.SetParent(_pool, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.enabled = false;
                Material occluder = EnsureOccluderMaterial();
                if (occluder != null) sr.sharedMaterial = occluder;
                _pieces.Add(new Piece { Renderer = sr });
            }
            return _pieces[index];
        }

        private void WriteOccluder(Piece p, float id, float top)
        {
            if (p.Renderer == null) return;
            if (p.BlockValid && p.IdWritten == id && p.TopWritten == top) return;

            _block ??= new MaterialPropertyBlock();
            p.Renderer.GetPropertyBlock(_block);
            _block.SetFloat(DeckOccluderIdProperty, id);
            _block.SetFloat(DeckOccluderIdTopProperty, top);
            p.Renderer.SetPropertyBlock(_block);
            p.IdWritten = id;
            p.TopWritten = top;
            p.BlockValid = true;
        }

        private void ReleaseSlot(Piece p)
        {
            if (p.Slot < 0) return;
            _heldSlots?.Release(p.Slot, p);
            p.Slot = -1;
            WriteOccluder(p, 0f, 0f);
        }

        /// <summary>Give every slot back and take every piece off screen. Called when the deck goes away,
        /// when its hull is swapped, and on teardown — a stranded slot is one the next thing aboard
        /// cannot have.</summary>
        private void ReleaseAll()
        {
            for (int i = 0; i < _pieces.Count; i++)
            {
                Piece p = _pieces[i];
                if (p.Renderer != null && p.Renderer.enabled) p.Renderer.enabled = false;
                p.Live = false;
                ReleaseSlot(p);
            }
            _heldSlots = null;
            _warnedOverflow = false;
        }

        /// <summary>A missing shader is never fatal: the gear keeps whatever material it had and draws
        /// un-occluded — the picture that shipped before any of this existed. A magenta bait box would be
        /// far worse than a bait box visible through a wheelhouse.</summary>
        private Material EnsureOccluderMaterial()
        {
            if (_occluderMaterial != null) return _occluderMaterial;
            Shader shader = Shader.Find(OccludedSpriteShader);
            if (shader == null)
            {
                Debug.LogWarning($"[SternDeck] Shader '{OccludedSpriteShader}' not found — the deck gear " +
                                 "will draw through the boat's own superstructure (the pre-fix picture). " +
                                 "Nothing else is affected.");
                return null;
            }
            _occluderMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _occluderMaterial;
        }

        private void OnDestroy()
        {
            if (_occluderMaterial == null) return;
            if (Application.isPlaying) Destroy(_occluderMaterial);
            else DestroyImmediate(_occluderMaterial);
            _occluderMaterial = null;
        }

        private void WarnOnce(string message)
        {
            if (_warnedOverflow) return;
            _warnedOverflow = true;
            Debug.LogWarning(message);
        }

        /// <summary>The live presenter for the hull worn RIGHT NOW — the same read
        /// <c>DeckWalkController</c> and <c>DeckRiderVisual</c> make, and it must stay the same read: the
        /// dev picker re-skins a boat IN PLACE, so a presenter cached once would go on answering for a
        /// hull that is no longer drawn.</summary>
        private IBoatHullPresenter LiveHull()
        {
            var host = GetComponent<BoatHullPresenterHost>();
            return host != null ? host.Presenter : null;
        }

        /// <summary>The decor band's own Y → order rule, stated from the band constants rather than
        /// duplicating <c>YSortSprite</c>'s tunables — Boats may not reference Art (rule 4), and the band
        /// is the shared contract Core keeps for exactly this (ADR 0032).</summary>
        private static int DecorOrderFor(float worldY)
            => Mathf.Clamp(Mathf.RoundToInt(SortingBands.DecorBase - worldY * SortingBands.OrdersPerMetre),
                           SortingBands.DecorFloor, SortingBands.DecorCeiling);

        // ---- code-built stand-ins (cached; the kit's sheets replace them wholesale) -----------------

        // Per INSTANCE, not static: a stand-in is built at this deck's own serialized
        // pixels-per-unit, so a shared cache would hand a second deck the first one's scale.
        private readonly Dictionary<DeckGearKind, Sprite> _greyboxCache = new();
        private Sprite _potGreybox;

        private Sprite GreyboxFor(DeckGearKind kind)
        {
            if (_greyboxCache.TryGetValue(kind, out Sprite cached) && cached != null) return cached;
            // Footprints in tenths of a metre, so a stand-in is roughly the size of the cell that will
            // replace it. Greybox art, not balance data.
            Sprite built = kind switch
            {
                DeckGearKind.HaulerStation => BuildBox(15, 14, new Color(0.36f, 0.38f, 0.42f, 1f)),
                DeckGearKind.BandingTable => BuildBox(11, 9, new Color(0.52f, 0.42f, 0.30f, 1f)),
                DeckGearKind.ChopBoard => BuildBox(9, 8, new Color(0.60f, 0.50f, 0.36f, 1f)),
                DeckGearKind.BaitBin => BuildBox(7, 9, new Color(0.30f, 0.36f, 0.34f, 1f)),
                _ => BuildBox(9, 5, new Color(0.34f, 0.40f, 0.38f, 1f)),
            };
            _greyboxCache[kind] = built;
            return built;
        }

        private void Awake()
        {
            // The pot's own stand-in is per component rather than static: it is built at this deck's
            // pixels-per-unit, which is a serialized field.
            _potGreybox = BuildBox(12, 6, new Color(0.42f, 0.33f, 0.22f, 1f));
        }

        /// <summary>A flat filled box with a one-pixel keyline, pivoted on GROUND CONTACT (bottom centre)
        /// so a stand-in sits on the deck exactly where its cell will.</summary>
        private Sprite BuildBox(int w, int h, Color color)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Color edge = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 1f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, (x == 0 || y == 0 || x == w - 1 || y == h - 1) ? edge : color);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f),
                                 Mathf.Max(1f, _greyboxPixelsPerUnit));
        }
    }
}
