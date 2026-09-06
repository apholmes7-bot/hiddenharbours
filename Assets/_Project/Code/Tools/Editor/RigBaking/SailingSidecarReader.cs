using System;
using System.Collections.Generic;
using System.Globalization;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One row of a polar grid, as the sidecar states it.</summary>
    public sealed class SailingPolarRow
    {
        public float TrueWindAngleDeg;
        public float TrueWindKn;
        public float BoatSpeedKn;
        public float ApparentWindAngleDeg;
        public float ApparentWindKn;
        public float HeelDeg;

        /// <summary>What the RIG's pose law would draw at this cell — <c>drawing</c>, <c>luffing</c>,
        /// <c>stalled</c>, <c>in irons</c> or <c>becalmed</c>. Not a gameplay state.</summary>
        public string Mode = "";
    }

    /// <summary>One named rig state and the render opts that produce it.</summary>
    public sealed class SailingState
    {
        public string Id = "";
        public readonly Dictionary<string, float> Opts = new Dictionary<string, float>(StringComparer.Ordinal);
    }

    /// <summary>Everything <see cref="SailingSidecarReader"/> could read, plus why it refused.</summary>
    public sealed class SailingSidecarRead
    {
        public string SidecarPath = "";
        public string Schema = "";
        public string RigFileName = "";
        public string ExportSymbol = "";
        public string ExpectedRigSha = "";
        public string ActualRigSha = "";
        public RigHashMatch HashMatch = RigHashMatch.None;

        // HULL_FORM
        public float LoaMeters, LwlMeters, WaterlineBeamMeters, CanoeBodyDraftMeters;
        public float CanoeBodyDisplacementKg, HullSpeedKn, Cp, DisplacementLengthRatio;

        /// <summary>Waterline half-breadths as (y_metres, half_beam_metres) pairs, bow-to-stern order
        /// as shipped. The honest input for anything that needs a waterplane — NOT
        /// <see cref="CanoeBodyDisplacementKg"/>, which excludes keel and bulb.</summary>
        public readonly List<KeyValuePair<float, float>> WaterlineHalfBreadths =
            new List<KeyValuePair<float, float>>();

        // SAIL_PLAN
        public float UpwindSailAreaM2, SailAreaDisplacementRatio, BoomLengthMeters;

        // WATERLINE (from the GEOMETRY sidecar's section, mirrored here when present)
        public float DesignWaterlineZ;

        // WIND / thresholds — the rig is the authority; these are the file's declaration of it.
        public float IronsApparentDeg, BlanketingApparentDeg, BecalmedBelowAwsKn;
        public float LuffBelowAoaDeg, StallAboveAoaDeg, HeelMaxDeg, HeelSaturatesAtAwsKn;

        /// <summary>The true→apparent glue, verbatim from <c>WIND.from_true</c> — the expressions a
        /// drive must reproduce so the sprite and the physics agree. Kept as STRINGS: they are a
        /// contract to implement, not a formula to evaluate here.</summary>
        public readonly Dictionary<string, string> FromTrue = new Dictionary<string, string>(StringComparer.Ordinal);

        // POLAR_REFERENCE
        public string PolarStatusNote = "", PolarModel = "";
        public float NoGoTrueWindDeg;
        public readonly List<float> PolarTrueWindKn = new List<float>();
        public readonly List<float> PolarTrueWindAngleDeg = new List<float>();
        public readonly List<SailingPolarRow> PolarRows = new List<SailingPolarRow>();

        public readonly List<SailingState> States = new List<SailingState>();

        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Notes = new List<string>();

        public bool Ok => Errors.Count == 0;
    }

    /// <summary>
    /// <b>Reads a <c>hidden-harbours/boat-sailing@1</c> sidecar, and refuses anything else.</b>
    ///
    /// <para>The sail rig kit brought a SECOND sidecar schema alongside the fleet's
    /// <c>boat-gameplay-geometry@1</c>: the hull form integrated off the loft, the sail plan, the
    /// pose law sampled, the named states, the manoeuvre paths, the true→apparent glue and a
    /// reference polar. <see cref="DeckSidecarReader"/> reads the geometry file and would find none
    /// of those sections; pointing it at a sailing file would return an empty read rather than an
    /// error, so the two schemas get two readers and this one asserts which file it was handed.</para>
    ///
    /// <para><b>Two refusals, both hard.</b> A schema that is not <see cref="Schema"/> is refused
    /// (not "best effort" — the sections would silently come back empty). A rig whose bytes do not
    /// hash to <c>derivedFromRigSha256</c> is refused by <see cref="DeckSidecarReader.MatchRigHash"/>
    /// on exactly the fleet's existing rule: exact, or LF↔CRLF (which cannot move a vertex), or
    /// STALE. Absent field counts as stale, deliberately — an unstamped sidecar is the defect.</para>
    ///
    /// <para><b>⚠️ POLAR_REFERENCE IS A REFERENCE.</b> The file says so itself. This reader carries
    /// the sentence through to <see cref="SailingSidecarRead.PolarStatusNote"/> so the importer can
    /// stamp it on the asset; nothing here treats a speed as a fact about the boat.</para>
    /// </summary>
    public static class SailingSidecarReader
    {
        /// <summary>The one schema this reader accepts.</summary>
        public const string Schema = "hidden-harbours/boat-sailing@1";

        /// <summary>
        /// Reads the sidecar. <paramref name="rigBytes"/> are the rig file's bytes AS ON DISK; pass
        /// null with <paramref name="enforceHash"/> false only in a fixture that is testing the
        /// parse and not the pin.
        /// </summary>
        public static SailingSidecarRead Read(string sidecarJson, string sidecarPath, byte[] rigBytes,
                                              bool enforceHash = true)
        {
            var read = new SailingSidecarRead { SidecarPath = sidecarPath ?? "" };

            object root;
            try { root = DeckSidecarJson.Parse(sidecarJson); }
            catch (Exception e)
            {
                read.Errors.Add($"unreadable JSON: {e.Message}");
                return read;
            }

            read.Schema = DeckSidecarJson.String(DeckSidecarJson.Member(root, "schema")) ?? "";
            if (!string.Equals(read.Schema, Schema, StringComparison.Ordinal))
            {
                read.Errors.Add(
                    $"WRONG SCHEMA: '{read.SidecarPath}' declares '{read.Schema}', this reader only " +
                    $"reads '{Schema}'. A geometry sidecar read as a sailing one comes back EMPTY " +
                    "rather than wrong, which is worse — nothing downstream can tell the difference. " +
                    "Not read.");
                return read;
            }

            read.RigFileName = DeckSidecarJson.String(DeckSidecarJson.Member(root, "rig")) ?? "";
            read.ExportSymbol = DeckSidecarJson.String(DeckSidecarJson.Member(root, "exportSymbol")) ?? "";
            read.ExpectedRigSha = DeckSidecarJson.String(DeckSidecarJson.Member(root, "derivedFromRigSha256")) ?? "";

            if (enforceHash)
            {
                read.HashMatch = DeckSidecarReader.MatchRigHash(rigBytes, read.ExpectedRigSha, out string actual);
                read.ActualRigSha = actual;
                if (read.HashMatch == RigHashMatch.None)
                {
                    read.Errors.Add(
                        $"STALE: '{read.RigFileName}' hashes to {Short(actual)} but the sidecar was " +
                        $"derived from {Short(read.ExpectedRigSha)}. Regenerate with SAIL_KIT.write() — " +
                        "never patch a number (the kit README's own rule). Not read.");
                    return read;
                }
                if (read.HashMatch == RigHashMatch.LineEndingNormalized)
                    read.Notes.Add(
                        $"SHA matches '{read.RigFileName}' only with line endings normalised. The " +
                        "geometry is unchanged, but .gitattributes pins this kit to LF precisely so " +
                        "this cannot happen — check the checkout rather than shrugging.");
            }

            ReadHullForm(root, read);
            ReadSailPlan(root, read);
            ReadWind(root, read);
            ReadPolar(root, read);
            ReadStates(root, read);
            return read;
        }

        // ---- sections -------------------------------------------------------------------------

        static void ReadHullForm(object root, SailingSidecarRead read)
        {
            object hf = DeckSidecarJson.Member(root, "HULL_FORM");
            if (hf == null) { read.Errors.Add("HULL_FORM missing."); return; }

            read.LoaMeters = F(hf, "LOA_m");
            read.LwlMeters = F(hf, "LWL_m");
            read.WaterlineBeamMeters = F(hf, "BWL_m");
            read.CanoeBodyDraftMeters = F(hf, "canoe_body_draft_m");
            read.CanoeBodyDisplacementKg = F(hf, "displacement_kg");
            read.HullSpeedKn = F(hf, "hull_speed_kn");
            read.Cp = F(hf, "Cp");
            read.DisplacementLengthRatio = F(hf, "DL_ratio");

            // ⚠️ The half-breadths are the honest waterplane input; displacement_kg is CANOE BODY
            // ONLY (the fin keel and bulb are not integrated), so a hull-weight ride that reads
            // displacement as the boat's mass is reading a number the file tells it not to.
            var hb = DeckSidecarJson.AsArray(DeckSidecarJson.Member(hf, "waterline_half_breadths"));
            if (hb != null)
                foreach (object o in hb)
                {
                    var pair = DeckSidecarJson.AsArray(o);
                    if (pair != null && pair.Count >= 2)
                    {
                        read.WaterlineHalfBreadths.Add(new KeyValuePair<float, float>(
                            DeckSidecarJson.Float(pair[0]), DeckSidecarJson.Float(pair[1])));
                        continue;
                    }
                    var obj = DeckSidecarJson.AsObject(o);
                    if (obj != null)
                        read.WaterlineHalfBreadths.Add(new KeyValuePair<float, float>(
                            F(o, "y"), F(o, "half_beam_m")));
                }
            if (read.WaterlineHalfBreadths.Count == 0)
                read.Notes.Add("HULL_FORM.waterline_half_breadths is empty — anything that needs a " +
                               "waterplane must ask the loft, not the displacement.");
        }

        static void ReadSailPlan(object root, SailingSidecarRead read)
        {
            object sp = DeckSidecarJson.Member(root, "SAIL_PLAN");
            if (sp == null) { read.Errors.Add("SAIL_PLAN missing."); return; }
            read.UpwindSailAreaM2 = F(sp, "upwind_area_m2");
            read.SailAreaDisplacementRatio = F(sp, "SA_D");
            read.BoomLengthMeters = F(sp, "boom_length_m");
        }

        static void ReadWind(object root, SailingSidecarRead read)
        {
            object w = DeckSidecarJson.Member(root, "WIND");
            if (w == null) { read.Notes.Add("WIND missing — the true→apparent glue is not declared."); return; }

            // The thresholds live under WIND in this schema, but the rig is the authority and the
            // numbers are re-measured from it in the fixtures; these are read so a consumer that
            // never loads V8 still has them.
            object th = DeckSidecarJson.Member(w, "thresholds") ?? w;
            read.IronsApparentDeg = F(th, "in_irons_below_awa_deg");
            read.BlanketingApparentDeg = F(th, "blanketed_above_awa_deg");
            read.BecalmedBelowAwsKn = F(th, "becalmed_below_aws_kn");
            read.LuffBelowAoaDeg = F(th, "luff_below_aoa_deg");
            read.StallAboveAoaDeg = F(th, "stall_above_aoa_deg");
            read.HeelMaxDeg = F(th, "heel_max_deg");
            read.HeelSaturatesAtAwsKn = F(th, "heel_saturates_at_aws_kn");

            // ⚠️ from_true.heading_deg says "dir * 45 (N = 0, CLOCKWISE)". THE PIXELS SAY OTHERWISE:
            // both sloops step −45°/dir, measured off a port/starboard anchor pair at equal z (see
            // RigCatalog.SailKit). The note describes the rig's own `order` array, which is the label
            // that has been wrong on nineteen of twenty-one directional rigs. The MATHS in this block
            // (twa, aws, awa) is frame-independent and correct; only the dir→heading sentence is not.
            // Carried verbatim so a reader can see the contradiction rather than inherit it.
            var ft = DeckSidecarJson.AsObject(DeckSidecarJson.Member(w, "from_true"));
            if (ft != null)
                foreach (var kv in ft)
                    if (kv.Value is string expr) read.FromTrue[kv.Key] = expr;
        }

        static void ReadPolar(object root, SailingSidecarRead read)
        {
            object p = DeckSidecarJson.Member(root, "POLAR_REFERENCE");
            if (p == null) { read.Errors.Add("POLAR_REFERENCE missing."); return; }

            read.PolarStatusNote = DeckSidecarJson.String(DeckSidecarJson.Member(p, "status")) ?? "";
            object model = DeckSidecarJson.Member(p, "model");
            read.PolarModel = DeckSidecarJson.String(DeckSidecarJson.Member(model, "v_kn")) ?? "";
            read.NoGoTrueWindDeg = ParseLeadingNumberAfter(
                DeckSidecarJson.String(DeckSidecarJson.Member(p, "no_go")) ?? "", "below twa ");

            object grid = DeckSidecarJson.Member(p, "grid");
            AppendFloats(DeckSidecarJson.Member(grid, "tws_kn"), read.PolarTrueWindKn);
            AppendFloats(DeckSidecarJson.Member(grid, "twa_deg"), read.PolarTrueWindAngleDeg);

            var rows = DeckSidecarJson.AsArray(DeckSidecarJson.Member(p, "rows"));
            if (rows == null) { read.Errors.Add("POLAR_REFERENCE.rows missing."); return; }
            foreach (object o in rows)
                read.PolarRows.Add(new SailingPolarRow
                {
                    TrueWindAngleDeg = F(o, "twa"),
                    TrueWindKn = F(o, "tws"),
                    BoatSpeedKn = F(o, "v_kn"),
                    ApparentWindAngleDeg = F(o, "awa"),
                    ApparentWindKn = F(o, "aws"),
                    HeelDeg = F(o, "heel_deg"),
                    Mode = DeckSidecarJson.String(DeckSidecarJson.Member(o, "mode")) ?? "",
                });

            int expected = read.PolarTrueWindKn.Count * read.PolarTrueWindAngleDeg.Count;
            if (expected > 0 && read.PolarRows.Count != expected)
                read.Errors.Add($"POLAR_REFERENCE has {read.PolarRows.Count} rows but the grid " +
                                $"declares {read.PolarTrueWindAngleDeg.Count}×{read.PolarTrueWindKn.Count} " +
                                $"= {expected}. A partial grid indexed row-major reads the wrong cell.");
        }

        static void ReadStates(object root, SailingSidecarRead read)
        {
            var states = DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "STATES"));
            if (states == null) { read.Notes.Add("STATES missing."); return; }
            foreach (object o in states)
            {
                var s = new SailingState { Id = DeckSidecarJson.String(DeckSidecarJson.Member(o, "id")) ?? "" };
                var opts = DeckSidecarJson.AsObject(DeckSidecarJson.Member(o, "opts"));
                if (opts != null)
                    foreach (var kv in opts)
                    {
                        if (DeckSidecarJson.TryDouble(kv.Value, out double d)) s.Opts[kv.Key] = (float)d;
                        else if (kv.Value is bool b) s.Opts[kv.Key] = b ? 1f : 0f;
                    }
                read.States.Add(s);
            }
        }

        // ---- helpers --------------------------------------------------------------------------

        static float F(object owner, string key) => DeckSidecarJson.Float(DeckSidecarJson.Member(owner, key));

        static void AppendFloats(object arrayValue, List<float> into)
        {
            var arr = DeckSidecarJson.AsArray(arrayValue);
            if (arr == null) return;
            foreach (object o in arr) into.Add(DeckSidecarJson.Float(o));
        }

        /// <summary>
        /// Pulls the number that follows <paramref name="marker"/> out of a prose note — the polar's
        /// no-go lives in a sentence (<c>"G = 0 below twa 35."</c>) rather than in a field.
        ///
        /// <para>Returns 0 when the marker is absent, and the CALLER must treat 0 as "not stated",
        /// never as "no no-go angle". A silent 0 here would make every angle sailable.</para>
        /// </summary>
        internal static float ParseLeadingNumberAfter(string text, string marker)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(marker)) return 0f;
            int i = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return 0f;
            int j = i + marker.Length, k = j;
            while (k < text.Length && (char.IsDigit(text[k]) || text[k] == '.' || text[k] == '-')) k++;
            return k > j && float.TryParse(text.Substring(j, k - j), NumberStyles.Float,
                                           CultureInfo.InvariantCulture, out float v)
                ? v : 0f;
        }

        static string Short(string sha) =>
            string.IsNullOrEmpty(sha) ? "(none)" : sha.Substring(0, Math.Min(12, sha.Length));
    }
}
