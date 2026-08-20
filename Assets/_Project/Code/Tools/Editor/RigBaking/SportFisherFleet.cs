using System;
using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// One hull of <c>sportFisherIsoRig2.js</c> — the 53′ convertible or the 90′ skybridge — plus
    /// every name derived from that one word.
    ///
    /// <para><b>She is a REGISTRY, and that makes her the third generator SHAPE, not a third
    /// generator.</b> The lobster and the zodiac build their hulls from a module-level function and
    /// publish one cell, one camera and one set of anchors on the global. This rig publishes a LIST
    /// of hull objects, and every per-hull fact lives on the object: cell, pivot, elevation, render,
    /// rock and nav mounts. Measured in the repo's own V8, 2026-08-15 — her global carries no
    /// <c>defaultElev</c>, no <c>render</c>, no <c>ROCK</c> and no <c>navMounts</c> at all. Hence
    /// <see cref="RigHullExtraction.HullScope"/>, which is what this type contributes over
    /// <see cref="LobsterVariant"/> and <see cref="ZodiacBuild"/>.</para>
    /// </summary>
    public readonly struct SportFisherHull
    {
        /// <summary>The rig's own hull id: convertible / skybridge.</summary>
        public readonly string Hull;

        /// <summary>LOA in metres, from the rig's own <c>L</c> and her sidecar's <c>frame.LOA_m</c>
        /// (the two agree). Labels only — never geometry.</summary>
        public readonly float LoaMetres;

        public SportFisherHull(string hull, float loaMetres)
        {
            Hull = hull; LoaMetres = loaMetres;
        }

        public string Snake => $"sport_fisher_{Hull}_iso";
        public string Key => $"sportFisher{Pascal(Hull)}";
        public string AssetName => $"SportFisher{Pascal(Hull)}Iso";
        public string SidecarStem => char.ToLowerInvariant(AssetName[0]) + AssetName.Substring(1);
        public string SidecarFileName => $"{SidecarStem}.gameplay.json";

        public string MeshId => $"hullmesh.{Snake}";
        public string VisualId => $"visual.{Snake}";
        public string DeckId => $"deck.{Snake}";

        /// <summary>
        /// The <c>HullPaintSchemeDef</c> id prefix for this hull's four schemes, e.g.
        /// <c>paint.sport_fisher_skybridge_</c>. Trimmed from <see cref="Snake"/> for the reason
        /// <c>LobsterVariant.PaintIdPrefix</c> gives, and qualified PER HULL rather than per rig
        /// because the two tables are different sizes of boat wearing the same four colourways.
        /// </summary>
        public string PaintIdPrefix => $"paint.{Snake.Substring(0, Snake.Length - IsoSuffix.Length)}_";

        /// <summary>The suffix <see cref="Snake"/> carries and <see cref="PaintIdPrefix"/> drops.</summary>
        const string IsoSuffix = "_iso";

        /// <summary>
        /// How the extractor reaches THIS hull — and it needs all three fields, which no previous
        /// family did.
        ///
        /// <list type="bullet">
        ///   <item><see cref="RigHullExtraction.FaceExpression"/> — the shimmed <c>variantFaces(id)</c>,
        ///   which finds the hull by EXACT id and returns her own <c>faceList('stowed')</c>. It
        ///   throws on an unknown id rather than falling back, unlike the rig's own <c>byId</c>.</item>
        ///   <item><see cref="RigHullExtraction.ViewOptions"/> — <c>{}</c>, because on this rig the
        ///   hull is chosen by WHICH OBJECT you call, not by a descriptor argument. Her
        ///   <c>render(dir, opts)</c> takes riggers and paint, never a hull id.</item>
        ///   <item><see cref="RigHullExtraction.HullScope"/> — the object itself. This is the one
        ///   that does the work here.</item>
        /// </list>
        ///
        /// <para>⚠️ <c>byId</c> is used for the SCOPE and cannot be used for the FACES. Reading a
        /// cell off the wrong hull is caught instantly — the mesh would be the wrong size — but
        /// reading the wrong FACE LIST under the right id is invisible except to a geometry hash,
        /// which is why the two reach the hull by different routes and why the hash test exists.
        /// Measured: <c>byId('NONSENSE')</c> returns the convertible, silently.</para>
        /// </summary>
        public RigHullExtraction Extraction => new RigHullExtraction
        {
            FaceExpression = $"{SportFisherFleet.ShimSymbol}('{Hull}')",
            ExtraSymbols = new[] { SportFisherFleet.ShimSymbol },
            ViewOptions = "{}",
            HullScope = $"byId('{Hull}')",
        };

        public string Label => $"sport fisher {Hull} (~{LoaMetres:0.#} m)";

        static string Pascal(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        public override string ToString() => Hull;
    }

    /// <summary>
    /// <b>The two battlewagons one rig file makes.</b>
    ///
    /// <para>⚠️ <b>NOT the sport skiff's v2</b>, despite the two arriving in one drop and the drop's
    /// own folder names inviting the confusion. She is a different model at a different size — 16.2 m
    /// and 27.4 m against the skiff's 7.0 m — and the sport skiff Mk2 is her own separate hull under
    /// <c>hullmesh.sport_skiff_mk2_iso</c>.</para>
    ///
    /// <para><b>Her drafts are an authored judgment call and are NOT re-derived here.</b>
    /// <c>docs/design/fleet-flotation.md</c> §6 records that the draft/LOA band and the sole-ratio
    /// method do not overlap for this type, and the owner ruled her in on 2026-08-14 with
    /// 0.67 m and 1.13 m carried as authored. This table's job is to name her hulls; the numbers stay
    /// where they were ruled, and the owner eyeballs her waterline at the review moorage.</para>
    /// </summary>
    public static class SportFisherFleet
    {
        public const string ScriptPath = "docs/art/rigs/sportFisherIsoRig2.js";
        public const string GlobalName = "SportFisherIso2";

        /// <summary>The name <see cref="RigMeshSymbols.Reconstructions"/> binds her per-hull face
        /// composer to. Same name, same concept, per-file expression — see
        /// <see cref="ZodiacFleet.ShimSymbol"/>.</summary>
        public const string ShimSymbol = "variantFaces";

        /// <summary>Her <c>HULLS</c>, in the rig's own order. Checked against the rig by
        /// <c>SportFisherFleetTests</c>.</summary>
        public static readonly IReadOnlyList<SportFisherHull> All = new[]
        {
            new SportFisherHull("convertible", 16.2f),
            new SportFisherHull("skybridge", 27.4f),
        };

        /// <summary>Her hull ids as one comparable string, for the guard that <see cref="All"/> is
        /// still the whole list.</summary>
        public static string HullSignature(IRigScriptHost host) =>
            host.EvaluateString($"{GlobalName}.HULLS.map(function(h){{return h.id;}}).join(',')");

        public static string ExpectedHullSignature
        {
            get
            {
                var ids = new List<string>(All.Count);
                foreach (SportFisherHull h in All) ids.Add(h.Hull);
                return string.Join(",", ids);
            }
        }

        public static SportFisherHull Get(string key)
        {
            foreach (SportFisherHull h in All) if (h.Key == key) return h;
            throw new ArgumentException($"No sport fisher hull '{key}'.", nameof(key));
        }
    }
}
