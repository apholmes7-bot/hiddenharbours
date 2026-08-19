using System;
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>Everything one interior sidecar says, plus how its read went.</summary>
    public sealed class BoatInteriorRead
    {
        public string SidecarPath = "";
        public string HullStem = "";
        public string InteriorRigFileName = "";

        /// <summary>The sidecar's <c>derivedFromRigSha256</c> — the interior RENDERER it claims.</summary>
        public string ExpectedInteriorRigSha = "";
        /// <summary>What the renderer on disk actually hashes to.</summary>
        public string ActualInteriorRigSha = "";
        public RigHashMatch InteriorHashMatch = RigHashMatch.None;

        /// <summary>The sidecar's <c>hullRigSha256</c> value — the exterior loft it was measured
        /// against. One entry, keyed by the hull rig's stem.</summary>
        public string HullRigStem = "";
        public string ExpectedHullRigSha = "";

        public string[] FitsHulls = Array.Empty<string>();
        public int PixelsPerMetre;
        public Vector2Int CellPixels;
        public Vector2Int PivotPixels;
        public bool RidesHullRock;

        public Vector2[] Footprint = Array.Empty<Vector2>();
        public readonly List<BoatInteriorLevel> Levels = new List<BoatInteriorLevel>();
        public BoatInteriorDoor Door;
        public readonly List<BoatInteriorDoor> AdditionalDoors = new List<BoatInteriorDoor>();
        public readonly List<BoatInteriorAnchor> Anchors = new List<BoatInteriorAnchor>();
        public readonly List<BoatInteriorRoute> Routes = new List<BoatInteriorRoute>();

        /// <summary>Fatal problems — a read with any of these must not become an asset.</summary>
        public readonly List<string> Errors = new List<string>();
        /// <summary>Things worth saying out loud that do not stop the import.</summary>
        public readonly List<string> Notes = new List<string>();

        public bool Ok => Errors.Count == 0;
    }

    /// <summary>
    /// <b>Reads the boat-interior sidecars — the intake half of the 2026-08-19 interiors drop.</b>
    /// Sidecar JSON in, hull-local rooms out, with every refusal the intake handoff demands enforced on
    /// the way through. Nothing here touches <c>AssetDatabase</c> or creates a <c>ScriptableObject</c>,
    /// so the whole parse — the hash law, the unknown-field guard, the schema checks — is EditMode
    /// testable from plain strings and bytes, the shape <see cref="DeckSidecarReader"/> already
    /// established.
    ///
    /// <para><b>Four things are refusals, not warnings</b>, and they are the reason this class exists
    /// rather than a JsonUtility call:</para>
    /// <list type="number">
    /// <item><b>An absent or mismatched interior-renderer SHA.</b> The sidecar-hash law: a sidecar you
    /// cannot check against the renderer that generated it is a sidecar you cannot trust. Twenty-five of
    /// the drop's twenty-seven pin a renderer that is in neither the kit nor the repository's history,
    /// so this arm is not hypothetical — it is most of the drop.</item>
    /// <item><b>An absent or mismatched exterior hull SHA.</b> Same law, other axis: rooms measured
    /// against a loft that has moved are rooms in the wrong place.</item>
    /// <item><b>A hull the S0 adjudication marked refused</b>
    /// (<c>docs/art/rigs/boat-interiors-intake/s0-verdicts.json</c>). The cape's washboards moved by
    /// owner ruling after her interior was measured; no def may be built for her until she is
    /// re-measured.</item>
    /// <item><b>An unknown field it would otherwise drop.</b> Silently ignoring a section the drop
    /// thought it was shipping is how a feature goes missing without anyone noticing. Every key this
    /// reader does not understand is named.</item>
    /// </list>
    /// </summary>
    public static class BoatInteriorSidecarReader
    {
        /// <summary>Top-level keys this reader understands. Anything else in a sidecar is reported —
        /// see refusal 4 in the class doc. Append here in the same PR that starts reading a key.</summary>
        static readonly HashSet<string> KnownRootKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "schema", "hull_stem", "fits_hulls", "interior_rig",
            "derivedFromRigSha256", "hullRigSha256",
            "frame", "cell", "motion",
            "FOOTPRINT", "WALKABLE", "THRESHOLD", "STAIRS", "LADDER", "INTERACT",
            "mechanism_map", "variant",
            // Underscore-prefixed keys are annotations by the sidecar contract's own convention
            // (_note, _excluded, _stem_note, _confirm…) and are skipped wholesale, not listed.
        };

        /// <summary>The schema this reader was written against. A sidecar declaring anything else is
        /// refused rather than parsed hopefully.</summary>
        public const string Schema = "hidden-harbours/boat-interior@1";

        /// <summary>
        /// Read one interior sidecar.
        /// </summary>
        /// <param name="sidecarJson">the file's contents.</param>
        /// <param name="sidecarPath">repo-relative, for the messages.</param>
        /// <param name="interiorRigBytes">the interior renderer the sidecar names, as stored on disk.
        /// Null skips the interior-SHA check (tests only — the builder never passes null).</param>
        /// <param name="hullRigBytes">the exterior hull rig the sidecar names. Null skips that check.</param>
        /// <param name="enforceHashes">false disables both SHA arms. Tests only.</param>
        public static BoatInteriorRead Read(string sidecarJson, string sidecarPath,
                                            byte[] interiorRigBytes, byte[] hullRigBytes,
                                            bool enforceHashes = true)
        {
            var read = new BoatInteriorRead { SidecarPath = sidecarPath ?? "" };

            object root;
            try { root = DeckSidecarJson.Parse(sidecarJson); }
            catch (Exception e)
            {
                read.Errors.Add($"unreadable JSON: {e.Message}");
                return read;
            }

            string schema = DeckSidecarJson.String(DeckSidecarJson.Member(root, "schema")) ?? "";
            if (!string.Equals(schema, Schema, StringComparison.Ordinal))
            {
                read.Errors.Add($"schema is '{schema}', not '{Schema}'. This reader will not guess at " +
                                "a format it was not written against. Not imported.");
                return read;
            }

            read.HullStem = DeckSidecarJson.String(DeckSidecarJson.Member(root, "hull_stem")) ?? "";
            read.InteriorRigFileName =
                DeckSidecarJson.String(DeckSidecarJson.Member(root, "interior_rig")) ?? "";
            read.ExpectedInteriorRigSha =
                DeckSidecarJson.String(DeckSidecarJson.Member(root, "derivedFromRigSha256")) ?? "";

            ReadHullRigPin(root, read);

            // An unknown section stops everything HERE, before a single coordinate is read. A refused
            // sidecar must contribute no geometry at all, and that has to hold on every arm.
            ReportUnknownRootKeys(root, read);
            if (read.Errors.Count > 0 && !enforceHashes) return read;

            if (enforceHashes && !CheckHashes(read, interiorRigBytes, hullRigBytes)) return read;
            if (read.Errors.Count > 0) return read;

            ReadFitsHulls(root, read);
            ReadFrameAndCell(root, read);
            ReadMotion(root, read);
            ReadFootprint(root, read);
            ReadWalkable(root, read);
            ReadThreshold(root, read);
            ReadStairs(root, read);
            ReadLadders(root, read);
            ReadInteract(root, read);
            CrossCheck(read);
            return read;
        }

        // ---- provenance ----------------------------------------------------------------------------

        /// <summary>
        /// <c>hullRigSha256</c> is an OBJECT keyed by the exterior rig's stem —
        /// <c>{"capeIslanderIsoRig": "a3be…"}</c> — because the sidecar is naming which loft it measured
        /// as well as what that loft hashed to. Exactly one entry is expected; more than one would mean
        /// an interior measured against two hulls, which nothing in the kit does and nothing here would
        /// know how to check.
        /// </summary>
        static void ReadHullRigPin(object root, BoatInteriorRead read)
        {
            var pin = DeckSidecarJson.AsObject(DeckSidecarJson.Member(root, "hullRigSha256"));
            if (pin == null || pin.Count == 0)
            {
                read.Errors.Add("no hullRigSha256 — the sidecar does not say which exterior loft its " +
                                "rooms were measured against, so nothing can check that the loft has " +
                                "not moved. Absent is a refusal (the sidecar-hash law). Not imported.");
                return;
            }
            if (pin.Count > 1)
            {
                read.Errors.Add($"hullRigSha256 names {pin.Count} hull rigs; one interior is measured " +
                                "against one loft. Not imported.");
                return;
            }
            foreach (var kv in pin)
            {
                read.HullRigStem = kv.Key;
                read.ExpectedHullRigSha = kv.Value as string ?? "";
            }
            if (string.IsNullOrWhiteSpace(read.ExpectedHullRigSha))
                read.Errors.Add($"hullRigSha256['{read.HullRigStem}'] is empty. Not imported.");
        }

        /// <summary>Both SHA arms. Returns false when the read must stop.</summary>
        static bool CheckHashes(BoatInteriorRead read, byte[] interiorRigBytes, byte[] hullRigBytes)
        {
            if (string.IsNullOrWhiteSpace(read.ExpectedInteriorRigSha))
            {
                read.Errors.Add("no derivedFromRigSha256 — the sidecar does not say which interior " +
                                "renderer generated it. Absent is a refusal (the sidecar-hash law). " +
                                "Not imported.");
                return false;
            }

            read.InteriorHashMatch = DeckSidecarReader.MatchRigHash(
                interiorRigBytes, read.ExpectedInteriorRigSha, out string actualInterior);
            read.ActualInteriorRigSha = actualInterior;

            if (read.InteriorHashMatch == RigHashMatch.None)
            {
                read.Errors.Add(
                    $"STALE INTERIOR RIG: '{read.InteriorRigFileName}' hashes to {Short(actualInterior)} " +
                    $"but this sidecar was generated by {Short(read.ExpectedInteriorRigSha)}. The " +
                    "renderer it names is not the renderer that shipped, so nothing can verify these " +
                    "rooms against the code that drew them. art-director must regenerate this sidecar " +
                    "against the shipped boatInteriorRig.js (or ship the renderer it names). " +
                    "Not imported.");
                return false;
            }
            if (read.InteriorHashMatch == RigHashMatch.LineEndingNormalized)
                read.Notes.Add($"interior renderer SHA matches only with line endings normalised — " +
                               "the sidecar was generated from a CRLF copy. Geometry is unaffected; " +
                               "art-director should re-stamp from the committed file.");

            // ReadHullRigPin already refused if it could not produce exactly one usable pin; without
            // one there is nothing to hash the loft against, so stop rather than check against "".
            if (string.IsNullOrWhiteSpace(read.ExpectedHullRigSha)) return false;

            RigHashMatch hullMatch = DeckSidecarReader.MatchRigHash(
                hullRigBytes, read.ExpectedHullRigSha, out string actualHull);
            if (hullMatch == RigHashMatch.None)
            {
                read.Errors.Add(
                    $"STALE HULL RIG: '{read.HullRigStem}.js' hashes to {Short(actualHull)} but these " +
                    $"rooms were measured against {Short(read.ExpectedHullRigSha)}. The loft moved " +
                    "since; art-director must re-measure. Not imported.");
                return false;
            }
            if (hullMatch == RigHashMatch.LineEndingNormalized)
                read.Notes.Add($"hull rig SHA for '{read.HullRigStem}' matches only with line endings " +
                               "normalised. Geometry is unaffected; re-stamp from the committed file.");
            return true;
        }

        /// <summary>Refusal 4 — name every top-level key this reader would otherwise drop. Annotation
        /// keys (the contract's leading-underscore convention) are not unknown, they are deliberately
        /// non-normative.</summary>
        static void ReportUnknownRootKeys(object root, BoatInteriorRead read)
        {
            var obj = DeckSidecarJson.AsObject(root);
            if (obj == null) return;
            var unknown = new List<string>();
            foreach (var kv in obj)
            {
                if (kv.Key.Length > 0 && kv.Key[0] == '_') continue;
                if (!KnownRootKeys.Contains(kv.Key)) unknown.Add(kv.Key);
            }
            if (unknown.Count == 0) return;
            unknown.Sort(StringComparer.Ordinal);
            read.Errors.Add(
                $"unknown section(s) this reader would silently drop: {string.Join(", ", unknown)}. " +
                "A section the drop thought it was shipping must not vanish into an importer that " +
                "never heard of it — teach the reader, then re-run. Not imported.");
        }

        // ---- header blocks -------------------------------------------------------------------------

        static void ReadFitsHulls(object root, BoatInteriorRead read)
        {
            var arr = DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "fits_hulls"));
            if (arr == null || arr.Count == 0)
            {
                read.Errors.Add("no fits_hulls — the sidecar does not say which hull this interior is " +
                                "for. Not imported.");
                return;
            }
            var hulls = new List<string>(arr.Count);
            for (int i = 0; i < arr.Count; i++)
            {
                string s = arr[i] as string;
                if (!string.IsNullOrWhiteSpace(s)) hulls.Add(s.Trim());
            }
            read.FitsHulls = hulls.ToArray();
            if (read.FitsHulls.Length == 0)
                read.Errors.Add("fits_hulls holds no usable hull stem. Not imported.");
        }

        /// <summary>
        /// The cell block, and the one number in this whole kit most likely to be assumed rather than
        /// read: <c>scale_px_per_m</c>. The tanker is 16 where the fleet is 32, so an absent value is a
        /// refusal and never a default — a def that silently claimed 32 for her would place every room
        /// at half scale.
        /// </summary>
        static void ReadFrameAndCell(object root, BoatInteriorRead read)
        {
            object frame = DeckSidecarJson.Member(root, "frame");
            object pxRaw = DeckSidecarJson.Member(frame, "scale_px_per_m");
            if (!DeckSidecarJson.TryDouble(pxRaw, out double px) || px <= 0)
            {
                read.Errors.Add("frame.scale_px_per_m is absent or not a positive number. This kit " +
                                "holds TWO pixel grids (the tanker at 16, the fleet at 32) — a default " +
                                "here would place a whole hull's rooms at the wrong scale. Not imported.");
                return;
            }
            read.PixelsPerMetre = (int)Math.Round(px);

            object cell = DeckSidecarJson.Member(root, "cell");
            if (cell == null)
            {
                read.Errors.Add("no cell block — nothing states the sheet size or the pivot. Not imported.");
                return;
            }
            int w = (int)Math.Round(DeckSidecarJson.Float(DeckSidecarJson.Member(cell, "w"), 0f));
            int h = (int)Math.Round(DeckSidecarJson.Float(DeckSidecarJson.Member(cell, "h"), 0f));
            if (w <= 0 || h <= 0)
            {
                read.Errors.Add($"cell is {w}×{h} — not a sheet. Not imported.");
                return;
            }
            read.CellPixels = new Vector2Int(w, h);

            object pivot = DeckSidecarJson.Member(cell, "pivot");
            if (pivot == null)
            {
                read.Errors.Add("cell has no pivot — the interior could not be registered under the " +
                                "exterior. Not imported.");
                return;
            }
            read.PivotPixels = new Vector2Int(
                (int)Math.Round(DeckSidecarJson.Float(DeckSidecarJson.Member(pivot, "x"), 0f)),
                (int)Math.Round(DeckSidecarJson.Float(DeckSidecarJson.Member(pivot, "y"), 0f)));
        }

        static void ReadMotion(object root, BoatInteriorRead read)
        {
            object motion = DeckSidecarJson.Member(root, "motion");
            if (motion == null)
            {
                read.Notes.Add("no motion block — assuming the interior does NOT ride the hull's rock. " +
                               "Every sidecar in this kit states it; a hull that does not is worth a look.");
                return;
            }
            read.RidesHullRock = DeckSidecarJson.Member(motion, "rides_hull_rock") as bool? ?? false;
        }

        static void ReadFootprint(object root, BoatInteriorRead read)
        {
            object fp = DeckSidecarJson.Member(root, "FOOTPRINT");
            var house = DeckSidecarJson.AsArray(DeckSidecarJson.Member(fp, "house"));
            if (house == null || house.Count < 3)
            {
                read.Errors.Add("FOOTPRINT.house is absent or has fewer than 3 points. The footprint is " +
                                "the one thing every level shares — without it 'true to the footprint' " +
                                "has nothing to be true to. Not imported.");
                return;
            }
            read.Footprint = ReadPairs(house, "FOOTPRINT.house", read);
        }

        // ---- sections ------------------------------------------------------------------------------

        /// <summary><c>WALKABLE</c> is an OBJECT keyed by level id, not an array — one entry per
        /// standable plane.</summary>
        static void ReadWalkable(object root, BoatInteriorRead read)
        {
            var walk = DeckSidecarJson.AsObject(DeckSidecarJson.Member(root, "WALKABLE"));
            if (walk == null || walk.Count == 0)
            {
                read.Errors.Add("no WALKABLE section — a measured interior with nowhere to stand is an " +
                                "export fault, not an absence. Not imported.");
                return;
            }

            foreach (var kv in walk)
            {
                string levelId = kv.Key;
                object v = kv.Value;
                var poly = DeckSidecarJson.AsArray(DeckSidecarJson.Member(v, "polygon"));
                if (poly == null || poly.Count < 3)
                {
                    read.Errors.Add($"WALKABLE '{levelId}': polygon absent or fewer than 3 points.");
                    continue;
                }
                if (!DeckSidecarJson.TryDouble(DeckSidecarJson.Member(v, "z"), out double z))
                {
                    read.Errors.Add($"WALKABLE '{levelId}': no 'z' — a sole with no height cannot be " +
                                    "placed against the waterline.");
                    continue;
                }

                var level = new BoatInteriorLevel
                {
                    Id = levelId,
                    SoleZMeters = (float)z,
                    Outline = ReadPairs(poly, $"WALKABLE.{levelId}.polygon", read),
                    Obstructions = ReadObstructions(DeckSidecarJson.Member(v, "obstructions"), levelId, read),
                };
                read.Levels.Add(level);
            }
        }

        static BoatInteriorObstruction[] ReadObstructions(object raw, string levelId, BoatInteriorRead read)
        {
            var arr = DeckSidecarJson.AsArray(raw);
            if (arr == null || arr.Count == 0) return Array.Empty<BoatInteriorObstruction>();

            var list = new List<BoatInteriorObstruction>(arr.Count);
            for (int i = 0; i < arr.Count; i++)
            {
                object e = arr[i];
                string id = DeckSidecarJson.String(DeckSidecarJson.Member(e, "id")) ?? $"obstruction_{i}";
                var fp = DeckSidecarJson.AsArray(DeckSidecarJson.Member(e, "footprint"));
                if (fp == null || fp.Count < 3)
                {
                    read.Errors.Add($"WALKABLE '{levelId}' obstruction '{id}': footprint absent or " +
                                    "fewer than 3 points.");
                    continue;
                }
                list.Add(new BoatInteriorObstruction
                {
                    Id = id,
                    Footprint = ReadPairs(fp, $"{levelId}.{id}.footprint", read),
                    HeightAboveSoleMeters =
                        DeckSidecarJson.Float(DeckSidecarJson.Member(e, "height_above_sole_m"), 0f),
                    Treatment = DeckSidecarJson.String(DeckSidecarJson.Member(e, "treatment")) ?? "",
                });
            }
            return list.ToArray();
        }

        static void ReadThreshold(object root, BoatInteriorRead read)
        {
            object th = DeckSidecarJson.Member(root, "THRESHOLD");
            if (th == null)
            {
                read.Errors.Add("no THRESHOLD — an interior with no way in. Not imported.");
                return;
            }
            read.Door = ReadDoor(th, "THRESHOLD", read);

            // The 90's skylounge carries a second slider onto the aft deck, declared as THRESHOLD.additional.
            var extra = DeckSidecarJson.AsArray(DeckSidecarJson.Member(th, "additional"));
            if (extra == null) return;
            for (int i = 0; i < extra.Count; i++)
            {
                BoatInteriorDoor d = ReadDoor(extra[i], $"THRESHOLD.additional[{i}]", read);
                if (d != null) read.AdditionalDoors.Add(d);
            }
        }

        static BoatInteriorDoor ReadDoor(object node, string where, BoatInteriorRead read)
        {
            var pt = DeckSidecarJson.AsArray(DeckSidecarJson.Member(node, "threshold_point"));
            if (pt == null || pt.Count < 3)
            {
                read.Errors.Add($"{where}: threshold_point absent or not an [x, y, z].");
                return null;
            }

            string mech = (DeckSidecarJson.String(DeckSidecarJson.Member(node, "mechanism")) ?? "")
                .Trim().ToLowerInvariant();
            BoatInteriorDoorMechanism mechanism;
            switch (mech)
            {
                case "sliding": mechanism = BoatInteriorDoorMechanism.Sliding; break;
                case "hinged": mechanism = BoatInteriorDoorMechanism.Hinged; break;
                default:
                    read.Errors.Add($"{where}: mechanism '{mech}' is neither 'sliding' nor 'hinged'. " +
                                    "Guessing which way a door opens is not a thing an importer may do.");
                    return null;
            }

            // A slider states its keep-clear inside `slide`; a hinged leaf inside `swing`.
            object motionBlock = DeckSidecarJson.Member(node, mechanism == BoatInteriorDoorMechanism.Sliding
                ? "slide" : "swing");
            var keepClear = DeckSidecarJson.AsArray(DeckSidecarJson.Member(motionBlock, "keep_clear"));

            object cue = DeckSidecarJson.Member(node, "door_cue");
            return new BoatInteriorDoor
            {
                Id = DeckSidecarJson.String(DeckSidecarJson.Member(node, "id")) ?? "entry",
                Side = DeckSidecarJson.String(DeckSidecarJson.Member(node, "side")) ?? "",
                ClearWidthMeters =
                    DeckSidecarJson.Float(DeckSidecarJson.Member(node, "door_clear_width_m"), 0f),
                ClearHeightMeters =
                    DeckSidecarJson.Float(DeckSidecarJson.Member(node, "door_clear_height_m"), 0f),
                ThresholdPoint = new Vector3(
                    DeckSidecarJson.Float(pt[0], 0f), DeckSidecarJson.Float(pt[1], 0f),
                    DeckSidecarJson.Float(pt[2], 0f)),
                Mechanism = mechanism,
                KeepClear = keepClear != null
                    ? ReadPairs(keepClear, $"{where}.keep_clear", read)
                    : Array.Empty<Vector2>(),
                CueFrames = (int)Math.Round(DeckSidecarJson.Float(DeckSidecarJson.Member(cue, "frames"), 0f)),
                CueMillisecondsPerFrame =
                    DeckSidecarJson.Float(DeckSidecarJson.Member(cue, "suggested_ms_per_frame"), 0f),
                CueReversedOnExit =
                    DeckSidecarJson.Member(cue, "played_reversed_on_exit") as bool? ?? true,
            };
        }

        static void ReadStairs(object root, BoatInteriorRead read)
        {
            object stairs = DeckSidecarJson.Member(root, "STAIRS");
            if (stairs == null) return;                     // a one-level cabin has no companionway
            var ways = DeckSidecarJson.AsArray(DeckSidecarJson.Member(stairs, "companionways"));
            if (ways == null) return;

            for (int i = 0; i < ways.Count; i++)
            {
                object w = ways[i];
                read.Routes.Add(new BoatInteriorRoute
                {
                    Id = DeckSidecarJson.String(DeckSidecarJson.Member(w, "id")) ?? $"companionway_{i}",
                    FromLevel = DeckSidecarJson.String(DeckSidecarJson.Member(w, "from")) ?? "",
                    ToLevel = DeckSidecarJson.String(DeckSidecarJson.Member(w, "to")) ?? "",
                    Mechanism = DeckSidecarJson.String(DeckSidecarJson.Member(w, "mechanism")) ?? "InteriorStair",
                    Exterior = false,
                    TotalRiseMeters = DeckSidecarJson.Float(DeckSidecarJson.Member(w, "total_rise_m"), 0f),
                });
            }
        }

        /// <summary><c>LADDER</c> legs. Absent on every hull whose only route up is her washboards —
        /// absence is data.</summary>
        static void ReadLadders(object root, BoatInteriorRead read)
        {
            var arr = DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "LADDER"));
            if (arr == null) return;

            for (int i = 0; i < arr.Count; i++)
            {
                object l = arr[i];
                float z0 = DeckSidecarJson.Float(DeckSidecarJson.Member(l, "z0"), 0f);
                float z1 = DeckSidecarJson.Float(DeckSidecarJson.Member(l, "z1"), 0f);
                read.Routes.Add(new BoatInteriorRoute
                {
                    Id = DeckSidecarJson.String(DeckSidecarJson.Member(l, "id")) ?? $"ladder_{i}",
                    FromLevel = DeckSidecarJson.String(DeckSidecarJson.Member(l, "from")) ?? "",
                    ToLevel = DeckSidecarJson.String(DeckSidecarJson.Member(l, "to")) ?? "",
                    Mechanism = DeckSidecarJson.String(DeckSidecarJson.Member(l, "kind")) ?? "vertical_ladder",
                    Exterior = DeckSidecarJson.Member(l, "exterior") as bool? ?? false,
                    TotalRiseMeters = Mathf.Abs(z1 - z0),
                });
            }
        }

        static void ReadInteract(object root, BoatInteriorRead read)
        {
            var arr = DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "INTERACT"));
            if (arr == null || arr.Count == 0) return;      // a bare cabin is legal

            for (int i = 0; i < arr.Count; i++)
            {
                object e = arr[i];
                string id = DeckSidecarJson.String(DeckSidecarJson.Member(e, "id")) ?? "";
                if (string.IsNullOrWhiteSpace(id))
                {
                    read.Errors.Add($"INTERACT[{i}] has no id — anchor ids are append-only and a " +
                                    "generated one would not be stable.");
                    continue;
                }
                var rp = DeckSidecarJson.AsArray(DeckSidecarJson.Member(e, "reach_point"));
                if (rp == null || rp.Count < 3)
                {
                    read.Errors.Add($"INTERACT '{id}': reach_point absent or not an [x, y, z].");
                    continue;
                }
                read.Anchors.Add(new BoatInteriorAnchor
                {
                    Id = id,
                    Action = DeckSidecarJson.String(DeckSidecarJson.Member(e, "action")) ?? "",
                    ReachPoint = new Vector3(DeckSidecarJson.Float(rp[0], 0f),
                                             DeckSidecarJson.Float(rp[1], 0f),
                                             DeckSidecarJson.Float(rp[2], 0f)),
                    VisibleFacings = ReadStrings(DeckSidecarJson.Member(e, "visible_facings")),
                });
            }

            // Ids are append-only, so a duplicate inside one file is a generator fault, not a merge.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < read.Anchors.Count; i++)
                if (!seen.Add(read.Anchors[i].Id))
                    read.Errors.Add($"INTERACT '{read.Anchors[i].Id}' appears twice — anchor ids are " +
                                    "append-only and must be unique within a hull.");
        }

        /// <summary>
        /// Checks that need the whole file read first.
        ///
        /// <para>Route ends are NOT required to be levels of this interior: the 53's companionway lands
        /// on <c>bridge_sole</c>, an exterior deck owned by the hull's gameplay sidecar. So an
        /// unresolved end is a NOTE here and is resolved for real by the builder, which has the hull's
        /// DECK ids to hand.</para>
        /// </summary>
        static void CrossCheck(BoatInteriorRead read)
        {
            var levels = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < read.Levels.Count; i++) levels.Add(read.Levels[i].Id);

            for (int i = 0; i < read.Routes.Count; i++)
            {
                BoatInteriorRoute r = read.Routes[i];
                if (!string.IsNullOrEmpty(r.FromLevel) && !levels.Contains(r.FromLevel))
                    read.Notes.Add($"route '{r.Id}' starts at '{r.FromLevel}', which is not a level of " +
                                   "this interior — expected to be one of the hull's exterior decks.");
                if (!string.IsNullOrEmpty(r.ToLevel) && !levels.Contains(r.ToLevel))
                    read.Notes.Add($"route '{r.Id}' ends at '{r.ToLevel}', which is not a level of this " +
                                   "interior — expected to be one of the hull's exterior decks.");
            }
        }

        // ---- primitives ----------------------------------------------------------------------------

        static Vector2[] ReadPairs(List<object> pairs, string where, BoatInteriorRead read)
        {
            var outv = new List<Vector2>(pairs.Count);
            for (int i = 0; i < pairs.Count; i++)
            {
                var p = DeckSidecarJson.AsArray(pairs[i]);
                if (p == null || p.Count < 2)
                {
                    read.Errors.Add($"{where}[{i}] is not an [x, y] pair.");
                    continue;
                }
                outv.Add(new Vector2(DeckSidecarJson.Float(p[0], 0f), DeckSidecarJson.Float(p[1], 0f)));
            }
            return outv.ToArray();
        }

        static string[] ReadStrings(object raw)
        {
            var arr = DeckSidecarJson.AsArray(raw);
            if (arr == null) return Array.Empty<string>();
            var outv = new List<string>(arr.Count);
            for (int i = 0; i < arr.Count; i++)
                if (arr[i] is string s && !string.IsNullOrWhiteSpace(s)) outv.Add(s);
            return outv.ToArray();
        }

        static string Short(string sha)
            => string.IsNullOrEmpty(sha) ? "(none)"
                                         : (sha.Length <= 12 ? sha : sha.Substring(0, 12) + "…");
    }
}
