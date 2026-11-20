using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The rod as its baked facet mesh in the mesh figure's hand</b> (ADR 0053): the one place that
    /// decides, frame by frame, whether the rod can be drawn as a mesh, and poses it when it can. The
    /// fight presenter asks it first and draws its sprite rod only when this says no.
    ///
    /// <para><b>When it draws.</b> All of: the tool's Def asks for it
    /// (<see cref="ToolDef.DrawHeldMeshOnFigure"/>) and names a held mesh that is usable and plots a tip;
    /// a mesh figure is drawing him this frame; the clip it draws aims a tool of the mesh's family on
    /// some frame (the fisher's fishing clips aim the rod on every frame), or the Def asks for the
    /// carried placement (<see cref="ToolDef.DrawCarriedMeshOnFigure"/>: the rig's own held pose on the
    /// socket, D1, for idle, walk and a moving angler's gait); and the figure has the mount bone the
    /// Def names. Anything else is a refusal with a reason (<see cref="Refusal"/>), and the
    /// sprite rod draws exactly as before. Nothing here invents a pose or bakes a mesh.</para>
    ///
    /// <para><b>The pose is the rig's.</b> The grip is the mount bone's point on the frame drawn, and the
    /// aim is the clip's own tool key for that frame — the pitch and yaw the rig aimed the rod with —
    /// turned from the pitch and yaw the mesh was baked at (<see cref="HeldPropPose.FromBakedPose"/>). On
    /// a frame whose clip holds no tool, the nearest frame's aim is used. The bend the rig plots on the
    /// blank under load is not drawn (the mesh is baked straight); the fight's lean still is.</para>
    ///
    /// <para>Allocation-free per frame once the renderer is built (rule 7); the build runs once per held
    /// mesh, and again only if the figure it hung under was torn down with it.</para>
    /// </summary>
    public sealed class HeldRodMesh
    {
        /// <summary>Why the rod mesh did not draw this frame.</summary>
        public enum Refusal
        {
            None,
            SwitchOff,
            NoTool,
            ToolKeepsSprite,
            NoUsableMesh,
            NoBakedAim,
            NoMeshFigure,
            ClipAimsNoTool,
            NoMountBone,
            BuildFailed,
        }

        private readonly Transform _home;
        private IsoFacetHeldPropRenderer _renderer;

        // Resolved once per Def / skin / clip, never per frame.
        private ToolDef _tool;
        private HeldPropMeshDef _def;
        private bool _defAimKnown, _defDrawable;
        private float _bakedPitch, _bakedYaw;
        private CharacterSkinDef _skin;
        private string _mountBoneId;
        private int _mountBone = CharacterSkinDef.NoBone;
        private string _clipKey;
        private CharacterSkinDef.ToolKey[] _clipTrack;
        private IsoCharacterFigureRenderer _reachFigure;

        /// <param name="home">Where the renderer's object is made and given back to when hidden.</param>
        public HeldRodMesh(Transform home) => _home = home;

        /// <summary>The reason of the last <see cref="TryDraw"/>.</summary>
        public Refusal LastRefusal { get; private set; } = Refusal.NoTool;

        /// <summary>True on a frame the mesh rod is drawn.</summary>
        public bool Drawing { get; private set; }

        /// <summary>True when the last resolve placed the prop at its rig's held pose (D1) because the
        /// clip drawn aims no tool, rather than at the clip's own tool key.</summary>
        public bool Carried { get; private set; }

        /// <summary>The renderer, once built. For tests and plates.</summary>
        public IsoFacetHeldPropRenderer Renderer => _renderer;

        /// <summary>The held mesh drawn (or last resolved), or null.</summary>
        public HeldPropMeshDef Def => _def;

        /// <summary>The grip in world space this frame.</summary>
        public Vector2 GripWorld => _renderer != null ? (Vector2)_renderer.GripWorld : Vector2.zero;

        /// <summary>The tip in world space this frame, lean included.</summary>
        public Vector2 TipWorld => _renderer != null ? (Vector2)_renderer.TipWorld : Vector2.zero;

        /// <summary>
        /// Draw the rod on <paramref name="figure"/> this frame if everything it needs is there; otherwise
        /// hide it and say why. <paramref name="switchOn"/> is <see cref="GameConfig.MeshRodInHand"/>.
        /// </summary>
        public bool TryDraw(bool switchOn, ToolDef tool, IsoCharacterFigureRenderer figure, float lineWidthPx)
        {
            Refusal r = Resolve(switchOn, tool, figure, out Vector3 grip, out Quaternion aim);
            if (r == Refusal.None && !EnsureRenderer(figure, lineWidthPx)) r = Refusal.BuildFailed;
            if (r == Refusal.None && !_renderer.Mount(figure, grip, aim)) r = Refusal.NoMeshFigure;
            LastRefusal = r;
            Drawing = r == Refusal.None;
            if (Drawing) KeepReach(figure);
            else Hide();
            return Drawing;
        }

        /// <summary>Lean the drawn rod in the picture about its grip (the fight's load lean).</summary>
        public void Lean(float degrees)
        {
            if (Drawing && _renderer != null) _renderer.LeanInPicture(degrees);
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

        /// <summary>
        /// The decision and the pose, without touching a renderer: what <see cref="TryDraw"/> would do
        /// this frame. Public so the rule is testable on its own.
        /// </summary>
        public Refusal Resolve(bool switchOn, ToolDef tool, IsoCharacterFigureRenderer figure,
                               out Vector3 gripLocal, out Quaternion aimLocal)
        {
            gripLocal = Vector3.zero;
            aimLocal = Quaternion.identity;
            Carried = false;
            if (!switchOn) return Refusal.SwitchOff;
            if (tool == null) return Refusal.NoTool;
            if (!tool.DrawHeldMeshOnFigure) return Refusal.ToolKeepsSprite;

            ResolveDef(tool);
            if (_def == null || !_defDrawable) return Refusal.NoUsableMesh;
            if (!_defAimKnown) return Refusal.NoBakedAim;

            if (figure == null || !figure.IsConfigured || !figure.Visible || figure.SkinDef == null
                || string.IsNullOrEmpty(figure.DrawnStateKey))
                return Refusal.NoMeshFigure;

            ResolveSkin(figure.SkinDef, tool.HeldMeshMountBone);
            if (_mountBone == CharacterSkinDef.NoBone) return Refusal.NoMountBone;

            ResolveClip(figure.DrawnStateKey);
            bool aimed = HeldPropPose.TryNearestHeldKey(_clipTrack, figure.DrawnFrame, _def.Family,
                                                        out CharacterSkinDef.ToolKey key);
            // D1: a clip that aims no tool (idle, walk, a moving angler's gait) holds the prop at its
            // rig's own held pose on the socket, when the Def asks for it; otherwise the sprite keeps it.
            if (!aimed && !tool.DrawCarriedMeshOnFigure) return Refusal.ClipAimsNoTool;

            if (!figure.TryBonePoint(_mountBone, out gripLocal)) return Refusal.NoMeshFigure;
            Carried = !aimed;
            aimLocal = aimed ? HeldPropPose.FromBakedPose(_bakedPitch, _bakedYaw, key.Pitch, key.Yaw)
                             : Quaternion.identity;
            return Refusal.None;
        }

        private void ResolveDef(ToolDef tool)
        {
            if (ReferenceEquals(tool, _tool) && _def != null) return;
            _tool = tool;
            _def = tool.HeldMeshFor(tool.HeldMeshVariant);
            _defDrawable = _def != null && _def.IsUsable() && HeldPropPose.TryTipLocal(_def, out _);
            _defAimKnown = _def != null && HeldPropPose.TryParseSourcePose(_def.SourcePose, out _bakedPitch, out _bakedYaw);
        }

        private void ResolveSkin(CharacterSkinDef skin, string boneId)
        {
            if (ReferenceEquals(skin, _skin) && string.Equals(boneId, _mountBoneId, System.StringComparison.Ordinal))
                return;
            _skin = skin;
            _mountBoneId = boneId;
            _mountBone = skin.IndexOfBone(boneId);
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
                var go = new GameObject("HeldRodMesh") { hideFlags = HideFlags.DontSave };
                if (_home != null)
                {
                    go.transform.SetParent(_home, false);
                    go.layer = _home.gameObject.layer;
                }
                _renderer = go.AddComponent<IsoFacetHeldPropRenderer>();
            }
            CharacterSkinDef skin = figure.SkinDef;
            if (_renderer.Configure(_def, lineWidthPx, skin.PivotPx, skin.PxPerMetre, out string why)) return true;
            Debug.LogWarning($"[HeldRodMesh] {why}: the rod stays a sprite.");
            return false;
        }

        /// <summary>Ashore the figure's overlay is the window the facet pass composites through, so it is
        /// padded by the rod's length while the rod is drawn, and given back when it is not.</summary>
        private void KeepReach(IsoCharacterFigureRenderer figure)
        {
            if (!ReferenceEquals(figure, _reachFigure) && _reachFigure != null) _reachFigure.SetHeldReachMetres(0f);
            _reachFigure = figure;
            if (figure != null && _renderer != null) figure.SetHeldReachMetres(_renderer.TipLocal.magnitude);
        }
    }
}
