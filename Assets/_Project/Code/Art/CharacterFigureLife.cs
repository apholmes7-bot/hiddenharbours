using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>One figure's life between its clip's keys</b> (character PR 2a; rig 9 README §4 and §5): its
    /// blink, and what it looks at, as the <see cref="IsoCharacterFigureRenderer.Life"/> a presenter
    /// hands to <see cref="IsoCharacterFigureRenderer.SetPose(string, int, in IsoCharacterFigureRenderer.Life)"/>.
    /// One per figure, owned by the presenter that poses it — the cast's
    /// <see cref="CharacterFigurePresenter"/> and the player's own — so both play the rig's life by one
    /// set of rules.
    ///
    /// <para><b>The blink</b> is <see cref="CharacterFigureBlink"/>, seeded by the def id and the ONE hash
    /// the presenter took of the figure's key (<see cref="ICharacterFigureIdentity"/>; ADR 0044 §9), and
    /// re-seeded whenever either changes, so the same person blinks the same way on every run. It plays on
    /// the clock the clips play on.</para>
    ///
    /// <para><b>The look</b> asks the Core seam (<see cref="CharacterLookTargets"/>) WHO; the radius, the
    /// aim height and the frame are this class's and the same for every figure: the answer is carried
    /// onto the figure's own ground (<see cref="IsoCharacterFigureRenderer.TryFigureGround"/>), kept only
    /// within <see cref="GameConfig.CharacterLookRadiusMetres"/> of the feet, and raised to
    /// <see cref="GameConfig.CharacterLookTargetHeightMetres"/>. A figure that must look at nothing (the
    /// player's own) passes <c>looks: false</c> and never asks.</para>
    ///
    /// <para>Presentation only (rule 5): nothing here is saved or read by a simulation system, and it
    /// never touches <c>UnityEngine.Random</c>. No allocation per call (rule 7).</para>
    /// </summary>
    public sealed class CharacterFigureLife
    {
        private readonly CharacterFigureBlink _blink = new CharacterFigureBlink();
        private CharacterSkinDef _def;
        private string _key;
        private uint _keyHash;
        private bool _seeded;

        /// <summary>The figure's key, as last stepped: the one its look is asked for, or empty for none.</summary>
        public string Key => _key ?? string.Empty;

        /// <summary>The one hash of that key, as last stepped: the one its blink is seeded by.</summary>
        public uint KeyHash => _keyHash;

        /// <summary>The blink's schedule — read by a test.</summary>
        public CharacterFigureBlink Blink => _blink;

        /// <summary>
        /// This moment's life for <paramref name="figure"/>, playing <paramref name="def"/> at
        /// <paramref name="now"/> (<see cref="IGameClock.TotalSeconds"/>). <paramref name="figureKey"/> is
        /// the figure's <see cref="ICharacterFigureIdentity.FigureKey"/> (null reads as empty), which names it
        /// to the look; <paramref name="keyHash"/> is the one hash its presenter took of that key, once, which
        /// seeds its blink with the def id (0 for no key). <paramref name="looks"/> false never asks for a look
        /// target. Each part is behind its own switch — <see cref="GameConfig.CharacterBlink"/>,
        /// <see cref="GameConfig.CharacterHeadLook"/>, <see cref="GameConfig.CharacterEyeLook"/> — read live,
        /// and behind the def carrying it.
        /// </summary>
        public IsoCharacterFigureRenderer.Life Step(CharacterSkinDef def, string figureKey, uint keyHash, double now,
                                                    IsoCharacterFigureRenderer figure, bool looks)
        {
            _key = figureKey ?? string.Empty;
            if (!_seeded || !ReferenceEquals(def, _def) || keyHash != _keyHash)
            {
                _def = def;
                _keyHash = keyHash;
                _seeded = true;
                _blink.Reset(def, CharacterFigureBlink.SeedFor(def != null ? def.Id : string.Empty, keyHash));
            }

            var life = new IsoCharacterFigureRenderer.Life { BlinkEyes = CharacterSkinDef.NoFaceGroup };
            if (def == null) return life;

            if (GameServices.CharacterBlink && def.HasBlink) life.BlinkEyes = _blink.EyesAt(now);

            if (!looks || figure == null || !def.HasLook) return life;
            bool head = GameServices.CharacterHeadLook, eyes = GameServices.CharacterEyeLook;
            if (!head && !eyes) return life;
            if (!CharacterLookTargets.TryGet(_key, figure.transform.position, out Vector3 world)) return life;
            if (!figure.TryFigureGround(world, out Vector3 ground)) return life;

            float radius = GameServices.CharacterLookRadiusMetres;
            if (!(ground.x * ground.x + ground.y * ground.y <= radius * radius)) return life;

            life.HasTarget = true;
            life.Target = new Vector3(ground.x, ground.y, GameServices.CharacterLookTargetHeightMetres);
            life.HeadLook = head;
            life.EyeLook = eyes;
            return life;
        }
    }
}
