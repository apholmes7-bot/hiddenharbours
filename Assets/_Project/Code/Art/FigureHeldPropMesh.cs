using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>A held prop as its facet mesh in a mesh figure's hand, aimed by the clip</b> (ADR 0053 §10): the
    /// driver a villager's figure uses for the tool her work clip holds. It draws the SAME baked mesh the
    /// player's tool draws (a <see cref="HeldPropMeshDef"/> asset), on the SAME socket
    /// (<see cref="HeldPropSockets"/>), by the SAME pose rule (<see cref="HeldPropPose"/>): the grip is the
    /// mount bone's point on the frame drawn and the aim is the clip's own tool key for that frame, turned
    /// from the aim the mesh was baked at. On a frame whose clip holds no tool, the nearest frame's aim is
    /// used; a clip that never aims a tool of the prop's family draws nothing.
    ///
    /// <para><b>Aimed only.</b> The player's driver also carries a tool on a free gait and slings the rod on
    /// his back; a villager holds her tool only while her work clip uses it, so neither is here. Anything
    /// missing is a <see cref="Refusal"/>, and nothing is drawn in its place.</para>
    ///
    /// <para>Allocation-free per frame once the renderer is built (rule 7): the def, skin and clip lookups
    /// run once per reference change, and the build once per held mesh.</para>
    /// </summary>
    public sealed class FigureHeldPropMesh
    {
        /// <summary>Why the prop did not draw this frame.</summary>
        public enum Refusal
        {
            None,
            SwitchOff,
            NoProp,
            NoUsableMesh,
            NoBakedAim,
            NoMeshFigure,
            NoMountBone,
            ClipAimsNoTool,
            BuildFailed,
        }

        private readonly Transform _home;
        private readonly string _name;
        private readonly int _reachClaimant;
        private IsoFacetHeldPropRenderer _renderer;
        private IsoCharacterFigureRenderer _reachFigure;

        // Resolved once per def / skin / clip, never per frame.
        private HeldPropMeshDef _def;
        private bool _defDrawable, _defAimKnown;
        private float _bakedPitch, _bakedYaw;
        private CharacterSkinDef _skin;
        private string _mountBoneId;
        private int _mountBone = CharacterSkinDef.NoBone;
        private string _clipKey;
        private CharacterSkinDef.ToolKey[] _clipTrack;

        /// <param name="home">Where the renderer's object is made and given back to when hidden.</param>
        /// <param name="name">The renderer object's name.</param>
        /// <param name="reachClaimant">The figure's overlay claim the prop pads
        /// (<see cref="IsoCharacterFigureRenderer.HeldReachHandTool"/> for a hand tool).</param>
        public FigureHeldPropMesh(Transform home, string name, int reachClaimant)
        {
            _home = home;
            _name = string.IsNullOrEmpty(name) ? "FigureHeldPropMesh" : name;
            _reachClaimant = reachClaimant;
        }

        /// <summary>The reason of the last <see cref="TryDraw"/>.</summary>
        public Refusal LastRefusal { get; private set; } = Refusal.NoProp;

        /// <summary>True on a frame the prop is drawn.</summary>
        public bool Drawing { get; private set; }

        /// <summary>The renderer, once built. For tests and plates.</summary>
        public IsoFacetHeldPropRenderer Renderer => _renderer;

        /// <summary>The held mesh drawn (or last resolved), or null.</summary>
        public HeldPropMeshDef Def => _def;

        /// <summary>The prop's tip in world space on the frame drawn (a rod's tip, where a line leaves it), as
        /// the player's <c>HeldRodMesh.TipWorld</c> reads it; zero when nothing is drawn.</summary>
        public Vector2 TipWorld => Drawing && _renderer != null ? (Vector2)_renderer.TipWorld : Vector2.zero;

        /// <summary>
        /// True when <paramref name="def"/> can be drawn on <paramref name="skin"/> at all: it is usable,
        /// plots a tip, carries its baked aim, and the skin has <paramref name="mountBone"/>. A presenter
        /// asks this before it starts a work clip, so a figure never works a tool it cannot hold.
        /// </summary>
        public bool CanHold(HeldPropMeshDef def, CharacterSkinDef skin, string mountBone)
        {
            if (def == null || skin == null) return false;
            ResolveDef(def);
            if (!_defDrawable || !_defAimKnown) return false;
            ResolveSkin(skin, mountBone);
            return _mountBone != CharacterSkinDef.NoBone;
        }

        /// <summary>
        /// Draw <paramref name="def"/> on <paramref name="figure"/>'s <paramref name="mountBone"/> this frame if
        /// everything it needs is there; otherwise hide it and say why. <paramref name="switchOn"/> is
        /// <see cref="GameConfig.MeshHandToolInHand"/>; <paramref name="lineWidthPx"/> is
        /// <see cref="GameConfig.HeldPropLineWidthPx"/>.
        /// </summary>
        public bool TryDraw(bool switchOn, HeldPropMeshDef def, string mountBone, IsoCharacterFigureRenderer figure,
                            float lineWidthPx)
        {
            Refusal r = Resolve(switchOn, def, mountBone, figure, out Vector3 grip, out Quaternion aim);
            if (r == Refusal.None && !EnsureRenderer(figure, lineWidthPx)) r = Refusal.BuildFailed;
            if (r == Refusal.None && !_renderer.Mount(figure, grip, aim)) r = Refusal.NoMeshFigure;
            LastRefusal = r;
            Drawing = r == Refusal.None;
            if (Drawing) KeepReach(figure);
            else Hide();
            return Drawing;
        }

        /// <summary>
        /// The decision and the pose, without touching a renderer: what <see cref="TryDraw"/> would do this
        /// frame. Public so the rule is testable on its own.
        /// </summary>
        public Refusal Resolve(bool switchOn, HeldPropMeshDef def, string mountBone, IsoCharacterFigureRenderer figure,
                               out Vector3 gripLocal, out Quaternion aimLocal)
        {
            gripLocal = Vector3.zero;
            aimLocal = Quaternion.identity;
            if (!switchOn) return Refusal.SwitchOff;
            if (def == null) return Refusal.NoProp;

            ResolveDef(def);
            if (!_defDrawable) return Refusal.NoUsableMesh;
            if (!_defAimKnown) return Refusal.NoBakedAim;

            if (figure == null || !figure.IsConfigured || !figure.Visible || figure.SkinDef == null
                || string.IsNullOrEmpty(figure.DrawnStateKey))
                return Refusal.NoMeshFigure;

            ResolveSkin(figure.SkinDef, mountBone);
            if (_mountBone == CharacterSkinDef.NoBone) return Refusal.NoMountBone;

            ResolveClip(figure.DrawnStateKey);
            if (!HeldPropPose.TryNearestHeldKey(_clipTrack, figure.DrawnFrame, _def.Family,
                                                out CharacterSkinDef.ToolKey key))
                return Refusal.ClipAimsNoTool;

            if (!figure.TryBonePoint(_mountBone, out gripLocal)) return Refusal.NoMeshFigure;
            aimLocal = HeldPropPose.FromBakedPose(_bakedPitch, _bakedYaw, key.Pitch, key.Yaw);
            return Refusal.None;
        }

        /// <summary>Stop drawing it.</summary>
        public void Hide()
        {
            Drawing = false;
            if (_renderer != null) _renderer.Hide();
            KeepReach(null);
        }

        /// <summary>Destroy what this made.</summary>
        public void Dispose()
        {
            Hide();
            if (_renderer != null)
            {
                if (Application.isPlaying) Object.Destroy(_renderer.gameObject);
                else Object.DestroyImmediate(_renderer.gameObject);
            }
            _renderer = null;
        }

        private void ResolveDef(HeldPropMeshDef def)
        {
            if (ReferenceEquals(def, _def)) return;
            _def = def;
            _defDrawable = def.IsUsable() && HeldPropPose.TryTipLocal(def, out _);
            _defAimKnown = HeldPropPose.TryParseSourcePose(def.SourcePose, out _bakedPitch, out _bakedYaw);
        }

        private void ResolveSkin(CharacterSkinDef skin, string boneId)
        {
            if (ReferenceEquals(skin, _skin) && string.Equals(boneId, _mountBoneId, System.StringComparison.Ordinal))
                return;
            _skin = skin;
            _mountBoneId = boneId;
            _mountBone = string.IsNullOrEmpty(boneId) ? CharacterSkinDef.NoBone : skin.IndexOfBone(boneId);
            _clipKey = null;
            _clipTrack = null;
        }

        private void ResolveClip(string key)
        {
            // A clip key is the figure's own string reference while it holds a clip, so the lookup runs
            // once per clip change, not per frame.
            if (ReferenceEquals(key, _clipKey)) return;
            _clipKey = key;
            _clipTrack = _skin != null && _skin.TryGetClip(key, out CharacterSkinDef.SkinClip clip) ? clip.Tool : null;
        }

        private bool EnsureRenderer(IsoCharacterFigureRenderer figure, float lineWidthPx)
        {
            if (_renderer != null && ReferenceEquals(_renderer.Def, _def) && _renderer.IsConfigured) return true;
            if (_renderer == null)
            {
                var go = new GameObject(_name) { hideFlags = HideFlags.DontSave };
                if (_home != null)
                {
                    go.transform.SetParent(_home, false);
                    go.layer = _home.gameObject.layer;
                }
                _renderer = go.AddComponent<IsoFacetHeldPropRenderer>();
            }
            CharacterSkinDef skin = figure.SkinDef;
            if (_renderer.Configure(_def, lineWidthPx, skin.PivotPx, skin.PxPerMetre, out string why)) return true;
            Debug.LogWarning($"[{_name}] {why}: '{_def.Id}' is not drawn.");
            return false;
        }

        /// <summary>Ashore the figure's overlay is the window the facet pass composites through, so it is
        /// padded by the prop's length while the prop is drawn, and given back when it is not.</summary>
        private void KeepReach(IsoCharacterFigureRenderer figure)
        {
            if (!ReferenceEquals(figure, _reachFigure) && _reachFigure != null)
                _reachFigure.SetHeldReachMetres(_reachClaimant, 0f);
            _reachFigure = figure;
            if (figure != null && _renderer != null)
                figure.SetHeldReachMetres(_reachClaimant, _renderer.TipLocal.magnitude);
        }
    }
}
