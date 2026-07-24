using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>What kind of control an axis needs, and how it serialises into the options literal.</summary>
    public enum BuildingAxisKind
    {
        /// <summary>One of a set of strings — a dropdown.</summary>
        Choice,
        /// <summary>A 0..1 dial (size, weather, window density).</summary>
        Unit,
        /// <summary>A small whole number (chimneys, stacks) — <see cref="BuildingAxis.MaxCount"/> caps it.</summary>
        Count,
        /// <summary>On/off (dock, hvac, boom, night).</summary>
        Flag,
    }

    /// <summary>
    /// One dial on a building rig: the option key <c>resolve()</c> reads, what kind of control it wants,
    /// and where its allowed values come from.
    /// </summary>
    public readonly struct BuildingAxis
    {
        /// <summary>The options key, exactly as the rig's <c>resolve()</c> spells it.</summary>
        public readonly string Key;

        /// <summary>Label for the inspector.</summary>
        public readonly string Label;

        public readonly BuildingAxisKind Kind;

        /// <summary>
        /// For a <see cref="BuildingAxisKind.Choice"/>: the name of the rig's exported table whose KEYS
        /// are the allowed values (<c>"BODY"</c>, <c>"SIDINGS"</c>, …), or null when the values are
        /// inline literals in the rig's source rather than an exported table — see <see cref="Inline"/>.
        /// </summary>
        public readonly string RigTable;

        /// <summary>
        /// Allowed values for a Choice axis whose rig exports NO table for it.
        ///
        /// <para>⚠️ These are transcribed from the rig's source, so they are the one thing here that can
        /// drift if a rig drop changes them. <c>BuildingAxisTests</c> greps each value out of the rig
        /// file, so a drop that renames one fails loudly instead of silently offering a value the rig
        /// will ignore (the rigs fall through to a default on an unknown string — no error).</para>
        /// </summary>
        public readonly string[] Inline;

        /// <summary>Upper bound for a Count axis (inclusive).</summary>
        public readonly int MaxCount;

        /// <summary>What the rig itself defaults to when the key is absent — the studio's starting value.</summary>
        public readonly object Default;

        public BuildingAxis(string key, string label, BuildingAxisKind kind, object def,
                            string rigTable = null, string[] inline = null, int maxCount = 3)
        {
            Key = key; Label = label; Kind = kind; Default = def;
            RigTable = rigTable; Inline = inline; MaxCount = maxCount;
        }
    }

    /// <summary>
    /// The dials each building rig exposes, and the serialiser that turns a dialled set into the JS
    /// options literal <c>render()</c> takes.
    ///
    /// <para><b>Why the axis KEYS are hand-written but the VALUES are not.</b> The keys are the rig's
    /// API — <c>resolve()</c> reads <c>siding</c>, <c>winD</c>, <c>cupola</c> and so on, and there is no
    /// way to discover them from the rig short of parsing its JavaScript. The allowed VALUES, though,
    /// are mostly exported as tables (<c>SIDINGS</c>, <c>BODY</c>, <c>ROOFS</c>, <c>DOORS</c>,
    /// <c>CUPOLAS</c>, <c>WINDOWS</c>, <c>SHAPES</c>, <c>TYPES</c>) — so the studio reads those live and
    /// a rig drop that adds a paint colour shows up in the dropdown with no code change. Only the
    /// handful with no exported table are transcribed, and those are grepped by a test.</para>
    ///
    /// <para><b>⚠️ Unknown values fail SILENTLY.</b> Both rigs resolve an option with
    /// <c>opts[k] != null ? opts[k] : fallback</c> and then look it up in a table — a misspelled value
    /// is not an error, it just quietly renders something else. That is why the studio only ever offers
    /// values it read from the rig, and why the inline lists are tested rather than trusted.</para>
    /// </summary>
    public static class BuildingAxes
    {
        /// <summary>
        /// <c>wharfBuildingRig</c>'s dials, in a sensible authoring order (massing → cladding → colour →
        /// openings → fittings → weathering).
        ///
        /// <para>⚠️ The window-density OPTION is <c>winDensity</c>, even though the rig's internal
        /// build field and its TYPES/PRESETS entries spell it <c>winD</c>. The line that matters is
        /// <c>winD: opts.winDensity != null ? opts.winDensity : T.winD</c> — the left side is internal,
        /// the right side is the option key. Dialling <c>winD</c> would be accepted in silence and do
        /// nothing, since these rigs ignore unknown keys. The house rig is the same shape.</para>
        /// </summary>
        public static readonly BuildingAxis[] Wharf =
        {
            new BuildingAxis("type",    "Type",          BuildingAxisKind.Choice, "shack",        rigTable: "TYPES"),
            new BuildingAxis("shape",   "Roofline",      BuildingAxisKind.Choice, "gable",        rigTable: "SHAPES"),
            new BuildingAxis("size",    "Size",          BuildingAxisKind.Unit,   0.4),
            new BuildingAxis("siding",  "Siding",        BuildingAxisKind.Choice, "shingle",      rigTable: "SIDINGS"),
            new BuildingAxis("base",    "Wainscot",      BuildingAxisKind.Choice, "none",
                             inline: new[] { "none", "block" }),
            new BuildingAxis("body",    "Body colour",   BuildingAxisKind.Choice, "greyShingle",  rigTable: "BODY"),
            new BuildingAxis("roof",    "Roof",          BuildingAxisKind.Choice, "asphaltGrey",  rigTable: "ROOFS"),
            new BuildingAxis("door",    "Main door",     BuildingAxisKind.Choice, "doubleBarn",   rigTable: "DOORS"),
            new BuildingAxis("windows", "Windows",       BuildingAxisKind.Choice, "twoOverTwo",   rigTable: "WINDOWS"),
            new BuildingAxis("winDensity", "Window density", BuildingAxisKind.Unit, 0.35),
            new BuildingAxis("cupola",  "Cupola",        BuildingAxisKind.Choice, "none",         rigTable: "CUPOLAS"),
            new BuildingAxis("loft",    "Gable loft",    BuildingAxisKind.Choice, "none",
                             inline: new[] { "none", "window", "door" }),
            new BuildingAxis("dock",    "Loading dock",  BuildingAxisKind.Flag,   false),
            new BuildingAxis("hvac",    "HVAC",          BuildingAxisKind.Flag,   false),
            new BuildingAxis("stacks",  "Stacks",        BuildingAxisKind.Count,  0, maxCount: 3),
            new BuildingAxis("vents",   "Vents",         BuildingAxisKind.Flag,   false),
            new BuildingAxis("sign",    "Sign board",    BuildingAxisKind.Flag,   false),
            new BuildingAxis("boom",    "Roof hoist",    BuildingAxisKind.Flag,   false),
            new BuildingAxis("weather", "Weathering",    BuildingAxisKind.Unit,   0.0),
            new BuildingAxis("night",   "Night (lit)",   BuildingAxisKind.Flag,   false),
        };

        /// <summary>
        /// <c>houseIsoRig</c>'s dials. Note <c>era</c> is a *seed* axis like the wharf rig's
        /// <c>type</c> — it fills in shape/roof/siding/pitch/windows/attic defaults, which any explicit
        /// axis then overrides.
        /// </summary>
        public static readonly BuildingAxis[] House =
        {
            new BuildingAxis("era",        "Era",         BuildingAxisKind.Choice, "plain",       rigTable: "ERAS"),
            new BuildingAxis("shape",      "Massing",     BuildingAxisKind.Choice, "gable",       rigTable: "SHAPES"),
            new BuildingAxis("size",       "Size",        BuildingAxisKind.Unit,   0.4),
            new BuildingAxis("siding",     "Siding",      BuildingAxisKind.Choice, "shingle",     rigTable: "SIDINGS"),
            new BuildingAxis("body",       "Body colour", BuildingAxisKind.Choice, "greyShingle", rigTable: "BODY"),
            new BuildingAxis("lower",      "Lower colour",BuildingAxisKind.Choice, "red",         rigTable: "BODY"),
            new BuildingAxis("roof",       "Roof",        BuildingAxisKind.Choice, "asphaltGrey", rigTable: "ROOFS"),
            new BuildingAxis("windows",    "Windows",     BuildingAxisKind.Choice, "twoOverTwo",  rigTable: "WINDOWS"),
            new BuildingAxis("winDensity", "Window density", BuildingAxisKind.Unit, 0.35),
            new BuildingAxis("attic",      "Attic",       BuildingAxisKind.Choice, "gable",
                             inline: new[] { "none", "gable", "gothic", "round" }),
            new BuildingAxis("porch",      "Porch",       BuildingAxisKind.Choice, "none",
                             inline: new[] { "none", "front", "wrap" }),
            new BuildingAxis("dormers",    "Dormers",     BuildingAxisKind.Count,  0, maxCount: 4),
            new BuildingAxis("chimneys",   "Chimneys",    BuildingAxisKind.Count,  1, maxCount: 3),
            new BuildingAxis("bay",        "Bay window",  BuildingAxisKind.Flag,   false),
            new BuildingAxis("bargeboard", "Bargeboard",  BuildingAxisKind.Flag,   false),
            new BuildingAxis("crossGable", "Cross gable", BuildingAxisKind.Count,  0, maxCount: 1),
            new BuildingAxis("garage",     "Garage",      BuildingAxisKind.Flag,   false),
            new BuildingAxis("weather",    "Weathering",  BuildingAxisKind.Unit,   0.0),
            new BuildingAxis("night",      "Night (lit)", BuildingAxisKind.Flag,   false),
        };

        /// <summary>The dial set for a catalog rig key.</summary>
        public static BuildingAxis[] For(string rigKey) => rigKey switch
        {
            "house" => House,
            "wharfBuilding" => Wharf,
            _ => throw new ArgumentException($"'{rigKey}' is not a building rig."),
        };

        /// <summary>Every building rig the studio can drive.</summary>
        public static readonly string[] RigKeys = { "wharfBuilding", "house" };

        /// <summary>
        /// Serialise a dialled set into the JS object literal <c>render()</c> takes.
        ///
        /// <para>Pure and static so it can be tested without a JS engine — the studio's whole output
        /// funnels through this one string, so a quoting bug here would be a syntax error inside the rig
        /// call rather than anything legible.</para>
        ///
        /// <para><paramref name="elev"/> is appended when given: the rigs take camera elevation as an
        /// option, and the studio exposes it so a build can be previewed at a non-default camera without
        /// that leaking into what gets baked (the bake always uses the rig's default).</para>
        /// </summary>
        public static string ToOptionsLiteral(IEnumerable<KeyValuePair<string, object>> dialled,
                                              double? elev = null)
        {
            var sb = new StringBuilder("{");
            bool first = true;

            foreach (var kv in dialled)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(kv.Key).Append(':').Append(Literal(kv.Value));
            }

            if (elev.HasValue)
            {
                if (!first) sb.Append(',');
                sb.Append("elev:").Append(Literal(elev.Value));
            }

            return sb.Append('}').ToString();
        }

        /// <summary>One value as a JS literal. Invariant culture — a comma decimal separator would
        /// silently split an option into two.</summary>
        public static string Literal(object v) => v switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            string s => $"'{s.Replace("\\", "\\\\").Replace("'", "\\'")}'",
            int i => i.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString("0.####", CultureInfo.InvariantCulture),
            double d => d.ToString("0.####", CultureInfo.InvariantCulture),
            _ => throw new ArgumentException($"Cannot serialise {v.GetType().Name} into a rig option."),
        };

        /// <summary>The defaults for a rig, as the studio's starting state.</summary>
        public static Dictionary<string, object> Defaults(string rigKey) =>
            For(rigKey).ToDictionary(a => a.Key, a => a.Default);

        /// <summary>
        /// A filename-safe stem for a build name. Studio builds are named by the owner, and a name with
        /// a space or a slash in it would otherwise produce a path that fails deep inside File.WriteAllBytes.
        /// </summary>
        public static string SafeStem(string name, string fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return fallback;

            var sb = new StringBuilder(name.Length);
            foreach (char c in name.Trim())
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');

            string s = sb.ToString().Trim('_');
            return s.Length == 0 ? fallback : s;
        }
    }
}
