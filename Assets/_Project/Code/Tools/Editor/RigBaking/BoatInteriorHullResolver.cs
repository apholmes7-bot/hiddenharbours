using System;
using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One committed hull gameplay sidecar, reduced to the three facts that identify it.</summary>
    public readonly struct HullSidecarIdentity
    {
        /// <summary>The sidecar's file stem — <c>sportFisherConvertibleIso</c>, <c>capeIslanderIsoRig</c>.
        /// This is the HULL's name, and the input to every downstream naming convention.</summary>
        public readonly string FileStem;
        /// <summary>The sidecar's own <c>rig</c> field — <c>sportFisherIsoRig2.js</c>.</summary>
        public readonly string RigFileName;
        /// <summary>
        /// This hull's variant identity, canonicalised — empty for a rig that makes one boat.
        ///
        /// <para><b>Two conventions live in this repo and the resolver has to speak both.</b> The sport
        /// fishers name their variant with a single <c>variant.hull</c> ("convertible"); the eighteen
        /// lobster variants have no <c>hull</c> field at all and identify themselves by a
        /// <c>{size, style, region}</c> triple. Knowing only the first convention silently fails to
        /// resolve all eighteen, which reads as "refused" rather than as a bug. See
        /// <see cref="BoatInteriorHullResolver.VariantKeyOf"/> for the canonical form both collapse to.</para>
        /// </summary>
        public readonly string VariantKey;

        public HullSidecarIdentity(string fileStem, string rigFileName, string variantKey)
        {
            FileStem = fileStem ?? "";
            RigFileName = rigFileName ?? "";
            VariantKey = variantKey ?? "";
        }
    }

    /// <summary>
    /// <b>Which committed hull an interior sidecar belongs to — resolved from FIELDS, never from a file
    /// name.</b>
    ///
    /// <para>An interior sidecar names its hull as <c>&lt;rigStem&gt;</c> or
    /// <c>&lt;rigStem&gt;.&lt;variant&gt;</c> (<c>capeIslanderIsoRig</c>,
    /// <c>sportFisherIsoRig2.convertible</c>). The rest of the project names the same boat by her
    /// gameplay sidecar's file stem (<c>capeIslanderIsoRig</c>, <c>sportFisherConvertibleIso</c>), and
    /// that stem is what <c>deck.&lt;…&gt;</c> ids are built from. The two conventions do NOT transform
    /// into one another — <c>sportFisherIsoRig2.convertible</c> cannot be rewritten into
    /// <c>sportFisherConvertibleIso</c> by any rule that would not also invent wrong answers elsewhere.
    /// </para>
    ///
    /// <para>So the mapping is looked up, not computed: find the committed gameplay sidecar whose own
    /// <c>rig</c> and <c>variant.hull</c> fields match, and take its file stem. This is the same lesson
    /// <c>DeckSidecarReader.ResolveRigFileName</c> already learned — reading the declaration is strictly
    /// safer than reading the name, because a name can be right about a file that is wrong about
    /// itself. An interior whose hull cannot be found this way is REFUSED; guessing an id would attach
    /// a cabin to somebody else's boat.</para>
    /// </summary>
    public static class BoatInteriorHullResolver
    {
        /// <summary>The result of a resolution attempt.</summary>
        public readonly struct Resolution
        {
            /// <summary>The hull's file stem, or empty when unresolved.</summary>
            public readonly string HullFileStem;
            /// <summary>Why it failed, or empty on success.</summary>
            public readonly string Error;

            public Resolution(string hullFileStem, string error)
            {
                HullFileStem = hullFileStem ?? "";
                Error = error ?? "";
            }

            public bool Ok => string.IsNullOrEmpty(Error);
        }

        /// <summary>
        /// Resolve one interior sidecar's <c>hull_stem</c> against the committed hull sidecars.
        /// </summary>
        public static Resolution Resolve(string interiorHullStem, IReadOnlyList<HullSidecarIdentity> catalogue)
        {
            if (string.IsNullOrWhiteSpace(interiorHullStem))
                return new Resolution("", "the interior sidecar states no hull_stem.");
            if (catalogue == null || catalogue.Count == 0)
                return new Resolution("", "no committed hull sidecars to resolve against.");

            Split(interiorHullStem, out string rigStem, out string variant);
            string wantRigFile = rigStem + ".js";

            var matches = new List<string>();
            for (int i = 0; i < catalogue.Count; i++)
            {
                HullSidecarIdentity id = catalogue[i];
                if (!string.Equals(id.RigFileName, wantRigFile, StringComparison.Ordinal)) continue;
                if (!string.Equals(id.VariantKey, variant, StringComparison.Ordinal)) continue;
                matches.Add(id.FileStem);
            }

            if (matches.Count == 1) return new Resolution(matches[0], "");

            if (matches.Count == 0)
                return new Resolution("",
                    $"no committed hull sidecar declares rig '{wantRigFile}'" +
                    (variant.Length > 0 ? $" with variant.hull '{variant}'" : " without a variant") +
                    $". '{interiorHullStem}' names a hull this project does not have; an interior " +
                    "cannot be attached to a guess.");

            matches.Sort(StringComparer.Ordinal);
            return new Resolution("",
                $"{matches.Count} committed hull sidecars declare rig '{wantRigFile}'" +
                (variant.Length > 0 ? $" with variant.hull '{variant}'" : " without a variant") +
                $" ({string.Join(", ", matches)}). Ambiguous — an interior must belong to one boat.");
        }

        /// <summary>The def id for a resolved hull: <c>interior.&lt;hull_def_id&gt;</c>, built from the
        /// same stem-strip-and-snake the deck ids use, so a hull's interior and her deck are named for
        /// the same boat.</summary>
        public static string DefId(string hullFileStem)
            => "interior." + DeckSidecarImporter.SnakeCase(DeckSidecarImporter.StripRigSuffix(hullFileStem));

        /// <summary>The asset file name for a resolved hull — <c>SportFisherConvertibleIso.asset</c>.</summary>
        public static string AssetName(string hullFileStem)
        {
            string stripped = DeckSidecarImporter.StripRigSuffix(hullFileStem);
            return string.IsNullOrEmpty(stripped)
                ? stripped
                : char.ToUpperInvariant(stripped[0]) + stripped.Substring(1);
        }

        /// <summary>
        /// The canonical variant key for a gameplay sidecar's <c>variant</c> node, matching the form an
        /// interior sidecar's <c>hull_stem</c> suffix already uses.
        ///
        /// <para><c>{"hull":"convertible"}</c> → <c>convertible</c> (the sport fishers).
        /// <c>{"size":"standard","style":"hardtop","region":"fundy","paint":"gelcoat"}</c> →
        /// <c>standard_hardtop_fundy</c> (the eighteen lobster variants). Absent or unrecognised →
        /// empty, which is what a one-boat rig carries.</para>
        ///
        /// <para><b><c>paint</c> is deliberately not part of the key.</b> Two paint builds of one hull
        /// share one interior for the same reason they share one deck — paint does not move a bulkhead.</para>
        /// </summary>
        public static string VariantKeyOf(object variantNode)
        {
            if (variantNode == null) return "";

            string hull = DeckSidecarJson.String(DeckSidecarJson.Member(variantNode, "hull"));
            if (!string.IsNullOrWhiteSpace(hull)) return hull.Trim();

            string size = DeckSidecarJson.String(DeckSidecarJson.Member(variantNode, "size"));
            string style = DeckSidecarJson.String(DeckSidecarJson.Member(variantNode, "style"));
            string region = DeckSidecarJson.String(DeckSidecarJson.Member(variantNode, "region"));
            if (!string.IsNullOrWhiteSpace(size) && !string.IsNullOrWhiteSpace(style) &&
                !string.IsNullOrWhiteSpace(region))
                return $"{size.Trim()}_{style.Trim()}_{region.Trim()}";

            return "";
        }

        /// <summary>
        /// The unanimity rule for a <c>hullRigSha256</c> pin: several entries are acceptable iff every
        /// entry holds the identical value AND every key resolves through <see cref="Split"/> to the
        /// same rig stem — the shape a generator produces when it stamps a variant-keyed entry
        /// alongside the rig's own. The STEM, never a raw key, is what names the bundled hull-rig
        /// file: <c>hull-rigs/</c> ships <c>sportFisherIsoRig2.js</c> and not
        /// <c>sportFisherIsoRig2.convertible.js</c>, so a path built from a variant key misses, reads
        /// as a null rig, and turns a good drop into a misdiagnosed HULL RIG MISMATCH. Entries that
        /// disagree on either arm are refused with both sides named — an interior claiming two lofts
        /// is the dangerous shape this gate exists for, and a future variant-with-its-own-rig
        /// legitimately fails here until per-variant pin resolution is its own design.
        /// </summary>
        public static bool TryUnanimousHullRigPin(Dictionary<string, object> pin,
                                                  out string rigStem, out string sha, out string error)
        {
            rigStem = ""; sha = ""; error = "";
            if (pin == null || pin.Count == 0)
            {
                error = "hullRigSha256 is absent or empty.";
                return false;
            }

            // Sorted so every refusal names the same keys in the same order on every run.
            var keys = new List<string>(pin.Keys);
            keys.Sort(StringComparer.Ordinal);

            string stem = null, value = null;
            foreach (string key in keys)
            {
                Split(key, out string keyStem, out _);
                string v = pin[key] as string ?? "";
                if (stem == null) { stem = keyStem; value = v; continue; }

                // Hex is case-insensitive and IsSha256Hex accepts both cases, so unanimity must too.
                if (!string.Equals(v, value, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"hullRigSha256 names {pin.Count} hull rigs with DISAGREEING hashes " +
                            $"({DescribePin(pin, keys)}); one interior is measured against one loft, " +
                            "and nothing here can adjudicate which of them these rooms were " +
                            "measured against.";
                    return false;
                }
                if (!string.Equals(keyStem, stem, StringComparison.Ordinal))
                {
                    error = $"hullRigSha256's keys name different rigs ({string.Join(", ", keys)} — " +
                            $"stems '{stem}' vs '{keyStem}'); several entries are only acceptable " +
                            "when every key is one rig under at most a variant suffix, holding one " +
                            "hash.";
                    return false;
                }
            }

            rigStem = stem;
            sha = value;
            return true;
        }

        static string DescribePin(Dictionary<string, object> pin, List<string> sortedKeys)
        {
            var parts = new List<string>(sortedKeys.Count);
            foreach (string key in sortedKeys)
            {
                string v = pin[key] as string ?? "(null)";
                parts.Add($"{key} = {(v.Length <= 12 ? v : v.Substring(0, 12) + "…")}");
            }
            return string.Join(", ", parts);
        }

        /// <summary>Split <c>sportFisherIsoRig2.convertible</c> into rig stem and variant. A stem with
        /// no dot is a rig that makes one boat, and its variant is empty.</summary>
        public static void Split(string interiorHullStem, out string rigStem, out string variant)
        {
            string s = (interiorHullStem ?? "").Trim();
            int dot = s.IndexOf('.');
            if (dot < 0) { rigStem = s; variant = ""; return; }
            rigStem = s.Substring(0, dot);
            variant = s.Substring(dot + 1);
        }
    }
}
