#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;      // DwellingRigFleet — the table that owns where a sidecar lives

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>HOW BIG THE CAMPER ACTUALLY IS</b> — read from art's own committed gameplay sidecar, so the lot
    /// that parks one cannot drift from the thing it parks.
    ///
    /// <para><b>Why read the file at all instead of typing 8.58 into the lot.</b> The Bantam and the
    /// Clipper are the same loft re-run at two lengths and the owner has not yet picked which one he
    /// wants (see <see cref="StPetersCamperLot.Variant"/>). If the lot carried its own numbers, flipping
    /// that one field would move the camper and silently leave the spacing, the clearing and the doorstep
    /// measured against the other length. Reading the sidecar makes the flip genuinely one field: every
    /// number below re-derives. The sidecars are hash-pinned against the rig (<c>DwellingRigFleetTests</c>
    /// adjudicates), so they are the honest source.</para>
    ///
    /// <para><b>⚠ WHAT THE ORIGIN IS, AND WHY IT IS NOT THE MIDDLE.</b> The sidecar's own frame says
    /// <i>"ground-centre of the BODY footprint (tongue excluded)"</i> — so the pivot is NOT the centre of
    /// the thing you park. The drawbar hangs 1.42 m (Clipper) past the nose of the body, which means the
    /// ground the camper occupies is ASYMMETRIC about its own position: <see cref="NoseMetres"/> forward
    /// and only <see cref="TailMetres"/> back. Half the overall length is the tempting wrong number and it
    /// under-reserves the front by most of a tow hitch. <see cref="FootprintRadiusMetres"/> does it
    /// properly.</para>
    ///
    /// <para><b>⚠ AND IT IS WIDER THAN ITS BODY, on both sides.</b> The door step folds out past the skin
    /// on the curb side and the sewer outlet stands proud of it on the street side, so
    /// <c>hull.width_m</c> — the number a reader reaches for first — is NOT how much ground a parked
    /// camper claims across. <see cref="DoorReachMetres"/> and <see cref="StreetReachMetres"/> are, and
    /// they come from <c>STEP</c> and <c>HOOKUPS</c> rather than from <c>hull</c>.</para>
    ///
    /// <para><b>Greybox-tolerant like everything else on this plot:</b> a missing or unparseable sidecar
    /// returns <see cref="Dimensions.IsValid"/> false rather than throwing, so a partial checkout still
    /// places what it can and says what it could not.</para>
    /// </summary>
    public static class CamperSidecar
    {
        /// <summary>The two lengths the one rig lofts. These are the sidecars' own <c>variant</c> strings
        /// and <see cref="DwellingRigFleet.Dwellings"/>' own keys, so they cannot drift from either.</summary>
        public const string Bantam = "bantam";
        public const string Clipper = "clipper";

        /// <summary>One variant's measured shape — everything the lot needs to park it, and nothing
        /// else.</summary>
        public readonly struct Dimensions
        {
            /// <summary>False when the sidecar is missing or did not parse. Every other field is 0/empty.</summary>
            public readonly bool IsValid;

            /// <summary>The sidecar's own key ("bantam" / "clipper").</summary>
            public readonly string Variant;

            /// <summary>Art's own label, e.g. "Clipper 26". For the hierarchy and the log line.</summary>
            public readonly string Label;

            /// <summary>Body length in metres, tongue EXCLUDED — the box you live in.</summary>
            public readonly float BodyLengthMetres;

            /// <summary>Overall length in metres, drawbar INCLUDED — the ground it takes up.</summary>
            public readonly float OverallLengthMetres;

            /// <summary>Body width in metres at the skin. See the class note: this is not the full
            /// across-the-ground span.</summary>
            public readonly float WidthMetres;

            /// <summary>Overall height in metres.</summary>
            public readonly float HeightMetres;

            /// <summary>Standing headroom inside, in metres — 1.80 m (stooping) on the Bantam, 2.00 m
            /// (standing) on the Clipper. The number the owner's variant call actually turns on.</summary>
            public readonly float HeadroomMetres;

            /// <summary>How far the outermost step tread reaches from the centreline on the DOOR side.</summary>
            public readonly float DoorReachMetres;

            /// <summary>How far the outermost hookup stands proud of the centreline on the STREET side.</summary>
            public readonly float StreetReachMetres;

            public Dimensions(string variant, string label, float bodyLength, float overallLength,
                              float width, float height, float headroom, float doorReach, float streetReach)
            {
                IsValid = true;
                Variant = variant;
                Label = label;
                BodyLengthMetres = bodyLength;
                OverallLengthMetres = overallLength;
                WidthMetres = width;
                HeightMetres = height;
                HeadroomMetres = headroom;
                DoorReachMetres = doorReach;
                StreetReachMetres = streetReach;
            }

            /// <summary>How far the drawbar reaches FORWARD of the position, in metres — the long half.</summary>
            public float NoseMetres => OverallLengthMetres - BodyLengthMetres * 0.5f;

            /// <summary>How far the tail reaches BACK of the position, in metres — the short half.</summary>
            public float TailMetres => BodyLengthMetres * 0.5f;

            /// <summary>Widest reach either side of the centreline, in metres — whichever of the step, the
            /// hookups and half the body sticks out furthest.</summary>
            public float HalfSpanMetres =>
                Mathf.Max(WidthMetres * 0.5f, Mathf.Max(DoorReachMetres, StreetReachMetres));

            /// <summary>
            /// The ground this camper reserves at any heading, measured from its own position.
            ///
            /// <para>The same promise <c>StPetersVillage.FootprintRadiusMetres</c> makes for a house and
            /// <see cref="StPetersGinnyPlot.Shed.FootprintRadiusMetres"/> makes for a shed — a circle,
            /// because a quarter-turned thing presents its diagonal and a rectangle would be a promise
            /// this file cannot keep. Computed from the FURTHEST CORNER rather than from half
            /// the overall length, because the origin is the body centre and the drawbar hangs past it (see
            /// the class note).</para>
            /// </summary>
            public float FootprintRadiusMetres =>
                IsValid
                    ? Mathf.Sqrt(HalfSpanMetres * HalfSpanMetres +
                                 Mathf.Max(NoseMetres, TailMetres) * Mathf.Max(NoseMetres, TailMetres))
                    : 0f;
        }

        /// <summary>Repo-relative path to one variant's sidecar, taken from
        /// <see cref="DwellingRigFleet"/> so the two cannot name different files.</summary>
        public static string PathFor(string variant)
        {
            foreach (var d in DwellingRigFleet.Dwellings)
                if (string.Equals(d.Key, variant, StringComparison.Ordinal)) return d.SidecarPath;
            return null;
        }

        /// <summary>
        /// Read one variant's shape off disk. Returns an invalid <see cref="Dimensions"/> (never throws)
        /// when the variant is unknown, the file is missing, or the blocks it needs are not there.
        /// </summary>
        public static Dimensions Read(string variant)
        {
            string relative = PathFor(variant);
            if (string.IsNullOrEmpty(relative)) return default;

            string full = Path.Combine(RigCatalog.RepoRoot, relative);
            if (!File.Exists(full)) return default;

            string json;
            try { json = File.ReadAllText(full); }
            catch (IOException) { return default; }

            // ⚠️ The document as a whole is NOT JsonUtility-shaped: SOLE carries polygons, which are
            // arrays of arrays, and THRESHOLD carries the door's keep-clear triangle the same way.
            // JsonUtility has no representation for a nested array, so the whole-file parse is not an
            // option — and a regex over the raw text is worse than it looks (`width_m` appears twice,
            // once as the body's 2.20 m and once as the step's 0.60 m; picking the first hit silently
            // makes the camper a third of its width). So: lift the three blocks that ARE flat, and parse
            // each of those. Anything nested is simply never looked at.
            string hullText = Block(json, "\"hull\"", '{', '}');
            string stepText = Block(json, "\"STEP\"", '{', '}');
            string hookupsText = Block(json, "\"HOOKUPS\"", '[', ']');
            if (hullText == null || stepText == null || hookupsText == null) return default;

            HullDto hull;
            StepDto step;
            HookupListDto hookups;
            try
            {
                hull = JsonUtility.FromJson<HullDto>(hullText);
                step = JsonUtility.FromJson<StepDto>(stepText);
                hookups = JsonUtility.FromJson<HookupListDto>("{\"items\":" + hookupsText + "}");
            }
            catch (ArgumentException) { return default; }      // JsonUtility's malformed-input throw

            if (hull == null || step?.treads == null || hookups?.items == null) return default;
            if (hull.body_length_m <= 0f || hull.overall_length_m <= 0f) return default;

            float doorReach = 0f;
            foreach (var t in step.treads)
                if (t != null && t.out_x_m > doorReach) doorReach = t.out_x_m;

            // The street side is −x in the rig's frame ("+x curb side (door side)"), so the reach is the
            // most NEGATIVE hookup x, flipped. A hookup on the A-frame (the propane bottles) sits on the
            // centreline and contributes nothing, which is correct.
            float streetReach = 0f;
            foreach (var k in hookups.items)
                if (k?.pos != null && k.pos.Length >= 1 && -k.pos[0] > streetReach) streetReach = -k.pos[0];

            return new Dimensions(variant, string.IsNullOrEmpty(hull.label) ? variant : hull.label,
                                  hull.body_length_m, hull.overall_length_m, hull.width_m, hull.height_m,
                                  hull.interior_headroom_m, doorReach, streetReach);
        }

        /// <summary>
        /// The text of the value that follows <paramref name="key"/>, from its opening bracket to the
        /// matching closing one, or null if the key is absent or the document is truncated. Depth-counted
        /// rather than "find the next <c>}</c>", because <c>STEP</c> holds an array of tread objects and
        /// <c>HOOKUPS</c> holds an array of position arrays.
        ///
        /// <para>Quote-blind on purpose, and safe here: none of the three blocks contains a bracket
        /// character inside a string value (checked — the notes are prose). A general JSON reader is not
        /// what this file is for; if a future sidecar puts a brace in a note, the block simply fails to
        /// lift and the lot falls back to greybox, which is the honest failure.</para>
        /// </summary>
        static string Block(string json, string key, char open, char close)
        {
            int k = json.IndexOf(key, StringComparison.Ordinal);
            if (k < 0) return null;

            int start = json.IndexOf(open, k + key.Length);
            if (start < 0) return null;

            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == open) depth++;
                else if (json[i] == close && --depth == 0) return json.Substring(start, i - start + 1);
            }
            return null;
        }

        // ---- JsonUtility DTOs ------------------------------------------------------------------------
        // ⚠️ These field names are the JSON's own keys and therefore break the repo's naming convention on
        // purpose: JsonUtility matches by exact field name, so renaming one to PascalCase does not fail to
        // compile — it silently deserialises to 0. The PascalCase reading is Dimensions, above.

        [Serializable] sealed class HullDto
        {
            public string label;
            public float body_length_m;
            public float overall_length_m;
            public float width_m;
            public float height_m;
            public float interior_headroom_m;
        }

        [Serializable] sealed class StepDto
        {
            public TreadDto[] treads;
        }

        [Serializable] sealed class TreadDto
        {
            public float out_x_m;
        }

        [Serializable] sealed class HookupListDto
        {
            public HookupDto[] items;
        }

        [Serializable] sealed class HookupDto
        {
            public string id;
            public float[] pos;
        }
    }
}
#endif
