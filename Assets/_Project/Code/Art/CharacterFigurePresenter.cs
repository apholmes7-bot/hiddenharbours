using System;
using HiddenHarbours.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>A member of the CAST, drawn as one skinned mesh through the iso facet pass while they stand on a
    /// facet hull</b> (ADR 0044, amendment 2026-09-17, behind <see cref="GameConfig.MeshCast"/>), <b>or,
    /// for a villager, on her own feet ashore</b> (amendment 2026-09-27, behind
    /// <see cref="GameConfig.MeshCastAshore"/> as well). The cast's twin of the player's
    /// <c>DeckRiderMeshPresenter</c>, on the far side of the Core seam: Boats puts it on a skipper and World
    /// on a villager through <see cref="CharacterFigurePresentation"/>, and neither learns this type exists
    /// (rule 4).
    ///
    /// <para><b>A reader, like the player's.</b> Stance, gait and facing are the character's own
    /// <see cref="IsoCharacterSprite"/>'s; where the feet are and which way the deck points are published
    /// by whoever stands the character (<see cref="ICharacterFigureStand"/>). This component turns
    /// (stance, gait) into a clip key, the clip into a frame, and the frame into one posed mesh at the
    /// published stand point — the same clip, direction and frame the sprite would show — and decides
    /// nothing else about the person.</para>
    ///
    /// <para><b>⚠ It never owns whether the character is SEEN.</b> <see cref="SpriteRenderer.enabled"/>
    /// belongs to whoever stands the character (a villager's shelter, a routine) and is only ever READ
    /// here: a character hidden in one picture is hidden in both. The sprite is stood down with
    /// <see cref="Renderer.forceRenderingOff"/>, only on the frames the mesh really draws, and given back
    /// the moment it does not — and only if this component was the one that set it. Nothing else in the
    /// project writes that flag (grep, 2026-09-17), which is why it is the one this component may own.</para>
    ///
    /// <para><b>Every gate falls back to the sprite</b>, and says which one shut
    /// (<see cref="WhyNot"/>, <see cref="NotDrawingReason"/>): no stand, no hull under a stand that is not
    /// an ashore stand, a sprite that is disabled or hidden by somebody else, a sprite re-sorted off the
    /// hull's picture, the switch off, a suspended character, no skin or an unusable one, a sprite hull, no
    /// clip, a state not in <see cref="CharacterSkinDef.MeshStates"/> (ADR 0041), a def that refuses to
    /// build, and ashore, a facet-id pool that has none left for her.</para>
    ///
    /// <para><b>Ashore</b> (<see cref="ICharacterFigureAshoreStand"/>, the player's ashore path's twin):
    /// a figure of her own stands under her sprite at her feet and takes a facet id of her own through
    /// <see cref="IsoCharacterFigureRenderer.EnterAshore"/> (#861). The id is taken at her first draw, kept
    /// while she is sheltered or suspended, and given back when a switch goes off or this component is
    /// disabled or destroyed. Refused at exhaustion, she keeps her whole sprite, builds nothing, and does
    /// not ask again until a switch is turned off and on again or she is re-enabled. Her sort is copied
    /// from her sprite every frame AFTER <c>YSortSprite</c> wrote it, her facing is her sprite's heading,
    /// and her idle phase is moved off her neighbours' by her <see cref="ICharacterFigureAshoreStand.FigureKey"/>.
    /// The two aboard-only gates do not apply: she stands on no hull, and her sprite is re-sorted every
    /// frame by design, so ashore the figure follows the sort instead of refusing it.</para>
    ///
    /// <para><b>⚠ The re-sorted sprite, and why it is a gate.</b> The mesh can only ever be seen INSIDE
    /// its hull's picture — each hull's overlay quad re-composes the facet pass at the hull's own sorting
    /// order. A sprite whose owner has re-sorted it is being staged somewhere that picture cannot reach:
    /// the St Peters arrival raises its skipper over the cabin room while the player is below decks. So
    /// the sorting the sprite had when the figure was attached is remembered, and while it differs the
    /// sprite keeps the draw. That is a READ of somebody else's decision, not a second opinion about it.</para>
    ///
    /// <para><b>Between the clip's keys: the rig's blink and look</b> (character PR 2a). A
    /// <see cref="CharacterFigureLife"/> per figure, keyed by the stand's one stable identity
    /// (<see cref="ICharacterFigureIdentity"/>; a moored boat answers with her owner's id): the skipper
    /// blinks on her own seeded schedule and looks at whoever the Core seam names
    /// (<see cref="CharacterLookTargets"/>; the player, by default) within the configured radius.
    /// Presentation only, and each part behind its own GameConfig switch.</para>
    ///
    /// <para><b>Budget (rule 7).</b> No allocation per frame: the refusal is an enum and its words are
    /// only built when somebody reads them; the hull lookup and the def's usability are cached per
    /// reference; the posed mesh is built once per (hull, skin) and hidden rather than destroyed when a
    /// gate shuts.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]   // after the default-order LateUpdates that publish its inputs
    public sealed class CharacterFigurePresenter : MonoBehaviour, ICharacterFigure
    {
        /// <summary>The name of the posed figure's GameObject under the hull's posed mesh.</summary>
        public const string FigureObjectName = "MeshCastFigure";

        /// <summary>The name of the ashore figure's GameObject, under the character's own sprite.</summary>
        public const string AshoreFigureObjectName = "MeshCastFigureAshore";

        /// <summary>Which gate shut, for tests, plates and anyone looking at a figure that came back as a
        /// sprite. <see cref="None"/> while the mesh draws.</summary>
        public enum Refusal
        {
            None = 0,
            NotPosedYet,
            NoStand,
            Ashore,
            NoSpriteRenderer,
            SpriteDisabled,
            SpriteHiddenElsewhere,
            SpriteRestaged,
            NoConfig,
            SwitchOff,
            NoCharacter,
            CharacterSuspended,
            NoSkin,
            SkinUnusable,
            NotAFacetHull,
            NoClipForState,
            StateNotMeshed,
            FigureRefused,
            ClipVanished,
            PoseRefused,
            PresenterDisabled,
            FacetIdRefused,
        }

        // ---------------------------------------------------------------- state

        private ICharacterFigureStand _stand;
        private SpriteRenderer _sprite;
        private bool _restCaptured;
        private int _restSortingLayerId;
        private int _restSortingOrder;
        private bool _hidSprite;

        private IsoCharacterFigureRenderer _figure;
        private Transform _figureRoot;
        private IsoFacetHullRenderer _hull;
        private CharacterSkinDef _configured;
        private string _stateKey;
        private double _stateStartSeconds;
        private readonly CharacterFigureLife _life = new CharacterFigureLife();

        private Transform _hullVisual;                 // the stand's hull transform last looked up
        private IsoFacetHullRenderer _hullCandidate;   // ... and what was on it
        private CharacterSkinDef _usabilityOf;
        private bool _usable;
        private CharacterSkinDef _refusedSkin;         // a def that threw building a figure: said once

        // Ashore (amendment 2026-09-27): a figure of her own under her sprite, the latch that keeps a
        // refused id from being asked for again every frame, and her phase key, hashed once in Configure.
        private IsoCharacterFigureRenderer _ashoreFigure;
        private CharacterSkinDef _ashoreConfigured;
        private string _ashoreStateKey;
        private double _ashoreStateStartSeconds;
        private bool _ashoreRefused;
        private uint _ashoreKeyHash;

        private Refusal _refusal = Refusal.NotPosedYet;
        private string _refusalKey;
        private CharacterStance _refusalStance;
        private CharacterGait _refusalGait;
        private int _refusalFrame;
        private int _refusalLayer, _refusalOrder;
        private CharacterSkinDef _refusalSkin;
        private bool _refusalAshoreSwitch;             // SwitchOff: MeshCastAshore was the one off

        // ---------------------------------------------------------------- published

        /// <inheritdoc/>
        public bool DrawsInsteadOfSprite { get; private set; }

        /// <summary>Who this figure is posed from, or null before <see cref="Configure"/>.</summary>
        public ICharacterFigureStand Stand => _stand;

        /// <summary>True while this component is holding the sprite's
        /// <see cref="Renderer.forceRenderingOff"/> — the only write it ever makes to the sprite.</summary>
        public bool HidesSprite => _hidSprite;

        /// <summary>The skinned figure, once one exists. Null before the character has ever drawn.</summary>
        public IsoCharacterFigureRenderer Figure => _figure;

        /// <summary>The facet hull the figure was built under, or null.</summary>
        public IsoFacetHullRenderer Hull => _hull;

        /// <summary>The def the figure was built from — always the character art def's own
        /// <see cref="CharacterVisualDef.Skin"/>, so the sheets and the mesh cannot describe two people.</summary>
        public CharacterSkinDef Skin => _configured;

        /// <summary>The ABOARD figure's clip key drawn this frame, or null. Ashore, read
        /// <see cref="AshoreFigure"/>.</summary>
        public string DrawnStateKey => _figure != null ? _figure.DrawnStateKey : null;

        /// <summary>The ABOARD figure's clip frame drawn this frame (after the poisoned-frame fence), or
        /// -1. Ashore, read <see cref="AshoreFigure"/>.</summary>
        public int DrawnFrame => _figure != null ? _figure.DrawnFrame : -1;

        /// <summary>The frame the ABOARD figure ASKED for before the fence, or -1.</summary>
        public int RequestedFrame => _figure != null ? _figure.RequestedFrame : -1;

        /// <summary>True when the clip the state map asked for was not baked and a shorter one stood in.</summary>
        public bool FellBackToGait { get; private set; }

        /// <summary>Where the figure was placed, in the hull's posed-mesh local metres (the rig frame).</summary>
        public Vector3 FigureLocalMetres { get; private set; }

        /// <summary>The yaw applied about the rig's up axis, in degrees.</summary>
        public float FigureYawDegrees { get; private set; }

        /// <summary>The figure's blink and look (character PR 2a) — read by a test.</summary>
        public CharacterFigureLife FigureLife => _life;

        /// <summary>Which gate shut this frame; <see cref="Refusal.None"/> while the mesh draws.</summary>
        public Refusal WhyNot => _refusal;

        /// <summary>Her ASHORE figure, once one exists: built at her first draw ashore with both switches on,
        /// and kept, with its facet id, until a switch goes off or this component is disabled, destroyed or
        /// re-configured. Null for a stand aboard, with a switch off, and after a refusal.</summary>
        public IsoCharacterFigureRenderer AshoreFigure => _ashoreFigure;

        /// <summary>True from the pose the facet-id pool refused her until a switch is turned off and on
        /// again or this component is re-enabled or re-configured. Never asked again in between.</summary>
        public bool AshoreRefused => _ashoreRefused;

        /// <summary>The yaw given to the ashore figure, in degrees about the rig's up: her sprite's compass
        /// heading through the skin's measured azimuth sign.</summary>
        public float AshoreYawDegrees { get; private set; }

        /// <summary>
        /// <b>Why this character is not drawing as a mesh</b>, in words, or null while it is. Built when
        /// read — never on the frame path — because every reason is a silent, correct-looking no-op (the
        /// sprite simply keeps drawing) and a plate that came back as the sprite must still say why.
        /// </summary>
        public string NotDrawingReason
        {
            get
            {
                switch (_refusal)
                {
                    case Refusal.None: return null;
                    case Refusal.NotPosedYet: return "not posed yet";
                    case Refusal.NoStand: return "no stand — nobody publishes where this character stands";
                    case Refusal.Ashore:
                        return "no hull under them — ashore, or a hull that holds no deck slot for them — and " +
                               "the stand is not an ashore stand: only a villager on her own feet is drawn ashore";
                    case Refusal.NoSpriteRenderer: return "no SpriteRenderer on the character";
                    case Refusal.SpriteDisabled:
                        return "the sprite is disabled by whoever stands the character — hidden in both pictures";
                    case Refusal.SpriteHiddenElsewhere:
                        return "the sprite's forceRenderingOff was set by something other than this figure";
                    case Refusal.SpriteRestaged:
                        return $"the sprite was re-sorted off its hull's picture (layer {_refusalLayer}, order " +
                               $"{_refusalOrder}; attached at layer {_restSortingLayerId}, order " +
                               $"{_restSortingOrder}) — only the sprite can be drawn there";
                    case Refusal.NoConfig: return "no GameConfig";
                    case Refusal.SwitchOff:
                        return _refusalAshoreSwitch ? "GameConfig.MeshCastAshore is off" : "GameConfig.MeshCast is off";
                    case Refusal.NoCharacter: return "no IsoCharacterSprite to read stance and gait from";
                    case Refusal.CharacterSuspended:
                        return "the IsoCharacterSprite is suspended — another driver owns the picture";
                    case Refusal.NoSkin: return "no CharacterSkinDef on the character's art def";
                    case Refusal.SkinUnusable:
                        return $"CharacterSkinDef '{SkinId(_refusalSkin)}' is not usable — re-bake it";
                    case Refusal.NotAFacetHull: return "the hull under them is not a facet mesh hull";
                    case Refusal.NoClipForState:
                        return $"no clip for stance {_refusalStance} / gait {_refusalGait}";
                    case Refusal.StateNotMeshed:
                        return $"state '{_refusalKey}' is not in CharacterSkinDef.MeshStates (ADR 0041)";
                    case Refusal.FigureRefused:
                        return $"CharacterSkinDef '{SkinId(_refusalSkin)}' refused to build a figure — see the log";
                    case Refusal.ClipVanished: return $"clip '{_refusalKey}' vanished";
                    case Refusal.PoseRefused: return $"could not pose '{_refusalKey}' frame {_refusalFrame}";
                    case Refusal.PresenterDisabled: return "presenter disabled";
                    case Refusal.FacetIdRefused:
                        return "ashore — EnterAshore refused her (the facet-id pool is used up): she keeps her " +
                               "whole sprite until a switch is turned off and on again or she is re-enabled";
                    default: return _refusal.ToString();
                }
            }
        }

        /// <summary>
        /// Pose this figure from <paramref name="stand"/> every frame from now on. Remembers the sprite's
        /// sorting AS IT IS NOW, which is the staging the figure can stand in for — so call it after the
        /// stand has finished placing the sprite (<c>MooredBoat</c> attaches last). An ashore stand's key
        /// is hashed here, once; an ashore figure from an earlier stand gives its id back, and a refusal
        /// is forgotten.
        /// </summary>
        public void Configure(ICharacterFigureStand stand)
        {
            RestoreSprite();
            Release();
            ReleaseAshore();
            _stand = stand;
            _ashoreKeyHash = KeyHash(stand is ICharacterFigureAshoreStand ashore ? ashore.FigureKey : null);
            _sprite = GetComponent<SpriteRenderer>();
            _restCaptured = false;
            if (_sprite != null) CaptureRestStaging(_sprite);
            _hullVisual = null;
            _hullCandidate = null;
            _usabilityOf = null;
            _refusedSkin = null;
            DrawsInsteadOfSprite = false;
            _refusal = Refusal.NotPosedYet;
        }

        // ---------------------------------------------------------------- lifecycle

        private void LateUpdate()
        {
            ICharacterFigureStand stand = _stand;
            // The stand's own word for aboard: a hull under the feet. A moored skipper always has one; a
            // stand ashore answers null and the figure stands down before anything else is read.
            PoseFigure(stand, IsLive(stand) && stand.FigureHull != null);
        }

        private void OnDisable()
        {
            DrawsInsteadOfSprite = false;
            Stop(Refusal.PresenterDisabled);
            ReleaseAshore();   // her id goes back now; enabled again, she asks at her next draw
        }

        private void OnDestroy()
        {
            RestoreSprite();
            Release();
            ReleaseAshore();
        }

        // ---------------------------------------------------------------- the one path

        /// <inheritdoc/>
        /// <remarks>Called from this component's own <c>LateUpdate</c> (execution order 100, after the
        /// sprite, the stand and anything that holds them have run this frame), and directly by tests.</remarks>
        public void PoseFigure(ICharacterFigureStand stand, bool aboard)
        {
            DrawsInsteadOfSprite = false;

            if (!IsLive(stand)) { Stop(Refusal.NoStand); return; }
            if (!aboard)
            {
                // A villager on her own feet (amendment 2026-09-27). Any other stand with no hull keeps
                // its sprite, exactly as before.
                if (stand is ICharacterFigureAshoreStand ashore) { PoseAshore(ashore); return; }
                Stop(Refusal.Ashore);
                return;
            }

            // Aboard, an ashore figure (a stand that has been drawn ashore) hides and keeps her id, as the
            // player's does; with a switch off it gives the id back. Nothing here moves for a stand that
            // has never been ashore, so every aboard frame is the frame before the amendment.
            StandDownAshore(GameServices.Config);

            SpriteRenderer sprite = ResolveSprite();
            if (sprite == null) { Stop(Refusal.NoSpriteRenderer); return; }
            if (!sprite.enabled) { Stop(Refusal.SpriteDisabled); return; }
            if (!_hidSprite && sprite.forceRenderingOff) { Stop(Refusal.SpriteHiddenElsewhere); return; }
            if (sprite.sortingLayerID != _restSortingLayerId || sprite.sortingOrder != _restSortingOrder)
            {
                _refusalLayer = sprite.sortingLayerID;
                _refusalOrder = sprite.sortingOrder;
                Stop(Refusal.SpriteRestaged);
                return;
            }

            GameConfig config = GameServices.Config;
            if (config == null) { Stop(Refusal.NoConfig); return; }
            if (!config.MeshCast) { _refusalAshoreSwitch = false; Stop(Refusal.SwitchOff); return; }

            IsoCharacterSprite character = stand.FigureCharacter;
            if (character == null) { Stop(Refusal.NoCharacter); return; }
            // Suspended = a clip player or a work animator owns the sprite's picture this frame, and the
            // skin carries none of those poses. The sprite shows what it is being made to show.
            if (character.IsSuspended) { Stop(Refusal.CharacterSuspended); return; }

            CharacterVisualDef visual = character.Visual;
            CharacterSkinDef skin = visual != null ? visual.Skin : null;
            if (skin == null) { Stop(Refusal.NoSkin); return; }
            if (!IsUsable(skin)) { _refusalSkin = skin; Stop(Refusal.SkinUnusable); return; }

            IsoFacetHullRenderer hull = ResolveHull(stand.FigureHull);
            if (hull == null) { Stop(Refusal.NotAFacetHull); return; }

            // The REQUESTED stance with the gait the sprite is playing — the player presenter's rule, for
            // its reason (DrawnStance reports the SHEETS' coverage, not what the person is doing).
            CharacterStance stance = character.Stance;
            CharacterGait gait = character.Gait;
            if (!CharacterSkinStateMap.Resolve(skin, stance, gait, out string stateKey, out bool fellBack))
            {
                _refusalStance = stance;
                _refusalGait = gait;
                Stop(Refusal.NoClipForState);
                return;
            }
            FellBackToGait = fellBack;

            // ADR 0041: the switch is PER STATE. A state not listed keeps its sheet.
            if (!skin.DrawsAsMesh(stateKey)) { _refusalKey = stateKey; Stop(Refusal.StateNotMeshed); return; }

            if (!EnsureFigure(skin, hull)) { _refusalSkin = skin; Stop(Refusal.FigureRefused); return; }
            if (!skin.TryGetClip(stateKey, out CharacterSkinDef.SkinClip clip))
            {
                _refusalKey = stateKey;
                Stop(Refusal.ClipVanished);
                return;
            }

            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0d;
            if (!string.Equals(stateKey, _stateKey, StringComparison.Ordinal))
            {
                _stateKey = stateKey;
                _stateStartSeconds = now;
            }

            // RULE 5, as the player's: a looping clip runs from absolute game time (a pure function of
            // seed and time); a one-shot runs from the moment its state was entered and holds its end.
            double clipStart = clip.Loop ? 0d : _stateStartSeconds;
            int seed = GameServices.Environment != null ? GameServices.Environment.WorldSeed : 0;
            int frame = CharacterSkinPose.FrameFor(clip, seed, now, clipStart);

            // Stood BEFORE it is posed: the look reads where the figure stands this frame.
            Place(stand, skin);
            string figureKey = stand is ICharacterFigureIdentity identity ? identity.FigureKey : string.Empty;
            IsoCharacterFigureRenderer.Life life = _life.Step(skin, figureKey, now, _figure, looks: true);
            if (!_figure.SetPose(stateKey, frame, life))
            {
                _refusalKey = stateKey;
                _refusalFrame = frame;
                Stop(Refusal.PoseRefused);
                return;
            }

            _figure.Visible = true;
            sprite.forceRenderingOff = true;
            _hidSprite = true;
            DrawsInsteadOfSprite = true;
            _refusal = Refusal.None;
        }

        /// <summary>
        /// <b>Ashore: a villager on her own feet</b> (<see cref="ICharacterFigureAshoreStand"/>, amendment
        /// 2026-09-27; the player's ashore path's twin). With either switch off this is the frame before the
        /// amendment exactly: no ashore figure, no facet id, and her sprite as her owner leaves it. With both
        /// on, her own figure draws instead of her sprite while every gate below holds.
        ///
        /// <para><b>The switch is read FIRST</b>, before the sprite, so a switch turned off while she is
        /// sheltered still gives her id back. After it come the aboard path's gates, less the two that are
        /// aboard's alone: she stands on no hull, and her sprite is re-sorted every frame by design, so the
        /// figure copies that sort instead of refusing it.</para>
        /// </summary>
        private void PoseAshore(ICharacterFigureAshoreStand stand)
        {
            // The aboard figure stands down exactly as it does for any stand ashore (hidden, kept).
            StandDownAboard();

            GameConfig config = GameServices.Config;
            if (!AshoreSwitchOn(config))
            {
                // ⭐ SWITCH OFF IS THE SPRITE, BYTE FOR BYTE: her figure goes and her id goes back, now.
                _refusalAshoreSwitch = config != null && config.MeshCast;
                ReleaseAshore();
                Stop(config == null ? Refusal.NoConfig : Refusal.SwitchOff);
                return;
            }

            SpriteRenderer sprite = ResolveSprite();
            if (sprite == null) { Stop(Refusal.NoSpriteRenderer); return; }
            // Sheltered: her owner has switched her sprite off. The figure hides with it and keeps her id,
            // so a door costs nothing, and she is hidden in both pictures.
            if (!sprite.enabled) { Stop(Refusal.SpriteDisabled); return; }
            if (!_hidSprite && sprite.forceRenderingOff) { Stop(Refusal.SpriteHiddenElsewhere); return; }

            IsoCharacterSprite character = stand.FigureCharacter;
            if (character == null) { Stop(Refusal.NoCharacter); return; }
            if (character.IsSuspended) { Stop(Refusal.CharacterSuspended); return; }

            CharacterVisualDef visual = character.Visual;
            CharacterSkinDef skin = visual != null ? visual.Skin : null;
            if (skin == null) { Stop(Refusal.NoSkin); return; }
            if (!IsUsable(skin)) { _refusalSkin = skin; Stop(Refusal.SkinUnusable); return; }

            // Stance as REQUESTED, exactly as aboard (see there).
            CharacterStance stance = character.Stance;
            CharacterGait gait = character.Gait;
            if (!CharacterSkinStateMap.Resolve(skin, stance, gait, out string stateKey, out bool fellBack))
            {
                _refusalStance = stance;
                _refusalGait = gait;
                Stop(Refusal.NoClipForState);
                return;
            }
            FellBackToGait = fellBack;

            if (!skin.DrawsAsMesh(stateKey)) { _refusalKey = stateKey; Stop(Refusal.StateNotMeshed); return; }

            // Refused once: the whole sprite, nothing built, and no second ask until a switch is turned off
            // and on again or this component is enabled again (the registry has already said so once).
            if (_ashoreRefused) { Stop(Refusal.FacetIdRefused); return; }

            if (!EnsureAshoreFigure(skin, sprite)) { _refusalSkin = skin; Stop(Refusal.FigureRefused); return; }
            if (!skin.TryGetClip(stateKey, out CharacterSkinDef.SkinClip clip))
            {
                _refusalKey = stateKey;
                Stop(Refusal.ClipVanished);
                return;
            }

            // ⭐ THE ID, asked for once and then held: through shelter and suspension, until a switch goes
            // off or this component is disabled or destroyed. At exhaustion the registry logs its warning for
            // this ask, and she keeps her whole sprite.
            if (!_ashoreFigure.IsAshore)
            {
                bool granted;
                try
                {
                    granted = _ashoreFigure.EnterAshore(sprite);
                }
                catch (Exception e)
                {
                    // Logged for this ask and held as a refusal, so it is not thrown again every frame.
                    Debug.LogException(e, this);
                    granted = false;
                }
                if (!granted)
                {
                    _ashoreRefused = true;
                    ReleaseAshoreFigure();   // refused, she builds nothing: the figure made for the ask goes
                    Stop(Refusal.FacetIdRefused);
                    return;
                }
            }

            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0d;
            if (!string.Equals(stateKey, _ashoreStateKey, StringComparison.Ordinal))
            {
                _ashoreStateKey = stateKey;
                _ashoreStateStartSeconds = now;
            }

            // RULE 5, as aboard: a looping clip runs from absolute game time, a one-shot from its entry. Her
            // key moves the loop's phase off her neighbours' (aboard's phase is untouched).
            double clipStart = clip.Loop ? 0d : _ashoreStateStartSeconds;
            int seed = GameServices.Environment != null ? GameServices.Environment.WorldSeed : 0;
            int frame = CharacterSkinPose.FrameFor(clip, AshorePhaseSeed(seed, _ashoreKeyHash), now, clipStart);
            if (!_ashoreFigure.SetPose(stateKey, frame))
            {
                _refusalKey = stateKey;
                _refusalFrame = frame;
                Stop(Refusal.PoseRefused);
                return;
            }

            // Her facing: her sprite's own compass heading, through the def's MEASURED azimuth sign. The
            // ashore frame is a hull frame at heading 0, where a deck bearing and a compass heading are the
            // same number — the player's rule.
            float heading = character.HeadingDegrees;
            float yaw = skin.AzimuthCounterClockwise ? -heading : heading;
            _ashoreFigure.SetAshoreYaw(yaw);
            AshoreYawDegrees = yaw;

            // ⭐ THE SAME-FRAME SORT. YSortSprite wrote her sprite's order at execution order 0, and the
            // figure's own copy, also at 0, may have run before it. This is order 100, so the copy made here
            // is this frame's and the overlay ends the frame sorted exactly as her sprite.
            _ashoreFigure.WriteAshoreProperties();

            // ONE FIGURE, NOT TWO: the mesh shows and the sprite hides on the same call.
            _ashoreFigure.Visible = true;
            sprite.forceRenderingOff = true;
            _hidSprite = true;
            DrawsInsteadOfSprite = true;
            _refusal = Refusal.None;
        }

        /// <summary>
        /// Stand the figure down, give the sprite back, and remember why. Hides rather than destroys: a
        /// gate that shuts for a frame (a routine hiding someone, the switch flipped) must not cost a
        /// rebuilt mesh, material and two ramp textures when it opens again (rule 7). The build is only
        /// thrown away when the hull under it is gone. An ashore figure hides too, and keeps her id.
        /// </summary>
        private void Stop(Refusal why)
        {
            _refusal = why;
            RestoreSprite();
            if (_ashoreFigure != null) _ashoreFigure.Visible = false;
            StandDownAboard();
        }

        private void StandDownAboard()
        {
            if (_hull == null) { Release(); return; }   // Unity-null: the hull was destroyed, or never built
            if (_figure != null) _figure.Visible = false;
        }

        /// <summary>Aboard: an ashore figure hides and keeps her id while both switches are on; with either
        /// off it gives the id back and a refusal is forgotten. A no-op for a stand never drawn ashore.</summary>
        private void StandDownAshore(GameConfig config)
        {
            if (AshoreSwitchOn(config))
            {
                if (_ashoreFigure != null) _ashoreFigure.Visible = false;
                return;
            }
            ReleaseAshore();
        }

        private void RestoreSprite()
        {
            if (!_hidSprite) return;
            _hidSprite = false;
            if (_sprite != null) _sprite.forceRenderingOff = false;
        }

        // ---------------------------------------------------------------- resolution

        private static bool IsLive(ICharacterFigureStand stand) =>
            stand != null && !(stand is Object standObject && standObject == null);

        private SpriteRenderer ResolveSprite()
        {
            if (_sprite == null)
            {
                _hidSprite = false;   // whatever held the flag died with the renderer
                _sprite = GetComponent<SpriteRenderer>();
                if (_sprite != null && !_restCaptured) CaptureRestStaging(_sprite);
            }
            return _sprite;
        }

        private void CaptureRestStaging(SpriteRenderer sprite)
        {
            _restSortingLayerId = sprite.sortingLayerID;
            _restSortingOrder = sprite.sortingOrder;
            _restCaptured = true;
        }

        /// <summary>
        /// The facet hull on the stand's hull transform, looked up once per transform (and again if the
        /// renderer found there is destroyed). A sprite hull answers null — correctly: it records no facet
        /// block, so there is no pass to draw a mesh figure through.
        ///
        /// <para>⚠ A renderer installed LATER on the same transform is not seen. No stand does that today:
        /// <c>MooredBoat</c> skins her hull before it stands anyone on it, and never re-skins.</para>
        /// </summary>
        private IsoFacetHullRenderer ResolveHull(Transform visual)
        {
            if (visual == null)
            {
                _hullVisual = null;
                _hullCandidate = null;
                return null;
            }

            if (!ReferenceEquals(visual, _hullVisual) ||
                (!ReferenceEquals(_hullCandidate, null) && _hullCandidate == null))
            {
                _hullVisual = visual;
                _hullCandidate = visual.GetComponent<IsoFacetHullRenderer>();
            }

            IsoFacetHullRenderer hull = _hullCandidate;
            return hull != null && hull.PosedMesh != null ? hull : null;
        }

        private bool IsUsable(CharacterSkinDef skin)
        {
            if (!ReferenceEquals(skin, _usabilityOf))
            {
                _usabilityOf = skin;
                _usable = skin.IsUsable();
            }
            return _usable;
        }

        // ---------------------------------------------------------------- the figure

        private bool EnsureFigure(CharacterSkinDef skin, IsoFacetHullRenderer hull)
        {
            if (_figure != null && _hull == hull && _configured == skin) return true;
            if (ReferenceEquals(skin, _refusedSkin)) return false;   // said once, not rebuilt every frame

            Release();

            Transform posed = hull.PosedMesh;
            var go = new GameObject(FigureObjectName) { hideFlags = HideFlags.DontSave };
            go.layer = posed.gameObject.layer;   // the hull's layer, or the facet pass never sees the figure
            // Parent FIRST, configure second: the figure renderer finds its hull up the parent chain.
            go.transform.SetParent(posed, false);
            var figure = go.AddComponent<IsoCharacterFigureRenderer>();

            try
            {
                figure.Configure(skin);
            }
            catch (Exception e)
            {
                _refusedSkin = skin;
                Debug.LogError($"[CharacterFigurePresenter] '{name}': CharacterSkinDef '{skin.Id}' could not " +
                               $"build a figure, so this character keeps the sprite. {e.Message}");
                DestroySafely(go);
                return false;
            }

            if (!figure.IsConfigured)
            {
                _refusedSkin = skin;
                Debug.LogError($"[CharacterFigurePresenter] '{name}': CharacterSkinDef '{skin.Id}' built no " +
                               "posed mesh, so this character keeps the sprite.");
                DestroySafely(go);
                return false;
            }

            _figureRoot = go.transform;
            _figure = figure;
            _hull = hull;
            _configured = skin;
            _figure.Visible = false;
            _stateKey = null;
            return true;
        }

        /// <summary>
        /// Where the figure stands and which way it faces — both published by the stand, neither computed
        /// here. The posed mesh child's local space IS the hull's rig frame in metres, so the stand point
        /// the deck-occupant slot is fed from and the figure's feet are the same point by construction.
        /// The yaw goes through the def's own measured azimuth sign, as the player's does.
        /// </summary>
        private void Place(ICharacterFigureStand stand, CharacterSkinDef skin)
        {
            Vector3 local = stand.FigureStandRigMetres;
            float bearing = stand.FigureDeckBearingDegrees;
            float yaw = skin.AzimuthCounterClockwise ? -bearing : bearing;
            FigureLocalMetres = local;
            FigureYawDegrees = yaw;
            _figureRoot.SetLocalPositionAndRotation(local, Quaternion.AngleAxis(yaw, Vector3.forward));
        }

        private void Release()
        {
            if (_figureRoot != null) DestroySafely(_figureRoot.gameObject);
            _figureRoot = null;
            _figure = null;
            _hull = null;
            _configured = null;
            _stateKey = null;
            FellBackToGait = false;
        }

        // ---------------------------------------------------------------- ashore

        /// <summary>
        /// Her ashore figure, built once per skin: under her sprite at its pivot (her feet), on her sprite's
        /// layer, because its overlay quad is what sorts against sprites on the camera that draws hers. Never
        /// under a hull, so <see cref="IsoCharacterFigureRenderer.EnterAshore"/>'s parent refusal cannot trip
        /// for a villager.
        /// </summary>
        private bool EnsureAshoreFigure(CharacterSkinDef skin, SpriteRenderer sprite)
        {
            if (_ashoreFigure != null && _ashoreConfigured == skin) return true;
            if (ReferenceEquals(skin, _refusedSkin)) return false;   // said once, not rebuilt every frame

            ReleaseAshoreFigure();

            var go = new GameObject(AshoreFigureObjectName) { hideFlags = HideFlags.DontSave };
            go.layer = sprite.gameObject.layer;
            go.transform.SetParent(sprite.transform, false);
            var figure = go.AddComponent<IsoCharacterFigureRenderer>();

            try
            {
                figure.Configure(skin);
            }
            catch (Exception e)
            {
                _refusedSkin = skin;
                Debug.LogError($"[CharacterFigurePresenter] '{name}': CharacterSkinDef '{skin.Id}' could not " +
                               $"build a figure, so this character keeps the sprite. {e.Message}");
                DestroySafely(go);
                return false;
            }

            if (!figure.IsConfigured)
            {
                _refusedSkin = skin;
                Debug.LogError($"[CharacterFigurePresenter] '{name}': CharacterSkinDef '{skin.Id}' built no " +
                               "posed mesh, so this character keeps the sprite.");
                DestroySafely(go);
                return false;
            }

            figure.Visible = false;
            _ashoreFigure = figure;
            _ashoreConfigured = skin;
            _ashoreStateKey = null;
            return true;
        }

        /// <summary>Everything ashore undone: her figure gone, her id returned, and a refusal forgotten, so
        /// her next draw asks the pool again. The sprite is its caller's to give back.</summary>
        private void ReleaseAshore()
        {
            ReleaseAshoreFigure();
            _ashoreRefused = false;
        }

        private void ReleaseAshoreFigure()
        {
            if (_ashoreFigure != null)
            {
                // The id goes back NOW, not at the end of the frame when the deferred destroy lands.
                _ashoreFigure.LeaveAshore();
                DestroySafely(_ashoreFigure.gameObject);
            }
            _ashoreFigure = null;
            _ashoreConfigured = null;
            _ashoreStateKey = null;
        }

        /// <summary>Both switches: <see cref="GameConfig.MeshCastAshore"/> is live only with
        /// <see cref="GameConfig.MeshCast"/> on too (amendment 2026-09-27), as the player's ashore switch
        /// needs hers.</summary>
        public static bool AshoreSwitchOn(GameConfig config) =>
            config != null && config.MeshCast && config.MeshCastAshore;

        /// <summary>
        /// Her key as FNV-1a over its chars, hashed once in <see cref="Configure"/>. 0 for a null or empty
        /// key, which is the phase with no offset of her own (a key that hashes to 0 is the same, one in
        /// four billion). Pure: the same key gives the same hash in every process and on every platform.
        /// </summary>
        internal static uint KeyHash(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0u;
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < key.Length; i++) h = (h ^ key[i]) * 16777619u;
                return h;
            }
        }

        /// <summary>
        /// The seed her clip's phase is drawn from: the world seed mixed with her key's hash through a
        /// 32-bit finalizer (MurmurHash3's). The finalizer is the point. The phase is FNV-1a modulo a small
        /// frame count, often a power of two (a walk is 8), and FNV's low bits only ever see its input's
        /// low bits, so a plain XOR would lock two villagers in step on every seed if their keys agreed
        /// there. Pure in (worldSeed, key), so she is the same on every run (rule 5). No key: the world
        /// seed alone, the phase aboard.
        /// </summary>
        internal static int AshorePhaseSeed(int worldSeed, uint keyHash)
        {
            if (keyHash == 0u) return worldSeed;
            unchecked
            {
                uint h = (uint)worldSeed ^ keyHash;
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return (int)h;
            }
        }

        private static string SkinId(CharacterSkinDef skin) => skin != null ? skin.Id : "<null>";

        private static void DestroySafely(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
