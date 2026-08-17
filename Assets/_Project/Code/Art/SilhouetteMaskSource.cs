using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The body that reads through the leaves.</b> Attached to the on-foot fisher, this keeps ONE
    /// hidden child renderer carrying the same sprite she is currently drawn in, on the mask material,
    /// so <see cref="SilhouetteMaskFeature"/> can stamp her shape into a screen texture before any
    /// foliage draws (owner ruling, 2026-08-16 — foliage stays opaque and in front; the player reads
    /// as a tinted silhouette through it).
    ///
    /// <para><b>It is the SpriteShadow pattern, deliberately.</b> <see cref="SpriteShadow"/> already
    /// solved "keep a second renderer on the caster's CURRENT sprite, every frame, for free": a
    /// reference compare against the last sprite, which a static caster never trips and an animating
    /// one trips a few times a second. This does the same thing for the same reason — at the 10 Hz of
    /// a throttled tick the mask would lag the body by up to a whole walk frame, and a silhouette that
    /// lags is worse than none: it reads as a second fisher a step behind the first.</para>
    ///
    /// <para><b>Flip is copied, not inferred.</b> The walk skin faces west by flipping east, so a mask
    /// that ignored <c>flipX</c> would put her silhouette on the wrong side of her own body — half a
    /// cell out, which at PPU 32 is the whole figure. The mask shader runs URP's own
    /// <c>UnityFlipSprite</c> vertex path, so copying the renderer's flip flags is all it takes for
    /// the two to land on identical pixels.</para>
    ///
    /// <para><b>One caster, every state.</b> Same seam <see cref="PlayerShadowInstaller"/> is built on
    /// and for the same reason: the walk skin, the iso 8-direction skin, the haul cycle and the
    /// rod-fight poses are all the SAME <see cref="SpriteRenderer"/>, so one source silhouettes
    /// whichever frame is drawn. It needs no state gate of its own either — <c>ControlSwitcher</c>
    /// disables that renderer at the helm, and this keys its own visibility off
    /// <c>caster.enabled</c>, so stepping to the wheel takes the silhouette with it.</para>
    ///
    /// <para><b>⚠️ Scope: the BODY, not what she is carrying.</b> A carried tool or a fish in hand is a
    /// separate renderer (<c>CarryHands</c>) and is deliberately not masked — it would mean walking a
    /// renderer list every frame to catch something that is a few pixels wide and, being held out in
    /// front, is the part of her least likely to be behind a trunk. If that ever reads wrong in play,
    /// the fix is to give this component a second synced child, not to make it search.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class SilhouetteMaskSource : MonoBehaviour
    {
        /// <summary>Name of the hidden child that carries the mask sprite — shared with the tests so a
        /// rename breaks them rather than silently un-silhouetting the fisher.</summary>
        public const string MaskChildName = "HHSilhouetteMask";

        /// <summary>The mask shader, by name. One shared engine material serves every source: the
        /// SPRITE lives on the renderer, not the material, so nothing here is ever per-instance — no
        /// per-object material, and therefore nothing that could dirty a shipped asset (the
        /// render-fixture lesson: <c>sharedMaterial.SetFloat</c> writes to the ASSET).</summary>
        public const string MaskShaderName = "HiddenHarbours/SilhouetteMask";

        static Material s_MaskMaterial;

        SpriteRenderer _caster;
        SpriteRenderer _mask;
        Sprite _lastSprite;
        bool _registered;

        /// <summary>The hidden child's renderer, for the tests and for nothing else.</summary>
        public SpriteRenderer MaskRenderer => _mask;

        void OnEnable()
        {
            _caster = GetComponent<SpriteRenderer>();
            if (_caster == null)
            {
                // Nothing to silhouette. Not an error — the installer is allowed to be optimistic.
                enabled = false;
                return;
            }

            EnsureMask();
            SilhouetteRegistry.Register(this);
            _registered = true;
        }

        void OnDisable()
        {
            if (_registered)
            {
                SilhouetteRegistry.Unregister(this);
                _registered = false;
            }
            if (_mask != null) _mask.enabled = false;
        }

        /// <summary>
        /// Build the hidden child, once.
        ///
        /// <para><b>⚠️ Created INACTIVE, configured, then activated.</b> An <c>[ExecuteAlways]</c>
        /// component wakes the instant it is added, so a child built active would run one frame in its
        /// default state — here, one frame of an unconfigured SpriteRenderer with no material and no
        /// rendering layer, which is a frame of the mask drawing a white quad through the whole
        /// filtered pass. The project has been bitten by this twice; the order below is the cure.</para>
        /// </summary>
        void EnsureMask()
        {
            if (_mask != null) return;

            Transform existing = transform.Find(MaskChildName);
            if (existing != null)
            {
                _mask = existing.GetComponent<SpriteRenderer>();
                if (_mask != null) return;
            }

            var go = new GameObject(MaskChildName) { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);                       // configure BEFORE anything can render it
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            _mask = go.AddComponent<SpriteRenderer>();
            _mask.sharedMaterial = MaskMaterial();
            // The membership bit. Without it the renderer carries the mask pass but is not IN the
            // filtered list — see SilhouetteRegistry.RenderingLayer for why the tag alone is not the
            // contract.
            _mask.renderingLayerMask = SilhouetteRegistry.RenderingLayer;
            // Nothing about the ordinary sprite queue applies to a renderer whose shader has only an
            // HHSilhouette pass, but a sane slot costs nothing and keeps the scene view honest.
            _mask.sortingLayerID = _caster.sortingLayerID;
            _mask.sortingOrder = _caster.sortingOrder;

            go.SetActive(true);
            SyncNow();
        }

        static Material MaskMaterial()
        {
            if (s_MaskMaterial != null) return s_MaskMaterial;
            var shader = Shader.Find(MaskShaderName);
            if (shader == null)
            {
                Debug.LogError($"[SilhouetteMaskSource] Shader '{MaskShaderName}' missing; the fisher " +
                               "will not read through foliage.");
                return null;
            }
            s_MaskMaterial = new Material(shader)
            {
                name = "HHSilhouetteMask",
                hideFlags = HideFlags.HideAndDontSave,
            };
            return s_MaskMaterial;
        }

        /// <summary>
        /// Keep the mask on the caster's CURRENT sprite and flip. Every frame, for the reason
        /// <see cref="SpriteShadow.SyncSilhouette"/> gives: an animating body changes sprite several
        /// times a second and a mask a frame behind reads as a ghost. Costs one reference compare on a
        /// static caster and one assignment on a moving one.
        /// </summary>
        void LateUpdate() => SyncNow();

        /// <summary>
        /// The sync itself. <b>Public so a test can drive the SAME code path the frame loop runs</b>
        /// rather than a re-statement of it — EditMode has no <c>LateUpdate</c>, and a guard that
        /// exercises its own copy of the rule proves nothing about the rule that ships (the same
        /// argument <see cref="PlayerShadowInstaller.Attach"/> is public for).
        /// </summary>
        public void SyncNow()
        {
            if (_mask == null || _caster == null) return;

            // Follow the body's visibility exactly: at the helm ControlSwitcher disables the caster,
            // and a silhouette of a fisher who is not ashore would stamp her shape into the trees from
            // the wheelhouse.
            bool visible = _caster.enabled && _caster.gameObject.activeInHierarchy;
            if (_mask.enabled != visible) _mask.enabled = visible;
            if (!visible) return;

            // Flip is cheap and must never lag — a bool compare, not a sprite compare.
            if (_mask.flipX != _caster.flipX) _mask.flipX = _caster.flipX;
            if (_mask.flipY != _caster.flipY) _mask.flipY = _caster.flipY;

            Sprite sprite = _caster.sprite;
            if (sprite == _lastSprite) return;   // the overwhelmingly common case — one compare, no work
            _lastSprite = sprite;
            _mask.sprite = sprite;
        }
    }
}
