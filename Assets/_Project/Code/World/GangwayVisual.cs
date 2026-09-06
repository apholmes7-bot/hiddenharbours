using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE PICTURE OF A BROW, RE-SOLVING ITS OWN SLOPE.</b> <see cref="GangwayPlatform"/> has always
    /// been walkable and has never been drawn: at Nine Mile Creek that left 12 m of crossing between the
    /// apron and the float run with no pixels at all. #735 built the ramp, plated it at three states of
    /// tide and TOOK IT OUT, because the only drawn ramp the pack owned was baked into the raft cell and
    /// rode with it — a hinge 2.0 m in the air at spring high and 2.3 m buried at spring low, at the one
    /// end of a gangway that is not supposed to move.
    ///
    /// <para><b>⭐ A BROW'S PICTURE IS A FUNCTION OF THE TIDE, NOT MERELY ITS POSITION</b> — which is what
    /// makes it unlike every other piece in this pack. Its hinge is bolted to fixed ground and its landing
    /// rides a float, so the drop between them is the whole tidal range and its slope swings from 1:30 at
    /// spring high to 1:2.6 at spring low. No single cell can be right at more than one water level. So
    /// the sheet carries a LADDER: 8 facings across, 9 rungs down
    /// (<c>NineMileCreekQuayFace.GangwayRungDrops</c>), and this component swaps the sprite. Every rung
    /// shares one cell and one pivot — measured, in the contract — so <b>the object never moves</b>: the
    /// hinge is exact at every tide by construction, and only the picture between the ends changes.</para>
    ///
    /// <para><b>The rung is rounded UP</b> (<c>GangwayRungFor</c>): the drawn ramp is never FLATTER than
    /// the real one. A flatter ramp lifts its foot off the planks and shows daylight under the rollers,
    /// which reads as broken; a steeper one settles its foot into a deck drawn 17 px deep, and the
    /// residual is at most one 0.55 m step of the ladder. That is the whole of the trade this component
    /// makes, and <c>NineMileCreekFloatTests</c> holds it to it at spring low, mean and spring high.</para>
    ///
    /// <para><b>Nothing interpolates and nothing is saved.</b> The drop is read straight off the brow —
    /// <see cref="GangwayPlatform.DropAt"/> over the live deterministic water level — so the ramp the
    /// player sees and the ramp the player walks cannot disagree about where the landing is, for the same
    /// reason <see cref="GangwayPlatform"/> reads its landing off the float rather than off the tide.</para>
    ///
    /// <para><b>Gate-off shape.</b> With no brow wired, no rungs, or a brow with no landing, the component
    /// leaves whatever sprite it has and holds still rather than throwing or blanking — the established
    /// shape beside it (<see cref="FloatingPlatformVisual"/>), so a scene that lost its float still draws
    /// the crossing where the builder put it.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GangwayVisual : MonoBehaviour
    {
        [Tooltip("The brow whose slope this picture is OF. Without it the sprite holds still.")]
        [SerializeField] private GangwayPlatform _brow;

        [Tooltip("The ladder's cells, SHALLOWEST DROP FIRST — one per rung of the baked slope axis. " +
                 "They share a cell and a pivot (the pack contract measures the union across all 72), " +
                 "so swapping between them cannot move the object.")]
        [SerializeField] private Sprite[] _rungs;

        [Tooltip("The drop, in metres from hinge to landing, each rung was BAKED at — same order and " +
                 "same length as the sprites. Take them from the art (the rig's gangwayDrops()), never " +
                 "pick them, or the ramp drawn and the ramp walked part company as the tide runs.")]
        [SerializeField] private float[] _rungDrops;

        private SpriteRenderer _renderer;
        private int _shown = -1;

        /// <summary>The brow it draws (for tests / tooling).</summary>
        public GangwayPlatform Brow => _brow;

        /// <summary>The ladder's baked drops, metres (for tests / tooling).</summary>
        public IReadOnlyList<float> RungDrops => _rungDrops;

        /// <summary>Which rung is on screen right now, or −1 before the first solve.</summary>
        public int ShownRung => _shown;

        /// <summary>Wire it from a builder: the brow, the ladder's cells and the drops they were baked
        /// at. The transform is set by the builder too and is never touched here — a brow that moved
        /// would be the defect this whole component exists to end.</summary>
        public void Configure(GangwayPlatform brow, Sprite[] rungs, float[] rungDrops)
        {
            _brow = brow;
            _rungs = rungs;
            _rungDrops = rungDrops;
            _shown = -1;
            Apply();
        }

        /// <summary>
        /// ⭐ <b>WHICH RUNG DRAWS A DROP OF <paramref name="dropMetres"/>: the shallowest rung that is not
        /// SHALLOWER than the truth.</b> Pure and static, so the whole choice is EditMode-testable with no
        /// scene and no sprites.
        ///
        /// <para>Rounding up rather than to the nearest is the design: a rung flatter than reality lifts
        /// the foot off the planks (daylight under the rollers — reads as broken), while a steeper one
        /// settles it into a deck the raft draws 17 px of hull beneath. The ladder is assumed ascending,
        /// as the rig bakes it; an empty ladder answers −1 rather than 0, because "no rungs" is not
        /// "rung zero".</para>
        /// </summary>
        public static int RungFor(IReadOnlyList<float> rungDrops, float dropMetres)
        {
            if (rungDrops == null || rungDrops.Count == 0) return -1;
            for (int i = 0; i < rungDrops.Count; i++)
                if (rungDrops[i] >= dropMetres) return i;
            return rungDrops.Count - 1;
        }

        private void OnEnable() => Apply();

        // The tide is slow but CONTINUOUS, and this is the one structure whose PICTURE follows it. The
        // work is a comparison and, on the handful of frames a cycle where the rung changes, one sprite
        // assignment — inside rule 7's budget, and a slow tick would step the ramp in visible jumps.
        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (_brow == null || _rungs == null || _rungDrops == null) return;
            if (_rungs.Length == 0 || _rungs.Length != _rungDrops.Length) return;
            if (_brow.Landing == null) return;

            int rung = RungFor(_rungDrops, _brow.DropAt(FloatingPlatform.WaterLevelNow()));
            if (rung < 0 || rung == _shown) return;

            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            // Only on a real change: a sprite assignment every frame for a ramp that re-solves a few
            // times a tide is pure churn, and the ladder is deliberately coarse enough that it is rare.
            _renderer.sprite = _rungs[rung];
            _shown = rung;
        }
    }
}
