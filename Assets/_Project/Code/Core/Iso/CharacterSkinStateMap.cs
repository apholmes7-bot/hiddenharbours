namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Which clip the figure is in, from the inputs the SPRITE already has.</b>
    ///
    /// <para><b>There is exactly one authority for stance, facing and gait, and it is not this
    /// file.</b> <c>DeckRiderVisual</c> decides what the player is doing and hands it to
    /// <c>IsoCharacterSprite</c>; the mesh presenter reads the SAME two values at the SAME seam and
    /// turns them into a clip name. Nothing here looks at the boat, the input stack or the clock. If
    /// the mesh and the sprite ever disagree about what she is doing, the bug is upstream of both,
    /// which is the entire reason this mapping is a pure function of (stance, gait).</para>
    ///
    /// <para><b>⚠ THE STANCES ARE MOSTLY NOT BAKED, AND FOR TWO OF THEM THE MESH IS A STEP BACK.</b>
    /// Rig 7's clip table is the ANIMS rows, and only <c>balance</c> of the four stances is one of
    /// them — there is no <c>helm</c> clip and no <c>oars</c> clip. So Helm and Oars resolve to the
    /// gait clip and draw the FREE BODY.</para>
    ///
    /// <para>Be exact about what that costs, because the obvious reading is wrong. The PLAYER's visual
    /// def is <c>FisherIso.asset</c> (<c>visual.fisher_iso</c> — the def every scene wires into the
    /// Player's <c>IsoCharacterSprite</c>), and it DOES carry a HelmStance and an OarsStance sheet, 6
    /// idle and 8 walk frames each. On the sprite path she visibly takes the wheel and visibly pulls an
    /// oar; on the mesh path she stands there. That is a divergence this PR ships KNOWINGLY, not a gap
    /// the mesh inherits, and it is one of the reasons the mesh stays behind a toggle that defaults to
    /// the sprite. (<c>BoyIso.asset</c> is the other iso character def, the one with empty stance
    /// sheets — nothing loads it at runtime, so do not read its gap as the player's.) The only stance
    /// the mesh matches is <c>balance</c>, which is at least the one she spends deck time in. The fix
    /// is an ANIMS row, not a pose invented here.</para>
    /// </summary>
    public static class CharacterSkinStateMap
    {
        public const string Idle = "idle";
        public const string Walk = "walk";
        public const string Run = "run";
        public const string Balance = "balance";

        /// <summary>
        /// The clip key for a stance and gait — the name to look up with
        /// <see cref="CharacterSkinDef.TryGetClip"/> and to gate with
        /// <see cref="CharacterSkinDef.DrawsAsMesh"/>.
        /// </summary>
        public static string StateKeyFor(CharacterStance stance, CharacterGait gait) =>
            stance == CharacterStance.Balance ? Balance : GaitKey(gait);

        /// <summary>The free-body clip for a gait. The fallback every unbaked stance lands on.</summary>
        public static string GaitKey(CharacterGait gait) => gait switch
        {
            CharacterGait.Run => Run,
            CharacterGait.Walk => Walk,
            _ => Idle,
        };

        /// <summary>
        /// Resolve to a clip the def actually carries, walking the fallback chain
        /// <c>stance → gait → idle</c> and reporting whether the first choice survived.
        ///
        /// <para><paramref name="fellBack"/> is not decoration. A presenter that silently drew idle
        /// for a stance the bake forgot would look like a working feature with a missing pose, which
        /// is the hardest kind of art gap to find; the caller publishes this so a test and a plate can
        /// both see it.</para>
        /// </summary>
        public static bool Resolve(CharacterSkinDef def, CharacterStance stance, CharacterGait gait,
                                   out string stateKey, out bool fellBack)
        {
            fellBack = false;
            stateKey = null;
            if (def == null) return false;

            string wanted = StateKeyFor(stance, gait);
            if (def.TryGetClip(wanted, out _)) { stateKey = wanted; return true; }

            fellBack = true;
            string gaitKey = GaitKey(gait);
            if (gaitKey != wanted && def.TryGetClip(gaitKey, out _)) { stateKey = gaitKey; return true; }
            if (def.TryGetClip(Idle, out _)) { stateKey = Idle; return true; }
            return false;
        }
    }
}
