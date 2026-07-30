using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HiddenHarbours.Tools.RigStudio
{
    /// <summary>What kind of dial an axis is — the three kinds every kit's parameter space has so far
    /// decomposed into, and the only three the studio knows how to draw.</summary>
    public enum RigStudioAxisKind
    {
        /// <summary>One of an enumerated set of string keys (species, presets, phases, stages,
        /// snow steps) — the values come from the kit's own catalog/contract, never restated.</summary>
        Keys,

        /// <summary>A bounded whole number (facing index, sway frame) — inclusive
        /// <see cref="RigStudioAxis.Min"/>..<see cref="RigStudioAxis.Max"/>.</summary>
        IntRange,

        /// <summary>A bounded whole number that SELECTS A VARIANT rather than measuring anything —
        /// same storage as <see cref="IntRange"/>, but the studio draws it with a "next variant"
        /// step instead of a slider. There is no hidden randomness: the seed is always visible and
        /// always the owner's.</summary>
        Seed,
    }

    /// <summary>
    /// One dial on a kit's parameter space. The legal values are ENUMERATED here by the kit's adapter —
    /// from the kit's own catalog, contract or rig tables — because the rigs' shared failure mode is
    /// silence: they resolve options as <c>opts[k] != null ? opts[k] : fallback</c>, so an unknown
    /// value renders something else with no error. The studio therefore only ever offers what an
    /// adapter enumerated, and <see cref="RigStudioPoints.Validate"/> refuses anything else.
    /// </summary>
    public sealed class RigStudioAxis
    {
        /// <summary>Stable parameter key — what a point dictionary is keyed by.</summary>
        public string Key;

        /// <summary>What the owner reads beside the control.</summary>
        public string Label;

        public RigStudioAxisKind Kind;

        /// <summary>Legal values for a <see cref="RigStudioAxisKind.Keys"/> axis, in the kit's own
        /// order. Null/ignored for the int kinds.</summary>
        public string[] Keys;

        /// <summary>
        /// Values that EXIST in the kit's space but are held back, mapped to the kit's own reason,
        /// shown verbatim (the worked example: Tamarack, excluded by the tree rig's own rule-1 gate,
        /// coordinator ruling 2026-07-29). Shown greyed-out — never hidden, never selectable:
        /// hiding it would make the hold-back look like a missing species, and selecting it would
        /// preview art the kit refuses to bake.
        /// </summary>
        public IReadOnlyDictionary<string, string> Excluded;

        /// <summary>Inclusive bounds for the int kinds.</summary>
        public int Min, Max;

        /// <summary>Starting value: a string from <see cref="Keys"/>, or an int within bounds.</summary>
        public object Default;

        /// <summary>Optional one-liner drawn under the control (e.g. what a facing index means).</summary>
        public string Note;
    }

    /// <summary>
    /// Everything the studio knows about a kit — which is to say, everything the kit's adapter taught
    /// it. The studio core holds no kit knowledge of its own: a new kit import registers a new adapter
    /// and the studio grows a tab with zero core changes.
    /// </summary>
    public sealed class RigStudioSchema
    {
        /// <summary>Tab title.</summary>
        public string KitName;

        /// <summary>One line for the owner: what this kit is.</summary>
        public string About;

        /// <summary>What "Bake this" actually writes, in the kit's own terms — drawn beside the
        /// button so the owner knows the bake's true granularity (a sheet, a set) before pressing it.</summary>
        public string BakeNote;

        public RigStudioAxis[] Axes;
    }

    /// <summary>
    /// A horizontal guide line over a preview, in cell px (top-left origin) — for kit facts that are
    /// TRUE OF THE CELL but not baked into its pixels, like the shrub kit's snow surface ("snow CLIPS,
    /// it never floods": the sheets bake snow-free and the scene removes rows below this line at
    /// placement). Guides are annotation, deliberately: pixels below a guide are still the baked
    /// pixels, so the preview==bake bit-identity holds at every parameter point, and the studio never
    /// grows a second render path to show a placement-time effect.
    /// </summary>
    public sealed class RigStudioGuide
    {
        /// <summary>Row in cell px from the cell's TOP-LEFT.</summary>
        public double Y;

        /// <summary>The kit's own words for what this line is.</summary>
        public string Label;
    }

    /// <summary>One rendered cell at one parameter point — the actual pixels, straight off the kit's
    /// own render path.</summary>
    public sealed class RigStudioPreview
    {
        /// <summary>RGBA, TOP-LEFT-origin rows (the rigs' convention), <see cref="Width"/>×<see cref="Height"/>×4.</summary>
        public byte[] Rgba;

        public int Width, Height;

        /// <summary>Pivot in cell px, top-left origin — where this thing plants when placed. Optional
        /// because not every kit's cell has a meaningful single pivot to show.</summary>
        public bool HasPivot;
        public double PivotX, PivotY;

        /// <summary>One line of the kit's own numbers about this cell (footprint metres, cell size,
        /// measured convention) — measured, never restated.</summary>
        public string Caption;

        /// <summary>Annotation lines over the pixels (may be null). See <see cref="RigStudioGuide"/>
        /// for why these exist and why they are never pixels.</summary>
        public RigStudioGuide[] Guides;
    }

    /// <summary>What a bake wrote and where the owner can now place it.</summary>
    public sealed class RigStudioBakeReport
    {
        /// <summary>Project-relative paths the bake landed (sheets, contract, prefab folder). They land
        /// in the WORKING TREE exactly as a menu bake would — the studio never commits anything.</summary>
        public string[] Minted;

        /// <summary>Where the thing the owner was looking at is now placeable, in one sentence.</summary>
        public string PlaceableNote;

        /// <summary>The kit's own bake report, verbatim.</summary>
        public string Summary;
    }

    /// <summary>
    /// The seam between the Rig Studio and a kit — <b>the studio knows NOTHING about any specific rig;
    /// each kit teaches the studio about itself through one of these</b> (lead-architect ruling,
    /// 2026-07-30). Three members:
    ///
    /// <list type="number">
    ///   <item><see cref="Describe"/> — the kit's parameter AXES, with legal values enumerated FROM the
    ///   kit's existing catalog/contract. Never restate a value the contract already carries.</item>
    ///   <item><see cref="Preview"/> — render ONE cell at a parameter point, through the SAME render
    ///   entry the kit's baker uses. ⚠️ ONE render path: the preview must be BIT-IDENTICAL to what a
    ///   bake at that point produces, and a test pins it per adapter. A second renderer that can
    ///   disagree with the bake means the owner approves art that never ships (the flick-cast lesson).</item>
    ///   <item><see cref="Bake"/> — delegate to the kit's EXISTING baker + slicer + catalog refresh.
    ///   The studio is a front door, never a parallel writer: output lands exactly where a menu bake
    ///   would, in the working tree, uncommitted.</item>
    /// </list>
    ///
    /// <para>Editor-only, discovered by <see cref="RigStudioAdapterRegistry"/> via
    /// <c>TypeCache</c> — an adapter needs a public parameterless constructor and nothing else, so a
    /// new kit's adapter shows up as a tab with zero studio-core changes. Adapters may hold a live
    /// JS host between previews (spinning one up costs far more than a render), hence
    /// <see cref="IDisposable"/>; the window disposes every adapter on close.</para>
    ///
    /// <para>Every method FAILS LOUDLY: an illegal or excluded axis value, a kit guard tripping, a
    /// slicer refusal — all throw with the kit's own message, never render a fallback and never
    /// swallow. A plausible default shown in the preview is exactly the class of silent defect this
    /// studio exists to retire.</para>
    /// </summary>
    public interface IRigStudioAdapter : IDisposable
    {
        RigStudioSchema Describe();

        RigStudioPreview Preview(IReadOnlyDictionary<string, object> point);

        RigStudioBakeReport Bake(IReadOnlyDictionary<string, object> point);
    }

    /// <summary>
    /// Parameter-point plumbing shared by the window, the adapters and the tests — one definition of
    /// "legal point", so an adapter cannot accidentally accept what its schema does not describe.
    /// </summary>
    public static class RigStudioPoints
    {
        /// <summary>A fresh point at the schema's defaults.</summary>
        public static Dictionary<string, object> Defaults(RigStudioSchema schema)
        {
            if (schema?.Axes == null) throw new ArgumentNullException(nameof(schema));
            var point = new Dictionary<string, object>(schema.Axes.Length, StringComparer.Ordinal);
            foreach (var axis in schema.Axes) point[axis.Key] = axis.Default;
            return point;
        }

        /// <summary>
        /// Refuse anything the schema does not describe: a missing axis, a stranger key, a value
        /// outside the enumerated set or bounds, or an EXCLUDED value (whose kit-stated reason is
        /// thrown verbatim). Throwing here — before any render — is what keeps "silently renders the
        /// default" out of this tool's vocabulary.
        /// </summary>
        public static void Validate(RigStudioSchema schema, IReadOnlyDictionary<string, object> point)
        {
            if (schema?.Axes == null) throw new ArgumentNullException(nameof(schema));
            if (point == null) throw new ArgumentNullException(nameof(point));

            foreach (var key in point.Keys)
                if (schema.Axes.All(a => !string.Equals(a.Key, key, StringComparison.Ordinal)))
                    throw new ArgumentException(
                        $"'{key}' is not an axis of {schema.KitName}. Known: " +
                        $"{string.Join(", ", schema.Axes.Select(a => a.Key))}.");

            foreach (var axis in schema.Axes)
            {
                if (!point.TryGetValue(axis.Key, out object value))
                    throw new ArgumentException(
                        $"The point does not set '{axis.Key}' ({schema.KitName}). Every axis must be " +
                        "explicit — an absent key is how a rig quietly falls through to its default.");

                switch (axis.Kind)
                {
                    case RigStudioAxisKind.Keys:
                        if (!(value is string s))
                            throw new ArgumentException(
                                $"'{axis.Key}' wants one of its enumerated keys, got " +
                                $"{value?.GetType().Name ?? "null"}.");

                        if (axis.Excluded != null && axis.Excluded.TryGetValue(s, out string why))
                            throw new ArgumentException(
                                $"'{s}' is EXCLUDED from {schema.KitName} on axis '{axis.Key}': {why}");

                        if (axis.Keys == null || Array.IndexOf(axis.Keys, s) < 0)
                            throw new ArgumentException(
                                $"'{s}' is not a value of axis '{axis.Key}' ({schema.KitName}). The " +
                                $"kit enumerates: {string.Join(", ", axis.Keys ?? Array.Empty<string>())}. " +
                                "Refusing rather than rendering a fallback — the rigs ignore unknown " +
                                "values in silence, and silence is the defect.");
                        break;

                    case RigStudioAxisKind.IntRange:
                    case RigStudioAxisKind.Seed:
                        if (!(value is int i))
                            throw new ArgumentException(
                                $"'{axis.Key}' wants an int, got {value?.GetType().Name ?? "null"}.");
                        if (i < axis.Min || i > axis.Max)
                            throw new ArgumentException(
                                $"{axis.Key} = {i} is outside the kit's own bounds " +
                                $"{axis.Min}..{axis.Max} ({schema.KitName}).");
                        break;

                    default:
                        throw new ArgumentException($"Unknown axis kind {axis.Kind} on '{axis.Key}'.");
                }
            }
        }

        /// <summary>The point's value for a Keys axis. Assumes <see cref="Validate"/> ran.</summary>
        public static string KeyOf(IReadOnlyDictionary<string, object> point, string axisKey) =>
            (string)point[axisKey];

        /// <summary>The point's value for an int axis. Assumes <see cref="Validate"/> ran.</summary>
        public static int IntOf(IReadOnlyDictionary<string, object> point, string axisKey) =>
            (int)point[axisKey];

        /// <summary>The point as one readable line, in schema order — for reports and error messages.</summary>
        public static string Describe(RigStudioSchema schema, IReadOnlyDictionary<string, object> point)
        {
            var sb = new StringBuilder();
            foreach (var axis in schema.Axes)
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(axis.Key).Append(':')
                  .Append(point.TryGetValue(axis.Key, out var v) ? v : "∅");
            }
            return sb.ToString();
        }
    }
}
