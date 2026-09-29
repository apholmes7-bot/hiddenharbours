using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Who a figure looks at</b> — the seam a presenter asks, and the one a later lane attaches its
    /// own figures to.
    ///
    /// <para>The answer is a WORLD point (the plane every transform stands in). The presenter, which
    /// owns the figure's transforms, carries it into the figure's own frame, measures the distance on
    /// the figure's ground in rig metres against <see cref="GameConfig.CharacterLookRadiusMetres"/>,
    /// and raises the aim point by <see cref="GameConfig.CharacterLookTargetHeightMetres"/>. So a
    /// source says only WHO; the radius, the height and the frame are the same for every figure.</para>
    /// </summary>
    public interface ICharacterLookTargetSource
    {
        /// <summary>
        /// The world point the figure keyed <paramref name="figureKey"/>, standing at
        /// <paramref name="figureWorldPosition"/>, looks toward — or false for nothing to look at.
        /// </summary>
        bool TryGetLookTarget(string figureKey, Vector3 figureWorldPosition, out Vector3 targetWorldPosition);
    }

    /// <summary>
    /// <b>The look seam's one entry point.</b> By default every figure that asks looks at the player
    /// (<see cref="GameServices.PlayerTransform"/>), and the presenter keeps the answer only within the
    /// radius. The player's own figure never asks, so she looks at nothing.
    ///
    /// <para>Like <see cref="CharacterFigurePresentation.Service"/>, this is not reset by
    /// <see cref="GameServices"/>: the default holds no state of its own (it reads the published player
    /// transform, which IS reset), and a test that installs a source restores it with
    /// <see cref="Restore"/>.</para>
    /// </summary>
    public static class CharacterLookTargets
    {
        static ICharacterLookTargetSource s_source;

        /// <summary>The source figures ask. Setting null restores the default.</summary>
        public static ICharacterLookTargetSource Source
        {
            get => s_source ?? PlayerLookTarget.Instance;
            set => s_source = value;
        }

        /// <summary>Put the default source back.</summary>
        public static void Restore() => s_source = null;

        /// <summary>Ask the installed source.</summary>
        public static bool TryGet(string figureKey, Vector3 figureWorldPosition, out Vector3 targetWorldPosition) =>
            Source.TryGetLookTarget(figureKey, figureWorldPosition, out targetWorldPosition);

        /// <summary>The default: the player, wherever she is published.</summary>
        sealed class PlayerLookTarget : ICharacterLookTargetSource
        {
            public static readonly PlayerLookTarget Instance = new PlayerLookTarget();

            public bool TryGetLookTarget(string figureKey, Vector3 figureWorldPosition,
                                         out Vector3 targetWorldPosition)
            {
                Transform player = GameServices.PlayerTransform;
                if (player == null)
                {
                    targetWorldPosition = default;
                    return false;
                }
                targetWorldPosition = player.position;
                return true;
            }
        }
    }
}
