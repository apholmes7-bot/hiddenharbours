using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// ⭐ <b>A FIGURE BEING CARRIED</b> — somebody standing on a hull they are not steering, whose place
    /// and pose are stated by whoever carries them rather than read off their own motion. The arrival's
    /// passenger is the case that made it: she crosses a cabin and a deck the switcher knows nothing
    /// about, on a boat that is not hers.
    ///
    /// <para><b>Why a seam and not a class name</b> (owner playtest 2026-09-18: <i>"character was not
    /// mesh on intro boat"</i>). The arrival wrote her stance and holds straight onto
    /// <see cref="IsoCharacterSprite"/>, the SPRITE drawer. That was right while the sheets were the only
    /// way she was drawn, and silently wrong once she became a skinned mesh aboard (ADR 0044 d): the mesh
    /// figure is posed by the deck rider, the rider was never told she was aboard, so it stood down and
    /// the sheets drew her the whole way in. A carrier now says what is TRUE — this hull, this point, this
    /// stance, this heading, this speed — and the live figure decides how she is drawn. The carrier names
    /// no drawer at all.</para>
    ///
    /// <para><b>Frames.</b> The stand point is in the hull's RIG metres — +X starboard, +Y bow, +Z up from
    /// the keel — the frame the hull's deck-occupant slots take, so the figure is placed at the point the
    /// hull ranks her depth by. The heading is a COMPASS heading in degrees, the convention
    /// <see cref="IsoCharacterSprite.HoldHeading"/> takes. The speed is metres of DECK per second: the
    /// floor she actually crosses, which is the honest number for a gait on a moving boat.</para>
    ///
    /// <para><b>Contract.</b> <see cref="Carry"/> on every carried frame, from <c>Update</c>: the holds
    /// are consumed in a <c>LateUpdate</c>, so inputs go early. <see cref="Release"/> when she is set
    /// down: the stance goes back to free and both holds to her own motion, keeping the last heading so
    /// nobody snaps. Release is idempotent, and a figure never carried has nothing to release.</para>
    /// </summary>
    public interface ICarriedFigure
    {
        /// <summary>This frame's statement: she is on <paramref name="hullRoot"/> (the boat's physics
        /// root) at <paramref name="standRigLocalMetres"/>, braced in <paramref name="stance"/>, looking
        /// along <paramref name="headingDegrees"/> (compass) and crossing her floor at
        /// <paramref name="speedMetresPerSecond"/>.</summary>
        void Carry(Transform hullRoot, Vector3 standRigLocalMetres, CharacterStance stance,
                   float headingDegrees, float speedMetresPerSecond);

        /// <summary>Set her down: stance free, both holds handed back to her own motion. Idempotent.</summary>
        void Release();
    }

    /// <summary>Finds the figure a carrier should talk to.</summary>
    public static class CarriedFigure
    {
        /// <summary>
        /// The live figure of <paramref name="passenger"/>: the component that implements
        /// <see cref="ICarriedFigure"/> on it or a parent (the player's deck rider, which draws her as
        /// a mesh or as a sprite), else her bare <see cref="IsoCharacterSprite"/> behind the same seam,
        /// else null. Resolved once per carry, not per frame. The fallback allocates one small object.
        ///
        /// <para>The fallback is a rig with sheets and nothing else (a fixture's player, a greybox). It
        /// is the exact write the arrival made before this seam existed, so that rig draws as it
        /// always did.</para>
        /// </summary>
        public static ICarriedFigure Of(Transform passenger)
        {
            if (passenger == null) return null;
            ICarriedFigure live = passenger.GetComponentInParent<ICarriedFigure>();
            if (live != null) return live;
            IsoCharacterSprite sheets = passenger.GetComponentInParent<IsoCharacterSprite>();
            return sheets != null ? new SheetsOnly(sheets) : null;
        }

        /// <summary>The sheets on their own: the stance and both holds, written straight through.</summary>
        private sealed class SheetsOnly : ICarriedFigure
        {
            private readonly IsoCharacterSprite _sheets;
            private bool _carried;

            public SheetsOnly(IsoCharacterSprite sheets) => _sheets = sheets;

            public void Carry(Transform hullRoot, Vector3 standRigLocalMetres, CharacterStance stance,
                              float headingDegrees, float speedMetresPerSecond)
            {
                if (_sheets == null) return;
                _carried = true;
                _sheets.Stance = stance;
                _sheets.HoldHeading(headingDegrees);
                _sheets.HoldSpeed(speedMetresPerSecond);
            }

            public void Release()
            {
                if (!_carried) return;
                _carried = false;
                if (_sheets == null) return;
                _sheets.Stance = CharacterStance.Free;
                _sheets.ReleaseHeading();
                _sheets.ReleaseSpeed();
            }
        }
    }
}
