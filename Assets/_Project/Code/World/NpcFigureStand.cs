using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>Where a villager's mesh figure stands: on her own feet, on the ground</b> (ADR 0044, amendment
    /// 2026-09-27). The villager's side of the Core seam, as <c>MooredBoat</c> is a skipper's: it says which
    /// sprite she is and who she is, and asks Core's <see cref="CharacterFigurePresentation"/> for a figure.
    /// World never learns what draws it (rule 4).
    ///
    /// <para><b>A publisher, nothing more.</b> <see cref="FigureCharacter"/> is her own
    /// <see cref="IsoCharacterSprite"/>, whose stance, gait and heading her routine already writes, and
    /// <see cref="FigureKey"/> is her <see cref="NpcDef.Id"/>. She stands on no hull, so the hull members
    /// answer null and zero and are never read ashore.</para>
    ///
    /// <para><b>Whether she is seen is not decided here.</b> Her routine's shelter hides her through her
    /// renderer's <c>enabled</c> and makes her untalkable through the <see cref="Interactable"/>'s. The
    /// figure reads the first; this stand reads and writes neither, and its liveness is its own.</para>
    ///
    /// <para>Added by <see cref="Interactable"/>'s <c>Awake</c> to a host that names an <see cref="NpcDef"/>
    /// and carries an <see cref="IsoCharacterSprite"/>. It asks for her figure once, in its own
    /// <c>Awake</c>. With no presentation service installed, or no skin on her art def, nothing is attached
    /// and her sprite draws exactly as before.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcFigureStand : MonoBehaviour, ICharacterFigureAshoreStand
    {
        private IsoCharacterSprite _character;
        private string _key;

        /// <inheritdoc/>
        public IsoCharacterSprite FigureCharacter => _character;

        /// <inheritdoc/>
        /// <remarks>Always null: she stands on the ground.</remarks>
        public Transform FigureHull => null;

        /// <inheritdoc/>
        /// <remarks>Zero, and never read ashore.</remarks>
        public Vector3 FigureStandRigMetres => Vector3.zero;

        /// <inheritdoc/>
        /// <remarks>Zero, and never read ashore: her facing is her sprite's heading.</remarks>
        public float FigureDeckBearingDegrees => 0f;

        /// <inheritdoc/>
        public string FigureKey => _key;

        private void Awake()
        {
            _character = GetComponent<IsoCharacterSprite>();
            Interactable talk = GetComponent<Interactable>();
            NpcDef npc = talk != null ? talk.Npc : null;
            _key = npc != null ? npc.Id : null;
            CharacterFigurePresentation.Service?.Attach(gameObject, this);
        }
    }
}
