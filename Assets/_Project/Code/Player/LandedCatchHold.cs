using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The landed fish held up in the fisher's hand, as its mesh</b> (ADR 0053): which held mesh the
    /// catch is at its size rung, which hand it hangs from (the free left, unless that hand is behind the
    /// body at this facing, then the nearer one), and the <see cref="HeldCatchMesh"/> that draws it.
    /// Shared by every presenter that draws a landing; the figure is handed in by the caller each frame
    /// (the player's live mesh figure, a villager's own), so nothing here knows whose hand it is.
    /// </summary>
    public sealed class LandedCatchHold
    {
        private readonly Transform _home;
        private HeldCatchMesh _mesh;
        private HeldPropMeshDef _def;
        private float _scale = 1f;
        private string _socket = HeldPropSockets.LeftHandTool;

        /// <param name="home">The transform the mesh's driver is made under.</param>
        public LandedCatchHold(Transform home) { _home = home; }

        /// <summary>The landed-fish mesh driver, once made. For tests and plates.</summary>
        public HeldCatchMesh Mesh => _mesh;

        /// <summary>The hand socket the fish hangs from (sticky: it changes only when the other hand is
        /// clearly nearer the camera).</summary>
        public string Socket => _socket;

        /// <summary>Pick the held mesh for this catch at the sized kind the hands will carry it as, and
        /// start again from the free (left) hand.</summary>
        public void Resolve(CatchItemLibrary catchArt, string fishId, float weightKg)
        {
            _def = catchArt != null
                ? catchArt.HeldMeshFor(fishId, catchArt.SizedKindFor(fishId, weightKg), out _scale)
                : null;
            _socket = HeldPropSockets.LeftHandTool;
        }

        /// <summary>Would <see cref="TryDraw"/> do anything: the switch is on and there is a mesh for
        /// this catch, or a mesh was made and must be told this frame's answer.</summary>
        public bool Wants(GameConfig config)
            => (config != null && config.MeshCatchInHand && _def != null) || _mesh != null;

        /// <summary>
        /// Draw the fish from its hand on <paramref name="figure"/> this frame (the figure posed for this
        /// frame, or null while the fisher is a sprite). False draws nothing; the caller shows its sprite.
        /// </summary>
        public bool TryDraw(GameConfig config, IsoCharacterFigureRenderer figure)
        {
            bool on = config != null && config.MeshCatchInHand && _def != null;
            if (!on && _mesh == null) return false;
            _mesh ??= new HeldCatchMesh(_home, "LandedCatchMesh");
            // From the free (left) hand, unless that hand is behind him at this facing: then the nearer one,
            // so the fish he shows off is never hidden by his own body.
            string other = _socket == HeldPropSockets.LeftHandTool
                ? HeldPropSockets.RightHandTool : HeldPropSockets.LeftHandTool;
            if (config != null && HeldCatchMesh.TrySocketDepth(figure, _socket, out float here)
                && HeldCatchMesh.TrySocketDepth(figure, other, out float there))
                _socket = HeldCatchMesh.NearerSocket(_socket, other, here, there,
                                                     config.HeldFishHandSwapDepthMeters);
            float scale = config != null
                ? HeldCatchMesh.ReadableScale(_def, _scale, config.HeldFishMinLengthPx)
                : _scale;
            return _mesh.TryDraw(config, _def, scale, _socket, 0, figure);
        }

        /// <summary>Stop drawing it (kept for the next landing).</summary>
        public void Hide()
        {
            if (_mesh != null && _mesh.Drawing) _mesh.Hide();
        }

        /// <summary>Destroy what was made.</summary>
        public void Dispose()
        {
            _mesh?.Dispose();
            _mesh = null;
        }
    }
}
