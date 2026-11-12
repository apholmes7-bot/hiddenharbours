using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The Art side of <see cref="IGroundPropPresentationService"/>: a sprite that depth-sorts like any
    /// other static decor (<see cref="YSortSprite"/>) and, for a lit thing, a round
    /// <see cref="SceneLight"/> that fades in with the dark by itself. Nothing here knows what the
    /// thing is or why it is there.
    /// </summary>
    public sealed class GroundPropPresentationService : IGroundPropPresentationService
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterAtLoad() => EnsureRegistered();

        public static void EnsureRegistered()
        {
            GroundPropPresentation.Service ??= new GroundPropPresentationService();
        }

        /// <summary>How much of a lit sprite's width is the flame itself (the rest is stones and wood).</summary>
        public const float FlameShareOfSprite = 0.1f;

        public void Dress(GameObject host, Sprite sprite, in GroundGlow glow)
        {
            if (host == null) return;

            if (sprite != null)
            {
                var renderer = host.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = host.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                if (host.GetComponent<YSortSprite>() == null) host.AddComponent<YSortSprite>();
            }

            if (!glow.Lit) return;

            // The light is a child so the sprite's own sorting is left alone.
            var lamp = new GameObject("Glow");
            lamp.transform.SetParent(host.transform, false);
            var light = lamp.AddComponent<SceneLight>();
            // The look is the library's one live flame; the Def says how big, how far and what colour.
            // The BLOOM is the lit thing itself (the flames, a part of the sprite's width); how far it
            // lights the ground is the pool, which is the reach. Mixing the two up draws a ball of light
            // the size of the pool.
            float flames = sprite != null ? sprite.bounds.size.x * FlameShareOfSprite : 0f;
            LightPresets.ApplyFitting(light, LightPresets.Kind.Lantern, flames);
            light.Color = glow.Color;
            light.Intensity *= glow.Intensity;
            light.ReachMetres = glow.RangeMetres;
            light.FlickerAmount = glow.Flicker01;
            light.LampHeightMeters = glow.HeightMetres;
            light.CastsShadows = false;
        }
    }
}
