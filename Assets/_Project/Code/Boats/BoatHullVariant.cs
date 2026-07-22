namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>How a hull is RENDERED</b> — the discriminator ADR 0022 introduces so sprite hulls and (later)
    /// real-time mesh hulls can coexist behind <see cref="IBoatHullPresenter"/>. A
    /// <see cref="BoatVisualDef"/> carries one; <see cref="BoatHullSkinner"/> reads it to decide which
    /// presenter to build.
    ///
    /// <para><b>⚠ TWO independent things keep existing assets on <see cref="Sprite"/>, and both are
    /// load-bearing.</b> Every <see cref="BoatVisualDef"/> in <c>Data/Boats/Visuals</c> predates this
    /// field, so none of them has a <c>Variant:</c> key in its YAML.</para>
    ///
    /// <list type="number">
    ///   <item><b>The field initialiser</b> (<c>= BoatHullVariant.Sprite</c> on
    ///   <see cref="BoatVisualDef.Variant"/>). This is what actually protects the shipped assets today —
    ///   <b>measured, not assumed</b>: with the numbering deliberately inverted, every committed asset
    ///   still loaded as <see cref="Sprite"/>, because Unity constructs the managed object (running
    ///   initialisers) and only then overlays the YAML, so a key that is absent leaves the initialiser
    ///   standing. Delete that initialiser and this protection is gone.</item>
    ///   <item><b>The numbering.</b> <see cref="Sprite"/> must stay 0 as the second line of defence, for
    ///   every path where the initialiser does NOT get to run — a zero-filled managed struct, an element
    ///   grown into a resized array, <c>default(BoatHullVariant)</c> in any code that constructs a
    ///   variant without going through the asset. If <see cref="Mesh"/> were 0, each of those quietly
    ///   becomes a mesh hull, and phase 1 ships no mesh renderer, so that boat is invisible.</item>
    /// </list>
    ///
    /// <para>New variants are therefore <b>append-only</b>: add them at the END, never renumber, never
    /// insert — the value is persisted as an <c>int</c>, so renumbering re-points every asset that stored
    /// the old number. <c>BoatHullPresenterSeamTests</c> pins the numbering, the initialiser, and the
    /// real committed assets separately, because they are separate guarantees.</para>
    /// </summary>
    public enum BoatHullVariant
    {
        /// <summary>
        /// Pre-drawn facing cells swapped by heading and kept screen-axis-aligned — the shipped path
        /// (<see cref="DirectionalBoatSprite"/>), and the value every existing asset deserialises to.
        /// <b>Must remain 0.</b>
        /// </summary>
        Sprite = 0,

        /// <summary>
        /// A real-time 3D hull mesh extracted from the same rig, rotated continuously and shaded by the
        /// facet shader (ADR 0022). <b>Not implemented yet</b> — phase 1 defines the seam only, and
        /// <see cref="BoatHullSkinner"/> still builds the sprite path for every hull. Declared here so
        /// the discriminator, and the tests that guard its numbering, land before any mesh code does.
        /// </summary>
        Mesh = 1,
    }
}
