using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Cuts this hull's house open while the occupant is below, and closes it the instant she is
    /// not</b> — the owner's cutaway ruling of 2026-08-26, wired to the two facts that already say
    /// where she is.
    ///
    /// <para><b>The ruling, and the whole of this component's logic:</b> in a boat interior the view
    /// shows the boat EXTERIOR with a wall/roof CUTAWAY revealing the interior. <i>At the helm →
    /// exterior only. Player out on deck → exterior only.</i> So the cut is live for exactly one
    /// state — somebody is below on THIS hull and is not steering her — and every other state is the
    /// shipped whole-boat picture.</para>
    ///
    /// <para><b>It invents no third source of truth.</b> "Is she below, and on which level" is
    /// <c>CabinSignals</c>, published by <see cref="BoatInterior"/> on its three real transitions.
    /// "Is she at this wheel" is <c>HelmSlot.PilotedHull</c>, the occupancy arbiter (#642), read on
    /// <see cref="ControlModeChanged"/> — which <c>ControlSwitcher</c> publishes AFTER its own
    /// <c>Mode</c> setter has written the piloted hull, so the read is never a frame stale. Neither
    /// fact is polled: a hull nobody has boarded costs one idle subscription and nothing per frame.</para>
    ///
    /// <para><b>⚠️ Three vocabularies name one room, and the def is the authority here.</b> The
    /// signal carries an index into <c>BoatInteriorDef.Levels</c>; the hull's mesh carries the rig's
    /// own int in TexCoord1.x; the interior SHEETS run a third order that is neither, and indexing
    /// one by the other has already drawn the wrong room once on this fleet. The join is the def's
    /// level ID (<c>house_sole</c>) matched against <c>HullMeshDef.LevelTags[].DeckId</c>, which the
    /// RIG published beside the tag — builder-computed data carried down, never a suffix rule
    /// re-derived at runtime.</para>
    ///
    /// <para><b>State survives <c>OnDisable</c>, deliberately</b> — the same law
    /// <see cref="BoatInterior"/> and <c>DeckRiderVisual</c> keep, and for the same reason:
    /// root-toggling IS how a region hop works, so a component that reset there would close the house
    /// around a player who is still standing in it, at every boundary she crossed while below.
    /// <c>OnEnable</c> re-asserts; <c>OnDisable</c> only unsubscribes.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoatCutaway : MonoBehaviour
    {
        /// <summary>Not a level index — "nobody is below on this hull". −1 rather than 0 because 0 is
        /// a real level on every def in the fleet.</summary>
        private const int NotBelow = -1;

        private BoatInterior _cabin;
        private HullMeshDef _mesh;
        private Transform _boatRoot;
        private object _hullToken;

        private IHullCutaway _renderer;
        private int _belowLevel = NotBelow;
        private bool _subscribed;

        /// <summary>The cut this component last asked her renderer for — None = whole exterior. Read
        /// by tests, and by anyone debugging a house that will not open.</summary>
        public HullMeshDef.Cut RequestedCut { get; private set; }

        /// <summary>True while the occupant is below on this hull, whatever the helm says. Separate
        /// from <see cref="RequestedLevelTag"/> on purpose: "she is below but at the wheel" is a real
        /// state with a closed house, and one field cannot say both.</summary>
        public bool OccupantIsBelow => _belowLevel != NotBelow;

        /// <summary>
        /// Wire this cutaway to its hull. <paramref name="hullToken"/> is the identity
        /// <c>ControlSwitcher</c> declares to <see cref="HelmSlot.SetPilotedHull"/> — this boat's
        /// <see cref="BoatController"/> — held rather than re-resolved, because a token re-derived
        /// later is a token that can drift off the one the declaration used.
        /// </summary>
        public void Configure(BoatInterior cabin, HullMeshDef mesh, Transform boatRoot, object hullToken)
        {
            _cabin = cabin;
            _mesh = mesh;
            _boatRoot = boatRoot != null ? boatRoot : transform;
            _hullToken = hullToken;
            Subscribe();
            Reassert();
        }

        private void OnEnable()
        {
            Subscribe();
            // The swap is an INVARIANT that is maintained, not merely established: a region hop
            // disables and re-enables this root with the player still below, and the renderer she
            // comes back to is the same one holding the same cut. Re-asserting is free when nothing
            // moved (ShowCutawayLevel early-returns on an unchanged level).
            Reassert();
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            if (_subscribed) return;
            EventBus.Subscribe<CabinEntered>(OnCabinEntered);
            EventBus.Subscribe<CabinLeft>(OnCabinLeft);
            EventBus.Subscribe<ControlModeChanged>(OnControlModeChanged);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            EventBus.Unsubscribe<CabinEntered>(OnCabinEntered);
            EventBus.Unsubscribe<CabinLeft>(OnCabinLeft);
            EventBus.Unsubscribe<ControlModeChanged>(OnControlModeChanged);
            _subscribed = false;
        }

        // ⚠ A cabin on ANOTHER boat is not this boat's business. Eighteen lobster boats can be afloat
        // in one creek, most of them the same def — a hull that took every CabinEntered as its own
        // would open its house because somebody boarded a sister ship two berths down.
        private void OnCabinEntered(CabinEntered e)
        {
            if (e.HullId != HullId) return;
            _belowLevel = e.Level;
            Reassert();
        }

        private void OnCabinLeft(CabinLeft e)
        {
            if (e.HullId != HullId) return;
            _belowLevel = NotBelow;
            Reassert();
        }

        // Taking or giving up a wheel changes nothing about where she is standing, so the cabin
        // publishes nothing — but it changes whether the house should be open, which is why this is
        // a second input and not a derivation of the first.
        private void OnControlModeChanged(ControlModeChanged e)
        {
            if (!OccupantIsBelow) return;   // nothing to open or close
            Reassert();
        }

        private EntityId HullId =>
            _boatRoot != null ? _boatRoot.gameObject.GetEntityId() : gameObject.GetEntityId();

        /// <summary>
        /// Say again what this hull should be showing. Cheap and idempotent, so every input can just
        /// call it rather than each one working out what changed.
        /// </summary>
        private void Reassert()
        {
            HullMeshDef.Cut cut = WantedCut();
            RequestedCut = cut;

            IHullCutaway target = Renderer();
            if (target == null) return;     // a sprite hull, or the mesh has not been skinned on yet
            target.ShowCutaway(cut);
        }

        /// <summary>
        /// The cut the ruling asks for — the level, and the lid its ceiling record names (the
        /// coordinator ruling of 2026-08-27, one hop). None — the whole exterior — is the answer to
        /// every question except one, and each of the ways it can be None is a deliberate refusal
        /// rather than a gap: nobody below; she is at THIS wheel; the def has no such level; the rig
        /// declared that level OPEN (cutting one would be cutting the sky); this hull was baked
        /// before the cutaway kit.
        /// </summary>
        private HullMeshDef.Cut WantedCut()
        {
            if (_belowLevel == NotBelow) return HullMeshDef.Cut.None;
            if (PlayerIsAtThisHelm()) return HullMeshDef.Cut.None;
            if (_mesh == null) return HullMeshDef.Cut.None;

            string deckId = DeckIdOf(_belowLevel);
            return string.IsNullOrEmpty(deckId)
                ? HullMeshDef.Cut.None
                : _mesh.CutawayForDeck(deckId);
        }

        /// <summary>The def's own id for a level index — <c>house_sole</c>, <c>cuddy_sole</c>. The
        /// def is the level authority; nothing here reads a sheet row or a rig name.</summary>
        private string DeckIdOf(int level)
        {
            BoatInteriorDef def = _cabin != null ? _cabin.Def : null;
            if (def == null || def.Levels == null) return null;
            if (level < 0 || level >= def.Levels.Length) return null;
            return def.Levels[level] != null ? def.Levels[level].Id : null;
        }

        /// <summary>
        /// Is the player steering THIS hull? Compared by <see cref="object.ReferenceEquals"/>, the way
        /// <see cref="HelmSlot"/> itself arbitrates — Unity's <c>==</c> reports two DESTROYED objects
        /// as equal to null and therefore to each other, which would make a dead hull match every
        /// other dead hull.
        /// </summary>
        private bool PlayerIsAtThisHelm()
        {
            if (_hullToken == null) return false;
            return ReferenceEquals(GameServices.Helm.PilotedHull, _hullToken);
        }

        /// <summary>
        /// Find the hull's cutaway-capable renderer, once, and hold it until it goes away.
        ///
        /// <para><b>Resolved lazily rather than at Configure</b> because the skin is applied by
        /// <c>BoatHullSkinner</c> on its own schedule and may not have run yet — and because the A/B
        /// sprite⇄mesh toggle can tear the renderer off and put a different one back under a hull
        /// that never stopped existing.</para>
        ///
        /// <para>⚠️ The liveness test goes through <c>UnityEngine.Object</c>'s own <c>==</c>. An
        /// interface reference compared with <c>== null</c> / <c>is null</c> / <c>?.</c> sees the raw
        /// reference and is satisfied by a destroyed component's fake-null, so the cutaway would go
        /// on writing to a renderer that is gone.</para>
        /// </summary>
        private IHullCutaway Renderer()
        {
            if (_renderer is UnityEngine.Object live && live != null) return _renderer;
            _renderer = GetComponentInChildren<IHullCutaway>(includeInactive: true);
            return _renderer is UnityEngine.Object found && found != null ? _renderer : null;
        }
    }
}
