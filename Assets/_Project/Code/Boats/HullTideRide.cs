using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// ⭐ <b>A HULL RIDES THE TIDE</b> (owner playtest 2026-09-06, at the Nine Mile Creek north wall on a
    /// spring ebb: <i>"boats dont ride the tide"</i>). The sim has always known she floats — her cleats
    /// rise with the water (<c>BoatCleats.ElevationOf</c>), her deck rises with them
    /// (<c>LadderBoardingMath.BoatDeckElevation</c>), the ladder's gap opens and closes as the tide runs.
    /// The <b>picture</b> is what stayed behind: a moored hull was drawn at her berth's plan point and
    /// nothing ever moved it, so at dead low spring she lay at the same place on a 4.6 m quay face she
    /// lies at high water, while the float beside her — the one structure that had been taught this —
    /// rode up and down past her.
    ///
    /// <para><b>What this component owns: the number, not the transform.</b> It publishes
    /// <see cref="ScreenRiseNow"/> — how far up-screen this hull's picture belongs right now — and writes
    /// nothing. <see cref="BoatWaveMotion"/> applies it, because that component already owns this
    /// visual's screen-vertical offset and a second writer on one transform is how a picture starts
    /// fighting itself (it resets to a cached base every LateUpdate; anything else writing the same
    /// field is either stomped or baked into that base). The composition is also the honest physics: the
    /// sea's surface under her is ONE surface, the tide is its mean and the waves are what is left
    /// over.</para>
    ///
    /// <para><b>And it is her PICTURE that rides, never her plan.</b> The transform this hull stands on
    /// is her position on the ground plane — her body, her collider, her berth, the outline a swimmer
    /// reaches for — and a metre of tide is not a metre of northing. Lifting the root would walk a
    /// moored boat five metres into the wall she is tied to. The float already draws this distinction:
    /// its walkable surface holds its plan rectangle while <c>FloatingPlatformVisual</c> rides.</para>
    ///
    /// <para><b>The rule is not this component's either</b> — it is <see cref="TidalRide"/>'s, in Core,
    /// which is the very rule the harbour float rides (<c>FloatingPlatformVisual.ScreenRise</c> over
    /// <c>FloatingPlatform.DeckElevation</c>). That is deliberate and it is the whole point: a boat lying
    /// at a float must go up and down with the planks she is tied to, and two arithmetics for one rise is
    /// how they part company (the memory
    /// <c>a-weight-and-its-colour-must-come-from-one-publisher</c>).</para>
    ///
    /// <para><b>She takes the ground.</b> The rise is measured from her WATERLINE, which is the sea's
    /// level until the ebb sets her down on the bottom and then stops — so a punt in a drying berth
    /// settles on the mud and the water goes on falling without her, and a hull over the dredged berth
    /// trench never grounds at all. Same expression, both readings (P5: the harbour has teeth, and one of
    /// them is that a boat left in the wrong berth dries out).</para>
    ///
    /// <para><b>Deterministic, nothing saved</b> (rule 5): the water level is recomputed from
    /// <c>(worldSeed, gameTime)</c> and the bed is read off the authored terrain. The offset is always
    /// measured from the hull's plan line — never added to where her picture already was, which would
    /// accumulate every rounding error the tide ever made.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HullTideRide : MonoBehaviour
    {
        [Tooltip("The water level (m above chart datum) this hull's picture is CORRECT at — where her " +
                 "plan point puts her. 0 is the game's datum, which is mean water: a builder that " +
                 "places a berth as a plain plan point (every one of them does) has drawn her as she " +
                 "floats at mean tide, and the sea then carries her either side of it. A region whose " +
                 "plan lines were struck at some other state of tide says so here.")]
        [SerializeField] private float _bakedWaterlineElevation;

        [Tooltip("Her draught in metres, or NEGATIVE to read it off the hull she is (a BoatController's " +
                 "BoatHullDef, or a MooredBoat's owner's). Draught decides one thing only: WHEN SHE " +
                 "TAKES THE GROUND. A hull with no def known reads 0 and therefore never grounds, " +
                 "which is the honest answer for art on the water with no data behind it.")]
        [SerializeField] private float _draughtOverrideMetres = -1f;

        /// <summary>
        /// How far the bed under her may be stale before it is re-read, in metres of her own travel.
        /// The seabed is a terrain sample (a smoothstep march down every channel in the region), and a
        /// hull that has not moved is over the same ground she was over last frame — so the sample is
        /// taken when she MOVES, not when the clock ticks (rule 7). The falling tide is not throttled
        /// with it: the water level is read every frame, which is cheap and is the term that actually
        /// changes under a boat lying still.
        /// </summary>
        private const float BedResampleMetres = 0.5f;

        private BoatController _controller;
        private MooredBoat _moored;
        private bool _hullResolved;

        private float _bedElevation = float.NegativeInfinity;
        private Vector2 _bedSampledAt;
        private bool _bedSampled;

        /// <inheritdoc cref="_bakedWaterlineElevation"/>
        public float BakedWaterlineElevation => _bakedWaterlineElevation;

        /// <summary>
        /// Wire her from a builder or the skinner. <paramref name="draughtMetres"/> negative leaves the
        /// draught to be read off the hull she is, which is what almost everything wants: the player
        /// swaps boats under this component (<c>BoatController.SetHull</c>) and a draught captured once
        /// would go stale the moment she did.
        /// </summary>
        public void Configure(float bakedWaterlineElevation, float draughtMetres = -1f)
        {
            _bakedWaterlineElevation = bakedWaterlineElevation;
            _draughtOverrideMetres = draughtMetres;
            _bedSampled = false;
        }

        /// <summary>
        /// Her draught in metres — the override when one is authored, else the hull she is. Resolved
        /// through the sibling that KNOWS which boat this is: a piloted hull's
        /// <see cref="BoatController.Hull"/>, or a moored hull's owner's boat. Zero when neither can say,
        /// and a hull with no draught simply never takes the ground.
        /// </summary>
        public float DraughtMetres
        {
            get
            {
                if (_draughtOverrideMetres >= 0f) return _draughtOverrideMetres;
                if (!_hullResolved)
                {
                    _controller = GetComponent<BoatController>();
                    _moored = GetComponent<MooredBoat>();
                    _hullResolved = true;
                }
                if (_controller != null && _controller.Hull != null)
                    return _controller.Hull.DraughtMeters;
                if (_moored != null && _moored.Owner != null && _moored.Owner.Boat != null)
                    return _moored.Owner.Boat.DraughtMeters;
                return 0f;
            }
        }

        /// <summary>
        /// The ground under her, metres above chart datum — <see cref="float.NegativeInfinity"/> in open
        /// water (no terrain wired), which is the same "no bottom to be shallow over" reading
        /// <see cref="BoatCrossing.DepthAt"/> gives, and means she can never ground.
        /// </summary>
        public float BedElevation
        {
            get
            {
                Vector2 at = transform.position;
                if (_bedSampled &&
                    (at - _bedSampledAt).sqrMagnitude <= BedResampleMetres * BedResampleMetres)
                    return _bedElevation;

                ITidalTerrain terrain = GameServices.TidalTerrain;
                _bedElevation = terrain != null ? terrain.ElevationAt(at) : float.NegativeInfinity;
                _bedSampledAt = at;
                _bedSampled = true;
                return _bedElevation;
            }
        }

        /// <summary>The deterministic water level right now (m above datum), or <b>0</b> with no
        /// environment service wired — the established gate-off shape (<c>BoatCleats.ElevationOf</c>,
        /// <c>FloatingPlatform.WaterLevelNow</c>), under which the tide simply does not run and every
        /// hull sits exactly where her builder put her.</summary>
        public static float WaterLevelNow()
        {
            IEnvironmentService environment = GameServices.Environment;
            if (environment == null) return 0f;
            IGameClock clock = GameServices.Clock;
            return environment.WaterLevelAt(clock != null ? clock.TotalSeconds : 0.0);
        }

        /// <summary>The waterline she is sitting at right now (m above datum) — the sea, until the bed
        /// takes her.</summary>
        public float WaterlineNow() => TidalRide.Waterline(WaterLevelNow(), BedElevation, DraughtMetres);

        /// <summary>True when the ebb has set her on the bottom where she lies.</summary>
        public bool IsAgroundNow() => TidalRide.IsAground(WaterLevelNow(), BedElevation, DraughtMetres);

        /// <summary>
        /// ⭐ <b>How far UP-SCREEN this hull's picture belongs right now</b>, in world units, measured
        /// from the plan line her transform stands on. Positive on the flood. The one number this
        /// component exists to publish.
        /// </summary>
        public float ScreenRiseNow() => TidalRide.ScreenRise(WaterlineNow(), _bakedWaterlineElevation);

        /// <summary>
        /// The same rise at an ARBITRARY water level and bed — the pure form, so the ride can be
        /// asserted headless against the float's own formula without a terrain, a clock or a scene.
        /// </summary>
        public float ScreenRiseAt(float waterLevel, float bedElevation)
            => TidalRide.ScreenRise(TidalRide.Waterline(waterLevel, bedElevation, DraughtMetres),
                                    _bakedWaterlineElevation);
    }
}
