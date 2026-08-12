using UnityEngine;
using HiddenHarbours.Core;   // IHullMeshRenderer / IDeckOccupantSlots / IsoCharacterSprite

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>A BOAT LYING AT HER BERTH</b> — somebody else's hull, moored, breathing on the same sea the
    /// shader draws, with her skipper standing on the deck. The owner's ruling (2026-08-10): the fleet is
    /// <i>rendered into the sea as floating assets at their berths</i>, and the ambient fleet uses the
    /// mesh assets with NPC workers/captains aboard. <b>No motion, no routine, no schedule</b> — those are
    /// a later arc (see <c>docs/design/nine-mile-creek-wharf.md</c> §9).
    ///
    /// <para><b>⭐ WHY THIS IS A RUNTIME COMPONENT AND NOT BUILDER OUTPUT.</b> The whole fleet is mesh
    /// (ADR 0022), and the mesh path is chosen LIVE:
    /// <c>IsoFacetHullPresentationService</c> registers itself at
    /// <c>RuntimeInitializeOnLoadMethod</c> and says so in its own words — <i>"edit-time scene BUILDERS
    /// deliberately do not [register], so a built scene serialises the sprite rig and the mesh path is
    /// chosen live, per run, by the skinner (builder-generated scenes must not bake a renderer whose
    /// setup is runtime-owned)"</i>. A builder that instantiated hulls itself would therefore bake the
    /// SPRITE fallback into the committed scene and the fleet would never be mesh at all — silently, with
    /// the right number of boats in the right places. So the builder places one of these per berth,
    /// carrying its owner's Def, and the hull is skinned here, on wake.</para>
    ///
    /// <para><b>⚠ THE WAVE TRAP, CLOSED BY CONSTRUCTION.</b> A floating hull must ride the PUBLISHED wave
    /// field — a private smoother agrees only with itself, and two boats on two smoothers sit on two
    /// different seas (paid for twice: PR #331's wavelength half, PR #460's phase half). Nothing here
    /// samples the sea. The skinner installs <see cref="BoatWaveMotion"/>, which reads
    /// <c>SharedWaveField</c> — the one instance the bridge publishes beside the shader push — and falls
    /// back to its own animator only when nothing is published at all. Rolling a rider here would be
    /// re-opening that hole; the one line that matters is that <c>SkipWaveMotion</c> stays FALSE.</para>
    ///
    /// <para><b>She settles at her OWN waterline</b> for the same reason: <c>BoatWaveMotion</c> takes the
    /// settle from the hull presenter (#460's law), so a punt and a Cape Islander lie at their own
    /// draughts rather than at a shared guess.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MooredBoat : MonoBehaviour
    {
        [Tooltip("Whose boat this is. Everything drawn here comes off this asset: her hull, her skipper, " +
                 "and (through the region's own plan) the berth she lies in.")]
        [SerializeField] private BoatOwnerDef _owner;

        [Tooltip("The compass heading (0 = North, clockwise) her BOW points while she lies alongside. " +
                 "The region's plan derives this from the mooring face rather than typing it — a boat " +
                 "lies parallel to the wall she is tied to, not across it.")]
        [SerializeField] private float _headingDegrees = 90f;

        [Tooltip("Name of the child the skipper is drawn under.")]
        public const string SkipperChildName = "Skipper";

        /// <summary>The shader that can be cut through by a hull in front of it, and the two properties
        /// that tell it which hull and how deep. Spelled here rather than reached for through Art, which
        /// Boats may not reference (rule 4) — the same string contract <c>DeckRiderVisual</c> keeps on
        /// the Player side of the same seam.</summary>
        private const string OccludedSpriteShader = "HiddenHarbours/DeckOccludedSprite";
        private static readonly int DeckOccluderIdProperty = Shader.PropertyToID("_HHDeckOccluderId");
        private static readonly int DeckOccluderIdTopProperty = Shader.PropertyToID("_HHDeckOccluderIdTop");

        private BoatHullSkinner.Rig _rig;
        private IsoCharacterSprite _skipper;
        private SpriteRenderer _skipperRenderer;
        private Material _skipperMaterial;
        private MaterialPropertyBlock _skipperBlock;
        private IDeckOccupantSlots _slots;
        private int _slot = -1;
        private Vector3 _standRigMeters;
        private float _idWritten = -1f, _idTopWritten = -1f;

        /// <summary>Whose boat this is. For the region's tests and tooling.</summary>
        public BoatOwnerDef Owner => _owner;

        /// <summary>True once the hull has actually been skinned — the claim a PlayMode test reads, and
        /// false on a hull whose art has not imported (warned, never thrown).</summary>
        public bool IsPresented => _rig.Skinned;

        /// <summary>The heading her bow lies at, in compass degrees.</summary>
        public float HeadingDegrees => _headingDegrees;

        /// <summary>Wire her before she wakes. The builder calls this; nothing else needs to.</summary>
        public void Configure(BoatOwnerDef owner, float headingDegrees)
        {
            _owner = owner;
            _headingDegrees = headingDegrees;
        }

        private void OnEnable() => Present();

        private void OnDisable()
        {
            if (_slots != null && _slot >= 0) _slots.Release(_slot, this);
            _slot = -1;
            _slots = null;
            if (_skipperMaterial != null) Destroy(_skipperMaterial);
            _skipperMaterial = null;
        }

        /// <summary>
        /// Draw her. Null-tolerant throughout, the region-dressing rule: an owner with no boat, a boat
        /// with no art, a pack that has not baked — each warns once and leaves her undrawn rather than
        /// throwing halfway through a scene load. Where she LIES is still correct either way, because the
        /// builder put this object there.
        /// </summary>
        private void Present()
        {
            if (_owner == null)
            {
                Debug.LogWarning($"[MooredBoat] '{name}' has no owner Def, so there is no boat to draw " +
                                 "and no skipper to stand on her. The berth is still hers on the plan; " +
                                 "point this at the owner asset and she appears.");
                return;
            }

            if (!_owner.IsPresentable())
            {
                Debug.LogWarning(
                    $"[MooredBoat] '{_owner.Id}' keeps " +
                    (_owner.Boat == null ? "no boat at all" : $"'{_owner.Boat.Id}', which has no visual") +
                    " — her berth stays empty. This is an authoring gap in Data/Boats/Owners, not a " +
                    "drawing one.");
                return;
            }

            // ⚠ THE BOW LIES ALONG THE WALL, and the heading is the region's, not a default here. A hull
            // rotated about Z by −heading has her bow (transform.up) on that compass bearing, which is
            // the convention BOTH presentation paths read: DirectionalBoatSprite counter-rotates the
            // child off it, and MeshHullDriver takes HeadingDegreesFromBow(transform.up).
            transform.rotation = Quaternion.Euler(0f, 0f, -_headingDegrees);

            // SkipWaveMotion stays FALSE — see the class note. SkipOars is true because there is no
            // controller to animate them from: an oar overlay with nobody pulling it reads as a fault,
            // and a moored boat has her oars shipped anyway.
            //
            // PaintScheme is where the register's colour reaches the water. It is read off the OWNER,
            // not off the boat def: every lobster boat on this wharf shares one BoatVisualDef and one
            // mesh, so a scheme on the visual would paint the whole fleet the same and there would be
            // nothing to tell them apart by. Null is the shipped look, so an owner with no scheme is
            // not a special case anywhere below.
            _rig = BoatHullSkinner.Apply(gameObject, _owner.Boat.Visual, boat: null,
                                         new BoatHullSkinner.Options
                                         {
                                             SkipOars = true,
                                             PaintScheme = _owner.HullPaint,
                                         });

            if (!_rig.Skinned)
            {
                Debug.LogWarning(
                    $"[MooredBoat] '{_owner.Id}': '{_owner.Boat.Visual.Id}' could not be skinned — her " +
                    "hull mesh has not baked and she has no full sprite compass to fall back to. The " +
                    "berth is drawn empty. Re-run Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Bake for this rig.");
                return;
            }

            StripTheTieOffs();
            StandTheSkipper();
        }

        /// <summary>
        /// ⚠ <b>A STRANGER'S BOAT IS NOT SOMETHING YOU MAY TIE TO.</b> The skinner writes the hull's own
        /// <see cref="BoatCleats"/> from her deck sidecar, and those register into <c>MooringCleats</c> —
        /// which is a GLOBAL spatial registry that <c>MooringController</c> searches by position. Left
        /// alone, seven moored NPC hulls would put dozens of tie-off points around the basin and the
        /// player could make a line fast to somebody else's boat: a rope attached to a hull nothing
        /// simulates, on a wharf where the whole point of #451 is that the thing you can see is the thing
        /// you can use.
        ///
        /// <para>So the component comes straight back off. Rafting up — tying to another boat — is a real
        /// thing a working wharf does and the fleet placer's own messages mention it; when it is built it
        /// should be built deliberately, on hulls that know they are being tied to, not inherited by
        /// accident from the skinner.</para>
        /// </summary>
        private void StripTheTieOffs()
        {
            var cleats = GetComponent<BoatCleats>();
            if (cleats != null) Destroy(cleats);
        }

        /// <summary>
        /// Put her skipper on the deck.
        ///
        /// <para><b>They ride the hull because they are parented to the VISUAL child</b>, which is the
        /// transform <see cref="BoatWaveMotion"/> writes roll, pitch and bob onto — so the figure heaves
        /// with the boat she is standing on rather than beside a hull that moves without her. That is the
        /// same attachment the player's own <c>DeckRiderVisual</c> uses, for the same reason.</para>
        ///
        /// <para><b>And the wheelhouse hides them properly</b>, per pixel, at any heading: the figure
        /// claims one of the hull's deck-occupant slots (#481's twelve-slot seam), is told where they
        /// stand in the hull's own rig metres, and draws through the occluded-sprite shader that discards
        /// against the id range the hull publishes. Without the claim the skipper would draw over her own
        /// cabin — which is exactly the defect the seam exists to prevent.</para>
        /// </summary>
        private void StandTheSkipper()
        {
            if (_owner.Skipper == null || _owner.AboardCount() <= 0) return;
            if (_rig.Visual == null) return;

            var child = new GameObject(SkipperChildName);
            child.transform.SetParent(_rig.Visual, worldPositionStays: false);
            // At the visual's own origin — the hull's pivot, which is her waterline amidships. The ONE
            // deck point this component can place a figure at without re-deriving the hull's iso
            // projection; see the seam note on BoatOwnerDef for why there is only one.
            child.transform.localPosition = Vector3.zero;

            _skipperRenderer = child.AddComponent<SpriteRenderer>();
            // Above the hull picture, so a skipper standing on deck is not drawn inside the planking.
            // The CABIN is what must hide them, and that is the occupant seam's job below — never a
            // sorting order, which cannot express "in front of the deck, behind the wheelhouse".
            _skipperRenderer.sortingOrder = _owner.Boat.Visual.SortingOrder + 1;

            _skipper = child.AddComponent<IsoCharacterSprite>();
            _skipper.Configure(_owner.Skipper);
            // Standing still, looking the way the boat looks. Held rather than left to the walk logic:
            // nothing moves this figure, so an unheld heading would resolve from a zero velocity.
            _skipper.HoldHeading(_headingDegrees);
            _skipper.HoldSpeed(0f);

            GiveTheOccludedMaterial();
            ClaimTheDeckSlot();
        }

        /// <summary>
        /// The material that CAN be cut through. Built here rather than wired by the builder so every rig
        /// gets it without a re-run, and owned here so it dies with the boat.
        ///
        /// <para><b>A missing shader is not fatal and must never be</b> (the <c>DeckRiderVisual</c>
        /// discipline): the skipper keeps whatever material she had and simply draws un-occluded, which
        /// is a figure standing in front of a wheelhouse — wrong, and visible, and far better than a
        /// magenta hole where a person should be.</para>
        /// </summary>
        private void GiveTheOccludedMaterial()
        {
            var shader = Shader.Find(OccludedSpriteShader);
            if (shader == null)
            {
                Debug.LogWarning(
                    $"[MooredBoat] '{_owner.Id}': the '{OccludedSpriteShader}' shader is not in this " +
                    "build, so her skipper draws un-occluded and will show through the wheelhouse from " +
                    "some headings. Everything else about her is unaffected.");
                return;
            }
            _skipperMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _skipperRenderer.sharedMaterial = _skipperMaterial;
        }

        private void ClaimTheDeckSlot()
        {
            _slots = _rig.Presenter?.DeckOccupants;
            if (_slots == null) return;   // a sprite hull has no facet depth to discard against

            _slot = _slots.Claim(this);
            if (_slot < 0) return;        // refused, and the hull has already said so loudly

            // WHERE THEY STAND, in the hull's own rig metres — derived from the deck SHE publishes, never
            // a typed offset: the middle of the measured walkable deck, at the height that deck actually
            // is there. A hull with no measured deck leaves them at the pivot, which is where they are
            // drawn anyway.
            _standRigMeters = StandPointOf(_owner.Boat.Visual.Deck);
            _slots.Set(_slot, this, _standRigMeters, active: true);
        }

        /// <summary>
        /// The rig-local point the skipper's feet are at, from the hull's own measured deck. Pure, so the
        /// region's tests can assert a hull puts her crew on her deck without standing a scene up.
        /// </summary>
        public static Vector3 StandPointOf(BoatDeckDef deck)
        {
            if (deck == null || !deck.HasWalkableDeck()) return Vector3.zero;

            Vector2 centre = deck.WalkCenter;
            int hint = 0;
            // Washboards deliberately excluded: a skipper stands on the DECK, not up on the rail.
            Vector2 on = deck.ClampToWalkable(centre, ref hint, out float height);
            return new Vector3(on.x, on.y, height);
        }

        private void LateUpdate()
        {
            // The id range the skipper discards against, refreshed each frame because the hull re-ranks
            // its occupants by depth as it turns — cheap and idempotent by contract, and skipped entirely
            // once the written values stop changing.
            if (_skipperRenderer == null || _slots == null || _slot < 0) return;

            float id = _slots.OccluderId(_slot);
            float top = _slots.OccluderIdTop;
            if (Mathf.Approximately(id, _idWritten) && Mathf.Approximately(top, _idTopWritten)) return;

            _skipperBlock ??= new MaterialPropertyBlock();
            _skipperRenderer.GetPropertyBlock(_skipperBlock);
            _skipperBlock.SetFloat(DeckOccluderIdProperty, id);
            _skipperBlock.SetFloat(DeckOccluderIdTopProperty, top);
            _skipperRenderer.SetPropertyBlock(_skipperBlock);
            _idWritten = id;
            _idTopWritten = top;
        }
    }
}
