using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE PICTURE OF A FLOAT, RIDING THE TIDE.</b> <see cref="FloatingPlatform"/> made a float's deck
    /// a query — "how high are my planks right now?" — and nothing has ever drawn the answer. At Nine Mile
    /// Creek that left 48 m of pontoon and a 12 m brow you could walk on, moor to and step off, with no
    /// pixels at all: the two small craft on the float fingers lay in open water, which is what the owner
    /// saw on 2026-09-04 ("there should be a floating dock with a gangway too with boats moored").
    ///
    /// <para><b>One line, and it is the mirror of the platform's own.</b> The sprite was baked with the
    /// float lying at ONE water level (the wharf rig bakes at <c>tideRange × 0.55</c>, so her deck was
    /// drawn <see cref="BakedDeckElevation"/> above chart datum). The live deck is
    /// <see cref="FloatingPlatform.DeckElevationNow"/>. A metre of HEIGHT draws
    /// <see cref="IsoGround.HeightScale"/> up the screen — not <c>GroundDepthScale</c>, which is the
    /// other half of the same camera and 19% short — so the picture belongs
    /// <c>(deck − bakedDeck) × 0.766</c> up-screen of the line the float's plan sits on. Nothing
    /// interpolates and nothing is saved: the deck is already a pure function of the deterministic water
    /// level, so this is too.</para>
    ///
    /// <para><b>⚠️ IT RIDES THE WHOLE PICTURE, INCLUDING THINGS THAT SHOULD NOT RIDE.</b> The committed
    /// <c>timberFloat</c> / <c>floatSet</c> cells bake the guide piles, the mooring chain, the seabed
    /// anchor block and the gangway INTO the same sprite as the raft — and the rig itself tags exactly
    /// those four <c>fixed</c> so they are skipped by its own rock transform, because they are driven into
    /// the seabed and must not move. One sprite cannot hold both, so at Nine Mile Creek the piles rise and
    /// fall with the dock through <b>4.28 m</b> of tide (3.28 units of screen travel). Named, measured, and
    /// fixed by a re-bake, not by code: the pack needs a float preset baked
    /// <c>{ guidePiles:false, chain:false }</c> and its fixed furniture as its own cell. Until then this is
    /// the honest trade — a dock that is drawn and moves with a lie in its piles, against a dock that is
    /// not drawn at all.</para>
    ///
    /// <para><b>Gate-off shape.</b> With no platform wired the component does nothing rather than throwing
    /// or parking the sprite at datum, so a scene that lost its float still draws the picture where the
    /// builder put it. With no environment service the platform itself reads water 0 (its own established
    /// shape), and the float sits at its freeboard above datum.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FloatingPlatformVisual : MonoBehaviour
    {
        [Tooltip("The float whose deck this picture is OF. Without it the sprite holds still.")]
        [SerializeField] private FloatingPlatform _platform;

        [Tooltip("The deck height (m above chart datum) the SPRITE was baked at — take it from the art's " +
                 "own numbers, never pick one, or the planks drawn and the planks stood on part company " +
                 "as the tide runs.")]
        [SerializeField] private float _bakedDeckElevation;

        [Tooltip("The world Y the piece's PLAN line sits on — where the sprite belongs when the live deck " +
                 "happens to equal the baked one. Held here so the ride is an offset from a known place " +
                 "rather than an accumulation on last frame's position.")]
        [SerializeField] private float _planY;

        /// <inheritdoc cref="_bakedDeckElevation"/>
        public float BakedDeckElevation => _bakedDeckElevation;

        /// <inheritdoc cref="_planY"/>
        public float PlanY => _planY;

        /// <summary>Wire it from a builder: the float, the deck height its picture was baked at, and the
        /// plan line the piece stands on.</summary>
        public void Configure(FloatingPlatform platform, float bakedDeckElevation, float planY)
        {
            _platform = platform;
            _bakedDeckElevation = bakedDeckElevation;
            _planY = planY;
            Apply();
        }

        /// <summary>
        /// How far UP-SCREEN a float drawn at <paramref name="bakedDeckElevation"/> belongs when her deck
        /// is really at <paramref name="deckElevation"/>. Pure and static, so the whole ride is EditMode-
        /// testable with no scene: <c>(deck − baked) × 0.766</c>, positive on the flood.
        /// </summary>
        public static float ScreenRise(float deckElevation, float bakedDeckElevation)
            => (deckElevation - bakedDeckElevation) * IsoGround.HeightScale;

        private void OnEnable() => Apply();

        // The tide is slow but it is CONTINUOUS, and the float is the one structure in the region whose
        // picture has to follow it. Eight sprites setting a transform each frame is inside rule 7's
        // budget; a slow-tick would step the dock down the screen in visible jumps.
        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (_platform == null) return;
            float y = _planY + ScreenRise(_platform.DeckElevationNow(), _bakedDeckElevation);
            Vector3 at = transform.position;
            // Assign only on a real move: a float that is aground holds still for hours of game time, and
            // an unconditional write dirties the transform every frame for nothing.
            if (!Mathf.Approximately(at.y, y)) transform.position = new Vector3(at.x, y, at.z);
        }
    }
}
