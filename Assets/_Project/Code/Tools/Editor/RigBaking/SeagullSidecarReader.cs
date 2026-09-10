using System;
using System.Collections.Generic;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>Everything <see cref="SeagullSidecarReader"/> could read, plus why it refused.</summary>
    public sealed class SeagullSidecarRead
    {
        public string SidecarPath = "";

        /// <summary>The content. Empty — never half-filled — when <see cref="Errors"/> is not.</summary>
        public readonly SeagullGameplay Gameplay = new SeagullGameplay();

        /// <summary>The digest the rig on disk actually hashes to.</summary>
        public string ActualRigSha = "";

        public RigHashMatch HashMatch = RigHashMatch.None;

        /// <summary>Non-empty means NOTHING here may be trusted. A refused read returns an empty
        /// <see cref="Gameplay"/> rather than a partial one, so no caller can mistake half a bird for
        /// a whole one.</summary>
        public readonly List<string> Errors = new List<string>();

        /// <summary>Read, but worth saying out loud — an absent optional section, a line-ending-only
        /// hash match.</summary>
        public readonly List<string> Notes = new List<string>();

        public bool Ok => Errors.Count == 0;
    }

    /// <summary>
    /// Reads the herring gull's <c>hidden-harbours/creature-gameplay@1</c> sidecar and pins it to the
    /// rig it was cut from.
    ///
    /// <para><b>A new schema kind.</b> A creature is not a hull: there is no DECK and no CLEATS, the
    /// frame is a CONTACT POINT rather than a waterline, and the sections are behaviours. It shares
    /// nothing with <see cref="DeckSidecarReader"/> but the pin and the JSON reader, which is why
    /// this is its own file rather than a branch inside one of theirs.</para>
    ///
    /// <para><b>Why editor-side, when the charter said World/ or Art/.</b> The pin hashes
    /// <c>docs/art/rigs/seagullIsoRig.js</c>, and <c>docs/</c> is not in a build — a runtime reader
    /// could never perform the check that is the point of this class. <see cref="DeckSidecarJson"/>
    /// also lives in this editor assembly and no runtime assembly references it, while
    /// <c>JsonUtility</c> cannot express what this sidecar is shaped like (STATES is a map of
    /// heterogeneous objects, TRANSITIONS an array of arrays). So the PARSE is here and the DATA is
    /// <see cref="SeagullGameplay"/> over in <c>Code/Art/</c> — engine-light, no UnityEngine types —
    /// which is what the charter was protecting: the bird's behaviour can consume the data without a
    /// rewrite, and a baker can serialise it into <c>Assets/</c> the way CatchStorageAnchors is.</para>
    ///
    /// <para><b>Absence is data.</b> LAND / PERCH / WATER / FLOCK / ANCHORS_PX are optional per the
    /// file's own <c>extractor_contract</c>: absent means the creature does not support the feature,
    /// and is recorded as a Note with the section's <c>Has…</c> flag left false. <c>frame</c>,
    /// <c>creature</c>, <c>STATES</c> and <c>TRANSITIONS</c> are NOT optional — a creature with no
    /// cell geometry or no states cannot be drawn at all, so their absence is an error.</para>
    /// </summary>
    public static class SeagullSidecarReader
    {
        /// <summary>The one schema this reader accepts.</summary>
        public const string Schema = "hidden-harbours/creature-gameplay@1";

        /// <summary>
        /// Reads the sidecar. <paramref name="rigBytes"/> are the rig file's bytes AS ON DISK. Pass
        /// null with <paramref name="enforceHash"/> false only in a fixture that is testing the parse
        /// and not the pin.
        /// </summary>
        public static SeagullSidecarRead Read(string sidecarJson, string sidecarPath, byte[] rigBytes,
                                              bool enforceHash = true)
        {
            var read = new SeagullSidecarRead { SidecarPath = sidecarPath ?? "" };
            var g = read.Gameplay;

            object root;
            try { root = DeckSidecarJson.Parse(sidecarJson); }
            catch (Exception e)
            {
                read.Errors.Add($"unreadable JSON: {e.Message}");
                return read;
            }

            g.Schema = Str(root, "schema");
            if (!string.Equals(g.Schema, Schema, StringComparison.Ordinal))
            {
                read.Errors.Add(
                    $"WRONG SCHEMA: '{read.SidecarPath}' declares '{g.Schema}', this reader only " +
                    $"reads '{Schema}'. A boat sidecar read as a creature one comes back EMPTY rather " +
                    "than wrong, which is worse — nothing downstream can tell the difference. Not read.");
                g.Schema = "";
                return read;
            }

            g.RigFileName = Str(root, "rig");
            g.ExportSymbol = Str(root, "exportSymbol");
            g.DerivedFromRigSha256 = Str(root, "derivedFromRigSha256");

            if (enforceHash)
            {
                read.HashMatch = DeckSidecarReader.MatchRigHash(rigBytes, g.DerivedFromRigSha256,
                                                                out string actual);
                read.ActualRigSha = actual;
                if (read.HashMatch == RigHashMatch.None)
                {
                    read.Errors.Add(
                        $"STALE: '{g.RigFileName}' hashes to {Short(actual)} but the sidecar was " +
                        $"derived from {Short(g.DerivedFromRigSha256)}. The bird was RESHAPED. " +
                        "Re-run SeagullIso.gameplayGeometry() upstream and land a new drop — never " +
                        "patch a number, and never edit the rig to match (the kit README's own rule). " +
                        "Not read.");
                    return read;
                }
                if (read.HashMatch == RigHashMatch.LineEndingNormalized)
                    read.Notes.Add(
                        $"SHA matches '{g.RigFileName}' only with line endings normalised. The bird is " +
                        "unchanged, but .gitattributes pins this kit to LF (the drop measured 0 CR in " +
                        "either file) precisely so this cannot happen — check the checkout rather " +
                        "than shrugging.");
            }

            if (!ReadFrame(root, read)) return read;
            if (!ReadCreature(root, read)) return read;
            if (!ReadStates(root, read)) return read;
            if (!ReadTransitions(root, read)) return read;

            ReadLand(root, read);
            ReadPerch(root, read);
            ReadWater(root, read);
            ReadFlock(root, read);
            ReadAnchors(root, read);
            return read;
        }

        // ---- required sections ------------------------------------------------------------------

        static bool ReadFrame(object root, SeagullSidecarRead read)
        {
            object f = DeckSidecarJson.Member(root, "frame");
            if (f == null)
            {
                read.Errors.Add("frame missing — without a cell and a pivot nothing can be sliced. " +
                                "Not read.");
                return false;
            }

            var g = read.Gameplay;
            var cell = DeckSidecarJson.AsArray(DeckSidecarJson.Member(f, "cell"));
            var pivot = DeckSidecarJson.AsArray(DeckSidecarJson.Member(f, "pivot"));
            if (cell == null || cell.Count < 2 || pivot == null || pivot.Count < 2)
            {
                read.Errors.Add("frame.cell / frame.pivot must each be a pair of numbers. Not read.");
                return false;
            }

            g.CellWidth = (int)DeckSidecarJson.Float(cell[0]);
            g.CellHeight = (int)DeckSidecarJson.Float(cell[1]);
            g.PivotX = (int)DeckSidecarJson.Float(pivot[0]);
            g.PivotY = (int)DeckSidecarJson.Float(pivot[1]);
            g.PixelsPerMetre = F(f, "scale_px_per_m");
            g.Directions = (int)F(f, "dirs");
            g.Dir0 = Str(f, "dir0");
            g.FrameOrigin = Str(f, "origin");
            g.FrameAxes = Str(f, "axes");

            if (g.CellWidth <= 0 || g.CellHeight <= 0 || g.Directions <= 0 || g.PixelsPerMetre <= 0f)
            {
                read.Errors.Add(
                    $"frame is degenerate: cell {g.CellWidth}x{g.CellHeight}, {g.Directions} dirs, " +
                    $"{g.PixelsPerMetre} px/m. A zero here slices an empty sheet in silence. Not read.");
                return false;
            }
            return true;
        }

        static bool ReadCreature(object root, SeagullSidecarRead read)
        {
            object c = DeckSidecarJson.Member(root, "creature");
            if (c == null)
            {
                read.Errors.Add("creature missing — the world-scale dimensions are the whole point of " +
                                "the section. Not read.");
                return false;
            }

            var g = read.Gameplay;
            g.Label = Str(c, "label");
            g.LengthMetres = F(c, "length_m");
            g.WingspanMetres = F(c, "wingspan_m");
            g.MassKg = F(c, "mass_kg");
            g.DraftMetres = F(c, "draft_m");

            object bz = DeckSidecarJson.Member(c, "body_centre_z");
            g.BodyCentreZStand = F(bz, "stand");
            g.BodyCentreZPerch = F(bz, "perch");
            g.BodyCentreZFloat = F(bz, "float");

            object fp = DeckSidecarJson.Member(c, "footprint_m");
            Pair(DeckSidecarJson.Member(fp, "stand"), out g.FootprintStandX, out g.FootprintStandY);
            Pair(DeckSidecarJson.Member(fp, "wings_open"),
                 out g.FootprintWingsOpenX, out g.FootprintWingsOpenY);

            if (g.WingspanMetres <= 0f)
            {
                read.Errors.Add("creature.wingspan_m is zero — it is what fixes the bird's size on " +
                                "screen at every altitude. Not read.");
                return false;
            }
            return true;
        }

        static bool ReadStates(object root, SeagullSidecarRead read)
        {
            var states = DeckSidecarJson.AsObject(DeckSidecarJson.Member(root, "STATES"));
            if (states == null || states.Count == 0)
            {
                read.Errors.Add("STATES missing or empty — a creature with no states cannot be " +
                                "animated. Not read.");
                return false;
            }

            // ⚠️ The order this yields is the JSON reader's dictionary order and is NOT the sheet's
            // column order. The sheet's order is the rig's own sheetOrder(), which the bake asks the
            // rig for; nothing may infer a column index from this list.
            foreach (var kv in states)
            {
                object v = kv.Value;
                var s = new SeagullState
                {
                    Id = kv.Key,
                    Frames = (int)F(v, "frames"),
                    Milliseconds = (int)F(v, "ms"),
                    SpeedMetresPerSecond = F(v, "v"),
                    ClimbMetresPerSecond = F(v, "vz"),
                    Loop = Bool(v, "loop"),
                    Next = Str(v, "next"),
                    Note = Str(v, "note"),
                };

                Pair(DeckSidecarJson.Member(v, "alt"), out s.AltitudeA, out s.AltitudeB);

                object travel = DeckSidecarJson.Member(v, "travel");
                s.HasTravel = travel != null;
                s.TravelMetres = DeckSidecarJson.Float(travel);

                var cycles = DeckSidecarJson.AsArray(DeckSidecarJson.Member(v, "cycles"));
                s.HasCycles = cycles != null && cycles.Count >= 2;
                if (s.HasCycles)
                {
                    s.CyclesMin = (int)DeckSidecarJson.Float(cycles[0]);
                    s.CyclesMax = (int)DeckSidecarJson.Float(cycles[1]);
                }

                if (s.Frames <= 0 || s.Milliseconds <= 0)
                {
                    read.Errors.Add(
                        $"STATES.{s.Id} has frames={s.Frames} ms={s.Milliseconds}. Either zero means " +
                        "a state that occupies no sheet columns or plays in no time — a silent " +
                        "no-op, not a state. Not read.");
                    return false;
                }
                read.Gameplay.States.Add(s);
            }
            return true;
        }

        static bool ReadTransitions(object root, SeagullSidecarRead read)
        {
            var arr = DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "TRANSITIONS"));
            if (arr == null || arr.Count == 0)
            {
                read.Errors.Add("TRANSITIONS missing or empty — with no edges the bird can never " +
                                "leave the state it starts in. Not read.");
                return false;
            }

            var g = read.Gameplay;
            foreach (object o in arr)
            {
                var pair = DeckSidecarJson.AsArray(o);
                if (pair == null || pair.Count < 2)
                {
                    read.Errors.Add("TRANSITIONS holds an entry that is not a [from, to] pair. Not read.");
                    g.Transitions.Clear();
                    return false;
                }
                string from = DeckSidecarJson.String(pair[0]) ?? "";
                string to = DeckSidecarJson.String(pair[1]) ?? "";

                // An edge naming a state that does not exist is the failure that turns a state
                // machine into a dead end at runtime, one transition deep, in play only.
                if (g.State(from) == null || g.State(to) == null)
                {
                    read.Errors.Add(
                        $"TRANSITIONS declares '{from}'->'{to}' but " +
                        $"{(g.State(from) == null ? "'" + from + "'" : "'" + to + "'")} is not in " +
                        "STATES. Not read.");
                    g.Transitions.Clear();
                    return false;
                }
                g.Transitions.Add(new SeagullTransition { From = from, To = to });
            }

            // Every one-shot's `next` must also be a declared edge, or the state machine and the
            // animation table disagree about where the bird goes when the clip ends.
            foreach (var s in g.States)
                if (!string.IsNullOrEmpty(s.Next) && !g.HasTransition(s.Id, s.Next))
                {
                    read.Errors.Add(
                        $"STATES.{s.Id}.next is '{s.Next}' but TRANSITIONS declares no {s.Id}->{s.Next} " +
                        "edge. The clip would end somewhere the graph forbids. Not read.");
                    g.Transitions.Clear();
                    return false;
                }
            return true;
        }

        // ---- optional sections — absent is a fact, not a failure --------------------------------

        static void ReadLand(object root, SeagullSidecarRead read)
        {
            object l = DeckSidecarJson.Member(root, "LAND");
            if (l == null) { read.Notes.Add("LAND absent — this creature does not land."); return; }

            var g = read.Gameplay;
            g.HasLand = true;
            Pair(DeckSidecarJson.Member(l, "needs_clear_m"), out g.LandNeedsClearX, out g.LandNeedsClearY);
            g.LandApproachIntoWind = Bool(l, "approach_into_wind");
            g.LandMinFlatMetres = F(l, "min_flat_m");
            g.LandNote = Str(l, "note");
        }

        static void ReadPerch(object root, SeagullSidecarRead read)
        {
            object p = DeckSidecarJson.Member(root, "PERCH");
            if (p == null) { read.Notes.Add("PERCH absent — this creature does not perch."); return; }

            var g = read.Gameplay;
            g.HasPerch = true;
            object r = DeckSidecarJson.Member(p, "rules");
            g.PerchMinWidthMetres = F(r, "min_width_m");
            g.PerchMaxWidthForGripMetres = F(r, "max_width_for_grip_m");
            g.PerchFlatOkMinMetres = F(r, "flat_ok_min_m");
            g.PerchMaxSlopeDegrees = F(r, "max_slope_deg");
            g.PerchClearanceAboveMetres = F(r, "clearance_above_m");
            g.PerchExit = Str(p, "exit");
            g.PerchNote = Str(p, "note");

            var cands = DeckSidecarJson.AsArray(DeckSidecarJson.Member(p, "candidates"));
            if (cands != null)
                foreach (object c in cands)
                {
                    string s = DeckSidecarJson.String(c);
                    if (!string.IsNullOrEmpty(s)) g.PerchCandidates.Add(s);
                }

            object feet = DeckSidecarJson.Member(DeckSidecarJson.Member(p, "feet_px"), "dir0");
            g.PerchFootLeft = Offset(DeckSidecarJson.Member(feet, "L"));
            g.PerchFootRight = Offset(DeckSidecarJson.Member(feet, "R"));
        }

        static void ReadWater(object root, SeagullSidecarRead read)
        {
            object w = DeckSidecarJson.Member(root, "WATER");
            if (w == null) { read.Notes.Add("WATER absent — this creature does not swim."); return; }

            var g = read.Gameplay;
            g.HasWater = true;
            g.FloatBodyZMetres = F(w, "float_body_z");
            g.BillAtSurfacePx = Offset(DeckSidecarJson.Member(
                DeckSidecarJson.Member(w, "bill_at_surface_px"), "dir0"));
            g.SplashBurstFrame = (int)F(w, "splash_burst_frame");
            g.SplashRig = Str(w, "splash_rig");
            g.DriftWithCurrent = Bool(w, "drift_with_current");
            g.WaterNote = Str(w, "note");

            object dy = DeckSidecarJson.Member(w, "dive_yield");
            if (dy == null) { read.Notes.Add("WATER.dive_yield absent — a dive catches nothing."); return; }
            g.HasDiveYield = true;
            g.DiveCatchProbability = F(dy, "catch_p");
            g.DiveCatchRig = Str(dy, "catch_rig");
            var species = DeckSidecarJson.AsArray(DeckSidecarJson.Member(dy, "catch_species"));
            if (species != null)
                foreach (object s in species)
                {
                    string id = DeckSidecarJson.String(s);
                    if (!string.IsNullOrEmpty(id)) g.DiveCatchSpecies.Add(id);
                }

            // The burst frame must be a frame `splash` actually has, or the signal never fires.
            var splash = g.State("splash");
            if (splash != null && (g.SplashBurstFrame < 0 || g.SplashBurstFrame >= splash.Frames))
                read.Notes.Add(
                    $"WATER.splash_burst_frame is {g.SplashBurstFrame} but splash has " +
                    $"{splash.Frames} frames — anything hung off that frame would never fire.");
        }

        static void ReadFlock(object root, SeagullSidecarRead read)
        {
            object f = DeckSidecarJson.Member(root, "FLOCK");
            if (f == null) { read.Notes.Add("FLOCK absent — this creature is always a single."); return; }

            var g = read.Gameplay;
            g.HasFlock = true;
            g.FlockRadiusMetres = F(f, "radius_m");
            g.FlockGlideDuty = F(f, "glide_duty");
            g.FlockSpacingMetres = F(f, "spacing_m");
            g.FlockAttractRadiusMetres = F(f, "attract_radius_m");
            g.FlockFleeRadiusMetres = F(f, "flee_radius_m");
            g.FlockSettleAfterSeconds = F(f, "settle_after_s");
            g.FlockNote = Str(f, "note");

            IntPair(DeckSidecarJson.Member(f, "size"), out g.FlockSizeMin, out g.FlockSizeMax);
            Pair(DeckSidecarJson.Member(f, "alt_m"),
                 out g.FlockAltitudeMinMetres, out g.FlockAltitudeMaxMetres);
            Pair(DeckSidecarJson.Member(f, "period_s"),
                 out g.FlockPeriodMinSeconds, out g.FlockPeriodMaxSeconds);
            Pair(DeckSidecarJson.Member(f, "swoop_every_s"),
                 out g.FlockSwoopEveryMinSeconds, out g.FlockSwoopEveryMaxSeconds);

            var attractors = DeckSidecarJson.AsArray(DeckSidecarJson.Member(f, "attractors"));
            if (attractors != null)
                foreach (object o in attractors)
                {
                    string id = Str(o, "id");
                    if (string.IsNullOrEmpty(id)) continue;
                    g.Attractors.Add(new SeagullAttractor
                    {
                        Id = id,
                        Weight = F(o, "weight"),
                        Source = Str(o, "source"),
                    });
                }

            object flee = DeckSidecarJson.Member(f, "flee");
            if (flee != null)
            {
                g.FlockFleeReaction = Str(flee, "reaction");
                g.FlockRegroupSeconds = F(flee, "regroup_s");
            }

            // Read, not built. See SeagullGameplay.HasSteal.
            object steal = DeckSidecarJson.Member(f, "steal");
            if (steal == null) { read.Notes.Add("FLOCK.steal absent — birds take nothing."); return; }
            g.HasSteal = true;
            g.StealFrom = Str(steal, "from");
            g.StealPerBirdSeconds = F(steal, "per_bird_s");
            g.StealTakes = Str(steal, "takes");
            var deterred = DeckSidecarJson.AsArray(DeckSidecarJson.Member(steal, "deterred_by"));
            if (deterred != null)
                foreach (object o in deterred)
                {
                    string s = DeckSidecarJson.String(o);
                    if (!string.IsNullOrEmpty(s)) g.StealDeterredBy.Add(s);
                }
        }

        static void ReadAnchors(object root, SeagullSidecarRead read)
        {
            var a = DeckSidecarJson.AsObject(DeckSidecarJson.Member(root, "ANCHORS_PX"));
            if (a == null) { read.Notes.Add("ANCHORS_PX absent — no named points on this creature."); return; }

            foreach (var kv in a)
            {
                var parts = DeckSidecarJson.AsObject(kv.Value);
                if (parts == null) continue;   // "note" is a string member of the same object
                var byPart = new Dictionary<string, SeagullOffsetPx>(StringComparer.Ordinal);
                foreach (var p in parts)
                {
                    var off = Offset(p.Value);
                    if (off != null) byPart[p.Key] = off;
                }
                if (byPart.Count > 0) read.Gameplay.AnchorsPx[kv.Key] = byPart;
            }

            if (read.Gameplay.AnchorsPx.Count == 0)
                read.Notes.Add("ANCHORS_PX present but held no {dx,dy,z} points.");
        }

        // ---- small readers ----------------------------------------------------------------------

        static string Str(object owner, string key) => DeckSidecarJson.String(DeckSidecarJson.Member(owner, key)) ?? "";

        static float F(object owner, string key) => DeckSidecarJson.Float(DeckSidecarJson.Member(owner, key));

        static bool Bool(object owner, string key) => DeckSidecarJson.Member(owner, key) is bool b && b;

        static void Pair(object v, out float a, out float b)
        {
            a = 0f; b = 0f;
            var arr = DeckSidecarJson.AsArray(v);
            if (arr == null || arr.Count < 2) return;
            a = DeckSidecarJson.Float(arr[0]);
            b = DeckSidecarJson.Float(arr[1]);
        }

        static void IntPair(object v, out int a, out int b)
        {
            Pair(v, out float fa, out float fb);
            a = (int)fa; b = (int)fb;
        }

        /// <summary>A <c>{dx,dy,z}</c> point, or null when the member is absent or not an object.</summary>
        static SeagullOffsetPx Offset(object v)
        {
            if (DeckSidecarJson.AsObject(v) == null) return null;
            return new SeagullOffsetPx
            {
                Dx = F(v, "dx"),
                Dy = F(v, "dy"),
                Z = F(v, "z"),
            };
        }

        static string Short(string sha) =>
            string.IsNullOrEmpty(sha) ? "(none)" : sha.Substring(0, Math.Min(8, sha.Length));
    }
}
