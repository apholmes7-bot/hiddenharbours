using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>A fish in the mesh figure's hand, as its baked facet mesh</b> (ADR 0053, PR 4 for the catch):
    /// the one place that decides, frame by frame, whether a held fish can be drawn as a mesh, and hangs
    /// it from his hand when it can. <see cref="CarryHands"/> asks it for a carried catch, and the fight
    /// presenter for the fish he holds up at the landing; each draws its sprite only when this says no.
    ///
    /// <para><b>When it draws.</b> All of: the switch is on (<see cref="GameConfig.MeshCatchInHand"/>);
    /// the catch's species has a held mesh at its size rung
    /// (<see cref="CatchItemLibrary.HeldMeshFor(string, string, out float)"/>; a species that borrows its
    /// kind's art, or has none, keeps its sprite); a mesh figure is drawing him this frame; and the figure
    /// has the hand's socket. Anything else is a refusal with a reason (<see cref="Refusal"/>).</para>
    ///
    /// <para><b>The pose is the rig's.</b> The fish was baked hanging by its tail from the grip at the
    /// rig's tail rest, so it is mounted on the hand's socket at the identity (D1: the rig's held pose,
    /// relative to the body). It follows the hand because the socket is read off the frame the figure
    /// draws: carry, walk and the landing alike. A flatfish turns its broad side to the camera by the
    /// held sheet's own face-on map (<see cref="CatchItemLibrary.KindEntry.HeldFacingMap"/>), the
    /// measured answer the sprite already uses.</para>
    ///
    /// <para>Allocation-free per frame once built (rule 7). The build runs when the fish or its size
    /// changes (a catch put in his hand), never per frame.</para>
    /// </summary>
    public sealed class HeldCatchMesh
    {
        /// <summary>Why the fish mesh did not draw this frame.</summary>
        public enum Refusal
        {
            None,
            SwitchOff,
            NoCatch,
            NoMesh,
            NoMeshFigure,
            NoMountBone,
            BuildFailed,
        }

        /// <summary>The fish ramp whose open edges get the thin fin line.</summary>
        public const string FinRampName = "fin";

        private readonly Transform _home;
        private readonly string _name;
        private IsoFacetHeldPropRenderer _renderer;

        private CharacterSkinDef _skin;
        private string _mountBoneId;
        private int _mountBone = CharacterSkinDef.NoBone;
        private IsoCharacterFigureRenderer _reachFigure;
        private float _builtFinPx = -1f, _builtEyePx = -1f;
        private Vector3 _builtTone = new Vector3(-1f, -1f, -1f);
        private float _builtFront = -1f;

        /// <param name="home">Where the renderer's object is made and given back to when hidden.</param>
        /// <param name="name">The renderer object's name (for a plate or a hierarchy read).</param>
        public HeldCatchMesh(Transform home, string name = "HeldCatchMesh")
        {
            _home = home;
            _name = name;
        }

        /// <summary>The reason of the last <see cref="TryDraw"/>.</summary>
        public Refusal LastRefusal { get; private set; } = Refusal.NoCatch;

        /// <summary>True on a frame the mesh fish is drawn.</summary>
        public bool Drawing { get; private set; }

        /// <summary>The held mesh drawn (or last built), or null.</summary>
        public HeldPropMeshDef Def => _renderer != null ? _renderer.Def : null;

        /// <summary>The scale the drawn mesh was built at.</summary>
        public float Scale => _renderer != null ? _renderer.BuiltScale : 0f;

        /// <summary>The socket it hangs from this frame.</summary>
        public string MountBoneId => _mountBoneId;

        /// <summary>The renderer, once built. For tests and plates.</summary>
        public IsoFacetHeldPropRenderer Renderer => _renderer;

        /// <summary>The grip in world space this frame.</summary>
        public Vector2 GripWorld => _renderer != null ? (Vector2)_renderer.GripWorld : Vector2.zero;

        /// <summary>
        /// Draw <paramref name="def"/> at <paramref name="scale"/> from <paramref name="socket"/> on
        /// <paramref name="figure"/> this frame if everything it needs is there; otherwise hide it and say
        /// why. <paramref name="turnDegrees"/> turns it about its own length, in the figure's frame
        /// (<see cref="TurnToFacingDegrees"/>); 0 holds the rig's own pose on the socket.
        /// </summary>
        public bool TryDraw(GameConfig config, HeldPropMeshDef def, float scale, string socket, float turnDegrees,
                            IsoCharacterFigureRenderer figure)
        {
            Refusal r = Resolve(config, def, socket, figure, out Vector3 grip);
            if (r == Refusal.None && !EnsureRenderer(config, def, scale, figure)) r = Refusal.BuildFailed;
            if (r == Refusal.None)
            {
                Quaternion turn = Mathf.Abs(turnDegrees) < 1e-3f
                    ? Quaternion.identity
                    : Quaternion.AngleAxis(turnDegrees, Vector3.forward);
                if (!_renderer.Mount(figure, grip, turn)) r = Refusal.NoMeshFigure;
            }
            LastRefusal = r;
            Drawing = r == Refusal.None;
            if (Drawing) KeepReach(figure);
            else Hide();
            return Drawing;
        }

        /// <summary>One facing's turn, degrees (the eight baked facings).</summary>
        public const float StepDegrees = 45f;

        /// <summary>
        /// <b>The turn that shows the fish the way its held sprite shows it.</b> The carrier picks the held
        /// sheet's facing from the rig's own anchor row (<c>CarryAnchorRow.ItemFacing</c>), a facing in the
        /// CAMERA's terms, while the mesh hangs in the FIGURE's frame, which turns with his heading. So the
        /// mesh is turned about its length by the facing's compass bearing less the heading the figure is
        /// drawn at (through the same dial the figure is), in the held mesh's own azimuth sense
        /// (<see cref="HeldPropMeshDef.AzimuthCounterClockwise"/>). A flatfish's face-on map is applied
        /// to the facing first, so it shows its broad side as the sprite does. Pure; no allocation.
        /// </summary>
        public static float TurnToFacingDegrees(HeldPropMeshDef def, int[] faceOnMap, int facing, float figureHeadingDeg)
        {
            if (def == null) return 0f;
            int f = facing;
            if (faceOnMap != null && faceOnMap.Length > 0)
            {
                int n = faceOnMap.Length, d = ((facing % n) + n) % n;
                if (faceOnMap[d] >= 0 && faceOnMap[d] < n) f = faceOnMap[d];
            }
            float drawn = FigureHeadingDial.Heading(figureHeadingDeg, FigureHeadingDial.LiveSteps);
            float compass = Mathf.DeltaAngle(drawn, f * StepDegrees);
            return def.AzimuthCounterClockwise ? -compass : compass;
        }

        /// <summary>
        /// <b>The scale a fish mesh is drawn at so it reads</b>: <paramref name="scale"/> (its size rung), raised
        /// so the mesh's longest side is at least <paramref name="minLengthPx"/> art pixels
        /// (<see cref="GameConfig.HeldFishMinLengthPx"/>). The mesh is linear in its scale about the grip, so
        /// this is the same fish, only bigger. Pure; 0 or a def with no mesh leaves the scale alone. Only the
        /// fish family is raised: a crustacean or a handful of shellfish (<see cref="HeldPropMeshDef.ShellfishFamily"/>)
        /// is baked at the size the game's own hand sheet draws it, and the fish floor would blow a crab up
        /// past his chest.
        /// </summary>
        public static float ReadableScale(HeldPropMeshDef def, float scale, float minLengthPx)
        {
            scale = scale > 0f ? scale : 1f;
            if (def == null || def.Mesh == null || minLengthPx <= 0f || def.PxPerMetre <= 0) return scale;
            if (def.Family != HeldPropMeshDef.FishFamily) return scale;
            Vector3 size = def.Mesh.bounds.size;
            float lengthPx = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * def.PxPerMetre * scale;
            return lengthPx > 0f && lengthPx < minLengthPx ? scale * (minLengthPx / lengthPx) : scale;
        }

        /// <summary>
        /// <b>Which hand a held fish hangs from so he does not hide it</b>: the one nearer the camera (the
        /// smaller depth), changing from <paramref name="current"/> only when the other is nearer by more
        /// than <paramref name="swapDepth"/>, so it never flickers between two near-equal hands. The rig's own
        /// fish prop swaps hands by facing the same way. Pure.
        /// </summary>
        public static string NearerSocket(string current, string other, float currentDepth, float otherDepth,
                                          float swapDepth)
            => otherDepth < currentDepth - Mathf.Max(0f, swapDepth) ? other : current;

        /// <summary>
        /// The depth (world z; smaller is nearer the camera) of <paramref name="socket"/> on the frame the
        /// figure last drew. False when the figure or the socket is not there.
        /// </summary>
        public static bool TrySocketDepth(IsoCharacterFigureRenderer figure, string socket, out float depth)
        {
            depth = 0f;
            if (figure == null || figure.SkinDef == null || figure.FacetTransform == null) return false;
            int bone = figure.SkinDef.IndexOfBone(socket);
            if (bone == CharacterSkinDef.NoBone || !figure.TryBonePoint(bone, out Vector3 p)) return false;
            depth = figure.FacetTransform.TransformPoint(p).z;
            return true;
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

        /// <summary>The decision and the grip, without touching a renderer. Public so the rule is
        /// testable on its own.</summary>
        public Refusal Resolve(GameConfig config, HeldPropMeshDef def, string socket,
                               IsoCharacterFigureRenderer figure, out Vector3 gripLocal)
        {
            gripLocal = Vector3.zero;
            if (config == null || !config.MeshCatchInHand) return Refusal.SwitchOff;
            if (def == null || !def.IsUsable()) return Refusal.NoMesh;
            if (figure == null || !figure.IsConfigured || !figure.Visible || figure.SkinDef == null
                || string.IsNullOrEmpty(figure.DrawnStateKey))
                return Refusal.NoMeshFigure;
            ResolveSkin(figure.SkinDef, socket);
            if (_mountBone == CharacterSkinDef.NoBone) return Refusal.NoMountBone;
            return figure.TryBonePoint(_mountBone, out gripLocal) ? Refusal.None : Refusal.NoMeshFigure;
        }

        private void ResolveSkin(CharacterSkinDef skin, string boneId)
        {
            if (ReferenceEquals(skin, _skin) && string.Equals(boneId, _mountBoneId, System.StringComparison.Ordinal))
                return;
            _skin = skin;
            _mountBoneId = boneId;
            _mountBone = string.IsNullOrEmpty(boneId) ? CharacterSkinDef.NoBone : skin.IndexOfBone(boneId);
        }

        private bool EnsureRenderer(GameConfig config, HeldPropMeshDef def, float scale, IsoCharacterFigureRenderer figure)
        {
            scale = scale > 0f ? scale : 1f;
            float finPx = Mathf.Max(0f, config.HeldFishFinLineWidthPx);
            float eyePx = Mathf.Max(0f, config.HeldFishEyeSizePx);
            var tone = new Vector3(config.HeldFishToneGain, config.HeldFishToneLift, config.HeldFishDitherScale);
            float front = Mathf.Max(0f, config.HeldFishFrontBiasMeters);
            if (_renderer != null && _renderer.IsConfigured && ReferenceEquals(_renderer.Def, def)
                && Mathf.Approximately(_renderer.BuiltScale, scale)
                && Mathf.Approximately(finPx, _builtFinPx) && Mathf.Approximately(eyePx, _builtEyePx)
                && tone == _builtTone && Mathf.Approximately(front, _builtFront))
                return true;
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
            // Widths are art pixels in the def's own frame, so they stay a pixel at every size rung.
            float m = def.PxPerMetre > 0 ? 1f / def.PxPerMetre : 0f;
            var options = new IsoFacetHeldPropMesh.Options
            {
                Scale = scale,
                LineWidthMeters = config.HeldPropLineWidthPx * m,
                DotSizeMeters = eyePx * m,
                EdgeLineWidthMeters = finPx * m,
                EdgeRampName = FinRampName,
                // The held sheet's tone: its contrast, brightness and dither, as data (GameConfig).
                Tone = true,
                ToneGain = tone.x,
                ToneLift = tone.y,
                DitherScale = tone.z,
                // In front of the body that would hide it, as the sheet always is (GameConfig).
                DepthBiasMeters = front,
            };
            CharacterSkinDef skin = figure.SkinDef;
            _builtFinPx = finPx;
            _builtEyePx = eyePx;
            _builtTone = tone;
            _builtFront = front;
            if (_renderer.Configure(def, options, skin.PivotPx, skin.PxPerMetre, out string why)) return true;
            Debug.LogWarning($"[HeldCatchMesh] {why}: the fish stays a sprite.");
            return false;
        }

        /// <summary>The figure's overlay is padded by the fish's reach while it is drawn (its own claim,
        /// beside a rod's), and given back when it is not.</summary>
        private void KeepReach(IsoCharacterFigureRenderer figure)
        {
            if (!ReferenceEquals(figure, _reachFigure) && _reachFigure != null)
                _reachFigure.SetHeldReachMetres(IsoCharacterFigureRenderer.HeldReachCatch, 0f);
            _reachFigure = figure;
            if (figure != null && _renderer != null)
                figure.SetHeldReachMetres(IsoCharacterFigureRenderer.HeldReachCatch, _renderer.TipLocal.magnitude);
        }
    }
}
