using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>Fits a native brow picture to its world-plan hinge and live float toe.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(GangwayVisual))]
    public sealed class NmcWharfBrowSpanVisual : MonoBehaviour
    {
        [SerializeField] GangwayVisual _visual;
        [SerializeField] float _planY;

        void LateUpdate()
        {
            if (_visual == null || _visual.Brow == null || _visual.ShownRung < 0) return;
            var brow = _visual.Brow;
            if (brow.Landing == null || _visual.ShownRung >= _visual.RungDrops.Count) return;
            float run = Mathf.Abs(brow.ShoreEnd.y - brow.FloatEnd.y);
            float nativeSpan = run * IsoGround.GroundDepthScale
                + _visual.RungDrops[_visual.ShownRung] * IsoGround.HeightScale;
            if (nativeSpan <= 1e-5f) return;
            float drop = brow.AbutmentElevation - brow.Landing.DeckElevationNow();
            float scale = (run + drop * IsoGround.HeightScale) / nativeSpan;
            var size = transform.localScale;
            size.y = scale;
            transform.localScale = size;
            var at = transform.position;
            at.y = brow.ShoreEnd.y + brow.AbutmentElevation * IsoGround.HeightScale
                - (brow.ShoreEnd.y - _planY) * IsoGround.GroundDepthScale * scale;
            transform.position = at;
        }
    }
}
