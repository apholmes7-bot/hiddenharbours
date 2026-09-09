using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>How a sidecar's <c>derivedFromRigSha256</c> lined up against the rig on disk.</summary>
    public enum RigHashMatch
    {
        /// <summary>No reading of the rig hashes to what the sidecar claims. <b>The hull was reshaped
        /// and the sidecar has not been re-derived</b> — the import refuses it (the contract's own
        /// staleness rule) rather than trusting polygons cut from a different boat.</summary>
        None = 0,
        /// <summary>The rig's bytes hash exactly to the sidecar's SHA.</summary>
        Exact = 1,
        /// <summary>
        /// The rig hashes exactly ONCE ITS LINE ENDINGS ARE NORMALISED — the sidecar was derived from a
        /// CRLF working copy of a file the repo stores with LF (or the reverse).
        ///
        /// <para>Accepted, and deliberately: the rule exists to catch a RESHAPED hull, and a line ending
        /// cannot move a vertex. It is still reported on every run, because the honest fix is for
        /// art-director to re-stamp the SHA from the committed file — see the importer's summary.</para>
        /// </summary>
        LineEndingNormalized = 2,
    }

    /// <summary>One walkable area as the sidecar states it, before anything is baked from it.</summary>
    public sealed class SidecarArea
    {
        public string Id = "";
        public bool IsWashboard;
        /// <summary>Hull-local metres, (x abeam, y bow, z up). A flat <c>polygon</c> + <c>z</c> area
        /// arrives with that single z copied onto every vertex.</summary>
        public Vector3[] Vertices = Array.Empty<Vector3>();
    }

    /// <summary>One tie-off point as the sidecar states it.</summary>
    public sealed class SidecarCleat
    {
        public string Id = "";
        public string Type = "";
        public Vector3 Position;
    }

    /// <summary>Everything one sidecar says, plus how its read went.</summary>
    public sealed class SidecarRead
    {
        public string SidecarPath = "";
        public string RigFileName = "";
        public string ExpectedRigSha = "";
        public string ActualRigSha = "";
        public RigHashMatch HashMatch = RigHashMatch.None;
        public float LoaMeters;
        public readonly List<SidecarArea> Areas = new List<SidecarArea>();
        public readonly List<SidecarCleat> Cleats = new List<SidecarCleat>();

        /// <summary>True when this hull's rig publishes a helm station — see
        /// <c>DeckSidecarReader.ReadHelmStation</c>. A flag rather than a magic value: (0, 0, 0) is a
        /// legal station.</summary>
        public bool HasHelmStation;

        /// <summary>Where her pilot stands to steer her, hull-local metres. Meaningless unless
        /// <see cref="HasHelmStation"/>.</summary>
        public Vector3 HelmStation;

        /// <summary>Which sidecar key the station came out of — provenance, quoted by the parity test.</summary>
        public string HelmStationSource = "";
        /// <summary>Fatal problems — a read with any of these must not become an asset.</summary>
        public readonly List<string> Errors = new List<string>();
        /// <summary>Things worth saying out loud that do not stop the import.</summary>
        public readonly List<string> Notes = new List<string>();

        public bool Ok => Errors.Count == 0;
    }

    /// <summary>
    /// <b>Reads the art director's gameplay sidecars — the "no hand transcription" half of M2-37's data
    /// slice.</b> Sidecar JSON in, hull-local polygons out, with the rig-hash staleness rule enforced on
    /// the way through. Nothing here touches <c>AssetDatabase</c> or a <c>ScriptableObject</c>, so the
    /// whole parse — including the hash rule and the washboard construction — is EditMode-testable from
    /// plain strings and bytes.
    ///
    /// <para><b>Why a reader and not an extractor.</b> The rigs export <c>DECK</c> as a scalar deck
    /// HEIGHT, not as polygons; the walkable areas live in the per-hull sidecars beside them
    /// (<c>docs/art/rigs/gameplay/README.md</c>), each derived from that rig's own station maths and
    /// each carrying the SHA of the rig it was cut from. So the honest source of truth here is the
    /// sidecar, and the rig's role is to say whether the sidecar is still describing it — which is
    /// exactly what <see cref="MatchRigHash"/> asks.</para>
    ///
    /// <para><b>Absence is data</b> (the contract's rule, kept literally): a missing <c>WASHBOARD</c>
    /// section is an open boat, a missing <c>CLEATS</c> section is a hull with no modelled tie-off.
    /// Neither is a warning. A missing <c>DECK</c> section IS reported, because every hull that has a
    /// sidecar at all has been measured and an empty one means the export went wrong.</para>
    /// </summary>
    public static class DeckSidecarReader
    {
        /// <summary>Lower-case hex SHA-256 of the given bytes — the form the sidecars record.</summary>
        public static string Sha256Hex(byte[] bytes)
        {
            if (bytes == null) return "";
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>
        /// Does <paramref name="rigBytes"/> still hash to what the sidecar claims? Exact first; then the
        /// same bytes with their line endings flipped to the other convention, which is the one
        /// difference that provably cannot move a vertex. Anything else is <see cref="RigHashMatch.None"/>
        /// — the hull was reshaped, and the sidecar must be re-derived before it is trusted.
        /// </summary>
        /// <summary>
        /// SHA-256 of the given rig bytes with line endings normalised to <b>LF</b> — the one digest
        /// that is the same on every machine.
        ///
        /// <para>No <c>.gitattributes</c> rule covers <c>docs/art/rigs/**/*.js</c> and
        /// <c>core.autocrlf</c> is true on Windows, so a rig is stored LF in the blob and checked out
        /// CRLF. Hashing the raw working-tree bytes therefore records a DIFFERENT number for the same
        /// rig depending on which platform ran the bake — a recorded artifact that is not reproducible
        /// across machines. LF is what git stores and what the sidecars pin
        /// (<c>docs/art/rigs/gameplay/README.md</c>), so it is the form a generated contract should
        /// carry.</para>
        ///
        /// <para><see cref="MatchRigHash"/> accepts either form, so an older contract that recorded the
        /// CRLF digest stays valid — this only changes what a NEW bake writes.</para>
        /// </summary>
        public static string Sha256HexLineEndingNormalised(byte[] bytes)
        {
            if (bytes == null) return "";
            string lf = Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n");
            return Sha256Hex(Encoding.UTF8.GetBytes(lf));
        }

        public static RigHashMatch MatchRigHash(byte[] rigBytes, string expectedSha, out string actualSha)
        {
            actualSha = Sha256Hex(rigBytes);
            if (string.IsNullOrEmpty(expectedSha) || rigBytes == null) return RigHashMatch.None;
            if (string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
                return RigHashMatch.Exact;

            string text = Encoding.UTF8.GetString(rigBytes);
            string lf = text.Replace("\r\n", "\n");
            string crlf = lf.Replace("\n", "\r\n");
            if (string.Equals(Sha256Hex(Encoding.UTF8.GetBytes(crlf)), expectedSha, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Sha256Hex(Encoding.UTF8.GetBytes(lf)), expectedSha, StringComparison.OrdinalIgnoreCase))
                return RigHashMatch.LineEndingNormalized;

            return RigHashMatch.None;
        }

        /// <summary>
        /// <b>Which rig file this sidecar was cut from — asked of the FILE, not of its name.</b>
        ///
        /// <para>Every sidecar has always carried a <c>rig</c> field naming its source; until now the
        /// importer ignored it and derived the rig mechanically from the file name instead
        /// (<c>lobsterBoatIsoRig.gameplay.json</c> → <c>lobsterBoatIsoRig.js</c>). That worked while
        /// every rig made exactly one boat, and it stops working the moment one does not:
        /// <c>lobsterBoatVariantsIsoRig.js</c> makes EIGHTEEN, and eighteen sidecars cannot share one
        /// file name. So the variants' files are named for the HULL
        /// (<c>lobsterStandardHardtopFundyIso.gameplay.json</c>) and the rig is read from the
        /// declaration inside.</para>
        ///
        /// <para><b>Reading the field is also strictly safer than reading the name</b>, which is the
        /// part worth noticing. The staleness rule hashes the rig against
        /// <c>derivedFromRigSha256</c>; with the name as the source of truth, a sidecar could name
        /// rig A in its JSON while being hashed against rig B off its file name, and nothing checked
        /// that the two agreed. Resolving from the field closes that: the file that gets hashed is
        /// the file the sidecar claims to describe.</para>
        ///
        /// <para>The file name remains the fallback for a sidecar with no <c>rig</c> field at all, so
        /// the historic rule still applies where it is the only rule available.</para>
        /// </summary>
        /// <param name="sidecarFileName">e.g. <c>doryIsoRig.gameplay.json</c>.</param>
        /// <param name="sidecarJson">the file's contents; unparseable content falls back to the name.</param>
        public static string ResolveRigFileName(string sidecarFileName, string sidecarJson)
        {
            string declared = null;
            try { declared = DeckSidecarJson.String(DeckSidecarJson.Member(DeckSidecarJson.Parse(sidecarJson), "rig")); }
            catch (Exception) { /* an unreadable sidecar is reported by Read(); fall back to the name */ }

            if (!string.IsNullOrWhiteSpace(declared)) return declared.Trim();

            string stem = (sidecarFileName ?? "").Replace(".gameplay.json", "");
            return stem + ".js";
        }

        /// <summary>
        /// <b>Where that rig file actually IS.</b> Flat first, then anywhere under
        /// <c>docs/art/rigs/</c> — because kits arrive in folders now and a sidecar names a FILE, not
        /// a path.
        ///
        /// <para>Every hull rig before the sail rig kit sat directly in <c>docs/art/rigs/</c>, so a
        /// flat <c>Path.Combine(root, "docs/art/rigs", name)</c> was the whole resolution. The sail
        /// kit lands as <c>sail-rig-kit/sloop-30/sloopIsoRig.js</c> — its two READMEs, its writer and
        /// its stamp script belong beside the rigs, and splitting the kit across two places to satisfy
        /// a lookup would be the tail wagging the dog. So the lookup widened instead.</para>
        ///
        /// <para><b>⚠️ The flat path is tried FIRST, and that is not an optimisation.</b> It
        /// guarantees the thirty-nine committed sidecars resolve to exactly the file they resolved to
        /// before, so this change cannot move any existing hull's staleness check — a recursive search
        /// that happened to find a same-named copy in a subfolder would do precisely that.</para>
        ///
        /// <para><b>⚠️ Ambiguity THROWS.</b> Two files with one name under the rig tree means the
        /// sidecar's <c>rig</c> field no longer identifies a file, and silently taking the first hit
        /// would hash a sidecar against a hull it does not describe — the failure the staleness rule
        /// exists to catch, reintroduced by its own resolver. Returns null when there is no match at
        /// all; the caller reports that as a refusal.</para>
        /// </summary>
        public static string ResolveRigPath(string repoRoot, string rigFileName)
        {
            if (string.IsNullOrWhiteSpace(repoRoot) || string.IsNullOrWhiteSpace(rigFileName)) return null;

            string rigFolder = Path.Combine(repoRoot, "docs", "art", "rigs");
            string flat = Path.Combine(rigFolder, rigFileName);
            if (File.Exists(flat)) return flat;
            if (!Directory.Exists(rigFolder)) return null;

            string[] hits = Directory.GetFiles(rigFolder, rigFileName, SearchOption.AllDirectories);
            if (hits.Length == 1) return hits[0];
            if (hits.Length == 0) return null;

            Array.Sort(hits, StringComparer.Ordinal);
            throw new InvalidOperationException(
                $"AMBIGUOUS RIG: '{rigFileName}' exists {hits.Length} times under docs/art/rigs/ " +
                $"({string.Join(", ", hits)}). A sidecar names a file, so two files with one name " +
                "means the name no longer identifies a hull — and picking the first would hash a " +
                "sidecar against a boat it does not describe. Rename one, or give the sidecar a path.");
        }

        /// <summary>
        /// Read one sidecar. <paramref name="rigBytes"/> is the rig file the sidecar names, as stored;
        /// pass null to skip the staleness check entirely (tests only — the import never does).
        /// </summary>
        public static SidecarRead Read(string sidecarJson, string sidecarPath, byte[] rigBytes,
                                       bool enforceHash = true)
        {
            var read = new SidecarRead { SidecarPath = sidecarPath ?? "" };

            object root;
            try { root = DeckSidecarJson.Parse(sidecarJson); }
            catch (Exception e)
            {
                read.Errors.Add($"unreadable JSON: {e.Message}");
                return read;
            }

            read.RigFileName = DeckSidecarJson.String(DeckSidecarJson.Member(root, "rig")) ?? "";
            read.ExpectedRigSha = DeckSidecarJson.String(DeckSidecarJson.Member(root, "derivedFromRigSha256")) ?? "";
            read.LoaMeters = ReadLoa(root);

            if (enforceHash)
            {
                read.HashMatch = MatchRigHash(rigBytes, read.ExpectedRigSha, out string actual);
                read.ActualRigSha = actual;
                if (read.HashMatch == RigHashMatch.None)
                {
                    read.Errors.Add(
                        $"STALE: '{read.RigFileName}' hashes to {Short(actual)} but the sidecar was derived " +
                        $"from {Short(read.ExpectedRigSha)}. The hull has been reshaped since; art-director " +
                        "must re-derive this sidecar (docs/art/rigs/gameplay/README.md). Not imported.");
                    return read;
                }
                if (read.HashMatch == RigHashMatch.LineEndingNormalized)
                    read.Notes.Add(
                        $"SHA matches '{read.RigFileName}' only with line endings normalised — the sidecar " +
                        "was derived from a CRLF copy of a file the repo stores as LF. The geometry is " +
                        "unchanged (a line ending cannot move a vertex); art-director should re-stamp " +
                        "derivedFromRigSha256 from the committed file.");
            }

            ReadDeckAreas(root, read);
            ReadWashboards(root, read);
            ReadCleats(root, read);
            ReadHelmStation(root, read);
            return read;
        }

        // ---- sections ---------------------------------------------------------------------------

        private static void ReadDeckAreas(object root, SidecarRead read)
        {
            var deck = DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "DECK"));
            if (deck == null || deck.Count == 0)
            {
                read.Errors.Add("no DECK section — a measured hull with no walkable area is an export fault, " +
                                "not an absence. Not imported.");
                return;
            }

            for (int i = 0; i < deck.Count; i++)
            {
                object entry = deck[i];
                string id = DeckSidecarJson.String(DeckSidecarJson.Member(entry, "id")) ?? $"deck_{i}";

                var poly3d = DeckSidecarJson.AsArray(DeckSidecarJson.Member(entry, "polygon3d"));
                var poly = DeckSidecarJson.AsArray(DeckSidecarJson.Member(entry, "polygon"));

                Vector3[] verts;
                if (poly3d != null) verts = ReadTriples(poly3d, id, read);
                else if (poly != null)
                {
                    object zRaw = DeckSidecarJson.Member(entry, "z");
                    if (!DeckSidecarJson.TryDouble(zRaw, out double z))
                    {
                        read.Errors.Add($"DECK '{id}': a flat 'polygon' needs its single 'z'.");
                        continue;
                    }
                    verts = ReadPairs(poly, (float)z, id, read);
                }
                else
                {
                    read.Errors.Add($"DECK '{id}': neither 'polygon' nor 'polygon3d'.");
                    continue;
                }

                if (verts.Length < 3)
                {
                    read.Errors.Add($"DECK '{id}': {verts.Length} vertices — not a polygon.");
                    continue;
                }
                read.Areas.Add(new SidecarArea { Id = id, IsWashboard = false, Vertices = verts });
            }
        }

        /// <summary>
        /// The <c>WASHBOARD</c> strips, built from the authored side's outer edge and its inboard width.
        /// The section is optional and its absence means an open boat — never an error.
        ///
        /// <para>The authored side gives a polyline along the sheer/cap plus <c>width_m</c> measured
        /// INBOARD from it, so the strip is that polyline offset toward the centreline along its own
        /// local normal (which follows the hull's curve rather than assuming a straight side) and closed
        /// back along itself. The opposite side is written as <c>{"mirror": "port across x=0"}</c> and is
        /// built by negating x — the rigs are symmetric about the centreline by construction.</para>
        /// </summary>
        private static void ReadWashboards(object root, SidecarRead read)
        {
            var wbs = DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "WASHBOARD"));
            if (wbs == null || wbs.Count == 0) return;      // open boat — absence is data

            SidecarArea authored = null;
            var pending = new List<string>();               // sides that asked to mirror the authored one

            for (int i = 0; i < wbs.Count; i++)
            {
                object entry = wbs[i];
                string side = DeckSidecarJson.String(DeckSidecarJson.Member(entry, "side")) ?? $"side_{i}";
                string mirror = DeckSidecarJson.String(DeckSidecarJson.Member(entry, "mirror"));
                if (!string.IsNullOrEmpty(mirror)) { pending.Add(side); continue; }

                var edge = DeckSidecarJson.AsArray(DeckSidecarJson.Member(entry, "outer_edge"));
                float width = DeckSidecarJson.Float(DeckSidecarJson.Member(entry, "width_m"), 0f);
                if (edge == null || width <= 0f)
                {
                    read.Errors.Add($"WASHBOARD '{side}': needs both 'outer_edge' and a positive 'width_m'.");
                    continue;
                }

                Vector3[] outer = ReadTriples(edge, $"washboard_{side}", read);
                if (outer.Length < 2)
                {
                    read.Errors.Add($"WASHBOARD '{side}': outer_edge has {outer.Length} points — not a polyline.");
                    continue;
                }

                var area = new SidecarArea
                {
                    Id = $"washboard_{side}",
                    IsWashboard = true,
                    Vertices = BuildStrip(outer, width),
                };
                read.Areas.Add(area);
                if (authored == null) authored = area;
            }

            foreach (string side in pending)
            {
                if (authored == null)
                {
                    read.Errors.Add($"WASHBOARD '{side}': asks to mirror a side that was never authored.");
                    continue;
                }
                read.Areas.Add(new SidecarArea
                {
                    Id = $"washboard_{side}",
                    IsWashboard = true,
                    Vertices = MirrorAcrossCentreline(authored.Vertices),
                });
            }
        }

        private static void ReadCleats(object root, SidecarRead read)
        {
            var cleats = DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "CLEATS"));
            if (cleats == null) return;                     // no modelled tie-off — absence is data

            for (int i = 0; i < cleats.Count; i++)
            {
                object entry = cleats[i];
                string id = DeckSidecarJson.String(DeckSidecarJson.Member(entry, "id")) ?? $"cleat_{i}";
                var pos = DeckSidecarJson.AsArray(DeckSidecarJson.Member(entry, "pos"));
                if (pos == null || pos.Count < 3)
                {
                    read.Errors.Add($"CLEATS '{id}': 'pos' must be [x, y, z].");
                    continue;
                }
                read.Cleats.Add(new SidecarCleat
                {
                    Id = id,
                    Type = DeckSidecarJson.String(DeckSidecarJson.Member(entry, "type")) ?? "",
                    Position = new Vector3(DeckSidecarJson.Float(pos[0]), DeckSidecarJson.Float(pos[1]),
                                           DeckSidecarJson.Float(pos[2])),
                });
            }
        }

        /// <summary>
        /// ⭐ <b>WHERE THIS HULL'S PILOT STANDS TO STEER HER</b>, if her rig publishes it. Hull-local
        /// metres in the sidecars' own frame — the same frame the DECK polygons and the CLEATS are in, so
        /// nothing is converted and nothing can be converted wrongly.
        ///
        /// <para><b>Two shapes, because two generations of sidecar wrote it two ways</b>, and the ladder is
        /// stated here rather than guessed per file. The generator's kits (18 lobster variants, both
        /// sportfishers, sportSkiffMk2, and the cape since 2026-09-09) publish <c>ANCHORS</c> as an
        /// OBJECT keyed by name with <c>{x, y, z}</c> values. The hand-authored ones (the punt, both
        /// zodiacs) publish <c>STATIONS</c> as an ARRAY of <c>{id, type, pos:[x,y,z], provenance}</c>
        /// records — the same shape <see cref="ReadCleats"/> reads, which is this repo's convention for
        /// a list of named points. ⚠ It is NOT a map: reading it as one finds nothing, silently, on three
        /// hulls (measured before CI, 2026-09-09). <c>helm</c> first, then <c>helm_seat</c> — on a boat
        /// you sit at the tiller of, the station IS the seat. First match wins and
        /// <see cref="SidecarRead.HelmStationSource"/> records WHICH: a station whose provenance is not
        /// written down is a constant with extra steps.</para>
        ///
        /// <para><b>Absence is data.</b> Ten shipped hulls publish no station at all (cape islander, dory,
        /// console, coastal packet, the old lobster boat and sport skiff, side dragger, stern trawler and
        /// Mk2, tanker). They import with <c>HasHelmStation</c> false and the switcher falls back to its
        /// own tuned offset, loudly and by name. A missing section is not an error here for the same
        /// reason a missing <c>WASHBOARD</c> is not: it is a hull nobody has measured yet.</para>
        /// </summary>
        private static void ReadHelmStation(object root, SidecarRead read)
        {
            // (1) The generator's map: ANCHORS is an OBJECT keyed by name, values {x, y, z}.
            if (TryAnchorStation(DeckSidecarJson.Member(root, "ANCHORS"), "helm", read)) return;

            // (2) The hand-authored list: STATIONS is an ARRAY of {id, type, pos:[x,y,z], …} — the
            // same record shape CLEATS uses, which is the house convention for a list of named
            // points. 'helm' first, then 'helm_seat': on a boat you sit at the tiller of, the
            // station IS the seat, and the punt says so in her own provenance note.
            List<object> stations = DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "STATIONS"));
            if (TryListStation(stations, "helm", read)) return;
            TryListStation(stations, "helm_seat", read);
        }

        /// <summary>A named member of the ANCHORS map, as an <c>{x, y, z}</c> object. Absent section
        /// or absent key returns false SILENTLY (a hull nobody has measured); a key that is present
        /// and malformed is an ERROR, because a station somebody authored and mistyped is a defect
        /// where an absent one is a decision.</summary>
        private static bool TryAnchorStation(object anchors, string key, SidecarRead read)
        {
            object entry = DeckSidecarJson.Member(anchors, key);
            if (entry == null) return false;

            bool hasX = DeckSidecarJson.TryDouble(DeckSidecarJson.Member(entry, "x"), out double x);
            bool hasY = DeckSidecarJson.TryDouble(DeckSidecarJson.Member(entry, "y"), out double y);
            bool hasZ = DeckSidecarJson.TryDouble(DeckSidecarJson.Member(entry, "z"), out double z);
            if (!hasX || !hasY || !hasZ)
            {
                read.Errors.Add($"ANCHORS '{key}': an anchor must carry x, y and z.");
                return false;
            }

            read.HelmStation = new Vector3((float)x, (float)y, (float)z);
            read.HelmStationSource = "ANCHORS." + key;
            read.HasHelmStation = true;
            return true;
        }

        /// <summary>The STATIONS record whose <c>id</c> is <paramref name="id"/>, read out of its
        /// <c>pos: [x, y, z]</c> — <see cref="ReadCleats"/>'s shape, and validated the same way.</summary>
        private static bool TryListStation(List<object> stations, string id, SidecarRead read)
        {
            if (stations == null) return false;

            for (int i = 0; i < stations.Count; i++)
            {
                object entry = stations[i];
                if (DeckSidecarJson.String(DeckSidecarJson.Member(entry, "id")) != id) continue;

                var pos = DeckSidecarJson.AsArray(DeckSidecarJson.Member(entry, "pos"));
                if (pos == null || pos.Count < 3)
                {
                    read.Errors.Add($"STATIONS '{id}': 'pos' must be [x, y, z].");
                    return false;
                }

                read.HelmStation = new Vector3(DeckSidecarJson.Float(pos[0]), DeckSidecarJson.Float(pos[1]),
                                               DeckSidecarJson.Float(pos[2]));
                read.HelmStationSource = $"STATIONS[id={id}]";
                read.HasHelmStation = true;
                return true;
            }
            return false;
        }

        // ---- geometry helpers --------------------------------------------------------------------

        /// <summary>
        /// A polyline offset <paramref name="width"/> metres toward the centreline and closed back on
        /// itself — the washboard as a walkable strip. Each vertex moves along the local INWARD normal
        /// of the polyline (central difference at the interior vertices, one-sided at the ends), so the
        /// strip keeps its width around the hull's curve instead of shearing where the sheer bends in
        /// toward the bow.
        /// </summary>
        public static Vector3[] BuildStrip(Vector3[] outerEdge, float width)
        {
            if (outerEdge == null || outerEdge.Length < 2) return Array.Empty<Vector3>();

            int n = outerEdge.Length;
            var strip = new Vector3[n * 2];
            for (int i = 0; i < n; i++)
            {
                Vector3 p = outerEdge[i];
                Vector3 prev = outerEdge[Mathf.Max(0, i - 1)];
                Vector3 next = outerEdge[Mathf.Min(n - 1, i + 1)];

                Vector2 tangent = new Vector2(next.x - prev.x, next.y - prev.y);
                if (tangent.sqrMagnitude <= 1e-10f) tangent = Vector2.up;
                tangent.Normalize();

                // Of the two normals, the one that steps TOWARD the centreline (x = 0).
                Vector2 normal = new Vector2(tangent.y, -tangent.x);
                if (normal.x * p.x > 0f) normal = -normal;

                strip[i] = p;                                                   // outer edge, in order
                strip[2 * n - 1 - i] = new Vector3(p.x + normal.x * width,      // inner edge, reversed
                                                   p.y + normal.y * width, p.z);
            }
            return strip;
        }

        /// <summary>The same shape on the other side of the boat: x negated, order reversed so the
        /// mirrored ring still traces one way round. The rigs are symmetric about x = 0 by
        /// construction, which is why the sidecar states the second side as a mirror rather than
        /// re-authoring it.</summary>
        public static Vector3[] MirrorAcrossCentreline(Vector3[] verts)
        {
            if (verts == null) return Array.Empty<Vector3>();
            var mirrored = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[verts.Length - 1 - i];
                mirrored[i] = new Vector3(-v.x, v.y, v.z);
            }
            return mirrored;
        }

        // ---- primitives --------------------------------------------------------------------------

        private static Vector3[] ReadTriples(List<object> rows, string context, SidecarRead read)
        {
            var verts = new List<Vector3>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = DeckSidecarJson.AsArray(rows[i]);
                if (row == null || row.Count < 3)
                {
                    read.Errors.Add($"{context}: vertex {i} is not [x, y, z].");
                    continue;
                }
                verts.Add(new Vector3(DeckSidecarJson.Float(row[0]), DeckSidecarJson.Float(row[1]),
                                      DeckSidecarJson.Float(row[2])));
            }
            return verts.ToArray();
        }

        private static Vector3[] ReadPairs(List<object> rows, float z, string context, SidecarRead read)
        {
            var verts = new List<Vector3>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = DeckSidecarJson.AsArray(rows[i]);
                if (row == null || row.Count < 2)
                {
                    read.Errors.Add($"{context}: vertex {i} is not [x, y].");
                    continue;
                }
                verts.Add(new Vector3(DeckSidecarJson.Float(row[0]), DeckSidecarJson.Float(row[1]), z));
            }
            return verts.ToArray();
        }

        /// <summary>The hull's LOA, wherever this sidecar's schema keeps it: the newer small-craft files
        /// put it in <c>frame.LOA_m</c>, the original drop in <c>hull.loa_m</c>. Provenance only.</summary>
        private static float ReadLoa(object root)
        {
            object frame = DeckSidecarJson.Member(root, "frame");
            if (DeckSidecarJson.TryDouble(DeckSidecarJson.Member(frame, "LOA_m"), out double a)) return (float)a;
            object hull = DeckSidecarJson.Member(root, "hull");
            if (DeckSidecarJson.TryDouble(DeckSidecarJson.Member(hull, "loa_m"), out double b)) return (float)b;
            return 0f;
        }

        private static string Short(string sha)
            => string.IsNullOrEmpty(sha) ? "(none)" : sha.Substring(0, Mathf.Min(12, sha.Length));
    }
}
