using System;
using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>What the intake's S0 adjudication concluded about one hull family.</summary>
    public enum BoatInteriorS0Verdict
    {
        /// <summary>The ledger says nothing about this hull. Treated as a refusal: an unadjudicated
        /// hull is not a cleared one, and the whole point of S0 was that no def may be built for a
        /// baseline nobody checked.</summary>
        Unknown = 0,
        /// <summary>Both axes pass — the interior renderer is pinned to the one that shipped, and
        /// main's exterior rig has not drifted in any way that moves measured geometry.</summary>
        Clean = 1,
        /// <summary>Measured loft/house/door geometry moved after the baseline. Fatal; needs an
        /// upstream re-measure, not a re-stamp.</summary>
        StaleFatal = 2,
        /// <summary>The hull baseline is fine but the sidecar's renderer pin cannot be verified —
        /// it names a renderer that did not ship. Cheap to fix upstream, still a refusal here.</summary>
        RefusedPin = 3,
        /// <summary>
        /// The rooms are SOUND, but the exterior rig they were measured against and the one in the
        /// repository are two FORKS of different features that never merged — neither can be adopted
        /// whole.
        ///
        /// <para>Distinct from <see cref="StaleFatal"/> in what it asks for, which is why it is its own
        /// verdict rather than a flavour of that one. Stale means the measurement is wrong and must be
        /// retaken. Forked means the measurement is right and there is nothing to retake — what is
        /// missing is a merged rig, carrying both branches' features, that can land in the repository.
        /// Telling upstream to "re-measure" a forked hull sends them to do work that would not help and
        /// may be impossible (a repo-side rig that publishes no loft cannot be measured against at
        /// all).</para>
        /// </summary>
        ForkedRig = 4,
    }

    /// <summary>One hull's row in the ledger.</summary>
    public sealed class BoatInteriorS0Entry
    {
        public string HullStem = "";
        public BoatInteriorS0Verdict Verdict = BoatInteriorS0Verdict.Unknown;
        public string Evidence = "";
        public string UpstreamAsk = "";
        /// <summary>The hull family whose verdict a set of derived sidecars inherits (the 18 lobster
        /// variants inherit <c>lobsterBoatVariantsIsoRig</c>'s), or empty.</summary>
        public string InheritsTo = "";

        public bool IsClean => Verdict == BoatInteriorS0Verdict.Clean;
    }

    /// <summary>
    /// <b>The S0 adjudication, as data the builder must consult before it builds anything.</b> Reads
    /// <c>docs/art/rigs/boat-interiors-intake/s0-verdicts.json</c>.
    ///
    /// <para><b>Why a ledger and not a re-derivation.</b> S0 asked a question only the repository's
    /// whole history can answer — for six of the nine hulls the bundled <c>_supersedes</c> stamp is
    /// self-referential or absent, and the true baseline had to be found by hashing every version of
    /// that rig across all 588 commits and every ref. An editor session cannot redo that, and a
    /// builder that guessed would be exactly the silent-drift failure the sidecar-hash law exists to
    /// prevent. So the adjudication is committed, with its evidence, and the builder reads it.</para>
    ///
    /// <para><b>Absent means refused.</b> A hull with no row is <see cref="BoatInteriorS0Verdict.Unknown"/>,
    /// and Unknown does not build. Adding a hull to the fleet therefore requires adjudicating it, which
    /// is the point.</para>
    /// </summary>
    public static class BoatInteriorS0Ledger
    {
        /// <summary>Repo-relative path of the committed ledger.</summary>
        public const string LedgerPath = "docs/art/rigs/boat-interiors-intake/s0-verdicts.json";

        /// <summary>The schema this reader was written against.</summary>
        public const string Schema = "hidden-harbours/boat-interior-s0@1";

        /// <summary>
        /// Parse the ledger. Throws <see cref="FormatException"/> on malformed JSON or a schema it does
        /// not know — a ledger that will not parse must stop the build, never default to permissive.
        /// </summary>
        public static Dictionary<string, BoatInteriorS0Entry> Parse(string json)
        {
            object root = DeckSidecarJson.Parse(json);

            string schema = DeckSidecarJson.String(DeckSidecarJson.Member(root, "schema")) ?? "";
            if (!string.Equals(schema, Schema, StringComparison.Ordinal))
                throw new FormatException($"S0 ledger schema is '{schema}', not '{Schema}'.");

            var hulls = DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "hulls"));
            if (hulls == null)
                throw new FormatException("S0 ledger has no 'hulls' array.");

            var map = new Dictionary<string, BoatInteriorS0Entry>(StringComparer.Ordinal);
            for (int i = 0; i < hulls.Count; i++)
            {
                object h = hulls[i];
                string stem = DeckSidecarJson.String(DeckSidecarJson.Member(h, "hull_stem")) ?? "";
                if (string.IsNullOrWhiteSpace(stem))
                    throw new FormatException($"S0 ledger hulls[{i}] has no hull_stem.");

                var entry = new BoatInteriorS0Entry
                {
                    HullStem = stem,
                    Verdict = ParseVerdict(DeckSidecarJson.String(DeckSidecarJson.Member(h, "verdict"))),
                    Evidence = DeckSidecarJson.String(DeckSidecarJson.Member(h, "evidence")) ?? "",
                    UpstreamAsk = DeckSidecarJson.String(DeckSidecarJson.Member(h, "upstream_ask")) ?? "",
                    InheritsTo = DeckSidecarJson.String(DeckSidecarJson.Member(h, "inherits_to")) ?? "",
                };
                map[stem] = entry;
            }
            return map;
        }

        /// <summary>
        /// The verdict for one sidecar's <c>hull_stem</c>.
        ///
        /// <para>A derived stem falls back to its FAMILY: the eighteen
        /// <c>lobsterBoatVariantsIsoRig.&lt;size&gt;_&lt;style&gt;_&lt;region&gt;</c> sidecars have no
        /// row of their own and inherit <c>lobsterBoatVariantsIsoRig</c>'s, exactly as the intake
        /// handoff specifies. The fallback is the text before the first dot, so
        /// <c>sportFisherIsoRig2.convertible</c> — which DOES have its own row — is matched exactly and
        /// never reaches it.</para>
        /// </summary>
        public static BoatInteriorS0Entry For(Dictionary<string, BoatInteriorS0Entry> ledger, string hullStem)
        {
            if (ledger == null || string.IsNullOrWhiteSpace(hullStem))
                return new BoatInteriorS0Entry { HullStem = hullStem ?? "" };

            if (ledger.TryGetValue(hullStem, out BoatInteriorS0Entry exact)) return exact;

            int dot = hullStem.IndexOf('.');
            if (dot > 0)
            {
                string family = hullStem.Substring(0, dot);
                if (ledger.TryGetValue(family, out BoatInteriorS0Entry inherited))
                    return new BoatInteriorS0Entry
                    {
                        HullStem = hullStem,
                        Verdict = inherited.Verdict,
                        Evidence = $"inherited from '{family}': {inherited.Evidence}",
                        UpstreamAsk = inherited.UpstreamAsk,
                        InheritsTo = inherited.InheritsTo,
                    };
            }
            return new BoatInteriorS0Entry { HullStem = hullStem };
        }

        /// <summary>The sentence the builder prints when a hull does not build. Refusals are LOUD by
        /// contract — a skipped hull that said nothing is how a fleet quietly loses a boat.</summary>
        public static string RefusalMessage(BoatInteriorS0Entry entry)
        {
            if (entry == null) return "S0: no entry.";
            switch (entry.Verdict)
            {
                case BoatInteriorS0Verdict.Clean:
                    return $"S0 CLEAN: '{entry.HullStem}'.";
                case BoatInteriorS0Verdict.StaleFatal:
                    return $"S0 STALE-FATAL — REFUSED: '{entry.HullStem}'. Measured geometry moved after " +
                           $"the baseline this interior was cut against. {entry.Evidence} " +
                           $"UPSTREAM: {entry.UpstreamAsk}";
                case BoatInteriorS0Verdict.RefusedPin:
                    return $"S0 REFUSED (unverifiable renderer pin): '{entry.HullStem}'. {entry.Evidence} " +
                           $"UPSTREAM: {entry.UpstreamAsk}";
                case BoatInteriorS0Verdict.ForkedRig:
                    return $"S0 FORKED RIG — REFUSED: '{entry.HullStem}'. The rooms are sound; the rig they " +
                           "were measured against and the repository's are two forks that never merged, so " +
                           $"neither can be adopted whole. {entry.Evidence} " +
                           $"UPSTREAM (a merge, NOT a re-measure): {entry.UpstreamAsk}";
                default:
                    return $"S0 UNKNOWN — REFUSED: '{entry.HullStem}' has no row in {LedgerPath}. An " +
                           "unadjudicated hull is not a cleared one; adjudicate it, then re-run.";
            }
        }

        static BoatInteriorS0Verdict ParseVerdict(string raw)
        {
            switch ((raw ?? "").Trim().ToUpperInvariant())
            {
                case "CLEAN": return BoatInteriorS0Verdict.Clean;
                case "STALE-FATAL": return BoatInteriorS0Verdict.StaleFatal;
                case "REFUSED-PIN": return BoatInteriorS0Verdict.RefusedPin;
                case "FORKED-RIG": return BoatInteriorS0Verdict.ForkedRig;
                default: return BoatInteriorS0Verdict.Unknown;
            }
        }
    }
}
