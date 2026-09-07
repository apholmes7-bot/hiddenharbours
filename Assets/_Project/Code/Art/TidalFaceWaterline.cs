using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// ⭐ <b>THE SEA COVERS A DRAWN WALL, AND UNCOVERS IT AGAIN.</b> The owner, at the Nine Mile Creek
    /// north wall at 06:11 on 2026-09-06: <i>"boats arent touching but not sitting in water"</i>.
    ///
    /// <para><b>What was measured</b> (on the owner's own scene and the committed <c>logCrib</c> sheet, no
    /// engine needed): the quay face is a static sprite whose ink runs <b>2.625 units below its pivot</b>,
    /// and the pieces stand at y 84.6235 with their drawn deck lip on the wall's plan line at y 87.00. So
    /// the wall's picture covered the sea from <b>82.00 to 87.00 — five units of timber standing over the
    /// water — at every state of the tide</b>, while the five hulls lying at the wall sit at y 84.00-84.50
    /// and, since #753, ride ±1.685 units with it. The sea's drawn edge was 2.0-2.5 units below every hull
    /// at mean water and 3.7-4.2 below her at spring high. She was not hovering: she was drawn against a
    /// wall, because the wall was drawn over the water she floats in.</para>
    ///
    /// <para><b>So this component cuts the face at the waterline, and adds no water at all.</b> What shows
    /// through the cut is the sea that was already drawn there and hidden — the water plane covers the
    /// whole basin and sorts at <see cref="SortingBands.Sea"/>, below the face's own fixed rung. Nothing
    /// new draws; the wall stops covering something up.</para>
    ///
    /// <para><b>⭐ It does not get its own sea.</b> The level arrives on <c>_HHSeaLevelWorld</c> — the one
    /// global <see cref="WaterSurface"/> publishes for "every surface that meets the sea without being
    /// it", which is the same global the cliff faces ride
    /// (<see cref="CliffWaterlineMath"/>'s opening rule). A second read of the tide here is exactly how a
    /// cliff, a quay and the hull lying against it start disagreeing about where the water is.</para>
    ///
    /// <para><b>⭐ And the arithmetic is Core's <see cref="TidalRide"/>, not a second spelling.</b> The
    /// waterline's row is <see cref="WaterlineWorldY"/> = <c>lip + TidalRide.ScreenRise(sea, lipElevation)</c>
    /// — the identical rule the harbour float rides and the hulls ride, which is the whole reason a boat
    /// alongside now meets the water at the same line the wall does. The camera's height scale is PUSHED
    /// into the shader rather than written there as 0.766, so <see cref="IsoGround.HeightScale"/> stays the
    /// single definition (rule 6).</para>
    ///
    /// <para><b>What falls out of it for free.</b> The wall's exposed height becomes
    /// <c>deck − sea</c>: 0.61 units at spring high, which is this wharf's authored 0.80 m of freeboard,
    /// and 3.98 at spring low, which is its 5.2 m of bared face. And the growth bands the pack was
    /// re-baked for at #478 — barnacle 1.76-3.52 m, rockweed 0.26-1.76 m above chart datum — are covered
    /// and uncovered by the water they were painted for, instead of standing permanently dry.</para>
    ///
    /// <para><b>Presentation only</b> (rule 5): this is a clip on a picture. It moves no walkability, no
    /// standable surface, no berth and no <c>_WaterLevel</c>, and it saves nothing. Deterministic — the
    /// level it reads is itself recomputed from <c>(worldSeed, gameTime)</c>.</para>
    ///
    /// <para><b>Cost</b> (rule 7): <b>there is no per-frame work here at all.</b> The per-piece numbers
    /// never change, so they are pushed ONCE into a <see cref="MaterialPropertyBlock"/> when the face is
    /// configured, and the tide arrives on a global somebody else already publishes on a throttled tick.
    /// ⚠️ A per-renderer property block IS a per-draw value and forgoes batching — that is the honest
    /// cost of this, it is UNMEASURED on the wharf's ~44 courses, and it is the first term to look at if
    /// a frame-time budget ever comes to the quay.</para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TidalFaceWaterline : MonoBehaviour
    {
        /// <summary>The shader that does the cutting. A string contract, named once.</summary>
        public const string ShaderName = "HiddenHarbours/TidalFace";

        /// <summary>The per-renderer packing the shader reads: (lip world y, lip elevation, screen units
        /// per metre of height, 1 when configured).</summary>
        public const string TideProperty = "_HHFaceTide";

        /// <summary>The published drawn waterline, <see cref="WaterSurface.PublishSeaLevel"/>'s global.
        /// Read here rather than re-derived, which is the whole of the "one sea" rule.</summary>
        public const string SeaLevelProperty = "_HHSeaLevelWorld";

        [Tooltip("The WORLD Y this face's drawn deck lip lands on — the one line the placement " +
                 "guarantees, and therefore the only line a row of pixels can be measured from. Every " +
                 "other row of the sprite is this one plus a height, and heights draw at the camera's " +
                 "own scale.")]
        [SerializeField] private float _lipWorldY;

        [Tooltip("What that lip is, in metres above the game's datum — the elevation of the deck this " +
                 "face holds up. Measured off the authored terrain where the face is placed, never " +
                 "typed, so a terrain edit that lowers a wall moves its waterline with it.")]
        [SerializeField] private float _lipElevation;

        [Tooltip("Off draws the shipped picture: the face uncut, exactly as it drew before the sea was " +
                 "let up it. Kept as a switch rather than a deletion because it is the only honest A/B " +
                 "for a plate — the same placed dressing, one term changed.")]
        [SerializeField] private bool _ridesTheTide = true;

        [Tooltip("Set once the piece has been told where its lip is. Unconfigured is a pixel-identical " +
                 "passthrough rather than a wall cut at zero, which is what a face on a rig that " +
                 "predates this must draw.")]
        [SerializeField] private bool _configured;

        private static Material _sharedMaterial;
        private static readonly int TideId = Shader.PropertyToID(TideProperty);
        private static readonly int SeaLevelId = Shader.PropertyToID(SeaLevelProperty);

        private SpriteRenderer _renderer;
        private MaterialPropertyBlock _block;

        /// <inheritdoc cref="_lipWorldY"/>
        public float LipWorldY => _lipWorldY;
        /// <inheritdoc cref="_lipElevation"/>
        public float LipElevation => _lipElevation;
        /// <inheritdoc cref="_ridesTheTide"/>
        public bool RidesTheTide => _ridesTheTide;
        /// <inheritdoc cref="_configured"/>
        public bool IsConfigured => _configured;

        /// <summary>
        /// ⭐ <b>THE ROW THE WATER'S EDGE DRAWS ON THIS FACE</b>, in world units — the lip, plus how far
        /// up-screen the sea stands above the lip's own elevation.
        ///
        /// <para>That second term is <see cref="TidalRide.ScreenRise"/> and nothing else: the same rule
        /// that lifts a hull's picture and the harbour float's, asked here with the WALL's baked
        /// elevation instead of a hull's baked waterline. Which is the point — a boat lying against this
        /// wall and the wall she lies against must meet the water on one line, and two arithmetics for
        /// one rise is how they part company.</para>
        ///
        /// <para>Pure and frame-free, so the whole rule is asserted headless with no scene, no sea and no
        /// GPU.</para>
        /// </summary>
        public static float WaterlineWorldY(float lipWorldY, float lipElevation, float seaLevelMetres)
            => lipWorldY + TidalRide.ScreenRise(seaLevelMetres, lipElevation);

        /// <summary>
        /// The drawn sea level right now, in metres above the game's datum, as
        /// <see cref="WaterSurface"/> published it. False when nothing has published one — a scene with no
        /// water, a stopped play session, an EditMode fixture — under which a face must draw uncut rather
        /// than be cut at zero. The <c>w</c> component is the flag, exactly as the water shader treats
        /// <c>_HHFoamBufferWorld.z</c>.
        /// </summary>
        public static bool TryPublishedSeaLevel(out float seaLevelMetres)
        {
            Vector4 published = Shader.GetGlobalVector(SeaLevelId);
            seaLevelMetres = published.x;
            return published.w > 0.5f;
        }

        /// <summary>Where this face's waterline is right now, or <see cref="float.NegativeInfinity"/> when
        /// there is no published sea or the piece is not riding — "below everything", which is what an
        /// uncut face means. The number a fixture measures instead of counting pixels.</summary>
        public float WaterlineWorldYNow()
        {
            if (!_configured || !_ridesTheTide) return float.NegativeInfinity;
            return TryPublishedSeaLevel(out float sea)
                ? WaterlineWorldY(_lipWorldY, _lipElevation, sea)
                : float.NegativeInfinity;
        }

        /// <summary>
        /// Tell this piece where its drawn deck lip lands and what that lip is. Called by the placement
        /// that already knows both — the dressing anchors every course by its lip, which is the one line
        /// it guarantees, and reads the deck's height off the authored terrain.
        /// </summary>
        public void Configure(float lipWorldY, float lipElevation)
        {
            _lipWorldY = lipWorldY;
            _lipElevation = lipElevation;
            _configured = true;
            Apply();
        }

        /// <summary>Draw the shipped, uncut picture (or stop) — the A/B switch, applied immediately so a
        /// plate fixture can shoot both halves of a pair on one placed dressing.</summary>
        public void SetRidesTheTide(bool rides)
        {
            _ridesTheTide = rides;
            Apply();
        }

        private void OnEnable() => Apply();

        private void OnValidate()
        {
            if (isActiveAndEnabled) Apply();
        }

        /// <summary>
        /// Put the piece on the cutting material and push its two numbers. Called from
        /// <see cref="Configure"/> as well as from <c>OnEnable</c>, because EditMode has no
        /// <c>OnEnable</c> and a fixture that configured a face and then measured it would otherwise
        /// measure a face that had never been told anything.
        /// </summary>
        private void Apply()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) return;

            Material material = SharedMaterial();
            if (material == null) return;                       // warned once, in SharedMaterial
            if (_renderer.sharedMaterial != material) _renderer.sharedMaterial = material;

            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetVector(TideId, new Vector4(
                _lipWorldY,
                _lipElevation,
                IsoGround.HeightScale,
                _configured && _ridesTheTide ? 1f : 0f));
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// ONE material for every drawn face in the game — they differ only in the two numbers above, and
        /// those travel in a property block, so a wharf's ~44 courses cost one material instead of
        /// forty-four (rule 7). Built on demand and kept out of the scene and the save, the
        /// <c>DeckRiderVisual</c> arrangement.
        /// </summary>
        private static Material SharedMaterial()
        {
            if (_sharedMaterial != null) return _sharedMaterial;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogWarning(
                    $"[TidalFaceWaterline] Shader '{ShaderName}' not found — the quay face will draw " +
                    "uncut, which is the picture it drew before the sea was let up it. Nothing else is " +
                    "affected.");
                return null;
            }

            _sharedMaterial = new Material(shader)
            {
                name = "TidalFace (shared)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            return _sharedMaterial;
        }
    }
}
