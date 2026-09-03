using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>A mesh hull throws a lamp shadow</b> (ADR 0016, lights PR B; the owner's 2026-08-05
    /// ruling that boats cast). A hull has no sprite for a shadow to shear — she is drawn by
    /// re-composing her pixels out of the feature's resolved screen texture
    /// (<c>_HHHullScreenTex</c>, alpha = her hull id over 255) — so this caster hands the shadow
    /// system her ID BLOCK and her image rect, and the shadow shader asks that same texture, per
    /// pixel, whether the point it is the shadow of is her. Whatever she is drawing right now —
    /// heading, roll, a cut-open house — is what casts, with no second silhouette pass and no bake.
    ///
    /// <para><b>The feet are her waterline contact</b> — the ground-contact pivot her reflection
    /// already mirrors about (<see cref="ReflectiveObject.MirrorPivot"/>, ADR 0026), read off the
    /// reflector on her overlay quad so the shadow and the reflection agree about where she meets
    /// the sea. The image rect is her overlay quad's bounds — the cell around that pivot, riding the
    /// compositing window as she heaves.</para>
    ///
    /// <para><b>Self-installing from the presentation service</b>, beside her reflector, her churn
    /// and her lamps: every mesh hull the game builds carries one, in every region, with no scene
    /// wiring. Costs nothing until a lamp is in range of her (rule 7); registers in
    /// <c>OnEnable</c>, leaves in <c>OnDisable</c>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HullLampShadowCaster : MonoBehaviour, ILampShadowCaster
    {
        private IsoFacetHullRenderer _hull;

        private void OnEnable()
        {
            _hull = null;   // a hull swap replaces the renderer under us; never cache across an enable
            LampShadowSystem.RegisterCaster(this);
        }

        private void OnDisable() => LampShadowSystem.UnregisterCaster(this);

        /// <inheritdoc/>
        public bool TryGetLampShadowCaster(out LampShadowCasterState state)
        {
            state = default;
            if (_hull == null) _hull = GetComponent<IsoFacetHullRenderer>();
            if (_hull == null || !_hull.isActiveAndEnabled || !_hull.IsConfigured) return false;
            if (_hull.HullId == 0) return false;                  // not registered: nothing of hers is in the texture

            MeshRenderer overlay = _hull.OverlayRenderer;
            if (overlay == null) return false;

            var reflector = overlay.GetComponent<ReflectiveObject>();
            Vector2 foot = reflector != null ? reflector.MirrorPivot : (Vector2)overlay.transform.position;
            Bounds b = overlay.bounds;

            state.Foot = foot;
            state.RectMin = new Vector2(b.min.x, b.min.y);
            state.RectMax = new Vector2(b.max.x, b.max.y);
            state.Hull = _hull;
            return state.IsValid;
        }

        /// <summary>Give <paramref name="host"/> a hull caster if it has none. Idempotent; null-safe.</summary>
        public static HullLampShadowCaster Fit(GameObject host)
        {
            if (host == null) return null;
            var existing = host.GetComponent<HullLampShadowCaster>();
            return existing != null ? existing : host.AddComponent<HullLampShadowCaster>();
        }
    }
}
