using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.App
{
    /// <summary>
    /// ⭐⭐ <b>SHE WALKS THE DECK OF THE BOAT THAT IS CARRYING HER.</b> The twin of
    /// <see cref="ArrivalCabinWalk"/>, one deck up: the passenger's place on Armand's cape stops being a
    /// constant she is pinned to and becomes a hull-local point her own keys move, clamped to his
    /// measured planking, riding the hull as he turns.
    ///
    /// <para><b>The defect it closes (owner playtest, 2026-09-04):</b> <i>"the player is unable to walk on
    /// the boat deck in the new intro, going outside locks them in place."</i> Nothing was broken — the
    /// opening was built as <i>walk the cabin → come up → ride in → step ashore</i>, and walking the deck
    /// under way was simply never built. <c>ArrivalOpening._passengerDeckOffset</c> was written once, when
    /// she crossed the threshold, and only ever read afterwards.</para>
    ///
    /// <para><b>⛔ IT ADDS NO SECOND CLAMP, NO SECOND PROJECTION AND NO SECOND BEARING.</b> That is the
    /// whole design. Every quantity here is computed by the component that already owns it:</para>
    /// <list type="bullet">
    ///   <item>the step is her key in the hull's frame as <see cref="DeckWalkController.HeldInHullFrame"/>
    ///   turns it — the direction <see cref="DeckWalkController.StepOnDeckPolygon"/> steps along — and the
    ///   clamp is the deck's own <see cref="BoatDeckDef.ClampToWalkable"/>, the one that step calls: the
    ///   authored polygons, in the hull's own metres;</item>
    ///   <item>the projection onto the drawn hull is <see cref="DeckAreaMath.DeckToWorld"/>, the same
    ///   foreshortened transform the deck walk places the player's own boat by;</item>
    ///   <item>the join at the threshold is <see cref="DeckWalkController.SeedDeckLocalPure"/> — the
    ///   iterative inverse of that projection, made public for this caller rather than restated;</item>
    ///   <item>the facing is <see cref="DeckRiderFacingMath"/>'s composition of a deck bearing and the
    ///   hull's drawn heading, which is what makes a passenger standing still <b>turn with the boat</b>
    ///   instead of losing her reference to it.</item>
    /// </list>
    /// <para>One quantity, one computation: a second clamp is precisely the shape this project has paid
    /// for before.</para>
    ///
    /// <para><b>⭐ His doorway (owner ruling D1 (a), 2026-09-30).</b> Within one clear width of his aft
    /// door, a key held within 30° of its axis is bent toward the opening
    /// (<see cref="BoatCabinDoor.SteerHeld"/>). And the deck may not stand her INSIDE the house it is
    /// measured under: the cape's cockpit sole runs forward under the whole deckhouse, and a walker who
    /// could stand in the house's outline on deck would walk through its walls onto the bow and come at
    /// the doorway from the wrong side of its wall line. So a step that ends in the room is put back out
    /// of it by its nearest edge (<see cref="BoatCabinWalkMath.PushOutOfTheRoom"/>) — for the arrival's
    /// passenger only; the fleet's own deck walk steers from inside its wheelhouses.</para>
    ///
    /// <para><b>Why a plain class and not a component</b> — <see cref="ArrivalCabinWalk"/>'s reason,
    /// verbatim. It never writes her transform: it holds where she is standing and how fast, and
    /// <see cref="ArrivalOpening"/>'s one <c>LateUpdate</c> puts her there. Two things placing the player
    /// is the defect this codebase has already paid for twice. And it deliberately does NOT reach for a
    /// real <see cref="DeckWalkController"/>: the <c>ControlSwitcher</c> owns those and enables them by
    /// mode, the arrival never sets a mode (she is not aboard <i>her</i> boat), and a controller the
    /// switcher believes it has disabled would be a second writer by another name.</para>
    ///
    /// <para><b>⚠ A hull with no measured deck keeps the shipped seat.</b> <see cref="CanWalk"/> is false
    /// for her, <see cref="ArrivalOpening"/> falls back to its authored offset, and the arrival is
    /// unchanged — absence is data, the same law <see cref="ArrivalCabinWalk.TryOpen"/> keeps about
    /// rooms. The question is asked LIVE, every frame, because the deck arrives with the SKIN
    /// (<c>BoatHullSkinner</c> writes <see cref="BoatDeckAreas"/>) and the skinner runs after the spawn.
    /// </para>
    /// </summary>
    internal sealed class ArrivalDeckWalk
    {
        /// <summary>Below this the deck step is noise and her facing is HELD rather than re-derived — a
        /// fisher who stops keeps looking where she was going. <c>DeckRiderVisual._deckStepMinSpeed</c>'s
        /// own number, and not a feel knob: it answers "was that a real step or renderer jitter?", the
        /// same question in the same frame. A zero here would let <see cref="IsoCharacterMath.HeadingFor"/>
        /// take the bearing of a zero vector and snap a standing passenger to north.</summary>
        private const float StepNoiseFloorMetresPerSecond = 0.05f;

        /// <summary>Her hull. Held as the GameObject rather than the deck def, because the deck is a LIVE
        /// read (see the class note) — and because the dev hull picker re-skins a boat in place.</summary>
        private readonly GameObject _boat;

        private readonly float _walkSpeedMetresPerSecond;

        private Vector2 _deckLocal;
        private float _deckHeightMetres;
        private int _areaHint = -1;
        private float _deckBearingDegrees;
        private float _speedMetresPerSecond;
        private bool _seated;
        private Vector2 _held;
        private float _paceMetresPerSecond;
        private float _paceFromMetresPerSecond;
        private float _sinceSeedSeconds = float.PositiveInfinity;

        public ArrivalDeckWalk(GameObject boat, float walkSpeedMetresPerSecond)
        {
            _boat = boat;
            _walkSpeedMetresPerSecond = Mathf.Max(0f, walkSpeedMetresPerSecond);
            _paceMetresPerSecond = _walkSpeedMetresPerSecond;
        }

        /// <summary>This hull's imported walkable areas, read live off the boat root. Null until she is
        /// skinned, and null forever on a hull the rigs have never measured.</summary>
        public BoatDeckDef Deck => BoatDeckAreas.Resolve(_boat);

        /// <summary>⭐ <b>Is there planking under her to walk?</b> The one gate — live, so a hull skinned a
        /// frame or two after the spawn opens the walk the moment her deck arrives.</summary>
        public bool CanWalk
        {
            get
            {
                BoatDeckDef deck = Deck;
                return deck != null && deck.HasWalkableDeck();
            }
        }

        /// <summary>False until something has told this walk where she is standing. A walk that has not
        /// been seated must not place anybody: its <see cref="LocalPosition"/> is amidships on the keel,
        /// which is a point she never chose.</summary>
        public bool IsSeated => _seated;

        /// <summary>Where she is standing, in the hull's own metres (x abeam to starboard, y toward the
        /// bow) — the deck frame the polygons live in.</summary>
        public Vector2 LocalPosition => _deckLocal;

        /// <summary>How high above the keel the deck under her stands (m) — what lifts her up-screen onto
        /// a raised foredeck.</summary>
        public float HeightMetres => _deckHeightMetres;

        /// <summary>Her honest travelling speed: metres of DECK per second, which is the planking she
        /// actually crosses. Zero on a tick she took no step — including one spent pressed into a
        /// bulkhead, because a clamped step is no step — and on the tick she comes out through his doorway,
        /// the pace she came through at (<see cref="SeedFromCabin"/>).</summary>
        public float SpeedMetresPerSecond => _speedMetresPerSecond;

        /// <summary>The pace her keys walk her at, metres of deck per second: the deck's own walk speed, or
        /// on its way to it from the pace she came through his doorway at.</summary>
        public float PaceMetresPerSecond => _paceMetresPerSecond;

        /// <summary>The gait she carries in through his doorway (<see cref="ArrivalCabinWalk.SeedFromDeck"/>):
        /// her travelling speed, or the pace her key was asking for when the doorway cut the step short —
        /// at the wall line, or at the deck's edge short of it — so a figure walking through a doorway is
        /// never drawn slowing in it.</summary>
        public float GaitThroughTheDoorwayMetresPerSecond
            => Mathf.Max(_speedMetresPerSecond, _paceMetresPerSecond * Mathf.Min(1f, _held.magnitude));

        /// <summary>Her key this tick in the hull's frame, as her step turned it and before any doorway bent
        /// it — what his doorway is asked with. Zero for no key.</summary>
        public Vector2 HeldHullLocal => _held;

        /// <summary>Where she is looking RELATIVE TO THE DECK (0 = at the bow, +90 = to starboard). The
        /// half of her facing that only her own walking changes.</summary>
        public float DeckBearingDegrees => _deckBearingDegrees;

        /// <summary>
        /// ⭐ Her COMPASS facing on a hull drawn at <paramref name="drawnHeadingDegrees"/> — the two facts
        /// composed (<see cref="DeckRiderFacingMath.CompassHeading"/>), never integrated.
        ///
        /// <para>Two behaviours fall out of the composition with nothing to accumulate, and both are what
        /// the owner would expect of a woman standing on a boat: a passenger who is not walking
        /// <b>turns with the hull</b>, and one who is walking faces the way she is walking. It also means
        /// a walk seeded at bearing 0 reproduces the arrival's shipped picture exactly — she is looking
        /// along the boat, at the harbour she is arriving at — so nothing changes until she presses a
        /// key.</para>
        /// </summary>
        public float HeadingDegrees(float drawnHeadingDegrees)
            => DeckRiderFacingMath.CompassHeading(drawnHeadingDegrees, _deckBearingDegrees);

        /// <summary>
        /// ⭐ <b>One step about his deck.</b> <paramref name="moveInput"/> is the screen-axis walk input
        /// (the same vector the cabin walk and <c>DeckWalkController</c> read),
        /// <paramref name="drawnHeadingDegrees"/> the heading of the hull PICTURE she is standing on, and
        /// <paramref name="bakeElevationDegrees"/> that artwork's own foreshortening.
        ///
        /// <para>The step is <see cref="DeckWalkController.StepOnDeckPolygon"/>'s: the screen-axis input
        /// becomes the deck direction that DRAWS along it, the travel is metres of real deck, and the
        /// result is clamped onto the walkable areas <b>every tick even with no input</b> — which is what
        /// keeps her aboard while the hull turns under her.</para>
        ///
        /// <para><b>⚠ Her gait is measured in the DECK frame, not on screen.</b> Both are honest numbers
        /// and only one is hers: a boat making five knots moves her a long way through the world without
        /// her taking a step, and the walk-in-place defect this component's twin exists to prevent is
        /// exactly that read. The deck frame is heading-independent by construction, so a turning hull
        /// contributes nothing to it.</para>
        ///
        /// <para>⭐ <b>Near his doorway</b> (<paramref name="door"/>, null on a hull with no cabin): the key
        /// is bent toward the opening within its pull, the step is kept out of the house the door opens,
        /// and the pace eases from the one she came through at to the deck's over
        /// <paramref name="paceBlendSeconds"/>.</para>
        ///
        /// <para>Returns false when there is no measured deck to walk (the caller then leaves the shipped
        /// seat alone) or when nothing has seated her yet.</para>
        /// </summary>
        public bool Step(Vector2 moveInput, float deltaSeconds, float drawnHeadingDegrees,
                         float bakeElevationDegrees, BoatCabinDoor door, float paceBlendSeconds)
        {
            BoatDeckDef deck = Deck;
            if (deck == null || !deck.HasWalkableDeck() || !_seated) return false;

            float step = Mathf.Max(0f, deltaSeconds);
            _sinceSeedSeconds += step;
            _paceMetresPerSecond = ArrivalCabinWalk.BlendedPace(_paceFromMetresPerSecond,
                                                                _walkSpeedMetresPerSecond,
                                                                _sinceSeedSeconds, paceBlendSeconds);

            _held = DeckWalkController.HeldInHullFrame(moveInput, drawnHeadingDegrees, bakeElevationDegrees);
            // `!= null`, never `?.` — the door is a UnityEngine.Object (see ArrivalOpening.WalkTheCabin).
            Vector2 steered = door != null ? door.SteerHeld(_deckLocal, _held) : _held;

            Vector2 before = _deckLocal;
            float beforeHeight = _deckHeightMetres;
            _deckLocal = deck.ClampToWalkable(before + steered * (_paceMetresPerSecond * step), ref _areaHint,
                                              out _deckHeightMetres);
            KeepOutOfTheRoom(deck, door, before, beforeHeight);

            float dt = Mathf.Max(1e-4f, deltaSeconds);
            Vector2 deckVelocity = (_deckLocal - before) / dt;
            _speedMetresPerSecond = deckVelocity.magnitude;
            _deckBearingDegrees = DeckRiderFacingMath.DeckBearing(deckVelocity,
                                                                  StepNoiseFloorMetresPerSecond,
                                                                  _deckBearingDegrees);
            return true;
        }

        /// <summary>Where she is standing in the world — the hull's position plus her point on the deck,
        /// through the projection the hull's own art is drawn by. The SAME transform
        /// <c>DeckWalkController</c> places the player by on her own boat, which is what makes the spot
        /// she walks to the spot the picture shows.</summary>
        public Vector3 WorldPosition(Transform boatRoot, float drawnHeadingDegrees,
                                     float bakeElevationDegrees, float z)
        {
            if (boatRoot == null) return new Vector3(0f, 0f, z);
            Vector2 offset = DeckAreaMath.DeckToWorld(_deckLocal, _deckHeightMetres,
                                                      drawnHeadingDegrees, bakeElevationDegrees);
            return new Vector3(boatRoot.position.x + offset.x, boatRoot.position.y + offset.y, z);
        }

        /// <summary>
        /// <b>Seat her at an AUTHORED deck point</b> — the arrival's own <c>_passengerDeckOffset</c>, which
        /// is already stated in this frame ("in metres from the hull's own centre, in HER frame, x across,
        /// y along, bow positive"). Used for the opening that begins ON DECK, where there is no earlier
        /// position of hers to read: the author's intent is the honest seed, and clamping it onto the
        /// walkable areas is what stops an offset tuned for one hull from standing her in the sea on
        /// another.
        /// </summary>
        /// <param name="compassHeadingDegrees">The facing she arrives holding — the hull's own drawn
        /// heading for a first seat, which lands a deck bearing of exactly zero and reproduces the
        /// arrival's shipped picture.</param>
        public void SeedFromDeckPoint(Vector2 deckPoint, float compassHeadingDegrees,
                                      float drawnHeadingDegrees)
        {
            BoatDeckDef deck = Deck;
            if (deck == null || !deck.HasWalkableDeck()) return;

            _deckLocal = deck.ClampToWalkable(deckPoint, ref _areaHint, out _deckHeightMetres);
            Settle(compassHeadingDegrees, drawnHeadingDegrees);
        }

        /// <summary>
        /// ⛔ <b>SEED HER FROM WHERE SHE IS STANDING</b> — the no-teleport join, used when she comes up
        /// through the aft door. The frame she is placed in changes underneath her (sole → deck) and the
        /// two must meet without a step, so the deck point is chosen to reproduce her exact world position
        /// rather than snapping her to one somebody typed.
        ///
        /// <para>The inversion is <see cref="DeckWalkController.SeedDeckLocalPure"/> — the projection folds
        /// along-hull distance and deck height onto the same screen axis, so there is no closed form and
        /// it is a converging iteration. Borrowed rather than restated, for the reason the class note
        /// gives.</para>
        ///
        /// <para>Her FACING is carried across too: she keeps looking where she was looking in the cabin
        /// instead of being spun to face the bow the instant she is outside. Same law, applied to the
        /// other half of her pose (<see cref="DeckRiderFacingMath.DeckBearingFor"/>).</para>
        /// </summary>
        public void SeedFromWorld(Transform boatRoot, Vector3 world, float compassHeadingDegrees,
                                  float drawnHeadingDegrees, float bakeElevationDegrees)
        {
            BoatDeckDef deck = Deck;
            if (boatRoot == null || deck == null || !deck.HasWalkableDeck()) return;

            Vector2 relative = (Vector2)world - (Vector2)boatRoot.position;
            _deckLocal = DeckWalkController.SeedDeckLocalPure(relative, drawnHeadingDegrees,
                                                              bakeElevationDegrees, deck,
                                                              includeWashboards: false,
                                                              ref _areaHint, out _deckHeightMetres);
            Settle(compassHeadingDegrees, drawnHeadingDegrees);
        }

        /// <summary>
        /// ⭐ <b>SEED HER FROM THE CABIN WALK'S OWN POINT</b> — the doorway's join coming out. The sole and
        /// the deck both speak the hull's own metres, and his doorway only lets her out standing on its
        /// wall line — the one place both floors can hold her — so the deck's clamp, and the step out of
        /// the house's outline (<see cref="KeepOutOfTheRoom"/>), move her by a hair at most. No round trip
        /// through a world point drawn by last frame's hull, which is what used to jump her.
        ///
        /// <para>Her facing, her gait and her pace come across with her: she keeps looking where she was
        /// looking, she is drawn walking on the tick she comes through, and her pace eases from the
        /// cabin's to the deck's in <see cref="Step"/>.</para>
        /// </summary>
        public void SeedFromCabin(Vector2 hullLocalMetres, float carriedPaceMetresPerSecond,
                                  float carriedSpeedMetresPerSecond, float compassHeadingDegrees,
                                  float drawnHeadingDegrees, BoatCabinDoor door)
        {
            BoatDeckDef deck = Deck;
            if (deck == null || !deck.HasWalkableDeck()) return;

            _deckLocal = deck.ClampToWalkable(hullLocalMetres, ref _areaHint, out _deckHeightMetres);
            KeepOutOfTheRoom(deck, door, _deckLocal, _deckHeightMetres);
            Settle(compassHeadingDegrees, drawnHeadingDegrees);
            _paceFromMetresPerSecond = Mathf.Max(0f, carriedPaceMetresPerSecond);
            _paceMetresPerSecond = _paceFromMetresPerSecond;
            _sinceSeedSeconds = 0f;
            _speedMetresPerSecond = Mathf.Max(0f, carriedSpeedMetresPerSecond);
        }

        /// <summary>
        /// Out of the house his doorway opens, if the step just taken ended in it: by its nearest edge
        /// (<see cref="BoatCabinWalkMath.PushOutOfTheRoom"/>) and back onto the planking. Only on the floor
        /// the doorway's sill is on — a deck stacked over the room is not in it — and only where the
        /// planking outside will hold her; where it will not, she stays where she was
        /// (<paramref name="before"/>).
        /// </summary>
        private void KeepOutOfTheRoom(BoatDeckDef deck, BoatCabinDoor door, Vector2 before, float beforeHeight)
        {
            BoatInteriorLevel room = door != null ? door.RoomLevel : null;
            BoatInteriorDef def = room != null && door.Interior != null ? door.Interior.Def : null;
            if (def == null || !BoatCabinThreshold.IsOnTheSill(door.Door, _deckHeightMetres, def.FloorTolerance))
                return;

            Vector2 pushed = BoatCabinWalkMath.PushOutOfTheRoom(room, _deckLocal);
            if (pushed == _deckLocal) return;

            Vector2 onDeck = deck.ClampToWalkable(pushed, ref _areaHint, out float height);
            if (DeckAreaMath.Contains(room.Outline, onDeck))
            {
                _deckLocal = before;
                _deckHeightMetres = beforeHeight;
                return;
            }
            _deckLocal = onDeck;
            _deckHeightMetres = height;
        }

        /// <summary>The half of a seat that is the same whichever way she was seated: she is standing
        /// still, she is looking where she arrived looking, and the walk may place her from now on.</summary>
        private void Settle(float compassHeadingDegrees, float drawnHeadingDegrees)
        {
            _deckBearingDegrees = DeckRiderFacingMath.DeckBearingFor(compassHeadingDegrees,
                                                                     drawnHeadingDegrees);
            _speedMetresPerSecond = 0f;
            _seated = true;
        }
    }
}
