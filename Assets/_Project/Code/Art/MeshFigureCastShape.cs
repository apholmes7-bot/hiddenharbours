using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>Hands a mesh figure's traced outline to the hidden body's shadow and foliage silhouette.</b>
    /// One per presenter (the player's and a mesh villager's share it). Each frame the presenter says
    /// which figure, if any, is drawn instead of the body; this traces that figure
    /// (<see cref="MeshFigureCastSilhouette"/>) and sets it as the
    /// <see cref="SpriteShadow.SilhouetteOverride"/> and <see cref="SilhouetteMaskSource.SilhouetteOverride"/>
    /// on the body, and clears both the frame no figure draws, so the body's own sprite casts again
    /// exactly as before.
    ///
    /// <para>No allocation per frame: the components are looked up only while missing (an installer may
    /// add them after the presenter starts), and the outline texture is made once, on the first frame a
    /// figure draws.</para>
    /// </summary>
    public sealed class MeshFigureCastShape : System.IDisposable
    {
        /// <summary>The outline's pixels per unit when the body has no sprite to read it from.</summary>
        public const float FallbackPixelsPerUnit = 32f;

        private SpriteRenderer _body;
        private SpriteShadow _shadow;
        private SilhouetteMaskSource _mask;
        private MeshFigureCastSilhouette _silhouette;

        /// <summary>The outline in force this frame, or null when the body casts its own sprite.</summary>
        public Sprite Current { get; private set; }

        /// <summary>The tracer, once made (tests and the look probe read its trace count).</summary>
        public MeshFigureCastSilhouette Silhouette => _silhouette;

        /// <summary>
        /// This frame's word: <paramref name="drawn"/> is the figure drawn instead of
        /// <paramref name="body"/>, or null when the body (or nothing) is drawn.
        /// </summary>
        public void Sync(SpriteRenderer body, IsoCharacterFigureRenderer drawn)
        {
            if (body != _body) { Clear(); _body = body; }
            if (_body == null) return;
            if (_shadow == null) _body.TryGetComponent(out _shadow);
            if (_mask == null) _body.TryGetComponent(out _mask);

            // Traced only for a shadow or a foliage outline someone will see: at the oars the body is
            // disabled off a walking deck and neither draws, so nothing is traced there.
            bool wanted = (_shadow != null && _shadow.WouldCast)
                          || (_mask != null && _mask.isActiveAndEnabled && _body.enabled && _body.gameObject.activeInHierarchy);
            Sprite shape = null;
            if (drawn != null && wanted)
            {
                if (_silhouette == null)
                {
                    float ppu = _body.sprite != null ? _body.sprite.pixelsPerUnit : FallbackPixelsPerUnit;
                    _silhouette = new MeshFigureCastSilhouette(ppu);
                }
                if (_silhouette.Refresh(drawn, _body.transform)) shape = _silhouette.Sprite;
            }
            Apply(shape);
        }

        /// <summary>Give the body its own sprite back on both, and forget it.</summary>
        public void Clear()
        {
            Apply(null);
            _body = null;
            _shadow = null;
            _mask = null;
        }

        public void Dispose()
        {
            Clear();
            _silhouette?.Dispose();
            _silhouette = null;
        }

        private void Apply(Sprite shape)
        {
            Current = shape;
            if (_shadow != null) _shadow.SilhouetteOverride = shape;
            if (_mask != null) _mask.SilhouetteOverride = shape;
        }
    }
}
