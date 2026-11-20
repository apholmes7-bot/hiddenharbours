using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.App
{
    /// <summary>
    /// The persistent hull drops behind this pier while she lies on its north side. The wharf deck sorts
    /// below the decor band (<see cref="SortingBands.WharfDeckMin"/> to <see cref="SortingBands.WharfDeckMax"/>),
    /// so a hull sorting by where she lies would draw over the deck she is moored behind.
    ///
    /// <para><b>An input, not a writer</b> (ADR 0059 decision 4). The boat's own sorting component asks
    /// <see cref="TryOrderAt"/> each frame and writes her group once; south of the pier (or off its ends)
    /// this rule stands aside and she sorts by where she lies, in front of the pier and among its pilings.
    /// It used to write the same group every <c>LateUpdate</c> as a second writer.</para>
    ///
    /// <para><b>Asked where her picture is.</b> The point it is given is her sort point: the root plus the
    /// tide's screen rise (Core <c>HullSorting.SortPoint</c>), not the bare root, so "north of the pier"
    /// means her picture's waterline is north of it at this state of the tide.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SortingGroup))]
    public sealed class StPetersPierBoatSorting : MonoBehaviour, IHullSortingInput
    {
        [SerializeField] private Rect _pier;
        [Tooltip("The order she takes while she lies north of the pier: under the wharf deck rows south of her.")]
        [SerializeField] private int _behindOrder = -1;

        /// <summary>The pier's deck footprint, read back for tests.</summary>
        public Rect Pier => _pier;

        /// <summary>The order she takes behind the pier, read back for tests.</summary>
        public int BehindOrder => _behindOrder;

        /// <summary>Set the pier's deck footprint and the order she takes behind it.</summary>
        public void Configure(Rect pier, int behindOrder)
        {
            _pier = pier; _behindOrder = behindOrder;
        }

        /// <summary>True when <paramref name="at"/> lies north of the pier, between its ends.</summary>
        public static bool Behind(Rect pier, Vector2 at) =>
            at.x >= pier.xMin && at.x <= pier.xMax && at.y > pier.yMax;

        /// <inheritdoc/>
        public bool TryOrderAt(Vector2 waterlinePivot, out int order)
        {
            order = _behindOrder;
            return enabled && Behind(_pier, waterlinePivot);
        }
    }
}
