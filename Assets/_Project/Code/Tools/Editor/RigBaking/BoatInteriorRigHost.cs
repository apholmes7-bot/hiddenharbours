using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Loads <c>boatInteriorRig.js</c> over the one exterior hull rig it needs, and spells out every
    /// render option so no cell is drawn from a default nobody chose.
    ///
    /// <para><b>The interior renderer is a DELEGATING rig and the delegation is silent when it
    /// fails.</b> <c>hullEnv()</c> reads <c>root[HULLS[key].sym].loft</c> and returns <c>null</c> if
    /// the global is absent or carries no loft; <c>render()</c> then returns a FOUR-BYTE buffer rather
    /// than throwing. So the exterior rig must be installed first, it must be the KIT's revised copy
    /// (only that one publishes <c>loft</c>), and the result must be size-checked — a rig that quietly
    /// declined to draw looks like a very small picture.</para>
    ///
    /// <para><b>Which exterior file goes with which hull key is read FROM THE RIG</b>
    /// (<c>BoatInterior.HULLS</c> carries <c>sym</c>, <c>rig</c> and, where a family makes more than one
    /// boat, <c>stem</c>), never from a table restated here. A table would be one more thing to drift.</para>
    /// </summary>
    public static class BoatInteriorRigHost
    {
        /// <summary>The global the interior renderer installs.</summary>
        public const string Global = BoatInteriorKit.GlobalName;

        /// <summary>Facings per level, from the kit.</summary>
        public const int Facings = BoatInteriorKit.Facings;

        /// <summary>One row of the interior rig's own <c>HULLS</c> table.</summary>
        public readonly struct HullBinding
        {
            /// <summary>The interior rig's hull key — <c>lobster</c>, <c>tanker</c>, <c>lobvar</c>.</summary>
            public readonly string Key;
            /// <summary>The exterior global this hull's rooms are measured from — <c>TankerIso</c>.</summary>
            public readonly string ExteriorGlobal;
            /// <summary>The exterior rig's file name, as the interior rig names it.</summary>
            public readonly string ExteriorRigFileName;
            /// <summary>The hull stem this row claims, when it claims one (the sport fishers and the
            /// lobster variants do; the single-boat rigs do not).</summary>
            public readonly string DeclaredStem;
            /// <summary>True when the exterior rig makes many boats and needs a variant to resolve one.</summary>
            public readonly bool VariantAware;
            /// <summary>The sub-hull this row picks out of a multi-hull exterior global
            /// (<c>convertible</c>, <c>skybridge</c>) — empty when the global IS the hull object.</summary>
            public readonly string Pick;

            public HullBinding(string key, string exteriorGlobal, string exteriorRigFileName,
                               string declaredStem, bool variantAware, string pick)
            {
                Key = key ?? "";
                ExteriorGlobal = exteriorGlobal ?? "";
                ExteriorRigFileName = exteriorRigFileName ?? "";
                DeclaredStem = declaredStem ?? "";
                VariantAware = variantAware;
                Pick = pick ?? "";
            }

            /// <summary>
            /// The JS expression for this row's exterior HULL OBJECT — the thing that carries
            /// W/H/pivot, <c>render()</c> and <c>rock()</c>. Mirrors the interior rig's own
            /// resolution (<c>if(meta.pick &amp;&amp; E.byId) E=E.byId(meta.pick)</c>): a multi-hull
            /// global (the sport fishers' <c>SportFisherIso2</c>) publishes geometry per hull, so
            /// addressing the GLOBAL reads <c>undefined</c> and crashes where a refusal should be.
            /// <c>byId</c> falls back to its default hull on an unknown pick — the registration
            /// probe's agreement gate catches that by cell size.
            /// </summary>
            public string ExteriorHullJs =>
                Pick.Length > 0
                    ? $"({ExteriorGlobal}.byId && {ExteriorGlobal}.byId('{Pick}') || {ExteriorGlobal})"
                    : ExteriorGlobal;

            /// <summary>The exterior rig's stem, i.e. its file name without <c>.js</c>.</summary>
            public string ExteriorRigStem =>
                ExteriorRigFileName.EndsWith(".js", StringComparison.Ordinal)
                    ? ExteriorRigFileName.Substring(0, ExteriorRigFileName.Length - 3)
                    : ExteriorRigFileName;
        }

        /// <summary>Absolute path of a file inside the kit.</summary>
        public static string KitPath(params string[] parts)
        {
            string p = Path.Combine(RigCatalog.RepoRoot, BoatInteriorKit.KitFolder);
            foreach (string s in parts) p = Path.Combine(p, s);
            return p;
        }

        /// <summary>
        /// Read the interior rig's <c>HULLS</c> table by running the renderer ALONE. It installs its
        /// global with no exterior present — every hull simply resolves to nothing — which is exactly
        /// what makes the table readable before we know which exterior to load.
        /// </summary>
        public static IReadOnlyList<HullBinding> ReadHullTable(IRigScriptHost host)
        {
            host.Execute(File.ReadAllText(KitPath(BoatInteriorKit.InteriorRigFileName)));

            if (!host.EvaluateBool($"typeof {Global} === 'object' && {Global} !== null"))
                throw new InvalidOperationException(
                    $"{BoatInteriorKit.InteriorRigFileName} ran but installed no globalThis.{Global}. " +
                    "Either the renderer changed shape or the kit folder holds a different file.");

            int count = (int)host.EvaluateNumber($"Object.keys({Global}.HULLS).length");
            var rows = new List<HullBinding>(count);
            for (int i = 0; i < count; i++)
            {
                string key = host.EvaluateString($"Object.keys({Global}.HULLS)[{i}]");
                string h = $"{Global}.HULLS['{key}']";
                rows.Add(new HullBinding(
                    key,
                    host.EvaluateString($"{h}.sym || ''"),
                    host.EvaluateString($"{h}.rig || ''"),
                    host.EvaluateString($"{h}.stem || ''"),
                    host.EvaluateBool($"!!{h}.variantAware"),
                    host.EvaluateString($"{h}.pick || ''")));
            }
            return rows;
        }

        /// <summary>
        /// The row that owns one interior sidecar's <c>hull_stem</c>.
        ///
        /// <para>Matched on DECLARATIONS, in decreasing specificity: an exact <c>stem</c>, then a
        /// <c>stem</c> equal to the rig part of a variant stem, then a unique <c>rig</c> file name.
        /// Two rows sharing a rig file with no stem to tell them apart is an ambiguity, not a coin
        /// toss — the sport fishers are exactly that shape, and they are the pair the S0 ledger
        /// refuses.</para>
        /// </summary>
        public static bool TryBind(IReadOnlyList<HullBinding> table, string hullStem,
                                   out HullBinding binding, out string error)
        {
            binding = default;
            error = "";
            if (table == null || table.Count == 0) { error = "the interior rig declares no HULLS."; return false; }

            BoatInteriorHullResolver.Split(hullStem, out string rigStem, out string variant);

            for (int i = 0; i < table.Count; i++)
                if (table[i].DeclaredStem.Length > 0 &&
                    string.Equals(table[i].DeclaredStem, hullStem, StringComparison.Ordinal))
                { binding = table[i]; return true; }

            for (int i = 0; i < table.Count; i++)
                if (table[i].DeclaredStem.Length > 0 &&
                    string.Equals(table[i].DeclaredStem, rigStem, StringComparison.Ordinal))
                { binding = table[i]; return true; }

            var matches = new List<HullBinding>();
            for (int i = 0; i < table.Count; i++)
                if (string.Equals(table[i].ExteriorRigStem, rigStem, StringComparison.Ordinal))
                    matches.Add(table[i]);

            if (matches.Count == 1) { binding = matches[0]; return true; }

            if (matches.Count == 0)
            {
                error = $"no row of the interior rig's HULLS table is drawn from '{rigStem}.js', so " +
                        $"'{hullStem}' names a hull this renderer cannot draw.";
                return false;
            }

            var keys = new List<string>();
            foreach (var m in matches) keys.Add(m.Key);
            keys.Sort(StringComparer.Ordinal);
            error = $"{matches.Count} rows of HULLS are drawn from '{rigStem}.js' ({string.Join(", ", keys)}) " +
                    $"and none declares the stem '{hullStem}'. Ambiguous — a room must belong to one boat.";
            return false;
        }

        /// <summary>
        /// Install the exterior rig this hull needs, then the interior renderer, into a fresh host.
        ///
        /// <para>⚠️ <b>The exterior comes from the KIT's <c>hull-rigs/</c>, never from
        /// <c>docs/art/rigs/</c>.</b> The repository's copy is by definition the version BEFORE the
        /// drop published the loft, so <c>hullEnv()</c> would resolve to null against it and every
        /// hull would render four bytes. The question of whether the repository has since diverged
        /// from the kit's rig is a different one, it is Axis B of the S0 adjudication, and it is
        /// already answered per hull in the committed ledger.</para>
        /// </summary>
        public static void Install(IRigScriptHost host, in HullBinding binding)
        {
            string exterior = KitPath("hull-rigs", binding.ExteriorRigFileName);
            if (!File.Exists(exterior))
                throw new FileNotFoundException(
                    $"the interior rig names '{binding.ExteriorRigFileName}' as {binding.Key}'s exterior, " +
                    $"but it is not in {BoatInteriorKit.KitFolder}/hull-rigs/. Without it every cell " +
                    "renders four bytes and nothing throws.", exterior);

            host.Execute(File.ReadAllText(exterior));

            if (!host.EvaluateBool($"typeof {binding.ExteriorGlobal} === 'object' && " +
                                   $"{binding.ExteriorGlobal} !== null"))
                throw new InvalidOperationException(
                    $"'{binding.ExteriorRigFileName}' ran but installed no globalThis." +
                    $"{binding.ExteriorGlobal}, which is the symbol the interior rig looks for.");

            host.Execute(File.ReadAllText(KitPath(BoatInteriorKit.InteriorRigFileName)));

            if (!host.EvaluateBool($"typeof {Global} === 'object' && {Global} !== null"))
                throw new InvalidOperationException(
                    $"{BoatInteriorKit.InteriorRigFileName} ran but installed no globalThis.{Global}.");
        }

        /// <summary>
        /// The variant, as a JS OBJECT LITERAL, or an empty string for a rig that makes one boat.
        ///
        /// <para>⚠️ <b>An object, and never the joined string.</b> The variants rig resolves
        /// <c>v.size</c>, <c>v.style</c> and <c>v.region</c> off whatever it is handed; a string has
        /// none of the three, so it falls through to <c>standard/hardtop/northumberland</c> with no
        /// error. Measured: hand the eighteen variants their own joined stems and all eighteen render
        /// ONE identical picture.</para>
        /// </summary>
        public static string VariantLiteral(string size, string style, string region)
        {
            if (string.IsNullOrEmpty(size) && string.IsNullOrEmpty(style) && string.IsNullOrEmpty(region))
                return "";
            return $"{{size:'{size}',style:'{style}',region:'{region}'}}";
        }

        /// <summary>
        /// The same triple in the shape the EXTERIOR rig wants — <b>at the TOP LEVEL of its options,
        /// not nested under a <c>variant</c> key.</b>
        ///
        /// <para>⚠️ <b>The two rigs spell one fact two ways, and both spellings fail silently on the
        /// other side.</b> The interior renderer reads <c>opts.variant</c> and hands it whole to the
        /// exterior's <c>interiorEnv(v)</c>; the exterior's own <c>render(dir, opts)</c> reads
        /// <c>opts.size</c> / <c>opts.style</c> / <c>opts.region</c> directly (its header says so:
        /// <c>opts = { size, style, region, paint, elev, … }</c>). Hand the exterior a
        /// <c>{variant:{…}}</c> and every one of the three resolves undefined, so it draws
        /// <c>standard/hardtop/northumberland</c> — a real, correct-looking boat that is not the one
        /// you asked for. That is precisely the failure the registration probe exists to catch, so it
        /// must not commit it itself.</para>
        /// </summary>
        public static string ExteriorOptsLiteral(string size, string style, string region)
        {
            if (string.IsNullOrEmpty(size) && string.IsNullOrEmpty(style) && string.IsNullOrEmpty(region))
                return "{}";
            return $"{{size:'{size}',style:'{style}',region:'{region}'}}";
        }

        /// <summary>
        /// One interior cell, with every option written out.
        ///
        /// <para><c>resolve()</c> supplies its own values for <c>weather</c> (0.30), <c>clutter</c>
        /// (true) and the rest. Those defaults are fine; a default that is not recorded anywhere is
        /// not, because nobody can reproduce the bake from the sheet. So the kit states them and this
        /// passes them.</para>
        /// </summary>
        public static string RenderExpression(string hullKey, string level, string variantJs,
                                              string dirLiteral)
            => $"{Global}.render({dirLiteral},{OptsLiteral(hullKey, level, variantJs)})";

        public static string OptsLiteral(string hullKey, string level, string variantJs)
        {
            string variant = string.IsNullOrEmpty(variantJs) ? "null" : variantJs;
            string weather = BoatInteriorKit.BakedWeather.ToString("R", CultureInfo.InvariantCulture);
            string door = BoatInteriorKit.BakedDoorOpen.ToString("R", CultureInfo.InvariantCulture);
            return $"{{hull:'{hullKey}',level:'{level}',variant:{variant}," +
                   $"doorOpen:{door},night:{Js(BoatInteriorKit.BakedNight)}," +
                   $"lamp:{Js(BoatInteriorKit.BakedLamp)},weather:{weather}," +
                   $"clutter:{Js(BoatInteriorKit.BakedClutter)},focus:null,layers:null,outline:false}}";
        }

        static string Js(bool b) => b ? "true" : "false";

        /// <summary>The levels the rig itself will draw for this hull, in its own order.</summary>
        public static string[] LevelsOf(IRigScriptHost host, string hullKey)
        {
            int n = (int)host.EvaluateNumber($"{Global}.levelsOf('{hullKey}').length");
            var levels = new string[n];
            for (int i = 0; i < n; i++)
                levels[i] = host.EvaluateString($"{Global}.levelsOf('{hullKey}')[{i}]");
            return levels;
        }
    }
}
