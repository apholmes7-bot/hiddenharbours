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
    ///   <item>the step and the clamp are <see cref="DeckWalkController.StepOnDeckPolygon"/> — the
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

        public ArrivalDeckWalk(GameObject boat, float walkSpeedMetresPerSecond)
        {
            _boat = boat;
            _walkSpeedMetresPerSecond = Mathf.Max(0f, walkSpeedMetresPerSecond);
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
        /// bulkhead, because a clamped step is no step.</summary>
        public float SpeedMetresPerSecond => _speedMetresPerSecond;

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
        /// <para>Returns false when there is no measured deck to walk (the caller then leaves the shipped
        /// seat alone) or when nothing has seated her yet.</para>
        /// </summary>
        public bool Step(Vector2 moveInput, float deltaSeconds, float drawnHeadingDegrees,
                         float bakeElevationDegrees)
        {
            BoatDeckDef deck = Deck;
            if (deck == null || !deck.HasWalkableDeck() || !_seated) return false;

            Vector2 before = _deckLocal;
            _deckLocal = DeckWalkController.StepOnDeckPolygon(_deckLocal, moveInput,
                                                              _walkSpeedMetresPerSecond, deltaSeconds,
                                                              drawnHeadingDegrees, bakeElevationDegrees,
                                                              deck, ref _areaHint, out _deckHeightMetres);

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
