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
            light.Shape = SceneLight.LightShape.Radial;
            light.Color = glow.Color;
            light.Range = glow.RangeMetres;
            light.Intensity = glow.Intensity;
            light.FlickerAmount = glow.Flicker01;
            light.LampHeightMeters = glow.HeightMetres;
            light.CastsShadows = false;
        }
    }
}
