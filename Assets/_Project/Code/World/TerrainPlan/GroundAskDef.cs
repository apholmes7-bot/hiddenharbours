using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>Who wrote a ground ask: a package's ground file, or the game (a fix laid after the package's asks).</summary>
    public enum GroundAskSource
    {
        Package = 0,
        Game = 1,
    }

    /// <summary>What a ground ask lays on the base.</summary>
    public enum GroundAskKind
    {
        /// <summary>Heights on a grid of samples, bilinear between them: the last covering patch wins (the file's rule).</summary>
        Patch = 0,

        /// <summary>No ground: a rule the guards hold (the cannery's hold).</summary>
        Rule = 1,

        /// <summary>A profile's toe raised to the flats' level along a shore line, never lowered (fix 2).</summary>
        ShoreBlend = 2,

        /// <summary>A channel's bed held below a level along a line, never raised (fix 3).</summary>
        ChannelHold = 3,

        /// <summary>A closed hollow filled to its own spill level, raised only, outside a kept circle (fix 4).</summary>
        HollowFill = 4,
    }

    /// <summary>
    /// <b>ONE ASK ON A GROUND FILE.</b> One asset per ask (<c>ground.snake_case</c>, e.g. <c>ground.stp_main_beach</c>),
    /// listed in order by its <see cref="GroundFileDef"/>. A package's ask keeps the file's id, box, step and rule words,
    /// and its samples as a 16-bit PNG beside it. The game's own asks are the same Def with <see cref="Source"/> set to
    /// the game; they come after the package's. A re-run on a revised package rewrites the package's asks by id; an ask
    /// the package drops is <see cref="Retired"/>, and its id is never reused.
    ///
    /// <para><see cref="GroundFileImport"/> reads it. The import's map is an output; this asset is the source.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Ground Ask", fileName = "GroundAsk")]
    public class GroundAskDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (ground.snake_case). A package's ask keeps the file's id.")]
        public string Id = "ground.example";
        public string DisplayName = "";
        public GroundAskSource Source = GroundAskSource.Package;
        public GroundAskKind Kind = GroundAskKind.Patch;
        [Tooltip("A later package dropped it, or carries the fix itself: the import skips it, and its id stays taken.")]
        public bool Retired;
        [Tooltip("The ask's rule: the file's words for a package's ask, the fix's for the game's own.")]
        [TextArea] public string Rule = "";

        [Header("Provenance")]
        [Tooltip("The package the ask came in (its folder name); empty for the game's own.")]
        public string Package = "";
        [Tooltip("The ground file's sha256 (the JSON) the ask was read from; empty for the game's own.")]
        public string GroundFileSha256 = "";
        [Tooltip("The key scene the file names for the ask (its scene), or empty.")]
        public string Scene = "";
        [Tooltip("The file's own word for what the ask does (its kind: fill, raise, cut and fill, hold…).")]
        public string FileKind = "";
        [Tooltip("The file's ruling words for the ask (its ruled), or empty when it has none.")]
        public string Ruled = "";

        [Header("A patch (Kind = Patch)")]
        [Tooltip("The patch's south-west sample (world units)...")]
        public Vector2 BoxMin;
        [Tooltip("...and its north-east sample.")]
        public Vector2 BoxMax;
        [Tooltip("The samples' spacing (units).")]
        public float Step = 0.25f;
        [Tooltip("Samples west to east.")]
        public int Width;
        [Tooltip("Samples south to north.")]
        public int Height;
        [Tooltip("The samples as a 16-bit grey PNG, north up: the file's row 0, the box's south edge, is the image's bottom row.")]
        public Texture2D Patch;
        [Tooltip("sha256 of the samples as the file holds them (u16 big-endian, row 0 at the box's south edge): the import stops on a mismatch.")]
        public string PatchSha256 = "";
        [Tooltip("The samples' range (m): height = x + (y - x) × code / 65535.")]
        public Vector2 PatchRange = new Vector2(-4f, 12f);

        [Header("A line (ShoreBlend: the shore; ChannelHold: a wall's toe)")]
        [Tooltip("The line's points (world units), in order.")]
        public Vector2[] Line = new Vector2[0];
        [Tooltip("Another ask whose Line this one reads (the beach's shore), or empty to read its own.")]
        public string LineOf = "";
        [Tooltip("Where the line came from: a wall's real id and the table it was read from.")]
        public string LineSource = "";

        [Header("ShoreBlend (fix 2): the toe held at the flats' level; HollowFill (fix 4) reads the window too")]
        [Tooltip("The cells the ask may touch (world units): a bound on the work, wider than the band or the hollow.")]
        public Vector2 WindowMin = new Vector2(-14f, -86f);
        public Vector2 WindowMax = new Vector2(18f, -54f);
        [Tooltip("The shore's metric: y is divided by sin(this), so the line's distances are ground metres (CD's SE).")]
        public float ShoreYScaleDeg = 40f;
        [Tooltip("The flats' level is sampled along the line at this spacing (m)...")]
        public float SampleStep = 0.05f;
        [Tooltip("...from this far before its west end to this far past its east end (m), zero beyond the ends.")]
        public float SampleRunOut = 6f;
        [Tooltip("The flats' level at a sample: smax(0, the base there, this) (m), so it is never below 0 m.")]
        public float Softness = 0.25f;
        [Tooltip("The level is smoothed along the line by a Gaussian of this sigma (m)...")]
        public float Sigma = 1f;
        [Tooltip("...cut at this many sigmas.")]
        public float KernelSigmas = 4f;
        [Tooltip("The band fades in from x m along the line from its west end, over y m...")]
        public Vector2 AlongIn = new Vector2(0.5f, 5f);
        [Tooltip("...and out from x m, over y m.")]
        public Vector2 AlongOut = new Vector2(13f, 1f);
        [Tooltip("Across the line: it fades in from x m (seaward is negative), over y m...")]
        public Vector2 SeaFade = new Vector2(-1.2f, 0.8f);
        [Tooltip("...and out from x m inland, over y m.")]
        public Vector2 LandFade = new Vector2(6f, 2f);
        [Tooltip("Seaward of the line the target is the beach's own sea rule: smax(SeaZ × (1 − exp(d / SeaTail)), the base, Softness) (m).")]
        public float SeaZ = -1.05f;
        public float SeaTail = 9f;

        [Header("ChannelHold (fix 3): the bed below the spring low")]
        [Tooltip("The bed is held at least this far below the spring low (m), where the base holds water there.")]
        public float Margin = 0.05f;
        [Tooltip("The line is walked at this step (units); every texel its bilinear read touches is a hold cell.")]
        public float FootprintStep = 0.02f;

        [Header("A rule (Kind = Rule); HollowFill's seed")]
        [Tooltip("The rule's centre (world units), or the texel a hollow fill starts from...")]
        public Vector2 Centre;
        [Tooltip("...and the rule's reach (units).")]
        public float Reach;

        [Header("HollowFill (fix 4): a closed hollow to its spill")]
        [Tooltip("The spill level the hollow was measured at (m): the import stops when the ground's own spill is not this.")]
        public float Level;
        [Tooltip("How far the ground's spill may sit from Level (m) before the import stops.")]
        public float LevelTolerance = 0.01f;
        [Tooltip("A rule ask (its id) whose circle the fill keeps out of: texels inside it are held, not filled.")]
        public string KeepOutOf = "";
    }
}
