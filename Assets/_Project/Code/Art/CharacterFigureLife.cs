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
    /// <para><b>The idle fidgets</b> (<see cref="IdleFidgetSchedule"/>): while the presenter says the figure
    /// stands idle, the library's fidgets play on the figure's own schedule, keyed by the world seed, the def
    /// id and the same one hash, so the player and every villager and skipper fidget by one set of rules and
    /// never in step. A Look fidget is drawn through the look above; a Clip fidget is handed to the presenter
    /// by <see cref="TryFidgetClip"/>.</para>
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
        /// <paramref name="now"/> (<see cref="IGameClock.FlowSeconds"/>). <paramref name="figureKey"/> is
        /// the figure's <see cref="ICharacterFigureIdentity.FigureKey"/> (null reads as empty), which names it
        /// to the look; <paramref name="keyHash"/> is the one hash its presenter took of that key, once, which
        /// seeds its blink with the def id (0 for no key). <paramref name="looks"/> false never asks for a look
        /// target. Each part is behind its own switch — <see cref="GameConfig.CharacterBlink"/>,
        /// <see cref="GameConfig.CharacterHeadLook"/>, <see cref="GameConfig.CharacterEyeLook"/> — read live,
        /// and behind the def carrying it.
        /// </summary>
        public IsoCharacterFigureRenderer.Life Step(CharacterSkinDef def, string figureKey, uint keyHash, double now,
                                                    IsoCharacterFigureRenderer figure, bool looks)
            => Step(def, figureKey, keyHash, now, figure, looks, idle: false);

        /// <summary>
        /// <see cref="Step(CharacterSkinDef, string, uint, double, IsoCharacterFigureRenderer, bool)"/> with the
        /// figure's idle fidgets (<see cref="IdleFidgetSchedule"/>). <paramref name="idle"/> is the presenter's
        /// word that the figure is standing idle this frame: the plain <c>idle</c> clip, not rowing, not at
        /// work or in a beat, no performance clip, and for the player no input this frame. False cancels a
        /// fidget at once. While a Look fidget plays it takes the look (the external target, when there is
        /// one, comes back the beat it ends); a Clip fidget is handed over by <see cref="TryFidgetClip"/>.
        /// The fidget's look plays whether or not the figure <paramref name="looks"/> at others, so the
        /// player, who looks at nobody, still looks about. Behind the library's own switch and
        /// <see cref="GameConfig.CharacterHeadLook"/>.
        /// </summary>
        public IsoCharacterFigureRenderer.Life Step(CharacterSkinDef def, string figureKey, uint keyHash, double now,
                                                    IsoCharacterFigureRenderer figure, bool looks, bool idle)
        {
            _key = figureKey ?? string.Empty;
            if (!_seeded || !ReferenceEquals(def, _def) || keyHash != _keyHash)
            {
                _def = def;
                _keyHash = keyHash;
                _seeded = true;
                _blink.Reset(def, CharacterFigureBlink.SeedFor(def != null ? def.Id : string.Empty, keyHash));
                _fidgets.Reset();
                _fidgetKeyFor = null;
            }

            var life = new IsoCharacterFigureRenderer.Life { BlinkEyes = CharacterSkinDef.NoFaceGroup };
            if (def == null) { _fidgets.Reset(); return life; }

            if (GameServices.CharacterBlink && def.HasBlink) life.BlinkEyes = _blink.EyesAt(now);

            if (StepFidget(def, keyHash, now, idle) && TryFidgetLook(def, figure, ref life)) return life;

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

        // ---------------------------------------------------------------- idle fidgets

        private readonly IdleFidgetClock _fidgets = new IdleFidgetClock();
        private IdleFidgetLibraryDef _library;
        private bool _libraryAssigned;
        private string _fidgetKeyFor;   // the skin id the key below was taken for (null: none yet)
        private int _fidgetSeedFor;
        private uint _fidgetKey;

        /// <summary>The fidget library this figure reads: the shipped one unless a test assigns another
        /// (null assigned = no fidgets).</summary>
        public IdleFidgetLibraryDef Library
        {
            get => _libraryAssigned ? _library : IdleFidgetLibraryDef.Shipped;
            set { _library = value; _libraryAssigned = true; }
        }

        /// <summary>The fidget playing as of the last step (tests, plates).</summary>
        public IdleFidgetMoment Fidget => _fidgets.Current;

        /// <summary>The figure's fidget schedule key (<see cref="IdleFidgetSchedule.KeyFor"/>) as last taken.</summary>
        public uint FidgetKey => _fidgetKey;

        private bool StepFidget(CharacterSkinDef def, uint keyHash, double now, bool idle)
        {
            int seed = GameServices.Environment != null ? GameServices.Environment.WorldSeed : 0;
            if (!ReferenceEquals(_fidgetKeyFor, def.Id) || seed != _fidgetSeedFor)
            {
                _fidgetKeyFor = def.Id;
                _fidgetSeedFor = seed;
                _fidgetKey = IdleFidgetSchedule.KeyFor(seed, def.Id, keyHash);
            }
            return _fidgets.Step(idle, now, Library, def, _fidgetKey);
        }

        // A Look fidget's gaze, as the look's target: from the eye on the frame last drawn, out along the
        // gaze to the library's distance. False (and the look decides as before) for a Clip fidget, a figure
        // that has not drawn yet, a def with no look, or the head's turn switched off.
        private bool TryFidgetLook(CharacterSkinDef def, IsoCharacterFigureRenderer figure,
                                   ref IsoCharacterFigureRenderer.Life life)
        {
            IdleFidgetMoment m = _fidgets.Current;
            if (!m.Active || figure == null || !def.HasLook || figure.DrawnFrame < 0) return false;
            if (!GameServices.CharacterHeadLook) return false;
            if (!m.Def.TrySampleGaze(m.LocalSeconds, out float yaw, out float up)) return false;
            IdleFidgetLibraryDef library = Library;
            float distance = library != null ? library.LookDistanceMetres : 0f;
            if (!(distance > 0f)) return false;

            Vector3 eye = figure.HeadLocalToFigure(figure.HeadMidLocal);
            life.HasTarget = true;
            life.Target = IdleFidgetDef.GazeTarget(eye, yaw, up, distance);
            life.HeadLook = true;
            life.EyeLook = GameServices.CharacterEyeLook;
            return true;
        }

        /// <summary>
        /// The clip a Clip fidget asks for this frame, when <paramref name="skin"/> carries it: its state key
        /// and its frame (played once from the fidget's start, holding the last frame). False while no Clip
        /// fidget plays. The presenter swaps it in for the idle clip after its own gates, and only where the
        /// clip may draw as a mesh.
        /// </summary>
        public bool TryFidgetClip(CharacterSkinDef skin, out string stateKey, out int frame)
        {
            stateKey = null;
            frame = 0;
            IdleFidgetMoment m = _fidgets.Current;
            if (!m.Active || skin == null || m.Def.Kind != IdleFidgetDef.Channel.Clip) return false;
            if (!skin.TryGetClip(m.Def.ClipKey, out CharacterSkinDef.SkinClip clip) || clip.FrameCount <= 0) return false;
            if (!clip.KeysWellFormed(skin.BoneCount)) return false;
            stateKey = m.Def.ClipKey;
            frame = IdleFidgetDef.ClipFrame(clip, m.LocalSeconds);
            return true;
        }
    }
}
