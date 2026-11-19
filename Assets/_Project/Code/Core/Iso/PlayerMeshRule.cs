using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// One row of the player's mesh fallback map: when the skin cannot draw <see cref="From"/>, try
    /// <see cref="To"/>. Rows live in <see cref="GameConfig.PlayerMeshFallbacks"/>, so the owner can
    /// change which pose stands in for which without code (rule 6). Both are baked clip state keys.
    /// </summary>
    [Serializable]
    public struct MeshStateFallback
    {
        public string From;
        public string To;

        public MeshStateFallback(string from, string to)
        {
            From = from;
            To = to;
        }
    }

    /// <summary>
    /// <b>The player is always the mesh</b> (ADR 0060). The pure rule the player's presenter asks
    /// when <see cref="GameConfig.PlayerLegacySpriteFallback"/> is off: which baked clip draws a
    /// state, never "hand the picture back to the sprite sheet".
    ///
    /// <list type="number">
    /// <item>A whole-body clip names the skin state it wants (<see cref="WantedKeyFor"/>): the
    /// <see cref="CharacterClipStateMap"/> key where it has one, else the clip's own baked state
    /// (swim, tread, sleep, drive) or, for the three reaches, a name the map below resolves.</item>
    /// <item>A wanted state the skin carries as a well-formed clip draws as itself, listed in
    /// <see cref="CharacterSkinDef.MeshStates"/> or not: that list is the cast's per-state switch and
    /// the player no longer waits on it.</item>
    /// <item>Otherwise the fallback map is walked, row by row, to the nearest pose the skin does
    /// carry; and the end of every chain is <see cref="CharacterSkinStateMap.Idle"/>.</item>
    /// </list>
    ///
    /// <para>No allocation, no cache, no clock: the same skin, key and map give the same answer.</para>
    /// </summary>
    public static class PlayerMeshRule
    {
        // Baked clip state names (art, not tuning), the same convention as CharacterClipStateMap.
        public const string Swim = "swim";
        public const string Tread = "tread";
        public const string Sleep = "sleep";
        public const string Drive = "drive";
        public const string Reach = "reach";
        public const string ReachGround = "reachGround";
        public const string ReachStowV = "reachStowV";
        public const string ReachStowH = "reachStowH";

        /// <summary>
        /// The state a whole-body clip asks for. <see cref="CharacterClipStateMap.StateKeyFor"/> first,
        /// so every clip the cast already draws asks for exactly what it did; the clips that map left
        /// with the sprite name their own baked state here instead. Null only for
        /// <see cref="CharacterClip.None"/> and values outside the enum.
        /// </summary>
        public static string WantedKeyFor(CharacterClip clip, string carry = null)
        {
            string mapped = CharacterClipStateMap.StateKeyFor(clip, carry);
            if (mapped != null) return mapped;
            return clip switch
            {
                CharacterClip.Swim => Swim,
                CharacterClip.Tread => Tread,
                CharacterClip.Sleep => Sleep,
                CharacterClip.Drive => Drive,
                CharacterClip.ReachGround => ReachGround,
                CharacterClip.ReachStowV => ReachStowV,
                CharacterClip.ReachStowH => ReachStowH,
                _ => null,
            };
        }

        /// <summary>True when the skin carries <paramref name="key"/> as a clip it can actually pose:
        /// present, with a key table exactly as long as its frames times the skin's bones.</summary>
        public static bool Carries(CharacterSkinDef skin, string key)
        {
            if (skin == null || string.IsNullOrEmpty(key)) return false;
            return skin.TryGetClip(key, out CharacterSkinDef.SkinClip clip) && clip.KeysWellFormed(skin.BoneCount);
        }

        /// <summary>
        /// The clip that draws <paramref name="wanted"/> on <paramref name="skin"/>: itself when the
        /// skin carries it, else the first carried state down its fallback chain, else idle. False
        /// only when the skin cannot draw even idle (an unusable bake), which the presenter reports.
        /// The walk is bounded by the map's length, so a cycle in the data ends rather than spins.
        /// </summary>
        public static bool TryResolve(CharacterSkinDef skin, string wanted, MeshStateFallback[] map,
                                      out string resolved, out bool fellBack)
        {
            resolved = null;
            fellBack = false;
            if (skin == null) return false;

            string key = wanted;
            int hops = map != null ? map.Length : 0;
            for (int step = 0; step <= hops && !string.IsNullOrEmpty(key); step++)
            {
                if (Carries(skin, key))
                {
                    resolved = key;
                    fellBack = step > 0;
                    return true;
                }
                key = Next(map, key);
            }

            if (Carries(skin, CharacterSkinStateMap.Idle))
            {
                resolved = CharacterSkinStateMap.Idle;
                fellBack = true;
                return true;
            }
            return false;
        }

        /// <summary>The map's row for <paramref name="key"/>, or null. First match wins.</summary>
        public static string Next(MeshStateFallback[] map, string key)
        {
            if (map == null || string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < map.Length; i++)
                if (string.Equals(map[i].From, key, StringComparison.Ordinal)) return map[i].To;
            return null;
        }

        /// <summary>
        /// A frame of the clip that is drawing, from a frame counted on another clip (the sprite's,
        /// or the wanted state's before a fallback stood in): wrapped on a loop, held on a one-shot.
        /// </summary>
        public static int FrameIn(in CharacterSkinDef.SkinClip clip, int frame)
        {
            int count = clip.FrameCount;
            if (count <= 0) return 0;
            if (frame < 0) frame = 0;
            return clip.Loop ? frame % count : (frame < count ? frame : count - 1);
        }

        /// <summary>
        /// The shipped map, which a fresh <see cref="GameConfig"/> starts with and the shipped asset
        /// repeats row for row. Each row names the nearest baked pose: the three reaches share the
        /// one reach; a swimmer with no stroke treads; a helm with no clip of its own takes the plain drive;
        /// a carry vault with no carry clip takes the bare vault; the oars with no stroke rest on them.
        /// </summary>
        public static MeshStateFallback[] DefaultFallbacks() => new[]
        {
            new MeshStateFallback(ReachGround, Reach),
            new MeshStateFallback(ReachStowV, Reach),
            new MeshStateFallback(ReachStowH, Reach),
            new MeshStateFallback(Swim, Tread),
            new MeshStateFallback(Drive, CharacterClipStateMap.Bench),
            new MeshStateFallback("drive_helm_go_fast", Drive),
            new MeshStateFallback("drive_helm_bowrider", Drive),
            new MeshStateFallback("drive_helm_express_cruiser", Drive),
            new MeshStateFallback("astride_helm_jet_ski", "astride"),
            new MeshStateFallback("row_oars", "idle_oars"),
            new MeshStateFallback(CharacterClipStateMap.BoardBuckets, CharacterClipStateMap.Board),
            new MeshStateFallback(CharacterClipStateMap.BoardTray, CharacterClipStateMap.Board),
            new MeshStateFallback(CharacterClipStateMap.BoardPot, CharacterClipStateMap.Board),
            new MeshStateFallback(CharacterClipStateMap.BoardDownBuckets, CharacterClipStateMap.BoardDown),
            new MeshStateFallback(CharacterClipStateMap.BoardDownTray, CharacterClipStateMap.BoardDown),
            new MeshStateFallback(CharacterClipStateMap.BoardDownPot, CharacterClipStateMap.BoardDown),
            new MeshStateFallback("stagger", "balance"),
        };
    }
}
