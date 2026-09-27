using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>ONE OF TODAY'S SHORE ROCKS, WEARING A ROCK PX FORM.</b> One asset per rock, keyed by a
    /// stable, append-only <see cref="Id"/> (<c>rock.snake_case</c>, e.g.
    /// <c>rock.stp_strand_block</c>). Terrain pass 9, part 2 §4.4: the rock keeps its place and
    /// takes a v6 form from the Rock Px kit.
    ///
    /// <para><b>Which rock.</b> <see cref="Today"/> and <see cref="At"/> name the rock the shore
    /// painter lays today (its object name and where it stands). The Shoreline refresh
    /// (<c>StPetersLayerRefresh</c>) finds exactly one painted rock with that name within a
    /// hundredth of a metre of <see cref="At"/>, or refuses.</para>
    ///
    /// <para><b>Which sprite: kit coordinates, not a reference.</b> The sprite is named by the
    /// kit's own coordinates — form, stone, dress, variant, mirror and tide state — and the editor
    /// step resolves them through the kit's catalog (<c>RockPxCatalog</c>) to the sheet's cell
    /// <c>&lt;stem&gt;_c&lt;col&gt;_r&lt;row&gt;</c>. A <c>Sprite</c> field would serialize the
    /// Rock Px sprite's GUID, and the kit's <c>.meta</c> files are not committed, so that GUID
    /// would resolve only in the checkout that wrote it. The coordinates hold no GUID, read like
    /// the plan's table, and a typo fails in the catalog's own check.</para>
    ///
    /// <para>Plain data, no behaviour: the World module holds it and the App editor step reads it.
    /// Nothing at runtime reads it yet.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Shore Rock", fileName = "ShoreRock")]
    public class ShoreRockDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (rock.snake_case; St Peters' rocks are rock.stp_*).")]
        public string Id = "rock.example";

        [Header("Which rock (the shore painter's, today)")]
        [Tooltip("The painted rock's object name today, e.g. Rock_bs or FieldRock_bs.")]
        public string Today = "Rock_bs";

        [Tooltip("Where that rock stands, in world metres, to 0.01 m. It keeps this place.")]
        public Vector2 At;

        [Header("Its Rock Px form (the kit's coordinates)")]
        [Tooltip("The kit's form key: erratic, block, knuckle, skerry, ... (RockPxCatalog.FormKeys).")]
        public string Form = "block";

        [Tooltip("The kit's stone: sandstone, till, basalt, granite or quartzite. The form must ship it.")]
        public string Stone = "sandstone";

        [Tooltip("The tide row the rock is drawn in: dry, wet or awash (RockPxCatalog.Tides).")]
        public string State = "dry";

        [Tooltip("The dress sheet: bare, barnacled or weeded. Part 2 names none, so bare.")]
        public string Dress = "bare";

        [Tooltip("Which of the kit's four variants (0-3 = A-D). Part 2 names none, so A.")]
        [Range(0, 3)] public int Variant;

        [Tooltip("Draw the variant's mirror (the sheet's columns 4-7).")]
        public bool Mirrored;
    }
}
