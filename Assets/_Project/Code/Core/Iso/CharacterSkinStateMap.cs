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
    /// which is the entire reason this mapping is a pure function of (stance, gait, carry).</para>
    ///
    /// <para><b>Helm and Oars are the rig's carry stances, and rig 9 bakes them.</b> Rig 7 baked no
    /// <c>helm</c> or <c>oars</c> clip, so both stances drew the free body. Rig 9's cast carries
    /// <c>helm_idle</c> / <c>helm_walk</c> (anim <c>idle</c> / <c>walk</c> with carry <c>helm</c>) and
    /// <c>oars_idle</c> / <c>oars_row</c> (anim <c>idle</c> / <c>walk</c> with carry <c>oars</c>), and the
    /// bake keys them the way the sheet baker keys every carry clip — <c>anim_carry</c>, so
    /// <c>idle_helm</c>, <c>walk_helm</c>, <c>idle_oars</c>, <c>walk_oars</c> (<see cref="CarryKey"/>, the
    /// Core twin of <c>CharacterState.Key</c>, pinned against it over every carry clip of the ten defs).
    /// A stance bakes no run, by design, on the rig and on the sprite alike (the rig has no
    /// <c>run_helm</c> or <c>run_oars</c>; <c>CharacterVisualDef.SheetFor</c> gives a stance's run an
    /// empty sheet and <c>Playable</c> sends it to the free body): so a run at the helm or the oars ASKS
    /// for the free <c>run</c>, exactly as the sprite draws it, and nothing falls back. A def that lacks a
    /// stance clip (every rig 7 def) falls back to the free gait and says so through
    /// <c>fellBack</c>.</para>
    ///
    /// <para><b>A carry is the third input.</b> The rig's <c>pose()</c> takes the carry stance, so a
    /// bucket walk braces the arms and leans differently from a free walk. Which stance a held thing
    /// asks for is DATA (<see cref="CharacterCarryPoseDef"/>), never a switch here; this file only
    /// spells the key. A carry applies to the FREE stance alone: the rig ignores a carry on a stance
    /// clip, and a balance, helm or oars clip wins over it.</para>
    /// </summary>
    public static class CharacterSkinStateMap
    {
        public const string Idle = "idle";
        public const string Walk = "walk";
        public const string Run = "run";
        public const string Balance = "balance";

        /// <summary>The rig's carry stance for <see cref="CharacterStance.Helm"/>. A guard reads the rig's
        /// clip table and holds this spelling to it.</summary>
        public const string HelmCarry = "helm";

        /// <summary>The rig's carry stance for <see cref="CharacterStance.Oars"/>. Held to the rig the
        /// same way.</summary>
        public const string OarsCarry = "oars";

        /// <summary>
        /// The clip key for a stance and gait — the name to look up with
        /// <see cref="CharacterSkinDef.TryGetClip"/> and to gate with
        /// <see cref="CharacterSkinDef.DrawsAsMesh"/>.
        /// </summary>
        public static string StateKeyFor(CharacterStance stance, CharacterGait gait) =>
            StateKeyFor(stance, gait, null);

        /// <summary>
        /// The clip key for a stance, a gait and the carry stance a held thing asks for (null or empty
        /// for none). A stance other than <see cref="CharacterStance.Free"/> wins over the carry.
        /// </summary>
        public static string StateKeyFor(CharacterStance stance, CharacterGait gait, string carry)
        {
            switch (stance)
            {
                case CharacterStance.Balance: return Balance;
                // No stance bakes a run (see the class doc): a run there is the free run, as the sprite.
                case CharacterStance.Helm: return gait == CharacterGait.Run ? Run : CarryGaitKey(gait, HelmCarry);
                case CharacterStance.Oars: return gait == CharacterGait.Run ? Run : CarryGaitKey(gait, OarsCarry);
                default: return CarryGaitKey(gait, carry);
            }
        }

        // A presenter resolves every frame, and a concatenated key is an allocation per figure per frame
        // (rule 7). So each carry's three gait keys are built once, on first sight, and reused. A pure
        // memo of a pure function: nothing here is state a new run could see differently.
        static readonly System.Collections.Generic.Dictionary<string, string[]> s_carryGaitKeys =
            new System.Collections.Generic.Dictionary<string, string[]>(System.StringComparer.Ordinal);

        static string CarryGaitKey(CharacterGait gait, string carry)
        {
            if (string.IsNullOrEmpty(carry)) return GaitKey(gait);
            if (!s_carryGaitKeys.TryGetValue(carry, out string[] keys))
            {
                keys = new[] { CarryKey(Idle, carry), CarryKey(Walk, carry), CarryKey(Run, carry) };
                s_carryGaitKeys[carry] = keys;
            }
            return keys[gait == CharacterGait.Run ? 2 : gait == CharacterGait.Walk ? 1 : 0];
        }

        /// <summary>The free-body clip for a gait. The fallback every unbaked stance lands on.</summary>
        public static string GaitKey(CharacterGait gait) => gait switch
        {
            CharacterGait.Run => Run,
            CharacterGait.Walk => Walk,
            _ => Idle,
        };

        /// <summary>
        /// The state key of an anim under a carry stance: <c>walk</c> + <c>buckets</c> →
        /// <c>walk_buckets</c>, and the bare anim when there is no carry. The Core twin of the sheet
        /// baker's <c>CharacterState.Key</c> for a clip with no power and no rest, which is every carry
        /// clip the rig has; a test holds the two equal over every carry clip of the ten defs.
        /// </summary>
        public static string CarryKey(string anim, string carry) =>
            string.IsNullOrEmpty(carry) ? anim : anim + "_" + carry;

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
                                   out string stateKey, out bool fellBack) =>
            Resolve(def, stance, gait, null, out stateKey, out fellBack);

        /// <summary>
        /// <see cref="Resolve(CharacterSkinDef, CharacterStance, CharacterGait, out string, out bool)"/>
        /// with a carry stance: <c>walk_buckets</c>, else the free <c>walk</c>, else <c>idle</c>.
        /// </summary>
        public static bool Resolve(CharacterSkinDef def, CharacterStance stance, CharacterGait gait,
                                   string carry, out string stateKey, out bool fellBack)
        {
            fellBack = false;
            stateKey = null;
            if (def == null) return false;

            string wanted = StateKeyFor(stance, gait, carry);
            if (def.TryGetClip(wanted, out _)) { stateKey = wanted; return true; }

            fellBack = true;
            string gaitKey = GaitKey(gait);
            if (gaitKey != wanted && def.TryGetClip(gaitKey, out _)) { stateKey = gaitKey; return true; }
            if (def.TryGetClip(Idle, out _)) { stateKey = Idle; return true; }
            return false;
        }
    }
}
