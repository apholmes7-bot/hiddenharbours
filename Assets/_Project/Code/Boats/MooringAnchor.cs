using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// What a mooring line is made fast to — the <b>tie target</b>. Today there are two:
    /// <list type="bullet">
    ///   <item><b>Held by the player's hand</b> (a moving anchor — the line follows the player as they
    ///   walk), and</item>
    ///   <item><b>Rooted to a fixed ground point</b> (the player drops the line at their feet and roams).</item>
    /// </list>
    ///
    /// <para><b>Future work (NOT built — structured for it).</b> This abstraction is the seam that lets the
    /// tie target grow beyond "the player / the bare ground" to dedicated <b>cleats / posts /
    /// user-placeable tie items</b> without reworking the rope physics: any of them is just another
    /// <see cref="IMooringAnchor"/> whose <see cref="Position"/> the tether reads. It also makes a future
    /// <b>second line</b> (a bow line + a stern line, each made fast to its own anchor) a matter of holding
    /// two <see cref="BoatMooring"/>/anchor pairs rather than a new mechanic. See
    /// design/boats-and-navigation.md §9.6.</para>
    /// </summary>
    public interface IMooringAnchor
    {
        /// <summary>Where the line is made fast, in world space — read fresh each tick so a moving anchor
        /// (the player's hand) drags the boat along on its leash.</summary>
        Vector2 Position { get; }

        /// <summary>True for a fixed anchor (rooted ground / a cleat / a post) the boat tethers to while the
        /// player roams free; false for a moving anchor (the player's hand) that the boat follows.</summary>
        bool IsFixed { get; }
    }

    /// <summary>A fixed tie point in world space — the rope rooted to the ground at the player's feet (and,
    /// later, a cleat/post/placed tie item, which would supply its own position the same way). The boat
    /// tethers here and the player is free to roam.</summary>
    public readonly struct FixedAnchor : IMooringAnchor
    {
        private readonly Vector2 _position;
        public FixedAnchor(Vector2 position) { _position = position; }
        public Vector2 Position => _position;
        public bool IsFixed => true;
    }

    /// <summary>
    /// A tie point on a <see cref="HiddenHarbours.Core.IMooringCleat"/> — the M2-38 case this whole
    /// abstraction was written in anticipation of (see the interface doc's "future work": a dedicated
    /// cleat/post is "just another <see cref="IMooringAnchor"/> whose Position the tether reads"). Read
    /// live, because a boat's cleat swings with her heading and a wharf's does not.
    ///
    /// <para><see cref="IsFixed"/> reports whether the cleat is SHORE-side: a bollard holds still and the
    /// boat swings around it, which is the anchor semantic. A boat-side cleat is not a fixed anchor — it
    /// is the far END of the line, and <c>BoatMooring</c> reads it as such rather than tethering to
    /// it.</para>
    /// </summary>
    public readonly struct CleatAnchor : IMooringAnchor
    {
        private readonly HiddenHarbours.Core.IMooringCleat _cleat;
        public CleatAnchor(HiddenHarbours.Core.IMooringCleat cleat) { _cleat = cleat; }

        /// <summary>The cleat this anchor names (null-safe for a torn-down region).</summary>
        public HiddenHarbours.Core.IMooringCleat Cleat => _cleat;

        public Vector2 Position => _cleat != null ? _cleat.WorldPosition : Vector2.zero;
        public bool IsFixed => _cleat != null && _cleat.Side == HiddenHarbours.Core.CleatSide.Shore;
    }

    /// <summary>A moving tie point that tracks a <see cref="Transform"/> — the rope held in the player's
    /// hand. The boat is tethered to wherever the player currently stands, so it follows them on the leash.
    /// The transform is read live each tick (null-safe: a destroyed/absent transform reports
    /// <see cref="Vector2.zero"/>, which the caller guards by only using a held anchor while the player
    /// exists).</summary>
    public readonly struct TransformAnchor : IMooringAnchor
    {
        private readonly Transform _transform;
        public TransformAnchor(Transform transform) { _transform = transform; }
        public Vector2 Position => _transform != null ? (Vector2)_transform.position : Vector2.zero;
        public bool IsFixed => false;
    }
}
