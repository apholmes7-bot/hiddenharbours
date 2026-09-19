using System;
using HiddenHarbours.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>A member of the CAST, drawn as one skinned mesh through the iso facet pass while they stand on a
    /// facet hull</b> (ADR 0044, amendment 2026-09-17, behind <see cref="GameConfig.MeshCast"/>). The
    /// cast's twin of the player's <c>DeckRiderMeshPresenter</c>, on the far side of the Core seam: Boats
    /// puts it on a skipper through <see cref="CharacterFigurePresentation"/> and never learns this type
    /// exists (rule 4).
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
    /// (<see cref="WhyNot"/>, <see cref="NotDrawingReason"/>): no stand, ashore (the facet pass is only
    /// recorded while a mesh hull is registered, so there is nothing to draw a figure through), a sprite
    /// that is disabled or hidden by somebody else, a sprite re-sorted off the hull's picture, the switch
    /// off, a suspended character, no skin or an unusable one, a sprite hull, no clip, a state not in
    /// <see cref="CharacterSkinDef.MeshStates"/> (ADR 0041), a def that refuses to build.</para>
    ///
    /// <para><b>⚠ The re-sorted sprite, and why it is a gate.</b> The mesh can only ever be seen INSIDE
    /// its hull's picture — each hull's overlay quad re-composes the facet pass at the hull's own sorting
    /// order. A sprite whose owner has re-sorted it is being staged somewhere that picture cannot reach:
    /// the St Peters arrival raises its skipper over the cabin room while the player is below decks. So
    /// the sorting the sprite had when the figure was attached is remembered, and while it differs the
    /// sprite keeps the draw. That is a READ of somebody else's decision, not a second opinion about it.</para>
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

        private Transform _hullVisual;                 // the stand's hull transform last looked up
        private IsoFacetHullRenderer _hullCandidate;   // ... and what was on it
        private CharacterSkinDef _usabilityOf;
        private bool _usable;
        private CharacterSkinDef _refusedSkin;         // a def that threw building a figure: said once

        private Refusal _refusal = Refusal.NotPosedYet;
        private string _refusalKey;
        private CharacterStance _refusalStance;
        private CharacterGait _refusalGait;
        private int _refusalFrame;
        private int _refusalLayer, _refusalOrder;
        private CharacterSkinDef _refusalSkin;

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

        /// <summary>The clip key drawn this frame, or null.</summary>
        public string DrawnStateKey => _figure != null ? _figure.DrawnStateKey : null;

        /// <summary>The clip frame drawn this frame (after the poisoned-frame fence), or -1.</summary>
        public int DrawnFrame => _figure != null ? _figure.DrawnFrame : -1;

        /// <summary>The frame ASKED for before the fence, or -1.</summary>
        public int RequestedFrame => _figure != null ? _figure.RequestedFrame : -1;

        /// <summary>True when the clip the state map asked for was not baked and a shorter one stood in.</summary>
        public bool FellBackToGait { get; private set; }

        /// <summary>Where the figure was placed, in the hull's posed-mesh local metres (the rig frame).</summary>
        public Vector3 FigureLocalMetres { get; private set; }

        /// <summary>The yaw applied about the rig's up axis, in degrees.</summary>
        public float FigureYawDegrees { get; private set; }

        /// <summary>Which gate shut this frame; <see cref="Refusal.None"/> while the mesh draws.</summary>
        public Refusal WhyNot => _refusal;

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
                        return "no hull under them — ashore, or a hull that holds no deck slot for them — so no " +
                               "facet pass to draw a figure through";
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
                    case Refusal.SwitchOff: return "GameConfig.MeshCast is off";
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
                    default: return _refusal.ToString();
                }
            }
        }

        /// <summary>
        /// Pose this figure from <paramref name="stand"/> every frame from now on. Remembers the sprite's
        /// sorting AS IT IS NOW, which is the staging the figure can stand in for — so call it after the
        /// stand has finished placing the sprite (<c>MooredBoat</c> attaches last).
        /// </summary>
        public void Configure(ICharacterFigureStand stand)
        {
            RestoreSprite();
            Release();
            _stand = stand;
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
        }

        private void OnDestroy()
        {
            RestoreSprite();
            Release();
        }

        // ---------------------------------------------------------------- the one path

        /// <inheritdoc/>
        /// <remarks>Called from this component's own <c>LateUpdate</c> (execution order 100, after the
        /// sprite, the stand and anything that holds them have run this frame), and directly by tests.</remarks>
        public void PoseFigure(ICharacterFigureStand stand, bool aboard)
        {
            DrawsInsteadOfSprite = false;

            if (!IsLive(stand)) { Stop(Refusal.NoStand); return; }
            if (!aboard) { Stop(Refusal.Ashore); return; }

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
            if (!config.MeshCast) { Stop(Refusal.SwitchOff); return; }

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

            if (!_figure.SetPose(stateKey, frame))
            {
                _refusalKey = stateKey;
                _refusalFrame = frame;
                Stop(Refusal.PoseRefused);
                return;
            }

            Place(stand, skin);
            _figure.Visible = true;
            sprite.forceRenderingOff = true;
            _hidSprite = true;
            DrawsInsteadOfSprite = true;
            _refusal = Refusal.None;
        }

        /// <summary>
        /// Stand the figure down, give the sprite back, and remember why. Hides rather than destroys: a
        /// gate that shuts for a frame (a routine hiding someone, the switch flipped) must not cost a
        /// rebuilt mesh, material and two ramp textures when it opens again (rule 7). The build is only
        /// thrown away when the hull under it is gone.
        /// </summary>
        private void Stop(Refusal why)
        {
            _refusal = why;
            RestoreSprite();
            if (_hull == null) { Release(); return; }   // Unity-null: the hull was destroyed, or never built
            if (_figure != null) _figure.Visible = false;
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

        private static string SkinId(CharacterSkinDef skin) => skin != null ? skin.Id : "<null>";

        private static void DestroySafely(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
