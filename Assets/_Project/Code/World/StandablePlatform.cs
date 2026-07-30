using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// A <b>static rectangular standable structure</b> — the concrete <see cref="IStandableSurface"/> a
    /// built thing (a wharf deck, a jetty, a boardwalk) registers so the on-foot sim knows the fisher stands
    /// on IT and not on the seabed beneath it. Drop it on a structure's root, author the world-space
    /// footprint + deck height, and it registers itself into <see cref="StandableSurfaces"/> on enable —
    /// the same self-installing, scene-scoped pattern as <see cref="TidalTerrain"/> /
    /// <see cref="PaintedTidalTerrain"/> (CLAUDE.md rule 4: gameplay reads it through Core and never
    /// references this class).
    ///
    /// <para><b>Why it lives beside the terrain components.</b> A deck is authored GEOMETRY, exactly like
    /// the height map: the world publishes it, gameplay consumes it. And like them it is <b>data</b> (rule 6)
    /// — the footprint and deck height are serialized tunables the owner can nudge in the Inspector without
    /// touching code, with a <see cref="Configure"/> for the region builders (the
    /// <c>RegionAnchor.Configure</c> convention).</para>
    ///
    /// <para><b>The deck height is MEASURED, never guessed.</b> A structure's deck should be authored from
    /// the ground it launches from (the wharf builder samples the terrain at the pier root), so it can never
    /// drift out of step with a terrain edit. Author it clear of the region's highest water and the deck is
    /// dry at every tide; author it low and the sea reaches it, honestly, through the same wade bands as any
    /// other ground — which is the behaviour a float or an awash washboard actually wants.</para>
    ///
    /// <para><b>Static only, on purpose.</b> The footprint is an axis-aligned world-space
    /// <see cref="Rect"/>, so this cannot express a deck that MOVES or ROTATES (the M2 walkable-boat-deck
    /// vision). That is deliberate: a moving deck is another <see cref="IStandableSurface"/>
    /// implementation, not a change to the contract — see <see cref="IStandableSurface"/>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StandablePlatform : MonoBehaviour, IStandableSurface
    {
        [Header("Identity")]
        [Tooltip("Stable id of the structure (e.g. 'wharf.st_peters') — diagnostics only; never a lookup key.")]
        [SerializeField] private string _id = "structure.unnamed";

        [Header("Footprint (WORLD space, axis-aligned — metres)")]
        [Tooltip("The rectangle of deck you can stand on, in WORLD metres (not local to this transform, so " +
                 "a builder can author it straight from the structure's own cell geometry). One metre " +
                 "outside it the sim is back to reading the ground/seabed.")]
        [SerializeField] private Rect _footprint = new Rect(0f, 0f, 1f, 1f);

        [Header("Deck height")]
        [Tooltip("The standing height of the deck, in metres above chart datum — the SAME frame the tide " +
                 "model and the terrain use. Measure it off the ground the structure launches from rather " +
                 "than picking a number: authored above the region's highest water the deck is dry at every " +
                 "tide; authored below it, the sea reaches it through the ordinary wade bands.")]
        [SerializeField] private float _deckElevation = 0f;

        /// <inheritdoc/>
        public string Id => _id;

        /// <summary>The world-space rectangle of standable deck (for tests / tooling / the validator).</summary>
        public Rect Footprint => _footprint;

        /// <summary>The deck's standing height, metres above chart datum (for tests / tooling).</summary>
        public float DeckElevation => _deckElevation;

        /// <summary>
        /// Author the platform in one call (region builders; the direct-configure convention). The footprint
        /// is WORLD space — pass the structure's own geometry, not an offset from this transform.
        /// </summary>
        public void Configure(string id, Rect worldFootprint, float deckElevation)
        {
            _id = id;
            _footprint = worldFootprint;
            _deckElevation = deckElevation;
        }

        private void OnEnable() => StandableSurfaces.Register(this);

        private void OnDisable() => StandableSurfaces.Unregister(this);

        /// <inheritdoc/>
        public bool TryGetDeckElevation(Vector2 worldPos, out float deckElevation)
        {
            deckElevation = _deckElevation;
            return Contains(_footprint, worldPos);
        }

        /// <summary>
        /// Is a world position on the deck? Pure + static so the footprint rule is EditMode-testable without
        /// a component.
        ///
        /// <para>⚠ Deliberately NOT <see cref="Rect.Contains(Vector2)"/>: that is half-open (it excludes
        /// xMax/yMax), so a deck authored as cells <c>[288, 329) × [-3, 3)</c> would report its own north
        /// and east lips as water. The tile grid's cell rectangles ARE half-open, but "can I stand here" is
        /// asked about a point, and a point exactly on the lip is on the planks — so both edges are
        /// inclusive. A visible seam at the deck edge is a bug; half a texel of overlap is not.</para>
        /// </summary>
        public static bool Contains(Rect footprint, Vector2 worldPos)
            => worldPos.x >= footprint.xMin && worldPos.x <= footprint.xMax
            && worldPos.y >= footprint.yMin && worldPos.y <= footprint.yMax;
    }
}
